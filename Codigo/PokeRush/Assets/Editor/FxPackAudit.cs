using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Reporta que hay REALMENTE adentro de los packs de FX antes de cablearlos.
//
// Existe porque un efecto de impacto dura 0,3 segundos: mirarlo en Play y sacar
// conclusiones es adivinar. Este auditor lee los prefabs como assets -sin
// instanciar nada ni tocar la escena abierta- y deja por escrito los tres datos
// que deciden si un efecto sirve:
//
//  1. QUE SHADER usa cada material. Los packs de la Asset Store suelen venir
//     para el pipeline built-in, y en URP esos materiales salen magenta. El
//     campo `soportado` es la respuesta: si dice NO, ese prefab hay que
//     convertirlo antes de usarlo.
//  2. EL MODO DE ESCALADO de cada ParticleSystem. Con `Local`, agrandar el
//     objeto raiz NO agranda las particulas: el efecto se queda del tamaño con
//     el que lo autoro su creador, sin importar el radio de la habilidad. Es la
//     causa tipica de "el FX no escala".
//  3. CUANTO DURA Y CUANTO OCUPA, calculado de los propios parametros del
//     sistema y no a ojo, para poder elegir el efecto que corresponde al tamaño
//     del golpe.
//
// El reporte va a Temp/, que Unity ya ignora y limpia solo.
public static class FxPackAudit
{
    private const string Reporte = "Temp/fx-pack-audit.txt";

    private static readonly string[] Carpetas =
    {
        "Assets/Hovl Studio",
        "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs",
    };

    [MenuItem("PokeRush/3D/Auditar packs de FX")]
    public static void Auditar()
    {
        var texto = new StringBuilder();
        texto.AppendLine("# Auditoria de los packs de FX");
        texto.AppendLine("# columnas: prefab | sistemas | duracion | radio | escalado | shaders");
        texto.AppendLine();

        foreach (string carpeta in Carpetas)
        {
            if (!AssetDatabase.IsValidFolder(carpeta))
            {
                texto.AppendLine($"FALTA la carpeta {carpeta}");
                continue;
            }

            texto.AppendLine($"== {carpeta} ==");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { carpeta }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
                if (prefab == null) continue;

                texto.AppendLine(Describir(prefab, ruta));
            }
            texto.AppendLine();
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(Reporte, texto.ToString());
        Debug.Log($"[FX] Auditoria escrita en {Reporte}");
    }

    // Se corre sola la primera vez de cada sesion del editor. Es solo lectura de
    // assets, asi que no puede romper nada, y evita tener que acordarse del menu.
    [InitializeOnLoadMethod]
    private static void AlCargar()
    {
        if (File.Exists(Reporte)) return;
        EditorApplication.delayCall += Auditar;
    }

    private static string Describir(GameObject prefab, string ruta)
    {
        var sistemas = prefab.GetComponentsInChildren<ParticleSystem>(true);

        float duracion = 0f;
        float radio = 0f;
        var escalados = new HashSet<string>();

        foreach (ParticleSystem sistema in sistemas)
        {
            ParticleSystem.MainModule principal = sistema.main;
            escalados.Add(principal.scalingMode.ToString());

            float vida = principal.startLifetime.constantMax;
            duracion = Mathf.Max(duracion,
                principal.startDelay.constantMax + principal.duration + vida);

            // Hasta donde llega el efecto: lo que se abre la forma de emision,
            // mas lo que viaja una particula, mas su propio tamaño. Es una cota
            // superior, que es justo lo que hace falta para escalarlo despues.
            float escalaMundo = Maxima(sistema.transform.lossyScale);
            float alcance = sistema.shape.radius
                            + principal.startSpeed.constantMax * vida
                            + principal.startSize.constantMax * 0.5f;
            radio = Mathf.Max(radio, alcance * escalaMundo);
        }

        var shaders = new SortedSet<string>();
        foreach (Renderer render in prefab.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material material in render.sharedMaterials)
            {
                if (material == null) { shaders.Add("<sin material>"); continue; }
                if (material.shader == null) { shaders.Add("<sin shader>"); continue; }

                // isSupported es lo que separa un efecto usable de uno magenta.
                shaders.Add(material.shader.isSupported
                    ? material.shader.name
                    : $"{material.shader.name} !!NO-SOPORTADO!!");
            }
        }

        var cultura = CultureInfo.InvariantCulture;
        return string.Join(" | ",
            Path.GetFileNameWithoutExtension(ruta),
            $"ps={sistemas.Length}",
            $"dur={duracion.ToString("0.00", cultura)}s",
            $"radio={radio.ToString("0.00", cultura)}",
            $"escalado={string.Join(",", escalados)}",
            string.Join(" + ", shaders));
    }

    private static float Maxima(Vector3 v)
        => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
}
