// Qué clip de capturas/ usa cada plano y desde qué segundo del clip.
// Los momentos "medidos" se sacaron de los clips cuadro a cuadro (cuándo cambia
// la caja de diálogo, cuándo pega el golpe): ver docs/diseno.md → "Clips".
import { T } from "./tiempos";

export type Captura = {
  archivo: string;
  desdeSeg: number;
};

const MEDIDO = {
  // 04-pantallas: entra "ERROR: USUARIO NO ENCONTRADO EN NINGÚN REGISTRO."
  pantallasError: 6.067,
  // 03-tixi: aparece la caja "??? Aaah... conque recibiste mi carta."
  tixiCaja: 1.733,
  // 05-protocolo: aparece "SISTEMA: PROTOCOLO DE DEFENSA: LISTO." con Pelear / Todavía no
  protocoloCaja: 16.8,
  // 06-combate: el Palmeo de Lucario le pega a Porygon-Z
  combateImpacto: 3.8,
  // 09-tacconi: "Antes de jugar, quiero preguntarte algo. ¿Te acordás de 2026?"
  tacconiPregunta: 8.7,
  // 10-reymar: "Porque yo participé en tu preparación."
  reymarPreparacion: 13.133,
  // 13-puerta: "SECTOR RESTRINGIDO. SERVIDOR PRINCIPAL. ACCESO EXCLUSIVO: JULIETA."
  puertaSistema: 2.1,
  // 14-julieta: la cámara ya llegó y todavía no hay caja
  julietaQuieta: 0.95,
};

const aSeg = (frames: number) => frames / 30;

export const CAPTURAS = {
  // Plano 7: que el ERROR caiga cuando lo pide el timeline
  escaneo: {
    archivo: "04-pantallas.mp4",
    desdeSeg: MEDIDO.pantallasError - aSeg(T.errorRegistro - T.escaneo),
  },
  // Plano 8: la carta (dos cajas de print)
  carta: { archivo: "02-carta.mp4", desdeSeg: 1.2 },
  // Plano 9: la caja "???" entra a los 4 frames del corte
  caja: { archivo: "03-tixi.mp4", desdeSeg: MEDIDO.tixiCaja - aSeg(4) },
  // Plano 11: la pantalla de título
  titulo: { archivo: "01-titulo.mp4", desdeSeg: 0 },
  // Plano 13
  protocolo: { archivo: "05-protocolo.mp4", desdeSeg: MEDIDO.protocoloCaja },
  // Plano 15: el impacto cae en el beat 200
  combate: {
    archivo: "06-combate.mp4",
    desdeSeg: MEDIDO.combateImpacto - aSeg(T.impacto - T.combate),
  },
  // Plano 16
  tragamonedas: { archivo: "07-tragamonedas.mp4", desdeSeg: 1.4 },
  ruleta: { archivo: "08-ruleta.mp4", desdeSeg: 11.3 },
  blackjack: { archivo: "09-tacconi.mp4", desdeSeg: 18.2 },
  // Planos 17 y 18
  tacconi: { archivo: "09-tacconi.mp4", desdeSeg: MEDIDO.tacconiPregunta },
  reymar: { archivo: "10-reymar.mp4", desdeSeg: MEDIDO.reymarPreparacion },
  // Plano 20: de fondo del contador (la desconexión de Reymar)
  desconexion: { archivo: "11-acceso-denegado.mp4", desdeSeg: 2.5 },
  // Planos 23 a 26
  intruso: { archivo: "12-intruso.mp4", desdeSeg: 0 },
  puerta: { archivo: "13-puerta.mp4", desdeSeg: MEDIDO.puertaSistema },
  julieta: { archivo: "14-julieta.mp4", desdeSeg: 0 },
  julietaQuieta: { archivo: "14-julieta.mp4", desdeSeg: MEDIDO.julietaQuieta },
} satisfies Record<string, Captura>;
