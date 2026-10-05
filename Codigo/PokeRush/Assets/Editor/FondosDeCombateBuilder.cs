using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Elige el fondo de combate de cada lugar y de los rivales que tienen uno
// propio (playtest del 5-oct: "mejora los fondos de las peleas").
//
//   PokeRush > Combate > Elegir los fondos de combate
//
// Antes casi todo se peleaba en el pasto del prefab (Pradera_bg). Ahora:
//
//   Casino Nivel 1     GimnasioSabrina  luces de ciudad y piso de neon
//   Casino Nivel 2     Castillo         salon oscuro con alfombra roja
//   Casino nivel 3     Mansion          marmol a cuadros, el piso VIP
//   Casino piso FINAL  BaseVillano      la oficina de Reymar (los guardias)
//   NestorGOD2         Central          sala de maquinas: el Data Center
//   habitacion         Gimnasio5        el cuarto de Santiago, de madera
//   Quarcho-Cueva      Extra            el Entrenamiento intenso del chip
//   BossFinal          Laboratorio      el de Julieta, para toda la arena
//
// Y por rival (manda sobre el del lugar):
//
//   Reymar (JefeFinalCasino)   Campeon   la pelea grande del casino
//   Rival del tutorial         Gimnasio5 (antes Interior, un blanco liso)
//   Nodos del firewall         Espacio   el nucleo, "adentro" de la red
//
// Nestor (su arte propio) y Julieta (Laboratorio) quedan como estaban.
//
// Se puede volver a correr: pisa solo las filas y los rivales de arriba.
public static class FondosDeCombateBuilder
{
    private const string Carpeta = "Assets/Resources/Definitions/Combate";
    private const string Ruta = Carpeta + "/FondosDeCombate.asset";
    private const string Fondos = "Assets/Sprites/Battlebacks/";
    private const string Definiciones = "Assets/Resources/Definitions/";

    private static readonly (string escena, string fondo)[] PorEscena =
    {
        ("Casino Nivel 1", "GimnasioSabrina_bg.png"),
        ("Casino Nivel 2", "Castillo_bg.png"),
        ("Casino nivel 3", "Mansion_bg.png"),
        ("Casino piso FINAL", "BaseVillano_bg.png"),
        ("NestorGOD2", "Central_bg.png"),
        ("habitacion", "Gimnasio5_bg.png"),
        ("Quarcho-Cueva", "Extra_bg.png"),
        ("BossFinal", "Laboratorio_bg.png"),
    };

    private static readonly (string rival, string fondo)[] PorRival =
    {
        ("Casino/JefeFinalCasino", "Campeon_bg.png"),
        ("Tutorial/RivalDelTutorial", "Gimnasio5_bg.png"),
        ("Torre1/NodoFirewall", "Espacio_bg.png"),
        ("Torre1/NodoFirewallA", "Espacio_bg.png"),
        ("Torre1/NodoFirewallB", "Espacio_bg.png"),
        ("Torre1/NodoFirewallC", "Espacio_bg.png"),
    };

    // Los que usan los builders del casino y del tutorial al rearmar.
    public const string FondoDeReymar = Fondos + "Campeon_bg.png";
    public const string FondoDelRivalDelTutorial = Fondos + "Gimnasio5_bg.png";

    [MenuItem("PokeRush/Combate/Elegir los fondos de combate")]
    public static void Elegir()
    {
        FondosDeCombate tabla = AssetDatabase.LoadAssetAtPath<FondosDeCombate>(Ruta);
        if (tabla == null)
        {
            MenuUiKit.EnsureFolder(Carpeta);
            tabla = ScriptableObject.CreateInstance<FondosDeCombate>();
            AssetDatabase.CreateAsset(tabla, Ruta);
        }

        foreach (var (escena, archivo) in PorEscena)
        {
            Sprite fondo = Fondo(archivo);
            if (fondo == null) continue;
            FondosDeCombate.FondoDeUnaEscena fila = tabla.porEscena.Find(f => f != null && f.escena == escena);
            if (fila == null)
            {
                fila = new FondosDeCombate.FondoDeUnaEscena { escena = escena };
                tabla.porEscena.Add(fila);
            }
            fila.fondo = fondo;
        }
        EditorUtility.SetDirty(tabla);

        var sinAsset = new List<string>();
        foreach (var (rival, archivo) in PorRival)
        {
            Sprite fondo = Fondo(archivo);
            Object def = AssetDatabase.LoadMainAssetAtPath($"{Definiciones}{rival}.asset");
            if (fondo == null) continue;
            if (def is EnemyDefinition e) e.battleBackground = fondo;
            else if (def is BossDefinition b) b.battleBackground = fondo;
            else { sinAsset.Add(rival); continue; }
            EditorUtility.SetDirty(def);
        }

        AssetDatabase.SaveAssets();
        if (sinAsset.Count > 0)
            Debug.LogWarning("[Combate] No se encontraron estos rivales: " + string.Join(", ", sinAsset));
        Debug.Log($"[Combate] Fondos de combate elegidos: {PorEscena.Length} lugares y {PorRival.Length - sinAsset.Count} rivales.");
    }

    private static Sprite Fondo(string archivo)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(Fondos + archivo);
        if (s == null) Debug.LogError($"[Combate] Falta {Fondos}{archivo}.");
        return s;
    }
}
