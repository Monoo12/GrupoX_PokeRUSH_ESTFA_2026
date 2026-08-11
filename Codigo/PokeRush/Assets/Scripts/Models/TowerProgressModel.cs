// Lectura ordenada del progreso de la torre 1 (Ciberseguridad).
//
// No guarda estado propio: es una fachada de solo lectura sobre StoryModel.
// La fuente de verdad son los StoryFlag, que SaveService ya persiste solos.
// Tener esto aparte evita que medio proyecto ande preguntando
// story.HasFlag(StoryFlag.Torre1_FirewallNodoB) suelto por ahi.
//
// Regla del GDD que se refleja aca: cortar subsistemas nunca bloquea contenido,
// solo cambia la dificultad del boss. Por eso todo lo que expone son consultas,
// no permisos.
public class TowerProgressModel
{
    private readonly StoryModel _story;

    public TowerProgressModel(StoryModel story)
    {
        _story = story;
    }

    // Camaras: cortadas = la Prediccion de Nestor se anuncia un turno antes
    public bool CamarasCortadas => Tiene(StoryFlag.Torre1_CamarasCortadas);

    // Firewall: es el unico subsistema PARCIAL. Cada nodo achica el escudo,
    // pero solo los tres completos cuentan para la condicion de Melmetal.
    public int FirewallNodos =>
        (Tiene(StoryFlag.Torre1_FirewallNodoA) ? 1 : 0) +
        (Tiene(StoryFlag.Torre1_FirewallNodoB) ? 1 : 0) +
        (Tiene(StoryFlag.Torre1_FirewallNodoC) ? 1 : 0);

    public const int FirewallNodosTotales = 3;
    public bool FirewallCompleto => FirewallNodos >= FirewallNodosTotales;

    // Rastreo: cortado = podes cambiar de Poken durante el combate
    public bool RastreoCortado => Tiene(StoryFlag.Torre1_RastreoCortado);

    // Energia auxiliar: cortada = la fase de liberacion dura la mitad.
    // No hace la pelea mas facil, hace la LIBERACION mas facil: el que quiere
    // salvar a Nestor tiene un motivo distinto al que solo quiere ganar.
    public bool EnergiaAuxiliarCortada => Tiene(StoryFlag.Torre1_EnergiaAuxiliarCortada);

    // Condicion de Melmetal: todo o nada, a proposito. Cortar dos de tres nodos
    // del firewall achica el escudo pero NO alcanza, para obligar a terminar lo
    // que se empieza.
    public bool TodosCortados =>
        CamarasCortadas && FirewallCompleto && RastreoCortado && EnergiaAuxiliarCortada;

    // Cuantos de los cuatro subsistemas estan cortados (el firewall cuenta como
    // uno solo y solo si esta completo). Sirve para el resumen en pantalla.
    public int SubsistemasCortados =>
        (CamarasCortadas ? 1 : 0) + (FirewallCompleto ? 1 : 0) +
        (RastreoCortado ? 1 : 0) + (EnergiaAuxiliarCortada ? 1 : 0);

    public const int SubsistemasTotales = 4;

    private bool Tiene(StoryFlag flag) => _story != null && _story.HasFlag(flag);
}
