# Cómo armar la torre 1 (Ciberseguridad)

Guía para quien diseñe el recorrido. **Los scripts ya están hechos**: acá se explica cómo usarlos desde el Inspector, sin tocar código.

La regla madre del GDD: **todo lo que apagues en la torre se lo sacás a Néstor.** Cada subsistema cortado cambia el combate final.

---

## Lo que hay disponible

| Script | Dónde va | Para qué |
|---|---|---|
| `SceneBootstrapper` | un GameObject suelto de la escena | conecta el jugador guardado con la escena. **Sin esto no te podés mover.** |
| `InteractableTrigger` | paneles, guardias, generadores | corta un subsistema (con o sin combate, con o sin costo) |
| `DoorTrigger` | pasajes entre salas | teletransporta al jugador |
| `SubsystemFeedbackView` | un panel del Canvas | el cartel que confirma el corte |

---

## 1. Armado mínimo de la escena

Una sola escena con todas las salas sobre el mismo `Grid`, a distintos offsets (la sala 2 a 100 unidades de la 1, etc.). **No usar una escena por sala**: obligaría a re-inicializar todo y a guardar la posición del jugador, cosa que el guardado todavía no hace.

Objetos necesarios:

1. **`Grid`** con las capas de tilemap. Está el menú `PokeRush > Armar escena de mapa` que arma Suelo / Decorado / Colisiones / Techos ya ordenados. Lo que pintes en **Colisiones** bloquea al jugador solo.
2. **Jugador**: un GameObject con `PlayerController`, `PlayerInputController`, `Rigidbody2D` (Body Type: **Kinematic**) y un `Collider2D`.
   - El `Rigidbody2D` es obligatorio: sin él Unity no dispara los triggers de las puertas.
   - En `PlayerController` hay que setear **`obstacleMask`** (capas que frenan) e **`interactableMask`** (capas donde buscar cosas para interactuar).
3. **`SceneBootstrapper`**, con las referencias al `PlayerController`, `MapController` y `EventController` de la escena.
4. **`SubsystemFeedbackView`** en un panel del Canvas, con sus dos `Text`.
5. **`BattleController`** + HUD si la torre tiene combates (copiar de `CombatePrueba.unity`).

> **Para probar sin pasar por el menú**: `SceneBootstrapper` tiene `crearPartidaSiFalta` prendido. Apretás Play en la escena de la torre y te crea una partida de prueba con el equipo inicial. No hace falta arrancar desde el menú cada vez.

---

## 2. Los cuatro subsistemas

Los cuatro usan **el mismo componente** (`InteractableTrigger`); lo que cambia son los datos. Cada uno se corta de una forma distinta a propósito: si todos fueran combates, alcanzaría con un menú en vez de un overworld.

### Cámaras — combate contra un guardia

En la sala de monitores, **puerta lateral** desde Control de acceso (opcional: el que la ignora lo paga en el boss).

| Campo | Valor |
|---|---|
| `titulo` | `CÁMARAS` |
| `combate` | `Definitions/Torre1/GuardiaCamaras` |
| `otorgaFlag` | **dejar en `None`** |
| `mensajeExito` | `Cámaras caídas. Vas a ver venir la predicción de Néstor.` |

⚠️ **El flag no se pone acá.** Los cortes por combate lo otorgan al ganar, leyendo el `defeatGrantsFlag` del asset del enemigo. Está configurado en `GuardiaCamaras.asset` como `Torre1_CamarasCortadas`. Si lo ponés en los dos lados, se activa antes de pelear.

### Firewall — tres nodos, en salas distintas

El único **parcial**, a propósito: le enseña al jugador que esto no es binario. Cada nodo achica el escudo del núcleo; los tres completos son condición para que no aparezca Melmetal.

Tres `InteractableTrigger`, uno por nodo, cada uno con su `EnemyDefinition` y su flag (`Torre1_FirewallNodoA` / `B` / `C`).

| Campo | Nodo A | Nodo B | Nodo C |
|---|---|---|---|
| `titulo` | `FIREWALL` | `FIREWALL` | `FIREWALL` |
| `combate` | `NodoFirewall` | `NodoFirewall` | `NodoFirewall` |
| `requiereFlag` | `None` | `None` | **`Torre1_CamarasCortadas`** |
| `mensajeExito` | `Nodo 1 de 3 caído. Escudo del núcleo reducido.` | `Nodo 2 de 3...` | `Nodo 3 de 3...` |

El **nodo C detrás de las cámaras** enseña que los sistemas se relacionan entre sí, y sigue sin bloquear nada obligatorio.

### Rastreo — cuesta PS, sin combate

En la sala de servidores, **camino obligatorio**: la decisión no se puede esquivar.

| Campo | Valor |
|---|---|
| `titulo` | `RASTREO` |
| `otorgaFlag` | `Torre1_RastreoCortado` |
| `costoHP` | `30` |
| `combate` | vacío |
| `mensajeExito` | `Rastreo sobrecargado. Vas a poder cambiar de Poken.` |
| `mensajeSinRecursos` | `Tu equipo no aguanta la sobrecarga.` |

El costo se reparte entre los Pokémon vivos y **nunca deja a ninguno en 0 PS** — el GDD dice que los subsistemas no pueden bloquear nada, y quedarte sin equipo en un pasillo sería exactamente eso. Si no alcanza, no pasa nada y podés volver después.

### Energía auxiliar — gratis, cuesta encontrarla

Rama oculta, puerta de servicio detrás de los racks del atrio. **Sin combate y sin costo: cuesta encontrarla.**

| Campo | Valor |
|---|---|
| `titulo` | `ENERGÍA AUXILIAR` |
| `otorgaFlag` | `Torre1_EnergiaAuxiliarCortada` |
| `combate` | vacío |
| `costoHP` | `0` |
| `mensajeExito` | `Energía auxiliar cortada. La liberación va a durar la mitad.` |

No hace la pelea más fácil: hace la **liberación** más fácil. El que quiere salvar a Néstor tiene un motivo distinto al que solo quiere ganar.

---

## 3. Puertas

`DoorTrigger` en un GameObject con `Collider2D` e **`isTrigger` tildado**.

| Campo | Para qué |
|---|---|
| `destino` | un `Transform` vacío, puesto **del lado de adentro** de la sala destino |
| `miraHacia` | hacia dónde queda mirando al llegar |
| `requiereFlag` | opcional, para puertas que abren al cortar algo |

Para que una puerta necesite las cámaras: `requiereFlag = Torre1_CamarasCortadas`.

---

## 4. El combate final

El trigger del núcleo apunta a **uno de dos assets**, según el jugador haya cortado los cuatro subsistemas o no:

- `Definitions/Torre1/Nestor` — Raticate 16 · Braixen 17 · **Melmetal 18** · Porygon-Z 19
- `Definitions/Torre1/Nestor_SinMelmetal` — Raticate 16 · Braixen 17 · **Unown 14** · Porygon-Z 19

Es **todo o nada**: los cuatro completos o Melmetal está. Cortar dos de tres nodos del firewall achica el escudo pero no cuenta, para obligar a terminar lo que se empieza. Unown tiene un solo movimiento y stats bajos: la recompensa por explorar se lee al instante.

---

## 5. Regla de diseño del GDD

> **Cada sala tiene un motivo o no existe.** Combate, subsistema, objeto, o algo que cambie el estado de la torre. Una sala vacía es peor que un mapa de menú: perdiste tiempo de diseño y encima empeoraste el juego.

Presupuesto: 12 salas (7 obligatorias, 5 optativas), ~6 templates de arte, 25-40 min de duración. **Se diseñan templates, no salas**: la sala de racks se repite cuatro veces con distinta decoración.

---

## Problemas típicos

| Síntoma | Causa |
|---|---|
| El jugador no se mueve | Falta `SceneBootstrapper` o sus referencias están vacías |
| Atraviesa las paredes | `obstacleMask` del `PlayerController` no incluye la capa de Colisiones |
| No pasa nada al interactuar | `interactableMask` no incluye la capa del panel, o no lo estás mirando de frente (busca en el tile de adelante, no en el propio) |
| Las puertas no disparan | Al jugador le falta el `Rigidbody2D`, o al collider de la puerta le falta `isTrigger` |
| El subsistema se corta antes de pelear | Pusiste `otorgaFlag` **y** `defeatGrantsFlag`. En los cortes por combate va solo en el asset del enemigo |
| No aparece el cartel de confirmación | No hay `SubsystemFeedbackView` en la escena (cae a `Debug.Log`, mirá la consola) |
