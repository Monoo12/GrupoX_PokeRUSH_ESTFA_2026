"""Parte los tilesets de RPG Maker en trozos que Unity pueda importar.

Por que fuera de Unity: los sheets van de 14.272 a 23.370 px de alto y el
limite duro de textura de Unity son 16.384. Los que lo superan no se pueden
cargar NI a resolucion completa, asi que no hay forma de leer sus pixeles
desde un script de editor. Hay que cortarlos antes.

Se conserva el orden original de la grilla (no se reempaqueta ni se
deduplica) porque en la Tile Palette importa que los grupos de tiles -una
casa, un camino, un arbol- queden juntos y reconocibles.
"""
import os
import sys
from PIL import Image

TILE = 32
FILAS_POR_TROZO = 256          # 256 * 32 = 8.192 px, bien debajo del limite
ORIGEN = "Codigo/PokeRush/Assets/Graphics/Tilesets"
DESTINO = "Codigo/PokeRush/Assets/Sprites/Tilesets"


def partir(repo, nombres):
    origen = os.path.join(repo, ORIGEN)
    destino = os.path.join(repo, DESTINO)
    total_trozos = 0

    for nombre in nombres:
        src = os.path.join(origen, nombre + ".png")
        if not os.path.exists(src):
            print(f"  FALTA {nombre}.png")
            continue

        im = Image.open(src).convert("RGBA")
        w, h = im.size
        filas = h // TILE
        cols = w // TILE
        carpeta = os.path.join(destino, nombre)
        os.makedirs(carpeta, exist_ok=True)

        # limpiamos trozos de corridas anteriores para no dejar huerfanos
        for viejo in os.listdir(carpeta):
            if viejo.endswith(".png") or viejo.endswith(".png.meta"):
                os.remove(os.path.join(carpeta, viejo))

        trozos = 0
        for i in range(0, filas, FILAS_POR_TROZO):
            alto = min(FILAS_POR_TROZO, filas - i) * TILE
            corte = im.crop((0, i * TILE, cols * TILE, i * TILE + alto))
            # un trozo enteramente vacio no aporta nada a la paleta
            if corte.getbbox() is None:
                continue
            corte.save(os.path.join(carpeta, f"{nombre}_{trozos:02d}.png"))
            trozos += 1

        total_trozos += trozos
        print(f"  {nombre:26s} {w}x{h} = {cols}x{filas} tiles -> {trozos} trozos")

    print(f"\nTotal: {total_trozos} trozos en {DESTINO}/")


if __name__ == "__main__":
    repo = sys.argv[1]
    nombres = sys.argv[2:]
    print("Partiendo tilesets (se conserva el orden de la grilla):")
    partir(repo, nombres)
