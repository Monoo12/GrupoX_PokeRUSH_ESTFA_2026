using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Arma la pantalla de la mesa de ruleta y la registra en el MenuSystem.
//
// Igual que el resto de los builders del casino: NO reconstruye el sistema de
// menus, le agrega la pantalla que le falta y lo vuelve a guardar, asi lo que
// el equipo retoco a mano no se pisa.
//
// El arte sale de Assets/Ruleta Nueva (los roulette_*.png). Casi todo es la
// version recortada y recoloreada de sprite_sheet_completo.png; la rueda es la
// excepcion y sale de ruleta_base_2.png, que es un dibujo aparte y mucho mejor.
// Ver RouletteArtImporter y scripts/limpiar_ruleta.py para el por que.
//
// Las medidas de abajo son pixeles de la resolucion de referencia del Canvas
// (512x384), con el origen en el centro. La idea del acomodo:
//
//   izquierda  la rueda (el elemento protagonista), y debajo el resultado y
//              el historial, que es lo que se mira despues de cada tirada;
//   derecha    todo lo que se toca: la grilla de numeros, rojo/negro, las
//              fichas y los botones, de arriba hacia abajo en el orden en que
//              se usan.
//
// La mesa dibujada (roulette_table.png) NO se usa de fondo: trae los 37
// numeros pintados encima y abajo van los de verdad, que tienen que poder
// resaltarse de a uno. Por eso la grilla se apoya en roulette_felt.png, que es
// un panel liso de 9 tajadas con los mismos colores sacados de esa mesa.
public static class RouletteTableBuilder
{
    private const string ArtFolder = "Assets/Ruleta Nueva";
    private const string PrefabFolder = "Assets/Prefabs/UI/Casino";
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";

    // ---- rueda ----
    private static readonly Vector2 WheelPos = new Vector2(-172f, 40f);
    private const float WheelLado = 152f;

    // Radios de la bola, en unidades del Canvas. Salen de medir
    // ruleta_base_2.png: el circulo de la rueda ocupa 0.9955 del sprite, la
    // pista lisa por la que corre la bola esta a 0.80 de ese radio y el anillo
    // de casilleros a 0.52 (ver RUEDA_PISTA / RUEDA_CASILLEROS en
    // scripts/limpiar_ruleta.py). O sea 152/2 * 0.9955 * 0.80 y * 0.52.
    private const float BallOuterRadius = 60.5f;
    private const float BallPocketRadius = 39.5f;

    // ---- grilla ----
    private static readonly Vector2 CeldaSize = new Vector2(21f, 27f);
    private const float ColumnaPaso = 23f;
    private const float FilaPaso = 30f;
    private const float GrillaX0 = -30f;      // centro de la primera columna
    private const float GrillaY0 = 118f;      // centro de la primera fila

    // El cero ocupa las tres filas de alto, como en una mesa de verdad. Antes
    // era una celda suelta arriba y debajo quedaba un hueco de fieltro que se
    // leia como una columna a medio dibujar.
    private static readonly Vector2 CeroPos = new Vector2(-58f, 88f);
    private static readonly Vector2 CeroSize = new Vector2(22f, 87f);

    // Fondo de la grilla: tiene que cubrir del borde del cero (-69) al de la
    // ultima columna (233), mas los 10 px de marco dorado del propio panel.
    private static readonly Vector2 FondoGrillaPos = new Vector2(82f, 88f);
    private static readonly Vector2 FondoGrillaSize = new Vector2(336f, 116f);

    // ---- resto ----
    private const float ColoresY = 1f;
    private const float FichasY = -50f;
    private const float BotonesY = -104f;

    private static readonly Color Tinta = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TintaSuave = new Color(0.93f, 0.95f, 1f, 1f);

    [MenuItem("PokeRush/Casino/Construir la mesa de ruleta")]
    public static void BuildAll()
    {
        RouletteArtImporter.ImportarTodo();
        MenuUiKit.EnsureFolder(PrefabFolder);

        GameObject pantalla = BuildScreen();
        RegistrarEnMenuSystem(pantalla);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Ruleta] Mesa generada en {PrefabFolder}/RouletteScreen.prefab " +
                  "y registrada en el MenuSystem.");
    }

    // Los settings de import los pone RouletteArtImporter, que corre al
    // principio de BuildAll. Aca NO se usa MenuUiKit.Sprite() a proposito: ese
    // helper fuerza Point y ppu 100 porque el resto de los menus son pixel art,
    // y pisaria el Bilinear y el ppu 200 del fieltro que el importer acaba de
    // dejar puestos. Se paso una vez: el arte salia en Point igual y se veia
    // sucio al agrandarse 3,75x.
    private static Sprite Art(string nombre)
        => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{nombre}.png");

    // ------------------------------------------------------------- pantalla

    private static GameObject BuildScreen()
    {
        GameObject root = new GameObject("RouletteScreen", typeof(RectTransform));
        MenuUiKit.Full(root);

        var screen = root.AddComponent<RouletteScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Roulette);

        // Marco de 512x384 centrado: este nodo existe solo para que las
        // coordenadas de adentro sean pixeles del arte y no porcentajes de una
        // pantalla que cambia de ancho.
        GameObject marco = MenuUiKit.Node("Marco", root.transform);
        MenuUiKit.Box(marco, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));

        ArmarCabecera(marco.transform, screen);
        ArmarRueda(marco.transform, screen);
        ArmarResultadoEHistorial(marco.transform, screen);
        ArmarGrilla(marco.transform, screen);
        ArmarColores(marco.transform, screen);
        ArmarFichas(marco.transform, screen);
        ArmarBotones(marco.transform, screen);
        ArmarEfectos(marco.transform, screen);

        MenuUiKit.SetField(screen, "ballOuterRadius", BallOuterRadius);
        MenuUiKit.SetField(screen, "ballPocketRadius", BallPocketRadius);
        MenuUiKit.SetArrayField(screen, "resultChipSprites", new Object[]
        {
            Art("roulette_cell_red"), Art("roulette_cell_black"), Art("roulette_cell_green")
        });

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/RouletteScreen.prefab");
    }

    // -------------------------------------------------------------- cabecera

    private static void ArmarCabecera(Transform padre, RouletteScreen screen)
    {
        Imagen(padre, "Titulo", "roulette_title", new Vector2(-176f, 152f), new Vector2(132f, 57f));

        // La pila de fichas hace de icono de la plata: se lee de una y evita
        // tener que escribir "dinero" al lado del numero. Va en la banda de
        // arriba, que es la unica franja libre entre el titulo y la grilla.
        Imagen(padre, "PilaDeFichas", "roulette_stack_gold", new Vector2(-80f, 168f),
            new Vector2(14f, 30f));

        Text plata = MenuUiKit.Label("Plata", padre, new Vector2(14f, 168f), new Vector2(160f, 24f),
            "0", 18, TextAnchor.MiddleLeft, MenuUiKit.Gold, new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(plata);
        MenuUiKit.SetField(screen, "moneyLabel", plata);

        Imagen(padre, "Subtitulo", "roulette_subtitle", new Vector2(180f, 152f),
            new Vector2(122f, 26f));

        Text ayuda = MenuUiKit.Label("Ayuda", padre, new Vector2(84f, -142f), new Vector2(300f, 20f),
            "", 12, TextAnchor.MiddleCenter, TintaSuave, new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(ayuda);
        MenuUiKit.SetField(screen, "hintLabel", ayuda);
    }

    // ------------------------------------------------------------------ rueda

    private static void ArmarRueda(Transform padre, RouletteScreen screen)
    {
        GameObject contenedor = MenuUiKit.Node("Rueda", padre);
        MenuUiKit.Box(contenedor, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            WheelPos, new Vector2(WheelLado, WheelLado));

        // Lo unico que rota. La bola cuelga del contenedor, NO de la rueda: si
        // colgara de la rueda giraria con ella y nunca se veria correr.
        GameObject rueda = MenuUiKit.Node("Disco", contenedor.transform);
        MenuUiKit.Full(rueda);
        MenuUiKit.AddImage(rueda, Art("roulette_wheel"), Color.white);
        MenuUiKit.SetField(screen, "wheel", (RectTransform)rueda.transform);

        GameObject bola = MenuUiKit.Node("Bola", contenedor.transform);
        MenuUiKit.Box(bola, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, BallOuterRadius), new Vector2(14f, 14f));
        MenuUiKit.AddImage(bola, Art("roulette_ball"), Color.white);
        MenuUiKit.SetField(screen, "ball", (RectTransform)bola.transform);
    }

    private static void ArmarResultadoEHistorial(Transform padre, RouletteScreen screen)
    {
        // ---- ultimo resultado ----
        Imagen(padre, "PanelResultado", "roulette_panel_result", new Vector2(-172f, -70f),
            new Vector2(86f, 46f));

        GameObject chip = MenuUiKit.Node("ResultadoChip", padre);
        MenuUiKit.Box(chip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-172f, -78f), new Vector2(30f, 34f));
        Image chipImg = MenuUiKit.AddImage(chip, Art("roulette_cell_green"), Color.white);
        MenuUiKit.SetField(screen, "resultChip", chipImg);

        Text num = MenuUiKit.Label("ResultadoNum", chip.transform, Vector2.zero,
            new Vector2(30f, 34f), "-", 18, TextAnchor.MiddleCenter, Tinta,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(num);
        MenuUiKit.SetField(screen, "resultLabel", num);

        // ---- historial ----
        Imagen(padre, "PanelHistorial", "roulette_history", new Vector2(-172f, -140f),
            new Vector2(168f, 49f));

        var chips = new List<Object>();
        var labels = new List<Object>();
        for (int i = 0; i < 10; i++)
        {
            float x = -239.5f + i * 15f;
            GameObject h = MenuUiKit.Node($"Hist{i}", padre);
            MenuUiKit.Box(h, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, -148f), new Vector2(12f, 14f));
            chips.Add(MenuUiKit.AddImage(h, Art("roulette_cell_green"), Color.white));

            Text t = MenuUiKit.Label("N", h.transform, Vector2.zero, new Vector2(12f, 14f),
                "", 9, TextAnchor.MiddleCenter, Tinta, new Vector2(0.5f, 0.5f));
            labels.Add(t);
            // Arrancan apagadas: hasta que no haya tirada no hay nada que
            // mostrar, y diez casillas verdes vacias se leen como un error.
            h.SetActive(false);
        }

        MenuUiKit.SetArrayField(screen, "historyChips", chips.ToArray());
        MenuUiKit.SetArrayField(screen, "historyLabels", labels.ToArray());
    }

    // ----------------------------------------------------------------- grilla

    private static void ArmarGrilla(Transform padre, RouletteScreen screen)
    {
        // Fondo liso detras de los 37 casilleros.
        GameObject fondo = MenuUiKit.Node("FondoGrilla", padre);
        MenuUiKit.Box(fondo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            FondoGrillaPos, FondoGrillaSize);
        Image fondoImg = MenuUiKit.AddImage(fondo, Art("roulette_felt"), Color.white);
        fondoImg.type = Image.Type.Sliced;

        var fondos = new Object[RouletteBoard.Casilleros];
        var brillos = new Object[RouletteBoard.Casilleros];
        var fichas = new Object[RouletteBoard.Casilleros];
        var montos = new Object[RouletteBoard.Casilleros];

        for (int n = 0; n < RouletteBoard.Casilleros; n++)
        {
            Vector2 pos;
            Vector2 size = CeldaSize;
            if (n == 0)
            {
                pos = CeroPos;
                size = CeroSize;
            }
            else
            {
                int fila = (n - 1) / 12;
                int col = (n - 1) % 12;
                pos = new Vector2(GrillaX0 + col * ColumnaPaso, GrillaY0 - fila * FilaPaso);
            }

            string sprite = n == 0 ? "roulette_cell_green"
                : RouletteBoard.EsRojo(n) ? "roulette_cell_red" : "roulette_cell_black";

            // En el cero, que es alto, el numero va arriba y la ficha abajo:
            // centrados los dos se taparian entre si.
            float yNum = n == 0 ? 26f : 5f;
            float yFicha = n == 0 ? -24f : -7f;

            ArmarCasillero(padre, screen, n, sprite, pos, size, yNum, yFicha,
                out Image fondoCelda, out Image brillo, out Image ficha, out Text monto);

            fondos[n] = fondoCelda;
            brillos[n] = brillo;
            fichas[n] = ficha;
            montos[n] = monto;
        }

        MenuUiKit.SetArrayField(screen, "cellBackgrounds", fondos);
        MenuUiKit.SetArrayField(screen, "cellGlows", brillos);
        MenuUiKit.SetArrayField(screen, "cellChips", fichas);
        MenuUiKit.SetArrayField(screen, "cellAmounts", montos);
    }

    // Un casillero: fondo de color, el numero, el halo de seleccion y la ficha
    // apostada encima. El halo va DETRAS del fondo para que se vea como un
    // resplandor que asoma por el borde y no como un marco tapando el numero.
    private static void ArmarCasillero(Transform padre, RouletteScreen screen, int numero,
                                       string sprite, Vector2 pos, Vector2 size,
                                       float yNum, float yFicha,
                                       out Image fondo, out Image brillo, out Image ficha,
                                       out Text monto)
    {
        GameObject celda = MenuUiKit.Node($"Celda{numero}", padre);
        MenuUiKit.Box(celda, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        GameObject halo = MenuUiKit.Node("Halo", celda.transform);
        MenuUiKit.Box(halo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, size + new Vector2(10f, 10f));
        brillo = MenuUiKit.AddImage(halo, Art("roulette_cell_glow"), Color.white);
        halo.SetActive(false);

        GameObject fondoGo = MenuUiKit.Node("Fondo", celda.transform);
        MenuUiKit.Box(fondoGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        fondo = MenuUiKit.AddImage(fondoGo, Art(sprite), Color.white, raycast: true);

        Text etiqueta = MenuUiKit.Label("Num", celda.transform, new Vector2(0f, yNum),
            new Vector2(size.x, 20f), numero.ToString(), 11, TextAnchor.MiddleCenter, Tinta,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(etiqueta);

        GameObject fichaGo = MenuUiKit.Node("Ficha", celda.transform);
        MenuUiKit.Box(fichaGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, yFicha), new Vector2(15f, 15f));
        ficha = MenuUiKit.AddImage(fichaGo, Art("roulette_token"), Color.white);
        fichaGo.SetActive(false);

        monto = MenuUiKit.Label("Monto", celda.transform, new Vector2(0f, yFicha),
            new Vector2(24f, 12f), "", 8, TextAnchor.MiddleCenter, Tinta, new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(monto);
        monto.gameObject.SetActive(false);

        // El fondo del casillero es el que recibe el click.
        var boton = fondoGo.AddComponent<Button>();
        boton.targetGraphic = fondo;
        boton.transition = Selectable.Transition.None;
        UnityEventTools.AddIntPersistentListener(boton.onClick, screen.OnCellButton, numero);
    }

    // ---------------------------------------------------------------- colores

    private static void ArmarColores(Transform padre, RouletteScreen screen)
    {
        GameObject fondo = MenuUiKit.Node("FondoColores", padre);
        MenuUiKit.Box(fondo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(FondoGrillaPos.x, ColoresY), new Vector2(FondoGrillaSize.x, 42f));
        Image fondoImg = MenuUiKit.AddImage(fondo, Art("roulette_felt"), Color.white);
        fondoImg.type = Image.Type.Sliced;

        // Las x salen de no pisarse: la marca mide 30 de ancho y el texto 62,
        // asi que van separados 50. Antes el texto arrancaba sobre el circulo.
        ArmarColor(padre, screen, 0, "roulette_mark_red", "ROJO", -46f, 4f, 56f,
            out Image rm, out Image rg, out Image rc, out Text ra);
        ArmarColor(padre, screen, 1, "roulette_mark_black", "NEGRO", 108f, 160f, 216f,
            out Image bm, out Image bg, out Image bc, out Text ba);

        MenuUiKit.SetField(screen, "redMarker", rm);
        MenuUiKit.SetField(screen, "redGlow", rg);
        MenuUiKit.SetField(screen, "redChip", rc);
        MenuUiKit.SetField(screen, "redAmount", ra);
        MenuUiKit.SetField(screen, "blackMarker", bm);
        MenuUiKit.SetField(screen, "blackGlow", bg);
        MenuUiKit.SetField(screen, "blackChip", bc);
        MenuUiKit.SetField(screen, "blackAmount", ba);
    }

    private static void ArmarColor(Transform padre, RouletteScreen screen, int cual,
                                   string sprite, string texto,
                                   float xMarca, float xTexto, float xFicha,
                                   out Image marca, out Image brillo, out Image ficha, out Text monto)
    {
        float y = ColoresY;

        GameObject halo = MenuUiKit.Node($"HaloColor{cual}", padre);
        MenuUiKit.Box(halo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(xMarca, y), new Vector2(40f, 40f));
        brillo = MenuUiKit.AddImage(halo, Art("roulette_cell_glow"), Color.white);
        halo.SetActive(false);

        GameObject marcaGo = MenuUiKit.Node($"Color{cual}", padre);
        MenuUiKit.Box(marcaGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(xMarca, y), new Vector2(30f, 30f));
        marca = MenuUiKit.AddImage(marcaGo, Art(sprite), Color.white, raycast: true);

        Text etiqueta = MenuUiKit.Label($"Texto{cual}", padre, new Vector2(xTexto, y),
            new Vector2(62f, 20f), texto, 13, TextAnchor.MiddleCenter, MenuUiKit.Gold,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(etiqueta);

        GameObject fichaGo = MenuUiKit.Node($"FichaColor{cual}", padre);
        MenuUiKit.Box(fichaGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(xFicha, y), new Vector2(20f, 20f));
        ficha = MenuUiKit.AddImage(fichaGo, Art("roulette_token"), Color.white);
        fichaGo.SetActive(false);

        monto = MenuUiKit.Label($"MontoColor{cual}", padre, new Vector2(xFicha, y),
            new Vector2(26f, 12f), "", 8, TextAnchor.MiddleCenter, Tinta, new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(monto);
        monto.gameObject.SetActive(false);

        var boton = marcaGo.AddComponent<Button>();
        boton.targetGraphic = marca;
        boton.transition = Selectable.Transition.None;
        UnityEventTools.AddIntPersistentListener(boton.onClick, screen.OnColorButton, cual);
    }

    // ---------------------------------------------------------------- fichas

    private static void ArmarFichas(Transform padre, RouletteScreen screen)
    {
        string[] sprites =
        {
            "roulette_chip_1", "roulette_chip_5", "roulette_chip_25",
            "roulette_chip_100", "roulette_chip_500", "roulette_chip_1000"
        };

        var botones = new Object[sprites.Length];
        var brillos = new Object[sprites.Length];

        for (int i = 0; i < sprites.Length; i++)
        {
            float x = -25f + i * 34f;

            GameObject halo = MenuUiKit.Node($"HaloFicha{i}", padre);
            MenuUiKit.Box(halo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, FichasY), new Vector2(34f, 34f));
            brillos[i] = MenuUiKit.AddImage(halo, Art("roulette_cell_glow"), Color.white);
            halo.SetActive(false);

            GameObject go = MenuUiKit.Node($"Ficha{i}", padre);
            MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(x, FichasY), new Vector2(26f, 27f));
            Image img = MenuUiKit.AddImage(go, Art(sprites[i]), Color.white, raycast: true);
            botones[i] = img;

            var boton = go.AddComponent<Button>();
            boton.targetGraphic = img;
            boton.transition = Selectable.Transition.None;
            UnityEventTools.AddIntPersistentListener(boton.onClick, screen.OnChipButton, i);
        }

        MenuUiKit.SetArrayField(screen, "chipButtons", botones);
        MenuUiKit.SetArrayField(screen, "chipGlows", brillos);

        // ---- cuanto va apostado ----
        Imagen(padre, "PanelApuesta", "roulette_panel_bet", new Vector2(205f, FichasY),
            new Vector2(86f, 49f));

        Text total = MenuUiKit.Label("Apostado", padre, new Vector2(214f, FichasY - 7f),
            new Vector2(60f, 20f), "0", 15, TextAnchor.MiddleCenter, Tinta,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(total);
        MenuUiKit.SetField(screen, "betLabel", total);
    }

    // --------------------------------------------------------------- botones

    private static void ArmarBotones(Transform padre, RouletteScreen screen)
    {
        Button limpiar = Boton(padre, "BtnLimpiar", "roulette_btn_clear",
            new Vector2(-14f, BotonesY), new Vector2(92f, 25f), out Image limpiarImg);
        UnityEventTools.AddVoidPersistentListener(limpiar.onClick, screen.OnClearButton);

        Button girar = Boton(padre, "BtnGirar", "roulette_btn_spin",
            new Vector2(84f, BotonesY), new Vector2(92f, 25f), out Image girarImg);
        UnityEventTools.AddVoidPersistentListener(girar.onClick, screen.OnSpinButton);

        Button volver = Boton(padre, "BtnVolver", "roulette_btn_back",
            new Vector2(182f, BotonesY), new Vector2(92f, 25f), out Image volverImg);
        UnityEventTools.AddVoidPersistentListener(volver.onClick, screen.OnBackButton);

        MenuUiKit.SetField(screen, "clearButton", limpiarImg);
        MenuUiKit.SetField(screen, "spinButton", girarImg);
        MenuUiKit.SetField(screen, "backButton", volverImg);
        MenuUiKit.SetField(screen, "spinEnabledSprite", Art("roulette_btn_spin"));
        MenuUiKit.SetField(screen, "spinDisabledSprite", Art("roulette_btn_spin_off"));
    }

    private static Button Boton(Transform padre, string nombre, string sprite,
                                Vector2 pos, Vector2 size, out Image imagen)
    {
        GameObject go = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        imagen = MenuUiKit.AddImage(go, Art(sprite), Color.white, raycast: true);

        var boton = go.AddComponent<Button>();
        boton.targetGraphic = imagen;
        // Sin transicion de UGUI: el estado lo dibuja la pantalla cambiando el
        // sprite (btn_spin / btn_spin_off), que es el arte que hizo el equipo.
        boton.transition = Selectable.Transition.None;
        return boton;
    }

    // --------------------------------------------------------------- efectos

    private static void ArmarEfectos(Transform padre, RouletteScreen screen)
    {
        // El destello cuelga del contenedor de la rueda porque se coloca sobre
        // el casillero donde quedo la bola, en las mismas coordenadas que ella.
        Transform rueda = padre.Find("Rueda");

        GameObject destello = MenuUiKit.Node("FxDestello", rueda != null ? rueda : padre);
        MenuUiKit.Box(destello, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(46f, 48f));
        Image destelloImg = MenuUiKit.AddImage(destello, Art("roulette_fx_spark"), Color.white);
        destello.SetActive(false);
        MenuUiKit.SetField(screen, "sparkFx", destelloImg);

        GameObject confeti = MenuUiKit.Node("FxConfeti", padre);
        MenuUiKit.Box(confeti, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            WheelPos, new Vector2(130f, 112f));
        Image confetiImg = MenuUiKit.AddImage(confeti, Art("roulette_fx_confetti"), Color.white);
        confeti.SetActive(false);
        MenuUiKit.SetField(screen, "confettiFx", confetiImg);
    }

    // ----------------------------------------------------------------- util

    private static Image Imagen(Transform padre, string nombre, string sprite,
                                Vector2 pos, Vector2 size)
    {
        GameObject go = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        return MenuUiKit.AddImage(go, Art(sprite), Color.white);
    }

    // ------------------------------------------------------------ MenuSystem

    private static void RegistrarEnMenuSystem(GameObject pantalla)
    {
        var screen = pantalla != null ? pantalla.GetComponent<MenuScreen>() : null;
        if (screen == null) return;

        GameObject contenido = PrefabUtility.LoadPrefabContents(MenuSystemPath);
        if (contenido == null)
        {
            Debug.LogError($"[Ruleta] No se encontro {MenuSystemPath}. " +
                           "Genera primero el sistema de menus con PokeRush/UI.");
            return;
        }

        try
        {
            var manager = contenido.GetComponent<MenuManager>();
            if (manager == null)
            {
                Debug.LogError("[Ruleta] El prefab MenuSystem no tiene MenuManager.");
                return;
            }

            var so = new SerializedObject(manager);
            SerializedProperty lista = so.FindProperty("screenPrefabs");

            var yaEstan = new HashSet<Object>();
            for (int i = 0; i < lista.arraySize; i++)
                yaEstan.Add(lista.GetArrayElementAtIndex(i).objectReferenceValue);

            if (yaEstan.Contains(screen)) return;

            lista.InsertArrayElementAtIndex(lista.arraySize);
            lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = screen;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contenido, MenuSystemPath);
            Debug.Log("[Ruleta] MenuSystem: agregada la pantalla Roulette.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
