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

        // Cada subtipo trae su propio pokenTeam (EnemyTeamEntry[]) y su recompensa.
        // Se detecta el subtipo real y se toman sus entradas de equipo.
        EnemyTeamEntry[] teamEntries = null;

        if (definition is EnemyDefinition enemy)
        {
            Money = enemy.moneyReward;
            teamEntries = enemy.pokenTeam;
        }
        else if (definition is BossDefinition boss)
        {
            Money = boss.moneyReward;
            teamEntries = boss.pokenTeam;
        }
        else
        {
            Money = 0;
        }

        BuildTeam(teamEntries);
    }

    // Construye los PokenModel del equipo a partir de las entradas (especie + nivel)
    private void BuildTeam(EnemyTeamEntry[] entries)
    {
        if (entries == null) return;

        foreach (EnemyTeamEntry entry in entries)
        {
            // Salta entradas sin especie asignada en el Inspector
            if (entry == null || entry.species == null) continue;

            // Nivel minimo 1: evita stats degenerados si quedo en 0 sin querer
            int level = entry.level < 1 ? 1 : entry.level;
            Team.Add(new PokenModel(entry.species, level));
        }
    }
}