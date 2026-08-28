using UnityEngine;

// Lo que quedo de aplicar un premio de la ruleta.
public class RouletteApplyResult
{
    // false = la casilla no se pudo aplicar tal cual (equipo lleno, sin objetos
    // que perder...). No es un error: el mensaje lo explica.
    public bool Aplicado;
    public string Mensaje;
    // Este resultado cumple el objetivo del piso.
    public bool CumpleObjetivo;
}

// Aplica un premio de la ruleta sobre la partida.
//
// Vive aparte del motor y de la pantalla porque es la parte peligrosa: toca el
// dinero, la mochila y el EQUIPO. Todo lo que entrega o saca pasa por los
// sistemas que ya existen (PlayerModel, InventoryModel), no hay una segunda
// economia ni una lista de Pokens paralela.
//
// Reglas duras, para que la ruleta no pueda romper una partida:
//   - el dinero nunca queda en negativo;
//   - un Poken nuevo se CREA desde su definicion y se agrega al equipo; nunca
//     se copia uno existente, asi no hay forma de duplicar;
//   - con el equipo lleno no se tira nada a la basura: se avisa;
//   - el danio nunca debilita a nadie (minimo 1 PS).
public static class RouletteRewardService
{
    // Tope del equipo, igual que el resto del juego.
    public const int MaxEquipo = 6;

    public static RouletteApplyResult Aplicar(RoulettePrize premio, PlayerModel jugador)
    {
        var resultado = new RouletteApplyResult();
        if (premio == null || jugador == null)
        {
            resultado.Mensaje = "No paso nada.";
            return resultado;
        }

        string detalle;
        switch (premio.tipo)
        {
            case RoulettePrizeKind.Nada:
                resultado.Aplicado = true;
                detalle = "";
                break;

            case RoulettePrizeKind.Dinero:
                jugador.AddMoney(Mathf.Max(0, premio.monto));
                resultado.Aplicado = true;
                detalle = $"{premio.monto} monedas";
                break;

            case RoulettePrizeKind.PerderDinero:
            {
                // Nunca en negativo: se pierde lo que haya.
                int aSacar = Mathf.Min(Mathf.Max(0, premio.monto), jugador.Money);
                jugador.SpendMoney(aSacar);
                resultado.Aplicado = true;
                detalle = aSacar > 0 ? $"{aSacar} monedas" : "nada, ya estabas seco";
                break;
            }

            case RoulettePrizeKind.Objeto:
                if (premio.objeto == null)
                {
                    resultado.Mensaje = "La casilla no tiene objeto configurado.";
                    return resultado;
                }
                jugador.Inventory.AddHealItem(premio.objeto, Mathf.Max(1, premio.cantidad));
                resultado.Aplicado = true;
                detalle = premio.cantidad > 1
                    ? $"{premio.objeto.itemName} x{premio.cantidad}"
                    : premio.objeto.itemName;
                break;

            case RoulettePrizeKind.PerderObjeto:
            {
                HealItemDefinition perdido = SacarUnObjeto(jugador);
                resultado.Aplicado = true;
                detalle = perdido != null ? perdido.itemName : "nada, tenias la mochila vacia";
                break;
            }

            case RoulettePrizeKind.Poken:
            {
                if (premio.poken == null)
                {
                    resultado.Mensaje = "La casilla no tiene especie configurada.";
                    return resultado;
                }

                if (jugador.Team.Count >= MaxEquipo)
                {
                    // No se descarta nada del equipo por una tirada de ruleta.
                    resultado.Mensaje = $"Te tocaba {premio.poken.pokenName}, " +
                                        "pero tenes el equipo lleno.";
                    return resultado;
                }

                jugador.Team.Add(new PokenModel(premio.poken, Mathf.Max(1, premio.nivel)));
                resultado.Aplicado = true;
                detalle = $"{premio.poken.pokenName} Nv{premio.nivel}";
                break;
            }

            case RoulettePrizeKind.DanioAlEquipo:
            {
                int golpeados = DanarEquipo(jugador, premio.monto);
                resultado.Aplicado = true;
                detalle = golpeados > 0
                    ? $"{golpeados} Poken tocados"
                    : "tu equipo aguanto";
                break;
            }

            default:
                resultado.Mensaje = "No paso nada.";
                return resultado;
        }

        resultado.CumpleObjetivo = premio.cumpleObjetivo && resultado.Aplicado;
        resultado.Mensaje = ArmarMensaje(premio, detalle);
        return resultado;
    }

    private static string ArmarMensaje(RoulettePrize premio, string detalle)
    {
        if (string.IsNullOrEmpty(premio.mensaje))
            return string.IsNullOrEmpty(detalle) ? premio.nombre : $"{premio.nombre}: {detalle}";

        return premio.mensaje.Contains("{0}")
            ? string.Format(premio.mensaje, detalle)
            : premio.mensaje;
    }

    // Saca UN objeto de curacion al azar de la mochila. Se elige entre los que
    // tienen stock para no "perder" algo que no se tenia.
    private static HealItemDefinition SacarUnObjeto(PlayerModel jugador)
    {
        var conStock = new System.Collections.Generic.List<HealItemStack>();
        foreach (HealItemStack stack in jugador.Inventory.HealItems)
            if (stack != null && stack.Quantity > 0) conStock.Add(stack);

        if (conStock.Count == 0) return null;

        HealItemStack elegido = conStock[Random.Range(0, conStock.Count)];
        HealItemDefinition definicion = elegido.Definition;
        jugador.Inventory.ConsumeHealItem(definicion, 1);
        return definicion;
    }

    // Quita un porcentaje de los PS maximos a cada Poken vivo, sin debilitar a
    // ninguno: quedar sin equipo por una ruleta seria un callejon sin salida.
    private static int DanarEquipo(PlayerModel jugador, int porcentaje)
    {
        int golpeados = 0;
        int pct = Mathf.Clamp(porcentaje, 0, 100);
        if (pct <= 0) return 0;

        foreach (PokenModel poken in jugador.Team)
        {
            if (poken.IsFainted) continue;

            int danio = Mathf.Max(1, poken.MaxHP * pct / 100);
            int nuevo = Mathf.Max(1, poken.CurrentHP - danio);
            if (nuevo == poken.CurrentHP) continue;

            poken.SetCurrentHP(nuevo);
            golpeados++;
        }

        return golpeados;
    }
}
