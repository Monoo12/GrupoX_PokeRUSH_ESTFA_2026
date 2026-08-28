using System.Collections;
using UnityEngine;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Abre cada pantalla de menu y saca una captura, para poder revisar la UI sin
// tener que jugar hasta ahi. Las capturas van a Capturas/Menus/ en la raiz del
// proyecto de Unity.
public class MenuScreenshotTaker : MonoBehaviour
{
    public string carpeta = "Capturas/Menus";

    private void Awake()
    {
        // Con la ventana de Unity en segundo plano el player loop se congela y
        // no se dibuja ni un frame.
        Application.runInBackground = true;
    }

    private void Start() => StartCoroutine(Capturar());

    private IEnumerator Capturar()
    {
        yield return null;
        yield return null;

        PlayerModel player = GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
        if (player == null) yield break;

        // Datos suficientes para que las pantallas no salgan vacias.
        var especies = Resources.LoadAll<PokenDefinition>("Definitions/Pokens");
        for (int i = 0; player.Team.Count < 4 && i < especies.Length; i++)
            player.Team.Add(new PokenModel(especies[i], 14 + i));

        foreach (HealItemDefinition item in Resources.LoadAll<HealItemDefinition>("Definitions/Items"))
            player.Inventory.AddHealItem(item, Random.Range(1, 9));

        player.Team[1].TakeDamage(player.Team[1].MaxHP / 2);
        player.Team[2].SetStatus(StatusEffect.Poisoned);

        System.IO.Directory.CreateDirectory(carpeta);
        MenuManager menus = MenuManager.Instance;

        menus.Open(MenuScreenId.Pause);
        yield return Foto("1-pausa");

        // La tragaperras del casino: se abre con plata suficiente para que el
        // visor de credito muestre algo.
        if (player.Money < 500) player.AddMoney(500 - player.Money);
        var maquina = Resources.Load<SlotMachineDefinition>("Definitions/Casino/MaquinaBasica");
        if (maquina != null)
        {
            menus.Open(MenuScreenId.Slots, new SlotsScreenRequest { Maquina = maquina });
            yield return Foto("0-slots");
            menus.Top.HandleAction(GameAction.Confirm);
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Foto("0-slots-resultado");
            menus.Close(menus.Top);
        }

        var mesa = Resources.Load<RouletteTableDefinition>("Definitions/Casino/MesaDeRuleta");
        if (mesa != null)
        {
            menus.Open(MenuScreenId.Roulette, new RouletteScreenRequest { Mesa = mesa });
            yield return Foto("0-ruleta");
            menus.Close(menus.Top);
        }

        menus.Open(MenuScreenId.Bag, new BagScreenRequest { Pocket = ItemPocket.Medicine });
        yield return Foto("2-mochila");
        menus.Close(menus.Top);

        menus.Open(MenuScreenId.Party, new PartyScreenRequest { Mode = PartyScreenMode.Browse });
        yield return Foto("3-equipo");

        menus.Open(MenuScreenId.Summary, new SummaryRequest { Team = player.Team, StartIndex = 0 });
        yield return Foto("4-resumen-info");

        menus.Top.HandleAction(GameAction.Right);
        yield return Foto("5-resumen-notas");
        menus.Top.HandleAction(GameAction.Right);
        yield return Foto("6-resumen-stats");
        menus.Top.HandleAction(GameAction.Right);
        yield return Foto("7-resumen-movimientos");
        menus.Close(menus.Top);
        menus.Close(menus.Top);

        menus.Open(MenuScreenId.Options);
        yield return Foto("8-opciones");
        menus.Close(menus.Top);

        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest
        {
            Message = "¿Querés guardar la partida?",
            IsQuestion = true
        });
        yield return Foto("9-mensaje");

        menus.CloseAll();
        Debug.Log("[Capturas] Listo: " + carpeta);

#if UNITY_EDITOR
        yield return null;
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private IEnumerator Foto(string nombre)
    {
        // Dos frames para que el LayoutGroup ya haya acomodado todo.
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot($"{carpeta}/{nombre}.png");
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
    }
}
