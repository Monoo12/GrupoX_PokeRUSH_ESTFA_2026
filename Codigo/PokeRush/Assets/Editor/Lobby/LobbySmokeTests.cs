using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Smoke tests de la Quarcho Cueva. Mismo esquema que los del casino: se corren
// desde el menu y reportan a la Console (y al Editor.log) con la etiqueta
// [SmokeLobby] y una linea de resumen al final.
//
// Prueban las reglas sin UI (recorrido, mejoras, tienda, tutor, Tacconi,
// Martin, Tixi), que los datos esten (MTs, descripciones, ruletas) y que la
// escena haya quedado armada como dice el builder. Lo que queda por ver a mano
// (o con 'Probar la cueva en Play') es que las pantallas se vean bien.
namespace PokeRush.EditorTools
{
    public static class LobbySmokeTests
    {
        private const string Pokens = "Assets/Resources/Definitions/Pokens/";

        private static int _passed;
        private static int _failed;

        public static int Fallos => _failed;

        [MenuItem("PokeRush/Quarcho Cueva/Correr smoke tests del lobby")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;
            bool guardado = CasinoDialogo.GuardadoAutomatico;
            CasinoDialogo.GuardadoAutomatico = false;

            try
            {
                TestRecorrido();
                TestMejorasSeAcumulanYSeGuardan();
                TestMejorasSeAplicanACadaUnoQueEntra();
                TestMejorasValenUnaSolaBatalla();
                TestPremioDeMejora();
                TestTiendaCobraYEntrega();
                TestTiendaNoFia();
                TestPreciosDelTutor();
                TestTutorEnseniaConLugarLibre();
                TestTutorReemplazaUnMovimiento();
                TestTutorNoEnseniaLoQueNoCorresponde();
                TestDatosDeMts();
                TestTacconiTresChances();
                TestTacconiDelCasinoSinTope();
                TestMartinCobraLasRuletas();
                TestRuletasDeMartin();
                TestTiendaDeTixi();
                TestTixiSePresentaUnaSolaVez();
                TestEscenaArmada();
                TestRecorridoConectado();
            }
            finally
            {
                CasinoDialogo.GuardadoAutomatico = guardado;
            }

            if (_failed == 0) Debug.Log($"[SmokeLobby] TERMINADO. OK={_passed} FALLOS=0");
            else Debug.LogError($"[SmokeLobby] TERMINADO. OK={_passed} FALLOS={_failed}");
        }

        private static void Check(bool condicion, string nombre)
        {
            if (condicion) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[SmokeLobby] FALLO: {nombre}");
            }
        }

        private static PokenDefinition Especie(string archivo)
        {
            var def = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{archivo}.asset");
            Check(def != null, $"existe la especie {archivo}");
            return def;
        }

        // ---------- recorrido ----------

        private static void TestRecorrido()
        {
            var story = new StoryModel();
            Check(RecorridoDeLaHistoria.SiguienteNivel(story)?.EscenaDeEntrada == "NestorGOD2",
                  "Recorrido: al empezar, el primer nivel es el Data Center de Nestor");
            Check(RecorridoDeLaHistoria.NivelesCompletados(story) == 0, "Recorrido: al empezar no hay niveles completos");

            story.ActivateFlag(StoryFlag.Torre1_NestorLiberado);
            Check(RecorridoDeLaHistoria.SiguienteNivel(story)?.EscenaDeEntrada == "Casino Nivel 1",
                  "Recorrido: con Nestor rescatado sigue el casino");

            story.ActivateFlag(StoryFlag.Casino_Completado);
            Check(RecorridoDeLaHistoria.SiguienteNivel(story)?.EscenaDeEntrada == "BossFinal",
                  "Recorrido: despues del casino sigue el jefe final");
            Check(RecorridoDeLaHistoria.NivelesCompletados(story) == 2, "Recorrido: dos niveles completos");

            var soloCasino = new StoryModel();
            soloCasino.ActivateFlag(StoryFlag.Casino_Completado);
            Check(RecorridoDeLaHistoria.SiguienteNivel(soloCasino)?.EscenaDeEntrada == "NestorGOD2",
                  "Recorrido: un flag suelto de un nivel posterior no saltea el que falta");

            Check(RecorridoDeLaHistoria.NivelDeLaEscena("Casino nivel 3")?.FlagCompletado == StoryFlag.Casino_Completado,
                  "Recorrido: el piso 3 es del casino");
            Check(RecorridoDeLaHistoria.NivelDeLaEscena("Casino piso FINAL") != null, "Recorrido: el piso final es del casino");
            Check(RecorridoDeLaHistoria.NivelDeLaEscena(RecorridoDeLaHistoria.EscenaDelLobby) == null,
                  "Recorrido: la cueva no es un nivel");
            Check(RecorridoDeLaHistoria.EsElLobby("Quarcho-Cueva"), "Recorrido: la escena del lobby es Quarcho-Cueva");
        }

        // ---------- mejoras ----------

        private static void TestMejorasSeAcumulanYSeGuardan()
        {
            var lista = new List<MejoraPendiente>();
            MejorasDeBatalla.Sumar(lista, MejoraDeBatalla.Ataque, 1);
            MejorasDeBatalla.Sumar(lista, MejoraDeBatalla.Ataque, 2);
            MejorasDeBatalla.Sumar(lista, MejoraDeBatalla.Velocidad, 1);
            MejorasDeBatalla.Sumar(lista, MejoraDeBatalla.Ninguna, 3);
            Check(lista.Count == 2, "Mejoras: la misma mejora se acumula en una sola entrada");
            Check(lista[0].Etapas == 3, "Mejoras: +1 y +2 de Ataque dan +3");

            MejorasDeBatalla.Sumar(lista, MejoraDeBatalla.Ataque, 10);
            Check(lista[0].Etapas == MejorasDeBatalla.EtapasMaximas, "Mejoras: nunca pasan de +6");

            string texto = MejorasDeBatalla.Serializar(lista);
            List<MejoraPendiente> vuelta = MejorasDeBatalla.Deserializar(texto);
            Check(vuelta.Count == 2 && vuelta[0].Mejora == MejoraDeBatalla.Ataque && vuelta[0].Etapas == 6 &&
                  vuelta[1].Mejora == MejoraDeBatalla.Velocidad && vuelta[1].Etapas == 1,
                  $"Mejoras: se guardan y se leen igual ('{texto}')");
            Check(MejorasDeBatalla.Deserializar(null).Count == 0 && MejorasDeBatalla.Deserializar("basura;Ataque:x;:1").Count == 0,
                  "Mejoras: un guardado viejo (null) o roto se lee como ninguna");

            Check(MejorasDeBatalla.Describir(new List<MejoraPendiente>
                  {
                      new MejoraPendiente(MejoraDeBatalla.Ataque, 1), new MejoraPendiente(MejoraDeBatalla.Velocidad, 2)
                  }) == "+1 Ataque y +2 Velocidad", "Mejoras: el cartel dice '+1 Ataque y +2 Velocidad'");
        }

        private static BattleModel Pelea(PlayerModel jugador)
        {
            PokenDefinition rival = Especie("0092_Gastly");
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            def.characterName = "Rival de prueba";
            def.pokenTeam = new[] { new EnemyTeamEntry { species = rival, level = 20 } };
            return new BattleModel(jugador, new EnemyModel(def));
        }

        private static void TestMejorasSeAplicanACadaUnoQueEntra()
        {
            PokenDefinition a = Especie("0025_Pikachu"), b = Especie("0133_Eevee");
            if (a == null || b == null) return;

            var jugador = new PlayerModel("Test");
            jugador.Team.Add(new PokenModel(a, 20));
            jugador.Team.Add(new PokenModel(b, 20));
            jugador.AgregarMejora(MejoraDeBatalla.Ataque, 1);
            jugador.AgregarMejora(MejoraDeBatalla.Todas, 1);

            BattleModel pelea = Pelea(jugador);
            pelea.MejorasDelJugador.AddRange(jugador.TomarMejoras());
            pelea.SwitchPlayerPoken(0);
            pelea.SwitchEnemyPoken(0);

            int[] s = pelea.PlayerState.Stages;
            Check(s[(int)Stat.Attack] == 2, "Mejoras: +1 Ataque y +1 a todo dan +2 de Ataque al que arranca");
            Check(s[(int)Stat.Defense] == 1 && s[(int)Stat.SpecialAttack] == 1 && s[(int)Stat.SpecialDefense] == 1 &&
                  s[(int)Stat.Speed] == 1, "Mejoras: 'todas' sube las cinco de pelea");
            Check(s[(int)Stat.Accuracy] == 0 && s[(int)Stat.Evasion] == 0, "Mejoras: 'todas' no toca precision ni evasion");
            Check(pelea.EnemyState.Stages.All(x => x == 0), "Mejoras: el rival no recibe nada");

            pelea.SwitchPlayerPoken(1);
            Check(pelea.PlayerState.Stages[(int)Stat.Attack] == 2, "Mejoras: el que entra por un cambio tambien las tiene");
        }

        private static void TestMejorasValenUnaSolaBatalla()
        {
            PokenDefinition a = Especie("0025_Pikachu");
            if (a == null) return;

            var jugador = new PlayerModel("Test");
            jugador.Team.Add(new PokenModel(a, 20));
            jugador.AgregarMejora(MejoraDeBatalla.Velocidad, 1);
            Check(jugador.TieneMejoras, "Mejoras: la ganada queda esperando");

            // Lo mismo que hace BattleController.BeginBattle.
            BattleModel primera = Pelea(jugador);
            primera.MejorasDelJugador.AddRange(jugador.TomarMejoras());
            primera.SwitchPlayerPoken(0);
            Check(!jugador.TieneMejoras, "Mejoras: la batalla se las lleva");
            Check(primera.PlayerState.Stages[(int)Stat.Speed] == 1, "Mejoras: la primera batalla las usa");

            BattleModel segunda = Pelea(jugador);
            segunda.MejorasDelJugador.AddRange(jugador.TomarMejoras());
            segunda.SwitchPlayerPoken(0);
            Check(segunda.PlayerState.Stages[(int)Stat.Speed] == 0, "Mejoras: la segunda batalla ya no las tiene");
        }

        private static void TestPremioDeMejora()
        {
            var jugador = new PlayerModel("Test");
            var premio = new Prize { prizeName = "+1 Defensa", mejora = MejoraDeBatalla.Defensa, etapasDeMejora = 1 };
            string cartel = PrizeGranter.Entregar(premio, jugador);
            Check(jugador.MejorasPendientes.Count == 1 && jugador.MejorasPendientes[0].Mejora == MejoraDeBatalla.Defensa,
                  "Premio de mejora: queda pendiente para la proxima batalla");
            Check(cartel.Contains("+1 Defensa"), $"Premio de mejora: el cartel la nombra ('{cartel}')");
            Check(jugador.Inventory.TotalItemCount == 0 && jugador.Money == 0, "Premio de mejora: no da objetos ni plata");
        }

        // ---------- tienda ----------

        private static HealItemDefinition Pocion()
        {
            var o = AssetDatabase.LoadAssetAtPath<HealItemDefinition>("Assets/Resources/Definitions/Items/Pocion.asset");
            Check(o != null, "existe la Pocion");
            return o;
        }

        private static void TestTiendaCobraYEntrega()
        {
            HealItemDefinition pocion = Pocion();
            if (pocion == null) return;

            var jugador = new PlayerModel("Test");
            jugador.AddMoney(1000);
            int precio = ReglasDeTienda.Precio(pocion);
            Check(precio == pocion.buyPrice && precio > 0, "Tienda: el precio es el buyPrice del objeto");
            Check(ReglasDeTienda.MaximoQueAlcanza(1000, pocion) == 1000 / precio, "Tienda: calcula cuantas alcanzan");

            Check(ReglasDeTienda.Comprar(jugador, pocion, 3), "Tienda: compra 3");
            Check(jugador.Money == 1000 - 3 * precio, "Tienda: cobra el total");
            Check(jugador.Inventory.CountHealItem(pocion) == 3, "Tienda: las 3 van a la mochila");
        }

        private static void TestTiendaNoFia()
        {
            HealItemDefinition pocion = Pocion();
            if (pocion == null) return;

            var jugador = new PlayerModel("Test");
            jugador.AddMoney(pocion.buyPrice - 1);
            Check(!ReglasDeTienda.Comprar(jugador, pocion, 1), "Tienda: sin plata no vende");
            Check(jugador.Money == pocion.buyPrice - 1 && jugador.Inventory.CountHealItem(pocion) == 0,
                  "Tienda: una compra rechazada no toca nada");
            Check(!ReglasDeTienda.Comprar(jugador, pocion, 0) && !ReglasDeTienda.Comprar(jugador, pocion, 100),
                  "Tienda: 0 o mas de 99 no se venden");
        }

        // ---------- tutor ----------

        private static MoveDefinition Movimiento(int poder, AttackType tipo)
        {
            var m = ScriptableObject.CreateInstance<MoveDefinition>();
            m.moveName = $"Prueba{poder}";
            m.power = poder;
            m.attackType = tipo;
            m.maxPP = 10;
            m.implementation = MoveImplementation.Full;
            return m;
        }

        private static void TestPreciosDelTutor()
        {
            var p = new PreciosDelTutor();
            Check(TutorDeMovimientos.Precio(Movimiento(0, AttackType.Status), p) == p.estado, "Tutor: los de estado salen 'estado'");
            Check(TutorDeMovimientos.Precio(Movimiento(40, AttackType.Physical), p) == p.debil, "Tutor: potencia 40 es 'debil'");
            Check(TutorDeMovimientos.Precio(Movimiento(75, AttackType.Special), p) == p.medio, "Tutor: potencia 75 es 'medio'");
            Check(TutorDeMovimientos.Precio(Movimiento(90, AttackType.Special), p) == p.fuerte, "Tutor: potencia 90 es 'fuerte'");
            Check(TutorDeMovimientos.Precio(Movimiento(120, AttackType.Physical), p) == p.muyFuerte, "Tutor: potencia 120 es 'muy fuerte'");
        }

        // Un Mew con exactamente 'movimientos' movimientos. Por nivel no llega a
        // cuatro (Transformacion y Metronomo no estan implementados), asi que se
        // completa con sus MT.
        private static PokenModel Mew(int movimientos)
        {
            PokenDefinition mew = Especie("0151_Mew");
            if (mew == null) return null;
            var poken = new PokenModel(mew, 30);
            while (poken.Moves.Count > movimientos) poken.Moves.RemoveAt(poken.Moves.Count - 1);
            if (mew.mtMoves != null)
                foreach (MoveDefinition m in mew.mtMoves)
                {
                    if (poken.Moves.Count >= movimientos) break;
                    poken.TryLearnMove(m);
                }
            Check(poken.Moves.Count == movimientos, $"Tutor: el Mew de prueba sabe {movimientos} movimientos");
            return poken;
        }

        private static void TestTutorEnseniaConLugarLibre()
        {
            PokenModel mew = Mew(3);
            if (mew == null) return;

            List<MoveDefinition> lista = TutorDeMovimientos.Ensenables(mew);
            Check(lista.Count > 0, "Tutor: Mew tiene MT para aprender");
            if (lista.Count == 0) return;

            MoveDefinition m = lista[0];
            int precio = TutorDeMovimientos.Precio(m, null);
            var jugador = new PlayerModel("Test");
            jugador.AddMoney(precio + 5);

            Check(TutorDeMovimientos.Ensenar(jugador, mew, m, -1, precio), "Tutor: con lugar libre lo suma");
            Check(mew.Moves.Count == 4 && mew.KnowsMove(m), "Tutor: queda aprendido");
            Check(jugador.Money == 5, "Tutor: cobra el precio");
            Check(!TutorDeMovimientos.Ensenables(mew).Contains(m), "Tutor: lo que ya sabe no se vuelve a ofrecer");
        }

        private static void TestTutorReemplazaUnMovimiento()
        {
            PokenModel mew = Mew(4);
            if (mew == null) return;
            List<MoveDefinition> lista = TutorDeMovimientos.Ensenables(mew);
            if (lista.Count == 0) { Check(false, "Tutor: Mew tiene MT para reemplazar"); return; }

            MoveDefinition nuevo = lista[lista.Count - 1];
            MoveDefinition viejo = mew.Moves[1].Definition;
            var jugador = new PlayerModel("Test");
            jugador.AddMoney(5000);
            int precio = TutorDeMovimientos.Precio(nuevo, null);

            Check(TutorDeMovimientos.MotivoParaNoEnsenar(jugador, mew, nuevo, -1, precio) != null,
                  "Tutor: con cuatro no deja sumar sin olvidar");
            Check(TutorDeMovimientos.Ensenar(jugador, mew, nuevo, 1, precio), "Tutor: reemplaza el segundo");
            Check(mew.Moves[1].Definition == nuevo && !mew.KnowsMove(viejo), "Tutor: olvida el viejo en el mismo lugar");
            Check(mew.Moves[1].CurrentPP == nuevo.maxPP, "Tutor: el nuevo llega con los PP llenos");
            Check(mew.Moves.Count == 4 && jugador.Money == 5000 - precio, "Tutor: siguen siendo cuatro y cobra una vez");
        }

        private static void TestTutorNoEnseniaLoQueNoCorresponde()
        {
            PokenModel mew = Mew(3);
            if (mew == null) return;
            List<MoveDefinition> lista = TutorDeMovimientos.Ensenables(mew);
            if (lista.Count == 0) return;

            var pobre = new PlayerModel("Test");
            MoveDefinition m = lista[0];
            Check(!TutorDeMovimientos.Ensenar(pobre, mew, m, -1, TutorDeMovimientos.Precio(m, null)),
                  "Tutor: sin plata no enseña");
            Check(mew.Moves.Count == 3, "Tutor: una enseñanza rechazada no toca los movimientos");

            var rico = new PlayerModel("Test");
            rico.AddMoney(99999);
            MoveDefinition sabido = mew.Moves[0].Definition;
            Check(!TutorDeMovimientos.Ensenar(rico, mew, sabido, -1, 100), "Tutor: no enseña lo que ya sabe");
            Check(rico.Money == 99999, "Tutor: y no cobra");
        }

        private static void TestDatosDeMts()
        {
            List<PokenDefinition> especies = AssetDatabase.FindAssets("t:PokenDefinition", new[] { "Assets/Resources/Definitions/Pokens" })
                .Select(g => AssetDatabase.LoadAssetAtPath<PokenDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(e => e != null && e.dexNumber > 0).ToList();
            int conMt = especies.Count(e => e.mtMoves != null && e.mtMoves.Length > 0);
            Check(conMt >= especies.Count * 9 / 10, $"Datos: casi todas las especies tienen MT ({conMt} de {especies.Count})");

            bool todasUsables = especies.All(e => e.mtMoves == null ||
                e.mtMoves.All(m => m != null && m.implementation != MoveImplementation.NotImplemented));
            Check(todasUsables, "Datos: ninguna MT es un movimiento sin implementar");

            List<MoveDefinition> movimientos = AssetDatabase.FindAssets("t:MoveDefinition", new[] { "Assets/Resources/Definitions/Moves" })
                .Select(g => AssetDatabase.LoadAssetAtPath<MoveDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null).ToList();
            int conTexto = movimientos.Count(m => !string.IsNullOrEmpty(m.descripcion));
            Check(conTexto >= movimientos.Count * 9 / 10, $"Datos: casi todos los movimientos tienen descripcion ({conTexto} de {movimientos.Count})");

            MoveDefinition rayoHielo = movimientos.FirstOrDefault(m => m.englishName == "ice-beam");
            Check(rayoHielo != null && rayoHielo.descripcion.Contains("congel"),
                  $"Datos: la descripcion esta en español ('{rayoHielo?.descripcion}')");
        }

        // ---------- Tacconi ----------

        private static CasinoTacconiNpc TacconiDeLaCueva(GameObject go, PokenDefinition premio)
        {
            go.AddComponent<BoxCollider2D>();
            var tacconi = go.AddComponent<CasinoTacconiNpc>();
            var so = new SerializedObject(tacconi);
            so.FindProperty("especiePremio").objectReferenceValue = premio;
            so.FindProperty("nivelPremio").intValue = 30;
            so.FindProperty("premioShiny").boolValue = false;
            so.FindProperty("flagGanado").enumValueIndex = (int)StoryFlag.Lobby_TacconiMewGanado;
            SerializedProperty chances = so.FindProperty("chances");
            StoryFlag[] flags = { StoryFlag.Lobby_TacconiChance1, StoryFlag.Lobby_TacconiChance2, StoryFlag.Lobby_TacconiChance3 };
            chances.arraySize = flags.Length;
            for (int i = 0; i < flags.Length; i++) chances.GetArrayElementAtIndex(i).enumValueIndex = (int)flags[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return tacconi;
        }

        private static void TestTacconiTresChances()
        {
            PokenDefinition mew = Especie("0151_Mew"), pikachu = Especie("0025_Pikachu");
            if (mew == null || pikachu == null) return;

            var go = new GameObject("TacconiDePrueba");
            try
            {
                CasinoTacconiNpc tacconi = TacconiDeLaCueva(go, mew);
                var story = new StoryModel();
                var jugador = new PlayerModel("Test");
                for (int i = 0; i < 5; i++) jugador.Team.Add(new PokenModel(pikachu, 10));

                Check(tacconi.ChancesTotales == 3 && !tacconi.SinChances(story), "Tacconi: arranca con tres chances");

                string empate = tacconi.AplicarResultado(jugador, story, jugador.Team[0], BlackjackOutcome.Empata);
                Check(tacconi.ChancesUsadas(story) == 0 && jugador.Team.Count == 5, "Tacconi: el empate no gasta chance ni Pokemon");

                string primera = tacconi.AplicarResultado(jugador, story, jugador.Team[0], BlackjackOutcome.Pierde);
                Check(jugador.Team.Count == 4 && tacconi.ChancesUsadas(story) == 1, "Tacconi: perder se lleva el Pokemon y una chance");
                Check(primera.Contains("2"), $"Tacconi: avisa que quedan dos ('{primera}')");

                tacconi.AplicarResultado(jugador, story, jugador.Team[0], BlackjackOutcome.Pierde);
                string ultima = tacconi.AplicarResultado(jugador, story, jugador.Team[0], BlackjackOutcome.Pierde);
                Check(tacconi.SinChances(story) && story.HasFlag(StoryFlag.Lobby_TacconiChance3),
                      "Tacconi: a la tercera mano perdida no quedan chances");
                Check(ultima.Contains("última"), $"Tacconi: avisa que era la ultima ('{ultima}')");
                Check(jugador.Team.Count == 2, "Tacconi: se quedo con los tres apostados");

                var otra = new StoryModel();
                var ganador = new PlayerModel("Test");
                ganador.Team.Add(new PokenModel(pikachu, 10));
                ganador.Team.Add(new PokenModel(pikachu, 10));
                tacconi.AplicarResultado(ganador, otra, ganador.Team[0], BlackjackOutcome.Gana);
                Check(ganador.Team.Exists(p => p.Definition == mew && p.Level == 30 && !p.IsShiny),
                      "Tacconi: ganar da el Mew Nv 30");
                Check(otra.HasFlag(StoryFlag.Lobby_TacconiMewGanado) && ganador.Team.Count == 3,
                      "Tacconi: y marca el Mew como ganado, sin llevarse nada");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void TestTacconiDelCasinoSinTope()
        {
            PokenDefinition cosmog = Especie("0789_Cosmog"), pikachu = Especie("0025_Pikachu");
            if (cosmog == null || pikachu == null) return;

            var go = new GameObject("TacconiDelCasino");
            try
            {
                go.AddComponent<BoxCollider2D>();
                var tacconi = go.AddComponent<CasinoTacconiNpc>();
                var so = new SerializedObject(tacconi);
                so.FindProperty("especiePremio").objectReferenceValue = cosmog;
                so.ApplyModifiedPropertiesWithoutUndo();

                var story = new StoryModel();
                var jugador = new PlayerModel("Test");
                for (int i = 0; i < 6; i++) jugador.Team.Add(new PokenModel(pikachu, 10));
                for (int i = 0; i < 4; i++) tacconi.AplicarResultado(jugador, story, jugador.Team[0], BlackjackOutcome.Pierde);

                Check(!tacconi.SinChances(story) && tacconi.ChancesTotales == 0,
                      "Tacconi del casino: sin chances configuradas no hay tope (como antes)");
                Check(!story.HasFlag(StoryFlag.Lobby_TacconiChance1), "Tacconi del casino: no gasta las chances de la cueva");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- Martin ----------

        private static void TestMartinCobraLasRuletas()
        {
            var ruleta = new QuarchoMartinNpc.Ruleta { nombre = "Prueba", tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>(), precio = 250 };
            var jugador = new PlayerModel("Test");
            jugador.AddMoney(300);
            Check(QuarchoMartinNpc.Cobrar(jugador, ruleta) && jugador.Money == 50, "Martin: cobra el tiro");
            Check(!QuarchoMartinNpc.Cobrar(jugador, ruleta) && jugador.Money == 50, "Martin: sin plata no hay tiro, y no cobra");
            Check(!QuarchoMartinNpc.Cobrar(jugador, new QuarchoMartinNpc.Ruleta { precio = 0 }),
                  "Martin: una ruleta sin tabla no se cobra");
        }

        private static void TestRuletasDeMartin()
        {
            CasinoPrizeTable pokemon = QuarchoCuevaAssets.RuletaDePokemon();
            CasinoPrizeTable objetos = QuarchoCuevaAssets.RuletaDeObjetos();
            CasinoPrizeTable mejoras = QuarchoCuevaAssets.RuletaDeMejoras();

            foreach (var (nombre, tabla) in new[] { ("Pokemon", pokemon), ("objetos", objetos), ("mejoras", mejoras) })
            {
                Check(tabla != null && tabla.premios.Length == CasinoRuletaDeCombate.Gajos,
                      $"Ruleta de {nombre} de Martin: ocho gajos (la rueda esta dibujada con ocho)");
                Check(tabla != null && tabla.premios.All(p => p != null && p.icon != null && p.probability > 0),
                      $"Ruleta de {nombre} de Martin: todos con icono y peso");
            }

            Check(pokemon.premios.All(p => p.poken != null), "Ruleta de Pokemon de Martin: siempre sale un Pokemon");
            Check(objetos.premios.All(p => p.item != null || p.money > 0), "Ruleta de objetos de Martin: objeto o plata");
            Check(mejoras.premios.All(p => p.mejora != MejoraDeBatalla.Ninguna && p.poken == null && p.item == null),
                  "Ruleta de mejoras de Martin: solo mejoras");
        }

        // ---------- Tixi ----------

        private static void TestTiendaDeTixi()
        {
            VendorDefinition tienda = QuarchoCuevaAssets.TiendaDeTixi(null);
            Check(tienda != null && tienda.healItemsForSale != null && tienda.healItemsForSale.Length >= 4,
                  "Tixi: la tienda vende al menos cuatro objetos");
            Check(tienda != null && tienda.healItemsForSale.All(o => o != null && o.buyPrice > 0 && !string.IsNullOrEmpty(o.itemName)),
                  "Tixi: todo lo que vende tiene nombre y precio");

            HealItemDefinition revivir = QuarchoCuevaAssets.Revivir();
            HealItemDefinition cura = QuarchoCuevaAssets.CuraTotal();
            Check(revivir.revives && cura.curesAllStatuses, "Tixi: Revivir revive y Cura Total cura todo");
        }

        private static void TestTixiSePresentaUnaSolaVez()
        {
            var go = new GameObject("TixiDePrueba");
            try
            {
                go.AddComponent<BoxCollider2D>();
                var tixi = go.AddComponent<QuarchoTixiNpc>();
                var story = new StoryModel();
                var jugador = new PlayerModel("Test");
                jugador.AddMoney(100);

                tixi.TerminarPresentacion(jugador, story);
                Check(story.HasFlag(StoryFlag.Lobby_TixiPresentado), "Tixi: presentarse abre la salida");
                Check(jugador.Money == 100 + tixi.RegaloDeBienvenida, "Tixi: da el regalo de bienvenida");

                tixi.TerminarPresentacion(jugador, story);
                Check(jugador.Money == 100 + tixi.RegaloDeBienvenida, "Tixi: el regalo es una sola vez");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------- la escena ----------

        private static void TestEscenaArmada()
        {
            Scene previa = SceneManager.GetSceneByPath(QuarchoCuevaBuilder.Escena);
            bool yaAbierta = previa.IsValid() && previa.isLoaded;
            Scene scene = yaAbierta ? previa : EditorSceneManager.OpenScene(QuarchoCuevaBuilder.Escena, OpenSceneMode.Additive);
            try
            {
                GameObject[] raices = scene.GetRootGameObjects();
                GameObject mapa = raices.FirstOrDefault(r => r.name == "MapaRaiz");
                Transform lobby = mapa != null ? mapa.transform.Find(QuarchoCuevaBuilder.Contenedor) : null;
                Check(lobby != null, "Escena: la cueva tiene el contenedor Lobby dentro de MapaRaiz");
                if (lobby == null) return;

                Check(lobby.Find("Tixi")?.GetComponent<QuarchoTixiNpc>() != null, "Escena: Tixi esta y es Tixi");
                Check(lobby.Find("Nestor")?.GetComponent<QuarchoNestorNpc>() != null, "Escena: Nestor esta y es el tutor");
                Check(lobby.Find("Martin")?.GetComponent<QuarchoMartinNpc>() != null, "Escena: Martin esta");
                Check(lobby.Find("Tacconi")?.GetComponent<CasinoTacconiNpc>() != null, "Escena: Tacconi esta");
                Check(lobby.Find("PC")?.GetComponent<PcInteractable>() != null, "Escena: hay PC");
                Check(lobby.Find("Salida")?.GetComponent<SalidaDelLobby>() != null, "Escena: hay salida");

                Check(!lobby.Find("Nestor").gameObject.activeSelf && !lobby.Find("Martin").gameObject.activeSelf &&
                      !lobby.Find("Tacconi").gameObject.activeSelf,
                      "Escena: Nestor, Martin y Tacconi arrancan escondidos");
                Check(lobby.Find("Tixi").gameObject.activeSelf, "Escena: Tixi esta desde el principio");

                Collider2D salida = lobby.Find("Salida").GetComponent<Collider2D>();
                Check(salida.isTrigger && salida.gameObject.layer == 0,
                      "Escena: la salida es un trigger en Default (en Obstacles frenaria al jugador)");

                var barreras = lobby.GetComponentsInChildren<BarreraPorFlag>(true);
                Check(barreras.Length == 2, "Escena: dos apariciones por flag (Nestor; Martin y Tacconi)");

                PlayerController jugador = raices.SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
                Check(jugador != null && Vector2.Distance(jugador.transform.position, QuarchoCuevaBuilder.Spawn) < 0.01f,
                      "Escena: el jugador arranca al lado de la puerta");
                Check(raices.Any(r => r.GetComponentInChildren<DialogueController>(true) != null), "Escena: hay caja de dialogo");
                Check(raices.Any(r => r.GetComponentInChildren<SceneBootstrapper>(true) != null), "Escena: hay bootstrap");
                Check(raices.Any(r => r.GetComponentInChildren<GameManager>(true) != null), "Escena: hay GameManager de respaldo");

                var tixi = lobby.Find("Tixi").GetComponent<QuarchoTixiNpc>();
                Check(tixi.Tienda != null, "Escena: Tixi tiene la tienda cargada");
                var martin = lobby.Find("Martin").GetComponent<QuarchoMartinNpc>();
                Check(martin.Ruletas.Length == 3 && martin.Ruletas.All(r => r.tabla != null && r.precio > 0),
                      "Escena: Martin tiene sus tres ruletas con precio");
            }
            finally
            {
                if (!yaAbierta) EditorSceneManager.CloseScene(scene, true);
            }

            Check(EditorBuildSettings.scenes.Any(s => s.path == QuarchoCuevaBuilder.Escena && s.enabled),
                  "Escena: la cueva esta en Build Settings");
        }

        private static void TestRecorridoConectado()
        {
            string creacion = System.IO.File.ReadAllText("Assets/Scenes/CreacionPersonaje.unity");
            string saves = System.IO.File.ReadAllText("Assets/Scenes/MenuSaves.unity");
            Check(creacion.Contains($"escenaDestino: {RecorridoDeLaHistoria.EscenaDelLobby}"),
                  "Recorrido: la creacion de personaje lleva a la cueva");
            Check(saves.Contains($"sceneJuego: {RecorridoDeLaHistoria.EscenaDelLobby}"),
                  "Recorrido: 'Continuar' lleva a la cueva");
        }
    }
}
