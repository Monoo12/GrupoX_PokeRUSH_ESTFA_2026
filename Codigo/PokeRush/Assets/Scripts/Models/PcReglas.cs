using System.Collections.Generic;

// Un lugar donde puede estar un Pokemon: un hueco del equipo o de una caja del
// PC. Es lo que el jugador agarra y suelta en la pantalla del PC.
public struct PcLugar
{
    public bool EnEquipo;
    // Solo cuenta fuera del equipo.
    public int Caja;
    // Posicion en el equipo, o lugar dentro de la caja.
    public int Indice;

    public static PcLugar DelEquipo(int indice) => new PcLugar { EnEquipo = true, Caja = -1, Indice = indice };
    public static PcLugar DeCaja(int caja, int lugar) => new PcLugar { EnEquipo = false, Caja = caja, Indice = lugar };

    public bool Igual(PcLugar otro)
        => EnEquipo == otro.EnEquipo && Indice == otro.Indice && (EnEquipo || Caja == otro.Caja);
}

// Las reglas del PC, sin nada de Unity para poder probarlas solas.
//
// La pantalla no toca el equipo ni las cajas: le pide a esto que mueva un
// Pokemon de un lugar a otro, y esto hace el movimiento entero o no hace nada.
// Asi no puede quedar un Pokemon "en el aire" si algo sale mal a mitad de camino.
public static class PcReglas
{
    public const string MensajeEquipoSinPeleador =
        "Tu equipo tiene que quedarse con al menos un Pokémon que pueda pelear.";

    public static PokenModel Get(PlayerModel jugador, PcLugar lugar)
    {
        if (jugador == null) return null;
        if (lugar.EnEquipo)
            return lugar.Indice >= 0 && lugar.Indice < jugador.Team.Count ? jugador.Team[lugar.Indice] : null;
        return jugador.Storage.Get(lugar.Caja, lugar.Indice);
    }

    // Lleva el Pokemon de 'desde' a 'hasta'. Si en 'hasta' habia otro, los
    // intercambia. Un hueco vacio del equipo (indice >= cantidad) significa
    // "sumalo al final".
    //
    // Devuelve null si se hizo, o el motivo por el que no se puede.
    public static string Mover(PlayerModel jugador, PcLugar desde, PcLugar hasta)
    {
        if (jugador == null) return "No hay partida cargada.";

        PokenModel agarrado = Get(jugador, desde);
        if (agarrado == null) return "Ahí no hay ningún Pokémon.";
        if (desde.Igual(hasta)) return null;

        if (!hasta.EnEquipo && !jugador.Storage.EsLugarValido(hasta.Caja, hasta.Indice))
            return "Ese lugar no existe.";

        List<PokenModel> equipo = jugador.Team;
        PokenModel destino = Get(jugador, hasta);

        // ---- equipo -> equipo: cambiar el orden ----
        if (desde.EnEquipo && hasta.EnEquipo)
        {
            if (destino != null)
            {
                jugador.SwapTeamSlots(desde.Indice, hasta.Indice);
            }
            else
            {
                equipo.RemoveAt(desde.Indice);
                equipo.Add(agarrado);
            }
            return null;
        }

        // ---- caja -> caja ----
        if (!desde.EnEquipo && !hasta.EnEquipo)
        {
            jugador.Storage.Set(hasta.Caja, hasta.Indice, agarrado);
            jugador.Storage.Set(desde.Caja, desde.Indice, destino);
            return null;
        }

        // ---- equipo -> caja: dejarlo (o cambiarlo por el que estaba) ----
        if (desde.EnEquipo)
        {
            var queda = new List<PokenModel>(equipo);
            if (destino != null) queda[desde.Indice] = destino;
            else queda.RemoveAt(desde.Indice);

            if (!HayQuienPelee(queda)) return MensajeEquipoSinPeleador;

            if (destino != null) equipo[desde.Indice] = destino;
            else equipo.RemoveAt(desde.Indice);
            jugador.Storage.Set(hasta.Caja, hasta.Indice, agarrado);
            return null;
        }

        // ---- caja -> equipo ----
        if (destino == null)
        {
            if (equipo.Count >= PlayerModel.EquipoMaximo)
                return $"Tu equipo ya tiene {PlayerModel.EquipoMaximo} Pokémon.";

            equipo.Add(agarrado);
            jugador.Storage.Set(desde.Caja, desde.Indice, null);
            return null;
        }

        var nuevoEquipo = new List<PokenModel>(equipo) { [hasta.Indice] = agarrado };
        if (!HayQuienPelee(nuevoEquipo)) return MensajeEquipoSinPeleador;

        equipo[hasta.Indice] = agarrado;
        jugador.Storage.Set(desde.Caja, desde.Indice, destino);
        return null;
    }

    public static bool HayQuienPelee(IList<PokenModel> equipo)
    {
        if (equipo == null) return false;
        foreach (PokenModel p in equipo)
            if (p != null && !p.IsFainted) return true;
        return false;
    }

    // Si el jugador tiene un ejemplar de esa especie, en el equipo o en el PC.
    // Lo usa el ascensor del piso 3 (pide un shiny).
    public static bool TieneEspecie(PlayerModel jugador, PokenDefinition especie, bool debeSerShiny)
    {
        if (jugador == null || especie == null) return false;

        bool Sirve(PokenModel p) => p != null && p.Definition == especie && (!debeSerShiny || p.IsShiny);

        foreach (PokenModel p in jugador.Team)
            if (Sirve(p)) return true;
        return jugador.Storage.Contiene(Sirve);
    }
}
