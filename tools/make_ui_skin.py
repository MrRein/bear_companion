"""Спрайтовете на менюто (рамка, хартия, табове, бутони, деленца, плочки…) в стила на храната.

    python tools/make_ui_skin.py            -> assets/ui/skin/*.png и skin.json
    python tools/make_ui_skin.py <папка>    -> само в тази папка (за преглед)

Всеки спрайт се „реже“ на 9 части (9-slice): ъглите стоят както са нарисувани, ръбовете и
средата се повтарят (не се разтягат), за да остават пикселите цели. В skin.json за всеки:
  slice – колко пиксела от всяка страна са ръб (ляво, горе, дясно, долу);
  pad   – колко навътре започва съдържанието (текстът), в пиксели на спрайта.
Един пиксел на спрайта = един пиксел на храната в менюто.

Нарисувани са в цветовете на класическия скин („Дъб и мед“); другите скинове ги
преоцветяват сами. Искаш ли да ги прерисуваш на ръка – отвори PNG-то, рисувай в същия
размер и пусни програмата (скриптът е само първият вариант).
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


def save(name, s, slc, pad, out):
    s.img.save(os.path.join(out, name + ".png"))
    DEFS[name] = {"slice": list(slc), "pad": list(pad)}


# ───────────────────────── Рамка (дърво) ─────────────────────────

def wood_px(x, y):
    """Дъски по 5 реда, с фуги и жилки; повтаря се през 20 пиксела."""
    row = y // 5
    ox = (x + (10 if row % 2 else 0)) % 20
    yy = y % 5
    if yy == 4 or ox == 19:
        return WOODD
    if yy == 0:
        return WOODL
    if yy == 2 and 2 <= ox <= 6:
        return WOODL
    if yy == 2 and 10 <= ox <= 14:
        return WOODD
    return WOOD


def frame(out):
    s = S(30, 30)
    s.fill(wood_px)
    s.outline()
    s.corners(2, 2, 2, 2)
    save("frame", s, (5, 5, 5, 5), (1, 1, 1, 1), out)


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
    save(name, s, (2, 2, 2, 2), (1, 1, 1, 1), out)


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
    frame(out)
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
    with open(os.path.join(out, "skin.json"), "w", encoding="utf-8") as fh:
        json.dump(DEFS, fh, indent=1)
    print("Спрайтове:", len(DEFS), "→", os.path.relpath(out, ROOT) if out == OUT else out)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else OUT)
