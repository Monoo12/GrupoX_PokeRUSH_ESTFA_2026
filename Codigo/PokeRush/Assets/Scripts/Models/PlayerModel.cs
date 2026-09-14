using System.Collections.Generic;
using UnityEngine;

public class PlayerModel
{
    public string PlayerName { get; private set; }
    public Genero Genero { get; private set; }
    public int Money { get; private set; }

    public List<PokenModel> Team { get; private set; }
    public InventoryModel Inventory { get; private set; }

    // El PC: los Pokemon que no entran en el equipo. Ver PokenStorageModel.
    public PokenStorageModel Storage { get; private set; }

    // Tope clasico de 6: es lo que dibuja el HUD de combate.
    public const int EquipoMaximo = 6;

    public bool HasAlivePoken => Team.Exists(p => !p.IsFainted);

    public PlayerModel(string playerName) : this(playerName, Genero.Varon) { }

    public PlayerModel(string playerName, Genero genero)
    {
        PlayerName = playerName;
        Genero = genero;
        Money = 0;
        Team = new List<PokenModel>();
        Inventory = new InventoryModel();
        Storage = new PokenStorageModel();
    }

    // Un Pokemon que se consigue (premio, liberado, ganado en una apuesta) va al
    // equipo si hay lugar y al PC si no. Es el unico lugar donde se decide eso,
    // asi ninguna fuente de Pokemon se olvida del PC.
    public PokenRecibido RecibirPokemon(PokenModel poken)
    {
        if (poken == null) return PokenRecibido.SinLugar;

        if (Team.Count < EquipoMaximo)
        {
            Team.Add(poken);
            return new PokenRecibido(DestinoDePokemon.Equipo, -1);
        }

        return Storage.TryAgregar(poken, out int caja, out _)
            ? new PokenRecibido(DestinoDePokemon.Pc, caja)
            : PokenRecibido.SinLugar;
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

    // ---------- deuda ----------
    //
    // La enfermera del casino cura gratis al que no puede pagar, pero le anota
    // lo que debe. Vive en el jugador y no en el casino porque se guarda con la
    // partida y porque el ascensor la mira para no dejarlo subir debiendo.
    public int Debt { get; private set; }

    public bool HasDebt => Debt > 0;

    public void AddDebt(int amount)
    {
        if (amount <= 0) return;
        Debt += amount;
    }

    // Paga lo que se pueda con la plata que hay y devuelve cuanto se pago.
    // Nunca deja el saldo en negativo ni la deuda por debajo de cero.
    public int PayDebt(int amount)
    {
        if (amount <= 0 || Debt <= 0) return 0;

        int aPagar = Mathf.Min(Mathf.Min(amount, Debt), Money);
        if (aPagar <= 0) return 0;

        Money -= aPagar;
        Debt -= aPagar;
        return aPagar;
    }

    // Solo para la carga del guardado: pone la deuda tal cual estaba.
    public void RestoreDebt(int amount)
    {
        Debt = Mathf.Max(0, amount);
    }

    public void SpendMoney(int amount)
    {
        Money -= amount;
    }
}