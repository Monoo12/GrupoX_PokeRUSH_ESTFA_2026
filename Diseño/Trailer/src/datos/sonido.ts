// Efectos en su frame exacto y la automatización de volumen de la música.
import { interpolate } from "remotion";
import { PULSOS_ALARMA, T, VOCES } from "./tiempos";
import { durVoz } from "./voz";

export type Efecto = {
  archivo: string;
  frame: number;
  volumen: number;
  // Cuántos frames suena como mucho (se corta con un fade corto)
  dura?: number;
  // Golpe fuerte: no puede pisar una línea de voz (lo chequea verificarSonido)
  fuerte?: boolean;
};

const sfx = (archivo: string) => `assets/sfx/${archivo}`;

export const EFECTOS: Efecto[] = [
  // Cold open
  { archivo: sfx("glitch.wav"), frame: T.glitch2031, volumen: 0.5 },
  {
    archivo: sfx("chispazo.wav"),
    frame: T.glitch2031 + 2,
    volumen: 0.35,
    dura: 12,
  },
  { archivo: sfx("chispazo.wav"), frame: T.chispazo, volumen: 0.6, dura: 14 },
  {
    archivo: sfx("chip-apagado.ogg"),
    frame: T.flashNuca,
    volumen: 0.9,
    fuerte: true,
  },
  {
    archivo: sfx("golpe-kizaru.wav"),
    frame: T.flashNuca,
    volumen: 0.85,
    fuerte: true,
  },

  // El mundo
  { archivo: sfx("transmisor.ogg"), frame: T.ciudad + 10, volumen: 0.45 },
  ...[0, 1, 2, 3, 4].map((i) => ({
    archivo: sfx("chispazo.wav"),
    frame: T.entrenadores + 4 + i * 12,
    volumen: 0.16,
    dura: 8,
  })),
  { archivo: sfx("menu-error.ogg"), frame: T.errorRegistro, volumen: 0.75 },
  { archivo: sfx("glitch.wav"), frame: T.errorRegistro, volumen: 0.4 },

  // La carta: el "X" que pasa a la segunda caja del clip y la caja de "???"
  { archivo: sfx("menu-cursor.ogg"), frame: T.carta + 68, volumen: 0.35 },
  { archivo: sfx("menu-abrir.ogg"), frame: T.caja + 4, volumen: 0.4 },

  // Drop
  {
    archivo: sfx("golpe-kizaru.wav"),
    frame: T.drop,
    volumen: 0.7,
    fuerte: true,
  },

  // Montaje
  { archivo: sfx("transmisor.ogg"), frame: T.red, volumen: 0.3 },
  { archivo: sfx("transmisor.ogg"), frame: T.nodo2, volumen: 0.4 },
  { archivo: sfx("transmisor.ogg"), frame: T.nodo3, volumen: 0.4 },
  { archivo: sfx("chispazo.wav"), frame: T.redLate, volumen: 0.3, dura: 10 },
  {
    archivo: sfx("seleccion.mp3"),
    frame: T.nestor - 3,
    volumen: 0.45,
    dura: 24,
  },
  { archivo: sfx("chispazo.wav"), frame: T.nestor, volumen: 0.45, dura: 10 },
  {
    archivo: sfx("chispazo.wav"),
    frame: T.nestorFurioso,
    volumen: 0.5,
    dura: 10,
  },
  { archivo: sfx("palmeo.wav"), frame: T.combate + 14, volumen: 0.6 },
  {
    archivo: sfx("golpe-super-eficaz.ogg"),
    frame: T.impacto,
    volumen: 1,
    fuerte: true,
  },
  {
    archivo: sfx("slots-moneda.ogg"),
    frame: T.casino,
    volumen: 0.16,
    dura: 24,
  },
  {
    archivo: sfx("ruleta-girando.mp3"),
    frame: T.ruleta,
    volumen: 0.5,
    dura: 26,
  },
  { archivo: sfx("carta.mp3"), frame: T.blackjack + 4, volumen: 0.6 },
  { archivo: sfx("carta.mp3"), frame: T.blackjack + 14, volumen: 0.6 },
  { archivo: sfx("menu-cursor.ogg"), frame: T.tacconi, volumen: 0.3 },
  { archivo: sfx("menu-cursor.ogg"), frame: T.reymar, volumen: 0.3 },
  {
    archivo: sfx("menu-error.ogg"),
    frame: T.accesoDenegado,
    volumen: 0.85,
    fuerte: true,
  },
  { archivo: sfx("glitch.wav"), frame: T.accesoDenegado, volumen: 0.5 },
  { archivo: sfx("pc-apagar.ogg"), frame: T.contador, volumen: 0.6 },
  {
    archivo: sfx("nucleo-destruido.wav"),
    frame: T.contador02,
    volumen: 0.65,
    dura: 12,
    fuerte: true,
  },
  { archivo: sfx("pc-apagar.ogg"), frame: T.contador01, volumen: 0.25 },
  { archivo: sfx("transmisor.ogg"), frame: T.led1, volumen: 0.3 },
  { archivo: sfx("transmisor.ogg"), frame: T.led2, volumen: 0.3 },

  // Clímax
  ...PULSOS_ALARMA.map((frame) => ({
    archivo: sfx("alarma.ogg"),
    frame,
    volumen: 0.7,
    fuerte: true,
  })),
  {
    archivo: sfx("puerta-se-abre.ogg"),
    frame: T.puertaAbre,
    volumen: 0.8,
    fuerte: true,
  },
  {
    archivo: sfx("grito-mewtwo.ogg"),
    frame: T.voz09 + durVoz("09") + 2,
    volumen: 0.3,
    dura: 40,
  },
  { archivo: sfx("menu-abrir.ogg"), frame: T.cajaJulieta, volumen: 0.35 },
  { archivo: sfx("chispazo.wav"), frame: T.peleando, volumen: 0.4, dura: 10 },

  // Cierre
  { archivo: sfx("golpe-kizaru.wav"), frame: T.logo, volumen: 1, fuerte: true },
];

// --- Música
const DUCK_VOZ = 0.316; // -10 dB
const DUCK_TEXTO = 0.5; // -6 dB
const RAMPA = 4;

const ventanasDeVoz = VOCES.map((v) => ({
  desde: v.frame,
  hasta: v.frame + durVoz(v.id),
}));
// Golpes de texto: la música se corre medio segundo para que se oigan
const golpesDeTexto = [T.accesoDenegado, T.intruso, T.puerta, T.peleando].map(
  (f) => ({ desde: f, hasta: f + 14 }),
);

const ganancia = (
  frame: number,
  ventanas: { desde: number; hasta: number }[],
  piso: number,
): number =>
  ventanas.reduce((g, w) => {
    const dentro = interpolate(
      frame,
      [w.desde - RAMPA, w.desde, w.hasta, w.hasta + RAMPA * 2],
      [0, 1, 1, 0],
      {
        extrapolateLeft: "clamp",
        extrapolateRight: "clamp",
      },
    );
    return Math.min(g, 1 - dentro * (1 - piso));
  }, 1);

// Volumen del tema de Kizaru en el frame `frame` del tráiler.
export const volumenTema = (frame: number): number => {
  if (frame >= T.silencio && frame < T.drop) {
    return 0;
  }
  if (frame >= T.corteSeco && frame < T.logo) {
    return 0;
  }
  const base = 0.85;
  const fade = interpolate(frame, [T.fadeOutEmpieza, T.stinger], [1, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  return (
    base *
    fade *
    ganancia(frame, ventanasDeVoz, DUCK_VOZ) *
    ganancia(frame, golpesDeTexto, DUCK_TEXTO)
  );
};

// Volumen de la cama del arranque (el tema del menú filtrado).
export const volumenCama = (frame: number): number => {
  const entrada = interpolate(frame, [T.ciudad, T.ciudad + 45], [0, 0.55], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const salida = interpolate(frame, [T.carta, T.carta + 9], [1, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  return entrada * salida * ganancia(frame, ventanasDeVoz, DUCK_VOZ);
};

// Volumen del zumbido del cold open.
export const volumenZumbido = (frame: number): number => {
  const env = interpolate(
    frame,
    [0, 12, T.ciudad, T.ciudad + 20],
    [0, 0.32, 0.32, 0],
    {
      extrapolateLeft: "clamp",
      extrapolateRight: "clamp",
    },
  );
  return env * ganancia(frame, ventanasDeVoz, 0.5);
};

// Ningún golpe fuerte encima de una línea de voz.
const verificarSonido = () => {
  for (const e of EFECTOS.filter((x) => x.fuerte)) {
    for (const v of VOCES) {
      const hasta = v.frame + durVoz(v.id);
      if (e.frame > v.frame + 2 && e.frame < hasta - 2) {
        throw new Error(
          `El efecto ${e.archivo} (frame ${e.frame}) pisa la voz ${v.id} (${v.frame}-${hasta})`,
        );
      }
    }
  }
};
verificarSonido();
