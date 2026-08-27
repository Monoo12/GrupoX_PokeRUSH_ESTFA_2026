using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Les pone el icono a los HealItemDefinition que lo tengan vacio.
//
// Los assets de objetos venian con 'icon' en None, asi que la mochila mostraba
// las filas sin icono (y el recuadro de la descripcion vacio). Los PNG ya estan
// en Assets/Graphics/Items/ con el nombre en ingles de Essentials; esto los
// engancha por nombre de asset, una sola vez.
public static class ItemIconAssigner
{
    private const string ItemsFolder = "Assets/Graphics/Items/";

    // Nombre del asset (sin extension) -> PNG del pack.
    private static readonly Dictionary<string, string> Mapa = new Dictionary<string, string>
    {
        { "Pocion", "POTION" },
        { "SuperPocion", "SUPERPOTION" },
        { "HiperPocion", "HYPERPOTION" },
        { "PocionMaxima", "MAXPOTION" },
        { "CuraTotal", "FULLHEAL" },
        { "Restaurar", "FULLRESTORE" },
        { "Antidoto", "ANTIDOTE" },
        { "Revivir", "REVIVE" },
        { "MaxRevivir", "MAXREVIVE" },
        { "Despertar", "AWAKENING" },
        { "Antiquemar", "BURNHEAL" },
        { "Antihielo", "ICEHEAL" },
        { "Antiparalisis", "PARALYZEHEAL" }
    };

    [MenuItem("PokeRush/UI/Asignar iconos a los objetos")]
    public static void Asignar()
    {
        int puestos = 0, yaTenian = 0, sinMapa = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:HealItemDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var item = AssetDatabase.LoadAssetAtPath<HealItemDefinition>(path);
            if (item == null) continue;

            if (item.icon != null) { yaTenian++; continue; }

            string assetName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!Mapa.TryGetValue(assetName, out string png))
            {
                Debug.LogWarning($"[Iconos] '{assetName}' no esta en el mapa: asignale el icono a mano.");
                sinMapa++;
                continue;
            }

            Sprite sprite = MenuUiKit.Sprite(ItemsFolder + png + ".png");
            if (sprite == null) { sinMapa++; continue; }

            item.icon = sprite;
            EditorUtility.SetDirty(item);
            puestos++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Iconos] Asignados: {puestos}. Ya tenian: {yaTenian}. Sin mapear: {sinMapa}.");
    }
}
