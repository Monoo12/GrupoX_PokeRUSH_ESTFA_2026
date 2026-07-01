using System.Collections.Generic;

public class PokenModel
{
    public PokenDefinition Definition { get; private set; }

    public int Level { get; private set; }
    public int CurrentXP { get; private set; }

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

    public PokenModel(PokenDefinition definition, int level)
    {
        Definition = definition;
        Level = level;
        CurrentXP = 0;
        CurrentStatus = StatusEffect.None;
        Moves = new List<MoveModel>();
        // Aprende los movimientos de su especie (maximo 4, como en la saga).
        if (definition.moves != null)
        {
            foreach (MoveDefinition moveDef in definition.moves)
            {
                if (moveDef == null) continue;
                Moves.Add(new MoveModel(moveDef));
                if (Moves.Count >= 4) break;
            }
        }
        RecalculateStats();
        CurrentHP = MaxHP;
    }

    // Debe llamarse antes de usar AddXP
    public void SetExperienceTable(ExperienceTableDefinition table)
    {
        _xpTable = table;
    }

    public void AddXP(int xp)
    {
        if (_xpTable == null || Level >= _xpTable.maxLevel) return;

        CurrentXP += xp;

        int threshold = _xpTable.GetXPForLevel(Level);
        while (threshold != -1 && CurrentXP >= threshold && Level < _xpTable.maxLevel)
        {
            CurrentXP -= threshold;
            LevelUp();
            threshold = _xpTable.GetXPForLevel(Level);
        }

        // Al llegar al nivel maximo, descartamos XP sobrante
        if (Level >= _xpTable.maxLevel) CurrentXP = 0;
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
        // Formula tipo Pokemon: floor((2 * base * nivel) / 100) + modificador
        // Division entera en C# ya trunca hacia abajo, equivalente al floor
        MaxHP = (2 * Definition.baseHP * Level) / 100 + Level + 10;
        Attack = (2 * Definition.baseAttack * Level) / 100 + 5;
        Defense = (2 * Definition.baseDefense * Level) / 100 + 5;
        SpecialAttack = (2 * Definition.baseSpecialAttack * Level) / 100 + 5;
        SpecialDefense = (2 * Definition.baseSpecialDefense * Level) / 100 + 5;
        Speed = (2 * Definition.baseSpeed * Level) / 100 + 5;
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
}