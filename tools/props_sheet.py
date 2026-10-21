"""Лист с мебелите, уредите и скиновете им: какво има и как се казва файлът.

    python tools/props_sheet.py      прави art_templates/props_sheet.png

Всеки ред е един предмет: класическият и скиновете му, с всички кадри
(уредите: "", _b, _open; печицата: "", _b, _off). Под всяка рисунка пише името
на файла и размерът. Тут рисува файла със същото име и размер и го качва в
done_art/ – той заменя временната рисунка (виж README → „Мебели и скинове“).
"""

import os

from PIL import Image, ImageDraw, ImageFont

import props_layout as pl  # къде стои всяка рисунка – по папки

ROOT = os.path.join(os.path.dirname(__file__), "..")
PROPS = os.path.join(ROOT, "assets", "props")
CLOTHES = os.path.join(ROOT, "assets", "clothes")
OUT = os.path.join(ROOT, "art_templates", "props_sheet.png")
ZOOM = 3

# Предмет → (подпис, база на файла, скинове, кадри, папка)
COOKER_FRAMES = ["", "_b", "_open"]
ROWS = [
    ("Диван", "couch", ["", "_velvet", "_forest", "_dots", "_mint", "_honey", "_leather"], [""], PROPS),
    ("Легло", "bed", ["", "_pink", "_forest", "_honey", "_stars"], [""], PROPS),
    ("Печица", "heater", ["", "_fireplace", "_tiles", "_retro", "_cozy"], ["", "_b", "_off"], PROPS),
    ("Лампа (свети)", "lamp", ["", "_mushroom", "_lantern", "_moon", "_lava", "_candle"], [""], PROPS),
    ("Кошница с прежда", "yarn_basket", [""], [""], PROPS),
    ("Китара (в лапите)", "tool_guitar", ["", "_electric", "_ukulele", "_blue", "_star"], [""], CLOTHES),
    ("Куки (в лапите)", "tool_knitting", [""], [""], CLOTHES),
    ("Тенджера", "cook_pot", ["", "_enamel", "_copper"], COOKER_FRAMES, PROPS),
    ("Тиган", "cook_pan", ["", "_cast_iron", "_pink"], COOKER_FRAMES, PROPS),
    ("Фурна", "cook_oven", ["", "_retro", "_wood"], COOKER_FRAMES, PROPS),
    ("Кафе машина", "cook_coffee_machine", ["", "_moka", "_retro"], COOKER_FRAMES, PROPS),
    ("Чайник", "cook_kettle", ["", "_whistle", "_glass"], COOKER_FRAMES, PROPS),
    ("Миксер", "cook_mixer", ["", "_mint", "_hand"], COOKER_FRAMES, PROPS),
    ("Сандвичарник", "cook_sandwich", ["", "_red", "_waffle"], COOKER_FRAMES, PROPS),
    ("Сладолед", "cook_icecream", ["", "_cart", "_pink"], COOKER_FRAMES, PROPS),
    ("Скара", "cook_grill", ["", "_kettle", "_camp"], COOKER_FRAMES, PROPS),
]


def font(size):
    try:
        return ImageFont.truetype("DejaVuSans.ttf", size)
    except OSError:
        return ImageFont.load_default(size=size)


def main():
    title, small = font(14), font(9)
    cells = []   # (ред, рисунка, име)
    for r, (label, base, skins, frames, folder) in enumerate(ROWS):
        for skin in skins:
            for frame in frames:
                name = base + skin + frame
                path = pl.path(name) if folder == PROPS else os.path.join(folder, name + ".png")
                if os.path.exists(path):
                    cells.append((r, Image.open(path).convert("RGBA"), name))
    gap, label_w = 10, 150
    row_h = {r: max(img.height for rr, img, _ in cells if rr == r) * ZOOM + 34 for r in range(len(ROWS))}
    widths = {}
    for r, img, name in cells:
        widths[r] = widths.get(r, 0) + max(img.width * ZOOM, small.getlength(name) + 4, 40) + gap
    width = label_w + max(widths.values()) + 10
    height = sum(row_h.values()) + 10
    sheet = Image.new("RGBA", (int(width), height), (235, 235, 235, 255))
    d = ImageDraw.Draw(sheet)
    y = 6
    for r, (label, *_rest) in enumerate(ROWS):
        d.text((8, y + 8), label, fill=(40, 40, 40), font=title)
        x = label_w
        for rr, img, name in cells:
            if rr != r:
                continue
            big = img.resize((img.width * ZOOM, img.height * ZOOM), Image.NEAREST)
            sheet.paste(Image.new("RGBA", big.size, (255, 255, 255, 255)), (int(x), y))
            sheet.alpha_composite(big, (int(x), y))
            d.text((x, y + big.height + 2), name, fill=(40, 40, 40), font=small)
            d.text((x, y + big.height + 13), f"{img.width}×{img.height}", fill=(120, 120, 120), font=small)
            x += max(big.width, small.getlength(name) + 4, 40) + gap
        y += row_h[r]
        d.line((0, y - 4, width, y - 4), fill=(200, 200, 200))
    sheet.save(OUT)
    print("art_templates/props_sheet.png")


if __name__ == "__main__":
    main()
