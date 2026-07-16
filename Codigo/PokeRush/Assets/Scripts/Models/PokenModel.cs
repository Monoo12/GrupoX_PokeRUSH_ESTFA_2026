using System.Collections.Generic;

public class PokenModel
{
    // Tabla de XP compartida por todos los Pokens. La inyecta GameManager al
    // arrancar (desde Resources/Definitions/ExperienceTable). Los tests pueden
    // pisarla con una tabla propia.
    public static ExperienceTableDefinition DefaultXpTable;

    // RNG propio para generar IVs sin depender de UnityEngine (clase pura).
    private static readonly System.Random IvRandom = new System.Random();

    public PokenDefinition Definition { get; private set; }

    public int Level { get; private set; }
    public int CurrentXP { get; private set; }

    // Valores individuales (0-31 por stat): hacen unico a cada ejemplar.
    public IvSet Ivs { get; private set; }

    public int MaxHP { get; private set; }
    public int CurrentHP { get; private set; }

    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public int SpecialAttack { get; private set; }
    public int SpecialDefense { get; private set; }
    public int Speed { get; private set; }

    public StatusEffect CurrentStatus { get; private set; }
    public CosmeticDefinition EquippedCosmetic { get; private set; }

    public List<MoveModel> Moves { get; private set; }

    public bool IsFainted => CurrentHP <= 0;

    private ExperienceTableDefinition _xpTable;

    // Constructor normal: genera IVs al azar (cada ejemplar sale distinto).
    public PokenModel(PokenDefinition definition, int level)
        : this(definition, level, IvSet.GenerateRandom(IvRandom))
    {
    }

    // Constructor con IVs conocidos: lo usa SaveService al cargar la partida.
    public PokenModel(PokenDefinition definition, int level, IvSet ivs)
    {
        Definition = definition;
        Level = level;
        CurrentXP = 0;
        Ivs = ivs;
        CurrentStatus = StatusEffect.None;
        Moves = new List<MoveModel>();
        LearnInitialMoves();
        RecalculateStats();
        CurrentHP = MaxHP;
    }

    // Arma los movimientos iniciales (maximo 4, como en la saga):
    // - Con learnset: los 4 de mayor nivel que ya puede conocer a este nivel.
    // - Sin learnset (Pokens de prueba legados): los primeros 4 de 'moves'.
    private void LearnInitialMoves()
    {
        if (Definition.learnset != null && Definition.learnset.Length > 0)
        {
            // Junta las entradas aprendibles (nivel <= Level, sin repetir movimiento).
            var learnable = new List<LearnsetEntry>();
            foreach (LearnsetEntry entry in Definition.learnset)
            {
                if (entry == null || entry.move == null || entry.level > Level) continue;
                if (learnable.Exists(e => e.move == entry.move)) continue;
                learnable.Add(entry);
            }
            // Orden ascendente por nivel: los ultimos 4 aprendidos son los que conserva.
            learnable.Sort((a, b) => a.level.CompareTo(b.level));
            int start = learnable.Count > 4 ? learnable.Count - 4 : 0;
            for (int i = start; i < learnable.Count; i++)
                Moves.Add(new MoveModel(learnable[i].move));
            return;
        }

        if (Definition.moves != null)
        {
            foreach (MoveDefinition moveDef in Definition.moves)
            {
                if (moveDef == null) continue;
                Moves.Add(new MoveModel(moveDef));
                if (Moves.Count >= 4) break;
            }
        }
    }

    // Movimientos del learnset que se aprenden exactamente al nivel dado
    // (para el aviso de "quiere aprender X" al subir de nivel).
    public List<MoveDefinition> MovesLearnedAt(int level)
    {
        var result = new List<MoveDefinition>();
        if (Definition.learnset == null) return result;
        foreach (LearnsetEntry entry in Definition.learnset)
        {
            if (entry == null || entry.move == null || entry.level != level) continue;
            if (!result.Contains(entry.move)) result.Add(entry.move);
        }
        return result;
    }

    public bool KnowsMove(MoveDefinition move)
        => Moves.Exists(m => m.Definition == move);

    // Intenta aprender un movimiento. Devuelve false si ya conoce 4
    // (el flujo de reemplazo llega con la UI definitiva).
    public bool TryLearnMove(MoveDefinition move)
    {
        if (move == null || KnowsMove(move)) return false;
        if (Moves.Count >= 4) return false;
        Moves.Add(new MoveModel(move));
        return true;
    }

    // Debe llamarse antes de usar AddXP (o dejar que use DefaultXpTable)
    public void SetExperienceTable(ExperienceTableDefinition table)
    {
        _xpTable = table;
    }

    private ExperienceTableDefinition XpTable => _xpTable != null ? _xpTable : DefaultXpTable;

    // Suma XP y sube de nivel las veces que haga falta.
    // Devuelve la lista de niveles alcanzados (vacia si no subio).
    public List<int> AddXP(int xp)
    {
        var levelsGained = new List<int>();
        ExperienceTableDefinition table = XpTable;
        if (table == null || Level >= table.maxLevel) return levelsGained;

        CurrentXP += xp;

        int threshold = table.GetXPForLevel(Level);
        while (threshold != -1 && CurrentXP >= threshold && Level < table.maxLevel)
        {
            CurrentXP -= threshold;
            LevelUp();
            levelsGained.Add(Level);
            threshold = table.GetXPForLevel(Level);
        }

        // Al llegar al nivel maximo, descartamos XP sobrante
        if (Level >= table.maxLevel) CurrentXP = 0;
        return levelsGained;
    }

    private void LevelUp()
    {
        int oldMaxHP = MaxHP;
        Level++;
        RecalculateStats();
        // HP sube en proporcion al nuevo MaxHP, sin curarse completamente
        CurrentHP = CurrentHP * MaxHP / oldMaxHP;
        if (CurrentHP < 1) CurrentHP = 1;
    }

    private void RecalculateStats()
    {
        // Formula oficial de la saga (sin EVs ni naturalezas):
        //   HP    = ((2*base + IV) * nivel) / 100 + nivel + 10
        //   Otras = ((2*base + IV) * nivel) / 100 + 5
        // Division entera en C# ya trunca hacia abajo, equivalente al floor
        MaxHP = ((2 * Definition.baseHP + Ivs.hp) * Level) / 100 + Level + 10;
        Attack = ((2 * Definition.baseAttack + Ivs.attack) * Level) / 100 + 5;
        Defense = ((2 * Definition.baseDefense + Ivs.defense) * Level) / 100 + 5;
        SpecialAttack = ((2 * Definition.baseSpecialAttack + Ivs.specialAttack) * Level) / 100 + 5;
        SpecialDefense = ((2 * Definition.baseSpecialDefense + Ivs.specialDefense) * Level) / 100 + 5;
        Speed = ((2 * Definition.baseSpeed + Ivs.speed) * Level) / 100 + 5;
    }

    // Para restaurar HP al cargar desde la base de datos
    public void SetCurrentHP(int hp)
    {
        CurrentHP = hp;
        if (CurrentHP < 0) CurrentHP = 0;
        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
    }

    public void TakeDamage(int damage)
    {
        CurrentHP -= damage;
        if (CurrentHP < 0) CurrentHP = 0;
    }

    public void Heal(int amount)
    {
        CurrentHP += amount;
        if (CurrentHP > MaxHP) CurrentHP = MaxHP;
    }

    public void SetStatus(StatusEffect status)
    {
        CurrentStatus = status;
    }

    public void CureStatus()
    {
        CurrentStatus = StatusEffect.None;
    }
}
