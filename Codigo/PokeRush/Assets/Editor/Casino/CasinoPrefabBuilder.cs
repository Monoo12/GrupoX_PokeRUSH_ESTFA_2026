using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Genera los prefabs y assets del casino y los engancha al sistema de menus que
// ya existe.
//
// A diferencia de MenuPrefabBuilder, este NO reconstruye el MenuSystem: lo abre,
// le AGREGA lo que le falte y lo vuelve a guardar. Asi el equipo puede seguir
// retocando a mano las pantallas del juego base sin que correr esto se las pise.
//
// La pantalla de slots se arma sobre el arte de tragamonedas que ya trae el pack
// (Graphics/UI/Slot Machine): la maquina, la sombra de los rodillos y la tira de
// simbolos Pokemon. El fondo mide 512x384, que es exactamente la resolucion de
// referencia del Canvas, asi que las posiciones de abajo son pixeles del dibujo.
public static class CasinoPrefabBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI/Casino";
    private const string DefinitionFolder = "Assets/Resources/Definitions/Casino";
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";

    // Medidas leidas del propio dibujo (bg.png).
    private const float VentanaX0 = 64f;    // borde izquierdo del rodillo 1
    private const float VentanaY0 = 110f;   // borde superior de los rodillos
    private const float PasoRodillo = 80f;  // de un rodillo al siguiente
    private const float AnchoCelda = 64f;
    private const float AltoCelda = 48f;

    [MenuItem("PokeRush/Casino/Construir pantallas del casino")]
    public static void BuildAll()
    {
        MenuUiKit.EnsureFolder(PrefabFolder);
        MenuUiKit.EnsureFolder(DefinitionFolder);

        CasinoSlotArtImporter.ImportarTodo();

        SlotMachineDefinition maquina = BuildMaquinaBasica();
        GameObject slots = BuildSlotsScreen();

        RouletteTableDefinition mesa = BuildMesaDeRuleta();
        GameObject ruleta = BuildRouletteScreen();

        RegistrarEnMenuSystem(new List<GameObject> { slots, ruleta });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Casino] Pantallas en {PrefabFolder}; maquina '{maquina.name}' y " +
                  $"mesa '{mesa.name}' en {DefinitionFolder}. Registradas en MenuSystem.");
    }

    // ------------------------------------------------------------- definicion

    // La maquina del piso 1, con los simbolos del Game Corner que trae el pack.
    //
    // La tabla de pagos respeta la que esta DIBUJADA abajo del gabinete (777 =
    // 300, 777 azul = 90, caritas = 15, bichos = 8, cereza = 2), y los pesos
    // estan elegidos para que la maquina devuelva ~86% de lo apostado: la casa
    // gana a la larga pero una racha existe. Son tres lineas, las tres filas,
    // que es lo que marcan los indicadores 1/2/3 del dibujo.
    //
    // Al tocar estos numeros, correr PokeRush/Casino/Correr smoke tests del
    // casino: reporta la devolucion resultante y falla si la maquina regala.
    private static SlotMachineDefinition BuildMaquinaBasica()
    {
        string path = DefinitionFolder + "/MaquinaBasica.asset";

        var def = AssetDatabase.LoadAssetAtPath<SlotMachineDefinition>(path);
        bool nueva = def == null;
        if (nueva) def = ScriptableObject.CreateInstance<SlotMachineDefinition>();

        Sprite[] iconos = CasinoSlotArtImporter.CargarSimbolos();
        Sprite Icono(int i) => iconos != null && i < iconos.Length ? iconos[i] : null;

        def.maquinaNombre = "TRAGAPERRAS";
        def.descripcion = "Maquina del piso 1 del casino.";
        def.rodillos = 3;
        def.filas = 3;
        def.apuestaMinima = 10;
        def.apuestaMaxima = 100;
        def.pasoDeApuesta = 10;
        def.duracionDelGiro = 0.9f;
        def.retrasoEntreRodillos = 0.3f;

        def.simbolos = new[]
        {
            Simbolo("Cereza",    Icono(0), 60, 2),
            Simbolo("Magnemite", Icono(1), 48, 8),
            Simbolo("Gastly",    Icono(2), 36, 8),
            Simbolo("Psyduck",   Icono(4), 26, 15),
            Simbolo("Pikachu",   Icono(3), 18, 15),
            Simbolo("Siete azul", Icono(6), 8, 90),
            Simbolo("Siete rojo", Icono(5), 3, 300),
            Comodin("Replay",    Icono(7), 1, 500)
        };

        // Las tres filas, como los indicadores 1/2/3 del gabinete.
        def.lineas = new[]
        {
            Linea("Linea 1 (arriba)", 0, 0, 0),
            Linea("Linea 2 (medio)",  1, 1, 1),
            Linea("Linea 3 (abajo)",  2, 2, 2)
        };

        if (nueva) AssetDatabase.CreateAsset(def, path);
        EditorUtility.SetDirty(def);
        return def;
    }

    private static SlotSymbol Simbolo(string nombre, Sprite icono, int peso, int pagoPorTres)
        => new SlotSymbol
        {
            nombre = nombre,
            icono = icono,
            letra = nombre.Substring(0, 1),
            peso = peso,
            pagos = new[] { 0, pagoPorTres }
        };

    private static SlotSymbol Comodin(string nombre, Sprite icono, int peso, int pagoPorTres)
    {
        SlotSymbol s = Simbolo(nombre, icono, peso, pagoPorTres);
        s.comodin = true;
        return s;
    }

    private static SlotPayline Linea(string nombre, params int[] filas)
        => new SlotPayline { nombre = nombre, filas = filas };

    // ----------------------------------------------------------------- ruleta

    // La mesa del piso 2.
    //
    // El objetivo del piso es SACAR EL POKEMON: es la casilla marcada con
    // cumpleObjetivo, y al salir activa el flag que mira el ascensor. Su peso
    // (6 de 100) es lo que decide cuanto cuesta pasar de piso, y se toca desde
    // el Inspector sin abrir un script.
    //
    // La mesa no regala: entre "Nada", perder monedas, perder un objeto y el
    // golpe al equipo, la mitad de las casillas juegan en contra.
    private static RouletteTableDefinition BuildMesaDeRuleta()
    {
        string path = DefinitionFolder + "/MesaDeRuleta.asset";

        var def = AssetDatabase.LoadAssetAtPath<RouletteTableDefinition>(path);
        bool nueva = def == null;
        if (nueva) def = ScriptableObject.CreateInstance<RouletteTableDefinition>();

        def.mesaNombre = "RULETA";
        def.costoPorGiro = 50;
        def.duracionDelGiro = 2.5f;
        def.vueltas = 4;
        def.flagObjetivo = StoryFlag.Casino_Ruleta_ObjetivoCumplido;

        var pocion = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(
            "Assets/Resources/Definitions/Items/Pocion.asset");
        var pikachu = AssetDatabase.LoadAssetAtPath<PokenDefinition>(
            "Assets/Resources/Definitions/Pokens/0025_Pikachu.asset");

        def.premios = new[]
        {
            Casilla("Nada", new Color(0.28f, 0.30f, 0.36f), 24, RoulettePrizeKind.Nada,
                    "La bolita cae en el cero. Nada."),
            Casilla("Monedas", new Color(0.95f, 0.78f, 0.25f), 24, RoulettePrizeKind.Dinero,
                    "Pagan {0}.", monto: 60),
            Casilla("Multa", new Color(0.75f, 0.28f, 0.30f), 14, RoulettePrizeKind.PerderDinero,
                    "La casa se cobra {0}.", monto: 80),
            Casilla("Objeto", new Color(0.35f, 0.72f, 0.45f), 12, RoulettePrizeKind.Objeto,
                    "Te dan {0}.", objeto: pocion, cantidad: 2),
            Casilla("Descarga", new Color(0.55f, 0.35f, 0.75f), 10, RoulettePrizeKind.DanioAlEquipo,
                    "Salta una chispa de la mesa: {0}.", monto: 25),
            Casilla("Premio grande", new Color(0.98f, 0.55f, 0.15f), 6, RoulettePrizeKind.Dinero,
                    "Premio grande! {0}.", monto: 250),
            Casilla("Manotazo", new Color(0.55f, 0.30f, 0.25f), 4, RoulettePrizeKind.PerderObjeto,
                    "Alguien te saca {0} de la mochila."),
            Objetivo("Pokemon", new Color(0.35f, 0.80f, 0.95f), 6, pikachu, 15,
                     "Sale el premio gordo: {0}!")
        };

        if (nueva) AssetDatabase.CreateAsset(def, path);
        EditorUtility.SetDirty(def);
        return def;
    }

    private static RoulettePrize Casilla(string nombre, Color color, int peso,
                                         RoulettePrizeKind tipo, string mensaje,
                                         int monto = 0, HealItemDefinition objeto = null,
                                         int cantidad = 1)
        => new RoulettePrize
        {
            nombre = nombre, color = color, peso = peso, tipo = tipo,
            mensaje = mensaje, monto = monto, objeto = objeto, cantidad = cantidad
        };

    private static RoulettePrize Objetivo(string nombre, Color color, int peso,
                                          PokenDefinition especie, int nivel, string mensaje)
        => new RoulettePrize
        {
            nombre = nombre, color = color, peso = peso,
            tipo = RoulettePrizeKind.Poken, poken = especie, nivel = nivel,
            cumpleObjetivo = true, mensaje = mensaje
        };

    private static GameObject BuildRouletteScreen()
    {
        GameObject root = new GameObject("RouletteScreen", typeof(RectTransform));
        MenuUiKit.Full(root);

        var screen = root.AddComponent<RouletteScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Roulette);

        GameObject velo = MenuUiKit.Node("Oscurecedor", root.transform);
        MenuUiKit.Full(velo);
        MenuUiKit.AddImage(velo, null, new Color(0f, 0f, 0f, 0.72f));

        // Tapete verde y marco dorado, los dos del pack: la sala se ve como una
        // sala y no como un cuadrado de color.
        GameObject exterior;
        GameObject panel = CasinoUiKit.Panel("Panel", root.transform,
            new Vector2(500f, 364f), out exterior);

        Text titulo = MenuUiKit.Label("Titulo", panel.transform, Vector2.zero,
            new Vector2(440f, 24f), "RULETA", 18, TextAnchor.UpperCenter, MenuUiKit.Gold);
        MenuUiKit.Box(titulo.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -10f), new Vector2(440f, 24f));

        // La rueda ocupa la mitad izquierda del panel: la columna de texto
        // arranca en x=248, asi que no se pisan.
        GameObject ruedaRaiz = MenuUiKit.Node("Ruleta", panel.transform);
        MenuUiKit.Box(ruedaRaiz, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(18f, -6f), new Vector2(216f, 216f));

        // Aro de madera con borde dorado, atras de todo: las porciones se dibujan
        // adentro y lo que asoma es el marco de la ruleta de verdad.
        GameObject aro = MenuUiKit.Node("Aro", ruedaRaiz.transform);
        MenuUiKit.Box(aro, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(232f, 226f));
        Image aroImg = MenuUiKit.AddImage(aro, CasinoUiKit.Aro(), Color.white);
        aroImg.raycastTarget = false;

        GameObject giratorio = MenuUiKit.Node("Rueda", ruedaRaiz.transform);
        MenuUiKit.Box(giratorio, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(188f, 188f));

        // Buje metalico: tapa el vertice donde se juntan todas las porciones.
        GameObject centro = MenuUiKit.Node("Centro", giratorio.transform);
        MenuUiKit.Box(centro, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(56f, 56f));
        Image tapa = MenuUiKit.AddImage(centro, CasinoUiKit.Centro(), Color.white);
        tapa.raycastTarget = false;

        GameObject aguja = MenuUiKit.Node("Aguja", ruedaRaiz.transform);
        MenuUiKit.Box(aguja, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 6f), new Vector2(24f, 24f));
        Image puntero = MenuUiKit.AddImage(aguja, TrianguloBlanco(), MenuUiKit.Gold);
        puntero.raycastTarget = false;

        var widget = ruedaRaiz.AddComponent<RouletteWheelWidget>();
        MenuUiKit.SetField(widget, "circulo", CirculoBlanco());
        MenuUiKit.SetField(widget, "rueda", giratorio.GetComponent<RectTransform>());
        MenuUiKit.SetField(widget, "centro", centro.GetComponent<RectTransform>());

        Text saldo = MenuUiKit.Label("Saldo", panel.transform, Vector2.zero,
            new Vector2(120f, 22f), "$0", 16, TextAnchor.UpperRight, MenuUiKit.Ink);
        MenuUiKit.Box(saldo.gameObject, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-16f, -12f), new Vector2(120f, 22f));

        GameObject cajaTabla = CasinoUiKit.Cartel("CajaTabla", panel.transform,
            new Vector2(244f, -38f), new Vector2(214f, 166f),
            new Color(0.05f, 0.12f, 0.09f, 0.92f));
        Text tabla = MenuUiKit.Label("Tabla", cajaTabla.transform, Vector2.zero,
            new Vector2(186f, 138f), "", 13, TextAnchor.UpperLeft, MenuUiKit.Ink);
        MenuUiKit.Box(tabla.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -14f), new Vector2(186f, 138f));
        tabla.lineSpacing = 1.15f;

        // Ficha de casino al lado del precio del giro: se lee de un vistazo que
        // eso es lo que se apuesta.
        GameObject ficha = MenuUiKit.Node("Ficha", panel.transform);
        MenuUiKit.Box(ficha, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(248f, -210f), new Vector2(26f, 26f));
        Image fichaImg = MenuUiKit.AddImage(ficha, CasinoUiKit.Ficha(8), Color.white);
        fichaImg.raycastTarget = false;

        Text costo = MenuUiKit.Label("Costo", panel.transform, Vector2.zero,
            new Vector2(196f, 22f), "GIRO  50", 16, TextAnchor.MiddleLeft, MenuUiKit.Accent);
        MenuUiKit.Box(costo.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(280f, -212f), new Vector2(168f, 22f));

        GameObject cajaMensaje = CasinoUiKit.Cartel("CajaMensaje", panel.transform,
            new Vector2(244f, -240f), new Vector2(214f, 72f),
            new Color(0.05f, 0.12f, 0.09f, 0.92f));
        Text mensaje = MenuUiKit.Label("Mensaje", cajaMensaje.transform, Vector2.zero,
            new Vector2(186f, 44f), "", 14, TextAnchor.UpperLeft, MenuUiKit.Ink);
        MenuUiKit.Box(mensaje.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(14f, -14f), new Vector2(186f, 44f));
        mensaje.horizontalOverflow = HorizontalWrapMode.Wrap;

        // En una sola linea: dos renglones no entraban adentro del marco y se
        // salian del panel.
        Text ayuda = MenuUiKit.Label("Ayuda", panel.transform, Vector2.zero,
            new Vector2(210f, 22f), "X  girar        Z  salir", 14,
            TextAnchor.MiddleLeft, MenuUiKit.Border);
        MenuUiKit.Box(ayuda.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(250f, -314f), new Vector2(210f, 22f));

        MenuUiKit.SetField(screen, "tituloText", titulo);
        MenuUiKit.SetField(screen, "saldoText", saldo);
        MenuUiKit.SetField(screen, "costoText", costo);
        MenuUiKit.SetField(screen, "mensajeText", mensaje);
        MenuUiKit.SetField(screen, "tablaText", tabla);
        MenuUiKit.SetField(screen, "rueda", widget);

        root.SetActive(false);
        return MenuUiKit.SavePrefab(root, PrefabFolder + "/RouletteScreen.prefab");
    }

    // El pack no trae ruleta, asi que el circulo y la aguja se generan una vez y
    // quedan como assets normales: se reemplazan por arte de verdad soltando
    // otro sprite en su lugar.

    private const string SpriteFolder = "Assets/Sprites/UI/Casino";

    private static Sprite CirculoBlanco() => SpriteGenerado("circulo.png", 256, 256, (x, y, w, h) =>
    {
        float dx = x - w / 2f + 0.5f, dy = y - h / 2f + 0.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        return r <= w / 2f - 1f ? 1f : Mathf.Clamp01(w / 2f - r);
    });

    private static Sprite TrianguloBlanco() => SpriteGenerado("aguja.png", 64, 64, (x, y, w, h) =>
    {
        // Apunta hacia ABAJO: la aguja marca la casilla que quedo arriba.
        float t = 1f - y / (float)h;
        float mitad = (w / 2f) * t;
        return Mathf.Abs(x - w / 2f + 0.5f) <= mitad ? 1f : 0f;
    });

    private static Sprite SpriteGenerado(string archivo, int ancho, int alto,
                                         System.Func<int, int, int, int, float> alfa)
    {
        string path = SpriteFolder + "/" + archivo;
        var existente = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existente != null) return existente;

        MenuUiKit.EnsureFolder(SpriteFolder);

        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        for (int y = 0; y < alto; y++)
            for (int x = 0; x < ancho; x++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alfa(x, y, ancho, alto)));
        tex.Apply();

        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------------------------------------------------------------- pantalla

    private static GameObject BuildSlotsScreen()
    {
        GameObject root = new GameObject("SlotsScreen", typeof(RectTransform));
        MenuUiKit.Full(root);

        var screen = root.AddComponent<SlotsScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Slots);

        // Oscurecedor: la maquina mide 512x384 y en pantallas mas anchas quedan
        // franjas a los costados. Sin esto se ve el mapa del casino al lado de la
        // maquina y no se entiende que hay un menu abierto.
        GameObject velo = MenuUiKit.Node("Oscurecedor", root.transform);
        MenuUiKit.Full(velo);
        MenuUiKit.AddImage(velo, null, new Color(0f, 0f, 0f, 0.72f));

        // El gabinete entero. Mide 512x384 igual que el Canvas: entra clavado.
        GameObject fondo = MenuUiKit.Node("Maquina", root.transform);
        MenuUiKit.Box(fondo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));
        MenuUiKit.AddImage(fondo, CasinoSlotArtImporter.CargarSprite(CasinoSlotArtImporter.Maquina),
            Color.white);

        // Las casillas, encima de las ventanas de los rodillos. Se crean rodillo
        // por rodillo y de arriba hacia abajo: ese ES el orden que espera
        // SlotsScreen.
        var celdas = new List<Object>();
        for (int r = 0; r < 3; r++)
            for (int f = 0; f < 3; f++)
                celdas.Add(BuildCelda($"Celda_R{r}F{f}", fondo.transform,
                    new Vector2(VentanaX0 + PasoRodillo * r, VentanaY0 + AltoCelda * f)));
        MenuUiKit.SetArrayField(screen, "celdas", celdas.ToArray());

        // Sombra curva del tambor, encima de los simbolos.
        Sprite sombra = CasinoSlotArtImporter.CargarSprite(CasinoSlotArtImporter.Sombra);
        for (int r = 0; r < 3; r++)
        {
            GameObject go = MenuUiKit.Node($"Sombra_R{r}", fondo.transform);
            Arriba(go, new Vector2(VentanaX0 + PasoRodillo * r, VentanaY0),
                new Vector2(AnchoCelda, AltoCelda * 3f));
            Image img = MenuUiKit.AddImage(go, sombra, Color.white);
            img.raycastTarget = false;
        }

        // Los dos visores del gabinete ya dicen CREDITO y PREMIO en el dibujo:
        // aca solo van los numeros, calzados adentro de cada ventanita.
        Text credito = Numero("Credito", fondo.transform, new Vector2(360f, 61f));
        Text premio = Numero("Premio", fondo.transform, new Vector2(437f, 61f));

        // El panel negro de la derecha es el sitio para hablarle al jugador.
        Text mensaje = MenuUiKit.Label("Mensaje", fondo.transform, Vector2.zero,
            new Vector2(140f, 150f), "", 12, TextAnchor.UpperLeft, MenuUiKit.Ink);
        Arriba(mensaje.gameObject, new Vector2(364f, 104f), new Vector2(140f, 150f));
        mensaje.horizontalOverflow = HorizontalWrapMode.Wrap;

        Text apuesta = MenuUiKit.Label("Apuesta", fondo.transform, Vector2.zero,
            new Vector2(140f, 22f), "APUESTA 10", 16, TextAnchor.MiddleLeft, MenuUiKit.Gold);
        Arriba(apuesta.gameObject, new Vector2(364f, 214f), new Vector2(140f, 22f));

        // La ayuda entra en el panel negro con aire de sobra: apretada contra el
        // borde de abajo, con las lineas pisandose, es como se veia mal.
        Text ayuda = MenuUiKit.Label("Ayuda", fondo.transform, Vector2.zero,
            new Vector2(140f, 66f), "IZQ/DER  apuesta\nX  girar\nZ  salir",
            14, TextAnchor.UpperLeft, MenuUiKit.Border);
        ayuda.lineSpacing = 1.25f;
        Arriba(ayuda.gameObject, new Vector2(364f, 240f), new Vector2(140f, 66f));

        // El titulo no va: el gabinete ya tiene el suyo dibujado.
        MenuUiKit.SetField(screen, "tituloText", null);
        MenuUiKit.SetField(screen, "saldoText", credito);
        MenuUiKit.SetField(screen, "premioText", premio);
        MenuUiKit.SetField(screen, "apuestaText", apuesta);
        MenuUiKit.SetField(screen, "mensajeText", mensaje);

        root.SetActive(false);
        return MenuUiKit.SavePrefab(root, PrefabFolder + "/SlotsScreen.prefab");
    }

    private static SlotCellWidget BuildCelda(string nombre, Transform padre, Vector2 posArriba)
    {
        GameObject celda = MenuUiKit.Node(nombre, padre);
        Arriba(celda, posArriba, new Vector2(AnchoCelda, AltoCelda));

        // Fondo del tambor: el blanco hueso del dibujo, para que los simbolos
        // con transparencia no dejen ver el gabinete por atras.
        Image fondo = MenuUiKit.AddImage(celda, null, new Color(0.94f, 0.94f, 0.97f, 1f));

        GameObject iconoGo = MenuUiKit.Node("Icono", celda.transform);
        MenuUiKit.Box(iconoGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(AnchoCelda, AltoCelda));
        Image icono = MenuUiKit.AddImage(iconoGo, null, Color.white);
        icono.enabled = false;
        icono.preserveAspect = true;

        // Respaldo para cuando un simbolo no tenga sprite cargado.
        Text letra = MenuUiKit.Label("Letra", celda.transform, Vector2.zero,
            new Vector2(AnchoCelda, AltoCelda), "", 20, TextAnchor.MiddleCenter,
            MenuUiKit.InkDark);
        letra.enabled = false;

        GameObject resaltado = MenuUiKit.Node("Resaltado", celda.transform);
        MenuUiKit.Box(resaltado, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(AnchoCelda, AltoCelda));
        Image marco = MenuUiKit.AddImage(resaltado, null, new Color(1f, 0.9f, 0.2f, 0.4f));
        marco.raycastTarget = false;
        resaltado.SetActive(false);

        var widget = celda.AddComponent<SlotCellWidget>();
        MenuUiKit.SetField(widget, "fondo", fondo);
        MenuUiKit.SetField(widget, "icono", icono);
        MenuUiKit.SetField(widget, "letra", letra);
        MenuUiKit.SetField(widget, "resaltado", resaltado);
        return widget;
    }

    // Numero de visor: alineado a la derecha, como los contadores del gabinete.
    //
    // Va sobre una tapa negra porque el dibujo trae los digitos '88888' apagados
    // pintados adentro de la ventanita: sin taparlos, el numero de verdad se lee
    // encima de ellos y no se entiende nada.
    private static Text Numero(string nombre, Transform padre, Vector2 posArriba)
    {
        GameObject tapa = MenuUiKit.Node(nombre + "Fondo", padre);
        Arriba(tapa, posArriba, new Vector2(70f, 22f));
        MenuUiKit.AddImage(tapa, null, Color.black);

        Text t = MenuUiKit.Label(nombre, tapa.transform, Vector2.zero, new Vector2(64f, 20f),
            "0", 16, TextAnchor.MiddleRight, MenuUiKit.Gold);
        MenuUiKit.Box(t.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-2f, 0f), new Vector2(64f, 20f));
        return t;
    }

    // Coloca por pixel del dibujo, contando desde arriba a la izquierda.
    private static void Arriba(GameObject go, Vector2 posArriba, Vector2 size)
    {
        MenuUiKit.Box(go, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(posArriba.x, -posArriba.y), size);
    }

    // ------------------------------------------------------------ MenuSystem

    // Agrega al MenuSystem las pantallas del casino que le falten, sin tocar las
    // que ya estaban, y le deja el arreglo de nitidez de la fuente.
    private static void RegistrarEnMenuSystem(List<GameObject> pantallas)
    {
        GameObject contenido = PrefabUtility.LoadPrefabContents(MenuSystemPath);
        if (contenido == null)
        {
            Debug.LogError($"[Casino] No se encontro {MenuSystemPath}. " +
                           "Genera primero el sistema de menus con PokeRush/UI.");
            return;
        }

        try
        {
            var manager = contenido.GetComponent<MenuManager>();
            if (manager == null)
            {
                Debug.LogError("[Casino] El prefab MenuSystem no tiene MenuManager.");
                return;
            }

            bool cambio = false;

            if (contenido.GetComponent<PixelFontCrisp>() == null)
            {
                contenido.AddComponent<PixelFontCrisp>();
                cambio = true;
                Debug.Log("[Casino] MenuSystem: agregado PixelFontCrisp (texto de pixel nitido).");
            }

            var so = new SerializedObject(manager);
            SerializedProperty lista = so.FindProperty("screenPrefabs");

            var yaEstan = new HashSet<Object>();
            for (int i = 0; i < lista.arraySize; i++)
                yaEstan.Add(lista.GetArrayElementAtIndex(i).objectReferenceValue);

            foreach (GameObject pantalla in pantallas)
            {
                var screen = pantalla != null ? pantalla.GetComponent<MenuScreen>() : null;
                if (screen == null || yaEstan.Contains(screen)) continue;

                lista.InsertArrayElementAtIndex(lista.arraySize);
                lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = screen;
                cambio = true;
            }

            if (!cambio) return;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contenido, MenuSystemPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
