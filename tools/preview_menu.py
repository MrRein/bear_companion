"""Как изглежда менюто – сглобено от спрайтовете (assets/ui/skin), иконките и храната.

    python tools/preview_menu.py [папка]   -> menu_main.png, menu_food.png (уголемени ×2)

Режи спрайтовете като програмата (9-slice по skin.json, ръбовете и средата се повтарят),
затова прерисуваш ли спрайт – пусни това и виж менюто веднага. Размерите и текстът са
приблизителни (в програмата ги нарежда WPF).
"""

import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
SKIN = os.path.join(ROOT, "assets", "ui", "skin")
UI = os.path.join(ROOT, "assets", "ui")
FOOD = os.path.join(ROOT, "assets", "props", "food")
FONTS = os.path.join(ROOT, "src", "Mecho", "fonts")
P = 4                       # един пиксел на спрайта/храната = 4 точки (при 100%)

INK = (13, 18, 28)
CREAM, MUTED = (255, 248, 230), (125, 98, 72)
HONEY, GOOD, OKAY = (255, 209, 102), (126, 188, 89), (240, 180, 70)

f = ImageFont.truetype(os.path.join(FONTS, "PixeloidSans.ttf"), 18)
fb = ImageFont.truetype(os.path.join(FONTS, "PixeloidSans-Bold.ttf"), 18)
fbig = ImageFont.truetype(os.path.join(FONTS, "PixeloidSans-Bold.ttf"), 27)

with open(os.path.join(SKIN, "skin.json"), encoding="utf-8") as fh:
    DEFS = json.load(fh)


def with_shadow(art):
    """Като Ui.WithShadow: тънка полупрозрачна черта под долния ред на храната."""
    w, h = art.size
    solid = lambda x, y: art.getpixel((x, y))[3] > 128
    bottom = max((y for y in range(h) for x in range(w) if solid(x, y)), default=-1)
    if bottom < 0:
        return art
    xs = [x for y in range(max(0, bottom - 1), bottom + 1) for x in range(w) if solid(x, y)]
    out = Image.new("RGBA", (w, max(h, bottom + 2)), (0, 0, 0, 0))
    out.paste(art, (0, 0))
    for x in range(min(xs), max(xs) + 1):
        if out.getpixel((x, bottom + 1))[3] == 0:
            out.putpixel((x, bottom + 1), (13, 18, 28, 40 if x in (min(xs), max(xs)) else 80))
    return out


def resolve(name):
    while name not in DEFS and "_" in name:
        name = name.rsplit("_", 1)[0]
    return name


class Canvas:
    def __init__(self, w, h):
        self.W, self.H = w, h
        self.img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.img)

    def sprite(self, name, x, y, w, h):
        """9-slice: ъглите както са, ръбовете и средата се повтарят. Връща вътрешността (pad)."""
        name = resolve(name)
        d = DEFS[name]
        src = Image.open(os.path.join(SKIN, name + ".png")).convert("RGBA")
        L, T, R, B = d["slice"]
        iw, ih = src.size
        sx = [0, L, iw - R, iw]
        sy = [0, T, ih - B, ih]
        tx = [0, min(L * P, w // 2), w - min(R * P, w // 2), w]
        ty = [0, min(T * P, h // 2), h - min(B * P, h // 2), h]
        layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        for row in range(3):
            for col in range(3):
                pw, ph = sx[col + 1] - sx[col], sy[row + 1] - sy[row]
                rw, rh = tx[col + 1] - tx[col], ty[row + 1] - ty[row]
                if pw <= 0 or ph <= 0 or rw <= 0 or rh <= 0:
                    continue
                part = src.crop((sx[col], sy[row], sx[col + 1], sy[row + 1])).resize((pw * P, ph * P), Image.NEAREST)
                # Ъгълът/ръбът отдясно и отдолу започва от края.
                ox = tx[3] - pw * P if col == 2 else tx[col]
                oy = ty[3] - ph * P if row == 2 else ty[row]
                region = Image.new("RGBA", (rw, rh), (0, 0, 0, 0))
                for yy in range(oy - ty[row] - ph * P * 50, rh, ph * P):
                    for xx in range(ox - tx[col] - pw * P * 50, rw, pw * P):
                        if xx + pw * P > 0 and yy + ph * P > 0:
                            region.alpha_composite(part, (max(xx, 0), max(yy, 0)), (max(-xx, 0), max(-yy, 0)))
                layer.alpha_composite(region, (tx[col], ty[row]))
        self.img.alpha_composite(layer, (x, y))
        pl, pt, pr, pb = d["pad"]
        return x + pl * P, y + pt * P, w - (pl + pr) * P, h - (pt + pb) * P

    def shadow(self, x, y, w, h):
        sh = Image.new("RGBA", (w, h), INK + (56,))
        self.img.alpha_composite(sh, (x, y + P))

    def icon(self, name, x, y, folder=UI, scale=2):
        im = Image.open(os.path.join(folder, name + ".png")).convert("RGBA")
        if folder == FOOD:
            im = with_shadow(im)
        im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        self.img.alpha_composite(im, (int(x), int(y)))

    def text(self, x, y, s, font=f, fill=INK):
        self.d.text((x, y), s, font=font, fill=fill)

    def tw(self, s, font=f):
        return self.d.textlength(s, font=font)

    def save(self, path):
        big = self.img.resize((self.W * 2, self.H * 2), Image.NEAREST)
        bg = Image.new("RGBA", big.size, (222, 230, 236, 255))
        bg.alpha_composite(big)
        bg.save(path)
        print(path)


TABS = ["bear", "food", "shop", "wardrobe", "notes", "tasks", "gear"]


def chrome(c, sel):
    W, H = c.W, c.H
    c.sprite("frame", 0, 0, W, H)
    c.icon("bear", 16, 14)
    c.text(52, 16, "МЕЧО", fbig, INK)
    c.text(50, 14, "МЕЧО", fbig, CREAM)
    px = W - 16 - 40 - 8 - 104
    ix, iy, _, _ = c.sprite("pill", px, 12, 104, 36)
    c.icon("nut", ix + 8, iy + 2)
    c.text(ix + 40, iy + 4, "128", fb)
    c.sprite("button", W - 16 - 40, 10, 40, 40)
    c.icon("close", W - 16 - 40 + 8, 16)
    tx0, ty, gap = 12, 60, P
    tabw = (W - 24 - gap * (len(TABS) - 1)) // len(TABS)
    ptop = ty + 52
    for i, name in enumerate(TABS):
        x = tx0 + i * (tabw + gap)
        if i == sel:
            continue
        c.sprite("tab", x, ty + P, tabw, 48)
        c.icon(name, x + (tabw - 36) // 2, ty + 12, scale=3)
    ix, iy, iw, ih = c.sprite("panel", 12, ptop, W - 24, H - ptop - 12)
    x = tx0 + sel * (tabw + gap)
    c.sprite("tab_on", x, ty, tabw, 52 + P)
    c.icon(TABS[sel], x + (tabw - 36) // 2, ty + 10, scale=3)
    return ix, iy, iw, ih


def button(c, x, y, w, h, label, icon=None, sprite="button"):
    if not sprite.startswith("button_on"):
        c.shadow(x, y, w, h)
    ix, iy, iw, ih = c.sprite(sprite, x, y, w, h)
    total = (30 if icon else 0) + c.tw(label, fb)
    lx = ix + (iw - total) / 2
    ty = iy + (ih - 22) / 2
    if icon:
        c.icon(icon, lx, ty + 1)
        lx += 30
    c.text(lx, ty + 2, label, fb)


def ribbon(c, x, y, w, label, icon):
    c.icon(icon, x, y + 2)
    c.text(x + 30, y + 4, label, fb)
    dash = Image.open(os.path.join(SKIN, "dash.png")).convert("RGBA")
    dash = dash.resize((dash.width * P, dash.height * P), Image.NEAREST)
    for xx in range(x, x + w - dash.width, dash.width):
        c.img.alpha_composite(dash, (xx, y + 32))


def main_tab(out):
    c = Canvas(620, 612)
    x0, y0, pw, ph = chrome(c, 0)
    cx, cy = x0 + 16, y0 + 16
    c.sprite("tile_art", cx, cy, 144, 144)
    bear = Image.open(os.path.join(ROOT, "assets/bear/idle.png")).convert("RGBA").crop((0, 0, 32, 32)).resize((128, 128), Image.NEAREST)
    c.img.alpha_composite(bear, (cx + 8, cy + 12))
    sx = cx + 144 + 18
    c.text(sx, cy + 4, "Доволен", fbig)
    c.icon("w_partly", sx, cy + 44)
    c.text(sx + 32, cy + 46, "14°")
    for k, (ic, label, val, cell) in enumerate((("honey", "Ситост", 8, "cell_good"), ("energy", "Енергия", 4, "cell_okay"))):
        y = cy + 78 + k * 34
        c.icon(ic, sx, y + 2)
        c.text(sx + 30, y + 4, label, fb)
        for i in range(10):
            c.sprite(cell if i < val else "cell_empty", sx + 126 + i * 4 * P, y, 5 * P, 6 * P)
    ry = cy + 162
    ribbon(c, cx, ry, pw - 32, "Какво да правим?", "star")
    buttons = [("food", "Нахрани"), ("moon", "Приспи"), ("heart", "Погали"),
               ("quiet", "Тихо"), ("walk", "Разходи се"), ("relax", "Почивай си"),
               ("tasks", "Работи си"), ("stop", "Спри"), ("couch", "Неща на Мечо")]
    bw, bh = (pw - 32 - 16) // 3, 44
    for k, (ic, label) in enumerate(buttons):
        x = cx + (k % 3) * (bw + 8)
        y = ry + 50 + (k // 3) * (bh + 10)
        sprite = "button_on" if label == "Почивай си" else "button_hover" if label == "Погали" else "button"
        button(c, x, y, bw, bh, label, ic, sprite)
    c.save(os.path.join(out, "menu_main.png"))


def food_tab(out):
    c = Canvas(620, 800)
    x0, y0, pw, ph = chrome(c, 1)
    cx, cy = x0 + 16, y0 + 14
    c.text(cx, cy, "Сготви нещо – уредът сам ще се сложи.", f, MUTED)
    cy += 34
    c.icon("book", cx, cy - 4, scale=3)
    c.text(cx + 44, cy + 4, "Следваща рецепта:", fb)
    px = cx + 44 + c.tw("Следваща рецепта: ", fb)
    for i in range(3):
        c.sprite("cell_honey" if i < 2 else "cell_empty", int(px) + i * 4 * P, cy + 2, 3 * P, 4 * P)
    c.text(px + 12 * P + 8, cy + 4, "2/3 страници  (?)", f)
    cy += 40
    x = cx
    for k, cat in enumerate(["Всичко", "Манджи", "Закуски", "Сладко", "Напитки"]):
        w = int(c.tw(cat, fb)) + 16 + 2 * P
        if x + w > cx + pw - 32:
            x = cx
            cy += 46
        sprite = "button_on" if k == 0 else "button"
        button(c, x, cy, w, 18 + 7 + 3 * P, cat, None, sprite)
        x += w + 6
    cy += 52
    foods = [("pancakes", "Палачинки", 80, 0, "2 мин", 5), ("soup_veg", "Зеленчукова супа", 30, 0, "4 мин", 8),
             ("pizza_margarita", "Пица Маргарита", 60, 0, "5 мин", 12), ("latte", "Лате", 0, 3, "1 мин", 4),
             ("berries", "Боровинки", 10, 0, "", 0), ("waffle_chocolate", "Шоколадов гофрет", 50, 0, "3 мин", 7)]
    tw_ = (pw - 32 - 16) // 3
    th = 300
    for k, (art, name, gain, egain, time, cost) in enumerate(foods):
        tx = cx + (k % 3) * (tw_ + 8)
        ty = cy + (k // 3) * (th + 8)
        ix, iy, iw, ih = c.sprite("tile_hover" if k == 1 else "tile", tx, ty, tw_, th)
        ax, ay, aw = ix + 8, iy + 8, iw - 16
        c.sprite("tile_art", ax, ay, aw, 96)
        c.icon(art, ax + (aw - 64) // 2, ay + 16, folder=FOOD, scale=4)
        if k == 2:
            c.icon("heart", ax + aw - 30, ay + 6)
        ny = ay + 106
        lines = [name] if c.tw(name, fb) < aw else name.split(" ", 1)
        for li, ln in enumerate(lines):
            c.text(ix + (iw - c.tw(ln, fb)) / 2, ny + li * 22, ln, fb)
        gy = ny + 22 * len(lines) + 8

        def gbar(y, ic, now, add, col):
            bwid = 10 * 2 * P + 2 * P
            bx = ix + (iw - (28 + bwid)) / 2
            c.icon(ic, bx, y)
            bx = int(bx + 28)
            c.d.rectangle([bx, y + 4, bx + bwid - 1, y + 4 + 4 * P - 1], fill=INK)
            hv, wl = round(now / 10), min(10, round((now + add) / 10))
            for i in range(10):
                ci = col if i < hv else HONEY if i < wl else (214, 190, 150)
                x1 = bx + P + i * 2 * P
                c.d.rectangle([x1, y + 4 + P, x1 + 2 * P - 1, y + 4 + 3 * P - 1], fill=ci)

        gbar(gy, "honey", 40, gain, OKAY)
        gy += 26
        if egain:
            gbar(gy, "energy", 30, egain * 10, OKAY)
            gy += 26
        if time:
            w2 = 30 + c.tw(time)
            c.icon("clock", ix + (iw - w2) / 2, gy)
            c.text(ix + (iw - w2) / 2 + 30, gy + 2, time, f, (60, 60, 64))
        by = iy + ih - 8 - 44
        button(c, ix + 8, by, iw - 16, 44, f"Сготви  {cost}" if cost else "Набери", "nut" if cost else None, "button_honey")
    c.save(os.path.join(out, "menu_food.png"))


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "art_templates")
    os.makedirs(out, exist_ok=True)
    main_tab(out)
    food_tab(out)
