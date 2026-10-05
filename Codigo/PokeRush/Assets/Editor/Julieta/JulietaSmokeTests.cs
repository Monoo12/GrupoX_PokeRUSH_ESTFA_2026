using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Smoke tests de la pelea contra Julieta. Mismo esquema que los demas: se
// corren desde el menu, usan SeededRng y reportan a la Console con la etiqueta
// [JulietaSmokeTests]. Prueban las REGLAS (el guion y sus fases, la ruleta dada
// vuelta, el Pokemon controlado, la vida del PJ) sin abrir la escena; lo que se
// ve lo prueba PeleaJulietaPlayTest en Play. Tambien que el cierre del juego
// (historia.pdf) y la arena de Santiago (BossFinal) esten armados.
namespace PokeRush.EditorTools
{
    public static class JulietaSmokeTests
    {
        private static int _passed;
        private static int _failed;

        [MenuItem("PokeRush/Jefe final (Julieta)/Correr smoke tests de la pelea")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            TestLaRuletaDelCasinoNoCambio();
            TestRuletaAFavorDelJugador();
            TestRuletaAFavorDelRival();
            TestElMordiscoNoDebilitaAlRival();
            TestGuionAlEmpezarYUnaVez();
            TestGuionFiltraPorFase();
            TestGuionAlcanzaConUnaCondicion();
            TestGuionCuentaTurnosDesdeLaFase();
            TestAdelantarLaFase();
            TestVidaDelPJ();
            TestControladoEnElCampo();
            TestControladoFueraDeOrden();
            TestElegirMovimientoYVictima();
            TestGolpeAlPJEscalaConLaPotencia();
            TestPrediccionConOtroLector();
            TestAssetsDeLaPelea();
            TestGuionDelProyectoLlegaAlFinal();
            TestGuionDelProyectoRecorrePorTurnos();
            TestElCierreDelJuego();
            TestLaArena();

            if (_failed == 0)
                Debug.Log($"[JulietaSmokeTests] TODOS OK: {_passed} checks pasaron.");
            else
                Debug.LogError($"[JulietaSmokeTests] {_failed} FALLARON, {_passed} pasaron.");
        }

        private static void Check(bool condition, string name)
        {
            if (condition) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[JulietaSmokeTests] FALLO: {name}");
            }
        }

        // ---------- helpers ----------

        private static readonly IvSet Ivs = new IvSet
        { hp = 15, attack = 15, defense = 15, specialAttack = 15, specialDefense = 15, speed = 15 };

        private static PokenDefinition Especie(string nombre, PokenType tipo)
        {
            var s = ScriptableObject.CreateInstance<PokenDefinition>();
            s.pokenName = nombre;
            s.type = tipo;
            s.type2 = PokenType.None;
            s.baseHP = 80; s.baseAttack = 80; s.baseDefense = 80;
            s.baseSpecialAttack = 80; s.baseSpecialDefense = 80; s.baseSpeed = 80;
            s.baseXpYield = 60;
            return s;
        }

        private static MoveDefinition Movimiento(string nombre, int potencia, AttackType tipo = AttackType.Physical)
        {
            var m = ScriptableObject.CreateInstance<MoveDefinition>();
            m.moveName = nombre;
            m.type = PokenType.Normal;
            m.attackType = tipo;
            m.power = potencia;
            m.accuracy = 100;
            m.maxPP = 10;
            m.implementation = MoveImplementation.Full;
            return m;
        }

        private static PokenModel Poken(string nombre, params MoveDefinition[] movs)
        {
            PokenDefinition s = Especie(nombre, PokenType.Normal);
            s.moves = movs;
            return new PokenModel(s, 30, Ivs);
        }

        // Jugador con 'delJugador' (el primero activo) contra un rival con 'delRival'.
        private static BattleModel Pelea(PokenModel[] delJugador, PokenModel[] delRival)
        {
            var player = new PlayerModel("Tester");
            foreach (PokenModel p in delJugador) player.Team.Add(p);
            var enemy = new EnemyModel(delRival[0].Definition, delRival[0].Level);
            enemy.Team.Clear();
            foreach (PokenModel p in delRival) enemy.Team.Add(p);
            var m = new BattleModel(player, enemy);
            m.SwitchPlayerPoken(0);
            m.SwitchEnemyPoken(0);
            return m;
        }

        private static BattleModel PeleaSimple()
            => Pelea(new[] { Poken("Yo", Movimiento("Placaje", 40)) },
                     new[] { Poken("Ella", Movimiento("Placaje", 40)) });

        private static Prize Gajo(EfectoDeRuleta efecto)
            => new Prize { prizeName = efecto.ToString(), efectoDeCombate = efecto, customMessage = "del casino" };

        private static MomentoDeJulieta Momento(string nombre, FaseDeJulieta fase, params CondicionDeJulieta[] cuando)
            => new MomentoDeJulieta { nombre = nombre, fase = fase, cuando = cuando, lineas = new LineaDeJulieta[0] };

        // ---------- la ruleta ----------

        // Cuantas etapas mueve un gajo (bajo de 2 a 1 en el playtest del 4-oct).
        private static int Etapas => CasinoRuletaDeCombate.EtapasPorGajo;

        private static void TestLaRuletaDelCasinoNoCambio()
        {
            // La del jefe del casino tiene que seguir exactamente igual: todo al
            // jugador y el cartel del premio.
            var ruleta = new CasinoRuletaDeCombate(new[] { Gajo(EfectoDeRuleta.BajarAtaque) });
            Check(ruleta.QuienCobra == QuienCobraLaRuleta.SiempreElJugador, "Casino: por defecto todo al jugador");

            BattleModel m = PeleaSimple();
            ruleta.Cobrar(ruleta.Premios[0], m, new SeededRng(1), out bool alJugador, out string cartel);
            Check(alJugador && m.PlayerState.GetStage(Stat.Attack) == -Etapas && m.EnemyState.GetStage(Stat.Attack) == 0,
                  "Casino: la baja le cae al jugador, como siempre");
            Check(cartel == "del casino", "Casino: el cartel es el del premio");

            foreach (EfectoDeRuleta e in System.Enum.GetValues(typeof(EfectoDeRuleta)))
                Check(CasinoRuletaDeCombate.LeTocaAlJugador(e, QuienCobraLaRuleta.SiempreElJugador),
                      $"Casino: {e} le toca al jugador");
        }

        private static void TestRuletaAFavorDelJugador()
        {
            var modo = QuienCobraLaRuleta.AFavorDelJugador;
            var todos = new[]
            {
                EfectoDeRuleta.SubirAtaque, EfectoDeRuleta.BajarAtaque, EfectoDeRuleta.BajarDefensa,
                EfectoDeRuleta.CambiarTipo, EfectoDeRuleta.SacarVida, EfectoDeRuleta.DarPocion,
                EfectoDeRuleta.BloquearTurno
            };
            foreach (EfectoDeRuleta e in todos)
            {
                bool bueno = e == EfectoDeRuleta.SubirAtaque || e == EfectoDeRuleta.DarPocion;
                Check(CasinoRuletaDeCombate.LeTocaAlJugador(e, modo) == bueno,
                      $"A favor del jugador: {e} le cae a {(bueno ? "el jugador" : "el rival")}");
            }

            var ruleta = new CasinoRuletaDeCombate(new[] { Gajo(EfectoDeRuleta.SubirAtaque) }) { QuienCobra = modo };
            BattleModel m = PeleaSimple();
            ruleta.Cobrar(Gajo(EfectoDeRuleta.SubirAtaque), m, new SeededRng(1), out _, out _);
            ruleta.Cobrar(Gajo(EfectoDeRuleta.BajarDefensa), m, new SeededRng(1), out _, out _);
            ruleta.Cobrar(Gajo(EfectoDeRuleta.BloquearTurno), m, new SeededRng(1), out _, out _);
            Check(m.PlayerState.GetStage(Stat.Attack) == Etapas, "A favor del jugador: su ataque sube");
            Check(m.EnemyState.GetStage(Stat.Defense) == -Etapas && m.PlayerState.GetStage(Stat.Defense) == 0,
                  "A favor del jugador: la defensa floja es del rival");
            Check(m.EnemyState.Flinched && !m.PlayerState.Flinched, "A favor del jugador: el turno lo pierde el rival");

            m.PlayerState.Poken.TakeDamage(50);
            int antes = m.PlayerState.Poken.CurrentHP;
            ruleta.Cobrar(Gajo(EfectoDeRuleta.DarPocion), m, new SeededRng(1), out bool alJugador, out _);
            Check(alJugador && m.PlayerState.Poken.CurrentHP > antes, "A favor del jugador: la pocion es para el");

            var conCartel = new CasinoRuletaDeCombate(new[] { Gajo(EfectoDeRuleta.SacarVida) })
            {
                QuienCobra = modo,
                Cartel = (p, j) => j ? "jugador" : "rival"
            };
            conCartel.Cobrar(conCartel.Premios[0], PeleaSimple(), new SeededRng(1), out _, out string cartel);
            Check(cartel == "rival", "El cartel lo arma quien usa la ruleta, sabiendo a quien le toco");
        }

        private static void TestRuletaAFavorDelRival()
        {
            var modo = QuienCobraLaRuleta.AFavorDelRival;
            Check(!CasinoRuletaDeCombate.LeTocaAlJugador(EfectoDeRuleta.SubirAtaque, modo),
                  "A favor del rival: el ataque sube del rival");
            Check(CasinoRuletaDeCombate.LeTocaAlJugador(EfectoDeRuleta.SacarVida, modo),
                  "A favor del rival: el mordisco es para el jugador");

            var ruleta = new CasinoRuletaDeCombate(new[] { Gajo(EfectoDeRuleta.SubirAtaque) }) { QuienCobra = modo };
            BattleModel m = PeleaSimple();
            ruleta.Cobrar(Gajo(EfectoDeRuleta.SubirAtaque), m, new SeededRng(1), out _, out _);
            ruleta.Cobrar(Gajo(EfectoDeRuleta.BajarAtaque), m, new SeededRng(1), out _, out _);
            Check(m.EnemyState.GetStage(Stat.Attack) == Etapas && m.PlayerState.GetStage(Stat.Attack) == -Etapas,
                  "A favor del rival: lo bueno al rival y lo malo al jugador");

            // Darla vuelta a mitad es cambiar el campo: la misma ruleta.
            ruleta.QuienCobra = QuienCobraLaRuleta.AFavorDelJugador;
            ruleta.Cobrar(Gajo(EfectoDeRuleta.SubirAtaque), m, new SeededRng(1), out bool alJugador, out _);
            Check(alJugador && m.PlayerState.GetStage(Stat.Attack) == 0, "La misma ruleta se da vuelta cambiando QuienCobra");
        }

        private static void TestElMordiscoNoDebilitaAlRival()
        {
            BattleModel m = PeleaSimple();
            m.EnemyState.Poken.SetCurrentHP(1);
            CasinoRuletaDeCombate.Aplicar(EfectoDeRuleta.SacarVida, m, new SeededRng(1), alJugador: false);
            Check(m.EnemyState.Poken.CurrentHP == 1, "El mordisco de la casa tampoco debilita al rival");
        }

        // ---------- el guion ----------

        private static void TestGuionAlEmpezarYUnaVez()
        {
            var guion = new GuionEnCurso(new[] { Momento("inicio", FaseDeJulieta.Analisis, new CondicionDeJulieta(DisparadorDeJulieta.AlEmpezar)) });
            var e = new EstadoDePeleaJulieta();
            Check(guion.Siguiente(e)?.nombre == "inicio", "Guion: AlEmpezar suena en el arranque");
            Check(guion.Siguiente(e) == null, "Guion: cada momento suena una sola vez");
            guion.Reiniciar();
            e.MarcarEmpezado();
            Check(guion.Siguiente(e) == null, "Guion: AlEmpezar no suena despues del arranque");
        }

        private static void TestGuionFiltraPorFase()
        {
            var guion = new GuionEnCurso(new[]
            {
                Momento("de la 2", FaseDeJulieta.ControlDelSistema, new CondicionDeJulieta(DisparadorDeJulieta.AlRivalLeQuedan, 6)),
            });
            var e = new EstadoDePeleaJulieta { RivalLeQuedan = 3 };
            Check(guion.Siguiente(e) == null, "Guion: un momento de la fase 2 no suena en la 1");
            e.CambiarDeFase(FaseDeJulieta.ControlDelSistema);
            Check(guion.Siguiente(e)?.nombre == "de la 2", "Guion: ...y si en la 2");

            var cualquiera = Momento("siempre", FaseDeJulieta.Analisis, new CondicionDeJulieta(DisparadorDeJulieta.UltimoDelRival));
            cualquiera.cualquierFase = true;
            var g2 = new GuionEnCurso(new[] { cualquiera });
            Check(g2.Siguiente(new EstadoDePeleaJulieta { RivalEnElUltimo = true }) != null,
                  "Guion: 'cualquierFase' suena en cualquier fase");
        }

        private static void TestGuionAlcanzaConUnaCondicion()
        {
            MomentoDeJulieta m = Momento("vuelta", FaseDeJulieta.Analisis,
                new CondicionDeJulieta(DisparadorDeJulieta.TurnosEnLaFase, 5),
                new CondicionDeJulieta(DisparadorDeJulieta.RivalSalvado));
            var e = new EstadoDePeleaJulieta();
            Check(!GuionEnCurso.SeCumple(m, e), "Guion: sin ninguna condicion no suena");
            e.RivalSalvado = true;
            Check(GuionEnCurso.SeCumple(m, e), "Guion: alcanza con una de las condiciones");

            var vida = new EstadoDePeleaJulieta { VidaDelPJPorcentaje = 55 };
            Check(GuionEnCurso.Cumple(new CondicionDeJulieta(DisparadorDeJulieta.VidaDelPJBaja, 60), vida) &&
                  !GuionEnCurso.Cumple(new CondicionDeJulieta(DisparadorDeJulieta.VidaDelPJBaja, 30), vida),
                  "Guion: la vida del PJ se compara en porcentaje");
        }

        private static void TestGuionCuentaTurnosDesdeLaFase()
        {
            var e = new EstadoDePeleaJulieta();
            e.PonerTurno(4);
            Check(e.TurnosEnLaFase == 3, "Estado: la fase 1 cuenta desde el turno 1");
            e.CambiarDeFase(FaseDeJulieta.ControlDelSistema);
            Check(e.TurnosEnLaFase == 0, "Estado: al cambiar de fase la cuenta vuelve a cero");
            Check(GuionEnCurso.Cumple(new CondicionDeJulieta(DisparadorDeJulieta.AlEntrarALaFase), e),
                  "Estado: recien entrado, AlEntrarALaFase se cumple");
            e.PonerTurno(6);
            Check(e.TurnosEnLaFase == 2 && !GuionEnCurso.Cumple(new CondicionDeJulieta(DisparadorDeJulieta.AlEntrarALaFase), e),
                  "Estado: dos turnos despues ya no");

            e.ContarGiro();
            e.CambiarDeFase(FaseDeJulieta.ControlDelVinculo);
            Check(e.GirosEnLaFase == 0 && e.GirosTotales == 1, "Estado: los giros se cuentan por fase y en total");

            e.Reiniciar();
            Check(e.Fase == FaseDeJulieta.Analisis && !e.Empezo && e.GirosTotales == 0, "Estado: reiniciar vuelve todo a cero");
        }

        private static void TestAdelantarLaFase()
        {
            var e = new EstadoDePeleaJulieta();
            e.PonerTurno(2);
            e.AdelantarTurnosDeLaFase(50);
            Check(e.TurnosEnLaFase >= 50, "Adelantar: la fase se da por cumplida");
            e.CambiarDeFase(FaseDeJulieta.ControlDelSistema);
            Check(e.TurnosEnLaFase == 0, "Adelantar: se pierde al cambiar de fase");
        }

        // ---------- la vida del PJ y el controlado ----------

        private static void TestVidaDelPJ()
        {
            var v = new VidaDelPJ(100);
            Check(v.Actual == 100 && v.Porcentaje == 100 && !v.Caido, "Vida del PJ: arranca llena");
            Check(v.Recibir(30) == 30 && v.Porcentaje == 70, "Vida del PJ: recibe daño");
            Check(v.Recibir(500) == 70 && v.Actual == 0 && v.Caido, "Vida del PJ: no baja de cero y cae");
            v.Reiniciar(80);
            Check(v.Actual == 80 && v.Maxima == 80 && !v.Caido, "Vida del PJ: reiniciar la vuelve a llenar");
            v.Recibir(10);
            Check(v.Curar(50) == 10 && v.Actual == 80, "Vida del PJ: curar no pasa del maximo");
        }

        private static void TestControladoEnElCampo()
        {
            PokenModel poken = Poken("Lucario", Movimiento("Esfera Aural", 80, AttackType.Special), Movimiento("Danza", 0, AttackType.Status));
            var v = new VinculoControlado { ProbabilidadEnElCampo = 1f };
            Check(!v.Activo, "Controlado: sin Pokemon no hay control");
            v.Controlar(poken);
            Check(v.Activo && v.Poken == poken, "Controlado: queda el Pokemon del jugador");

            DecisionDelControlado d = v.DecidirEnElCampo(new SeededRng(3), 100);
            Check(d.Que == QueHaceElControlado.AtacaAlPJ && d.Dano > 0, "Controlado: con chance 1 ataca al PJ");
            Check(d.Movimiento == 0, "Controlado: usa un movimiento que pega");

            v.ProbabilidadEnElCampo = 0f;
            d = v.DecidirEnElCampo(new SeededRng(3), 100);
            Check(d.Que == QueHaceElControlado.AtacaAlRival && d.Movimiento == 0,
                  "Controlado: si no ataca al PJ, pelea contra el rival con un movimiento suyo");

            poken.SetCurrentHP(0);
            Check(!v.Activo && v.DecidirEnElCampo(new SeededRng(3), 100).Que == QueHaceElControlado.Nada,
                  "Controlado: debilitado no hace nada");

            v.Reiniciar();
            Check(v.Poken == null && v.AtaquesAlPJ == 0, "Controlado: reiniciar lo suelta");
        }

        private static void TestControladoFueraDeOrden()
        {
            var v = new VinculoControlado { ProbabilidadFueraDeOrden = 0f };
            v.Controlar(Poken("Gengar", Movimiento("Bola Sombra", 80, AttackType.Special)));
            Check(v.DecidirFueraDeOrden(new SeededRng(1), 100).Que == QueHaceElControlado.Nada,
                  "Fuera de orden: con chance 0 se queda quieto");
            v.ProbabilidadFueraDeOrden = 1f;
            Check(v.DecidirFueraDeOrden(new SeededRng(1), 100).Que == QueHaceElControlado.AtacaAlPJ,
                  "Fuera de orden: con chance 1 sale a atacar al PJ");

            var rng = new SeededRng(11);
            v.ProbabilidadFueraDeOrden = 0.35f;
            int ataques = 0;
            for (int i = 0; i < 2000; i++)
                if (v.DecidirFueraDeOrden(rng, 100).Que == QueHaceElControlado.AtacaAlPJ) ataques++;
            Check(ataques > 600 && ataques < 800, $"Fuera de orden: la chance se respeta (35% -> {ataques}/2000)");
        }

        private static void TestElegirMovimientoYVictima()
        {
            PokenModel sinPP = Poken("Cansado", Movimiento("Placaje", 40));
            sinPP.Moves[0].SetCurrentPP(0);
            Check(VinculoControlado.ElegirMovimiento(sinPP, new SeededRng(1)) == -1, "Sin PP en nada: -1 (Forcejeo)");

            // PokenModel.AsegurarUnAtaque le puede sumar un ataque (Placaje, si
            // ya corrio un GameManager en esta sesion): se lo deja sin PP.
            PokenModel soloEstado = Poken("Tranqui", Movimiento("Gruñido", 0, AttackType.Status));
            for (int i = 0; i < soloEstado.Moves.Count; i++)
                if (PredictionState.EsDeDano(soloEstado.Moves[i].Definition)) soloEstado.Moves[i].SetCurrentPP(0);
            int elegido = VinculoControlado.ElegirMovimiento(soloEstado, new SeededRng(1));
            Check(elegido >= 0 && PredictionState.EsDeEstado(soloEstado.Moves[elegido].Definition),
                  "Sin movimientos que peguen, usa uno de estado");

            PokenModel a = Poken("A", Movimiento("Placaje", 40));
            PokenModel b = Poken("B", Movimiento("Placaje", 40));
            BattleModel m = Pelea(new[] { a, b }, new[] { Poken("Ella", Movimiento("Placaje", 40)) });
            Check(VinculoControlado.ElegirVictima(m) == 0, "Victima: el que esta en el campo");
            a.SetCurrentHP(0);
            Check(VinculoControlado.ElegirVictima(m) == 1, "Victima: si el del campo cayo, el primero en pie");
            b.SetCurrentHP(0);
            Check(VinculoControlado.ElegirVictima(m) == -1, "Victima: nadie en pie, nadie");
        }

        private static void TestGolpeAlPJEscalaConLaPotencia()
        {
            int flojo = VinculoControlado.DanoAlPJ(Movimiento("x", 20), 100, 0.16f, new FixedRng());
            int fuerte = VinculoControlado.DanoAlPJ(Movimiento("y", 120), 100, 0.16f, new FixedRng());
            Check(fuerte > flojo, "Golpe al PJ: un movimiento mas fuerte le saca mas");
            Check(fuerte <= 32 && flojo >= 15, $"Golpe al PJ: en el rango esperado ({flojo}..{fuerte} de 100)");
            Check(VinculoControlado.DanoAlPJ(null, 1, 0f, new FixedRng()) >= 1, "Golpe al PJ: nunca 0");
        }

        // Rng que devuelve siempre el medio (Range(-2,3) -> 0).
        private class FixedRng : IBattleRng
        {
            public float Value() => 0.5f;
            public int Range(int minInclusive, int maxExclusive) => (minInclusive + maxExclusive - 1) / 2;
            public float Range(float minInclusive, float maxInclusive) => (minInclusive + maxInclusive) / 2f;
        }

        // ---------- la Prediccion con otro lector ----------

        private static void TestPrediccionConOtroLector()
        {
            var nestor = new PredictionState(false, 3);
            Check(nestor.Lector == "Nestor" && nestor.Snapshot().Titulo == "LECTURA DE NESTOR",
                  "Prediccion: por defecto sigue siendo de Nestor (la torre 1 no cambia)");
            var julieta = new PredictionState(false, 3) { Lector = "Julieta" };
            Check(julieta.Snapshot().Titulo == "LECTURA DE JULIETA" && julieta.MensajeRechazo().StartsWith("Julieta"),
                  "Prediccion: con otro lector, los textos lo nombran");
        }

        // ---------- los assets del proyecto ----------

        private static void TestAssetsDeLaPelea()
        {
            var julieta = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(PeleaJulietaAssets.RutaJulieta);
            Check(julieta != null, $"Existe {PeleaJulietaAssets.RutaJulieta} (PokeRush > Jefe final > Armar la escena de prueba)");
            if (julieta != null)
            {
                Check(julieta.pokenTeam != null && julieta.pokenTeam.Length == 6 && julieta.pokenTeam.All(t => t.species != null),
                      "Julieta: seis Pokemon con especie");
                var especies = julieta.pokenTeam.Select(t => t.species.pokenName).ToList();
                Check(especies.Contains("Mewtwo") && especies.Contains("Hoopa"),
                      "Julieta: sus ases son los dos del portal (Mewtwo y Hoopa)");
                Check(julieta.aiProfile != null, "Julieta: tiene perfil de IA");
                Check(julieta.defeatGrantsFlag == StoryFlag.Julieta_Vencida, "Julieta: vencerla da Julieta_Vencida");
            }

            var ruleta = PeleaJulietaAssets.RuletaDelCasino();
            Check(ruleta != null && ruleta.premios.Length == CasinoRuletaDeCombate.Gajos &&
                  ruleta.premios.All(p => p.efectoDeCombate != EfectoDeRuleta.Ninguno),
                  "La ruleta del jefe del casino esta y todos sus gajos hacen algo en combate");
        }

        private static GuionDeJulieta GuionDelProyecto()
            => AssetDatabase.LoadAssetAtPath<GuionDeJulieta>(PeleaJulietaAssets.RutaGuion);

        // Las cuatro fases estan conectadas, cada cambio de fase tiene la salida
        // de emergencia (RivalSalvado: el ultimo de Julieta no puede quedar
        // inmortal) y las acciones clave estan donde tienen que estar.
        private static void TestGuionDelProyectoLlegaAlFinal()
        {
            GuionDeJulieta g = GuionDelProyecto();
            Check(g != null, $"Existe {PeleaJulietaAssets.RutaGuion}");
            if (g == null) return;

            var nombres = new HashSet<string>();
            foreach (MomentoDeJulieta m in g.momentos)
            {
                Check(!string.IsNullOrEmpty(m.nombre) && nombres.Add(m.nombre), $"Guion: nombre unico ('{m.nombre}')");
                Check(m.cuando != null && m.cuando.Length > 0, $"Guion: '{m.nombre}' tiene cuando sonar");
                Check(m.lineas != null && m.lineas.All(l => !string.IsNullOrEmpty(l.texto) || l.accion != AccionDeJulieta.Ninguna),
                      $"Guion: '{m.nombre}' no tiene lineas vacias");
            }

            (FaseDeJulieta de, FaseDeJulieta a)[] pasos =
            {
                (FaseDeJulieta.Analisis, FaseDeJulieta.ControlDelSistema),
                (FaseDeJulieta.ControlDelSistema, FaseDeJulieta.ControlDelVinculo),
                (FaseDeJulieta.ControlDelVinculo, FaseDeJulieta.Ruptura),
            };
            foreach (var (de, a) in pasos)
            {
                MomentoDeJulieta paso = g.momentos.FirstOrDefault(m => m.fase == de && !m.cualquierFase &&
                    m.lineas.Any(l => l.accion == AccionDeJulieta.PasarAFase && l.parametro == (int)a));
                Check(paso != null, $"Guion: hay un momento que pasa de {de} a {a}");
                if (paso == null) continue;
                Check(paso.cuando.Any(c => c.cuando == DisparadorDeJulieta.RivalSalvado),
                      $"Guion: el paso de {de} a {a} se dispara tambien si el ultimo de Julieta se salvo");
                Check(paso.cuando.Any(c => c.cuando == DisparadorDeJulieta.TurnosEnLaFase),
                      $"Guion: el paso de {de} a {a} llega igual por turnos (sin depender de los KOs)");
            }

            bool Tiene(FaseDeJulieta fase, AccionDeJulieta accion, int parametro = int.MinValue)
                => g.momentos.Where(m => m.fase == fase).SelectMany(m => m.lineas)
                    .Any(l => l.accion == accion && (parametro == int.MinValue || l.parametro == parametro));

            Check(Tiene(FaseDeJulieta.Analisis, AccionDeJulieta.EncenderNestor), "Fase 1: se enciende Nestor");
            Check(Tiene(FaseDeJulieta.Analisis, AccionDeJulieta.EncenderRuleta, (int)QuienCobraLaRuleta.AFavorDelJugador),
                  "Fase 1: la ruleta arranca a favor del jugador");
            Check(Tiene(FaseDeJulieta.Analisis, AccionDeJulieta.ApagarNestor) &&
                  Tiene(FaseDeJulieta.Analisis, AccionDeJulieta.EncenderRuleta, (int)QuienCobraLaRuleta.AFavorDelRival) &&
                  Tiene(FaseDeJulieta.Analisis, AccionDeJulieta.EncenderLecturaDeJulieta) &&
                  Tiene(FaseDeJulieta.Analisis, AccionDeJulieta.SenalDeControl, 1),
                  "Paso a la fase 2: Nestor se apaga, la ruleta se da vuelta, Julieta lee y aparece la señal");
            Check(Tiene(FaseDeJulieta.ControlDelSistema, AccionDeJulieta.ControlarPokemon) &&
                  Tiene(FaseDeJulieta.ControlDelSistema, AccionDeJulieta.MostrarVidaDelPJ, 1),
                  "Paso a la fase 3: se controla un Pokemon y aparece la vida del PJ");
            Check(Tiene(FaseDeJulieta.ControlDelVinculo, AccionDeJulieta.LiberarPokemon) &&
                  Tiene(FaseDeJulieta.ControlDelVinculo, AccionDeJulieta.ApagarLecturaDeJulieta) &&
                  Tiene(FaseDeJulieta.ControlDelVinculo, AccionDeJulieta.EncenderNestor) &&
                  Tiene(FaseDeJulieta.ControlDelVinculo, AccionDeJulieta.EncenderRuleta, (int)QuienCobraLaRuleta.AFavorDelJugador) &&
                  Tiene(FaseDeJulieta.ControlDelVinculo, AccionDeJulieta.SenalDeControl, 0),
                  "Ruptura: todo vuelve al jugador");

            Check(g.alEncontrarla.Length > 0 && g.alPerder.Length > 0 && g.finalDelJuego.Length > 0,
                  "Guion: tiene encuentro, derrota y el arranque del final");
            Check(g.enElServidor.Length > 0 && g.alDestruirlo.Length > 0 && g.conJulietaEnElSuelo.Length > 0,
                  "Guion: el final esta partido en tramos (servidor, destruirlo, Julieta en el suelo)");
            Check(Final(g).Count() >= 60, "Guion: el final del PDF esta completo entre los tramos");
        }

        // Simula la pelea con el guion del proyecto: el rival pierde un Pokemon
        // cada dos turnos y solo se ejecutan los cambios de fase. Tiene que
        // llegar a la ruptura antes de que a Julieta se le acaben los Pokemon.
        private static void TestGuionDelProyectoRecorrePorTurnos()
        {
            GuionDeJulieta g = GuionDelProyecto();
            if (g == null) return;

            foreach (int turnosPorKO in new[] { 1, 2, 4, 50 })
            {
                var guion = new GuionEnCurso(g.momentos);
                var e = new EstadoDePeleaJulieta { RivalLeQuedan = 6 };
                var fases = new List<FaseDeJulieta>();

                for (int turno = 1; turno <= 40 && e.Fase != FaseDeJulieta.Ruptura; turno++)
                {
                    e.PonerTurno(turno);
                    int caidos = (turno - 1) / turnosPorKO;
                    e.RivalLeQuedan = Mathf.Max(1, 6 - caidos);
                    e.RivalEnElUltimo = e.RivalLeQuedan == 1;
                    // El ultimo no cae antes de la ruptura: se salva en 1 PS.
                    if (6 - caidos < 1) e.RivalSalvado = true;

                    for (MomentoDeJulieta m = guion.Siguiente(e); m != null; m = guion.Siguiente(e))
                        foreach (LineaDeJulieta l in m.lineas)
                            if (l.accion == AccionDeJulieta.PasarAFase)
                            {
                                e.CambiarDeFase((FaseDeJulieta)l.parametro);
                                fases.Add(e.Fase);
                            }
                    e.MarcarEmpezado();
                }

                Check(e.Fase == FaseDeJulieta.Ruptura,
                      $"Guion simulado (un KO cada {turnosPorKO} turnos): llega a la ruptura ({string.Join(" > ", fases)})");
                Check(fases.SequenceEqual(new[] { FaseDeJulieta.ControlDelSistema, FaseDeJulieta.ControlDelVinculo, FaseDeJulieta.Ruptura }),
                      $"Guion simulado (un KO cada {turnosPorKO} turnos): pasa por las fases en orden");
            }
        }

        // El cierre de la historia.pdf: la ruptura la abre Tixi por el
        // transmisor, el final destruye el servidor y el resto esta en el guion.
        private static void TestElCierreDelJuego()
        {
            GuionDeJulieta g = GuionDelProyecto();
            if (g == null) return;

            MomentoDeJulieta ruptura = g.momentos.FirstOrDefault(m => m.fase == FaseDeJulieta.ControlDelVinculo &&
                m.lineas.Any(l => l.accion == AccionDeJulieta.PasarAFase && l.parametro == (int)FaseDeJulieta.Ruptura));
            Check(ruptura != null && ruptura.lineas.Any(l => l.hablante == HablanteDeJulieta.Tixi),
                  "Cierre: en la ruptura habla Tixi (el transmisor)");
            Check(g.retratoTixi != null && !string.IsNullOrEmpty(g.nombreTixi), "Cierre: Tixi tiene nombre y retrato en el guion");
            Check(g.alReintentar != null && g.alReintentar.Length > 0, "Cierre: al reintentar no se repite la charla entera");
            Check(g.alEncontrarla.Any(l => l.texto.Contains("Peleando")), "Cierre: antes de pelear dice lo del PDF ('Peleando.')");

            Check(Final(g).Count(l => l.accion == AccionDeJulieta.RomperServidor) == 1 &&
                  g.alDestruirlo.Any(l => l.accion == AccionDeJulieta.RomperServidor),
                  "Cierre: el servidor se destruye una vez, en su tramo");
            Check(g.conJulietaEnElSuelo.Any(l => l.texto.Contains("Estoy sola en mi propia cabeza")),
                  "Cierre: el final es el del PDF");
            Check(Final(g).Count(l => l.sonido != null) >= 2, "Cierre: suenan el nucleo y el chip de Julieta");
            // Ningun tramo es un bloque eterno (playtest del 4-oct).
            foreach (var tramo in new[] { g.finalDelJuego, g.enElServidor, g.alDestruirlo, g.conJulietaEnElSuelo })
                Check(tramo.Length <= 50, $"Cierre: un tramo del final tiene {tramo.Length} lineas (max 50)");

            Check(g.epilogo != null && g.epilogo.Count(e => !string.IsNullOrWhiteSpace(e)) >= 4, "Cierre: hay epilogo");
            Check(g.enLaCueva != null && g.enLaCueva.lines != null && g.enLaCueva.lines.Length >= 30,
                  "Cierre: la charla final con Tixi esta en el guion");
            Check(g.ultimaConexion != null && g.ultimaConexion.Length > 0 &&
                  g.ultimaConexion[g.ultimaConexion.Length - 1].Contains("VENTRESCA AI"),
                  "Cierre: la ultima conexion termina en VENTRESCA AI");
            Check(g.fuente != null, "Cierre: el epilogo tiene la fuente del juego");

            // El mismo tope que los dialogos del mapa (HistoriaSmokeTests): mas
            // largo no entra en la caja.
            var todas = g.alEncontrarla.Concat(g.alReintentar).Concat(g.alPerder).Concat(Final(g))
                .Concat(g.yaVencida).Concat(g.momentos.SelectMany(m => m.lineas));
            foreach (LineaDeJulieta l in todas)
                if (l != null && l.texto != null && l.texto.Length > 125)
                    Check(false, $"Guion: linea demasiado larga ({l.texto.Length}): {l.texto.Substring(0, 40)}...");
        }

        // El final entero, en el orden en que se lee.
        private static IEnumerable<LineaDeJulieta> Final(GuionDeJulieta g) =>
            g.finalDelJuego.Concat(g.enElServidor).Concat(g.alDestruirlo).Concat(g.conJulietaEnElSuelo);

        // La arena de Santiago con la pelea adentro, leida como texto (sin abrirla).
        private static void TestLaArena()
        {
            string ruta = PeleaJulietaBuilder.EscenaDeLaArena;
            Check(System.IO.File.Exists(ruta), $"Arena: existe {ruta} (PokeRush > Jefe final > Mudar la pelea a la arena)");
            if (!System.IO.File.Exists(ruta)) return;

            string escena = System.IO.File.ReadAllText(ruta);
            bool Usa(string asset) => escena.Contains(AssetDatabase.AssetPathToGUID(asset));

            Check(Usa("Assets/Scripts/Julieta/PeleaJulieta.cs"), "Arena: tiene la pelea");
            Check(Usa("Assets/Scripts/Julieta/ServidorPrincipal.cs"), "Arena: tiene el servidor principal");
            Check(Usa("Assets/Scripts/Controllers/ApagonDelNivel.cs"), "Arena: se apagan las luces al destruir el servidor");
            Check(Usa(PeleaJulietaAssets.RutaGuion), "Arena: la pelea usa el guion del proyecto");
            foreach (string zona in new[] { "Final_Sede_Llegada", "Final_Sede_Intruso", "Final_Sede_Puerta" })
            {
                string path = PeleaJulietaAssets.Carpeta + "/Dialogos/" + zona + ".asset";
                var d = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(path);
                Check(d != null && d.grantsFlag != StoryFlag.None, $"Arena: {zona} existe y marca su flag (suena una vez)");
                Check(Usa(path), $"Arena: la zona {zona} esta en la escena");
            }
            Check(!escena.Contains("PeleaJulietaBancoDePrueba") &&
                  !Usa("Assets/Scripts/Testing/PeleaJulietaBancoDePrueba.cs"), "Arena: no lleva el banco de pruebas");
            Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == ruta), "Arena: esta en Build Settings");
            Check(RecorridoDeLaHistoria.Niveles.Last().EscenaDeEntrada ==
                  System.IO.Path.GetFileNameWithoutExtension(ruta), "Arena: el recorrido termina en ella");
        }
    }
}
