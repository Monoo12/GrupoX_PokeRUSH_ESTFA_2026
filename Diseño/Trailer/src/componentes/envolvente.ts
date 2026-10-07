import { interpolate } from "remotion";

// Cuánto queda de un golpe a los `frame` frames: 1 en el golpe y baja a 0 en
// `caida` frames. Antes del golpe, 0.
export const despuesDelGolpe = (
  frame: number,
  golpe: number,
  caida: number,
): number => {
  if (frame < golpe) {
    return 0;
  }
  return interpolate(frame, [golpe, golpe + caida], [1, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
};

// El mayor de varios golpes (para efectos que se repiten en cada beat).
export const despuesDeGolpes = (
  frame: number,
  golpes: number[],
  caida: number,
): number =>
  golpes.reduce((m, g) => Math.max(m, despuesDelGolpe(frame, g, caida)), 0);

// Redondeo a píxel entero, para que nada de píxel art quede entre dos píxeles.
export const px = (n: number): number => Math.round(n);
