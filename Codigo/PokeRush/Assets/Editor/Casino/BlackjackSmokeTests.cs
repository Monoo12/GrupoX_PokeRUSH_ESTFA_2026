using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Smoke tests de la mesa de blackjack. Mismo esquema que RouletteSmokeTests: se
// corren desde el menu y reportan a la Console.
//
// Prueban la LOGICA (BlackjackTable), no la UI: cuanto vale una mano, el As
// valiendo 1 u 11 segun convenga, cuando pide el croupier, que gana contra que
// y cuanto paga cada cosa. Son las cuentas que, si se rompen, hacen que el
// juego pague mal sin que nada se vea raro en pantalla.
namespace PokeRush.EditorTools
{
    public static class BlackjackSmokeTests
    {
        private static int _passed;
        private static int _failed;

        [MenuItem("PokeRush/Casino/Correr smoke tests del blackjack")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            TestValorDeLasCartas();
            TestElAsValeOnceOUno();
            TestManoSuave();
            TestBlackjackSonDosCartas();
            TestElCroupierSePlantaEn17();
            TestQuienGanaContraQuien();
            TestPasarsePierdeSiempre();
            TestPagos();
            TestSePuedeDoblar();
            TestSePuedeDividir();
            TestElZapatoEstaCompleto();
            TestBarajarNoPierdeCartas();

            if (_failed == 0)
                Debug.Log($"[BlackjackSmokeTests] TODOS OK: {_passed} checks pasaron.");
            else
                Debug.LogError($"[BlackjackSmokeTests] {_failed} FALLARON, {_passed} pasaron.");
        }

        private static void Check(bool condition, string name)
        {
            if (condition) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[BlackjackSmokeTests] FALLO: {name}");
            }
        }

        // Atajo para escribir manos: Mano(1, 13) es As de picas y K de picas.
        private static List<BlackjackCard> Mano(params int[] rangos)
        {
            var cartas = new List<BlackjackCard>();
            foreach (int r in rangos) cartas.Add(new BlackjackCard(BlackjackSuit.Picas, r));
            return cartas;
        }

        private static BlackjackHand Como(int apuesta, bool split, params int[] rangos)
        {
            var mano = new BlackjackHand { Bet = apuesta, DeSplit = split };
            mano.Cards.AddRange(Mano(rangos));
            return mano;
        }

        // ---------- valores ----------

        private static void TestValorDeLasCartas()
        {
            Check(new BlackjackCard(BlackjackSuit.Picas, 2).Valor == 2, "el 2 vale 2");
            Check(new BlackjackCard(BlackjackSuit.Picas, 9).Valor == 9, "el 9 vale 9");
            Check(new BlackjackCard(BlackjackSuit.Picas, 10).Valor == 10, "el 10 vale 10");

            // Las tres figuras valen 10, no 11, 12 y 13.
            Check(new BlackjackCard(BlackjackSuit.Picas, 11).Valor == 10, "la J vale 10");
            Check(new BlackjackCard(BlackjackSuit.Picas, 12).Valor == 10, "la Q vale 10");
            Check(new BlackjackCard(BlackjackSuit.Picas, 13).Valor == 10, "la K vale 10");

            Check(BlackjackTable.ValorDe(Mano(10, 7)) == 17, "10 + 7 son 17");
            Check(BlackjackTable.ValorDe(Mano(12, 13)) == 20, "Q + K son 20");
            Check(BlackjackTable.ValorDe(Mano(5, 6, 7)) == 18, "5 + 6 + 7 son 18");
        }

        private static void TestElAsValeOnceOUno()
        {
            Check(BlackjackTable.ValorDe(Mano(1)) == 11, "un As solo vale 11");
            Check(BlackjackTable.ValorDe(Mano(1, 13)) == 21, "As + K son 21");

            // Con 12 en la mesa el As tiene que bajar a 1 o la mano se pasaria.
            Check(BlackjackTable.ValorDe(Mano(1, 10, 5)) == 16, "As + 10 + 5 son 16, no 26");

            // Y los ases bajan DE A UNO: este es el error clasico de contarlos
            // todos igual. A + A + 9 son 21 (11 + 1 + 9), no 31 ni 11.
            Check(BlackjackTable.ValorDe(Mano(1, 1)) == 12, "As + As son 12");
            Check(BlackjackTable.ValorDe(Mano(1, 1, 9)) == 21, "As + As + 9 son 21");
            Check(BlackjackTable.ValorDe(Mano(1, 1, 9, 8)) == 19, "As + As + 9 + 8 son 19");

            // Cuatro ases y nada mas: 11 + 1 + 1 + 1.
            Check(BlackjackTable.ValorDe(Mano(1, 1, 1, 1)) == 14, "cuatro Ases son 14");
        }

        private static void TestManoSuave()
        {
            Check(BlackjackTable.EsSuave(Mano(1, 6)), "As + 6 es suave");
            Check(!BlackjackTable.EsSuave(Mano(10, 7)), "10 + 7 es dura");

            // Con el As ya bajado a 1 la mano dejo de ser suave.
            Check(!BlackjackTable.EsSuave(Mano(1, 10, 5)), "As + 10 + 5 es dura");
            Check(BlackjackTable.EsSuave(Mano(1, 1, 5)), "As + As + 5 es suave");
        }

        private static void TestBlackjackSonDosCartas()
        {
            Check(BlackjackTable.EsBlackjack(Mano(1, 13)), "As + K es blackjack");
            Check(!BlackjackTable.EsBlackjack(Mano(7, 7, 7)), "7 + 7 + 7 son 21 pero no blackjack");
            Check(!BlackjackTable.EsBlackjack(Mano(10, 9)), "19 no es blackjack");

            // Un 21 servido despues de dividir NO es blackjack: paga 1 a 1.
            Check(!Como(100, true, 1, 13).EsBlackjack, "el 21 de un split no es blackjack");
            Check(Como(100, false, 1, 13).EsBlackjack, "el 21 servido si lo es");
        }

        // ---------- el croupier ----------

        private static void TestElCroupierSePlantaEn17()
        {
            Check(BlackjackTable.DealerPide(Mano(10, 6)), "con 16 pide");
            Check(!BlackjackTable.DealerPide(Mano(10, 7)), "con 17 se planta");
            Check(!BlackjackTable.DealerPide(Mano(10, 10)), "con 20 se planta");

            // S17: tambien se planta en 17 SUAVE. Es la regla elegida y la que
            // menos ventaja le da a la casa; si alguien la cambia sin querer,
            // este check lo caza.
            Check(!BlackjackTable.DealerPide(Mano(1, 6)), "con 17 suave tambien se planta");
            Check(BlackjackTable.DealerPide(Mano(1, 5)), "con 16 suave pide");
        }

        // ---------- quien gana ----------

        private static void TestQuienGanaContraQuien()
        {
            Check(BlackjackTable.Resolver(Como(100, false, 10, 10), Como(0, false, 10, 9))
                  == BlackjackOutcome.Gana, "20 le gana a 19");

            Check(BlackjackTable.Resolver(Como(100, false, 10, 8), Como(0, false, 10, 9))
                  == BlackjackOutcome.Pierde, "18 pierde contra 19");

            Check(BlackjackTable.Resolver(Como(100, false, 10, 9), Como(0, false, 10, 9))
                  == BlackjackOutcome.Empata, "19 contra 19 es empate");

            Check(BlackjackTable.Resolver(Como(100, false, 10, 6), Como(0, false, 10, 5, 9))
                  == BlackjackOutcome.Gana, "si el croupier se pasa, gana el 16");

            Check(BlackjackTable.Resolver(Como(100, false, 1, 13), Como(0, false, 10, 9))
                  == BlackjackOutcome.Blackjack, "el blackjack se cobra aparte");

            // Blackjack contra blackjack es empate, no gana el jugador.
            Check(BlackjackTable.Resolver(Como(100, false, 1, 13), Como(0, false, 1, 12))
                  == BlackjackOutcome.Empata, "blackjack contra blackjack empata");

            // Un 21 de tres cartas PIERDE contra un blackjack servido.
            Check(BlackjackTable.Resolver(Como(100, false, 7, 7, 7), Como(0, false, 1, 13))
                  == BlackjackOutcome.Pierde, "21 de tres cartas pierde con el blackjack");
        }

        private static void TestPasarsePierdeSiempre()
        {
            // Aunque el croupier tambien se pase: esa es la ventaja de la casa,
            // y sin ella plantarse en 16 no seria una decision.
            Check(BlackjackTable.Resolver(Como(100, false, 10, 10, 5), Como(0, false, 10, 6, 9))
                  == BlackjackOutcome.Pierde, "pasarse pierde aunque el croupier se pase");

            Check(BlackjackTable.Resolver(Como(100, false, 10, 10, 5), Como(0, false, 10, 7))
                  == BlackjackOutcome.Pierde, "pasarse pierde contra 17");
        }

        // ---------- el pago ----------

        private static void TestPagos()
        {
            // Vuelve la apuesta MAS la ganancia: 100 apostados devuelven 200.
            Check(BlackjackTable.PagoDe(BlackjackOutcome.Gana, 100) == 200, "ganar paga 1 a 1");
            Check(BlackjackTable.PagoDe(BlackjackOutcome.Empata, 100) == 100,
                "empatar devuelve la apuesta");
            Check(BlackjackTable.PagoDe(BlackjackOutcome.Pierde, 100) == 0, "perder no devuelve nada");

            // 3 a 2: 100 vuelven 250.
            Check(BlackjackTable.PagoDe(BlackjackOutcome.Blackjack, 100) == 250,
                "el blackjack paga 3 a 2");
            Check(BlackjackTable.PagoDe(BlackjackOutcome.Blackjack, 10) == 25,
                "el blackjack de 10 devuelve 25");
            // Con apuestas impares la mesa redondea para abajo.
            Check(BlackjackTable.PagoDe(BlackjackOutcome.Blackjack, 5) == 12,
                "el blackjack de 5 devuelve 12");
        }

        // ---------- que se permite ----------

        private static void TestSePuedeDoblar()
        {
            Check(BlackjackTable.SePuedeDoblar(Como(100, false, 5, 6), 100),
                "se dobla con dos cartas y plata");
            Check(!BlackjackTable.SePuedeDoblar(Como(100, false, 5, 6), 99),
                "no se dobla sin plata");
            Check(!BlackjackTable.SePuedeDoblar(Como(100, false, 5, 6, 2), 500),
                "no se dobla con tres cartas");

            var cerrada = Como(100, false, 5, 6);
            cerrada.Cerrada = true;
            Check(!BlackjackTable.SePuedeDoblar(cerrada, 500), "no se dobla una mano cerrada");
        }

        private static void TestSePuedeDividir()
        {
            Check(BlackjackTable.SePuedeDividir(Como(100, false, 8, 8), 1, 100),
                "se divide un par de 8");
            // Mismo VALOR alcanza: K y Q valen 10 las dos.
            Check(BlackjackTable.SePuedeDividir(Como(100, false, 13, 12), 1, 100),
                "se divide K + Q");
            Check(!BlackjackTable.SePuedeDividir(Como(100, false, 8, 9), 1, 100),
                "no se divide 8 + 9");
            Check(!BlackjackTable.SePuedeDividir(Como(100, false, 8, 8), 1, 99),
                "no se divide sin plata");
            // Una sola vez por ronda.
            Check(!BlackjackTable.SePuedeDividir(Como(100, false, 8, 8), 2, 500),
                "no se vuelve a dividir");
        }

        // ---------- el zapato ----------

        private static void TestElZapatoEstaCompleto()
        {
            List<BlackjackCard> zapato = BlackjackTable.ZapatoOrdenado();
            Check(zapato.Count == BlackjackTable.BarajasEnElZapato * 52,
                "el zapato tiene 4 barajas enteras");

            var cuenta = new Dictionary<string, int>();
            foreach (BlackjackCard c in zapato)
            {
                string k = $"{(int)c.Suit}-{c.Rank}";
                cuenta[k] = cuenta.TryGetValue(k, out int n) ? n + 1 : 1;
            }

            Check(cuenta.Count == 52, "hay 52 cartas distintas");

            bool todas = true;
            foreach (int n in cuenta.Values) todas &= n == BlackjackTable.BarajasEnElZapato;
            Check(todas, "cada carta aparece una vez por baraja");
        }

        private static void TestBarajarNoPierdeCartas()
        {
            List<BlackjackCard> zapato = BlackjackTable.ZapatoOrdenado(1);
            var antes = new List<string>();
            foreach (BlackjackCard c in zapato) antes.Add($"{(int)c.Suit}-{c.Rank}");

            // Semilla fija: el test tiene que dar siempre lo mismo.
            var rng = new System.Random(1234);
            BlackjackTable.Barajar(zapato, n => rng.Next(n));

            Check(zapato.Count == 52, "barajar no cambia la cantidad");

            var despues = new HashSet<string>();
            foreach (BlackjackCard c in zapato) despues.Add($"{(int)c.Suit}-{c.Rank}");
            Check(despues.Count == 52, "barajar no repite ni pierde cartas");

            // Y ademas MEZCLA: si el orden quedara igual, el bug pasaria
            // desapercibido con los dos checks de arriba.
            int iguales = 0;
            for (int i = 0; i < zapato.Count; i++)
                if (antes[i] == $"{(int)zapato[i].Suit}-{zapato[i].Rank}") iguales++;
            Check(iguales < 10, $"el orden cambia de verdad ({iguales} cartas quedaron en su lugar)");
        }
    }
}
