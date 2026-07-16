// Resultado final de un combate: lo publica BattleController al terminar para
// que EventController/GameManager apliquen flags, dialogos, items y guardado.
public class BattleOutcome
{
    public bool PlayerVictory;
    // El jugador escapo de un salvaje (ni victoria ni derrota)
    public bool Escaped;
    public int MoneyEarned;
    // Rival del combate: de aca salen itemRewards, defeatGrantsFlag y dialogos
    // (via Enemy.Definition como EnemyDefinition o BossDefinition), o IsWild.
    public EnemyModel Enemy;
}
