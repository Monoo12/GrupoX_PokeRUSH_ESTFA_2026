// Resultado final de un combate: lo publica BattleController al terminar para
// que EventController/GameManager apliquen flags, dialogos, items y guardado.
public class BattleOutcome
{
    public bool PlayerVictory;
    public int MoneyEarned;
    // Rival del combate: de aca salen itemRewards, defeatGrantsFlag y dialogos
    // (via Enemy.Definition como EnemyDefinition o BossDefinition), o IsWild.
    public EnemyModel Enemy;
}
