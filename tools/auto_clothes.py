"""Прави шаблоните за дрехите от нарисувания мечок (рисунките на Тут).

    python tools/auto_clothes.py --all          всички нарисувани анимации
    python tools/auto_clothes.py idle walk      само тези
    python tools/auto_clothes.py idle --redo    нова чернова на картата (изтрива поправките!)

Шаблоните се правят от картата assets/clothes/paint/<anim>.png (виж по-долу);
ако я няма, първо се прави чернова от автоматичния разрез.

Чете assets/bear/<anim>.png и записва в assets/clothes/layers/ <anim>_shirt.png,
_pants.png, _shoes.png, _gloves.png и _head.png (сиви шаблони и точката за шапката).

Мечокът се разделя на части по контурите, кадър по кадър:
* **глава** – парчето козина с муцуната (ушите са към нея);
* **тяло** – от средата на реда навън до контура, под брадичката, докато
  средата е козина (или нещо в лапите, например книжка);
* **ръце** – другите парчета козина над кръста; краят на ръката (3 реда:
  долу, ако виси, горе, ако е вдигната) е ръкавица, останалото е ръкав;
* **крака** – парчетата козина под кръста са обувки;
* долните 3 реда на тялото са панталон;
* **контурите** взимат частта до тях (дрехата е с предимство пред главата).
Само козината се облича: книжка, зар, Z-та и т.н. остават отгоре.
"""

import os
import sys
from collections import deque

from PIL import Image

BEAR = os.path.join(os.path.dirname(__file__), "..", "assets", "bear")
LAYERS = os.path.join(os.path.dirname(__file__), "..", "assets", "clothes", "layers")
SIZE = 32

# Цветовете (новата палитра на Тут и старата).
OUTLINE = {(68, 20, 8), (88, 29, 8)}
FUR = {(161, 87, 48), (194, 135, 83), (180, 108, 65), (208, 154, 104)}
SHADOW = {(113, 51, 36), (154, 82, 45), (58, 35, 21), (135, 69, 49), (174, 103, 61), (76, 47, 28)}
FACE = {(249, 227, 180), (239, 217, 169), (182, 101, 82), (199, 122, 102), (0, 0, 0), (255, 255, 255),
        (251, 232, 195), (243, 224, 185)}
MUZZLE = {(249, 227, 180), (239, 217, 169), (251, 232, 195), (243, 224, 185)}

# Анимации, в които лежи: само тялото е с дреха, шапката се сваля.
LYING = {"sleep", "sleep_peek"}

NONE, HEAD, SHIRT, PANTS, SHOES, GLOVES = range(6)
LAYER = {SHIRT: "shirt", PANTS: "pants", SHOES: "shoes", GLOVES: "gloves"}
PRIORITY = [GLOVES, SHOES, PANTS, SHIRT]


def kind(p):
    if p[3] == 0:
        return "."
    rgb = p[:3]
    if rgb in OUTLINE:
        return "k"
    if rgb in FUR or rgb in SHADOW:
        return "f"
    if rgb in FACE:
        return "c" if rgb in MUZZLE else "e"
    return "i"   # нещо в лапите или наоколо (книжка, зар, Z)


def tone(p):
    rgb = p[:3]
    if rgb in OUTLINE:
        return 60
    if rgb in SHADOW:
        return 110
    return 160


def components(cells):
    """Свързани парчета (по 4 съседа) от множество клетки."""
    seen, out = set(), []
    for start in cells:
        if start in seen:
            continue
        group, queue = [], deque([start])
        seen.add(start)
        while queue:
            x, y = queue.popleft()
            group.append((x, y))
            for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if n in cells and n not in seen:
                    seen.add(n)
                    queue.append(n)
        out.append(group)
    return out


def segment(frame, lying):
    """Връща (части по пиксел, точката за шапката или None)."""
    k = {(x, y): kind(frame.getpixel((x, y))) for x in range(SIZE) for y in range(SIZE)}
    body = {c for c, v in k.items() if v in "fce"}
    parts = {}
    front = set()

    groups = components(body)
    head = max(groups, key=lambda g: sum(1 for c in g if k[c] == "c"), default=[])
    if not any(k[c] == "c" for c in head):
        head = []
    eyes = -1
    if not lying and head:
        # Главата: ред по ред от средата на муцуната навън до контура (така
        # вдигната лапа, която докосва бузата, не става част от главата).
        muzzle = [c for c in head if k[c] == "c"]
        hx = round(sum(x for x, _ in muzzle) / len(muzzle))
        chin = max(y for _, y in muzzle)
        if k[(hx, chin + 1)] == "f":
            chin += 1
        head = []
        for y in range(chin + 1):
            for step in (-1, 1):
                x, seen_other = hx, False
                while 0 <= x < SIZE:
                    v = k[(x, y)]
                    if v in ".i":
                        break
                    if v == "k":
                        if seen_other:
                            break
                    else:
                        seen_other = True
                        head.append((x, y))
                    x += step
        eyes = min((y for (x, y) in head if frame.getpixel((x, y))[:3] == (0, 0, 0)), default=chin)
    head_set = set(head)
    for c in head:
        parts[c] = HEAD
    chin = max((y for _, y in head), default=-1)

    if lying:
        for c in body - head_set:
            if k[c] == "f":
                parts[c] = SHIRT
        anchor = {}
    else:
        # Тялото: ред по ред от средата навън, докато средата е козина.
        cx, bottom, started = 16, -1, False
        torso, via_item = set(), set()
        for y in range(chin + 1, SIZE):
            mid = k[(cx, y)]
            if mid in "fi":
                started = True
            elif started:
                break
            elif mid == ".":
                continue
            row = set()
            for step in (-1, 1):
                x, seen_other, crossed = cx, False, False
                while 0 <= x < SIZE:
                    v = k[(x, y)]
                    if v == ".":
                        break
                    if v == "k":
                        if seen_other:
                            break
                    else:
                        seen_other = True
                        if v == "i":
                            crossed = True
                        if v == "f":
                            row.add((x, y))
                            if crossed:
                                via_item.add((x, y))
                    x += step
            if row:
                torso |= row
                bottom = y
        # Лапа, която държи нещо (книжка): стига се до нея през предмета и
        # докосва ръката, значи е ръка, а не тяло.
        rest = {c for c in body - head_set - torso if k[c] == "f"}
        moved = True
        while moved:
            moved = False
            for (x, y) in list(via_item & torso):
                if any(n in rest for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))):
                    torso.discard((x, y))
                    rest.add((x, y))
                    moved = True
        bottom = max((y for _, y in torso), default=bottom)
        waist = bottom - 2
        for c in torso:
            parts[c] = PANTS if c[1] >= waist else SHIRT

        # Ръце и крака: останалите парчета козина. Ръцете са „отпред“: шалът
        # (и другото на точка) е под тях, когато лапата мине пред тялото.
        for g in components(rest):
            top = min(y for _, y in g)
            low = max(y for _, y in g)
            if low < eyes:
                for c in g:
                    parts[c] = HEAD     # ухо
                continue
            if top >= waist:
                for c in g:
                    parts[c] = SHOES
                continue
            if len(g) < 8 or low - top < 3:
                for c in g:
                    parts[c] = SHIRT    # рамо, част от блузата
                continue
            # Лапата е най-далечният край от рамото (накъдето и да сочи ръката).
            def far(c, base):
                return max(abs(c[0] - base[0]), abs(c[1] - base[1]))
            anchors = torso or {(cx, chin + 3)}
            shoulder = min(g, key=lambda c: min(far(c, t) for t in anchors))
            # Посоката на ръката: от рамото към най-далечната точка. Лапата е
            # последните 3 реда по тази посока (вися ли – долу, вдигната – горе).
            tip = max(g, key=lambda c: (c[0] - shoulder[0]) ** 2 + (c[1] - shoulder[1]) ** 2)
            dx, dy = tip[0] - shoulder[0], tip[1] - shoulder[1]
            length = max(1e-6, (dx * dx + dy * dy) ** 0.5)
            along = {c: ((c[0] - shoulder[0]) * dx + (c[1] - shoulder[1]) * dy) / length for c in g}
            end = max(along.values())
            for c in g:
                parts[c] = GLOVES if along[c] >= end - 2.5 else SHIRT
                front.add(c)

        # Точките: шапката над средата на муцуната, 3 реда под темето; очилата
        # на реда на очите; шалът под брадичката.
        muzzle = [x for (x, y) in head if k[(x, y)] == "c"]
        anchor = {}
        if muzzle:
            hx = round(sum(muzzle) / len(muzzle))
            tops = [y for y in range(SIZE) if k[(hx, y)] in "kf"]
            if tops:
                anchor["head"] = (hx, tops[0] + 3)
            if eyes >= 0:
                anchor["face"] = (hx, eyes + 1)
            anchor["neck"] = (hx, min(SIZE - 1, chin + 1))

    # Контурите: частта до тях (по 4 съседа, после по 8); дрехата пред главата.
    for (x, y), v in k.items():
        if v != "k":
            continue
        for neighbours in (((1, 0), (-1, 0), (0, 1), (0, -1)),
                           ((1, 1), (1, -1), (-1, 1), (-1, -1))):
            near = [parts.get((x + dx, y + dy), NONE) for dx, dy in neighbours]
            clothes = [p for p in near if p in LAYER]
            if clothes:
                best = max(PRIORITY, key=lambda p: (clothes.count(p), -PRIORITY.index(p)))
                parts[(x, y)] = best
                break
            if HEAD in near:
                break
    # Контурите на ръцете също са отпред.
    for (x, y), v in k.items():
        if v == "k" and parts.get((x, y)) in (SHIRT, GLOVES) and any(n in front for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1))):
            front.add((x, y))
    return parts, anchor, front


# Чашата (при пиене): долната ѝ среда е тук в първия кадър на sit_drinking;
# в другите кадри се мести с дясната лапа (търси се изместването на лапата).
CUP_BASE = (17, 26)
PAW_BOX = (16, 18, 26, 28)   # къде е лапата с чашата (x0, y0, x1, y1)


# Храната при ядене (eat, eat_hold): средата ѝ е в лапите пред корема и се
# мести с лапите кадър по кадър.
EAT_BASE = (16, 21)
EAT_BOX = (7, 16, 25, 25)


def paw_shift(base, frame, box):
    """С колко пиксела са се преместили лапите в кутията спрямо основния кадър."""
    x0, y0, x1, y1 = box
    best, shift = None, (0, 0)
    for dy in range(-4, 3):
        for dx in range(-3, 4):
            bad = 0
            for y in range(y0, y1):
                for x in range(x0, x1):
                    a = base.getpixel((x, y))
                    xx, yy = x + dx, y + dy
                    b = frame.getpixel((xx, yy)) if 0 <= xx < SIZE and 0 <= yy < SIZE else (0, 0, 0, 0)
                    if (a[3] > 0) != (b[3] > 0) or (a[3] and a[:3] != b[:3]):
                        bad += 1
            if best is None or bad < best or (bad == best and abs(dx) + abs(dy) < abs(shift[0]) + abs(shift[1])):
                best, shift = bad, (dx, dy)
    return shift


def eat_anchors(strip):
    base = Image.open(os.path.join(BEAR, "eat.png")).convert("RGBA").crop((0, 0, SIZE, SIZE))
    out = []
    for f in range(strip.width // SIZE):
        frame = strip.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE))
        dx, dy = paw_shift(base, frame, EAT_BOX)
        out.append((EAT_BASE[0] + dx, EAT_BASE[1] + dy))
    return out


def cup_anchors(strip, name):
    base = Image.open(os.path.join(BEAR, "sit_drinking.png")).convert("RGBA").crop((0, 0, SIZE, SIZE))
    x0, y0, x1, y1 = PAW_BOX
    out = []
    for f in range(strip.width // SIZE):
        frame = strip.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE))
        best, shift = None, (0, 0)
        for dy in range(-4, 3):
            for dx in range(-3, 4):
                bad = 0
                for y in range(y0, y1):
                    for x in range(x0, x1):
                        a = base.getpixel((x, y))
                        xx, yy = x + dx, y + dy
                        b = frame.getpixel((xx, yy)) if 0 <= xx < SIZE and 0 <= yy < SIZE else (0, 0, 0, 0)
                        if (a[3] > 0) != (b[3] > 0) or (a[3] and a[:3] != b[:3]):
                            bad += 1
                if best is None or bad < best or (bad == best and abs(dx) + abs(dy) < abs(shift[0]) + abs(shift[1])):
                    best, shift = bad, (dx, dy)
        # Лапата е вдигната (кадърът се различава от основния около лапата):
        # чашата отива към устата – при отпиване до устата, при духане наполовина.
        changed = 0
        for y in range(y0, y1):
            for x in range(x0, x1):
                a, b = base.getpixel((x, y)), frame.getpixel((x, y))
                if (a[3] > 0) != (b[3] > 0) or (a[3] and a[:3] != b[:3]):
                    changed += 1
        full = 4 if "sip" in name else 2
        lift = 0 if changed < 20 else (full if changed >= 45 or "blow" in name else full // 2 + 1)
        lean = -1 if lift >= 3 else 0
        out.append((CUP_BASE[0] + lean, CUP_BASE[1] - lift))
    return out


# ───────────────────────── Картата за дрехите ─────────────────────────
#
# assets/clothes/paint/<anim>.png е „карта“ върху мечока, със същия размер като
# лентата. Всяка дреха е цвят; Тут я поправя в Aseprite (слой над мечока) и
# картата е винаги с предимство пред автоматичния разрез. Ако я няма, скриптът
# я прави сам от разреза (чернова) – после може да се поправи на ръка.

PAINT = os.path.join(os.path.dirname(__file__), "..", "assets", "clothes", "paint")

PART_COLORS = {SHIRT: (255, 0, 0), PANTS: (0, 0, 255), SHOES: (255, 255, 0), GLOVES: (0, 255, 0)}
# Тъмните са същото, но „отпред“ (ръка пред тялото: шалът е под нея).
FRONT_COLORS = {SHIRT: (128, 0, 0), GLOVES: (0, 128, 0), NONE: (128, 128, 128)}
# Точките са в отделни ленти до картата (<anim>_head.png и т.н.), по един пиксел на кадър.
POINT_COLORS = {"head": (255, 0, 255), "face": (0, 255, 255), "neck": (255, 255, 255), "cup": (255, 128, 0)}


def draft(anim, strip):
    """Чернова на картата и на точките от автоматичния разрез."""
    paint = Image.new("RGBA", strip.size, (0, 0, 0, 0))
    dots = {name: Image.new("RGBA", strip.size, (0, 0, 0, 0)) for name in POINT_COLORS}
    cups = cup_anchors(strip, anim) if anim.startswith("sit_drinking") else eat_anchors(strip) if anim in ("eat", "eat_hold") else None
    for f in range(strip.width // SIZE):
        frame = strip.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE))
        parts, anchor, front = segment(frame, anim in LYING)
        for (x, y), part in parts.items():
            if (x, y) in front:
                paint.putpixel((f * SIZE + x, y), FRONT_COLORS.get(part, FRONT_COLORS[NONE]) + (255,))
            elif part in PART_COLORS:
                paint.putpixel((f * SIZE + x, y), PART_COLORS[part] + (255,))
        if cups:
            anchor["cup"] = (cups[f][0], min(SIZE - 1, cups[f][1]))
        for name, (ax, ay) in anchor.items():
            # Всяка точка е в своя лента (могат да се застъпват: чаша пред шала).
            dots[name].putpixel((f * SIZE + ax, ay), POINT_COLORS[name] + (255,))
    if not cups:
        del dots["cup"]
    return paint, dots


def nearest(rgb, table):
    """Кой цвят от таблицата е най-близо (картата може да е рисувана с леко различни цветове)."""
    best, dist = None, 60 ** 2
    for key, c in table.items():
        d = sum((a - b) ** 2 for a, b in zip(rgb, c))
        if d < dist:
            best, dist = key, d
    return best


def layers_from(anim, strip, paint):
    """От картата: сивите шаблони, точките и слоят „отпред“ в assets/clothes/layers."""
    names = list(LAYER.values()) + ["head", "face", "neck", "front"]
    if anim.startswith("sit_drinking") or anim in ("eat", "eat_hold"):
        names.append("cup")
    layers = {n: Image.new("RGBA", strip.size, (0, 0, 0, 0)) for n in names}
    w = min(strip.width, paint.width)
    for y in range(SIZE):
        for x in range(w):
            p = paint.getpixel((x, y))
            if p[3] < 128:
                continue
            rgb = p[:3]
            bear = strip.getpixel((x, y))
            if bear[3] == 0:
                continue
            part = nearest(rgb, PART_COLORS)
            is_front = part is None and nearest(rgb, FRONT_COLORS) is not None
            if is_front:
                part = nearest(rgb, FRONT_COLORS)
                layers["front"].putpixel((x, y), (255, 255, 255, 255))
            if part in LAYER:
                g = tone(bear)
                layers[LAYER[part]].putpixel((x, y), (g, g, g, 255))
    # Точките: <anim>_head.png (шапка), _face.png (очила), _neck.png (шал),
    # _cup.png (чаша/храна) от папката с картите – по един пиксел на кадър.
    for name in POINT_COLORS:
        src = os.path.join(PAINT, f"{anim}_{name}.png")
        if os.path.exists(src):
            layers[name] = Image.open(src).convert("RGBA")
        elif name in layers and name == "cup":
            del layers[name]
    os.makedirs(LAYERS, exist_ok=True)
    for name, img in layers.items():
        img.save(os.path.join(LAYERS, f"{anim}_{name}.png"))


def main(anim, redo=False):
    strip = Image.open(os.path.join(BEAR, f"{anim}.png")).convert("RGBA")
    os.makedirs(PAINT, exist_ok=True)
    path = os.path.join(PAINT, f"{anim}.png")
    if redo or not os.path.exists(path):
        paint, dots = draft(anim, strip)
        paint.save(path)
        for name, img in dots.items():
            img.save(os.path.join(PAINT, f"{anim}_{name}.png"))
        print(f"{anim}: нова чернова на картата")
    paint = Image.open(path).convert("RGBA")
    layers_from(anim, strip, paint)


def all_anims():
    """Нарисуваните от Тут (временните си имат точни шаблони от make_placeholders.py)."""
    sys.path.insert(0, os.path.dirname(__file__))
    import make_placeholders as mp
    return sorted(mp.HAND_DRAWN | set(mp.EXTRA_DRAWN) | {"eat_hold"})


if __name__ == "__main__":
    args = sys.argv[1:]
    redo = "--redo" in args
    names = [a for a in args if not a.startswith("--")]
    if "--all" in args or not names:
        names = all_anims()
    for name in names:
        main(name, redo)
