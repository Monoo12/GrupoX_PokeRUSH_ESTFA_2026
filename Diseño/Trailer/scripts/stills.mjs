// Renderiza cuadros sueltos de una composición con un solo bundle y un solo
// navegador (con `remotion render --frames=` y una lista salteada, el mezclador de
// audio de Remotion falla con EINVAL).
//
//   node scripts/stills.mjs Trailer 100 200 300      -> out/stills/Trailer-0100.png ...
import {bundle} from '@remotion/bundler';
import {openBrowser, renderStill, selectComposition} from '@remotion/renderer';
import {mkdir} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const [id = 'Trailer', ...cuadros] = process.argv.slice(2);
if (cuadros.length === 0) {
	console.error('Uso: node scripts/stills.mjs <Composicion> <frame> [frame...]');
	process.exit(1);
}

const serveUrl = await bundle({entryPoint: path.join(RAIZ, 'src/index.ts'), rspack: true});
const browser = await openBrowser('chrome');
const composition = await selectComposition({serveUrl, id, puppeteerInstance: browser});
const salida = path.join(RAIZ, 'out/stills');
await mkdir(salida, {recursive: true});
for (const c of cuadros) {
	const frame = Number(c);
	const archivo = path.join(salida, `${id}-${String(frame).padStart(4, '0')}.png`);
	await renderStill({serveUrl, composition, frame, output: archivo, puppeteerInstance: browser, imageFormat: 'png'});
	console.log(archivo);
}
await browser.close({silent: true});
