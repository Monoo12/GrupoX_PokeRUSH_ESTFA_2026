using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Pule las tres pantallas del arranque sin cambiarles la identidad: el titulo
// (Menu Principal), la eleccion de partida (MenuSaves) y la creacion de
// personaje.
//
// La identidad la pone MenuSaves y se respeta: el fondo de la ciudad de noche
// (fondoCampeon), los paneles y botones grises que se vuelven celestes al
// pasarles por encima (panels.png), la letra en negrita clara y los titulos en
// celeste. La creacion de personaje era un prototipo con otra cara (fondo
// liso, fichas grises sin estados, sin tildes): se rearma con esas mismas
// piezas.
//
//   Titulo     entra con fundido, el PRESS START respira (una copia exacta de
//              ese pedazo del arte con PulsoSuave) y destella al aceptar.
//   Partidas   estados de boton (BotonAnimado), "CONTINUAR ›" en la tarjeta.
//   Creacion   indicador de pasos, fichas con estados y elegido, Continuar
//              deshabilitado hasta elegir, Volver en cada paso, ayuda de
//              teclas, paneles que entran animados (EntradaAnimada).
//
// Se puede correr de nuevo: rehace lo suyo y no duplica nada. De la creacion
// conserva lo que es dato (los iniciales, el nivel y la escena destino, que
// escribe QuarchoCuevaBuilder).
public static class PantallasDeInicioBuilder
{
    private const string EscenaTitulo = "Assets/Scenes/Menu Principal.unity";
    private const string EscenaPartidas = "Assets/Scenes/MenuSaves.unity";
    private const string EscenaCreacion = "Assets/Scenes/CreacionPersonaje.unity";

    private const string Paneles = "Assets/Sprites/Menus/panels.png";
    private const string FondoCiudad = "Assets/Sprites/Menus/fondoCampeon.png";
    private const string ArteDelTitulo = "Assets/Sprites/Menus/pantalla incio.png";
    private const string EntrenadoraCaminando = "Assets/Sprites/Player/POKEMONTRAINER_HojaNeutra_walk_2.png";

    // Donde esta el PRESS START dentro del arte del titulo, en pixeles de la
    // textura (desde abajo a la izquierda). Medido sobre el PNG de 1672x941:
    // el borde izquierdo es azul y el derecho rojizo (cae sobre la mitad roja).
    private static readonly RectInt PressStartEnElArte = new RectInt(586, 72, 498, 104);

    // La misma referencia que el titulo y MenuSaves: las tres pantallas miden igual.
    private static readonly Vector2 Referencia = new Vector2(1672f, 941f);

    private static readonly Color Celeste = new Color(0.35f, 0.72f, 0.95f, 1f);
    private static readonly Color Tinta = new Color(0.92f, 0.95f, 0.97f, 1f);
    private static readonly Color TintaSuave = new Color(0.62f, 0.68f, 0.76f, 1f);
    private static readonly Color ColorAviso = new Color(1f, 0.6f, 0.47f, 1f);

    [MenuItem("PokeRush/UI/Pulir pantallas de inicio")]
    public static void PulirTodo()
    {
        if (!SinCambiosPendientes()) return;

        bool titulo = PulirTitulo();
        bool partidas = PulirPartidas();
        bool creacion = ArmarCreacion();

        Debug.Log($"[Inicio] Titulo {(titulo ? "ok" : "FALLO")}, partidas {(partidas ? "ok" : "FALLO")}, " +
                  $"creacion de personaje {(creacion ? "ok" : "FALLO")}.");
    }

    // ------------------------------------------------------------------ titulo

    private static bool PulirTitulo()
    {
        Scene scene = EditorSceneManager.OpenScene(EscenaTitulo, OpenSceneMode.Single);
        StartScreenView vista = Buscar<StartScreenView>(scene);
        if (vista == null) { Debug.LogError("[Inicio] Menu Principal no tiene StartScreenView."); return false; }

        GameObject canvas = vista.gameObject;
        // Sin '??': en el editor GetComponent devuelve un null "falso" que no lo es para C#.
        CanvasGroup grupo = canvas.GetComponent<CanvasGroup>();
        if (grupo == null) grupo = canvas.AddComponent<CanvasGroup>();

        Transform fondo = canvas.transform.Find("Fondo");
        var texturaDelTitulo = AssetDatabase.LoadAssetAtPath<Texture2D>(ArteDelTitulo);
        if (fondo == null || texturaDelTitulo == null)
        {
            Debug.LogError("[Inicio] Falta el Fondo del titulo o su arte.");
            return false;
        }

        Borrar(fondo, "PressStart");

        // Una copia exacta del pedazo del arte donde esta el PRESS START, encima
        // del original: anclada a la misma fraccion del fondo, se ve identica en
        // cualquier resolucion. Como es una copia, puede crecer y destellar sin
        // tocar el PNG.
        var uv = new Rect(PressStartEnElArte.x / (float)texturaDelTitulo.width,
                          PressStartEnElArte.y / (float)texturaDelTitulo.height,
                          PressStartEnElArte.width / (float)texturaDelTitulo.width,
                          PressStartEnElArte.height / (float)texturaDelTitulo.height);
        GameObject press = Nodo("PressStart", fondo);
        var rt = (RectTransform)press.transform;
        rt.anchorMin = uv.min;
        rt.anchorMax = uv.max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var copia = press.AddComponent<RawImage>();
        copia.texture = texturaDelTitulo;
        copia.uvRect = uv;
        copia.raycastTarget = false;
        press.AddComponent<CanvasGroup>();
        PulsoSuave pulso = press.AddComponent<PulsoSuave>();
        Poner(pulso, "periodo", 1.8f);
        Poner(pulso, "escalaMinima", 1f);
        Poner(pulso, "escalaMaxima", 1.04f);
        Poner(pulso, "alfaMinimo", 1f);

        Poner(vista, "pantalla", grupo);
        Poner(vista, "pressStart", pulso);

        Image imagenDelFondo = fondo.GetComponent<Image>();
        if (imagenDelFondo != null && imagenDelFondo.preserveAspect)
            Debug.LogWarning("[Inicio] El Fondo del titulo conserva la proporcion: la copia del PRESS START puede no calzar.");

        Poner(vista, "quitButton", CruzDeSalir(canvas.transform));

        return Guardar(scene);
    }

    // ------------------------------------------------------- la cruz de salir

    // Arriba a la izquierda del titulo y de MenuSaves: cierra el juego
    // (pedido de Augusto, 8-oct-2026). Un boton cuadrado con los mismos
    // paneles que el resto de MenuSaves (gris, celeste con el cursor encima o
    // seleccionado) y una X de pixel art.
    [MenuItem("PokeRush/UI/Poner la cruz de salir en el inicio")]
    public static void PonerCruzDeSalir()
    {
        if (!SinCambiosPendientes()) return;

        Scene titulo = EditorSceneManager.OpenScene(EscenaTitulo, OpenSceneMode.Single);
        StartScreenView inicio = Buscar<StartScreenView>(titulo);
        if (inicio == null) { Debug.LogError("[Inicio] Menu Principal no tiene StartScreenView."); return; }
        Poner(inicio, "quitButton", CruzDeSalir(inicio.transform));
        bool okTitulo = Guardar(titulo);

        Scene partidas = EditorSceneManager.OpenScene(EscenaPartidas, OpenSceneMode.Single);
        SaveSlotMenuView menu = Buscar<SaveSlotMenuView>(partidas);
        if (menu == null) { Debug.LogError("[Inicio] MenuSaves no tiene SaveSlotMenuView."); return; }
        Poner(menu, "quitButton", CruzDeSalir(menu.GetComponentInParent<Canvas>().rootCanvas.transform));
        bool okPartidas = Guardar(partidas);

        Debug.Log($"[Inicio] Cruz de salir: titulo {(okTitulo ? "ok" : "FALLO")}, partidas {(okPartidas ? "ok" : "FALLO")}.");
    }

    private const string ArteDeLaCruz = "Assets/Sprites/Menus/cruz_salir.png";
    private const float LadoDeLaCruz = 72f;

    private static Button CruzDeSalir(Transform raiz)
    {
        Borrar(raiz, "BotonSalir");

        Image fondo = Imagen("BotonSalir", raiz, SubSprite(Paneles, "boton_gris"), Color.white, null);
        fondo.type = Image.Type.Sliced;
        fondo.raycastTarget = true;
        Ubicar(fondo.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f),
               new Vector2(LadoDeLaCruz, LadoDeLaCruz));

        Image x = Imagen("X", fondo.transform, SpriteDeLaCruz(), Tinta, null);
        Ubicar(x.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));

        Button b = fondo.gameObject.AddComponent<Button>();
        b.targetGraphic = fondo;
        b.transition = Selectable.Transition.SpriteSwap;
        Sprite encima = SubSprite(Paneles, "boton_celeste");
        b.spriteState = new SpriteState { highlightedSprite = encima, selectedSprite = encima, pressedSprite = encima };

        BotonAnimado anim = fondo.gameObject.AddComponent<BotonAnimado>();
        Poner(anim, "escalaEncima", 1.08f);
        Poner(anim, "escalaApretado", 0.94f);

        // Encima de todo: en el titulo, el fondo y el PRESS START son hermanos anteriores.
        fondo.transform.SetAsLastSibling();
        return b;
    }

    // Una X de 14x14 con trazo de 2 px, blanca (la tiñe la Image). Point, como
    // panels.png. Se genera una sola vez.
    private static Sprite SpriteDeLaCruz()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(ArteDeLaCruz) == null)
        {
            const int n = 14, m = n - 2;   // 1 px de margen para que el borde no se corte
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var pixeles = new Color32[n * n];
            for (int v = 0; v < m; v++)
                for (int u = 0; u < m; u++)
                {
                    bool principal = u - v == 0 || u - v == 1;
                    bool cruzada = u + v == m - 2 || u + v == m - 1;
                    if (principal || cruzada) pixeles[(v + 1) * n + u + 1] = new Color32(255, 255, 255, 255);
                }
            tex.SetPixels32(pixeles);
            System.IO.File.WriteAllBytes(ArteDeLaCruz, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ArteDeLaCruz, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(ArteDeLaCruz);
        if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(ArteDeLaCruz);
    }

    // ---------------------------------------------------------------- partidas

    // Las mismas teclas en todas las pantallas (playtest del 4-oct: "a veces se
    // acepta con X, a veces con Enter, a veces con el mouse").
    private const string AyudaDeTeclas =
        "Flechas: mover     X / Enter: elegir     Z / Esc: volver     También con el mouse";

    [MenuItem("PokeRush/UI/Poner la ayuda de teclas en MenuSaves")]
    public static void AyudaEnPartidas()
    {
        if (!SinCambiosPendientes()) return;
        Scene scene = EditorSceneManager.OpenScene(EscenaPartidas, OpenSceneMode.Single);
        SaveSlotMenuView vista = Buscar<SaveSlotMenuView>(scene);
        if (vista == null) { Debug.LogError("[Inicio] MenuSaves no tiene SaveSlotMenuView."); return; }
        PonerAyuda(vista);
        Guardar(scene);
    }

    // Abajo de todo, como en la creacion de personaje.
    private static void PonerAyuda(SaveSlotMenuView vista)
    {
        Canvas canvas = vista.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Transform raiz = canvas.rootCanvas.transform;
        Borrar(raiz, "Ayuda");
        Text ayuda = Texto("Ayuda", raiz, AyudaDeTeclas, 24, TextAnchor.MiddleCenter, TintaSuave, false);
        ayuda.raycastTarget = false;
        Ubicar(ayuda.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1400f, 34f));
    }

    private static bool PulirPartidas()
    {
        Scene scene = EditorSceneManager.OpenScene(EscenaPartidas, OpenSceneMode.Single);
        SaveSlotMenuView vista = Buscar<SaveSlotMenuView>(scene);
        if (vista == null) { Debug.LogError("[Inicio] MenuSaves no tiene SaveSlotMenuView."); return false; }

        foreach (Button b in vista.GetComponentsInChildren<Button>(true))
        {
            BotonAnimado anim = b.GetComponent<BotonAnimado>();
            if (anim == null) anim = b.gameObject.AddComponent<BotonAnimado>();
            bool tarjeta = b.name == "SaveCard";
            // La tarjeta es grande: un 5 % se notaria demasiado.
            Poner(anim, "escalaEncima", tarjeta ? 1.015f : 1.03f);
            Poner(anim, "escalaApretado", tarjeta ? 0.99f : 0.97f);
        }

        // "CONTINUAR ›" en la tarjeta: tocarla era la unica forma de seguir y
        // nada lo decia.
        Transform tarjetaTr = vista.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SaveCard");
        if (tarjetaTr != null)
        {
            Borrar(tarjetaTr, "Continuar");
            Text continuar = Texto("Continuar", tarjetaTr, "CONTINUAR  ›", 30, TextAnchor.LowerRight, Celeste, true);
            Ubicar(continuar.gameObject, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 26f), new Vector2(380f, 46f));
            continuar.gameObject.AddComponent<CanvasGroup>();
            PulsoSuave pulso = continuar.gameObject.AddComponent<PulsoSuave>();
            Poner(pulso, "periodo", 1.6f);
            Poner(pulso, "escalaMaxima", 1.05f);
            Poner(pulso, "alfaMinimo", 0.55f);
        }

        // El entrenador de la tarjeta segun el genero de la partida (antes
        // siempre el varon). El de la escena es el varon: se toma de ahi.
        var vistaSo = new SerializedObject(vista);
        Image dibujo = vistaSo.FindProperty("playerSprite").objectReferenceValue as Image;
        if (dibujo != null && dibujo.sprite != null && vistaSo.FindProperty("spriteVaron").objectReferenceValue == null)
            vistaSo.FindProperty("spriteVaron").objectReferenceValue = dibujo.sprite;
        Sprite mujer = SubSprite(EntrenadoraCaminando, "POKEMONTRAINER_HojaNeutra_walk_2_0");
        if (mujer != null) vistaSo.FindProperty("spriteMujer").objectReferenceValue = mujer;
        vistaSo.ApplyModifiedPropertiesWithoutUndo();

        PonerAyuda(vista);
        Poner(vista, "quitButton", CruzDeSalir(vista.GetComponentInParent<Canvas>().rootCanvas.transform));

        // "Próximo: el primer Data Center" no entraba en la cajita de la ruta.
        Text ruta = vista.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "RutaText");
        if (ruta != null)
        {
            ruta.resizeTextForBestFit = true;
            ruta.resizeTextMinSize = 18;
            ruta.resizeTextMaxSize = 30;
            EditorUtility.SetDirty(ruta);
        }

        return Guardar(scene);
    }

    // ---------------------------------------------------------------- creacion

    private struct Inicial
    {
        public string Nombre, Descripcion;
        public PokenDefinition Definicion;
        public Sprite Retrato;
    }

    private static bool ArmarCreacion()
    {
        Scene scene = EditorSceneManager.OpenScene(EscenaCreacion, OpenSceneMode.Single);
        CreacionPersonajeView vieja = Buscar<CreacionPersonajeView>(scene);
        if (vieja == null) { Debug.LogError("[Inicio] CreacionPersonaje no tiene CreacionPersonajeView."); return false; }

        // Lo que es dato se conserva (lo escriben el equipo y otros builders).
        var so = new SerializedObject(vieja);
        string destino = so.FindProperty("escenaDestino").stringValue;
        int nivel = so.FindProperty("nivelInicial").intValue;
        Sprite caraVaron = (so.FindProperty("retratoVaron").objectReferenceValue as Image)?.sprite;
        Sprite caraMujer = (so.FindProperty("retratoMujer").objectReferenceValue as Image)?.sprite;
        var iniciales = new List<Inicial>();
        SerializedProperty lista = so.FindProperty("iniciales");
        for (int i = 0; i < lista.arraySize; i++)
        {
            SerializedProperty e = lista.GetArrayElementAtIndex(i);
            iniciales.Add(new Inicial
            {
                Nombre = e.FindPropertyRelative("nombre").stringValue,
                Definicion = e.FindPropertyRelative("definicion").objectReferenceValue as PokenDefinition,
                Retrato = e.FindPropertyRelative("retrato").objectReferenceValue as Sprite,
                Descripcion = ConTildes(e.FindPropertyRelative("descripcion").stringValue)
            });
        }
        if (iniciales.Count == 0 || caraVaron == null || caraMujer == null)
        {
            Debug.LogError("[Inicio] La creacion de personaje no tiene iniciales o retratos: no se rearma.");
            return false;
        }

        Object.DestroyImmediate(vieja.gameObject);

        Sprite panelGris = SubSprite(Paneles, "panel_gris");
        Sprite panelCeleste = SubSprite(Paneles, "panel_celeste");
        Sprite botonGris = SubSprite(Paneles, "boton_gris");
        Sprite botonCeleste = SubSprite(Paneles, "boton_celeste");
        Sprite ciudad = AssetDatabase.LoadAllAssetsAtPath(FondoCiudad).OfType<Sprite>().FirstOrDefault();

        // ---- lienzo ----
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasGo, scene);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Referencia;
        scaler.matchWidthOrHeight = 0.5f;
        var vista = canvasGo.AddComponent<CreacionPersonajeView>();
        canvasGo.AddComponent<CanvasGroup>();

        Imagen("Fondo", canvasGo.transform, ciudad, Color.white, Estirar);
        Imagen("Velo", canvasGo.transform, null, new Color(0.02f, 0.04f, 0.09f, 0.45f), Estirar);

        // ---- encabezado ----
        Text titulo = Texto("Titulo", canvasGo.transform, "CREÁ TU PERSONAJE", 54, TextAnchor.MiddleCenter, Celeste, true);
        Ubicar(titulo.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 70f));
        Sombra(titulo);

        string[] nombresDePaso = { "1  Quién sos", "2  Nombre", "3  Compañero", "4  ¡Listo!" };
        var etiquetas = new Text[4];
        var marcas = new Image[4];
        GameObject pasos = Nodo("Pasos", canvasGo.transform);
        Ubicar(pasos, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(1120f, 56f));
        for (int i = 0; i < 4; i++)
        {
            float x = -420f + i * 280f;
            marcas[i] = Imagen("Marca" + (i + 1), pasos.transform, null, TintaSuave, null);
            Ubicar(marcas[i].gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -20f), new Vector2(240f, 6f));
            etiquetas[i] = Texto("Etiqueta" + (i + 1), pasos.transform, nombresDePaso[i], 26, TextAnchor.MiddleCenter, TintaSuave, true);
            Ubicar(etiquetas[i].gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 8f), new Vector2(260f, 36f));
        }

        // ---- marco central ----
        Image marco = Imagen("Marco", canvasGo.transform, panelGris, Color.white, null);
        marco.type = Image.Type.Sliced;
        // Debajo del indicador de pasos, con aire arriba y abajo.
        Ubicar(marco.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(1200f, 560f));

        // ---- paso 1: genero ----
        GameObject pGenero = Paso("PanelGenero", marco.transform, "¿Quién sos?");
        (Button fichaVaron, Image retVaron) = Ficha("FichaVaron", pGenero.transform, new Vector2(-190f, -4f), new Vector2(320f, 330f),
            caraVaron, new Vector2(230f, 230f), "Varón", panelGris, panelCeleste, 0f);
        (Button fichaMujer, Image retMujer) = Ficha("FichaMujer", pGenero.transform, new Vector2(190f, -4f), new Vector2(320f, 330f),
            caraMujer, new Vector2(230f, 230f), "Mujer", panelGris, panelCeleste, 0.06f);
        Button contGenero = Boton("Continuar", pGenero.transform, "CONTINUAR  ›", botonGris, botonCeleste, true);
        Button volGenero = Boton("Volver", pGenero.transform, "‹  MENÚ", botonGris, botonCeleste, false);

        // ---- paso 2: nombre ----
        GameObject pNombre = Paso("PanelNombre", marco.transform, "¿Cómo te llamás?");
        InputField campo = CampoDeTexto(pNombre.transform, botonGris);
        Text contador = Texto("Contador", pNombre.transform, "0/10", 26, TextAnchor.MiddleRight, TintaSuave, false);
        Ubicar(contador.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(230f, -32f), new Vector2(160f, 34f));
        Text aviso = Texto("Aviso", pNombre.transform, "", 28, TextAnchor.MiddleCenter, ColorAviso, true);
        Ubicar(aviso.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(900f, 40f));
        Text pista = Texto("Pista", pNombre.transform, "Hasta 10 letras. Lo vas a ver en toda la aventura.", 24, TextAnchor.MiddleCenter, TintaSuave, false);
        Ubicar(pista.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 34f));
        Button contNombre = Boton("Continuar", pNombre.transform, "CONTINUAR  ›", botonGris, botonCeleste, true);
        Button volNombre = Boton("Volver", pNombre.transform, "‹  VOLVER", botonGris, botonCeleste, false);

        // ---- paso 3: inicial ----
        GameObject pInicial = Paso("PanelInicial", marco.transform, "Elegí a tu compañero");
        var botonesInicial = new Button[iniciales.Count];
        var retratosInicial = new Image[iniciales.Count];
        var nombresInicial = new Text[iniciales.Count];
        for (int i = 0; i < iniciales.Count; i++)
        {
            float x = (i - (iniciales.Count - 1) * 0.5f) * 340f;
            string nombre = string.IsNullOrEmpty(iniciales[i].Nombre) && iniciales[i].Definicion != null
                ? iniciales[i].Definicion.pokenName : iniciales[i].Nombre;
            (botonesInicial[i], retratosInicial[i]) = Ficha("Ficha" + nombre, pInicial.transform, new Vector2(x, 30f),
                new Vector2(280f, 270f), iniciales[i].Retrato, new Vector2(170f, 170f), nombre, panelGris, panelCeleste, i * 0.06f);
            nombresInicial[i] = botonesInicial[i].transform.Find("Nombre").GetComponent<Text>();
        }
        Text descripcion = Texto("Descripcion", pInicial.transform, "", 27, TextAnchor.MiddleCenter, Tinta, false);
        Ubicar(descripcion.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(1060f, 60f));
        Button contInicial = Boton("Continuar", pInicial.transform, "CONTINUAR  ›", botonGris, botonCeleste, true);
        Button volInicial = Boton("Volver", pInicial.transform, "‹  VOLVER", botonGris, botonCeleste, false);

        // ---- paso 4: confirmar ----
        GameObject pConfirmar = Paso("PanelConfirmar", marco.transform, "¿Todo listo?");
        Image resJugador = Imagen("RetratoJugador", pConfirmar.transform, caraVaron, Color.white, null);
        resJugador.preserveAspect = true;
        Ubicar(resJugador.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-400f, 30f), new Vector2(260f, 260f));
        Image resInicial = Imagen("RetratoInicial", pConfirmar.transform, iniciales[0].Retrato, Color.white, null);
        resInicial.preserveAspect = true;
        Ubicar(resInicial.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(400f, 30f), new Vector2(220f, 220f));
        Text resumen = Texto("Resumen", pConfirmar.transform, "", 32, TextAnchor.MiddleCenter, Tinta, false);
        resumen.supportRichText = true;
        resumen.lineSpacing = 1.15f;
        Ubicar(resumen.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(520f, 260f));
        Button empezar = Boton("Empezar", pConfirmar.transform, "¡EMPEZAR AVENTURA!", botonCeleste, botonCeleste, true);
        ((RectTransform)empezar.transform).sizeDelta = new Vector2(440f, 72f);
        Button volConfirmar = Boton("Volver", pConfirmar.transform, "‹  VOLVER", botonGris, botonCeleste, false);

        // ---- pie: las teclas ----
        Text ayuda = Texto("Ayuda", canvasGo.transform,
            AyudaDeTeclas, 24, TextAnchor.MiddleCenter, TintaSuave, false);
        Ubicar(ayuda.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1400f, 34f));

        // Arranca en el primer paso; la vista igual los ordena en Start.
        pNombre.SetActive(false);
        pInicial.SetActive(false);
        pConfirmar.SetActive(false);

        // ---- cableado ----
        var v = new SerializedObject(vista);
        v.FindProperty("panelGenero").objectReferenceValue = pGenero;
        v.FindProperty("panelNombre").objectReferenceValue = pNombre;
        v.FindProperty("panelInicial").objectReferenceValue = pInicial;
        v.FindProperty("panelConfirmar").objectReferenceValue = pConfirmar;
        Lista(v.FindProperty("etiquetasDePaso"), etiquetas);
        Lista(v.FindProperty("marcasDePaso"), marcas);
        v.FindProperty("botonVaron").objectReferenceValue = fichaVaron;
        v.FindProperty("botonMujer").objectReferenceValue = fichaMujer;
        v.FindProperty("retratoVaron").objectReferenceValue = retVaron;
        v.FindProperty("retratoMujer").objectReferenceValue = retMujer;
        v.FindProperty("continuarGenero").objectReferenceValue = contGenero;
        v.FindProperty("volverGenero").objectReferenceValue = volGenero;
        v.FindProperty("campoNombre").objectReferenceValue = campo;
        v.FindProperty("botonNombreOk").objectReferenceValue = contNombre;
        v.FindProperty("volverNombre").objectReferenceValue = volNombre;
        v.FindProperty("avisoNombre").objectReferenceValue = aviso;
        v.FindProperty("contadorNombre").objectReferenceValue = contador;
        v.FindProperty("largoMaximo").intValue = 10;
        Lista(v.FindProperty("botonesInicial"), botonesInicial);
        Lista(v.FindProperty("retratosInicial"), retratosInicial);
        Lista(v.FindProperty("nombresInicial"), nombresInicial);
        v.FindProperty("descripcionInicial").objectReferenceValue = descripcion;
        v.FindProperty("continuarInicial").objectReferenceValue = contInicial;
        v.FindProperty("volverInicial").objectReferenceValue = volInicial;
        v.FindProperty("nivelInicial").intValue = nivel;
        v.FindProperty("textoResumen").objectReferenceValue = resumen;
        v.FindProperty("retratoResumenJugador").objectReferenceValue = resJugador;
        v.FindProperty("retratoResumenInicial").objectReferenceValue = resInicial;
        v.FindProperty("botonEmpezar").objectReferenceValue = empezar;
        v.FindProperty("botonVolver").objectReferenceValue = volConfirmar;
        v.FindProperty("escenaDestino").stringValue = destino;

        SerializedProperty nuevas = v.FindProperty("iniciales");
        nuevas.arraySize = iniciales.Count;
        for (int i = 0; i < iniciales.Count; i++)
        {
            SerializedProperty e = nuevas.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("nombre").stringValue = iniciales[i].Nombre;
            e.FindPropertyRelative("definicion").objectReferenceValue = iniciales[i].Definicion;
            e.FindPropertyRelative("retrato").objectReferenceValue = iniciales[i].Retrato;
            e.FindPropertyRelative("descripcion").stringValue = iniciales[i].Descripcion;
        }
        v.ApplyModifiedPropertiesWithoutUndo();

        return Guardar(scene);
    }

    // ------------------------------------------------------------------ piezas

    private static readonly System.Action<RectTransform> Estirar = rt =>
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    };

    // Un paso: ocupa todo el marco, entra animado y trae su pregunta arriba.
    private static GameObject Paso(string nombre, Transform marco, string pregunta)
    {
        GameObject go = Nodo(nombre, marco);
        Estirar((RectTransform)go.transform);
        EntradaAnimada entrada = go.AddComponent<EntradaAnimada>();
        Poner(entrada, "duracion", 0.3f);
        Poner(entrada, "desde", new Vector2(0f, -30f));

        // En la franja de arriba del panel (la misma donde la tarjeta de
        // MenuSaves pone el nombre).
        Text titulo = Texto("Pregunta", go.transform, pregunta, 42, TextAnchor.MiddleCenter, Tinta, true);
        Ubicar(titulo.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 56f));
        Sombra(titulo);
        return go;
    }

    // Una ficha elegible: panel gris que se vuelve celeste con el cursor o al
    // quedar elegida, con su retrato y su nombre. Va envuelta en un nodo que
    // entra animado (con 'retraso' para el escalonado), asi la animacion de
    // entrada y la del boton no se pelean por la transparencia.
    private static (Button, Image) Ficha(string nombre, Transform padre, Vector2 pos, Vector2 tam, Sprite cara,
        Vector2 tamCara, string etiqueta, Sprite normal, Sprite elegido, float retraso)
    {
        GameObject envoltura = Nodo(nombre + "_Entrada", padre);
        Ubicar(envoltura, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, tam);
        EntradaAnimada entrada = envoltura.AddComponent<EntradaAnimada>();
        Poner(entrada, "retraso", 0.08f + retraso);
        Poner(entrada, "duracion", 0.3f);
        Poner(entrada, "desde", new Vector2(0f, -40f));

        Image fondo = Imagen(nombre, envoltura.transform, normal, Color.white, Estirar);
        fondo.type = Image.Type.Sliced;
        fondo.raycastTarget = true;

        // Lo celeste de "elegido" va arriba del gris y debajo del contenido.
        Image marca = Imagen("Elegido", fondo.transform, elegido, Color.white, Estirar);
        marca.type = Image.Type.Sliced;
        marca.gameObject.SetActive(false);

        Image retrato = Imagen("Retrato", fondo.transform, cara, Color.white, null);
        retrato.preserveAspect = true;
        Ubicar(retrato.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), tamCara);

        Text texto = Texto("Nombre", fondo.transform, etiqueta, 34, TextAnchor.MiddleCenter, Tinta, true);
        Ubicar(texto.gameObject, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(tam.x - 20f, 48f));
        Sombra(texto);

        Button boton = fondo.gameObject.AddComponent<Button>();
        boton.targetGraphic = fondo;
        boton.transition = Selectable.Transition.SpriteSwap;
        boton.spriteState = new SpriteState
        {
            highlightedSprite = elegido, selectedSprite = elegido, pressedSprite = elegido
        };

        BotonAnimado anim = fondo.gameObject.AddComponent<BotonAnimado>();
        Poner(anim, "escalaEncima", 1.05f);
        Poner(anim, "escalaApretado", 0.96f);
        Poner(anim, "marcaDeElegido", marca.gameObject);
        return (boton, retrato);
    }

    // Boton de abajo del marco: el de seguir a la derecha, el de volver a la izquierda.
    private static Button Boton(string nombre, Transform padre, string texto, Sprite normal, Sprite encima, bool derecha)
    {
        Image img = Imagen(nombre, padre, normal, Color.white, null);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        Vector2 ancla = derecha ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
        Ubicar(img.gameObject, ancla, ancla, new Vector2(derecha ? -36f : 36f, 34f), new Vector2(derecha ? 360f : 300f, 66f));

        Text t = Texto("Texto", img.transform, texto, 30, TextAnchor.MiddleCenter, Tinta, true);
        Estirar((RectTransform)t.transform);

        Button b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.SpriteSwap;
        b.spriteState = new SpriteState { highlightedSprite = encima, selectedSprite = encima, pressedSprite = encima };

        BotonAnimado anim = img.gameObject.AddComponent<BotonAnimado>();
        Poner(anim, "escalaEncima", 1.04f);
        Poner(anim, "escalaApretado", 0.96f);
        return b;
    }

    private static InputField CampoDeTexto(Transform padre, Sprite fondo)
    {
        Image img = Imagen("CampoNombre", padre, fondo, Color.white, null);
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;
        Ubicar(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(620f, 92f));

        Text texto = Texto("Texto", img.transform, "", 44, TextAnchor.MiddleCenter, Tinta, true);
        texto.supportRichText = false;
        Estirar((RectTransform)texto.transform);
        ((RectTransform)texto.transform).offsetMin = new Vector2(24f, 8f);
        ((RectTransform)texto.transform).offsetMax = new Vector2(-24f, -8f);

        Text placeholder = Texto("Placeholder", img.transform, "Escribí tu nombre", 40, TextAnchor.MiddleCenter, TintaSuave, false);
        placeholder.fontStyle = FontStyle.Italic;
        Estirar((RectTransform)placeholder.transform);

        InputField campo = img.gameObject.AddComponent<InputField>();
        campo.targetGraphic = img;
        campo.textComponent = texto;
        campo.placeholder = placeholder;
        campo.characterLimit = 10;
        campo.lineType = InputField.LineType.SingleLine;
        campo.caretWidth = 3;
        campo.customCaretColor = true;
        campo.caretColor = Celeste;
        campo.selectionColor = new Color(0.35f, 0.72f, 0.95f, 0.45f);
        campo.transition = Selectable.Transition.ColorTint;
        var colores = campo.colors;
        colores.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
        colores.selectedColor = new Color(0.8f, 0.93f, 1f, 1f);
        campo.colors = colores;
        return campo;
    }

    // ------------------------------------------------------------- utilidades

    private static GameObject Nodo(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return go;
    }

    private static void Ubicar(GameObject go, Vector2 ancla, Vector2 pivote, Vector2 pos, Vector2 tam)
    {
        var rt = (RectTransform)go.transform;
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = pivote;
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
    }

    private static Image Imagen(string nombre, Transform padre, Sprite sprite, Color color, System.Action<RectTransform> forma)
    {
        GameObject go = Nodo(nombre, padre);
        forma?.Invoke((RectTransform)go.transform);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Text Texto(string nombre, Transform padre, string contenido, int tam, TextAnchor ancla, Color color, bool negrita)
    {
        GameObject go = Nodo(nombre, padre);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text = contenido;
        t.fontSize = tam;
        t.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
        t.alignment = ancla;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static void Sombra(Text t)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        s.effectDistance = new Vector2(3f, -3f);
    }

    private static void Borrar(Transform padre, string nombre)
    {
        Transform viejo = padre.Find(nombre);
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);
    }

    private static Sprite SubSprite(string hoja, string nombre)
    {
        Sprite s = AssetDatabase.LoadAllAssetsAtPath(hoja).OfType<Sprite>().FirstOrDefault(x => x.name == nombre);
        if (s == null) Debug.LogWarning($"[Inicio] No encontre '{nombre}' en {hoja}.");
        return s;
    }

    // Las descripciones venian sin tildes.
    private static string ConTildes(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        return texto.Replace("La opcion segura", "La opción segura")
                    .Replace("Fragil", "Frágil")
                    .Replace("rapidisimo", "rapidísimo")
                    .Replace("Muy rapido", "Muy rápido");
    }

    private static void Lista(SerializedProperty prop, Object[] valores)
    {
        prop.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
    }

    private static void Poner(Object target, string campo, object valor)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Inicio] {target.GetType().Name} no tiene '{campo}'."); return; }
        switch (valor)
        {
            case float f: p.floatValue = f; break;
            case int i: p.intValue = i; break;
            case bool b: p.boolValue = b; break;
            case Vector2 v2: p.vector2Value = v2; break;
            case Object o: p.objectReferenceValue = o; break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Buscar<T>(Scene scene) where T : Component
        => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

    private static bool Guardar(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        bool ok = EditorSceneManager.SaveScene(scene);
        if (!ok) Debug.LogError($"[Inicio] No se pudo guardar {scene.path}.");
        return ok;
    }

    private static bool SinCambiosPendientes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene abierta = SceneManager.GetSceneAt(i);
            if (abierta.isDirty)
            {
                Debug.LogError($"[Inicio] '{abierta.name}' tiene cambios sin guardar: guardala o descartala y volve a correr esto.");
                return false;
            }
        }
        return true;
    }
}
