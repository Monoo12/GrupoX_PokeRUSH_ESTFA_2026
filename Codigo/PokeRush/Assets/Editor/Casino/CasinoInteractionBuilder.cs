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
    //
    // RuletaDePremios ya no la usa ningun piso: quedo del minijuego de regalos
    // que estuvo un rato en el piso 2 antes de que llegara la mesa de apuestas.
    // Se deja porque el componente sigue existiendo y se puede volver a colgar.
    private enum Mecanica { Ninguna, Slots, RuletaDePremios, MesaDeRuleta, Blackjack }

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
        // Cartel del ascensor cuando falta el objetivo. Vacio = el del componente.
        public string MensajeFaltaObjetivo;
        // Con requisito = TenerPokemon: path de la especie que hay que tener shiny,
        // y el cartel cuando falta ({0} = la especie).
        public string EspecieRequerida;
        public string MensajeFaltaPokemon;
        // Pisos a los que baja el ascensor, del mas cercano al mas lejano.
        public (string escena, string nombre)[] PisosDeAbajo;
        // Mesas de ruleta: la ruleta de premios que abren al ganar al color.
        public string RuletaPorColor;
        public int UmbralRuletaPorColor;
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
            // El pase lo da el guardia de al lado (pagandole o ganandole), ver
            // CasinoElevatorGuard. Antes pedia tener 500 juntados.
            Requisito = CasinoRequisitoTipo.Flag,
            FlagRequerido = StoryFlag.Casino_Piso1_PaseAscensor,
            MensajeFaltaObjetivo = "El guardia no te deja subir. Hablá con él: se le paga o se le gana.",
            OtorgaFlag = StoryFlag.Casino_Piso1_Superado
        },
        new Piso
        {
            Escena = "Assets/Scenes/Casino Nivel 2.unity",
            // Las seis mesas de ruleta del piso 2 ocupan 4x2 celdas cada una.
            FormaDelMueble = "4x2", PrefijoDelMueble = "Ruleta",
            Mecanica = Mecanica.MesaDeRuleta,
            Ascensor = new Vector2(0.5f, 20.5f),
            EscenaDestino = "Casino nivel 3", NombreDelDestino = "el piso 3",
            // El pase lo da el guardia VIP de al lado: mostrandole la Tarjeta VIP
            // o ganandole (ver CasinoElevatorGuard y CasinoNpcBuilder).
            Requisito = CasinoRequisitoTipo.Flag,
            FlagRequerido = StoryFlag.Casino_Piso2_PaseAscensor,
            MensajeFaltaObjetivo = "El guardia VIP no te deja subir. Hablá con él: con la Tarjeta VIP o ganándole.",
            OtorgaFlag = StoryFlag.Casino_Piso2_Superado,
            PisosDeAbajo = new[] { ("Casino Nivel 1", "PISO 1") },
            RuletaPorColor = "Assets/Resources/Definitions/Casino/RuletaDeLaMesa.asset",
            UmbralRuletaPorColor = 200
        },
        new Piso
        {
            Escena = "Assets/Scenes/Casino nivel 3.unity",
            // Las cuatro mesas de cartas del piso 3 ocupan 4x3 celdas cada una.
            // Son las semicirculares con fieltro verde de la mitad izquierda;
            // las 2x2 de la derecha son las mesas del bar y no llevan nada.
            FormaDelMueble = "4x3", PrefijoDelMueble = "Blackjack",
            Mecanica = Mecanica.Blackjack,
            Ascensor = new Vector2(0.5f, 20.5f),
            EscenaDestino = "Casino piso FINAL", NombreDelDestino = "el ultimo piso",
            // Hay que tener el Cosmog shiny, que solo se le gana a Tacconi.
            Requisito = CasinoRequisitoTipo.TenerPokemon,
            EspecieRequerida = "Assets/Resources/Definitions/Pokens/0789_Cosmog.asset",
            MensajeFaltaPokemon = "El de seguridad no te deja subir: arriba solo entran los que tienen un {0} shiny. Dicen que Tacconi tiene uno.",
            OtorgaFlag = StoryFlag.Casino_Piso3_Superado,
            PisosDeAbajo = new[] { ("Casino Nivel 2", "PISO 2"), ("Casino Nivel 1", "PISO 1") }
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
            // En los tres casos vale la misma regla: solo el PRIMER mueble
            // otorga el flag del piso. Si lo dieran todos, volver a jugar en
            // otra mesa cantaria de nuevo el cartel del objetivo.
            if (piso.Mecanica == Mecanica.Blackjack)
            {
                OtorgarSoloElPrimero(go.AddComponent<BlackjackTableInteractable>(),
                    puestos, StoryFlag.Casino_Blackjack_Ganado);
            }
            else if (piso.Mecanica == Mecanica.MesaDeRuleta)
            {
                var mesa = go.AddComponent<RouletteTableInteractable>();
                OtorgarSoloElPrimero(mesa, puestos, StoryFlag.Casino_Ruleta_ObjetivoCumplido);

                // Todas las mesas abren la ruleta de premios al ganar al color.
                if (!string.IsNullOrEmpty(piso.RuletaPorColor))
                {
                    var so = new SerializedObject(mesa);
                    so.FindProperty("ruletaPorColor").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<CasinoPrizeTable>(piso.RuletaPorColor);
                    so.FindProperty("umbralRuletaPorColor").intValue = piso.UmbralRuletaPorColor;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else if (piso.Mecanica == Mecanica.RuletaDePremios)
            {
                // Sin premios asignados: se queda con los ocho que trae cargados
                // el prefab de la pantalla.
                OtorgarSoloElPrimero(go.AddComponent<PrizeWheelInteractable>(),
                    puestos, StoryFlag.Casino_Ruleta_ObjetivoCumplido);
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

    // Le pone el flag del piso al primer mueble y None a los demas. Los tres
    // interactuables lo declaran con el mismo nombre de campo, asi que sale una
    // sola vez en vez de tres copias de lo mismo.
    private static void OtorgarSoloElPrimero(Component comp, int cual, StoryFlag flag)
    {
        var so = new SerializedObject(comp);
        SerializedProperty prop = so.FindProperty("otorgaFlag");
        if (prop == null)
        {
            Debug.LogWarning($"[CasinoInter] {comp.GetType().Name} no tiene 'otorgaFlag'.");
            return;
        }

        prop.enumValueIndex = cual == 1 ? (int)flag : (int)StoryFlag.None;
        so.ApplyModifiedPropertiesWithoutUndo();
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
        if (!string.IsNullOrEmpty(piso.MensajeFaltaObjetivo))
            so.FindProperty("mensajeFaltaObjetivo").stringValue = piso.MensajeFaltaObjetivo;

        if (!string.IsNullOrEmpty(piso.EspecieRequerida))
        {
            so.FindProperty("especieRequerida").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<PokenDefinition>(piso.EspecieRequerida);
            so.FindProperty("debeSerShiny").boolValue = true;
        }
        if (!string.IsNullOrEmpty(piso.MensajeFaltaPokemon))
            so.FindProperty("mensajeFaltaPokemon").stringValue = piso.MensajeFaltaPokemon;

        var abajo = piso.PisosDeAbajo ?? new (string escena, string nombre)[0];
        SerializedProperty pisos = so.FindProperty("pisosDeAbajo");
        pisos.arraySize = abajo.Length;
        for (int i = 0; i < abajo.Length; i++)
        {
            pisos.GetArrayElementAtIndex(i).FindPropertyRelative("escena").stringValue = abajo[i].escena;
            pisos.GetArrayElementAtIndex(i).FindPropertyRelative("nombre").stringValue = abajo[i].nombre;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
