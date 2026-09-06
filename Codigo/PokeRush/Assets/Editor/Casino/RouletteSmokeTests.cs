using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Smoke tests de la mesa de ruleta. Mismo esquema que CasinoSmokeTests: se
// corren desde el menu y reportan a la Console.
//
// Prueban la LOGICA (RouletteBoard), no la UI: la tabla de la rueda, los pagos,
// que apuesta cobra con que numero y, sobre todo, que la bola caiga en el
// casillero sorteado. Ese ultimo es el que importa: si esa cuenta se rompe, el
// juego paga bien pero la animacion frena en otro numero, que es el peor bug
// posible en una ruleta porque parece trampa.
namespace PokeRush.EditorTools
{
    public static class RouletteSmokeTests
    {
        private static int _passed;
        private static int _failed;

        [MenuItem("PokeRush/Casino/Correr smoke tests de la ruleta")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            TestLaRuedaTieneLos37Numeros();
            TestHay18Rojos18NegrosYUnCero();
            TestElCeroNoEsNiRojoNiNegro();
            TestCadaCasilleroTieneSuAngulo();
            TestPagos();
            TestQuienCobraConCadaNumero();
            TestElCeroSeLlevaLosDosColores();
            TestCuentaDeUnaMesaCompleta();
            TestLaBolaCaeEnElCasilleroSorteado();
            TestLaBolaSiempreGiraAlReves();

            if (_failed == 0)
                Debug.Log($"[RouletteSmokeTests] TODOS OK: {_passed} checks pasaron.");
            else
                Debug.LogError($"[RouletteSmokeTests] {_failed} FALLARON, {_passed} pasaron.");
        }

        private static void Check(bool condition, string name)
        {
            if (condition) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[RouletteSmokeTests] FALLO: {name}");
            }
        }

        // ---------- la rueda ----------

        private static void TestLaRuedaTieneLos37Numeros()
        {
            Check(RouletteBoard.Orden.Length == RouletteBoard.Casilleros,
                "la rueda tiene 37 casilleros");

            var vistos = new HashSet<int>(RouletteBoard.Orden);
            Check(vistos.Count == RouletteBoard.Casilleros, "no hay numeros repetidos");

            bool todos = true;
            for (int n = 0; n <= 36; n++) todos &= vistos.Contains(n);
            Check(todos, "estan todos los numeros del 0 al 36");
        }

        private static void TestHay18Rojos18NegrosYUnCero()
        {
            int rojos = 0, negros = 0, verdes = 0;
            for (int n = 0; n <= 36; n++)
            {
                switch (RouletteBoard.ColorDe(n))
                {
                    case RouletteColor.Rojo: rojos++; break;
                    case RouletteColor.Negro: negros++; break;
                    default: verdes++; break;
                }
            }

            Check(rojos == 18, "hay 18 rojos");
            Check(negros == 18, "hay 18 negros");
            Check(verdes == 1, "hay un solo verde");
        }

        private static void TestElCeroNoEsNiRojoNiNegro()
        {
            Check(!RouletteBoard.EsRojo(0) && !RouletteBoard.EsNegro(0),
                "el cero no es rojo ni negro");
            Check(RouletteBoard.ColorDe(0) == RouletteColor.Verde, "el cero es verde");
        }

        private static void TestCadaCasilleroTieneSuAngulo()
        {
            Check(Mathf.Approximately(RouletteBoard.AnguloDe(0), 0f),
                "el cero esta arriba de todo");

            // El indice 8 de la tabla es el 17: 8 pasos desde arriba.
            float esperado = 8 * RouletteBoard.PasoDeCasillero;
            Check(Mathf.Abs(RouletteBoard.AnguloDe(17) - esperado) < 0.001f,
                "el 17 esta a 8 pasos del cero");

            // Ningun casillero se pisa con otro. Esto tambien vale para el
            // dibujo: el anillo se repinta desde esta misma tabla.
            var angulos = new HashSet<int>();
            for (int n = 0; n <= 36; n++)
                angulos.Add(Mathf.RoundToInt(RouletteBoard.AnguloDe(n) * 100f));
            Check(angulos.Count == RouletteBoard.Casilleros, "no hay dos casilleros en el mismo angulo");
        }

        // ---------- apuestas ----------

        private static void TestPagos()
        {
            Check(RouletteBoard.PagoDe(RouletteBetKind.Numero) == 35, "el pleno paga 35 a 1");
            Check(RouletteBoard.PagoDe(RouletteBetKind.Rojo) == 1, "el rojo paga 1 a 1");
            Check(RouletteBoard.PagoDe(RouletteBetKind.Negro) == 1, "el negro paga 1 a 1");
        }

        private static void TestQuienCobraConCadaNumero()
        {
            Check(RouletteBoard.Gana(RouletteBet.AlNumero(17), 17), "el pleno al 17 cobra con el 17");
            Check(!RouletteBoard.Gana(RouletteBet.AlNumero(17), 18), "el pleno al 17 no cobra con el 18");

            // El 19 es rojo y el 20 negro.
            Check(RouletteBoard.Gana(RouletteBet.AlRojo(), 19), "el rojo cobra con el 19");
            Check(!RouletteBoard.Gana(RouletteBet.AlRojo(), 20), "el rojo no cobra con el 20");
            Check(RouletteBoard.Gana(RouletteBet.AlNegro(), 20), "el negro cobra con el 20");
            Check(!RouletteBoard.Gana(RouletteBet.AlNegro(), 19), "el negro no cobra con el 19");
        }

        private static void TestElCeroSeLlevaLosDosColores()
        {
            Check(!RouletteBoard.Gana(RouletteBet.AlRojo(), 0), "sale cero y el rojo pierde");
            Check(!RouletteBoard.Gana(RouletteBet.AlNegro(), 0), "sale cero y el negro pierde");
            Check(RouletteBoard.Gana(RouletteBet.AlNumero(0), 0), "sale cero y el pleno al cero cobra");
        }

        // Una mesa con tres apuestas puestas, para que la cuenta se vea entera.
        private static void TestCuentaDeUnaMesaCompleta()
        {
            var apuestas = new Dictionary<RouletteBet, int>
            {
                { RouletteBet.AlNumero(17), 100 },   // el 17 es negro
                { RouletteBet.AlNumero(3), 25 },
                { RouletteBet.AlRojo(), 500 }
            };

            int apostado = 0;
            foreach (int v in apuestas.Values) apostado += v;
            Check(apostado == 625, "la mesa tiene 625 apostados");

            // Sale el 17: cobra el pleno (100 + 3500) y pierden los otros dos.
            int pagado = Pagar(apuestas, 17);
            Check(pagado == 3600, "sale el 17 y paga 3600");

            // Sale el 3, que es rojo: cobran el pleno al 3 y el rojo.
            pagado = Pagar(apuestas, 3);
            Check(pagado == 25 + 875 + 500 + 500, "sale el 3 y cobran el pleno y el rojo");

            // Sale el cero: no cobra nadie.
            Check(Pagar(apuestas, 0) == 0, "sale el cero y no cobra nadie");
        }

        // Misma cuenta que hace RouletteScreen.Cobrar().
        private static int Pagar(Dictionary<RouletteBet, int> apuestas, int numero)
        {
            int total = 0;
            foreach (KeyValuePair<RouletteBet, int> par in apuestas)
            {
                if (!RouletteBoard.Gana(par.Key, numero)) continue;
                total += par.Value + par.Value * RouletteBoard.PagoDe(par.Key.Kind);
            }
            return total;
        }

        // ---------- la animacion ----------

        // El que importa: despues de girar, la bola tiene que quedar EXACTAMENTE
        // sobre el casillero que salio sorteado. Se prueba con todos los numeros
        // y con angulos de arranque feos a proposito.
        private static void TestLaBolaCaeEnElCasilleroSorteado()
        {
            float peorError = 0f;

            foreach (float giroRueda in new[] { 0f, 37.5f, 360f, 1443.7f, -220f, 2160f })
            {
                foreach (float bolaInicial in new[] { 0f, 91.3f, 359.9f, 180f })
                {
                    for (int ganador = 0; ganador <= 36; ganador++)
                    {
                        float giroBola = RouletteBoard.GiroDeLaBola(ganador, giroRueda, bolaInicial, 7);

                        float finBola = RouletteBoard.NormalizarAngulo(bolaInicial + giroBola);
                        float finCasillero = RouletteBoard.AnguloFinalDelCasillero(ganador, giroRueda);

                        // Diferencia por el camino corto del circulo.
                        float d = Mathf.Abs(Mathf.DeltaAngle(finBola, finCasillero));
                        if (d > peorError) peorError = d;
                    }
                }
            }

            Check(peorError < 0.01f,
                $"la bola cae en el casillero sorteado (peor error: {peorError:0.####} grados)");
        }

        private static void TestLaBolaSiempreGiraAlReves()
        {
            bool todas = true;
            for (int ganador = 0; ganador <= 36; ganador++)
            {
                float giro = RouletteBoard.GiroDeLaBola(ganador, 720f, 0f, 7);
                // Al reves que la rueda y con las vueltas pedidas cumplidas.
                todas &= giro <= -7 * 360f && giro > -8 * 360f;
            }
            Check(todas, "la bola gira al reves y da las vueltas pedidas");
        }
    }
}
