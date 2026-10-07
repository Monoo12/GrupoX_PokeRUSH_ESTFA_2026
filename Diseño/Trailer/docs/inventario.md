# Inventario para el tráiler

Relevamiento del 5-oct-2026 (Fase 1, solo lectura). Lo que el tráiler usa del
proyecto de Unity se copia a `public/assets/` con `npm run assets`
(`scripts/copiar-assets.mjs`); esa carpeta no se versiona. En las tablas, **Unity**
es la ruta dentro de `Codigo/PokeRush/Assets/` y **Copia** es la ruta dentro de
`public/assets/`.

---

## 1. Historia

**Fuentes leídas:** `Documentacion/Documentacion del Codigo/Historia_En_Los_Niveles.md`,
`Jefe_Final_Julieta.md`, `Quarcho_Cueva_Lobby.md`, `Casino_Piso_Final.md`, los
92 diálogos de `Resources/Definitions/**/Dialogos/*.asset` y
`Resources/Definitions/Julieta/GuionDeJulieta.asset` (decodificados con un
cargador de YAML: los `\xE1` y compañía quedan como acentos).

**El gancho:** 2031. El NeuraRush-LINK (un chip en la nuca) controla a la gente y le
edita los recuerdos. El protagonista se despierta en su habitación, no figura en
ningún registro y un Fletchling le trae una carta escrita como código. Desde la
Quarcho Cueva, Tixi lo manda a voltear los servidores: el Data Center de Néstor,
el casino de cuatro pisos de Martín Reymar (con Tacconi, la ruleta y el
blackjack) y la sede de la Liga en el Ameghino, donde espera Julieta, "la
Directora".

### Frases verificadas en los assets

| Frase del brief | Texto exacto en el juego | Quién / dónde | Nota |
|---|---|---|---|
| "No confíes en la Liga" | `print("No confíes en la Liga")   print("No confíes en Julieta")` / `print("Si todavía recordás el 2026")   print("Vení a la QuarchoCueva")` | Narrador, `Tutorial/Dialogos/Tutorial_Despertar` (líneas 7 y 8) | La carta son cuatro `print` |
| "USUARIO NO ENCONTRADO EN NINGÚN REGISTRO" | `USUARIO DETECTADO. BUSCANDO CHIP... BUSCANDO CHIP...` → `ERROR: USUARIO NO ENCONTRADO EN NINGÚN REGISTRO.` | SISTEMA, `Torre1/Dialogos/DC_Zona_Pantallas` | Lleva "ERROR:" adelante |
| "Conque recibiste mi carta." | `Aaah... conque recibiste mi carta.` | **`???`**, `Lobby/Dialogos/Lobby_Tixi_Lore` (línea 0) | En el juego Tixi todavía no tiene nombre: el cartel dice `???` |
| "PROTOCOLO DE DEFENSA: LISTO." | `PROTOCOLO DE DEFENSA: LISTO.` con opciones `Pelear` / `Todavía no` | SISTEMA, pregunta antes de pelear con Néstor (`NestorGOD2.unity`, la pone `HistoriaDataCenterBuilder.cs:473`) | No es un diálogo: es la pregunta del `InteractableTrigger` |
| "RECUERDOS: SINCRONIZADOS. ÚLTIMA EDICIÓN: HACE 4 MINUTOS" | `GUARDIÁN: N. [DATO RESTRINGIDO]. ROL: DEFENSA. RECUERDOS: SINCRONIZADOS. ÚLTIMA EDICIÓN: HACE 4 MINUTOS.` | SISTEMA, `Torre1/Dialogos/DC_TerminalHall` | |
| "¿Te acordás de 2026?" | `Antes de jugar, quiero preguntarte algo. ¿Te acordás de 2026?` | TACCONI, `Casino/Dialogos/Casino_Tacconi_Presentacion` | |
| "Vos no aparecés en ninguno." | `Vos no aparecés en ninguno.` | TACCONI, mismo asset (línea 25) | |
| "Participé en tu preparación." | `Porque yo participé en tu preparación.` / `Dije que participé en tu preparación.` | REYMAR, `Casino/Dialogos/Casino_Reymar_Tramo3` | Retrato: `MARTIN.png` |
| "ACCESO DENEGADO" / "SERVIDORES RESTANTES: 01" | `ACCESO DENEGADO.` / `SERVIDORES RESTANTES: 01` | SISTEMA, `Casino/Dialogos/Casino_Reymar_Servidor` | |
| "INTRUSO DETECTADO" / "ACCESO EXCLUSIVO: JULIETA" | `ACCESO NO AUTORIZADO. INTRUSO DETECTADO.` / `SECTOR RESTRINGIDO. SERVIDOR PRINCIPAL. ACCESO EXCLUSIVO: JULIETA.` | SISTEMA, `Julieta/Dialogos/Final_Sede_Intruso` y `Final_Sede_Puerta` | |
| "De la misma forma que siempre. Peleando." | `De la misma forma que siempre.` → (narrador: `Julieta se pone frente a vos.`) → `Peleando.` | JULIETA, `GuionDeJulieta.asset` (`alEncontrarla` 21 y 23) | Son dos cajas |

Otras líneas que sirven: `FECHA DEL SISTEMA: 2031. TENÉS 1.826 CORREOS SIN LEER.`
(`Tutorial_Pc`), `Algo te chispea en la nuca.` (`Tutorial_Despertar`), `Hoy el 98,7%
de la población durmió en su cápsula.` (el noticiero, `Tutorial_Opciones`),
`"El sujeto que no figura en ningún registro se despertó. No lo busquen. Va a venir
por su cuenta. -J."` (`DC_Memo`), `Iniciando protocolo de defensa.` (`DC_Nestor_Antes`).

### Para no spoilear

Quedan **afuera**: que Julieta pierde (`finalDelJuego`, `enElServidor`, `alDestruirlo`,
`conJulietaEnElSuelo`), el epílogo, `Final_Tixi_EnLaCueva` ("Que vos estabas ahí",
la sala de 2026), la última conexión (`NOMBRE EN CLAVE: VENTRESCA AI`) y todo lo que
dice Julieta adentro de la pelea sobre Tixi.

### Una inconsistencia del juego que toca al tráiler

Tixi dice que detectó **cuatro** servidores (`Lobby_Tixi_Plan`) y la terminal del
Data Center dice `SERVIDOR 01 DE 04`, pero después del casino el juego dice
`SERVIDORES RESTANTES: 01` (o sea, tres). El tráiler usa **tres**, que es lo que el
jugador recorre y lo que cierra con el contador del juego. Está anotado en
`Historia_En_Los_Niveles.md` → "Cuántos servidores hay".

---

## 2. Visuales

**Píxel art** = paleta chica y grilla limpia: se escala solo por enteros con
`image-rendering: pixelated`. **Ilustración** = muchos colores (dibujo del equipo o
arte generado): se escala por enteros igual, pero no tiene una grilla de píxeles
exacta.

### Logos y pantallas

| Unity | Copia | Medidas | Para qué | Nota |
|---|---|---|---|---|
| `Sprites/Logos/LevelUpCreators.png` | `logos/level-up-creators.png` | 498×501, ilustración | Cierre | |
| `Sprites/Logos/IconoDelJuego.png` | `logos/icono-del-juego.png` | 512×512, ilustración | — | **Es el logo de Level Up Creators**, no el de PokeRush |
| `Sprites/Menus/pantalla incio.png` | `menus/pantalla-inicio.png` | 1672×941, ilustración estilo píxel | Flash del drop y logo final | Arte del título: dos personajes enfrentados en azul y rojo (por el pelo y la ropa parecen Tixi y Julieta), el logo PokeRush arriba y PRESS START. **Es el único lugar donde está el logo de PokeRush**: no hay un PNG suelto |
| `Sprites/Menus/fondoCampeon.png` | `menus/fondo-campeon.png` | 800×800, ilustración estilo píxel | La ciudad de noche ("El mundo") | |
| `Sprites/Menus/panels.png` | `menus/panels.png` | 408×536, 19 colores | Paleta y marcos | Grises y azul pizarra con borde turquesa |
| `Diseño/Pantallas/Pantalla-hud batalla.png` | (no se copia) | 1536×1024 | Referencia | Diseño del HUD que todavía no está en el juego |
| `Diseño/Mockups/Mockup hud batalla.png`, `MockUP pantalla incio.png`, `MockUP Menu principal-save.png` | (no se copian) | — | Referencia | |

### Personajes

| Unity | Copia | Medidas | Para qué | Nota |
|---|---|---|---|---|
| `Sprites/Nestor/NESTOR_retrato_enojado.png` | `personajes/nestor-enojado.png` | 302×301, ilustración | Montaje (Néstor) | Es el retrato de su caja de diálogo |
| `Sprites/Nestor/NESTOR_retrato_furioso.png` | `personajes/nestor-furioso.png` | 357×310, ilustración | Montaje | |
| `Sprites/Nestor/NESTOR_retrato_shock.png`, `_pensando.png` | `personajes/nestor-shock.png`, `nestor-pensando.png` | 302×313, 318×305 | Reserva | |
| `Sprites/Nestor/NESTOR_brazos_cruzados.png` | `personajes/nestor-brazos-cruzados.png` | 205×328 | Reserva | Hay 14 poses más en la carpeta |
| `Graphics/Trainers/TIXI.png` | `personajes/tixi-retrato.png` | 160×160, 30 colores | La caja "???" (en silueta) | GUID del retrato en `Lobby_Tixi_Lore` |
| `Graphics/Trainers/JULIETA.png` | `personajes/julieta-retrato.png` | 100×100, 21 colores | Silueta del clímax y su caja | GUID de `portrait` y `battleFrontSprite` en `Julieta/Julieta.asset`. La figura mide 48×92 |
| `Graphics/Characters/julieta.png` | `personajes/julieta-hoja.png` | 256×320 (4×4 de 64×80) | Reserva | Hoja del mapa |
| `Graphics/Trainers/MARTIN.png` | `personajes/reymar-retrato.png` | 160×160, 32 colores | Caja de Reymar | Retrato de `Casino_Reymar_*` |
| `Graphics/Trainers/TACCONI.png` | `personajes/tacconi-retrato.png` | 160×160, 33 colores | Caja de Tacconi | Retrato de `Casino_Tacconi_*` |
| `Sprites/Casino/tacconi_frente.png` | `personajes/tacconi-frente.png` | 30×76 | Reserva | Sprite del mapa |
| `Sprites/Player/POKEMONTRAINER_RojoNeutro_walk_2.png` | `personajes/jugador-rojo.png` | 256×256 (4×4 de 64×64) | "No figurás en ningún registro" | |
| `Sprites/Player/POKEMONTRAINER_HojaNeutra_walk_2.png` | `personajes/jugadora-hoja.png` | 256×256 | Alternativa | |
| `Sprites/Trainers/CHICO, CHICA, CIENTIFICA, ESTUDIANTA, JUGON, DAMISELA.png` | `entrenadores/*.png` | 160×160 (ESTUDIANTA 178×178) | La gente con chip | Hay 175 en la carpeta |

### Combate

| Unity | Copia | Medidas | Para qué | Nota |
|---|---|---|---|---|
| `Sprites/Battlebacks/Central_bg.png` | `combate/fondo-central.png` | 512×384, render | Fondo del combate del montaje | Sala de generadores: encaja con el Data Center |
| `Sprites/Battlebacks/Laboratorio_bg.png` | `combate/fondo-laboratorio.png` | 512×384 | Reserva | Es el fondo de la pelea de Julieta (`battleBackground`) |
| `Sprites/Battlebacks/InteriorRojo_bg.png` | `combate/fondo-interior-rojo.png` | 512×384 | Reserva | Fondo de la pelea con Reymar |
| `Sprites/Pokens/Back/LUCARIO.png` | `combate/lucario-espalda.png` | 9295×65: 143 cuadros de 65×65 | El Pokémon del jugador | Lucario es uno de los tres iniciales (con Gengar y Greninja, `CreacionPersonaje.unity`). Lucha contra Normal: súper eficaz |
| `Sprites/Pokens/Front/PORYGONZ.png` | `combate/porygonz-frente.png` | 5510×58: 95 cuadros de 58×58 | El rival | Del equipo de Néstor (Raticate, Braixen, Melmetal, Porygon-Z). Digital: encaja con el chip |
| `Sprites/Pokens/Front/MEWTWO.png` | `combate/mewtwo-frente.png` | 11250×90: 125 cuadros | Reserva (sombra en el clímax) | Del equipo de Julieta |
| `Sprites/UI/Battle/databox_normal.png`, `databox_normal_foe.png` | `combate/databox-jugador.png`, `databox-rival.png` | 260×84, 260×70 | HUD | Barra de PS en `#01FF4A` / `#00BD21` |
| `Sprites/UI/Battle/overlay_message.png` | `combate/caja-mensaje.png` | 512×96, una sola forma | Caja de mensajes **y caja de diálogo** | Es el sprite de `CajaDialogo` en `Prefabs/UI/DialogoCanvas.prefab`, teñido `rgba(0.12, 0.12, 0.15, 0.92)` |
| `Sprites/UI/Battle/ball_POKEBALL.png`, `ballBurst_star.png` | `combate/pokeball.png`, `destello-ball.png` | 256×64, 64×64 | Reserva | |
| `Graphics/Animations/PRAS- Aura Sphere.png` | `combate/fx-esfera-aural.png` | 960×192: 5 cuadros de 192×192 | Impacto de Esfera Aural | Lo usa `MoveAnimations/0396_Esfera Aural` (impacto a 15 fps, escala 4) |

`Sprites/Battle/Animations/` está **vacía**: las animaciones de ataque salen de
`Graphics/Animations/` (hojas de 192×192 cortadas en Unity) y las elige
`Resources/Definitions/MoveAnimations/`.

### Casino

| Unity | Copia | Medidas | Para qué |
|---|---|---|---|
| `Graphics/UI/Slot Machine/bg.png` | `casino/slots-maquina.png` | 512×384, 52 colores | La tragamonedas |
| `Graphics/UI/Slot Machine/images.png` | `casino/slots-simbolos.png` | 512×48: 8 símbolos de 64×48 | Rodillos (Cereza, Magnemite, Gastly, Psyduck, Pikachu, siete azul, siete rojo, Replay: `MaquinaBasica.asset`) |
| `Graphics/UI/Slot Machine/ReelOverlay.png`, `lights.png` | `casino/slots-rodillo.png`, `slots-luces.png` | 64×144, 96×104 | Rodillo y luces |
| `Ruleta Nueva/roulette_wheel.png` | `casino/ruleta-rueda.png` | 640×640, ilustración | La ruleta girando |
| `Ruleta Nueva/roulette_ball.png`, `mesa_ruleta.png` | `casino/ruleta-bola.png`, `ruleta-mesa.png` | 56×57, 645×350 | Bola y paño |
| `BlackJack God/cartas_picas.png`, `cartas_corazones.png` | `casino/cartas-*.png` | 640×85, tira de cartas | La carta de blackjack |
| `BlackJack God/reverso_rojo.png`, `mesa_blackjack.png` | `casino/carta-reverso.png`, `blackjack-mesa.png` | 105×150, 570×350 | |
| `Graphics/Casino/mesa_fieltro.png` | `casino/mesa-fieltro.png` | 512×384 | Fondo de mesa |
| `Sprites/Casino/tarjeta_vip.png` | `casino/tarjeta-vip.png` | 48×48 | Reserva |
| `Sprites/Tilesets/2D Top Down Pixel Art Tileset Casino/` | (no se copia) | 1024×512 | Reserva: el tileset de los pisos |

### La sede

| Unity | Copia | Medidas | Para qué | Nota |
|---|---|---|---|---|
| `Sprites/Tilesets/juliTILE.png` | `sede/julitile.png` | 1536×1024 | "Y la Liga" y la puerta | Hoja del tileset de la arena (tiles de 32×32): trae una arena armada de ejemplo, puertas y la pantalla grande que hace de servidor |

---

## 3. Fuentes

| Unity | Copia | Uso |
|---|---|---|
| `Fonts/power clear.ttf` | `fuentes/power-clear.ttf` | **La de la caja de diálogo** (GUID `ce613fd9…` en `DialogoCanvas.prefab`; es idéntica a `TextMesh Pro/Fonts/power clear.ttf`). 15 px en el Canvas de 512×384, interlineado 1,6 |
| `Fonts/power clear bold.ttf` | `fuentes/power-clear-bold.ttf` | Títulos y textos grandes (la usan los títulos de los créditos) |
| `Fonts/power green.ttf` | `fuentes/power-green.ttf` | Texto de sistema / terminal |

---

## 4. Audio

### Temas analizados (librosa, `scripts/detectar-beats.py`)

| Tema | Unity | Duración | BPM | Cómo es |
|---|---|---|---|---|
| **Luffy vs. Kizaru (My Peak), Epic Version** | `Audio/Pelea contra julieta/Fase 1/` | 145,2 s | **143,6** | Golpe enorme en 1,79 s; dos "builds" de 16 beats a −23 dB (2,2–8,6 s y 71,1–77,9 s), cada uno termina en la entrada de la banda completa; bajón en 100,2–102,8 s; sección más fuerte de 102,8 a 137,5 s. Es la música de la fase 1 de Julieta |
| One Piece - Big Fight | `Audio/Peleas Random/` | 92,8 s | 103,4 | Pared de sonido pareja (−15 dB todo el tema); sin un build claro |
| Whitebeard vs. Blackbeard | `Audio/Peleas Random/` | 75,1 s | 172,3 | Muy rápido y parejo |
| Three Sword Style! Dragon Twister! | `Audio/Sala BossFinal_Julieta/` | 65,8 s | 117,5 | Parejo, con un bajón en 25 s |
| Musica_MenuPrincipalySave | `Audio/` | 293,5 s | 95,7 | Masterizado muy fuerte (−9,5 dB); filtrado sirve de cama "a través de una pared" |

Otros que no se usan: `Pelea contra julieta/Fase 2` (44 s), `Fase Final casi por
caer` (361 s), `Pelea_Nestor.mp3` (147 s), los del casino, `After-vencer-Julieta`
(es el tema del final: no va).

### Efectos

| Unity | Copia | Duración | Para qué |
|---|---|---|---|
| `Audio/Efectos/Final/alarma.ogg` | `sfx/alarma.ogg` | 0,35 s | INTRUSO DETECTADO (en loop, al beat) |
| `Audio/Efectos/Final/chip_apagado.ogg` | `sfx/chip-apagado.ogg` | 0,36 s | El chispazo en la nuca |
| `Audio/Efectos/Final/nucleo_destruido.wav` | `sfx/nucleo-destruido.wav` | 1,51 s | Contador de servidores |
| `Audio/Efectos/Final/puerta_se_abre.ogg` | `sfx/puerta-se-abre.ogg` | 0,86 s | ACCESO EXCLUSIVO: JULIETA |
| `Audio/Efectos/Final/transmisor.ogg` | `sfx/transmisor.ogg` | 0,45 s | Textos del sistema |
| `Audio/Efectos/Combate/golpe_super_eficaz.ogg` | `sfx/golpe-super-eficaz.ogg` | 1,78 s | El golpe del combate |
| `Audio/Efectos/Combate/debilitado.ogg` | `sfx/debilitado.ogg` | 0,81 s | Porygon-Z cae |
| `Audio/Efectos/Combate/lanzar_ball.ogg` | `sfx/lanzar-ball.ogg` | 1,09 s | Reserva |
| `Audio/Efectos/Combate/estado_paralizado.wav` | `sfx/chispazo.wav` | 1,22 s | Glitch eléctrico de los chips |
| `Audio/Combate/Ataques/aura-sphere_1.wav` | `sfx/esfera-aural.wav` | 1,20 s | El ataque de Lucario |
| `Audio/Combate/Gritos/0448_Lucario.ogg`, `0474_Porygon-Z.ogg`, `0150_Mewtwo.ogg` | `sfx/grito-*.ogg` | 0,66 / 1,56 / 1,60 s | Gritos (hay 440) |
| `Audio/Efectos/Casino/slots_frena.ogg`, `slots_moneda.ogg`, `premio_grande.ogg` | `sfx/slots-*.ogg`, `premio-grande.ogg` | 1,57 / 1,55 / 5,08 s | La tragamonedas |
| `Audio/Efectos/Ruleta NegroRojo girando/…mp3` | `sfx/ruleta-girando.mp3` | 8,31 s | La ruleta |
| `Audio/Efectos/Taking Card Poker/…taking-playing-card-2…mp3` | `sfx/carta.mp3` | 0,50 s | La carta |
| `Audio/Efectos/Menus/menu_cursor.ogg` | `sfx/menu-cursor.ogg` | 0,22 s | Tipeo letra por letra y stinger |
| `Audio/Efectos/Menus/menu_error.ogg`, `menu_abrir.ogg`, `pc_apagar.ogg` | `sfx/menu-*.ogg`, `pc-apagar.ogg` | 0,35 / 0,54 / 0,36 s | ERROR, ACCESO DENEGADO, contador |
| `Audio/Efectos/Seleccion en el menu (X)/tmpq91k5v_6.mp3` | `sfx/seleccion.mp3` | 2,06 s | Elegir "Pelear" |

Varios `.ogg` del pack vienen a 65 456 Hz; Chromium los decodifica igual.

### Audios derivados (los arma `npm run assets` con ffmpeg)

| Copia | De dónde sale |
|---|---|
| `musica/menu-apagado.wav` | Los primeros 16 s del tema del menú, pasa-bajos a 420 Hz (dos veces) |
| `sfx/golpe-kizaru.wav` | El primer golpe del tema de Kizaru (1,70–3,00 s), con fade |
| `sfx/zumbido.wav` | Sintetizado: 50 Hz + armónicos + ruido rosa filtrado, 8 s |
| `sfx/glitch.wav` | Sintetizado: ruido blanco a 4 bits con trémolo, 0,45 s |

---

## 5. Lo que no hay

- **Capturas del juego:** no existe `Diseño/Trailer/capturas/`. La lista de tomas
  para grabar con OBS está en `docs/diseno.md`.
- **Logo de PokeRush suelto:** solo está dentro de `pantalla incio.png`. Si el
  equipo tiene el logo en PNG con transparencia, alcanza con soltarlo en el
  proyecto de Unity y sumarlo a `copiar-assets.mjs`.
- **Arte de la sede** propio: la arena de Santiago usa `juliTILE.png`.
- `Sprites/Menus/menu_bg.png` dice ser PNG de 512×384 pero Pillow no lo puede abrir
  (no se usa).
