using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Genera/actualiza los ScriptableObjects del juego a partir del cache de PokeAPI.
// Idempotente: busca el asset existente por pokeApiId (moves) o dexNumber (pokens)
// y lo actualiza en lugar de duplicarlo. Nunca toca sprites ni animators: esas
// referencias las asigna el equipo de arte a mano y se preservan.
namespace PokeRush.EditorTools.PokeApi
{
    public static class PokeApiAssetGenerator
    {
        private const string PokensFolder = "Assets/Resources/Definitions/Pokens";
        private const string MovesFolder = "Assets/Resources/Definitions/Moves";

        public class GenerateReport
        {
            public int MovesCreated, MovesUpdated;
            public int PokensCreated, PokensUpdated;
            public int MovesNotImplemented;
            public List<string> Warnings = new List<string>();
            public bool Cancelled;
        }

        public static GenerateReport GenerateAll(Action<string, float> progress, Func<bool> cancelled)
        {
            var report = new GenerateReport();
            SpeciesSelection selection = PokeApiDownloader.LoadSelection();

            progress?.Invoke("Resolviendo especies desde el cache...", 0f);
            SortedSet<int> speciesIds = PokeApiDownloader.ResolveSpeciesIds(
                selection, forceDownload: false, progress, cancelled);
            if (cancelled != null && cancelled()) { report.Cancelled = true; return report; }

            // Junta todos los movimientos que aparecen en algun learnset
            var moveIds = new SortedSet<int>();
            var pokemonBySpecies = new Dictionary<int, PokemonDto>();
            var speciesDtos = new Dictionary<int, PokemonSpeciesDto>();

            foreach (int speciesId in speciesIds)
            {
                if (cancelled != null && cancelled()) { report.Cancelled = true; return report; }
                try
                {
                    var species = PokeApiDownloader.GetDto<PokemonSpeciesDto>("pokemon-species", speciesId);
                    var pokemon = PokeApiDownloader.GetDto<PokemonDto>(
                        "pokemon", PokeApiDownloader.DefaultPokemonId(species));
                    speciesDtos[speciesId] = species;
                    pokemonBySpecies[speciesId] = pokemon;
                    foreach (var (moveId, _, _) in PokeApiMapper.ExtractLevelUpLearnset(pokemon))
                        if (moveId > 0) moveIds.Add(moveId);
                }
                catch (Exception e)
                {
                    report.Warnings.Add($"Especie {speciesId}: sin datos en cache ({e.Message})");
                }
            }

            EnsureFolder(MovesFolder);
            EnsureFolder(PokensFolder);

            // Indices de assets existentes (antes de crear nada)
            Dictionary<int, MoveDefinition> movesByApiId = LoadExisting<MoveDefinition>(MovesFolder)
                .Where(m => m.pokeApiId > 0)
                .GroupBy(m => m.pokeApiId).ToDictionary(g => g.Key, g => g.First());
            Dictionary<int, PokenDefinition> pokensByDex = LoadExisting<PokenDefinition>(PokensFolder)
                .Where(p => p.dexNumber > 0)
                .GroupBy(p => p.dexNumber).ToDictionary(g => g.Key, g => g.First());

            // ---- movimientos ----
            int index = 0;
            foreach (int moveId in moveIds)
            {
                if (cancelled != null && cancelled()) { report.Cancelled = true; break; }
                index++;
                progress?.Invoke($"Movimientos ({index}/{moveIds.Count})", 0.3f + 0.35f * index / moveIds.Count);

                try
                {
                    var dto = PokeApiDownloader.GetDto<MoveDto>("move", moveId);
                    bool created = GetOrCreateMoveAsset(dto, movesByApiId, out MoveDefinition asset);
                    PopulateMove(asset, dto);
                    EditorUtility.SetDirty(asset);
                    if (created) report.MovesCreated++; else report.MovesUpdated++;
                    if (asset.implementation == MoveImplementation.NotImplemented)
                        report.MovesNotImplemented++;
                }
                catch (Exception e)
                {
                    report.Warnings.Add($"Move {moveId}: {e.Message}");
                }
            }

            // ---- especies ----
            index = 0;
            foreach (int speciesId in speciesIds)
            {
                if (cancelled != null && cancelled()) { report.Cancelled = true; break; }
                if (!speciesDtos.ContainsKey(speciesId)) continue;
                index++;
                progress?.Invoke($"Pokens ({index}/{speciesDtos.Count})", 0.65f + 0.35f * index / speciesDtos.Count);

                try
                {
                    var species = speciesDtos[speciesId];
                    var pokemon = pokemonBySpecies[speciesId];
                    bool created = GetOrCreatePokenAsset(species, pokensByDex, out PokenDefinition asset);
                    PopulatePoken(asset, species, pokemon, movesByApiId, report);
                    EditorUtility.SetDirty(asset);
                    if (created) report.PokensCreated++; else report.PokensUpdated++;
                }
                catch (Exception e)
                {
                    report.Warnings.Add($"Especie {speciesId}: {e.Message}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        // ---------- movimientos ----------

        private static bool GetOrCreateMoveAsset(
            MoveDto dto, Dictionary<int, MoveDefinition> existing, out MoveDefinition asset)
        {
            if (existing.TryGetValue(dto.Id, out asset)) return false;

            string spanishName = PokeApiMapper.SpanishName(dto.Names, dto.Name);
            string path = $"{MovesFolder}/{dto.Id:0000}_{SanitizeFileName(spanishName)}.asset";
            asset = ScriptableObject.CreateInstance<MoveDefinition>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            existing[dto.Id] = asset;
            return true;
        }

        private static void PopulateMove(MoveDefinition asset, MoveDto dto)
        {
            asset.moveName = PokeApiMapper.SpanishName(dto.Names, dto.Name);
            asset.pokeApiId = dto.Id;
            asset.englishName = dto.Name;
            asset.descripcion = MtYDescripciones.Descripcion(dto);
            asset.type = PokeApiMapper.MapType(dto.Type?.Name);
            asset.attackType = PokeApiMapper.MapDamageClass(dto.DamageClass?.Name);
            asset.power = dto.Power ?? 0;
            asset.accuracy = dto.Accuracy ?? 0;
            asset.alwaysHits = dto.Accuracy == null;
            asset.maxPP = dto.Pp ?? 5;
            asset.priority = dto.Priority;
            asset.critRateStage = dto.Meta?.CritRate ?? 0;

            // Estado alterado que produce (si esta soportado)
            asset.effect = StatusEffect.None;
            asset.effectChance = 0;
            if (dto.Meta != null && PokeApiMapper.TryMapAilment(dto.Meta.Ailment?.Name, out StatusEffect status))
            {
                asset.effect = status;
                // ailment_chance 0 en PokeAPI significa "siempre" (movs de estado puros)
                asset.effectChance = dto.Meta.AilmentChance == 0 ? 100 : dto.Meta.AilmentChance;
            }

            // Cambios de stats (solo los mapeables)
            var changes = new List<StatChangeEntry>();
            if (dto.StatChanges != null)
            {
                bool targetSelf = PokeApiMapper.StatChangesTargetSelf(dto);
                foreach (var sc in dto.StatChanges)
                {
                    if (!PokeApiMapper.TryMapStat(sc.Stat?.Name, out Stat stat)) continue;
                    changes.Add(new StatChangeEntry { stat = stat, stages = sc.Change, targetSelf = targetSelf });
                }
            }
            asset.statChanges = changes.ToArray();
            asset.statChangeChance = dto.Meta?.StatChance ?? 0;

            asset.flinchChance = dto.Meta?.FlinchChance ?? 0;
            asset.minHits = dto.Meta?.MinHits ?? 0;
            asset.maxHits = dto.Meta?.MaxHits ?? 0;

            // PokeAPI: drain positivo = drenaje, negativo = retroceso
            int drain = dto.Meta?.Drain ?? 0;
            asset.drainPercent = drain > 0 ? drain : 0;
            asset.recoilPercent = drain < 0 ? -drain : 0;
            asset.healPercent = dto.Meta?.Healing ?? 0;
            asset.makesContact = PokeApiMapper.MakesContact(dto);

            // Clima y efectos de campo: estos movimientos SI estan implementados
            // aunque su categoria (whole-field-effect...) los excluiria
            asset.weatherEffect = PokeApiMapper.WeatherFor(dto.Name);
            asset.fieldEffect = PokeApiMapper.FieldEffectFor(dto.Name);

            asset.implementation = PokeApiMapper.ClassifyMove(dto);
            if (asset.weatherEffect != WeatherKind.None || asset.fieldEffect != FieldEffectKind.None)
                asset.implementation = MoveImplementation.Full;
            asset.effectDescription = BuildEffectDescription(dto, asset.implementation);
            AplicarReglaDeTurnos(asset);
        }

        // Carga, fuera de alcance y recarga (ver PokeApiMapper.TryGetTurnRule).
        // Publico: lo usa tambien el menu que lo aplica a los assets ya
        // importados sin volver a descargar nada. Devuelve true si cambio algo.
        public static bool AplicarReglaDeTurnos(MoveDefinition asset)
        {
            if (asset == null) return false;

            if (!PokeApiMapper.TryGetTurnRule(asset.englishName, out var regla))
            {
                if (asset.turnRule == MoveTurnRule.Normal) return false;
                asset.turnRule = MoveTurnRule.Normal;
                asset.chargeMessage = null;
                asset.chargeStatChanges = new StatChangeEntry[0];
                asset.skipChargeInWeather = WeatherKind.None;
                return true;
            }

            string antes = JsonUtility.ToJson(asset);

            asset.turnRule = regla.Regla;
            asset.chargeMessage = regla.MensajeDeCarga;
            asset.skipChargeInWeather = regla.SinCargaCon;

            // Cabezazo sube la Defensa al AGACHAR la cabeza, no al pegar: los
            // cambios de stats del movimiento pasan al turno de carga.
            if (regla.SubeStatsAlCargar && asset.statChanges != null && asset.statChanges.Length > 0)
            {
                asset.chargeStatChanges = asset.statChanges;
                asset.statChanges = new StatChangeEntry[0];
                asset.statChangeChance = 0;
            }
            else if (!regla.SubeStatsAlCargar)
            {
                asset.chargeStatChanges = new StatChangeEntry[0];
            }

            // Antes eran DamageOnly ("solo hace daño"): ahora el efecto es completo.
            if (asset.power > 0 && asset.implementation == MoveImplementation.DamageOnly)
                asset.implementation = MoveImplementation.Full;
            if (!string.IsNullOrEmpty(asset.effectDescription))
                asset.effectDescription = asset.effectDescription
                    .Replace(" [Efecto secundario no implementado: solo hace daño]", "")
                    .Replace("[Efecto secundario no implementado: solo hace daño]", "").Trim();

            return JsonUtility.ToJson(asset) != antes;
        }

        // Especies que no aprenden por nivel NINGUN movimiento que pegue (Abra
        // solo sabe Teletransporte; Cosmog, Salpicadura). Se les suman al nivel 1
        // hasta dos ataques de MT/tutor/huevo (primero los de su tipo, y los mas
        // flojos primero para no regalar poder), y si no hay ninguno, el basico
        // de su tipo y Placaje. Devuelve los nombres agregados (vacio = no hacia falta).
        public static List<string> CompletarAtaques(PokenDefinition asset, PokemonDto pokemon,
            Func<int, MoveDefinition> porId, Func<string, MoveDefinition> porNombreIngles)
        {
            var agregados = new List<string>();
            if (asset == null) return agregados;

            bool yaTiene = (asset.learnset != null && asset.learnset.Any(e => e != null && PokenModel.SirveParaPegar(e.move)))
                           || (asset.moves != null && asset.moves.Any(PokenModel.SirveParaPegar));
            if (yaTiene) return agregados;

            var candidatos = new List<MoveDefinition>();
            if (pokemon != null)
            {
                foreach (int id in PokeApiMapper.ExtractOtherMethodMoveIds(pokemon))
                {
                    MoveDefinition m = porId(id);
                    if (PokenModel.SirveParaPegar(m) && !candidatos.Contains(m)) candidatos.Add(m);
                }
            }

            bool EsDeSuTipo(MoveDefinition m)
                => m.type == asset.type || (asset.type2 != PokenType.None && m.type == asset.type2);

            // Sin dos turnos ni retroceso: el ataque de respaldo tiene que ser simple.
            var elegidos = candidatos
                .Where(m => m.turnRule == MoveTurnRule.Normal && m.recoilPercent == 0)
                .OrderByDescending(EsDeSuTipo).ThenBy(m => m.power).ThenByDescending(m => m.accuracy)
                .Take(2).ToList();

            if (elegidos.Count == 0)
            {
                if (PokeApiMapper.AtaqueBasicoPorTipo.TryGetValue(asset.type, out string basico))
                {
                    MoveDefinition m = porNombreIngles(basico);
                    if (PokenModel.SirveParaPegar(m)) elegidos.Add(m);
                }
                MoveDefinition placaje = porNombreIngles("tackle");
                if (PokenModel.SirveParaPegar(placaje) && !elegidos.Contains(placaje)) elegidos.Add(placaje);
            }

            if (elegidos.Count == 0) return agregados;

            var learnset = new List<LearnsetEntry>(asset.learnset ?? new LearnsetEntry[0]);
            foreach (MoveDefinition m in elegidos)
            {
                learnset.Insert(0, new LearnsetEntry { move = m, level = 1 });
                agregados.Add(m.moveName);
            }
            asset.learnset = learnset.OrderBy(e => e.level).ToArray(); // OrderBy es estable
            return agregados;
        }

        private static string BuildEffectDescription(MoveDto dto, MoveImplementation implementation)
        {
            string desc = dto.EffectEntries?
                .FirstOrDefault(e => e.Language?.Name == "en")?.ShortEffect ?? "";
            desc = desc.Replace("$effect_chance", (dto.EffectChance ?? 0).ToString());

            switch (implementation)
            {
                case MoveImplementation.DamageOnly:
                    return $"{desc} [Efecto secundario no implementado: solo hace daño]".Trim();
                case MoveImplementation.NotImplemented:
                    return $"{desc} [No implementado: excluido de los learnsets]".Trim();
                default:
                    return desc;
            }
        }

        // ---------- especies ----------

        private static bool GetOrCreatePokenAsset(
            PokemonSpeciesDto species, Dictionary<int, PokenDefinition> existing, out PokenDefinition asset)
        {
            if (existing.TryGetValue(species.Id, out asset)) return false;

            string spanishName = PokeApiMapper.SpanishName(species.Names, species.Name);
            string path = $"{PokensFolder}/{species.Id:0000}_{SanitizeFileName(spanishName)}.asset";
            asset = ScriptableObject.CreateInstance<PokenDefinition>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            existing[species.Id] = asset;
            return true;
        }

        private static void PopulatePoken(
            PokenDefinition asset, PokemonSpeciesDto species, PokemonDto pokemon,
            Dictionary<int, MoveDefinition> movesByApiId, GenerateReport report)
        {
            asset.pokenName = PokeApiMapper.SpanishName(species.Names, species.Name);
            asset.dexNumber = species.Id;

            // Tipos por slot (slot 1 = principal, slot 2 = secundario)
            asset.type = PokenType.None;
            asset.type2 = PokenType.None;
            if (pokemon.Types != null)
            {
                foreach (var slot in pokemon.Types.OrderBy(t => t.Slot))
                {
                    PokenType mapped = PokeApiMapper.MapType(slot.Type?.Name);
                    if (asset.type == PokenType.None) asset.type = mapped;
                    else asset.type2 = mapped;
                }
            }

            // Stats base por nombre
            if (pokemon.Stats != null)
            {
                foreach (var stat in pokemon.Stats)
                {
                    switch (stat.Stat?.Name)
                    {
                        case "hp": asset.baseHP = stat.BaseStat; break;
                        case "attack": asset.baseAttack = stat.BaseStat; break;
                        case "defense": asset.baseDefense = stat.BaseStat; break;
                        case "special-attack": asset.baseSpecialAttack = stat.BaseStat; break;
                        case "special-defense": asset.baseSpecialDefense = stat.BaseStat; break;
                        case "speed": asset.baseSpeed = stat.BaseStat; break;
                    }
                }
            }

            asset.baseXpYield = pokemon.BaseExperience ?? 60;

            // Learnset: solo movimientos usables (no NotImplemented), un nivel por
            // movimiento (el mas bajo, por si la especie lo re-aprende mas arriba)
            var learnset = new List<LearnsetEntry>();
            var seenMoves = new HashSet<int>();
            foreach (var (moveId, moveName, level) in PokeApiMapper.ExtractLevelUpLearnset(pokemon))
            {
                if (!seenMoves.Add(moveId)) continue;
                if (!movesByApiId.TryGetValue(moveId, out MoveDefinition moveAsset))
                {
                    report.Warnings.Add($"{asset.pokenName}: movimiento '{moveName}' ({moveId}) sin asset, se omite");
                    continue;
                }
                if (moveAsset.implementation == MoveImplementation.NotImplemented) continue;
                learnset.Add(new LearnsetEntry { move = moveAsset, level = level });
            }
            learnset.Sort((a, b) => a.level.CompareTo(b.level));
            asset.learnset = learnset.ToArray();

            // Lo que le puede enseñar Nestor en la Quarcho Cueva (MT y tutor).
            asset.mtMoves = MtYDescripciones.MovimientosDeMt(pokemon,
                id => movesByApiId.TryGetValue(id, out MoveDefinition m) ? m : null);

            // Todo Pokemon tiene que poder pegar: si por nivel no aprende ningun
            // ataque, se le suman de MT/tutor/huevo (o uno basico de su tipo).
            List<string> agregados = CompletarAtaques(asset, pokemon,
                id => movesByApiId.TryGetValue(id, out MoveDefinition m) ? m : null,
                nombre => movesByApiId.Values.FirstOrDefault(m => m != null && m.englishName == nombre));
            if (agregados.Count > 0)
                report.Warnings.Add($"{asset.pokenName}: no aprendia ningun ataque por nivel; se le sumo {string.Join(" y ", agregados)} al nivel 1");
            else if (asset.learnset.Length == 0)
                report.Warnings.Add($"{asset.pokenName}: learnset vacio (todos sus movimientos quedaron excluidos)");

            // Habilidad: nombre/descripcion en español + enum si esta implementada
            int abilityApiId = PokeApiDownloader.FirstAbilityId(pokemon);
            if (abilityApiId > 0)
            {
                try
                {
                    var ability = PokeApiDownloader.GetDto<AbilityDto>("ability", abilityApiId);
                    asset.ability = PokeApiMapper.SpanishName(ability.Names, ability.Name);
                    asset.abilityDescription = PokeApiMapper.SpanishFlavorText(ability.FlavorTextEntries);
                    asset.abilityId = PokeApiMapper.MapAbility(ability.Name);
                }
                catch (Exception e)
                {
                    report.Warnings.Add($"{asset.pokenName}: sin datos de habilidad ({e.Message})");
                }
            }
        }

        // ---------- utilidades ----------

        private static List<T> LoadExisting<T>(string folder) where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(a => a != null)
                .ToList();
        }

        private static void EnsureFolder(string assetPath)
        {
            // Crea la jerarquia de carpetas dentro de Assets/ si no existe
            string[] parts = assetPath.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
