# ⚔️ Sistema de Combate e IA en PokeRush

Documento técnico del sistema de combate por turnos, las cuatro inteligencias
artificiales y el importador de datos de PokeAPI. Escrito para el equipo: explica
**cómo funciona** cada pieza y **por qué** se diseñó así, no solo qué hace.

> 🖥️ La capa visual (databoxes, menús LUCHAR/POKÉMON/MOCHILA/HUIR, fondos
> intercambiables y sprites) está documentada aparte en `HUD_De_Batalla.md`.

> **Resumen de la sesión (16-jul-2026):** se completaron las mecánicas de la saga
> (estados alterados, cambios de estadística, cambio de Pokémon, Forcejeo, efectos
> de movimientos, habilidades), se construyó una IA modular por niveles
> (salvaje → entrenador → líder → boss) y se pobló la base de datos con **435
> especies y 595 movimientos** en español desde PokeAPI.

---

## 🗺️ Cómo está organizado el combate

La idea central es **separar las reglas del combate de su presentación en pantalla**.
Antes, toda la lógica vivía dentro de una corrutina de `BattleController` mezclada con
las esperas y los mensajes de la UI. Eso hacía imposible testear el combate sin abrir
una escena de Unity y jugar a mano.

Ahora hay dos capas:

```
┌─────────────────────────────────────────────────────────────┐
│  BattleController (MonoBehaviour)  →  la "PRESENTACIÓN"       │
│  - Lee el teclado (input del jugador)                        │
│  - Le pide la acción a la IA                                  │
│  - REPRODUCE los eventos: actualiza el HUD y espera entre    │
│    mensajes. NO decide nada de las reglas.                    │
└───────────────────────────┬─────────────────────────────────┘
                            │ le pasa 2 acciones, recibe eventos
┌───────────────────────────▼─────────────────────────────────┐
│  TurnEngine (C# puro, sin Unity)  →  las "REGLAS"            │
│  - Resuelve el turno completo (orden, daño, estados...)      │
│  - Devuelve una List<BattleEvent> describiendo qué pasó      │
│  - Es testeable sin escena, con azar reproducible            │
└─────────────────────────────────────────────────────────────┘
```

### El flujo de un turno

1. El jugador aprieta una tecla → `BattleController` arma una **`BattleAction`**
   (usar movimiento, cambiar de Pokémon o Forcejeo).
2. Le pide su acción a la IA rival con `_enemyAI.ChooseAction(...)`.
3. Pasa ambas acciones a `TurnEngine.ResolveTurn(...)`, que:
   - Resuelve primero los **cambios de Pokémon** (siempre van antes de atacar).
   - Ordena los movimientos por **prioridad → velocidad efectiva → azar**.
   - Ejecuta cada movimiento (chequeos de estado, daño, efectos, habilidades).
   - Aplica el **daño residual** de fin de turno (quemadura, veneno) y habilidades
     como Impulso.
   - Maneja los **debilitados**: reparte experiencia y busca reemplazos.
   - Devuelve la **lista de eventos** (`BattleEvent`) de todo lo que ocurrió.
4. `BattleController` recorre esa lista, actualiza las barras de HP y muestra cada
   mensaje con su pausa.

### Piezas clave (todas en `Assets/Scripts/Core/Battle/`)

| Archivo | Qué es |
|---|---|
| `TurnEngine.cs` | El motor. Resuelve el turno y emite eventos. |
| `BattleAction.cs` | La acción de un lado: `UseMove`, `SwitchPoken` (y `UseItem`/`Flee` a futuro). |
| `BattleEvent.cs` | Un paso visible del combate (un mensaje, un cambio de HP, un debilitado...). |
| `BattlePokenState.cs` | El estado **volátil** del Pokémon activo (ver abajo). |
| `StatusEffects.cs` | Estados alterados: aplicarlos, chequearlos y el daño por turno. |
| `MoveEffectExecutor.cs` | Efectos secundarios de un movimiento (subir/bajar stats, retroceso, drenaje...). |
| `AbilityEffects.cs` | Las habilidades (Intimidación, Levitación, Robustez...). |
| `RewardCalculator.cs` | Reparto de experiencia entre los participantes. |

### 🔑 Estado persistente vs. estado volátil

Una distinción importante del diseño:

- **`PokenModel`** guarda lo que **sobrevive entre combates**: nivel, HP actual, PP,
  estado alterado (quemado, envenenado...). Esto se persiste en el save.
- **`BattlePokenState`** guarda lo que **solo dura este combate**: los cambios de
  estadística (±6 stages), la confusión, el retroceso, los turnos de sueño. Se crea
  de cero cada vez que un Pokémon entra al campo y se descarta al cambiarlo. **Nunca
  se guarda.**

Por eso la confusión no persiste (es volátil) pero la quemadura sí (vive en `PokenModel`).

---

## 🎲 Azar reproducible (`IBattleRng`)

El combate no usa `UnityEngine.Random` directo. Usa una interfaz **`IBattleRng`** con
dos implementaciones:

- **`UnityRng`** → el azar normal del juego.
- **`SeededRng`** → azar con semilla fija: la **misma semilla produce siempre el mismo
  resultado**.

Esto permite que los tests automáticos (`PokeRush > Correr smoke tests`) verifiquen
comportamientos exactos, y que la IA pueda simular resultados sin depender del motor de
Unity. Todo el combate recibe el RNG "desde afuera" en vez de invocarlo internamente.

---

## 🤖 Las cuatro inteligencias artificiales

Todas las IAs comparten una interfaz común, **`IBattleAI`** (en `Core/AI/`):

```csharp
public interface IBattleAI
{
    BattleAction ChooseAction(AiContext context, IBattleRng rng); // qué hacer este turno
    int ChooseReplacement(AiContext context);                      // a quién enviar al caer
}
```

Esto es lo que pide la consigna: un sistema **modular basado en comportamientos**, donde
todas comparten la misma interfaz para poder mejorarlas sin tocar el resto.

### 🚫 Sin trampas: la regla de oro

La consigna insiste en que la IA sea **creíble antes que tramposa**. Para lograrlo, la IA
solo recibe **información que un entrenador humano podría ver en pantalla**, a través de
`AiContext`:

- ✅ Su propio equipo completo (movimientos, PP, HP exactos).
- ✅ Del rival: especie, tipos, nivel, **porcentaje** de HP visible, estado alterado y
  cambios de estadística visibles.
- ❌ **Nunca** ve los movimientos exactos del jugador ni los Pokémon que tiene en la banca.

Cuando calcula "cuánto daño haría este ataque", usa los tipos del rival (que son
visibles), igual que un jugador que sabe que Agua le pega fuerte a Fuego.

### El cerebro compartido: `MoveScorer`

En vez de elegir un movimiento al azar, las IAs competentes **le ponen un puntaje a cada
opción** y eligen la de puntaje más alto. El puntaje sale de `MoveScorer.cs` y combina
varios factores, cada uno con un **peso configurable** por nivel de IA:

```
Movimiento de daño:
  Si no tiene efecto (inmunidad de tipo o habilidad)  →  puntaje 0
  puntaje = W_daño × (daño esperado / HP del rival) × probabilidad de acierto
  Si el daño alcanza para debilitar                   →  + W_KO
  Si tiene STAB (bonus de tipo)                        →  + 0.05 (desempate)

Movimiento de estado:
  Estado que el rival ya tiene o es inmune             →  puntaje 0
  Aplicar estado (parálisis, quemadura...)             →  W_estado × HP% del rival
  Subir stats propias (setup)                          →  W_setup, solo con HP alto
  Curarse                                              →  W_cura × (cuánto HP falta)
```

El "daño esperado" lo calcula `DamageCalculator.CalculateExpected(...)`: una versión
**determinista** de la fórmula de daño (sin crítico, con el promedio del factor aleatorio)
para que la IA pueda comparar movimientos sin tirar los dados de verdad.

### Los perfiles (`AIProfileDefinition`)

Cada nivel de IA es un **ScriptableObject** con los pesos y comportamientos. Cambiar estos
números **re-balancea la IA sin tocar código**. Los cinco perfiles generados
(`Resources/Definitions/AIProfiles/`):

| Parámetro | Wild | Trainer | GymLeader | Elite | FinalBoss |
|---|---|---|---|---|---|
| **Aleatoriedad** (torpeza) | 0.50 | 0.20 | 0.10 | 0.05 | 0.02 |
| **Evita ataques inútiles** | No | Sí | Sí | Sí | Sí |
| **Peso del daño** | 1.0 | 1.0 | 1.0 | 1.0 | 1.0 |
| **Peso del KO** | 0 | 0.6 | 0.8 | 0.9 | 1.0 |
| **Peso de estados** | 0 | 0.35 | 0.45 | 0.5 | 0.5 |
| **Peso de setup** | 0 | 0 | 0.5 | 0.6 | 0.6 |
| **Peso de curación** | 0 | 0 | 0.4 | 0.5 | 0.5 |
| **¿Cambia de Pokémon?** | No | No | Sí | Sí | Sí |
| **Umbral de cambio** | — | — | 0.5 | 0.35 | 0.25 |

Cuanto más bajo el umbral de cambio, más seguido cambia de Pokémon.

---

### 1️⃣ `WildPokemonAI` — Pokémon salvaje

**La más simple, como en los juegos oficiales.**

Elige un movimiento **casi al azar**, pero con un **sesgo suave** hacia los efectivos: el
peso de cada movimiento de daño es `1 + su efectividad`. Es decir, un ataque supereficaz
(×2) pesa 3, mientras que uno neutro (×1) pesa 2 — sale elegido más seguido, pero **no
siempre**.

- No evalúa estados alterados, no hace setup, no planifica.
- No cambia de Pokémon (un salvaje es uno solo).
- Se siente como un salvaje: impredecible pero no tonto del todo.

> **Ejemplo:** un Charmander salvaje contra tu Bulbasaur elige Ascuas (Fuego, ×2 contra
> Planta) más o menos el 60% de las veces, pero a veces tira Placaje igual.

---

### 2️⃣ `TrainerAI` — Entrenador común

**La IA base de la mayoría de los entrenadores del juego.**

Le pone puntaje a **todos** sus movimientos con `MoveScorer` y elige el mejor. Cumple lo
que pide la consigna para entrenadores comunes:

- ✅ Evalúa la efectividad de tipos.
- ✅ **Nunca usa ataques sin efecto** (los descarta con puntaje 0).
- ✅ Prioriza los supereficaces (más daño = más puntaje).
- ✅ Usa movimientos de estado cuando conviene (parálisis, quemadura...).
- ✅ Considera la posibilidad de debilitar al rival este turno.

Tiene una **"torpeza" configurable** (`randomness = 0.20`): el 20% de las veces elige al
azar en lugar del mejor movimiento. Esto lo hace sentir humano, no una máquina perfecta.
Ante empates de puntaje, elige al azar entre los empatados para no ser predecible.

- ❌ **No cambia de Pokémon** voluntariamente (eso lo hacen los líderes).
- Al caer un Pokémon, envía al **siguiente con vida** (como un entrenador común).

---

### 3️⃣ `GymLeaderAI` — Líder de gimnasio / Elite

**Hereda todo lo de `TrainerAI` y le suma tres capas estratégicas**, evaluadas en este
orden de prioridad:

1. **🎯 Remate garantizado.** Si algún movimiento asegura el KO (el daño esperado alcanza
   y el ataque no puede fallar), lo usa **siempre**, sin dudarlo. Entre varios remates
   posibles, elige el de mayor precisión. Esto replica a un líder que no perdona a un
   rival debilitado.

2. **🔄 Cambio estratégico.** Si su mejor movimiento puntúa bajo (está en mala posición),
   evalúa a sus suplentes. Calcula un **"matchup"** de cada candidato:

   ```
   matchup = (mi mejor efectividad ofensiva contra el rival)
           − (la mejor efectividad del rival contra mí)
   ```

   Si un suplente mejora el matchup por encima del umbral del perfil, **cambia**. Usa solo
   información legítima: los tipos visibles del rival.

3. **📊 Scoring normal** con setup y curación activados (los perfiles de líder tienen esos
   pesos > 0, a diferencia del entrenador común). Un líder con HP alto puede usar Danza
   Espada antes de atacar; uno lastimado puede curarse.

Al caer un Pokémon, **no envía al siguiente de la lista**: envía al que tenga **mejor
matchup** contra el rival actual.

> **Ejemplo:** un líder con un Pokémon de Fuego enfrentando a tu tanque de Agua, y con un
> Pokémon de Planta en la banca, va a **cambiar al de Planta** en vez de seguir golpeando
> en desventaja.

---

### 4️⃣ `FinalBossAI` — Bosses finales

**Por ahora hereda todo el comportamiento de `GymLeaderAI`** con el perfil más agresivo
(casi sin torpeza, máximo peso al KO, cambios frecuentes).

Es un **gancho preparado** para los dos bosses finales del juego. La clase existe y está
documentada con lo que se le va a agregar cuando esos bosses se diseñen:

- Predicción parcial de la respuesta del jugador (asumir que atacará con su STAB y
  cambiar **antes** de recibir el golpe).
- Lookahead de 2 turnos calculando daño en ambas direcciones.
- Secuencias de setup planificadas (ej. Danza Espada ×2 → remate) cuando el matchup
  defensivo lo permite.

Todo eso se implementa sobrescribiendo `ChooseAction` en `FinalBossAI.cs`, **sin tocar el
resto del sistema**.

---

### 🏭 Cómo se elige la IA de cada enemigo

La fábrica **`BattleAIFactory`** mira el `AIProfileDefinition` asignado al enemigo y crea
la clase correspondiente. Cada `EnemyDefinition` y `BossDefinition` tiene ahora un campo
**`aiProfile`** en el Inspector:

- Le arrastrás uno de los perfiles de `Resources/Definitions/AIProfiles/`.
- Si lo dejás **vacío**, usa `TrainerAI` por defecto (o `WildPokemonAI` si es un salvaje).

---

## 🧬 Mecánicas de combate implementadas

Todas siguen las reglas de la saga principal.

### Estados alterados (`StatusEffects.cs`)

| Estado | Efecto |
|---|---|
| **Quemadura** | Pierde 1/16 del HP máximo por turno. El **ataque físico baja a la mitad**. |
| **Veneno** | Pierde 1/8 del HP máximo por turno. |
| **Parálisis** | 25% de perder el turno. La **velocidad baja a la mitad**. |
| **Sueño** | No puede actuar durante 1-3 turnos (se despierta solo). |
| **Congelación** | No puede actuar; 20% de descongelarse por turno. Un golpe de Fuego lo descongela. |
| **Confusión** | 33% de golpearse a sí mismo (daño físico sin tipo). Dura 1-4 turnos. **Es volátil.** |

**Inmunidades por tipo:** los de Fuego no se queman, los de tipo Eléctrico no se paralizan,
Veneno y Acero no se envenenan, Hielo no se congela.

### Cambios de estadística (stages -6 a +6)

Los movimientos como Danza Espada (+2 Ataque) o Malicioso (-1 Ataque rival) modifican
"stages" que van de -6 a +6, con los **multiplicadores oficiales** de la saga
(`StatStageTable.cs`). También afectan a la **precisión y la evasión**.

### Efectos de movimientos (`MoveEffectExecutor.cs`)

- **Multi-golpe** (2-5 veces, con las probabilidades oficiales 35/35/15/15).
- **Retroceso** (recoil): el usuario sufre parte del daño (ej. Doble Filo).
- **Drenaje**: el usuario se cura con parte del daño (ej. Absorber).
- **Curación** directa (ej. Recuperación cura 50% del HP máximo).
- **Flinch**: probabilidad de que el rival pierda su turno.
- **Críticos por movimiento**: algunos tienen ratio de crítico aumentado.

### Cambio manual de Pokémon

El jugador puede **cambiar de Pokémon como acción de turno** (consume el turno: el rival
ataca al que entra). En la escena de prueba se hace con las teclas **F1-F6**.

### Forcejeo (Struggle)

Cuando un Pokémon **se queda sin PP en todos sus movimientos**, usa Forcejeo: potencia 50,
sin tipo (pega neutro a todo), nunca falla, y el usuario sufre 25% de retroceso.

### Habilidades básicas (`AbilityEffects.cs`)

Implementadas con un sistema de "ganchos" (un `switch` por momento del combate), fácil de
extender. Las incluidas:

| Habilidad | Efecto |
|---|---|
| **Intimidación** | Baja el Ataque del rival al entrar al campo. |
| **Levitación** | Inmune a movimientos de tipo Tierra. |
| **Absorber Agua / Electricidad** | Inmune a ese tipo; se cura 25% del HP máximo. |
| **Absorber Fuego** | Inmune a Fuego; potencia sus propios ataques de Fuego. |
| **Robustez** | Con HP lleno, aguanta cualquier golpe letal con 1 HP. |
| **Agallas** | Con estado alterado: Ataque ×1.5 e ignora la reducción por quemadura. |
| **Impulso** | +1 de Velocidad al final de cada turno. |
| **Mar Llamas / Torrente / Espesura / Enjambre** | Con HP < 1/3, potencian su tipo ×1.5. |
| **Electricidad Estática / Punto Tóxico / Cuerpo Llama** | 30% de afectar al atacante por contacto. |
| **Piel Tosca** | El atacante pierde 1/8 del HP máximo por contacto. |

Las especies cuya habilidad **no** está en esta lista muestran el nombre en la ficha pero
no tienen efecto mecánico todavía. Para agregar una nueva: se suma al enum `AbilityId` y se
le da efecto en `AbilityEffects.cs`.

---

## 🌦️ Climas y efectos de campo (`FieldEffects.cs`)

El estado del campo vive en `BattleModel` (clima + terreno + un `SideState` por
lado) y toda su lógica en `Core/Battle/FieldEffects.cs` (puro). Los movimientos
que los generan tienen los campos `weatherEffect`/`fieldEffect` en
`MoveDefinition` — el importador los mapea por nombre (Danza Lluvia, Reflejo,
Trampa Rocas...) y los marca implementados.

**Climas** (5 turnos, mensaje al empezar y al terminar, overlay visual):

| Clima | Efecto |
|---|---|
| **Lluvia** | Agua ×1.5, Fuego ×0.5 |
| **Sol** | Fuego ×1.5, Agua ×0.5 |
| **Tormenta de Arena** | 1/16 por turno a los no Roca/Tierra/Acero; Def. Esp. ×1.5 a los Roca |
| **Granizo** | 1/16 por turno a los no Hielo |

**Pantallas** (5 turnos, protegen el lado del usuario, los críticos las ignoran):
Reflejo (físico ×0.5), Pantalla de Luz (especial ×0.5), Velo Aurora (ambos,
solo activable con granizo).

**Trampas de entrada** (se colocan en el lado rival y golpean a cada Pokémon
que entra): Trampa Rocas (daño según efectividad de Roca), Púas (1/8→1/6→1/4
según capas, solo a los que tocan el suelo), Púas Tóxicas (envenenan; los tipo
Veneno las absorben), Red Viscosa (-1 Velocidad).

**Terrenos** (5 turnos, afectan a los Pokémon "en el suelo" — no voladores ni
Levitación): Eléctrico (×1.3 Eléctrico, nadie se duerme), Psíquico (×1.3),
Hierba (×1.3 Planta, cura 1/16 por turno), Nebuloso (Dragón ×0.5, sin estados
nuevos).

La IA los conoce: `CalculateExpected` recibe el campo (un líder valora más su
Surf bajo lluvia) y los movimientos de campo puntúan como jugada de setup solo
si aportan algo nuevo (`FieldEffects.IsRedundant`). **No implementado aún**:
Sustituto, bloqueo de prioridad del Campo Psíquico, precisión perfecta de
Trueno/Ventisca bajo su clima, y habilidades de clima (Llovizna/Sequía).

---

## 💾 Datos: IVs, experiencia y learnsets

### IVs (valores individuales)

Cada Pokémon ahora tiene **IVs** (0-31 por estadística), como en los juegos oficiales.
Hacen que dos Pikachu del mismo nivel tengan stats ligeramente distintos. La fórmula pasó
de la simplificada a la **oficial**:

```
HP    = ((2 × base + IV) × nivel) / 100 + nivel + 10
Otras = ((2 × base + IV) × nivel) / 100 + 5
```

Los IVs se generan al azar al crear el Pokémon y **se guardan en la partida** (columna
`Ivs` en la base de datos SQLite). Las partidas viejas que no tienen esa columna
**regeneran IVs automáticamente** al cargar, sin romperse.

### Experiencia y subida de nivel

Al derrotar a un Pokémon rival, la experiencia se reparte entre los **participantes** (los
Pokémon del jugador que enfrentaron a ese rival):

```
XP = XP_base_del_rival × nivel_del_rival / 7
```

Al subir de nivel, el Pokémon **aprende automáticamente** los movimientos de su learnset
correspondientes a ese nivel (si tiene menos de 4). Si ya tiene 4, por ahora solo avisa
(el flujo para reemplazar un movimiento es parte de la UI final).

### Learnsets por nivel

Cada especie tiene un **learnset** (`LearnsetEntry[]`): qué movimiento aprende y a qué
nivel. Al crear un Pokémon, se le asignan los **4 movimientos de mayor nivel** que ya puede
conocer.

---

## 🌐 Importador de PokeAPI

Para no cargar 435 Pokémon a mano, hay una **herramienta de Editor** que descarga los datos
oficiales desde [PokeAPI](https://pokeapi.co) y genera los ScriptableObjects.

### Cómo se usa

`PokeRush > Importador PokeAPI` abre una ventana con tres botones: descargar el cache,
generar los assets, o hacer ambos. **Ya se corrió**: hay 435 especies y 595 movimientos.

### Qué genera

- **Especies** (`PokenDefinition`): nombre en **español**, número de Pokédex, tipos, stats
  base, learnset por nivel, habilidad y XP base.
- **Movimientos** (`MoveDefinition`): nombre en español, tipo, categoría, potencia,
  precisión, PP, prioridad, efectos secundarios (estados, cambios de stats, retroceso...).

### Detalles de diseño

- **Lista editable**: qué especies importar se define en `Tools/PokeApiCache/species_selection.json`
  por número de Pokédex, agrupadas (iniciales, legendarios, míticos, semilegendarios, meta
  competitivo). Con `expandEvolutionLines: true`, basta listar una especie de cada línea
  evolutiva y el importador trae la cadena completa.
- **Idempotente**: se puede volver a correr sin crear duplicados. Actualiza los assets
  existentes y **preserva los sprites** asignados a mano.
- **Cache en el repo**: los JSON crudos quedan en `Tools/PokeApiCache/` (fuera de `Assets/`)
  para regenerar sin internet y ver los cambios en los diffs.
- **Movimientos complejos**: los que tienen efectos que aún no se implementan (dos turnos,
  trampas, KO fulminante...) se marcan como `DamageOnly` (funcionan como daño simple) o
  `NotImplemented` (excluidos de los learnsets, pero el asset queda para el futuro).

---

## ✅ Verificación

`PokeRush > Correr smoke tests` corre 20 pruebas automáticas con azar reproducible que
validan las fórmulas, los estados, las habilidades y el comportamiento de cada IA. La
consola debe decir **"TODOS OK"**.

Para probar el combate a mano: `PokeRush > Armar combate de prueba` genera una escena lista
con todo cableado. En Play:

| Tecla | Acción |
|---|---|
| **1-4** | Usar movimiento |
| **F1-F6** | Cambiar de Pokémon |
| **B** | Reiniciar combate contra el rival |
| **W** | Combate contra un Pokémon salvaje |

---

## 📁 Mapa de archivos

```
Assets/Scripts/Core/
├── Stat.cs, AbilityId.cs, IvSet.cs          (enums y datos base)
├── IBattleRng.cs                             (azar inyectable)
├── MoveImplementation.cs                     (Full / DamageOnly / NotImplemented)
├── DamageCalculator.cs                       (fórmula de daño + estimación para la IA)
├── Battle/
│   ├── TurnEngine.cs                         (el motor de turnos)
│   ├── BattleAction.cs, BattleEvent.cs       (acciones y eventos)
│   ├── BattlePokenState.cs                   (estado volátil)
│   ├── StatusEffects.cs                       (estados alterados)
│   ├── MoveEffectExecutor.cs                  (efectos de movimientos)
│   ├── AbilityEffects.cs                      (habilidades)
│   ├── StatStageTable.cs                      (multiplicadores de stages)
│   ├── RewardCalculator.cs                    (experiencia)
│   └── BattleOutcome.cs                       (resultado del combate)
└── AI/
    ├── IBattleAI.cs, AiContext.cs             (interfaz e información legítima)
    ├── MoveScorer.cs                          (el cerebro compartido)
    ├── BattleAIFactory.cs                     (elige la IA según el perfil)
    ├── WildPokemonAI.cs                       (salvaje)
    ├── TrainerAI.cs                           (entrenador común)
    ├── GymLeaderAI.cs                         (líder / elite)
    └── FinalBossAI.cs                         (bosses finales)

Assets/Scripts/Data/
├── AIProfileDefinition.cs                     (perfil de IA configurable)
├── WildEncounterDefinition.cs                 (encuentro salvaje)
├── LearnsetEntry.cs, StatChangeEntry.cs       (datos serializables)
└── MoveDefinition.cs, PokenDefinition.cs      (ampliados con los campos nuevos)

Assets/Editor/
├── PokeApi/                                   (importador de PokeAPI)
├── PokeRushSmokeTests.cs                      (20 tests automáticos)
└── BattleTestSceneBuilder.cs                  (arma la escena de prueba)
```
