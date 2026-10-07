import { Video } from "@remotion/media";
import { getStaticFiles } from "@remotion/studio";
import React, { createContext, useContext } from "react";
import { AbsoluteFill, Freeze, staticFile } from "remotion";
import type { Captura } from "../datos/capturas";

// La composición "Trailer-Reconstruccion" lo apaga para ver los planos armados
// con sprites aunque haya clips.
export const UsarCapturas = createContext(true);

export const useHayCaptura = (archivo: string): boolean => {
  const usar = useContext(UsarCapturas);
  return (
    usar &&
    getStaticFiles().some((f) => f.name === `assets/capturas/${archivo}`)
  );
};

type Props = {
  captura: Captura;
  // Frames que ocupa en el tráiler
  duracion: number;
  // Lo que se muestra si el clip no está
  reconstruccion: React.ReactNode;
  // Muestra un solo cuadro quieto (el del segundo `desdeSeg`)
  congelar?: boolean;
};

// Cada plano toma el clip de OBS si el archivo existe, o la reconstrucción si no.
// El audio del juego no se usa: el tráiler tiene su música y sus efectos.
export const ClipOReconstruccion: React.FC<Props> = ({
  captura,
  duracion,
  reconstruccion,
  congelar = false,
}) => {
  const hay = useHayCaptura(captura.archivo);
  if (!hay) {
    return <AbsoluteFill>{reconstruccion}</AbsoluteFill>;
  }
  const video = (
    <Video
      src={staticFile(`assets/capturas/${captura.archivo}`)}
      trimBefore={Math.round(captura.desdeSeg * 30)}
      durationInFrames={duracion}
      muted
      objectFit="cover"
      style={{ width: 1920, height: 1080 }}
    />
  );
  return (
    <AbsoluteFill>
      {congelar ? <Freeze frame={0}>{video}</Freeze> : video}
    </AbsoluteFill>
  );
};
