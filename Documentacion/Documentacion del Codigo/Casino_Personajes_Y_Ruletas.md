# Casino: personajes y ruletas de premios (pisos 1 a 3)

Qué gente hay en los pisos del casino, qué hace cada uno y cómo se tocan los textos
y los porcentajes sin escribir código.

Lo arman dos menús (`Assets/Editor/Casino/CasinoNpcBuilder.cs`), separados para que rehacer
un piso no toque el otro:

- **`PokeRush > Casino > Poner los personajes del piso 1`**
- **`PokeRush > Casino > Poner los personajes de los pisos 2 y 3`**: además configura los
  ascensores de esos pisos y las seis mesas de ruleta del piso 2.

> ## ⚠️ Pendiente: los diálogos de la historia
>
> **Los textos de historia son de relleno.** Faltan los diálogos definitivos de:
>
> - el anciano (piso 1);
> - la damisela y la secretaria (piso 2);
> - el empresario y el jubilado (piso 3).
>
> Van cuando esté escrita la historia del casino.
>
> Se cargan en `Assets/Resources/Definitions/Casino/Dialogos/`: `Historia_Piso1_Anciano`,
> `Historia_Piso2_Damisela`, `Historia_Piso2_Secretaria`, `Historia_Piso3_Empresario` y
> `Historia_Piso3_Jubilado`. Hay que tocar solo el asset, sin abrir las escenas. Volver a
> correr el builder no pisa los textos. Si un NPC tiene que decir otra cosa la segunda vez,
> ponerle un `grantsFlag` al diálogo y un `dialogoRepetido` al `NpcQueHabla`.
>
> Las frases de los jugadores (tragaperras, ruleta y blackjack: `Jugador*.asset`) también son
> provisorias, pero no son de historia.

## Los sprites de Tacconi (ya son los suyos)

Desde el 18-sep Tacconi usa **su propio arte**, no el provisorio de Giovanni:

| Archivo | Qué es |
|---|---|
| `Sprites/Casino/tacconi_frente.png` | El del mapa: 30x76, parado de frente. |
| `Graphics/Trainers/TACCONI.png` | El retrato de la caja de diálogo, 160x160. |
| `Assets/Tacconi/Gemini_Generated_Image_v0fpte...jpg` | El dibujo que pasó el equipo. |

**Está parado y no se mueve**, así que es **un sprite suelto** y no una hoja de 4x4. Lo
cuelga `CasinoNpcBuilder.PersonajeSuelto`, que es el mismo `Personaje` de siempre pero con un
único sprite. `SpriteDePersonaje` lo importa a 32 píxeles por unidad y con el pivote abajo al
medio, que es lo que hace que los pies queden apoyados en la celda.

### Lo que hay que hacerle al dibujo antes de meterlo

No es un recorte directo, y saltearse estos dos pasos fue lo que hizo que quedara mal dos
veces seguidas:

- **Sacarle el damero.** El archivo es un **JPG**, así que el damero de transparencia quedó
  pegado como cuadrados blancos y grises. Se saca marcando como fondo lo neutro y claro y
  rellenando **desde el borde**, no por color: el blanco del cuello del polo está encerrado
  por el contorno negro, así que el relleno no lo alcanza y se salva.
- **Volverlo pixel art.** El JPEG le mete ruido: la figura sale con ~18.000 colores. Un
  personaje del pack usa **~20** y lleva **contorno negro de 1 píxel**. Después de achicar
  hay que **reducir la paleta sin difuminado** (a este tamaño el dither es suciedad, no
  sombreado) y **repintar el contorno por dentro de la silueta**, para no agrandar el sprite.
  Sin eso, en el mapa se ve como una mancha al lado de los demás por más que la escala y el
  pivote estén bien.

### Por qué mide 76 y no 58

Su dibujo tiene proporciones de unas **3 cabezas** y los del pack son de **2,2**. Igualando
por **altura** queda un palito de 23 px de ancho contra los 36 del guardia, y la cara se
vuelve una mancha. Se iguala por **cabeza**, que es lo que se mira primero en un sprite
chibi: ahí dan 76 y se le leen los anteojos y la remera. Queda 3,04 unidades de alto contra
2,32 del resto.

Es una constante del script de exportación. Si se lo quiere más bajo, a 68 todavía se lee;
a 58 no.

### Si se vuelve a generar el dibujo

Lo que sigue sin cumplirse y haría que entrara en 58 sin perder nada:

- **40 x 60 píxeles reales**, no una imagen grande "estilo pixel art". Las tres que se
  probaron vinieron entre 300 y 1140 px de alto, y ahí el detalle se pierde al achicar sí o sí.
- **PNG con fondo transparente de verdad**, nunca JPEG: el JPG rompe la paleta plana y pega
  el damero.
- Proporción **chibi de 2 a 2,5 cabezas**, como los personajes de Essentials del proyecto.

Lo que sí vino bien en la última: figura única, de frente, parada, completa con pies,
contorno negro y paleta razonablemente plana.

> Se probaron tres. La primera tenía las poses de frente cortadas a la altura de la rodilla y
> proporciones realistas (hubo que injertarle piernas: quedó mal). La segunda era una hoja de
> 20 poses, mejor pero todavía con el detalle perdido al achicar. La que vale es la tercera
> (`Gemini_Generated_Image_v0fpte...`), una sola figura ya dibujada de frente.

## Néstor de ayudante en todos los combates

Todas las peleas de los pisos del casino (el guardia y los doce que pelean una vez) traen a
Néstor de ayudante: la lectura aliada de `NestorPredictionSystem`, con su retrato arriba a la
derecha del HUD. Cómo lee y cada cuánto está en `Sistema_De_Combate_Y_IA.md`.

No depende de la escena ni de la partida: `CasinoNpcConCombate` lo pide en cada combate con
`StartBattle(..., conNestorAliado: true)`. Por eso aparece también entrando directo a un piso
con Play, con la partida `DEMO` que no trae `Torre1_NestorLiberado`. Un NPC nuevo que herede
de `CasinoNpcConCombate` lo tiene sin hacer nada.

Para comparar una pelea sin él, poner `nestorAliado` en `SiempreOff` en el `BattleController`
de la escena. Contra un boss (el del piso final, cuando exista) nunca aparece: ese candado no
lo saltea nada.

---

## Quién está en cada piso

| Piso | Personaje | Qué hace | Celda |
|---|---|---|---|
| 1 | **Guardia** (`GuardiaAscensor`) | Pide 4000 monedas o que le ganes para usar el ascensor. Ganarle da la ruleta de Pokémon | (3.5, 19.5), al lado del ascensor |
| 1 | Apostador | Pelea una vez (Alakazam y Raichu, Nv41). Paga 100 | (-3.5, 12.5) |
| 1 | Malabarista | Pelea una vez (Mimikyu y Klefki, Nv42) | (9.5, 15.5) |
| 1 | Motorista | Pelea una vez (Magmortar Nv42, Rhyperior Nv43) | (-10.5, 4.5) |
| 1 | Pokemaníaco | Pelea una vez (Electivire Nv42, Aggron Nv43) | (-9.5, 15.5) |
| 1 | Enfermera | Cura por 100 monedas o fiado, sin tope de deuda | (4.5, 12.5) |
| 1 | Anciano | Historia | (5.5, 4.5) |
| 1 | 8 jugadores (`JugadorTragaperras1..8`) | Juegan a las tragaperras: dan saltitos y dicen una frase | en taburetes, ver abajo |
| 2 | Joven rica | Pelea una vez (Togekiss Nv44, Sylveon Nv45) | (-10.5, 10.5), charlando con la damisela entre las mesas de la izquierda |
| 2 | Modelo | Pelea una vez (Froslass Nv44, Milotic Nv45) | (0.5, 5.5), posando al final de la alfombra |
| 2 | Médium | Pelea una vez (Dusknoir Nv44, Hatterene Nv45) | (-13.5, 15.5), en la mesa de arriba a la izquierda |
| 2 | Ladrón | Pelea una vez (Meowscarada y Sneasler, Nv45) | (13.5, 11.5), pegado a una jugadora de la mesa 04 |
| 2 | **Guardia VIP** (`GuardiaVipCasino`) | Pide la **Tarjeta VIP** o que le ganes para usar el ascensor. Ganarle da su ruleta exclusiva | (2.5, 19.5), a la derecha de la puerta del ascensor |
| 2 | **Magnate** | Pelea una vez (Gardevoir y Haxorus Nv45, Chandelure Nv46). **Ganarle da la Tarjeta VIP** | (12.5, 18.5), en la cabecera de la mesa 06, al lado de la esquina VIP |
| 2 | **Revendedor** | Vende la Tarjeta VIP por 1500 | (15.5, 19.5), en la esquina de arriba a la derecha |
| 2 | Tahúr | Pelea una vez (Cofagrigus y Aegislash, Nv45) | (-5.5, 16.5), en la mesa decorativa más cerca de la alfombra |
| 2 | Bailarina | Pelea una vez (Altaria Nv44, Roserade Nv45) | (-7.5, 5.5), calentando en el espacio libre de abajo |
| 2 | Enfermera | Cura | (-13.5, 19.5), arriba a la izquierda, con el PC al lado |
| 2 | Damisela | Historia | (-8.5, 10.5), charlando con la joven rica |
| 2 | Secretaria | Historia | (-1.5, 19.5), a la izquierda de la puerta del ascensor |
| 2 | 6 jugadores (`JugadorRuleta1..6`) | Juegan a la ruleta, uno en el frente de cada mesa. Algunos dan pistas | ver abajo |
| 3 | **Tacconi** | Juega su **Cosmog shiny** a una mano de blackjack contra uno de tus Pokémon | (-6.5, 18.5), del lado del croupier de **su** mesa (la de arriba a la derecha) |
| 3 | Macarra | Pelea una vez (Weavile Nv47, Krookodile Nv48) | (3.5, 12.5), al costado de la alfombra |
| 3 | Guay | Pelea una vez (Gliscor Nv47, Corviknight y Absol Nv48) | (5.5, 19.5), al lado de la rockola |
| 3 | Veterano | Pelea una vez (Donphan Nv48, Steelix Nv49) | (-8.5, 16.5), mirando la mesa de Tacconi desde el pasillo |
| 3 | Karateka | Pelea una vez (Annihilape y Lucario, Nv48) | (4.5, 5.5), en el espacio libre de abajo |
| 3 | Gángster | Pelea una vez (Incineroar Nv47, Drapion Nv48) | (-3.5, 16.5), al costado de la mesa de Tacconi, mirando a la alfombra |
| 3 | Crupiera | Pelea una vez (Porygon-Z Nv47, Armarouge Nv48) | (5.5, 16.5), en su descanso, apoyada en la barra |
| 3 | Enfermera | Cura | (-13.5, 19.5), arriba a la izquierda, con el PC al lado |
| 3 | **Empresario** | Vende tiros de su ruleta de premios (300 c/u); si le decís que no, cuenta su historia | (11.5, 14.5), sentado en el restaurante |
| 3 | **Tendero** (`TenderoCasino`) | La tienda del piso: lo mismo que vende Tixi, a los mismos precios | (-11.5, 19.5), a la derecha de la enfermera y el PC |
| 3 | Jubilado | Historia | (5.5, 9.5), sentado en el restaurante |
| 3 | 4 jugadores (`JugadorBlackjack1..4`) | Juegan al blackjack: parados debajo de los banquitos (el lado curvo de la mesa), mirando a la mesa. Del lado recto va el croupier. Uno juega en la mesa de Tacconi | ver abajo |

Además hay **un PC en cada piso** (ver `PC_De_Almacenamiento.md`). En los pisos 2 y 3 está arriba a
la izquierda, al lado de la enfermera: es lo primero que se ve al salir del ascensor.

Los jugadores de ruleta y de blackjack usan los mismos dos componentes que los de las
tragaperras (abajo). Están en el **frente** de cada mesa (el lado desde el que se juega) y ocupan
una sola celda: al jugador le quedan lugares libres para usarla.

Las enfermeras de los pisos 2 y 3 son nuevas: **perder un combate no cura al equipo**,
así que sin ellas una derrota dejaba al jugador sin poder pelear por el resto del piso.

### Los jugadores de tragaperras (piso 1)

Ocho personas sentadas en los taburetes, mirando a su máquina. Hay dos componentes:

- **`NpcQueHabla`**: la frase de cada uno está en `Dialogos/JugadorTragaperras*.asset`.
- **`NpcJugandoMaquina`**: cada 1,6–4,5 s dan un saltito (a veces doble). Mueve **solo el
  hijo `Visual`**, así que el NPC sigue ocupando la misma celda. El ritmo sale del nombre y la
  posición: se ve variado pero es igual en todas las partidas.

Nunca ocupan los dos taburetes de una máquina y hay máquinas sin nadie. Así, todas las
tragaperras se siguen pudiendo usar de frente o desde un taburete libre. Las posiciones están
en la tabla `Jugadores` del builder.

---

## El guardia del ascensor (piso 1)

`CasinoElevatorGuard`. El ascensor del piso 1 **ya no pide tener 500 juntados**: pide el
flag `Casino_Piso1_PaseAscensor`, que da el guardia.

- **Con plata suficiente** pregunta si le pagás. Sí → cobra `precio` (4000; eran 500 hasta el 4-oct) y da el pase.
  No → ofrece pelear.
- **Sin plata** va directo a ofrecer la pelea.
- **Ganándole** da el pase y abre **su ruleta de Pokémon**, una sola vez en la partida
  (`Casino_GuardiaVencido`).
- **Perdiendo** no pasa nada: se puede volver a intentar o pagar.

El combate es medianamente difícil a propósito: Houndoom Nv43, Krookodile Nv43 y Gengar
Nv44, con perfil de IA `GymLeader`. El equipo del jugador llega al casino alrededor del
nivel 42–44 (rebalanceo del 6-oct-2026: todo forma final; ver `Rebalanceo_Oct_2026.md`).

---

## Los que pelean una vez

`CasinoTrainerNpc`. Preguntan antes de pelear y, al ganarles:

1. activan su flag de vencido (`Casino_ApostadorVencido`, `Casino_MalabaristaVencido`, …),
   que es lo que impide repetir la pelea;
2. sortean la **ruleta de premios de los NPCs**;
3. guardan la partida.

> **Por qué el flag lo activa el NPC y no el `EventController`:** las escenas del casino no
> tienen `EventController`, así que el `defeatGrantsFlag` del enemigo no se aplicaba nunca.
> El apostador del piso 1 se podía pelear infinitas veces. Ahora cada NPC activa su flag.

Lo que se muestra después del combate (el cartel de victoria y la ruleta) espera a que el
mapa vuelva a estar activo. Durante la pelea `MapBattleBridge` apaga `MapaRaiz`, y el NPC
vive adentro. Eso lo resuelve la base `CasinoNpcConCombate`.

---

## La ruleta de premios de los NPCs

**Una cada dos combates ganados, hasta cinco por partida (eran cuatro hasta el 4-oct), y no siempre con los mismos NPCs.**

Regla (`CasinoRuletaDeNpcs`): los combates contra los NPCs de la lista se agrupan de a dos, en
el orden en que los ganás (1.º y 2.º, 3.º y 4.º, …). En cada par sale **exactamente una**
ruleta:

- al ganarle al primero del par, sale con probabilidad **1/2**;
- si no salió, al ganarle al segundo sale seguro.

Con eso:

- ganando 2, 4, 6, 8 o 10 combates salen 1 a 5 ruletas; del undécimo en adelante ya no sale;
- aunque pelees siempre en el mismo orden, cuál de los dos NPCs del par la da cambia de
  partida en partida (hay 32 combinaciones posibles);
- peleando en cualquier orden, ninguno de los doce la da más seguido que otro.

No hace falta guardar "quiénes son los elegidos" ni contar combates aparte: los combates
ganados son los flags de vencido, y los usos son cinco flags (`Casino_RuletaNpc_1` a `_5`)
que el guardado ya persiste. La pelea del guardia no cuenta: tiene su propia ruleta.

### Premios

Hay **dos tablas**, porque la Tarjeta VIP solo tiene sentido en el piso 2 (en el 1 todavía no
sirve y en el 3 ya no).

**Pisos 1 y 3** (`Resources/Definitions/Casino/RuletaDeLosNpcs.asset`), sin tarjeta:

| Premio | Peso |
|---|---|
| Pokémon (Poké Ball) Nv44 | 23 |
| Pokémon (Super Ball) Nv44 | 22 |
| Pokémon (Poké Ball) Nv44 | 20 |
| Poción x3 | 10 |
| Superpoción x2 | 9 |
| Hiperpoción x1 | 4 |
| 150 monedas | 8 |
| 400 monedas | 4 |

**65% Pokémon, 23% curativos y 12% plata.** (Hasta el 4-oct era 35% Pokémon: el playtest
pidió llegar al final con más equipo, y Augusto fijó el 65%.)

**Piso 2** (`Resources/Definitions/Casino/RuletaDeLosNpcsPiso2.asset`), con la tarjeta:

| Premio | Peso |
|---|---|
| Pokémon (Poké Ball) Nv46 | 22 |
| Pokémon (Super Ball) Nv46 | 22 |
| Pokémon (Poké Ball) Nv46 | 21 |
| **Tarjeta VIP** | 15 |
| Poción x3 | 7 |
| Superpoción x2 | 6 |
| 150 monedas | 5 |
| 400 monedas | 2 |

**65% Pokémon, 15% Tarjeta VIP, 13% curativos y 7% plata.**

Las dos tienen **la misma** lista en `npcsQueLaPuedenDar`: los flags de los dieciséis NPCs que
dan ruleta, de los tres pisos. La regla de "una cada dos combates, hasta cinco" cuenta a todos
juntos; la tabla solo decide **qué premios** tiene la rueda que sale. No están los guardias, que
tienen la suya, ni el magnate, que da la tarjeta directo.

> Durante un tiempo la tarjeta estuvo también en la tabla de los pisos 1 y 3. El builder la saca
> **una sola vez** si la encuentra ahí y vuelve al reparto de arriba.

---

## La ruleta del guardia

`Resources/Definitions/Casino/RuletaDelGuardia.asset`. **Siempre sale un Pokémon**; el ícono
marca la rareza:

| Rareza | Ícono | Pokémon | Peso |
|---|---|---|---|
| Común | Poké Ball | del pozo Poké Ball (Nv44) | 15 c/u (60%) |
| Bueno | Great Ball | del pozo Super Ball (Nv45) | 12 c/u (24%) |
| Raro | Ultra Ball | del pozo Ultra Ball (Nv46) | 12% |
| Muy raro | Master Ball | del pozo Master Ball (Nv46) | 4% |

---

## El ascensor: subir y bajar

`CasinoElevatorInteractable`. En los pisos 2 y 3 el ascensor pregunta **¿Adónde vas?**
(SUBIR / BAJAR):

- **Subir** pide la condición del piso.
- **Bajar no pide nada**: volver a un piso anterior tiene que poder hacerse siempre. Con dos pisos
  debajo pregunta a cuál (PISO 2 / PISO 1). Con más, va de a uno ("MÁS ABAJO").
- **Volver no elige nada**: cancelar la pregunta no te baja de piso (`MessageBoxResult.Cancelled`).
- **La deuda con la enfermería frena solo la subida, y solo si alcanza para pagarla.** Si alcanza,
  el ascensor pregunta si la salda ahí mismo; si no, sube igual y la deuda queda anotada
  (playtest del 5-oct: con la deuda trabada se podía quedar sin peleas, sin plata y sin poder
  curarse). Bloquear la bajada podría dejarte encerrado sin forma de juntar la plata.
- **Al llegar por el ascensor** aparecés frente a las puertas del piso de destino, no en el spawn
  de la escena.

| Piso | Para subir | Baja a |
|---|---|---|
| 1 | El pase del guardia (`Casino_Piso1_PaseAscensor`) | — |
| 2 | El pase del guardia VIP (`Casino_Piso2_PaseAscensor`) | Piso 1 |
| 3 | Tener un **Cosmog shiny** (en el equipo o en el PC) | Pisos 2 y 1 |

Una vez que se sube, el piso queda superado (`Casino_PisoN_Superado`) y no vuelve a pedir nada.

---

## Piso 2: el guardia VIP y la Tarjeta VIP

El guardia VIP es un `CasinoElevatorGuard` con **`objetoRequerido`**: no cobra, pide ver la
Tarjeta VIP.

- **Con la tarjeta** te da el pase.
- **Sin la tarjeta** ofrece pelear. Es más difícil que el del piso 1: Weavile y Spiritomb Nv48,
  Excadrill y Magnezone Nv49, con IA de líder.
- **Ganándole** te da el pase y **su ruleta exclusiva**, que siempre da un Pokémon.

**La Tarjeta VIP** (`Resources/Definitions/Items/TarjetaVip.asset`, objeto clave) se consigue de
tres formas:

1. **Ganándole al magnate** (`CasinoTrainerNpc.objetoAlGanar`): tres Pokémon de nivel 45-46,
   cuando el resto del piso anda por el 15-17.
2. **En la ruleta de premios de los combates del piso 2**: es uno de los gajos, con ~15%. Si ya
   la tenés, paga 400 monedas en su lugar (`moneyIfOwned`). En las ruletas de los NPCs de los
   pisos 1 y 3 no sale.
3. **Comprándosela al revendedor** por 1500 (`CasinoVendedorNpc`).

### La ruleta exclusiva del guardia VIP (`RuletaDelGuardiaVip.asset`)

| Rareza | Ícono | Pokémon | Peso |
|---|---|---|---|
| Común | Poké Ball | del pozo Poké Ball (Nv47) | 15 c/u (60%) |
| Bueno | Great Ball | del pozo Super Ball (Nv48) | 12 c/u (24%) |
| Raro | Ultra Ball | del pozo Ultra Ball (Nv49) | 12% |
| Muy raro | Master Ball | del pozo Master Ball (Nv49) | 4% |

---

## Piso 3: la ruleta del empresario

El empresario (`CasinoRuletaPagaNpc`) cobra **300 monedas por tiro** y gira una vez la
ruleta de premios. Se puede tirar todas las veces que alcance la plata. Si le decís que no,
dice su diálogo de historia (`charla` = `Historia_Piso3_Empresario`), así que ese texto se
sigue reescribiendo en el asset como el de los demás NPCs.

- **Precio:** `precio` del componente en la escena (o `PrecioRuletaDelEmpresario` en el
  builder; ojo, volver a armar los pisos 2 y 3 lo vuelve a ese valor).
- **Cobro:** `CasinoRuletaPagaNpc.Cobrar`, probado en `TestRuletaDelEmpresario`.

### Premios (`RuletaDelEmpresario.asset`): siempre un Pokémon

| Rareza | Ícono | Pokémon | Peso |
|---|---|---|---|
| Común | Poké Ball | del pozo Poké Ball (Nv47) | 15 c/u (60%) |
| Bueno | Great Ball | del pozo Super Ball (Nv48) | 12 c/u (24%) |
| Raro | Ultra Ball | del pozo Ultra Ball (Nv49) | 12% |
| Muy raro | Master Ball | del pozo Master Ball (Nv49) | 4% |

---

## Piso 2: la ruleta de premios por ganarle al color

En **las seis mesas** de ruleta, si en un giro le ganás **más de 200 en limpio** apostando a rojo
o negro, se abre la ruleta de premios apenas cerrás el cartel del resultado.

- **Solo cuenta lo ganado al color:** un pleno ganado en el mismo giro no suma.
- **Tiene que ser más:** 200 justos no alcanza.
- **Cómo se configura:** la tabla y el umbral están en cada `RouletteTableInteractable`
  (`ruletaPorColor` y `umbralRuletaPorColor`).
- **Dónde está la regla:** `RouletteBoard.GananciaDeColor` y `DaRuletaDePremios`.

### Premios (`RuletaDeLaMesa.asset`)

| Premio | Peso |
|---|---|
| Pokémon (Poké Ball) Nv45 | 33 |
| Pokémon (Super Ball) Nv45 | 32 |
| Poción x2 | 8 |
| Superpoción | 6 |
| Hiperpoción | 4 |
| 250 monedas | 8 |
| 500 monedas | 6 |
| 1000 monedas | 3 |

**65% Pokémon** (era 25%). Todas las ruletas de premios del casino dan alguna chance de Pokémon.

---

## Piso 3: Tacconi y el Cosmog shiny

Para subir del piso 3 hay que tener un **Cosmog shiny**. La única forma de conseguirlo es
**Tacconi** (`CasinoTacconiNpc`), que lo juega a **una mano de blackjack contra uno de tus Pokémon**:

1. Hablás con él y aceptás.
2. Elegís qué Pokémon apostás (pantalla del equipo, modo `SelectAny`) y confirmás.
3. Se abre la mesa en **modo apuesta de Pokémon** (`BlackjackScreenRequest.ApuestaPokemon`):
   - sin fichas;
   - una sola mano, sin doblar ni dividir;
   - la mesa se cierra sola al terminar.
4. Según cómo sale la mano:
   - **Ganás:** el Cosmog shiny va a tu equipo, o al PC si el equipo está lleno.
   - **Perdés:** Tacconi se queda con el Pokémon apostado.
   - **Empate:** no pasa nada y se puede volver a jugar.

**Nunca te deja sin equipo:** no acepta la apuesta si tenés un solo Pokémon o si, al perder,
no quedaría nadie que pueda pelear.

**Por qué un Cosmog shiny.** El pedido era un Pokémon raro, no muy poderoso, shiny y medio meme:

- en los juegos Cosmog **no puede salir shiny**;
- pelea con Salpicadura y Teletransporte;
- es el "Nebby, get in the bag" de Sol y Luna.

Se cambia en `CasinoTacconiNpc.especiePremio` y en `EspecieDeTacconi` del builder.

**La historia (28-sep-2026).** Antes de ofrecer la mano, Tacconi cuenta lo del guion
(`Casino_Tacconi_Presentacion`: "¿Te acordás de 2026?", los tres grupos, "vos no aparecés
en ninguno"); al perderla entrega también la **Tarjeta de acceso P4** y dice que el PJ "era
parte del desarrollo" (`Casino_Tacconi_DespuesDeGanar`). El ascensor sigue pidiendo el shiny:
lo que cambió son los textos (el lector de tarjetas del piso 4). Los NPCs de historia de los
tres pisos, el croupier de la primera mesa del piso 2 y los jugadores tienen sus textos de
historia. Ver `Historia_En_Los_Niveles.md`.

---

## Premios con el equipo lleno

Si un premio da un Pokémon y el equipo está lleno, **va al PC** y el cartel dice a qué caja. Solo
si el equipo **y** el PC están llenos se paga `moneyIfTeamFull`. Lo mismo vale para los Pokémon
liberados de la torre y para el de Tacconi.

---

## La economía (playtest del 4-oct)

Se ajustó con lo que trae el PJ al entrar al casino. Lo que suma antes:

| De dónde | Monedas |
|---|---|
| Partida nueva | 100 |
| Rival del tutorial | 100 |
| Data Center (seguridad, nodos, guardia de cámaras) | ~650 |
| Néstor | 1500 |
| Regalo de bienvenida de Tixi | 1000 |
| **Al llegar al casino** | **~2.700-3.350** (menos lo que gaste en la cueva) |

**La plata tiene que pesar.** El pase del guardia del piso 1 cuesta **4000**,
más de lo que trae el PJ: para subir pagando hay que pelear con los del piso o
jugar un rato a las máquinas, y si no, ganarle al guardia (deja pasar gratis y da
una ruleta con Pokémon). Hasta el 4-oct costaba 500 y se pagaba sin pensarlo.

| Qué | Precio |
|---|---|
| Pase del guardia del piso 1 (o ganarle) | **4000** (era 500) |
| Premio de los entrenadores del casino | 100-230 (sin cambios) |
| Tarjeta VIP (revendedor) | 1500 (sin cambios) |
| Tiro de la ruleta del empresario (siempre Pokémon Nv47-49) | **300** (era 200: un Pokémon seguro salía regalado) |
| Poción / Superpoción / Hiperpoción | 200 / 700 / 1200 (sin cambios) |
| Poción Máxima / Revivir / Cura Total | 2500 / 1500 / 400 (sin cambios) |

El número del pase está en `CasinoNpcBuilder.PrecioDelPaseDelPiso1` y en el
`precio` del guardia en la escena del piso 1: se cambia en el Inspector.

**Las tragaperras** devolvían el 86% y pagaban en 1 de cada 7 tiradas. Augusto
pidió **1 de cada 5**: se tocaron **solo los pesos** (para no contradecir la tabla
de pagos dibujada en el gabinete) y ahora pagan en 1 de cada 5 y devuelven el
~88%. La casa sigue ganando a la larga (el smoke test falla si la devolución
llega a 100%), pero con apuestas de hasta 100 y los sietes pagando 90 y 300, una
racha alcanza para el pase.
**El blackjack** ya tenía reglas estándar (el croupier se planta en 17, el
blackjack paga 3:2): no se tocó.

Varios NPCs ahora **nombran al empresario** (el anciano del piso 1, la damisela
del piso 2, el jubilado del piso 3 y Tixi antes de mandar al PJ al casino).

### Playtest del 5-oct

- **Tragaperras: 1 de cada 4** (25,1%), devolviendo ~89-90%. Otra vez solo los
  pesos (`CasinoPrefabBuilder` y `MaquinaBasica.asset`): cereza 100, Magnemite 50,
  Gastly 44, Psyduck 20, Pikachu 16, siete azul 6, siete rojo 3, comodín (Replay) 1.
- **La enfermera cobra 100** (era 30) y **fía sin tope**: siempre cura. Con plata
  y deuda, primero ofrece saldar. El ascensor solo exige la deuda si alcanza.
- **Una tienda en el piso 3** (`CasinoTiendaNpc`, la pone
  `PokeRush > Casino > Poner la tienda del piso 3`): la misma lista que Tixi
  (`TiendaDelCasino.asset` se copia de `TiendaDeTixi`), en su propio contenedor
  (`TiendaCasino`) para que rearmar los personajes del piso no la borre.
- **Las ruletas cambian de Pokémon en cada giro** (`VariedadDeLasRuletas`): la
  tabla dice cuántos gajos son Pokémon, a qué nivel y de qué rareza (el ícono de
  la ball); la especie sale de un pozo por rareza, sin repetir lo que el jugador
  ya tiene (equipo y PC) ni otro gajo de la misma rueda:

  | Ícono | Pozo |
  |---|---|
  | Poké Ball | Formas finales conocidas de poder parejo: Raichu, Machamp, Alakazam, las Eeveelutions, Luxray, Houndoom, Scizor, Heracross, Altaria, Absol, Talonflame, Corviknight, Mimikyu... |
  | Super Ball | Los iniciales finales de todas las regiones y los favoritos: Charizard, Greninja, Lucario, Gengar, Gardevoir, Togekiss, Lapras, Snorlax, Chandelure, Kingambit... |
  | Ultra Ball | Los pseudolegendarios ya evolucionados (Dragonite, Tyranitar, Garchomp, Hydreigon...) y Volcarona |
  | Master Ball | Legendarios: las tres aves, las tres bestias, Latias, Latios, Heatran, el trío espadachín, Zeraora, Tapu Koko y Tapu Lele |

  Desde el rebalanceo del 6-oct-2026 todo es forma final: el juego no tiene
  evolución, así que un Pikachu de la ruleta quedaba Pikachu toda la partida.
  La rareza dice cuán fuerte es la forma final; el nivel lo pone la tabla (ver
  `Rebalanceo_Oct_2026.md`).

## Cómo se cambia

- **Porcentajes o Pokémon de una ruleta:** abrir el asset de la tabla y tocar los `premios`.
  Los pesos son relativos, no hace falta que sumen 100. Cada premio puede dar un objeto
  (`item` + `amount`), plata (`money`) o un Pokémon (`poken` + `pokenLevel`).
- **Equipo lleno:** si sale un Pokémon con 6 en el equipo, va al PC. Solo con el PC también
  lleno se paga `moneyIfTeamFull`.
- **Umbral de la ruleta por color:** `umbralRuletaPorColor` en cada mesa de ruleta del piso 2
  (o `UmbralRuletaPorColor` en el builder, que lo pone en las seis).
- **Precio de la Tarjeta VIP:** `precio` del revendedor (o `PrecioTarjeta` en el builder).
- **Precio del tiro del empresario:** `precio` de su `CasinoRuletaPagaNpc` (arranca en 300).
- **Textos de historia:** `Resources/Definitions/Casino/Dialogos/Historia_*.asset`. Son
  **provisorios**: se reescriben en el asset sin abrir la escena. `NpcQueHabla` también
  acepta un `dialogoRepetido` para las veces siguientes.
- **Rivales:** `Resources/Definitions/Casino/*Casino.asset` y `GuardiaAscensor.asset`.
- **Retratos:** son los PNG de `Graphics/Trainers` y se usan en la caja de diálogo y en el
  combate. El builder los fuerza a **imagen entera (Single), Point y sin compresión**. Venían
  del pack con importaciones sueltas: `MALABARISTA.png` estaba partido en 4 por el recorte
  automático y el retrato mostraba un pedazo agrandado, y todos estaban borrosos por el filtro
  bilinear.
- **Textos:** van con tildes, ñ y `¿¡`. La caja usa la fuente legacy de Unity, que los dibuja.
- **Precio del guardia y textos de los NPCs:** en el componente de cada personaje, en la
  escena.

### Qué pisa el builder si se vuelve a correr

- **Rehace** el contenedor `PersonajesCasino` de cada piso y **actualiza** los rivales
  (equipo, plata, flag).
- **No pisa** los diálogos de historia ni las tablas de ruleta si ya existen. Para regenerar
  uno, borrar el asset y volver a correrlo. La única excepción es la lista
  `npcsQueLaPuedenDar`, que se mantiene al día con los NPCs que pone el builder.
- **No toca** un piso abierto con cambios sin guardar.
- Antes de guardar cada piso **verifica** que todos los interactuables se puedan alcanzar
  desde el spawn y que ningún personaje esté sobre una pared. Si algo falla, ese piso no
  se guarda.

> `CasinoInteractionBuilder` también configura los ascensores (el pase de los guardias, el
> Cosmog del piso 3 y a dónde bajan) y la ruleta por color de las mesas, así que correr
> cualquiera de los dos deja lo mismo. Ojo que ese builder **rehace las interacciones de los
> cuatro pisos**, incluido el piso 1.

---

## Cómo se prueba

- `PokeRush > Casino > Correr smoke tests del casino`:
  - la regla de la ruleta sobre miles de partidas (siempre dos si se les gana a todos, sin
    favoritos, nunca más de dos);
  - la entrega de premios (plata, Pokémon y equipo lleno);
  - que las tablas del proyecto sigan dando chance de Pokémon (las de los NPCs, 65%; el test
    pide al menos 25%) y un Pokémon seguro en la del guardia.
- Los mismos smoke tests prueban también lo de los pisos 2 y 3:
  - la ganancia al color y el umbral de la ruleta por color;
  - que el ascensor del piso 3 pida el Cosmog shiny, aunque esté en el PC;
  - la apuesta de Tacconi (ganar, perder, empatar y cuándo no acepta);
  - el revendedor;
  - que todas las tablas den chance de Pokémon y la del guardia VIP lo dé siempre.
- `PokeRush > UI > Probar y capturar la mesa de Tacconi`: entra en Play, abre la mesa como la
  abre Tacconi (apostando un Pokémon, sin fichas), juega una mano y cierra el cartel. Chequea
  que la mesa se cierre sola, que devuelva el resultado y que no se juegue plata. Capturas en
  `Codigo/PokeRush/Capturas/Tacconi/`, resultado `[CapturaTacconi]` en el Editor.log.
- En Play, recorrido completo:
  1. hablar con el guardia sin plata, perder y volver;
  2. ganarle y girar su ruleta;
  3. ganarles a los NPCs y ver que la ruleta sale una cada dos combates, hasta cinco;
  4. en el piso 2, conseguir la Tarjeta VIP (magnate, ruleta o revendedor) y mostrársela al
     guardia VIP;
  5. ganarle más de 200 al rojo en una mesa y girar la ruleta de premios;
  6. en el piso 3, apostarle un Pokémon a Tacconi hasta ganarle el Cosmog shiny, y subir;
  7. bajar con el ascensor al piso 1 y al 2.
