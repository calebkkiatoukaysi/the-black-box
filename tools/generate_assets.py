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
# Columns: the three idles (hostile, even, open), talking, placing a hand, hurt.
# Rows: untouched, hurt once, hurt twice -- StartingLives minus the lives they hold.
#
# Every cell is one finished picture. Nothing animates: a pose is a state the game
# switches to, not a sequence it plays, so the eyes are painted in and there is no
# blink sheet. Even the smoke is still. What the player reads is that the face is not
# the one that was there a moment ago.
#
# The frame is drawn at 4x and it is big on purpose -- the table lip to the top of the
# wall, the way a visitor fills a doorway. That is the whole of the change from the
# first sheet, which was a head and a collar: this one is a person, long hair down over
# a hood, thin, tired, a cigarette held up beside the jaw, and the coat wide enough to
# come out either side of the box. The figure sits left of the box on screen, so the
# near shoulder and the raised hand are always in the open and the far shoulder is the
# one the box covers. What is under the table lip is not drawn: a person sitting behind
# a table is cut off by its edge, and drawing any lower puts them on top of it.
#
# The hair is the character. Strands run down in columns, brightness varying across
# and barely along, and every cell of the sheet is seeded the same so the same hair is
# on the same head in every pose -- switching frames changes the face and nothing else.
# --------------------------------------------------------------------------- #

OPP_OW, OPP_OH = 160, 150

OPP_POSES = ("hostile", "even", "open", "talk", "reach", "hurt")
OPP_INJURIES = (0, 1, 2)

# The head is a little right of the frame's centre. On screen the figure sits left of the
# box, so the near side of the frame is the side the raised hand lives on and it needs the
# room; the far side is the side the box covers.
OPP_CX = 84.0

OPP_HEAD_TOP, OPP_HEAD_BOTTOM = 10, 76
OPP_EYE_Y, OPP_EYE_DX, OPP_EYE_RX, OPP_EYE_RY = 42.0, 11.0, 7.2, 4.8
OPP_NOSE_TOP, OPP_NOSE_BOT = 40, 56
OPP_MOUTH_Y = 65
OPP_NECK_TOP, OPP_NECK_BOTTOM = 70, 92

OPP_VOID    = (11,  9, 13, 255)
OPP_SKIN_D  = (34, 27, 32, 255)
OPP_SKIN_M  = (86, 70, 68, 255)
OPP_SKIN_L  = (150, 128, 114, 255)
OPP_SKIN_H  = (196, 172, 150, 255)
OPP_BOX_RED = (168, 62, 44, 255)
OPP_COLD    = (58, 58, 76, 255)
OPP_SOCKET  = (16, 11, 15, 255)
OPP_MOUTH   = (14,  6,  9, 255)
OPP_LIP     = (74, 44, 46, 255)

OPP_BLOOD_D = (58, 10, 12, 255)
OPP_BLOOD   = (122, 20, 20, 255)

OPP_SCLERA  = (96, 72, 68, 255)
OPP_IRIS    = (40, 24, 28, 255)
OPP_IRIS_E  = (92, 48, 42, 255)
OPP_PUPIL   = (8,  5,  9, 255)
OPP_CATCH   = (244, 158, 118, 255)

OPP_HAIR_D  = (13, 10, 13, 255)
OPP_HAIR_M  = (36, 27, 29, 255)
OPP_HAIR_L  = (80, 60, 52, 255)

OPP_HOOD_D  = (18, 17, 24, 255)
OPP_HOOD_M  = (36, 34, 45, 255)
OPP_HOOD_L  = (66, 62, 78, 255)
OPP_HOOD_IN = (52, 48, 60, 255)
OPP_CORD    = (128, 118, 108, 255)

OPP_CIG     = (216, 208, 198, 255)
OPP_CIG_TIP = (176, 134, 92, 255)
OPP_EMBER   = (255, 118, 44, 255)
OPP_ASH     = (108, 102, 100, 255)
OPP_SMOKE   = (176, 170, 172)

# Half-width of the skull, row by row. Wider at the cheekbone than the crown and closing
# to a narrow chin -- a thin face, which the hair is going to make thinner.
OPP_PROFILE = ((10, 6.0), (12, 12.0), (15, 16.5), (19, 19.8), (24, 22.0), (30, 23.4),
               (36, 24.0), (42, 24.0), (48, 23.4), (54, 22.0), (60, 19.8), (65, 17.0),
               (69, 13.8), (73, 9.6), (76, 4.5))

# Half-width of the hair mass behind the head, row by row. It hangs past the jaw and onto
# the shoulders, so it keeps widening long after the skull has stopped.
OPP_HAIR_PROFILE = ((3, 8.0), (5, 16.0), (8, 21.0), (12, 25.0), (18, 28.0), (26, 30.0),
                    (40, 30.8), (56, 31.6), (72, 33.0), (88, 34.6), (100, 35.4))

#: Where the hair ends, before the strands take over. Individual strands run past it.
OPP_HAIR_END = 96

#: Half-width of the hoodie, row by row, from the neckline to the bottom of the frame.
OPP_BODY_PROFILE = ((84, 13.0), (88, 22.0), (92, 35.0), (97, 46.0), (104, 55.0),
                    (114, 62.0), (128, 67.0), (149, 70.0))


def _profile(table, y):
    if y < table[0][0] or y > table[-1][0]:
        return 0.0
    for i in range(len(table) - 1):
        y0, w0 = table[i]
        y1, w1 = table[i + 1]
        if y0 <= y <= y1:
            return w0 + (w1 - w0) * ((y - y0) / float(y1 - y0))
    return 0.0


def opp_head_half_width(y):
    return _profile(OPP_PROFILE, y)


def opp_hair_half_width(y):
    return _profile(OPP_HAIR_PROFILE, y)


def opp_body_half_width(y):
    return _profile(OPP_BODY_PROFILE, y)


def opp_skin(v):
    v = max(0.0, min(1.6, v))
    if v < 0.34:
        return lerp_color(OPP_VOID, OPP_SKIN_D, v / 0.34)
    if v < 0.66:
        return lerp_color(OPP_SKIN_D, OPP_SKIN_M, (v - 0.34) / 0.32)
    if v < 1.02:
        return lerp_color(OPP_SKIN_M, OPP_SKIN_L, (v - 0.66) / 0.36)
    return lerp_color(OPP_SKIN_L, OPP_SKIN_H, min(1.0, (v - 1.02) / 0.40))


def opp_hair(v):
    v = max(0.0, min(1.0, v))
    if v < 0.5:
        return lerp_color(OPP_HAIR_D, OPP_HAIR_M, v / 0.5)
    return lerp_color(OPP_HAIR_M, OPP_HAIR_L, (v - 0.5) / 0.5)


def opp_hood(v):
    v = max(0.0, min(1.0, v))
    if v < 0.5:
        return lerp_color(OPP_HOOD_D, OPP_HOOD_M, v / 0.5)
    return lerp_color(OPP_HOOD_M, OPP_HOOD_L, (v - 0.5) / 0.5)


# Per pose: how far the head drops and leans, how far the far shoulder is hauled up, how
# far the lids are down, the curve of a closed mouth, how far the jaw is open, how the
# brows sit, and where the cigarette is.
#
# lean is signed toward the box. The far shoulder is the one the box covers, so a
# shoulder lift only ever shows above the box's top edge -- which is the point of it.
OPP_POSE = {
    "hostile": dict(drop=0, lean=0, shoulder=0, lids=0.32, mouth=2.6, gape=0.0, brow=3.8, cig="hand"),
    "even":    dict(drop=0, lean=0, shoulder=0, lids=0.10, mouth=0.5, gape=0.0, brow=0.4, cig="hand"),
    "open":    dict(drop=0, lean=0, shoulder=0, lids=0.00, mouth=-1.8, gape=0.0, brow=-2.6, cig="hand"),
    "talk":    dict(drop=0, lean=0, shoulder=0, lids=0.16, mouth=0.4, gape=2.6, brow=-1.0, cig="hand"),
    # Reaching: leant toward the box, far shoulder up because the arm under it is in the
    # box. The hand that held the cigarette is the one in the box, so it is in the mouth.
    "reach":   dict(drop=6, lean=6, shoulder=14, lids=0.24, mouth=0.6, gape=0.0, brow=1.4, cig="mouth"),
    # Hurt: head down and away, eyes shut, mouth open. The cigarette is on the floor.
    "hurt":    dict(drop=5, lean=-3, shoulder=2, lids=1.00, mouth=0.0, gape=4.8, brow=2.6, cig=None),
}


def opp_draw_torso(img, ox, oy, shoulder, rng):
    """The hoodie, from the neckline to the bottom of the frame, lit from the lamp above."""
    for yy in range(OPP_BODY_PROFILE[0][0], OPP_OH):
        for x in range(OPP_OW):
            dx = x - OPP_CX

            # Far shoulder lift, off the widest part of the coat so it is one smooth curve.
            # Each row of the frame asks which row of the coat has been hauled up into it,
            # and past the table the coat is as wide as its last row -- so the corner
            # under a lifted shoulder is filled rather than torn open.
            lift = shoulder * min(1.0, max(0.0, dx / 70.0)) ** 1.3
            y = min(OPP_OH - 1, yy + lift)
            hw = opp_body_half_width(y)
            if hw <= 0 or abs(dx) > hw:
                continue

            edge = abs(dx) / hw
            depth = (y - 84) / float(OPP_OH - 84)

            # The slope of each shoulder faces the lamp; the chest faces the player and
            # falls away into the dark.
            top = max(0.0, 1.0 - depth * 2.2) ** 1.4
            v = 0.16 + 0.70 * top * (0.55 + 0.45 * edge ** 0.8)
            v *= 1.0 - 0.38 * edge ** 2.4

            # Folds hanging off the shoulders, as soft bands that run down the chest.
            fold = 0.5 + 0.5 * math.sin(dx * 0.42 + math.sin(dx * 0.09) * 3.0)
            v *= 0.90 + 0.14 * fold * max(0.0, depth)

            c = opp_hood(v)

            # Red off the box, low and on the part nearest it.
            front = max(0.0, depth) ** 2.2 * (1.0 - edge ** 1.6)
            if front > 0:
                c = lerp_color(c, OPP_BOX_RED, front * 0.16)
            if edge > 0.93:
                c = lerp_color(c, OPP_COLD, (edge - 0.93) / 0.07 * 0.55)

            put(img, ox + x, oy + yy, c)

    # Raglan seams, curving from the neckline out over each shoulder. A dark thread with
    # the fold it pulls up catching the lamp just under it.
    for side in (-1, 1):
        for i in range(0, 56):
            t = i / 56.0
            x = OPP_CX + side * (15.0 + 50.0 * t)
            lift = shoulder * min(1.0, max(0.0, (x - OPP_CX) / 70.0)) ** 1.3 if side > 0 else 0.0
            y = 89.0 + 18.0 * t ** 1.5 - lift
            # Only where there is coat under it. Past the shoulder the thread would be
            # stitched onto the wall.
            if abs(x - OPP_CX) > opp_body_half_width(y + lift) - 2.0:
                break  # past the shoulder
            put(img, ox + r(x), oy + r(y), (OPP_HOOD_D[0], OPP_HOOD_D[1], OPP_HOOD_D[2], 200))
            put(img, ox + r(x), oy + r(y) + 1, (OPP_HOOD_L[0], OPP_HOOD_L[1], OPP_HOOD_L[2], int(110 * (1.0 - t))))


def opp_draw_neckline(img, ox, oy, hx):
    """The lip of the hood's opening. Drawn after the neck, because it is in front of it."""
    for i in range(-16, 17):
        t = abs(i) / 16.0
        y = 89.0 + 7.0 * (1.0 - t * t)
        x = OPP_CX + hx * 0.4 + i
        put(img, ox + r(x), oy + r(y), shade(OPP_HOOD_L, 0.95 - 0.35 * t))
        put(img, ox + r(x), oy + r(y) + 1, shade(OPP_HOOD_M, 0.9))
        put(img, ox + r(x), oy + r(y) - 1, (OPP_HOOD_D[0], OPP_HOOD_D[1], OPP_HOOD_D[2], 140))

    # Drawstrings, hanging off the neckline and swinging a little apart.
    for side in (-1, 1):
        for i in range(0, 30):
            x = OPP_CX + side * (5.0 + i * 0.16) + (0.8 * math.sin(i * 0.5) if side > 0 else 0.0)
            y = 95 + i
            put(img, ox + r(x), oy + y, shade(OPP_CORD, 0.72 + 0.28 * (1.0 - i / 30.0)))
        # aglet
        x = OPP_CX + side * (5.0 + 29 * 0.16)
        for k in range(3):
            put(img, ox + r(x), oy + 125 + k, shade(OPP_CORD, 0.55))


def opp_draw_hood(img, ox, oy, hx, drop):
    """The hood, down, bunched behind the neck. Its inside catches the lamp."""
    cy = 86 + drop * 0.5
    for y in range(int(cy - 14), int(cy + 12)):
        for x in range(OPP_OW):
            dx = x - OPP_CX - hx * 0.5
            dy = y - cy
            d = math.hypot(dx / 34.0, dy / 13.0)
            if d > 1.0:
                continue
            # Ridges: the fabric folds over on itself in a few thick rolls.
            roll = 0.5 + 0.5 * math.sin(dx * 0.31 + dy * 0.6)
            v = (0.45 + 0.40 * (1.0 - d)) * (0.75 + 0.25 * roll)
            if dy < -6:
                v *= 1.15
            c = lerp_color(opp_hood(v), OPP_HOOD_IN, 0.35 * (1.0 - d))
            if d > 0.9:
                c = lerp_color(c, OPP_HOOD_D, (d - 0.9) / 0.1)
            put(img, ox + x, oy + y, c)


def opp_draw_hair_back(img, ox, oy, hx, drop, rng):
    """
    The hair as a mass behind the head, hanging to the shoulders.

    Strands are columns: the brightness varies across x and barely along y, because that
    is what hair hanging straight down does under a lamp. The ends are ragged per column
    and seeded the same for every frame, so the same hair is on the same head in every
    cell of the sheet.
    """
    ends = [OPP_HAIR_END + rng.randint(-6, 9) for _ in range(OPP_OW)]
    strand = [rng.uniform(0.62, 1.30) for _ in range(OPP_OW)]
    # Neighbouring columns share a little, so strands are two or three pixels wide.
    strand = [(strand[max(0, i - 1)] + 2 * strand[i] + strand[min(OPP_OW - 1, i + 1)]) / 4.0
              for i in range(OPP_OW)]

    for y in range(OPP_HAIR_PROFILE[0][0], OPP_OH):
        for x in range(OPP_OW):
            dx = x - OPP_CX - hx
            hw = opp_hair_half_width(y)
            if hw <= 0 or abs(dx) > hw:
                continue
            # Ragged ends. The columns nearest the face hang longest.
            end = ends[x] + 6.0 * (1.0 - abs(dx) / hw)
            if y > end:
                continue
            edge = abs(dx) / hw
            top = max(0.0, 1.0 - (y - 3) / 60.0) ** 1.4
            v = (0.14 + 0.52 * top) * strand[x] * (1.0 - 0.34 * edge ** 3)
            # The crown catches the lamp along a band, the way hair does.
            v += 0.30 * math.exp(-((y - 9.0) / 5.5) ** 2) * (1.0 - edge ** 2)
            # Falls into shadow where it lies against the face and neck.
            if y > 40 and abs(dx) < 22:
                v *= 0.72
            c = opp_hair(v)
            if edge > 0.94:
                c = lerp_color(c, OPP_COLD, (edge - 0.94) / 0.06 * 0.45)
            put(img, ox + x, oy + y + drop, c)


def opp_draw_hair_front(img, ox, oy, hx, drop, rng):
    """The fringe over the brow, and the two curtains that hang in front of the temples."""
    # Fringe: parted a little off centre, sweeping away from the parting on both sides.
    part = -5.0
    for x in range(OPP_OW):
        dx = x - OPP_CX - hx
        if abs(dx) > 23.5:
            continue
        away = dx - part
        # Short over the parting, long over the temples, with a ragged edge.
        end = 15.0 + 9.5 * min(1.0, abs(away) / 16.0) ** 1.3 + rng.uniform(-1.5, 2.5)
        if abs(away) < 3.0:
            end -= 2.0 * (1.0 - abs(away) / 3.0)
        v_col = rng.uniform(0.55, 1.15)
        for y in range(OPP_HEAD_TOP - 2, int(end) + 1):
            t = (y - OPP_HEAD_TOP) / max(1.0, end - OPP_HEAD_TOP)
            v = (0.20 + 0.42 * (1.0 - t)) * v_col
            v += 0.25 * math.exp(-((y - 11.0) / 4.0) ** 2)
            put(img, ox + x, oy + y + drop, opp_hair(v))
        # The tip of each strand, one pixel darker where it lies on skin.
        put(img, ox + x, oy + int(end) + 1 + drop, (OPP_HAIR_D[0], OPP_HAIR_D[1], OPP_HAIR_D[2], 150))

    # Curtains: hair in front of the outer edge of the face, covering the ears.
    for side in (-1, 1):
        for x in range(OPP_OW):
            dx = (x - OPP_CX - hx) * side
            if dx < 17.0 or dx > 32.0:
                continue
            inner = (dx - 17.0) / 15.0
            end = OPP_HAIR_END + rng.randint(-5, 8)
            v_col = rng.uniform(0.60, 1.20)
            for y in range(20, end):
                hw_face = opp_head_half_width(y)
                hw_hair = opp_hair_half_width(y)
                if dx > hw_hair:
                    continue
                # Only in front of the face where the face actually is; below the jaw it is
                # already drawn as the back mass, so this just keeps the strand going.
                if dx > hw_face + 6.0 and y < 70:
                    continue
                top = max(0.0, 1.0 - (y - 18) / 70.0)
                v = (0.16 + 0.40 * top) * v_col * (0.70 + 0.30 * inner)
                put(img, ox + x, oy + y + drop, opp_hair(v))


def opp_draw_neck(img, ox, oy, hx, drop):
    for y in range(OPP_NECK_TOP + drop, OPP_NECK_BOTTOM + 3 + drop):
        t = (y - drop - OPP_NECK_TOP) / float(OPP_NECK_BOTTOM - OPP_NECK_TOP)
        hw = 9.5 + 2.5 * t
        for x in range(OPP_OW):
            dx = x - OPP_CX - hx
            if abs(dx) > hw:
                continue
            # In the shadow of the jaw at the top, and the sternocleidomastoid catching
            # a little light down each side.
            v = 0.14 + 0.30 * (abs(dx) / hw) ** 1.4 + 0.18 * t
            v *= 0.55 + 0.45 * min(1.0, t * 2.5)
            put(img, ox + x, oy + y, opp_skin(v))


def opp_draw_eye(img, ox, oy, ex, ey, lids, rng):
    """The eye, painted straight into the head. Static, so there is no blink sheet."""
    # Shadow under the eye, whatever the lids are doing. This is a face that has not slept.
    for y in range(int(ey + 2), int(ey + 9)):
        for x in range(int(ex - 8), int(ex + 9)):
            d = math.hypot((x - ex) / 8.0, (y - (ey + 4.5)) / 4.0)
            if d <= 1.0:
                put(img, ox + x, oy + y, (38, 22, 34, int(74 * (1.0 - d))))

    if lids >= 0.99:
        for x in range(int(ex - 7), int(ex + 8)):
            t = abs(x - ex) / 7.0
            a = int(235 * (1.0 - t * t))
            put(img, ox + x, oy + int(ey), (26, 12, 16, a))
            put(img, ox + x, oy + int(ey) + 1, (150, 82, 64, int(a * 0.42)))
        return

    ry = OPP_EYE_RY * (1.0 - lids)
    for y in range(int(ey - 7), int(ey + 8)):
        for x in range(int(ex - 8), int(ex + 9)):
            dx, dy = (x - ex) / OPP_EYE_RX, (y - ey) / max(0.6, ry)
            if dx * dx + dy * dy > 1.0:
                continue
            c = lerp_color(OPP_SCLERA, shade(OPP_SCLERA, 0.5), min(1.0, abs(dx) ** 1.2))
            if y < ey - ry * 0.10:
                c = shade(c, 0.56)
            d = math.hypot(x - ex, (y - ey) * 1.10)
            if d <= 3.6:
                c = lerp_color(OPP_IRIS_E, OPP_IRIS, min(1.0, (3.6 - d) / 1.8))
                if y < ey - ry * 0.10:
                    c = shade(c, 0.72)
            if d <= 1.5:
                c = OPP_PUPIL
            put(img, ox + x, oy + y, c)

    # The upper lid: a dark line with the lashes thickening toward the outer corner.
    for x in range(int(ex - 8), int(ex + 9)):
        t = (x - ex) / 8.0
        if abs(t) > 1.0:
            continue
        ly = ey - ry * math.sqrt(max(0.0, 1.0 - t * t)) - 0.6
        put(img, ox + x, oy + r(ly), (20, 10, 14, 220))
        if abs(t) > 0.5:
            put(img, ox + x, oy + r(ly) - 1, (20, 10, 14, 120))

    if lids < 0.6:
        put(img, ox + int(ex) - 1, oy + int(ey + 1.5), OPP_CATCH)
        put(img, ox + int(ex), oy + int(ey + 1.5), lerp_color(OPP_CATCH, OPP_IRIS, 0.5))


def opp_draw_head(img, ox, oy, p, hx, drop, injury, rng):
    tilt, lids, curve = p["brow"], p["lids"], p["mouth"]

    for y in range(OPP_HEAD_TOP, OPP_HEAD_BOTTOM + 1):
        hw = opp_head_half_width(y)
        if hw <= 0:
            continue
        for x in range(OPP_OW):
            dx = x - OPP_CX - hx
            if abs(dx) > hw:
                continue
            edge = abs(dx) / hw

            top = max(0.0, 1.0 - (y - OPP_HEAD_TOP) / 58.0) ** 1.5
            up = max(0.0, (y - 34) / 42.0) ** 1.7

            v = (0.09 + 0.92 * top + 0.30 * up) * (1.0 - 0.42 * edge ** 2.2)
            # Brow ridge shadow over the sockets.
            v *= 1.0 - 0.40 * math.exp(-((y - 38.0) / 5.5) ** 2)
            # The fringe throws a shadow across the forehead.
            v *= 1.0 - 0.34 * math.exp(-((y - 24.0) / 5.0) ** 2)

            cheek = math.hypot((x - (OPP_CX + hx + math.copysign(13.0, dx))) / 7.5, (y - 52) / 9.0)
            if cheek < 1.0:
                v *= 0.54 + 0.46 * cheek

            if OPP_NOSE_TOP <= y <= OPP_NOSE_BOT and abs(dx) < 5.6:
                v *= 1.0 + 0.44 * (1.0 - abs(dx) / 5.6)
                if 1.4 < dx < 5.2:
                    v *= 0.62

            # Under the lower lip and along the jaw.
            if y > OPP_MOUTH_Y + 2 and abs(dx) < 8:
                v *= 0.80

            c = opp_skin(v * (1.0 - 0.13 * injury))
            if up > 0.1:
                c = lerp_color(c, OPP_BOX_RED, min(0.42, up * 0.40 * (1.0 - edge ** 2)))
            if edge > 0.90:
                c = lerp_color(c, OPP_COLD, (edge - 0.90) / 0.10 * 0.55)

            put(img, ox + x, oy + y + drop, c)

    # brows
    for side in (-1, 1):
        ex = OPP_CX + hx + side * OPP_EYE_DX
        for i in range(-8, 9):
            bx = ex + i
            if abs(bx - OPP_CX - hx) < 3.6:
                continue
            inner = (i * side) < 0
            reach = abs(i) / 8.0
            by = OPP_EYE_Y - 8.4 + (tilt * reach if inner else -0.8 * reach)
            for t in range(3):
                put(img, ox + r(bx), oy + r(by - t) + drop, shade(OPP_SKIN_D, 0.62 - 0.10 * t))

    # Sockets: the skin around each eye sinks into shadow. Darkened in place rather than
    # painted, so the bridge of the nose between them keeps its light.
    for side in (-1, 1):
        ex = OPP_CX + hx + side * OPP_EYE_DX
        for y in range(int(OPP_EYE_Y) - 8, int(OPP_EYE_Y) + 9):
            for x in range(int(ex) - 10, int(ex) + 11):
                if abs(x - OPP_CX - hx) < 4.5:
                    continue
                d = math.hypot((x - ex) / (OPP_EYE_RX + 1.6), (y - OPP_EYE_Y) / (OPP_EYE_RY + 2.2))
                if d > 1.0:
                    continue
                px = img[oy + y + drop][ox + x]
                if px[3] == 0:
                    continue
                img[oy + y + drop][ox + x] = list(shade(tuple(px), 0.52 + 0.48 * d ** 0.8))
        opp_draw_eye(img, ox, oy + drop, ex, OPP_EYE_Y, lids, rng)

    # nostrils
    for side in (-1, 1):
        put(img, ox + r(OPP_CX + hx + side * 3.0), oy + OPP_NOSE_BOT + drop, OPP_MOUTH)
        put(img, ox + r(OPP_CX + hx + side * 3.8), oy + OPP_NOSE_BOT + drop, shade(OPP_SKIN_D, 0.55))
    put(img, ox + r(OPP_CX + hx), oy + OPP_NOSE_BOT - 1 + drop, shade(OPP_SKIN_H, 0.9))

    # mouth
    gape = p["gape"]
    if gape > 0.0:
        half = 6.4 + 0.30 * gape
        for x in range(OPP_OW):
            dx = x - OPP_CX - hx
            if abs(dx) > half:
                continue
            h = gape * (1.0 - (abs(dx) / half) ** 2)
            for y in range(int(OPP_MOUTH_Y - h), int(OPP_MOUTH_Y + h) + 1):
                put(img, ox + x, oy + y + drop, OPP_MOUTH)
            put(img, ox + x, oy + int(OPP_MOUTH_Y + h) + 1 + drop, OPP_LIP)
    else:
        for x in range(OPP_OW):
            dx = x - OPP_CX - hx
            if abs(dx) > 9.0:
                continue
            cy = OPP_MOUTH_Y + curve * ((abs(dx) / 9.0) ** 1.6)
            put(img, ox + x, oy + r(cy) + drop, OPP_MOUTH)
            put(img, ox + x, oy + r(cy + 1) + drop, OPP_LIP)
            put(img, ox + x, oy + r(cy - 1) + drop, shade(OPP_SKIN_M, 0.80))


def _limb(img, ox, oy, x0, y0, x1, y1, r0, r1, colour_fn):
    """Lays a tapering limb down, shaded by how far in from its edge a pixel is."""
    steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 3) + 6
    cells = {}
    for i in range(steps + 1):
        t = i / steps
        px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        r = r0 + (r1 - r0) * t
        for yy in range(int(py - r) - 1, int(py + r) + 2):
            for xx in range(int(px - r) - 1, int(px + r) + 2):
                d = math.hypot(xx - px, yy - py)
                if d <= r:
                    depth = r - d
                    if cells.get((xx, yy), -1.0) < depth:
                        cells[(xx, yy)] = depth
    for (xx, yy), depth in cells.items():
        put(img, ox + xx, oy + yy, colour_fn(xx, yy, depth))


def opp_draw_arm(img, ox, oy, rng):
    """
    The near arm, forearm up off the table and the hand held beside the jaw. It is what
    the coat and the hair are not: a shape with light on it, near the camera.
    """
    def sleeve(xx, yy, depth):
        top = max(0.0, 1.0 - (yy - 70) / 80.0)
        v = 0.22 + 0.55 * top * min(1.0, depth / 4.0) ** 0.7
        c = opp_hood(v)
        if depth < 1.3:
            c = lerp_color(c, OPP_COLD, 0.4)
        return c

    def skin(xx, yy, depth):
        top = max(0.0, 1.0 - (yy - 50) / 40.0)
        v = 0.40 + 0.70 * top * min(1.0, depth / 3.0) ** 0.8
        c = opp_skin(v)
        if depth < 1.2:
            c = lerp_color(c, OPP_COLD, 0.35)
        return c

    # Forearm from the table up to the wrist, and the cuff.
    _limb(img, ox, oy, 24.0, 152.0, 42.0, 86.0, 10.0, 8.0, sleeve)
    for i in range(-8, 9):
        put(img, ox + r(42.0 + i * 0.95), oy + r(86 + abs(i) * 0.25), shade(OPP_HOOD_L, 0.85))

    # The hand: back of the hand, then the curled fingers, then two extended.
    _limb(img, ox, oy, 43.0, 84.0, 47.0, 70.0, 7.4, 7.0, skin)
    _limb(img, ox, oy, 47.0, 71.0, 53.0, 66.0, 5.6, 4.2, skin)     # curled fingers, as a knuckle mass
    _limb(img, ox, oy, 46.0, 67.0, 41.5, 52.0, 2.6, 2.0, skin)     # index
    _limb(img, ox, oy, 49.5, 66.0, 45.5, 51.5, 2.6, 2.0, skin)     # middle

    # Creases where the extended fingers meet the hand.
    for k in range(4):
        put(img, ox + 44 + k, oy + 66, (OPP_SKIN_D[0], OPP_SKIN_D[1], OPP_SKIN_D[2], 170))
        put(img, ox + 47 + k, oy + 64, (OPP_SKIN_D[0], OPP_SKIN_D[1], OPP_SKIN_D[2], 130))


def opp_draw_cigarette(img, ox, oy, x0, y0, x1, y1, lit=True):
    """A cigarette from (x0, y0) at the fingers to (x1, y1) at the ember."""
    steps = int(math.hypot(x1 - x0, y1 - y0) * 3) + 2
    for i in range(steps + 1):
        t = i / steps
        px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        if t < 0.22:
            c = OPP_CIG_TIP
        elif t < 0.88:
            c = OPP_CIG
        else:
            c = OPP_ASH
        for d in (-0.5, 0.5):
            put(img, ox + r(px + d * 0.7), oy + r(py - d * 0.7), c)
    if lit:
        put(img, ox + r(x1), oy + r(y1), OPP_EMBER)
        put(img, ox + r(x1) - 1, oy + r(y1), (OPP_EMBER[0], OPP_EMBER[1], OPP_EMBER[2], 120))
        put(img, ox + r(x1), oy + r(y1) - 1, (OPP_EMBER[0], OPP_EMBER[1], OPP_EMBER[2], 120))


def opp_draw_smoke(img, ox, oy, x, y, rng, length=None):
    """One thread of smoke, static like everything else on him, rising off the ember."""
    px = float(x)
    length = int(y) - 2 if length is None else min(length, int(y) - 2)
    for i in range(length):
        t = i / float(length)
        px += 0.55 * math.sin(i * 0.19 + 0.7) + 0.18 * math.sin(i * 0.61)
        a = int(96 * (1.0 - t) ** 1.6)
        yy = y - i - 1
        put(img, ox + r(px), oy + yy, (OPP_SMOKE[0], OPP_SMOKE[1], OPP_SMOKE[2], a))
        # the thread thickens and thins as it goes
        if (i // 3) % 2 == 0:
            put(img, ox + r(px) + 1, oy + yy, (OPP_SMOKE[0], OPP_SMOKE[1], OPP_SMOKE[2], int(a * 0.5)))


def opp_draw_blood(img, ox, oy, injury, hx):
    """
    What has already been taken off them.

    Asymmetric and starting at a wound, because symmetry reads as decoration.
    """
    if injury <= 0:
        return

    def run(x0, y0, length, drift):
        x = float(x0)
        for i in range(length):
            t = i / float(length)
            x += drift * (0.6 + 0.8 * t)
            c = lerp_color(OPP_BLOOD, OPP_BLOOD_D, t ** 0.6)
            a = int(230 * (1.0 - t * 0.55))
            put(img, ox + r(x), oy + y0 + i, (c[0], c[1], c[2], a))
            if i % 4 == 1:
                put(img, ox + r(x) + 1, oy + y0 + i, (c[0], c[1], c[2], int(a * 0.45)))

    hxi = r(hx)

    # One cut, over the near brow, opened the first time something landed.
    cut_x = int(OPP_CX + hxi - 15)
    for i in range(8):
        put(img, ox + cut_x + i, oy + 28, OPP_BLOOD)
        put(img, ox + cut_x + i, oy + 29, OPP_BLOOD_D)
    run(cut_x + 2, 30, 28, 0.18)
    run(cut_x + 6, 30, 18, 0.24)

    for y in range(24, 40):
        for x in range(cut_x - 5, cut_x + 13):
            d = math.hypot((x - (cut_x + 4)) / 9.0, (y - 31) / 8.0)
            if d <= 1.0:
                put(img, ox + x, oy + y, (44, 14, 20, int(70 * (1.0 - d))))

    if injury < 2:
        return

    # The second time, it took the mouth and the other eye.
    for x in range(int(OPP_CX + hxi) - 7, int(OPP_CX + hxi) + 6):
        put(img, ox + x, oy + OPP_MOUTH_Y - 1, (OPP_BLOOD_D[0], OPP_BLOOD_D[1], OPP_BLOOD_D[2], 185))
    run(int(OPP_CX + hxi) - 4, OPP_MOUTH_Y + 1, 11, -0.10)

    far = int(OPP_CX + hxi + OPP_EYE_DX)
    for y in range(int(OPP_EYE_Y) - 8, int(OPP_EYE_Y) + 9):
        for x in range(far - 10, far + 11):
            d = math.hypot((x - far) / 10.0, (y - OPP_EYE_Y) / 8.0)
            if d <= 1.0:
                put(img, ox + x, oy + y, (30, 12, 22, int(110 * (1.0 - d))))


def build_opponent_frame(img, ox, oy, pose_name, injury, seed=7171):
    p = OPP_POSE[pose_name]
    drop, hx, shoulder = p["drop"] + injury, p["lean"], p["shoulder"]

    # One seed for every cell: the strands of hair and the folds land in the same place in
    # every frame, so switching poses changes the face and nothing else.
    rng = random.Random(seed)

    opp_draw_torso(img, ox, oy, shoulder, rng)
    opp_draw_hood(img, ox, oy, hx, drop)
    opp_draw_hair_back(img, ox, oy, hx, drop, random.Random(seed + 1))
    opp_draw_neck(img, ox, oy, hx, drop)
    opp_draw_head(img, ox, oy, p, hx, drop, injury, rng)
    opp_draw_neckline(img, ox, oy, hx)
    opp_draw_hair_front(img, ox, oy, hx, drop, random.Random(seed + 2))

    if p["cig"] == "hand":
        opp_draw_arm(img, ox, oy, rng)
        opp_draw_cigarette(img, ox, oy, 42.5, 51.0, 33.0, 39.0)
        opp_draw_smoke(img, ox, oy, 32, 37, rng)
    elif p["cig"] == "mouth":
        mx, my = OPP_CX + hx - 7.0, OPP_MOUTH_Y + drop + 0.5
        opp_draw_cigarette(img, ox, oy, mx, my, mx - 10.0, my + 3.0)
        opp_draw_smoke(img, ox, oy, int(mx - 10.0), int(my + 2), rng, length=26)

    opp_draw_blood(img, ox, oy + drop, injury, hx)


def build_opponent_sheet():
    img = new_image(OPP_OW * len(OPP_POSES), OPP_OH * len(OPP_INJURIES))
    for r, injury in enumerate(OPP_INJURIES):
        for c, pose in enumerate(OPP_POSES):
            build_opponent_frame(img, c * OPP_OW, r * OPP_OH, pose, injury)
    write_png(os.path.join(OUT, "opponent-sheet.png"), img)



# --------------------------------------------------------------------------- #
# hand-sheet.png -- the player's own hand, 5 frames from curled to offered
#
# Seen from behind, reaching away from the camera toward the box, because it is the
# player's hand and that is where their hand is. Frame 0 is curled on the table and
# frame 4 is open and fanned, which is the pose the box is waiting for.
#
# Shading runs off a distance field over the whole silhouette rather than per limb --
# shading each finger as it was laid down gave every one its own rim light and the
# hand read as a row of pipes. Knuckles are creases and nails are barely lighter than
# the finger, for the same reason: lit, they become studs and beads.
# --------------------------------------------------------------------------- #

HAND_HW, HAND_HH = 40, 48
HAND_REACH = 5

HAND_VOID   = (12,  9, 14, 255)
HAND_SKIN_D = (40, 22, 24, 255)
HAND_SKIN_M = (92, 48, 44, 255)
HAND_SKIN_L = (158, 80, 60, 255)
HAND_SKIN_H = (208, 116, 84, 255)
HAND_COLD   = (58, 56, 76, 255)

HAND_CX = 19.5
HAND_WRIST_Y, HAND_KNUCKLE_Y = 38.0, 22.0

# knuckle offset, length, how far the tip fans out when the hand opens
HAND_FINGERS = ((-7.1, 14.4, -4.2), (-2.4, 17.2, -1.4), (2.4, 16.2, 1.4), (7.1, 12.8, 4.2))

HAND_NAIL = (188, 150, 132, 255)


def hand_tone(v):
    if v < 0.36:
        return lerp_color(HAND_VOID, HAND_SKIN_D, v / 0.36)
    if v < 0.70:
        return lerp_color(HAND_SKIN_D, HAND_SKIN_M, (v - 0.36) / 0.34)
    if v < 1.02:
        return lerp_color(HAND_SKIN_M, HAND_SKIN_L, (v - 0.70) / 0.32)
    return lerp_color(HAND_SKIN_L, HAND_SKIN_H, min(1.0, (v - 1.02) / 0.40))


def hand_stamp(mask, owner, x0, y0, x1, y1, r0, r1, tag):
    """Lays a tapering limb into the mask, tagged so seams can be found later."""
    steps = int(max(abs(x1 - x0), abs(y1 - y0)) * 3) + 6
    for i in range(steps + 1):
        t = i / steps
        px, py = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
        r = r0 + (r1 - r0) * t
        for yy in range(int(py - r) - 1, int(py + r) + 2):
            if yy < 0 or yy >= HAND_HH:
                continue
            for xx in range(int(px - r) - 1, int(px + r) + 2):
                if xx < 0 or xx >= HAND_HW:
                    continue
                if math.hypot(xx - px, yy - py) <= r:
                    mask[yy][xx] = 1
                    owner[yy][xx] = tag


def build_hand_frame(img, ox, openness):
    curl = 1.0 - openness
    mask = [[0] * HAND_HW for _ in range(HAND_HH)]
    owner = [[0] * HAND_HW for _ in range(HAND_HH)]

    # Forearm, wrist and the back of the hand are one body; the fingers and thumb
    # each get their own tag so the creases between them can be found.
    hand_stamp(mask, owner, HAND_CX, HAND_HH + 4, HAND_CX, HAND_WRIST_Y, 6.2, 5.2, 1)
    hand_stamp(mask, owner, HAND_CX, HAND_WRIST_Y, HAND_CX, HAND_KNUCKLE_Y + 1.5, 5.2, 8.8, 1)

    ta = math.radians(198 + 24 * openness)
    tl = 11.5 + 2.5 * openness
    hand_stamp(mask, owner, HAND_CX - 5.4, HAND_WRIST_Y - 4.0,
          HAND_CX - 5.4 + math.cos(ta) * -tl, HAND_WRIST_Y - 4.0 + math.sin(ta) * tl, 3.2, 2.2, 2)

    tips = []
    for n, (kx, length, swing) in enumerate(HAND_FINGERS):
        bx, by = HAND_CX + kx, HAND_KNUCKLE_Y
        reach = length * (1.0 - 0.60 * curl)
        tx, ty = bx + swing * openness, by - reach
        mx = bx + swing * openness * 0.40
        my = by - reach * 0.55 + curl * 2.2
        # Three segments rather than two: the taper from knuckle to nail is most of what
        # makes a finger read as a finger instead of a dowel.
        hand_stamp(mask, owner, bx, by, mx, my, 2.4, 2.1, 3 + n)
        hand_stamp(mask, owner, mx, my, tx, ty, 2.1, 1.6, 3 + n)
        tips.append((tx, ty, 3 + n))

    # Distance from each filled pixel to the nearest empty one. Shading the whole
    # silhouette off this, rather than shading each limb as it is laid down, is what
    # stops every finger arriving with its own rim light and reading as a row of pipes.
    CAP = 3.5
    depth = [[0.0] * HAND_HW for _ in range(HAND_HH)]
    for y in range(HAND_HH):
        for x in range(HAND_HW):
            if not mask[y][x]:
                continue
            best = CAP
            r = int(CAP) + 1
            for yy in range(y - r, y + r + 1):
                for xx in range(x - r, x + r + 1):
                    inside = 0 <= yy < HAND_HH and 0 <= xx < HAND_HW and mask[yy][xx]
                    if not inside:
                        best = min(best, math.hypot(xx - x, yy - y))
            depth[y][x] = min(best, CAP)

    for y in range(HAND_HH):
        for x in range(HAND_HW):
            if not mask[y][x]:
                continue

            # The box is ahead of the hand, so the far end of every finger is the lit end.
            t = max(0.0, 1.0 - (y / float(HAND_HH)))
            v = 0.19 + 1.10 * t ** 1.12

            # Round the limb off: the middle of a finger faces the light, its sides fall away.
            v *= 0.52 + 0.48 * (depth[y][x] / CAP)

            # Creases. A pixel whose neighbour belongs to a different limb is where two
            # fingers touch, and a hand without those is a mitten.
            seam = False
            for yy, xx in ((y, x - 1), (y, x + 1), (y - 1, x), (y + 1, x)):
                if 0 <= yy < HAND_HH and 0 <= xx < HAND_HW and mask[yy][xx] and owner[yy][xx] != owner[y][x]:
                    seam = True
            if seam:
                v *= 0.62

            c = hand_tone(max(0.0, min(1.55, v)))

            # A cold edge all the way round, so the hand separates from the dark.
            if depth[y][x] < 1.25:
                c = lerp_color(c, HAND_COLD, (1.25 - depth[y][x]) / 1.25 * 0.40)

            put(img, ox + x, y, c)

    # Knuckles: a shallow crease across the back of the hand where the fingers hinge.
    # Drawn as shadow rather than highlight -- the first draft lit them and they read as
    # four bright studs sitting on top of the hand.
    for kx, _, swing in HAND_FINGERS:
        kxx = int(round(HAND_CX + kx + swing * openness * 0.25))
        for dx in (-1, 0, 1):
            for dy in (0, 1):
                x, y = kxx + dx, int(HAND_KNUCKLE_Y) + dy
                if 0 <= y < HAND_HH and 0 <= x < HAND_HW and mask[y][x]:
                    px = img[y][ox + x]
                    img[y][ox + x] = list(shade(tuple(px), 0.74 if dx == 0 else 0.86))

    # Nails, on the far end of each finger and only once the hand has opened enough to
    # show them. Barely lighter than the finger: a nail catching the light is a small
    # thing and painting it bright turns every fingertip into a bead.
    if openness > 0.25:
        for tx, ty, _ in tips:
            for dx in (-1, 0, 1):
                for dy in (0, 1):
                    x, y = int(round(tx)) + dx, int(round(ty)) + dy
                    if 0 <= y < HAND_HH and 0 <= x < HAND_HW and mask[y][x]:
                        t = 0.55 if dx == 0 and dy == 0 else 0.25
                        img[y][ox + x] = list(lerp_color(tuple(img[y][ox + x]), HAND_NAIL, t * openness))


def build_hand_sheet():
    img = new_image(HAND_HW * HAND_REACH, HAND_HH)
    for f in range(HAND_REACH):
        build_hand_frame(img, f * HAND_HW, f / (HAND_REACH - 1))
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
