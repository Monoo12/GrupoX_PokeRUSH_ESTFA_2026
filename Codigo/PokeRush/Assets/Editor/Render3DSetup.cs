using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Cambia el renderer de URP entre 3D y 2D desde el menu, sin editar YAML a mano.
//
// El proyecto nacio de la plantilla 2D, asi que Assets/Settings/UniversalRP.asset
// apunta a Renderer2D.asset. Para dibujar en 3D hace falta un UniversalRendererData
// (forward). Este script lo crea la primera vez y lo enchufa al pipeline.
//
// Se hace por codigo y no editando el .asset porque los GUID de los tipos de URP
// no son estables entre versiones del paquete: un YAML escrito a mano se rompe
// en la proxima actualizacion. Ademas un renderer creado con CreateInstance queda
// con las referencias a shaders internos en null, y hay que rellenarlas.
//
// Renderer2D.asset se conserva: "Volver al render 2D" deshace todo.
namespace PokeRush.EditorTools
{
    public static class Render3DSetup
    {
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string Renderer3DPath = "Assets/Settings/UniversalRenderer3D.asset";
        private const string Renderer2DPath = "Assets/Settings/Renderer2D.asset";

        [MenuItem("PokeRush/3D/Configurar render 3D", priority = 0)]
        public static void UseRenderer3D()
        {
            UniversalRendererData renderer = GetOrCreateRenderer3D();
            if (renderer == null) return;

            if (!AssignRenderer(renderer)) return;

            Debug.Log("[PokeRush] Render 3D activo (UniversalRenderer forward).");
            EditorUtility.DisplayDialog("Render 3D listo",
                "El pipeline ahora usa UniversalRenderer3D.asset (forward).\n\n" +
                "Los sprites se siguen dibujando igual: usan el shader Sprites/Default, " +
                "que es unlit y no depende del renderer.\n\n" +
                "Para volver atras: PokeRush > 3D > Volver al render 2D.",
                "OK");
        }

        [MenuItem("PokeRush/3D/Volver al render 2D", priority = 1)]
        public static void UseRenderer2D()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(Renderer2DPath);
            if (renderer == null)
            {
                Debug.LogError($"[PokeRush] No se encontro {Renderer2DPath}.");
                return;
            }

            if (!AssignRenderer(renderer)) return;
            Debug.Log("[PokeRush] Render 2D restaurado (Renderer2D).");
        }

        // Crea el renderer 3D si todavia no existe. Replica lo que hace URP en
        // UniversalRenderPipelineAsset.CreateRendererAsset, que es internal y no
        // se puede llamar desde aca.
        private static UniversalRendererData GetOrCreateRenderer3D()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer3DPath);
            if (existing != null) return existing;

            var data = ScriptableObject.CreateInstance<UniversalRendererData>();

            // Los efectos de post-proceso viven en un asset del paquete de URP.
            // Sin esto el renderer funciona, pero el post-proceso queda apagado.
            string postProcessPath =
                UniversalRenderPipelineAsset.packagePath + "/Runtime/Data/PostProcessData.asset";
            data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(postProcessPath);
            if (data.postProcessData == null)
                Debug.LogWarning($"[PokeRush] No se encontro {postProcessPath}. " +
                                 "El render 3D funciona igual, pero sin post-proceso.");

            AssetDatabase.CreateAsset(data, Renderer3DPath);

            // Un renderer creado por codigo queda con las referencias a los shaders
            // internos de URP (blit, copy depth, etc.) en null. Sin rellenarlas el
            // renderer no dibuja nada. Esta es la misma llamada que hace URP.
            ResourceReloader.ReloadAllNullIn(data, UniversalRenderPipelineAsset.packagePath);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PokeRush] Creado {Renderer3DPath}.");
            return data;
        }

        // m_RendererDataList y m_DefaultRendererIndex son campos internal del
        // pipeline asset: se llegan por SerializedObject, no por la API publica.
        private static bool AssignRenderer(ScriptableRendererData renderer)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                Debug.LogError($"[PokeRush] No se encontro el pipeline en {PipelinePath}.");
                return false;
            }

            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            SerializedProperty defaultIndex = so.FindProperty("m_DefaultRendererIndex");
            if (list == null || defaultIndex == null)
            {
                Debug.LogError("[PokeRush] El pipeline asset no expone m_RendererDataList. " +
                               "Cambio la version de URP: revisar Render3DSetup.");
                return false;
            }

            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            defaultIndex.intValue = 0;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }
    }
}
