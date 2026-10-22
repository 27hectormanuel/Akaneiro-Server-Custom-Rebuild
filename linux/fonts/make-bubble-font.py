"""Writes the font for the speech bubbles: a font from the game's own data, renamed to "Arial".

The bubbles ask for the system font "Arial" (see redirect/akaneiro_redirect.c). Handing the game a
font that calls itself Arial changes their look without changing the game:

    python make-bubble-font.py <game folder> <output .ttf> [font name in the game, default edo]

Needs UnityPy and fontTools (pip install UnityPy fonttools).
"""
import io
import os
import sys

import UnityPy
from fontTools.ttLib import TTFont


def main():
    game_dir, out_path = sys.argv[1], sys.argv[2]
    wanted = sys.argv[3] if len(sys.argv) > 3 else "edo"
    data = os.path.join(game_dir, "Akaneiro_Data")
    files = [os.path.join(data, f) for f in sorted(os.listdir(data)) if f.endswith(".assets") or f == "mainData"]
    env = UnityPy.load(*files)
    for obj in env.objects:
        if obj.type.name != "Font":
            continue
        font = obj.read()
        if font.m_Name.lower() == wanted.lower() and font.m_FontData:
            break
    else:
        sys.exit(f"No font '{wanted}' with embedded data in {data}")

    tt = TTFont(io.BytesIO(bytes(font.m_FontData)))
    names = tt["name"]
    for record in list(names.names):
        if record.nameID in (1, 4, 16):
            names.setName("Arial", record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID == 6:
            names.setName("ArialMT", record.nameID, record.platformID, record.platEncID, record.langID)
        elif record.nameID in (2, 17):
            names.setName("Regular", record.nameID, record.platformID, record.platEncID, record.langID)
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    tt.save(out_path)
    print(f"{out_path}: '{font.m_Name}' from the game, named Arial")


if __name__ == "__main__":
    main()
