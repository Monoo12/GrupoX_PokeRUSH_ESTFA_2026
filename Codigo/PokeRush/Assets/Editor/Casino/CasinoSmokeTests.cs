using UnityEditor;
using UnityEngine;

// Smoke tests de la logica del casino. Mismo esquema que PokeRushSmokeTests:
// se corren desde el menu, usan SeededRng para ser reproducibles y reportan a la
// Console. Prueban el motor, no la UI: si estos pasan, lo que quede por revisar
// a mano es que la pantalla se vea y se cierre bien.
namespace PokeRush.EditorTools
{
    public static class CasinoSmokeTests
    {
        private const string MaquinaBasica = "Assets/Resources/Definitions/Casino/MaquinaBasica.asset";

        private static int _passed;
        private static int _failed;

        [MenuItem("PokeRush/Casino/Correr smoke tests del casino")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            TestDefinicionValida();
            TestValidacionDetectaErrores();
            TestPesoCeroNuncaSale();
            TestSorteoRespetaPesos();
            TestTresIgualesPagan();
            TestDosIgualesNoPaganPorDefecto();
            TestComodinCompletaLinea();
            TestTodaLaGrillaIgualPagaTodasLasLineas();
            TestPagoEscalaConLaApuesta();
            TestApuestaSeAjustaALosLimites();
            TestElDineroDelJugadorCierra();
            TestDevolucionDeLaMaquinaBasica();

            TestRuletaDeNpcsConLosDoceDelProyecto();
            TestRuletaDeNpcsSaleDosVecesSiSeLesGanaATodos();
            TestRuletaDeNpcsNoSonSiempreLosMismos();
            TestRuletaDeNpcsNuncaPasaDeDos();
            TestRuletaDeNpcsSoloLosDeLaLista();
            TestPremioDePlata();
            TestPremioPokemonSeSumaAlEquipo();
            TestPremioPokemonConEquipoLlenoPagaPlata();
            TestTablasDeRuletaDelProyecto();

            if (_failed == 0)
                Debug.Log($"[CasinoSmokeTests] TODOS OK: {_passed} checks pasaron.");
            else
                Debug.LogError($"[CasinoSmokeTests] {_failed} FALLARON, {_passed} pasaron.");
        }

        private static void Check(bool condition, string name)
        {
            if (condition) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[CasinoSmokeTests] FALLO: {name}");
            }
        }

        // ---------- helpers ----------

        // Maquina de prueba con numeros redondos, independiente del asset del
        // proyecto: si el equipo rebalancea la maquina del piso 1, estos tests
        // tienen que seguir diciendo lo mismo.
        private static SlotMachineDefinition Maquina()
        {
            var def = ScriptableObject.CreateInstance<SlotMachineDefinition>();
            def.rodillos = 3;
            def.filas = 3;
            def.apuestaMinima = 10;
            def.apuestaMaxima = 50;
            def.pasoDeApuesta = 10;
            def.simbolos = new[]
            {
                new SlotSymbol { nombre = "A", peso = 60, pagos = new[] { 0, 5 } },
                new SlotSymbol { nombre = "B", peso = 40, pagos = new[] { 3, 10 } },
                new SlotSymbol { nombre = "W", peso = 0, comodin = true, pagos = new[] { 0, 20 } }
            };
            def.lineas = new[]
            {
                new SlotPayline { nombre = "arriba", filas = new[] { 0, 0, 0 } },
                new SlotPayline { nombre = "medio",  filas = new[] { 1, 1, 1 } },
                new SlotPayline { nombre = "abajo",  filas = new[] { 2, 2, 2 } },
                new SlotPayline { nombre = "diag1",  filas = new[] { 0, 1, 2 } },
                new SlotPayline { nombre = "diag2",  filas = new[] { 2, 1, 0 } }
            };
            return def;
        }

        // Grilla 3x3 llena de 'relleno', para despues pisar casillas sueltas.
        private static int[,] Grilla(int relleno)
        {
            var g = new int[3, 3];
            for (int r = 0; r < 3; r++)
                for (int f = 0; f < 3; f++)
                    g[r, f] = relleno;
            return g;
        }

        // ---------- definicion ----------

        private static void TestDefinicionValida()
        {
            string error;
            Check(Maquina().Validar(out error), "la maquina de prueba valida");

            var asset = AssetDatabase.LoadAssetAtPath<SlotMachineDefinition>(MaquinaBasica);
            if (asset == null)
            {
                Debug.LogWarning("[CasinoSmokeTests] Todavia no existe " + MaquinaBasica +
                                 ". Corre PokeRush/Casino/Construir pantallas del casino.");
                return;
            }
            Check(asset.Validar(out error), "la maquina del piso 1 valida: " + error);
        }

        private static void TestValidacionDetectaErrores()
        {
            string error;

            SlotMachineDefinition sinSimbolos = Maquina();
            sinSimbolos.simbolos = new SlotSymbol[0];
            Check(!sinSimbolos.Validar(out error), "una maquina sin simbolos no valida");

            SlotMachineDefinition lineaCorta = Maquina();
            lineaCorta.lineas = new[] { new SlotPayline { filas = new[] { 0, 0 } } };
            Check(!lineaCorta.Validar(out error), "una linea con menos filas que rodillos no valida");

            SlotMachineDefinition filaFueraDeRango = Maquina();
            filaFueraDeRango.lineas = new[] { new SlotPayline { filas = new[] { 0, 9, 0 } } };
            Check(!filaFueraDeRango.Validar(out error), "una linea que apunta fuera de la grilla no valida");
        }

        // ---------- sorteo ----------

        private static void TestPesoCeroNuncaSale()
        {
            var motor = new SlotMachineEngine(Maquina());
            var rng = new SeededRng(1234);

            bool salioElDePesoCero = false;
            for (int i = 0; i < 5000; i++)
                if (motor.SortearSimbolo(rng) == 2) salioElDePesoCero = true;

            Check(!salioElDePesoCero, "un simbolo con peso 0 no sale nunca");
        }

        private static void TestSorteoRespetaPesos()
        {
            var motor = new SlotMachineEngine(Maquina());
            var rng = new SeededRng(99);

            int a = 0, b = 0;
            const int tiradas = 20000;
            for (int i = 0; i < tiradas; i++)
            {
                int s = motor.SortearSimbolo(rng);
                if (s == 0) a++;
                else if (s == 1) b++;
            }

            // 60/40 con margen: lo que se prueba es que el peso pesa, no la
            // calidad del generador.
            float proporcionA = a / (float)tiradas;
            Check(a + b == tiradas, "el sorteo siempre devuelve un simbolo con peso");
            Check(proporcionA > 0.56f && proporcionA < 0.64f,
                  $"el simbolo de peso 60 sale ~60% de las veces (salio {proporcionA:P1})");
        }

        // ---------- pagos ----------

        private static void TestTresIgualesPagan()
        {
            var motor = new SlotMachineEngine(Maquina());

            // Solo la fila del medio con 'A'; el resto con 'B' para que no sumen
            // ni la diagonal ni las otras filas.
            int[,] g = Grilla(1);
            g[0, 1] = 0; g[1, 1] = 0; g[2, 1] = 0;

            SlotSpinResult r = motor.EvaluarGrilla(g, 10);

            // 'A' paga 5x por tres. 'B' llena arriba y abajo, y paga 10x por tres.
            Check(r.Gano, "una linea de tres iguales paga");
            SlotLineHit medio = r.Lineas.Find(h => h.LineaIndex == 1);
            Check(medio != null && medio.Cantidad == 3 && medio.SimboloIndex == 0,
                  "la linea del medio se detecta con 3 iguales");
            Check(medio != null && medio.Pago == 50, "tres 'A' con apuesta 10 pagan 50");
        }

        private static void TestDosIgualesNoPaganPorDefecto()
        {
            var motor = new SlotMachineEngine(Maquina());

            // Dos 'A' seguidas y despues 'B' en la fila del medio; las demas
            // filas mezcladas para que no aporten.
            int[,] g = Grilla(0);
            g[0, 0] = 0; g[1, 0] = 1; g[2, 0] = 0;   // corta la fila de arriba
            g[0, 1] = 0; g[1, 1] = 0; g[2, 1] = 1;   // dos 'A' y una 'B'
            g[0, 2] = 1; g[1, 2] = 0; g[2, 2] = 0;   // corta la fila de abajo

            SlotSpinResult r = motor.EvaluarGrilla(g, 10);
            SlotLineHit medio = r.Lineas.Find(h => h.LineaIndex == 1);
            Check(medio == null, "dos 'A' seguidas no pagan (su pago por 2 es 0)");

            // 'B' si tiene pago por 2: mismo caso pero al reves.
            int[,] g2 = Grilla(0);
            g2[0, 1] = 1; g2[1, 1] = 1; g2[2, 1] = 0;
            SlotSpinResult r2 = motor.EvaluarGrilla(g2, 10);
            SlotLineHit medio2 = r2.Lineas.Find(h => h.LineaIndex == 1);
            Check(medio2 != null && medio2.Cantidad == 2 && medio2.Pago == 30,
                  "dos 'B' pagan 3x porque estan configuradas para pagar por 2");
        }

        private static void TestComodinCompletaLinea()
        {
            var motor = new SlotMachineEngine(Maquina());

            int[,] g = Grilla(1);
            g[0, 1] = 0; g[1, 1] = 2; g[2, 1] = 0;   // A, comodin, A

            SlotSpinResult r = motor.EvaluarGrilla(g, 10);
            SlotLineHit medio = r.Lineas.Find(h => h.LineaIndex == 1);
            Check(medio != null && medio.Cantidad == 3 && medio.SimboloIndex == 0,
                  "el comodin completa una linea de tres");
            Check(medio != null && medio.Pago == 50, "la linea con comodin paga como el simbolo real");
        }

        private static void TestTodaLaGrillaIgualPagaTodasLasLineas()
        {
            var motor = new SlotMachineEngine(Maquina());
            SlotSpinResult r = motor.EvaluarGrilla(Grilla(0), 10);

            Check(r.Lineas.Count == 5, $"con la grilla entera igual pagan las 5 lineas (pagaron {r.Lineas.Count})");
            Check(r.Pago == 5 * 50, "el pago total es la suma de las lineas");
        }

        private static void TestPagoEscalaConLaApuesta()
        {
            var motor = new SlotMachineEngine(Maquina());
            int[,] g = Grilla(0);

            int pago10 = motor.EvaluarGrilla(g, 10).Pago;
            int pago30 = motor.EvaluarGrilla(g, 30).Pago;

            Check(pago30 == pago10 * 3, "el pago es proporcional a la apuesta");
        }

        private static void TestApuestaSeAjustaALosLimites()
        {
            SlotMachineDefinition def = Maquina();
            Check(def.AjustarApuesta(0) == 10, "la apuesta nunca baja del minimo");
            Check(def.AjustarApuesta(999) == 50, "la apuesta nunca pasa del maximo");
            Check(def.AjustarApuesta(30) == 30, "una apuesta valida no se toca");
        }

        // ---------- economia ----------

        // La pantalla cobra antes de girar y paga despues de resolver. Esto
        // reproduce esa secuencia sobre el PlayerModel de verdad: si el neto no
        // cierra, el bug seria dinero fabricado o perdido.
        private static void TestElDineroDelJugadorCierra()
        {
            var motor = new SlotMachineEngine(Maquina());
            var rng = new SeededRng(7);
            var jugador = new PlayerModel("Test");
            jugador.AddMoney(1000);

            int inicial = jugador.Money;
            int neto = 0;

            for (int i = 0; i < 200; i++)
            {
                const int apuesta = 10;
                if (jugador.Money < apuesta) break;

                jugador.SpendMoney(apuesta);
                SlotSpinResult r = motor.Girar(rng, apuesta);
                if (r.Pago > 0) jugador.AddMoney(r.Pago);
                neto += r.Neto;
            }

            Check(jugador.Money == inicial + neto,
                  $"el saldo cierra con la suma de los netos ({jugador.Money} vs {inicial + neto})");
            Check(jugador.Money >= 0, "el jugador nunca queda en negativo");
        }

        // No es un test de aprobado/desaprobado fino: es el numero que necesita
        // el equipo para balancear. Solo falla si la maquina esta rota (regala
        // plata infinita o no paga nunca).
        private static void TestDevolucionDeLaMaquinaBasica()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SlotMachineDefinition>(MaquinaBasica);
            if (asset == null) return;

            var motor = new SlotMachineEngine(asset);
            float rtp = motor.DevolucionEstimada(new SeededRng(2024), 30000);

            Debug.Log($"[CasinoSmokeTests] Devolucion de '{asset.maquinaNombre}': {rtp:P1} " +
                      "de lo apostado (por debajo de 100% la casa gana a la larga).");
            Check(rtp > 0.2f && rtp < 1f,
                  $"la maquina del piso 1 devuelve menos de lo que recauda ({rtp:P1})");
        }

        // ---------- ruleta de premios de los NPCs ----------

        private static readonly StoryFlag[] Elegibles =
        {
            StoryFlag.Casino_ApostadorVencido,
            StoryFlag.Casino_MalabaristaVencido,
            StoryFlag.Casino_JovenRicaVencida,
            StoryFlag.Casino_ModeloVencida,
            StoryFlag.Casino_MacarraVencido,
            StoryFlag.Casino_GuayVencido
        };

        // Los doce que pelean una vez en los pisos 1 a 3 (los que pone
        // CasinoNpcBuilder). Las pruebas de la regla usan los primeros seis; esta
        // lista es para comprobar que con el proyecto real sigue valiendo.
        private static readonly StoryFlag[] ElegiblesDelProyecto =
        {
            StoryFlag.Casino_ApostadorVencido,
            StoryFlag.Casino_MalabaristaVencido,
            StoryFlag.Casino_JovenRicaVencida,
            StoryFlag.Casino_ModeloVencida,
            StoryFlag.Casino_MacarraVencido,
            StoryFlag.Casino_GuayVencido,
            StoryFlag.Casino_MotoristaVencido,
            StoryFlag.Casino_PokemaniacoVencido,
            StoryFlag.Casino_MediumVencido,
            StoryFlag.Casino_LadronVencido,
            StoryFlag.Casino_VeteranoVencido,
            StoryFlag.Casino_KaratekaVencido
        };

        private static void TestRuletaDeNpcsConLosDoceDelProyecto()
        {
            var rng = new SeededRng(123);
            var veces = new System.Collections.Generic.Dictionary<StoryFlag, int>();
            bool siempreDos = true;
            const int partidas = 6000;
            for (int partida = 0; partida < partidas; partida++)
            {
                var dieron = JugarPartida(rng, ElegiblesDelProyecto.Length, ElegiblesDelProyecto);
                if (dieron.Count != 2) siempreDos = false;
                foreach (StoryFlag f in dieron) veces[f] = veces.TryGetValue(f, out int v) ? v + 1 : 1;
            }
            Check(siempreDos, "con los doce NPCs del proyecto, ganandole a todos la ruleta sale exactamente 2 veces");

            // Cada uno deberia estar en 2 de cada 12 partidas (~16,7%).
            bool parejo = true;
            foreach (StoryFlag f in ElegiblesDelProyecto)
            {
                float p = (veces.TryGetValue(f, out int v) ? v : 0) / (float)partidas;
                if (p < 0.14f || p > 0.195f) parejo = false;
            }
            Check(parejo, "y ninguno de los doce la da mas seguido que otro");
        }

        // Una partida: le gana a 'cuantos' NPCs en un orden al azar y devuelve
        // quienes dieron la ruleta. Replica lo que hace CasinoTrainerNpc al ganar
        // (sortea y despues marca vencido).
        private static System.Collections.Generic.List<StoryFlag> JugarPartida(IBattleRng rng, int cuantos)
        {
            return JugarPartida(rng, cuantos, Elegibles);
        }

        private static System.Collections.Generic.List<StoryFlag> JugarPartida(IBattleRng rng, int cuantos, StoryFlag[] lista)
        {
            StoryFlag[] Elegibles = lista;
            var story = new StoryModel();
            var orden = new System.Collections.Generic.List<StoryFlag>(Elegibles);
            for (int i = orden.Count - 1; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                StoryFlag tmp = orden[i]; orden[i] = orden[j]; orden[j] = tmp;
            }

            var dieron = new System.Collections.Generic.List<StoryFlag>();
            for (int i = 0; i < cuantos && i < orden.Count; i++)
            {
                if (CasinoRuletaDeNpcs.Sortear(story, Elegibles, orden[i], rng.Value()))
                    dieron.Add(orden[i]);
                story.ActivateFlag(orden[i]);
            }
            return dieron;
        }

        private static void TestRuletaDeNpcsSaleDosVecesSiSeLesGanaATodos()
        {
            var rng = new SeededRng(31);
            bool siempreDos = true;
            for (int partida = 0; partida < 2000; partida++)
                if (JugarPartida(rng, Elegibles.Length).Count != 2) siempreDos = false;

            Check(siempreDos, "ganandole a todos los NPCs, la ruleta sale exactamente 2 veces por partida");
        }

        private static void TestRuletaDeNpcsNoSonSiempreLosMismos()
        {
            var rng = new SeededRng(77);
            var veces = new System.Collections.Generic.Dictionary<StoryFlag, int>();
            var pares = new System.Collections.Generic.HashSet<string>();
            const int partidas = 6000;

            for (int partida = 0; partida < partidas; partida++)
            {
                var dieron = JugarPartida(rng, Elegibles.Length);
                foreach (StoryFlag f in dieron) veces[f] = veces.TryGetValue(f, out int v) ? v + 1 : 1;
                dieron.Sort();
                pares.Add(string.Join("+", dieron));
            }

            // Cada NPC deberia estar en 2 de cada 6 partidas.
            bool parejo = true;
            foreach (StoryFlag f in Elegibles)
            {
                float proporcion = (veces.TryGetValue(f, out int v) ? v : 0) / (float)partidas;
                if (proporcion < 0.30f || proporcion > 0.37f) parejo = false;
            }
            Check(parejo, "cada NPC da la ruleta en ~1 de cada 3 partidas: no hay favoritos");
            Check(pares.Count == 15, $"salen los 15 pares posibles de NPCs a lo largo de las partidas (salieron {pares.Count})");
        }

        private static void TestRuletaDeNpcsNuncaPasaDeDos()
        {
            var rng = new SeededRng(5);
            bool nuncaMas = true;
            for (int partida = 0; partida < 1000; partida++)
                for (int cuantos = 0; cuantos <= Elegibles.Length; cuantos++)
                    if (JugarPartida(rng, cuantos).Count > 2) nuncaMas = false;
            Check(nuncaMas, "venza a cuantos venza, la ruleta nunca sale mas de 2 veces");

            var story = new StoryModel();
            story.ActivateFlag(StoryFlag.Casino_RuletaNpc_1);
            story.ActivateFlag(StoryFlag.Casino_RuletaNpc_2);
            Check(CasinoRuletaDeNpcs.Probabilidad(story, Elegibles, Elegibles[0]) == 0f,
                  "con las dos ruletas usadas, la probabilidad es 0");

            // Ganandole solo a uno, con dos ruletas y seis NPCs: 2/6.
            var nueva = new StoryModel();
            float p = CasinoRuletaDeNpcs.Probabilidad(nueva, Elegibles, Elegibles[3]);
            Check(Mathf.Abs(p - 2f / 6f) < 0.001f, $"al primer NPC le toca con 2/6 ({p:0.000})");
        }

        private static void TestRuletaDeNpcsSoloLosDeLaLista()
        {
            var story = new StoryModel();
            Check(CasinoRuletaDeNpcs.Probabilidad(story, Elegibles, StoryFlag.Casino_GuardiaVencido) == 0f,
                  "un NPC fuera de la lista no da la ruleta");
            Check(CasinoRuletaDeNpcs.Probabilidad(story, Elegibles, StoryFlag.None) == 0f,
                  "un NPC sin flag no da la ruleta");
            Check(!CasinoRuletaDeNpcs.Sortear(story, Elegibles, StoryFlag.Casino_GuardiaVencido, 0f),
                  "y aunque la tirada sea 0, no la da");
            Check(CasinoRuletaDeNpcs.Usadas(story) == 0, "un sorteo fallido no gasta una ruleta");
        }

        // ---------- entrega de premios ----------

        private static void TestPremioDePlata()
        {
            var jugador = new PlayerModel("Test");
            string texto = PrizeGranter.Entregar(new Prize { prizeName = "150 monedas", money = 150 }, jugador);
            Check(jugador.Money == 150, "un premio de plata suma las monedas");
            Check(texto.Contains("150"), $"y el cartel dice cuanto ({texto})");
        }

        private static void TestPremioPokemonSeSumaAlEquipo()
        {
            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>("Assets/Resources/Definitions/Pokens/0025_Pikachu.asset");
            if (especie == null) { Debug.LogWarning("[CasinoSmokeTests] No esta Pikachu: se saltea la prueba."); return; }

            var jugador = new PlayerModel("Test");
            string texto = PrizeGranter.Entregar(new Prize { poken = especie, pokenLevel = 15 }, jugador);
            Check(jugador.Team.Count == 1 && jugador.Team[0].Definition == especie,
                  "un premio de Pokemon lo suma al equipo");
            Check(jugador.Team.Count == 1 && jugador.Team[0].Level == 15, "con el nivel del premio");
            Check(texto.Contains(especie.pokenName), $"y el cartel lo nombra ({texto})");
        }

        private static void TestPremioPokemonConEquipoLlenoPagaPlata()
        {
            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>("Assets/Resources/Definitions/Pokens/0025_Pikachu.asset");
            if (especie == null) return;

            var jugador = new PlayerModel("Test");
            for (int i = 0; i < PrizeGranter.EquipoMaximo; i++) jugador.Team.Add(new PokenModel(especie, 5));

            string texto = PrizeGranter.Entregar(new Prize { poken = especie, pokenLevel = 15, moneyIfTeamFull = 300 }, jugador);
            Check(jugador.Team.Count == PrizeGranter.EquipoMaximo, "con el equipo lleno no se pasa de 6");
            Check(jugador.Money == 300, "y se paga la plata de compensacion");
            Check(texto.Contains("300"), $"y el cartel lo avisa ({texto})");
        }

        // Las tablas que arma el builder de personajes: si alguien las rebalancea
        // tiene que seguir cumpliendo lo pedido.
        private static void TestTablasDeRuletaDelProyecto()
        {
            var npcs = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>("Assets/Resources/Definitions/Casino/RuletaDeLosNpcs.asset");
            var guardia = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>("Assets/Resources/Definitions/Casino/RuletaDelGuardia.asset");
            if (npcs == null || guardia == null)
            {
                Debug.LogWarning("[CasinoSmokeTests] Todavia no estan las tablas de ruleta. " +
                                 "Corre PokeRush/Casino/Poner los personajes de los pisos 1 a 3.");
                return;
            }

            float pokemon = npcs.Porcentaje(p => p.poken != null);
            float curativos = npcs.Porcentaje(p => p.poken == null && p.item != null);
            float plata = npcs.Porcentaje(p => p.poken == null && p.item == null && p.money > 0);
            Check(Mathf.Abs(pokemon - 0.35f) < 0.005f, $"la ruleta de los NPCs da Pokemon el 35% de las veces ({pokemon:P1})");
            Check(curativos > 0f && plata > 0f, $"y el resto se reparte entre curativos ({curativos:P0}) y plata ({plata:P0})");
            Check(Mathf.Abs(pokemon + curativos + plata - 1f) < 0.001f, "y no hay gajos que no den nada");
            Check(npcs.premios.Length <= 8, "entra en los 8 gajos de la rueda");
            bool listaCompleta = npcs.npcsQueLaPuedenDar != null &&
                                 npcs.npcsQueLaPuedenDar.Length == ElegiblesDelProyecto.Length;
            foreach (StoryFlag f in ElegiblesDelProyecto)
                if (npcs.npcsQueLaPuedenDar == null || System.Array.IndexOf(npcs.npcsQueLaPuedenDar, f) < 0) listaCompleta = false;
            Check(listaCompleta, "la lista de NPCs que la dan tiene a los doce que pelean");

            bool todosPokemon = guardia.premios.Length > 0;
            float menor = float.MaxValue, mayor = 0f;
            foreach (Prize p in guardia.premios)
            {
                if (p == null || p.poken == null || p.probability <= 0f) todosPokemon = false;
                if (p == null) continue;
                menor = Mathf.Min(menor, p.probability);
                mayor = Mathf.Max(mayor, p.probability);
            }
            Check(todosPokemon, "la ruleta del guardia da un Pokemon si o si");
            Check(menor < mayor, "y tiene Pokemon mas raros que otros");
            Check(guardia.premios.Length <= 8, "la ruleta del guardia entra en los 8 gajos");
        }
    }
}
