using UnityEngine;

// Que musica suena en esta escena.
//
// Va en un objeto suelto de cada escena, con el clip elegido en el Inspector.
// La reproduccion la hace AudioService, que vive en el GameManager y sobrevive
// los cambios de escena: por eso alcanza con decirle QUE sonar, y no hay que
// arrastrar un AudioSource por escena.
//
// Es OPCIONAL: sin este componente la escena toma su cancion del
// CatalogoDeAudio por el nombre (ver MusicaDelJuego). Si esta, manda este.
//
// Si dos escenas seguidas tienen el MISMO clip (el menu y la pantalla de
// guardados, por ejemplo), la musica no se reinicia al pasar de una a otra:
// AudioService.PlayMusic ignora el pedido si ya esta sonando ese clip.
//
// COMBATE
// 'musicaDeCombate' NO es la de cada pelea: es para una escena donde TODAS las
// peleas tienen que sonar igual. Lo normal es dejarlo vacio y que MusicaDelJuego
// elija (la del rival si tiene, o una de las peleas comunes).
public class MusicaDeEscena : MonoBehaviour
{
    [Header("Musica")]
    [Tooltip("La que suena mientras estas en esta escena.")]
    [SerializeField] private AudioClip musica;
    [Tooltip("Opcional: la de TODAS las peleas de esta escena. Vacio = la elige MusicaDelJuego.")]
    [SerializeField] private AudioClip musicaDeCombate;
    [SerializeField] private bool loop = true;
    [Range(0f, 1f)]
    [SerializeField] private float volumen = 0.5f;

    public static MusicaDeEscena Actual { get; private set; }

    public AudioClip Musica => musica;
    public AudioClip MusicaDeCombate => musicaDeCombate;
    public float Volumen => volumen;

    private void OnEnable() => Actual = this;

    private void OnDisable()
    {
        if (Actual == this) Actual = null;
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

        if (musica != null) StartCoroutine(SonarSeguro(musica));
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
            MusicaDelJuego.SonarEnLaEscena(clip, volumen, loop);
            yield return null;

            if (AudioService.Instance.IsMusicPlaying) yield break;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        Debug.LogWarning($"[Musica] '{clip.name}' no arranco despues de varios intentos. " +
                         "Revisa que el GameManager tenga asignadas sus AudioSource.");
    }
}
