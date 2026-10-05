# Playtest del 4-oct-2026 (versión 1.1.0)

Lo que salió de jugar la build 1.0.0 entera y cómo quedó cada cosa. El detalle de
cada sistema está en su documento; acá va el índice.

| # | Pedido | Qué se hizo | Dónde |
|---|---|---|---|
| 1 | Los cuadrados rojos se ven en la build | Las paredes invisibles de Santiago (`ParedInvisibleTile`) deciden su color con `Application.isPlaying`, pero la build dibujaba el color que quedó guardado en el tilemap. `ParedesInvisiblesEnJuego` refresca esas celdas al cargar cada escena. | `Assets/ANDRUSIANI/Runtime/ParedesInvisiblesEnJuego.cs` |
| 2 | Néstor sin subsistemas, un poco más difícil | Su variante sube de nivel: Raticate 18, Braixen 19, Unown 18 (era 14), Porygon-Z 20. | `Resources/Definitions/Torre1/Nestor_SinMelmetal.asset` |
| 3 | El Basculin del agua se ve pixelado | Usaba el ícono de menú agrandado y con filtro bilineal. Ahora es la hoja de seguidor del pack, nadando de costado, al pixel de los personajes. | `Editor/Historia/DataCenterArreglos.cs` |
| 4 | Los servidores cortan la cabeza del PJ | La mitad de arriba de los racks estaba en `Techos` (se dibuja encima y no choca): 26 celdas pasaron a `Colisiones`. | `Editor/Historia/DataCenterArreglos.cs` |
| 5 | Controles distintos en cada pantalla | X / Enter aceptan, Z / Retroceso / Escape vuelven y el mouse anda, en todas. La pantalla de partidas era la única que no tomaba la X. | `Sistema_De_Menus.md` → Controles |
| 6 | Economía | El pase del piso 1 cuesta 4000, más de lo que trae el PJ (~2.700-3.350): hay que pelear o jugar a las máquinas, o ganarle al guardia. El tiro del empresario sube a 300. Los demás precios, como estaban. | `Casino_Personajes_Y_Ruletas.md` → La economía |
| 7 | Potencia y precisión en el combate | `PanelDatos` al lado de PP/TIPO: categoría, POT y PRE. | `HUD_De_Batalla.md` |
| 8 | Más Pokémon en el casino / nombrar al empresario | Ruletas de los NPCs y de las mesas: 65% Pokémon (era 35% y 25%); hasta 5 ruletas por partida. El anciano, la damisela, el jubilado y Tixi nombran al empresario. | `Casino_Personajes_Y_Ruletas.md` |
| 9 | Enfermera en la Quarcho Cueva | A la izquierda del escritorio del PC, cura gratis. | `Quarcho_Cueva_Lobby.md` |
| 10 | El tutorial habla de la tabla de tipos | La compañera la explica antes de la pelea de práctica. | `Tutorial_Habitacion.md` |
| 11 | Chances del póker y las tragaperras | Tragaperras: premio en 1 de cada 5 tiradas y 88% de devolución (era 1 de cada 7 y 86%), sin tocar la tabla de pagos dibujada. Blackjack: reglas estándar, sin cambios. | `Casino_Personajes_Y_Ruletas.md` → La economía |
| 12 | NPCs bien parados en el piso final | Los tres guardias de la izquierda estaban contra la pared: ahora al lado de la alfombra, como los de la derecha. | `Casino_Piso_Final.md` |
| 13 | Reymar y su mesa más grandes, enfermera en ese piso | Mesa x1,5 (dibujo + tile invisible), busto x1,4, enfermera enfrente del PC. | `Casino_Piso_Final.md` |
| 14 | Flecha a la salida del tutorial | Aparece al terminarlo, sobre la puerta. | `Tutorial_Habitacion.md` |
| 15 | Menos lectura seguida | Reymar: cuatro tramos camino a la mesa y el final atrás de la mesa. Julieta: el final se juega tocando el servidor dos veces y hablándole. Tixi: el transmisor sale al irse de la cueva. | `Casino_Piso_Final.md`, `Jefe_Final_Julieta.md`, `Quarcho_Cueva_Lobby.md` |
| 16 | Buffos y debuffos menos pesados | La ruleta en combate mueve 1 etapa (eran 2) y cura un 35% (era media barra). | `Casino_Piso_Final.md` → Los ocho gajos |
| 17 | Néstor: 1 turno de gracia y 2 de cooldown | Valores por defecto del `BattleController` y del prefab de combate. | `Sistema_De_Combate_Y_IA.md` |
| 18 | Excavar como en la saga | Se esconde, los ataques fallan salvo Terremoto/Magnitud (x2) y Fisura; lo mismo para Vuelo y Buceo con sus excepciones. | `Sistema_De_Combate_Y_IA.md` → Movimientos de carga |
| 19 | Bajarle el nivel a Reymar | 24-27 (era 26-29). | `Casino_Piso_Final.md` |

## Cómo se verificó

- Smoke tests: combate 281, casino 164, blackjack 60, ruleta 29, historia 131,
  cueva 126, tutorial 106 y Julieta 192, todos en verde.
- La pelea de Julieta entera en Play sobre `BossFinal` (67 checks), incluido el
  final de a tramos y los créditos.
- En Play: el panel de potencia/precisión en una pelea del casino y Excavar
  (se esconde, el rival falla, vuelve a aparecer y pega).
- Capturas del piso final, la cueva, la flecha del tutorial y el Data Center
  después de cada cambio de escena.

Lo que **no** se pudo probar sin una build es el punto 1 (en el editor las
paredes ya se veían bien): hay que mirarlo en la build 1.1.0.
