# Las pantallas de inicio

El arranque del juego son tres escenas: **`Menu Principal`** (el título),
**`MenuSaves`** (continuar la partida) y **`CreacionPersonaje`**. El 1-oct-2026 se
pulieron para que se sientan de juego terminado y no de prototipo, **sin cambiarles
la identidad**: la pone `MenuSaves` y las otras dos la siguen.

| Pieza de la identidad | Dónde está |
|---|---|
| Fondo de la ciudad de noche | `Sprites/Menus/fondoCampeon.png` |
| Paneles grises que se vuelven celestes con el cursor | `Sprites/Menus/panels.png` (`panel_gris` / `panel_celeste`) |
| Botones grises → celestes | `panels.png` (`boton_gris` / `boton_celeste`) |
| Letra | `LegacyRuntime` en negrita, clara; títulos en celeste |
| Referencia del Canvas | 1672 × 941 (la del arte del título), match 0.5 |

---

## Qué hace cada una

### Título — `StartScreenView`

- Entra con un fundido desde transparente.
- El **PRESS START** "respira": es una copia exacta de ese pedazo del arte
  (`RawImage` con el mismo `uvRect`, anclada a la misma fracción del fondo) con
  `PulsoSuave`. Como es una copia, crece y destella sin tocar el PNG y calza en
  cualquier resolución. El recorte está medido en `PantallasDeInicioBuilder`
  (`PressStartEnElArte`): si cambia el arte, hay que medirlo de nuevo.
- Al aceptar destella (el "aceptado" clásico) y funde a negro hacia la escena que
  toque (`FundidoAEscena`). Los primeros 0,35 s no acepta nada, así el clic con el
  que se llegó no saltea la pantalla.

### Partidas — `SaveSlotMenuView`

- La tarjeta dice **CONTINUAR ›** (antes nada indicaba que había que tocarla).
- La ruta muestra **dónde va la historia** ("Próximo: el primer Data Center"),
  calculado de los flags con `RecorridoDeLaHistoria.SiguienteNivel`. Tiempo de juego
  y fecha de guardado no se guardan todavía en la base: no se muestran (un "--" se
  leía como algo roto). Cuando existan, se llenan donde dice el TODO.
- El entrenador de la tarjeta sigue el género de la partida (antes siempre el varón).
- **Borrar partida pregunta** ("¿Borrar la partida de X? No se puede deshacer.",
  BORRAR / CANCELAR) y la pregunta arranca en CANCELAR.
- **Opciones** abre la pantalla de opciones del sistema de menús (antes no hacía
  nada si la escena no traía un panel propio).
- Mientras hay un menú encima, la pantalla no recibe nada.
- Los cambios de escena funden a negro.

### Creación de personaje — `CreacionPersonajeView`

Ver también `Como_Correr_La_Demo.md`.

- Indicador de pasos arriba: **1 Quién sos · 2 Nombre · 3 Compañero · 4 ¡Listo!**
  (celeste el actual, blanco los hechos, gris los que faltan).
- **Elegir en dos tiempos**: tocar una ficha la marca (queda celeste y un poco
  más grande, las otras se apagan) y habilita "Continuar"; tocarla otra vez o
  "Continuar" sigue. Nada se decide por un clic apurado.
- "Continuar" se ve **deshabilitado** hasta que hay algo elegido o un nombre escrito.
- "Volver" en cada paso; el del primero vuelve al menú principal.
- El nombre tiene contador ("3/10") y Enter en el campo sigue.
- En el paso del inicial, pasar por una ficha (o llegar con las flechas) muestra
  la descripción; el elegido se anima con su tira de combate.
- Los paneles entran animados y, mientras cambian de paso, no se acepta nada.
- Teclado y joystick: flechas para moverse (el EventSystem), **X / Enter / A** para
  aceptar y **Z / Esc / B** para volver, con las teclas de `GameInputService`
  (remapeables). Mientras se escribe el nombre, las teclas son del campo.
- "¡Empezar aventura!" crea y guarda la partida (igual que antes) y funde a negro;
  un segundo clic no hace nada.

---

## Componentes reutilizables

Sirven para cualquier pantalla de uGUI, no solo estas:

| Componente | Qué hace |
|---|---|
| `BotonAnimado` | Estados de un botón: crece con el cursor encima o seleccionado por teclado, se achica apretado, queda marcado si está **elegido**, se apaga a medias deshabilitado. Se suma a la transición del `Button` (SpriteSwap o ColorTint). |
| `EntradaAnimada` | El panel entra deslizándose y apareciendo cada vez que se prende; `Salir()` es la inversa. Mientras entra no se puede tocar. Con `retraso` distintos se hace la entrada escalonada. |
| `PulsoSuave` | "Respira" (escala y opcionalmente transparencia). `Destellar()` lo hace parpadear rápido. |
| `FundidoAEscena` (ya existía) | Fundido a negro, carga y aclarado. |

**Ojo:** `BotonAnimado` y `EntradaAnimada` usan cada uno un `CanvasGroup`. No van
en el mismo objeto: la ficha se envuelve en un nodo que entra animado
(`Ficha_Entrada`) y el botón va adentro.

---

## Cómo se arman

`PokeRush > UI > Pulir pantallas de inicio` (`Editor/Menus/PantallasDeInicioBuilder.cs`):

- **Título y partidas**: les agrega lo suyo (la copia del PRESS START, los
  `BotonAnimado`, el "CONTINUAR ›") y cablea los campos nuevos. No mueve nada de
  lo que ya estaba.
- **Creación de personaje**: rehace el Canvas entero con las piezas de arriba.
  Conserva lo que es dato: los **iniciales** (nombre, definición, retrato,
  descripción), el **nivel** y la **escena destino** (que escribe
  `QuarchoCuevaBuilder`). Para cambiar un inicial, el Inspector del
  `CreacionPersonajeView` sigue siendo el lugar.

Se puede correr de nuevo sin duplicar nada. Si el equipo retoca a mano la
creación de personaje, **no volver a correrlo**: la rehace.
