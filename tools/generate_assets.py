"""
Procedural art generator for "The Black Box".

Every sprite shipped in Content/ is produced by this script -- nothing is traced,
downloaded, or derived from third-party art. Run it from the repository root:

    python tools/generate_assets.py

It writes PNGs with a small dependency-free encoder, so the project regenerates
its art on a clean machine with nothing but CPython installed.
"""

import math
import os
import random
import struct
import zlib

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUT = os.path.join(ROOT, "Content")


# --------------------------------------------------------------------------- #
# Minimal RGBA PNG encoder
# --------------------------------------------------------------------------- #

def new_image(w, h, fill=(0, 0, 0, 0)):
    return [[list(fill) for _ in range(w)] for _ in range(h)]


def put(img, x, y, rgba):
    """Alpha-composites rgba over the pixel at (x, y); out-of-bounds is ignored."""
    if y < 0 or y >= len(img) or x < 0 or x >= len(img[0]):
        return
    sr, sg, sb, sa = rgba
    if sa <= 0:
        return
    if sa >= 255:
        img[y][x] = [sr, sg, sb, 255]
        return
    dr, dg, db, da = img[y][x]
    a = sa / 255.0
    out_a = sa + da * (1 - a)
    if out_a <= 0:
        img[y][x] = [0, 0, 0, 0]
        return
    img[y][x] = [
        int(round((sr * sa + dr * da * (1 - a)) / out_a)),
        int(round((sg * sa + dg * da * (1 - a)) / out_a)),
        int(round((sb * sa + db * da * (1 - a)) / out_a)),
        int(round(out_a)),
    ]


def rect(img, x0, y0, x1, y1, rgba):
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            put(img, x, y, rgba)


def write_png(path, img):
    h = len(img)
    w = len(img[0])
    raw = bytearray()
    for row in img:
        raw.append(0)  # filter type 0 (None)
        for px in row:
            raw += bytes(px)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)  # 8-bit RGBA
    blob = (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", header)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(blob)
    print("  Content/" + os.path.basename(path) + "  (" + str(w) + "x" + str(h) + ")")


def lerp_color(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(4))


def shade(rgba, factor):
    return (
        max(0, min(255, int(rgba[0] * factor))),
        max(0, min(255, int(rgba[1] * factor))),
        max(0, min(255, int(rgba[2] * factor))),
        rgba[3],
    )


# --------------------------------------------------------------------------- #
# ash-drift.png -- seamlessly tiling particulate for the dead sky behind the box
# --------------------------------------------------------------------------- #

def build_ash_drift(size=512, seed=20250904):
    rng = random.Random(seed)
    img = new_image(size, size)
    margin = 4  # keep flecks off the seam so the tile wraps invisibly

    ASH = (168, 160, 150)
    GRIT = (118, 114, 118)
    EMBER = (196, 88, 42)

    for _ in range(360):
        x = rng.randrange(margin, size - margin)
        y = rng.randrange(margin, size - margin)
        roll = rng.random()
        if roll < 0.05:                      # a few dying embers still falling
            base, alpha = EMBER, rng.randint(60, 150)
        elif roll < 0.30:                    # lit ash
            base, alpha = ASH, rng.randint(28, 120)
        else:                                # cold grit, most of the sky
            base, alpha = GRIT, rng.randint(18, 80)
        put(img, x, y, (base[0], base[1], base[2], alpha))

        # Larger flecks smudge into their neighbours rather than twinkling like stars.
        if alpha > 70:
            smudge = int(alpha * 0.32)
            for dx, dy in ((1, 0), (0, 1), (1, 1)):
                put(img, x + dx, y + dy, (base[0], base[1], base[2], smudge))

    write_png(os.path.join(OUT, "ash-drift.png"), img)


# --------------------------------------------------------------------------- #
# black-box.png -- the 128x128 centrepiece, drawn at 3x in game
# --------------------------------------------------------------------------- #

def build_black_box(seed=4041):
    """
    A shell of dead concrete around an opening that light does not come back out of.

    The shell band is deliberately thin, and the box has no lid, seam or latch, because
    none of those belong on something you are meant to put your hand into. The front
    face is not a surface, it is a hole, and the art has to say so at a glance.
    """
    rng = random.Random(seed)
    S = 128
    img = new_image(S, S)

    X0, Y0, X1, Y1 = 14, 17, 113, 116        # outer silhouette (inclusive)
    AX0, AY0, AX1, AY1 = 26, 29, 101, 104    # the aperture; everything inside is void

    OUTLINE = (0, 0, 0, 255)
    SHELL = (32, 30, 37, 255)
    SHELL_LT = (58, 55, 65, 255)
    SHELL_DK = (13, 12, 16, 255)
    RIM = (42, 40, 50)
    BOLT = (88, 84, 96, 255)
    RUST = (74, 45, 33)

    # 1. Hard silhouette so the shell separates from the sky behind it.
    rect(img, X0 - 1, Y0 - 1, X1 + 1, Y1 + 1, OUTLINE)

    # 2. Shell band, with grain so it reads as poured concrete rather than flat fill.
    for y in range(Y0, Y1 + 1):
        for x in range(X0, X1 + 1):
            n = rng.randint(-3, 3)
            put(img, x, y, (max(0, SHELL[0] + n), max(0, SHELL[1] + n), max(0, SHELL[2] + n), 255))

    # 3. Outer bevel: light from the upper left, nothing coming back from the lower right.
    for i in range(2):
        f = 1.0 - i * 0.4
        for x in range(X0 + i, X1 - i + 1):
            put(img, x, Y0 + i, shade(SHELL_LT, f))
            put(img, x, Y1 - i, SHELL_DK)
        for y in range(Y0 + i, Y1 - i + 1):
            put(img, X0 + i, y, shade(SHELL_LT, f * 0.85))
            put(img, X1 - i, y, SHELL_DK)

    # 4. Soft vertical bands where the dark smears further out than it should. Built as
    #    overlapping bumps rather than single columns, so they read as smudges in the
    #    murk instead of scratches ruled down the front of it.
    smear = {}
    for _ in range(5):
        centre = rng.randint(AX0 + 4, AX1 - 4)
        width = rng.randint(2, 5)
        amplitude = rng.uniform(0.10, 0.26)
        for dx in range(-width, width + 1):
            falloff = amplitude * (1.0 - abs(dx) / float(width + 1))
            smear[centre + dx] = smear.get(centre + dx, 0.0) + falloff

    # 5. The void.
    #
    #    The falloff follows a superellipse rather than the distance to the nearest edge.
    #    A nearest-edge field creases along the diagonals, and those four seams make the
    #    opening read as a bevelled picture frame -- exactly the wrong thing. A rounded
    #    square has no seams, still follows the shape of the mouth, and just goes dark.
    #
    #    Light also has to fall off directionally, or the hole looks painted on. The bias
    #    is a smooth function of the angle toward the upper left, where the key light is.
    cx, cy = (AX0 + AX1) / 2.0, (AY0 + AY1) / 2.0
    hw, hh = (AX1 - AX0) / 2.0, (AY1 - AY0) / 2.0
    SQUARENESS = 5.0     # 2 would be an ellipse; high values approach the square aperture
    REACH = 0.40         # fraction of the way in that any light survives at all

    for y in range(AY0, AY1 + 1):
        for x in range(AX0, AX1 + 1):
            nx, ny = (x - cx) / hw, (y - cy) / hh
            d = pow(pow(abs(nx), SQUARENESS) + pow(abs(ny), SQUARENESS), 1.0 / SQUARENESS)

            inside = 1.0 - d                       # 0 at the mouth, 1 at the centre
            reach = REACH * (1.0 + smear.get(x, 0.0))
            if inside >= reach or d > 1.0:
                put(img, x, y, OUTLINE)            # absolute black, the bottom of the void
                continue

            facing = 0.34 + 0.66 * ((-nx - ny) * 0.5 + 1.0) * 0.5
            brightness = pow(1.0 - inside / reach, 2.0) * facing + rng.uniform(-0.04, 0.04)
            put(img, x, y, (
                max(0, int(RIM[0] * brightness)),
                max(0, int(RIM[1] * brightness)),
                max(0, int(RIM[2] * brightness)),
                255))

    # 6. The lower lip catches light; the upper lip casts the shadow that falls inside.
    for x in range(AX0 - 1, AX1 + 2):
        put(img, x, AY0 - 1, SHELL_DK)
        put(img, x, AY1 + 1, shade(SHELL_LT, 0.75))
    for y in range(AY0 - 1, AY1 + 2):
        put(img, AX0 - 1, y, SHELL_DK)
        put(img, AX1 + 1, y, shade(SHELL_LT, 0.60))

    # 7. Corner reinforcement plates, bolted on.
    ARM, THICK = 16, 4
    for cx, cy, sx, sy in ((X0, Y0, 1, 1), (X1, Y0, -1, 1), (X0, Y1, 1, -1), (X1, Y1, -1, -1)):
        for i in range(ARM):
            for t in range(THICK):
                tone = shade(SHELL_LT, 0.80) if t < THICK - 1 else SHELL_DK
                put(img, cx + sx * i, cy + sy * t, tone)
                put(img, cx + sx * t, cy + sy * i, tone)
        for bx, by in ((2, 2), (ARM - 4, 1), (1, ARM - 4)):
            px, py = cx + sx * bx, cy + sy * by
            rect(img, px, py, px + 1, py + 1, BOLT)
            put(img, px + 1, py + 1, SHELL_DK)

    # 8. Rust weeping down the shell.
    for _ in range(26):
        x, y = _shell_pixel(rng, X0, Y0, X1, Y1, AX0, AY0, AX1, AY1)
        for run in range(rng.randint(2, 7)):
            put(img, x, y + run, (RUST[0], RUST[1], RUST[2], rng.randint(30, 95)))

    # 9. Chips and pits in the concrete.
    for _ in range(70):
        x, y = _shell_pixel(rng, X0, Y0, X1, Y1, AX0, AY0, AX1, AY1)
        if rng.random() < 0.6:
            put(img, x, y, (62, 59, 70, rng.randint(40, 120)))
        else:
            put(img, x, y, (0, 0, 0, rng.randint(50, 130)))

    write_png(os.path.join(OUT, "black-box.png"), img)


def _shell_pixel(rng, X0, Y0, X1, Y1, AX0, AY0, AX1, AY1):
    """Picks a random pixel on the shell band, never inside the aperture."""
    if rng.random() < 0.5:
        x = rng.randint(X0, X1)
        y = rng.choice([rng.randint(Y0, AY0 - 2), rng.randint(AY1 + 2, Y1)])
    else:
        x = rng.choice([rng.randint(X0, AX0 - 2), rng.randint(AX1 + 2, X1)])
        y = rng.randint(Y0, Y1)
    return x, y


# --------------------------------------------------------------------------- #
# eye-sheet.png -- 5 blink frames of 16x16, wide open through fully shut
# --------------------------------------------------------------------------- #

EYE_OPENNESS = (1.0, 0.72, 0.42, 0.18, 0.0)


def build_eye_sheet():
    FS = 16
    img = new_image(FS * len(EYE_OPENNESS), FS)
    cx = cy = 7.5
    RX, RY = 6.7, 4.4

    CORE = (255, 176, 138, 255)
    HOT = (247, 58, 40, 255)
    MID = (198, 24, 26, 255)
    OUTER = (124, 11, 16, 255)
    LINER = (28, 4, 7, 255)

    for f, openness in enumerate(EYE_OPENNESS):
        ox = f * FS

        if openness <= 0.02:
            # Shut: a dim ember-line where the lids meet.
            for x in range(2, 14):
                t = abs(x - cx) / RX
                a = max(0, int(235 * (1.0 - t * t)))
                put(img, ox + x, 8, (104, 20, 22, a))
                put(img, ox + x, 7, (34, 6, 9, int(a * 0.55)))
                put(img, ox + x, 9, (26, 4, 7, int(a * 0.40)))
            continue

        ry = RY * openness
        rx2, ry2 = RX + 1.0, ry + 1.0
        for y in range(FS):
            for x in range(FS):
                dx, dy = (x - cx) / RX, (y - cy) / ry
                d = dx * dx + dy * dy
                if d <= 1.0:
                    t = math.sqrt(d)
                    if t < 0.40:
                        c = CORE
                    elif t < 0.70:
                        c = lerp_color(CORE, HOT, (t - 0.40) / 0.30)
                    elif t < 0.88:
                        c = lerp_color(HOT, MID, (t - 0.70) / 0.18)
                    else:
                        c = lerp_color(MID, OUTER, (t - 0.88) / 0.12)
                    if y < cy - ry * 0.22:        # lid shadow across the top
                        c = shade(c, 0.60)
                    elif y > cy + ry * 0.55:      # bounce light off the lower lid
                        c = shade(c, 1.10)
                    put(img, ox + x, y, c)
                else:
                    dx2, dy2 = (x - cx) / rx2, (y - cy) / ry2
                    if dx2 * dx2 + dy2 * dy2 <= 1.0:
                        put(img, ox + x, y, LINER)

    write_png(os.path.join(OUT, "eye-sheet.png"), img)


# --------------------------------------------------------------------------- #
# pupil.png -- drawn over the eye and offset to aim the gaze
# --------------------------------------------------------------------------- #

def build_pupil():
    S = 8
    img = new_image(S, S)
    c, r = 3.5, 3.0
    for y in range(S):
        for x in range(S):
            d = math.hypot(x - c, y - c)
            if d <= r - 1.0:
                put(img, x, y, (6, 4, 9, 255))
            elif d <= r:
                put(img, x, y, (46, 7, 12, 255))
    put(img, 2, 2, (255, 206, 190, 225))   # specular catch-light
    put(img, 3, 2, (255, 176, 160, 110))
    write_png(os.path.join(OUT, "pupil.png"), img)


# --------------------------------------------------------------------------- #
# glow.png -- soft radial falloff, tinted red and blended additively
# --------------------------------------------------------------------------- #

def build_glow(size=64):
    img = new_image(size, size)
    c = (size - 1) / 2.0
    r = size / 2.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / r
            if d >= 1.0:
                continue
            a = int(255 * pow(1.0 - d, 2.4))
            if a > 0:
                put(img, x, y, (255, 255, 255, a))
    write_png(os.path.join(OUT, "glow.png"), img)


# --------------------------------------------------------------------------- #
# mote.png -- a speck of ash on its way into the void
# --------------------------------------------------------------------------- #

def build_mote(size=8):
    img = new_image(size, size)
    c = (size - 1) / 2.0
    r = size / 2.0
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - c, y - c) / r
            if d >= 1.0:
                continue
            a = int(255 * pow(1.0 - d, 1.8))
            if a > 0:
                put(img, x, y, (214, 208, 200, a))
    write_png(os.path.join(OUT, "mote.png"), img)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("Generating sprites for The Black Box...")
    build_ash_drift()
    build_black_box()
    build_eye_sheet()
    build_pupil()
    build_glow()
    build_mote()
    print("Done.")
