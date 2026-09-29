# Cómo correr la demo

Recorrido completo: **Menú principal → Creación de personaje → Habitación (el tutorial) → Quarcho Cueva (el lobby) → Torre (NestorGOD2) → Quarcho Cueva → Casino → Quarcho Cueva**.

Para probarla, abrir **`Assets/Scenes/Menu Principal.unity`** y darle Play. Las escenas ya están en Build Settings, con el menú en el índice 0.

---

## El recorrido

```
Menu Principal
   │  no hay partida guardada ──> CreacionPersonaje ──> habitacion ──> Quarcho-Cueva
   │  hay partida guardada ─────> MenuSaves ──────────> Quarcho-Cueva

Quarcho-Cueva ──(la puerta del este o Tixi)──> el siguiente nivel
el nivel terminado ──(VueltaAlLobby, solo)──> Quarcho-Cueva
```

Quién decide qué:

- **`StartScreenView`** (en `Menu Principal`): `sceneNewGame = "CreacionPersonaje"`, `sceneWithSave = "MenuSaves"`. Mira si existe un guardado y manda a una u otra.
- **`SaveSlotMenuView`** (en `MenuSaves`): `sceneJuego = "Quarcho-Cueva"`. Como el guardado no guarda la escena, "Continuar" siempre arranca en la cueva.
- **`CreacionPersonajeView`**: `escenaDestino = "habitacion"`, la habitación tutorial. Su puerta lleva a la cueva (ver `Tutorial_Habitacion.md`).
- **A qué nivel se sale de la cueva** lo decide `RecorridoDeLaHistoria` según los flags de la partida, y **la vuelta** a la cueva al terminar un nivel la hace `VueltaAlLobby`. Todo eso está en **`Quarcho_Cueva_Lobby.md`**.

> Si agregás una escena nueva al recorrido, **acordate de sumarla a Build Settings**. `SceneManager.LoadScene` por nombre falla si la escena no está en la lista, y el error se ve recién en runtime.

---

## Creación de personaje

Tres pasos y una confirmación, todo en una escena (`CreacionPersonajeView` prende y apaga un panel por paso, así se puede volver atrás sin recargar):

1. **Género** — Varón o Mujer, con los sprites `POKEMONTRAINER_RojoNeutro` y `POKEMONTRAINER_HojaNeutra`. El click elige y avanza. **El género cambia la skin en el mapa**: `PlayerView` tiene un segundo juego de animaciones (`walkMujer` / `runMujer`) y las usa si la partida es de una jugadora. Las hojas están en `Sprites/Player/POKEMONTRAINER_HojaNeutra_walk_2.png` y `_run.png`, sliceadas 4x4 igual que las del varón.
2. **Nombre** — máximo 10 caracteres. No deja seguir vacío.
3. **Inicial** — Lucario, Gengar o Greninja, con su descripción. Entra al equipo en **Nv20**, igual que los que se liberan en el mapa.
4. **Confirmación** — resumen y el aviso de que **se guarda con F2**.

Al apretar "Empezar":

```
GameManager.NewGame(nombre, genero, inicial, nivel)   // crea la partida
GameManager.SaveGame()                                 // la guarda YA
SceneManager.LoadScene("Quarcho-Cueva")
```

Se guarda en el acto a propósito: si el jugador cierra el juego sin llegar a apretar F2, igual queda una partida para continuar desde el menú.

### Cambiar los iniciales

Están en el array **`iniciales`** del `CreacionPersonajeView` (en el `Canvas`). Cada entrada tiene nombre, `PokenDefinition`, retrato y descripción. Agregar o cambiar uno es tocar el Inspector; el nivel sale de `nivelInicial` (hoy 20).

> **Pelipper no existe en el proyecto.** No hay `PokenDefinition` ni sprite: la lista curada tiene 445 especies y no está entre ellas, tampoco en el pack `Graphics/Pokemon`. Está puesto **Greninja** en su lugar. Para tener Pelipper hay que crear la definición y conseguirle los sprites.

---

## Guardado

- **F2 guarda en cualquier momento** (`GuardadoRapido`, en el objeto `Sistema` de `NestorGOD2`). Avisa por la caja de diálogo.
- No guarda si hay un diálogo abierto: guardar en el medio de una decisión dejaría el save a mitad de camino.
- Se guarda en SQLite (`SaveService`): nombre, **género**, dinero, equipo completo con niveles y movimientos, inventario y flags de historia.

El género se agregó como columna `Gender` en la tabla `player`. sqlite-net la agrega sola en las bases que ya existían; los guardados viejos quedan en null y se leen como `Varon`.

---

## Música

Cada escena dice qué suena con un componente **`MusicaDeEscena`** en un objeto `Musica`. La reproducción la hace `AudioService`, que vive en el `GameManager` y sobrevive los cambios de escena: por eso no hay un `AudioSource` por escena, solo se le dice **qué** sonar.

| Escena | Pista |
|---|---|
| Menu Principal / MenuSaves | `Musica_MenuPrincipalySave.mp3` |
| CreacionPersonaje | `Creacion _PJ.mp3` |
| NestorGOD2 (mapa) | `BackMusic_Torre1.mp3` |
| Combate contra Néstor | `Pelea_Nestor.mp3` |

El menú y la pantalla de guardados comparten pista **a propósito**: al pasar de una a otra la música no se reinicia, porque `AudioService.PlayMusic` ignora el pedido si ya está sonando ese clip.

La música de combate se cambia sola: `MusicaDeEscena` se engancha a los eventos del `BattleController` y vuelve a la del mapa al terminar la pelea. No hay que tocar el `MapBattleBridge`.

### Dos cosas del import que importan

Las cuatro pistas están importadas como **Streaming** con **Preload Audio Data** prendido, y conviene dejarlas así:

- **Streaming**: son de 95 a 293 segundos. Con `DecompressOnLoad` se descomprimen enteras en memoria; en streaming se leen del disco a medida que suenan.
- **Preload prendido**: sin esto, el `Play()` del arranque de la escena **no hace nada y no avisa** — el clip queda asignado y en silencio.

> Un clip en Streaming **nunca** pasa al estado `Loaded`. Si alguna vez hace falta esperar a que un clip esté listo, no se puede usar `AudioClip.loadState`: hay que mirar si la fuente está sonando. `MusicaDeEscena` reintenta un par de veces y avisa por consola si no arrancó.

---

## Qué se puede hacer dentro de la torre

- Moverse con **WASD / flechas**, correr con **Shift**.
- **X** para interactuar.
- **Néstor** pregunta si querés pelear; si aceptás arranca el combate contra el boss.
- **Basculin** está en la sala inicial y se puede liberar: se suma al equipo en Nv20.
- **Dos Pokéballs en la sala de Néstor**, una a cada lado del escritorio: la izquierda da **3 Pociones** y la derecha **2 Superpociones**. Se agarran con X y no vuelven a aparecer (cada una tiene su flag).
- **El panel del rack** en la sala de servidores corta la energía y apaga los rayos del pasillo.
- Detrás de los rayos, subiendo por el pasillo, está **Combusken**, que también se libera.
- **F2** guarda.

---

## Si algo no anda

| Síntoma | Causa casi segura |
|---|---|
| El jugador atraviesa las paredes | se perdieron los `colliderType` del tilemap. En el juego lo repone solo `ColisionesTilemapRuntimeFix`; en el **editor** correr `PokeRush > Reparar colisiones del mapa` |
| Apretar X no hace nada | el `interactableMask` del `PlayerController` está vacío, o el NPC no está en `Obstacles` |
| "No hay GameManager" | entraste directo a una escena sin `GameManager`; entrá por `Menu Principal` |
| El combate no arranca | falta el prefab `SistemaDeCombate` (desactivado) o el `MapBattleBridge` en la escena |
| La escena no carga por nombre | no está en Build Settings |
| No se escucha la música | el `GameManager` de esa escena no tiene asignadas sus dos `AudioSource`. Ojo que el que manda es el de la **primera** escena, porque es `DontDestroyOnLoad` |
| La música se corta al rato, probando en el editor | no es un bug: **Run In Background** está apagado, así que Unity pausa el juego entero (audio incluido) cuando el Editor pierde el foco |

Detalle de cada sistema en `Como_Armar_Un_Mapa_Nuevo.md`, `Como_Hacer_Dialogos_Y_Decisiones.md` y `Quarcho_Cueva_Lobby.md` (el lobby).
