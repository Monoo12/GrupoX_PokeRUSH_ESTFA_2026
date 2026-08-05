"""
Convierte las texturas WebP de un .glb a PNG y saca la extension EXT_texture_webp.

Por que hace falta: los modelos de Pokemon-3D-api estan optimizados para la web
(Draco + WebP). WebP va declarado en "extensionsRequired", y por especificacion
glTF un loader DEBE rechazar el archivo entero si no soporta una extension
requerida. glTFast lo rechaza, asi que Unity no produce ningun GameObject.

Que hace:
  1. decodifica cada imagen image/webp a PNG (Pillow)
  2. reconstruye el chunk binario reubicando TODOS los bufferViews (los de las
     mallas Draco se copian intactos, solo cambian de offset)
  3. mueve el 'source' de texture.extensions.EXT_texture_webp al campo estandar
  4. borra EXT_texture_webp de extensionsRequired / extensionsUsed

Escribe a un temporal y recien al final reemplaza el original, asi un fallo a
mitad de camino no deja un .glb corrupto.
"""
import json
import os
import struct
import sys
from io import BytesIO

from PIL import Image

JSON_CHUNK = 0x4E4F534A  # 'JSON'
BIN_CHUNK = 0x004E4942   # 'BIN\0'
WEBP_EXT = "EXT_texture_webp"


def read_glb(path):
    with open(path, "rb") as fh:
        data = fh.read()

    magic, _version, _length = struct.unpack_from("<III", data, 0)
    if magic != 0x46546C67:
        raise ValueError("no es un .glb")

    gltf, binary, offset = None, b"", 12
    while offset < len(data):
        chunk_len, chunk_type = struct.unpack_from("<II", data, offset)
        payload = data[offset + 8: offset + 8 + chunk_len]
        if chunk_type == JSON_CHUNK:
            gltf = json.loads(payload.decode("utf-8"))
        elif chunk_type == BIN_CHUNK:
            binary = payload
        offset += 8 + chunk_len
        offset += (4 - offset % 4) % 4  # los chunks van alineados a 4
    return gltf, binary


def write_glb(path, gltf, binary):
    json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    json_bytes += b" " * ((4 - len(json_bytes) % 4) % 4)   # JSON se rellena con espacios
    binary += b"\x00" * ((4 - len(binary) % 4) % 4)        # BIN con ceros

    total = 12 + 8 + len(json_bytes) + (8 + len(binary) if binary else 0)
    out = bytearray()
    out += struct.pack("<III", 0x46546C67, 2, total)
    out += struct.pack("<II", len(json_bytes), JSON_CHUNK) + json_bytes
    if binary:
        out += struct.pack("<II", len(binary), BIN_CHUNK) + binary

    with open(path, "wb") as fh:
        fh.write(out)


def convert(path):
    """Devuelve (convertido, detalle)."""
    gltf, binary = read_glb(path)

    if WEBP_EXT not in gltf.get("extensionsUsed", []):
        return False, "sin webp"

    # 1. decodificar las imagenes webp a png
    replacements = {}
    for image in gltf.get("images", []):
        if image.get("mimeType") != "image/webp":
            continue
        view = gltf["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        raw = binary[start: start + view["byteLength"]]

        with Image.open(BytesIO(raw)) as img:
            buffer = BytesIO()
            img.convert("RGBA").save(buffer, format="PNG", optimize=True)
        replacements[image["bufferView"]] = buffer.getvalue()
        image["mimeType"] = "image/png"

    # 2. reconstruir el binario: los bufferViews cambian de tamaño, asi que hay
    #    que reubicar TODOS (incluidos los de geometria Draco, que se copian tal cual)
    new_binary = bytearray()
    for index, view in enumerate(gltf.get("bufferViews", [])):
        payload = replacements.get(index)
        if payload is None:
            start = view.get("byteOffset", 0)
            payload = binary[start: start + view["byteLength"]]

        new_binary += b"\x00" * ((4 - len(new_binary) % 4) % 4)
        view["byteOffset"] = len(new_binary)
        view["byteLength"] = len(payload)
        new_binary += payload

    if gltf.get("buffers"):
        gltf["buffers"][0]["byteLength"] = len(new_binary)

    # 3. la textura pasa a usar el campo estandar 'source'
    for texture in gltf.get("textures", []):
        extensions = texture.get("extensions", {})
        if WEBP_EXT in extensions:
            texture["source"] = extensions[WEBP_EXT]["source"]
            del extensions[WEBP_EXT]
            if not extensions:
                texture.pop("extensions", None)

    # 4. sacar la extension de las listas
    for key in ("extensionsRequired", "extensionsUsed"):
        if key in gltf:
            gltf[key] = [e for e in gltf[key] if e != WEBP_EXT]
            if not gltf[key]:
                del gltf[key]

    temp = path + ".tmp"
    write_glb(temp, gltf, bytes(new_binary))
    os.replace(temp, path)
    return True, f"{len(replacements)} texturas"


def main(folder):
    files = sorted(
        (f for f in os.listdir(folder) if f.endswith(".glb")),
        key=lambda f: int(os.path.splitext(f)[0]) if os.path.splitext(f)[0].isdigit() else 0,
    )
    converted = skipped = failed = 0
    for name in files:
        path = os.path.join(folder, name)
        try:
            did, detail = convert(path)
            if did:
                converted += 1
            else:
                skipped += 1
        except Exception as exc:  # noqa: BLE001 - se reporta y se sigue con el resto
            failed += 1
            print(f"  FALLO {name}: {exc}")
    print(f"Convertidos: {converted}. Sin webp: {skipped}. Fallidos: {failed}.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))
