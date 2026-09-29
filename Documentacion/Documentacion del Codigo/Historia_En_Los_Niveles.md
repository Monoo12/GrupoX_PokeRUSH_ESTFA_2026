# La historia en los niveles

Cómo está contada la historia de PokeRush dentro de cada nivel jugable: qué pasa
en cada lugar, quién dice qué, qué cambia según lo que hizo el jugador y dónde se
edita cada texto.

**Fuente canónica:** `historia.pdf` (el guion del 28-sep-2026: el acto I en 2026,
el acto II en 2031, la habitación, la Quarcho Cueva, el Data Center de Néstor y el
casino hasta el ascensor al piso 4). Lo que el PDF dice está implementado casi
textual (pasado a voseo, como el resto del juego). Lo que el PDF no cubre (los
NPCs secundarios, el piso 4 del casino, la vuelta a la cueva después del casino)
se escribió para que encaje con él y con la pelea final contra Julieta
(`Jefe_Final_Julieta.md`), sin adelantar lo que la historia guarda para el final.

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
| Casino piso 4 | `Casino piso FINAL` | Los guardias (cada uno distinto: la Directora que mira por sus ojos, el zumbido detrás de la pared...). **Reymar** antes ("la casa no juega, calcula") y después: el chip se apaga, se vuelve **Martín**, deja la ruleta y el jugador busca su nombre en el servidor: *EQUIPO DE DESARROLLO: {PJ} (NO VOLUNTARIO). APORTE: MÓDULO DE PROPAGACIÓN.* |
| Vuelta 2 | `Quarcho-Cueva` | Tixi: SERVIDOR 02 OFFLINE, "vamos a necesitar más sillas". Si Tacconi te lo contó, le preguntás si sabía — y esquiva. Los dos servidores que quedan mandan todo a un tercer punto: **el servidor central**. Llega un mensaje: `print("Buen trabajo. Te espero. -J.")`. Recién ahora están en la cueva **Néstor**, **Martín** y **Tacconi**. |

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
sobre piso libre y se llegue caminando, y repone los Grid de Colisiones antes de
guardar.

| Quién / qué | Dónde | Antes de la caída | Después | Para qué está |
|---|---|---|---|---|
| Recepcionista | atrás del mostrador (se le habla desde el mostrador) | Guion de bienvenida robótico; el lector no reconoce tu chip; "Guardián de turno: [DATO RESTRINGIDO]" | Se acuerda de su florería | Presenta el lugar sin nombrar a Néstor; efecto del chip |
| Operaria | escritorio del salón | Te toma por el turno nuevo y explica firewall, cámaras, rastreo y energía | Tiene hambre por primera vez en cinco años | Explica la mecánica de los subsistemas de forma diegética |
| Técnico | sala de racks | El rack caliente que no figura en planos; **su hermana trabaja en un casino** y habla del piso 4 | Se acuerda de su hermana | Pista del piso 4; continuidad con la secretaria del casino |
| Entusiasta | sala de descanso | Aprendió redes en una siesta; "la Directora nunca se equivoca" | Se le borró todo lo aprendido: "¿solo nos lo prestaba?" | La mirada a favor del LINK; siembra "el LINK no enseña" |
| Empleado dormido | el sillón | "Estoy descargando Excel avanzado..." | "¿Qué AÑO es?" | Humor |
| Guardia de la Liga | sala de control | **Evento**: el sistema le ordena buscar a alguien sin chip... y te pregunta a vos. Sabe lo que hiciste con Cruz | Recibió la última orden y no sabe qué hacer un martes | Tensión, humor, continuidad |
| Bióloga | junto al tanque de Basculin | Pokémon que pelean "por protocolo"; señala el panel de las cámaras; sueña con un aula | Los Pokémon del tanque se durmieron | Pokémon afectados; pista para liberar a Basculin |
| Porygon con chip | sala de generadores (detrás de los rayos) | "SERVIDOR 01... ESTABLE" | Te empuja la mano, curioso | Un Pokémon controlado y liberado |
| Terminal del salón | escritorio | El registro del Guardián: "RECUERDOS: SINCRONIZADOS. ÚLTIMA EDICIÓN: HACE 4 MINUTOS" | "RECUERDOS: SIN SINCRONIZAR" | Adelanta que el chip edita recuerdos |
| Memo de J. | mesa de control | "El sujeto que no figura en ningún registro se despertó. No lo busquen. Va a venir por su cuenta. -J." | Pantalla apagada | Amenaza de Julieta; Tixi lo comenta al volver |
| Foto | mesa de Néstor | La promoción 2026 con una cara pixelada donde vos te hubieras parado | Néstor la mira de reojo | Misterio: el PJ borrado de los registros |
| Máquina expendedora | sala de descanso | Solo acepta chip | La patearon | Humor |
| Zona "pantallas" | entrada al salón | USUARIO NO ENCONTRADO EN NINGÚN REGISTRO | — | Evento; pista de "no aparecés en ninguno" |
| Zona "guardián" | pasillo a Néstor | GUARDIÁN EN LÍNEA. PROTOCOLO DE DEFENSA: EN ESPERA | — | Tensión antes del jefe |
| Salida | recepción, donde aparecés | cerrada (no hace nada) | brilla verde y pregunta "¿Volvés a la Quarcho Cueva?" | Volver caminando, como en el guion |

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
| Tacconi te contó lo del registro | un guardia del piso 4 duda de que Tacconi haya perdido de verdad, Reymar lo menciona y Tixi esquiva la pregunta |
| cayó el servidor 01 | todos en el Data Center dicen otra cosa y la terminal cambia |
| ganaste el casino | Martín y Tacconi llegan a la cueva, y Néstor también ("te dije que te iba a buscar") |

## Pistas y misterios

Lo que se siembra y lo que **no** se dice todavía:

- **El virus del PJ.** El diario (el plan de la graduación), el PC (`virus_ameghino`
  vacío, abierto después que vos), Tacconi ("eras parte del desarrollo") y el
  registro del piso 4 ("MÓDULO DE PROPAGACIÓN", "NO VOLUNTARIO"). Nadie dice qué
  hizo Julieta con él: eso lo revela la pelea final.
- **El PJ fuera de los registros.** La zona de las pantallas, el memo de J., la
  foto pixelada, Tacconi ("no aparecés en ninguno"), el registro con el estado
  "[DATO ELIMINADO POR LA ADMINISTRADORA]".
- **El chip edita la memoria.** La compañera que no se acuerda del Ameghino, la
  terminal ("ÚLTIMA EDICIÓN"), la bióloga que sueña con un aula, el entusiasta que
  pierde lo aprendido, Néstor.
- **Mewtwo y Hoopa.** Solo como sombras: el aro dorado sobre el Ameghino (el
  anciano, Martín), el Mew de Tacconi que apareció el día del portal.
- **Tixi sabe más de lo que dice.** "Tixi sabe muchas cosas" (Tacconi) y su
  respuesta esquiva al volver del casino.
- **Julieta** casi no aparece: la sonrisa congelada en la tele, "la Directora" en
  boca de otros, la firma "J." y un `print()` al final del casino.

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
  primer Data Center", "el casino", "el servidor central".
- **Lo que sigue al casino**: los dos servidores que quedan apuntan a un servidor
  central que no estaba en el mapa. Los servidores 03 y 04 siguen existiendo (si el
  equipo los hace niveles, no hay que cambiar nada de lo dicho).
- **Voces**: el PJ con humor seco y momentos de "no entiendo nada"; Tixi técnica y
  breve (y sin género gramatical, porque el PDF usa los dos); Néstor frío y con
  fallas antes, confundido y cálido después ("Ante todo, buenos días"); Tacconi
  siempre "llegaste tarde"; Reymar en el casino (el dueño) y Martín cuando se le
  apaga el chip.

---

## Dónde se edita cada cosa

| Qué | Dónde |
|---|---|
| Habitación | `Resources/Definitions/Tutorial/Dialogos/Tutorial_*` (la puerta: campos de `PuertaDelTutorial` en `habitacion.unity`) |
| El camino | `Resources/Definitions/Historia/Camino/Camino_*` |
| Cueva | `Resources/Definitions/Lobby/Dialogos/Lobby_*` |
| Data Center | `Resources/Definitions/Torre1/Dialogos/DC_*` (posiciones: tablas de `HistoriaDataCenterBuilder`) |
| Casino | `Resources/Definitions/Casino/Dialogos/` (`Casino_*`, `Historia_*`, `Jugador*`) |
| Objetos clave | `Resources/Definitions/Items/` (`CartaAnonima`, `RaspberryPy`, `ProtocoloNestor`, `TarjetaPiso4`) |

Para que un NPC nuevo reaccione a algo: una **variante** en su asset con el flag
que corresponda. Si hace falta un flag nuevo, va **al final** de `StoryFlag` (las
escenas guardan los flags como número). Los de la historia empiezan en
`Historia_CaminoContado`.

---

## Menús y pruebas

| Menú | Qué hace |
|---|---|
| `PokeRush > Historia > Poblar el Data Center de Nestor` | Arma lo de arriba en `NestorGOD2`. |
| `PokeRush > Historia > Poner iconos a los objetos de la historia` | Pone los iconos del pack a los cuatro objetos clave. |
| `PokeRush > Historia > Correr smoke tests de la historia` | 112 checks, `[SmokeHistoria]`: el sistema de diálogos nuevo, que los assets estén sanos (largo de líneas, cadenas, respuestas), la continuidad y que las escenas tengan enganchado lo suyo (leyendo el `.unity`, sin abrirlo). |
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
> `Graphics/Items`: AIRMAIL, UPGRADE, DUBIOUSDISC, REDCARD) están en `.gitignore`:
> se suben con `git add -f` junto con su `.meta`.
