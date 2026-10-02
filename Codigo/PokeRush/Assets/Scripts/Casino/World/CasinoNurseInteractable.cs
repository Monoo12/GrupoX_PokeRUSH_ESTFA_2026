using UnityEngine;

// La enfermera del casino.
//
// Cura al equipo por una moneda... o fiado. Si el jugador no tiene con que
// pagar igual lo cura, pero le anota la deuda, y con deuda encima el ascensor no
// lo deja subir de piso. Es la red de seguridad del casino: nadie queda trabado
// sin poder curarse, pero perder toda la plata tiene consecuencias.
//
// La deuda vive en PlayerModel (se guarda con la partida), no aca: si viviera en
// este componente, cambiar de piso la borraria.
[RequireComponent(typeof(Collider2D))]
public class CasinoNurseInteractable : MonoBehaviour, IInteractable
{
    [Header("Precio")]
    [Tooltip("Lo que cuesta la curacion.")]
    [Min(0)] [SerializeField] private int costo = 30;

    [Header("Textos")]
    [SerializeField] private string nombre = "ENFERMERA";
    [SerializeField, TextArea(2, 3)] private string preguntaCobrando =
        "Te dejo el equipo como nuevo por {0} monedas. Te lo hago?";
    [SerializeField, TextArea(2, 3)] private string preguntaFiando =
        "No te alcanza... Mira, te curo igual y me lo debes. Te anoto {0}?";
    [SerializeField, TextArea(2, 3)] private string mensajeCurado =
        "Listo, tu equipo esta como nuevo.";
    [SerializeField, TextArea(2, 3)] private string mensajeFiado =
        "Curado. Me debes {0} monedas: sin saldarlas no vas a poder subir de piso.";
    [SerializeField, TextArea(2, 3)] private string mensajeSano =
        "Tu equipo esta perfecto, no hace falta.";
    [SerializeField, TextArea(2, 3)] private string preguntaPagarDeuda =
        "Me debes {0}. Me las pagas ahora?";
    [SerializeField, TextArea(2, 3)] private string mensajeDeudaSaldada =
        "Saldado. Ahora si, suerte alla arriba.";
    [SerializeField, TextArea(2, 3)] private string mensajeDeudaParcial =
        "Me diste {0}. Todavia me debes {1}.";
    [SerializeField, TextArea(2, 3)] private string mensajeSinNadaParaPagar =
        "No tenes con que. Anda a ganar algo y volve.";

    public bool Interact(PlayerModel player, StoryModel story)
    {
        if (player == null) return false;

        // Lo primero es la deuda: mientras exista, la enfermera cobra antes de
        // volver a fiar. Si no, se podria acumular deuda infinita gratis.
        if (player.HasDebt) { OfrecerPagar(player); return true; }

        if (!player.EquipoNecesitaCuracion) { Decir(mensajeSano); return true; }

        bool puedePagar = player.Money >= costo;
        string pregunta = string.Format(puedePagar ? preguntaCobrando : preguntaFiando, costo);

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
                Decir(string.Format(mensajeFiado, costo));
            }
        });

        return true;
    }

    private void OfrecerPagar(PlayerModel player)
    {
        int deuda = player.Debt;

        if (player.Money <= 0)
        {
            Decir(mensajeSinNadaParaPagar);
            return;
        }

        Preguntar(string.Format(preguntaPagarDeuda, deuda), acepto =>
        {
            if (!acepto) return;

            int pagado = player.PayDebt(deuda);
            Decir(player.HasDebt
                ? string.Format(mensajeDeudaParcial, pagado, player.Debt)
                : mensajeDeudaSaldada);
        });
    }

    // ---------- carteles ----------

    private void Decir(string mensaje)
    {
        if (string.IsNullOrEmpty(mensaje)) return;

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { Debug.Log($"[{nombre}] {mensaje}"); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = mensaje });
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
