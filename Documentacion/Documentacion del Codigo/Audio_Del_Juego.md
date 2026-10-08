# El audio del juego

Qué suena en cada momento, de dónde sale y cómo se cambia. Todo pasa por **un
catálogo** (`Assets/Resources/Audio/CatalogoDeAudio.asset`): ninguna escena
tiene que arrastrar clips, y una escena nueva suena con solo llamarse como dice
la tabla.

---

## Las piezas

| Archivo | Qué hace |
|---|---|
| `Scripts/Services/AudioService.cs` | El que hace sonar todo. Dos fuentes de música con **fundido cruzado**, efectos cortos (`PlayOneShot`), efectos con tono propio (pasos, gritos), **jingles** (la música baja mientras suenan) y efectos largos que se pueden cortar (`SfxEnCurso`: la ruleta girando). |
| `Scripts/Services/MusicaDelJuego.cs` | **Qué música toca.** Por escena (por nombre, desde el catálogo), por pelea (la del rival, o una común al azar sin repetir), la fanfarria de victoria y la vuelta a la música del mapa. Se crea solo. |
| `Scripts/Services/Sonidos.cs` | **Los efectos**, uno por momento: `Sonidos.Confirmar()`, `Sonidos.Curar()`, `Sonidos.Grito(poken)`… Si falta el clip no suena nada y no hay error. |
| `Scripts/Services/Pasos.cs` | Los pasos del jugador y de los NPCs que caminan. |
| `Scripts/Data/CatalogoDeAudio.cs` | El catálogo: todos los clips, con su volumen medido. |
| `Scripts/Controllers/MusicaDeEscena.cs` | **Opcional**: una escena que lo tiene manda su propia canción (los menús y el Data Center lo traen de antes). |
| `Editor/Audio/AudioDelJuegoBuilder.cs` | `PokeRush > Audio > ...`: arma el catálogo, mide los volúmenes, corta los pasos y configura la importación. |
| `Codigo/Herramientas/copiar_audio_essentials.py` | Copia del pack de Essentials (`Assets/AudioAÑIL`, solo local) lo que el juego usa. |

### Cómo se rearma

`PokeRush > Audio > Armar todo el audio`. Se puede correr las veces que haga
falta: respeta los `volumen` retocados a mano en el catálogo y no vuelve a medir
lo que ya midió. Qué va en cada lugar está en las tablas del principio de
`AudioDelJuegoBuilder.cs`.

Para **cambiar una canción**: reemplazar el archivo dentro de su carpeta de
`Assets/Audio` (la carpeta manda: se usa la primera pista que haya adentro) y
volver a armar. Para **subir o bajar** algo: el campo `volumen` de ese sonido en
el catálogo (1 = como lo dejó el armador).

El catálogo nombra a las especies, los movimientos y los rivales **por su
nombre** (texto), no con una referencia al asset. Es a propósito: como el
catálogo se carga al arrancar, una referencia arrastraría a memoria las 445
especies con sus sprites (ver "Especies y movimientos nuevos" en el README).
Por eso, si se renombra el asset de una especie o de un rival, hay que volver
a armar el audio.

---

## Música

| Momento | Pista (`Assets/Audio/...`) |
|---|---|
| Menú principal y partidas | `Musica_MenuPrincipalySave.mp3` |
| Creación de personaje **y la habitación** (sigue sin cortarse) | `Creacion _PJ.mp3` |
| Quarcho Cueva | `Quarchocueva/` |
| Data Center de Néstor | `BackMusic_Torre1.mp3` |
| Casino, cada piso | `Casino Piso 1/`, `Casino piso 2/`, `Casino piso 3/`, `Casino Piso Final/` |
| Arena de Julieta (`BossFinal`; también `ArenaJuli_Vacia` y la escena de prueba) | `Sala BossFinal_Julieta/` |
| Pelea contra Néstor | `Pelea_Nestor.mp3` |
| Pelea contra Martín Reymar (jefe del casino) | `Pelea contra martin/` |
| **Cualquier otra pelea** (incluida la del tutorial) | una de `Peleas Random/` al azar, sin repetir la anterior |
| Al ganar una pelea | fanfarria de Essentials (`victoria_entrenador`; contra un jefe, `victoria_importante`) |
| Julieta, fase 1 (Análisis) | `Pelea contra julieta/Fase 1/` |
| Julieta, fases 2 y 3 | `Pelea contra julieta/Fase 2/` |
| Julieta, **Ruptura** (cuando ya puede caer) | `Pelea contra julieta/Fase Final casi por caer/` |
| Después de ganarle, durante el diálogo final | `After-vencer-Julieta/` |
| Créditos | `Creditos/` |

**El tema final de Julieta** arranca en la Ruptura. Para que arranque en la
fase 3 (cuando controla un Pokémon del jugador): en el `PeleaJulieta` de la
arena, campo **Fase De La Musica Final** = `ControlDelVinculo`.

**Volumen**: cada pista se midió y se le puso una ganancia para que todas suenen
parejas (la mediana de las pistas manda). Encima va el volumen de la escena
(0,5) y el que elige el jugador en Opciones. Los cambios de canción son con
fundido cruzado (0,8 s, campo `fundidoEntreCanciones` del catálogo).

---

> **Playtest del 5-oct:** la música de los pisos 1, 2 y 3 del casino sube con el retoque de
> `volumen` (1,45 / 1,5 / 1,25) y el "clinc" de aceptar baja a 0,33 (su ganancia de 3 lo dejaba
> el triple que el resto); el cursor, a 0,65. Todo en `CatalogoDeAudio.asset`.

## Efectos

| Momento | Sonido | Dónde se dispara |
|---|---|---|
| Confirmar con X en cualquier menú, título, partidas, creación y respuestas Sí/No | `Efectos/Seleccion en el menu (X)` | `MenuManager`, `BotonAnimado`, `StartScreenView`, `DialogueController` |
| Moverse por un menú | `Efectos/Menus/menu_cursor` | ídem |
| Volver / cerrar con Z | `Efectos/Menus/menu_cancelar` | ídem |
| Abrir el menú de pausa | `Efectos/Menus/menu_abrir` | `PlayerController.OpenMenu` |
| No alcanza la plata (tragaperras) | `Efectos/Menus/menu_error` | `SlotsScreen` |
| Guardar (pausa o F2) | `Efectos/Menus/guardar_partida` | `PauseMenuScreen`, `GuardadoRapido` |
| Comprar (tienda de Tixi, vendedores del casino) | `Efectos/Menus/tienda_comprar` | `ReglasDeTienda`, `CasinoVendedorNpc` |
| Prender / apagar el PC | `Efectos/Pc Sound`, `Efectos/Menus/pc_apagar` | `PcInteractable` |
| La enfermera cura | `Efectos/Curar Pokemon con enfermera` (jingle) | `NpcQueCura`, `CasinoNurseInteractable` |
| Encontrar un objeto en el mapa | `Efectos/Pokemon Item Found` (jingle) | `InteractableTrigger`, `TutorialParte` |
| Un NPC te da algo (objeto clave, plata, premio de la rueda) | `Efectos/Pokemon Item received` (jingle) | `DialogueController`, `CasinoTrainerNpc`, `QuarchoTixiNpc`, `PrizeGranter`, `EventController` |
| Recibir un Pokémon (Mew, Cosmog, liberado) | `Efectos/Mundo/pokemon_recibido` (jingle) | `PrizeGranter`, `CasinoTacconiNpc`, `LiberarPokenTrigger` |
| Salir de la cueva a un nivel y volver | `Efectos/Pokemon Teleport Sound` | `SalidaDelLobby`, `SalidaDelNivel`, `VueltaAlLobby` |
| Ascensor del casino | `Efectos/Mundo/ascensor_viaja` + `ascensor_llega` | `CasinoElevatorInteractable` |
| Puertas dentro de un mapa | `Efectos/Mundo/puerta_entrar` | `DoorTrigger` |
| Un guardia te ve ("!") | `Efectos/Mundo/exclamacion` | `NpcQueTeDetecta` |
| Chocar contra una pared | `Efectos/Mundo/choque_pared` (cada 0,38 s como mucho) | `PlayerController.Move` |

### Combate

| Momento | Sonido |
|---|---|
| Lanzar la Pokéball | `Efectos/Combate/lanzar_ball` |
| Sale el Pokémon | `Efectos/Pokeball Pokemon Out` + **el grito de la especie** |
| Retirarlo | `Efectos/Combate/retirar` |
| Cada ataque | **el sonido propio del movimiento** (`Audio/Combate/Ataques/<slug>.wav`; si tiene partes, la primera en la carga y la última en el impacto). Sin sonido propio, el de un movimiento de su tipo |
| El golpe en la barra de PS | `golpe_normal`, `golpe_super_eficaz` o `golpe_poco_eficaz` según la efectividad (la marca el `TurnEngine` en `BattleEvent.Sonido`) |
| Sube / baja una estadística | `stat_sube` / `stat_baja` |
| Quemado, envenenado, paralizado, dormido, congelado | `estado_*` |
| Pociones en combate | `usar_objeto` |
| Debilitado | el grito más grave + `debilitado` |
| Subir de nivel | `subir_nivel` (jingle) |
| Escapar | `huir` |

Los gritos son 441 (todas las especies del proyecto menos las cuatro propias:
Aquita, Bulbi, Chispa y NESTOR) y los ataques ~490 movimientos con sonido propio.
Se importan **sin precargar**: `Sonidos.PrecargarPelea` carga los de los dos
equipos al empezar cada pelea y los suelta al terminar.

### Casino

| Momento | Sonido |
|---|---|
| Tragaperras: apostar, girar, cada rodillo que frena | `slots_moneda`, `Efectos/Slots` (se corta al frenar el último), `slots_frena` |
| Tragaperras: premio (10× la apuesta o más = grande) | `premio_chico` / `premio_grande` |
| Ruleta: poner y sacar fichas, girar | `Efectos/TakingorFalling chips`, `Efectos/Ruleta NegroRojo girando` (se apaga cuando cae la bola) |
| Rueda de premios: girar | `Efectos/Rueda de premios` |
| Blackjack: fichas, cada carta, mezclar el zapato, ganar | `TakingorFalling chips`, `Taking Card Poker/...taking-playing-card`, `...shuffling-deck`, `premio_chico` |

Si alguna pantalla del casino tiene un clip asignado en su prefab, manda ese:
el catálogo es lo que suena cuando el campo está vacío (que es lo normal).

---

## Pasos

Suenan **cuando el pie apoya**: las hojas de personaje tienen cuatro cuadros
por dirección (quieto, pie izquierdo, quieto, pie derecho), y `PlayerView` y
`NpcQueCamina` llaman a `Pasos` al pasar a los cuadros 1 y 3. Al correr la
animación va más rápido y los pasos se aceleran solos. Al chocar contra una
pared no hay pasos (el personaje no se mueve).

Las grabaciones de `Efectos/footstep` se cortaron en pasos sueltos
(`Efectos/footstep/Pasos/<superficie>_N.wav`, `PokeRush > Audio > Cortar los
pasos`). Nunca suena el mismo dos veces seguidas y cada uno lleva un tono apenas
distinto.

| Escena | Superficie |
|---|---|
| habitación | laminado |
| Quarcho Cueva | laminado |
| Data Center | metal |
| Casino (los cuatro pisos) | alfombra |
| Arena de Julieta | metal |

`ejimas` (de `Ejimas1.ogg`) queda cargada en el catálogo para usarla donde
quieran: se cambia en la columna `superficie` de la tabla de escenas.

Los NPCs que caminan (Tixi, los guardias) suenan a la mitad y se apagan con la
distancia a la cámara.

---

## Importación

| Qué | Cómo se importa | Por qué |
|---|---|---|
| Música | Streaming, Vorbis 60% | Una canción de seis minutos descomprimida son ~60 MB de RAM |
| Efectos (`Audio/Efectos`) | Descomprimidos al cargar, mono, Vorbis 70% | Suenan al instante |
| Gritos y ataques (`Audio/Combate`) | Descomprimidos, mono, **sin precargar** | Son cientos: se cargan los de cada pelea |

Lo que entra nuevo a `Assets/Audio` toma estas reglas solo
(`ReglasDeImportacionDeAudio`, la primera vez; después se respeta lo que se
toque a mano). Para reaplicarlas a todo: `PokeRush > Audio > Configurar la
importacion`.

---

## El pack de Essentials (`Assets/AudioAÑIL`)

Son 351 MB y traen un `Game.exe`: **vive solo en local** (está en el
`.gitignore`, como `Assets/Graphics`). Lo que el juego usa se copió con nombres
claros a `Assets/Audio/Combate` y `Assets/Audio/Efectos/{Menus,Mundo,Combate,Casino}`
con `python Codigo/Herramientas/copiar_audio_essentials.py`; eso sí está versionado. Para
sumar un efecto nuevo del pack: agregarlo a la lista `EFECTOS` del script,
correrlo, y asignarlo en el catálogo (o en `AudioDelJuegoBuilder`).
