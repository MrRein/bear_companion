"""Картинките на дрехите в гардероба и магазина (без мечока): сиви шаблони.

    python tools/make_garment_icons.py

Записва assets/clothes/icons/<вид>.png – тениска, блуза, пуловер, ръкавички,
обувки, панталон. Тоновете са като при шаблоните върху мечока: тъмно (под 80)
контур, средно (под 135) сянка, основно (160) цветът, светло (над 200) отблясък.
Програмата ги оцветява с цвета и шарката на всяка дреха. Тут може да ги
прерисува на ръка (същите тонове) – програмата взима файла, какъвто е.
"""

import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "clothes", "icons")

# k = контур, s = сянка, m = основен цвят, h = отблясък, . = празно
TONES = {"k": 60, "s": 110, "m": 160, "h": 230}

ICONS = {
    # Тениска: къс ръкав.
    "tee": """
.....kkk..kkk.....
...kkmmkkkkmmkk...
..kmmmmmkkmmmmmk..
.kmmmmmmmmmmmmmmk.
kmmmmmmmmmmmmmmmmk
kmhmmmmmmmmmmmmsmk
kkkkmmmmmmmmmmkkkk
...kmhmmmmmmmsk...
...kmmmmmmmmmsk...
...kmmmmmmmmmsk...
...kmmmmmmmmmsk...
...kmmmmmmmmmsk...
...kmmmmmmmmmsk...
...kssssssssssk...
...kkkkkkkkkkkk...
""",
    # Блуза: дълъг тънък ръкав.
    "blouse": """
......kkk..kkk......
....kkmmkkkkmmkk....
...kmmmmmkkmmmmmk...
..kmmmmmmmmmmmmmmk..
.kmmmmmmmmmmmmmmmmk.
.kmhkmmmmmmmmmmkmsk.
.kmmkmhmmmmmmmskmsk.
.kmmkmmmmmmmmmskmsk.
.kmmkmmmmmmmmmskmsk.
.kmmkmmmmmmmmmskmsk.
.kmmkmmmmmmmmmskmsk.
.kmmkmmmmmmmmmskmsk.
.kkkkmmmmmmmmmskkkk.
....kmmmmmmmmmsk....
....kssssssssssk....
....kkkkkkkkkkkk....
""",
    # Пуловер: пухкав – неравен контур, плетка, дебели маншети и яка.
    "sweater": """
.......kkkkkkkk.......
.....kkhhhhhhhhkk.....
...kkmmkssssssskmmkk..
..kmmmmmmmmmmmmmmmmmk.
.kmmhmmmmmmmmmmmmmmmmk
kmmhmmhmmmmmmmmmmhmmmk
kmhkmmmmhmmmmmhmmmkmsk
kmmkmmhmmmmhmmmmmskmmk
.kmkmmmmmhmmmmhmmskmk.
kmmkmmhmmmmmmmmmmskmmk
kmmkmmmmmmhmmmmhmskmmk
.kmkmhmmmmmmmhmmmskmk.
kssskmmmmmmmmmmmmskssk
khhhkmmmhmmmmmmmmskhhk
kkkkkssssssssssssskkkk
....khhhhhhhhhhhhhk...
....kkkkkkkkkkkkkkk...
""",
    # Ръкавички: чифт палчики.
    "gloves": """
..kkkk.......kkkk..
.kmmmmk.....kmmmmk.
kmhmmmmk...kmmmmhmk
kmhmmmmkk.kkmmmmhmk
kmmmmmmkmkmkmmmmmmk
kmmmmmmkmkmkmmmmmmk
kmmmmmmkkk.kmmmmmmk
.kmmmmmk...kmmmmmk.
.kssssk.....ksssskk
.khhhhk.....khhhhk.
.kkkkkk.....kkkkkk.
""",
    # Обувки: чифт, отстрани (гуменки с подметка).
    "shoes": """
...kkkk.........kkkk.....
..kmmmmk.......kmmmmk....
..kmhmmk.......kmhmmk....
..kmmmmkkkk....kmmmmkkkk.
.kmmmmmmmmmk..kmmmmmmmmmk
kmhmmmmmsmmmkkmhmmmmmsmmmk
kssssssssssskkssssssssssk
khhhhhhhhhhhkkhhhhhhhhhhhk
kkkkkkkkkkkkk.kkkkkkkkkkkk
""",
    # Панталон.
    "pants": """
.kkkkkkkkkkkkk.
.kssssssssssssk
.kmhmmmmmmmmmsk
.kmhmmmmmmmmmsk
.kmhmmmkmmmmmsk
.kmhmmkkkmmmmsk
.kmhmmk.kmmmmsk
.kmhmmk.kmmmmsk
.kmhmmk.kmmmmsk
.kmhmmk.kmmmmsk
.kmhmmk.kmmmmsk
.kssssk.kssssk.
.kkkkkk.kkkkkk.
""",
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, art in ICONS.items():
        rows = [r for r in art.strip("\n").split("\n")]
        w = max(len(r) for r in rows)
        img = Image.new("RGBA", (w, len(rows)), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if ch in TONES:
                    g = TONES[ch]
                    img.putpixel((x, y), (g, g, g, 255))
        img.save(os.path.join(OUT, f"{name}.png"))
    print(f"{len(ICONS)} картинки в assets/clothes/icons/")


if __name__ == "__main__":
    main()
