# Graph Report - PokeRush  (2026-06-25)

## Corpus Check
- 71 files · ~9,456,902 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 840 nodes · 992 edges · 77 communities (70 shown, 7 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7f570485`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Community 39|Community 39]]
- [[_COMMUNITY_Community 40|Community 40]]
- [[_COMMUNITY_Community 41|Community 41]]
- [[_COMMUNITY_Community 42|Community 42]]
- [[_COMMUNITY_Community 43|Community 43]]
- [[_COMMUNITY_Community 44|Community 44]]
- [[_COMMUNITY_Community 45|Community 45]]
- [[_COMMUNITY_Community 46|Community 46]]
- [[_COMMUNITY_Community 47|Community 47]]
- [[_COMMUNITY_Community 48|Community 48]]
- [[_COMMUNITY_Community 49|Community 49]]
- [[_COMMUNITY_Community 50|Community 50]]
- [[_COMMUNITY_Community 51|Community 51]]
- [[_COMMUNITY_Community 52|Community 52]]
- [[_COMMUNITY_Community 53|Community 53]]
- [[_COMMUNITY_Community 54|Community 54]]
- [[_COMMUNITY_Community 55|Community 55]]
- [[_COMMUNITY_Community 56|Community 56]]
- [[_COMMUNITY_Community 57|Community 57]]
- [[_COMMUNITY_Community 58|Community 58]]
- [[_COMMUNITY_Community 59|Community 59]]
- [[_COMMUNITY_Community 60|Community 60]]
- [[_COMMUNITY_Community 61|Community 61]]
- [[_COMMUNITY_Community 62|Community 62]]
- [[_COMMUNITY_Community 63|Community 63]]
- [[_COMMUNITY_Community 64|Community 64]]
- [[_COMMUNITY_Community 65|Community 65]]
- [[_COMMUNITY_Community 66|Community 66]]
- [[_COMMUNITY_Community 67|Community 67]]
- [[_COMMUNITY_Community 68|Community 68]]
- [[_COMMUNITY_Community 69|Community 69]]
- [[_COMMUNITY_Community 70|Community 70]]

## God Nodes (most connected - your core abstractions)
1. `PlayerController` - 17 edges
2. `MonoBehaviour` - 16 edges
3. `com.unity.modules.jsonserialize` - 15 edges
4. `TeamView` - 14 edges
5. `EventController` - 13 edges
6. `DefinitionRepository` - 12 edges
7. `com.unity.modules.uielements` - 12 edges
8. `com.unity.mathematics` - 12 edges
9. `BattleController` - 11 edges
10. `DialogueController` - 11 edges

## Surprising Connections (you probably didn't know these)
- `BattleController` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/BattleController.cs → Codigo/PokeRush/Assets/Scripts/Services/SceneLoaderService.cs
- `EventController` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/EventController.cs → Codigo/PokeRush/Assets/Scripts/Services/SceneLoaderService.cs
- `MapController` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/MapController.cs → Codigo/PokeRush/Assets/Scripts/Services/SceneLoaderService.cs
- `PlayerController` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Controllers/PlayerController.cs → Codigo/PokeRush/Assets/Scripts/Services/SceneLoaderService.cs
- `GameManager` --inherits--> `MonoBehaviour`  [EXTRACTED]
  Codigo/PokeRush/Assets/Scripts/Managers/GameManager.cs → Codigo/PokeRush/Assets/Scripts/Services/SceneLoaderService.cs

## Import Cycles
- None detected.

## Communities (77 total, 7 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.04
Nodes (53): dependencies, com.gilzoide.sqlite-net, com.unity.2d.animation, com.unity.2d.aseprite, com.unity.2d.psdimporter, com.unity.2d.sprite, com.unity.2d.spriteshape, com.unity.2d.tilemap (+45 more)

### Community 1 - "Community 1"
Cohesion: 0.05
Nodes (23): Animator, Action, DialogueDefinition, int, StoryModel, BattleController, bool, PlayerController (+15 more)

### Community 2 - "Community 2"
Cohesion: 0.05
Nodes (46): dependencies, depth, source, url, version, dependencies, depth, source (+38 more)

### Community 3 - "Community 3"
Cohesion: 0.05
Nodes (45): dependencies, depth, source, version, dependencies, depth, source, version (+37 more)

### Community 4 - "Community 4"
Cohesion: 0.06
Nodes (35): dependencies, dependencies, depth, source, version, dependencies, depth, source (+27 more)

### Community 5 - "Community 5"
Cohesion: 0.07
Nodes (24): AudioClip, bool, DialogueDefinition, EnemyTeamEntry, HealItemDefinition, int, Sprite, StoryFlag (+16 more)

### Community 6 - "Community 6"
Cohesion: 0.08
Nodes (27): dependencies, depth, source, url, version, dependencies, depth, dependencies (+19 more)

### Community 7 - "Community 7"
Cohesion: 0.08
Nodes (24): 1. Exploración del Mundo, 2. Sistema de Combate, 3. Equipo y Progresión, 4. Inventario y Economía, 5. NPCs, Historia y Servicios, 6. Personalización, 7. Partida y Configuración, 8. Desarrollo (+16 more)

### Community 8 - "Community 8"
Cohesion: 0.09
Nodes (25): depth, source, version, dependencies, depth, source, version, dependencies (+17 more)

### Community 9 - "Community 9"
Cohesion: 0.15
Nodes (10): InventoryModel, List, PlayerModel, PokenModel, StoryModel, PlayerInventoryRow, PlayerPokenRow, DatabaseService (+2 more)

### Community 10 - "Community 10"
Cohesion: 0.10
Nodes (21): dependencies, depth, source, version, dependencies, depth, source, version (+13 more)

### Community 11 - "Community 11"
Cohesion: 0.13
Nodes (10): bool, IEnumerator, PlayerModel, PokenModel, StoryModel, PlayerController, float, LayerMask (+2 more)

### Community 12 - "Community 12"
Cohesion: 0.12
Nodes (9): BattleItemDefinition, bool, HealItemDefinition, MoveDefinition, PokenDefinition, CosmeticDefinition, Dictionary, DefinitionRepository (+1 more)

### Community 13 - "Community 13"
Cohesion: 0.19
Nodes (8): GameObject, HealthBarView, Image, List, PokenModel, Text, Transform, TeamView

### Community 14 - "Community 14"
Cohesion: 0.13
Nodes (14): 1. Tener listas las especies y sus movimientos, 2. Crear el asset del enemigo, 3. Llenar los campos en el Inspector, 4. Configurar el equipo (`pokenTeam`), 5. Disparar la batalla, ✅ Checklist rápido, 🧠 Conceptos previos (leer esto primero), 👾 Cómo crear un enemigo en PokeRush (+6 more)

### Community 15 - "Community 15"
Cohesion: 0.14
Nodes (15): dependencies, depth, source, version, dependencies, depth, source, version (+7 more)

### Community 16 - "Community 16"
Cohesion: 0.24
Nodes (8): CinematicView, BattleController, DialogueController, NPCDefinition, PlayerModel, StoryModel, EventController, EventDefinition

### Community 17 - "Community 17"
Cohesion: 0.18
Nodes (7): BattleHudView, BattleModel, NPCDefinition, PlayerModel, BattleController, EnemyModel, MoveModel

### Community 18 - "Community 18"
Cohesion: 0.18
Nodes (7): GameObject, InventoryModel, List, Sprite, Text, Transform, InventoryView

### Community 19 - "Community 19"
Cohesion: 0.21
Nodes (3): StatusEffect, ExperienceTableDefinition, PokenModel

### Community 20 - "Community 20"
Cohesion: 0.45
Nodes (9): clear_cell(), main(), set_font(), set_row_text(), set_value_row(), shade_cell(), style_header_rows(), unique_cells() (+1 more)

### Community 21 - "Community 21"
Cohesion: 0.29
Nodes (5): Button, PlayerModel, InventoryView, TeamView, PauseMenuView

### Community 22 - "Community 22"
Cohesion: 0.22
Nodes (6): DialogueController, NPCDefinition, PlayerController, StoryModel, MapController, EventController

### Community 23 - "Community 23"
Cohesion: 0.20
Nodes (8): AnimatorOverrideController, Sprite, string, bool, DialogueDefinition, StoryFlag, CharacterDefinition, NPCDefinition

### Community 24 - "Community 24"
Cohesion: 0.27
Nodes (3): AudioSource, GameState, GameManager

### Community 25 - "Community 25"
Cohesion: 0.20
Nodes (10): depth, source, version, dependencies, depth, source, version, dependencies (+2 more)

### Community 26 - "Community 26"
Cohesion: 0.20
Nodes (10): dependencies, depth, source, version, dependencies, depth, source, version (+2 more)

### Community 27 - "Community 27"
Cohesion: 0.20
Nodes (10): dependencies, depth, source, version, dependencies, depth, source, version (+2 more)

### Community 28 - "Community 28"
Cohesion: 0.22
Nodes (8): BossDefinition, bool, DialogueDefinition, HealItemDefinition, int, StoryFlag, string, EventDefinition

### Community 29 - "Community 29"
Cohesion: 0.22
Nodes (4): BattleModel, HealthBarView, Text, BattleHudView

### Community 30 - "Community 30"
Cohesion: 0.25
Nodes (7): AnimatorOverrideController, int, MoveDefinition, PokenType, Sprite, string, PokenDefinition

### Community 31 - "Community 31"
Cohesion: 0.36
Nodes (3): AudioClip, AudioSource, AudioService

### Community 32 - "Community 32"
Cohesion: 0.25
Nodes (7): Arquitectura del Proyecto — PokeRush, 📊 Diagramas de Flujo de Datos (DFD), 💻 Entorno de Desarrollo, 📦 Librerías y Paquetes Principales, 🎮 Motor y Lenguaje, 🧩 Patrón de Arquitectura: MVC, 💾 Persistencia de Datos

### Community 33 - "Community 33"
Cohesion: 0.25
Nodes (3): NPCModel, NurseModel, VendorModel

### Community 34 - "Community 34"
Cohesion: 0.25
Nodes (7): ActiveFlagRow, DialogueRow, PlayerCosmeticRow, PlayerInventoryRow, PlayerPokenMoveRow, PlayerPokenRow, PlayerRow

### Community 35 - "Community 35"
Cohesion: 0.29
Nodes (6): AttackType, int, PokenType, StatusEffect, string, MoveDefinition

### Community 36 - "Community 36"
Cohesion: 0.29
Nodes (4): int, BossRushDefinition, ExperienceTableDefinition, ScriptableObject

### Community 37 - "Community 37"
Cohesion: 0.43
Nodes (3): Action, IEnumerator, SceneLoaderService

### Community 38 - "Community 38"
Cohesion: 0.29
Nodes (6): dependencies, depth, source, version, dependencies, com.unity.modules.wind

### Community 39 - "Community 39"
Cohesion: 0.33
Nodes (5): AnimatorOverrideController, PokenDefinition, Sprite, string, CosmeticDefinition

### Community 40 - "Community 40"
Cohesion: 0.33
Nodes (5): bool, int, Sprite, string, HealItemDefinition

### Community 41 - "Community 41"
Cohesion: 0.33
Nodes (5): int, Sprite, StoryFlag, string, SpecialItemDefinition

### Community 42 - "Community 42"
Cohesion: 0.33
Nodes (6): dependencies, depth, hash, source, version, com.gilzoide.sqlite-net

### Community 43 - "Community 43"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.2d.animation

### Community 44 - "Community 44"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.2d.aseprite

### Community 45 - "Community 45"
Cohesion: 0.33
Nodes (6): dependencies, dependencies, depth, source, version, com.unity.modules.animation

### Community 46 - "Community 46"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.2d.psdimporter

### Community 47 - "Community 47"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.2d.tooling

### Community 48 - "Community 48"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.burst

### Community 49 - "Community 49"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.collab-proxy

### Community 50 - "Community 50"
Cohesion: 0.33
Nodes (6): dependencies, depth, source, url, version, com.unity.mathematics

### Community 51 - "Community 51"
Cohesion: 0.40
Nodes (4): int, Sprite, string, BattleItemDefinition

### Community 52 - "Community 52"
Cohesion: 0.40
Nodes (4): Sprite, StoryFlag, string, DialogueDefinition

### Community 54 - "Community 54"
Cohesion: 0.40
Nodes (4): BattleItemStack, HealItemStack, InventoryModel, SpecialItemStack

### Community 55 - "Community 55"
Cohesion: 0.40
Nodes (5): depth, source, url, version, com.unity.2d.common

### Community 56 - "Community 56"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.2d.sprite

### Community 57 - "Community 57"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.accessibility

### Community 58 - "Community 58"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.ai

### Community 59 - "Community 59"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.androidjni

### Community 60 - "Community 60"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.umbra

### Community 61 - "Community 61"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.unitywebrequestaudio

### Community 62 - "Community 62"
Cohesion: 0.40
Nodes (5): dependencies, depth, source, version, com.unity.modules.unitywebrequesttexture

### Community 63 - "Community 63"
Cohesion: 0.50
Nodes (3): int, PokenDefinition, EnemyTeamEntry

### Community 67 - "Community 67"
Cohesion: 0.50
Nodes (3): ACLARACIONES, GrupoX_PokeRUSH_ESTFA_2026, Plataforma de Organizacion

## Knowledge Gaps
- **467 isolated node(s):** `Assembly-CSharp`, `BattleHudView`, `BattleModel`, `EnemyModel`, `NPCDefinition` (+462 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `dependencies` connect `Community 38` to `Community 2`, `Community 3`, `Community 4`, `Community 6`, `Community 8`, `Community 10`, `Community 15`, `Community 25`, `Community 26`, `Community 27`, `Community 42`, `Community 43`, `Community 44`, `Community 45`, `Community 46`, `Community 47`, `Community 48`, `Community 49`, `Community 50`, `Community 55`, `Community 56`, `Community 57`, `Community 58`, `Community 59`, `Community 60`, `Community 61`, `Community 62`?**
  _High betweenness centrality (0.154) - this node is a cross-community bridge._
- **Why does `MonoBehaviour` connect `Community 1` to `Community 37`, `Community 11`, `Community 13`, `Community 16`, `Community 17`, `Community 18`, `Community 21`, `Community 22`, `Community 24`, `Community 29`?**
  _High betweenness centrality (0.037) - this node is a cross-community bridge._
- **Why does `PlayerController` connect `Community 11` to `Community 1`?**
  _High betweenness centrality (0.008) - this node is a cross-community bridge._
- **What connects `Assembly-CSharp`, `BattleHudView`, `BattleModel` to the rest of the system?**
  _467 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.037037037037037035 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.04931972789115646 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.04541062801932367 - nodes in this community are weakly interconnected._