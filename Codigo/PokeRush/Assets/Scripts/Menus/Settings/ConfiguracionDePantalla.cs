using System.Collections.Generic;
using UnityEngine;

// Resolucion y modo de pantalla elegidos en Opciones.
//
// Se guardan en la tabla 'settings' de pokerush.db, como el volumen y los
// controles (SettingsService). La primera vez que se abre el juego no hay nada
// guardado y manda lo de Player Settings: pantalla completa a la resolucion
// del monitor.
//
// Las resoluciones son las que ofrece el monitor (Screen.resolutions), sin
// repetir por frecuencia y desde 1024x576: mas chico el juego no se lee.
public static class ConfiguracionDePantalla
{
    public const string KeyAncho = "video.ancho";
    public const string KeyAlto = "video.alto";
    public const string KeyModo = "video.modo";

    public static readonly FullScreenMode[] Modos = { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };

    public static string NombreDelModo(FullScreenMode modo)
        => modo == FullScreenMode.Windowed ? "Ventana" : "Pantalla completa";

    // De menor a mayor. Siempre trae al menos la actual.
    public static List<Vector2Int> Resoluciones()
    {
        var lista = new List<Vector2Int>();
        foreach (Resolution r in Screen.resolutions)
        {
            var v = new Vector2Int(r.width, r.height);
            if (v.x >= 1024 && v.y >= 576 && !lista.Contains(v)) lista.Add(v);
        }
        var actual = new Vector2Int(Screen.width, Screen.height);
        if (!lista.Contains(actual)) lista.Add(actual);
        lista.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return lista;
    }

    public static Vector2Int ResolucionActual
    {
        get
        {
            int w = (int)SettingsService.Instance.GetFloat(KeyAncho, 0f);
            int h = (int)SettingsService.Instance.GetFloat(KeyAlto, 0f);
            return w > 0 && h > 0 ? new Vector2Int(w, h) : new Vector2Int(Screen.width, Screen.height);
        }
    }

    public static FullScreenMode ModoActual
    {
        get
        {
            float guardado = SettingsService.Instance.GetFloat(KeyModo, -1f);
            return guardado < 0f ? Screen.fullScreenMode : (FullScreenMode)(int)guardado;
        }
    }

    // Cambia y guarda. En el editor solo guarda: la ventana de Game no se toca.
    public static void Poner(Vector2Int resolucion, FullScreenMode modo)
    {
        SettingsService.Instance.SetFloat(KeyAncho, resolucion.x);
        SettingsService.Instance.SetFloat(KeyAlto, resolucion.y);
        SettingsService.Instance.SetFloat(KeyModo, (int)modo);
        Aplicar(resolucion, modo);
    }

    // Al arrancar: lo que el jugador haya elegido la ultima vez, si eligio algo.
    public static void AplicarGuardada()
    {
        if (SettingsService.Instance.GetFloat(KeyAncho, 0f) <= 0f) return;
        Aplicar(ResolucionActual, ModoActual);
    }

    public static void Restaurar()
    {
        SettingsService.Instance.Delete(KeyAncho);
        SettingsService.Instance.Delete(KeyAlto);
        SettingsService.Instance.Delete(KeyModo);
        Resolution nativa = Screen.currentResolution;
        Aplicar(new Vector2Int(nativa.width, nativa.height), FullScreenMode.FullScreenWindow);
    }

    private static void Aplicar(Vector2Int resolucion, FullScreenMode modo)
    {
        if (Application.isEditor) return;
        Screen.SetResolution(resolucion.x, resolucion.y, modo);
    }
}
