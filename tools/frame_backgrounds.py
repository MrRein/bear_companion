# Frame backgrounds as a drawn pattern (boards / bricks), seamless every 40 px; 50x50, 9-slice 5.
import random, sys
from PIL import Image
OUT = sys.argv[1]
INK = (13, 18, 28, 255)
N, P, S = 50, 40, 5

def mix(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)

def frame(name, draw, edge_light, edge_dark):
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    px = im.load()
    tile = [[None] * P for _ in range(P)]
    draw(random.Random(name), tile)
    for y in range(N):
        for x in range(N):
            px[x, y] = tile[(y - S) % P][(x - S) % P]
    for i in range(N):
        px[i, 1] = edge_light; px[1, i] = edge_light
        px[i, N - 2] = edge_dark; px[N - 2, i] = edge_dark
        px[i, 0] = INK; px[i, N - 1] = INK; px[0, i] = INK; px[N - 1, i] = INK
    for (x, y) in [(0, 0), (N - 1, 0), (0, N - 1), (N - 1, N - 1)]: px[x, y] = (0, 0, 0, 0)
    for (x, y) in [(1, 1), (N - 2, 1), (1, N - 2), (N - 2, N - 2)]: px[x, y] = INK
    im.save(f'{OUT}/frame_{name}.png')

def boards(base, light, dark, gap, deco=None):
    def draw(rnd, t):
        H = 8
        for b in range(P // H):
            y0 = b * H
            joint = (b * 17 + 9) % P          # staggered joints
            for y in range(H):
                for x in range(P):
                    c = base
                    if y == 0: c = light                       # top highlight of the board
                    elif y == H - 1: c = gap                   # dark gap between boards
                    elif y == H - 2: c = dark                  # board's own shadow
                    t[y0 + y][x] = c
            # grain: two soft streaks per board, wrapping
            for k in range(3):
                gy = y0 + 2 + (k * 2 + b) % 4
                gx = rnd.randrange(P)
                for i in range(rnd.randrange(6, 14)):
                    t[gy][(gx + i) % P] = mix(base, dark, 0.55)
            # joint: dark line + light line after it, nails both sides
            for y in range(1, H - 1):
                t[y0 + y][joint] = gap
                t[y0 + y][(joint + 1) % P] = mix(base, light, 0.6)
            for nx in ((joint - 2) % P, (joint + 3) % P):
                t[y0 + 3][nx] = mix(base, gap, 0.8)
            if deco: deco(rnd, t, y0, H, joint)
    return draw

def bricks(base, light, dark, mortar):
    def draw(rnd, t):
        H, W = 8, 20
        for r in range(P // H):
            y0, off = r * H, (r % 2) * (W // 2)
            for y in range(H):
                for x in range(P):
                    bx = (x + off) % W
                    c = base
                    if y == H - 1 or bx == W - 1: c = mortar
                    elif y == 0 or bx == 0: c = light
                    elif y == H - 2 or bx == W - 2: c = dark
                    t[y0 + y][x] = c
            for _ in range(5):
                x, y = rnd.randrange(P), y0 + rnd.randrange(2, H - 2)
                if t[y][x] == base: t[y][x] = mix(base, dark, 0.5)
    return draw

def petals(rnd, t, y0, H, joint):
    x = (joint + 10 + rnd.randrange(12)) % P
    if rnd.random() < 0.6:
        t[y0 + 3][x] = (255, 214, 226, 255); t[y0 + 3][(x + 1) % P] = (240, 150, 178, 255); t[y0 + 4][x] = (240, 150, 178, 255)

def stars(rnd, t, y0, H, joint):
    x = (joint + 8 + rnd.randrange(20)) % P
    t[y0 + 3 + rnd.randrange(2)][x] = (200, 206, 245, 255)

def leaves(rnd, t, y0, H, joint):
    x = (joint + 8 + rnd.randrange(20)) % P
    t[y0 + 3][x] = (150, 206, 100, 255); t[y0 + 4][(x + 1) % P] = (150, 206, 100, 255)

def birchmarks(rnd, t, y0, H, joint):
    if rnd.random() < 0.7:
        x, y = (joint + 8 + rnd.randrange(20)) % P, y0 + 3 + rnd.randrange(2)
        for i in range(rnd.randrange(2, 4)):
            t[y][(x + i) % P] = (90, 84, 76, 255)

C = lambda *v: tuple(v) + (255,)
frame('classic', boards(C(146, 94, 46), C(184, 128, 72), C(118, 74, 36), C(78, 48, 26)), C(196, 140, 84), C(70, 44, 22))
frame('sakura', boards(C(128, 74, 62), C(166, 108, 94), C(102, 56, 48), C(70, 38, 34), petals), C(176, 118, 104), C(64, 34, 30))
frame('birch', boards(C(230, 226, 214), C(248, 246, 238), C(204, 198, 184), C(150, 144, 132), birchmarks), C(252, 250, 244), C(140, 134, 122))
frame('grass', boards(C(84, 138, 54), C(122, 176, 80), C(66, 112, 42), C(44, 80, 30), leaves), C(134, 188, 92), C(40, 72, 26))
frame('night', boards(C(42, 48, 86), C(66, 74, 118), C(34, 38, 70), C(22, 26, 48), stars), C(78, 86, 132), C(20, 22, 42))
frame('stone', bricks(C(140, 140, 146), C(172, 172, 178), C(116, 116, 124), C(88, 88, 96)), C(184, 184, 190), C(80, 80, 88))
