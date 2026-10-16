"""Иконката на програмата (src/Mecho/mecho.ico): първият кадър от idle анимацията.

    python tools/make_icon.py

Изрязва празното около мечока, слага го в квадрат и го уголемява без
размазване (пиксел за пиксел) до 16, 24, 32, 48, 64, 128 и 256 px.
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
    images = []
    for size in SIZES:
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
