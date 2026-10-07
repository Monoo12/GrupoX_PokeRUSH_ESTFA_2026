import beats from "../../public/beats.json";

// Todo el tráiler corre a esta velocidad (también el Storyboard).
export const FPS = beats.fps;

const porIndice = new Map(beats.beatsTrailer.map((b) => [b.beat, b]));

// Frame del tráiler en el que cae el beat `i` del tema (índice de beats.json).
export const beat = (i: number): number => {
  const b = porIndice.get(i);
  if (!b) {
    throw new Error(
      `El beat ${i} no está en public/beats.json (npm run beats)`,
    );
  }
  return b.frame;
};

export const TEMA = {
  archivo: beats.tema.archivo,
  // segundo del tráiler = segundo del tema + desfaseSeg
  desfaseSeg: beats.montaje.desfaseSeg,
};

export const MARCAS = beats.marcas;
