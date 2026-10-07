import React from "react";
import { AbsoluteFill, Sequence } from "remotion";
import { ClipOReconstruccion } from "../componentes/Captura";
import { Glitch } from "../componentes/Glitch";
import { Flash } from "../componentes/Overlays";
import { PixelSprite } from "../componentes/PixelSprite";
import { ScreenShake } from "../componentes/ScreenShake";
import { beat } from "../datos/beats";
import { CAPTURAS } from "../datos/capturas";
import { T, bloque } from "../datos/tiempos";

const B = bloque("SilencioYGolpe");

// Reconstrucción del plano 11: la pantalla de título a ×1.
const Titulo: React.FC = () => (
  <AbsoluteFill
    style={{
      backgroundColor: "black",
      justifyContent: "center",
      alignItems: "center",
    }}
  >
    <PixelSprite
      src="assets/menus/pantalla-inicio.png"
      ancho={1672}
      alto={941}
      escala={1}
    />
  </AbsoluteFill>
);

// 20-22 s: un beat de negro y silencio, y el drop con la pantalla de título.
export const SilencioYGolpe: React.FC = () => {
  const drop = T.drop - B.desde;
  const dura = B.hasta - T.drop;
  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <Sequence
        name="11 · Drop"
        from={drop}
        durationInFrames={dura}
        premountFor={30}
      >
        <ScreenShake golpes={[0, beat(182) - T.drop]} fuerza={30} caida={14}>
          <Glitch golpes={[0, beat(182) - T.drop]} caida={14} fuerza={28}>
            <ClipOReconstruccion
              captura={CAPTURAS.titulo}
              duracion={dura}
              reconstruccion={<Titulo />}
            />
          </Glitch>
        </ScreenShake>
        <Flash en={0} dura={4} />
      </Sequence>
    </AbsoluteFill>
  );
};
