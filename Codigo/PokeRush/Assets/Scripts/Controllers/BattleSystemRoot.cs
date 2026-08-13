using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Raiz del prefab de combate. Es lo que se arrastra a una escena para tener
// combate: adentro vienen el HUD, los PokenView, el fondo, los efectos y el
// BattleController ya cableados entre si.
//
// Su unico trabajo es que el prefab sea REALMENTE autosuficiente: una escena de
// mapa no tiene por que traer un EventSystem, y sin EventSystem los botones del
// HUD no responden al click. En vez de pedirle al equipo que se acuerde de
// agregarlo, se chequea al despertar y se crea si falta.
//
// USO
//   1. Arrastrar el prefab a la escena y dejarlo DESACTIVADO.
//   2. En MapBattleBridge: 'combateRaiz' = este objeto,
//      'battleController' = el de adentro (Controller).
//   3. Los BattleZoneTrigger / InteractableTrigger lo encuentran solos.
public class BattleSystemRoot : MonoBehaviour
{
    [Header("Referencias internas del prefab")]
    [SerializeField] private BattleController battleController;
    [Tooltip("Camara propia del combate (orthographicSize 1.92).")]
    [SerializeField] private Camera battleCamera;
    [Tooltip("AudioListener de la camara de combate. Se apaga solo si la escena " +
             "ya tiene otro activo (Unity solo admite uno).")]
    [SerializeField] private AudioListener audioListener;

    [Header("Autosuficiencia")]
    [Tooltip("Crear un EventSystem si la escena no tiene ninguno. Sin el, los " +
             "botones del HUD no reciben clicks.")]
    [SerializeField] private bool crearEventSystemSiFalta = true;

    // Punto de entrada para quien quiera lanzar un combate por codigo
    public BattleController Controller => battleController;

    private void Awake()
    {
        if (battleController == null)
            battleController = GetComponentInChildren<BattleController>(includeInactive: true);
        if (battleCamera == null)
            battleCamera = GetComponentInChildren<Camera>(includeInactive: true);

        if (crearEventSystemSiFalta && EventSystem.current == null
            && FindAnyObjectByType<EventSystem>() == null)
        {
            var go = new GameObject("EventSystem (auto)");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>(); // el proyecto usa el Input System nuevo
        }
    }

    // El prefab se prende justo cuando arranca el combate (lo hace
    // MapBattleBridge). Ahi es cuando hay que resolver los choques con la
    // escena: dos AudioListener activos son un warning permanente de Unity, y
    // dos camaras dibujando a la vez tapan una a la otra.
    private void OnEnable()
    {
        if (audioListener == null && battleCamera != null)
            audioListener = battleCamera.GetComponent<AudioListener>();
        if (audioListener != null)
            audioListener.enabled = !HayOtroAudioListenerActivo();

        if (battleCamera == null)
        {
            Debug.LogWarning("[Combate] El prefab no tiene camara propia. El combate va a " +
                             "usar la de la escena y se va a ver desproporcionado: su layout " +
                             "esta hecho para orthographicSize 1.92.");
            return;
        }

        // Aviso util al montarlo: si el mapa sigue visible detras, es que el
        // puente no esta apagando su raiz.
        foreach (Camera otra in Camera.allCameras)
            if (otra != battleCamera && otra.depth >= battleCamera.depth)
                Debug.LogWarning($"[Combate] La camara '{otra.name}' dibuja por encima o al mismo " +
                                 "nivel que la del combate. Revisá el 'mapaRaiz' del MapBattleBridge.");
    }

    private bool HayOtroAudioListenerActivo()
    {
        foreach (AudioListener otro in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (otro != audioListener && otro.enabled && otro.gameObject.activeInHierarchy) return true;
        return false;
    }
}
