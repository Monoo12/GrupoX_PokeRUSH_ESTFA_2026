# PokeRush

**PokeRush** es un RPG de combate por turnos estilo Pokémon, en 2D y pixel art,
hecho en Unity por **Level Up Creators** (Grupo X) para la materia Evaluación de
Proyectos del Instituto Tecnológico Florentino Ameghino (2026).

El jugador despierta en 2031 en un mundo donde un chip, el NeuraRush-LINK,
controla a la gente. Desde la Quarcho Cueva sale a liberar los servidores: el
Data Center de Néstor, el casino de cuatro pisos de Martín Reymar y, al final,
la sede central de la Liga, donde espera Julieta.

- **Género:** RPG / combate por turnos (fan game académico, sin fines de lucro).
- **Plataforma:** Windows 10 y 11 (64 bits).
- **Equipo:** Augusto Brunello, Santiago Andrusiani, Thiago Koyik.

---

## Requisitos

| Qué | Versión |
|---|---|
| **Unity** | **6000.4.2f1** (Unity 6.4) — exactamente esta. |
| Unity Hub | 3.x o más nuevo |
| Módulo de Unity | **Windows Build Support (Mono)** (viene con el editor de Windows) |
| **Git** | 2.40 o más nuevo, **en el PATH** (Unity baja un paquete desde GitHub con git) |
| Sistema operativo | Windows 10 u 11 de 64 bits |
| Disco | ~15 GB para el proyecto abierto (el `Library/` que genera Unity pesa ~12 GB) |
| Hardware | cualquier PC con placa compatible con DirectX 11/12; 8 GB de RAM o más para el editor |

> No abrir el proyecto con otra versión de Unity (por ejemplo 6000.5.x): Unity
> lo "actualiza", cambia `ProjectSettings/ProjectVersion.txt` y el resto del
> equipo queda con un proyecto distinto.

---

## Instalación

1. **Clonar el repositorio**
   ```
   git clone https://github.com/Monoo12/GrupoX_PokeRUSH_ESTFA_2026.git
   ```
   (Es privado: hace falta una cuenta con acceso.)
2. **Abrir Unity Hub** e instalar el editor **6000.4.2f1** si no está
   (*Installs → Install Editor → Archive*; con el módulo *Windows Build Support (Mono)*).
3. **Agregar el proyecto:** *Projects → Add → Add project from disk* y elegir la
   carpeta **`Codigo/PokeRush`** (no la raíz del repo: el proyecto de Unity está ahí adentro).
4. **Elegir la versión:** en la lista de proyectos, la columna *Editor Version*
   tiene que decir **6000.4.2f1**.
5. **Abrir el proyecto.**
6. **Esperar la importación.** La primera vez tarda (de 15 a 40 minutos según la
   PC): Unity importa todos los assets y baja los paquetes. Es normal.
7. **Abrir la escena inicial:** `Assets/Scenes/Menu Principal.unity`.
8. **Ejecutar:** botón **Play**.

Si Unity muestra advertencias al abrir, ver [BUILD.md → Problemas comunes](BUILD.md#13-problemas-comunes-y-soluciones).

---

## Cómo ejecutar el juego

- **Escena inicial:** `Assets/Scenes/Menu Principal.unity` (es la 0 de Build Settings).
  El recorrido completo funciona solo entrando por ahí: el `GameManager` de esa
  escena arma la partida y sobrevive los cambios de escena.
- Las escenas de los niveles también se pueden abrir sueltas y darle Play: arrancan
  con una partida de prueba (`SceneBootstrapper`).

### Recorrido

`Menu Principal → MenuSaves → CreacionPersonaje → habitacion (tutorial) →
Quarcho-Cueva → NestorGOD2 → Quarcho-Cueva → Casino Nivel 1 → 2 → 3 → piso FINAL →
Quarcho-Cueva → BossFinal (arena de Julieta) → Quarcho-Cueva (epílogo y última
escena con Tixi) → CreditosFinales → Menu Principal`

### Controles

| Acción | Teclado | Joystick |
|---|---|---|
| Moverse | Flechas o WASD | Cruceta |
| Correr | Shift (mantener) | X / Cuadrado |
| Aceptar / hablar / interactuar | **X** o Enter | A / Cruz |
| Volver / abrir el menú | **Z** o Backspace (y Esc) | B / Círculo (y Start) |
| Cambiar de página (mochila, PC) | Q / E | L1 / R1 |
| Guardado rápido | F2 | — |

Todas las teclas se pueden reasignar en **Menú → Opciones**, donde también están
el volumen de música y efectos, la **resolución** y el **modo de pantalla**
(completa o ventana). Alt+Enter alterna pantalla completa.

### Dónde se guarda la partida

Una base SQLite (`pokerush.db`) en
`%USERPROFILE%\AppData\LocalLow\LevelUP Creators\PokeRush\`.
Borrar ese archivo empieza de cero (también se puede borrar la partida desde la
pantalla de partidas).

---

## Cómo hacer una build

Paso a paso en **[BUILD.md](BUILD.md)**. Resumen: abrir el proyecto y elegir
**PokeRush → Release → Generar build de Windows**. La build queda en
`Codigo/PokeRush/Builds/PokeRush_1.2.0/PokeRush.exe`.

---

## Estructura del proyecto

```
GrupoX_PokeRUSH_ESTFA_2026/
├── Codigo/PokeRush/              ← el proyecto de Unity (esto se abre en el Hub)
│   ├── Assets/
│   │   ├── Audio/                música por nivel, efectos, gritos y ataques
│   │   ├── Editor/               herramientas de editor (menú PokeRush: builders y pruebas)
│   │   ├── Fonts/                tipografías de pixel art (power clear, etc.)
│   │   ├── Graphics/             pack de gráficos de Pokémon Essentials (local; ver Git)
│   │   ├── Palettes/             paletas de tiles para pintar los mapas
│   │   ├── Prefabs/              jugador, sistema de combate, UI de diálogo y menús
│   │   ├── Resources/            datos que se cargan por código: especies, movimientos,
│   │   │                         enemigos, diálogos, catálogo de audio, créditos, menús
│   │   ├── Scenes/               las escenas del juego y las de prueba
│   │   ├── Scripts/              el código del juego (ver abajo)
│   │   ├── Sprites/              arte propio y recortes (tilesets, personajes, logos)
│   │   ├── ANDRUSIANI/           tiles y herramientas de los mapas de Santiago
│   │   ├── BlackJack God/        arte del blackjack del casino
│   │   └── TextMesh Pro/         fuentes y recursos de TextMesh Pro
│   ├── Packages/                 manifest de paquetes de Unity
│   └── ProjectSettings/          configuración del proyecto (versionada)
├── Documentacion/                documentación del código, base de datos y entregables
│   └── Documentacion del Codigo/ un .md por sistema (combate, menús, audio, casino...)
├── Diseño/                       mockups y material de diseño
├── Contexto IA/graphify-out/     grafo del código (para las sesiones con IA)
├── scripts/                      scripts de Python para procesar arte y audio
├── BUILD.md                      cómo generar la build
└── README.md
```

### `Assets/Scripts`

| Carpeta | Qué hay |
|---|---|
| `Core/` | Reglas del combate: `TurnEngine`, daño, estados, clima, habilidades, IA |
| `Controllers/` | Jugador, combate (`BattleController`), diálogos, NPCs, puertas, música de escena |
| `Views/` | Lo visual: HUD de combate, efectos de combate, jugador, pantallas de inicio |
| `Models/` | Partida en memoria: jugador, Pokémon, inventario, historia |
| `Data/` | ScriptableObjects: especies, movimientos, enemigos, diálogos, catálogo de audio, índice de definiciones |
| `Services/` | Base de datos, guardado, audio (`AudioService`, `MusicaDelJuego`, `Sonidos`, `Pasos`) |
| `Menus/` | Pausa, mochila, equipo, PC, opciones, tienda |
| `Casino/` | Tragaperras, ruleta, rueda de premios, blackjack y los NPCs del casino |
| `Lobby/` | La Quarcho Cueva y el recorrido de la historia |
| `Julieta/` | La pelea final y los créditos |
| `Tutorial/` | El tutorial de la habitación |
| `Testing/` | Bancos de prueba en Play (no se usan en el juego) |

---

## Tecnologías

- **Unity 6000.4.2f1** con **URP 2D** (Universal Render Pipeline 17.4)
- **C#** (.NET Standard 2.1, scripting backend Mono)
- **SQLite** para el guardado y la configuración (paquete `sqlite-net`)
- **Input System** 1.19 (teclado y joystick, teclas reasignables)
- **uGUI** (UI) y **Tilemap** (mapas)

## Dependencias

Las baja Unity solo al abrir el proyecto (`Packages/manifest.json`):

| Paquete | Versión | Para qué |
|---|---|---|
| `com.gilzoide.sqlite-net` | 1.3.2 (desde GitHub) | Base de datos SQLite (partida y opciones). **Necesita git en el PATH.** |
| `com.unity.render-pipelines.universal` | 17.4.0 | Render 2D |
| `com.unity.inputsystem` | 1.19.0 | Controles |
| `com.unity.2d.tilemap.extras` | 7.0.1 | Tiles de los mapas |
| `com.unity.2d.animation`, `aseprite`, `psdimporter`, `sprite`, `spriteshape` | 14.0.3 / 4.0.1 / 13.0.2 / 1.0.0 / 14.0.1 | Herramientas 2D |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 | JSON |
| `com.unity.timeline` | 1.8.12 | — |
| `com.unity.ai.assistant` | 2.17.0-pre.1 | Solo editor: el MCP que usan las sesiones con IA. No va a la build. |

Recursos de terceros (arte, música y sonido): ver `Documentacion/Creditos_De_Assets.md`
y los créditos del juego. Es material de fan game de uso académico, sin fines de lucro.

---

## Versiones

| | |
|---|---|
| Juego | **1.2.0** |
| Unity | 6000.4.2f1 |
| URP | 17.4.0 |
| Input System | 1.19.0 |
| sqlite-net | 1.3.2 |

---

## Git / GitHub

- **La organización del repo no se cambia** (lo decidimos así para no romper el
  contexto de la IA).
- **Escenas y prefabs no se fusionan como texto** (`.gitattributes`:
  `*.unity` y `*.prefab` con `merge=binary`). Si un pull o un merge marca
  conflicto en uno de esos archivos, hay que elegir una versión entera:
  `git checkout --ours -- "ruta/archivo"` (la propia) o
  `git checkout --theirs -- "ruta/archivo"` (la que llega), y rehacer en Unity lo
  que falte de la otra. **Nunca resolverlo editando el archivo a mano**: así
  quedó vacía "Casino Nivel 1" el 25-sep. Antes de tocar una escena que otro
  está editando, avisar.
- **Packs que viven solo en local** (están en el `.gitignore`):
  - `Assets/Graphics/` — pack de gráficos de Pokémon Essentials (17.000 PNG).
  - `Assets/AudioAÑIL/` — pack de audio de Essentials (351 MB, trae un `.exe`).

  Lo que el juego usa de esos packs **sí** está versionado. Si se empieza a usar
  un PNG nuevo del pack de gráficos hay que subirlo a mano:
  `git add -f "Codigo/PokeRush/Assets/Graphics/.../archivo.png" "....png.meta"`.
  Del pack de audio se copia con `python scripts/copiar_audio_essentials.py`.
- No se versionan `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` ni los
  `.csproj`/`.sln` (Unity los regenera).

---

## Especies y movimientos nuevos

El juego no carga las 445 especies al arrancar: `DefinitionRepository` busca
cada una en `Assets/Resources/Definitions/IndiceDeDefiniciones.asset` y la carga
recién cuando hace falta (la partida, el equipo, los rivales). Así la build usa
~550 MB de RAM en vez de reservar más de 5 GB.

Ese índice **se rearma solo antes de cada build**. Si se agrega una especie o un
movimiento y se prueba en el editor sin rearmarlo, no se rompe nada: la consola
avisa `[Definiciones] ... no esta en el indice` y se cargan todas como antes. Para
sacar el aviso: **PokeRush → Release → Armar indice de definiciones**.

---

## Problemas conocidos

- **La arena de Julieta (`BossFinal`) es la primera versión de Santiago**, ya con
  la pelea adentro y en Build Settings. Si Santiago la sigue pintando, tiene que
  traerse `BossFinal.unity` (antes se llamaba `ArenaJuli_Vacia`): las escenas se
  mergean como binario. Ver `Jefe_Final_Julieta.md` → "La arena".
- La primera apertura del proyecto es lenta (importación de ~25.000 archivos).

---

## Documentación

`Documentacion/Documentacion del Codigo/` tiene un `.md` por sistema: combate e IA,
menús, HUD, casino, Quarcho Cueva, historia en los niveles, tutorial, jefe final,
**audio** (`Audio_Del_Juego.md`) y cómo armar mapas, enemigos y diálogos.

Lo que cambió en la **1.1.0** (el playtest del 4-oct) está resumido en
`Playtest_4_Oct_2026.md`, y lo de la **1.2.0** (el playtest del 5-oct: el
Entrenamiento intenso del chip, el aviso del rival, aprender movimientos al subir
de nivel, los fondos de combate, la tienda del piso 3...) en
`Playtest_5_Oct_2026.md`, con el documento de cada sistema donde está el detalle.

---

## Notas de la entrega

Devolución docente y datos del grupo (roles, color, plataforma de organización): `Documentacion/Devolucion_Docente.md`.
