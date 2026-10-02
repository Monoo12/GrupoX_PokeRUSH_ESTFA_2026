# El jefe final: la pelea contra Julieta

La última pelea del juego. Julieta no pelea solo con sus Pokémon: controla los
sistemas que rodean al jugador y se los va sacando. La pelea avanza por **fases
narrativas**, no por una sola barra de PS, y cada fase la dispara un diálogo.

Está armada **aparte**, en una escena de prueba, para mudarla después a la arena
definitiva copiando un solo objeto.

---

## Las cuatro fases

| Fase | Qué pasa | Cuándo termina |
|---|---|---|
| **1. Análisis** | Néstor ayuda (su lectura aliada, más presente que en el casino) y la **ruleta del jefe del casino juega a favor del jugador**: lo bueno le cae a él, lo malo al rival. Julieta pelea con la IA `Elite` y comenta lo que ve. | 5 turnos, o cuando a Julieta le quedan 4 Pokémon |
| **2. Control del sistema** | Julieta activa el NeuraRush-LINK: **desconecta a Néstor**, usa **su protocolo contra el jugador** (la Predicción de la torre 1, firmada "Julieta") y **da vuelta la ruleta** (lo bueno para ella, lo malo para el jugador). Aparece la **señal violeta** (marco que late + cartel). La IA pasa a `FinalBoss`. | 5 turnos, o cuando le quedan 2 |
| **3. Control del vínculo** | Julieta **se mete en un Pokémon del jugador**: sigue en su equipo pero no obedece, y **ataca al propio PJ**, que tiene una **barra de vida aparte**. Si llega a 0, la pelea se pierde. | 5 turnos |
| **Ruptura** | El PJ rompe el control. **Todo vuelve a su lado**: el Pokémon, Néstor, la ruleta a favor; se apagan la lectura de Julieta y la señal. Recién ahora Julieta puede caer. | Julieta sin Pokémon: fin |

Además, cada cambio de fase se dispara **también** si el último Pokémon de
Julieta "se salvó" (ver abajo): así ninguna fase puede quedar trabada.

**Julieta no pierde antes de la ruptura.** Mientras tiene el control, su último
Pokémon queda en 1 PS en vez de caer ("el NeuraRush-LINK no lo deja caer"), y eso
mismo dispara la ruptura en el turno siguiente. Es el mismo mecanismo que usa la
fase de liberación de Néstor (`TurnEngine.EnemyCannotFaint`).

---

## Las piezas

| Archivo | Qué hace |
|---|---|
| `Scripts/Julieta/PeleaJulieta.cs` | **El director.** Ejecuta el guion, prende y apaga los sistemas, maneja el Pokémon controlado, la derrota, el reinicio y el final. |
| `Scripts/Julieta/GuionDeJulieta.cs` | Los tipos del guion: fases, disparadores, acciones, líneas y el asset. |
| `Scripts/Julieta/EstadoDePeleaJulieta.cs` | Lo que el guion mira (fase, turnos, KOs, giros...) y `GuionEnCurso`, que decide qué momento suena. **Puro**, sin escena. |
| `Scripts/Julieta/VinculoControlado.cs` | El Pokémon controlado (qué hace cada turno) y `VidaDelPJ`. **Puro**. |
| `Scripts/Julieta/ZonaDePeleaJulieta.cs` | La zona de delante de Julieta: pisarla arranca el encuentro. |
| `Scripts/Julieta/LienzoDePeleaJulieta.cs`, `VidaDelPJView.cs`, `SenalDeControlView.cs` | **UI de prueba**: la barra del PJ y la señal violeta. Se arman solas en runtime. |
| `Scripts/Julieta/FundidoAEscena.cs` | Fundido a negro y cambio de escena (del final a los créditos). |
| `Scripts/Julieta/CreditosFinales.cs` | Los créditos: leen `Resources/Creditos/CreditosFinales.txt`, suben solos y vuelven al menú principal. |
| `Scripts/Controllers/IDirectorDeCombate.cs` | **Lo único que se agregó al combate**: la interfaz de los ganchos (ver abajo). |
| `Editor/Julieta/PeleaJulietaAssets.cs` | Crea a Julieta (`EnemyDefinition`) y el **guion** con todos los textos. |
| `Editor/Julieta/PeleaJulietaBuilder.cs` | Arma la escena de prueba, prepara la arena definitiva y arma los créditos. |
| `Resources/Definitions/Julieta/Julieta.asset` | Equipo, niveles, sprites, IA, fondo del combate. |
| `Resources/Definitions/Julieta/GuionDeJulieta.asset` | **Todo lo que se dice y cuándo cambia cada cosa.** |
| `Scenes/BossJulieta_Prueba.unity` | La escena de prueba (se regenera entera, no se trabaja sobre ella). |
| `Scenes/CreditosFinales.unity` | Los créditos. Está en Build Settings. |

### Lo que se reusa (no hay versiones paralelas)

| Sistema | De dónde viene | Qué se le agregó |
|---|---|---|
| La ruleta | `CasinoRuletaDeCombate` + `RuletaDelJefeFinal.asset` (la de Reymar) + la pantalla `PrizeWheelController` | `QuienCobra`: a quién le cae cada gajo. Por defecto `SiempreElJugador`, o sea que **el casino no cambió**. |
| Néstor de ayudante | `NestorPredictionSystem` + `NestorPortraitView` | Se puede encender y apagar a mitad de pelea. |
| La lectura de Julieta | `PredictionState` (la Predicción de Néstor en la torre 1) | `Lector`: el nombre en los textos. Por defecto "Nestor", **la torre 1 no cambió**. |
| Diálogos | `DialogueController` + `DialogoCanvas` | `Cancelar()`, para cerrar una caja sin callback al reiniciar. |
| Combate, IA, HUD, FX | `BattleController`, `TurnEngine`, `BattleAIFactory`, `BattleFxView` | Los ganchos del director (abajo). |
| Mapa ↔ combate | `MapBattleBridge`, `SceneBootstrapper` | Nada. |

---

## Cómo se engancha al combate

`BattleController` no sabe nada de Julieta. Tiene un parámetro nuevo
(`StartBattle(..., director: ...)`) y llama a un **`IDirectorDeCombate`** en
momentos fijos. Sin director (todas las demás peleas) ningún gancho corre.

```
Intro (Pokémon en el campo)
   └─ director.AlEmpezar          ← suenan los momentos del arranque
Cada turno del jugador:
   director.AntesDeElegir         ← suenan los momentos que toquen (y cambian fases)
   Predicción · ruleta · menú
   El jugador elige
   director.AntesDeResolver       ← el Pokémon controlado desobedece
   IA elige · Néstor lee · TurnEngine resuelve · se reproduce
   director.DespuesDelTurno       ← el controlado ataca desde el banco
Fin: director.AlTerminar          ← apaga todo lo temporal
```

Y los "mandos" que el director puede tocar con la pelea en curso: `PonerRuleta`,
`PonerLecturaAliada`, `PonerPrediccion`, `CambiarPerfilDeIA`, `Anunciar`,
`Destello`, `Abandonar`. Son los mismos sistemas de siempre, solo que
encendibles a mitad de camino.

> **Detalle que importa:** los ganchos corren **adentro** de las corrutinas del
> combate. `PeleaJulieta` vive en `MapaRaiz`, que se apaga durante la pelea, y
> aun así funciona porque sus pasos los ejecuta el `BattleController`. Por lo
> mismo, durante el combate el jugador (y su `PlayerInputController`) está
> apagado: la tecla para avanzar las cajas la lee el director mismo.

---

## El guion (cómo se escribe y se cambia)

Todo vive en `GuionDeJulieta.asset` y se edita desde el Inspector, sin tocar código:

- **`alEncontrarla`**: lo que se dice en la arena antes de pelear.
- **`momentos`**: lo que pasa **durante** la pelea. Cada momento tiene:
  - `fase` (o `cualquierFase`): en qué fase puede sonar;
  - `cuando`: una o varias condiciones — **alcanza con una**;
  - `lineas`: quién habla (`Julieta`, `PJ`, `Nestor`, `Sistema`), el texto, una
    cara opcional y **una acción que se ejecuta al cerrarse la línea**.
- **`alPerder`**, **`finalDelJuego`**, **`yaVencida`**.

Cada momento suena **una sola vez** por pelea, en el orden de la lista.

**En los textos:** `{PJ}` es el nombre del jugador y `{POKEMON}` el del Pokémon
controlado (o el que está en el campo, si todavía no hay ninguno).

**Las condiciones** (`cuando`): `AlEmpezar`, `AlEntrarALaFase`, `TurnosEnLaFase`,
`AlRivalLeQuedan`, `RivalActivoBajo` (%), `UltimoDelRival`, `JugadorPerdio`,
`NestorLeyo`, `RuletaGiroEnLaFase`, `VidaDelPJBaja` (%), `ControladoAtacoAlPJ`,
`ControladoCayo`, `RivalSalvado`.

**Las acciones**: `PasarAFase`, `EncenderRuleta` (con a favor de quién),
`ApagarRuleta`, `EncenderNestor`, `ApagarNestor`, `EncenderLecturaDeJulieta`,
`ApagarLecturaDeJulieta`, `ControlarPokemon`, `LiberarPokemon`,
`MostrarVidaDelPJ`, `PerfilDeIA`, `SenalDeControl`, `Destello`.

> **Los cambios de fase son líneas del guion.** Por ejemplo, la fase 2 empieza
> en la línea "La casa siempre gana, {PJ}. Y la casa soy yo." (`PasarAFase`).
> Mover esa acción a otra línea cambia en qué momento de la charla ocurre.

El builder **crea** el guion solo si no existe: una vez creado es del equipo.
Para volver al del código: `PokeRush > Jefe final (Julieta) > Rehacer el guion
desde el código (pisa los cambios)`.

---

## El Pokémon controlado y la vida del PJ

Cuando Julieta entra al vínculo agarra **al Pokémon que está en el campo** (si
está debilitado, al primero en pie).

| Situación | Qué hace el controlado |
|---|---|
| En el campo y el jugador elige **LUCHAR** | "¡X no te obedece!" — 60%: se da vuelta y **ataca al PJ**; si no, usa **un movimiento suyo** contra el rival. |
| El jugador lo **cambia** o usa un **objeto** | 35%: igual sale y ataca al PJ. |
| En el **banco** | 35% por turno: "rompe su QuerryBola" y ataca al PJ. |
| Se **debilita** | El chip se apaga con él; Julieta se mete en otro (una vez). |

El golpe al PJ sale de la potencia del movimiento: `16% + potencia/1000` de su
vida (con 100 de vida: Placaje ~20, Terremoto ~26). Sacarlo del campo **ayuda
pero no lo apaga**, que es lo que convierte la fase en una lucha por sobrevivir.
La barra del PJ vive solo durante la pelea: no se guarda en la partida.

---

## El final

1. Julieta cae (recién se puede en la ruptura) → el combate cierra como victoria.
2. `AlTerminar` apaga **todo** lo temporal: ruleta, Néstor, lectura, control,
   barra del PJ, señal.
3. Se activa `StoryFlag.Julieta_Vencida` y **se guarda la partida**.
4. Vuelve el mapa y suena `finalDelJuego` en la arena.
5. Fundido a negro → `CreditosFinales` → al terminar, `Menu Principal`.

Los créditos se editan en `Assets/Resources/Creditos/CreditosFinales.txt`
(`# título`, `## sección`, líneas sueltas). Manteniendo **X** van más rápido.

### Si el jugador pierde

Por equipo debilitado o porque la vida del PJ llegó a 0: Julieta habla y **el
equipo vuelve a como entró a la pelea** (la arena no tiene enfermería). Para
reintentar, salir de la zona y volver a entrar, o hablarle con X.

---

## Cómo probarlo

**La escena de prueba:** `PokeRush > Jefe final (Julieta) > Armar la escena de
prueba` y Play. Caminá hacia Julieta. Arranca con una partida DEMO de seis
Pokémon de nivel 34-36 y **no guarda** la partida. Atajos (solo en esta escena):

| Tecla | Qué hace |
|---|---|
| **F9** | Reinicia **todo** desde cero (corta la pelea, devuelve el equipo) y la arranca de nuevo. |
| **F10** | Da por cumplida la fase actual: cambia en el turno siguiente. |
| **F11** | Deja al Pokémon activo de Julieta en 1 PS. |

**Las reglas solas:** `PokeRush > Jefe final (Julieta) > Correr smoke tests de
la pelea` (161 checks: la ruleta dada vuelta, el guion y sus fases, el
controlado, la vida del PJ, los assets, y una simulación del guion del proyecto
con cuatro ritmos de KOs distintos).

**La pelea entera en Play:** `PokeRush > Jefe final (Julieta) > Probar la pelea
en Play` (44 checks, resultados en el `Editor.log` con `[PlayJulieta]`, capturas
en `Capturas/Julieta/`). Juega el encuentro, reinicia a mitad, pierde por la
vida del PJ, recorre las cuatro fases, gana, ve el final y los créditos.

Como se tocó el combate, conviene correr también `PokeRush > Correr smoke tests`
(270) y los del casino (160): los dos dan TODOS OK con estos cambios.

---

## Mudarla a la arena definitiva

1. Abrir la escena de prueba y **copiar `MapaRaiz/PeleaJulieta`**.
2. En la arena (la de Santiago, hoy `ArenaJuli_Vacia`, ya pintada), **pegarlo
   adentro de `MapaRaiz`** y moverlo donde vaya Julieta. La zona es su hijo: se
   mueve con ella.
3. `PokeRush > Jefe final (Julieta) > Preparar la escena abierta (después de
   pegar)`: agrega lo que falte (jugador, cámara, combate, diálogo, GameManager,
   puente mapa/combate), repone las colisiones y **verifica que se llegue
   caminando a la zona**. Si algo falla, no guarda y dice qué.
4. Para que el juego llegue ahí: el recorrido espera que el último nivel se
   llame **`BossFinal`** (`RecorridoDeLaHistoria`). Renombrar la escena o cambiar
   esa entrada, y sumarla a Build Settings.

La arena definitiva no lleva el banco de pruebas (F9/F10/F11).

---

## Dónde tocar el balance

Todo desde el Inspector de `PeleaJulieta` o desde assets, sin recompilar:

- **Equipo y niveles**: `Julieta.asset`. Hoy: Magnezone 33, Alakazam 34,
  Genesect 34, Metagross 35, **Hoopa 37 y Mewtwo 38** (los dos del portal).
  Un escalón arriba de Reymar (26-29).
- **Cuándo cambia cada fase**: las condiciones de los momentos `F1 -> F2`,
  `F2 -> F3` y `F3 -> Ruptura` en el guion.
- **Néstor**: probabilidad 45%, cooldown 2, gracia 1, máximo 3 (en el casino son
  20/4/2/2: acá tiene que notarse que está).
- **La lectura de Julieta**: cada 3 turnos, con aviso un turno antes.
- **El controlado**: 60% en el campo, 35% fuera de orden, golpe 16% + potencia.
  **Vida del PJ**: 100.
- **La ruleta**: cada 2 turnos (la del casino). Sus gajos y pesos son los de
  `RuletaDelJefeFinal.asset`, compartida con Reymar.

> En el play test, un piloto que pega con el movimiento más fuerte sin mirar
> tipos llegó a la ruptura con la mitad del equipo caído. Es la idea (la pelea
> tiene que apretar), pero conviene jugarla a mano antes de fijar los números.

---

## Decisiones de historia que tomé yo (revisar)

La historia (el PDF de historia y el GDD) llega hasta el último piso del casino.
Lo que la pelea necesitaba y todavía no estaba escrito lo resolví así, sin
contradecir nada anterior; todo está en el guion y se cambia desde el Inspector:

- **Por qué el PJ es inmune.** Tacconi dijo que el PJ "no era un sujeto: era
  parte del desarrollo". En el guion, Julieta encontró el **virus que el PJ
  preparaba para vengarse del colegio** y lo usó como la capa que deja al LINK
  entrar en una cabeza. "Un virus no infecta a quien lo escribió." Con eso el
  PJ rompe el control en la ruptura ("este código es mío").
- **Para qué era de verdad el LINK.** "No enseña: escribe. Escribe lo que tenés
  que saber y borra lo que no" (coherente con Néstor: el chip controlaba sus
  recuerdos). Y su motivo: que ningún alumno se quedara atrás.
- **Julieta sabía que el PJ estaba escondido en 2026** con su virus.
- **El Protocolo Néstor y la ruleta son de ella**: "Néstor aprendió a leer una
  jugada porque yo le enseñé" (el GDD ya decía "ella le enseñó"); "Martín la
  giraba, yo la programé".
- **Mewtwo y Hoopa son sus ases** (GDD) y al final el sistema los desconecta;
  Julieta retoma la frase de Mewtwo de 2026 ("el futuro todavía puede salvarse").
- **El cierre**: los chips se desconectan, las memorias vuelven, Néstor habla por
  el canal, Julieta le dice "Aprobado" al PJ y lo manda con Tixi. No se decidió
  nada sobre los dos mundos fusionados.

## Lo que falta (equipo)

1. **Arte de Julieta (2-oct-2026)**: hoja `Graphics/Characters/julieta.png` (4x4 de
   64x80, 72 px de alto: más grande que el resto porque es la jefa final, lo pidió Augusto) y retrato `Graphics/Trainers/JULIETA.png`, sacados del arte que
   pasó el equipo en `Graphics/Juli` (Gemini). El retrato es también su sprite de combate:
   es la figura a su resolución nativa (48x92 en un lienzo de 100x100) importada a
   **PPU 62,5**, así mide en combate lo mismo que un entrenador de 160x160 a PPU 100.
   La pose quieta de frente sale de esa misma figura (la de la hoja de Gemini no tenía
   boca); a los pasos de frente se les pintó la boca 3 filas arriba del mentón. De perfil,
   la hoja de Gemini traía una sola pose mirando a la izquierda: la izquierda es el espejo
   de la derecha. La científica del pack la sigue usando la bióloga del Data Center.
   Como `Assets/Graphics` está en `.gitignore`, los dos PNG (con su `.meta`) van con
   `git add -f`.
2. **La UI es de prueba** (barra del PJ, señal, créditos).
3. **Música**: la pelea usa la del combate de siempre; `CreditosFinales` tiene un
   campo `musica` vacío.
4. **Conectarla al recorrido** cuando la arena esté pintada (ver "Mudarla").
