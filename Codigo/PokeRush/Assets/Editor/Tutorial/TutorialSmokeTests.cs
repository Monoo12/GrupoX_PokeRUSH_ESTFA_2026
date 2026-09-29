using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Smoke tests del tutorial de la habitacion. Mismo esquema que los del lobby y
// el casino: se corren desde el menu y reportan al Editor.log con la etiqueta
// [SmokeTutorial] y una linea de resumen al final.
//
// Prueban las reglas sin escena (que falta, la pelea al final, cuando abre la
// puerta), los datos (flags, textos, rival) y que el esqueleto haya quedado
// como dice el builder. El que mas importa para el equipo es el de las
// referencias: nada adentro de 'Tutorial' puede apuntar a un objeto de afuera,
// porque al copiarlo y pegarlo en otra escena esa referencia se pierde.
namespace PokeRush.EditorTools
{
    public static class TutorialSmokeTests
    {
        private static int _passed;
        private static int _failed;

        public static int Fallos => _failed;

        [MenuItem("PokeRush/Tutorial/Correr smoke tests del tutorial")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            TestFaltantesYPuerta();
            TestPeleaAlFinal();
            TestOpcionalesNoCuentan();
            TestPuertaConFlagsDelGuardado();
            TestEnumerar();
            TestCurarEquipo();
            TestFlagsAlFinal();
            TestAssets();
            TestRecorrido();
            TestEsqueleto();
            TestHabitacion();

            if (_failed == 0) Debug.Log($"[SmokeTutorial] TERMINADO. OK={_passed} FALLOS=0");
            else Debug.LogError($"[SmokeTutorial] TERMINADO. OK={_passed} FALLOS={_failed}");
        }

        private static void Check(bool condicion, string nombre)
        {
            if (condicion) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[SmokeTutorial] FALLO: {nombre}");
            }
        }

        // ---------- reglas ----------

        private class ParteFalsa : ReglasDelTutorial.IParte
        {
            public string Nombre { get; set; }
            public StoryFlag Flag { get; set; }
            public bool Obligatoria { get; set; } = true;
            public bool EsperaALasDemas { get; set; }
        }

        private static List<ParteFalsa> PartesDePrueba() => new List<ParteFalsa>
        {
            new ParteFalsa { Nombre = "la mochila", Flag = StoryFlag.Tutorial_Mochila },
            new ParteFalsa { Nombre = "el PC", Flag = StoryFlag.Tutorial_Pc },
            new ParteFalsa { Nombre = "la pelea", Flag = StoryFlag.Tutorial_Pelea, EsperaALasDemas = true },
        };

        private static void TestFaltantesYPuerta()
        {
            var partes = PartesDePrueba();
            var story = new StoryModel();

            Check(ReglasDelTutorial.Faltantes(partes, story).Count == 3, "Reglas: al principio falta todo");
            Check(!ReglasDelTutorial.Completo(partes, story), "Reglas: sin hacer nada no esta completo");
            Check(!ReglasDelTutorial.PuedeSalir(partes, story), "Reglas: la puerta no abre al principio");

            story.ActivateFlag(StoryFlag.Tutorial_Mochila);
            Check(ReglasDelTutorial.Hecha(partes[0], story) && !ReglasDelTutorial.Hecha(partes[1], story),
                  "Reglas: una parte queda hecha con su flag, las otras no");
            Check(ReglasDelTutorial.Faltantes(partes, story).Select(p => p.Nombre).SequenceEqual(new[] { "el PC", "la pelea" }),
                  "Reglas: faltan las otras, en orden");

            story.ActivateFlag(StoryFlag.Tutorial_Pc);
            story.ActivateFlag(StoryFlag.Tutorial_Pelea);
            Check(ReglasDelTutorial.Completo(partes, story), "Reglas: con todas hechas esta completo");
            Check(ReglasDelTutorial.PuedeSalir(partes, story), "Reglas: con todas hechas la puerta abre");
        }

        private static void TestPeleaAlFinal()
        {
            var partes = PartesDePrueba();
            var pelea = partes[2];
            var story = new StoryModel();

            Check(!ReglasDelTutorial.Disponible(pelea, partes, story), "Pelea: al principio no se puede");
            Check(ReglasDelTutorial.FaltanAntesDe(pelea, partes, story).Count == 2, "Pelea: le faltan las otras dos");
            Check(ReglasDelTutorial.FaltanAntesDe(pelea, partes, story).All(p => !p.EsperaALasDemas),
                  "Pelea: no se espera a si misma");

            story.ActivateFlag(StoryFlag.Tutorial_Mochila);
            Check(!ReglasDelTutorial.Disponible(pelea, partes, story), "Pelea: con una sola hecha todavia no");

            story.ActivateFlag(StoryFlag.Tutorial_Pc);
            Check(ReglasDelTutorial.Disponible(pelea, partes, story), "Pelea: con las otras hechas se habilita");
            Check(ReglasDelTutorial.Disponible(partes[0], partes, story), "Pelea: las que no esperan estan siempre disponibles");
            Check(!ReglasDelTutorial.PuedeSalir(partes, story), "Pelea: sin pelear, la puerta sigue cerrada");
        }

        private static void TestOpcionalesNoCuentan()
        {
            var partes = PartesDePrueba();
            partes.Add(new ParteFalsa { Nombre = "el poster", Flag = StoryFlag.Tutorial_Opciones, Obligatoria = false });
            partes.Add(new ParteFalsa { Nombre = "una sin flag", Flag = StoryFlag.None });
            var story = new StoryModel();

            Check(ReglasDelTutorial.Faltantes(partes, story).Count == 3,
                  "Opcionales: ni la opcional ni la que no tiene flag cuentan como faltantes");

            story.ActivateFlag(StoryFlag.Tutorial_Mochila);
            story.ActivateFlag(StoryFlag.Tutorial_Pc);
            Check(ReglasDelTutorial.Disponible(partes[2], partes, story),
                  "Opcionales: la pelea no espera a una parte opcional");

            story.ActivateFlag(StoryFlag.Tutorial_Pelea);
            Check(ReglasDelTutorial.Completo(partes, story), "Opcionales: se completa sin hacer la opcional");
        }

        private static void TestPuertaConFlagsDelGuardado()
        {
            var partes = PartesDePrueba();

            var omitido = new StoryModel();
            omitido.ActivateFlag(StoryFlag.Tutorial_Omitido);
            Check(ReglasDelTutorial.PuedeSalir(partes, omitido), "Puerta: omitido, abre aunque falte todo");

            var completado = new StoryModel();
            completado.ActivateFlag(StoryFlag.Tutorial_Completado);
            Check(ReglasDelTutorial.PuedeSalir(partes, completado), "Puerta: con Tutorial_Completado abre");

            Check(!ReglasDelTutorial.PuedeSalir(partes, null), "Puerta: sin partida no abre");
        }

        private static void TestEnumerar()
        {
            Check(ReglasDelTutorial.Enumerar(new List<string>()) == "", "Enumerar: vacio");
            Check(ReglasDelTutorial.Enumerar(new List<string> { "la mochila" }) == "la mochila", "Enumerar: uno");
            Check(ReglasDelTutorial.Enumerar(new List<string> { "la mochila", "el PC" }) == "la mochila y el PC",
                  "Enumerar: dos con 'y'");
            Check(ReglasDelTutorial.Enumerar(new List<string> { "la mochila", "el PC", "la pelea" }) ==
                  "la mochila, el PC y la pelea", "Enumerar: tres con coma y 'y'");
        }

        private static void TestCurarEquipo()
        {
            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>("Assets/Resources/Definitions/Pokens/0448_Lucario.asset");
            Check(especie != null, "Curar: existe Lucario para probar");
            if (especie == null) return;

            var jugador = new PlayerModel("PRUEBA");
            var poken = new PokenModel(especie, 20);
            jugador.Team.Add(poken);
            poken.SetCurrentHP(1);
            poken.SetStatus(StatusEffect.Poisoned);
            foreach (MoveModel m in poken.Moves) m.SetCurrentPP(0);

            ReglasDelTutorial.CurarEquipo(jugador);
            Check(poken.CurrentHP == poken.MaxHP, "Curar: PS al maximo");
            Check(poken.CurrentStatus == StatusEffect.None, "Curar: sin estado");
            Check(poken.Moves.All(m => m.CurrentPP == m.Definition.maxPP), "Curar: PP llenos");

            poken.SetCurrentHP(0);
            ReglasDelTutorial.CurarEquipo(jugador);
            Check(!poken.IsFainted, "Curar: levanta a un debilitado (la pelea arranca siempre)");
        }

        // ---------- datos ----------

        private static readonly StoryFlag[] FlagsDelTutorial =
        {
            StoryFlag.Tutorial_Despertar, StoryFlag.Tutorial_Mochila, StoryFlag.Tutorial_Equipo, StoryFlag.Tutorial_Pc,
            StoryFlag.Tutorial_Guardar, StoryFlag.Tutorial_Opciones, StoryFlag.Tutorial_Pelea,
            StoryFlag.Tutorial_Completado, StoryFlag.Tutorial_Omitido
        };

        private static void TestFlagsAlFinal()
        {
            // Las escenas guardan los flags como numero: los nuevos van al final
            // o le cambian el valor a todos los de abajo.
            Check(FlagsDelTutorial.All(f => (int)f > (int)StoryFlag.Lobby_TacconiMewGanado),
                  "Flags: los del tutorial van despues de los del lobby");
            Check(FlagsDelTutorial.Distinct().Count() == FlagsDelTutorial.Length, "Flags: son todos distintos");
        }

        private static void TestAssets()
        {
            foreach (string archivo in TutorialAssets.ArchivosDeDialogo)
            {
                var d = AssetDatabase.LoadAssetAtPath<DialogueDefinition>($"{TutorialAssets.Dialogos}/{archivo}.asset");
                Check(d != null && d.lines != null && d.lines.Length > 0 && d.lines.All(l => !string.IsNullOrWhiteSpace(l)),
                      $"Assets: {archivo} existe y tiene lineas");
            }

            var rival = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(TutorialAssets.RutaDelRival);
            Check(rival != null, "Assets: existe el rival del tutorial");
            if (rival != null)
            {
                Check(rival.pokenTeam != null && rival.pokenTeam.Length > 0 &&
                      rival.pokenTeam.All(e => e != null && e.species != null && e.level > 0),
                      "Assets: el rival tiene equipo valido");
                Check(rival.aiProfile != null, "Assets: el rival tiene perfil de IA");
                Check(rival.battleFrontSprite != null, "Assets: el rival tiene retrato para la intro del combate");
                Check(rival.defeatGrantsFlag == StoryFlag.None,
                      "Assets: el rival no da flag (la parte activa Tutorial_Pelea sola, no hay EventController)");
            }

            Check(AssetDatabase.LoadAssetAtPath<HealItemDefinition>(TutorialAssets.RutaPocion) != null,
                  "Assets: existe la Pocion que da la mochila");
        }

        private static void TestRecorrido()
        {
            string esqueleto = System.IO.Path.GetFileNameWithoutExtension(TutorialBuilder.EscenaEsqueleto);
            Check(esqueleto != RecorridoDeLaHistoria.EscenaDelTutorial,
                  "Recorrido: el esqueleto no se llama como la habitacion de verdad (no entra al recorrido por error)");
            Check(!EditorBuildSettings.scenes.Any(s => s.path == TutorialBuilder.EscenaEsqueleto),
                  "Recorrido: el esqueleto no esta en Build Settings");

            bool hayHabitacion = EditorBuildSettings.scenes.Any(s =>
                s.enabled && System.IO.Path.GetFileNameWithoutExtension(s.path) == RecorridoDeLaHistoria.EscenaDelTutorial);
            string esperado = hayHabitacion ? RecorridoDeLaHistoria.EscenaDelTutorial : RecorridoDeLaHistoria.EscenaDelLobby;
            Check(RecorridoDeLaHistoria.EscenaDespuesDeCrearPersonaje == esperado,
                  $"Recorrido: despues de crear el personaje va a {esperado}");
        }

        // ---------- el esqueleto ----------

        private static void TestEsqueleto()
        {
            if (!System.IO.File.Exists(TutorialBuilder.EscenaEsqueleto))
            {
                Check(false, $"Esqueleto: existe {TutorialBuilder.EscenaEsqueleto} (correr 'Armar el esqueleto')");
                return;
            }

            Scene previa = SceneManager.GetSceneByPath(TutorialBuilder.EscenaEsqueleto);
            bool yaAbierta = previa.IsValid() && previa.isLoaded;
            Scene scene = yaAbierta ? previa : EditorSceneManager.OpenScene(TutorialBuilder.EscenaEsqueleto, OpenSceneMode.Additive);

            try
            {
                RevisarTutorial(scene);
                RevisarRig(scene);
            }
            finally
            {
                if (!yaAbierta) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void RevisarTutorial(Scene scene)
        {
            var tutorial = TutorialBuilder.Buscar<TutorialDeLaHabitacion>(scene);
            Check(tutorial != null, "Esqueleto: tiene el TutorialDeLaHabitacion");
            if (tutorial == null) return;

            GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
            Check(tutorial.name == TutorialBuilder.Raiz, "Esqueleto: el objeto a copiar se llama Tutorial");
            Check(mapaRaiz != null && tutorial.transform.IsChildOf(mapaRaiz.transform),
                  "Esqueleto: el Tutorial vive adentro de MapaRaiz (se esconde durante la pelea)");
            Check(tutorial.PuntoDeDespertar != null && tutorial.PuntoDeDespertar.IsChildOf(tutorial.transform),
                  "Esqueleto: 'Despertar' esta adentro del Tutorial (se copia con el)");
            Check(tutorial.Despertar != null && tutorial.Cierre != null, "Esqueleto: tiene los textos del despertar y del cierre");

            List<string> afuera = ReferenciasAfuera(tutorial.transform);
            foreach (string r in afuera) Debug.LogError($"[SmokeTutorial] Referencia que se pierde al pegar: {r}");
            Check(afuera.Count == 0, "Esqueleto: nada adentro del Tutorial apunta a un objeto de afuera (se puede copiar y pegar)");

            TutorialParte[] partes = tutorial.GetComponentsInChildren<TutorialParte>(true);
            Check(partes.Length == 6, $"Esqueleto: seis partes (hay {partes.Length})");
            Check(partes.All(p => p.Flag != StoryFlag.None) && partes.Select(p => p.Flag).Distinct().Count() == partes.Length,
                  "Esqueleto: cada parte tiene su propio flag");

            var tipos = new[]
            {
                TutorialParte.Tipo.Mochila, TutorialParte.Tipo.Equipo, TutorialParte.Tipo.Pc,
                TutorialParte.Tipo.Guardar, TutorialParte.Tipo.Opciones, TutorialParte.Tipo.Pelea
            };
            foreach (TutorialParte.Tipo tipo in tipos)
                Check(partes.Count(p => p.QueHace == tipo) == 1, $"Esqueleto: hay una parte de {tipo}");

            TutorialParte pelea = partes.FirstOrDefault(p => p.QueHace == TutorialParte.Tipo.Pelea);
            Check(pelea != null && pelea.EsperaALasDemas && pelea.Rival != null, "Esqueleto: la pelea espera a las demas y tiene rival");
            Check(partes.Where(p => p != pelea).All(p => !p.EsperaALasDemas), "Esqueleto: solo la pelea espera a las demas");
            Check(partes.All(p => p.Obligatoria), "Esqueleto: todas son obligatorias");
            Check(partes.All(p => p.Explicacion != null && p.Explicacion.lines != null && p.Explicacion.lines.Length > 0),
                  "Esqueleto: todas explican algo");

            Check(partes.All(p => p.gameObject.layer == 3 && p.GetComponent<Collider2D>() != null && !p.GetComponent<Collider2D>().isTrigger),
                  "Esqueleto: las partes estan en Obstacles y son solidas (se aprietan con X de al lado)");

            bool brillos = partes.All(p =>
            {
                var b = p.GetComponent<BrilloInteractuable>();
                if (b == null) return false;
                var so = new SerializedObject(b);
                return so.FindProperty("flagQueLoApaga").enumValueIndex == (int)StoryFlag.None &&
                       so.FindProperty("spriteHalo").objectReferenceValue != null &&
                       so.FindProperty("spriteEstrella").objectReferenceValue != null;
            });
            Check(brillos, "Esqueleto: todas brillan, y el brillo toma el flag de la parte");

            TutorialParte mochila = partes.FirstOrDefault(p => p.QueHace == TutorialParte.Tipo.Mochila);
            if (mochila != null)
            {
                SerializedProperty entrega = new SerializedObject(mochila).FindProperty("entrega");
                bool daPociones = entrega.arraySize == 1 &&
                                  entrega.GetArrayElementAtIndex(0).FindPropertyRelative("objeto").objectReferenceValue ==
                                  AssetDatabase.LoadAssetAtPath<HealItemDefinition>(TutorialAssets.RutaPocion) &&
                                  entrega.GetArrayElementAtIndex(0).FindPropertyRelative("cantidad").intValue == 2;
                Check(daPociones, "Esqueleto: la mochila da dos Pociones");
            }

            if (pelea != null)
            {
                var sr = pelea.GetComponentInChildren<SpriteRenderer>(true);
                Check(sr != null && sr.sprite != null, "Esqueleto: la compañera tiene sprite");
            }

            var puerta = tutorial.GetComponentInChildren<PuertaDelTutorial>(true);
            Check(puerta != null, "Esqueleto: tiene puerta");
            if (puerta != null)
            {
                var zona = puerta.GetComponent<Collider2D>();
                Check(zona != null && zona.isTrigger && puerta.gameObject.layer == 0,
                      "Esqueleto: la puerta es una zona para pisar (trigger en Default)");
                Transform tope = puerta.transform.Find("Tope");
                Check(tope != null && tope.gameObject.layer == 3 && tope.GetComponent<Collider2D>() != null &&
                      !tope.GetComponent<Collider2D>().isTrigger, "Esqueleto: detras de la puerta hay un tope solido");
                Check(puerta.EscenaSiguiente == RecorridoDeLaHistoria.EscenaDelLobby, "Esqueleto: la puerta lleva a la cueva");
                Check(EditorBuildSettings.scenes.Any(s => s.enabled &&
                          System.IO.Path.GetFileNameWithoutExtension(s.path) == puerta.EscenaSiguiente),
                      "Esqueleto: la escena de la puerta esta en Build Settings");
            }
        }

        private static void RevisarRig(Scene scene, string prefijo = "")
        {
            GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
            var tutorial = TutorialBuilder.Buscar<TutorialDeLaHabitacion>(scene);
            var player = TutorialBuilder.Buscar<PlayerController>(scene);
            Check(player != null && mapaRaiz != null && player.transform.IsChildOf(mapaRaiz.transform),
                  $"{prefijo}Rig: el jugador vive adentro de MapaRaiz");
            if (player != null)
            {
                int mascara = new SerializedObject(player).FindProperty("interactableMask").intValue;
                Check((mascara & (1 << 3)) != 0, $"{prefijo}Rig: el jugador interactua con Obstacles");
                if (tutorial != null && tutorial.PuntoDeDespertar != null)
                    Check(Vector2.Distance(player.transform.position,
                              TutorialDeLaHabitacionCentro(tutorial.PuntoDeDespertar.position)) < 0.01f,
                          $"{prefijo}Rig: el jugador arranca en el centro de la celda de 'Despertar'");
            }

            Camera cam = mapaRaiz != null ? mapaRaiz.GetComponentInChildren<Camera>(true) : null;
            var follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
            Check(follow != null && player != null &&
                  new SerializedObject(follow).FindProperty("target").objectReferenceValue == player.transform,
                  $"{prefijo}Rig: la camara del mapa sigue al jugador");

            GameObject sistema = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Sistema");
            var bridge = sistema != null ? sistema.GetComponent<MapBattleBridge>() : null;
            var combate = TutorialBuilder.Buscar<BattleController>(scene);
            Check(combate != null && !combate.gameObject.activeSelf, $"{prefijo}Rig: el combate esta y arranca apagado");
            if (bridge != null)
            {
                var so = new SerializedObject(bridge);
                Check(so.FindProperty("mapaRaiz").objectReferenceValue == mapaRaiz &&
                      combate != null && so.FindProperty("combateRaiz").objectReferenceValue == combate.gameObject &&
                      so.FindProperty("battleController").objectReferenceValue == combate &&
                      so.FindProperty("playerController").objectReferenceValue == player,
                      $"{prefijo}Rig: MapBattleBridge conectado al mapa, al combate y al jugador");
            }
            else Check(false, $"{prefijo}Rig: hay MapBattleBridge en 'Sistema'");

            var boot = sistema != null ? sistema.GetComponent<SceneBootstrapper>() : null;
            Check(boot != null && new SerializedObject(boot).FindProperty("playerController").objectReferenceValue == player,
                  $"{prefijo}Rig: SceneBootstrapper conectado al jugador");
            Check(sistema != null && sistema.GetComponent<GuardadoRapido>() != null, $"{prefijo}Rig: F2 guarda (GuardadoRapido)");
            Check(TutorialBuilder.Buscar<DialogueController>(scene) != null, $"{prefijo}Rig: hay caja de dialogo");
            Check(TutorialBuilder.Buscar<EventSystem>(scene) != null, $"{prefijo}Rig: hay EventSystem");

            var gm = TutorialBuilder.Buscar<GameManager>(scene);
            SerializedProperty equipo = gm != null ? new SerializedObject(gm).FindProperty("equipoInicial") : null;
            Check(equipo != null && equipo.arraySize >= 1 && equipo.GetArrayElementAtIndex(0).objectReferenceValue != null,
                  $"{prefijo}Rig: la partida DEMO arranca con un Pokemon");

            Tilemap colisiones = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Tilemap>(true))
                .FirstOrDefault(t => t.name == "Colisiones");
            Check(colisiones != null && colisiones.gameObject.layer == 3 &&
                  colisiones.GetComponent<ColisionesTilemapRuntimeFix>() != null &&
                  colisiones.GetComponent<CompositeCollider2D>() != null,
                  $"{prefijo}Rig: Colisiones en Obstacles con su collider y el arreglo de runtime");

            Physics2D.SyncTransforms();
            List<string> problemas = TutorialBuilder.Verificar(scene, out int alcanzables);
            foreach (string p in problemas) Debug.LogError($"[SmokeTutorial] {p}");
            Check(problemas.Count == 0, $"{prefijo}Rig: se llega caminando a todas las partes y a la puerta ({alcanzables} celdas)");
        }

        // La habitacion de verdad (la del recorrido), si ya existe: que tenga el
        // tutorial pegado entero, el rig y que se llegue a todo sin huecos. Las
        // posiciones son del equipo: aca no se controla donde esta cada cosa.
        private static void TestHabitacion()
        {
            string path = $"Assets/Scenes/{RecorridoDeLaHistoria.EscenaDelTutorial}.unity";
            if (!System.IO.File.Exists(path)) return;

            Scene previa = SceneManager.GetSceneByPath(path);
            bool yaAbierta = previa.IsValid() && previa.isLoaded;
            Scene scene = yaAbierta ? previa : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                var tutorial = TutorialBuilder.Buscar<TutorialDeLaHabitacion>(scene);
                Check(tutorial != null, $"Habitacion: '{scene.name}' tiene el tutorial pegado");
                if (tutorial == null) return;

                List<string> afuera = ReferenciasAfuera(tutorial.transform);
                foreach (string r in afuera) Debug.LogError($"[SmokeTutorial] Habitacion, referencia que se pierde: {r}");
                Check(afuera.Count == 0, "Habitacion: nada adentro del Tutorial apunta a un objeto de afuera");

                TutorialParte[] partes = tutorial.GetComponentsInChildren<TutorialParte>(true);
                Check(partes.Length > 0 && partes.All(p => p.Flag != StoryFlag.None) &&
                      partes.Select(p => p.Flag).Distinct().Count() == partes.Length,
                      "Habitacion: cada parte tiene su propio flag");
                Check(partes.Count(p => p.QueHace == TutorialParte.Tipo.Pelea && p.Rival != null) == 1,
                      "Habitacion: hay una pelea con rival");

                var puerta = tutorial.GetComponentInChildren<PuertaDelTutorial>(true);
                Check(puerta != null && puerta.EscenaSiguiente == RecorridoDeLaHistoria.EscenaDelLobby,
                      "Habitacion: la puerta lleva a la cueva");

                RevisarRig(scene, "Habitacion ");
            }
            finally
            {
                if (!yaAbierta) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Vector2 TutorialDeLaHabitacionCentro(Vector3 p)
            => new Vector2(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f);

        // Referencias de los componentes de adentro de 'raiz' a objetos de la
        // escena que quedan AFUERA. Los assets (dialogos, sprites, el rival) se
        // copian bien y no cuentan; los Transform tampoco (el padre siempre
        // queda afuera y Unity lo resuelve al pegar).
        private static List<string> ReferenciasAfuera(Transform raiz)
        {
            var afuera = new List<string>();
            foreach (Component c in raiz.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform) continue;

                SerializedProperty it = new SerializedObject(c).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object o = it.objectReferenceValue;
                    if (o == null || EditorUtility.IsPersistent(o)) continue;

                    Transform t = o is GameObject g ? g.transform : o is Component k ? k.transform : null;
                    if (t != null && !t.IsChildOf(raiz))
                        afuera.Add($"{c.GetType().Name} de '{c.name}'.{it.propertyPath} -> '{t.name}'");
                }
            }
            return afuera;
        }
    }
}
