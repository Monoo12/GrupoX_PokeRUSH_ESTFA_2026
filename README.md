# PokeRush

**PokeRush** es un RPG de combate por turnos estilo Pokémon, en 2D y pixel art,
hecho en Unity por **Level Up Creators** (Grupo X) como proyecto final de la tecnicatura en el
Instituto Tecnológico Florentino Ameghino (2026).

El jugador despierta en 2031 en un mundo donde un chip, el NeuraRush-LINK,
controla a la gente. Desde la Quarcho Cueva sale a liberar los servidores: el
Data Center de Néstor, el casino de cuatro pisos de Martín Reymar y, al final,
la sede central de la Liga, donde espera Julieta.

- **Género:** RPG / combate por turnos (fan game académico, sin fines de lucro).
- **Plataforma:** Windows 10 y 11 (64 bits).
- **Versión actual:** 1.4.0
- **Equipo:** Augusto Brunello, Santiago Andrusiani, Thiago Koyik.

---

## Dos formas de montarlo

| | **A. Jugar con el release** | **B. Abrir el proyecto en Unity** |
|---|---|---|
| Para quién | Quien solo quiere **jugar** o probar el juego | Quien quiere **editar, compilar o revisar el código** |
| Qué se necesita | Windows 10/11 y el `.zip` del release | Git, Unity Hub y **Unity 6000.4.2f1** |
| Espacio en disco | ~0,5 GB (el .zip y la carpeta descomprimida) | ~15 GB (repo + `Library/` que genera Unity) |
| Tiempo | 2 minutos | 15–40 minutos la primera vez (importación) |

> **Ojo:** el repositorio es **privado**, y los releases de un repo privado
> también. En las dos formas hace falta iniciar sesión en GitHub con una cuenta
> a la que el equipo le haya dado acceso a `Monoo12/GrupoX_PokeRUSH_ESTFA_2026`.

---

## Requisitos generales

| Qué | Detalle |
|---|---|
| Sistema operativo | **Windows 10 u 11, 64 bits.** Es la única plataforma probada y construida. El editor de Unity también corre en macOS y Linux, pero el juego solo se compila y se prueba para Windows. |
| Placa de video | Cualquier GPU compatible con **DirectX 11/12** (la build trae `D3D12`). Con los drivers al día. |
| RAM | El juego usa ~550 MB. Para abrir el **editor** de Unity, 8 GB o más. |
| Cuenta | GitHub con acceso al repo (ver arriba). |
| Teclado o joystick | Teclado, o un joystick/gamepad compatible con el Input System (se puede reasignar todo). |

No hace falta instalar .NET, Visual C++ ni DirectX aparte: el runtime de Mono
y lo que necesita Unity viajan dentro de la carpeta del juego.

---

## A. Montarlo con el release (solo jugar)

**Qué se necesita:** Windows 10/11 de 64 bits, una cuenta de GitHub con acceso y
un programa para descomprimir `.zip` (el Explorador de Windows alcanza).

1. **Bajar el release.** Entrar a
   <https://github.com/Monoo12/GrupoX_PokeRUSH_ESTFA_2026/releases/latest>,
   iniciar sesión y descargar **`PokeRush_1.4.0.zip`**.

   Con la [CLI de GitHub](https://cli.github.com) (`gh auth login` antes):
   ```bash
   gh release download v1.4.0 --repo Monoo12/GrupoX_PokeRUSH_ESTFA_2026 --pattern "PokeRush_1.4.0.zip"
   ```
2. **Descomprimir la carpeta entera** (clic derecho → *Extraer todo…*). Hacerlo
   en una ruta normal, por ejemplo `C:\Juegos\PokeRush_1.4.0`. No abrir el
   `.exe` desde adentro del .zip.
3. **Abrir `PokeRush.exe`.** El `.exe` **solo no anda**: necesita las carpetas
   `PokeRush_Data/`, `MonoBleedingEdge/` y `D3D12/` y las `.dll` que están a su
   lado, así que la carpeta no se separa ni se mueve a pedazos.
4. **Jugar.** Se abre en el menú principal.

Contenido de la carpeta descomprimida:

```
PokeRush_1.4.0/
├── PokeRush.exe              ← el juego
├── PokeRush_Data/            ← los datos del juego
├── MonoBleedingEdge/         ← el runtime de Mono
├── D3D12/, dstorage*.dll     ← DirectX 12
├── UnityPlayer.dll
└── UnityCrashHandler64.exe
```

**Si Windows avisa al abrir el .exe:** *SmartScreen* dice "Windows protegió su PC"
porque la build no tiene firma digital. Elegir **Más información → Ejecutar de
todas formas**. Si el antivirus lo bloquea, agregar la carpeta como excepción.

**Dónde se guarda la partida:**
`%USERPROFILE%\AppData\LocalLow\LevelUP Creators\PokeRush\pokerush.db`
(una base SQLite). Borrar ese archivo empieza de cero. Para desinstalar el juego
alcanza con borrar la carpeta y, si se quiere, esa base.

---

## B. Montarlo descargando el repo y abriéndolo en Unity

Para editar el juego, correrlo desde el editor o generar una build propia.

### B.1 Programas necesarios

| Qué | Versión | Notas |
|---|---|---|
| **Unity Hub** | 3.x o más nuevo | <https://unity.com/download> |
| **Unity Editor** | **6000.4.2f1** (Unity 6.4) — *exactamente esta* | Sale de `ProjectSettings/ProjectVersion.txt` |
| Módulo de Unity | **Windows Build Support (Mono)** | Viene con el editor de Windows. No hacen falta IL2CPP, Mac, Linux, Android ni WebGL |
| **Git** | 2.40 o más nuevo, **en el PATH** | Unity baja un paquete desde GitHub usando git (ver B.3). `git --version` tiene que responder en una consola |
| Editor de código (opcional) | Visual Studio 2022 o Rider | Los paquetes `ide.visualstudio` / `ide.rider` ya están en el proyecto |
| Sistema operativo | Windows 10/11 de 64 bits | |
| Disco | ~15 GB libres (más ~5 GB si se genera una build) | El `Library/` que arma Unity pesa ~12 GB |
| Internet | Sí, la primera vez | Para clonar, instalar el editor y bajar los paquetes |

> **No abrir el proyecto con otra versión de Unity** (ni siquiera una más nueva
> como 6000.5.x): Unity lo "actualiza", cambia `ProjectVersion.txt` y el resto del
> equipo queda con un proyecto distinto.

### B.2 Instalar el editor correcto

Si no está instalado **6000.4.2f1**, abrir este enlace con Unity Hub instalado:

```
unityhub://6000.4.2f1/7a4c1aeef971
```

o a mano: *Unity Hub → Installs → Install Editor → Archive → download archive* y
buscar **6000.4.2f1**. Al elegir los módulos, dejar tildado **Windows Build
Support (Mono)**.

### B.3 Clonar el repositorio

```bash
git clone https://github.com/Monoo12/GrupoX_PokeRUSH_ESTFA_2026.git
cd GrupoX_PokeRUSH_ESTFA_2026
```

El repo pesa ~500 MB. Git va a pedir iniciar sesión en GitHub la primera vez.
**Git LFS no hace falta.** Es opcional bajar el `.zip` del repo desde GitHub
(*Code → Download ZIP*), pero conviene clonar para poder hacer pull y push.

### B.4 Abrir el proyecto en Unity

1. **Unity Hub → Projects → Add → Add project from disk.**
2. Elegir la carpeta **`Codigo/PokeRush`**. *No* la raíz del repo: el proyecto de
   Unity está una carpeta más adentro.
3. En la lista de proyectos, la columna *Editor Version* tiene que decir
   **6000.4.2f1**. Abrirlo.
4. **Esperar la importación.** La primera vez tarda de 15 a 40 minutos según la
   PC: Unity importa ~25.000 archivos y baja los paquetes. Es normal. Dejarlo
   terminar (la barra de abajo a la derecha tiene que desaparecer).
5. Cuando termina aparece el menú **PokeRush** en la barra de arriba.
6. Abrir `Assets/Scenes/Menu Principal.unity` y apretar **Play**.

### B.5 Paquetes y librerías

Los baja Unity solo al abrir el proyecto, según `Packages/manifest.json`. **No
hay que instalar nada a mano** (salvo tener Git, por el primero de la lista).

| Paquete | Versión | Para qué |
|---|---|---|
| `com.gilzoide.sqlite-net` | 1.3.2 (**desde GitHub**) | Base SQLite: partida y opciones. **Necesita git en el PATH** |
| `com.unity.render-pipelines.universal` | 17.4.0 | URP 2D: render del juego |
| `com.unity.inputsystem` | 1.19.0 | Controles: teclado y joystick, teclas reasignables |
| `com.unity.2d.tilemap.extras` | 7.0.1 | Tiles de los mapas |
| `com.unity.2d.animation`, `aseprite`, `psdimporter`, `sprite`, `spriteshape` | 14.0.3 / 4.0.1 / 13.0.2 / 1.0.0 / 14.0.1 | Herramientas 2D |
| `com.unity.ugui` | 2.0.0 | UI |
| `com.unity.nuget.newtonsoft-json` | 3.2.1 | JSON |
| `com.unity.timeline` | 1.8.12 | Animaciones por línea de tiempo |
| `com.unity.test-framework` | 1.6.0 | Pruebas |
| `com.unity.ai.assistant` | 2.17.0-pre.1 | **Solo editor**: el MCP que usan las sesiones con IA. No va a la build |

### B.6 Si Unity muestra advertencias al abrir

| Aviso | Qué hacer |
|---|---|
| *"This project was saved with a different version of the Editor"* | Se abrió con otra versión. Cerrar sin guardar e instalar **6000.4.2f1** |
| *"Enter Safe Mode?"* (errores de compilación) | Elegir **Ignore**. Casi siempre es `sqlite-net` que no se pudo bajar (ver abajo) |
| *"Unable to add package … git"* / *"No 'git' executable was found"* | Falta Git o no está en el PATH. Instalarlo, **cerrar Unity Hub y Unity** y volver a abrir |
| Errores `SQLite` / `sqlite-net` no encontrado | Verificar `git --version`, cerrar Unity y el Hub, borrar `Codigo/PokeRush/Library/PackageCache` y reabrir |
| Sprites o personajes rosados o vacíos | Falta un PNG del pack `Assets/Graphics` que nadie subió con `git add -f` (ver [Git](#git--github)); avisar a quien lo agregó |
| Avisos amarillos al importar | Normales en la primera importación; no impiden nada |
| *Unity AI Assistant* pide iniciar sesión | Es una herramienta del editor; se puede cerrar la ventana |
| La importación no termina nunca | Dejarla; si se cortó, cerrar Unity y reabrir (retoma). Último recurso: borrar `Codigo/PokeRush/Library` (se regenera) |

### B.7 Generar la build de Windows

Con el proyecto abierto: **PokeRush → Release → Generar build de Windows**. Deja
la build en `Codigo/PokeRush/Builds/PokeRush_1.4.0/PokeRush.exe`. El paso a paso,
la lista de escenas, las opciones de Player Settings y la forma por línea de
comandos están en **[BUILD.md](BUILD.md)**.

---

## Cómo ejecutar el juego

- **Escena inicial:** `Assets/Scenes/Menu Principal.unity` (es la 0 de Build Settings).
  El recorrido completo funciona solo entrando por ahí: el `GameManager` de esa
  escena arma la partida y sobrevive los cambios de escena.
- Las escenas de los niveles también se pueden abrir sueltas y darle Play:
  arrancan con una partida de prueba (`SceneBootstrapper`).

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
| Consola de administrador (si está prendida) | La tecla de la izquierda del 1 (**\|**) | — |

Todas las teclas se pueden reasignar en **Menú → Opciones**, donde también están
el volumen de música y efectos, la **resolución** y el **modo de pantalla**
(completa o ventana). Alt+Enter alterna pantalla completa.

Para probar el juego sin jugarlo entero está la **consola de administrador**: se
prende en **Opciones → Pruebas** y se abre con la tecla de la izquierda del 1.
Va a cualquier punto de la historia, da Pokémon, plata y objetos, gana peleas,
atraviesa paredes y acelera el juego. Ver `Consola_De_Administrador.md`.

---

## Estructura del proyecto

```
GrupoX_PokeRUSH_ESTFA_2026/
├── Codigo/PokeRush/              ← el proyecto de Unity (esto se abre en el Hub)
│   ├── Assets/
│   │   ├── Audio/                música por nivel, efectos, gritos y ataques
│   │   ├── Editor/               herramientas de editor (menú PokeRush: builders y pruebas)
│   │   ├── Fonts/                tipografías de pixel art
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
├── Diseño/                       mockups, material de diseño y el tráiler
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
| `Consola/` | La consola de administrador (comandos y botones para probar) |
| `Testing/` | Bancos de prueba en Play (no se usan en el juego) |

---

## Tecnologías

- **Unity 6000.4.2f1** con **URP 2D** (Universal Render Pipeline 17.4)
- **C#** (.NET Standard 2.1, scripting backend Mono)
- **SQLite** para el guardado y la configuración (`sqlite-net`)
- **Input System** 1.19 (teclado y joystick, teclas reasignables)
- **uGUI** (UI) y **Tilemap** (mapas)

| | |
|---|---|
| Juego | **1.4.0** |
| Unity | 6000.4.2f1 |
| URP | 17.4.0 |
| Input System | 1.19.0 |
| sqlite-net | 1.3.2 |

Recursos de terceros (arte, música y sonido): ver `Documentacion/Creditos_De_Assets.md`
y los créditos del juego. Es material de fan game de uso académico, sin fines de lucro.

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

  Lo que el juego usa de esos packs **sí** está versionado, así que un clon limpio
  funciona sin ellos. Si se empieza a usar un PNG nuevo del pack de gráficos hay
  que subirlo a mano:
  `git add -f "Codigo/PokeRush/Assets/Graphics/.../archivo.png" "....png.meta"`.
  Del pack de audio se copia con `python scripts/copiar_audio_essentials.py`.
- No se versionan `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` ni los
  `.csproj`/`.sln` (Unity los regenera). Por eso la build y el release **no** están
  en el repo: el `.zip` vive solo en la sección *Releases* de GitHub.

---

## Especies y movimientos nuevos

El juego no carga las 445 especies al arrancar: `DefinitionRepository` busca cada
una en `Assets/Resources/Definitions/IndiceDeDefiniciones.asset` y la carga recién
cuando hace falta. Así la build usa ~550 MB de RAM en vez de reservar más de 5 GB.

Ese índice **se rearma solo antes de cada build**. Si se agrega una especie o un
movimiento y se prueba en el editor sin rearmarlo, no se rompe nada: la consola
avisa `[Definiciones] ... no esta en el indice` y se cargan todas como antes. Para
sacar el aviso: **PokeRush → Release → Armar indice de definiciones**.

---

## Documentación

`Documentacion/Documentacion del Codigo/` tiene un `.md` por sistema: combate e IA,
menús, HUD, casino, Quarcho Cueva, historia en los niveles, tutorial, jefe final,
**audio** (`Audio_Del_Juego.md`) y cómo armar mapas, enemigos y diálogos.

Qué trajo cada versión:

- **1.1.0** (playtest del 4-oct): `Playtest_4_Oct_2026.md`.
- **1.2.0** (playtest del 5-oct: Entrenamiento intenso del chip, aviso del rival,
  aprender movimientos al subir de nivel, fondos de combate, tienda del piso 3):
  `Playtest_5_Oct_2026.md`.
- **1.3.0**: rebalanceo de Pokémon y dificultad (`Rebalanceo_Oct_2026.md`) y la
  consola de administrador (`Consola_De_Administrador.md`).

Devolución docente y datos del grupo (roles, color, plataforma de organización):
`Documentacion/Devolucion_Docente.md`.
