using System.Collections.Generic;
using Newtonsoft.Json;

// DTOs de deserializacion de PokeAPI (https://pokeapi.co/docs/v2).
// Solo los campos que usa el importador; el resto del JSON se ignora.
namespace PokeRush.EditorTools.PokeApi
{
    // ---------- comun ----------

    public class NamedApiResource
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("url")] public string Url;

        // Los recursos vienen como ".../api/v2/move/85/": el id es el ultimo segmento.
        public int IdFromUrl()
        {
            if (string.IsNullOrEmpty(Url)) return 0;
            string[] parts = Url.TrimEnd('/').Split('/');
            return int.TryParse(parts[parts.Length - 1], out int id) ? id : 0;
        }
    }

    public class LocalizedName
    {
        [JsonProperty("language")] public NamedApiResource Language;
        [JsonProperty("name")] public string Name;
    }

    // ---------- pokemon/{id} ----------

    public class PokemonDto
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("base_experience")] public int? BaseExperience;
        [JsonProperty("types")] public List<PokemonTypeSlot> Types;
        [JsonProperty("stats")] public List<PokemonStat> Stats;
        [JsonProperty("abilities")] public List<PokemonAbilitySlot> Abilities;
        [JsonProperty("moves")] public List<PokemonMoveEntry> Moves;
    }

    public class PokemonTypeSlot
    {
        [JsonProperty("slot")] public int Slot;
        [JsonProperty("type")] public NamedApiResource Type;
    }

    public class PokemonStat
    {
        [JsonProperty("base_stat")] public int BaseStat;
        [JsonProperty("stat")] public NamedApiResource Stat;
    }

    public class PokemonAbilitySlot
    {
        [JsonProperty("is_hidden")] public bool IsHidden;
        [JsonProperty("slot")] public int Slot;
        [JsonProperty("ability")] public NamedApiResource Ability;
    }

    public class PokemonMoveEntry
    {
        [JsonProperty("move")] public NamedApiResource Move;
        [JsonProperty("version_group_details")] public List<VersionGroupDetail> VersionGroupDetails;
    }

    public class VersionGroupDetail
    {
        [JsonProperty("level_learned_at")] public int LevelLearnedAt;
        [JsonProperty("move_learn_method")] public NamedApiResource MoveLearnMethod;
        [JsonProperty("version_group")] public NamedApiResource VersionGroup;
    }

    // ---------- pokemon-species/{id} ----------

    public class PokemonSpeciesDto
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("names")] public List<LocalizedName> Names;
        [JsonProperty("evolution_chain")] public UrlOnlyResource EvolutionChain;
        [JsonProperty("varieties")] public List<PokemonSpeciesVariety> Varieties;
    }

    public class UrlOnlyResource
    {
        [JsonProperty("url")] public string Url;

        public int IdFromUrl()
        {
            if (string.IsNullOrEmpty(Url)) return 0;
            string[] parts = Url.TrimEnd('/').Split('/');
            return int.TryParse(parts[parts.Length - 1], out int id) ? id : 0;
        }
    }

    public class PokemonSpeciesVariety
    {
        [JsonProperty("is_default")] public bool IsDefault;
        [JsonProperty("pokemon")] public NamedApiResource Pokemon;
    }

    // ---------- evolution-chain/{id} ----------

    public class EvolutionChainDto
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("chain")] public ChainLink Chain;
    }

    public class ChainLink
    {
        [JsonProperty("species")] public NamedApiResource Species;
        [JsonProperty("evolves_to")] public List<ChainLink> EvolvesTo;
    }

    // ---------- move/{id} ----------

    public class MoveDto
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("names")] public List<LocalizedName> Names;
        [JsonProperty("accuracy")] public int? Accuracy;
        [JsonProperty("power")] public int? Power;
        [JsonProperty("pp")] public int? Pp;
        [JsonProperty("priority")] public int Priority;
        [JsonProperty("damage_class")] public NamedApiResource DamageClass;
        [JsonProperty("type")] public NamedApiResource Type;
        [JsonProperty("target")] public NamedApiResource Target;
        [JsonProperty("effect_chance")] public int? EffectChance;
        [JsonProperty("meta")] public MoveMetaDto Meta;
        [JsonProperty("stat_changes")] public List<MoveStatChange> StatChanges;
        [JsonProperty("effect_entries")] public List<MoveEffectEntry> EffectEntries;
    }

    public class MoveMetaDto
    {
        [JsonProperty("ailment")] public NamedApiResource Ailment;
        [JsonProperty("ailment_chance")] public int AilmentChance;
        [JsonProperty("category")] public NamedApiResource Category;
        [JsonProperty("crit_rate")] public int CritRate;
        // Positivo = drenaje, negativo = retroceso (% del daño infligido)
        [JsonProperty("drain")] public int Drain;
        [JsonProperty("flinch_chance")] public int FlinchChance;
        // % del MaxHP propio que cura
        [JsonProperty("healing")] public int Healing;
        [JsonProperty("max_hits")] public int? MaxHits;
        [JsonProperty("min_hits")] public int? MinHits;
        [JsonProperty("stat_chance")] public int StatChance;
    }

    public class MoveStatChange
    {
        [JsonProperty("change")] public int Change;
        [JsonProperty("stat")] public NamedApiResource Stat;
    }

    public class MoveEffectEntry
    {
        [JsonProperty("effect")] public string Effect;
        [JsonProperty("short_effect")] public string ShortEffect;
        [JsonProperty("language")] public NamedApiResource Language;
    }

    // ---------- ability/{id} ----------

    public class AbilityDto
    {
        [JsonProperty("id")] public int Id;
        [JsonProperty("name")] public string Name;
        [JsonProperty("names")] public List<LocalizedName> Names;
        [JsonProperty("flavor_text_entries")] public List<AbilityFlavorText> FlavorTextEntries;
    }

    public class AbilityFlavorText
    {
        [JsonProperty("flavor_text")] public string FlavorText;
        [JsonProperty("language")] public NamedApiResource Language;
    }

    // ---------- species_selection.json ----------

    public class SpeciesSelection
    {
        [JsonProperty("expandEvolutionLines")] public bool ExpandEvolutionLines = true;
        [JsonProperty("groups")] public Dictionary<string, List<int>> Groups;

        public HashSet<int> AllIds()
        {
            var ids = new HashSet<int>();
            if (Groups == null) return ids;
            foreach (var group in Groups.Values)
                foreach (int id in group)
                    if (id > 0) ids.Add(id);
            return ids;
        }
    }
}
