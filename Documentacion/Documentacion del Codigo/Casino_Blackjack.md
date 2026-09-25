# La mesa de blackjack (piso 3 del casino)

Cómo está armado el minijuego, dónde tocar cada cosa y cómo probarlo.

Es la tercera pantalla del casino y sigue el mismo molde que las otras dos
(tragaperras y mesa de ruleta): la lógica va aparte de la UI, la UI es una
pantalla del `MenuManager`, y todo lo que se ve lo genera un builder del editor.
Si ya leíste cómo funciona la ruleta, esto no te va a sorprender.

---

## Las piezas

| Archivo | Qué hace |
|---|---|
| `Scripts/Casino/Blackjack/BlackjackTable.cs` | **Las reglas.** No sabe nada de Unity: cuánto vale una mano, cuándo pide el croupier, quién gana y cuánto paga. Se puede probar sin abrir una escena. |
| `Scripts/Casino/Blackjack/BlackjackTypes.cs` | Carta, mano, resultado y los tipos que viajan entre el mundo y la pantalla. |
| `Scripts/Casino/UI/BlackjackScreen.cs` | La pantalla: fases, input, animaciones y dibujo. |
| `Scripts/Casino/World/BlackjackTableInteractable.cs` | La mesa puesta en el mapa. Abre la pantalla y otorga el flag del piso. |
| `Editor/Casino/BlackjackArtImporter.cs` | Deja el arte importado y corta las hojas en sprites. |
| `Editor/Casino/BlackjackTableBuilder.cs` | Arma el prefab de la pantalla y lo registra en el `MenuSystem`. |
| `Editor/Casino/BlackjackSmokeTests.cs` | Prueba las reglas (60 checks). |
| `Scripts/Testing/BlackjackSmokeTest.cs` | Juega una sesión entera en Play Mode (64 checks). |
| `scripts/cortar_arte_blackjack.py` | Prepara los `bj_*.png` a partir del arte crudo. |

---

## Las reglas que se implementaron

Son las de un casino de verdad, no inventadas. Están todas en `BlackjackTable`
y cada una tiene su smoke test:

- **Zapato de 4 barajas.** Con una sola, contar cartas es trivial y las rachas se
  sienten amañadas. Se rearma cuando quedan menos de 20 cartas, y siempre
  **entre manos**, nunca a mitad de una.
- **El As vale 11 o 1**, y los ases bajan **de a uno**: `A + A + 9` son 21, no 31
  ni 11. Es el error clásico y tiene test propio.
- **Las figuras valen 10.**
- **El croupier se planta en 17, incluso en 17 suave** (regla S17). Es la
  variante que menos ventaja le da a la casa; perder contra un croupier que roba
  en 17 suave se lee como trampa.
- **Blackjack paga 3 a 2**, ganar paga 1 a 1, empatar devuelve la apuesta.
  Con apuestas impares se redondea para abajo, como una mesa de verdad.
- **Pasarse pierde siempre**, aunque el croupier también se pase después. Esa es
  la ventaja de la casa y es lo que hace que plantarse en 16 sea una decisión.
- **Se dobla solo con dos cartas** y después se recibe **una sola** carta.
- **Se divide una vez por ronda**, con dos cartas del mismo *valor* (así `K + Q`
  también se puede partir). Los **ases divididos reciben una carta y se cierran**.
  Un 21 servido después de dividir **no es blackjack**: paga 1 a 1.
- No hay seguro: el arte no trae botón y la mesa no lo pide.

---

## El flujo y los botones

```
APUESTA  →  REPARTIENDO  →  TURNO DEL JUGADOR  →  TURNO DEL CROUPIER
                                                        ↓
                          NUEVA RONDA  ←  CARTEL  ←  RESULTADO
```

El arte trae seis botones: PEDIR, PLANTARSE, DOBLAR, DIVIDIR, REINICIAR y SALIR.
No trae ninguno que diga «repartir», y no hace falta: **PEDIR significa siempre
lo mismo, «dame cartas»**, así que en la fase de apuestas reparte y durante el
turno roba. Para limpiar las fichas puestas se usa **Cancelar**, que además es lo
que ya hace «volver atrás» en el resto del juego.

Qué se puede tocar en cada fase lo decide un solo método, `Habilitado(Accion)`.
La pintura de los botones y el teclado preguntan ahí, así que **no puede pasar
que un botón se vea apagado y igual responda**. Durante el reparto, el turno del
croupier y el cartel no se acepta ningún input.

Cuándo se cobra:

- La apuesta se descuenta **al repartir**, no al poner cada ficha. Así cerrar la
  mesa con fichas puestas no obliga a devolver nada, y no hay forma de perder
  plata sin haber jugado.
- Doblar y dividir cobran **en el momento**, que es cuando el jugador se compromete.
- El premio se acredita **al resolver**, no al cerrar el cartel: si el juego se
  corta con la caja de texto abierta, la plata ya es del jugador.

---

## Lo que se ve

Todo el arte del pack vive en `Assets/BlackJack God/`. Dos cosas no salen de ahí
porque el pack no las trae y el proyecto ya las tenía:

- **el croupier** es `Graphics/Characters/noble.png`, el señor de galera y traje.
  Va dibujado **detrás** de la mesa, así el borde de madera le tapa las piernas y
  se lee como que está parado del otro lado;
- **la sala** es el battleback `Graphics/Battlebacks/InteriorNegro_bg.png`, que
  mide justo 512×384 (la referencia del Canvas) y entra entero.

**Dividir reparte por la misma cola que el reparto inicial**, así que las dos
cartas nuevas llegan volando desde el mazo de a una, y las dos que ya estaban se
corren deslizándose a su nuevo lugar. La primera versión las metía en la mano y
las dibujaba ya puestas: era la única jugada del juego sin animación. Cada mano
tiene su propia placa «TUS CARTAS»; la que no se está jugando queda atenuada.

Las cartas van **sobre el fieltro**: las del croupier en el paño libre de arriba
y las del jugador sobre los casilleros de apuesta. Por eso la mesa está dibujada
grande y las cartas chicas. Cuando una mano se hace larga, las cartas se aprietan
solas hasta un mínimo que siempre deja ver el número de la esquina.

### El arte derivado

Los recortes del pack vienen bien de color y con alpha de verdad, pero tres cosas
hay que arreglarlas antes de usarlas. Las hace `scripts/cortar_arte_blackjack.py`
y quedan como `bj_*.png` al lado de los originales, que no se tocan:

1. **Las 52 cartas no se pueden cortar con una grilla.** En la hoja están a un
   paso de 48,46 px y cada una salió de un tamaño distinto (de 47×72 a 51×75), así
   que una grilla regular parte las cartas de la séptima en adelante. El script
   busca el corte real entre carta y carta y las rearma en una grilla exacta.
2. **La fila de tréboles trae dos cartas mal dibujadas** (en el lugar del 3 hay un
   2, y la K salió con la letra pisada). Se arreglan trasplantando el número de la
   carta de picas equivalente, que es el otro palo negro y usa la misma tipografía.
3. **Las fichas traen el valor escrito debajo y cortado al medio.** Se quedan solo
   con el disco; el número lo dibuja la UI con la fuente del juego.

Las hojas se cortan con `SpriteEditorDataProvider` y **no** con el slicer
automático: en Unity 6 el campo `spritesheet` del `.meta` se ignora y el slicer
por celdas vuelve a detectar bordes por alpha, con lo que una carta con la esquina
transparente sale corrida respecto de las demás.

### Tamaños

El `CanvasScaler` es `ScaleWithScreenSize` en modo **Expand** sobre 512×384, así
que en un 16:9 de 1080 todo se agranda **2,8125x** (manda el alto). De ahí salen
las medidas del builder: una celda de carta de 104 px dibujada a 37 unidades cae
1:1 y se ve nítida. Si vas a mover algo, acordate de que **el arte NO se exporta
al tamaño del Canvas**: exportar una carta a 37 px la deja como un borrón.

---

## Cómo se arma y cómo se prueba

Desde el menú `PokeRush/Casino/`, en este orden:

1. `python scripts/cortar_arte_blackjack.py` — solo si cambió el arte crudo.
2. **Importar el arte del blackjack** — deja los PNG en Point y corta las hojas.
   (Lo corre solo el builder, no hace falta a mano.)
3. **Construir la mesa de blackjack** — genera
   `Assets/Prefabs/UI/Casino/BlackjackScreen.prefab` y lo agrega al `MenuSystem`.
4. **Colgar las interacciones en las escenas** — pone las cuatro mesas del piso 3
   sobre los muebles 4×3 que dibujó el equipo de nivel. Es idempotente.

Para probar:

- **Correr smoke tests del blackjack** — las reglas, sin abrir escena (60 checks).
- **Probar el blackjack** — juega una sesión entera en Play Mode (64 checks) y
  deja capturas en `Capturas/Blackjack/`. El zapato se arma a mano en cada ronda
  para poder afirmar algo sobre el resultado: se prueban ganar plantándose,
  pasarse, empatar, blackjack servido, doblar y dividir.

---

## Cómo se destraba el piso

`CasinoProgressModel.BlackjackGanado` mira el flag `Casino_Blackjack_Ganado`, y
el ascensor del piso 3 lo pide para dejar subir al último piso.

La primera de las cuatro mesas es la que otorga el flag; las otras tres juegan
igual pero sin flag, para que volver a ganar no vuelva a cantar el cartel del
objetivo. **Alcanza con ganar una mano**, no hace falta hacer blackjack (hay un
checkbox `exigeBlackjack` en la mesa si alguna vez se quiere endurecer).

A diferencia de la ruleta —que destraba el ascensor con solo *jugar*, porque
atarlo a ganar dejaría al jugador encerrado por mala suerte— acá el flag pide
ganar. No es un problema: si la ronda sale mal se vuelve a jugar, y la apuesta
mínima está puesta baja para que siempre alcance.

> **Pendiente:** el comentario original de `CasinoProgressModel` decía que el
> objetivo era ganar una mano *apostando un Poken*. Apostar Pokémon no existe
> como mecánica en ningún lado del proyecto, así que el flag se otorga ganando
> con dinero. Si se quiere la versión con Poken, es una mecánica nueva que toca
> el equipo y el guardado, no un ajuste de esta pantalla.

---

## Si algo se rompe

- **Las cartas se ven corridas o cortadas** → se cambió la celda de la grilla.
  El script (`CELDA`, `ESCALA`) y `BlackjackArtImporter` (`CeldaAncho`,
  `CeldaAlto`) tienen que decir lo mismo. Volvé a correr el script y el builder.
- **La pantalla no abre** → `MenuScreenId.Blackjack` tiene que ser el **último**
  valor del enum. Los prefabs guardan el id como número: meter un valor en el
  medio le cambia la identidad a las pantallas de abajo y hay que regenerarlas
  todas.
- **Un botón no responde y no dice por qué** → mirá `Habilitado()` y `PorQueNo()`.
  Todo botón apagado tiene que tener un aviso que explique el motivo.
- **Las capturas del banco muestran las cartas amontonadas** → la foto se sacó a
  mitad del vuelo. `Capturar()` espera a que no queden cartas en el aire; si
  volvés a ver eso, revisá esa espera antes de tocar el acomodo.
