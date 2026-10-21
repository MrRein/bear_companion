"""Рисува новите предмети: скинове на радиото, картини за статива, растения
(3 вида × 10 стадия), градински инструменти, хранилки за птици, птици,
скинове на бюрото и столчето и иконки за магазина.

    python tools/make_items.py

Записва в assets/props/ и assets/clothes/ и прави преглед
art_templates/items_sheet.png (всичко ×3 с имената на файловете).
"""

import math
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(__file__))
import make_placeholders as mp  # noqa: E402
import props_layout as pl  # noqa: E402  (къде стои всяка рисунка – по папки)

ROOT = os.path.join(os.path.dirname(__file__), "..")
PROPS = os.path.join(ROOT, "assets", "props")
CLOTHES = os.path.join(ROOT, "assets", "clothes")
SHEET = os.path.join(ROOT, "art_templates", "items_sheet.png")
ZOOM = 3

O = mp.OUTLINE
WOOD = mp.WOOD
WHITE = mp.WHITE
BLACK = mp.BLACK
WOOD_MID = (130, 85, 50, 255)
WOOD_LIGHT = (175, 120, 70, 255)
OAK = (205, 160, 100, 255)
OAK_DARK = (170, 125, 75, 255)
YELLOW = (255, 209, 102, 255)
GOLD = (230, 180, 70, 255)
GOLD_DARK = (170, 115, 40, 255)
GREY = (150, 150, 160, 255)
LGREY = (200, 200, 210, 255)
DGREY = (70, 70, 80, 255)
STEEL = (190, 195, 205, 255)
RED = (210, 60, 55, 255)
RED_DARK = (150, 40, 40, 255)
PINK = (240, 150, 175, 255)
PINK_DARK = (205, 105, 135, 255)
SKY = (170, 210, 235, 255)
BLUE = (90, 140, 210, 255)
NAVY = (35, 45, 90, 255)
LEAF = (120, 180, 80, 255)
LEAF_DARK = (70, 130, 60, 255)
LEAF_LIGHT = (160, 210, 110, 255)
SOIL = (85, 55, 35, 255)
CLAY = (200, 110, 70, 255)
CLAY_LIGHT = (225, 145, 100, 255)
CLAY_DARK = (165, 80, 50, 255)
CREAM = (250, 235, 205, 255)
SEED = (225, 200, 140, 255)
SEED_DARK = (60, 45, 35, 255)
SEED_MID = (175, 130, 80, 255)

OUT = {}   # име на файла → (папка, картинка); пазим реда за прегледа


def new(w, h):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)


def save(name, img, folder=PROPS, group=""):
    img.save(pl.path(name) if folder == PROPS else os.path.join(folder, name + ".png"))
    OUT[name] = (folder, img, group)


def box(d, b, fill, outline=O):
    d.rectangle(b, fill=fill, outline=outline)


def pix(img, pts, color):
    px = img.load()
    for x, y in pts:
        if 0 <= x < img.width and 0 <= y < img.height:
            px[x, y] = color


def seeds(img, x0, y0, x1, y1, step=0):
    """Разпилени семки: светли, кафяви и черни точки в правоъгълника."""
    px = img.load()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            h = (x * 37 + y * 91 + step * 17 + x * y * 7) % 11
            px[x, y] = SEED_DARK if h in (0, 5) else SEED_MID if h in (2, 8) else SEED


# ---------------------------------------------------------------- радио


def radio_gramophone():
    img, d = new(18, 22)
    # фуния: от гърлото долу вляво към голям раструб горе вдясно
    d.polygon([(4, 14), (7, 14), (16, 7), (10, 1)], fill=GOLD, outline=O)
    d.line((6, 12, 12, 4), fill=(250, 215, 120, 255))
    d.ellipse((8, 0, 17, 10), fill=GOLD, outline=O)
    d.ellipse((10, 2, 15, 8), fill=GOLD_DARK)
    d.ellipse((12, 4, 14, 6), fill=(110, 70, 30, 255))
    # плоча и рамо
    d.rectangle((2, 13, 15, 14), fill=(40, 40, 45, 255), outline=O)
    d.point((9, 13), fill=RED)
    box(d, (3, 12, 6, 14), GOLD)
    # дървена кутия
    box(d, (1, 15, 16, 21), WOOD_MID)
    d.line((2, 16, 15, 16), fill=WOOD_LIGHT)
    box(d, (4, 17, 13, 20), WOOD_LIGHT)
    d.line((17, 17, 17, 19), fill=O)   # манивела
    d.point((16, 19), fill=O)
    return img


def radio_boombox():
    img, d = new(20, 12)
    d.rectangle((4, 0, 15, 0), fill=O)               # дръжка
    d.line((4, 0, 4, 3), fill=O)
    d.line((15, 0, 15, 3), fill=O)
    d.line((5, 1, 14, 1), fill=GREY)
    box(d, (0, 3, 19, 11), DGREY)
    d.line((1, 4, 18, 4), fill=GREY)
    for cx in (3, 16):
        d.ellipse((cx - 3, 5, cx + 3, 10), fill=(40, 40, 48, 255), outline=O)
        d.rectangle((cx - 1, 7, cx, 8), fill=GREY)
    box(d, (7, 6, 12, 10), LGREY)                     # касета
    d.rectangle((8, 7, 11, 8), fill=(60, 60, 70, 255))
    d.point((8, 8), fill=WHITE)
    d.point((11, 8), fill=WHITE)
    d.point((7, 4), fill=RED)
    d.point((9, 4), fill=YELLOW)
    d.point((11, 4), fill=(120, 200, 120, 255))
    return img


def radio_retro():
    img, d = new(16, 14)
    box(d, (0, 4, 15, 13), WOOD_MID)
    d.ellipse((0, 0, 15, 9), fill=WOOD_MID, outline=O)
    d.rectangle((1, 4, 14, 8), fill=WOOD_MID)
    d.ellipse((3, 2, 12, 11), fill=(225, 205, 160, 255), outline=O)   # плат
    d.rectangle((3, 7, 12, 8), fill=WOOD_MID)
    d.line((3, 7, 12, 7), fill=O)
    for x in (5, 7, 8, 10):
        d.line((x, 3, x, 6), fill=(190, 160, 110, 255))
    box(d, (2, 9, 9, 11), YELLOW)                       # скала
    d.point((5, 10), fill=RED)
    for x in (11, 13):
        d.rectangle((x, 9, x + 1, 10), fill=(60, 40, 25, 255))
    d.line((1, 12, 14, 12), fill=WOOD_LIGHT)
    return img


def radio_jukebox():
    img, d = new(16, 24)
    box(d, (0, 6, 15, 22), (180, 60, 60, 255))
    d.ellipse((0, 0, 15, 12), fill=(180, 60, 60, 255), outline=O)
    d.rectangle((1, 6, 14, 11), fill=(180, 60, 60, 255))
    # неонова дъга
    for i, c in enumerate([(255, 140, 60, 255), YELLOW, (120, 220, 200, 255)]):
        d.arc((1 + i, 1 + i, 14 - i, 13 - i), 180, 360, fill=c)
    d.line((1 + 0, 7, 1, 13), fill=(255, 140, 60, 255))
    d.line((14, 7, 14, 13), fill=(255, 140, 60, 255))
    box(d, (3, 5, 12, 11), (40, 40, 60, 255))           # прозорец с плочи
    d.ellipse((4, 7, 7, 10), fill=BLACK)
    d.ellipse((8, 7, 11, 10), fill=BLACK)
    d.point((5, 8), fill=RED)
    d.point((9, 8), fill=YELLOW)
    box(d, (3, 13, 12, 19), GOLD)                       # решетка
    for y in (15, 17):
        d.line((4, y, 11, y), fill=GOLD_DARK)
    d.point((7, 12), fill=WHITE)
    d.point((8, 12), fill=WHITE)
    box(d, (1, 21, 14, 23), DGREY)
    return img


# ---------------------------------------------------------------- картини за статива

CANVAS = (2, 4)   # отгоре вляво на вътрешността на платното, 14×13


def painting_mountains(c, d):
    d.rectangle((0, 0, 13, 12), fill=SKY)
    d.ellipse((9, 1, 12, 4), fill=YELLOW)
    d.polygon([(0, 10), (4, 3), (9, 10)], fill=(130, 120, 160, 255))
    d.polygon([(5, 10), (9, 5), (13, 10)], fill=(110, 105, 145, 255))
    d.polygon([(3, 5), (4, 3), (5, 5)], fill=WHITE)
    d.polygon([(8, 6), (9, 5), (10, 6)], fill=WHITE)
    d.rectangle((0, 10, 13, 12), fill=LEAF)


def painting_night(c, d):
    d.rectangle((0, 0, 13, 12), fill=NAVY)
    d.ellipse((2, 2, 7, 7), fill=YELLOW)
    d.ellipse((4, 1, 8, 5), fill=NAVY)
    for x, y in [(10, 2), (12, 6), (8, 9), (3, 10), (11, 11), (6, 6), (13, 1)]:
        d.point((x, y), fill=WHITE)
    for x, y in [(10, 5), (2, 8)]:
        d.point((x, y), fill=YELLOW)
    d.point((11, 9), fill=YELLOW)
    d.line((10, 9, 12, 9), fill=YELLOW)
    d.line((11, 8, 11, 10), fill=YELLOW)


def painting_flower(c, d):
    d.rectangle((0, 0, 13, 12), fill=(250, 240, 210, 255))
    d.rectangle((0, 11, 13, 12), fill=WOOD_LIGHT)
    d.line((7, 4, 7, 8), fill=LEAF_DARK)
    d.point((6, 6), fill=LEAF)
    d.point((8, 5), fill=LEAF)
    d.ellipse((5, 1, 9, 5), fill=(235, 90, 110, 255))
    d.point((7, 3), fill=YELLOW)
    d.polygon([(5, 8), (9, 8), (10, 11), (4, 11)], fill=BLUE)
    d.line((5, 9, 9, 9), fill=(150, 190, 240, 255))


def painting_bear(c, d):
    d.rectangle((0, 0, 13, 12), fill=(190, 225, 200, 255))
    d.ellipse((2, 1, 5, 4), fill=mp.FUR)
    d.ellipse((8, 1, 11, 4), fill=mp.FUR)
    d.point((3, 2), fill=mp.MUZZLE)
    d.point((10, 2), fill=mp.MUZZLE)
    d.ellipse((2, 2, 11, 11), fill=mp.FUR)
    d.ellipse((5, 6, 8, 10), fill=mp.MUZZLE)
    d.point((4, 5), fill=BLACK)
    d.point((9, 5), fill=BLACK)
    d.line((6, 7, 7, 7), fill=BLACK)
    d.point((3, 8), fill=(220, 120, 110, 255))
    d.point((10, 8), fill=(220, 120, 110, 255))
    d.rectangle((3, 12, 10, 12), fill=RED)


def painting_sea(c, d):
    d.rectangle((0, 0, 13, 6), fill=SKY)
    d.ellipse((1, 1, 3, 3), fill=YELLOW)
    d.rectangle((0, 7, 13, 12), fill=BLUE)
    d.line((0, 7, 13, 7), fill=(60, 110, 180, 255))
    for x, y in [(1, 10), (2, 9), (9, 11), (10, 10), (12, 9)]:
        d.point((x, y), fill=WHITE)
    d.polygon([(4, 7), (11, 7), (10, 8), (5, 8)], fill=WOOD_MID)
    d.line((7, 1, 7, 6), fill=WOOD)
    d.polygon([(8, 1), (8, 6), (11, 6)], fill=WHITE)
    d.polygon([(6, 2), (6, 6), (4, 6)], fill=(240, 120, 100, 255))


def painting_forest(c, d):
    d.rectangle((0, 0, 13, 12), fill=(200, 230, 245, 255))
    d.rectangle((0, 10, 13, 12), fill=(110, 160, 80, 255))
    for x, top, col in [(2, 3, LEAF_DARK), (7, 1, (50, 110, 60, 255)), (11, 4, LEAF_DARK), (5, 5, LEAF)]:
        d.polygon([(x - 2, top + 6), (x, top), (x + 2, top + 6)], fill=col)
        d.polygon([(x - 2, top + 4), (x, top + 1), (x + 2, top + 4)], fill=col)
        d.line((x, top + 7, x, 10), fill=WOOD)


def painting_abstract(c, d):
    d.rectangle((0, 0, 13, 12), fill=WHITE)
    d.ellipse((1, 1, 6, 6), fill=RED)
    d.rectangle((7, 2, 12, 7), fill=BLUE)
    d.polygon([(2, 12), (6, 6), (10, 12)], fill=YELLOW)
    d.line((0, 9, 13, 5), fill=LEAF)
    d.line((0, 10, 13, 6), fill=LEAF)
    d.rectangle((11, 9, 12, 11), fill=(170, 110, 210, 255))
    d.point((4, 3), fill=WHITE)


def painting_honey(c, d):
    d.rectangle((0, 0, 13, 12), fill=(255, 225, 230, 255))
    heart = [(1, 1), (2, 0), (3, 1), (4, 0), (5, 1)]
    d.polygon([(1, 2), (3, 0), (5, 0), (7, 2), (9, 0), (11, 0), (13, 2), (13, 3), (7, 9), (1, 3)],
              fill=(230, 70, 90, 255))
    del heart
    d.point((3, 1), fill=(255, 160, 170, 255))
    # горнецът с мед отпред, вдясно долу
    d.ellipse((6, 6, 13, 12), fill=(220, 150, 40, 255))
    d.rectangle((7, 5, 12, 6), fill=(150, 90, 40, 255))
    d.rectangle((8, 8, 11, 10), fill=CREAM)
    d.point((9, 9), fill=(220, 150, 40, 255))
    d.point((10, 9), fill=(220, 150, 40, 255))
    d.line((7, 7, 7, 8), fill=(255, 200, 80, 255))


def painting_chick(c, d):
    """Патутник: жълто кръгло смешно човече (пиле) с оранжев контур."""
    d.rectangle((0, 0, 13, 12), fill=(205, 235, 250, 255))
    art = [
        "..............",
        ".....ooo......",
        "....owyyo.....",
        "...owyyyyo....",
        "..oyyyyyyyo...",
        "..oykyyykyo...",
        "..opybbyypo...",
        "..oyyyyyyyoo..",
        "..oyYYYYyyo...",
        "...oyYYyyo....",
        "....ooooo.....",
        ".....f..f.....",
        "..............",
    ]
    pal = {"o": (235, 135, 40, 255), "y": (255, 238, 110, 255), "Y": (255, 214, 80, 255),
           "w": WHITE, "k": (50, 25, 15, 255), "p": (250, 185, 120, 255),
           "b": (240, 110, 30, 255), "f": (240, 110, 30, 255)}
    for y, row in enumerate(art):
        for x, ch in enumerate(row):
            if ch in pal:
                d.point((x, y), fill=pal[ch])


PAINTINGS = [painting_mountains, painting_night, painting_flower, painting_bear,
             painting_sea, painting_forest, painting_abstract, painting_honey, painting_chick]


def easel_paintings():
    base = Image.open(pl.path("easel")).convert("RGBA")
    out = []
    for fn in PAINTINGS:
        canvas = Image.new("RGBA", (14, 13), (0, 0, 0, 0))
        fn(canvas, ImageDraw.Draw(canvas))
        img = base.copy()
        img.paste(canvas, CANVAS)
        out.append(img)
    return out


# ---------------------------------------------------------------- растения


def pot(img):
    """Една и съща глинена саксия, 12×8, долу в средата (y 32…39)."""
    d = ImageDraw.Draw(img)
    d.line((6, 32, 17, 32), fill=O)
    d.point((6, 33), fill=O)
    d.point((17, 33), fill=O)
    d.line((6, 34, 17, 34), fill=O)
    d.line((6, 35, 17, 35), fill=O)
    d.line((7, 34, 16, 34), fill=CLAY_LIGHT)
    d.rectangle((7, 36, 16, 39), fill=CLAY, outline=O)
    d.line((8, 36, 15, 36), fill=CLAY_DARK)
    d.point((9, 37), fill=CLAY_LIGHT)


def soil(img):
    d = ImageDraw.Draw(img)
    d.line((7, 33, 16, 33), fill=SOIL)


def leaf(d, b, fill=LEAF, vein=LEAF_DARK, horizontal=True):
    d.ellipse(b, fill=fill, outline=O)
    x0, y0, x1, y1 = b
    if horizontal and x1 - x0 >= 4:
        d.line((x0 + 1, (y0 + y1) // 2, x1 - 1, (y0 + y1) // 2), fill=vein)


def sunflower(t):
    img, d = new(24, 40)
    pot(img)
    soil(img)
    if t == 0:
        pix(img, [(11, 32), (11, 31)], LEAF_DARK)
        pix(img, [(10, 30), (12, 30)], LEAF)
        pix(img, [(9, 30), (13, 30)], LEAF_LIGHT)
        return img
    height = [0, 5, 8, 11, 13, 15, 17, 19, 21, 22][t]
    top = 32 - height
    thick = t >= 4
    d.line((11, 33, 11, top), fill=LEAF_DARK)
    if thick:
        d.line((12, 33, 12, top), fill=(95, 160, 70, 255))
    # листа: по двойка на всеки ~5 px, по-долните по-големи
    lw = min(2 + t // 2, 6)
    lh = 2 if t < 5 else 3
    y = 29
    side = 0
    while y > top + (3 if t < 5 else 6):
        if side % 2 == 0:
            leaf(d, (11 - lw - 1, y - lh // 2 - 1, 11, y + lh // 2))
        else:
            leaf(d, (12 + int(thick), y - lh // 2 - 1, 12 + int(thick) + lw + 1, y + lh // 2))
        side += 1
        y -= 4 if t < 6 else 5
    # връх: листенца, пъпка или цвят
    cx = 11 + (0.5 if thick else 0)
    if t <= 4:
        pix(img, [(10, top), (12, top), (9, top), (13, top)], LEAF_LIGHT)
        pix(img, [(11, top - 1)], LEAF_LIGHT)
    elif t == 5:
        d.ellipse((10, top - 3, 13, top), fill=LEAF, outline=O)
    elif t == 6:
        d.ellipse((9, top - 4, 14, top), fill=LEAF, outline=O)
        pix(img, [(10, top - 4), (12, top - 5), (13, top - 4), (11, top - 4)], YELLOW)
    else:
        r = {7: 3, 8: 4, 9: 6}[t]
        cy = top - r + 3
        x0, x1 = int(cx - r), int(math.ceil(cx + r))
        d.ellipse((x0, cy - r, x1, cy + r), fill=(250, 200, 40, 255), outline=O)
        # зъбчета на венчелистчетата
        for a in range(0, 360, 30 if r >= 4 else 45):
            px_ = cx + (r + 0.5) * math.cos(math.radians(a))
            py_ = cy + (r + 0.5) * math.sin(math.radians(a))
            pix(img, [(int(round(px_)), int(round(py_)))], (250, 200, 40, 255))
        cr = max(1, r // 2)
        d.ellipse((int(cx - cr), cy - cr, int(math.ceil(cx + cr)), cy + cr), fill=(120, 70, 30, 255),
                  outline=(80, 45, 20, 255) if r >= 4 else None)
        if r >= 6:
            for x, y2 in [(int(cx) - 1, cy - 1), (int(cx) + 1, cy), (int(cx), cy + 1), (int(cx) - 1, cy + 2)]:
                pix(img, [(x, y2)], (160, 100, 45, 255))
        if r >= 4:
            pix(img, [(x0 + 2, cy - r + 2)], (255, 235, 140, 255))
    return img


MON = (50, 145, 80, 255)
MON_LIGHT = (90, 180, 100, 255)
MON_DARK = (30, 100, 55, 255)
# Листата при пълния ръст (стадий 9): център и радиуси. Появяват се в този ред.
MONSTERA_LEAVES = [(7, 14), (17, 19), (5, 24), (16, 10), (10, 5), (19, 28), (3, 30)]
# L светло, G зелено, D среден нерв; тъмните линии отстрани са срезите
LEAF_BIG = ["..OOOOOOO..",
            ".OLLLDGGGO.",
            "OLLLLDGGGGO",
            "OOOLLDGGOOO",
            "OLLLLDGGGGO",
            "OLLLLDGGGGO",
            "OOLLLDGGOOO",
            ".OLLLDGGGO.",
            "..OOOOOOO.."]
LEAF_MID = [".OOOOO.",
            "OLLDGGO",
            "OOLDGOO",
            "OLLDGGO",
            "OLLDGGO",
            ".OOOOO."]
LEAF_SMALL = [".OOO.",
              "OLDGO",
              "OLDGO",
              ".OOO."]


def stamp(img, tpl, cx, cy, flip=False):
    cols = {"O": O, "L": MON_LIGHT, "G": MON, "D": MON_DARK}
    h, w = len(tpl), len(tpl[0])
    x0, y0 = cx - w // 2, cy - h // 2
    px = img.load()
    for y, row in enumerate(tpl):
        for x, ch in enumerate(row[::-1] if flip else row):
            if ch != "." and 0 <= x0 + x < img.width and 0 <= y0 + y < img.height:
                px[x0 + x, y0 + y] = cols[ch]


def monstera(t):
    img, d = new(24, 40)
    pot(img)
    soil(img)
    if t == 0:
        pix(img, [(11, 32), (11, 31), (11, 30)], MON_DARK)
        pix(img, [(12, 29), (12, 30), (13, 29)], MON_LIGHT)
        return img
    count = [0, 1, 2, 2, 3, 4, 5, 5, 6, 7][t]
    s = [0, 0.3, 0.4, 0.5, 0.58, 0.66, 0.74, 0.84, 0.92, 1.0][t]
    tpl = LEAF_SMALL if t <= 2 else LEAF_MID if t <= 5 else LEAF_BIG
    leaves = []
    for i, (cx, cy) in enumerate(MONSTERA_LEAVES[:count]):
        x = round(11.5 + (cx - 11.5) * s)
        y = round(32 + (cy - 32) * s)
        small = tpl is LEAF_BIG and i >= 5   # последните две са по-млади
        leaves.append((x, y, LEAF_MID if small else tpl))
    for x, y, _t in leaves:                 # дръжките отзад
        d.line((11 if x < 12 else 12, 33, x, y + 1), fill=MON_DARK)
    for x, y, tp in sorted(leaves, key=lambda l: l[1]):
        stamp(img, tp, x, y, flip=x > 12)
    if t < 9 and t >= 2:   # ново свито листенце горе
        tx, ty = 12, round(32 - 31 * s)
        d.line((12, 33, tx, ty + 1), fill=MON_DARK)
        pix(img, [(tx, ty), (tx + 1, ty - 1), (tx + 1, ty)], MON_LIGHT)
    return img


CAC = (95, 165, 95, 255)
CAC_LIGHT = (140, 200, 120, 255)
CAC_DARK = (60, 120, 70, 255)
SPINE = (250, 245, 210, 255)


def column(d, img, x0, x1, top, bottom):
    w = x1 - x0
    d.ellipse((x0, top, x1, top + w), fill=CAC, outline=O)
    d.rectangle((x0, top + w // 2, x1, bottom), fill=CAC, outline=O)
    d.rectangle((x0 + 1, top + w // 2, x1 - 1, bottom - 1), fill=CAC)
    if w >= 4:
        d.line((x0 + 1, top + w // 2, x0 + 1, bottom - 1), fill=CAC_LIGHT)
        d.line((x1 - 1, top + w // 2, x1 - 1, bottom - 1), fill=CAC_DARK)
    if w >= 6:
        mid = (x0 + x1) // 2
        d.line((mid, top + 2, mid, bottom - 1), fill=CAC_DARK)


def cactus(t):
    img, d = new(24, 40)
    pot(img)
    soil(img)
    if t == 0:
        d.ellipse((10, 30, 13, 33), fill=CAC, outline=O)
        pix(img, [(11, 31)], CAC_LIGHT)
        return img
    w = [0, 4, 4, 5, 5, 6, 6, 7, 7, 7][t]
    height = [0, 5, 8, 11, 14, 17, 20, 22, 24, 25][t]
    x0 = 12 - w // 2 - 1
    x1 = x0 + w
    top = 33 - height
    # ръце: отзад, после стволът
    if t >= 5:
        ay = 33 - height // 2 + 1
        reach = 3 + (t - 5) // 2
        up = [0, 0, 0, 0, 0, 4, 6, 7, 8, 9][t]
        ax0 = x0 - reach - 1
        d.rectangle((ax0, ay, x0 + 1, ay + 3), fill=CAC, outline=O)
        column(d, img, ax0, ax0 + 3, ay - up, ay + 3)
        d.rectangle((ax0 + 1, ay + 1, x0 + 1, ay + 2), fill=CAC)
    if t >= 7:
        ay = 33 - height // 2 - 3
        reach = 3 if t < 9 else 4
        up = [0] * 7 + [4, 6, 7]
        ax1 = x1 + reach + 1
        d.rectangle((x1 - 1, ay, ax1, ay + 3), fill=CAC, outline=O)
        column(d, img, ax1 - 3, ax1, ay - up[t], ay + 3)
        d.rectangle((x1 - 1, ay + 1, ax1 - 1, ay + 2), fill=CAC)
    column(d, img, x0, x1, top, 33)
    # бодлички
    for y in range(top + 3, 31, 5):
        pix(img, [(x0 - 1, y), (x1 + 1, y + 2)], SPINE)
    # цвят
    mid = (x0 + x1) // 2
    if t == 8:
        d.rectangle((mid, top - 2, mid + 1, top), fill=PINK, outline=None)
        pix(img, [(mid, top - 2), (mid + 1, top - 2)], PINK_DARK)
    if t == 9:
        d.ellipse((mid - 2, top - 3, mid + 3, top + 1), fill=PINK, outline=O)
        pix(img, [(mid, top - 1), (mid + 1, top - 1)], YELLOW)
        pix(img, [(mid - 1, top - 2)], (255, 200, 215, 255))
    return img


# ---------------------------------------------------------------- инструменти в лапите (27×27, хват в 13,13)


def tool_shovel():
    img, d = new(27, 27)
    # дръжка: по диагонал през центъра
    d.polygon([(9, 17), (10, 18), (15, 13), (14, 12)], fill=WOOD_LIGHT, outline=O)
    d.point((10, 16), fill=OAK)
    d.point((12, 14), fill=OAK)
    d.line((15, 12, 16, 11), fill=GREY)                    # шийка
    d.polygon([(15, 12), (15, 8), (19, 4), (24, 2), (22, 7), (18, 11)], fill=STEEL, outline=O)
    d.line((16, 9, 20, 5), fill=WHITE)
    d.point((13, 13), fill=WOOD_LIGHT)
    return img


def tool_scissors():
    img, d = new(27, 27)
    # две леко разтворени остриета нагоре вдясно
    d.polygon([(15, 10), (21, 3), (23, 4), (16, 12)], fill=STEEL, outline=O)
    d.polygon([(16, 12), (24, 8), (24, 10), (16, 13)], fill=LGREY, outline=O)
    d.line((17, 9, 20, 5), fill=WHITE)
    # червените дръжки: разтворени надолу вляво; хватът е в 13,13
    d.polygon([(15, 12), (8, 14), (7, 16), (9, 17), (15, 14)], fill=RED, outline=O)
    d.polygon([(15, 13), (11, 19), (12, 21), (14, 20), (16, 14)], fill=RED_DARK, outline=O)
    d.ellipse((14, 11, 17, 14), fill=DGREY, outline=O)   # нитът
    d.point((15, 12), fill=LGREY)
    d.point((13, 13), fill=RED)
    return img


# ---------------------------------------------------------------- хранилки 16×28


def pole(d, top, base=(3, 25, 12, 27)):
    box(d, (6, top, 9, 25), WOOD_MID)
    d.line((7, top + 1, 7, 24), fill=WOOD_LIGHT)
    box(d, base, WOOD)
    d.line((base[0] + 1, base[1] + 1, base[2] - 1, base[1] + 1), fill=WOOD_MID)


def feeder_wood(food):
    img, d = new(16, 28)
    pole(d, 16)
    d.line((2, 8, 2, 14), fill=O)
    d.line((13, 8, 13, 14), fill=O)
    box(d, (0, 14, 15, 16), WOOD_LIGHT)
    d.line((1, 15, 14, 15), fill=WOOD_MID)
    if food:
        d.ellipse((3, 11, 12, 15), fill=SEED)
        seeds(img, 4, 12, 11, 13)
        seeds(img, 3, 13, 12, 13, 1)
    d.polygon([(7, 1), (8, 1), (15, 8), (0, 8)], fill=WOOD_MID, outline=O)
    d.line((0, 8, 15, 8), fill=O)
    d.line((7, 2, 2, 7), fill=WOOD_LIGHT)
    return img


def feeder_tube(food):
    img, d = new(16, 28)
    box(d, (2, 2, 4, 25), (60, 90, 70, 255))                 # стойка с кука
    d.line((3, 3, 3, 24), fill=(90, 130, 100, 255))
    box(d, (2, 1, 11, 3), (60, 90, 70, 255))
    d.line((3, 2, 10, 2), fill=(90, 130, 100, 255))
    d.line((10, 4, 10, 5), fill=O)
    box(d, (0, 25, 7, 27), (60, 90, 70, 255))
    box(d, (7, 5, 13, 7), LEAF)                             # капаче
    box(d, (8, 7, 12, 20), (215, 235, 245, 255))            # тръба
    if food:
        seeds(img, 9, 9, 11, 19)
    else:
        d.line((9, 8, 9, 19), fill=WHITE)
    for y in (11, 17):                                      # отвори и кацалки
        d.point((8, y), fill=O)
        d.point((12, y), fill=O)
        d.line((6, y + 1, 7, y + 1), fill=STEEL)
        d.line((13, y + 1, 14, y + 1), fill=STEEL)
    box(d, (7, 20, 13, 21), LEAF)
    return img


def feeder_cottage(food):
    img, d = new(16, 28)
    pole(d, 17)
    box(d, (2, 8, 13, 16), CREAM)
    box(d, (4, 10, 11, 15), (70, 45, 35, 255))              # отворът
    if food:
        d.ellipse((4, 12, 11, 17), fill=SEED)
        seeds(img, 5, 13, 10, 14, 2)
        seeds(img, 3, 15, 12, 15)
    box(d, (1, 16, 14, 17), WOOD_LIGHT)
    if food:
        seeds(img, 3, 15, 12, 15)
    d.polygon([(7, 0), (8, 0), (15, 7), (15, 8), (0, 8), (0, 7)], fill=RED, outline=O)
    d.line((0, 8, 15, 8), fill=O)
    d.line((7, 1, 2, 6), fill=(240, 110, 100, 255))
    box(d, (11, 1, 12, 4), (180, 120, 100, 255))           # комин
    d.point((11, 5), fill=RED)
    d.point((12, 5), fill=RED)
    return img


def feeder_round(food):
    img, d = new(16, 28)
    pole(d, 14)
    d.line((7, 4, 7, 10), fill=O)
    d.line((8, 4, 8, 10), fill=O)
    d.ellipse((2, 1, 13, 5), fill=(90, 160, 200, 255), outline=O)   # капак
    d.line((4, 2, 8, 2), fill=(150, 200, 230, 255))
    if food:
        d.ellipse((3, 8, 12, 13), fill=SEED, outline=O)
        seeds(img, 4, 9, 11, 11)
        seeds(img, 6, 8, 9, 8, 1)
    d.ellipse((0, 10, 15, 15), fill=(90, 160, 200, 255), outline=O)  # паничка
    d.ellipse((2, 11, 13, 13), fill=(50, 110, 150, 255) if not food else SEED)
    if food:
        seeds(img, 4, 11, 11, 12, 3)
    d.line((2, 14, 13, 14), fill=(60, 120, 165, 255))
    return img


def feeder_birdhouse(food):
    img, d = new(16, 28)
    pole(d, 20)
    box(d, (2, 8, 13, 20), WOOD_LIGHT)
    for y in (11, 14, 17):
        d.line((3, y, 12, y), fill=OAK)
    d.ellipse((5, 9, 10, 14), fill=(50, 30, 20, 255), outline=O)    # дупката
    d.line((7, 15, 8, 15), fill=O)                                   # кацалка
    d.line((7, 16, 8, 16), fill=WOOD)
    box(d, (0, 20, 15, 22), WOOD_MID)
    if food:
        seeds(img, 1, 19, 14, 19)
        seeds(img, 2, 18, 4, 18, 1)
        seeds(img, 11, 18, 13, 18, 2)
    d.polygon([(7, 1), (8, 1), (15, 8), (0, 8)], fill=(70, 150, 160, 255), outline=O)
    d.line((0, 8, 15, 8), fill=O)
    d.line((7, 2, 2, 7), fill=(120, 190, 200, 255))
    return img


FEEDERS = {"": feeder_wood, "_tube": feeder_tube, "_cottage": feeder_cottage,
           "_round": feeder_round, "_birdhouse": feeder_birdhouse}


# ---------------------------------------------------------------- птици 10×10 (гледат надясно)

BIRD_FRAMES = {
    "sit": [
        "..........",
        ".....OOO..",
        "....OHHHO.",
        "....OHHEOK",
        ".OOOOZZCO.",
        "OTTWWWBCCO",
        ".OTWWWBCCO",
        "..OOWWBCO.",
        "....OOOO..",
        ".....L.L..",
    ],
    "peck": [
        "..........",
        "..........",
        "OO........",
        "OTOOOO....",
        ".OTWWWOOOO",
        "..OWWWHHEO",
        "..OBWCCZHO",
        "...OBCCOOK",
        "....OOO...",
        "....L.L...",
    ],
    "fly1": [
        "..OO......",
        "..OWO.....",
        "...OWO.OO.",
        "...OWWOHHO",
        "OOOOWWHHEK",
        "OTTBWBZCCO",
        ".OOBBCCCO.",
        "...OOOOO..",
        "..........",
        "..........",
    ],
    "fly2": [
        "..........",
        "..........",
        ".......OO.",
        "......OHHO",
        "OOOOOOHHEK",
        "OTTBBBZCCO",
        ".OOWWWCCO.",
        "...OWWOOO.",
        "...OWO....",
        "....O.....",
    ],
}

BEAK = (230, 160, 60, 255)
BEAK_DARK = (50, 50, 55, 255)
LEGS = (120, 80, 60, 255)
# вид → H глава, B гръб, W крило, T опашка, C гърди, Z буза, K клюн, E око
BIRDS = {
    "sparrow": dict(H=(140, 95, 60, 255), B=(165, 120, 80, 255), W=(110, 70, 40, 255), T=(100, 70, 45, 255),
                    C=(225, 205, 170, 255), Z=(200, 190, 175, 255), K=BEAK_DARK, E=BLACK),
    "robin": dict(H=(140, 110, 75, 255), B=(150, 120, 85, 255), W=(115, 90, 60, 255), T=(110, 85, 55, 255),
                  C=(240, 120, 50, 255), Z=(240, 120, 50, 255), K=BEAK_DARK, E=BLACK),
    "tit": dict(H=(35, 35, 45, 255), B=(130, 160, 90, 255), W=(90, 130, 170, 255), T=(80, 110, 150, 255),
                C=(250, 215, 60, 255), Z=WHITE, K=BEAK_DARK, E=WHITE),
    "bullfinch": dict(H=(30, 30, 38, 255), B=(150, 150, 160, 255), W=(55, 55, 65, 255), T=(40, 40, 50, 255),
                      C=(225, 70, 70, 255), Z=(225, 70, 70, 255), K=BEAK_DARK, E=WHITE),
    "bluebird": dict(H=(70, 120, 220, 255), B=(80, 135, 225, 255), W=(50, 90, 190, 255), T=(45, 80, 170, 255),
                     C=(200, 225, 250, 255), Z=(110, 160, 235, 255), K=BEAK_DARK, E=BLACK),
}


def bird(kind, frame):
    cols = dict(BIRDS[kind], O=O, L=LEGS)
    img, _ = new(10, 10)
    px = img.load()
    for y, row in enumerate(BIRD_FRAMES[frame]):
        assert len(row) == 10, (frame, y, row)
        for x, ch in enumerate(row):
            if ch != ".":
                px[x, y] = cols[ch]
    return img


# ---------------------------------------------------------------- бюро и столче


def desk_skin(kind):
    src = Image.open(pl.path("desk")).convert("RGBA")
    img, d = new(24, 22)
    img.paste(src.crop((0, 0, 24, 12)), (0, 0))          # лаптопът, лампата и чашката остават
    top, light, dark = {"oak": (OAK, (225, 185, 125, 255), OAK_DARK),
                        "white": ((240, 240, 235, 255), WHITE, (200, 200, 195, 255)),
                        "pink": (PINK, (250, 190, 205, 255), PINK_DARK),
                        "drawers": (WOOD_LIGHT, OAK, WOOD_MID)}[kind]
    if kind == "white":
        for x in (1, 21):
            box(d, (x, 13, x + 1, 21), (215, 215, 220, 255))
    elif kind == "pink":
        for x in (1, 20):
            box(d, (x, 13, x + 2, 21), top)
            d.line((x + 1, 14, x + 1, 20), fill=light)
        d.line((3, 18, 20, 18), fill=O)
    elif kind == "drawers":
        box(d, (0, 13, 3, 21), top)
        d.line((1, 14, 1, 20), fill=light)
        box(d, (14, 13, 23, 21), top)
        d.line((14, 17, 23, 17), fill=O)
        for y in (15, 19):
            d.line((17, y, 20, y), fill=dark)
            d.point((18, y), fill=YELLOW)
            d.point((19, y), fill=YELLOW)
    else:
        for x in (1, 20):
            box(d, (x, 13, x + 2, 21), top)
            d.line((x + 1, 14, x + 1, 20), fill=dark)
    box(d, (0, 12, 23, 14), top)
    d.line((1, 13, 22, 13), fill=light if kind != "oak" else top)
    if kind == "oak":
        d.line((3, 13, 8, 13), fill=dark)
        d.line((13, 13, 19, 13), fill=dark)
    return img


def stool_skin(kind):
    img, d = new(16, 8)
    if kind == "cushion":
        d.line((3, 4, 2, 7), fill=O)
        d.line((12, 4, 13, 7), fill=O)
        d.line((3, 6, 12, 6), fill=O)
        box(d, (1, 3, 14, 4), WOOD)
        d.rounded_rectangle((1, 0, 14, 3), radius=1, fill=PINK, outline=O)
        d.line((3, 1, 12, 1), fill=(250, 195, 210, 255))
        d.point((7, 2), fill=PINK_DARK)
        d.point((8, 2), fill=PINK_DARK)
    elif kind == "green":
        g, gl = (90, 160, 100, 255), (140, 200, 140, 255)
        for x0, x1 in ((3, 2), (12, 13)):
            d.line((x0, 3, x1, 7), fill=O)
        d.line((4, 3, 3, 7), fill=g)
        d.line((11, 3, 12, 7), fill=g)
        d.line((3, 5, 12, 5), fill=O)
        box(d, (1, 0, 14, 2), g)
        d.line((2, 1, 13, 1), fill=gl)
    elif kind == "log":
        box(d, (2, 2, 13, 7), WOOD_MID)
        for x in (4, 7, 10):
            d.line((x, 4, x, 6), fill=WOOD)
        d.ellipse((1, 0, 14, 4), fill=OAK, outline=O)
        d.ellipse((4, 1, 11, 3), outline=OAK_DARK)
        d.point((7, 2), fill=OAK_DARK)
        d.point((8, 2), fill=OAK_DARK)
    elif kind == "round":
        d.line((4, 3, 2, 7), fill=GREY)
        d.line((11, 3, 13, 7), fill=GREY)
        d.line((7, 3, 7, 7), fill=GREY)
        d.line((8, 3, 8, 7), fill=DGREY)
        d.line((3, 6, 12, 6), fill=DGREY)
        d.ellipse((1, 0, 14, 3), fill=(230, 120, 70, 255), outline=O)
        d.line((4, 1, 10, 1), fill=(250, 170, 120, 255))
    return img


# ---------------------------------------------------------------- иконки за магазина


def shop_desk():
    img, d = new(16, 16)
    d.polygon([(2, 8), (3, 3), (8, 3), (8, 8)], fill=GREY, outline=O)
    d.rectangle((4, 4, 7, 6), fill=SKY)
    d.line((11, 8, 11, 5), fill=O)
    d.polygon([(10, 5), (13, 5), (11, 3)], fill=YELLOW, outline=O)
    box(d, (0, 9, 15, 10), WOOD_MID)
    d.line((1, 11, 1, 14), fill=O)
    d.line((14, 11, 14, 14), fill=O)
    return img


def shop_stool():
    img, d = new(16, 16)
    d.line((4, 7, 2, 13), fill=O)
    d.line((11, 7, 13, 13), fill=O)
    d.line((3, 11, 12, 11), fill=O)
    box(d, (1, 4, 14, 7), WOOD_MID)
    d.line((2, 5, 13, 5), fill=WOOD_LIGHT)
    return img


def shop_feeder():
    img, d = new(16, 16)
    box(d, (6, 10, 9, 15), WOOD_MID)
    d.line((7, 11, 7, 14), fill=WOOD_LIGHT)
    d.line((3, 5, 3, 8), fill=O)
    d.line((12, 5, 12, 8), fill=O)
    box(d, (1, 8, 14, 10), WOOD_LIGHT)
    seeds(img, 4, 7, 11, 7)
    d.polygon([(7, 0), (8, 0), (15, 5), (0, 5)], fill=WOOD_MID, outline=O)
    d.line((7, 1, 3, 4), fill=WOOD_LIGHT)
    return img


# ---------------------------------------------------------------- преглед


def font(size):
    try:
        return ImageFont.truetype("DejaVuSans.ttf", size)
    except OSError:
        return ImageFont.load_default(size=size)


def sheet():
    small, title = font(9), font(13)
    groups = []
    for name, (_folder, img, group) in OUT.items():
        if not groups or groups[-1][0] != group:
            groups.append((group, []))
        groups[-1][1].append((name, img))
    max_w, gap, label_w = 1500, 8, 0
    rows = []   # (група, [(име, картинка, x)], височина)
    for group, items in groups:
        x, cur = 0, []
        hmax = 0
        for name, img in items:
            w = max(img.width * ZOOM, small.getlength(name) + 4, 36)
            if cur and x + w > max_w:
                rows.append((group, cur, hmax))
                group, x, cur, hmax = "", 0, [], 0
            cur.append((name, img, x))
            hmax = max(hmax, img.height * ZOOM)
            x += w + gap
        rows.append((group, cur, hmax))
    height = sum(h + 30 + (18 if g else 0) for g, _c, h in rows) + 10
    out = Image.new("RGBA", (max_w + 20, height), (235, 235, 235, 255))
    d = ImageDraw.Draw(out)
    y = 6
    for group, cur, h in rows:
        if group:
            d.text((8, y), group, fill=(40, 40, 40), font=title)
            y += 18
        for name, img, x in cur:
            big = img.resize((img.width * ZOOM, img.height * ZOOM), Image.NEAREST)
            out.paste(Image.new("RGBA", big.size, (255, 255, 255, 255)), (10 + int(x), y + h - big.height))
            out.alpha_composite(big, (10 + int(x), y + h - big.height))
            d.text((10 + x, y + h + 2), name, fill=(40, 40, 40), font=small)
            d.text((10 + x, y + h + 13), f"{img.width}×{img.height}", fill=(120, 120, 120), font=small)
        y += h + 30
        d.line((0, y - 4, out.width, y - 4), fill=(200, 200, 200))
    out.save(SHEET)


def differs(a, b):
    pa, pb = a.load(), b.load()
    return sum(1 for y in range(a.height) for x in range(a.width) if pa[x, y] != pb[x, y])


def main():
    g = "Радио (скинове)"
    save("radio_gramophone", radio_gramophone(), group=g)
    save("radio_boombox", radio_boombox(), group=g)
    save("radio_retro", radio_retro(), group=g)
    save("radio_jukebox", radio_jukebox(), group=g)

    for i, img in enumerate(easel_paintings(), 1):
        save(f"easel_{i}", img, group="Статив (картини)")

    for kind, fn in (("sunflower", sunflower), ("monstera", monstera), ("cactus", cactus)):
        prev = None
        for t in range(10):
            img = fn(t)
            if prev is not None:
                assert differs(prev, img) >= 4, (kind, t)
            prev = img
            save(f"plant_{kind}_{t}", img, group=f"Растение: {kind}")

    save("tool_shovel", tool_shovel(), CLOTHES, "Инструменти (в лапите, хват 13,13)")
    save("tool_scissors", tool_scissors(), CLOTHES, "Инструменти (в лапите, хват 13,13)")

    for skin, fn in FEEDERS.items():
        save(f"feeder{skin}", fn(False), group="Хранилки")
        save(f"feeder{skin}_food", fn(True), group="Хранилки")

    for kind in BIRDS:
        for frame in ("fly1", "fly2", "sit", "peck"):
            save(f"bird_{kind}_{frame}", bird(kind, frame), group="Птици")

    for kind in ("oak", "white", "pink", "drawers"):
        save(f"desk_{kind}", desk_skin(kind), group="Бюро и столче (скинове)")
    for kind in ("cushion", "green", "log", "round"):
        save(f"stool_{kind}", stool_skin(kind), group="Бюро и столче (скинове)")

    save("shop_desk", shop_desk(), group="Магазин")
    save("shop_stool", shop_stool(), group="Магазин")
    save("shop_feeder", shop_feeder(), group="Магазин")

    sheet()
    print(f"{len(OUT)} файла, преглед: art_templates/items_sheet.png")


if __name__ == "__main__":
    main()
