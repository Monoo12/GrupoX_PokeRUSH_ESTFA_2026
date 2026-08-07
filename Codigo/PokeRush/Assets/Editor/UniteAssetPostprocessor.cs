using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Arregla al importar los modelos y texturas del dump de Pokemon UNITE.
//
// Unity importa estos FBX con cuatro defectos. Uno impide animarlos y los otros
// tres se ven en pantalla:
//
//  0. SIN AVATAR. Los FBX llegan con avatarSetup = NoAvatar, asi que Unity no
//     les agrega Animator y no hay donde reproducir los clips: el modelo queda
//     congelado por mas que el FBX traiga 25 animaciones.
//
//  1. EMISION PARASITA. Los FBX declaran un color emisivo que el shader propio
//     de UNITE usaba para otra cosa. Unity lo toma como emision real y prende
//     _EMISSION; como no hay mapa de emision, tiñe la superficie entera. Un
//     Ninetales de Alola blanco sale naranja; Leafeon y meowscarada salen
//     lavados con emision blanca a 3.0.
//
//  2. MATERIALES SIN LIGAR. En 20 de 78 especies Unity no arma los materiales y
//     deja el "Lit" gris 0.5 por defecto, aunque las texturas esten al lado.
//
//  3. NORMAL MAPS SIN TIPAR. Los t_n_ se importan como textura comun, asi que
//     no se pueden usar como normal map y la iluminacion queda plana.
//
// La nomenclatura del pack es sistematica, y de ahi sale el emparejamiento:
//     mat_bat_0038body_lv3   <-> t_d_bat_0038body_lv3   (difusa)
//                            <-> t_n_bat_0038body_lv3   (normal)
//                            <-> t_mro_bat_0038body_lv3 (metal/rugosidad/oclusion)
public class UniteAssetPostprocessor : AssetPostprocessor
{
    // Solo toca lo que este bajo estas carpetas, para no pisar materiales ni
    // texturas de otros assets del proyecto.
    private static readonly string[] CarpetasUnite =
    {
        "Assets/_Audit",
        "Assets/Models/",
        "Assets/Scenes/try/",   // mapas de UNITE en prueba
    };

    // El pack trae texturas para el shader propio de UNITE (matcap, mascaras de
    // efectos) que no tienen equivalente en URP Lit. Se ignoran.
    private static readonly string[] PrefijosIgnorados = { "t_matcap", "t_fx_", "t_m_" };

    private bool EsDeUnite()
    {
        foreach (string carpeta in CarpetasUnite)
            if (assetPath.StartsWith(carpeta, System.StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // --- Texturas -----------------------------------------------------------

    private void OnPreprocessTexture()
    {
        if (!EsDeUnite()) return;

        string nombre = Path.GetFileNameWithoutExtension(assetPath);
        var importer = (TextureImporter)assetImporter;

        // Todo lo que cae en _UI es para el HUD y para los decals de apuntado:
        // va como Sprite o no se puede usar en un Image ni en un material de UI.
        if (assetPath.Contains("/_UI/"))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            return;
        }

        if (nombre.StartsWith("t_n_"))
        {
            // Sin esto Unity rechaza asignarlas a _BumpMap.
            importer.textureType = TextureImporterType.NormalMap;
        }
        else if (nombre.StartsWith("t_mro"))
        {
            // Datos, no color: van en lineal.
            importer.sRGBTexture = false;
        }

        // El importador reescala a 2048 por defecto cualquier textura mas
        // grande, y la version achicada y vuelta a estirar se ve borrosa. Es el
        // mismo problema que tuvieron las tiras de sprites Gen 5 (el arreglo de
        // SpriteAssigner). Subir el tope deja que cada una se importe al tamaño
        // real. OJO: las texturas de los MAPAS son chicas de nacimiento (128 a
        // 512): si un piso se ve borroso no es por esto sino porque el material
        // perdio su TILING al importar y la textura quedo estirada una sola vez
        // sobre todo el terreno. Ese tiling esta en los mat_*.json del dump.
        importer.maxTextureSize = 8192;

        // Con la camara inclinada de la ruta, el piso a distancia se lava sin
        // filtrado anisotropico (el default es 1).
        importer.anisoLevel = 8;
    }

    // --- Modelos ------------------------------------------------------------

    private void OnPreprocessModel()
    {
        if (!EsDeUnite()) return;

        var importer = (ModelImporter)assetImporter;

        // Generic y no Humanoid: son criaturas, y un rig humanoide les deforma
        // el esqueleto. CreateFromThisModel es lo que hace que Unity genere el
        // Avatar y le ponga el Animator al prefab; con NoAvatar no hay donde
        // reproducir los clips.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
    }

    // Se usa OnPostprocessModel y no OnPostprocessMaterial: para los FBX, Unity
    // importa los materiales por la via de "material description" y el callback
    // de material nunca llega a dispararse.
    private void OnPostprocessModel(GameObject modelo)
    {
        if (!EsDeUnite()) return;

        Dictionary<string, Texture2D> texturas = IndexarTexturasVecinas();

        foreach (Renderer renderer in modelo.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materiales = renderer.sharedMaterials;
            string idMalla = IdDe(MallaDe(renderer));

            for (int i = 0; i < materiales.Length; i++)
            {
                Material material = materiales[i];
                if (material == null) continue;

                // Cuando Unity no logra armar el material deja el "Lit" por
                // defecto de URP, que vive en el paquete y esta COMPARTIDO entre
                // todos los modelos rotos. NO se toca: escribirle texturas se
                // las escribiria a todos a la vez y encima modifica un paquete
                // inmutable. Esas especies (20 de 78) las resuelve
                // UniteMaterialBuilder, que crea materiales propios como asset.
                if (EsCompartido(material)) continue;

                ArreglarMaterial(material, texturas, idMalla);
            }
        }
    }

    // Durante la importacion, los materiales que crea el propio importador
    // todavia no tienen ruta de asset; los externos (el Lit del paquete) si.
    private static bool EsCompartido(Material material)
    {
        return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material));
    }

    private static Mesh MallaDe(Renderer renderer)
    {
        var skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null) return skinned.sharedMesh;

        var filtro = renderer.GetComponent<MeshFilter>();
        return filtro != null ? filtro.sharedMesh : null;
    }

    // "rig_bat_0384dy_lv3" -> "0384". Sirve para elegir la textura correcta
    // cuando el material no tiene nombre del que deducirla.
    private static string IdDe(Mesh malla)
    {
        if (malla == null) return null;

        System.Text.RegularExpressions.Match m =
            System.Text.RegularExpressions.Regex.Match(malla.name, @"(\d{4})");
        return m.Success ? m.Groups[1].Value : null;
    }

    // Todas las texturas viven junto al FBX, una carpeta por especie.
    private Dictionary<string, Texture2D> IndexarTexturasVecinas()
    {
        var indice = new Dictionary<string, Texture2D>();
        string carpeta = Path.GetDirectoryName(assetPath).Replace('\\', '/');

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            if (EsIgnorada(nombre)) continue;

            var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            if (textura != null) indice[nombre] = textura;
        }
        return indice;
    }

    private static bool EsIgnorada(string nombre)
    {
        foreach (string prefijo in PrefijosIgnorados)
            if (nombre.StartsWith(prefijo)) return true;
        return false;
    }

    private void ArreglarMaterial(Material material, Dictionary<string, Texture2D> texturas, string idMalla)
    {
        // 1. Matar la emision parasita.
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        // 2. Ligar las texturas que falten.
        string clave = ClaveDe(material.name);
        string parte = ParteDe(clave ?? material.name);

        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == null)
        {
            Texture2D difusa = Buscar(texturas, "t_d_", clave, parte, idMalla);
            if (difusa != null)
            {
                material.SetTexture("_BaseMap", difusa);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", difusa);
                material.SetColor("_BaseColor", Color.white);
            }
        }

        if (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") == null)
        {
            Texture2D normal = Buscar(texturas, "t_n_", clave, parte, idMalla);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
        }

        // 3. Acabado mate. El MRO del pack guarda metal/rugosidad/oclusion en RGB,
        //    que NO es el layout que espera URP (_MetallicGlossMap usa R + alpha),
        //    asi que asignarlo directo deja los modelos espejados. Estos modelos
        //    son de estilo plano: mejor sin metalicidad y con brillo bajo.
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
    }

    // "mat_bat_0038body_lv3" -> "bat_0038body_lv3".
    // Los materiales que Unity no logro armar se llaman "Lit": no hay clave.
    private static string ClaveDe(string nombreMaterial)
    {
        return nombreMaterial.StartsWith("mat_") ? nombreMaterial.Substring(4) : null;
    }

    private static string ParteDe(string texto)
    {
        if (texto.Contains("eye")) return "eye";
        if (texto.Contains("mouth")) return "mouth";
        return "body";
    }

    // Primero el nombre exacto que dicta la nomenclatura. Si no aparece (pasa
    // con los materiales que Unity no armo, que no tienen clave), se cae al ID
    // de la malla y a la parte del cuerpo.
    private static Texture2D Buscar(Dictionary<string, Texture2D> texturas,
                                    string prefijo, string clave, string parte, string idMalla)
    {
        if (clave != null && texturas.TryGetValue(prefijo + clave, out Texture2D exacta))
            return exacta;

        string mejorNombre = null;
        Texture2D mejor = null;

        foreach (KeyValuePair<string, Texture2D> par in texturas)
        {
            if (!par.Key.StartsWith(prefijo)) continue;
            if (!par.Key.Contains(parte)) continue;
            // El ID descarta texturas de otro personaje metido en el mismo FBX,
            // como el Pikachu que viene dentro del modelo de Cramorant.
            if (idMalla != null && !par.Key.Contains(idMalla)) continue;

            // Con varias variantes gana el nombre mas corto: "...0384body_lv3"
            // es el cuerpo principal y "...0384bodyA_lv3" un detalle. A igual
            // largo (bodya / bodyb) decide el orden alfabetico.
            if (mejorNombre == null
                || par.Key.Length < mejorNombre.Length
                || (par.Key.Length == mejorNombre.Length
                    && string.CompareOrdinal(par.Key, mejorNombre) < 0))
            {
                mejorNombre = par.Key;
                mejor = par.Value;
            }
        }
        return mejor;
    }
}
