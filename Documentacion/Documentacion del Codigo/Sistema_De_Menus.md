# 📋 Sistema de menús generales de PokeRush

Documento técnico del sistema de menús reutilizable: pausa, mochila, equipo,
resumen del Poken, opciones y cajas de mensaje. Funciona igual en el Overworld
y dentro de un combate, y se agrega a una escena nueva **sin configurar nada**.

> **Resumen para el apurado:** no hay que hacer nada. El sistema se
> auto-instancia desde `Assets/Resources/Menus/MenuSystem.prefab` la primera vez
> que alguien lo pide. En una escena nueva alcanza con que haya un `GameManager`
> con partida cargada (lo normal, vía `SceneBootstrapper`) y que el jugador
> apriete la tecla de menú.

---

## 🧭 La idea general

Tres reglas mandan sobre todo lo demás:

1. **La lógica no sabe cómo está dibujada la UI.** Las pantallas piden datos al
   modelo (`PlayerModel`, `InventoryModel`, `PokenModel`) y los widgets se
   limitan a mostrarlos. Cambiar sprites, posiciones o layouts no toca código.
2. **Solo una pantalla escucha teclas a la vez.** El `MenuManager` mantiene una
   pila y le manda el input **únicamente a la de arriba**. Por eso nunca quedan
   dos menús peleándose por la misma tecla.
3. **No hay una versión "de combate" y otra "del mundo".** La mochila y la party
   son las mismas; lo que cambia es el `MenuContextKind` con el que se abrieron.

```
┌──────────────────────────────────────────────────────────────────┐
│  GameInputService  (el ÚNICO que mira el teclado)                 │
│  Traduce tecla -> GameAction (Up/Down/Confirm/Cancel/Menu/...)    │
│  Los bindings se guardan en la tabla 'settings' de pokerush.db    │
└───────────────────────────┬──────────────────────────────────────┘
                            │ GameAction
┌───────────────────────────▼──────────────────────────────────────┐
│  MenuManager  (la PILA)                                           │
│  Open(id, payload, onClosed) / Close(screen) / CloseAll()         │
│  Solo la pantalla de arriba recibe HandleAction()                 │
└───────────────────────────┬──────────────────────────────────────┘
                            │ HandleAction(GameAction)
┌───────────────────────────▼──────────────────────────────────────┐
│  MenuScreen  (PauseMenu / Bag / Party / Summary / Options / Msg)  │
│  Lee el modelo, decide, y le pide a los widgets que se dibujen    │
└───────────────────────────┬──────────────────────────────────────┘
                            │ Bind(modelo)
┌───────────────────────────▼──────────────────────────────────────┐
│  Widgets  (MenuOption / ItemRow / PartyPanel / MoveRow / Option)  │
│  Solo dibujan. No leen input ni deciden nada.                     │
└──────────────────────────────────────────────────────────────────┘
```

---

## 🎮 Controles

| Acción | Tecla por defecto | Alias |
|---|---|---|
| Navegar | Flechas | WASD, D-pad |
| Aceptar / entrar / interactuar | **X** | Enter, botón sur |
| Volver / cancelar | **Z** | Backspace, botón este |
| Abrir el menú de pausa | **Z** | Escape, Start |
| Correr | Shift | botón oeste |
| Página anterior / siguiente | Q / E | L / R |

`Cancel` y `Menu` comparten la Z a propósito: con el menú cerrado la Z lo abre,
con el menú abierto la Z vuelve atrás. El `MenuManager` despacha una sola de las
dos por frame para que una Z no retroceda dos niveles de golpe.

**Todo esto es reasignable** desde `Opciones > Controles` y se guarda solo. Ningún
menú tiene teclas escritas adentro: si mañana "Aceptar" pasa a ser la K, todos
los menús y también el movimiento del Overworld usan la K.

---

## 📦 Qué hay y dónde está

| Qué | Dónde |
|---|---|
| Prefab raíz (auto-instanciable) | `Assets/Resources/Menus/MenuSystem.prefab` |
| Iconos compartidos (tipos, estados) | `Assets/Resources/Menus/MenuIcons.asset` (ver nota abajo) |
| Prefabs de pantalla | `Assets/Prefabs/UI/Menus/*.prefab` |
| Prefabs de widget | `Assets/Prefabs/UI/Menus/Widgets/*.prefab` |
| Código del framework | `Assets/Scripts/Menus/Core/` |
| Pantallas | `Assets/Scripts/Menus/Screens/` |
| Widgets | `Assets/Scripts/Menus/Widgets/` |
| Uso de objetos (lógica pura) | `Assets/Scripts/Menus/Items/` |
| Opciones y persistencia | `Assets/Scripts/Menus/Settings/` |
| Generador de prefabs | `Assets/Editor/Menus/` |

> **Ojo con los iconos de tipo y de estado.** Las hojas `icon_types.png` e `icon_statuses.png`
> están en el orden de Essentials (Normal, Lucha, Volador… / Dormido, Envenenado…), **no** en el
> de los enums `PokenType` y `StatusEffect`. `MenuIcons.asset` se indexa por el enum y la fila
> sale de `BattleUiAssetImporter.TypeRow` / `StatusRow`, el mismo mapa que usa el HUD de combate.
> Hasta el 14-sep se tomaba fila = enum y el Resumen mostraba mal todos los tipos (Psíquico
> salía como Agua). Para rehacer solo los iconos, sin tocar los prefabs:
> `PokeRush > UI > Rehacer iconos de tipos y estados`.

---

## 🚀 Cómo usarlo en una escena nueva

**Paso 1:** que la escena tenga un `GameManager` y un `SceneBootstrapper` (es lo
que ya hacen todas las escenas jugables).

**Paso 2:** no hay paso 2.

La primera vez que alguien llama a `MenuManager.Instance`, el manager se carga
solo desde `Resources/Menus/MenuSystem` y sobrevive los cambios de escena
(`DontDestroyOnLoad`). No hay que arrastrar nada al Inspector.

Para abrirlo desde el mundo, `PlayerController.OpenMenu()` ya lo hace:

```csharp
MenuManager.Instance.SetContext(MenuContextKind.Overworld);
MenuManager.Instance.Toggle(MenuScreenId.Pause);
```

Y para que el jugador no camine por atrás del menú, `PlayerInputController`
chequea una sola línea:

```csharp
if (MenuManager.AnyOpen) return;   // el mundo no escucha nada
```

> Si preferís tenerlo visible en la escena (para tocarlo en el Inspector),
> podés arrastrar `MenuSystem.prefab` a la jerarquía: el manager detecta que ya
> hay uno y no instancia un segundo.

---

## 🗂️ Las pantallas

### Menú de pausa — `PauseMenuScreen`

Pokémon / Bolsa / Guardar / Opciones / Salir.

**Las opciones no están en el código.** Son los `MenuOptionWidget` que cuelgan de
`Option Container` en el prefab, en el orden en que estén ahí. Cada uno tiene un
`Option Id` que la pantalla lee para saber qué hacer.

**Agregar una opción nueva:**
1. Duplicá un `Opcion_*` dentro de `Opciones` en `PauseMenuScreen.prefab`.
2. Poné el `Option Id` (ej. `Pokedex`), el texto y el icono.
3. Agregá el `case "Pokedex":` en `PauseMenuScreen.Activate()`.

Reordenar o sacar opciones es solo mover/borrar objetos: no hay índices fijos.

**Guardar** hace el flujo completo: pregunta → cartel "Guardando..." (que no se
puede cerrar) → guarda de verdad → **verifica** con `SaveService.HasSaveData()` →
cartel de confirmación. Si el guardado tira excepción, muestra el error y deja
seguir jugando; nunca queda trabado.

**Salir** pide confirmación, cierra toda la pila y carga la escena del campo
`Main Menu Scene` (por defecto `Menu Principal`).

### Mochila — `BagScreen`

Tres bolsillos: Medicinas, Obj. Combate, Obj. Claves. Salen directo de las tres
listas de `InventoryModel`, normalizadas por `BagModel.Build()` en `BagEntry`.

- Arriba/abajo: mover el cursor (la lista scrollea sola, `Visible Rows` en el Inspector).
- Izquierda/derecha: cambiar de bolsillo.
- Aceptar: submenú del objeto (**USAR** / **CANCELAR**).
- Volver: cerrar.

El texto de la acción es un campo del Inspector (`Use Label`). Si el equipo
prefiere que diga **DAR**, se cambia ahí: la lógica no depende del texto.

Recuerda el último bolsillo entre aperturas.

### Selección de Pokémon y equipo — `PartyScreen`

Una sola pantalla con tres modos:

| Modo | Quién lo usa | Qué hace Aceptar |
|---|---|---|
| `Browse` | Pausa → Pokémon | Submenú: DATOS / CAMBIAR / CANCELAR |
| `SelectTarget` | Mochila → objeto | Aplica el objeto (mundo) o devuelve el índice (combate) |
| `SelectSwitch` | Combate → relevo | Devuelve el índice elegido |

**Cambiar el orden:** Aceptar → CAMBIAR agarra el Poken, movés el cursor y
Aceptar de nuevo lo intercambia. Modifica `PlayerModel.Team` **de verdad**: el
slot 0 es el que sale primero al combate y el que `SaveService` guarda con
`SlotIndex = 0`.

En `SelectTarget`, los Pokémon sobre los que el objeto no haría nada se pintan
apagados (`ItemUsageService.Evaluate`) y elegirlos muestra el motivo sin gastar
el objeto.

**El botón SALIR** (desde el 1-oct-2026). Antes era una etiqueta gris a la que ni
el cursor ni el mouse llegaban: solo se salía con Z, y en la fase de liberación de
Néstor (que se juega con el mouse) no había forma visible de volver. Ahora:

- Es el botón CANCEL del pack (`Graphics/UI/Party/icon_cancel_narrow*.png`): azul,
  y con el borde rojo cuando el cursor está encima, igual que los paneles.
- Es **un lugar más del cursor**, después del último panel: pasarse por abajo (o
  por la derecha) cae en SALIR y desde ahí se vuelve al primero o al último. Con
  el cursor encima, el cartel dice "Volver sin cambiar de Pokémon.".
- Se puede **tocar con el mouse** (`MenuPuntero`: pasar por encima mueve el
  cursor, el clic acepta, se achica mientras está apretado). Si hay un cartel
  abierto encima, el clic no le corresponde y no hace nada.
- Aceptar en SALIR hace lo mismo que Z: devuelve `Cancelled` sin tocar el equipo.
  Quien abrió la pantalla vuelve a lo suyo (en combate, el menú de comandos; en la
  liberación, el panel de la liberación).
- Si no hay a quién elegir (en la liberación con un solo Pokémon, el único es el
  que pelea), el cursor arranca en SALIR.
- En el reemplazo obligado (`CanCancel = false`) sigue sin dibujarse.

Para poner el botón en un prefab ya armado sin rehacer los demás menús:
`PokeRush > UI > Boton SALIR de la pantalla de equipo`. Los dos PNG están en
`Assets/Graphics` (ignorado): se suben con `git add -f` junto con su `.meta`.

### Resumen — `SummaryScreen`

Cuatro páginas: Información, Notas Entrenador, Estadísticas, Movimientos.

- Izquierda/derecha: cambiar de página.
- Arriba/abajo: cambiar de Pokémon sin salir.
- En la página de movimientos, Aceptar entra al modo reordenar: Aceptar agarra un
  movimiento, movés y Aceptar de nuevo los intercambia. Cambia `PokenModel.Moves`
  real, que es la lista que usa el combate y la que se guarda.

**Agregar una página:** creá el GameObject dentro de `Paginas`, agregá una entrada
al array `Pages` del componente (título + root + pestaña) y llenala en `Refresh()`.

### Opciones — `OptionsScreen`

Volumen de música y de efectos (izquierda/derecha) y la lista de controles.
Aceptar sobre un control entra en modo "apretá la tecla nueva"; Escape cancela.

**Las filas se arman solas** desde `GameInputService.RebindableActions`: agregar
una `GameAction` nueva al juego la hace aparecer acá sin tocar el prefab.

### Caja de mensajes — `MessageBoxScreen`

Avisos y confirmaciones. Es una pantalla más de la pila (no un panel que cada
menú prende), así que mientras está abierta el menú de abajo deja de recibir
teclas solo. Desde cualquier pantalla:

```csharp
ShowMessage("¡No le hace nada!");
Ask("¿Guardar la partida?", ok => { if (ok) Guardar(); });
```

Una pregunta puede traer sus propias opciones (`YesLabel` / `NoLabel`, por ejemplo
SUBIR / BAJAR en el ascensor del casino). En ese caso hay que mirar
`MessageBoxResult.Cancelled`: cerrar con Volver da `Confirmed = false`, igual que
elegir la segunda opción, y no es lo mismo.

### PC de almacenamiento — `PcStorageScreen`

Las cajas del PC y el equipo en una sola pantalla, con el arte de
`Graphics/UI/Storage`. Se abre desde un `PcInteractable` del mapa. Reglas, guardado y
cómo se pone un PC en un nivel: ver `PC_De_Almacenamiento.md`. El prefab lo genera
`PokeRush > UI > Construir la pantalla del PC`, que lo agrega al MenuSystem sin
rehacer las otras pantallas.

La pantalla del equipo tiene un modo `PartyScreenMode.SelectAny`: elegir cualquier
Pokémon y devolverlo, sin submenú (lo usa Tacconi para elegir qué apostar).

### Tienda — `ShopScreen` y tutor de movimientos — `MoveTutorScreen`

Las dos pantallas de la Quarcho Cueva (`MenuScreenId.Shop` y `MoveTutor`), con el arte
del pack: la tienda es `Graphics/UI/Mart` (se abre encima del mapa, sin oscurecer) y el
tutor es el Recordador de movimientos, `Graphics/UI/Move Reminder`. Las reglas no
viven en las pantallas sino en `ReglasDeTienda` y `TutorDeMovimientos`. Las genera
`PokeRush > UI > Construir las pantallas de la Quarcho Cueva`, que las agrega al
MenuSystem sin rehacer las otras. Son de prueba: las finales las diseña el equipo.
Detalle en `Quarcho_Cueva_Lobby.md`.

---

## ⌨️ El HUD de combate con las flechas

El HUD de combate (`BattleHudView`) se maneja con las flechas además del mouse,
usando **las mismas teclas** que el resto del juego. Aplica a los cuatro paneles:
comandos, movimientos, equipo y la fase de liberación.

- Flechas: mover el cursor.
- Aceptar (X): pulsar el botón marcado.
- Volver (Z): salir del panel — desde movimientos y equipo vuelve a comandos;
  desde comandos no hace nada (el turno hay que jugarlo), y en un cambio
  obligado tampoco (tenés que elegir con quién seguís).

**No hay un cursor nuevo.** Se usa el estado `Selected` que cada botón ya tiene
configurado en el prefab — los de movimiento cambian de sprite según el tipo, y
eso ya estaba resuelto. Cambiar cómo se ve el cursor es cambiar la transición
del botón en Unity.

**No hay layouts hardcodeados.** El movimiento usa la navegación automática de
Unity (`FindSelectableOnUp/Down/Left/Right`), que resuelve qué botón está al
lado de cuál **según dónde están dibujados**. Por eso funciona igual con el menú
de comandos en 2×2 que con la mochila en lista, y sigue funcionando si movés los
botones. En el borde del panel da la vuelta, como en la saga, salteando lo que
no se puede elegir (un slot de movimiento vacío, un Pokémon debilitado).

Dos detalles que costó encontrar y conviene no deshacer:

- **El `InputSystemUIInputModule` del `EventSystem` también navega con las
  flechas.** Si se deja prendido, cada flecha salta dos botones. Mientras el HUD
  tiene un panel abierto se apaga con `sendNavigationEvents = false`, y se
  restaura al cerrarlo.
- **`BattleHudView.IsNavigating`** funciona igual que `MenuManager.AnyOpen`: con
  un panel del HUD abierto, `PlayerInputController` no lee nada. Sin eso, según
  cuál `Update()` corriera primero, el mundo se comía la tecla antes de que
  llegara al combate.

Se puede apagar entero con `Navegacion Por Teclado` en el Inspector del
`BattleHudView` (vuelve a quedar solo el mouse).

---

## ⚔️ Integración con el combate

`BattleController` no reimplementa nada: al abrir la mochila usa la misma.

```
Combate (turno del jugador)
  └─ MOCHILA
       └─ BagScreen (Context = Battle)
            └─ objeto -> USAR
                 └─ PartyScreen (SelectTarget)
                      └─ elegís el Pokémon  ─┐
                                             │  devuelve item + índice
  ┌──────────────────────────────────────────┘
  └─ BattleAction.UseItemOn(item, índice) -> TurnEngine
```

**Lo importante:** en combate la mochila **no aplica** el objeto. Solo dice cuál y
sobre quién; lo aplica el `TurnEngine` como acción del turno. Si lo aplicara la
UI, el objeto curaría dos veces.

### Cambiar de Pokémon

La misma `PartyScreen` del mundo se usa para elegir relevo, en modo
`SelectSwitch`. Aceptar sobre un Pokémon abre el submenú **SACAR A BATALLA /
DATOS / CANCELAR** (el texto de la primera es el campo `Send Out Label`).

Hay dos entradas:

```
Combate → POKÉMON            (o CAMBIAR en la fase de liberación)
  └─ PartyScreen (SelectSwitch, se puede cancelar)
       └─ SACAR A BATALLA → OnSwitchChosen(índice)

El Pokémon activo se debilita
  └─ PartyScreen (SelectSwitch, CanCancel = false)
       └─ SACAR A BATALLA → reemplazo obligado
```

En el reemplazo obligado la pantalla **no se puede cancelar**: el combate está
detenido esperando la elección, así que Volver muestra "Tenés que elegir un
Pokémon" y el botón SALIR ni se dibuja. El cursor además arranca sobre alguien
elegible, no sobre el que acaba de caer.

El que ya está en el campo llega bloqueado por `BlockedIndices`, y los
debilitados los filtra sola la pantalla. El bloqueo del rastreo de Nestor
(`BossBattleConfig.PermiteCambio`) se avisa **antes** de abrir, para no hacer
elegir un Pokémon y rechazarlo después.

El interruptor `Usar Party Compartida` en el `BattleController` vuelve al panel
simple del HUD.

Si el jugador cancela, no se consume el turno y vuelve el menú de comandos.

El interruptor `Usar Mochila Compartida` en el `BattleController` permite volver a
la lista simple del HUD (que cura siempre al Poken activo) si hiciera falta.

> La fase de liberación sigue usando la lista del HUD a propósito: ahí la
> curación va sí o sí al Poken que está en el campo, así que un panel de
> objetivo sería mentira.

---

## 💾 Persistencia

Todo va a la **misma base** que el resto del save (`pokerush.db`). No hay un
segundo sistema de guardado.

| Dato | Dónde vive | Se guarda con |
|---|---|---|
| Orden del equipo | `player_poken.SlotIndex` | `GameManager.SaveGame()` |
| Orden de los movimientos | `player_poken_moves.SlotIndex` | ídem |
| PP de cada movimiento | `player_poken_moves.CurrentPP` | ídem |
| Inventario y cantidades | `player_inventory` | ídem |
| PS y estado de cada Poken | `player_poken` | ídem |
| Volumen de música y efectos | tabla `settings` | al toque, sin "Aplicar" |
| Controles | tabla `settings` (`input.<Acción>`) | al toque |

La tabla `settings` es aparte de `player` a propósito: volumen y controles son de
la máquina, no de la partida. Borrar el save no te devuelve los controles de
fábrica.

**Lo único que se agregó al save existente** fue la columna `SlotIndex` en
`player_poken_moves` (sin ella el orden de movimientos dependía del orden físico
de las filas, que es un detalle de SQLite) y la tabla `settings`. sqlite-net crea
ambas solo; los saves viejos siguen cargando.

---

## 🎨 Cambiar la apariencia

Todos los prefabs se editan en Unity como cualquier otro. La lógica **no** depende
de posiciones, nombres de GameObject ni cantidad de elementos en pantalla.

Se puede cambiar libremente: sprites, fuentes, textos, colores, tamaños,
posiciones, layouts, fondos, animaciones y los indicadores de selección.

**Cómo se ve "seleccionado":** lo define el componente `MenuWidget` de cada widget,
y hay tres mecanismos que se pueden combinar o dejar vacíos:

| Campo | Qué hace |
|---|---|
| `Selected Objects` | GameObjects que se prenden al estar seleccionado (cursor, marco, brillo) |
| `Deselected Objects` | GameObjects que se prenden al NO estar seleccionado |
| `Background Image` + `Normal/Selected Sprite` | Cambia el sprite del fondo |
| `Tinted Graphics` + `Normal/Selected Color` | Cambia el color de textos e imágenes |
| `Disabled Color` | Color de un elemento que no se puede elegir |

Ejemplo: para usar la flechita clásica en vez del resaltado naranja, poné un
`Image` con `sel_arrow.png` dentro del widget y arrastralo a `Selected Objects`
(y sacá el objeto `Resaltado`). Cero código.

**Los formatos de texto también son campos:** `Level Format` (`"Nv.{0}"`),
`Hp Format` (`"{0}/ {1}"`), `Pp Format`, `Quantity Format`, `Money Format`.

---

## 🔧 Agregar cosas nuevas

**Un tipo de objeto nuevo (ej. Revivir):**
`HealItemDefinition` ya tiene los campos: marcá `Revives` y el `Revive Percent`.
Para curar estados, llená `Cures Statuses` (o `Cures All Statuses` para una Cura
Total). `ItemUsageService` se encarga del resto y vale igual en el mundo y en
combate.

**Un bolsillo nuevo:** agregá el valor a `ItemPocket`, el caso en
`BagModel.Build()` y su nombre en `BagModel.PocketName()`. La UI lo toma solo.

**Una pantalla nueva:**
1. Agregá el valor a `MenuScreenId`.
2. Creá una clase que herede de `MenuScreen` e implemente `HandleAction`.
3. Armá el prefab, poné el `Screen Id` y arrastralo a `Screen Prefabs` del
   `MenuManager` en `MenuSystem.prefab`.

**Una acción de input nueva:** agregá el valor a `GameAction`, su binding por
defecto en `GameInputService.DefaultBindings()` y, si querés que se pueda
reasignar, metela en `RebindableActions`. Aparece sola en Opciones.

---

## 🧪 Probarlo

`PokeRush > UI > Probar sistema de menus` mete un objeto de prueba en la escena
abierta, entra en Play Mode y recorre el flujo completo verificando contra el
modelo y contra el save. Al terminar sale solo y borra el objeto.

Lo que verifica (59 chequeos, todos en verde al 21-ago-2026 sobre `NestorGOD2`):

- **La tecla de menú abre, cierra y vuelve a abrir la pausa.** Se prueba con
  teclas de verdad (`InputSystem.QueueStateEvent`), no llamando a
  `HandleAction`: es una regresión de un bug donde el menú se abría una sola vez.
- El `MenuManager` se auto-instancia desde Resources.
- **Elegir relevo en combate** usa la pantalla nueva, ofrece "sacar a batalla",
  no deja cancelar el reemplazo obligado y nunca devuelve al que ya peleaba.
- **El HUD de combate se navega con las flechas**: el menú de comandos arranca
  con una opción marcada, las flechas mueven la selección, Aceptar entra al
  panel de movimientos y Volver regresa a comandos. También con teclas de verdad.
- La pausa abre, navega y resalta una sola opción.
- La party dibuja un panel por Poken del equipo real.
- Reordenar el equipo cambia `PlayerModel.Team`.
- El resumen abre sobre el Poken correcto y reordenar movimientos cambia
  `PokenModel.Moves`.
- La mochila lee el inventario real; usar una poción cura de verdad y descuenta
  el objeto.
- En **combate** la mochila NO cura ni descuenta: devuelve objeto + objetivo, y
  el `TurnEngine` cura al Poken **elegido** (no al activo) y descuenta ahí.
- Opciones cambia el volumen y reasigna una tecla, y ambos quedan guardados.
- Guardar → recargar conserva orden del equipo, orden de movimientos, cantidades
  del inventario y el volumen.
- `CloseAll()` deja la pila vacía y ninguna pantalla activa.

**Qué NO cubre:** que las teclas físicas lleguen (el test llama a `HandleAction`
directo, no aprieta teclas). Eso hay que probarlo jugando.

> **Desde el 2-oct-2026 pasa sin fallos.** Hasta el 1-oct fallaban 9 chequeos de
> "usar un objeto": el banco usaba el **primer** objeto del bolsillo de medicinas,
> que era *Cura Total* (no cura PS). Ahora elige uno que cure PS
> (`IndiceDeUnoQueCura`). Correrlo con una escena de nivel abierta (por ejemplo
> `Quarcho-Cueva`): necesita una partida cargada y, si no la hay, corta con
> "No hay partida cargada".
>
> El banco **guarda la partida** (`GameManager.SaveGame`): respaldar
> `%USERPROFILE%/AppData/LocalLow/LevelUP Creators/PokeRush/pokerush.db` antes de
> correrlo si hay una partida que importe.

Para revisar la UI sin jugar hasta cada pantalla:
`PokeRush > UI > Sacar capturas de los menus` abre las seis pantallas, saca una
captura de cada una en `Codigo/PokeRush/Capturas/Menus/` y sale del Play Mode.
Esa carpeta está en el `.gitignore`: es para mirar, no para versionar.

---

## 🏗️ Regenerar los prefabs

`PokeRush > UI > Construir sistema de menus` los vuelve a crear de cero.

> ⚠️ **Los reemplaza.** Si el equipo ya retocó los prefabs a mano, no lo corras:
> el generador es el punto de partida, no la fuente de verdad.

---

## ⚠️ Detalles que conviene saber

- **No se toca `Time.timeScale`.** Congelar el juego rompería las corrutinas del
  movimiento por grilla y del ritmo del combate. En vez de eso, los menús se
  quedan con el input y usan tiempo sin escalar.
- **Las pantallas no se destruyen al cerrarse**, quedan apagadas en un pool. Volver
  a abrir la mochila no reinstancia la lista entera.
- **Cerrar una pantalla que no está arriba** cierra también todo lo que tenga
  encima; nunca queda una pantalla viva sin nadie que le mande input.
- **Los callbacks de cierre corren con la pila ya consistente**, para que uno de
  ellos pueda abrir otra pantalla sin romper el recorrido.
- El `PokenModel` no tiene naturaleza ni lugar de captura, así que la página de
  Notas muestra la **característica** derivada del IV más alto y la habilidad. Si
  algún día existen esos campos, se agregan ahí.
- El ID de entrenador del resumen se deriva del nombre del jugador: no hay un ID
  real en el save todavía.
- **Las barras de relleno necesitan un sprite.** Un `Image` sin sprite ignora
  `type` y `fillAmount` y dibuja siempre el rectángulo entero, así que las barras
  de PS/EXP/PP se veían llenas siempre. Por eso el generador crea
  `Widgets/white_pixel.png` y se lo asigna a cada barra. Si cambiás el sprite de
  una barra, poné otro: no lo dejes vacío.
- **Los fondos del pack son de 512×384.** Las pantallas con fondo ilustrado
  (mochila, equipo, resumen) tienen un nodo `Marco` de exactamente esa medida
  centrado en pantalla; todo lo demás cuelga de ahí. Estirar el fondo a toda la
  pantalla lo deformaba en 16:9 y desalineaba lo que iba encima.
- **El contenido va DENTRO de los huecos del arte, no encima.** Las coordenadas
  salen de medir los PNG: en `Bag/bg_1.png` la ventana de objetos está en
  x 190..460 / y 8..272 y el recuadro del ícono de la descripción en
  x 20..76 / y 308..364; en el resumen el panel izquierdo va en x 4..212, el
  derecho en x 216..508, la barra de EXP en x 353..505 / y 203..215, y las
  cuatro filas de movimientos cada 64 px desde y 104. Por eso la mochila y el
  resumen no dibujan ni un panel propio.
- **El resumen usa un fondo POR PÁGINA** (`bg_info`, `bg_memo`, `bg_skills`,
  `bg_moves`), que es como viene el pack y como se ven las referencias.
- **Las pestañas traen dos frames en un PNG** (apagada + encendida, 56×28). Se
  muestran con `RawImage` + `uvRect` en vez de cortar el sprite: dos hijos, uno
  por mitad, que el `MenuWidget` prende y apaga.
- **Sobre los fondos celestes el texto blanco se pierde**, así que los textos del
  resumen llevan un `Outline` oscuro de 1px. En la ventana blanca de la mochila
  pasa lo contrario: ahí el texto es azul oscuro.

---

## 🎯 Estado y qué falta

Lo que hay funciona punta a punta y está verificado. Lo que queda para el equipo:

- **La UI es funcional, no definitiva.** Está armada con los assets del pack y se
  lee bien, pero las pantallas finales las diseña el equipo. Toda la arquitectura
  está pensada para que eso sea trabajo de Unity, no de código.
- Los íconos de los objetos se asignan solos con
  `PokeRush > UI > Asignar iconos a los objetos` (mapea el nombre del asset al
  PNG de `Assets/Graphics/Items/`). Si agregás un objeto nuevo, sumá su entrada
  al diccionario de `ItemIconAssigner` o asignale el ícono a mano.
- Los objetos de combate (`BattleItemDefinition`) se muestran pero todavía no
  tienen efecto en el `TurnEngine`; la mochila lo avisa en vez de gastarlos.
- Los objetos clave muestran su descripción; enchufar su efecto real es agregar
  el caso en `BagScreen.UseCurrent()`.
