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

            // Estados de vida/muerte y fin de combate (bugs del boss)
            TestEsElUltimoExcluyeAlActivo();
            TestDebilitadoNoPuedeSerActivo();
            TestBossNoSalvaAlAnteultimo();
            TestCombateTerminaAlCaerElUltimo();
            TestMuertoNoRecibeTurno();
            TestJugadorCaidoEsperaEleccion();
            TestReemplazoRivalInvalidoNoRevive();

            // Subsistemas de la torre -> dificultad del boss
            TestSubsistemasSinCortarDanElBossMasDuro();
            TestCadaSubsistemaAflojaLoSuyo();
            TestFirewallEsParcialPeroMelmetalEsTodoONada();

            // La lectura de Nestor: que varie y que el aviso no mienta
            TestLecturaVariaConElEquipoDelPrimerNivel();
            TestLecturaNoAnulaLaUnicaSalida();
            TestLecturaDeLaMochilaNoEsperaAlCriticoDeVida();
            TestLecturaNoLeeLoQueElJugadorNoPuedeHacer();
            TestLecturaCadaTresTurnos();
            TestAvisoAnticipadoNoMiente();

            // La lectura ALIADA de Nestor (nivel 2): que sea rara, que salga de
            // la accion que la IA eligio, y que el bloqueo se note en el motor
            TestNestorNoLeeEnLosPrimerosTurnos();
            TestNestorRespetaCooldownYTope();
            TestNestorEsRaroConLosValoresPorDefecto();
            TestNestorLeeElRemateYLeSacaElTurno();
            TestNestorNoQuemaElTurnoPorUnGolpeFlojo();
            TestNestorLeeLaCuracionDelRival();
            TestNestorNoLeeElForcejeo();
            TestNestorAliadoDisponible();
            TestTamanioDeLosPokemonEnCombate();
            TestPcYShinySobrevivenAlGuardado();
            TestWaitDejaAlRivalSinActuar();
            TestElEnumDeAccionesNoSeCorrio();

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

        // ---------- vida, muerte y fin de combate ----------
        //
        // Estos tests cubren los bugs del combate contra Nestor: el bucle contra
        // un Poken ya derrotado, el boss inmortal y el turno que se seguia
        // pidiendo despues de que el rival se quedara sin equipo.

        // Combate con banco de los dos lados, que MakeBattle no arma.
        private static BattleModel MakeBattleConBancos(
            List<PokenModel> equipoJugador, List<PokenModel> equipoRival)
        {
            var player = new PlayerModel("Tester");
            foreach (PokenModel p in equipoJugador) player.Team.Add(p);

            var enemy = new EnemyModel(equipoRival[0].Definition, equipoRival[0].Level);
            enemy.Team.Clear();
            foreach (PokenModel p in equipoRival) enemy.Team.Add(p);

            var model = new BattleModel(player, enemy);
            model.SwitchPlayerPoken(0);
            model.SwitchEnemyPoken(0);
            return model;
        }

        private static PokenModel MakeDummy(string nombre, int hp, int speed, MoveDefinition move)
            => MakePoken(MakeSpecies(nombre, PokenType.Normal, PokenType.None,
                hp, 50, 50, 50, 50, speed), 20, move);

        private static void TestEsElUltimoExcluyeAlActivo()
        {
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
            var a = MakeDummy("A", 60, 50, tackle);
            var b = MakeDummy("B", 60, 50, tackle);
            var team = new List<PokenModel> { a, b };

            // Con el activo caido pero un compañero vivo, NO es el ultimo
            a.SetCurrentHP(0);
            Check(!BattleTeamRules.EsElUltimo(team, 0),
                "EsElUltimo: con el activo caido y un compañero vivo devuelve false");

            // Ahora si: el compañero tambien cayo
            b.SetCurrentHP(0);
            Check(BattleTeamRules.EsElUltimo(team, 0),
                "EsElUltimo: sin nadie mas en el banco devuelve true");
            Check(!BattleTeamRules.TieneAlgunoVivo(team),
                "TieneAlgunoVivo: equipo entero caido devuelve false");
        }

        private static void TestDebilitadoNoPuedeSerActivo()
        {
            var tackle = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);
            var lead = MakeDummy("Lider", 60, 60, tackle);
            var banco = MakeDummy("Banco", 60, 50, tackle);
            var rival = MakeDummy("Rival", 60, 10, tackle);

            var model = MakeBattleConBancos(
                new List<PokenModel> { lead, banco }, new List<PokenModel> { rival });

            banco.SetCurrentHP(0);
            bool cambio = model.SwitchPlayerPoken(1);
            Check(!cambio && model.ActivePlayerIndex == 0,
                "SwitchPlayerPoken: un Poken debilitado nunca pasa a ser el activo");
        }

        // El bug central: el ANTEULTIMO Poken del boss se salvaba de caer en
        // 1 PS y el combate quedaba en bucle contra un muerto.
        private static void TestBossNoSalvaAlAnteultimo()
        {
            var golpe = MakeMove("Golpe", PokenType.Normal, AttackType.Physical, 250, 100, 35);
            var atacante = MakeDummy("Atacante", 200, 200, golpe);
            var boss1 = MakeDummy("Boss1", 40, 5, golpe);
            var boss2 = MakeDummy("Boss2", 200, 5, golpe);

            var model = MakeBattleConBancos(
                new List<PokenModel> { atacante }, new List<PokenModel> { boss1, boss2 });

            var engine = new TurnEngine(model, new SeededRng(7));
            // Mismo delegado que arma BattleController: solo el ULTIMO no cae
            engine.EnemyCannotFaint =
                m => BattleTeamRules.EsElUltimo(m.Enemy.Team, m.ActiveEnemyIndex);

            // PS al minimo para que el test mida la REGLA y no la formula de daño
            boss1.SetCurrentHP(1);
            engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));

            Check(boss1.IsFainted,
                "Boss: el anteultimo Poken SI se debilita (no queda inmortal en 1 PS)");
            Check(model.ActiveEnemyIndex == 1 && !model.IsBattleOver,
                "Boss: al caer el anteultimo entra el siguiente y el combate sigue");

            // El ultimo si se salva: es lo que habilita la fase de liberacion
            boss2.SetCurrentHP(1);
            engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));
            Check(!boss2.IsFainted && boss2.CurrentHP == 1,
                "Boss: el ULTIMO Poken queda en 1 PS para la fase de liberacion");
            Check(!model.IsBattleOver,
                "Boss: con el ultimo en 1 PS el combate todavia no termino");
        }

        private static void TestCombateTerminaAlCaerElUltimo()
        {
            var golpe = MakeMove("Golpe", PokenType.Normal, AttackType.Physical, 250, 100, 35);
            var atacante = MakeDummy("Atacante", 200, 200, golpe);
            var rival = MakeDummy("Rival", 40, 5, golpe);

            var model = MakeBattleConBancos(
                new List<PokenModel> { atacante }, new List<PokenModel> { rival });
            var engine = new TurnEngine(model, new SeededRng(11));

            rival.SetCurrentHP(1);
            List<BattleEvent> events = engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));

            Check(model.IsBattleOver && model.IsPlayerVictory,
                "Fin: sin Pokens disponibles del rival el combate termina en victoria");
            Check(events.Exists(e => e.Kind == BattleEventKind.BattleEnded),
                "Fin: se emite el evento BattleEnded");

            // Y no se puede pedir otro turno contra el que ya cayo
            List<BattleEvent> despues = engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));
            Check(despues.Count == 0,
                "Fin: pedir otro turno con el combate terminado no produce nada");
        }

        private static void TestMuertoNoRecibeTurno()
        {
            var golpe = MakeMove("Golpe", PokenType.Normal, AttackType.Physical, 60, 100, 35);
            var jugador = MakeDummy("Jugador", 100, 60, golpe);
            var rivalA = MakeDummy("RivalA", 100, 10, golpe);
            var rivalB = MakeDummy("RivalB", 100, 10, golpe);

            var model = MakeBattleConBancos(
                new List<PokenModel> { jugador }, new List<PokenModel> { rivalA, rivalB });
            var engine = new TurnEngine(model, new SeededRng(3));

            // Se fuerza el estado invalido: el activo rival esta caido y aun asi
            // se pide un turno. El motor tiene que resolver el debilitamiento,
            // no dejarlo atacar.
            rivalA.SetCurrentHP(0);
            int hpAntes = jugador.CurrentHP;
            engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));

            Check(jugador.CurrentHP == hpAntes,
                "Muerto: un Poken debilitado no ataca aunque se le pida el turno");
            Check(model.ActiveEnemyIndex == 1,
                "Muerto: en vez de actuar, el rival caido es reemplazado");
        }

        private static void TestJugadorCaidoEsperaEleccion()
        {
            var golpe = MakeMove("Golpe", PokenType.Normal, AttackType.Physical, 250, 100, 35);
            var suave = MakeMove("Suave", PokenType.Normal, AttackType.Physical, 1, 100, 35);
            var lead = MakeDummy("Lider", 30, 5, suave);
            var banco = MakeDummy("Banco", 100, 5, suave);
            var rival = MakeDummy("Rival", 200, 200, golpe);

            var model = MakeBattleConBancos(
                new List<PokenModel> { lead, banco }, new List<PokenModel> { rival });
            var engine = new TurnEngine(model, new SeededRng(5));

            lead.SetCurrentHP(1);
            List<BattleEvent> events = engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));

            Check(lead.IsFainted && model.AwaitingPlayerReplacement,
                "Jugador caido: el motor queda esperando que ELIJA reemplazo");
            Check(model.ActivePlayerIndex == 0,
                "Jugador caido: no se auto-cambia (el activo sigue siendo el caido)");
            Check(events.Exists(e => e.Kind == BattleEventKind.PlayerMustSwitch),
                "Jugador caido: se emite PlayerMustSwitch para que la UI abra el selector");
            Check(!model.IsBattleOver,
                "Jugador caido: con banco disponible el combate no termina");

            // Elegir uno debilitado no hace nada; elegir uno vivo si
            Check(engine.CompletePlayerReplacement(0).Count == 0 && model.AwaitingPlayerReplacement,
                "Reemplazo: elegir al caido se rechaza y se sigue esperando");

            List<BattleEvent> entrada = engine.CompletePlayerReplacement(1);
            Check(!model.AwaitingPlayerReplacement && model.ActivePlayerIndex == 1,
                "Reemplazo: al elegir uno vivo entra al combate");
            Check(entrada.Exists(e => e.Kind == BattleEventKind.PokenSwitched),
                "Reemplazo: el evento PokenSwitched llega DESPUES de la eleccion");
        }

        // Una IA que devuelve basura no puede revivir a nadie ni trabar el combate
        private static void TestReemplazoRivalInvalidoNoRevive()
        {
            var golpe = MakeMove("Golpe", PokenType.Normal, AttackType.Physical, 250, 100, 35);
            var atacante = MakeDummy("Atacante", 200, 200, golpe);
            var rivalA = MakeDummy("RivalA", 40, 5, golpe);
            var rivalB = MakeDummy("RivalB", 200, 5, golpe);

            var model = MakeBattleConBancos(
                new List<PokenModel> { atacante }, new List<PokenModel> { rivalA, rivalB });
            var engine = new TurnEngine(model, new SeededRng(13));
            engine.EnemyReplacementSelector = _ => 99; // indice invalido a proposito

            rivalA.SetCurrentHP(1);
            engine.ResolveTurn(BattleAction.Move(0), BattleAction.Move(0));

            Check(model.ActiveEnemyIndex == 1 && !model.IsBattleOver,
                "Reemplazo rival: un indice invalido cae al primer vivo del banco");
            Check(rivalA.IsFainted,
                "Reemplazo rival: el caido sigue caido (nadie lo revive)");
        }

        // ---------- subsistemas de la torre -> combate de Nestor ----------
        //
        // Verifican que cortar cosas caminando la torre REALMENTE llegue a la
        // pelea. Es una cadena larga (StoryFlag -> TowerProgressModel ->
        // BossBattleConfig -> BattleController) y cualquier eslabon flojo se ve
        // igual desde afuera: un boss que no cambia.

        private static BossBattleConfig ConfigCon(params StoryFlag[] flags)
        {
            var story = new StoryModel();
            foreach (StoryFlag f in flags) story.ActivateFlag(f);
            return BossBattleConfig.DesdeTorre(new TowerProgressModel(story));
        }

        private static void TestSubsistemasSinCortarDanElBossMasDuro()
        {
            BossBattleConfig nada = ConfigCon();

            Check(nada.EscudoInicial ==
                  BossBattleConfig.EscudoPorNodoActivo * TowerProgressModel.FirewallNodosTotales,
                "Subsistemas: sin cortar nada, el escudo del nucleo esta entero");
            Check(!nada.PrediccionConAviso,
                "Subsistemas: sin cortar las camaras, la lectura de Nestor no se avisa antes");
            Check(!nada.PermiteCambio,
                "Subsistemas: sin cortar el rastreo, no se puede cambiar de Poken");
            Check(nada.TurnosLiberacion ==
                  BossBattleConfig.TurnosLiberacionBase * BossBattleConfig.MultiplicadorSinEnergia,
                "Subsistemas: sin cortar la energia, la liberacion dura el doble");
            Check(!nada.SinMelmetal,
                "Subsistemas: sin cortar todo, Nestor pelea con Melmetal");
        }

        private static void TestCadaSubsistemaAflojaLoSuyo()
        {
            BossBattleConfig camaras = ConfigCon(StoryFlag.Torre1_CamarasCortadas);
            Check(camaras.PrediccionConAviso,
                "Camaras cortadas: la lectura de Nestor se avisa un turno antes");
            // Las camaras cambian el TIMING del aviso, no cada cuanto lee: la
            // frecuencia es un numero de balance, no un efecto de subsistema.
            Check(camaras.TurnosEntrePredicciones == ConfigCon().TurnosEntrePredicciones,
                "Camaras cortadas: la frecuencia de lectura NO cambia");

            Check(ConfigCon(StoryFlag.Torre1_RastreoCortado).PermiteCambio,
                "Rastreo cortado: se habilita el cambio de Poken");

            Check(ConfigCon(StoryFlag.Torre1_EnergiaAuxiliarCortada).TurnosLiberacion
                  == BossBattleConfig.TurnosLiberacionBase,
                "Energia cortada: la fase de liberacion dura la mitad");

            // Y no se pisan entre si: cortar una no toca las otras
            BossBattleConfig soloCamaras = ConfigCon(StoryFlag.Torre1_CamarasCortadas);
            Check(!soloCamaras.PermiteCambio && soloCamaras.EscudoInicial ==
                  BossBattleConfig.EscudoPorNodoActivo * TowerProgressModel.FirewallNodosTotales,
                "Subsistemas: cortar uno no afloja los demas");
        }

        private static void TestFirewallEsParcialPeroMelmetalEsTodoONada()
        {
            // El firewall es el unico PARCIAL: cada nodo achica el escudo
            int escudo0 = ConfigCon().EscudoInicial;
            int escudo1 = ConfigCon(StoryFlag.Torre1_FirewallNodoA).EscudoInicial;
            int escudo2 = ConfigCon(StoryFlag.Torre1_FirewallNodoA,
                                    StoryFlag.Torre1_FirewallNodoB).EscudoInicial;
            int escudo3 = ConfigCon(StoryFlag.Torre1_FirewallNodoA,
                                    StoryFlag.Torre1_FirewallNodoB,
                                    StoryFlag.Torre1_FirewallNodoC).EscudoInicial;

            Check(escudo0 > escudo1 && escudo1 > escudo2 && escudo2 > escudo3 && escudo3 == 0,
                $"Firewall: cada nodo achica el escudo ({escudo0}/{escudo1}/{escudo2}/{escudo3})");

            // Melmetal es TODO O NADA: tres de cuatro subsistemas no alcanzan
            Check(!ConfigCon(StoryFlag.Torre1_CamarasCortadas,
                             StoryFlag.Torre1_RastreoCortado,
                             StoryFlag.Torre1_EnergiaAuxiliarCortada).SinMelmetal,
                "Melmetal: con el firewall a medias NO desaparece");

            Check(!ConfigCon(StoryFlag.Torre1_FirewallNodoA,
                             StoryFlag.Torre1_FirewallNodoB,
                             StoryFlag.Torre1_FirewallNodoC,
                             StoryFlag.Torre1_RastreoCortado,
                             StoryFlag.Torre1_EnergiaAuxiliarCortada).SinMelmetal,
                "Melmetal: faltando las camaras NO desaparece");

            Check(ConfigCon(StoryFlag.Torre1_CamarasCortadas,
                            StoryFlag.Torre1_FirewallNodoA,
                            StoryFlag.Torre1_FirewallNodoB,
                            StoryFlag.Torre1_FirewallNodoC,
                            StoryFlag.Torre1_RastreoCortado,
                            StoryFlag.Torre1_EnergiaAuxiliarCortada).SinMelmetal,
                "Melmetal: con los CUATRO subsistemas cortados, Nestor pelea sin el");
        }

        // ---------- la lectura de Nestor ----------
        //
        // El bug que cubren: en las condiciones en las que se llega al primer
        // boss (un Poken o dos, mochila vacia, rastreo activo asi que no se
        // puede cambiar) la heuristica se quedaba con UNA sola candidata y
        // Nestor anulaba siempre la misma categoria, lectura tras lectura.

        // Poken con un movimiento de dano y uno de estado, para que las dos
        // categorias de movimiento existan de verdad.
        private static PokenModel MakePokenDeLectura(string nombre)
        {
            var especie = MakeSpecies(nombre, PokenType.Normal, PokenType.None,
                80, 70, 70, 70, 70, 70);
            return MakePoken(especie, 30,
                MakeMove("Golpe", PokenType.Normal, AttackType.Physical, 60, 100, 20),
                MakeMove("Gruñido", PokenType.Normal, AttackType.Status, 0, 100, 20));
        }

        private static BattleModel MakeBattleDeLectura(int pokensDelJugador)
        {
            var player = new PlayerModel("Tester");
            for (int i = 0; i < pokensDelJugador; i++)
                player.Team.Add(MakePokenDeLectura($"Lector{i}"));

            PokenModel rival = MakePokenDeLectura("Rival");
            var enemy = new EnemyModel(rival.Definition, rival.Level);
            enemy.Team.Clear();
            enemy.Team.Add(rival);

            var model = new BattleModel(player, enemy);
            model.SwitchPlayerPoken(0);
            model.SwitchEnemyPoken(0);
            return model;
        }

        // Las salidas se cablean a mano (en el juego las cablea BattleController
        // mirando la mochila, el banco y los PP) para poder reproducir una
        // condicion exacta sin montar media pelea.
        private static PredictionState MakeLectura(bool conAviso,
            bool dano, bool estado, bool objetos, bool cambio)
        {
            var prediction = new PredictionState(conAviso, 3);
            prediction.TieneMovimientoDeDano = () => dano;
            prediction.TieneMovimientoDeEstado = () => estado;
            prediction.TieneObjetos = () => objetos;
            prediction.PuedeCambiar = () => cambio;
            return prediction;
        }

        // Corre N turnos y devuelve las categorias que se anularon de verdad.
        private static List<PredictedCategory> Lecturas(
            PredictionState prediction, BattleModel model, int turnos, int semilla = 7)
        {
            var rng = new SeededRng(semilla);
            var salieron = new List<PredictedCategory>();
            for (int t = 0; t < turnos; t++)
            {
                prediction.Avanzar(model, rng);
                if (prediction.Categoria != PredictedCategory.None)
                    salieron.Add(prediction.Categoria);
            }
            return salieron;
        }

        private static bool Variadas(List<PredictedCategory> lecturas)
            => lecturas.Count > 0 && lecturas.Exists(c => c != lecturas[0]);

        private static bool RepiteSeguidas(List<PredictedCategory> lecturas)
        {
            for (int i = 1; i < lecturas.Count; i++)
                if (lecturas[i] == lecturas[i - 1]) return true;
            return false;
        }

        private static void TestLecturaVariaConElEquipoDelPrimerNivel()
        {
            // Como se llega a Nestor la primera vez: un solo Poken, mochila
            // vacia y el rastreo activo (no se puede cambiar).
            BattleModel model = MakeBattleDeLectura(1);
            PredictionState prediction = MakeLectura(false,
                dano: true, estado: true, objetos: false, cambio: false);

            List<PredictedCategory> lecturas = Lecturas(prediction, model, 30);

            Check(lecturas.Count > 0,
                "Lectura de Nestor: con un solo Poken y sin objetos igual lee");
            Check(Variadas(lecturas),
                "Lectura de Nestor: con el equipo del primer nivel NO anula siempre lo mismo");
            Check(!RepiteSeguidas(lecturas),
                "Lectura de Nestor: no repite categoria dos lecturas seguidas");
        }

        private static void TestLecturaNoAnulaLaUnicaSalida()
        {
            // Un Poken, solo movimientos de dano, sin objetos y sin cambio:
            // anularle el ataque lo deja sin ninguna accion valida.
            BattleModel model = MakeBattleDeLectura(1);
            PredictionState prediction = MakeLectura(false,
                dano: true, estado: false, objetos: false, cambio: false);

            List<PredictedCategory> lecturas = Lecturas(prediction, model, 15);
            Check(lecturas.Count == 0,
                "Lectura de Nestor: no anula la unica salida que le queda al jugador");

            MoveDefinition golpe = model.ActivePlayerPoken.Moves[0].Definition;
            Check(!prediction.Bloquea(BattleActionKind.UseMove, golpe),
                "Lectura de Nestor: sin candidatas no bloquea nada");
        }

        private static void TestLecturaDeLaMochilaNoEsperaAlCriticoDeVida()
        {
            // Con el activo tocado y objetos en la mochila, la lectura de
            // Objeto tiene que entrar en la rotacion. Antes pedia menos del 35%
            // de PS: llegando al boss con el equipo entero no se cumplia nunca
            // y la categoria quedaba muerta media pelea.
            BattleModel model = MakeBattleDeLectura(1);
            model.ActivePlayerPoken.TakeDamage(model.ActivePlayerPoken.MaxHP / 4);

            PredictionState prediction = MakeLectura(false,
                dano: true, estado: false, objetos: true, cambio: false);

            List<PredictedCategory> lecturas = Lecturas(prediction, model, 30);
            Check(lecturas.Contains(PredictedCategory.Objeto),
                "Lectura de Nestor: con el activo tocado tambien lee la mochila");
            Check(Variadas(lecturas),
                "Lectura de Nestor: la mochila entra en la rotacion, no la monopoliza");
        }

        private static void TestLecturaNoLeeLoQueElJugadorNoPuedeHacer()
        {
            // Banco disponible pero rastreo activo: el cambio ya esta cerrado,
            // leerlo seria regalarle el turno.
            BattleModel model = MakeBattleDeLectura(3);
            PredictionState prediction = MakeLectura(false,
                dano: true, estado: true, objetos: false, cambio: false);

            List<PredictedCategory> lecturas = Lecturas(prediction, model, 30);
            Check(!lecturas.Contains(PredictedCategory.Cambio),
                "Lectura de Nestor: con el rastreo activo no anula el cambio");
            Check(!lecturas.Contains(PredictedCategory.Objeto),
                "Lectura de Nestor: con la mochila vacia no anula los objetos");

            // Con el rastreo cortado si aparece: es lo que se gana al cortarlo.
            PredictionState conCambio = MakeLectura(false,
                dano: true, estado: true, objetos: false, cambio: true);
            Check(Lecturas(conCambio, model, 30).Contains(PredictedCategory.Cambio),
                "Lectura de Nestor: con el rastreo cortado el cambio si se lee");
        }

        private static void TestLecturaCadaTresTurnos()
        {
            BattleModel model = MakeBattleDeLectura(1);
            PredictionState prediction = MakeLectura(false,
                dano: true, estado: true, objetos: false, cambio: false);
            var rng = new SeededRng(3);

            prediction.Avanzar(model, rng);
            bool turno1Libre = prediction.Categoria == PredictedCategory.None;
            prediction.Avanzar(model, rng);
            bool turno2Libre = prediction.Categoria == PredictedCategory.None;
            prediction.Avanzar(model, rng);
            bool turno3Anula = prediction.Categoria != PredictedCategory.None;

            Check(turno1Libre && turno2Libre && turno3Anula,
                "Lectura de Nestor: turnos 1 y 2 libres, el 3 condicionado");
        }

        private static void TestAvisoAnticipadoNoMiente()
        {
            // Camaras cortadas: lo que se avisa un turno antes tiene que ser
            // exactamente lo que despues se anula. Antes se sorteaba dos veces
            // (una para avisar y otra para bloquear) y el aviso podia mentir.
            BattleModel model = MakeBattleDeLectura(3);
            PredictionState prediction = MakeLectura(true,
                dano: true, estado: true, objetos: false, cambio: true);
            var rng = new SeededRng(11);

            int avisos = 0;
            bool cumplio = true;
            PredictedCategory anunciada = PredictedCategory.None;

            for (int t = 0; t < 30; t++)
            {
                prediction.Avanzar(model, rng);

                if (anunciada != PredictedCategory.None)
                {
                    if (prediction.Categoria != anunciada) cumplio = false;
                    avisos++;
                    anunciada = PredictedCategory.None;
                }

                if (prediction.Anticipada != PredictedCategory.None)
                    anunciada = prediction.Anticipada;
            }

            Check(avisos > 0, "Aviso anticipado: con las camaras cortadas se avisa");
            Check(cumplio, "Aviso anticipado: Nestor anula exactamente lo que aviso");
        }

        // ---------- la lectura ALIADA de Nestor (nivel 2) ----------

        // Nestor con todos los frenos abiertos: interviene siempre que pueda.
        // Cada test cierra a mano el freno que quiere probar.
        private static NestorPredictionSystem MakeNestorSinFrenos()
        {
            return new NestorPredictionSystem
            {
                Probabilidad = 1f,
                CooldownTurnos = 0,
                TurnosDeGracia = 0,
                MaximoPorCombate = 99
            };
        }

        // Rival con dos golpes: 'fuerte' se lleva al jugador puesto, 'flojo' no.
        private static BattleModel MakeBattleParaNestor(int hpActualDelJugador)
        {
            var fuerte = MakeMove("Cañonazo", PokenType.Normal, AttackType.Physical, 120, 100, 5);
            var flojo = MakeMove("Ataque Rapido", PokenType.Normal, AttackType.Physical, 40, 100, 30);

            var rival = MakePoken(
                MakeSpecies("Rival", PokenType.Normal, PokenType.None, 90, 110, 50, 60, 50, 70),
                40, fuerte, flojo);
            var jugador = MakePoken(
                MakeSpecies("Aliado", PokenType.Normal, PokenType.None, 140, 60, 60, 60, 60, 50),
                35, MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35));
            jugador.SetCurrentHP(hpActualDelJugador);

            return MakeBattle(jugador, rival);
        }

        private static AiContext ContextoDeNestor(BattleModel model)
            => MakeEnemyContext(model, BattleAIFactory.CreateDefaultProfile(BattleAIType.Trainer));

        // Cuando hay Nestor de ayudante. Los NPCs del casino lo piden en todas
        // sus peleas, asi que tiene que aparecer aunque la partida no traiga el
        // flag de la torre; pero nunca en la pelea contra el propio Nestor.
        private static void TestNestorAliadoDisponible()
        {
            Check(!BattleController.LecturaAliadaDisponible(NestorAllyMode.Auto, false, false, false),
                "Nestor aliado: sin el flag y sin pedirlo no aparece (primer nivel)");
            Check(BattleController.LecturaAliadaDisponible(NestorAllyMode.Auto, false, false, true),
                "Nestor aliado: con la torre 1 liberada aparece");
            Check(BattleController.LecturaAliadaDisponible(NestorAllyMode.Auto, false, true, false),
                "Nestor aliado: una pelea del casino lo tiene aunque falte el flag");
            Check(!BattleController.LecturaAliadaDisponible(NestorAllyMode.Auto, true, true, true),
                "Nestor aliado: nunca en la pelea contra el propio Nestor, aunque se pida");
            Check(!BattleController.LecturaAliadaDisponible(NestorAllyMode.SiempreOn, true, false, true),
                "Nestor aliado: SiempreOn tampoco saltea el candado del boss");
            Check(!BattleController.LecturaAliadaDisponible(NestorAllyMode.SiempreOff, false, true, true),
                "Nestor aliado: SiempreOff lo apaga incluso en el casino");
        }

        // El tamaño en combate sale de los pixeles de la tira. Lo que se pidio
        // arreglar: Aron (32 px) se veia del alto de un Haunter (85 px).
        private static void TestTamanioDeLosPokemonEnCombate()
        {
            Check(Mathf.Approximately(PokeApi.SpriteAssigner.AltoEnCombate(80), 1f),
                "Tamaño en combate: un frame de 80 px mide 1 unidad, como antes");
            Check(Mathf.Approximately(PokeApi.SpriteAssigner.AltoEnCombate(32), 0.5f),
                "Tamaño en combate: los muy chicos no bajan de 0,5");
            Check(Mathf.Approximately(PokeApi.SpriteAssigner.AltoEnCombate(182), 1.5f),
                "Tamaño en combate: los gigantes no pasan de 1,5");

            float aron = AltoDelPrimerFrame(Resources.Load<PokenDefinition>("Definitions/Pokens/0304_Aron"));
            float haunter = AltoDelPrimerFrame(Resources.Load<PokenDefinition>("Definitions/Pokens/0093_Haunter"));
            Check(aron > 0f && haunter > 0f && aron < haunter * 0.6f,
                $"Tamaño en combate: Aron se ve mas chico que Haunter ({aron:0.00} contra {haunter:0.00} unidades)");
        }

        private static float AltoDelPrimerFrame(PokenDefinition def)
        {
            if (def == null) return 0f;
            Sprite s = def.battleFrontFrames != null && def.battleFrontFrames.Length > 0
                ? def.battleFrontFrames[0]
                : def.battleFrontSprite;
            return s != null ? s.rect.height / s.pixelsPerUnit : 0f;
        }

        // El PC y el shiny se guardan en la misma tabla que el equipo: todo tiene
        // que volver a su lugar, sin mezclar el equipo con las cajas.
        private static void TestPcYShinySobrevivenAlGuardado()
        {
            const int slot = 777;
            DefinitionRepository.Instance.LoadAll();
            PokenDefinition pikachu = AssetDatabase.LoadAssetAtPath<PokenDefinition>("Assets/Resources/Definitions/Pokens/0025_Pikachu.asset");
            PokenDefinition cosmog = AssetDatabase.LoadAssetAtPath<PokenDefinition>("Assets/Resources/Definitions/Pokens/0789_Cosmog.asset");
            if (pikachu == null || cosmog == null)
            {
                Debug.LogWarning("[SmokeTests] Faltan Pikachu o Cosmog: se saltea la prueba del guardado del PC.");
                return;
            }

            var jugador = new PlayerModel("PruebaPC");
            jugador.Team.Add(new PokenModel(pikachu, 12));
            var shiny = new PokenModel(cosmog, 15);
            shiny.SetShiny(true);
            jugador.Team.Add(shiny);
            jugador.Storage.Set(2, 7, new PokenModel(pikachu, 30));

            try
            {
                SaveService.Instance.SavePlayer(jugador, new StoryModel(), slot);
                PlayerModel cargado = SaveService.Instance.LoadPlayer(new StoryModel(), slot);

                Check(cargado != null && cargado.Team.Count == 2,
                    "Guardado del PC: el equipo vuelve entero y sin mezclarse con las cajas");
                Check(cargado != null && cargado.Team.Count == 2 && cargado.Team[1].IsShiny && !cargado.Team[0].IsShiny,
                    "Guardado del PC: el shiny sigue siendo shiny");
                PokenModel enCaja = cargado?.Storage.Get(2, 7);
                Check(enCaja != null && enCaja.Level == 30 && cargado.Storage.Count == 1,
                    "Guardado del PC: el Pokemon de la caja vuelve a su caja y a su lugar");
            }
            finally
            {
                SaveService.Instance.DeleteSave(slot);
            }
        }

        // El freno mas visible: los primeros turnos Nestor solo mira. Sin esto
        // la lectura puede caer en el turno 1 y arruina la entrada del combate.
        private static void TestNestorNoLeeEnLosPrimerosTurnos()
        {
            BattleModel model = MakeBattleParaNestor(12); // el rival lo remata
            AiContext ctx = ContextoDeNestor(model);
            var rng = new SeededRng(3);

            NestorPredictionSystem nestor = MakeNestorSinFrenos();
            nestor.TurnosDeGracia = 2;

            bool leyoEnLaGracia = false;
            for (int t = 0; t < 2; t++)
                if (nestor.Evaluar(ctx, BattleAction.Move(0), rng).Intervino) leyoEnLaGracia = true;

            bool leyoDespues = nestor.Evaluar(ctx, BattleAction.Move(0), rng).Intervino;

            Check(!leyoEnLaGracia, "Nestor aliado: no lee durante los turnos de gracia");
            Check(leyoDespues, "Nestor aliado: pasada la gracia si lee");
        }

        // Cooldown y tope por combate: los dos frenos que evitan que Nestor
        // anule al rival todos los turnos.
        private static void TestNestorRespetaCooldownYTope()
        {
            BattleModel model = MakeBattleParaNestor(12);
            AiContext ctx = ContextoDeNestor(model);
            var rng = new SeededRng(9);

            NestorPredictionSystem nestor = MakeNestorSinFrenos();
            nestor.CooldownTurnos = 4;
            nestor.MaximoPorCombate = 2;

            var turnosQueLeyo = new List<int>();
            for (int t = 1; t <= 30; t++)
                if (nestor.Evaluar(ctx, BattleAction.Move(0), rng).Intervino)
                    turnosQueLeyo.Add(t);

            Check(turnosQueLeyo.Count == 2,
                $"Nestor aliado: nunca pasa del tope por combate (leyo {turnosQueLeyo.Count} veces en 30 turnos)");

            bool separadas = turnosQueLeyo.Count < 2
                || turnosQueLeyo[1] - turnosQueLeyo[0] > nestor.CooldownTurnos;
            Check(separadas, "Nestor aliado: dos lecturas nunca caen dentro del cooldown");
        }

        // Con la probabilidad conservadora que trae de fabrica, la lectura tiene
        // que ser RARA. Este test es el que se rompe si alguien sube el numero
        // sin querer.
        private static void TestNestorEsRaroConLosValoresPorDefecto()
        {
            BattleModel model = MakeBattleParaNestor(12);
            AiContext ctx = ContextoDeNestor(model);

            int lecturas = 0;
            int turnos = 0;
            for (int pelea = 0; pelea < 200; pelea++)
            {
                var nestor = new NestorPredictionSystem();   // valores de fabrica
                var rng = new SeededRng(pelea);
                for (int t = 0; t < 10; t++)                 // pelea tipica
                {
                    turnos++;
                    if (nestor.Evaluar(ctx, BattleAction.Move(0), rng).Intervino) lecturas++;
                }
            }

            float porTurno = (float)lecturas / turnos;
            Check(porTurno < 0.15f,
                $"Nestor aliado: interviene en menos del 15% de los turnos ({porTurno:P1})");
            Check(lecturas > 0, "Nestor aliado: pero interviene alguna vez");
        }

        // Lo que se pidio: la lectura tiene que salir de la accion que la IA
        // REALMENTE eligio. Un remate le cuesta el turno entero al rival.
        private static void TestNestorLeeElRemateYLeSacaElTurno()
        {
            BattleModel model = MakeBattleParaNestor(12); // muere con cualquiera
            AiContext ctx = ContextoDeNestor(model);

            NestorPredictionSystem nestor = MakeNestorSinFrenos();
            NestorIntervention lectura =
                nestor.Evaluar(ctx, BattleAction.Move(0), new SeededRng(4));

            Check(lectura.Plan.Kind == EnemyPlanKind.Remate,
                "Nestor aliado: reconoce el remate del rival");
            Check(lectura.Intervino && lectura.PerdioElTurno,
                "Nestor aliado: al remate le saca el turno entero");
            Check(lectura.AccionFinal.Kind == BattleActionKind.Wait,
                "Nestor aliado: la accion del rival queda en Wait (no hace nada)");
        }

        // El espejo del anterior: un golpe flojo NO le cuesta el turno. Solo
        // pierde su mejor opcion y juega la segunda, que sigue saliendo del
        // MoveScorer de la IA y no de un sorteo.
        private static void TestNestorNoQuemaElTurnoPorUnGolpeFlojo()
        {
            BattleModel model = MakeBattleParaNestor(140); // full: nada lo remata
            AiContext ctx = ContextoDeNestor(model);

            NestorPredictionSystem nestor = MakeNestorSinFrenos();
            NestorIntervention lectura =
                nestor.Evaluar(ctx, BattleAction.Move(1), new SeededRng(6)); // el flojo

            Check(lectura.Plan.Kind == EnemyPlanKind.Ataque && !lectura.Plan.EsJugadaFuerte,
                "Nestor aliado: un golpe flojo no cuenta como jugada fuerte");
            Check(lectura.Intervino && !lectura.PerdioElTurno,
                "Nestor aliado: por un golpe flojo el rival no pierde el turno");
            Check(lectura.AccionFinal.Kind == BattleActionKind.UseMove
                  && lectura.AccionFinal.MoveIndex != 1,
                "Nestor aliado: el rival cae a otro movimiento, no a Wait");
        }

        // Un rival que se cura es de las jugadas que mas duele no cortar.
        private static void TestNestorLeeLaCuracionDelRival()
        {
            var cura = MakeMove("Recuperacion", PokenType.Normal, AttackType.Status, 0, 100, 10);
            cura.healPercent = 50;
            var golpe = MakeMove("Placaje", PokenType.Normal, AttackType.Physical, 40, 100, 35);

            var rival = MakePoken(
                MakeSpecies("Curandero", PokenType.Normal, PokenType.None, 120, 60, 60, 60, 60, 60),
                35, cura, golpe);
            rival.SetCurrentHP(30); // le falta vida: la curacion sirve

            var jugador = MakePoken(
                MakeSpecies("Aliado", PokenType.Normal, PokenType.None, 120, 60, 60, 60, 60, 55),
                35, golpe);

            BattleModel model = MakeBattle(jugador, rival);
            AiContext ctx = ContextoDeNestor(model);

            NestorIntervention lectura =
                MakeNestorSinFrenos().Evaluar(ctx, BattleAction.Move(0), new SeededRng(8));

            Check(lectura.Plan.Kind == EnemyPlanKind.Curacion,
                "Nestor aliado: reconoce que el rival se iba a curar");
            Check(lectura.Intervino && lectura.PerdioElTurno,
                "Nestor aliado: la curacion le cuesta el turno al rival");
        }

        // Un rival sin PP forcejeando ya esta en su peor momento: leerselo no
        // demuestra nada y ademas seria gratis todos los turnos.
        private static void TestNestorNoLeeElForcejeo()
        {
            BattleModel model = MakeBattleParaNestor(12);
            AiContext ctx = ContextoDeNestor(model);
            var rng = new SeededRng(2);

            NestorPredictionSystem nestor = MakeNestorSinFrenos();

            bool leyoAlguna = false;
            for (int t = 0; t < 20; t++)
                if (nestor.Evaluar(ctx, BattleAction.Struggle(), rng).Intervino) leyoAlguna = true;

            Check(!leyoAlguna, "Nestor aliado: nunca lee un forcejeo");
        }

        // El bloqueo tiene que traducirse en que el rival NO actua. Se prueba
        // contra el motor de verdad, no contra la intencion.
        private static void TestWaitDejaAlRivalSinActuar()
        {
            BattleModel model = MakeBattleParaNestor(140);
            var engine = new TurnEngine(model, new SeededRng(5));

            int hpAntes = model.ActivePlayerPoken.CurrentHP;
            engine.ResolveTurn(BattleAction.Wait(), BattleAction.Wait());
            int hpConWait = model.ActivePlayerPoken.CurrentHP;

            engine.ResolveTurn(BattleAction.Wait(), BattleAction.Move(0));
            int hpConAtaque = model.ActivePlayerPoken.CurrentHP;

            Check(hpConWait == hpAntes,
                "Wait: el rival con Wait no le hace nada al jugador");
            Check(hpConAtaque < hpConWait,
                "Wait: el mismo rival sin Wait si pega (el test no es un falso positivo)");
        }

        // Los valores del enum se serializan por numero. Si alguien mete algo
        // en el medio, todo lo guardado con Flee o Protect cambia de accion.
        private static void TestElEnumDeAccionesNoSeCorrio()
        {
            Check((int)BattleActionKind.UseMove == 0
                  && (int)BattleActionKind.SwitchPoken == 1
                  && (int)BattleActionKind.UseItem == 2
                  && (int)BattleActionKind.Flee == 3
                  && (int)BattleActionKind.Protect == 4
                  && (int)BattleActionKind.Wait == 5,
                "BattleActionKind: Wait quedo ULTIMO y no corrio a los demas");
        }
    }
}
