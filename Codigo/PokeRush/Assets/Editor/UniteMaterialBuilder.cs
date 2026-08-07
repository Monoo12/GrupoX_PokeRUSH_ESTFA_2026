using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Arma materiales para las especies de UNITE cuyo FBX no trae descripcion de
// material parseable. Son 20 de las 78 jugables.
//
// PROBLEMA 1 - el material compartido.
// Esas 20 importan con CERO materiales como sub-asset, asi que Unity les asigna
// su "Lit" por defecto, que vive en Packages/com.unity.render-pipelines.universal
// y esta COMPARTIDO entre todas. Se ven gris 0.5. No se puede arreglar desde el
// postprocessor: escribirle texturas se las escribe a las 20 a la vez (todas
// terminan con la textura de la ultima procesada) y ademas modifica un paquete
// inmutable. Y los materiales creados con "new Material()" dentro de
// OnPostprocessModel no se persisten: quedan nulos al terminar la importacion.
// Por eso aca se crean materiales de verdad, como asset.
//
// PROBLEMA 2 - las UV en mosaico.
// Varias de esas mallas tienen UNA sola submalla pero UV que se salen de 0..1:
// Snorlax llega a y=2, gyarados a x=2, Espeon va de y=-1 a y=2. Es un layout
// tipo UDIM, donde cada mosaico debia muestrear una textura distinta (cuerpo,
// ojos, boca). Con una sola submalla no hay forma de asignar varias texturas,
// asi que la cara terminaba repitiendo la textura del cuerpo: el "bug de cara".
// La solucion es componer un atlas con cada textura en su mosaico y ajustar el
// tiling del material para que las UV originales lo recorran tal cual.
public static class UniteMaterialBuilder
{
    private const string CarpetaRaiz = "Assets/_AuditRank";
    private const string SufijoGenerado = "_generado.mat";

    [MenuItem("PokeRush/3D/Reparar materiales de UNITE")]
    public static void Reparar()
    {
        int reparados = 0;

        foreach (string carpeta in Directory.GetDirectories(Application.dataPath + "/_AuditRank"))
        {
            string especie = Path.GetFileName(carpeta);
            if (especie.StartsWith("_")) continue;

            GameObject modelo = CargarModelo(especie);
            if (modelo == null) continue;

            foreach (Renderer renderer in modelo.GetComponentsInChildren<Renderer>(true))
            {
                if (!TieneMaterialRoto(renderer)) continue;
                if (RepararRenderer(especie, renderer)) reparados++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int enEscena = AsignarEnEscena();
        Debug.Log($"[UNITE] Especies reparadas: {reparados}. Slots corregidos en la escena: {enEscena}.");
    }

    private static GameObject CargarModelo(string especie)
    {
        string[] fbx = Directory.GetFiles(Application.dataPath + "/_AuditRank/" + especie, "*.fbx");
        return fbx.Length == 0
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>($"{CarpetaRaiz}/{especie}/{Path.GetFileName(fbx[0])}");
    }

    // El "Lit" de URP vive en Packages/: si un renderer lo usa, es que Unity no
    // pudo armarle el material propio.
    private static bool TieneMaterialRoto(Renderer renderer)
    {
        foreach (Material material in renderer.sharedMaterials)
        {
            if (material == null) return true;
            if (AssetDatabase.GetAssetPath(material).StartsWith("Packages/")) return true;
        }
        return false;
    }

    private static bool RepararRenderer(string especie, Renderer renderer)
    {
        Mesh malla = MallaDe(renderer);
        if (malla == null) return false;

        string id = IdDe(malla.name);
        List<Vector2Int> mosaicos = MosaicosDe(malla);
        List<string> difusas = TexturasOrdenadas(especie, "t_d_", id);
        if (difusas.Count == 0) return false;

        Material material = ObtenerOCrear(especie, id);

        if (mosaicos.Count <= 1)
        {
            // Caso simple: una sola region de UV, una sola textura.
            Aplicar(material, Cargar(especie, difusas[0]), Cargar(especie, PrimeraNormal(especie, id)),
                    Vector2.one, Vector2.zero);
        }
        else
        {
            // Caso mosaico: hay que componer el atlas.
            //
            // Solo la difusa. El atlas de normales NO se arma, por dos razones:
            // los t_n_ ya importados vienen con los canales permutados (DXT5nm),
            // asi que releerlos y re-guardarlos los permuta otra vez y quedan
            // corruptos; y casi siempre hay un solo t_n_ para varios mosaicos,
            // con lo cual los mosaicos sobrantes quedarian en negro, que decodifica
            // como normal de longitud cero y apaga la iluminacion de esa zona.
            Vector2 escala, offset;
            Texture2D atlasD = ComponerAtlas(especie, "d", mosaicos, difusas, true, out escala, out offset);
            Aplicar(material, atlasD, null, escala, offset);
        }

        EditorUtility.SetDirty(material);
        return true;
    }

    private static void Aplicar(Material material, Texture2D difusa, Texture2D normal, Vector2 escala, Vector2 offset)
    {
        if (difusa == null) return;

        material.SetTexture("_BaseMap", difusa);
        material.SetTexture("_MainTex", difusa);
        material.SetColor("_BaseColor", Color.white);

        // URP Lit usa _BaseMap_ST para todas sus texturas, asi que con esto
        // queda alineado tambien el normal map.
        material.SetTextureScale("_BaseMap", escala);
        material.SetTextureOffset("_BaseMap", offset);

        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
        }

        material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.12f);
    }

    // --- Mosaicos -----------------------------------------------------------

    // Devuelve los mosaicos de UV que usa la malla, del que mas vertices tiene
    // al que menos. El mas poblado es siempre el cuerpo.
    private static List<Vector2Int> MosaicosDe(Mesh malla)
    {
        var conteo = new Dictionary<Vector2Int, int>();
        foreach (Vector2 uv in malla.uv)
        {
            var mosaico = new Vector2Int(Mathf.FloorToInt(uv.x), Mathf.FloorToInt(uv.y));
            conteo.TryGetValue(mosaico, out int previo);
            conteo[mosaico] = previo + 1;
        }

        var lista = new List<Vector2Int>(conteo.Keys);
        lista.Sort((a, b) =>
        {
            int porVertices = conteo[b].CompareTo(conteo[a]);
            if (porVertices != 0) return porVertices;
            // A igual cantidad (gyarados parte el cuerpo en dos mitades
            // exactas) decide la posicion, para que sea reproducible.
            return a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
        });
        return lista;
    }

    private static Texture2D ComponerAtlas(string especie, string tipo, List<Vector2Int> mosaicos,
                                           List<string> texturas, bool esColor,
                                           out Vector2 escala, out Vector2 offset)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector2Int m in mosaicos)
        {
            minX = Mathf.Min(minX, m.x); maxX = Mathf.Max(maxX, m.x);
            minY = Mathf.Min(minY, m.y); maxY = Mathf.Max(maxY, m.y);
        }
        int columnas = maxX - minX + 1;
        int filas = maxY - minY + 1;

        escala = new Vector2(1f / columnas, 1f / filas);
        offset = new Vector2(-minX / (float)columnas, -minY / (float)filas);

        int lado = 0;
        foreach (string nombre in texturas)
        {
            Texture2D t = Cargar(especie, nombre);
            if (t != null) lado = Mathf.Max(lado, Mathf.Max(t.width, t.height));
        }
        if (lado == 0) return null;

        var atlas = new Texture2D(lado * columnas, lado * filas, TextureFormat.RGBA32, false);
        // Fondo negro: si sobran mosaicos sin textura, mejor negro que basura.
        var vacio = new Color[lado * columnas * lado * filas];
        atlas.SetPixels(vacio);

        for (int i = 0; i < mosaicos.Count && i < texturas.Count; i++)
        {
            Texture2D origen = Cargar(especie, texturas[i]);
            if (origen == null) continue;

            Texture2D copia = LeerEscalada(origen, lado, esColor);
            atlas.SetPixels((mosaicos[i].x - minX) * lado, (mosaicos[i].y - minY) * lado,
                            lado, lado, copia.GetPixels());
            Object.DestroyImmediate(copia);
        }
        atlas.Apply();

        string ruta = $"{CarpetaRaiz}/{especie}/atlas_{tipo}_{especie}.png";
        File.WriteAllBytes(Path.GetFullPath(ruta), atlas.EncodeToPNG());
        Object.DestroyImmediate(atlas);

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(ruta);
        importer.textureType = esColor ? TextureImporterType.Default : TextureImporterType.NormalMap;
        importer.sRGBTexture = esColor;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
    }

    // Lee una textura sin depender de que este marcada como readable. Los normal
    // map se leen en lineal para no corromperlos.
    private static Texture2D LeerEscalada(Texture2D origen, int lado, bool esColor)
    {
        RenderTexture rt = RenderTexture.GetTemporary(
            lado, lado, 0, RenderTextureFormat.ARGB32,
            esColor ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);

        Graphics.Blit(origen, rt);
        RenderTexture previa = RenderTexture.active;
        RenderTexture.active = rt;

        var copia = new Texture2D(lado, lado, TextureFormat.RGBA32, false);
        copia.ReadPixels(new Rect(0, 0, lado, lado), 0, 0);
        copia.Apply();

        RenderTexture.active = previa;
        RenderTexture.ReleaseTemporary(rt);
        return copia;
    }

    // --- Texturas -----------------------------------------------------------

    // Cuerpo primero (es el mosaico mas poblado), despues ojos y boca. Ese es el
    // mismo orden en que vienen los mosaicos ordenados por cantidad de vertices.
    private static List<string> TexturasOrdenadas(string especie, string prefijo, string id)
    {
        var cuerpo = new List<string>();
        var ojos = new List<string>();
        var boca = new List<string>();

        foreach (string ruta in Directory.GetFiles(Application.dataPath + "/_AuditRank/" + especie, "*.png"))
        {
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            if (!nombre.StartsWith(prefijo)) continue;
            if (nombre == "t_n_default" || nombre.StartsWith("atlas_")) continue;
            // El ID descarta texturas de otro personaje metido en el mismo FBX,
            // como el Pikachu que viene dentro del modelo de Cramorant.
            if (id != null && !nombre.Contains(id)) continue;

            if (nombre.Contains("eye")) ojos.Add(nombre);
            else if (nombre.Contains("mouth")) boca.Add(nombre);
            else cuerpo.Add(nombre);
        }

        cuerpo.Sort(); ojos.Sort(); boca.Sort();
        cuerpo.AddRange(ojos);
        cuerpo.AddRange(boca);
        return cuerpo;
    }

    private static string PrimeraNormal(string especie, string id)
    {
        List<string> normales = TexturasOrdenadas(especie, "t_n_", id);
        return normales.Count > 0 ? normales[0] : null;
    }

    private static Texture2D Cargar(string especie, string nombre)
    {
        return nombre == null
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>($"{CarpetaRaiz}/{especie}/{nombre}.png");
    }

    // --- Varios -------------------------------------------------------------

    private static Mesh MallaDe(Renderer renderer)
    {
        var skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;

        var filtro = renderer.GetComponent<MeshFilter>();
        return filtro != null ? filtro.sharedMesh : null;
    }

    private static string IdDe(string nombre)
    {
        Match m = Regex.Match(nombre, @"(\d{4})");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static Material ObtenerOCrear(string especie, string id)
    {
        string ruta = $"{CarpetaRaiz}/{especie}/mat_{id ?? "x"}{SufijoGenerado}";

        var material = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, ruta);
        }
        return material;
    }

    // Los materiales del FBX son sub-assets de solo lectura, asi que la
    // correccion se aplica sobre las instancias de la escena abierta. Los
    // prefabs los genera UnitePrefabBuilder, que congela esto mismo.
    private static int AsignarEnEscena()
    {
        int corregidos = 0;
        UnityEngine.SceneManagement.Scene escena = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();

        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            int guion = raiz.name.IndexOf('_');
            if (guion < 0) continue;
            string especie = raiz.name.Substring(guion + 1);

            foreach (Renderer renderer in raiz.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materiales = renderer.sharedMaterials;
                bool cambio = false;

                for (int i = 0; i < materiales.Length; i++)
                {
                    Material material = materiales[i];
                    if (material != null && !AssetDatabase.GetAssetPath(material).StartsWith("Packages/")) continue;

                    Material generado = BuscarGenerado(especie);
                    if (generado == null) continue;

                    materiales[i] = generado;
                    cambio = true;
                    corregidos++;
                }

                if (cambio) renderer.sharedMaterials = materiales;
            }
        }

        if (corregidos > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(escena);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(escena);
        }
        return corregidos;
    }

    private static Material BuscarGenerado(string especie)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { $"{CarpetaRaiz}/{especie}" }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.EndsWith(SufijoGenerado)) return AssetDatabase.LoadAssetAtPath<Material>(ruta);
        }
        return null;
    }
}
