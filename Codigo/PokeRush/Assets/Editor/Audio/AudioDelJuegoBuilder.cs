using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Arma el audio del juego a partir de las carpetas de Assets/Audio.
//
//   PokeRush > Audio > Armar todo el audio
//     1. Cortar los pasos: parte las grabaciones de footstep en pasos sueltos
//        (Assets/Audio/Efectos/footstep/Pasos/<superficie>_N.wav).
//     2. Medir y armar el catalogo: mide el volumen real de cada archivo (para
//        que todo suene parejo) y llena Resources/Audio/CatalogoDeAudio.asset
//        con que suena en cada escena, pelea y momento.
//     3. Configurar la importacion: musica en Streaming, efectos cortos
//        descomprimidos, gritos y ataques sin precargar.
//
// Se puede volver a correr: respeta los 'volumen' retocados a mano en el
// catalogo y no vuelve a medir lo que ya midio. Que va en cada lugar esta en
// las tablas de abajo; ver Documentacion/Documentacion del Codigo/Audio_Del_Juego.md.
public static class AudioDelJuegoBuilder
{
    public const string Raiz = "Assets/Audio";
    public const string RutaDelCatalogo = "Assets/Resources/Audio/CatalogoDeAudio.asset";
    private const string CarpetaDePasos = "Assets/Audio/Efectos/footstep/Pasos";
    private const string Gritos = "Assets/Audio/Combate/Gritos";
    private const string Ataques = "Assets/Audio/Combate/Ataques";
    private const string Pokens = "Assets/Resources/Definitions/Pokens";
    private const string Movimientos = "Assets/Resources/Definitions/Moves";

    // ---------------------------------------------------------------- tablas

    // Escena -> carpeta (o archivo) de su musica, y superficie de los pasos.
    private static readonly (string escena, string musica, string superficie)[] Escenas =
    {
        ("Menu Principal", "Musica_MenuPrincipalySave.mp3", null),
        ("MenuSaves", "Musica_MenuPrincipalySave.mp3", null),
        ("CreacionPersonaje", "Creacion _PJ.mp3", null),
        // La habitacion no tiene tema propio: sigue el de la creacion, sin cortar.
        ("habitacion", "Creacion _PJ.mp3", "laminado"),
        ("HabitacionTutorial_Esqueleto", "Creacion _PJ.mp3", "laminado"),
        ("Quarcho-Cueva", "Quarchocueva", "laminado"),
        ("NestorGOD2", "BackMusic_Torre1.mp3", "metal"),
        ("Casino Nivel 1", "Casino Piso 1", "alfombra"),
        ("Casino Nivel 2", "Casino piso 2", "alfombra"),
        ("Casino nivel 3", "Casino piso 3", "alfombra"),
        ("Casino piso FINAL", "Casino Piso Final", "alfombra"),
        // La arena de Julieta: el recorrido la espera como BossFinal; mientras
        // tanto, la vacia de Santiago y la de prueba suenan igual.
        ("BossFinal", "Sala BossFinal_Julieta", "metal"),
        ("ArenaJuli_Vacia", "Sala BossFinal_Julieta", "metal"),
        ("BossJulieta_Prueba", "Sala BossFinal_Julieta", "metal"),
    };

    // Rivales con tema propio.
    private static readonly (string rival, string musica)[] Rivales =
    {
        ("Assets/Resources/Definitions/Torre1/Nestor.asset", "Pelea_Nestor.mp3"),
        ("Assets/Resources/Definitions/Casino/JefeFinalCasino.asset", "Pelea contra martin"),
    };

    // Grabacion -> nombre de la superficie de sus pasos.
    private static readonly (string archivo, string superficie)[] GrabacionesDePasos =
    {
        ("Efectos/footstep/footsteps-walking-on-laminated-floor-flat-shoes-vadi-sound-1-1-00-14.mp3", "laminado"),
        ("Efectos/footstep/freesound_community-footsteps-on-carpet-106597.mp3", "alfombra"),
        ("Efectos/footstep/freesound_community-footsteps-on-metal-floor-45788.mp3", "metal"),
        ("Efectos/footstep/Ejimas1.ogg", "ejimas"),
    };

    // Sonido de ataque de cada tipo, para los movimientos sin sonido propio. Si
    // el primero no tiene sonido, el siguiente de la lista (separados por |).
    private static readonly (PokenType tipo, string movimiento)[] AtaquePorTipo =
    {
        (PokenType.Normal, "tackle"), (PokenType.Fire, "ember"), (PokenType.Water, "water-gun"),
        (PokenType.Grass, "vine-whip"), (PokenType.Electric, "thunder-shock"), (PokenType.Ice, "ice-shard"),
        (PokenType.Fighting, "karate-chop|brick-break|low-kick|cross-chop|close-combat"),
        (PokenType.Poison, "poison-sting"), (PokenType.Ground, "mud-slap"),
        (PokenType.Flying, "gust"), (PokenType.Psychic, "confusion"), (PokenType.Bug, "bug-bite"),
        (PokenType.Rock, "rock-throw"), (PokenType.Ghost, "lick"), (PokenType.Dragon, "dragon-breath"),
        (PokenType.Dark, "bite"), (PokenType.Steel, "metal-claw"), (PokenType.Fairy, "fairy-wind"),
    };

    // ---------------------------------------------------------------- menus

    [MenuItem("PokeRush/Audio/Armar todo el audio")]
    public static void ArmarTodo()
    {
        CortarPasos();
        ArmarCatalogo();
        ConfigurarImportacion();
        Debug.Log("[Audio] TERMINADO. Catalogo en " + RutaDelCatalogo + ".");
    }

    // ---------------------------------------------------------------- importacion

    public enum Clase { Musica, Efecto, SinPrecargar }

    public static Clase ClaseDe(string ruta)
    {
        if (ruta.StartsWith(Gritos + "/") || ruta.StartsWith(Ataques + "/")) return Clase.SinPrecargar;
        if (ruta.StartsWith(Raiz + "/Efectos/")) return Clase.Efecto;
        return Clase.Musica;
    }

    // Lo que tiene que tener un clip segun su clase. Devuelve true si cambio algo.
    public static bool Aplicar(AudioImporter imp, string ruta)
    {
        Clase clase = ClaseDe(ruta);
        AudioImporterSampleSettings s = imp.defaultSampleSettings;
        AudioImporterSampleSettings antes = s;
        bool mono = imp.forceToMono, fondo = imp.loadInBackground;

        s.compressionFormat = AudioCompressionFormat.Vorbis;
        switch (clase)
        {
            case Clase.Musica:
                // Streaming: una cancion de seis minutos descomprimida son 60 MB de RAM.
                s.loadType = AudioClipLoadType.Streaming;
                s.quality = 0.6f;
                imp.forceToMono = false;
                imp.loadInBackground = true;
                break;
            case Clase.Efecto:
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.quality = 0.7f;
                s.preloadAudioData = true;
                imp.forceToMono = true;
                imp.loadInBackground = false;
                break;
            case Clase.SinPrecargar:
                // Son cientos: se cargan los de cada pelea al empezarla (Sonidos.PrecargarPelea).
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.quality = 0.5f;
                s.preloadAudioData = false;
                imp.forceToMono = true;
                imp.loadInBackground = true;
                break;
        }

        bool cambio = s.loadType != antes.loadType || !Mathf.Approximately(s.quality, antes.quality) ||
                      s.compressionFormat != antes.compressionFormat || s.preloadAudioData != antes.preloadAudioData ||
                      mono != imp.forceToMono || fondo != imp.loadInBackground;
        imp.defaultSampleSettings = s;
        return cambio;
    }

    [MenuItem("PokeRush/Audio/Configurar la importacion")]
    public static void ConfigurarImportacion()
    {
        int cambiados = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Raiz }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(ruta) is AudioImporter imp && Aplicar(imp, ruta))
                {
                    imp.SaveAndReimport();
                    cambiados++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        Debug.Log($"[Audio] Importacion: {cambiados} clips reconfigurados.");
    }

    // ---------------------------------------------------------------- pasos

    [MenuItem("PokeRush/Audio/Cortar los pasos")]
    public static void CortarPasos()
    {
        Directory.CreateDirectory(CarpetaDePasos);
        int total = 0;
        foreach ((string archivo, string superficie) in GrabacionesDePasos)
        {
            string ruta = Raiz + "/" + archivo;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
            if (clip == null) { Debug.LogWarning("[Audio] No esta la grabacion " + ruta); continue; }

            List<float[]> pasos = Cortar(clip, 6);
            for (int i = 0; i < pasos.Count; i++)
                EscribirWav($"{CarpetaDePasos}/{superficie}_{i + 1}.wav", pasos[i], clip.frequency);
            total += pasos.Count;
            Debug.Log($"[Audio] {superficie}: {pasos.Count} pasos de {clip.name} ({clip.length:0.0} s).");
        }
        AssetDatabase.Refresh();
        Debug.Log($"[Audio] Pasos: {total} cortados en {CarpetaDePasos}.");
    }

    // Encuentra los golpes de la grabacion (subidas bruscas de la envolvente) y
    // se queda con los 'maximo' mas claros: cada uno desde un poco antes del
    // golpe hasta el siguiente (o 0,32 s), con fundido de salida y normalizado.
    private static List<float[]> Cortar(AudioClip clip, int maximo)
    {
        float[] mono = LeerMono(clip);
        int hz = clip.frequency;
        int ventana = Mathf.Max(1, hz / 200);              // 5 ms
        int pasoMinimo = (int)(hz * 0.22f);
        int largoMaximo = (int)(hz * 0.32f);

        var env = new float[mono.Length / ventana];
        for (int i = 0; i < env.Length; i++)
        {
            float s = 0f;
            for (int k = 0; k < ventana; k++) s += Mathf.Abs(mono[i * ventana + k]);
            env[i] = s / ventana;
        }
        float maxEnv = env.Length > 0 ? env.Max() : 0f;
        if (maxEnv <= 0f) return new List<float[]>();

        var golpes = new List<(int inicio, float fuerza)>();
        float umbral = maxEnv * 0.22f;
        int ultimo = -pasoMinimo;
        for (int i = 0; i < env.Length; i++)
        {
            int muestra = i * ventana;
            // Un golpe que ya arranca fuerte en el primer instante (la grabacion
            // de un paso suelto) tambien cuenta, aunque no haya silencio antes.
            bool subio = i < 2 ? env[i] >= umbral : env[i - 2] < umbral * 0.7f;
            if (env[i] >= umbral && subio && muestra - ultimo >= pasoMinimo)
            {
                float fuerza = 0f;
                for (int k = i; k < Mathf.Min(env.Length, i + 20); k++) fuerza = Mathf.Max(fuerza, env[k]);
                golpes.Add((muestra, fuerza));
                ultimo = muestra;
            }
        }

        // Un golpe solo (una grabacion de un paso): el clip entero desde el golpe.
        var elegidos = golpes.OrderByDescending(g => g.fuerza).Take(maximo).OrderBy(g => g.inicio).ToList();
        var pasos = new List<float[]>();
        for (int n = 0; n < elegidos.Count; n++)
        {
            int inicio = Mathf.Max(0, elegidos[n].inicio - (int)(hz * 0.012f));
            int siguiente = golpes.Where(g => g.inicio > elegidos[n].inicio).Select(g => g.inicio).DefaultIfEmpty(mono.Length).First();
            int largo = Mathf.Min(largoMaximo, siguiente - inicio - (int)(hz * 0.01f), mono.Length - inicio);
            if (largo < hz * 0.08f) continue;

            largo = SinSegundoGolpe(mono, inicio, largo, hz);
            var paso = new float[largo];
            Array.Copy(mono, inicio, paso, 0, largo);

            float pico = paso.Max(x => Mathf.Abs(x));
            float escala = pico > 0f ? 0.7f / pico : 1f;            // -3 dB
            int entrada = (int)(hz * 0.004f), salida = Mathf.Min((int)(hz * 0.06f), largo / 3);
            for (int i = 0; i < largo; i++)
            {
                float g = escala;
                if (i < entrada) g *= i / (float)entrada;
                if (i > largo - salida) g *= (largo - i) / (float)salida;
                paso[i] *= g;
            }
            pasos.Add(paso);
        }
        return pasos;
    }

    // Si despues del golpe la grabacion se calla y vuelve a subir (el paso
    // siguiente, o un taco que suena aparte), se corta en ese silencio: si no,
    // un solo paso sonaria como dos.
    private static int SinSegundoGolpe(float[] mono, int inicio, int largo, int hz)
    {
        int ventana = Mathf.Max(1, hz / 200);   // 5 ms
        int n = largo / ventana;
        if (n < 4) return largo;
        var env = new float[n];
        float pico = 0f;
        for (int i = 0; i < n; i++)
        {
            float s = 0f;
            for (int k = 0; k < ventana; k++) s += Mathf.Abs(mono[inicio + i * ventana + k]);
            env[i] = s / ventana;
            pico = Mathf.Max(pico, env[i]);
        }

        int desde = Mathf.Min(n - 1, (int)(0.05f * hz / ventana));   // a partir de 50 ms
        for (int i = desde; i < n; i++)
        {
            if (env[i] > pico * 0.08f) continue;
            for (int j = i + 1; j < n; j++)
                if (env[j] > pico * 0.3f) return Mathf.Max((int)(hz * 0.08f), (i + 2) * ventana);
            return largo;   // se apaga solo: queda entero
        }
        return largo;
    }

    private static float[] LeerMono(AudioClip clip)
    {
        clip.LoadAudioData();
        var datos = new float[clip.samples * clip.channels];
        if (!clip.GetData(datos, 0))
        {
            Debug.LogWarning($"[Audio] No se pudo leer {clip.name} (¿Streaming?).");
            return new float[0];
        }
        if (clip.channels == 1) return datos;
        var mono = new float[clip.samples];
        for (int i = 0; i < clip.samples; i++)
        {
            float s = 0f;
            for (int c = 0; c < clip.channels; c++) s += datos[i * clip.channels + c];
            mono[i] = s / clip.channels;
        }
        return mono;
    }

    private static void EscribirWav(string ruta, float[] muestras, int hz)
    {
        using (var fs = new FileStream(ruta, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int bytes = muestras.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(hz); w.Write(hz * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            foreach (float m in muestras) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(m * 32767f), -32768, 32767));
        }
    }

    // ---------------------------------------------------------------- medicion

    // Volumen percibido aproximado: el promedio de energia de las ventanas de
    // 0,4 s mas fuertes (el 70%), sin contar silencios. En dBFS.
    private static (float rms, float pico)? Medir(AudioClip clip)
    {
        clip.LoadAudioData();
        int canales = clip.channels;
        int ventana = Mathf.Max(1, (int)(clip.frequency * 0.4f)) * canales;
        var buffer = new float[ventana];
        var energias = new List<float>();
        float pico = 0f;

        for (int desde = 0; desde < clip.samples; desde += ventana / canales)
        {
            int n = Mathf.Min(ventana, (clip.samples - desde) * canales);
            if (n <= 0) break;
            if (n < buffer.Length) buffer = new float[n];
            if (!clip.GetData(buffer, desde)) return null;
            double suma = 0;
            for (int i = 0; i < n; i++) { float v = buffer[i]; suma += v * v; float a = Mathf.Abs(v); if (a > pico) pico = a; }
            float energia = (float)(suma / n);
            if (energia > 1e-6f) energias.Add(energia);   // -60 dB: silencio
        }
        if (energias.Count == 0) return null;

        energias.Sort();
        int saltar = (int)(energias.Count * 0.3f);
        float promedio = energias.Skip(saltar).Average();
        return (10f * Mathf.Log10(promedio), pico);
    }

    private static Dictionary<AudioClip, (float rms, float pico)> _medidas;

    private static (float rms, float pico)? MedidaDe(CatalogoDeAudio c, AudioClip clip)
    {
        if (clip == null) return null;
        if (_medidas.TryGetValue(clip, out var m)) return m;

        CatalogoDeAudio.Medicion guardada = c.mediciones.FirstOrDefault(x => x != null && x.clip == clip);
        if (guardada != null) return _medidas[clip] = (guardada.rmsDb, guardada.pico);

        string ruta = AssetDatabase.GetAssetPath(clip);
        var imp = AssetImporter.GetAtPath(ruta) as AudioImporter;
        bool eraStreaming = imp != null && imp.defaultSampleSettings.loadType == AudioClipLoadType.Streaming;
        if (eraStreaming)
        {
            // Un clip en Streaming no deja leer sus muestras: se lo descomprime
            // un momento para medirlo. La configuracion final la pone ConfigurarImportacion.
            AudioImporterSampleSettings s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
        }

        var medida = Medir(clip);
        if (medida == null) { Debug.LogWarning($"[Audio] No se pudo medir {ruta}."); return null; }
        _medidas[clip] = medida.Value;
        c.mediciones.Add(new CatalogoDeAudio.Medicion { clip = clip, rmsDb = medida.Value.rms, pico = medida.Value.pico });
        return medida;
    }

    // ---------------------------------------------------------------- catalogo

    private enum Grupo { Musica, Efecto }

    [MenuItem("PokeRush/Audio/Medir y armar el catalogo")]
    public static void ArmarCatalogo()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RutaDelCatalogo));
        var c = AssetDatabase.LoadAssetAtPath<CatalogoDeAudio>(RutaDelCatalogo);
        if (c == null)
        {
            c = ScriptableObject.CreateInstance<CatalogoDeAudio>();
            AssetDatabase.CreateAsset(c, RutaDelCatalogo);
        }
        if (c.mediciones == null) c.mediciones = new List<CatalogoDeAudio.Medicion>();
        c.mediciones.RemoveAll(m => m == null || m.clip == null);
        _medidas = new Dictionary<AudioClip, (float, float)>();

        // Musica por escena
        var escenas = new List<CatalogoDeAudio.MusicaDeUnaEscena>();
        foreach ((string escena, string musica, string superficie) in Escenas)
        {
            CatalogoDeAudio.MusicaDeUnaEscena vieja = c.escenas.FirstOrDefault(e => e != null && e.escena == escena);
            escenas.Add(new CatalogoDeAudio.MusicaDeUnaEscena
            {
                escena = escena,
                musica = Poner(vieja?.musica, Clip(musica), Grupo.Musica),
                combate = vieja?.combate ?? new Sonido(),
                superficie = superficie ?? vieja?.superficie
            });
        }
        c.escenas = escenas;

        var rivales = new List<CatalogoDeAudio.MusicaDeRival>();
        foreach ((string ruta, string musica) in Rivales)
        {
            var rival = AssetDatabase.LoadAssetAtPath<NPCDefinition>(ruta);
            if (rival == null) { Debug.LogWarning("[Audio] No esta el rival " + ruta); continue; }
            CatalogoDeAudio.MusicaDeRival viejo = c.rivales.FirstOrDefault(r => r != null && r.rival == rival.name);
            rivales.Add(new CatalogoDeAudio.MusicaDeRival { rival = rival.name, musica = Poner(viejo?.musica, Clip(musica), Grupo.Musica) });
        }
        c.rivales = rivales;

        AudioClip[] comunes = Clips("Peleas Random");
        c.peleasComunes = comunes.Select(clip =>
            Poner(c.peleasComunes?.FirstOrDefault(s => s != null && s.clip == clip), clip, Grupo.Musica)).ToArray();
        c.victoriaComun = Poner(c.victoriaComun, Clip("Efectos/Combate/victoria_entrenador.ogg"), Grupo.Musica);
        c.victoriaImportante = Poner(c.victoriaImportante, Clip("Efectos/Combate/victoria_importante.ogg"), Grupo.Musica);

        c.julietaFase1 = Poner(c.julietaFase1, Clip("Pelea contra julieta/Fase 1"), Grupo.Musica);
        c.julietaFase2 = Poner(c.julietaFase2, Clip("Pelea contra julieta/Fase 2"), Grupo.Musica);
        c.julietaFinal = Poner(c.julietaFinal, Clip("Pelea contra julieta/Fase Final casi por caer"), Grupo.Musica);
        c.julietaDespues = Poner(c.julietaDespues, Clip("After-vencer-Julieta"), Grupo.Musica);
        c.creditos = Poner(c.creditos, Clip("Creditos"), Grupo.Musica);

        // Menus
        c.cursor = Poner(c.cursor, Clip("Efectos/Menus/menu_cursor.ogg"), Grupo.Efecto);
        c.confirmar = Poner(c.confirmar, Clip("Efectos/Seleccion en el menu (X)"), Grupo.Efecto);
        c.cancelar = Poner(c.cancelar, Clip("Efectos/Menus/menu_cancelar.ogg"), Grupo.Efecto);
        c.abrirMenu = Poner(c.abrirMenu, Clip("Efectos/Menus/menu_abrir.ogg"), Grupo.Efecto);
        c.cerrarMenu = Poner(c.cerrarMenu, Clip("Efectos/Menus/menu_cerrar.ogg"), Grupo.Efecto);
        c.error = Poner(c.error, Clip("Efectos/Menus/menu_error.ogg"), Grupo.Efecto);
        c.guardar = Poner(c.guardar, Clip("Efectos/Menus/guardar_partida.wav"), Grupo.Efecto);
        c.comprar = Poner(c.comprar, Clip("Efectos/Menus/tienda_comprar.ogg"), Grupo.Efecto);
        c.pcPrender = Poner(c.pcPrender, Clip("Efectos/Pc Sound"), Grupo.Efecto);
        c.pcApagar = Poner(c.pcApagar, Clip("Efectos/Menus/pc_apagar.ogg"), Grupo.Efecto);
        c.pcAgarrar = Poner(c.pcAgarrar, Clip("Efectos/Menus/pc_agarrar.ogg"), Grupo.Efecto);
        c.pcSoltar = Poner(c.pcSoltar, Clip("Efectos/Menus/pc_soltar.ogg"), Grupo.Efecto);

        // Mundo
        c.curar = Poner(c.curar, Clip("Efectos/Curar Pokemon con enfermera"), Grupo.Efecto);
        c.objetoEncontrado = Poner(c.objetoEncontrado, Clip("Efectos/Pokemon Item Found"), Grupo.Efecto);
        c.objetoRecibido = Poner(c.objetoRecibido, Clip("Efectos/Pokemon Item received"), Grupo.Efecto);
        c.pokemonRecibido = Poner(c.pokemonRecibido, Clip("Efectos/Mundo/pokemon_recibido.wav"), Grupo.Efecto);
        c.teletransporte = Poner(c.teletransporte, Clip("Efectos/Pokemon Teleport Sound"), Grupo.Efecto);
        c.puertaEntrar = Poner(c.puertaEntrar, Clip("Efectos/Mundo/puerta_entrar.ogg"), Grupo.Efecto);
        c.puertaSalir = Poner(c.puertaSalir, Clip("Efectos/Mundo/puerta_salir.wav"), Grupo.Efecto);
        c.ascensorViaja = Poner(c.ascensorViaja, Clip("Efectos/Mundo/ascensor_viaja.ogg"), Grupo.Efecto);
        c.ascensorLlega = Poner(c.ascensorLlega, Clip("Efectos/Mundo/ascensor_llega.ogg"), Grupo.Efecto);
        c.exclamacion = Poner(c.exclamacion, Clip("Efectos/Mundo/exclamacion.ogg"), Grupo.Efecto);
        c.choque = Poner(c.choque, Clip("Efectos/Mundo/choque_pared.ogg"), Grupo.Efecto);

        // Combate
        c.lanzarBall = Poner(c.lanzarBall, Clip("Efectos/Combate/lanzar_ball.ogg"), Grupo.Efecto);
        c.pokeballSale = Poner(c.pokeballSale, Clip("Efectos/Pokeball Pokemon Out"), Grupo.Efecto);
        c.retirar = Poner(c.retirar, Clip("Efectos/Combate/retirar.ogg"), Grupo.Efecto);
        c.debilitado = Poner(c.debilitado, Clip("Efectos/Combate/debilitado.ogg"), Grupo.Efecto);
        c.golpeNormal = Poner(c.golpeNormal, Clip("Efectos/Combate/golpe_normal.ogg"), Grupo.Efecto);
        c.golpeSuperEficaz = Poner(c.golpeSuperEficaz, Clip("Efectos/Combate/golpe_super_eficaz.ogg"), Grupo.Efecto);
        c.golpePocoEficaz = Poner(c.golpePocoEficaz, Clip("Efectos/Combate/golpe_poco_eficaz.ogg"), Grupo.Efecto);
        c.subirNivel = Poner(c.subirNivel, Clip("Efectos/Combate/subir_nivel.ogg"), Grupo.Efecto);
        c.huir = Poner(c.huir, Clip("Efectos/Combate/huir.ogg"), Grupo.Efecto);
        c.usarObjeto = Poner(c.usarObjeto, Clip("Efectos/Combate/usar_objeto.ogg"), Grupo.Efecto);
        c.statSube = Poner(c.statSube, Clip("Efectos/Combate/stat_sube.wav"), Grupo.Efecto);
        c.statBaja = Poner(c.statBaja, Clip("Efectos/Combate/stat_baja.wav"), Grupo.Efecto);
        c.quemado = Poner(c.quemado, Clip("Efectos/Combate/estado_quemado.wav"), Grupo.Efecto);
        c.envenenado = Poner(c.envenenado, Clip("Efectos/Combate/estado_envenenado.wav"), Grupo.Efecto);
        c.paralizado = Poner(c.paralizado, Clip("Efectos/Combate/estado_paralizado.wav"), Grupo.Efecto);
        c.dormido = Poner(c.dormido, Clip("Efectos/Combate/estado_dormido.wav"), Grupo.Efecto);
        c.congelado = Poner(c.congelado, Clip("Efectos/Combate/estado_congelado.wav"), Grupo.Efecto);
        c.confundido = Poner(c.confundido, Clip("Efectos/Combate/estado_confundido.wav"), Grupo.Efecto);

        // Casino
        c.slotsGiro = Poner(c.slotsGiro, Clip("Efectos/Slots"), Grupo.Efecto);
        c.slotsFrena = Poner(c.slotsFrena, Clip("Efectos/Casino/slots_frena.ogg"), Grupo.Efecto);
        c.slotsMoneda = Poner(c.slotsMoneda, Clip("Efectos/Casino/slots_moneda.ogg"), Grupo.Efecto);
        c.premioChico = Poner(c.premioChico, Clip("Efectos/Casino/premio_chico.ogg"), Grupo.Efecto);
        c.premioGrande = Poner(c.premioGrande, Clip("Efectos/Casino/premio_grande.ogg"), Grupo.Efecto);
        c.ruletaGiro = Poner(c.ruletaGiro, Clip("Efectos/Ruleta NegroRojo girando"), Grupo.Efecto);
        c.ruedaGiro = Poner(c.ruedaGiro, Clip("Efectos/Rueda de premios"), Grupo.Efecto);
        c.fichas = Poner(c.fichas, Clip("Efectos/TakingorFalling chips"), Grupo.Efecto);
        c.carta = Poner(c.carta, Clip("Efectos/Taking Card Poker/oxidvideos-taking-playing-card-2-522516.mp3"), Grupo.Efecto);
        c.mezclar = Poner(c.mezclar, Clip("Efectos/Taking Card Poker/oxidvideos-shuffling-deck-of-cards-522518.mp3"), Grupo.Efecto);

        // Gritos (un .ogg con el nombre del asset de cada especie)
        c.gritos = new List<CatalogoDeAudio.GritoDeEspecie>();
        foreach (string guid in AssetDatabase.FindAssets("t:PokenDefinition", new[] { Pokens }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            var especie = AssetDatabase.LoadAssetAtPath<PokenDefinition>(ruta);
            var grito = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Gritos}/{Path.GetFileNameWithoutExtension(ruta)}.ogg");
            if (especie != null && grito != null)
                c.gritos.Add(new CatalogoDeAudio.GritoDeEspecie { especie = especie.name, grito = grito });
        }

        // Ataques (<slug>.wav, o <slug>_1.wav ... _N.wav: el primero es la carga y el ultimo el impacto)
        c.ataques = new List<CatalogoDeAudio.SonidoDeAtaque>();
        var porSlug = new Dictionary<string, CatalogoDeAudio.SonidoDeAtaque>();
        foreach (string guid in AssetDatabase.FindAssets("t:MoveDefinition", new[] { Movimientos }))
        {
            var mov = AssetDatabase.LoadAssetAtPath<MoveDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (mov == null || string.IsNullOrEmpty(mov.englishName)) continue;
            var sonido = SonidoDeAtaque(mov);
            if (sonido == null) continue;
            c.ataques.Add(sonido);
            porSlug[mov.englishName] = sonido;
        }
        c.ataquePorTipo = new AudioClip[Enum.GetValues(typeof(PokenType)).Length];
        foreach ((PokenType tipo, string candidatos) in AtaquePorTipo)
            foreach (string slug in candidatos.Split('|'))
                if (porSlug.TryGetValue(slug, out var s)) { c.ataquePorTipo[(int)tipo] = s.impacto; break; }

        // Pasos
        c.superficies = new List<CatalogoDeAudio.Superficie>();
        foreach (var grupo in AssetDatabase.FindAssets("t:AudioClip", new[] { CarpetaDePasos })
                     .Select(AssetDatabase.GUIDToAssetPath)
                     .GroupBy(r => Path.GetFileNameWithoutExtension(r).Split('_')[0]))
        {
            c.superficies.Add(new CatalogoDeAudio.Superficie
            {
                nombre = grupo.Key,
                pasos = grupo.OrderBy(r => r).Select(r => new Sonido { clip = AssetDatabase.LoadAssetAtPath<AudioClip>(r) }).ToArray()
            });
        }

        CalcularGanancias(c);
        EditorUtility.SetDirty(c);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Audio] Catalogo: {c.escenas.Count} escenas, {c.peleasComunes.Length} peleas comunes, " +
                  $"{c.gritos.Count} gritos, {c.ataques.Count} ataques con sonido propio, {c.superficies.Count} superficies.");
    }

    private static CatalogoDeAudio.SonidoDeAtaque SonidoDeAtaque(MoveDefinition mov)
    {
        string slug = mov.englishName;
        var unico = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ataques}/{slug}.wav");
        if (unico != null) return new CatalogoDeAudio.SonidoDeAtaque { movimiento = slug, impacto = unico };

        var partes = new List<AudioClip>();
        for (int i = 1; i < 10; i++)
        {
            var parte = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Ataques}/{slug}_{i}.wav");
            if (parte == null) break;
            partes.Add(parte);
        }
        if (partes.Count == 0) return null;
        return new CatalogoDeAudio.SonidoDeAtaque
        {
            movimiento = slug,
            carga = partes.Count > 1 ? partes[0] : null,
            impacto = partes[partes.Count - 1]
        };
    }

    // Que todo suene parejo: cada grupo se lleva a la mediana de su grupo (asi
    // el volumen general queda como lo venia escuchando el equipo), sin
    // saturar. El 'volumen' a mano queda aparte, encima de esto.
    private static readonly List<(Sonido sonido, Grupo grupo)> _asignados = new List<(Sonido, Grupo)>();
    private static CatalogoDeAudio _catalogoEnCurso;

    private static Sonido Poner(Sonido viejo, AudioClip clip, Grupo grupo)
    {
        var nuevo = new Sonido { clip = clip, volumen = viejo != null && viejo.clip == clip ? viejo.volumen : 1f };
        _asignados.Add((nuevo, grupo));
        return nuevo;
    }

    private static void CalcularGanancias(CatalogoDeAudio c)
    {
        foreach (Grupo grupo in new[] { Grupo.Musica, Grupo.Efecto })
        {
            var deEsteGrupo = _asignados.Where(a => a.grupo == grupo && a.sonido.clip != null).ToList();
            var medidas = new Dictionary<Sonido, (float rms, float pico)>();
            foreach (var a in deEsteGrupo)
            {
                var m = MedidaDe(c, a.sonido.clip);
                if (m != null) medidas[a.sonido] = m.Value;
            }
            if (medidas.Count == 0) continue;

            List<float> todos = medidas.Values.Select(v => v.rms).OrderBy(v => v).ToList();
            float objetivo = todos[todos.Count / 2];
            float minimo = grupo == Grupo.Musica ? 0.5f : 0.35f;
            // Los efectos pueden subir mas (hay grabaciones muy bajas, como el
            // sonido de seleccion), pero nunca por encima de su pico.
            float maximo = grupo == Grupo.Musica ? 1.9f : 3f;

            foreach (var par in medidas)
            {
                float g = Mathf.Pow(10f, (objetivo - par.Value.rms) / 20f);
                g = Mathf.Clamp(g, minimo, maximo);
                // Un efecto no se sube por encima del pico: saturaria.
                if (grupo == Grupo.Efecto && par.Value.pico > 0f) g = Mathf.Min(g, Mathf.Max(1f, 0.9f / par.Value.pico));
                par.Key.ganancia = (float)Math.Round(g, 3);
            }
        }
        _asignados.Clear();
    }

    // ---------------------------------------------------------------- busqueda

    // Un archivo ("Pelea_Nestor.mp3") o la primera pista de una carpeta.
    private static AudioClip Clip(string relativo)
    {
        if (string.IsNullOrEmpty(relativo)) return null;
        string ruta = Raiz + "/" + relativo;
        if (AssetDatabase.IsValidFolder(ruta)) return Clips(relativo).FirstOrDefault();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ruta);
        if (clip == null) Debug.LogWarning("[Audio] Falta " + ruta);
        return clip;
    }

    private static AudioClip[] Clips(string carpeta)
    {
        string ruta = Raiz + "/" + carpeta;
        if (!AssetDatabase.IsValidFolder(ruta)) { Debug.LogWarning("[Audio] Falta la carpeta " + ruta); return new AudioClip[0]; }
        return AssetDatabase.FindAssets("t:AudioClip", new[] { ruta })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(r => Path.GetDirectoryName(r).Replace('\\', '/') == ruta)
            .OrderBy(r => r)
            .Select(r => AssetDatabase.LoadAssetAtPath<AudioClip>(r))
            .Where(a => a != null)
            .ToArray();
    }
}

// Lo nuevo que entra a Assets/Audio se importa con las mismas reglas, solo la
// primera vez: si alguien cambia la configuracion a mano, no se la pisa.
public class ReglasDeImportacionDeAudio : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(AudioDelJuegoBuilder.Raiz + "/")) return;
        if (!assetImporter.importSettingsMissing) return;
        AudioDelJuegoBuilder.Aplicar((AudioImporter)assetImporter, assetPath);
    }
}
