using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Arma una escena minima para MIRAR las animaciones de ataque: dos Pokens, el
// fondo, los renderers que usa BattleFxView y un cartel que dice que se esta
// reproduciendo. Sin HUD, sin combate, sin reglas: entras en Play y las ves en
// loop. Es el banco de pruebas que faltaba para ajustar tiempos y tamaños sin
// tener que jugar hasta que un Poken aprenda el movimiento.
namespace PokeRush.EditorTools
{
    public static class AnimationSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/AnimacionesPrueba.unity";

        [MenuItem("PokeRush/Armar escena de animaciones")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildBackground();

            // mismas posiciones y escala que CombatePrueba, para que lo que se
            // vea aca sea representativo del combate real
            PokenView atacante = BuildPokenView("PokenAtacante", new Vector3(-0.95f, -0.35f, 0f), 1.3f,
                "Assets/Sprites/Pokens");
            PokenView defensor = BuildPokenView("PokenDefensor", new Vector3(0.95f, 0.30f, 0f), 1.1f,
                "Assets/Sprites/Pokens");

            BattleFxView fx = BuildBattleFx();
            Text label = BuildLabel();

            var showcaseGo = new GameObject("Showcase");
            var showcase = showcaseGo.AddComponent<AnimationShowcaseView>();
            var so = new SerializedObject(showcase);
            so.FindProperty("fx").objectReferenceValue = fx;
            so.FindProperty("attacker").objectReferenceValue = atacante;
            so.FindProperty("defender").objectReferenceValue = defensor;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[AnimScene] Escena creada en {ScenePath}. Entra en Play para ver " +
                      "las animaciones en loop; la lista se edita en el objeto Showcase.");
        }

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.92f;   // igual que CombatePrueba
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
        }

        private static void BuildBackground()
        {
            var go = new GameObject("FondoBatalla");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = FindSprite("Assets/Sprites/Battlebacks", "Bosque_bg", "Pradera_bg");
            renderer.sortingOrder = -10;
        }

        private static PokenView BuildPokenView(string name, Vector3 position, float scale, string spriteDir)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 0;
            // cualquier sprite de Poken sirve: esto es un maniqui para ver el efecto
            renderer.sprite = FirstSpriteIn(spriteDir);
            var view = go.AddComponent<PokenView>();
            var so = new SerializedObject(view);
            SerializedProperty prop = so.FindProperty("spriteRenderer");
            if (prop != null) prop.objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static BattleFxView BuildBattleFx()
        {
            var fxGo = new GameObject("BattleFx");
            var fx = fxGo.AddComponent<BattleFxView>();

            var projectileGo = new GameObject("Proyectil");
            var projectileRenderer = projectileGo.AddComponent<SpriteRenderer>();
            projectileRenderer.sortingOrder = 8;
            projectileGo.SetActive(false);

            var impactGo = new GameObject("ImpactoGolpe");
            var impactRenderer = impactGo.AddComponent<SpriteRenderer>();
            impactRenderer.sortingOrder = 8;
            impactGo.SetActive(false);

            var fadeCanvasGo = new GameObject("CanvasFx");
            var fadeCanvas = fadeCanvasGo.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 100;
            var fadeGo = new GameObject("Fundido", typeof(RectTransform));
            fadeGo.transform.SetParent(fadeCanvasGo.transform, false);
            StretchFull(fadeGo.GetComponent<RectTransform>());
            var fadeImage = fadeGo.AddComponent<Image>();
            fadeImage.color = new Color(1f, 1f, 1f, 0f);
            fadeImage.raycastTarget = false;
            fadeGo.SetActive(false);

            var so = new SerializedObject(fx);
            so.FindProperty("projectileRenderer").objectReferenceValue = projectileRenderer;
            so.FindProperty("impactRenderer").objectReferenceValue = impactRenderer;
            so.FindProperty("fadeOverlay").objectReferenceValue = fadeImage;
            so.ApplyModifiedPropertiesWithoutUndo();
            return fx;
        }

        // Cartel arriba: nombre del movimiento, su clase y de donde sale la animacion
        private static Text BuildLabel()
        {
            var canvasGo = new GameObject("CanvasInfo");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            var textGo = new GameObject("Etiqueta", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(-24f, 70f);

            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.UpperCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "...";
            var shadow = textGo.AddComponent<Outline>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        // ---------- utilidades ----------

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite FindSprite(string dir, params string[] preferidos)
        {
            foreach (string nombre in preferidos)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{nombre}.png");
                if (s != null) return s;
            }
            return FirstSpriteIn(dir);
        }

        private static Sprite FirstSpriteIn(string dir)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            string guid = AssetDatabase.FindAssets("t:Sprite", new[] { dir }).FirstOrDefault();
            return guid == null ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
