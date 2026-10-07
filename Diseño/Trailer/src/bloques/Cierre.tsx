import React from "react";
import { AbsoluteFill, Img, staticFile, useCurrentFrame } from "remotion";
import { Flash } from "../componentes/Overlays";
import { PixelSprite } from "../componentes/PixelSprite";
import { ScreenShake } from "../componentes/ScreenShake";
import { TextoGlitch } from "../componentes/TextoGlitch";
import { PALETA } from "../datos/paleta";
import { T, bloque } from "../datos/tiempos";
import { FUENTE, tamClear } from "../fuentes";

const B = bloque("Cierre");
const L = (f: number) => f - B.desde;

// El logo de PokeRush solo existe adentro de la pantalla de título: se recorta.
const LOGO = { x: 520, y: 30, w: 660, h: 380 };
const MASCARA = "radial-gradient(ellipse at center, #000 52%, transparent 74%)";

const Logo: React.FC<{ escala: number }> = ({ escala }) => (
  <div style={{ maskImage: MASCARA, WebkitMaskImage: MASCARA }}>
    <PixelSprite
      src="assets/menus/pantalla-inicio.png"
      ancho={1672}
      alto={941}
      escala={escala}
      recorte={LOGO}
    />
  </div>
);

const DISCLAIMER =
  "Fan game académico sin fines de lucro. Música: One Piece OST. Pokémon © Nintendo / Game Freak";

// 53-58 s: el logo entra con "pops" de escala entera (×4, ×3, ×2), "Por 2026.",
// y después la placa con Level Up Creators, la plataforma y el aviso legal.
export const Cierre: React.FC = () => {
  const frame = useCurrentFrame();
  const por2026 = L(T.por2026);
  const placa = L(T.creditos);
  const escala = frame === 0 ? 4 : frame === 1 ? 3 : 2;

  if (frame < placa) {
    return (
      <AbsoluteFill style={{ backgroundColor: "black" }}>
        <ScreenShake golpes={[0]} fuerza={30} caida={14}>
          <AbsoluteFill
            style={{
              justifyContent: "center",
              alignItems: "center",
              paddingBottom: 150,
            }}
          >
            <Logo escala={escala} />
          </AbsoluteFill>
        </ScreenShake>
        {frame >= por2026 ? (
          <AbsoluteFill
            style={{
              justifyContent: "flex-end",
              alignItems: "center",
              paddingBottom: 110,
            }}
          >
            <TextoGlitch
              texto="Por 2026."
              fuente={FUENTE.titulo}
              tamano={tamClear(8)}
              golpes={[por2026]}
              caida={8}
              fuerza={14}
            />
          </AbsoluteFill>
        ) : null}
        <Flash en={0} dura={5} />
      </AbsoluteFill>
    );
  }

  return (
    <AbsoluteFill style={{ backgroundColor: "black", alignItems: "center" }}>
      <div style={{ marginTop: 40 }}>
        <Logo escala={1} />
      </div>
      <div
        style={{
          fontFamily: FUENTE.titulo,
          fontSize: tamClear(5),
          lineHeight: `${tamClear(5)}px`,
          color: PALETA.texto,
          marginTop: -10,
        }}
      >
        Por 2026.
      </div>
      {/* El logo es azul sobre blanco: va en su placa, como en los créditos del juego */}
      <div style={{ backgroundColor: "#F8F8F8", padding: 8, marginTop: 36 }}>
        <Img
          src={staticFile("assets/logos/level-up-creators.png")}
          style={{ width: 240, height: 241, display: "block" }}
        />
      </div>
      <div
        style={{
          fontFamily: FUENTE.caja,
          fontSize: tamClear(4),
          lineHeight: `${tamClear(4)}px`,
          color: PALETA.texto,
          marginTop: 24,
        }}
      >
        Windows · 2026 · ITFA
      </div>
      <div
        style={{
          position: "absolute",
          bottom: 48,
          fontFamily: FUENTE.caja,
          fontSize: tamClear(2),
          lineHeight: `${tamClear(2)}px`,
          color: PALETA.textoSecundario,
        }}
      >
        {DISCLAIMER}
      </div>
    </AbsoluteFill>
  );
};
