"""Картите в лапите на мечока, докато играе/работи върху настолна игра (doing_sit).

    python tools/make_cards.py

Записва assets/clothes/tool_cards_<цвят>_<брой>.png (27 x 27, като четката: средата на
рисунката е точката _tool в лапите). Ветрило от 2–4 карти с гръб в цвета; най-предната
е с лицето напред (бяла, с точка в цвета). Програмата сменя цвета и броя на 2 часа.
"""

import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "assets", "clothes")
OUTLINE = (59, 36, 20, 255)
WHITE = (255, 250, 240, 255)
COLORS = {
    "red": ((214, 74, 70, 255), (240, 140, 130, 255)),
    "blue": ((80, 130, 210, 255), (150, 190, 240, 255)),
    "green": ((100, 165, 70, 255), (160, 210, 120, 255)),
    "purple": ((150, 100, 200, 255), (200, 165, 235, 255)),
    "yellow": ((235, 180, 50, 255), (250, 220, 120, 255)),
}
W, H = 6, 8   # една карта


def card(img, x, y, back, light, face):
    px = img.load()
    for dy in range(H):
        for dx in range(W):
            edge = dx in (0, W - 1) or dy in (0, H - 1)
            if edge:
                c = OUTLINE
            elif face:
                c = WHITE
            else:
                # Гръб: цвят с по-светъл ромб в средата.
                c = light if abs(dx - 2.5) + abs(dy - 3.5) <= 1.5 else back
            px[x + dx, y + dy] = c
    if face:
        px[x + 2, y + 3] = back
        px[x + 3, y + 3] = back
        px[x + 2, y + 4] = back
        px[x + 3, y + 4] = back


def cards(color, n):
    back, light = COLORS[color]
    img = Image.new("RGBA", (27, 27), (0, 0, 0, 0))
    # Ветрило: всяка следваща карта – 3 вдясно и 1 нагоре; цялото е центрирано в лапите.
    width = W + 3 * (n - 1)
    x0, y0 = 13 - width // 2, 12 - H // 2 + (n - 1) // 2
    for i in range(n):
        card(img, x0 + 3 * i, y0 - i, back, light, face=i == n - 1)
    return img


def main():
    for color in COLORS:
        for n in (2, 3, 4):
            cards(color, n).save(os.path.join(OUT, f"tool_cards_{color}_{n}.png"))


if __name__ == "__main__":
    main()
