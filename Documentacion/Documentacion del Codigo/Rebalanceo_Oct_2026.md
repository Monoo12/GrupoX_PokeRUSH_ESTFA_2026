# Rebalanceo de Pokémon y dificultad (6/7-oct-2026)

Pedido: el juego es corto y **no tiene evolución**, así que un Pokémon que se
recibía en su primera forma (Pikachu, Gastly, Riolu...) quedaba así toda la
partida, y los rivales eran débiles para la altura de la historia. Se pidió:

1. Que los enemigos usen Pokémon mucho mejores, desde Nv 36, casi todos en su
   forma final, con jefes épicos y equipos variados.
2. Que el jugador reciba formas finales a lo largo de la aventura, **con el
   sistema de entrega que ya existía** (sin crear otro).
3. Dificultad media, probada jugando, y ajustada hasta que quede pareja.

Además, en la misma tanda: la referencia de la cámara del Data Center y el
nombre de la compañera del tutorial (**Rebeca**). La dificultad se calibró
con el simulador (la partida entera, 360 veces) y con smoke tests; en Play se
jugaron el tutorial y Néstor, y dieron lo mismo que el simulador.

---

## Cómo se le entregan Pokémon al jugador (sin cambios de arquitectura)

Se tocó **solo el contenido** de los mecanismos que ya existían:

| Mecanismo | Dónde | Qué cambió |
|---|---|---|
| Inicial (`CreacionPersonajeView.nivelInicial` + `GameManager.NewGame`) | `CreacionPersonaje.unity` | Nv 20 → **42**. Lucario, Gengar y Greninja ya eran formas finales. |
| Liberados (`LiberarPokenTrigger`) | `NPC_Basculin.prefab`, `NPC_Combusken.prefab` (Data Center) | Basculin Nv 20 → **Basculegion Nv 42**; Combusken Nv 20 → **Blaziken Nv 44**. Texto, nombre y hoja de seguidor nuevos (`DataCenterArreglos`). La bióloga ahora habla de Basculegion. |
| Ruletas de premios (`CasinoPrizeTable` + `VariedadDeLasRuletas` + `PrizeGranter` → `PlayerModel.RecibirPokemon`) | `Resources/Definitions/Casino/*`, `Lobby/RuletaDeMartin_Pokemon` | Los cuatro pozos de especies pasan a **formas finales** y los niveles de cada tabla suben (tabla de abajo). |
| Mew de Tacconi (`CasinoTacconiNpc.nivelPremio`) | `Quarcho-Cueva.unity` | Nv 30 → **52** (dos arriba del Mewtwo de Julieta, el rival más alto del juego). |
| Cosmog shiny de Tacconi | `Casino nivel 3.unity` | Nv 15 → 45 (es la llave del ascensor y un chiste; no pelea). |

### Las ruletas

La regla de siempre: la tabla dice cuántos gajos son Pokémon, a qué nivel y de
qué rareza (el ícono); la especie sale del pozo de esa rareza sin repetir lo
que el jugador ya tiene.

| Pozo (ícono) | Antes | Ahora |
|---|---|---|
| Poké Ball | Iniciales y clásicos en su primera forma | Formas finales conocidas de poder parejo (Eeveelutions, Raichu, Scizor, Luxray, Absol, Corviknight...) |
| Super Ball | Riolu, Ralts, Axew... sin evolucionar | Los iniciales finales de todas las regiones y los favoritos (Charizard, Lucario, Gardevoir, Lapras, Snorlax, Kingambit...) |
| Ultra Ball | Pseudolegendarios en primera forma | Pseudolegendarios ya evolucionados (Dragonite, Tyranitar, Garchomp, Hydreigon...) y Volcarona |
| Master Ball | Lucario, Charizard, Gengar... | Legendarios: las aves, las bestias, Latias/Latios, Heatran, el trío espadachín, Zeraora, Tapu Koko y Tapu Lele |

| Ruleta | Dónde | Nivel antes | Nivel ahora |
|---|---|---|---|
| De los NPC (pisos 1 y 3) | una cada dos victorias, hasta 5 | 15 | **44** |
| De los NPC del piso 2 | ídem, con la Tarjeta VIP | 15 | **46** |
| Del guardia del ascensor | ganarle | 15-18 | **44-46** |
| De la mesa (ruleta por color) | ganar más de 200 | 15 | **45** |
| Del guardia VIP | ganarle | 21-23 | **47-49** |
| Del empresario | 300 el tiro | 24-26 | **47-49** |
| De Martín (cueva) | 1500 el tiro, después del casino | 28-30 | **50-52** |

El progreso queda así: inicial 42 → liberados 42/44 → casino 44-49 → cueva
50-52 → Julieta (43-47 y su as, Mewtwo, Nv 50). Nada de esto llega al
principio: en el Data Center el jugador tiene tres Pokémon; el resto se gana en
el casino.

---

## Los rivales

Todos con formas finales (salvo el Unown de la variante de Néstor, que es un
Pokémon de una sola etapa y el premio por cortar todo). En general es la
evolución de lo que cada uno traía, con reemplazos donde no había o se repetía.

| Rival | Antes | Ahora | IA |
|---|---|---|---|
| **Rebeca** (tutorial) | Eevee 10 | **Vaporeon 38** | Wild |
| **Néstor** | Raticate 16, Braixen 17, Melmetal 18, Porygon-Z 19 | **Silvally 36, Delphox 36, Melmetal 36, Porygon-Z 36** | GymLeader |
| Néstor (todo cortado) | Raticate 18, Braixen 19, Unown 18, Porygon-Z 20 | Silvally 36, Delphox 36, **Unown 36**, Porygon-Z 36 | GymLeader |
| Apostador (p1) | Raticate 12, Kadabra 12 | Alakazam 41, Raichu 41 | Trainer |
| Malabarista (p1) | Mimikyu 14, Klefki 14 | Mimikyu 42, Klefki 42 | Trainer |
| Motorista (p1) | Magby 13, Rhyhorn 14 | Magmortar 42, Rhyperior 43 | Trainer |
| Pokemaníaco (p1) | Elekid 13, Aron 14 | Electivire 42, Aggron 43 | Trainer |
| **Guardia del ascensor** (p1) | Houndour 18, Krokorok 18, Haunter 19 | Houndoom 43, Krookodile 43, Gengar 44 | GymLeader |
| Bailarina (p2) | Swablu 16, Roselia 17 | Altaria 44, Roserade 45 | Trainer |
| Joven rica (p2) | Togepi 15, Sableye 16 | Togekiss 44, Sylveon 45 | Trainer |
| Ladrón (p2) | Pawniard 16, Mawile 17 | Meowscarada 45, Sneasler 45 | Trainer |
| Magnate (p2) | Kirlia 19, Porygon2 20, Lampent 20 | Gardevoir 45, Haxorus 45, Chandelure 46 | Trainer |
| Médium (p2) | Duskull 16, Gastly 16 | Dusknoir 44, Hatterene 45 | Trainer |
| Modelo (p2) | Litwick 15, Shuppet 16 | Froslass 44, Milotic 45 | Trainer |
| Tahúr (p2) | Yamask 16, Honedge 17 | Cofagrigus 45, Aegislash 45 | Trainer |
| **Guardia VIP** (p2) | Sneasel 21, Haunter 22, Krokorok 22, Magneton 23 | Weavile 48, Spiritomb 48, Excadrill 49, Magnezone 49 | GymLeader |
| Crupiera (p3) | Porygon 18, Lampent 19 | Porygon-Z 47, Armarouge 48 | Trainer |
| Gángster (p3) | Houndour 18, Skorupi 19 | Incineroar 47, Drapion 48 | Trainer |
| Guay (p3) | Gligar 17, Skarmory 18, Absol 18 | Gliscor 47, Corviknight 48, Absol 48 | Trainer |
| Karateka (p3) | Primeape 18, Riolu 18 | Annihilape 48, Lucario 48 | Trainer |
| Macarra (p3) | Sneasel 17, Sandile 17 | Weavile 47, Krookodile 48 | Trainer |
| Veterano (p3) | Donphan 18, Lairon 18 | Donphan 48, Steelix 49 | Trainer |
| **Reymar** | Absol 24, Drapion 24, Bisharp 25, Krookodile 25, Weavile 26, Gengar 27 | **Absol 46, Drapion 46, Grimmsnarl 47, Kingambit 47, Tyranitar 48, Hydreigon 49** | Elite |
| **Julieta** | Magnezone 33, Alakazam 34, Genesect 34, Metagross 35, Hoopa 37, Mewtwo 38 | **Magnezone 43, Alakazam 44, Genesect 44, Metagross 45, Hoopa 47, Mewtwo 50** | FinalBoss |

La plata que paga cada rival **no se tocó** (la economía es de Augusto).

---

## Arreglos de balance que no estaban en el pedido

Salieron del playtest y perjudicaban claramente la experiencia:

1. **Las formas finales no sabían pegar con sus tipos.** Con "los últimos 4
   movimientos del learnset", Blaziken a Nv 40 no sabía nada de Lucha (justo su
   papel contra Melmetal y Porygon-Z según el GDD), Greninja nada Siniestro, Mew
   un solo ataque y de tipo Roca, Delphox pegaba Fuego con Giro Fuego (35). Nueva
   regla en `PokenModel.AsegurarAtaquesDeSusTipos`, del mismo estilo que
   `AsegurarUnAtaque` y `AsegurarUnAtaqueEspecial`: por cada tipo, si no sabe
   ningún ataque de ese tipo aprende el más fuerte que su learnset ya le deja (o
   el primero que aprendería); si sabe uno flojo y hay otro que pega el doble,
   lo cambia (con 1,5 y 1,75 el Silvally y el Delphox de Néstor cambiaban a
   Multiataque y Premonición y la pelea se endurecía de más). Solo al crearse:
   un Pokémon guardado conserva lo que eligió el jugador. El reemplazo nunca
   pisa su físico más fuerte ni el único ataque de su otro tipo.
2. **La fase de liberación de Néstor quedaba de adorno.** El chip pegaba 34 / 12
   / 22 fijos, pensados para el equipo de Nv 20 (~62 PS); a Nv 42 (~150 PS) se
   aguantaba sin curar ni cambiar. Ahora escala con los PS del que recibe el
   golpe (`LiberationEngine`, nunca menos que la tabla) con exponente 0,6.
   Medido con el simulador (que ahora también juega la liberación): sin
   escalar casi nunca mata; en proporción directa mata hasta al que cortó todo,
   porque las Pociones curan lo mismo que a Nv 20; con 0,6 hay que protegerse,
   cambiar y curarse. Con los números finales, de 360 partidas se pierden en la
   liberación 7 cortando todo, 23 con rastreo y un nodo, y 20 sin cortar nada.
3. **El escudo del núcleo también**: 10 PS por nodo eran un golpe a Nv 18; a Nv
   40 no llegaban a medio golpe. Ahora 25 por nodo (`BossBattleConfig`).
4. **Gengar no tenía con qué pegar a Nv 38** (Pulso Umbrío recién a Nv 42): con
   el inicial a Nv 38 perdía casi siempre contra Néstor. Por eso el inicial va a
   Nv 42 y no a 38.
5. **Los equipos DEMO** (entrar por Play directo a una escena) eran un Gastly Nv
   20 o seis Pokémon de Nv 34-36: no se le ganaba a nadie. Ahora cada escena trae
   uno a la altura de su etapa (Gengar 42 en el Data Center, 43-47 en el casino,
   seis de 47-50 en la arena de Julieta). Los Arceus Nv 100 de depuración
   quedaron donde estaban.

---

## Cómo se midió

**Simulador** (`PokeRush > Balance > Simular la partida`,
`Editor/Balance/SimuladorDeBalance.cs`): juega la partida entera con el motor de
verdad (TurnEngine, la IA de cada rival, la lectura aliada de Néstor), leyendo
los equipos y las ruletas de los assets, con un "jugador razonable" (pega con lo
que más rinde, cambia si está muy mal parado, usa hasta dos pociones por pelea,
se equivoca un 15%, y en los reintentos abre con el que mejor le va). Contra
Néstor juega también su Predicción y la fase de liberación. 360 partidas, 120
por inicial, con semilla fija: la misma semilla da el mismo informe (hasta el
7-oct los IVs salían de un azar sin semilla y el mismo informe variaba ±4
puntos por pelea). El informe queda en `Balance_Simulacion.txt`.

Resultado final (ganadas al primer intento con el equipo curado; entre
paréntesis, cuántas de las 360 partidas no la ganaban ni en 5 intentos):

| Pelea | Gana al primer intento | Intentos | Notas |
|---|---|---|---|
| Tutorial (Rebeca) | 99% | 1,0 | 3 turnos, termina con 60% de vida |
| Néstor sin cortar nada | 29% | 3,0 (66) | GDD: lo que no apagás lo pagás en el boss; se puede volver a cortar |
| Néstor, rastreo y un nodo | 60% | 2,0 (27) | lo más común |
| Néstor, todo cortado | 89% | 1,1 | |
| NPC del casino | 91-100% | 1,0-1,1 | pierde ~1 Pokémon por pelea, termina con 30-50% de vida |
| Guardia del ascensor | 87% | 1,3 | con Gengar de inicial, 72% |
| Guardia VIP | 88% | 1,2 | |
| Reymar | 64% | 1,8 (27) | Lucario 89%, Greninja 60%, Gengar 44% (Reymar es todo Siniestro) |
| Julieta (6 contra 6 sin guion) | 42% | 2,5 (81) | el guion suma ayudas y trabas: es la pelea a probar a mano |

Antes del rebalanceo, la misma simulación daba: casino 100% en 2-4 turnos (un
paseo), Reymar 37% y **Julieta 0%** (el equipo llegaba a Nv ~25 con formas base
contra Nv 33-38).

Lo que el simulador no juega: la ruleta de Reymar (aparece cada dos turnos y
todo le cae al jugador: más en contra que a favor), su mano de
blackjack (le da una revancha al que perdió, sea quien sea) y el guion de
Julieta (fase 1 a favor, fases 2 y 3 en contra, la ruptura a favor). Por eso
esas dos peleas son las que más conviene jugar a mano.

**Playtest en Play** (`PokeRush > Balance > Playtest en Play`,
`Scripts/Testing/PlaytestDeBalance.cs`, **experimental**): las peleas
importantes en su escena, con el combate real y el mismo piloto. Ver abajo qué
se llegó a jugar.

**Smoke tests**: `PokeRush > Balance > Correr smoke tests del balance` (rivales
a Nv 36+, 90% formas finales, lo que recibe el jugador, ruletas, pozos) y los de
siempre (combate 322 con los nuevos de la regla de tipos y del chip, casino 164,
ruleta 29, blackjack 60, historia 131, Julieta 192, cueva 126, tutorial 106).

### Lo que se jugó en Play

- **Tutorial**: 6 de 6 ganadas contra el Vaporeon de Rebeca.
- **Néstor** (con los niveles de entonces, 36/36/37/38): sin cortar nada 0 de
  6, con rastreo y un nodo 2 de 5, todo cortado 2 de 2. El simulador sin la
  Predicción daba bastante más; con la Predicción y la liberación agregadas al
  simulador, los dos coinciden. Por eso después se bajó a Néstor a Nv 36 parejo
  y se calibró con el simulador.
- Lo más útil del Play fue mostrar que al simulador le faltaba la Predicción
  de Néstor: sin ella, Néstor parecía mucho más fácil de lo que era.
- El piloto en Play quedó **experimental**: el casino, Reymar y Julieta no se
  llegaron a correr enteros. Es lento (animaciones y diálogos de verdad), y al
  ganarle a Néstor el juego vuelve a la cueva (como debe), así que el banco
  tiene que recargar la escena y borrar el flag del nivel entre pelea y pelea.
  Lo que lo trababa era el banco, no el juego. Esas peleas conviene jugarlas a
  mano (lista abajo).

### Para probar a mano

| Pelea | Qué mirar |
|---|---|
| Rebeca (tutorial) | que se gane fácil y se entienda el combate; que diga "Rebeca" en todos lados |
| Néstor, cortando lo típico | que se pueda en 1-2 intentos; que la Predicción y la liberación se sientan como amenaza y no como castigo |
| Néstor sin cortar nada | que sea dura pero que se entienda que cortar subsistemas ayuda |
| Guardia del ascensor y guardia VIP | primeras peleas que piden pensar; con Gengar el del ascensor es la más dura |
| Reymar | con la ruleta y el blackjack: que se pueda en 1-3 intentos |
| Julieta | el guion entero: que las fases 2 y 3 aprieten pero se llegue a la ruptura; que no haga falta entrenar |

---

## Decidido acá (revisar)

- **Basculegion y Blaziken** reemplazan a Basculin y Combusken (pedido); los
  objetos de la escena siguen llamándose `NPC_Basculin` y `NPC_Combusken`.
- **Néstor**: Silvally reemplaza a Raticate (Normal como el original, "Pokémon
  sintético"), Braixen → Delphox. Melmetal y Porygon-Z siguen. Todos a **Nv 36
  parejo** (antes 36/36/37/38): con Melmetal 37 y Porygon-Z 38 el simulador
  daba 49% en el caso típico (rastreo y un nodo) y 23% sin cortar nada, y en
  Play salió 2 de 5 y 0 de 6. Ahora 60% y 29%, y 89% cortando todo: cortar
  sigue valiendo la pena.
- **Reymar**: la línea de siempre (Absol, Drapion, Bisharp ya Kingambit) más
  Grimmsnarl, Tyranitar e Hydreigon de as. Se probó Urshifu y lo volvía
  demasiado difícil (33%). Es todo Siniestro: con Gengar de inicial cuesta
  bastante más (44% contra 89% con Lucario), y Blaziken es la respuesta.
- **Julieta**: los mismos seis (Mewtwo y Hoopa son los del portal). Mewtwo 50
  es el rival más alto del juego; el resto queda **por debajo de los números de
  Reymar** (43-47 contra 46-49) porque son legendarios y míticos y pesan más que
  lo que dice el nivel. Con 45-50 el simulador daba 27%, y un 39% de las
  partidas no le ganaba ni en 5 intentos. Lo que más pesa es Mewtwo (bajarlo a
  48 o 49 sube la tasa 8-10 puntos) y Alakazam: a Nv 45 aprende Premonición
  (120) y la tasa baja unos 5 puntos. Si en la prueba a mano queda fácil, el
  primer número a subir es ese (Alakazam a 45).
- **Rebeca** con un Vaporeon: el Eevee de la compañera, evolucionado.
- El **precio del empresario** (300 el tiro) no se tocó, aunque ahora da formas
  finales Nv 47-49. Si se quiere que la plata pese más, es el primer número a
  mirar.
- La **referencia de la cámara**: "Ves a "La directora" introduciendo chips
  Neura-LINK a Dante!" (en el pedido decía "Vez"). La historia llama al chip
  "NeuraRush-LINK"; se dejó "Neura-LINK" como se pidió.
- Las partidas guardadas **de antes** conservan sus Pokémon de Nv 15-30: el
  balance nuevo es para partidas nuevas.
