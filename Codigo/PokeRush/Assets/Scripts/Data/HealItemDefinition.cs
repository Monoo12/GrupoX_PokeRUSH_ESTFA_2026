using UnityEngine;
//Esta clase define las caracter�sticas de un objeto de curaci�n, incluyendo su identidad, valores de compra/venta y efectos de curaci�n.
[CreateAssetMenu(fileName = "NewHealItem", menuName = "PokeRush/Items/HealItemDefinition")]
public class HealItemDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string itemName;
    public string description;
    public Sprite icon;

    [Header("Valores")]
    public int buyPrice;
    public int sellPrice;

    [Header("Curacion")]
    public int healAmount;
    public bool fullHeal;

    [Header("Estados alterados")]
    [Tooltip("Estados que cura este objeto (Antidoto -> Poisoned, Antiquemar -> Burned...). " +
             "Vacio = no cura ningun estado.")]
    public StatusEffect[] curesStatuses;
    [Tooltip("Cura cualquier estado alterado (Cura Total). Ignora la lista de arriba.")]
    public bool curesAllStatuses;

    [Header("Revivir")]
    [Tooltip("Se puede usar sobre un Poken debilitado. Sin esto, el objeto se rechaza.")]
    public bool revives;
    [Tooltip("PS con los que vuelve, en % del maximo. Con 'fullHeal' marcado revive al 100%.")]
    [Range(1, 100)] public int revivePercent = 50;

    // Atajo para la UI y el ItemUsageService: si el objeto sirve para algun estado.
    public bool CuresAnyStatus
        => curesAllStatuses || (curesStatuses != null && curesStatuses.Length > 0);
}