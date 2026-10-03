using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Genera TODOS los prefabs del sistema de menus de una sola pasada.
//
// Es un punto de partida, no una jaula: lo que sale son prefabs comunes que se
// editan en Unity como cualquier otro (mover, cambiar sprites, tocar colores).
// Volver a correrlo los REEMPLAZA, asi que si el equipo ya los retoco a mano,
// no correrlo de nuevo.
public static class MenuPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI/Menus";
    private const string WidgetFolder = "Assets/Prefabs/UI/Menus/Widgets";
    private const string ResourcesFolder = "Assets/Resources/Menus";

    private const string TypeSheet = "Assets/Sprites/UI/Battle/icon_types.png";
    private const string StatusSheet = "Assets/Sprites/UI/Battle/icon_statuses.png";

    [MenuItem("PokeRush/UI/Construir sistema de menus")]
    public static void BuildAll()
    {
        MenuUiKit.EnsureFolder(PrefabFolder);
        MenuUiKit.EnsureFolder(WidgetFolder);
        MenuUiKit.EnsureFolder(ResourcesFolder);

        BuildIconLibrary();
        ItemIconAssigner.Asignar();

        GameObject option = BuildMenuOption();
        GameObject itemRow = BuildItemRow();
        GameObject partyPanel = BuildPartyPanel();
        GameObject moveRow = BuildMoveRow();
        GameObject optionRow = BuildOptionRow();

        var screens = new List<GameObject>
        {
            BuildPauseScreen(option),
            BuildBagScreen(itemRow, option),
            BuildPartyScreen(partyPanel, option),
            BuildSummaryScreen(moveRow),
            BuildOptionsScreen(optionRow),
            BuildMessageBoxScreen(option)
        };

        BuildMenuSystem(screens);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Menus] Prefabs generados en " + PrefabFolder +
                  " y MenuSystem en " + ResourcesFolder + ".");
    }

    // Solo la biblioteca de iconos, sin rehacer los prefabs de las pantallas
    // (que el equipo puede haber retocado a mano).
    [MenuItem("PokeRush/UI/Rehacer iconos de tipos y estados")]
    public static void RehacerIconos()
    {
        MenuUiKit.EnsureFolder(ResourcesFolder);
        BuildIconLibrary();
        AssetDatabase.SaveAssets();
        Debug.Log("[Menus] Iconos de tipos y estados rehechos en " + ResourcesFolder + "/MenuIcons.asset.");
    }

    // ---------------------------------------------------------------- iconos

    private static void BuildIconLibrary()
    {
        string path = ResourcesFolder + "/MenuIcons.asset";
        var library = AssetDatabase.LoadAssetAtPath<MenuIconLibrary>(path);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<MenuIconLibrary>();
            AssetDatabase.CreateAsset(library, path);
        }

        // Las hojas ya vienen cortadas para el HUD de combate: se reusan tal
        // cual en vez de recortar otras iguales.
        //
        // OJO: las filas de las hojas estan en el orden de Essentials (Normal,
        // Lucha, Volador... / Dormido, Envenenado...), NO en el de los enums. El
        // indice de la biblioteca es el enum y la fila la da el mismo mapa que
        // usa el HUD de combate. Antes se tomaba fila = enum y el Resumen
        // mostraba Psiquico como Agua (y el resto de los tipos corridos).
        var types = new Sprite[System.Enum.GetValues(typeof(PokenType)).Length];
        foreach (PokenType tipo in System.Enum.GetValues(typeof(PokenType)))
        {
            if (tipo == PokenType.None) continue;
            if (PokeRush.EditorTools.BattleUiAssetImporter.TypeRow.TryGetValue(tipo, out int fila))
                types[(int)tipo] = MenuUiKit.SubSprite(TypeSheet, $"type_{fila}_0");
        }

        // None y Confundido no tienen icono en la hoja.
        var statuses = new Sprite[System.Enum.GetValues(typeof(StatusEffect)).Length];
        foreach (StatusEffect estado in System.Enum.GetValues(typeof(StatusEffect)))
        {
            if (PokeRush.EditorTools.BattleUiAssetImporter.StatusRow.TryGetValue(estado, out int fila))
                statuses[(int)estado] = MenuUiKit.SubSprite(StatusSheet, $"status_{fila}_0");
        }

        library.typeIcons = types;
        library.statusIcons = statuses;
        EditorUtility.SetDirty(library);
    }

    // --------------------------------------------------------------- widgets

    private static GameObject BuildMenuOption()
    {
        GameObject root = new GameObject("MenuOption", typeof(RectTransform));
        MenuUiKit.Box((GameObject)root, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(132f, 26f));

        Image fondo = Fill(root, "Fondo", MenuUiKit.Panel);
        Image resaltado = Fill(root, "Resaltado", new Color(0.98f, 0.66f, 0.18f, 0.35f));

        GameObject iconGo = MenuUiKit.Node("Icono", root.transform);
        MenuUiKit.Box(iconGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(20f, 20f));
        Image icono = MenuUiKit.AddImage(iconGo, null, Color.white);
        icono.preserveAspect = true;

        Text texto = MenuUiKit.Label("Texto", root.transform, new Vector2(40f, -4f),
            new Vector2(86f, 18f), "OPCION", 12, TextAnchor.MiddleLeft, MenuUiKit.Ink);

        var widget = root.AddComponent<MenuOptionWidget>();
        MenuUiKit.SetField(widget, "label", texto);
        MenuUiKit.SetField(widget, "icon", icono);
        MenuUiKit.SetField(widget, "backgroundImage", fondo);
        MenuUiKit.SetArrayField(widget, "selectedObjects", new Object[] { resaltado.gameObject });
        MenuUiKit.SetArrayField(widget, "tintedGraphics", new Object[] { texto });
        MenuUiKit.SetField(widget, "normalColor", MenuUiKit.Ink);
        MenuUiKit.SetField(widget, "selectedColor", MenuUiKit.Accent);
        resaltado.gameObject.SetActive(false);

        return MenuUiKit.SavePrefab(root, WidgetFolder + "/MenuOption.prefab");
    }

    // La fila de la mochila va DENTRO de la ventana blanca del fondo, asi que
    // el texto es oscuro y el resaltado es un azul translucido, no un bloque.
    private static GameObject BuildItemRow()
    {
        GameObject root = new GameObject("ItemRow", typeof(RectTransform));
        MenuUiKit.Box(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(244f, 24f));

        Image resaltado = Fill(root, "Resaltado", new Color(0.16f, 0.45f, 0.85f, 0.28f));
        resaltado.sprite = MenuUiKit.WhitePixel;

        GameObject iconGo = MenuUiKit.Node("Icono", root.transform);
        MenuUiKit.Box(iconGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(14f, 0f), new Vector2(22f, 22f));
        Image icono = MenuUiKit.AddImage(iconGo, null, Color.white);
        icono.preserveAspect = true;
        icono.enabled = false;

        Text nombre = MenuUiKit.Label("Nombre", root.transform, new Vector2(44f, -2f),
            new Vector2(146f, 20f), "Objeto", 13, TextAnchor.MiddleLeft, MenuUiKit.InkDark);
        Text cantidad = MenuUiKit.Label("Cantidad", root.transform, new Vector2(-8f, -2f),
            new Vector2(60f, 20f), "x 1", 13, TextAnchor.MiddleRight, MenuUiKit.InkDark,
            new Vector2(1f, 1f));

        var widget = root.AddComponent<ItemRowWidget>();
        MenuUiKit.SetField(widget, "nameText", nombre);
        MenuUiKit.SetField(widget, "quantityText", cantidad);
        MenuUiKit.SetField(widget, "icon", icono);
        MenuUiKit.SetArrayField(widget, "selectedObjects", new Object[] { resaltado.gameObject });
        MenuUiKit.SetArrayField(widget, "tintedGraphics", new Object[] { nombre, cantidad });
        MenuUiKit.SetField(widget, "normalColor", MenuUiKit.InkDark);
        MenuUiKit.SetField(widget, "selectedColor", new Color(0.05f, 0.24f, 0.60f, 1f));
        resaltado.gameObject.SetActive(false);

        return MenuUiKit.SavePrefab(root, WidgetFolder + "/ItemRow.prefab");
    }

    private static GameObject BuildPartyPanel()
    {
        Sprite normal = MenuUiKit.Sprite("Assets/Graphics/UI/Party/panel_rect.png");
        Sprite selected = MenuUiKit.Sprite("Assets/Graphics/UI/Party/panel_rect_sel.png");

        GameObject root = new GameObject("PartyPanel", typeof(RectTransform));
        MenuUiKit.Box(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(232f, 56f));

        Image fondo = Fill(root, "Fondo", Color.white);
        fondo.sprite = normal;
        if (normal == null) fondo.color = MenuUiKit.PanelLight;

        GameObject spriteGo = MenuUiKit.Node("Sprite", root.transform);
        MenuUiKit.Box(spriteGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(24f, 0f), new Vector2(40f, 40f));
        Image pokenSprite = MenuUiKit.AddImage(spriteGo, null, Color.white);
        pokenSprite.preserveAspect = true;

        Text nombre = MenuUiKit.Label("Nombre", root.transform, new Vector2(50f, -6f),
            new Vector2(110f, 16f), "Poken", 12, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        Text nivel = MenuUiKit.Label("Nivel", root.transform, new Vector2(50f, -38f),
            new Vector2(46f, 14f), "Nv.5", 10, TextAnchor.MiddleLeft, MenuUiKit.Ink);

        Image ps = MenuUiKit.FillBar("BarraPS", root.transform, new Vector2(96f, -26f),
            new Vector2(96f, 8f), new Color(0.09f, 0.11f, 0.15f, 1f),
            new Color(0.11f, 0.79f, 0.29f, 1f));

        Text psTexto = MenuUiKit.Label("TextoPS", root.transform, new Vector2(-10f, -36f),
            new Vector2(80f, 14f), "0/ 0", 10, TextAnchor.MiddleRight, MenuUiKit.Ink,
            new Vector2(1f, 1f));

        GameObject estadoGo = MenuUiKit.Node("Estado", root.transform);
        MenuUiKit.Box(estadoGo, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-8f, -6f), new Vector2(22f, 10f));
        Image estado = MenuUiKit.AddImage(estadoGo, null, Color.white);
        estado.preserveAspect = true;

        Image lider = MenuUiKit.Rect("Lider", root.transform, new Vector2(6f, -6f),
            new Vector2(10f, 10f), MenuUiKit.Accent);
        Image swap = MenuUiKit.Rect("Intercambio", root.transform, new Vector2(6f, 6f),
            new Vector2(10f, 10f), new Color(0.35f, 0.75f, 1f, 1f), null, new Vector2(0f, 0f));

        var widget = root.AddComponent<PartyPanelWidget>();
        MenuUiKit.SetField(widget, "pokenSprite", pokenSprite);
        MenuUiKit.SetField(widget, "nameText", nombre);
        MenuUiKit.SetField(widget, "levelText", nivel);
        MenuUiKit.SetField(widget, "hpText", psTexto);
        MenuUiKit.SetField(widget, "hpFill", ps);
        MenuUiKit.SetField(widget, "statusIcon", estado);
        MenuUiKit.SetField(widget, "leaderMark", lider.gameObject);
        MenuUiKit.SetField(widget, "swapMark", swap.gameObject);
        MenuUiKit.SetField(widget, "backgroundImage", fondo);
        MenuUiKit.SetField(widget, "normalSprite", normal);
        MenuUiKit.SetField(widget, "selectedSprite", selected);
        MenuUiKit.SetArrayField(widget, "tintedGraphics", new Object[] { nombre, nivel, psTexto });
        MenuUiKit.SetField(widget, "normalColor", MenuUiKit.Ink);
        MenuUiKit.SetField(widget, "selectedColor", Color.white);

        lider.gameObject.SetActive(false);
        swap.gameObject.SetActive(false);

        return MenuUiKit.SavePrefab(root, WidgetFolder + "/PartyPanel.prefab");
    }

    // La fila de movimiento calza con los huecos que bg_moves.png ya dibuja:
    // 64 px de alto (una fila cada 64), el icono de tipo y el nombre arriba, y
    // los PP sobre la barra clara de la derecha.
    private static GameObject BuildMoveRow()
    {
        GameObject root = new GameObject("MoveRow", typeof(RectTransform));
        MenuUiKit.Box(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(260f, 64f));

        Image resaltado = Fill(root, "Resaltado", new Color(1f, 0.85f, 0.24f, 0.22f));
        resaltado.sprite = MenuUiKit.WhitePixel;

        GameObject tipoGo = MenuUiKit.Node("Tipo", root.transform);
        MenuUiKit.Box(tipoGo, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, -2f), new Vector2(52f, 20f));
        Image tipo = MenuUiKit.AddImage(tipoGo, null, Color.white);
        tipo.preserveAspect = true;

        Text nombre = MenuUiKit.Label("Nombre", root.transform, new Vector2(76f, -1f),
            new Vector2(182f, 22f), "Movimiento", 14, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        MenuUiKit.Contorno(nombre);

        Text pp = MenuUiKit.Label("PP", root.transform, new Vector2(114f, -32f),
            new Vector2(112f, 20f), "PP  0/0", 13, TextAnchor.MiddleRight, MenuUiKit.InkDark);

        Image ppFill = MenuUiKit.FillBar("BarraPP", root.transform, new Vector2(116f, -54f),
            new Vector2(112f, 5f), new Color(0.20f, 0.35f, 0.50f, 0.55f),
            new Color(0.20f, 0.55f, 0.90f, 1f));

        Image swap = MenuUiKit.Rect("Intercambio", root.transform, new Vector2(-4f, -2f),
            new Vector2(10f, 10f), new Color(0.35f, 0.75f, 1f, 1f), MenuUiKit.WhitePixel,
            new Vector2(1f, 1f));

        var widget = root.AddComponent<MoveRowWidget>();
        MenuUiKit.SetField(widget, "nameText", nombre);
        MenuUiKit.SetField(widget, "typeIcon", tipo);
        MenuUiKit.SetField(widget, "ppText", pp);
        MenuUiKit.SetField(widget, "ppFill", ppFill);
        MenuUiKit.SetField(widget, "swapMark", swap.gameObject);
        MenuUiKit.SetArrayField(widget, "selectedObjects", new Object[] { resaltado.gameObject });
        MenuUiKit.SetArrayField(widget, "tintedGraphics", new Object[] { nombre });
        MenuUiKit.SetField(widget, "normalColor", MenuUiKit.Ink);
        MenuUiKit.SetField(widget, "selectedColor", MenuUiKit.Gold);

        resaltado.gameObject.SetActive(false);
        swap.gameObject.SetActive(false);

        return MenuUiKit.SavePrefab(root, WidgetFolder + "/MoveRow.prefab");
    }

    private static GameObject BuildOptionRow()
    {
        GameObject root = new GameObject("OptionRow", typeof(RectTransform));
        MenuUiKit.Box(root, new Vector2(0f, 1f), new Vector2(0f, 1f),
            Vector2.zero, new Vector2(340f, 24f));

        Image resaltado = Fill(root, "Resaltado", new Color(0.98f, 0.66f, 0.18f, 0.30f));

        Text etiqueta = MenuUiKit.Label("Etiqueta", root.transform, new Vector2(12f, -3f),
            new Vector2(170f, 18f), "Opcion", 12, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        Text valor = MenuUiKit.Label("Valor", root.transform, new Vector2(-14f, -3f),
            new Vector2(120f, 18f), "", 12, TextAnchor.MiddleRight, MenuUiKit.Ink,
            new Vector2(1f, 1f));

        Image barra = MenuUiKit.FillBar("Barra", root.transform, new Vector2(-14f, -18f),
            new Vector2(120f, 4f), new Color(0.09f, 0.11f, 0.15f, 1f), MenuUiKit.Accent,
            new Vector2(1f, 1f));

        Image flechas = MenuUiKit.Rect("Flechas", root.transform, new Vector2(-140f, -3f),
            new Vector2(8f, 14f), MenuUiKit.Border, null, new Vector2(1f, 1f));
        Image header = MenuUiKit.Rect("MarcaTitulo", root.transform, new Vector2(4f, -3f),
            new Vector2(4f, 16f), MenuUiKit.Accent);

        var widget = root.AddComponent<OptionRowWidget>();
        MenuUiKit.SetField(widget, "labelText", etiqueta);
        MenuUiKit.SetField(widget, "valueText", valor);
        MenuUiKit.SetField(widget, "valueFill", barra);
        MenuUiKit.SetField(widget, "valueBar", barra.transform.parent.gameObject);
        MenuUiKit.SetField(widget, "arrows", flechas.gameObject);
        MenuUiKit.SetField(widget, "headerMark", header.gameObject);
        MenuUiKit.SetArrayField(widget, "selectedObjects", new Object[] { resaltado.gameObject });
        MenuUiKit.SetArrayField(widget, "tintedGraphics", new Object[] { etiqueta, valor });
        MenuUiKit.SetField(widget, "normalColor", MenuUiKit.Ink);
        MenuUiKit.SetField(widget, "selectedColor", MenuUiKit.Accent);

        resaltado.gameObject.SetActive(false);
        header.gameObject.SetActive(false);

        return MenuUiKit.SavePrefab(root, WidgetFolder + "/OptionRow.prefab");
    }

    // -------------------------------------------------------------- pantallas

    private static GameObject BuildPauseScreen(GameObject optionPrefab)
    {
        GameObject root = ScreenRoot("PauseMenuScreen");
        var screen = root.AddComponent<PauseMenuScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Pause);

        GameObject panel = MenuUiKit.Node("Panel", root.transform);
        MenuUiKit.Box(panel, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-10f, -10f), new Vector2(148f, 200f));
        MenuUiKit.AddImage(panel, null, MenuUiKit.Panel);
        Borde(panel);

        GameObject lista = MenuUiKit.Node("Opciones", panel.transform);
        MenuUiKit.Stretch(lista, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        MenuUiKit.VerticalList(lista, 4f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);

        (string id, string label, string icon)[] opciones =
        {
            (PauseMenuScreen.IdPokemon, "Pokemon", "menuPokemon"),
            (PauseMenuScreen.IdBag, "Bolsa", "menuBag"),
            (PauseMenuScreen.IdSave, "Guardar", "menuSave"),
            (PauseMenuScreen.IdOptions, "Opciones", "menuOptions"),
            (PauseMenuScreen.IdExit, "Salir", "menuQuit")
        };

        foreach ((string id, string label, string icon) o in opciones)
        {
            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(optionPrefab, lista.transform);
            instancia.name = "Opcion_" + o.id;
            ConfigurarOpcion(instancia, o.id, o.label,
                MenuUiKit.Sprite($"Assets/Graphics/Icons/{o.icon}.png"));
        }

        MenuUiKit.SetField(screen, "optionContainer", lista.transform);
        return MenuUiKit.SavePrefab(root, PrefabFolder + "/PauseMenuScreen.prefab");
    }

    // La mochila se arma DENTRO del fondo del pack, no encima.
    //
    // Las medidas salen del propio PNG (512x384): la ventana blanca de los
    // objetos esta en x 190..460 / y 8..272, el recuadro del icono de la
    // descripcion en x 20..76 / y 308..364, y la fila de bolsillos en y 228.
    // Por eso aca no se dibuja ni un panel propio: solo se pone contenido en
    // los huecos que el arte ya tiene.
    private static GameObject BuildBagScreen(GameObject itemRowPrefab, GameObject optionPrefab)
    {
        GameObject root = ScreenRoot("BagScreen");
        var screen = root.AddComponent<BagScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Bag);

        Transform marco = Marco(root, "Assets/Graphics/UI/Bag/bg_1.png");
        Image fondo = marco.Find("Fondo").GetComponent<Image>();

        // Mochila grande, en el hueco rosa de la izquierda.
        GameObject bolsaGo = MenuUiKit.Node("Bolsa", marco);
        MenuUiKit.Box(bolsaGo, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(28f, -22f), new Vector2(134f, 134f));
        Image bolsa = MenuUiKit.AddImage(bolsaGo, MenuUiKit.Sprite("Assets/Graphics/UI/Bag/bag_1.png"),
            Color.white);
        bolsa.preserveAspect = true;

        // Nombre del bolsillo: va sobre la barra clara que el fondo ya trae.
        Text bolsillo = MenuUiKit.AddText(
            MenuUiKit.Node("Bolsillo", marco), "Medicinas", 15,
            TextAnchor.MiddleCenter, MenuUiKit.InkDark);
        MenuUiKit.Box(bolsillo.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(8f, -176f), new Vector2(178f, 22f));

        Text dinero = MenuUiKit.Label("Dinero", marco, new Vector2(10f, -202f),
            new Vector2(176f, 20f), "$ 0", 13, TextAnchor.MiddleCenter, MenuUiKit.Ink);
        MenuUiKit.Contorno(dinero);

        // Lista dentro de la ventana blanca. Texto oscuro: el fondo es blanco.
        GameObject lista = MenuUiKit.Node("Lista", marco);
        MenuUiKit.Box(lista, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(202f, -22f), new Vector2(248f, 240f));
        MenuUiKit.VerticalList(lista, 2f, new RectOffset(0, 0, 0, 0));

        Text vacio = MenuUiKit.Label("Vacio", marco, new Vector2(202f, -130f),
            new Vector2(248f, 22f), "No hay objetos.", 13, TextAnchor.MiddleCenter,
            new Color(0.45f, 0.45f, 0.50f, 1f));

        // Descripcion: texto en la franja gris de abajo, icono en el recuadro
        // blanco que ya esta dibujado.
        GameObject descIconGo = MenuUiKit.Node("IconoDescripcion", marco);
        MenuUiKit.Box(descIconGo, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(26f, -314f), new Vector2(44f, 44f));
        Image descIcono = MenuUiKit.AddImage(descIconGo, null, Color.white);
        descIcono.preserveAspect = true;

        Text descripcion = MenuUiKit.Label("Descripcion", marco, new Vector2(92f, -304f),
            new Vector2(404f, 68f), "", 13, TextAnchor.UpperLeft, MenuUiKit.InkDark);
        descripcion.horizontalOverflow = HorizontalWrapMode.Wrap;

        // Submenu del objeto: esto SI es un panel propio, porque es un popup y
        // el fondo no tiene ningun hueco para el.
        GameObject submenu = MenuUiKit.Node("Submenu", marco);
        MenuUiKit.Box(submenu, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(310f, -190f), new Vector2(136f, 74f));
        MenuUiKit.AddImage(submenu, null, MenuUiKit.Panel);
        Borde(submenu);

        GameObject acciones = MenuUiKit.Node("Acciones", submenu.transform);
        MenuUiKit.Stretch(acciones, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        MenuUiKit.VerticalList(acciones, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        submenu.SetActive(false);

        MenuUiKit.SetField(screen, "itemRowPrefab", itemRowPrefab.GetComponent<ItemRowWidget>());
        MenuUiKit.SetField(screen, "itemContainer", lista.transform);
        MenuUiKit.SetField(screen, "visibleRows", 9);
        MenuUiKit.SetField(screen, "pocketNameText", bolsillo);
        MenuUiKit.SetField(screen, "bagImage", bolsa);
        MenuUiKit.SetField(screen, "backgroundImage", fondo);
        MenuUiKit.SetField(screen, "descriptionText", descripcion);
        MenuUiKit.SetField(screen, "descriptionIcon", descIcono);
        MenuUiKit.SetField(screen, "emptyText", vacio);
        MenuUiKit.SetField(screen, "moneyText", dinero);
        MenuUiKit.SetField(screen, "actionMenuRoot", submenu);
        MenuUiKit.SetField(screen, "actionOptionPrefab", optionPrefab.GetComponent<MenuOptionWidget>());
        MenuUiKit.SetField(screen, "actionContainer", acciones.transform);

        MenuUiKit.SetArrayField(screen, "bagSpritesByPocket", new Object[]
        {
            MenuUiKit.Sprite("Assets/Graphics/UI/Bag/bag_1.png"),
            MenuUiKit.Sprite("Assets/Graphics/UI/Bag/bag_3.png"),
            MenuUiKit.Sprite("Assets/Graphics/UI/Bag/bag_8.png")
        });
        MenuUiKit.SetArrayField(screen, "backgroundsByPocket", new Object[]
        {
            MenuUiKit.Sprite("Assets/Graphics/UI/Bag/bg_1.png"),
            MenuUiKit.Sprite("Assets/Graphics/UI/Bag/bg_3.png"),
            MenuUiKit.Sprite("Assets/Graphics/UI/Bag/bg_8.png")
        });

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/BagScreen.prefab");
    }

    private static GameObject BuildPartyScreen(GameObject panelPrefab, GameObject optionPrefab)
    {
        GameObject root = ScreenRoot("PartyScreen");
        var screen = root.AddComponent<PartyScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Party);

        Transform marco = Marco(root, "Assets/Graphics/UI/Party/bg.png");

        GameObject paneles = MenuUiKit.Node("Paneles", marco);
        MenuUiKit.Stretch(paneles, Vector2.zero, Vector2.one,
            new Vector2(8f, 46f), new Vector2(-8f, -8f));
        MenuUiKit.Grid(paneles, new Vector2(232f, 56f), new Vector2(8f, 6f), 2,
            new RectOffset(0, 0, 0, 0));

        Text prompt = MenuUiKit.Label("Mensaje", marco, new Vector2(10f, 10f),
            new Vector2(360f, 26f), "Elegí un Pokémon.", 13, TextAnchor.MiddleLeft, MenuUiKit.Ink,
            new Vector2(0f, 0f));

        var cancelar = (GameObject)PrefabUtility.InstantiatePrefab(optionPrefab, marco);
        cancelar.name = "Cancelar";
        MenuOptionWidget cancelarWidget = ConfigurarOpcion(cancelar, "cancel", "SALIR", null);
        ConfigurarBotonSalir(cancelar);

        // Submenu del Poken
        GameObject submenu = MenuUiKit.Node("Submenu", marco);
        MenuUiKit.Box(submenu, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-10f, 40f), new Vector2(148f, 100f));
        MenuUiKit.AddImage(submenu, null, MenuUiKit.PanelLight);
        Borde(submenu);

        GameObject acciones = MenuUiKit.Node("Acciones", submenu.transform);
        MenuUiKit.Stretch(acciones, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        MenuUiKit.VerticalList(acciones, 2f, new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
        submenu.SetActive(false);

        MenuUiKit.SetField(screen, "panelPrefab", panelPrefab.GetComponent<PartyPanelWidget>());
        MenuUiKit.SetField(screen, "panelContainer", paneles.transform);
        MenuUiKit.SetField(screen, "columns", 2);
        MenuUiKit.SetField(screen, "promptText", prompt);
        MenuUiKit.SetField(screen, "cancelOption", cancelarWidget);
        MenuUiKit.SetField(screen, "actionMenuRoot", submenu);
        MenuUiKit.SetField(screen, "actionOptionPrefab", optionPrefab.GetComponent<MenuOptionWidget>());
        MenuUiKit.SetField(screen, "actionContainer", acciones.transform);

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/PartyScreen.prefab");
    }

    // El resumen usa UN FONDO POR PAGINA (bg_info / bg_memo / bg_skills /
    // bg_moves), que es como viene el pack y como se ven las referencias. No se
    // dibuja ningun panel propio: cada dato va en el hueco que el arte ya tiene.
    //
    // Las coordenadas salen de medir los PNG (512x384): panel izquierdo en
    // x 4..212, panel derecho en x 216..508, la barra de EXP en x 353..505 /
    // y 203..215, y las cuatro filas de movimientos cada 64 px desde y 104.
    private static GameObject BuildSummaryScreen(GameObject moveRowPrefab)
    {
        GameObject root = ScreenRoot("SummaryScreen");
        var screen = root.AddComponent<SummaryScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Summary);

        Transform marco = Marco(root, null);

        // --- paginas (cada una con su propio fondo) ---
        GameObject paginas = MenuUiKit.Node("Paginas", marco);
        MenuUiKit.Full(paginas);

        GameObject pagInfo = PaginaConFondo("PagInformacion", paginas.transform, "bg_info");
        GameObject pagNotas = PaginaConFondo("PagNotas", paginas.transform, "bg_memo");
        GameObject pagStats = PaginaConFondo("PagEstadisticas", paginas.transform, "bg_skills");
        GameObject pagMovs = PaginaConFondo("PagMovimientos", paginas.transform, "bg_moves");

        // --- Informacion ---
        Text especie = CampoResumen(pagInfo, "Especie", "Especie", 44f);
        CampoEtiqueta(pagInfo, "TipoEtiqueta", "Tipo", 74f);
        GameObject tipoFila = MenuUiKit.Node("Tipos", pagInfo.transform);
        MenuUiKit.Box(tipoFila, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(352f, -74f), new Vector2(150f, 22f));
        var tipoLayout = tipoFila.AddComponent<HorizontalLayoutGroup>();
        tipoLayout.spacing = 4f;
        tipoLayout.childAlignment = TextAnchor.MiddleLeft;
        tipoLayout.childControlWidth = false;
        tipoLayout.childControlHeight = false;
        Image tipo1 = TipoIcono(tipoFila.transform, "Tipo1");
        Image tipo2 = TipoIcono(tipoFila.transform, "Tipo2");

        Text entrenador = CampoResumen(pagInfo, "EO", "EO", 104f);
        Text idNo = CampoResumen(pagInfo, "ID", "ID No.", 134f);
        Text exp = CampoResumen(pagInfo, "Exp", "Puntos Exp.", 166f, TextAnchor.MiddleRight);
        Image expFill = MenuUiKit.FillBar("BarraExp", pagInfo.transform, new Vector2(355f, -204f),
            new Vector2(148f, 9f), new Color(0.10f, 0.25f, 0.40f, 0.6f),
            new Color(0.35f, 0.85f, 1f, 1f));
        Text expSiguiente = CampoResumen(pagInfo, "SiguienteNv", "Siguiente Nv.", 216f,
            TextAnchor.MiddleRight);

        CampoEtiqueta(pagInfo, "ObjetoEtiqueta", "Objeto Equipado", 250f, 200f);
        Text objeto = MenuUiKit.Label("Objeto", pagInfo.transform, new Vector2(228f, -282f),
            new Vector2(270f, 24f), "Ninguno", 14, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        MenuUiKit.Contorno(objeto);

        // --- Notas ---
        CampoEtiqueta(pagNotas, "EncuentroEtiqueta", "Encuentro", 44f, 200f);
        Text encuentro = MenuUiKit.Label("Encuentro", pagNotas.transform, new Vector2(238f, -78f),
            new Vector2(262f, 100f), "", 12, TextAnchor.UpperLeft, MenuUiKit.Ink);
        encuentro.horizontalOverflow = HorizontalWrapMode.Wrap;
        MenuUiKit.Contorno(encuentro);

        CampoEtiqueta(pagNotas, "CaracterEtiqueta", "Características", 204f, 200f);
        Text caracteristica = MenuUiKit.Label("Caracteristica", pagNotas.transform,
            new Vector2(238f, -238f), new Vector2(262f, 90f), "", 12, TextAnchor.UpperLeft,
            MenuUiKit.Ink);
        caracteristica.horizontalOverflow = HorizontalWrapMode.Wrap;
        MenuUiKit.Contorno(caracteristica);

        // --- Estadisticas ---
        Text ps = StatResumen(pagStats, "PS", "PS", 230f, 42f, 200f, TextAnchor.MiddleCenter);
        Text ataque = StatResumen(pagStats, "Ataque", "Ataque", 222f, 126f, 104f, TextAnchor.MiddleLeft);
        Text defensa = StatResumen(pagStats, "Defensa", "Defensa", 222f, 194f, 104f, TextAnchor.MiddleLeft);
        Text atEsp = StatResumen(pagStats, "AtEsp", "At. Esp.", 398f, 126f, 104f, TextAnchor.MiddleRight);
        Text defEsp = StatResumen(pagStats, "DefEsp", "Def. Esp.", 398f, 194f, 104f, TextAnchor.MiddleRight);
        Text velocidad = StatResumen(pagStats, "Velocidad", "Velocidad", 312f, 216f, 104f, TextAnchor.MiddleCenter);
        Image psFill = MenuUiKit.FillBar("BarraPS", pagStats.transform, new Vector2(268f, -92f),
            new Vector2(124f, 8f), new Color(0.10f, 0.25f, 0.40f, 0.6f),
            new Color(0.20f, 0.90f, 0.40f, 1f));

        CampoEtiqueta(pagStats, "HabilidadEtiqueta", "Habilidad", 276f, 100f);
        Text habilidad = MenuUiKit.Label("Habilidad", pagStats.transform, new Vector2(330f, -276f),
            new Vector2(174f, 22f), "", 14, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        MenuUiKit.Contorno(habilidad);
        Text habilidadDesc = MenuUiKit.Label("HabilidadDesc", pagStats.transform,
            new Vector2(228f, -300f), new Vector2(274f, 44f), "", 11, TextAnchor.UpperLeft,
            MenuUiKit.Ink);
        habilidadDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
        MenuUiKit.Contorno(habilidadDesc);

        // --- Movimientos ---
        GameObject movsLista = MenuUiKit.Node("Lista", pagMovs.transform);
        MenuUiKit.Box(movsLista, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(240f, -74f), new Vector2(260f, 256f));
        MenuUiKit.VerticalList(movsLista, 0f, new RectOffset(0, 0, 0, 0));
        Text pista = MenuUiKit.Label("Pista", pagMovs.transform, new Vector2(20f, -344f),
            new Vector2(470f, 20f), "", 12, TextAnchor.MiddleLeft, MenuUiKit.Gold);
        MenuUiKit.Contorno(pista);

        // --- comun a todas las paginas (va ENCIMA de los fondos) ---
        GameObject comun = MenuUiKit.Node("Comun", marco);
        MenuUiKit.Full(comun);

        Text titulo = MenuUiKit.Label("Titulo", comun.transform, new Vector2(22f, -2f),
            new Vector2(240f, 24f), "Información", 15, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        MenuUiKit.Contorno(titulo);

        GameObject spriteGo = MenuUiKit.Node("SpritePoken", comun.transform);
        MenuUiKit.Box(spriteGo, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(32f, -68f), new Vector2(152f, 152f));
        Image pokenSprite = MenuUiKit.AddImage(spriteGo, null, Color.white);
        pokenSprite.preserveAspect = true;

        Text nombre = MenuUiKit.Label("Nombre", comun.transform, new Vector2(16f, -258f),
            new Vector2(140f, 24f), "Poken", 14, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        MenuUiKit.Contorno(nombre);

        GameObject estadoGo = MenuUiKit.Node("Estado", comun.transform);
        MenuUiKit.Box(estadoGo, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(158f, -260f), new Vector2(44f, 18f));
        Image estado = MenuUiKit.AddImage(estadoGo, null, Color.white);
        estado.preserveAspect = true;

        Text nivel = MenuUiKit.Label("Nivel", comun.transform, new Vector2(42f, -298f),
            new Vector2(46f, 24f), "22", 14, TextAnchor.MiddleLeft, MenuUiKit.Ink);
        MenuUiKit.Contorno(nivel);

        // Pestanias arriba a la derecha.
        string[] tabSprites = { "page_info", "page_memo", "page_skills", "page_moves" };
        string[] tabTitles = { "Información", "Notas Entrenador", "Estadísticas", "Movimientos" };
        var tabs = new MenuWidget[4];
        GameObject tabsRow = MenuUiKit.Node("Pestanias", comun.transform);
        MenuUiKit.Box(tabsRow, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-6f, -1f), new Vector2(256f, 26f));
        var tabsLayout = tabsRow.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 4f;
        tabsLayout.childAlignment = TextAnchor.MiddleRight;
        tabsLayout.childControlWidth = false;
        tabsLayout.childControlHeight = false;

        for (int i = 0; i < tabs.Length; i++)
        {
            GameObject tabGo = MenuUiKit.Node("Tab_" + tabSprites[i], tabsRow.transform);
            MenuUiKit.Box(tabGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(28f, 28f));

            string tex = "Assets/Graphics/UI/Summary/" + tabSprites[i] + ".png";
            RawImage apagada = MenuUiKit.MediaTextura("Apagada", tabGo.transform,
                Vector2.zero, tex, false);
            RawImage encendida = MenuUiKit.MediaTextura("Encendida", tabGo.transform,
                Vector2.zero, tex, true);

            var tabWidget = tabGo.AddComponent<MenuWidget>();
            MenuUiKit.SetArrayField(tabWidget, "selectedObjects",
                new Object[] { encendida.gameObject });
            MenuUiKit.SetArrayField(tabWidget, "deselectedObjects",
                new Object[] { apagada.gameObject });
            encendida.gameObject.SetActive(false);
            tabs[i] = tabWidget;
        }

        // Referencias
        MenuUiKit.SetField(screen, "pageTitleText", titulo);
        MenuUiKit.SetField(screen, "pokenSprite", pokenSprite);
        MenuUiKit.SetField(screen, "nameText", nombre);
        MenuUiKit.SetField(screen, "levelText", nivel);
        MenuUiKit.SetField(screen, "statusIcon", estado);

        MenuUiKit.SetField(screen, "speciesText", especie);
        MenuUiKit.SetField(screen, "type1Icon", tipo1);
        MenuUiKit.SetField(screen, "type2Icon", tipo2);
        MenuUiKit.SetField(screen, "trainerText", entrenador);
        MenuUiKit.SetField(screen, "idText", idNo);
        MenuUiKit.SetField(screen, "xpText", exp);
        MenuUiKit.SetField(screen, "xpToNextText", expSiguiente);
        MenuUiKit.SetField(screen, "xpFill", expFill);
        MenuUiKit.SetField(screen, "heldItemText", objeto);

        MenuUiKit.SetField(screen, "encounterText", encuentro);
        MenuUiKit.SetField(screen, "characteristicText", caracteristica);
        MenuUiKit.SetField(screen, "abilityNameText", habilidad);
        MenuUiKit.SetField(screen, "abilityDescriptionText", habilidadDesc);

        MenuUiKit.SetField(screen, "hpText", ps);
        MenuUiKit.SetField(screen, "attackText", ataque);
        MenuUiKit.SetField(screen, "defenseText", defensa);
        MenuUiKit.SetField(screen, "specialAttackText", atEsp);
        MenuUiKit.SetField(screen, "specialDefenseText", defEsp);
        MenuUiKit.SetField(screen, "speedText", velocidad);
        MenuUiKit.SetField(screen, "hpFill", psFill);

        MenuUiKit.SetField(screen, "moveRowPrefab", moveRowPrefab.GetComponent<MoveRowWidget>());
        MenuUiKit.SetField(screen, "moveContainer", movsLista.transform);
        MenuUiKit.SetField(screen, "moveHintText", pista);
        MenuUiKit.SetField(screen, "movesPageIndex", 3);
        // El panel izquierdo del fondo ya trae "Nv." dibujado: el texto solo
        // pone el numero, si no queda "Nv. Nv. 20".
        MenuUiKit.SetField(screen, "levelFormat", "{0}");

        var so = new SerializedObject(screen);
        SerializedProperty pagesProp = so.FindProperty("pages");
        GameObject[] roots = { pagInfo, pagNotas, pagStats, pagMovs };
        pagesProp.arraySize = roots.Length;
        for (int i = 0; i < roots.Length; i++)
        {
            SerializedProperty e = pagesProp.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("title").stringValue = tabTitles[i];
            e.FindPropertyRelative("root").objectReferenceValue = roots[i];
            e.FindPropertyRelative("tab").objectReferenceValue = tabs[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/SummaryScreen.prefab");
    }

    private static GameObject BuildOptionsScreen(GameObject optionRowPrefab)
    {
        GameObject root = ScreenRoot("OptionsScreen");
        var screen = root.AddComponent<OptionsScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Options);

        GameObject fondoGo = MenuUiKit.Node("Fondo", root.transform);
        MenuUiKit.Full(fondoGo);
        MenuUiKit.AddImage(fondoGo, null, new Color(0.10f, 0.12f, 0.17f, 0.97f));

        Text titulo = MenuUiKit.Label("Titulo", root.transform, new Vector2(16f, -10f),
            new Vector2(240f, 24f), "Opciones", 18, TextAnchor.MiddleLeft, MenuUiKit.Accent);

        GameObject panel = MenuUiKit.Node("Panel", root.transform);
        MenuUiKit.Stretch(panel, Vector2.zero, Vector2.one,
            new Vector2(70f, 34f), new Vector2(-70f, -44f));
        MenuUiKit.AddImage(panel, null, MenuUiKit.Panel);
        Borde(panel);

        GameObject filas = MenuUiKit.Node("Filas", panel.transform);
        MenuUiKit.Stretch(filas, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        MenuUiKit.VerticalList(filas, 2f, new RectOffset(0, 0, 0, 0));

        Text pista = MenuUiKit.Label("Pista", root.transform, new Vector2(0f, 10f),
            new Vector2(480f, 20f), "", 11, TextAnchor.MiddleCenter, MenuUiKit.Border,
            new Vector2(0.5f, 0f));

        MenuUiKit.SetField(screen, "rowPrefab", optionRowPrefab.GetComponent<OptionRowWidget>());
        MenuUiKit.SetField(screen, "rowContainer", filas.transform);
        MenuUiKit.SetField(screen, "visibleRows", 11);
        MenuUiKit.SetField(screen, "titleText", titulo);
        MenuUiKit.SetField(screen, "hintText", pista);

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/OptionsScreen.prefab");
    }

    private static GameObject BuildMessageBoxScreen(GameObject optionPrefab)
    {
        GameObject root = ScreenRoot("MessageBoxScreen");
        var screen = root.AddComponent<MessageBoxScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.MessageBox);

        GameObject caja = MenuUiKit.Node("Caja", root.transform);
        MenuUiKit.Box(caja, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 10f), new Vector2(490f, 72f));
        MenuUiKit.AddImage(caja, null, MenuUiKit.Panel);
        Borde(caja);

        Text texto = MenuUiKit.Label("Texto", caja.transform, new Vector2(14f, -10f),
            new Vector2(430f, 52f), "", 13, TextAnchor.UpperLeft, MenuUiKit.Ink);
        texto.horizontalOverflow = HorizontalWrapMode.Wrap;

        Image flecha = MenuUiKit.Rect("Flecha", caja.transform, new Vector2(-14f, 10f),
            new Vector2(10f, 10f), MenuUiKit.Accent, null, new Vector2(1f, 0f));

        GameObject opciones = MenuUiKit.Node("Opciones", root.transform);
        MenuUiKit.Box(opciones, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-12f, 90f), new Vector2(120f, 62f));
        MenuUiKit.AddImage(opciones, null, MenuUiKit.PanelLight);
        Borde(opciones);
        MenuUiKit.VerticalList(opciones, 2f, new RectOffset(5, 5, 5, 5), TextAnchor.UpperCenter);

        var si = (GameObject)PrefabUtility.InstantiatePrefab(optionPrefab, opciones.transform);
        si.name = "Si";
        MenuOptionWidget siWidget = ConfigurarOpcion(si, "yes", "SI", null);

        var no = (GameObject)PrefabUtility.InstantiatePrefab(optionPrefab, opciones.transform);
        no.name = "No";
        MenuOptionWidget noWidget = ConfigurarOpcion(no, "no", "NO", null);

        opciones.SetActive(false);

        MenuUiKit.SetField(screen, "messageText", texto);
        MenuUiKit.SetField(screen, "choiceRoot", opciones);
        MenuUiKit.SetField(screen, "yesOption", siWidget);
        MenuUiKit.SetField(screen, "noOption", noWidget);
        MenuUiKit.SetField(screen, "continueHint", flecha.gameObject);

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/MessageBoxScreen.prefab");
    }

    // ------------------------------------------------------------ MenuSystem

    private static void BuildMenuSystem(List<GameObject> screens)
    {
        GameObject root = new GameObject("MenuSystem",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Bien alto: los menus tienen que quedar por encima del HUD de combate
        // y de la caja de dialogo.
        canvas.sortingOrder = 500;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight);
        // Expand: la pantalla entera de 512x384 siempre entra, aunque el monitor
        // sea mas ancho. Con Match se recortarian los bordes de los fondos.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        GameObject dimmer = MenuUiKit.Node("Oscurecedor", root.transform);
        MenuUiKit.Full(dimmer);
        MenuUiKit.AddImage(dimmer, null, new Color(0f, 0f, 0f, 0.55f));
        dimmer.SetActive(false);

        GameObject pantallas = MenuUiKit.Node("Pantallas", root.transform);
        MenuUiKit.Full(pantallas);

        var manager = root.AddComponent<MenuManager>();
        MenuUiKit.SetField(manager, "screenRoot", pantallas.GetComponent<RectTransform>());
        MenuUiKit.SetField(manager, "dimmer", dimmer);

        var refs = new Object[screens.Count];
        for (int i = 0; i < screens.Count; i++)
            refs[i] = screens[i].GetComponent<MenuScreen>();
        MenuUiKit.SetArrayField(manager, "screenPrefabs", refs);

        MenuUiKit.SavePrefab(root, ResourcesFolder + "/MenuSystem.prefab");
    }

    // ---------------------------------------------------------------- helpers

    private static GameObject ScreenRoot(string name)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        MenuUiKit.Full(root);
        return root;
    }

    // Marco de exactamente 512x384 centrado en pantalla.
    //
    // Los fondos del pack son de 512x384. Estirarlos a toda la pantalla (que en
    // 16:9 es mucho mas ancha) los deformaba y desalineaba todo lo que va
    // encima. Con el marco fijo, el arte conserva su proporcion y las
    // coordenadas de cada elemento son las mismas que en el PNG original.
    private static Transform Marco(GameObject root, string fondo)
    {
        GameObject marco = MenuUiKit.Node("Marco", root.transform);
        MenuUiKit.Box(marco, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));

        if (!string.IsNullOrEmpty(fondo))
        {
            GameObject fondoGo = MenuUiKit.Node("Fondo", marco.transform);
            MenuUiKit.Full(fondoGo);
            MenuUiKit.AddImage(fondoGo, MenuUiKit.Sprite(fondo), Color.white);
        }

        return marco.transform;
    }

    private static Image Fill(GameObject parent, string name, Color color)
    {
        GameObject go = MenuUiKit.Node(name, parent.transform);
        MenuUiKit.Full(go);
        return MenuUiKit.AddImage(go, null, color);
    }

    // Marco alrededor de un panel, para que se despegue del fondo.
    //
    // Son cuatro tiras y no un hijo mas grande: un hijo siempre se dibuja
    // ENCIMA del padre, asi que el "marco" taparia el panel entero.
    private static void Borde(GameObject panel)
    {
        Tira(panel, "BordeArriba", new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -2f), new Vector2(0f, 0f));
        Tira(panel, "BordeAbajo", new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, 2f));
        Tira(panel, "BordeIzq", new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(2f, 0f));
        Tira(panel, "BordeDer", new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-2f, 0f), new Vector2(0f, 0f));
    }

    private static void Tira(GameObject panel, string name, Vector2 anchorMin, Vector2 anchorMax,
                             Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = MenuUiKit.Node(name, panel.transform);
        MenuUiKit.Stretch(go, anchorMin, anchorMax, offsetMin, offsetMax);
        MenuUiKit.AddImage(go, null, MenuUiKit.Border);
    }

    // Configura una instancia de MenuOption anidada.
    //
    // Se hace todo por SerializedObject: tocar los componentes de una instancia
    // de prefab con la API normal no siempre registra el override, y el cambio
    // se pierde al guardar el prefab que la contiene.
    private static MenuOptionWidget ConfigurarOpcion(GameObject instancia, string id,
                                                     string label, Sprite icono)
    {
        var widget = instancia.GetComponent<MenuOptionWidget>();
        MenuUiKit.SetField(widget, "optionId", id);

        Transform textoTr = instancia.transform.Find("Texto");
        if (textoTr != null) MenuUiKit.SetField(textoTr.GetComponent<Text>(), "m_Text", label);

        Transform iconoTr = instancia.transform.Find("Icono");
        if (iconoTr != null)
        {
            Image img = iconoTr.GetComponent<Image>();
            MenuUiKit.SetField(img, "m_Sprite", icono);
            MenuUiKit.SetField(img, "m_Enabled", icono != null);
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(widget);
        return widget;
    }

    // ---------------------------------------------------------------- SALIR del equipo

    private const string PartyScreenPath = PrefabFolder + "/PartyScreen.prefab";
    private const string SalirNormal = "Assets/Graphics/UI/Party/icon_cancel_narrow.png";
    private const string SalirElegido = "Assets/Graphics/UI/Party/icon_cancel_narrow_sel.png";

    // Arregla SOLO el boton SALIR del prefab de la pantalla de equipo, sin
    // rehacer el resto de los menus (que el equipo puede haber retocado).
    [MenuItem("PokeRush/UI/Boton SALIR de la pantalla de equipo")]
    public static void ArreglarSalirDelEquipo()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(PartyScreenPath);
        try
        {
            Transform cancelar = raiz.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "Cancelar");
            if (cancelar == null)
            {
                Debug.LogError($"[Menus] {PartyScreenPath} no tiene el boton 'Cancelar'.");
                return;
            }
            ConfigurarBotonSalir(cancelar.gameObject);
            PrefabUtility.SaveAsPrefabAsset(raiz, PartyScreenPath);
            Debug.Log("[Menus] Boton SALIR de la pantalla de equipo listo: arte del pack, " +
                      "estados normal/elegido/apretado y clic con el mouse. Los PNG " +
                      "(icon_cancel_narrow*.png) estan en Assets/Graphics: subirlos con git add -f.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    // El SALIR de la pantalla de equipo como un boton de verdad: el boton
    // CANCEL del pack (azul; con borde rojo cuando el cursor esta encima, igual
    // que los paneles de esa misma pantalla), abajo a la derecha, con el texto
    // centrado y un MenuPuntero para el mouse. Antes era una etiqueta gris que
    // ni el cursor ni el mouse podian alcanzar: solo se salia con Z.
    private static void ConfigurarBotonSalir(GameObject cancelar)
    {
        Sprite normal = MenuUiKit.Sprite(SalirNormal);
        Sprite elegido = MenuUiKit.Sprite(SalirElegido);

        // 112x36 a la resolucion de referencia (512x384): el tamanio nativo del
        // arte, asi no se escala ni se ve borroso. Entra en el margen de 46 px
        // que dejan los paneles abajo.
        MenuUiKit.Box(cancelar, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-8f, 5f), new Vector2(112f, 36f));

        var widget = cancelar.GetComponent<MenuOptionWidget>();
        Transform fondoTr = cancelar.transform.Find("Fondo");
        Transform textoTr = cancelar.transform.Find("Texto");
        Transform resaltadoTr = cancelar.transform.Find("Resaltado");

        if (fondoTr != null)
        {
            Image fondo = fondoTr.GetComponent<Image>();
            MenuUiKit.SetField(fondo, "m_Sprite", normal);
            MenuUiKit.SetField(fondo, "m_Color", Color.white);
            // Sin esto el clic no llega: el fondo es lo unico que lo recibe.
            MenuUiKit.SetField(fondo, "m_RaycastTarget", true);
        }

        if (textoTr != null)
        {
            var rt = (RectTransform)textoTr;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(0f, 2f);
            rt.offsetMax = Vector2.zero;
            Text texto = textoTr.GetComponent<Text>();
            MenuUiKit.SetField(texto, "m_FontData.m_Alignment", (int)TextAnchor.MiddleCenter);
            MenuUiKit.SetField(texto, "m_FontData.m_FontSize", 14);
        }

        // El resaltado naranja de las opciones comunes taparia el arte: aca lo
        // que cambia es el sprite (y el color del texto, que sobre el celeste
        // claro del elegido tiene que ser oscuro para leerse).
        if (resaltadoTr != null) resaltadoTr.gameObject.SetActive(false);
        if (widget != null)
        {
            MenuUiKit.SetField(widget, "normalSprite", normal);
            MenuUiKit.SetField(widget, "selectedSprite", elegido);
            MenuUiKit.SetArrayField(widget, "selectedObjects", new Object[0]);
            MenuUiKit.SetField(widget, "normalColor", MenuUiKit.Ink);
            MenuUiKit.SetField(widget, "selectedColor", MenuUiKit.InkDark);
        }

        if (cancelar.GetComponent<MenuPuntero>() == null) cancelar.AddComponent<MenuPuntero>();

        if (PrefabUtility.IsPartOfPrefabInstance(cancelar))
            foreach (Component c in cancelar.GetComponentsInChildren<Component>(true))
                if (c != null) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
    }

    private static GameObject Pagina(string name, Transform parent)
    {
        GameObject go = MenuUiKit.Node(name, parent);
        MenuUiKit.Full(go);
        return go;
    }

    // Una pagina del resumen con su propio fondo del pack.
    private static GameObject PaginaConFondo(string name, Transform parent, string fondo)
    {
        GameObject go = Pagina(name, parent);
        GameObject fondoGo = MenuUiKit.Node("Fondo", go.transform);
        MenuUiKit.Full(fondoGo);
        MenuUiKit.AddImage(fondoGo, MenuUiKit.Sprite("Assets/Graphics/UI/Summary/" + fondo + ".png"),
            Color.white);
        return go;
    }

    // Etiqueta amarilla del panel derecho del resumen.
    private static Text CampoEtiqueta(GameObject page, string name, string texto, float y,
                                      float ancho = 120f)
    {
        Text t = MenuUiKit.Label(name, page.transform, new Vector2(228f, -y),
            new Vector2(ancho, 24f), texto, 13, TextAnchor.MiddleLeft, MenuUiKit.Gold);
        return MenuUiKit.Contorno(t);
    }

    // Etiqueta + valor, en las dos columnas que el fondo ya separa con la linea
    // punteada de x=345.
    private static Text CampoResumen(GameObject page, string name, string etiqueta, float y,
                                     TextAnchor alineacion = TextAnchor.MiddleLeft)
    {
        CampoEtiqueta(page, name + "Etiqueta", etiqueta, y);
        Text valor = MenuUiKit.Label(name, page.transform, new Vector2(352f, -y),
            new Vector2(150f, 24f), "", 14, alineacion, MenuUiKit.Ink);
        return MenuUiKit.Contorno(valor);
    }

    // Un stat del hexagono: etiqueta arriba, numero abajo.
    private static Text StatResumen(GameObject page, string name, string etiqueta,
                                    float x, float y, float ancho, TextAnchor alineacion)
    {
        Text lbl = MenuUiKit.Label(name + "Etiqueta", page.transform, new Vector2(x, -y),
            new Vector2(ancho, 22f), etiqueta, 13, alineacion, MenuUiKit.Gold);
        MenuUiKit.Contorno(lbl);

        Text valor = MenuUiKit.Label(name, page.transform, new Vector2(x, -(y + 22f)),
            new Vector2(ancho, 22f), "", 15, alineacion, MenuUiKit.Ink);
        return MenuUiKit.Contorno(valor);
    }

    // Una fila "etiqueta: valor" dentro de una pagina del Summary.
    private static Text Campo(GameObject page, string name, int fila, string etiqueta)
    {
        float y = -4f - fila * 24f;
        MenuUiKit.Label(name + "Etiqueta", page.transform, new Vector2(4f, y),
            new Vector2(110f, 18f), etiqueta, 12, TextAnchor.MiddleLeft, MenuUiKit.Accent);
        return MenuUiKit.Label(name, page.transform, new Vector2(120f, y),
            new Vector2(200f, 18f), "", 12, TextAnchor.MiddleLeft, MenuUiKit.Ink);
    }

    private static Image TipoIcono(Transform parent, string name)
    {
        GameObject go = MenuUiKit.Node(name, parent);
        MenuUiKit.Box(go, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            Vector2.zero, new Vector2(64f, 18f));
        Image img = MenuUiKit.AddImage(go, null, Color.white);
        img.preserveAspect = true;
        return img;
    }
}
