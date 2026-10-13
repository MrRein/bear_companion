"""Преглед: всяка нарисувана анимация, облечена с всичко (за проверка на картите).

    python tools/clothes_preview.py            всички нарисувани
    python tools/clothes_preview.py idle eat   само тези

Записва tools/clothes_preview.png: блуза, панталон, обувки, ръкавици, шал и шапка
в ярки цветове, по 8 кадъра на ред. Ако нещо стои грешно, поправи картата в
assets/clothes/paint/<anim>.png и пусни tools/auto_clothes.py <anim>.
"""

import os
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..", "assets")
BEAR = os.path.join(ROOT, "bear")
LAYERS = os.path.join(ROOT, "clothes", "layers")
SIZE, HAT_ROOM = 32, 12
COLORS = {"shoes": (250, 200, 60), "pants": (70, 100, 170), "shirt": (214, 74, 70), "gloves": (120, 230, 120)}


def mix(c, d, t):
    return tuple(int(a + (b - a) * t) for a, b in zip(c, d))


def recolor(g, c):
    if g < 80:
        c = mix(c, (40, 25, 15), .6)
    elif g < 135:
        c = mix(c, (40, 25, 15), .25)
    elif g > 200:
        c = mix(c, (255, 255, 255), .3)
    return c + (255,)


def layer(anim, name, f):
    path = os.path.join(LAYERS, f"{anim}_{name}.png")
    if not os.path.exists(path):
        return None
    img = Image.open(path).convert("RGBA")
    if (f + 1) * SIZE > img.width:
        f = f % max(1, img.width // SIZE)
    return img.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE))


def point(img):
    if img is None:
        return None
    for y in range(SIZE):
        for x in range(SIZE):
            if img.getpixel((x, y))[3]:
                return x, y
    return None


def frame(anim, f, long_sleeves=True):
    base = Image.open(os.path.join(BEAR, f"{anim}.png")).convert("RGBA")
    out = Image.new("RGBA", (SIZE, SIZE + HAT_ROOM), (0, 0, 0, 0))
    out.alpha_composite(base.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE)), (0, HAT_ROOM))
    for name, color in COLORS.items():
        mask = layer(anim, name, f)
        if mask is None:
            continue
        if name == "shirt" and long_sleeves:
            sleeves = layer(anim, "sleeves", f)
            if sleeves is not None:
                mask = mask.copy()
                mask.alpha_composite(sleeves)
        for y in range(SIZE):
            for x in range(SIZE):
                p = mask.getpixel((x, y))
                if p[3]:
                    out.putpixel((x, y + HAT_ROOM), recolor(p[0], color))
    under = out.copy()
    neck = point(layer(anim, "neck", f))
    if neck:
        art = Image.open(os.path.join(ROOT, "clothes", "neck_scarf_stripes.png")).convert("RGBA")
        out.alpha_composite(art, (neck[0] - art.width // 2, neck[1] + HAT_ROOM))
    face = point(layer(anim, "face", f))
    if face:
        art = Image.open(os.path.join(ROOT, "clothes", "glasses_round.png")).convert("RGBA")
        out.alpha_composite(art, (face[0] - art.width // 2, face[1] + HAT_ROOM - art.height // 2))
    head = point(layer(anim, "head", f))
    if head:
        art = Image.open(os.path.join(ROOT, "clothes", "hat_beanie.png")).convert("RGBA")
        out.alpha_composite(art, (head[0] - art.width // 2, head[1] + HAT_ROOM - art.height + 1))
    # Ръцете отпред са над шала, очилата и шапката.
    front = layer(anim, "front", f)
    if front is not None:
        for y in range(SIZE):
            for x in range(SIZE):
                if front.getpixel((x, y))[3]:
                    out.putpixel((x, y + HAT_ROOM), under.getpixel((x, y + HAT_ROOM)))
    return out


def main(names, long_sleeves=True, out_name="clothes_preview.png"):
    cols = 8
    sheet = Image.new("RGBA", (cols * (SIZE + 2) + 70, len(names) * (SIZE + HAT_ROOM + 2)), (120, 160, 120, 255))
    for row, anim in enumerate(names):
        frames = Image.open(os.path.join(BEAR, f"{anim}.png")).width // SIZE
        for f in range(min(cols, frames)):
            sheet.alpha_composite(frame(anim, f, long_sleeves), (70 + f * (SIZE + 2), row * (SIZE + HAT_ROOM + 2)))
    big = sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST)
    try:
        from PIL import ImageDraw, ImageFont
        d = ImageDraw.Draw(big)
        font = ImageFont.load_default(size=24)
        for row, anim in enumerate(names):
            d.text((8, row * (SIZE + HAT_ROOM + 2) * 4 + 70), anim, fill=(255, 255, 255), font=font)
    except Exception:
        pass
    path = os.path.join(os.path.dirname(__file__), out_name)
    big.save(path)
    print(path)


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(__file__))
    import make_placeholders as mp
    names = sys.argv[1:] or sorted(mp.HAND_DRAWN | set(mp.EXTRA_DRAWN) | {"eat_hold"})
    main(names, True, "clothes_preview.png")            # дълъг ръкав
    main(names, False, "clothes_preview_short.png")     # къс ръкав
