using System.Collections.Generic;

public class EnemyModel
{
    public NPCDefinition Definition { get; private set; }
    public List<PokenModel> Team { get; private set; }
    public int Money { get; private set; }

    public bool HasAlivePoken => Team.Exists(p => !p.IsFainted);
    public bool IsBoss => Definition is BossDefinition;
    public bool IsEnemy => Definition is EnemyDefinition;

    public EnemyModel(NPCDefinition definition)
    {
        Definition = definition;
        Team = new List<PokenModel>();

        if (definition is EnemyDefinition enemy)
            Money = enemy.moneyReward;
        else if (definition is BossDefinition boss)
            Money = boss.moneyReward;
        else
            Money = 0;
    }
}