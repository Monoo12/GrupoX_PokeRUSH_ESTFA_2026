using UnityEngine;
using UnityEngine.SceneManagement;

// Los pasos de los que caminan por el mapa.
//
// Suenan cuando el PIE APOYA, no por tile ni por tiempo: las hojas de
// personaje tienen cuatro cuadros por direccion (quieto, pie izquierdo,
// quieto, pie derecho) y los llaman PlayerView y NpcQueCamina al pasar a los
// cuadros 1 y 3. Asi el sonido va pegado a lo que se ve, y al correr (la
// animacion va mas rapido) los pasos se aceleran solos.
//
// La superficie sale de la escena (tabla 'escenas' del CatalogoDeAudio): el
// Data Center es metal, el casino alfombra... Cada superficie tiene varias
// variantes cortadas de la grabacion; nunca suena la misma dos veces seguidas
// y cada una con un tono apenas distinto, para que no se note la repeticion.
public static class Pasos
{
    private const float DistanciaAudible = 9f;

    private static string _escena;
    private static CatalogoDeAudio.Superficie _superficie;
    private static int _ultimo = -1;

    // Para las pruebas: cuantos pasos sonaron y cual fue el ultimo.
    public static int Sonados { get; private set; }
    public static string UltimoPaso { get; private set; }

    // El jugador apoyo un pie.
    public static void DelJugador(bool corriendo) => Sonar(1f, corriendo);

    // Un NPC apoyo un pie: mas bajo, y menos cuanto mas lejos de la camara.
    public static void DeNpc(Vector3 posicion)
    {
        CatalogoDeAudio c = CatalogoDeAudio.Actual;
        if (c == null) return;

        Camera cam = Camera.main;
        float cerca = 1f;
        if (cam != null)
        {
            Vector2 d = (Vector2)(posicion - cam.transform.position);
            cerca = Mathf.Clamp01(1f - d.magnitude / DistanciaAudible);
        }
        if (cerca <= 0.05f) return;
        Sonar(c.volumenDePasosDeNpc * cerca, corriendo: false);
    }

    private static void Sonar(float factor, bool corriendo)
    {
        CatalogoDeAudio c = CatalogoDeAudio.Actual;
        if (c == null) return;

        CatalogoDeAudio.Superficie superficie = SuperficieActual(c);
        if (superficie == null || superficie.pasos == null || superficie.pasos.Length == 0) return;

        int n = superficie.pasos.Length;
        int i = Random.Range(0, n);
        if (n > 1 && i == _ultimo) i = (i + 1 + Random.Range(0, n - 1)) % n;
        _ultimo = i;

        Sonido paso = superficie.pasos[i];
        if (paso == null || !paso.Hay) return;

        float volumen = c.volumenDePasos * paso.Volumen * factor * (corriendo ? 1.12f : 1f);
        AudioService.Instance.PlaySFX(paso.clip, volumen, Random.Range(0.94f, 1.06f));
        Sonados++;
        UltimoPaso = paso.clip.name;
    }

    private static CatalogoDeAudio.Superficie SuperficieActual(CatalogoDeAudio c)
    {
        string escena = SceneManager.GetActiveScene().name;
        if (escena == _escena && _superficie != null) return _superficie;

        _escena = escena;
        CatalogoDeAudio.MusicaDeUnaEscena entrada = c.DeLaEscena(escena);
        string nombre = entrada != null && !string.IsNullOrEmpty(entrada.superficie)
            ? entrada.superficie
            : c.superficiePorDefecto;
        _superficie = c.SuperficieLlamada(nombre);
        return _superficie;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        _escena = null;
        _superficie = null;
        _ultimo = -1;
        Sonados = 0;
        UltimoPaso = null;
    }
}
