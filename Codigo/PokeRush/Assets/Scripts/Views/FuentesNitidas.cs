using UnityEngine;

// Las fuentes de pixel art ("power clear" y familia) siempre en filtro Point.
//
// Hace lo mismo que PixelFontCrisp (ver ese comentario), pero para todo el
// juego y desde el arranque: antes, la caja de dialogo se veia borrosa hasta
// que se abria el primer menu, porque el que las ponia nitidas era el
// PixelFontCrisp del sistema de menus. Unity regenera la textura de una fuente
// cada vez que aparece un caracter nuevo, por eso se escucha textureRebuilt.
public static class FuentesNitidas
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Iniciar()
    {
        Font.textureRebuilt -= Ajustar;
        Font.textureRebuilt += Ajustar;
    }

    private static void Ajustar(Font fuente)
    {
        if (fuente == null || !fuente.name.StartsWith("power")) return;
        Texture textura = fuente.material != null ? fuente.material.mainTexture : null;
        if (textura == null || textura.filterMode == FilterMode.Point) return;
        textura.filterMode = FilterMode.Point;
        textura.anisoLevel = 0;
    }
}
