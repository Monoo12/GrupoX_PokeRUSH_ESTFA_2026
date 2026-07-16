// Estado VOLATIL del Poken activo durante el combate: stat stages, confusion,
// retroceso, contadores... Se descarta al cambiar de Poken o terminar el combate
// (a diferencia de PokenModel, que persiste HP, estado y PP entre combates).
public class BattlePokenState
{
    public PokenModel Poken { get; private set; }

    // De que lado del combate esta (para que los eventos refresquen la barra correcta)
    public bool IsPlayerSide { get; private set; }

    // Stages -6..+6 indexados por el enum Stat (Atk, Def, SpA, SpD, Spe, Acc, Eva)
    public int[] Stages { get; private set; } = new int[7];

    // Confusion: volatil, convive con el estado persistente (ej: quemado Y confuso)
    public bool IsConfused;
    public int ConfusionTurnsLeft;

    // Retroceso: pierde su accion este turno; se limpia al final del turno
    public bool Flinched;

    // Turnos de sueño restantes (se sortea 1-3 al aplicar Asleep)
    public int SleepTurnsLeft;

    // Flag de Flash Fire: se activo la inmunidad y potencia sus movimientos Fire
    public bool FlashFireBoosted;

    // Ya se emitio el evento "se debilitó" (evita anunciarlo dos veces)
    public bool FaintAnnounced;

    public AbilityId Ability => Poken.Definition.abilityId;

    public BattlePokenState(PokenModel poken, bool isPlayerSide)
    {
        Poken = poken;
        IsPlayerSide = isPlayerSide;
    }

    public int GetStage(Stat stat) => Stages[(int)stat];

    // Aplica un cambio de stage respetando los limites -6/+6.
    // Devuelve cuanto cambio realmente (0 = ya estaba en el tope).
    public int ModifyStage(Stat stat, int delta)
    {
        int before = Stages[(int)stat];
        int after = StatStageTable.Clamp(before + delta);
        Stages[(int)stat] = after;
        return after - before;
    }

    // ---- stats efectivas (base con IVs + multiplicador de stage) ----

    public int EffectiveAttack => Scaled(Poken.Attack, Stat.Attack);
    public int EffectiveDefense => Scaled(Poken.Defense, Stat.Defense);
    public int EffectiveSpecialAttack => Scaled(Poken.SpecialAttack, Stat.SpecialAttack);
    public int EffectiveSpecialDefense => Scaled(Poken.SpecialDefense, Stat.SpecialDefense);

    // Velocidad efectiva: stages + reduccion del 50% por paralisis
    public int EffectiveSpeed
    {
        get
        {
            int speed = Scaled(Poken.Speed, Stat.Speed);
            if (Poken.CurrentStatus == StatusEffect.Paralyzed) speed /= 2;
            return speed < 1 ? 1 : speed;
        }
    }

    private int Scaled(int baseValue, Stat stat)
    {
        int value = (int)(baseValue * StatStageTable.Multiplier(GetStage(stat)));
        return value < 1 ? 1 : value;
    }

    // Multiplicador de precision combinado: precision propia vs evasion del rival
    public static float HitChanceMultiplier(BattlePokenState attacker, BattlePokenState defender)
    {
        int combined = StatStageTable.Clamp(
            attacker.GetStage(Stat.Accuracy) - defender.GetStage(Stat.Evasion));
        return StatStageTable.AccuracyMultiplier(combined);
    }
}
