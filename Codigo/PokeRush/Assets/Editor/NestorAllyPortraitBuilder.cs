using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Mete el retrato de Nestor aliado (nivel 2) en el HUD de combate que YA
// existe.
//
// Por que es aditivo y no parte de BattlePrefabBuilder:
//   "PokeRush > Armar prefab de combate" REHACE el prefab entero desde cero.
//   El HUD final lo va a diseñar el equipo a mano, asi que rehacerlo para
//   agregar un recuadro seria pisar ese trabajo. Este comando abre el prefab,
//   agrega lo que falta y lo guarda: no toca nada de lo que ya estaba.
//
// Es IDEMPOTENTE: si el retrato ya existe lo reusa y solo vuelve a cablear los
// sprites. Correrlo dos veces no deja dos retratos.
//
// DONDE QUEDA Y POR QUE
//   Arriba a la DERECHA, no a la izquierda como en la imagen de referencia.
//   En este HUD la esquina superior izquierda ya la ocupan el databox del rival
//   (y=24..94) y la barra del escudo del nucleo (y=96..114). La derecha de
//   arriba esta libre: el Poken rival arranca recien en y=107 y llega hasta
//   x=406, y el databox del jugador esta abajo. Asi no tapa Pokens, barras ni
//   botones, que era el requisito.
namespace PokeRush.EditorTools
{
    public static class NestorAllyPortraitBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
        private const string CarpetaRetratos = "Assets/Sprites/Nestor";
        private const string NombreDelNodo = "RetratoNestor";
        private const string NombreDelGlobo = "Globo";

        // La MISMA fuente que usa BattleTestSceneBuilder. Ojo: en Unity 6
        // "Arial.ttf" ya no existe como built-in y tira ArgumentException; el
        // reemplazo es LegacyRuntime.ttf, y se pide por Resources, no por
        // AssetDatabase.GetBuiltinExtraResource.
        private static Font _fuente;
        private static Font FuenteDeUi =>
            _fuente != null ? _fuente
                : (_fuente = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/power clear.ttf")
                             ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        // Canvas del combate: 512x384 a resolucion nativa.
        private const float Margen = 6f;
        private const float LadoDelRetrato = 60f;
        private const float AnchoDelGlobo = 200f;
        private const float AltoDelGlobo = 32f;

        // Que retrato va en cada animo. El indice es el valor del enum
        // NestorMood, asi que este array y ese enum tienen que moverse juntos.
        private static readonly string[] RetratoPorAnimo =
        {
            "NESTOR_retrato_pensando",    // Neutral
            "NESTOR_retrato_pensando",    // Pensando
            "NESTOR_retrato_enojado",     // Concentrado
            "NESTOR_retrato_feliz",       // Confiado
            "NESTOR_retrato_shock",       // Sorprendido
            "NESTOR_retrato_preocupado",  // Preocupado
            "NESTOR_retrato_furioso"      // Furioso
        };

        [MenuItem("PokeRush/Nestor aliado/Agregar retrato al prefab de combate")]
        public static void AgregarAlPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[RetratoNestor] No encontre {PrefabPath}. " +
                               "Arma el prefab de combate primero (PokeRush > Armar prefab de combate).");
                return;
            }

            GameObject contenido = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var hud = contenido.GetComponentInChildren<BattleHudView>(true);
                if (hud == null)
                {
                    Debug.LogError("[RetratoNestor] El prefab no tiene BattleHudView. No toco nada.");
                    return;
                }

                NestorPortraitView view = AsegurarRetrato(hud);
                CablearEnElHud(hud, view);
                PrefabUtility.SaveAsPrefabAsset(contenido, PrefabPath);
                Debug.Log($"[RetratoNestor] Retrato de Nestor listo en {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contenido);
            }

            AssetDatabase.SaveAssets();
        }

        // Para los que ya montaron el combate a mano en una escena en vez de
        // usar el prefab. Trabaja sobre la escena ABIERTA y no guarda solo: se
        // ve el cambio y se decide si guardar.
        [MenuItem("PokeRush/Nestor aliado/Agregar retrato al combate de esta escena")]
        public static void AgregarAEstaEscena()
        {
            BattleHudView[] huds = Object.FindObjectsByType<BattleHudView>(FindObjectsInactive.Include);

            if (huds.Length == 0)
            {
                Debug.LogWarning("[RetratoNestor] No hay ningun BattleHudView en la escena abierta.");
                return;
            }

            foreach (BattleHudView hud in huds)
            {
                NestorPortraitView view = AsegurarRetrato(hud);
                CablearEnElHud(hud, view);
                EditorUtility.SetDirty(hud);
            }

            Scene escena = SceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log($"[RetratoNestor] Retrato agregado a {huds.Length} HUD de '{escena.name}'. " +
                      "Guarda la escena para que quede.");
        }

        // ---------- armado ----------

        // Devuelve el NestorPortraitView del HUD, creandolo si no estaba. Si ya
        // existia se respeta la jerarquia que tenga (puede estar rediseñada a
        // mano) y solo se le refrescan los sprites.
        private static NestorPortraitView AsegurarRetrato(BattleHudView hud)
        {
            Transform padre = hud.transform;
            var existente = hud.GetComponentInChildren<NestorPortraitView>(true);

            // La limpieza va SIEMPRE, tambien cuando ya hay un retrato bueno.
            //
            // Paso de verdad: la primera version de este script pedia la fuente
            // "Arial.ttf", que en Unity 6 tira ArgumentException. La excepcion
            // cortaba DESPUES de crear el nodo y ANTES del AddComponent, asi que
            // quedaba un "RetratoNestor" sin NestorPortraitView y —peor— activo,
            // porque el SetActive(false) del final tampoco llegaba a correr. Un
            // Image sin sprite se dibuja como un cuadrado BLANCO, y eso es lo que
            // se veia en pantalla tapando el combate.
            //
            // Si esto viviera solo en la rama "no habia retrato", el resto roto
            // sobreviviria para siempre: la busqueda de arriba es por componente
            // y al resto no lo encuentra, asi que se sale por el return de abajo
            // sin limpiar nada.
            LimpiarRestos(padre, existente);

            if (existente != null)
            {
                CablearSprites(existente);
                return existente;
            }

            // Recuadro, arriba a la derecha. Pivot en (1,1) para que el margen
            // se mida contra la esquina y no contra el centro.
            var panel = NuevoRect(NombreDelNodo, padre,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margen, -Margen),
                new Vector2(LadoDelRetrato, LadoDelRetrato));

            var marco = panel.AddComponent<Image>();
            marco.color = new Color(0.16f, 0.17f, 0.24f, 0.85f);
            marco.raycastTarget = false;

            var grupo = panel.AddComponent<CanvasGroup>();
            grupo.blocksRaycasts = false;

            // La cara, con 3px de aire para que se vea el marco alrededor.
            var caraGo = NuevoRect("Cara", panel.transform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var caraRect = (RectTransform)caraGo.transform;
            caraRect.offsetMin = new Vector2(3f, 3f);
            caraRect.offsetMax = new Vector2(-3f, -3f);

            var cara = caraGo.AddComponent<Image>();
            cara.raycastTarget = false;
            // Los retratos son cuadrados: preserveAspect evita que un sprite de
            // otra proporcion salga estirado si despues se agrega uno nuevo.
            cara.preserveAspect = true;

            // Globo de dialogo: debajo del recuadro y hacia la izquierda, para
            // no meterse con el Poken rival (que arranca en y=107).
            var globo = NuevoRect(NombreDelGlobo, padre,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-Margen, -(Margen + LadoDelRetrato + 4f)),
                new Vector2(AnchoDelGlobo, AltoDelGlobo));

            var globoBg = globo.AddComponent<Image>();
            globoBg.color = new Color(0.07f, 0.07f, 0.11f, 0.88f);
            globoBg.raycastTarget = false;

            var texto = NuevoTexto("Texto", globo.transform, 12);
            texto.alignment = TextAnchor.MiddleRight;
            texto.color = new Color(1f, 0.92f, 0.72f);

            globo.SetActive(false);

            var view = panel.AddComponent<NestorPortraitView>();
            var so = new SerializedObject(view);
            so.FindProperty("root").objectReferenceValue = panel;
            so.FindProperty("portraitImage").objectReferenceValue = cara;
            so.FindProperty("marco").objectReferenceValue = marco;
            so.FindProperty("globo").objectReferenceValue = globo;
            so.FindProperty("dialogoText").objectReferenceValue = texto;
            so.FindProperty("pulseGroup").objectReferenceValue = grupo;
            so.ApplyModifiedPropertiesWithoutUndo();

            CablearSprites(view);

            panel.SetActive(false); // lo prende el HUD cuando hay lectura aliada
            return view;
        }

        // Llena el array de retratos por animo. Un sprite que no aparezca queda
        // en null y NestorPortraitView cae al primero de la lista, asi que
        // faltar uno no rompe nada.
        private static void CablearSprites(NestorPortraitView view)
        {
            var sprites = new Object[RetratoPorAnimo.Length];
            int encontrados = 0;
            for (int i = 0; i < RetratoPorAnimo.Length; i++)
            {
                Sprite sprite = CargarRetrato(RetratoPorAnimo[i]);
                sprites[i] = sprite;
                if (sprite != null) encontrados++;
            }

            if (encontrados == 0)
                Debug.LogWarning($"[RetratoNestor] No encontre ningun NESTOR_retrato_* en " +
                                 $"{CarpetaRetratos}. El recuadro va a salir vacio.");

            var so = new SerializedObject(view);
            SerializedProperty array = so.FindProperty("retratosPorAnimo");
            array.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CablearEnElHud(BattleHudView hud, NestorPortraitView view)
        {
            var so = new SerializedObject(hud);
            so.FindProperty("nestorPortraitView").objectReferenceValue = view;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- helpers ----------

        // El .png trae la textura y el Sprite como subassets: hay que pedir el
        // Sprite explicitamente o sale la Texture2D y el Image queda vacio.
        private static Sprite CargarRetrato(string nombre)
        {
            string path = $"{CarpetaRetratos}/{nombre}.png";
            Object[] todos = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (Object asset in todos)
                if (asset is Sprite sprite) return sprite;
            return null;
        }

        // Borra los nodos con el nombre del retrato o del globo que NO sean los
        // del retrato bueno. El globo cuelga del MISMO padre que el recuadro
        // (no de adentro), asi que hay que preguntarle al componente cual es el
        // suyo: borrarlo por nombre a secas se llevaria puesto el bueno.
        private static void LimpiarRestos(Transform padre, NestorPortraitView bueno)
        {
            GameObject raizBuena = bueno != null ? bueno.gameObject : null;
            GameObject globoBueno = GloboDe(bueno);

            // Hacia atras: borrar hijos corre los indices de los que siguen.
            for (int i = padre.childCount - 1; i >= 0; i--)
            {
                GameObject hijo = padre.GetChild(i).gameObject;
                if (hijo == raizBuena || hijo == globoBueno) continue;
                if (hijo.name != NombreDelNodo && hijo.name != NombreDelGlobo) continue;

                Debug.Log($"[RetratoNestor] Borro un resto suelto: '{hijo.name}' " +
                          "(quedo de una corrida que se corto a la mitad).");
                Object.DestroyImmediate(hijo);
            }
        }

        private static GameObject GloboDe(NestorPortraitView view)
        {
            if (view == null) return null;
            var so = new SerializedObject(view);
            return so.FindProperty("globo").objectReferenceValue as GameObject;
        }

        private static GameObject NuevoRect(string nombre, Transform padre,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 posicion, Vector2 tamaño)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = posicion;
            rect.sizeDelta = tamaño;
            return go;
        }

        private static Text NuevoTexto(string nombre, Transform padre, int tamañoDeFuente)
        {
            var go = NuevoRect(nombre, padre,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var rect = (RectTransform)go.transform;
            rect.offsetMin = new Vector2(6f, 2f);
            rect.offsetMax = new Vector2(-6f, -2f);

            var text = go.AddComponent<Text>();
            text.font = FuenteDeUi;
            text.fontSize = tamañoDeFuente;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
