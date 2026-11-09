"""Мебелите и нещата, които мечокът ползва, в стила на храната на Тут (черен контур, три тона,
светлина отгоре вляво).

    python tools/make_furniture.py             -> assets/props/furniture/
    python tools/make_furniture.py <папка>     -> само в тази папка (за преглед)

Всяка мебел е със същия размер като старата и със същите редове, на които мечокът сяда
(props.json → seat), за да не се разместват сцените. Картината на статива (easel_1…9) се
взима от старите рисунки и се слага в новата рамка.
"""

import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
import props_layout as pl  # noqa: E402

O = (13, 18, 28, 255)
T = (0, 0, 0, 0)


def rgba(c, a=255):
    return (c[0], c[1], c[2], a)


def mix(a, b, k):
    return (int(round(a[0] + (b[0] - a[0]) * k)), int(round(a[1] + (b[1] - a[1]) * k)),
            int(round(a[2] + (b[2] - a[2]) * k)), 255)


def tone(hi, mid, lo):
    return (rgba(hi), rgba(mid), rgba(lo))


WOOD = tone((214, 132, 76), (166, 88, 52), (112, 52, 40))
DWOOD = tone((150, 82, 54), (112, 56, 42), (78, 36, 34))
LWOOD = tone((244, 196, 128), (220, 150, 86), (172, 100, 56))
RED = tone((240, 128, 116), (204, 76, 72), (140, 44, 52))
WINE = tone((190, 84, 84), (146, 52, 62), (98, 32, 46))
BLUE = tone((168, 198, 250), (110, 146, 226), (70, 96, 172))
CREAM = tone((255, 252, 240), (240, 230, 206), (196, 182, 152))
STEEL = tone((214, 220, 232), (170, 178, 198), (120, 128, 152))
DARK = tone((96, 104, 128), (64, 70, 92), (40, 44, 62))
HONEY = tone((255, 232, 150), (250, 200, 90), (206, 146, 52))
GREEN = tone((150, 210, 90), (95, 179, 36), (32, 96, 59))
FIRE = tone((255, 236, 140), (246, 160, 52), (210, 90, 40))
IRON = tone((206, 104, 80), (160, 66, 56), (108, 40, 44))
BERRY = tone((210, 150, 226), (160, 96, 196), (106, 58, 146))
PINK = tone((255, 200, 214), (240, 140, 168), (190, 86, 124))
TAN = tone((250, 222, 160), (226, 186, 118), (176, 132, 76))
SKY = tone((196, 230, 250), (150, 200, 236), (104, 154, 204))
COAL = (40, 34, 40, 255)
WHITE = (255, 255, 255, 255)


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.img = Image.new("RGBA", (w, h), T)

    def px(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h and c is not None:
            self.img.putpixel((int(x), int(y)), c)

    def get(self, x, y):
        return self.img.getpixel((x, y)) if 0 <= x < self.w and 0 <= y < self.h else T

    def hline(self, x0, x1, y, c):
        for x in range(x0, x1 + 1):
            self.px(x, y, c)

    def vline(self, x, y0, y1, c):
        for y in range(y0, y1 + 1):
            self.px(x, y, c)

    def region(self, pixels, t, outline=True):
        """Петно: контур по края, вътре – светло горе/вляво, тъмно долу/вдясно, средно другаде."""
        hi, mid, lo = t
        pixels = set(pixels)
        border = {p for p in pixels if any((p[0] + a, p[1] + b) not in pixels for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1)))} if outline else set()
        inner = pixels - border
        for (x, y) in pixels:
            if (x, y) in border:
                self.px(x, y, O)
                continue
            c = mid
            if (x, y - 1) not in inner or (x - 1, y) not in inner:
                c = hi
            if (x, y + 1) not in inner or (x + 1, y) not in inner:
                c = lo
            self.px(x, y, c)
        return inner

    def box(self, x0, y0, x1, y1, t, cut=0, outline=True):
        """Кутия; cut – отрязани ъгли (заобленост от 1–2 пиксела)."""
        pix = set()
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                dx = min(x - x0, x1 - x)
                dy = min(y - y0, y1 - y)
                if dx + dy < cut:
                    continue
                pix.add((x, y))
        inner = self.region(pix, t, outline)
        if t in (WOOD, DWOOD, LWOOD):
            self.grain(inner, t, x0 * 3 + y0)
        return inner

    def oval(self, cx, cy, rx, ry, t, outline=True):
        pix = {(x, y) for y in range(self.h) for x in range(self.w)
               if ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0}
        return self.region(pix, t, outline)

    def poly(self, rows, t, outline=True):
        """rows: {y: (x0, x1)} – по един отрязък на ред (трапец, покрив…)."""
        pix = {(x, y) for y, (a, b) in rows.items() for x in range(a, b + 1)}
        return self.region(pix, t, outline)

    def grain(self, inner, t, seed=0):
        """Жилки: къси по-тъмни черти по средния тон (дърво)."""
        for (x, y) in inner:
            if self.get(x, y) == t[1] and (x * 7 + y * 13 + seed) % 19 == 0:
                self.px(x, y, mix(t[1], t[2], 0.6))
                self.px(x + 1, y, mix(t[1], t[2], 0.6)) if (x + 1, y) in inner and self.get(x + 1, y) == t[1] else None

    def selout(self):
        """Черен контур само по силуета; вътрешните линии – тъмен тон на по-тъмния съсед."""
        src = self.img.copy()
        for y in range(self.h):
            for x in range(self.w):
                if src.getpixel((x, y)) != O:
                    continue
                nb = [src.getpixel((x + a, y + b)) if 0 <= x + a < self.w and 0 <= y + b < self.h else T
                      for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))]
                if any(n[3] == 0 for n in nb):
                    continue
                cols = [n for n in nb if n != O and n[3]]
                if cols:
                    c = min(cols, key=lambda c: sum(c[:3]))
                    self.img.putpixel((x, y), mix(c, O, 0.55))

    def save(self, name, out):
        self.selout()
        path = os.path.join(out, name + ".png") if out else pl.path(name)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        self.img.save(path)


# ───────────────────────── Легло (46×16, сяда на ред 9) ─────────────────────────

def bed(blanket=BLUE, frame=WOOD, pillow=CREAM):
    c = Canvas(46, 16)
    c.box(2, 11, 43, 14, frame)                       # рамка
    c.box(4, 7, 42, 12, CREAM)                        # дюшек
    inner = c.box(13, 7, 42, 12, blanket)             # одеяло
    for (x, y) in inner:                              # райета
        if (x - 13) % 6 in (4, 5) and c.get(x, y) == blanket[1]:
            c.px(x, y, mix(blanket[1], blanket[0], 0.5))
    c.vline(15, 8, 11, blanket[0])                    # прегънатият край
    c.vline(16, 8, 11, blanket[2])
    c.box(5, 3, 14, 8, pillow, cut=1)                 # възглавница
    c.hline(7, 11, 5, pillow[2])
    c.box(0, 0, 4, 15, frame, cut=1)                  # табла горе
    c.vline(2, 3, 10, frame[2])
    c.box(41, 6, 45, 15, frame, cut=1)                # табла долу
    c.vline(43, 8, 12, frame[2])
    return c


# ───────────────────────── Диван (48×24, сяда на ред 14) ─────────────────────────

def couch(fabric=RED, back=WINE, legs=DWOOD):
    c = Canvas(48, 24)
    c.box(4, 21, 7, 23, legs)
    c.box(40, 21, 43, 23, legs)
    c.box(5, 0, 42, 15, back, cut=2)                  # облегалка
    for x in (14, 24, 33):                            # капитонаж
        c.px(x, 6, back[2])
        c.px(x, 10, back[2])
    for x0, x1 in ((6, 24), (24, 41)):                # възглавници: издути
        c.box(x0, 13, x1, 21, fabric, cut=1)
        c.hline(x0 + 2, x0 + 6, 15, fabric[0])
        c.px(x0 + 2, 16, fabric[0])
        c.hline(x0 + 3, x1 - 3, 19, mix(fabric[1], fabric[2], 0.5))
    c.box(0, 8, 7, 21, fabric, cut=2)                 # облегалки за ръце
    c.box(40, 8, 47, 21, fabric, cut=2)
    for x in (2, 42):
        c.vline(x, 11, 13, fabric[0])
    return c


# ───────────────────────── Бюро (24×22) и компютър (20×22) ─────────────────────────

def desk(top=WOOD):
    c = Canvas(24, 22)
    c.box(1, 13, 3, 21, top)
    c.box(20, 13, 22, 21, top)
    c.box(3, 13, 20, 16, top)                         # чекмедже
    c.px(11, 15, O)
    c.box(0, 11, 23, 13, top)                         # плот
    # лаптоп
    c.box(4, 3, 13, 10, DARK)
    c.box(5, 4, 12, 9, SKY, outline=False)
    c.hline(6, 9, 6, WHITE)
    c.hline(6, 10, 8, rgba((110, 146, 226)))
    c.px(12, 4, WHITE)
    c.box(3, 9, 15, 11, STEEL)
    # лампичка
    c.box(17, 9, 21, 11, DARK)
    c.vline(19, 4, 8, O)
    c.box(16, 1, 21, 4, HONEY, cut=1)
    c.px(18, 5, rgba((255, 240, 170)))
    return c


def computer(top=WOOD):
    c = Canvas(20, 22)
    c.box(1, 15, 3, 21, top)
    c.box(16, 15, 18, 21, top)
    c.box(0, 13, 19, 15, top)
    c.box(8, 11, 11, 14, STEEL)                       # стойка
    c.box(2, 1, 17, 12, DARK, cut=1)                  # монитор
    c.box(4, 3, 15, 10, tone((40, 60, 96), (30, 44, 74), (22, 32, 56)), outline=False)
    for x, h, col in ((5, 2, GREEN), (8, 4, RED), (11, 3, HONEY), (13, 5, BLUE)):
        for y in range(10 - h, 10):
            c.px(x, y, col[1])
            c.px(x + 1, y, col[0] if y == 10 - h else col[1])
    return c


def stool(seat=WOOD):
    c = Canvas(16, 8)
    c.box(2, 3, 13, 5, seat)                          # напречна летва
    c.box(1, 1, 4, 7, seat)
    c.box(11, 1, 14, 7, seat)
    c.box(0, 0, 15, 2, seat, cut=1)                   # седалка
    return c


# ───────────────────────── Статив (18×36) с картината ─────────────────────────

def easel(painting=None):
    c = Canvas(18, 36)
    c.box(7, 0, 10, 34, DWOOD)                        # задният крак
    c.box(1, 19, 4, 35, WOOD)
    c.box(13, 19, 16, 35, WOOD)
    c.box(1, 27, 16, 29, WOOD)                        # напречна летва
    c.box(1, 3, 16, 19, CREAM)                        # платното
    if painting is not None:
        # Картината от старата рисунка (вътрешността на платното).
        for y in range(5, 18):
            for x in range(3, 15):
                p = painting.getpixel((x, y))
                if p[3]:
                    c.px(x, y, p)
    c.box(0, 18, 17, 20, WOOD)                        # поличка
    return c


# ───────────────────────── Гардероб (26×34) ─────────────────────────

def wardrobe(wood=WOOD):
    c = Canvas(26, 34)
    c.box(3, 1, 22, 33, wood)
    c.box(6, 3, 19, 30, tone((80, 54, 44), (60, 40, 34), (44, 30, 28)))  # вътрешността
    c.hline(7, 18, 5, STEEL[0])                       # лост
    for x, col in ((7, RED), (11, BLUE), (15, GREEN)):
        c.px(x + 1, 6, O)                             # закачалка
        c.box(x, 7, x + 3, 16, col, cut=1)
    c.box(7, 21, 18, 27, HONEY)                       # чекмедже
    c.hline(11, 14, 23, O)
    c.box(0, 2, 5, 32, LWOOD if wood is WOOD else wood)  # вратите
    c.box(20, 2, 25, 32, LWOOD if wood is WOOD else wood)
    c.px(4, 17, HONEY[1])
    c.px(21, 17, HONEY[1])
    c.box(1, 0, 24, 2, wood)                          # корниз
    return c


# ───────────────────────── Лампа (14×30) ─────────────────────────

def lamp(shade=HONEY, pole=DARK):
    c = Canvas(14, 30)
    c.box(5, 9, 8, 27, pole)
    c.box(2, 26, 11, 29, pole, cut=1)
    rows = {y: (4 - (y - 1) // 3, 9 + (y - 1) // 3) for y in range(1, 11)}
    inner = c.poly(rows, shade)
    for (x, y) in inner:                              # гънки
        if x % 2 == 0 and y > 2 and c.get(x, y) == shade[1]:
            c.px(x, y, mix(shade[1], shade[2], 0.45))
    c.hline(4, 9, 10, rgba((255, 244, 190)))           # светлина под абажура
    return c


# ───────────────────────── Радио (14×12) ─────────────────────────

def radio(body=RED):
    c = Canvas(14, 12)
    for i, (x, y) in enumerate(((13, 0), (12, 1), (11, 2), (10, 3))):
        c.px(x, y, O)
    c.box(0, 4, 13, 11, body, cut=1)
    c.oval(4, 8, 2.6, 2.6, DARK)
    c.px(4, 8, DARK[0])
    c.box(8, 6, 12, 8, HONEY)
    c.px(9, 10, O)
    c.px(11, 10, O)
    return c


# ───────────────────────── Печица (20×22, три кадъра) ─────────────────────────

def heater(state="on", body=IRON):
    c = Canvas(20, 22)
    c.box(3, 18, 5, 21, DARK)
    c.box(14, 18, 16, 21, DARK)
    c.box(1, 3, 18, 19, body, cut=1)
    c.box(3, 0, 16, 3, DARK)                          # капакът
    c.px(9, 0, O)
    c.box(4, 7, 15, 16, tone((60, 50, 56), (40, 34, 40), (28, 24, 30)))  # огнището
    if state == "off":
        for x, y in ((6, 14), (8, 13), (10, 14), (12, 13), (13, 14), (7, 12)):
            c.px(x, y, rgba((120, 112, 116)))
    else:
        b = state == "b"
        flame = [(9, 9), (10, 10), (8, 10), (9, 11), (10, 11), (11, 12), (7, 12), (8, 12), (9, 12), (10, 12)] if not b else \
                [(10, 9), (9, 10), (11, 10), (9, 11), (10, 11), (8, 12), (9, 12), (10, 12), (11, 12), (12, 13)]
        for x in range(5, 15):
            for y in range(12, 16):
                c.px(x, y, FIRE[2])
        for x, y in flame:
            c.px(x, y, FIRE[1])
        for x, y in ((9, 13), (10, 13), (8, 14), (10, 14) if not b else (11, 14)):
            c.px(x, y, FIRE[0])
    c.hline(6, 13, 5, body[0])                        # отдушник
    return c


# ───────────────────────── Хранилка (16×28) ─────────────────────────

def feeder(food=False, roof=RED, wood=WOOD):
    c = Canvas(16, 28)
    c.box(6, 15, 9, 26, wood)                         # стълбчето
    c.box(3, 25, 12, 27, wood, cut=1)
    c.box(2, 8, 4, 15, wood)                          # колонките
    c.box(11, 8, 13, 15, wood)
    if food:
        c.oval(7.5, 12.5, 5.0, 3.0, TAN)
        for x, y in ((5, 12), (8, 11), (10, 12), (7, 13)):
            c.px(x, y, TAN[2])
    c.box(0, 14, 15, 16, wood)                        # таблата
    rows = {y: (7 - y, 8 + y) for y in range(1, 8)}
    rows[8] = (0, 15)
    rows[0] = (7, 8)
    c.poly(rows, roof)
    return c


# ───────────────────────── Батут, кошница, игра, постелка ─────────────────────────

def trampoline(mat=BLUE, frame=RED):
    c = Canvas(28, 8)
    c.box(2, 4, 4, 7, DARK)
    c.box(12, 4, 15, 7, DARK)
    c.box(23, 4, 25, 7, DARK)
    c.box(0, 2, 27, 5, frame, cut=1)
    c.box(2, 1, 25, 3, DARK)
    c.hline(4, 23, 2, mat[1])
    return c


def yarn_basket():
    c = Canvas(16, 12)
    c.oval(3.5, 3.5, 3.2, 3.2, PINK)
    c.oval(11.5, 3.5, 3.2, 3.2, HONEY)
    c.oval(7.5, 2.8, 3.0, 3.0, BLUE)
    for x, y in ((3, 3), (7, 2), (11, 3)):
        c.px(x, y, O)
    c.vline(15, 6, 9, PINK[1])                        # провиснала нишка
    inner = c.box(0, 5, 14, 11, TAN)
    for (x, y) in inner:
        if (x + y) % 3 == 0:
            c.px(x, y, TAN[2])
    c.box(0, 5, 14, 6, LWOOD)                         # ръбът
    return c


def table_board(top=WOOD):
    c = Canvas(20, 16)
    c.box(1, 10, 3, 15, top)
    c.box(16, 10, 18, 15, top)
    c.box(0, 9, 19, 11, top)
    c.box(1, 6, 12, 9, GREEN)                         # дъската
    c.box(4, 3, 6, 6, RED, cut=1)                     # пионка
    c.box(13, 5, 18, 9, CREAM)                        # зарчето
    c.px(15, 7, O)
    return c


def mat(col=BERRY):
    c = Canvas(40, 3)
    c.box(0, 0, 39, 2, col)
    c.hline(1, 38, 1, col[1])
    for x in range(4, 37, 6):
        c.px(x, 1, col[0])
    return c


# ───────────────────────── Всичко ─────────────────────────

def all_items():
    items = {
        "bed": bed(),
        "couch": couch(),
        "desk": desk(),
        "computer": computer(),
        "stool": stool(),
        "easel": easel(),
        "wardrobe": wardrobe(),
        "lamp": lamp(),
        "radio": radio(),
        "heater": heater("on"),
        "heater_b": heater("b"),
        "heater_off": heater("off"),
        "feeder": feeder(),
        "feeder_food": feeder(food=True),
        "trampoline": trampoline(),
        "yarn_basket": yarn_basket(),
        "table_board": table_board(),
        "mat": mat(),
    }
    for i in range(1, 10):
        old = pl.path(f"easel_{i}")
        if os.path.exists(old):
            items[f"easel_{i}"] = easel(Image.open(old).convert("RGBA"))
    return items


def main(out=None):
    for name, c in all_items().items():
        c.save(name, out)
    print("Мебели:", len(all_items()))


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else None)
