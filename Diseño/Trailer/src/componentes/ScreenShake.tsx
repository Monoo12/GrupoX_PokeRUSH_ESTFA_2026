import { noise2D } from "@remotion/noise";
import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { despuesDeGolpes, px } from "./envolvente";

type Props = {
  // Frames (locales) donde pega cada golpe
  golpes: number[];
  // Desplazamiento máximo en píxeles
  fuerza?: number;
  // En cuántos frames se calma
  caida?: number;
  semilla?: string;
  children: React.ReactNode;
};

// Sacude todo lo de adentro después de cada golpe. El desplazamiento se redondea a
// píxeles enteros para no ensuciar el píxel art.
export const ScreenShake: React.FC<Props> = ({
  golpes,
  fuerza = 24,
  caida = 12,
  semilla = "shake",
  children,
}) => {
  const frame = useCurrentFrame();
  const amp = despuesDeGolpes(frame, golpes, caida) * fuerza;
  const dx = px(noise2D(semilla, frame * 0.9, 0) * amp);
  const dy = px(noise2D(semilla, 0, frame * 0.9) * amp * 0.7);
  return (
    <AbsoluteFill style={{ translate: `${dx}px ${dy}px` }}>
      {children}
    </AbsoluteFill>
  );
};
