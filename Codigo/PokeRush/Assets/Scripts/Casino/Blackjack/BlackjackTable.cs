using System.Collections.Generic;

// Las reglas del blackjack, sin nada de Unity adentro.
//
// Vive aparte de la pantalla a proposito, igual que RouletteBoard: cuanto vale
// una mano, cuando el croupier tiene que pedir, que paga cada resultado y como
// se baraja se pueden probar desde un test sin abrir una escena, y la UI se
// queda solo con dibujar.
//
// Las reglas que se eligieron, y por que (son las de un casino real, no
// inventadas; conviene no cambiarlas sin mirar los smoke tests):
//
//   * Zapato de 4 barajas. Con una sola, contar cartas es trivial y las
//     rachas se sienten amañadas; con 4 el reparto se ve parejo.
//   * El croupier se PLANTA en 17, incluso en 17 suave (regla S17). Es la
//     variante que menos ventaja le da a la casa y la que se espera de un
//     minijuego: perder contra un croupier que roba en 17 suave se lee como
//     trampa.
//   * Blackjack paga 3 a 2. Es lo que lo hace valer la pena frente a un 21
//     armado con tres cartas.
//   * Se dobla SOLO con dos cartas, y despues de doblar se recibe una sola.
//   * Se divide una vez (dos manos como maximo) y solo con dos cartas del
//     mismo VALOR, asi K+Q tambien se puede partir. Los ases divididos
//     reciben una carta cada uno y se cierran: es la regla estandar y ademas
//     evita que partir ases sea plata gratis.
//   * Un 21 con dos cartas despues de dividir NO es blackjack: paga 1 a 1.
public static class BlackjackTable
{
    public const int BarajasEnElZapato = 4;
    public const int CartasPorBaraja = 52;

    // Cuando quedan menos que esto se rearma el zapato. Es la "carta de corte"
    // de una mesa de verdad: se cambia entre manos y nunca a mitad de una.
    public const int CorteDelZapato = 20;

    public const int DealerSePlantaEn = 17;

    // ------------------------------------------------------------ el valor

    // Cuanto vale una mano, contando los ases como 11 mientras entren.
    //
    // Los ases se bajan de a uno: con A-A-9 el primero vale 11 y el segundo 1
    // (total 21), y recien con A-A-9-8 bajan los dos (total 19). Contarlos
    // todos como 1 o todos como 11 son los dos errores clasicos.
    public static int ValorDe(IList<BlackjackCard> cartas)
    {
        if (cartas == null) return 0;

        int total = 0;
        int ases = 0;

        for (int i = 0; i < cartas.Count; i++)
        {
            total += cartas[i].Valor;
            if (cartas[i].EsAs) ases++;
        }

        while (total > 21 && ases > 0)
        {
            total -= 10;
            ases--;
        }

        return total;
    }

    // Una mano es SUAVE si le queda un as contando 11: no se puede pasar
    // pidiendo una carta mas.
    public static bool EsSuave(IList<BlackjackCard> cartas)
    {
        if (cartas == null) return false;

        int duro = 0;
        int ases = 0;
        for (int i = 0; i < cartas.Count; i++)
        {
            duro += cartas[i].EsAs ? 1 : cartas[i].Valor;
            if (cartas[i].EsAs) ases++;
        }

        return ases > 0 && duro + 10 <= 21;
    }

    public static bool SePaso(IList<BlackjackCard> cartas) => ValorDe(cartas) > 21;

    public static bool EsBlackjack(IList<BlackjackCard> cartas)
        => cartas != null && cartas.Count == 2 && ValorDe(cartas) == 21;

    // ---------------------------------------------------------- el croupier

    // Si el croupier tiene que pedir otra carta. S17: se planta en cualquier
    // 17, duro o suave.
    public static bool DealerPide(IList<BlackjackCard> cartas)
        => ValorDe(cartas) < DealerSePlantaEn;

    // ------------------------------------------------------- que se permite

    // Doblar: solo con la mano recien servida y si alcanza la plata.
    //
    // Se puede doblar con cualquier total y no solo con 9, 10 u 11. Limitarlo
    // seria mas fiel a algunos casinos, pero deja al jugador con un boton
    // apagado casi siempre y sin manera de saber por que.
    public static bool SePuedeDoblar(BlackjackHand mano, int plataDisponible)
        => mano != null
           && !mano.Cerrada
           && mano.Cards.Count == 2
           && plataDisponible >= mano.Bet;

    // Dividir: dos cartas del mismo valor, una sola vez por ronda y con plata
    // para poner la segunda apuesta.
    public static bool SePuedeDividir(BlackjackHand mano, int manosEnJuego, int plataDisponible)
        => mano != null
           && !mano.Cerrada
           && manosEnJuego == 1
           && mano.Cards.Count == 2
           && mano.Cards[0].Valor == mano.Cards[1].Valor
           && plataDisponible >= mano.Bet;

    // ------------------------------------------------------------ el pago

    public static BlackjackOutcome Resolver(BlackjackHand mano, BlackjackHand croupier)
    {
        if (mano == null || croupier == null) return BlackjackOutcome.Pendiente;

        // Pasarse pierde SIEMPRE, aunque el croupier tambien se pase despues.
        // Esa es la ventaja de la casa y es lo que hace que plantarse en 16
        // sea una decision de verdad.
        if (mano.SePaso) return BlackjackOutcome.Pierde;

        bool bjJugador = mano.EsBlackjack;
        bool bjCroupier = croupier.EsBlackjack;

        if (bjJugador && bjCroupier) return BlackjackOutcome.Empata;
        if (bjJugador) return BlackjackOutcome.Blackjack;
        if (bjCroupier) return BlackjackOutcome.Pierde;

        if (croupier.SePaso) return BlackjackOutcome.Gana;

        int mio = mano.Total;
        int suyo = croupier.Total;
        if (mio > suyo) return BlackjackOutcome.Gana;
        if (mio < suyo) return BlackjackOutcome.Pierde;
        return BlackjackOutcome.Empata;
    }

    // Lo que se le devuelve al jugador por una mano, apuesta incluida.
    //
    // Devuelve el TOTAL que vuelve a la caja del jugador, no la ganancia: la
    // apuesta ya se le descontó al repartir. O sea que perder devuelve 0 y
    // empatar devuelve lo puesto.
    public static int PagoDe(BlackjackOutcome resultado, int apuesta)
    {
        switch (resultado)
        {
            // 3 a 2: con 100 apostados vuelven 250. La division entera va
            // redondeada hacia abajo, que es lo que hace una mesa de verdad
            // con las fichas impares.
            case BlackjackOutcome.Blackjack: return apuesta + apuesta * 3 / 2;
            case BlackjackOutcome.Gana: return apuesta * 2;
            case BlackjackOutcome.Empata: return apuesta;
            default: return 0;
        }
    }

    // ------------------------------------------------------------ el zapato

    // Un zapato entero sin barajar, en orden. Barajarlo es cosa de quien lo
    // pida, porque el orden depende del RNG y este archivo no elige uno.
    public static List<BlackjackCard> ZapatoOrdenado(int barajas = BarajasEnElZapato)
    {
        var cartas = new List<BlackjackCard>(barajas * CartasPorBaraja);

        for (int b = 0; b < barajas; b++)
            for (int palo = 0; palo < 4; palo++)
                for (int rango = 1; rango <= 13; rango++)
                    cartas.Add(new BlackjackCard((BlackjackSuit)palo, rango));

        return cartas;
    }

    // Fisher-Yates: cada orden posible sale con la misma probabilidad.
    //
    // El sorteo se recibe de afuera para poder correr los tests con un RNG
    // fijo; el juego le pasa UnityEngine.Random.
    public static void Barajar(IList<BlackjackCard> cartas, System.Func<int, int> siguiente)
    {
        if (cartas == null || siguiente == null) return;

        for (int i = cartas.Count - 1; i > 0; i--)
        {
            int j = siguiente(i + 1);
            if (j < 0 || j > i) j = i;

            BlackjackCard tmp = cartas[i];
            cartas[i] = cartas[j];
            cartas[j] = tmp;
        }
    }
}
