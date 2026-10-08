# El jefe final: la pelea contra Julieta

La última pelea del juego. Julieta no pelea solo con sus Pokémon: controla los
sistemas que rodean al jugador y se los va sacando. La pelea avanza por **fases
narrativas**, no por una sola barra de PS, y cada fase la dispara un diálogo.

Vive en la **arena de Santiago** (`Scenes/BossFinal.unity`, el último nivel del
recorrido) y también en una escena de prueba que se regenera sola. La historia
(antes, durante y después) sale de `historia.pdf` (versión del 4-oct-2026).

---

## Las cuatro fases

| Fase | Qué pasa | Cuándo termina |
|---|---|---|
| **1. Análisis** | Néstor ayuda (su lectura aliada, más presente que en el casino) y la **ruleta del jefe del casino juega a favor del jugador**: lo bueno le cae a él, lo malo al rival. Julieta pelea con la IA `Elite` y comenta lo que ve. | 5 turnos, o cuando a Julieta le quedan 4 Pokémon |
| **2. Control del sistema** | Julieta activa el NeuraRush-LINK: **desconecta a Néstor**, usa **su protocolo contra el jugador** (la Predicción de la torre 1, firmada "Julieta") y **da vuelta la ruleta** (lo bueno para ella, lo malo para el jugador). Aparece la **señal violeta** (marco que late + cartel). La IA pasa a `FinalBoss`. | 5 turnos, o cuando le quedan 2 |
| **3. Control del vínculo** | Julieta **se mete en un Pokémon del jugador**: sigue en su equipo pero no obedece, y **ataca al propio PJ**, que tiene una **barra de vida aparte**. Si llega a 0, la pelea se pierde. | 5 turnos |
| **Ruptura** | **Tixi entra a la red por el transmisor** que le dio al PJ en la cueva, y el PJ recupera a su Pokémon. **Todo vuelve a su lado**: el Pokémon, Néstor, la ruleta a favor; se apagan la lectura de Julieta y la señal. Recién ahora Julieta puede caer. | Julieta sin Pokémon: fin |

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
| `Scripts/Julieta/ServidorPrincipal.cs` | La pantalla del servidor detrás de Julieta: el final la quema y la apaga. |
| `Scripts/Julieta/FinalDelJuego.cs` | El cierre: de la arena a la cueva y de la cueva a los créditos. Mientras dura, Tixi no habla sola, la música no cambia y F2 no guarda. |
| `Scripts/Julieta/EscenaFinalEnLaCueva.cs` | La última escena, en la Quarcho Cueva: epílogo sobre negro, charla con Tixi, la cámara que se aleja y la última conexión. Se agrega sola (la cueva no se toca). |
| `Scripts/Julieta/FundidoAEscena.cs` | Fundido a negro y cambio de escena. |
| `Scripts/Julieta/CreditosFinales.cs` | Los créditos: leen `Resources/Creditos/CreditosFinales.txt`, suben solos y vuelven al menú principal. |
| `Scripts/Controllers/IDirectorDeCombate.cs` | **Lo único que se agregó al combate**: la interfaz de los ganchos (ver abajo). |
| `Editor/Julieta/PeleaJulietaAssets.cs` | Crea a Julieta (`EnemyDefinition`) y el **guion** con todos los textos. |
| `Editor/Julieta/PeleaJulietaBuilder.cs` | Arma la escena de prueba, **muda la pelea a la arena** (`BossFinal`) y arma los créditos. |
| `Resources/Definitions/Julieta/Julieta.asset` | Equipo, niveles, sprites, IA, fondo del combate. |
| `Resources/Definitions/Julieta/GuionDeJulieta.asset` | **Todo lo que se dice y cuándo cambia cada cosa**, más el epílogo y la última conexión. |
| `Resources/Definitions/Julieta/Dialogos/` | La llegada a la sede (`Final_Sede_*`) y la charla final con Tixi (`Final_Tixi_EnLaCueva`). |
| `Audio/Efectos/Final/` | Alarma, puerta, transmisor, núcleo destruido y chip apagado (del pack de Essentials). |
| `Scenes/BossFinal.unity` | **La arena de Santiago** con la pelea adentro. Está en Build Settings. |
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

- **`alEncontrarla`**: lo que se dice en la arena antes de pelear. **`alReintentar`**:
  lo corto que dice las veces siguientes, después de perder.
- **`momentos`**: lo que pasa **durante** la pelea. Cada momento tiene:
  - `fase` (o `cualquierFase`): en qué fase puede sonar;
  - `cuando`: una o varias condiciones — **alcanza con una**;
  - `lineas`: quién habla (`Julieta`, `PJ`, `Nestor`, `Sistema`), el texto, una
    cara opcional y **una acción que se ejecuta al cerrarse la línea**.
- **`alPerder`** y el final de a tramos: **`finalDelJuego`** (al caer Julieta),
  **`enElServidor`** y **`alDestruirlo`** (al tocar el servidor con X, dos veces)
  y **`conJulietaEnElSuelo`** (al hablarle después). **`yaVencida`** es lo que se
  ve si se le habla a Julieta antes de terminar con el servidor.
- El cierre: **`epilogo`**, **`enLaCueva`** (un DialogueDefinition) y
  **`ultimaConexion`** (ver "El final").

Cada momento suena **una sola vez** por pelea, en el orden de la lista.

**En los textos:** `{PJ}` es el nombre del jugador y `{POKEMON}` el del Pokémon
controlado (o el que está en el campo, si todavía no hay ninguno). Hablan
`Julieta`, `PJ`, `Nestor`, `Tixi` (por el transmisor) y `Sistema` (sin cartel: los
avisos del LINK y lo que se narra). Cada línea puede traer un **sonido** propio. Como
en el resto de los diálogos, una línea no pasa de **125 caracteres** (lo chequean
los smoke tests).

**Las condiciones** (`cuando`): `AlEmpezar`, `AlEntrarALaFase`, `TurnosEnLaFase`,
`AlRivalLeQuedan`, `RivalActivoBajo` (%), `UltimoDelRival`, `JugadorPerdio`,
`NestorLeyo`, `RuletaGiroEnLaFase`, `VidaDelPJBaja` (%), `ControladoAtacoAlPJ`,
`ControladoCayo`, `RivalSalvado`.

**Las acciones**: `PasarAFase`, `EncenderRuleta` (con a favor de quién),
`ApagarRuleta`, `EncenderNestor`, `ApagarNestor`, `EncenderLecturaDeJulieta`,
`ApagarLecturaDeJulieta`, `ControlarPokemon`, `LiberarPokemon`,
`MostrarVidaDelPJ`, `PerfilDeIA`, `SenalDeControl`, `Destello`, `RomperServidor`
(el final: apaga la pantalla del servidor y enciende
`Historia_ServidorPrincipalDestruido`, que el `ApagonDelNivel` de la arena usa para
apagar las luces).

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

## La llegada (antes de la pelea)

En la arena, la historia de la sede de la Liga (historia.pdf, "Sede central de la
Liga -- Ameghino") la cuentan tres `ZonaDeHistoria` adentro de
`MapaRaiz/HistoriaDeLaSede`, cada una una sola vez:

| Zona | Dónde | Qué pasa |
|---|---|---|
| Llegada | afuera, donde aparece el jugador | El Ameghino remodelado. "Tanto tiempo... A terminar con esto." |
| Intruso | recién entrado al edificio | ACCESO NO AUTORIZADO. INTRUSO DETECTADO. **Alarma** y pulsos rojos. |
| Puerta | la boca del pasillo a la sala | SECTOR RESTRINGIDO. ACCESO EXCLUSIVO: JULIETA. "La puerta se abre lentamente." |

`ZonaDeHistoria` ganó dos campos opcionales para esto: un **sonido** al empezar y
**pulsos** de color sobre el mapa (`DestelloDePantalla`).

## El final

1. Julieta cae (recién se puede en la ruptura) → el combate cierra como victoria.
2. `AlTerminar` apaga **todo** lo temporal: ruleta, Néstor, lectura, control,
   barra del PJ, señal. Con el cartel de victoria entra el tema
   `After-vencer-Julieta`.
3. Se activa `StoryFlag.Julieta_Vencida` en memoria, pero **la partida NO se
   guarda** (pedido de Augusto, 2-oct-2026): el juego termina en los créditos y
   vuelve a la pantalla de inicio, sin post-game. "Continuar" lleva otra vez a la
   cueva, a las puertas del final.
4. Vuelve el mapa y el final del PDF se juega **de a tramos** (playtest del
   4-oct: eran 130 cajas seguidas). `PeleaJulieta.EtapaFinal` lleva la cuenta:
   - `finalDelJuego` (10 cajas): Julieta cae y mira el servidor, que brilla;
   - el PJ va al **servidor principal** y lo toca con X (`ServidorPrincipal` es
     `IInteractable`): `enElServidor` (47), lo que Julieta explica;
   - lo toca otra vez: `alDestruirlo` (46). En el medio el PJ **destruye el
     núcleo** (`RomperServidor`): la pantalla se quema, explota el sonido y **se
     apagan las luces de la sala**;
   - le habla a **Julieta**, que quedó en el suelo: `conJulietaEnElSuelo` (29),
     el chip que se apaga y "Estoy sola en mi propia cabeza".
   La zona de la pelea ya no arranca nada después de ganarle. Un tramo vacío en
   el guion se saltea solo.
5. Fundido a la **Quarcho Cueva** (`FinalDelJuego`), que arranca en negro:
   - el **epílogo**, línea por línea (con aceptar se pasa a la siguiente);
   - aparece la cueva con el PJ parado frente a Tixi, y **Tixi charla**
     (`Final_Tixi_EnLaCueva`, el PDF);
   - la **cámara se aleja** despacio y en una pantalla se escribe la **última
     conexión** ("CONECTANDO NEURARUSH-LINK... ERROR... NOMBRE EN CLAVE: VENTRESCA
     AI"); la pantalla se apaga como un televisor viejo.
6. Fundido a `CreditosFinales` → pantalla de FIN → `Menu Principal`.

Mientras dura el cierre sigue sonando el tema de después de vencerla (la cueva no
pone el suyo), Tixi no habla sola y F2 no guarda.

### Los créditos

`CreditosFinales` arma toda la secuencia final (ver el comentario del script):

1. Entra la música de los créditos (`Creditos/`), el logo de Level Up Creators
   sobre blanco y el título. (El epílogo ya no va acá: lo cuenta la cueva.)
2. El rollo sube solo, **a la velocidad justa para terminar con la canción**.
   Tiene el reparto con sus retratos (`@reparto` en el texto).
3. **Pantalla de FIN**: "Gracias por jugar, {PJ}" y el equipo con el que terminó.
   El aviso "Presioná X para volver al inicio" aparece recién a los 3,5 s.

**No se puede acelerar ni saltear**: la tecla no hace nada hasta el aviso del
final. El texto se edita en `Assets/Resources/Creditos/CreditosFinales.txt`
(`# título`, `## sección`, `@reparto`, líneas sueltas); los retratos del reparto
los pone `PokeRush > Jefe final (Julieta) > Armar la escena de créditos`.

### La música de la pelea

Sale del `CatalogoDeAudio` (ver `Audio_Del_Juego.md`): fase 1 con su tema, las
fases 2 y 3 con el de la fase 2, y desde la **Ruptura** el tema final. Para que
el final arranque en la fase 3: campo **Fase De La Musica Final** de
`PeleaJulieta` = `ControlDelVinculo`. Al perder vuelve la música de la arena.

### Si el jugador pierde

> **Desde el playtest del 5-oct**, después de la charla de la derrota pregunta
> "¿Volvés a la Quarcho Cueva a prepararte?" (**Volver** / **Reintentar**). Volver guarda (sin
> el flag de Julieta: Tixi lo vuelve a llevar al último nivel) y funde a la cueva, donde la
> enfermera cura gratis, Tixi vende y está el Entrenamiento intenso del chip para juntar plata.
> Reintentar deja todo como antes. Es `PeleaJulieta.OfrecerVolverALaCueva`
> (`ofrecerVolverALaCueva` en el Inspector).


Por equipo debilitado o porque la vida del PJ llegó a 0: Julieta habla y **el
equipo vuelve a como entró a la pelea** (la arena no tiene enfermería). Para
reintentar, salir de la zona y volver a entrar, o hablarle con X.

---

## Cómo probarlo

**La escena de prueba:** `PokeRush > Jefe final (Julieta) > Armar la escena de
prueba` y Play. Caminá hacia Julieta. Arranca con una partida DEMO de seis
Pokémon de nivel 47-50 (desde el rebalanceo del 6-oct-2026) y **no guarda** la partida. Atajos (solo en esta escena):

| Tecla | Qué hace |
|---|---|
| **F9** | Reinicia **todo** desde cero (corta la pelea, devuelve el equipo) y la arranca de nuevo. |
| **F10** | Da por cumplida la fase actual: cambia en el turno siguiente. |
| **F11** | Deja al Pokémon activo de Julieta en 1 PS. |

**Las reglas solas:** `PokeRush > Jefe final (Julieta) > Correr smoke tests de
la pelea` (186 checks: la ruleta dada vuelta, el guion y sus fases, el
controlado, la vida del PJ, los assets, una simulación del guion del proyecto
con cuatro ritmos de KOs distintos, el cierre y la arena leída como texto).

**La pelea entera en Play:** `PokeRush > Jefe final (Julieta) > Probar la pelea
en Play` (resultados en el `Editor.log` con `[PlayJulieta]`, capturas en
`Capturas/Julieta/`). Juega el encuentro, reinicia a mitad, pierde por la vida del
PJ, recorre las cuatro fases, gana, ve el final (el servidor destruido y la sala a
oscuras), la cueva (epílogo, Tixi, la última conexión) y los créditos. Corre sobre
la escena abierta si tiene la pelea: abrí `BossFinal` para probar la arena de
verdad.

Como se tocó el combate, conviene correr también `PokeRush > Correr smoke tests`
(270) y los del casino (160): los dos dan TODOS OK con estos cambios.

---

## La arena (hecha el 4-oct-2026)

La pelea está en la arena de Santiago. La subió como `ArenaJuli_Vacia` ("primera
versión de la arena", 3-oct-2026) y se renombró **`BossFinal`** (misma GUID), que es
el nombre que ya esperaban el recorrido (`RecorridoDeLaHistoria`), el catálogo de
audio y la preparación de la release. Está en Build Settings.

`PokeRush > Jefe final (Julieta) > Mudar la pelea a la arena (BossFinal)` hace todo
y se puede volver a correr (no duplica nada):

1. Si todavía se llama `ArenaJuli_Vacia`, la renombra.
2. Pone `PeleaJulieta` (Julieta, la zona y el servidor) en la sala redonda, si no
   estaba: Julieta arriba de todo en (1, 22.5), entre las dos columnas del centro
   (su collider tapa las dos, así se le habla desde cualquiera), el servidor
   detrás y la zona en la fila 19, delante de la pokébola. El servidor se rehace
   siempre, con el pie en (1.5, 23): en el hueco de la pared, apoyado sobre la
   fila 22, desde donde se lo toca con X. Para correrlo se mueve el objeto
   `Servidor`, **no las pantallas**: el brillo y el collider van en el objeto
   (el 7-oct se movieron solo las pantallas y el final no se podía terminar).
3. Rehace `MapaRaiz/HistoriaDeLaSede`: las tres zonas de la llegada y el
   `ApagonDelNivel` del final.
4. **Abre la puerta del edificio**: en la primera versión, la puerta (1,4) estaba
   en `Colisiones` y en `Techos`, y no se podía entrar. Toda celda que esté en las
   dos capas pierde la de `Colisiones` (el dibujo queda en `Techos`).
5. Completa el rig (combate, diálogo, GameManager con un equipo de fin de juego
   para entrar por Play directo; viniendo de la cueva manda la partida), repone
   las colisiones, **verifica que se llegue caminando a la zona y que el servidor
   se pueda tocar con X** (y que su dibujo no esté corrido del objeto) y guarda.

La arena no lleva el banco de pruebas (F9/F10/F11). El mapa sigue siendo de
Santiago: el builder no pinta nada (salvo esa celda de la puerta).

> Si Santiago sigue editando la arena: tiene que traerse `BossFinal.unity` antes
> (las escenas se mergean como binario) y trabajar sobre esa, no sobre
> `ArenaJuli_Vacia`.

---

## Dónde tocar el balance

Todo desde el Inspector de `PeleaJulieta` o desde assets, sin recompilar:

- **Equipo y niveles**: `Julieta.asset` (y `PeleaJulietaAssets.Equipo`, que lo
  rehace). Hoy: Magnezone 43, Alakazam 44, Genesect 44, Metagross 45, **Hoopa
  47 y Mewtwo 50** (los dos del portal). Mewtwo es el Pokémon rival más alto del
  juego (Reymar llega a 49); el resto queda más abajo porque son legendarios y
  míticos. Ojo con Alakazam: a Nv 45 aprende Premonición (120) y la pelea se
  pone bastante más dura. Ver `Rebalanceo_Oct_2026.md`.
- **Cuándo cambia cada fase**: las condiciones de los momentos `F1 -> F2`,
  `F2 -> F3` y `F3 -> Ruptura` en el guion.
- **Néstor**: probabilidad 45%, cooldown 2, gracia 1, máximo 3 (en el casino son
  20/2/1/2 desde el playtest del 4-oct, antes 20/4/2/2: acá tiene que notarse
  que está).
- **La lectura de Julieta**: cada 3 turnos, con aviso un turno antes.
- **El controlado**: 60% en el campo, 35% fuera de orden, golpe 16% + potencia.
  **Vida del PJ**: 100.
- **La ruleta**: cada 2 turnos (la del casino). Sus gajos y pesos son los de
  `RuletaDelJefeFinal.asset`, compartida con Reymar.

> En el play test, un piloto que pega con el movimiento más fuerte sin mirar
> tipos llegó a la ruptura con la mitad del equipo caído. Es la idea (la pelea
> tiene que apretar), pero conviene jugarla a mano antes de fijar los números.

---

## Decisiones de historia (revisar)

Desde el 4-oct-2026 **la historia la manda el PDF**: la charla de antes de pelear,
el final, el epílogo y la escena de la cueva son las del PDF casi palabra por
palabra (pasadas a voseo). Lo que inventé el 28-sep para llenar huecos y que el PDF
contradice **se sacó**: el virus del PJ como la razón de su inmunidad ("un virus no
infecta a quien lo escribió"), "el LINK no enseña: escribe", "Aprobado", Mewtwo y
Hoopa desconectados en el final, Néstor hablando por el canal al final.

Lo que el PDF no cuenta es lo de **adentro** de la pelea. Lo resolví así:

- **Lo que se dice en las fases no adelanta el final.** Julieta provoca y promete
  ("¿y de lo que pasó antes en esa sala, qué te acordás? Ganame y te lo cuento");
  la explicación (los experimentos, "te estudiamos", Tixi) queda para después.
- **La ruptura la abre Tixi por el transmisor** que le da al PJ en la cueva
  ("voy a intentar abrirte el acceso", el PDF). Julieta no sabe quién entró ("¿Quién
  más está en mi red?"): se entera recién en el final ("¿Tixi?").
- **El PJ recupera a su Pokémon porque el chip nunca le pudo escribir la cabeza**
  (Tixi en la cueva: "el chip no tiene efecto sobre vos"; Reymar: "podía leer tu
  actividad cerebral sin destruirla").
- **El Protocolo Néstor y la ruleta**: "es una función de mi NeuraRush-LINK"
  (Néstor en el PDF: "una de las funciones que tenía mi LINK") y "Reymar la
  giraba, yo cambié los permisos" (Reymar en el PDF: "cambiaron los permisos").
- **Al reintentar** Julieta no repite la charla entera: dos líneas.
- **Tixi sin género gramatical**, como en el resto del juego: "¿Conocías a Tixi?",
  "intentó detenernos", "Tixi no se mueve", "¿No te enojaste?". El PDF usa "él".
- **La sala se apaga** con el `ApagonDelNivel` del Data Center, y el servidor es la
  pantalla grande de la hoja de la arena (JuliTile).

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
3. **Música**: hecha (2-oct-2026), ver "La música de la pelea".
4. **El arte de la sede**: la arena de Santiago es la primera versión. El
   servidor es la pantalla grande de JuliTile; si se dibuja uno propio, alcanza
   con cambiar los sprites de `PeleaJulieta/Servidor`.
