using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Llena el catalogo de FX con los prefabs de los dos packs importados, y MIDE
// cuanto ocupa cada uno.
//
// La eleccion no es decorativa. En un boss rush, el color del golpe es la unica
// forma de saber de que tipo es sin frenar a leer, asi que cada tipo cae en un
// efecto que se parezca a el: electrico con chispas azules, planta con hojas
// verdes, roca con piedras y polvo.
//
// Dos criterios que costaron una iteracion entera:
//
//   EL ARCO TIENE QUE SER UN TAJO. Los "AoE slash" de Hovl suenan a lo que hace
//   falta pero son anillos horizontales que se abren en el piso: puestos como
//   estela de una patada quedan como un aro naranja a la altura de la cintura,
//   y se lee como un adorno, no como un golpe. Los que sirven son los tajos
//   propiamente dichos -CFXR _SLASH_, los Sword Trail, los "Charge slash".
//
//   LA MEDIDA SE MIDE, NO SE ESTIMA. Ver ProbarYMedir.
//
// De cada pack se toma lo que hace mejor: Hovl tiene los *hits* separados por
// elemento (Electro, Green, Snow, Stones, Holy, Star), que mapean casi uno a uno
// con los tipos de Pokemon; Cartoon FX trae su propio shader compatible con URP
// y los mejores tajos. Cada entrada lista candidatos en orden de preferencia y
// se queda con el primero que ademas rinda.
public static class FxCatalogBuilder
{
    private const string Carpeta = "Assets/Fx";
    private const string RutaCatalogo = Carpeta + "/CatalogoFx.asset";

    private static readonly string[] CarpetasDePacks =
    {
        "Assets/Hovl Studio",
        "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs",
    };

    // Carpetas que se saltean aunque esten adentro de un pack.
    //
    // "Texts" son carteles de comic: cada efecto deletrea una palabra con un
    // sistema de particulas POR LETRA. El de "CFXR _SLASH_" tiene sus sistemas
    // llamados S, L, A, S, H y !, y suena exactamente a lo que uno busca para un
    // tajo, pero lo que dibuja es la palabra SLASH! flotando en el aire.
    private static readonly string[] CarpetasSalteadas =
    {
        "/Texts/",
    };

    // NINGUNO DE LOS DOS PACKS TIENE UN TAJO QUE SIRVA DE ARCO, y por eso los
    // arcos quedan vacios. Se probaron los tres candidatos, mirando cada uno en
    // pantalla y no por el nombre:
    //
    //   "AoE slash *" de Hovl son anillos HORIZONTALES que se abren en el piso.
    //   Como estela de una patada quedan como un aro naranja a la altura de la
    //   cintura: parece un adorno, no un golpe.
    //
    //   "Charge/Electro/Snow/Stone slash" de Hovl son destellos CIRCULARES de
    //   carga. Puestos delante del Poken se leen como una bola flotando, que es
    //   justo lo que hay que evitar.
    //
    //   Los "Sword Trail" de Cartoon FX son estelas: necesitan que algo se MUEVA
    //   para dibujar el recorrido. Instanciados quietos dejan una raya vertical.
    //   Es el mismo problema que tenian las mallas de FX de UNITE.
    //
    //   Y "CFXR _SLASH_" no es un tajo: es la palabra SLASH! en letras de comic,
    //   con un sistema de particulas por letra (S, L, A, S, H, !).
    //
    // En un golpe cuerpo a cuerpo la estela la hace LA ANIMACION, que para eso
    // son las animaciones reales de UNITE. Lo que aporta el FX es el impacto
    // sobre el rival, y al aire una marca chica en el suelo. El campo sigue en
    // el catalogo para cargarle un tajo a mano a una habilidad puntual, o si
    // aparece un pack con arcos de verdad.
    //
    // tipo -> candidatos de arco (hoy sin usar) y de impacto.
    private static readonly (PokenType tipo, string[] arco, string[] impacto)[] Mapa =
    {
        // Normal usa "Explosion", que a pesar del nombre es un golpe seco de
        // polvo y chispas blancas, no una bola de fuego. Se eligio mirando los
        // cuatro candidatos neutros de Hovl uno al lado del otro: "Sparks white"
        // y "Stones hit" quedan tan tenues que no se ven, y "Star hit" es
        // violeta -los materiales de Hovl son blancos y el color se lo pone el
        // sistema de particulas-, asi que el basico parecia de tipo psiquico.
        (PokenType.Normal,   new[] { "CFXR4 Sword Trail PLAIN (360 Spiral)", "Charge slash blue" },
                             new[] { "Explosion", "CFXR3 Hit Misc A" }),
        (PokenType.Fire,     new[] { "CFXR4 Sword Trail FIRE (360 Spiral)", "Charge slash red" },
                             new[] { "CFXR3 Hit Fire B (Air)", "Red energy explosion" }),
        (PokenType.Water,    new[] { "Charge slash blue", "CFXR4 Sword Trail ICE (360 Spiral)" },
                             new[] { "CFXR Water Splash (Smaller)", "Sparks blue" }),
        (PokenType.Grass,    new[] { "CFXR4 Sword Trail PLAIN (360 Spiral)", "Charge slash blue" },
                             new[] { "Green hit", "CFXR3 Hit Leaves A (Lit)" }),
        (PokenType.Electric, new[] { "Electro slash", "Charge slash blue" },
                             new[] { "Electro hit", "CFXR3 Hit Electric C (Air)" }),
        (PokenType.Ice,      new[] { "CFXR4 Sword Trail ICE (360 Spiral)", "Snow slash" },
                             new[] { "Snow hit", "CFXR3 Hit Ice B (Air)" }),
        (PokenType.Fighting, new[] { "Charge slash red", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "CFXR Hit A (Red)", "Explosion" }),
        (PokenType.Poison,   new[] { "Charge slash purple", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "CFXR2 Poison Cloud", "Green hit" }),
        (PokenType.Ground,   new[] { "Stone slash", "Charge slash red" },
                             new[] { "CFXR2 Ground Hit", "Stones hit" }),
        (PokenType.Flying,   new[] { "Charge slash blue", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "CFXR3 Hit Misc A", "Star hit" }),
        (PokenType.Psychic,  new[] { "Charge slash purple", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "Holy hit", "CFXR3 Hit Light B (Air)" }),
        (PokenType.Bug,      new[] { "CFXR4 Sword Trail PLAIN (360 Spiral)", "Charge slash purple" },
                             new[] { "Green hit", "CFXR3 Hit Leaves A (Lit)" }),
        (PokenType.Rock,     new[] { "Stone slash", "Charge slash red" },
                             new[] { "Stones hit", "CFXR2 Ground Hit" }),
        (PokenType.Ghost,    new[] { "Charge slash purple", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "CFXR2 Souls Escape", "CFXR3 Hit Misc F Smoke" }),
        (PokenType.Dragon,   new[] { "Charge slash purple", "Charge slash blue" },
                             new[] { "CFXR Impact Glowing HDR (Blue)", "Holy hit" }),
        (PokenType.Dark,     new[] { "Charge slash purple", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "CFXR3 Hit Misc F Smoke", "CFXR2 Souls Escape" }),
        (PokenType.Steel,    new[] { "Charge slash blue", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "CFXR Hit D 3D (Yellow)", "Sparks white" }),
        (PokenType.Fairy,    new[] { "Charge slash purple", "CFXR4 Sword Trail PLAIN (360 Spiral)" },
                             new[] { "Love hit", "Holy hit" }),
    };

    private static readonly string[] ArcoPorDefecto = { "CFXR4 Sword Trail PLAIN (360 Spiral)", "Charge slash blue" };
    private static readonly string[] ImpactoPorDefecto = { "CFXR3 Hit Misc A", "Star hit" };

    [MenuItem("PokeRush/3D/Cablear catalogo de FX")]
    public static FxCatalog Cablear()
    {
        Dictionary<string, GameObject> indice = IndexarPrefabs();
        if (indice.Count == 0)
        {
            Debug.LogError("[FX] No hay prefabs en los packs. Revisa que esten importados en " +
                           string.Join(" y ", CarpetasDePacks));
            return null;
        }

        FxCatalog catalogo = CargarOCrear();
        catalogo.entradas.Clear();

        var faltantes = new List<string>();

        foreach ((PokenType tipo, string[] arco, string[] impacto) in Mapa)
        {
            catalogo.entradas.Add(new FxCatalog.Entrada
            {
                tipo = tipo,
                // El arco queda VACIO a proposito. Ver ArcosDesactivados.
                arco = default,
                impacto = Elegir(indice, impacto, faltantes),
            });
        }

        catalogo.arcoPorDefecto = default;
        catalogo.impactoPorDefecto = Elegir(indice, ImpactoPorDefecto, faltantes);

        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();

        int completos = catalogo.entradas.Count(e => e.impacto.Existe);
        Debug.Log($"[FX] Catalogo cableado: {completos} de {Mapa.Length} tipos con impacto propio. " +
                  "Los arcos van vacios a proposito (ver el comentario del mapa).");

        if (faltantes.Count > 0)
        {
            Debug.LogWarning("[FX] Sin candidato usable (no estan en los packs o su shader no " +
                             $"rinde en URP): {string.Join(", ", faltantes.Distinct())}");
        }

        return catalogo;
    }

    // Primer candidato que exista Y que se dibuje bien, ya medido.
    private static FxCatalog.Efecto Elegir(
        Dictionary<string, GameObject> indice, string[] candidatos, List<string> faltantes)
    {
        foreach (string nombre in candidatos)
        {
            if (!indice.TryGetValue(nombre, out GameObject prefab)) continue;
            if (!SeDibuja(prefab)) continue;

            return new FxCatalog.Efecto { prefab = prefab, medida = ProbarYMedir(prefab) };
        }

        faltantes.Add(string.Join(" / ", candidatos));
        return default;
    }

    // Cuanto ocupa el efecto de verdad, en unidades de mundo y a escala 1.
    //
    // Se instancia, se lo hace correr con Simulate y se mira el bounds de sus
    // renderers. Es la unica forma confiable: derivarlo de los parametros del
    // sistema -radio de la forma + velocidad x vida- da mal porque casi ningun
    // efecto viaja en linea recta a velocidad pareja. Medido contra la realidad,
    // esa estimacion daba 15 unidades para "Star hit", que ocupa 9, y el efecto
    // terminaba encogido a 0,6: una bolita flotando sobre el rival.
    //
    // Se muestrea a lo largo del arranque y se toma el maximo, porque un efecto
    // crece: medirlo en un solo instante lo dejaria chico o gigante segun donde
    // caiga la muestra.
    private static float ProbarYMedir(GameObject prefab)
    {
        // En una escena de vista previa, para no ensuciar la que este abierta.
        UnityEngine.SceneManagement.Scene banco = EditorSceneManager.NewPreviewScene();
        GameObject instancia = null;

        try
        {
            instancia = Object.Instantiate(prefab);
            instancia.transform.localScale = Vector3.one;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instancia, banco);

            var sistemas = instancia.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem sistema in sistemas)
            {
                // Semilla fija: si no, dos corridas del cableador dan medidas
                // distintas y el tamaño de los golpes cambia sin que nadie lo
                // haya tocado.
                sistema.useAutoRandomSeed = false;
                ParticleSystem.MainModule principal = sistema.main;
                principal.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            var renderers = instancia.GetComponentsInChildren<Renderer>(true);
            if (sistemas.Length == 0 || renderers.Length == 0) return 0f;

            float medida = 0f;
            for (int paso = 1; paso <= 10; paso++)
            {
                float t = paso * 0.08f;
                foreach (ParticleSystem sistema in sistemas)
                    sistema.Simulate(t, true, true, false);

                foreach (Renderer render in renderers)
                {
                    if (!render.enabled) continue;

                    Vector3 tamaño = render.bounds.size;
                    medida = Mathf.Max(medida, Mathf.Max(tamaño.x, Mathf.Max(tamaño.y, tamaño.z)));
                }
            }
            return medida;
        }
        finally
        {
            if (instancia != null) Object.DestroyImmediate(instancia);
            EditorSceneManager.ClosePreviewScene(banco);
        }
    }

    // Shaders del pipeline viejo que URP NO dibuja: son los que dejan el objeto
    // rosa. Son de superficie, con pases marcados para el forward de built-in
    // que URP directamente saltea.
    //
    // Los de particulas heredados NO estan aca a proposito: su pase no declara
    // LightMode, y un pase sin LightMode cuenta como SRPDefaultUnlit, que URP si
    // dibuja. Por eso los packs viejos de efectos se ven bien igual.
    private static readonly string[] ShadersQueUrpNoDibuja =
    {
        "Standard",
        "Standard (Specular setup)",
        "Standard (Roughness setup)",
        "Autodesk Interactive",
    };

    private static bool SeDibuja(GameObject prefab)
    {
        bool alguno = false;

        foreach (Renderer render in prefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in render.sharedMaterials)
            {
                // Un hueco de material NO descalifica al prefab.
                //
                // Casi todos los prefabs de Cartoon FX tienen algun renderer con
                // el slot vacio -sub-emisores, estelas apagadas, sistemas que se
                // encienden por codigo. Descartarlos por eso dejaba afuera el
                // pack entero, incluidos los tajos, que son justo los que mejor
                // sirven. Lo que descalifica es un material que EXISTE y no se
                // puede dibujar.
                if (material == null) continue;
                if (material.shader == null) return false;

                // isSupported dice si el shader COMPILA para esta plataforma, no
                // si este pipeline lo dibuja. Hacen falta las dos preguntas: un
                // material de built-in compila perfecto y aun asi sale rosa.
                if (!material.shader.isSupported) return false;
                if (System.Array.IndexOf(ShadersQueUrpNoDibuja, material.shader.name) >= 0) return false;

                alguno = true;
            }
        }

        // Sin un solo material utilizable no hay nada que dibujar.
        return alguno;
    }

    private static Dictionary<string, GameObject> IndexarPrefabs()
    {
        var indice = new Dictionary<string, GameObject>();

        string[] carpetas = CarpetasDePacks.Where(AssetDatabase.IsValidFolder).ToArray();
        if (carpetas.Length == 0) return indice;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", carpetas))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (CarpetasSalteadas.Any(c => ruta.Replace('\\', '/').Contains(c))) continue;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (prefab == null) continue;

            // Sin sobrescribir: si dos packs traen el mismo nombre gana el
            // primero, y el orden de CarpetasDePacks es el de preferencia.
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            if (!indice.ContainsKey(nombre)) indice[nombre] = prefab;
        }
        return indice;
    }

    private static FxCatalog CargarOCrear()
    {
        var catalogo = AssetDatabase.LoadAssetAtPath<FxCatalog>(RutaCatalogo);
        if (catalogo != null) return catalogo;

        // Se crea la carpeta en disco y se refresca en vez de confiar en
        // IsValidFolder: si alguien la borro por fuera del editor, la base de
        // assets sigue diciendo que existe y CreateAsset falla.
        string absoluta = Path.Combine(Application.dataPath, "Fx");
        if (!Directory.Exists(absoluta))
        {
            Directory.CreateDirectory(absoluta);
            AssetDatabase.Refresh();
        }

        catalogo = ScriptableObject.CreateInstance<FxCatalog>();
        AssetDatabase.CreateAsset(catalogo, RutaCatalogo);
        return catalogo;
    }
}
