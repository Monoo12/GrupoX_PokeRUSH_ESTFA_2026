import { Audio } from "@remotion/media";
import React from "react";
import { AbsoluteFill, Sequence, staticFile, useCurrentFrame } from "remotion";
import { CajaDeDialogo } from "../componentes/CajaDeDialogo";
import { ClipOReconstruccion } from "../componentes/Captura";
import { Glitch } from "../componentes/Glitch";
import { beat } from "../datos/beats";
import { CAPTURAS } from "../datos/capturas";
import { PALETA } from "../datos/paleta";
import { T, bloque } from "../datos/tiempos";
import { FUENTE, tamGreen } from "../fuentes";

const B = bloque("LaCarta");
const L = (f: number) => f - B.desde;

// La carta del juego (Tutorial_Despertar), un print por beat par del build.
const LINEAS = [
  'print("No confíes en la Liga")',
  'print("No confíes en Julieta")',
  'print("Si todavía recordás el 2026")',
  'print("Vení a la QuarchoCueva")',
];
const BEATS_DE_LINEA = [164, 166, 168, 170];
const LETRAS_POR_FRAME = 1.2;

// Reconstrucción del plano 8: la carta en verde de terminal.
const Carta: React.FC = () => {
  const frame = useCurrentFrame();
  const resaltar = frame >= L(beat(172));
  return (
    <AbsoluteFill
      style={{
        backgroundColor: "black",
        justifyContent: "center",
        paddingLeft: 180,
      }}
    >
      {LINEAS.map((linea, i) => {
        const desde = L(beat(BEATS_DE_LINEA[i]));
        const n = Math.max(0, Math.floor((frame - desde) * LETRAS_POR_FRAME));
        const primera = i === 0;
        return (
          <div
            key={linea}
            style={{
              fontFamily: FUENTE.sistema,
              fontSize: tamGreen(6),
              lineHeight: `${tamGreen(9)}px`,
              whiteSpace: "pre",
              color: resaltar
                ? primera
                  ? "#000000"
                  : PALETA.gris
                : PALETA.turquesa,
              backgroundColor:
                resaltar && primera ? PALETA.turquesa : "transparent",
              alignSelf: "flex-start",
            }}
          >
            {linea.slice(0, n)}
          </div>
        );
      })}
      {LINEAS.map((linea, i) =>
        Array.from({ length: Math.ceil(linea.length / 2) }, (_, k) => (
          <Sequence
            key={`${i}-${k}`}
            from={
              L(beat(BEATS_DE_LINEA[i])) +
              Math.round((k * 2) / LETRAS_POR_FRAME)
            }
            durationInFrames={6}
            layout="none"
          >
            <Audio
              src={staticFile("assets/sfx/menu-cursor.ogg")}
              volume={0.18}
            />
          </Sequence>
        )),
      )}
    </AbsoluteFill>
  );
};

// 14-20 s: la carta y la caja de "???". Entra el build del tema.
export const LaCarta: React.FC = () => {
  const caja = L(T.caja);
  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <Sequence name="8 · La carta" durationInFrames={caja}>
        <Glitch golpes={[0]} caida={8} fuerza={26}>
          <ClipOReconstruccion
            captura={CAPTURAS.carta}
            duracion={caja}
            reconstruccion={<Carta />}
          />
        </Glitch>
      </Sequence>
      <Sequence
        name="9 · Caja ???"
        from={caja}
        durationInFrames={B.hasta - T.caja}
        premountFor={30}
      >
        <ClipOReconstruccion
          captura={CAPTURAS.caja}
          duracion={B.hasta - T.caja}
          reconstruccion={
            <CajaDeDialogo
              nombre="???"
              texto="Aaah... conque recibiste mi carta."
              desde={4}
            />
          }
        />
      </Sequence>
    </AbsoluteFill>
  );
};
