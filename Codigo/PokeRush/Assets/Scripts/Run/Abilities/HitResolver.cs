using System.Collections.Generic;
using UnityEngine;

// Resuelve a quien alcanza una habilidad.
//
// Se usa deteccion por volumen y no colliders de proyectil: en un juego de
// accion con ventanas de daño cortas, un OverlapSphere en el instante justo es
// mas predecible que un trigger que puede perderse entre cuadros si el objetivo
// se mueve rapido. Ademas deja el golpe alineado exactamente con el momento de
// la animacion que se configuro.
public static class HitResolver
{
    // Buffer reutilizado: estas consultas corren en cada golpe y no conviene
    // que generen basura.
    private static readonly Collider[] Buffer = new Collider[32];

    /// Devuelve los combatientes del bando contrario alcanzados por la habilidad.
    public static List<RunCombatant> Alcanzados(
        RunCombatant atacante, AbilityDefinition habilidad, Vector3 puntoApuntado)
    {
        var alcanzados = new List<RunCombatant>();
        if (atacante == null || habilidad == null) return alcanzados;

        Vector3 origen = atacante.transform.position;
        Vector3 direccion = Direccion(origen, puntoApuntado, atacante.transform.forward);

        switch (habilidad.apuntado)
        {
            case AimMode.SkillshotLineal:
                // El haz se aproxima con varias esferas a lo largo de la linea.
                // Es mas barato que un CapsuleCast y evita que un objetivo justo
                // en el borde quede afuera por un margen minimo.
                int pasos = Mathf.Max(2, Mathf.CeilToInt(habilidad.rango / habilidad.radio));
                for (int i = 1; i <= pasos; i++)
                {
                    Vector3 centro = origen + direccion * (habilidad.rango * i / pasos);
                    Juntar(alcanzados, atacante, centro, habilidad.radio);
                }
                break;

            case AimMode.AreaEnSuelo:
                Vector3 objetivo = Vector3.ClampMagnitude(puntoApuntado - origen, habilidad.rango) + origen;
                Juntar(alcanzados, atacante, objetivo, habilidad.radio);
                break;

            case AimMode.Dash:
                Juntar(alcanzados, atacante, origen + direccion * habilidad.rango * 0.5f,
                       habilidad.rango * 0.5f);
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
        if (alcanzados.Count > 0) return alcanzados[0].PuntoDeImpacto;

        Vector3 origen = atacante.transform.position;
        if (habilidad.apuntado == AimMode.AreaPropia || habilidad.apuntado == AimMode.Propia)
            return origen + Vector3.up * 0.5f;

        Vector3 direccion = Direccion(origen, puntoApuntado, atacante.transform.forward);
        return origen + direccion * habilidad.rango + Vector3.up * 0.5f;
    }

    private static Vector3 Direccion(Vector3 origen, Vector3 objetivo, Vector3 porDefecto)
    {
        Vector3 plana = Vector3.ProjectOnPlane(objetivo - origen, Vector3.up);
        return plana.sqrMagnitude < 0.01f ? porDefecto : plana.normalized;
    }

    private static void Juntar(List<RunCombatant> destino, RunCombatant atacante, Vector3 centro, float radio)
    {
        int cantidad = Physics.OverlapSphereNonAlloc(centro, radio, Buffer, ~0, QueryTriggerInteraction.Collide);

        for (int i = 0; i < cantidad; i++)
        {
            var combatiente = Buffer[i].GetComponentInParent<RunCombatant>();
            if (combatiente == null) continue;
            // Solo el bando contrario, y nunca dos veces al mismo en el mismo golpe.
            if (combatiente.EsDelJugador == atacante.EsDelJugador) continue;
            if (combatiente.EstaDerrotado) continue;
            if (destino.Contains(combatiente)) continue;

            destino.Add(combatiente);
        }
    }
}
