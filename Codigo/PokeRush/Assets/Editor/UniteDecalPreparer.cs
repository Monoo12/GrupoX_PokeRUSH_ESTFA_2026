using System.IO;
using UnityEditor;
using UnityEngine;

// Prepara los decals de apuntado del pack para poder usarlos sobre un quad.
//
// El problema: varios de esos PNG tienen el canal alfa en 1 de punta a punta y
// definen la forma con el COLOR, sobre un fondo azul oscuro. UNITE los dibuja
// con su propio shader aditivo, donde ese fondo casi no suma. Puestos sobre un
// quad con blending comun se ve el rectangulo entero, no la forma.
//
// La solucion es generar una copia con alfa = luminancia: lo oscuro pasa a
// transparente y la figura queda recortada. Es lo mismo que hace el shader del
// juego, pero horneado en la textura, asi funciona con el URP de fabrica.
//
// El original NO se toca: se escribe un archivo nuevo con sufijo _alfa.
public static class UniteDecalPreparer
{
    private const string Carpeta = "Assets/_AuditRank/_UI";
    private const string Sufijo = "_alfa";

    [MenuItem("PokeRush/3D/Preparar decals de apuntado")]
    public static void Preparar()
    {
        int generados = 0;

        foreach (string ruta in Directory.GetFiles(Application.dataPath + "/_AuditRank/_UI", "*.png"))
        {
            string nombre = Path.GetFileNameWithoutExtension(ruta);
            if (!nombre.StartsWith("t_fx_")) continue;      // solo los decals de FX
            if (nombre.EndsWith(Sufijo)) continue;          // ya generado

            if (Generar(nombre)) generados++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"[UNITE] Decals con alfa generados: {generados}. Quedaron en {Carpeta} con sufijo {Sufijo}.");
    }

    private static bool Generar(string nombre)
    {
        string origen = $"{Carpeta}/{nombre}.png";
        var textura = LeerSinTocarImportador(origen);
        if (textura == null) return false;

        Color[] pixeles = textura.GetPixels();
        for (int i = 0; i < pixeles.Length; i++)
        {
            Color p = pixeles[i];
            // El canal mas brillante manda: en estos decals la figura siempre es
            // mas clara que el fondo, y usar el maximo conserva los bordes finos
            // mejor que un promedio.
            float luz = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
            pixeles[i] = new Color(p.r, p.g, p.b, luz * p.a);
        }

        var salida = new Texture2D(textura.width, textura.height, TextureFormat.RGBA32, false);
        salida.SetPixels(pixeles);
        salida.Apply();

        string destino = $"{Carpeta}/{nombre}{Sufijo}.png";
        File.WriteAllBytes(Path.GetFullPath(destino), salida.EncodeToPNG());
        Object.DestroyImmediate(salida);
        Object.DestroyImmediate(textura);

        AssetDatabase.ImportAsset(destino, ImportAssetOptions.ForceUpdate);
        return true;
    }

    // Se lee por RenderTexture en vez de marcar la textura como readable: asi no
    // queda modificado el importador del asset original.
    private static Texture2D LeerSinTocarImportador(string ruta)
    {
        var origen = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
        if (origen == null) return null;

        RenderTexture rt = RenderTexture.GetTemporary(
            origen.width, origen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

        Graphics.Blit(origen, rt);
        RenderTexture previa = RenderTexture.active;
        RenderTexture.active = rt;

        var copia = new Texture2D(origen.width, origen.height, TextureFormat.RGBA32, false);
        copia.ReadPixels(new Rect(0, 0, origen.width, origen.height), 0, 0);
        copia.Apply();

        RenderTexture.active = previa;
        RenderTexture.ReleaseTemporary(rt);
        return copia;
    }
}
