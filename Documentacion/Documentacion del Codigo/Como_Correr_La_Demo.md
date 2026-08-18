# Cómo correr la demo

Recorrido completo: **Menú principal → Creación de personaje → Torre (NestorGOD2)**.

Para probarla, abrir **`Assets/Scenes/Menu Principal.unity`** y darle Play. Las cuatro escenas ya están en Build Settings, con el menú en el índice 0.

---

## El recorrido

```
Menu Principal
   │  no hay partida guardada ──> CreacionPersonaje ──> NestorGOD2
   │  hay partida guardada ─────> MenuSaves ──────────> NestorGOD2
```

Quién decide qué:

- **`StartScreenView`** (en `Menu Principal`): `sceneNewGame = "CreacionPersonaje"`, `sceneWithSave = "MenuSaves"`. Mira si existe un guardado y manda a una u otra.
- **`SaveSlotMenuView`** (en `MenuSaves`): `sceneJuego = "NestorGOD2"`.
- **`CreacionPersonajeView`**: `escenaDestino = "NestorGOD2"`.

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
SceneManager.LoadScene("NestorGOD2")
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

Detalle de cada sistema en `Como_Armar_Un_Mapa_Nuevo.md` y `Como_Hacer_Dialogos_Y_Decisiones.md`.
