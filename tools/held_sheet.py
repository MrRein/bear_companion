"""Лист с всичко, което мечето държи в лапите, както го рисува програмата.

    python tools/held_sheet.py        -> art_templates/held_items.png
    python tools/held_sheet.py eat    -> art_templates/held_items_eat.png (само тези анимации)

За всеки предмет: анимацията, в която го държи (до 4 кадъра), без дрехи. Предметът е
на синята точка от hold_sheet. Където анимацията е с item_below_arm (anim.json),
ръкавите и ръкавиците от картата на дрехите са над предмета. Четката, картите и т.н.
и чашата са в кадъра (пиксел за пиксел); храната, подаръкът и саксията са отделна
рисунка, по-малка или по-голяма (както в програмата).
"""

import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.join(os.path.dirname(__file__), "..")
BEAR = os.path.join(ROOT, "assets", "bear")
CLOTHES = os.path.join(ROOT, "assets", "clothes")
LAYERS = os.path.join(CLOTHES, "layers")
PROPS = os.path.join(ROOT, "assets", "props")
OUT = os.path.join(ROOT, "art_templates", "held_items.png")
SIZE, S = 32, 3                       # кадърът и колко пъти е уголемен
CELL = SIZE * S
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


def font(size):
    try:
        return ImageFont.truetype(FONT, size)
    except OSError:
        return ImageFont.load_default(size=size)


def layer(anim, name):
    path = os.path.join(LAYERS, f"{anim}_{name}.png")
    return Image.open(path).convert("RGBA") if os.path.exists(path) else None


def frame(img, i):
    return img.crop((i * SIZE, 0, i * SIZE + SIZE, SIZE))


def point(lay, i):
    if lay is None or (i + 1) * SIZE > lay.width:
        return None
    f = frame(lay, i)
    return next(((x, y) for y in range(SIZE) for x in range(SIZE) if f.getpixel((x, y))[3]), None)


def arms_mask(anim, i):
    mask = set()
    for name in ("sleeves", "gloves"):
        lay = layer(anim, name)
        if lay is None or (i + 1) * SIZE > lay.width:
            continue
        f = frame(lay, i)
        mask |= {(x, y) for y in range(SIZE) for x in range(SIZE) if f.getpixel((x, y))[3]}
    return mask


# (заглавие, анимация, слой с точката, рисунка, как се рисува, мащаб)
def items():
    out = []
    tools = sorted(f[5:-4] for f in os.listdir(CLOTHES) if f.startswith("tool_") and f.endswith(".png"))
    for t in tools:
        anim = "doing" if t in ("wateringcan", "shovel", "scissors") else "doing_sit"
        out.append((t, anim, "tool", os.path.join(CLOTHES, f"tool_{t}.png"), "tool", 1))
    for d in sorted(f[6:-4] for f in os.listdir(CLOTHES) if f.startswith("drink_") and f.endswith(".png")):
        for anim in ("sit_drinking", "sit_drinking_sip", "sit_drinking_blow"):
            out.append((d, anim, "cup", os.path.join(CLOTHES, f"drink_{d}.png"), "cup", 1))
    foods = sorted(f[:-4] for f in os.listdir(PROPS) if f.startswith("food_") and f.endswith(".png") and not f.endswith("_menu.png"))
    for f in foods:
        for anim in ("eat", "eat_hold"):
            out.append((f[5:], anim, "cup", os.path.join(PROPS, f + ".png"), "over", 0.8))
    gifts = sorted(f[:-4] for f in os.listdir(PROPS) if f.startswith("gift") and f.endswith(".png") and "_open" not in f)
    for g in gifts:
        for anim in ("hold", "hold_walk"):
            out.append((g, anim, "tool", os.path.join(PROPS, g + ".png"), "over", 0.75))
    for kind in ("sunflower", "monstera", "cactus"):
        path = os.path.join(PROPS, f"plant_{kind}_9.png")
        if os.path.exists(path):
            out.append((f"саксия {kind}", "hold_walk", "tool", path, "plant", 1))
    return out


def render(title, anim, point_layer, art_path, how, scale, below):
    strip = Image.open(os.path.join(BEAR, f"{anim}.png")).convert("RGBA")
    art = Image.open(art_path).convert("RGBA")
    lay = layer(anim, point_layer)
    n = strip.width // SIZE
    picks = sorted({round(k * (n - 1) / 3) for k in range(4)}) if n > 4 else list(range(n))
    cells = []
    for i in picks:
        bear = frame(strip, i)
        p = point(lay, i)
        mask = arms_mask(anim, i) if below else set()
        big = Image.new("RGBA", (CELL, CELL), (255, 255, 255, 255))
        if p is None:
            big.alpha_composite(bear.resize((CELL, CELL), Image.NEAREST))
            ImageDraw.Draw(big).text((3, 2), "няма точка", fill=(200, 0, 0), font=font(11))
            cells.append(big)
            continue
        px, py = p
        if how in ("tool", "cup"):
            # В кадъра: точката е средата (предмет) или дъното (чаша).
            item = Image.new("RGBA", (SIZE, SIZE))
            top = py - art.height // 2 if how == "tool" else py - art.height + 1
            item.paste(art, (px - art.width // 2, top), art)
            done = bear.copy()
            done.alpha_composite(item)
            for x, y in mask:
                done.putpixel((x, y), bear.getpixel((x, y)))
            big.alpha_composite(done.resize((CELL, CELL), Image.NEAREST))
        else:
            # Отделна рисунка (храна, подарък, саксия) над мечока, после ръцете отгоре.
            big.alpha_composite(bear.resize((CELL, CELL), Image.NEAREST))
            w, h = max(1, round(art.width * S * scale)), max(1, round(art.height * S * scale))
            cx, cy = (px + 0.5) * S, (py + 0.5) * S
            top = cy - h / 2
            if how == "plant":
                top = min(top, CELL - h)
            # Може да излиза извън клетката (саксията е по-висока от мечока).
            scaled = art.resize((w, h), Image.NEAREST)
            layer_img = Image.new("RGBA", (CELL, CELL))
            layer_img.paste(scaled, (int(cx - w / 2), int(top)), scaled)
            big.alpha_composite(layer_img)
            for x, y in mask:
                c = bear.getpixel((x, y))
                big.paste(Image.new("RGBA", (S, S), c), (x * S, y * S))
        cells.append(big)
    return cells


def main(only=()):
    with open(os.path.join(BEAR, "anim.json"), encoding="utf-8") as fh:
        data = json.load(fh)
    anims = data.get("animations", data)
    rows = [r for r in items() if not only or r[1].startswith(tuple(only))]
    label_w, gap, per_row = 170, 8, 3
    block_w = label_w + 4 * (CELL + 2) + gap * 2
    row_h = CELL + 22
    lines = (len(rows) + per_row - 1) // per_row
    sheet = Image.new("RGBA", (per_row * block_w, 40 + lines * row_h), (245, 240, 228, 255))
    d = ImageDraw.Draw(sheet)
    d.text((8, 8), "Всичко, което мечето държи (без дрехи; ръкави/ръкавици над предмета, където е item_below_arm)",
           fill=(40, 40, 40), font=font(16))
    for k, (title, anim, pl, path, how, scale) in enumerate(rows):
        below = bool(anims.get(anim, {}).get("item_below_arm"))
        x0 = (k % per_row) * block_w + gap
        y0 = 40 + (k // per_row) * row_h
        d.text((x0, y0 + CELL // 2 - 16), title, fill=(40, 40, 40), font=font(14))
        d.text((x0, y0 + CELL // 2 + 2), anim + (" · под ръцете" if below else ""), fill=(110, 110, 110), font=font(11))
        for j, cell in enumerate(render(title, anim, pl, path, how, scale, below)):
            sheet.paste(cell, (x0 + label_w + j * (CELL + 2), y0))
    out = OUT if not only else OUT.replace(".png", "_" + "_".join(only) + ".png")
    sheet.save(out)
    print(os.path.relpath(out, ROOT), sheet.size, len(rows), "предмета")


if __name__ == "__main__":
    main(sys.argv[1:])
