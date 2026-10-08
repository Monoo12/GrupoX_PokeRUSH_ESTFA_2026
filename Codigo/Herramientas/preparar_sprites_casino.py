#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""Convierte los dibujos de personajes del casino en sprites que el juego puede
usar: Tacconi (piso 3) y Martin Reymar (el jefe del ultimo piso).

Los dibujos los genera el equipo con una IA y vienen en `Assets/<Personaje>/`.
Dejarlos caer en la escena NO alcanza, y saltearse estos pasos fue lo que hizo
que quedaran mal varias veces seguidas:

  1. **Sacar el fondo.** Ninguno viene con alpha de verdad. Tacconi trae el
     damero de transparencia PEGADO (se guardo como JPG) y Martin un gris
     azulado liso. Los dos se sacan rellenando **desde el borde**, no por color:
     asi los blancos y celestes de adentro del personaje —el cuello del polo, la
     camisa de Martin— quedan protegidos por el contorno negro.

     Ojo con Martin: su camisa celeste esta a 42 de distancia del gris del
     fondo, asi que un umbral por distancia se la come. Se separa por
     SATURACION: el fondo es gris (max-min = 14) y la camisa no (max-min = 51).

  2. **Volverlo pixel art.** El JPEG les mete ruido: salen con 18.000 a 21.000
     colores. Un personaje del pack de Essentials usa ~20 y lleva **contorno
     negro de 1 pixel**. Sin eso, en el mapa se ven como una mancha al lado de
     los demas por mas que la escala y el pivote esten bien. Entonces, despues
     de achicar: se **reduce la paleta sin difuminado** (a este tamano el dither
     es suciedad, no sombreado) y se **repinta el contorno por adentro** de la
     silueta, para no agrandar el sprite.

El alto de cada uno no es el mismo que el de los NPCs del pack (58 px) y esta
explicado en su constante. Los PNG que deja se importan solos: los configura
`CasinoNpcBuilder.SpriteDePersonaje` (32 px por unidad, pivote abajo al medio).

Uso:  python Codigo/Herramientas/preparar_sprites_casino.py
"""
import os
from collections import deque

import numpy as np
from PIL import Image

# La raiz del repo: este archivo esta en Codigo/Herramientas/.
RAIZ = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ASSETS = os.path.join(RAIZ, "Codigo", "PokeRush", "Assets")

CONTORNO = (0, 0, 0)


# ---------------------------------------------------------------- fondo

def _rellenar_desde_el_borde(es_fondo):
    """Marca que pixeles del candidato a fondo estan realmente AFUERA.

    Se hace por relleno y no por color para no comerse los colores claros de
    adentro del personaje, que estan encerrados por el contorno negro.
    """
    h, w = es_fondo.shape
    fuera = np.zeros((h, w), bool)
    q = deque()

    for x in range(w):
        for y in (0, h - 1):
            if es_fondo[y, x] and not fuera[y, x]:
                fuera[y, x] = True; q.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if es_fondo[y, x] and not fuera[y, x]:
                fuera[y, x] = True; q.append((y, x))

    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and es_fondo[ny, nx] and not fuera[ny, nx]:
                fuera[ny, nx] = True; q.append((ny, nx))

    return fuera


def sin_damero(img, umbral=190):
    """Para el que vino con el damero de transparencia pegado (blanco y gris)."""
    a = np.asarray(img.convert("RGB")).astype(np.int16)
    candidato = ((a.max(axis=2) - a.min(axis=2)) < 16) & (a.min(axis=2) > umbral)
    return _armar(a, _rellenar_desde_el_borde(candidato))


def sin_fondo_liso(img, color, tolerancia=70, saturacion_max=30):
    """Para el que vino con un fondo de un color liso.

    Pide las DOS cosas: parecido al color del fondo y poco saturado. Lo segundo
    es lo que salva la camisa celeste de Martin, que de puro parecido entra en
    la tolerancia pero es mucho mas saturada que el gris del fondo.
    """
    a = np.asarray(img.convert("RGB")).astype(np.int16)
    color = np.array(color, dtype=np.int16)
    parecido = np.abs(a - color).sum(axis=2) < tolerancia
    apagado = (a.max(axis=2) - a.min(axis=2)) < saturacion_max
    return _armar(a, _rellenar_desde_el_borde(parecido & apagado))


def _armar(rgb, fuera):
    rgba = np.dstack([rgb.astype(np.uint8), np.where(fuera, 0, 255).astype(np.uint8)])
    return recortar(Image.fromarray(rgba, "RGBA"))


# ---------------------------------------------------------------- pixel art

def recortar(img):
    bb = img.getbbox()
    return img.crop(bb) if bb else img


def achicar(img, alto):
    ancho = max(1, round(img.width * alto / img.height))
    return img.resize((ancho, alto), Image.LANCZOS)


def alfa_dura(img, corte=128):
    a = np.asarray(img).astype(np.uint8).copy()
    a[:, :, 3] = np.where(a[:, :, 3] > corte, 255, 0)
    return Image.fromarray(a, "RGBA")


def reducir_paleta(img, colores):
    a = np.asarray(img)
    opaco = a[:, :, 3] > 0
    rgb = Image.fromarray(a[:, :, :3], "RGB")
    q = rgb.quantize(colors=colores, method=Image.MEDIANCUT, dither=Image.NONE)
    rgb = np.asarray(q.convert("RGB"))
    return Image.fromarray(np.dstack([rgb, np.where(opaco, 255, 0).astype(np.uint8)]), "RGBA")


def contornear(img, color=CONTORNO, solo_lados_y_arriba=False):
    """Pinta de negro el anillo exterior de la silueta, por ADENTRO.

    Con 'solo_lados_y_arriba' no toca la fila de abajo: sirve para el busto de
    Martin, que se corta a la altura de la mesa y no tiene que llevar una linea
    negra ahi, porque no es un borde del dibujo sino donde lo tapa el mueble.
    """
    a = np.asarray(img).astype(np.uint8).copy()
    op = a[:, :, 3] > 0

    vecino_vacio = np.zeros_like(op)
    vecino_vacio[:-1, :] |= ~op[1:, :]
    vecino_vacio[1:, :] |= ~op[:-1, :]
    vecino_vacio[:, :-1] |= ~op[:, 1:]
    vecino_vacio[:, 1:] |= ~op[:, :-1]
    vecino_vacio[0, :] = True
    vecino_vacio[:, 0] = True
    vecino_vacio[:, -1] = True
    if not solo_lados_y_arriba:
        vecino_vacio[-1, :] = True

    borde = op & vecino_vacio
    a[borde, 0], a[borde, 1], a[borde, 2] = color
    return Image.fromarray(a, "RGBA")


def a_pixel_art(img, alto, colores, sin_borde_abajo=False):
    chico = recortar(alfa_dura(achicar(img, alto)))
    return contornear(reducir_paleta(chico, colores), solo_lados_y_arriba=sin_borde_abajo)


def guardar(img, destino):
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    img.save(destino)
    a = np.asarray(img)
    colores = len({tuple(c) for c in a[a[:, :, 3] > 0][:, :3]})
    print(f"  -> {os.path.relpath(destino, RAIZ)}  {img.size[0]}x{img.size[1]}  {colores} colores")


def retrato(fig, destino, alto_del_busto=0.62, lado=160, colores=32):
    """El cuadrito de la caja de dialogo, del tamano de los otros Trainers."""
    busto = recortar(fig.crop((0, 0, fig.width, round(fig.height * alto_del_busto))))
    busto = a_pixel_art(busto, lado - 8, colores)
    lienzo = Image.new("RGBA", (lado, lado), (0, 0, 0, 0))
    lienzo.paste(busto, ((lado - busto.width) // 2, lado - busto.height), busto)
    guardar(lienzo, destino)


# ---------------------------------------------------------------- personajes

# Tacconi va a 76 y no a 58 como los NPCs del pack: su dibujo tiene proporciones
# de unas 3 cabezas y los del pack 2,2, asi que igualar por ALTURA lo deja como
# un palito con la cara hecha una mancha. Se iguala por CABEZA.
TACCONI_ALTO = 76
TACCONI_COLORES = 24

# Martin es un BUSTO: se lo ve de la cintura para arriba, sentado atras de la
# mesa de poker. El alto sale de que su cabeza mida como la de los demas.
MARTIN_ALTO = 52
MARTIN_COLORES = 24
MARTIN_FONDO = (185, 194, 199)


def tacconi():
    print("Tacconi:")
    origen = os.path.join(ASSETS, "Tacconi", "Gemini_Generated_Image_v0fptev0fptev0fp.jpg")
    fig = sin_damero(Image.open(origen))
    guardar(a_pixel_art(fig, TACCONI_ALTO, TACCONI_COLORES),
            os.path.join(ASSETS, "Sprites", "Casino", "tacconi_frente.png"))
    retrato(fig, os.path.join(ASSETS, "Graphics", "Trainers", "TACCONI.png"))


def martin():
    print("Martin:")
    origen = os.path.join(ASSETS, "Martin", "Gemini_Generated_Image_tuyakntuyakntuya.jpg")
    fig = sin_fondo_liso(Image.open(origen), MARTIN_FONDO)
    # Sin contorno abajo: ahi lo tapa la mesa, no es un borde del dibujo.
    guardar(a_pixel_art(fig, MARTIN_ALTO, MARTIN_COLORES, sin_borde_abajo=True),
            os.path.join(ASSETS, "Sprites", "Casino", "martin_sentado.png"))
    # El retrato si lleva el busto entero, que ya es lo que es el dibujo.
    retrato(fig, os.path.join(ASSETS, "Graphics", "Trainers", "MARTIN.png"),
            alto_del_busto=1.0)


if __name__ == "__main__":
    tacconi()
    martin()
