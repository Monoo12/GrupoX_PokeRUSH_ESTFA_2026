# Cómo armar un mapa nuevo (jugable)

Guía para dejar una escena de mapa caminable. **Los scripts ya están hechos y hay un Prefab del jugador**: acá se explica qué hay que tocar en la escena, sin escribir código.

La trampa principal está en el punto 2: **pintar tiles en la capa `Colisiones` NO alcanza para que el jugador choque.** Es el error que se comió una sesión entera; leelo antes de pintar el mapa.

---

## 1. Estructura de la escena

Está el menú **`PokeRush > Armar escena de mapa`** (`Assets/Editor/MapSceneBuilder.cs`) que crea el `Grid` con las cuatro capas ya ordenadas:

| Capa | Sorting | Para qué |
|---|---|---|
| `Suelo` | -10 | piso caminable |
| `Decorado` | 0 | detalles que no bloquean |
| `Colisiones` | 10 | paredes, muebles, bordes |
| `Techos` | 20 | aleros y copas que tapan al jugador |

El `Grid` tiene que quedar con **Cell Size (1,1,1)** y **Tile Anchor (0.5, 0.5)** (es lo que crea el menú). De ahí sale una regla que importa: **los centros de tile caen en medios enteros** — (-3.5, -0.5), no (-3, 0).

---

## 2. Colisiones: lo que hay que saber sí o sí

`Colisiones` viene con `TilemapCollider2D` + `CompositeCollider2D` + `Rigidbody2D` (Static). Igual, recién pintado, **no bloquea nada**. Son dos cosas:

### a) La capa del GameObject

El `PlayerController` decide si puede avanzar con un `Physics2D.OverlapCircle` contra su **`obstacleMask`**, que apunta a la capa **`Obstacles`**. Si el GameObject `Colisiones` quedó en `Default`, el jugador atraviesa todo.

> **Poné el GameObject `Colisiones` en la capa `Obstacles`.** No alcanza con que tenga collider.

### b) El `colliderType` de los tiles

Los tiles de `Assets/Palettes/Interior1_Tiles.asset` vienen con **`colliderType = None`**. Pintados en `Colisiones` se ven, pero no generan geometría: el `CompositeCollider2D` queda en `shapeCount = 0`.

**No arregles esto editando el `.asset`.** Esos tiles son compartidos: varios de los que usa `Colisiones` son los mismos objetos que usa `Suelo`, y otros mapas usan la misma paleta. Si les ponés collider al asset, **se vuelve sólido el piso y se rompen las otras escenas**.

La forma correcta es un **override por celda**, que se guarda en el Tilemap de esa escena y no toca el asset. Con la escena abierta, `PokeRush > Run Command` o cualquier script de editor:

```csharp
var go = GameObject.Find("Grid/Colisiones");
var tm = go.GetComponent<Tilemap>();
var tc = go.GetComponent<TilemapCollider2D>();
var cc = go.GetComponent<CompositeCollider2D>();

go.layer = LayerMask.NameToLayer("Obstacles");

foreach (var p in tm.cellBounds.allPositionsWithin)
    if (tm.GetTile(p) != null)
        tm.SetColliderType(p, Tile.ColliderType.Grid);

tc.enabled = false; tc.enabled = true;   // sin esto el shapeCount sigue en 0
cc.GenerateGeometry();
```

> **Lo definitivo:** el GameObject `Colisiones` de `NestorGOD2` y de los cuatro pisos del casino lleva el componente **`ColisionesTilemapRuntimeFix`**, que repone los `colliderType` en `Awake`. Con eso el **juego** arranca bien siempre, aunque la escena se haya guardado con las colisiones perdidas. Copialo a cualquier mapa nuevo: es la única forma de no depender de acordarse.
>
> No es teórico: hasta el 13-sep-2026 los pisos del casino no lo tenían, y entre un guardado y otro la pared de arriba y todos los muebles quedaron sin `Grid`. El jugador atravesaba las tragaperras y se salía del mapa por arriba.

Para auditar una escena sin abrir Unity: el override queda en el YAML, en `m_AllTileFlags` de cada tile. **Bit 31 = `Grid`, bit 30 = `Sprite`** (los bits bajos son los TileFlags). Las celdas que frenan tienen el valor en `2147483648` o más: `grep -cE "m_AllTileFlags: 21474836(49|5[01])"` las cuenta (`2147483649` y `2147483651` son `Grid` con distintos TileFlags; contar solo `214748365` se come la mitad). `1073741827` parece un override y es `Sprite`, que con estos tiles no frena.

> **Para el editor:** está el menú **`PokeRush > Reparar colisiones del mapa`**, que hace todo esto de una sobre la escena abierta. Correlo cada vez que toques la capa `Colisiones` — pintar o borrar tiles **pierde los overrides en silencio**, y el síntoma es siempre el mismo: el jugador atraviesa las paredes.

Dos detalles que cuestan horas si no se saben:

- **`Grid`, no `Sprite`.** Los sprites de estos tiles tienen la physics shape vacía, así que `ColliderType.Sprite` genera geometría vacía igual. `Grid` usa la celda entera, que además es lo que querés para movimiento por grilla.
- **El toggle `enabled` es obligatorio.** `SetColliderType` no le avisa al collider, y el `shapeCount` se queda en 0 aunque el override esté bien puesto.

Verificado: el arreglo sobrevive al guardado y al domain reload de entrar en Play.

> Solo `Colisiones` lleva `TilemapCollider2D`. Si `Suelo`, `Decorado` o `Techos` tienen uno, sacalo: bloquean el mapa entero.

---

## 3. El jugador

**No lo armes a mano.** Instanciá **`Assets/Prefabs/Player.prefab`** y listo. Ya trae todo:

```
Player          Rigidbody2D (Kinematic), CapsuleCollider2D,
│               PlayerController, PlayerInputController
└── Visual      SpriteRenderer (sortingOrder 15), PlayerView
```

- **Posicionalo en un centro de tile** (medios enteros). `PlayerController.Awake()` toma la posición inicial como origen de la grilla: si arranca desfasado, **todos** los pasos siguientes quedan desfasados y los chequeos de pared fallan.
- El **`Rigidbody2D` Kinematic + el collider** no son para chocar (de eso se encarga el `OverlapCircle`): son para que Unity dispare los `OnTriggerEnter2D` de `DoorTrigger` y `BattleZoneTrigger`. Sin ellos las puertas no andan.
- Lo visual va en el hijo `Visual` a propósito: la raíz queda en escala (1,1,1) para que el collider y la grilla se midan en unidades reales. **Si querés el personaje más grande, escalá `Visual`, nunca la raíz.**
- `pauseMenuView` y `battleController` quedan en `null` en el prefab y se completan por escena. No los guardes en el prefab.

### Sorting del jugador

El prefab usa **15**: encima de `Colisiones` (10) para que las paredes y muebles no le tapen la cabeza, y debajo de `Techos` (20) para que los aleros sigan escondiéndolo.

> Ojo: el comentario de `MapSceneBuilder.cs` dice 5. Con 5 los muebles de `Colisiones` se dibujan **encima** del personaje. Vale 15.

Los NPCs van en el **mismo 15** que el jugador, y entre ellos se tapan según la altura: el que está más al sur se dibuja adelante. Eso lo hace el `Assets/Settings/Renderer2D.asset`, con **Transparency Sort Mode = Custom Axis (0, 1, 0)**. Hasta el 13-sep-2026 estaba en `Default`, que con cámara ortográfica ignora el eje: jugador y NPC con el mismo orden se tapaban al azar. El eje solo desempata dentro del mismo sorting order, así que no cambia nada de lo que ya tiene órdenes distintos (tiles, combate, UI).

---

## 4. Cámara

Agregale **`CameraFollow`** a la `Main Camera` y arrastrale el `Player` al campo `target`. Nada más — no usamos Cinemachine.

`smoothTime` 0.15 va bien; 0 la deja pegada al jugador. La cámara solo lee la posición del target en `LateUpdate`, así que el jugador no depende de la cámara ni al revés.

---

## 5. Animación del personaje

Ya está configurada en el prefab, pero si cambiás la hoja de sprites hay que saber esto:

- Las hojas son de **4 filas × 4 columnas**, y las filas van de arriba abajo: **abajo, izquierda, derecha, arriba**.
- El ciclo correcto es de 4 tiempos: **neutro → zancada → neutro → zancada**. Si le sacás los neutros, la caminata se ve mal y no hay pose de idle (`PlayerView` usa el cuadro `[0]` como pose quieta).
- **En `POKEMONTRAINER_RojoNeutro_walk_2.png` la columna 1 no sirve**: es la pose de lanzar la Pokébola (se nota porque mide 50px y las otras 38-42). El neutro de esa hoja es la columna 3. La hoja de `run` sí es una 4x4 limpia.
- Los FPS tienen que acompañar la velocidad o el personaje patina: **~2 cuadros por tile recorrido**. Con `walkSpeed 4` y `runSpeed 7` eso da `walkFps 8` y `runFps 14`.

---

## 6. NPCs

Hay dos prefabs listos en `Assets/Prefabs/NPCs/`. Se arrastran a la escena y listo, **no hace falta ningun script**:

| Prefab | Sprite | Alto |
|---|---|---|
| `NPC_Nestor.prefab` | `Sprites/Nestor/NESTOR_frente.png` | 2,6 u |
| `NPC_Combusken.prefab` | `Sprites/Pokens/COMBUSKEN.png` (fila de arriba, mirando abajo) | 1,7 u |

Estructura, igual que el jugador:

```
NPC_X          BoxCollider2D (0.8 x 0.8), capa Obstacles
└── Visual     SpriteRenderer (sortingOrder 15)
```

**Lo importante: la raiz va en la capa `Obstacles`.** Asi el mismo `obstacleMask` del `PlayerController` que lo frena contra las paredes lo frena tambien contra el NPC, sin escribir una linea de codigo. Si queda en `Default`, el jugador lo atraviesa.

**No llevan `Rigidbody2D`**, a diferencia del jugador: no se mueven y nadie les dispara triggers, asi que alcanza con el collider estatico.

### Alto y pivot

Ojo que los sprites del proyecto **no usan la misma convencion de pivot**, y eso cambia donde hay que poner el `Visual`:

- Los de **Nestor** tienen el **pivot en los pies** y el PPU igual a su propia altura en pixeles, asi que miden 1 unidad exacta (se lo pone `PokeRush > Importar sprites de Nestor`).
- Los de **Pokemon y del jugador** tienen el **pivot centrado** y PPU 100.

Esta formula sirve para los dos casos y evita tener que adivinar:

```
Visual.localScale    = altoDeseado / sprite.bounds.size.y
Visual.localPosition = (0, -0.25 - escala * sprite.bounds.min.y, 0)
```

El `-0.25` es cuanto sobresalen los pies bajo el centro del tile, igual que el jugador. Como referencia de tamanios: **el jugador mide 2,24 u**.

### Que les falta para ser interactuables

Los NPCs de hoy solo bloquean el paso. Para que respondan al boton de interactuar hay que agregarles `InteractableTrigger` **y** meter la capa `Obstacles` dentro del `interactableMask` del `PlayerController`, que hoy esta vacio. `PlayerController.Interact()` los busca con `GetComponentInParent<IInteractable>()`, asi que el componente puede ir en la raiz del NPC.

---

## 7. Interacciones y dialogo

La tecla de accion es la **X** (quedan Z y Enter como alias, y el boton sur del gamepad). Se define en codigo en `PlayerInputController`, no hay asset de input que tocar.

### Lo que necesita la escena

1. Un **`DialogoCanvas`**: Canvas en ScreenSpaceOverlay, `sortingOrder 100`, CanvasScaler a 512x384, con un `DialogueController` en la raiz y un `DialogueView` en la caja. Copialo de `NestorGOD2`.
2. El **`interactableMask`** del `PlayerController` tiene que incluir la capa donde viven los NPC (en NestorGOD2 es `Obstacles`). Si esta vacio, apretar X no hace nada — y no avisa.

Mientras hay un dialogo abierto, `PlayerInputController` le manda todo el input a la caja y **el jugador no se mueve**. Por eso no hay que leer teclado desde otro lado: si dos componentes escuchan la misma tecla, la X que cierra la linea vuelve a abrir el dialogo.

### NPC que pelea

`InteractableTrigger` con el campo `combate` apuntando a un `EnemyDefinition` o `BossDefinition` (Nestor es `Resources/Definitions/Torre1/Nestor.asset`). No hace falta codigo: el trigger busca solo el `BattleController`, lanza la pelea y `MapBattleBridge` se encarga de esconder el mapa y devolverlo al terminar.

Para que eso funcione la escena necesita, **fuera** de `MapaRaiz`:
- el prefab `Assets/Prefabs/Combate/SistemaDeCombate.prefab`, **desactivado**;
- un `MapBattleBridge` con `mapaRaiz`, `combateRaiz`, `battleController` y `playerController`;
- un `GameManager` y un `SceneBootstrapper` (sin bootstrap el `PlayerModel` no llega a la escena y `Interact()` hace early-return en silencio).

> **Todo lo del mundo va adentro de `MapaRaiz`** (Grid, jugador, NPCs y la camara del mapa). Es lo que el puente apaga durante la pelea. Lo que quede afuera se sigue viendo encima del combate.

### NPC que se libera y se suma al equipo

`LiberarPokenTrigger`: pregunta con dos opciones y, si aceptan, crea el `PokenModel` y lo mete en el equipo (tope 6).

- `poken` + `nivel`: que especie se suma.
- Se dispara de **dos formas**: apretando X de frente (`IInteractable`) y, si `tambienPorTrigger` esta activo, al caminarle al lado. Para lo segundo el NPC necesita un **segundo collider en Is Trigger** ademas del solido — el solido no deja que el jugador se le encime, asi que sin el trigger nunca entraria. En los prefabs es un `CircleCollider2D` de radio 0.9.
- El trigger queda desarmado hasta que el jugador se aleja, si no vuelve a preguntar apenas se cierra la caja.
- `desaparecerAlLiberar` esconde el NPC, pero **recien cuando se cierra el cartel**: si se apagara antes, moriria el callback y la caja quedaria abierta para siempre.

Prefabs listos: `NPC_Combusken.prefab` y `NPC_Basculin.prefab`.

---

## 8. Checklist antes de dar el mapa por listo

No alcanza con que los componentes estén puestos. Comprobá:

1. `Colisiones` está en la capa **`Obstacles`**.
2. El `CompositeCollider2D` tiene **`shapeCount > 0`** (si da 0, falta el toggle del punto 2b).
3. El jugador arranca en un **centro de tile** y sobre piso transitable.
3b. Todo lo interactuable es **realmente alcanzable**. Que un tile esté libre **no** quiere decir que se pueda llegar: puede ser un hueco encerrado por paredes. Comprobalo con un flood fill desde el spawn, no mirando el mapa — un botón puesto del lado equivocado de una pared parece bien puesto y no se puede apretar.
4. Camina, y **choca** contra paredes y muebles.
5. La cámara lo sigue.
6. Entrás en **Play** y sigue funcionando (el `shapeCount` sobrevive al reload).
7. Los NPCs que hayas puesto **frenan** al jugador (si los atraviesa, quedaron en capa `Default`).
8. Si hay NPC con interaccion: apretar **X** de frente abre la caja, y el jugador no se mueve mientras esta abierta.
9. La consola no tira errores nuevos.
10. La escena está en **Build Settings** si la vas a necesitar fuera del editor.

Para chequear las colisiones sin adivinar, este barrido imprime un mapa ASCII de lo que ve la física; tiene que calcar al mapa dibujado:

```csharp
Vector2 p = GameObject.Find("Player").transform.position;
int mask = 1 << LayerMask.NameToLayer("Obstacles");
for (int dy = 4; dy >= -5; dy--) {
    string row = "";
    for (int dx = -8; dx <= 8; dx++)
        row += Physics2D.OverlapCircle(p + new Vector2(dx, dy), 0.3f, mask) != null ? "X" : ".";
    Debug.Log(row);
}
```

---

## Referencia rápida

| Cosa | Valor |
|---|---|
| Cell Size / Tile Anchor | (1,1,1) / (0.5, 0.5) |
| Centros de tile | medios enteros (x.5, y.5) |
| Capa de `Colisiones` | `Obstacles` (layer 3) |
| `colliderType` de los tiles | `Grid`, por celda |
| Sorting del jugador | 15 |
| Prefab del jugador | `Assets/Prefabs/Player.prefab` |
| Prefabs de NPC | `Assets/Prefabs/NPCs/NPC_Nestor.prefab`, `NPC_Combusken.prefab` |
| Capa de los NPC | `Obstacles` (si no, no frenan) |
| Alto de referencia | jugador 2,24 u / Nestor 2,6 u / Combusken 1,7 u |
| Controles | WASD / flechas / stick, Shift para correr, **X** para interactuar |
| Canvas de diálogo | `DialogoCanvas` (copiar de NestorGOD2), sortingOrder 100 |
| Prefab de combate | `Assets/Prefabs/Combate/SistemaDeCombate.prefab`, desactivado |

Escenas de referencia ya armadas: **`NestorGOD2.unity`** (mapa + combate + diálogo + NPCs).

Ver también:
- **`Como_Hacer_Dialogos_Y_Decisiones.md`** — diálogos, preguntas de sí/no, botones y barreras por flag.
- **`Como_Correr_La_Demo.md`** — el recorrido menú → creación de personaje → torre, y el guardado con F2.
