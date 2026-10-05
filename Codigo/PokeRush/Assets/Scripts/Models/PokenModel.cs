using System.Collections.Generic;

public class PokenModel
{
    // Tabla de XP compartida por todos los Pokens. La inyecta GameManager al
    // arrancar (desde Resources/Definitions/ExperienceTable). Los tests pueden
    // pisarla con una tabla propia.
    public static ExperienceTableDefinition DefaultXpTable;

    // Ataque de ultimo recurso para una especie que no trae ninguno (ver
    // AsegurarUnAtaque). Lo inyecta GameManager (Placaje); sin inyectar, no se
    // agrega nada.
    public static MoveDefinition DefaultMove;

    // Ataque ESPECIAL de respaldo por tipo para una especie que a su nivel no
    // aprende ninguno (ver AsegurarUnAtaqueEspecial): Ascuas para Fuego,
    // Pistola Agua para Agua... Lo inyecta GameManager (AtaquesEspecialesDeRespaldo);
    // sin inyectar, solo se usa lo que trae el learnset de la especie.
    public static System.Func<PokenType, MoveDefinition> EspecialDeRespaldo;

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

    // Variante de color. No toca los stats: cambia que sprites se dibujan. La
    // guarda SaveService (columna IsShiny de player_poken).
    public bool IsShiny { get; private set; }

    public void SetShiny(bool shiny) => IsShiny = shiny;

    // ---------- sprites de ESTE ejemplar ----------
    // Los shiny si lo es y la especie los trae; si no, los normales. Las
    // pantallas preguntan aca y no a la definicion: asi un shiny se ve shiny en
    // el combate, el equipo, el resumen y el PC sin que cada una lo sepa.
    public UnityEngine.Sprite IconSprite => Elegir(Definition?.inventoryShinySprite, Definition?.inventorySprite);
    public UnityEngine.Sprite FrontSprite => Elegir(Definition?.battleFrontShinySprite, Definition?.battleFrontSprite);
    public UnityEngine.Sprite BackSprite => Elegir(Definition?.battleBackShinySprite, Definition?.battleBackSprite);
    public UnityEngine.Sprite[] FrontFrames => ElegirFrames(Definition?.battleFrontShinyFrames, Definition?.battleFrontFrames);
    public UnityEngine.Sprite[] BackFrames => ElegirFrames(Definition?.battleBackShinyFrames, Definition?.battleBackFrames);

    private UnityEngine.Sprite Elegir(UnityEngine.Sprite shiny, UnityEngine.Sprite normal)
        => IsShiny && shiny != null ? shiny : normal;

    private UnityEngine.Sprite[] ElegirFrames(UnityEngine.Sprite[] shiny, UnityEngine.Sprite[] normal)
        => IsShiny && shiny != null && shiny.Length > 0 ? shiny : normal;

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
        AsegurarUnAtaque();
        AsegurarUnAtaqueEspecial();
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
                if (entry.move.implementation == MoveImplementation.NotImplemented) continue;
                if (MovimientosRetirados.EstaRetirado(entry.move)) continue;
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
                if (moveDef == null || moveDef.implementation == MoveImplementation.NotImplemented) continue;
                if (MovimientosRetirados.EstaRetirado(moveDef)) continue;
                Moves.Add(new MoveModel(moveDef));
                if (Moves.Count >= 4) break;
            }
        }
    }

    // ---------- que siempre tenga con que pegar ----------

    // Un movimiento que sirve para pegar: hace daño y esta implementado.
    public static bool SirveParaPegar(MoveDefinition move)
        => move != null && move.implementation != MoveImplementation.NotImplemented
           && move.power > 0 && move.attackType != AttackType.Status
           && !MovimientosRetirados.EstaRetirado(move);

    // Un ataque especial que sirve para pegar.
    public static bool EsAtaqueEspecial(MoveDefinition move)
        => SirveParaPegar(move) && move.attackType == AttackType.Special;

    // Todo Pokemon tiene que poder pegar. Con learnset, "los ultimos 4" pueden
    // ser todos de estado (Togepi a nivel 15), y hay especies que por nivel no
    // aprenden nada util (Abra solo sabe Teletransporte). Si no tiene ningun
    // ataque se le da uno: el mas nuevo de su learnset que ya pueda conocer; si
    // no, el primero que aprenderia; si su learnset no trae ninguno, DefaultMove.
    // Con lugar lo suma; si no, reemplaza al primero que no sirve (uno no
    // implementado antes que uno de estado).
    //
    // Lo llama el constructor y tambien SaveService al cargar, asi un Pokemon
    // guardado sin ataques (los Abra de la ruleta) los recupera.
    public void AsegurarUnAtaque()
    {
        if (Definition == null) return;
        if (Moves.Exists(m => m != null && SirveParaPegar(m.Definition))) return;

        MoveDefinition ataque = AtaqueDeRespaldo();
        if (ataque == null) return;

        if (Moves.Count < 4)
        {
            Moves.Add(new MoveModel(ataque));
            return;
        }

        int lugar = Moves.FindIndex(m => m == null || m.Definition == null
                                        || m.Definition.implementation == MoveImplementation.NotImplemented);
        if (lugar < 0) lugar = 0; // el mas viejo: la lista inicial va de menor a mayor nivel
        Moves[lugar] = new MoveModel(ataque);
    }

    private MoveDefinition AtaqueDeRespaldo()
    {
        MoveDefinition masNuevoYaAprendible = null, primeroQueAprenderia = null;
        int nivelMasNuevo = -1, nivelPrimero = int.MaxValue;

        if (Definition.learnset != null)
        {
            foreach (LearnsetEntry entry in Definition.learnset)
            {
                if (entry == null || !SirveParaPegar(entry.move)) continue;
                if (entry.level <= Level && entry.level > nivelMasNuevo)
                {
                    masNuevoYaAprendible = entry.move;
                    nivelMasNuevo = entry.level;
                }
                if (entry.level < nivelPrimero)
                {
                    primeroQueAprenderia = entry.move;
                    nivelPrimero = entry.level;
                }
            }
        }
        if (masNuevoYaAprendible != null) return masNuevoYaAprendible;
        if (primeroQueAprenderia != null) return primeroQueAprenderia;

        if (Definition.moves != null)
            foreach (MoveDefinition move in Definition.moves)
                if (SirveParaPegar(move)) return move;

        return SirveParaPegar(DefaultMove) ? DefaultMove : null;
    }

    // ---------- que tenga algun ataque especial ----------

    // Todo Pokemon tiene al menos un ataque ESPECIAL (playtest del 5-oct).
    // Primero el especial mas nuevo que su learnset ya le deja conocer; si no
    // tiene ninguno a este nivel, uno basico de su tipo (EspecialDeRespaldo:
    // Ascuas, Pistola Agua, Impactrueno...).
    //
    // Con lugar lo suma. Si ya sabe cuatro, reemplaza al que menos rinde (uno
    // de estado vale como potencia 30), pero nunca a su ataque fisico mas
    // fuerte: tiene que seguir pegando de las dos maneras.
    //
    // Lo llama el constructor (asi vale tambien para los rivales) y SaveService
    // al cargar una partida de antes de esta regla.
    public void AsegurarUnAtaqueEspecial()
    {
        if (Definition == null) return;
        if (Moves.Exists(m => m != null && EsAtaqueEspecial(m.Definition))) return;

        MoveDefinition especial = EspecialDelLearnset();
        if (especial == null && EspecialDeRespaldo != null)
        {
            especial = EspecialDeRespaldo(Definition.type);
            if (!EsAtaqueEspecial(especial) && Definition.type2 != PokenType.None)
                especial = EspecialDeRespaldo(Definition.type2);
            if (!EsAtaqueEspecial(especial)) especial = EspecialDeRespaldo(PokenType.Normal);
        }
        if (!EsAtaqueEspecial(especial) || KnowsMove(especial)) return;

        if (Moves.Count < 4)
        {
            Moves.Add(new MoveModel(especial));
            return;
        }
        Moves[LugarParaElEspecial()] = new MoveModel(especial);
    }

    // El especial mas nuevo del learnset que ya puede conocer.
    private MoveDefinition EspecialDelLearnset()
    {
        if (Definition.learnset == null) return null;
        MoveDefinition mejor = null;
        int nivelMejor = -1;
        foreach (LearnsetEntry entry in Definition.learnset)
        {
            if (entry == null || !EsAtaqueEspecial(entry.move) || entry.level > Level) continue;
            if (entry.level > nivelMejor) { mejor = entry.move; nivelMejor = entry.level; }
        }
        return mejor;
    }

    private int LugarParaElEspecial()
    {
        int fisicoProtegido = -1, potenciaProtegida = -1;
        for (int i = 0; i < Moves.Count; i++)
        {
            MoveDefinition d = Moves[i]?.Definition;
            if (SirveParaPegar(d) && d.attackType == AttackType.Physical && d.power > potenciaProtegida)
            {
                fisicoProtegido = i;
                potenciaProtegida = d.power;
            }
        }

        int lugar = 0;
        int peor = int.MaxValue;
        for (int i = 0; i < Moves.Count; i++)
        {
            if (i == fisicoProtegido) continue;
            MoveDefinition d = Moves[i]?.Definition;
            int rinde = d == null || d.implementation == MoveImplementation.NotImplemented ? -1
                : d.attackType == AttackType.Status ? 30 : d.power;
            if (rinde < peor) { peor = rinde; lugar = i; }
        }
        return lugar;
    }

    // ---------- movimientos retirados (Excavar) ----------

    // Saca los movimientos que el juego ya no usa (ver MovimientosRetirados) y
    // le devuelve con que pegar. Para las partidas guardadas de antes: lo
    // llama SaveService al cargar. Devuelve si saco alguno.
    public bool QuitarMovimientosRetirados()
    {
        int antes = Moves.Count;
        Moves.RemoveAll(m => m != null && MovimientosRetirados.EstaRetirado(m.Definition));
        if (Moves.Count == antes) return false;

        // El lugar que quedo se llena con lo mas nuevo que su learnset le deja
        // conocer y todavia no sabe.
        if (Definition != null && Definition.learnset != null)
        {
            var candidatos = new List<LearnsetEntry>();
            foreach (LearnsetEntry entry in Definition.learnset)
            {
                if (entry == null || entry.move == null || entry.level > Level) continue;
                if (entry.move.implementation == MoveImplementation.NotImplemented) continue;
                if (MovimientosRetirados.EstaRetirado(entry.move) || KnowsMove(entry.move)) continue;
                candidatos.Add(entry);
            }
            candidatos.Sort((a, b) => b.level.CompareTo(a.level));
            foreach (LearnsetEntry entry in candidatos)
            {
                if (Moves.Count >= 4) break;
                if (!KnowsMove(entry.move)) Moves.Add(new MoveModel(entry.move));
            }
        }
        AsegurarUnAtaque();
        return true;
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
            if (MovimientosRetirados.EstaRetirado(entry.move)) continue;
            if (!result.Contains(entry.move)) result.Add(entry.move);
        }
        return result;
    }

    // Intercambia dos movimientos de lugar. Lo usa el Summary: el orden que ve
    // el jugador ES el orden real de la lista (el que usa el combate y el que
    // guarda el save), no una copia para la UI.
    public bool SwapMoves(int a, int b)
    {
        if (a == b) return false;
        if (a < 0 || b < 0 || a >= Moves.Count || b >= Moves.Count) return false;

        MoveModel tmp = Moves[a];
        Moves[a] = Moves[b];
        Moves[b] = tmp;
        return true;
    }

    public bool KnowsMove(MoveDefinition move)
        => Moves.Exists(m => m.Definition == move);

    // Intenta aprender un movimiento. Devuelve false si ya conoce 4: en el
    // combate el jugador decide cual olvida (BattleEventKind.QuiereAprender).
    public bool TryLearnMove(MoveDefinition move)
    {
        if (move == null || KnowsMove(move) || MovimientosRetirados.EstaRetirado(move)) return false;
        if (Moves.Count >= 4) return false;
        Moves.Add(new MoveModel(move));
        return true;
    }

    // Olvida el movimiento de ese lugar y aprende otro en su lugar, con los PP
    // llenos. Mantiene la posicion: el orden es el del combate y el guardado.
    // Lo usa el tutor de MTs de Nestor.
    public bool ReplaceMove(int index, MoveDefinition move)
    {
        if (move == null || KnowsMove(move) || MovimientosRetirados.EstaRetirado(move)) return false;
        if (index < 0 || index >= Moves.Count) return false;
        Moves[index] = new MoveModel(move);
        return true;
    }

    // Debe llamarse antes de usar AddXP (o dejar que use DefaultXpTable)
    public void SetExperienceTable(ExperienceTableDefinition table)
    {
        _xpTable = table;
    }

    private ExperienceTableDefinition XpTable => _xpTable != null ? _xpTable : DefaultXpTable;

    // Progreso 0-1 hacia el proximo nivel (para la barra de EXP del HUD)
    public float XpProgress
    {
        get
        {
            ExperienceTableDefinition table = XpTable;
            if (table == null || Level >= table.maxLevel) return 0f;
            int threshold = table.GetXPForLevel(Level);
            if (threshold <= 0) return 0f;
            float progress = (float)CurrentXP / threshold;
            return progress < 0f ? 0f : (progress > 1f ? 1f : progress);
        }
    }

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
