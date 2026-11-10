"""Всички елементи на менюто в ЕДИН лист – за рисуване – и обратно на отделни файлове.

    python tools/menu_sheet.py export [комплект]           -> art_templates/menu_sheet.png (+ _map.png, .json)
    python tools/menu_sheet.py import <лист.png> [комплект] -> assets/ui/menu/<комплект>/*.png

Програмата ползва ОТДЕЛНИ файлове (assets/ui/menu/<комплект>/<елемент>.png). Листът е само
за удобство: export ги подрежда един до друг (с 2 празни пиксела помежду им), рисуваш върху
menu_sheet.png – всеки елемент в своето квадратче, в същия размер – и import го реже обратно
на отделните файлове по menu_sheet.json. Комплектът по подразбиране е default.

menu_sheet_map.png е същият лист, уголемен ×8, с името на всеки елемент; сините линии
делят 9-те части (ъглите остават, ръбовете и средата се повтарят).
"""

import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
MENU = os.path.join(ROOT, "assets", "ui", "menu")
TEMPLATES = os.path.join(ROOT, "art_templates")
THEMES = ["classic", "birch", "grass", "stone", "sakura", "night"]
WIDTH, GAP = 160, 2


def files(kit):
    """Всички файлове на комплекта по реда в menu.json (фонът – за всеки цвят)."""
    with open(os.path.join(MENU, kit, "menu.json"), encoding="utf-8") as fh:
        defs = json.load(fh)
    out = []
    for name, d in defs.items():
        if "file" in d:
            out += [(d["file"].replace("{theme}", t), d) for t in THEMES]
        else:
            out.append((name, d))
    return out


def export(kit="default"):
    items = []
    for name, d in files(kit):
        path = os.path.join(MENU, kit, name + ".png")
        if os.path.exists(path):
            items.append((name, d, Image.open(path).convert("RGBA")))
    # Подреждане на редове: първо фоновете (големите), после останалите.
    items.sort(key=lambda it: (not it[0].startswith("frame_"),))
    x = y = row = 0
    rects = {}
    for name, d, img in items:
        if x and x + img.width > WIDTH:
            x, y, row = 0, y + row + GAP, 0
        rects[name] = [x, y, img.width, img.height]
        x += img.width + GAP
        row = max(row, img.height)
    sheet = Image.new("RGBA", (WIDTH, y + row), (0, 0, 0, 0))
    for name, d, img in items:
        sheet.paste(img, tuple(rects[name][:2]))
    os.makedirs(TEMPLATES, exist_ok=True)
    sheet.save(os.path.join(TEMPLATES, "menu_sheet.png"))
    with open(os.path.join(TEMPLATES, "menu_sheet.json"), "w", encoding="utf-8") as fh:
        fh.write("{\n" + ",\n".join(f'  "{k}": {json.dumps(v)}' for k, v in rects.items()) + "\n}\n")
    sheet_map(sheet, rects, {name: d for name, d, _ in items})
    print("Лист:", sheet.size, len(rects), "елемента → art_templates/menu_sheet.png")


def sheet_map(sheet, rects, defs, s=8):
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 11)
    except OSError:
        font = ImageFont.load_default()
    big = Image.new("RGBA", (sheet.width * s, sheet.height * s), (235, 235, 240, 255))
    big.alpha_composite(sheet.resize(big.size, Image.NEAREST))
    d = ImageDraw.Draw(big)
    for name, (x, y, w, h) in rects.items():
        L, T, R, B = defs[name]["slice"]
        x0, y0 = x * s, y * s
        d.rectangle([x0, y0, x0 + w * s - 1, y0 + h * s - 1], outline=(220, 40, 120))
        for k in (L, w - R):
            if 0 < k < w:
                d.line([(x0 + k * s, y0), (x0 + k * s, y0 + h * s)], fill=(60, 120, 230))
        for k in (T, h - B):
            if 0 < k < h:
                d.line([(x0, y0 + k * s), (x0 + w * s, y0 + k * s)], fill=(60, 120, 230))
        d.text((x0 + 2, y0 + 2), name, fill=(220, 40, 120), font=font)
    big.save(os.path.join(TEMPLATES, "menu_sheet_map.png"))


def import_sheet(path, kit="default"):
    with open(os.path.join(TEMPLATES, "menu_sheet.json"), encoding="utf-8") as fh:
        rects = json.load(fh)
    sheet = Image.open(path).convert("RGBA")
    out = os.path.join(MENU, kit)
    os.makedirs(out, exist_ok=True)
    n = 0
    for name, (x, y, w, h) in rects.items():
        if x + w > sheet.width or y + h > sheet.height:
            print("  извън листа:", name)
            continue
        sheet.crop((x, y, x + w, y + h)).save(os.path.join(out, name + ".png"))
        n += 1
    if kit != "default" and not os.path.exists(os.path.join(out, "menu.json")):
        # Нов комплект: размерите на елементите са същите – взимаме описанието от default.
        with open(os.path.join(MENU, "default", "menu.json"), encoding="utf-8") as src, \
                open(os.path.join(out, "menu.json"), "w", encoding="utf-8") as dst:
            dst.write(src.read())
    print(f"Взех {n} елемента от {os.path.basename(path)} → assets/ui/menu/{kit}/")


if __name__ == "__main__":
    args = sys.argv[1:]
    if args[:1] == ["export"]:
        export(args[1] if len(args) > 1 else "default")
    elif args[:1] == ["import"] and len(args) > 1:
        import_sheet(args[1], args[2] if len(args) > 2 else "default")
    else:
        print(__doc__)
