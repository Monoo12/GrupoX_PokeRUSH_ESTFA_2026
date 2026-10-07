using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Los assets de la pelea contra Julieta: su EnemyDefinition (equipo, sprites,
// IA) y el GUION (que dice y cuando cambia cada cosa).
//
// Los dos se crean SOLO si no existen: una vez creados son del equipo y se
// tocan desde el Inspector. Para volver a los del codigo hay un menu aparte
// (PokeRush > Jefe final (Julieta) > Rehacer el guion desde el código), que
// avisa que pisa.
//
// El guion sigue la historia (Downloads/historia.pdf, version del 4-oct-2026):
// la charla de antes de pelear ("Servidor principal"), la de despues ("Combate
// final -- Julieta"), el epilogo y la ultima conexion de la cueva salen del
// PDF casi palabra por palabra. Lo que el PDF no cuenta es lo de ADENTRO de la
// pelea (las cuatro fases); esos textos no adelantan lo que Julieta explica al
// final, y la ruptura la abre el transmisor que Tixi le da al PJ en la cueva.
// Ver "Decisiones de historia" en Jefe_Final_Julieta.md.
public static class PeleaJulietaAssets
{
    public const string Carpeta = "Assets/Resources/Definitions/Julieta";
    public const string RutaJulieta = Carpeta + "/Julieta.asset";
    public const string RutaGuion = Carpeta + "/GuionDeJulieta.asset";
    public const string RutaRuleta = "Assets/Resources/Definitions/Casino/RuletaDelJefeFinal.asset";

    private const string Pokens = "Assets/Resources/Definitions/Pokens/";
    private const string Perfiles = "Assets/Resources/Definitions/AIProfiles/";

    // El arte que paso el equipo (Graphics/Juli), pasado a pixel art: hoja 4x4
    // de 64x80 (72 px de alto: mas grande que el resto, es la jefa final) y retrato a resolucion nativa (100x100, importado a PPU 62,5
    // para medir en combate lo mismo que un entrenador de 160x160 a PPU 100).
    // La cientifica del pack (la provisoria) la sigue usando la biologa del Data Center.
    public const string RetratoDeJulieta = "Assets/Graphics/Trainers/JULIETA.png";
    public const string HojaDeJulieta = "julieta";
    public const string FondoDelCombate = "Assets/Sprites/Battlebacks/Laboratorio_bg.png";

    private const string RetratoNestor = "Assets/Sprites/Nestor/NESTOR_retrato_pensando.png";
    private const string RetratoNestorShock = "Assets/Sprites/Nestor/NESTOR_retrato_shock.png";
    private const string RetratoNestorFeliz = "Assets/Sprites/Nestor/NESTOR_retrato_feliz.png";
    private const string RetratoNestorPreocupado = "Assets/Sprites/Nestor/NESTOR_retrato_preocupado.png";
    private const string RetratoPJVaron = "Assets/Sprites/Trainers/POKEMONTRAINER_RojoNeutro.png";
    private const string RetratoPJMujer = "Assets/Sprites/Trainers/POKEMONTRAINER_HojaNeutra.png";
    // El mismo retrato que usa Tixi en la cueva (QuarchoTixiNpc).
    private const string RetratoDeTixi = "Assets/Graphics/Trainers/TIXI.png";
    private const string FuenteDelFinal = "Assets/TextMesh Pro/Fonts/power clear.ttf";

    // La charla final con Tixi en la cueva: un DialogueDefinition, como el
    // resto de lo que dice Tixi (el texto se edita en el asset).
    public const string RutaCharlaEnLaCueva = Carpeta + "/Dialogos/Final_Tixi_EnLaCueva.asset";

    // Los efectos del final (sacados del pack de Essentials, AudioAÑIL/SE).
    public const string CarpetaDeSonidos = "Assets/Audio/Efectos/Final/";
    private const string SonidoTransmisor = CarpetaDeSonidos + "transmisor.ogg";
    private const string SonidoNucleo = CarpetaDeSonidos + "nucleo_destruido.wav";
    private const string SonidoChip = CarpetaDeSonidos + "chip_apagado.ogg";

    // El equipo: tecnologia y mente, ninguno repetido de Nestor, y los dos del
    // portal de 2026 como ases (GDD: "sus dos ases son los dos Pokemon del
    // portal"). Rebalanceo del 6/7-oct-2026: su as (Mewtwo 50) queda arriba
    // del de Reymar (49) y el resto, 43-47. En el simulador (6 contra 6, sin el
    // guion) el equipo tipico de la partida le gana al primer intento: con
    // 47-52, 11%; con 45-50, 27%; asi, 43%. Lo que mas pesa es Mewtwo y que
    // Alakazam no llegue a Premonicion (120, la aprende a Nv 45). El guion suma
    // ayudas y trabas: es la pelea a probar a mano.
    private static readonly (string especie, int nivel)[] Equipo =
    {
        ("0462_Magnezone", 43),
        ("0065_Alakazam", 44),
        ("0649_Genesect", 44),
        ("0376_Metagross", 45),
        ("0720_Hoopa", 47),
        ("0150_Mewtwo", 50),
    };

    // ------------------------------------------------------------ Julieta

    public static EnemyDefinition Julieta()
    {
        AsegurarCarpeta();
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(RutaJulieta);
        bool nueva = def == null;
        if (nueva)
        {
            def = ScriptableObject.CreateInstance<EnemyDefinition>();
            AssetDatabase.CreateAsset(def, RutaJulieta);
        }

        // Lo que falte se completa; lo que el equipo ya puso no se toca.
        if (string.IsNullOrEmpty(def.characterName)) def.characterName = "Julieta";
        Sprite retrato = CasinoNpcBuilder.SpriteRetrato(RetratoDeJulieta);
        if (def.portrait == null) def.portrait = retrato;
        if (def.battleFrontSprite == null) def.battleFrontSprite = retrato;
        if (def.battleBackground == null) def.battleBackground = AssetDatabase.LoadAssetAtPath<Sprite>(FondoDelCombate);
        if (def.aiProfile == null) def.aiProfile = Perfil("FinalBoss");
        if (def.defeatGrantsFlag == StoryFlag.None) def.defeatGrantsFlag = StoryFlag.Julieta_Vencida;

        if (def.pokenTeam == null || def.pokenTeam.Length == 0)
        {
            var equipo = new List<EnemyTeamEntry>();
            foreach (var (especie, nivel) in Equipo)
            {
                var p = AssetDatabase.LoadAssetAtPath<PokenDefinition>($"{Pokens}{especie}.asset");
                if (p == null) { Debug.LogError($"[Julieta] No existe la especie {especie}."); continue; }
                equipo.Add(new EnemyTeamEntry { species = p, level = nivel });
            }
            def.pokenTeam = equipo.ToArray();
            // El jefe final no paga: despues de ganarle vienen los creditos.
            def.moneyReward = 0;
        }

        EditorUtility.SetDirty(def);
        if (nueva) Debug.Log($"[Julieta] Creada {RutaJulieta}.");
        return def;
    }

    public static AIProfileDefinition Perfil(string nombre)
        => AssetDatabase.LoadAssetAtPath<AIProfileDefinition>($"{Perfiles}{nombre}.asset");

    public static CasinoPrizeTable RuletaDelCasino()
        => AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(RutaRuleta);

    // ------------------------------------------------------------ guion

    public static GuionDeJulieta Guion()
    {
        AsegurarCarpeta();
        var guion = AssetDatabase.LoadAssetAtPath<GuionDeJulieta>(RutaGuion);
        if (guion != null) return guion;

        guion = ScriptableObject.CreateInstance<GuionDeJulieta>();
        Escribir(guion);
        AssetDatabase.CreateAsset(guion, RutaGuion);
        Debug.Log($"[Julieta] Creado el guion en {RutaGuion}.");
        return guion;
    }

    [MenuItem("PokeRush/Jefe final (Julieta)/Rehacer el guion desde el código (pisa los cambios)")]
    public static void RehacerGuion()
    {
        var guion = AssetDatabase.LoadAssetAtPath<GuionDeJulieta>(RutaGuion);
        if (guion == null) { Guion(); return; }

        if (!Application.isBatchMode && !EditorUtility.DisplayDialog("Rehacer el guion",
                "Esto reemplaza TODO el guion de Julieta por el que está escrito en PeleaJulietaAssets.cs. " +
                "Lo que se haya cambiado desde el Inspector se pierde.", "Rehacer", "Cancelar"))
            return;

        Escribir(guion);
        EditorUtility.SetDirty(guion);
        AssetDatabase.SaveAssets();
        Debug.Log("[Julieta] Guion rehecho desde el código.");
    }

    // Atajos para que el guion de abajo se lea como un guion.
    private static LineaDeJulieta J(string t, AccionDeJulieta a = AccionDeJulieta.Ninguna, int p = 0)
        => new LineaDeJulieta(HablanteDeJulieta.Julieta, t, a, p);
    private static LineaDeJulieta PJ(string t, AccionDeJulieta a = AccionDeJulieta.Ninguna, int p = 0)
        => new LineaDeJulieta(HablanteDeJulieta.PJ, t, a, p);
    private static LineaDeJulieta N(string t, AccionDeJulieta a = AccionDeJulieta.Ninguna, int p = 0, string cara = null)
        => new LineaDeJulieta(HablanteDeJulieta.Nestor, t, a, p) { retrato = cara != null ? Sprite(cara) : null };
    private static LineaDeJulieta S(string t, AccionDeJulieta a = AccionDeJulieta.Ninguna, int p = 0)
        => new LineaDeJulieta(HablanteDeJulieta.Sistema, t, a, p);
    // Lo que se narra va sin cartel de nombre, igual que los avisos del sistema.
    private static LineaDeJulieta Narra(string t, AccionDeJulieta a = AccionDeJulieta.Ninguna, int p = 0)
        => new LineaDeJulieta(HablanteDeJulieta.Sistema, t, a, p);
    private static LineaDeJulieta T(string t, AccionDeJulieta a = AccionDeJulieta.Ninguna, int p = 0)
        => new LineaDeJulieta(HablanteDeJulieta.Tixi, t, a, p);
    private static LineaDeJulieta Sonando(LineaDeJulieta l, string sonido)
    {
        l.sonido = AssetDatabase.LoadAssetAtPath<AudioClip>(sonido);
        if (l.sonido == null) Debug.LogWarning($"[Julieta] No esta el sonido {sonido}.");
        return l;
    }
    private static CondicionDeJulieta C(DisparadorDeJulieta d, int v = 0) => new CondicionDeJulieta(d, v);

    private static MomentoDeJulieta M(string nombre, FaseDeJulieta fase, CondicionDeJulieta[] cuando,
                                      params LineaDeJulieta[] lineas)
        => new MomentoDeJulieta { nombre = nombre, fase = fase, cuando = cuando, lineas = lineas };

    private const int AFavorDelJugador = (int)QuienCobraLaRuleta.AFavorDelJugador;
    private const int AFavorDelRival = (int)QuienCobraLaRuleta.AFavorDelRival;

    public static void Escribir(GuionDeJulieta g)
    {
        g.nombreJulieta = "JULIETA";
        g.nombreNestor = "NÉSTOR";
        g.nombreTixi = "TIXI";
        g.retratoJulieta = CasinoNpcBuilder.SpriteRetrato(RetratoDeJulieta);
        g.retratoNestor = Sprite(RetratoNestor);
        g.retratoTixi = CasinoNpcBuilder.SpriteRetrato(RetratoDeTixi);
        g.retratoPJVaron = PrimerSprite(RetratoPJVaron);
        g.retratoPJMujer = PrimerSprite(RetratoPJMujer);
        g.fuente = AssetDatabase.LoadAssetAtPath<Font>(FuenteDelFinal);

        const FaseDeJulieta F1 = FaseDeJulieta.Analisis;
        const FaseDeJulieta F2 = FaseDeJulieta.ControlDelSistema;
        const FaseDeJulieta F3 = FaseDeJulieta.ControlDelVinculo;
        const FaseDeJulieta F4 = FaseDeJulieta.Ruptura;

        // ---------------------------------------------------------- en la arena
        // historia.pdf, "Servidor principal". Lo de antes (la llegada al
        // Ameghino, la alarma, la puerta) lo cuentan las zonas de la arena.

        g.alEncontrarla = new[]
        {
            Narra("En el centro de la sala está el servidor principal, conectado a cientos de cables que recorren las paredes."),
            Narra("Frente a él está Julieta."),
            J("Llegaste."),
            PJ("¿Esperabas que viniera?"),
            J("Después de todo lo que hiciste, era cuestión de tiempo."),
            PJ("Destruí tus servidores."),
            J("Lo sé."),
            PJ("Desconecté a tus guardianes."),
            J("También lo sé."),
            PJ("Y ahora voy a destruir este."),
            J("Se ve que pensar no es lo tuyo... ¿De verdad pensás que es tan sencillo?"),
            Narra("Julieta se da vuelta y observa el servidor."),
            J("¿Sabés cuántas personas dependen de esto?"),
            PJ("Demasiadas."),
            J("¿Y sabés qué pasaría si lo destruyeras?"),
            PJ("No me interesa."),
            J("Claro que no. Nunca te interesó entender lo que hicimos."),
            PJ("Entonces explicámelo."),
            Narra("Julieta se queda en silencio unos segundos."),
            J("Está bien. Pero primero vas a tener que demostrarme que realmente merecés estar acá."),
            PJ("¿Y cómo pensás hacer eso?"),
            J("De la misma forma que siempre."),
            Narra("Julieta se pone frente a vos."),
            J("Peleando."),
            PJ("Por fin."),
        };

        g.alReintentar = new[]
        {
            J("Volviste."),
            J("Bien. Demostrame que esta vez sí merecés estar acá."),
        };

        // ---------------------------------------------------------- la pelea
        // El PDF no cuenta la pelea por dentro. Lo que se dice aca no adelanta
        // lo que Julieta explica al final (los experimentos, la sala de 2026):
        // solo lo promete ("ganame y te lo cuento").

        var m = new List<MomentoDeJulieta>();

        // ===== FASE 1: Julieta analiza. El jugador tiene todo a favor. =====

        m.Add(M("Inicio: Nestor y la ruleta en linea", F1, new[] { C(DisparadorDeJulieta.AlEmpezar) },
            S("PROTOCOLO NÉSTOR: EN LÍNEA.", AccionDeJulieta.EncenderNestor),
            N("{PJ}, estoy conectado. Yo le leo las jugadas, vos pegá."),
            S("RULETA DE REYMAR: EN LÍNEA. JUEGA A FAVOR DE {PJ}.", AccionDeJulieta.EncenderRuleta, AFavorDelJugador),
            J("Néstor en tu oído y la ruleta de Reymar en el bolsillo. Qué bien equipado venís.",
              AccionDeJulieta.PerfilDeIA, 0),
            J("Adelante. Yo solo voy a mirar.")));

        m.Add(M("F1: primera lectura de Nestor", F1, new[] { C(DisparadorDeJulieta.NestorLeyo, 1) },
            J("Ahí está. El Protocolo Néstor."),
            J("Ve la jugada antes de que pase, ¿no? Qué curioso. Así pienso yo."),
            N("No la escuches, {PJ}. Seguí.")));

        m.Add(M("F1: primer giro de la ruleta", F1, new[] { C(DisparadorDeJulieta.RuletaGiroEnLaFase, 1) },
            J("La ruleta de Reymar. Te la dio cuando lo desconectaste, ¿no?"),
            J("Un generador de números al azar. Qué nombre lindo para algo que no existe.")));

        m.Add(M("F1: Julieta pierde el primero", F1, new[] { C(DisparadorDeJulieta.AlRivalLeQuedan, 5) },
            J("Golpe limpio. Lo anoto."),
            J("Siempre fuiste así en clase: rápido para romper cosas, lento para preguntarte por qué funcionan.")));

        m.Add(M("F1: Julieta termina de estudiar", F1, new[] { C(DisparadorDeJulieta.TurnosEnLaFase, 3) },
            J("Tres turnos. Ya sé cuánto tarda Néstor en recalcular y cada cuánto gira la ruleta."),
            J("¿Sabés qué es lo gracioso, {PJ}? Todo lo que te está ayudando corre sobre mi red.")));

        // ----- La vuelta: pasa a la fase 2 -----
        m.Add(M("F1 -> F2: Julieta cambia las reglas", F1, new[]
            {
                C(DisparadorDeJulieta.TurnosEnLaFase, 5),
                C(DisparadorDeJulieta.AlRivalLeQuedan, 4),
                C(DisparadorDeJulieta.RivalSalvado),
            },
            J("Suficiente. Ya entendí cómo peleás."),
            J("Ahora te explico yo cómo funciona esto.", AccionDeJulieta.Destello),
            S("NEURARUSH-LINK: ACCESO DE ADMINISTRADORA CONCEDIDO.", AccionDeJulieta.SenalDeControl, 1),
            N("¡{PJ}! Se metió en el canal... no... no la puedo sacar...", cara: RetratoNestorShock),
            S("PROTOCOLO NÉSTOR: CONEXIÓN PERDIDA.", AccionDeJulieta.ApagarNestor),
            J("El Protocolo Néstor es una función de mi NeuraRush-LINK. Néstor la usaba porque yo se la di."),
            J("Ahora lee tus jugadas. Para mí.", AccionDeJulieta.EncenderLecturaDeJulieta),
            S("RULETA DE REYMAR: PREMIOS REASIGNADOS. JUEGA A FAVOR DE: JULIETA.",
              AccionDeJulieta.EncenderRuleta, AFavorDelRival),
            J("Y la ruleta... Reymar la giraba. Yo cambié los permisos.", AccionDeJulieta.PerfilDeIA, 1),
            J("La casa siempre gana, {PJ}. Y la casa soy yo.", AccionDeJulieta.PasarAFase, (int)F2)));

        // ===== FASE 2: el sistema es de Julieta. =====

        m.Add(M("F2: primer giro dado vuelta", F2, new[] { C(DisparadorDeJulieta.RuletaGiroEnLaFase, 1) },
            J("¿Lo ves? Los mismos números, la misma rueda. Lo único que cambió es para quién juega."),
            J("Eso es el azar, {PJ}: una regla que todavía nadie te contó.")));

        m.Add(M("F2: 2026", F2, new[] { C(DisparadorDeJulieta.TurnosEnLaFase, 2) },
            J("¿Te acordás de la oficina de la directora? El día de la graduación."),
            PJ("Estaba ahí. Vi el portal, a Mewtwo, a Hoopa... y a vos tirándoles el LINK."),
            J("Viste el final. ¿Y de lo que pasó antes en esa sala, qué te acordás?"),
            PJ("...¿Qué pasó antes?"),
            J("Ganame y te lo cuento.")));

        m.Add(M("F2: Julieta pierde otro", F2, new[] { C(DisparadorDeJulieta.AlRivalLeQuedan, 3) },
            J("Seguís pegando fuerte. Pero ya no estás peleando contra mis Pokémon."),
            J("Estás peleando contra las reglas. Y las reglas son mías.")));

        m.Add(M("F2: el jugador pierde uno", F2, new[] { C(DisparadorDeJulieta.JugadorPerdio, 1) },
            J("Uno menos. En 2026 la red colapsó en un segundo. Tu equipo va a tardar un poco más.")));

        // ----- Julieta entra al vinculo: pasa a la fase 3 -----
        m.Add(M("F2 -> F3: Julieta entra al vinculo", F2, new[]
            {
                C(DisparadorDeJulieta.TurnosEnLaFase, 5),
                C(DisparadorDeJulieta.AlRivalLeQuedan, 2),
                C(DisparadorDeJulieta.RivalSalvado),
            },
            J("Te voy a mostrar para qué sirve de verdad el NeuraRush-LINK."),
            J("Conecta. Personas, máquinas... Pokémon."),
            J("Y un Pokémon conectado escucha a quien tiene la red.", AccionDeJulieta.Destello),
            S("NEURARUSH-LINK: ACCEDIENDO A {POKEMON}...", AccionDeJulieta.ControlarPokemon),
            PJ("¡{POKEMON}! ¡Soy yo!", AccionDeJulieta.MostrarVidaDelPJ, 1),
            J("Sigue en tu equipo. Sigue teniendo tu nombre en la ficha. Pero ya no te escucha."),
            J("Ahora no alcanza con ganarme, {PJ}. Tenés que sobrevivirte.", AccionDeJulieta.PasarAFase, (int)F3)));

        // ===== FASE 3: el peligro es interno. =====

        m.Add(M("F3: primer golpe al PJ", F3, new[] { C(DisparadorDeJulieta.ControladoAtacoAlPJ, 1) },
            PJ("¡Agh...! ¡{POKEMON}, pará!"),
            J("Duele porque es tuyo. Para eso sirve un vínculo.")));

        m.Add(M("F3: Tacconi y el registro", F3, new[] { C(DisparadorDeJulieta.TurnosEnLaFase, 2) },
            PJ("Tacconi encontró mi nombre en un registro de 2026. El del primer experimento del LINK."),
            PJ("Dijo que yo no era uno de los sujetos. Que era parte del desarrollo."),
            J("Tacconi siempre leyó de más."),
            J("Pero esta vez leyó bien.")));

        m.Add(M("F3: el PJ se da cuenta", F3, new[] { C(DisparadorDeJulieta.VidaDelPJBaja, 60) },
            PJ("Si lo dejo en el campo me va a terminar... Tengo que sacarlo. O terminar esto rápido.")));

        m.Add(M("F3: el PJ al limite", F3, new[] { C(DisparadorDeJulieta.VidaDelPJBaja, 30) },
            J("Mirate, {PJ}. Cinco años esquivando el chip, y te tira abajo tu propio equipo.")));

        m.Add(M("F3: el controlado cayo", F3, new[] { C(DisparadorDeJulieta.ControladoCayo) },
            J("Uno menos. No importa: tu equipo tiene más puertas."),
            S("NEURARUSH-LINK: ACCEDIENDO A {POKEMON}...", AccionDeJulieta.ControlarPokemon)));

        m.Add(M("F3: el ultimo de Julieta", F3, new[] { C(DisparadorDeJulieta.UltimoDelRival) },
            J("Me queda uno. Y mientras la red sea mía, no cae.")));

        // ----- La ruptura: el transmisor de Tixi abre la red -----
        // historia.pdf, vuelta del casino: "Si conseguís llegar al servidor,
        // voy a poder conectarme desde acá e intentar abrirte el acceso". Julieta
        // no sabe quien entro: se entera recien al final ("¿Tixi?").
        m.Add(M("F3 -> Ruptura: Tixi abre la red", F3, new[]
            {
                C(DisparadorDeJulieta.TurnosEnLaFase, 5),
                C(DisparadorDeJulieta.RivalSalvado),
            },
            J("¿Ves, {PJ}? Así se ve una red cuando tiene dueña."),
            Sonando(S("TRANSMISOR EXTERNO DETECTADO. ORIGEN: DESCONOCIDO.", AccionDeJulieta.Destello), SonidoTransmisor),
            T("¡{PJ}! ¡Entré! El transmisor me abrió una puerta en la red. No sé cuánto la puedo sostener."),
            J("¿Quién más está en mi red?"),
            T("El chip nunca te pudo escribir, {PJ}. Hablale a {POKEMON} por el LINK: a vos te va a escuchar."),
            PJ("{POKEMON}... soy yo. Volvé.", AccionDeJulieta.Destello),
            S("VÍNCULO LIBERADO.", AccionDeJulieta.LiberarPokemon),
            S("LECTURA DE LA ADMINISTRADORA: REVOCADA.", AccionDeJulieta.ApagarLecturaDeJulieta),
            S("PROTOCOLO NÉSTOR: EN LÍNEA.", AccionDeJulieta.EncenderNestor),
            N("¡{PJ}! ¡Volví! Alguien abrió el canal de nuevo.", cara: RetratoNestorFeliz),
            S("RULETA DE REYMAR: JUEGA A FAVOR DE {PJ}.", AccionDeJulieta.EncenderRuleta, AFavorDelJugador),
            J("No... esto no estaba en el plan.", AccionDeJulieta.SenalDeControl, 0),
            T("Ya está. Terminalo, {PJ}."),
            PJ("Estoy bien. Sigo en pie.", AccionDeJulieta.MostrarVidaDelPJ, 0),
            PJ("Ahora sí, profe. Terminemos.", AccionDeJulieta.PasarAFase, (int)F4)));

        // ===== RUPTURA: todo vuelve al jugador y Julieta ya puede caer. =====

        m.Add(M("Ruptura: el ultimo de Julieta", F4, new[] { C(DisparadorDeJulieta.UltimoDelRival) },
            J("Me queda uno, {PJ}. Y no lo pienso soltar.")));

        m.Add(M("Ruptura: Nestor acompana", F4, new[] { C(DisparadorDeJulieta.TurnosEnLaFase, 2) },
            N("Ella nunca perdió un turno en su vida, {PJ}. Que este sea el primero.", cara: RetratoNestorPreocupado)));

        g.momentos = m.ToArray();

        // ---------------------------------------------------------- si pierde

        g.alPerder = new[]
        {
            J("Esto es lo que te quería mostrar, {PJ}. Sin mis reglas no hay juego."),
            J("Tu equipo vuelve a como entró. No me sirve de nada ganarle a alguien roto."),
            J("Cuando quieras, volvé a intentarlo. Yo no me voy a ningún lado."),
        };

        // ---------------------------------------------------------- el final
        // historia.pdf, "Combate final -- Julieta", entero. Despues vienen el
        // epilogo y la cueva (EscenaFinalEnLaCueva) y los creditos.

        g.finalDelJuego = new[]
        {
            Narra("El último Pokémon de Julieta cae. Ella se queda de pie unos segundos..."),
            Narra("...y finalmente cae de rodillas."),
            PJ("Se terminó."),
            J("No..."),
            PJ("Sí."),
            J("Si destruís ese servidor, todo va a terminar."),
            PJ("Eso es exactamente lo que vine a hacer."),
            J("No entendés lo que estás haciendo."),
            PJ("Entonces explicámelo."),
            // En el PDF esta mirada va antes de "Si destruis ese servidor": se
            // corrio al final del tramo porque es la que manda al PJ al servidor.
            Narra("Julieta mira hacia el servidor principal, que sigue encendido detrás de ella."),
        };

        // Lo que sigue va en tramos (playtest del 4-oct: era un bloque muy largo
        // de leer): cada uno arranca cuando el PJ toca el servidor con X, y el
        // ultimo cuando le habla a Julieta. Lo encadena PeleaJulieta.
        g.enElServidor = new[]
        {
            J("El NeuraRush-LINK no fue creado para controlar personas."),
            PJ("¿Entonces para qué?"),
            J("Para conectar."),
            PJ("¿Conectar qué?"),
            J("Personas. Máquinas. Pokémon. Todo."),
            PJ("Y para hacer eso destruyeron la vida de miles de personas."),
            J("Fue un accidente."),
            PJ("¿Un accidente que duró cinco años?"),
            J("Después de 2026 ya no había vuelta atrás."),
            PJ("Siempre hubo una vuelta atrás."),
            J("No."),
            Narra("Julieta te mira."),
            J("Vos sos la prueba de eso."),
            PJ("¿Qué querés decir?"),
            J("Vos sobreviviste sin modificaciones. Sin estabilizadores. Sin depender del chip."),
            PJ("Eso ya me lo dijo Reymar."),
            J("Reymar no sabía todo."),
            PJ("Entonces decime."),
            J("Cuando comenzamos los primeros experimentos, necesitábamos encontrar una forma de conectar un cerebro humano al sistema..."),
            J("...sin destruirlo."),
            PJ("Y me usaron para eso."),
            J("Te estudiamos."),
            PJ("¿Sin que yo lo supiera?"),
            J("Eras el único caso que funcionaba."),
            PJ("¿Y qué pasó después?"),
            J("El accidente."),
            PJ("¿Y después?"),
            J("Después intentamos controlar lo que habíamos creado."),
            PJ("Y terminaron conectando a todo el mundo."),
            J("No exactamente."),
            PJ("¿Qué significa eso?"),
            J("El NeuraRush-LINK no obligó a todos a conectarse."),
            PJ("¿Entonces?"),
            J("Una vez que comenzó a expandirse, las personas empezaron a conectarse voluntariamente."),
            PJ("¿Por qué?"),
            J("Porque podían hacer cosas que antes eran imposibles."),
            J("Compartir conocimientos."),
            J("Comunicarse sin hablar."),
            J("Aprender en segundos."),
            J("Incluso conectar sus pensamientos con Pokémon."),
            PJ("Y también perdieron sus recuerdos."),
            J("Algunos."),
            PJ("Se convirtieron en otra cosa."),
            J("Tal vez."),
            PJ("Entonces todo esto..."),
            J("Es el resultado de intentar solucionar un problema que nosotros mismos creamos."),
            Narra("Silencio."),
        };

        g.alDestruirlo = new[]
        {
            PJ("No importa cuánto intentes justificarlo."),
            PJ("Hay gente que perdió cinco años de su vida."),
            J("Lo sé."),
            PJ("Entonces se terminó."),
            J("Si destruís el servidor, también vas a destruir toda la red."),
            PJ("Tixi dijo lo mismo."),
            J("¿Tixi?"),
            PJ("Sí."),
            J("Entonces finalmente te encontró."),
            PJ("¿Conocías a Tixi?"),
            J("Todos los que trabajamos en el proyecto conocíamos su trabajo."),
            PJ("¿Tixi también estuvo en el proyecto?"),
            J("No como vos."),
            PJ("¿Qué significa eso?"),
            J("Que intentó detenernos antes del accidente."),
            PJ("¿Y por qué nunca me lo contó?"),
            J("Porque probablemente tenía miedo de que recordaras."),
            PJ("¿Recordar qué?"),
            J("Lo que pasó en aquella sala."),
            Narra("Te quedás en silencio."),
            PJ("¿Qué pasó?"),
            J("Vos estabas ahí cuando comenzó todo."),
            PJ("Yo..."),
            J("Y viste cómo empezó el primer experimento."),
            PJ("No recuerdo nada."),
            J("Por eso sos el único que todavía puede decidir qué hacer con esto."),
            PJ("¿Qué querés decir?"),
            J("Si destruís el servidor, nadie podrá volver a utilizar el NeuraRush-LINK."),
            PJ("Entonces lo voy a destruir."),
            J("Pero tampoco vas a poder recuperar tus propios recuerdos."),
            Narra("Mirás el servidor."),
            PJ("Ya perdí cinco años."),
            PJ("No voy a perder el resto de mi vida intentando recuperarlos."),
            J("..."),
            PJ("Se terminó, Julieta."),
            Narra("Te acercás al servidor principal."),
            J("Esperá."),
            PJ("¿Qué?"),
            J("Quizás algún día entiendas por qué lo hicimos."),
            PJ("Quizás."),
            PJ("Pero no va a ser con esto conectado."),
            Sonando(Narra("Destruís el núcleo del servidor.", AccionDeJulieta.RomperServidor), SonidoNucleo),
            S("ADVERTENCIA. SERVIDOR PRINCIPAL DESTRUIDO."),
            S("CONEXIÓN CON NEURARUSH-LINK INTERRUMPIDA. DESCONEXIÓN GLOBAL INICIADA."),
            Narra("Las luces de la sala comienzan a apagarse. Los cables que recorren las paredes dejan de emitir energía."),
            Narra("Julieta observa el servidor destruido."),
        };

        g.conJulietaEnElSuelo = new[]
        {
            J("Cinco años..."),
            PJ("Sí."),
            J("Todo este tiempo..."),
            PJ("Se terminó."),
            Narra("El NeuraRush-LINK de Julieta comienza a emitir pulsos.", AccionDeJulieta.Destello),
            Sonando(S("ERROR CRÍTICO. CONEXIÓN PERDIDA. DESCONEXIÓN INICIADA."), SonidoChip),
            Narra("El chip de Julieta se apaga. Julieta cae al suelo."),
            PJ("Julieta."),
            J("..."),
            PJ("¿Estás bien?"),
            J("No sé."),
            Narra("Julieta se mira las manos."),
            J("¿Qué año es?"),
            PJ("2031."),
            Narra("Julieta cierra los ojos."),
            J("Entonces funcionó."),
            PJ("¿Qué cosa?"),
            J("Por primera vez en cinco años..."),
            J("Estoy sola en mi propia cabeza."),
            Narra("No le respondés."),
            J("¿Qué vas a hacer ahora?"),
            PJ("No tengo idea."),
            J("¿Y Tixi?"),
            PJ("Esperándome."),
            J("Entonces andá."),
            PJ("¿Y vos?"),
            Narra("Julieta mira el servidor destruido."),
            J("Yo tengo mucho que recordar."),
            Narra("Te das vuelta y empezás a caminar hacia la salida."),
        };

        g.yaVencida = new[]
        {
            Narra("Julieta no te mira: mira el servidor principal."),
        };

        // ---------------------------------------------------------- el cierre
        // historia.pdf, "Epílogo" y "QuarchoCueva". Lo cuenta
        // EscenaFinalEnLaCueva; la charla con Tixi es un DialogueDefinition
        // (se edita desde el asset, como el resto de la cueva).

        g.epilogo = new[]
        {
            "Horas después.",
            "Desde una radio se narran noticias de distintas partes del mundo.",
            "Personas y Pokémon comienzan a recuperar sus recuerdos.",
            "Algunos se abrazan.\nOtros simplemente permanecen en silencio, intentando entender qué ocurrió.",
            "En distintas ciudades, los dispositivos conectados al NeuraRush-LINK dejan de funcionar.",
            "El mundo vuelve a escuchar su propio silencio.",
        };

        g.enLaCueva = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(RutaCharlaEnLaCueva);
        if (g.enLaCueva == null) Debug.LogWarning($"[Julieta] Falta la charla final con Tixi ({RutaCharlaEnLaCueva}).");

        g.ultimaConexion = new[]
        {
            "ÚLTIMA CONEXIÓN DETECTADA.",
            "UBICACIÓN: DESCONOCIDA.",
            "CONECTANDO NEURARUSH-LINK...",
            "CONECTANDO NEURARUSH-LINK...",
            "CONECTANDO NEURARUSH-LINK...",
            "ERROR.",
            "ERROR.",
            "INICIANDO PROCESO DE RECUPERACIÓN GLOBAL...",
            "INICIANDO PROCESO.",
            "NOMBRE EN CLAVE: VENTRESCA AI",
        };
    }

    // ------------------------------------------------------------ utilidades

    private static void AsegurarCarpeta()
    {
        if (AssetDatabase.IsValidFolder(Carpeta)) return;
        AssetDatabase.CreateFolder("Assets/Resources/Definitions", "Julieta");
    }

    private static Sprite Sprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    // Las hojas de entrenador de Sprites/Trainers vienen en modo Multiple: se
    // usa el primer cuadro sin reimportar nada (otras cosas usan la hoja).
    private static Sprite PrimerSprite(string path)
    {
        Sprite suelto = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (suelto != null) return suelto;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => s.name.Length).ThenBy(s => s.name).FirstOrDefault();
    }
}
