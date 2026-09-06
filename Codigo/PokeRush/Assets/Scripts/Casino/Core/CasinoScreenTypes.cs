// Peticiones y resultados de las pantallas del casino.
//
// Van en su propio archivo y no en MenuTypes.cs para que el casino se pueda
// leer (y sacar) como una pieza aparte, aunque use el mismo MenuManager que el
// resto del juego.

// Lo que le manda la maquina del mundo a la pantalla de slots.
public class SlotsScreenRequest
{
    public SlotMachineDefinition Maquina;
    // Titulo del cartel. Vacio = el nombre de la maquina.
    public string Titulo;
    // Apuesta con la que abre. 0 = la minima de la maquina.
    public int ApuestaInicial;
}

// Lo que devuelve al cerrarse. La maquina del mundo lo usa para reaccionar
// (un cartel, una animacion) sin tener que espiar el modelo del jugador.
public class SlotsScreenResult
{
    public int Tiradas;
    // Suma de lo ganado menos lo apostado en toda la sesion de esta maquina.
    public int Neto;
    // El premio mas grande de la sesion, para el cartel de despedida.
    public int MayorPremio;
}

// ---------- ruleta de premios ----------

public class PrizeWheelRequest
{
    // Premios de ESTA maquina. Vacio = los que trae cargados el prefab.
    public Prize[] Prizes;
}

public class PrizeWheelResult
{
    public int Giros;
    // El ultimo premio que salio, para que la maquina del mundo pueda
    // reaccionar sin espiar la mochila.
    public Prize UltimoPremio;
}
