using System.Text;
using UnityEditor;
using UnityEngine;

// ANDRUSIANI > Dibujar el mapa de colisiones en la consola
//
// Imprime en ASCII lo que ve la FISICA alrededor del jugador (o del objeto
// seleccionado). Tiene que calcar al mapa dibujado: donde hay pared va X y
// donde se camina va punto.
//
// POR QUE SIRVE
// Es la unica forma de distinguir "la pared no esta puesta" de "la pared esta
// puesta pero no bloquea". Mirando la escena las dos se ven identicas, y con
// paredes invisibles todavia mas. Este barrido usa el mismo OverlapCircle y la
// misma mascara que el PlayerController, asi que lo que se imprime aca es
// literalmente lo que el jugador va a poder atravesar.
//
// Esta guardado como menu porque el snippet suelto de Como_Armar_Un_Mapa_Nuevo.md
// se pega mal, se pierde y hay que reescribirlo en cada mapa nuevo.
namespace PokeRush.EditorTools
{
    public static class MapaDeColisiones
    {
        private const int Radio = 8;          // celdas a cada lado
        private const float RadioTest = 0.3f; // el mismo que usa el PlayerController

        [MenuItem("ANDRUSIANI/Dibujar el mapa de colisiones en la consola")]
        public static void Dibujar()
        {
            var centro = Centro();
            if (centro == null)
            {
                Debug.LogError("[ANDRUSIANI] Selecciona un objeto en la escena (o dejale el " +
                               "nombre 'Player' al jugador) para saber desde donde barrer.");
                return;
            }

            int mascara = LayerMask.GetMask("Obstacles");
            if (mascara == 0)
            {
                Debug.LogError("[ANDRUSIANI] No existe la capa 'Obstacles'.");
                return;
            }

            // Sin esto los colliders recien movidos o recien generados todavia
            // no estan en el broadphase y el barrido sale vacio.
            Physics2D.SyncTransforms();

            Vector2 origen = centro.position;
            var sb = new StringBuilder();
            sb.Append("[ANDRUSIANI] Colisiones alrededor de '").Append(centro.name)
              .Append("' en ").Append(origen).Append(" (X = bloquea, o = el centro)\n");

            int bloqueadas = 0;
            for (int dy = Radio / 2; dy >= -Radio / 2 - 1; dy--)
            {
                for (int dx = -Radio; dx <= Radio; dx++)
                {
                    bool solido = Physics2D.OverlapCircle(
                        origen + new Vector2(dx, dy), RadioTest, mascara) != null;
                    if (solido) bloqueadas++;
                    sb.Append(dx == 0 && dy == 0 ? 'o' : solido ? 'X' : '.');
                }
                sb.Append('\n');
            }

            sb.Append(bloqueadas).Append(" celdas bloqueadas de ")
              .Append((Radio * 2 + 1) * (Radio + 2)).Append('.');

            if (bloqueadas == 0)
                Debug.LogWarning(sb + "\nNinguna celda bloquea. Casi siempre es la capa: el " +
                                 "Tilemap o el bloque quedaron en 'Default' en vez de 'Obstacles'.");
            else
                Debug.Log(sb.ToString());
        }

        private static Transform Centro()
        {
            if (Selection.activeTransform != null) return Selection.activeTransform;
            var player = GameObject.Find("Player");
            return player != null ? player.transform : null;
        }
    }
}
