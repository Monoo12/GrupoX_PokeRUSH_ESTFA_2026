import React from "react";
import { Img, staticFile, useCurrentFrame } from "remotion";

export type Recorte = { x: number; y: number; w: number; h: number };

type Props = {
  // Ruta dentro de public/
  src: string;
  // Medidas naturales del PNG entero
  ancho: number;
  alto: number;
  // Siempre entera: el píxel art no se escala de a fracciones
  escala: number;
  // Un pedazo de la hoja (un cuadro de una hoja de personaje, por ejemplo)
  recorte?: Recorte;
  style?: React.CSSProperties;
};

// Un sprite de píxel art a escala entera y sin suavizado.
export const PixelSprite: React.FC<Props> = ({
  src,
  ancho,
  alto,
  escala,
  recorte,
  style,
}) => {
  if (!Number.isInteger(escala) || escala < 1) {
    throw new Error(
      `PixelSprite: la escala de ${src} tiene que ser un entero (vino ${escala})`,
    );
  }
  const r = recorte ?? { x: 0, y: 0, w: ancho, h: alto };
  return (
    <div
      style={{
        position: "relative",
        width: r.w * escala,
        height: r.h * escala,
        overflow: "hidden",
        ...style,
      }}
    >
      <Img
        src={staticFile(src)}
        style={{
          position: "absolute",
          left: -r.x * escala,
          top: -r.y * escala,
          width: ancho * escala,
          height: alto * escala,
          maxWidth: "none",
          imageRendering: "pixelated",
        }}
      />
    </div>
  );
};

type PropsTira = {
  src: string;
  // Cuadro cuadrado de lado `lado` en una tira horizontal (los Pokémon animados)
  lado: number;
  cuadros: number;
  escala: number;
  // Cuadros de la tira por segundo
  velocidad?: number;
  style?: React.CSSProperties;
};

// Una tira horizontal de cuadros animada al ritmo del tráiler.
export const TiraAnimada: React.FC<PropsTira> = ({
  src,
  lado,
  cuadros,
  escala,
  velocidad = 20,
  style,
}) => {
  const frame = useCurrentFrame();
  const i = Math.floor((frame * velocidad) / 30) % cuadros;
  return (
    <PixelSprite
      src={src}
      ancho={lado * cuadros}
      alto={lado}
      escala={escala}
      recorte={{ x: i * lado, y: 0, w: lado, h: lado }}
      style={style}
    />
  );
};
