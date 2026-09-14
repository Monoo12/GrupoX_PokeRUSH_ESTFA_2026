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

            TestRuletaDeNpcsConLosDelProyecto();
            TestRuletaDeNpcsSaleUnaCadaDosCombates();
            TestRuletaDeNpcsNoSonSiempreLosMismos();
            TestRuletaDeNpcsNuncaPasaDeCuatro();
            TestRuletaDeNpcsSoloLosDeLaLista();
            TestPremioDePlata();
            TestPremioPokemonSeSumaAlEquipo();
            TestPremioPokemonConEquipoLlenoVaAlPc();
            TestPremioPokemonConPcLlenoPagaPlata();
            TestPremioObjetoClave();
            TestTablasDeRuletaDelProyecto();

            TestPcReglas();
            TestRecibirPokemonVaAlPc();
            TestShinyUsaSusSprites();
            TestGananciaDeColor();
            TestAscensorPideElShiny();
            TestApuestaDeTacconi();
            TestRevendedorDeLaTarjeta();

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
        // CasinoNpcBuilder). Las pruebas de la regla usan esta lista; la de seis
        // de arriba queda para los casos que no dependen de cuantos son.
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
            StoryFlag.Casino_KaratekaVencido,
            StoryFlag.Casino_TahurVencido,
            StoryFlag.Casino_BailarinaVencida,
            StoryFlag.Casino_GangsterVencido,
            StoryFlag.Casino_CrupieraVencida
        };

        private static void TestRuletaDeNpcsConLosDelProyecto()
        {
            var rng = new SeededRng(123);
            var veces = new System.Collections.Generic.Dictionary<StoryFlag, int>();
            bool siempreCuatro = true;
            const int partidas = 6000;
            for (int partida = 0; partida < partidas; partida++)
            {
                var dieron = JugarPartida(rng, ElegiblesDelProyecto.Length, ElegiblesDelProyecto, mezclar: true);
                if (dieron.Count != CasinoRuletaDeNpcs.MaximoPorPartida) siempreCuatro = false;
                foreach (StoryFlag f in dieron) veces[f] = veces.TryGetValue(f, out int v) ? v + 1 : 1;
            }
            Check(siempreCuatro, $"con los {ElegiblesDelProyecto.Length} NPCs del proyecto, ganandole a todos la ruleta sale exactamente 4 veces");

            // Peleando en cualquier orden, cada uno deberia estar en 4 de cada N
            // partidas.
            float esperado = 4f / ElegiblesDelProyecto.Length;
            bool parejo = true;
            foreach (StoryFlag f in ElegiblesDelProyecto)
            {
                float p = (veces.TryGetValue(f, out int v) ? v : 0) / (float)partidas;
                if (Mathf.Abs(p - esperado) > 0.03f) parejo = false;
            }
            Check(parejo, $"y ninguno la da mas seguido que otro (~{esperado:P0} cada uno)");
        }

        // Una partida: le gana a los primeros 'cuantos' NPCs de la lista (en ese
        // orden o mezclados) y devuelve quienes dieron la ruleta. Replica lo que
        // hace CasinoTrainerNpc al ganar (sortea y despues marca vencido).
        private static System.Collections.Generic.List<StoryFlag> JugarPartida(IBattleRng rng, int cuantos,
            StoryFlag[] lista, bool mezclar)
        {
            var story = new StoryModel();
            var orden = new System.Collections.Generic.List<StoryFlag>(lista);
            if (mezclar)
            {
                for (int i = orden.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    StoryFlag tmp = orden[i]; orden[i] = orden[j]; orden[j] = tmp;
                }
            }

            var dieron = new System.Collections.Generic.List<StoryFlag>();
            for (int i = 0; i < cuantos && i < orden.Count; i++)
            {
                if (CasinoRuletaDeNpcs.Sortear(story, lista, orden[i], rng.Value()))
                    dieron.Add(orden[i]);
                story.ActivateFlag(orden[i]);
            }
            return dieron;
        }

        // Lo pedido: una cada dos combates. Despues de cada combate ganado se
        // mira cuantas salieron: al cerrar cada par, una por par; en el medio,
        // como mucho la de ese par. Y nunca mas de cuatro.
        private static void TestRuletaDeNpcsSaleUnaCadaDosCombates()
        {
            var rng = new SeededRng(31);
            int maximo = CasinoRuletaDeNpcs.MaximoPorPartida;
            bool cadaDos = true;
            for (int partida = 0; partida < 2000; partida++)
            {
                var story = new StoryModel();
                var orden = new System.Collections.Generic.List<StoryFlag>(ElegiblesDelProyecto);
                for (int i = orden.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    StoryFlag tmp = orden[i]; orden[i] = orden[j]; orden[j] = tmp;
                }

                for (int i = 0; i < orden.Count; i++)
                {
                    CasinoRuletaDeNpcs.Sortear(story, ElegiblesDelProyecto, orden[i], rng.Value());
                    story.ActivateFlag(orden[i]);

                    int ganados = i + 1;
                    int usadas = CasinoRuletaDeNpcs.Usadas(story);
                    if (usadas < Mathf.Min(ganados / 2, maximo) || usadas > Mathf.Min((ganados + 1) / 2, maximo))
                        cadaDos = false;
                }
            }
            Check(cadaDos, "sale una ruleta cada dos combates ganados, hasta 4 por partida");
        }

        // Aunque el jugador pelee SIEMPRE en el mismo orden, la ruleta no la
        // dan siempre los mismos: en cada par la puede dar cualquiera de los dos.
        private static void TestRuletaDeNpcsNoSonSiempreLosMismos()
        {
            var rng = new SeededRng(77);
            var veces = new System.Collections.Generic.Dictionary<StoryFlag, int>();
            var combinaciones = new System.Collections.Generic.HashSet<string>();
            const int partidas = 4000;

            for (int partida = 0; partida < partidas; partida++)
            {
                var dieron = JugarPartida(rng, ElegiblesDelProyecto.Length, ElegiblesDelProyecto, mezclar: false);
                foreach (StoryFlag f in dieron) veces[f] = veces.TryGetValue(f, out int v) ? v + 1 : 1;
                combinaciones.Add(string.Join("+", dieron));
            }

            // Los ocho primeros del orden caen en un par con ruleta: ~50% cada uno.
            bool parejo = true;
            for (int i = 0; i < 8; i++)
            {
                float proporcion = (veces.TryGetValue(ElegiblesDelProyecto[i], out int v) ? v : 0) / (float)partidas;
                if (proporcion < 0.46f || proporcion > 0.54f) parejo = false;
            }
            bool despuesNoSale = true;
            for (int i = 8; i < ElegiblesDelProyecto.Length; i++)
                if (veces.ContainsKey(ElegiblesDelProyecto[i])) despuesNoSale = false;

            Check(parejo, "con el mismo orden de peleas, cada NPC de los cuatro primeros pares la da ~1 de cada 2 partidas");
            Check(despuesNoSale, "del noveno combate en adelante ya no sale (tope de 4)");
            Check(combinaciones.Count == 16, $"salen las 16 combinaciones posibles (salieron {combinaciones.Count})");
        }

        private static void TestRuletaDeNpcsNuncaPasaDeCuatro()
        {
            var rng = new SeededRng(5);
            bool nuncaMas = true;
            for (int partida = 0; partida < 1000; partida++)
                for (int cuantos = 0; cuantos <= ElegiblesDelProyecto.Length; cuantos++)
                    if (JugarPartida(rng, cuantos, ElegiblesDelProyecto, mezclar: true).Count > CasinoRuletaDeNpcs.MaximoPorPartida)
                        nuncaMas = false;
            Check(nuncaMas, "venza a cuantos venza, la ruleta nunca sale mas de 4 veces");

            var story = new StoryModel();
            foreach (StoryFlag uso in CasinoRuletaDeNpcs.FlagsDeUso) story.ActivateFlag(uso);
            Check(CasinoRuletaDeNpcs.Probabilidad(story, ElegiblesDelProyecto, ElegiblesDelProyecto[0]) == 0f,
                  "con las cuatro ruletas usadas, la probabilidad es 0");

            // El primer combate: 1/2. El segundo: seguro si el primero no la dio,
            // nada si la dio.
            var nueva = new StoryModel();
            float p = CasinoRuletaDeNpcs.Probabilidad(nueva, ElegiblesDelProyecto, ElegiblesDelProyecto[3]);
            Check(Mathf.Abs(p - 0.5f) < 0.001f, $"al primer NPC le toca con 1/2 ({p:0.000})");

            nueva.ActivateFlag(ElegiblesDelProyecto[3]);
            Check(CasinoRuletaDeNpcs.Probabilidad(nueva, ElegiblesDelProyecto, ElegiblesDelProyecto[5]) == 1f,
                  "si el primero del par no la dio, al segundo le toca seguro");

            nueva.ActivateFlag(StoryFlag.Casino_RuletaNpc_1);
            Check(CasinoRuletaDeNpcs.Probabilidad(nueva, ElegiblesDelProyecto, ElegiblesDelProyecto[5]) == 0f,
                  "si el primero del par la dio, el segundo no");
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

        private static void TestPremioPokemonConEquipoLlenoVaAlPc()
        {
            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>("Assets/Resources/Definitions/Pokens/0025_Pikachu.asset");
            if (especie == null) return;

            var jugador = new PlayerModel("Test");
            for (int i = 0; i < PrizeGranter.EquipoMaximo; i++) jugador.Team.Add(new PokenModel(especie, 5));

            string texto = PrizeGranter.Entregar(new Prize { poken = especie, pokenLevel = 15, moneyIfTeamFull = 300 }, jugador);
            Check(jugador.Team.Count == PrizeGranter.EquipoMaximo, "con el equipo lleno no se pasa de 6");
            Check(jugador.Storage.Count == 1 && jugador.Storage.Get(0, 0)?.Definition == especie,
                  "y el Pokemon va a la primera caja del PC");
            Check(jugador.Money == 0, "sin pagar compensacion");
            Check(texto.Contains("PC"), $"y el cartel dice que fue al PC ({texto})");
        }

        private static void TestPremioPokemonConPcLlenoPagaPlata()
        {
            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>("Assets/Resources/Definitions/Pokens/0025_Pikachu.asset");
            if (especie == null) return;

            var jugador = new PlayerModel("Test");
            for (int i = 0; i < PrizeGranter.EquipoMaximo; i++) jugador.Team.Add(new PokenModel(especie, 5));
            var relleno = new PokenModel(especie, 5);
            while (jugador.Storage.TryAgregar(relleno, out _, out _)) { }

            string texto = PrizeGranter.Entregar(new Prize { poken = especie, pokenLevel = 15, moneyIfTeamFull = 300 }, jugador);
            Check(jugador.Money == 300, "con el equipo y el PC llenos se paga la plata de compensacion");
            Check(texto.Contains("300"), $"y el cartel lo avisa ({texto})");
        }

        private static void TestPremioObjetoClave()
        {
            var tarjeta = ScriptableObject.CreateInstance<SpecialItemDefinition>();
            tarjeta.itemName = "Tarjeta VIP";
            try
            {
                var jugador = new PlayerModel("Test");
                var premio = new Prize { prizeName = "Tarjeta VIP", article = "la", specialItem = tarjeta, moneyIfOwned = 250 };

                PrizeGranter.Entregar(premio, jugador);
                Check(jugador.Inventory.HasSpecialItem(tarjeta), "un premio de objeto clave lo mete en la mochila");

                string texto = PrizeGranter.Entregar(premio, jugador);
                Check(jugador.Inventory.SpecialItems.Count == 1 && jugador.Inventory.SpecialItems[0].Quantity == 1,
                      "ganarlo otra vez no lo repite");
                Check(jugador.Money == 250, $"y paga la plata en su lugar ({texto})");
            }
            finally
            {
                Object.DestroyImmediate(tarjeta);
            }
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
            float tarjeta = npcs.Porcentaje(p => p.poken == null && p.specialItem != null);
            float curativos = npcs.Porcentaje(p => p.poken == null && p.specialItem == null && p.item != null);
            float plata = npcs.Porcentaje(p => p.poken == null && p.specialItem == null && p.item == null && p.money > 0);
            Check(pokemon >= 0.25f, $"la ruleta de los NPCs da Pokemon ({pokemon:P1})");
            Check(Mathf.Abs(tarjeta - 0.15f) < 0.02f, $"y la Tarjeta VIP cerca del 15% ({tarjeta:P1})");
            Check(curativos > 0f && plata > 0f, $"y el resto se reparte entre curativos ({curativos:P0}) y plata ({plata:P0})");
            Check(Mathf.Abs(pokemon + tarjeta + curativos + plata - 1f) < 0.001f, "y no hay gajos que no den nada");
            Check(npcs.premios.Length <= 8, "entra en los 8 gajos de la rueda");
            bool listaCompleta = npcs.npcsQueLaPuedenDar != null &&
                                 npcs.npcsQueLaPuedenDar.Length == ElegiblesDelProyecto.Length;
            foreach (StoryFlag f in ElegiblesDelProyecto)
                if (npcs.npcsQueLaPuedenDar == null || System.Array.IndexOf(npcs.npcsQueLaPuedenDar, f) < 0) listaCompleta = false;
            Check(listaCompleta, $"la lista de NPCs que la dan tiene a los {ElegiblesDelProyecto.Length} que pelean");

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

            // Todas las ruletas de premios tienen que dar alguna chance de Pokemon.
            var mesa = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>("Assets/Resources/Definitions/Casino/RuletaDeLaMesa.asset");
            var guardiaVip = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>("Assets/Resources/Definitions/Casino/RuletaDelGuardiaVip.asset");
            if (mesa == null || guardiaVip == null)
            {
                Debug.LogWarning("[CasinoSmokeTests] Todavia no estan las ruletas del piso 2. " +
                                 "Corre PokeRush/Casino/Poner los personajes de los pisos 2 y 3.");
                return;
            }

            Check(mesa.Porcentaje(p => p.poken != null) > 0f,
                  $"la ruleta de las mesas da Pokemon ({mesa.Porcentaje(p => p.poken != null):P0})");
            Check(mesa.premios.Length <= 8, "la ruleta de las mesas entra en los 8 gajos");

            bool vipTodosPokemon = guardiaVip.premios.Length > 0;
            foreach (Prize p in guardiaVip.premios)
                if (p == null || p.poken == null || p.probability <= 0f) vipTodosPokemon = false;
            Check(vipTodosPokemon, "la ruleta del guardia VIP da un Pokemon si o si");
            Check(guardiaVip.premios.Length <= 8, "la ruleta del guardia VIP entra en los 8 gajos");
        }

        // ---------- PC, shiny y lo nuevo de los pisos 2 y 3 ----------

        private static PokenDefinition Especie(string archivo)
            => AssetDatabase.LoadAssetAtPath<PokenDefinition>($"Assets/Resources/Definitions/Pokens/{archivo}.asset");

        private static void TestPcReglas()
        {
            PokenDefinition pikachu = Especie("0025_Pikachu"), eevee = Especie("0133_Eevee");
            if (pikachu == null || eevee == null) return;

            var jugador = new PlayerModel("Test");
            var a = new PokenModel(pikachu, 10);
            var b = new PokenModel(eevee, 10);
            jugador.Team.Add(a);
            jugador.Team.Add(b);

            Check(PcReglas.Mover(jugador, PcLugar.DelEquipo(1), PcLugar.DeCaja(0, 5)) == null &&
                  jugador.Team.Count == 1 && jugador.Storage.Get(0, 5) == b,
                  "PC: dejar un Pokemon en una caja lo saca del equipo");

            string error = PcReglas.Mover(jugador, PcLugar.DelEquipo(0), PcLugar.DeCaja(0, 6));
            Check(error != null && jugador.Team.Count == 1 && jugador.Storage.Get(0, 6) == null,
                  "PC: no deja el equipo vacio");

            Check(PcReglas.Mover(jugador, PcLugar.DeCaja(0, 5), PcLugar.DelEquipo(jugador.Team.Count)) == null &&
                  jugador.Team.Count == 2 && jugador.Team[1] == b && jugador.Storage.Get(0, 5) == null,
                  "PC: retirar a un hueco vacio lo suma al final del equipo");

            var c = new PokenModel(pikachu, 3);
            jugador.Storage.Set(1, 0, c);
            Check(PcReglas.Mover(jugador, PcLugar.DeCaja(1, 0), PcLugar.DelEquipo(0)) == null &&
                  jugador.Team[0] == c && jugador.Storage.Get(1, 0) == a,
                  "PC: soltar sobre alguien del equipo los intercambia");

            Check(PcReglas.Mover(jugador, PcLugar.DeCaja(1, 0), PcLugar.DeCaja(2, 9)) == null &&
                  jugador.Storage.Get(2, 9) == a && jugador.Storage.Get(1, 0) == null,
                  "PC: mover entre cajas");

            // Con un debilitado: el equipo no puede quedar solo con el.
            b.TakeDamage(9999);
            error = PcReglas.Mover(jugador, PcLugar.DelEquipo(0), PcLugar.DeCaja(0, 0));
            Check(error != null && jugador.Team.Count == 2,
                  "PC: no deja al equipo solo con un Pokemon debilitado");

            for (int i = jugador.Team.Count; i < PlayerModel.EquipoMaximo; i++) jugador.Team.Add(new PokenModel(pikachu, 5));
            error = PcReglas.Mover(jugador, PcLugar.DeCaja(2, 9), PcLugar.DelEquipo(PlayerModel.EquipoMaximo));
            Check(error != null && jugador.Storage.Get(2, 9) == a, "PC: con el equipo lleno no se retira a un hueco vacio");
        }

        private static void TestRecibirPokemonVaAlPc()
        {
            PokenDefinition pikachu = Especie("0025_Pikachu");
            if (pikachu == null) return;

            var jugador = new PlayerModel("Test");
            for (int i = 0; i < PlayerModel.EquipoMaximo - 1; i++) jugador.Team.Add(new PokenModel(pikachu, 5));

            PokenRecibido r1 = jugador.RecibirPokemon(new PokenModel(pikachu, 5));
            PokenRecibido r2 = jugador.RecibirPokemon(new PokenModel(pikachu, 5));
            Check(r1.Destino == DestinoDePokemon.Equipo && jugador.Team.Count == PlayerModel.EquipoMaximo,
                  "Recibir: con lugar va al equipo");
            Check(r2.Destino == DestinoDePokemon.Pc && r2.Caja == 0 && jugador.Storage.Count == 1,
                  "Recibir: con el equipo lleno va a la primera caja del PC");
        }

        private static void TestShinyUsaSusSprites()
        {
            PokenDefinition cosmog = Especie("0789_Cosmog");
            if (cosmog == null || cosmog.inventoryShinySprite == null) return;

            var normal = new PokenModel(cosmog, 5);
            var shiny = new PokenModel(cosmog, 5);
            shiny.SetShiny(true);
            Check(normal.IconSprite == cosmog.inventorySprite && shiny.IconSprite == cosmog.inventoryShinySprite,
                  "Shiny: el icono es el shiny solo si el ejemplar lo es");
            Check(shiny.FrontFrames == cosmog.battleFrontShinyFrames || cosmog.battleFrontShinyFrames == null || cosmog.battleFrontShinyFrames.Length == 0,
                  "Shiny: en combate usa los frames shiny");
        }

        private static void TestGananciaDeColor()
        {
            var apuestas = new System.Collections.Generic.Dictionary<RouletteBet, int>
            {
                [RouletteBet.AlRojo()] = 250,
                [RouletteBet.AlNumero(1)] = 50
            };

            // El 1 es rojo: cobran las dos, pero solo cuenta la del color.
            int alRojo = RouletteBoard.GananciaDeColor(apuestas, 1);
            Check(alRojo == 250, $"Ruleta: la ganancia al color no cuenta el pleno ({alRojo})");
            Check(RouletteBoard.DaRuletaDePremios(alRojo, 200), "Ruleta: ganar 250 al color abre la ruleta de premios");
            Check(RouletteBoard.GananciaDeColor(apuestas, 2) == 0, "Ruleta: si sale negro, el rojo no gana nada");
            Check(RouletteBoard.GananciaDeColor(apuestas, 0) == 0, "Ruleta: con el cero pierden los colores");

            var justo = new System.Collections.Generic.Dictionary<RouletteBet, int> { [RouletteBet.AlNegro()] = 200 };
            Check(!RouletteBoard.DaRuletaDePremios(RouletteBoard.GananciaDeColor(justo, 2), 200),
                  "Ruleta: ganar 200 justos no alcanza (tiene que ser MAS de 200)");
        }

        private static void TestAscensorPideElShiny()
        {
            PokenDefinition pikachu = Especie("0025_Pikachu"), cosmog = Especie("0789_Cosmog");
            if (pikachu == null || cosmog == null) return;

            // RequireComponent(Collider2D) no se puede cumplir solo: Collider2D es
            // abstracto, asi que sin un collider concreto AddComponent devuelve null.
            var go = new GameObject("AscensorDePrueba");
            go.AddComponent<BoxCollider2D>();
            try
            {
                var ascensor = go.AddComponent<CasinoElevatorInteractable>();
                var so = new SerializedObject(ascensor);
                so.FindProperty("requisito").enumValueIndex = (int)CasinoRequisitoTipo.TenerPokemon;
                so.FindProperty("especieRequerida").objectReferenceValue = cosmog;
                so.FindProperty("debeSerShiny").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                var story = new StoryModel();
                var jugador = new PlayerModel("Test");
                jugador.Team.Add(new PokenModel(pikachu, 10));

                Check(!ascensor.CumpleRequisito(jugador, story, out string mensaje) && mensaje.Contains(cosmog.pokenName),
                      $"Ascensor 3: sin el Cosmog shiny no sube ({mensaje})");

                jugador.Team.Add(new PokenModel(cosmog, 15));
                Check(!ascensor.CumpleRequisito(jugador, story, out _), "Ascensor 3: un Cosmog que no es shiny no alcanza");

                var shiny = new PokenModel(cosmog, 15);
                shiny.SetShiny(true);
                jugador.Storage.Set(0, 0, shiny);
                Check(ascensor.CumpleRequisito(jugador, story, out _), "Ascensor 3: con el Cosmog shiny, aunque este en el PC, sube");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void TestApuestaDeTacconi()
        {
            PokenDefinition pikachu = Especie("0025_Pikachu"), eevee = Especie("0133_Eevee"), cosmog = Especie("0789_Cosmog");
            if (pikachu == null || eevee == null || cosmog == null) return;

            var go = new GameObject("TacconiDePrueba");
            go.AddComponent<BoxCollider2D>();
            try
            {
                var tacconi = go.AddComponent<CasinoTacconiNpc>();
                var so = new SerializedObject(tacconi);
                so.FindProperty("especiePremio").objectReferenceValue = cosmog;
                so.ApplyModifiedPropertiesWithoutUndo();

                var story = new StoryModel();
                var jugador = new PlayerModel("Test");
                var a = new PokenModel(pikachu, 10);
                var b = new PokenModel(eevee, 10);

                jugador.Team.Add(a);
                Check(CasinoTacconiNpc.MotivoParaNoAceptar(jugador, a) != null, "Tacconi: con un solo Pokemon no acepta la apuesta");

                jugador.Team.Add(b);
                b.TakeDamage(9999);
                Check(CasinoTacconiNpc.MotivoParaNoAceptar(jugador, a) != null,
                      "Tacconi: no acepta si al perder solo queda un debilitado");
                Check(CasinoTacconiNpc.MotivoParaNoAceptar(jugador, b) == null, "Tacconi: apostar al debilitado si se puede");
                b.Heal(9999);

                tacconi.AplicarResultado(jugador, story, a, BlackjackOutcome.Pierde);
                Check(!jugador.Team.Contains(a) && jugador.Team.Count == 1, "Tacconi: perdiendo, se queda con el Pokemon apostado");
                Check(!story.HasFlag(StoryFlag.Casino_TacconiVencido), "Tacconi: perdiendo no queda anotado como ganado");

                tacconi.AplicarResultado(jugador, story, b, BlackjackOutcome.Empata);
                Check(jugador.Team.Count == 1 && jugador.Team.Contains(b), "Tacconi: empatando no pasa nada");

                tacconi.AplicarResultado(jugador, story, b, BlackjackOutcome.Gana);
                Check(jugador.Team.Exists(p => p.Definition == cosmog && p.IsShiny), "Tacconi: ganando, el Cosmog shiny se suma al equipo");
                Check(jugador.Team.Contains(b), "Tacconi: y el jugador no pierde el suyo");
                Check(story.HasFlag(StoryFlag.Casino_TacconiVencido), "Tacconi: y queda anotado");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void TestRevendedorDeLaTarjeta()
        {
            var tarjeta = ScriptableObject.CreateInstance<SpecialItemDefinition>();
            tarjeta.itemName = "Tarjeta VIP";
            var go = new GameObject("RevendedorDePrueba");
            go.AddComponent<BoxCollider2D>();
            try
            {
                var vendedor = go.AddComponent<CasinoVendedorNpc>();
                var so = new SerializedObject(vendedor);
                so.FindProperty("objeto").objectReferenceValue = tarjeta;
                so.FindProperty("precio").intValue = 1500;
                so.ApplyModifiedPropertiesWithoutUndo();

                var jugador = new PlayerModel("Test");
                jugador.AddMoney(1000);
                Check(!vendedor.Comprar(jugador) && jugador.Money == 1000 && !jugador.Inventory.HasSpecialItem(tarjeta),
                      "Revendedor: sin 1500 no la vende");

                jugador.AddMoney(600);
                Check(vendedor.Comprar(jugador) && jugador.Money == 100 && jugador.Inventory.HasSpecialItem(tarjeta),
                      "Revendedor: con 1500 la vende y la cobra");
                Check(!vendedor.Comprar(jugador) && jugador.Money == 100, "Revendedor: no la vende dos veces");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(tarjeta);
            }
        }
    }
}
