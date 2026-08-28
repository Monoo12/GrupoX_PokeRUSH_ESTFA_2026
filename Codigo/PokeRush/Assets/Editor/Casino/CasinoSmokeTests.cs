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

            // ---- ruleta ----
            TestMesaDeRuletaValida();
            TestRuletaRespetaPesos();
            TestRuletaDaYQuitaDinero();
            TestRuletaNuncaDejaEnNegativo();
            TestRuletaDaObjetosYLosSaca();
            TestRuletaDaPokenSinDuplicar();
            TestRuletaNoDebilitaAlEquipo();

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

        // ================= ruleta =================

        private const string MesaBasica = "Assets/Resources/Definitions/Casino/MesaDeRuleta.asset";

        // Mesa de prueba con numeros redondos, independiente del asset del
        // proyecto: si el equipo rebalancea la ruleta, estos tests siguen
        // diciendo lo mismo.
        private static RouletteTableDefinition Mesa(params RoulettePrize[] premios)
        {
            var def = ScriptableObject.CreateInstance<RouletteTableDefinition>();
            def.mesaNombre = "TEST";
            def.costoPorGiro = 50;
            def.flagObjetivo = StoryFlag.Casino_Ruleta_ObjetivoCumplido;
            def.premios = premios;
            return def;
        }

        private static RoulettePrize P(string nombre, RoulettePrizeKind tipo, int peso, int monto = 0)
            => new RoulettePrize { nombre = nombre, tipo = tipo, peso = peso, monto = monto };

        private static PokenDefinition EspecieDePrueba(string nombre)
        {
            var especie = ScriptableObject.CreateInstance<PokenDefinition>();
            especie.pokenName = nombre;
            especie.type = PokenType.Normal;
            especie.type2 = PokenType.None;
            especie.baseHP = 60;
            especie.baseAttack = 60;
            especie.baseDefense = 60;
            especie.baseSpecialAttack = 60;
            especie.baseSpecialDefense = 60;
            especie.baseSpeed = 60;
            especie.baseXpYield = 60;
            return especie;
        }

        private static PlayerModel JugadorConEquipo(int cuantos, int plata)
        {
            var jugador = new PlayerModel("Test");
            jugador.AddMoney(plata);
            for (int i = 0; i < cuantos; i++)
                jugador.Team.Add(new PokenModel(EspecieDePrueba("Test" + i), 20));
            return jugador;
        }

        private static void TestMesaDeRuletaValida()
        {
            string error;
            Check(Mesa(P("a", RoulettePrizeKind.Dinero, 1), P("b", RoulettePrizeKind.Nada, 1))
                      .Validar(out error), "la mesa de prueba valida");

            var sinPremios = Mesa();
            Check(!sinPremios.Validar(out error), "una mesa sin casillas no valida");

            var sinObjeto = Mesa(P("x", RoulettePrizeKind.Objeto, 1), P("y", RoulettePrizeKind.Nada, 1));
            Check(!sinObjeto.Validar(out error),
                  "una casilla de objeto sin objeto asignado no valida");

            var asset = AssetDatabase.LoadAssetAtPath<RouletteTableDefinition>(MesaBasica);
            if (asset == null)
            {
                Debug.LogWarning("[CasinoSmokeTests] Todavia no existe " + MesaBasica + ".");
                return;
            }
            Check(asset.Validar(out error), "la mesa del piso 2 valida: " + error);

            bool tieneObjetivo = false;
            foreach (RoulettePrize p in asset.premios)
                if (p != null && p.cumpleObjetivo && p.peso > 0) tieneObjetivo = true;
            Check(tieneObjetivo, "la mesa del piso 2 tiene una casilla que cumple el objetivo");
        }

        private static void TestRuletaRespetaPesos()
        {
            var mesa = Mesa(P("comun", RoulettePrizeKind.Nada, 75),
                            P("rara", RoulettePrizeKind.Dinero, 25, 10));
            var motor = new RouletteEngine(mesa);
            var rng = new SeededRng(4242);

            int raras = 0;
            const int giros = 20000;
            for (int i = 0; i < giros; i++)
                if (motor.Girar(rng).PremioIndex == 1) raras++;

            float proporcion = raras / (float)giros;
            Check(proporcion > 0.22f && proporcion < 0.28f,
                  $"la casilla de peso 25 sale ~25% de las veces (salio {proporcion:P1})");
            Check(Mathf.Approximately(motor.Probabilidad(1), 0.25f),
                  "la probabilidad informada coincide con el peso");
        }

        private static void TestRuletaDaYQuitaDinero()
        {
            var jugador = JugadorConEquipo(1, 500);

            RouletteRewardService.Aplicar(P("premio", RoulettePrizeKind.Dinero, 1, 200), jugador);
            Check(jugador.Money == 700, $"la casilla de dinero paga ({jugador.Money})");

            RouletteRewardService.Aplicar(P("multa", RoulettePrizeKind.PerderDinero, 1, 300), jugador);
            Check(jugador.Money == 400, $"la casilla de multa cobra ({jugador.Money})");
        }

        // La penalizacion no puede dejar deuda encubierta ni saldo negativo: eso
        // romperia las comparaciones de dinero de los ascensores.
        private static void TestRuletaNuncaDejaEnNegativo()
        {
            var jugador = JugadorConEquipo(1, 30);
            RouletteRewardService.Aplicar(P("multa", RoulettePrizeKind.PerderDinero, 1, 500), jugador);
            Check(jugador.Money == 0, $"perder mas de lo que hay deja el saldo en 0 ({jugador.Money})");
        }

        private static void TestRuletaDaObjetosYLosSaca()
        {
            var objetos = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(
                "Assets/Resources/Definitions/Items/Pocion.asset");
            if (objetos == null) return;

            var jugador = JugadorConEquipo(1, 0);

            var premio = new RoulettePrize
            {
                nombre = "objeto", tipo = RoulettePrizeKind.Objeto,
                peso = 1, objeto = objetos, cantidad = 2
            };
            RouletteRewardService.Aplicar(premio, jugador);
            Check(jugador.Inventory.CountHealItem(objetos) == 2,
                  "la casilla de objeto lo mete en la mochila");

            RouletteRewardService.Aplicar(P("manotazo", RoulettePrizeKind.PerderObjeto, 1), jugador);
            Check(jugador.Inventory.CountHealItem(objetos) == 1,
                  "la casilla de manotazo saca uno");

            // Con la mochila vacia no puede romper ni inventar objetos.
            var pelado = JugadorConEquipo(1, 0);
            RouletteApplyResult r = RouletteRewardService.Aplicar(
                P("manotazo", RoulettePrizeKind.PerderObjeto, 1), pelado);
            Check(r.Aplicado && pelado.Inventory.HealItems.Count == 0,
                  "con la mochila vacia el manotazo no rompe nada");
        }

        // El caso peligroso: nada de duplicar Pokens ni tirar uno del equipo.
        private static void TestRuletaDaPokenSinDuplicar()
        {
            var especie = EspecieDePrueba("Premiado");

            var jugador = JugadorConEquipo(2, 0);
            var premio = new RoulettePrize
            {
                nombre = "poken", tipo = RoulettePrizeKind.Poken,
                peso = 1, poken = especie, nivel = 15, cumpleObjetivo = true
            };

            RouletteApplyResult r = RouletteRewardService.Aplicar(premio, jugador);
            Check(jugador.Team.Count == 3, $"el Poken se suma al equipo ({jugador.Team.Count})");
            Check(r.CumpleObjetivo, "la casilla marcada cumple el objetivo del piso");
            Check(jugador.Team[2] != jugador.Team[0] && jugador.Team[2] != jugador.Team[1],
                  "el Poken nuevo es una instancia propia, no una copia de otro");

            // Equipo lleno: no entra y no se pierde nada de lo que ya habia.
            var lleno = JugadorConEquipo(RouletteRewardService.MaxEquipo, 0);
            PokenModel primeroAntes = lleno.Team[0];
            RouletteApplyResult r2 = RouletteRewardService.Aplicar(premio, lleno);

            Check(lleno.Team.Count == RouletteRewardService.MaxEquipo,
                  "con el equipo lleno no se agrega nada");
            Check(lleno.Team[0] == primeroAntes, "y no se descarta ninguno de los que estaban");
            Check(!r2.Aplicado && !r2.CumpleObjetivo,
                  "con el equipo lleno la casilla no cuenta como cumplida");
        }

        private static void TestRuletaNoDebilitaAlEquipo()
        {
            var jugador = JugadorConEquipo(3, 0);
            foreach (PokenModel p in jugador.Team) p.SetCurrentHP(2);

            RouletteRewardService.Aplicar(P("descarga", RoulettePrizeKind.DanioAlEquipo, 1, 90), jugador);

            bool alguienCaido = false;
            foreach (PokenModel p in jugador.Team) if (p.IsFainted) alguienCaido = true;

            Check(!alguienCaido, "el golpe de la ruleta nunca debilita a nadie");
            Check(jugador.HasAlivePoken, "el jugador nunca queda sin equipo por una tirada");
        }
    }
}
