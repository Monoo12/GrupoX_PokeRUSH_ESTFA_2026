import { Audio } from "@remotion/media";
import React from "react";
import { Sequence, interpolate, staticFile } from "remotion";
import { TEMA } from "./datos/beats";
import {
  EFECTOS,
  volumenCama,
  volumenTema,
  volumenZumbido,
} from "./datos/sonido";
import { T, VOCES } from "./datos/tiempos";
import { durVoz } from "./datos/voz";
import { VozEnOff } from "./componentes/VozEnOff";

// Desde qué frame del tema arranca, para que el beat del drop caiga en T.drop.
// trimBefore va en frames a 30 fps; el redondeo deja el drop a menos de 17 ms.
const TEMA_DESDE = Math.round(T.carta - TEMA.desfaseSeg * 30);

export const BandaSonora: React.FC = () => {
  return (
    <>
      <Sequence name="Zumbido" durationInFrames={T.ciudad + 20} layout="none">
        <Audio
          src={staticFile("assets/sfx/zumbido.wav")}
          volume={(f) => volumenZumbido(f)}
        />
      </Sequence>

      <Sequence
        name="Cama: menú filtrado"
        from={T.ciudad}
        durationInFrames={T.carta + 9 - T.ciudad}
        layout="none"
      >
        <Audio
          src={staticFile("assets/musica/menu-apagado.wav")}
          volume={(f) => volumenCama(f + T.ciudad)}
        />
      </Sequence>

      <Sequence
        name="Tema: Kizaru"
        from={T.carta}
        durationInFrames={T.stinger - T.carta}
        layout="none"
      >
        <Audio
          src={staticFile(TEMA.archivo)}
          trimBefore={TEMA_DESDE}
          volume={(f) => volumenTema(f + T.carta)}
        />
      </Sequence>

      {VOCES.map((v) => (
        <Sequence
          key={v.id}
          name={`Voz ${v.id}`}
          from={v.frame}
          durationInFrames={durVoz(v.id) + 3}
          layout="none"
        >
          <VozEnOff id={v.id} />
        </Sequence>
      ))}

      {EFECTOS.map((e, i) => (
        <Sequence
          key={`${e.archivo}-${e.frame}-${i}`}
          name={e.archivo.replace("assets/sfx/", "")}
          from={e.frame}
          durationInFrames={e.dura ?? 150}
          layout="none"
        >
          <Audio
            src={staticFile(e.archivo)}
            volume={(f) =>
              e.dura
                ? e.volumen *
                  interpolate(f, [e.dura - 4, e.dura], [1, 0], {
                    extrapolateLeft: "clamp",
                    extrapolateRight: "clamp",
                  })
                : e.volumen
            }
          />
        </Sequence>
      ))}
    </>
  );
};
