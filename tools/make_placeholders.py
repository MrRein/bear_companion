"""Рисува временните (placeholder) спрайтове в assets/bear/ и assets/props/.

Всяка анимация е лента от кадри 32x32, един до друг отляво надясно.
Истинските рисунки просто заменят тези PNG файлове (може и с друг брой кадри).

    python tools/make_placeholders.py
"""

import json
import os

from PIL import Image, ImageDraw

import props_layout as pl  # къде стои всяка рисунка – по папки

SIZE = 32
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "bear")
PROPS = os.path.join(os.path.dirname(__file__), "..", "assets", "props")
LAYERS = os.path.join(os.path.dirname(__file__), "..", "assets", "clothes", "layers")

# Анимациите, които вече са нарисувани на ръка: скриптът не ги пипа (нито
# шаблоните им за дрехи; тях ги прави tools/auto_clothes.py или Тут).
HAND_DRAWN = {"idle", "walk", "sleep", "read", "read_sleep", "drag", "dance", "dice", "bounce", "eat", "fall",
              "happy", "sad", "think", "tired", "walk_tired", "sleep_peek",
              "cook", "eat_hold", "focus", "focus_tired", "juggle", "sit", "yawn",
              "cook_tired", "doing", "doing_tired", "doing_sit", "doing_sit_tired", "focus_write", "game", "game_tired",
              "write", "write_tired", "write_sit", "write_sit_tired", "dice_sit_boardgame", "yoga"}

# Нарисувани от Тут анимации, които нямат placeholder: име → (кадри в секунда, повтаря ли се).
EXTRA_DRAWN = {"sit_drinking": (10, True), "sit_drinking_sip": (10, False), "sit_drinking_blow": (10, False),
               "walk_gift": (8, True)}

# Цветовете са от мечока на Тут (assets/bear/idle.png).
OUTLINE = (68, 20, 8, 255)
FUR = (161, 87, 48, 255)
LIGHT = (194, 135, 83, 255)
MUZZLE = (249, 227, 180, 255)
BLACK = (0, 0, 0, 255)
PINK = (182, 101, 82, 255)
WHITE = (255, 255, 255, 255)
NUT = (150, 95, 40, 255)
PAPER = (245, 240, 225, 255)
RED = (220, 60, 60, 255)
COUCH = (170, 70, 70, 255)
COUCH_DARK = (120, 45, 50, 255)
COUCH_LIGHT = (200, 100, 95, 255)
BOOK = (60, 110, 170, 255)
WOOD = (90, 55, 30, 255)


def oval(d, box, fill, outline=OUTLINE):
    d.ellipse(box, fill=fill, outline=outline)


# Части от тялото за дрехите. Всеки кадър има и „карта“: кой пиксел е блуза,
# панталон, обувки или ръкавици. От нея стават сивите шаблони <anim>_shirt.png
# и т.н.; програмата ги оцветява в цвета на дрехата.
OTHER, SHIRT, PANTS, SHOES, GLOVES, ARM = 1, 2, 3, 4, 5, 6
SLOTS = {"shirt": SHIRT, "pants": PANTS, "shoes": SHOES, "gloves": GLOVES}


class Dual:
    """Рисува едновременно мечока и картата с частите (с цвета на текущата част)."""

    def __init__(self, img):
        self.img = img
        self.ids = Image.new("L", img.size, 0)
        self.d = ImageDraw.Draw(img)
        self.m = ImageDraw.Draw(self.ids)
        self.part = OTHER

    def __getattr__(self, name):
        real, ids = getattr(self.d, name), getattr(self.m, name)

        def both(*args, **kw):
            real(*args, **kw)
            kw = {k: (self.part if k in ("fill", "outline") and v is not None else v) for k, v in kw.items()}
            ids(*args, **kw)
        return both

    def relabel(self, src, dst, test):
        """Пикселите от част src, за които test(x, y) е вярно, стават част dst."""
        px = self.ids.load()
        for yy in range(self.ids.height):
            for xx in range(self.ids.width):
                if px[xx, yy] == src and test(xx, yy):
                    px[xx, yy] = dst


def part(d, which):
    if isinstance(d, Dual):
        d.part = which


def bear(eyes="open", mouth="none", arms="down", legs="stand", bob=0, ears="up", extra=None):
    """Мечок отпред. bob мести всичко без стъпалата нагоре/надолу."""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = Dual(img)
    y = bob

    # крака
    if legs == "stand":
        feet = [(10, 26, 14, 31), (17, 26, 21, 31)]
    elif legs == "step1":
        feet = [(9, 25, 13, 30), (17, 26, 21, 31)]
    elif legs == "step2":
        feet = [(10, 26, 14, 31), (18, 25, 22, 30)]
    elif legs == "dangle":
        feet = [(10, 26 + y, 13, 31), (18, 26 + y, 21, 31)]
    else:  # sit: краката напред, тялото ниско
        feet = [(6, 27, 12, 31), (19, 27, 25, 31)]
    part(d, SHOES)
    for f in feet:
        oval(d, f, FUR)

    # уши
    part(d, OTHER)
    if ears == "up":
        oval(d, (5, 1 + y, 11, 7 + y), FUR)
        oval(d, (20, 1 + y, 26, 7 + y), FUR)
        d.rectangle((7, 3 + y, 8, 4 + y), fill=MUZZLE)
        d.rectangle((22, 3 + y, 23, 4 + y), fill=MUZZLE)
    else:  # клепнали
        oval(d, (3, 4 + y, 9, 9 + y), FUR)
        oval(d, (22, 4 + y, 28, 9 + y), FUR)

    # тяло и коремче: горе блуза, долу панталон
    part(d, SHIRT)
    oval(d, (8, 15 + y, 23, 29 + y), FUR)
    oval(d, (12, 19 + y, 19, 27 + y), LIGHT, outline=None)
    d.relabel(SHIRT, PANTS, lambda xx, yy: yy >= 25 + y)

    # ръце
    left, right = {
        "down": ((5, 17, 9, 25), (22, 17, 26, 25)),
        "up": ((4, 8, 8, 17), (23, 8, 27, 17)),
        "left_up": ((4, 8, 8, 17), (22, 17, 26, 25)),
        "right_up": ((5, 17, 9, 25), (23, 8, 27, 17)),
        "front": ((10, 18, 15, 23), (16, 18, 21, 23)),
    }[arms]
    for a in (left, right):
        part(d, ARM)
        box = (a[0], a[1] + y, a[2], a[3] + y)
        oval(d, box, FUR)
        # Лапите (3 реда в края на ръката) са ръкавици, останалото е ръкав.
        if arms == "front":
            paw = lambda xx, yy: True
        elif box[1] < 15 + y:   # вдигната ръка: лапата е горе
            paw = lambda xx, yy, b=box: yy <= b[1] + 2
        else:
            paw = lambda xx, yy, b=box: yy >= b[3] - 2
        d.relabel(ARM, GLOVES, paw)
        d.relabel(ARM, SHIRT, lambda xx, yy: True)

    # глава и муцуна
    part(d, OTHER)
    img.head = (16, 5 + y)   # точката за шапката: средата на долния ѝ ръб
    img.face = (16, 10 + y)  # очилата: средата им е на реда на очите
    img.neck = (16, 19 + y)  # шалът: горният му ред е под брадичката
    oval(d, (6, 2 + y, 25, 19 + y), FUR)
    oval(d, (12, 11 + y, 19, 17 + y), MUZZLE, outline=None)
    d.rectangle((15, 12 + y, 16, 13 + y), fill=BLACK)

    # очи
    ey = 9 + y
    if eyes == "open":
        d.rectangle((10, ey, 11, ey + 1), fill=BLACK)
        d.rectangle((20, ey, 21, ey + 1), fill=BLACK)
        d.point((10, ey), fill=WHITE)
        d.point((20, ey), fill=WHITE)
    elif eyes == "closed":
        d.line((10, ey + 1, 11, ey + 1), fill=BLACK)
        d.line((20, ey + 1, 21, ey + 1), fill=BLACK)
    elif eyes == "happy":  # ^ ^
        for x in (10, 20):
            d.point((x, ey + 1), fill=BLACK)
            d.point((x + 1, ey), fill=BLACK)
            d.point((x + 2, ey + 1), fill=BLACK)
    elif eyes == "sad":
        for x in (10, 20):
            d.line((x, ey + 1, x + 1, ey + 1), fill=BLACK)
            d.point((x if x == 20 else x + 1, ey), fill=BLACK)
    elif eyes == "dizzy":  # x x
        for x in (10, 20):
            d.point((x, ey), fill=BLACK)
            d.point((x + 2, ey), fill=BLACK)
            d.point((x + 1, ey + 1), fill=BLACK)
            d.point((x, ey + 2), fill=BLACK)
            d.point((x + 2, ey + 2), fill=BLACK)
    # бузки
    d.point((8, 12 + y), fill=PINK)
    d.point((23, 12 + y), fill=PINK)

    # уста
    if mouth == "smile":
        d.line((14, 15 + y, 17, 15 + y), fill=BLACK)
        d.point((13, 14 + y), fill=BLACK)
        d.point((18, 14 + y), fill=BLACK)
    elif mouth == "open":
        d.rectangle((14, 14 + y, 17, 16 + y), fill=BLACK)
        d.rectangle((15, 16 + y, 16, 16 + y), fill=PINK)
    elif mouth == "big":
        d.rectangle((13, 14 + y, 18, 17 + y), fill=BLACK)
        d.rectangle((14, 17 + y, 17, 17 + y), fill=PINK)
    elif mouth == "sad":
        d.line((14, 16 + y, 17, 16 + y), fill=BLACK)
        d.point((13, 17 + y), fill=BLACK)
        d.point((18, 17 + y), fill=BLACK)

    if extra:
        extra(d, y)
    img.ids = d.ids
    return img


def sleeping(z):
    """Мечок, свит на кълбо, с Z-та над него."""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = Dual(img)
    breathe = z % 2
    part(d, SHIRT)
    oval(d, (3, 18 - breathe, 28, 31), FUR)
    part(d, OTHER)
    oval(d, (4, 15, 10, 20), FUR)  # ухо
    oval(d, (3, 19, 15, 30), FUR)  # глава
    img.head = (10, 21)
    oval(d, (4, 24, 10, 29), MUZZLE, outline=None)
    d.rectangle((4, 25, 5, 26), fill=BLACK)
    d.line((8, 22, 10, 22), fill=BLACK)  # затворено око
    part(d, SHOES)
    oval(d, (20, 25, 27, 31), FUR)  # краче
    part(d, OTHER)
    # мехурче от носа
    if z in (1, 2):
        r = z + 1
        d.ellipse((1 - r + 1, 27 - r, 1 + r, 27 + r - 1), outline=(160, 200, 240, 255))
    # Z z z
    zs = [(15, 10), (20, 5), (25, 1)][: z + 1]
    for i, (x, yy) in enumerate(zs):
        s = 3 if i < 2 else 4
        d.line((x, yy, x + s, yy), fill=WHITE)
        d.line((x + s, yy, x, yy + s), fill=WHITE)
        d.line((x, yy + s, x + s, yy + s), fill=WHITE)
    img.ids = d.ids
    return img


def nut(d, y, bite=False):
    oval(d, (14, 18 + y, 18, 22 + y), NUT)
    if bite:
        d.rectangle((17, 18 + y, 18, 19 + y), fill=(0, 0, 0, 0))


def notebook(d, y):
    d.rectangle((11, 20 + y, 20, 26 + y), fill=PAPER, outline=OUTLINE)
    d.line((13, 22 + y, 18, 22 + y), fill=(120, 120, 140, 255))


def pencil(dx):
    def f(d, y):
        notebook(d, y)
        d.line((19 + dx, 17 + y, 22 + dx, 21 + y), fill=(240, 200, 60, 255))
        d.point((19 + dx, 17 + y), fill=BLACK)
    return f


def die(x, yy, pips):
    def f(d, y):
        d.rectangle((x, yy, x + 4, yy + 4), fill=WHITE, outline=OUTLINE)
        for px, py in pips:
            d.point((x + px, yy + py), fill=RED)
    return f


def book(open_pages=True, page=0):
    """Книжка в ръцете на седнал мечок."""
    def f(d, y):
        d.rectangle((10, 19 + y, 21, 25 + y), fill=BOOK, outline=OUTLINE)
        if open_pages:
            d.rectangle((11, 20 + y, 15, 24 + y), fill=PAPER)
            d.rectangle((16, 20 + y, 20, 24 + y), fill=PAPER)
            d.line((12, 21 + y, 14, 21 + y), fill=(120, 120, 140, 255))
            d.line((17, 21 + y, 19, 21 + y), fill=(120, 120, 140, 255))
            if page:  # страницата се обръща
                d.rectangle((16 - page, 19 + y, 16, 24 + y), fill=WHITE, outline=(120, 120, 140, 255))
    return f


def reading_sleep(z):
    """Заспал на дивана с книжката на корема."""
    def f(d, y):
        d.rectangle((11, 21 + y, 20, 25 + y), fill=BOOK, outline=OUTLINE)
        zs = [(23, 6), (26, 2)][: z + 1]
        for x, yy in zs:
            d.line((x, yy, x + 3, yy), fill=WHITE)
            d.line((x + 3, yy, x, yy + 3), fill=WHITE)
            d.line((x, yy + 3, x + 3, yy + 3), fill=WHITE)
    return bear(eyes="closed", arms="front", legs="sit", bob=2, extra=f)


def couch(main=None, dark=None, light=None, pattern=None, accent=None, legs=None):
    """Диван отпред, 48x24. Седалката е на 10 пиксела от земята. Цветовете са скинът."""
    main, dark, light = main or COUCH, dark or COUCH_DARK, light or COUCH_LIGHT
    legs = legs or WOOD
    img = Image.new("RGBA", (48, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((4, 21, 6, 23), fill=legs)  # крачета
    d.rectangle((41, 21, 43, 23), fill=legs)
    d.rounded_rectangle((4, 0, 43, 15), 4, fill=dark, outline=OUTLINE)  # облегалка
    if pattern == "dots":
        for x in range(8, 41, 5):
            for yy in (3, 8):
                d.point((x + (yy // 5) * 2, yy), fill=accent)
    elif pattern == "stripes":
        for x in range(8, 41, 4):
            d.line((x, 2, x, 12), fill=accent)
    elif pattern == "buttons":   # капитоне (кожа)
        for x in range(9, 41, 7):
            for yy in (4, 9):
                d.point((x, yy), fill=accent)
    d.rounded_rectangle((5, 12, 42, 21), 2, fill=main, outline=OUTLINE)  # седалка
    d.line((24, 13, 24, 20), fill=OUTLINE)
    d.line((7, 13, 22, 13), fill=light)
    d.line((26, 13, 40, 13), fill=light)
    d.rounded_rectangle((0, 8, 7, 21), 3, fill=main, outline=OUTLINE)  # облегалки за ръце
    d.rounded_rectangle((40, 8, 47, 21), 3, fill=main, outline=OUTLINE)
    if pattern == "dots":
        for x in range(9, 40, 6):
            d.point((x, 17), fill=accent)
    return img


# Скиновете на дивана: id → (основен, тъмен, светъл, шарка, акцент, крачета)
COUCH_SKINS = {
    "velvet": ((70, 90, 170, 255), (45, 60, 130, 255), (110, 130, 210, 255), None, None, None),
    "forest": ((90, 150, 80, 255), (60, 110, 60, 255), (130, 190, 110, 255), None, None, None),
    "dots": ((240, 150, 170, 255), (215, 110, 140, 255), (255, 200, 210, 255), "dots", (255, 255, 255, 255), None),
    "leather": ((120, 70, 40, 255), (90, 50, 30, 255), (160, 100, 60, 255), "buttons", (60, 35, 20, 255), (40, 30, 25, 255)),
    "mint": ((150, 215, 190, 255), (110, 180, 160, 255), (200, 240, 225, 255), "stripes", (255, 255, 255, 255), None),
    "honey": ((250, 200, 80, 255), (220, 160, 50, 255), (255, 230, 140, 255), None, None, None),
}


def bed(blanket, dark, pillow=(255, 255, 255, 255), pattern=None, accent=None, frame=None):
    """Легло отстрани, 46 x 16: табла отляво, дюшек, одеяло. Лежи на 7 пиксела от земята."""
    frame = frame or WOOD
    img = Image.new("RGBA", (46, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 0, 3, 15), fill=frame, outline=OUTLINE)        # табла
    d.rectangle((42, 6, 45, 15), fill=frame, outline=OUTLINE)      # долна табла
    d.rectangle((2, 12, 43, 13), fill=frame, outline=OUTLINE)      # рамка
    d.rectangle((3, 7, 42, 11), fill=(245, 240, 230, 255), outline=OUTLINE)  # дюшек
    d.rectangle((4, 4, 12, 8), fill=pillow, outline=OUTLINE)       # възглавница
    d.rectangle((13, 7, 42, 11), fill=blanket, outline=OUTLINE)    # одеяло
    d.line((14, 8, 41, 8), fill=dark)
    if pattern == "stars":
        for x, yy in [(17, 10), (23, 9), (29, 10), (35, 9), (40, 10)]:
            d.point((x, yy), fill=accent)
    elif pattern == "checks":
        for x in range(14, 42, 4):
            d.rectangle((x, 9, x + 1, 10), fill=accent)
    d.rectangle((3, 14, 4, 15), fill=OUTLINE)
    d.rectangle((41, 14, 42, 15), fill=OUTLINE)
    return img


# Скиновете на леглото: id → (одеяло, тъмно, възглавница, шарка, акцент, рамка)
BED_SKINS = {
    "classic": ((110, 140, 220, 255), (80, 105, 180, 255), (255, 255, 255, 255), None, None, None),
    "stars": ((50, 60, 120, 255), (35, 40, 90, 255), (255, 240, 200, 255), "stars", (255, 220, 100, 255), None),
    "pink": ((245, 160, 185, 255), (220, 120, 150, 255), (255, 240, 245, 255), None, None, (240, 240, 240, 255)),
    "forest": ((100, 160, 90, 255), (70, 120, 65, 255), (240, 230, 200, 255), "checks", (60, 100, 55, 255), None),
    "honey": ((250, 205, 90, 255), (220, 165, 60, 255), (255, 250, 230, 255), "checks", (240, 150, 60, 255), None),
}


def controller(blink):
    def f(d, y):
        d.rectangle((11, 20 + y, 20, 23 + y), fill=(60, 60, 70, 255), outline=OUTLINE)
        d.point((13, 21 + y), fill=RED if blink else (120, 120, 130, 255))
        d.point((18, 21 + y), fill=(90, 160, 220, 255))
    return f


def look_left(d, y):
    # зеници наляво (гледа към компютъра)
    for x in (10, 20):
        d.rectangle((x, 9 + y, x + 1, 10 + y), fill=MUZZLE)
        d.rectangle((x - 1, 9 + y, x, 10 + y), fill=BLACK)


def brush(up):
    def f(d, y):
        if up:
            d.line((25, 6 + y, 28, 2 + y), fill=WOOD)
            d.rectangle((28, 0 + y, 29, 2 + y), fill=(90, 140, 210, 255))
        else:
            d.line((25, 16 + y, 29, 13 + y), fill=WOOD)
            d.rectangle((29, 12 + y, 30, 13 + y), fill=(220, 90, 80, 255))
    return f


def cup_in_hands(up):
    def f(d, y):
        cy = 14 if up else 19
        d.rectangle((13, cy + y, 18, cy + 4 + y), fill=WHITE, outline=OUTLINE)
        d.line((14, cy + 1 + y, 17, cy + 1 + y), fill=(200, 140, 60, 255))
    return f


def headphones(note):
    def f(d, y):
        d.arc((6, 0 + y, 25, 14 + y), 180, 360, fill=OUTLINE, width=2)
        d.rectangle((4, 7 + y, 7, 12 + y), fill=RED, outline=OUTLINE)
        d.rectangle((24, 7 + y, 27, 12 + y), fill=RED, outline=OUTLINE)
        if note:
            d.line((28, 0, 28, 4), fill=OUTLINE)
            d.rectangle((26, 3, 27, 4), fill=OUTLINE)
    return f


def watering(pour):
    def f(d, y):
        d.rectangle((21, 18 + y, 27, 23 + y), fill=(90, 140, 210, 255), outline=OUTLINE)
        d.line((27, 19 + y, 31, 16 + y if pour else 19 + y), fill=OUTLINE)
        if pour:
            d.point((31, 19), fill=(120, 170, 240, 255))
            d.point((30, 22), fill=(120, 170, 240, 255))
    return f


def spoon(tx, ty):
    """Дървена лъжица от лапите до (tx, ty) (тенджерата е отдясно, извън кадъра)."""
    def f(d, y):
        d.line((19, 20 + y, tx, ty + y), fill=WOOD)
        d.rectangle((tx - 1, ty + y, tx, ty + 1 + y), fill=(150, 95, 50, 255))
    return f


def prop(w, h, fn):
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    fn(ImageDraw.Draw(img))
    return img


def stool(d):
    d.rectangle((1, 0, 14, 2), fill=WOOD, outline=OUTLINE)
    d.line((3, 3, 2, 7), fill=OUTLINE)
    d.line((12, 3, 13, 7), fill=OUTLINE)
    d.line((3, 5, 12, 5), fill=OUTLINE)


def computer(d):
    d.rectangle((0, 14, 19, 15), fill=WOOD, outline=OUTLINE)   # плот
    d.line((1, 16, 1, 21), fill=OUTLINE)
    d.line((18, 16, 18, 21), fill=OUTLINE)
    d.rectangle((2, 2, 17, 12), fill=(200, 200, 210, 255), outline=OUTLINE)  # монитор
    d.rectangle((4, 4, 15, 10), fill=(30, 40, 60, 255))
    d.rectangle((5, 8, 7, 9), fill=(120, 200, 120, 255))   # героят в игричката
    d.rectangle((10, 6, 11, 9), fill=(220, 90, 80, 255))   # враг
    d.point((13, 5), fill=(255, 209, 102, 255))            # монетка
    d.rectangle((8, 12, 11, 13), fill=(160, 160, 170, 255))


def easel(d):
    # Висок статив: платното е горе (на нивото на лапите на седналия мечок), краката дълги.
    d.line((4, 4, 1, 35), fill=WOOD)
    d.line((13, 4, 16, 35), fill=WOOD)
    d.line((9, 0, 9, 35), fill=WOOD)
    d.line((3, 28, 14, 28), fill=WOOD)                              # напречна летва
    d.rectangle((1, 3, 16, 17), fill=WHITE, outline=OUTLINE)
    d.ellipse((3, 5, 8, 10), fill=(255, 209, 102, 255))            # слънце
    d.polygon([(2, 16), (7, 10), (11, 16)], fill=(120, 180, 80, 255))  # хълм
    d.rectangle((10, 11, 14, 15), fill=(90, 140, 210, 255))
    d.line((0, 18, 17, 18), fill=OUTLINE)                           # поличка


def table_tea(d):
    d.rectangle((0, 8, 15, 9), fill=WOOD, outline=OUTLINE)
    d.line((2, 10, 2, 15), fill=OUTLINE)
    d.line((13, 10, 13, 15), fill=OUTLINE)
    d.ellipse((2, 2, 9, 8), fill=(220, 120, 120, 255), outline=OUTLINE)  # чайник
    d.line((9, 4, 11, 3), fill=OUTLINE)
    d.rectangle((11, 5, 13, 7), fill=WHITE, outline=OUTLINE)


def table_board(d):
    d.rectangle((0, 8, 19, 9), fill=WOOD, outline=OUTLINE)
    d.line((2, 10, 2, 15), fill=OUTLINE)
    d.line((17, 10, 17, 15), fill=OUTLINE)
    d.rectangle((2, 5, 12, 7), fill=(120, 180, 80, 255), outline=OUTLINE)  # дъска
    d.rectangle((4, 3, 5, 4), fill=RED)   # пионка
    d.rectangle((14, 4, 17, 7), fill=WHITE, outline=OUTLINE)  # карти
    d.point((15, 5), fill=RED)


def mat(d):
    d.rectangle((0, 0, 39, 2), fill=(170, 120, 210, 255), outline=OUTLINE)


def plant(d):
    d.rectangle((2, 12, 9, 17), fill=(200, 110, 70, 255), outline=OUTLINE)
    d.line((5, 11, 5, 4), fill=(70, 130, 60, 255))
    d.ellipse((0, 4, 5, 8), fill=(120, 180, 80, 255), outline=OUTLINE)
    d.ellipse((6, 2, 11, 6), fill=(120, 180, 80, 255), outline=OUTLINE)
    d.ellipse((3, 0, 7, 3), fill=(240, 140, 150, 255), outline=OUTLINE)  # цвят


def radio(d):
    d.rectangle((0, 3, 13, 11), fill=(200, 90, 70, 255), outline=OUTLINE)
    d.ellipse((2, 5, 7, 10), fill=(60, 60, 70, 255))
    d.rectangle((9, 5, 11, 6), fill=(255, 209, 102, 255))
    d.line((10, 2, 13, 0), fill=OUTLINE)


def nut_small(d):
    d.ellipse((0, 1, 4, 5), fill=NUT, outline=OUTLINE)
    d.line((1, 0, 3, 0), fill=OUTLINE)


def serious(d, y):
    """Очила и вежди: сериозен работен вид."""
    ey = 9 + y
    for x in (9, 19):
        d.rectangle((x, ey - 1, x + 3, ey + 2), outline=OUTLINE)
    d.line((13, ey, 18, ey), fill=OUTLINE)
    d.line((9, ey - 3, 12, ey - 2), fill=OUTLINE)    # намръщени вежди
    d.line((19, ey - 2, 22, ey - 3), fill=OUTLINE)


def typing(left_down):
    def f(d, y):
        serious(d, y)
        # ръцете на клавиатурата (отдясно е бюрото)
        d.rectangle((12, 22 + y, 21, 24 + y), fill=(80, 80, 90, 255), outline=OUTLINE)
        d.point((14 if left_down else 18, 22 + y), fill=WHITE)
    return f


def thinking(d, y):
    serious(d, y)
    oval(d, (17, 13 + y, 21, 17 + y), FUR)   # лапа на брадичката
    d.point((26, 3), fill=OUTLINE)            # мисъл: три точки
    d.point((28, 1), fill=OUTLINE)
    d.point((30, 0), fill=OUTLINE)


def writing(dx):
    def f(d, y):
        serious(d, y)
        d.rectangle((11, 20 + y, 20, 25 + y), fill=PAPER, outline=OUTLINE)
        d.line((13, 22 + y, 13 + 2 + dx, 22 + y), fill=(120, 120, 140, 255))
        d.line((19 + dx, 17 + y, 21 + dx, 21 + y), fill=(240, 200, 60, 255))
    return f


def droopy(d, y):
    """Полузатворени очи и торбички: уморен."""
    ey = 9 + y
    for x in (10, 20):
        d.rectangle((x, ey, x + 1, ey + 1), fill=FUR)
        d.line((x, ey + 1, x + 1, ey + 1), fill=BLACK)
        d.point((x, ey + 2), fill=(110, 70, 40, 255))
        d.point((x + 1, ey + 2), fill=(110, 70, 40, 255))


def trampoline(d):
    d.rectangle((1, 2, 26, 4), fill=(90, 140, 210, 255), outline=OUTLINE)   # пружиниращото
    d.line((0, 3, 27, 3), fill=OUTLINE)
    for x in (3, 13, 24):
        d.line((x, 5, x, 7), fill=OUTLINE)                                  # крачета
    d.line((2, 1, 25, 1), fill=(255, 209, 102, 255))


def desk(d):
    d.rectangle((0, 12, 23, 13), fill=WOOD, outline=OUTLINE)    # плот
    d.line((1, 14, 1, 21), fill=OUTLINE)
    d.line((22, 14, 22, 21), fill=OUTLINE)
    d.polygon([(2, 11), (4, 4), (13, 4), (13, 11)], fill=(150, 150, 160, 255), outline=OUTLINE)  # лаптоп
    d.rectangle((5, 5, 12, 9), fill=(170, 210, 235, 255))
    d.line((6, 7, 10, 7), fill=OUTLINE)
    d.line((17, 11, 17, 5), fill=OUTLINE)                       # лампа
    d.polygon([(15, 5), (20, 5), (18, 2)], fill=(255, 209, 102, 255), outline=OUTLINE)
    d.ellipse((19, 9, 22, 11), fill=WHITE, outline=OUTLINE)     # чашка


ANIMS = {
    # име: (кадри, кадри в секунда, повтаря ли се)
    "idle": ([bear(), bear(bob=1), bear(), bear(eyes="closed")], 3, True),
    "walk": ([bear(legs="step1"), bear(bob=-1), bear(legs="step2"), bear(bob=-1)], 8, True),
    "sleep": ([sleeping(i) for i in range(3)] + [sleeping(2)], 2, True),
    "yawn": ([bear(eyes="closed", mouth="open"), bear(eyes="closed", mouth="big", arms="up"),
              bear(eyes="closed", mouth="big", arms="up"), bear(eyes="closed", mouth="open")], 3, False),
    "eat": ([bear(arms="front", mouth="open", extra=nut), bear(arms="front", mouth="none", extra=nut),
             bear(arms="front", mouth="open", extra=lambda d, y: nut(d, y, True)), bear(eyes="happy", mouth="smile")], 3, True),
    "happy": ([bear(eyes="happy", mouth="smile", arms="up", bob=-3, legs="dangle"),
               bear(eyes="happy", mouth="smile", arms="up", bob=-1),
               bear(eyes="happy", mouth="smile")], 6, False),
    "dance": ([bear(eyes="happy", mouth="smile", arms="left_up", legs="step1"),
               bear(eyes="happy", mouth="open", arms="up", bob=-1),
               bear(eyes="happy", mouth="smile", arms="right_up", legs="step2"),
               bear(eyes="happy", mouth="open", arms="up", bob=-1)], 6, True),
    "dice": ([bear(arms="right_up", extra=die(24, 4, [(2, 2)])),
              bear(arms="down", mouth="open", extra=die(26, 14, [(1, 1), (3, 3)])),
              bear(eyes="happy", mouth="smile", extra=die(25, 26, [(1, 1), (2, 2), (3, 3)]))], 5, False),
    "drag": ([bear(arms="up", legs="dangle", mouth="open"), bear(arms="up", legs="dangle", bob=1, mouth="open")], 4, True),
    "fall": ([bear(legs="sit", eyes="dizzy", mouth="open", bob=3), bear(legs="sit", eyes="dizzy", mouth="open", bob=2),
              bear(legs="sit", eyes="happy", mouth="smile", bob=3)], 3, False),
    "sad": ([bear(eyes="sad", mouth="sad", ears="down"), bear(eyes="sad", mouth="sad", ears="down", bob=1)], 2, True),
    "read": ([bear(arms="front", legs="sit", bob=2, extra=book()), bear(arms="front", legs="sit", bob=2, extra=book()),
              bear(arms="front", legs="sit", bob=2, eyes="closed", extra=book()),
              bear(arms="front", legs="sit", bob=2, extra=book()), bear(arms="front", legs="sit", bob=2, extra=book()),
              bear(arms="front", legs="sit", bob=2, extra=book(page=2)),
              bear(arms="front", legs="sit", bob=2, extra=book(page=4)),
              bear(arms="front", legs="sit", bob=2, mouth="smile", extra=book())], 2, True),
    "read_sleep": ([reading_sleep(0), reading_sleep(1)], 1, True),
    # седнал (столчето го вдига) и играе на компютъра отляво
    "game": ([bear(arms="front", legs="sit", bob=2, extra=lambda d, y: (look_left(d, y), controller(True)(d, y))),
              bear(arms="front", legs="sit", bob=2, mouth="open", extra=lambda d, y: (look_left(d, y), controller(False)(d, y))),
              bear(arms="front", legs="sit", bob=1, extra=lambda d, y: (look_left(d, y), controller(True)(d, y))),
              bear(arms="front", legs="sit", bob=2, eyes="happy", mouth="smile", extra=controller(False))], 4, True),
    "paint": ([bear(arms="right_up", legs="sit", bob=2, extra=brush(True)),
               bear(arms="down", legs="sit", bob=2, extra=brush(False)),
               bear(arms="right_up", legs="sit", bob=2, mouth="smile", extra=brush(True)),
               bear(arms="down", legs="sit", bob=2, eyes="closed", extra=brush(False))], 3, True),
    "yoga": ([bear(arms="up", eyes="closed"), bear(arms="up", eyes="closed", bob=-1),
              bear(arms="left_up", eyes="closed", legs="step1"), bear(arms="right_up", eyes="closed", legs="step2")], 1.5, True),
    "water": ([bear(arms="front", extra=watering(False)), bear(arms="front", extra=watering(True)),
               bear(arms="front", eyes="happy", extra=watering(True))], 2, True),
    "music": ([bear(eyes="closed", mouth="smile", extra=headphones(True)), bear(eyes="closed", mouth="smile", bob=1, extra=headphones(False)),
               bear(eyes="closed", mouth="open", legs="step1", extra=headphones(True)), bear(eyes="closed", mouth="smile", bob=1, extra=headphones(False))], 4, True),
    # работният режим: седнал на столче, бюрото е отдясно
    "focus": ([bear(arms="front", legs="sit", bob=2, mouth="none", extra=typing(True)),
               bear(arms="front", legs="sit", bob=2, mouth="none", extra=typing(False)),
               bear(arms="front", legs="sit", bob=2, mouth="none", extra=typing(True)),
               bear(arms="front", legs="sit", bob=1, mouth="none", extra=typing(False))], 6, True),
    "think": ([bear(legs="sit", bob=2, extra=thinking), bear(legs="sit", bob=2, eyes="closed", extra=thinking)], 1, True),
    "write": ([bear(arms="front", legs="sit", bob=2, extra=writing(0)), bear(arms="front", legs="sit", bob=2, extra=writing(2)),
               bear(arms="front", legs="sit", bob=2, extra=writing(1))], 3, True),
    # уморен: клепнали уши, полузатворени очи, отпуснат (всичко върви и по-бавно)
    "tired": ([bear(ears="down", eyes="none", bob=1, extra=droopy), bear(ears="down", eyes="none", bob=2, extra=droopy),
               bear(ears="down", eyes="closed", bob=2, mouth="open"), bear(ears="down", eyes="none", bob=1, extra=droopy)], 2, True),
    "walk_tired": ([bear(ears="down", eyes="none", legs="step1", bob=1, extra=droopy), bear(ears="down", eyes="none", bob=2, extra=droopy),
                    bear(ears="down", eyes="none", legs="step2", bob=1, extra=droopy), bear(ears="down", eyes="none", bob=2, extra=droopy)], 5, True),
    "focus_tired": ([bear(arms="front", legs="sit", bob=2, eyes="none", extra=lambda d, y: (droopy(d, y), serious(d, y))),
                     bear(arms="front", legs="sit", bob=3, eyes="closed", extra=serious),
                     bear(arms="front", legs="sit", bob=4, eyes="closed", mouth="open", extra=serious),
                     bear(arms="front", legs="sit", bob=2, eyes="none", extra=lambda d, y: (droopy(d, y), serious(d, y)))], 1.5, True),
    "bounce": ([bear(arms="up", legs="dangle", eyes="happy", mouth="open"), bear(arms="up", legs="dangle", eyes="happy", mouth="smile", bob=-1),
                bear(arms="left_up", legs="dangle", eyes="happy", mouth="open"), bear(arms="right_up", legs="dangle", eyes="happy", mouth="smile")], 6, True),
    # просто си седи (когато дълго няма какво да прави)
    "sit": ([bear(legs="sit", bob=2), bear(legs="sit", bob=2), bear(legs="sit", bob=2, eyes="closed"),
             bear(legs="sit", bob=2), bear(legs="sit", bob=3), bear(legs="sit", bob=2, mouth="smile")], 2, True),
    # готви: бърка с лъжица в тенджерата отдясно (уредът е до него)
    "cook": ([bear(arms="front", mouth="smile", extra=spoon(30, 22)),
              bear(arms="front", mouth="none", extra=spoon(31, 24)),
              bear(arms="front", eyes="happy", mouth="smile", extra=spoon(30, 25)),
              bear(arms="front", mouth="open", extra=spoon(29, 23))], 6, True),
    "juggle": ([bear(arms="left_up", mouth="open"), bear(arms="right_up", mouth="smile"),
                bear(arms="left_up", eyes="happy"), bear(arms="right_up", mouth="open")], 6, True),
}

def food_hazelnut():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    oval(d, (2, 4, 13, 15), NUT)
    d.rectangle((4, 2, 11, 6), fill=(110, 70, 30, 255), outline=OUTLINE)  # шапчица
    d.line((7, 0, 8, 2), fill=OUTLINE)
    d.point((5, 9), fill=(200, 140, 80, 255))
    d.point((6, 8), fill=(200, 140, 80, 255))
    return img


def food_berries():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line((8, 1, 8, 5), fill=(70, 130, 60, 255))
    d.ellipse((8, 1, 12, 4), fill=(90, 170, 70, 255))  # листо
    for x, y in [(2, 6), (8, 5), (5, 10)]:
        oval(d, (x, y, x + 6, y + 5), (70, 90, 190, 255))
        d.point((x + 2, y + 1), fill=(170, 190, 255, 255))
    return img


def cup(drink, steam=True):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if steam:
        d.line((6, 0, 5, 3), fill=(220, 220, 220, 255))
        d.line((9, 1, 10, 4), fill=(220, 220, 220, 255))
    d.rectangle((2, 6, 11, 14), fill=WHITE, outline=OUTLINE)
    d.rectangle((3, 7, 10, 8), fill=drink)
    d.ellipse((10, 8, 14, 12), outline=OUTLINE)
    d.line((1, 15, 13, 15), fill=OUTLINE)
    return img


def food_popcorn():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(2, 7), (13, 7), (11, 15), (4, 15)], fill=RED, outline=OUTLINE)
    d.line((6, 8, 6, 14), fill=WHITE)
    d.line((9, 8, 9, 14), fill=WHITE)
    for x, y in [(3, 4), (6, 2), (9, 3), (12, 5), (5, 6), (10, 6), (7, 5)]:
        oval(d, (x - 1, y - 1, x + 2, y + 2), (255, 245, 210, 255), outline=(200, 170, 90, 255))
    return img


def food_meatballs():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    oval(d, (0, 9, 15, 15), WHITE)  # чиния
    for x in (2, 6, 10):
        oval(d, (x, 6, x + 4, 11), (130, 70, 40, 255))
    d.line((5, 1, 5, 4), fill=(220, 220, 220, 255))  # пара
    d.line((10, 0, 10, 3), fill=(220, 220, 220, 255))
    return img


def food_potatoes():
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 10, 15, 14), fill=(150, 150, 160, 255), outline=OUTLINE)  # тава
    for x, y in [(1, 6), (5, 7), (9, 6), (4, 3), (8, 3)]:
        oval(d, (x, y, x + 5, y + 4), (230, 180, 70, 255), outline=(150, 100, 30, 255))
    return img


def butterfly(wing=(250, 150, 200, 255), low=(180, 140, 250, 255)):
    img = Image.new("RGBA", (9, 7), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    oval(d, (0, 0, 3, 3), wing)
    oval(d, (5, 0, 8, 3), wing)
    oval(d, (1, 3, 3, 6), low)
    oval(d, (5, 3, 7, 6), low)
    d.line((4, 1, 4, 6), fill=OUTLINE)
    return img


GREY = (150, 150, 160, 255)
DARK = (70, 70, 80, 255)


def icon(draw_fn):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(img))
    return img


def shop_pot(d):
    d.rectangle((2, 6, 13, 14), fill=GREY, outline=OUTLINE)
    d.line((0, 7, 2, 7), fill=OUTLINE)
    d.line((13, 7, 15, 7), fill=OUTLINE)
    d.rectangle((1, 4, 14, 6), fill=DARK, outline=OUTLINE)
    d.rectangle((6, 2, 9, 4), fill=DARK, outline=OUTLINE)


def shop_pan(d):
    oval(d, (0, 7, 10, 13), DARK)
    d.rectangle((10, 9, 15, 11), fill=WOOD, outline=OUTLINE)
    oval(d, (3, 8, 7, 11), (250, 230, 120, 255), outline=None)  # яйце
    d.point((5, 9), fill=(240, 160, 40, 255))


def shop_oven(d):
    d.rectangle((1, 2, 14, 15), fill=(200, 200, 210, 255), outline=OUTLINE)
    d.rectangle((3, 7, 12, 13), fill=(60, 40, 30, 255), outline=OUTLINE)
    d.rectangle((4, 10, 11, 12), fill=(240, 120, 40, 255))  # огън
    for x in (4, 7, 10):
        d.point((x, 4), fill=OUTLINE)


def shop_kettle(d):
    oval(d, (2, 5, 12, 15), (90, 160, 200, 255))
    d.line((12, 8, 15, 5), fill=OUTLINE)
    d.arc((4, 1, 10, 7), 180, 360, fill=OUTLINE)
    d.line((5, 2, 4, 0), fill=(220, 220, 220, 255))


def shop_coffee(d):
    d.rectangle((2, 1, 13, 15), fill=(60, 60, 70, 255), outline=OUTLINE)
    d.rectangle((4, 3, 11, 5), fill=(120, 200, 120, 255))  # екранче
    d.rectangle((6, 6, 9, 8), fill=GREY)  # чучур
    d.rectangle((5, 10, 10, 14), fill=WHITE, outline=OUTLINE)  # чашка
    d.line((6, 11, 9, 11), fill=(90, 55, 30, 255))


def shop_headphones(d):
    d.arc((2, 1, 13, 12), 180, 360, fill=OUTLINE, width=2)
    d.rectangle((1, 7, 4, 13), fill=RED, outline=OUTLINE)
    d.rectangle((11, 7, 14, 13), fill=RED, outline=OUTLINE)


def shop_bed(d):
    d.rectangle((0, 3, 2, 15), fill=WOOD, outline=OUTLINE)
    d.rectangle((2, 9, 15, 12), fill=(110, 140, 220, 255), outline=OUTLINE)
    d.rectangle((3, 7, 6, 9), fill=WHITE, outline=OUTLINE)  # възглавница
    d.line((14, 12, 14, 15), fill=OUTLINE)


def shop_fridge(d):
    d.rectangle((3, 0, 12, 15), fill=(230, 240, 250, 255), outline=OUTLINE)
    d.line((3, 6, 12, 6), fill=OUTLINE)
    d.line((10, 2, 10, 4), fill=OUTLINE)
    d.line((10, 8, 10, 11), fill=OUTLINE)


def shop_cookbook(d):
    d.rectangle((2, 2, 13, 14), fill=(200, 60, 60, 255), outline=OUTLINE)
    d.line((4, 2, 4, 14), fill=OUTLINE)
    d.rectangle((6, 5, 11, 8), fill=PAPER)
    d.point((8, 6), fill=(230, 180, 70, 255))


def wardrobe(d):
    """Гардероб с отворени врати и дрешки вътре, 26 x 34."""
    d.rectangle((5, 0, 20, 33), fill=(110, 70, 35, 255), outline=OUTLINE)   # корпус
    d.rectangle((7, 2, 18, 29), fill=(60, 38, 22, 255))                     # вътре
    d.line((7, 4, 18, 4), fill=GREY)                                        # лост
    for x, c in [(8, RED), (12, (90, 140, 210, 255)), (15, (120, 180, 80, 255))]:
        d.line((x + 1, 4, x + 1, 5), fill=GREY)
        d.rectangle((x, 6, x + 2, 13), fill=c, outline=OUTLINE)             # закачени дрешки
    d.rectangle((8, 22, 17, 27), fill=(255, 209, 102, 255), outline=OUTLINE)  # кутия с шапки
    d.rectangle((0, 1, 5, 31), fill=(150, 95, 50, 255), outline=OUTLINE)    # лява врата
    d.rectangle((20, 1, 25, 31), fill=(150, 95, 50, 255), outline=OUTLINE)  # дясна врата
    d.point((4, 16), fill=(255, 209, 102, 255))
    d.point((21, 16), fill=(255, 209, 102, 255))
    d.rectangle((6, 31, 7, 33), fill=OUTLINE)
    d.rectangle((18, 31, 19, 33), fill=OUTLINE)


# Уредите, в които мечокът готви пред Тут (стоят отдясно до него, на земята).
# Всеки има два кадъра (докато готви) и „open“ (капакът изхвръква).
STEEL = (190, 190, 200, 255)
FLAME = (240, 120, 40, 255)
FLAME2 = (255, 209, 102, 255)


def stove(d, frame):
    """Котлон 20 x 9 най-отдолу на рисунката (височина 24)."""
    d.rectangle((1, 15, 18, 23), fill=(200, 200, 210, 255), outline=OUTLINE)
    d.rectangle((3, 17, 16, 21), fill=(60, 60, 70, 255))
    d.point((5, 22), fill=OUTLINE)
    d.point((14, 22), fill=OUTLINE)
    for x in range(4, 16, 3):   # пламъчета
        d.point((x + frame % 2, 14), fill=FLAME if (x + frame) % 2 else FLAME2)


def cook_pot(frame):
    def f(d):
        stove(d, frame)
        d.rectangle((3, 7, 16, 13), fill=STEEL, outline=OUTLINE)
        d.line((1, 8, 3, 8), fill=OUTLINE)
        d.line((16, 8, 18, 8), fill=OUTLINE)
        if frame == 2:   # капакът хвръква, пуканки изскачат
            d.rectangle((2, 0, 13, 2), fill=DARK, outline=OUTLINE)
            d.rectangle((6, 0, 9, 0), fill=DARK)
            for x, yy in [(5, 5), (9, 4), (12, 6)]:
                oval(d, (x - 1, yy - 1, x + 1, yy + 1), (255, 245, 210, 255), outline=(200, 170, 90, 255))
        else:
            lift = frame   # капакът подскача
            d.rectangle((2, 5 - lift, 17, 7 - lift), fill=DARK, outline=OUTLINE)
            d.rectangle((8, 3 - lift, 11, 5 - lift), fill=DARK, outline=OUTLINE)
    return f


def cook_pan(frame):
    def f(d):
        stove(d, frame)
        d.rectangle((2, 11, 15, 13), fill=DARK, outline=OUTLINE)
        d.line((15, 12, 19, 10), fill=WOOD, width=2)
        for x in (4, 8, 11):
            oval(d, (x, 8 - (frame == 2) * 3 - (x == 8 and frame == 1), x + 3, 11 - (frame == 2) * 3), (130, 70, 40, 255))
        for x in range(3, 15, 4):   # цвърчи
            d.point((x + frame, 6 - frame), fill=(220, 220, 220, 255))
    return f


def cook_oven(frame):
    def f(d):
        d.rectangle((1, 2, 20, 23), fill=(200, 200, 210, 255), outline=OUTLINE)
        for x in (4, 8, 12):
            d.point((x, 4), fill=OUTLINE)
        if frame == 2:   # вратата е отворена, тавата излиза
            d.rectangle((3, 7, 18, 19), fill=(60, 40, 30, 255), outline=OUTLINE)
            d.rectangle((0, 20, 21, 22), fill=(170, 170, 180, 255), outline=OUTLINE)
            for x in (3, 8, 13):
                oval(d, (x, 16, x + 4, 19), (230, 180, 70, 255), outline=(150, 100, 30, 255))
        else:
            d.rectangle((3, 7, 18, 19), fill=(60, 40, 30, 255), outline=OUTLINE)
            d.rectangle((5, 9, 16, 17), fill=(240, 150, 60, 255) if frame else (220, 110, 40, 255))
            for x in (6, 10, 13):
                oval(d, (x, 13, x + 3, 15), (230, 180, 70, 255), outline=(150, 100, 30, 255))
            d.line((5, 6, 16, 6), fill=OUTLINE)
        d.line((6, 0, 6 + frame, 1), fill=(220, 220, 220, 255))   # мирише
        d.line((14, 1, 15 - frame, 0), fill=(220, 220, 220, 255))
    return f


def cook_coffee(frame):
    def f(d):
        d.rectangle((3, 4, 16, 23), fill=(60, 60, 70, 255), outline=OUTLINE)
        d.rectangle((5, 6, 14, 8), fill=(120, 200, 120, 255) if frame != 1 else (200, 240, 200, 255))
        d.rectangle((8, 10, 11, 12), fill=GREY)
        d.rectangle((6, 17, 13, 22), fill=WHITE, outline=OUTLINE)
        if frame == 1:
            d.line((9, 13, 9, 16), fill=(90, 55, 30, 255))
        if frame == 2:
            d.line((7, 18, 12, 18), fill=(90, 55, 30, 255))
            d.line((8, 14, 7, 11), fill=(220, 220, 220, 255))
            d.line((11, 15, 12, 12), fill=(220, 220, 220, 255))
    return f


def cook_kettle(frame):
    def f(d):
        stove(d, frame)
        oval(d, (4, 5, 15, 14), (90, 160, 200, 255))
        d.line((15, 8, 19, 5 - (frame == 2)), fill=OUTLINE)
        d.arc((6, 1, 13, 7), 180, 360, fill=OUTLINE)
        if frame:   # пара от чучура
            d.line((19, 3, 18 + frame, 0), fill=(220, 220, 220, 255))
            d.line((17, 2, 16 + frame, 0), fill=(220, 220, 220, 255))
    return f


def cook_jug(juice, light):
    """Стъклена каничка със сок; лъжичката бърка (два кадъра), после сокът е готов."""
    def make(frame):
        def f(d):
            glass = (215, 238, 248, 255)
            d.rectangle((4, 8, 15, 23), fill=glass, outline=OUTLINE)
            d.line((3, 7, 5, 9), fill=OUTLINE)                       # чучурче
            d.rectangle((16, 11, 18, 18), outline=OUTLINE)           # дръжка
            d.rectangle((5, 12, 14, 22), fill=juice)
            d.line((5, 12, 14, 12), fill=light)
            d.line((6, 14, 6, 20), fill=(255, 255, 255, 160))         # отблясък
            if frame == 2:   # готово: капчици над каничката
                for x, y in [(7, 4), (11, 2), (13, 5)]:
                    d.point((x, y), fill=juice)
                d.point((9, 6), fill=light)
            else:            # лъжичката се люшка и прави вихрушка
                top = 6 if frame == 0 else 13
                d.line((top, 1, 10, 17), fill=WOOD)
                d.rectangle((9, 17, 11, 18), fill=WOOD)
                for x in ((8, 12) if frame == 0 else (7, 11)):
                    d.point((x, 15 + frame), fill=light)
        return f
    return make


def heater(frame):
    """Печица: малка метална печка с решетка; когато е включена, решетката свети."""
    def f(d):
        d.rectangle((2, 3, 17, 18), fill=(150, 60, 50, 255), outline=OUTLINE)       # кутията
        d.rectangle((4, 1, 15, 3), fill=(120, 45, 40, 255), outline=OUTLINE)        # капак
        d.rectangle((4, 6, 15, 15), fill=(60, 40, 35, 255), outline=OUTLINE)        # прозорче
        if frame > 0:
            glow = [(255, 150, 60, 255), (255, 200, 90, 255)]
            for y in range(7, 15):
                for x in range(5, 15):
                    if (x + y + frame) % 3 == 0:
                        d.point((x, y), fill=glow[(x + frame) % 2])
                    elif (x * 3 + y) % 5 == 0:
                        d.point((x, y), fill=(220, 90, 40, 255))
            d.point((7 + frame, 0), fill=(255, 210, 120, 255))                       # искрица
        for x in range(5, 15, 3):
            d.line((x, 6, x, 15), fill=OUTLINE)                                      # решетката
        d.rectangle((3, 19, 5, 21), fill=OUTLINE)                                    # крачета
        d.rectangle((14, 19, 16, 21), fill=OUTLINE)
    return f


def bee(frame):
    """Пчеличка (гост): жълто-черно телце, крилца нагоре/надолу."""
    def f(d):
        wing = (230, 245, 255, 220)
        if frame == 0:
            d.ellipse((3, 0, 7, 4), fill=wing, outline=OUTLINE)
        else:
            d.ellipse((2, 3, 6, 6), fill=wing, outline=OUTLINE)
        d.ellipse((1, 4, 10, 10), fill=(250, 200, 40, 255), outline=OUTLINE)
        for x in (4, 7):
            d.line((x, 5, x, 9), fill=OUTLINE)
        d.point((9, 6), fill=OUTLINE)                                                 # око
        d.point((0, 7), fill=OUTLINE)                                                 # жило
    return f


def squirrel(frame):
    """Катеричка (гост): кафява, с голяма пухкава опашка; два кадъра тичане."""
    def f(d):
        fur, light, tail = (190, 100, 45, 255), (235, 170, 110, 255), (205, 120, 60, 255)
        d.ellipse((0, 0, 8, 11), fill=tail, outline=OUTLINE)                          # опашката
        d.point((3, 3), fill=light); d.point((5, 6), fill=light)
        d.ellipse((5, 6, 14, 13), fill=fur, outline=OUTLINE)                          # тяло
        d.ellipse((11, 3, 17, 9), fill=fur, outline=OUTLINE)                          # глава
        d.point((13, 2), fill=OUTLINE); d.point((15, 2), fill=OUTLINE)               # ушички
        d.point((15, 5), fill=OUTLINE)                                                # око
        d.point((17, 7), fill=OUTLINE)                                                # нос
        d.ellipse((8, 9, 11, 12), fill=light)                                         # коремче
        if frame == 0:
            d.line((7, 13, 6, 15), fill=OUTLINE); d.line((13, 13, 14, 15), fill=OUTLINE)
        else:
            d.line((8, 13, 9, 15), fill=OUTLINE); d.line((12, 13, 11, 15), fill=OUTLINE)
    return f


def parcel(opened, w=18, h=16):
    """Доставка: кашон с тиксо (затворен) или с отворени капаци; w x h – малък, среден, голям."""
    def f(d):
        card, dark, tape = (205, 160, 105, 255), (165, 120, 70, 255), (235, 215, 160, 255)
        top = max(3, h // 4)
        d.rectangle((1, top, w - 2, h - 1), fill=card, outline=OUTLINE)
        d.line((2, (top + h) // 2, w - 3, (top + h) // 2), fill=dark)
        mid = w // 2
        if opened:
            d.polygon([(1, top), (-1, 0), (mid - 2, 0), (mid - 1, top)], fill=dark, outline=OUTLINE)
            d.polygon([(w - 2, top), (w, 0), (mid + 1, 0), (mid, top)], fill=dark, outline=OUTLINE)
        else:
            d.rectangle((1, top - 2, w - 2, top), fill=card, outline=OUTLINE)
            d.rectangle((mid - 2, top - 2, mid + 1, h - 1), fill=tape)
            d.line((mid - 2, top - 2, mid - 2, h - 1), fill=dark); d.line((mid + 1, top - 2, mid + 1, h - 1), fill=dark)
        d.point((3, h - 4), fill=OUTLINE); d.point((4, h - 4), fill=OUTLINE)            # „етикет“
    return f


COOKERS = {"pot": cook_pot, "pan": cook_pan, "oven": cook_oven, "coffee_machine": cook_coffee, "kettle": cook_kettle,
           "jug_orange": cook_jug((250, 160, 40, 255), (255, 210, 120, 255)),
           "jug_berry": cook_jug((150, 60, 140, 255), (200, 120, 190, 255))}


# Шапките: 16 широки, долният ред е там, където шапката сяда на главата
# (точката от <anim>_head.png). Рисунката е assets/clothes/hat_<id>.png.
def hat_beanie(d):
    d.ellipse((6, 0, 9, 3), fill=WHITE, outline=OUTLINE)               # помпон
    d.pieslice((2, 2, 13, 14), 180, 360, fill=RED, outline=OUTLINE)
    d.rectangle((1, 7, 14, 9), fill=WHITE, outline=OUTLINE)            # подгъв
    for x in (3, 6, 9, 12):
        d.point((x, 5), fill=(250, 120, 110, 255))


def hat_beret(d):
    d.line((8, 2, 8, 3), fill=OUTLINE)                                 # опашчица
    d.ellipse((1, 3, 15, 9), fill=(60, 60, 120, 255), outline=OUTLINE)
    d.line((4, 5, 8, 4), fill=(110, 110, 180, 255))
    d.rectangle((4, 8, 12, 9), fill=(45, 45, 90, 255), outline=OUTLINE)


def hat_cap(d):
    d.pieslice((3, 1, 13, 13), 180, 360, fill=(90, 140, 210, 255), outline=OUTLINE)
    d.point((8, 1), fill=OUTLINE)
    d.line((5, 4, 7, 3), fill=(150, 190, 240, 255))
    d.rectangle((0, 7, 15, 9), fill=(60, 100, 170, 255), outline=OUTLINE)  # козирка


def hat_party(d):
    d.polygon([(8, 0), (3, 9), (12, 9)], fill=(240, 140, 150, 255), outline=OUTLINE)
    d.line((5, 6, 11, 6), fill=(255, 209, 102, 255))
    d.line((7, 3, 9, 3), fill=(90, 140, 210, 255))
    d.ellipse((7, 0, 9, 1), fill=(255, 209, 102, 255))


def hat_chef(d):
    for box in [(2, 0, 8, 6), (7, 0, 13, 6), (4, 1, 11, 7)]:
        d.ellipse(box, fill=WHITE, outline=OUTLINE)
    d.ellipse((4, 1, 11, 7), fill=WHITE)
    d.rectangle((3, 5, 12, 9), fill=WHITE, outline=OUTLINE)
    d.line((4, 6, 11, 6), fill=(220, 220, 225, 255))


def hat_crown(d):
    gold, dark = (255, 209, 102, 255), (200, 150, 40, 255)
    d.polygon([(2, 9), (2, 2), (5, 5), (8, 0), (11, 5), (14, 2), (14, 9)], fill=gold, outline=OUTLINE)
    d.line((3, 8, 13, 8), fill=dark)
    d.point((8, 5), fill=RED)
    d.point((5, 7), fill=(90, 140, 210, 255))
    d.point((11, 7), fill=(120, 180, 80, 255))


def hat_nightcap(d):
    """Нощна шапка: синя, клюмнала надясно, с бял помпон и звездички."""
    blue, dark = (70, 90, 170, 255), (45, 60, 130, 255)
    d.polygon([(2, 9), (4, 3), (9, 1), (14, 4), (15, 7), (13, 6), (12, 9)], fill=blue, outline=OUTLINE)
    d.ellipse((13, 5, 15, 8), fill=WHITE, outline=OUTLINE)       # помпон
    d.rectangle((1, 8, 13, 9), fill=WHITE, outline=OUTLINE)       # подгъв
    d.point((6, 4), fill=(255, 220, 100, 255))
    d.point((9, 6), fill=(255, 220, 100, 255))
    d.line((5, 7, 10, 7), fill=dark)


HATS = {"beanie": hat_beanie, "beret": hat_beret, "cap": hat_cap, "party": hat_party, "chef": hat_chef, "crown": hat_crown, "nightcap": hat_nightcap}


PROP_LIST = {
    # име: (рисунка, на колко пиксела от земята е седалката)
    "couch": (couch(), 10),
    "butterfly": (butterfly(), 0),
    "butterfly_yellow": (butterfly((250, 215, 80, 255), (240, 150, 60, 255)), 0),
    "butterfly_blue": (butterfly((110, 170, 240, 255), (70, 110, 200, 255)), 0),
    "butterfly_orange": (butterfly((245, 150, 60, 255), (60, 50, 50, 255)), 0),
    "butterfly_white": (butterfly((250, 250, 245, 255), (210, 215, 225, 255)), 0),
    "butterfly_green": (butterfly((150, 215, 120, 255), (90, 160, 90, 255)), 0),
    "stool": (prop(16, 8, stool), 7),
    "trampoline": (prop(28, 8, trampoline), 4),
    "desk": (prop(24, 22, desk), 0),
    "computer": (prop(20, 22, computer), 0),
    "easel": (prop(18, 36, easel), 0),
    "table_tea": (prop(16, 16, table_tea), 0),
    "table_board": (prop(20, 16, table_board), 0),
    "mat": (prop(40, 3, mat), 2),
    "plant": (prop(12, 18, plant), 0),
    "radio": (prop(14, 12, radio), 0),
    "nut": (prop(5, 6, nut_small), 0),
    "food_berries": (food_berries(), 0),
    "food_coffee": (cup((90, 55, 30, 255)), 0),
    "food_tea": (cup((200, 140, 60, 255)), 0),
    "shop_pot": (icon(shop_pot), 0),
    "shop_pan": (icon(shop_pan), 0),
    "shop_oven": (icon(shop_oven), 0),
    "shop_kettle": (icon(shop_kettle), 0),
    "shop_coffee_machine": (icon(shop_coffee), 0),
    "shop_headphones": (icon(shop_headphones), 0),
    "shop_bed": (icon(shop_bed), 0),
    "shop_fridge": (icon(shop_fridge), 0),
    "shop_cookbook": (icon(shop_cookbook), 0),
    "food_popcorn": (food_popcorn(), 0),
    "food_meatballs": (food_meatballs(), 0),
    "food_potatoes": (food_potatoes(), 0),
    "wardrobe": (prop(26, 34, wardrobe), 0),
}
def couch_shape(img, shape, main, dark, accent):
    """Друг вид диван (същата височина на седалката – 10 пиксела от земята)."""
    d = ImageDraw.Draw(img)
    if shape == "camel":          # облегалка с гърбица в средата
        d.rounded_rectangle((14, 0, 33, 6), 3, fill=dark, outline=OUTLINE)
        d.rectangle((15, 5, 32, 7), fill=dark)
    elif shape == "pillows":      # възглавнички на облегалката, високи крачета
        for x in (9, 20, 31):
            d.rounded_rectangle((x, 6, x + 8, 12), 2, fill=accent or (240, 230, 200, 255), outline=OUTLINE)
    elif shape == "loveseat":     # малко и кръгличко: ръцете са топчета
        img2 = Image.new("RGBA", img.size, (0, 0, 0, 0))
        img2.paste(img.crop((0, 0, 48, 24)).resize((40, 24), Image.NEAREST), (4, 0))
        img.paste(img2, (0, 0))
        d.ellipse((2, 7, 10, 15), fill=main, outline=OUTLINE)
        d.ellipse((38, 7, 46, 15), fill=main, outline=OUTLINE)
    elif shape == "bench":        # дървена рамка като пейка
        d.rectangle((0, 7, 47, 8), fill=WOOD, outline=OUTLINE)
        d.rectangle((0, 7, 2, 23), fill=WOOD, outline=OUTLINE)
        d.rectangle((45, 7, 47, 23), fill=WOOD, outline=OUTLINE)
    elif shape == "blob":         # меко като торба: без отделни ръце
        d.rounded_rectangle((0, 3, 47, 21), 9, fill=main, outline=OUTLINE)
        d.rounded_rectangle((6, 12, 41, 20), 4, fill=(255, 230, 140, 255), outline=OUTLINE)
        d.line((10, 7, 20, 7), fill=(255, 240, 180, 255))
    elif shape == "chesterfield":  # ръцете колкото облегалката, навити
        d.rounded_rectangle((0, 1, 8, 21), 4, fill=main, outline=OUTLINE)
        d.rounded_rectangle((39, 1, 47, 21), 4, fill=main, outline=OUTLINE)
        for y in (5, 10, 15):
            d.point((4, y), fill=accent); d.point((43, y), fill=accent)
    return img


COUCH_SHAPES = {"velvet": "camel", "forest": "pillows", "dots": "loveseat", "mint": "bench", "honey": "blob", "leather": "chesterfield"}


def bed_shape(img, shape, blanket, dark, accent):
    """Друго легло (дюшекът е на същата височина – 7 пиксела от земята)."""
    d = ImageDraw.Draw(img)
    if shape == "canopy":         # с колонки и балдахин
        d.rectangle((0, 0, 1, 15), fill=(240, 240, 240, 255), outline=OUTLINE)
        d.rectangle((44, 0, 45, 15), fill=(240, 240, 240, 255), outline=OUTLINE)
        d.rectangle((0, 0, 45, 1), fill=(250, 200, 215, 255), outline=OUTLINE)
        for x in range(3, 44, 4):
            d.point((x, 2), fill=(250, 200, 215, 255))
    elif shape == "logs":         # от трупчета
        for x0 in (0, 42):
            for y in range(0 if x0 == 0 else 6, 16, 3):
                d.ellipse((x0, y, x0 + 3, y + 2), fill=(140, 90, 50, 255), outline=OUTLINE)
    elif shape == "futon":        # ниско, без крачета, дебел дюшек
        d.rectangle((0, 0, 45, 6), fill=(0, 0, 0, 0))
        d.rectangle((0, 12, 45, 15), fill=(200, 160, 100, 255), outline=OUTLINE)
        d.rectangle((2, 4, 10, 8), fill=(255, 250, 230, 255), outline=OUTLINE)
    elif shape == "metal":        # метална рамка с кръгла табла
        d.rectangle((0, 0, 3, 15), fill=(0, 0, 0, 0))
        d.arc((0, 0, 10, 14), 180, 270, fill=(60, 60, 80, 255), width=2)
        d.line((0, 7, 0, 15), fill=(60, 60, 80, 255), width=2)
        for x in (3, 6):
            d.line((x, 2, x, 7), fill=(60, 60, 80, 255))
    return img


BED_SHAPES = {"pink": "canopy", "forest": "logs", "honey": "futon", "stars": "metal"}

for _skin, _args in COUCH_SKINS.items():
    PROP_LIST[f"couch_{_skin}"] = (couch_shape(couch(*_args), COUCH_SHAPES.get(_skin), _args[0], _args[1], _args[4]), 10)
PROP_LIST["bed"] = (bed(*BED_SKINS["classic"]), 7)
for _skin, _args in BED_SKINS.items():
    PROP_LIST[f"bed_{_skin}"] = (bed_shape(bed(*_args), BED_SHAPES.get(_skin), _args[0], _args[1], _args[4]), 7)
for _i, _name in enumerate(["heater_off", "heater", "heater_b"]):
    PROP_LIST[_name] = (prop(20, 22, heater(_i)), 0)

# ── Нови ястия (16x16) и уреди (22x24, кадри "", "_b", "_open") ──

def _img16(fn):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    fn(ImageDraw.Draw(img))
    return img


def _glass(color, top=None, straw=False, ice=False):
    def f(d):
        d.polygon([(3, 3), (12, 3), (11, 15), (4, 15)], fill=(235, 245, 255, 255), outline=OUTLINE)
        d.polygon([(4, 6), (11, 6), (10, 14), (5, 14)], fill=color)
        if top:
            d.rectangle((4, 4, 11, 6), fill=top)
        if ice:
            d.rectangle((5, 7, 7, 9), fill=(230, 245, 255, 255)); d.rectangle((8, 9, 10, 11), fill=(230, 245, 255, 255))
        if straw:
            d.line((9, 0, 8, 6), fill=RED)
    return _img16(f)


def _mug(color, foam=None, steam=True):
    def f(d):
        if steam:
            d.line((6, 0, 5, 3), fill=(220, 220, 220, 255)); d.line((9, 1, 10, 4), fill=(220, 220, 220, 255))
        d.rectangle((2, 6, 11, 14), fill=WHITE, outline=OUTLINE)
        d.rectangle((3, 7, 10, 8), fill=color)
        if foam:
            d.rectangle((3, 6, 10, 7), fill=foam)
        d.ellipse((10, 8, 14, 12), outline=OUTLINE)
        d.line((1, 15, 13, 15), fill=OUTLINE)
    return _img16(f)


def _small_cup(color):
    def f(d):
        d.line((7, 2, 6, 5), fill=(220, 220, 220, 255))
        d.rectangle((4, 8, 10, 13), fill=WHITE, outline=OUTLINE)
        d.rectangle((5, 9, 9, 9), fill=color)
        d.ellipse((10, 9, 12, 11), outline=OUTLINE)
        d.line((2, 14, 13, 14), fill=OUTLINE)
    return _img16(f)


def _bowl(color, bits=None):
    def f(d):
        d.line((6, 1, 5, 4), fill=(220, 220, 220, 255)); d.line((10, 1, 11, 4), fill=(220, 220, 220, 255))
        d.pieslice((1, 2, 14, 15), 0, 180, fill=(240, 240, 245, 255), outline=OUTLINE)
        d.rectangle((2, 8, 13, 9), fill=color)
        for x, y, c in (bits or []):
            d.point((x, y), fill=c)
    return _img16(f)


def _plate(draw):
    def f(d):
        d.ellipse((0, 10, 15, 15), fill=(240, 240, 245, 255), outline=OUTLINE)
        draw(d)
    return _img16(f)


def _cone(scoop, spot):
    def f(d):
        d.polygon([(4, 8), (11, 8), (8, 15)], fill=(220, 170, 90, 255), outline=OUTLINE)
        d.line((5, 10, 9, 10), fill=(180, 130, 60, 255))
        oval(d, (3, 2, 12, 9), scoop)
        d.point((6, 4), fill=spot); d.point((9, 6), fill=spot)
    return _img16(f)


FOOD_ART = {
    "lemonade": _glass((250, 230, 90, 255), straw=True),
    "soup": _bowl((230, 150, 60, 255), [(5, 8, (90, 170, 70, 255)), (9, 8, (240, 210, 120, 255))]),
    "corn": _img16(lambda d: (d.ellipse((5, 1, 11, 14), fill=(250, 210, 60, 255), outline=OUTLINE),
                             [d.point((x, y), fill=(220, 170, 30, 255)) for y in range(3, 13, 2) for x in (7, 9)],
                             d.polygon([(4, 9), (8, 15), (3, 15)], fill=(110, 180, 80, 255), outline=OUTLINE))),
    "cocoa": _mug((110, 60, 30, 255), foam=(250, 240, 240, 255)),
    "linden_tea": _mug((230, 200, 90, 255)),
    "eggs": _plate(lambda d: (d.ellipse((2, 7, 8, 12), fill=WHITE, outline=OUTLINE), d.ellipse((4, 8, 6, 10), fill=(250, 190, 40, 255)),
                              d.ellipse((8, 6, 14, 11), fill=WHITE, outline=OUTLINE), d.ellipse((10, 7, 12, 9), fill=(250, 190, 40, 255)))),
    "pancakes": _plate(lambda d: ([d.ellipse((2, 9 - i * 2, 13, 12 - i * 2), fill=(230, 180, 100, 255), outline=OUTLINE) for i in range(3)],
                                  d.line((6, 4, 6, 7), fill=(240, 170, 40, 255)), d.ellipse((5, 3, 10, 5), fill=(240, 170, 40, 255)))),
    "espresso": _small_cup((70, 40, 20, 255)),
    "latte": _glass((200, 160, 110, 255), top=(250, 245, 235, 255)),
    "mocha": _mug((90, 50, 30, 255), foam=(250, 245, 235, 255)),
    "cappuccino": _mug((150, 100, 60, 255), foam=(250, 245, 235, 255)),
    "iced_coffee": _glass((140, 90, 50, 255), ice=True, straw=True),
    "cookies": _img16(lambda d: [(oval(d, (x, y, x + 6, y + 6), (210, 160, 90, 255)), d.point((x + 2, y + 2), fill=(90, 50, 30, 255)),
                                  d.point((x + 4, y + 4), fill=(90, 50, 30, 255))) for x, y in [(1, 8), (8, 9), (4, 3)]]),
    "muffins": _img16(lambda d: (d.polygon([(4, 9), (12, 9), (11, 15), (5, 15)], fill=(230, 120, 140, 255), outline=OUTLINE),
                                 oval(d, (2, 3, 14, 10), (220, 170, 100, 255)), d.point((6, 6), fill=(70, 90, 190, 255)),
                                 d.point((9, 5), fill=(70, 90, 190, 255)), d.point((10, 7), fill=(70, 90, 190, 255)))),
    "pizza": _img16(lambda d: (d.polygon([(1, 3), (14, 3), (8, 15)], fill=(250, 210, 90, 255), outline=OUTLINE),
                               d.line((1, 3, 14, 3), fill=(200, 140, 70, 255), width=2), d.ellipse((5, 5, 7, 7), fill=RED),
                               d.ellipse((9, 6, 11, 8), fill=RED), d.point((8, 10), fill=RED))),
    "smoothie": _glass((230, 110, 160, 255), straw=True),
    "toast": _plate(lambda d: (d.rectangle((3, 3, 12, 11), fill=(230, 180, 110, 255), outline=OUTLINE),
                               d.rectangle((4, 9, 11, 10), fill=(250, 210, 60, 255)), d.line((4, 5, 11, 5), fill=(190, 130, 70, 255)))),
    "sandwich_ham": _plate(lambda d: (d.polygon([(2, 11), (13, 11), (2, 2)], fill=(240, 220, 170, 255), outline=OUTLINE),
                                      d.line((3, 9, 11, 9), fill=(230, 130, 140, 255)), d.line((3, 8, 9, 8), fill=(110, 180, 80, 255)))),
    "sandwich_honey": _plate(lambda d: (d.polygon([(2, 11), (13, 11), (2, 2)], fill=(240, 220, 170, 255), outline=OUTLINE),
                                        d.line((3, 9, 11, 9), fill=(250, 230, 120, 255)), d.line((3, 8, 9, 8), fill=(240, 170, 40, 255)))),
    "waffle_honey": _img16(lambda d: (d.rectangle((2, 4, 13, 14), fill=(230, 180, 90, 255), outline=OUTLINE),
                                      [d.line((x, 5, x, 13), fill=(190, 130, 60, 255)) for x in (5, 8, 11)],
                                      [d.line((3, y, 12, y), fill=(190, 130, 60, 255)) for y in (7, 10)],
                                      d.line((4, 3, 10, 3), fill=(240, 170, 40, 255)))),
    "waffle_choco": _img16(lambda d: (d.rectangle((2, 4, 13, 14), fill=(230, 180, 90, 255), outline=OUTLINE),
                                      [d.line((x, 5, x, 13), fill=(190, 130, 60, 255)) for x in (5, 8, 11)],
                                      d.rectangle((3, 5, 12, 8), fill=(100, 55, 30, 255)))),
    "waffle_icecream": _img16(lambda d: (d.rectangle((1, 8, 14, 14), fill=(230, 180, 90, 255), outline=OUTLINE),
                                         [d.line((x, 9, x, 13), fill=(190, 130, 60, 255)) for x in (4, 7, 10)],
                                         oval(d, (4, 2, 11, 9), (250, 235, 200, 255)), d.point((7, 4), fill=(240, 170, 40, 255)))),
    "icecream_honey": _cone((250, 225, 150, 255), (150, 95, 40, 255)),
    "icecream_berry": _cone((170, 110, 200, 255), (90, 60, 160, 255)),
    "skewers": _img16(lambda d: (d.line((1, 14, 14, 1), fill=WOOD, width=1),
                                 [oval(d, (x - 2, 13 - x - 2, x + 2, 13 - x + 2), c) for x, c in
                                  [(4, (150, 80, 50, 255)), (7, (110, 180, 80, 255)), (10, (170, 90, 60, 255))]])),
    "veggies": _plate(lambda d: (oval(d, (2, 5, 7, 11), (110, 180, 80, 255)), oval(d, (6, 4, 11, 10), RED),
                                 oval(d, (10, 6, 14, 11), (250, 200, 60, 255)), d.line((5, 7, 5, 9), fill=(70, 120, 50, 255)))),
    "grilled_bread": _img16(lambda d: [(d.rectangle((x, y, x + 6, y + 6), fill=(220, 160, 90, 255), outline=OUTLINE),
                                        d.line((x + 1, y + 2, x + 5, y + 2), fill=(120, 70, 40, 255)),
                                        d.line((x + 1, y + 4, x + 5, y + 4), fill=(120, 70, 40, 255))) for x, y in [(1, 7), (8, 4)]]),
}


def _pressed(filling, spot):
    """Мачкан сандвич: запечен хляб с ивици от скарата и пълнеж отстрани."""
    return _plate(lambda d: (d.rectangle((2, 4, 13, 11), fill=(225, 170, 100, 255), outline=OUTLINE),
                             d.line((3, 9, 12, 9), fill=filling), d.line((3, 8, 12, 8), fill=spot),
                             [d.line((x, 5, x + 2, 7), fill=(150, 95, 50, 255)) for x in (4, 8)]))


FOOD_ART.update({
    "sandwich": _plate(lambda d: (d.rectangle((2, 5, 13, 11), fill=(245, 225, 175, 255), outline=OUTLINE),
                                  d.line((3, 8, 12, 8), fill=(250, 210, 80, 255)), d.line((3, 7, 12, 7), fill=(110, 180, 80, 255)),
                                  d.line((3, 9, 12, 9), fill=(230, 130, 140, 255)))),
    "roast_soup": _bowl((200, 110, 50, 255), [(5, 8, (240, 170, 60, 255)), (9, 8, (150, 70, 40, 255)), (7, 7, (110, 170, 80, 255))]),
    "pressed_caprese": _pressed((220, 60, 60, 255), (250, 250, 245, 255)),
    "pressed_veggie": _pressed((110, 180, 80, 255), (240, 170, 60, 255)),
    "pressed_chicken": _pressed((230, 190, 140, 255), (110, 180, 80, 255)),
    "pressed_egg": _pressed((250, 200, 60, 255), (250, 250, 245, 255)),
    "pressed_mushroom": _pressed((160, 120, 90, 255), (220, 200, 180, 255)),
})
for _id, _img in FOOD_ART.items():
    PROP_LIST[f"food_{_id}"] = (_img, 0)


def cook_table(frame):
    """Масичка с дъска: мечокът реже и подрежда сандвича (готово – на чинийка)."""
    def f(d):
        d.rectangle((1, 13, 20, 15), fill=WOOD, outline=OUTLINE)             # плотът
        d.rectangle((3, 15, 4, 23), fill=WOOD); d.rectangle((17, 15, 18, 23), fill=WOOD)
        d.rectangle((3, 10, 13, 13), fill=(200, 150, 90, 255), outline=OUTLINE)   # дъската
        if frame == 2:
            d.ellipse((5, 6, 18, 12), fill=(240, 240, 245, 255), outline=OUTLINE)
            d.rectangle((7, 6, 16, 9), fill=(245, 225, 175, 255), outline=OUTLINE)
            d.line((8, 8, 15, 8), fill=(110, 180, 80, 255))
        else:
            d.rectangle((5, 8 - frame, 11, 10 - frame), fill=(245, 225, 175, 255), outline=OUTLINE)   # филия
            d.line((14, 4 + frame * 2, 17, 10), fill=GREY)                                           # ножът
            d.point((7 + frame * 2, 7 - frame), fill=(250, 210, 80, 255))
    return f


for _i, _suffix in enumerate(["", "_b", "_open"]):
    PROP_LIST[f"cook_table{_suffix}"] = (prop(22, 24, cook_table(_i)), 0)


def cook_mixer(frame):
    """Миксер: кана върху основа; работи – перките се въртят."""
    def f(d):
        d.rectangle((4, 18, 15, 23), fill=(220, 90, 80, 255), outline=OUTLINE)
        d.point((9, 20), fill=(250, 240, 120, 255) if frame else WHITE)
        d.polygon([(5, 6), (14, 6), (13, 17), (6, 17)], fill=(220, 240, 250, 200), outline=OUTLINE)
        d.rectangle((7, 11, 12, 16), fill=(240, 200, 150, 255))
        if frame == 1:
            d.line((7, 13, 12, 14), fill=GREY)
        else:
            d.line((9, 12, 10, 16), fill=GREY)
        if frame == 2:
            d.rectangle((5, 2, 14, 4), fill=(220, 90, 80, 255), outline=OUTLINE)
        else:
            d.rectangle((5, 4, 14, 6), fill=(220, 90, 80, 255), outline=OUTLINE)
    return f


def cook_sandwich(frame):
    """Сандвичарник: две плочи с панта; работи – свети лампичката и пуши."""
    def f(d):
        d.rectangle((2, 15, 17, 22), fill=(80, 80, 90, 255), outline=OUTLINE)
        if frame == 2:
            d.polygon([(2, 15), (4, 4), (19, 4), (17, 15)], fill=(110, 110, 120, 255), outline=OUTLINE)
            d.rectangle((4, 13, 15, 15), fill=(230, 180, 100, 255))
        else:
            d.rectangle((2, 10, 17, 15), fill=(110, 110, 120, 255), outline=OUTLINE)
            d.rectangle((14, 7, 19, 9), fill=OUTLINE)
        d.point((5, 19), fill=(250, 80, 60, 255) if frame == 1 else (120, 40, 40, 255))
        if frame == 1:
            d.line((8, 7, 7, 4), fill=(220, 220, 220, 255)); d.line((12, 6, 13, 3), fill=(220, 220, 220, 255))
    return f


def cook_icecream(frame):
    """Машинка за сладолед: кутия с купа отгоре; работи – бърка."""
    def f(d):
        d.rectangle((3, 12, 16, 23), fill=(150, 200, 230, 255), outline=OUTLINE)
        d.rectangle((5, 15, 14, 17), fill=WHITE)
        d.ellipse((4, 5, 15, 13), fill=(240, 245, 250, 255), outline=OUTLINE)
        if frame == 2:
            oval(d, (6, 2, 13, 8), (250, 235, 200, 255))
        else:
            d.ellipse((6, 7, 13, 11), fill=(250, 235, 200, 255))
            d.line((9 + frame, 7, 10 - frame, 11), fill=GREY)
    return f


def cook_grill(frame):
    """Скара: решетка на крачета; работи – въглените светят и пуши."""
    def f(d):
        d.line((4, 17, 2, 23), fill=OUTLINE); d.line((15, 17, 17, 23), fill=OUTLINE)
        d.pieslice((2, 6, 17, 19), 0, 180, fill=(60, 60, 70, 255), outline=OUTLINE)
        for x in range(4, 16, 2):
            d.point((x, 13), fill=FLAME if (x + frame) % 4 else (255, 210, 90, 255))
        d.line((2, 12, 17, 12), fill=GREY)
        if frame != 2:
            oval(d, (5, 8, 9, 11), (150, 80, 50, 255)); oval(d, (11, 8, 15, 11), (110, 180, 80, 255))
        if frame == 1:
            d.line((7, 6, 6, 2), fill=(220, 220, 220, 255)); d.line((12, 5, 13, 1), fill=(220, 220, 220, 255))
    return f


for _tool, _fn in {"mixer": cook_mixer, "sandwich": cook_sandwich, "icecream": cook_icecream, "grill": cook_grill}.items():
    for _i, _suffix in enumerate(["", "_b", "_open"]):
        PROP_LIST[f"cook_{_tool}{_suffix}"] = (prop(22, 24, _fn(_i)), 0)
    # Иконката в магазина: първият кадър, смален до 16x16.
    PROP_LIST[f"shop_{_tool}"] = (prop(22, 24, _fn(0)).crop((0, 0, 20, 24)).resize((16, 16), Image.NEAREST), 0)

for _sfx, (_w, _h) in {"_s": (12, 10), "": (18, 16), "_l": (28, 20)}.items():
    PROP_LIST[f"parcel{_sfx}"] = (prop(_w, _h, parcel(False, _w, _h)), 0)
    PROP_LIST[f"parcel{_sfx}_open"] = (prop(_w, _h, parcel(True, _w, _h)), 0)

for _i, _name in enumerate(["bee", "bee_b"]):
    PROP_LIST[_name] = (prop(11, 11, bee(_i)), 0)
for _i, _name in enumerate(["squirrel", "squirrel_b"]):
    PROP_LIST[_name] = (prop(18, 16, squirrel(_i)), 0)
for _tool, _fn in COOKERS.items():
    for _i, _suffix in enumerate(["", "_b", "_open"]):
        PROP_LIST[f"cook_{_tool}{_suffix}"] = (prop(22, 24, _fn(_i)), 0)


# ── Скинове (временни: оцветени копия на класическия; Тут ги рисува истински) ──

def tint(img, color, strength=0.75, pattern=None, accent=WHITE):
    """Оцветява всичко без контура и пламъчетата към color (светлото остава светло)."""
    out = img.copy()
    px = out.load()
    keep = {OUTLINE, FLAME, FLAME2, (255, 150, 60, 255), (255, 200, 90, 255), (220, 220, 220, 255)}
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if not p[3] or p in keep:
                continue
            lum = (0.3 * p[0] + 0.59 * p[1] + 0.11 * p[2]) / 255
            target = [min(255, int(c * (0.45 + 0.8 * lum))) for c in color[:3]]
            px[x, y] = tuple(int(p[i] * (1 - strength) + target[i] * strength) for i in range(3)) + (p[3],)
    if pattern == "dots":
        for y in range(2, out.height - 2, 4):
            for x in range(2 + (y // 4) % 2 * 2, out.width - 2, 4):
                if px[x, y][3] and px[x, y] != OUTLINE and px[x - 1, y] != OUTLINE and px[x + 1, y] != OUTLINE:
                    px[x, y] = accent
    return out


# ── Други видове на уредите и печицата (временни рисунки; Тут рисува истинските) ──
# Кадри: 0 = готви (""), 1 = готви, втори кадър ("_b"), 2 = готово ("_open").
STEAM = (220, 220, 220, 255)
COPPER, COPPER_D = (215, 120, 60, 255), (160, 80, 40, 255)
BLACK_IRON = (50, 50, 58, 255)


def steam(d, frame, x1=7, x2=12, top=0):
    if frame:
        d.line((x1, top + 4, x1 - 1 + frame % 2, top), fill=STEAM)
        d.line((x2, top + 3, x2 + 1 - frame % 2, top), fill=STEAM)


def pot_enamel(frame):
    """Емайлирана тенджера: бяла, кръгличка, на сини точки."""
    def f(d):
        stove(d, frame)
        d.rounded_rectangle((3, 6, 16, 13), 3, fill=WHITE, outline=OUTLINE)
        for x, y in [(5, 9), (9, 11), (13, 8), (12, 12)]:
            d.point((x, y), fill=(70, 120, 210, 255))
        d.line((1, 8, 3, 8), fill=OUTLINE); d.line((16, 8, 18, 8), fill=OUTLINE)
        lift = 0 if frame == 2 else frame
        d.rectangle((3, 4 - lift - (frame == 2) * 3, 16, 5 - lift - (frame == 2) * 3), fill=(70, 120, 210, 255), outline=OUTLINE)
        steam(d, frame)
    return f


def pot_copper(frame):
    """Медна тенджера: широка и ниска, с дълга дръжка."""
    def f(d):
        stove(d, frame)
        d.rectangle((2, 8, 15, 13), fill=COPPER, outline=OUTLINE)
        d.line((3, 9, 14, 9), fill=(240, 170, 110, 255))
        d.line((15, 9, 20, 7), fill=COPPER_D, width=2)
        if frame == 2:
            d.rectangle((1, 2, 12, 3), fill=COPPER_D, outline=OUTLINE)
        else:
            d.rectangle((2, 6 - frame, 15, 7 - frame), fill=COPPER_D, outline=OUTLINE)
        steam(d, frame)
    return f


def pan_cast_iron(frame):
    """Чугунен тиган: черен, дълбок, с късата дръжка и ухо."""
    def f(d):
        stove(d, frame)
        d.rectangle((2, 9, 15, 13), fill=BLACK_IRON, outline=OUTLINE)
        d.line((15, 10, 19, 10), fill=BLACK_IRON, width=2)
        d.point((1, 10), fill=OUTLINE)
        for x in (4, 9):
            oval(d, (x, 6 - (frame == 2) * 3, x + 4, 9 - (frame == 2) * 3), (240, 200, 90, 255))   # яйца
        steam(d, frame, 5, 13)
    return f


def pan_pink(frame):
    """Розов уок: дълбок, заоблен."""
    def f(d):
        stove(d, frame)
        d.chord((1, 4, 17, 15), 0, 180, fill=(240, 140, 170, 255), outline=OUTLINE)
        d.line((16, 9, 20, 6), fill=WOOD, width=2)
        for x in (4, 8, 12):
            d.point((x + frame, 7 - (frame == 2) * 3), fill=(110, 180, 80, 255))
            d.point((x + 1, 8 - (frame == 2) * 3), fill=(230, 120, 60, 255))
        steam(d, frame, 6, 12)
    return f


def oven_retro(frame):
    """Ретро фурна: заоблена, ментова, кръгло прозорче и хромирани копчета."""
    def f(d):
        d.rounded_rectangle((1, 2, 20, 23), 4, fill=(120, 200, 190, 255), outline=OUTLINE)
        for x in (5, 9, 13, 17):
            d.point((x, 4), fill=(230, 230, 235, 255))
        if frame == 2:
            d.rectangle((0, 18, 21, 20), fill=(200, 200, 205, 255), outline=OUTLINE)
            for x in (3, 9, 14):
                oval(d, (x, 15, x + 4, 18), (230, 180, 70, 255))
        oval(d, (5, 7, 16, 17), (60, 40, 30, 255))
        if frame != 2:
            oval(d, (7, 9, 14, 15), (240, 150, 60, 255) if frame else (220, 110, 40, 255), outline=None)
        steam(d, frame, 7, 14)
    return f


def oven_wood(frame):
    """Пещ на дърва: тухлен купол с огън в отвора."""
    def f(d):
        d.pieslice((0, 2, 21, 30), 180, 360, fill=(180, 90, 60, 255), outline=OUTLINE)
        d.rectangle((0, 16, 21, 23), fill=(170, 160, 150, 255), outline=OUTLINE)
        for y in (6, 10, 14):
            d.line((4, y, 17, y), fill=(140, 65, 45, 255))
        d.pieslice((6, 9, 15, 22), 180, 360, fill=(40, 25, 20, 255), outline=OUTLINE)
        if frame == 2:
            oval(d, (6, 12, 14, 16), (230, 180, 70, 255))     # пицата излиза
        else:
            d.point((9 + frame, 13), fill=FLAME); d.point((11, 14 - frame), fill=FLAME2); d.point((10, 15), fill=FLAME)
        d.rectangle((9, 0, 12, 4), fill=(120, 110, 100, 255), outline=OUTLINE)   # комин
        if frame:
            d.point((10, 0), fill=STEAM)
    return f


def coffee_moka(frame):
    """Мока кана на котлона: осмоъгълна, със заострен чучур."""
    def f(d):
        stove(d, frame)
        d.polygon([(6, 14), (13, 14), (14, 10), (12, 8), (7, 8), (5, 10)], fill=(200, 200, 210, 255), outline=OUTLINE)
        d.polygon([(7, 8), (12, 8), (13, 3), (6, 3)], fill=(185, 185, 195, 255), outline=OUTLINE)
        d.rectangle((6, 1, 13, 3), fill=(160, 160, 170, 255), outline=OUTLINE)
        d.line((5, 4, 4, 2), fill=OUTLINE)
        d.line((13, 4, 16, 7), fill=OUTLINE); d.line((16, 7, 15, 10), fill=OUTLINE)
        if frame:
            d.point((4, 1), fill=(90, 55, 30, 255) if frame == 2 else STEAM)
            d.point((3, 0), fill=STEAM)
    return f


def coffee_retro(frame):
    """Ретро еспресо машина: червена, заоблена, с купол и лост."""
    def f(d):
        d.rounded_rectangle((2, 6, 17, 23), 4, fill=(210, 60, 60, 255), outline=OUTLINE)
        d.pieslice((4, 1, 15, 12), 180, 360, fill=(200, 200, 210, 255), outline=OUTLINE)
        d.point((9, 1), fill=OUTLINE)
        d.line((16, 9, 20, 5 + frame * 2), fill=OUTLINE)            # лостът
        d.rectangle((7, 11, 12, 13), fill=(200, 200, 210, 255), outline=OUTLINE)
        d.rectangle((6, 17, 12, 21), fill=WHITE, outline=OUTLINE)
        if frame == 1:
            d.line((9, 14, 9, 16), fill=(90, 55, 30, 255))
        if frame == 2:
            d.line((7, 18, 11, 18), fill=(90, 55, 30, 255))
            steam(d, 1, 8, 11, 12)
    return f


def kettle_whistle(frame):
    """Свирещ чайник: червен, ъгловат, със свирка на чучура."""
    def f(d):
        stove(d, frame)
        d.polygon([(4, 14), (15, 14), (13, 6), (6, 6)], fill=(230, 80, 70, 255), outline=OUTLINE)
        d.line((14, 9, 18, 5), fill=OUTLINE)
        d.rectangle((17, 3, 19, 5), fill=(200, 200, 210, 255), outline=OUTLINE)   # свирката
        d.arc((6, 1, 13, 8), 180, 360, fill=OUTLINE)
        if frame:
            for k in range(frame + 1):
                d.point((20, 2 - k), fill=STEAM)
            d.line((20, 1, 21, 0), fill=STEAM)
    return f


def kettle_glass(frame):
    """Стъклен чайник: вижда се чаят, който става все по-тъмен."""
    def f(d):
        stove(d, frame)
        oval(d, (4, 5, 15, 14), (215, 238, 248, 255))
        tea = [(230, 200, 120, 255), (210, 150, 70, 255), (180, 100, 40, 255)][frame]
        oval(d, (6, 9, 13, 13), tea, outline=None)
        d.line((15, 8, 19, 6), fill=OUTLINE)
        d.rectangle((8, 3, 11, 5), fill=(200, 200, 210, 255), outline=OUTLINE)
        steam(d, frame, 8, 11)
    return f


def mixer_mint(frame):
    """Планетарен миксер: основа, рамо отгоре и купа."""
    def f(d):
        mint = (150, 215, 190, 255)
        d.rectangle((2, 20, 17, 23), fill=mint, outline=OUTLINE)
        d.rectangle((14, 6, 17, 20), fill=mint, outline=OUTLINE)
        d.rounded_rectangle((3, 3 - (frame == 2) * 2, 17, 8 - (frame == 2) * 2), 2, fill=mint, outline=OUTLINE)
        d.chord((3, 10, 14, 22), 0, 180, fill=(220, 225, 230, 255), outline=OUTLINE)
        d.rectangle((5, 14, 12, 16), fill=(240, 200, 150, 255))
        if frame != 2:
            d.line((8 + frame, 8, 9 - frame, 15), fill=GREY)
    return f


def mixer_hand(frame):
    """Купа и бъркалка на ръка."""
    def f(d):
        d.chord((1, 8, 18, 24), 0, 180, fill=(200, 170, 120, 255), outline=OUTLINE)
        d.line((3, 16, 16, 16), fill=(240, 220, 180, 255))
        if frame == 2:
            oval(d, (6, 10, 13, 15), (250, 240, 220, 255))    # готово, на купчинка
        else:
            x = 7 + frame * 4
            d.line((x, 2, x + 1, 14), fill=WOOD)
            d.ellipse((x - 2, 11, x + 3, 16), outline=GREY)
    return f


def sandwich_red(frame):
    """Червена преса за панини: дълги плочи и дръжка отпред."""
    def f(d):
        red = (210, 60, 60, 255)
        d.rectangle((1, 16, 20, 22), fill=red, outline=OUTLINE)
        if frame == 2:
            d.polygon([(1, 16), (2, 5), (20, 5), (20, 16)], fill=red, outline=OUTLINE)
            d.rectangle((3, 14, 18, 16), fill=(230, 180, 100, 255))
            for x in range(4, 18, 3):
                d.point((x, 15), fill=(160, 100, 50, 255))
        else:
            d.rectangle((1, 11, 20, 16), fill=red, outline=OUTLINE)
            d.line((18, 12, 21, 9), fill=OUTLINE)
        d.point((4, 19), fill=(250, 240, 120, 255) if frame == 1 else (120, 40, 40, 255))
        steam(d, frame, 8, 14, 5)
    return f


def sandwich_waffle(frame):
    """Гофретник: кръгъл, с решетка; отворен – вижда се гофретата."""
    def f(d):
        d.ellipse((2, 15, 18, 23), fill=(70, 70, 80, 255), outline=OUTLINE)
        if frame == 2:
            d.ellipse((3, 3, 19, 11), fill=(70, 70, 80, 255), outline=OUTLINE)
            oval(d, (4, 14, 16, 20), (230, 180, 90, 255))
            for x in range(6, 15, 3):
                d.line((x, 15, x, 19), fill=(190, 130, 60, 255))
        else:
            d.ellipse((2, 11, 18, 18), fill=(90, 90, 100, 255), outline=OUTLINE)
            d.line((17, 13, 21, 12), fill=OUTLINE)
        d.point((5, 21), fill=(250, 80, 60, 255) if frame == 1 else (120, 40, 40, 255))
        steam(d, frame, 7, 13, 6)
    return f


def icecream_cart(frame):
    """Количка за сладолед: чадърче, кутия и колелца."""
    def f(d):
        d.pieslice((1, 0, 20, 10), 180, 360, fill=(240, 120, 150, 255), outline=OUTLINE)
        for x in (5, 13):
            d.line((x, 1, x + 1, 5), fill=WHITE)
        d.line((10, 5, 10, 11), fill=OUTLINE)
        d.rectangle((2, 11, 19, 19), fill=(250, 240, 230, 255), outline=OUTLINE)
        d.line((3, 13, 18, 13), fill=(120, 190, 230, 255))
        oval(d, (3, 18, 8, 23), GREY); oval(d, (13, 18, 18, 23), GREY)
        if frame == 2:
            oval(d, (8, 7, 13, 11), (250, 235, 200, 255))
        elif frame == 1:
            d.point((10, 15), fill=(240, 120, 150, 255))
    return f


def icecream_pink(frame):
    """Машина за сладолед на фунийка: розова, с кранче."""
    def f(d):
        pink = (240, 150, 180, 255)
        d.rectangle((3, 2, 17, 15), fill=pink, outline=OUTLINE)
        d.rectangle((5, 4, 15, 8), fill=(250, 220, 230, 255), outline=OUTLINE)
        d.rectangle((9, 15, 11, 17), fill=(200, 200, 210, 255), outline=OUTLINE)    # кранчето
        d.rectangle((2, 21, 18, 23), fill=pink, outline=OUTLINE)
        if frame == 2:
            d.polygon([(8, 21), (12, 21), (10, 23)], fill=(220, 170, 90, 255))
            oval(d, (8, 17, 12, 21), (250, 235, 200, 255))
        elif frame == 1:
            d.line((10, 18, 10, 20), fill=(250, 235, 200, 255))
    return f


def grill_kettle(frame):
    """Кръгло барбекю: кълбо на три крачета с капак."""
    def f(d):
        d.line((5, 17, 3, 23), fill=OUTLINE); d.line((14, 17, 16, 23), fill=OUTLINE); d.line((10, 18, 10, 23), fill=OUTLINE)
        d.chord((2, 4, 18, 20), 0, 180, fill=BLACK_IRON, outline=OUTLINE)
        if frame == 2:
            d.chord((2, 0, 18, 12), 180, 360, fill=BLACK_IRON, outline=OUTLINE)   # отворено
            oval(d, (5, 9, 9, 12), (150, 80, 50, 255)); oval(d, (11, 9, 15, 12), (110, 180, 80, 255))
        else:
            d.chord((2, 4, 18, 20), 180, 360, fill=(70, 70, 80, 255), outline=OUTLINE)
            d.rectangle((9, 3, 11, 4), fill=OUTLINE)
            for x in range(4, 17, 3):
                d.point((x, 12), fill=FLAME if (x + frame) % 2 else FLAME2)
        steam(d, frame, 6, 14)
    return f


def grill_camp(frame):
    """Огнище: камъчета в кръг, огън и шиш с наденички."""
    def f(d):
        for x in range(2, 19, 4):
            oval(d, (x, 19, x + 3, 22), (150, 150, 155, 255))
        d.line((4, 18, 9, 13), fill=WOOD, width=2); d.line((16, 18, 11, 13), fill=WOOD, width=2)
        if frame != 2:
            d.polygon([(7, 18), (10, 9 + frame), (13, 18)], fill=FLAME)
            d.polygon([(9, 18), (10, 13 - frame), (11, 18)], fill=FLAME2)
        else:
            for x in (8, 10, 12):
                d.point((x, 17), fill=(255, 150, 60, 255))
        d.line((1, 7, 20, 7), fill=WOOD)                 # шишът
        d.line((1, 7, 1, 18), fill=WOOD); d.line((20, 7, 20, 18), fill=WOOD)
        for x in (5, 9, 13):
            d.rectangle((x, 6, x + 2, 8), fill=(190, 90, 60, 255) if frame == 2 else (220, 140, 120, 255), outline=OUTLINE)
        steam(d, frame, 6, 14)
    return f


# Уред → {вид: функция}. Името на рисунката е cook_<уред>_<вид>[_b|_open].
COOKER_SKINS = {
    "pot": {"enamel": pot_enamel, "copper": pot_copper},
    "pan": {"cast_iron": pan_cast_iron, "pink": pan_pink},
    "oven": {"retro": oven_retro, "wood": oven_wood},
    "coffee_machine": {"moka": coffee_moka, "retro": coffee_retro},
    "kettle": {"whistle": kettle_whistle, "glass": kettle_glass},
    "mixer": {"mint": mixer_mint, "hand": mixer_hand},
    "sandwich": {"red": sandwich_red, "waffle": sandwich_waffle},
    "icecream": {"cart": icecream_cart, "pink": icecream_pink},
    "grill": {"kettle": grill_kettle, "camp": grill_camp},
}
for _tool, _skins in COOKER_SKINS.items():
    for _skin, _fn in _skins.items():
        for _i, _suffix in enumerate(["", "_b", "_open"]):
            PROP_LIST[f"cook_{_tool}_{_skin}{_suffix}"] = (prop(22, 24, _fn(_i)), 0)


def heater_fireplace(frame):
    """Камина: тухлена, с полица и огън (frame 0 = изгасена)."""
    def f(d):
        d.rectangle((0, 3, 19, 21), fill=(180, 90, 60, 255), outline=OUTLINE)
        for y in (7, 11, 15, 19):
            d.line((1, y, 18, y), fill=(140, 65, 45, 255))
        d.rectangle((0, 1, 19, 3), fill=WOOD, outline=OUTLINE)              # полицата
        d.rectangle((4, 9, 15, 21), fill=(40, 25, 20, 255), outline=OUTLINE)
        d.line((6, 20, 13, 19), fill=WOOD, width=2)                       # цепеници
        if frame:
            d.polygon([(6, 19), (9, 11 + frame), (13, 19)], fill=FLAME)
            d.polygon([(8, 19), (10, 14 - frame), (11, 19)], fill=FLAME2)
    return f


def heater_tiles(frame):
    """Кахлена печка: висока, на сини плочки, с вратичка."""
    def f(d):
        d.rectangle((3, 0, 16, 21), fill=(240, 240, 235, 255), outline=OUTLINE)
        for y in range(2, 20, 4):
            for x in range(5, 15, 4):
                d.rectangle((x, y, x + 2, y + 2), outline=(70, 120, 200, 255))
        d.rectangle((6, 13, 13, 18), fill=(60, 40, 35, 255), outline=OUTLINE)
        if frame:
            d.point((8 + frame, 16), fill=FLAME); d.point((10, 15), fill=FLAME2); d.point((9, 17), fill=FLAME)
    return f


def heater_retro(frame):
    """Ретро радиатор: ребра на крачета; включен – ребрата светят оранжево."""
    def f(d):
        for x in range(1, 19, 3):
            col = (230, 120, 60, 255) if frame and (x // 3 + frame) % 2 else (230, 220, 200, 255)
            d.rounded_rectangle((x, 4, x + 2, 19), 1, fill=col, outline=OUTLINE)
        d.line((1, 6, 19, 6), fill=OUTLINE)
        d.rectangle((2, 20, 3, 21), fill=OUTLINE); d.rectangle((16, 20, 17, 21), fill=OUTLINE)
        d.point((19, 9), fill=(250, 80, 60, 255) if frame else (120, 40, 40, 255))
    return f


def heater_cozy(frame):
    """Пузеста печка с котлонче и кюнец."""
    def f(d):
        d.rectangle((8, 0, 11, 6), fill=BLACK_IRON, outline=OUTLINE)       # кюнец
        d.ellipse((2, 5, 17, 20), fill=(70, 70, 80, 255), outline=OUTLINE)
        d.rectangle((4, 5, 15, 7), fill=BLACK_IRON, outline=OUTLINE)
        d.ellipse((6, 10, 13, 16), fill=(40, 30, 30, 255), outline=OUTLINE)
        if frame:
            d.point((9, 12 + frame % 2), fill=FLAME); d.point((10, 13), fill=FLAME2); d.point((8, 14), fill=FLAME)
        d.rectangle((4, 19, 6, 21), fill=OUTLINE); d.rectangle((13, 19, 15, 21), fill=OUTLINE)
    return f


HEATER_SKINS = {"fireplace": heater_fireplace, "tiles": heater_tiles, "retro": heater_retro, "cozy": heater_cozy}
for _skin, _fn in HEATER_SKINS.items():
    for _suffix, _frame in [("", 1), ("_b", 2), ("_off", 0)]:
        PROP_LIST[f"heater_{_skin}{_suffix}"] = (prop(20, 22, _fn(_frame)), 0)


def lamp(kind):
    """Лампа 14 x 30, свети. Видовете: classic (лампион), mushroom, lantern, moon, lava, candle."""
    img = Image.new("RGBA", (14, 30), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    glow, light = (255, 236, 150, 255), (255, 250, 210, 255)
    if kind == "classic":
        d.polygon([(3, 2), (10, 2), (12, 9), (1, 9)], fill=(240, 200, 120, 255), outline=OUTLINE)
        d.line((4, 8, 9, 8), fill=light)
        d.rectangle((6, 10, 7, 27), fill=WOOD)
        d.rectangle((3, 27, 10, 29), fill=WOOD, outline=OUTLINE)
    elif kind == "mushroom":
        oval(d, (1, 12, 12, 21), (220, 60, 60, 255))
        for x, y in [(4, 14), (8, 13), (10, 17), (5, 18)]:
            d.point((x, y), fill=WHITE)
        d.rectangle((5, 21, 8, 28), fill=light, outline=OUTLINE)
        d.line((3, 29, 10, 29), fill=OUTLINE)
    elif kind == "lantern":
        d.arc((4, 8, 9, 14), 180, 360, fill=OUTLINE)
        d.rectangle((3, 12, 10, 26), fill=(60, 60, 70, 255), outline=OUTLINE)
        d.rectangle((4, 15, 9, 24), fill=glow)
        d.line((6, 18, 6, 22), fill=(255, 160, 60, 255))
        d.rectangle((2, 26, 11, 29), fill=(60, 60, 70, 255), outline=OUTLINE)
    elif kind == "moon":
        oval(d, (1, 10, 12, 21), glow)
        oval(d, (5, 9, 14, 19), (0, 0, 0, 0), outline=None)
        d.rectangle((5, 21, 8, 27), fill=WOOD)
        d.rectangle((2, 27, 11, 29), fill=WOOD, outline=OUTLINE)
    elif kind == "lava":
        d.polygon([(4, 9), (9, 9), (11, 24), (2, 24)], fill=(120, 60, 160, 255), outline=OUTLINE)
        oval(d, (4, 12, 7, 15), (255, 120, 80, 255), outline=None)
        oval(d, (6, 18, 9, 22), (255, 160, 80, 255), outline=None)
        d.rectangle((3, 6, 10, 9), fill=GREY, outline=OUTLINE)
        d.rectangle((2, 24, 11, 29), fill=GREY, outline=OUTLINE)
    elif kind == "candle":
        oval(d, (5, 14, 8, 19), (255, 200, 80, 255), outline=None)
        d.point((6, 15), fill=WHITE)
        d.rectangle((4, 19, 9, 27), fill=(250, 245, 230, 255), outline=OUTLINE)
        d.rectangle((1, 27, 12, 29), fill=(200, 160, 60, 255), outline=OUTLINE)
    return img


LAMP_SKINS = ["mushroom", "lantern", "moon", "lava", "candle"]
PROP_LIST["lamp"] = (lamp("classic"), 0)
for _skin in LAMP_SKINS:
    PROP_LIST[f"lamp_{_skin}"] = (lamp(_skin), 0)


def yarn_basket(d):
    """Кошница с прежда 16 x 12: три кълбета, една нишка виси."""
    for x, c in [(3, (240, 140, 150, 255)), (8, (120, 180, 220, 255)), (11, (250, 200, 80, 255))]:
        oval(d, (x - 2, 1, x + 2, 5), c)
    d.rectangle((1, 5, 14, 11), fill=(190, 140, 80, 255), outline=OUTLINE)
    for x in range(3, 14, 3):
        d.line((x, 6, x, 10), fill=(150, 100, 55, 255))
    d.line((2, 8, 14, 8), fill=(150, 100, 55, 255))
    d.line((14, 3, 15, 7), fill=(240, 140, 150, 255))


PROP_LIST["yarn_basket"] = (prop(16, 12, yarn_basket), 0)


def guitar_tool(body, kind="classic"):
    """Китарата в лапите, 27 x 27: средата (13,13) е там, където лапата дрънка по струните."""
    img = Image.new("RGBA", (27, 27), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    neck = (110, 70, 40, 255)
    small = kind == "ukulele"
    nx = 21 if small else 24
    d.line((12, 15, nx, 4 if not small else 7), fill=neck, width=2)                 # грифът
    d.rectangle((nx - 1, (2 if not small else 5), nx + 1, (5 if not small else 8)), fill=OUTLINE)  # главата
    if kind == "electric":
        d.polygon([(4, 12), (11, 10), (14, 14), (12, 22), (5, 22), (3, 17)], fill=body, outline=OUTLINE)
        d.rectangle((7, 15, 10, 16), fill=WHITE)
    elif kind == "star":
        cx, cy = 9, 16
        pts = [(cx, cy - 7), (cx + 2, cy - 2), (cx + 7, cy - 2), (cx + 3, cy + 1), (cx + 5, cy + 6),
               (cx, cy + 3), (cx - 5, cy + 6), (cx - 3, cy + 1), (cx - 7, cy - 2), (cx - 2, cy - 2)]
        d.polygon(pts, fill=body, outline=OUTLINE)
        d.point((cx, cy), fill=OUTLINE)
    elif small:
        oval(d, (6, 11, 13, 17), body)
        oval(d, (5, 15, 13, 22), body)
        d.point((9, 17), fill=OUTLINE)
    else:
        oval(d, (5, 9, 14, 17), body)
        oval(d, (3, 14, 14, 24), body)
        oval(d, (7, 15, 10, 18), OUTLINE, outline=None)
    return img


GUITARS = {
    "": ((210, 150, 80, 255), "classic"),
    "_electric": ((210, 50, 50, 255), "electric"),
    "_ukulele": ((230, 180, 110, 255), "ukulele"),
    "_blue": ((80, 130, 210, 255), "classic"),
    "_star": ((250, 200, 60, 255), "star"),
}


def knitting_tool():
    """Куките с плетката, 27 x 27, средата е между лапите."""
    img = Image.new("RGBA", (27, 27), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((9, 13, 17, 17), fill=(240, 140, 150, 255), outline=OUTLINE)     # плетката
    for x in range(10, 17, 2):
        d.point((x, 15), fill=(255, 200, 210, 255))
    d.line((6, 8, 13, 14), fill=GREY)                                             # куките
    d.line((20, 8, 14, 14), fill=GREY)
    d.point((6, 8), fill=OUTLINE)
    d.point((20, 8), fill=OUTLINE)
    d.line((13, 18, 12, 22), fill=(240, 140, 150, 255))                         # нишката
    return img


def save_tools():
    """Предметите в лапите (assets/clothes/tool_*.png), които Тут още не е нарисувала."""
    done = os.path.join(os.path.dirname(__file__), "..", "done_art")
    folder = os.path.join(os.path.dirname(__file__), "..", "assets", "clothes")
    tools = {f"guitar{_sfx}": guitar_tool(*_args) for _sfx, _args in GUITARS.items()}
    tools["knitting"] = knitting_tool()
    for name, img in tools.items():
        if not os.path.exists(os.path.join(done, f"{name}.png")):
            img.save(os.path.join(folder, f"tool_{name}.png"))


_g = guitar_tool(*GUITARS[""])
PROP_LIST["shop_guitar"] = (_g.crop(_g.getbbox()).resize((16, 16), Image.NEAREST), 0)


# ── Градинката (поливане): цвете в 5 стадия, 12 x 16; цъфналото е в различни цветове ──
SOIL, SOIL_D, STEM, LEAF = (130, 85, 50, 255), (95, 60, 35, 255), (80, 150, 60, 255), (110, 180, 80, 255)


def flower(stage, petal=(240, 120, 160, 255), center=(250, 210, 80, 255)):
    def f(d):
        d.ellipse((1, 12, 10, 15), fill=SOIL, outline=SOIL_D)          # купчинка пръст
        if stage == 0:
            d.point((5, 12), fill=SOIL_D); d.point((7, 13), fill=SOIL_D)
            return
        top = {1: 9, 2: 6, 3: 3, 4: 3}[stage]
        d.line((5, top, 5, 12), fill=STEM)
        if stage >= 1:
            d.point((4, top), fill=LEAF); d.point((6, top), fill=LEAF)
        if stage >= 2:
            oval(d, (1, 8, 4, 10), LEAF, outline=None); oval(d, (6, 7, 9, 9), LEAF, outline=None)
        if stage == 3:
            oval(d, (3, 1, 7, 5), (120, 190, 90, 255))
            d.point((5, 1), fill=petal)
        if stage == 4:
            for dx, dy in [(-2, 0), (2, 0), (0, -2), (0, 2)]:
                oval(d, (5 + dx - 2, 3 + dy - 2, 5 + dx + 2, 3 + dy + 2), petal, outline=None)
            oval(d, (4, 2, 6, 4), center, outline=None)
    return f


for _st in range(4):
    PROP_LIST[f"flower_{_st}"] = (prop(12, 16, flower(_st)), 0)
FLOWER_COLORS = {
    "pink": (240, 120, 160, 255), "yellow": (250, 210, 70, 255), "red": (220, 60, 60, 255),
    "purple": (170, 120, 220, 255), "white": (250, 250, 250, 255), "blue": (110, 160, 240, 255),
}
for _c, _petal in FLOWER_COLORS.items():
    PROP_LIST[f"flower_4_{_c}"] = (prop(12, 16, flower(4, _petal, (250, 200, 60, 255) if _c != "yellow" else (200, 120, 40, 255))), 0)


DONE_ART = os.path.join(os.path.dirname(__file__), "..", "done_art")
# Цветовете на подаръка на Тут → за всеки цвят: (кутия, тъмно, панделка).
GIFT_SRC = {(135, 193, 234): 0, (95, 148, 200): 1, (249, 115, 129): 2}
GIFT_RECOLOR = {
    "": ((135, 193, 234, 255), (95, 148, 200, 255), (249, 115, 129, 255)),
    "pink": ((245, 165, 190, 255), (215, 115, 150, 255), (255, 250, 240, 255)),
    "green": ((140, 200, 110, 255), (90, 145, 70, 255), (250, 210, 80, 255)),
    "yellow": ((250, 215, 95, 255), (220, 170, 50, 255), (220, 70, 70, 255)),
    "purple": ((180, 140, 225, 255), (125, 90, 180, 255), (255, 220, 120, 255)),
    "red": ((225, 80, 80, 255), (170, 45, 50, 255), (250, 220, 120, 255)),
}


def recolor_gift(img, color):
    out = img.convert("RGBA").copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            p = px[x, y]
            if p[3] and p[:3] in GIFT_SRC:
                px[x, y] = GIFT_RECOLOR[color][GIFT_SRC[p[:3]]]
    return out


def gift_opened(img):
    """Отворен подарък от нарисувания: капакът (горните 8 реда) отскача нагоре-вляво, отвътре блести."""
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.alpha_composite(img.crop((0, 8, 16, 16)), (0, 8))
    lid = img.crop((0, 0, 16, 8))
    out.alpha_composite(lid, (-3, -2))
    d = ImageDraw.Draw(out)
    for x, y in [(9, 6), (12, 5), (14, 7), (11, 3)]:
        d.point((x, y), fill=(255, 230, 120, 255))
    return out


def save_gifts():
    """Подаръците на Тут във всички цветове: кутията, отворена и анимацията с подаръка (в лапите е кутията)."""
    gift = os.path.join(DONE_ART, "gift.png")
    if not os.path.exists(gift):
        return
    box = Image.open(gift).convert("RGBA")
    walk = Image.open(os.path.join(OUT, "walk_gift.png")).convert("RGBA") if os.path.exists(os.path.join(OUT, "walk_gift.png")) else None
    with open(os.path.join(OUT, "anim.json"), encoding="utf-8") as fh:
        manifest = json.load(fh)
    for color in GIFT_RECOLOR:
        sfx = "_" + color if color else ""
        closed = recolor_gift(box, color)
        closed.save(pl.path(f"gift{sfx}"))
        gift_opened(closed).save(pl.path(f"gift{sfx}_open"))
        if walk is not None and color:
            # Анимацията в цвета на подаръка (дрехите – същите шаблони като walk_gift).
            recolor_gift(walk, color).save(os.path.join(OUT, f"walk_gift{sfx}.png"))
            manifest["animations"][f"walk_gift{sfx}"] = dict(manifest["animations"].get("walk_gift", {"fps": 8, "loop": True}), file=f"walk_gift{sfx}.png")
            for layer in os.listdir(LAYERS):
                if layer.startswith("walk_gift_") and not any(layer.startswith(f"walk_gift_{c}_") for c in GIFT_RECOLOR if c):
                    src = Image.open(os.path.join(LAYERS, layer))
                    src.save(os.path.join(LAYERS, layer.replace("walk_gift_", f"walk_gift{sfx}_", 1)))
    with open(os.path.join(OUT, "anim.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
        fh.write("\n")
    props_path = os.path.join(PROPS, "props.json")
    with open(props_path, encoding="utf-8") as fh:
        props = json.load(fh)
    for color in GIFT_RECOLOR:
        sfx = "_" + color if color else ""
        props["props"].pop(f"gift_hold{sfx}", None)   # в лапите е самата кутия (gift)
        for name in (f"gift{sfx}", f"gift{sfx}_open"):
            if os.path.exists(pl.path(name)):
                props["props"][name] = {"file": pl.rel(name), "seat": 0}
    with open(props_path, "w", encoding="utf-8") as fh:
        json.dump(props, fh, indent=2, ensure_ascii=False)
        fh.write("\n")


def derive_sleep_peek():
    """
    „Сънливо надига глава“ от нарисувания sleep.png на Тут: главата (лявата
    половина) се вдига с 1–2 пиксела, без Z-тата. Временно, докато не се нарисува.
    """
    path = os.path.join(OUT, "sleep.png")
    src = Image.open(path).convert("RGBA").crop((0, 0, SIZE, SIZE))
    body = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    head = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    for y in range(SIZE):
        for x in range(SIZE):
            p = src.getpixel((x, y))
            if p[3] == 0 or (p[:3] == (255, 255, 255) and y < 15):
                continue   # прозрачно или Z
            (head if x <= 16 and y >= 14 else body).putpixel((x, y), p)
    frames = []
    for lift in (1, 2, 2, 1):
        f = body.copy()
        f.alpha_composite(head, (0, -lift))
        frames.append(f)
    strip = Image.new("RGBA", (SIZE * len(frames), SIZE), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        strip.paste(f, (i * SIZE, 0))
    strip.save(os.path.join(OUT, "sleep_peek.png"))


def derive_eat_hold():
    """„Държи храната“ (паузите между хапките): вторият кадър на eat.png на Тут, неподвижен."""
    src = Image.open(os.path.join(OUT, "eat.png")).convert("RGBA")
    n = src.width // SIZE
    src.crop(((n - 1) * SIZE, 0, n * SIZE, SIZE)).save(os.path.join(OUT, "eat_hold.png"))


def save_clothes_layers(name, frames):
    """Шаблоните за дрехите (сиви) и точката за шапката, по един на анимация."""
    for slot, which in SLOTS.items():
        strip = Image.new("RGBA", (SIZE * len(frames), SIZE), (0, 0, 0, 0))
        any_pixel = False
        for i, f in enumerate(frames):
            ids = getattr(f, "ids", None)
            if ids is None:
                continue
            for y in range(SIZE):
                for x in range(SIZE):
                    if ids.getpixel((x, y)) != which:
                        continue
                    c = f.getpixel((x, y))
                    if c[3] == 0:
                        continue
                    # Три тона: контур, основен цвят, светло (коремчето).
                    g = 60 if c == OUTLINE else 230 if c in (LIGHT, MUZZLE) else 160
                    strip.putpixel((i * SIZE + x, y), (g, g, g, 255))
                    any_pixel = True
        path = os.path.join(LAYERS, f"{name}_{slot}.png")
        if any_pixel:
            strip.save(path)
        elif os.path.exists(path):
            os.remove(path)
    for point in ("head", "face", "neck"):
        strip = Image.new("RGBA", (SIZE * len(frames), SIZE), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            if hasattr(f, point):
                hx, hy = getattr(f, point)
                if 0 <= hy < SIZE:
                    strip.putpixel((i * SIZE + hx, hy), (255, 0, 255, 255))
        strip.save(os.path.join(LAYERS, f"{name}_{point}.png"))


# Аксесоари: очила (16 x 4, средата на реда на очите) и неща за шията (16 широки,
# горният ред е под брадичката). Стъклата са центрирани на 3.5 и 11.5, т.е. точно
# пред очите на мечока (очите са на 4 пиксела от средата).
def glasses(frame, lens=None, shape="round"):
    img = Image.new("RGBA", (16, 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for x0 in (2, 10):
        if shape == "heart":
            d.polygon([(x0, 0), (x0 + 1, 0), (x0 + 2, 1), (x0 + 3, 0), (x0 + 3, 1), (x0 + 2, 3), (x0 + 1, 3), (x0, 1)], fill=lens, outline=frame)
        elif lens:
            d.rectangle((x0, 0, x0 + 3, 3), fill=lens, outline=frame)
        else:
            d.rectangle((x0, 0, x0 + 3, 3), outline=frame)
    d.line((6, 1, 9, 1), fill=frame)          # мостче
    d.point((0, 1), fill=frame)               # дръжки
    d.point((1, 1), fill=frame)
    d.point((14, 1), fill=frame)
    d.point((15, 1), fill=frame)
    return img


def glasses_artist():
    img = glasses((60, 60, 70, 255), None)
    d = ImageDraw.Draw(img)
    d.point((12, 0), fill=(90, 140, 210, 255))   # петънца боя по рамката
    d.point((3, 3), fill=(220, 90, 80, 255))
    d.point((7, 1), fill=(255, 209, 102, 255))
    return img


def neck_scarf(main, alt=None):
    img = Image.new("RGBA", (16, 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((2, 0, 13, 2), 1, fill=main, outline=OUTLINE)
    d.rectangle((9, 2, 12, 7), fill=main, outline=OUTLINE)       # опашката виси
    if alt:
        for yy in (0, 4, 6):
            d.line((10, yy, 11, yy) if yy else (4, 1, 8, 1), fill=alt)
    d.line((10, 7, 11, 7), fill=OUTLINE)
    return img


def neck_bowtie():
    img = Image.new("RGBA", (16, 5), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    red = (200, 50, 60, 255)
    d.polygon([(4, 0), (7, 1), (7, 3), (4, 4)], fill=red, outline=OUTLINE)
    d.polygon([(11, 0), (8, 1), (8, 3), (11, 4)], fill=red, outline=OUTLINE)
    d.rectangle((7, 1, 8, 3), fill=(150, 30, 40, 255), outline=OUTLINE)
    return img


def neck_necklace():
    img = Image.new("RGBA", (16, 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gold = (230, 190, 80, 255)
    d.line((3, 0, 6, 2), fill=gold)
    d.line((12, 0, 9, 2), fill=gold)
    d.line((6, 2, 9, 2), fill=gold)
    oval(d, (5, 3, 10, 7), NUT)                                   # лешник
    d.rectangle((6, 2, 9, 4), fill=(110, 70, 30, 255), outline=OUTLINE)
    return img


ACCESSORIES = {
    "glasses_round": glasses((60, 60, 70, 255)),
    "glasses_sun": glasses((30, 30, 35, 255), (40, 40, 50, 255)),
    "glasses_heart": glasses((200, 40, 80, 255), (240, 90, 130, 255), "heart"),
    "glasses_artist": glasses_artist(),
    "neck_scarf_red": neck_scarf((214, 74, 70, 255)),
    "neck_scarf_stripes": neck_scarf((90, 140, 210, 255), (250, 250, 250, 255)),
    "neck_bowtie": neck_bowtie(),
    "neck_nut_necklace": neck_necklace(),
}


# Напитките в лапата (при пиене): рисунка 7 x 8, долната среда сяда на точката
# <anim>_cup.png. Горещите (чай, кафе) имат пара; соковете – сламка.
def drink_mug(cup, liquid, steam=True):
    img = Image.new("RGBA", (7, 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if steam:
        d.point((1, 0), fill=(235, 235, 240, 200))
        d.point((2, 1), fill=(235, 235, 240, 200))
        d.point((3, 0), fill=(235, 235, 240, 200))
    d.rectangle((0, 3, 4, 7), fill=cup, outline=OUTLINE)
    d.line((1, 3, 3, 3), fill=liquid)
    d.rectangle((5, 4, 6, 6), outline=OUTLINE)          # дръжка (към лапата)
    return img


def drink_glass(juice):
    img = Image.new("RGBA", (7, 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line((4, 0, 3, 3), fill=(240, 90, 110, 255))      # сламка
    d.rectangle((0, 2, 5, 7), fill=(225, 240, 250, 255), outline=OUTLINE)
    d.rectangle((1, 4, 4, 6), fill=juice)
    d.point((1, 3), fill=(255, 255, 255, 255))
    return img


DRINKS = {
    "drink_tea": drink_mug((255, 255, 255, 255), (200, 140, 60, 255)),
    "drink_coffee": drink_mug((235, 225, 210, 255), (90, 55, 30, 255)),
    "drink_juice_orange": drink_glass((250, 160, 40, 255)),
    "drink_juice_berry": drink_glass((150, 60, 140, 255)),
}


def food_juice(color):
    img = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.line((10, 0, 8, 5), fill=(240, 90, 110, 255))
    d.polygon([(3, 4), (12, 4), (11, 15), (4, 15)], fill=(225, 240, 250, 255), outline=OUTLINE)
    d.polygon([(4, 7), (11, 7), (10, 14), (5, 14)], fill=color)
    d.line((5, 5, 5, 6), fill=(255, 255, 255, 255))
    return img


# Храната на парчета: всяка пуканка, кюфтенце и картофка е отделна (и в няколко
# вида), за да се разлитат по екрана. Първият вид е и картинката в менюто.
POP = (255, 245, 210, 255)
POP_EDGE = (205, 170, 95, 255)


def popcorn_piece(variant):
    img = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    blobs = [
        [(1, 3, 5, 7), (4, 1, 8, 5), (4, 4, 8, 8)],
        [(2, 1, 6, 5), (1, 4, 5, 8), (5, 3, 9, 7)],
        [(1, 2, 5, 6), (5, 2, 9, 6), (3, 5, 7, 9)],
        [(2, 2, 7, 7), (5, 0, 8, 3), (0, 5, 3, 8)],
    ][variant]
    for b in blobs:
        d.ellipse(b, fill=POP, outline=POP_EDGE)
    for b in blobs:   # вътрешните ръбове се махат, за да е един пухкав облак
        d.ellipse((b[0] + 1, b[1] + 1, b[2] - 1, b[3] - 1), fill=POP)
    d.point((4 + variant % 2, 6), fill=(230, 180, 90, 255))   # люспичка
    return img


def meatball_piece(variant):
    img = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    box = [(1, 2, 8, 8), (1, 1, 8, 8), (2, 2, 8, 9), (1, 2, 9, 8)][variant]
    oval(d, box, (130, 70, 40, 255))
    d.point((box[0] + 2, box[1] + 2), fill=(175, 105, 65, 255))   # отблясък
    d.point((box[0] + 3, box[1] + 2), fill=(175, 105, 65, 255))
    if variant % 2:
        d.point((box[0] + 4, box[1] + 4), fill=(90, 140, 60, 255))  # магданоз
    d.line((3 + variant, 0, 3 + variant, 1), fill=(225, 225, 225, 200))   # пара
    return img


def potato_piece(variant):
    img = Image.new("RGBA", (10, 10), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    gold, crust = (235, 185, 75, 255), (175, 115, 40, 255)
    shapes = [
        [(1, 7), (4, 1), (8, 3), (6, 8)],
        [(1, 4), (6, 1), (9, 5), (3, 8)],
        [(2, 8), (2, 2), (7, 1), (8, 7)],
    ][variant]
    d.polygon(shapes, fill=gold, outline=crust)
    d.point((4, 4), fill=(255, 225, 140, 255))
    d.point((5, 5), fill=crust)
    return img


for _i in range(4):
    PROP_LIST[f"food_popcorn_{_i + 1}"] = (popcorn_piece(_i), 0)
    PROP_LIST[f"food_meatballs_{_i + 1}"] = (meatball_piece(_i), 0)
for _i in range(3):
    PROP_LIST[f"food_potatoes_{_i + 1}"] = (potato_piece(_i), 0)
PROP_LIST["food_popcorn"] = (popcorn_piece(0), 0)
PROP_LIST["food_meatballs"] = (meatball_piece(0), 0)
PROP_LIST["food_potatoes"] = (potato_piece(0), 0)

PROP_LIST["food_juice_orange"] = (food_juice((250, 160, 40, 255)), 0)
PROP_LIST["food_juice_berry"] = (food_juice((150, 60, 140, 255)), 0)


def save_hats():
    folder = os.path.join(os.path.dirname(__file__), "..", "assets", "clothes")
    os.makedirs(folder, exist_ok=True)
    for name, fn in HATS.items():
        img = Image.new("RGBA", (16, 10), (0, 0, 0, 0))
        fn(ImageDraw.Draw(img))
        img.save(os.path.join(folder, f"hat_{name}.png"))
    for name, img in ACCESSORIES.items():
        img.save(os.path.join(folder, f"{name}.png"))
    for name, img in DRINKS.items():
        img.save(os.path.join(folder, f"{name}.png"))


# Фонове за портрета в менюто (50 x 50; мечокът стои на реда 48, земята е долу).
BG = 50
GROUND = 42


def bands(d, colors, top=0, bottom=GROUND):
    h = (bottom - top) / len(colors)
    for i, c in enumerate(colors):
        d.rectangle((0, int(top + i * h), BG - 1, int(top + (i + 1) * h)), fill=c)


def ground(d, fill, edge):
    d.rectangle((0, GROUND, BG - 1, BG - 1), fill=fill)
    d.line((0, GROUND, BG - 1, GROUND), fill=edge)


def cloud(d, x, y, c=(255, 255, 255, 255)):
    d.ellipse((x, y + 1, x + 6, y + 5), fill=c)
    d.ellipse((x + 3, y, x + 10, y + 5), fill=c)
    d.ellipse((x + 7, y + 2, x + 12, y + 5), fill=c)


def stars(d, color, n, seed, bottom=GROUND):
    import random
    r = random.Random(seed)
    for _ in range(n):
        d.point((r.randrange(BG), r.randrange(bottom - 2)), fill=color)


def bg_meadow(d):
    bands(d, [(150, 210, 240, 255), (170, 220, 245, 255), (195, 232, 248, 255)])
    cloud(d, 5, 6)
    cloud(d, 30, 14)
    ground(d, (120, 180, 80, 255), (80, 130, 60, 255))
    for x in (4, 13, 27, 41):
        d.point((x, GROUND + 3), fill=(250, 220, 90, 255))


def bg_sunset(d):
    bands(d, [(110, 70, 140, 255), (190, 90, 120, 255), (240, 140, 90, 255), (255, 200, 110, 255)])
    d.pieslice((15, 30, 35, 50), 180, 360, fill=(255, 230, 140, 255))
    d.polygon([(0, GROUND), (12, 34), (24, GROUND)], fill=(90, 60, 90, 255))
    d.polygon([(22, GROUND), (38, 32), (BG, GROUND)], fill=(80, 50, 80, 255))
    ground(d, (70, 60, 70, 255), (50, 40, 50, 255))


def bg_night(d):
    bands(d, [(20, 25, 60, 255), (30, 40, 85, 255), (45, 60, 110, 255)])
    stars(d, (255, 255, 220, 255), 30, 1)
    d.ellipse((36, 4, 45, 13), fill=(250, 240, 200, 255))
    d.ellipse((39, 3, 47, 11), fill=(20, 25, 60, 255))
    ground(d, (40, 70, 50, 255), (25, 45, 35, 255))


def bg_beach(d):
    bands(d, [(130, 200, 240, 255), (160, 215, 245, 255)], 0, 30)
    d.ellipse((38, 4, 46, 12), fill=(255, 220, 90, 255))
    bands(d, [(60, 140, 200, 255), (80, 170, 215, 255)], 30, GROUND)
    for x in range(2, BG, 9):
        d.line((x, 33, x + 3, 33), fill=(230, 245, 255, 255))
    ground(d, (240, 215, 150, 255), (210, 180, 110, 255))
    d.point((10, 46), fill=(230, 120, 100, 255))


def bg_winter(d):
    bands(d, [(190, 210, 235, 255), (210, 225, 240, 255)])
    stars(d, (255, 255, 255, 255), 25, 2)
    d.polygon([(40, 18), (34, 34), (46, 34)], fill=(60, 120, 80, 255))
    d.polygon([(40, 24), (33, GROUND), (47, GROUND)], fill=(50, 105, 70, 255))
    d.line((34, 34, 46, 34), fill=(255, 255, 255, 255))
    ground(d, (245, 250, 255, 255), (200, 215, 235, 255))


def bg_autumn(d):
    bands(d, [(170, 205, 230, 255), (200, 220, 235, 255)])
    for x, c in [(6, (220, 120, 50, 255)), (38, (200, 80, 50, 255))]:
        d.rectangle((x + 4, 28, x + 6, GROUND), fill=(110, 70, 40, 255))
        d.ellipse((x, 14, x + 11, 30), fill=c)
    ground(d, (150, 110, 60, 255), (110, 80, 40, 255))
    for x in (3, 15, 22, 31, 44):
        d.point((x, GROUND + 2 + x % 4), fill=(230, 140, 50, 255))


def bg_space(d):
    bands(d, [(10, 10, 25, 255), (20, 15, 45, 255)])
    stars(d, (255, 255, 255, 255), 40, 3)
    d.ellipse((5, 6, 19, 20), fill=(220, 140, 90, 255))
    d.line((2, 15, 22, 11), fill=(240, 200, 140, 255))
    ground(d, (150, 150, 160, 255), (110, 110, 120, 255))
    d.ellipse((30, 45, 35, 48), fill=(120, 120, 130, 255))


def bg_underwater(d):
    bands(d, [(40, 110, 170, 255), (30, 90, 150, 255), (25, 75, 130, 255)])
    for x, y in [(8, 30), (10, 22), (9, 14), (40, 26), (41, 18)]:
        d.ellipse((x, y, x + 2, y + 2), outline=(170, 220, 250, 255))
    for x in (3, 44):
        d.line((x, GROUND, x - 1, 30), fill=(70, 160, 90, 255))
        d.line((x + 2, GROUND, x + 3, 33), fill=(70, 160, 90, 255))
    ground(d, (220, 200, 140, 255), (180, 160, 100, 255))


def bg_studio(d):
    d.rectangle((0, 0, BG - 1, GROUND), fill=(250, 238, 215, 255))
    d.rectangle((5, 8, 18, 20), fill=(255, 255, 255, 255), outline=OUTLINE)
    d.ellipse((7, 10, 11, 14), fill=(255, 209, 102, 255))
    d.polygon([(6, 19), (12, 13), (17, 19)], fill=(120, 180, 80, 255))
    d.rectangle((32, 5, 44, 16), fill=(90, 140, 210, 255), outline=OUTLINE)
    d.rectangle((34, 22, 42, 30), fill=(240, 140, 150, 255), outline=OUTLINE)
    ground(d, (170, 120, 75, 255), (120, 80, 45, 255))
    for x in range(0, BG, 10):
        d.line((x, GROUND + 1, x, BG - 1), fill=(150, 100, 60, 255))


def bg_library(d):
    d.rectangle((0, 0, BG - 1, GROUND), fill=(120, 80, 50, 255))
    colors = [(200, 60, 60, 255), (90, 140, 210, 255), (120, 180, 80, 255), (255, 209, 102, 255), (150, 100, 200, 255)]
    for row, y in enumerate((4, 16, 28)):
        d.line((0, y + 10, BG - 1, y + 10), fill=(80, 50, 30, 255))
        x = 1 + row
        i = row
        while x < BG - 2:
            w = 2 + (i % 2)
            d.rectangle((x, y + 2 + i % 3, x + w - 1, y + 9), fill=colors[i % len(colors)])
            x += w + 1
            i += 1
    ground(d, (150, 60, 60, 255), (110, 40, 40, 255))


def bg_candy(d):
    bands(d, [(250, 200, 220, 255), (255, 220, 235, 255)])
    cloud(d, 4, 5, (255, 240, 250, 255))
    cloud(d, 30, 12, (255, 240, 250, 255))
    for x, c in [(10, (240, 120, 150, 255)), (38, (130, 200, 230, 255))]:
        d.line((x, GROUND, x, 30), fill=(255, 255, 255, 255))
        d.ellipse((x - 4, 22, x + 4, 30), fill=c)
    ground(d, (255, 255, 255, 255), (240, 120, 150, 255))
    for x in range(0, BG, 4):
        d.line((x, GROUND + 1, x + 3, BG - 1), fill=(240, 120, 150, 255))


BACKGROUNDS = {
    "meadow": bg_meadow, "sunset": bg_sunset, "night": bg_night, "beach": bg_beach, "winter": bg_winter,
    "autumn": bg_autumn, "space": bg_space, "underwater": bg_underwater, "studio": bg_studio,
    "library": bg_library, "candy": bg_candy,
}


def mystery(d):
    """Кутия с „?“: нещо, което ще дойде по-късно."""
    d.rectangle((1, 2, 14, 15), fill=(200, 160, 100, 255), outline=OUTLINE)
    d.line((1, 5, 14, 5), fill=OUTLINE)
    q = [".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.."]
    for y, row in enumerate(q):
        for x, ch in enumerate(row):
            if ch == "#":
                d.point((5 + x, 7 + y), fill=WHITE)


PROP_LIST["mystery"] = (prop(16, 16, mystery), 0)


def gift(opened, box=(120, 180, 230, 255), dark=(80, 130, 190, 255), ribbon=(240, 90, 110, 255)):
    """Подаръкът, който мечокът носи (затворен или с отворен капак)."""
    def f(d):
        d.rectangle((2, 7, 13, 15), fill=box, outline=OUTLINE)
        d.rectangle((3, 13, 12, 14), fill=dark)
        d.rectangle((7, 7, 8, 15), fill=ribbon)
        if opened:
            # Капакът е отхвръкнал наляво, отвътре блести.
            d.rectangle((0, 1, 9, 4), fill=box, outline=OUTLINE)
            d.rectangle((4, 1, 5, 4), fill=ribbon)
            for x, y in [(6, 5), (10, 4), (13, 6), (8, 3)]:
                d.point((x, y), fill=(255, 230, 120, 255))
        else:
            d.rectangle((1, 4, 14, 7), fill=box, outline=OUTLINE)
            d.rectangle((7, 4, 8, 7), fill=ribbon)
            d.ellipse((3, 0, 7, 4), outline=ribbon)   # панделка
            d.ellipse((8, 0, 12, 4), outline=ribbon)
    return f


PROP_LIST["gift"] = (prop(16, 16, gift(False)), 0)
PROP_LIST["gift_open"] = (prop(16, 16, gift(True)), 0)

# Цветни подаръци: gift_<цвят>[_open] (кутия, тъмно, панделка). Ако Тут е нарисувала
# подаръка (done_art/gift.png, gift_hold.png, walk_gift.png), цветовете са нейната
# рисунка, пребоядисана (виж save_gifts – тя пише и върху тези временни).
GIFT_COLORS = {
    "pink": ((245, 160, 185, 255), (215, 115, 150, 255), (255, 255, 255, 255)),
    "green": ((130, 190, 100, 255), (90, 145, 70, 255), (250, 210, 80, 255)),
    "yellow": ((250, 210, 90, 255), (220, 170, 50, 255), (220, 70, 70, 255)),
    "purple": ((170, 130, 220, 255), (125, 90, 180, 255), (255, 220, 120, 255)),
    "red": ((220, 70, 70, 255), (170, 45, 50, 255), (250, 220, 120, 255)),
}
for _c, (_box, _dark, _rib) in GIFT_COLORS.items():
    PROP_LIST[f"gift_{_c}"] = (prop(16, 16, gift(False, _box, _dark, _rib)), 0)
    PROP_LIST[f"gift_{_c}_open"] = (prop(16, 16, gift(True, _box, _dark, _rib)), 0)


def save_backgrounds():
    folder = os.path.join(os.path.dirname(__file__), "..", "assets", "backgrounds")
    os.makedirs(folder, exist_ok=True)
    for name, fn in BACKGROUNDS.items():
        img = Image.new("RGBA", (BG, BG), (0, 0, 0, 255))
        fn(ImageDraw.Draw(img))
        img.save(os.path.join(folder, f"bg_{name}.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    manifest = {"frameWidth": SIZE, "frameHeight": SIZE, "scale": 3, "animations": {}}
    # Скоростите, нагласени на ръка в anim.json, остават.
    try:
        with open(os.path.join(OUT, "anim.json"), encoding="utf-8") as fh:
            old = json.load(fh)["animations"]
    except (OSError, ValueError, KeyError):
        old = {}
    preview = Image.new("RGBA", (SIZE * 4 + 5, (SIZE + 1) * len(ANIMS)), (90, 120, 90, 255))
    for row, (name, (frames, fps, loop)) in enumerate(ANIMS.items()):
        if name in HAND_DRAWN:
            drawn = Image.open(os.path.join(OUT, f"{name}.png")).convert("RGBA")
            for i in range(min(4, drawn.width // SIZE)):
                preview.alpha_composite(drawn.crop((i * SIZE, 0, i * SIZE + SIZE, SIZE)), (i * (SIZE + 1), row * (SIZE + 1)))
            manifest["animations"][name] = {"file": f"{name}.png", "fps": fps, "loop": loop}
            continue
        strip = Image.new("RGBA", (SIZE * len(frames), SIZE), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            strip.paste(f, (i * SIZE, 0))
            if i < 4:
                preview.alpha_composite(f, (i * (SIZE + 1), row * (SIZE + 1)))
        strip.save(os.path.join(OUT, f"{name}.png"))
        save_clothes_layers(name, frames)
        manifest["animations"][name] = {"file": f"{name}.png", "fps": fps, "loop": loop}
    for name, (fps, loop) in EXTRA_DRAWN.items():
        manifest["animations"][name] = {"file": f"{name}.png", "fps": fps, "loop": loop}
    if "sleep_peek" not in HAND_DRAWN:
        derive_sleep_peek()
    if not os.path.exists(os.path.join(os.path.dirname(__file__), "..", "done_art", "eat_hold.png")):
        derive_eat_hold()
    manifest["animations"]["eat_hold"] = {"file": "eat_hold.png", "fps": 1, "loop": True}
    for name, entry in old.items():
        manifest["animations"][name] = entry if name in manifest["animations"] or os.path.exists(os.path.join(OUT, entry.get("file", name + ".png"))) else None
    manifest["animations"] = {k: v for k, v in manifest["animations"].items() if v}
    manifest["animations"]["sleep_peek"] = {"file": "sleep_peek.png", "fps": 3, "loop": False}
    with open(os.path.join(OUT, "anim.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
        fh.write("\n")
    # Иконки за магазина от предметите за почивка: смалени до 16 x 16.
    for shop, src in [("shop_easel", "easel"), ("shop_computer", "computer"), ("shop_yoga_mat", "mat"),
                      ("shop_plant", "plant"), ("shop_radio", "radio"), ("shop_board_game", "table_board"), ("shop_trampoline", "trampoline"),
                      ("shop_couch", "couch"), ("shop_bed_real", "bed"), ("shop_wardrobe", "wardrobe"), ("shop_heater", "heater"),
                      ("shop_yarn_basket", "yarn_basket"), ("shop_lamp", "lamp")]:
        img, _ = PROP_LIST[src]
        icon = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
        scale = min(1.0, 16 / img.width, 16 / img.height)
        small = img.resize((max(1, int(img.width * scale)), max(1, int(img.height * scale))), Image.NEAREST)
        icon.alpha_composite(small, ((16 - small.width) // 2, (16 - small.height) // 2))
        PROP_LIST[shop] = (icon, 0)

    os.makedirs(PROPS, exist_ok=True)
    props = {"scale": 3, "props": {}}
    for name, (img, seat) in PROP_LIST.items():
        # Храната е нарисувана от Тут (done_art/food → assets/props/food/): временната не я сменя.
        if name.startswith("food_"):
            continue
        img.save(pl.path(name))
        props["props"][name] = {"file": pl.rel(name), "seat": seat}
    with open(os.path.join(PROPS, "props.json"), "w", encoding="utf-8") as fh:
        json.dump(props, fh, indent=2, ensure_ascii=False)
        fh.write("\n")

    save_hats()
    save_tools()
    save_gifts()
    save_backgrounds()
    preview.resize((preview.width * 4, preview.height * 4), Image.NEAREST).save(
        os.path.join(os.path.dirname(__file__), "placeholders_preview.png"))


if __name__ == "__main__":
    main()
