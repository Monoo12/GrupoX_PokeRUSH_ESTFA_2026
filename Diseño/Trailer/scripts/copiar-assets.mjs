// Copia a public/assets/ lo que el trailer usa del proyecto de Unity, arma los
// audios derivados y trae las capturas de OBS. public/assets/ no se versiona:
// se regenera con `npm run assets`. El proyecto de Unity solo se lee.
//
// Los nombres de destino no llevan espacios, eñes ni mayúsculas, así staticFile()
// no tiene que escapar nada.

import {copyFile, mkdir, readdir} from 'node:fs/promises';
import {existsSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {buscarFfmpeg, correr} from './ffmpeg.mjs';

const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const UNITY = path.resolve(RAIZ, '../../Codigo/PokeRush/Assets');
const DESTINO = path.join(RAIZ, 'public/assets');
const CAPTURAS = path.join(RAIZ, 'capturas');

// [origen dentro de Codigo/PokeRush/Assets, destino dentro de public/assets]
const ARCHIVOS = [
	// Logos y pantallas
	['Sprites/Logos/IconoDelJuego.png', 'logos/icono-del-juego.png'],
	['Sprites/Logos/LevelUpCreators.png', 'logos/level-up-creators.png'],
	['Sprites/Menus/pantalla incio.png', 'menus/pantalla-inicio.png'],
	['Sprites/Menus/fondoCampeon.png', 'menus/fondo-campeon.png'],
	['Sprites/Menus/panels.png', 'menus/panels.png'],

	// Personajes (retratos de la caja de diálogo y hojas del mapa)
	['Sprites/Nestor/NESTOR_retrato_enojado.png', 'personajes/nestor-enojado.png'],
	['Sprites/Nestor/NESTOR_retrato_furioso.png', 'personajes/nestor-furioso.png'],
	['Sprites/Nestor/NESTOR_retrato_shock.png', 'personajes/nestor-shock.png'],
	['Sprites/Nestor/NESTOR_retrato_pensando.png', 'personajes/nestor-pensando.png'],
	['Sprites/Nestor/NESTOR_brazos_cruzados.png', 'personajes/nestor-brazos-cruzados.png'],
	['Graphics/Trainers/TIXI.png', 'personajes/tixi-retrato.png'],
	['Graphics/Trainers/JULIETA.png', 'personajes/julieta-retrato.png'],
	['Graphics/Characters/julieta.png', 'personajes/julieta-hoja.png'],
	['Graphics/Trainers/MARTIN.png', 'personajes/reymar-retrato.png'],
	['Graphics/Trainers/TACCONI.png', 'personajes/tacconi-retrato.png'],
	['Sprites/Casino/tacconi_frente.png', 'personajes/tacconi-frente.png'],
	['Sprites/Player/POKEMONTRAINER_RojoNeutro_walk_2.png', 'personajes/jugador-rojo.png'],
	['Sprites/Player/POKEMONTRAINER_HojaNeutra_walk_2.png', 'personajes/jugadora-hoja.png'],
	['Sprites/Trainers/CHICO.png', 'entrenadores/chico.png'],
	['Sprites/Trainers/CHICA.png', 'entrenadores/chica.png'],
	['Sprites/Trainers/CIENTIFICA.png', 'entrenadores/cientifica.png'],
	['Sprites/Trainers/ESTUDIANTA.png', 'entrenadores/estudianta.png'],
	['Sprites/Trainers/JUGON.png', 'entrenadores/jugon.png'],
	['Sprites/Trainers/DAMISELA.png', 'entrenadores/damisela.png'],

	// Combate
	['Sprites/Battlebacks/Central_bg.png', 'combate/fondo-central.png'],
	['Sprites/Battlebacks/Laboratorio_bg.png', 'combate/fondo-laboratorio.png'],
	['Sprites/Battlebacks/InteriorRojo_bg.png', 'combate/fondo-interior-rojo.png'],
	['Sprites/Pokens/Back/LUCARIO.png', 'combate/lucario-espalda.png'],
	['Sprites/Pokens/Front/PORYGONZ.png', 'combate/porygonz-frente.png'],
	['Sprites/Pokens/Front/MEWTWO.png', 'combate/mewtwo-frente.png'],
	['Sprites/UI/Battle/databox_normal.png', 'combate/databox-jugador.png'],
	['Sprites/UI/Battle/databox_normal_foe.png', 'combate/databox-rival.png'],
	['Sprites/UI/Battle/overlay_message.png', 'combate/caja-mensaje.png'],
	['Sprites/UI/Battle/ball_POKEBALL.png', 'combate/pokeball.png'],
	['Sprites/UI/Battle/ballBurst_star.png', 'combate/destello-ball.png'],
	['Graphics/Animations/PRAS- Aura Sphere.png', 'combate/fx-esfera-aural.png'],
	['Graphics/Animations/PRAS- Force Palm.png', 'combate/fx-palmeo.png'],

	// Casino
	['Graphics/UI/Slot Machine/bg.png', 'casino/slots-maquina.png'],
	['Graphics/UI/Slot Machine/images.png', 'casino/slots-simbolos.png'],
	['Graphics/UI/Slot Machine/ReelOverlay.png', 'casino/slots-rodillo.png'],
	['Graphics/UI/Slot Machine/lights.png', 'casino/slots-luces.png'],
	['Ruleta Nueva/roulette_wheel.png', 'casino/ruleta-rueda.png'],
	['Ruleta Nueva/roulette_ball.png', 'casino/ruleta-bola.png'],
	['Ruleta Nueva/mesa_ruleta.png', 'casino/ruleta-mesa.png'],
	['BlackJack God/cartas_picas.png', 'casino/cartas-picas.png'],
	['BlackJack God/cartas_corazones.png', 'casino/cartas-corazones.png'],
	['BlackJack God/reverso_rojo.png', 'casino/carta-reverso.png'],
	['BlackJack God/mesa_blackjack.png', 'casino/blackjack-mesa.png'],
	['Graphics/Casino/mesa_fieltro.png', 'casino/mesa-fieltro.png'],
	['Sprites/Casino/tarjeta_vip.png', 'casino/tarjeta-vip.png'],

	// La sede (arena de Santiago)
	['Sprites/Tilesets/juliTILE.png', 'sede/julitile.png'],

	// Fuentes del juego
	['Fonts/power clear.ttf', 'fuentes/power-clear.ttf'],
	['Fonts/power clear bold.ttf', 'fuentes/power-clear-bold.ttf'],
	['Fonts/power green.ttf', 'fuentes/power-green.ttf'],

	// Música
	['Audio/Pelea contra julieta/Fase 1/Luffy Vs. Kizaru (My Peak) - One Piece EP 1126 OST ワンピース SOUNDTRACK - Epic Version.mp3', 'musica/kizaru-epic.mp3'],
	['Audio/Musica_MenuPrincipalySave.mp3', 'musica/menu.mp3'],

	// Efectos
	['Audio/Efectos/Final/alarma.ogg', 'sfx/alarma.ogg'],
	['Audio/Efectos/Final/chip_apagado.ogg', 'sfx/chip-apagado.ogg'],
	['Audio/Efectos/Final/nucleo_destruido.wav', 'sfx/nucleo-destruido.wav'],
	['Audio/Efectos/Final/puerta_se_abre.ogg', 'sfx/puerta-se-abre.ogg'],
	['Audio/Efectos/Final/transmisor.ogg', 'sfx/transmisor.ogg'],
	['Audio/Efectos/Combate/golpe_super_eficaz.ogg', 'sfx/golpe-super-eficaz.ogg'],
	['Audio/Efectos/Combate/debilitado.ogg', 'sfx/debilitado.ogg'],
	['Audio/Efectos/Combate/lanzar_ball.ogg', 'sfx/lanzar-ball.ogg'],
	['Audio/Efectos/Combate/estado_paralizado.wav', 'sfx/chispazo.wav'],
	['Audio/Efectos/Casino/slots_moneda.ogg', 'sfx/slots-moneda.ogg'],
	['Audio/Efectos/Casino/slots_frena.ogg', 'sfx/slots-frena.ogg'],
	['Audio/Efectos/Casino/premio_grande.ogg', 'sfx/premio-grande.ogg'],
	['Audio/Efectos/Ruleta NegroRojo girando/spinopel-spinning-roulette-wheel-429832.mp3', 'sfx/ruleta-girando.mp3'],
	['Audio/Efectos/Taking Card Poker/oxidvideos-taking-playing-card-2-522516.mp3', 'sfx/carta.mp3'],
	['Audio/Efectos/Menus/menu_cursor.ogg', 'sfx/menu-cursor.ogg'],
	['Audio/Efectos/Menus/menu_error.ogg', 'sfx/menu-error.ogg'],
	['Audio/Efectos/Menus/menu_abrir.ogg', 'sfx/menu-abrir.ogg'],
	['Audio/Efectos/Menus/pc_apagar.ogg', 'sfx/pc-apagar.ogg'],
	['Audio/Efectos/Seleccion en el menu (X)/tmpq91k5v_6.mp3', 'sfx/seleccion.mp3'],
	['Audio/Combate/Ataques/aura-sphere_1.wav', 'sfx/esfera-aural.wav'],
	['Audio/Combate/Ataques/force-palm_1.wav', 'sfx/palmeo.wav'],
	['Audio/Combate/Gritos/0448_Lucario.ogg', 'sfx/grito-lucario.ogg'],
	['Audio/Combate/Gritos/0474_Porygon-Z.ogg', 'sfx/grito-porygonz.ogg'],
	['Audio/Combate/Gritos/0150_Mewtwo.ogg', 'sfx/grito-mewtwo.ogg'],
];

// Audios que se arman con ffmpeg a partir de los copiados (o de la nada).
// `entrada` es relativa a public/assets; los filtros son de ffmpeg.
const DERIVADOS = [
	{
		// El tema del menú "a través de una pared": la cama tensa del arranque.
		salida: 'musica/menu-apagado.wav',
		entrada: 'musica/menu.mp3',
		args: ['-t', '16', '-af', 'lowpass=f=420,lowpass=f=420,highpass=f=40,volume=1.6,afade=t=in:d=1.5'],
	},
	{
		// El primer golpe del tema de Kizaru (1,79 s), suelto: el chispazo de la nuca.
		salida: 'sfx/golpe-kizaru.wav',
		entrada: 'musica/kizaru-epic.mp3',
		args: ['-ss', '1.70', '-t', '1.30', '-af', 'afade=t=in:d=0.02,afade=t=out:st=0.35:d=0.95'],
	},
	{
		// Zumbido eléctrico: red de 50 Hz con armónicos y un poco de ruido rosa.
		salida: 'sfx/zumbido.wav',
		lavfi: [
			'sine=f=50:d=8', 'sine=f=100:d=8', 'sine=f=150:d=8',
			'anoisesrc=c=pink:a=0.25:d=8',
		],
		args: ['-filter_complex',
			'[0]volume=0.5[a];[1]volume=0.35[b];[2]volume=0.18[c];[3]highpass=f=2000,lowpass=f=6000,volume=0.18[d];'
			+ '[a][b][c][d]amix=inputs=4:normalize=0,tremolo=f=7:d=0.15,volume=0.7[o]',
			'-map', '[o]'],
	},
	{
		// Ráfaga digital para los glitches (ruido blanco machacado a 4 bits).
		salida: 'sfx/glitch.wav',
		lavfi: ['anoisesrc=c=white:a=0.6:d=0.45'],
		args: ['-af', 'acrusher=bits=4:mode=log:aa=0,tremolo=f=28:d=0.9,highpass=f=300,afade=t=out:st=0.3:d=0.15'],
	},
];

const copiar = async () => {
	let faltan = 0;
	for (const [origen, destino] of ARCHIVOS) {
		const de = path.join(UNITY, origen);
		const a = path.join(DESTINO, destino);
		if (!existsSync(de)) {
			console.warn(`  FALTA  ${origen}`);
			faltan++;
			continue;
		}
		await mkdir(path.dirname(a), {recursive: true});
		await copyFile(de, a);
	}
	console.log(`Copiados ${ARCHIVOS.length - faltan} de ${ARCHIVOS.length} archivos a public/assets/`);
	return faltan;
};

const derivar = async () => {
	const ff = buscarFfmpeg();
	for (const d of DERIVADOS) {
		const salida = path.join(DESTINO, d.salida);
		await mkdir(path.dirname(salida), {recursive: true});
		const entradas = d.lavfi
			? d.lavfi.flatMap((src) => ['-f', 'lavfi', '-i', src])
			: ['-i', path.join(DESTINO, d.entrada)];
		correr(ff, ['-y', '-hide_banner', '-loglevel', 'error', ...entradas, ...d.args, '-ar', '48000', '-ac', '2', salida]);
	}
	console.log(`Armados ${DERIVADOS.length} audios derivados`);
};

// Si hay clips de OBS en capturas/, van a public/assets/capturas/ (el plano los
// prefiere a la reconstrucción).
const traerCapturas = async () => {
	if (!existsSync(CAPTURAS)) {
		console.log('Sin capturas/: todos los planos van con reconstrucción');
		return;
	}
	const clips = (await readdir(CAPTURAS)).filter((n) => /\.(mp4|mov|webm)$/i.test(n));
	await mkdir(path.join(DESTINO, 'capturas'), {recursive: true});
	for (const c of clips) {
		await copyFile(path.join(CAPTURAS, c), path.join(DESTINO, 'capturas', c));
	}
	console.log(`Capturas copiadas: ${clips.length ? clips.join(', ') : 'ninguna'}`);
};

const faltan = await copiar();
await derivar();
await traerCapturas();
if (faltan > 0) {
	process.exitCode = 1;
}
