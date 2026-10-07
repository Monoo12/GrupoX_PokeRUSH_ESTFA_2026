import { loadFont } from "@remotion/fonts";
import { staticFile } from "remotion";

// Las tres fuentes del juego. Son de píxel: Power Clear tiene una grilla de 13
// píxeles por em y Power Green de 12, así que los tamaños van en múltiplos para
// que cada píxel de la letra caiga en un píxel de la pantalla.
export const FUENTE = {
  caja: "PowerClear",
  titulo: "PowerClearBold",
  sistema: "PowerGreen",
};

export const tamClear = (n: number) => 13 * n;
export const tamGreen = (n: number) => 12 * n;

loadFont({
  family: FUENTE.caja,
  url: staticFile("assets/fuentes/power-clear.ttf"),
});
loadFont({
  family: FUENTE.titulo,
  url: staticFile("assets/fuentes/power-clear-bold.ttf"),
});
loadFont({
  family: FUENTE.sistema,
  url: staticFile("assets/fuentes/power-green.ttf"),
});
