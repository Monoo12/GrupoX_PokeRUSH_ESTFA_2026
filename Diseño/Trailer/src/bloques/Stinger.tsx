import { Audio } from "@remotion/media";
import React from "react";
import { AbsoluteFill, Sequence, staticFile, useCurrentFrame } from "remotion";
import { T, bloque } from "../datos/tiempos";
import { FUENTE, tamClear } from "../fuentes";

const B = bloque("Stinger");
const TEXTO = "¿Continuar?";
const LETRA_CADA = 3;

// El final: negro, el cursor de opción que parpadea y "¿Continuar?". Sin música.
export const Stinger: React.FC = () => {
  const frame = useCurrentFrame();
  const desde = T.continuar - B.desde;
  const n = Math.max(0, Math.floor((frame - desde) / LETRA_CADA) + 1);
  const cursor = Math.floor(frame / 15) % 2 === 0;
  const tam = tamClear(6);
  return (
    <AbsoluteFill
      style={{
        backgroundColor: "black",
        justifyContent: "center",
        alignItems: "center",
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 28 }}>
        <div
          style={{
            width: 36,
            height: 52,
            backgroundColor: "#FFFFFF",
            clipPath: "polygon(0 0, 100% 50%, 0 100%)",
            opacity: cursor ? 1 : 0,
          }}
        />
        <div
          style={{
            fontFamily: FUENTE.titulo,
            fontSize: tam,
            lineHeight: `${tam}px`,
            color: "#FFFFFF",
            whiteSpace: "pre",
            minWidth: 560,
          }}
        >
          {frame >= desde ? TEXTO.slice(0, n) : ""}
        </div>
      </div>
      {TEXTO.split("").map((c, i) =>
        /[\p{L}]/u.test(c) ? (
          <Sequence
            key={i}
            from={desde + i * LETRA_CADA}
            durationInFrames={7}
            layout="none"
          >
            <Audio
              src={staticFile("assets/sfx/menu-cursor.ogg")}
              volume={0.4}
            />
          </Sequence>
        ) : null,
      )}
    </AbsoluteFill>
  );
};
