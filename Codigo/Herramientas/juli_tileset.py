"""Convierte juliTILE.png en un tileset real de 32x32 que Unity pueda usar.

Por que hace falta: juliTILE.png dice "32x32" en el titulo pero no lo es. Es un
render suave (870.000 colores unicos, corridas horizontales de 1 px) con las
celdas dibujadas a ~51x56 px, irregulares, sobre un fondo opaco y con rotulos
de seccion encima. Cortarlo en una grilla de 32 devuelve basura.

Que hace en cambio:
  1. separa arte de fondo con flood-fill desde el borde (tol 11: mas alto se
     come los arboles y los pisos oscuros, que son casi del color del panel);
  2. saca cada elemento como componente conexo;
  3. lo reescala a un multiplo de 32 - eso corrige de paso que las celdas
     originales no sean cuadradas (50x56 -> 32x32);
  4. los repackea en una hoja limpia, en orden de lectura del sheet original
     para que los grupos (una pared, un camino) queden juntos en la paleta.

Salida: Assets/Sprites/Tilesets/JuliTile/JuliTile_00.png, lista para
`PokeRush > Armar paleta del tileset seleccionado`.

Uso:  python Codigo/Herramientas/juli_tileset.py <ruta-del-repo>
"""
import json
import os
import sys
from collections import Counter, deque

import numpy as np
from PIL import Image

ORIGEN = "Codigo/PokeRush/Assets/Sprites/Tilesets/juliTILE.png"
DESTINO = "Codigo/PokeRush/Assets/Sprites/Tilesets/JuliTile"

TILE = 32
TOL = 11                 # tolerancia del flood-fill de fondo
ESCALA = 32.0 / 51.2     # el arte esta dibujado a ~51 px por tile
MIN_LADO = 14            # menos que esto es texto o ruido
MIN_PIXELES = 120
COLS = 48                # ancho de la hoja de salida, en tiles

# Bandas donde viven los rotulos de seccion: ahi cualquier cosa baja es texto.
BANDAS_TEXTO = [(0, 30), (38, 56), (258, 278), (426, 444), (746, 766), (995, 1024)]

# Zonas que no son tiles.
ZONAS_EXCLUIDAS = [
    (1325, 770, 1536, 1000),   # PALETA DE COLORES: es una leyenda
    (728, 380, 1175, 740),     # EJEMPLO DE ARENA: es un montaje, no piezas
    (205, 765, 365, 1000),     # SOMBRAS / OVERLAYS: cuadrados negros sueltos
]

# Paneles del sheet. Sirven para etiquetar cada elemento con su rol, que es lo
# que despues usa el builder de la escena para elegir piso, pared o arbol.
SECCIONES = {
    "PISOS":      (5, 56, 366, 348),
    "PARED_INT":  (368, 56, 720, 348),
    "PARED_EXT":  (722, 56, 1016, 348),
    "TECH":       (1018, 56, 1176, 355),
    "DECORACION": (1178, 56, 1536, 424),
    "OBJETOS":    (5, 380, 366, 745),
    "ARENA":      (368, 380, 720, 745),
    "NATURALEZA": (1178, 444, 1536, 745),
    "ESCALERAS":  (5, 766, 202, 995),
    "PUERTAS":    (368, 766, 652, 995),
    "SENALES":    (654, 766, 976, 995),
    "DETALLES":   (978, 766, 1326, 995),
}


def seccion_de(x0, y0, x1, y1):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    for nombre, (a, b, c, d) in SECCIONES.items():
        if a <= cx <= c and b <= cy <= d:
            return nombre
    return "OTROS"


def mascara_fondo(rgb):
    """Fondo = todo lo alcanzable desde el borde con color parecido."""
    h, w, _ = rgb.shape
    fondo = np.zeros((h, w), bool)
    visto = np.zeros((h, w), bool)
    dq = deque()
    for x in range(w):
        for y in (0, h - 1):
            dq.append((y, x)); visto[y, x] = True
    for y in range(h):
        for x in (0, w - 1):
            if not visto[y, x]:
                dq.append((y, x)); visto[y, x] = True
    while dq:
        y, x = dq.popleft()
        c = rgb[y, x]
        fondo[y, x] = True
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and not visto[ny, nx]:
                if np.abs(rgb[ny, nx] - c).max() <= TOL:
                    visto[ny, nx] = True
                    dq.append((ny, nx))
    return fondo


def componentes(arte):
    h, w = arte.shape
    visto = np.zeros((h, w), bool)
    salida = []
    ys, xs = np.nonzero(arte)
    for sy, sx in zip(ys, xs):
        if visto[sy, sx]:
            continue
        dq = deque([(sy, sx)])
        visto[sy, sx] = True
        px = []
        x0 = x1 = int(sx); y0 = y1 = int(sy)
        while dq:
            y, x = dq.pop()
            px.append((y, x))
            x0 = min(x0, x); x1 = max(x1, x)
            y0 = min(y0, y); y1 = max(y1, y)
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    ny, nx = y + dy, x + dx
                    if 0 <= ny < h and 0 <= nx < w and not visto[ny, nx] and arte[ny, nx]:
                        visto[ny, nx] = True
                        dq.append((ny, nx))
        salida.append((x0, y0, x1, y1, px))
    return salida


def es_texto(y0, y1):
    if y1 - y0 + 1 > 22:
        return False
    cy = (y0 + y1) / 2
    return any(b0 <= cy <= b1 for b0, b1 in BANDAS_TEXTO)


def excluida(x0, y0, x1, y1):
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    return any(a <= cx <= c and b <= cy <= d for a, b, c, d in ZONAS_EXCLUIDAS)


def main(repo):
    src = os.path.join(repo, ORIGEN)
    if not os.path.exists(src):
        print(f"No existe {src}")
        return 1

    rgb = np.asarray(Image.open(src).convert("RGB")).astype(np.int16)

    print("1/4 separando fondo...")
    arte = ~mascara_fondo(rgb)
    print(f"    arte: {100 * arte.mean():.1f}% de la imagen")

    print("2/4 buscando elementos...")
    crudos = componentes(arte)
    elementos = [
        c for c in crudos
        if (c[2] - c[0] + 1) >= MIN_LADO and (c[3] - c[1] + 1) >= MIN_LADO
        and len(c[4]) >= MIN_PIXELES
        and not es_texto(c[1], c[3]) and not excluida(*c[:4])
    ]
    print(f"    {len(crudos)} componentes -> {len(elementos)} elementos utiles")

    # Orden de lectura del sheet: fila de paneles, despues fila dentro del
    # panel, despues columna. Asi los grupos quedan juntos en la paleta.
    elementos.sort(key=lambda c: (c[1] // 240, c[1] // 60, c[0]))

    print("3/4 reescalando a la grilla de 32...")
    recortes, secciones = [], []
    for x0, y0, x1, y1, px in elementos:
        secciones.append(seccion_de(x0, y0, x1, y1))
        alto, ancho = y1 - y0 + 1, x1 - x0 + 1
        sub = np.zeros((alto, ancho, 4), np.uint8)
        sub[..., :3] = rgb[y0:y1 + 1, x0:x1 + 1]
        m = np.zeros((alto, ancho), bool)
        for y, x in px:
            m[y - y0, x - x0] = True
        sub[..., 3] = m * 255

        cw = max(1, round(ancho * ESCALA / TILE)) * TILE
        ch = max(1, round(alto * ESCALA / TILE)) * TILE
        im = np.asarray(Image.fromarray(sub).resize((cw, ch), Image.LANCZOS)).copy()
        # alfa binario: un tile con bordes semitransparentes se ve sucio
        im[..., 3] = np.where(im[..., 3] > 110, 255, 0)
        recortes.append(Image.fromarray(im))

    print("4/4 armando la hoja...")
    # Empaquetado por filas en orden de lectura (no por tamaño: perder el
    # agrupamiento hace la paleta imposible de usar).
    filas, actual, ancho_actual, alto_actual = [], [], 0, 0
    for im, sec in zip(recortes, secciones):
        tw, th = im.width // TILE, im.height // TILE
        if ancho_actual + tw > COLS:
            filas.append((actual, alto_actual))
            actual, ancho_actual, alto_actual = [], 0, 0
        actual.append((im, sec))
        ancho_actual += tw
        alto_actual = max(alto_actual, th)
    if actual:
        filas.append((actual, alto_actual))

    alto_tiles = sum(f[1] for f in filas)
    hoja = Image.new("RGBA", (COLS * TILE, alto_tiles * TILE), (0, 0, 0, 0))
    manifiesto = []
    y = 0
    for items, alto_fila in filas:
        x = 0
        for im, sec in items:
            hoja.paste(im, (x * TILE, y * TILE), im)
            tw, th = im.width // TILE, im.height // TILE
            manifiesto.append({
                "seccion": sec, "col": x, "fila": y, "ancho": tw, "alto": th,
                "tiles": [f"JuliTile_00_{(y + f) * COLS + (x + c):04d}"
                          for f in range(th) for c in range(tw)],
            })
            x += tw
        y += alto_fila

    destino = os.path.join(repo, DESTINO)
    os.makedirs(destino, exist_ok=True)
    hoja.save(os.path.join(destino, "JuliTile_00.png"))
    with open(os.path.join(destino, "JuliTile_manifiesto.json"), "w", encoding="utf-8") as f:
        json.dump({"cols": COLS, "filas": alto_tiles, "elementos": manifiesto},
                  f, indent=1, ensure_ascii=False)

    cuenta = Counter(m["seccion"] for m in manifiesto)
    print(f"\n{len(recortes)} elementos en {hoja.width}x{hoja.height} "
          f"({COLS}x{alto_tiles} tiles) -> {DESTINO}/JuliTile_00.png")
    for sec, n in cuenta.most_common():
        print(f"    {sec:12s} {n}")
    print("Ahora en Unity: PokeRush > Armar paleta del tileset seleccionado")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "."))
