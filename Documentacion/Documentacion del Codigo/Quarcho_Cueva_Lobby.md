# La Quarcho Cueva (el lobby)

La cueva es el **lobby entre niveles**: se vuelve a ella al terminar cada nivel y desde ahí se sale al siguiente. Los personajes se van sumando a medida que avanza la historia.

```
Menu Principal -> Creacion de personaje -> (habitacion tutorial) -> Quarcho Cueva
  -> Data Center de Nestor -> Quarcho Cueva -> Casino -> Quarcho Cueva -> Jefe final
```

> La **habitación tutorial** es `habitacion.unity` (ver `Tutorial_Habitacion.md`): la creación de personaje lleva ahí y su puerta, a la cueva (`RecorridoDeLaHistoria.EscenaDespuesDeCrearPersonaje`). El **jefe final** todavía no existe: la salida lo dice en vez de romper (ver más abajo).

---

## Cómo se arma

El **mapa lo hizo el equipo y no se toca**. Todo lo demás lo pone un builder, igual que en los pisos del casino:

| Menú | Qué hace |
|---|---|
| `PokeRush > Quarcho Cueva > Armar todo y probar` | Los cuatro de abajo en orden, y los smoke tests. |
| `PokeRush > Datos > Completar MTs y descripciones de movimientos` | Llena `PokenDefinition.mtMoves` y `MoveDefinition.descripcion` desde el cache de PokeAPI. |
| `PokeRush > UI > Construir las pantallas de la Quarcho Cueva` | La tienda y el tutor, registrados en el `MenuSystem` (no toca las otras pantallas). |
| `PokeRush > Quarcho Cueva > Armar el lobby` | Jugador, cámara, `GameManager`, bootstrap, diálogo y el contenedor `Lobby` con los personajes. |
| `PokeRush > Quarcho Cueva > Conectar el recorrido` | El "Continuar" de la pantalla de guardados va a la cueva. La creación de personaje va a la habitación tutorial si está en Build Settings y, si no, también a la cueva. |
| `PokeRush > Quarcho Cueva > Correr smoke tests del lobby` | 126 checks de reglas, datos y escena (`[SmokeLobby]` en el Editor.log). |
| `PokeRush > Quarcho Cueva > Probar la cueva en Play` | Recorre la cueva como un jugador y deja capturas en `Capturas/Lobby/` (`[PlayLobby]`). Juega con una partida DEMO y **no guarda**. |

`Armar el lobby` es **idempotente**: borra y rehace solo el contenedor `Lobby` (y la mesa de Martín, que vive en el `Grid`). Antes de guardar verifica con un flood fill desde el spawn que cada personaje esté sobre piso libre y que se llegue a hablarle; si algo falla, **no guarda** y dice qué.

Los **textos** (diálogos) y el **balance** (precios, premios) se crean una vez y después se tocan en el Inspector: volver a correr el builder no los pisa.

---

## Quién está y cuándo

| Personaje | Celda | Aparece | Qué hace |
|---|---|---|---|
| **Tixi** | (1, 2), en el hueco entre el escritorio y la biblioteca | Siempre | Lore, tienda, te manda al siguiente nivel |
| **PC** | el escritorio de la computadora (x −3..−1, fila 2) | Siempre | El PC de siempre |
| **Néstor** | (−12, 2), entre los racks de servidores | `Casino_Completado` (en el guion se queda en su Data Center hasta recordar; antes era `Torre1_NestorLiberado`) | Tutor de MTs |
| **Martín** | sentado atrás de su mesa en el rincón de juegos, celdas (−4..−2, −4..−3), contra la pared de abajo y al lado de la máquina arcade | `Casino_Completado` | Tres ruletas y los juegos del casino |
| **Tacconi** | (−1, −4), al costado derecho de la mesa de Martín | `Casino_Completado` | Tres chances de ganarle un Mew |
| **Salida** | (6, 0), la puerta del este | Siempre (abre después de Tixi) | Al siguiente nivel |

Los que se suman con la historia se guardan **apagados** y los prende un `BarreraPorFlag` al revés que vive en `Lobby/Apariciones` (así no se ven un frame antes de que llegue el flag). La mesa de Martín es la **misma mesa de cartas del último piso del casino**: el builder copia sus seis tiles a un `Tilemap` propio (`Grid/MesaDeMartin`) en modo *Individual*, para que se ordene por altura con los personajes (Martín queda detrás de la mesa y el que juega, delante).

> Mover a alguien es cambiar la tabla de posiciones al principio de `QuarchoCuevaBuilder` y volver a correr `Armar el lobby`. Cada posición tiene su comentario con el por qué.

---

## Tixi

- **La primera vez habla sola** apenas llegás (`hablaAlLlegar`): cuenta el lore (`Resources/Definitions/Lobby/Dialogos/Lobby_Tixi_Lore`), te da un **regalo de bienvenida** y te ofrece la tienda. Recién ahí se activa `Lobby_TixiPresentado`, que es lo que abre la salida.
- Después es un menú: **Comprar / Viajar / Charlar / Nada**. "Charlar" usa el asset `charla` si se carga uno; si no, dice a qué nivel toca ir.
- La tienda vende lo que dice `Resources/Definitions/Lobby/TiendaDeTixi` (`healItemsForSale`). El precio es el `buyPrice` de cada objeto.

> ⚠️ **El lore es texto de relleno.** Lo escribe el equipo en `Lobby_Tixi_Lore`. El builder no pisa el asset.
>
> **Sprite de Tixi** (2-oct-2026): `Graphics/Characters/tixi` (hoja 4x4 de 64x82) y el retrato `Graphics/Trainers/TIXI` (160x160), sacados del arte que pasó el equipo en `Graphics/Tixi` (Gemini). Se pasaron a pixel art igual que Tacconi: achicar promediando solo la figura, paleta de ~30 colores sin difuminado (con cuatro grises fijos para las zapatillas y la cadena) y contorno negro pintado por dentro. Mide **76 px** de alto contra los ~58 del pack: el dibujo tiene unas 3 cabezas y se igualó por cabeza. La hoja de Gemini traía dos errores que se corrigieron al armarla: las filas 2 y 3 miraban las dos a la derecha (la izquierda es el espejo) y su columna 3 era una pose de frente (va la pose quieta).
>
> **Camina hasta vos.** Cuando habla sola (la presentación y cada vuelta de un nivel) se acerca caminando a una celda pegada al jugador, lo mira, habla, y cuando ya no queda **ninguna caja ni menú abiertos** vuelve a su lugar. Mientras tanto el jugador queda frenado (`PlayerInputController.Frenar`). Si le hablás vos ya estás al lado: solo se da vuelta y al terminar vuelve a mirar abajo. El caminar es `NpcQueCamina` (BFS sobre la capa Obstacles, 4 cuadros por dirección, 3,5 u/s): sirve para cualquier NPC con hoja 4x4.
>
> **Regalo de bienvenida: 1000.** Con los 100 de arranque no alcanza ni para una Poción, y el pedido es que antes del primer nivel se pueda comprar. Es el campo `regaloDeBienvenida` de Tixi; en 0 no da nada.

Se agregaron dos objetos para la tienda: **Revivir** (revive al 50%) y **Cura Total** (cura cualquier estado), con los íconos del pack.

---

## Néstor: el tutor de MTs

**Enseñar una MT** → elegís un Pokémon del equipo → pantalla del tutor (arte de `Graphics/UI/Move Reminder`).

- Lista **las MT y los movimientos de tutor que esa especie aprende en los juegos originales** (PokeAPI), sin los que ya sabe ni los que el combate no implementa. Están en `PokenDefinition.mtMoves`, en orden alfabético: 440 de las 441 especies tienen (Cosmog no aprende ninguna que exista en el proyecto), 44 de promedio.
- A la derecha: categoría, potencia, precisión, PP y la **descripción en español** (`MoveDefinition.descripcion`, el texto de los juegos).
- Si sabe menos de cuatro, lo suma. Si sabe cuatro, pasa a **"¿Qué olvida?"**: sus cuatro movimientos y el nuevo abajo; elegir el nuevo (o Volver) vuelve a la lista sin cobrar. El que aprende ocupa **el mismo lugar** y llega con los PP llenos.
- **Siempre pregunta el precio antes de cobrar.** Precios por escalón de potencia (`PreciosDelTutor`, en el Inspector de Néstor):

| Movimiento | Precio |
|---|---|
| De estado | 800 |
| Potencia < 60 | 600 |
| 60–89 | 1000 |
| 90–109 | 1500 |
| 110 o más | 2000 |

> Los datos salen **solo del cache** (`Tools/PokeApiCache`); lo que no está cacheado se saltea con aviso, no se descarga nada. El importador completo (`PokeApiAssetGenerator`) también los llena, así que volver a importar no los borra.

---

## Martín: ruletas y juegos del casino

El menú: **Ruleta de Pokémon / de objetos / de mejoras**, **Juegos del casino**, Charlar, Nada. Se habla con él apretando X contra su mesa desde cualquier lado.

| Ruleta | Precio | Qué da |
|---|---|---|
| Pokémon | 400 | Siempre un Pokémon Nv 28–30 (rareza por el ícono, igual que las del casino) |
| Objetos | 150 | Curativos, Revivir, Cura Total o plata |
| Mejoras | 250 | Una mejora para **la próxima batalla** |

Las tres son `CasinoPrizeTable` en `Resources/Definitions/Lobby/` y abren la **misma ruleta de premios** del casino.

**Juegos del casino** abre la tragaperras, la mesa de ruleta y el blackjack: son los **mismos componentes** de los pisos (`SlotMachineInteractable`, `RouletteTableInteractable`, `BlackjackTableInteractable`), colgados de `Martin/Juegos` con el collider apagado; Martín solo les pasa la posta. No tienen flags de objetivo: acá no destraban nada.

### Las mejoras (buff por una batalla)

- Cada gajo sube un stat de **todo el equipo** durante **una sola batalla**: la próxima que pelees, sea cual sea.
- Se aplican a **cada Pokémon que entra al campo** en esa batalla (los stages viven en `BattlePokenState`, que se recrea en cada cambio; si solo se aplicaran al primero, la mejora se perdería al primer cambio).
- Se acumulan: dos veces +1 Ataque es +2 (tope +6). "+1 a todo" sube ataque, defensa, especiales y velocidad.
- El combate las anuncia una vez al arrancar ("¡La ruleta de Martín! Tu equipo pelea esta batalla con +1 Ataque.") y se **gastan al empezar** esa batalla (pasado el chequeo de equipos vacíos, así una pelea que no arranca no se las come).
- **Se guardan con la partida** (columna `PendingBuffs` de `player`, como texto: `Ataque:1;Velocidad:2`). Los guardados viejos quedan en null y se leen como ninguna.

Código: `MejorasDeBatalla` (reglas puras), `PlayerModel.MejorasPendientes`, `BattleModel.MejorasDelJugador`, `Prize.mejora` y `PrizeGranter`.

---

## Tacconi: tres chances por un Mew

Es el **mismo `CasinoTacconiNpc` del piso 3** con otro premio y con tope:

- Una mano de blackjack apostando un Pokémon. **Si perdés, Tacconi se queda con tu Pokémon y gastás una chance**; el empate no gasta; si ganás, te llevás el **Mew (Nv 30)** y se termina.
- Las chances son los flags `Lobby_TacconiChance1..3`; gastadas las tres, no juega más. El Mew ganado es `Lobby_TacconiMewGanado`.
- Como en el casino, no acepta la apuesta si perdiéndola te quedás sin nadie que pueda pelear.

En el casino `chances` queda vacío y se juega sin tope, como siempre.

---

## La salida y la vuelta

- **Una sola puerta para todos los niveles.** Adónde lleva lo deciden los flags (`RecorridoDeLaHistoria.SiguienteNivel`), no la puerta. Tixi ("Viajar") y la puerta dicen y cargan lo mismo (`ViajeEntreNiveles`).
- La puerta es una **zona para pisar**: trigger en la capa `Default` (en `Obstacles` frenaría al jugador). Detrás hay un tope invisible para no salir caminando al vacío.
- Antes de hablar con Tixi no abre.
- Si la escena del nivel no está en Build Settings (hoy, `BossFinal`), avisa que el camino todavía no está abierto.

**La vuelta a la cueva** la hace `VueltaAlLobby`, que se crea solo al arrancar el juego y vive entre escenas: si en una escena de un nivel se enciende el flag de "completado" de ese nivel (`Torre1_NestorLiberado`, `Casino_Completado`), espera a que no haya combate, cartel ni menú abiertos, dice "¡Nivel superado!" y carga la cueva. **No hace falta tocar NestorGOD2 ni los pisos del casino.** Volver a entrar a un nivel ya completo no expulsa a nadie.

**Excepción (28-sep-2026):** si la escena tiene una `SalidaDelNivel` (el Data Center de Néstor), `VueltaAlLobby` no hace nada y el jugador vuelve caminando por la salida, que se prende recién con el nivel terminado. Tixi además cuenta sola la vuelta de cada nivel (campo `alVolver`) y tiene su charla (`Lobby_Tixi_Charla`). Ver `Historia_En_Los_Niveles.md`.

> Como `SaveService` no guarda la escena, **"Continuar" siempre arranca en la cueva**, y la salida igual sabe adónde ir.

Agregar un nivel es sumar una entrada a `RecorridoDeLaHistoria.Niveles` (nombre, escena de entrada, escenas del nivel y flag de completado). Cuando exista el jefe final, alcanza con que su escena se llame `BossFinal` (o cambiar ese nombre ahí) y esté en Build Settings.

---

## Diálogos con más de dos opciones

Los menús de los NPCs usan `DialogueController.StartChoice(hablante, retrato, pregunta, opciones, alElegir)` (o `CasinoDialogo.Elegir`). El callback recibe el índice; **Escape elige la última**, que por convención es "Nada".

- Con dos opciones la caja se ve **exactamente igual que antes** (el Sí/No de siempre).
- Con más, `DialogueView` clona el último renglón y pone la ventana de opciones **arriba de la caja**, a la derecha, con el ancho de la opción más larga.
- Si no entran todas (una ventana de juego muy ancha deja el Canvas bajito), muestra las que entran y la lista **se corre con la selección**; `^` y `v` avisan que hay más.

---

## Trampas que ya mordieron

- **Un builder que lee assets de OTRA escena y después abre una escena en modo Single los pierde:** Unity descarga lo que no usa ninguna escena abierta y la referencia llega nula. Pasó dos veces: el equipo de respaldo (la partida DEMO arrancaba con cero Pokémon) y la mesa de Martín (quedaba sentado en el aire). Solución: guardar **rutas** y recargar, o leer con la escena destino **ya abierta**.
- **La pregunta del `MessageBox` arranca en NO** a propósito (machacar Aceptar no contesta que sí). Para comprar o pagar una MT hay que mover a SÍ. Las pruebas automáticas lo tienen que hacer también.
- El `NPC_Nestor.prefab` trae el `InteractableTrigger` de la **pelea** de la torre: por eso el Néstor de la cueva se arma a mano. Dos `IInteractable` en el mismo objeto no se sabe cuál contesta.

---

## Al commitear: arte del pack que hay que subir a mano

`Assets/Graphics` está en el `.gitignore` (el pack vive en local). Estos PNG los usa la cueva o sus pantallas y hay que subirlos **con su `.meta`** (y el `.meta` de la carpeta si falta), o en GitHub las referencias quedan rotas:

```
git add -f "Codigo/PokeRush/Assets/Graphics/Characters/tixi.png"* \
  "Codigo/PokeRush/Assets/Graphics/Trainers/TIXI.png"* "Codigo/PokeRush/Assets/Graphics/Trainers/MARTIN.png"* \
  "Codigo/PokeRush/Assets/Graphics/Items/"{FULLHEAL,REVIVE,XACCURACY,XATTACK2,XDEFENSE,XSPATK,XSPDEF,XSPEED}.png* \
  "Codigo/PokeRush/Assets/Graphics/UI/Mart"* "Codigo/PokeRush/Assets/Graphics/UI/Move Reminder"* \
  "Codigo/PokeRush/Assets/Graphics/UI/category.png"* "Codigo/PokeRush/Assets/Graphics/Windowskins/choice 1.png"*
```

`Sprites/Casino/martin_sentado.png` (el busto de Martín) todavía no está versionado: se sube junto con el trabajo del último piso del casino.

---

## Pendiente (del equipo)

- El **lore** de Tixi y las charlas de Néstor y Martín (`Resources/Definitions/Lobby/Dialogos`): hoy es texto de relleno.
- La escena del **jefe final** (`BossFinal`).
- La **música** de la cueva: no hay pista propia, sigue sonando la de la escena anterior.
- Las pantallas de la tienda y del tutor son **de prueba** con el arte del pack: las finales las diseña el equipo.
