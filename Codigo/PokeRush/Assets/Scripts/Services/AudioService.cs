using System.Collections.Generic;
using UnityEngine;

// El que hace sonar todo. Una sola instancia para todo el juego.
//
// MUSICA: dos fuentes que se pasan la posta con un fundido cruzado, asi el
// cambio de escena o de mapa a combate no corta seco. Si piden la cancion que
// ya esta sonando no se reinicia (el menu y la pantalla de partidas comparten
// tema y pasar de una a otra no se nota).
//
// EFECTOS: los cortos van por PlayOneShot y se pueden pisar entre si (dos
// golpes seguidos). Los que necesitan tono propio (pasos, gritos) salen de un
// pequeño grupo de fuentes; los largos que hay que poder cortar (la ruleta
// girando) devuelven un SfxEnCurso para pararlos.
//
// JINGLES: "recibiste un objeto", "curaste a tu equipo". Mientras suenan, la
// musica baja sola y despues vuelve.
//
// VOLUMEN: musica = escena x ganancia del clip x Opciones x jingle x fundido.
// La ganancia de cada clip la mide el armador del catalogo, para que todas las
// canciones suenen parejas. Los efectos multiplican por el volumen de efectos
// de Opciones.
public class AudioService
{
    // Singleton: una sola instancia compartida por todo el juego
    private static AudioService _instance;
    public static AudioService Instance => _instance ??= new AudioService();

    // El proyecto no recarga el dominio al entrar a Play: sin esto quedaban
    // referencias a fuentes de la sesion anterior, ya destruidas.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstatico() => _instance = null;

    private class CanalDeMusica
    {
        public AudioSource Fuente;
        public float Fundido;         // 0..1, lo mueve Tick
        public float Objetivo = 1f;
        public float Velocidad = 1f;  // unidades de fundido por segundo
        public float Ganancia = 1f;   // la del clip, medida por el catalogo
        public bool PararAlApagarse;
    }

    private CanalDeMusica _a, _b, _actual;
    private CanalDeMusica[] _canales = new CanalDeMusica[0];   // _a y _b, sin crear arrays por frame
    private AudioSource _sfxSource;
    private AudioSource _jingle;
    private readonly List<AudioSource> _conTono = new List<AudioSource>();
    private int _proximoConTono;
    private readonly List<SfxEnCurso> _largos = new List<SfxEnCurso>();
    private GameObject _raiz;

    private float _sceneMusicVolume = 1f;
    private float _userMusicVolume = 1f;
    private float _userSfxVolume = 1f;
    private float _agache = 1f;   // 1 = normal; baja mientras suena un jingle

    private const int FuentesConTono = 6;
    private const float MusicaDuranteJingle = 0.2f;

    private AudioService() { }

    // ---------- arranque ----------

    // Se llama una vez al arrancar el juego (GameManager.Awake), con las
    // fuentes de su objeto. Si vienen vacias se crean: sin fuentes el juego
    // quedaria mudo sin ningun aviso.
    public void Initialize(AudioSource musicSource, AudioSource sfxSource)
    {
        // Si ya habia fuentes (las que armo Asegurar porque la escena no tenia
        // GameManager todavia), la cancion que venia sonando pasa a las nuevas
        // en el mismo punto, y las viejas se callan. Pasa al entrar directo a
        // un nivel: la musica arranca antes de que SceneBootstrapper cree el
        // GameManager.
        AudioClip previa = null;
        float previaTiempo = 0f, previaGanancia = 1f;
        bool previaLoop = true;
        if (Listo && _actual.Fuente.isPlaying && _actual.Fuente.clip != null)
        {
            previa = _actual.Fuente.clip;
            previaTiempo = _actual.Fuente.time;
            previaLoop = _actual.Fuente.loop;
            previaGanancia = _actual.Ganancia;
        }
        foreach (CanalDeMusica c in _canales)
            if (c != null && c.Fuente != null) c.Fuente.Stop();

        _raiz = musicSource != null ? musicSource.gameObject
              : sfxSource != null ? sfxSource.gameObject
              : null;
        if (_raiz == null)
        {
            _raiz = new GameObject("[Audio]");
            Object.DontDestroyOnLoad(_raiz);
        }

        if (musicSource == null) musicSource = _raiz.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = _raiz.AddComponent<AudioSource>();

        _a = new CanalDeMusica { Fuente = Preparar(musicSource, musica: true) };
        _b = new CanalDeMusica { Fuente = Preparar(_raiz.AddComponent<AudioSource>(), musica: true), Fundido = 0f };
        _actual = _a;
        _canales = new[] { _a, _b };
        _sfxSource = Preparar(sfxSource, musica: false);
        _jingle = Preparar(_raiz.AddComponent<AudioSource>(), musica: false);

        _conTono.Clear();
        for (int i = 0; i < FuentesConTono; i++)
            _conTono.Add(Preparar(_raiz.AddComponent<AudioSource>(), musica: false));
        _largos.Clear();
        _agache = 1f;

        if (_raiz.GetComponent<AudioServiceRunner>() == null) _raiz.AddComponent<AudioServiceRunner>();

        // Las fuentes son nuevas en cada arranque: hay que volcarles el volumen
        // que ya estaba configurado o el menu de Opciones no tendria efecto.
        AplicarVolumenes();

        if (previa != null)
        {
            _a.Fuente.clip = previa;
            _a.Fuente.loop = previaLoop;
            _a.Ganancia = previaGanancia;
            _a.Fundido = 1f;
            _a.Objetivo = 1f;
            _a.Fuente.Play();
            _a.Fuente.time = Mathf.Min(previaTiempo, Mathf.Max(0f, previa.length - 0.05f));
            AplicarVolumenes();
        }
    }

    private static AudioSource Preparar(AudioSource fuente, bool musica)
    {
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;
        fuente.loop = musica;
        // La musica pide prioridad alta: con muchas voces a la vez Unity corta
        // primero las de menor prioridad, y que se corte un paso no se nota.
        fuente.priority = musica ? 0 : 128;
        return fuente;
    }

    // Todas las fuentes vivas. Despues de salir de Play (sin recarga de dominio)
    // el servicio sigue apuntando a las de esa sesion, ya destruidas.
    public bool Listo => _a != null && _a.Fuente != null && _b != null && _b.Fuente != null
                         && _sfxSource != null && _jingle != null;

    // Una escena abierta sola para probar (los creditos, una de prueba) no
    // tiene GameManager: en Play, el servicio arma sus propias fuentes en vez de
    // quedar mudo. Si despues aparece un GameManager, Initialize las reemplaza.
    private bool Asegurar()
    {
        // En el editor fuera de Play (los smoke tests entregan premios, curan,
        // etc.) no suena nada.
        if (!Application.isPlaying) return false;
        if (Listo) return true;
        Initialize(null, null);
        return Listo;
    }

    // ---------- musica ----------

    // Cambia la musica de fondo. Si ya esta sonando la misma, no la reinicia
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        CatalogoDeAudio catalogo = CatalogoDeAudio.Actual;
        PlayMusic(clip, loop, catalogo != null ? catalogo.GananciaDe(clip) : 1f,
                  catalogo != null ? catalogo.fundidoEntreCanciones : 0.8f);
    }

    public void PlayMusic(AudioClip clip, bool loop, float ganancia, float fundido)
    {
        if (clip == null || !Asegurar()) return;

        if (_actual.Fuente.clip == clip)
        {
            // La misma: si sono y se corto (o todavia no arranco porque el clip
            // en Streaming no estaba listo) se reintenta en la misma fuente, sin
            // fundido cruzado contra si misma.
            _actual.Ganancia = ganancia;
            _actual.Objetivo = 1f;
            _actual.PararAlApagarse = false;
            _actual.Fuente.loop = loop;
            if (!_actual.Fuente.isPlaying) _actual.Fuente.Play();
            AplicarVolumenes();
            return;
        }

        CanalDeMusica saliente = _actual;
        CanalDeMusica entrante = _actual == _a ? _b : _a;
        bool habiaMusica = saliente.Fuente.isPlaying && saliente.Fundido > 0.01f;
        float velocidad = fundido > 0.01f ? 1f / fundido : 1000f;

        if (habiaMusica)
        {
            saliente.Objetivo = 0f;
            saliente.Velocidad = velocidad;
            saliente.PararAlApagarse = true;
        }
        else
        {
            saliente.Fuente.Stop();
            saliente.Fundido = 0f;
        }

        entrante.Fuente.Stop();
        entrante.Fuente.clip = clip;
        entrante.Fuente.loop = loop;
        entrante.Ganancia = ganancia;
        entrante.Fundido = habiaMusica ? 0f : Mathf.Min(1f, 0.35f);
        entrante.Objetivo = 1f;
        entrante.Velocidad = velocidad;
        entrante.PararAlApagarse = false;
        entrante.Fuente.Play();
        _actual = entrante;
        AplicarVolumenes();
    }

    public void StopMusic() => StopMusic(0f);

    public void StopMusic(float fundido)
    {
        if (!Listo) return;
        foreach (CanalDeMusica c in _canales)
        {
            if (fundido <= 0.01f)
            {
                c.Fuente.Stop();
                c.Fundido = 0f;
                c.Fuente.clip = null;
            }
            else
            {
                c.Objetivo = 0f;
                c.Velocidad = 1f / fundido;
                c.PararAlApagarse = true;
            }
        }
    }

    public AudioClip CurrentMusic => Listo && _actual.Fuente.isPlaying ? _actual.Fuente.clip : null;

    // Volumen de la musica de fondo. Cada escena puede pedir el suyo: la fuente
    // es una sola y sobrevive los cambios de escena, asi que sin esto el volumen
    // quedaria fijo al del primer AudioSource que se haya inicializado.
    public void SetMusicVolume(float volumen)
    {
        _sceneMusicVolume = Mathf.Clamp01(volumen);
        AplicarVolumenes();
    }

    public float GetMusicVolume() => Listo ? _actual.Fuente.volume : 0f;

    // ---------- volumen configurado por el jugador ----------
    //
    // Son DOS volumenes multiplicandose, no uno: cada escena pide el suyo
    // (MusicaDeEscena) y el jugador tiene el general del menu de Opciones. Con
    // un solo valor, entrar a una escena pisaba lo que el jugador habia
    // elegido, o al reves.

    public float UserMusicVolume => _userMusicVolume;
    public float UserSfxVolume => _userSfxVolume;

    public void SetUserMusicVolume(float volumen)
    {
        _userMusicVolume = Mathf.Clamp01(volumen);
        AplicarVolumenes();
    }

    public void SetUserSfxVolume(float volumen)
    {
        _userSfxVolume = Mathf.Clamp01(volumen);
        AplicarVolumenes();
    }

    private void AplicarVolumenes()
    {
        if (!Listo) return;
        foreach (CanalDeMusica c in _canales)
            c.Fuente.volume = Mathf.Clamp01(_sceneMusicVolume * c.Ganancia * _userMusicVolume * _agache * c.Fundido);
        if (_sfxSource != null) _sfxSource.volume = _userSfxVolume;
        foreach (SfxEnCurso largo in _largos) largo.AplicarVolumen(_userSfxVolume);
    }

    // Para poder confirmar que la musica arranco de verdad. Un clip en
    // Streaming nunca queda en estado 'Loaded', asi que no sirve mirar
    // AudioClip.loadState: hay que preguntarle a la fuente.
    public bool IsMusicPlaying => Listo && _actual.Fuente.isPlaying;

    // ---------- efectos ----------

    // Reproduce un efecto de sonido una sola vez, sin interrumpir la musica
    public void PlaySFX(AudioClip clip) => PlaySFX(clip, 1f);

    public void PlaySFX(AudioClip clip, float volumen)
    {
        if (clip == null || !Asegurar()) return;
        _sfxSource.PlayOneShot(clip, Mathf.Max(0f, volumen));
    }

    // Con tono propio: pasos y gritos, que suenan menos repetidos con un poco
    // de variacion, o mas graves (el grito al debilitarse).
    public void PlaySFX(AudioClip clip, float volumen, float tono)
    {
        if (clip == null || !Asegurar()) return;
        if (_conTono.Count == 0) return;
        AudioSource fuente = _conTono[_proximoConTono];
        _proximoConTono = (_proximoConTono + 1) % _conTono.Count;
        if (fuente == null) return;
        fuente.pitch = Mathf.Clamp(tono, 0.3f, 3f);
        fuente.volume = _userSfxVolume;
        fuente.PlayOneShot(clip, Mathf.Max(0f, volumen));
    }

    // Una fanfarria corta: la musica baja mientras suena y despues vuelve.
    public void PlayJingle(AudioClip clip, float volumen = 1f)
    {
        if (clip == null || !Asegurar()) return;
        _jingle.Stop();
        _jingle.clip = clip;
        _jingle.volume = Mathf.Clamp01(volumen * _userSfxVolume);
        _jingle.Play();
    }

    public bool JingleSonando => _jingle != null && _jingle.isPlaying;

    // Un efecto largo que hay que poder cortar (la ruleta girando, los rodillos
    // de la tragaperras). Devuelve null si no hay audio.
    public SfxEnCurso PlayLargo(AudioClip clip, float volumen = 1f, bool loop = false)
    {
        if (clip == null || !Asegurar()) return null;

        SfxEnCurso libre = null;
        foreach (SfxEnCurso l in _largos)
            if (!l.Sonando) { libre = l; break; }
        if (libre == null)
        {
            libre = new SfxEnCurso(Preparar(_raiz.AddComponent<AudioSource>(), musica: false));
            _largos.Add(libre);
        }

        libre.Empezar(clip, volumen, loop, _userSfxVolume);
        return libre;
    }

    // ---------- tick ----------

    // Lo llama AudioServiceRunner en cada frame (tiempo sin escalar: los
    // fundidos siguen andando con el juego en pausa).
    internal void Tick(float dt)
    {
        if (!Listo) return;

        float objetivoAgache = JingleSonando ? MusicaDuranteJingle : 1f;
        _agache = Mathf.MoveTowards(_agache, objetivoAgache, dt * (objetivoAgache < _agache ? 6f : 1.5f));

        foreach (CanalDeMusica c in _canales)
        {
            if (!Mathf.Approximately(c.Fundido, c.Objetivo))
                c.Fundido = Mathf.MoveTowards(c.Fundido, c.Objetivo, dt * c.Velocidad);
            if (c.PararAlApagarse && c.Fundido <= 0.001f && c.Fuente.isPlaying)
            {
                c.Fuente.Stop();
                c.Fuente.clip = null;
                c.PararAlApagarse = false;
            }
        }

        foreach (SfxEnCurso largo in _largos) largo.Tick(dt);
        AplicarVolumenes();
    }
}

// Un efecto largo en curso. Se para con o sin fundido.
public class SfxEnCurso
{
    private readonly AudioSource _fuente;
    private float _volumen = 1f;
    private float _fundido = 1f;
    private float _velocidadDeSalida;
    private bool _saliendo;

    internal SfxEnCurso(AudioSource fuente) { _fuente = fuente; }

    public bool Sonando => _fuente != null && _fuente.isPlaying;

    internal void Empezar(AudioClip clip, float volumen, bool loop, float usuario)
    {
        _volumen = volumen;
        _fundido = 1f;
        _saliendo = false;
        _fuente.clip = clip;
        _fuente.loop = loop;
        _fuente.time = 0f;
        AplicarVolumen(usuario);
        _fuente.Play();
    }

    public void Parar(float fundido = 0.15f)
    {
        if (!Sonando) return;
        if (fundido <= 0.01f) { _fuente.Stop(); return; }
        _saliendo = true;
        _velocidadDeSalida = 1f / fundido;
    }

    internal void Tick(float dt)
    {
        if (!_saliendo) return;
        _fundido = Mathf.MoveTowards(_fundido, 0f, dt * _velocidadDeSalida);
        if (_fundido <= 0.001f)
        {
            _saliendo = false;
            _fuente.Stop();
        }
    }

    internal void AplicarVolumen(float usuario)
    {
        if (_fuente != null) _fuente.volume = Mathf.Clamp01(_volumen * _fundido * usuario);
    }
}

/*AudioSource es un componente de Unity que reproduce sonido: necesita estar en un GameObject de la escena,
 * por eso Initialize() recibe referencias ya creadas en lugar de crearlas el servicio mismo. PlayOneShot permite que un efecto de sonido
 * se superponga a otro sin cortar el anterior (por ejemplo, dos golpes seguidos), mientras que _musicSource.Play() reemplaza la musica actual.*/
