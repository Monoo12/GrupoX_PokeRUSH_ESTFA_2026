# La habitación tutorial

El personaje se despierta en su habitación y, antes de salir, aprende lo básico: moverse, interactuar, los menús y una pelea. Va entre la creación de personaje y la Quarcho Cueva:

```
Menu Principal -> Creacion de personaje -> HABITACION TUTORIAL -> Quarcho Cueva -> ...
```

Lo que está hecho es el **esqueleto**: toda la lógica del tutorial andando en una escena de prueba sin decorar, lista para **copiar y pegar** en la habitación decorada que arme el equipo.

---

## Qué hay en el esqueleto

`Assets/Scenes/HabitacionTutorial_Esqueleto.unity`: un cuarto de 11×9 celdas hecho con baldosas lisas que tienen escrito el nombre del mueble (CAMA, MOCHILA, PC...). Se ve que es provisorio y en Play se entiende qué es cada cosa. **No está en Build Settings** y **se regenera entero** cada vez que se corre el menú: no se trabaja sobre él, se copia.

```
MapaRaiz
├── Grid                 el cuarto de prueba (NO se copia)
├── Player / Main Camera (NO se copian: los pone "Preparar la escena abierta")
└── Tutorial             <- ESTO es lo que se copia
    ├── Despertar        donde aparece el jugador, al lado de la cama
    ├── Partes
    │   ├── Mochila      TutorialParte + BrilloInteractuable
    │   ├── Equipo
    │   ├── PC
    │   ├── Diario
    │   ├── Tele
    │   └── Companera    la pelea (un NPC con su sprite)
    └── Puerta           PuertaDelTutorial, y su hijo Tope
GameManager / Sistema / DialogoCanvas / SistemaDeCombate / EventSystem
```

Todo lo de adentro de `Tutorial` busca solo al arrancar el diálogo, el combate y los menús. **Nada apunta a un objeto de afuera**, y hay un smoke test que lo controla: al pegar en otra escena, una referencia a un objeto de la escena vieja se pierde sin avisar.

En la Scene view cada parte dibuja un recuadro amarillo con su nombre (la pelea en rojo, la puerta en verde, el punto de despertar en celeste). Sirve para ubicarlas sobre los muebles después de pegarlas.

---

## Cómo funciona

| Cuándo | Qué pasa |
|---|---|
| Al llegar | Funde desde negro, pone al jugador en `Despertar` y cuenta lo básico solo (moverse, X para usar, Z para el menú, "lo que brilla te enseña"). |
| Cada parte | Explica, abre su pantalla del menú y queda hecha: **deja de brillar**. Al cerrar el menú dice qué falta ("Te falta tu equipo, el PC y la pelea."). |
| La pelea | Se habilita cuando están hechas las demás. Si se le habla antes, dice qué falta. |
| Última parte | Activa `Tutorial_Completado` y dice el cierre. |
| La puerta | Sin terminar: dice qué falta y pregunta **Seguir / Omitir**. Terminado: "¿Salís hacia la Quarcho Cueva?". Al salir guarda y carga la cueva. |

| Parte | Tipo | Qué abre | Flag |
|---|---|---|---|
| Mochila | `Mochila` | la Bolsa (y antes da **2 Pociones**) | `Tutorial_Mochila` |
| Equipo (la Pokéball del estante) | `Equipo` | la pantalla del equipo; desde ahí, DATOS | `Tutorial_Equipo` |
| PC | `Pc` | el PC de almacenamiento | `Tutorial_Pc` |
| Diario | `Guardar` | el menú de pausa (ahí está Guardar) y cuenta F2 | `Tutorial_Guardar` |
| Tele | `Opciones` | Opciones: volumen y teclas | `Tutorial_Opciones` |
| Compañera | `Pelea` | la pelea de práctica | `Tutorial_Pelea` |

Una parte ya hecha sigue sirviendo de atajo: la mochila del cuarto abre la Bolsa sin volver a explicar.

### Lo que eligió Augusto (25-sep-2026)

- **Orden libre, la pelea al final**: todo brilla desde el principio; la pelea espera a las demás, así se llega sabiendo usar la mochila.
- **La pelea es contra un NPC en la habitación.**
- **Cada parte abre su menú** directamente (el texto aclara con qué se abre desde Z).

### Lo que elegí yo (revisar si algo no cierra)

- Una parte de menú queda **hecha al abrir el menú**, no al cerrarlo: cuenta haberlo visto, y así no depende de que el menú avise.
- La pelea **cura el equipo antes y después**, ganes o pierdas. Si perdés, no cuenta y se puede repetir.
- **Omitir da lo mismo que hacerlo**: te llevás las Pociones de la mochila. Saltearlo no deja en desventaja.
- En la puerta, **"Seguir" va primero y es lo que elige Escape**: machacar una tecla nunca saltea el tutorial.
- El rival: **COMPAÑERA** con un **Eevee Nv 10**, IA `Wild`, $100. Normal a propósito: a Lucario lo de Lucha le pega doble y a Gengar lo Normal no le hace nada, que es justo lo que cuenta la charla de antes. Sprite (`chica1`) y retrato (`CHICA`) **provisorios**, del pack y ya versionados.
- La partida DEMO (entrar por Play directo) arranca con **un Lucario Nv 20**, como una partida recién creada.

---

## Cómo pasarlo a la habitación decorada

1. **La escena decorada**: el `Grid` con las capas de siempre (`Suelo`, `Decorado`, `Colisiones`, `Techos`; ver `Como_Armar_Un_Mapa_Nuevo.md`). Guardala como **`Assets/Scenes/HabitacionTutorial.unity`**: es el nombre que espera el recorrido (`RecorridoDeLaHistoria.EscenaDelTutorial`).
2. Abrí el esqueleto, seleccioná **`MapaRaiz/Tutorial`** y copialo (Ctrl+C). Abrí la habitación decorada y pegalo (Ctrl+V) en cualquier lado.
3. **Acomodá las piezas** (con los recuadros de la Scene view):
   - cada parte **sobre su mueble**: la mochila sobre la mochila dibujada, el PC sobre el escritorio... Si el mueble ocupa otra cantidad de celdas, cambiá el `Size` del `BoxCollider2D`;
   - `Despertar` al lado de la cama, en una celda libre;
   - `Puerta` en la celda de la salida (el felpudo), con su `Tope` del lado de afuera. Si la puerta queda en otra pared, mové el `Tope` con ella;
   - la compañera en una celda libre. Su sprite está en su hijo `Visual`: cuando esté el arte definitivo, se cambia ahí.
4. Corré **`PokeRush > Tutorial > Preparar la escena abierta (después de pegar)`**. Agrega lo que falte (jugador, cámara, GameManager, combate, diálogo, EventSystem), mete el mapa y el tutorial adentro de `MapaRaiz`, repara las colisiones y **verifica que desde la cama se llegue caminando a cada parte y a la puerta**. Si algo no se alcanza, **no guarda** y dice qué parte es. Se puede correr las veces que haga falta.
5. **Play** directo en la escena para probarla (arranca una partida DEMO).
6. Cuando esté lista: **`PokeRush > Tutorial > Conectar la escena abierta al recorrido`**. La agrega a Build Settings y la creación de personaje pasa a llevar ahí.

> Del esqueleto se copia **solo `Tutorial`**. El cuarto de prueba, el jugador y la cámara se quedan; "Preparar la escena abierta" pone todo lo demás.

---

## Los textos

Todo lo que se dice en el tutorial es **de relleno pero útil**: explica los controles de verdad. Lo reescribe el equipo, y ningún menú lo pisa.

| Qué | Dónde |
|---|---|
| Al despertar, cada parte y el cierre | `Assets/Resources/Definitions/Tutorial/Dialogos/Tutorial_*.asset` (un `DialogueDefinition` por cosa; el hablante y el retrato también van ahí) |
| El rival (equipo, plata, retrato, fondo) | `Assets/Resources/Definitions/Tutorial/RivalDelTutorial.asset` |
| Frases cortas ("Te falta {0}.", la pregunta de la puerta, "Primero terminá lo demás...") | en el Inspector de cada componente (`TutorialDeLaHabitacion`, `TutorialParte`, `PuertaDelTutorial`) |

Dos cuidados:

- Los nombres tienen que ser **los que ve el jugador**: en el menú de pausa la mochila es **Bolsa** y el equipo **Pokemon**; en el equipo, el resumen es **DATOS**; en combate, **LUCHAR / POKÉMON / MOCHILA / HUIR**.
- Una pregunta de **dos opciones** usa la ventanita del Sí/No, que tiene **ancho fijo**: las opciones van cortas ("Seguir", "Todavía no"). "Seguir el tutorial" se partía en dos renglones que se pisaban.

---

## Agregar o sacar una parte

- **Sacar**: borrar el objeto. Para que exista pero no frene la puerta, destildar `obligatoria`.
- **Agregar**: duplicar una parte y cambiarle `tipo`, `nombre` (cómo se nombra en "te falta ...") y `explicacion`. Necesita un **flag propio**: se agrega uno nuevo **al final** de `Assets/Scripts/Core/StoryFlag.cs` (`Tutorial_...`). Las escenas guardan el flag como número, así que meterlo en el medio le cambia el valor a todos los de abajo. El brillo toma el flag de la parte solo.
- `SoloCharla` sirve para algo que solo explica, como un póster.
- Si dos partes comparten flag se completan juntas: el tutorial lo avisa en la consola al arrancar.

---

## Piezas de código

| Archivo | Qué es |
|---|---|
| `Scripts/Tutorial/TutorialDeLaHabitacion.cs` | El despertar, el progreso, el cierre y omitir. Va en el objeto `Tutorial`. |
| `Scripts/Tutorial/TutorialParte.cs` | Cada cosa que brilla. |
| `Scripts/Tutorial/PuertaDelTutorial.cs` | La salida: zona para pisar (trigger en `Default`, no en `Obstacles`). |
| `Scripts/Tutorial/ReglasDelTutorial.cs` | Las reglas sin escena: qué falta, cuándo va la pelea, cuándo abre la puerta, curar. |
| `Editor/Tutorial/TutorialBuilder.cs` | Los tres menús. La tabla de posiciones del cuarto de prueba está al principio. |
| `Editor/Tutorial/TutorialAssets.cs` | Crea los textos y el rival (una sola vez). |

Se tocaron tres cosas que ya existían:

- **`BrilloInteractuable`** también toma el flag de una `TutorialParte`. Además, ahora lo resuelve en una variable propia y ya no lo escribe en el campo del Inspector. Antes quedaba guardado fijo en la escena (NestorGOD2 lo tiene así, sin problema), y cambiar después el flag del trigger no le llegaba al brillo.
- **`RecorridoDeLaHistoria`**: `EscenaDelTutorial` y `EscenaDespuesDeCrearPersonaje`, que es la habitación si está en Build Settings y, si no, la cueva. `Conectar el recorrido` de la cueva escribe eso, así que volver a correrlo no desconecta el tutorial.
- Los flags `Tutorial_*`, al final de `StoryFlag`.

---

## Pruebas

| Menú | Qué hace |
|---|---|
| `PokeRush > Tutorial > Correr smoke tests del tutorial` | 88 checks de reglas, datos y del esqueleto (incluido el de referencias para el copy-paste). `[SmokeTutorial]` en el Editor.log. |
| `PokeRush > Tutorial > Probar el esqueleto en Play` | Recorre el tutorial como un jugador: despertar, puerta cerrada, pelea antes de tiempo, las cinco partes, la pelea, el cierre, la puerta abierta, omitir y la carga de la cueva. 55 checks, `[PlayTutorial]` en el Editor.log, capturas en `Capturas/Tutorial/`. Juega con la partida DEMO y **no guarda**. |

Los dos pasaban completos el 25-sep-2026.

---

## Trampas que ya mordieron

- **`Application.CanStreamedLevelBeLoaded` da `false` para todas las escenas en el editor fuera de Play**, incluida la cueva. Lo que pregunta por Build Settings desde un menú del editor usa `RecorridoDeLaHistoria.EstaEnBuildSettings`.
- La ventanita de **dos opciones** es de ancho fijo (ver *Los textos*).
- La puerta es una **zona para pisar**: en `Obstacles` el jugador frenaría en el borde y nunca entraría (igual que la salida de la cueva).

---

## Pendiente (del equipo)

- La **habitación decorada** y pasarle el esqueleto (pasos de arriba).
- Los **textos** definitivos: quién despierta al personaje, quién es la compañera, el cierre.
- El **sprite y el retrato** de la compañera.
- La **música** de la habitación.
- Borrar el arte del esqueleto (`Assets/Sprites/Tutorial/Esqueleto`) si ya no hace falta. Solo lo usa el cuarto de prueba.
