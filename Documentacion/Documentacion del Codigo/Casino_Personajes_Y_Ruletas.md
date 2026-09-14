# Casino: personajes y ruletas de premios (pisos 1 a 3)

Qué gente hay en los pisos del casino, qué hace cada uno y cómo se tocan los textos
y los porcentajes sin escribir código.

Todo lo arma el menú **`PokeRush > Casino > Poner los personajes de los pisos 1 a 3`**
(`Assets/Editor/Casino/CasinoNpcBuilder.cs`).

> ## ⚠️ Pendiente: los diálogos de la historia
>
> **Los textos de historia son de relleno.** Faltan los diálogos definitivos del anciano
> (piso 1), la damisela (piso 2) y el empresario (piso 3). Van cuando esté escrita la
> historia del casino.
>
> Se cargan en `Assets/Resources/Definitions/Casino/Dialogos/`:
> `Historia_Piso1_Anciano.asset`, `Historia_Piso2_Damisela.asset` y
> `Historia_Piso3_Empresario.asset`. Hay que tocar solo el asset, sin abrir las escenas.
> Volver a correr el builder no pisa los textos. Si un NPC tiene que decir otra cosa la
> segunda vez, ponerle un `grantsFlag` al diálogo y un `dialogoRepetido` al `NpcQueHabla`.
>
> Las frases de los ocho jugadores de tragaperras (`JugadorTragaperras*.asset`) también son
> provisorias, pero no son de historia.

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
| 1 | **Guardia** (`GuardiaAscensor`) | Pide 500 monedas o que le ganes para usar el ascensor. Ganarle da la ruleta de Pokémon | (3.5, 19.5), al lado del ascensor |
| 1 | Apostador | Pelea una vez (Raticate y Kadabra, Nv12). Paga 100 | (-3.5, 12.5) |
| 1 | Malabarista | Pelea una vez (Mimikyu y Klefki, Nv14) | (9.5, 15.5) |
| 1 | Motorista | Pelea una vez (Magby Nv13, Rhyhorn Nv14) | (-10.5, 4.5) |
| 1 | Pokemaníaco | Pelea una vez (Elekid Nv13, Aron Nv14) | (-9.5, 15.5) |
| 1 | Enfermera | Cura por 30 monedas o fiado | (4.5, 12.5) |
| 1 | Anciano | Historia | (5.5, 4.5) |
| 1 | 8 jugadores (`JugadorTragaperras1..8`) | Juegan a las tragaperras: dan saltitos y dicen una frase | en taburetes, ver abajo |
| 2 | Joven rica | Pelea una vez (Togepi Nv15, Sableye Nv16) | (-10.5, 13.5) |
| 2 | Modelo | Pelea una vez (Litwick Nv15, Shuppet Nv16) | (-5.5, 6.5) |
| 2 | Médium | Pelea una vez (Duskull y Gastly, Nv16) | (-10.5, 18.5) |
| 2 | Ladrón | Pelea una vez (Pawniard Nv16, Mawile Nv17) | (14.5, 10.5) |
| 2 | Enfermera | Cura | (-3.5, 4.5) |
| 2 | Damisela | Historia | (-13.5, 18.5) |
| 3 | Macarra | Pelea una vez (Sneasel y Sandile, Nv17) | (4.5, 10.5) |
| 3 | Guay | Pelea una vez (Gligar Nv17, Skarmory y Absol Nv18) | (10.5, 5.5) |
| 3 | Veterano | Pelea una vez (Donphan y Lairon, Nv18) | (-13.5, 12.5) |
| 3 | Karateka | Pelea una vez (Primeape y Riolu, Nv18) | (6.5, 4.5) |
| 3 | Enfermera | Cura | (-5.5, 4.5) |
| 3 | Empresario | Historia | (-9.5, 13.5) |

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

- **Con plata suficiente** pregunta si le pagás. Sí → cobra `precio` (500) y da el pase.
  No → ofrece pelear.
- **Sin plata** va directo a ofrecer la pelea.
- **Ganándole** da el pase y abre **su ruleta de Pokémon**, una sola vez en la partida
  (`Casino_GuardiaVencido`).
- **Perdiendo** no pasa nada: se puede volver a intentar o pagar.

El combate es medianamente difícil a propósito: Houndour Nv18, Krokorok Nv18 y Haunter
Nv19, con perfil de IA `GymLeader`. El equipo del jugador llega al casino alrededor del
nivel 15–17.

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

**Máximo dos veces por partida, y no siempre con los mismos NPCs.**

Regla (`CasinoRuletaDeNpcs`): cada vez que le ganás a un NPC de la lista, la ruleta sale con
probabilidad **k / m**:

- **k** = ruletas que quedan (2 menos las usadas);
- **m** = NPCs de la lista que todavía no venciste, contando al de recién.

Con eso:

- si les ganás a todos, salen **exactamente dos** ruletas;
- cualquier par de NPCs tiene la misma chance de ser el que la dio, en el orden que sea;
- si te salteás NPCs, salen menos (nunca más de dos).

No hace falta guardar "quiénes son los elegidos": alcanza con los flags de vencido y los dos
flags de uso (`Casino_RuletaNpc_1` y `_2`), que el guardado ya persiste.

### Premios (`Resources/Definitions/Casino/RuletaDeLosNpcs.asset`)

| Premio | Peso |
|---|---|
| Pikachu Nv15 | 13 |
| Eevee Nv15 | 12 |
| Togepi Nv15 | 10 |
| Poción x3 | 17 |
| Superpoción x2 | 15 |
| Hiperpoción | 8 |
| 150 monedas | 17 |
| 400 monedas | 8 |

**35% Pokémon, 40% curativos, 25% plata.** El campo `npcsQueLaPuedenDar` lista los flags de
los doce NPCs que la dan; el guardia no está porque tiene la suya.

---

## La ruleta del guardia

`Resources/Definitions/Casino/RuletaDelGuardia.asset`. **Siempre sale un Pokémon**; el ícono
marca la rareza:

| Rareza | Ícono | Pokémon | Peso |
|---|---|---|---|
| Común | Poké Ball | Abra, Machop, Magnemite, Houndour (Nv15) | 15 c/u (60%) |
| Bueno | Great Ball | Eevee, Porygon (Nv16) | 12 c/u (24%) |
| Raro | Ultra Ball | Scyther (Nv17) | 12% |
| Muy raro | Master Ball | Dratini (Nv18) | 4% |

---

## Cómo se cambia

- **Porcentajes o Pokémon de una ruleta:** abrir el asset de la tabla y tocar los `premios`.
  Los pesos son relativos, no hace falta que sumen 100. Cada premio puede dar un objeto
  (`item` + `amount`), plata (`money`) o un Pokémon (`poken` + `pokenLevel`).
- **Equipo lleno:** si sale un Pokémon con 6 en el equipo, se paga `moneyIfTeamFull` en su
  lugar y el cartel lo avisa (no hay PC adonde mandarlo).
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

> `CasinoInteractionBuilder` también configura el ascensor del piso 1 con el pase del
> guardia, así que correr cualquiera de los dos deja el requisito igual.

---

## Cómo se prueba

- `PokeRush > Casino > Correr smoke tests del casino`:
  - la regla de la ruleta sobre miles de partidas (siempre dos si se les gana a todos, sin
    favoritos, nunca más de dos);
  - la entrega de premios (plata, Pokémon y equipo lleno);
  - que las tablas del proyecto sigan dando 35% de Pokémon y un Pokémon seguro en la del
    guardia.
- En Play, recorrido completo:
  1. hablar con el guardia sin plata, perder y volver;
  2. ganarle y girar su ruleta;
  3. ganarles a los NPCs y ver que la ruleta sale como mucho dos veces.
