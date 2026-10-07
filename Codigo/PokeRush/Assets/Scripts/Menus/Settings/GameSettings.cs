using UnityEngine;

// Las opciones del jugador, ya interpretadas (no strings sueltos).
//
// Es la fachada que usan el menu de Opciones y quien necesite el volumen: nadie
// mas toca SettingsService por su cuenta, asi las claves viven en un solo lado.
public static class GameSettings
{
    public const string KeyMusicVolume = "audio.music";
    public const string KeySfxVolume = "audio.sfx";
    public const string KeyConsola = "pruebas.consola";

    private const float DefaultMusicVolume = 0.7f;
    private const float DefaultSfxVolume = 0.8f;

    private static bool _applied;

    public static float MusicVolume
    {
        get => Mathf.Clamp01(SettingsService.Instance.GetFloat(KeyMusicVolume, DefaultMusicVolume));
        set
        {
            float v = Mathf.Clamp01(value);
            SettingsService.Instance.SetFloat(KeyMusicVolume, v);
            AudioService.Instance.SetUserMusicVolume(v);
        }
    }

    public static float SfxVolume
    {
        get => Mathf.Clamp01(SettingsService.Instance.GetFloat(KeySfxVolume, DefaultSfxVolume));
        set
        {
            float v = Mathf.Clamp01(value);
            SettingsService.Instance.SetFloat(KeySfxVolume, v);
            AudioService.Instance.SetUserSfxVolume(v);
        }
    }

    // La consola de administrador (ConsolaDeAdministrador): apagada de fabrica.
    // Prendida, se abre con la tecla de la izquierda del 1. Es de la maquina,
    // como el volumen: borrar la partida no la apaga.
    public static bool ConsolaHabilitada
    {
        get => SettingsService.Instance.GetBool(KeyConsola, false);
        set => SettingsService.Instance.SetBool(KeyConsola, value);
    }

    // Empuja los valores guardados al AudioService. Lo llama el MenuManager al
    // arrancar; idempotente para poder llamarlo desde donde sea sin pensarlo.
    public static void Apply()
    {
        AudioService.Instance.SetUserMusicVolume(MusicVolume);
        AudioService.Instance.SetUserSfxVolume(SfxVolume);
        ConfiguracionDePantalla.AplicarGuardada();
        _applied = true;
    }

    // El proyecto no recarga el dominio al entrar a Play: sin esto, la segunda
    // vez que se entra en el editor el volumen guardado no se aplicaba.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar() => _applied = false;

    public static void ApplyOnce()
    {
        if (!_applied) Apply();
    }

    public static void ResetToDefaults()
    {
        MusicVolume = DefaultMusicVolume;
        SfxVolume = DefaultSfxVolume;
        ConfiguracionDePantalla.Restaurar();
        GameInputService.Instance.ResetToDefaults();
        ConsolaHabilitada = false;
    }
}
