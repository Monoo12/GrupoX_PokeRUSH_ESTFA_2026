# Cómo generar la build de PokeRush

Guía paso a paso para alguien que nunca abrió el proyecto. El resultado es la
build de producción para **Windows 10/11 de 64 bits**: una carpeta con
`PokeRush.exe` lista para entregar.

---

## 1. Requisitos

- Una PC con **Windows 10 u 11 de 64 bits**, ~20 GB libres (proyecto + `Library/` + build).
- **Git** 2.40 o más nuevo, instalado y **en el PATH** (`git --version` en una
  consola tiene que responder). Unity lo usa para bajar el paquete de SQLite.
- **Unity Hub** 3.x o más nuevo: <https://unity.com/download>.
- Acceso al repositorio privado `Monoo12/GrupoX_PokeRUSH_ESTFA_2026`.

## 2. Versión exacta de Unity

**Unity 6000.4.2f1** (Unity 6.4). Es la que figura en
`Codigo/PokeRush/ProjectSettings/ProjectVersion.txt`.

En Unity Hub: *Installs → Install Editor → Archive → download archive* y buscar
**6000.4.2f1**. No usar otra versión (ni más nueva): Unity "actualizaría" el
proyecto y cambiaría archivos versionados.

## 3. Módulos necesarios

- **Windows Build Support (Mono)**: viene incluido con el editor de Windows. Es el
  único necesario.
- No hacen falta *Windows Build Support (IL2CPP)*, Mac, Linux, Android ni WebGL.
  La build usa **Mono** a propósito (ver punto 10).

Para revisarlo: Unity Hub → *Installs* → el engranaje de 6000.4.2f1 → *Add modules*.

## 4. Clonar el repositorio

```
git clone https://github.com/Monoo12/GrupoX_PokeRUSH_ESTFA_2026.git
cd GrupoX_PokeRUSH_ESTFA_2026
```

El proyecto de Unity está en la subcarpeta **`Codigo/PokeRush`**.

## 5. Abrir el proyecto

1. Unity Hub → *Projects → Add → Add project from disk*.
2. Elegir la carpeta `GrupoX_PokeRUSH_ESTFA_2026/Codigo/PokeRush`.
3. Verificar que la columna *Editor Version* diga **6000.4.2f1** y abrirlo.
4. **La primera vez tarda** (15–40 minutos): Unity importa ~25.000 archivos y baja
   los paquetes. Dejarlo terminar; la barra de progreso de abajo a la derecha
   tiene que desaparecer.
5. Cuando termina aparece el menú **PokeRush** en la barra de arriba.

## 6. Si Unity muestra advertencias al abrir

| Aviso | Qué hacer |
|---|---|
| *"This project was saved with a different version of the Editor"* | Se abrió con otra versión. Cerrar sin guardar e instalar **6000.4.2f1**. |
| *"Enter Safe Mode?"* (errores de compilación) | Elegir **Ignore**. Casi siempre es el paquete `sqlite-net` que no se pudo bajar: ver "Problemas comunes". |
| *"Unable to add package ... git"* / *"No 'git' executable was found"* | Falta Git o no está en el PATH. Instalarlo, **cerrar Unity Hub y Unity** y volver a abrir. |
| Avisos amarillos en la consola al importar | Son normales en la primera importación; no impiden la build. |
| *Unity AI Assistant* pide iniciar sesión | Es una herramienta del editor (no va a la build). Se puede cerrar la ventana. |

## 7. Configurar la plataforma

1. *File → Build Profiles*.
2. Elegir **Windows** (Windows, Mac, Linux → Windows). Si no está activo,
   *Switch Platform*.
3. *Architecture*: **x86_64** (Intel 64-bit).

## 8. Abrir Build Settings / Build Profiles

*File → Build Profiles* (en Unity 6 reemplaza al viejo *Build Settings*). La lista
**Scene List** muestra las escenas que van a la build.

## 9. Escenas que deben estar incluidas (en este orden)

| # | Escena | Qué es |
|---|---|---|
| 0 | `Assets/Scenes/Menu Principal.unity` | Pantalla de título (**tiene que ser la 0**) |
| 1 | `Assets/Scenes/MenuSaves.unity` | Partidas guardadas |
| 2 | `Assets/Scenes/CreacionPersonaje.unity` | Creación de personaje |
| 3 | `Assets/Scenes/habitacion.unity` | Tutorial (donde despierta el personaje) |
| 4 | `Assets/Scenes/Quarcho-Cueva.unity` | El lobby entre niveles |
| 5 | `Assets/Scenes/NestorGOD2.unity` | Nivel 1: Data Center de Néstor |
| 6 | `Assets/Scenes/Casino Nivel 1.unity` | Nivel 2: casino, piso 1 |
| 7 | `Assets/Scenes/Casino Nivel 2.unity` | Casino, piso 2 |
| 8 | `Assets/Scenes/Casino nivel 3.unity` | Casino, piso 3 |
| 9 | `Assets/Scenes/Casino piso FINAL.unity` | Casino, piso final (jefe Reymar) |
| 10 | `Assets/Scenes/BossFinal.unity` | Arena de Julieta (la de Santiago, con la pelea) |
| 11 | `Assets/Scenes/CreditosFinales.unity` | Créditos |

**No hace falta cargarlas a mano**: el menú **PokeRush → Release → Aplicar
configuracion de release** deja exactamente esta lista (las que existen) y saca
las escenas de prueba.

## 10. Configuraciones importantes

Todo esto lo aplica **PokeRush → Release → Aplicar configuracion de release**
(`Assets/Editor/Release/PreparacionDeRelease.cs`). Para revisarlo a mano:
*Edit → Project Settings → Player*.

| Opción | Valor | Por qué |
|---|---|---|
| Company Name | `LevelUP Creators` | Define la carpeta de la partida: `AppData/LocalLow/LevelUP Creators/PokeRush` |
| Product Name | `PokeRush` | Nombre de la ventana y del .exe |
| Version | `1.4.0` | |
| Icono | `Assets/Sprites/Logos/IconoDelJuego.png` | Logo de Level Up Creators |
| Splash | Logo de Level Up Creators sobre blanco, sin el de Unity | |
| Fullscreen Mode | Fullscreen Window, resolución nativa | En el juego: Opciones → Pantalla |
| Resizable Window / Fullscreen switch (Alt+Enter) | Sí | |
| Force Single Instance | Sí | Dos copias abiertas escribirían la misma partida |
| Scripting Backend | **Mono** | Es el módulo que viene con el editor; IL2CPP pide otro módulo y Visual Studio con C++ |
| API Compatibility Level | .NET Standard 2.1 | |
| Managed Stripping Level | Disabled | La base de datos (`sqlite-net`) usa reflexión |
| Stack Trace (Log / Warning) | None | Logs livianos en producción |
| Stack Trace (Error / Exception) | Script Only | Para poder diagnosticar un reporte |
| Development Build | **Desactivado** | Build de producción |
| Compresión de la build | **LZ4HC** | Sin pérdida. Sin esto la build pesa ~3,6 GB (las tiras animadas de los Pokémon van sin comprimir para no alterar el pixel art) |
| Índice de definiciones | Se rearma solo antes de cada build | El juego carga cada especie recién cuando la necesita: ~550 MB de RAM en vez de más de 5 GB (ver README → "Especies y movimientos nuevos") |
| Calidad en Windows | Ultra (con VSync) | `Project Settings → Quality` |

## 11. Generar la build

**Opción A — desde el editor (recomendada):**

*PokeRush → Release → Generar build de Windows*. Aplica la configuración del
punto 10 y genera la build. Tarda unos minutos; al terminar, la consola dice
`[Release] Build Succeeded ...`.

**Opción B — a mano:** *File → Build Profiles → Windows → Build*, y elegir como
carpeta `Codigo/PokeRush/Builds/PokeRush_1.4.0`. (Correr antes *Aplicar
configuracion de release*.)

**Opción C — por línea de comandos** (con Unity **cerrado** en ese proyecto):

```
"C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe" -batchmode -quit -projectPath "Codigo/PokeRush" -executeMethod PreparacionDeRelease.BuildWindows -logFile build.log
```

Termina con código 0 si salió bien y 1 si falló; el detalle queda en `build.log`.

## 12. Dónde queda la build

```
Codigo/PokeRush/Builds/PokeRush_1.4.0/
├── PokeRush.exe          ← el juego
├── PokeRush_Data/        ← los datos (tiene que viajar junto al .exe)
├── MonoBleedingEdge/     ← el runtime de Mono
├── D3D12/, dstorage*.dll, UnityCrashHandler64.exe
└── UnityPlayer.dll
```

La carpeta `PokeRush_BurstDebugInformation_DoNotShip` que deja Unity la borra
la herramienta al terminar (no se entrega). Si se genera a mano (opción B),
borrarla antes de comprimir.

Para entregarla se comprime **la carpeta entera** `PokeRush_1.4.0` en un .zip.
El .exe solo no anda. La carpeta `Builds/` está en el `.gitignore`: la build no
se sube al repo.

## 13. Problemas comunes y soluciones

| Problema | Solución |
|---|---|
| **Errores `SQLite` / `sqlite-net` no encontrado** al abrir | El paquete se baja de GitHub con git. Instalar Git, verificar `git --version`, cerrar Unity Hub y Unity, borrar `Codigo/PokeRush/Library/PackageCache` y volver a abrir. |
| **Sprites o personajes invisibles** (rosados o vacíos) | Falta un PNG del pack `Assets/Graphics` que nadie subió con `git add -f`. Ver la regla en el README (sección Git) y avisar a quien lo agregó. |
| La build dice *"Scene ... couldn't be loaded"* o la salida de la cueva dice que el jefe no está | La escena no está en la lista. Correr *Aplicar configuracion de release*. |
| *"Build completed with a result of 'Failed'"* sin más datos | Mirar la consola de Unity (o `build.log` en la opción C) y buscar la primera línea con `error`. Casi siempre es un error de compilación de un script que se editó. |
| El batchmode termina enseguida con código 1 y un log cortado | El proyecto está abierto en el editor (Unity no deja abrirlo dos veces). Cerrar el editor. |
| El juego arranca y **no suena nada** | Falta `Assets/Resources/Audio/CatalogoDeAudio.asset`: correr *PokeRush → Audio → Armar todo el audio*. |
| La consola avisa `[Definiciones] ... no esta en el indice` | Se agregó una especie o un movimiento después de armar el índice. No rompe nada (se cargan todos), pero gasta memoria: correr *PokeRush → Release → Armar indice de definiciones*. La build lo hace sola. |
| La partida "desapareció" | Se guarda en `AppData/LocalLow/LevelUP Creators/PokeRush/pokerush.db`. Hasta el 2-oct-2026 se guardaba en `.../DefaultCompany/PokeRush/`: la primera vez que se abre, el juego trae sola esa partida a la carpeta nueva (`DatabaseService`). Si la carpeta nueva ya tenía una base, no la pisa: copiar a mano si hace falta. |
| Windows SmartScreen avisa al abrir el .exe | Es una build sin firma digital: *Más información → Ejecutar de todas formas*. |
| La primera importación no termina nunca | Dejarla; si se cortó, cerrar Unity y volver a abrir (retoma). Como último recurso, borrar `Codigo/PokeRush/Library` (se regenera; tarda lo mismo que la primera vez). |
