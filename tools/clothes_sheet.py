"""Лист за маркиране на дрехите: всички нарисувани анимации на един файл.

    python tools/clothes_sheet.py              прави листовете в art_templates/
    python tools/clothes_sheet.py import       чете маркирания лист (done_art/clothes_sheet.png)

art_templates/clothes_sheet.png – мечокът без дрехи, кадрите един до друг, по
един ред на анимация (размерът е 1:1, пиксел за пиксел). Тут го отваря в
Aseprite, на нов слой оцветява къде седи всяка дреха (цветовете са в README →
„Картата за дрехите“), изнася го цял като clothes_sheet.png и го качва в
done_art/. При import всеки ред, в който има маркировка, става карта
assets/clothes/paint/<anim>.png (+ точките), а после шаблоните се правят наново.

art_templates/clothes_sheet_draft.png е същото, но с черновата отгоре: може да
се поправя само грешното вместо да се рисува всичко.
"""

import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(__file__))
import auto_clothes as ac  # noqa: E402
import make_placeholders as mp  # noqa: E402

ROOT = os.path.join(os.path.dirname(__file__), "..")
BEAR = os.path.join(ROOT, "assets", "bear")
OUTDIR = os.path.join(ROOT, "art_templates")
SIZE, GAP, LABEL = 32, 2, 96


def anims():
    return sorted(mp.HAND_DRAWN | set(mp.EXTRA_DRAWN) | {"eat_hold"})


def layout():
    rows, y = [], GAP
    for name in anims():
        frames = Image.open(os.path.join(BEAR, f"{name}.png")).width // SIZE
        rows.append({"anim": name, "y": y, "frames": frames})
        y += SIZE + GAP
    width = LABEL + max(r["frames"] for r in rows) * (SIZE + GAP)
    return rows, width, y


def cell_x(f):
    return LABEL + f * (SIZE + GAP)


def export():
    os.makedirs(OUTDIR, exist_ok=True)
    rows, width, height = layout()
    bg = (200, 200, 200, 255)
    bare = Image.new("RGBA", (width, height), bg)
    draft = Image.new("RGBA", (width, height), bg)
    font = ImageFont.load_default(size=11)
    for img in (bare, draft):
        d = ImageDraw.Draw(img)
        for r in rows:
            d.text((4, r["y"] + 10), r["anim"], fill=(40, 40, 40), font=font)
    for r in rows:
        strip = Image.open(os.path.join(BEAR, f"{r['anim']}.png")).convert("RGBA")
        paint_path = os.path.join(ac.PAINT, f"{r['anim']}.png")
        paint = Image.open(paint_path).convert("RGBA") if os.path.exists(paint_path) else None
        dots = {n: os.path.join(ac.PAINT, f"{r['anim']}_{n}.png") for n in ac.POINT_COLORS}
        dots = {n: Image.open(p).convert("RGBA") for n, p in dots.items() if os.path.exists(p)}
        for f in range(r["frames"]):
            frame = strip.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE))
            # Празната клетка е бяла, за да личи къде свършва кадърът.
            cell = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 255))
            cell.alpha_composite(frame)
            bare.paste(cell, (cell_x(f), r["y"]))
            over = cell.copy()
            if paint is not None:
                over.alpha_composite(paint.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE)))
            for img in dots.values():
                over.alpha_composite(img.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE)))
            draft.paste(over, (cell_x(f), r["y"]))
    bare.save(os.path.join(OUTDIR, "clothes_sheet.png"))
    draft.save(os.path.join(OUTDIR, "clothes_sheet_draft.png"))
    with open(os.path.join(OUTDIR, "clothes_sheet.json"), "w", encoding="utf-8") as fh:
        json.dump(rows, fh, indent=1)
    print("art_templates/clothes_sheet.png, clothes_sheet_draft.png")


def close(a, b, tol=40):
    return sum((x - y) ** 2 for x, y in zip(a, b)) <= tol * tol


# Цветовете за маркиране и как излизат от Aseprite с друг цветови профил
# (леко изместени): всеки вариант → истинският цвят от картата.
MARKS = {
    (255, 0, 0): [(255, 43, 6)],          # блуза
    (128, 0, 0): [(151, 20, 2)],          # ръкав / ръка отпред
    (0, 0, 255): [(25, 34, 251)],         # панталон
    (255, 255, 0): [(254, 253, 50)],      # обувки
    (0, 255, 0): [],                      # ръкавици
    (0, 128, 0): [(0, 144, 23)],          # ръкавици отпред
    (128, 128, 128): [],                  # лапа отпред без дреха
}
POINT_MARKS = {
    "head": [(255, 0, 255), (255, 56, 252)],
    "face": [(0, 255, 255), (0, 252, 255)],
    "neck": [(255, 255, 255)],
    "cup": [(255, 128, 0), (255, 149, 26)],
}


def mark_of(rgb):
    """Цвят от картата (или None), ако пикселът е маркировка."""
    for real, variants in MARKS.items():
        if any(close(rgb, c, 14) for c in [real] + variants):
            return real
    return None


def point_of(rgb):
    for name, variants in POINT_MARKS.items():
        if any(close(rgb, c, 14) for c in variants):
            return name
    return None


def import_sheet(path):
    sheet = Image.open(path).convert("RGBA")
    with open(os.path.join(OUTDIR, "clothes_sheet.json"), encoding="utf-8") as fh:
        rows = json.load(fh)
    parts = {**{ac.LAYER[k]: v for k, v in ac.PART_COLORS.items()}}
    fronts = {ac.LAYER.get(k, "none"): v for k, v in ac.FRONT_COLORS.items()}
    done = []
    for r in rows:
        anim = r["anim"]
        strip = Image.open(os.path.join(BEAR, f"{anim}.png")).convert("RGBA")
        paint = Image.new("RGBA", strip.size, (0, 0, 0, 0))
        dots = {n: Image.new("RGBA", strip.size, (0, 0, 0, 0)) for n in ac.POINT_COLORS}
        old_dots = {}
        for n in ac.POINT_COLORS:
            old = os.path.join(ac.PAINT, f"{anim}_{n}.png")
            if os.path.exists(old):
                old_dots[n] = Image.open(old).convert("RGBA")
        on_point = []      # пиксели на мечока под точка: дрехата им идва от съседите
        marked = False
        for f in range(r["frames"]):
            for y in range(SIZE):
                for x in range(SIZE):
                    p = sheet.getpixel((cell_x(f) + x, r["y"] + y))
                    if p[3] == 0:
                        continue
                    rgb = p[:3]
                    bear = strip.getpixel((f * SIZE + x, y))
                    if not bear[3] and close(rgb, (255, 255, 255), 4):
                        continue   # празно
                    # Бялото е точка само ако мечокът там не е бял (очите са бели).
                    point = point_of(rgb)
                    if point == "neck" and bear[3] and close(bear[:3], (255, 255, 255), 4):
                        point = None
                    if point:
                        dots[point].putpixel((f * SIZE + x, y), ac.POINT_COLORS[point] + (255,))
                        if bear[3]:
                            on_point.append((f * SIZE + x, y))
                        marked = True
                        continue
                    color = mark_of(rgb)
                    if color and bear[3]:
                        paint.putpixel((f * SIZE + x, y), color + (255,))
                        marked = True
        if not marked:
            continue
        for x, y in on_point:
            around = [paint.getpixel((xx, yy)) for xx, yy in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))
                      if 0 <= xx < paint.width and 0 <= yy < SIZE and xx // SIZE == x // SIZE]
            around = [p for p in around if p[3]]
            if around:
                paint.putpixel((x, y), max(set(around), key=around.count))
        # Няколко точки от един вид в кадър (например бели очи, взети за шала):
        # остава тази, която е най-близо до мястото ѝ в другите кадри.
        for n, img in dots.items():
            frames_pts = []
            for f in range(r["frames"]):
                frames_pts.append([(x, y) for y in range(SIZE) for x in range(SIZE)
                                   if img.getpixel((f * SIZE + x, y))[3]])
            single = [p[0] for p in frames_pts if len(p) == 1]
            # Легнал мечок (сън) е без шапка, очила и шал; иначе без ясна точка – нищо.
            if not single or (anim in ac.LYING and n != "cup"):
                img.paste((0, 0, 0, 0), (0, 0, img.width, img.height))
                continue
            mx = sum(x for x, _ in single) / len(single)
            my = sum(y for _, y in single) / len(single)
            for f, pts in enumerate(frames_pts):
                if len(pts) <= 1:
                    continue
                keep = min(pts, key=lambda q: (q[0] - mx) ** 2 + (q[1] - my) ** 2)
                for x, y in pts:
                    if (x, y) != keep:
                        img.putpixel((f * SIZE + x, y), (0, 0, 0, 0))
        # Кадър без нова точка (например две точки на един пиксел) пази старата.
        for n, old in old_dots.items():
            if anim in ac.LYING and n != "cup":
                continue
            for f in range(r["frames"]):
                box = (f * SIZE, 0, f * SIZE + SIZE, SIZE)
                if dots[n].crop(box).getbbox() is None:
                    dots[n].paste(old.crop(box), (f * SIZE, 0))
        os.makedirs(ac.PAINT, exist_ok=True)
        paint.save(os.path.join(ac.PAINT, f"{anim}.png"))
        for n, img in dots.items():
            path = os.path.join(ac.PAINT, f"{anim}_{n}.png")
            if img.getbbox() or os.path.exists(path):
                img.save(path)
        ac.main(anim)
        done.append(anim)
    print("Нови карти:", ", ".join(done) or "няма маркирани редове")


if __name__ == "__main__":
    if sys.argv[1:2] == ["import"]:
        import_sheet(sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "done_art", "clothes_sheet.png"))
    else:
        export()
