using System.Collections.Generic;

// Vista del combate que recibe la IA. Sin trampas: la IA conoce su propio
// equipo completo, pero del rival solo lo que un entrenador veria en pantalla
// (especie, tipos, nivel, % de HP, estado, stages). La referencia Foe se usa
// unicamente para calcular daño esperado, igual que hace la IA de la saga:
// las clases de IA no deben leer los movimientos ni el equipo del rival.
public class AiContext
{
    public BattlePokenState Self;
    public BattlePokenState Foe;
    public List<PokenModel> OwnTeam;
    public int OwnActiveIndex;
    public AIProfileDefinition Profile;

    // Señales legitimas del rival, listas para usar
    public float FoeHpPercent => Foe.Poken.MaxHP > 0
        ? (float)Foe.Poken.CurrentHP / Foe.Poken.MaxHP : 0f;
    public float SelfHpPercent => Self.Poken.MaxHP > 0
        ? (float)Self.Poken.CurrentHP / Self.Poken.MaxHP : 0f;

    // Contexto del lado rival del combate (la IA controla al Enemy)
    public static AiContext ForEnemy(BattleModel model, AIProfileDefinition profile)
    {
        return new AiContext
        {
            Self = model.EnemyState,
            Foe = model.PlayerState,
            OwnTeam = model.Enemy.Team,
            OwnActiveIndex = model.ActiveEnemyIndex,
            Profile = profile
        };
    }
}
