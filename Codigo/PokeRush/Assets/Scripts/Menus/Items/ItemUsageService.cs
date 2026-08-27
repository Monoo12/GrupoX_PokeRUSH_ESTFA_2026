// Resultado de intentar usar un objeto sobre un Poken.
public enum ItemUseOutcome
{
    Applied,        // se aplico y hay que descontar el objeto
    NothingToDo,    // el objeto no le hace nada a ese Poken (PS al maximo, sin estado)
    InvalidTarget,  // no se puede usar sobre ese Poken (debilitado y el objeto no revive)
    NoStock,        // no quedan unidades
    NotUsableHere   // el objeto no se puede usar en este contexto
}

public struct ItemUseResult
{
    public ItemUseOutcome Outcome;
    public string Message;
    public int HealedHp;
    public bool CuredStatus;
    public bool Revived;

    public bool Applied => Outcome == ItemUseOutcome.Applied;
}

// Toda la logica de "que le hace este objeto a este Poken", separada de la UI y
// del combate.
//
// Antes esto vivia dentro de TurnEngine.DoUseItem y solo sabia curar al Poken
// ACTIVO del combate. El enunciado pide poder elegir a quien se le aplica y
// poder hacerlo tambien desde el mundo, asi que las reglas se mudaron aca: el
// TurnEngine y la mochila del mundo llaman a los mismos metodos y no hay dos
// versiones de la misma regla que se puedan desincronizar.
public static class ItemUsageService
{
    // Chequea sin consumir nada. Lo usa la party para pintar en gris a los que
    // no son objetivo valido y para avisar antes de gastar el objeto.
    public static ItemUseResult Evaluate(HealItemDefinition item, PokenModel target)
    {
        if (item == null || target == null)
            return Fail(ItemUseOutcome.InvalidTarget, "No se puede usar ese objeto.");

        string nombre = target.Definition != null ? target.Definition.pokenName : "El Poken";

        if (target.IsFainted)
        {
            // Un objeto de curacion normal NO revive: revivir es una mecanica
            // aparte y hay que marcarla explicitamente en el objeto.
            if (!item.revives)
                return Fail(ItemUseOutcome.InvalidTarget, $"¡{nombre} está debilitado!");

            return Ok($"¡{nombre} se recuperó!");
        }

        if (item.revives && !item.CuresAnyStatus && item.healAmount <= 0 && !item.fullHeal)
            return Fail(ItemUseOutcome.NothingToDo, $"¡{nombre} no está debilitado!");

        bool puedeCurarPs = (item.fullHeal || item.healAmount > 0) && target.CurrentHP < target.MaxHP;
        bool puedeCurarEstado = CuresStatusOf(item, target);

        if (!puedeCurarPs && !puedeCurarEstado)
        {
            // Un objeto que no cambia nada no se gasta: en la version anterior
            // la pocion desaparecia del inventario sin curar.
            string motivo = target.CurrentHP >= target.MaxHP
                ? $"¡{nombre} ya está al máximo!"
                : $"No le haría ningún efecto a {nombre}.";
            return Fail(ItemUseOutcome.NothingToDo, motivo);
        }

        return Ok(null);
    }

    // Descuenta el objeto y lo aplica. Devuelve el detalle de lo que paso para
    // que el que llama arme el mensaje o dispare animaciones.
    public static ItemUseResult Apply(HealItemDefinition item, PokenModel target, InventoryModel inventory)
    {
        ItemUseResult check = Evaluate(item, target);
        if (!check.Applied) return check;

        if (inventory == null || !inventory.ConsumeHealItem(item, 1))
            return Fail(ItemUseOutcome.NoStock, $"No te quedan {item.itemName}.");

        string nombre = target.Definition != null ? target.Definition.pokenName : "El Poken";
        var result = new ItemUseResult { Outcome = ItemUseOutcome.Applied };

        if (target.IsFainted && item.revives)
        {
            int psRevivido = item.fullHeal
                ? target.MaxHP
                : UnityEngine.Mathf.Max(1, target.MaxHP * UnityEngine.Mathf.Clamp(item.revivePercent, 1, 100) / 100);
            target.SetCurrentHP(psRevivido);
            target.CureStatus();
            result.Revived = true;
            result.HealedHp = psRevivido;
            result.Message = $"¡{nombre} recuperó fuerzas!";
            return result;
        }

        int antes = target.CurrentHP;
        if (item.fullHeal || item.healAmount > 0)
        {
            target.Heal(item.fullHeal ? target.MaxHP : item.healAmount);
            result.HealedHp = target.CurrentHP - antes;
        }

        if (CuresStatusOf(item, target))
        {
            target.CureStatus();
            result.CuredStatus = true;
        }

        result.Message = BuildMessage(nombre, result);
        return result;
    }

    // Si el objeto sirve para el estado que tiene AHORA ese Poken.
    public static bool CuresStatusOf(HealItemDefinition item, PokenModel target)
    {
        if (item == null || target == null) return false;
        if (target.CurrentStatus == StatusEffect.None) return false;
        if (item.curesAllStatuses || item.fullHeal) return true;

        if (item.curesStatuses == null) return false;
        foreach (StatusEffect estado in item.curesStatuses)
            if (estado == target.CurrentStatus) return true;
        return false;
    }

    private static string BuildMessage(string nombre, ItemUseResult result)
    {
        if (result.HealedHp > 0 && result.CuredStatus)
            return $"¡{nombre} recuperó {result.HealedHp} PS y se curó!";
        if (result.HealedHp > 0)
            return $"¡{nombre} recuperó {result.HealedHp} PS!";
        if (result.CuredStatus)
            return $"¡{nombre} se recuperó!";
        return $"¡{nombre} está mejor!";
    }

    private static ItemUseResult Ok(string message)
        => new ItemUseResult { Outcome = ItemUseOutcome.Applied, Message = message };

    private static ItemUseResult Fail(ItemUseOutcome outcome, string message)
        => new ItemUseResult { Outcome = outcome, Message = message };
}
