using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// La tienda del piso 3 del casino (playtest del 5-oct: "agrega un NPC que
// funcione como tienda en el piso 3").
//
//   PokeRush > Casino > Poner la tienda del piso 3
//
// Un tendero (hoja Graphics/Characters/tendero) en la fila de arriba, a la
// derecha de la enfermera y del PC: el rincon de servicios del piso, como el
// mostrador de un Centro Pokemon. Vende lo mismo que Tixi en la cueva
// (TiendaDelCasino copia la lista de TiendaDeTixi), a los precios de siempre.
//
// Va en su propio contenedor ("TiendaCasino") y no en PersonajesCasino:
// CasinoNpcBuilder rehace ese contenedor entero cada vez que arma el piso.
// Se puede volver a correr: borra el tendero anterior y lo rehace.
public static class CasinoTiendaBuilder
{
    private const string Piso3 = "Assets/Scenes/Casino nivel 3.unity";
    private const string RutaDeLaTienda = "Assets/Resources/Definitions/Casino/TiendaDelCasino.asset";
    private const string Contenedor = "TiendaCasino";
    private const string Hoja = "tendero";
    // Enfermera en (-13.5, 19.5) y PC en (-14.5, 19.5): queda una celda libre
    // entre la enfermera y el tendero, y la fila 18 sigue libre para pasar.
    private static readonly Vector2 Lugar = new Vector2(-11.5f, 19.5f);

    [MenuItem("PokeRush/Casino/Poner la tienda del piso 3")]
    public static void Poner()
    {
        VendorDefinition tienda = TiendaDelCasino();
        if (tienda == null) return;

        Scene scene = SceneManager.GetSceneByPath(Piso3);
        bool yaAbierta = scene.IsValid() && scene.isLoaded;
        if (!yaAbierta) scene = EditorSceneManager.OpenScene(Piso3, OpenSceneMode.Additive);

        GameObject mapaRaiz = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "MapaRaiz");
        if (mapaRaiz == null)
        {
            Debug.LogError($"[Casino] '{scene.name}' no tiene MapaRaiz.");
            if (!yaAbierta) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        Transform vieja = mapaRaiz.transform.Find(Contenedor);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);
        var raiz = new GameObject(Contenedor);
        raiz.transform.SetParent(mapaRaiz.transform, false);

        GameObject tendero = CasinoNpcBuilder.Personaje(raiz, "TenderoCasino", Lugar, Hoja, CasinoNpcBuilder.Abajo);
        var npc = tendero.AddComponent<CasinoTiendaNpc>();
        var so = new SerializedObject(npc);
        so.FindProperty("tienda").objectReferenceValue = tienda;
        so.ApplyModifiedPropertiesWithoutUndo();

        CasinoNpcBuilder.ReponerColisiones(scene);
        Physics2D.SyncTransforms();

        List<string> problemas = CasinoNpcBuilder.Verificar(scene, raiz);
        if (problemas.Count > 0)
        {
            foreach (string p in problemas) Debug.LogError($"[Casino] {scene.name}: {p}");
            Debug.LogError($"[Casino] {scene.name}: NO se guarda.");
            if (yaAbierta) EditorSceneManager.OpenScene(Piso3, OpenSceneMode.Single);
            else EditorSceneManager.CloseScene(scene, true);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!yaAbierta) EditorSceneManager.CloseScene(scene, true);
        Debug.Log($"[Casino] Tienda del piso 3 puesta en {Lugar} ({tienda.healItemsForSale.Length} objetos).");
    }

    // Lo mismo que vende Tixi: si alguien cambia la lista de Tixi, la del casino
    // se actualiza la proxima vez que se corra esto.
    public static VendorDefinition TiendaDelCasino()
    {
        VendorDefinition tixi = QuarchoCuevaAssets.TiendaDeTixi(null);
        if (tixi == null || tixi.healItemsForSale == null || tixi.healItemsForSale.Length == 0)
        {
            Debug.LogError("[Casino] TiendaDeTixi no tiene objetos: no hay que copiar.");
            return null;
        }

        var def = AssetDatabase.LoadAssetAtPath<VendorDefinition>(RutaDeLaTienda);
        if (def == null)
        {
            def = ScriptableObject.CreateInstance<VendorDefinition>();
            AssetDatabase.CreateAsset(def, RutaDeLaTienda);
        }
        def.characterName = "TENDERO";
        def.healItemsForSale = (HealItemDefinition[])tixi.healItemsForSale.Clone();
        def.battleItemsForSale = new BattleItemDefinition[0];
        EditorUtility.SetDirty(def);
        AssetDatabase.SaveAssets();
        return def;
    }
}
