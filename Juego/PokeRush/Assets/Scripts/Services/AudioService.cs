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