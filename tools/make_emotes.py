"""Емоциите над главата на мечока: малки балончета 13 x 13 с пикселен знак.

    python tools/make_emotes.py      прави assets/ui/emote_<име>.png

Балончето е бяло с тъмен контур и опашчица долу вляво (към главата).
Знакът е 7 x 7 в средата. Тут може да ги прерисува (същото име и размер).
"""

import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "ui")
K = (59, 36, 20, 255)
W = (255, 255, 255, 255)

PAL = {
    ".": None, "k": K, "w": W,
    "r": (220, 60, 70, 255), "p": (245, 140, 160, 255),
    "y": (250, 200, 60, 255), "o": (240, 150, 50, 255),
    "b": (80, 140, 220, 255), "c": (150, 200, 240, 255),
    "g": (110, 170, 80, 255), "n": (150, 95, 40, 255), "s": (160, 160, 170, 255),
}

BUBBLE = """
..kkkkkkkkk..
.kwwwwwwwwwk.
kwwwwwwwwwwwk
kwwwwwwwwwwwk
kwwwwwwwwwwwk
kwwwwwwwwwwwk
kwwwwwwwwwwwk
kwwwwwwwwwwwk
kwwwwwwwwwwwk
.kwwwwwwwwwk.
..kwkkkkkkk..
..kk.........
..k..........
"""

# Знаците: 7 x 7, слагат се в (3, 2).
SIGNS = {
    "heart": """
.rr.rr.
rprrrrr
rrrrrrr
rrrrrrr
.rrrrr.
..rrr..
...r...""",
    "zzz": """
....bbb
.....b.
....bbb
bbbb...
..b....
.b.....
bbbb...""",
    "idea": """
..yyy..
.yyyyy.
.yywyy.
.yyyyy.
..yyy..
..sss..
...s...""",
    "exclaim": """
...r...
..rrr..
..rrr..
..rrr..
...r...
.......
...r...""",
    "question": """
..bbb..
.bb.bb.
....bb.
...bb..
...b...
.......
...b...""",
    "note": """
...kkkk
...k..k
...k..k
...k..k
.kkk.kk
kkkkkkk
.kk.kk.""",
    "sweat": """
...c...
...c...
..ccc..
.ccccc.
.cbccc.
.cbbcc.
..ccc..""",
    "hungry": """
....kk.
...kwk.
.nnnkk.
nonnn..
nnnnn..
.nnn...
.......""",
    "cold": """
...c...
.c.c.c.
..ccc..
ccc.ccc
..ccc..
.c.c.c.
...c...""",
    "sparkle": """
...y...
...y...
.yyyyy.
yyywyyy
.yyyyy.
...y...
...y...""",
    "happy": """
.......
.k...k.
kk...kk
.......
k.....k
.k...k.
..kkk..""",
    "sad": """
.......
.k...k.
.......
...c...
..kkk..
.k...k.
k.....k""",
}


def main():
    os.makedirs(OUT, exist_ok=True)
    rows = BUBBLE.strip("\n").split("\n")
    for name, art in SIGNS.items():
        img = Image.new("RGBA", (13, 13), (0, 0, 0, 0))
        for y, row in enumerate(rows):
            for x, ch in enumerate(row):
                if PAL[ch]:
                    img.putpixel((x, y), PAL[ch])
        for y, row in enumerate(art.strip("\n").split("\n")):
            for x, ch in enumerate(row):
                if PAL.get(ch):
                    img.putpixel((3 + x, 2 + y), PAL[ch])
        img.save(os.path.join(OUT, f"emote_{name}.png"))
    print(", ".join(SIGNS))


if __name__ == "__main__":
    main()
