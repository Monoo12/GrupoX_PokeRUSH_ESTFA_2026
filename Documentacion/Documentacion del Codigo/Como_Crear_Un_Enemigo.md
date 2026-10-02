# 👾 Cómo crear un enemigo en PokeRush

Guía paso a paso para dar de alta un **enemigo** (combatiente común) desde el Editor de
Unity. Al final hay una sección con las diferencias para crear un **Boss** y otra con las
**limitaciones actuales** que conviene conocer antes de probar la batalla.

---

## 🧠 Conceptos previos (leer esto primero)

### Especie ≠ instancia
- **`PokenDefinition`** (ScriptableObject) = una **especie** (ej. "Pikachu"). Es una
  plantilla **compartida**: el Pikachu del jugador y el de un enemigo apuntan al **mismo**
  asset. Por eso **el nivel NO vive acá**.
- **`PokenModel`** (clase pura, runtime) = una **instancia concreta**: este Pikachu a nivel
  12, con su HP actual. El nivel y el estado viven acá.

### Jerarquía de personajes

```
CharacterDefinition        (identidad + sprites de mundo)
└── NPCDefinition           (diálogo + flags de historia)
    ├── EnemyDefinition     (enemigo común: equipo, recompensas, huida)
    └── BossDefinition      (jefe de nivel: cinemática, diálogos intro/derrota)
```

Un enemigo se arma con un asset **`EnemyDefinition`**, que hereda todos los campos de
`NPCDefinition` y `CharacterDefinition`.

### `EnemyTeamEntry` = especie + nivel
El equipo del enemigo (`pokenTeam`) es un array de **`EnemyTeamEntry`**. Cada entrada pega
una **especie** (`PokenDefinition`) con su **nivel** propio para este enemigo:

```csharp
[System.Serializable]
public class EnemyTeamEntry
{
    public PokenDefinition species;   // qué especie usa (asset compartido)
    public int level;                 // a qué nivel para ESTE enemigo
}
```

Así un mismo Pikachu puede aparecer a nivel 5 con un enemigo y a nivel 30 con otro, sin
duplicar el asset.

---

## 🪜 Pasos para crear un enemigo

### 1. Tener listas las especies y sus movimientos
Antes del enemigo necesitás los `PokenDefinition` de las especies que va a usar (y sus
`MoveDefinition`). Si no existen, crealos:

- **Especie:** click derecho en *Project* → **Create → PokeRush → PokenDefinition**.
  Llená *Stats base*, *Sprites* y el array *Movimientos*.
- **Movimiento:** **Create → PokeRush → MoveDefinition**.

Después de crear especies o movimientos, correr **PokeRush → Release → Armar
indice de definiciones** (el juego los busca por ese índice; la build lo rearma
sola, pero en el editor sin rearmarlo la consola avisa y se cargan todos).

### 2. Crear el asset del enemigo
Click derecho en *Project* → **Create → PokeRush → Characters → EnemyDefinition**.
Nombralo de forma clara, ej. `Enemy_Rival_Ruta1`.

### 3. Llenar los campos en el Inspector

**Heredados de `CharacterDefinition` (identidad y mundo):**

| Campo | Para qué sirve |
|---|---|
| `characterName` | Nombre que se muestra |
| `portrait` | Retrato (opcional, para diálogo) |
| `worldFrontSprite` / `worldBackSprite` / `worldSideSprite` | Sprites en el mapa |
| `worldAnimator` | Animator de mundo (opcional) |

**Heredados de `NPCDefinition` (diálogo e historia):**

| Campo | Para qué sirve |
|---|---|
| `defaultDialogue` | Diálogo al interactuar |
| `requiredFlag` | Flag que debe estar activo para que aparezca/condición (`None` = sin condición) |
| `grantsFlag` | Flag que otorga al interactuar (`None` = ninguno) |
| `canBattleLater` | Si se puede volver a pelear contra él |

**Propios de `EnemyDefinition` (batalla):**

| Campo | Para qué sirve |
|---|---|
| `pokenTeam` | **El equipo.** Array de `EnemyTeamEntry` (especie + nivel) — ver paso 4 |
| `battleFrontSprite` / `battleBackSprite` | Sprites del entrenador en combate |
| `battleMusic` | Música de la pelea |
| `battleIntroDialogue` | Diálogo de entrada al combate (opcional) |
| `moneyReward` | Dinero que da al ser derrotado |
| `itemRewards` | Ítems que suelta (array de `HealItemDefinition`) |
| `defeatGrantsFlag` | Flag que se activa al derrotarlo (ej. abrir un camino) |
| `fleeIfWeak` | Si huye cuando está débil |

### 4. Configurar el equipo (`pokenTeam`)
En el campo `pokenTeam`, subí el *Size* a la cantidad de Pokén del rival. Por cada entrada:
- **Species:** arrastrá un `PokenDefinition` ya creado.
- **Level:** el nivel de ese Pokén para este enemigo.

Ejemplo: un rival de la primera ruta podría ser
`[ {Rattata, Lv 3}, {Pidgey, Lv 5} ]`.

> ⚠️ El código pone un **mínimo de nivel 1**: si dejás `level` en 0 sin querer, igual se
> construye a nivel 1 para no romper los stats. Y las entradas **sin especie asignada se
> saltan**, así que no agregan un Pokén vacío.

### 5. Disparar la batalla
Acá está el estado real hoy (ver *Limitaciones*): el único camino **cableado** para iniciar
un combate es vía un **`EventDefinition`** con `triggerBattle = true` y un `bossToFight`
asignado — pero ese campo es de tipo **`BossDefinition`**, o sea **solo sirve para bosses**.

Para un enemigo común, el disparador todavía no está conectado (vive en
`PlayerController.Interact()`, que está vacío). El método que recibe la pelea sí acepta
cualquier NPC:

```csharp
battleController.StartBattle(playerModel, enemyDefinition); // acepta NPCDefinition
```

…así que el día que se cablee `Interact()`, podrá lanzar tu `EnemyDefinition`
directamente.

---

## 👑 Diferencias para crear un Boss

Un boss usa **`BossDefinition`** (**Create → PokeRush → Characters → BossDefinition**).
Comparte `pokenTeam` (mismo `EnemyTeamEntry[]`), `battleMusic`, `moneyReward`,
`itemRewards` y `defeatGrantsFlag`, pero suma:

| Campo extra | Para qué sirve |
|---|---|
| `battleIntroSprite` / `battleFrontSprite` / `battleFrontShinySprite` | Sprites especiales del jefe |
| `battleIntroDialogue` / `battleDefeatDialogue` | Diálogos de entrada y de derrota |
| `triggerElaborateCinematic` | Si dispara una cinemática elaborada |

Y a diferencia del enemigo común, un boss **sí** se puede arrastrar al campo `bossToFight`
de un `EventDefinition`, así que su batalla **ya se puede disparar** por evento de zona.

---

## 🚧 Limitaciones actuales (estado del código)

1. **Los Pokén enemigos nacen SIN movimientos.** Hoy `EnemyModel` construye el equipo con
   especie y nivel, pero no copia el moveset de `PokenDefinition.moves`. Hasta resolver eso,
   el rival no puede atacar en batalla. *(Pendiente — "bug #5".)*
2. **El trigger de batalla para enemigos comunes no está cableado.** Solo los bosses se
   lanzan por `EventDefinition.bossToFight`. Para enemigos hace falta implementar
   `PlayerController.Interact()`.
3. **La lógica de combate todavía está vacía** (`BattleController.ExecutePlayerAction`,
   `ExecuteEnemyTurn`, `EndBattle`): falta fórmula de daño, tabla de tipos e IA enemiga.
4. **`DefinitionRepository` no carga nada todavía** porque faltan las carpetas
   `Assets/Resources/Definitions/...`. Esto NO afecta a las referencias que arrastrás a mano
   en el Inspector del enemigo (esas funcionan igual), solo al guardado/carga por nombre.

---

## ✅ Checklist rápido

- [ ] Existen los `PokenDefinition` (con stats y movimientos) que usará el enemigo
- [ ] Creado el asset `EnemyDefinition` (Create → PokeRush → Characters → EnemyDefinition)
- [ ] `characterName` y sprites de mundo cargados
- [ ] `pokenTeam` con cada entrada = especie + nivel
- [ ] `moneyReward` / `itemRewards` / `defeatGrantsFlag` definidos
- [ ] (Si es relevante) `defaultDialogue`, `requiredFlag`, `grantsFlag`
- [ ] Plan para disparar la batalla (hoy: vía evento solo para bosses)
