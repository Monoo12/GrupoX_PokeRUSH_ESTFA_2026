// El timeline del tráiler, en frames. Los cortes del montaje salen de los beats
// (public/beats.json) y el arranque lo empuja la duración de cada línea de voz
// (public/voz/duraciones.json). Los únicos números sueltos son decisiones de
// diseño del cold open, con nombre.
import { beat, MARCAS } from "./beats";
import { durVoz, type IdVoz } from "./voz";

export const ANCHO = 1920;
export const ALTO = 1080;

export const s = (seg: number): number => Math.round(seg * 30);

// --- Cold open: el tipeo de "2026." y el glitch a "2031."
const CURSOR_APARECE = s(0.5);
const TIPEO_2026 = s(0.8);
const GLITCH_2031 = s(2.3);
const QUEDA_2031 = s(2.8);
// La voz dice el año apenas queda "2031."
const VOZ_01 = QUEDA_2031;

// --- Desde acá, cada cosa espera a que termine la línea anterior. Los respiros
// son cortos porque las cuatro líneas tienen que entrar antes de la carta, que
// la fija la música.
const RESPIRO = 6;
const CHISPAZO = VOZ_01 + durVoz("01") + 2;
const FLASH_NUCA = CHISPAZO + 8;
const CIUDAD = FLASH_NUCA + 4;
const VOZ_02 = CIUDAD + RESPIRO;
const ENTRENADORES = VOZ_02 + durVoz("02") + RESPIRO;
const VOZ_03 = ENTRENADORES + 4;
const ESCANEO = VOZ_03 + durVoz("03") + 4;
const VOZ_04 = ESCANEO + 4;
// El ERROR entra cuando la voz está terminando "...ningún registro"
const ERROR_REGISTRO = VOZ_04 + durVoz("04") - 10;

const STINGER = MARCAS.fadeOutTermina.frame;

export const T = {
  cursorAparece: CURSOR_APARECE,
  tipeo2026: TIPEO_2026,
  glitch2031: GLITCH_2031,
  queda2031: QUEDA_2031,
  voz01: VOZ_01,
  chispazo: CHISPAZO,
  flashNuca: FLASH_NUCA,
  ciudad: CIUDAD,
  voz02: VOZ_02,
  entrenadores: ENTRENADORES,
  voz03: VOZ_03,
  escaneo: ESCANEO,
  voz04: VOZ_04,
  errorRegistro: ERROR_REGISTRO,

  // La carta: entra el build del tema (beat 164)
  carta: MARCAS.buildEntra.frame,
  caja: beat(174),
  // El último beat del build se silencia: el medio segundo de negro
  silencio: MARCAS.silencio.frame,
  drop: MARCAS.drop.frame,

  // Montaje
  red: beat(184),
  nodo2: beat(186),
  nodo3: beat(188),
  redLate: beat(190),
  protocolo: beat(192),
  cursorPelear: beat(194),
  confirma: beat(195),
  nestor: beat(196),
  nestorFurioso: beat(197),
  combate: beat(198),
  impacto: beat(200),
  superEficaz: beat(204),
  casino: beat(208),
  ruleta: beat(210),
  blackjack: beat(212),
  tacconi: beat(214),
  reymar: beat(218),
  accesoDenegado: beat(224),
  contador: beat(226),
  contador02: beat(227),
  contador01: beat(228),
  bajon: beat(233),
  led1: beat(234),
  led2: beat(235),

  // Clímax
  intruso: beat(236),
  puerta: beat(240),
  puertaAbre: beat(241),
  silueta: beat(244),
  julietaClip: beat(246),
  anteojos: beat(245),
  // "Ella te está esperando." entra un beat antes y cruza el corte a la silueta
  voz09: beat(243),
  cajaJulieta: beat(248),
  peleando: beat(252),
  corteSeco: beat(254),

  // Cierre
  logo: beat(256),
  voz10: beat(257),
  por2026: beat(260),
  creditos: beat(264),
  fadeOutEmpieza: MARCAS.fadeOutEmpieza.frame,
  stinger: STINGER,
  continuar: STINGER + s(0.4),
  fin: STINGER + s(2.0),
};

export const DURACION = T.fin;

// Los beats de la alarma (uno por beat entre el intruso y la puerta)
export const PULSOS_ALARMA = [236, 237, 238, 239].map(beat);

export const VOCES: { id: IdVoz; frame: number }[] = [
  { id: "01", frame: T.voz01 },
  { id: "02", frame: T.voz02 },
  { id: "03", frame: T.voz03 },
  { id: "04", frame: T.voz04 },
  { id: "05", frame: T.red },
  { id: "06", frame: T.protocolo },
  { id: "07", frame: T.casino },
  { id: "08", frame: T.contador01 },
  { id: "09", frame: T.voz09 },
  { id: "10", frame: T.voz10 },
  { id: "11", frame: T.por2026 },
];

export const BLOQUES = [
  { id: "ColdOpen", nombre: "Cold open", desde: 0, hasta: T.ciudad },
  { id: "ElMundo", nombre: "El mundo", desde: T.ciudad, hasta: T.carta },
  { id: "LaCarta", nombre: "La carta", desde: T.carta, hasta: T.silencio },
  {
    id: "SilencioYGolpe",
    nombre: "Silencio y golpe",
    desde: T.silencio,
    hasta: T.red,
  },
  { id: "Montaje", nombre: "Montaje", desde: T.red, hasta: T.intruso },
  { id: "Climax", nombre: "Clímax", desde: T.intruso, hasta: T.logo },
  { id: "Cierre", nombre: "Cierre", desde: T.logo, hasta: T.stinger },
  { id: "Stinger", nombre: "Stinger", desde: T.stinger, hasta: T.fin },
] as const;

export type IdBloque = (typeof BLOQUES)[number]["id"];

export const bloque = (id: IdBloque) => {
  const b = BLOQUES.find((x) => x.id === id);
  if (!b) {
    throw new Error(`No existe el bloque ${id}`);
  }
  return b;
};

// Los 31 planos de docs/diseno.md, con el frame que va al Storyboard.
export const PLANOS: {
  n: number;
  nombre: string;
  desde: number;
  hasta: number;
  clave: number;
}[] = [
  {
    n: 1,
    nombre: "Tipeo de 2026",
    desde: 0,
    hasta: T.glitch2031,
    clave: T.glitch2031 - 8,
  },
  {
    n: 2,
    nombre: "Glitch a 2031",
    desde: T.glitch2031,
    hasta: T.queda2031,
    clave: T.glitch2031 + 6,
  },
  {
    n: 3,
    nombre: "2031",
    desde: T.queda2031,
    hasta: T.chispazo,
    clave: T.voz01 + 20,
  },
  {
    n: 4,
    nombre: "Chispazo en la nuca",
    desde: T.chispazo,
    hasta: T.ciudad,
    clave: T.flashNuca - 3,
  },
  {
    n: 5,
    nombre: "La ciudad sincroniza",
    desde: T.ciudad,
    hasta: T.entrenadores,
    clave: T.entrenadores - 12,
  },
  {
    n: 6,
    nombre: "Gente con chip",
    desde: T.entrenadores,
    hasta: T.escaneo,
    clave: T.escaneo - 15,
  },
  {
    n: 7,
    nombre: "Usuario no encontrado",
    desde: T.escaneo,
    hasta: T.carta,
    clave: T.errorRegistro + 10,
  },
  {
    n: 8,
    nombre: "La carta",
    desde: T.carta,
    hasta: T.caja,
    clave: T.caja - 20,
  },
  {
    n: 9,
    nombre: "Caja ???",
    desde: T.caja,
    hasta: T.silencio,
    clave: T.silencio - 10,
  },
  {
    n: 10,
    nombre: "Silencio",
    desde: T.silencio,
    hasta: T.drop,
    clave: T.silencio + 5,
  },
  {
    n: 11,
    nombre: "Drop: el título",
    desde: T.drop,
    hasta: T.red,
    clave: T.drop + 6,
  },
  {
    n: 12,
    nombre: "Tres servidores",
    desde: T.red,
    hasta: T.protocolo,
    clave: T.redLate + 5,
  },
  {
    n: 13,
    nombre: "Protocolo de defensa",
    desde: T.protocolo,
    hasta: T.nestor,
    clave: T.confirma,
  },
  {
    n: 14,
    nombre: "Néstor",
    desde: T.nestor,
    hasta: T.combate,
    clave: T.nestorFurioso + 4,
  },
  {
    n: 15,
    nombre: "Combate",
    desde: T.combate,
    hasta: T.casino,
    clave: T.impacto + 3,
  },
  {
    n: 16,
    nombre: "Casino",
    desde: T.casino,
    hasta: T.tacconi,
    clave: T.ruleta + 12,
  },
  {
    n: 17,
    nombre: "Tacconi",
    desde: T.tacconi,
    hasta: T.reymar,
    clave: T.reymar - 10,
  },
  {
    n: 18,
    nombre: "Reymar",
    desde: T.reymar,
    hasta: T.accesoDenegado,
    clave: T.accesoDenegado - 15,
  },
  {
    n: 19,
    nombre: "Acceso denegado",
    desde: T.accesoDenegado,
    hasta: T.contador,
    clave: T.accesoDenegado + 8,
  },
  {
    n: 20,
    nombre: "Contador 03 a 02",
    desde: T.contador,
    hasta: T.contador01,
    clave: T.contador02 + 4,
  },
  {
    n: 21,
    nombre: "01 y la sede",
    desde: T.contador01,
    hasta: T.bajon,
    clave: T.contador01 + 30,
  },
  {
    n: 22,
    nombre: "Bajón",
    desde: T.bajon,
    hasta: T.intruso,
    clave: T.led1 + 2,
  },
  {
    n: 23,
    nombre: "Intruso detectado",
    desde: T.intruso,
    hasta: T.puerta,
    clave: T.intruso + 2,
  },
  {
    n: 24,
    nombre: "Acceso exclusivo: Julieta",
    desde: T.puerta,
    hasta: T.silueta,
    clave: T.puerta + 20,
  },
  {
    n: 25,
    nombre: "Silueta de Julieta",
    desde: T.silueta,
    hasta: T.cajaJulieta,
    clave: T.anteojos + 3,
  },
  {
    n: 26,
    nombre: "De la misma forma que siempre",
    desde: T.cajaJulieta,
    hasta: T.peleando,
    clave: T.peleando - 8,
  },
  {
    n: 27,
    nombre: "Peleando",
    desde: T.peleando,
    hasta: T.corteSeco,
    clave: T.peleando + 10,
  },
  {
    n: 28,
    nombre: "Corte seco",
    desde: T.corteSeco,
    hasta: T.logo,
    clave: T.corteSeco + 6,
  },
  {
    n: 29,
    nombre: "Logo y Por 2026",
    desde: T.logo,
    hasta: T.creditos,
    clave: T.por2026 + 20,
  },
  {
    n: 30,
    nombre: "Créditos y aviso",
    desde: T.creditos,
    hasta: T.stinger,
    clave: T.creditos + 30,
  },
  {
    n: 31,
    nombre: "¿Continuar?",
    desde: T.stinger,
    hasta: T.fin,
    clave: T.fin - 5,
  },
];

// Si una voz larga (por ejemplo, una grabación manual) no entra, que avise acá y
// no en el render.
const verificar = () => {
  if (T.errorRegistro > T.carta - 20) {
    throw new Error(
      `La voz 04 termina demasiado tarde: el ERROR caería en el frame ${T.errorRegistro} y la carta entra en ${T.carta}`,
    );
  }
  for (const b of BLOQUES) {
    if (b.hasta <= b.desde) {
      throw new Error(
        `El bloque ${b.id} quedó vacío o al revés (${b.desde}-${b.hasta})`,
      );
    }
  }
  const montaje = [
    { id: "05" as IdVoz, hasta: T.protocolo },
    { id: "06" as IdVoz, hasta: T.nestor },
    { id: "07" as IdVoz, hasta: T.tacconi },
    { id: "08" as IdVoz, hasta: T.bajon },
    { id: "09" as IdVoz, hasta: T.cajaJulieta },
    { id: "10" as IdVoz, hasta: T.por2026 },
    { id: "11" as IdVoz, hasta: T.stinger },
  ];
  for (const m of montaje) {
    const v = VOCES.find((x) => x.id === m.id)!;
    if (v.frame + durVoz(m.id) > m.hasta) {
      throw new Error(
        `La voz ${m.id} no entra en su plano: termina en ${v.frame + durVoz(m.id)} y el plano en ${m.hasta}`,
      );
    }
  }
};
verificar();
