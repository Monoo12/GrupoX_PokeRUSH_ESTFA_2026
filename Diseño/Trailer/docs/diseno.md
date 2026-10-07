# Diseño del tráiler

**Estado: aprobado el 5-oct-2026 y construido** (ver la sección 8, "Lo que cambió
al construir"). Las secciones 1 a 7 son el diseño que se aprobó.

- **Duración:** 60,5 s (1815 frames a 30 fps), 1920×1080.
- **Música:** *Luffy vs. Kizaru (My Peak), Epic Version* (la de la fase 1 de Julieta),
  143,55 BPM, con el tema del menú filtrado como cama del arranque.
- **Voz:** **Jorge (Loquendo)**, generada por Augusto en `voz/manual/` (ver la
  sección 8).
- **Datos que mandan en el timeline:** `public/beats.json` (beats del tema, ya en
  segundos y frames del tráiler) y `public/voz/duraciones.json` (medido con ffprobe).
  Cada plano se define por el beat donde empieza; cada línea de voz, por el beat o
  el momento donde entra. Nada de segundos sueltos en el código.

---

## 1. Música

### Por qué este tema

Los cinco candidatos son "pared de sonido": masterizados fuerte y parejos. Lo que
decide es la **estructura**, y Kizaru tiene justo lo que pide el tráiler:

- un **build** de 16 beats a −23 dB (71,09–77,43 s) que desemboca en la **entrada de
  la banda completa** en 77,85 s (beat 180);
- desde ahí, una sección de **22 s** casi exacta hasta un **bajón** (100,24–101,5 s),
  o sea, el largo del montaje;
- después del bajón, el golpe más fuerte de esa zona (beat 236, 101,5 s) y una
  sección todavía más intensa: el clímax;
- **compases regulares** (el "uno" cae en los beats múltiplos de 4: se ve en los
  graves) y tempo firme (desvío de 9 ms entre beats).

Además es la música de la pelea contra Julieta: el tráiler termina en ella.

| Candidato | BPM | Por qué no |
|---|---|---|
| Big Fight | 103,4 | Parejo de punta a punta (−15 dB), sin build ni bajón |
| Whitebeard vs. Blackbeard | 172,3 | Parejo y muy rápido para cortar al beat sin marear |
| Three Sword Style (sala de Julieta) | 117,5 | Parejo; su único bajón (25 s) es corto |
| Menú principal | 95,7 | Masterizado a −9,5 dB: sirve filtrado, como cama |

### El empalme (una sola pista de Kizaru, sin cortes adentro)

| Tramo | Tráiler | Qué suena |
|---|---|---|
| Cold open | 0,00–5,20 | Sin música: zumbido eléctrico y efectos |
| Cama | 5,20–13,74 | `menu-apagado.wav` (el tema del menú, pasa-bajos a 420 Hz), entra con fade de 1,5 s |
| Build | 13,74–20,08 | Kizaru desde 71,09 s (beats 164–178). La cama sale en 0,3 s debajo del glitch |
| Silencio | 20,08–20,50 | Se silencia el beat 179: el "medio segundo" de negro (0,42 s, un beat) |
| Drop | 20,50 | Beat 180 (77,85 s del tema): entra la banda completa |
| Corte seco | 51,77–52,63 | Se silencian los beats 254–255 y el tema vuelve en el 256 con el logo |
| Fade out | 56,50–58,50 | El stinger queda en silencio, solo con `menu_cursor` |

Como el tema corre de corrido desde 13,74 s, todo es automatización de volumen
(silencios, ducking de −10 dB debajo de cada línea de voz y de los golpes de texto,
fade): no hay empalmes adentro de la música que se puedan notar. El golpe del
arranque del tema (1,79 s, el más fuerte de todo el archivo) se usa suelto como
efecto en el chispazo (5,00 s) y en el logo (52,63 s).

**Lo que no pude hacer:** escucharlo. Los puntos salen del análisis; si al oírlo
conviene otro beat de drop, se cambian `BEAT_DROP`/`DROP_EN_TRAILER` en
`scripts/detectar-beats.py` y `npm run beats` rehace todo el JSON.

---

## 2. Voz

### Qué hay instalado (paso 1)

| API | Voces |
|---|---|
| SAPI (`System.Speech`) | Microsoft Sabina Desktop (es-MX, F), Microsoft Zira Desktop (en-US, F) |
| OneCore (WinRT, `Windows.Media.SpeechSynthesis`) | Microsoft **Raúl** (es-MX, M), Microsoft Sabina (es-MX, F) |

Ninguna Loquendo. No están Helena ni Pablo. Raúl es la única voz masculina en
español, la más cercana a Jorge.

### Las tres variantes (misma línea: "Pero vos... no figurás en ningún registro.")

| | Archivo | Voz | Procesado |
|---|---|---|---|
| **A** (recomendada) | `public/voz/variantes/A-loquendo-clasico.wav` (2,41 s) | Raúl, velocidad 1,08 | Banda 110 Hz–7,2 kHz, +4 dB en 2,4 kHz (nasal), −3 dB en 350 Hz, remuestreo a 16 kHz y vuelta, compresión 4:1, bitcrush a 12 bits al 25 % |
| **B** | `public/voz/variantes/B-robot-telefonico.wav` (2,59 s) | Raúl, tono 0,9 | Banda telefónica 300–3400 Hz, 11 kHz, bitcrush a 8 bits al 50 %, eco de 7 ms (metálico), compresión 6:1 |
| **C** | `public/voz/variantes/C-sabina-grave.wav` (2,51 s) | Sabina | Bajada de tono a 0,84 (sin preservar formantes: suena sintética) + el procesado de A |

Las tres están recortadas, a 48 kHz, con pico en −1 dBFS y la pausa de los "..."
(0,35 s, hecha con SSML). Los perfiles viven en `voz/guion.json`: cambiar de
variante es cambiar `"perfil"` y correr `npm run voz`.

**Reemplazo manual:** `public/voz/manual/NN.wav` manda sobre el generado (el script
no lo pisa y mide esa duración). Para grabar con un Loquendo real, alcanza con
soltar ahí los WAV y volver a correr `npm run voz`.

### Guion de la voz (duraciones medidas con la variante A)

| Línea | Texto | Dura | Entra | Ancla |
|---|---|---|---|---|
| 01 | Año dos mil treinta y uno. | 1,44 s | 3,00 | después del glitch a "2031." |
| 02 | Un chip controla a todos. | 1,46 s | 5,60 | la ciudad |
| 03 | Decide qué recordás... y qué no. | 1,84 s | 7,80 | los entrenadores con chip |
| 04 | Pero vos... no figurás en ningún registro. | 2,41 s | 10,40 | el jugador escaneado (termina 12,81, antes del ERROR) |
| 05 | Tres servidores. | 1,05 s | 22,20 | beat 184 |
| 06 | Un Data Center. | 0,91 s | 25,57 | beat 192 (el TTS lee "Un deita sénter") |
| 07 | Un casino. | 0,69 s | 32,33 | beat 208 |
| 08 | Y la Liga. | 0,56 s | 40,78 | beat 228 |
| 09 | Ella te está esperando. | 1,24 s | 47,54 | beat 244 |
| 10 | PokeRush. | 0,55 s | 53,04 | beat 257, después del golpe del logo (el TTS lee "Pouk Rach") |
| 11 | Por dos mil veintiséis. | 1,38 s | 54,31 | beat 260 |

Entre 14 y 20 s no hay voz: hablan la carta y la caja de diálogo.

---

## 3. Guion de planos

Tiempos en segundos del tráiler (frame entre paréntesis). "b" = beat del tema
(índice de `beats.json`); los marcados en negrita son "uno" de compás.

| # | Tiempo | Bloque | Plano / qué se ve | Texto en pantalla | Voz | Sonido | Entra con |
|---|---|---|---|---|---|---|---|
| 1 | 0,00–2,30 (0–69) | Cold open | Negro con scanlines. A los 0,5 s aparece el cursor de bloque; se tipea "2026." (una letra cada 3 frames desde 0,80) | `2026.` | — | `zumbido` (fade in 0,4 s), `menu_cursor` por letra | Negro |
| 2 | 2,30–3,00 (69–90) | Cold open | Glitch: RGB split, franjas corridas, los dígitos se revuelven y quedan en "2031." | `2031.` | — | `glitch`, `chispazo` corto | Glitch |
| 3 | 3,00–4,60 (90–138) | Cold open | "2031." quieto, el cursor parpadea | `2031.` | 01 | zumbido | — |
| 4 | 4,60–5,20 (138–156) | Cold open | **El chispazo en la nuca:** la espalda del jugador (`jugador-rojo`, fila de espaldas, ×6) sale de la oscuridad; arcos eléctricos en la nuca; flash blanco en 5,00 | — | — | `chispazo`, `chip_apagado` + `golpe-kizaru` en 5,00 | Corte |
| 5 | 5,20–7,60 (156–228) | El mundo | La ciudad de noche (`fondo-campeon` ×3), paneo vertical lento del cielo al skyline (de a píxel entero). Panel de sistema arriba a la izquierda con barra de progreso | `NeuraRush-LINK: SINCRONIZANDO…` → `98,7 %` | 02 | cama del menú, `transmisor` al aparecer el panel | Flash |
| 6 | 7,60–10,10 (228–303) | El mundo | Sobre la ciudad, cinco entrenadores (×2) en fila; a cada uno le titila un LED rojo en la nuca y le pasa un glitch, uno atrás del otro | `RECUERDOS: SINCRONIZADOS.` / `ÚLTIMA EDICIÓN: HACE 4 MINUTOS` | 03 | `chispazo` bajito en cada glitch | Corte |
| 7 | 10,10–13,74 (303–412) | El mundo | La ciudad se apaga; el jugador de frente (×5), solo, con una línea de escaneo turquesa. En 12,90: todo en rojo | `USUARIO DETECTADO. BUSCANDO CHIP... BUSCANDO CHIP...` → `ERROR: USUARIO NO ENCONTRADO EN NINGÚN REGISTRO.` | 04 | `menu_error` + `glitch` en 12,90 | Corte |
| 8 | 13,74–17,97 (412–539) | La carta | Negro de terminal. Los cuatro `print`, uno en cada beat par (b164, 166, 168, 170); en b172 el primero se invierte y el resto se apaga | `print("No confíes en la Liga")` `print("No confíes en Julieta")` `print("Si todavía recordás el 2026")` `print("Vení a la QuarchoCueva")` | — | Entra el build. `menu_cursor` cada dos letras | Glitch |
| 9 | 17,97–20,08 (539–602) | La carta | La caja de diálogo del juego sobre negro; en el retrato, Tixi en silueta | **???**: `Aaah... conque recibiste mi carta.` | — | `menu_abrir`, `menu_cursor` por letra | Corte en b174 |
| 10 | 20,08–20,50 (602–615) | Silencio | Negro total | — | — | Silencio total (beat 179 muteado) | Corte seco |
| 11 | 20,50–22,20 (615–666) | Drop | Flash blanco de 3 frames y la pantalla de título entera (los dos personajes en azul y rojo, el logo, PRESS START) a ×1, con sacudida y RGB split que se apaga en medio segundo | (el logo del arte) | — | **Drop** (b180) | Flash |
| 12 | 22,20–25,57 (666–767) | Montaje | La red: la ciudad oscura con grilla; tres nodos que se encienden en b184, b186 y b188, unidos por líneas turquesa; en b190 los tres laten en rojo | `SERVIDOR 01 · DATA CENTER` `SERVIDOR 02 · CASINO` `SERVIDOR 03 · SEDE DE LA LIGA` | 05 | `transmisor` por nodo | Corte (b184) |
| 13 | 25,57–27,27 (767–818) | Montaje | El fondo de la sala de generadores (×4) y la caja de SISTEMA con la ventanita de opciones; el cursor arranca en "Todavía no" (como en el juego), pasa a "Pelear" en b194 y confirma en b195 | **SISTEMA**: `PROTOCOLO DE DEFENSA: LISTO.` / `Pelear` · `Todavía no` | 06 | `menu_cursor`, `seleccion` | Corte (b192) |
| 14 | 27,27–28,11 (818–843) | Montaje | Néstor: retrato enojado (b196) y furioso (b197), ×2, con el LED del chip latiendo | `NÉSTOR` | — | `chispazo` | Corte |
| 15 | 28,11–32,33 (843–970) | Montaje | **Combate reconstruido:** fondo de generadores ×4, Lucario de espaldas ×8, Porygon-Z de frente ×8, databoxes ×3. b198: sale el ataque; b200: impacto de Esfera Aural, flash y sacudida, la barra de PS baja de 100 % a 9 % (verde → amarillo → rojo); b204: Porygon-Z se debilita | `¡LUCARIO usó Esfera Aural!` → `¡Es súper eficaz!` → `¡PORYGON-Z se debilitó!` | — | `esfera-aural`, **`golpe_super_eficaz` en b200**, grito de Porygon-Z y `debilitado` en b204 | Corte (b198) |
| 16 | 32,33–34,87 (970–1046) | Montaje | Casino en cortes de dos beats: tragamonedas (×2) que frena en 7-7-7 (b208), ruleta girando (b210), carta de blackjack que se da vuelta: as y rey (b212) | `BLACKJACK` | 07 | `slots_frena`, `slots_moneda`, `ruleta-girando`, `carta` (−8 dB debajo de la voz) | Corte (b208) |
| 17 | 34,87–36,56 (1046–1097) | Montaje | Caja de TACCONI sobre la mesa de blackjack apagada | **TACCONI**: `¿Te acordás de 2026?` | — | `menu_cursor` por letra | Corte (b214) |
| 18 | 36,56–39,10 (1097–1173) | Montaje | Caja de REYMAR sobre el fondo rojo de su pelea | **REYMAR**: `Porque yo participé en tu preparación.` | — | `menu_cursor` por letra | Corte (b218) |
| 19 | 39,10–39,94 (1173–1198) | Montaje | Sello rojo a pantalla completa con glitch | `ACCESO DENEGADO.` | — | `menu_error`, `glitch` | Glitch (b224) |
| 20 | 39,94–40,78 (1198–1223) | Montaje | Contador grande; el dígito cae de 03 a 02 en b227 | `SERVIDORES RESTANTES: 03` → `02` | — | `pc_apagar` por cambio | Corte (b226) |
| 21 | 40,78–42,90 (1223–1287) | Montaje | El contador marca 01 en rojo y debajo aparece la sede (la arena de `juliTILE` ×2, teñida de rojo, paneo de a píxel) | `SERVIDORES RESTANTES: 01` / `SEDE CENTRAL DE LA LIGA · AMEGHINO` | 08 | `nucleo_destruido` en b228 | Corte (b228) |
| 22 | 42,90–44,16 (1287–1325) | Montaje | El bajón del tema: la sede se apaga por escalones; queda un LED rojo que titila dos veces | — | — | (el bajón de la música) | Fundido escalonado |
| 23 | 44,16–45,86 (1325–1376) | Clímax | **Alarma:** pulsos rojos a pantalla completa en cada beat, banda de texto al centro, sacudida en b236 | `ACCESO NO AUTORIZADO.` / **`INTRUSO DETECTADO.`** | — | `alarma` en cada beat | Corte (b236) |
| 24 | 45,86–47,54 (1376–1426) | Clímax | La puerta (tiles de `juliTILE` ×4): las dos hojas se corren de b241 a b243 y entra luz roja | `SECTOR RESTRINGIDO. SERVIDOR PRINCIPAL.` / **`ACCESO EXCLUSIVO: JULIETA.`** | — | `puerta_se_abre` en b241 | Corte (b240) |
| 25 | 47,54–49,23 (1426–1477) | Clímax | **La silueta de Julieta** (`JULIETA.png` ×8, negra con contorno rojo de un píxel); solo brillan los anteojos, en b246 | — | 09 | grito de Mewtwo muy bajo en b247 (opcional) | Corte (b244) |
| 26 | 49,23–50,93 (1477–1528) | Clímax | Caja de JULIETA (su retrato ×2) sobre la silueta apagada | **JULIETA**: `De la misma forma que siempre.` | — | `menu_cursor` por letra | Corte (b248) |
| 27 | 50,93–51,77 (1528–1553) | Clímax | La misma caja: el texto cambia de golpe en el "uno"; glitch rojo en el retrato | **JULIETA**: `Peleando.` | — | (el golpe del compás) | Corte de texto (b252) |
| 28 | 51,77–52,63 (1553–1579) | Clímax | **Corte seco:** negro | — | — | Silencio (beats 254–255 muteados) | Corte seco |
| 29 | 52,63–55,99 (1579–1680) | Cierre | **El logo:** recorte del logo de la pantalla de título a ×2; entra con "pops" de escala entera (×4 → ×3 → ×2, un frame cada uno), flash y sacudida. En b260 aparece "Por 2026." debajo | `Por 2026.` | 10 y 11 | `golpe-kizaru` + la música que vuelve en b256 | Flash (b256) |
| 30 | 55,99–58,50 (1680–1755) | Cierre | Logo de Level Up Creators, la línea de plataforma y el aviso legal abajo, chico | `Windows · 2026 · ITFA` / *Fan game académico sin fines de lucro. Música: One Piece OST. Pokémon © Nintendo / Game Freak* | — | Fade out de la música (56,5–58,5) | Corte (b264) |
| 31 | 58,50–60,50 (1755–1815) | Stinger | Negro. El cursor de opción (▶) parpadea y se tipea "¿Continuar?"; queda parpadeando hasta el final | `¿Continuar?` | — | Solo `menu_cursor` | Corte |

### Sin spoilers

Julieta aparece como silueta, en una caja de diálogo y nombrada por el sistema:
nunca pierde ni se la ve vencida. No están el final en la cueva, `VENTRESCA AI`, la
sala de 2026 ni nada de lo que se sabe de Tixi al final. La pantalla de título
es lo primero que ve cualquier jugador.

---

## 4. Dirección visual

### Paleta (sacada de `panels.png` y del HUD)

| Uso | Colores |
|---|---|
| Fondos y paneles | `#181818` `#202020` `#282828` `#303030` `#404040` `#606060` |
| Azul pizarra (los "celestes" de los paneles) | `#283848` `#304050` `#384858` `#405060` `#586878` `#607078` |
| Acento turquesa (borde del panel seleccionado) | `#38F8C8` `#18B8A0` `#007870` |
| Texto | `#E8E8E8` (principal), `#A8A8A8` (secundario) |
| Barra de PS (del databox) | `#01FF4A` / `#00BD21`, y amarillo y rojo al bajar |
| Alarma | `#F82838` (pulso), `#A01020` (banda), `#500810` (fondo) |

Turquesa = el sistema y la red (lo que controla); rojo = alarma, error y Julieta.

### La caja de diálogo, calcada del juego

`Prefabs/UI/DialogoCanvas.prefab`: Canvas de referencia de 512×384 que el juego
agranda **×3,75** a 1080p. La caja (`CajaDialogo`) es `overlay_message.png` teñido
`rgba(31, 31, 38, 0.92)`, de todo el ancho y 96 de alto → **1920×360** abajo de
todo; el texto en `power clear`, blanco, 15 px de referencia (≈56 px) con
interlineado 1,6; el retrato a la izquierda. Diferencia a propósito: el retrato va a
**×2** (320 px) y no a ×1,6875 como en el juego, para que no se deforme el píxel. El
texto se tipea letra por letra con `menu_cursor` por letra (sin sonar en espacios).

### Píxel art

- `image-rendering: pixelated` y escalas **enteras** siempre: fondos de combate ×4,
  Pokémon ×8 (2:1 con el fondo, como en el juego), HUD ×3, retratos ×2, la silueta de
  Julieta ×8, la ciudad ×3, el título a ×1.
- **Sin zooms suaves** sobre píxel art: los cambios de tamaño son "pops" de un
  entero a otro, y los paneos y las sacudidas se redondean a píxeles enteros.
- La ruleta, las cartas y los retratos de Néstor son ilustraciones de muchos
  colores: pueden girar (la ruleta) pero se muestran a escala entera igual.

### Glitch

Hecho con CSS y `@remotion/noise`, sin shaders:

- **RGB split:** dos copias del plano en rojo y cian con `mix-blend-mode: screen`,
  corridas ±N px; N sale de `noise2D` y de una envolvente que decae en ~0,5 s después
  de cada golpe.
- **Franjas:** 6 a 10 bandas horizontales (`clip-path`) corridas en X con `noise3D`
  durante 2 a 4 frames.
- **Scanlines** fijas en todo el tráiler: una línea oscura de 2 px cada 4 px al 12 %.
- **Nieve** en los cortes con glitch: bloques de 8×8 que se prenden con `noise2D`.

### Tipografía

| Fuente | Para qué |
|---|---|
| `power clear bold` | Golpes de texto grandes (INTRUSO DETECTADO, ACCESO EXCLUSIVO, Por 2026., ¿Continuar?) |
| `power green` | Sistema y terminal (2026./2031., SINCRONIZANDO, los `print`, la red, el contador) |
| `power clear` | La caja de diálogo, como en el juego |

Los tamaños van en múltiplos de la grilla de cada fuente (la mido en la Fase 3), así
el texto también queda nítido.

---

## 5. Tomas para grabar con OBS

No hay `capturas/`. Cada plano de la tabla busca su clip en
`Diseño/Trailer/capturas/` (`npm run assets` lo copia a `public/assets/capturas/`):
si está, usa el clip; si no, la reconstrucción con sprites.

**Cómo grabar:** la build de Windows (no el editor: algunas cosas, como las
paredes rojas, solo se ven en la build) a 1920×1080, OBS a 1080p60, captura de
ventana o de juego, sin cursor del mouse. El audio del juego no se usa (el tráiler
tiene su música y sus efectos). De 3 a 6 s cada toma, empezando medio segundo
antes de la acción.

| Archivo | Escena | Qué hacer | Plano |
|---|---|---|---|
| `01-titulo.mp4` | Menú principal | La pantalla de título quieta, con PRESS START parpadeando | 11 |
| `02-carta.mp4` | `habitacion` (partida nueva) | Despertar hasta que aparece la carta con los `print` | 8 |
| `03-tixi.mp4` | `Quarcho-Cueva` (primera vez) | Tixi se acerca caminando y dice "Aaah... conque recibiste mi carta." | 9 |
| `04-pantallas.mp4` | `NestorGOD2` | Entrar al salón: parpadean las pantallas y sale "USUARIO NO ENCONTRADO EN NINGÚN REGISTRO" | 7 |
| `05-protocolo.mp4` | `NestorGOD2` | Hablarle a Néstor hasta "PROTOCOLO DE DEFENSA: LISTO." y elegir Pelear | 13 |
| `06-combate.mp4` | Pelea con Néstor, con Lucario | Esfera Aural contra Porygon-Z: el golpe súper eficaz y la barra que baja | 15 |
| `07-tragamonedas.mp4` | `Casino Nivel 1` | Una tirada de la tragamonedas de punta a punta | 16 |
| `08-ruleta.mp4` | `Casino Nivel 2` | La mesa de ruleta girando | 16 |
| `09-tacconi.mp4` | `Casino nivel 3` | Tacconi: "¿Te acordás de 2026?" y una mano de blackjack | 16 y 17 |
| `10-reymar.mp4` | `Casino piso FINAL` | El pasillo de guardias y el tramo 3: "participé en tu preparación" | 18 |
| `11-acceso-denegado.mp4` | `Casino piso FINAL`, después de ganarle a Reymar | La computadora: ACCESO DENEGADO … SERVIDORES RESTANTES: 01 | 19 a 21 |
| `12-intruso.mp4` | `BossFinal` | Entrar al edificio: INTRUSO DETECTADO y los pulsos rojos | 23 |
| `13-puerta.mp4` | `BossFinal` | La puerta: ACCESO EXCLUSIVO: JULIETA, y se abre | 24 |
| `14-julieta.mp4` | `BossFinal` | Acercarse a Julieta hasta "Peleando." **y cortar ahí**: nada de la pelea ni del final | 25 a 27 |
| `15-ruleta-combate.mp4` | Pelea con Reymar | La ruleta de combate apareciendo en el turno 2 | Reserva |

---

## 6. Decisiones que tomé (revisar)

1. **La caja de la carta dice `???`, no `TIXI`:** es como está en el juego (Tixi
   todavía no se presentó) y suma misterio. El retrato va en silueta. Si preferís
   TIXI con su cara, es un cambio de texto.
2. **"Tres servidores"** aunque Tixi diga "cuatro" (ver `inventario.md`): el
   contador del juego termina en 01 después de dos, y la red del plano 12 muestra
   tres.
3. **El drop cae en 20,50 s** (no en 22): es lo que dan el build y el beat
   silenciado. El montaje arranca en 22,20, el clímax en 44,16 y el logo en 52,63.
   Total 60,5 s.
4. **El combate es Lucario contra Porygon-Z:** Lucario es inicial y Lucha es súper
   eficaz contra Normal; Porygon-Z es de Néstor y es digital, como el chip.
5. **El logo sale de la pantalla de título**, porque no hay otro. Va con el fondo
   azul y rojo del arte, recortado.
6. **Reymar dice la línea entera** ("Porque yo participé en tu preparación.") y
   Tacconi solo la pregunta ("¿Te acordás de 2026?"), que es el final de su línea.
7. **Pronunciación:** el TTS lee "Data Center" y "PokeRush" escritos como suenan
   ("deita sénter", "Pouk Rach"); en `voz/guion.json` queda el texto real.
8. **Paquetes:** además de los cinco de la base, sumé `@remotion/media` (es el
   `<Audio>`/`<Video>` actual que recomienda la skill de Remotion). No hace falta
   ninguno más.

## 7. Construcción

Hecha: ver la sección 8 y el `README.md`.

## 8. Lo que cambió al construir (Fase 3)

### Voz

- **Jorge (Loquendo), generado por Augusto.** Los 11 WAV están en `voz/manual/`
  (16 kHz estéreo, con la frase que él eligió en cada caso). `npm run voz` los
  limpia hacia `public/voz/`: recorta el medio segundo de silencio que agrega
  Loquendo al final, deja en 0,3 s las pausas internas más largas (la de
  "recordás..." duraba 0,53 s), pasa a 48 kHz mono y le da una compresión suave
  (3:1): venía 5 dB más baja que la voz C y quedaba tapada por la música.
- Antes de eso se probó usar Jorge desde el script (perfil `J`, motor `sapi32`):
  la primera instalación (`LQJorge`) no leía el texto (toda oración daba el mismo
  clip de 4,6 s: un Loquendo sin licencia) y la de Loquendo TTS 7 (`LTTS7Jorge`)
  devuelve E_FAIL al escribir a archivo por SAPI. El perfil quedó por si se
  arregla, y el script frena si una voz devuelve el mismo audio para líneas
  distintas.
- Duraciones (limpias): 01 1,55 s · 02 1,48 · 03 3,48 · 04 2,50 · 05 0,98 ·
  06 1,01 · 07 0,69 · 08 0,53 · 09 1,76 · 10 0,66 · 11 1,72. Jorge habla más
  lento que la voz C, así que el arranque se apretó: la línea 01 entra apenas
  queda "2031." (2,8 s), los respiros entre líneas son de 4 a 6 frames y el ERROR
  del Data Center entra en 12,8 s. La línea 09 entra un beat antes (beat 243),
  sobre el final de la puerta, y cruza el corte a la silueta.

### Clips (los 14 de `capturas/`)

Cada momento se midió cuadro a cuadro (cuándo cambia la caja, cuándo pega el
golpe) y está en `src/datos/capturas.ts`.

| Plano | Clip | Desde | Nota |
|---|---|---|---|
| 7 · Usuario no encontrado | `04-pantallas` | 3,57 s | El ERROR del clip (6,07 s) cae cuando termina la voz 04 |
| 8 · La carta | `02-carta` | 1,20 s | Las dos cajas de `print` del juego |
| 9 · Caja ??? | `03-tixi` | 1,60 s | La caja entra a los 4 frames del corte |
| 11 · Drop | `01-titulo` | 0 | Con flash, sacudida y RGB split |
| 13 · Protocolo de defensa | `05-protocolo` | 16,80 s | Termina justo antes del flash de entrada al combate |
| 15 · Combate | `06-combate` | 2,93 s | El golpe del Palmeo (3,80 s del clip) cae en el beat 200. **Un solo tramo**: más adelante el databox del juego ya dice "Raticate" con Porygon-Z en pantalla |
| 16 · Casino | `07`, `08`, `09` | 1,4 / 11,3 / 18,2 s | Rodillos girando, la ruleta, el reparto de blackjack |
| 17 · Tacconi | `09-tacconi` | 8,70 s | "Antes de jugar, quiero preguntarte algo. ¿Te acordás de 2026?" |
| 18 · Reymar | `10-reymar` | 13,13 s | "Porque yo participé en tu preparación." |
| 20 · Contador | `11-acceso-denegado` | 2,5 s | De fondo, oscurecido. **El clip no muestra ACCESO DENEGADO** (es la desconexión de Reymar), así que el plano 19 sigue reconstruido |
| 23 · Intruso | `12-intruso` | 0 | Más los pulsos rojos al beat encima |
| 24 · Acceso exclusivo | `13-puerta` | 2,10 s | |
| 25 · Julieta | `14-julieta` | 0 | Silueta (reconstruida) en los beats 244-245 y el clip en 246-247 |
| 26-27 · La caja de Julieta | `14-julieta` congelado en 0,95 s | | El clip no tiene "De la misma forma que siempre.": la caja se arma calcada sobre el cuadro quieto |

Siguen **reconstruidos**: 1-6 (cold open, ciudad, gente con chip), 10, 12 (red de
servidores), 14 (Néstor), 19 (ACCESO DENEGADO), 21-22 (la sede y el bajón), 25a (la
silueta), 26-27 (la caja), 28-31 (cierre y stinger). La composición
`Trailer-Reconstruccion` muestra todos los planos reconstruidos.

### Otros ajustes

- **Combate:** el juego dice "¡Lucario usó Palmeo!" y "¡Es súper efectivo!" (no
  "eficaz"); la reconstrucción usa los mismos textos y el efecto de Palmeo.
- **Anteojos de Julieta:** brillan en el beat 245 (en el 246 ya corta al clip).
- **Logo de Level Up Creators:** va sobre una placa blanca (es azul sobre blanco;
  sobre negro no se veía), como en los créditos del juego.
- **Render de cuadros sueltos:** `remotion render --frames=` con una lista salteada
  falla con `EINVAL` en el mezclador de audio de Remotion; `scripts/stills.mjs` los
  renderiza con un solo bundle y un solo navegador.
- La caja de diálogo se midió sobre los clips: banda de 360 px desde y=720, nombre
  en x=309 (línea de base 795), texto en 871, `power clear` a 52 px (4 píxeles de
  pantalla por píxel de la letra; el juego la dibuja a 56,25).
