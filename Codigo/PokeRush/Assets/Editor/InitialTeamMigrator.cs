using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Rellena el 'Equipo inicial' de los GameManager que hay en las escenas:
//   PokeRush > Reasignar equipo inicial
//
// POR QUE EXISTE
// El campo paso de string[] (se buscaba por pokenName en runtime) a
// PokenDefinition[] (referencias directas). Cambiar el tipo de un campo
// serializado hace que Unity DESCARTE lo que habia guardado, asi que todas las
// escenas con un GameManager quedaron con el equipo vacio. Esto lo rellena de
// una, en vez de arrastrar tres assets a mano en cada escena.
//
// Es una herramienta de migracion de un solo uso, no parte del juego. Se puede
// borrar cuando las escenas ya esten guardadas con el equipo asignado; queda
// igual porque tambien sirve para recrear el equipo de arranque si alguien lo
// vacia sin querer.
//
// La busqueda por nombre sobrevive SOLO aca, en tiempo de editor, que es donde
// corresponde: si una especie no aparece, falla ruidoso mientras se edita en
// vez de dejar al jugador sin equipo en runtime.
namespace PokeRush.EditorTools
{
    public static class InitialTeamMigrator
    {
        // El equipo de la torre 1. Ver el comentario de GameManager.equipoInicial
        // para por que son estos tres.
        private static readonly string[] EquipoTorre1 = { "Gastly", "Basculin", "Combusken" };

        [MenuItem("PokeRush/Reasignar equipo inicial")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            List<PokenDefinition> especies = Resolver(EquipoTorre1);
            if (especies == null) return;

            string escenaOriginal = SceneManager.GetActiveScene().path;
            int tocadas = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                bool cambiada = false;
                foreach (GameObject raiz in scene.GetRootGameObjects())
                {
                    foreach (GameManager gm in raiz.GetComponentsInChildren<GameManager>(true))
                    {
                        Asignar(gm, especies);
                        cambiada = true;
                    }
                }

                if (!cambiada) continue;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                tocadas++;
                Debug.Log($"[EquipoInicial] Asignado en {path}");
            }

            if (!string.IsNullOrEmpty(escenaOriginal))
                EditorSceneManager.OpenScene(escenaOriginal, OpenSceneMode.Single);

            Debug.Log($"[EquipoInicial] Listo: {tocadas} escena(s) con GameManager actualizadas.");
        }

        // Deja el campo cableado. Publico para que el builder de la escena demo
        // arme su GameManager con el mismo equipo y no se desincronicen.
        public static void Asignar(GameManager gm, List<PokenDefinition> especies)
        {
            var so = new SerializedObject(gm);
            SerializedProperty prop = so.FindProperty("equipoInicial");
            prop.arraySize = especies.Count;
            for (int i = 0; i < especies.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = especies[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static List<PokenDefinition> EquipoPorDefecto() => Resolver(EquipoTorre1);

        // Busca los assets por pokenName. Devuelve null (con error) si falta
        // alguno: es preferible no tocar nada a dejar el equipo a medias.
        private static List<PokenDefinition> Resolver(string[] nombres)
        {
            var todas = new List<PokenDefinition>();
            foreach (string guid in AssetDatabase.FindAssets("t:PokenDefinition"))
                todas.Add(AssetDatabase.LoadAssetAtPath<PokenDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid)));

            var resultado = new List<PokenDefinition>();
            foreach (string nombre in nombres)
            {
                PokenDefinition encontrada = todas.Find(
                    p => p != null && p.pokenName == nombre);
                if (encontrada == null)
                {
                    Debug.LogError($"[EquipoInicial] No existe ninguna PokenDefinition con " +
                                   $"pokenName '{nombre}'. No se toca ninguna escena.");
                    return null;
                }
                resultado.Add(encontrada);
            }
            return resultado;
        }
    }
}
