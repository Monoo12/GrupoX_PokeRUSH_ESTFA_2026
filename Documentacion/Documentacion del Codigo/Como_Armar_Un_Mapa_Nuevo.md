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

## 6. Checklist antes de dar el mapa por listo

No alcanza con que los componentes estén puestos. Comprobá:

1. `Colisiones` está en la capa **`Obstacles`**.
2. El `CompositeCollider2D` tiene **`shapeCount > 0`** (si da 0, falta el toggle del punto 2b).
3. El jugador arranca en un **centro de tile** y sobre piso transitable.
4. Camina, y **choca** contra paredes y muebles.
5. La cámara lo sigue.
6. Entrás en **Play** y sigue funcionando (el `shapeCount` sobrevive al reload).
7. La consola no tira errores nuevos.
8. La escena está en **Build Settings** si la vas a necesitar fuera del editor.

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
| Controles | WASD / flechas / stick, Shift o botón este para correr |

Escenas de referencia ya armadas: **`NESTORGOD.unity`**.
