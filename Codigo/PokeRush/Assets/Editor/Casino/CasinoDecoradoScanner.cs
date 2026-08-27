using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Herramienta de diagnostico: agrupa los tiles de la capa 'Decorado' en piezas
// (grupos de celdas pegadas) y las reporta.
//
// Existe porque el equipo de nivel dibujo los muebles del casino como TILES, no
// como objetos: no hay una tragaperras que se pueda buscar por nombre. Para
// colgarles la interaccion primero hay que saber donde estan, y esto lo dice sin
// tener que contar a ojo sobre una captura.
public static class CasinoDecoradoScanner
{
    [MenuItem("PokeRush/Casino/Analizar el decorado de la escena abierta")]
    public static void Analizar()
    {
        foreach (string capa in new[] { "Decorado", "Colisiones", "Techos" })
            AnalizarCapa(capa);
    }

    [MenuItem("PokeRush/Casino/Analizar los cuatro pisos")]
    public static void AnalizarTodos()
    {
        if (!UnityEditor.SceneManagement.EditorSceneManager
                .SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string escena in new[]
                 {
                     "Assets/Scenes/Casino Nivel 1.unity",
                     "Assets/Scenes/Casino Nivel 2.unity",
                     "Assets/Scenes/Casino nivel 3.unity",
                     "Assets/Scenes/Casino piso FINAL.unity"
                 })
        {
            if (!System.IO.File.Exists(escena)) continue;

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                escena, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Debug.Log($"[CasinoScan] ======== {System.IO.Path.GetFileNameWithoutExtension(escena)}");
            Analizar();
        }
    }

    private static void AnalizarCapa(string nombreCapa)
    {
        Tilemap decorado = BuscarTilemap(nombreCapa);
        if (decorado == null)
        {
            Debug.Log($"[CasinoScan] No hay capa '{nombreCapa}' en la escena abierta.");
            return;
        }

        List<List<Vector3Int>> piezas = Agrupar(decorado);

        // Se agrupan por tamanio y forma: las piezas iguales son el mismo mueble
        // repetido, y eso es lo que interesa para colgarles un componente.
        var porForma = new Dictionary<string, List<List<Vector3Int>>>();
        foreach (List<Vector3Int> pieza in piezas)
        {
            string forma = FormaDe(pieza);
            if (!porForma.TryGetValue(forma, out var lista))
                porForma[forma] = lista = new List<List<Vector3Int>>();
            lista.Add(pieza);
        }

        Debug.Log($"[CasinoScan] '{decorado.name}': {piezas.Count} piezas, " +
                  $"{porForma.Count} formas distintas.");

        foreach (var par in porForma)
        {
            var ejemplos = new List<string>();
            for (int i = 0; i < par.Value.Count && i < 20; i++)
                ejemplos.Add(V(CentroAbajo(par.Value[i], decorado)));

            Debug.Log($"[CasinoScan] forma {par.Key} x{par.Value.Count} -> " +
                      string.Join("  ", ejemplos));
        }
    }

    // Celdas pegadas en cruz (sin diagonales) = una pieza.
    public static List<List<Vector3Int>> Agrupar(Tilemap tilemap)
    {
        var pendientes = new HashSet<Vector3Int>();
        foreach (Vector3Int p in tilemap.cellBounds.allPositionsWithin)
            if (tilemap.HasTile(p)) pendientes.Add(p);

        var piezas = new List<List<Vector3Int>>();
        var vecinos = new[]
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
        };

        while (pendientes.Count > 0)
        {
            Vector3Int inicio = default;
            foreach (Vector3Int p in pendientes) { inicio = p; break; }

            var pieza = new List<Vector3Int>();
            var cola = new Queue<Vector3Int>();
            cola.Enqueue(inicio);
            pendientes.Remove(inicio);

            while (cola.Count > 0)
            {
                Vector3Int actual = cola.Dequeue();
                pieza.Add(actual);

                foreach (Vector3Int d in vecinos)
                {
                    Vector3Int v = actual + d;
                    if (!pendientes.Contains(v)) continue;
                    pendientes.Remove(v);
                    cola.Enqueue(v);
                }
            }

            piezas.Add(pieza);
        }

        piezas.Sort((a, b) =>
        {
            int porY = Alto(b).CompareTo(Alto(a));
            return porY != 0 ? porY : b.Count.CompareTo(a.Count);
        });
        return piezas;
    }

    // "anchoxalto:celdas", suficiente para distinguir muebles.
    public static string FormaDe(List<Vector3Int> pieza)
        => $"{Ancho(pieza)}x{Alto(pieza)}:{pieza.Count}";

    public static int Ancho(List<Vector3Int> pieza)
    {
        int min = int.MaxValue, max = int.MinValue;
        foreach (Vector3Int p in pieza) { if (p.x < min) min = p.x; if (p.x > max) max = p.x; }
        return max - min + 1;
    }

    public static int Alto(List<Vector3Int> pieza)
    {
        int min = int.MaxValue, max = int.MinValue;
        foreach (Vector3Int p in pieza) { if (p.y < min) min = p.y; if (p.y > max) max = p.y; }
        return max - min + 1;
    }

    // Punto de la pieza donde conviene poner la interaccion: el centro de su
    // fila mas baja, que es la cara que mira el jugador cuando viene de abajo.
    public static Vector3 CentroAbajo(List<Vector3Int> pieza, Tilemap tilemap)
    {
        int minY = int.MaxValue;
        foreach (Vector3Int p in pieza) if (p.y < minY) minY = p.y;

        int minX = int.MaxValue, maxX = int.MinValue;
        foreach (Vector3Int p in pieza)
        {
            if (p.y != minY) continue;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
        }

        Vector3 a = tilemap.GetCellCenterWorld(new Vector3Int(minX, minY, 0));
        Vector3 b = tilemap.GetCellCenterWorld(new Vector3Int(maxX, minY, 0));
        return (a + b) * 0.5f;
    }

    public static Tilemap BuscarTilemap(string nombre)
    {
        foreach (Tilemap t in Object.FindObjectsByType<Tilemap>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == nombre) return t;
        return null;
    }

    private static string V(Vector3 v)
        => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.#}|{1:0.#})", v.x, v.y);
}
