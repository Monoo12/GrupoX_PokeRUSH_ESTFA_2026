using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arma de un click la escena gray box para probar las fases 2 y 3: movilidad,
// camara, y habilidades apuntadas.
//
// Es a proposito una caja gris sin arte: lo que se evalua es si moverse y pegar
// se sienten bien. Cualquier decorado a esta altura distrae y hace mas lento
// iterar.
//
// La escena se crea ANTES de cargar los assets. Al reves, NewScene descarga lo
// que ya estaba cargado y las referencias quedan serializadas en null: es el
// mismo bug que hubo que corregir en BattleTestSceneBuilder.
public static class RunTestSceneBuilder
{
    private const string RutaEscena = "Assets/Scenes/RutaPrueba.unity";
    private const string CarpetaPrefabs = "Assets/_AuditRank/_Prefabs";
    private const string CarpetaModelos = "Assets/_AuditRank";

    // Cinderace: set de clips completo, materiales limpios y proporciones de
    // biped, que es lo mas facil de leer al juzgar el movimiento.
    private const string EspecieJugador = "Cinderace";
    // Zeraora tiene su propio set completo y contrasta bien de color.
    private const string EspecieRival = "Zeraora";

    private const int NivelJugador = 30;
    private const int NivelRival = 25;

    [MenuItem("PokeRush/3D/Armar ruta de prueba")]
    public static void Armar()
    {
        // NewScene no se puede usar en Play, y el error que tira no dice que sea
        // por eso.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[Ruta] Sali del modo Play antes de rearmar la escena.");
            return;
        }

        UnityEngine.SceneManagement.Scene escena = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene, NewSceneMode.Single);

        ArmarIluminacion();
        ArmarTerreno();

        GameObject jugador = ArmarJugador();
        Camera camara = ArmarCamara(jugador != null ? jugador.transform : null);

        if (jugador != null && camara != null)
        {
            Asignar(jugador.GetComponent<RunPlayerController>(), "camaraReferencia", camara.transform);

            var habilidadesJugador = jugador.GetComponent<RunPlayerAbilities>();
            Asignar(habilidadesJugador, "camaraRig", camara.GetComponent<RunCameraRig>());
            Asignar(habilidadesJugador, "indicador", ArmarIndicador());

            ArmarHud(jugador);
        }

        ArmarRivales();

        EditorSceneManager.SaveScene(escena, RutaEscena);
        EditorSceneManager.OpenScene(RutaEscena);
        Debug.Log("[Ruta] Escena armada. WASD para moverte, Shift para correr, " +
                  "click izquierdo para el basico, 1 y 2 mantenidas para apuntar habilidades.");
    }

    // --- Escenario ----------------------------------------------------------

    private static void ArmarIluminacion()
    {
        var luz = new GameObject("Luz Direccional");
        Light componente = luz.AddComponent<Light>();
        componente.type = LightType.Directional;
        componente.intensity = 1.1f;
        componente.shadows = LightShadows.Soft;
        luz.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.46f, 0.5f);
    }

    // Suelo amplio mas unos bloques: sin referencias verticales no se percibe la
    // velocidad ni se puede juzgar si el giro responde.
    private static void ArmarTerreno()
    {
        Material gris = MaterialPlano(new Color(0.24f, 0.25f, 0.27f));
        Material grisClaro = MaterialPlano(new Color(0.34f, 0.35f, 0.38f));

        GameObject suelo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        suelo.name = "Suelo";
        suelo.transform.localScale = new Vector3(6f, 1f, 6f);
        suelo.GetComponent<Renderer>().sharedMaterial = gris;

        var contenedor = new GameObject("Obstaculos");
        var posiciones = new[]
        {
            new Vector3(8f, 0f, 6f), new Vector3(-7f, 0f, 10f), new Vector3(12f, 0f, -8f),
            new Vector3(-12f, 0f, -5f), new Vector3(3f, 0f, 16f), new Vector3(-16f, 0f, 4f),
            new Vector3(18f, 0f, 12f), new Vector3(-4f, 0f, -14f),
        };

        for (int i = 0; i < posiciones.Length; i++)
        {
            float alto = 1.5f + (i % 3);
            GameObject bloque = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bloque.name = $"Bloque_{i:00}";
            bloque.transform.SetParent(contenedor.transform);
            bloque.transform.localScale = new Vector3(2f, alto, 2f);
            bloque.transform.position = posiciones[i] + Vector3.up * (alto * 0.5f);
            bloque.GetComponent<Renderer>().sharedMaterial = grisClaro;
        }
    }

    private static Material MaterialPlano(Color color)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0f);
        return material;
    }

    // --- Combatientes -------------------------------------------------------

    // El CharacterController va en una raiz a escala 1, con el modelo adentro:
    // el prefab de la especie viene a escala 55 y un collider escalado 55 veces
    // seria del tamaño de la ruta entera.
    private static GameObject ArmarJugador()
    {
        GameObject raiz = ArmarCuerpo("Jugador", EspecieJugador, Vector3.zero);
        if (raiz == null) return null;

        raiz.AddComponent<RunPlayerController>();

        var locomocion = raiz.AddComponent<RunLocomotionAnimator>();
        CablearLocomocion(locomocion, EspecieJugador);

        var combatiente = raiz.AddComponent<RunCombatant>();
        combatiente.Configurar(BuscarEspecie(EspecieJugador), NivelJugador, true);

        var chispazo = raiz.AddComponent<HitBurst>();

        var ejecutor = raiz.AddComponent<AbilityRunner>();
        Asignar(ejecutor, "animador", locomocion);
        Asignar(ejecutor, "efectos", chispazo);
        Asignar(ejecutor, "habilidades", GenerarHabilidades(EspecieJugador, false));

        var cadena = raiz.AddComponent<BasicAttackChain>();
        Asignar(cadena, "golpes", GenerarHabilidades(EspecieJugador, true));

        raiz.AddComponent<RunPlayerAbilities>();
        return raiz;
    }

    private static void ArmarRivales()
    {
        var posiciones = new[] { new Vector3(6f, 0f, 8f), new Vector3(-5f, 0f, 9f), new Vector3(11f, 0f, 2f) };
        PokenDefinition especie = BuscarEspecie(EspecieRival);

        for (int i = 0; i < posiciones.Length; i++)
        {
            GameObject rival = ArmarCuerpo($"Rival_{i:00}", EspecieRival, posiciones[i]);
            if (rival == null) return;

            var locomocion = rival.AddComponent<RunLocomotionAnimator>();
            CablearLocomocion(locomocion, EspecieRival);

            var combatiente = rival.AddComponent<RunCombatant>();
            combatiente.Configurar(especie, NivelRival, false);

            // Sin IA todavia: en esta fase son muñecos para medir si pegarles se
            // siente contundente. La IA es la fase 4.
            rival.AddComponent<RunDummyFeedback>();
        }
    }

    private static GameObject ArmarCuerpo(string nombre, string especie, Vector3 posicion)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaPrefabs}/{especie}.prefab");
        if (prefab == null)
        {
            Debug.LogError($"[Ruta] Falta el prefab de '{especie}'. " +
                           "Corre antes PokeRush > 3D > Generar prefabs de UNITE.");
            return null;
        }

        var raiz = new GameObject(nombre);
        var modelo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        modelo.transform.SetParent(raiz.transform, false);

        float alto = AltoDe(modelo);

        CharacterController controlador = raiz.AddComponent<CharacterController>();
        controlador.height = alto;
        controlador.radius = Mathf.Clamp(alto * 0.25f, 0.2f, 0.6f);
        controlador.center = new Vector3(0f, alto * 0.5f, 0f);
        controlador.slopeLimit = 50f;
        controlador.stepOffset = Mathf.Min(0.3f, alto * 0.25f);

        raiz.transform.position = posicion;
        return raiz;
    }

    private static float AltoDe(GameObject modelo)
    {
        var renderers = modelo.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 1.5f;

        Bounds limites = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) limites.Encapsulate(renderers[i].bounds);

        return Mathf.Max(0.5f, limites.size.y);
    }

    private static PokenDefinition BuscarEspecie(string nombre)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{nombre} t:PokenDefinition"))
        {
            var definicion = AssetDatabase.LoadAssetAtPath<PokenDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definicion != null) return definicion;
        }
        Debug.LogWarning($"[Ruta] No hay PokenDefinition para '{nombre}'.");
        return null;
    }

    // --- Habilidades --------------------------------------------------------

    // Genera un AbilityDefinition por clip y lo deja como asset, para poder
    // ajustar rangos y ventanas desde el inspector sin tocar codigo. Si ya
    // existe se reusa: rearmar la escena no pisa lo que hayas afinado.
    private static AbilityDefinition[] GenerarHabilidades(string especie, bool basicos)
    {
        string[] fbx = Directory.GetFiles(Application.dataPath + "/_AuditRank/" + especie, "*.fbx");
        if (fbx.Length == 0) return new AbilityDefinition[0];

        string ruta = $"{CarpetaModelos}/{especie}/{Path.GetFileName(fbx[0])}";
        List<AnimationClip> propios = UniteClips.PropiosDe(ruta);

        string carpeta = $"{CarpetaModelos}/{especie}/Habilidades";
        if (!AssetDatabase.IsValidFolder(carpeta))
            AssetDatabase.CreateFolder($"{CarpetaModelos}/{especie}", "Habilidades");

        string[] prefijos = basicos
            ? new[] { "ani_atk1_", "ani_atk2_", "ani_atk3_" }
            : new[] { "ani_spell1_", "ani_spell2_" };

        var resultado = new List<AbilityDefinition>();
        for (int i = 0; i < prefijos.Length; i++)
        {
            AnimationClip clip = UniteClips.Buscar(propios, prefijos[i]);
            if (clip == null) continue;

            string nombreAsset = $"{especie}_{(basicos ? "basico" : "habilidad")}{i + 1}";
            string rutaAsset = $"{carpeta}/{nombreAsset}.asset";

            var habilidad = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(rutaAsset);
            if (habilidad == null)
            {
                habilidad = ScriptableObject.CreateInstance<AbilityDefinition>();
                ConfigurarPorDefecto(habilidad, nombreAsset, clip, basicos, i, prefijos.Length);
                AssetDatabase.CreateAsset(habilidad, rutaAsset);
            }
            else if (habilidad.clip == null)
            {
                habilidad.clip = clip;
                EditorUtility.SetDirty(habilidad);
            }
            resultado.Add(habilidad);
        }

        AssetDatabase.SaveAssets();
        return resultado.ToArray();
    }

    // Valores de arranque, pensados para que se sienta algo desde el primer Play.
    // Las ventanas salen como fraccion del clip: el pack no trae marcadores de
    // impacto, asi que hay que medirlos a ojo y estos son el punto de partida.
    private static void ConfigurarPorDefecto(
        AbilityDefinition habilidad, string nombre, AnimationClip clip, bool basico, int indice, int total)
    {
        habilidad.nombre = nombre;
        habilidad.clip = clip;
        habilidad.ventanaInicio = clip.length * 0.35f;
        habilidad.ventanaFin = clip.length * 0.55f;
        habilidad.bloqueo = clip.length * 0.7f;

        bool esRemate = basico && indice == total - 1;

        // Los tres roles tienen que sentirse distintos, si no da lo mismo cual
        // apretar. El basico es corto y rapido, la 1 es larga y fina y atraviesa
        // a varios, y la 2 es un zonazo lento que ademas frena.
        if (basico)
        {
            // Cuerpo a cuerpo justo delante: no lleva indicador y se encadena
            // rapido. Es el golpe de relleno mientras esperas los enfriamientos.
            habilidad.apuntado = AimMode.AreaEnSuelo;
            habilidad.rango = 1.6f;
            habilidad.radio = 1.2f;
            habilidad.enfriamiento = 0f;
            habilidad.ventanaInicio = clip.length * 0.25f;
            habilidad.ventanaFin = clip.length * 0.45f;
            habilidad.bloqueo = clip.length * 0.5f;
            habilidad.potencia = esRemate ? 60 : 28;
            habilidad.tipo = PokenType.Normal;
            habilidad.categoria = AttackType.Physical;
            // El remate empuja: cierra la cadena separando al rival.
            if (esRemate)
                habilidad.control = new ControlEffect { tipo = ControlKind.Empujar, duracion = 0.1f, fuerza = 2.2f };
        }
        else if (indice == 0)
        {
            // EMBESTIDA. El clip es una patada, no un conjuro: el alcance sale de
            // que el Poken se desplaza, no de que dispare algo. Por eso Dash y no
            // SkillshotLineal, y por eso es fisico.
            habilidad.apuntado = AimMode.Dash;
            habilidad.rango = 4.5f;
            habilidad.radio = 1.1f;
            habilidad.enfriamiento = 5f;
            habilidad.ventanaInicio = clip.length * 0.25f;
            habilidad.ventanaFin = clip.length * 0.55f;
            habilidad.bloqueo = clip.length * 0.7f;
            habilidad.potencia = 75;
            habilidad.tipo = PokenType.Fire;
            habilidad.categoria = AttackType.Physical;
            habilidad.control = new ControlEffect { tipo = ControlKind.Empujar, duracion = 0.1f, fuerza = 1.2f };
        }
        else
        {
            // PATADA GIRATORIA. Golpea alrededor del Poken, no a distancia: hay
            // que meterse. Pega fuerte y ralentiza, pero la anticipacion es larga
            // y se ve venir, asi que es la que se prepara y no la de reflejo.
            habilidad.apuntado = AimMode.AreaEnSuelo;
            habilidad.rango = 3f;
            habilidad.radio = 2.4f;
            habilidad.enfriamiento = 9f;
            habilidad.ventanaInicio = clip.length * 0.5f;
            habilidad.ventanaFin = clip.length * 0.7f;
            habilidad.bloqueo = clip.length * 0.85f;
            habilidad.potencia = 95;
            habilidad.tipo = PokenType.Fire;
            habilidad.categoria = AttackType.Physical;
            habilidad.control = new ControlEffect { tipo = ControlKind.Ralentizar, duracion = 2f, fuerza = 0.4f };
        }
    }

    // Los clips son sub-assets del FBX. Se piden por UniteClips y no leyendo el
    // FBX directo, porque varios traen animaciones de otro personaje adentro y
    // hay que descartarlas: Miraidon tiene 41 clips de Wigglytuff y de Mewtwo.
    private static void CablearLocomocion(RunLocomotionAnimator locomocion, string especie)
    {
        string[] fbx = Directory.GetFiles(Application.dataPath + "/_AuditRank/" + especie, "*.fbx");
        if (fbx.Length == 0) return;

        string ruta = $"{CarpetaModelos}/{especie}/{Path.GetFileName(fbx[0])}";
        List<AnimationClip> propios = UniteClips.PropiosDe(ruta);

        Asignar(locomocion, "espera", UniteClips.Buscar(propios, "ani_idle_"));
        Asignar(locomocion, "carrera", UniteClips.Buscar(propios, "ani_run_"));
    }

    // --- Camara, indicador y HUD -------------------------------------------

    private static Camera ArmarCamara(Transform objetivo)
    {
        var go = new GameObject("Camara");
        Camera camara = go.AddComponent<Camera>();
        camara.tag = "MainCamera";
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = new Color(0.13f, 0.14f, 0.17f);
        camara.fieldOfView = 55f;
        camara.farClipPlane = 500f;

        var extra = go.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (extra == null) extra = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        extra.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        extra.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;

        var rig = go.AddComponent<RunCameraRig>();
        if (objetivo != null) rig.Seguir(objetivo);

        return camara;
    }

    // Los decals de apuntado y los sprites del HUD son los del propio UNITE,
    // copiados a _UI. Ver UniteAssetPostprocessor: todo lo que cae ahi se
    // importa como Sprite.
    private const string CarpetaUI = "Assets/_AuditRank/_UI";

    private static AbilityIndicator ArmarIndicador()
    {
        var go = new GameObject("IndicadorApuntado", typeof(MeshFilter), typeof(MeshRenderer));
        var indicador = go.AddComponent<AbilityIndicator>();

        // Las versiones _alfa las genera PokeRush > 3D > Preparar decals de
        // apuntado: los originales traen el alfa en 1 y se veria el cuadrado.
        Asignar(indicador, "decalLinea", Textura("t_fx_Indicator_rectangle_02_cj_alfa"));
        // _Circle_03_wyj y no _02_sam: este trae mascara de alfa de verdad
        // (0,04 en las esquinas contra 0,25), asi el circulo no arrastra el
        // cuadrado del quad.
        Asignar(indicador, "decalArea", Textura("t_fx_Indicator_Decal_Circle_03_wyj_alfa"));
        Asignar(indicador, "decalAlcance", Textura("t_fx_0656_indicator_Ring_01_sam_alfa"));
        return indicador;
    }

    private static void ArmarHud(GameObject jugador)
    {
        var go = new GameObject("HUD");
        var hud = go.AddComponent<RunHudView>();

        Asignar(hud, "jugador", jugador.GetComponent<RunCombatant>());
        Asignar(hud, "ejecutor", jugador.GetComponent<AbilityRunner>());
        Asignar(hud, "basico", jugador.GetComponent<BasicAttackChain>());

        // La barra de vida real del juego: t_Ingame_HP_bg2 es el marco oscuro y
        // t_Ingame_HP_Me el relleno verde. La de boss rush que se usaba antes es
        // la del jefe, mucho mas ancha y plana.
        Asignar(hud, "fondoBarra", Sprite("t_Ingame_HP_bg2"));
        Asignar(hud, "rellenoBarra", Sprite("t_Ingame_HP_Me"));
        Asignar(hud, "marcoHabilidad", Sprite("t_Pokemon_SkillBtn"));
        Asignar(hud, "fondoPanel", Sprite("t_battle_hud_normalAtk_bg"));
    }

    private static Texture2D Textura(string nombre)
        => AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaUI}/{nombre}.png");

    private static Sprite Sprite(string nombre)
        => AssetDatabase.LoadAssetAtPath<Sprite>($"{CarpetaUI}/{nombre}.png");

    // --- Utilidades ---------------------------------------------------------

    private static void Asignar(Object destino, string campo, Object valor)
    {
        if (destino == null) return;

        var so = new SerializedObject(destino);
        SerializedProperty propiedad = so.FindProperty(campo);
        if (propiedad != null) propiedad.objectReferenceValue = valor;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Asignar<T>(Object destino, string campo, T[] valores) where T : Object
    {
        if (destino == null || valores == null) return;

        var so = new SerializedObject(destino);
        SerializedProperty propiedad = so.FindProperty(campo);
        if (propiedad == null) return;

        propiedad.arraySize = valores.Length;
        for (int i = 0; i < valores.Length; i++)
            propiedad.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
