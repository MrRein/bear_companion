"""Спрайтовете на менюто (рамка, хартия, табове, бутони, деленца, плочки…) в стила на храната.

    python tools/make_ui_skin.py            -> assets/ui/skin/skin.png и skin.json
    python tools/make_ui_skin.py <папка>    -> само в тази папка (за преглед)

Всички спрайтове са в ЕДИН файл – skin.png (подредени на редове, с 2 празни пиксела
помежду им) – освен фона на рамката (дървото): той е отделен файл за всеки цвят на менюто,
frame_<скин>.png (classic, birch, grass, stone, sakura, night), нарисуван да отива на скина
и не се преоцветява. В skin.json за всеки:
  file  – (само за рамката) от кой файл е; {theme} е скинът;
  rect  – къде е в skin.png (x, y, ширина, височина);

Всеки спрайт се „реже“ на 9 части (9-slice): ъглите стоят както са нарисувани, ръбовете и
средата се повтарят (не се разтягат), за да остават пикселите цели. В skin.json за всеки:
  slice – колко пиксела от всяка страна са ръб (ляво, горе, дясно, долу);
  pad   – колко навътре започва съдържанието (текстът), в пиксели на спрайта.
Един пиксел на спрайта = един пиксел на храната в менюто.

Нарисувани са в цветовете на класическия скин („Дъб и мед“); другите скинове ги
преоцветяват сами (без спрайтовете с "recolor": false – те са еднакви навсякъде). Искаш
ли някой скин нарисуван на ръка – направи skin_<скин>.png (например skin_birch.png) със
същата подредба: тогава той се ползва както е, без преоцветяване. Искаш ли да ги прерисуваш на ръка – отвори skin.png и рисувай всеки
спрайт в неговото квадратче (същия размер); програмата го взима оттам. Коя е всяка и
къде са ръбовете – виж art_templates/skin_map.png (сините линии делят 9-те части). Не пускай
скрипта след това – той рисува skin.png наново (скриптът е само първият вариант).
"""

import json
import os
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..")
OUT = os.path.join(ROOT, "assets", "ui", "skin")

O = (13, 18, 28)
INK = O
WOOD, WOODD, WOODL = (139, 90, 43), (98, 62, 32), (181, 125, 70)
PAPER, PAPERD = (255, 244, 220), (240, 221, 186)
TILE, TILEART = (251, 233, 200), (236, 208, 160)
HOVER, HONEY, EMPTY = (255, 226, 154), (255, 209, 102), (214, 190, 150)
GOOD, OKAY, BAD = (126, 188, 89), (240, 180, 70), (220, 90, 80)


def mix(a, b, k):
    return tuple(int(round(a[i] + (b[i] - a[i]) * k)) for i in range(3))


def hi(c):
    return mix(c, (255, 255, 255), 0.4)


def lo(c):
    return mix(c, INK, 0.2)


class S:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), (0, 0, 0, 0))

    def px(self, x, y, c, a=255):
        if 0 <= x < self.w and 0 <= y < self.h:
            self.img.putpixel((x, y), (*c, a) if c is not None else (0, 0, 0, 0))

    def fill(self, c):
        for y in range(self.h):
            for x in range(self.w):
                self.px(x, y, c(x, y) if callable(c) else c)

    def rect(self, x0, y0, x1, y1, c):
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                self.px(x, y, c)

    def outline(self, top=1, bottom=1, left=1, right=1, color=O):
        for k in range(top):
            self.rect(0, k, self.w - 1, k, color)
        for k in range(bottom):
            self.rect(0, self.h - 1 - k, self.w - 1, self.h - 1 - k, color)
        for k in range(left):
            self.rect(k, 0, k, self.h - 1, color)
        for k in range(right):
            self.rect(self.w - 1 - k, 0, self.w - 1 - k, self.h - 1, color)

    def corners(self, tl, tr, br, bl, color=O):
        """Стъпаловидни ъгли: n стъпала по един пиксел; отвън е прозрачно, на ръба – контур."""
        def corner(n, fx, fy):
            for i in range(n + 1):
                for j in range(n + 1):
                    if i + j < n:
                        self.px(fx(i), fy(j), None)      # отвън – прозрачно
                    elif i + j == n:
                        self.px(fx(i), fy(j), color)     # стъпалото на контура
        corner(tl, lambda i: i, lambda j: j)
        corner(tr, lambda i: self.w - 1 - i, lambda j: j)
        corner(br, lambda i: self.w - 1 - i, lambda j: self.h - 1 - j)
        corner(bl, lambda i: i, lambda j: self.h - 1 - j)


DEFS = {}
SPRITES = {}
ATLAS_W = 100        # ширината на skin.png (в пиксели)
GAP = 2              # празно между спрайтовете


def save(name, s, slc, pad, out, recolor=True):
    SPRITES[name] = s.img
    DEFS[name] = {"slice": list(slc), "pad": list(pad)}
    if not recolor:
        DEFS[name]["recolor"] = False        # остава в своите цветове при всеки скин


def pack(out):
    """Подрежда спрайтовете на редове (в реда, в който са нарисувани) в един skin.png."""
    x = y = row = 0
    places = {}
    for name, img in SPRITES.items():
        if x and x + img.width > ATLAS_W:
            x, y, row = 0, y + row + GAP, 0
        places[name] = (x, y)
        DEFS[name]["rect"] = [x, y, img.width, img.height]
        x += img.width + GAP
        row = max(row, img.height)
    atlas = Image.new("RGBA", (ATLAS_W, y + row), (0, 0, 0, 0))
    for name, img in SPRITES.items():
        atlas.paste(img, places[name])
    atlas.save(os.path.join(out, "skin.png"))
    skin_map(atlas, out)


def skin_map(atlas, out, s=8):
    """Карта за рисуване: skin.png уголемен ×8, всяко квадратче с името си и ръбовете (slice)."""
    from PIL import ImageDraw, ImageFont
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 11)
    except OSError:
        font = ImageFont.load_default()
    big = Image.new("RGBA", (atlas.width * s, atlas.height * s + 14), (235, 235, 240, 255))
    big.alpha_composite(atlas.resize((atlas.width * s, atlas.height * s), Image.NEAREST), (0, 14))
    d = ImageDraw.Draw(big)
    for name, v in DEFS.items():
        x, y, w, h = v["rect"]
        L, T, R, B = v["slice"]
        x0, y0 = x * s, y * s + 14
        d.rectangle([x0, y0, x0 + w * s - 1, y0 + h * s - 1], outline=(220, 40, 120))
        for k in (L, w - R):           # ръбовете, които се повтарят само по дължина
            if 0 < k < w:
                d.line([(x0 + k * s, y0), (x0 + k * s, y0 + h * s)], fill=(60, 120, 230))
        for k in (T, h - B):
            if 0 < k < h:
                d.line([(x0, y0 + k * s), (x0 + w * s, y0 + k * s)], fill=(60, 120, 230))
        d.text((x0 + 2, y0 + 2), name, fill=(220, 40, 120), font=font)
    path = os.path.join(ROOT, "art_templates", "skin_map.png") if out == OUT else os.path.join(out, "skin_map.png")
    big.save(path)


# ───────────────────────── Рамка (фонът) – отделен файл за всеки скин ─────────────────────────
#
# Фонът на рамката (дървото) НЕ е в skin.png и не се преоцветява: за всеки цвят на менюто
# има свой нарисуван фон – frame_<скин>.png (50×50; ръбът е 5, средата 40×40 се повтаря).

FRAME, FS, PER = 50, 5, 40          # размер, ръб, през колко се повтаря шарката


def h(x, y, seed=0):
    """Псевдослучайно число 0..999 за точка (повтаря се през PER) – за разпръснати шарки."""
    x, y = x % PER, y % PER
    v = (x * 73856093) ^ (y * 19349663) ^ (seed * 83492791)
    v = (v ^ (v >> 13)) * 1274126177
    return (v ^ (v >> 16)) % 1000


def planks(x, y, wood, dark, light, seed=0):
    """Дъски по 5 реда, дълги по 20, разминати, с фуги и жилки."""
    row = y // 5
    ox = (x + (10 if row % 2 else 0)) % 20
    yy = y % 5
    if yy == 4 or ox == 19:
        return dark
    if yy == 0:
        return light
    if yy == 2 and h(x // 4, row, seed) < 450 and ox % 9 < 5:
        return light if h(x, row, seed + 1) < 500 else dark
    return wood


def frame_oak(x, y):
    return planks(x, y, (139, 90, 43), (98, 62, 32), (181, 125, 70))


def frame_birch(x, y):
    base, line, mark = (232, 228, 216), (206, 200, 186), (52, 48, 44)
    if y % 10 == 9:                                  # между дъските от бреза
        return line
    n = h(x // 3, y, 3)
    if n < 70 and y % 10 not in (0, 8):              # черните чертички по кората
        return mark
    if n < 120:
        return (186, 180, 168)
    if y % 10 == 0:
        return (248, 246, 238)
    return base


def frame_grass(x, y):
    dark, mid, light = (52, 96, 36), (90, 146, 58), (142, 196, 92)
    n = h(x, y, 5)
    if n < 6:
        return (255, 236, 120)                       # цветенце
    if n < 11:
        return (255, 255, 255)
    if n < 15:
        return (246, 160, 190)
    # листенца: светъл връх и тъмна сянка под него, по диагонал
    k = (x + 2 * y) % 6
    j = h(x // 3, y // 2, 7)
    if k == 0 and j < 650:
        return light
    if k == 1 and j < 650:
        return mid
    if (x + 2 * y) % 6 == 3 and h(x // 2, y // 3, 9) < 500:
        return dark
    return mid if j < 520 else (76, 128, 50)


def frame_stone(x, y):
    mortar, face, lit, shade = (78, 78, 86), (140, 140, 146), (182, 182, 188), (110, 110, 118)
    row = y // 8
    bw = 16 if row % 2 == 0 else 20
    ox = (x + (8 if row % 2 else 0)) % 40
    bx = ox % bw if ox < 40 - (40 % bw) else ox - (40 - (40 % bw))
    yy = y % 8
    if yy == 7 or bx == bw - 1 or ox == 39:
        return mortar
    if yy == 0 or bx == 0:
        return lit
    if yy == 6 or bx == bw - 2:
        return shade
    n = h(x, y, 11)
    if n < 60:
        return shade
    if n < 100:
        return lit
    return face


def frame_sakura(x, y):
    c = planks(x, y, (122, 70, 60), (86, 46, 40), (168, 106, 92), seed=4)
    n = h(x // 2, y // 2, 13)
    if n < 22:                                       # розово цветче (2×2, със светла точка)
        return (255, 214, 226) if (x + y) % 2 == 0 else (240, 150, 178)
    return c


def frame_night(x, y):
    sky, deep = (40, 46, 82), (32, 36, 66)
    n = h(x, y, 17)
    if n < 8:
        return (255, 250, 220)                       # звезда
    if n < 12:
        return (180, 190, 240)
    # няколко по-големи звезди – кръстче
    for cx, cy in ((7, 9), (27, 23), (16, 34), (34, 4)):
        dx, dy = (x - cx) % PER, (y - cy) % PER
        dx = dx - PER if dx > PER // 2 else dx
        dy = dy - PER if dy > PER // 2 else dy
        if dx == 0 and dy == 0:
            return (255, 240, 170)
        if abs(dx) + abs(dy) == 1:
            return (150, 160, 220)
    return deep if h(x // 2, y // 2, 19) < 160 else sky


FRAMES = {
    "classic": frame_oak,
    "birch": frame_birch,
    "grass": frame_grass,
    "stone": frame_stone,
    "sakura": frame_sakura,
    "night": frame_night,
}


def frames(out):
    for theme, fn in FRAMES.items():
        s = S(FRAME, FRAME)
        s.fill(fn)
        s.outline()
        s.corners(2, 2, 2, 2)
        s.img.save(os.path.join(out, f"frame_{theme}.png"))
    DEFS["frame"] = {"file": "frame_{theme}", "rect": [0, 0, FRAME, FRAME], "slice": [FS] * 4, "pad": [1, 1, 1, 1], "recolor": False}
    frames_map(out)


def frames_map(out, s=6):
    """Всички фонове един до друг (×6), за да се сравнят и прерисуват."""
    from PIL import ImageDraw, ImageFont
    try:
        font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    except OSError:
        font = ImageFont.load_default()
    cell = FRAME * s
    big = Image.new("RGBA", (len(FRAMES) * (cell + 10) + 10, cell + 34), (235, 235, 240, 255))
    d = ImageDraw.Draw(big)
    for i, theme in enumerate(FRAMES):
        img = Image.open(os.path.join(out, f"frame_{theme}.png"))
        x = 10 + i * (cell + 10)
        big.alpha_composite(img.resize((cell, cell), Image.NEAREST), (x, 24))
        for k in (FS, FRAME - FS):
            d.line([(x + k * s, 24), (x + k * s, 24 + cell)], fill=(60, 120, 230))
            d.line([(x, 24 + k * s), (x + cell, 24 + k * s)], fill=(60, 120, 230))
        d.text((x, 4), f"frame_{theme}.png", fill=(220, 40, 120), font=font)
    path = os.path.join(ROOT, "art_templates", "skin_frames.png") if out == OUT else os.path.join(out, "skin_frames.png")
    big.save(path)


# ───────────────────────── Хартия ─────────────────────────

def paper_px(x, y):
    if (x % 8, y % 8) in ((1, 1), (5, 5)):
        return PAPERD
    return PAPER


def panel(out, name="panel", corners=(0, 0, 2, 2)):
    s = S(24, 24)
    s.fill(paper_px)
    s.rect(1, 1, 22, 1, PAPERD)                  # вдлъбнато: сянка под горния ръб
    s.outline()
    s.corners(*corners)
    save(name, s, (4, 4, 4, 4), (1, 1, 1, 1), out)


# ───────────────────────── Табове ─────────────────────────

def tab(out, name, base, on=False):
    s = S(16, 14)
    if on:
        s.fill(paper_px)
        s.outline(bottom=0)
    else:
        s.fill(base)
        s.rect(1, 1, 14, 1, hi(base))
        s.rect(1, 12, 14, 12, lo(base))
        s.outline()
    s.corners(2, 2, 0, 0)
    save(name, s, (4, 4, 4, 2), (1, 1, 1, 0 if on else 1), out)


# ───────────────────────── Бутони ─────────────────────────

def button(out, name, base, state="up"):
    s = S(12, 12)
    s.fill(base)
    if state == "up":
        s.rect(1, 1, 10, 1, hi(base))                 # светло горе
        s.rect(1, 9, 10, 9, lo(base))                 # тъмно долу
        s.outline(bottom=2)
        pad = (1, 1, 1, 2)
        slc = (2, 2, 2, 3)
    elif state == "down":
        s.rect(1, 2, 10, 2, lo(base))                 # сянката е горе
        s.outline(top=2)
        pad = (1, 2, 1, 1)
        slc = (2, 3, 2, 2)
    else:                                             # плосък (не се натиска)
        s.outline()
        pad = (1, 1, 1, 1)
        slc = (2, 2, 2, 2)
    s.corners(1, 1, 1, 1)
    save(name, s, slc, pad, out)


# ───────────────────────── Деленца на лентите ─────────────────────────

def cell(out, name, base):
    s = S(5, 6)
    s.fill(base)
    s.rect(1, 1, 3, 1, hi(base))
    s.rect(1, 4, 3, 4, lo(base))
    s.outline()
    # Зелено/оранжево/червено значат „сит/гладен“ – еднакви при всеки цвят на менюто.
    save(name, s, (2, 2, 2, 2), (1, 1, 1, 1), out, recolor=name not in ("cell_good", "cell_okay", "cell_bad"))


# ───────────────────────── Плочки, табелки, джобове ─────────────────────────

def tile(out, name, base):
    s = S(24, 24)
    s.fill(base)
    s.rect(1, 1, 22, 1, hi(base))
    s.rect(1, 1, 1, 22, hi(base))
    s.rect(1, 22, 22, 22, lo(base))
    s.rect(22, 2, 22, 22, lo(base))
    s.outline()
    s.corners(2, 2, 2, 2)
    save(name, s, (6, 6, 6, 6), (1, 1, 1, 1), out)


def inset(out, name, base, edge):
    """Вдлъбната кутия (картинката в плочката, джобът): тъмен ръб, сянка отгоре."""
    s = S(16, 16)
    s.fill(base)
    s.rect(1, 1, 14, 1, lo(base))
    s.rect(1, 1, 1, 14, lo(base))
    s.outline(color=edge)
    s.corners(1, 1, 1, 1, color=edge)
    save(name, s, (4, 4, 4, 4), (1, 1, 1, 1), out)


def pill(out):
    s = S(16, 10)
    s.fill(HONEY)
    s.rect(1, 1, 14, 1, hi(HONEY))
    s.rect(1, 8, 14, 8, lo(HONEY))
    s.outline()
    s.corners(2, 2, 2, 2)
    save("pill", s, (4, 4, 4, 4), (1, 1, 1, 1), out)


def dash(out):
    s = S(3, 1)
    s.px(0, 0, WOODL)
    s.px(1, 0, WOODL)
    save("dash", s, (0, 0, 0, 0), (0, 0, 0, 0), out)


def main(out=OUT):
    os.makedirs(out, exist_ok=True)
    panel(out, "panel", (0, 0, 2, 2))
    panel(out, "card", (2, 2, 2, 2))
    tab(out, "tab", WOODL)
    tab(out, "tab_hover", HOVER)
    tab(out, "tab_on", PAPER, on=True)
    for name, base in (("button", PAPERD), ("button_honey", HONEY)):
        button(out, name, base, "up")
        button(out, name + "_hover", HOVER if name == "button" else hi(HONEY), "up")
        button(out, name + "_down", base, "down")
        button(out, name + "_flat", base, "flat")
    button(out, "button_on", HONEY, "down")
    for name, base in (("empty", EMPTY), ("good", GOOD), ("okay", OKAY), ("bad", BAD), ("honey", HONEY)):
        cell(out, "cell_" + name, base)
    tile(out, "tile", TILE)
    tile(out, "tile_hover", HOVER)
    inset(out, "tile_art", TILEART, WOODD)
    inset(out, "pocket", EMPTY, O)
    pill(out)
    dash(out)
    pack(out)
    frames(out)
    # Старите отделни файлове (преди всичко да е в skin.png) вече не трябват.
    for name in list(SPRITES) + ["frame"]:
        old = os.path.join(out, name + ".png")
        if os.path.exists(old):
            os.remove(old)
    defs = {k: {**({"file": v["file"]} if "file" in v else {}), "rect": v["rect"], "slice": v["slice"], "pad": v["pad"],
                **({"recolor": False} if v.get("recolor") is False else {})}
            for k, v in DEFS.items()}
    # По един ред на спрайт – лесно се чете и поправя на ръка.
    lines = [f'  "{k}": {json.dumps(v)}' for k, v in defs.items()]
    with open(os.path.join(out, "skin.json"), "w", encoding="utf-8") as fh:
        fh.write("{\n" + ",\n".join(lines) + "\n}\n")
    print("Спрайтове:", len(DEFS), "→", os.path.relpath(out, ROOT) if out == OUT else out)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else OUT)
