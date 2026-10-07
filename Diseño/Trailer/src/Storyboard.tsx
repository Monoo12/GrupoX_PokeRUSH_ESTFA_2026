import React from "react";
import { AbsoluteFill, Freeze } from "remotion";
import { PALETA } from "./datos/paleta";
import { PLANOS } from "./datos/tiempos";
import { FUENTE, tamGreen } from "./fuentes";
import { TrailerVisual, type PropsTrailer } from "./Trailer";

const COLUMNAS = 6;
const CELDA = { w: 320, h: 180 };

const tiempo = (f: number) => `${(f / 30).toFixed(2).replace(".", ",")} s`;

// Un cuadro clave por plano, en grilla, para revisar el tráiler de un vistazo.
export const Storyboard: React.FC<PropsTrailer> = ({ usarCapturas }) => (
  <AbsoluteFill style={{ backgroundColor: PALETA.fondo }}>
    {PLANOS.map((p, i) => (
      <div
        key={p.n}
        style={{
          position: "absolute",
          left: (i % COLUMNAS) * CELDA.w,
          top: Math.floor(i / COLUMNAS) * CELDA.h,
          width: CELDA.w,
          height: CELDA.h,
          overflow: "hidden",
          outline: `1px solid ${PALETA.panelClaro}`,
        }}
      >
        <div
          style={{
            position: "relative",
            width: 1920,
            height: 1080,
            scale: String(CELDA.w / 1920),
            transformOrigin: "0 0",
          }}
        >
          <Freeze frame={p.clave}>
            <TrailerVisual usarCapturas={usarCapturas} />
          </Freeze>
        </div>
        <div
          style={{
            position: "absolute",
            left: 0,
            top: 0,
            padding: "2px 6px",
            backgroundColor: "rgba(0,0,0,0.75)",
            fontFamily: FUENTE.sistema,
            fontSize: tamGreen(1),
            lineHeight: `${tamGreen(1) + 2}px`,
            color: PALETA.turquesa,
            whiteSpace: "pre",
          }}
        >
          {`${p.n} · ${p.nombre}\n${tiempo(p.clave)}`}
        </div>
      </div>
    ))}
  </AbsoluteFill>
);
