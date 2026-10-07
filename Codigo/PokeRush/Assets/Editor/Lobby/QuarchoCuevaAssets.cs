using System.Linq;
using UnityEditor;
using UnityEngine;

// Los assets de la Quarcho Cueva: dialogos, la tienda de Tixi, los objetos
// nuevos que vende y las tres ruletas de Martin.
//
// Regla de siempre (la misma de los NPCs del casino): lo que es TEXTO del
// equipo (dialogos) se crea una vez y no se pisa nunca; lo que es BALANCE del
// builder (precios, premios) se crea una vez y despues se toca a mano en el
// Inspector, asi que tampoco se pisa. Volver a correr el builder no borra nada
// que alguien haya cambiado.
public static class QuarchoCuevaAssets
{
    public const string Carpeta = "Assets/Resources/Definitions/Lobby";
    public const string Dialogos = Carpeta + "/Dialogos";
    private const string Items = "Assets/Resources/Definitions/Items/";
    private const string Pokens = "Assets/Resources/Definitions/Pokens/";
    private const string IconosRuleta = "Assets/Graphics/Ruleta/";
    private const string IconosItems = "Assets/Graphics/Items/";

    // Lo que cuesta cada tiro de las ruletas de Martin (playtest del 5-oct:
    // "ajusta el precio"). A 150 la de objetos era plata gratis: solo los
    // gajos de 300 y 1000 ya devolvian 168 por tiro de promedio. Ahora la
    // plata se saca del Entrenamiento intenso del chip (400 a 1200 por
    // victoria): un tiro de objetos o de mejoras es mas o menos una pelea y
    // uno de Pokemon, dos o tres.
    public const int PrecioRuletaPokemon = 1500;
    public const int PrecioRuletaObjetos = 600;
    public const int PrecioRuletaMejoras = 500;

    // ---------- dialogos (texto del equipo: no se pisan) ----------

    // El lore de Tixi. Son lineas de RELLENO: el lore lo escribe el equipo en
    // este asset (Resources/Definitions/Lobby/Dialogos/Lobby_Tixi_Lore).
    // Si el asset ya existe no se toca: la historia de verdad (historia.pdf)
    // esta escrita ahi, con varios que hablan, preguntas y lo que sigue.
    public static DialogueDefinition LoreDeTixi(Sprite retrato) => Dialogo("Lobby_Tixi_Lore", "TIXI", retrato,
        "[TIXI] ¡Por fin llegaste! Te estaba esperando.",
        "[TIXI] Desde acá vamos a salir a cada misión, y acá vas a volver cada vez que termines una.",
        "[TIXI] El primer servidor queda cerca. Quién lo cuida, no lo sé: eso lo vas a descubrir vos.");

    public static DialogueDefinition CharlaDeNestor(Sprite retrato) => Dialogo("Lobby_Nestor_Charla", "NÉSTOR", retrato,
        "[NÉSTOR] Ante todo, buenos días. Te dije que cuando me acordara de todo te iba a buscar.",
        "[NÉSTOR] Mientras tanto, armé acá mi puesto de MT. Si querés que alguno de tus Pokémon aprenda algo nuevo, avisame.");

    public static DialogueDefinition CharlaDeMartin(Sprite retrato) => Dialogo("Lobby_Martin_Charla", "MARTÍN", retrato,
        "Nunca nadie me había ganado en mi propia mesa.",
        "Me traje las ruletas y los juegos del casino. Acá la casa también gana... casi siempre.");

    private static DialogueDefinition Dialogo(string archivo, string hablante, Sprite retrato, params string[] lineas)
    {
        MenuUiKit.EnsureFolder(Dialogos);
        string path = $"{Dialogos}/{archivo}.asset";
        var def = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(path);
        if (def != null) return def;

        def = ScriptableObject.CreateInstance<DialogueDefinition>();
        def.speakerName = hablante;
        def.speakerPortrait = retrato;
        def.lines = lineas;
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    // ---------- objetos nuevos ----------

    // Revivir y Cura Total: la tienda de Tixi los necesita y el proyecto solo
    // tenia pociones. El nombre es la clave con la que los guarda SaveService,
    // asi que no se cambia una vez creado.
    public static HealItemDefinition Revivir() => Objeto("Revivir", o =>
    {
        o.itemName = "Revivir";
        o.description = "Reanima a un Pokémon debilitado y le devuelve la mitad de sus PS.";
        o.buyPrice = 1500;
        o.sellPrice = 750;
        o.revives = true;
        o.revivePercent = 50;
        o.icon = MenuUiKit.Sprite(IconosItems + "REVIVE.png");
    });

    public static HealItemDefinition CuraTotal() => Objeto("CuraTotal", o =>
    {
        o.itemName = "Cura Total";
        o.description = "Cura todos los problemas de estado de un Pokémon.";
        o.buyPrice = 400;
        o.sellPrice = 200;
        o.curesAllStatuses = true;
        o.icon = MenuUiKit.Sprite(IconosItems + "FULLHEAL.png");
    });

    private static HealItemDefinition Objeto(string archivo, System.Action<HealItemDefinition> armar)
    {
        string path = $"{Items}{archivo}.asset";
        var def = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(path);
        if (def != null) return def;

        def = ScriptableObject.CreateInstance<HealItemDefinition>();
        armar(def);
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    private static HealItemDefinition Existente(string archivo)
    {
        var def = AssetDatabase.LoadAssetAtPath<HealItemDefinition>($"{Items}{archivo}.asset");
        if (def == null) Debug.LogError($"[Lobby] No existe el objeto {archivo}.");
        return def;
    }

    // ---------- la tienda de Tixi ----------

    public static VendorDefinition TiendaDeTixi(Sprite retrato)
    {
        MenuUiKit.EnsureFolder(Carpeta);
        string path = $"{Carpeta}/TiendaDeTixi.asset";
        var def = AssetDatabase.LoadAssetAtPath<VendorDefinition>(path);
        if (def != null) return def;

        def = ScriptableObject.CreateInstance<VendorDefinition>();
        def.characterName = "TIXI";
        def.portrait = retrato;
        def.healItemsForSale = new[]
        {
            Existente("Pocion"), Existente("SuperPocion"), Existente("HiperPocion"),
            Revivir(), CuraTotal(), Existente("PocionMaxima")
        }.Where(o => o != null).ToArray();
        def.battleItemsForSale = new BattleItemDefinition[0];
        AssetDatabase.CreateAsset(def, path);
        return def;
    }

    // ---------- las ruletas de Martin ----------

    // Siempre un Pokemon, un escalon arriba de la del empresario del piso 3
    // (Nv 50-52: se gira despues del casino, para llegar a Julieta). El icono
    // marca la rareza, como en el resto de las ruletas (todas formas finales,
    // ver VariedadDeLasRuletas):
    //   Poke Ball  (comun):     4 x 15% = 60%
    //   Great Ball (bueno):     2 x 12% = 24%
    //   Ultra Ball (raro):      1 x 12% = 12%
    //   Master Ball (muy raro): 1 x  4% =  4%
    public static CasinoPrizeTable RuletaDePokemon() => Tabla("RuletaDeMartin_Pokemon", () => new[]
    {
        Pokemon("0425_Drifloon", 50, 15, "icon_pokeball"),
        Pokemon("0447_Riolu", 51, 12, "icon_greatball"),
        Pokemon("0551_Sandile", 50, 15, "icon_pokeball"),
        Pokemon("0782_Jangmo-o", 52, 12, "icon_ultraball"),
        Pokemon("0607_Litwick", 50, 15, "icon_pokeball"),
        Pokemon("0704_Goomy", 51, 12, "icon_greatball"),
        Pokemon("0661_Fletchling", 50, 15, "icon_pokeball"),
        Pokemon("0885_Dreepy", 52, 4, "icon_masterball")
    });

    // Curativos y plata: 72% objetos, 28% plata.
    public static CasinoPrizeTable RuletaDeObjetos() => Tabla("RuletaDeMartin_Objetos", () => new[]
    {
        ObjetoPremio(Existente("Pocion"), "Poción", "Pociones", "una", 3, 18, IconosRuleta + "icon_pocion.png"),
        ObjetoPremio(Existente("SuperPocion"), "Superpoción", "Superpociones", "una", 2, 16, IconosRuleta + "icon_superpocion.png"),
        Plata(300, 16),
        ObjetoPremio(Revivir(), "Revivir", "Revivir", "un", 1, 12, IconosItems + "REVIVE.png"),
        ObjetoPremio(Existente("HiperPocion"), "Hiperpoción", "Hiperpociones", "una", 1, 12, IconosRuleta + "icon_hiperpocion.png"),
        ObjetoPremio(CuraTotal(), "Cura Total", "Curas Totales", "una", 2, 8, IconosItems + "FULLHEAL.png"),
        ObjetoPremio(Existente("PocionMaxima"), "Poción Máxima", "Pociones Máximas", "una", 1, 6, IconosItems + "MAXPOTION.png"),
        Plata(1000, 12)
    });

    // Una mejora para la PROXIMA batalla (ver MejorasDeBatalla). Los iconos
    // son las fichas de los objetos X del pack.
    public static CasinoPrizeTable RuletaDeMejoras() => Tabla("RuletaDeMartin_Mejoras", () => new[]
    {
        Mejora(MejoraDeBatalla.Ataque, 1, 16, "XATTACK.png"),
        Mejora(MejoraDeBatalla.Defensa, 1, 16, "XDEFENSE.png"),
        Mejora(MejoraDeBatalla.AtaqueEspecial, 1, 16, "XSPATK.png"),
        Mejora(MejoraDeBatalla.DefensaEspecial, 1, 16, "XSPDEF.png"),
        Mejora(MejoraDeBatalla.Velocidad, 1, 14, "XSPEED.png"),
        Mejora(MejoraDeBatalla.Precision, 1, 10, "XACCURACY.png"),
        Mejora(MejoraDeBatalla.Ataque, 2, 8, "XATTACK2.png"),
        MejoraConIcono(MejoraDeBatalla.Todas, 1, 4, IconosRuleta + "icon_rare_candy.png")
    });

    private static CasinoPrizeTable Tabla(string archivo, System.Func<Prize[]> premios)
    {
        MenuUiKit.EnsureFolder(Carpeta);
        string path = $"{Carpeta}/{archivo}.asset";
        var tabla = AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(path);
        if (tabla != null) return tabla;

        tabla = ScriptableObject.CreateInstance<CasinoPrizeTable>();
        tabla.premios = premios();
        AssetDatabase.CreateAsset(tabla, path);
        return tabla;
    }

    private static Prize Pokemon(string especie, int nivel, float peso, string icono)
    {
        var def = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{especie}.asset");
        if (def == null) Debug.LogError($"[Lobby] No existe la especie {especie}.");
        return new Prize
        {
            prizeName = def != null ? def.pokenName : especie,
            article = "",
            amount = 1,
            probability = peso,
            icon = CasinoNpcBuilder.SpriteSimple($"{IconosRuleta}{icono}.png"),
            poken = def,
            pokenLevel = nivel,
            moneyIfTeamFull = 500
        };
    }

    private static Prize ObjetoPremio(HealItemDefinition item, string nombre, string plural, string articulo,
                                      int cantidad, float peso, string icono)
        => new Prize
        {
            prizeName = nombre,
            prizePlural = plural,
            article = articulo,
            amount = cantidad,
            probability = peso,
            icon = CasinoNpcBuilder.SpriteSimple(icono),
            item = item
        };

    private static Prize Plata(int monedas, float peso)
        => new Prize
        {
            prizeName = $"{monedas} monedas",
            article = "",
            amount = 1,
            probability = peso,
            icon = CasinoNpcBuilder.SpriteSimple(IconosRuleta + "icon_monedas.png"),
            money = monedas
        };

    private static Prize Mejora(MejoraDeBatalla mejora, int etapas, float peso, string icono)
        => MejoraConIcono(mejora, etapas, peso, IconosItems + icono);

    private static Prize MejoraConIcono(MejoraDeBatalla mejora, int etapas, float peso, string icono)
        => new Prize
        {
            prizeName = MejorasDeBatalla.Describir(new MejoraPendiente(mejora, etapas)),
            article = "",
            amount = 1,
            probability = peso,
            icon = CasinoNpcBuilder.SpriteSimple(icono),
            mejora = mejora,
            etapasDeMejora = etapas
        };
}
