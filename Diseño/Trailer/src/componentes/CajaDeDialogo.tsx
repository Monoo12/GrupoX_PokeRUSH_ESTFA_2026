import { Audio } from "@remotion/media";
import React from "react";
import { AbsoluteFill, Sequence, staticFile, useCurrentFrame } from "remotion";
import { FUENTE, tamClear } from "../fuentes";
import { PixelSprite } from "./PixelSprite";

export type Retrato = {
  src: string;
  ancho: number;
  alto: number;
  escala: number;
  silueta?: boolean;
};

type Props = {
  texto: string;
  nombre?: string;
  retrato?: Retrato;
  // Frame (local) en el que empieza a tipear
  desde?: number;
  // Letras por segundo
  velocidad?: number;
  // Sonido de menu_cursor por letra
  conSonido?: boolean;
  volumenTipeo?: number;
  conPista?: boolean;
};

// Medidas de la caja del juego a 1080p, tomadas de Prefabs/UI/DialogoCanvas.prefab
// (Canvas de 512×384 agrandado ×3,75) y medidas sobre los clips grabados.
const CAJA_ARRIBA = 720;
const TEXTO_X = 309;
const TAM = tamClear(4); // 52 px: 4 píxeles de pantalla por píxel de la letra
const ASCENSO = Math.round(TAM * (624 / 832));
const NOMBRE_BASE = 795;
const TEXTO_BASE = 871;
const INTERLINEA = 84;
const RETRATO_CENTRO = { x: 150, y: 913 };

export const CajaDeDialogo: React.FC<Props> = ({
  texto,
  nombre,
  retrato,
  desde = 0,
  velocidad = 40,
  conSonido = true,
  volumenTipeo = 0.22,
  conPista = true,
}) => {
  const frame = useCurrentFrame();
  const visibles = Math.max(0, Math.floor(((frame - desde) * velocidad) / 30));
  const mostrado = texto.slice(0, visibles);

  // Un "tic" por letra (no en espacios ni signos), con al menos 2 frames entre tics
  const tics: number[] = [];
  for (let i = 0; i < texto.length; i++) {
    if (!/[\p{L}\p{N}]/u.test(texto[i])) {
      continue;
    }
    const f = desde + Math.ceil((i * 30) / velocidad);
    if (tics.length === 0 || f - tics[tics.length - 1] >= 2) {
      tics.push(f);
    }
  }

  const anchoRetrato = retrato ? retrato.ancho * retrato.escala : 0;
  const altoRetrato = retrato ? retrato.alto * retrato.escala : 0;

  return (
    <AbsoluteFill>
      <div
        style={{
          position: "absolute",
          left: 0,
          top: CAJA_ARRIBA,
          width: 1920,
          height: 1080 - CAJA_ARRIBA,
          backgroundColor: "rgba(4,4,6,0.30)",
        }}
      />
      {retrato ? (
        <PixelSprite
          src={retrato.src}
          ancho={retrato.ancho}
          alto={retrato.alto}
          escala={retrato.escala}
          style={{
            position: "absolute",
            left: Math.max(0, RETRATO_CENTRO.x - Math.round(anchoRetrato / 2)),
            top: RETRATO_CENTRO.y - Math.round(altoRetrato / 2),
            filter: retrato.silueta ? "brightness(0)" : undefined,
          }}
        />
      ) : null}
      {nombre ? (
        <div
          style={{
            position: "absolute",
            left: TEXTO_X,
            top: NOMBRE_BASE - ASCENSO,
            fontFamily: FUENTE.caja,
            fontSize: TAM,
            lineHeight: `${TAM}px`,
            color: "#FFFFFF",
            whiteSpace: "pre",
          }}
        >
          {nombre}
        </div>
      ) : null}
      <div
        style={{
          position: "absolute",
          left: TEXTO_X,
          top: TEXTO_BASE - ASCENSO - (INTERLINEA - TAM) / 2,
          width: 1155,
          fontFamily: FUENTE.caja,
          fontSize: TAM,
          lineHeight: `${INTERLINEA}px`,
          color: "#FFFFFF",
        }}
      >
        {mostrado}
      </div>
      {conPista ? (
        <div
          style={{
            position: "absolute",
            right: 28,
            top: 1072 - Math.round(tamClear(3) * (624 / 832)),
            fontFamily: FUENTE.caja,
            fontSize: tamClear(3),
            lineHeight: `${tamClear(3)}px`,
            color: "rgba(255,255,255,0.6)",
          }}
        >
          X para seguir
        </div>
      ) : null}
      {conSonido
        ? tics.map((f, i) => (
            <Sequence key={i} from={f} durationInFrames={7} layout="none">
              <Audio
                src={staticFile("assets/sfx/menu-cursor.ogg")}
                volume={volumenTipeo}
              />
            </Sequence>
          ))
        : null}
    </AbsoluteFill>
  );
};
