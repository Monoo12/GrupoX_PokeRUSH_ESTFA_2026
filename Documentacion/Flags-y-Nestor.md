# Flags de historia y el combate contra Nestor

Cómo el juego recuerda lo que hizo el jugador, y cómo eso cambia la pelea final
de la torre 1.

## La idea central: no hay estado de "progreso de Nestor"

Nestor **no registra nada**. No existe ningún objeto que lleve la cuenta de los
subsistemas cortados.

Lo único que se guarda son **StoryFlag**. Todo lo demás —cuánto escudo tiene el
boss, si podés cambiar de Pokémon, cuánto dura la liberación— se **calcula en el
momento** en que arranca el combate, leyendo esos flags.

Es a propósito, y es lo que hace que sea difícil de romper:

- Hay **una sola fuente de verdad**. No hay dos cosas que puedan desincronizarse.
- `SaveService` ya persiste los flags, así que la dificultad del boss sobrevive
  guardar y cargar **sin código extra**.
- No se puede "corromper" el progreso: si el flag está, el efecto está.

## Los flags de la torre 1

Viven en el enum `StoryFlag`:

| Flag | Qué representa |
|---|---|
| `Torre1_CamarasCortadas` | Cámaras de vigilancia fuera de servicio |
| `Torre1_FirewallNodoA` | Nodo A del firewall caído |
| `Torre1_FirewallNodoB` | Nodo B |
| `Torre1_FirewallNodoC` | Nodo C |
| `Torre1_RastreoCortado` | Sistema de rastreo apagado |
| `Torre1_EnergiaAuxiliarCortada` | Energía auxiliar cortada |
| `Torre1_NestorLiberado` | Se completó la fase de liberación |

## Cómo se graba un corte

Dos caminos, según el subsistema. La diferencia es de **datos**, no de lógica:
un mismo componente (`InteractableTrigger`) cubre los dos.

### Sin combate — rastreo, energía auxiliar

El jugador interactúa con el panel y listo. El trigger activa su campo
`otorgaFlag` directamente.

El rastreo además cuesta PS (`costoHP`): se cobran repartidos entre el equipo y
**nunca dejan a un Pokémon en 0**, porque el GDD dice que los subsistemas no
pueden bloquear contenido.

### Con combate — cámaras, nodos de firewall

El trigger lanza la pelea y **no otorga el flag**. Lo hace `EventController`
cuando el combate termina en victoria, leyendo el campo `defeatGrantsFlag` del
`EnemyDefinition`.

> **Al configurar un corte por combate**, el flag va en el asset del enemigo, no
> en el trigger. Si lo ponés en el trigger se otorga al *tocarlo*, aunque
> pierdas la pelea.

## Cómo se lee al empezar el combate

```
StoryFlag
   ↓  (fachada de solo lectura)
TowerProgressModel          ← GameManager.Tower
   ↓  (traducción a parámetros de pelea)
BossBattleConfig.DesdeTorre()
   ↓
BattleController.StartBattle()
```

`TowerProgressModel` no guarda nada: cada propiedad es una consulta sobre
`StoryModel`. Existe para que medio proyecto no ande preguntando
`story.HasFlag(StoryFlag.Torre1_FirewallNodoB)` suelto por ahí.

`BossBattleConfig` es la traducción de "lo que hiciste caminando" a "lo que pasa
en la pelea". Se arma una vez al empezar y de ahí en más solo se lee.

## Qué cambia cada subsistema

| Subsistema | Efecto si lo cortás | Campo de config |
|---|---|---|
| **Cámaras** | La lectura de Nestor avisa un turno antes | `PrediccionConAviso` |
| **Firewall** | Escudo del núcleo: −10 PS por nodo | `EscudoInicial` |
| **Rastreo** | Habilita cambiar de Pokémon | `PermiteCambio` |
| **Energía auxiliar** | La liberación dura 4 turnos en vez de 8 | `TurnosLiberacion` |
| **Los cuatro** | Nestor pelea **sin Melmetal** | `SinMelmetal` |

Detalle que confunde: el escudo se cuenta por nodos **activos**, o sea los que
*no* cortaste. Cortar los tres lo deja en 0.

Las cámaras cambian el **timing** del aviso, no cada cuánto lee. La frecuencia
(`BossBattleConfig.PrediccionCadaTurnos`, hoy cada 3 turnos) es un número de
balance suelto, no un efecto de subsistema: se toca ahí y afecta a toda la
pelea por igual.

## Tres formas distintas, a propósito

- **Binarios** — cámaras, rastreo, energía. Cortás o no cortás.
- **Parcial** — el firewall es el único con gradiente: cada nodo achica el
  escudo. Está para enseñar que esto no es todo o nada.
- **Todo o nada** — Melmetal solo desaparece con los **cuatro** subsistemas, y
  el firewall cuenta solo si están los tres nodos. Dos de tres achican el escudo
  pero no alcanzan: es lo que obliga a terminar lo que empezás.

## Cómo se configura la variante sin Melmetal

En el asset del boss, campo `varianteTodoCortado`: apunta a otra
`BossDefinition` entera (`Nestor.asset` → `Nestor_SinMelmetal.asset`).

Es una definición completa y no solo un equipo porque la variante puede cambiar
también sprites, diálogo y recompensas. Si queda vacío, el boss pelea siempre
con el mismo equipo sin importar los subsistemas.

Lo que la variante deja **vacío** lo toma del boss original: a quién se libera
(`pokenDeLiberacion` y su nivel), el fondo y el sprite de entrenador
(`BattleController.DefinicionDeLiberacion`). Por eso `Nestor_SinMelmetal` no
repite a Néstor. Hasta el 29-sep no heredaba nada: con los cuatro subsistemas
cortados, la fase de liberación seguía con Porygon-Z, Néstor no aparecía nunca y
se peleaba en el fondo de pasto por defecto.

## Agregar un subsistema nuevo

1. Agregar el `StoryFlag`.
2. Exponerlo como propiedad en `TowerProgressModel`.
3. Agregar el campo a `BossBattleConfig` y mapearlo en `DesdeTorre()`
   (y en `PorDefecto()`, que es la config de quien pelea sin pasar por la torre).
4. Consumirlo donde corresponda en `BattleController`.
5. Poner un `InteractableTrigger` en la escena de la torre.
6. **Agregar el check en los smoke tests.** Sin esto no hay nada que avise si el
   efecto deja de llegar: un boss que no cambia se ve exactamente igual que uno
   que sí cambió poco.

El punto 6 no es ceremonia. `SinMelmetal` se calculaba bien durante semanas y
**no lo leía nadie** en el flujo real: el jugador cortaba los cuatro subsistemas
y Nestor seguía apareciendo con Melmetal. No fallaba nada, no había error en
consola, simplemente el trabajo no tenía efecto.

## Probarlo sin caminar la torre

- **`BattleTester`** (escena `CombatePrueba`, tecla **N**): marcás los
  subsistemas en el Inspector y peleás directo. Sirve para las 16 combinaciones.
- **Smoke tests** (`PokeRush > Correr smoke tests`): verifican la cadena entera
  de flags a config, incluidas las reglas de forma de arriba.
