# 🖥️ HUD de Batalla clásico en PokeRush

Documento técnico del HUD de combate estilo saga clásica: databoxes, menú de
comandos (LUCHAR / POKÉMON / MOCHILA / HUIR), menú de movimientos con panel de
PP/TIPO, menú de equipo, fondos intercambiables y la asignación automática de
sprites. Complementa a `Sistema_De_Combate_Y_IA.md` (que explica las reglas
del combate y la IA — este documento explica la **capa visual e interactiva**).

> **Resumen:** el HUD usa los gráficos originales del equipo de diseño
> (`Diseño/Graphics/UI/Battle/` y `Diseño/Graphics/Battlebacks/`, pack estilo
> Pokémon Essentials). Todo se arma con un click desde
> `PokeRush > Armar combate de prueba` — nadie tiene que cablear nada a mano.

---

## 🧭 La idea general

El principio es el mismo que en el resto del combate: **la vista no decide
reglas**. El flujo completo es:

```
┌──────────────────────────────────────────────────────────────┐
│  BattleHudView (la VISTA)                                     │
│  - Dibuja databoxes, menús y mensajes                          │
│  - Cuando el jugador hace click, dispara un EVENTO:            │
│      MoveChosen(2)  /  SwitchChosen(4)  /  FleeChosen  ...     │
│  - NO sabe qué es un turno ni cuánto daño hace nada            │
└──────────────────────────┬───────────────────────────────────┘
                           │ eventos C# (Action)
┌──────────────────────────▼───────────────────────────────────┐
│  BattleController (el ORQUESTADOR)                             │
│  - Se suscribe a los eventos del HUD en Awake()                │
│  - Convierte cada evento en una BattleAction                   │
│  - Se la pasa al TurnEngine y reproduce los BattleEvents       │
└──────────────────────────────────────────────────────────────┘
```

Gracias a esto, el mismo `BattleController` funciona igual con teclado
(1-4, F1-F6) que con los botones del mouse: ambos caminos terminan llamando a
`SelectMove(i)` / `SelectSwitch(i)` / `SelectFlee()`.

---

## 📄 `BattleHudView.cs` — la vista del combate

**Ubicación:** `Assets/Scripts/Views/BattleHudView.cs`

### Los eventos (el contrato con el controller)

```csharp
public event Action<int> MoveChosen;    // eligió el movimiento i
public event Action<int> SwitchChosen;  // eligió cambiar al Pokémon i del equipo
public event Action FleeChosen;         // apretó HUIR
public event Action BagChosen;          // apretó MOCHILA
```

`BattleController.Awake()` se suscribe a los cuatro. La vista **nunca** llama
al controller directamente — así se puede reemplazar el HUD entero sin tocar
la lógica (por ejemplo, cuando se haga la versión final con otro estilo).

### La API pública (lo que usa el controller)

| Método | Cuándo se llama |
|---|---|
| `Show()` / `Hide()` | Al empezar / terminar el combate. |
| `UpdateHud(BattleModel)` | Cada vez que cambia HP/estado/Pokémon activo. |
| `ShowMessage(string)` | Cada mensaje del combate ("¡Es súper efectivo!"). |
| `ShowCommandMenu(BattleModel)` | Al empezar el turno del jugador: muestra los 4 comandos y la pregunta "¿Qué hará X?". |
| `HideMenus()` | Mientras se resuelve el turno (los menús no deben estar). |

### Los tres menús internos

1. **Comandos** (`commandPanel`) — los 4 botones con el texto horneado en el
   sprite (LUCHAR/POKÉMON/MOCHILA/HUIR). Al pasar el mouse cambian al estado
   iluminado usando `Button.spriteState` con transición `SpriteSwap` — no hay
   código de hover, lo hace el propio `Button` de Unity.
2. **Lucha** (`fightPanel`) — hasta 4 botones de movimiento. Cada botón toma su
   **fondo según el tipo del movimiento** (Fuego rojo, Agua azul...) desde la
   tabla `moveSpritesByType`, y el nombre se escribe encima con un `Text`.
   El panel lateral muestra **PP actual/máximo** y el **ícono de TIPO** del
   movimiento bajo el puntero, y a su izquierda **`PanelDatos`** (playtest del
   4-oct) suma la **categoría** (físico / especial / estado, de
   `Graphics/UI/category.png`), la **potencia** ("POT 80", o "POT ---" si es de
   estado) y la **precisión** ("PRE 95", o "PRE ---" si nunca falla). Lo arma
   `PokeRush > Combate > Agregar panel de potencia y precision`
   (`PanelDeDatosDelMovimientoBuilder`) sobre el prefab `SistemaDeCombate`; las
   escenas que tienen una copia suelta y no la instancia del prefab
   (`CombatePrueba`, `battleTry`, solo de prueba) no lo tienen. El hover está implementado con un `EventTrigger`
   agregado por código en `AddHoverHandler`. Si ningún movimiento tiene PP,
   abrir este menú dispara directamente `MoveChosen(0)` y el controller lo
   convierte en Forcejeo.
3. **Equipo** (`teamPanel`) — overlay oscuro con los 6 Pokémon: nombre, PS, y
   deshabilitados el activo (marcado con ►) y los debilitados ("DEBILITADO").

### Las tablas de sprites por enum

La vista no sabe recortar hojas: recibe los sprites ya listos en arrays
serializados **indexados por el valor del enum**:

```csharp
[SerializeField] private Sprite[] moveSpritesByType;         // índice = (int)PokenType
[SerializeField] private Sprite[] moveSelectedSpritesByType; // ídem, estado iluminado
[SerializeField] private Sprite[] typeIconsByType;           // ídem, ícono de tipo
[SerializeField] private Sprite[] statusIconsByEffect;       // índice = (int)StatusEffect
```

El builder los llena una vez; en runtime buscar un sprite es un acceso a array
(`table[(int)type]`), sin diccionarios ni strings.

### Detalles visuales

- **Barra de HP**: `Image` en modo `Filled` horizontal. El color se decide en
  `UpdateHpFill`: verde > 50%, amarillo > 20%, rojo debajo — los umbrales de la
  saga.
- **Barra de EXP**: usa `PokenModel.XpProgress` (propiedad nueva que devuelve
  0-1 hacia el próximo nivel según la tabla de experiencia).
- **Ícono de estado**: se enciende solo cuando el Pokémon tiene un estado
  persistente (la confusión es volátil y no tiene ícono, igual que en la saga).

---

## ✂️ `BattleUiAssetImporter.cs` — copiar y recortar los gráficos

**Ubicación:** `Assets/Editor/BattleUiAssetImporter.cs` (solo Editor)

Unity solo puede referenciar imágenes que estén dentro de `Assets/`, y los
gráficos del equipo viven en `Diseño/Graphics/` (fuera del proyecto Unity).
Este script hace el puente:

1. **Copia** lo necesario a `Assets/Sprites/UI/Battle/` y los ~100 fondos a
   `Assets/Sprites/Battlebacks/`. Idempotente: no recopia lo que ya está.
2. **Configura** cada textura como Sprite pixel-art (filtro Point, sin
   compresión, sin mipmaps) — si no, los píxeles se ven borrosos.
3. **Recorta las hojas de sprites** en grillas con `SliceGrid`:

| Hoja | Grilla | Contenido |
|---|---|---|
| `cursor_command.png` | 130×46, 12 filas × 2 col | Botones de comando (col 0 = normal, col 1 = iluminado). Filas que usamos: 0 LUCHAR, 1 POKÉMON, 2 MOCHILA, 3 HUIR, 9 ATRÁS. |
| `cursor_fight.png` | 192×46, 19 filas × 2 col | Botones de movimiento, **una fila por tipo**. |
| `icon_types.png` | 96×32, 19 filas | Íconos de tipo (panel TIPO). |
| `icon_statuses.png` | 44×16, 6 filas | Etiquetas de estado (SLP/PSN/BRN/PAR/FRZ). |

Cada recorte se nombra `prefijo_fila_columna` (ej: `move_10_1` = botón de
movimiento tipo Fuego iluminado), y `LoadSlice(...)` los recupera por nombre.

### 🔑 El mapa de tipos (importante si se agregan gráficos)

Las hojas vienen del ecosistema Essentials, que ordena los tipos **distinto**
que nuestro enum `PokenType`. El diccionario `TypeRow` traduce:

```
Normal→0  Fighting→1  Flying→2  Poison→3  Ground→4  Rock→5  Bug→6
Ghost→7   Steel→8    (9 = tipo "???", no se usa)   Fire→10  Water→11
Grass→12  Electric→13  Psychic→14  Ice→15  Dragon→16  Dark→17  Fairy→18
```

Lo mismo con `StatusRow` para los estados (orden Essentials: SLP, PSN, BRN,
PAR, FRZ). **Si un botón sale del color equivocado, el error está en estos
mapas** — es el único lugar donde se traduce.

### Truco de medición

Las posiciones exactas de las ranuras de HP dentro de los databoxes se midieron
ampliando los PNG 4x: en `databox_normal.png` (jugador, 260×84) la ranura está
en el píxel (136, 40) y mide 96×6; en `databox_normal_foe.png` (rival, 260×70)
está en (118, 40). La barra de EXP del jugador va en (38, 72) y mide 192×4.
Esos números están en el builder — si diseño cambia los databoxes, hay que
volver a medirlos.

---

## 🏗️ `BattleTestSceneBuilder.cs` — armar la escena con un click

**Ubicación:** `Assets/Editor/BattleTestSceneBuilder.cs` (solo Editor)
**Menú:** `PokeRush > Armar combate de prueba`

Reconstruye `Assets/Scenes/CombatePrueba.unity` completa. En orden:

1. Genera dependencias si faltan (perfiles de IA, tabla de XP) e importa los
   gráficos de UI (llama a `BattleUiAssetImporter`).
2. Crea/actualiza los assets de prueba con su **fondo asignado**:
   `EntrenadorDePrueba` (Bosque), `BossDePrueba` (Gimnasio 1),
   `SalvajeDePrueba` (Pradera).
3. Construye la escena: cámara, fondo, los dos Pokémon, `EventSystem` y el HUD.
4. Cablea todos los campos privados (`[SerializeField]`) por código usando
   `SerializedObject` — por eso el equipo no tiene que arrastrar nada.

### Decisiones que conviene conocer

- **Canvas a 512×384** (la resolución nativa de los gráficos del pack), con
  `CanvasScaler` en modo *Scale With Screen Size* ajustando por altura. Esto
  hace que **las coordenadas del builder sean píxeles del mockup del
  diseñador**: si diseño dice "el databox va en (0, 24)", ese número se usa
  tal cual. En pantallas 16:9 el HUD escala parejo y sin deformarse.
- **Cámara ortográfica con size 1.92**: es la mitad de la altura del fondo
  (384 px / 2 / 100 píxeles-por-unidad), así el fondo llena la vista de forma
  exacta.
- **`EventSystem` con `InputSystemUIInputModule`**: el proyecto usa el Input
  System nuevo; con el módulo viejo (`StandaloneInputModule`) los botones no
  responderían. Si algún día los clicks "no hacen nada", revisar esto primero.
- **⚠️ La escena se crea ANTES de cargar assets.** Lección aprendida a los
  golpes: `EditorSceneManager.NewScene(..., Single)` descarga de memoria los
  assets cargados hasta ese momento, y las referencias tomadas antes se
  serializan como `null` (el bug del primer intento, donde el BattleTester
  quedó sin equipo). Cualquier builder futuro debe respetar este orden.

---

## 🌄 Fondos de batalla intercambiables

Cada enemigo define su propio escenario con el campo nuevo
**`battleBackground`** en `EnemyDefinition`, `BossDefinition` y
`WildEncounterDefinition`:

1. Seleccionar el asset del enemigo en el Inspector.
2. Arrastrar cualquier sprite de `Assets/Sprites/Battlebacks/` al campo
   *Battle Background* (hay ~100: `Pradera_bg`, `Cueva_bg`, `Gimnasio1_bg`,
   `AltoMando1_bg`, `Volcan_bg`...).
3. Listo — al empezar ese combate, `BattleController.ApplyBackground()` pone el
   sprite y lo **escala solo** para cubrir la cámara (usa el máximo entre la
   proporción de ancho y de alto, así nunca quedan bordes).

Si el campo queda vacío, el del **lugar** donde se pelea: la tabla
`Resources/Definitions/Combate/FondosDeCombate.asset` (escena → fondo, la arma
`PokeRush > Combate > Elegir los fondos de combate`). Y si la escena no está en la
tabla, el del prefab (Pradera). Antes una pelea sin fondo se quedaba con el de la
pelea anterior. Los fondos de cada lugar están en `Documentacion/Testing/Playtest_5_Oct_2026.md`.

## ⚪ Las Poké Balls del equipo rival (playtest del 5-oct)

Debajo de la barra del databox rival, del lado izquierdo (donde el sprite es
transparente), una Poké Ball chiquita por Pokémon del rival:
**roja** si sigue en pie, **rosada** si tiene un estado, **gris** si se debilitó.
Contra un salvaje no se muestran. Las actualiza `BattleHudView.UpdateEnemyTeamBalls`
en cada `UpdateHud`.

Los íconos son los del pack (`icon_ball`, `_status`, `_faint`) a la mitad: el arte
es 2x, así que a 15 px no se pierde ningún pixel (`Sprites/UI/Battle/icon_ball_small*`).
Las pone en el prefab `PokeRush > Combate > Agregar las Poke Balls del rival`
(se puede volver a correr).

---

## 🏃 La acción HUIR (cambios en el motor)

Huir existía como enum (`BattleActionKind.Flee`) pero no estaba implementado.
Ahora:

- **`TurnEngine.TryEscape()`** usa la fórmula clásica de la saga:

  ```
  F = (velocidad propia × 128) / velocidad del rival + 30 × intentos
  escapa si un número al azar de 0-255 es menor que F
  ```

  Cada intento fallido suma +30, así siempre se termina escapando. Si falla,
  el jugador pierde su acción y el salvaje ataca igual (como en los juegos).
- **`BattleModel.FinishEscaped()`** termina el combate en un estado nuevo:
  ni victoria ni derrota (`PlayerEscaped`). `BattleOutcome.Escaped` lo
  propaga para que nadie dé recompensas ni muestre "Perdiste...".
- **Contra entrenadores no se puede**: `BattleController.SelectFlee()` corta
  antes de llegar al motor con el mensaje clásico.

**MOCHILA** por ahora es un stub: muestra "La mochila todavía no está
disponible" y vuelve al menú (los ítems en combate son una mejora futura, el
enum `UseItem` ya está reservado).

---

## 🎨 `SpriteAssigner.cs` — sprites de los Pokémon

**Ubicación:** `Assets/Editor/PokeApi/SpriteAssigner.cs`
**Menú:** `PokeRush > Asignar sprites a los Pokemon`

Asigna a cada `PokenDefinition` sus 6 sprites desde el set de
`Diseño/Graphics/Pokemon/`: frente y espalda de batalla (normal + shiny) e
ícono de UI (normal + shiny).

El problema que resuelve: los archivos del set se nombran por el **nombre
inglés en MAYÚSCULAS sin símbolos** (`BULBASAUR.png`, `NIDORANF.png`,
`MRMIME.png`, `HOOH.png`), pero nuestros assets tienen el nombre en español.
La solución: el número de Pokédex (`dexNumber`) busca el nombre inglés en el
**cache de PokeAPI** (`Tools/PokeApiCache/pokemon-species/{dex}.json`) y lo
convierte con `Sanitize()` (mayúsculas + quitar todo lo que no sea A-Z/0-9).

### 🎞️ Tiras de animación (los sprites están ANIMADOS)

Los PNG del set no son imágenes sueltas: son **tiras estilo Gen 5** con N
frames cuadrados en fila, donde **el lado del frame = el alto del PNG**
(ej: `BULBASAUR.png` de Front mide 3762×38 → 99 frames de 38×38). El script:

- Lee el tamaño real del PNG **directo del encabezado del archivo** (no de la
  textura importada, que puede estar reescalada) y recorta la tira en frames
  con pivot **bottom-center** (los pies apoyan en la plataforma).
- Sube `maxTextureSize` a la potencia de 2 que cubra la tira (hasta 16384):
  el borroso original venía de que Unity reescalaba las tiras al límite
  default de 2048.
- **El tamaño sale de los píxeles.** Las tiras vienen recortadas al contenido,
  así que el alto del frame *es* el tamaño del Pokémon: Aron mide 32 px y
  Noivern 182. Un frame de **80 px** (la mediana de las tiras) mide 1 unidad de
  alto, y el resto en proporción, con topes de **0,5** (para que los muy chicos
  se vean) y **1,5** (para que los gigantes entren en pantalla). Encima van las
  escalas del builder (enemigo 1.1, jugador 1.7). Lo calcula
  `SpriteAssigner.PpuDeCombate()`; los íconos y los entrenadores siguen con PPU =
  alto.
  - Antes el PPU era el alto del frame y **todos medían 1 unidad**: un Aron
    quedaba del alto de un Haunter. Para reaplicar la regla sin volver a copiar
    ni recortar nada: **`PokeRush > Ajustar tamaño de los Pokémon en combate`**
    (solo cambia el PPU de las tiras de Front/Back y sus shiny).
- Llena en `PokenDefinition` los arrays `battleFrontFrames` / `battleBackFrames`
  (+ shiny) con todos los frames; los campos single quedan con el frame 0.
- `PokenView` cicla los frames en loop (`frameRate` serializado, default 30 fps),
  con fallback al sprite estático si una especie no tiene tira.
- Los frames son para **tiras de animación**, no para poses sueltas. `NESTOR`
  tenía tres poses distintas (frente, manos en la cadera, brazos cruzados) y
  parpadeaba entre ellas a 30 fps durante la fase de liberación: ahora va sin
  frames, solo con `battleFrontSprite`.

⚠️ La primera ejecución re-importa ~2500 tiras sin comprimir: tarda varios
minutos y agranda la carpeta Library (solo local, no va al repo).

Igual que el resto de las herramientas: copia solo lo necesario a
`Assets/Sprites/Pokens/`, es idempotente, y al final reporta a cuáles especies
no les encontró sprite (solo Front/Back cuentan como faltantes críticos).

---

## 🎬 Secuencias de inicio y animaciones (`BattleFxView.cs`)

**Ubicación:** `Assets/Scripts/Views/BattleFxView.cs`

Todas las secuencias visuales del combate viven en este componente (corrutinas que
`BattleController` encadena con `yield return`). Igual que el HUD: **solo
presentación**, nunca toca reglas. Si el campo `battleFxView` del controller queda
vacío, el combate degrada a los refrescos instantáneos (modo de respaldo).

**Inicio de combate** (`BeginBattleRoutine`):
- `ScreenTransition()` — doble flash blanco + fundido a negro (Image fullscreen en
  un canvas propio con sorting 100, encima del HUD).
- Vs entrenador: `TrainerSlideIn` (el sprite del entrenador —campo
  `battleFrontSprite` del EnemyDefinition/BossDefinition— entra deslizándose),
  mensaje de desafío, `TrainerSlideOut`, y `ThrowBallAndAppear` para su Pokémon.
- Vs salvaje: `WildAppear` (fade + saltito desde el entorno).
- Siempre: el jugador lanza su ball (`ThrowBallAndAppear`: arco parabólico con la
  tira de rotación `ball_POKEBALL.png` de 8 frames, apertura + destello
  `ballBurst_ray`, y el Pokémon crece desde el piso con rebote).

**Durante el combate** (`PlayEvents` del controller, por tipo de evento):
- `PokenSwitched` → retirada (`Recall`: se encoge hacia la ball) + ball del nuevo.
  Si venía de debilitarse, no hay retirada (flag por lado que setea `Fainted`).
- `Fainted` → `FaintAnim`: se hunde y desvanece — **después** de que la barra
  terminó de vaciarse.
- `StatStageChanged` → pulso naranja (subida) o azul (bajada).
- `StatusChanged` → pulso con el color del estado (rojizo quemado, púrpura veneno,
  amarillo parálisis, gris sueño, cian congelado).

## ⚔️ Animaciones de ataques (`BattleFxView.PlayAttack`)

Cada movimiento se anima con la secuencia **anticipación → viaje/embestida →
impacto → recuperación**. El evento `MoveUsed` ahora lleva el `MoveDefinition`
usado, y el controller reproduce la animación completa **antes** del evento
`HpChanged` — el daño visual (drenado de barra) nunca ocurre antes del impacto,
como pide la spec.

**Animación genérica** (sin configurar nada, todos los 595 movimientos):
- **Físico**: el atacante retrocede (carga) y embiste hasta el objetivo.
- **Especial**: paso al frente y dispara un **proyectil teñido del color del
  tipo** (tabla `ColorForType`: Fuego naranja, Agua azul, Eléctrico amarillo...).
- **Estado**: destello del color del tipo sobre el usuario.
- **Impacto** (siempre): estrella de impacto teñida + parpadeo del golpeado +
  **sacudida de cámara**.

**Extensible por movimiento**: el asset `MoveAnimationDefinition`
(`Crear > PokeRush > MoveAnimationDefinition`) define tiempos (inicio/viaje/
impacto/recuperación), sprites propios de proyectil e impacto, tinte, fuerza de
sacudida, **flash de pantalla** y ganchos de sonido. Se arrastra al campo
`animationOverride` del `MoveDefinition` — el importador de PokeAPI **nunca toca
ese campo**, así que sobrevive a re-importaciones. Diseño puede darle animación
propia a los movimientos importantes sin tocar código.

## 📉 Barra de HP progresiva (`BattleHudView.AnimateHp`)

La vista mantiene un **HP mostrado** separado del real. Ante cada evento
`HpChanged`, `AnimateHp` lo drena/sube hacia el valor real con duración
proporcional al daño (0.25 a 1.3 s), actualizando color y números por frame, y
termina **exacto**. Como el evento `Fainted` viene después del `HpChanged` en la
lista del TurnEngine, el debilitamiento arranca recién con la barra en 0 — el
requisito de la spec sale gratis del diseño por eventos. `UpdateHud` (instantáneo)
queda para inicio/cambios. Gancho comentado para el sonido de drenaje.

## 🗂️ Resumen de archivos de esta capa

```
Assets/Scripts/
├── Views/BattleHudView.cs            ← la vista completa del HUD (reescrita)
├── Controllers/BattleController.cs   ← +menús, +SelectFlee, +MOCHILA stub, +ApplyBackground
├── Core/Battle/TurnEngine.cs         ← +TryEscape (fórmula de huida)
├── Core/Battle/BattleAction.cs       ← +factory Flee()
├── Core/Battle/BattleOutcome.cs      ← +Escaped
├── Models/BattleModel.cs             ← +PlayerEscaped / FinishEscaped()
├── Models/PokenModel.cs              ← +XpProgress (barra de EXP)
└── Data/{Enemy,Boss,WildEncounter}Definition.cs  ← +battleBackground

Assets/Editor/
├── BattleUiAssetImporter.cs          ← copia y recorta los gráficos de UI
├── BattleTestSceneBuilder.cs         ← arma la escena completa (reescrito)
└── PokeApi/SpriteAssigner.cs         ← sprites de los 435 Pokémon
```

## 🧪 Cómo probarlo

1. `PokeRush > Armar combate de prueba` (reconstruye la escena con el HUD).
2. Play: todo se maneja con el **mouse**; el teclado sigue funcionando
   (1-4 movimientos, F1-F6 cambio, B reiniciar, W salvaje).
3. Para el boss: arrastrar `Testing/BossDePrueba` al campo *Enemy Definition*
   del objeto `BattleTester`.

## 📌 Pendientes conocidos de esta capa

- **Género (♂/♀)**: el HUD de referencia lo muestra, pero `PokenModel` no tiene
  género todavía (requiere campo + persistencia en el save + ratio por especie
  desde PokeAPI).
- **Animación de las barras**: HP y EXP cambian de golpe; falta interpolarlas
  gradualmente.
- **MOCHILA**: implementar ítems en combate (la acción `UseItem` ya existe en
  `BattleActionKind`).
- **Lineup de balls** (los puntitos de equipo del mockup): `icon_ball.png` ya
  se importa pero no se muestra.
