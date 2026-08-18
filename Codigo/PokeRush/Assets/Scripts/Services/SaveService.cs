using System.Collections.Generic;
using System.Linq;
using SQLite;
using SQLiteConnection = SQLite.SQLiteConnection;

public class SaveService
{
    // Singleton: una sola instancia compartida por todo el juego
    private static SaveService _instance;
    public static SaveService Instance => _instance ??= new SaveService();

    // Acceso directo a la conexion SQLite que mantiene abierta DatabaseService
    private SQLiteConnection _db => DatabaseService.Instance.Connection;

    private SaveService() { }

    // ============ GUARDADO ============

    // Punto de entrada para guardar todo el progreso del jugador
    public void SavePlayer(PlayerModel player, StoryModel story, int playerId = 1)
    {
        // --- Datos basicos del jugador (nombre, dinero) ---
        // Busca si ya existe una fila para este jugador
        var playerRow = _db.Table<PlayerRow>().FirstOrDefault(p => p.Id == playerId);
        if (playerRow == null)
        {
            // No existe: crear fila nueva
            playerRow = new PlayerRow { Id = playerId, Name = player.PlayerName,
                                       Gender = player.Genero.ToString(), Money = player.Money };
            _db.Insert(playerRow);
        }
        else
        {
            // Ya existe: actualizar valores
            playerRow.Name = player.PlayerName;
            playerRow.Gender = player.Genero.ToString();
            playerRow.Money = player.Money;
            _db.Update(playerRow);
        }

        // --- Equipo de Pokens ---
        // Borra todas las filas viejas del equipo de este jugador antes de guardar las nuevas
        // (mas simple que actualizar una por una)
        _db.Table<PlayerPokenRow>().Where(p => p.PlayerId == playerId).ToList()
            .ForEach(row => _db.Delete(row));

        // Guarda cada Poken del equipo actual, en orden (slotIndex = posicion en el equipo)
        for (int i = 0; i < player.Team.Count; i++)
        {
            SavePoken(player.Team[i], playerId, i);
        }

        // --- Inventario ---
        // Mismo patron: borrar todo lo viejo y volver a insertar
        _db.Table<PlayerInventoryRow>().Where(p => p.PlayerId == playerId).ToList()
            .ForEach(row => _db.Delete(row));

        SaveInventory(player.Inventory, playerId);

        // --- Flags de historia ---
        // Mismo patron: borrar todo lo viejo y volver a insertar
        _db.Table<ActiveFlagRow>().Where(f => f.PlayerId == playerId).ToList()
            .ForEach(row => _db.Delete(row));

        // story.ActiveFlags es un HashSet<StoryFlag> (enum) -> se guarda como texto con .ToString()
        foreach (StoryFlag flag in story.ActiveFlags)
        {
            _db.Insert(new ActiveFlagRow { PlayerId = playerId, FlagName = flag.ToString() });
        }
    }

    // Guarda un PokenModel individual y sus movimientos
    private void SavePoken(PokenModel poken, int playerId, int slotIndex)
    {
        var pokenRow = new PlayerPokenRow
        {
            PlayerId = playerId,
            // Se guarda el NOMBRE del ScriptableObject (string), no el objeto en si
            // DefinitionRepository lo usa para volver a encontrarlo al cargar
            PokenDefinitionId = poken.Definition.pokenName,
            Level = poken.Level,
            CurrentXP = poken.CurrentXP,
            CurrentHP = poken.CurrentHP,
            // Los enums se guardan como texto (ej: "Burned", "None")
            StatusEffect = poken.CurrentStatus.ToString(),
            SlotIndex = slotIndex,
            // Si no tiene cosmetico equipado, se guarda null
            EquippedCosmetic = poken.EquippedCosmetic != null ? poken.EquippedCosmetic.cosmeticName : null,
            // IVs como texto "hp,atk,def,spa,spd,spe"
            Ivs = poken.Ivs.Serialize()
        };
        _db.Insert(pokenRow);
        // pokenRow.Id se llena automaticamente despues del Insert (AutoIncrement)
        // por eso podemos usarlo aca abajo para vincular los movimientos a este Poken

        // Guarda los PP restantes de cada movimiento del Poken
        foreach (MoveModel move in poken.Moves)
        {
            _db.Insert(new PlayerPokenMoveRow
            {
                PlayerPokenId = pokenRow.Id, // vincula este movimiento al Poken recien guardado
                MoveDefinitionId = move.Definition.moveName,
                CurrentPP = move.CurrentPP
            });
        }
    }

    // Guarda los tres tipos de items del inventario, cada uno con su ItemType para identificarlo al cargar
    private void SaveInventory(InventoryModel inventory, int playerId)
    {
        foreach (HealItemStack stack in inventory.HealItems)
            _db.Insert(new PlayerInventoryRow { PlayerId = playerId, ItemDefinitionId = stack.Definition.itemName, ItemType = "Heal", Quantity = stack.Quantity });

        foreach (BattleItemStack stack in inventory.BattleItems)
            _db.Insert(new PlayerInventoryRow { PlayerId = playerId, ItemDefinitionId = stack.Definition.itemName, ItemType = "Battle", Quantity = stack.Quantity });

        foreach (SpecialItemStack stack in inventory.SpecialItems)
            _db.Insert(new PlayerInventoryRow { PlayerId = playerId, ItemDefinitionId = stack.Definition.itemName, ItemType = "Special", Quantity = stack.Quantity });
    }

    // Util para saber si existe una partida guardada (ej: mostrar boton "Continuar" en el menu)
    public bool HasSaveData(int playerId = 1)
    {
        return _db.Table<PlayerRow>().Any(p => p.Id == playerId);
    }

    // Borra por completo la partida guardada de este jugador (boton "Borrar partida")
    // Elimina en orden: movimientos -> pokens -> inventario -> flags -> jugador
    public void DeleteSave(int playerId = 1)
    {
        // Primero los movimientos de cada Poken (estan vinculados por PlayerPokenId)
        var pokenRows = _db.Table<PlayerPokenRow>().Where(p => p.PlayerId == playerId).ToList();
        foreach (var pokenRow in pokenRows)
        {
            _db.Table<PlayerPokenMoveRow>().Where(m => m.PlayerPokenId == pokenRow.Id).ToList()
                .ForEach(row => _db.Delete(row));
            _db.Delete(pokenRow);
        }

        _db.Table<PlayerInventoryRow>().Where(i => i.PlayerId == playerId).ToList()
            .ForEach(row => _db.Delete(row));

        _db.Table<ActiveFlagRow>().Where(f => f.PlayerId == playerId).ToList()
            .ForEach(row => _db.Delete(row));

        var playerRow = _db.Table<PlayerRow>().FirstOrDefault(p => p.Id == playerId);
        if (playerRow != null) _db.Delete(playerRow);
    }

    // ============ CARGA ============

    // Punto de entrada para cargar todo el progreso del jugador
    // Devuelve null si no hay datos guardados (partida nueva)
    public PlayerModel LoadPlayer(StoryModel story, int playerId = 1)
    {
        var playerRow = _db.Table<PlayerRow>().FirstOrDefault(p => p.Id == playerId);
        if (playerRow == null) return null; // no hay guardado para este id

        // Crea el PlayerModel con el nombre guardado y le suma el dinero guardado
        // Un guardado viejo no tiene la columna: cae en Varon sin romper.
        System.Enum.TryParse(playerRow.Gender ?? "", out Genero genero);
        var player = new PlayerModel(playerRow.Name, genero);
        player.AddMoney(playerRow.Money);

        // --- Cargar equipo de Pokens ---
        // OrderBy(SlotIndex) asegura que el equipo se reconstruya en el mismo orden en que se guardo
        var pokenRows = _db.Table<PlayerPokenRow>().Where(p => p.PlayerId == playerId).OrderBy(p => p.SlotIndex).ToList();
        foreach (var pokenRow in pokenRows)
        {
            PokenModel poken = LoadPoken(pokenRow);
            if (poken != null) // si la Definition no se encontro, se omite ese Poken
                player.Team.Add(poken);
        }

        // --- Cargar inventario ---
        var inventoryRows = _db.Table<PlayerInventoryRow>().Where(i => i.PlayerId == playerId).ToList();
        LoadInventory(inventoryRows, player.Inventory);

        // --- Cargar flags de historia ---
        var flagRows = _db.Table<ActiveFlagRow>().Where(f => f.PlayerId == playerId).ToList();
        foreach (var flagRow in flagRows)
        {
            // TryParse convierte el texto guardado (ej: "BossDefeated_01") de vuelta al enum StoryFlag
            // Si el texto no corresponde a ningun valor del enum, TryParse devuelve false y se ignora
            if (System.Enum.TryParse<StoryFlag>(flagRow.FlagName, out StoryFlag flag))
                story.ActivateFlag(flag);
        }

        return player;
    }

    // Reconstruye un PokenModel a partir de una fila guardada
    private PokenModel LoadPoken(PlayerPokenRow row)
    {
        // Busca el ScriptableObject original por su nombre guardado
        PokenDefinition definition = DefinitionRepository.Instance.GetPoken(row.PokenDefinitionId);
        if (definition == null) return null; // si no existe (ej: se borro del proyecto), se omite

        // Crea el modelo y le restaura el estado guardado.
        // Si el save tiene IVs los usa; si es un save viejo (columna NULL),
        // el constructor normal genera IVs nuevos al azar.
        PokenModel poken = IvSet.TryParse(row.Ivs, out IvSet ivs)
            ? new PokenModel(definition, row.Level, ivs)
            : new PokenModel(definition, row.Level);
        poken.SetCurrentHP(row.CurrentHP);

        if (System.Enum.TryParse<StatusEffect>(row.StatusEffect, out StatusEffect status))
            poken.SetStatus(status);

        // Carga los movimientos de este Poken (vinculados por PlayerPokenId).
        // El constructor ya auto-aprendio movimientos del learnset: si el save
        // trae movimientos propios, se pisan los auto-aprendidos para no duplicar.
        var moveRows = _db.Table<PlayerPokenMoveRow>().Where(m => m.PlayerPokenId == row.Id).ToList();
        if (moveRows.Count > 0)
            poken.Moves.Clear();
        foreach (var moveRow in moveRows)
        {
            MoveDefinition moveDefinition = DefinitionRepository.Instance.GetMove(moveRow.MoveDefinitionId);
            if (moveDefinition != null)
            {
                MoveModel move = new MoveModel(moveDefinition);
                move.SetCurrentPP(moveRow.CurrentPP);
                poken.Moves.Add(move);
            }
        }

        return poken;
    }

    // Reconstruye el inventario a partir de las filas guardadas, separando por ItemType
    private void LoadInventory(List<PlayerInventoryRow> rows, InventoryModel inventory)
    {
        foreach (var row in rows)
        {
            switch (row.ItemType)
            {
                case "Heal":
                    var healDef = DefinitionRepository.Instance.GetHealItem(row.ItemDefinitionId);
                    if (healDef != null)
                        inventory.HealItems.Add(new HealItemStack(healDef, row.Quantity));
                    break;

                case "Battle":
                    var battleDef = DefinitionRepository.Instance.GetBattleItem(row.ItemDefinitionId);
                    if (battleDef != null)
                        inventory.BattleItems.Add(new BattleItemStack(battleDef, row.Quantity));
                    break;

                case "Special":
                    var specialDef = DefinitionRepository.Instance.GetSpecialItem(row.ItemDefinitionId);
                    if (specialDef != null)
                        inventory.SpecialItems.Add(new SpecialItemStack(specialDef, row.Quantity));
                    break;
            }
        }
    }
}

