using System.IO;
using UnityEditor;
using UnityEngine;

// Genera un prefab por especie, listo para arrastrar a cualquier escena.
//
// Por que hace falta: el FBX crudo NO es portable. Las 20 especies cuyo modelo
// importa sin materiales quedan con el "Lit" gris compartido del paquete, y la
// escala nativa del pack es ~0.026, asi que un modelo recien arrastrado se ve
// gris y microscopico. Arreglarlo escena por escena no escala: los arreglos
// quedan como override de instancia y no viajan.
//
// El prefab congela las tres cosas de una vez:
//   - escala 55x (deja a Cinderace en 1.43 m y a Snorlax en 1.71 m, respetando
//     las proporciones relativas entre especies)
//   - materiales correctos, incluidos los generados por UniteMaterialBuilder
//   - un nombre limpio, sin el "pref_char_bat_081500_..." del pack
//
// Con esto, llevar un Pokemon a una escena nueva es arrastrar su prefab. Y si
// mas adelante se corrige un material, se corrige en el prefab y se propaga a
// todas las escenas que lo usen.
public static class UnitePrefabBuilder
{
    private const string CarpetaModelos = "Assets/_AuditRank";
    private const string CarpetaPrefabs = "Assets/_AuditRank/_Prefabs";

    // Los modelos del pack vienen a una escala de ~0.026 unidades. 55x los deja
    // en metros creibles sin normalizar especie por especie, que aplastaria las
    // diferencias de tamano entre un Zorua y un Snorlax.
    private const float EscalaUnite = 55f;

    [MenuItem("PokeRush/3D/Generar prefabs de UNITE")]
    public static void Generar()
    {
        if (!AssetDatabase.IsValidFolder(CarpetaPrefabs))
            AssetDatabase.CreateFolder(CarpetaModelos, "_Prefabs");

        int generados = 0;
        int sinModelo = 0;

        foreach (string carpeta in Directory.GetDirectories(Application.dataPath + "/_AuditRank"))
        {
            string especie = Path.GetFileName(carpeta);
            if (especie.StartsWith("_")) continue;

            GameObject modelo = CargarModelo(especie);
            if (modelo == null) { sinModelo++; continue; }

            GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
            instancia.name = especie;
            instancia.transform.localScale = Vector3.one * EscalaUnite;

            AplicarMateriales(instancia, especie);

            PrefabUtility.SaveAsPrefabAsset(instancia, $"{CarpetaPrefabs}/{especie}.prefab");
            Object.DestroyImmediate(instancia);
            generados++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[UNITE] Prefabs generados: {generados}. Especies sin modelo: {sinModelo}. " +
                  $"Estan en {CarpetaPrefabs}.");
    }

    private static GameObject CargarModelo(string especie)
    {
        string carpeta = Application.dataPath + "/_AuditRank/" + especie;
        string[] fbx = Directory.GetFiles(carpeta, "*.fbx");
        if (fbx.Length == 0) return null;

        return AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{CarpetaModelos}/{especie}/{Path.GetFileName(fbx[0])}");
    }

    // Reemplaza cualquier slot que haya quedado con el material compartido del
    // paquete por el material propio que dejo UniteMaterialBuilder.
    private static void AplicarMateriales(GameObject instancia, string especie)
    {
        Material generado = null;

        foreach (Renderer renderer in instancia.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materiales = renderer.sharedMaterials;
            bool cambio = false;

            for (int i = 0; i < materiales.Length; i++)
            {
                Material material = materiales[i];
                bool roto = material == null
                            || AssetDatabase.GetAssetPath(material).StartsWith("Packages/");
                if (!roto) continue;

                if (generado == null) generado = BuscarGenerado(especie);
                if (generado == null) continue;

                materiales[i] = generado;
                cambio = true;
            }

            if (cambio) renderer.sharedMaterials = materiales;
        }
    }

    private static Material BuscarGenerado(string especie)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { $"{CarpetaModelos}/{especie}" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.EndsWith("_generado.mat")) return AssetDatabase.LoadAssetAtPath<Material>(ruta);
        }
        return null;
    }
}
