using System.Collections.Generic;
using UnityEngine;

// Entrega lo que dice un premio de la ruleta y devuelve el texto del cartel.
//
// Vive aparte de la pantalla por dos razones: se puede probar sin UI
// (CasinoSmokeTests), y la ruleta y quien la abre no pueden tener dos ideas
// distintas de que significa "ganar un Pokemon".
//
// Un premio puede combinar cosas (un objeto y plata, por ejemplo): se entrega
// todo lo que tenga cargado.
public static class PrizeGranter
{
    // Tope clasico de 6: es lo que dibuja el HUD de combate.
    public const int EquipoMaximo = PlayerModel.EquipoMaximo;

    public static string Entregar(Prize premio, PlayerModel jugador)
    {
        if (premio == null) return "¡Has obtenido un premio!";
        if (jugador == null) return premio.BuildMessage();

        var partes = new List<string>();
        // Hay avisos que el texto a mano no puede tapar: el jugador tiene que
        // saber adonde fue su Pokemon, o por que le dieron plata en su lugar.
        bool avisoObligado = false;

        if (premio.poken != null)
        {
            string nombre = string.IsNullOrEmpty(premio.poken.pokenName)
                ? premio.poken.name
                : premio.poken.pokenName;
            if (premio.shiny) nombre += " shiny";

            var nuevo = new PokenModel(premio.poken, Mathf.Max(1, premio.pokenLevel));
            if (premio.shiny) nuevo.SetShiny(true);

            PokenRecibido recibido = jugador.RecibirPokemon(nuevo);
            switch (recibido.Destino)
            {
                case DestinoDePokemon.Equipo:
                    partes.Add($"¡{nombre} se unió a tu equipo!");
                    break;

                case DestinoDePokemon.Pc:
                    partes.Add($"¡Salió {nombre}! Tu equipo está lleno: lo mandaste al PC " +
                               $"({PokenStorageModel.NombreDeCaja(recibido.Caja)}).");
                    avisoObligado = true;
                    break;

                default:
                    // Ni en el equipo ni en el PC: se paga en plata, asi el giro
                    // no se siente perdido.
                    int compensacion = Mathf.Max(0, premio.moneyIfTeamFull);
                    if (compensacion > 0) jugador.AddMoney(compensacion);
                    partes.Add($"¡Salió {nombre}! Tu equipo y el PC están llenos: te llevás {compensacion} monedas en su lugar.");
                    avisoObligado = true;
                    break;
            }
        }

        if (premio.specialItem != null)
        {
            string nombre = string.IsNullOrEmpty(premio.specialItem.itemName)
                ? premio.prizeName
                : premio.specialItem.itemName;

            if (jugador.Inventory.HasSpecialItem(premio.specialItem))
            {
                // Un objeto clave repetido no sirve de nada: se cambia por plata.
                int compensacion = Mathf.Max(0, premio.moneyIfOwned);
                if (compensacion > 0) jugador.AddMoney(compensacion);
                partes.Add($"Salió {nombre}, que ya tenías: te llevás {compensacion} monedas en su lugar.");
                avisoObligado = true;
            }
            else
            {
                jugador.Inventory.AddSpecialItem(premio.specialItem);
                if (premio.poken == null) partes.Add(premio.BuildMessage());
            }
        }

        if (premio.item != null && premio.amount > 0)
        {
            jugador.Inventory.AddHealItem(premio.item, premio.amount);
            if (premio.poken == null && premio.specialItem == null) partes.Add(premio.BuildMessage());
        }

        if (premio.money > 0)
        {
            jugador.AddMoney(premio.money);
            if (premio.poken == null && premio.item == null && premio.specialItem == null)
                partes.Add($"¡Ganaste {premio.money} monedas!");
        }

        // La ruleta de mejoras de Martin: no va a la mochila, espera a la
        // proxima batalla (ver PlayerModel.MejorasPendientes).
        if (premio.mejora != MejoraDeBatalla.Ninguna)
        {
            int etapas = Mathf.Max(1, premio.etapasDeMejora);
            jugador.AgregarMejora(premio.mejora, etapas);
            partes.Add($"¡Tu equipo va a pelear la próxima batalla con " +
                       $"{MejorasDeBatalla.Describir(new MejoraPendiente(premio.mejora, etapas))}!");
        }

        if (!avisoObligado && !string.IsNullOrEmpty(premio.customMessage))
            return premio.customMessage;

        return partes.Count > 0 ? string.Join(" ", partes) : premio.BuildMessage();
    }
}
