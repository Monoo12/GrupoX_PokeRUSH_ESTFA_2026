using UnityEngine;

// Caja invisible que bloquea al jugador. El hermano suelto de ParedInvisibleTile.
//
// CUANDO USAR CADA UNO
// - El TILE, para lo que cae en la grilla: bordes del mapa, paredes, marcos de
//   puerta. Se pinta rapido y queda alineado solo.
// - ESTE BLOQUE, para lo que no cae en la grilla o cambia: una zona que hay que
//   estirar 3,5 celdas, tapar una salida hasta que se cumpla un evento, o
//   probar un bloqueo sin abrir la Tile Palette. Se arrastra y se escala.
//
// COMO SE USA
// Arrastrar Assets/ANDRUSIANI/Prefabs/BloqueInvisible.prefab a la escena y
// escalarlo. Se ve como una caja rosa en el editor (gizmo) y no se ve en el
// juego: OnDrawGizmos no existe fuera del editor, asi que no hay nada que
// apagar ni ningun SpriteRenderer que sacar antes de compilar.
//
// LA TRAMPA DE SIEMPRE
// Bloquea por CAPA, no por collider: el PlayerController resuelve el paso con
// un Physics2D.OverlapCircle contra su obstacleMask, que mira 'Obstacles'. Un
// bloque en 'Default' tiene collider y no frena a nadie. Por eso Awake lo
// corrige y avisa, en vez de fallar en silencio como los NPC mal puestos.
[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class BloqueInvisible : MonoBehaviour
{
    private const string CapaObstaculos = "Obstacles";

    [Tooltip("Color del gizmo en el editor. No se ve en el juego.")]
    [SerializeField] private Color color = new Color(1f, 0.15f, 0.45f, 0.35f);

    [Tooltip("Con esto en true el bloque deja pasar y solo avisa (OnTriggerEnter2D). " +
             "Sirve para zonas de evento; para bloquear de verdad, dejarlo en false.")]
    [SerializeField] private bool soloZona;

    private void Reset()
    {
        var caja = GetComponent<BoxCollider2D>();
        caja.size = Vector2.one;
        caja.offset = Vector2.zero;
        PonerEnCapaObstaculos(false);
    }

    private void OnValidate()
    {
        GetComponent<BoxCollider2D>().isTrigger = soloZona;
    }

    private void Awake()
    {
        PonerEnCapaObstaculos(true);
    }

    private void PonerEnCapaObstaculos(bool avisar)
    {
        int capa = LayerMask.NameToLayer(CapaObstaculos);
        if (capa < 0 || gameObject.layer == capa) return;

        gameObject.layer = capa;
        if (avisar)
            Debug.LogWarning($"[BloqueInvisible] '{name}' estaba en la capa " +
                             $"'{LayerMask.LayerToName(gameObject.layer)}' y no habria " +
                             $"frenado al jugador. Lo pase a '{CapaObstaculos}'.", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var caja = GetComponent<BoxCollider2D>();
        if (caja == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = soloZona ? new Color(0.2f, 0.8f, 1f, color.a) : color;
        Gizmos.DrawCube(caja.offset, caja.size);
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 1f);
        Gizmos.DrawWireCube(caja.offset, caja.size);
    }
#endif
}
