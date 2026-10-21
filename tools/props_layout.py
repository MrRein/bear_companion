"""Къде стои всяка рисунка в assets/props (по папки).

    python tools/props_layout.py        подрежда файловете и оправя props.json

Името на рисунката в програмата (prop) не се сменя – сменя се само къде е файлът:

    food/       храната: food_pizza -> food/pizza.png, парче: food_pizza_2 -> food/pizza_piece_2.png,
                иконката за менюто: food_pizza_menu -> food/pizza_menu.png
    kitchen/    уредите за готвене (cook_*); скиновете – kitchen/skins/
    furniture/  мебелите и предметите за слагане (легло, диван, бюро, статив, печица,
                лампа, радио, хранилка, саксия…); скиновете – furniture/skins/
    (направо)   останалото: птички, пеперуди, подаръци, пратки, иконки за магазина…

Програмата (Sprites.cs) намира файла по същите правила: обхожда папките и от пътя
връща името (само за храната името е различно от файла).
"""

import json
import os
import re
import shutil

ROOT = os.path.join(os.path.dirname(__file__), "..")
PROPS = os.path.join(ROOT, "assets", "props")

KITCHEN = ("cook_coffee_machine", "cook_grill", "cook_icecream", "cook_jug_berry", "cook_jug_orange",
           "cook_kettle", "cook_mixer", "cook_oven", "cook_pan", "cook_pot", "cook_sandwich", "cook_table")
FURNITURE = ("bed", "couch", "desk", "stool", "table_board", "table_tea", "wardrobe", "yarn_basket",
             "trampoline", "mat", "computer", "easel", "heater", "lamp", "radio", "feeder", "plant")
# Кадри/състояния на една и съща рисунка (не са скин).
STATES = ("_b", "_open", "_off", "_food")


def _split(name, bases):
    """(основа, останалото) – най-дългата основа, с която почва името."""
    for base in sorted(bases, key=len, reverse=True):
        if name == base or name.startswith(base + "_"):
            return base, name[len(base):]
    return None, None


def _is_skin(base, rest):
    for s in STATES:
        if rest.endswith(s):
            rest = rest[: -len(s)]
            break
    rest = rest.lstrip("_")
    if not rest or rest == "classic":
        return False
    if base == "easel" and rest.isdigit():          # картините на статива
        return False
    if base == "plant":                             # plant_<вид>_<фаза>: слънчогледът е класическият
        return not rest.startswith("sunflower")
    return True


def rel(name):
    """Пътят на файла (спрямо assets/props) за рисунката name."""
    if name.startswith("food_"):
        rest = name[5:]
        m = re.fullmatch(r"(.+)_(\d+)", rest)
        if m:
            rest = f"{m.group(1)}_piece_{m.group(2)}"
        return f"food/{rest}.png"
    for folder, bases in (("kitchen", KITCHEN), ("furniture", FURNITURE)):
        base, rest = _split(name, bases)
        if base is not None:
            return f"{folder}/skins/{name}.png" if _is_skin(base, rest) else f"{folder}/{name}.png"
    return f"{name}.png"


def name_of(relpath):
    """Обратното: от пътя на файла – името на рисунката."""
    relpath = relpath.replace("\\", "/")
    stem = os.path.splitext(os.path.basename(relpath))[0]
    if relpath.startswith("food/"):
        return "food_" + re.sub(r"_piece_(\d+)$", r"_\1", stem)
    return stem


def path(name):
    """Пълният път до файла на рисунката (папката се прави, ако я няма)."""
    p = os.path.join(PROPS, rel(name))
    os.makedirs(os.path.dirname(p), exist_ok=True)
    return p


def all_names():
    """Всички рисунки, които ги има (име -> пълен път)."""
    out = {}
    for folder, _, files in os.walk(PROPS):
        for f in files:
            if f.endswith(".png"):
                full = os.path.join(folder, f)
                out[name_of(os.path.relpath(full, PROPS))] = full
    return out


def sync():
    """Мести всяка рисунка на мястото ѝ и оправя пътищата в props.json."""
    moved = 0
    for name, full in all_names().items():
        want = path(name)
        if os.path.normpath(full) != os.path.normpath(want):
            shutil.move(full, want)
            moved += 1
    # Празни папки (след местене) – махат се.
    for folder, dirs, files in os.walk(PROPS, topdown=False):
        if folder != PROPS and not os.listdir(folder):
            os.rmdir(folder)
    manifest_path = os.path.join(PROPS, "props.json")
    with open(manifest_path, encoding="utf-8") as fh:
        manifest = json.load(fh)
    for name, entry in manifest["props"].items():
        entry["file"] = rel(name)
    with open(manifest_path, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
        fh.write("\n")
    return moved


if __name__ == "__main__":
    print("Преместени:", sync())
