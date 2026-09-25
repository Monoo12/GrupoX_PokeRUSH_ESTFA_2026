using UnityEngine;

// Un NPC del casino que pelea UNA sola vez.
//
// Pregunta antes, lanza el combate y, si el jugador gana:
//   1. activa su flag de vencido, que es lo que impide repetir la pelea;
//   2. si esta en la lista de la tabla de ruleta, la sortea con
//      CasinoRuletaDeNpcs (una cada dos combates ganados, hasta cuatro por
//      partida, no siempre los mismos);
//   3. guarda la partida.
//
// El cartel de victoria y la ruleta salen cuando el mapa vuelve (ver
// CasinoNpcConCombate).
[RequireComponent(typeof(Collider2D))]
public class CasinoTrainerNpc : CasinoNpcConCombate, IInteractable
{
    [Header("Rival")]
    [SerializeField] private EnemyDefinition rival;
    [Tooltip("Flag que marca que ya se le gano. Sin el, la pelea se podria repetir para siempre.")]
    [SerializeField] private StoryFlag flagVencido = StoryFlag.None;

    [Header("Ruleta de premios (opcional)")]
    [Tooltip("Tabla de la ruleta de los NPCs. Vacio = este NPC nunca la da. Aunque " +
             "este puesta, solo la puede dar si su flagVencido figura en la lista " +
             "'npcsQueLaPuedenDar' de la tabla.")]
    [SerializeField] private CasinoPrizeTable ruleta;

    [Header("Objeto clave al ganar (opcional)")]
    [Tooltip("Objeto clave que da al ganarle (piso 2: el magnate da la Tarjeta VIP). " +
             "Si ya lo tenes, no lo vuelve a dar.")]
    [SerializeField] private SpecialItemDefinition objetoAlGanar;
    [Tooltip("{0} = el objeto.")]
    [SerializeField, TextArea(2, 4)] private string mensajeObjeto = "Tomá, te la ganaste: la {0} es tuya.";

    [Header("Textos")]
    [SerializeField, TextArea(2, 4)] private string pregunta = "¿Peleamos?";
    [SerializeField] private string opcionSi = "Dale";
    [SerializeField] private string opcionNo = "Paso";
    [SerializeField, TextArea(2, 4)] private string mensajeSiRechaza = "";
    [SerializeField, TextArea(2, 4)] private string mensajeVictoria = "Me ganaste.";
    [SerializeField, TextArea(2, 4)] private string mensajeRuleta =
        "Y encima te toca la ruleta de premios. ¡Girá!";
    [SerializeField, TextArea(2, 4)] private string mensajeDerrota = "";
    [SerializeField, TextArea(2, 4)] private string mensajeYaVencido = "Ya me ganaste. Dejame tranquilo.";

    public EnemyDefinition Rival => rival;
    public StoryFlag FlagVencido => flagVencido;
    public CasinoPrizeTable Ruleta => ruleta;
    public SpecialItemDefinition ObjetoAlGanar => objetoAlGanar;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || story == null) return false;
        if (EsperandoCombate) return false;

        if (flagVencido != StoryFlag.None && story.HasFlag(flagVencido))
        {
            Decir(mensajeYaVencido);
            return true;
        }

        Preguntar(pregunta, opcionSi, opcionNo, acepto =>
        {
            if (!acepto) { Decir(mensajeSiRechaza); return; }
            LanzarCombate(player, rival, outcome => AlTerminar(outcome, story));
        });
        return true;
    }

    private void AlTerminar(BattleOutcome outcome, StoryModel story)
    {
        if (outcome == null || !outcome.PlayerVictory)
        {
            AlVolverAlMapa(() => Decir(mensajeDerrota));
            return;
        }

        bool daRuleta = false;
        if (ruleta != null && ruleta.premios != null && ruleta.premios.Length > 0)
            daRuleta = CasinoRuletaDeNpcs.Sortear(story, ruleta.npcsQueLaPuedenDar, flagVencido, Random.value);

        // El objeto va a la partida en curso (AlTerminar no recibe al jugador:
        // la firma la usa por reflexion el banco de pruebas).
        PlayerModel jugador = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        bool daObjeto = objetoAlGanar != null && jugador != null && !jugador.Inventory.HasSpecialItem(objetoAlGanar);
        if (daObjeto) jugador.Inventory.AddSpecialItem(objetoAlGanar);

        Activar(story, flagVencido);
        if (rival != null) Activar(story, rival.defeatGrantsFlag);
        CasinoDialogo.Guardar();

        Debug.Log($"[Casino] '{name}' vencido. Ruleta: {(daRuleta ? "SI" : "no")} " +
                  $"({CasinoRuletaDeNpcs.Usadas(story)}/{CasinoRuletaDeNpcs.MaximoPorPartida} usadas)" +
                  (daObjeto ? $", da {objetoAlGanar.itemName}." : "."));

        string lineaObjeto = daObjeto
            ? string.Format(mensajeObjeto, string.IsNullOrEmpty(objetoAlGanar.itemName) ? objetoAlGanar.name : objetoAlGanar.itemName)
            : null;

        AlVolverAlMapa(() =>
        {
            if (!daRuleta) { Decir(new[] { mensajeVictoria, lineaObjeto }); return; }
            Decir(new[] { mensajeVictoria, lineaObjeto, mensajeRuleta }, () => CasinoDialogo.AbrirRuletaDePremio(ruleta));
        });
    }
}
