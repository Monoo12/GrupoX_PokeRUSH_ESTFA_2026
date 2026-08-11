using UnityEngine;

// Contenedor de los Tiles de un tileset: van todos como sub-assets de un solo
// archivo en vez de miles de .asset sueltos. Un tileset son ~4.000 tiles, y
// como archivos individuales serian eso por dos (cada uno con su .meta), lo que
// hace lenta la base de assets sin ninguna ventaja.
//
// Tiene que estar en un archivo con su mismo nombre: Unity no resuelve el
// script de un ScriptableObject anidado en otro archivo y el asset queda
// huerfano ("No script asset for TileContainer").
namespace PokeRush.EditorTools
{
    public class TileContainer : ScriptableObject { }
}
