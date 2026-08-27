using UnityEngine;
using UnityEngine.UI;

// Una casilla de la grilla de slots.
//
// Sabe pintarse a partir de un SlotSymbol y nada mas: no conoce la maquina, ni
// la apuesta, ni al jugador. Si el simbolo no tiene icono cargado se dibuja con
// color y letra, asi la maquina es jugable antes de que exista el arte.
public class SlotCellWidget : MonoBehaviour
{
    [SerializeField] private Image fondo;
    [SerializeField] private Image icono;
    [SerializeField] private Text letra;
    [Tooltip("Se prende cuando esta casilla es parte de una linea que pago.")]
    [SerializeField] private GameObject resaltado;

    public void Mostrar(SlotSymbol simbolo)
    {
        if (simbolo == null)
        {
            if (icono != null) icono.enabled = false;
            if (letra != null) letra.text = "";
            return;
        }

        bool tieneIcono = simbolo.icono != null;

        if (icono != null)
        {
            icono.enabled = tieneIcono;
            icono.sprite = simbolo.icono;
            icono.color = Color.white;
        }

        if (letra != null)
        {
            letra.enabled = !tieneIcono;
            letra.text = tieneIcono ? "" : simbolo.letra;
        }

        if (fondo != null)
            fondo.color = tieneIcono ? new Color(0.12f, 0.13f, 0.18f, 1f) : simbolo.color;
    }

    public void Resaltar(bool activo)
    {
        if (resaltado != null) resaltado.SetActive(activo);
    }
}
