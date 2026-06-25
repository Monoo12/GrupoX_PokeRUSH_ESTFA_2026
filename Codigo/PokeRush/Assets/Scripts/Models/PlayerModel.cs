using System.Collections.Generic;

public class PlayerModel
{
    public string PlayerName { get; private set; }
    public int Money { get; private set; }

    public List<PokenModel> Team { get; private set; }
    public InventoryModel Inventory { get; private set; }

    public bool HasAlivePoken => Team.Exists(p => !p.IsFainted);

    public PlayerModel(string playerName)
    {
        PlayerName = playerName;
        Money = 0;
        Team = new List<PokenModel>();
        Inventory = new InventoryModel();
    }

    public void AddMoney(int amount)
    {
        Money += amount;
    }

    public void SpendMoney(int amount)
    {
        Money -= amount;
    }
}