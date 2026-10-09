"""Рисува временните (placeholder) спрайтове в assets/bear/ и assets/props/.

Всяка анимация е лента от кадри 32x32, един до друг отляво надясно.
Истинските рисунки просто заменят тези PNG файлове (може и с друг брой кадри).

    python tools/make_placeholders.py
"""

import json
import os

from PIL import Image, ImageDraw

SIZE = 32
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "bear")
PROPS = os.path.join(os.path.dirname(__file__), "..", "assets", "props")

OUTLINE = (59, 36, 20, 255)
FUR = (139, 90, 43, 255)
LIGHT = (196, 138, 79, 255)
MUZZLE = (232, 196, 150, 255)
BLACK = (25, 18, 12, 255)
PINK = (240, 140, 150, 255)
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


def bear(eyes="open", mouth="none", arms="down", legs="stand", bob=0, ears="up", extra=None):
    """Мечок отпред. bob мести всичко без стъпалата нагоре/надолу."""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
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
    for f in feet:
        oval(d, f, FUR)

    # уши
    if ears == "up":
        oval(d, (5, 1 + y, 11, 7 + y), FUR)
        oval(d, (20, 1 + y, 26, 7 + y), FUR)
        d.rectangle((7, 3 + y, 8, 4 + y), fill=MUZZLE)
        d.rectangle((22, 3 + y, 23, 4 + y), fill=MUZZLE)
    else:  # клепнали
        oval(d, (3, 4 + y, 9, 9 + y), FUR)
        oval(d, (22, 4 + y, 28, 9 + y), FUR)

    # тяло и коремче
    oval(d, (8, 15 + y, 23, 29 + y), FUR)
    oval(d, (12, 19 + y, 19, 27 + y), LIGHT, outline=None)

    # ръце
    left, right = {
        "down": ((5, 17, 9, 25), (22, 17, 26, 25)),
        "up": ((4, 8, 8, 17), (23, 8, 27, 17)),
        "left_up": ((4, 8, 8, 17), (22, 17, 26, 25)),
        "right_up": ((5, 17, 9, 25), (23, 8, 27, 17)),
        "front": ((10, 18, 15, 23), (16, 18, 21, 23)),
    }[arms]
    oval(d, (left[0], left[1] + y, left[2], left[3] + y), FUR)
    oval(d, (right[0], right[1] + y, right[2], right[3] + y), FUR)

    # глава и муцуна
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
    return img


def sleeping(z):
    """Мечок, свит на кълбо, с Z-та над него."""
    img = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    breathe = z % 2
    oval(d, (3, 18 - breathe, 28, 31), FUR)
    oval(d, (4, 15, 10, 20), FUR)  # ухо
    oval(d, (3, 19, 15, 30), FUR)  # глава
    oval(d, (4, 24, 10, 29), MUZZLE, outline=None)
    d.rectangle((4, 25, 5, 26), fill=BLACK)
    d.line((8, 22, 10, 22), fill=BLACK)  # затворено око
    oval(d, (20, 25, 27, 31), FUR)  # краче
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


def couch():
    """Диван отпред, 48x24. Седалката е на 10 пиксела от земята."""
    img = Image.new("RGBA", (48, 24), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((4, 21, 6, 23), fill=WOOD)  # крачета
    d.rectangle((41, 21, 43, 23), fill=WOOD)
    d.rounded_rectangle((4, 0, 43, 15), 4, fill=COUCH_DARK, outline=OUTLINE)  # облегалка
    d.rounded_rectangle((5, 12, 42, 21), 2, fill=COUCH, outline=OUTLINE)  # седалка
    d.line((24, 13, 24, 20), fill=OUTLINE)
    d.line((7, 13, 22, 13), fill=COUCH_LIGHT)
    d.line((26, 13, 40, 13), fill=COUCH_LIGHT)
    d.rounded_rectangle((0, 8, 7, 21), 3, fill=COUCH, outline=OUTLINE)  # облегалки за ръце
    d.rounded_rectangle((40, 8, 47, 21), 3, fill=COUCH, outline=OUTLINE)
    return img


ANIMS = {
    # име: (кадри, кадри в секунда, повтаря ли се)
    "idle": ([bear(), bear(bob=1), bear(), bear(eyes="closed")], 3, True),
    "walk": ([bear(legs="step1"), bear(bob=-1), bear(legs="step2"), bear(bob=-1)], 8, True),
    "sleep": ([sleeping(i) for i in range(3)] + [sleeping(2)], 2, True),
    "yawn": ([bear(eyes="closed", mouth="open"), bear(eyes="closed", mouth="big", arms="up"),
              bear(eyes="closed", mouth="big", arms="up"), bear(eyes="closed", mouth="open")], 3, False),
    "eat": ([bear(arms="front", mouth="open", extra=nut), bear(arms="front", mouth="none", extra=nut),
             bear(arms="front", mouth="open", extra=lambda d, y: nut(d, y, True)), bear(eyes="happy", mouth="smile")], 4, False),
    "happy": ([bear(eyes="happy", mouth="smile", arms="up", bob=-3, legs="dangle"),
               bear(eyes="happy", mouth="smile", arms="up", bob=-1),
               bear(eyes="happy", mouth="smile")], 6, False),
    "dance": ([bear(eyes="happy", mouth="smile", arms="left_up", legs="step1"),
               bear(eyes="happy", mouth="open", arms="up", bob=-1),
               bear(eyes="happy", mouth="smile", arms="right_up", legs="step2"),
               bear(eyes="happy", mouth="open", arms="up", bob=-1)], 6, True),
    "work": ([bear(arms="front", extra=pencil(0)), bear(arms="front", extra=pencil(-2)),
              bear(arms="front", eyes="closed", extra=pencil(0)), bear(arms="front", extra=pencil(-2))], 3, True),
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


def butterfly():
    img = Image.new("RGBA", (9, 7), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    wing = (250, 150, 200, 255)
    oval(d, (0, 0, 3, 3), wing)
    oval(d, (5, 0, 8, 3), wing)
    oval(d, (1, 3, 3, 6), (180, 140, 250, 255))
    oval(d, (5, 3, 7, 6), (180, 140, 250, 255))
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


PROP_LIST = {
    # име: (рисунка, на колко пиксела от земята е седалката)
    "couch": (couch(), 10),
    "butterfly": (butterfly(), 0),
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
}


def main():
    os.makedirs(OUT, exist_ok=True)
    manifest = {"frameWidth": SIZE, "frameHeight": SIZE, "scale": 3, "animations": {}}
    preview = Image.new("RGBA", (SIZE * 4 + 5, (SIZE + 1) * len(ANIMS)), (90, 120, 90, 255))
    for row, (name, (frames, fps, loop)) in enumerate(ANIMS.items()):
        strip = Image.new("RGBA", (SIZE * len(frames), SIZE), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            strip.paste(f, (i * SIZE, 0))
            if i < 4:
                preview.alpha_composite(f, (i * (SIZE + 1), row * (SIZE + 1)))
        strip.save(os.path.join(OUT, f"{name}.png"))
        manifest["animations"][name] = {"file": f"{name}.png", "fps": fps, "loop": loop}
    with open(os.path.join(OUT, "anim.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=2, ensure_ascii=False)
        fh.write("\n")
    os.makedirs(PROPS, exist_ok=True)
    props = {"scale": 3, "props": {}}
    for name, (img, seat) in PROP_LIST.items():
        img.save(os.path.join(PROPS, f"{name}.png"))
        props["props"][name] = {"file": f"{name}.png", "seat": seat}
    with open(os.path.join(PROPS, "props.json"), "w", encoding="utf-8") as fh:
        json.dump(props, fh, indent=2, ensure_ascii=False)
        fh.write("\n")

    preview.resize((preview.width * 4, preview.height * 4), Image.NEAREST).save(
        os.path.join(os.path.dirname(__file__), "placeholders_preview.png"))


if __name__ == "__main__":
    main()
