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

    // Intercambia dos Pokens de posicion en el equipo.
    //
    // Modifica la lista REAL: el indice 0 es el que sale primero al combate y el
    // que SaveService guarda con SlotIndex 0. Reordenar solo en la UI hubiera
    // dejado la pantalla mintiendo respecto de lo que hace el juego.
    public bool SwapTeamSlots(int a, int b)
    {
        if (a == b) return false;
        if (a < 0 || b < 0 || a >= Team.Count || b >= Team.Count) return false;

        PokenModel tmp = Team[a];
        Team[a] = Team[b];
        Team[b] = tmp;
        return true;
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