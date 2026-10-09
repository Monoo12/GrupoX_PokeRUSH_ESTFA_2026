-- =====================================================================
-- PokeRush - Base de datos de la partida (SQLite)
-- Version 1.4.0 - octubre 2026
--
-- Es el esquema que el juego crea de verdad en
--   Application.persistentDataPath/pokerush.db
-- (en Windows: AppData/LocalLow/LevelUP Creators/PokeRush/pokerush.db).
--
-- En tiempo de ejecucion las tablas las genera sqlite-net a partir de las
-- clases de Codigo/PokeRush/Assets/Scripts/Services/DatabaseRows.cs y
-- Menus/Settings/SettingsService.cs. Por eso tablas y columnas usan los
-- MISMOS nombres que esas clases: este .db se puede poner en lugar del
-- pokerush.db del juego y el juego lo abre.
--
-- Lo que sqlite-net no declara y aca si (claves foraneas, NOT NULL,
-- CHECK, UNIQUE, indices) documenta las reglas que hoy cumple el codigo
-- de SaveService. SQLite no exige las claves foraneas salvo con
-- PRAGMA foreign_keys = ON, y el juego no lo activa: los borrados en
-- cascada los hace SaveService a mano.
--
-- Cambios respecto del Pokerush.sql anterior:
--   player              + Gender, Debt, PendingBuffs; Id sin AUTOINCREMENT
--   player_poken        + IsShiny, BoxIndex (el PC)
--   player_poken_moves  + SlotIndex (orden de los movimientos)
--   settings            tabla nueva (opciones de la maquina)
-- =====================================================================

PRAGMA foreign_keys = ON;

-- ---------------------------------------------------------------------
-- JUGADOR. Una fila por slot de guardado (hoy siempre el 1).
-- ---------------------------------------------------------------------
CREATE TABLE player (
    -- Numero de slot de guardado. Lo asigna SavePlayer a mano: NO es
    -- AUTOINCREMENT (con autoincrement LoadPlayer(1) no encontraba la fila).
    Id            INTEGER PRIMARY KEY NOT NULL,
    Name          VARCHAR NOT NULL,
    -- Enum Genero como texto. NULL en guardados viejos = se lee como Varon.
    Gender        VARCHAR CHECK (Gender IN ('Varon', 'Mujer')),
    Money         INTEGER NOT NULL DEFAULT 0,
    -- Lo que el jugador le debe a la enfermeria del casino.
    Debt          INTEGER NOT NULL DEFAULT 0 CHECK (Debt >= 0),
    -- Mejoras de la ruleta de Martin para la proxima batalla, serializadas
    -- como "Ataque:1;Velocidad:2" (ver MejorasDeBatalla). NULL o '' = ninguna.
    PendingBuffs  VARCHAR
);

-- ---------------------------------------------------------------------
-- POKEN. Cada Pokemon del jugador, este en el equipo o en el PC.
-- ---------------------------------------------------------------------
CREATE TABLE player_poken (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    PlayerId           INTEGER NOT NULL,
    -- pokenName del PokenDefinition (ScriptableObject). No es FK de la base:
    -- lo resuelve DefinitionRepository al cargar.
    PokenDefinitionId  VARCHAR NOT NULL,
    Level              INTEGER NOT NULL DEFAULT 1 CHECK (Level BETWEEN 1 AND 100),
    CurrentXP          INTEGER NOT NULL DEFAULT 0 CHECK (CurrentXP >= 0),
    CurrentHP          INTEGER NOT NULL DEFAULT 0 CHECK (CurrentHP >= 0),
    -- Enum StatusEffect como texto.
    StatusEffect       VARCHAR NOT NULL DEFAULT 'None'
                       CHECK (StatusEffect IN ('None', 'Burned', 'Frozen', 'Paralyzed',
                                               'Poisoned', 'Asleep', 'Confused')),
    -- BoxIndex NULL: posicion en el equipo (0-5).
    -- BoxIndex con numero: lugar dentro de esa caja del PC.
    SlotIndex          INTEGER NOT NULL CHECK (SlotIndex >= 0),
    -- cosmeticName del cosmetico equipado (como maximo uno). NULL = ninguno.
    EquippedCosmetic   VARCHAR,
    -- IVs "hp,atk,def,spa,spd,spe" (ej: "31,12,0,5,20,31"). Es texto para
    -- distinguir un guardado viejo (NULL -> se regeneran) de un IV 0 legitimo.
    Ivs                VARCHAR,
    IsShiny            INTEGER NOT NULL DEFAULT 0 CHECK (IsShiny IN (0, 1)),
    -- NULL = en el equipo; un numero = caja del PC.
    BoxIndex           INTEGER CHECK (BoxIndex IS NULL OR BoxIndex >= 0),

    FOREIGN KEY (PlayerId) REFERENCES player (Id) ON DELETE CASCADE
);

CREATE INDEX ix_player_poken_player ON player_poken (PlayerId);

-- ---------------------------------------------------------------------
-- POKEN_MOVIMIENTO. Los movimientos que conoce cada Poken (hasta 4).
-- ---------------------------------------------------------------------
CREATE TABLE player_poken_moves (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    PlayerPokenId     INTEGER NOT NULL,
    -- moveName del MoveDefinition. Lo resuelve DefinitionRepository.
    MoveDefinitionId  VARCHAR NOT NULL,
    CurrentPP         INTEGER NOT NULL DEFAULT 0 CHECK (CurrentPP >= 0),
    -- Posicion del movimiento en la lista del Poken (el Summary deja
    -- reordenarlos y el orden tiene que sobrevivir el guardado).
    SlotIndex         INTEGER NOT NULL DEFAULT 0 CHECK (SlotIndex BETWEEN 0 AND 3),

    FOREIGN KEY (PlayerPokenId) REFERENCES player_poken (Id) ON DELETE CASCADE,
    UNIQUE (PlayerPokenId, SlotIndex)
);

-- ---------------------------------------------------------------------
-- INVENTARIO. Una fila por pila de items (curacion, combate o especial).
-- ---------------------------------------------------------------------
CREATE TABLE player_inventory (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    PlayerId          INTEGER NOT NULL,
    -- itemName del HealItem / BattleItem / SpecialItemDefinition.
    ItemDefinitionId  VARCHAR NOT NULL,
    -- Dice en que repositorio de definiciones buscar el item al cargar.
    ItemType          VARCHAR NOT NULL CHECK (ItemType IN ('Heal', 'Battle', 'Special')),
    Quantity          INTEGER NOT NULL DEFAULT 1 CHECK (Quantity >= 0),

    FOREIGN KEY (PlayerId) REFERENCES player (Id) ON DELETE CASCADE,
    UNIQUE (PlayerId, ItemType, ItemDefinitionId)
);

-- ---------------------------------------------------------------------
-- COSMETICO_EQUIPADO. Cosmeticos que tiene desbloqueados el jugador.
-- La tabla se crea pero la version 1.4.0 todavia no escribe en ella.
-- ---------------------------------------------------------------------
CREATE TABLE player_cosmetics (
    Id                    INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    PlayerId              INTEGER NOT NULL,
    CosmeticDefinitionId  VARCHAR NOT NULL,

    FOREIGN KEY (PlayerId) REFERENCES player (Id) ON DELETE CASCADE,
    UNIQUE (PlayerId, CosmeticDefinitionId)
);

-- ---------------------------------------------------------------------
-- FLAG. Hitos de la historia que el jugador ya cumplio.
-- ---------------------------------------------------------------------
CREATE TABLE active_flags (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    PlayerId  INTEGER NOT NULL,
    -- Nombre de un valor del enum StoryFlag. Si el enum ya no lo tiene,
    -- Enum.TryParse falla al cargar y la fila se ignora.
    FlagName  VARCHAR NOT NULL,

    FOREIGN KEY (PlayerId) REFERENCES player (Id) ON DELETE CASCADE,
    UNIQUE (PlayerId, FlagName)
);

-- ---------------------------------------------------------------------
-- NPC / DIALOGO. Lineas de dialogo de los NPC.
-- La tabla se crea pero queda vacia: los dialogos del juego viven en los
-- ScriptableObjects y en las escenas.
-- ---------------------------------------------------------------------
CREATE TABLE dialogues (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
    NpcId         VARCHAR NOT NULL,
    LineIndex     INTEGER NOT NULL CHECK (LineIndex >= 0),
    Text          VARCHAR NOT NULL,
    -- Nombres de StoryFlag; se comparan en codigo, no son FK.
    RequiredFlag  VARCHAR,
    GrantsFlag    VARCHAR,

    UNIQUE (NpcId, LineIndex)
);

-- ---------------------------------------------------------------------
-- OPCION. Volumen, video y controles, como pares clave/valor.
-- Es de la maquina, no de la partida: no tiene PlayerId, y borrar la
-- partida no la toca.
-- ---------------------------------------------------------------------
CREATE TABLE settings (
    Key    VARCHAR PRIMARY KEY NOT NULL,
    Value  VARCHAR
);


-- =====================================================================
-- DATOS DE EJEMPLO
-- Una partida a mitad de la Torre 1, tomada de una partida de prueba y
-- completada con un segundo Pokemon en el equipo y uno en el PC.
-- =====================================================================

INSERT INTO player (Id, Name, Gender, Money, Debt, PendingBuffs) VALUES
    (1, 'Mono', 'Varon', 1100, 0, NULL);

-- Equipo (BoxIndex NULL) y PC (caja 0, lugar 0).
INSERT INTO player_poken
    (Id, PlayerId, PokenDefinitionId, Level, CurrentXP, CurrentHP, StatusEffect,
     SlotIndex, EquippedCosmetic, Ivs, IsShiny, BoxIndex) VALUES
    (1, 1, 'Lucario',  42,   0, 117, 'None',     0, NULL, '17,23,8,4,2,14',    0, NULL),
    (2, 1, 'Greninja', 40, 350,  96, 'Poisoned', 1, NULL, '31,12,0,5,20,31',   0, NULL),
    (3, 1, 'Cosmog',   20,   0,  52, 'None',     0, NULL, '25,10,14,30,9,22',  1, 0);

INSERT INTO player_poken_moves (PlayerPokenId, MoveDefinitionId, CurrentPP, SlotIndex) VALUES
    (1, 'Esfera Aural',   20, 0),
    (1, 'Garra Metal',    35, 1),
    (1, 'Ataque Óseo',    10, 2),
    (1, 'Danza Espada',   20, 3),
    (2, 'Surf',           12, 0),
    (2, 'Viento Hielo',   15, 1),
    (2, 'Tumba Rocas',    15, 2),
    (2, 'Tóxico',          9, 3),
    (3, 'Salpicadura',    40, 0),
    (3, 'Teletransporte', 20, 1);

INSERT INTO player_inventory (PlayerId, ItemDefinitionId, ItemType, Quantity) VALUES
    (1, 'Poción',             'Heal',    2),
    (1, 'Superpoción',        'Heal',    1),
    (1, 'Carta anónima',      'Special', 1),
    (1, 'RaspBerry PY',       'Special', 1),
    (1, 'Transmisor de Tixi', 'Special', 1);

INSERT INTO active_flags (PlayerId, FlagName) VALUES
    (1, 'GameStarted'),
    (1, 'ReceivedStarterPoken'),
    (1, 'Tutorial_Despertar'),
    (1, 'Historia_CaminoContado'),
    (1, 'Historia_CruzPateado'),
    (1, 'Lobby_TixiPresentado'),
    (1, 'Historia_TixiTransmisor'),
    (1, 'Historia_DC_PantallasParpadearon'),
    (1, 'Historia_DC_TerminalLeida'),
    (1, 'Torre1_FirewallNodoA'),
    (1, 'Torre1_FirewallNodoB'),
    (1, 'Torre1_FirewallNodoC');

INSERT INTO settings (Key, Value) VALUES
    ('audio.music', '0.35'),
    ('audio.sfx',   '0.8'),
    ('video.ancho', '1920'),
    ('video.alto',  '1080'),
    ('video.modo',  '1');


-- =====================================================================
-- CONSULTAS DE EJEMPLO (las mismas que hace SaveService)
-- =====================================================================

-- Equipo del jugador, en orden:
--   SELECT * FROM player_poken WHERE PlayerId = 1 AND BoxIndex IS NULL ORDER BY SlotIndex;
-- PC del jugador:
--   SELECT * FROM player_poken WHERE PlayerId = 1 AND BoxIndex IS NOT NULL ORDER BY BoxIndex, SlotIndex;
-- Movimientos de un Poken, en orden:
--   SELECT * FROM player_poken_moves WHERE PlayerPokenId = 1 ORDER BY SlotIndex;
-- Hay partida guardada?
--   SELECT EXISTS (SELECT 1 FROM player WHERE Id = 1);
-- Borrar la partida (con foreign_keys = ON, la cascada borra el resto):
--   DELETE FROM player WHERE Id = 1;
