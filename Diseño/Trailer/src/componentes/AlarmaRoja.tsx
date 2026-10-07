import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { PALETA } from "../datos/paleta";
import { despuesDeGolpes } from "./envolvente";

type Props = {
  // Frames (locales) de cada pulso: uno por beat
  pulsos: number[];
  // Qué tan fuerte tiñe (sobre un clip que ya tiene sus pulsos, conviene bajo)
  intensidad?: number;
  caida?: number;
};

// La alarma de la sede: un rojo que late en cada beat y un borde rojo fijo.
export const AlarmaRoja: React.FC<Props> = ({
  pulsos,
  intensidad = 0.55,
  caida = 9,
}) => {
  const frame = useCurrentFrame();
  const pulso = despuesDeGolpes(frame, pulsos, caida);
  return (
    <AbsoluteFill style={{ pointerEvents: "none" }}>
      <AbsoluteFill
        style={{
          backgroundColor: PALETA.alarma,
          opacity: pulso * intensidad,
          mixBlendMode: "multiply",
        }}
      />
      <AbsoluteFill
        style={{
          backgroundColor: PALETA.alarma,
          opacity: pulso * intensidad * 0.35,
        }}
      />
      <AbsoluteFill
        style={{
          boxShadow: `inset 0 0 0 16px ${PALETA.alarmaBanda}, inset 0 0 220px 60px ${PALETA.alarmaFondo}`,
          opacity: 0.5 + 0.5 * pulso,
        }}
      />
    </AbsoluteFill>
  );
};
