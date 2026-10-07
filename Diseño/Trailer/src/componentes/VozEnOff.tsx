import { Audio } from "@remotion/media";
import React from "react";
import { staticFile } from "remotion";
import { archivoVoz, type IdVoz } from "../datos/voz";

// Una línea de la voz en off. `npm run voz` deja en public/voz/NN.wav la versión
// que suena: la manual de voz/manual/ (limpia) si existe, o la sintetizada.
export const VozEnOff: React.FC<{ id: IdVoz; volumen?: number }> = ({
  id,
  volumen = 1,
}) => <Audio src={staticFile(archivoVoz(id))} volume={volumen} />;
