"""Detecta tempo, beats y golpes del tema del tráiler y escribe public/beats.json.

Se corre con `npm run beats` (usa el Python de .venv, con librosa). Lee la
música de public/assets/musica/ (antes: `npm run assets`).

Lo que es análisis (beats, compases, secciones, golpes) sale de librosa. Lo que
es decisión de montaje (qué beat es el drop y en qué segundo del tráiler cae) está
en las constantes de abajo: si al escucharlo se quiere otro punto, se cambia acá
y se vuelve a correr.
"""
import json
import datetime
from pathlib import Path

import numpy as np
import librosa

RAIZ = Path(__file__).resolve().parent.parent
TEMA = RAIZ / "public/assets/musica/kizaru-epic.mp3"
SALIDA = RAIZ / "public/beats.json"

FPS = 30
SR = 22050
HOP = 256  # 11,6 ms por cuadro de análisis: bastante más fino que un frame de video (33 ms)

# --- Decisiones de montaje -------------------------------------------------
# El tema tiene un "build" de 16 beats (71,1 s) que termina en la entrada de la
# banda completa (77,86 s). El build va debajo de la carta y de la caja de
# diálogo; su último beat se silencia (el medio segundo de negro) y la banda
# completa es el drop.
BEAT_BUILD = 164
BEAT_DROP = 180
DROP_EN_TRAILER = 20.5  # segundos
# Fade out de la música al final (el stinger queda en silencio)
FADE_OUT_EMPIEZA = 56.5
FADE_OUT_TERMINA = 58.5


def segundo_a_frame(s: float) -> int:
    return int(round(s * FPS))


def main() -> None:
    y, sr = librosa.load(TEMA, sr=SR, mono=True)
    duracion = len(y) / sr

    onset = librosa.onset.onset_strength(y=y, sr=sr, hop_length=HOP)
    tempo, cuadros = librosa.beat.beat_track(onset_envelope=onset, sr=sr, hop_length=HOP, tightness=200)
    tempo = float(np.atleast_1d(tempo)[0])
    beats = librosa.frames_to_time(cuadros, sr=sr, hop_length=HOP)

    # Envolvente de graves (< 180 Hz): marca el "uno" de cada compás y los golpes.
    mel = librosa.feature.melspectrogram(y=y, sr=sr, hop_length=HOP, n_mels=64, fmax=8000)
    f_mel = librosa.mel_frequencies(n_mels=64, fmax=8000)
    graves = librosa.onset.onset_strength(S=librosa.power_to_db(mel[f_mel < 180]), sr=sr, hop_length=HOP)
    fuerza = np.array([graves[max(0, c - 3):c + 4].max() for c in cuadros])
    fuerza = fuerza / fuerza.max()
    fase = int(np.argmax([fuerza[p::4].mean() for p in range(4)]))

    rms = []
    for i, t in enumerate(beats):
        fin = beats[i + 1] if i + 1 < len(beats) else min(duracion, t + 60 / tempo)
        seg = y[int(t * sr):int(fin * sr)]
        rms.append(float(20 * np.log10(np.sqrt(np.mean(seg ** 2)) + 1e-9)))

    # Secciones por similitud de timbre y armonía, sincronizadas al beat.
    croma = librosa.util.sync(librosa.feature.chroma_cqt(y=y, sr=sr, hop_length=HOP), cuadros)
    mfcc = librosa.util.sync(librosa.feature.mfcc(y=y, sr=sr, hop_length=HOP, n_mfcc=13), cuadros)
    rasgos = librosa.util.normalize(np.vstack([croma, mfcc]), axis=1)
    limites = [int(b) for b in librosa.segment.agglomerative(rasgos, 14)]

    t_drop = float(beats[BEAT_DROP])
    desfase = DROP_EN_TRAILER - t_drop  # segundo del tráiler = segundo del tema + desfase

    def en_trailer(i: int) -> dict:
        s = float(beats[i]) + desfase
        return {"beat": i, "seg": round(s, 3), "frame": segundo_a_frame(s)}

    beats_trailer = []
    for i in range(BEAT_BUILD, len(beats)):
        s = float(beats[i]) + desfase
        if s > FADE_OUT_TERMINA + 1:
            break
        beats_trailer.append({
            "beat": i,
            "seg": round(s, 3),
            "frame": segundo_a_frame(s),
            "uno": (i - fase) % 4 == 0,
            "graves": round(float(fuerza[i]), 2),
            "rmsDb": round(rms[i], 1),
        })

    datos = {
        "generado": datetime.datetime.now().isoformat(timespec="seconds"),
        "fps": FPS,
        "tema": {
            "archivo": "assets/musica/kizaru-epic.mp3",
            "titulo": "Luffy Vs. Kizaru (My Peak) - Epic Version (One Piece OST)",
            "origen": "Codigo/PokeRush/Assets/Audio/Pelea contra julieta/Fase 1/",
            "duracionSeg": round(duracion, 3),
            "bpm": round(tempo, 2),
            "beatSeg": round(60 / tempo, 4),
            "faseDelUno": fase,
            "beats": [round(float(b), 3) for b in beats],
            "secciones": [{"beat": b, "seg": round(float(beats[b]), 3)} for b in limites],
            "golpesFuertes": [
                {"beat": int(i), "seg": round(float(beats[i]), 3), "graves": round(float(fuerza[i]), 2)}
                for i in np.argsort(-fuerza)[:12]
            ],
        },
        "montaje": {
            "beatBuild": BEAT_BUILD,
            "beatDrop": BEAT_DROP,
            "dropEnTrailerSeg": DROP_EN_TRAILER,
            "desfaseSeg": round(desfase, 3),
            "nota": "segundo del trailer = segundo del tema + desfaseSeg",
        },
        "marcas": {
            "buildEntra": en_trailer(BEAT_BUILD),
            "silencio": en_trailer(BEAT_DROP - 1),
            "drop": en_trailer(BEAT_DROP),
            "fadeOutEmpieza": {"seg": FADE_OUT_EMPIEZA, "frame": segundo_a_frame(FADE_OUT_EMPIEZA)},
            "fadeOutTermina": {"seg": FADE_OUT_TERMINA, "frame": segundo_a_frame(FADE_OUT_TERMINA)},
        },
        "beatsTrailer": beats_trailer,
    }
    SALIDA.write_text(json.dumps(datos, ensure_ascii=False, indent=1), encoding="utf-8")

    print(f"{TEMA.name}: {tempo:.2f} BPM (beat de {60 / tempo:.4f} s), {len(beats)} beats, uno en fase {fase}")
    print(f"drop: beat {BEAT_DROP} del tema ({t_drop:.3f} s) -> {DROP_EN_TRAILER} s del trailer")
    print(f"build: beat {BEAT_BUILD} -> {beats[BEAT_BUILD] + desfase:.3f} s; silencio desde {beats[BEAT_DROP - 1] + desfase:.3f} s")
    print(f"escrito {SALIDA.relative_to(RAIZ)} ({len(beats_trailer)} beats en el trailer)")


if __name__ == "__main__":
    main()
