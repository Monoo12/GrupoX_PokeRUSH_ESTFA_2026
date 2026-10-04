# El piso final del casino: el pasillo y el jefe

El último piso: un pasillo de guardias, una mesa de póker al fondo y, atrás de
ella, el jefe de todo el nivel. Su pelea tiene dos cosas que no tiene ninguna
otra del juego: una **ruleta que aparece cada dos turnos** y le pega a tu Pokémon,
y una **mano de blackjack al terminar** que puede reabrir el combate.

Todo lo visible se genera con un builder del editor, como el resto del casino.

---

## Las piezas

| Archivo | Qué hace |
|---|---|
| `Scripts/Casino/Core/CasinoRuletaDeCombate.cs` | **Las reglas de la ruleta de combate.** Qué hace cada gajo y cada cuántos turnos aparece. No toca la escena ni la UI: se prueba sin abrir un combate. |
| `Scripts/Casino/World/CasinoJefeFinalNpc.cs` | El jefe en el mapa: lanza la pelea con la ruleta, reparte la mano de blackjack al terminar y arranca la revancha. |
| `Editor/Casino/CasinoPisoFinalBuilder.cs` | Arma la escena: los seis guardias, el jefe y el ascensor de bajada. Crea el rival y la tabla de la ruleta. |
| `Resources/Definitions/Casino/JefeFinalCasino.asset` | El rival: seis Pokémon de nivel 26-29, perfil de IA `Elite`. |
| `Resources/Definitions/Casino/RuletaDelJefeFinal.asset` | Los ocho gajos de la ruleta, con sus pesos. |
| `Sprites/Casino/martin_sentado.png` | El busto de Martín, 51x52. Lo prepara `scripts/preparar_sprites_casino.py` a partir de `Assets/Martin/`. |
| `Graphics/Trainers/MARTIN.png` | Su retrato de la caja de diálogo, 160x160. |

Lo demás **ya existía y se reusa entero**: la pantalla de la ruleta
(`PrizeWheelController`), la mesa de blackjack (`BlackjackScreen`), el combate
(`BattleController`) y las herramientas del builder de los pisos 1 a 3
(`CasinoNpcBuilder.ArmarPiso`, `Personaje`, `Enemigo`, `Dialogo`).

---

## Cómo está armada la escena

`Assets/Scenes/Casino piso FINAL.unity`. El mapa ya venía dibujado; el builder
solo cuelga los objetos.

```
  y=30.6   [ MARTIN ]              ← busto, metido atrás de la mesa
  y=29-30  ▓▓▓ mesa de póker ▓▓▓
  y=26-29  ░░░ ZONA ░░░            ← pisarla arranca la pelea
  y=23-25  sala
  y=20-21  puerta angosta
  y=4-19   pasillo: 3 guardias por lado, alfombra en el medio
  y=3      ascensor (solo baja)
```

**Al jefe no se le habla: la pelea la dispara una zona.** El objeto `JefeFinal`
vive donde está la **hitbox** (5x3 celdas delante de la mesa) y su `Collider2D`
está en *Is Trigger*. Martín es un hijo de ese objeto, con la posición puesta en
mundo: la zona y el dibujo están en lugares distintos a propósito.

**Martín está sentado.** Su sprite es un **busto** (de la cintura para arriba) y
se apoya en y = 30,6, o sea 0,4 celdas metido dentro de la mesa. Para que la mesa
lo tape en vez de taparse él, su `sortingOrder` es **5**: los NPCs van en 15 y la
mesa vive en el tilemap `Colisiones`, que dibuja en 10.

> **La zona va en la capa `Default`, no en `Obstacles` como el resto de las
> interacciones del casino.** No es un descuido. El proyecto tiene *Queries Hit
> Triggers* prendido y el jugador decide si puede pisar una celda con
> `Physics2D.OverlapCircle(destino, radio, obstacleMask)`, donde `obstacleMask`
> **es** `Obstacles`. Un trigger puesto en `Obstacles` frena al jugador igual que
> una pared: se quedaría parado en el borde de la zona y la pelea no arrancaría
> nunca. En `Default` no lo frena y el trigger igual le llega, porque el jugador
> está en `Default`, tiene `Rigidbody2D` cinemático y `Default` choca con
> `Default`.

**La zona se desarma hasta que el jugador sale.** `MapBattleBridge` apaga el mapa
entero durante la pelea, así que al volver Unity re-dispara el `OnTriggerEnter2D`
con el jugador todavía parado adentro. Es el mismo cuidado que tiene
`BattleZoneTrigger`; sin eso, la pelea se relanza en bucle.

**El ascensor solo baja.** El último piso no tiene enfermería: sin una salida,
perder contra el jefe dejaba al jugador encerrado con el equipo debilitado.
No hay puertas dibujadas todavía — el arte va sobre las celdas (-1..1, 3).

**Se llega por ARRIBA del ascensor.** En los pisos 1 a 3 las puertas están en la
pared de arriba y se aparece en la celda de abajo (`llegada` = (0, -1), el
default). Acá están en la pared de abajo, así que `llegada` = (0, 1): con el
default el jugador aparecía afuera del mapa, en negro y sin poder moverse. El
smoke test `TestAscensoresDejanEnElPiso` revisa la llegada de los cuatro pisos.

---

## La ruleta de combate

Es la **misma ruleta de premios** de los pisos de abajo: mismo prefab, misma
animación, mismos ocho gajos. Lo único que cambia es quién cobra el premio.

`PrizeWheelRequest` tiene un campo `Entregar`: si está vacío el premio lo cobra
`PrizeGranter` (objeto, plata o Pokémon, como siempre); si está puesto, lo cobra
quien abrió la ruleta. `BattleController` pasa el suyo y aplica el efecto sobre
el Pokémon que está peleando.

Cada `Prize` tiene ahora un `efectoDeCombate`. En los premios de mapa es
`Ninguno` y no pasa nada, así que las ruletas de los otros pisos siguen igual.

**A quién le cae cada gajo** lo decide `CasinoRuletaDeCombate.QuienCobra`. Acá es
`SiempreElJugador` (el valor por defecto): todo le pega al Pokémon del jugador. La
pelea contra Julieta reusa **esta misma ruleta** con los otros dos modos
(`AFavorDelJugador` y `AFavorDelRival`), ver `Jefe_Final_Julieta.md`.

### Los ocho gajos

| Gajo | Qué hace | Peso |
|---|---|---|
| Ataque | +2 al Ataque | 15 |
| Poción | cura media barra | 14 |
| Ataque flojo | −2 al Ataque | 13 |
| Defensa floja | −2 a la Defensa | 13 |
| Cambio de tipo | pasa a ser de UN tipo sorteado | 12 |
| La casa cobra | le saca 1/8 de los PS máximos | 12 |
| Turno perdido | pierde el turno | 11 |
| Otro giro | vuelve a girar | 10 |

Aparece en los **turnos 2, 4, 6...** El turno 1 queda libre para ver con quién
peleás antes de que la casa meta mano. La cadencia es fija y no sorteada, por lo
mismo que la Predicción del boss de la torre: así se puede planificar (guardás la
jugada fuerte para el turno limpio) y no cambia al cargar la partida.

### Detalles que importan

- **"La casa cobra" nunca debilita**: deja 1 PS de piso. Un KO fuera de turno
  dejaría el combate pidiendo reemplazo cuando el motor no lo espera.
- **"Turno perdido" reusa el retroceso** (`BattlePokenState.Flinched`), que es
  exactamente eso y ya está probado.
- **"Otro giro" se corta a los 3 encadenados**, o con mala suerte la ruleta se
  queda girando sola.
- **El cambio de tipo vale para todo.** `BattlePokenState.TipoCambiado` lo leen
  `Tipo1`/`Tipo2`, y el combate pregunta por esas dos y no por
  `Poken.Definition.type`: efectividad, STAB e inmunidades de estado siguen al
  tipo nuevo. Se pierde al cambiar de Pokémon, que es la semántica correcta.

---

## La mano de blackjack y la revancha

Cuando la pelea termina —la gane quien la gane— hay **una mano 1 contra 1**. Usa
el modo de apuesta que ya tenía la mesa para Tacconi
(`BlackjackScreenRequest.ApuestaPokemon`): una sola mano, sin fichas, sin doblar
ni dividir, y se cierra sola.

Lo que se decide es si **el derrotado se levanta**:

| Resultado para el derrotado | Qué pasa |
|---|---|
| Gana (o blackjack) | revive un Pokémon al azar con la vida **llena** |
| Empata | revive uno al azar con la **mitad** |
| Pierde | no revive nadie y la pelea queda como terminó |

Con alguien revivido, **la pelea sigue**: `BattleController.Revancha` vuelve a
empezar con el **mismo `EnemyModel`**, o sea con los equipos como quedaron.
Rearmar el rival desde su definición lo traería con los seis Pokémon enteros y la
mano no significaría nada.

**La mano la reparte siempre el jugador**, porque es la única mesa que hay. Si el
derrotado es el jefe, el resultado se da vuelta: que el jefe gane su revancha es
que el jugador pierda la mano. Eso lo traduce `CasinoJefeFinalNpc.QuienGana`.

**Hay una sola mano por encuentro** (`maximoDeRevanchas`, en el Inspector). Sin
tope, dos jugadores parejos se quedan jugando para siempre.

---

## Dónde tocar el balance

Todo desde el Inspector o desde assets, sin recompilar:

- **El equipo del jefe y los niveles**: `Resources/Definitions/Casino/JefeFinalCasino.asset`.
  Hoy son seis de nivel 26-29 con IA `Elite`, un escalón arriba del guardia VIP
  (cuatro de nivel 21-23 con `GymLeader`) y todavía por debajo de `FinalBoss`.
- **Los pesos de la ruleta**: `Resources/Definitions/Casino/RuletaDelJefeFinal.asset`.
  El builder **no la pisa** si ya existe.
- **Cada cuántos turnos gira** y **cuántas revanchas hay**: en el
  `CasinoJefeFinalNpc` de la escena.
- **Cuánto mueve cada gajo** (los 2 escalones, el 1/8, la media barra): las
  constantes de `CasinoRuletaDeCombate`.

---

## Cómo probarlo

**Sin caminar el casino.** `BattleTester` (escena `CombatePrueba`) tiene ahora un
campo `Ruleta De Combate`: poniéndole `RuletaDelJefeFinal.asset`, la pelea de la
tecla **B** abre la ruleta cada dos turnos. Es la forma rápida de tocar números.

**Las reglas solas.** `PokeRush > Casino > Correr smoke tests del casino` (153
checks; los últimos ocho grupos son de este piso). También conviene correr
`PokeRush > Correr smoke tests` (270 checks): el cambio de tipo toca el núcleo
del combate.

**Rehacer la escena.** `PokeRush > Casino > Poner el piso final (pasillo y jefe)`.
Es idempotente: borra lo que puso antes y lo vuelve a crear, y antes de guardar
verifica que todo se pueda alcanzar caminando desde el spawn. **No toca los pisos
1 a 3.**

---

## Lo que salió del playtest (17-sep)

Se jugó el piso entero en Play Mode: pasillo, guardia, jefe, ruleta, derrota,
mano de blackjack, revive y revancha. Lo que apareció:

- **Los NPCs se veían chicos.** No era del piso final: el jugador dibuja con
  PPU 100 y escala 4 (0,04 unidades por píxel de arte) y las hojas de personaje
  se cortan a PPU 32 (0,03125), así que **todos** los NPCs del casino salían un
  28% más chicos. Arreglado en `CasinoNpcBuilder.EscalaDelVisual`, que es el
  cociente entre los dos y no un número a ojo.

  **Los pisos 1 a 3 siguen con el tamaño viejo**: la corrección va en el builder,
  no en las escenas ya guardadas. Para emparejarlos hay que volver a correr
  `PokeRush > Casino > Poner los personajes del piso 1` y `... de los pisos 2 y 3`.

- **El jefe pega muy fuerte contra Fantasma y Psíquico.** Su equipo es todo
  Siniestro, así que un Gengar de nivel 30 se come dos golpes de un Absol de
  nivel 26 y cae (el primero le sacó 80 de 81 PS). Es temático y es un jefe
  final, pero conviene saberlo: quien llegue con un equipo Fantasma sin respuesta
  Lucha/Hada/Bicho la va a pasar mal. Si se quiere suavizar, lo más limpio es
  bajarle el nivel a Absol y a Weavile antes que tocar la ruleta.

- **El sorteo de la rueda estaba bien.** Salieron tres POCIÓN seguidas (14% cada
  una: 0,3%), así que se verificó: 4000 tiradas dan cada gajo dentro del 1% de su
  peso. Lo que faltaba era el test, y ahora está
  (`TestSorteoDeLaRuedaDePremios`, cubre todas las ruletas del juego).

- **El ascensor de bajada anda** y el tope de una mano por encuentro también
  ("Se acabaron las manos. La casa cierra.").

Detalle del banco de pruebas, no del juego: Unity **no avanza frames sin foco**
(`Application.runInBackground` está en false), así que un playtest manejado desde
fuera del editor parece congelado. Se destraba poniendo
`Application.runInBackground = true` en la sesión de Play.

---

## Lo que falta (equipo)

1. ~~Los textos de los seis guardias son de relleno.~~ Desde el 28-sep-2026 cada
   guardia dice algo distinto de la historia, y el jefe tiene `dialogoAntes`
   (`Casino_Reymar_Antes`, la primera vez) y `dialogoDespues` (`Casino_Reymar_Despues`).
   Desde el 4-oct-2026 los dos son los de `historia.pdf`: el laboratorio que el
   casino tapa, "participé en tu preparación", el acceso denegado y el núcleo
   destruido. Ver `Historia_En_Los_Niveles.md`.
2. **Los iconos de la ruleta son items del pack** (Ataque X, orbe tóxico, cola
   lenta, placa en blanco, monedero). Se leen, pero no son arte propio.
3. **El ascensor no tiene puertas dibujadas.**
