# Arquitectura del Proyecto — PokeRush

## 🎮 Motor y Lenguaje
- **Motor:** Unity (render 2D con **URP** — Universal Render Pipeline)
- **Lenguaje de programación:** C#

## 💻 Entorno de Desarrollo
- **IDEs configurados:** JetBrains Rider / Visual Studio (paquetes `com.unity.ide.rider`, `com.unity.ide.visualstudio`)
- **Control de versiones:** Git + GitHub

---

## 🧩 Patrón de Arquitectura: MVC
El proyecto sigue el patrón **Modelo–Vista–Controlador (MVC)**, con capas de apoyo de
*Services*, *Data* y *Core*. El código vive en `Juego/PokeRush/Assets/Scripts/`:

| Capa | Carpeta | Responsabilidad | Ejemplos |
|------|---------|-----------------|----------|
| **Model** | `Models/` | Estado y lógica de negocio (sin Unity UI) | `PlayerModel`, `BattleModel`, `EnemyModel`, `InventoryModel`, `StoryModel` |
| **View** | `Views/` | Render de UI y feedback al jugador (uGUI) | `BattleHudView`, `DialogueView`, `HealthBarView`, `InventoryView`, `TeamView` |
| **Controller** | `Controllers/` | Orquestan la interacción entre Model y View | `BattleController`, `MapController`, `EventController`, `DialogueController`, `PlayerController`, `PlayerInputController` |
| **Services** | `Services/` | Servicios transversales (persistencia, audio, escenas) | `DatabaseService`, `SaveService`, `AudioService`, `SceneLoaderService`, `DefinitionRepository` |
| **Data** | `Data/` | Definiciones de contenido (ScriptableObjects) | `PokenDefinition`, `MoveDefinition`, `EnemyDefinition`, `VendorDefinition`, `NurseDefinition` |
| **Core** | `Core/` | Enums y tipos base del dominio | `BattlePhase`, `PokenType`, `StatusEffect`, `GameState`, `StoryFlag` |

> **Inyección de dependencias:** se resuelve con el patrón **Singleton manual**
> (`DatabaseService.Instance`, `SaveService.Instance`), no con un framework de DI externo.

---

## 💾 Persistencia de Datos
- **Motor de base de datos:** **SQLite** (embebida, archivo local `pokerush.db` en `Application.persistentDataPath`).
- **Librería:** [`com.gilzoide.sqlite-net`](https://github.com/gilzoide/unity-sqlite-net) (ORM SQLite-net).
- **Acceso a datos:**
  - `DatabaseService` → abre la conexión y crea las tablas.
  - `SaveService` → guarda/carga el progreso completo del jugador.
  - `DatabaseRows` → mapeo objeto-tabla (filas).
- **Tablas:** `player`, `player_poken`, `player_poken_moves`, `player_inventory`, `player_cosmetics`, `active_flags`, `dialogues` (ver `Base de datos/Pokerush.sql`).

---

## 📦 Librerías y Paquetes Principales
| Paquete | Uso |
|---------|-----|
| `com.unity.render-pipelines.universal` | Pipeline de render 2D (URP) |
| `com.unity.inputsystem` | Entrada del jugador (`PlayerInputController`) |
| `com.unity.ugui` + TextMeshPro | Interfaz de usuario (uGUI) |
| Suite **2D** (`2d.animation`, `2d.aseprite`, `2d.tilemap`, `spriteshape`…) | Sprites, animación y mapas por tiles |
| `com.unity.timeline` | Cinemáticas (`CinematicView`) |
| `com.gilzoide.sqlite-net` | Persistencia en SQLite |

---

## 📊 Diagramas de Flujo de Datos (DFD)
- **Nivel 0 (Contexto):** [`DFD/DFD_Nivel0_Contexto_PokeRush`](DFD/DFD_Nivel0_Contexto_PokeRush.png) — el sistema como un único proceso y su relación con el Jugador.
- **Nivel 1 (Procesos):** [`DFD/DFD_Nivel1_PokeRush`](DFD/DFD_Nivel1_PokeRush.png) — 8 procesos del juego y los 6 almacenes (tablas SQLite).

> Archivos editables vectoriales `.svg` disponibles en la carpeta `DFD/`.
