# Base de datos — Creación y Queries

Documentación de cómo se crea la base de datos de PokeRush y de todas las operaciones (queries) que el juego ejecuta sobre ella.

## Tecnología

- **Motor:** SQLite (archivo local, embebido — no hay servidor).
- **Acceso desde código:** librería **sqlite-net** (ORM). No se escribe SQL a mano en tiempo de ejecución: cada tabla es una clase C# con atributos (`[Table]`, `[PrimaryKey]`, `[AutoIncrement]`) y las "queries" se expresan como llamadas LINQ (`Table<T>().Where(...)`) e inserts/updates/deletes tipados.
- **Ubicación del archivo:** `Application.persistentDataPath/pokerush.db` (carpeta de datos persistentes de Unity, propia de cada equipo/instalación).

> El SQL crudo equivalente está en [`Pokerush.sql`](Pokerush.sql). Ese archivo es la referencia del esquema; en tiempo de ejecución las tablas las genera sqlite-net a partir de las clases (ver más abajo).

---

## 1. Creación de la base de datos

La creación está centralizada en `DatabaseService`, un singleton que abre (o crea si no existe) el archivo `pokerush.db` y genera todas las tablas al construirse.

**Archivo:** [`Services/DatabaseService.cs`](../../Codigo/PokeRush/Assets/Scripts/Services/DatabaseService.cs)

```csharp
private DatabaseService()
{
    string dbPath = Path.Combine(Application.persistentDataPath, "pokerush.db");
    _connection = new SQLiteConnection(dbPath);   // crea el archivo si no existe
    CreateTables();
}

private void CreateTables()
{
    _connection.CreateTable<PlayerRow>();
    _connection.CreateTable<PlayerPokenRow>();
    _connection.CreateTable<PlayerPokenMoveRow>();
    _connection.CreateTable<PlayerInventoryRow>();
    _connection.CreateTable<PlayerCosmeticRow>();
    _connection.CreateTable<ActiveFlagRow>();
    _connection.CreateTable<DialogueRow>();
}
```

`CreateTable<T>()`:
- Ejecuta internamente un `CREATE TABLE IF NOT EXISTS`, por lo que es idempotente (no rompe si la tabla ya existe).
- Si la tabla ya existe pero le falta alguna columna nueva, sqlite-net agrega la columna con un `ALTER TABLE` automático. Así se sumó la columna `Ivs` sin migración manual (ver comentario en `DatabaseRows.cs`).

**¿Quién dispara la creación?** `GameManager.Awake()` toca `DatabaseService.Instance` para forzar la apertura de la conexión antes de que cualquier pantalla la use:

```csharp
// GameManager.cs
_ = DatabaseService.Instance;   // abre la BD y crea las tablas
```

El cierre de la conexión se hace con `DatabaseService.Instance.Close()`.

---

## 2. Esquema (tablas y columnas)

Las tablas se definen como clases en [`Services/DatabaseRows.cs`](../../Codigo/PokeRush/Assets/Scripts/Services/DatabaseRows.cs). Resumen:

| Tabla (`[Table]`)     | Clase                 | PK                     | Columnas                                                                                                   |
|-----------------------|-----------------------|------------------------|-----------------------------------------------------------------------------------------------------------|
| `player`              | `PlayerRow`           | `Id` (manual, sin auto)| `Name`, `Money`                                                                                            |
| `player_poken`        | `PlayerPokenRow`      | `Id` (autoincrement)   | `PlayerId`, `PokenDefinitionId`, `Level`, `CurrentXP`, `CurrentHP`, `StatusEffect`, `SlotIndex`, `EquippedCosmetic`, `Ivs` |
| `player_poken_moves`  | `PlayerPokenMoveRow`  | `Id` (autoincrement)   | `PlayerPokenId`, `MoveDefinitionId`, `CurrentPP`                                                           |
| `player_inventory`    | `PlayerInventoryRow`  | `Id` (autoincrement)   | `PlayerId`, `ItemDefinitionId`, `ItemType`, `Quantity`                                                     |
| `player_cosmetics`    | `PlayerCosmeticRow`   | `Id` (autoincrement)   | `PlayerId`, `CosmeticDefinitionId`                                                                         |
| `active_flags`        | `ActiveFlagRow`       | `Id` (autoincrement)   | `PlayerId`, `FlagName`                                                                                     |
| `dialogues`           | `DialogueRow`         | `Id` (autoincrement)   | `NpcId`, `LineIndex`, `Text`, `RequiredFlag`, `GrantsFlag`                                                 |

Notas de diseño relevantes:

- **`player.Id` NO es autoincrement**: es el número de slot de guardado (hoy siempre `1`), asignado a mano en `SavePlayer`. Con autoincrement, sqlite-net ignoraba el Id asignado y `HasSaveData(1)` / `LoadPlayer(1)` no encontraban la fila.
- **`player_poken.Ivs`** guarda los IVs serializados como texto `"hp,atk,def,spa,spd,spe"` (ej: `"31,12,0,5,20,31"`). Es `string` (no `int`) para distinguir saves viejos (`NULL` → se regeneran IVs) de un IV `0` legítimo.
- Los **enums se persisten como texto** (`StatusEffect`, `StoryFlag`) vía `.ToString()` / `Enum.TryParse`.
- Las **definiciones (Pokens, movimientos, ítems, cosméticos)** NO se guardan enteras: se guarda el **nombre del ScriptableObject** (`PokenDefinitionId`, `MoveDefinitionId`, etc.) y al cargar se resuelve contra `DefinitionRepository`.

> ℹ️ **Fuente de verdad:** el esquema real en ejecución lo genera sqlite-net desde las clases, así que la referencia canónica son `DatabaseRows.cs` + `DatabaseService.cs`. El archivo [`Pokerush.sql`](Pokerush.sql) se mantiene alineado a mano (ya incluye la columna `ivs` en `player_poken`).

---

## 3. Queries / operaciones sobre la BD

Toda la lógica de lectura/escritura vive en [`Services/SaveService.cs`](../../Codigo/PokeRush/Assets/Scripts/Services/SaveService.cs) (singleton). La conexión la toma de `DatabaseService.Instance.Connection`.

Patrón general de guardado de colecciones: **borrar todo lo del jugador y re-insertar** (más simple que hacer diff fila por fila).

### 3.1 Guardar partida — `SavePlayer(player, story, playerId = 1)`

| # | Operación | Query equivalente (LINQ / SQL) |
|---|-----------|--------------------------------|
| 1 | Buscar fila del jugador | `Table<PlayerRow>().FirstOrDefault(p => p.Id == playerId)` → `SELECT * FROM player WHERE id = ? LIMIT 1` |
| 2 | Insertar jugador (si no existía) | `Insert(playerRow)` → `INSERT INTO player (...) VALUES (...)` |
| 3 | Actualizar jugador (si existía) | `Update(playerRow)` → `UPDATE player SET name=?, money=? WHERE id=?` |
| 4 | Borrar equipo viejo | `Table<PlayerPokenRow>().Where(p => p.PlayerId == playerId)` + `Delete(row)` por fila → `DELETE FROM player_poken WHERE ...` |
| 5 | Insertar cada Poken | `Insert(pokenRow)` (ver `SavePoken`) |
| 6 | Borrar inventario viejo | `Table<PlayerInventoryRow>().Where(p => p.PlayerId == playerId)` + `Delete` |
| 7 | Insertar inventario | `Insert(...)` (ver `SaveInventory`) |
| 8 | Borrar flags viejas | `Table<ActiveFlagRow>().Where(f => f.PlayerId == playerId)` + `Delete` |
| 9 | Insertar flags | `Insert(new ActiveFlagRow { ... })` por cada `StoryFlag` activa |

**`SavePoken(poken, playerId, slotIndex)`** (privado):
- `Insert(pokenRow)` → inserta en `player_poken`. Tras el insert, `pokenRow.Id` queda cargado (autoincrement) y se usa para vincular los movimientos.
- Por cada movimiento: `Insert(new PlayerPokenMoveRow { PlayerPokenId = pokenRow.Id, ... })` → `INSERT INTO player_poken_moves ...`.

**`SaveInventory(inventory, playerId)`** (privado): un `Insert` en `player_inventory` por cada ítem, con `ItemType` = `"Heal"` / `"Battle"` / `"Special"` para poder separarlos al cargar.

### 3.2 Cargar partida — `LoadPlayer(story, playerId = 1)`

Devuelve `null` si no hay guardado.

| # | Operación | Query equivalente |
|---|-----------|-------------------|
| 1 | Traer jugador | `Table<PlayerRow>().FirstOrDefault(p => p.Id == playerId)` → `SELECT ... WHERE id=?` |
| 2 | Traer equipo ordenado | `Table<PlayerPokenRow>().Where(p => p.PlayerId == playerId).OrderBy(p => p.SlotIndex)` → `SELECT ... WHERE player_id=? ORDER BY slot_index` |
| 3 | Traer movimientos de cada Poken | `Table<PlayerPokenMoveRow>().Where(m => m.PlayerPokenId == row.Id)` (ver `LoadPoken`) |
| 4 | Traer inventario | `Table<PlayerInventoryRow>().Where(i => i.PlayerId == playerId)` |
| 5 | Traer flags | `Table<ActiveFlagRow>().Where(f => f.PlayerId == playerId)` |

`LoadPoken` y `LoadInventory` reconstruyen los modelos resolviendo cada `*DefinitionId` contra `DefinitionRepository`; si una definición ya no existe en el proyecto, esa fila se omite.

### 3.3 ¿Existe guardado? — `HasSaveData(playerId = 1)`

```csharp
return _db.Table<PlayerRow>().Any(p => p.Id == playerId);
// SELECT EXISTS(SELECT 1 FROM player WHERE id = ?)
```
Se usa para mostrar/ocultar el botón "Continuar" (ver `StartScreenView`) y como verificación tras un guardado en `GameManager`.

### 3.4 Borrar guardado — `DeleteSave(playerId = 1)`

Borra en orden respetando las dependencias (hijos antes que padres):

1. `player_poken_moves` de cada Poken del jugador (`WHERE player_poken_id = ?`).
2. `player_poken` del jugador (`WHERE player_id = ?`).
3. `player_inventory` del jugador.
4. `active_flags` del jugador.
5. `player` (la fila raíz).

---

## 4. Dónde se llaman estas operaciones

| Llamada | Archivo | Contexto |
|---------|---------|----------|
| `DatabaseService.Instance` (abre BD + crea tablas) | [`GameManager.cs:46`](../../Codigo/PokeRush/Assets/Scripts/Managers/GameManager.cs) | Arranque del juego |
| `SaveService.Instance.LoadPlayer(Story)` | [`GameManager.cs:104`](../../Codigo/PokeRush/Assets/Scripts/Managers/GameManager.cs), [`SaveSlotMenuView.cs:96`](../../Codigo/PokeRush/Assets/Scripts/Views/SaveSlotMenuView.cs) | Cargar / continuar partida |
| `SaveService.Instance.SavePlayer(...)` | [`GameManager.cs:114`](../../Codigo/PokeRush/Assets/Scripts/Managers/GameManager.cs) | Guardar progreso |
| `SaveService.Instance.HasSaveData(...)` | [`StartScreenView.cs:71`](../../Codigo/PokeRush/Assets/Scripts/Views/StartScreenView.cs), [`GameManager.cs:147`](../../Codigo/PokeRush/Assets/Scripts/Managers/GameManager.cs) | Botón "Continuar" / verificación |
| `SaveService.Instance.DeleteSave()` | [`SaveSlotMenuView.cs:152`](../../Codigo/PokeRush/Assets/Scripts/Views/SaveSlotMenuView.cs) | Botón "Borrar partida" |

---

## 5. ABM por entidad (Alta / Baja / Modificación / Consulta)

ABM de cada entidad tal como lo implementa el código. Todas las operaciones son métodos tipados de sqlite-net (`Insert` / `Update` / `Delete` / `Table<T>()...`); a la derecha figura el SQL equivalente. Recordar el patrón general: las colecciones (equipo, movimientos, inventario, flags) **no se modifican fila por fila**, se hace **Baja total + Alta** en cada guardado.

### 5.1 Jugador (`player`)

| Operación | Método (`SaveService`) | Código sqlite-net | SQL equivalente |
|-----------|------------------------|-------------------|-----------------|
| **Alta** | `SavePlayer` (si no existe) | `_db.Insert(playerRow)` | `INSERT INTO player (id, name, money) VALUES (?, ?, ?)` |
| **Modificación** | `SavePlayer` (si existe) | `_db.Update(playerRow)` | `UPDATE player SET name = ?, money = ? WHERE id = ?` |
| **Baja** | `DeleteSave` | `_db.Delete(playerRow)` | `DELETE FROM player WHERE id = ?` |
| **Consulta** | `LoadPlayer` / `HasSaveData` | `Table<PlayerRow>().FirstOrDefault(...)` / `.Any(...)` | `SELECT * FROM player WHERE id = ?` / `SELECT EXISTS(...)` |

> `player` es la única entidad con **Modificación real (UPDATE)**. El resto usa borrar-y-reinsertar.

### 5.2 Poken del equipo (`player_poken`)

| Operación | Método | Código | SQL equivalente |
|-----------|--------|--------|-----------------|
| **Alta** | `SavePoken` | `_db.Insert(pokenRow)` | `INSERT INTO player_poken (...) VALUES (...)` |
| **Baja** | `SavePlayer` (previo a re-guardar) y `DeleteSave` | `Where(p => p.PlayerId == playerId)...ForEach(Delete)` | `DELETE FROM player_poken WHERE player_id = ?` |
| **Modificación** | — (no hay UPDATE; se rehace por Baja + Alta) | — | — |
| **Consulta** | `LoadPlayer` | `Where(p => p.PlayerId == playerId).OrderBy(p => p.SlotIndex)` | `SELECT * FROM player_poken WHERE player_id = ? ORDER BY slot_index` |

### 5.3 Movimientos del Poken (`player_poken_moves`)

| Operación | Método | Código | SQL equivalente |
|-----------|--------|--------|-----------------|
| **Alta** | `SavePoken` | `_db.Insert(new PlayerPokenMoveRow { PlayerPokenId = pokenRow.Id, ... })` | `INSERT INTO player_poken_moves (...) VALUES (...)` |
| **Baja** | `DeleteSave` | `Where(m => m.PlayerPokenId == pokenRow.Id)...ForEach(Delete)` | `DELETE FROM player_poken_moves WHERE player_poken_id = ?` |
| **Modificación** | — (Baja + Alta) | — | — |
| **Consulta** | `LoadPoken` | `Where(m => m.PlayerPokenId == row.Id)` | `SELECT * FROM player_poken_moves WHERE player_poken_id = ?` |

> En `SavePlayer` la baja de los movimientos ocurre de forma indirecta: al borrarse el `player_poken` y volver a insertarse, `SavePoken` reinserta sus movimientos con el nuevo `Id`.

### 5.4 Inventario (`player_inventory`)

| Operación | Método | Código | SQL equivalente |
|-----------|--------|--------|-----------------|
| **Alta** | `SaveInventory` | `_db.Insert(new PlayerInventoryRow { ..., ItemType = "Heal"/"Battle"/"Special" })` | `INSERT INTO player_inventory (...) VALUES (...)` |
| **Baja** | `SavePlayer` (previo a re-guardar) y `DeleteSave` | `Where(i => i.PlayerId == playerId)...ForEach(Delete)` | `DELETE FROM player_inventory WHERE player_id = ?` |
| **Modificación** | — (Baja + Alta) | — | — |
| **Consulta** | `LoadPlayer` → `LoadInventory` | `Where(i => i.PlayerId == playerId)` | `SELECT * FROM player_inventory WHERE player_id = ?` |

### 5.5 Flags de historia (`active_flags`)

| Operación | Método | Código | SQL equivalente |
|-----------|--------|--------|-----------------|
| **Alta** | `SavePlayer` | `_db.Insert(new ActiveFlagRow { PlayerId = playerId, FlagName = flag.ToString() })` | `INSERT INTO active_flags (...) VALUES (...)` |
| **Baja** | `SavePlayer` (previo a re-guardar) y `DeleteSave` | `Where(f => f.PlayerId == playerId)...ForEach(Delete)` | `DELETE FROM active_flags WHERE player_id = ?` |
| **Modificación** | — (Baja + Alta) | — | — |
| **Consulta** | `LoadPlayer` | `Where(f => f.PlayerId == playerId)` | `SELECT * FROM active_flags WHERE player_id = ?` |

### 5.6 Cosméticos (`player_cosmetics`) y Diálogos (`dialogues`)

Ambas tablas **se crean** (`CreateTable<T>()`) pero **todavía no tienen ABM implementado** en el código — no hay Alta, Baja, Modificación ni Consulta sobre ellas. Son estructura prevista para funcionalidad futura:

- `player_cosmetics`: catálogo de cosméticos que el jugador posee (hoy el cosmético equipado se guarda inline en `player_poken.equipped_cosmetic`).
- `dialogues`: líneas de diálogo de NPCs condicionadas por flags (`required_flag` / `grants_flag`).

---

## 6. Resumen de queries por tabla

| Tabla | SELECT | INSERT | UPDATE | DELETE |
|-------|:------:|:------:|:------:|:------:|
| `player`             | ✔ (por id, existencia) | ✔ | ✔ | ✔ |
| `player_poken`       | ✔ (por player, ordenado) | ✔ | — | ✔ |
| `player_poken_moves` | ✔ (por poken)          | ✔ | — | ✔ |
| `player_inventory`   | ✔ (por player)         | ✔ | — | ✔ |
| `active_flags`       | ✔ (por player)         | ✔ | — | ✔ |
| `player_cosmetics`   | — (creada, sin uso aún) | — | — | — |
| `dialogues`          | — (creada, sin uso aún) | — | — | — |

> `player_cosmetics` y `dialogues` están creadas en el esquema pero todavía no tienen queries en el código: son estructura prevista para funcionalidad futura (cosméticos equipables y diálogos de NPCs condicionados por flags).
