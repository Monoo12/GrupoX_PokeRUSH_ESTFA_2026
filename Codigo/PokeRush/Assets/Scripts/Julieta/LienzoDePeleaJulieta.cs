using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Las dos piezas visuales propias de la pelea de Julieta: la barra de vida del
// PJ y la señal de que Julieta tomo el control del sistema.
//
// UI DE PRUEBA: se arma sola en runtime (sin prefab ni escena) para que la
// mecanica se lea mientras el equipo no diseñe la definitiva. Para
// reemplazarla alcanza con otra vista que reciba las mismas llamadas; la logica
// (PeleaJulieta) no sabe como esta dibujada.
//
// Viven en un Canvas propio, suelto en la raiz de la escena: el objeto de la
// pelea esta adentro de MapaRaiz, que se apaga durante el combate, y una vista
// hija suya se apagaria justo cuando hace falta.
public class LienzoDePeleaJulieta : MonoBehaviour
{
    public VidaDelPJView Vida { get; private set; }
    public SenalDeControlView Senal { get; private set; }

    public static LienzoDePeleaJulieta Crear(Font fuente, Color colorDeJulieta)
    {
        var go = new GameObject("[PeleaJulieta · UI]");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Arriba del HUD del combate (0) y abajo de la caja de dialogo (100).
        canvas.sortingOrder = 60;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        var lienzo = go.AddComponent<LienzoDePeleaJulieta>();
        if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // La señal va primero para quedar DEBAJO de la barra.
        lienzo.Senal = SenalDeControlView.Armar(go.transform, fuente, colorDeJulieta);
        lienzo.Vida = VidaDelPJView.Armar(go.transform, fuente, colorDeJulieta);
        return lienzo;
    }

    // Herramientas de armado compartidas por las dos vistas.
    public static RectTransform Rect(Transform padre, string nombre, Vector2 anclaMin, Vector2 anclaMax,
                                     Vector2 pivote, Vector2 posicion, Vector2 tamanio)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.pivot = pivote;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamanio;
        return rt;
    }

    public static Image Imagen(RectTransform rt, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;   // nunca le roba el click al HUD
        return img;
    }

    public static Text Texto(RectTransform rt, Font fuente, int tamanio, TextAnchor alineado, Color color)
    {
        var t = rt.gameObject.AddComponent<Text>();
        t.font = fuente;
        t.fontSize = tamanio;
        t.alignment = alineado;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var sombra = rt.gameObject.AddComponent<Shadow>();
        sombra.effectColor = new Color(0f, 0f, 0f, 0.8f);
        sombra.effectDistance = new Vector2(2f, -2f);
        return t;
    }
}
