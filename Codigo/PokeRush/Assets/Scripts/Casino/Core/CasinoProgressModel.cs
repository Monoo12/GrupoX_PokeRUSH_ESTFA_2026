// Lectura ordenada del progreso del casino, igual que TowerProgressModel lo es
// de la torre 1.
//
// No guarda estado propio: es una fachada de solo lectura sobre StoryModel, que
// SaveService ya persiste. Tener esto aparte evita que los ascensores, la UI y
// los minijuegos anden preguntando story.HasFlag(...) sueltos por ahi, cada uno
// con su propia idea de que significa "superar un piso".
public class CasinoProgressModel
{
    private readonly StoryModel _story;

    public CasinoProgressModel(StoryModel story)
    {
        _story = story;
    }

    // Piso 1 (tragaperras): el guardia del ascensor da el pase al pagarle o al
    // ganarle. El requisito real vive en el ascensor, que es quien lo puede
    // configurar; aca solo se lee si ya paso.
    public bool PaseDelPiso1 => Tiene(StoryFlag.Casino_Piso1_PaseAscensor);
    public bool GuardiaVencido => Tiene(StoryFlag.Casino_GuardiaVencido);
    public bool Piso1Superado => Tiene(StoryFlag.Casino_Piso1_Superado);

    // La ruleta de premios que dan los NPCs al ganarles (una cada dos combates, hasta cuatro).
    public int RuletasDeNpcUsadas => CasinoRuletaDeNpcs.Usadas(_story);

    // Piso 2 (ruleta): el objetivo lo marca la propia ruleta al cumplirse.
    public bool ObjetivoDeRuletaCumplido => Tiene(StoryFlag.Casino_Ruleta_ObjetivoCumplido);
    public bool Piso2Superado => Tiene(StoryFlag.Casino_Piso2_Superado);

    // Piso 3 (blackjack): hay que ganar una partida apostando un Poken.
    public bool BlackjackGanado => Tiene(StoryFlag.Casino_Blackjack_Ganado);
    public bool Piso3Superado => Tiene(StoryFlag.Casino_Piso3_Superado);

    // Acceso al boss del ultimo piso.
    public bool BossDesbloqueado => Piso3Superado;

    // Ultimo piso: el jefe del casino, al final del pasillo de guardias.
    public bool JefeFinalVencido => Tiene(StoryFlag.Casino_JefeFinalVencido);
    public bool CasinoCompletado => Tiene(StoryFlag.Casino_Completado);

    // Cuantos pisos quedaron atras, para carteles y para el resumen.
    public int PisosSuperados =>
        (Piso1Superado ? 1 : 0) + (Piso2Superado ? 1 : 0) + (Piso3Superado ? 1 : 0);

    public const int PisosTotales = 3;

    private bool Tiene(StoryFlag flag) => _story != null && _story.HasFlag(flag);
}
