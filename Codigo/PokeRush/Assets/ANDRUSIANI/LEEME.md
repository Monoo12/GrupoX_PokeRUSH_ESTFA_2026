# ANDRUSIANI — herramientas de bloqueo para diseñar mapas

Cosas reutilizables para tapar por dónde no se tiene que poder pasar, sin dibujar nada
y sin tocar las paletas compartidas del proyecto.

Todo el menú vive en **`ANDRUSIANI/`** en la barra de Unity.

---

## Lo que hay

| Asset | Para qué |
|---|---|
| `Tiles/ParedInvisible.asset` | El tile de pared invisible. Se pinta como cualquier otro. |
| `Paleta/ANDRUSIANI.prefab` | La paleta que lo contiene (sin paleta no se puede pintar). |
| `Prefabs/BloqueInvisible.prefab` | Caja invisible suelta: se arrastra y se escala. |
| `Runtime/`, `Editor/` | Los scripts. No hace falta tocarlos. |

| Menú | Para qué |
|---|---|
| `ANDRUSIANI > Preparar capa de paredes invisibles` | Arma la capa donde pintar, ya configurada. |
| `ANDRUSIANI > Dibujar el mapa de colisiones en la consola` | Imprime en ASCII lo que ve la física. |
| `ANDRUSIANI > Verificar que la pared choca` | Chequeo automático del tile. Da OK o error. |
| `ANDRUSIANI > Generar assets` | Regenera tile, paleta y prefab si se pierden. |

---

## Pintar paredes invisibles (el camino normal)

1. Abrí la escena del mapa.
2. **`ANDRUSIANI > Preparar capa de paredes invisibles`**. Crea `ParedesInvisibles` colgando
   del `Grid`, con collider, composite, rigidbody estático y —lo que más se olvida— la capa
   **`Obstacles`**.
3. `Window > 2D > Tile Palette`, elegí la paleta **ANDRUSIANI** y poné `ParedesInvisibles`
   como *Active Tilemap*.
4. Pintá. Se ve un aspa rosa translúcida: eso es solo el editor, **en el juego no se ve nada**.
5. Guardá la escena.

Para comprobar sin adivinar: seleccioná al jugador y corré
**`ANDRUSIANI > Dibujar el mapa de colisiones en la consola`**. Donde bloquea sale `X`.

---

## Bloques sueltos

Para lo que no cae en la grilla —media celda, una zona que hay que estirar, tapar una salida
mientras se prueba algo— arrastrá **`Prefabs/BloqueInvisible.prefab`** a la escena y escalalo.
Se ve como una caja rosa en el editor (es un gizmo) y no existe en el juego.

Tiene un toggle **`soloZona`**: en `true` deja pasar y solo dispara `OnTriggerEnter2D`, para
zonas de evento. En `false` (el default) bloquea.

---

## Por qué está hecho así

**El tile trae su propio `colliderType = Grid`.** Los tiles de `Interior1_Tiles.asset` vienen con
`colliderType = None` y hay que arreglarlos con un override por celda que se pierde en silencio
cada vez que alguien pinta o borra (todo el problema está contado en
`Documentacion/Documentacion del Codigo/Como_Armar_Un_Mapa_Nuevo.md`). Acá el collider es parte
del asset, así que no hay nada que reponer.

**En el juego se oculta con alpha 0, no borrando el sprite.** Sin sprite la celda deja de
dibujarse y la generación del collider pasa a depender de detalles de versión de Unity. Con el
sprite puesto y el color transparente la celda sigue existiendo para todos menos para el ojo.
Son cuatro triángulos invisibles por pared: no se nota.

**Bloquea por capa, no por collider.** El `PlayerController` resuelve el paso con un
`Physics2D.OverlapCircle` contra su `obstacleMask`, que mira `Obstacles`. Un tilemap o un bloque
en `Default` tiene collider perfecto y **no frena a nadie**. Por eso el menú pone la capa solo y
`BloqueInvisible` se corrige a sí mismo en `Awake` avisando por consola.

**La capa lleva `ColisionesTilemapRuntimeFix`.** Es el seguro del proyecto contra el
`CompositeCollider2D` que queda en `shapeCount = 0`: rehace la geometría en `Awake`. Si en el
editor las paredes dejan de frenar después de pintar, corré `PokeRush > Reparar colisiones del mapa`.

---

## Verificado

`ANDRUSIANI > Verificar que la pared choca` pinta 3 celdas en un tilemap de prueba y controla
las cinco cosas de una: que el composite dé formas, que el `colliderType` sea `Grid`, que el
marcador se vea en el editor, que el `OverlapCircle` del jugador **frene** contra la pared y que
**no** frene donde no hay nada. Al 27-ago-2026 da OK (`formas=1`, `frena=True`).
