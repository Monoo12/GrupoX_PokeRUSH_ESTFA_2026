using UnityEngine;

// La enfermera del casino.
//
// Cura al equipo por unas monedas... o fiado. Si el jugador no tiene con que
// pagar igual lo cura y le anota la deuda, SIN TOPE (playtest del 5-oct: con la
// deuda trabada se podia quedar sin peleas, sin plata y sin poder curarse, casi
// un game over). Con plata en el bolsillo primero ofrece saldar lo que se debe.
//
// La deuda tiene su peso en el ascensor: si el jugador puede pagarla, no lo
// deja subir hasta que la pague. Si no le alcanza, sube igual y la deuda queda
// anotada para cuando tenga (ver CasinoElevatorInteractable).
//
// La deuda vive en PlayerModel (se guarda con la partida), no aca: si viviera en
// este componente, cambiar de piso la borraria.
[RequireComponent(typeof(Collider2D))]
public class CasinoNurseInteractable : MonoBehaviour, IInteractable
{
    [Header("Precio")]
    [Tooltip("Lo que cuesta la curacion.")]
    [Min(0)] [SerializeField] private int costo = 100;

    [Header("Textos")]
    [SerializeField] private string nombre = "ENFERMERA";
    [SerializeField, TextArea(2, 3)] private string preguntaCobrando =
        "Te dejo el equipo como nuevo por {0} monedas. Te lo hago?";
    [SerializeField, TextArea(2, 3)] private string preguntaFiando =
        "No te alcanza... Mira, te curo igual y me lo debes. Te anoto {0}?";
    [Tooltip("{0} = el precio, {1} = lo que ya se debe.")]
    [SerializeField, TextArea(2, 3)] private string preguntaFiandoConDeuda =
        "Ya me debés {1}, pero acá nadie se queda sin curar. ¿Te anoto {0} más?";
    [SerializeField, TextArea(2, 3)] private string mensajeCurado =
        "Listo, tu equipo esta como nuevo.";
    [Tooltip("{0} = lo que se debe en total.")]
    [SerializeField, TextArea(2, 3)] private string mensajeFiadoAnotado =
        "Curado. Me debés {0} monedas en total: pagámelas cuando tengas.";
    [SerializeField, TextArea(2, 3)] private string mensajeSano =
        "Tu equipo esta perfecto, no hace falta.";
    [Tooltip("{0} = lo que se debe.")]
    [SerializeField, TextArea(2, 3)] private string mensajeSanoConDeuda =
        "Tu equipo está perfecto. Eso sí: me seguís debiendo {0}.";
    [SerializeField, TextArea(2, 3)] private string preguntaPagarDeuda =
        "Me debes {0}. Me las pagas ahora?";
    [SerializeField, TextArea(2, 3)] private string mensajeDeudaSaldada =
        "Saldado. Ahora si, suerte alla arriba.";
    [SerializeField, TextArea(2, 3)] private string mensajeDeudaParcial =
        "Me diste {0}. Todavia me debes {1}.";

    public int Costo => costo;

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        bool necesitaCurarse = player.EquipoNecesitaCuracion;

        // Con deuda y algo de plata, primero ofrece saldar; despues, si hace
        // falta, cura (cobrando lo que se pueda o fiado otra vez).
        if (player.HasDebt && player.Money > 0)
        {
            OfrecerPagar(player, () => { if (necesitaCurarse) OfrecerCurar(player); });
            return true;
        }

        if (!necesitaCurarse)
        {
            Decir(player.HasDebt ? string.Format(mensajeSanoConDeuda, player.Debt) : mensajeSano);
            return true;
        }

        OfrecerCurar(player);
        return true;
    }

    private void OfrecerCurar(PlayerModel player)
    {
        bool puedePagar = player.Money >= costo;
        string pregunta = puedePagar
            ? string.Format(preguntaCobrando, costo)
            : player.HasDebt
                ? string.Format(preguntaFiandoConDeuda, costo, player.Debt)
                : string.Format(preguntaFiando, costo);

        Preguntar(pregunta, acepto =>
        {
            if (!acepto) return;

            player.CurarEquipo();
            Sonidos.Curar();

            if (puedePagar)
            {
                player.SpendMoney(costo);
                Decir(mensajeCurado);
            }
            else
            {
                player.AddDebt(costo);
                Decir(string.Format(mensajeFiadoAnotado, player.Debt));
            }
        });
    }

    private void OfrecerPagar(PlayerModel player, System.Action despues)
    {
        int deuda = player.Debt;

        Preguntar(string.Format(preguntaPagarDeuda, deuda), acepto =>
        {
            if (!acepto) { despues?.Invoke(); return; }

            int pagado = player.PayDebt(deuda);
            Decir(player.HasDebt
                ? string.Format(mensajeDeudaParcial, pagado, player.Debt)
                : mensajeDeudaSaldada, despues);
        });
    }

    // ---------- carteles ----------

    private void Decir(string mensaje, System.Action alCerrar = null)
    {
        if (string.IsNullOrEmpty(mensaje)) { alCerrar?.Invoke(); return; }

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[{nombre}] {mensaje}"); alCerrar?.Invoke(); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = mensaje },
            _ => alCerrar?.Invoke());
    }

    private void Preguntar(string pregunta, System.Action<bool> respuesta)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null) { respuesta?.Invoke(false); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = pregunta, IsQuestion = true },
            resultado => respuesta?.Invoke(resultado is MessageBoxResult r && r.Confirmed));
    }
}
