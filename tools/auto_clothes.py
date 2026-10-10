"""Прави шаблоните за дрехите от нарисуван мечок отпред (стои прав).

    python tools/auto_clothes.py idle

Чете assets/bear/<anim>.png и записва <anim>_shirt.png, _pants.png, _shoes.png,
_gloves.png и _head.png. Частите се познават по редовете (виж BANDS) за мечока
от кадър 0; ако тялото в някой кадър е слязло надолу (дишане), редовете се
местят с него, а стъпалата остават. Ако Тут нарисува шаблоните сама, те са
по-точни: този скрипт е само за бързо начало.
"""

import os
import sys

from PIL import Image

BEAR = os.path.join(os.path.dirname(__file__), "..", "assets", "bear")
SIZE = 32

# Редове в кадър 0 (с двата края).
SHIRT = (17, 25)      # врат, рамене, гърди, ръкави
PANTS = (26, 28)
FEET = 29             # от този ред надолу са обувките (не дишат)
PAW_ROWS = (23, 25)   # лапите (ръкавиците) в колоните на ръцете
ARMS_LEFT, ARMS_RIGHT = 9, 22   # ръцете са вляво от x <= 9 и вдясно от x >= 22
HEAD = (16, 5)        # точката за шапката в кадър 0

OUTLINE = (88, 29, 8)
SHADOW = {(135, 69, 49), (174, 103, 61), (76, 47, 28)}
LIGHT = {(251, 232, 195), (243, 224, 185), (255, 255, 255)}


def tone(c):
    """Сивият тон на шаблона: 60 контур, 110 сянка, 160 цвят, 230 отблясък."""
    rgb = c[:3]
    if rgb == OUTLINE or rgb == (0, 0, 0):
        return 60
    if rgb in SHADOW:
        return 110
    if rgb in LIGHT:
        return 230
    return 160


def main(anim):
    strip = Image.open(os.path.join(BEAR, f"{anim}.png")).convert("RGBA")
    frames = strip.width // SIZE
    top0 = None
    layers = {k: Image.new("RGBA", strip.size, (0, 0, 0, 0)) for k in ("shirt", "pants", "shoes", "gloves", "head")}
    for f in range(frames):
        frame = strip.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE))
        top = frame.getbbox()[1]
        top0 = top if top0 is None else top0
        bob = top - top0
        for y in range(SIZE):
            for x in range(SIZE):
                c = frame.getpixel((x, y))
                if c[3] == 0:
                    continue
                y0 = y - bob
                if y >= FEET + bob:
                    part = "shoes"
                elif SHIRT[0] <= y0 <= SHIRT[1]:
                    arm = x <= ARMS_LEFT or x >= ARMS_RIGHT
                    part = "gloves" if arm and PAW_ROWS[0] <= y0 <= PAW_ROWS[1] else "shirt"
                elif PANTS[0] <= y0 <= PANTS[1]:
                    part = "pants"
                else:
                    continue
                g = tone(c)
                layers[part].putpixel((f * SIZE + x, y), (g, g, g, 255))
        layers["head"].putpixel((f * SIZE + HEAD[0], HEAD[1] + bob), (255, 0, 255, 255))
    for name, img in layers.items():
        img.save(os.path.join(BEAR, f"{anim}_{name}.png"))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "idle")
