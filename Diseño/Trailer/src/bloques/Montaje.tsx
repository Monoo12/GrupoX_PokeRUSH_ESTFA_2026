import React from "react";
import { AbsoluteFill, Sequence, interpolate, useCurrentFrame } from "remotion";
import { CajaDeDialogo } from "../componentes/CajaDeDialogo";
import { ClipOReconstruccion } from "../componentes/Captura";
import { Glitch } from "../componentes/Glitch";
import { Flash, NegroEnEscalones } from "../componentes/Overlays";
import { PixelSprite } from "../componentes/PixelSprite";
import { PlanoCombate } from "../componentes/PlanoCombate";
import { ScreenShake } from "../componentes/ScreenShake";
import { TextoGlitch } from "../componentes/TextoGlitch";
import { CAPTURAS } from "../datos/capturas";
import { PALETA } from "../datos/paleta";
import { T, bloque } from "../datos/tiempos";
import { FUENTE, tamClear, tamGreen } from "../fuentes";

const B = bloque("Montaje");
const L = (f: number) => f - B.desde;

const textoSistema = (
  n: number,
  color: string = PALETA.turquesa,
): React.CSSProperties => ({
  fontFamily: FUENTE.sistema,
  fontSize: tamGreen(n),
  lineHeight: `${tamGreen(n)}px`,
  color,
  whiteSpace: "pre",
});

// --- 12 · La red de servidores
const NODOS = [
  { x: 420, y: 470, etiqueta: "SERVIDOR 01 · DATA CENTER", aparece: T.red },
  {
    x: 1500,
    y: 380,
    etiqueta: "SERVIDOR 02 · CASINO",
    aparece: T.nodo2,
    aLaIzquierda: true,
  },
  {
    x: 960,
    y: 800,
    etiqueta: "SERVIDOR 03 · SEDE DE LA LIGA",
    aparece: T.nodo3,
  },
];

const Red: React.FC = () => {
  const frame = useCurrentFrame();
  const late = frame >= L(T.redLate);
  const activos = NODOS.filter((n) => frame >= L(n.aparece)).length;
  const pulso = late && Math.floor((frame - L(T.redLate)) / 4) % 2 === 0;
  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <AbsoluteFill style={{ filter: "brightness(0.3)" }}>
        <PixelSprite
          src="assets/menus/fondo-campeon.png"
          ancho={800}
          alto={800}
          escala={3}
          style={{ position: "absolute", left: -240, top: -1200 }}
        />
      </AbsoluteFill>
      <AbsoluteFill
        style={{
          backgroundImage: `repeating-linear-gradient(to right, rgba(56,248,200,0.10) 0 2px, transparent 2px 64px), repeating-linear-gradient(to bottom, rgba(56,248,200,0.10) 0 2px, transparent 2px 64px)`,
        }}
      />
      <svg width={1920} height={1080} style={{ position: "absolute" }}>
        {NODOS.slice(1).map((n, i) => {
          const a = NODOS[i];
          const largo = Math.hypot(n.x - a.x, n.y - a.y);
          const dibujado = interpolate(
            frame,
            [L(n.aparece), L(n.aparece) + 8],
            [0, 1],
            { extrapolateLeft: "clamp", extrapolateRight: "clamp" },
          );
          return (
            <line
              key={n.etiqueta}
              x1={a.x}
              y1={a.y}
              x2={a.x + (n.x - a.x) * dibujado}
              y2={a.y + (n.y - a.y) * dibujado}
              stroke={late ? PALETA.alarma : PALETA.turquesa}
              strokeWidth={6}
              strokeDasharray="18 12"
              strokeDashoffset={-frame * 3}
              opacity={largo > 0 ? 1 : 0}
            />
          );
        })}
      </svg>
      {NODOS.map((n) =>
        frame >= L(n.aparece) ? (
          <div
            key={n.etiqueta}
            style={{
              position: "absolute",
              left: n.x - 36,
              top: n.y - 36,
              width: 72,
            }}
          >
            <div
              style={{
                width: 72,
                height: 72,
                backgroundColor: late
                  ? pulso
                    ? PALETA.alarma
                    : PALETA.alarmaBanda
                  : PALETA.turquesa,
                border: `6px solid ${PALETA.texto}`,
                boxSizing: "border-box",
              }}
            />
            <div
              style={{
                ...textoSistema(4, late ? PALETA.alarma : PALETA.texto),
                position: "absolute",
                top: 12,
                ...("aLaIzquierda" in n ? { right: 96 } : { left: 96 }),
              }}
            >
              {n.etiqueta}
            </div>
          </div>
        ) : null,
      )}
      <div
        style={{ ...textoSistema(4), position: "absolute", left: 96, top: 72 }}
      >{`NEURARUSH-LINK · SERVIDORES ACTIVOS: 0${activos}`}</div>
      <Flash en={0} dura={3} maximo={0.6} />
    </AbsoluteFill>
  );
};

// --- 13 · Reconstrucción: la pregunta antes de pelear con Néstor
const Protocolo: React.FC = () => {
  const frame = useCurrentFrame();
  const enPelear = frame >= L(T.cursorPelear) - L(T.protocolo);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ filter: "brightness(0.5)" }}>
        <PixelSprite
          src="assets/combate/fondo-central.png"
          ancho={512}
          alto={384}
          escala={4}
          style={{ position: "absolute", left: -64, top: -228 }}
        />
      </AbsoluteFill>
      <CajaDeDialogo
        nombre="SISTEMA"
        texto="PROTOCOLO DE DEFENSA: LISTO."
        velocidad={60}
      />
      <div
        style={{
          position: "absolute",
          left: 1440,
          top: 500,
          width: 420,
          padding: "24px 32px",
          backgroundColor: "rgba(51,51,66,0.9)",
        }}
      >
        {["Pelear", "Todavía no"].map((op, i) => (
          <div
            key={op}
            style={{
              fontFamily: FUENTE.caja,
              fontSize: tamClear(4),
              lineHeight: `${tamClear(6)}px`,
              color: "#FFFFFF",
              whiteSpace: "pre",
            }}
          >
            {(enPelear ? i === 0 : i === 1) ? "> " : "  "}
            {op}
          </div>
        ))}
      </div>
    </AbsoluteFill>
  );
};

// --- 14 · Néstor
const RETRATOS_NESTOR = [
  { src: "assets/personajes/nestor-enojado.png", ancho: 302, alto: 301 },
  { src: "assets/personajes/nestor-furioso.png", ancho: 357, alto: 310 },
];

const Nestor: React.FC = () => {
  const frame = useCurrentFrame();
  const segundo = L(T.nestorFurioso) - L(T.nestor);
  const r = RETRATOS_NESTOR[frame < segundo ? 0 : 1];
  const led = Math.floor(frame / 3) % 2 === 0;
  return (
    <AbsoluteFill style={{ backgroundColor: PALETA.alarmaFondo }}>
      <Glitch golpes={[0, segundo]} caida={6} fuerza={20}>
        <AbsoluteFill
          style={{ justifyContent: "center", alignItems: "center" }}
        >
          <PixelSprite src={r.src} ancho={r.ancho} alto={r.alto} escala={2} />
        </AbsoluteFill>
      </Glitch>
      <div
        style={{
          position: "absolute",
          right: 160,
          top: 300,
          width: 24,
          height: 24,
          backgroundColor: PALETA.alarma,
          opacity: led ? 1 : 0.3,
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 0,
          right: 0,
          bottom: 70,
          textAlign: "center",
          fontFamily: FUENTE.titulo,
          fontSize: tamClear(8),
          lineHeight: `${tamClear(8)}px`,
          color: "#FFFFFF",
        }}
      >
        NÉSTOR
      </div>
    </AbsoluteFill>
  );
};

// --- 15 · Combate: el Palmeo de Lucario, la barra de PS que baja y "¡Es súper
// efectivo!". Un solo tramo del clip: más adelante el databox del juego ya dice
// "Raticate" con Porygon-Z todavía en pantalla.
const Combate: React.FC = () => {
  const total = T.casino - T.combate;
  const golpe = T.impacto - T.combate;
  return (
    <AbsoluteFill>
      <ScreenShake golpes={[golpe]} fuerza={28}>
        <ClipOReconstruccion
          captura={CAPTURAS.combate}
          duracion={total}
          reconstruccion={<PlanoCombate golpe={golpe} desmayo={total + 1} />}
        />
      </ScreenShake>
      <Flash en={golpe} dura={5} />
    </AbsoluteFill>
  );
};

// --- 16 · Casino (reconstrucciones)
const SIMBOLO = { w: 64, h: 48 };
const RODILLOS = [65, 145, 222];

const Tragamonedas: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill
      style={{
        backgroundColor: PALETA.fondo,
        justifyContent: "center",
        alignItems: "center",
      }}
    >
      <div style={{ position: "relative", width: 1024, height: 768 }}>
        <PixelSprite
          src="assets/casino/slots-maquina.png"
          ancho={512}
          alto={384}
          escala={2}
        />
        {RODILLOS.map((x, r) => {
          // Cada rodillo frena un poco después que el anterior y queda en el siete rojo (símbolo 6)
          const frena = 12 + r * 4;
          const giro = frame < frena ? Math.floor(frame * 1.5 + r * 3) : 0;
          return (
            <div
              key={x}
              style={{
                position: "absolute",
                left: x * 2,
                top: 112 * 2,
                width: SIMBOLO.w * 2,
                height: 132 * 2,
                overflow: "hidden",
              }}
            >
              {[0, 1, 2].map((fila) => {
                const simbolo =
                  frame < frena ? (giro + fila * 3) % 8 : [5, 6, 7][fila];
                return (
                  <PixelSprite
                    key={fila}
                    src="assets/casino/slots-simbolos.png"
                    ancho={512}
                    alto={48}
                    escala={2}
                    recorte={{
                      x: simbolo * SIMBOLO.w,
                      y: 0,
                      w: SIMBOLO.w,
                      h: SIMBOLO.h,
                    }}
                    style={{ position: "absolute", top: fila * 88 }}
                  />
                );
              })}
            </div>
          );
        })}
      </div>
    </AbsoluteFill>
  );
};

const Ruleta: React.FC = () => {
  const frame = useCurrentFrame();
  const giro = interpolate(frame, [0, 26], [0, 540], {
    extrapolateRight: "clamp",
  });
  return (
    <AbsoluteFill
      style={{
        backgroundColor: "#0B3B1E",
        justifyContent: "center",
        alignItems: "center",
      }}
    >
      <div style={{ position: "relative", width: 640, height: 640 }}>
        <PixelSprite
          src="assets/casino/ruleta-rueda.png"
          ancho={640}
          alto={640}
          escala={1}
          style={{ rotate: `${giro}deg` }}
        />
        <div
          style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 640,
            height: 640,
            rotate: `${-giro * 1.4}deg`,
          }}
        >
          <PixelSprite
            src="assets/casino/ruleta-bola.png"
            ancho={56}
            alto={57}
            escala={1}
            style={{ position: "absolute", left: 292, top: 70 }}
          />
        </div>
      </div>
    </AbsoluteFill>
  );
};

const Blackjack: React.FC = () => {
  const frame = useCurrentFrame();
  const entra = (desde: number) =>
    Math.round(
      interpolate(frame, [desde, desde + 5], [-400, 0], {
        extrapolateLeft: "clamp",
        extrapolateRight: "clamp",
      }),
    );
  return (
    <AbsoluteFill
      style={{
        backgroundColor: PALETA.fondo,
        justifyContent: "center",
        alignItems: "center",
      }}
    >
      <div style={{ position: "relative", width: 1140, height: 700 }}>
        <PixelSprite
          src="assets/casino/blackjack-mesa.png"
          ancho={570}
          alto={350}
          escala={2}
        />
        <PixelSprite
          src="assets/casino/carta-reverso.png"
          ancho={105}
          alto={150}
          escala={1}
          style={{ position: "absolute", left: 440, top: 300 + entra(4) }}
        />
        <PixelSprite
          src="assets/casino/carta-reverso.png"
          ancho={105}
          alto={150}
          escala={1}
          style={{ position: "absolute", left: 580, top: 300 + entra(14) }}
        />
      </div>
      {frame >= 18 ? (
        <div
          style={{
            position: "absolute",
            top: 140,
            fontFamily: FUENTE.titulo,
            fontSize: tamClear(8),
            lineHeight: `${tamClear(8)}px`,
            color: PALETA.psAmarillo,
          }}
        >
          BLACKJACK
        </div>
      ) : null}
    </AbsoluteFill>
  );
};

const Casino: React.FC = () => {
  const ruleta = L(T.ruleta) - L(T.casino);
  const blackjack = L(T.blackjack) - L(T.casino);
  const fin = L(T.tacconi) - L(T.casino);
  return (
    <AbsoluteFill>
      <Sequence durationInFrames={ruleta}>
        <ClipOReconstruccion
          captura={CAPTURAS.tragamonedas}
          duracion={ruleta}
          reconstruccion={<Tragamonedas />}
        />
      </Sequence>
      <Sequence
        from={ruleta}
        durationInFrames={blackjack - ruleta}
        premountFor={20}
      >
        <ClipOReconstruccion
          captura={CAPTURAS.ruleta}
          duracion={blackjack - ruleta}
          reconstruccion={<Ruleta />}
        />
      </Sequence>
      <Sequence
        from={blackjack}
        durationInFrames={fin - blackjack}
        premountFor={20}
      >
        <ClipOReconstruccion
          captura={CAPTURAS.blackjack}
          duracion={fin - blackjack}
          reconstruccion={<Blackjack />}
        />
      </Sequence>
    </AbsoluteFill>
  );
};

// --- 17 y 18 · Reconstrucciones de las cajas de Tacconi y Reymar
const CajaSobreFondo: React.FC<{
  fondo: string;
  nombre: string;
  texto: string;
  retrato: string;
}> = ({ fondo, nombre, texto, retrato }) => (
  <AbsoluteFill>
    <AbsoluteFill style={{ filter: "brightness(0.55)" }}>
      <PixelSprite
        src={fondo}
        ancho={512}
        alto={384}
        escala={4}
        style={{ position: "absolute", left: -64, top: -228 }}
      />
    </AbsoluteFill>
    <CajaDeDialogo
      nombre={nombre}
      texto={texto}
      retrato={{ src: retrato, ancho: 160, alto: 160, escala: 2 }}
    />
  </AbsoluteFill>
);

// --- 19 · ACCESO DENEGADO
const AccesoDenegado: React.FC = () => (
  <AbsoluteFill
    style={{
      backgroundColor: PALETA.alarmaFondo,
      justifyContent: "center",
      alignItems: "center",
    }}
  >
    <div style={{ ...textoSistema(4, PALETA.texto), marginBottom: 40 }}>
      IDENTIFICACIÓN REQUERIDA. ADMINISTRADOR REYMAR.
    </div>
    <div style={{ backgroundColor: PALETA.alarmaBanda, padding: "26px 80px" }}>
      <TextoGlitch
        texto="ACCESO DENEGADO."
        fuente={FUENTE.titulo}
        tamano={tamClear(10)}
        golpes={[0, 6]}
        caida={6}
        fuerza={16}
      />
    </div>
  </AbsoluteFill>
);

// --- 20 y 21 · El contador
const Contador: React.FC<{ valor: string; rojo: boolean; chico?: boolean }> = ({
  valor,
  rojo,
  chico = false,
}) => (
  <div style={{ display: "flex", alignItems: "baseline", gap: 36 }}>
    <div style={textoSistema(chico ? 4 : 6, PALETA.texto)}>
      SERVIDORES RESTANTES:
    </div>
    <div
      style={textoSistema(
        chico ? 8 : 20,
        rojo ? PALETA.alarma : PALETA.turquesa,
      )}
    >
      {valor}
    </div>
  </div>
);

const ContadorSobreClip: React.FC = () => {
  const frame = useCurrentFrame();
  const cambio = L(T.contador02) - L(T.contador);
  return (
    <AbsoluteFill>
      <AbsoluteFill style={{ filter: "brightness(0.35)" }}>
        <ClipOReconstruccion
          captura={CAPTURAS.desconexion}
          duracion={L(T.contador01) - L(T.contador)}
          reconstruccion={
            <AbsoluteFill style={{ backgroundColor: PALETA.fondo }} />
          }
        />
      </AbsoluteFill>
      <Glitch golpes={[0, cambio]} caida={6} fuerza={18} conNieve={false}>
        <AbsoluteFill
          style={{ justifyContent: "center", alignItems: "center" }}
        >
          <Contador
            valor={frame < cambio ? "03" : "02"}
            rojo={frame >= cambio}
          />
        </AbsoluteFill>
      </Glitch>
      <Flash en={cambio} dura={4} color={PALETA.alarma} maximo={0.5} />
    </AbsoluteFill>
  );
};

// La sede: la arena de juliTILE.png (la de Santiago), teñida de rojo
const ARENA = { x: 722, y: 380, w: 446, h: 356 };

const Sede: React.FC = () => {
  const frame = useCurrentFrame();
  const bajon = L(T.bajon) - L(T.contador01);
  const led = [L(T.led1) - L(T.contador01), L(T.led2) - L(T.contador01)].some(
    (f) => frame >= f && frame < f + 6,
  );
  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <Glitch golpes={[0]} caida={8} fuerza={20} conNieve={false}>
        <AbsoluteFill
          style={{
            justifyContent: "center",
            alignItems: "center",
            paddingTop: 120,
          }}
        >
          <div style={{ position: "relative" }}>
            <PixelSprite
              src="assets/sede/julitile.png"
              ancho={1536}
              alto={1024}
              escala={2}
              recorte={ARENA}
            />
            <AbsoluteFill
              style={{
                backgroundColor: PALETA.alarma,
                mixBlendMode: "multiply",
                opacity: 0.55,
              }}
            />
          </div>
        </AbsoluteFill>
      </Glitch>
      <div
        style={{
          position: "absolute",
          top: 40,
          left: 0,
          right: 0,
          display: "flex",
          justifyContent: "center",
        }}
      >
        <Contador valor="01" rojo chico />
      </div>
      <div
        style={{
          ...textoSistema(4, PALETA.texto),
          position: "absolute",
          bottom: 40,
          left: 0,
          right: 0,
          textAlign: "center",
        }}
      >
        SEDE CENTRAL DE LA LIGA · AMEGHINO
      </div>
      <NegroEnEscalones desde={bajon} hasta={bajon + 12} />
      {led ? (
        <AbsoluteFill
          style={{ justifyContent: "center", alignItems: "center" }}
        >
          <div
            style={{ width: 32, height: 32, backgroundColor: PALETA.alarma }}
          />
        </AbsoluteFill>
      ) : null}
    </AbsoluteFill>
  );
};

// 22-44 s: el montaje, con cada corte en un beat.
export const Montaje: React.FC = () => {
  const p = (desde: number, hasta: number) => ({
    from: L(desde),
    durationInFrames: hasta - desde,
  });
  return (
    <AbsoluteFill style={{ backgroundColor: "black" }}>
      <Sequence name="12 · Tres servidores" {...p(T.red, T.protocolo)}>
        <Red />
      </Sequence>
      <Sequence
        name="13 · Protocolo de defensa"
        {...p(T.protocolo, T.nestor)}
        premountFor={30}
      >
        <ClipOReconstruccion
          captura={CAPTURAS.protocolo}
          duracion={T.nestor - T.protocolo}
          reconstruccion={<Protocolo />}
        />
      </Sequence>
      <Sequence name="14 · Néstor" {...p(T.nestor, T.combate)}>
        <Nestor />
      </Sequence>
      <Sequence
        name="15 · Combate"
        {...p(T.combate, T.casino)}
        premountFor={30}
      >
        <Combate />
      </Sequence>
      <Sequence name="16 · Casino" {...p(T.casino, T.tacconi)} premountFor={30}>
        <Casino />
      </Sequence>
      <Sequence
        name="17 · Tacconi"
        {...p(T.tacconi, T.reymar)}
        premountFor={30}
      >
        <ClipOReconstruccion
          captura={CAPTURAS.tacconi}
          duracion={T.reymar - T.tacconi}
          reconstruccion={
            <CajaSobreFondo
              fondo="assets/casino/mesa-fieltro.png"
              nombre="TACCONI"
              texto="¿Te acordás de 2026?"
              retrato="assets/personajes/tacconi-retrato.png"
            />
          }
        />
      </Sequence>
      <Sequence
        name="18 · Reymar"
        {...p(T.reymar, T.accesoDenegado)}
        premountFor={30}
      >
        <ClipOReconstruccion
          captura={CAPTURAS.reymar}
          duracion={T.accesoDenegado - T.reymar}
          reconstruccion={
            <CajaSobreFondo
              fondo="assets/combate/fondo-interior-rojo.png"
              nombre="REYMAR"
              texto="Porque yo participé en tu preparación."
              retrato="assets/personajes/reymar-retrato.png"
            />
          }
        />
      </Sequence>
      <Sequence
        name="19 · Acceso denegado"
        {...p(T.accesoDenegado, T.contador)}
      >
        <AccesoDenegado />
        <Flash en={0} dura={3} color={PALETA.alarma} maximo={0.7} />
      </Sequence>
      <Sequence
        name="20 · Contador 03 a 02"
        {...p(T.contador, T.contador01)}
        premountFor={30}
      >
        <ContadorSobreClip />
      </Sequence>
      <Sequence
        name="21-22 · 01, la sede y el bajón"
        {...p(T.contador01, T.intruso)}
      >
        <Sede />
      </Sequence>
    </AbsoluteFill>
  );
};
