using UnityEngine;
using UnityEngine.SceneManagement;

// Que musica suena en cada momento, para todo el juego.
//
// MAPA: al cargar una escena busca su cancion en el CatalogoDeAudio por el
// NOMBRE de la escena. Si la escena tiene su propio MusicaDeEscena, manda ese
// (los menus y el Data Center lo traen desde antes). Asi una escena nueva no
// necesita nada adentro: la arena de Julieta suena apenas se llame BossFinal.
//
// COMBATE: al empezar una pelea elige, en este orden, la musica que el
// catalogo le asigna a ese rival, la que trae el propio rival
// (EnemyDefinition.battleMusic), la de combate de la escena, o una de las
// peleas comunes al azar sin repetir la anterior. Al ganar suena la fanfarria
// (la "importante" contra un jefe) y al volver al mapa vuelve su cancion.
//
// Una pelea con director (la de Julieta) maneja su propia musica por fases:
// aca no se toca nada mientras dure.
//
// Se crea solo al arrancar el juego; no se pone en ninguna escena.
public class MusicaDelJuego : MonoBehaviour
{
    private static MusicaDelJuego _instancia;

    private AudioClip _ultimaComun;
    private bool _enPelea;
    private Scene _escenaAtendida;   // la ultima que ya eligio su musica

    // Mientras este prendido, cambiar de escena no cambia la musica: el final
    // del juego lleva el tema de despues de vencer a Julieta de la arena a la
    // cueva (ver FinalDelJuego).
    public static bool Sostener { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Limpiar() => Sostener = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Crear()
    {
        if (_instancia != null) return;
        var go = new GameObject("[MusicaDelJuego]");
        DontDestroyOnLoad(go);
        _instancia = go.AddComponent<MusicaDelJuego>();
    }

    // La primera escena: si se entra a Play sin recargar la escena (opciones de
    // Enter Play Mode del proyecto), sceneLoaded no llega para ella.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void PrimeraEscena()
    {
        if (_instancia != null) _instancia.AlCargarEscena(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += AlCargarEscena;
        BattleController.AlgunaPeleaEmpezo += AlEmpezarPelea;
        BattleController.AlgunaPeleaTermina += AlTerminarPelea;
        BattleController.AlgunaPeleaTermino += AlVolverDeLaPelea;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        BattleController.AlgunaPeleaEmpezo -= AlEmpezarPelea;
        BattleController.AlgunaPeleaTermina -= AlTerminarPelea;
        BattleController.AlgunaPeleaTermino -= AlVolverDeLaPelea;
        if (_instancia == this) _instancia = null;
    }

    private static CatalogoDeAudio Catalogo => CatalogoDeAudio.Actual;
    private static AudioService Audio => AudioService.Instance;

    // ---------- lo que piden otros ----------

    // Hace sonar un tema del catalogo (la pelea de Julieta, los creditos).
    public static void Sonar(Sonido sonido, bool loop = true, float fundido = -1f)
    {
        if (sonido == null || !sonido.Hay) return;
        CatalogoDeAudio c = Catalogo;
        Audio.SetMusicVolume(c != null ? c.volumenBaseDeMusica : 0.5f);
        Audio.PlayMusic(sonido.clip, loop, sonido.Volumen,
                        fundido >= 0f ? fundido : c != null ? c.fundidoEntreCanciones : 0.8f);
    }

    // Lo que usa MusicaDeEscena: un clip suelto con el volumen de su escena.
    public static void SonarEnLaEscena(AudioClip clip, float volumenDeEscena, bool loop)
    {
        if (clip == null) return;
        Audio.SetMusicVolume(volumenDeEscena);
        Audio.PlayMusic(clip, loop);
    }

    // Vuelve a la cancion de la escena abierta (despues de un tema especial).
    public static void VolverALaDeLaEscena(float fundido = -1f)
    {
        AudioClip propia = MusicaDeEscena.Actual != null ? MusicaDeEscena.Actual.Musica : null;
        if (propia != null)
        {
            SonarEnLaEscena(propia, MusicaDeEscena.Actual.Volumen, true);
            return;
        }

        CatalogoDeAudio.MusicaDeUnaEscena entrada = Catalogo != null
            ? Catalogo.DeLaEscena(SceneManager.GetActiveScene().name)
            : null;
        if (entrada != null) Sonar(entrada.musica, true, fundido);
    }

    public static void Callar(float fundido = 1f) => Audio.StopMusic(fundido);

    // ---------- escenas ----------

    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        if (modo != LoadSceneMode.Single || escena == _escenaAtendida) return;
        _escenaAtendida = escena;
        _enPelea = false;
        if (Sostener) return;

        // La escena que trae su propio MusicaDeEscena lo hace sonar en su Start.
        if (FindAnyObjectByType<MusicaDeEscena>() != null) return;

        CatalogoDeAudio.MusicaDeUnaEscena entrada = Catalogo != null ? Catalogo.DeLaEscena(escena.name) : null;
        if (entrada != null && entrada.musica.Hay) Sonar(entrada.musica);
        // Sin entrada sigue lo que venia sonando: los creditos y las escenas de
        // prueba se ocupan de lo suyo.
    }

    // ---------- combate ----------

    private void AlEmpezarPelea(BattleController combate)
    {
        _enPelea = true;
        if (combate.Director != null) return;   // la pelea con guion tiene su musica

        Sonido tema = MusicaDePelea(combate.Rival);
        if (tema != null) Sonar(tema, true, 0.25f);
    }

    private void AlTerminarPelea(BattleController combate, bool victoria, bool escapo)
    {
        if (combate.Director != null || Catalogo == null) return;

        if (victoria)
        {
            Sonido fanfarria = EsUnJefe(combate.Rival) ? Catalogo.victoriaImportante : Catalogo.victoriaComun;
            if (fanfarria.Hay) { Sonar(fanfarria, false, 0.2f); return; }
        }

        // Perder o escapar: la musica de pelea se apaga mientras sale el cartel.
        Callar(escapo ? 0.6f : 1.2f);
    }

    private void AlVolverDeLaPelea(BattleController combate, BattleOutcome resultado)
    {
        _enPelea = false;
        if (combate.Director != null) return;
        VolverALaDeLaEscena(1.2f);
    }

    public static bool EnPelea => _instancia != null && _instancia._enPelea;

    private Sonido MusicaDePelea(NPCDefinition rival)
    {
        CatalogoDeAudio c = Catalogo;

        // 1) la que el catalogo le asigna a este rival
        if (c != null && rival != null)
            foreach (CatalogoDeAudio.MusicaDeRival m in c.rivales)
                if (m != null && m.rival == rival.name && m.musica.Hay) return m.musica;

        // 2) la que trae el rival
        AudioClip propia = rival is EnemyDefinition e ? e.battleMusic
                         : rival is BossDefinition b ? b.battleMusic
                         : null;
        if (propia != null) return new Sonido { clip = propia, ganancia = c != null ? c.GananciaDe(propia) : 1f };

        // 3) la de combate de la escena
        AudioClip deLaEscena = MusicaDeEscena.Actual != null ? MusicaDeEscena.Actual.MusicaDeCombate : null;
        if (deLaEscena != null) return new Sonido { clip = deLaEscena, ganancia = c != null ? c.GananciaDe(deLaEscena) : 1f };
        CatalogoDeAudio.MusicaDeUnaEscena entrada = c != null ? c.DeLaEscena(SceneManager.GetActiveScene().name) : null;
        if (entrada != null && entrada.combate.Hay) return entrada.combate;

        // 4) una comun al azar, que no sea la de la pelea anterior
        if (c == null || c.peleasComunes == null || c.peleasComunes.Length == 0) return null;
        Sonido elegida = null;
        for (int intento = 0; intento < 6; intento++)
        {
            elegida = c.peleasComunes[Random.Range(0, c.peleasComunes.Length)];
            if (elegida != null && elegida.Hay && (elegida.clip != _ultimaComun || c.peleasComunes.Length == 1)) break;
        }
        if (elegida != null) _ultimaComun = elegida.clip;
        return elegida;
    }

    // Un jefe es el que tiene musica propia (Nestor, Reymar) o es un BossDefinition.
    private static bool EsUnJefe(NPCDefinition rival)
    {
        if (rival == null) return false;
        if (rival is BossDefinition) return true;
        if (rival is EnemyDefinition e && e.battleMusic != null) return true;
        CatalogoDeAudio c = Catalogo;
        if (c != null)
            foreach (CatalogoDeAudio.MusicaDeRival m in c.rivales)
                if (m != null && m.rival == rival.name) return true;
        return false;
    }
}
