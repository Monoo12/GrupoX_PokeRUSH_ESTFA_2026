import React from "react";
import { AbsoluteFill, interpolate, useCurrentFrame } from "remotion";
import { PALETA } from "../datos/paleta";
import { FUENTE, tamClear } from "../fuentes";
import { Flash } from "./Overlays";
import { PixelSprite, TiraAnimada } from "./PixelSprite";
import { ScreenShake } from "./ScreenShake";

type Props = {
  // Frames locales: cuándo pega el golpe y cuándo cae el rival
  golpe: number;
  desmayo: number;
};

// Fondo a ×4, Pokémon a ×8 (2:1 con el fondo, como en el juego), HUD a ×3.
const FONDO = 4;
const BICHO = 8;
const HUD = 3;

// La barra de PS dentro de databox_normal_foe.png (medida sobre el PNG)
const BARRA = { x: 118, y: 40, w: 96, h: 6 };

const colorDePS = (fraccion: number) =>
  fraccion > 0.5
    ? PALETA.psVerde
    : fraccion > 0.2
      ? PALETA.psAmarillo
      : PALETA.psRojo;

// El combate armado con sprites: Lucario contra el Porygon-Z de Néstor, el
// Palmeo, la barra de PS que baja y el rival que cae. Es la reconstrucción del
// plano 15 cuando no está 06-combate.mp4.
export const PlanoCombate: React.FC<Props> = ({ golpe, desmayo }) => {
  const frame = useCurrentFrame();
  const ps = interpolate(frame, [golpe + 6, golpe + 30], [1, 0.09], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const titila =
    frame >= golpe &&
    frame < golpe + 12 &&
    Math.floor((frame - golpe) / 2) % 2 === 0;
  const caida = Math.round(
    interpolate(frame, [desmayo, desmayo + 10], [0, 58], {
      extrapolateLeft: "clamp",
      extrapolateRight: "clamp",
    }),
  );
  const cuadroFx = Math.floor(((frame - golpe) * 15) / 30);
  const mensaje =
    frame < golpe + 20
      ? "¡Lucario usó Palmeo!"
      : frame < desmayo + 6
        ? "¡Es súper efectivo!"
        : "¡Porygon-Z se debilitó!";
  // Lucario se tira hacia adelante antes del golpe
  const embestida = Math.round(
    interpolate(frame, [golpe - 8, golpe - 2, golpe + 6], [0, 64, 0], {
      extrapolateLeft: "clamp",
      extrapolateRight: "clamp",
    }),
  );

  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <ScreenShake golpes={[golpe]} fuerza={28}>
        <PixelSprite
          src="assets/combate/fondo-central.png"
          ancho={512}
          alto={384}
          escala={FONDO}
          style={{
            position: "absolute",
            left: (1920 - 512 * FONDO) / 2,
            top: (1080 - 384 * FONDO) / 2,
          }}
        />
        {/* El rival: se recorta desde abajo al caer, como en el juego */}
        <div
          style={{
            position: "absolute",
            left: 1380 - (58 * BICHO) / 2,
            top: 560 - 58 * BICHO,
            width: 58 * BICHO,
            height: 58 * BICHO,
            overflow: "hidden",
            opacity: titila ? 0 : 1,
          }}
        >
          <TiraAnimada
            src="assets/combate/porygonz-frente.png"
            lado={58}
            cuadros={95}
            escala={BICHO}
            style={{ position: "absolute", left: 0, top: caida * BICHO }}
          />
        </div>
        <TiraAnimada
          src="assets/combate/lucario-espalda.png"
          lado={65}
          cuadros={143}
          escala={BICHO}
          style={{
            position: "absolute",
            left: 520 - (65 * BICHO) / 2 + embestida,
            top: 960 - 65 * BICHO,
          }}
        />
        {frame >= golpe && cuadroFx < 4 ? (
          <PixelSprite
            src="assets/combate/fx-palmeo.png"
            ancho={768}
            alto={192}
            escala={2}
            recorte={{ x: cuadroFx * 192, y: 0, w: 192, h: 192 }}
            style={{
              position: "absolute",
              left: 1380 - 192,
              top: 560 - 58 * 4 - 192,
            }}
          />
        ) : null}
        {/* Databox del rival con su barra de PS */}
        <div style={{ position: "absolute", left: 40, top: 70 }}>
          <PixelSprite
            src="assets/combate/databox-rival.png"
            ancho={260}
            alto={70}
            escala={HUD}
          />
          <div
            style={{
              position: "absolute",
              left: BARRA.x * HUD,
              top: BARRA.y * HUD,
              width: Math.round(BARRA.w * ps) * HUD,
              height: BARRA.h * HUD,
              backgroundColor: colorDePS(ps),
            }}
          />
          <div
            style={{
              position: "absolute",
              left: 24,
              top: 18,
              fontFamily: FUENTE.caja,
              fontSize: tamClear(4),
              lineHeight: `${tamClear(4)}px`,
              color: PALETA.texto,
            }}
          >
            Porygon-Z
          </div>
        </div>
        <div
          style={{
            position: "absolute",
            left: 1920 - 260 * HUD - 40,
            top: 470,
          }}
        >
          <PixelSprite
            src="assets/combate/databox-jugador.png"
            ancho={260}
            alto={84}
            escala={HUD}
          />
          <div
            style={{
              position: "absolute",
              left: 136 * HUD,
              top: 40 * HUD,
              width: 96 * HUD,
              height: 6 * HUD,
              backgroundColor: PALETA.psVerde,
            }}
          />
          <div
            style={{
              position: "absolute",
              left: 150,
              top: 18,
              fontFamily: FUENTE.caja,
              fontSize: tamClear(4),
              lineHeight: `${tamClear(4)}px`,
              color: PALETA.texto,
            }}
          >
            Lucario
          </div>
        </div>
      </ScreenShake>
      <div
        style={{
          position: "absolute",
          left: 0,
          top: 820,
          width: 1920,
          height: 260,
          backgroundColor: "rgba(4,4,6,0.55)",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 60,
          top: 900,
          fontFamily: FUENTE.caja,
          fontSize: tamClear(4),
          lineHeight: `${tamClear(4)}px`,
          color: "#FFFFFF",
        }}
      >
        {mensaje}
      </div>
      <Flash en={golpe} dura={5} />
    </AbsoluteFill>
  );
};
