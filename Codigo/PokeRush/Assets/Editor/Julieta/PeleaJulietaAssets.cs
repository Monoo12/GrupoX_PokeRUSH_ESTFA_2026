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
// El guion sigue la historia (Downloads/historia.pdf y el GDD): 2026, la oficina
// de la directora, el NeuraRush-LINK, Mewtwo y Hoopa, el PJ escondido con su
// virus, Nestor y el Protocolo Nestor, Tacconi y el registro del primer
// experimento, la ruleta de Martin. Lo que la historia todavia no decia (POR QUE
// el PJ es inmune y para que era de verdad el LINK) esta resuelto aca de una
// forma que no contradice nada de lo anterior, y esta marcado en
// Jefe_Final_Julieta.md como decision a revisar por el equipo.
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

    // El equipo: tecnologia y mente, ninguno repetido de Nestor, y los dos del
    // portal de 2026 como ases (GDD: "sus dos ases son los dos Pokemon del
    // portal"). Un escalon arriba del jefe del casino (26-29).
    private static readonly (string especie, int nivel)[] Equipo =
    {
        ("0462_Magnezone", 33),
        ("0065_Alakazam", 34),
        ("0649_Genesect", 34),
        ("0376_Metagross", 35),
        ("0720_Hoopa", 37),
        ("0150_Mewtwo", 38),
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
        g.retratoJulieta = CasinoNpcBuilder.SpriteRetrato(RetratoDeJulieta);
        g.retratoNestor = Sprite(RetratoNestor);
        g.retratoPJVaron = PrimerSprite(RetratoPJVaron);
        g.retratoPJMujer = PrimerSprite(RetratoPJMujer);

        const FaseDeJulieta F1 = FaseDeJulieta.Analisis;
        const FaseDeJulieta F2 = FaseDeJulieta.ControlDelSistema;
        const FaseDeJulieta F3 = FaseDeJulieta.ControlDelVinculo;
        const FaseDeJulieta F4 = FaseDeJulieta.Ruptura;

        // ---------------------------------------------------------- en la arena

        g.alEncontrarla = new[]
        {
            S("SERVIDOR CENTRAL DE NEURARUSH-LINK. USUARIO SIN REGISTRAR EN LA SALA."),
            J("Llegaste, {PJ}. Tarde, como siempre. Pero llegaste."),
            PJ("Julieta."),
            J("Profesora Julieta. Que el mundo se haya dado vuelta no es excusa para perder los modales."),
            PJ("Vengo a apagar todo esto."),
            J("Ya sé. Apagaste mis servidores uno por uno, desconectaste a Néstor, te llevaste el casino de Martín."),
            J("Mewtwo y Hoopa también están acá. Vinieron en 2026 a detenerme, ¿te acordás? Ahora pelean para mí."),
            J("Vení. Mostrame lo que aprendiste. Quiero ver con qué me vas a enfrentar."),
        };

        // ---------------------------------------------------------- la pelea

        var m = new List<MomentoDeJulieta>();

        // ===== FASE 1: Julieta analiza. El jugador tiene todo a favor. =====

        m.Add(M("Inicio: Nestor y la ruleta en linea", F1, new[] { C(DisparadorDeJulieta.AlEmpezar) },
            S("PROTOCOLO NÉSTOR: EN LÍNEA.", AccionDeJulieta.EncenderNestor),
            N("{PJ}, estoy conectado. Yo le leo las jugadas, vos pegá."),
            S("RULETA DE MARTÍN: EN LÍNEA. JUEGA A FAVOR DE {PJ}.", AccionDeJulieta.EncenderRuleta, AFavorDelJugador),
            J("Néstor en tu oído y la ruleta de Martín en el bolsillo. Qué bien equipado venís.",
              AccionDeJulieta.PerfilDeIA, 0),
            J("Adelante. Yo solo voy a mirar.")));

        m.Add(M("F1: primera lectura de Nestor", F1, new[] { C(DisparadorDeJulieta.NestorLeyo, 1) },
            J("Ahí está. El Protocolo Néstor."),
            J("Ve la jugada antes de que pase, ¿no? Qué curioso. Así pienso yo."),
            N("No la escuches, {PJ}. Seguí.")));

        m.Add(M("F1: primer giro de la ruleta", F1, new[] { C(DisparadorDeJulieta.RuletaGiroEnLaFase, 1) },
            J("La ruleta de Martín. Te la dio cuando lo desconectaste, ¿no?"),
            J("Un generador de números al azar. Qué nombre lindo para algo que no existe.")));

        m.Add(M("F1: Julieta pierde el primero", F1, new[] { C(DisparadorDeJulieta.AlRivalLeQuedan, 5) },
            J("Golpe limpio. Lo anoto."),
            J("Siempre fuiste así en clase: rápido para romper cosas, lento para preguntarte por qué funcionan.")));

        m.Add(M("F1: Julieta termina de estudiar", F1, new[] { C(DisparadorDeJulieta.TurnosEnLaFase, 3) },
            J("Tres turnos. Ya sé cuánto tarda Néstor en recalcular y cada cuánto gira la ruleta."),
            J("¿Sabés qué es lo gracioso, {PJ}? Todo lo que te está ayudando salió de mi código.")));

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
            J("El Protocolo Néstor lo escribí yo. Néstor aprendió a leer una jugada porque yo le enseñé."),
            J("Ahora lee las tuyas. Para mí.", AccionDeJulieta.EncenderLecturaDeJulieta),
            S("RULETA DE MARTÍN: PREMIOS REASIGNADOS. JUEGA A FAVOR DE: JULIETA.",
              AccionDeJulieta.EncenderRuleta, AFavorDelRival),
            J("Y la ruleta... Martín la giraba. Yo la programé.", AccionDeJulieta.PerfilDeIA, 1),
            J("La casa siempre gana, {PJ}. Y la casa soy yo.", AccionDeJulieta.PasarAFase, (int)F2)));

        // ===== FASE 2: el sistema es de Julieta. =====

        m.Add(M("F2: primer giro dado vuelta", F2, new[] { C(DisparadorDeJulieta.RuletaGiroEnLaFase, 1) },
            J("¿Lo ves? Los mismos números, la misma rueda. Lo único que cambió es para quién juega."),
            J("Eso es el azar, {PJ}: una regla que todavía nadie te contó.")));

        m.Add(M("F2: 2026", F2, new[] { C(DisparadorDeJulieta.TurnosEnLaFase, 2) },
            J("¿Te acordás de la oficina de la directora? El día de la graduación."),
            PJ("Estaba ahí. Lo vi todo."),
            J("Ya sé que estabas ahí. Escondido, esperando que la red se cayera para meter un virus que llevabas años escribiendo."),
            PJ("...¿Cómo sabés eso?"),
            J("Porque lo encontré mucho antes de que vos pudieras usarlo.")));

        m.Add(M("F2: Julieta pierde otro", F2, new[] { C(DisparadorDeJulieta.AlRivalLeQuedan, 3) },
            J("Seguís pegando fuerte. Pero ya no estás peleando contra mis Pokémon."),
            J("Estás peleando contra las reglas. Y las reglas son mías.")));

        m.Add(M("F2: el jugador pierde uno", F2, new[] { C(DisparadorDeJulieta.JugadorPerdio, 1) },
            J("Uno menos. En 2026 la red colapsó en un segundo. Tu equipo va a tardar un poco más.")));

        // ----- La ruptura del vinculo: pasa a la fase 3 -----
        m.Add(M("F2 -> F3: Julieta entra al vinculo", F2, new[]
            {
                C(DisparadorDeJulieta.TurnosEnLaFase, 5),
                C(DisparadorDeJulieta.AlRivalLeQuedan, 2),
                C(DisparadorDeJulieta.RivalSalvado),
            },
            J("Te voy a mostrar para qué sirve de verdad el NeuraRush-LINK."),
            J("No es para enseñar. Enseñar es lento: la gente se equivoca, se olvida, se rebela."),
            J("El LINK no enseña, {PJ}. Escribe. Escribe lo que tenés que saber... y borra lo que no."),
            J("Y un Pokémon es un vínculo. Un vínculo también se puede escribir.", AccionDeJulieta.Destello),
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
            J("Mirate, {PJ}. En 2026 querías tirar abajo la red del colegio. Ahora la red te tira abajo a vos.")));

        m.Add(M("F3: el controlado cayo", F3, new[] { C(DisparadorDeJulieta.ControladoCayo) },
            J("Uno menos. No importa: tu equipo tiene más puertas."),
            S("NEURARUSH-LINK: ACCEDIENDO A {POKEMON}...", AccionDeJulieta.ControlarPokemon)));

        m.Add(M("F3: el ultimo de Julieta", F3, new[] { C(DisparadorDeJulieta.UltimoDelRival) },
            J("Me queda uno. Y mientras el sistema sea mío, no cae.")));

        // ----- El PJ rompe el control: fase final -----
        m.Add(M("F3 -> Ruptura: el PJ rompe el control", F3, new[]
            {
                C(DisparadorDeJulieta.TurnosEnLaFase, 5),
                C(DisparadorDeJulieta.RivalSalvado),
            },
            J("¿Todavía no lo entendiste? No eras un sujeto del experimento, {PJ}. Eras un autor."),
            J("Tu virus es lo que deja al LINK entrar en una cabeza sin pedir permiso. Lo encontré en el servidor del colegio y lo usé."),
            PJ("Por eso el chip de mi nuca no me hace nada..."),
            J("Un virus no infecta a quien lo escribió."),
            PJ("Entonces tu sistema también me escucha a mí."),
            PJ("{POKEMON}... soy yo. Este código es mío. ¡Desconectate!", AccionDeJulieta.Destello),
            S("FIRMA DE AUTOR RECONOCIDA. VÍNCULO LIBERADO.", AccionDeJulieta.LiberarPokemon),
            S("LECTURA DE LA ADMINISTRADORA: REVOCADA.", AccionDeJulieta.ApagarLecturaDeJulieta),
            S("PROTOCOLO NÉSTOR: EN LÍNEA.", AccionDeJulieta.EncenderNestor),
            N("¡{PJ}! ¡Volví! Lo que hiciste abrió el canal de nuevo.", cara: RetratoNestorFeliz),
            S("RULETA DE MARTÍN: JUEGA A FAVOR DE {PJ}.", AccionDeJulieta.EncenderRuleta, AFavorDelJugador),
            J("No... esto no estaba en el plan.", AccionDeJulieta.SenalDeControl, 0),
            PJ("Estoy bien. Sigo en pie.", AccionDeJulieta.MostrarVidaDelPJ, 0),
            PJ("Aprendí de la mejor, profe. Ahora terminemos.", AccionDeJulieta.PasarAFase, (int)F4)));

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

        g.finalDelJuego = new[]
        {
            S("NEURARUSH-LINK: ADMINISTRADORA DESCONECTADA."),
            S("MEWTWO: CONEXIÓN PERDIDA. HOOPA: CONEXIÓN PERDIDA."),
            J("..."),
            J("Se terminó."),
            PJ("¿Por qué, Julieta? ¿Por qué todo esto?"),
            J("Porque vi a mis alumnos equivocarse durante años. Olvidarse todo al otro día del examen. Quedarse atrás."),
            J("El LINK iba a arreglar eso. Nadie más se iba a quedar atrás."),
            PJ("Nadie se iba a quedar atrás porque nadie se iba a poder mover."),
            J("Cuando se abrió el portal, el mundo se rompió. Alguien tenía que escribir las reglas nuevas."),
            J("Si no era yo, no iba a ser nadie."),
            PJ("Había que dejar que la gente se equivoque. Eso también es aprender."),
            J("Lo dice el que quería voltear la red del colegio por venganza."),
            PJ("Sí. Y mirá: mi virus terminó sirviendo para algo."),
            J("..."),
            J("Aprobado, {PJ}."),
            S("SERVIDOR CENTRAL: APAGÁNDOSE. DESCONECTANDO TODOS LOS CHIPS..."),
            S("MEMORIAS RESTAURADAS."),
            N("{PJ}... lo lograste. Lo recuerdo todo. Todos lo están recordando.", cara: RetratoNestorFeliz),
            J("En 2026 Mewtwo dijo que el futuro todavía podía salvarse."),
            J("Parece que tenía razón."),
            J("Andá, {PJ}. Tixi te está esperando en la Quarcho Cueva. Y esta vez nadie va a olvidar lo que pasó."),
            PJ("Nos vemos, profe."),
        };

        g.yaVencida = new[]
        {
            J("Ya está, {PJ}. Andá con Tixi."),
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
