import React from "react";
import { AbsoluteFill, Sequence, interpolate, useCurrentFrame } from "remotion";
import { AlarmaRoja } from "../componentes/AlarmaRoja";
import { CajaDeDialogo } from "../componentes/CajaDeDialogo";
import { ClipOReconstruccion, useHayCaptura } from "../componentes/Captura";
import { Glitch } from "../componentes/Glitch";
import { Flash } from "../componentes/Overlays";
import { PixelSprite } from "../componentes/PixelSprite";
import { ScreenShake } from "../componentes/ScreenShake";
import { TextoGlitch } from "../componentes/TextoGlitch";
import { CAPTURAS } from "../datos/capturas";
import { PALETA } from "../datos/paleta";
import { PULSOS_ALARMA, T, bloque } from "../datos/tiempos";
import { FUENTE, tamClear, tamGreen } from "../fuentes";

const B = bloque("Climax");
const L = (f: number) => f - B.desde;

const JULIETA = {
  src: "assets/personajes/julieta-retrato.png",
  ancho: 100,
  alto: 100,
};
const SILUETA = 8;
// Los anteojos dentro de JULIETA.png (100×100)
const ANTEOJOS = { x: 35, y: 30, w: 32, h: 12 };

// Banda de texto de sistema para las reconstrucciones
const Aviso: React.FC<{ chico: string; grande: string; golpe?: number }> = ({
  chico,
  grande,
  golpe = 0,
}) => (
  <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
    <div
      style={{
        fontFamily: FUENTE.sistema,
        fontSize: tamGreen(4),
        lineHeight: `${tamGreen(4)}px`,
        color: PALETA.texto,
        marginBottom: 32,
      }}
    >
      {chico}
    </div>
    <div style={{ backgroundColor: PALETA.alarmaBanda, padding: "24px 72px" }}>
      <TextoGlitch
        texto={grande}
        fuente={FUENTE.titulo}
        tamano={tamClear(9)}
        golpes={[golpe]}
        caida={8}
        fuerza={14}
      />
    </div>
  </AbsoluteFill>
);

const ARENA = { x: 722, y: 380, w: 446, h: 356 };

const IntrusoArmado: React.FC = () => (
  <AbsoluteFill style={{ backgroundColor: "black" }}>
    <AbsoluteFill
      style={{
        justifyContent: "center",
        alignItems: "center",
        filter: "brightness(0.5)",
      }}
    >
      <PixelSprite
        src="assets/sede/julitile.png"
        ancho={1536}
        alto={1024}
        escala={2}
        recorte={ARENA}
      />
    </AbsoluteFill>
    <Aviso chico="ACCESO NO AUTORIZADO." grande="INTRUSO DETECTADO." />
  </AbsoluteFill>
);

const PuertaArmada: React.FC = () => {
  const frame = useCurrentFrame();
  const abre = L(T.puertaAbre) - L(T.puerta);
  const corrimiento =
    Math.round(
      interpolate(frame, [abre, abre + 24], [0, 420], {
        extrapolateLeft: "clamp",
        extrapolateRight: "clamp",
      }) / 4,
    ) * 4;
  return (
    <AbsoluteFill style={{ backgroundColor: PALETA.alarmaFondo }}>
      <div
        style={{
          position: "absolute",
          left: 540 - corrimiento,
          top: 0,
          width: 420,
          height: 1080,
          backgroundColor: PALETA.panelMedio,
          borderRight: `12px solid ${PALETA.pizarraBorde}`,
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 960 + corrimiento,
          top: 0,
          width: 420,
          height: 1080,
          backgroundColor: PALETA.panelMedio,
          borderLeft: `12px solid ${PALETA.pizarraBorde}`,
        }}
      />
      <Aviso
        chico="SECTOR RESTRINGIDO. SERVIDOR PRINCIPAL."
        grande="ACCESO EXCLUSIVO: JULIETA."
      />
    </AbsoluteFill>
  );
};

// La silueta de Julieta: negra, con contorno rojo de un píxel del arte, y en
// el beat 246 se le ven solo los anteojos.
const Silueta: React.FC<{ oscura?: boolean }> = ({ oscura = false }) => {
  const frame = useCurrentFrame();
  const brillo = frame >= L(T.anteojos) - L(T.silueta) && !oscura;
  const lado = JULIETA.ancho * SILUETA;
  return (
    <AbsoluteFill
      style={{
        backgroundColor: PALETA.alarmaFondo,
        boxShadow: "inset 0 0 400px 160px #000",
      }}
    >
      <div
        style={{
          position: "absolute",
          left: 960 - lado / 2,
          top: 1080 - lado,
          opacity: oscura ? 0.6 : 1,
        }}
      >
        <PixelSprite
          src={JULIETA.src}
          ancho={JULIETA.ancho}
          alto={JULIETA.alto}
          escala={SILUETA}
          style={{
            filter: `brightness(0) drop-shadow(${SILUETA}px 0 0 ${PALETA.alarma}) drop-shadow(-${SILUETA}px 0 0 ${PALETA.alarma}) drop-shadow(0 -${SILUETA}px 0 ${PALETA.alarma})`,
          }}
        />
        {brillo ? (
          <PixelSprite
            src={JULIETA.src}
            ancho={JULIETA.ancho}
            alto={JULIETA.alto}
            escala={SILUETA}
            recorte={ANTEOJOS}
            style={{
              position: "absolute",
              left: ANTEOJOS.x * SILUETA,
              top: ANTEOJOS.y * SILUETA,
              filter: "brightness(1.6)",
            }}
          />
        ) : null}
      </div>
    </AbsoluteFill>
  );
};

const RETRATO_JULIETA = { ...JULIETA, escala: 3 };

// 26-27: la caja de Julieta sobre el cuadro quieto del clip (o la silueta).
const CajaDeJulieta: React.FC = () => {
  const peleando = L(T.peleando) - L(T.cajaJulieta);
  const dura = L(T.corteSeco) - L(T.cajaJulieta);
  const frame = useCurrentFrame();
  const hayClip = useHayCaptura(CAPTURAS.julietaQuieta.archivo);
  return (
    <AbsoluteFill>
      <ClipOReconstruccion
        captura={CAPTURAS.julietaQuieta}
        duracion={dura}
        congelar
        reconstruccion={<Silueta oscura />}
      />
      {hayClip ? (
        <AbsoluteFill style={{ backgroundColor: "rgba(80,8,16,0.25)" }} />
      ) : null}
      <Glitch golpes={[peleando]} caida={8} fuerza={16} conNieve={false}>
        {frame < peleando ? (
          <CajaDeDialogo
            nombre="JULIETA"
            texto="De la misma forma que siempre."
            retrato={RETRATO_JULIETA}
            desde={2}
          />
        ) : (
          <Sequence from={peleando} layout="none">
            <CajaDeDialogo
              nombre="JULIETA"
              texto="Peleando."
              retrato={RETRATO_JULIETA}
              velocidad={60}
            />
          </Sequence>
        )}
      </Glitch>
      <Flash en={peleando} dura={3} color={PALETA.alarma} maximo={0.35} />
    </AbsoluteFill>
  );
};

// 44-53 s: la alarma, la puerta, Julieta y el corte seco.
export const Climax: React.FC = () => {
  const p = (desde: number, hasta: number) => ({
    from: L(desde),
    durationInFrames: hasta - desde,
  });
  const pulsos = PULSOS_ALARMA.map((f) => f - T.intruso);
  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <Sequence
        name="23 · Intruso detectado"
        {...p(T.intruso, T.puerta)}
        premountFor={30}
      >
        <ScreenShake golpes={[0]} fuerza={26}>
          <ClipOReconstruccion
            captura={CAPTURAS.intruso}
            duracion={T.puerta - T.intruso}
            reconstruccion={<IntrusoArmado />}
          />
        </ScreenShake>
        <AlarmaRoja pulsos={pulsos} intensidad={0.35} />
        <Flash en={0} dura={4} color={PALETA.alarma} maximo={0.6} />
      </Sequence>
      <Sequence
        name="24 · Acceso exclusivo: Julieta"
        {...p(T.puerta, T.silueta)}
        premountFor={30}
      >
        <Glitch golpes={[0]} caida={6} fuerza={18}>
          <ClipOReconstruccion
            captura={CAPTURAS.puerta}
            duracion={T.silueta - T.puerta}
            reconstruccion={<PuertaArmada />}
          />
        </Glitch>
      </Sequence>
      <Sequence name="25 · Silueta de Julieta" {...p(T.silueta, T.julietaClip)}>
        <Silueta />
      </Sequence>
      <Sequence
        name="25 · Julieta en la sala"
        {...p(T.julietaClip, T.cajaJulieta)}
        premountFor={30}
      >
        <ClipOReconstruccion
          captura={CAPTURAS.julieta}
          duracion={T.cajaJulieta - T.julietaClip}
          reconstruccion={<Silueta />}
        />
      </Sequence>
      <Sequence
        name="26-27 · De la misma forma que siempre. Peleando."
        {...p(T.cajaJulieta, T.corteSeco)}
        premountFor={30}
      >
        <CajaDeJulieta />
      </Sequence>
      {/* 28 · Corte seco: negro hasta el logo */}
    </AbsoluteFill>
  );
};
