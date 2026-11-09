"""Иконките на менюто в стила на храната: черен контур и три тона (светло, нормално, тъмно).

    python tools/stylize_icons.py

Оригиналите (плоските, с кафяв контур) стоят в art_templates/ui_flat/ – оттам се правят
assets/ui/*.png всеки път наново (пускането два пъти не ги затъмнява още). Нова или
пренарисувана иконка: сложи я в art_templates/ui_flat/ (12×12) и пусни скрипта.
Емоциите (emote_*) и no_bubble не се пипат; нарисуваните на ръка (art_templates/ui_drawn,
tools/draw_icons.py) се взимат както са.

Как: тъмните пиксели (контурът, очи…) стават черни; всеки цвят вътре – по-светъл там,
където горе/вляво свършва петното му, и по-тъмен там, където свършва долу/вдясно.
"""

import os
import shutil

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..")
UI = os.path.join(ROOT, "assets", "ui")
FLAT = os.path.join(ROOT, "art_templates", "ui_flat")
DRAWN = os.path.join(ROOT, "art_templates", "ui_drawn")     # нарисувани на ръка – не се пипат

BLACK = (13, 18, 28, 255)
WHITE = (255, 255, 255)


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def stylize(img):
    img = img.convert("RGBA")
    w, h = img.size
    src = img.load()
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dst = out.load()

    def at(x, y):
        return src[x, y] if 0 <= x < w and 0 <= y < h else (0, 0, 0, 0)

    def dark(c):
        return c[3] > 0 and lum(c) < 75

    for y in range(h):
        for x in range(w):
            c = src[x, y]
            if c[3] == 0:
                continue
            if dark(c):
                dst[x, y] = (BLACK[0], BLACK[1], BLACK[2], c[3])
                continue
            same = lambda dx, dy: at(x + dx, y + dy)[:3] == c[:3] and at(x + dx, y + dy)[3] > 0
            # свършва ли петното горе/вляво (светло) или долу/вдясно (тъмно)
            top_left = not same(0, -1) or not same(-1, 0)
            bottom_right = not same(0, 1) or not same(1, 0)
            rgb = c[:3]
            if top_left and not bottom_right:
                rgb = mix(rgb, WHITE, 0.38)
            elif bottom_right and not top_left:
                rgb = mix(rgb, BLACK[:3], 0.28)
            dst[x, y] = (rgb[0], rgb[1], rgb[2], c[3])
    return out


def icons():
    return sorted(f for f in os.listdir(UI) if f.endswith(".png") and not f.startswith("emote_"))


def main():
    os.makedirs(FLAT, exist_ok=True)
    n = 0
    for f in icons():
        drawn = os.path.join(DRAWN, f)
        if os.path.exists(drawn):
            shutil.copy(drawn, os.path.join(UI, f))
            n += 1
            continue
        flat = os.path.join(FLAT, f)
        if not os.path.exists(flat):          # първия път: пазим оригинала
            shutil.copy(os.path.join(UI, f), flat)
        stylize(Image.open(flat)).save(os.path.join(UI, f))
        n += 1
    print("Иконки:", n)


if __name__ == "__main__":
    main()
