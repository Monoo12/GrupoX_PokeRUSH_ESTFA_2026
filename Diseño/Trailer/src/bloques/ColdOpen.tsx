import { Audio } from "@remotion/media";
import { noise2D } from "@remotion/noise";
import React from "react";
import {
  AbsoluteFill,
  Sequence,
  interpolate,
  staticFile,
  useCurrentFrame,
} from "remotion";
import { Glitch } from "../componentes/Glitch";
import { Flash } from "../componentes/Overlays";
import { PixelSprite } from "../componentes/PixelSprite";
import { TextoGlitch } from "../componentes/TextoGlitch";
import { PALETA } from "../datos/paleta";
import { T } from "../datos/tiempos";
import { FUENTE, tamGreen } from "../fuentes";

const LETRA_CADA = 3;
const TAM = tamGreen(12); // 144 px

// Cursor de bloque que parpadea
const Cursor: React.FC<{ visible: boolean }> = ({ visible }) => (
  <div
    style={{
      width: 72,
      height: TAM,
      backgroundColor: PALETA.turquesa,
      opacity: visible ? 1 : 0,
    }}
  />
);

// Arcos eléctricos de píxeles alrededor de la nuca (cambian de forma cada 2 frames)
const Chispas: React.FC<{ x: number; y: number; intensidad: number }> = ({
  x,
  y,
  intensidad,
}) => {
  const frame = useCurrentFrame();
  const paso = Math.floor(frame / 2);
  if (intensidad <= 0) {
    return null;
  }
  return (
    <>
      {Array.from({ length: 5 }, (_, arco) =>
        Array.from({ length: 7 }, (_, i) => {
          const dx = Math.round(noise2D("arco" + arco, i * 0.7, paso) * 7) * 6;
          const dy = Math.round(noise2D("arcoy" + arco, i * 0.7, paso) * 6) * 6;
          return (
            <div
              key={`${arco}-${i}`}
              style={{
                position: "absolute",
                left: x + dx,
                top: y + dy,
                width: 6,
                height: 6,
                backgroundColor: i % 2 ? PALETA.turquesa : "#FFFFFF",
                opacity: intensidad,
              }}
            />
          );
        }),
      )}
    </>
  );
};

// 0-5 s: negro, se tipea "2026.", glitch a "2031." y el chispazo en la nuca.
export const ColdOpen: React.FC = () => {
  const frame = useCurrentFrame();
  const texto = frame < T.glitch2031 ? "2026." : "2031.";
  const tipeadas = Math.max(
    0,
    Math.min(5, Math.floor((frame - T.tipeo2026) / LETRA_CADA) + 1),
  );
  const mostrado =
    frame < T.glitch2031
      ? texto.slice(0, frame < T.tipeo2026 ? 0 : tipeadas)
      : texto;
  const cursorVisible =
    frame >= T.cursorAparece && Math.floor(frame / 8) % 2 === 0;
  const enChispazo = frame >= T.chispazo;

  // La espalda del jugador sale de la oscuridad en escalones
  const luz =
    Math.floor(
      interpolate(frame, [T.chispazo, T.flashNuca], [2, 4], {
        extrapolateLeft: "clamp",
        extrapolateRight: "clamp",
      }),
    ) / 4;
  const chispa = enChispazo ? (Math.floor(frame / 2) % 2 === 0 ? 1 : 0.5) : 0;

  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      {!enChispazo ? (
        <Glitch
          golpes={[
            T.glitch2031,
            T.glitch2031 + 5,
            T.glitch2031 + 10,
            T.queda2031,
          ]}
          caida={6}
          fuerza={22}
          semilla="anio"
        >
          <AbsoluteFill
            style={{ justifyContent: "center", alignItems: "center" }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 24 }}>
              <TextoGlitch
                texto={mostrado}
                fuente={FUENTE.sistema}
                tamano={TAM}
                color={PALETA.texto}
                golpes={[
                  T.glitch2031,
                  T.glitch2031 + 5,
                  T.glitch2031 + 10,
                  T.queda2031,
                ]}
                caida={6}
                fuerza={14}
                revolver={{
                  desde: T.glitch2031,
                  hasta: T.queda2031,
                  alfabeto: "0123456789#%&",
                }}
                semilla="2031"
              />
              <Cursor visible={cursorVisible} />
            </div>
          </AbsoluteFill>
        </Glitch>
      ) : (
        <AbsoluteFill
          style={{ justifyContent: "center", alignItems: "center" }}
        >
          <div style={{ position: "relative", filter: `brightness(${luz})` }}>
            {/* jugador-rojo.png: fila 3 = de espaldas, columna 2 = parado */}
            <PixelSprite
              src="assets/personajes/jugador-rojo.png"
              ancho={256}
              alto={256}
              escala={6}
              recorte={{ x: 128, y: 192, w: 64, h: 64 }}
            />
          </div>
          <div
            style={{
              position: "absolute",
              left: 960 - 192,
              top: 540 - 192,
              width: 384,
              height: 384,
            }}
          >
            <Chispas x={180} y={168} intensidad={chispa} />
          </div>
        </AbsoluteFill>
      )}
      <Flash en={T.flashNuca} dura={5} />
      {/* Tipeo de "2026.": un menu_cursor por letra */}
      {[0, 1, 2, 3, 4].map((i) => (
        <Sequence
          key={i}
          from={T.tipeo2026 + i * LETRA_CADA}
          durationInFrames={7}
          layout="none"
        >
          <Audio src={staticFile("assets/sfx/menu-cursor.ogg")} volume={0.35} />
        </Sequence>
      ))}
    </AbsoluteFill>
  );
};
