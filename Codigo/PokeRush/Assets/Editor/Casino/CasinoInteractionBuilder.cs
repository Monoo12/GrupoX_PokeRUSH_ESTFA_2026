using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Cuelga las interacciones del casino sobre el arte que ya dibujo el equipo de
// nivel.
//
// Los muebles del casino son TILES pintados en la capa 'Colisiones', no objetos:
// no hay una tragaperras a la que agregarle un componente. Lo que hace esto es
// buscar cada mueble por su forma (una tragaperras ocupa 4x3 celdas, una mesa de
// ruleta 4x2...) y crear encima un objeto vacio con collider que es el que el
// jugador interactua. No se pinta, no se mueve y no se borra ni un tile.
//
// Es idempotente: borra las interacciones que puso antes y las vuelve a crear,
// asi correrlo de nuevo despues de que el equipo mueva un mueble las reubica.
// Todo lo que crea son GameObjects comunes: se pueden mover a mano en Unity.
public static class CasinoInteractionBuilder
{
    private const int CapaObstacles = 3;
    private const string Contenedor = "InteraccionesCasino";
    private const string MaquinaBasica = "Assets/Resources/Definitions/Casino/MaquinaBasica.asset";

    // Que mecanica lleva el mueble principal de cada piso.
    private enum Mecanica { Ninguna, Slots, RuletaDePremios }

    // Configuracion de un piso. Sale de mirar las escenas con
    // PokeRush/Casino/Analizar los cuatro pisos.
    private class Piso
    {
        public string Escena;
        // Forma de los muebles que llevan la mecanica principal, "anchoxalto".
        public string FormaDelMueble;
        public string PrefijoDelMueble;
        public Mecanica Mecanica;
        // Posicion de las puertas del ascensor (leida de la escena).
        public Vector2 Ascensor;
        public string EscenaDestino;
        public string NombreDelDestino;
        public CasinoRequisitoTipo Requisito;
        public int DineroRequerido;
        public StoryFlag FlagRequerido;
        public StoryFlag OtorgaFlag;
    }

    private static readonly Piso[] Pisos =
    {
        new Piso
        {
            Escena = "Assets/Scenes/Casino Nivel 1.unity",
            FormaDelMueble = "4x3", PrefijoDelMueble = "Tragaperras",
            Mecanica = Mecanica.Slots,
            Ascensor = new Vector2(0.5f, 20.5f),
            EscenaDestino = "Casino Nivel 2", NombreDelDestino = "el piso 2",
            Requisito = CasinoRequisitoTipo.DineroMinimo, DineroRequerido = 500,
            OtorgaFlag = StoryFlag.Casino_Piso1_Superado
        },
        new Piso
        {
            Escena = "Assets/Scenes/Casino Nivel 2.unity",
            // Las seis mesas de ruleta del piso 2 ocupan 4x2 celdas cada una.
            FormaDelMueble = "4x2", PrefijoDelMueble = "Ruleta",
            Mecanica = Mecanica.RuletaDePremios,
            Ascensor = new Vector2(0.5f, 20.5f),
            EscenaDestino = "Casino nivel 3", NombreDelDestino = "el piso 3",
            Requisito = CasinoRequisitoTipo.Flag,
            FlagRequerido = StoryFlag.Casino_Ruleta_ObjetivoCumplido,
            OtorgaFlag = StoryFlag.Casino_Piso2_Superado
        },
        new Piso
        {
            Escena = "Assets/Scenes/Casino nivel 3.unity",
            FormaDelMueble = null, PrefijoDelMueble = null,   // el blackjack llega despues
            Ascensor = new Vector2(0.5f, 20.5f),
            EscenaDestino = "Casino piso FINAL", NombreDelDestino = "el ultimo piso",
            Requisito = CasinoRequisitoTipo.Flag,
            FlagRequerido = StoryFlag.Casino_Blackjack_Ganado,
            OtorgaFlag = StoryFlag.Casino_Piso3_Superado
        }
    };

    [MenuItem("PokeRush/Casino/Colgar las interacciones en las escenas")]
    public static void ColgarTodas()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[CasinoInter] Sali del Play Mode antes de correr esto.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (Piso piso in Pisos)
        {
            if (!System.IO.File.Exists(piso.Escena))
            {
                Debug.LogWarning($"[CasinoInter] No existe {piso.Escena}, se saltea.");
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(piso.Escena, OpenSceneMode.Single);
            Colgar(scene, piso);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[CasinoInter] TERMINADO.");
    }

    private static void Colgar(Scene scene, Piso piso)
    {
        GameObject raiz = PrepararContenedor(scene);

        int muebles = piso.FormaDelMueble == null ? 0 : ColgarMuebles(raiz, piso);
        ColgarAscensor(raiz, piso);

        Debug.Log($"[CasinoInter] {scene.name}: {muebles} mueble(s) con interaccion " +
                  $"+ ascensor a '{piso.EscenaDestino}'.");
    }

    // El contenedor va DENTRO de MapaRaiz: lo que quede afuera se sigue viendo y
    // funcionando durante el combate, y un cartel de tragaperras encima de una
    // pelea no tiene sentido.
    private static GameObject PrepararContenedor(Scene scene)
    {
        GameObject mapaRaiz = null;
        foreach (GameObject r in scene.GetRootGameObjects())
            if (r.name == "MapaRaiz") mapaRaiz = r;

        if (mapaRaiz == null)
        {
            Debug.LogWarning("[CasinoInter] No hay MapaRaiz: corre primero " +
                             "PokeRush/Casino/Armar el rig de las escenas del casino.");
            mapaRaiz = new GameObject("MapaRaiz");
            SceneManager.MoveGameObjectToScene(mapaRaiz, scene);
        }

        Transform vieja = mapaRaiz.transform.Find(Contenedor);
        if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

        var go = new GameObject(Contenedor);
        go.transform.SetParent(mapaRaiz.transform);
        return go;
    }

    private static int ColgarMuebles(GameObject raiz, Piso piso)
    {
        Tilemap colisiones = CasinoDecoradoScanner.BuscarTilemap("Colisiones");
        if (colisiones == null)
        {
            Debug.LogWarning("[CasinoInter] No hay capa 'Colisiones' en la escena.");
            return 0;
        }

        var maquina = AssetDatabase.LoadAssetAtPath<SlotMachineDefinition>(MaquinaBasica);

        if (piso.Mecanica == Mecanica.Slots && maquina == null)
            Debug.LogWarning($"[CasinoInter] Falta {MaquinaBasica}: las maquinas quedan sin definicion.");

        int puestos = 0;
        foreach (List<Vector3Int> pieza in CasinoDecoradoScanner.Agrupar(colisiones))
        {
            string forma = $"{CasinoDecoradoScanner.Ancho(pieza)}x{CasinoDecoradoScanner.Alto(pieza)}";
            if (forma != piso.FormaDelMueble) continue;

            puestos++;
            Vector3 centroAbajo = CasinoDecoradoScanner.CentroAbajo(pieza, colisiones);

            var go = new GameObject($"{piso.PrefijoDelMueble}_{puestos:00}");
            go.transform.SetParent(raiz.transform);
            go.transform.position = centroAbajo;
            go.layer = CapaObstacles;

            // El collider cubre la fila de abajo del mueble: es la celda a la
            // que mira el jugador cuando se para delante y aprieta X.
            //
            // Las tragaperras ademas llevan la fila del medio, porque tienen los
            // banquitos a los costados: sentado ahi el jugador mira esa fila, y
            // como el mueble frena (Colisiones con Grid) no hay otra forma de
            // llegarle. La fila de arriba queda afuera para que no se juegue
            // desde atras de la maquina.
            int filas = piso.Mecanica == Mecanica.Slots
                ? Mathf.Max(1, CasinoDecoradoScanner.Alto(pieza) - 1)
                : 1;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(CasinoDecoradoScanner.Ancho(pieza), filas);
            col.offset = new Vector2(0f, (filas - 1) * 0.5f);
            col.isTrigger = true;

            // Cada piso cuelga lo suyo. El collider y la posicion son iguales:
            // lo unico que cambia es que componente lleva encima.
            if (piso.Mecanica == Mecanica.RuletaDePremios)
            {
                // Sin premios asignados: se queda con los ocho que trae cargados
                // el prefab de la pantalla. La primera mesa es la que destraba
                // el ascensor; las otras son las mismas pero sin el flag, para
                // que girar de nuevo no vuelva a cantar el cartel del objetivo.
                var comp = go.AddComponent<PrizeWheelInteractable>();
                var so = new SerializedObject(comp);
                so.FindProperty("otorgaFlag").enumValueIndex =
                    puestos == 1 ? (int)StoryFlag.Casino_Ruleta_ObjetivoCumplido
                                 : (int)StoryFlag.None;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                var comp = go.AddComponent<SlotMachineInteractable>();
                if (maquina != null)
                {
                    var so = new SerializedObject(comp);
                    so.FindProperty("maquina").objectReferenceValue = maquina;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        return puestos;
    }

    private static void ColgarAscensor(GameObject raiz, Piso piso)
    {
        var go = new GameObject("Ascensor");
        go.transform.SetParent(raiz.transform);
        go.transform.position = new Vector3(piso.Ascensor.x, piso.Ascensor.y, 0f);
        go.layer = CapaObstacles;

        // Ancho generoso: las puertas son dos y el jugador puede pararse frente
        // a cualquiera de las dos, o al medio.
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(4f, 1f);
        col.isTrigger = true;

        var ascensor = go.AddComponent<CasinoElevatorInteractable>();
        var so = new SerializedObject(ascensor);
        so.FindProperty("escenaDestino").stringValue = piso.EscenaDestino;
        so.FindProperty("nombreDelDestino").stringValue = piso.NombreDelDestino;
        so.FindProperty("requisito").enumValueIndex = (int)piso.Requisito;
        so.FindProperty("dineroRequerido").intValue = piso.DineroRequerido;
        so.FindProperty("flagRequerido").enumValueIndex = (int)piso.FlagRequerido;
        so.FindProperty("otorgaFlag").enumValueIndex = (int)piso.OtorgaFlag;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
