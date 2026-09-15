using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Arreglos sobre los assets YA importados, sin volver a descargar nada de
// PokeAPI (lo que haga falta se lee de Tools/PokeApiCache). Hacen lo mismo que
// el importador hace ahora al generar, para no tener que reimportar todo.
namespace PokeRush.EditorTools.PokeApi
{
    public static class MovimientosDeCombateFixer
    {
        private const string MovesFolder = "Assets/Resources/Definitions/Moves";
        private const string PokensFolder = "Assets/Resources/Definitions/Pokens";

        // Hiperrayo descansa, Rayo Solar carga, Vuelo carga fuera de alcance...
        [MenuItem("PokeRush/Combate/Aplicar carga y recarga a los movimientos")]
        public static void AplicarCargaYRecarga()
        {
            var cambiados = new List<string>();
            foreach (MoveDefinition move in Cargar<MoveDefinition>(MovesFolder))
            {
                if (!PokeApiAssetGenerator.AplicarReglaDeTurnos(move)) continue;
                EditorUtility.SetDirty(move);
                cambiados.Add($"{move.moveName} ({move.turnRule})");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Combate] Carga y recarga: {cambiados.Count} movimientos actualizados. " +
                      string.Join(", ", cambiados));
        }

        // Especies que no aprenden ningun ataque (Abra, Cosmog...): se les suma
        // uno o dos al nivel 1. Corre antes lo de carga y recarga para no
        // elegirles un movimiento de dos turnos como ataque de respaldo.
        [MenuItem("PokeRush/Combate/Dar ataques a las especies que no tienen")]
        public static void DarAtaques()
        {
            AplicarCargaYRecarga();

            List<MoveDefinition> moves = Cargar<MoveDefinition>(MovesFolder);
            Dictionary<int, MoveDefinition> porId = moves.Where(m => m.pokeApiId > 0)
                .GroupBy(m => m.pokeApiId).ToDictionary(g => g.Key, g => g.First());
            Dictionary<string, MoveDefinition> porNombre = moves.Where(m => !string.IsNullOrEmpty(m.englishName))
                .GroupBy(m => m.englishName).ToDictionary(g => g.Key, g => g.First());

            var arreglados = new List<string>();
            foreach (PokenDefinition especie in Cargar<PokenDefinition>(PokensFolder))
            {
                List<string> agregados = PokeApiAssetGenerator.CompletarAtaques(especie, PokemonDelCache(especie),
                    id => porId.TryGetValue(id, out MoveDefinition m) ? m : null,
                    nombre => porNombre.TryGetValue(nombre, out MoveDefinition m) ? m : null);
                if (agregados.Count == 0) continue;

                EditorUtility.SetDirty(especie);
                arreglados.Add($"{especie.pokenName}: {string.Join(" y ", agregados)}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Combate] Especies sin ataques arregladas: {arreglados.Count}. " +
                      string.Join(" | ", arreglados));
        }

        // Solo del cache: si no esta, null (se usa el ataque basico del tipo).
        private static PokemonDto PokemonDelCache(PokenDefinition especie)
        {
            if (especie == null || especie.dexNumber <= 0) return null;
            if (!PokeApiDownloader.IsCached("pokemon-species", especie.dexNumber)) return null;

            try
            {
                var species = PokeApiDownloader.GetDto<PokemonSpeciesDto>("pokemon-species", especie.dexNumber);
                int pokemonId = PokeApiDownloader.DefaultPokemonId(species);
                if (!PokeApiDownloader.IsCached("pokemon", pokemonId)) return null;
                return PokeApiDownloader.GetDto<PokemonDto>("pokemon", pokemonId);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Combate] {especie.pokenName}: no se pudo leer el cache ({e.Message}).");
                return null;
            }
        }

        private static List<T> Cargar<T>(string carpeta) where T : Object
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { carpeta })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null).ToList();
    }
}
