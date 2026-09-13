#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Deja usable el arte de Assets/BlackJack God.

A diferencia de los recortes de la ruleta (ver limpiar_ruleta.py), estos PNG SI
vienen bien: tienen alpha de verdad y ya estan en la paleta que pide el juego.
No hay que recolorear nada. Lo unico que falta son tres cosas que la UI no
puede resolver sola:

  1. Las 52 cartas no se pueden cortar con una grilla. En la hoja estan a un
     paso de 48,46 px y cada carta salio de un tamanio distinto (de 47x72 a
     51x75), asi que una grilla regular corta media carta a partir de la
     septima. Aca se busca el corte real entre carta y carta (el minimo de
     alpha alrededor de la posicion teorica), se recorta cada una a su caja y
     se rearman todas en una grilla EXACTA.

     Ojo: se corta de la HOJA y no de los cuatro cartas_*.png sueltos. Esos son
     recortes desalineados de la hoja: a diamantes le faltan filas arriba y a
     treboles abajo, y los cuatro se llevaron una franja de la fila de al lado.

  2. La fila de treboles trae dos cartas mal dibujadas (defecto de la hoja
     original): en el lugar del 3 hay un 2, y la K salio con la letra pisada
     por otro glifo. Se arreglan trasplantando el numero de la carta
     equivalente de picas, que es el otro palo negro y usa la misma
     tipografia. El simbolo del palo, que es lo que diferencia una carta de la
     otra, no se toca.

  3. Las fichas traen su valor escrito DEBAJO del disco, y ademas cortado: en
     'ficha_50' y las tres siguientes el numero quedo partido al medio. El
     valor lo dibuja la UI con un Text (igual que en la ruleta), asi que aca se
     queda solo el disco.

  4. El croupier no viene en el pack. Sale de un NPC que ya esta en el juego:
     Graphics/Characters/noble.png, el senior de galera y traje oscuro, que es
     lo mas parecido a un croupier que hay en el pack de Essentials. Se le
     sacan los cuatro cuadros de la fila que mira hacia adelante y quedan en su
     propia hoja. Se copia en vez de reimportar el original porque ese PNG lo
     usan los mapas como sprite entero: pasarlo a Multiple para cortarlo le
     cambiaria la referencia a quien ya lo tenga puesto.

Ademas se limpia el texto de ejemplo de dialogo_normal.png ('¿Que haras?'), que
en el juego lleva texto variable.

Sobre el TAMANIO de salida (esto es lo que la primera version tuvo mal en la
ruleta y conviene no repetir): el CanvasScaler del juego trabaja con una
referencia de 512x384, asi que a 1920 de ancho todo se agranda 3,75x. Una carta
de 47 px dibujada a 42 unidades de Canvas se estira 3,3x y se ve como un
borron. Por eso la hoja de cartas sale escalada x3 con NEAREST: la celda de
156 px cae casi 1:1 en pantalla (156 / 3,75 = 41,6 unidades) y el pixel art se
ve nitido. Las fichas ya vienen a ~88 px, que es mas o menos como se usan.

Uso:  python scripts/cortar_arte_blackjack.py
"""

import os

import numpy as np
from PIL import Image

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CARPETA = os.path.join(RAIZ, "Codigo", "PokeRush", "Assets", "BlackJack God")
HOJA = "blackjack_sprite_sheet_completo.png"

# Los generados llevan prefijo para no mezclarse con los originales, que quedan
# intactos al lado (el importer de Unity solo mira los 'bj_').
PREFIJO = "bj_"

ALPHA = 16          # arriba de esto se considera dibujo

# ---- cartas ----
# La zona de las cartas dentro de la hoja: cuatro filas de trece.
CARTAS_X0, CARTAS_X1 = 614, 1244
# Arriba y abajo de cada fila. El alto sobrante de las filas 2 a 4 es la sombra
# que cae debajo de la carta, y va incluida a proposito.
CARTAS_FILAS = [(11, 83), (87, 163), (168, 242), (248, 321)]

# Orden de los palos, de arriba hacia abajo en la hoja. Coincide con
# BlackjackSuit del juego, y de ese orden depende el indice de cada sprite.
PALOS = ["picas", "corazones", "diamantes", "treboles"]

# Celda de la grilla de salida, antes de escalar. Entra la carta mas grande
# (51x75) con un pixel de aire.
CELDA = (52, 76)
# x2 con NEAREST. El CanvasScaler es ScaleWithScreenSize / Expand sobre 512x384,
# asi que en un 16:9 de 1080 el factor es 2,8125 (lo manda el alto). Una celda de
# 104 px dibujada a 37 unidades cae 1:1; con x3 habria que dibujarla a 55, que no
# entra en la mesa, y achicarla se come columnas de pixeles justo en los numeros.
ESCALA = 2

# Las dos cartas de treboles que salieron mal, y de que carta de picas se copia
# el numero. Indice 0 = As, 12 = K.
TREBOLES_ROTAS = {2: 2, 12: 12}    # el 3 y la K

# Recuadro del numero dentro de la celda ya normalizada. El numero vive arriba
# a la izquierda; el simbolo del palo esta de la mitad para abajo.
NUMERO_CAJA = (0, 0, 30, 32)       # x0, y0, x1, y1

# ---- fichas ----
# El disco termina en la fila 89 y el texto arranca en la 92: hay una fila
# entera vacia en el medio, asi que el corte es limpio.
FICHAS = [1, 5, 10, 25, 50, 100, 500, 1000]
FICHA_ALTO = 90

# ---- croupier ----
CROUPIER_HOJA = os.path.join(RAIZ, "Codigo", "PokeRush", "Assets",
                             "Graphics", "Characters", "noble.png")
# Las hojas de personaje del pack son 4x4: cuatro cuadros por direccion, en el
# orden abajo / izquierda / derecha / arriba. El croupier mira siempre de frente.
CROUPIER_FILA = 0
CROUPIER_CUADROS = 4
CROUPIER_ESCALA = 3

# ---- dialogo ----
# Zona interior del cuadro de dialogo: todo lo que hay que borrar es el texto
# de ejemplo y la flechita de 'seguir', que aca no corresponde porque el cuadro
# no se pasa con una tecla. El marco queda intacto.
DIALOGO_CAJA = (22, 26, 488, 103)
# Fila limpia del propio cuadro de la que se saca el color de relleno. Cae
# entre el texto y la flecha, y no tiene un solo pixel de tinta.
DIALOGO_FILA_LIMPIA = 70
# Cuanto se tiene que despegar un pixel de esa fila para contarlo como tinta.
# Bajo a proposito: con 40 el trazo se iba pero quedaba el halo del antialias
# como un fantasma perfectamente legible.
DIALOGO_TOLERANCIA = 10


def cargar(nombre):
    return Image.open(os.path.join(CARPETA, nombre)).convert("RGBA")


def guardar(im, nombre):
    im.save(os.path.join(CARPETA, PREFIJO + nombre))
    print(f"  {PREFIJO + nombre}: {im.width}x{im.height}")


def mascara(im):
    return np.array(im)[:, :, 3] > ALPHA


def recortar_alpha(im):
    caja = im.getchannel("A").point(lambda v: 255 if v > ALPHA else 0).getbbox()
    return im.crop(caja) if caja else im


# --------------------------------------------------------------------- cartas

def cortes_de_la_fila(mask, y0, y1):
    """Los 14 bordes verticales que separan las 13 cartas de una fila.

    Las cartas estan a un paso de 48,46 px y algunas se tocan por la sombra, asi
    que no alcanza ni una grilla regular ni buscar columnas vacias. Lo que si
    funciona: alrededor de cada borde teorico, quedarse con la columna que menos
    dibujo tiene. Esa es la juntura, este vacia del todo o no.
    """
    paso = (CARTAS_X1 - CARTAS_X0) / 13.0
    perfil = mask[y0:y1, :].sum(axis=0)

    bordes = [CARTAS_X0]
    for i in range(1, 13):
        teorico = int(round(CARTAS_X0 + i * paso))
        candidatos = range(teorico - 3, teorico + 4)
        bordes.append(min(candidatos, key=lambda x: (perfil[x], abs(x - teorico))))
    bordes.append(CARTAS_X1)
    return bordes


def normalizar(carta):
    """Centra la carta horizontalmente en la celda y la apoya arriba.

    Se alinea por ARRIBA y no al centro porque el borde de arriba es el mismo en
    todas; lo que cambia de fila en fila es cuanta sombra quedo abajo, y
    centrando esa diferencia se repartiria en las dos puntas y las cartas se
    verian saltar al repartirse.
    """
    ancho, alto = CELDA
    celda = Image.new("RGBA", CELDA, (0, 0, 0, 0))
    celda.paste(carta, ((ancho - carta.width) // 2, 0))
    return celda


def cartas_de_la_hoja(hoja):
    mask = mascara(hoja)
    por_palo = {}

    for palo, (y0, y1) in zip(PALOS, CARTAS_FILAS):
        bordes = cortes_de_la_fila(mask, y0, y1)
        fila = []
        for i in range(13):
            trozo = hoja.crop((bordes[i], y0, bordes[i + 1], y1))
            fila.append(normalizar(recortar_alpha(trozo)))
        por_palo[palo] = fila

    return por_palo


def arreglar_treboles(por_palo):
    for destino, origen in TREBOLES_ROTAS.items():
        parche = por_palo["picas"][origen].crop(NUMERO_CAJA)
        carta = por_palo["treboles"][destino].copy()
        # Se pega BORRANDO lo que habia: componer encima dejaria el numero viejo
        # asomando por debajo del nuevo.
        carta.paste(parche, (NUMERO_CAJA[0], NUMERO_CAJA[1]))
        por_palo["treboles"][destino] = carta
    return por_palo


def encajar(im, ancho, alto):
    """Reescala manteniendo proporcion y centra en un lienzo de ancho x alto."""
    escala = min(ancho / im.width, alto / im.height)
    nuevo = im.resize((max(1, round(im.width * escala)),
                       max(1, round(im.height * escala))), Image.NEAREST)
    lienzo = Image.new("RGBA", (ancho, alto), (0, 0, 0, 0))
    lienzo.paste(nuevo, ((ancho - nuevo.width) // 2, (alto - nuevo.height) // 2))
    return lienzo


def hoja_de_cartas():
    """Grilla de 13x5: los cuatro palos y una fila con los dos reversos.

    Los reversos van en la MISMA grilla y no como PNG sueltos porque tienen que
    medir exactamente lo mismo que una carta: el original es de 105x150, o sea
    otra escala, y estirarlo dentro del rect de una carta duplicaria pixeles de
    forma despareja justo en el damero, que es donde mas se nota.
    """
    por_palo = arreglar_treboles(cartas_de_la_hoja(cargar(HOJA)))

    cw, ch = CELDA[0] * ESCALA, CELDA[1] * ESCALA
    grilla = Image.new("RGBA", (13 * cw, 5 * ch), (0, 0, 0, 0))

    for fila, palo in enumerate(PALOS):
        for col, carta in enumerate(por_palo[palo]):
            grilla.paste(carta.resize((cw, ch), Image.NEAREST), (col * cw, fila * ch))

    # Los reversos se ajustan al ANCHO DE LA CARTA, no al de la celda: una
    # carta ocupa ~49 de los 52 px y el reverso, que es mas ancho de origen,
    # llenaba la celda entera y se veia un 10% mas grande que las de adelante.
    for col, archivo in enumerate(["reverso_rojo.png", "reverso_azul.png"]):
        reverso = recortar_alpha(cargar(archivo))
        ajustado = encajar(reverso, 49 * ESCALA, 74 * ESCALA)
        celda = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        celda.paste(ajustado, ((cw - ajustado.width) // 2, (ch - ajustado.height) // 2))
        grilla.paste(celda, (col * cw, 4 * ch))

    return grilla, cw, ch


# --------------------------------------------------------------------- fichas

def hoja_de_fichas():
    """Los ocho discos en una tira, sin el valor escrito debajo."""
    discos = [recortar_alpha(cargar(f"ficha_{v}.png").crop((0, 0, 110, FICHA_ALTO)))
              for v in FICHAS]

    ancho = max(d.width for d in discos)
    alto = max(d.height for d in discos)
    tira = Image.new("RGBA", (ancho * len(discos), alto), (0, 0, 0, 0))
    for i, d in enumerate(discos):
        tira.paste(d, (i * ancho + (ancho - d.width) // 2, (alto - d.height) // 2))
    return tira, ancho, alto


# ------------------------------------------------------------------ croupier

def hoja_del_croupier():
    """Los cuatro cuadros del croupier de frente, en una tira.

    Se recortan todos a la MISMA caja (la union de las cuatro) y no cada uno a
    la suya: recortando por separado, los cuadros en los que el personaje
    levanta un pie quedan de otro alto y el croupier se ve dar saltitos al
    animarse.
    """
    hoja = Image.open(CROUPIER_HOJA).convert("RGBA")
    cw, ch = hoja.width // 4, hoja.height // 4

    cuadros = [hoja.crop((i * cw, CROUPIER_FILA * ch, (i + 1) * cw, (CROUPIER_FILA + 1) * ch))
               for i in range(CROUPIER_CUADROS)]

    caja = None
    for c in cuadros:
        b = c.getchannel("A").point(lambda v: 255 if v > ALPHA else 0).getbbox()
        if b is None:
            continue
        caja = b if caja is None else (min(caja[0], b[0]), min(caja[1], b[1]),
                                       max(caja[2], b[2]), max(caja[3], b[3]))
    if caja is None:
        caja = (0, 0, cw, ch)

    ancho = (caja[2] - caja[0]) * CROUPIER_ESCALA
    alto = (caja[3] - caja[1]) * CROUPIER_ESCALA

    tira = Image.new("RGBA", (ancho * CROUPIER_CUADROS, alto), (0, 0, 0, 0))
    for i, c in enumerate(cuadros):
        tira.paste(c.crop(caja).resize((ancho, alto), Image.NEAREST), (i * ancho, 0))
    return tira, ancho, alto


# -------------------------------------------------------------------- dialogo

def dilatar(mask):
    """Engorda la mascara un pixel en las cuatro direcciones.

    El borde del glifo se va apagando y los ultimos pixeles quedan por debajo de
    cualquier tolerancia razonable; sin engordar, el texto se sigue leyendo como
    una marca de agua.
    """
    salida = mask.copy()
    salida[1:, :] |= mask[:-1, :]
    salida[:-1, :] |= mask[1:, :]
    salida[:, 1:] |= mask[:, :-1]
    salida[:, :-1] |= mask[:, 1:]
    return salida


def dialogo_vacio():
    """El cuadro de dialogo sin el texto de ejemplo.

    El relleno se toma del propio cuadro (una franja de la parte de abajo, que
    esta limpia) y se repite: un color plano se notaria, porque el papel tiene
    un ruido suave.
    """
    im = cargar("dialogo_normal.png")
    x0, y0, x1, y1 = DIALOGO_CAJA

    datos = np.array(im).astype(int)
    limpia = datos[DIALOGO_FILA_LIMPIA]

    # Se borra SOLO lo que sea tinta, no toda la caja: el papel tiene un
    # sombreado propio (mas oscuro contra el marco) y rellenar el rectangulo
    # entero con una franja copiada lo deja a rayas.
    zona = datos[y0:y1, x0:x1]
    ref = np.broadcast_to(limpia[x0:x1], zona.shape)
    tinta = np.abs(zona - ref).sum(axis=2) > DIALOGO_TOLERANCIA
    tinta = dilatar(tinta)

    zona[tinta] = ref[tinta]
    datos[y0:y1, x0:x1] = zona
    return Image.fromarray(datos.astype("uint8"), "RGBA")


# ----------------------------------------------------------------------- main

def main():
    if not os.path.isdir(CARPETA):
        raise SystemExit(f"No existe {CARPETA}")

    print("Arte de blackjack:")

    grilla, cw, ch = hoja_de_cartas()
    guardar(grilla, "cartas.png")
    print(f"    celda {cw}x{ch}, 13 columnas x 5 filas "
          f"({', '.join(PALOS)}, reversos)")

    tira, fw, fh = hoja_de_fichas()
    guardar(tira, "fichas.png")
    print(f"    celda {fw}x{fh}, {len(FICHAS)} fichas: {FICHAS}")

    croupier, ccw, cch = hoja_del_croupier()
    guardar(croupier, "croupier.png")
    print(f"    celda {ccw}x{cch}, {CROUPIER_CUADROS} cuadros (de noble.png)")

    guardar(dialogo_vacio(), "dialogo.png")


if __name__ == "__main__":
    main()
