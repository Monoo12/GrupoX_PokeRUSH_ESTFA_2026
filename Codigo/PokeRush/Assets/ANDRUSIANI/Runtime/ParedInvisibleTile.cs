using UnityEngine;
using UnityEngine.Tilemaps;

// Tile que bloquea al jugador y no se ve en el juego.
//
// PARA QUE SIRVE
// Tapar huecos del mapa sin dibujar nada: bordes de la pantalla, el costado de
// una escalera, la mitad de un mueble que el dibujo deja pasar. Se pinta como
// cualquier otro tile, en la capa de paredes invisibles.
//
// COMO FUNCIONA
// En el editor devuelve un marcador rosa translucido, asi se ve donde se pinto
// y se puede elegir en la paleta. En Play devuelve el mismo sprite con alpha 0,
// asi que desaparece pero el collider se genera igual.
//
// POR QUE ALPHA 0 Y NO sprite = null
// Con sprite = null el Tilemap deja de dibujar la celda, y ahi el
// TilemapCollider2D depende de detalles de version para seguir generando
// geometria. Con el sprite puesto y el color transparente la celda sigue
// existiendo para todo el mundo menos para el ojo: es la version que no se
// rompe. Son cuatro triangulos invisibles por pared, nada que se note.
//
// LO IMPORTANTE
// El colliderType lo declara este tile (Grid), no un override por celda. Por
// eso NO se pierde al pintar, borrar ni recargar el dominio, que es lo que si
// pasa con los tiles de Interior1_Tiles.asset (ver Como_Armar_Un_Mapa_Nuevo.md).
// Lo que si hay que respetar: el GameObject del Tilemap tiene que estar en la
// capa 'Obstacles', porque el PlayerController mira esa capa y no el collider.
// De eso se encarga el menu 'ANDRUSIANI > Preparar capa de paredes invisibles'.
[CreateAssetMenu(fileName = "ParedInvisible", menuName = "ANDRUSIANI/Pared invisible")]
public class ParedInvisibleTile : TileBase
{
    [Tooltip("Lo que se ve en el editor y en la paleta. Se genera solo con " +
             "'ANDRUSIANI > Generar assets'.")]
    public Sprite marcador;

    [Tooltip("Tinte del marcador en el editor. No afecta al juego.")]
    public Color colorEnEditor = new Color(1f, 0.15f, 0.45f, 0.85f);

    [Tooltip("Dejarlo en true. Solo para depurar: en false la pared se ve " +
             "tambien en Play.")]
    public bool invisibleEnJuego = true;

    [Tooltip("Grid usa la celda entera, que es lo que se quiere para " +
             "movimiento por grilla. Sprite no sirve: el marcador no tiene " +
             "physics shape y la geometria sale vacia.")]
    public Tile.ColliderType tipoDeCollider = Tile.ColliderType.Grid;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        bool oculto = invisibleEnJuego && Application.isPlaying;

        tileData.sprite = marcador;
        tileData.color = oculto ? Color.clear : colorEnEditor;
        tileData.transform = Matrix4x4.identity;
        tileData.gameObject = null;
        tileData.colliderType = tipoDeCollider;

        // LockColor es obligatorio: sin el, el color que quedo guardado en la
        // celda del Tilemap le gana al de aca y la pared vuelve a verse en Play.
        tileData.flags = TileFlags.LockColor | TileFlags.LockTransform;
    }
}
