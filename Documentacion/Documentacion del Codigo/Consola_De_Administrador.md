# Consola de administrador (7-oct-2026)

Una consola para probar la build sin jugar todo de nuevo: ir a cualquier punto
de la historia, dar Pokémon, plata y objetos, ganar una pelea, atravesar paredes,
acelerar el juego. Tiene una **línea de comandos** y **botones** para lo más usado.
Cada botón escribe su comando, así el registro enseña qué escribir.

## Cómo se usa

1. **Opciones → Pruebas → Consola de administrador: Sí** (Izq/Der o Aceptar).
   Queda guardado en la máquina, como el volumen: borrar la partida no la apaga.
   "Restaurar valores" la vuelve a apagar.
2. En cualquier escena, la tecla de la **izquierda del 1** (la **|** en el teclado
   latinoamericano) la abre y la cierra. Se lee por la posición de la tecla, así
   que anda igual con un teclado español (º) o de EE.UU. (`). **Esc** también la
   cierra.
3. Se escribe el comando y Enter. **Tab** completa (comandos, especies, objetos,
   destinos, flags), **↑/↓** recorren lo escrito antes.

Con la consola cerrada, si quedó algún truco prendido (dios, paredes, velocidad,
sin autoguardado) se ve un cartel arriba a la izquierda. Apagar la consola en
Opciones apaga sus trucos.

## Comandos

Los nombres no distinguen mayúsculas ni acentos, y alcanza con el principio si no
es ambiguo (`dar blazi`, `ir casinof`).

| Grupo | Comando | Qué hace |
|---|---|---|
| Equipo | `equipo` | El equipo (nivel, PS, movimientos) y cuántos hay en el PC. |
| | `dar <especie\|número> [nivel] [shiny]` | Da un Pokémon por nombre o número de Pokédex (`dar 257 50`). Sin nivel, el más alto del equipo. Va al equipo si hay lugar y si no al PC: es `PlayerModel.RecibirPokemon`, el mismo camino que las ruletas y los liberados. |
| | `quitar <lugar>` | Manda al PC al Pokémon de ese lugar (1-6). No deja el equipo vacío. |
| | `kit` | Equipo de prueba: Lucario 50, Blaziken 48, Garchomp 50, Gardevoir 49, Gengar 49, Basculegion 47 (el de la arena de prueba de Julieta). El equipo de antes va al PC. |
| | `curar` | PS, estados y PP, también en plena pelea. |
| | `nivel <N> [lugar]` | Sube a nivel N a todo el equipo o a uno. Por experiencia, como al pelear: no aprende movimientos y no baja. |
| | `subir [cantidad]` | Todo el equipo sube esa cantidad (5 si no se dice). |
| Objetos | `plata <cantidad>` | Suma (o saca, con negativo; nunca deja menos de 0). |
| | `objeto <nombre> [cantidad]` | Pociones, objetos de batalla y objetos clave (la Tarjeta VIP...). Sin nombre, la lista. |
| Combate | `ganar` | Gana la pelea en curso por el cierre normal: plata, flags del rival, guardado y lo que venga después (la mano de blackjack de Reymar, el final de Julieta). Contra Néstor cuenta como liberarlo. |
| | `perder` | Pierde la pelea en curso. |
| | `rival1` | Deja al Pokémon rival en campo con 1 PS. |
| | `dios [si\|no]` | Tu equipo no recibe daño de nada (ataques, estados, la ruleta, el chip). |
| | `fase` | Contra Julieta: la fase actual cambia en el próximo turno. |
| Historia | `ir <destino>` | Va a un punto de la historia y deja los flags como si se hubiera jugado hasta ahí (ver abajo). Sin destino, la lista. |
| | `escena [nombre]` | Carga cualquier escena del build sin tocar la historia. |
| | `flag <nombre>` / `flag -<nombre>` | Prende o apaga un flag de historia. |
| | `flags [filtro]` | Los flags prendidos. |
| | `historia` | Niveles completos, el que sigue y la escena actual. |
| Mundo | `fantasma [si\|no]` | Atravesar paredes (puertas, zonas y NPCs siguen reaccionando). |
| | `velocidad [x]` | Velocidad del juego (`velocidad 4`, hasta x10). |
| | `guardar` | Guarda la partida. |
| | `autoguardado [si\|no]` | Apaga el guardado automático (al salir a un nivel, ganarle a un NPC del casino, comprar...) para no pisar la partida de verdad mientras se prueba. |
| | `fps [si\|no]` | Cuadros por segundo arriba a la derecha. |
| Consola | `ayuda [comando]`, `limpiar` | |

Si no hay partida (la pantalla de título), los comandos que la necesitan arrancan
una de prueba ("PRUEBA").

### Los destinos de `ir`

| Destino | Escena | Qué queda marcado |
|---|---|---|
| `habitacion` | habitacion | La partida arrancada |
| `cueva` | Quarcho-Cueva | Tutorial completo, el camino contado y Tixi presentada (la salida abre) |
| `datacenter` | NestorGOD2 | Igual que `cueva` |
| `cueva2` | Quarcho-Cueva | Néstor liberado y su charla; Tixi todavía tiene que contar la vuelta |
| `casino1` | Casino Nivel 1 | La vuelta del Data Center contada |
| `casino2` | Casino Nivel 2 | El pase y el piso 1 superado |
| `casino3` | Casino nivel 3 | El pase VIP y el piso 2 superado; la Tarjeta VIP |
| `casinofinal` | Casino piso FINAL | Tacconi vencido y el piso 3 superado; la tarjeta del piso 4 |
| `cueva3` | Quarcho-Cueva | Reymar vencido, el servidor 02 y el casino completo |
| `julieta` | BossFinal | La vuelta del casino contada y el transmisor de Tixi (el flag y el objeto) |

Los flags se acumulan. Saltar para atrás **apaga** los de los puntos que vienen
después (`ir datacenter` desde el final encuentra a Néstor sin liberar); lo que no
es un hito (un NPC vencido, un diálogo leído) queda como estaba. Los objetos clave
se dan si faltan y no se sacan al volver (la mochila no tiene cómo quitarlos). El equipo no se
toca: para llegar al casino con algo parecido a lo de una partida, `dar basculegion
42` y `dar blaziken 44`, o `kit` para el final. Los destinos están en
`DestinosDeLaConsola`: agregar un punto es sumar una entrada.

## Cómo está hecha

| Archivo | Qué hace |
|---|---|
| `Scripts/Consola/ComandosDeLaConsola.cs` | El intérprete: comandos, búsqueda por nombre, autocompletar. No dibuja ni sabe de teclas. Lo que toca del juego entra por delegados (jugador, historia, pelea en curso, carga de escena...) que los tests reemplazan. |
| `Scripts/Consola/DestinosDeLaConsola.cs` | Los puntos de la historia de `ir` y cómo marcar los flags. Puro. |
| `Scripts/Consola/ConsolaDeAdministrador.cs` | La ventana (IMGUI), la tecla, el historial. Se crea sola al arrancar (`RuntimeInitializeOnLoadMethod`) y sobrevive a los cambios de escena. |
| `Editor/Consola/ConsolaSmokeTests.cs` | 149 checks: cada comando contra un jugador y una historia de prueba. **PokeRush → Consola → Correr smoke tests de la consola**. |

Lo que se tocó de otros sistemas (todo chico):

- `GameSettings.ConsolaHabilitada` (clave `pruebas.consola` en la tabla de
  opciones) y la fila en `OptionsScreen`. La tecla de la consola no se puede
  asignar a otra acción.
- `GameInputService.TecladoOcupado` / `OcuparTeclado`: mientras se escribe, el
  input del juego se apaga igual que al reasignar una tecla (el personaje no se
  mueve, la X no confirma diálogos). Sigue ocupado el frame en que se cierra, así
  el Esc que la cierra no abre el menú. Los pocos que leen el teclado directo
  (el 1-4 del combate, F2, el Esc de las pantallas con botones, "apretá
  cualquier tecla" del título, la creación de personaje) preguntan por esto.
- Mientras está abierta se apagan los `EventSystem` de la escena: si no, el Enter
  que ejecuta un comando además apretaría el botón seleccionado (por ejemplo
  "Borrar partida" en MenuSaves). Al cerrarla vuelven.
- `BattleController.GanarAhora()`, el par de `Abandonar()`.
- `PokenModel.NoRecibeDanio` (modo dios: todo el daño pasa por `TakeDamage`) y
  `PlayerController.AtravesarParedes` (saltea el chequeo de obstáculos).
- `DefinitionRepository.NombresDePokens / NumeroDePokedex / NombresDeObjetos`:
  buscar y autocompletar sin cargar las 445 especies (salen del índice).

**Por qué IMGUI y no un Canvas:** tiene que andar arriba de cualquier escena,
tenga o no `EventSystem` (varias no tienen), sin prefab que mantener y sin meterse
en la navegación de los menús. Es lo mismo que usa el banco de prueba de Julieta.
No es UI del juego: no la ve quien no la prende.

## Probado

- Smoke tests: 149 de la consola, y los de siempre siguen en verde.
- En Play (Quarcho-Cueva): la tecla abre la consola (evento de la tecla de la
  izquierda del 1 por el Input System), los comandos andan, `ir casino1` carga el
  piso con la historia marcada y el `EventSystem` del casino queda apagado
  mientras está abierta. En una pelea, `rival1` y `ganar` cierran la pelea como
  victoria con su premio. Esc la cierra sin abrir el menú. La fila de Opciones se
  ve y se guarda.
- El banco de menús en Play (**PokeRush → UI → Probar sistema de menus**) da
  75/75 corrido con **Casino Nivel 1** abierta. Con la Quarcho Cueva abierta
  falla uno ("Y la vuelve a abrir"): la entrada de Tixi se queda con la tercera Z.
  Falla igual sin la consola, así que es de la escena y no de este cambio.
  Ojo: ese banco guarda la partida en `pokerush.db` (la misma de la build).
