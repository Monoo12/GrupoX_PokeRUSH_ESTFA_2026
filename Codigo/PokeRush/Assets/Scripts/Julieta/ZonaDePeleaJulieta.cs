using UnityEngine;

// La zona delante de Julieta: pisarla arranca el encuentro. Le avisa a la
// PeleaJulieta de arriba en la jerarquia.
//
// Va en la capa Default con su Collider2D en 'Is Trigger', NO en Obstacles: el
// jugador decide si puede pisar una celda con un OverlapCircle contra
// Obstacles, y un trigger ahi lo frena como una pared (ver
// trigger-en-obstacles-frena-al-jugador y Casino_Piso_Final.md).
[RequireComponent(typeof(Collider2D))]
public class ZonaDePeleaJulieta : MonoBehaviour
{
    private PeleaJulieta _pelea;

    private void Awake() => _pelea = GetComponentInParent<PeleaJulieta>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_pelea == null || other.GetComponentInParent<PlayerController>() == null) return;
        _pelea.AlPisarLaZona();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (_pelea == null || other.GetComponentInParent<PlayerController>() == null) return;
        _pelea.AlSalirDeLaZona();
    }
}
