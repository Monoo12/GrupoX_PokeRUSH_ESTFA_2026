using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Arma la pantalla de la mesa de blackjack y la registra en el MenuSystem.
//
// Igual que el resto de los builders del casino: NO reconstruye el sistema de
// menus, le agrega la pantalla que le falta y lo vuelve a guardar, asi lo que el
// equipo retoco a mano no se pisa.
//
// Casi todo el arte sale de Assets/BlackJack God. Las dos excepciones son el
// fondo de la sala, que es un battleback del pack (Battlebacks/InteriorOscuro),
// y el croupier, que es un NPC del juego (Characters/noble, el senior de galera
// y traje oscuro): el pack de blackjack no trae ni sala ni personaje, y dibujar
// arte nuevo cuando el proyecto ya los tiene seria inventar por inventar.
//
// Las medidas de abajo son pixeles de la resolucion de referencia del Canvas
// (512x384), con el origen en el centro. El CanvasScaler es ScaleWithScreenSize
// en modo Expand sobre esa referencia, asi que en un 16:9 de 1080 todo se
// agranda 2,8125x (manda el alto). De ahi salen los tamanios: una celda de
// carta de 104 px dibujada a 37 unidades cae 1:1 y se ve nitida.
//
// La idea del acomodo, que es la de una mesa de verdad vista de frente:
//
//   fondo       la sala en penumbra, a pantalla completa;
//   arriba      el croupier parado del otro lado, con el mazo de un costado y
//               la bandeja de descartes del otro;
//   mesa        LAS CARTAS VAN SOBRE EL FIELTRO: las del croupier en el pano
//               libre de arriba y las del jugador sobre los casilleros de
//               apuesta. Por eso la mesa es grande y las cartas chicas, y no al
//               reves;
//   derecha     los carteles: de quien es cada mano y cuanto suma;
//   abajo       lo que se toca, en el orden en que se usa: el cuadro de texto,
//               las fichas y los seis botones.
public static class BlackjackTableBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/UI/Casino";
    private const string MenuSystemPath = "Assets/Resources/Menus/MenuSystem.prefab";

    // ---- sala ----
    // El battleback mide justo 512x384, o sea la referencia del Canvas: entra
    // entero, sin recortes ni mosaicos.
    // Se probaron varios: InteriorOscuro es una oficina iluminada y le pelea la
    // atencion a la mesa, y Castillo trae una alfombra redonda que choca con el
    // fieltro. Este es un degrade de rojo oscuro a negro, o sea una sala en
    // penumbra: da el clima sin discutirle nada al primer plano.
    private const string FondoPath = "Assets/Graphics/Battlebacks/InteriorNegro_bg.png";

    // ---- mesa y reparto ----
    // La mesa es el centro de todo y por eso es grande: las dos filas de cartas
    // tienen que entrar SOBRE el fieltro, y con una mesa chica quedaban
    // flotando por encima del borde de madera.
    private const float MesaX = -80f;
    private static readonly Vector2 MesaPos = new Vector2(MesaX, 20f);
    private static readonly Vector2 MesaSize = new Vector2(350f, 215f);

    // El croupier va DETRAS de la mesa: se dibuja antes, y el borde de madera
    // le tapa las piernas, que es lo que lo hace ver parado del otro lado.
    // Queda a caballo del borde de arriba: la mitad de abajo se la come la
    // madera y solo asoma por la muesca que el dibujo de la mesa tiene en el
    // medio, que es justo el lugar del croupier.
    private static readonly Vector2 CroupierPos = new Vector2(MesaX, 144f);
    private static readonly Vector2 CroupierSize = new Vector2(46f, 77f);

    private static readonly Vector2 MazoPos = new Vector2(-218f, 152f);
    private static readonly Vector2 MazoSize = new Vector2(60f, 40f);
    private static readonly Vector2 DescartePos = new Vector2(52f, 152f);
    private static readonly Vector2 DescarteSize = new Vector2(56f, 40f);

    // Centro de cada fila de cartas, medido sobre el dibujo de la mesa: la del
    // croupier cae en el pano libre de arriba y la del jugador justo sobre los
    // casilleros de apuesta.
    private static readonly Vector2 FilaCroupier = new Vector2(MesaX, 74f);
    private static readonly Vector2 FilaJugador = new Vector2(MesaX, 2f);
    private static readonly Vector2 CartaSize = new Vector2(37f, 54f);
    private const float SeparacionDeCartas = 26f;
    // Lo que mide el fieltro a la altura de los casilleros. Pasarse de aca deja
    // las cartas sobre la madera.
    private const float AnchoDeFila = 190f;
    private const float SeparacionDeManos = 62f;

    // ---- columna de la derecha ----
    private const float ColumnaX = 168f;
    // Las placas 'TOTAL:' van METIDAS en el hueco crema de su panel, no
    // debajo. Los dos paneles del pack son cajas con un recuadro vacio adentro
    // (asi los dibujo el equipo, para poner algo ahi); dejandolo vacio la
    // pantalla se ve a medio hacer, y la placa entra justa.
    private static readonly Vector2 PanelDealerPos = new Vector2(ColumnaX, 118f);
    private static readonly Vector2 PanelDealerSize = new Vector2(125f, 67f);
    private static readonly Vector2 TotalDealerPos = new Vector2(ColumnaX, 108f);
    private static readonly Vector2 TotalDealerSize = new Vector2(112f, 35f);
    private static readonly Vector2 PanelJugadorPos = new Vector2(ColumnaX, 10f);
    private static readonly Vector2 PanelJugadorSize = new Vector2(140f, 67f);
    private static readonly Vector2 TotalJugadorPos = new Vector2(ColumnaX, 0f);
    private static readonly Vector2 TotalJugadorSize = new Vector2(115f, 35f);
    // La segunda mano lleva su propia placa 'TUS CARTAS', igual que la primera.
    // La version anterior le dejaba el cartel del total suelto y le escribia
    // '1)' y '2)' adentro del numero para distinguirlas: no entraba en el hueco
    // del arte y se leia como un error. Dos placas iguales, una arriba de la
    // otra y con la que no se esta jugando atenuada, se entienden solas.
    private static readonly Vector2 PanelSplitPos = new Vector2(ColumnaX, -62f);
    private static readonly Vector2 TotalSplitPos = new Vector2(ColumnaX, -72f);

    // Donde cae el numero dentro de la placa 'TOTAL:'. La palabra ocupa la
    // mitad izquierda y el recuadro oscuro la derecha.
    private static readonly Vector2 HuecoDelTotal = new Vector2(30f, 1f);

    // ---- abajo ----
    private const float FichasY = -145f;
    private const float FichaPrimeraX = -228f;
    private const float FichaPaso = 30f;
    private static readonly Vector2 FichaSize = new Vector2(25f, 24f);

    private const float BotonesY = -180f;
    private const float BotonPrimeroX = -213f;
    private const float BotonPaso = 78f;
    private static readonly Vector2 BotonSize = new Vector2(66f, 20f);

    private static readonly Vector2 DialogoPos = new Vector2(MesaX, -108f);
    private static readonly Vector2 DialogoSize = new Vector2(190f, 52f);

    // Sale sobre el centro de la mesa, pero corrido hacia arriba para no comerse
    // entera la mano del jugador: se tiene que ver que gano Y con que.
    private static readonly Vector2 BannerPos = new Vector2(MesaX, 52f);
    private static readonly Vector2 BannerSize = new Vector2(112f, 110f);

    // Los seis botones, en el mismo orden que el enum Accion de la pantalla.
    private static readonly string[] BotonSprites =
    {
        "btn_pedir", "btn_plantarse", "btn_doblar", "btn_dividir",
        "btn_reiniciar", "btn_salir"
    };

    private static readonly int[] ValoresDeFicha = { 1, 5, 10, 25, 50, 100, 500, 1000 };

    private static readonly Color Tinta = Color.white;
    private static readonly Color TintaOscura = new Color(0.11f, 0.13f, 0.2f, 1f);
    private static readonly Color TintaSuave = new Color(0.93f, 0.95f, 1f, 1f);
    // Continua el borde del battleback hacia los costados. El CanvasScaler
    // encaja la referencia 4:3 dentro de la ventana, asi que en un 16:9 sobran
    // franjas; sin esto, por ahi se ve el mapa del casino y la sala parece
    // flotar. El color sale del pixel del borde del propio fondo.
    private static readonly Color Telon = new Color(0.27f, 0.191f, 0.194f, 1f);

    private static Dictionary<string, Sprite> _cartas;
    private static Dictionary<string, Sprite> _fichas;
    private static Dictionary<string, Sprite> _croupier;

    [MenuItem("PokeRush/Casino/Construir la mesa de blackjack")]
    public static void BuildAll()
    {
        BlackjackArtImporter.ImportarTodo();
        MenuUiKit.EnsureFolder(PrefabFolder);

        _cartas = BlackjackArtImporter.SpritesDe(BlackjackArtImporter.CartasPath);
        _fichas = BlackjackArtImporter.SpritesDe(BlackjackArtImporter.FichasPath);
        _croupier = BlackjackArtImporter.SpritesDe(BlackjackArtImporter.CroupierPath);

        if (_cartas.Count < 54)
        {
            Debug.LogError($"[Blackjack] La hoja de cartas tiene {_cartas.Count} sprites y " +
                           "deberia tener 54. Corre el importador y volve a intentar.");
            return;
        }

        GameObject pantalla = BuildScreen();
        RegistrarEnMenuSystem(pantalla);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Blackjack] Mesa generada en {PrefabFolder}/BlackjackScreen.prefab " +
                  "y registrada en el MenuSystem.");
    }

    private static Sprite Art(string nombre) => BlackjackArtImporter.Suelto(nombre);

    private static Sprite Carta(string nombre)
        => _cartas != null && _cartas.TryGetValue(nombre, out Sprite s) ? s : null;

    private static Sprite Ficha(int valor)
        => _fichas != null && _fichas.TryGetValue($"ficha_{valor:0000}", out Sprite s) ? s : null;

    private static Sprite Croupier(int cuadro)
        => _croupier != null &&
           _croupier.TryGetValue(BlackjackArtImporter.NombreDeCroupier(cuadro), out Sprite s)
            ? s : null;

    // ------------------------------------------------------------- pantalla

    private static GameObject BuildScreen()
    {
        var root = new GameObject("BlackjackScreen", typeof(RectTransform));
        MenuUiKit.Full(root);

        var screen = root.AddComponent<BlackjackScreen>();
        MenuUiKit.SetField(screen, "screenId", (int)MenuScreenId.Blackjack);

        // Telon opaco a pantalla completa, DETRAS del marco de 512x384. El
        // CanvasScaler encaja la referencia 4:3 dentro de la ventana, asi que en
        // cualquier pantalla mas ancha sobran franjas a los costados; sin esto,
        // por ahi se ve el mapa del casino y la mesa parece flotar.
        GameObject telon = MenuUiKit.Node("Telon", root.transform);
        MenuUiKit.Full(telon);
        MenuUiKit.AddImage(telon, null, Telon);

        // Marco de 512x384 centrado: este nodo existe solo para que las
        // coordenadas de adentro sean pixeles del arte y no porcentajes de una
        // pantalla que cambia de ancho.
        GameObject marco = MenuUiKit.Node("Marco", root.transform);
        MenuUiKit.Box(marco, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(MenuUiKit.RefWidth, MenuUiKit.RefHeight));
        Transform t = marco.transform;

        // El orden importa: UGUI dibuja por jerarquia, asi que lo que va detras
        // se crea primero. Sala -> croupier -> mesa -> carteles -> cartas ->
        // controles -> banner.
        //
        // Que el croupier vaya ANTES que la mesa no es un detalle: es lo que
        // hace que el borde de madera le tape las piernas y se lea como que
        // esta parado del otro lado y no pegado encima del dibujo.
        Fondo(t);
        ArmarCroupier(t, screen);
        Imagen(t, "Mesa", "mesa_blackjack", MesaPos, MesaSize);
        ArmarCabecera(t, screen);
        ArmarCarteles(t, screen);
        ArmarCartas(t, screen);
        ArmarFichas(t, screen);
        ArmarBotones(t, screen);
        ArmarDialogo(t, screen);
        ArmarBanner(t, screen);

        MenuUiKit.SetField(screen, "dealerRowCenter", FilaCroupier);
        MenuUiKit.SetField(screen, "playerRowCenter", FilaJugador);
        MenuUiKit.SetField(screen, "cardSize", CartaSize);
        MenuUiKit.SetField(screen, "cardStep", SeparacionDeCartas);
        MenuUiKit.SetField(screen, "rowMaxWidth", AnchoDeFila);
        MenuUiKit.SetField(screen, "splitSpread", SeparacionDeManos);
        MenuUiKit.SetIntArrayField(screen, "chipValues", ValoresDeFicha);

        return MenuUiKit.SavePrefab(root, PrefabFolder + "/BlackjackScreen.prefab");
    }

    // ------------------------------------------------------- sala y croupier

    private static void Fondo(Transform padre)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FondoPath);
        if (sprite == null)
        {
            Debug.LogWarning($"[Blackjack] No encontre el fondo {FondoPath}; " +
                             "la sala queda en color plano.");
            return;
        }

        GameObject go = MenuUiKit.Node("Sala", padre);
        MenuUiKit.Full(go);
        MenuUiKit.AddImage(go, sprite, Color.white);
    }

    private static void ArmarCroupier(Transform padre, BlackjackScreen screen)
    {
        Image img = Sprite(padre, "Croupier", Croupier(0), CroupierPos, CroupierSize);
        MenuUiKit.SetField(screen, "dealerSprite", img);

        var cuadros = new Object[BlackjackArtImporter.CroupierCuadros];
        for (int i = 0; i < cuadros.Length; i++) cuadros[i] = Croupier(i);
        MenuUiKit.SetArrayField(screen, "dealerFrames", cuadros);
    }

    // -------------------------------------------------------------- cabecera

    private static void ArmarCabecera(Transform padre, BlackjackScreen screen)
    {
        // El mazo hace de origen de las cartas: la pantalla las manda volando
        // desde su posicion, asi que este nodo no es decorativo.
        Image mazo = Imagen(padre, "Mazo", "mazo", MazoPos, MazoSize);
        MenuUiKit.SetField(screen, "deck", (RectTransform)mazo.transform);

        Image descarte = Imagen(padre, "Descarte", "descarte", DescartePos, DescarteSize);
        MenuUiKit.SetField(screen, "discard", (RectTransform)descarte.transform);

        // La ficha dorada hace de icono de la plata: se lee de una y evita
        // tener que escribir "dinero" al lado del numero.
        Sprite(padre, "IconoPlata", Ficha(100), new Vector2(-240f, 176f), new Vector2(20f, 19f));

        Text plata = MenuUiKit.Label("Plata", padre, new Vector2(-154f, 176f),
            new Vector2(140f, 22f), "0", 15, TextAnchor.MiddleLeft, MenuUiKit.Gold,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(plata);
        MenuUiKit.SetField(screen, "moneyLabel", plata);

        Text titulo = MenuUiKit.Label("TituloApuesta", padre, new Vector2(ColumnaX, 182f),
            new Vector2(140f, 14f), "APUESTA", 9, TextAnchor.MiddleCenter, MenuUiKit.Gold,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(titulo);

        Sprite(padre, "IconoApuesta", Ficha(25), new Vector2(ColumnaX - 52f, 169f),
            new Vector2(18f, 17f));

        // El rect arranca 12 px despues del icono: alineado a la izquierda, el
        // texto sale del borde del rect, y centrado sobre la ficha se pisaban.
        Text apuesta = MenuUiKit.Label("Apuesta", padre, new Vector2(ColumnaX + 14f, 169f),
            new Vector2(80f, 20f), "0", 14, TextAnchor.MiddleLeft, Tinta,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(apuesta);
        MenuUiKit.SetField(screen, "betLabel", apuesta);
    }

    // -------------------------------------------------------------- carteles

    private static void ArmarCarteles(Transform padre, BlackjackScreen screen)
    {
        Imagen(padre, "PanelDealer", "panel_dealer", PanelDealerPos, PanelDealerSize);
        MenuUiKit.SetField(screen, "dealerTotalLabel",
            PlacaDeTotal(padre, "TotalDealer", "total_dealer", TotalDealerPos, TotalDealerSize));

        Image panelJugador = Imagen(padre, "PanelJugador", "panel_jugador",
            PanelJugadorPos, PanelJugadorSize);
        MenuUiKit.SetField(screen, "playerPanel", panelJugador);
        MenuUiKit.SetField(screen, "playerTotalLabel",
            PlacaDeTotal(padre, "TotalJugador", "total_jugador", TotalJugadorPos, TotalJugadorSize));

        // La segunda mano no existe hasta que se divide, asi que su placa y su
        // total van juntos en un nodo aparte que la pantalla prende y apaga de
        // una sola vez.
        GameObject grupo = MenuUiKit.Node("GrupoSplit", padre);
        MenuUiKit.Full(grupo);

        Image panelSplit = Imagen(grupo.transform, "PanelSplit", "panel_jugador",
            PanelSplitPos, PanelJugadorSize);
        Text split = PlacaDeTotal(grupo.transform, "TotalSplit", "total_jugador",
            TotalSplitPos, TotalJugadorSize);
        grupo.SetActive(false);

        MenuUiKit.SetField(screen, "splitGroup", grupo);
        MenuUiKit.SetField(screen, "splitPanel", panelSplit);
        MenuUiKit.SetField(screen, "splitTotalLabel", split);

        // Marca cual de las dos manos se esta jugando. Es una ficha chiquita
        // debajo de la fila; la pantalla la mueve y la prende.
        Image marca = Sprite(padre, "MarcaDeMano", Ficha(1), Vector2.zero, new Vector2(22f, 21f));
        marca.gameObject.SetActive(false);
        MenuUiKit.SetField(screen, "handMarker", marca);
    }

    // La placa 'TOTAL:' con el numero metido en su recuadro oscuro. Devuelve el
    // Text, que es lo que la pantalla escribe; la placa entera es su padre, y de
    // eso depende que se pueda prender y apagar de una (ver PintarTotalDeMano).
    private static Text PlacaDeTotal(Transform padre, string nombre, string sprite,
                                     Vector2 pos, Vector2 size)
    {
        GameObject placa = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(placa, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        MenuUiKit.AddImage(placa, Art(sprite), Color.white);

        Text numero = MenuUiKit.Label("Numero", placa.transform, HuecoDelTotal,
            new Vector2(size.x * 0.44f, 20f), "-", 13, TextAnchor.MiddleCenter, Tinta,
            new Vector2(0.5f, 0.5f));
        MenuUiKit.Contorno(numero);
        return numero;
    }

    // ---------------------------------------------------------------- cartas

    private static void ArmarCartas(Transform padre, BlackjackScreen screen)
    {
        MenuUiKit.SetArrayField(screen, "cardSprites", TodasLasCartas());
        MenuUiKit.SetField(screen, "cardBackSprite",
            Carta(BlackjackArtImporter.ReversoRojo));

        MenuUiKit.SetArrayField(screen, "dealerCards",
            FilaDeSlots(padre, "CartaDealer", FilaCroupier));
        MenuUiKit.SetArrayField(screen, "playerCards",
            FilaDeSlots(padre, "CartaJugador", FilaJugador));
        MenuUiKit.SetArrayField(screen, "splitCards",
            FilaDeSlots(padre, "CartaSplit", FilaJugador));
    }

    // Las 52 en el orden que espera la pantalla: palo * 13 + (rango - 1).
    private static Object[] TodasLasCartas()
    {
        var todas = new Object[52];
        for (int palo = 0; palo < 4; palo++)
            for (int col = 0; col < 13; col++)
                todas[palo * 13 + col] = Carta(BlackjackArtImporter.NombreDeCarta(palo, col));
        return todas;
    }

    // Los slots arrancan apagados y sin posicion util: la pantalla los coloca
    // segun cuantas cartas tenga la mano, que es algo que aca no se sabe.
    private static Object[] FilaDeSlots(Transform padre, string nombre, Vector2 centro)
    {
        var slots = new Object[BlackjackScreen.SlotsPorMano];

        for (int i = 0; i < slots.Length; i++)
        {
            GameObject go = MenuUiKit.Node($"{nombre}{i}", padre);
            MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                centro, CartaSize);
            slots[i] = MenuUiKit.AddImage(go, Carta(BlackjackArtImporter.ReversoRojo),
                Color.white);
            go.SetActive(false);
        }

        return slots;
    }

    // ---------------------------------------------------------------- fichas

    private static void ArmarFichas(Transform padre, BlackjackScreen screen)
    {
        var botones = new Object[ValoresDeFicha.Length];
        var brillos = new Object[ValoresDeFicha.Length];

        for (int i = 0; i < ValoresDeFicha.Length; i++)
        {
            var pos = new Vector2(FichaPrimeraX + i * FichaPaso, FichasY);

            // El halo va DETRAS de la ficha para que se lea como un resplandor
            // que asoma por el borde y no como un marco tapandola.
            GameObject halo = MenuUiKit.Node($"HaloFicha{i}", padre);
            MenuUiKit.Box(halo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                pos, FichaSize + new Vector2(9f, 9f));
            brillos[i] = MenuUiKit.AddImage(halo, MenuUiKit.WhitePixel,
                new Color(1f, 0.93f, 0.45f, 0.4f));
            halo.SetActive(false);

            GameObject go = MenuUiKit.Node($"Ficha{i}", padre);
            MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, FichaSize);
            Image img = MenuUiKit.AddImage(go, Ficha(ValoresDeFicha[i]), Color.white,
                raycast: true);
            botones[i] = img;

            // El valor va escrito abajo con la fuente del juego. El arte trae el
            // numero dibujado, pero cortado al medio en la mitad de las fichas;
            // el recorte las deja solo con el disco (ver cortar_arte_blackjack).
            // El valor va justo debajo del disco, en la franja que queda entre
            // las fichas y los botones. Con 16 px de separacion se metia abajo
            // de la fila de botones y quedaba cortado al medio.
            Text valor = MenuUiKit.Label($"ValorFicha{i}", padre,
                pos + new Vector2(0f, -15f), new Vector2(34f, 11f),
                ValoresDeFicha[i].ToString(), 8, TextAnchor.MiddleCenter, TintaSuave,
                new Vector2(0.5f, 0.5f));
            MenuUiKit.Contorno(valor);

            var boton = go.AddComponent<Button>();
            boton.targetGraphic = img;
            boton.transition = Selectable.Transition.None;
            UnityEventTools.AddIntPersistentListener(boton.onClick, screen.OnChipButton, i);
        }

        MenuUiKit.SetArrayField(screen, "chipButtons", botones);
        MenuUiKit.SetArrayField(screen, "chipGlows", brillos);
    }

    // --------------------------------------------------------------- botones

    private static void ArmarBotones(Transform padre, BlackjackScreen screen)
    {
        var botones = new Object[BotonSprites.Length];

        for (int i = 0; i < BotonSprites.Length; i++)
        {
            var pos = new Vector2(BotonPrimeroX + i * BotonPaso, BotonesY);

            GameObject go = MenuUiKit.Node($"Btn{BotonSprites[i]}", padre);
            MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, BotonSize);
            Image img = MenuUiKit.AddImage(go, Art(BotonSprites[i]), Color.white, raycast: true);
            botones[i] = img;

            var boton = go.AddComponent<Button>();
            boton.targetGraphic = img;
            // Sin transicion de UGUI: el estado (habilitado, elegido, apagado)
            // lo dibuja la pantalla, que es la unica que sabe en que fase esta.
            boton.transition = Selectable.Transition.None;
            UnityEventTools.AddIntPersistentListener(boton.onClick, screen.OnActionButton, i);
        }

        MenuUiKit.SetArrayField(screen, "actionButtons", botones);
    }

    // --------------------------------------------------------------- dialogo

    private static void ArmarDialogo(Transform padre, BlackjackScreen screen)
    {
        Imagen(padre, "Dialogo", "bj_dialogo", DialogoPos, DialogoSize);

        Text ayuda = MenuUiKit.Label("Ayuda", padre, DialogoPos, new Vector2(160f, 20f),
            "", 11, TextAnchor.MiddleCenter, TintaOscura, new Vector2(0.5f, 0.5f));
        MenuUiKit.SetField(screen, "hintLabel", ayuda);
    }

    // ---------------------------------------------------------------- banner

    private static void ArmarBanner(Transform padre, BlackjackScreen screen)
    {
        GameObject go = MenuUiKit.Node("Resultado", padre);
        MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            BannerPos, BannerSize);
        Image img = MenuUiKit.AddImage(go, Art("resultado_ganaste"), Color.white);
        img.preserveAspect = true;
        go.SetActive(false);

        MenuUiKit.SetField(screen, "resultBanner", img);
        MenuUiKit.SetArrayField(screen, "resultSprites", new Object[]
        {
            Art("resultado_blackjack"), Art("resultado_ganaste"),
            Art("resultado_empate"), Art("resultado_perdiste")
        });
    }

    // ------------------------------------------------------------------ util

    private static Image Imagen(Transform padre, string nombre, string sprite,
                                Vector2 pos, Vector2 size)
        => Sprite(padre, nombre, Art(sprite), pos, size);

    private static Image Sprite(Transform padre, string nombre, Sprite sprite,
                                Vector2 pos, Vector2 size)
    {
        GameObject go = MenuUiKit.Node(nombre, padre);
        MenuUiKit.Box(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        return MenuUiKit.AddImage(go, sprite, Color.white);
    }

    // ------------------------------------------------------------ MenuSystem

    private static void RegistrarEnMenuSystem(GameObject pantalla)
    {
        var screen = pantalla != null ? pantalla.GetComponent<MenuScreen>() : null;
        if (screen == null) return;

        GameObject contenido = PrefabUtility.LoadPrefabContents(MenuSystemPath);
        if (contenido == null)
        {
            Debug.LogError($"[Blackjack] No se encontro {MenuSystemPath}. " +
                           "Genera primero el sistema de menus con PokeRush/UI.");
            return;
        }

        try
        {
            var manager = contenido.GetComponent<MenuManager>();
            if (manager == null)
            {
                Debug.LogError("[Blackjack] El prefab MenuSystem no tiene MenuManager.");
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
            Debug.Log("[Blackjack] MenuSystem: agregada la pantalla Blackjack.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contenido);
        }
    }
}
