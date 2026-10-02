"""
The lobby for The Black Box: the room the players wait in between tables, seen from above.

    python tools/generate_lobby.py

lobby-tiles.png is a sheet of 16x16 tiles (floor, wall faces, wall tops, the painted
threshold in front of the box's door). Every prop is its own PNG under Content/Lobby, so the
game can read each one's size straight off the texture. The lamp light is not painted in: the
game lays the dark over the room itself, so a figure walking out of a pool of light goes dark too.

Same rules as the other two scripts: CPython only, seeded, so a rerun gives the same bytes.
"""

import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generate_assets import OUT, new_image, put, write_png, lerp_color  # noqa: E402

LOBBY_OUT = os.path.join(OUT, "Lobby")

TILE = 16
TILE_COLUMNS = 8

# The concrete of the table's room, lit a little brighter: the game darkens it again.
FLOOR = (60, 56, 63)
FLOOR_D = (44, 41, 48)
FLOOR_L = (74, 70, 76)
CRACK = (30, 28, 34)
STAIN = (50, 44, 42)
WALL = (50, 45, 54)
WALL_D = (36, 32, 40)
WALL_L = (64, 59, 68)
SKIRT = (30, 27, 33)
TOP = (21, 19, 25)
TOP_L = (40, 37, 45)
SEAM = (24, 21, 27)
STEEL = (104, 102, 110)
STEEL_D = (54, 52, 60)
STEEL_L = (150, 148, 156)
RUST = (104, 56, 32)
RUST_D = (58, 30, 18)
HAZARD = (176, 118, 44)
HAZARD_D = (24, 20, 22)
RED = (204, 40, 32)
RED_L = (255, 120, 92)
RED_D = (70, 14, 12)
CHALK = (150, 144, 140)
LOCKER = (66, 74, 70)
LOCKER_D = (40, 46, 44)
LOCKER_L = (92, 102, 96)
CLOTH = (112, 106, 98)
CLOTH_D = (78, 72, 68)
CLOTH_L = (140, 134, 124)
BLANKET = (62, 66, 80)
BLANKET_D = (42, 44, 56)
BLANKET_L = (84, 88, 104)
WOOD = (84, 64, 46)
WOOD_D = (56, 42, 30)
LAMP = (232, 214, 170)
OUTLINE = (14, 12, 16)


def rgba(c, a=255):
    return (c[0], c[1], c[2], a)


def shade(c, k):
    return (max(0, min(255, int(c[0] * k))), max(0, min(255, int(c[1] * k))), max(0, min(255, int(c[2] * k))))


def fill(img, x0, y0, x1, y1, c):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(img, x, y, rgba(c))


def speckle(img, x0, y0, x1, y1, rng, amount, lo=0.92, hi=1.08):
    """Breaks a flat area up into grain, the way poured concrete never is one colour."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if rng.random() < amount:
                p = img[y][x]
                if p[3] == 0:
                    continue
                k = rng.uniform(lo, hi)
                img[y][x] = [min(255, int(p[0] * k)), min(255, int(p[1] * k)), min(255, int(p[2] * k)), p[3]]


def crack(img, x, y, steps, rng, colour=CRACK, ox=0, oy=0, size=TILE):
    """A wandering hairline crack. Kept inside the tile so the tiles still meet cleanly."""
    for _ in range(steps):
        if 1 <= x < size - 1 and 1 <= y < size - 1:
            put(img, ox + x, oy + y, rgba(colour, 200))
        x += rng.choice((-1, 0, 1, 1))
        y += rng.choice((0, 1, 1))


# --------------------------------------------------------------------------- #
# lobby-tiles.png
# --------------------------------------------------------------------------- #

#: What each tile is, in sheet order. The game names them in LobbyTile and picks them by this index.
TILES = (
    "floor", "floor-worn", "floor-cracked", "floor-stained",
    "drain", "hazard", "floor-shadow", "floor-scuffed",
    "wall-top", "wall-top-lip", "wall-pipe", "wall-panel",
    "wall-tally", "wall-vent", "wall-skirt", "wall-skirt-damp",
    "wall-plain", "floor-b", "floor-c",
)


def floor_base(img, ox, oy, rng):
    for y in range(TILE):
        for x in range(TILE):
            k = rng.uniform(0.94, 1.06)
            put(img, ox + x, oy + y, rgba(shade(FLOOR, k)))
    # A few pits of aggregate and lighter flecks. Faint, and only a few: the same tile is laid
    # hundreds of times, and anything strong in it shows up as a grid across the floor.
    for _ in range(3):
        put(img, ox + rng.randrange(TILE), oy + rng.randrange(TILE), rgba(shade(FLOOR, 0.86)))
    for _ in range(2):
        put(img, ox + rng.randrange(TILE), oy + rng.randrange(TILE), rgba(shade(FLOOR, 1.1)))


def wall_base(img, ox, oy, rng, top_colour, bottom_colour):
    for y in range(TILE):
        c = lerp_color(rgba(top_colour), rgba(bottom_colour), y / (TILE - 1.0))
        for x in range(TILE):
            k = rng.uniform(0.95, 1.05)
            put(img, ox + x, oy + y, rgba(shade(c, k)))


def build_tiles(seed=8080):
    rng = random.Random(seed)
    rows = (len(TILES) + TILE_COLUMNS - 1) // TILE_COLUMNS
    img = new_image(TILE * TILE_COLUMNS, TILE * rows)
    for i, name in enumerate(TILES):
        ox, oy = (i % TILE_COLUMNS) * TILE, (i // TILE_COLUMNS) * TILE
        if name.startswith("floor") or name in ("drain", "hazard"):
            floor_base(img, ox, oy, rng)
        if name == "floor-worn":
            for y in range(TILE):
                for x in range(TILE):
                    if (x - 8) ** 2 + (y - 8) ** 2 < 30 and rng.random() < 0.5:
                        put(img, ox + x, oy + y, rgba(FLOOR_L, 90))
        elif name == "floor-cracked":
            crack(img, 2, 3, 18, rng, ox=ox, oy=oy)
        elif name == "floor-stained":
            for y in range(TILE):
                for x in range(TILE):
                    d = math.hypot(x - 9.5, (y - 7.5) * 1.3)
                    if d < 5.5 + rng.uniform(-1, 1):
                        put(img, ox + x, oy + y, rgba(STAIN, 140))
        elif name == "floor-scuffed":
            for _ in range(5):
                x, y = rng.randrange(2, 12), rng.randrange(2, 14)
                for k in range(rng.randint(2, 4)):
                    put(img, ox + x + k, oy + y, rgba(FLOOR_D, 160))
        elif name == "floor-shadow":
            # Under the wall: the floor goes dark toward the top of the tile.
            for y in range(TILE):
                for x in range(TILE):
                    a = int(150 * (1.0 - y / TILE) ** 1.6)
                    put(img, ox + x, oy + y, rgba(OUTLINE, a))
        elif name == "drain":
            fill(img, ox + 3, oy + 3, ox + 12, oy + 12, STEEL_D)
            for y in range(4, 12):
                for x in range(4, 12):
                    put(img, ox + x, oy + y, rgba(OUTLINE if y % 2 == 0 else STEEL_D))
            for x in range(3, 13):
                put(img, ox + x, oy + 3, rgba(STEEL))
            for y in range(3, 13):
                put(img, ox + 3, oy + y, rgba(STEEL))
            put(img, ox + 9, oy + 13, rgba(RUST, 160))
            put(img, ox + 10, oy + 13, rgba(RUST_D, 160))
        elif name == "hazard":
            # Painted stripes, worn through to the concrete where people stand waiting.
            for y in range(TILE):
                for x in range(TILE):
                    stripe = ((x + y) // 4) % 2 == 0
                    if rng.random() < 0.18:
                        continue
                    put(img, ox + x, oy + y, rgba(HAZARD if stripe else HAZARD_D, 210))
        elif name in ("wall-top", "wall-top-lip"):
            for y in range(TILE):
                for x in range(TILE):
                    put(img, ox + x, oy + y, rgba(shade(TOP, rng.uniform(0.9, 1.1))))
            if name == "wall-top-lip":
                # The edge of the wall where it drops away into the room, caught by the light.
                for x in range(TILE):
                    put(img, ox + x, oy + TILE - 2, rgba(TOP_L))
                    put(img, ox + x, oy + TILE - 1, rgba(shade(TOP_L, 0.8)))
        elif name.startswith("wall-"):
            if name in ("wall-skirt", "wall-skirt-damp"):
                wall_base(img, ox, oy, rng, WALL_D, SKIRT)
                for x in range(TILE):
                    put(img, ox + x, oy + 4, rgba(SEAM))
                    put(img, ox + x, oy + 3, rgba(WALL_L, 120))
                if name == "wall-skirt-damp":
                    for x in range(TILE):
                        for y in range(5, TILE):
                            if rng.random() < 0.35:
                                put(img, ox + x, oy + y, rgba((30, 34, 30), 120))
            else:
                wall_base(img, ox, oy, rng, WALL_L if name == "wall-pipe" else WALL, WALL)
            if name == "wall-pipe":
                # The pipe along the top of the wall, same as the one over the table.
                for x in range(TILE):
                    put(img, ox + x, oy + 5, rgba(STEEL_L))
                    put(img, ox + x, oy + 6, rgba(STEEL))
                    put(img, ox + x, oy + 7, rgba(STEEL))
                    put(img, ox + x, oy + 8, rgba(STEEL_D))
                    put(img, ox + x, oy + 9, rgba(OUTLINE, 120))
                put(img, ox + 11, oy + 7, rgba(RUST))
                put(img, ox + 12, oy + 8, rgba(RUST_D))
                put(img, ox + 11, oy + 10, rgba(RUST_D, 160))
                put(img, ox + 11, oy + 11, rgba(RUST_D, 90))
            elif name == "wall-panel":
                for y in range(TILE):
                    put(img, ox + 0, oy + y, rgba(SEAM))
                    put(img, ox + 1, oy + y, rgba(WALL_L, 90))
            elif name == "wall-tally":
                # Somebody has been counting.
                for gx in (1, 9):
                    for k in range(4):
                        for y in range(4, 11):
                            put(img, ox + gx + 2 * k, oy + y, rgba(CHALK, 170))
                    for k in range(8):
                        put(img, ox + gx - 1 + k, oy + 9 - k * 5 // 8, rgba(CHALK, 200))
            elif name == "wall-vent":
                fill(img, ox + 2, oy + 3, ox + 13, oy + 12, STEEL_D)
                for y in range(4, 12, 2):
                    for x in range(3, 13):
                        put(img, ox + x, oy + y, rgba(OUTLINE))
                for x in range(2, 14):
                    put(img, ox + x, oy + 3, rgba(STEEL))
    write_png(os.path.join(OUT, "lobby-tiles.png"), img)


# --------------------------------------------------------------------------- #
# Props: one PNG each under Content/Lobby. Drawn as seen from slightly above, lit from the top left.
# --------------------------------------------------------------------------- #

def outline(img):
    """A dark rim round anything opaque, so props sit on the floor instead of melting into it."""
    h, w = len(img), len(img[0])
    edge = []
    for y in range(h):
        for x in range(w):
            if img[y][x][3] > 0:
                continue
            for xx, yy in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= xx < w and 0 <= yy < h and img[yy][xx][3] > 200:
                    edge.append((x, y))
                    break
    for x, y in edge:
        img[y][x] = list(rgba(OUTLINE, 230))


def bevel_box(img, x0, y0, x1, y1, base, rng=None, grain=0.0):
    """A face lit from the top left: light top and left edges, dark bottom and right."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            c = base
            if y == y0 or x == x0:
                c = shade(base, 1.22)
            elif y == y1 or x == x1:
                c = shade(base, 0.68)
            if rng is not None and grain and rng.random() < grain:
                c = shade(c, rng.uniform(0.9, 1.1))
            put(img, x, y, rgba(c))


def build_cot(rng):
    """A steel cot with a thin mattress and a folded blanket, seen from above."""
    img = new_image(34, 22)
    bevel_box(img, 1, 3, 32, 15, CLOTH, rng, 0.25)                      # mattress, top
    for x in range(1, 33):
        put(img, x, 16, rgba(CLOTH_D))
    bevel_box(img, 0, 16, 33, 18, STEEL_D)                              # the frame's front rail
    for x in (1, 32):
        fill(img, x, 19, x, 21, STEEL_D)                                # legs
    bevel_box(img, 3, 5, 9, 13, CLOTH_L)                                # pillow
    bevel_box(img, 18, 3, 32, 15, BLANKET, rng, 0.2)                     # the blanket over the foot
    for y in range(4, 15):
        put(img, 18, y, rgba(BLANKET_L))
    for y in (7, 11):
        for x in range(19, 32):
            put(img, x, y, rgba(BLANKET_D))
    # Stains, because nobody has washed it.
    for _ in range(3):
        x, y = rng.randrange(11, 17), rng.randrange(5, 13)
        put(img, x, y, rgba(STAIN, 200))
        put(img, x + 1, y, rgba(STAIN, 140))
    outline(img)
    return img


def build_locker(rng):
    """A tall steel locker, its door, three vents and a handle."""
    img = new_image(16, 42)
    bevel_box(img, 1, 0, 14, 3, LOCKER_L)                                # the top, seen from above
    bevel_box(img, 1, 4, 14, 40, LOCKER, rng, 0.12)                      # the door
    for y in (8, 10, 12):
        for x in range(4, 12):
            put(img, x, y, rgba(LOCKER_D))
            put(img, x, y + 1, rgba(LOCKER_L, 120))
    fill(img, 11, 20, 12, 25, STEEL_L)
    put(img, 12, 25, rgba(STEEL_D))
    # A dent and some rust at the foot of it.
    put(img, 6, 27, rgba(LOCKER_D))
    put(img, 7, 28, rgba(LOCKER_D))
    for x in range(2, 14):
        if rng.random() < 0.5:
            put(img, x, 39, rgba(RUST_D))
    outline(img)
    return img


def build_bench(rng):
    """A steel bench: the seat seen from above, its front edge, two legs."""
    img = new_image(42, 14)
    bevel_box(img, 1, 1, 40, 6, STEEL, rng, 0.15)
    for x in range(1, 41, 6):
        put(img, x, 3, rgba(STEEL_D))
    bevel_box(img, 1, 7, 40, 9, STEEL_D)
    for x in (3, 38):
        fill(img, x, 10, x + 1, 13, STEEL_D)
    put(img, 22, 2, rgba(RUST))
    put(img, 23, 2, rgba(RUST_D))
    outline(img)
    return img


def build_pillar(rng):
    """A square concrete column. It runs past the top of the view."""
    img = new_image(18, 52)
    for y in range(0, 50):
        for x in range(1, 17):
            k = 1.15 if x < 5 else (0.82 if x > 12 else 1.0)
            k *= rng.uniform(0.95, 1.05)
            put(img, x, y, rgba(shade(FLOOR_L, k)))
    for x in range(1, 17):
        put(img, x, 46, rgba(SEAM))
        put(img, x, 47, rgba(SKIRT))
        put(img, x, 48, rgba(SKIRT))
        put(img, x, 49, rgba(SKIRT))
    for y in range(0, 46):
        put(img, 5, y, rgba(LAMP, 40))
    crack(img, 8, 14, 14, rng, size=46)
    outline(img)
    return img


def build_crates(rng):
    """Two wooden crates, one on the other."""
    img = new_image(22, 30)
    for (x0, y0, x1, y1) in ((1, 10, 20, 28), (3, 1, 18, 11)):
        bevel_box(img, x0, y0, x1, y1, WOOD, rng, 0.2)
        for y in range(y0 + 3, y1, 4):
            for x in range(x0 + 1, x1):
                put(img, x, y, rgba(WOOD_D))
        for x in (x0 + 1, x1 - 1):
            for y in range(y0, y1 + 1):
                put(img, x, y, rgba(WOOD_D))
    outline(img)
    return img


def build_sink(rng):
    """A steel basin on the wall, a tap, and a cracked mirror over it."""
    img = new_image(18, 30)
    bevel_box(img, 3, 0, 14, 11, STEEL_D)                                # mirror frame
    fill(img, 4, 1, 13, 10, (70, 78, 86))
    for y in range(1, 11):
        put(img, 4 + y // 2, y, rgba((120, 130, 138)))
    crack(img, 10, 1, 9, rng, colour=(30, 34, 40), size=11)
    put(img, 8, 14, rgba(STEEL_L))
    put(img, 8, 15, rgba(STEEL))
    bevel_box(img, 1, 16, 16, 21, STEEL, rng, 0.1)                       # basin rim
    fill(img, 3, 17, 14, 19, STEEL_D)
    for y in range(22, 29):
        put(img, 8, y, rgba(STEEL_D))                                    # the drain pipe
    outline(img)
    return img


def build_chair(rng):
    """A steel chair, facing into the room."""
    img = new_image(16, 22)
    bevel_box(img, 2, 0, 13, 9, STEEL)                                   # back
    bevel_box(img, 1, 10, 14, 14, STEEL, rng, 0.1)                       # seat
    for x in (2, 13):
        fill(img, x, 15, x, 21, STEEL_D)
    outline(img)
    return img


def build_bucket(rng):
    img = new_image(14, 14)
    for y in range(2, 13):
        for x in range(2, 12):
            k = 1.15 if x < 5 else (0.8 if x > 9 else 1.0)
            put(img, x, y, rgba(shade(STEEL, k)))
    fill(img, 3, 1, 10, 3, OUTLINE)
    for x in range(2, 12):
        put(img, x, 1, rgba(STEEL_L))
    put(img, 6, 6, rgba(RUST))
    outline(img)
    return img


def build_camera():
    """A wall camera, two frames: the light on and off. It blinks."""
    img = new_image(28, 12)
    for f in range(2):
        ox = f * 14
        fill(img, ox + 1, 4, ox + 3, 9, STEEL_D)                         # the bracket
        bevel_box(img, ox + 3, 2, ox + 12, 8, (40, 38, 44))
        fill(img, ox + 11, 3, ox + 12, 7, OUTLINE)                       # lens hood
        put(img, ox + 10, 5, rgba((70, 80, 96)))
        put(img, ox + 5, 3, rgba(RED_L if f == 0 else RED_D))
    write_png(os.path.join(LOBBY_OUT, "camera.png"), img)


def build_door(rng):
    """The door to the box, in three frames: locked and dark, unlocked with its lamp lit, and open."""
    fw, fh = 36, 58
    img = new_image(fw * 3, fh)
    for f in range(3):
        ox = f * fw
        # The red lamp over the door.
        bevel_box(img, ox + 14, 0, ox + 21, 5, STEEL_D)
        fill(img, ox + 15, 1, ox + 20, 4, RED if f > 0 else RED_D)
        if f > 0:
            for x in range(16, 20):
                put(img, ox + x, 1, rgba(RED_L))
        # The frame.
        bevel_box(img, ox + 1, 8, ox + 34, 57, STEEL_D)
        for y in range(8, 58):
            put(img, ox + 1, y, rgba(STEEL))
        if f == 2:
            # Open: nothing behind it but dark, and the faintest red from deep inside.
            fill(img, ox + 4, 11, ox + 31, 57, (6, 4, 8))
            for y in range(40, 58):
                for x in range(4, 32):
                    a = int(60 * (y - 40) / 18.0)
                    put(img, ox + x, y, (RED_D[0], RED_D[1], RED_D[2], a))
            continue
        # The door itself: heavy steel, riveted, with a slot of a window like the one in the table's room.
        bevel_box(img, ox + 4, 11, ox + 31, 57, (60, 58, 66), rng, 0.1)
        for y in range(14, 56, 6):
            put(img, ox + 6, y, rgba(STEEL_L))
            put(img, ox + 29, y, rgba(STEEL_L))
        fill(img, ox + 12, 16, ox + 23, 21, OUTLINE)
        for x in range(13, 23, 3):
            for y in range(16, 22):
                put(img, ox + x, y, rgba(STEEL))
        # The box, painted on the door: a black square with its red eyes.
        fill(img, ox + 13, 29, ox + 22, 38, (8, 6, 10))
        for x in range(13, 23):
            put(img, ox + x, 29, rgba((40, 36, 44)))
        for ex, ey in ((15, 32), (19, 31), (17, 35), (21, 36)):
            put(img, ox + ex, ey, rgba(RED_L if f > 0 else RED))
        fill(img, ox + 27, 32, ox + 28, 37, STEEL_L)                     # the handle
        for x in range(5, 31):
            if rng.random() < 0.4:
                put(img, ox + x, 56, rgba(RUST_D))
        if f == 1:
            # Unlocked: the seam glows where it has come away from the frame.
            for y in range(11, 58):
                put(img, ox + 31, y, rgba(RED, 200))
    write_png(os.path.join(LOBBY_OUT, "arena-door.png"), img)


def build_props(seed=4242):
    rng = random.Random(seed)
    os.makedirs(LOBBY_OUT, exist_ok=True)
    for name, build in (("cot", build_cot), ("locker", build_locker), ("bench", build_bench),
                        ("pillar", build_pillar), ("crates", build_crates), ("sink", build_sink),
                        ("chair", build_chair), ("bucket", build_bucket)):
        write_png(os.path.join(LOBBY_OUT, name + ".png"), build(rng))
    build_camera()
    build_door(rng)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("Generating the lobby for The Black Box...")
    build_tiles()
    build_props()
    print("Done.")
