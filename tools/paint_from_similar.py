"""Карта за дрехите от най-близкия вече поправен кадър.

    python tools/paint_from_similar.py cook drink sit      само тези
    python tools/paint_from_similar.py cook --refs idle sit_drinking

Когато Тут качи нова анимация, автоматичният разрез понякога бърка (цялото тяло
става „обувка“). Тогава за всеки кадър търсим най-приличащия кадър от картите,
които вече са поправени (в git), с отместване до 2 пиксела, и копираме
маркировката оттам. Новите пиксели (които ги няма в образеца) взимат цвета на
съседите си. После: python tools/auto_clothes.py <анимации>.
"""

import os
import subprocess
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(__file__))
from auto_clothes import FUR, OUTLINE, SHADOW  # noqa: E402  козината (само тя се облича)

BODY = FUR | OUTLINE | SHADOW

ROOT = os.path.join(os.path.dirname(__file__), "..")
BEAR = os.path.join(ROOT, "assets", "bear")
PAINT = os.path.join(ROOT, "assets", "clothes", "paint")
SIZE = 32
SHIFT = 2


def tracked_refs():
    """Поправените карти: тези в git (без точките _head/_face/_neck/_cup)."""
    out = subprocess.run(["git", "ls-files", "assets/clothes/paint"], cwd=ROOT, capture_output=True, text=True).stdout
    names = []
    for line in out.split():
        n = os.path.splitext(os.path.basename(line))[0]
        if any(n.endswith(s) for s in ("_head", "_face", "_neck", "_cup")):
            continue
        if os.path.exists(os.path.join(BEAR, n + ".png")):
            names.append(n)
    return names


def frames(img):
    return [img.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE)) for f in range(img.width // SIZE)]


def score(a, b, dx, dy):
    """Колко си приличат кадър a и кадър b, отместен с (dx, dy)."""
    s = 0
    pa, pb = a.load(), b.load()
    for y in range(SIZE):
        for x in range(SIZE):
            bx, by = x - dx, y - dy
            p = pa[x, y]
            q = pb[bx, by] if 0 <= bx < SIZE and 0 <= by < SIZE else (0, 0, 0, 0)
            if p[3] and q[3]:
                s += 2 if p[:3] == q[:3] else 0.5
            elif p[3] or q[3]:
                s -= 1
    return s


def build(anim, refs):
    strip = Image.open(os.path.join(BEAR, anim + ".png")).convert("RGBA")
    library = []
    for r in refs:
        if r == anim:
            continue
        bear = frames(Image.open(os.path.join(BEAR, r + ".png")).convert("RGBA"))
        paint = frames(Image.open(os.path.join(PAINT, r + ".png")).convert("RGBA"))
        library += [(r, i, b, p) for i, (b, p) in enumerate(zip(bear, paint))]
    out = Image.new("RGBA", strip.size, (0, 0, 0, 0))
    matches = []
    for f, frame in enumerate(frames(strip)):
        best = None
        for name, i, b, p in library:
            for dy in range(-SHIFT, SHIFT + 1):
                for dx in range(-SHIFT, SHIFT + 1):
                    sc = score(frame, b, dx, dy)
                    if best is None or sc > best[0]:
                        best = (sc, name, i, b, p, dx, dy)
        _, name, i, b, p, dx, dy = best
        print(f"{anim} кадър {f}: като {name} {i} ({dx:+d},{dy:+d})")
        matches.append((name, i, dx, dy))
        fp, bp, pp = frame.load(), b.load(), p.load()
        label = {}
        unknown = []
        for y in range(SIZE):
            for x in range(SIZE):
                if not fp[x, y][3]:
                    continue
                bx, by = x - dx, y - dy
                inside = 0 <= bx < SIZE and 0 <= by < SIZE
                if inside and bp[bx, by][3]:
                    if pp[bx, by][3] >= 128:
                        label[(x, y)] = pp[bx, by]
                elif fp[x, y][:3] in BODY:
                    unknown.append((x, y))
        # Новите пиксели: като съседите си (до 2 стъпки).
        for _ in range(2):
            added = {}
            for x, y in unknown:
                if (x, y) in label:
                    continue
                near = [label[(x + ox, y + oy)] for ox, oy in ((1, 0), (-1, 0), (0, 1), (0, -1)) if (x + ox, y + oy) in label]
                if near:
                    added[(x, y)] = max(set(near), key=near.count)
            label.update(added)
        for (x, y), c in label.items():
            out.putpixel((f * SIZE + x, y), c)
    out.save(os.path.join(PAINT, anim + ".png"))
    copy_dots(anim, strip.width // SIZE, matches)


DOTS = ("head", "face", "neck", "cup")


def copy_dots(anim, count, matches):
    """Точките (шапка, очила, шал, чаша) – от същия кадър-образец, със същото отместване."""
    for dot in DOTS:
        out = Image.new("RGBA", (count * SIZE, SIZE), (0, 0, 0, 0))
        found = False
        for f, (name, i, dx, dy) in enumerate(matches):
            path = os.path.join(PAINT, f"{name}_{dot}.png")
            if not os.path.exists(path):
                continue
            src = Image.open(path).convert("RGBA")
            for y in range(SIZE):
                for x in range(SIZE):
                    p = src.getpixel((i * SIZE + x, y)) if i * SIZE + x < src.width else (0, 0, 0, 0)
                    if p[3] and 0 <= x + dx < SIZE and 0 <= y + dy < SIZE:
                        out.putpixel((f * SIZE + x + dx, y + dy), p)
                        found = True
        target = os.path.join(PAINT, f"{anim}_{dot}.png")
        if found:
            out.save(target)
        elif os.path.exists(target):
            os.remove(target)


def main():
    args = sys.argv[1:]
    refs = None
    if "--refs" in args:
        k = args.index("--refs")
        refs = args[k + 1:]
        args = args[:k]
    refs = refs or tracked_refs()
    for anim in args:
        build(anim, refs)


if __name__ == "__main__":
    main()
