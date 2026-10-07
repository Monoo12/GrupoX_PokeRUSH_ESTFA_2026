import React from "react";
import { AbsoluteFill, Series } from "remotion";
import { BandaSonora } from "./BandaSonora";
import { Cierre } from "./bloques/Cierre";
import { Climax } from "./bloques/Climax";
import { ColdOpen } from "./bloques/ColdOpen";
import { ElMundo } from "./bloques/ElMundo";
import { LaCarta } from "./bloques/LaCarta";
import { Montaje } from "./bloques/Montaje";
import { SilencioYGolpe } from "./bloques/SilencioYGolpe";
import { Stinger } from "./bloques/Stinger";
import { UsarCapturas } from "./componentes/Captura";
import { Scanlines } from "./componentes/Overlays";
import { BLOQUES, type IdBloque } from "./datos/tiempos";
import "./fuentes";

export type PropsTrailer = {
  // false: todos los planos con reconstrucción, aunque estén los clips
  usarCapturas: boolean;
};

const COMPONENTES: Record<IdBloque, React.FC> = {
  ColdOpen,
  ElMundo,
  LaCarta,
  SilencioYGolpe,
  Montaje,
  Climax,
  Cierre,
  Stinger,
};

// La imagen del tráiler (sin sonido): la usa el Storyboard congelada.
export const TrailerVisual: React.FC<PropsTrailer> = ({ usarCapturas }) => (
  <UsarCapturas.Provider value={usarCapturas}>
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <Series>
        {BLOQUES.map((b) => {
          const Bloque = COMPONENTES[b.id];
          return (
            <Series.Sequence
              key={b.id}
              name={b.nombre}
              durationInFrames={b.hasta - b.desde}
            >
              <Bloque />
            </Series.Sequence>
          );
        })}
      </Series>
      <Scanlines />
    </AbsoluteFill>
  </UsarCapturas.Provider>
);

export const Trailer: React.FC<PropsTrailer> = ({ usarCapturas }) => (
  <AbsoluteFill>
    <TrailerVisual usarCapturas={usarCapturas} />
    <BandaSonora />
  </AbsoluteFill>
);
