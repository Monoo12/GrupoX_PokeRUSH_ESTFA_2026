# Tráiler de PokeRush

Tráiler de ~60 s (1920×1080, 30 fps) hecho con [Remotion](https://www.remotion.dev).
El diseño (guion de planos, voz, música, dirección visual) está en
[`docs/diseno.md`](docs/diseno.md) y el relevamiento de assets en
[`docs/inventario.md`](docs/inventario.md).

## Cómo se arma

```bash
npm install
npm run assets    # copia de Codigo/PokeRush/Assets a public/assets/ y trae capturas/
npm run beats     # (opcional) rehace public/beats.json con librosa
npm run voz       # (opcional) rehace la voz en off y public/voz/duraciones.json
npm run dev       # Remotion Studio
npm run final     # render + normalización a -14 LUFS -> out/PokeRush_Trailer.mp4
```

- `public/assets/` **no se versiona**: lo arma `npm run assets` (copia del proyecto
  de Unity, audios derivados con ffmpeg y los clips de `capturas/`).
- `npm run beats` necesita el Python de `.venv` con librosa
  (`uv venv .venv && uv pip install --python .venv/Scripts/python.exe librosa soundfile`).
- `npm run voz` usa las voces de Windows (WinRT/OneCore) y ffmpeg.
- ffmpeg: el del PATH, el que instala `winget install Gyan.FFmpeg`, o el de Remotion.

## Qué manda en el timeline

- `public/beats.json`: los beats del tema, ya pasados a frames del tráiler. Los
  cortes del montaje se definen por número de beat (`src/datos/tiempos.ts`).
- `public/voz/duraciones.json`: la duración de cada línea de voz, medida con
  ffprobe. El arranque (cold open y "El mundo") se acomoda a esas duraciones; si
  una voz no entra en su plano, el proyecto no compila y dice cuál.
- `src/datos/capturas.ts`: qué clip usa cada plano y desde qué segundo.

## Voz

La voz que suena es **Jorge (Loquendo)**, generada por fuera y guardada en
`voz/manual/01.wav` … `11.wav`. `npm run voz` no la vuelve a sintetizar: la limpia
(recorta los silencios de las puntas, deja en 0,3 s las pausas internas más largas,
la pasa a 48 kHz mono y le da una compresión suave) y escribe el resultado en `public/voz/NN.wav`. Los
originales no se tocan. Si falta algún `voz/manual/NN.wav`, esa línea se sintetiza
con el perfil de `voz/guion.json` (hoy C: Sabina procesada).

Después de cambiar una línea: `npm run voz` y `npm run final`. Si una línea nueva
es demasiado larga para su plano, el proyecto no compila y dice cuál.

## Clips

Si en `capturas/` hay un clip con el nombre que espera un plano
(`src/datos/capturas.ts`), ese plano usa el clip; si no, la reconstrucción con
sprites. La composición `Trailer-Reconstruccion` (carpeta "Reconstrucciones" en
Studio) muestra todo reconstruido, para comparar.

## Revisar

- `Storyboard`: un cuadro clave por plano en una grilla
  (`npm run stills -- Storyboard 0` → `out/stills/`).
- `npm run stills -- Trailer 120 600 1500`: cuadros sueltos a resolución completa.
