using System.Collections;
using UnityEngine;

// El servidor principal de NeuraRush-LINK, detras de Julieta: la pantalla que
// el PJ destruye al final ("El PJ destruye el nucleo del servidor").
//
// Es solo lo que se ve. Lo rompe la accion RomperServidor del guion (la usa el
// final, despues de la pelea): parpadea como una pantalla que se quema y queda
// apagada. Reiniciar la pelea lo vuelve a prender.
//
// Vive adentro del objeto PeleaJulieta, asi se muda con ella a la arena.
public class ServidorPrincipal : MonoBehaviour
{
    [Tooltip("Vacio = todos los SpriteRenderer de los hijos.")]
    [SerializeField] private SpriteRenderer[] partes;
    [SerializeField] private Color apagado = new Color(0.22f, 0.22f, 0.28f, 1f);
    [Min(0f)] [SerializeField] private float segundosDelParpadeo = 1.2f;

    private Color[] _colores;

    public bool Roto { get; private set; }

    private void Awake()
    {
        if (partes == null || partes.Length == 0) partes = GetComponentsInChildren<SpriteRenderer>(true);
        _colores = new Color[partes.Length];
        for (int i = 0; i < partes.Length; i++) _colores[i] = partes[i] != null ? partes[i].color : Color.white;
    }

    public void Romper()
    {
        if (Roto) return;
        Roto = true;
        if (isActiveAndEnabled) StartCoroutine(Quemarse());
        else Pintar(apagado);
    }

    public void Reparar()
    {
        StopAllCoroutines();
        Roto = false;
        if (_colores == null) return;
        for (int i = 0; i < partes.Length; i++)
            if (partes[i] != null) partes[i].color = _colores[i];
    }

    // Cortes cada vez mas largos, como un tubo que se muere, y queda apagado.
    private IEnumerator Quemarse()
    {
        float t = 0f;
        while (t < segundosDelParpadeo)
        {
            float fase = t / Mathf.Max(0.01f, segundosDelParpadeo);
            bool prendido = Mathf.Sin(fase * Mathf.PI * 7f) > fase * 1.4f - 0.4f;
            Pintar(prendido ? Color.white : apagado);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        Pintar(apagado);
    }

    private void Pintar(Color color)
    {
        foreach (SpriteRenderer sr in partes)
            if (sr != null) sr.color = color;
    }
}
