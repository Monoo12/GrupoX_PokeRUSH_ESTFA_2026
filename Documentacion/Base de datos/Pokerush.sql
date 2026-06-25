CREATE TABLE player (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    money INTEGER DEFAULT 0
);

CREATE TABLE player_poken (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    player_id INTEGER NOT NULL,

    poken_definition_id TEXT NOT NULL,

    level INTEGER DEFAULT 1,
    current_xp INTEGER DEFAULT 0,
    current_hp INTEGER,

    status_effect TEXT DEFAULT 'None',

    slot_index INTEGER,
    equipped_cosmetic TEXT,

    FOREIGN KEY(player_id)
    REFERENCES player(id)
);

CREATE TABLE player_poken_moves (
    id INTEGER PRIMARY KEY AUTOINCREMENT,

    player_poken_id INTEGER NOT NULL,
    move_definition_id TEXT NOT NULL,

    current_pp INTEGER,

    FOREIGN KEY(player_poken_id)
    REFERENCES player_poken(id)
);

CREATE TABLE player_inventory (
    id INTEGER PRIMARY KEY AUTOINCREMENT,

    player_id INTEGER NOT NULL,

    item_definition_id TEXT NOT NULL,
    item_type TEXT NOT NULL,

    quantity INTEGER DEFAULT 1,

    FOREIGN KEY(player_id)
    REFERENCES player(id)
);

CREATE TABLE player_cosmetics (
    id INTEGER PRIMARY KEY AUTOINCREMENT,

    player_id INTEGER NOT NULL,
    cosmetic_definition_id TEXT NOT NULL,

    FOREIGN KEY(player_id)
    REFERENCES player(id)
);

CREATE TABLE active_flags (
    id INTEGER PRIMARY KEY AUTOINCREMENT,

    player_id INTEGER NOT NULL,
    flag_name TEXT NOT NULL,

    FOREIGN KEY(player_id)
    REFERENCES player(id)
);

CREATE TABLE dialogues (
    id INTEGER PRIMARY KEY AUTOINCREMENT,

    npc_id TEXT NOT NULL,
    line_index INTEGER NOT NULL,

    text TEXT NOT NULL,

    required_flag TEXT,
    grants_flag TEXT
);
