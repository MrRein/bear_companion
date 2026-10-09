"""Рисува временните (placeholder) спрайтове на мечока в assets/bear/.

Всяка анимация е лента от кадри 32x32, един до друг отляво надясно.
Истинските рисунки просто заменят тези PNG файлове (може и с друг брой кадри).

    python tools/make_placeholders.py
"""

import json
import os

from PIL import Image, ImageDraw

SIZE = 32
OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "bear")

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
    preview.resize((preview.width * 4, preview.height * 4), Image.NEAREST).save(
        os.path.join(os.path.dirname(__file__), "placeholders_preview.png"))


if __name__ == "__main__":
    main()
