using UnityEngine;

// El guardia del ascensor del piso 1.
//
// Para dejarte subir pide una de dos cosas: que le pagues o que le ganes un
// combate. Cualquiera de las dos activa el pase (flagPase), que es lo que mira el
// CasinoElevatorInteractable del piso. Ganarle ademas da su ruleta: sale un
// Pokemon si o si, y los pesos de la tabla deciden si es uno mejor o peor.
//
// Flujo de la charla (el DialogueController solo tiene preguntas de dos
// opciones, asi que se encadenan):
//   - con el pase ya conseguido: "adelante";
//   - con plata suficiente: "pagas?" -> si: cobra y da el pase
//                                    -> no: "entonces peleamos?";
//   - sin plata: directamente "entonces peleamos?".
[RequireComponent(typeof(Collider2D))]
public class CasinoElevatorGuard : CasinoNpcConCombate, IInteractable
{
    [Header("Pase")]
    [Tooltip("Flag que deja usar el ascensor. El ascensor del piso lo pide con requisito = Flag.")]
    [SerializeField] private StoryFlag flagPase = StoryFlag.Casino_Piso1_PaseAscensor;
    [Min(0)] [SerializeField] private int precio = 500;
    [Tooltip("Si esta puesto, el guardia no cobra: pide ver este objeto clave " +
             "(piso 2: la Tarjeta VIP). Sin el objeto, igual se le puede ganar.")]
    [SerializeField] private SpecialItemDefinition objetoRequerido;

    [Header("Combate")]
    [SerializeField] private EnemyDefinition rival;
    [SerializeField] private StoryFlag flagVencido = StoryFlag.Casino_GuardiaVencido;
    [Tooltip("La ruleta que da al ganarle. Tiene que tener SOLO Pokemon: el pedido es que salga uno si o si.")]
    [SerializeField] private CasinoPrizeTable ruletaDePokemon;

    [Header("Textos")]
    [Tooltip("{0} = el precio.")]
    [SerializeField, TextArea(2, 4)] private string preguntaPagar =
        "Para subir se paga: {0} monedas. ¿Me las das?";
    [Tooltip("{0} = el precio.")]
    [SerializeField] private string opcionPagar = "Pagar {0}";
    [SerializeField] private string opcionNoPagar = "No";
    [Tooltip("{0} = el precio, {1} = lo que tiene el jugador.")]
    [SerializeField, TextArea(2, 4)] private string preguntaPelearSinPlata =
        "Para subir son {0} monedas y tenés {1}. Si no pagás, ganame una pelea. ¿Te animás?";
    [SerializeField, TextArea(2, 4)] private string preguntaPelear =
        "Entonces ganame una pelea y te dejo pasar gratis. ¿Te animás?";
    [SerializeField] private string opcionPelear = "Pelear";
    [SerializeField] private string opcionNoPelear = "Todavía no";
    [SerializeField, TextArea(2, 4)] private string mensajeRechazo =
        "Sin pagar o ganarme, de acá no subís.";
    [SerializeField, TextArea(2, 4)] private string mensajePagado =
        "Trato hecho. El ascensor es todo tuyo.";
    [SerializeField, TextArea(2, 4)] private string mensajeVictoria =
        "Bien jugado. Pasá... y llevate un premio de la casa.";
    [SerializeField, TextArea(2, 4)] private string mensajeRuleta =
        "Girá la ruleta: lo que salga, es tuyo.";
    [SerializeField, TextArea(2, 4)] private string mensajeDerrota =
        "Así no subís. Volvé cuando quieras, o pagame.";
    [SerializeField, TextArea(2, 4)] private string mensajeYaTienePase =
        "Ya tenés el pase. Adelante.";

    [Header("Textos con objeto (objetoRequerido puesto)")]
    [Tooltip("{0} = el objeto.")]
    [SerializeField, TextArea(2, 4)] private string mensajeMuestraObjeto =
        "¿La {0}? Pasá, pasá. El ascensor es todo tuyo.";
    [Tooltip("{0} = el objeto.")]
    [SerializeField, TextArea(2, 4)] private string preguntaPelearSinObjeto =
        "Arriba solo suben los que tienen la {0}. Si no la tenés, ganame una pelea. ¿Te animás?";
    [Tooltip("{0} = el objeto.")]
    [SerializeField, TextArea(2, 4)] private string mensajeRechazoSinObjeto =
        "Sin la {0} no subís. Dicen que se consigue en este mismo piso.";

    public StoryFlag FlagPase => flagPase;
    public SpecialItemDefinition ObjetoRequerido => objetoRequerido;

    private string NombreDelObjeto => objetoRequerido == null ? ""
        : string.IsNullOrEmpty(objetoRequerido.itemName) ? objetoRequerido.name : objetoRequerido.itemName;
    public StoryFlag FlagVencido => flagVencido;
    public int Precio => precio;
    public EnemyDefinition Rival => rival;
    public CasinoPrizeTable RuletaDePokemon => ruletaDePokemon;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null || story == null) return false;
        if (EsperandoCombate) return false;

        if (story.HasFlag(flagPase))
        {
            Decir(mensajeYaTienePase);
            return true;
        }

        // Guardia de objeto: con el objeto pasa, sin el ofrece la pelea.
        if (objetoRequerido != null)
        {
            if (player.Inventory.HasSpecialItem(objetoRequerido))
            {
                Activar(story, flagPase);
                CasinoDialogo.Guardar();
                Decir(string.Format(mensajeMuestraObjeto, NombreDelObjeto));
            }
            else
            {
                OfrecerPelea(player, story, string.Format(preguntaPelearSinObjeto, NombreDelObjeto));
            }
            return true;
        }

        if (player.Money >= precio)
        {
            Preguntar(string.Format(preguntaPagar, precio), string.Format(opcionPagar, precio), opcionNoPagar,
                pagar =>
                {
                    if (pagar) Pagar(player, story);
                    else OfrecerPelea(player, story, preguntaPelear);
                });
        }
        else
        {
            OfrecerPelea(player, story, string.Format(preguntaPelearSinPlata, precio, player.Money));
        }

        return true;
    }

    private void Pagar(PlayerModel player, StoryModel story)
    {
        // Se vuelve a mirar: entre la pregunta y la respuesta no deberia cambiar,
        // pero cobrar sin tener dejaria el saldo en negativo.
        if (player.Money < precio)
        {
            OfrecerPelea(player, story, string.Format(preguntaPelearSinPlata, precio, player.Money));
            return;
        }

        player.SpendMoney(precio);
        Activar(story, flagPase);
        CasinoDialogo.Guardar();
        Decir(mensajePagado);
    }

    private void OfrecerPelea(PlayerModel player, StoryModel story, string pregunta)
    {
        Preguntar(pregunta, opcionPelear, opcionNoPelear, pelear =>
        {
            if (!pelear)
            {
                Decir(objetoRequerido != null
                    ? string.Format(mensajeRechazoSinObjeto, NombreDelObjeto)
                    : mensajeRechazo);
                return;
            }
            LanzarCombate(player, rival, outcome => AlTerminar(outcome, story));
        });
    }

    private void AlTerminar(BattleOutcome outcome, StoryModel story)
    {
        if (outcome == null || !outcome.PlayerVictory)
        {
            AlVolverAlMapa(() => Decir(mensajeDerrota));
            return;
        }

        // La ruleta sale una sola vez en la partida, aunque hubiera alguna
        // forma de volver a pelear.
        bool daRuleta = !story.HasFlag(flagVencido) && ruletaDePokemon != null;

        Activar(story, flagVencido);
        Activar(story, flagPase);
        if (rival != null) Activar(story, rival.defeatGrantsFlag);
        CasinoDialogo.Guardar();

        AlVolverAlMapa(() =>
        {
            if (!daRuleta) { Decir(mensajeVictoria); return; }
            Decir(new[] { mensajeVictoria, mensajeRuleta }, () => CasinoDialogo.AbrirRuletaDePremio(ruletaDePokemon));
        });
    }
}
