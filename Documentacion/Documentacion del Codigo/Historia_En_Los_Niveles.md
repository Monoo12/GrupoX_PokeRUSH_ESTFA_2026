# La historia en los niveles

Cómo está contada la historia de PokeRush dentro de cada nivel jugable: qué pasa
en cada lugar, quién dice qué, qué cambia según lo que hizo el jugador y dónde se
edita cada texto.

**Fuente canónica:** `historia.pdf`. La versión del 28-sep-2026 llegaba hasta el
ascensor al piso 4 del casino; la del **4-oct-2026** completa la historia: el
laboratorio de Reymar, la vuelta a la cueva, la sede de la Liga, la pelea contra
Julieta, el epílogo y la última escena en la cueva. Lo que el PDF dice está
implementado casi textual (pasado a voseo, como el resto del juego). Lo que el PDF
no cubre (los NPCs secundarios, lo que pasa **adentro** de la pelea final) se
escribió para que encaje con él (`Jefe_Final_Julieta.md`).

---

## El recorrido, contado

| Momento | Dónde | Qué pasa (resumen) |
|---|---|---|
| Despertar | `habitacion` | Un sueño de 2026 en fragmentos. Un Fletchling trae la carta (`print("No confíes en la Liga")`...), algo chispea en la nuca, llega {INICIAL} en una QuerryBola. Queda la **Carta anónima** en la mochila. |
| El cuarto | `habitacion` | Cada mueble del tutorial cuenta algo de 2031: la mochila con los apuntes de Redes llenos de polvo, el PC con **2031** y la carpeta `virus_ameghino` **vaciada por alguien más**, el diario con el plan de la graduación, la tele con el noticiero de la Liga y la sonrisa de Julieta, la compañera con chip que no se acuerda del Ameghino. |
| La puerta | `habitacion` | La decisión del guion: **Investigar** (seguir el tutorial) o **Salir ya** (en pijama). |
| El camino | sobre negro, al salir | Pokémon con chips, cápsulas en las ventanas, **Cruz y el Arturito** (Dialogar / Huir / Atacar; dialogando se puede **Mentir** o **Atacar**). Patear al Arturito da la **RaspBerry PY**. El viejo Ameghino y la **cerradura NFC**: usar la RaspBerry, **hackear** (pregunta de Redes: ¿en qué capa trabaja un router?) o **explorar** la escuela (la tarjeta de Pato). |
| Tixi | `Quarcho-Cueva` | El PDF: "conque recibiste mi carta", la onda de hace cinco años, los chips, "son su mano derecha", **¿Y vos?** (cómo sobrevivió, la inmunidad) o **¿Y ahora?**, las QuerryBolas (explica el tope de seis del equipo y el PC), los cuatro servidores, "volvé cuando seas nivel 10". **No nombra a Néstor**. |
| Data Center | `NestorGOD2` | Ocho personas, cuatro cosas para leer y dos momentos al pisar (ver abajo). Néstor antes de pelear (el PDF + un "Alumno... Usuario" que se le escapa) y después (el PDF entero: la desconexión, los recuerdos, "están conectados", el **Protocolo Néstor**, "no confíes en nadie que tenga un chip"). **Se apagan las luces** y el jugador sale caminando por la recepción. |
| Vuelta 1 | `Quarcho-Cueva` | Tixi cuenta sola la vuelta del PDF ("¿A Néstor? ¿Y volviste en una pieza?", "entonces funcionó", el Protocolo, el mapa con el **SERVIDOR 01: OFFLINE**, "¿eso es un casino?"). Si el jugador leyó el memo de J., se lo cuenta. |
| Casino piso 1 | `Casino Nivel 1` | La **mujer que perdió cinco años** (el PDF) y el ambiente: el anciano del aro dorado sobre el Ameghino, el turista que cree que sigue siendo 2026, el chip que hace "clin" cuando perdés, las máquinas que de noche dicen SINCRONIZANDO. |
| Casino piso 2 | `Casino Nivel 2` | El **croupier** de la primera mesa (el PDF: "¿Número?", la tarjeta, Tacconi). Después, el **jugador** que cuenta que Tacconi estaba en el Ameghino. La **secretaria** reconoce a su hermano si hablaste con el técnico del Data Center. |
| Casino piso 3 | `Casino nivel 3` | **Tacconi** (el PDF): "¿Te acordás de 2026?", los tres grupos afectados, "vos no aparecés en ninguno". La mano de blackjack por su shiny y la **Tarjeta de acceso P4**. Al ganar: "eras parte del desarrollo", "buscá tu propio nombre", el ascensor al piso 4. |
| Casino piso 4 | `Casino piso FINAL` | Los guardias (cada uno distinto: la Directora que mira por sus ojos, el zumbido detrás de la pared...). **Reymar** antes (el PDF: el laboratorio que el casino tapa, "participé en tu preparación", el LINK que leía tu cerebro sin destruirlo, "el sistema todavía me reconoce como administrador") y después (el PDF: "¿qué año es?", el laboratorio construido antes que el casino, **ACCESO DENEGADO** porque Julieta cambió los permisos, el PJ destruye el núcleo, **SERVIDORES RESTANTES: 01**). Al final deja la ruleta y va a buscar a Tixi. Desde el playtest del 4-oct va **de a tramos**: cuatro zonas en el camino a la mesa (`Casino_Reymar_Tramo1..4`), lo último al pisar la zona de la pelea (`Antes`), la charla al ganarle (`Despues`) y lo de la computadora y el núcleo atrás de la mesa (`Casino_Reymar_Servidor`). Ver `Casino_Piso_Final.md`. |
| Vuelta 2 | `Quarcho-Cueva` | Tixi (el PDF): "¿Reymar?", desconectar devuelve los recuerdos, los datos del PJ que no se pudieron ver, **queda uno: la sede central de la Liga, en el Ameghino**, conectado directo con el sistema principal. Le da el **Transmisor** ("voy a intentar abrirte el acceso") y le pide que le pregunte a Julieta qué pasó realmente en 2026; desde el 4-oct eso pasa **cuando el PJ se va** de la cueva (puerta o viaje con Tixi), no pegado a la charla. Recién ahora están en la cueva **Néstor**, **Martín** y **Tacconi**. |
| La sede | `BossFinal` | La arena de Santiago. Tres momentos al pisar (`ZonaDeHistoria`): la llegada al Ameghino remodelado ("Tanto tiempo..."), la **alarma** al entrar (INTRUSO DETECTADO, pulsos rojos) y la puerta del servidor principal (ACCESO EXCLUSIVO: JULIETA). |
| Julieta | `BossFinal` | La charla del PDF antes de pelear ("De la misma forma que siempre. Peleando."), la pelea por fases, y después el final entero del PDF: para qué era el LINK, "te estudiamos", Tixi intentó detenerlos, el PJ destruye el servidor, **se apagan las luces**, el chip de Julieta se apaga ("Estoy sola en mi propia cabeza"). Ver `Jefe_Final_Julieta.md`. |
| El final | `Quarcho-Cueva` | El **epílogo** sobre negro (la radio, la gente recordando, "el mundo vuelve a escuchar su propio silencio"), la última charla con Tixi ("Que vos estabas ahí", "Podemos decidir nosotros", "Por 2026") y la **última conexión** en una pantalla: *NOMBRE EN CLAVE: VENTRESCA AI*. Después, los créditos. |

---

## Lo que se agregó al sistema de diálogos

Nada se reemplazó: `DialogueDefinition` y `DialogueController` tienen campos y
reglas nuevas, todas opcionales. Un asset viejo se sigue viendo igual.

### En las líneas

| Marca | Qué hace |
|---|---|
| `[TIXI] Hola.` | La línea la dice TIXI (nombre y retrato), no el hablante del asset. El retrato sale del asset si es el mismo nombre, o de `otrosHablantes`. |
| `[PJ] ¿Qué?` | La dice el jugador: su nombre en mayúsculas, sin retrato. |
| `[] Llueve.` | Narrador: caja sin nombre. |
| `{PJ}` | El nombre del jugador. |
| `{INICIAL}` | El primer Pokémon del equipo (el inicial, al principio). |

Una línea sin corchete al principio se muestra como siempre ("LUCHAR: ..." no se toma como hablante).

### En el asset

| Campo | Para qué |
|---|---|
| `otrosHablantes` | Retratos de los que hablan con `[NOMBRE]`. |
| `variantes` | Otras líneas según la partida: cada una pide hasta dos flags (`requiere`, `requiereTambien`) y puede excluir uno (`excluye`). **Gana la primera que se cumple**; si ninguna, van las `lines`. Cada variante tiene su propio flag al terminar. Así un NPC cambia después de un evento. |
| `otorgaObjeto` | Un objeto clave que se entrega al terminar (una sola vez). |
| `pregunta` + `respuestas` | Al terminar las líneas, una pregunta. Cada respuesta puede pedir un flag o un objeto para aparecer, encender un flag y seguir con otro diálogo. Con dos respuestas la caja usa la ventanita del Sí/No: **opciones cortas**. |
| `cancelarElige` | Qué respuesta elige Escape (-1 = la última). Que no sea la más drástica: con Cruz, Escape es huir o mentir, nunca atacar. |
| `siguiente` | Si no hay pregunta, el diálogo que sigue. |

Un diálogo **sin nada que decir** en la partida (sin líneas y sin variante que se
cumpla) no abre la caja: pasa directo a lo que sigue. Sirve para tramos que solo
suenan si pasó algo antes (el memo en la vuelta de Tixi, lo de Cruz en el
guardia). El callback de `StartDialogue` se llama al final de **toda** la cadena.

### Componentes nuevos o con campos nuevos

| Componente | Qué hace |
|---|---|
| `ZonaDeHistoria` | Un momento que pasa al **pisar** un lugar (trigger en la capa **Default**). Suena una vez: la marca el flag de su diálogo. |
| `SalidaDelNivel` | La salida de un nivel **terminado**, para volver caminando a la cueva. Si una escena la tiene, `VueltaAlLobby` no saca al jugador solo. Un `BrilloInteractuable` en un hijo la marca recién cuando abre. |
| `ApagonDelNivel` | Las luces bajan cuando se enciende un flag (parpadeo y luz de emergencia). Es un velo en el mundo: no tapa la caja ni los menús. |
| `BrilloInteractuable` | Nuevo `flagQueLoPrende`: brilla recién con ese flag (la salida). |
| `InteractableTrigger` | `dialogoAntes` / `dialogoAntesRepetido` (antes de la pregunta), `dialogoAlGanar` (con el mapa de vuelta, en vez del cartel), `dialogoYaHecho` y `hechoConFlag` (para que un jefe vencido no vuelva a ofrecer pelea). |
| `QuarchoTixiNpc` | `alVolver`: charlas que Tixi dice sola al llegar (la primera que tenga algo para decir). |
| `CasinoTacconiNpc` | `presentacion`, `despuesDeGanar`, `charlaYaGanado`. |
| `CasinoJefeFinalNpc` | `dialogoAntes` (la primera vez) y `dialogoDespues` (al ganarle). |
| `RouletteTableInteractable` | `charlaDelCroupier`: la primera vez, en vez de abrir la mesa, habla el croupier. |
| `PuertaDelTutorial` | `camino`: al salir funde a negro y cuenta el viaje antes de cargar la cueva. |

---

## El Data Center de Néstor

Lo arma **`PokeRush > Historia > Poblar el Data Center de Nestor`**
(`Editor/Historia/HistoriaDataCenterBuilder.cs`), dentro de
`MapaRaiz/HistoriaDataCenter`. Borra y rehace **solo ese contenedor** (y los campos
de historia de Néstor, como override de la escena); verifica que cada uno esté
sobre piso libre, que nadie esté parado **arriba de un mueble** y que se llegue
caminando, y repone los Grid de Colisiones antes de guardar.

> **Nadie va parado sobre un mueble.** Las sillas (`Decorado`) y el sillón
> (`Techos`) no frenan, así que para la verificación vieja eran "piso libre", pero
> `Decorado` se dibuja debajo de los personajes: la recepcionista y la operaria
> quedaban subidas a su silla (arreglado el 29-sep). Ahora `Verificar` lo rechaza,
> salvo un NPC marcado `Acostado`. Tampoco conviene un mueble justo al **sur** del
> NPC: le queda bajo los pies (eso no lo verifica: mirarlo en una captura).

| Quién / qué | Dónde | Antes de la caída | Después | Para qué está |
|---|---|---|---|---|
| Recepcionista | atrás de su silla y del mostrador (se le habla desde el mostrador) | Guion de bienvenida robótico; el lector no reconoce tu chip; "Guardián de turno: [DATO RESTRINGIDO]" | Se acuerda de su florería | Presenta el lugar sin nombrar a Néstor; efecto del chip |
| **Enfermera** | recepción, al lado del PC (4,1), mirando al que entra | "Enfermería de guardia... una herida es una herida." **Cura al equipo gratis** (PS, estados y PP) con un destello de posta | Se acordó del juramento de cuando se recibió: "hoy curo porque quiero" | La curación del primer nivel (`NpcQueCura`, misma curación que la enfermera del casino) |
| Operaria | junto a la silla de la primera terminal del salón (-6,15), mirándola | Te toma por el turno nuevo y explica firewall, cámaras, rastreo y energía | Tiene hambre por primera vez en cinco años | Explica la mecánica de los subsistemas de forma diegética |
| Técnico | al fondo del pasillo entre racks (36,16), revisando el último | El rack caliente que no figura en planos; **su hermana trabaja en un casino** y habla del piso 4 | Se acuerda de su hermana | Pista del piso 4; continuidad con la secretaria del casino |
| Entusiasta | sala de descanso, delante de las mesas | Aprendió redes en una siesta; "la Directora nunca se equivoca" | Se le borró todo lo aprendido: "¿solo nos lo prestaba?" | La mirada a favor del LINK; siembra "el LINK no enseña" |
| Empleado dormido | acostado en el sillón (`Acostado`: el sprite de frente girado 90°, dibujado sobre el sillón) | "Estoy descargando Excel avanzado..." | "¿Qué AÑO es?" | Humor |
| Guardia de la Liga (el pelado) | sala de control, la del panel de la energía auxiliar, al lado de la puerta | **Evento**: **te ve apenas pisás la sala** (le sale el "!", se da vuelta y habla solo, una sola vez). El sistema le ordena buscar a alguien sin chip... y te pregunta a vos. Sabe lo que hiciste con Cruz | Recibió la última orden y no sabe qué hacer un martes (ya no vigila: se le habla con X) | Tensión, humor, continuidad |
| Bióloga | junto al tanque de Basculin | Pokémon que pelean "por protocolo"; señala el panel de las cámaras; sueña con un aula | Los Pokémon del tanque se durmieron | Pokémon afectados; pista para liberar a Basculin |
| Porygon con chip | sala de generadores (detrás de los rayos) | "SERVIDOR 01... ESTABLE" | Te empuja la mano, curioso | Un Pokémon controlado y liberado |
| Terminal del salón | escritorio | El registro del Guardián: "RECUERDOS: SINCRONIZADOS. ÚLTIMA EDICIÓN: HACE 4 MINUTOS" | "RECUERDOS: SIN SINCRONIZAR" | Adelanta que el chip edita recuerdos |
| Memo de J. | mesa de control | "El sujeto que no figura en ningún registro se despertó. No lo busquen. Va a venir por su cuenta. -J." | Pantalla apagada | Amenaza de Julieta; Tixi lo comenta al volver |
| Foto | mesa de Néstor | La promoción 2026 con una cara pixelada donde vos te hubieras parado | Néstor la mira de reojo | Misterio: el PJ borrado de los registros |
| Máquina expendedora | sala de descanso | Solo acepta chip | La patearon | Humor |
| Zona "pantallas" | entrada al salón | USUARIO NO ENCONTRADO EN NINGÚN REGISTRO | — | Evento; pista de "no aparecés en ninguno" |
| Zona "guardián" | pasillo a Néstor | GUARDIÁN EN LÍNEA. PROTOCOLO DE DEFENSA: EN ESPERA | — | Tensión antes del jefe |
| Salida | recepción, donde aparecés | cerrada (no hace nada) | brilla verde y pregunta "¿Volvés a la Quarcho Cueva?" | Volver caminando, como en el guion |

**El guardia que te ve** (`NpcQueTeDetecta`, desde el 1-oct-2026). La sala que
vigila es un rectángulo (x -46..-37, y 22..26) y no un collider: un trigger en
Obstacles frenaría al jugador en la puerta y el combate re-dispara los triggers al
volver el mapa. Al pisarla: el "!" del pack (`Graphics/Animations/Overworld
exclaim.png`, el primer globo) le sale arriba de la cabeza, se da vuelta hacia vos,
vos te das vuelta hacia él y arranca `DC_Guardia` solo. Suena **una vez**: lo marca
`Historia_DC_GuardiaRecibioOrden`, que enciende `DC_Guardia_Cruz` al final de la
cadena (es un flag de la partida, tampoco se repite al cargar). Si al entrar había
un cartel abierto, espera a que se cierre. Con el servidor caído ya no vigila.

**La enfermera** (`NpcQueCura`). Saludo → si el equipo está entero, `DC_Enfermera_Sano`;
si no, pregunta (Sí / No) → cura con `PlayerModel.CurarEquipo` (PS, estados y PP),
la pantalla destella tres veces por debajo de la caja y dice `DC_Enfermera_Curado`;
si decís que no, `DC_Enfermera_NoGracias`. No cobra (la del casino sí).

**Muebles que frenan** (desde el 1-oct-2026). Las sillas del salón y de la sala de
descanso, las banquetas y la silla de la recepción y los extremos del sillón se
dibujaban pero no frenaban: el jugador caminaba por encima. Ahora cada celda lleva
`Palettes/ColisionInvisible.asset` en Colisiones (un tile sin sprite con collider
Grid): frenan sin cambiar nada de lo que se ve ni el orden de dibujo. Lo mismo para
cuatro celdas de la pared del fondo de la sala de generadores que estaban pintadas
en `Suelo`. La lista está en `HistoriaDataCenterBuilder.MueblesQueFrenan` y
`Verificar` comprueba que **todo lo que se toca con X en la escena** (no solo lo de
la historia) siga alcanzable. Queda un hueco de dos celdas entre las sillas de las
mesas de la sala de descanso al que no se llega: es a propósito (es el espacio
entre dos sillas).

La pelea con Néstor no cambió: antes de la pregunta ("PROTOCOLO DE DEFENSA:
LISTO." Pelear / Todavía no) va la charla del PDF; la segunda vez, una corta. Al
ganar (fin de la fase de liberación) la charla de después arranca con el mapa de
vuelta; a la mitad se enciende `Historia_Servidor01Desconectado` y ahí bajan las
luces. Al final se recibe el **Protocolo Néstor**.

---

## Continuidad: quién se acuerda de qué

| Si... | Entonces... |
|---|---|
| pateaste / le mentiste / le huiste a Cruz | el guardia del Data Center te lo cuenta como un rumor |
| leíste el memo de J. | Tixi habla de eso al volver |
| hablaste con el técnico | la secretaria del piso 2 del casino te reconoce el "olor a data center" y se acuerda de su hermano |
| hablaste con la mujer de las tragamonedas | un guardia del piso 4 sabe que ella avisó |
| hablaste con el croupier | recién ahí el jugador de la ruleta te cuenta lo de Tacconi |
| Tacconi te contó lo del registro | un guardia del piso 4 duda de que Tacconi haya perdido de verdad |
| cayó el servidor 01 | todos en el Data Center dicen otra cosa y la terminal cambia |
| ganaste el casino | Martín y Tacconi llegan a la cueva, y Néstor también ("te dije que te iba a buscar") |

## Pistas y misterios

Lo que se siembra y lo que **no** se dice todavía:

- **El virus del PJ.** El diario (el plan de la graduación) y el PC (`virus_ameghino`
  vacío, abierto después que vos). El PDF no lo vuelve a tocar: queda como la razón
  por la que el PJ estaba escondido en la oficina en 2026.
- **Qué le hicieron al PJ.** Tacconi ("eras parte del desarrollo"), Reymar
  ("participé en tu preparación", "eras el único caso que no necesitaba las
  modificaciones") y Julieta al final ("te estudiamos"). Lo que pasó en "aquella
  sala" queda abierto a propósito: el juego termina con "¿Por dónde empezamos?
  Por 2026."
- **El PJ fuera de los registros.** La zona de las pantallas, el memo de J., la
  foto pixelada, Tacconi ("no aparecés en ninguno"), el registro con el estado
  "[DATO ELIMINADO POR LA ADMINISTRADORA]".
- **El chip edita la memoria.** La compañera que no se acuerda del Ameghino, la
  terminal ("ÚLTIMA EDICIÓN"), la bióloga que sueña con un aula, el entusiasta que
  pierde lo aprendido, Néstor.
- **Mewtwo y Hoopa.** Solo como sombras: el aro dorado sobre el Ameghino (el
  anciano, Martín), el Mew de Tacconi que apareció el día del portal.
- **Tixi sabe más de lo que dice.** "Tixi sabe muchas cosas" (Tacconi), "hay
  demasiadas cosas que no encajan" al volver del casino, y al final Julieta: "intentó
  detenernos antes del accidente". El PJ lo recuerda en la última escena.
- **Julieta** casi no aparece hasta la sede: la sonrisa congelada en la tele, "la
  Directora" en boca de otros, la firma "J." y los permisos cambiados en el casino.

---

## Decisiones que no están en el PDF (revisar)

- **Néstor llega a la cueva después del casino**, no apenas se lo libera. En el PDF
  dice "Todavía no" y "cuando recuerde todo, te voy a buscar", y en la vuelta a la
  cueva solo está Tixi. Es un solo valor: el `BarreraPorFlag` de Néstor en la cueva
  (`Casino_Completado`; antes `Torre1_NestorLiberado`) y `QuarchoCuevaBuilder`.
- **El inicial lo manda Tixi con la carta.** El PDF lo da en la cueva; el juego lo
  elige en la creación de personaje. Tixi dice que te mandó "la mejor de las tres"
  en una QuerryBola junto con la carta.
- **El camino a la cueva se cuenta sobre negro** al salir de la habitación, con las
  opciones del PDF, porque no hay mapa de la calle ni de la escuela.
- **El Protocolo Néstor es un objeto clave** (se ve en la mochila) y en combate es
  la lectura aliada de Néstor que ya existía en el casino. Así lo trata también la
  pelea final ("PROTOCOLO NÉSTOR: EN LÍNEA").
- **La tarjeta del piso 4**: Tacconi la da junto con su shiny. El ascensor sigue
  pidiendo el shiny (no se tocó la mecánica); los textos hablan de la tarjeta.
- **Los nombres de los niveles** en la salida de la cueva ya no adelantan nada: "el
  primer Data Center", "el casino", "la sede central de la Liga".
- **Cuántos servidores hay.** Tixi dice "cuatro" al principio (el PDF) y después
  del casino el PDF dice "SERVIDORES RESTANTES: 01" y "queda uno". Se dejó como
  está en el PDF; si el equipo lo unifica, son las líneas de `Lobby_Tixi_Lore` y
  de `Casino_Reymar_Despues`.
- **Reymar en el casino.** El PDF pone el piso 4 como un laboratorio vacío; la
  escena es un piso del casino con guardias y la mesa de Reymar. La narración dice
  que el laboratorio está **detrás de un vidrio**, atrás de la mesa ("lo que el
  casino tapa"), y no nombra la bata (el arte de Reymar es de traje y sombrero).
  "¿Qué mierda...?" quedó "¿Qué es esto...?". Se le agregaron tres líneas al final
  que el PDF no tiene: deja la ruleta (la usa la pelea de Julieta) y el PJ lo manda
  con Tixi (por eso aparece en la cueva).
- **El transmisor es un objeto clave** (`Transmisor`), y es lo que abre la ruptura
  de la pelea final: Tixi entra a la red por ahí.
- **Voces**: el PJ con humor seco y momentos de "no entiendo nada"; Tixi técnica y
  breve (y sin género gramatical, porque el PDF usa los dos); Néstor frío y con
  fallas antes, confundido y cálido después ("Ante todo, buenos días"); Tacconi
  siempre "llegaste tarde"; Reymar seco y técnico (el PDF), también después de
  desconectarse.

---

## Dónde se edita cada cosa

| Qué | Dónde |
|---|---|
| Habitación | `Resources/Definitions/Tutorial/Dialogos/Tutorial_*` (la puerta: campos de `PuertaDelTutorial` en `habitacion.unity`) |
| El camino | `Resources/Definitions/Historia/Camino/Camino_*` |
| Cueva | `Resources/Definitions/Lobby/Dialogos/Lobby_*` |
| Data Center | `Resources/Definitions/Torre1/Dialogos/DC_*` (posiciones: tablas de `HistoriaDataCenterBuilder`) |
| Casino | `Resources/Definitions/Casino/Dialogos/` (`Casino_*`, `Historia_*`, `Jugador*`) |
| La sede y el final en la cueva | `Resources/Definitions/Julieta/Dialogos/` (`Final_Sede_*`, `Final_Tixi_EnLaCueva`) |
| Julieta, el epílogo y la última conexión | `Resources/Definitions/Julieta/GuionDeJulieta.asset` (se rehace desde `PeleaJulietaAssets.cs`) |
| Objetos clave | `Resources/Definitions/Items/` (`CartaAnonima`, `RaspberryPy`, `ProtocoloNestor`, `TarjetaPiso4`, `Transmisor`) |

Para que un NPC nuevo reaccione a algo: una **variante** en su asset con el flag
que corresponda. Si hace falta un flag nuevo, va **al final** de `StoryFlag` (las
escenas guardan los flags como número). Los de la historia empiezan en
`Historia_CaminoContado`.

---

## Menús y pruebas

| Menú | Qué hace |
|---|---|
| `PokeRush > Historia > Poblar el Data Center de Nestor` | Arma lo de arriba en `NestorGOD2`. |
| `PokeRush > Historia > Poner iconos a los objetos de la historia` | Pone los iconos del pack a los cinco objetos clave. |
| `PokeRush > Historia > Correr smoke tests de la historia` | 122 checks (al 4-oct-2026), `[SmokeHistoria]`: el sistema de diálogos nuevo, que los assets estén sanos (largo de líneas, cadenas, respuestas), la continuidad y que las escenas tengan enganchado lo suyo (leyendo el `.unity`, sin abrirlo). |
| `PokeRush > Historia > Probar el Data Center en Play` | 41 checks, `[PlayHistoria]`: habla con todos, pisa las zonas, Néstor antes y después, el apagón, el Protocolo, todos cambian, la salida y la vuelta de Tixi. Capturas en `Capturas/Historia/`. No guarda. |

`Probar la cueva en Play` (33) y `Probar el tutorial en Play` (61) también recorren
lo nuevo: las vueltas de Tixi, la carta, la puerta y el camino con Cruz.

> Ojo al elegir la hoja de un NPC nuevo: `CasinoNpcBuilder.Personaje` la corta con
> nombres propios y eso cambia los IDs de sus sprites. Una hoja que otra escena ya usa
> cortada de otra forma queda rota allá (le pasaba a `tecnico`, que usan las escenas
> EdificioNucleo: por eso el técnico usa la hoja `operario`).
>
> Los PNG del pack que usan los NPCs nuevos (`Graphics/Characters`: asistenta,
> operaria, operario, chaval1, cerebrito, cientifica, `Followers/PORYGON`; `Graphics/Trainers/CIENTIFICA`;
> `Graphics/Items`: AIRMAIL, UPGRADE, DUBIOUSDISC, REDCARD, DEVONSCOPE) están en `.gitignore`:
> se suben con `git add -f` junto con su `.meta`.
