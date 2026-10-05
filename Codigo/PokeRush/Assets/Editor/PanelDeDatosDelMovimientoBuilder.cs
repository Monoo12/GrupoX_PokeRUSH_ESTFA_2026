using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// El panel de POTENCIA y PRECISION del menu de lucha.
//
//   PokeRush > Combate > Agregar panel de potencia y precision
//
// Va a la izquierda del panel de PP y TIPO, con el mismo fondo y la misma
// letra: arriba el icono de categoria (fisico / especial / estado, de
// Graphics/UI/category.png, el mismo que usa el tutor de MTs) y abajo la
// potencia y la precision. Lo llena BattleHudView al pasar por cada movimiento.
//
// Se puede volver a correr: borra el panel anterior y lo rehace.
public static class PanelDeDatosDelMovimientoBuilder
{
    private const string Prefab = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
    private const string Categorias = "Assets/Graphics/UI/category.png";
    private const string Nombre = "PanelDatos";

    [MenuItem("PokeRush/Combate/Agregar panel de potencia y precision")]
    public static void Agregar()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var hud = raiz.GetComponentInChildren<BattleHudView>(true);
            if (hud == null) { Debug.LogError($"[Combate] {Prefab} no tiene BattleHudView."); return; }

            var so = new SerializedObject(hud);
            var pp = so.FindProperty("ppText").objectReferenceValue as Text;
            if (pp == null) { Debug.LogError("[Combate] El HUD no tiene el texto de PP: no se sabe donde va el panel."); return; }

            var info = (RectTransform)pp.transform.parent;
            Transform viejo = info.parent.Find(Nombre);
            if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

            // El panel, igual al de PP/TIPO y pegado a su izquierda.
            var panel = new GameObject(Nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)panel.transform;
            rt.SetParent(info.parent, false);
            rt.SetSiblingIndex(info.GetSiblingIndex());
            rt.anchorMin = info.anchorMin;
            rt.anchorMax = info.anchorMax;
            rt.pivot = info.pivot;
            rt.sizeDelta = new Vector2(92f, info.sizeDelta.y);
            rt.anchoredPosition = new Vector2(info.anchoredPosition.x - info.sizeDelta.x - 4f, info.anchoredPosition.y);

            Image fondo = panel.GetComponent<Image>();
            Image fondoInfo = info.GetComponent<Image>();
            if (fondoInfo != null)
            {
                fondo.sprite = fondoInfo.sprite;
                fondo.type = fondoInfo.type;
                fondo.color = fondoInfo.color;
            }
            fondo.raycastTarget = false;

            // Categoria: una fila de la hoja (fisico arriba).
            var cat = new GameObject("Categoria", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var rc = (RectTransform)cat.transform;
            rc.SetParent(rt, false);
            rc.anchorMin = rc.anchorMax = new Vector2(0.5f, 1f);
            rc.pivot = new Vector2(0.5f, 1f);
            rc.anchoredPosition = new Vector2(0f, -4f);
            rc.sizeDelta = new Vector2(48f, 21f);
            var icono = cat.GetComponent<RawImage>();
            icono.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Categorias);
            icono.uvRect = new Rect(0f, 2f / 3f, 1f, 1f / 3f);
            icono.raycastTarget = false;
            if (icono.texture == null) Debug.LogWarning($"[Combate] No esta {Categorias}: el panel va sin icono de categoria.");

            Text potencia = Copia(pp, rt, "TextoPotencia", -26f);
            Text precision = Copia(pp, rt, "TextoPrecision", -40f);
            potencia.text = "POT 40";
            precision.text = "PRE 100";

            so.FindProperty("moveCategoryIcon").objectReferenceValue = icono;
            so.FindProperty("movePowerText").objectReferenceValue = potencia;
            so.FindProperty("moveAccuracyText").objectReferenceValue = precision;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Debug.Log($"[Combate] Panel de potencia y precision agregado a {Prefab}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    // Un texto con el mismo estilo que el de PP (fuente, color, sombra).
    private static Text Copia(Text modelo, RectTransform padre, string nombre, float y)
    {
        GameObject go = Object.Instantiate(modelo.gameObject, padre);
        go.name = nombre;
        Text t = go.GetComponent<Text>();
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(4f, y);
        r.sizeDelta = new Vector2(84f, 14f);
        t.fontSize = Mathf.Max(8, modelo.fontSize - 1);
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        return t;
    }
}
