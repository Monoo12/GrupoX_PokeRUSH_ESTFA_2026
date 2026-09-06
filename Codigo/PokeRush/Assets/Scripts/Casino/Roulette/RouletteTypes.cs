using System.Collections.Generic;

// Tipos de la mesa de ruleta.
//
// Van aparte de CasinoScreenTypes.cs para que la ruleta se pueda leer (y sacar)
// como una pieza entera, igual que hace el resto del casino con sus pantallas.

public enum RouletteColor
{
    Verde,   // solo el cero
    Rojo,
    Negro
}

public enum RouletteBetKind
{
    Numero,
    Rojo,
    Negro
}

// Una ficha puesta en un lugar de la mesa.
public struct RouletteBet
{
    public RouletteBetKind Kind;
    // Solo se mira cuando Kind es Numero.
    public int Numero;

    public static RouletteBet AlNumero(int n) => new RouletteBet { Kind = RouletteBetKind.Numero, Numero = n };
    public static RouletteBet AlRojo() => new RouletteBet { Kind = RouletteBetKind.Rojo };
    public static RouletteBet AlNegro() => new RouletteBet { Kind = RouletteBetKind.Negro };

    // Para que sirva de clave del diccionario de apuestas.
    public override int GetHashCode() => Kind == RouletteBetKind.Numero ? Numero : 100 + (int)Kind;

    public override bool Equals(object otro)
    {
        if (!(otro is RouletteBet b)) return false;
        if (Kind != b.Kind) return false;
        return Kind != RouletteBetKind.Numero || Numero == b.Numero;
    }

    // Como se lee en el cartel: "al 17", "al rojo".
    public string Nombre()
    {
        switch (Kind)
        {
            case RouletteBetKind.Numero: return "al " + Numero;
            case RouletteBetKind.Rojo: return "al rojo";
            case RouletteBetKind.Negro: return "al negro";
            default: return "";
        }
    }
}

// Lo que le manda la mesa del mundo a la pantalla.
public class RouletteScreenRequest
{
    // Titulo del cartel. Vacio = el de la pantalla.
    public string Titulo;
    // Apuesta minima y maxima por ficha. 0 = las que traiga el prefab.
    public int ApuestaMinima;
    public int ApuestaMaxima;
}

// Lo que devuelve al cerrarse, para que la mesa del mundo pueda reaccionar sin
// espiar el modelo del jugador.
public class RouletteScreenResult
{
    public int Giros;
    // Ganado menos apostado en toda la sesion de esta mesa.
    public int Neto;
    // El premio mas grande de la sesion, para el cartel de despedida.
    public int MayorPremio;
    // Los numeros que fueron saliendo, del mas viejo al mas nuevo.
    public readonly List<int> Historial = new List<int>();
}
