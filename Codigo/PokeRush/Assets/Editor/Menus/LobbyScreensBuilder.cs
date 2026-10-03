using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Arma las dos pantallas de la Quarcho Cueva y las registra en el MenuSystem:
//
//   PokeRush > UI > Construir las pantallas de la Quarcho Cueva
//
//   - La tienda de Tixi (ShopScreen), con el arte de Graphics/UI/Mart.
//   - El tutor de MTs de Nestor (MoveTutorScreen), con el de Graphics/UI/Move
//     Reminder (el Recordador de movimientos de Pokemon Essentials).
//
// Igual que el del PC: NO reconstruye el sistema de menus, le agrega las
// pantallas que faltan y lo vuelve a guardar, asi no pisa lo que el equipo
// haya retocado en las otras. Correrlo de nuevo rehace SOLO estas dos.
//
// Son pantallas de PRUEBA con el arte del pack (ver la memoria del proyecto:
// las pantallas finales las diseña el equipo). Las medidas de abajo son pixeles
// del arte, con el origen arriba a la izquierda como en un editor de imagenes;
// P() las pasa al centro, que es lo que usa UGUI.
public static class LobbyScreensBuilder
{
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";
    private const string ShopPath = "Assets/Prefabs/UI/Menus/ShopScreen.prefab";
    private const string TutorPath = "Assets/Prefabs/UI/Menus/MoveTutorScreen.prefab";
    private const string ShopRowPath = "Assets/Prefabs/UI/Menus/Widgets/ShopRow.prefab";
    private const string TutorRowPath = "Assets/Prefabs/UI/Menus/Widgets/TutorMoveRow.prefab";

    private const string Mart = "Assets/Graphics/UI/Mart/";
    private const string Reminder = "Assets/Graphics/UI/Move Reminder/";
    private const string Ventana = "Assets/Graphics/Windowskins/choice 1.png";
    private const string Categorias = "Assets/Graphics/UI/category.png";

    // Tinta de los textos sobre fondo claro (la ventana blanca de la tienda,
    // el panel gris del tutor).
    private static readonly Color Tinta = new Color(0.19f, 0.21f, 0.27f, 1f);
    private static readonly Color TintaApagada = new Color(0.55f, 0.57f, 0.62f, 1f);
    private static readonly Color Telon = new Color(0.12f, 0.44f, 0.78f, 1f);

    // ---- tienda (Mart/bg.png) ----
    private static readonly Rect Dinero = new Rect(4, 4, 182, 64);
    private static readonly Rect ListaTienda = new Rect(210, 28, 284, 224);
    private const float FilaTienda = 32f;
    private static readonly Rect IconoObjeto = new Rect(12, 310, 48, 48);
    private static readonly Rect Descripcion = new Rect(80, 292, 232, 84);
    private static readonly Rect Contador = new Rect(318, 300, 186, 60);

    // ---- tutor (Move Reminder/bg.png) ----
    private static readonly Rect Titulo = new Rect(16, 30, 200, 30);
    private static readonly Rect FlechaArriba = new Rect(228, 36, 20, 20);
    private static readonly Rect FlechaAbajo = new Rect(228, 332, 20, 20);
    private static readonly Rect ListaTutor = new Rect(8, 66, 248, 256);
    private const float FilaTutor = 64f;
    private static readonly Rect Pie = new Rect(16, 328, 200, 28);
    private static readonly Rect Vacio = new Rect(20, 150, 224, 80);
    private static readonly Rect IconoPoken = new Rect(266, 28, 64, 64);
    private static readonly Rect NombrePoken = new Rect(334, 32, 168, 24);
    private static readonly Rect Tipo1 = new Rect(334, 60, 72, 24);
    private static readonly Rect Tipo2 = new Rect(410, 60, 72, 24);
    private static readonly Rect[] Renglones =
    {
        new Rect(270, 100, 230, 22), new Rect(270, 122, 230, 22),
        new Rect(270, 144, 230, 22), new Rect(270, 166, 230, 22)
    };
    private static readonly Rect IconoCategoria = new Rect(430, 97, 64, 28);
    private static readonly Rect DescripcionTutor = new Rect(270, 200, 230, 154);

    [MenuItem("PokeRush/UI/Construir las pantallas de la Quarcho Cueva")]
    public static void Construir()
    {
        MenuUiKit.EnsureFolder("Assets/Prefabs/UI/Menus/Widgets");

        GameObject tienda = ArmarTienda();
        GameObject tutor = ArmarTutor();
        if (tienda == null || tutor == null) return;

        Registrar(tienda.GetComponent<MenuScreen>(), tutor.GetComponent<MenuScreen>());
        AssetDatabase.SaveAssets();
        Debug.Log("[Lobby] Pantallas de la tienda y del tutor generadas y registradas en el MenuSystem.");
    }

    // ---------- tienda ----------

    private static GameObject ArmarTienda()
    {
        Sprite fondo = MenuUiKit.Sprite(Mart + "bg.png");
        Sprite cursor = SpriteConBorde(Mart + "cursor.png", 8);
        Sprite ventana = SpriteConBorde(Ventana, 16);
        if (fondo == null || cursor == null || ventana == null)
        {
            Debug.LogError($"[Lobby] Falta el arte de {Mart} o {Ventana}. No se genero la tienda.");
            return null;
        }

        ShopRowWidget fila = ArmarFilaDeTienda(cursor);

        var root = new GameObject("ShopScreen", typeof(RectTransform));
        MenuUiKit.Full(root);
        var screen = root.AddComponent<ShopScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Shop);

        Transform m = Nodo("Marco", root.transform, Vector2.zero,
            new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));

        // El fondo es transparente arriba a la izquierda: ahi se ve la cueva.
        GameObject bg = MenuUiKit.Node("Fondo", m);
        MenuUiKit.Full(bg);
        MenuUiKit.AddImage(bg, fondo, Color.white);

        // Etiqueta y monto en dos Text: con un solo Text de dos renglones la
        // fuente pixel los encimaba.
        Image ventanaDinero = Imagen("VentanaDinero", m, P(Dinero), Dinero.size, ventana);
        ventanaDinero.type = Image.Type.Sliced;
        var renglonDinero = new Vector2(Dinero.width - 28f, 20f);
        Texto("Etiqueta", ventanaDinero.transform, new Vector2(0f, 10f), renglonDinero,
            "DINERO", 15, TextAnchor.MiddleLeft, Tinta);
        Text dinero = Texto("Dinero", ventanaDinero.transform, new Vector2(0f, -10f), renglonDinero,
            "$ 0", 16, TextAnchor.MiddleRight, Tinta);

        RectTransform lista = Nodo("Lista", m, P(ListaTienda), ListaTienda.size);
        var layout = MenuUiKit.VerticalList(lista.gameObject, 0f, new RectOffset(0, 0, 0, 0));
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;

        Image icono = Imagen("IconoObjeto", m, P(IconoObjeto), IconoObjeto.size, null);
        icono.preserveAspect = true;
        icono.enabled = false;

        Text descripcion = Texto("Descripcion", m, P(Descripcion), Descripcion.size, "", 15,
            TextAnchor.UpperLeft, Color.white);
        descripcion.horizontalOverflow = HorizontalWrapMode.Wrap;
        MenuUiKit.Contorno(descripcion);

        Image contador = Imagen("Contador", m, P(Contador), Contador.size, ventana);
        contador.type = Image.Type.Sliced;
        Text cantidad = Texto("Cantidad", contador.transform, new Vector2(-40f, 0f), new Vector2(80f, 30f),
            "x 01", 18, TextAnchor.MiddleLeft, Tinta);
        Text total = Texto("Total", contador.transform, new Vector2(38f, 0f), new Vector2(90f, 30f),
            "$ 0", 18, TextAnchor.MiddleRight, Tinta);
        contador.gameObject.SetActive(false);

        MenuUiKit.SetField(screen, "rowPrefab", fila);
        MenuUiKit.SetField(screen, "rowContainer", lista);
        MenuUiKit.SetField(screen, "visibleRows", Mathf.FloorToInt(ListaTienda.height / FilaTienda));
        MenuUiKit.SetField(screen, "itemIcon", icono);
        MenuUiKit.SetField(screen, "descriptionText", descripcion);
        MenuUiKit.SetField(screen, "moneyText", dinero);
        MenuUiKit.SetField(screen, "quantityWindow", contador.gameObject);
        MenuUiKit.SetField(screen, "quantityText", cantidad);
        MenuUiKit.SetField(screen, "quantityPriceText", total);

        return MenuUiKit.SavePrefab(root, ShopPath);
    }

    private static ShopRowWidget ArmarFilaDeTienda(Sprite cursor)
    {
        var root = new GameObject("ShopRow", typeof(RectTransform));
        MenuUiKit.Box(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
            new Vector2(ListaTienda.width, FilaTienda));

        // El marco rojo del pack mide 312 y la ventana 284: se estira como
        // 9-slice a lo ancho de la fila (+4 por lado, sobre el borde).
        GameObject marco = MenuUiKit.Node("Cursor", root.transform);
        MenuUiKit.Stretch(marco, Vector2.zero, Vector2.one, new Vector2(-4f, -1f), new Vector2(4f, 1f));
        Image img = MenuUiKit.AddImage(marco, cursor, Color.white);
        img.type = Image.Type.Sliced;
        marco.SetActive(false);

        Text nombre = MenuUiKit.Label("Nombre", root.transform, new Vector2(12f, -4f),
            new Vector2(180f, 24f), "Objeto", 16, TextAnchor.MiddleLeft, Tinta);
        Text precio = MenuUiKit.Label("Precio", root.transform, new Vector2(-12f, -4f),
            new Vector2(90f, 24f), "$ 0", 16, TextAnchor.MiddleRight, Tinta, new Vector2(1f, 1f));

        var widget = root.AddComponent<ShopRowWidget>();
        MenuUiKit.SetField(widget, "nameText", nombre);
        MenuUiKit.SetField(widget, "priceText", precio);
        MenuUiKit.SetArrayField(widget, "selectedObjects", new Object[] { marco });
        MenuUiKit.SetArrayField(widget, "tintedGraphics", new Object[] { nombre, precio });
        MenuUiKit.SetField(widget, "normalColor", Tinta);
        MenuUiKit.SetField(widget, "selectedColor", Tinta);
        MenuUiKit.SetField(widget, "disabledColor", TintaApagada);

        return MenuUiKit.SavePrefab(root, ShopRowPath).GetComponent<ShopRowWidget>();
    }

    // ---------- tutor ----------

    private static GameObject ArmarTutor()
    {
        Sprite fondo = MenuUiKit.Sprite(Reminder + "bg.png");
        Sprite cursor = MenuUiKit.Sprite(Reminder + "cursor.png");
        if (fondo == null || cursor == null)
        {
            Debug.LogError($"[Lobby] Falta el arte de {Reminder}. No se genero el tutor.");
            return null;
        }
        // Se usan como textura (RawImage con uvRect): traen varios cuadros.
        MenuUiKit.Sprite(Reminder + "buttons.png");
        MenuUiKit.Sprite(Categorias);

        TutorMoveRowWidget fila = ArmarFilaDelTutor(cursor);

        var root = new GameObject("MoveTutorScreen", typeof(RectTransform));
        MenuUiKit.Full(root);
        var screen = root.AddComponent<MoveTutorScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.MoveTutor);

        // Telon a pantalla completa, del azul del fondo: en 16:9 sobran franjas.
        GameObject telon = MenuUiKit.Node("Telon", root.transform);
        MenuUiKit.Full(telon);
        MenuUiKit.AddImage(telon, null, Telon);

        Transform m = Nodo("Marco", root.transform, Vector2.zero,
            new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));
        GameObject bg = MenuUiKit.Node("Fondo", m);
        MenuUiKit.Full(bg);
        MenuUiKit.AddImage(bg, fondo, Color.white);

        Text titulo = Texto("Titulo", m, P(Titulo), Titulo.size, "MT DE NÉSTOR", 16, TextAnchor.MiddleLeft, Color.white);
        MenuUiKit.Contorno(titulo);

        GameObject arriba = Flecha("FlechaArriba", m, FlechaArriba, mitadDerecha: true);
        GameObject abajo = Flecha("FlechaAbajo", m, FlechaAbajo, mitadDerecha: false);

        RectTransform lista = Nodo("Lista", m, P(ListaTutor), ListaTutor.size);
        var layout = MenuUiKit.VerticalList(lista.gameObject, 0f, new RectOffset(0, 0, 0, 0));
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;

        Text pie = Texto("Pie", m, P(Pie), Pie.size, "$ 0", 16, TextAnchor.MiddleLeft, Color.white);
        MenuUiKit.Contorno(pie);

        Text vacio = Texto("Vacio", m, P(Vacio), Vacio.size, "", 15, TextAnchor.MiddleCenter, Color.white);
        vacio.horizontalOverflow = HorizontalWrapMode.Wrap;
        MenuUiKit.Contorno(vacio);
        vacio.enabled = false;

        // ---- el Pokemon ----
        Image iconoPoken = Imagen("IconoPoken", m, P(IconoPoken), IconoPoken.size, null);
        iconoPoken.preserveAspect = true;
        Text nombrePoken = Texto("NombrePoken", m, P(NombrePoken), NombrePoken.size, "", 16, TextAnchor.MiddleLeft, Tinta);
        Image tipo1 = Imagen("Tipo1", m, P(Tipo1), Tipo1.size, null);
        tipo1.preserveAspect = true;
        Image tipo2 = Imagen("Tipo2", m, P(Tipo2), Tipo2.size, null);
        tipo2.preserveAspect = true;

        // ---- el movimiento ----
        string[] etiquetas = { "CATEGORÍA", "POTENCIA", "PRECISIÓN", "PP" };
        var valores = new Text[etiquetas.Length];
        for (int i = 0; i < etiquetas.Length; i++)
        {
            Texto("Etiqueta" + i, m, P(Renglones[i]), Renglones[i].size, etiquetas[i], 15, TextAnchor.MiddleLeft, Tinta);
            valores[i] = Texto("Valor" + i, m, P(Renglones[i]), Renglones[i].size - new Vector2(8f, 0f), "",
                15, TextAnchor.MiddleRight, Tinta);
        }

        RectTransform categoria = Nodo("Categoria", m, P(IconoCategoria), IconoCategoria.size);
        var raw = categoria.gameObject.AddComponent<RawImage>();
        raw.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Categorias);
        raw.uvRect = new Rect(0f, 2f / 3f, 1f, 1f / 3f);
        raw.raycastTarget = false;

        Text descripcion = Texto("Descripcion", m, P(DescripcionTutor), DescripcionTutor.size, "", 15,
            TextAnchor.UpperLeft, Tinta);
        descripcion.horizontalOverflow = HorizontalWrapMode.Wrap;

        MenuUiKit.SetField(screen, "rowPrefab", fila);
        MenuUiKit.SetField(screen, "rowContainer", lista);
        MenuUiKit.SetField(screen, "visibleRows", Mathf.FloorToInt(ListaTutor.height / FilaTutor));
        MenuUiKit.SetField(screen, "arrowUp", arriba);
        MenuUiKit.SetField(screen, "arrowDown", abajo);
        MenuUiKit.SetField(screen, "titleText", titulo);
        MenuUiKit.SetField(screen, "footerText", pie);
        MenuUiKit.SetField(screen, "emptyText", vacio);
        MenuUiKit.SetField(screen, "pokenIcon", iconoPoken);
        MenuUiKit.SetField(screen, "pokenNameText", nombrePoken);
        MenuUiKit.SetField(screen, "pokenType1", tipo1);
        MenuUiKit.SetField(screen, "pokenType2", tipo2);
        MenuUiKit.SetField(screen, "categoryIcon", raw);
        MenuUiKit.SetField(screen, "powerText", valores[1]);
        MenuUiKit.SetField(screen, "accuracyText", valores[2]);
        MenuUiKit.SetField(screen, "ppText", valores[3]);
        MenuUiKit.SetField(screen, "descriptionText", descripcion);
        // El renglon de la categoria no lleva texto: va el icono.
        Object.DestroyImmediate(valores[0].gameObject);

        return MenuUiKit.SavePrefab(root, TutorPath);
    }

    private static TutorMoveRowWidget ArmarFilaDelTutor(Sprite cursor)
    {
        var root = new GameObject("TutorMoveRow", typeof(RectTransform));
        MenuUiKit.Box(root, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
            new Vector2(ListaTutor.width, FilaTutor));

        GameObject marco = MenuUiKit.Node("Cursor", root.transform);
        MenuUiKit.Full(marco);
        MenuUiKit.AddImage(marco, cursor, Color.white);
        marco.SetActive(false);

        Text nombre = MenuUiKit.Label("Nombre", root.transform, new Vector2(14f, -4f),
            new Vector2(180f, 28f), "Movimiento", 16, TextAnchor.MiddleLeft, Color.white);
        MenuUiKit.Contorno(nombre);

        GameObject tipoGo = MenuUiKit.Node("Tipo", root.transform);
        MenuUiKit.Box(tipoGo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -34f), new Vector2(84f, 28f));
        Image tipo = MenuUiKit.AddImage(tipoGo, null, Color.white);
        tipo.preserveAspect = true;

        // El recuadro celeste del fondo va de x 112 a 238 de la fila.
        Text slot = MenuUiKit.Label("Recuadro", root.transform, new Vector2(116f, -34f),
            new Vector2(116f, 28f), "", 15, TextAnchor.MiddleRight, Tinta);

        Text nuevo = MenuUiKit.Label("Nuevo", root.transform, new Vector2(-12f, -4f),
            new Vector2(70f, 28f), "NUEVO", 13, TextAnchor.MiddleRight, MenuUiKit.Gold, new Vector2(1f, 1f));
        MenuUiKit.Contorno(nuevo);
        nuevo.gameObject.SetActive(false);

        var widget = root.AddComponent<TutorMoveRowWidget>();
        MenuUiKit.SetField(widget, "nameText", nombre);
        MenuUiKit.SetField(widget, "typeIcon", tipo);
        MenuUiKit.SetField(widget, "slotText", slot);
        MenuUiKit.SetField(widget, "newMark", nuevo.gameObject);
        MenuUiKit.SetArrayField(widget, "selectedObjects", new Object[] { marco });
        MenuUiKit.SetArrayField(widget, "tintedGraphics", new Object[] { nombre });
        MenuUiKit.SetField(widget, "normalColor", Color.white);
        MenuUiKit.SetField(widget, "selectedColor", MenuUiKit.Gold);
        MenuUiKit.SetField(widget, "disabledColor", new Color(0.6f, 0.65f, 0.75f, 1f));

        return MenuUiKit.SavePrefab(root, TutorRowPath).GetComponent<TutorMoveRowWidget>();
    }

    // Una flecha de buttons.png (20x40: la de abajo a la izquierda, la de
    // arriba a la derecha).
    private static GameObject Flecha(string nombre, Transform padre, Rect r, bool mitadDerecha)
    {
        RectTransform nodo = Nodo(nombre, padre, P(r), r.size);
        MenuUiKit.MediaTextura("Imagen", nodo, r.size, Reminder + "buttons.png", mitadDerecha);
        nodo.gameObject.SetActive(false);
        return nodo.gameObject;
    }

    // ---------- registro ----------

    private static void Registrar(params MenuScreen[] pantallas)
    {
        GameObject contenido = PrefabUtility.LoadPrefabContents(MenuSystemPath);
        if (contenido == null)
        {
            Debug.LogError($"[Lobby] No se encontro {MenuSystemPath}. Genera primero el sistema de menus.");
            return;
        }

        try
        {
            var manager = contenido.GetComponent<MenuManager>();
            if (manager == null) { Debug.LogError("[Lobby] El prefab MenuSystem no tiene MenuManager."); return; }

            var so = new SerializedObject(manager);
            SerializedProperty lista = so.FindProperty("screenPrefabs");

            foreach (MenuScreen pantalla in pantallas)
            {
                if (pantalla == null) continue;
                bool ya = false;
                for (int i = 0; i < lista.arraySize; i++)
                    if (lista.GetArrayElementAtIndex(i).objectReferenceValue == pantalla) { ya = true; break; }
                if (ya) continue;

                lista.InsertArrayElementAtIndex(lista.arraySize);
                lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = pantalla;
                Debug.Log($"[Lobby] MenuSystem: agregada la pantalla {pantalla.ScreenId}.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contenido, MenuSystemPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }

    // ---------- utilidades ----------

    // Como MenuUiKit.Sprite, pero con borde de 9-slice: la ventana y el marco
    // del cursor se estiran sin deformar las esquinas.
    private static Sprite SpriteConBorde(string path, int borde)
    {
        Sprite s = MenuUiKit.Sprite(path);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return s;

        var b = new Vector4(borde, borde, borde, borde);
        if (importer.spriteBorder == b) return s;

        importer.spriteBorder = b;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Rectangulo del arte (origen arriba a la izquierda) -> posicion anclada al
    // centro del padre de 512x384.
    private static Vector2 P(Rect r)
        => new Vector2(r.x + r.width * 0.5f - MenuUiKit.RefWidth * 0.5f,
                       MenuUiKit.RefHeight * 0.5f - (r.y + r.height * 0.5f));

    private static RectTransform Nodo(string nombre, Transform padre, Vector2 pos, Vector2 tam)
    {
        GameObject go = MenuUiKit.Node(nombre, padre);
        return MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, tam);
    }

    private static Image Imagen(string nombre, Transform padre, Vector2 pos, Vector2 tam, Sprite sprite)
    {
        RectTransform rt = Nodo(nombre, padre, pos, tam);
        return MenuUiKit.AddImage(rt.gameObject, sprite, Color.white);
    }

    private static Text Texto(string nombre, Transform padre, Vector2 pos, Vector2 tam, string contenido,
                              int tamanio, TextAnchor anclaje, Color color)
    {
        RectTransform rt = Nodo(nombre, padre, pos, tam);
        return MenuUiKit.AddText(rt.gameObject, contenido, tamanio, anclaje, color);
    }
}
