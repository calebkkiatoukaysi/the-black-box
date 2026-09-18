"""
Procedural art generator for "The Black Box".

Every sprite shipped in Content/ is produced by this script. All but one are drawn
from nothing; the opponent is cut from her character sheet, which is the one picture
the script reads (tools/source/opponent-portrait.png). Nothing is traced, downloaded,
or derived from third-party art. Run it from the repository root:

    python tools/generate_assets.py

It writes PNGs with a small dependency-free encoder, and reads the one it needs with
a decoder to match, so the project regenerates its art on a clean machine with
nothing but CPython installed.
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

def r(v):
    """
    Rounds half up. Python's round() is half-to-even, which turns a run of .5 coordinates
    into pairs and gaps -- the first draft's brows came out as a comb because of it.
    """
    return int(math.floor(v + 0.5))


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



# --------------------------------------------------------------------------- #
# button.png -- an animated, nine-sliceable plate for every clickable thing
# --------------------------------------------------------------------------- #
#
# The sheet is two stacked bands of the same grid:
#
#     top band (opaque)     the concrete plate itself, neutral grey so the game can
#                           tint it per button
#     bottom band (alpha)   the light bleeding out of the recessed groove, drawn
#                           white so it takes any accent colour
#
#   Row 0  idle      4 frames  the groove smouldering, never quite steady
#   Row 1  hover     6 frames  the plate kindling under the cursor, run backwards on the way out
#   Row 2  press     4 frames  bevel inverted so the plate sinks, light flaring out of the seam
#   Row 3  disabled  1 frame   grey, dead, no light at all
#
# Every frame is authored to survive a nine-slice: the corners carry the bolts and
# the elbow of the groove, the edges carry only structure that runs along them, and
# the middle is flat plate that stretches to any label width.

BUTTON_FRAME = 24
BUTTON_CORNER = 8
BUTTON_COLS = 6
BUTTON_ROWS = 4

#: Depth of the recessed groove, in pixels in from the frame border. It has to stay
#: inside the corner band (< BUTTON_CORNER) or the light would break at the seams.
BUTTON_GROOVE = 3

#: Per frame: (face brightness, inverted bevel, dead, groove alpha, how far light spreads)
BUTTON_FRAMES = {
    0: [  # idle -- a slow, uneven smoulder
        (1.00, False, False,  46, 1.05),
        (1.00, False, False,  64, 1.25),
        (1.00, False, False,  37, 0.95),
        (1.00, False, False,  55, 1.15),
    ],
    1: [  # hover -- light climbing out of the groove and onto the bevel
        (1.02, False, False,  78, 1.35),
        (1.04, False, False, 116, 1.75),
        (1.06, False, False, 152, 2.15),
        (1.08, False, False, 184, 2.55),
        (1.10, False, False, 208, 2.90),
        (1.11, False, False, 220, 3.10),
    ],
    2: [  # press -- the plate goes in, and whatever is behind it flares
        (0.86, True,  False, 255, 3.40),
        (0.82, True,  False, 238, 3.05),
        (0.84, True,  False, 219, 2.70),
        (0.86, True,  False, 210, 2.50),
    ],
    3: [  # disabled
        (0.74, False, True,    0, 0.00),
    ],
}


def _button_falloff(distance, spread):
    """Alpha multiplier for a pixel `distance` px off the groove."""
    if spread <= 0.0 or distance >= spread:
        return 0.0
    return pow(1.0 - distance / spread, 1.6)


def _button_plate(img, ox, oy, face_mul, inverted, dead, rng):
    """Draws one frame of the concrete plate into the top band."""
    S = BUTTON_FRAME

    PLATE = (40, 38, 47)
    PLATE_LT = (74, 70, 82)
    PLATE_DK = (16, 15, 20)
    GROOVE = (9, 8, 12)
    BOLT = (96, 92, 105)
    OUTLINE = (0, 0, 0, 255)

    def tone(rgb, extra=1.0):
        r, g, b = (c * face_mul * extra for c in rgb)
        if dead:
            # A disabled plate loses its blue cast as well as its light, so it reads
            # as switched off rather than merely dark.
            grey = (r + g + b) / 3.0
            r, g, b = (c * 0.35 + grey * 0.65 for c in (r, g, b))
        return (max(0, min(255, int(r))), max(0, min(255, int(g))), max(0, min(255, int(b))), 255)

    # 1. Face, grained like the box so the two read as the same material.
    #
    #    The grain has to be constant along whichever axis its region gets stretched,
    #    or the nine-slice smears one row of speckle across the whole button and the
    #    plate ends up ruled with horizontal streaks. So: corners are the only part
    #    that never stretches and get real per-pixel grain, the top and bottom edges
    #    vary only down the frame, the left and right edges only across it, and the
    #    middle -- stretched both ways -- is left flat.
    C = BUTTON_CORNER
    row_grain = [rng.randint(-3, 3) for _ in range(S)]
    col_grain = [rng.randint(-3, 3) for _ in range(S)]

    for y in range(S):
        edge_y = y < C or y >= S - C
        for x in range(S):
            edge_x = x < C or x >= S - C
            if edge_x and edge_y:
                n = rng.randint(-3, 3)
            elif edge_y:
                n = row_grain[y]
            elif edge_x:
                n = col_grain[x]
            else:
                n = 0
            put(img, ox + x, oy + y, tone((PLATE[0] + n, PLATE[1] + n, PLATE[2] + n)))

    hi, lo = (PLATE_DK, PLATE_LT) if inverted else (PLATE_LT, PLATE_DK)

    # 2. Hard silhouette, then two rings of bevel. The low side goes down first, so
    #    the top-left corner ends up lit and the bottom-right one does not.
    for x in range(S):
        put(img, ox + x, oy, OUTLINE)
        put(img, ox + x, oy + S - 1, OUTLINE)
    for y in range(S):
        put(img, ox, oy + y, OUTLINE)
        put(img, ox + S - 1, oy + y, OUTLINE)

    for i in (2, 1):
        f = 1.0 - (i - 1) * 0.35
        for x in range(i, S - i):
            put(img, ox + x, oy + S - 1 - i, tone(lo, f))
            put(img, ox + x, oy + i, tone(hi, f))
        for y in range(i, S - i):
            put(img, ox + S - 1 - i, oy + y, tone(lo, f * 0.90))
            put(img, ox + i, oy + y, tone(hi, f * 0.85))

    # 3. The groove: the only place light gets out of the plate. It is cut all the way
    #    around at a fixed distance in from the border, which is what lets the
    #    nine-slice stretch a button to any size without breaking the seam.
    g = BUTTON_GROOVE
    for x in range(g, S - g):
        put(img, ox + x, oy + g, tone(GROOVE))
        put(img, ox + x, oy + S - 1 - g, tone(GROOVE))
    for y in range(g, S - g):
        put(img, ox + g, oy + y, tone(GROOVE))
        put(img, ox + S - 1 - g, oy + y, tone(GROOVE))

    # 4. Bolts, one per corner, inside the groove where they hold the plate on.
    for bx, by, sx, sy in ((g + 2, g + 2, 1, 1), (S - 1 - g - 2, g + 2, -1, 1),
                           (g + 2, S - 1 - g - 2, 1, -1), (S - 1 - g - 2, S - 1 - g - 2, -1, -1)):
        put(img, ox + bx, oy + by, tone(BOLT))
        put(img, ox + bx + sx, oy + by, tone(BOLT, 0.80))
        put(img, ox + bx, oy + by + sy, tone(BOLT, 0.80))
        put(img, ox + bx + sx, oy + by + sy, tone(PLATE_DK))


def _button_accent(img, ox, oy, alpha, spread):
    """Draws one frame of the light mask into the bottom band, white so it can be tinted."""
    if alpha <= 0:
        return
    S = BUTTON_FRAME
    for y in range(S):
        for x in range(S):
            # Distance in from the nearest border. The groove sits at a constant inset,
            # so the light is a plain function of it and slices as cleanly as the plate.
            inset = min(x, S - 1 - x, y, S - 1 - y)
            a = int(alpha * _button_falloff(abs(inset - BUTTON_GROOVE), spread))
            if a > 0:
                put(img, ox + x, oy + y, (255, 255, 255, min(255, a)))


def build_button(seed=8812):
    band = BUTTON_ROWS * BUTTON_FRAME
    img = new_image(BUTTON_COLS * BUTTON_FRAME, band * 2)

    for row, frames in BUTTON_FRAMES.items():
        for col, (face_mul, inverted, dead, alpha, spread) in enumerate(frames):
            # One seed per frame, so the concrete grain does not crawl between frames.
            rng = random.Random(seed + row * 100 + col)
            ox, oy = col * BUTTON_FRAME, row * BUTTON_FRAME
            _button_plate(img, ox, oy, face_mul, inverted, dead, rng)
            _button_accent(img, ox, oy + band, alpha, spread)

    write_png(os.path.join(OUT, "button.png"), img)



# --------------------------------------------------------------------------- #
# panel.png -- the nine-sliceable slab every form is built on
# --------------------------------------------------------------------------- #
#
# One frame, no animation: a form does not react to the cursor, the controls on it
# do. It is the same concrete as the box and the buttons, but a shade darker and
# hollowed out, so a plate laid on top of it reads as sitting proud of the surface
# rather than dissolving into it.
#
# Like the button, everything that varies is either in a corner or runs along the
# edge it lives on, so the slab stretches to any form size without smearing.

PANEL_FRAME = 32

#: Fixed corner of the nine-slice. Must stay larger than PANEL_WELL, or the lip of
#: the recess would land in a stretched region and shear.
PANEL_CORNER = 12

#: How far in from the border the recessed well is cut.
PANEL_WELL = 6

#: How far in from the border the corner bolts sit.
PANEL_BOLT = 3


def build_panel(seed=4471):
    S = PANEL_FRAME
    C = PANEL_CORNER
    rng = random.Random(seed)
    img = new_image(S, S)

    SHELL = (30, 28, 37)
    SHELL_LT = (60, 56, 68)
    SHELL_DK = (12, 11, 16)
    WELL = (17, 16, 22)
    WELL_LT = (34, 32, 40)
    BOLT = (88, 84, 96)
    OUTLINE = (0, 0, 0, 255)

    def tone(rgb, extra=1.0):
        return tuple(max(0, min(255, int(c * extra))) for c in rgb) + (255,)

    # 1. Face. Same stretch-safe grain rule as the button: real noise only in the
    #    corners, one axis of noise along each edge, and nothing at all in the
    #    middle, which is the one region stretched both ways.
    row_grain = [rng.randint(-2, 2) for _ in range(S)]
    col_grain = [rng.randint(-2, 2) for _ in range(S)]
    for y in range(S):
        edge_y = y < C or y >= S - C
        for x in range(S):
            edge_x = x < C or x >= S - C
            if edge_x and edge_y:
                n = rng.randint(-2, 2)
            elif edge_y:
                n = row_grain[y]
            elif edge_x:
                n = col_grain[x]
            else:
                n = 0
            put(img, x, y, tone((SHELL[0] + n, SHELL[1] + n, SHELL[2] + n)))

    # 2. Silhouette and the outer bevel, lit from the top-left like everything else.
    for i in range(S):
        put(img, i, 0, OUTLINE)
        put(img, i, S - 1, OUTLINE)
        put(img, 0, i, OUTLINE)
        put(img, S - 1, i, OUTLINE)

    for i in (2, 1):
        f = 1.0 - (i - 1) * 0.40
        for x in range(i, S - i):
            put(img, x, i, tone(SHELL_LT, f))
            put(img, x, S - 1 - i, tone(SHELL_DK, f))
        for y in range(i, S - i):
            put(img, i, y, tone(SHELL_LT, f * 0.85))
            put(img, S - 1 - i, y, tone(SHELL_DK, f * 0.90))

    # 3. The well: the slab hollowed out, so the lighting inverts. Its far lip
    #    catches the light and its near lip falls into shadow, which is what sells
    #    the surface as cut into rather than laid on.
    w = PANEL_WELL
    for y in range(w, S - w):
        for x in range(w, S - w):
            put(img, x, y, tone(WELL))
    for x in range(w, S - w):
        put(img, x, w, tone(SHELL_DK))
        put(img, x, S - 1 - w, tone(WELL_LT))
    for y in range(w, S - w):
        put(img, w, y, tone(SHELL_DK, 0.90))
        put(img, S - 1 - w, y, tone(WELL_LT, 0.85))

    # 4. Bolts, in the lip between the border and the well, matching the buttons.
    b = PANEL_BOLT
    for bx, by, sx, sy in ((b, b, 1, 1), (S - 1 - b, b, -1, 1),
                           (b, S - 1 - b, 1, -1), (S - 1 - b, S - 1 - b, -1, -1)):
        put(img, bx, by, tone(BOLT))
        put(img, bx + sx, by, tone(BOLT, 0.80))
        put(img, bx, by + sy, tone(BOLT, 0.80))
        put(img, bx + sx, by + sy, tone(SHELL_DK))

    write_png(os.path.join(OUT, "panel.png"), img)



# --------------------------------------------------------------------------- #
# room.png -- the room the game is played in, drawn at a quarter size and blown up
#
# One lamp over one table and nothing else, falling off by inverse square: a room lit
# evenly is a room nobody is being kept in, and the dark in the corners does as much
# work as the light in the middle. The table's far lip is the brightest edge because
# it is the nearest thing facing the lamp, and the surface runs away into the dark
# toward the player -- which is what leaves the box a black shape to read against.
# --------------------------------------------------------------------------- #

# Drawn at a quarter of the window and blown up 4x like everything else.
ROOM_RW, ROOM_RH = 400, 225
# Where the far edge of the table meets the wall. This is also the line the opponent is
# cut off by, so it is really a decision about him: everything above it is his, and the
# table only has to be deep enough to stand the box on and reach across. It was higher
# when he was drawn smaller, and the wall it freed is what he grew into.
ROOM_HORIZON = 160
ROOM_LIP = 8                # thickness of the table's far edge

ROOM_WALL_TOP  = (17, 15, 21, 255)
ROOM_WALL_MID  = (33, 29, 36, 255)
ROOM_WALL_LOW  = (46, 40, 46, 255)
ROOM_SEAM      = (11, 10, 14, 255)
ROOM_GRIME     = (24, 20, 25, 255)

ROOM_TABLE_LIP  = (86, 74, 70, 255)
ROOM_TABLE_FAR  = (62, 53, 52, 255)
ROOM_TABLE_NEAR = (21, 18, 21, 255)

ROOM_LAMP = (196, 168, 130, 255)


ROOM_LAMP_X, ROOM_LAMP_Y = ROOM_RW / 2.0, 20.0


def build_room(seed=5150):
    rng = random.Random(seed)
    img = new_image(ROOM_RW, ROOM_RH)

    # 1. The wall, lit by one lamp and nothing else. Inverse-square falloff rather than a
    #    gradient: a room lit evenly is a room nobody is being kept in, and the darkness in
    #    the corners is doing as much work here as the light in the middle.
    for y in range(ROOM_HORIZON):
        for x in range(ROOM_RW):
            d = math.hypot((x - ROOM_LAMP_X) / 1.35, y - ROOM_LAMP_Y)
            fall = 1.0 / (1.0 + (d / 46.0) ** 2)

            c = lerp_color(ROOM_WALL_TOP, ROOM_WALL_LOW, min(1.0, fall * 1.5))

            # The wall behind the opponent picks up a little bounce off the table.
            if y > ROOM_HORIZON - 34:
                c = lerp_color(c, ROOM_WALL_MID, (y - (ROOM_HORIZON - 34)) / 34.0 * 0.35)

            put(img, x, y, c)

    # 2. Concrete panel seams, at irregular spacing -- evenly spaced ones read as wallpaper,
    #    and this is a room that was poured rather than decorated. Only visible where the
    #    lamp actually reaches.
    x = 14
    while x < ROOM_RW:
        # Never down the centre line: that is where the lamp hangs and where the opponent
        # sits, and a seam there reads as a wire coming out of their skull.
        if abs(x - ROOM_RW / 2) < 26:
            x += rng.randint(38, 62)
            continue
        for y in range(ROOM_HORIZON):
            d = math.hypot((x - ROOM_LAMP_X) / 1.35, y - ROOM_LAMP_Y)
            fall = 1.0 / (1.0 + (d / 46.0) ** 2)
            put(img, x, y, (ROOM_SEAM[0], ROOM_SEAM[1], ROOM_SEAM[2], int(170 * fall)))
        x += rng.randint(38, 62)

    # 3. Grime, and only where there is light to show it.
    for _ in range(3000):
        gx, gy = rng.randrange(ROOM_RW), rng.randrange(ROOM_HORIZON)
        d = math.hypot((gx - ROOM_LAMP_X) / 1.35, gy - ROOM_LAMP_Y)
        fall = 1.0 / (1.0 + (d / 46.0) ** 2)
        if rng.random() < fall * 0.55:
            put(img, gx, gy, (ROOM_GRIME[0], ROOM_GRIME[1], ROOM_GRIME[2], rng.randint(40, 120)))

    # 3b. The room dressed. One lamp is still all the light there is, and everything
    #     here is lit by it and only it -- so the door in the far corner is a shape in
    #     the dark and the pipe over the table has a highlight down it. Nothing goes
    #     where she sits (the middle third) or where the plate hangs (upper right):
    #     the left wall, the strip over her head, and the right wall under the plate
    #     are what is free, and that is where the room is.
    def lit_at(x, y):
        d = math.hypot((x - ROOM_LAMP_X) / 1.35, y - ROOM_LAMP_Y)
        return 1.0 / (1.0 + (d / 46.0) ** 2)

    def fixture(x, y, c, a=1.0, floor=0.34):
        # A thing on the wall, lit by the lamp with a floor under it, so that what is
        # in the corner is dim but there.
        k = floor + (1.0 - floor) * min(1.0, lit_at(x, y) * 2.2)
        put(img, x, y, (int(c[0] * k), int(c[1] * k), int(c[2] * k), int(255 * a)))

    # The wall is painted two tones, the lower band darker, with the line between them
    # at shoulder height: the way rooms like this are painted, so the lower half can
    # be scrubbed.
    for y in range(102, ROOM_HORIZON):
        for x in range(ROOM_RW):
            c = img[y][x]
            if y <= 104:
                fixture(x, y, (72, 66, 70), a=0.55)
            elif y == 105:
                put(img, x, y, (ROOM_SEAM[0], ROOM_SEAM[1], ROOM_SEAM[2], 140))
            else:
                img[y][x] = list(lerp_color(tuple(c), (30, 34, 34, 255), 0.22))

    # A steel door on the left, riveted, with a small barred window in it. Behind the
    # window is a corridor with its own light, cold, which is the only light in the
    # room that is not the lamp's -- and the only way out.
    DX0, DX1, DY0 = 10, 60, 34
    for y in range(DY0, ROOM_HORIZON):
        for x in range(DX0, DX1 + 1):
            edge = x in (DX0, DX0 + 1, DX1 - 1, DX1) or y in (DY0, DY0 + 1)
            plate = (36, 34, 42) if not edge else (22, 21, 26)
            if y >= ROOM_HORIZON - 22:
                plate = (28, 27, 33)                        # the kick plate, scuffed
            fixture(x, y, plate)
    for y in range(DY0 + 5, ROOM_HORIZON, 9):
        for x in (DX0 + 4, DX1 - 4):
            fixture(x, y, (88, 82, 90))
            fixture(x + 1, y + 1, (14, 13, 16), a=0.7)
    for y in range(52, 74):
        for x in range(24, 46):
            frame = x in (24, 45) or y in (52, 73)
            if frame:
                fixture(x, y, (18, 17, 21))
            elif x in (30, 36, 42):
                fixture(x, y, (64, 62, 70))
            else:
                # The corridor light, cold and faint, a little brighter at the top.
                t = (y - 53) / 20.0
                put(img, x, y, (44 - int(10 * t), 58 - int(12 * t), 74 - int(14 * t), 255))
    for x in range(48, 56):
        fixture(x, 108, (96, 90, 96))
        fixture(x, 109, (48, 44, 50))
    for y in range(DY0 + 8, DY0 + 20):
        fixture(DX0 - 2, y, (54, 50, 58))
    for y in range(ROOM_HORIZON - 34, ROOM_HORIZON - 22):
        fixture(DX0 - 2, y, (54, 50, 58))

    # Pipes along the top of the wall, either side of the lamp's flex, with a valve
    # where the near one turns down, and the stain the joint has been dripping onto the
    # wall for years. The highlight along the top of each is the lamp; the far side is
    # the room.
    def pipe(x0, x1, y):
        for x in range(x0, x1 + 1):
            fixture(x, y, (92, 84, 88))
            fixture(x, y + 1, (66, 60, 64))
            fixture(x, y + 2, (44, 40, 46))
            fixture(x, y + 3, (20, 18, 22))
    # Just under the run's bookkeeping along the top edge and just over the plate.
    pipe(0, 150, 11)
    pipe(250, ROOM_RW - 1, 11)
    for jx in (40, 104, 300, 356):
        for y in range(10, 16):
            fixture(jx, y, (58, 52, 58))
            fixture(jx + 1, y, (104, 96, 100))
            fixture(jx + 2, y, (58, 52, 58))
    for y in range(15, 32):
        fixture(150, y, (52, 46, 50))
        fixture(151, y, (78, 70, 74))
        fixture(152, y, (30, 27, 32))
    for a in range(0, 360, 6):
        px, py = 151 + 6.0 * math.cos(math.radians(a)), 36 + 6.0 * math.sin(math.radians(a))
        fixture(int(round(px)), int(round(py)), (96, 88, 90))
    for a in range(0, 360, 60):
        for t in range(0, 6):
            fixture(int(round(151 + t * math.cos(math.radians(a)))), int(round(36 + t * math.sin(math.radians(a)))), (70, 64, 66))
    for y in range(16, 66):
        t = (y - 16) / 50.0
        for x in range(103, 108):
            put(img, x + int(2.0 * math.sin(y * 0.3)), y, (16, 12, 16, int(90 * (1.0 - t) * (0.5 + 0.5 * math.sin(x * 2.1)))))

    # A cable slung from the flex across to the right wall, sagging.
    for x in range(int(ROOM_LAMP_X) + 4, ROOM_RW):
        t = (x - ROOM_LAMP_X - 4) / (ROOM_RW - ROOM_LAMP_X - 4)
        y = int(round(2 + 6 * t + 14 * 4 * t * (1 - t)))
        fixture(x, y, (30, 27, 32))
        fixture(x, y + 1, (14, 13, 16), a=0.7)

    # A vent low on the right wall, louvred, breathing whatever the building breathes.
    VX0, VX1, VY0, VY1 = 306, 346, 112, 132
    for y in range(VY0, VY1 + 1):
        for x in range(VX0, VX1 + 1):
            if x in (VX0, VX1) or y in (VY0, VY1):
                fixture(x, y, (60, 56, 62))
            elif (y - VY0) % 4 == 1:
                fixture(x, y, (58, 54, 60))
            elif (y - VY0) % 4 == 2:
                fixture(x, y, (12, 11, 14))
            else:
                fixture(x, y, (26, 24, 29))
    for y in range(VY1 + 1, VY1 + 18):
        t = (y - VY1) / 18.0
        for x in (VX0 + 3, VX0 + 4, VX1 - 5, VX1 - 4):
            put(img, x, y, (14, 11, 14, int(70 * (1.0 - t))))

    # A camera in the corner, up on the right where it can see the whole table, with
    # its one red light. The box is not the only thing watching.
    CX_, CY_ = 384, 100
    for y in range(CY_, CY_ + 9):
        for x in range(CX_ - 2, CX_ + 12):
            fixture(x, y, (30, 28, 34) if not (x in (CX_ - 2, CX_ + 11) or y in (CY_, CY_ + 8)) else (18, 17, 21))
    for y in range(CY_ + 2, CY_ + 7):
        for x in range(CX_ - 6, CX_ - 1):
            d = math.hypot(x - (CX_ - 3.5), y - (CY_ + 4))
            if d <= 2.6:
                fixture(x, y, (12, 12, 16) if d > 1.4 else (40, 44, 56))
    put(img, CX_ + 9, CY_ + 2, (200, 40, 36, 255))
    put(img, CX_ + 9, CY_ + 3, (120, 24, 22, 160))
    for y in range(CY_ + 9, CY_ + 16):
        fixture(CX_ + 4, y, (22, 21, 26))
    for x in range(CX_ + 4, ROOM_RW):
        fixture(x, CY_ + 15, (22, 21, 26))

    # Tally marks scratched into the paint under the vent by whoever sat here before,
    # in fives. Nobody knows what they were counting. Rounds, probably.
    def tally(x0, y0, groups):
        for gi in range(groups):
            gx = x0 + gi * 11
            for i in range(4):
                for y in range(y0, y0 + 8):
                    fixture(gx + i * 2, y, (110, 100, 100), a=0.55)
            for i in range(8):
                fixture(gx + i, y0 + 7 - i, (110, 100, 100), a=0.55)
    tally(248, 118, 3)
    tally(248, 132, 2)

    # A crack down from the top corner on the right, and one up from the table on the
    # left of her, where the wall has taken a knock.
    for (sx, sy, ex_, ey_, wob) in ((318, 0, 296, 58, 3.0), (66, ROOM_HORIZON - 1, 74, 118, 2.0)):
        n = int(abs(ey_ - sy))
        for i in range(n):
            t = i / float(n)
            x = int(round(sx + (ex_ - sx) * t + wob * math.sin(t * 9.0) + math.sin(t * 23.0)))
            y = int(round(sy + (ey_ - sy) * t))
            put(img, x, y, (ROOM_SEAM[0], ROOM_SEAM[1], ROOM_SEAM[2], int(200 * (1.0 - 0.6 * t))))
            if i % 5 == 0:
                put(img, x + 1, y, (70, 62, 66, 60))

    # 4. The lamp: flex, shade, and the filament under it.
    for y in range(0, 13):
        put(img, int(ROOM_LAMP_X), y, (26, 24, 29, 255))
    for y in range(12, 20):
        half = int((y - 11) * 1.8)
        for sx in range(int(ROOM_LAMP_X) - half, int(ROOM_LAMP_X) + half + 1):
            t = abs(sx - ROOM_LAMP_X) / max(1.0, half)
            put(img, sx, y, lerp_color((64, 57, 58, 255), (22, 20, 24, 255), t))
    for sx in range(int(ROOM_LAMP_X) - 4, int(ROOM_LAMP_X) + 5):
        a = int(255 * (1.0 - abs(sx - ROOM_LAMP_X) / 5.0))
        put(img, sx, 20, (255, 232, 196, a))
        put(img, sx, 21, (ROOM_LAMP[0], ROOM_LAMP[1], ROOM_LAMP[2], a // 2))

    # 5. The table. Its far lip is the brightest edge in the room because it is the closest
    #    thing to the lamp that faces it, and the surface runs away into the dark toward the
    #    player -- which is what leaves the box a black shape to read against.
    for y in range(ROOM_HORIZON, ROOM_HORIZON + ROOM_LIP):
        for sx in range(ROOM_RW):
            edge = abs(sx - ROOM_LAMP_X) / (ROOM_RW / 2)
            c = shade(ROOM_TABLE_LIP, (1.0 - 0.70 * edge ** 1.5) * (1.0 - 0.07 * (y - ROOM_HORIZON)))
            put(img, sx, y, c)

    for y in range(ROOM_HORIZON + ROOM_LIP, ROOM_RH):
        # Depth, compressed the way a receding plane is: most of the table's length is in
        # the first few rows under the lip.
        depth = ((y - ROOM_HORIZON - ROOM_LIP) / float(ROOM_RH - ROOM_HORIZON - ROOM_LIP)) ** 0.62
        for sx in range(ROOM_RW):
            spread = 1.0 + depth * 1.9              # the pool widens as it comes forward
            across = abs(sx - ROOM_LAMP_X) / (ROOM_RW / 2 * spread)
            pool = max(0.0, 1.0 - across ** 1.7) * (1.0 - depth) ** 1.5

            c = lerp_color(ROOM_TABLE_NEAR, ROOM_TABLE_FAR, min(1.0, pool * 1.35))
            put(img, sx, y, c)

    # Scuffs, pulled along the direction people reach across a table, and only where the
    # light lands. The first draft scattered them evenly and the table read as static.
    for _ in range(900):
        sy = rng.randrange(ROOM_HORIZON + ROOM_LIP, ROOM_RH)
        sx = rng.randrange(ROOM_RW)
        depth = ((sy - ROOM_HORIZON - ROOM_LIP) / float(ROOM_RH - ROOM_HORIZON - ROOM_LIP)) ** 0.62
        spread = 1.0 + depth * 1.9
        across = abs(sx - ROOM_LAMP_X) / (ROOM_RW / 2 * spread)
        pool = max(0.0, 1.0 - across ** 1.7) * (1.0 - depth) ** 1.5
        if rng.random() > pool * 0.75:
            continue
        for i in range(rng.randint(2, 7)):
            put(img, sx + i, sy, (150, 134, 122, rng.randint(10, 34)))

    write_png(os.path.join(OUT, "room.png"), img)



# --------------------------------------------------------------------------- #
# opponent-sheet.png -- the figure across the table: 6 poses across, 3 injuries down
#
# Columns: the three idles (hostile, even, open), talking, reaching into the box, hurt.
# Rows: untouched, hurt once, hurt twice -- StartingLives minus the lives they hold.
#
# Every cell is one finished picture. Nothing animates: a pose is a state the game
# switches to, not a sequence it plays, so there is no blink sheet. What the player
# reads is that the face is not the one that was there a moment ago.
#
# She is the one sprite in the game that is not drawn by this script. She is cut from
# her character sheet: the portrait in its corner, keyed off the sheet's background and
# sampled down to art pixels, is the even pose, and every other cell is that same
# picture with a few pixels moved. I drew her twice before this -- a lit height map,
# then a painted head to the sheet's palette -- and both were a likeness of her, which
# is not the same thing as her. The sheet is the design, so the sheet is the sprite.
# The source is tools/source/opponent-portrait.png, the portrait alone at the sheet's
# own resolution, and the whole sheet stays out of the build.
#
# She is a reference to Nikki from Obsession (2025): meant to resemble her, not to be
# her. The sheet gives the face and the hair -- long, straight, parted down the middle,
# hanging in front of the shoulders -- and four things the sheet does not have make it
# her: the hair is black, the skin is a shade toward grey, the oatmeal knit is
# recoloured to a dark top, and the near ear is tucked out of the hair with a hoop in
# it. All of it is done to the sampled picture before any pose is, and all of it is
# done as edits of what is there rather than paint over it: the hair keeps every
# streak the sheet gave it, the top keeps every fold and rib of the knit, and the ear
# takes its skin from the cheek beside it.
#
# The sheet's pixels are not on a clean grid (they run about five image pixels to the
# art pixel, and not evenly), so it is box-sampled at four image pixels per art pixel:
# a little finer than it was made at, which keeps the face rather than smearing it. At
# 6x on screen that puts her where the last one stood, the table lip to the top of the
# wall.
#
# The poses are pixel edits, and they are small on purpose. A smile lifts the corners
# of the mouth by a pixel; the hostile grin lifts them two and widens the line and puts
# a row of teeth under it; talking drops the lower lip two rows and opens the dark
# between; hurt paints the lids over the eyes and pulls the mouth open and down. The
# eyes are seven pixels wide and the mouth is ten, and one pixel is a whole expression
# at that size. Anything bigger than that and it stops being her face.
#
# Tilting the head bends the picture rather than turning it. Every row above the chin
# turns by the full angle about the base of the neck and the rows down through the
# neck turn less and less until the shoulders, which do not move -- so the hair that
# hangs in front of them stays joined to the hair on her head, and the shoulders stay
# on the table. It is sampled backwards, one source pixel per frame pixel, so nothing
# is blended and there are no holes.
# --------------------------------------------------------------------------- #

OPP_OW, OPP_OH = 107, 100

OPP_POSES = ("hostile", "even", "open", "talk", "reach", "hurt")
OPP_INJURIES = (0, 1, 2)

OPP_SOURCE = os.path.join(ROOT, "tools", "source", "opponent-portrait.png")

#: Source image pixels per art pixel.
OPP_PITCH = 4

#: The sheet's background, keyed out, and how far off it a pixel can be and still be it.
OPP_BG = (17, 17, 18)
OPP_BG_TOL = 7

#: Where the portrait sits in the frame. Its bottom row is the table lip.
OPP_OX, OPP_OY = 11, 8

# Landmarks in the portrait, in art pixels, read off the sampled picture. The eyes are
# not quite level -- her head is a touch over to one side on the sheet -- so each has
# its own rows.
OPP_EYE_L = (27, 35, 29, 33)    # x0, x1, the lash-line row, the lower-lid row
OPP_EYE_R = (43, 51, 30, 33)
OPP_MOUTH_Y = 46                # the line between the lips
OPP_MOUTH_X0, OPP_MOUTH_X1 = 33, 42
OPP_FACE_C = (38.0, 40.0)       # the middle of the face, for the paling and the bruise
OPP_NECK = (32, 46, 55, 70)     # x0, x1, y0, y1

#: The base of the neck, which the head bends about, and the rows the bend runs over:
#: full angle above the first, none below the second.
OPP_PIVOT = (39.0, 66.0)
OPP_BEND_TOP, OPP_BEND_BOTTOM = 56, 72

#: The first row of the knit's band on the sheet, and the two ends of the dark top it
#: becomes. Everything under that row that is knit-coloured is recoloured; the skin of
#: the shoulders is a good deal warmer than the knit and is told apart by that.
OPP_TEE_TOP = 79
OPP_TEE_D = (14, 12, 18, 255)
OPP_TEE_L = (78, 74, 92, 255)

#: The near ear: its centre, and its half-size. It sits against the edge of the cheek,
#: where the hair on the sheet hangs in front of it.
OPP_EAR = (23.5, 35.5, 2.0, 4.0)
OPP_GOLD = (204, 160, 84, 255)
OPP_GOLD_D = (112, 84, 40, 255)

OPP_MOUTH_DARK = (28, 8, 10, 255)
OPP_TEETH      = (218, 202, 190, 255)
OPP_LASH       = (14, 7, 8, 255)
OPP_BLOOD_D    = (58, 10, 12, 255)
OPP_BLOOD      = (134, 22, 24, 255)
OPP_BRUISE     = (92, 50, 90, 255)
OPP_SKIN_PALE  = (216, 194, 182, 255)

# Per pose: how far the head bends (degrees, negative is toward her left, the near
# side), how far it leans toward the box and drops, what the eyes are doing, and what
# the mouth is doing.
OPP_POSE = {
    # Hostile is the wrong smile: head over to one side, eyes too open, the grin wider
    # than the mouth is. Nothing about it is angry. That is what is wrong.
    "hostile": dict(tilt=-9.0, lean=0, drop=0, eyes="wide", mouth="grin"),
    "even":    dict(tilt=0.0, lean=0, drop=0, eyes="rest", mouth="rest"),
    "open":    dict(tilt=3.0, lean=0, drop=0, eyes="rest", mouth="smile"),
    "talk":    dict(tilt=0.0, lean=0, drop=0, eyes="rest", mouth="talk"),
    # Reaching: leant toward the box, eyes down on what the hand is doing.
    "reach":   dict(tilt=5.0, lean=4, drop=3, eyes="down", mouth="rest"),
    # Hurt: head down and away, eyes shut, mouth open.
    "hurt":    dict(tilt=-7.0, lean=-2, drop=4, eyes="shut", mouth="cry"),
}


def read_png(path):
    """
    Decodes an 8-bit, non-interlaced PNG into rows of [r, g, b, a].

    The mirror of write_png above, and just as small: it is here so the one sprite
    that comes from a picture needs no more than CPython to build, same as the rest.
    """
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", path
    pos, idat, plte, trns = 8, b"", None, None
    while pos < len(data):
        ln, = struct.unpack(">I", data[pos:pos + 4])
        tag, body = data[pos + 4:pos + 8], data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if tag == b"IHDR":
            w, h, depth, ctype, _, _, interlace = struct.unpack(">IIBBBBB", body)
        elif tag == b"IDAT":
            idat += body
        elif tag == b"PLTE":
            plte = body
        elif tag == b"tRNS":
            trns = body
        elif tag == b"IEND":
            break
    assert depth == 8 and interlace == 0, path
    ch = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[ctype]
    raw = zlib.decompress(idat)
    stride = w * ch
    out, prev, p = [], bytearray(stride), 0
    for _ in range(h):
        f, line = raw[p], bytearray(raw[p + 1:p + 1 + stride])
        p += 1 + stride
        for i in range(stride):
            a = line[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 1:
                line[i] = (line[i] + a) & 255
            elif f == 2:
                line[i] = (line[i] + b) & 255
            elif f == 3:
                line[i] = (line[i] + ((a + b) >> 1)) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else (b if pb <= pc else c))) & 255
        row = []
        for x in range(w):
            px = line[x * ch:(x + 1) * ch]
            if ctype == 6:
                row.append(list(px))
            elif ctype == 2:
                row.append([px[0], px[1], px[2], 255])
            elif ctype == 0:
                row.append([px[0], px[0], px[0], 255])
            elif ctype == 4:
                row.append([px[0], px[0], px[0], px[1]])
            else:
                i = px[0]
                row.append([plte[i * 3], plte[i * 3 + 1], plte[i * 3 + 2],
                            trns[i] if trns and i < len(trns) else 255])
        out.append(row)
        prev = line
    return out


def opp_load_portrait():
    """
    The portrait off the sheet, keyed and sampled down to art pixels.

    The background is found by flooding in from the border over everything within
    tolerance of the sheet's grey, rather than by colour alone: the darkest hair is
    nearly that grey and it is inside the figure, and the flood never reaches it. Each
    art pixel is the mean of the source pixels under it that are not background, with
    the fraction that are as its coverage, so the edge of her keeps the sheet's own
    soft dark rim.
    """
    src = read_png(OPP_SOURCE)
    H, W = len(src), len(src[0])
    bg = [[False] * W for _ in range(H)]
    stack = [(x, y) for x in range(W) for y in (0, H - 1)] + [(x, y) for y in range(H) for x in (0, W - 1)]
    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= W or y >= H or bg[y][x]:
            continue
        px = src[y][x]
        if max(abs(px[0] - OPP_BG[0]), abs(px[1] - OPP_BG[1]), abs(px[2] - OPP_BG[2])) > OPP_BG_TOL:
            continue
        bg[y][x] = True
        stack.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    P = OPP_PITCH
    small = []
    for by in range(H // P):
        row = []
        for bx in range(W // P):
            acc, n = [0, 0, 0], 0
            for y in range(by * P, by * P + P):
                for x in range(bx * P, bx * P + P):
                    if bg[y][x]:
                        continue
                    px = src[y][x]
                    acc[0] += px[0]
                    acc[1] += px[1]
                    acc[2] += px[2]
                    n += 1
            if n == 0:
                row.append([0, 0, 0, 0])
            else:
                row.append([acc[0] // n, acc[1] // n, acc[2] // n, 255 * n // (P * P)])
        small.append(row)
    return small


# --------------------------------------------------------------------------- #
# Pixel edits
# --------------------------------------------------------------------------- #

def _is_knit(px):
    # Knit is the skin's warmth with the colour taken out: for the same red it carries
    # more blue. The red-minus-green test alone let the sunlit ribs through, which are
    # as warm as skin in absolute terms and nowhere near it in proportion.
    return px[3] > 0 and px[0] >= 110 and (px[2] / float(px[0]) >= 0.455 or px[0] - px[1] <= 60)


def opp_edit_dress(img):
    """
    The knit made a dark top.

    Every knit-coloured pixel from the band down is moved onto a dark ramp by its
    brightness, so the ribs and the folds of the sheet's band are still there, in
    charcoal. The shoulders stay bare: the sheet's top is off the shoulder and so is a
    tee that has slipped, which is near enough to the one she wears.
    """
    for y in range(OPP_TEE_TOP, len(img)):
        for x in range(len(img[0])):
            px = img[y][x]
            # From the band's second row down everything that is not hair is top: the
            # sheet shows a sliver of chest where the band dips in the middle, and left
            # as skin it read as a hole in the cloth. The first row is sorted by colour,
            # because the shoulders are still on it.
            if px[3] == 0 or px[0] < 60:
                continue
            if y <= OPP_TEE_TOP + 1 and not _is_knit(px):
                continue
            lum = 0.3 * px[0] + 0.59 * px[1] + 0.11 * px[2]
            c = lerp_color(OPP_TEE_D, OPP_TEE_L, (lum - 80.0) / 150.0)
            img[y][x] = [c[0], c[1], c[2], px[3]]


def opp_edit_ear(img):
    """
    The near ear tucked out of the hair, and the hoop hanging off it.

    The ear is an oval painted over the hair against the edge of the cheek, in the
    skin from the cheek's own edge a row at a time, so it is lit the way the face is
    there: dark. A hollow in the middle and a darker rim make it an ear and not a
    patch. The hoop is a ring three pixels across with a hole in it, bright on top and dull below.
    """
    ex, ey, rx, ry = OPP_EAR
    for y in range(int(ey - ry) - 1, int(ey + ry) + 2):
        for x in range(int(ex - rx) - 1, int(ex + rx) + 2):
            d = math.hypot((x - ex) / rx, (y - ey) / ry)
            if d > 1.0:
                continue
            # The nearest skin on this row, a few pixels in from the edge of the cheek.
            sx = int(ex + rx) + 2
            while sx < len(img[0]) - 1 and not (img[y][sx][3] and img[y][sx][0] - img[y][sx][1] > 40):
                sx += 1
            skin = img[y][sx]
            v = 0.95 - 0.30 * max(0.0, (y - ey) / ry)
            hollow = math.hypot((x - ex - 0.5) / 1.2, (y - ey + 0.6) / 2.2)
            if hollow < 1.0:
                v *= 0.55 + 0.30 * hollow
            if d > 0.78:
                v *= 0.72
            c = shade((skin[0], skin[1], skin[2], 255), v)
            img[y][x] = [c[0], c[1], c[2], 255]
    # Three wide and four tall with a two-pixel hole, so it is a ring and not a stud.
    hx, hy = int(ex - 0.5), int(ey + ry) + 2
    for dx, dy, c in ((0, -1, OPP_GOLD), (-1, 0, OPP_GOLD), (1, 0, OPP_GOLD),
                      (-1, 1, OPP_GOLD_D), (1, 1, OPP_GOLD_D), (0, 2, OPP_GOLD_D)):
        img[hy + dy][hx + dx] = list(c)


def _copy_block(img, x0, y0, x1, y1):
    return [[list(img[y][x]) for x in range(x0, x1 + 1)] for y in range(y0, y1 + 1)]


def _paste_block(img, block, x0, y0):
    for j, row in enumerate(block):
        for i, px in enumerate(row):
            if 0 <= y0 + j < len(img) and 0 <= x0 + i < len(img[0]):
                img[y0 + j][x0 + i] = list(px)


def _shift_block(img, x0, y0, x1, y1, dx, dy):
    """Moves a block of pixels; what it leaves behind is the caller's to fill."""
    _paste_block(img, _copy_block(img, x0, y0, x1, y1), x0 + dx, y0 + dy)


def _blend(img, x, y, rgba, k):
    if 0 <= y < len(img) and 0 <= x < len(img[0]) and k > 0:
        img[y][x] = list(lerp_color(tuple(img[y][x]), rgba, min(1.0, k)))


def opp_edit_eyes(img, mode, injury):
    """
    What the eyes are doing, by moving the rows they are made of.

    wide lifts the brow and the lash line a row and doubles the row under them, so the
    eye is a pixel taller than it was. down moves the whole eye down a row under the
    lid. shut paints the lids over with the skin from the cheek below and lays a lash
    line across. The far eye, after the second hit, has its lid a row lower than the
    near one whatever the pose: it is swelling shut.
    """
    for side, (x0, x1, lash, lower) in (("L", OPP_EYE_L), ("R", OPP_EYE_R)):
        if mode == "shut":
            for y in range(lash, lower + 1):
                for x in range(x0, x1 + 1):
                    c = img[lower + 2][x]
                    img[y][x] = list(shade(tuple(c), 0.92))
            for x in range(x0 + 1, x1):
                t = (x - (x0 + x1) / 2.0) / ((x1 - x0) / 2.0)
                y = lash + 2 - int(round(1.2 * t * t))
                _blend(img, x, y, OPP_LASH, 0.9)
                _blend(img, x, y + 1, OPP_LASH, 0.3)
            continue
        if mode == "wide":
            _shift_block(img, x0, lash - 4, x1, lash, 0, -1)
            for x in range(x0, x1 + 1):
                img[lash][x] = list(img[lash + 1][x])
        elif mode == "down":
            _shift_block(img, x0, lash, x1, lower, 0, 1)
            for x in range(x0, x1 + 1):
                img[lash][x] = list(img[lash - 1][x])
        if side == "R" and injury >= 2:
            for x in range(x0 + 1, x1):
                img[lash + 1][x] = list(img[lash][x])
                img[lash][x] = list(img[lash - 1][x])


def opp_edit_mouth(img, mode):
    """
    What the mouth is doing.

    Everything is done to the ten columns of the lips. The corners are the outer three
    columns each side, and lifting or dropping them by a row is the whole difference
    between the sheet's mouth and a smile or a frown. Opening the mouth moves the lower
    lip down and puts the dark of the mouth, with or without teeth, in the gap.
    """
    if mode == "rest":
        return
    x0, x1, my = OPP_MOUTH_X0, OPP_MOUTH_X1, OPP_MOUTH_Y
    cx = (x0 + x1) / 2.0

    def lift(x, rows):
        # Slides one column of the mouth up (negative) or down by so many rows.
        if rows < 0:
            _shift_block(img, x, my - 2, x, my + 3, 0, rows)
            for k in range(-rows):
                img[my + 3 - k][x] = list(img[my + 4][x])
        elif rows > 0:
            _shift_block(img, x, my - 2, x, my + 3, 0, rows)
            for k in range(rows):
                img[my - 2 + k][x] = list(img[my - 3][x])

    if mode == "smile":
        for x in range(x0, x1 + 1):
            if abs(x - cx) >= 2.5:
                lift(x, -1)
        _blend(img, x0 - 1, my - 1, tuple(img[my - 1][x0]), 0.6)
        _blend(img, x1 + 1, my - 1, tuple(img[my - 1][x1]), 0.6)

    elif mode == "grin":
        # The corners up like the smile, then the line run on two columns past each
        # corner, and a straight row of teeth pushed in between the lips: the lower
        # lip goes down a row to make room. Wider than the mouth is, and held.
        for x in range(x0, x1 + 1):
            if abs(x - cx) >= 2.5:
                lift(x, -1)
        for k in (1, 2):
            _blend(img, x0 - k, my - 1, OPP_LASH, 0.6 - 0.2 * k)
            _blend(img, x1 + k, my - 1, OPP_LASH, 0.6 - 0.2 * k)
        _shift_block(img, x0, my + 1, x1, my + 3, 0, 1)
        for x in range(x0 + 1, x1):
            y = my + (0 if abs(x - cx) >= 2.5 else 1)
            img[y][x] = list(OPP_TEETH if x % 2 else shade(OPP_TEETH, 0.84))
            _blend(img, x, y + 1, OPP_MOUTH_DARK, 0.45)

    elif mode == "talk":
        _shift_block(img, x0 - 1, my + 1, x1 + 1, my + 3, 0, 2)
        for x in range(x0, x1 + 1):
            img[my + 1][x] = list(OPP_TEETH if x % 2 else shade(OPP_TEETH, 0.84)) if x0 + 2 <= x <= x1 - 2 else list(OPP_MOUTH_DARK)
            img[my + 2][x] = list(OPP_MOUTH_DARK)

    elif mode == "cry":
        for x in range(x0, x1 + 1):
            if abs(x - cx) >= 2.5:
                lift(x, 1)
        _shift_block(img, x0 - 1, my + 2, x1 + 1, my + 4, 0, 2)
        for x in range(x0 + 1, x1):
            img[my + 2][x] = list(OPP_MOUTH_DARK)
            img[my + 3][x] = list(OPP_MOUTH_DARK)
            _blend(img, x, my + 1, OPP_MOUTH_DARK, 0.5)


def _is_skin(px):
    return px[3] > 0 and px[0] > 120 and px[0] - px[1] > 40 and px[1] > px[2]


def opp_edit_injury(img, injury):
    """
    What has already been taken off her.

    Asymmetric and starting at a wound, because symmetry reads as decoration. The colour
    goes out of the face a little each time, and the face only: the sheet's skin is a
    warm ramp and the paling is a lerp toward grey on every pixel of it inside the
    oval of the face and the column of the neck.
    """
    if injury <= 0:
        return

    fx, fy = OPP_FACE_C
    pale = 0.12 * injury
    for y in range(len(img)):
        for x in range(len(img[0])):
            in_face = ((x - fx) / 15.0) ** 2 + ((y - fy) / 19.0) ** 2 <= 1.0
            in_neck = OPP_NECK[0] <= x <= OPP_NECK[1] and OPP_NECK[2] <= y <= OPP_NECK[3]
            if (in_face or in_neck) and _is_skin(img[y][x]):
                _blend(img, x, y, OPP_SKIN_PALE, pale)

    def run(x0, y0, length, drift):
        x = float(x0)
        for i in range(length):
            t = i / float(length)
            x += drift
            c = lerp_color(OPP_BLOOD, OPP_BLOOD_D, t ** 0.6)
            _blend(img, r(x), y0 + i, c, 0.9 - 0.5 * t)

    # One cut, through the outer end of the near brow, opened the first time something
    # landed, and the run off it down the temple past the corner of the eye.
    for i in range(5):
        _blend(img, 26 + i, 25 + (i // 3), OPP_BLOOD, 0.95)
        _blend(img, 26 + i, 26 + (i // 3), OPP_BLOOD_D, 0.8)
    run(27, 27, 15, -0.08)
    run(29, 28, 6, 0.05)

    if injury < 2:
        return

    # The second time, it split the lip, started the nose bleeding, and closed the far
    # eye: the lid is moved in opp_edit_eyes, the bruise is painted here.
    for x in range(38, 42):
        _blend(img, x, OPP_MOUTH_Y + 1, OPP_BLOOD_D, 0.85)
    run(40, OPP_MOUTH_Y + 2, 4, 0.1)
    run(36, 43, 3, 0.0)
    ex, ey = (OPP_EYE_R[0] + OPP_EYE_R[1]) / 2.0, OPP_EYE_R[2] + 1.0
    for y in range(int(ey) - 5, int(ey) + 6):
        for x in range(int(ex) - 7, int(ex) + 8):
            d = ((x - ex) / 6.5) ** 2 + ((y - ey) / 4.5) ** 2
            if d <= 1.0:
                _blend(img, x, y, OPP_BRUISE, 0.55 * (1.0 - d))


def opp_bend(img, tilt, lean, drop):
    """
    Bends the head about the base of the neck, and leans and drops it.

    Sampled backwards: every frame pixel asks which source pixel lands on it, so there
    is exactly one and no holes. The amount of turn, lean and drop is the full pose
    above the chin, nothing at the shoulders, and a straight blend down the neck
    between -- the hair in front of the shoulders bends with the neck and stays joined
    at both ends. It runs on the whole frame, not the portrait: the hair reaches the
    portrait's own edge, and bent inside that it was cut off in a straight line.
    """
    if abs(tilt) < 0.01 and lean == 0 and drop == 0:
        return img
    h, w = len(img), len(img[0])
    px, py = OPP_PIVOT[0] + OPP_OX, OPP_PIVOT[1] + OPP_OY
    top, bottom = OPP_BEND_TOP + OPP_OY, OPP_BEND_BOTTOM + OPP_OY
    out = new_image(w, h)
    for Y in range(h):
        wgt = 1.0 if Y <= top else max(0.0, (bottom - Y) / float(bottom - top))
        if wgt <= 0.0:
            out[Y] = [list(p) for p in img[Y]]
            continue
        a = math.radians(-tilt * wgt)
        ca, sa = math.cos(a), math.sin(a)
        for X in range(w):
            dx, dy = X - lean * wgt - px, Y - drop * wgt - py
            sx, sy = px + dx * ca - dy * sa, py + dx * sa + dy * ca
            ix, iy = r(sx), r(sy)
            if 0 <= ix < w and 0 <= iy < h:
                out[Y][X] = list(img[iy][ix])
    return out


# --------------------------------------------------------------------------- #
# Her colouring
#
# The sheet has her hair brown and her skin warm. She is meant to resemble Nikki, not
# to be her, and the two things that carry the resemblance without copying it are
# the colour of the hair and the cast of the skin: the hair is black, and the skin is
# a shade toward grey, the way a face goes under a lamp in a room with no windows.
# Both are done to the sampled picture as recolourings of what is there, so every
# streak the sheet painted into the hair and every bit of blush in the cheek is still
# there, at the new colour. The hair is the sheet's own -- long, straight, parted down
# the middle, hanging in front of the shoulders -- because a wolf cut was tried and it
# gave her more hair than face. She is supposed to be pretty. That is the sheet's job,
# and the less done to it the better it does it.
# --------------------------------------------------------------------------- #

# Black, with a cool grey where the lamp catches it: black hair does not shine brown.
OPP_HAIR_RAMP = ((5, 4, 7), (14, 12, 16), (27, 24, 30), (48, 44, 52), (82, 76, 88), (140, 132, 146))

#: How far the skin goes toward grey, and the grey it goes toward.
OPP_SKIN_GREY = 0.24
OPP_SKIN_GREY_TONE = (196, 190, 196, 255)


def _ramp(ramp, v):
    """The colour v of the way along a ramp, 0 at the darkest stop and 1 at the lightest."""
    v = max(0.0, min(1.0, v)) * (len(ramp) - 1)
    i = min(len(ramp) - 2, int(v))
    t = v - i
    a, b = ramp[i], ramp[i + 1]
    return (int(round(a[0] + (b[0] - a[0]) * t)),
            int(round(a[1] + (b[1] - a[1]) * t)),
            int(round(a[2] + (b[2] - a[2]) * t)), 255)


def _body_top(x):
    """The row the body starts at in this column: the neck, then the slope of the shoulder."""
    dx = abs(x - OPP_FACE_C[0])
    if dx <= 8:
        return 55.0
    if dx <= 19:
        return 64.0 + (dx - 8) * 0.6
    return 70.6 + (dx - 19) * 0.25


def _in_face(x, y):
    fx, fy = OPP_FACE_C
    return ((x - fx) / 15.5) ** 2 + ((y - fy) / 20.0) ** 2 <= 1.0


def _is_sheet_hair(px):
    """Dark and warm: the sheet's hair, as against the cold dark of the top."""
    return px[3] > 0 and max(px[0], px[1], px[2]) < 120 and px[0] > px[2] + 4


def _is_hair_here(img, x, y):
    """
    Whether the pixel at (x, y) is hair, by where it is as much as by what colour it is.

    Above the body line, everything that is not the face or the neck is hair. Over
    the shoulders the hair hangs in two falls between the chest and the bare shoulder,
    and there anything not bright enough to be lit skin is hair. On the top, only the
    dark warm strands are. Inside the oval of the face, the dark warm pixels at the
    hairline and the temples are the hair's edge and go with it.
    """
    px = img[y][x]
    if px[3] == 0:
        return False
    fx = OPP_FACE_C[0]
    dx = abs(x - fx)
    if _in_face(x, y):
        return _is_sheet_hair(px) and (y < 25 or dx > 12)
    # The forehead runs above the oval of the face, and the temples outside it: lit
    # skin there is skin, whatever the geometry says. The first draft blackened a band
    # across her brow.
    if y >= 13 and dx <= 17 and px[0] >= 150 and px[0] - px[1] >= 55:
        return False
    if dx <= 8 and y >= 55:
        return False
    if y < _body_top(x):
        return not (y > 24 and dx <= 16 and px[0] >= 100 and px[0] - px[1] > 45)
    if y < OPP_TEE_TOP:
        # The falls over the shoulders: dark, or lit but not as warm as skin is. The
        # first draft took a band of columns and painted the shoulder black with it.
        return dx >= 9 and (px[0] < 110 or px[0] - px[1] < 58)
    return _is_sheet_hair(px)


def opp_edit_hair_black(img):
    """The sheet's brown hair, black. Each pixel keeps its brightness and loses its colour."""
    h, w = len(img), len(img[0])
    for y in range(h):
        for x in range(w):
            if not _is_hair_here(img, x, y):
                continue
            px = img[y][x]
            lum = (0.3 * px[0] + 0.59 * px[1] + 0.11 * px[2]) / 255.0
            c = _ramp(OPP_HAIR_RAMP, 0.03 + 0.85 * lum)
            # Inside the face the hairline is a blend of hair and the shadow it throws
            # on the forehead; only what is plainly hair goes all the way to black, or
            # she gets a hard black band across her brow.
            k = 1.0
            if _in_face(x, y):
                k = max(0.0, min(1.0, (85 - max(px[0], px[1], px[2])) / 35.0))
            c = lerp_color((px[0], px[1], px[2], 255), c, k)
            img[y][x] = [c[0], c[1], c[2], px[3]]


def opp_edit_skin(img):
    """
    The skin, a shade toward grey.

    Everything warm enough to be skin that is not hair or the top -- the face, the
    neck, the shoulders, the lips too -- is pulled part of the way toward one cool
    grey. The blush stays, fainter; it is what keeps her from looking ill rather than
    kept.
    """
    h, w = len(img), len(img[0])
    for y in range(h):
        for x in range(w):
            px = img[y][x]
            if px[3] == 0 or px[0] < 90 or px[0] - px[1] < 30 or px[2] >= px[0]:
                continue
            c = lerp_color((px[0], px[1], px[2], 255), OPP_SKIN_GREY_TONE, OPP_SKIN_GREY)
            img[y][x] = [c[0], c[1], c[2], px[3]]


def build_opponent_frame(img, ox, oy, portrait, pose_name, injury):
    p = OPP_POSE[pose_name]
    cell = [[list(px) for px in row] for row in portrait]
    opp_edit_eyes(cell, p["eyes"], injury)
    opp_edit_mouth(cell, p["mouth"])
    opp_edit_injury(cell, injury)
    frame = new_image(OPP_OW, OPP_OH)
    for y, row in enumerate(cell):
        for x, px in enumerate(row):
            frame[OPP_OY + y][OPP_OX + x] = list(px)
    frame = opp_bend(frame, p["tilt"], p["lean"], p["drop"] + injury)
    for y, row in enumerate(frame):
        for x, px in enumerate(row):
            if px[3]:
                img[oy + y][ox + x] = list(px)


def build_opponent_sheet():
    portrait = opp_load_portrait()
    opp_edit_dress(portrait)
    opp_edit_hair_black(portrait)
    opp_edit_skin(portrait)
    opp_edit_ear(portrait)
    assert OPP_OX + len(portrait[0]) <= OPP_OW and OPP_OY + len(portrait) == OPP_OH, \
        ("the portrait no longer fits the frame", len(portrait[0]), len(portrait))
    img = new_image(OPP_OW * len(OPP_POSES), OPP_OH * len(OPP_INJURIES))
    for row, injury in enumerate(OPP_INJURIES):
        for col, pose in enumerate(OPP_POSES):
            build_opponent_frame(img, col * OPP_OW, row * OPP_OH, portrait, pose, injury)
    write_png(os.path.join(OUT, "opponent-sheet.png"), img)



# --------------------------------------------------------------------------- #
# hand-sheet.png -- the player's own arm, 5 frames from a curled hand to an open one
#
# Seen from behind and above, because it is the player's arm and that is where it
# is. It comes in from the bottom-right corner of the screen on a diagonal: the way
# your right arm crosses your own view when you reach for something in front of you.
# Frame 0 is a hand still half-curled from the table and frame 4 is open and fanned,
# which is the pose the box is waiting for.
#
# It is meant to look like a photograph of a hand shrunk to pixels, the way she does,
# and not like a drawing of one. So it is not drawn: it is modelled and lit. The arm
# is a height field -- the forearm and the back of the hand flattened tubes, each
# finger three rounded segments with a nail set into the last, the thumb two, the
# knuckles bumps that rise as the hand closes, the tendons low ridges up the back of
# the hand -- and every pixel is coloured by the angle that surface makes with the
# lamp, with the skin going red where the light comes through the edge of a finger
# and dark where two fingers meet. It is rendered at three times the size and
# averaged down, which is what gives it the soft pixels of a picture rather than the
# hard ones of a drawing. The light comes from the far end: the lamp is over the box,
# and the box is what the hand is going into, so the fingertips are the bright end
# and the elbow is in the dark near you.
#
# The first sheet was a hand on its own and it floated; the second was this arm, drawn
# with a distance field, and it read as a glove. Fingers are not tubes. The ridge of
# the tendon, the crease at the joint, the flat of the nail with its rim of skin, the
# bump of the knuckle: those are what a hand is, at any size, and they are all here.
# --------------------------------------------------------------------------- #

HAND_HW, HAND_HH = 96, 96
HAND_REACH = 5

#: Rendered at this many times the frame size and averaged down.
HAND_SS = 3

HAND_SKIN      = (224, 178, 150)        # albedo: light, warm
HAND_SKIN_RED  = (206, 110, 92)         # what it goes toward where the light comes through
HAND_KNUCKLE   = (212, 148, 126)
HAND_VEIN      = (160, 150, 176)
HAND_NAIL      = (236, 200, 188)
HAND_NAIL_TIP  = (248, 238, 230)
HAND_CUTICLE   = (198, 148, 132)
HAND_CLOTH     = (44, 44, 60)
HAND_CLOTH_L   = (92, 90, 112)
HAND_COLD      = (58, 56, 76)
HAND_RED       = (172, 54, 40)
HAND_RIM       = (10, 6, 10)

HAND_LAMP      = (-0.30, -0.62, 0.72)   # over the box, which is up and left of the arm
HAND_AMBIENT   = (0.20, 0.17, 0.22)
HAND_KEY       = (1.00, 0.92, 0.80)

# The line the arm lies along, from the elbow (off the bottom-right corner) to the
# wrist. Everything else is placed along it or across it.
HAND_ELBOW = (88.0, 104.0)
HAND_WRIST = (52.0, 50.0)

#: Where the tip of the middle finger is in the open frame, computed from the skeleton
#: and checked when the sheet is built. HandSprite anchors by it.
HAND_FINGERTIP = (27, 20)

# How far past the wrist the knuckles are, and how wide the hand is across them.
HAND_PALM = 17.0
HAND_KNUCKLE_HW = 11.5

# Per finger: offset across the knuckles (negative is the thumb side), length, and how
# far it swings outward when the hand fans open, in degrees.
HAND_FINGERS = ((-8.2, 19.0, -13.0), (-2.8, 21.5, -4.0), (2.8, 20.3, 4.0), (8.2, 16.5, 13.0))

#: The three segments of a finger as fractions of its length, and the radius at each joint.
HAND_SEGMENTS = (0.44, 0.31, 0.25)
HAND_RADII = (2.95, 2.65, 2.4, 1.95)

# Tags for what a pixel belongs to, which picks its albedo.
HAND_T_SKIN, HAND_T_NAIL, HAND_T_CLOTH = 1, 2, 3


def _norm3h(v):
    m = math.sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]) or 1.0
    return (v[0] / m, v[1] / m, v[2] / m)


HAND_LAMP = _norm3h(HAND_LAMP)
HAND_HALF = _norm3h((HAND_LAMP[0], HAND_LAMP[1], HAND_LAMP[2] + 1.0))


def _rot(vx, vy, deg):
    a = math.radians(deg)
    return vx * math.cos(a) - vy * math.sin(a), vx * math.sin(a) + vy * math.cos(a)


def _smax(a, b, k=1.6):
    """The larger of two heights, with the corner between them rounded off."""
    if a < b:
        a, b = b, a
    return a + math.log1p(math.exp(-k * (a - b))) / k


class _Field:
    """The height field, the tag under each pixel, and the albedo tints, at HAND_SS."""

    def __init__(self):
        self.W, self.H = HAND_HW * HAND_SS, HAND_HH * HAND_SS
        self.h = [[0.0] * self.W for _ in range(self.H)]
        self.tag = [[0] * self.W for _ in range(self.H)]
        self.tint = {}

    def capsule(self, x0, y0, x1, y1, r0, r1, flat, tag, lift=0.0):
        """A rounded tube from (x0, y0) to (x1, y1) with radius r0 to r1, squashed by flat."""
        S = HAND_SS
        lx, ly = x1 - x0, y1 - y0
        ll = lx * lx + ly * ly or 1.0
        rmax = max(r0, r1) + 1.0
        ax0, ay0 = int((min(x0, x1) - rmax) * S), int((min(y0, y1) - rmax) * S)
        ax1, ay1 = int((max(x0, x1) + rmax) * S) + 1, int((max(y0, y1) + rmax) * S) + 1
        for Y in range(max(0, ay0), min(self.H, ay1)):
            py = (Y + 0.5) / S
            for X in range(max(0, ax0), min(self.W, ax1)):
                px = (X + 0.5) / S
                t = max(0.0, min(1.0, ((px - x0) * lx + (py - y0) * ly) / ll))
                cx, cy = x0 + lx * t, y0 + ly * t
                d = math.hypot(px - cx, py - cy)
                rr = r0 + (r1 - r0) * t
                if d >= rr:
                    continue
                z = math.sqrt(rr * rr - d * d) * flat + lift
                old = self.h[Y][X]
                new = _smax(old, z) if old > 0.0 else z
                if new > old:
                    self.h[Y][X] = new
                    if z >= old * 0.85:
                        self.tag[Y][X] = tag

    def bump(self, x, y, r, amount):
        """A gaussian rise (or dip, when amount is negative) at (x, y)."""
        S = HAND_SS
        for Y in range(max(0, int((y - 2.5 * r) * S)), min(self.H, int((y + 2.5 * r) * S) + 1)):
            py = (Y + 0.5) / S
            for X in range(max(0, int((x - 2.5 * r) * S)), min(self.W, int((x + 2.5 * r) * S) + 1)):
                px = (X + 0.5) / S
                if self.h[Y][X] <= 0.0:
                    continue
                self.h[Y][X] += amount * math.exp(-((px - x) ** 2 + (py - y) ** 2) / (r * r))

    def ridge(self, x0, y0, x1, y1, width, amount, taper=True):
        """A low ridge (or a crease, when amount is negative) along a line."""
        S = HAND_SS
        lx, ly = x1 - x0, y1 - y0
        ll = lx * lx + ly * ly or 1.0
        m = width * 2.5
        for Y in range(max(0, int((min(y0, y1) - m) * S)), min(self.H, int((max(y0, y1) + m) * S) + 1)):
            py = (Y + 0.5) / S
            for X in range(max(0, int((min(x0, x1) - m) * S)), min(self.W, int((max(x0, x1) + m) * S) + 1)):
                px = (X + 0.5) / S
                if self.h[Y][X] <= 0.0:
                    continue
                t = ((px - x0) * lx + (py - y0) * ly) / ll
                if t < 0.0 or t > 1.0:
                    continue
                d = math.hypot(px - (x0 + lx * t), py - (y0 + ly * t))
                k = math.exp(-(d / width) ** 2)
                if taper:
                    k *= math.sin(math.pi * t) ** 0.5
                self.h[Y][X] += amount * k

    def nail(self, x, y, ux, uy, length, width):
        """
        A nail set into the end of a finger: an oval on the top of it, flattened, with
        a rim of skin round it and a fine cuticle line at its base.
        """
        S = HAND_SS
        nx, ny = -uy, ux
        m = max(length, width) + 1.0
        for Y in range(max(0, int((y - m) * S)), min(self.H, int((y + m) * S) + 1)):
            py = (Y + 0.5) / S
            for X in range(max(0, int((x - m) * S)), min(self.W, int((x + m) * S) + 1)):
                px = (X + 0.5) / S
                if self.h[Y][X] <= 0.0:
                    continue
                a = ((px - x) * ux + (py - y) * uy) / length
                b = ((px - x) * nx + (py - y) * ny) / width
                e = a * a + b * b
                if e <= 1.0:
                    self.tag[Y][X] = HAND_T_NAIL
                    # The nail is flatter than the finger under it and sits a little proud.
                    self.h[Y][X] = self.h[Y][X] * 0.92 + 0.35 * (1.0 - e)
                    self.tint[(X, Y)] = (a, e)
                elif e <= 1.5:
                    self.h[Y][X] -= 0.25 * (1.0 - (e - 1.0) / 0.5)


def hand_shade(albedo, n, spec_gain, red, ao):
    ndl = max(0.0, n[0] * HAND_LAMP[0] + n[1] * HAND_LAMP[1] + n[2] * HAND_LAMP[2])
    ndh = max(0.0, n[0] * HAND_HALF[0] + n[1] * HAND_HALF[1] + n[2] * HAND_HALF[2])
    out = []
    for i in range(3):
        v = albedo[i] * (HAND_AMBIENT[i] * ao + 0.95 * ndl * HAND_KEY[i] * ao)
        v += spec_gain * (ndh ** 26.0) * (248, 236, 224)[i] * ao
        v += red * (176, 48, 32)[i]
        out.append(max(0, min(255, int(round(v)))))
    return out


def build_hand_frame(img, ox, openness, rng):
    S = HAND_SS
    curl = 1.0 - openness
    f = _Field()

    ex, ey = HAND_ELBOW
    wx, wy = HAND_WRIST
    length = math.hypot(wx - ex, wy - ey)
    ux, uy = (wx - ex) / length, (wy - ey) / length      # along the arm, toward the box
    nx, ny = -uy, ux                                     # across it, toward the pinky side
    if nx < 0:
        nx, ny = -nx, -ny

    # Forearm and the back of the hand are one body, widening from the wrist out to
    # the knuckles, both flattened: seen from above an arm is wider than it is tall.
    kx, ky = wx + ux * HAND_PALM, wy + uy * HAND_PALM
    f.capsule(ex, ey, wx, wy, 13.5, 8.8, 0.55, HAND_T_SKIN)
    f.capsule(wx, wy, kx, ky, 8.8, HAND_KNUCKLE_HW, 0.48, HAND_T_SKIN)
    # The back of the hand domes up in the middle, over the metacarpals.
    f.bump(wx + ux * HAND_PALM * 0.55, wy + uy * HAND_PALM * 0.55, 6.0, 1.2)

    # Fingers: three segments each from the knuckle out, swinging away from the middle
    # as the hand fans. Curled, they are foreshortened -- from above, a folded finger
    # is its knuckle, its first joint, and not much else -- and the knuckles stand up.
    tips, nails = [], []
    for n_, (off, flen, swing) in enumerate(HAND_FINGERS):
        bx, by = kx + nx * off, ky + ny * off
        reach = flen * (0.40 + 0.60 * openness)
        dx, dy = _rot(ux, uy, swing * openness)
        thin = 0.92 if n_ == 3 else 1.0
        joints = [(bx, by)]
        acc = 0.0
        for seg in HAND_SEGMENTS:
            acc += seg
            joints.append((bx + dx * reach * acc, by + dy * reach * acc))
        for s in range(3):
            (x0, y0), (x1, y1) = joints[s], joints[s + 1]
            f.capsule(x0, y0, x1, y1, HAND_RADII[s] * thin, HAND_RADII[s + 1] * thin, 0.9, HAND_T_SKIN)
        # The knuckle at the base, and the joints along it: bumps, higher when curled,
        # each with a crease across the finger just past it.
        f.bump(bx, by, 2.4 * thin, 0.5 + 1.1 * curl)
        for s in (1, 2):
            jx, jy = joints[s]
            f.bump(jx, jy, 1.7 * thin, 0.25 + 0.6 * curl)
            f.ridge(jx - nx * 2.6 * thin, jy - ny * 2.6 * thin, jx + nx * 2.6 * thin, jy + ny * 2.6 * thin, 0.45, -0.45, taper=False)
        tips.append(joints[3])
        # The nail sits in the last segment, and shows once the finger is out enough.
        if openness > 0.25:
            (x2, y2), (x3, y3) = joints[2], joints[3]
            seg = math.hypot(x3 - x2, y3 - y2)
            cx_, cy_ = x2 + dx * seg * 0.58, y2 + dy * seg * 0.58
            nails.append((cx_, cy_, dx, dy, seg * 0.42, HAND_RADII[3] * thin * 0.70))

    # The thumb: off the wrist on its own side, tucked along the index when the hand is
    # curled and swung out wide when it opens. Two segments, with the nail on the far one.
    tbx, tby = wx + ux * 4.5 - nx * 7.4, wy + uy * 4.5 - ny * 7.4
    tdx, tdy = _rot(ux, uy, -(16.0 + 26.0 * openness))
    if tdx * nx + tdy * ny > 0:
        tdx, tdy = _rot(ux, uy, 16.0 + 26.0 * openness)
    tlen = 13.0 + 2.0 * openness
    tmx, tmy = tbx + tdx * tlen * 0.55, tby + tdy * tlen * 0.55
    ttx, tty = tbx + tdx * tlen, tby + tdy * tlen
    f.capsule(tbx, tby, tmx, tmy, 4.0, 3.3, 0.85, HAND_T_SKIN)
    f.capsule(tmx, tmy, ttx, tty, 3.3, 2.5, 0.85, HAND_T_SKIN)
    # The web of the thumb, filling the corner between it and the hand.
    f.capsule(tbx + tdx * 2.0, tby + tdy * 2.0, wx + ux * 8.0, wy + uy * 8.0, 4.0, 5.0, 0.35, HAND_T_SKIN)
    f.bump(tmx, tmy, 1.8, 0.3 + 0.4 * curl)
    f.ridge(tmx - 2.8 * tdy, tmy + 2.8 * tdx, tmx + 2.8 * tdy, tmy - 2.8 * tdx, 0.45, -0.4, taper=False)
    if openness > 0.25:
        seg = tlen * 0.45
        nails.append((tmx + tdx * seg * 0.58, tmy + tdy * seg * 0.58, tdx, tdy, seg * 0.42, 2.5 * 0.68))

    # Tendons up the back of the hand from the wrist to each knuckle, and the two
    # creases across the wrist.
    for off, _, _ in HAND_FINGERS:
        f.ridge(wx + nx * off * 0.45, wy + ny * off * 0.45, kx + nx * off * 0.92 - ux * 2.0, ky + ny * off * 0.92 - uy * 2.0, 0.9, 0.28)
    for i in (-1.0, 1.4):
        f.ridge(wx + ux * i - nx * 7.5, wy + uy * i - ny * 7.5, wx + ux * i + nx * 7.5, wy + uy * i + ny * 7.5, 0.5, -0.35)

    for cx_, cy_, dx, dy, ln, wd in nails:
        f.nail(cx_, cy_, dx, dy, ln, wd)

    # The cuff of a sleeve over the near end of the forearm: cloth wrapped round the arm
    # a little proud of it, with folds running down it and a hem at the far edge. It is
    # what makes this an arm coming out of a person rather than one lying on the table.
    cuff_cells = {}
    for Y in range(f.H):
        py = (Y + 0.5) / S
        for X in range(f.W):
            px = (X + 0.5) / S
            sp = (px - ex) * ux + (py - ey) * uy
            ap = (px - ex) * nx + (py - ey) * ny
            if -14.0 <= sp <= 23.0 and abs(ap) <= 16.5:
                rr = 13.5 + (8.8 - 13.5) * max(0.0, sp / length)
                rr += 1.4
                if abs(ap) >= rr:
                    continue
                z = math.sqrt(rr * rr - ap * ap) * 0.55 + 0.6
                for fold in (-8.5, 1.0, 9.0):
                    z += 0.35 * math.exp(-((ap - fold - 1.2 * math.sin(sp * 0.25)) / 1.1) ** 2)
                if sp > 21.2:
                    z += 0.5
                f.h[Y][X] = z
                f.tag[Y][X] = HAND_T_CLOTH
                cuff_cells[(X, Y)] = sp

    # Now light it. Normals from the height field, the height scaled up because the
    # field is in pixels and a finger is rounder than it is wide on this screen.
    ZS = 1.6
    out = [[[0, 0, 0, 0] for _ in range(f.W)] for _ in range(f.H)]
    noise = [[rng.uniform(-1.0, 1.0) for _ in range(f.W // 3 + 2)] for _ in range(f.H // 3 + 2)]

    def hat(X, Y):
        if 0 <= X < f.W and 0 <= Y < f.H:
            v = f.h[Y][X]
            return v if v > 0.0 else -3.0
        # Off the bottom or right edge the arm keeps going, off the screen.
        if X >= f.W or Y >= f.H:
            return f.h[min(f.H - 1, Y)][min(f.W - 1, X)]
        return -3.0

    for Y in range(f.H):
        py = (Y + 0.5) / S
        for X in range(f.W):
            z = f.h[Y][X]
            if z <= 0.0:
                continue
            px = (X + 0.5) / S
            dzdx = (hat(X + 1, Y) - hat(X - 1, Y)) * 0.5 * S
            dzdy = (hat(X, Y + 1) - hat(X, Y - 1)) * 0.5 * S
            n = _norm3h((-dzdx * ZS, -dzdy * ZS, 1.0))

            # Cavities: where something nearby stands higher, the light does not reach.
            ao = 1.0
            for ddx, ddy in ((3, 0), (-3, 0), (0, 3), (0, -3), (5, 0), (-5, 0), (0, 5), (0, -5)):
                nb = hat(X + ddx * S // 2, Y + ddy * S // 2)
                if nb > z + 0.6:
                    ao -= 0.09 * min(1.0, (nb - z - 0.6) / 1.5)
            ao = max(0.45, ao)

            # How far along the arm: 0 at the elbow, 1 at the wrist, on past that to the
            # tips. The far end is the lit end.
            s = ((px - ex) * ux + (py - ey) * uy) / length
            reachlit = 0.55 + 0.45 * max(0.0, min(1.0, s / 1.6)) ** 1.1
            tag = f.tag[Y][X]

            if tag == HAND_T_CLOTH:
                sp = cuff_cells.get((X, Y), 0.0)
                alb = HAND_CLOTH
                if sp > 21.2:
                    alb = HAND_CLOTH_L
                c = hand_shade(alb, n, 0.0, 0.0, ao * (0.5 + 0.5 * max(0.0, min(1.0, (sp + 14.0) / 37.0))))
            else:
                alb = HAND_SKIN
                # The skin is not one colour: a little mottling, redder over the
                # knuckles, a vein or two showing through the back of the hand.
                nz = noise[Y // 3][X // 3] * 0.5 + noise[(Y // 3 + 1) % len(noise)][(X // 3 + 1) % len(noise[0])] * 0.25
                alb = tuple(max(0, min(255, int(alb[i] * (1.0 + 0.085 * nz)))) for i in range(3))
                for off, _, _ in HAND_FINGERS:
                    kxx, kyy = kx + nx * off, ky + ny * off
                    kk = math.exp(-((px - kxx) ** 2 + (py - kyy) ** 2) / 9.0)
                    alb = lerp_color(alb + (255,), HAND_KNUCKLE + (255,), 0.5 * kk)[:3]
                if tag == HAND_T_NAIL:
                    a_, e_ = f.tint.get((X, Y), (0.0, 0.0))
                    alb = HAND_NAIL
                    if a_ > 0.55:
                        alb = lerp_color(HAND_NAIL + (255,), HAND_NAIL_TIP + (255,), min(1.0, (a_ - 0.55) / 0.35))[:3]
                    if a_ < -0.62 and e_ > 0.72:
                        alb = HAND_CUTICLE
                else:
                    for off in (-3.4, 2.6):
                        vx0, vy0 = wx + nx * off * 0.6, wy + ny * off * 0.6
                        vx1, vy1 = kx + nx * off * 1.4 - ux * 3.0, ky + ny * off * 1.4 - uy * 3.0
                        lx, ly = vx1 - vx0, vy1 - vy0
                        t = max(0.0, min(1.0, ((px - vx0) * lx + (py - vy0) * ly) / (lx * lx + ly * ly)))
                        dv = math.hypot(px - (vx0 + lx * t), py - (vy0 + ly * t)) + 0.6 * math.sin(t * 9.0 + off)
                        alb = lerp_color(alb + (255,), HAND_VEIN + (255,), 0.22 * math.exp(-(dv / 0.8) ** 2) * math.sin(math.pi * t))[:3]
                # Where the surface turns away from the lamp at a thin edge, the light
                # comes through the skin, red.
                ndl = max(0.0, n[0] * HAND_LAMP[0] + n[1] * HAND_LAMP[1] + n[2] * HAND_LAMP[2])
                thin = max(0.0, 1.0 - z / 2.4)
                red = 0.16 * thin * (1.0 - ndl) * reachlit
                spec = 0.10 if tag == HAND_T_NAIL else 0.035
                c = hand_shade(alb, n, spec, red, ao)
                # The box's own red on whatever is nearest it.
                glow = max(0.0, (s - 1.15) / 0.6)
                if glow > 0:
                    c = list(lerp_color(tuple(c) + (255,), HAND_RED + (255,), min(0.30, glow * 0.30))[:3])

            c = [int(v * reachlit) for v in c]
            out[Y][X] = [c[0], c[1], c[2], 255]

    # Average down to the frame, then the dark rim the sheet has round everything --
    # not along the bottom or right edge, where the arm leaves the screen.
    for y in range(HAND_HH):
        for x in range(HAND_HW):
            acc, n_ = [0, 0, 0], 0
            for Y in range(y * S, y * S + S):
                for X in range(x * S, x * S + S):
                    p = out[Y][X]
                    if p[3]:
                        acc[0] += p[0]
                        acc[1] += p[1]
                        acc[2] += p[2]
                        n_ += 1
            if n_:
                img[y][ox + x] = [acc[0] // n_, acc[1] // n_, acc[2] // n_, 255 * n_ // (S * S)]
    for y in range(HAND_HH - 1):
        for x in range(HAND_HW - 1):
            p = img[y][ox + x]
            if p[3] < 40:
                continue
            for xx, yy in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if xx < 0 or yy < 0 or (xx < HAND_HW and yy < HAND_HH and img[yy][ox + xx][3] < 40):
                    img[y][ox + x] = list(lerp_color(tuple(p), HAND_RIM + (p[3],), 0.5))
                    break

    return tips[1]


def build_hand_sheet():
    img = new_image(HAND_HW * HAND_REACH, HAND_HH)
    rng = random.Random(9090)
    tip = None
    for fr in range(HAND_REACH):
        tip = build_hand_frame(img, fr * HAND_HW, fr / (HAND_REACH - 1), random.Random(9090))
    # The open frame's middle fingertip is what HandSprite anchors by; if the geometry
    # moves it, HAND_FINGERTIP and HandSprite.Fingertip have to move with it.
    print("  middle fingertip, open frame: (%.1f, %.1f)" % tip)
    assert abs(tip[0] - HAND_FINGERTIP[0]) <= 1.5 and abs(tip[1] - HAND_FINGERTIP[1]) <= 1.5, \
        ("the fingertip moved", tip, HAND_FINGERTIP)
    write_png(os.path.join(OUT, "hand-sheet.png"), img)



if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("Generating sprites for The Black Box...")
    build_ash_drift()
    build_black_box()
    build_eye_sheet()
    build_pupil()
    build_glow()
    build_mote()
    build_button()
    build_panel()
    build_room()
    build_opponent_sheet()
    build_hand_sheet()
    print("Done.")
