using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Deja el proyecto listo para la build final y la genera.
//
//   PokeRush > Release > Aplicar configuracion de release
//     Player Settings (nombre, empresa, version, icono, splash, pantalla,
//     logs), escenas de Build Settings en el orden del recorrido y build de
//     produccion (sin Development Build ni depuracion).
//
//   PokeRush > Release > Generar build de Windows
//     Lo anterior + la build en Builds/PokeRush_<version>/PokeRush.exe
//     (carpeta ignorada por git).
//
// Desde la linea de comandos (ver BUILD.md):
//   Unity.exe -batchmode -quit -projectPath Codigo/PokeRush
//             -executeMethod PreparacionDeRelease.BuildWindows -logFile build.log
public static class PreparacionDeRelease
{
    public const string Empresa = "LevelUP Creators";
    public const string Producto = "PokeRush";
    public const string Version = "1.0.0";

    private const string Icono = "Assets/Sprites/Logos/IconoDelJuego.png";
    private const string Logo = "Assets/Sprites/Logos/LevelUpCreators.png";

    // El recorrido de la historia, en orden. El indice 0 es el que abre el juego.
    // BossFinal entra cuando exista (la arena de Santiago): ver Jefe_Final_Julieta.md.
    public static readonly string[] Escenas =
    {
        "Assets/Scenes/Menu Principal.unity",
        "Assets/Scenes/MenuSaves.unity",
        "Assets/Scenes/CreacionPersonaje.unity",
        "Assets/Scenes/habitacion.unity",
        "Assets/Scenes/Quarcho-Cueva.unity",
        "Assets/Scenes/NestorGOD2.unity",
        "Assets/Scenes/Casino Nivel 1.unity",
        "Assets/Scenes/Casino Nivel 2.unity",
        "Assets/Scenes/Casino nivel 3.unity",
        "Assets/Scenes/Casino piso FINAL.unity",
        "Assets/Scenes/BossFinal.unity",
        "Assets/Scenes/CreditosFinales.unity",
    };

    [MenuItem("PokeRush/Release/Aplicar configuracion de release")]
    public static void AplicarConfiguracion()
    {
        // Identidad. Ojo: Empresa y Producto definen la carpeta de la partida
        // (AppData/LocalLow/<Empresa>/<Producto>/pokerush.db).
        PlayerSettings.companyName = Empresa;
        PlayerSettings.productName = Producto;
        PlayerSettings.bundleVersion = Version;

        PrepararImagenes();
        var icono = AssetDatabase.LoadAssetAtPath<Texture2D>(Icono);
        if (icono != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icono }, IconKind.Any);
        else Debug.LogWarning("[Release] Falta el icono " + Icono);

        // Splash: el logo del grupo sobre blanco, sin el de Unity.
        var logo = AssetDatabase.LoadAssetAtPath<Sprite>(Logo);
        PlayerSettings.SplashScreen.show = logo != null;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.backgroundColor = Color.white;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
        PlayerSettings.SplashScreen.overlayOpacity = 0f;
        if (logo != null) PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.5f, logo) };

        // Pantalla: completa a la resolucion del monitor; en Opciones se puede
        // pasar a ventana y elegir otra resolucion.
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.defaultIsNativeResolution = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.allowFullscreenSwitch = true;
        PlayerSettings.runInBackground = false;
        // Dos copias abiertas escribirian la misma pokerush.db a la vez.
        PlayerSettings.forceSingleInstance = true;
        PlayerSettings.usePlayerLog = true;

        // Codigo: Mono (es el modulo instalado; IL2CPP pide otro modulo y las
        // herramientas de C++, y sqlite-net usa reflexion) y sin recortar codigo.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);

        // Logs de produccion: sin la pila en los Debug.Log (son caros y llenan
        // el Player.log), con la pila del script en errores y excepciones.
        PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        PlayerSettings.SetStackTraceLogType(LogType.Error, StackTraceLogType.ScriptOnly);
        PlayerSettings.SetStackTraceLogType(LogType.Assert, StackTraceLogType.ScriptOnly);
        PlayerSettings.SetStackTraceLogType(LogType.Exception, StackTraceLogType.ScriptOnly);

        // Build de produccion.
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.allowDebugging = false;
        EditorUserBuildSettings.connectProfiler = false;
        EditorUserBuildSettings.buildWithDeepProfilingSupport = false;

        SoltarPixelesDeLasAnimaciones();
        ArmarIndiceDeDefiniciones();
        PonerEscenas();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Release] Configuracion aplicada: {Empresa} / {Producto} {Version}, " +
                  $"{EditorBuildSettings.scenes.Count(s => s.enabled)} escenas.");
    }

    // Las del recorrido que existen, en orden. Las de prueba no van.
    public static void PonerEscenas()
    {
        var escenas = new List<EditorBuildSettingsScene>();
        foreach (string ruta in Escenas)
        {
            if (!File.Exists(ruta))
            {
                Debug.LogWarning($"[Release] {ruta} no existe todavia: queda fuera de la build.");
                continue;
            }
            escenas.Add(new EditorBuildSettingsScene(ruta, true));
        }
        EditorBuildSettings.scenes = escenas.ToArray();
    }

    // El icono, sin compresion y sin mipmaps; el logo, como un solo sprite suave.
    private static void PrepararImagenes()
    {
        if (AssetImporter.GetAtPath(Icono) is TextureImporter icono)
        {
            icono.textureType = TextureImporterType.Default;
            icono.alphaIsTransparency = true;
            icono.mipmapEnabled = false;
            icono.npotScale = TextureImporterNPOTScale.None;
            icono.textureCompression = TextureImporterCompression.Uncompressed;
            icono.SaveAndReimport();
        }
        if (AssetImporter.GetAtPath(Logo) is TextureImporter logo)
        {
            logo.textureType = TextureImporterType.Sprite;
            logo.spriteImportMode = SpriteImportMode.Single;
            logo.filterMode = FilterMode.Bilinear;
            logo.mipmapEnabled = false;
            logo.alphaIsTransparency = true;
            logo.textureCompression = TextureImporterCompression.CompressedHQ;
            logo.SaveAndReimport();
        }
    }

    // Donde esta cada especie y movimiento, para que DefinitionRepository los
    // cargue de a uno (ver IndiceDeDefiniciones). Se rearma solo antes de cada
    // build (AntesDeLaBuild, abajo).
    [MenuItem("PokeRush/Release/Armar indice de definiciones")]
    public static void ArmarIndiceDeDefiniciones()
    {
        const string ruta = "Assets/Resources/" + IndiceDeDefiniciones.RutaEnResources + ".asset";
        var indice = AssetDatabase.LoadAssetAtPath<IndiceDeDefiniciones>(ruta);
        if (indice == null)
        {
            indice = ScriptableObject.CreateInstance<IndiceDeDefiniciones>();
            AssetDatabase.CreateAsset(indice, ruta);
        }
        indice.pokens = Entradas<PokenDefinition>("Assets/Resources/Definitions/Pokens", p => p.pokenName);
        indice.movimientos = Entradas<MoveDefinition>("Assets/Resources/Definitions/Moves", m => m.moveName);
        EditorUtility.SetDirty(indice);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Release] Indice de definiciones: {indice.pokens.Count} especies, {indice.movimientos.Count} movimientos.");
    }

    private static List<IndiceDeDefiniciones.Entrada> Entradas<T>(string carpeta, System.Func<T, string> nombre) where T : Object
    {
        var lista = new List<IndiceDeDefiniciones.Entrada>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { carpeta }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null || string.IsNullOrEmpty(nombre(asset))) continue;
            string enResources = path.Substring("Assets/Resources/".Length);
            enResources = enResources.Substring(0, enResources.Length - Path.GetExtension(enResources).Length);
            lista.Add(new IndiceDeDefiniciones.Entrada { nombre = nombre(asset), ruta = enResources });
        }
        return lista;
    }

    // Las hojas de animacion de ataques quedaban legibles (isReadable) porque
    // el importador mide sus pixeles. En el juego nadie los lee, y una textura
    // legible guarda una segunda copia en RAM: eran ~400 MB de mas, cargados
    // al arrancar (los movimientos estan en Resources y arrastran sus hojas).
    [MenuItem("PokeRush/Release/Optimizar texturas de animaciones")]
    public static void SoltarPixelesDeLasAnimaciones()
    {
        int cambiadas = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Graphics/Animations" }))
            {
                if (!(AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is TextureImporter imp) || !imp.isReadable)
                    continue;
                imp.isReadable = false;
                imp.SaveAndReimport();
                cambiadas++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        if (cambiadas > 0) Debug.Log($"[Release] {cambiadas} hojas de animacion ya no son legibles (menos RAM).");
    }

    // Se programa para el ciclo siguiente del editor: Unity no deja generar una
    // build "adentro del player loop", que es donde corre un menu disparado por
    // otra herramienta (el MCP, por ejemplo).
    [MenuItem("PokeRush/Release/Generar build de Windows")]
    public static void GenerarBuild() => EditorApplication.delayCall += BuildWindows;

    // Tambien para -executeMethod: en batchmode sale con codigo 1 si falla.
    public static void BuildWindows()
    {
        AplicarConfiguracion();

        string raiz = Directory.GetParent(Application.dataPath).FullName;
        string carpeta = Path.Combine(raiz, "Builds", $"{Producto}_{Version}");
        var opciones = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(carpeta, Producto + ".exe"),
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            // LZ4HC: los datos del juego van comprimidos (sin perdida). Las tiras
            // de los Pokemon son PNG sin comprimir en la textura (comprimirlas con
            // BC7 cambia el pixel art), asi que sin esto la build pesaba 3,6 GB.
            options = BuildOptions.CompressWithLz4HC
        };

        BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
        BuildSummary r = reporte.summary;
        string resumen = $"[Release] Build {r.result}: {r.totalErrors} errores, {r.totalWarnings} avisos, " +
                         $"{r.totalSize / (1024f * 1024f):0.0} MB, {r.totalTime.TotalSeconds:0} s -> {opciones.locationPathName}";
        if (r.result == BuildResult.Succeeded)
        {
            // Unity deja la info de depuracion de Burst al lado del .exe: no se entrega.
            string burst = Path.Combine(carpeta, Producto + "_BurstDebugInformation_DoNotShip");
            if (Directory.Exists(burst)) Directory.Delete(burst, true);
            Debug.Log(resumen);
        }
        else Debug.LogError(resumen);

        if (Application.isBatchMode) EditorApplication.Exit(r.result == BuildResult.Succeeded ? 0 : 1);
    }
}

// Antes de cualquier build (la del menu o una hecha a mano): el indice de
// definiciones al dia, o una especie nueva caeria a la carga completa.
public class AntesDeLaBuild : UnityEditor.Build.IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => PreparacionDeRelease.ArmarIndiceDeDefiniciones();
}
