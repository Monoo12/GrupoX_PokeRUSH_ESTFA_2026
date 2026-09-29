using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Smoke tests de la historia repartida por los niveles. Mismo esquema que los
// demas: se corren desde el menu y reportan con la etiqueta [SmokeHistoria] y
// una linea de resumen al final (se lee del Editor.log con un grep).
//
// Prueban:
//   - lo nuevo del sistema de dialogos (quien habla en cada linea, marcas,
//     variantes, respuestas con requisito, cadenas sin bucles vacios);
//   - que todos los assets de historia esten sanos (lineas que entran en la
//     caja, cadenas que no apuntan a la nada, objetos clave con nombre unico);
//   - la continuidad: con la partida en cada momento, cada NPC dice lo que
//     tiene que decir (antes y despues de Nestor, Cruz, el hermano, Tacconi);
//   - que las escenas tengan enganchado lo suyo (leyendo el .unity, sin abrirlo:
//     abrir los pisos del casino les borra los Grid en memoria).
namespace PokeRush.EditorTools
{
    public static class HistoriaSmokeTests
    {
        private static int _passed;
        private static int _failed;

        public static int Fallos => _failed;

        private const int LargoMaximo = 125;

        [MenuItem("PokeRush/Historia/Correr smoke tests de la historia")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            TestPartirLineas();
            TestMarcasSinPartida();
            TestVariantes();
            TestRespuestasConRequisito();
            TestAssetsSanos();
            TestCadenasSinBuclesVacios();
            TestObjetosClave();
            TestContinuidadDataCenter();
            TestContinuidadCasino();
            TestVueltasDeTixi();
            TestEscenasEnganchadas();

            if (_failed == 0) Debug.Log($"[SmokeHistoria] TERMINADO. OK={_passed} FALLOS=0");
            else Debug.LogError($"[SmokeHistoria] TERMINADO. OK={_passed} FALLOS={_failed}");
        }

        private static void Check(bool condicion, string nombre)
        {
            if (condicion) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[SmokeHistoria] FALLO: {nombre}");
            }
        }

        private static DialogueDefinition D(string ruta)
        {
            var d = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(HistoriaAssets.Definiciones + ruta + ".asset");
            Check(d != null, $"existe {ruta}");
            return d;
        }

        private static StoryModel Partida(params StoryFlag[] flags)
        {
            var s = new StoryModel();
            foreach (StoryFlag f in flags) s.ActivateFlag(f);
            return s;
        }

        private static string Primera(DialogueDefinition d, StoryModel s)
        {
            string[] l = d != null ? d.LineasPara(s) : null;
            return l != null && l.Length > 0 ? l[0] : "";
        }

        private static bool Dice(DialogueDefinition d, StoryModel s, string fragmento)
            => d != null && (d.LineasPara(s) ?? new string[0]).Any(l => l.Contains(fragmento));

        // ---------- el sistema ----------

        private static void TestPartirLineas()
        {
            Check(DialogueController.Partir("[TIXI] Hola.", out string n, out string t) && n == "TIXI" && t == "Hola.",
                  "Partir: [TIXI] Hola. -> TIXI / Hola.");
            Check(DialogueController.Partir("[] Llueve.", out n, out t) && n == "" && t == "Llueve.",
                  "Partir: [] es un narrador sin nombre");
            Check(!DialogueController.Partir("LUCHAR: elegís un movimiento.", out n, out t) && t == "LUCHAR: elegís un movimiento.",
                  "Partir: una linea sin corchete no se toca (los dos puntos no cuentan)");
            Check(!DialogueController.Partir("[sin cerrar", out n, out t), "Partir: un corchete sin cerrar no es hablante");
            Check(DialogueController.EsElJugador("PJ") && DialogueController.EsElJugador("{PJ}") && !DialogueController.EsElJugador("TIXI"),
                  "EsElJugador: PJ y {PJ} son el jugador");
        }

        private static void TestMarcasSinPartida()
        {
            // En el editor no hay GameManager: las marcas caen a un texto neutro.
            string t = DialogueController.Marcas("Hola, {PJ}. Tu {INICIAL}.");
            Check(!t.Contains("{PJ}") && !t.Contains("{INICIAL}"), "Marcas: {PJ} e {INICIAL} se reemplazan siempre");
            Check(DialogueController.ComoTextoPlano("[TIXI] Hola.") == "TIXI: Hola.", "Texto plano: [TIXI] Hola. -> TIXI: Hola.");
            Check(DialogueController.ComoTextoPlano("[] Llueve.") == "Llueve.", "Texto plano: el narrador va sin nombre");
        }

        private static void TestVariantes()
        {
            var d = ScriptableObject.CreateInstance<DialogueDefinition>();
            try
            {
                d.lines = new[] { "base" };
                d.grantsFlag = StoryFlag.Historia_DC_TecnicoHablo;
                d.variantes = new[]
                {
                    new DialogueDefinition.Variante { requiere = StoryFlag.Torre1_NestorLiberado, requiereTambien = StoryFlag.Casino_Completado,
                        lineas = new[] { "las dos" }, otorgaFlag = StoryFlag.None },
                    new DialogueDefinition.Variante { requiere = StoryFlag.Torre1_NestorLiberado, excluye = StoryFlag.Casino_Completado,
                        lineas = new[] { "solo nestor" }, otorgaFlag = StoryFlag.Historia_NestorDesperto },
                };

                Check(Primera(d, Partida()) == "base", "Variantes: sin flags van las lineas del asset");
                Check(d.FlagPara(Partida()) == StoryFlag.Historia_DC_TecnicoHablo, "Variantes: sin variante, el flag es el del asset");
                Check(Primera(d, Partida(StoryFlag.Torre1_NestorLiberado)) == "solo nestor", "Variantes: la que pide un flag");
                Check(d.FlagPara(Partida(StoryFlag.Torre1_NestorLiberado)) == StoryFlag.Historia_NestorDesperto,
                      "Variantes: con variante, el flag es el de la variante");
                Check(Primera(d, Partida(StoryFlag.Torre1_NestorLiberado, StoryFlag.Casino_Completado)) == "las dos",
                      "Variantes: gana la primera que se cumple (pide dos flags)");
                Check(Primera(d, null) == "base", "Variantes: sin partida, las del asset");
                Check(d.TieneQueDecir(Partida()), "TieneQueDecir: con lineas, si");

                d.lines = new[] { "", "  " };
                Check(!d.TieneQueDecir(Partida()), "TieneQueDecir: renglones vacios no cuentan");
            }
            finally { Object.DestroyImmediate(d); }
        }

        private static void TestRespuestasConRequisito()
        {
            var d = ScriptableObject.CreateInstance<DialogueDefinition>();
            var llave = ScriptableObject.CreateInstance<SpecialItemDefinition>();
            try
            {
                llave.itemName = "Llave de prueba";
                d.pregunta = "¿?";
                d.respuestas = new[]
                {
                    new DialogueDefinition.Respuesta { opcion = "Con llave", requiereObjeto = llave },
                    new DialogueDefinition.Respuesta { opcion = "Con flag", requiere = StoryFlag.Historia_CruzPateado },
                    new DialogueDefinition.Respuesta { opcion = "Siempre" },
                    new DialogueDefinition.Respuesta { opcion = "" },
                };

                var player = new PlayerModel("Prueba");
                var sinNada = d.RespuestasDisponibles(Partida(), player);
                Check(sinNada.Count == 1 && sinNada[0].opcion == "Siempre", "Respuestas: sin llave ni flag queda una (la vacia no cuenta)");

                player.Inventory.AddSpecialItem(llave, 1);
                var conTodo = d.RespuestasDisponibles(Partida(StoryFlag.Historia_CruzPateado), player);
                Check(conTodo.Count == 3 && conTodo[0].opcion == "Con llave", "Respuestas: con llave y flag aparecen, en orden");
                Check(d.TienePregunta, "TienePregunta: con pregunta y respuestas");
            }
            finally
            {
                Object.DestroyImmediate(d);
                Object.DestroyImmediate(llave);
            }
        }

        // ---------- los assets ----------

        private static IEnumerable<DialogueDefinition> TodosLosDialogos()
        {
            return AssetDatabase.FindAssets("t:DialogueDefinition", new[] { "Assets/Resources/Definitions" })
                .Select(g => AssetDatabase.LoadAssetAtPath<DialogueDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null);
        }

        private static void TestAssetsSanos()
        {
            int revisados = 0;
            foreach (DialogueDefinition d in TodosLosDialogos())
            {
                revisados++;
                var todas = new List<string>(d.lines ?? new string[0]);
                if (d.variantes != null)
                    foreach (var v in d.variantes) if (v?.lineas != null) todas.AddRange(v.lineas);

                foreach (string l in todas)
                {
                    if (string.IsNullOrEmpty(l)) continue;
                    DialogueController.Partir(l, out _, out string texto);
                    if (texto.Length > LargoMaximo) Check(false, $"{d.name}: linea demasiado larga ({texto.Length}): {texto.Substring(0, 40)}...");
                    if (l.StartsWith("[") && l.IndexOf(']') < 0) Check(false, $"{d.name}: corchete sin cerrar: {l}");
                }

                if (d.respuestas != null)
                    foreach (var r in d.respuestas)
                        if (r != null && !string.IsNullOrEmpty(r.opcion) && r.siguiente == null)
                            Check(false, $"{d.name}: la respuesta '{r.opcion}' no lleva a ningun lado");
                if (!string.IsNullOrWhiteSpace(d.pregunta) && (d.respuestas == null || d.respuestas.Length == 0))
                    Check(false, $"{d.name}: pregunta sin respuestas");
            }
            Check(revisados >= 80, $"Assets: se revisaron {revisados} dialogos");
        }

        // Una cadena que vuelve sobre si misma sin ninguna pregunta en el medio
        // colgaria la caja (el controller la corta, pero es un error de datos).
        private static void TestCadenasSinBuclesVacios()
        {
            foreach (DialogueDefinition d in TodosLosDialogos())
            {
                var vistos = new HashSet<DialogueDefinition>();
                DialogueDefinition actual = d;
                while (actual != null && !actual.TienePregunta)
                {
                    if (!vistos.Add(actual)) { Check(false, $"{d.name}: su 'siguiente' da la vuelta sin ninguna pregunta"); break; }
                    actual = actual.siguiente;
                }
            }
            Check(true, "Cadenas: revisadas");
        }

        private static void TestObjetosClave()
        {
            var nombres = new HashSet<string>();
            foreach (var (archivo, icono) in HistoriaAssets.Objetos)
            {
                SpecialItemDefinition item = HistoriaAssets.Objeto(archivo);
                Check(item != null, $"Objetos: existe {archivo}");
                if (item == null) continue;
                Check(!string.IsNullOrEmpty(item.itemName) && nombres.Add(item.itemName), $"Objetos: '{item.itemName}' tiene nombre unico (se guarda por nombre)");
                if (item.icon == null) Debug.LogWarning($"[SmokeHistoria] {archivo} no tiene icono: correr 'Poner iconos a los objetos de la historia'.");
            }

            var todos = Resources.LoadAll<SpecialItemDefinition>("Definitions/Items");
            Check(todos.Select(i => i.itemName).Distinct().Count() == todos.Length, "Objetos: ningun nombre repetido en Definitions/Items");

            Check(D("Tutorial/Dialogos/Tutorial_Despertar")?.otorgaObjeto == HistoriaAssets.Objeto("CartaAnonima"), "Objetos: la carta sale al despertar");
            Check(D("Historia/Camino/Camino_CruzAtacar")?.otorgaObjeto == HistoriaAssets.Objeto("RaspberryPy"), "Objetos: la RaspBerry sale de patear al Arturito");
            Check(D("Torre1/Dialogos/DC_Nestor_Despues_Protocolo")?.otorgaObjeto == HistoriaAssets.Objeto("ProtocoloNestor"), "Objetos: Nestor da el Protocolo");
            Check(D("Casino/Dialogos/Casino_Tacconi_DespuesDeGanar")?.otorgaObjeto == HistoriaAssets.Objeto("TarjetaPiso4"), "Objetos: Tacconi da la tarjeta del piso 4");

            var cruz = D("Historia/Camino/Camino_Inicio");
            Check(cruz != null && cruz.cancelarElige >= 0 && cruz.respuestas[cruz.cancelarElige].opcion == "Huir",
                  "Camino: con Cruz, Escape elige huir (no atacar)");
            var dialogar = D("Historia/Camino/Camino_CruzDialogar");
            Check(dialogar != null && dialogar.cancelarElige >= 0 && dialogar.respuestas[dialogar.cancelarElige].opcion == "Mentir",
                  "Camino: dialogando con Cruz, Escape elige mentir (no atacar)");

            var cerradura = D("Historia/Camino/Camino_Cerradura");
            var player = new PlayerModel("Prueba");
            Check(cerradura != null && cerradura.RespuestasDisponibles(Partida(), player).Count == 2,
                  "Camino: sin la RaspBerry, la cerradura ofrece hackear o explorar");
            player.Inventory.AddSpecialItem(HistoriaAssets.Objeto("RaspberryPy"), 1);
            Check(cerradura != null && cerradura.RespuestasDisponibles(Partida(), player).Count == 3,
                  "Camino: con la RaspBerry aparece la opcion de usarla");
        }

        // ---------- continuidad ----------

        private static void TestContinuidadDataCenter()
        {
            StoryModel antes = Partida();
            StoryModel despues = Partida(StoryFlag.Torre1_NestorLiberado, StoryFlag.Historia_Servidor01Desconectado);

            foreach (string npc in new[] { "DC_Recepcionista", "DC_Operaria", "DC_Tecnico", "DC_Entusiasta", "DC_Dormido",
                                           "DC_Guardia", "DC_Biologa", "DC_Porygon", "DC_TerminalHall", "DC_Maquina" })
            {
                var d = D("Torre1/Dialogos/" + npc);
                Check(d != null && Primera(d, antes) != Primera(d, despues), $"Data Center: {npc} cambia lo que dice cuando cae el servidor");
            }

            var guardiaCruz = D("Torre1/Dialogos/DC_Guardia_Cruz");
            Check(Dice(guardiaCruz, Partida(StoryFlag.Historia_CruzPateado), "robot"), "Data Center: el guardia sabe que pateaste al robot de Cruz");
            Check(Dice(guardiaCruz, Partida(StoryFlag.Historia_CruzEnganiado), "estación de trabajo"), "Data Center: el guardia repite tu mentira a Cruz");
            Check(Dice(guardiaCruz, Partida(StoryFlag.Historia_CruzHuida), "corriendo"), "Data Center: el guardia sabe que huiste");
            Check(!guardiaCruz.TieneQueDecir(Partida(StoryFlag.Historia_CruzPateado, StoryFlag.Historia_DC_GuardiaRecibioOrden)),
                  "Data Center: lo de Cruz se cuenta una sola vez");

            var antesDeNestor = D("Torre1/Dialogos/DC_Nestor_Antes");
            Check(antesDeNestor != null && antesDeNestor.grantsFlag == StoryFlag.Historia_NestorPresentado,
                  "Nestor: la charla de antes se marca (la segunda vez va la corta)");
            var despuesDeNestor = D("Torre1/Dialogos/DC_Nestor_Despues");
            Check(despuesDeNestor != null && despuesDeNestor.grantsFlag == StoryFlag.Historia_Servidor01Desconectado &&
                  despuesDeNestor.siguiente != null,
                  "Nestor: a mitad de la charla se desconecta el servidor (ahi se apagan las luces) y sigue");

            // Nestor no se nombra antes de tiempo: ni Tixi, ni el cartel de la salida.
            Check(!Dice(D("Lobby/Dialogos/Lobby_Tixi_Plan"), Partida(), "Néstor") &&
                  !Dice(D("Lobby/Dialogos/Lobby_Tixi_Lore"), Partida(), "Néstor"),
                  "Sin spoilers: Tixi no nombra a Nestor antes del Data Center");
            Check(!RecorridoDeLaHistoria.Niveles[0].Nombre.Contains("Néstor"), "Sin spoilers: la salida dice 'el primer Data Center'");
        }

        private static void TestContinuidadCasino()
        {
            var secretaria = D("Casino/Dialogos/Historia_Piso2_Secretaria");
            Check(!Dice(secretaria, Partida(), "hermano") && Dice(secretaria, Partida(StoryFlag.Historia_DC_TecnicoHablo), "hermano"),
                  "Casino: la secretaria reconoce a su hermano solo si hablaste con el tecnico");

            var jugador = D("Casino/Dialogos/JugadorRuleta2");
            Check(!Dice(jugador, Partida(), "Ameghino") && Dice(jugador, Partida(StoryFlag.Historia_Casino_CroupierHablo), "Ameghino"),
                  "Casino: el jugador de la ruleta habla de Tacconi recien despues del croupier");

            var mujer = D("Casino/Dialogos/JugadorTragaperras2");
            Check(Dice(mujer, Partida(), "Cinco años") && !Dice(mujer, Partida(StoryFlag.Historia_Casino_MujerHablo), "Cinco años."),
                  "Casino: la mujer cuenta lo suyo una vez");

            Check(Dice(D("Casino/Dialogos/Historia_PisoFinal_GuardiaPasillo1"), Partida(StoryFlag.Historia_Casino_MujerHablo), "tragamonedas"),
                  "Casino: un guardia del ultimo piso sabe que la mujer aviso");
            Check(Dice(D("Casino/Dialogos/Casino_Reymar_Antes_Tacconi"), Partida(StoryFlag.Historia_TacconiRevelo), "Tacconi"),
                  "Casino: Martin sabe que Tacconi hablo de mas");
            Check(Dice(D("Casino/Dialogos/Casino_Tacconi_DespuesDeGanar"), Partida(), "desarrollo"),
                  "Casino: Tacconi dice que eras parte del desarrollo");
            Check(Dice(D("Casino/Dialogos/Casino_Reymar_Despues"), Partida(), "BUSCANDO: {PJ}"),
                  "Casino: en el ultimo piso se busca el nombre del PJ");
        }

        private static void TestVueltasDeTixi()
        {
            var go = new GameObject("TixiDePrueba");
            try
            {
                go.AddComponent<BoxCollider2D>();
                var tixi = go.AddComponent<QuarchoTixiNpc>();
                var so = new SerializedObject(tixi);
                HistoriaAssets.PonerLista(so.FindProperty("alVolver"),
                    "Lobby/Dialogos/Lobby_Tixi_VueltaDelDataCenter", "Lobby/Dialogos/Lobby_Tixi_VueltaDelCasino");
                so.ApplyModifiedPropertiesWithoutUndo();

                Check(tixi.VueltaQueToca(Partida()) == null, "Tixi: sin presentarse no cuenta vueltas");
                Check(tixi.VueltaQueToca(Partida(StoryFlag.Lobby_TixiPresentado)) == null, "Tixi: al principio no hay vuelta");
                Check(tixi.VueltaQueToca(Partida(StoryFlag.Lobby_TixiPresentado, StoryFlag.Torre1_NestorLiberado))?.name == "Lobby_Tixi_VueltaDelDataCenter",
                      "Tixi: al volver de Nestor cuenta lo del Data Center");
                Check(tixi.VueltaQueToca(Partida(StoryFlag.Lobby_TixiPresentado, StoryFlag.Torre1_NestorLiberado,
                          StoryFlag.Historia_TixiVueltaDelDataCenter)) == null,
                      "Tixi: la vuelta del Data Center se cuenta una sola vez");
                Check(tixi.VueltaQueToca(Partida(StoryFlag.Lobby_TixiPresentado, StoryFlag.Torre1_NestorLiberado,
                          StoryFlag.Historia_TixiVueltaDelDataCenter, StoryFlag.Casino_Completado))?.name == "Lobby_Tixi_VueltaDelCasino",
                      "Tixi: al volver del casino cuenta lo del casino");

                var memo = D("Lobby/Dialogos/Lobby_Tixi_VueltaDelDataCenter_Memo");
                Check(memo != null && !memo.TieneQueDecir(Partida()) && memo.TieneQueDecir(Partida(StoryFlag.Historia_DC_MemoLeido)),
                      "Tixi: lo del memo de J. suena solo si lo leiste");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---------- escenas (leidas como texto) ----------

        private static string Guid(string ruta) => AssetDatabase.AssetPathToGUID(HistoriaAssets.Definiciones + ruta + ".asset");

        private static bool EscenaUsa(string escena, string ruta)
        {
            string path = $"Assets/Scenes/{escena}.unity";
            if (!File.Exists(path)) return false;
            string g = Guid(ruta);
            return !string.IsNullOrEmpty(g) && File.ReadAllText(path).Contains(g);
        }

        private static void TestEscenasEnganchadas()
        {
            Check(EscenaUsa("NestorGOD2", "Torre1/Dialogos/DC_Nestor_Antes"), "Escena: Nestor tiene su charla de antes");
            Check(EscenaUsa("NestorGOD2", "Torre1/Dialogos/DC_Nestor_Despues"), "Escena: Nestor tiene su charla de despues");
            Check(EscenaUsa("NestorGOD2", "Torre1/Dialogos/DC_Guardia"), "Escena: el Data Center tiene sus NPCs");
            Check(EscenaUsa("NestorGOD2", "Torre1/Dialogos/DC_Zona_Pantallas"), "Escena: el Data Center tiene sus zonas");
            string nestor = File.ReadAllText("Assets/Scenes/NestorGOD2.unity");
            Check(nestor.Contains(AssetDatabase.AssetPathToGUID("Assets/Scripts/Lobby/SalidaDelNivel.cs")), "Escena: el Data Center tiene salida");
            Check(nestor.Contains(AssetDatabase.AssetPathToGUID("Assets/Scripts/Controllers/ApagonDelNivel.cs")), "Escena: el Data Center tiene el apagon");

            Check(EscenaUsa("Quarcho-Cueva", "Lobby/Dialogos/Lobby_Tixi_VueltaDelDataCenter") &&
                  EscenaUsa("Quarcho-Cueva", "Lobby/Dialogos/Lobby_Tixi_VueltaDelCasino"), "Escena: Tixi cuenta las vueltas");
            Check(EscenaUsa("Quarcho-Cueva", "Lobby/Dialogos/Lobby_Tixi_Charla"), "Escena: Tixi tiene charla");
            Check(EscenaUsa("Quarcho-Cueva", "Lobby/Dialogos/Lobby_Tacconi_Presentacion"), "Escena: Tacconi se presenta en la cueva");
            Check(EscenaUsa("habitacion", "Historia/Camino/Camino_Inicio"), "Escena: la puerta de la habitacion cuenta el camino");
            Check(EscenaUsa("Casino Nivel 2", "Casino/Dialogos/Casino_Croupier"), "Escena: hay un croupier en el piso 2");
            Check(EscenaUsa("Casino nivel 3", "Casino/Dialogos/Casino_Tacconi_Presentacion") &&
                  EscenaUsa("Casino nivel 3", "Casino/Dialogos/Casino_Tacconi_DespuesDeGanar"), "Escena: Tacconi cuenta lo del registro");
            Check(EscenaUsa("Casino piso FINAL", "Casino/Dialogos/Casino_Reymar_Antes") &&
                  EscenaUsa("Casino piso FINAL", "Casino/Dialogos/Casino_Reymar_Despues"), "Escena: Martin tiene su historia");
        }
    }
}
