using UnityEngine;

// Un NPC jugando en una tragaperras: de vez en cuando da un saltito, como quien
// se entusiasma con la tirada.
//
// Solo mueve el hijo 'Visual'. La raiz, con su collider, queda quieta: el NPC
// sigue bloqueando la misma celda y el jugador lo agarra con X igual que antes.
//
// El ritmo sale del nombre y la posicion del objeto, no de Random: entre NPCs
// se ve variado, pero es igual en todas las partidas, asi un bug visual siempre
// se reproduce.
public class NpcJugandoMaquina : MonoBehaviour
{
    [Tooltip("Lo que salta. Vacio = el hijo 'Visual'.")]
    [SerializeField] private Transform visual;

    [Header("Ritmo")]
    [Tooltip("Segundos entre saltitos: se elige un valor entre estos dos cada vez.")]
    [SerializeField] private float esperaMinima = 1.6f;
    [SerializeField] private float esperaMaxima = 4.5f;
    [Tooltip("Probabilidad de que el saltito sea doble.")]
    [Range(0f, 1f)] [SerializeField] private float chanceDoble = 0.3f;

    [Header("Salto")]
    [Tooltip("Alto en pixeles de arte (32 = una celda). Se redondea al pixel para no borronear.")]
    [SerializeField] private int alturaPixeles = 3;
    [SerializeField] private float duracion = 0.2f;

    private const float PixelesPorUnidad = 32f;

    private Vector3 _base;
    private System.Random _rng;
    private float _espera;
    private float _t;
    private int _saltosQuedan;

    private void Awake()
    {
        if (visual == null) visual = transform.Find("Visual");
        if (visual != null) _base = visual.localPosition;

        _rng = new System.Random(Semilla(name + transform.position));
        _espera = ProximaEspera();
    }

    private void OnDisable()
    {
        // Durante un combate el mapa se apaga: que no quede congelado en el aire.
        if (visual != null) visual.localPosition = _base;
        _t = 0f;
        _saltosQuedan = 0;
    }

    private void Update()
    {
        if (visual == null) return;

        if (_saltosQuedan > 0)
        {
            _t += Time.deltaTime;
            float p = Mathf.Clamp01(_t / Mathf.Max(0.01f, duracion));
            float pixeles = Mathf.Round(Mathf.Sin(p * Mathf.PI) * alturaPixeles);
            visual.localPosition = _base + new Vector3(0f, pixeles / PixelesPorUnidad, 0f);

            if (p < 1f) return;

            _t = 0f;
            _saltosQuedan--;
            visual.localPosition = _base;
            return;
        }

        _espera -= Time.deltaTime;
        if (_espera > 0f) return;

        _saltosQuedan = _rng.NextDouble() < chanceDoble ? 2 : 1;
        _espera = ProximaEspera();
    }

    private float ProximaEspera()
    {
        return Mathf.Lerp(esperaMinima, esperaMaxima, (float)_rng.NextDouble());
    }

    private static int Semilla(string texto)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in texto) h = h * 31 + c;
            return h;
        }
    }
}
