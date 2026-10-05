// Reglas de los movimientos de dos turnos (cargar y recargar), en un solo lugar
// para que el TurnEngine y los tests pregunten exactamente lo mismo.
public static class MoveTurnRules
{
    // Carga un turno antes de pegar (con o sin quedar fuera de alcance).
    public static bool Carga(MoveDefinition move)
        => move != null && (move.turnRule == MoveTurnRule.Charge
                            || move.turnRule == MoveTurnRule.ChargeInvulnerable);

    // Con ese clima no hace falta cargar: Rayo Solar con sol pega en el acto.
    public static bool SaltaLaCarga(MoveDefinition move, WeatherKind clima)
        => move != null && move.skipChargeInWeather != WeatherKind.None
           && move.skipChargeInWeather == clima;

    public static string MensajeDeCarga(MoveDefinition move, string nombre)
    {
        if (move == null || string.IsNullOrEmpty(move.chargeMessage))
            return $"¡{nombre} se está preparando para atacar!";
        // Replace y no string.Format: un texto escrito a mano con otra llave
        // tiraria una excepcion en medio del combate.
        return move.chargeMessage.Replace("{0}", nombre);
    }

    // ---- lo que alcanza al que esta escondido (como en la saga, gen 5+) ----
    // Por id de PokeAPI: los nombres de los assets se pueden traducir distinto.
    private const int Excavar = 91, Vuelo = 19, Bote = 340, Buceo = 291, CaidaLibre = 507;
    private const int Terremoto = 89, Magnitud = 222, Fisura = 90;
    private const int Tornado = 16, Cyclon = 239, Trueno = 87, Vendaval = 542, GanchoAlto = 327,
                      Antiaereo = 479, MilFlechas = 614;
    private const int Surf = 57, Torbellino = 250;

    // Cuanto le pega 'ataque' al que esta escondido con 'escondidoCon': 0 = no
    // lo alcanza (falla), 1 = normal, 2 = doble.
    //
    //   Excavar          Terremoto y Magnitud (doble), Fisura.
    //   Vuelo, Bote...   Tornado y Ciclon (doble), Trueno, Vendaval, Gancho Alto,
    //                    Antiaereo, Mil Flechas.
    //   Buceo            Surf y Torbellino (doble).
    //   Golpe Umbrio,    nada: desaparecen del todo.
    //   Golpe Fantasma
    public static float AlcanceContraEscondido(MoveDefinition escondidoCon, MoveDefinition ataque)
    {
        if (escondidoCon == null || ataque == null) return 0f;
        int a = ataque.pokeApiId;

        switch (escondidoCon.pokeApiId)
        {
            case Excavar:
                if (a == Terremoto || a == Magnitud) return 2f;
                return a == Fisura ? 1f : 0f;

            case Vuelo:
            case Bote:
            case CaidaLibre:
                if (a == Tornado || a == Cyclon) return 2f;
                return a == Trueno || a == Vendaval || a == GanchoAlto || a == Antiaereo || a == MilFlechas ? 1f : 0f;

            case Buceo:
                return a == Surf || a == Torbellino ? 2f : 0f;

            default:
                return 0f;
        }
    }

    // Si el movimiento le hace algo al rival. Contra uno que esta volando o
    // bajo tierra, esos fallan; los que el atacante se aplica a si mismo
    // (Danza Espada) salen igual.
    public static bool ApuntaAlRival(MoveDefinition move)
    {
        if (move == null) return false;
        if (move.power > 0 && move.attackType != AttackType.Status) return true;
        if (move.effect != StatusEffect.None) return true;
        if (move.statChanges != null)
            foreach (StatChangeEntry cambio in move.statChanges)
                if (cambio != null && !cambio.targetSelf) return true;
        return false;
    }
}
