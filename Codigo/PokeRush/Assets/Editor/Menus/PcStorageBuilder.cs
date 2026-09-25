using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Arma la pantalla del PC de almacenamiento y la registra en el MenuSystem.
//
//   PokeRush > UI > Construir la pantalla del PC
//
// Igual que los builders del casino: NO reconstruye el sistema de menus, le
// agrega la pantalla que falta y lo vuelve a guardar, asi no pisa lo que el
// equipo haya retocado en las otras pantallas.
//
// Todo el arte es de Graphics/UI/Storage (Pokemon Essentials) y esta dibujado
// para una pantalla de 512x384, que es justo la referencia del Canvas: las
// medidas de abajo son pixeles del arte, con el origen arriba a la izquierda
// como en un editor de imagenes (P() las pasa al centro, que es lo que usa UGUI).
//
//   izquierda   el panel de datos del Pokemon que se apunta (overlay_main);
//   derecha     la caja de 6x5 (box_N), con su nombre arriba;
//   abajo       EQUIPO y SALIR (tambien del overlay_main);
//   encima      el panel del equipo (overlay_party), que se abre con EQUIPO.
public static class PcStorageBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/Menus/PcStorageScreen.prefab";
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";
    private const string Arte = "Assets/Graphics/UI/Storage/";
    private const int FondosDeCaja = 40;

    // ---- caja ----
    private static readonly Rect Caja = new Rect(184, 18, 324, 296);
    // La franja del nombre, con el paisaje dibujado.
    private static readonly Rect NombreDeCaja = new Rect(232, 22, 228, 40);
    // Grilla de 6x5 con celdas de 48: queda centrada en el pano verde de la caja.
    private const float GrillaX = 202f;
    private const float GrillaY = 70f;
    private const float Celda = 48f;
    private static readonly Vector2 Icono = new Vector2(64f, 64f);

    // ---- panel de datos (overlay_main) ----
    private static readonly Rect Nombre = new Rect(8, 8, 164, 30);
    private static readonly Rect Retrato = new Rect(20, 56, 136, 136);
    private static readonly Rect Fila1 = new Rect(12, 232, 156, 32);
    private static readonly Rect Fila2 = new Rect(12, 268, 156, 32);
    private static readonly Rect Fila3 = new Rect(12, 304, 156, 32);
    private static readonly Rect Fila4 = new Rect(12, 340, 156, 32);

    // ---- botones de abajo (dibujados en el overlay_main) ----
    private static readonly Rect BotonEquipo = new Rect(188, 322, 162, 38);
    private static readonly Rect BotonSalir = new Rect(390, 322, 112, 38);

    // ---- panel del equipo (overlay_party, 172x352) ----
    private static readonly Rect PanelEquipo = new Rect(186, 16, 172, 352);
    // Centro de los seis ovalos, medidos sobre el dibujo del panel.
    private static readonly Vector2[] Ovalos =
    {
        new Vector2(50, 42), new Vector2(122, 58),
        new Vector2(50, 106), new Vector2(122, 122),
        new Vector2(50, 170), new Vector2(122, 186)
    };
    private static readonly Rect Volver = new Rect(20, 228, 132, 56);

    private static readonly Color Tinta = new Color(0.16f, 0.18f, 0.24f, 1f);
    private static readonly Color Telon = new Color(0.02f, 0.05f, 0.12f, 1f);

    [MenuItem("PokeRush/UI/Construir la pantalla del PC")]
    public static void Construir()
    {
        MenuUiKit.EnsureFolder(System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

        GameObject prefab = ArmarPantalla();
        if (prefab == null) return;

        RegistrarEnMenuSystem(prefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"[PC] Pantalla generada en {PrefabPath} y registrada en el MenuSystem.");
    }

    // Rectangulo del arte (origen arriba a la izquierda) -> posicion anclada al
    // centro del padre, que mide 'ancho' x 'alto'.
    private static Vector2 P(Rect r, float ancho = MenuUiKit.RefWidth, float alto = MenuUiKit.RefHeight)
        => new Vector2(r.x + r.width * 0.5f - ancho * 0.5f, alto * 0.5f - (r.y + r.height * 0.5f));

    private static Vector2 Centro(Vector2 c, float ancho, float alto)
        => new Vector2(c.x - ancho * 0.5f, alto * 0.5f - c.y);

    private static RectTransform Nodo(string nombre, Transform padre, Vector2 pos, Vector2 tam)
    {
        GameObject go = MenuUiKit.Node(nombre, padre);
        return MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, tam);
    }

    private static Image Imagen(string nombre, Transform padre, Vector2 pos, Vector2 tam, Sprite sprite, Color color)
    {
        RectTransform rt = Nodo(nombre, padre, pos, tam);
        Image img = MenuUiKit.AddImage(rt.gameObject, sprite, color);
        return img;
    }

    private static Text Texto(string nombre, Transform padre, Vector2 pos, Vector2 tam, string contenido,
                              int tamanio, TextAnchor anclaje, Color color)
    {
        RectTransform rt = Nodo(nombre, padre, pos, tam);
        return MenuUiKit.AddText(rt.gameObject, contenido, tamanio, anclaje, color);
    }

    private static GameObject ArmarPantalla()
    {
        Sprite bg = MenuUiKit.Sprite(Arte + "bg.png");
        Sprite overlay = MenuUiKit.Sprite(Arte + "overlay_main.png");
        Sprite panelEquipo = MenuUiKit.Sprite(Arte + "overlay_party.png");
        Sprite mano = MenuUiKit.Sprite(Arte + "cursor_point_1.png");
        Sprite manoCerrada = MenuUiKit.Sprite(Arte + "cursor_grab.png");

        var fondos = new List<Object>();
        for (int i = 0; i < FondosDeCaja; i++)
        {
            Sprite s = MenuUiKit.Sprite($"{Arte}box_{i}.png");
            if (s != null) fondos.Add(s);
        }

        if (bg == null || overlay == null || fondos.Count == 0)
        {
            Debug.LogError($"[PC] Falta el arte de {Arte}. No se genero la pantalla.");
            return null;
        }

        var root = new GameObject("PcStorageScreen", typeof(RectTransform));
        MenuUiKit.Full(root);
        var screen = root.AddComponent<PcStorageScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.PcStorage);

        // Telon opaco a pantalla completa: el Canvas encaja los 512x384 en la
        // ventana y en 16:9 sobran franjas a los costados.
        GameObject telon = MenuUiKit.Node("Telon", root.transform);
        MenuUiKit.Full(telon);
        MenuUiKit.AddImage(telon, null, Telon);

        RectTransform marco = Nodo("Marco", root.transform, Vector2.zero,
            new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));
        Transform m = marco;

        // UGUI dibuja por jerarquia: fondo -> panel de datos -> caja -> iconos ->
        // textos -> panel del equipo -> mano.
        GameObject fondo = MenuUiKit.Node("Fondo", m);
        MenuUiKit.Full(fondo);
        MenuUiKit.AddImage(fondo, bg, Color.white);

        GameObject marcoDatos = MenuUiKit.Node("PanelDeDatos", m);
        MenuUiKit.Full(marcoDatos);
        MenuUiKit.AddImage(marcoDatos, overlay, Color.white);

        // ---- caja ----
        Image caja = Imagen("Caja", m, P(Caja), Caja.size, (Sprite)fondos[0], Color.white);
        MenuUiKit.SetField(screen, "boxBackground", caja);
        MenuUiKit.SetArrayField(screen, "boxSprites", fondos.ToArray());

        RectTransform encabezado = Nodo("Encabezado", m, P(NombreDeCaja), NombreDeCaja.size);
        MenuUiKit.SetField(screen, "boxHeader", encabezado);
        Text nombreCaja = Texto("NombreDeCaja", encabezado, Vector2.zero, NombreDeCaja.size, "Caja 1",
            20, TextAnchor.MiddleCenter, Color.white);
        MenuUiKit.Contorno(nombreCaja);
        MenuUiKit.SetField(screen, "boxNameText", nombreCaja);

        var iconos = new Object[PokenStorageModel.LugaresPorCaja];
        for (int i = 0; i < iconos.Length; i++)
        {
            int col = i % PokenStorageModel.Columnas;
            int fila = i / PokenStorageModel.Columnas;
            var centro = new Vector2(GrillaX + Celda * 0.5f + col * Celda, GrillaY + Celda * 0.5f + fila * Celda);
            Image icono = Imagen($"Icono_{i:00}", m, Centro(centro, MenuUiKit.RefWidth, MenuUiKit.RefHeight),
                Icono, null, Color.white);
            icono.preserveAspect = true;
            icono.enabled = false;
            iconos[i] = icono;
        }
        MenuUiKit.SetArrayField(screen, "boxIcons", iconos);

        // ---- datos ----
        MenuUiKit.SetField(screen, "nameText",
            Texto("Nombre", m, P(Nombre), Nombre.size, "", 18, TextAnchor.MiddleCenter, Tinta));
        Image retrato = Imagen("Retrato", m, P(Retrato), Retrato.size, null, Color.white);
        retrato.preserveAspect = true;
        retrato.enabled = false;
        MenuUiKit.SetField(screen, "bigSprite", retrato);
        MenuUiKit.SetField(screen, "levelText",
            Texto("Nivel", m, P(Fila1), Fila1.size, "", 16, TextAnchor.MiddleLeft, Tinta));
        MenuUiKit.SetField(screen, "typeText",
            Texto("Tipos", m, P(Fila2), Fila2.size, "", 14, TextAnchor.MiddleLeft, Tinta));
        MenuUiKit.SetField(screen, "hpText",
            Texto("PS", m, P(Fila3), Fila3.size, "", 16, TextAnchor.MiddleLeft, Tinta));
        MenuUiKit.SetField(screen, "shinyText",
            Texto("Shiny", m, P(Fila4), Fila4.size, "", 16, TextAnchor.MiddleLeft, new Color(0.85f, 0.4f, 0.08f, 1f)));

        // ---- botones ----
        // El dibujo de los botones ya viene en el overlay: encima va solo un
        // brillo que la pantalla prende en el elegido.
        Image equipo = Imagen("BotonEquipo", m, P(BotonEquipo), BotonEquipo.size, MenuUiKit.WhitePixel, Color.clear);
        Texto("Texto", equipo.transform, Vector2.zero, BotonEquipo.size, "EQUIPO", 18, TextAnchor.MiddleCenter, Tinta);
        MenuUiKit.SetField(screen, "partyButton", equipo);

        Image salir = Imagen("BotonSalir", m, P(BotonSalir), BotonSalir.size, MenuUiKit.WhitePixel, Color.clear);
        Texto("Texto", salir.transform, Vector2.zero, BotonSalir.size, "SALIR", 18, TextAnchor.MiddleCenter, Tinta);
        MenuUiKit.SetField(screen, "exitButton", salir);

        // ---- panel del equipo ----
        Image panel = Imagen("PanelDelEquipo", m, P(PanelEquipo), PanelEquipo.size, panelEquipo, Color.white);
        MenuUiKit.SetField(screen, "partyPanel", panel.gameObject);

        var iconosEquipo = new Object[PlayerModel.EquipoMaximo];
        for (int i = 0; i < iconosEquipo.Length; i++)
        {
            // El icono se apoya sobre el ovalo, un poco hacia arriba: los
            // Pokemon "se paran" en el.
            Vector2 c = Ovalos[i] + new Vector2(0f, -6f);
            Image icono = Imagen($"Equipo_{i}", panel.transform, Centro(c, PanelEquipo.width, PanelEquipo.height),
                Icono, null, Color.white);
            icono.preserveAspect = true;
            icono.enabled = false;
            iconosEquipo[i] = icono;
        }
        MenuUiKit.SetArrayField(screen, "partyIcons", iconosEquipo);

        RectTransform volver = Nodo("Volver", panel.transform, P(Volver, PanelEquipo.width, PanelEquipo.height), Volver.size);
        MenuUiKit.AddText(volver.gameObject, "VOLVER", 18, TextAnchor.MiddleCenter, Tinta);
        MenuUiKit.SetField(screen, "partyBack", volver);
        panel.gameObject.SetActive(false);

        // ---- mano ----
        RectTransform cursor = Nodo("Cursor", m, Vector2.zero, Icono);
        Image agarrado = Imagen("Agarrado", cursor, new Vector2(-10f, -26f), Icono, null, Color.white);
        agarrado.preserveAspect = true;
        agarrado.gameObject.SetActive(false);
        Image manoImg = Imagen("Mano", cursor, Vector2.zero, Icono, mano, Color.white);

        MenuUiKit.SetField(screen, "cursor", cursor);
        MenuUiKit.SetField(screen, "cursorImage", manoImg);
        MenuUiKit.SetField(screen, "cursorPoint", mano);
        MenuUiKit.SetField(screen, "cursorGrab", manoCerrada);
        MenuUiKit.SetField(screen, "heldIcon", agarrado);

        return MenuUiKit.SavePrefab(root, PrefabPath);
    }

    private static void RegistrarEnMenuSystem(GameObject pantalla)
    {
        var screen = pantalla != null ? pantalla.GetComponent<MenuScreen>() : null;
        if (screen == null) return;

        GameObject contenido = PrefabUtility.LoadPrefabContents(MenuSystemPath);
        if (contenido == null)
        {
            Debug.LogError($"[PC] No se encontro {MenuSystemPath}. Genera primero el sistema de menus.");
            return;
        }

        try
        {
            var manager = contenido.GetComponent<MenuManager>();
            if (manager == null)
            {
                Debug.LogError("[PC] El prefab MenuSystem no tiene MenuManager.");
                return;
            }

            var so = new SerializedObject(manager);
            SerializedProperty lista = so.FindProperty("screenPrefabs");

            for (int i = 0; i < lista.arraySize; i++)
                if (lista.GetArrayElementAtIndex(i).objectReferenceValue == screen) return;

            lista.InsertArrayElementAtIndex(lista.arraySize);
            lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = screen;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contenido, MenuSystemPath);
            Debug.Log("[PC] MenuSystem: agregada la pantalla PcStorage.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
