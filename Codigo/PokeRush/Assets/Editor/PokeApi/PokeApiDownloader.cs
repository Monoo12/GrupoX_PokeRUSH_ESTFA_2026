using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

// Descarga y cachea los JSON crudos de PokeAPI en Tools/PokeApiCache/ (fuera de
// Assets/ para que Unity no los importe). El cache va al repo: la generacion de
// assets es reproducible offline y los cambios se ven en los diffs.
namespace PokeRush.EditorTools.PokeApi
{
    public static class PokeApiDownloader
    {
        public const string BaseUrl = "https://pokeapi.co/api/v2";

        // Pausa entre requests reales (fair use de PokeAPI). Los hits de cache no esperan.
        private const int DelayBetweenRequestsMs = 120;

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PokeRush-Importer/1.0 (proyecto academico)");
            return client;
        }

        // Tools/PokeApiCache/ relativo a la raiz del proyecto Unity
        public static string CacheRoot
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "PokeApiCache"));

        public static string SelectionPath => Path.Combine(CacheRoot, "species_selection.json");

        // ---------- acceso al cache ----------

        private static string CachePath(string resource, int id)
            => Path.Combine(CacheRoot, resource, $"{id}.json");

        public static bool IsCached(string resource, int id)
            => File.Exists(CachePath(resource, id));

        // Devuelve el JSON del recurso: del cache si existe, si no lo descarga.
        // Lanza excepcion si la descarga falla (el llamador decide si continuar).
        public static string GetJson(string resource, int id, bool forceDownload = false)
        {
            string path = CachePath(resource, id);
            if (!forceDownload && File.Exists(path))
                return File.ReadAllText(path);

            string url = $"{BaseUrl}/{resource}/{id}/";
            string json = Http.GetStringAsync(url).GetAwaiter().GetResult();

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            Thread.Sleep(DelayBetweenRequestsMs);
            return json;
        }

        public static T GetDto<T>(string resource, int id, bool forceDownload = false)
            => JsonConvert.DeserializeObject<T>(GetJson(resource, id, forceDownload));

        public static SpeciesSelection LoadSelection()
        {
            if (!File.Exists(SelectionPath))
                throw new FileNotFoundException(
                    $"No existe {SelectionPath}. Crear el archivo con la lista de especies a importar.");
            return JsonConvert.DeserializeObject<SpeciesSelection>(File.ReadAllText(SelectionPath));
        }

        // ---------- resolucion de la seleccion ----------

        // Expande la seleccion siguiendo las cadenas evolutivas y devuelve el set
        // final de ids de especie. Descarga species y evolution-chain segun haga falta.
        // progress(mensaje, 0..1) y cancel() vienen de la ventana del editor.
        public static SortedSet<int> ResolveSpeciesIds(
            SpeciesSelection selection, bool forceDownload,
            Action<string, float> progress, Func<bool> cancelled)
        {
            var pending = new Queue<int>(selection.AllIds());
            var resolved = new SortedSet<int>();
            var visitedChains = new HashSet<int>();
            int initialCount = pending.Count;

            while (pending.Count > 0)
            {
                if (cancelled != null && cancelled()) break;
                int id = pending.Dequeue();
                if (!resolved.Add(id)) continue;

                progress?.Invoke($"Especie {id} ({resolved.Count} resueltas)",
                    Mathf.Clamp01(resolved.Count / (float)(initialCount * 3)));

                PokemonSpeciesDto species;
                try
                {
                    species = GetDto<PokemonSpeciesDto>("pokemon-species", id, forceDownload);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PokeApi] No se pudo obtener pokemon-species/{id}: {e.Message}");
                    resolved.Remove(id);
                    continue;
                }

                if (!selection.ExpandEvolutionLines || species.EvolutionChain == null) continue;

                int chainId = species.EvolutionChain.IdFromUrl();
                if (chainId <= 0 || !visitedChains.Add(chainId)) continue;

                try
                {
                    var chain = GetDto<EvolutionChainDto>("evolution-chain", chainId, forceDownload);
                    foreach (int memberId in CollectChainSpeciesIds(chain.Chain))
                        if (!resolved.Contains(memberId))
                            pending.Enqueue(memberId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PokeApi] No se pudo obtener evolution-chain/{chainId}: {e.Message}");
                }
            }

            return resolved;
        }

        private static IEnumerable<int> CollectChainSpeciesIds(ChainLink link)
        {
            if (link == null) yield break;
            int id = link.Species?.IdFromUrl() ?? 0;
            if (id > 0) yield return id;
            if (link.EvolvesTo == null) yield break;
            foreach (ChainLink child in link.EvolvesTo)
                foreach (int childId in CollectChainSpeciesIds(child))
                    yield return childId;
        }

        // ---------- descarga completa ----------

        public class DownloadReport
        {
            public int SpeciesCount;
            public int MoveCount;
            public int AbilityCount;
            public bool Cancelled;
        }

        // Descarga todo lo que la generacion de assets va a necesitar:
        // species + pokemon (variedad default) + moves del learnset + abilities.
        public static DownloadReport DownloadAll(
            bool forceDownload, Action<string, float> progress, Func<bool> cancelled)
        {
            var report = new DownloadReport();
            SpeciesSelection selection = LoadSelection();

            SortedSet<int> speciesIds = ResolveSpeciesIds(selection, forceDownload, progress, cancelled);
            report.SpeciesCount = speciesIds.Count;

            var moveIds = new SortedSet<int>();
            var abilityIds = new SortedSet<int>();

            int index = 0;
            foreach (int speciesId in speciesIds)
            {
                if (cancelled != null && cancelled()) { report.Cancelled = true; return report; }
                index++;
                progress?.Invoke($"Pokemon {speciesId} ({index}/{speciesIds.Count})",
                    0.2f + 0.4f * index / speciesIds.Count);

                try
                {
                    var species = GetDto<PokemonSpeciesDto>("pokemon-species", speciesId);
                    int pokemonId = DefaultPokemonId(species);
                    var pokemon = GetDto<PokemonDto>("pokemon", pokemonId, forceDownload);

                    foreach (var (moveId, _, _) in PokeApiMapper.ExtractLevelUpLearnset(pokemon))
                        if (moveId > 0) moveIds.Add(moveId);

                    int abilityId = FirstAbilityId(pokemon);
                    if (abilityId > 0) abilityIds.Add(abilityId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PokeApi] Fallo descargando datos de la especie {speciesId}: {e.Message}");
                }
            }

            index = 0;
            foreach (int moveId in moveIds)
            {
                if (cancelled != null && cancelled()) { report.Cancelled = true; return report; }
                index++;
                progress?.Invoke($"Movimiento {moveId} ({index}/{moveIds.Count})",
                    0.6f + 0.3f * index / moveIds.Count);
                try
                {
                    GetJson("move", moveId, forceDownload);
                    report.MoveCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PokeApi] Fallo descargando move/{moveId}: {e.Message}");
                }
            }

            index = 0;
            foreach (int abilityId in abilityIds)
            {
                if (cancelled != null && cancelled()) { report.Cancelled = true; return report; }
                index++;
                progress?.Invoke($"Habilidad {abilityId} ({index}/{abilityIds.Count})",
                    0.9f + 0.1f * index / abilityIds.Count);
                try
                {
                    GetJson("ability", abilityId, forceDownload);
                    report.AbilityCount++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[PokeApi] Fallo descargando ability/{abilityId}: {e.Message}");
                }
            }

            WriteManifest(report);
            return report;
        }

        // La variedad default de la especie (ej: deoxys-normal para deoxys).
        public static int DefaultPokemonId(PokemonSpeciesDto species)
        {
            var def = species.Varieties?.Find(v => v.IsDefault);
            int id = def?.Pokemon?.IdFromUrl() ?? 0;
            return id > 0 ? id : species.Id;
        }

        // Primera habilidad no oculta (slot mas bajo); 0 si no tiene.
        public static int FirstAbilityId(PokemonDto pokemon)
        {
            if (pokemon.Abilities == null) return 0;
            PokemonAbilitySlot best = null;
            foreach (var slot in pokemon.Abilities)
            {
                if (slot.IsHidden || slot.Ability == null) continue;
                if (best == null || slot.Slot < best.Slot) best = slot;
            }
            return best?.Ability.IdFromUrl() ?? 0;
        }

        private static void WriteManifest(DownloadReport report)
        {
            var manifest = new
            {
                downloadedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                tool = "PokeRush PokeApiImporter 1.0",
                species = report.SpeciesCount,
                moves = report.MoveCount,
                abilities = report.AbilityCount
            };
            File.WriteAllText(Path.Combine(CacheRoot, "manifest.json"),
                JsonConvert.SerializeObject(manifest, Formatting.Indented));
        }
    }
}
