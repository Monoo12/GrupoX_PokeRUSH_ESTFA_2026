using UnityEngine;

public class AudioService
{
    // Singleton: una sola instancia compartida por todo el juego
    private static AudioService _instance;
    public static AudioService Instance => _instance ??= new AudioService();

    // Fuentes de audio separadas: una para musica de fondo, otra para efectos de sonido
    // Se asignan una sola vez desde un GameObject persistente en la escena inicial
    private AudioSource _musicSource;
    private AudioSource _sfxSource;

    private AudioService() { }

    // Se llama una vez al arrancar el juego, pasandole las fuentes de audio de la escena
    public void Initialize(AudioSource musicSource, AudioSource sfxSource)
    {
        _musicSource = musicSource;
        _sfxSource = sfxSource;
        // Las fuentes son nuevas en cada arranque: hay que volcarles el volumen
        // que ya estaba configurado o el menu de Opciones no tendria efecto.
        AplicarVolumenes();
    }

    // Cambia la musica de fondo. Si ya esta sonando la misma, no la reinicia
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (_musicSource == null || clip == null) return;

        if (_musicSource.clip == clip && _musicSource.isPlaying) return;

        _musicSource.clip = clip;
        _musicSource.loop = loop;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        _musicSource?.Stop();
    }

    // Volumen de la musica de fondo. Cada escena puede pedir el suyo: la fuente
    // es una sola y sobrevive los cambios de escena, asi que sin esto el volumen
    // quedaria fijo al del primer AudioSource que se haya inicializado.
    public void SetMusicVolume(float volumen)
    {
        _sceneMusicVolume = Mathf.Clamp01(volumen);
        AplicarVolumenes();
    }

    public float GetMusicVolume() => _musicSource == null ? 0f : _musicSource.volume;

    // ---------- volumen configurado por el jugador ----------
    //
    // Son DOS volumenes multiplicandose, no uno: cada escena pide el suyo
    // (MusicaDeEscena) y el jugador tiene el general del menu de Opciones. Con
    // un solo valor, entrar a una escena pisaba lo que el jugador habia
    // elegido, o al reves.

    private float _sceneMusicVolume = 1f;
    private float _userMusicVolume = 1f;
    private float _userSfxVolume = 1f;

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
        if (_musicSource != null) _musicSource.volume = _sceneMusicVolume * _userMusicVolume;
        if (_sfxSource != null) _sfxSource.volume = _userSfxVolume;
    }

    // Para poder confirmar que la musica arranco de verdad. Un clip en
    // Streaming nunca queda en estado 'Loaded', asi que no sirve mirar
    // AudioClip.loadState: hay que preguntarle a la fuente.
    public bool IsMusicPlaying => _musicSource != null && _musicSource.isPlaying;

    // Reproduce un efecto de sonido una sola vez, sin interrumpir la musica
    public void PlaySFX(AudioClip clip)
    {
        if (_sfxSource == null || clip == null) return;

        _sfxSource.PlayOneShot(clip);
    }
}

/*udioSource es un componente de Unity que reproduce sonido — necesita estar en un GameObject de la escena, 
 * por eso Initialize() recibe referencias ya creadas en lugar de crearlas el servicio mismo. PlayOneShot permite que un efecto de sonido 
 * se superponga a otro sin cortar el anterior (por ejemplo, dos golpes seguidos), mientras que _musicSource.Play() reemplaza la música actual.*/