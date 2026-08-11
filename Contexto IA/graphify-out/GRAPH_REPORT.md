# Graph Report - GrupoX_PokeRUSH_ESTFA_2026  (2026-08-11)

## Corpus Check
- 1953 files · ~22,554,653 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1675 nodes · 3033 edges · 132 communities (78 shown, 54 thin omitted)
- Extraction: 96% EXTRACTED · 4% INFERRED · 0% AMBIGUOUS · INFERRED: 135 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `dec4f12b`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- BattlePokenState
- AiContext
- PokenModel
- PauseMenuView
- PokeRushSmokeTests
- BattleTestSceneBuilder
- PokeApiDtos.cs
- Historias de Usuario — PokeRush
- PokeApiDownloader
- NPCDefinition
- com.unity.modules.jsonserialize
- com.unity.render-pipelines.core
- .GenerateAll
- com.unity.mathematics
- com.unity.modules.physics
- com.unity.test-framework
- SaveSlotMenuView
- PokeApiMapper
- MonoBehaviour
- com.unity.modules.ui
- MoveDefinition
- DialogueController
- com.unity.2d.tilemap
- com.unity.modules.imgui
- AIProfileDefinition
- PlayerView
- 👾 Cómo crear un enemigo en PokeRush
- BattleController
- BattleTester
- GameManager
- dependencies
- PokeRush.EditorTools.PokeApi
- dependencies
- com.unity.modules.physics2d
- com.unity.ext.nunit
- PlayerController
- PlayerInputController
- com.unity.modules.animation
- com.unity.2d.common
- com.unity.2d.sprite
- com.unity.modules.particlesystem
- com.unity.modules.subsystems
- com.unity.modules.imageconversion
- com.unity.modules.unitywebrequest
- build_project_charter_docx.py
- PokeApiImporterWindow
- EnemyDefinition
- StartScreenView
- com.unity.modules.uielements
- PokenView
- BattleModel
- BattleItemDefinition
- ScriptableObject
- InventoryModel
- NPCModel
- AudioService
- DefinitionRepository
- SceneLoaderService
- Arquitectura del Proyecto — PokeRush
- AIProfileGenerator
- .TriggerEvent
- HealItemDefinition
- CameraFollow
- com.unity.searcher
- .ActivateFlag
- CosmeticDefinition
- SpecialItemDefinition
- PlayerModel
- BattleHudView
- com.gilzoide.sqlite-net
- dependencies
- com.unity.collections
- com.unity.ide.visualstudio
- com.unity.inputsystem
- com.unity.modules.assetbundle
- com.unity.modules.audio
- com.unity.modules.director
- com.unity.modules.unitywebrequestassetbundle
- manifest.json
- DatabaseService
- com.unity.modules.accessibility
- com.unity.modules.androidjni
- com.unity.modules.umbra
- com.unity.modules.wind
- README.md
- CLAUDE.md
- com.gilzoide.sqlite-net
- com.unity.2d.animation
- com.unity.2d.aseprite
- com.unity.2d.psdimporter
- com.unity.2d.sprite
- com.unity.2d.spriteshape
- com.unity.2d.tilemap
- com.unity.2d.tilemap.extras
- com.unity.2d.tooling
- com.unity.ide.visualstudio
- com.unity.inputsystem
- com.unity.modules.accessibility
- com.unity.modules.adaptiveperformance
- com.unity.modules.ai
- com.unity.modules.animation
- com.unity.modules.assetbundle
- com.unity.modules.audio
- com.unity.modules.director
- com.unity.modules.imageconversion
- com.unity.modules.imgui
- com.unity.modules.jsonserialize
- com.unity.modules.particlesystem
- com.unity.modules.physics
- com.unity.modules.screencapture
- com.unity.modules.terrain
- com.unity.modules.terrainphysics
- com.unity.modules.ui
- com.unity.modules.uielements
- com.unity.modules.umbra
- com.unity.modules.unityanalytics
- com.unity.modules.unitywebrequest
- com.unity.modules.unitywebrequestassetbundle
- com.unity.modules.unitywebrequestaudio
- com.unity.modules.unitywebrequesttexture
- com.unity.modules.unitywebrequestwww
- com.unity.modules.vectorgraphics
- com.unity.modules.vehicles
- com.unity.modules.video
- com.unity.modules.vr
- com.unity.modules.wind
- com.unity.modules.xr
- com.unity.multiplayer.center
- com.unity.nuget.newtonsoft-json
- com.unity.render-pipelines.universal
- com.unity.timeline
- com.unity.ugui

## God Nodes (most connected - your core abstractions)
1. `BattleModel` - 43 edges
2. `PokenModel` - 42 edges
3. `MoveDefinition` - 41 edges
4. `BattlePokenState` - 40 edges
5. `BattleController` - 36 edges
6. `BattleEvent` - 36 edges
7. `PokeRushSmokeTests` - 34 edges
8. `BattleFxView` - 33 edges
9. `BattleHudView` - 29 edges
10. `PokenDefinition` - 28 edges

## Surprising Connections (you probably didn't know these)
- `BattleController` --references--> `PlayerInputController`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/BattleController.cs → Codigo/PokeRush/Assets/Scripts/Controllers/PlayerInputController.cs
- `BattleController` --references--> `IBattleAI`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/BattleController.cs → Codigo/PokeRush/Assets/Scripts/Core/AI/IBattleAI.cs
- `BattleController` --references--> `TurnEngine`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/BattleController.cs → Codigo/PokeRush/Assets/Scripts/Core/Battle/TurnEngine.cs
- `BattleController` --references--> `IBattleRng`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/BattleController.cs → Codigo/PokeRush/Assets/Scripts/Core/IBattleRng.cs
- `BattleController` --references--> `AIProfileDefinition`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/BattleController.cs → Codigo/PokeRush/Assets/Scripts/Data/AIProfileDefinition.cs

## Import Cycles
- None detected.

## Communities (132 total, 54 thin omitted)

### Community 0 - "BattlePokenState"
Cohesion: 0.24
Nodes (5): float, Func, int, List, TurnEngine

### Community 1 - "AiContext"
Cohesion: 0.06
Nodes (26): MenuItem, string, AIProfileGenerator, AiContext, int, List, BattleAIFactory, FinalBossAI (+18 more)

### Community 2 - "PokenModel"
Cohesion: 0.05
Nodes (20): HashSet, int, List, RewardCalculator, int, ExperienceTableDefinition, MoveModel, List (+12 more)

### Community 3 - "PauseMenuView"
Cohesion: 0.16
Nodes (3): Button, GameObject, PauseMenuView

### Community 4 - "PokeRushSmokeTests"
Cohesion: 0.16
Nodes (6): int, MenuItem, PokeRushSmokeTests, int, Random, IvSet

### Community 5 - "BattleTestSceneBuilder"
Cohesion: 0.07
Nodes (27): Button, GameObject, Image, List, MenuItem, Sprite, SpriteRenderer, string (+19 more)

### Community 6 - "PokeApiDtos.cs"
Cohesion: 0.20
Nodes (22): bool, int, List, string, AbilityDto, AbilityFlavorText, ChainLink, EvolutionChainDto (+14 more)

### Community 7 - "Historias de Usuario — PokeRush"
Cohesion: 0.08
Nodes (24): 1. Exploración del Mundo, 2. Sistema de Combate, 3. Equipo y Progresión, 4. Inventario y Economía, 5. NPCs, Historia y Servicios, 6. Personalización, 7. Partida y Configuración, 8. Desarrollo (+16 more)

### Community 8 - "PokeApiDownloader"
Cohesion: 0.15
Nodes (11): Action, Func, IEnumerable, string, PokeApiDownloader, Dictionary, HashSet, SpeciesSelection (+3 more)

### Community 9 - "NPCDefinition"
Cohesion: 0.10
Nodes (19): StoryFlag, BossDefinition, AudioClip, bool, int, Sprite, Sprite, string (+11 more)

### Community 10 - "com.unity.modules.jsonserialize"
Cohesion: 0.06
Nodes (33): dependencies, depth, source, version, dependencies, depth, source, version (+25 more)

### Community 11 - "com.unity.render-pipelines.core"
Cohesion: 0.07
Nodes (29): depth, source, version, dependencies, depth, source, version, dependencies (+21 more)

### Community 12 - ".GenerateAll"
Cohesion: 0.13
Nodes (11): Action, Dictionary, Func, string, PokeApiAssetGenerator, List, AbilityId, GenerateReport (+3 more)

### Community 13 - "com.unity.mathematics"
Cohesion: 0.08
Nodes (27): dependencies, dependencies, depth, source, url, version, depth, source (+19 more)

### Community 14 - "com.unity.modules.physics"
Cohesion: 0.10
Nodes (21): dependencies, depth, source, version, dependencies, depth, source, version (+13 more)

### Community 15 - "com.unity.test-framework"
Cohesion: 0.11
Nodes (19): dependencies, dependencies, depth, source, url, version, depth, dependencies (+11 more)

### Community 16 - "SaveSlotMenuView"
Cohesion: 0.12
Nodes (8): bool, Button, float, GameObject, Image, string, Text, SaveSlotMenuView

### Community 17 - "PokeApiMapper"
Cohesion: 0.20
Nodes (6): MoveDto, Dictionary, HashSet, string, PokeApiMapper, MoveImplementation

### Community 18 - "MonoBehaviour"
Cohesion: 0.11
Nodes (10): EventController, MapController, bool, int, string, EventDefinition, List, PlayerModel (+2 more)

### Community 19 - "com.unity.modules.ui"
Cohesion: 0.07
Nodes (30): dependencies, depth, source, version, dependencies, depth, source, version (+22 more)

### Community 20 - "MoveDefinition"
Cohesion: 0.16
Nodes (8): bool, List, MenuItem, Sprite, string, Slot, SpriteAssigner, Slot

### Community 21 - "DialogueController"
Cohesion: 0.06
Nodes (33): 1️⃣ `WildPokemonAI` — Pokémon salvaje, 2️⃣ `TrainerAI` — Entrenador común, 3️⃣ `GymLeaderAI` — Líder de gimnasio / Elite, 4️⃣ `FinalBossAI` — Bosses finales, 🎲 Azar reproducible (`IBattleRng`), Cambio manual de Pokémon, Cambios de estadística (stages -6 a +6), 🌦️ Climas y efectos de campo (`FieldEffects.cs`) (+25 more)

### Community 22 - "com.unity.2d.tilemap"
Cohesion: 0.12
Nodes (17): dependencies, depth, dependencies, depth, source, url, version, source (+9 more)

### Community 23 - "com.unity.modules.imgui"
Cohesion: 0.15
Nodes (8): BattlePokenState, bool, int, List, MoveEffectExecutor, int, StatStageTable, Stat

### Community 24 - "AIProfileDefinition"
Cohesion: 0.07
Nodes (20): MenuItem, Sprite, string, Text, Vector3, AnimationSceneBuilder, MenuItem, string (+12 more)

### Community 25 - "PlayerView"
Cohesion: 0.19
Nodes (8): float, int, Sprite, SpriteRenderer, Vector2, DirectionalAnimation, PlayerView, DirectionalAnimation

### Community 26 - "👾 Cómo crear un enemigo en PokeRush"
Cohesion: 0.13
Nodes (14): 1. Tener listas las especies y sus movimientos, 2. Crear el asset del enemigo, 3. Llenar los campos en el Inspector, 4. Configurar el equipo (`pokenTeam`), 5. Disparar la batalla, ✅ Checklist rápido, 🧠 Conceptos previos (leer esto primero), 👾 Cómo crear un enemigo en PokeRush (+6 more)

### Community 27 - "BattleController"
Cohesion: 0.14
Nodes (8): BattleController, bool, float, IEnumerator, List, Sprite, SpriteRenderer, BattlePhase

### Community 28 - "BattleTester"
Cohesion: 0.21
Nodes (6): BattleTester, bool, int, int, Sprite, WildEncounterDefinition

### Community 29 - "GameManager"
Cohesion: 0.22
Nodes (7): AttackType, bool, float, int, DamageCalculator, DamageResult, PokenType

### Community 30 - "dependencies"
Cohesion: 0.14
Nodes (13): dependencies, com.unity.ai.assistant, com.unity.ai.inference, com.unity.modules.ai, com.unity.modules.androidjni, com.unity.modules.vectorgraphics, com.unity.modules.wind, com.unity.modules.androidjni (+5 more)

### Community 31 - "PokeRush.EditorTools.PokeApi"
Cohesion: 0.14
Nodes (8): bool, int, List, GenerateReport, bool, int, DownloadReport, PokeRush.EditorTools.PokeApi

### Community 32 - "dependencies"
Cohesion: 0.09
Nodes (21): dependencies, depth, source, version, dependencies, depth, source, version (+13 more)

### Community 33 - "com.unity.modules.physics2d"
Cohesion: 0.15
Nodes (13): dependencies, depth, source, url, version, dependencies, depth, source (+5 more)

### Community 34 - "com.unity.ext.nunit"
Cohesion: 0.15
Nodes (13): dependencies, depth, source, version, dependencies, depth, source, url (+5 more)

### Community 35 - "PlayerController"
Cohesion: 0.20
Nodes (6): bool, float, IEnumerator, Vector2, PlayerController, LayerMask

### Community 36 - "PlayerInputController"
Cohesion: 0.18
Nodes (3): bool, PlayerInputController, InputAction

### Community 37 - "com.unity.modules.animation"
Cohesion: 0.07
Nodes (30): dependencies, depth, source, url, version, dependencies, depth, source (+22 more)

### Community 38 - "com.unity.2d.common"
Cohesion: 0.08
Nodes (23): ⚔️ Animaciones de ataques (`BattleFxView.PlayAttack`), 📉 Barra de HP progresiva (`BattleHudView.AnimateHp`), 📄 `BattleHudView.cs` — la vista del combate, 🏗️ `BattleTestSceneBuilder.cs` — armar la escena con un click, ✂️ `BattleUiAssetImporter.cs` — copiar y recortar los gráficos, 🧪 Cómo probarlo, Decisiones que conviene conocer, Detalles visuales (+15 more)

### Community 39 - "com.unity.2d.sprite"
Cohesion: 0.18
Nodes (10): BattleFxView, Color, Dictionary, float, IEnumerator, Image, Sprite, SpriteRenderer (+2 more)

### Community 40 - "com.unity.modules.particlesystem"
Cohesion: 0.09
Nodes (24): dependencies, depth, source, version, dependencies, depth, source, version (+16 more)

### Community 41 - "com.unity.modules.subsystems"
Cohesion: 0.15
Nodes (13): dependencies, depth, source, url, version, dependencies, depth, source (+5 more)

### Community 42 - "com.unity.modules.imageconversion"
Cohesion: 0.12
Nodes (17): dependencies, depth, source, version, dependencies, depth, source, version (+9 more)

### Community 43 - "com.unity.modules.unitywebrequest"
Cohesion: 0.06
Nodes (40): dependencies, depth, source, version, dependencies, depth, source, version (+32 more)

### Community 44 - "build_project_charter_docx.py"
Cohesion: 0.45
Nodes (9): clear_cell(), main(), set_font(), set_row_text(), set_value_row(), shade_cell(), style_header_rows(), unique_cells() (+1 more)

### Community 45 - "PokeApiImporterWindow"
Cohesion: 0.24
Nodes (6): bool, MenuItem, string, Vector2, PokeApiImporterWindow, EditorWindow

### Community 46 - "EnemyDefinition"
Cohesion: 0.16
Nodes (7): Action, int, DialogueController, Image, Sprite, Text, DialogueView

### Community 47 - "StartScreenView"
Cohesion: 0.22
Nodes (5): bool, Button, int, string, StartScreenView

### Community 48 - "com.unity.modules.uielements"
Cohesion: 0.09
Nodes (22): dependencies, depth, source, url, version, dependencies, depth, source (+14 more)

### Community 49 - "PokenView"
Cohesion: 0.14
Nodes (6): Animator, float, int, Sprite, SpriteRenderer, PokenView

### Community 50 - "BattleModel"
Cohesion: 0.12
Nodes (14): BattleEvent, BattleEventKind, bool, string, int, List, FieldEffects, FieldTerrain (+6 more)

### Community 51 - "BattleItemDefinition"
Cohesion: 0.12
Nodes (8): BattleItemDefinition, int, Sprite, string, VendorDefinition, NPCModel, NurseModel, VendorModel

### Community 52 - "ScriptableObject"
Cohesion: 0.22
Nodes (5): AbilityEffects, float, int, List, List

### Community 53 - "InventoryModel"
Cohesion: 0.18
Nodes (10): bool, int, Sprite, string, HealItemDefinition, BattleItemStack, List, HealItemStack (+2 more)

### Community 54 - "NPCModel"
Cohesion: 0.22
Nodes (6): GameState, BorrarSave(), CrearSaveDePrueba(), AudioSource, GameManager, ContextMenu

### Community 55 - "AudioService"
Cohesion: 0.36
Nodes (3): AudioService, AudioClip, AudioSource

### Community 56 - "DefinitionRepository"
Cohesion: 0.29
Nodes (3): bool, Dictionary, DefinitionRepository

### Community 57 - "SceneLoaderService"
Cohesion: 0.36
Nodes (4): Action, IEnumerator, MonoBehaviour, SceneLoaderService

### Community 58 - "Arquitectura del Proyecto — PokeRush"
Cohesion: 0.25
Nodes (7): Arquitectura del Proyecto — PokeRush, 📊 Diagramas de Flujo de Datos (DFD), 💻 Entorno de Desarrollo, 📦 Librerías y Paquetes Principales, 🎮 Motor y Lenguaje, 🧩 Patrón de Arquitectura: MVC, 💾 Persistencia de Datos

### Community 59 - "AIProfileGenerator"
Cohesion: 0.24
Nodes (6): AnimationShowcaseView, bool, float, IEnumerator, string, Text

### Community 60 - ".TriggerEvent"
Cohesion: 0.25
Nodes (5): float, int, List, StatusEffects, StatusEffect

### Community 61 - "HealItemDefinition"
Cohesion: 0.13
Nodes (16): bool, Dictionary, float, IEnumerable, int, MenuItem, Rect, Sprite (+8 more)

### Community 62 - "CameraFollow"
Cohesion: 0.25
Nodes (6): CameraFollow, float, Transform, Vector2, Vector3, MonoBehaviour

### Community 63 - "com.unity.searcher"
Cohesion: 0.15
Nodes (9): Image, Text, HealthBarView, GameObject, Image, List, Text, Transform (+1 more)

### Community 64 - ".ActivateFlag"
Cohesion: 0.25
Nodes (5): BattleOutcome, bool, int, List, EnemyModel

### Community 65 - "CosmeticDefinition"
Cohesion: 0.21
Nodes (6): GameObject, List, Sprite, Text, Transform, InventoryView

### Community 66 - "SpecialItemDefinition"
Cohesion: 0.14
Nodes (10): BossRushDefinition, CharacterDefinition, AnimatorOverrideController, Sprite, string, int, Sprite, string (+2 more)

### Community 67 - "PlayerModel"
Cohesion: 0.22
Nodes (7): FieldEffectKind, int, LearnsetEntry, bool, int, string, MoveDefinition

### Community 68 - "BattleHudView"
Cohesion: 0.12
Nodes (10): BattleHudView, Action, Button, Color, float, GameObject, IEnumerator, Image (+2 more)

### Community 69 - "com.gilzoide.sqlite-net"
Cohesion: 0.33
Nodes (6): dependencies, depth, hash, source, version, com.gilzoide.sqlite-net

### Community 70 - "dependencies"
Cohesion: 0.23
Nodes (4): Random, IBattleRng, SeededRng, UnityRng

### Community 71 - "com.unity.collections"
Cohesion: 0.25
Nodes (6): AudioClip, bool, Color, float, Sprite, MoveAnimationDefinition

### Community 72 - "com.unity.ide.visualstudio"
Cohesion: 0.33
Nodes (4): CosmeticDefinition, AnimatorOverrideController, Sprite, string

### Community 73 - "com.unity.inputsystem"
Cohesion: 0.33
Nodes (5): AnimatorOverrideController, int, Sprite, string, PokenDefinition

### Community 76 - "com.unity.modules.director"
Cohesion: 0.50
Nodes (3): bool, int, StatChangeEntry

### Community 79 - "manifest.json"
Cohesion: 0.33
Nodes (5): abilities, downloadedAt, moves, species, tool

### Community 83 - "com.unity.modules.androidjni"
Cohesion: 0.08
Nodes (26): dependencies, depth, source, url, version, dependencies, depth, source (+18 more)

### Community 87 - "README.md"
Cohesion: 0.50
Nodes (3): ACLARACIONES, GrupoX_PokeRUSH_ESTFA_2026, Plataforma de Organizacion

## Knowledge Gaps
- **436 isolated node(s):** `Coverage`, `PlayerRow`, `PlayerPokenMoveRow`, `PlayerCosmeticRow`, `ActiveFlagRow` (+431 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **54 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MoveDefinition` connect `PlayerModel` to `AiContext`, `SpecialItemDefinition`, `PokenModel`, `PokeRushSmokeTests`, `com.unity.collections`, `com.unity.2d.sprite`, `com.unity.inputsystem`, `.GenerateAll`, `com.unity.modules.director`, `PokeApiMapper`, `BattleModel`, `GameManager`, `com.unity.modules.imgui`, `DefinitionRepository`, `AIProfileGenerator`, `.TriggerEvent`, `HealItemDefinition`?**
  _High betweenness centrality (0.085) - this node is a cross-community bridge._
- **Why does `dependencies` connect `dependencies` to `com.unity.modules.physics2d`, `com.unity.ext.nunit`, `com.unity.modules.animation`, `com.gilzoide.sqlite-net`, `com.unity.modules.particlesystem`, `com.unity.modules.subsystems`, `com.unity.modules.jsonserialize`, `com.unity.modules.unitywebrequest`, `com.unity.modules.imageconversion`, `com.unity.mathematics`, `com.unity.modules.physics`, `com.unity.test-framework`, `com.unity.modules.uielements`, `com.unity.render-pipelines.core`, `com.unity.modules.androidjni`, `com.unity.modules.ui`, `com.unity.2d.tilemap`?**
  _High betweenness centrality (0.063) - this node is a cross-community bridge._
- **Why does `PokenDefinition` connect `com.unity.inputsystem` to `SpecialItemDefinition`, `PlayerModel`, `PokeRushSmokeTests`, `BattleTestSceneBuilder`, `PokenModel`, `com.unity.ide.visualstudio`, `NPCDefinition`, `.GenerateAll`, `.TriggerEvent`, `BattleModel`, `MoveDefinition`, `DefinitionRepository`, `BattleTester`, `GameManager`?**
  _High betweenness centrality (0.050) - this node is a cross-community bridge._
- **What connects `Coverage`, `PlayerRow`, `PlayerPokenMoveRow` to the rest of the system?**
  _436 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `AiContext` be split into smaller, more focused modules?**
  _Cohesion score 0.06120218579234973 - nodes in this community are weakly interconnected._
- **Should `PokenModel` be split into smaller, more focused modules?**
  _Cohesion score 0.05128205128205128 - nodes in this community are weakly interconnected._
- **Should `BattleTestSceneBuilder` be split into smaller, more focused modules?**
  _Cohesion score 0.0670762928827445 - nodes in this community are weakly interconnected._