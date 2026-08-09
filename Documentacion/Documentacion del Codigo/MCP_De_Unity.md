# 🔌 MCP for Unity — cómo dejarlo andando y cómo se usa

Este documento junta lo aprendido usando **MCP for Unity** (`CoplayDev/unity-mcp`) sobre PokeRush.
Sirve para que un asistente de IA lea el estado real del editor, corra código dentro de Unity y
saque capturas, en vez de trabajar a ciegas sobre el código fuente.

Está acá porque el conocimiento costó varias sesiones y no es obvio: casi todos los problemas dan
síntomas que **no** apuntan a su causa.

---

## 🧩 Las tres piezas

Son independientes y cada una falla distinto. Registrar el cliente **no** levanta el servidor ni fija
el transporte.

| pieza | qué es | dónde |
|---|---|---|
| **Servidor MCP** | proceso que habla el protocolo | `http://127.0.0.1:8080/mcp`, lo levanta `uvx` |
| **Bridge de Unity** | socket que sirve el editor abierto | puerto `6400` |
| **Registro del cliente** | entrada en `~/.claude.json` | bajo el proyecto, no por worktree |

Paquete: `com.coplaydev.unity-mcp` en el `manifest.json`.

---

## ⚠️ Las trampas, en orden de cuánto tiempo cuestan

### 1. El transporte tiene que coincidir de los dos lados

Es la que más cuesta. Si la ventana de Unity está en `Transport: Stdio` y el registro del cliente
dice `http`, quedan **los dos puertos escuchando, todo en verde**, y `mcpforunity://instances`
devuelve `instance_count: 0`. El editor publica su bridge por un canal y el cliente escucha por otro.

**Arreglo:** ventana *MCP for Unity* → pestaña **Connect** → **Server** → `Transport` = `HTTP`.

**Orden para diagnosticar:** ¿`instance_count` > 0? → ¿hay algo escuchando en 8080 y 6400? →
¿`claude mcp list` dice *Connected*? → ¿las herramientas están cargadas en la sesión?

### 2. Las herramientas solo se cargan al arrancar la sesión

Instalar o conectar el MCP a mitad de una conversación **no** trae las herramientas. Hay que
reiniciar la sesión y retomarla.

### 3. El MCP se conecta al proyecto ABIERTO EN EL EDITOR

No al directorio de trabajo del asistente. Para trabajar sobre un worktree hay que tener Unity
abierto **en ese worktree** y **cerrar las demás instancias** de Unity: el bridge sirve una sola y no
se sabe cuál. Verificar siempre `mcpforunity://project/info` y mirar que `projectRoot` sea el
esperado.

### 4. Batch mode y MCP se pelean por el lockfile

El lock de Unity es **por path de proyecto**. Si el editor está abierto en el worktree —que es lo que
sostiene el MCP— entonces `Unity.exe -batchmode` sobre ese mismo path **falla**, y el síntoma engaña:
el log corta después de `Successfully changed project path` y termina con `return code 1`, sin
ningún mensaje de error. Chequear `Codigo/PokeRush/Temp/UnityLockfile` antes de correr batch.

Con Unity abierto, la alternativa para verificar que compila es el compilador de Roslyn que trae el
propio editor, armando la lista de referencias desde los `.csproj` que Unity ya generó:

```bash
"/c/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Data/NetCoreRuntime/dotnet.exe" \
  "/c/Program Files/Unity/Hub/Editor/6000.4.2f1/Editor/Data/DotNetSdkRoslyn/csc.dll" "@respuesta.rsp"
```

### 5. No te asustes con los `Unity.exe` de más

Un solo editor abierto muestra **tres** procesos: el editor (~1,4 GB) y dos AssetImportWorkers
(~155 MB).

---

## 🛠️ Cómo se usa, en la práctica

### Leer estado vs. modificar

- **Recursos** (`mcpforunity://...`) para *leer*: estado del editor, info del proyecto, instancias.
- **Herramientas** para *actuar*: `execute_menu_item`, `manage_scene`, `manage_gameobject`.
- Después de tocar scripts, `refresh_unity` y **siempre** `read_console` para ver si compiló.

### `execute_code` es la navaja suiza

Corre C# arbitrario dentro del editor y devuelve lo que uno haga `return`. Es la forma más directa de
contestar una pregunta sobre el estado real. Dos detalles del compilador:

- Por defecto usa **CodeDom, que es C# 6**: nada de `switch` de expresión, interpolación con `$` sí,
  pero ojo con lo moderno.
- **No se puede repetir un nombre de variable** entre un scope hijo y el padre, aunque en C# moderno
  sea legal.
- Los miembros hay que buscarlos donde están de verdad: `useAutoRandomSeed` es de `ParticleSystem`,
  no de `main`.

### Sacar capturas

`manage_camera` con `action: "screenshot"`:

- `capture_source: "scene_view"` captura la vista de escena.
- `capture_source: "game_view"` captura la cámara del juego.
- `include_image: true` la devuelve inline.

**Límite conocido:** en **modo edición** los efectos de *Cartoon FX Remaster* no se dibujan en la
vista de escena (los de *Hovl* sí). En Play aparecen. Si un efecto de CFXR "no se ve" en una prueba
de modo edición, **no** concluir que está roto.

---

## 🎯 Receta: verificar algo que dura 0,3 segundos

El problema real al trabajar con efectos: duran menos que el viaje de ida y vuelta de una llamada MCP,
así que perseguirlos con capturas no funciona. Dos caminos, y el primero es mucho mejor.

### Camino bueno — congelar en modo edición

`ParticleSystem.Simulate()` deja el efecto detenido en el instante que uno elija. Es **determinista y
repetible**, y no hace falta entrar en Play:

```csharp
var inst = Instantiate(prefab, posicion, rotacion);
foreach (var s in inst.GetComponentsInChildren<ParticleSystem>(true))
{
    s.useAutoRandomSeed = false;              // sin esto cada corrida da distinto
    s.Simulate(0.22f, true, true, false);     // congelado a los 0,22 s
}
```

Sirve además para **medir**: recorriendo varios instantes y quedándose con el `bounds` más grande de
los renderers se obtiene cuánto ocupa el efecto de verdad. Es la única medida confiable — estimarlo
de los parámetros del sistema de partículas da mal por un factor de 2 o 3.

### Camino incómodo — modo Play

Dos obstáculos, los dos con solución:

1. **El bucle de juego no avanza si Unity está en segundo plano.** Poner
   `Application.runInBackground = true`.
2. **El bridge vuelve a pausar el editor entre llamadas.** Por eso no se puede "esperar" con llamadas
   sucesivas. Lo que sí funciona es dejar un vigía que frene solo cuando se cumpla una condición:

```csharp
UnityEditor.EditorApplication.CallbackFunction vigia = null;
vigia = () => {
    if (/* la condicion que te interesa */) {
        UnityEditor.EditorApplication.isPaused = true;
        UnityEditor.EditorApplication.update -= vigia;
    }
};
UnityEditor.EditorApplication.update += vigia;
UnityEditor.EditorApplication.isPaused = false;
```

Bajar `Time.timeScale` ayuda, pero **ojo**: `Time.time` es tiempo escalado, así que una espera de
0,33 s con `timeScale = 0.04` tarda 8 segundos reales. Es fácil creer que algo no pasó cuando en
realidad todavía no llegó.

---

## 📌 Por qué importa

Sin esto, un asistente afirma "arreglado" sobre cosas que nunca vio, y el resultado contradice el
reporte una y otra vez. Con `Simulate()` + capturas, las afirmaciones visuales pasan a ser
verificables. Ejemplo real: un efecto que "se veía como una bola a distancia" resultó ser un impacto
escalado a **0,62 unidades** por una estimación de tamaño equivocada — algo que se detecta midiendo
en dos minutos y es invisible leyendo el código.
