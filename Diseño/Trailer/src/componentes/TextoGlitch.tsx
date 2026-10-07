import { noise2D } from "@remotion/noise";
import React from "react";
import { useCurrentFrame } from "remotion";
import { despuesDeGolpes, px } from "./envolvente";

type Props = {
  texto: string;
  fuente: string;
  tamano: number;
  color?: string;
  // Frames (locales) de cada ráfaga de glitch
  golpes?: number[];
  caida?: number;
  // Desplazamiento máximo del RGB split, en píxeles
  fuerza?: number;
  // Mientras dure, cambia letras por otras de `alfabeto` (el "2026" que se revuelve)
  revolver?: { desde: number; hasta: number; alfabeto: string };
  semilla?: string;
  style?: React.CSSProperties;
};

const FRANJAS = 6;

// Texto con glitch: RGB split con sombras corridas y el texto cortado en franjas
// que se corren de costado. Sin ráfaga activa es texto plano.
export const TextoGlitch: React.FC<Props> = ({
  texto,
  fuente,
  tamano,
  color = "#FFFFFF",
  golpes = [],
  caida = 10,
  fuerza = 10,
  revolver,
  semilla = "texto",
  style,
}) => {
  const frame = useCurrentFrame();
  const env = despuesDeGolpes(frame, golpes, caida);

  let mostrado = texto;
  if (revolver && frame >= revolver.desde && frame < revolver.hasta) {
    mostrado = texto
      .split("")
      .map((c, i) => {
        if (!/[0-9A-Za-z]/.test(c)) {
          return c;
        }
        const r = (noise2D(semilla, i * 3.1, frame * 0.8) + 1) / 2;
        return r > 0.55
          ? revolver.alfabeto[Math.floor(r * 997) % revolver.alfabeto.length]
          : c;
      })
      .join("");
  }

  const d = px(
    env * fuerza * (0.5 + 0.5 * Math.abs(noise2D(semilla, frame, 1))),
  );
  const base: React.CSSProperties = {
    fontFamily: fuente,
    fontSize: tamano,
    lineHeight: `${tamano}px`,
    color,
    whiteSpace: "pre",
    textShadow:
      d !== 0
        ? `${d}px 0 rgba(255,40,60,0.85), ${-d}px 0 rgba(40,240,255,0.85)`
        : undefined,
  };

  if (env < 0.05) {
    return <div style={{ ...base, ...style }}>{mostrado}</div>;
  }

  // Con ráfaga: el mismo texto en franjas horizontales, cada una corrida en X
  return (
    <div style={{ position: "relative", ...style }}>
      <div style={{ ...base, visibility: "hidden" }}>{mostrado}</div>
      {Array.from({ length: FRANJAS }, (_, i) => {
        const arriba = (i / FRANJAS) * 100;
        const abajo = 100 - ((i + 1) / FRANJAS) * 100;
        const corrimiento = px(
          noise2D(semilla + "f", i * 5.3, frame * 1.7) * 40 * env,
        );
        return (
          <div
            key={i}
            style={{
              ...base,
              position: "absolute",
              left: corrimiento,
              top: 0,
              clipPath: `inset(${arriba}% 0 ${abajo}% 0)`,
            }}
          >
            {mostrado}
          </div>
        );
      })}
    </div>
  );
};
