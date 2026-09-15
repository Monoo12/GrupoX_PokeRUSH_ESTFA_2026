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
