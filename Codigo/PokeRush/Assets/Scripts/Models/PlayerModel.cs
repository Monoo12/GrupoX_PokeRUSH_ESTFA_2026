using System.Collections.Generic;

public class PlayerModel
{
    public string PlayerName { get; private set; }
    public Genero Genero { get; private set; }
    public int Money { get; private set; }

    public List<PokenModel> Team { get; private set; }
    public InventoryModel Inventory { get; private set; }

    public bool HasAlivePoken => Team.Exists(p => !p.IsFainted);

    public PlayerModel(string playerName) : this(playerName, Genero.Varon) { }

    public PlayerModel(string playerName, Genero genero)
    {
        PlayerName = playerName;
        Genero = genero;
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