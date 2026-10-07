// ffmpeg y ffprobe sueltos (el PATH o el que instala winget); si no hay, los
// que trae Remotion (`npx remotion ffmpeg`).
import {spawnSync} from 'node:child_process';
import {existsSync, readdirSync} from 'node:fs';
import path from 'node:path';

const buscar = (nombre) => {
	if (spawnSync(nombre, ['-version'], {stdio: 'ignore'}).status === 0) {
		return {cmd: nombre, pre: [], shell: false};
	}
	const paquetes = path.join(process.env.LOCALAPPDATA ?? '', 'Microsoft/WinGet/Packages');
	if (existsSync(paquetes)) {
		for (const p of readdirSync(paquetes).filter((n) => n.startsWith('Gyan.FFmpeg'))) {
			for (const build of readdirSync(path.join(paquetes, p))) {
				const exe = path.join(paquetes, p, build, `bin/${nombre}.exe`);
				if (existsSync(exe)) {
					return {cmd: exe, pre: [], shell: false};
				}
			}
		}
	}
	return {cmd: 'npx', pre: ['remotion', nombre], shell: true};
};

export const buscarFfmpeg = () => buscar('ffmpeg');
export const buscarFfprobe = () => buscar('ffprobe');

// Corre la herramienta y devuelve stdout y stderr como texto.
export const correr = (herramienta, args) => {
	const r = spawnSync(herramienta.cmd, [...herramienta.pre, ...args], {
		encoding: 'utf8',
		shell: herramienta.shell,
		maxBuffer: 64 * 1024 * 1024,
	});
	if (r.status !== 0) {
		throw new Error(`${herramienta.cmd} falló:\n${r.stderr}`);
	}
	return {stdout: r.stdout, stderr: r.stderr};
};
