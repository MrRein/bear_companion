"""Картинки за МЕНЮТО на храната, която излиза на парчета.

Парчето (assets/props/food/<id>.png) си остава за екрана; тук се рисува цялото
ястие (купичка боровинки, кофа пуканки, цяла пица…) в
assets/props/food/<id>_menu.png, 16x16, със същите цветове и черен контур.
Пише и преглед art_templates/food_menu_sheet.png (парче | меню, 4x).

    python tools/make_food_menu.py
"""

import os

from PIL import Image, ImageDraw, ImageFont

import props_layout as pl  # къде стои всяка рисунка – по папки

ROOT = os.path.join(os.path.dirname(__file__), "..")
PROPS = os.path.join(ROOT, "assets", "props")
SHEET = os.path.join(ROOT, "art_templates", "food_menu_sheet.png")

# Цветовете са същите като в tools/make_placeholders.py.
OUTLINE = (68, 20, 8, 255)
WHITE = (255, 255, 255, 255)
PLATE = (240, 240, 245, 255)
RED = (220, 60, 60, 255)
WOOD = (90, 55, 30, 255)
GREY = (150, 150, 160, 255)
STEAM = (220, 220, 220, 255)
HONEY = (240, 170, 40, 255)
GREEN = (110, 180, 80, 255)
DARK_GREEN = (70, 120, 50, 255)
BLUE = (70, 90, 190, 255)
BLUE_HI = (170, 190, 255, 255)
POP = (255, 245, 210, 255)
POP_EDGE = (205, 170, 95, 255)
MEAT = (130, 70, 40, 255)
MEAT_HI = (175, 105, 65, 255)
GOLD = (235, 185, 75, 255)
CRUST = (175, 115, 40, 255)
CORN = (250, 210, 60, 255)
CORN_DOT = (220, 170, 30, 255)
YOLK = (250, 190, 40, 255)
PANCAKE = (230, 180, 100, 255)
COOKIE = (210, 160, 90, 255)
CHIP = (90, 50, 30, 255)
LINER = (230, 120, 140, 255)
MUFFIN = (220, 170, 100, 255)
CHEESE = (250, 210, 90, 255)
PIZZA_CRUST = (200, 140, 70, 255)
WAFFLE = (230, 180, 90, 255)
WAFFLE_LINE = (190, 130, 60, 255)
CHOCO = (100, 55, 30, 255)
CREAM = (250, 235, 200, 255)
TOAST = (220, 160, 90, 255)
TOAST_LINE = (120, 70, 40, 255)


def oval(d, box, fill, outline=OUTLINE):
    d.ellipse(box, fill=fill, outline=outline)


def img16(draw):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    draw(ImageDraw.Draw(img))
    return img


def plate(d, top=10):
    oval(d, (0, top, 15, 15), PLATE)


def berries(d):
    oval(d, (1, 7, 14, 15), PLATE)                     # купичка (отдолу)
    for x, y in [(1, 5), (4, 6), (8, 6), (11, 5), (3, 3), (6, 2), (9, 3), (6, 5)]:
        oval(d, (x, y, x + 4, y + 4), BLUE)
        d.point((x + 1, y + 1), fill=BLUE_HI)
    d.polygon([(1, 9), (14, 9), (12, 14), (3, 14)], fill=PLATE, outline=OUTLINE)   # предната стена
    d.line((5, 15, 10, 15), fill=OUTLINE)
    d.line((3, 10, 4, 12), fill=WHITE)
    d.line((8, 0, 8, 2), fill=DARK_GREEN)              # листенце
    d.ellipse((9, 0, 12, 2), fill=(90, 170, 70, 255))


def popcorn(d):
    blobs = [(0, 4, 4, 8), (11, 4, 15, 8), (2, 1, 6, 5), (6, 0, 10, 4), (9, 1, 13, 5),
             (4, 3, 8, 7), (8, 3, 12, 7), (2, 5, 6, 8), (9, 5, 13, 8), (5, 5, 10, 8)]
    for b in blobs:
        d.ellipse(b, fill=POP, outline=POP_EDGE)
    for b in blobs:
        d.ellipse((b[0] + 1, b[1] + 1, b[2] - 1, b[3] - 1), fill=POP)
    for x, y in [(4, 3), (11, 6), (7, 2), (2, 6), (9, 5)]:
        d.point((x, y), fill=(230, 180, 90, 255))
    for x, y in [(6, 4), (9, 3), (4, 6), (12, 4), (7, 6), (5, 1)]:   # ръбчета между пуканките
        d.point((x, y), fill=POP_EDGE)
    d.polygon([(1, 8), (14, 8), (12, 15), (3, 15)], fill=RED, outline=OUTLINE)
    for x in (4, 7, 10):                               # бели ивици
        d.line((x, 9, x, 14), fill=WHITE)
        d.line((x + 1, 9, x + 1, 14), fill=WHITE)
    d.line((2, 9, 13, 9), fill=(255, 255, 255, 255))


def meatballs(d):
    plate(d, 9)
    for box in [(0, 6, 6, 12), (9, 6, 15, 12), (4, 2, 10, 8)]:
        oval(d, box, MEAT)
        d.point((box[0] + 2, box[1] + 2), fill=MEAT_HI)
        d.point((box[0] + 3, box[1] + 2), fill=MEAT_HI)
    d.point((7, 5), fill=(90, 140, 60, 255))           # магданоз
    d.point((12, 9), fill=(90, 140, 60, 255))
    d.point((3, 9), fill=(90, 140, 60, 255))
    d.line((3, 0, 3, 2), fill=STEAM)
    d.line((12, 1, 12, 3), fill=STEAM)


def potatoes(d):
    wedges = [[(1, 10), (2, 4), (6, 10)], [(4, 10), (7, 2), (9, 10)], [(8, 10), (12, 3), (13, 10)],
              [(10, 10), (14, 6), (15, 10)], [(5, 8), (3, 6), (9, 5)]]
    for w in wedges:
        d.polygon(w, fill=GOLD, outline=CRUST)
    for x, y in [(3, 7), (7, 5), (11, 6), (6, 6)]:
        d.point((x, y), fill=(255, 225, 140, 255))
    # плетена кошничка с карирана салфетка
    d.polygon([(0, 9), (15, 9), (13, 15), (2, 15)], fill=(200, 145, 80, 255), outline=OUTLINE)
    for x in (4, 7, 10):
        d.line((x, 10, x, 14), fill=(150, 95, 45, 255))
    d.line((2, 12, 13, 12), fill=(150, 95, 45, 255))
    for x in range(1, 15, 2):
        d.point((x, 9), fill=RED)


def corn_cob(d, x, y):
    oval(d, (x, y, x + 10, y + 5), CORN)
    for kx in range(x + 2, x + 10, 2):
        d.point((kx, y + 2), fill=CORN_DOT)
        d.point((kx + 1, y + 3), fill=CORN_DOT)
    d.polygon([(x - 1, y), (x + 3, y + 2), (x - 1, y + 5)], fill=GREEN, outline=OUTLINE)   # листата


def corn(d):
    plate(d, 9)
    corn_cob(d, 4, 2)
    corn_cob(d, 2, 7)
    d.point((12, 7), fill=(250, 240, 170, 255))        # масълце
    d.point((13, 7), fill=(250, 240, 170, 255))


def eggs(d):
    d.rectangle((12, 9, 15, 10), fill=WOOD, outline=OUTLINE)    # дръжка
    oval(d, (0, 4, 13, 15), (70, 70, 80, 255))                  # тиган
    oval(d, (1, 5, 12, 14), (110, 110, 120, 255), outline=(50, 50, 60, 255))
    for x, y in [(2, 6), (6, 8)]:
        oval(d, (x, y, x + 5, y + 5), WHITE, outline=(215, 215, 215, 255))
        d.rectangle((x + 2, y + 2, x + 3, y + 3), fill=YOLK)
        d.point((x + 2, y + 2), fill=(255, 230, 140, 255))
    d.line((3, 0, 3, 2), fill=STEAM)
    d.line((9, 1, 9, 3), fill=STEAM)


def pancakes(d):
    plate(d, 11)
    for i in range(5):
        oval(d, (2, 10 - i * 2, 13, 13 - i * 2), PANCAKE)
    d.ellipse((3, 1, 12, 4), fill=HONEY)                        # мед отгоре
    d.line((3, 3, 3, 7), fill=HONEY)                            # капе по краищата
    d.point((3, 8), fill=HONEY)
    d.line((12, 3, 12, 10), fill=HONEY)
    d.line((8, 4, 8, 5), fill=HONEY)
    d.rectangle((7, 1, 8, 2), fill=(250, 240, 170, 255))        # масълце


def cookies(d):
    plate(d, 9)
    for x, y in [(0, 6), (10, 6), (5, 8), (5, 2)]:
        oval(d, (x, y, x + 5, y + 5), COOKIE)
        d.point((x + 2, y + 2), fill=CHIP)
        d.point((x + 3, y + 4), fill=CHIP)


def muffins(d):
    for x, top in [(0, 4), (10, 4), (5, 3)]:
        d.polygon([(x, top + 5), (x + 5, top + 5), (x + 4, 12), (x + 1, 12)], fill=LINER, outline=OUTLINE)
        d.line((x + 2, top + 6, x + 2, 11), fill=(200, 90, 115, 255))
        oval(d, (x, top, x + 5, top + 6), MUFFIN)
        d.point((x + 2, top + 2), fill=BLUE)
        d.point((x + 4, top + 3), fill=BLUE)
        d.point((x + 1, top + 4), fill=BLUE)
    d.rectangle((0, 12, 15, 15), fill=GREY, outline=OUTLINE)   # тавичка
    d.line((1, 13, 14, 13), fill=(185, 185, 195, 255))


def pizza(d):
    oval(d, (0, 0, 15, 15), PIZZA_CRUST)
    d.ellipse((2, 2, 13, 13), fill=CHEESE)
    cut = (215, 160, 70, 255)
    d.line((7, 1, 7, 14), fill=cut)
    d.line((1, 7, 14, 7), fill=cut)
    d.line((3, 3, 12, 12), fill=cut)
    d.line((12, 3, 3, 12), fill=cut)
    for x, y in [(4, 1), (9, 2), (1, 5), (11, 5), (2, 9), (11, 9), (5, 11), (9, 11)]:
        d.ellipse((x, y, x + 2, y + 2), fill=RED)


def waffle(d, x, y):
    d.rectangle((x, y, x + 7, y + 7), fill=WAFFLE, outline=OUTLINE)
    for gx in (x + 2, x + 5):
        d.line((gx, y + 1, gx, y + 6), fill=WAFFLE_LINE)
    for gy in (y + 2, y + 5):
        d.line((x + 1, gy, x + 6, gy), fill=WAFFLE_LINE)


def waffles(top):
    def f(d):
        plate(d, 10)
        waffle(d, 1, 6)
        waffle(d, 7, 3)
        if top == "honey":
            d.line((8, 4, 13, 4), fill=HONEY)
            d.line((2, 7, 7, 7), fill=HONEY)
            d.line((13, 4, 13, 7), fill=HONEY)
            d.line((7, 7, 7, 10), fill=HONEY)
            d.point((10, 5), fill=HONEY)
        elif top == "choco":
            d.rectangle((8, 4, 13, 6), fill=CHOCO)
            d.rectangle((2, 7, 7, 9), fill=CHOCO)
            d.point((12, 7), fill=CHOCO)
            d.point((3, 10), fill=CHOCO)
        else:
            oval(d, (4, 0, 11, 7), CREAM)
            d.point((7, 2), fill=HONEY)
            d.point((6, 3), fill=(255, 250, 235, 255))
            d.point((9, 4), fill=HONEY)
    return f


def skewer(d, y, x0, chunks):
    d.line((x0, y, x0 + 13, y), fill=WOOD)
    for i, c in enumerate(chunks):
        x = x0 + 3 + i * 3
        oval(d, (x - 2, y - 2, x + 1, y + 2), c)


def skewers(d):
    plate(d, 9)
    meat, meat2, pepper = (150, 80, 50, 255), (170, 90, 60, 255), GREEN
    skewer(d, 3, 2, [meat, pepper, meat2, RED])
    skewer(d, 7, 0, [meat2, RED, meat, pepper])
    skewer(d, 11, 2, [meat, (250, 200, 60, 255), meat2, pepper])


def veggies(d):
    plate(d, 8)
    oval(d, (0, 6, 5, 11), GREEN)                      # тиквичка
    d.point((2, 8), fill=(200, 230, 160, 255))
    oval(d, (10, 7, 15, 12), GREEN)
    d.point((12, 9), fill=(200, 230, 160, 255))
    oval(d, (3, 3, 8, 9), RED)                         # червена чушка
    oval(d, (8, 2, 13, 8), (250, 200, 60, 255))        # жълта чушка
    oval(d, (5, 8, 10, 13), (240, 140, 50, 255))       # морков/тиква
    grill = (90, 45, 25, 255)
    for x, y in [(4, 5), (5, 6), (9, 4), (10, 5), (6, 10), (7, 11), (1, 8), (11, 9)]:
        d.point((x, y), fill=grill)
    d.line((6, 2, 6, 3), fill=DARK_GREEN)              # дръжчица


def grilled_bread(d):
    plate(d, 10)
    for x, y in [(1, 6), (8, 3)]:
        d.rectangle((x, y, x + 6, y + 6), fill=TOAST, outline=OUTLINE)
        d.line((x + 1, y + 2, x + 5, y + 2), fill=TOAST_LINE)
        d.line((x + 1, y + 4, x + 5, y + 4), fill=TOAST_LINE)
        d.line((x + 1, y + 1, x + 5, y + 1), fill=HONEY)
        d.point((x + 3, y + 3), fill=HONEY)
        d.point((x + 5, y + 3), fill=HONEY)
    d.line((14, 4, 14, 8), fill=HONEY)                 # мед капе
    d.line((7, 7, 7, 11), fill=HONEY)


MENU = {
    "berries": berries,
    "popcorn": popcorn,
    "meatballs": meatballs,
    "potatoes": potatoes,
    "corn": corn,
    "eggs": eggs,
    "pancakes": pancakes,
    "cookies": cookies,
    "muffins": muffins,
    "pizza": pizza,
    "waffle_honey": waffles("honey"),
    "waffle_choco": waffles("choco"),
    "waffle_icecream": waffles("icecream"),
    "skewers": skewers,
    "veggies": veggies,
    "grilled_bread": grilled_bread,
}


def preview(images):
    z, cols = 4, 4
    cell_w, cell_h = 16 * z * 2 + 3 * 8, 16 * z + 22
    rows = (len(images) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell_w, rows * cell_h), (205, 225, 205, 255))
    d = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()
    for i, (fid, menu) in enumerate(images.items()):
        cx, cy = (i % cols) * cell_w + 8, (i // cols) * cell_h + 4
        piece = Image.open(pl.path(f"food_{fid}")).convert("RGBA")
        box = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        box.alpha_composite(piece, ((16 - piece.width) // 2, (16 - piece.height) // 2))
        for j, im in enumerate((box, menu)):
            x = cx + j * (16 * z + 8)
            d.rectangle((x - 1, cy - 1, x + 16 * z, cy + 16 * z), outline=(170, 190, 170, 255))
            sheet.alpha_composite(im.resize((16 * z, 16 * z), Image.NEAREST), (x, cy))
        d.text((cx, cy + 16 * z + 4), fid, fill=(40, 40, 40, 255), font=font)
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    sheet.save(SHEET)


def main():
    images = {}
    for fid, draw in MENU.items():
        img = img16(draw)
        img.save(pl.path(f"food_{fid}_menu"))
        images[fid] = img
    preview(images)


if __name__ == "__main__":
    main()
