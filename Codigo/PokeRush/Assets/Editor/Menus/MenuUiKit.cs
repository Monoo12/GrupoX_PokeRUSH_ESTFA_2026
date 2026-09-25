using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Ladrillos para armar la UI de los menus por codigo.
//
// El generador existe para que el sistema se pueda REGENERAR entero (y para no
// tener que armar seis pantallas a mano en el Inspector), pero lo que produce
// son prefabs normales: una vez creados se editan en Unity como cualquier otro.
public static class MenuUiKit
{
    // Resolucion de referencia. Los fondos del pack (Bag, Party, Summary) son
    // de 512x384, asi que el canvas trabaja en esa medida y escala al monitor.
    public const int RefWidth = 512;
    public const int RefHeight = 384;

    public const string FontPath = "Assets/TextMesh Pro/Fonts/power clear.ttf";

    public static readonly Color Ink = new Color(1f, 1f, 1f, 1f);
    public static readonly Color InkDark = new Color(0.13f, 0.15f, 0.20f, 1f);
    public static readonly Color Panel = new Color(0.16f, 0.18f, 0.24f, 0.96f);
    public static readonly Color PanelLight = new Color(0.27f, 0.31f, 0.40f, 0.98f);
    public static readonly Color Accent = new Color(0.98f, 0.66f, 0.18f, 1f);
    public static readonly Color Border = new Color(0.85f, 0.88f, 0.95f, 1f);
    // Amarillo de las etiquetas del resumen, como en la saga.
    public static readonly Color Gold = new Color(1f, 0.85f, 0.24f, 1f);

    private static Font _font;

    public static Font PixelFont
    {
        get
        {
            if (_font == null) _font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }

    // ---------- creacion de nodos ----------

    public static GameObject Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // Ancla y estira el nodo dentro de su padre.
    // anchorMin/Max en 0..1; offsets en pixeles de la resolucion de referencia.
    public static RectTransform Stretch(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 offsetMin, Vector2 offsetMax)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    public static RectTransform Full(GameObject go)
        => Stretch(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    // Nodo de tamaño fijo anclado a un punto del padre.
    public static RectTransform Box(GameObject go, Vector2 anchor, Vector2 pivot,
                                    Vector2 anchoredPos, Vector2 size)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image AddImage(GameObject go, Sprite sprite, Color color, bool raycast = false)
    {
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        image.preserveAspect = false;
        if (sprite == null) image.sprite = null;
        return image;
    }

    public static Text AddText(GameObject go, string content, int size, TextAnchor anchor, Color color)
    {
        var text = go.AddComponent<Text>();
        text.font = PixelFont;
        text.fontSize = size;
        text.text = content;
        text.alignment = anchor;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    // Atajo: crea un nodo con texto ya posicionado.
    public static Text Label(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                             string content, int fontSize, TextAnchor anchor, Color color,
                             Vector2? anchorPoint = null)
    {
        GameObject go = Node(name, parent);
        Vector2 a = anchorPoint ?? new Vector2(0f, 1f);
        Box(go, a, a, anchoredPos, size);
        return AddText(go, content, fontSize, anchor, color);
    }

    public static Image Rect(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                             Color color, Sprite sprite = null, Vector2? anchorPoint = null)
    {
        GameObject go = Node(name, parent);
        Vector2 a = anchorPoint ?? new Vector2(0f, 1f);
        Box(go, a, a, anchoredPos, size);
        return AddImage(go, sprite, color);
    }

    // Sprite blanco de 4x4 que se genera una sola vez.
    //
    // Existe por un detalle de UGUI que cuesta caro descubrir: un Image SIN
    // sprite ignora 'type' y 'fillAmount' y dibuja siempre el rectangulo
    // completo. O sea que todas las barras de PS/EXP/PP se veian llenas
    // siempre. Con cualquier sprite asignado, el relleno funciona.
    public const string WhitePixelPath = "Assets/Prefabs/UI/Menus/Widgets/white_pixel.png";

    private static Sprite _white;

    public static Sprite WhitePixel
    {
        get
        {
            if (_white != null) return _white;

            _white = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePixelPath);
            if (_white != null) return _white;

            EnsureFolder(System.IO.Path.GetDirectoryName(WhitePixelPath).Replace('\\', '/'));
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixeles = new Color[16];
            for (int i = 0; i < pixeles.Length; i++) pixeles[i] = Color.white;
            tex.SetPixels(pixeles);
            tex.Apply();
            System.IO.File.WriteAllBytes(WhitePixelPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(WhitePixelPath, ImportAssetOptions.ForceUpdate);

            _white = Sprite(WhitePixelPath, true);
            return _white;
        }
    }

    // Barra de relleno horizontal (PS, EXP, PP, volumen).
    public static Image FillBar(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
                                Color backColor, Color fillColor, Vector2? anchorPoint = null)
    {
        Image back = Rect(name, parent, anchoredPos, size, backColor, WhitePixel, anchorPoint);
        GameObject fillGo = Node("Fill", back.transform);
        Stretch(fillGo, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
        Image fill = AddImage(fillGo, WhitePixel, fillColor);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        return fill;
    }

    // Muestra SOLO un trozo de una textura, sin recortarla como sprite.
    //
    // Las pestanias del pack (page_info.png y compania) son de 56x28 y traen
    // DOS frames al lado: apagada y encendida. Como Image dibuja la textura
    // entera, se veian los dos iconos juntos. RawImage con uvRect deja elegir
    // la mitad sin tener que cortar el PNG en el sprite editor.
    public static RawImage MediaTextura(string name, Transform parent, Vector2 size,
                                        string texturePath, bool mitadDerecha)
    {
        GameObject go = Node(name, parent);
        Stretch(go, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // Sprite() ademas deja el importer en Point/sin comprimir, que es lo que
        // hace falta para que el pixel art no salga borroso.
        Sprite(texturePath);

        var raw = go.AddComponent<RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        raw.uvRect = new Rect(mitadDerecha ? 0.5f : 0f, 0f, 0.5f, 1f);
        raw.raycastTarget = false;
        return raw;
    }

    // Contorno oscuro de 1px. Los fondos del pack son celestes claros: el texto
    // blanco encima se pierde sin esto.
    public static Text Contorno(Text text, float alpha = 0.85f)
    {
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.08f, 0.16f, alpha);
        outline.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    public static VerticalLayoutGroup VerticalList(GameObject go, float spacing, RectOffset padding,
                                                   TextAnchor align = TextAnchor.UpperLeft)
    {
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childAlignment = align;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static GridLayoutGroup Grid(GameObject go, Vector2 cell, Vector2 spacing, int columns,
                                       RectOffset padding)
    {
        var layout = go.AddComponent<GridLayoutGroup>();
        layout.cellSize = cell;
        layout.spacing = spacing;
        layout.padding = padding;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        return layout;
    }

    // ---------- assets ----------

    // Deja el PNG importado como sprite pixel-art: sin filtrado ni compresion,
    // que es lo unico que hace que un tileset de 16px no salga borroso.
    public static Sprite Sprite(string path, bool forceReimport = false)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool cambio = importer.textureType != TextureImporterType.Sprite
                          || importer.filterMode != FilterMode.Point
                          || importer.textureCompression != TextureImporterCompression.Uncompressed
                          || !importer.alphaIsTransparency;

            if (cambio || forceReimport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.spritePixelsPerUnit = 100f;
                importer.SaveAndReimport();
            }
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning($"[MenuUiKit] No se encontro el sprite '{path}'.");
        return sprite;
    }

    // Un sub-sprite de una hoja ya cortada (ej: type_5_0 dentro de icon_types.png).
    public static Sprite SubSprite(string sheetPath, string spriteName)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            if (asset is Sprite s && s.name == spriteName) return s;
        return null;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    // Guarda el GameObject como prefab y destruye la instancia temporal.
    public static GameObject SavePrefab(GameObject go, string path)
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // Asigna un campo privado [SerializeField] por nombre. Se usa para no tener
    // que hacer publico medio sistema solo para que el generador lo pueda llenar.
    public static void SetField(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[MenuUiKit] '{target.GetType().Name}' no tiene el campo '{field}'.");
            return;
        }

        switch (value)
        {
            case null: prop.objectReferenceValue = null; break;
            case Object o: prop.objectReferenceValue = o; break;
            case string s: prop.stringValue = s; break;
            case int i: prop.intValue = i; break;
            case float f: prop.floatValue = f; break;
            case bool b: prop.boolValue = b; break;
            case Color c: prop.colorValue = c; break;
            case Vector2 v: prop.vector2Value = v; break;
            default:
                Debug.LogWarning($"[MenuUiKit] Tipo no soportado para '{field}'.");
                break;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Llena un campo que es un array de int (valores de ficha, tablas de pago).
    // Va aparte de SetArrayField porque un array de valores y uno de referencias
    // se llenan por propiedades distintas del SerializedProperty.
    public static void SetIntArrayField(Object target, string field, int[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning($"[MenuUiKit] '{target.GetType().Name}' no tiene el array '{field}'.");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).intValue = values[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Llena un campo que es un array de Object.
    public static void SetArrayField(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning($"[MenuUiKit] '{target.GetType().Name}' no tiene el array '{field}'.");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
