import duraciones from "../../public/voz/duraciones.json";

// Duraciones medidas con ffprobe por scripts/generar-voz.ps1 (npm run voz).
export type IdVoz = keyof typeof duraciones.lineas;

export const durVoz = (id: IdVoz): number => duraciones.lineas[id].frames;

export const archivoVoz = (id: IdVoz): string => duraciones.lineas[id].archivo;
