using System.Collections.Generic;
using UnityEngine;

// Cajas de texto y ruleta de premio de los NPCs del casino.
//
// Hablan por el DialogueController del mapa, como el resto de los NPCs (nombre y
// retrato arriba de la caja). Si una escena no lo tuviera, caen al MessageBox
// del sistema de menus: peor presentado, pero el jugador ve la respuesta.
public static class CasinoDialogo
{
    public static void Decir(DialogueController dialogo, string nombre, Sprite retrato,
                             string[] lineas, System.Action alCerrar = null)
    {
        var limpias = new List<string>();
        if (lineas != null)
            foreach (string l in lineas)
                if (!string.IsNullOrEmpty(l)) limpias.Add(l);

        if (limpias.Count == 0) { alCerrar?.Invoke(); return; }

        if (dialogo != null)
        {
            dialogo.StartMessage(nombre, retrato, limpias.ToArray(), alCerrar);
            return;
        }

        MenuManager menus = MenuManager.Instance;
        if (menus == null)
        {
            foreach (string l in limpias) Debug.Log($"[{nombre}] {l}");
            alCerrar?.Invoke();
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        EnCadena(menus, limpias, 0, alCerrar);
    }

    private static void EnCadena(MenuManager menus, List<string> lineas, int i, System.Action alCerrar)
    {
        if (i >= lineas.Count) { alCerrar?.Invoke(); return; }

        menus.Open(MenuScreenId.MessageBox, new MessageBoxRequest { Message = lineas[i] },
            _ => EnCadena(menus, lineas, i + 1, alCerrar));
    }

    public static void Preguntar(DialogueController dialogo, string nombre, Sprite retrato,
                                 string pregunta, string si, string no, System.Action<bool> respuesta)
    {
        if (dialogo != null)
        {
            dialogo.StartConfirm(nombre, retrato, pregunta, si, no, respuesta);
            return;
        }

        MenuManager menus = MenuManager.Instance;
        if (menus == null) { respuesta?.Invoke(false); return; }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.MessageBox,
            new MessageBoxRequest { Message = pregunta, IsQuestion = true, YesLabel = si, NoLabel = no },
            resultado => respuesta?.Invoke(resultado is MessageBoxResult r && r.Confirmed));
    }

    // La ruleta como premio de un combate: un solo giro y sin poder salir antes.
    // Al cerrarse guarda la partida, porque el premio ya se entrego.
    public static void AbrirRuletaDePremio(CasinoPrizeTable tabla, System.Action alCerrar = null)
    {
        MenuManager menus = MenuManager.Instance;
        if (menus == null || tabla == null || tabla.premios == null || tabla.premios.Length == 0)
        {
            Debug.LogWarning("[Casino] No se pudo abrir la ruleta de premio (sin MenuManager o sin premios).");
            alCerrar?.Invoke();
            return;
        }

        menus.SetContext(MenuContextKind.Overworld);
        menus.Open(MenuScreenId.PrizeWheel,
            new PrizeWheelRequest { Prizes = tabla.premios, RecompensaUnica = true },
            _ =>
            {
                Guardar();
                alCerrar?.Invoke();
            });
    }

    // Los bancos de prueba lo apagan: si no, una pelea simulada guardaria la
    // partida DEMO encima del save de verdad.
    public static bool GuardadoAutomatico = true;

    public static void Guardar()
    {
        if (!GuardadoAutomatico) return;
        if (GameManager.Instance != null) GameManager.Instance.SaveGame();
    }
}
