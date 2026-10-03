using UnityEngine;

// Lo que necesitan los Text con la fuente de pixel art ("power clear" y
// familia) para leerse bien.
//
// La fuente declara un renglon del alto de una mayuscula (10 de 16), pero la g,
// la p o la Q bajan bastante mas: con interlineado 1 las colas de un renglon se
// pisan con las mayusculas del de abajo, y con el 1,15 que andaba con Arial se
// tocan. 1,6 deja un espacio parecido al de los juegos de GBA.
//
// Solo cambia los textos de varios renglones: en uno de un solo renglon el
// interlineado no mueve nada.
public static class FuentePixel
{
    public const float Interlineado = 1.6f;

    public static bool Es(Font fuente) => fuente != null && fuente.name.StartsWith("power");
}
