"""Иконката на програмата (src/Mecho/mecho.ico): първият кадър от idle анимацията.

ВНИМАНИЕ: сега иконката е нарисувана на ръка (done_art/bear_icon.ico) –
пускането на този скрипт ще я замени със старата.

    python tools/make_icon.py

Изрязва празното около мечока, слага го в квадрат и го уголемява без
размазване (пиксел за пиксел) до 48, 64, 128 и 256 px; малките (16, 24, 32 –
в лентата до часовника) са само главата и раменете, за да се вижда.
"""

import os

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..")
SRC = os.path.join(ROOT, "assets", "bear", "idle.png")
OUT = os.path.join(ROOT, "src", "Mecho", "mecho.ico")
SIZES = [16, 24, 32, 48, 64, 128, 256]


def main():
    strip = Image.open(SRC).convert("RGBA")
    frame = strip.crop((0, 0, strip.height, strip.height))
    frame = frame.crop(frame.getbbox())
    side = max(frame.size)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.alpha_composite(frame, ((side - frame.width) // 2, side - frame.height))
    # Малките (16–32, в лентата до часовника): само главата и раменете, за да не е мъничък.
    w = frame.width
    bust = Image.new("RGBA", (w, w), (0, 0, 0, 0))
    bust.alpha_composite(frame.crop((0, 0, w, min(w, frame.height))), (0, 0))
    images = []
    for size in SIZES:
        if size <= 32:
            canvas = bust.resize((size, size), Image.NEAREST if size % w == 0 else Image.BOX)
            images.append(canvas)
            continue
        k = max(1, size // side)
        big = square.resize((side * k, side * k), Image.NEAREST)
        canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
        if big.width > size:
            big = square.resize((size, size), Image.NEAREST)
        canvas.alpha_composite(big, ((size - big.width) // 2, (size - big.height) // 2))
        images.append(canvas)
    images[-1].save(OUT, format="ICO", sizes=[(s, s) for s in SIZES], append_images=images[:-1])
    print("src/Mecho/mecho.ico")


if __name__ == "__main__":
    main()
