import React from "react";
import { AbsoluteFill, Sequence, interpolate, useCurrentFrame } from "remotion";
import { ClipOReconstruccion } from "../componentes/Captura";
import { Glitch } from "../componentes/Glitch";
import { Flash } from "../componentes/Overlays";
import { PixelSprite } from "../componentes/PixelSprite";
import { TextoGlitch } from "../componentes/TextoGlitch";
import { CAPTURAS } from "../datos/capturas";
import { PALETA } from "../datos/paleta";
import { T, bloque } from "../datos/tiempos";
import { FUENTE, tamGreen } from "../fuentes";

const B = bloque("ElMundo");
const L = (f: number) => f - B.desde;

// fondo-campeon.png (800×800) a ×3: el paneo baja del cielo al skyline de a
// un píxel del arte (3 píxeles de pantalla), así nunca queda entre dos píxeles.
const Ciudad: React.FC<{ oscuro?: number; desfase?: number }> = ({
  oscuro = 0,
  desfase = 0,
}) => {
  const frame = useCurrentFrame() + desfase;
  const fila = Math.round(
    interpolate(frame, [0, L(T.escaneo)], [150, 400], {
      extrapolateLeft: "clamp",
      extrapolateRight: "clamp",
    }),
  );
  return (
    <AbsoluteFill
      style={{ filter: oscuro ? `brightness(${1 - oscuro})` : undefined }}
    >
      <PixelSprite
        src="assets/menus/fondo-campeon.png"
        ancho={800}
        alto={800}
        escala={3}
        style={{
          position: "absolute",
          left: (1920 - 2400) / 2,
          top: -fila * 3,
        }}
      />
    </AbsoluteFill>
  );
};

// Panel de sistema con los colores de panels.png
const Panel: React.FC<{
  children: React.ReactNode;
  style?: React.CSSProperties;
}> = ({ children, style }) => (
  <div
    style={{
      position: "absolute",
      backgroundColor: "rgba(24,24,24,0.88)",
      border: `6px solid ${PALETA.turquesa}`,
      boxShadow: `inset 0 0 0 6px ${PALETA.turquesaOscuro}`,
      padding: "24px 36px",
      ...style,
    }}
  >
    {children}
  </div>
);

const Sincronizando: React.FC = () => {
  const frame = useCurrentFrame();
  const avance = interpolate(frame, [6, L(T.entrenadores) - 6], [0, 98.7], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const puntos = ".".repeat(1 + (Math.floor(frame / 8) % 3));
  const lleno = Math.round(avance / 4); // bloques de la barra
  return (
    <Panel style={{ left: 96, top: 96 }}>
      <div
        style={{
          fontFamily: FUENTE.sistema,
          fontSize: tamGreen(4),
          lineHeight: `${tamGreen(4)}px`,
          color: PALETA.turquesa,
          whiteSpace: "pre",
        }}
      >
        {`NeuraRush-LINK: SINCRONIZANDO${puntos}`}
      </div>
      <div style={{ display: "flex", gap: 6, marginTop: 24 }}>
        {Array.from({ length: 25 }, (_, i) => (
          <div
            key={i}
            style={{
              width: 24,
              height: 36,
              backgroundColor: i < lleno ? PALETA.turquesa : PALETA.panelMedio,
            }}
          />
        ))}
      </div>
      <div
        style={{
          fontFamily: FUENTE.sistema,
          fontSize: tamGreen(3),
          lineHeight: `${tamGreen(3)}px`,
          color: PALETA.texto,
          marginTop: 24,
        }}
      >
        {`${avance.toFixed(1).replace(".", ",")} % DE LA POBLACIÓN`}
      </div>
    </Panel>
  );
};

const ENTRENADORES: { src: string; lado: number }[] = [
  { src: "assets/entrenadores/chico.png", lado: 160 },
  { src: "assets/entrenadores/cientifica.png", lado: 160 },
  { src: "assets/entrenadores/jugon.png", lado: 160 },
  { src: "assets/entrenadores/damisela.png", lado: 160 },
  { src: "assets/entrenadores/chica.png", lado: 160 },
];

// La gente con chip: a cada uno le titila el LED de la nuca y le pasa un glitch.
const GenteConChip: React.FC = () => {
  const frame = useCurrentFrame();
  const tipeo = (texto: string, desde: number) =>
    texto.slice(0, Math.max(0, Math.floor((frame - desde) * 1.6)));
  return (
    <AbsoluteFill>
      <Ciudad oscuro={0.5} desfase={L(T.entrenadores)} />
      {ENTRENADORES.map((e, i) => {
        const golpe = 4 + i * 12;
        const x = 80 + i * 360;
        const led = frame >= golpe && Math.floor((frame - golpe) / 4) % 2 === 0;
        return (
          <div
            key={e.src}
            style={{
              position: "absolute",
              left: x,
              top: 1080 - e.lado * 2 - 60,
              width: e.lado * 2,
              height: e.lado * 2,
            }}
          >
            <Glitch
              golpes={[golpe]}
              caida={8}
              fuerza={14}
              conNieve={false}
              semilla={"gente" + i}
            >
              <PixelSprite
                src={e.src}
                ancho={e.lado}
                alto={e.lado}
                escala={2}
              />
            </Glitch>
            <div
              style={{
                position: "absolute",
                left: e.lado - 4,
                top: 96,
                width: 8,
                height: 8,
                backgroundColor: PALETA.alarma,
                opacity: led ? 1 : 0.25,
              }}
            />
          </div>
        );
      })}
      <Panel style={{ left: 96, top: 96 }}>
        <div
          style={{
            fontFamily: FUENTE.sistema,
            fontSize: tamGreen(4),
            lineHeight: `${tamGreen(5)}px`,
            color: PALETA.turquesa,
            whiteSpace: "pre",
          }}
        >
          {tipeo("RECUERDOS: SINCRONIZADOS.", 0)}
          {"\n"}
          <span style={{ color: PALETA.alarma }}>
            {tipeo("ÚLTIMA EDICIÓN: HACE 4 MINUTOS", 18)}
          </span>
        </div>
      </Panel>
    </AbsoluteFill>
  );
};

// Reconstrucción del plano 7: el jugador solo, escaneado, y el ERROR.
const Escaneo: React.FC = () => {
  const frame = useCurrentFrame();
  const error = L(T.errorRegistro) - L(T.escaneo);
  const barrido = Math.round(interpolate(frame % 40, [0, 40], [180, 900]));
  const texto =
    frame < error
      ? "USUARIO DETECTADO. BUSCANDO CHIP... BUSCANDO CHIP..."
      : "ERROR: USUARIO NO ENCONTRADO EN NINGÚN REGISTRO.";
  return (
    <AbsoluteFill style={{ backgroundColor: PALETA.fondo }}>
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
        <PixelSprite
          src="assets/personajes/jugador-rojo.png"
          ancho={256}
          alto={256}
          escala={5}
          recorte={{ x: 128, y: 0, w: 64, h: 64 }}
        />
      </AbsoluteFill>
      {frame < error ? (
        <div
          style={{
            position: "absolute",
            left: 0,
            top: barrido,
            width: 1920,
            height: 6,
            backgroundColor: PALETA.turquesa,
            opacity: 0.8,
          }}
        />
      ) : null}
      <AbsoluteFill
        style={{
          justifyContent: "flex-end",
          alignItems: "center",
          paddingBottom: 120,
        }}
      >
        <TextoGlitch
          texto={texto}
          fuente={FUENTE.sistema}
          tamano={tamGreen(4)}
          color={frame < error ? PALETA.turquesa : PALETA.alarma}
          golpes={[error]}
          fuerza={12}
        />
      </AbsoluteFill>
    </AbsoluteFill>
  );
};

// 5-14 s: la ciudad, la gente con chip y el jugador que no figura en ningún registro.
export const ElMundo: React.FC = () => {
  const escaneo = L(T.escaneo);
  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <Sequence name="5 · Ciudad" durationInFrames={L(T.entrenadores)}>
        <Ciudad />
        <Sequence from={10} layout="none">
          <Sincronizando />
        </Sequence>
      </Sequence>
      <Sequence
        name="6 · Gente con chip"
        from={L(T.entrenadores)}
        durationInFrames={L(T.escaneo) - L(T.entrenadores)}
      >
        <GenteConChip />
      </Sequence>
      <Sequence
        name="7 · Usuario no encontrado"
        from={escaneo}
        durationInFrames={B.hasta - T.escaneo}
        premountFor={30}
      >
        <Glitch golpes={[L(T.errorRegistro) - escaneo]} caida={10} fuerza={20}>
          <ClipOReconstruccion
            captura={CAPTURAS.escaneo}
            duracion={B.hasta - T.escaneo}
            reconstruccion={<Escaneo />}
          />
        </Glitch>
        <Flash
          en={L(T.errorRegistro) - escaneo}
          dura={4}
          color={PALETA.alarma}
          maximo={0.45}
        />
      </Sequence>
      <Flash en={0} dura={4} />
    </AbsoluteFill>
  );
};
