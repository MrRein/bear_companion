"""Лист за предметите в doing*: къде мечокът държи четката и лейката.

    python tools/doing_sheet.py              прави art_templates/doing_sheet.png
    python tools/doing_sheet.py import       чете done_art/doing_sheet.png

За всяка анимация doing* има ред „точка“: мечокът с една синя точка #0080FF
на кадър – там е средата на предмета (дръжката в лапата). Тут мести точката
(изтрива старата, слага нова на същия ред/кадър). Под него – как изглежда
с предмета, точно както го рисува играта (каквото излиза извън кадъра, се реже).

Най-долу са самите предмети (27×27): дръжката трябва да е в средния пиксел,
отбелязан на копието вдясно. Предмет може да се премести или прерисува в
лявата клетка – при import се записва в assets/clothes/tool_<име>.png.
"""

import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(__file__))
import auto_clothes as ac  # noqa: E402

ROOT = os.path.join(os.path.dirname(__file__), "..")
BEAR = os.path.join(ROOT, "assets", "bear")
CLOTHES = os.path.join(ROOT, "assets", "clothes")
OUTDIR = os.path.join(ROOT, "art_templates")
SIZE, HAT = 32, 12            # кадърът и мястото над него (Clothes.HatRoom)
CELL, PAD = 48, 8             # клетката и отстъпът на кадъра в нея
LABEL = 140
DOT = (0, 128, 255)
BG = (200, 200, 200, 255)
OUTSIDE = (225, 225, 225, 255)   # над кадъра (там стига шапката/предметът)
TOOLS = ["paintbrush_red", "paintbrush_blue", "wateringcan"]
# Коя анимация с какво се рисува в играта.
ANIMS = {
    "doing_sit": ["paintbrush_red", "paintbrush_blue"],
    "doing_sit_tired": ["paintbrush_red", "paintbrush_blue"],
    "doing": ["wateringcan"],
    "doing_tired": ["wateringcan"],
}
NAMES = {"paintbrush_red": "червена четка", "paintbrush_blue": "синя четка", "wateringcan": "лейка"}


def frames(img):
    return [img.crop((f * SIZE, 0, f * SIZE + SIZE, SIZE)) for f in range(img.width // SIZE)]


def first_dot(img):
    for y in range(img.height):
        for x in range(img.width):
            if img.getpixel((x, y))[3]:
                return x, y
    return None


def frame_cell(frame):
    """Клетка: бяло = кадърът, светлосиво = мястото над него."""
    cell = Image.new("RGBA", (CELL, CELL + HAT), BG)
    cell.paste(Image.new("RGBA", (SIZE, HAT), OUTSIDE), (PAD, PAD))
    cell.paste(Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 255)), (PAD, PAD + HAT))
    cell.alpha_composite(frame, (PAD, PAD + HAT))
    return cell


def dressed(frame, tool, dot):
    """Както Dresser: средата на предмета на точката, изрязано до кадъра + HatRoom."""
    canvas = Image.new("RGBA", (SIZE, SIZE + HAT), (0, 0, 0, 0))
    canvas.alpha_composite(frame, (0, HAT))
    if dot:
        x, y = dot
        layer = Image.new("RGBA", (SIZE + 2 * tool.width, SIZE + HAT + 2 * tool.height), (0, 0, 0, 0))
        layer.alpha_composite(tool, (tool.width + x - tool.width // 2, tool.height + HAT + y - tool.height // 2))
        canvas.alpha_composite(layer.crop((tool.width, tool.height, tool.width + SIZE, tool.height + SIZE + HAT)))
    cell = Image.new("RGBA", (CELL, CELL + HAT), BG)
    back = Image.new("RGBA", (SIZE, SIZE + HAT), OUTSIDE)
    back.paste(Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 255)), (0, HAT))
    back.alpha_composite(canvas)
    cell.paste(back, (PAD, PAD))
    return cell


def export():
    os.makedirs(OUTDIR, exist_ok=True)
    rows, y = [], 4
    for anim, tools in ANIMS.items():
        rows.append({"kind": "dots", "anim": anim, "y": y})
        y += CELL + HAT
        for t in tools:
            rows.append({"kind": "preview", "anim": anim, "tool": t, "y": y})
            y += CELL + HAT
        y += 10
    tool_y = y + 16
    height = tool_y + 27 + 2 * PAD + 20
    width = LABEL + 4 * CELL + 40
    sheet = Image.new("RGBA", (width, height), BG)
    d = ImageDraw.Draw(sheet)
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 10)   # с кирилица
    except OSError:
        font = ImageFont.load_default(size=11)
    art = {t: Image.open(os.path.join(CLOTHES, f"tool_{t}.png")).convert("RGBA") for t in TOOLS}
    for r in rows:
        strip = Image.open(os.path.join(BEAR, f"{r['anim']}.png")).convert("RGBA")
        dots = Image.open(os.path.join(ac.PAINT, f"{r['anim']}_tool.png")).convert("RGBA")
        r["frames"] = strip.width // SIZE
        label = r["anim"] + ("\n  точка (мести я)" if r["kind"] == "dots" else f"\n  {NAMES[r['tool']]}")
        d.text((4, r["y"] + 24), label, fill=(40, 40, 40) if r["kind"] == "dots" else (110, 110, 110), font=font)
        for f, (frame, dot) in enumerate(zip(frames(strip), frames(dots))):
            x = LABEL + f * CELL
            if r["kind"] == "dots":
                cell = frame_cell(frame)
                p = first_dot(dot)
                if p:
                    cell.putpixel((PAD + p[0], PAD + HAT + p[1]), DOT + (255,))
            else:
                cell = dressed(frame, art[r["tool"]], first_dot(dot))
            sheet.paste(cell, (x, r["y"]))
    d.text((4, tool_y - 14), "предмети: дръжката в средата (вдясно – с отметка)", fill=(40, 40, 40), font=font)
    for i, t in enumerate(TOOLS):
        x = LABEL + i * 2 * (27 + 12)
        sheet.paste(Image.new("RGBA", (27, 27), (255, 255, 255, 255)), (x, tool_y))
        sheet.alpha_composite(art[t], (x, tool_y))
        guide = Image.new("RGBA", (27, 27), (255, 255, 255, 255))
        guide.alpha_composite(art[t])
        for k in range(27):
            for px, py in ((k, 13), (13, k)):
                if not art[t].getpixel((px, py))[3]:
                    guide.putpixel((px, py), (255, 200, 200, 255))
        guide.putpixel((13, 13), (255, 0, 0, 255))
        sheet.paste(guide, (x + 27 + 4, tool_y))
        d.text((x, tool_y + 30), NAMES[t], fill=(40, 40, 40), font=font)
    sheet.save(os.path.join(OUTDIR, "doing_sheet.png"))
    with open(os.path.join(OUTDIR, "doing_sheet.json"), "w", encoding="utf-8") as fh:
        json.dump({"rows": rows, "tool_y": tool_y}, fh, indent=1)
    print("art_templates/doing_sheet.png")


def import_sheet(path):
    sheet = Image.open(path).convert("RGBA")
    with open(os.path.join(OUTDIR, "doing_sheet.json"), encoding="utf-8") as fh:
        meta = json.load(fh)
    for r in meta["rows"]:
        if r["kind"] != "dots":
            continue
        out = Image.new("RGBA", (r["frames"] * SIZE, SIZE), (0, 0, 0, 0))
        for f in range(r["frames"]):
            x0, y0 = LABEL + f * CELL + PAD, r["y"] + PAD + HAT
            found = [(x, y) for y in range(SIZE) for x in range(SIZE)
                     if sum((a - b) ** 2 for a, b in zip(sheet.getpixel((x0 + x, y0 + y))[:3], DOT)) <= 40 * 40]
            if found:
                x, y = found[0]
                out.putpixel((f * SIZE + x, y), DOT + (255,))
            else:
                print(f"{r['anim']} кадър {f}: няма точка")
        out.save(os.path.join(ac.PAINT, f"{r['anim']}_tool.png"))
        ac.main(r["anim"])
    for i, t in enumerate(TOOLS):
        x = LABEL + i * 2 * (27 + 12)
        cell = sheet.crop((x, meta["tool_y"], x + 27, meta["tool_y"] + 27))
        art = Image.new("RGBA", (27, 27), (0, 0, 0, 0))
        for py in range(27):
            for px in range(27):
                c = cell.getpixel((px, py))
                if c[:3] != (255, 255, 255):
                    art.putpixel((px, py), c)
        art.save(os.path.join(CLOTHES, f"tool_{t}.png"))
    print("Точките и предметите са прочетени.")


if __name__ == "__main__":
    if sys.argv[1:2] == ["import"]:
        import_sheet(sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, "done_art", "doing_sheet.png"))
    else:
        export()
