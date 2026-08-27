using UnityEngine;

// Iconos compartidos por todos los menus (tipos, estados alterados).
//
// Existe como asset unico en Resources para que ningun prefab tenga que
// arrastrar 19 sprites de tipo a mano: cada widget lo resuelve solo. Sigue
// siendo un asset normal, asi que cambiar un icono es abrirlo y arrastrar otro
// sprite.
[CreateAssetMenu(fileName = "MenuIcons", menuName = "PokeRush/UI/MenuIconLibrary")]
public class MenuIconLibrary : ScriptableObject
{
    public const string ResourcePath = "Menus/MenuIcons";

    private static MenuIconLibrary _instance;
    public static MenuIconLibrary Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<MenuIconLibrary>(ResourcePath);
            return _instance;
        }
    }

    [Header("Tipos (indice = enum PokenType, 0 = None)")]
    [Tooltip("Sub-sprites de Sprites/UI/Battle/icon_types.png, en el orden del enum.")]
    public Sprite[] typeIcons;

    [Header("Estados alterados (indice = enum StatusEffect, 0 = None)")]
    [Tooltip("Sub-sprites de Sprites/UI/Battle/icon_statuses.png, en el orden del enum.")]
    public Sprite[] statusIcons;

    [Header("Genero")]
    public Sprite maleIcon;
    public Sprite femaleIcon;

    public Sprite GetTypeIcon(PokenType type) => At(typeIcons, (int)type);
    public Sprite GetStatusIcon(StatusEffect status) => At(statusIcons, (int)status);

    private static Sprite At(Sprite[] table, int index)
    {
        if (table == null || index <= 0 || index >= table.Length) return null;
        return table[index];
    }

    // Helper para pintar un Image de tipo/estado sin repetir el null-check en
    // cada widget: si no hay icono, el Image se apaga en vez de quedar en blanco.
    public static void PaintType(UnityEngine.UI.Image image, PokenType type)
    {
        if (image == null) return;
        Sprite sprite = Instance != null ? Instance.GetTypeIcon(type) : null;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    public static void PaintStatus(UnityEngine.UI.Image image, StatusEffect status)
    {
        if (image == null) return;
        Sprite sprite = Instance != null ? Instance.GetStatusIcon(status) : null;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }
}
