using UnityEngine;

// Que musica suena en esta escena.
//
// Va en un objeto suelto de cada escena, con el clip elegido en el Inspector.
// La reproduccion la hace AudioService, que vive en el GameManager y sobrevive
// los cambios de escena: por eso alcanza con decirle QUE sonar, y no hay que
// arrastrar un AudioSource por escena.
//
// Si dos escenas seguidas tienen el MISMO clip (el menu y la pantalla de
// guardados, por ejemplo), la musica no se reinicia al pasar de una a otra:
// AudioService.PlayMusic ignora el pedido si ya esta sonando ese clip.
//
// COMBATE
// Si se asigna 'musicaDeCombate', el componente se engancha al BattleController
// de la escena y cambia la musica al empezar la pelea, volviendo a la del mapa
// al terminar. Sirve para el boss sin tocar el MapBattleBridge.
public class MusicaDeEscena : MonoBehaviour
{
    [Header("Musica")]
    [Tooltip("La que suena mientras estas en esta escena.")]
    [SerializeField] private AudioClip musica;
    [Tooltip("Opcional: la que suena durante el combate. Vacio = no cambia.")]
    [SerializeField] private AudioClip musicaDeCombate;
    [SerializeField] private bool loop = true;
    [Range(0f, 1f)]
    [SerializeField] private float volumen = 0.5f;

    [Header("Referencias (opcional: se busca sola)")]
    [SerializeField] private BattleController battleController;

    private bool _enganchado;

    private void Awake()
    {
        if (battleController == null)
        {
            // Include: el prefab de combate vive DESACTIVADO hasta que arranca
            // la pelea, y las busquedas por defecto ignoran lo inactivo.
            battleController = FindAnyObjectByType<BattleController>(FindObjectsInactive.Include);
        }
    }

    private void OnEnable()
    {
        if (battleController == null || musicaDeCombate == null) return;
        battleController.BattleStarted += SonarCombate;
        battleController.BattleEnded += VolverAlMapa;
        _enganchado = true;
    }

    private void OnDisable()
    {
        if (!_enganchado || battleController == null) return;
        battleController.BattleStarted -= SonarCombate;
        battleController.BattleEnded -= VolverAlMapa;
        _enganchado = false;
    }

    // En Start y no en Awake: AudioService lo inicializa GameManager en SU
    // Awake, y entre dos Awake el orden no esta garantizado. Todos los Awake
    // corren antes que cualquier Start, asi que aca ya esta listo.
    private void Start()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning($"[Musica] '{name}': no hay GameManager, " +
                             "asi que AudioService no tiene fuentes y no va a sonar nada. " +
                             "Entra por 'Menu Principal' o agregale un GameManager a la escena.");
            return;
        }

        AudioService.Instance.SetMusicVolume(volumen);
        Sonar(musica);
    }

    private void SonarCombate() => Sonar(musicaDeCombate);

    private void VolverAlMapa(BattleOutcome outcome) => Sonar(musica);

    private void Sonar(AudioClip clip)
    {
        if (clip == null) return;
        StopAllCoroutines();
        StartCoroutine(SonarSeguro(clip));
    }

    // Pide reproducir y COMPRUEBA que haya arrancado, reintentando un par de
    // veces.
    //
    // No se puede esperar a que el clip este 'Loaded': las pistas de musica
    // estan importadas como Streaming y un clip en Streaming nunca pasa a ese
    // estado, asi que esperarlo se cuelga hasta el timeout y no suena nada.
    // Tampoco alcanza con llamar a Play() y confiar: si los datos todavia no
    // estan listos, Play() no hace nada y no avisa.
    private System.Collections.IEnumerator SonarSeguro(AudioClip clip)
    {
        for (int intento = 1; intento <= 5; intento++)
        {
            AudioService.Instance.PlayMusic(clip, loop);
            yield return null;

            if (AudioService.Instance.IsMusicPlaying) yield break;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        Debug.LogWarning($"[Musica] '{clip.name}' no arranco despues de varios intentos. " +
                         "Revisa que el GameManager tenga asignadas sus AudioSource.");
    }
}
