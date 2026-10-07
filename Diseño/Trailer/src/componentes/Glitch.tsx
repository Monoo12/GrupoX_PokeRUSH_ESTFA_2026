import { noise2D } from "@remotion/noise";
import React, { useId } from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { PALETA } from "../datos/paleta";
import { despuesDeGolpes, px } from "./envolvente";

// RGB split con un filtro SVG: separa los tres canales y corre el rojo y el azul
// en X. Sirve para cualquier cosa, también para los clips (es un solo dibujado).
export const RGBSplit: React.FC<{
  desplazamiento: number;
  children: React.ReactNode;
}> = ({ desplazamiento, children }) => {
  const id = "rgb" + useId().replace(/[^a-zA-Z0-9]/g, "");
  const d = px(desplazamiento);
  if (d === 0) {
    return <AbsoluteFill>{children}</AbsoluteFill>;
  }
  return (
    <AbsoluteFill>
      <svg width={0} height={0} style={{ position: "absolute" }}>
        <filter
          id={id}
          x="-3%"
          y="0%"
          width="106%"
          height="100%"
          colorInterpolationFilters="sRGB"
        >
          <feColorMatrix
            in="SourceGraphic"
            type="matrix"
            values="1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0"
            result="r"
          />
          <feOffset in="r" dx={d} dy={0} result="r2" />
          <feColorMatrix
            in="SourceGraphic"
            type="matrix"
            values="0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0"
            result="g"
          />
          <feColorMatrix
            in="SourceGraphic"
            type="matrix"
            values="0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0"
            result="b"
          />
          <feOffset in="b" dx={-d} dy={0} result="b2" />
          <feBlend in="r2" in2="g" mode="screen" result="rg" />
          <feBlend in="rg" in2="b2" mode="screen" />
        </filter>
      </svg>
      <AbsoluteFill style={{ filter: `url(#${id})` }}>{children}</AbsoluteFill>
    </AbsoluteFill>
  );
};

// Bloques de "nieve" digital de 8×8 que se prenden y apagan con ruido.
export const Nieve: React.FC<{ intensidad: number; semilla?: string }> = ({
  intensidad,
  semilla = "nieve",
}) => {
  const frame = useCurrentFrame();
  if (intensidad <= 0.01) {
    return null;
  }
  const cantidad = Math.round(90 * intensidad);
  const colores = [PALETA.texto, PALETA.gris, PALETA.turquesa, PALETA.negro];
  return (
    <AbsoluteFill style={{ pointerEvents: "none" }}>
      {Array.from({ length: cantidad }, (_, i) => {
        const x =
          Math.floor(((noise2D(semilla, i * 1.7, frame * 0.6) + 1) / 2) * 240) *
          8;
        const y =
          Math.floor(
            ((noise2D(semilla + "y", i * 2.3, frame * 0.6) + 1) / 2) * 135,
          ) * 8;
        const ancho =
          8 *
          (1 + Math.floor(((noise2D(semilla + "w", i, frame) + 1) / 2) * 12));
        const color = colores[i % colores.length];
        return (
          <div
            key={i}
            style={{
              position: "absolute",
              left: x,
              top: y,
              width: ancho,
              height: 8,
              backgroundColor: color,
              opacity: 0.85,
            }}
          />
        );
      })}
    </AbsoluteFill>
  );
};

type PropsGlitch = {
  // Frames (locales) donde arranca cada ráfaga
  golpes: number[];
  // Cuánto dura cada ráfaga
  caida?: number;
  // Desplazamiento máximo del RGB split
  fuerza?: number;
  // Si suma nieve
  conNieve?: boolean;
  semilla?: string;
  children: React.ReactNode;
};

// El glitch del tráiler: RGB split que tiembla con ruido y se apaga, un salto
// horizontal del cuadro entero y nieve digital.
export const Glitch: React.FC<PropsGlitch> = ({
  golpes,
  caida = 10,
  fuerza = 18,
  conNieve = true,
  semilla = "glitch",
  children,
}) => {
  const frame = useCurrentFrame();
  const env = despuesDeGolpes(frame, golpes, caida);
  const temblor = env > 0 ? (noise2D(semilla, frame * 1.3, 0) + 1) / 2 : 0;
  const desplazamiento = env * fuerza * (0.4 + 0.6 * temblor);
  const salto =
    env > 0.5 ? px(noise2D(semilla + "s", frame * 2.1, 0) * 30 * env) : 0;
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ translate: `${salto}px 0px` }}>
        <RGBSplit desplazamiento={desplazamiento}>{children}</RGBSplit>
      </AbsoluteFill>
      {conNieve ? <Nieve intensidad={env * env} semilla={semilla} /> : null}
    </AbsoluteFill>
  );
};
