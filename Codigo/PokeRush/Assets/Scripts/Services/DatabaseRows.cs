using SQLite;

[Table("player")]
public class PlayerRow
{
    // OJO: sin AutoIncrement. El Id es el numero de slot de guardado (siempre 1
    // por ahora) y lo asigna SavePlayer a mano. Con AutoIncrement, sqlite-net
    // ignoraba el Id asignado y HasSaveData(1)/LoadPlayer(1) no encontraban la fila.
    [PrimaryKey]
    public int Id { get; set; }
    public string Name { get; set; }
    public int Money { get; set; }
}

[Table("player_poken")]
public class PlayerPokenRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string PokenDefinitionId { get; set; }
    public int Level { get; set; }
    public int CurrentXP { get; set; }
    public int CurrentHP { get; set; }
    public string StatusEffect { get; set; }
    public int SlotIndex { get; set; }
    public string EquippedCosmetic { get; set; }
    // IVs serializados como "hp,atk,def,spa,spd,spe" (ej: "31,12,0,5,20,31").
    // Es string y no int para distinguir saves viejos (NULL -> se regeneran)
    // de un IV 0 legitimo. La columna la agrega solo sqlite-net via ALTER.
    public string Ivs { get; set; }
}

[Table("player_poken_moves")]
public class PlayerPokenMoveRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int PlayerPokenId { get; set; }
    public string MoveDefinitionId { get; set; }
    public int CurrentPP { get; set; }
}

[Table("player_inventory")]
public class PlayerInventoryRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string ItemDefinitionId { get; set; }
    public string ItemType { get; set; }
    public int Quantity { get; set; }
}

[Table("player_cosmetics")]
public class PlayerCosmeticRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string CosmeticDefinitionId { get; set; }
}

[Table("active_flags")]
public class ActiveFlagRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public string FlagName { get; set; }
}

[Table("dialogues")]
public class DialogueRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public string NpcId { get; set; }
    public int LineIndex { get; set; }
    public string Text { get; set; }
    public string RequiredFlag { get; set; }
    public string GrantsFlag { get; set; }
}