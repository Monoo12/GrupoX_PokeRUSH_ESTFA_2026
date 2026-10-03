using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Pone el interlineado de la fuente pixel (FuentePixel.Interlineado) en todos
// los Text con "power clear" de los prefabs.
//
//   PokeRush > UI > Interlineado de la fuente pixel
//
// Los builders ya lo ponen al crear cada texto; esto es para los prefabs que ya
// estaban hechos y para los que se armen a mano.
//
// NO toca escenas a proposito: guardar una escena de mapa desde un script le
// borra los colliderType = Grid de 'Colisiones' (ver ColisionesTilemapFixer).
// Las cajas de dialogo de las escenas son instancias de DialogoCanvas.prefab y
// lo heredan; la unica copia suelta (la del Data Center, NestorGOD2) se cambio
// a mano en el YAML.
public static class InterlineadoDeFuentePixel
{
    private static readonly string[] Carpetas = { "Assets/Prefabs", "Assets/Resources" };

    [MenuItem("PokeRush/UI/Interlineado de la fuente pixel")]
    public static void Aplicar()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Interlineado] Sali del Play Mode antes de correr esto.");
            return;
        }

        int textos = 0;
        var tocados = new List<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", Carpetas))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject raiz = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int cambiados = 0;
                foreach (Text t in raiz.GetComponentsInChildren<Text>(true))
                {
                    if (!FuentePixel.Es(t.font) || Mathf.Approximately(t.lineSpacing, FuentePixel.Interlineado)) continue;
                    // Los de un prefab anidado (las filas de los menus) se
                    // cambian en SU prefab: tocarlos aca dejaria un override.
                    if (PrefabUtility.IsPartOfPrefabInstance(t)) continue;
                    t.lineSpacing = FuentePixel.Interlineado;
                    cambiados++;
                }

                if (cambiados == 0) continue;
                PrefabUtility.SaveAsPrefabAsset(raiz, path);
                textos += cambiados;
                tocados.Add($"{path} ({cambiados})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        if (tocados.Count == 0)
            Debug.Log($"[Interlineado] Todos los textos con power clear ya usan {FuentePixel.Interlineado}.");
        else
            Debug.Log($"[Interlineado] {textos} textos en {tocados.Count} prefabs pasan a {FuentePixel.Interlineado}:\n" +
                      string.Join("\n", tocados));
    }
}
