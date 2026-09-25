# PC de almacenamiento

Dónde van los Pokémon que no entran en el equipo, cómo se mueven entre el equipo y las
cajas, y cómo se pone un PC en un nivel.

---

## Qué es

- **Cajas:** 8, de 6×5 (30 lugares cada una). Son 240 lugares.
- **Un PC en cada nivel jugable:** torre 1 (`NestorGOD2`), los tres pisos del casino y el piso
  final. Se interactúa con X, como con cualquier mueble.
- **Todo Pokémon que se consigue va al equipo si hay lugar y al PC si no.** Aplica a los premios
  de las ruletas, a los Pokémon liberados de la torre y al Cosmog shiny de Tacconi. Recién si el
  equipo **y** el PC están llenos, la ruleta paga plata en su lugar (`moneyIfTeamFull`).

## Cómo se usa

| Tecla | Qué hace |
|---|---|
| Flechas | Mover la mano por la caja. Arriba de todo se cambia de caja; abajo están **EQUIPO** y **SALIR** |
| Página izq./der. | Cambiar de caja desde cualquier lado |
| Aceptar sobre un Pokémon | Agarrarlo (su lugar queda a media luz) |
| Aceptar sobre otro lugar | Soltarlo ahí. Si había alguien, se intercambian |
| **EQUIPO** | Abre el panel del equipo, que se maneja igual |
| Volver | Suelta lo agarrado, cierra el panel del equipo o apaga el PC |

A la izquierda se ven los datos del Pokémon que se apunta (o del que está en la mano): nombre,
sprite, nivel, tipos, PS y si es shiny.

**Reglas** (`PcReglas`):

- El equipo tiene que quedarse con **al menos un Pokémon que pueda pelear**.
- No se pasa de 6 en el equipo.
- Agarrar es solo visual: el equipo y las cajas cambian recién al soltar, de una vez. Si el juego
  se corta con un Pokémon en la mano, no se pierde nadie.
- Al apagar el PC se guarda la partida (`PcInteractable.guardarAlApagar`).

## Piezas

| Pieza | Qué hace |
|---|---|
| `PokenStorageModel` | El modelo de las cajas. Sin Unity, se prueba solo |
| `PcReglas` / `PcLugar` | Mover de un lugar a otro (equipo o caja) validando las reglas |
| `PlayerModel.Storage` / `RecibirPokemon` | El PC del jugador y "adónde va un Pokémon nuevo" |
| `PcStorageScreen` | La pantalla (`MenuScreenId.PcStorage`) |
| `PcInteractable` | El PC del mapa: abre la pantalla |
| `PcStorageBuilder` | `PokeRush > UI > Construir la pantalla del PC`: arma el prefab y lo registra en el MenuSystem |
| `PcEnLosNivelesBuilder` | `PokeRush > Poner el PC en todos los niveles` |

## Guardado

Los Pokémon del PC van en la **misma tabla que el equipo** (`player_poken`), con dos columnas
nuevas que sqlite-net agrega sola en los guardados viejos:

- `BoxIndex`: `null` = está en el equipo (`SlotIndex` es su orden); un número = la caja
  (`SlotIndex` es el lugar dentro de la caja). Es **nullable a propósito**: en un guardado viejo
  la columna queda `NULL` y el equipo sigue siendo el equipo.
- `IsShiny`: si el ejemplar es shiny.

De paso, guardar ahora borra también los movimientos de los Pokémon viejos (antes quedaban filas
huérfanas en `player_poken_moves` en cada guardado).

## Shiny

`PokenModel.IsShiny` no toca los stats: cambia qué sprites se dibujan. Las pantallas piden los
sprites al **modelo** (`IconSprite`, `FrontSprite`, `BackFrames`...) y no a la definición, así un
shiny se ve shiny en el combate, el equipo, el resumen, la tarjeta del guardado y el PC. Si la
especie no trae sprites shiny, cae a los normales.

## Poner un PC en un nivel nuevo

1. Agregá la escena y la celda en la tabla `Niveles` de `PcEnLosNivelesBuilder` (el centro de la
   celda cae en `.5`).
2. Corré `PokeRush > Poner el PC en todos los niveles`. Verifica que la celda sea piso, que no
   haya pared ni otro objeto sólido, y que se llegue caminando desde el spawn. Si algo falla, esa
   escena no se guarda.

El sprite es `Assets/Sprites/Mundo/pc.png` (arte propio, 32×48, una celda de ancho y una y media
de alto). El arte de la pantalla es el de `Graphics/UI/Storage` del pack de Essentials.

## Cómo se prueba

- `PokeRush > Casino > Correr smoke tests del casino`: reglas del PC (dejar, retirar, intercambiar,
  no vaciar el equipo, no pasar de 6), adónde va un Pokémon nuevo y los sprites shiny.
- `PokeRush > Correr smoke tests`: guardar y cargar con Pokémon en cajas y un shiny en el equipo.
- `PokeRush > UI > Probar y capturar el PC`: entra en Play, abre el PC con Pokémon de prueba,
  agarra, suelta, cambia de caja y deja uno del equipo en una caja con las mismas acciones del
  teclado. Deja capturas en `Codigo/PokeRush/Capturas/PC/` y el resultado en el Editor.log
  (`[CapturaPC] TERMINADO. OK=7 FALLOS=0`). No guarda la partida.
- A mano: agarrar un Pokémon del equipo, dejarlo en otra caja, apagar, volver a entrar y ver que
  sigue ahí.
