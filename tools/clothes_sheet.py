"""Лист за маркиране на дрехите: всички нарисувани анимации на един файл.

    python tools/clothes_sheet.py              прави листовете в art_templates/
    python tools/clothes_sheet.py import       чете маркирания лист (done_art/clothes_sheet.png)
    python tools/clothes_sheet.py new a b c    лист само с тези анимации (clothes_sheet_new*.png)
    python tools/clothes_sheet.py import done_art/clothes_sheet_new.png
    python tools/clothes_sheet.py hold          hold_sheet: само синята точка – какво държи в лапите
    python tools/clothes_sheet.py import-hold   чете го (done_art/hold_sheet.png)

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
    """Всички анимации на мечока, които ги има в assets/bear."""
    return sorted(os.path.splitext(f)[0] for f in os.listdir(BEAR) if f.endswith(".png"))


def layout(names=None):
    rows, y = [], GAP
    for name in names or anims():
        frames = Image.open(os.path.join(BEAR, f"{name}.png")).width // SIZE
        rows.append({"anim": name, "y": y, "frames": frames})
        y += SIZE + GAP
    width = LABEL + max(r["frames"] for r in rows) * (SIZE + GAP)
    return rows, width, y


def cell_x(f):
    return LABEL + f * (SIZE + GAP)


def export(names=None, out="clothes_sheet"):
    os.makedirs(OUTDIR, exist_ok=True)
    rows, width, height = layout(names)
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
        # Точките на това, което държи (tool, cup), са в hold_sheet – тук не се показват.
        dots = {n: os.path.join(ac.PAINT, f"{r['anim']}_{n}.png") for n in ac.POINT_COLORS if n not in HOLD_POINTS}
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
    bare.save(os.path.join(OUTDIR, f"{out}.png"))
    draft.save(os.path.join(OUTDIR, f"{out}_draft.png"))
    with open(os.path.join(OUTDIR, f"{out}.json"), "w", encoding="utf-8") as fh:
        json.dump(rows, fh, indent=1)
    print(f"art_templates/{out}.png, {out}_draft.png")


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
    "tool": [(0, 128, 255), (0, 141, 255)],
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
    # Точките може да са в отделен файл до листа (<лист>_points.png – слой само с точките,
    # изнесен отделно от Aseprite). Тогава цветът под точката е този от листа, а не се гадае.
    pts_path = os.path.splitext(path)[0] + "_points.png"
    points = Image.open(pts_path).convert("RGBA") if os.path.exists(pts_path) else None
    # clothes_sheet_new.png → clothes_sheet_new.json (листът казва кой ред коя анимация е).
    name = os.path.splitext(os.path.basename(path))[0]
    if not os.path.exists(os.path.join(OUTDIR, f"{name}.json")):
        name = "clothes_sheet"
    with open(os.path.join(OUTDIR, f"{name}.json"), encoding="utf-8") as fh:
        rows = json.load(fh)
    parts = {**{ac.LAYER[k]: v for k, v in ac.PART_COLORS.items()}}
    fronts = {ac.LAYER.get(k, "none"): v for k, v in ac.FRONT_COLORS.items()}
    done = []
    for r in rows:
        anim = r["anim"]
        strip = Image.open(os.path.join(BEAR, f"{anim}.png")).convert("RGBA")
        paint = Image.new("RGBA", strip.size, (0, 0, 0, 0))
        old_paint_path = os.path.join(ac.PAINT, f"{anim}.png")
        old_paint = Image.open(old_paint_path).convert("RGBA") if os.path.exists(old_paint_path) else None
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
                    if points is not None:
                        q = points.getpixel((cell_x(f) + x, r["y"] + y))
                        if q[3] and (point := point_of(q[:3])):
                            dots[point].putpixel((f * SIZE + x, y), ac.POINT_COLORS[point] + (255,))
                            marked = True
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
            if not around:
                continue
            best = max(around.count(c) for c in set(around))
            tied = {c for c in set(around) if around.count(c) == best}
            # Равни съседи (ръкавица/блуза) – не се гадае: остава цветът от старата карта.
            old_px = old_paint.getpixel((x, y)) if old_paint is not None and x < old_paint.width else (0, 0, 0, 0)
            paint.putpixel((x, y), old_px if len(tied) > 1 and old_px in tied else max(tied, key=around.count))
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


# Какво държи в лапите: четка/лейка/карти (tool), храна и чаша (cup), подарък/саксия (tool).
HOLD_POINTS = ("tool", "cup")


def hold_point(anim):
    """В кой слой отива синята точка от hold_sheet за тази анимация."""
    return "cup" if anim.startswith(("eat", "sit_drinking")) else "tool"


def hold_anims():
    """Анимациите, в които мечето държи нещо."""
    return [a for a in anims() if a.startswith(("doing", "knit", "guitar", "hold", "eat", "sit_drinking"))]


def export_hold(out="hold_sheet"):
    """
    hold_sheet: само синята точка (0,128,255) – по една на кадър, там отива средата на
    това, което държи (четка, лейка, карти, храна, чаша, подарък, саксия). Мечокът е без
    карта на дрехите. hold_sheet.png е празен, hold_sheet_now.png е със сегашните точки.
    """
    os.makedirs(OUTDIR, exist_ok=True)
    rows, width, height = layout(hold_anims())
    bg = (200, 200, 200, 255)
    bare = Image.new("RGBA", (width, height), bg)
    now = Image.new("RGBA", (width, height), bg)
    font = ImageFont.load_default(size=11)
    blue = ac.POINT_COLORS["tool"] + (255,)
    for img in (bare, now):
        d = ImageDraw.Draw(img)
        for r in rows:
            d.text((4, r["y"] + 10), r["anim"], fill=(40, 40, 40), font=font)
    for r in rows:
        strip = Image.open(os.path.join(BEAR, f"{r['anim']}.png")).convert("RGBA")
        dot_path = os.path.join(ac.PAINT, f"{r['anim']}_{hold_point(r['anim'])}.png")
        dot = Image.open(dot_path).convert("RGBA") if os.path.exists(dot_path) else None
        for f in range(r["frames"]):
            cell = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 255))
            cell.alpha_composite(strip.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE)))
            bare.paste(cell, (cell_x(f), r["y"]))
            if dot is not None:   # и точката на храната/чашата е синя тук
                for y in range(SIZE):
                    for x in range(SIZE):
                        if f * SIZE + x < dot.width and dot.getpixel((f * SIZE + x, y))[3]:
                            cell.putpixel((x, y), blue)
            now.paste(cell, (cell_x(f), r["y"]))
    bare.save(os.path.join(OUTDIR, f"{out}.png"))
    now.save(os.path.join(OUTDIR, f"{out}_now.png"))
    with open(os.path.join(OUTDIR, f"{out}.json"), "w", encoding="utf-8") as fh:
        json.dump(rows, fh, indent=1)
    print(f"art_templates/{out}.png, {out}_now.png")


def import_hold(path):
    """Само синята точка: картата на дрехите не се пипа, цветът под точката остава."""
    sheet = Image.open(path).convert("RGBA")
    with open(os.path.join(OUTDIR, "hold_sheet.json"), encoding="utf-8") as fh:
        rows = json.load(fh)
    done = []
    for r in rows:
        anim = r["anim"]
        layer = hold_point(anim)
        dot_path = os.path.join(ac.PAINT, f"{anim}_{layer}.png")
        old = Image.open(dot_path).convert("RGBA") if os.path.exists(dot_path) else None
        dots = Image.new("RGBA", (r["frames"] * SIZE, SIZE), (0, 0, 0, 0))
        changed = False
        for f in range(r["frames"]):
            # Синя точка (и оранжевата от стария лист за храната – и тя става).
            pts = [(x, y) for y in range(SIZE) for x in range(SIZE)
                   if cell_x(f) + x < sheet.width and r["y"] + y < sheet.height
                   and sheet.getpixel((cell_x(f) + x, r["y"] + y))[3]
                   and point_of(sheet.getpixel((cell_x(f) + x, r["y"] + y))[:3]) in HOLD_POINTS]
            if len(pts) == 1:
                x, y = pts[0]
                dots.putpixel((f * SIZE + x, y), ac.POINT_COLORS[layer] + (255,))
                changed = True
            else:
                if len(pts) > 1:
                    print(f"{anim}, кадър {f + 1}: {len(pts)} точки – остава старата")
                if old is not None:   # кадър без (ясна) точка – пази старата
                    dots.paste(old.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE)), (f * SIZE, 0))
        if not changed or (old is not None and old.tobytes() == dots.tobytes()):
            continue
        dots.save(dot_path)
        ac.main(anim)
        done.append(anim)
    print("Нови точки:", ", ".join(done) or "няма промени")


if __name__ == "__main__":
    if sys.argv[1:2] == ["hold"]:
        export_hold()
    elif sys.argv[1:2] == ["import-hold"]:
        import_hold(sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "done_art", "hold_sheet.png"))
    elif sys.argv[1:2] == ["import"]:
        import_sheet(sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "done_art", "clothes_sheet.png"))
    elif sys.argv[1:2] == ["new"]:
        export(sys.argv[2:], "clothes_sheet_new")
    else:
        export()
