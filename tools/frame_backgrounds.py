# New calm frame backgrounds (50x50, 9-slice 5): seamless 40px pattern inside, ink outline, soft bevel.
import random, sys
from PIL import Image
OUT = sys.argv[1]
INK = (13, 18, 28, 255)
N = 130
P = 120  # period of the pattern (the middle tile)

def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c[:3]) + (255,)

def make(name, base, light, dark, deco):
    rnd = random.Random(name)
    im = Image.new('RGBA', (N, N), base + (255,))
    px = im.load()
    def put(x, y, c):
        # pattern coordinates are periodic: write every copy that lands inside the image
        for ox in (-P, 0, P):
            for oy in (-P, 0, P):
                X, Y = x + 5 + ox, y + 5 + oy
                if 0 <= X < N and 0 <= Y < N:
                    px[X, Y] = c
    deco(rnd, put, base, light, dark)
    # bevel + outline
    for i in range(N):
        px[i, 1] = light + (255,); px[1, i] = light + (255,)
        px[i, N - 2] = dark + (255,); px[N - 2, i] = dark + (255,)
        px[i, 0] = INK; px[i, N - 1] = INK; px[0, i] = INK; px[N - 1, i] = INK
    for (x, y) in [(0, 0), (N - 1, 0), (0, N - 1), (N - 1, N - 1)]:
        px[x, y] = (0, 0, 0, 0)
    for (x, y) in [(1, 1), (N - 2, 1), (1, N - 2), (N - 2, N - 2)]:
        px[x, y] = INK
    im.save(f'{OUT}/frame_{name}.png')
    return im

def wood(rnd, put, base, light, dark):
    # long soft grain streaks, sparse, no plank joints
    for y in range(0, P, 4):
        x = rnd.randrange(P)
        for _ in range(5):
            length = rnd.randrange(8, 22)
            col = dark if rnd.random() < 0.6 else light
            for i in range(length):
                put((x + i) % P, (y + rnd.choice([0, 0, 0, 1])) % P if i == 0 else (y) % P, col + (255,))
            x = (x + length + rnd.randrange(6, 14)) % P
    # a couple of knots
    for _ in range(3):
        kx, ky = rnd.randrange(P), rnd.randrange(P)
        for dx, dy in [(0, 0), (1, 0), (0, 1), (1, 1)]:
            put((kx + dx) % P, (ky + dy) % P, dark + (255,))

def birch(rnd, put, base, light, dark):
    for _ in range(30):
        x, y = rnd.randrange(P), rnd.randrange(P)
        length = rnd.randrange(2, 6)
        for i in range(length):
            put((x + i) % P, y, dark + (255,))
    for _ in range(30):
        x, y = rnd.randrange(P), rnd.randrange(P)
        for i in range(rnd.randrange(4, 9)):
            put((x + i) % P, y, light + (255,))

def grass(rnd, put, base, light, dark):
    for _ in range(110):
        x, y = rnd.randrange(P), rnd.randrange(P)
        put(x, y, light + (255,)); put(x, (y + 1) % P, dark + (255,))
    for _ in range(70):
        x, y = rnd.randrange(P), rnd.randrange(P)
        put(x, y, dark + (255,))

def night(rnd, put, base, light, dark):
    for _ in range(40):
        x, y = rnd.randrange(P), rnd.randrange(P)
        put(x, y, (150, 160, 220, 255))
    x, y = rnd.randrange(P), rnd.randrange(P)
    put(x, y, (255, 250, 220, 255)); put((x + 1) % P, y, (200, 200, 240, 255)); put((x - 1) % P, y, (200, 200, 240, 255))
    put(x, (y + 1) % P, (200, 200, 240, 255)); put(x, (y - 1) % P, (200, 200, 240, 255))
    for _ in range(60):
        x, y = rnd.randrange(P), rnd.randrange(P)
        put(x, y, dark + (255,))

def sakura(rnd, put, base, light, dark):
    wood(rnd, put, base, light, dark)
    for _ in range(6):
        x, y = rnd.randrange(P), rnd.randrange(P)
        put(x, y, (255, 214, 226, 255)); put((x + 1) % P, y, (240, 150, 178, 255)); put(x, (y + 1) % P, (240, 150, 178, 255))

def stone(rnd, put, base, light, dark):
    for _ in range(220):
        x, y = rnd.randrange(P), rnd.randrange(P)
        put(x, y, (dark if rnd.random() < 0.5 else light) + (255,))
    for _ in range(14):
        x, y = rnd.randrange(P), rnd.randrange(P)
        for i in range(rnd.randrange(3, 6)):
            put((x + i) % P, (y + (i // 2)) % P, dark + (255,))

make('classic', (139, 90, 43), (164, 110, 58), (118, 76, 37), wood)
make('birch', (232, 228, 216), (246, 244, 236), (90, 84, 76), birch)
make('grass', (84, 138, 54), (120, 178, 80), (64, 112, 42), grass)
make('night', (40, 46, 82), (62, 70, 112), (32, 36, 66), night)
make('sakura', (122, 70, 60), (146, 88, 76), (100, 56, 48), sakura)
make('stone', (140, 140, 146), (164, 164, 170), (116, 116, 124), stone)
