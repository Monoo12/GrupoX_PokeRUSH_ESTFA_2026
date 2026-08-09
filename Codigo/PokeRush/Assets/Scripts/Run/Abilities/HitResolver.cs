using System.Collections.Generic;
using UnityEngine;

// Resuelve a quien alcanza una habilidad EN ESTE INSTANTE.
//
// Se consulta una vez por cuadro mientras dura la ventana de daño, y el que
// llama lleva la cuenta de a quien ya golpeo. Antes se llamaba una sola vez, en
// el instante exacto en que abria la ventana: el que entraba al area un cuadro
// mas tarde no recibia nada, aunque la animacion lo estuviera atravesando. En un
// juego donde todos se mueven todo el tiempo eso se siente como que los golpes
// no conectan, que es exactamente la queja.
//
// La deteccion es por volumen y no por colliders de proyectil: asi la hitbox
// queda pegada al momento de la animacion que se configuro, y es la misma forma
// que dibuja el indicador en el suelo. Que lo que se ve y lo que golpea sean la
// misma cosa es lo que hace que un ataque apuntado se sienta justo.
public static class HitResolver
{
    // Buffer reutilizado: estas consultas corren en cada cuadro de cada golpe y
    // no conviene que generen basura. Grande a proposito: la consulta barre
    // TODOS los colliders del area, incluidos el suelo y los obstaculos, asi que
    // si el buffer se llenara con decorado los rivales quedarian afuera.
    private static readonly Collider[] Buffer = new Collider[128];

    /// Devuelve los combatientes del bando contrario que estan dentro de la
    /// habilidad ahora mismo. `puntoApuntado` es hacia donde se lanzo: la
    /// posicion del atacante se lee en vivo, para que un dash barra lo que pisa.
    public static List<RunCombatant> Alcanzados(
        RunCombatant atacante, AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        var alcanzados = new List<RunCombatant>();
        if (atacante == null || habilidad == null) return alcanzados;

        Vector3 origen = atacante.transform.position;
        Vector3 direccion = Direccion(origen, puntoApuntado, atacante.transform.forward);

        switch (habilidad.apuntado)
        {
            case AimMode.ConoFrontal:
                // Una sola consulta al alcance del golpe y despues se filtra por
                // angulo. Es exacto -no aproxima con esferas- y es la misma
                // cuenta que usa el indicador para dibujar el abanico.
                Juntar(alcanzados, atacante, origen, habilidad.rango,
                       direccion, habilidad.anguloCono);
                break;

            case AimMode.SkillshotLineal:
                // El haz se aproxima con esferas a lo largo de la linea. Los
                // pasos se solapan a medio radio para que nadie pueda quedar
                // entre dos esferas.
                int pasos = Mathf.Max(2, Mathf.CeilToInt(habilidad.rango / Mathf.Max(0.05f, habilidad.radio * 0.5f)));
                for (int i = 0; i <= pasos; i++)
                {
                    Vector3 centro = origen + direccion * (habilidad.rango * i / pasos);
                    Juntar(alcanzados, atacante, centro, habilidad.radio);
                }
                break;

            case AimMode.AreaEnSuelo:
                Vector3 objetivo = origen + Vector3.ClampMagnitude(puntoApuntado - origen, habilidad.rango);
                Juntar(alcanzados, atacante, objetivo, habilidad.radio);
                break;

            case AimMode.Dash:
                // Golpea alrededor del cuerpo, en su posicion actual. Antes era
                // una esfera de medio RANGO puesta a media distancia: un bolazo
                // que cubria todo el recorrido de una y que no se parecia en
                // nada a lo dibujado. Como ahora se consulta cuadro a cuadro
                // mientras el Poken se desplaza, el barrido sale solo.
                Juntar(alcanzados, atacante, origen, habilidad.radio);
                break;

            case AimMode.AreaPropia:
                Juntar(alcanzados, atacante, origen, habilidad.radio);
                break;

            case AimMode.Propia:
                break;
        }
        return alcanzados;
    }

    /// Punto donde conviene reventar el efecto visual del impacto.
    public static Vector3 PuntoDeImpacto(
        RunCombatant atacante, AbilityDefinition habilidad, Vector3 puntoApuntado, List<RunCombatant> alcanzados)
    {
        if (alcanzados != null && alcanzados.Count > 0) return alcanzados[0].PuntoDeImpacto;

        Vector3 origen = atacante.transform.position;
        if (habilidad.apuntado == AimMode.AreaPropia || habilidad.apuntado == AimMode.Propia)
            return origen + Vector3.up * 0.5f;

        Vector3 direccion = Direccion(origen, puntoApuntado, atacante.transform.forward);

        // Al aire, el efecto va donde de verdad barrio el golpe. En el cono eso
        // es la mitad del abanico y no la punta: reventar a rango completo
        // dibujaria el impacto mas lejos de donde llega la patada.
        float distancia = habilidad.apuntado == AimMode.ConoFrontal
            ? habilidad.rango * 0.6f
            : habilidad.rango;

        if (habilidad.apuntado == AimMode.AreaEnSuelo)
            return origen + Vector3.ClampMagnitude(puntoApuntado - origen, habilidad.rango) + Vector3.up * 0.5f;

        return origen + direccion * distancia + Vector3.up * 0.5f;
    }

    private static Vector3 Direccion(Vector3 origen, Vector3 objetivo, Vector3 porDefecto)
    {
        Vector3 plana = Vector3.ProjectOnPlane(objetivo - origen, Vector3.up);
        return plana.sqrMagnitude < 0.01f ? porDefecto : plana.normalized;
    }

    // Con `angulo` en 0 no filtra por direccion y la forma es una esfera.
    private static void Juntar(List<RunCombatant> destino, RunCombatant atacante, Vector3 centro, float radio,
                               Vector3 direccion = default, float angulo = 0f)
    {
        int cantidad = Physics.OverlapSphereNonAlloc(centro, radio, Buffer, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < cantidad; i++)
        {
            var combatiente = Buffer[i].GetComponentInParent<RunCombatant>();
            if (combatiente == null) continue;
            // Solo el bando contrario, y nunca dos veces al mismo en la lista.
            if (combatiente.EsDelJugador == atacante.EsDelJugador) continue;
            if (combatiente.EstaDerrotado) continue;
            if (destino.Contains(combatiente)) continue;

            if (angulo > 0f && !DentroDelCono(centro, direccion, angulo, combatiente.transform.position))
                continue;

            destino.Add(combatiente);
        }
    }

    // El angulo se mide en el plano del suelo. Sin proyectar, un rival mas alto
    // o mas bajo -o parado en una rampa- se saldria del cono por la diferencia
    // de altura, que no es lo que el jugador ve desde arriba.
    private static bool DentroDelCono(Vector3 centro, Vector3 direccion, float angulo, Vector3 punto)
    {
        Vector3 hacia = Vector3.ProjectOnPlane(punto - centro, Vector3.up);

        // Encima del atacante: entra siempre. Normalizar un vector de largo cero
        // daria una direccion cualquiera y el golpe fallaria al pegado.
        if (hacia.sqrMagnitude < 0.0001f) return true;

        return Vector3.Angle(direccion, hacia.normalized) <= angulo * 0.5f;
    }
}
