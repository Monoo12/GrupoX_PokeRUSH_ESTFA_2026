using System.Collections.Generic;
using System.Linq;

// Mapeos puros de datos de PokeAPI a los tipos del juego.
// Sin dependencias de Unity: toda la politica de importacion vive aca.
namespace PokeRush.EditorTools.PokeApi
{
    public static class PokeApiMapper
    {
        // Orden de preferencia para elegir el learnset por nivel:
        // el primer version group (de mas nuevo a mas viejo) donde la especie
        // tenga movimientos por subida de nivel.
        public static readonly string[] VersionGroupPreference =
        {
            "scarlet-violet", "sword-shield", "ultra-sun-ultra-moon", "sun-moon",
            "omega-ruby-alpha-sapphire", "x-y", "black-2-white-2", "black-white",
            "heartgold-soulsilver", "platinum", "diamond-pearl",
            "emerald", "firered-leafgreen", "ruby-sapphire",
            "crystal", "gold-silver", "yellow", "red-blue"
        };

        // Movimientos que NO deben importarse como usables aunque su categoria
        // pareciera soportada: su efecto real cambiaria demasiado el balance si
        // se ignora (ej: Descanso sin dormirse seria curacion gratis).
        private static readonly HashSet<string> NotImplementedBlacklist = new HashSet<string>
        {
            "rest", "belly-drum", "curse", "counter", "mirror-coat", "bide",
            "metronome", "transform", "mimic", "sketch", "substitute",
            "pain-split", "endeavor", "super-fang", "psywave", "seismic-toss",
            "night-shade", "dragon-rage", "sonic-boom", "self-destruct",
            "explosion", "misty-explosion", "final-gambit", "memento",
            "healing-wish", "lunar-dance", "geomancy", "shell-smash",
            "clangorous-soul", "fillet-away", "sleep-talk", "snore", "dream-eater"
        };

        // Movimientos de daño cuyo efecto real (cambiar de Poken al pegar) se
        // ignora: quedan como daño simple. Los de cargar un turno y los de
        // recargar ya NO van aca: estan implementados (ver ReglasDeTurnos).
        private static readonly HashSet<string> DamageOnlyList = new HashSet<string>
        {
            "u-turn", "volt-switch", "flip-turn"
        };

        // Movimientos FISICOS que NO hacen contacto (excepciones a la heuristica
        // "fisico = contacto"). Lista de los mas comunes; si falta alguno solo
        // significa que Static/Rough Skin se activarian de mas en ese caso.
        private static readonly HashSet<string> PhysicalNonContact = new HashSet<string>
        {
            "earthquake", "rock-slide", "stone-edge", "rock-throw", "rock-blast",
            "rock-tomb", "bulldoze", "magnitude", "fissure", "bonemerang",
            "bone-rush", "bone-club", "egg-bomb", "barrage", "fling",
            "gunk-shot", "poison-sting", "pin-missile", "twineedle",
            "spike-cannon", "razor-leaf", "petal-blizzard", "seed-bomb",
            "icicle-spear", "icicle-crash", "ice-shard", "smack-down",
            "pay-day", "present", "natural-gift", "magnet-bomb", "attack-order",
            "diamond-storm", "thousand-arrows", "dragon-darts", "scale-shot",
            "pyro-ball", "poltergeist", "stone-axe", "sand-tomb", "sky-drop",
            "beat-up", "feint", "spirit-shackle", "sacred-fire", "aura-wheel",
            "salt-cure", "hyper-drill", "barb-barrage", "mountain-gale"
        };

        private static readonly Dictionary<string, PokenType> TypeMap = new Dictionary<string, PokenType>
        {
            ["normal"] = PokenType.Normal, ["fire"] = PokenType.Fire,
            ["water"] = PokenType.Water, ["grass"] = PokenType.Grass,
            ["electric"] = PokenType.Electric, ["ice"] = PokenType.Ice,
            ["fighting"] = PokenType.Fighting, ["poison"] = PokenType.Poison,
            ["ground"] = PokenType.Ground, ["flying"] = PokenType.Flying,
            ["psychic"] = PokenType.Psychic, ["bug"] = PokenType.Bug,
            ["rock"] = PokenType.Rock, ["ghost"] = PokenType.Ghost,
            ["dragon"] = PokenType.Dragon, ["dark"] = PokenType.Dark,
            ["steel"] = PokenType.Steel, ["fairy"] = PokenType.Fairy
        };

        private static readonly Dictionary<string, Stat> StatMap = new Dictionary<string, Stat>
        {
            ["attack"] = Stat.Attack, ["defense"] = Stat.Defense,
            ["special-attack"] = Stat.SpecialAttack, ["special-defense"] = Stat.SpecialDefense,
            ["speed"] = Stat.Speed, ["accuracy"] = Stat.Accuracy, ["evasion"] = Stat.Evasion
        };

        private static readonly Dictionary<string, StatusEffect> AilmentMap = new Dictionary<string, StatusEffect>
        {
            ["paralysis"] = StatusEffect.Paralyzed, ["burn"] = StatusEffect.Burned,
            ["freeze"] = StatusEffect.Frozen, ["poison"] = StatusEffect.Poisoned,
            ["sleep"] = StatusEffect.Asleep, ["confusion"] = StatusEffect.Confused
        };

        // Nombre ingles de PokeAPI -> habilidad implementada en combate.
        private static readonly Dictionary<string, AbilityId> AbilityMap = new Dictionary<string, AbilityId>
        {
            ["intimidate"] = AbilityId.Intimidate, ["levitate"] = AbilityId.Levitate,
            ["volt-absorb"] = AbilityId.VoltAbsorb, ["water-absorb"] = AbilityId.WaterAbsorb,
            ["flash-fire"] = AbilityId.FlashFire, ["static"] = AbilityId.Static,
            ["poison-point"] = AbilityId.PoisonPoint, ["flame-body"] = AbilityId.FlameBody,
            ["sturdy"] = AbilityId.Sturdy, ["guts"] = AbilityId.Guts,
            ["speed-boost"] = AbilityId.SpeedBoost, ["blaze"] = AbilityId.Blaze,
            ["torrent"] = AbilityId.Torrent, ["overgrow"] = AbilityId.Overgrow,
            ["swarm"] = AbilityId.Swarm, ["rough-skin"] = AbilityId.RoughSkin
        };

        // Movimientos que instalan clima / efectos de campo (por nombre PokeAPI).
        // Estos SI se implementan aunque su categoria sea whole-field-effect.
        private static readonly Dictionary<string, WeatherKind> WeatherMoves = new Dictionary<string, WeatherKind>
        {
            ["rain-dance"] = WeatherKind.Rain, ["sunny-day"] = WeatherKind.Sun,
            ["sandstorm"] = WeatherKind.Sandstorm, ["hail"] = WeatherKind.Hail,
            ["snowscape"] = WeatherKind.Hail // la version moderna del granizo
        };

        private static readonly Dictionary<string, FieldEffectKind> FieldMoves = new Dictionary<string, FieldEffectKind>
        {
            ["reflect"] = FieldEffectKind.Reflect, ["light-screen"] = FieldEffectKind.LightScreen,
            ["aurora-veil"] = FieldEffectKind.AuroraVeil, ["stealth-rock"] = FieldEffectKind.StealthRock,
            ["spikes"] = FieldEffectKind.Spikes, ["toxic-spikes"] = FieldEffectKind.ToxicSpikes,
            ["sticky-web"] = FieldEffectKind.StickyWeb,
            ["electric-terrain"] = FieldEffectKind.ElectricTerrain,
            ["psychic-terrain"] = FieldEffectKind.PsychicTerrain,
            ["grassy-terrain"] = FieldEffectKind.GrassyTerrain,
            ["misty-terrain"] = FieldEffectKind.MistyTerrain
        };

        public static WeatherKind WeatherFor(string apiName)
            => apiName != null && WeatherMoves.TryGetValue(apiName, out WeatherKind w) ? w : WeatherKind.None;

        public static FieldEffectKind FieldEffectFor(string apiName)
            => apiName != null && FieldMoves.TryGetValue(apiName, out FieldEffectKind f) ? f : FieldEffectKind.None;

        // ---------- nombres ----------

        // Nombre oficial en español; si PokeAPI no lo tiene, cae al ingles.
        public static string SpanishName(List<LocalizedName> names, string fallback)
        {
            if (names != null)
            {
                var es = names.FirstOrDefault(n => n.Language?.Name == "es");
                if (es != null && !string.IsNullOrEmpty(es.Name)) return es.Name;
                var en = names.FirstOrDefault(n => n.Language?.Name == "en");
                if (en != null && !string.IsNullOrEmpty(en.Name)) return en.Name;
            }
            return fallback;
        }

        public static string SpanishFlavorText(List<AbilityFlavorText> entries)
        {
            if (entries == null) return "";
            var es = entries.LastOrDefault(e => e.Language?.Name == "es");
            if (es != null) return CleanFlavor(es.FlavorText);
            var en = entries.LastOrDefault(e => e.Language?.Name == "en");
            return en != null ? CleanFlavor(en.FlavorText) : "";
        }

        private static string CleanFlavor(string text)
            => string.IsNullOrEmpty(text) ? "" : text.Replace('\n', ' ').Replace('\f', ' ');

        // ---------- tipos / stats / estados / habilidades ----------

        public static PokenType MapType(string apiName)
            => apiName != null && TypeMap.TryGetValue(apiName, out PokenType t) ? t : PokenType.None;

        public static bool TryMapStat(string apiName, out Stat stat)
        {
            stat = Stat.Attack;
            return apiName != null && StatMap.TryGetValue(apiName, out stat);
        }

        public static bool TryMapAilment(string apiName, out StatusEffect status)
        {
            status = StatusEffect.None;
            return apiName != null && AilmentMap.TryGetValue(apiName, out status);
        }

        public static AbilityId MapAbility(string apiName)
            => apiName != null && AbilityMap.TryGetValue(apiName, out AbilityId id) ? id : AbilityId.None;

        public static AttackType MapDamageClass(string apiName)
        {
            switch (apiName)
            {
                case "physical": return AttackType.Physical;
                case "special": return AttackType.Special;
                default: return AttackType.Status;
            }
        }

        // ---------- learnset ----------

        // Extrae el learnset por nivel del version group preferido.
        // Devuelve pares (idDeMovimiento, nivel), nivel 0 -> 1.
        public static List<(int moveId, string moveName, int level)> ExtractLevelUpLearnset(PokemonDto pokemon)
        {
            var result = new List<(int, string, int)>();
            if (pokemon?.Moves == null) return result;

            // Version groups con algun movimiento por nivel para esta especie
            var groupsWithLevelUp = new HashSet<string>();
            foreach (var entry in pokemon.Moves)
            {
                if (entry.VersionGroupDetails == null) continue;
                foreach (var detail in entry.VersionGroupDetails)
                    if (detail.MoveLearnMethod?.Name == "level-up")
                        groupsWithLevelUp.Add(detail.VersionGroup?.Name);
            }

            string chosenGroup = VersionGroupPreference.FirstOrDefault(g => groupsWithLevelUp.Contains(g));
            if (chosenGroup == null) return result;

            foreach (var entry in pokemon.Moves)
            {
                if (entry.VersionGroupDetails == null || entry.Move == null) continue;
                var detail = entry.VersionGroupDetails.FirstOrDefault(d =>
                    d.MoveLearnMethod?.Name == "level-up" && d.VersionGroup?.Name == chosenGroup);
                if (detail == null) continue;

                int level = detail.LevelLearnedAt < 1 ? 1 : detail.LevelLearnedAt;
                result.Add((entry.Move.IdFromUrl(), entry.Move.Name, level));
            }

            result.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            return result;
        }

        // ---------- movimientos de dos turnos ----------

        public struct ReglaDeTurnos
        {
            public MoveTurnRule Regla;
            public string MensajeDeCarga;   // {0} = el Poken
            public bool SubeStatsAlCargar;  // los cambios de stats del movimiento van al turno de carga
            public WeatherKind SinCargaCon; // con ese clima pega en el acto
        }

        // PokeAPI no trae un flag de "carga" o "recarga": va por nombre.
        private static readonly Dictionary<string, ReglaDeTurnos> ReglasDeTurnos = new Dictionary<string, ReglaDeTurnos>
        {
            // Cargan un turno
            ["solar-beam"] = Carga("¡{0} está absorbiendo luz!", sinCargaCon: WeatherKind.Sun),
            ["solar-blade"] = Carga("¡{0} está absorbiendo luz!", sinCargaCon: WeatherKind.Sun),
            ["sky-attack"] = Carga("¡{0} se rodeó de una luz intensa!"),
            ["skull-bash"] = Carga("¡{0} agachó la cabeza!", subeStats: true),
            ["razor-wind"] = Carga("¡{0} provocó un remolino!"),
            ["meteor-beam"] = Carga("¡{0} rebosa de energía cósmica!", subeStats: true),
            ["electro-shot"] = Carga("¡{0} está absorbiendo electricidad!", subeStats: true, sinCargaCon: WeatherKind.Rain),
            ["freeze-shock"] = Carga("¡{0} se rodeó de un aire gélido!"),
            ["ice-burn"] = Carga("¡{0} se rodeó de un aire helado!"),

            // Cargan un turno fuera de alcance
            ["fly"] = FueraDeAlcance("¡{0} voló muy alto!"),
            ["bounce"] = FueraDeAlcance("¡{0} dio un gran salto!"),
            ["dig"] = FueraDeAlcance("¡{0} se escondió bajo tierra!"),
            ["dive"] = FueraDeAlcance("¡{0} se sumergió!"),
            ["phantom-force"] = FueraDeAlcance("¡{0} desapareció de repente!"),
            ["shadow-force"] = FueraDeAlcance("¡{0} desapareció de repente!"),

            // Pegan y descansan el turno siguiente
            ["hyper-beam"] = Recarga(),
            ["giga-impact"] = Recarga(),
            ["blast-burn"] = Recarga(),
            ["hydro-cannon"] = Recarga(),
            ["frenzy-plant"] = Recarga(),
            ["rock-wrecker"] = Recarga(),
            ["roar-of-time"] = Recarga(),
            ["prismatic-laser"] = Recarga(),
            ["eternabeam"] = Recarga(),
            ["meteor-assault"] = Recarga()
        };

        // Metodos y no campos: el diccionario de arriba se inicializa antes que
        // cualquier campo estatico declarado despues.
        private static ReglaDeTurnos Carga(string mensaje, bool subeStats = false, WeatherKind sinCargaCon = WeatherKind.None)
            => new ReglaDeTurnos
            {
                Regla = MoveTurnRule.Charge, MensajeDeCarga = mensaje,
                SubeStatsAlCargar = subeStats, SinCargaCon = sinCargaCon
            };

        private static ReglaDeTurnos FueraDeAlcance(string mensaje)
            => new ReglaDeTurnos { Regla = MoveTurnRule.ChargeInvulnerable, MensajeDeCarga = mensaje };

        private static ReglaDeTurnos Recarga()
            => new ReglaDeTurnos { Regla = MoveTurnRule.Recharge };

        public static bool TryGetTurnRule(string moveName, out ReglaDeTurnos regla)
            => ReglasDeTurnos.TryGetValue(moveName ?? "", out regla);

        public static IEnumerable<string> MovimientosConReglaDeTurnos => ReglasDeTurnos.Keys;

        // ---------- ataques de respaldo ----------

        // Un ataque basico por tipo (englishName) para las especies que no
        // aprenden ninguno ni por nivel ni por MT/tutor/huevo (Cosmog) o que no
        // vienen de PokeAPI (las de prueba).
        public static readonly Dictionary<PokenType, string> AtaqueBasicoPorTipo = new Dictionary<PokenType, string>
        {
            [PokenType.Normal] = "tackle", [PokenType.Fire] = "ember", [PokenType.Water] = "water-gun",
            [PokenType.Grass] = "vine-whip", [PokenType.Electric] = "thunder-shock", [PokenType.Ice] = "powder-snow",
            [PokenType.Fighting] = "karate-chop", [PokenType.Poison] = "poison-sting", [PokenType.Ground] = "mud-slap",
            [PokenType.Flying] = "gust", [PokenType.Psychic] = "confusion", [PokenType.Bug] = "struggle-bug",
            [PokenType.Rock] = "rock-throw", [PokenType.Ghost] = "lick", [PokenType.Dragon] = "twister",
            [PokenType.Dark] = "bite", [PokenType.Steel] = "metal-claw", [PokenType.Fairy] = "fairy-wind"
        };

        // Todos los movimientos que la especie aprende por un metodo que NO es
        // subir de nivel (MT, tutor, huevo...) en algun version group de la
        // lista de preferencia. Sin repetir.
        public static List<int> ExtractOtherMethodMoveIds(PokemonDto pokemon)
        {
            var result = new List<int>();
            if (pokemon?.Moves == null) return result;

            var grupos = new HashSet<string>(VersionGroupPreference);
            foreach (var entry in pokemon.Moves)
            {
                if (entry.Move == null || entry.VersionGroupDetails == null) continue;
                bool otroMetodo = entry.VersionGroupDetails.Any(d =>
                    d.MoveLearnMethod?.Name != "level-up" && grupos.Contains(d.VersionGroup?.Name));
                int id = entry.Move.IdFromUrl();
                if (otroMetodo && id > 0 && !result.Contains(id)) result.Add(id);
            }
            return result;
        }

        // ---------- clasificacion de movimientos ----------

        public static MoveImplementation ClassifyMove(MoveDto move)
        {
            if (NotImplementedBlacklist.Contains(move.Name))
                return MoveImplementation.NotImplemented;

            bool hasPower = (move.Power ?? 0) > 0;

            if (DamageOnlyList.Contains(move.Name))
                return hasPower ? MoveImplementation.DamageOnly : MoveImplementation.NotImplemented;

            string category = move.Meta?.Category?.Name;
            if (category == null)
                return hasPower ? MoveImplementation.DamageOnly : MoveImplementation.NotImplemented;

            switch (category)
            {
                case "damage":
                case "damage+heal":
                    return hasPower ? MoveImplementation.Full : MoveImplementation.NotImplemented;

                case "damage+ailment":
                    if (!hasPower) return MoveImplementation.NotImplemented;
                    return TryMapAilment(move.Meta.Ailment?.Name, out _)
                        ? MoveImplementation.Full
                        : MoveImplementation.DamageOnly;

                case "damage+lower":
                case "damage+raise":
                    if (!hasPower) return MoveImplementation.NotImplemented;
                    return HasAnyMappableStat(move) ? MoveImplementation.Full : MoveImplementation.DamageOnly;

                case "ailment":
                case "swagger":
                    return TryMapAilment(move.Meta.Ailment?.Name, out _)
                        ? MoveImplementation.Full
                        : MoveImplementation.NotImplemented;

                case "net-good-stats":
                    return HasAnyMappableStat(move) ? MoveImplementation.Full : MoveImplementation.NotImplemented;

                case "heal":
                    return move.Meta.Healing > 0 ? MoveImplementation.Full : MoveImplementation.NotImplemented;

                case "ohko":
                case "whole-field-effect":
                case "field-effect":
                case "force-switch":
                case "unique":
                default:
                    return hasPower ? MoveImplementation.DamageOnly : MoveImplementation.NotImplemented;
            }
        }

        private static bool HasAnyMappableStat(MoveDto move)
            => move.StatChanges != null && move.StatChanges.Any(sc => TryMapStat(sc.Stat?.Name, out _));

        // Heuristica de contacto: fisico = contacto salvo excepciones conocidas.
        // (PokeAPI no expone el flag de contacto en el endpoint de move.)
        public static bool MakesContact(MoveDto move)
        {
            if (move.DamageClass?.Name != "physical") return false;
            return !PhysicalNonContact.Contains(move.Name);
        }

        // Los cambios de stats afectan al usuario si el movimiento se apunta a si
        // mismo (Danza Espada) o si es de categoria damage+raise (Poder Pasado).
        public static bool StatChangesTargetSelf(MoveDto move)
            => move.Target?.Name == "user"
               || move.Target?.Name == "user-and-allies"
               || move.Meta?.Category?.Name == "damage+raise";
    }
}
