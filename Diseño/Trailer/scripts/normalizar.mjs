// Normaliza el audio del render a -14 LUFS (loudnorm en dos pasadas, lineal) sin
// tocar el video, y verifica duración, resolución y loudness final.
//
//   node scripts/normalizar.mjs out/PokeRush_Trailer_sin_normalizar.mp4 out/PokeRush_Trailer.mp4
import path from 'node:path';
import {buscarFfmpeg, buscarFfprobe, correr} from './ffmpeg.mjs';

const [entrada, salida] = process.argv.slice(2);
if (!entrada || !salida) {
	console.error('Uso: node scripts/normalizar.mjs <entrada.mp4> <salida.mp4>');
	process.exit(1);
}

const OBJETIVO = {I: -14, TP: -1, LRA: 11};
const ffmpeg = buscarFfmpeg();
const ffprobe = buscarFfprobe();

const medir = (archivo) => {
	const {stderr} = correr(ffmpeg, [
		'-hide_banner', '-nostats', '-i', archivo,
		'-af', `loudnorm=I=${OBJETIVO.I}:TP=${OBJETIVO.TP}:LRA=${OBJETIVO.LRA}:print_format=json`,
		'-f', 'null', '-',
	]);
	const json = stderr.slice(stderr.lastIndexOf('{'), stderr.lastIndexOf('}') + 1);
	return JSON.parse(json);
};

const antes = medir(entrada);
console.log(`Antes: ${antes.input_i} LUFS integrado, pico ${antes.input_tp} dBTP, LRA ${antes.input_lra}`);

const filtro = [
	`loudnorm=I=${OBJETIVO.I}:TP=${OBJETIVO.TP}:LRA=${OBJETIVO.LRA}`,
	`measured_I=${antes.input_i}`,
	`measured_TP=${antes.input_tp}`,
	`measured_LRA=${antes.input_lra}`,
	`measured_thresh=${antes.input_thresh}`,
	`offset=${antes.target_offset}`,
	'linear=true',
].join(':');
correr(ffmpeg, [
	'-hide_banner', '-y', '-i', entrada,
	'-c:v', 'copy',
	'-af', `${filtro},aresample=48000`,
	'-c:a', 'aac', '-b:a', '320k',
	'-movflags', '+faststart',
	salida,
]);

const despues = medir(salida);
const {stdout} = correr(ffprobe, [
	'-v', 'error',
	'-show_entries', 'format=duration:stream=codec_name,width,height,r_frame_rate,sample_rate',
	'-of', 'json', salida,
]);
const info = JSON.parse(stdout);
const video = info.streams.find((s) => s.width);
const audio = info.streams.find((s) => s.sample_rate);
const dur = Number(info.format.duration);
console.log(`Después: ${despues.input_i} LUFS integrado, pico ${despues.input_tp} dBTP`);
console.log(`${path.basename(salida)}: ${dur.toFixed(2)} s, ${video.codec_name} ${video.width}x${video.height} @ ${video.r_frame_rate}, ${audio.codec_name} ${audio.sample_rate} Hz`);
if (dur < 58 || dur > 62) {
	console.error(`OJO: la duración (${dur.toFixed(2)} s) está fuera de 58-62 s`);
	process.exitCode = 1;
}
