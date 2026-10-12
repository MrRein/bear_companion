"""Книгите, които Мечо чете: от done_art/book.png (рисунката на Тут) прави 6 вида.

    python tools/make_books.py

Записва assets/clothes/book_1.png … book_6.png. Първата е точно рисунката на
Тут; другите са с друга корица и други страници (текст, картинка, скица…).
Програмата сменя книгата на всеки 2–3 дни и я рисува най-отгоре (над шала).
"""

import os

from PIL import Image

ROOT = os.path.join(os.path.dirname(__file__), "..")
SRC = os.path.join(ROOT, "done_art", "book.png")
OUT = os.path.join(ROOT, "assets", "clothes")

COVER = (49, 21, 4)
PAGE = (233, 218, 189)
TEXT = (139, 139, 157)


def recolor(img, table):
    out = img.copy()
    for y in range(img.height):
        for x in range(img.width):
            p = img.getpixel((x, y))
            if p[3] and p[:3] in table:
                out.putpixel((x, y), table[p[:3]] + (255,))
    return out


def page_pixels(img, color):
    return [(x, y) for y in range(img.height) for x in range(img.width) if img.getpixel((x, y))[:3] == color and img.getpixel((x, y))[3]]


def main():
    book = Image.open(SRC).convert("RGBA")
    books = [book]

    # 2: червена корица.
    books.append(recolor(book, {COVER: (120, 32, 30)}))

    # 3: синя корица, тефтер: текст отляво, отдясно празно и червена лентичка.
    b = recolor(book, {COVER: (34, 52, 110)})
    for x, y in page_pixels(b, TEXT):
        if x > b.width // 2:
            b.putpixel((x, y), PAGE + (255,))
    for y in range(0, 6):
        b.putpixel((9, y), (210, 70, 70, 255))
    books.append(b)

    # 4: зелена корица, книжка с картинки (вместо текст – слънце и трева).
    b = recolor(book, {COVER: (36, 84, 44)})
    for x, y in page_pixels(b, TEXT):
        b.putpixel((x, y), PAGE + (255,))
    for x, y in [(2, 2), (3, 2), (2, 3), (3, 3)]:
        b.putpixel((x, y), (250, 200, 60, 255))           # слънце
    for x in range(7, 10):
        b.putpixel((x, 4), (110, 170, 80, 255))           # трева
    b.putpixel((8, 3), (220, 90, 80, 255))                # цвете
    books.append(b)

    # 5: лилава корица, розови страници (приказки).
    books.append(recolor(book, {COVER: (84, 46, 112), PAGE: (246, 214, 214), TEXT: (170, 120, 150)}))

    # 6: скицник с охра корица: вместо текст – драсканици с молив.
    b = recolor(book, {COVER: (140, 86, 24)})
    for x, y in page_pixels(b, TEXT):
        b.putpixel((x, y), PAGE + (255,))
    for x, y in [(2, 2), (3, 3), (4, 2), (7, 3), (8, 2), (9, 3), (8, 4)]:
        b.putpixel((x, y), (90, 90, 100, 255))
    books.append(b)

    for i, img in enumerate(books, 1):
        img.save(os.path.join(OUT, f"book_{i}.png"))
    print(f"{len(books)} книги в assets/clothes/")


if __name__ == "__main__":
    main()
