#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Deja usables los PNG de Assets/Ruleta Nueva.

Los 42 recortes sueltos que trajo la carpeta NO se pueden usar como estan:

  1. Estan aplanados sobre blanco. Tienen canal alpha, pero no hay un solo
     pixel transparente: la rueda, las fichas y la mesa son rectangulos
     opacos con las esquinas pintadas del fondo de la hoja.
  2. Algunos se llevaron el texto del label adentro del dibujo
     (titulo.png tiene "titulo.png" escrito abajo).
  3. Son versiones REESCALADAS de la hoja, no recortes exactos: la rueda
     mide 290x325 suelta y 284x341 en la hoja. Por eso no sirve buscarlas
     en la hoja por correlacion; hay que recortar de la hoja directamente.

sprite_sheet_completo.png SI trae alpha de verdad (fondo ~a=20, dibujo
~a=250), asi que es la unica fuente buena. Este script recorta de ahi.

Como encuentra cada asset: componentes conexas sobre alpha>140. Eso separa
todo menos las tres ruletas, que se tocan entre si (se superponen ~27 px),
asi que esas salen por circulo ajustado por minimos cuadrados sobre el borde.

Ademas del recorte hace el pase de color. La hoja original es un casino
realista (caoba oscura, fieltro verde apagado, paneles casi negros) y el
juego pide lo contrario: colores vivos, dorados y UI redondeada estilo
Pokemon. El remapeo esta en PALETA y trabaja en HSV con mascaras suaves
para no postorizar.

Salida: Assets/Ruleta Nueva/roulette_*.png, a la resolucion de la hoja.

Ojo con esto ultimo, que la primera version la tuvo mal: NO hay que exportar
al tamano que ocupan en el Canvas de 512x384. El CanvasScaler del juego es
ScaleWithScreenSize con match=0, asi que a 1920 de ancho todo se agranda
3,75x; una ficha exportada a 26 px se dibuja a 98 px reales y se ve como un
borron. Exportadas a los ~100 px que tienen en la hoja caen casi 1:1 en
pantalla. El tamano en el Canvas lo fija el RectTransform del builder, no el
PNG.

Uso:  python scripts/limpiar_ruleta.py
"""

import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CARPETA = os.path.join(RAIZ, "Codigo", "PokeRush", "Assets", "Ruleta Nueva")
HOJA = os.path.join(CARPETA, "sprite_sheet_completo.png")

# Fondo de la hoja y dibujo estan bien separados: el histograma de alpha es
# bimodal (fondo 7..63, dibujo 240..252) con un valle vacio en el medio.
ALPHA_FONDO = 60.0
ALPHA_DIBUJO = 250.0


# --------------------------------------------------------------------------
# Donde esta cada cosa en la hoja.
#
# (x, y, ancho, alto) medidos por componentes conexas sobre la hoja de
# 1536x1024. El destino es el ancho final en el Canvas de 512x384; el alto
# sale solo para no deformar.
# --------------------------------------------------------------------------
RECORTES = [
    # nombre                    x     y    w    h   ancho final (None = nativo)
    ("roulette_ball",         908,   45,  56,  57,  None),
    ("roulette_hub",          897,  154, 108, 157,  None),
    ("roulette_rim_arc",     1019,  169, 149, 142,  None),
    ("roulette_title",       1191,  135, 329, 143, None),
    ("roulette_subtitle",    1204,  304, 304,  64, None),
    ("roulette_table",          9,  365, 663, 346, None),

    ("roulette_chip_1",       690,  394,  99, 102,  None),
    ("roulette_chip_5",       826,  394, 100, 102,  None),
    ("roulette_chip_25",      962,  394,  99, 101,  None),
    ("roulette_chip_100",    1099,  394, 100, 101,  None),
    ("roulette_chip_500",    1244,  394, 100, 102,  None),
    ("roulette_chip_1000",   1392,  394, 100, 102,  None),

    ("roulette_mark_red",     687,  579,  78,  80,  None),
    ("roulette_mark_black",   792,  580,  76,  79,  None),
    ("roulette_mark_green",   899,  577,  83,  84,  None),

    ("roulette_stack_red",   1029,  563,  56,  96,  None),
    ("roulette_stack_blue",  1104,  575,  55,  84,  None),
    ("roulette_stack_green", 1183,  575,  52,  84,  None),
    ("roulette_stack_black", 1264,  572,  54,  87,  None),
    ("roulette_stack_purple",1345,  559,  55, 100,  None),
    ("roulette_stack_gold",  1440,  547,  54, 113,  None),

    ("roulette_btn_bet",       36,  744, 272,  73,  None),
    ("roulette_btn_clear",    338,  744, 255,  73,  None),
    ("roulette_btn_spin",     620,  744, 274,  73,  None),
    ("roulette_btn_spin_off", 922,  744, 271,  73,  None),
    ("roulette_btn_back",    1240,  745, 254,  72,  None),

    ("roulette_panel_bet",     24,  857, 208, 119,  None),
    ("roulette_panel_result",  242, 857, 222, 119,  None),
    ("roulette_history",       477, 857, 401, 116, None),

    ("roulette_cell_red",     897,  865,  81, 100,  None),
    ("roulette_cell_black",  1007,  866,  78,  99,  None),
    ("roulette_cell_green",  1122,  866,  79,  99,  None),

    ("roulette_fx_spark",    1252,  866,  86,  92,  None),
    ("roulette_fx_confetti", 1378,  858, 132, 112,  None),
]

# Los tres paneles vienen con un valor de ejemplo dibujado ("1000", el 27, y
# los diez circulos del historial). Como esos valores los tiene que escribir el
# juego, se borran y queda el panel como marco: encabezado dibujado arriba y el
# interior liso abajo para que la UI ponga lo suyo.
#
# Coordenadas dentro del recorte, antes de escalar: (x0, y0, x1, y1) del area a
# vaciar, mas el punto de donde se saca el color de fondo.
BORRADOS = {
    # El "1000" se va; la pokeball se queda, que hace de simbolo de la moneda.
    "roulette_panel_bet":    ([(76, 60, 196, 110)], (150, 52)),
    "roulette_panel_result": ([(16, 50, 206, 110)], (30, 52)),
    "roulette_history":      ([(16, 46, 385, 106)], (30, 48)),
}


# La rueda NO sale de la hoja: sale de ruleta_base_2.png, que paso Augusto
# aparte y es mucho mejor dibujo (1266x1243, con alpha de verdad y los 37
# numeros legibles). La de la hoja quedaba en 320 px y los numeros eran un
# borron.
#
# Geometria medida sobre el archivo, no a ojo:
#   - circulo ajustado por minimos cuadrados sobre el borde alpha:
#     centro (628.29, 614.52), r = 606.27, residuo medio 0.71 px;
#   - el casillero verde (el cero) ocupa de -4.1 a +4.9 grados, o sea 9.7 de
#     ancho (= 360/37, confirma que son 37) y centrado en 0.4 grados. Esa es
#     la parte que importa: el cero cae ARRIBA, que es donde RouletteBoard
#     pone su angulo 0, asi que la bola frena sobre el numero que se canta;
#   - el anillo de casilleros va de 0.46R a 0.71R, y la pista lisa por la que
#     corre la bola queda a 0.80R.
#
# Verificado ademas que el orden dibujado es el europeo estandar: poniendo un
# punto en el indice 9 cae sobre el 34, que es RouletteBoard.Orden[9].
RUEDA_ARCHIVO = "ruleta_base_2.png"
RUEDA = dict(cx=628.29, cy=614.52, r=606.27, destino=640)

# Fracciones del radio, para que el builder ponga la bola donde va.
RUEDA_PISTA = 0.80
RUEDA_CASILLEROS = 0.52

# --------------------------- el anillo se REPINTA ---------------------------
#
# El dibujo original NO es una ruleta valida: tiene 36 casilleros en vez de 37
# y su secuencia arranca bien (0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13,
# 36, 11, 30, 8) pero despues se saltea el 23, el 10 y el 24. Ademas corre al
# reves. Comprobado recortando los casilleros uno por uno: con paso 360/36
# quedan encuadrados y con 360/37 se corren.
#
# Con 36 huecos para 37 numeros es imposible que la bola frene sobre el numero
# que se canta, asi que el anillo se vuelve a pintar entero con los 37 en el
# orden europeo y en sentido horario, que es el que usa RouletteBoard. Se
# conservan el aro de madera, el cono y el centro, que es lo lindo del dibujo.
#
# Bandas medidas sobre el original (fraccion del radio):
#   0.46-0.55  casilleros lisos     0.56-0.59  aro dorado
#   0.60-0.70  casilleros numerados 0.71-0.74  aro dorado
ANILLO_R0, ANILLO_R1 = 0.452, 0.712
ANILLO_RADIO_NUM = 0.651
ANILLO_DIVISOR = 0.62          # medio ancho del separador dorado, en grados
ANILLO_FUENTE = "C:/Windows/Fonts/arialbd.ttf"

ORDEN_RUEDA = [0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8,
               23, 10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12,
               35, 3, 26]
ROJOS_RUEDA = {1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36}


# --------------------------------------------------------------------------
# Pase de color
# --------------------------------------------------------------------------
def _rgb_a_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(axis=-1)
    mn = rgb.min(axis=-1)
    d = mx - mn
    h = np.zeros_like(mx)
    seguro = d > 1e-6
    idx = seguro & (mx == r)
    h[idx] = ((g - b)[idx] / d[idx]) % 6
    idx = seguro & (mx == g)
    h[idx] = ((b - r)[idx] / d[idx]) + 2
    idx = seguro & (mx == b)
    h[idx] = ((r - g)[idx] / d[idx]) + 4
    h = (h / 6.0) % 1.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0.0)
    return h, s, mx


def _hsv_a_rgb(h, s, v):
    i = np.floor(h * 6.0)
    f = h * 6.0 - i
    p = v * (1 - s)
    q = v * (1 - f * s)
    t = v * (1 - (1 - f) * s)
    i = (i % 6).astype(np.int32)
    r = np.select([i == 0, i == 1, i == 2, i == 3, i == 4, i == 5], [v, q, p, p, t, v])
    g = np.select([i == 0, i == 1, i == 2, i == 3, i == 4, i == 5], [t, v, v, q, p, p])
    b = np.select([i == 0, i == 1, i == 2, i == 3, i == 4, i == 5], [p, p, t, v, v, q])
    return np.stack([r, g, b], axis=-1)


def _banda(x, lo, hi, suave=0.05):
    """Pertenencia suave a [lo, hi]. Evita el escalon que postoriza."""
    return np.clip((x - lo + suave) / suave, 0, 1) * np.clip((hi - x + suave) / suave, 0, 1)


def _mezclar_hue(h, objetivo, peso):
    """Interpola hacia un hue por el camino corto del circulo."""
    d = (objetivo - h + 0.5) % 1.0 - 0.5
    return (h + d * peso) % 1.0


def PALETA(rgb):
    """Casino realista -> casino de videojuego.

    Cuatro remapeos, cada uno con mascara suave para que no se vean bordes:

      caoba  -> dorado brillante   (el marco de la rueda y de la mesa)
      fieltro-> verde vivo         (el tapete)
      negro  -> azul noche         (paneles y casillas negras; el negro puro
                                    es lo que mas lee "casino de adultos")
      rojo   -> rojo mas encendido (queda rojo: es info de juego)
    """
    h, s, v = _rgb_a_hsv(rgb)

    # --- caoba / madera -> oro ---
    # marron = hue 0.02..0.12, con algo de saturacion y no muy claro
    m_madera = _banda(h, 0.015, 0.115, 0.02) * np.clip((s - 0.18) / 0.2, 0, 1)
    m_madera *= np.clip((0.72 - v) / 0.2, 0, 1)
    h = _mezclar_hue(h, 0.108, m_madera * 0.85)          # ~39 grados
    v = v * (1 + 0.62 * m_madera)
    s = s * (1 - 0.12 * m_madera) + 0.10 * m_madera

    # --- fieltro verde -> verde vivo ---
    m_verde = _banda(h, 0.24, 0.46, 0.03) * np.clip((s - 0.12) / 0.2, 0, 1)
    h = _mezclar_hue(h, 0.30, m_verde * 0.55)            # ~108 grados
    v = v * (1 + 0.34 * m_verde)
    s = np.clip(s * (1 + 0.30 * m_verde), 0, 1)

    # --- casi negro -> azul noche ---
    m_negro = np.clip((0.30 - v) / 0.24, 0, 1) * np.clip((0.45 - s) / 0.35, 0, 1)
    h = _mezclar_hue(h, 0.635, m_negro * 0.92)           # ~229 grados
    s = np.clip(s + 0.42 * m_negro, 0, 1)
    v = v + 0.115 * m_negro

    # --- rojos mas encendidos ---
    m_rojo = (_banda(h, 0.945, 1.0, 0.02) + _banda(h, 0.0, 0.035, 0.02))
    m_rojo = np.clip(m_rojo, 0, 1) * np.clip((s - 0.3) / 0.3, 0, 1)
    v = v * (1 + 0.16 * m_rojo)
    s = np.clip(s * (1 + 0.06 * m_rojo), 0, 1)

    # --- pase general: un toque mas de vida y sombras menos cerradas ---
    s = np.clip(s * 1.09, 0, 1)
    v = np.clip(np.power(np.clip(v, 0, 1), 0.93) * 1.03, 0, 1)

    return np.clip(_hsv_a_rgb(h, s, v), 0, 1)


# --------------------------------------------------------------------------
def _normalizar_alpha(a):
    """Fondo a 0 y dibujo a 255, conservando el antialias del medio."""
    return np.clip((a - ALPHA_FONDO) / (ALPHA_DIBUJO - ALPHA_FONDO), 0, 1)


def _recolorear(sub):
    """sub: HxWx4 float 0..255 -> Image RGBA ya recoloreada."""
    rgb = sub[..., :3] / 255.0
    a = _normalizar_alpha(sub[..., 3])
    rgb = PALETA(rgb)
    out = np.concatenate([rgb * 255.0, (a * 255.0)[..., None]], axis=-1)
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")


def _escalar(im, ancho):
    if ancho is None or ancho >= im.width:
        return im
    alto = max(1, int(round(im.height * ancho / im.width)))
    # LANCZOS y no NEAREST: el dibujo esta pintado con degrades, no es pixel
    # art de grilla. NEAREST a menos de la mitad lo rompe.
    return im.resize((ancho, alto), Image.LANCZOS)


def _recortar_transparente(im):
    caja = im.getbbox()
    return im.crop(caja) if caja else im


def _plantillas_del_anillo(rgb):
    """Perfil radial medio de un casillero rojo, uno negro y uno verde.

    Cada ANGULO se clasifica por el color que tiene a 0.50R (la banda de
    casilleros lisos, sin numeros ni dorado) y despues, para cada radio, se
    toma la mediana sobre los angulos de esa clase. La mediana aguanta que en
    la banda de los numeros haya pixeles blancos: son minoria dentro del
    casillero y no corren el valor.

    Asi el anillo repintado hereda el sombreado y el aro dorado del dibujo en
    vez de quedar de un color plano.
    """
    filas = int(round((ANILLO_R1 - ANILLO_R0) * RUEDA["r"]))
    radios = np.linspace(ANILLO_R0, ANILLO_R1, filas)
    angs = np.arange(0, 360, 0.25)
    cx, cy, r = RUEDA["cx"], RUEDA["cy"], RUEDA["r"]

    cls = []
    for a in angs:
        x = int(round(cx + r * 0.50 * np.sin(np.radians(a))))
        y = int(round(cy - r * 0.50 * np.cos(np.radians(a))))
        cr, cg, cb = rgb[y, x]
        if cg > 95 and cr < 95 and cb < 95: cls.append("V")
        elif cr > 115 and cr > cg * 1.7 and cr > cb * 1.7: cls.append("R")
        elif cr < 85 and cg < 85 and cb < 85: cls.append("N")
        else: cls.append("?")
    cls = np.array(cls)

    tpl = {}
    for c in ("R", "N", "V"):
        sel = angs[cls == c]
        if len(sel) == 0:
            continue
        col = np.zeros((filas, 3))
        for i, dr in enumerate(radios):
            xs = (cx + r * dr * np.sin(np.radians(sel))).round().astype(int)
            ys = (cy - r * dr * np.cos(np.radians(sel))).round().astype(int)
            col[i] = np.median(rgb[ys, xs], axis=0)
        tpl[c] = col
    if "V" not in tpl:
        tpl["V"] = tpl["R"] * np.array([0.25, 1.05, 0.35])

    xs = (cx + r * 0.575 * np.sin(np.radians(angs))).round().astype(int)
    ys = (cy - r * 0.575 * np.cos(np.radians(angs))).round().astype(int)
    return tpl, len(radios), np.median(rgb[ys, xs], axis=0)


def repintar_anillo(im):
    """Vuelve a pintar el anillo con los 37 casilleros en orden europeo."""
    rgb = np.asarray(im.convert("RGB")).astype(np.float64)
    tpl, filas, oro = _plantillas_del_anillo(rgb)
    cx, cy, r = RUEDA["cx"], RUEDA["cy"], RUEDA["r"]
    paso = 360.0 / 37

    out = np.asarray(im.convert("RGBA")).astype(np.float64).copy()
    y0, y1 = int(cy - r * ANILLO_R1) - 2, int(cy + r * ANILLO_R1) + 3
    x0, x1 = int(cx - r * ANILLO_R1) - 2, int(cx + r * ANILLO_R1) + 3
    yy, xx = np.mgrid[y0:y1, x0:x1]
    dx, dy = xx - cx, yy - cy
    rr = np.hypot(dx, dy) / r
    aa = np.degrees(np.arctan2(dx, -dy)) % 360.0

    idx = np.clip(((rr - ANILLO_R0) / (ANILLO_R1 - ANILLO_R0) * (filas - 1))
                  .round().astype(int), 0, filas - 1)
    # OJO con el medio paso: RouletteBoard.AnguloDe devuelve j*paso como el
    # CENTRO del casillero j, asi que el casillero tiene que ir de
    # (j-0.5)*paso a (j+0.5)*paso. Pintandolo de j*paso a (j+1)*paso la bola
    # frenaba justo sobre la raya que separa dos numeros.
    giro = (aa + paso / 2.0) % 360.0
    casillero = np.floor(giro / paso).astype(int) % 37

    def clase(n):
        return "V" if n == 0 else ("R" if n in ROJOS_RUEDA else "N")

    lut = np.stack([tpl[clase(ORDEN_RUEDA[j])] for j in range(37)])
    col = lut[casillero, idx]

    resto = giro - casillero * paso
    borde = np.minimum(resto, paso - resto) < ANILLO_DIVISOR
    col = np.where(borde[..., None], oro[None, None, :], col)

    # Borde suave contra el resto del dibujo, para que no se vea el corte.
    dentro = (rr >= ANILLO_R0) & (rr <= ANILLO_R1)
    suave = np.clip((rr - ANILLO_R0) / 0.004, 0, 1) * np.clip((ANILLO_R1 - rr) / 0.004, 0, 1)
    w = (dentro * suave)[..., None]
    sub = out[y0:y1, x0:x1]
    sub[..., :3] = sub[..., :3] * (1 - w) + col * w
    out[y0:y1, x0:x1] = sub

    res = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")

    fuente = ImageFont.truetype(ANILLO_FUENTE, 46)
    for j, n in enumerate(ORDEN_RUEDA):
        a = j * paso                      # centro del casillero, igual que AnguloDe
        txt = str(n)
        caja = fuente.getbbox(txt)
        chip = Image.new("RGBA", (caja[2] - caja[0] + 8, caja[3] - caja[1] + 8), (0, 0, 0, 0))
        ImageDraw.Draw(chip).text((4 - caja[0], 4 - caja[1]), txt,
                                  font=fuente, fill=(255, 255, 255, 255))
        # Se lee desde afuera: el "arriba" del numero apunta al borde.
        chip = chip.rotate(-a, expand=True, resample=Image.BICUBIC)
        px = cx + r * ANILLO_RADIO_NUM * np.sin(np.radians(a))
        py = cy - r * ANILLO_RADIO_NUM * np.cos(np.radians(a))
        res.alpha_composite(chip, (int(px - chip.width / 2), int(py - chip.height / 2)))

    return res


def extraer_rueda():
    """La rueda, recortada de ruleta_base_2.png y pasada por la paleta.

    Solo hay que centrarla: el archivo ya trae alpha de verdad, asi que no
    hace falta enmascarar nada. Se recorta cuadrado alrededor del centro
    ajustado para que el centro del PNG sea el eje de giro; si quedara
    corrido, la rueda cabecearia al rotar.

    Va por PALETA como todo el resto: el dibujo original es caoba realista y
    quedaria como lo unico marron entre el dorado y el azul noche de la mesa.
    Para dejarlo tal cual, devolver 'crop' sin pasar por _recolorear.

    640 px de salida: se dibuja a 152 unidades del Canvas, que a 1080p son
    ~570 px reales, asi que 640 alcanza sin desperdiciar memoria (el original
    de 1218 seria 4 veces mas textura para nada).
    """
    ruta = os.path.join(CARPETA, RUEDA_ARCHIVO)
    if not os.path.isfile(ruta):
        sys.exit("Falta " + ruta)

    im = repintar_anillo(Image.open(ruta).convert("RGBA"))
    cx, cy, r = RUEDA["cx"], RUEDA["cy"], RUEDA["r"]
    half = int(np.ceil(r)) + 2
    crop = im.crop((int(round(cx)) - half, int(round(cy)) - half,
                    int(round(cx)) + half, int(round(cy)) + half))

    out = _recolorear(np.asarray(crop).astype(np.float64))
    return out.resize((RUEDA["destino"], RUEDA["destino"]), Image.LANCZOS)


def vaciar_numero(im):
    """Borra el digito de una casilla para poder reusarla como fondo.

    Las tres casillas de la hoja vienen dibujadas con un numero de ejemplo
    (1, 2 y 0). Sirven como fondo de la grilla de apuestas, pero recien
    cuando se les saca ese numero: los de verdad los escribe la UI con la
    fuente del juego, que es la unica forma de que 37 casillas digan cada
    una lo suyo y se puedan resaltar.

    No alcanza con borrar lo blanco: el numero lleva contorno oscuro, y
    sacando solo el relleno queda el fantasma del contorno. Lo que si vale
    para las tres casillas es que el cuerpo es un degrade VERTICAL, o sea
    que dentro de una misma fila el color es casi constante. Entonces el
    numero es, por definicion, lo que se aparta de la mediana de su fila:
    eso agarra relleno y contorno de una, y sirve igual para el 1, el 2 y
    el 0 sin tener que tratarlos distinto.
    """
    a = np.asarray(im).astype(np.float64).copy()
    h, w = a.shape[:2]
    rgb, alpha = a[..., :3], a[..., 3]

    # El bisel del borde tambien se aparta de la mediana, asi que queda afuera.
    borde = max(2, int(round(min(h, w) * 0.17)))
    if h - 2 * borde < 2 or w - 2 * borde < 2:
        return im

    for y in range(borde, h - borde):
        fila = slice(borde, w - borde)
        cuerpo = rgb[y, fila]
        opaco = alpha[y, fila] > 200
        if opaco.sum() < 4:
            continue

        mediana = np.median(cuerpo[opaco], axis=0)
        dist = np.linalg.norm(cuerpo - mediana, axis=-1)
        # 26 sobre 0..255 deja pasar el degrade y el ruido del dibujo, y
        # marca el numero entero (relleno claro y contorno oscuro).
        malo = (dist > 26) & opaco
        if not malo.any():
            continue
        rgb[y, fila][malo] = mediana

    a[..., :3] = rgb
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def vaciar_regiones(im, regiones, muestra):
    """Tapa un area del panel con su propio color de fondo.

    El interior de los tres paneles es liso, asi que rellenar con la mediana
    de un pedacito limpio no deja marca. Se hace ANTES de escalar para que el
    borde del relleno se suavice junto con el resto.
    """
    a = np.asarray(im).astype(np.float64).copy()
    h, w = a.shape[:2]

    mx, my = muestra
    x0, x1 = max(0, mx - 6), min(w, mx + 6)
    y0, y1 = max(0, my - 4), min(h, my + 4)
    parche = a[y0:y1, x0:x1].reshape(-1, 4)
    parche = parche[parche[:, 3] > 200]
    if len(parche) == 0:
        return im
    fondo = np.median(parche, axis=0)

    for (rx0, ry0, rx1, ry1) in regiones:
        rx0, ry0 = max(0, rx0), max(0, ry0)
        rx1, ry1 = min(w, rx1), min(h, ry1)
        if rx1 <= rx0 or ry1 <= ry0:
            continue
        # Solo donde el panel ya era opaco: asi no se pisa el marco ni se
        # rellenan las esquinas redondeadas.
        zona = a[ry0:ry1, rx0:rx1]
        opaco = zona[..., 3] > 200
        zona[opaco] = fondo

    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


def panel_fieltro(tabla, lado=128, borde=20):
    """Panel verde con marco dorado para el area de apuestas, en 9 tajadas.

    La mesa dibujada (roulette_table) no sirve de fondo de la grilla: trae los
    37 numeros pintados encima, y abajo van los de verdad, que tienen que
    poder resaltarse de a uno. Asi que la grilla necesita un fondo liso.

    Los colores NO son inventados: salen de la mesa ya recoloreada, asi el
    panel y el resto de la mesa son la misma pieza. Sale cuadrado y con borde
    parejo para que Unity lo estire por 9 tajadas sin deformar las esquinas.
    """
    t = np.asarray(tabla.convert("RGBA")).astype(np.float64)
    h, w = t.shape[:2]

    # Verde del tapete: el centro de la mesa. Dorado: la franja del marco.
    centro = t[int(h * 0.62):int(h * 0.72), int(w * 0.35):int(w * 0.65)].reshape(-1, 4)
    centro = centro[centro[:, 3] > 200]
    verde = np.median(centro, axis=0)[:3] if len(centro) else np.array([40, 150, 60])

    marco = t[int(h * 0.02):int(h * 0.07), int(w * 0.3):int(w * 0.7)].reshape(-1, 4)
    marco = marco[marco[:, 3] > 200]
    oro = np.median(marco, axis=0)[:3] if len(marco) else np.array([222, 160, 60])

    n = lado * 4
    b = borde * 4
    yy, xx = np.mgrid[0:n, 0:n]
    c = (n - 1) / 2.0
    # Rectangulo redondeado, otra vez por Chebyshev suavizado.
    dx = np.abs(xx - c) / (n * 0.5)
    dy = np.abs(yy - c) / (n * 0.5)
    d = np.power(np.power(dx, 8) + np.power(dy, 8), 1 / 8.0)

    afuera = np.clip((d - 0.995) / 0.02, 0, 1)
    en_marco = np.clip((d - (1 - 2.0 * b / n)) / 0.05, 0, 1)
    # Filo claro por dentro del marco: sin eso el dorado se ve plano.
    filo = np.exp(-np.square((d - (1 - 2.0 * b / n)) / 0.022))

    rgb = np.zeros((n, n, 3))
    for i in range(3):
        rgb[..., i] = verde[i] * (1 - en_marco) + oro[i] * en_marco
    # Un brillo arriba y una sombra abajo: es lo que le da el bisel.
    luz = np.clip(1.0 - yy / float(n), 0, 1) * 0.16 - 0.10 * (yy / float(n)) + 0.22 * filo
    rgb = np.clip(rgb * (1 + luz[..., None]), 0, 255)

    a = (1 - afuera) * 255
    out = np.concatenate([rgb, a[..., None]], axis=-1)
    im = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
    return im.resize((lado, lado), Image.LANCZOS)


def ficha_apostada(lado=68):
    """Ficha lisa para marcar lo apostado en un casillero.

    Las fichas de la hoja traen su valor DIBUJADO (1, 5, 25...). Puestas sobre
    un casillero de 21x27 hay que escribirles encima el total acumulado, y los
    dos numeros juntos no se leen: queda un borron. Asi que la ficha que se
    apoya en la mesa es esta, lisa, y el numero lo pone la UI.

    Azul noche con aro dorado, los mismos dos colores que el resto de la mesa.
    """
    n = lado * 8
    yy, xx = np.mgrid[0:n, 0:n]
    c = (n - 1) / 2.0
    d = np.hypot(xx - c, yy - c) / (n * 0.5)

    centro = np.array([38.0, 44.0, 78.0])     # azul noche
    oro = np.array([232.0, 176.0, 64.0])

    aro = np.clip((d - 0.72) / 0.06, 0, 1)
    rgb = centro[None, None, :] * (1 - aro[..., None]) + oro[None, None, :] * aro[..., None]

    # Brillo arriba a la izquierda, sombra abajo: le da volumen de ficha.
    luz = ((c - yy) / n) * 0.55 + ((c - xx) / n) * 0.25
    rgb = np.clip(rgb * (1 + luz[..., None]), 0, 255)

    a = np.clip((0.98 - d) / 0.03, 0, 1) * 255
    out = np.concatenate([rgb, a[..., None]], axis=-1)
    im = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
    return im.resize((lado, lado), Image.LANCZOS)


def brillo_seleccion(lado=116):
    """Halo dorado para marcar la casilla elegida.

    No sale de la hoja: no hay ningun asset de seleccion ahi y la grilla lo
    necesita para cumplir con "la apuesta debe quedar visualmente marcada".
    """
    n = lado * 4
    yy, xx = np.mgrid[0:n, 0:n]
    cx = cy = (n - 1) / 2.0
    # Rectangulo redondeado: distancia de Chebyshev suavizada en las esquinas.
    dx = np.abs(xx - cx) / (n * 0.5)
    dy = np.abs(yy - cy) / (n * 0.5)
    d = np.power(np.power(dx, 6) + np.power(dy, 6), 1 / 6.0)
    anillo = np.exp(-np.square((d - 0.86) / 0.13))
    a = np.clip(anillo, 0, 1) * 255
    rgb = np.zeros((n, n, 3))
    rgb[..., 0], rgb[..., 1], rgb[..., 2] = 255, 226, 120
    out = np.concatenate([rgb, a[..., None]], axis=-1)
    im = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
    return im.resize((lado, lado), Image.LANCZOS)


def main():
    if not os.path.isfile(HOJA):
        sys.exit("No encontre " + HOJA)

    hoja = Image.open(HOJA).convert("RGBA")
    arr = np.asarray(hoja).astype(np.float64)
    print("hoja %dx%d" % hoja.size)

    hechos = []

    rueda = extraer_rueda()
    rueda.save(os.path.join(CARPETA, "roulette_wheel.png"))
    hechos.append(("roulette_wheel", rueda.size))

    for nombre, x, y, w, h, ancho in RECORTES:
        sub = arr[y:y + h, x:x + w]
        im = _recolorear(sub)
        # Antes de recortar: las medidas de BORRADOS son de la caja de la hoja.
        if nombre in BORRADOS:
            regiones, muestra = BORRADOS[nombre]
            im = vaciar_regiones(im, regiones, muestra)
        im = _recortar_transparente(im)
        # Las casillas de la grilla van sin el numero de ejemplo.
        if nombre in ("roulette_cell_red", "roulette_cell_black", "roulette_cell_green"):
            im = vaciar_numero(im)
        im = _escalar(im, ancho)
        im.save(os.path.join(CARPETA, nombre + ".png"))
        hechos.append((nombre, im.size))

    brillo = brillo_seleccion()
    brillo.save(os.path.join(CARPETA, "roulette_cell_glow.png"))
    hechos.append(("roulette_cell_glow", brillo.size))

    token = ficha_apostada()
    token.save(os.path.join(CARPETA, "roulette_token.png"))
    hechos.append(("roulette_token", token.size))

    fieltro = panel_fieltro(Image.open(os.path.join(CARPETA, "roulette_table.png")))
    fieltro.save(os.path.join(CARPETA, "roulette_felt.png"))
    hechos.append(("roulette_felt", fieltro.size))

    for nombre, size in hechos:
        print("  %-24s %dx%d" % (nombre, size[0], size[1]))
    print("listo: %d sprites en Ruleta Nueva/" % len(hechos))


if __name__ == "__main__":
    main()
