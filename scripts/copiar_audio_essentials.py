"""Copia del pack de audio de Essentials (Assets/AudioAÑIL, solo local) lo que
el juego usa, con nombres claros:

  Assets/Audio/Combate/Gritos/<asset>.ogg       un grito por especie (0001_Bulbasaur.ogg)
  Assets/Audio/Combate/Ataques/<slug>[_N].wav   el sonido de cada movimiento
  Assets/Audio/Efectos/<carpeta>/<nombre>       menus, mundo, combate y casino

El pack esta en el .gitignore (351 MB y un Game.exe): esto es lo que se
versiona. Se puede volver a correr; no pisa archivos que ya existen.

Despues de correrlo: Unity importa solo, y `PokeRush > Audio > Armar catalogo
de audio` arma los indices (gritos y ataques) y el catalogo.

Uso:  python scripts/copiar_audio_essentials.py
"""
import os
import re
import shutil
import sys

RAIZ = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Codigo", "PokeRush", "Assets")
PACK = os.path.join(RAIZ, "AudioAÑIL")
SE = os.path.join(PACK, "SE")
ME = os.path.join(PACK, "ME")
DEFS = os.path.join(RAIZ, "Resources", "Definitions")
DESTINO_GRITOS = os.path.join(RAIZ, "Audio", "Combate", "Gritos")
DESTINO_ATAQUES = os.path.join(RAIZ, "Audio", "Combate", "Ataques")
DESTINO_EFECTOS = os.path.join(RAIZ, "Audio", "Efectos")

# Efectos sueltos: (origen relativo al pack, carpeta destino, nombre destino)
EFECTOS = [
    # Menus
    ("SE/GUI sel cursor.ogg", "Menus", "menu_cursor.ogg"),
    ("SE/GUI sel cancel.ogg", "Menus", "menu_cancelar.ogg"),
    ("SE/GUI menu open.ogg", "Menus", "menu_abrir.ogg"),
    ("SE/GUI menu close.ogg", "Menus", "menu_cerrar.ogg"),
    ("SE/GUI sel buzzer.ogg", "Menus", "menu_error.ogg"),
    ("ME/GUI save game.wav", "Menus", "guardar_partida.wav"),
    ("SE/Mart buy item.ogg", "Menus", "tienda_comprar.ogg"),
    ("SE/PC close.ogg", "Menus", "pc_apagar.ogg"),
    ("SE/GUI storage pick up.ogg", "Menus", "pc_agarrar.ogg"),
    ("SE/GUI storage put down.ogg", "Menus", "pc_soltar.ogg"),
    # Mundo
    ("SE/Door enter.ogg", "Mundo", "puerta_entrar.ogg"),
    ("SE/Door exit.wav", "Mundo", "puerta_salir.wav"),
    ("SE/Elevator moving.ogg", "Mundo", "ascensor_viaja.ogg"),
    ("SE/Elevator end.ogg", "Mundo", "ascensor_llega.ogg"),
    ("SE/Exclaim.ogg", "Mundo", "exclamacion.ogg"),
    ("SE/Player bump.ogg", "Mundo", "choque_pared.ogg"),
    ("ME/Pkmn get.wav", "Mundo", "pokemon_recibido.wav"),
    # Combate
    ("SE/Battle throw.ogg", "Combate", "lanzar_ball.ogg"),
    ("SE/Battle recall.ogg", "Combate", "retirar.ogg"),
    ("SE/Pkmn faint.ogg", "Combate", "debilitado.ogg"),
    ("SE/Battle damage normal.ogg", "Combate", "golpe_normal.ogg"),
    ("SE/Battle damage super.ogg", "Combate", "golpe_super_eficaz.ogg"),
    ("SE/Battle damage weak.ogg", "Combate", "golpe_poco_eficaz.ogg"),
    ("SE/Pkmn level up.ogg", "Combate", "subir_nivel.ogg"),
    ("SE/Battle flee.ogg", "Combate", "huir.ogg"),
    ("SE/Use item in party.ogg", "Combate", "usar_objeto.ogg"),
    ("SE/PRSFX- Stat Up.wav", "Combate", "stat_sube.wav"),
    ("SE/PRSFX- Stat Down.wav", "Combate", "stat_baja.wav"),
    ("SE/PRSFX- Burn.wav", "Combate", "estado_quemado.wav"),
    ("SE/PRSFX- Poison.wav", "Combate", "estado_envenenado.wav"),
    ("SE/PRSFX- Paralysis.wav", "Combate", "estado_paralizado.wav"),
    ("SE/PRSFX- Sleep.wav", "Combate", "estado_dormido.wav"),
    ("SE/PRSFX- Frozen.wav", "Combate", "estado_congelado.wav"),
    ("SE/PRSFX- Confused.wav", "Combate", "estado_confundido.wav"),
    ("ME/VictoriaEntrenador.ogg", "Combate", "victoria_entrenador.ogg"),
    ("ME/Victoria Importante.ogg", "Combate", "victoria_importante.ogg"),
    # Casino
    ("SE/Slots stop.ogg", "Casino", "slots_frena.ogg"),
    ("SE/Slots coin.ogg", "Casino", "slots_moneda.ogg"),
    ("ME/Slots win.ogg", "Casino", "premio_chico.ogg"),
    ("ME/Slots big win.ogg", "Casino", "premio_grande.ogg"),
]

# Especies cuyo nombre en español no es el interno del pack (que esta en ingles).
ALIAS_ESPECIES = {
    "Código Cero": "TYPENULL",
    "Colmilargo": "GREATTUSK",
    "Colagrito": "SCREAMTAIL",
    "Furioseta": "BRUTEBONNET",
    "Melenaleteo": "FLUTTERMANE",
    "Reptalada": "SLITHERWING",
    "Pelarena": "SANDYSHOCKS",
    "Ferrodada": "IRONTREADS",
    "Ferrosaco": "IRONBUNDLE",
    "Ferropalmas": "IRONHANDS",
    "Ferrocuello": "IRONJUGULIS",
    "Ferropolilla": "IRONMOTH",
    "Ferropúas": "IRONTHORNS",
    "Bramaluna": "ROARINGMOON",
    "Ferropaladín": "IRONVALIANT",
    "Ondulagua": "WALKINGWAKE",
    "Ferroverdor": "IRONLEAVES",
    "Flamariete": "GOUGINGFIRE",
    "Electrofuria": "RAGINGBOLT",
    "Ferromole": "IRONBOULDER",
    "Ferrotesta": "IRONCROWN",
}

# Movimientos cuyo nombre en el pack no coincide con el slug de PokeAPI.
ALIAS_ATAQUES = {
    "hijumpkick": "highjumpkick",
    "theif": "thief",
    "selfedestruct": "selfdestruct",
    "stealthrocks": "stealthrock",
    "watershurkein": "watershuriken",
    "zpower": None,
}


def normalizar(texto):
    return re.sub(r"[^a-z0-9]", "", texto.lower())


def leer_campo(ruta, campo):
    with open(ruta, encoding="utf-8", errors="replace") as f:
        for linea in f:
            linea = linea.strip()
            if linea.startswith(campo + ":"):
                return linea.split(":", 1)[1].strip().strip('"')
    return None


def copiar(origen, destino):
    if os.path.exists(destino):
        return False
    os.makedirs(os.path.dirname(destino), exist_ok=True)
    shutil.copy2(origen, destino)
    return True


def gritos():
    disponibles = {}
    carpeta = os.path.join(SE, "Cries")
    for nombre in os.listdir(carpeta):
        if nombre.endswith(".meta"):
            continue
        base, ext = os.path.splitext(nombre)
        disponibles[base.upper()] = os.path.join(carpeta, nombre)

    copiados, faltan = 0, []
    carpeta_defs = os.path.join(DEFS, "Pokens")
    for archivo in sorted(os.listdir(carpeta_defs)):
        if not archivo.endswith(".asset"):
            continue
        # "0122_Mr. Mime.asset" -> "MRMIME"
        especie = re.sub(r"^\d+_", "", archivo[:-len(".asset")])
        clave = ALIAS_ESPECIES.get(especie) or re.sub(r"[^A-Z0-9]", "", especie.upper())
        origen = disponibles.get(clave)
        if origen is None:
            faltan.append(especie)
            continue
        # Con el nombre del asset de la especie: el catalogo los empareja por nombre.
        destino = archivo[:-len(".asset")] + os.path.splitext(origen)[1]
        if copiar(origen, os.path.join(DESTINO_GRITOS, destino)):
            copiados += 1
    print(f"Gritos: {copiados} copiados, {len(faltan)} especies sin grito en el pack: {faltan[:20]}")


def ataques():
    # clave normalizada -> lista de (numero de parte, ruta)
    partes = {}
    for nombre in os.listdir(SE):
        if not nombre.startswith("PRSFX- ") or nombre.endswith(".meta"):
            continue
        base = os.path.splitext(nombre)[0][len("PRSFX- "):]
        if "(2)" in base:
            continue
        m = re.match(r"^(.*?)(\d*)$", base.strip())
        clave = normalizar(m.group(1))
        numero = int(m.group(2)) if m.group(2) else 0
        clave = ALIAS_ATAQUES.get(clave, clave)
        if clave is None:
            continue
        partes.setdefault(clave, []).append((numero, os.path.join(SE, nombre)))

    copiados, sin_sonido = 0, []
    carpeta_defs = os.path.join(DEFS, "Moves")
    for archivo in sorted(os.listdir(carpeta_defs)):
        if not archivo.endswith(".asset"):
            continue
        slug = leer_campo(os.path.join(carpeta_defs, archivo), "englishName")
        if not slug:
            continue
        lista = partes.get(normalizar(slug))
        if not lista:
            sin_sonido.append(slug)
            continue
        lista.sort()
        if len(lista) == 1:
            if copiar(lista[0][1], os.path.join(DESTINO_ATAQUES, slug + ".wav")):
                copiados += 1
        else:
            for i, (_, ruta) in enumerate(lista, start=1):
                if copiar(ruta, os.path.join(DESTINO_ATAQUES, f"{slug}_{i}.wav")):
                    copiados += 1
    print(f"Ataques: {copiados} archivos copiados; {len(sin_sonido)} movimientos sin sonido propio "
          f"(usan el de su tipo): {sin_sonido[:25]}")


def efectos():
    copiados = 0
    for origen, carpeta, nombre in EFECTOS:
        ruta = os.path.join(PACK, origen)
        if not os.path.exists(ruta):
            print("  FALTA en el pack:", origen)
            continue
        if copiar(ruta, os.path.join(DESTINO_EFECTOS, carpeta, nombre)):
            copiados += 1
    print(f"Efectos: {copiados} copiados de {len(EFECTOS)}")


if __name__ == "__main__":
    if not os.path.isdir(PACK):
        sys.exit("No esta Assets/AudioAÑIL: el pack de Essentials vive solo en local.")
    gritos()
    ataques()
    efectos()
