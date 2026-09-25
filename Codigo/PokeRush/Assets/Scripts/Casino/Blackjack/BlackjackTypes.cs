using System.Collections.Generic;

// Tipos de la mesa de blackjack.
//
// Van aparte de CasinoScreenTypes.cs para que el blackjack se pueda leer (y
// sacar) como una pieza entera, igual que hacen la ruleta y las tragaperras con
// los suyos.

// El orden es el de las filas de bj_cartas.png, y de eso depende que sprite se
// dibuja: picas, corazones, diamantes, treboles. Si alguien reordena esto hay
// que volver a generar la hoja con scripts/cortar_arte_blackjack.py.
public enum BlackjackSuit
{
    Picas,
    Corazones,
    Diamantes,
    Treboles
}

// Una carta de la baraja. Es un struct y no una clase porque se reparten y se
// copian de a cientos por sesion y no tienen identidad propia: dos ases de
// picas son la misma carta.
public struct BlackjackCard
{
    public BlackjackSuit Suit;
    // 1 = As, 11 = J, 12 = Q, 13 = K. Se guarda el rango y NO el valor porque
    // el valor del As depende de la mano, y perderlo aca obligaria a adivinarlo
    // despues.
    public int Rank;

    public BlackjackCard(BlackjackSuit suit, int rank)
    {
        Suit = suit;
        Rank = rank;
    }

    public bool EsAs => Rank == 1;

    // Las figuras valen 10; el As, 11 de entrada (BlackjackTable lo baja a 1
    // cuando la mano se pasa).
    public int Valor => Rank >= 10 ? 10 : Rank == 1 ? 11 : Rank;

    // Indice de columna en la hoja de sprites: 0 = As, 12 = K.
    public int Columna => Rank - 1;

    public string Nombre()
    {
        string r;
        switch (Rank)
        {
            case 1: r = "A"; break;
            case 11: r = "J"; break;
            case 12: r = "Q"; break;
            case 13: r = "K"; break;
            default: r = Rank.ToString(); break;
        }

        switch (Suit)
        {
            case BlackjackSuit.Picas: return r + "♠";
            case BlackjackSuit.Corazones: return r + "♥";
            case BlackjackSuit.Diamantes: return r + "♦";
            default: return r + "♣";
        }
    }
}

// Una mano sobre la mesa. El jugador puede tener dos cuando divide.
public class BlackjackHand
{
    public readonly List<BlackjackCard> Cards = new List<BlackjackCard>();

    // Lo apostado a ESTA mano. Al doblar se duplica, y la mano nueva de un
    // split arranca con lo mismo que la original.
    public int Bet;

    // Ya no puede pedir mas: se planto, se paso, doblo o es un split de ases.
    public bool Cerrada;

    // Se doblo. Se recuerda para el cartel del final ("doblaste y ...").
    public bool Doblada;

    // Salio de dividir. Un 21 aca NO es blackjack: paga 1 a 1.
    public bool DeSplit;

    public int Total => BlackjackTable.ValorDe(Cards);
    public bool SePaso => Total > 21;
    public bool EsBlackjack => !DeSplit && Cards.Count == 2 && Total == 21;
    public bool EsSuave => BlackjackTable.EsSuave(Cards);

    public void Limpiar()
    {
        Cards.Clear();
        Bet = 0;
        Cerrada = false;
        Doblada = false;
        DeSplit = false;
    }
}

// Como termino una mano contra el croupier.
public enum BlackjackOutcome
{
    Pendiente,
    Blackjack,      // 21 con dos cartas y el croupier no lo tiene: paga 3 a 2
    Gana,           // paga 1 a 1
    Empata,         // devuelve la apuesta
    Pierde
}

// Lo que le manda la mesa del mundo a la pantalla.
public class BlackjackScreenRequest
{
    // Titulo del cartel. Vacio = el de la pantalla.
    public string Titulo;
    // Apuesta minima y maxima por mano. 0 = las que traiga el prefab.
    public int ApuestaMinima;
    public int ApuestaMaxima;

    // Apuesta de un Pokemon en vez de plata (Tacconi, piso 3). Con esto puesto
    // no hay fichas: se juega UNA sola mano, sin doblar ni dividir, y la mesa se
    // cierra sola al terminar. Es el nombre para los carteles; el Pokemon lo
    // maneja quien abre la mesa.
    public string ApuestaPokemon;
    // Lo que se lleva el jugador si gana, para el cartel. Ej: "su Cosmog shiny".
    public string PremioPokemon;
    // Contra quien se juega, para los carteles. Ej: "Tacconi".
    public string Rival;
}

// Lo que devuelve al cerrarse, para que la mesa del mundo pueda reaccionar sin
// espiar el modelo del jugador.
public class BlackjackScreenResult
{
    public int Manos;
    // Como termino la mano de una apuesta de Pokemon. Pendiente = se fue sin jugar.
    public BlackjackOutcome ResultadoApuestaPokemon = BlackjackOutcome.Pendiente;
    // Ganado menos apostado en toda la sesion de esta mesa.
    public int Neto;
    // El premio mas grande de la sesion, para el cartel de despedida.
    public int MayorPremio;
    // Si en algun momento se llevo una mano. Es lo que mira el piso 3 para
    // destrabar el ascensor.
    public bool GanoAlgunaMano;
    // Si alguna de esas manos fue un blackjack servido.
    public bool HizoBlackjack;
}
