# Cómo hacer diálogos y decisiones

Guía para escribir diálogos y preguntas de sí/no. **Los scripts ya están hechos**: casi todo se resuelve poniendo un componente y llenando campos en el Inspector.

La tecla de acción es la **X** (Z y Enter quedan como alias, y el botón sur del gamepad).

---

## Las tres piezas

| Pieza | Qué es | Dónde |
|---|---|---|
| `DialogueView` | la caja de texto: nombre, retrato, línea y las dos opciones | en `CajaDialogo`, dentro de `DialogoCanvas` |
| `DialogueController` | quién decide qué se muestra y guarda la respuesta | en la raíz de `DialogoCanvas` |
| `PlayerInputController` | **el único que lee teclado** | en el Player |

`PlayerController.Interact()` mira **primero el tile de adelante** y, si ahí no hay nada, prueba los otros tres. Así se puede hablarle a un NPC de costado o de espaldas, que es lo que uno espera cuando el NPC ocupa un tile y hay que rodearlo. Si consume la interacción, el jugador además gira hacia él.

Mientras hay un diálogo abierto, `PlayerInputController` le manda todo el input a la caja y **el jugador no se mueve**. Por eso no hay que leer teclas desde otro componente: si dos escuchan la X, la misma tecla que cierra la línea vuelve a abrir el diálogo.

Para tener diálogos en una escena alcanza con copiar el objeto **`DialogoCanvas`** de `NestorGOD2`.

Teclas dentro de un diálogo:

- **X** avanza la línea, o confirma la opción marcada.
- **Flechas / stick** cambian de opción.
- **Escape** cancela, que equivale a elegir la **segunda** opción.

---

## Desde código: las dos llamadas que importan

No hace falta crear assets para un texto suelto.

### Mostrar líneas

```csharp
dialogueController.StartMessage("Nestor", retrato,
    new[] { "Primera linea.", "Segunda linea." },
    () => Debug.Log("el jugador cerro el cartel"));
```

El nombre vacío (`""`) muestra la caja **sin** cartelito de hablante, que es lo que conviene para mensajes del sistema ("Partida guardada").

### Preguntar sí/no

```csharp
dialogueController.StartConfirm("Nestor", retrato,
    "Queres pelear conmigo?",
    "Pelear", "Todavia no",
    acepto =>
    {
        if (acepto) ArrancarPelea();
        else NoPasaNada();
    });
```

El callback recibe `true` si eligió la **primera** opción. La caja se cierra **antes** de llamarlo, así que desde el callback se puede abrir otro diálogo sin que se pise.

> **Trampa clásica**: si el objeto se apaga (`SetActive(false)`) antes de que el jugador cierre el cartel, muere el callback y la caja queda abierta para siempre. Apagalo **dentro** del callback del último mensaje. Está resuelto así en `LiberarPokenTrigger.Liberar()`.

### Con un asset de diálogo

Para textos largos o de guión conviene un `DialogueDefinition` (menú `Create > PokeRush > DialogueDefinition`): tiene hablante, retrato, líneas y un flag que se activa al terminar.

```csharp
dialogueController.StartDialogue(miDialogo, story, alTerminar);
```

---

## Recetas

### 1. Un NPC que pregunta antes de pelear

`InteractableTrigger` en el NPC:

| Campo | Valor |
|---|---|
| `combate` | el `EnemyDefinition` o `BossDefinition` (Néstor: `Resources/Definitions/Torre1/Nestor.asset`) |
| `preguntaAntes` | "Asi que llegaste hasta aca.\nQueres pelear conmigo?" |
| `opcionSi` / `opcionNo` | "Pelear" / "Todavia no" |
| `mensajeSiRechaza` | "Volve cuando estes listo." |

**`preguntaAntes` vacío = se ejecuta de una**, como siempre. Por eso los subsistemas de la torre no cambiaron.

### 2. Un NPC que te da un Pokémon

`LiberarPokenTrigger`: pregunta y, si aceptan, crea el `PokenModel` y lo suma al equipo (tope 6).

- `poken` + `nivel`: qué especie se suma.
- Se dispara de dos formas: apretando **X** de frente, y —si `tambienPorTrigger` está activo— **caminándole al lado** (`radioDeteccion`, 1.2 = el tile de al lado).
- Ejemplos ya armados: `NPC_Combusken.prefab` y `NPC_Basculin.prefab`.

> La cercanía se mide **por distancia**, no con un Collider2D en trigger. Con un trigger no anda: el movimiento del jugador decide si puede avanzar con un `OverlapCircle` contra su `obstacleMask`, y esa consulta **también engancha los triggers de esa capa**. Un trigger en `Obstacles` bloquea todos los tiles alrededor del NPC y no se puede ni llegar.

### 3. Un botón que abre una barrera

Dos componentes que **no se conocen entre sí**: se hablan por un `StoryFlag`.

**El botón** — `InteractableTrigger`:

| Campo | Valor |
|---|---|
| `otorgaFlag` | `Torre1_EnergiaAuxiliarCortada` |
| `preguntaAntes` | "El panel controla los rayos.\nCortas la energia?" |
| `mensajeExito` | "Los rayos de contencion se apagan." |
| `mensajeYaHecho` | "Los rayos ya estan apagados." |

**La barrera** — `BarreraPorFlag` en el objeto que bloquea (con su sprite y un Collider2D en capa `Obstacles`):

| Campo | Valor |
|---|---|
| `flagQueLaApaga` | el mismo flag |
| `alReves` | si está activo, aparece en vez de desaparecer |
| `objetos` | vacío = apaga sus propios renderers y colliders; con objetos, los apaga a ellos |

Se apaga de dos formas: **al vuelo** cuando el jugador toca el botón (escucha `StoryModel.FlagActivated`), y **al cargar la escena** si el flag ya venía del guardado.

Ejemplo armado en `NestorGOD2`: `BotonRayos` en el rack de la sala de servidores (27.5, 18.5) y `RayosBarrera` cortando el pasillo que sube al norte, con Combusken adentro (27.5, 23.5).

> **El botón tiene que estar del lado por donde entra el jugador.** Suena obvio y es el error más fácil de cometer: un tile puede estar libre y aun así ser inalcanzable. Verificalo con un flood fill desde el spawn.

> `BarreraPorFlag` **no apaga su propio GameObject**, apaga renderers y colliders. Si se apagara a sí misma y la escena se guardara así, `Update` no volvería a correr y el paso quedaría abierto para siempre sin tocar el botón.

> Los rayos eran **tiles** del tilemap `Colisiones`. Para poder apagarlos se borraron esos tiles y se reemplazaron por un GameObject con los mismos sprites: apagar un GameObject es un `SetActive`, mientras que apagar tiles obliga a operar el tilemap en runtime. **Después de borrar tiles hay que correr `PokeRush > Reparar colisiones del mapa`** (ver `Como_Armar_Un_Mapa_Nuevo.md`, punto 2).

### 3b. Marcar un interactuable con un brillito

Los paneles de subsistema **no tienen sprite propio**: son colliders invisibles puestos encima de los tiles del mapa. Sin una marca no hay forma de saber que ahí se puede apretar X.

**`BrilloInteractuable`**, en el mismo objeto que el `InteractableTrigger`. No hay nada que configurar: **toma el flag del propio trigger**, así que no se puede desincronizar. Se apaga solo al cortar ese subsistema, con lo cual el brillo funciona además como checklist de lo que falta.

| Campo | Para qué |
|---|---|
| `flagQueLoApaga` | `None` = lo lee del `InteractableTrigger` del objeto |
| `tamanioHalo` / `tamanioEstrella` | **alto en tiles** (1.15 y 0.4). `tamanioEstrella` en 0 = sin estrella |
| `escalaGeneral` | multiplica a los dos, para agrandar todo sin tocarlos por separado |
| `spriteHalo` / `spriteEstrella` | `ballBurst_particle` y `ballBurst_star` de `Sprites/UI/Battle` |
| `sortingOrder` | 12: encima de `Colisiones` (10), debajo del jugador (15) |
| `periodo`, `amplitud`, `girosPorMinuto` | el ritmo del latido |
| `verEnElEditor` | mostrarlo fuera de Play para poder calibrarlo |

**El tamaño se ajusta a ojo, sin entrar a Play.** El componente es `[ExecuteAlways]`, así que el brillo se ve en la Scene view, y `OnValidate` re-aplica el tamaño apenas movés el valor en el Inspector. Ojo que el alto que ves oscila ±`amplitud` (18% por defecto) alrededor del número pedido, porque está latiendo.

El visual se arma **en runtime** como dos hijos con `HideFlags.HideAndDontSave`: no ensucian la jerarquía ni se guardan en el `.unity`, y agregar un panel nuevo es poner un solo componente.

> Cada panel late **desfasado** de los demás. El desfase sale del nombre del objeto, no de un random: se ve orgánico pero es igual en todas las partidas, así que un bug visual siempre se reproduce.

> **Dos trampas que ya mordieron acá**, por si hacés otro componente parecido:
> - Los objetos con `HideFlags.DontSave` **no se destruyen al entrar a Play**. Los que arma el editor se cuelan en la partida y el componente ya no los conoce, así que no los puede apagar. Por eso `BrilloInteractuable` borra en `OnEnable` **todo** hijo que se llame `Brillo*`, no solo los que tiene referenciados.
> - No conviene escuchar `StoryModel.FlagActivated` para esto: `NewGame` crea un `StoryModel` nuevo, y quien se suscribió antes no se entera nunca más. El brillo **consulta el flag por frame** (es un `HashSet.Contains`), lo que además cubre gratis el caso de cargar una partida que ya tenía el subsistema cortado.

Puesto en `NestorGOD2` sobre los seis que debilitan a Néstor: `Camaras`, `nodoA/B/C`, `Rastreo` y `BotonRayos`. Las Pokéballs y Néstor **no** lo llevan, a propósito.

### 4. Un objeto en el piso (pokeball)

Mismo `InteractableTrigger`, con el campo **`entrega`**: una lista de objeto + cantidad que va a la mochila.

| Campo | Valor |
|---|---|
| `entrega` | `Pocion` x3 (o lo que sea) |
| `otorgaFlag` | uno propio, ej. `Torre1_PocionesTomadas` |
| `mensajeExito` | `"Encontraste {0}!"` — el `{0}` se reemplaza por lo que dio |
| `mensajeYaHecho` | "Ya no queda nada aca." |
| `desactivarAlUsar` | **false** |

Para que desaparezca se le suma un **`BarreraPorFlag`** con el mismo flag, en vez de usar `desactivarAlUsar`: así se esconde igual al agarrarlo (por el evento del flag) que al recargar una partida donde ya lo habías agarrado, por el mismo camino. Con `desactivarAlUsar` el objeto se apaga y su `Update` deja de correr, y hay que resolver la recarga aparte.

Ejemplos: `PokeballPociones` y `PokeballSuperPociones` en la sala de Néstor.

### 4b. Un menú con más de dos opciones

Para los NPCs que tienen varias cosas para ofrecer (los de la Quarcho Cueva: comprar, viajar, charlar...):

```csharp
dialogueController.StartChoice("TIXI", retrato, "¿Qué necesitás?",
    new[] { "Comprar", "Viajar", "Charlar", "Nada" },
    eleccion =>
    {
        if (eleccion == 0) AbrirTienda();
        else if (eleccion == 1) Viajar();
        // ...
    });
```

- El callback recibe el **índice** elegido. **Escape elige la última**, así que conviene que la última sea la de "no hacer nada".
- Con dos opciones se ve igual que `StartConfirm` (de hecho `StartConfirm` ahora es un `StartChoice` de dos).
- Con más, la ventana de opciones va **arriba de la caja**, a la derecha, con el ancho de la opción más larga. Si no entran todas, la lista se corre con la selección y `^`/`v` avisan que hay más.
- No hay que tocar el `DialogoCanvas`: los renglones que faltan se clonan del último.

### 5. Un mensaje del sistema

```csharp
dialogueController.StartMessage("", null, new[] { "Partida guardada." });
```

Es lo que hace `GuardadoRapido` al apretar F2.

---

## Cómo agregar un flag nuevo

Los flags viven en `Assets/Scripts/Core/StoryFlag.cs`. **Agregar miembros es seguro**: el guardado los persiste como texto (`flag.ToString()`) y los relee con `Enum.TryParse`, así que no hay que tocar la base de datos.

Lo que **no** hay que hacer es renombrar ni reordenar los que ya existen: los guardados viejos quedan con el texto anterior y se pierden en silencio.

---

## Checklist

1. La escena tiene un **`DialogoCanvas`** (copiado de `NestorGOD2`).
2. El `interactableMask` del `PlayerController` incluye la capa de los NPC (`Obstacles`).
3. Apretar **X** de frente abre la caja y el jugador **no se mueve** mientras está abierta.
4. Las flechas cambian de opción y se ve cuál está marcada.
5. Elegir "no" **no** ejecuta la acción.
6. Elegir "sí" la ejecuta y el cartel de resultado se cierra bien.
7. Si el objeto se apaga al usarlo, se apaga **después** de cerrar el cartel.
