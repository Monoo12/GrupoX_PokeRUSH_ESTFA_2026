# Créditos de assets de terceros

Quién hizo el arte que no es nuestro, dónde vive en el proyecto y bajo qué licencia
se usa. Si agregás assets de afuera, anotalos acá **antes** de commitearlos.

---

## Casino — mesa, rueda y fichas

- **Origen:** [mativallej/casino-roulette-game](https://github.com/mativallej/casino-roulette-game)
- **Autor:** Matías Vallejo
- **Licencia:** Creative Commons **Atribución-CompartirIgual 4.0 Internacional**
  ([CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/deed.es))
- **Dónde:** `Codigo/PokeRush/Assets/Graphics/Casino/`
  - `mesa_fieltro.png` — tapete de la mesa (de `Art/Texture/game/table/Roulette Table Flat r.png`)
  - `ruleta_aro.png` — aro de madera con borde dorado (de `Art/Texture/game/wheel fortune/Wheel-01.png`)
  - `ruleta_centro.png` — buje metálico del centro (de `.../wh2-02.png`)
  - `fichas.png` — tira de 15 fichas (de `Art/Texture/game/chips/chips-02.png`)
- **Qué les hicimos:** el arte original está exportado a 5.000–7.000 px. Se redujo
  una sola vez con filtro Lanczos al tamaño que usa el juego (512×384 de
  referencia) y se importa en Point. El buje además se recortó en círculo.

### Lo que implica CompartirIgual

CC BY-SA obliga a dos cosas: **dar crédito** (este archivo) y **licenciar la obra
derivada igual**. En la práctica, para este proyecto académico significa que las
pantallas del casino que usan este arte quedan bajo CC BY-SA 4.0. Si en algún
momento el juego se distribuye con otra licencia, hay que reemplazar estas cuatro
imágenes por arte propio o de licencia compatible — el código no depende de ellas:
se cambian los PNG de `Graphics/Casino/` y listo.

---

## Pokémon Essentials (pack de gráficos)

- **Dónde:** `Codigo/PokeRush/Assets/Graphics/` (Windowskins, UI, Tilesets,
  Characters, Trainers, Pokemon, Items…)
- **Qué usamos del pack en el casino:**
  - `Graphics/UI/Slot Machine/` — el gabinete de la tragaperras y su tira de símbolos
  - `Graphics/Windowskins/choice 18.png` — el marco dorado de las pantallas
  - `Graphics/Characters/enfermera.png` y `Lider 1 bw.png` — los NPC del piso 1
  - `Graphics/Trainers/JUGON.png` — el retrato del apostador
- **Nota:** es material de fan game basado en propiedad de Nintendo/Game Freak.
  Sirve para un trabajo académico sin fines de lucro; no se puede publicar ni
  vender el juego con estos assets.

---

## Nuestro

Todo lo de `Assets/Sprites/UI/Casino/` (círculo y aguja de la ruleta) lo genera
`CasinoPrefabBuilder` con código, y `Assets/Sprites/UI/Casino/fieltro.png` fue un
recorte que quedó sin uso al pasar al tapete del repo.
