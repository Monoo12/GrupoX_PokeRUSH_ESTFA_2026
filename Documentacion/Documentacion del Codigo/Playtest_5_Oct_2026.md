# Playtest del 5-oct-2026 (versión 1.2.0)

Lo que salió de jugar la 1.1.0 y cómo quedó cada cosa. Se hizo sin supervisión
("no hagas preguntas, cualquier cosa corregimos después"): donde el pedido
dejaba una decisión abierta, la columna **Decidido acá** dice qué se eligió,
para revisarlo.

| # | Pedido | Qué se hizo | Dónde |
|---|---|---|---|
| 1 | Tragaperras: que se gane más seguido, 1 de cada 4 | Se repartieron de nuevo los pesos de los símbolos (la cereza, el premio chico, pesa 100): premio en 1 de cada 4 tiradas (25,1%) y 89-90% de devolución (en la 1.1.0, 1 de cada 5 y 88%). La tabla de pagos dibujada no cambia. | `Resources/Definitions/Casino/MaquinaBasica.asset`, `CasinoPrefabBuilder` |
| 2 | El "clinc" al elegir aturde | Confirmar al 33% (su ganancia era 3, el triple que el resto) y el cursor al 65%: los dos quedan a ~1. | `Resources/Audio/CatalogoDeAudio.asset` |
| 3 | La enfermera del casino cobra 100 | `costo: 100` en los cuatro pisos (era 30). | `CasinoNurseInteractable`, escenas del casino |
| 4 | Mejores fondos de combate | Antes casi todo se peleaba en el pasto del prefab. Ahora cada lugar tiene el suyo (`FondosDeCombate`) y los jefes el propio: ver la tabla de abajo. | `Scripts/Data/FondosDeCombate.cs`, `Editor/FondosDeCombateBuilder.cs` |
| 5 | Subir la música de los pisos 1, 2 y 3 del casino | Retoque de volumen 1,45 / 1,5 / 1,25 (era 1). | `CatalogoDeAudio.asset` |
| 6 | Ruletas con más variedad, sin repetidos del equipo, conocidos y épicos, sin pasarse en la Poké Ball | Cada gajo de Pokémon sortea su especie de un pozo según el ícono: Poké Ball (iniciales y clásicos sin evolucionar), Super Ball (Riolu, Ralts, Scyther...), Ultra Ball (pseudolegendarios en su primera forma, Lapras, Snorlax) y Master Ball (Lucario, Charizard, Gengar, Greninja...). Nunca una especie que el jugador ya tenga (equipo o PC) ni dos iguales en la misma rueda. El nivel y la rareza siguen saliendo de la tabla. | `Scripts/Casino/Core/VariedadDeLasRuletas.cs` |
| 7 | Todos los Pokémon con algún ataque especial | Al crearse (y al cargar una partida vieja) cada Pokémon se asegura un especial: el más nuevo que su learnset ya le deja, o uno básico de su tipo (Ascuas, Pistola Agua, Impactrueno...). Si sabe cuatro, se va el que menos rinde, nunca su físico más fuerte. Vale también para los rivales. | `PokenModel.AsegurarUnAtaqueEspecial`, `AtaquesEspecialesDeRespaldo` |
| 8 | Revisar la confusión | Tenía un error: el contador arrancaba en 1-4 y una de cada cuatro veces se pasaba en la primera acción sin hacer nada. Ahora 2-5, como en la saga (pelea confuso de 1 a 4 turnos), y el autogolpe baja la barra en el momento. | `StatusEffects` |
| 9 | Al subir de nivel, reemplazar un movimiento viejo por el nuevo | Como en los originales: "¿Olvidar un movimiento para que X aprenda Y?" → la pantalla del tutor en modo "subir de nivel" (gratis): elegir cuál olvida o no aprenderlo. | `BattleController.QuiereAprenderRoutine`, `MoveTutorScreen` |
| 10 | Al derrotar un Pokémon rival, avisar cuál viene y ofrecer cambiar | Modo "Cambio" de la saga: "El rival va a enviar a X. ¿Querés cambiar de Pokémon?". No en la pelea de Julieta (su guion cuenta los KO) ni si no hay a quién cambiar. | `TurnEngine.OfrecerCambioAntesDeQueEntre`, `BattleController.OfrecerCambioRoutine` |
| 11 | Sacar Excavar | Retirado por id (91): no se aprende por nivel, ni con el tutor, ni sale entre los iniciales; una partida guardada que lo tenía lo pierde al cargar. El asset y los learnsets no se tocaron: volver a habilitarlo es sacarlo de la lista. | `Scripts/Core/Battle/MovimientosRetirados.cs` |
| 12 | Las Poké Balls chiquitas del rival en el HUD | Debajo del databox rival, una por Pokémon: roja en pie, rosada con estado, gris debilitado. Solo contra entrenadores. | `BattleHudView.UpdateEnemyTeamBalls`, `Editor/EquipoDelRivalEnElHudBuilder.cs` |
| 13 | El cuerpo de Reymar separado de la mesa | El borde que se ve de la mesa está más abajo que el de las celdas (los tiles de arriba son medio transparentes): el busto baja a 29,85 en el piso final (era 30,45) y a -2,85 en la cueva (era -2,4). | `CasinoPisoFinalBuilder`, `QuarchoCuevaBuilder` |
| 14 | El PJ camina "cojo" hacia adelante | La animación se reiniciaba en cada baldosa y siempre arrancaba con el mismo pie. Ahora el paso va atado a la baldosa: pie derecho en una, izquierdo en la siguiente. | `PlayerView`, `PlayerController.PasosDados` |
| 15 | Deuda infinita con la enfermera | Siempre cura: cobrando, o fiado sin tope. El ascensor solo exige saldar la deuda si al jugador le alcanza. | `CasinoNurseInteractable`, `CasinoElevatorInteractable` |
| 16 | Una tienda en el piso 3 | Un tendero al lado de la enfermera y el PC (el rincón de servicios del piso). Vende lo mismo que Tixi, a los mismos precios. | `CasinoTiendaNpc`, `Editor/Casino/CasinoTiendaBuilder.cs` |
| 17 | Después de perder con Julieta, poder volver a la cueva | Tras la charla de la derrota: "¿Volvés a la Quarcho Cueva a prepararte?" (Volver / Reintentar). Se guarda sin el flag de Julieta, así que Tixi lo vuelve a llevar al último nivel. | `PeleaJulieta.OfrecerVolverALaCueva` |
| 18 | Una fuente de plata en la cueva: el **Entrenamiento intenso del chip**, como una torre de batalla; ajustar las ruletas de Martín; la enfermera de la cueva gratis | Ver abajo. Ruletas de Martín: Pokémon 1500, objetos 600, mejoras 500 (eran 400 / 150 / 250: la de objetos devolvía más plata de la que costaba). La enfermera de la cueva ya no cobraba; ahora lo dice. | `EntrenamientoDelChip`, `QuarchoTixiNpc`, `QuarchoCuevaAssets` |

## Los fondos de combate

`BattleController` elige: el fondo del rival si tiene uno, si no el del lugar
(`Resources/Definitions/Combate/FondosDeCombate.asset`) y por último el del
prefab. Antes, una pelea sin fondo heredaba el de la pelea anterior.

| Lugar | Fondo |
|---|---|
| Casino Nivel 1 | GimnasioSabrina (luces de ciudad, piso de neón) |
| Casino Nivel 2 | Castillo (salón oscuro, alfombra roja) |
| Casino nivel 3 | Mansion (mármol a cuadros) |
| Casino piso FINAL | BaseVillano (la oficina de Reymar) |
| NestorGOD2 | Central (sala de máquinas) |
| habitacion | Gimnasio5 |
| Quarcho-Cueva | Extra (el entrenamiento del chip) |
| BossFinal | Laboratorio |

Rivales con fondo propio: Reymar → Campeon, el rival del tutorial → Gimnasio5,
los nodos del firewall → Espacio. Néstor (su arte propio) y Julieta quedan como
estaban. Cambiar un fondo es tocar la tabla desde el Inspector o
`FondosDeCombateBuilder` y volver a correr
`PokeRush > Combate > Elegir los fondos de combate`.

## El Entrenamiento intenso del chip

Lo ofrece Tixi en su menú (segunda opción) **después del casino**. Reglas, como
la torre de batalla:

- Rivales virtuales, uno detrás de otro, con tres Pokémon cada uno.
- Antes de cada combate el equipo se cura entero; al terminar, también.
- Cada victoria paga y sube la racha: 400, 500, 600... hasta 1200.
- Después de cada victoria: seguir o retirarse con lo ganado. Perder corta la
  racha, pero lo ganado ya quedó en el bolsillo.
- Dificultad: el rival arranca 5 niveles por debajo del Pokémon más alto del
  equipo y sube uno por combate (tope +5). Son todos evolucionados: con 3 de
  margen, un Espeon le ganaba de un golpe al Gastly del equipo de prueba. De la quinta racha salen especies
  fuertes (iniciales finales, Lucario, Gengar...) y de la décima, los
  pseudolegendarios. La IA juega mejor desde la cuarta y la séptima.

La cueva no tenía combates: `PokeRush > Quarcho Cueva > Poner el entrenamiento
intenso del chip` le agrega el sistema de combate, el puente mapa-combate, el
`EntrenamientoDelChip` (fuera de `MapaRaiz`) y la opción en Tixi.

## Decidido acá (revisar)

- **El chip recién después del casino** (`flagQueLoAbre = Casino_Completado`).
  Antes del casino rompería su economía: el pase de 4000 cuenta con que el PJ
  llega justo y tiene que jugar o pelear. Para abrirlo siempre: `None` en el
  Inspector.
- Premios del chip (400 a 1200), tres Pokémon por rival y los pozos de especies.
- Precios de las ruletas de Martín: 1500 / 600 / 500.
- Los pozos de las ruletas (qué Pokémon salen en cada ball).
- Los fondos de cada lugar.
- El tendero va en (-11.5, 19.5), con la hoja `Graphics/Characters/tendero`.
- Al cambiar de Pokémon por el aviso del rival, la pregunta arranca en NO (como
  todas las cajas Sí/No del juego).

## Cómo se verificó

- Smoke tests: combate 315 (con los nuevos de la confusión, el ataque especial,
  Excavar, el aviso del rival, las ruletas sin repetidos, el chip, los fondos y
  las bolitas del HUD), casino 164, ruleta 29, blackjack 60, historia 131, cueva
  126, tutorial 106 y Julieta 192, todos en verde.
- En Play: la cueva entera con el chip (Tixi → conectarse → pelea → retirarse)
  y la derrota contra Julieta con la pregunta de volver.
