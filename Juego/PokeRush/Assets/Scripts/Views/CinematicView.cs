using UnityEngine;

public class CinematicView : MonoBehaviour
{
    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Play()
    {
        Show();
        // logica de la cinematica especifica — se implementa despues
    }

    public void Stop()
    {
        // detener cinematica en curso — se implementa despues
        Hide();
    }
}

//Esta es la versión más mínima posible — solo el esqueleto de activar/desactivar. El contenido real de cada cinemática (mover cámara, reproducir un video, animaciones secuenciadas) depende de qué herramienta use el equipo (Timeline de Unity, animaciones por código, etc.), algo que todavía no está definido.