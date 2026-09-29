using UnityEditor;
using UnityEngine;

// Los assets de la historia repartida por los niveles: los dialogos (con
// varios que hablan, variantes, preguntas y lo que sigue) y los objetos clave
// que se entregan en el camino. Los builders los enganchan en las escenas
// desde aca, por ruta, asi volver a armar una escena no los pierde.
//
// Los textos NO se escriben aca: viven en los assets de
// Resources/Definitions (ver Historia_En_Los_Niveles.md).
public static class HistoriaAssets
{
    public const string Definiciones = "Assets/Resources/Definitions/";

    private const string SpriteHalo = "Assets/Sprites/UI/Battle/ballBurst_particle.png";
    private const string SpriteEstrella = "Assets/Sprites/UI/Battle/ballBurst_star.png";

    // Los objetos clave de la historia y el icono del pack que les toca.
    public static readonly (string archivo, string icono)[] Objetos =
    {
        ("CartaAnonima", "Assets/Graphics/Items/AIRMAIL.png"),
        ("RaspberryPy", "Assets/Graphics/Items/UPGRADE.png"),
        ("ProtocoloNestor", "Assets/Graphics/Items/DUBIOUSDISC.png"),
        ("TarjetaPiso4", "Assets/Graphics/Items/REDCARD.png"),
    };

    // "Torre1/Dialogos/DC_Guardia" -> el asset. Null (y aviso) si no existe.
    public static DialogueDefinition Dialogo(string ruta)
    {
        string path = Definiciones + ruta + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<DialogueDefinition>(path);
        if (d == null) Debug.LogWarning($"[Historia] No existe el dialogo {path}.");
        return d;
    }

    public static SpecialItemDefinition Objeto(string archivo)
        => AssetDatabase.LoadAssetAtPath<SpecialItemDefinition>($"{Definiciones}Items/{archivo}.asset");

    public static void PonerLista(SerializedProperty lista, params string[] rutas)
    {
        lista.arraySize = rutas.Length;
        for (int i = 0; i < rutas.Length; i++)
            lista.GetArrayElementAtIndex(i).objectReferenceValue = Dialogo(rutas[i]);
    }

    // El brillito de lo que se puede tocar, con otro color que el de los
    // subsistemas (dorado) para que no se confunda con un objetivo.
    public static BrilloInteractuable Brillo(GameObject go, StoryFlag apaga, StoryFlag prende, Color halo)
    {
        var brillo = go.AddComponent<BrilloInteractuable>();
        var so = new SerializedObject(brillo);
        so.FindProperty("flagQueLoApaga").enumValueIndex = (int)apaga;
        so.FindProperty("flagQueLoPrende").enumValueIndex = (int)prende;
        so.FindProperty("spriteHalo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteHalo);
        so.FindProperty("spriteEstrella").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteEstrella);
        so.FindProperty("colorHalo").colorValue = halo;
        so.FindProperty("tamanioHalo").floatValue = 1.0f;
        so.FindProperty("tamanioEstrella").floatValue = 0.3f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return brillo;
    }

    [MenuItem("PokeRush/Historia/Poner iconos a los objetos de la historia")]
    public static void PonerIconos()
    {
        int puestos = 0;
        foreach (var (archivo, icono) in Objetos)
        {
            SpecialItemDefinition item = Objeto(archivo);
            if (item == null) { Debug.LogError($"[Historia] Falta el objeto {archivo}."); continue; }

            Sprite sprite = CasinoNpcBuilder.SpriteSimple(icono);
            if (sprite == null) { Debug.LogError($"[Historia] No se pudo cargar el icono {icono}."); continue; }
            if (item.icon == sprite) continue;

            item.icon = sprite;
            EditorUtility.SetDirty(item);
            puestos++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Historia] Iconos de objetos clave: {puestos} puestos. " +
                  "Los PNG son del pack (Assets/Graphics, en .gitignore): se suben con git add -f.");
    }
}
