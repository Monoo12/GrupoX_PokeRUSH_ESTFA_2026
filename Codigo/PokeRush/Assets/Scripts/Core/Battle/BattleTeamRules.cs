using System.Collections.Generic;

// Reglas de "quien puede pelear". Un unico lugar que responde si un Poken esta
// vivo, si un equipo todavia tiene con que seguir y quien es el ULTIMO que le
// queda a un lado.
//
// Por que existe: antes cada sistema lo contaba a mano y no todos contaban
// igual. El combate contra el boss preguntaba "es el ultimo?" contando los
// vivos del equipo SIN excluir al activo, asi que el anteultimo tambien daba
// true: se salvaba de caer en 1 PS turno tras turno y el combate entraba en
// bucle contra un Poken que ya estaba muerto, mientras la fase de liberacion
// nunca arrancaba. Con el conteo centralizado esa clase de bug no vuelve.
//
// Regla fundamental: un Poken debilitado no actua, no recibe turno y no puede
// ser elegido como activo. La muerte es definitiva hasta que exista una
// mecanica explicita de revivir.
public static class BattleTeamRules
{
    public static bool EstaVivo(PokenModel poken) => poken != null && !poken.IsFainted;

    // El indice apunta a un Poken del equipo que esta vivo?
    public static bool EsIndiceVivo(List<PokenModel> team, int index)
        => team != null && index >= 0 && index < team.Count && EstaVivo(team[index]);

    // Primer Poken con vida del equipo (-1 si no queda ninguno)
    public static int PrimerVivo(List<PokenModel> team)
    {
        if (team == null) return -1;
        for (int i = 0; i < team.Count; i++)
            if (EstaVivo(team[i])) return i;
        return -1;
    }

    public static bool TieneAlgunoVivo(List<PokenModel> team) => PrimerVivo(team) >= 0;

    // Cuantos hay en el BANCO (sin contar al activo) listos para entrar.
    public static int VivosEnBanco(List<PokenModel> team, int activeIndex)
    {
        if (team == null) return 0;
        int vivos = 0;
        for (int i = 0; i < team.Count; i++)
            if (i != activeIndex && EstaVivo(team[i])) vivos++;
        return vivos;
    }

    public static bool HayBanco(List<PokenModel> team, int activeIndex)
        => VivosEnBanco(team, activeIndex) > 0;

    // El activo es el ultimo del equipo: no queda NINGUN otro con vida.
    //
    // Se calcula excluyendo al activo a proposito: asi da la misma respuesta
    // este vivo o recien debilitado, que es justo lo que necesita el boss para
    // decidir si se salva de caer y arranca la fase de liberacion.
    public static bool EsElUltimo(List<PokenModel> team, int activeIndex)
        => VivosEnBanco(team, activeIndex) == 0;

    // Primer vivo que NO sea el activo (-1 si no hay). Reemplazo por defecto.
    public static int PrimerVivoDelBanco(List<PokenModel> team, int activeIndex)
    {
        if (team == null) return -1;
        for (int i = 0; i < team.Count; i++)
            if (i != activeIndex && EstaVivo(team[i])) return i;
        return -1;
    }

    // Un Poken debilitado no ejecuta ninguna accion de combate. Se chequea
    // ANTES de dejarlo atacar, usar un movimiento o recibir un turno.
    public static bool PuedeActuar(BattlePokenState estado)
        => estado != null && EstaVivo(estado.Poken);
}
