using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Smoke tests de la logica pura del combate y la IA. Se corren desde el menu
// PokeRush > Correr smoke tests y reportan a la Console. Usan SeededRng para
// que los resultados sean reproducibles. (Sin asmdefs en el proyecto, esto
// reemplaza al Unity Test Framework; migrar a UTF si algun dia se agregan.)
namespace PokeRush.EditorTools
{
    public static class PokeRushSmokeTests
    {
        private static int _passed;
        private static int _failed;

        [MenuItem("PokeRush/Correr smoke tests")]
        public static void RunAll()
        {
            _passed = 0;
            _failed = 0;

            TestIvSetRoundtrip();
            TestStatFormula();
            TestStatStages();
            TestTypeChartNone();
            TestLearnsetSelection();
            TestXpFormula();
            TestBurnResidual();
            TestParalysisRate();
            TestSleepWakes();
            TestSturdy();
            TestLevitate();
            TestIntimidate();
            TestStruggle();
            TestTurnEngineDeterminism();
            TestMultiHitRange();
            TestWeatherDamageModifiers();
            TestWeatherResidualAndEnd();
            TestStealthRockOnSwitchIn();
            TestScreensHalveDamage();
            TestTrainerNeverPicksIneffective();
            TestTrainerSkipsRedundantStatus();
            TestGymLeaderPrefersGuaranteedKo();
            TestGymLeaderSwitchesOnBadMatchup();
            TestWildFavorsSuperEffective();

            if (_failed == 0)
                Debug.Log($"[SmokeTests] TODOS OK: {_passed} checks pasaron.");
            else
                Debug.LogError($"[SmokeTests] {_failed} FALLARON, {_passed} pasaron. Revisar errores arriba.");
        }

        private static void Check(bool condition, string name)
        {
            if (condition) _passed++;
            else
            {
                _failed++;
                Debug.LogError($"[SmokeTests] FALLO: {name}");
            }
        }

        // ---------- helpers de construccion ----------

        private static PokenDefinition MakeSpecies(
            string name, PokenType t1, PokenType t2,
            int hp, int atk, int def, int spa, int spd, int spe,
            AbilityId ability = AbilityId.None)
        {
            var species = ScriptableObject.CreateInstance<PokenDefinition>();
            species.pokenName = name;
            species.type = t1;
            species.type2 = t2;
            species.baseHP = hp;
            species.baseAttack = atk;
            species.baseDefense = def;
            species.baseSpecialAttack = spa;
            species.baseSpecialDefense = spd;
            species.baseSpeed = spe;
            species.abilityId = ability;
            species.baseXpYield = 60;
            return species;
        }

        private static MoveDefinition MakeMove(
            string name, PokenType type, AttackType attackType,
            int power, int accuracy, int pp)
        {
            var move = ScriptableObject.CreateInstance<MoveDefinition>();
            move.moveName = name;
            move.type = type;
            move.attackType = attackType;
            move.power = power;
            move.accuracy = accuracy;
            move.maxPP = pp;
            move.implementation = MoveImplementation.Full;
            return move;
        }

        private static readonly IvSet FixedIvs = new IvSet
        { hp = 15, attack = 15, defense = 15, specialAttack = 15, specialDefense = 15, speed = 15 };

        private static PokenModel MakePoken(PokenDefinition species, int level, params MoveDefinition[] moves)
        {
            species.moves = moves;
            return new PokenModel(species, level, FixedIvs);
        }

        private static BattleModel MakeBattle(PokenModel playerPoken, PokenModel enemyPoken,
            params PokenModel[] enemyBench)
        {
            var player = new PlayerModel("Tester");
            player.Team.Add(playerPoken);

            var enemy = new EnemyModel(enemyPoken.Definition, enemyPoken.Level);
            enemy.Team.Clear();
            enemy.Team.Add(enemyPoken);
            foreach (PokenModel bench in enemyBench) enemy.Team.Add(bench);

            var model = new BattleModel(player, enemy);
            model.SwitchPlayerPoken(0);
            model.SwitchEnemyPoken(0);
            return model;
        }

        private static AiContext MakeEnemyContext(BattleModel model, AIProfileDefinition profile)
            => AiContext.ForEnemy(model, profile);

        // ---------- datos y formulas ----------

        private static void TestIvSetRoundtrip()
        {
            var ivs = new IvSet { hp = 31, attack = 12, defense = 0, specialAttack = 5, specialDefense = 20, speed = 31 };
            Check(IvSet.TryParse(ivs.Serialize(), out IvSet parsed), "IvSet: parse del serializado");
            Check(parsed.hp == 31 && parsed.attack == 12 && parsed.defense == 0
                  && parsed.specialAttack == 5 && parsed.specialDefense == 20 && parsed.speed == 31,
                "IvSet: roundtrip conserva los valores");
            Check(!IvSet.TryParse(null, out _), "IvSet: null no parsea (save viejo)");
            Check(!IvSet.TryParse("1,2,3", out _), "IvSet: formato invalido no parsea");
        }

        private static void TestStatFormula()
        {
            // Stats tipo Pikachu: 35/55/40/50/50/90
            var species = MakeSpecies("Chu", PokenType.Electric, PokenType.None, 35, 55, 40, 50, 50, 90);
            species.moves = new MoveDefinition[0];

            var maxIvs = new IvSet { hp = 31, attack = 31, defense = 31, specialAttack = 31, specialDefense = 31, speed = 31 };
            var poken31 = new PokenModel(species, 50, maxIvs);
            // HP = ((2*35+31)*50)/100 + 50 + 10 = 110 ; Atk = ((2*55+31)*50)/100 + 5 = 75
            Check(poken31.MaxHP == 110, $"Formula: HP con IV 31 (esperado 110, dio {poken31.MaxHP})");
            Check(poken31.Attack == 75, $"Formula: Ataque con IV 31 (esperado 75, dio {poken31.Attack})");

            var poken0 = new PokenModel(species, 50, new IvSet());
            // HP = (70*50)/100 + 60 = 95 ; Atk = (110*50)/100 + 5 = 60
            Check(poken0.MaxHP == 95, $"Formula: HP con IV 0 (esperado 95, dio {poken0.MaxHP})");
            Check(poken0.Attack == 60, $"Formula: Ataque con IV 0 (esperado 60, dio {poken0.Attack})");
        }

        private static void TestStatStages()
        {
            Check(Mathf.Approximately(StatStageTable.Multiplier(0), 1f), "Stages: x1 en 0");
            Check(Mathf.Approximately(StatStageTable.Multiplier(-6), 0.25f), "Stages: x0.25 en -6");
            Check(Mathf.Approximately(StatStageTable.Multiplier(6), 4f), "Stages: x4 en +6");
            Check(Mathf.Approximately(StatStageTable.Multiplier(2), 2f), "Stages: x2 en +2");
            Check(Mathf.Approximately(StatStageTable.AccuracyMultiplier(6), 3f), "Stages: precision x3 en +6");
            Check(Mathf.Approximately(StatStageTable.AccuracyMultiplier(-6), 1f / 3f), "Stages: precision x1/3 en -6");
        }

        private static void TestTypeChartNone()
        {
            Check(Mathf.Approximately(TypeChart.Effectiveness(PokenType.None, PokenType.Water), 1f),
                "TypeChart: tipo None es neutro (Forcejeo/confusion)");
            Check(Mathf.Approximately(TypeChart.Effectiveness(PokenType.Electric, PokenType.Ground), 0f),
                "TypeChart: Electric vs Ground es inmune");
        }

        private static void TestLearnsetSelection()
        {
            var species = MakeSpecies("Learner", PokenType.Normal, PokenType.None, 50, 50, 50, 50, 50, 50);
            var m1 = MakeMove("M1", PokenType.Normal, AttackType.Physical, 40, 100, 30);
            var m4 = MakeMove("M4", PokenType.Normal, AttackType.Physical, 40, 100, 30);
            var m7 = MakeMove("M7", PokenType.Normal, AttackType.Physical, 40, 100, 30);
            var m10 = MakeMove("M10", PokenType.Normal, AttackType.Physical, 40, 100, 30);
            var m15 = MakeMove("M15", PokenType.Normal, AttackType.Physical, 40, 100, 30);
            species.learnset = new[]
            {
                new LearnsetEntry { move = m1, level = 1 },
                new LearnsetEntry { move = m4, level = 4 },
                new LearnsetEntry { move = m7, level = 7 },
                new LearnsetEntry { move = m10, level = 10 },
                new LearnsetEntry { move = m15, level = 15 }
            };

            var poken = new PokenModel(species, 12, FixedIvs);
            Check(poken.Moves.Count == 4, $"Learnset: 4 movimientos a nivel 12 (dio {poken.Moves.Count})");
            Check(poken.Moves.Exists(m => m.Definition == m10), "Learnset: incluye el de nivel 10");
            Check(!poken.Moves.Exists(m => m.Definition == m15), "Learnset: excluye el de nivel 15");
        }

        private static void TestXpFormula()
        {
            var species = MakeSpecies("Xp", PokenType.Normal, PokenType.None, 50, 50, 50, 50, 50, 50);
            species.baseXpYield = 112;
            species.moves = new MoveDefinition[0];
            var fainted = new PokenModel(species, 7, FixedIvs);
            Check(RewardCalculator.XpForDefeating(fainted) == 112, "XP: baseXpYield*nivel/7");
        }

        // ---------- estados y habilidades ----------

        private static void TestBurnResidual()
        {
            var species = MakeSpecies("Burnie", PokenType.Normal, PokenType.None, 100, 50, 50, 50, 50, 50);
            var poken = MakePoken(species, 50);
            poken.SetStatus(StatusEffect.Burned);
            var state = new BattlePokenState(poken, isPlayerSide: true);

            int before = poken.CurrentHP;
            var events = new List<BattleEvent>();
            StatusEffects.OnTurnEnd(state, events);
            int expected = poken.MaxHP / 16;
            Check(before - poken.CurrentHP == expected,
                $"Quemadura: pierde MaxHP/16 por turno (esperado {expected}, dio {before - poken.CurrentHP})");
        }

        private static void TestParalysisRate()
        {
            var species = MakeSpecies("Para", PokenType.Normal, PokenType.None, 100, 50, 50, 50, 50, 100);
            var rng = new SeededRng(42);
            int skips = 0;
            for (int i = 0; i < 1000; i++)
            {
                var poken = MakePoken(species, 50);
                poken.SetStatus(StatusEffect.Paralyzed);
                var state = new BattlePokenState(poken, true);
                if (!StatusEffects.CanAct(state, new List<BattleEvent>(), rng)) skips++;
            }
            Check(skips > 190 && skips < 310, $"Paralisis: ~25% pierde el turno (dio {skips}/1000)");

            // La paralisis tambien reduce la velocidad a la mitad
            var slow = MakePoken(species, 50);
            var fast = MakePoken(species, 50);
            slow.SetStatus(StatusEffect.Paralyzed);
            var slowState = new BattlePokenState(slow, true);
            var fastState = new BattlePokenState(fast, false);
            Check(slowState.EffectiveSpeed == fastState.EffectiveSpeed / 2,
                "Paralisis: velocidad a la mitad");
        }

        private static void TestSleepWakes()
        {
            var species = MakeSpecies("Sleepy", PokenType.Normal, PokenType.None, 100, 50, 50, 50, 50, 50);
            var rng = new SeededRng(7);
            for (int trial = 0; trial < 50; trial++)
            {
                var poken = MakePoken(species, 50);
                var state = new BattlePokenState(poken, true);
                StatusEffects.TryApplyStatus(state, StatusEffect.Asleep, new List<BattleEvent>(), rng, false);

                int turns = 0;
                while (poken.CurrentStatus == StatusEffect.Asleep && turns < 10)
                {
                    StatusEffects.CanAct(state, new List<BattleEvent>(), rng);
                    turns++;
                }
                if (turns > 3)
                {
                    Check(false, $"Sueño: desperto en {turns} turnos (maximo 3)");
                    return;
                }
            }
            Check(true, "Sueño: siempre despierta en 1-3 turnos");
        }

        private static void TestSturdy()
        {
            var species = MakeSpecies("Rocky", PokenType.Rock, PokenType.None, 60, 50, 50, 50, 50, 50, AbilityId.Sturdy);
            var poken = MakePoken(species, 30);
            var state = new BattlePokenState(poken, false);

            int damage = AbilityEffects.ModifyIncomingDamage(state, 9999, out bool endured);
            Check(endured && damage == poken.CurrentHP - 1, "Robustez: aguanta el golpe letal con 1 HP");

            // Con HP no lleno, no se activa
            poken.TakeDamage(1);
            AbilityEffects.ModifyIncomingDamage(state, 9999, out bool enduredHurt);
            Check(!enduredHurt, "Robustez: no se activa con HP incompleto");
        }

        private static void TestLevitate()
        {
            var species = MakeSpecies("Flo", PokenType.Ghost, PokenType.None, 60, 50, 50, 50, 50, 50, AbilityId.Levitate);
            var state = new BattlePokenState(MakePoken(species, 30), false);
            Check(AbilityEffects.CheckAbsorbOrImmunity(state, PokenType.Ground, new List<BattleEvent>()),
                "Levitacion: anula movimientos de tipo Ground");
            Check(!AbilityEffects.CheckAbsorbOrImmunity(state, PokenType.Water, new List<BattleEvent>()),
                "Levitacion: no anula otros tipos");
        }

        private static void TestIntimidate()
        {
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
            var playerPoken = MakePoken(
                MakeSpecies("Victima", PokenType.Normal, PokenType.None, 60, 50, 50, 50, 50, 50), 20, tackle);
            var enemyPoken = MakePoken(
                MakeSpecies("Intimidador", PokenType.Normal, PokenType.None, 60, 50, 50, 50, 50, 50, AbilityId.Intimidate),
                20, tackle);

            var model = MakeBattle(playerPoken, enemyPoken);
            var engine = new TurnEngine(model, new SeededRng(1));
            engine.BeginBattle();

            Check(model.PlayerState.GetStage(Stat.Attack) == -1,
                "Intimidacion: -1 Ataque del rival al entrar");
        }

        private static void TestStruggle()
        {
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
            var playerPoken = MakePoken(
                MakeSpecies("SinPP", PokenType.Normal, PokenType.None, 100, 50, 50, 50, 50, 90), 30, tackle);
            var enemyPoken = MakePoken(
                MakeSpecies("Rival", PokenType.Normal, PokenType.None, 100, 50, 50, 50, 50, 10), 30, tackle);
            playerPoken.Moves[0].SetCurrentPP(0);

            var model = MakeBattle(playerPoken, enemyPoken);
            var engine = new TurnEngine(model, new SeededRng(3));
            int enemyHpBefore = enemyPoken.CurrentHP;
            engine.ResolveTurn(BattleAction.Struggle(), BattleAction.Move(0));

            Check(enemyPoken.CurrentHP < enemyHpBefore, "Forcejeo: hace daño al rival");
            // El retroceso es 25% del MaxHP propio (mas el golpe del rival)
            Check(playerPoken.CurrentHP <= playerPoken.MaxHP - playerPoken.MaxHP / 4,
                "Forcejeo: el usuario sufre 25% de su MaxHP de retroceso");
        }

        private static void TestTurnEngineDeterminism()
        {
            List<string> RunOnce()
            {
                var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
                var ember = MakeMove("Ascuas", PokenType.Fire, AttackType.Special, 40, 100, 25);
                ember.effect = StatusEffect.Burned;
                ember.effectChance = 10;

                var playerPoken = MakePoken(
                    MakeSpecies("A", PokenType.Normal, PokenType.None, 80, 60, 50, 50, 50, 70), 25, tackle);
                var enemyPoken = MakePoken(
                    MakeSpecies("B", PokenType.Fire, PokenType.None, 80, 50, 50, 60, 50, 60), 25, ember);

                var model = MakeBattle(playerPoken, enemyPoken);
                var engine = new TurnEngine(model, new SeededRng(99));
                var messages = new List<string>();
                for (int turn = 0; turn < 5 && !model.IsBattleOver; turn++)
                    foreach (BattleEvent ev in engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0)))
                        if (!string.IsNullOrEmpty(ev.Message)) messages.Add(ev.Message);
                return messages;
            }

            List<string> first = RunOnce();
            List<string> second = RunOnce();
            bool equal = first.Count == second.Count;
            for (int i = 0; equal && i < first.Count; i++)
                equal = first[i] == second[i];
            Check(equal && first.Count > 0,
                "Determinismo: misma semilla produce exactamente los mismos eventos");
        }

        private static void TestMultiHitRange()
        {
            var multiHit = MakeMove("Pua Multiple", PokenType.Normal, AttackType.Physical, 15, 100, 30);
            multiHit.minHits = 2;
            multiHit.maxHits = 5;

            int totalHits = 0, samples = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                var playerPoken = MakePoken(
                    MakeSpecies("Multi", PokenType.Normal, PokenType.None, 100, 60, 50, 50, 50, 90), 30, multiHit);
                var enemyPoken = MakePoken(
                    MakeSpecies("Tanque", PokenType.Steel, PokenType.None, 200, 10, 200, 10, 200, 10), 50,
                    MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35));

                var model = MakeBattle(playerPoken, enemyPoken);
                var engine = new TurnEngine(model, new SeededRng(seed));
                foreach (BattleEvent ev in engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0)))
                {
                    if (ev.Message == null || !ev.Message.StartsWith("¡Golpeó ")) continue;
                    int hits = int.Parse(ev.Message.Split(' ')[1]);
                    Check(hits >= 2 && hits <= 5, $"Multi-golpe: {hits} golpes fuera del rango 2-5");
                    totalHits += hits;
                    samples++;
                }
            }
            float average = samples > 0 ? (float)totalHits / samples : 0f;
            Check(average > 2.6f && average < 3.6f,
                $"Multi-golpe: promedio ~3.1 golpes (dio {average:F2} sobre {samples} usos)");
        }

        // ---------- clima y efectos de campo ----------

        private static void TestWeatherDamageModifiers()
        {
            var waterGun = MakeMove("Pistola Agua", PokenType.Water, AttackType.Special, 40, 100, 25);
            var attacker = MakePoken(
                MakeSpecies("Atacante", PokenType.Water, PokenType.None, 80, 50, 50, 60, 50, 60), 30, waterGun);
            var defender = MakePoken(
                MakeSpecies("Defensor", PokenType.Normal, PokenType.None, 80, 50, 50, 50, 50, 50), 30,
                MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35));

            var model = MakeBattle(defender, attacker); // atacante = lado rival
            int normal = DamageCalculator.CalculateExpected(model.EnemyState, model.PlayerState, waterGun, model).Damage;

            model.SetWeather(WeatherKind.Rain, 5);
            int rain = DamageCalculator.CalculateExpected(model.EnemyState, model.PlayerState, waterGun, model).Damage;
            model.SetWeather(WeatherKind.Sun, 5);
            int sun = DamageCalculator.CalculateExpected(model.EnemyState, model.PlayerState, waterGun, model).Damage;

            Check(rain > normal && Mathf.Abs(rain - normal * 1.5f) <= 2,
                $"Clima: la lluvia potencia Agua x1.5 ({normal} -> {rain})");
            Check(sun < normal, $"Clima: el sol debilita Agua ({normal} -> {sun})");
        }

        private static void TestWeatherResidualAndEnd()
        {
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
            var playerPoken = MakePoken(
                MakeSpecies("Normalito", PokenType.Normal, PokenType.None, 160, 10, 200, 10, 200, 60), 50, tackle);
            var enemyPoken = MakePoken(
                MakeSpecies("Rocoso", PokenType.Rock, PokenType.None, 160, 10, 200, 10, 200, 50), 50, tackle);

            var model = MakeBattle(playerPoken, enemyPoken);
            var engine = new TurnEngine(model, new SeededRng(21));
            model.SetWeather(WeatherKind.Sandstorm, 2);

            int playerHp = playerPoken.CurrentHP;
            int enemyHp = enemyPoken.CurrentHP;
            engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));

            // El Normal sufre la tormenta ademas del golpe; el Roca solo el golpe
            int playerLoss = playerHp - playerPoken.CurrentHP;
            Check(playerLoss >= playerPoken.MaxHP / 16, "Clima: la tormenta daña a los no inmunes");
            Check(model.WeatherTurnsLeft == 1, $"Clima: el contador baja por turno (quedan {model.WeatherTurnsLeft})");

            engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));
            Check(model.CurrentWeather == WeatherKind.None, "Clima: termina al agotarse los turnos");
        }

        private static void TestStealthRockOnSwitchIn()
        {
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
            var lead = MakePoken(
                MakeSpecies("Lider", PokenType.Normal, PokenType.None, 100, 50, 50, 50, 50, 90), 30, tackle);
            var bird = MakePoken(
                MakeSpecies("Pajaro", PokenType.Flying, PokenType.None, 100, 50, 50, 50, 50, 70), 30, tackle);
            var enemyPoken = MakePoken(
                MakeSpecies("Rival", PokenType.Normal, PokenType.None, 100, 50, 50, 50, 50, 10), 30, tackle);

            var player = new PlayerModel("Tester");
            player.Team.Add(lead);
            player.Team.Add(bird);
            var enemy = new EnemyModel(enemyPoken.Definition, 30);
            enemy.Team.Clear();
            enemy.Team.Add(enemyPoken);
            var model = new BattleModel(player, enemy);
            model.SwitchPlayerPoken(0);
            model.SwitchEnemyPoken(0);

            // Trampa Rocas en el lado del jugador; el volador entra y recibe x2
            model.PlayerSide.StealthRock = true;
            var engine = new TurnEngine(model, new SeededRng(23));
            engine.ResolveTurn(BattleAction.Switch(1), BattleAction.Move(0));

            // Volador: eff Roca x2 -> MaxHP * 2/8 = 25 de 100
            int expected = (int)(bird.MaxHP * 2f / 8f);
            int lost = bird.MaxHP - bird.CurrentHP;
            Check(lost >= expected, $"Trampa Rocas: daño por efectividad al entrar (perdió {lost}, esperado >= {expected})");
        }

        private static void TestScreensHalveDamage()
        {
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
            var attacker = MakePoken(
                MakeSpecies("Atacante", PokenType.Normal, PokenType.None, 80, 60, 50, 60, 50, 60), 30, tackle);
            var defender = MakePoken(
                MakeSpecies("Protegido", PokenType.Normal, PokenType.None, 80, 50, 50, 50, 50, 50), 30, tackle);

            var model = MakeBattle(defender, attacker); // defensor = lado jugador
            int normal = DamageCalculator.CalculateExpected(model.EnemyState, model.PlayerState, tackle, model).Damage;
            model.PlayerSide.ReflectTurns = 5;
            int screened = DamageCalculator.CalculateExpected(model.EnemyState, model.PlayerState, tackle, model).Damage;

            Check(screened < normal && Mathf.Abs(screened - normal / 2) <= 2,
                $"Pantallas: Reflejo reduce el daño físico a la mitad ({normal} -> {screened})");
        }

        // ---------- IA ----------

        private static void TestTrainerNeverPicksIneffective()
        {
            var earthquake = MakeMove("Terremoto", PokenType.Ground, AttackType.Physical, 100, 100, 10);
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);

            var enemyPoken = MakePoken(
                MakeSpecies("IA", PokenType.Ground, PokenType.None, 80, 70, 50, 50, 50, 60), 30, earthquake, tackle);
            var playerPoken = MakePoken(
                MakeSpecies("Pajaro", PokenType.Flying, PokenType.None, 80, 50, 50, 50, 50, 70), 30, tackle);

            var model = MakeBattle(playerPoken, enemyPoken);
            var profile = BattleAIFactory.CreateDefaultProfile(BattleAIType.Trainer);
            var ai = new TrainerAI();
            var rng = new SeededRng(5);

            for (int i = 0; i < 500; i++)
            {
                BattleAction action = ai.ChooseAction(MakeEnemyContext(model, profile), rng);
                if (action.Kind == BattleActionKind.UseMove && action.MoveIndex == 0)
                {
                    Check(false, "TrainerAI: eligio Terremoto contra un volador (efectividad 0)");
                    return;
                }
            }
            Check(true, "TrainerAI: nunca elige movimientos sin efecto (500 muestras)");
        }

        private static void TestTrainerSkipsRedundantStatus()
        {
            var toxic = MakeMove("Toxico", PokenType.Poison, AttackType.Status, 0, 90, 10);
            toxic.effect = StatusEffect.Poisoned;
            toxic.effectChance = 100;
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);

            var enemyPoken = MakePoken(
                MakeSpecies("IA", PokenType.Poison, PokenType.None, 80, 60, 50, 50, 50, 60), 30, toxic, tackle);
            var playerPoken = MakePoken(
                MakeSpecies("Envenenado", PokenType.Normal, PokenType.None, 80, 50, 50, 50, 50, 70), 30, tackle);
            playerPoken.SetStatus(StatusEffect.Poisoned);

            var model = MakeBattle(playerPoken, enemyPoken);
            var profile = BattleAIFactory.CreateDefaultProfile(BattleAIType.Trainer);

            float score = MoveScorer.ScoreMove(MakeEnemyContext(model, profile), toxic);
            Check(score == 0f, "TrainerAI: Toxico contra un rival ya envenenado puntua 0");
        }

        private static void TestGymLeaderPrefersGuaranteedKo()
        {
            var weak = MakeMove("Ataque Rapido", PokenType.Normal, AttackType.Physical, 40, 100, 30);
            var strong = MakeMove("Cañonazo", PokenType.Normal, AttackType.Physical, 120, 70, 5);

            var enemyPoken = MakePoken(
                MakeSpecies("Lider", PokenType.Normal, PokenType.None, 80, 100, 50, 50, 50, 60), 40, weak, strong);
            var playerPoken = MakePoken(
                MakeSpecies("Casi KO", PokenType.Normal, PokenType.None, 80, 50, 50, 50, 50, 70), 30,
                MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35));
            playerPoken.SetCurrentHP(5); // cualquiera de los dos lo mata

            var model = MakeBattle(playerPoken, enemyPoken);
            var profile = BattleAIFactory.CreateDefaultProfile(BattleAIType.GymLeader);
            var ai = new GymLeaderAI();
            var rng = new SeededRng(11);

            int pickedSure = 0;
            for (int i = 0; i < 100; i++)
            {
                BattleAction action = ai.ChooseAction(MakeEnemyContext(model, profile), rng);
                if (action.Kind == BattleActionKind.UseMove && action.MoveIndex == 0) pickedSure++;
            }
            Check(pickedSure >= 95,
                $"GymLeaderAI: prefiere el KO seguro (100% acc) sobre el fuerte (70% acc): {pickedSure}/100");
        }

        private static void TestGymLeaderSwitchesOnBadMatchup()
        {
            var emberWeak = MakeMove("Ascuas", PokenType.Fire, AttackType.Special, 40, 100, 25);
            var razorLeaf = MakeMove("Hoja Afilada", PokenType.Grass, AttackType.Physical, 55, 95, 25);
            var waterGun = MakeMove("Pistola Agua", PokenType.Water, AttackType.Special, 40, 100, 25);

            // Activo de la IA: Fire debil contra un tanque Water (mal matchup)
            var enemyActive = MakePoken(
                MakeSpecies("Fueguito", PokenType.Fire, PokenType.None, 60, 30, 40, 30, 40, 50), 20, emberWeak);
            // Banco: Grass con ventaja clara
            var enemyBench = MakePoken(
                MakeSpecies("Plantita", PokenType.Grass, PokenType.None, 60, 60, 50, 60, 50, 55), 20, razorLeaf);
            var playerPoken = MakePoken(
                MakeSpecies("TanqueAgua", PokenType.Water, PokenType.None, 150, 60, 120, 60, 120, 40), 40, waterGun);

            var model = MakeBattle(playerPoken, enemyActive, enemyBench);
            var profile = BattleAIFactory.CreateDefaultProfile(BattleAIType.GymLeader);
            profile.randomness = 0f;
            var ai = new GymLeaderAI();

            BattleAction action = ai.ChooseAction(MakeEnemyContext(model, profile), new SeededRng(13));
            Check(action.Kind == BattleActionKind.SwitchPoken && action.SwitchIndex == 1,
                "GymLeaderAI: cambia al Grass ante un tanque Water con mal matchup");
        }

        private static void TestWildFavorsSuperEffective()
        {
            var ember = MakeMove("Ascuas", PokenType.Fire, AttackType.Special, 40, 100, 25);   // x2 vs Grass
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35); // x1

            var enemyPoken = MakePoken(
                MakeSpecies("Salvaje", PokenType.Fire, PokenType.None, 60, 50, 50, 50, 50, 50), 15, ember, tackle);
            var playerPoken = MakePoken(
                MakeSpecies("Planta", PokenType.Grass, PokenType.None, 60, 50, 50, 50, 50, 50), 15, tackle);

            var model = MakeBattle(playerPoken, enemyPoken);
            var profile = BattleAIFactory.CreateDefaultProfile(BattleAIType.Wild);
            var ai = new WildPokemonAI();
            var rng = new SeededRng(17);

            int pickedEmber = 0, pickedTackle = 0;
            for (int i = 0; i < 1000; i++)
            {
                BattleAction action = ai.ChooseAction(MakeEnemyContext(model, profile), rng);
                if (action.MoveIndex == 0) pickedEmber++;
                else pickedTackle++;
            }

            // Pesos 3 (1+2) vs 2 (1+1): proporcion esperada 1.5, sin ser determinista
            float ratio = (float)pickedEmber / pickedTackle;
            Check(pickedTackle > 100, $"WildAI: sigue usando el movimiento neutro a veces ({pickedTackle}/1000)");
            Check(ratio > 1.2f && ratio < 1.9f,
                $"WildAI: sesgo suave hacia lo super efectivo (proporcion {ratio:F2}, esperado ~1.5)");
        }
    }
}
