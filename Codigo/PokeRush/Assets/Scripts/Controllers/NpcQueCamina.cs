using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Un NPC que camina por la grilla como el jugador: celda por celda, con los
// cuatro cuadros de su hoja por direccion. El primero es Tixi, que se acerca a
// hablarte y despues vuelve a su lugar.
//
// No sabe nada de dialogos: el que lo usa pide "acercate al jugador" y "volve a
// tu casa" (corrutinas) y decide cuando. La casa es donde arranca la escena.
//
// El camino se busca con un BFS sobre las celdas de alrededor, mirando las
// mismas capas que frenan al jugador (Obstacles). Se arma de nuevo en cada
// paso: si algo se cruza (otro NPC que aparecio) busca otra vuelta, y si no hay
// ninguna se queda donde esta en vez de atravesar la pared.
//
// El collider va en el mismo objeto, asi que se mueve con el: el jugador lo
// choca y le habla donde este parado.
public class NpcQueCamina : MonoBehaviour
{
    private const int Abajo = 0, Izquierda = 1, Derecha = 2, Arriba = 3;

    [Header("La hoja")]
    [Tooltip("Los 16 cuadros de su hoja 4x4, en el orden de CasinoNpcBuilder: " +
             "abajo, izquierda, derecha, arriba; cuatro cuadros por direccion (el 0 es quieto).")]
    [SerializeField] private Sprite[] cuadros = new Sprite[16];
    [SerializeField] private SpriteRenderer visual;

    [Header("Caminar")]
    [Tooltip("Unidades por segundo. El jugador camina a 4.")]
    [SerializeField, Min(0.5f)] private float velocidad = 3.5f;
    [Tooltip("Cuadros por segundo, como el jugador.")]
    [SerializeField, Min(1f)] private float cuadrosPorSegundo = 6f;
    [Tooltip("Hacia donde mira cuando esta en su lugar.")]
    [SerializeField] private Vector2 miraEnSuLugar = Vector2.down;

    [Header("Choques")]
    [Tooltip("Lo que frena al jugador. Vacio = la capa Obstacles.")]
    [SerializeField] private LayerMask obstaculos;
    [SerializeField] private float radioDeChoque = 0.3f;
    [Tooltip("Hasta cuantas celdas se aleja buscando camino.")]
    [SerializeField, Min(2)] private int alcance = 14;

    private Vector2 _casa;
    private Vector2 _mira = Vector2.down;
    private Collider2D[] _propios;
    private float _reloj;
    private int _cuadro;

    public bool Caminando { get; private set; }
    public bool EnSuLugar => ((Vector2)transform.position - _casa).sqrMagnitude < 0.01f;
    public Vector2 Casa => _casa;

    private void Awake()
    {
        _casa = transform.position;
        _mira = miraEnSuLugar;
        _propios = GetComponentsInChildren<Collider2D>(true);
        if (obstaculos.value == 0) obstaculos = LayerMask.GetMask("Obstacles");
        if (visual == null)
        {
            Transform v = transform.Find("Visual");
            if (v != null) visual = v.GetComponent<SpriteRenderer>();
        }
    }

    // ---------- lo que se le pide ----------

    // Camina hasta una celda pegada al jugador (la mas cerca por donde se pueda
    // llegar) y lo mira. Si ya esta al lado, solo se da vuelta.
    public IEnumerator AcercarseA(Transform jugador)
    {
        if (jugador == null) yield break;
        Vector2 dondeEsta = Celda(jugador.position);

        if (!SonVecinas(Celda(transform.position), dondeEsta))
        {
            var destinos = new HashSet<Vector2Int>();
            foreach (Vector2 d in Direcciones)
                destinos.Add(Clave(dondeEsta + d));
            yield return Caminar(destinos, dondeEsta);
        }
        Mirar(dondeEsta - (Vector2)transform.position);
    }

    // Vuelve a donde arranco la escena y mira como miraba.
    public IEnumerator VolverASuLugar(Transform jugador)
    {
        if (!EnSuLugar)
            yield return Caminar(new HashSet<Vector2Int> { Clave(_casa) },
                                 jugador != null ? Celda(jugador.position) : (Vector2?)null);
        Mirar(miraEnSuLugar);
    }

    // Para darse vuelta sin caminar (cuando el jugador le habla de al lado).
    public void Mirar(Vector2 hacia)
    {
        _mira = Cardinal(hacia);
        _cuadro = 0;
        MostrarCuadro();
    }

    // ---------- caminar ----------

    private IEnumerator Caminar(HashSet<Vector2Int> destinos, Vector2? celdaDelJugador)
    {
        Caminando = true;
        _reloj = 0f;
        float z = transform.position.z;

        // Tope por si algo raro lo deja dando vueltas: nunca mas pasos que celdas a la redonda.
        for (int pasos = 0; pasos < alcance * alcance; pasos++)
        {
            Vector2 aca = Celda(transform.position);
            if (destinos.Contains(Clave(aca))) break;

            List<Vector2> camino = BuscarCamino(aca, destinos, celdaDelJugador);
            if (camino == null || camino.Count == 0)
            {
                Debug.Log($"[NpcQueCamina] '{name}' no encuentra camino: se queda en {aca}.");
                break;
            }

            Vector2 siguiente = camino[0];
            _mira = Cardinal(siguiente - aca);
            while ((Vector2)transform.position != siguiente)
            {
                Vector2 p = Vector2.MoveTowards(transform.position, siguiente, velocidad * Time.deltaTime);
                transform.position = new Vector3(p.x, p.y, z);
                Animar();
                yield return null;
            }
        }

        transform.position = new Vector3(Celda(transform.position).x, Celda(transform.position).y, z);
        Caminando = false;
        _cuadro = 0;
        MostrarCuadro();
    }

    // BFS de celda en celda. Devuelve las celdas a pisar, sin la de partida.
    private List<Vector2> BuscarCamino(Vector2 desde, HashSet<Vector2Int> destinos, Vector2? celdaDelJugador)
    {
        Vector2Int inicio = Clave(desde);
        var vino = new Dictionary<Vector2Int, Vector2Int> { [inicio] = inicio };
        var cola = new Queue<Vector2Int>();
        cola.Enqueue(inicio);
        Vector2Int? jugador = celdaDelJugador.HasValue ? Clave(celdaDelJugador.Value) : (Vector2Int?)null;

        while (cola.Count > 0)
        {
            Vector2Int c = cola.Dequeue();
            if (destinos.Contains(c) && c != inicio)
            {
                var camino = new List<Vector2>();
                for (Vector2Int k = c; k != inicio; k = vino[k]) camino.Add(Centro(k));
                camino.Reverse();
                return camino;
            }

            foreach (Vector2 d in Direcciones)
            {
                Vector2Int n = c + Vector2Int.RoundToInt(d);
                if (vino.ContainsKey(n)) continue;
                if (Mathf.Abs(n.x - inicio.x) > alcance || Mathf.Abs(n.y - inicio.y) > alcance) continue;
                if (jugador.HasValue && n == jugador.Value) continue;
                if (Ocupada(Centro(n))) continue;
                vino[n] = c;
                cola.Enqueue(n);
            }
        }
        return null;
    }

    // La misma pregunta que se hace el jugador antes de dar un paso, sin
    // contarse a si mismo.
    private bool Ocupada(Vector2 celda)
    {
        foreach (Collider2D c in Physics2D.OverlapCircleAll(celda, radioDeChoque, obstaculos))
        {
            if (System.Array.IndexOf(_propios, c) >= 0) continue;
            if (c.GetComponentInParent<PlayerController>() != null) continue; // el jugador se cuenta aparte
            return true;
        }
        return false;
    }

    // ---------- dibujo ----------

    private void Animar()
    {
        _reloj += Time.deltaTime;
        float dura = 1f / cuadrosPorSegundo;
        while (_reloj >= dura)
        {
            _reloj -= dura;
            _cuadro = (_cuadro + 1) % 4;
            // Cuadros 1 y 3 = el pie apoya (como el jugador, ver Pasos).
            if (_cuadro == 1 || _cuadro == 3) Pasos.DeNpc(transform.position);
        }
        MostrarCuadro();
    }

    private void MostrarCuadro()
    {
        if (visual == null || cuadros == null || cuadros.Length < 16) return;
        int fila = _mira == Vector2.left ? Izquierda : _mira == Vector2.right ? Derecha : _mira == Vector2.up ? Arriba : Abajo;
        Sprite s = cuadros[fila * 4 + _cuadro];
        if (s != null) visual.sprite = s;
    }

    // ---------- grilla ----------

    private static readonly Vector2[] Direcciones = { Vector2.down, Vector2.left, Vector2.right, Vector2.up };

    // Las celdas tienen el centro en .5 (la grilla del Tilemap).
    private static Vector2 Celda(Vector3 p) => new Vector2(Mathf.Floor(p.x) + 0.5f, Mathf.Floor(p.y) + 0.5f);
    private static Vector2Int Clave(Vector2 celda) => new Vector2Int(Mathf.FloorToInt(celda.x), Mathf.FloorToInt(celda.y));
    private static Vector2 Centro(Vector2Int k) => new Vector2(k.x + 0.5f, k.y + 0.5f);
    private static bool SonVecinas(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) < 1.1f;

    private static Vector2 Cardinal(Vector2 v)
    {
        if (v == Vector2.zero) return Vector2.down;
        if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return v.x >= 0f ? Vector2.right : Vector2.left;
        return v.y >= 0f ? Vector2.up : Vector2.down;
    }

    // Si el mapa se apaga a mitad de un paso (el combate apaga todo), que no
    // quede entre dos celdas.
    private void OnDisable()
    {
        if (!Caminando) return;
        Caminando = false;
        Vector2 c = Celda(transform.position);
        transform.position = new Vector3(c.x, c.y, transform.position.z);
    }
}
