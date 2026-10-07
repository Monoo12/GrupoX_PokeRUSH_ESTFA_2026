import React from "react";
import { AbsoluteFill, interpolate, useCurrentFrame } from "remotion";

// Líneas de barrido fijas: una línea oscura de 2 px cada 4 px.
export const Scanlines: React.FC<{ opacidad?: number }> = ({
  opacidad = 0.12,
}) => (
  <AbsoluteFill
    style={{
      pointerEvents: "none",
      backgroundImage: `repeating-linear-gradient(to bottom, rgba(0,0,0,${opacidad}) 0px, rgba(0,0,0,${opacidad}) 2px, transparent 2px, transparent 4px)`,
    }}
  />
);

// Un destello que entra de golpe en `en` y se va en `dura` frames.
export const Flash: React.FC<{
  en: number;
  dura?: number;
  color?: string;
  maximo?: number;
}> = ({ en, dura = 6, color = "#FFFFFF", maximo = 1 }) => {
  const frame = useCurrentFrame();
  if (frame < en || frame > en + dura) {
    return null;
  }
  const opacidad = interpolate(frame, [en, en + dura], [maximo, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  return (
    <AbsoluteFill
      style={{
        backgroundColor: color,
        opacity: opacidad,
        pointerEvents: "none",
      }}
    />
  );
};

// Fundido a negro en escalones (sin suavizado): baja de a cuartos.
export const NegroEnEscalones: React.FC<{
  desde: number;
  hasta: number;
  escalones?: number;
}> = ({ desde, hasta, escalones = 4 }) => {
  const frame = useCurrentFrame();
  const t = interpolate(frame, [desde, hasta], [0, 1], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const opacidad = Math.floor(t * escalones) / escalones;
  return (
    <AbsoluteFill
      style={{
        backgroundColor: "black",
        opacity: opacidad,
        pointerEvents: "none",
      }}
    />
  );
};
