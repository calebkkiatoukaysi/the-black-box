"""
Procedural art generator for The Black Box.

Every PNG in Content/ comes out of this script. Run it from the repo root:

    python tools/generate_assets.py

It only needs CPython: the PNG encoder and decoder are in here. Everything is drawn
from scratch except the two opponents, which are cut from the two pictures in
tools/source/ (opponent-portrait.png and opponent-second-portrait.png).
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
    """Rounds half up. Python's round() is half-to-even and it turned a row of .5 coords into a comb."""
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
    """A concrete shell around an opening. No lid or latch on purpose: it is a hole you put your hand in."""
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

    # 4. Soft vertical smears of dark, as overlapping bumps so they read as smudges, not scratches.
    smear = {}
    for _ in range(5):
        centre = rng.randint(AX0 + 4, AX1 - 4)
        width = rng.randint(2, 5)
        amplitude = rng.uniform(0.10, 0.26)
        for dx in range(-width, width + 1):
            falloff = amplitude * (1.0 - abs(dx) / float(width + 1))
            smear[centre + dx] = smear.get(centre + dx, 0.0) + falloff

    # 5. The void. Falloff follows a superellipse, not distance to the nearest edge: the
    #    nearest-edge version creased along the diagonals and looked like a picture frame.
    #    Light also falls off toward the upper left, where the key light is.
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
# Two stacked bands of the same 24px grid: the top band is the opaque concrete plate
# (neutral grey, tinted per button in game), the bottom band is the light out of the
# groove, drawn white so it takes any accent colour.
#   Row 0 idle (4 frames), row 1 hover (6, played backwards on the way out),
#   row 2 press (4, bevel inverted), row 3 disabled (1).
# Every frame has to survive a nine-slice: bolts and groove corners in the corners, the
# edges only carry what runs along them, and the middle is flat plate.

BUTTON_FRAME = 24
BUTTON_CORNER = 8
BUTTON_COLS = 6
BUTTON_ROWS = 4

#: Groove inset from the border. Has to stay < BUTTON_CORNER or the light breaks at the seams.
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
            # Disabled loses its blue cast too, so it reads as switched off, not just dark.
            grey = (r + g + b) / 3.0
            r, g, b = (c * 0.35 + grey * 0.65 for c in (r, g, b))
        return (max(0, min(255, int(r))), max(0, min(255, int(g))), max(0, min(255, int(b))), 255)

    # 1. Face, grained like the box. The grain has to be constant along whichever axis a
    #    region gets stretched, or the nine-slice smears one row of speckle across the
    #    whole button: corners get real grain, edges vary along one axis, middle is flat.
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

    # 3. The groove, cut all the way round at a fixed inset so the nine-slice can stretch it.
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
            # Distance in from the nearest border; the groove is a constant inset so this slices cleanly.
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
# One frame, no animation: a form does not react to the cursor, the controls on it do.
# Same concrete as the box and buttons, a shade darker and hollowed out. Same nine-slice
# rule as the button: variation only in the corners or along the edge it lives on.

PANEL_FRAME = 32

#: Fixed corner of the nine-slice. Must stay larger than PANEL_WELL or the well's lip shears.
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

    # 1. Face. Same stretch-safe grain rule as the button.
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

    # 3. The well: the slab hollowed out, so the lighting inverts (far lip lit, near lip in shadow).
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
# room.png -- the room the game is played in, drawn at half the window and blown up 2x
#
# One lamp over one table, falling off by inverse square, so the corners go dark. It is
# supposed to look like a photo of a room, not a drawing, so it is drawn at twice the
# resolution of the other sprites and nothing in it is a flat colour: the wall is three
# sizes of value noise (pour, aggregate, dust) with damp, bloom, seams and cracks, the
# door rusts up from the bottom, and the table has grain and scuffs.
# --------------------------------------------------------------------------- #

ROOM_R = 2
ROOM_RW, ROOM_RH = 400 * ROOM_R, 225 * ROOM_R
# Where the far edge of the table meets the wall. This is also the line the opponent is
# cut off by, so OpponentFoot.Y in BlackBoxGame has to agree with it.
ROOM_HORIZON = 160 * ROOM_R
ROOM_LIP = 8 * ROOM_R               # thickness of the table's far edge

ROOM_WALL_TOP  = (17, 15, 21, 255)
ROOM_WALL_MID  = (33, 29, 36, 255)
ROOM_WALL_LOW  = (46, 40, 46, 255)
ROOM_SEAM      = (11, 10, 14, 255)
ROOM_GRIME     = (24, 20, 25, 255)
ROOM_BLOOM     = (66, 62, 60, 255)
ROOM_RUST      = (96, 52, 30, 255)
ROOM_RUST_D    = (52, 26, 16, 255)

ROOM_TABLE_LIP  = (86, 74, 70, 255)
ROOM_TABLE_FAR  = (62, 53, 52, 255)
ROOM_TABLE_NEAR = (21, 18, 21, 255)

ROOM_LAMP = (196, 168, 130, 255)

ROOM_LAMP_X, ROOM_LAMP_Y = ROOM_RW / 2.0, 20.0 * ROOM_R


class _Noise:
    """Value noise: a grid of random numbers, smoothstepped between. Three of these added is the concrete."""

    def __init__(self, rng, w, h, cell):
        self.cell = float(cell)
        self.cols = int(w / cell) + 3
        self.rows = int(h / cell) + 3
        self.grid = [[rng.uniform(-1.0, 1.0) for _ in range(self.cols)] for _ in range(self.rows)]

    def at(self, x, y):
        fx, fy = x / self.cell, y / self.cell
        ix, iy = int(fx), int(fy)
        tx, ty = fx - ix, fy - iy
        tx = tx * tx * (3.0 - 2.0 * tx)
        ty = ty * ty * (3.0 - 2.0 * ty)
        ix = min(self.cols - 2, ix)
        iy = min(self.rows - 2, iy)
        g = self.grid
        top = g[iy][ix] + (g[iy][ix + 1] - g[iy][ix]) * tx
        bottom = g[iy + 1][ix] + (g[iy + 1][ix + 1] - g[iy + 1][ix]) * tx
        return top + (bottom - top) * ty


def build_room(seed=5150):
    R = ROOM_R
    rng = random.Random(seed)
    img = new_image(ROOM_RW, ROOM_RH)

    def lit_at(x, y):
        d = math.hypot((x - ROOM_LAMP_X) / 1.35, y - ROOM_LAMP_Y)
        return 1.0 / (1.0 + (d / (46.0 * R)) ** 2)

    # The three noises of the concrete, and a fourth, very large, for the damp.
    pour = _Noise(rng, ROOM_RW, ROOM_RH, 30 * R)
    grain = _Noise(rng, ROOM_RW, ROOM_RH, 7 * R)
    dust = _Noise(rng, ROOM_RW, ROOM_RH, 2 * R)
    damp = _Noise(rng, ROOM_RW, ROOM_RH, 60 * R)

    # 1. The wall, lit by the one lamp.
    for y in range(ROOM_HORIZON):
        for x in range(ROOM_RW):
            fall = lit_at(x, y)
            c = lerp_color(ROOM_WALL_TOP, ROOM_WALL_LOW, min(1.0, fall * 1.5))
            if y > ROOM_HORIZON - 34 * R:
                c = lerp_color(c, ROOM_WALL_MID, (y - (ROOM_HORIZON - 34 * R)) / (34.0 * R) * 0.35)
            # Pour, aggregate, dust; only as visible as the lamp makes it.
            k = 1.0 + (0.14 * pour.at(x, y) + 0.09 * grain.at(x, y) + 0.05 * dust.at(x, y)) * (0.4 + 0.6 * min(1.0, fall * 2.0))
            # Damp: darker and greener.
            wet = max(0.0, damp.at(x, y) - 0.25) * 1.4
            c = (int(c[0] * k * (1.0 - 0.22 * wet)), int(c[1] * k * (1.0 - 0.14 * wet)), int(c[2] * k * (1.0 - 0.20 * wet)), 255)
            # Bloom: the pale salts left where the damp dried, at its edges.
            bloom = max(0.0, 0.18 - abs(damp.at(x, y) - 0.25)) * 3.0
            if bloom > 0:
                c = lerp_color(c, ROOM_BLOOM, 0.3 * bloom * min(1.0, fall * 2.5))
            put(img, x, y, c)

    # 2. Panel seams at irregular spacing, with chipped edges.
    x = 14 * R
    while x < ROOM_RW:
        if abs(x - ROOM_RW / 2) < 26 * R:
            x += rng.randint(38 * R, 62 * R)
            continue
        for y in range(ROOM_HORIZON):
            fall = lit_at(x, y)
            chip = 1 if rng.random() < 0.06 else 0
            put(img, x + chip, y, (ROOM_SEAM[0], ROOM_SEAM[1], ROOM_SEAM[2], int(180 * fall)))
            put(img, x + chip + 1, y, (ROOM_SEAM[0], ROOM_SEAM[1], ROOM_SEAM[2], int(90 * fall)))
            # The lit side of the seam, where the lamp catches the lip of the panel.
            put(img, x - 1, y, (70, 64, 70, int(70 * fall)))
        x += rng.randint(38 * R, 62 * R)

    # 3. Damp streaks running down from the top, wandering a little and thinning out.
    for _ in range(18):
        sx = rng.randrange(ROOM_RW)
        if abs(sx - ROOM_RW / 2) < 30 * R:
            continue
        length = rng.randint(30 * R, 110 * R)
        width = rng.randint(1, 3) * R
        for y in range(0, min(ROOM_HORIZON, length)):
            t = y / float(length)
            wx = sx + int(2.0 * R * math.sin(y * 0.05 + sx))
            for i in range(width):
                put(img, wx + i, y, (10, 9, 12, int(70 * (1.0 - t) * (0.6 + 0.4 * math.sin(i * 2.0 + y * 0.3)))))

    # 4. Grime, and only where there is light to show it.
    for _ in range(3000 * R * R):
        gx, gy = rng.randrange(ROOM_RW), rng.randrange(ROOM_HORIZON)
        fall = lit_at(gx, gy)
        if rng.random() < fall * 0.55:
            put(img, gx, gy, (ROOM_GRIME[0], ROOM_GRIME[1], ROOM_GRIME[2], rng.randint(40, 120)))

    # 3b. Fixtures. Everything is lit by the same lamp. Nothing goes where she sits (the
    #     middle third) or where the plate hangs (upper right).
    def fixture(x, y, c, a=1.0, floor=0.34):
        k = floor + (1.0 - floor) * min(1.0, lit_at(x, y) * 2.2)
        # The dust is on the fixtures too.
        k *= 1.0 + 0.06 * dust.at(x, y) + 0.05 * grain.at(x, y)
        put(img, x, y, (int(c[0] * k), int(c[1] * k), int(c[2] * k), int(255 * a)))

    def rust(x, y, amount):
        # Rust: orange where it is fresh, dark where it has been there longest.
        if amount <= 0:
            return
        c = lerp_color(ROOM_RUST, ROOM_RUST_D, max(0.0, min(1.0, grain.at(x, y) * 0.5 + 0.5)))
        put(img, x, y, (c[0], c[1], c[2], int(255 * min(1.0, amount))))

    # Two-tone paint with the line at shoulder height, worn through in places.
    for y in range(102 * R, ROOM_HORIZON):
        for x in range(ROOM_RW):
            c = img[y][x]
            if y <= 104 * R + 1:
                fixture(x, y, (72, 66, 70), a=0.55)
            elif y == 105 * R:
                put(img, x, y, (ROOM_SEAM[0], ROOM_SEAM[1], ROOM_SEAM[2], 140))
            else:
                worn = max(0.0, pour.at(x, y) - 0.45) * 2.0
                img[y][x] = list(lerp_color(tuple(c), (30, 34, 34, 255), 0.22 * (1.0 - worn)))

    # A steel door on the left, riveted, with a barred window and a cold corridor light behind it.
    DX0, DX1, DY0 = 10 * R, 60 * R, 34 * R
    for y in range(DY0, ROOM_HORIZON):
        for x in range(DX0, DX1 + 1):
            edge = x <= DX0 + 1 or x >= DX1 - 1 or y <= DY0 + 1
            plate = (36, 34, 42) if not edge else (22, 21, 26)
            if y >= ROOM_HORIZON - 22 * R:
                plate = (28, 27, 33)
            fixture(x, y, plate)
            # Rust: from the bottom up, and along the edges, where the water sits.
            up = max(0.0, (y - (ROOM_HORIZON - 40 * R)) / (40.0 * R))
            side = max(0.0, 1.0 - min(x - DX0, DX1 - x) / (6.0 * R))
            rust(x, y, (0.5 * up * up + 0.3 * side * side) * (0.5 + 0.5 * grain.at(x, y)) * min(1.0, lit_at(x, y) * 3.0 + 0.3))
    for y in range(DY0 + 5 * R, ROOM_HORIZON, 9 * R):
        for x in (DX0 + 4 * R, DX1 - 4 * R):
            for dx in range(R):
                for dy in range(R):
                    fixture(x + dx, y + dy, (88, 82, 90))
            fixture(x + R, y + R, (14, 13, 16), a=0.7)
            fixture(x + R + 1, y + R, (14, 13, 16), a=0.5)
            # The streak of rust under each rivet.
            for k in range(6 * R):
                rust(x + rng.choice((0, 1)), y + R + k, 0.5 * (1.0 - k / (6.0 * R)))
    for y in range(52 * R, 74 * R):
        for x in range(24 * R, 46 * R):
            frame = x < 24 * R + R or x >= 46 * R - R or y < 52 * R + R or y >= 74 * R - R
            if frame:
                fixture(x, y, (18, 17, 21))
            elif (x - 24 * R) % (6 * R) < R + 1 and (x - 24 * R) >= 5 * R:
                fixture(x, y, (64, 62, 70))
            else:
                t = (y - 53 * R) / (20.0 * R)
                put(img, x, y, (44 - int(10 * t), 58 - int(12 * t), 74 - int(14 * t), 255))
    for x in range(48 * R, 56 * R):
        for dy in range(R):
            fixture(x, 108 * R + dy, (96, 90, 96))
            fixture(x, 109 * R + dy, (48, 44, 50))
    for y in list(range(DY0 + 8 * R, DY0 + 20 * R)) + list(range(ROOM_HORIZON - 34 * R, ROOM_HORIZON - 22 * R)):
        for dx in range(R):
            fixture(DX0 - 2 * R + dx, y, (54, 50, 58))

    # Pipes along the top of the wall either side of the flex, a valve where the near one
    # turns down, and the drip stains under the joints.
    def pipe(x0, x1, y):
        for x in range(x0, x1 + 1):
            shades = ((92, 84, 88), (66, 60, 64), (44, 40, 46), (20, 18, 22))
            for i, s_ in enumerate(shades):
                for dy in range(R):
                    fixture(x, y + i * R + dy, s_)
            # Rust rings, every so often, where the pipe sweats.
            if (x // R) % 23 == 0:
                for dy in range(4 * R):
                    rust(x, y + dy, 0.5)
    pipe(0, 150 * R, 11 * R)
    pipe(250 * R, ROOM_RW - 1, 11 * R)
    for jx in (40 * R, 104 * R, 300 * R, 356 * R):
        for y in range(10 * R, 16 * R):
            for dx in range(R):
                fixture(jx + dx, y, (58, 52, 58))
                fixture(jx + R + dx, y, (104, 96, 100))
                fixture(jx + 2 * R + dx, y, (58, 52, 58))
    for y in range(15 * R, 32 * R):
        for dx in range(R):
            fixture(150 * R + dx, y, (52, 46, 50))
            fixture(151 * R + dx, y, (78, 70, 74))
            fixture(152 * R + dx, y, (30, 27, 32))
    for a in range(0, 360, 3):
        px, py = 151 * R + 6.0 * R * math.cos(math.radians(a)), 36 * R + 6.0 * R * math.sin(math.radians(a))
        fixture(int(round(px)), int(round(py)), (96, 88, 90))
    for a in range(0, 360, 60):
        for t in range(0, 6 * R):
            fixture(int(round(151 * R + t * math.cos(math.radians(a)))), int(round(36 * R + t * math.sin(math.radians(a)))), (70, 64, 66))
    for jx in (104 * R, 300 * R):
        for y in range(16 * R, 66 * R):
            t = (y - 16 * R) / (50.0 * R)
            for x in range(jx - 1, jx + 5 * R):
                put(img, x + int(2.0 * R * math.sin(y * 0.15)), y, (16, 12, 16, int(90 * (1.0 - t) * (0.5 + 0.5 * math.sin(x * 1.1)))))

    # A cable slung from the flex across to the right wall, sagging.
    for x in range(int(ROOM_LAMP_X) + 4 * R, ROOM_RW):
        t = (x - ROOM_LAMP_X - 4 * R) / (ROOM_RW - ROOM_LAMP_X - 4 * R)
        y = int(round((2 + 6 * t + 14 * 4 * t * (1 - t)) * R))
        for dy in range(R):
            fixture(x, y + dy, (30, 27, 32))
        fixture(x, y + R, (14, 13, 16), a=0.7)

    # A louvred vent low on the right wall.
    VX0, VX1, VY0, VY1 = 306 * R, 346 * R, 112 * R, 132 * R
    for y in range(VY0, VY1 + 1):
        for x in range(VX0, VX1 + 1):
            if x < VX0 + R or x > VX1 - R or y < VY0 + R or y > VY1 - R:
                fixture(x, y, (60, 56, 62))
            elif ((y - VY0) // R) % 4 == 1:
                fixture(x, y, (58, 54, 60))
            elif ((y - VY0) // R) % 4 == 2:
                fixture(x, y, (12, 11, 14))
            else:
                fixture(x, y, (26, 24, 29))
    for y in range(VY1 + 1, VY1 + 18 * R):
        t = (y - VY1) / (18.0 * R)
        for x in list(range(VX0 + 3 * R, VX0 + 5 * R)) + list(range(VX1 - 5 * R, VX1 - 3 * R)):
            put(img, x, y, (14, 11, 14, int(70 * (1.0 - t))))

    # A camera in the corner, up on the right, with its one red light.
    CX_, CY_ = 384 * R, 100 * R
    for y in range(CY_, CY_ + 9 * R):
        for x in range(CX_ - 2 * R, CX_ + 12 * R):
            edge = x < CX_ - 2 * R + R or x >= CX_ + 12 * R - R or y < CY_ + R or y >= CY_ + 9 * R - R
            fixture(x, y, (30, 28, 34) if not edge else (18, 17, 21))
    for y in range(CY_ + 2 * R, CY_ + 7 * R):
        for x in range(CX_ - 6 * R, CX_ - R):
            d = math.hypot(x - (CX_ - 3.5 * R), y - (CY_ + 4 * R)) / R
            if d <= 2.6:
                fixture(x, y, (12, 12, 16) if d > 1.4 else (40, 44, 56))
    for dx in range(R):
        for dy in range(R):
            put(img, CX_ + 9 * R + dx, CY_ + 2 * R + dy, (200, 40, 36, 255))
            put(img, CX_ + 9 * R + dx, CY_ + 3 * R + dy, (120, 24, 22, 160))
    for y in range(CY_ + 9 * R, CY_ + 16 * R):
        for dx in range(R):
            fixture(CX_ + 4 * R + dx, y, (22, 21, 26))
    for x in range(CX_ + 4 * R, ROOM_RW):
        for dy in range(R):
            fixture(x, CY_ + 15 * R + dy, (22, 21, 26))

    # Tally marks scratched under the vent, in fives.
    def tally(x0, y0, groups):
        for gi in range(groups):
            gx = x0 + gi * 11 * R
            for i in range(4):
                for y in range(y0, y0 + 8 * R):
                    fixture(gx + i * 2 * R, y, (110, 100, 100), a=0.55)
            for i in range(8 * R):
                fixture(gx + i, y0 + 8 * R - 1 - i, (110, 100, 100), a=0.55)
    tally(248 * R, 118 * R, 3)
    tally(248 * R, 132 * R, 2)

    # Cracks, each branching once.
    def crack(sx, sy, ex_, ey_, wob, branch=True):
        n = int(max(abs(ey_ - sy), abs(ex_ - sx)))
        for i in range(n):
            t = i / float(n)
            x = int(round(sx + (ex_ - sx) * t + wob * math.sin(t * 9.0) + math.sin(t * 23.0) * R))
            y = int(round(sy + (ey_ - sy) * t))
            put(img, x, y, (ROOM_SEAM[0], ROOM_SEAM[1], ROOM_SEAM[2], int(200 * (1.0 - 0.6 * t))))
            if i % 5 == 0:
                put(img, x + 1, y, (70, 62, 66, 60))
            if branch and i == n // 2:
                crack(x, y, x + int((ex_ - sx) * 0.3) + 8 * R, y + int((ey_ - sy) * 0.25), wob * 0.5, branch=False)
    crack(318 * R, 0, 296 * R, 58 * R, 3.0 * R)
    crack(66 * R, ROOM_HORIZON - 1, 74 * R, 118 * R, 2.0 * R)
    crack(120 * R, 0, 128 * R, 30 * R, 2.0 * R, branch=False)
    crack(360 * R, 60 * R, 372 * R, 96 * R, 2.5 * R)

    # 4. The lamp: flex, shade, filament, and the haze under it.
    for y in range(0, 13 * R):
        for dx in range(R):
            put(img, int(ROOM_LAMP_X) + dx, y, (26, 24, 29, 255))
    for y in range(12 * R, 20 * R):
        half = int((y - 11 * R) * 1.8)
        for sx in range(int(ROOM_LAMP_X) - half, int(ROOM_LAMP_X) + half + 1):
            t = abs(sx - ROOM_LAMP_X) / max(1.0, half)
            put(img, sx, y, lerp_color((64, 57, 58, 255), (22, 20, 24, 255), t))
    for sx in range(int(ROOM_LAMP_X) - 4 * R, int(ROOM_LAMP_X) + 5 * R):
        a = int(255 * (1.0 - abs(sx - ROOM_LAMP_X) / (5.0 * R)))
        for dy in range(R):
            put(img, sx, 20 * R + dy, (255, 232, 196, a))
            put(img, sx, 21 * R + dy, (ROOM_LAMP[0], ROOM_LAMP[1], ROOM_LAMP[2], a // 2))
    for y in range(21 * R, ROOM_HORIZON):
        t = (y - 21 * R) / float(ROOM_HORIZON - 21 * R)
        spread = 6 * R + t * 90 * R
        for sx in range(int(ROOM_LAMP_X - spread), int(ROOM_LAMP_X + spread) + 1):
            k = 1.0 - abs(sx - ROOM_LAMP_X) / spread
            put(img, sx, y, (ROOM_LAMP[0], ROOM_LAMP[1], ROOM_LAMP[2], int(26 * k * k * (1.0 - t) * (0.85 + 0.15 * dust.at(sx, y)))))

    # 5. The table. The far lip is the brightest edge in the room and the surface runs
    #    off into the dark toward the player, which is what the box reads against.
    tgrain = _Noise(rng, ROOM_RW, ROOM_RH, 3 * R)
    for y in range(ROOM_HORIZON, ROOM_HORIZON + ROOM_LIP):
        for sx in range(ROOM_RW):
            edge = abs(sx - ROOM_LAMP_X) / (ROOM_RW / 2)
            c = shade(ROOM_TABLE_LIP, (1.0 - 0.70 * edge ** 1.5) * (1.0 - 0.07 * (y - ROOM_HORIZON) / R))
            c = shade(c, 1.0 + 0.10 * tgrain.at(sx * 0.15, y))
            put(img, sx, y, c)

    for y in range(ROOM_HORIZON + ROOM_LIP, ROOM_RH):
        depth = ((y - ROOM_HORIZON - ROOM_LIP) / float(ROOM_RH - ROOM_HORIZON - ROOM_LIP)) ** 0.62
        for sx in range(ROOM_RW):
            spread = 1.0 + depth * 1.9
            across = abs(sx - ROOM_LAMP_X) / (ROOM_RW / 2 * spread)
            pool = max(0.0, 1.0 - across ** 1.7) * (1.0 - depth) ** 1.5
            c = lerp_color(ROOM_TABLE_NEAR, ROOM_TABLE_FAR, min(1.0, pool * 1.35))
            # The grain runs across the table, stretched by the perspective.
            g = tgrain.at(sx * 0.12, y * 0.9)
            c = shade(c, 1.0 + 0.12 * g * (0.3 + 0.7 * pool))
            # The sheen: the lamp lying on the surface just past the lip.
            sheen = max(0.0, 1.0 - (y - ROOM_HORIZON - ROOM_LIP) / (26.0 * R)) * pool
            c = lerp_color(c, ROOM_TABLE_LIP, 0.18 * sheen * sheen)
            put(img, sx, y, c)

    # Scuffs along the reach direction, only where the light lands, and a couple of rings.
    for _ in range(900 * R * R):
        sy = rng.randrange(ROOM_HORIZON + ROOM_LIP, ROOM_RH)
        sx = rng.randrange(ROOM_RW)
        depth = ((sy - ROOM_HORIZON - ROOM_LIP) / float(ROOM_RH - ROOM_HORIZON - ROOM_LIP)) ** 0.62
        spread = 1.0 + depth * 1.9
        across = abs(sx - ROOM_LAMP_X) / (ROOM_RW / 2 * spread)
        pool = max(0.0, 1.0 - across ** 1.7) * (1.0 - depth) ** 1.5
        if rng.random() > pool * 0.75:
            continue
        for i in range(rng.randint(2 * R, 7 * R)):
            put(img, sx + i, sy, (150, 134, 122, rng.randint(10, 34)))
    for (rx, ry, rr) in ((300 * R, 190 * R, 9 * R), (110 * R, 200 * R, 7 * R)):
        for a in range(0, 360, 2):
            x = int(round(rx + rr * math.cos(math.radians(a))))
            y = int(round(ry + rr * 0.35 * math.sin(math.radians(a))))
            put(img, x, y, (150, 134, 122, 22))

    write_png(os.path.join(OUT, "room.png"), img)



# --------------------------------------------------------------------------- #
# opponent-sheet.png -- the chapter-two opponent: 5 poses across, one row, 107x100 a frame
#
# Columns: hostile, even, open, reach, hurt. Every opponent's sheet has these five columns
# in this order; OpponentSprite cuts them at the size and scale Opponents.cs says. There is
# no talking column: a line landing does not move her, the idle is the talking.
# She is not drawn here. The portrait off her character sheet (tools/source/
# opponent-portrait.png) is keyed and box-sampled at 4 image px per art px, and each
# pose is a few pixels moved. I drew her twice before this and both were a likeness,
# not her, so the sheet is the sprite. Five recolour edits make her the reference
# (Nikki from Obsession): black hair, pale skin, East Asian eyes, dark top, ear + hoop.
# --------------------------------------------------------------------------- #

OPP_OW, OPP_OH = 107, 100

OPP_POSES = ("hostile", "even", "open", "reach", "hurt")

OPP_SOURCE = os.path.join(ROOT, "tools", "source", "opponent-portrait.png")

#: Source image pixels per art pixel.
OPP_PITCH = 4

#: The sheet's background, keyed out, and how far off it a pixel can be and still be it.
OPP_BG = (17, 17, 18)
OPP_BG_TOL = 7

#: Where the portrait sits in the frame. Its bottom row is the table lip.
OPP_OX, OPP_OY = 11, 8

# Landmarks in art pixels. The eyes are not quite level on the sheet, so each has its own rows.
OPP_EYE_L = (27, 35, 29, 33)    # x0, x1, the lash-line row on the sheet, the lower-lid row
OPP_EYE_R = (43, 51, 30, 33)
#: Rows opp_edit_eye_shape moves the lash line. Zero now; see there.
OPP_LID_DROP = 0
OPP_MOUTH_Y = 46                # the line between the lips
OPP_MOUTH_X0, OPP_MOUTH_X1 = 33, 42
OPP_FACE_C = (38.0, 40.0)       # the middle of the face, for the paling and the bruise
OPP_NECK = (32, 46, 55, 70)     # x0, x1, y0, y1

#: The base of the neck the head bends about, and the rows the bend runs over.
OPP_PIVOT = (39.0, 66.0)
OPP_BEND_TOP, OPP_BEND_BOTTOM = 56, 72

#: First row of the knit's band, and the dark ramp it is recoloured to.
OPP_TEE_TOP = 79
OPP_TEE_D = (14, 12, 18, 255)
OPP_TEE_L = (78, 74, 92, 255)

#: The near ear: centre and half-size.
OPP_EAR = (23.5, 35.5, 2.0, 4.0)
OPP_GOLD = (204, 160, 84, 255)
OPP_GOLD_D = (112, 84, 40, 255)

OPP_MOUTH_DARK = (28, 8, 10, 255)
OPP_TEETH      = (218, 202, 190, 255)
OPP_LASH       = (14, 7, 8, 255)

# Per pose: head tilt in degrees (negative is toward the near side), lean toward the
# box, drop, and what the eyes and mouth are doing.
OPP_POSE = {
    # Hostile is the wrong smile: head over, eyes too open, grin too wide. Not angry, that is the point.
    "hostile": dict(tilt=-9.0, lean=0, drop=0, eyes="wide", mouth="grin"),
    # Idle leans toward the box a little, like someone who has been sitting a while.
    "even":    dict(tilt=4.0, lean=1, drop=1, eyes="rest", mouth="rest"),
    "open":    dict(tilt=7.0, lean=1, drop=0, eyes="rest", mouth="smile"),
    # Reaching: leant toward the box, eyes down on what the hand is doing.
    "reach":   dict(tilt=5.0, lean=4, drop=3, eyes="down", mouth="rest"),
    # Hurt: head down and away, eyes shut, mouth open.
    "hurt":    dict(tilt=-7.0, lean=-2, drop=4, eyes="shut", mouth="cry"),
}

#: All the landmarks in one table so the pose edits can take any face. Portrait pixels, except origin.
OPP_FACE = dict(
    origin=(OPP_OX, OPP_OY),
    eye_l=OPP_EYE_L, eye_r=OPP_EYE_R, lid_drop=OPP_LID_DROP,
    mouth=(OPP_MOUTH_X0, OPP_MOUTH_X1, OPP_MOUTH_Y), teeth=OPP_TEETH,
    centre=OPP_FACE_C, neck=OPP_NECK,
    pivot=OPP_PIVOT, bend=(OPP_BEND_TOP, OPP_BEND_BOTTOM),
)


def read_png(path):
    """Decodes an 8-bit non-interlaced PNG into rows of [r, g, b, a]. The mirror of write_png."""
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
    The portrait off the sheet, keyed and sampled down to art pixels. The background is
    flood-filled in from the border rather than keyed by colour alone, because the darkest
    hair is nearly the same grey. Coverage becomes alpha, so her edge keeps the sheet's rim.
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
    # Knit is skin with the colour taken out: more blue for the same red. Red-minus-green
    # alone let the sunlit ribs through.
    return px[3] > 0 and px[0] >= 110 and (px[2] / float(px[0]) >= 0.455 or px[0] - px[1] <= 60)


def opp_edit_dress(img):
    """
    The knit made a dark top: knit pixels from the band down go onto a dark ramp by
    brightness, so the ribs and folds survive. The shoulders stay bare.
    """
    for y in range(OPP_TEE_TOP, len(img)):
        for x in range(len(img[0])):
            px = img[y][x]
            # From the band's second row down everything that is not hair is top; the
            # sliver of chest read as a hole in the cloth. The first row is sorted by colour.
            if px[3] == 0 or px[0] < 60:
                continue
            if y <= OPP_TEE_TOP + 1 and not _is_knit(px):
                continue
            lum = 0.3 * px[0] + 0.59 * px[1] + 0.11 * px[2]
            c = lerp_color(OPP_TEE_D, OPP_TEE_L, (lum - 80.0) / 150.0)
            img[y][x] = [c[0], c[1], c[2], px[3]]


def opp_edit_eye_shape(img):
    """
    The eyes made East Asian: the crease above the lid painted over with the lid's own
    skin (a monolid), and the outer corner lifted a pixel. I tried dropping the lid a row
    too and at eight pixels of eye that looked asleep.
    """
    for side, (x0, x1, lash, lower) in (("L", OPP_EYE_L), ("R", OPP_EYE_R)):
        outer = (x1 - 1, x1) if side == "R" else (x0, x0 + 1)
        # The crease becomes lid, in the cheek's skin, a touch darker for the brow's shadow.
        for x in range(x0, x1 + 1):
            c = img[lower + 2][x]
            img[lash - 1][x] = list(shade((c[0], c[1], c[2], 255), 0.86))
        # The outer corner lifts a pixel.
        for x in outer:
            img[lash - 1][x] = list(img[lash][x])
            c = img[lower + 2][x]
            img[lash][x] = list(shade((c[0], c[1], c[2], 255), 0.90))


def opp_edit_ear(img):
    """
    The near ear tucked out of the hair, with a hoop. An oval painted in the skin from
    the cheek's edge on each row, with a hollow and a darker rim so it reads as an ear.
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


def opp_edit_eyes(img, mode, face):
    """
    What the eyes are doing, by moving rows. wide lifts brow and lash a row; down moves
    the eye down under the lid; shut paints the lids over with cheek skin plus a lash line.
    """
    for side, (x0, x1, lash, lower) in (("L", face["eye_l"]), ("R", face["eye_r"])):
        lash += face["lid_drop"]
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


def opp_edit_mouth(img, mode, face):
    """
    What the mouth is doing, on the ten columns of the lips. The corners are the outer
    three columns each side; one row up or down is the whole difference.
    """
    if mode == "rest":
        return
    x0, x1, my = face["mouth"]
    teeth = face["teeth"]
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
        # Corners up like the smile, the line run two columns past each corner, and a
        # row of teeth pushed in between the lips.
        for x in range(x0, x1 + 1):
            if abs(x - cx) >= 2.5:
                lift(x, -1)
        for k in (1, 2):
            _blend(img, x0 - k, my - 1, OPP_LASH, 0.6 - 0.2 * k)
            _blend(img, x1 + k, my - 1, OPP_LASH, 0.6 - 0.2 * k)
        _shift_block(img, x0, my + 1, x1, my + 3, 0, 1)
        for x in range(x0 + 1, x1):
            y = my + (0 if abs(x - cx) >= 2.5 else 1)
            img[y][x] = list(teeth if x % 2 else shade(teeth, 0.84))
            _blend(img, x, y + 1, OPP_MOUTH_DARK, 0.45)

    elif mode == "cry":
        for x in range(x0, x1 + 1):
            if abs(x - cx) >= 2.5:
                lift(x, 1)
        _shift_block(img, x0 - 1, my + 2, x1 + 1, my + 4, 0, 2)
        for x in range(x0 + 1, x1):
            img[my + 2][x] = list(OPP_MOUTH_DARK)
            img[my + 3][x] = list(OPP_MOUTH_DARK)
            _blend(img, x, my + 1, OPP_MOUTH_DARK, 0.5)


def opp_bend(img, tilt, lean, drop, face):
    """
    Bends the head about the base of the neck, and leans and drops it. Full amount above
    the chin, none at the shoulders, blended down the neck. Sampled backwards so there are
    no holes. Runs on the whole frame so the hair is not cut off at the portrait's edge.
    """
    if abs(tilt) < 0.01 and lean == 0 and drop == 0:
        return img
    h, w = len(img), len(img[0])
    ox, oy = face["origin"]
    px, py = face["pivot"][0] + ox, face["pivot"][1] + oy
    top, bottom = face["bend"][0] + oy, face["bend"][1] + oy
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
# The sheet has brown hair and warm skin. Black hair and greyed skin are what carry the
# resemblance, and both are recolours of what is there so every streak and bit of blush
# survives. I tried a wolf cut and it gave her more hair than face, so the hair is the
# sheet's own.
# --------------------------------------------------------------------------- #

# Black, with a cool grey where the lamp catches it: black hair does not shine brown.
OPP_HAIR_RAMP = ((5, 4, 7), (14, 12, 16), (27, 24, 30), (48, 44, 52), (82, 76, 88), (140, 132, 146))

#: How far the skin goes toward grey, and the grey. Half way keeps the blush and the jaw shadow.
OPP_SKIN_GREY = 0.50
OPP_SKIN_GREY_TONE = (214, 210, 216, 255)


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
    Whether (x, y) is hair, by position as much as colour: above the body line anything
    that is not face or neck; over the shoulders anything not bright enough to be lit skin.
    """
    px = img[y][x]
    if px[3] == 0:
        return False
    fx = OPP_FACE_C[0]
    dx = abs(x - fx)
    if _in_face(x, y):
        return _is_sheet_hair(px) and (y < 25 or dx > 12)
    # Lit skin on the forehead and temples is skin whatever the geometry says; the first
    # draft blackened a band across her brow.
    if y >= 13 and dx <= 17 and px[0] >= 150 and px[0] - px[1] >= 55:
        return False
    if dx <= 8 and y >= 55:
        return False
    if y < _body_top(x):
        return not (y > 24 and dx <= 16 and px[0] >= 100 and px[0] - px[1] > 45)
    if y < OPP_TEE_TOP:
        # The falls over the shoulders: dark, or lit but not as warm as skin.
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
            # At the hairline only what is plainly hair goes all the way black, or she
            # gets a hard band across her brow.
            k = 1.0
            if _in_face(x, y):
                k = max(0.0, min(1.0, (85 - max(px[0], px[1], px[2])) / 35.0))
            c = lerp_color((px[0], px[1], px[2], 255), c, k)
            img[y][x] = [c[0], c[1], c[2], px[3]]


def opp_edit_skin(img):
    """
    The skin, pale: everything warm enough to be skin is pulled halfway toward one cool
    tone. The pull is the same everywhere, so the shading and the blush survive.
    """
    h, w = len(img), len(img[0])
    for y in range(h):
        for x in range(w):
            px = img[y][x]
            # Skin: warm, warmer than the hair (neutral by now) and the top (cold).
            if px[3] == 0 or px[0] < 60 or px[0] - px[1] < 18 or px[0] < px[2] + 10:
                continue
            c = lerp_color((px[0], px[1], px[2], 255), OPP_SKIN_GREY_TONE, OPP_SKIN_GREY)
            img[y][x] = [c[0], c[1], c[2], px[3]]


def build_opponent_frame(img, ox, oy, portrait, pose_name, face):
    p = OPP_POSE[pose_name]
    fx, fy = face["origin"]
    cell = [[list(px) for px in row] for row in portrait]
    opp_edit_eyes(cell, p["eyes"], face)
    opp_edit_mouth(cell, p["mouth"], face)
    frame = new_image(OPP_OW, OPP_OH)
    for y, row in enumerate(cell):
        for x, px in enumerate(row):
            frame[fy + y][fx + x] = list(px)
    frame = opp_bend(frame, p["tilt"], p["lean"], p["drop"], face)
    for y, row in enumerate(frame):
        for x, px in enumerate(row):
            if px[3]:
                img[oy + y][ox + x] = list(px)


def write_opponent_sheet(name, portrait, face):
    """One opponent's sheet: the five poses of one finished portrait, side by side."""
    fx, fy = face["origin"]
    assert fx + len(portrait[0]) <= OPP_OW and fy + len(portrait) == OPP_OH, \
        (name + ": the portrait no longer fits the frame", len(portrait[0]), len(portrait))
    img = new_image(OPP_OW * len(OPP_POSES), OPP_OH)
    for col, pose in enumerate(OPP_POSES):
        build_opponent_frame(img, col * OPP_OW, 0, portrait, pose, face)
    write_png(os.path.join(OUT, name), img)


def build_opponent_sheet():
    portrait = opp_load_portrait()
    opp_edit_dress(portrait)
    opp_edit_hair_black(portrait)
    opp_edit_skin(portrait)
    opp_edit_eye_shape(portrait)
    opp_edit_ear(portrait)
    write_opponent_sheet("opponent-sheet.png", portrait, OPP_FACE)


# --------------------------------------------------------------------------- #
# opponent-second-sheet.png -- the default opponent, off her picture, 140x120 a frame at 4x
#
# She is tools/source/opponent-second-portrait.png with the wall keyed out (it is cool,
# she is warm; leaks into the dark hair closed, largest piece kept, which drops the
# stain), the plate and box in the picture filled down from the rows above, and then
# four things done to her before she is sampled down: the scar and the dirt cleaned
# off her face (dark specks inside the face swapped for the skin around them, eyes,
# nose and mouth left alone), the skin pulled toward a pale grey, the eye sockets put
# in shadow from the brow down, and four picture pixels to the art pixel, drawn at 4x.
# That is what makes her read as less beat up and more wrong, and it lost the fine
# detail the first cut of her had. Every column is that one picture: nothing on her
# moves when a line lands (a mouth edit looked pasted on, and a head bend jumped).
# Frame size and scale are also on Opponents.First and have to agree.
# --------------------------------------------------------------------------- #

OPP2_SOURCE = os.path.join(ROOT, "tools", "source", "opponent-second-portrait.png")

#: The row of the picture that is the table's far edge. Her frame ends there.
OPP2_LIP = 480

#: The columns of the picture that are the frame, at picture size, and the pitch it is
#: sampled at. 560x480 at 4 is the 140x120 frame Opponents.First names.
OPP2_FRAME_X0, OPP2_FRAME_W = 40, 560
OPP2_FRAME_H = OPP2_LIP
OPP2_PITCH = 4
OPP2_ART_W, OPP2_ART_H = OPP2_FRAME_W // OPP2_PITCH, OPP2_FRAME_H // OPP2_PITCH

#: What is in front of her, x0, y0, x1, y1 exclusive: the plate (a little past its right
#: edge, the last letter overhangs it) and the box.
OPP2_HIDDEN = ((0, 388, 254, 508), (382, 382, 606, 508))

#: The wall: cool, darker than skin, and its stain a shade warmer than the rest.
OPP2_WALL_MAX = 110
OPP2_STAIN_MIN = 36

#: The fill under the furniture: rows it is run from, and how much darker by the bottom.
OPP2_FILL_BAND = 30
OPP2_FILL_DARK = 0.28

#: The face in picture pixels: the oval the cleaning runs inside (centre and radii), and
#: the boxes inside it that are left alone (brows and eyes, nose, mouth). The scar is on
#: the cheek between them.
OPP2_FACE_C = (322, 212)
OPP2_FACE_R = (62, 88)
OPP2_FACE_KEEP = ((266, 128, 324, 190), (326, 130, 378, 190), (300, 184, 350, 252), (280, 236, 354, 274))

#: Cleaning: the window (half-size) the local skin is taken from, and how far under it a
#: pixel has to be to count as dirt or scar.
OPP2_CLEAN_HALF = 5
OPP2_CLEAN_DARK = 0.80

#: The skin: where it is looked for (the face oval, wider than the cleaning one, and the
#: neck and chest), what counts as it (warm, and the pull fades in between these two
#: brightnesses so the hair's dark is left alone and there is no hard edge), the grey it
#: goes toward, and how far.
OPP2_SKIN_FACE_R = (78, 108)
OPP2_SKIN_BODY = (160, 240, 560, 392)
OPP2_SKIN_WARMTH = 12
OPP2_SKIN_LUM0, OPP2_SKIN_LUM1 = 38, 85
OPP2_SKIN_PALE = (198, 200, 212, 255)
OPP2_SKIN_PALE_T = 0.55

#: The eye sockets, in picture pixels, and how dark they are at the brow (1 is untouched
#: at the lower lid). Darker eyes on paler skin is most of the scary.
OPP2_EYES = ((266, 140, 324, 194), (326, 142, 378, 194))
OPP2_HOOD = 0.42
OPP2_HOOD_EDGE = 8


def _opp2_hidden(x, y):
    return any(x0 <= x < x1 and y0 <= y < y1 for x0, y0, x1, y1 in OPP2_HIDDEN)


def _opp2_is_wall(px):
    # Cool: more blue than green. The stain has a little red in it and is told from
    # the hair by being lit where hair that dark is not.
    rd, gn, bl = px[0], px[1], px[2]
    if gn - bl > -3 or max(rd, gn, bl) > OPP2_WALL_MAX:
        return False
    return rd - bl <= 2 or (rd - bl <= 8 and max(rd, gn, bl) >= OPP2_STAIN_MIN)


def _lum(px):
    return 0.3 * px[0] + 0.59 * px[1] + 0.11 * px[2]


def _in_box(x, y, boxes):
    return any(x0 <= x < x1 and y0 <= y < y1 for x0, y0, x1, y1 in boxes)


def opp2_load_portrait():
    """
    The second opponent off her picture at picture size: keyed, kept, and filled in
    under the furniture. Returns the frame, OPP2_FRAME_W by OPP2_FRAME_H.
    """
    src = read_png(OPP2_SOURCE)
    H, W = len(src), len(src[0])

    # The wall: flood in from the border over everything cool.
    bg = [[False] * W for _ in range(H)]
    stack = [(x, y) for x in range(W) for y in (0, H - 1)] + [(x, y) for y in range(H) for x in (0, W - 1)]
    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= W or y >= H or bg[y][x] or _opp2_hidden(x, y):
            continue
        if not _opp2_is_wall(src[y][x]):
            continue
        bg[y][x] = True
        stack.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    # Close the leaks into the hair: wall with most of its neighbours in her is her.
    for _ in range(2):
        closed = [row[:] for row in bg]
        for y in range(1, H - 1):
            for x in range(1, W - 1):
                if not bg[y][x]:
                    continue
                n = sum(1 for dy in (-1, 0, 1) for dx in (-1, 0, 1)
                        if (dx or dy) and not bg[y + dy][x + dx] and not _opp2_hidden(x + dx, y + dy))
                if n >= 5:
                    closed[y][x] = False
        bg = closed

    # Keep the largest piece of what is not wall: her, and not the stain.
    seen = [[False] * W for _ in range(H)]
    keep, best = None, 0
    for sy in range(H):
        for sx in range(W):
            if seen[sy][sx] or bg[sy][sx] or _opp2_hidden(sx, sy):
                continue
            piece, stack = [], [(sx, sy)]
            seen[sy][sx] = True
            while stack:
                x, y = stack.pop()
                piece.append((x, y))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < W and 0 <= ny < H and not seen[ny][nx] and not bg[ny][nx] and not _opp2_hidden(nx, ny):
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            if len(piece) > best:
                keep, best = piece, len(piece)
    her = [[False] * W for _ in range(H)]
    for x, y in keep:
        her[y][x] = True

    # The frame: her pixels as they are, from the lip up, with the furniture unknown.
    frame = new_image(OPP2_FRAME_W, OPP2_FRAME_H)
    for y in range(OPP2_FRAME_H):
        for x in range(OPP2_FRAME_W):
            sx = OPP2_FRAME_X0 + x
            if her[y][sx] and not _opp2_hidden(sx, y):
                frame[y][x] = [src[y][sx][0], src[y][sx][1], src[y][sx][2], 255]

    # Nothing of her may be cut off by the frame's sides.
    for y in range(OPP2_FRAME_H):
        for sx in list(range(0, OPP2_FRAME_X0)) + list(range(OPP2_FRAME_X0 + OPP2_FRAME_W, W)):
            assert not her[y][sx] or _opp2_hidden(sx, y), ("she is wider than the frame", sx, y)

    # Fill down under the furniture from the rows above it.
    for x in range(OPP2_FRAME_W):
        sx = OPP2_FRAME_X0 + x
        last = None
        for y in range(OPP2_FRAME_H):
            if not _opp2_hidden(sx, y):
                last = y if frame[y][x][3] else None
                continue
            if last is None:
                continue
            band = [frame[yy][x] for yy in range(max(0, last - OPP2_FILL_BAND + 1), last + 1)]
            mean = tuple(sum(px[i] for px in band) // len(band) for i in range(3)) + (255,)
            period = max(1, 2 * len(band) - 2)
            i = (y - last - 1) % period
            if i >= len(band):
                i = period - i
            px = band[len(band) - 1 - i]
            c = lerp_color((px[0], px[1], px[2], 255), mean, 0.5)
            c = shade(c, 1.0 - OPP2_FILL_DARK * min(1.0, (y - last) / 90.0))
            frame[y][x] = [c[0], c[1], c[2], 255]

    return frame


def opp2_clean_face(frame):
    """
    Takes the scar and the dirt off her face. Inside the face oval, and outside the eyes,
    nose and mouth, any pixel much darker than the skin around it becomes that skin: the
    pixel of middling brightness in the window round it. Two passes, for the wider marks.
    """
    fx, fy = OPP2_FACE_C[0] - OPP2_FRAME_X0, OPP2_FACE_C[1]
    rx, ry = OPP2_FACE_R
    keep = [(x0 - OPP2_FRAME_X0, y0, x1 - OPP2_FRAME_X0, y1) for x0, y0, x1, y1 in OPP2_FACE_KEEP]
    h, w = len(frame), len(frame[0])

    def in_region(x, y):
        return ((x - fx) / rx) ** 2 + ((y - fy) / ry) ** 2 <= 1.0 and not _in_box(x, y, keep) and frame[y][x][3] > 0

    for _ in range(2):
        out = [[list(px) for px in row] for row in frame]
        for y in range(max(0, fy - ry), min(h, fy + ry + 1)):
            for x in range(max(0, fx - rx), min(w, fx + rx + 1)):
                if not in_region(x, y):
                    continue
                window = [frame[yy][xx] for yy in range(max(0, y - OPP2_CLEAN_HALF), min(h, y + OPP2_CLEAN_HALF + 1))
                          for xx in range(max(0, x - OPP2_CLEAN_HALF), min(w, x + OPP2_CLEAN_HALF + 1)) if in_region(xx, yy)]
                window.sort(key=_lum)
                mid = window[len(window) // 2]
                if _lum(frame[y][x]) < _lum(mid) * OPP2_CLEAN_DARK:
                    out[y][x] = [mid[0], mid[1], mid[2], 255]
        frame = out
    return frame


def opp2_make_pale(frame):
    """
    The skin toward grey and the eye sockets into shadow. Skin is anything bright enough
    and warmer than the knit; the sockets darken from the brow down to the lower lid, with
    the sides feathered so there is no box.
    """
    fx, fy = OPP2_FACE_C[0] - OPP2_FRAME_X0, OPP2_FACE_C[1]
    rx, ry = OPP2_SKIN_FACE_R
    bx0, by0, bx1, by1 = OPP2_SKIN_BODY[0] - OPP2_FRAME_X0, OPP2_SKIN_BODY[1], OPP2_SKIN_BODY[2] - OPP2_FRAME_X0, OPP2_SKIN_BODY[3]
    out = [[list(px) for px in row] for row in frame]
    for y, row in enumerate(frame):
        for x, px in enumerate(row):
            if px[3] == 0 or px[0] - px[2] < OPP2_SKIN_WARMTH:
                continue
            if ((x - fx) / rx) ** 2 + ((y - fy) / ry) ** 2 > 1.0 and not (bx0 <= x < bx1 and by0 <= y < by1):
                continue
            t = (_lum(px) - OPP2_SKIN_LUM0) / float(OPP2_SKIN_LUM1 - OPP2_SKIN_LUM0)
            t = max(0.0, min(1.0, t))
            t = t * t * (3.0 - 2.0 * t)
            c = lerp_color((px[0], px[1], px[2], 255), OPP2_SKIN_PALE, OPP2_SKIN_PALE_T * t)
            out[y][x] = [c[0], c[1], c[2], 255]
    for x0, y0, x1, y1 in OPP2_EYES:
        x0, x1 = x0 - OPP2_FRAME_X0, x1 - OPP2_FRAME_X0
        for y in range(y0, y1):
            down = (y - y0) / float(y1 - y0)
            f = OPP2_HOOD + (1.0 - OPP2_HOOD) * down ** 1.5
            for x in range(x0, x1):
                edge = min(x - x0, x1 - 1 - x) / float(OPP2_HOOD_EDGE)
                ff = f + (1.0 - f) * max(0.0, 1.0 - edge)
                px = out[y][x]
                if px[3]:
                    c = shade((px[0], px[1], px[2], 255), ff)
                    out[y][x] = [c[0], c[1], c[2], 255]
    return out


def opp2_sample(frame):
    """Box-samples the picture-size frame down to art pixels, with coverage as alpha."""
    P = OPP2_PITCH
    out = new_image(OPP2_ART_W, OPP2_ART_H)
    for by in range(OPP2_ART_H):
        for bx in range(OPP2_ART_W):
            acc, n = [0, 0, 0], 0
            for y in range(by * P, by * P + P):
                for x in range(bx * P, bx * P + P):
                    px = frame[y][x]
                    if px[3]:
                        acc[0] += px[0]
                        acc[1] += px[1]
                        acc[2] += px[2]
                        n += 1
            if n:
                out[by][bx] = [acc[0] // n, acc[1] // n, acc[2] // n, 255 * n // (P * P)]
    return out


def build_second_opponent_sheet():
    base = opp2_sample(opp2_make_pale(opp2_clean_face(opp2_load_portrait())))
    img = new_image(OPP2_ART_W * len(OPP_POSES), OPP2_ART_H)
    for col in range(len(OPP_POSES)):
        for y, row in enumerate(base):
            for x, px in enumerate(row):
                if px[3]:
                    img[y][col * OPP2_ART_W + x] = list(px)
    write_png(os.path.join(OUT, "opponent-second-sheet.png"), img)



# --------------------------------------------------------------------------- #
# token-sheet.png -- the steel tag the box pays with, 8 frames of it turning, 24x24
#
# Eight frames turn it through a half circle, which for a shape with two matching ends
# is a whole one. It lies flat so the lamp lights it evenly; the spin is sold by the
# brushed grain and the sheen, which turn with it, and the bevel on the top-left edge,
# which does not. Drawn at 3x and averaged down, like the hand.
# --------------------------------------------------------------------------- #

TOKEN_SIZE = 24
TOKEN_FRAMES = 8
TOKEN_SS = 3

TOKEN_STEEL   = (146, 142, 150)
TOKEN_STEEL_D = (58, 54, 62)
TOKEN_STEEL_L = (222, 218, 226)
TOKEN_RIM     = (12, 9, 14)
TOKEN_RED     = (172, 54, 40)

#: Half the tag's length and half its width, in frame pixels, and the corner radius.
TOKEN_HALF_L, TOKEN_HALF_W, TOKEN_CORNER = 8.0, 4.4, 2.0

#: Where the hole is along the tag, and how big.
TOKEN_HOLE_X, TOKEN_HOLE_R = -5.0, 1.15


def _tag_inside(u, v):
    """How far inside the rounded oblong (u, v) is, in frame pixels; negative is outside."""
    qx = abs(u) - (TOKEN_HALF_L - TOKEN_CORNER)
    qy = abs(v) - (TOKEN_HALF_W - TOKEN_CORNER)
    outside = math.hypot(max(qx, 0.0), max(qy, 0.0))
    inside = min(max(qx, qy), 0.0)
    return TOKEN_CORNER - (outside + inside)


def build_token_frame(img, ox, angle):
    S = TOKEN_SS
    ca, sa = math.cos(angle), math.sin(angle)
    cx = cy = TOKEN_SIZE / 2.0
    cells = []
    for Y in range(TOKEN_SIZE * S):
        row = []
        py = (Y + 0.5) / S - cy
        for X in range(TOKEN_SIZE * S):
            px = (X + 0.5) / S - cx
            # Into the tag's own frame, which turns with it.
            u = px * ca + py * sa
            v = -px * sa + py * ca
            d = _tag_inside(u, v)
            if d <= 0.0 or math.hypot(u - TOKEN_HOLE_X, v) < TOKEN_HOLE_R:
                row.append(None)
                continue
            # Brushed steel: the grain runs along the tag and catches the light in bands.
            grain = 0.5 + 0.5 * math.sin(v * 5.5 + u * 0.4) * 0.5 + 0.25 * math.sin(v * 13.0 + 2.0)
            t = 0.35 + 0.45 * grain
            # The sheen: a band of light across the tag that turns with it.
            t += 0.35 * math.exp(-((u * 0.55 + v * 0.8 - 1.5) / 2.6) ** 2)
            # The bevel: lit top-left, dark bottom-right, and it does not turn.
            if d < 1.1:
                lit = (-px - py) / max(1.0, math.hypot(px, py))
                t += 0.45 * lit
            # The box's red on the end nearest it, which is up the screen.
            red = max(0.0, -py / TOKEN_HALF_L) * 0.18
            c = lerp_color(TOKEN_STEEL_D + (255,), TOKEN_STEEL + (255,), min(1.0, t))
            if t > 1.0:
                c = lerp_color(TOKEN_STEEL + (255,), TOKEN_STEEL_L + (255,), min(1.0, t - 1.0))
            c = lerp_color(c, TOKEN_RED + (255,), red)
            # The rim of the hole, in shadow.
            if math.hypot(u - TOKEN_HOLE_X, v) < TOKEN_HOLE_R + 0.6:
                c = lerp_color(c, TOKEN_RIM + (255,), 0.6)
            row.append(c)
        cells.append(row)

    for y in range(TOKEN_SIZE):
        for x in range(TOKEN_SIZE):
            acc, n = [0, 0, 0], 0
            for Y in range(y * S, y * S + S):
                for X in range(x * S, x * S + S):
                    c = cells[Y][X]
                    if c is None:
                        continue
                    acc[0] += c[0]
                    acc[1] += c[1]
                    acc[2] += c[2]
                    n += 1
            if n:
                img[y][ox + x] = [acc[0] // n, acc[1] // n, acc[2] // n, 255 * n // (S * S)]
    _rim_frame(img, ox, TOKEN_SIZE, TOKEN_RIM)


def _rim_frame(img, ox, size, rim):
    """The dark rim the sheets have round everything: every edge pixel, halfway to the rim colour."""
    for y in range(size):
        for x in range(size):
            p = img[y][ox + x]
            if p[3] < 40:
                continue
            for xx, yy in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if xx < 0 or yy < 0 or xx >= size or yy >= size or img[yy][ox + xx][3] < 40:
                    img[y][ox + x] = list(lerp_color(tuple(p), rim + (p[3],), 0.5))
                    break


def build_token_sheet():
    img = new_image(TOKEN_SIZE * TOKEN_FRAMES, TOKEN_SIZE)
    for f in range(TOKEN_FRAMES):
        build_token_frame(img, f * TOKEN_SIZE, math.pi * f / TOKEN_FRAMES)
    write_png(os.path.join(OUT, "token-sheet.png"), img)


# --------------------------------------------------------------------------- #
# item-sheet.png -- a picture of every item the box deals, 24x24 each, in ItemId order
#
# Eighteen frames across, one per ItemId in the enum's order, so the frame index is the
# enum value and ItemSprite has nothing to look up. Each one is a handful of shapes
# (discs, rings, boxes, capsules, polygons) rasterised at 3x, lit from the upper left
# where the lamp is with a dark line at every edge, then averaged down like the token so
# they sit on the same table as the tag. They show on the pocket plates at 2x and beside
# the dealt item's line on the plate at 3x.
# --------------------------------------------------------------------------- #

ITEM_SIZE = 24
ITEM_SS = 3

ITEM_STEEL   = (146, 142, 150)
ITEM_STEEL_D = (58, 54, 62)
ITEM_BRASS   = (196, 156, 84)
ITEM_BRASS_D = (110, 82, 38)
ITEM_BONE    = (226, 214, 200)
ITEM_BONE_D  = (150, 138, 126)
ITEM_RED     = (172, 40, 34)
ITEM_RED_D   = (96, 16, 18)
ITEM_ASH     = (96, 92, 98)
ITEM_ASH_D   = (40, 38, 44)
ITEM_EMBER   = (250, 150, 60)
ITEM_FLAME   = (255, 226, 140)
ITEM_GLASS   = (150, 190, 200)
ITEM_GLINT   = (236, 246, 250)
ITEM_GREEN   = (48, 82, 52)
ITEM_GREEN_L = (92, 132, 92)
ITEM_INK     = (26, 22, 30)
ITEM_AMBER   = (196, 120, 40)
ITEM_CARD    = (54, 60, 84)
ITEM_RIM     = (12, 9, 14)

#: How much brighter the lit corner of a shape is than the dark one, and how wide the edge line is.
ITEM_LIGHT = 0.30
ITEM_EDGE = 0.55


def _sd_disc(x, y, cx, cy, radius):
    return radius - math.hypot(x - cx, y - cy)


def _sd_ring(x, y, cx, cy, radius, width):
    d = math.hypot(x - cx, y - cy)
    return min(radius - d, d - (radius - width))


def _sd_box(x, y, x0, y0, x1, y1, corner=0.0, rot=0.0):
    """A box with rounded corners, turned rot degrees about its middle."""
    cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
    if rot:
        a = math.radians(rot)
        ca, sa = math.cos(a), math.sin(a)
        dx, dy = x - cx, y - cy
        x, y = cx + dx * ca + dy * sa, cy - dx * sa + dy * ca
    qx = abs(x - cx) - ((x1 - x0) / 2.0 - corner)
    qy = abs(y - cy) - ((y1 - y0) / 2.0 - corner)
    outside = math.hypot(max(qx, 0.0), max(qy, 0.0))
    inside = min(max(qx, qy), 0.0)
    return corner - (outside + inside)


def _sd_line(x, y, x0, y0, x1, y1, width):
    """A capsule: a line of some thickness with round ends."""
    dx, dy = x1 - x0, y1 - y0
    length2 = dx * dx + dy * dy
    t = 0.0 if length2 == 0.0 else max(0.0, min(1.0, ((x - x0) * dx + (y - y0) * dy) / length2))
    return width / 2.0 - math.hypot(x - x0 - t * dx, y - y0 - t * dy)


def _sd_poly(x, y, points):
    """Even-odd inside test, and the distance to the nearest edge for the sign."""
    inside = False
    nearest = float("inf")
    n = len(points)
    for i in range(n):
        ax, ay = points[i]
        bx, by = points[(i + 1) % n]
        nearest = min(nearest, -_sd_line(x, y, ax, ay, bx, by, 0.0))
        if (ay > y) != (by > y):
            if x < ax + (y - ay) * (bx - ax) / (by - ay):
                inside = not inside
    return nearest if inside else -nearest


def _item_sd(shape, x, y):
    kind = shape[0]
    if kind == "disc":
        return _sd_disc(x, y, *shape[2:])
    if kind == "ring":
        return _sd_ring(x, y, *shape[2:])
    if kind == "box":
        return _sd_box(x, y, *shape[2:])
    if kind == "line":
        return _sd_line(x, y, *shape[2:])
    return _sd_poly(x, y, shape[2])


def _turn(points, deg, cx=12.0, cy=12.0):
    """Turns a list of points about a centre, for pips and marks on a turned shape."""
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * ca - (y - cy) * sa, cy + (x - cx) * sa + (y - cy) * ca) for x, y in points]


def build_item_frame(img, ox, shapes):
    """Paints the shapes in order, later ones over earlier, then lights and rims them."""
    S = ITEM_SS
    cells = []
    for Y in range(ITEM_SIZE * S):
        row = []
        py = (Y + 0.5) / S
        for X in range(ITEM_SIZE * S):
            px = (X + 0.5) / S
            hit = None
            for shape in shapes:
                d = _item_sd(shape, px, py)
                if d > 0.0:
                    hit = (shape[1], d)
            if hit is None:
                row.append(None)
                continue
            colour, d = hit
            # The lamp is up and to the left of everything on the table.
            lit = 0.5 + 0.5 * (-(px - 12.0) - (py - 12.0)) / 17.0
            t = 1.0 - ITEM_LIGHT / 2.0 + ITEM_LIGHT * lit
            c = tuple(min(255, int(ch * t)) for ch in colour) + (255,)
            if d < ITEM_EDGE:
                c = lerp_color(c, ITEM_RIM + (255,), 0.55)
            row.append(c)
        cells.append(row)
    _downsample_into(img, ox, cells, ITEM_SIZE, S)
    _rim_frame(img, ox, ITEM_SIZE, ITEM_RIM)


def item_shapes():
    """Every item, in ItemId order. Coordinates are frame pixels, y down."""
    die = _turn([(8.5, 8.0), (12.0, 12.0), (15.5, 16.0)], 15)
    flame = [(12.0, 2.0), (16.0, 7.0), (15.0, 12.0), (12.0, 10.0), (9.0, 12.0), (8.0, 7.0)]
    return [
        # Cinder: a lump of pressed ash with the last of the fire in it.
        [("poly", ITEM_ASH, [(5, 15), (8, 8), (13, 6), (18, 9), (20, 14), (17, 19), (9, 19)]),
         ("line", ITEM_ASH_D, 9, 13, 14, 11, 1.0),
         ("line", ITEM_ASH_D, 13, 15, 17, 14, 1.0),
         ("disc", ITEM_EMBER, 15.0, 15.5, 1.0),
         ("disc", ITEM_EMBER, 8.5, 12.0, 0.8)],
        # Spent Shell: a brass casing on its side, the open mouth toward the box.
        [("line", ITEM_BRASS, 7, 16, 17, 8, 6.0),
         ("disc", ITEM_BRASS_D, 7.0, 16.0, 3.3),
         ("disc", ITEM_BRASS, 7.0, 16.0, 2.2),
         ("disc", ITEM_ASH_D, 17.0, 8.0, 2.2)],
        # Revolver: side on, barrel to the left.
        [("box", ITEM_STEEL, 3, 8, 14, 11, 0.6),
         ("disc", ITEM_STEEL_D, 13.0, 11.0, 3.2),
         ("disc", ITEM_ASH_D, 13.0, 11.0, 1.3),
         ("box", ITEM_ASH_D, 14, 12, 19, 20, 1.0, 20),
         ("ring", ITEM_STEEL_D, 11.5, 14.5, 2.4, 0.9),
         ("box", ITEM_STEEL_D, 16.5, 7, 19, 10, 0.5)],
        # Pact: a folded paper with two lines on it and a seal in blood-red wax.
        [("box", ITEM_BONE, 6, 4, 18, 20, 1.0),
         ("poly", ITEM_BONE_D, [(6, 4), (12, 4), (6, 10)]),
         ("line", ITEM_INK, 9, 12, 15, 12, 1.0),
         ("line", ITEM_INK, 9, 15, 14, 15, 1.0),
         ("disc", ITEM_RED, 15.0, 17.0, 2.6),
         ("disc", ITEM_RED_D, 15.0, 17.0, 1.1)],
        # Wager: a die, since the box does the choosing.
        [("box", ITEM_BONE, 5, 5, 19, 19, 2.5, 15)]
        + [("disc", ITEM_INK, x, y, 1.2) for x, y in die],
        # High Card: one card, face up, a diamond on it.
        [("box", ITEM_BONE, 7, 3, 17, 21, 1.2),
         ("poly", ITEM_RED, [(12, 8.5), (14.6, 12), (12, 15.5), (9.4, 12)]),
         ("disc", ITEM_RED, 9.2, 5.8, 0.8),
         ("disc", ITEM_RED, 14.8, 18.2, 0.8)],
        # Tourniquet: a roll of bandage with the end trailing, and it has been used before.
        [("line", ITEM_BONE, 4, 17, 13, 15, 4.0),
         ("disc", ITEM_RED, 7.0, 16.5, 1.3),
         ("disc", ITEM_BONE, 14.0, 11.0, 5.5),
         ("ring", ITEM_BONE_D, 14.0, 11.0, 3.6, 1.0),
         ("disc", ITEM_BONE_D, 14.0, 11.0, 1.0)],
        # Ash Veil: a grey cloth hung from a rod.
        [("line", ITEM_STEEL_D, 4, 5, 20, 5, 1.6),
         ("poly", ITEM_ASH, [(5, 6), (19, 6), (17.5, 20), (6.5, 20)]),
         ("line", ITEM_ASH_D, 9, 7, 8.5, 19, 1.0),
         ("line", ITEM_ASH_D, 15, 7, 15.5, 19, 1.0),
         ("line", ITEM_STEEL, 12, 7, 12, 19, 0.8)],
        # Mirror: a hand mirror, brass round the glass, the glint where the lamp is.
        [("line", ITEM_BRASS_D, 13, 13, 19.5, 20, 3.0),
         ("disc", ITEM_BRASS, 10.0, 9.5, 6.6),
         ("disc", (190, 196, 210), 10.0, 9.5, 5.0),
         ("line", ITEM_GLINT, 7.2, 8.0, 9.0, 6.0, 1.3)],
        # Lens: a magnifier, dark handle, the glass a little blue.
        [("line", ITEM_ASH_D, 14, 14, 20, 20, 3.0),
         ("disc", ITEM_BRASS, 9.5, 9.5, 6.6),
         ("disc", ITEM_GLASS, 9.5, 9.5, 4.9),
         ("line", ITEM_GLINT, 6.8, 8.2, 8.4, 6.6, 1.3)],
        # Tally: five marks on a bone plate, four and the stroke through them.
        [("box", ITEM_BONE, 4, 5, 20, 19, 1.0),
         ("line", ITEM_INK, 7, 8, 7, 16, 1.2),
         ("line", ITEM_INK, 9.7, 8, 9.7, 16, 1.2),
         ("line", ITEM_INK, 12.3, 8, 12.3, 16, 1.2),
         ("line", ITEM_INK, 15, 8, 15, 16, 1.2),
         ("line", ITEM_INK, 5.5, 16, 17.5, 8, 1.2)],
        # Marked Deck: two cards face down, the top one carrying the mark.
        [("box", ITEM_BONE, 8, 3, 18, 19, 1.2, 6),
         ("box", ITEM_CARD, 9.2, 4.2, 16.8, 17.8, 0.8, 6),
         ("box", ITEM_BONE, 5, 5, 15, 21, 1.2, -6),
         ("box", ITEM_CARD, 6.2, 6.2, 13.8, 19.8, 0.8, -6),
         ("disc", ITEM_RED, 10.0, 13.0, 1.4)],
        # Confession: a candle, lit.
        [("line", ITEM_BRASS_D, 6, 20, 18, 20, 2.0),
         ("box", ITEM_BONE, 9, 9, 15, 20, 0.8),
         ("line", ITEM_INK, 12, 7.5, 12, 9, 1.0),
         ("poly", ITEM_EMBER, [(12, 2), (14.6, 6), (12, 9.6), (9.4, 6)]),
         ("disc", ITEM_FLAME, 12.0, 6.6, 1.2)],
        # Levy: something of theirs, burning.
        [("box", ITEM_ASH_D, 7, 8, 17, 21, 1.0, -10),
         ("poly", ITEM_EMBER, flame),
         ("disc", ITEM_FLAME, 12.0, 8.2, 1.6),
         ("disc", ITEM_EMBER, 17.5, 4.5, 0.8)],
        # Second Hand: a clock face. The box takes again without waiting for the round.
        [("disc", ITEM_BRASS_D, 12.0, 12.0, 9.2),
         ("disc", ITEM_BONE, 12.0, 12.0, 7.6),
         ("line", ITEM_INK, 12, 12, 12, 6.5, 1.3),
         ("line", ITEM_INK, 12, 12, 16, 12, 1.3),
         ("line", ITEM_RED, 12, 12, 8.2, 16, 0.8),
         ("disc", ITEM_INK, 12.0, 12.0, 0.9)],
        # Wild Card: a card split corner to corner, one pip on each half.
        [("box", ITEM_BONE, 7, 3, 17, 21, 1.2),
         ("poly", ITEM_INK, [(8, 3.6), (16.4, 3.6), (16.4, 20.4)]),
         ("disc", ITEM_RED, 10.0, 15.0, 1.5),
         ("disc", ITEM_BONE, 14.0, 9.0, 1.5)],
        # Rotgut: a bottle of something green, corked.
        [("box", ITEM_GREEN, 10.5, 3, 13.5, 9, 0.8),
         ("box", ITEM_GREEN, 8, 8, 16, 21, 2.0),
         ("line", ITEM_GREEN_L, 9.6, 10.5, 9.6, 18.5, 1.0),
         ("box", ITEM_BONE_D, 9.5, 13, 14.5, 17, 0.3),
         ("box", ITEM_BRASS_D, 10.8, 2, 13.2, 4.5, 0.5)],
        # Last Call: two glasses, poured.
        [("poly", ITEM_GLASS, [(3, 8), (11, 8), (10, 20), (4, 20)]),
         ("poly", ITEM_AMBER, [(4.6, 13), (9.8, 13), (9.4, 19.2), (4.9, 19.2)]),
         ("poly", ITEM_GLASS, [(13, 8), (21, 8), (20, 20), (14, 20)]),
         ("poly", ITEM_AMBER, [(14.6, 12), (19.8, 12), (19.4, 19.2), (14.9, 19.2)]),
         ("line", ITEM_GLINT, 3, 8, 11, 8, 0.9),
         ("line", ITEM_GLINT, 13, 8, 21, 8, 0.9)],
    ]


def build_item_sheet():
    shapes = item_shapes()
    img = new_image(ITEM_SIZE * len(shapes), ITEM_SIZE)
    for i, item in enumerate(shapes):
        build_item_frame(img, i * ITEM_SIZE, item)
    write_png(os.path.join(OUT, "item-sheet.png"), img)


# --------------------------------------------------------------------------- #
# sight-sheet.png, ember-sheet.png, steady-bar.png -- the three checks' furniture
#
# The sight is a bone-white ring with four ticks, 24x24, four frames turning a quarter
# of the way round (which for four ticks is all the way). The ember is a coal that
# breathes, 16x16, four frames. The bar is the groove the ember sits in, 145x8, with the
# lit band in the middle (SteadyCheck has the same band numbers). All drawn at 3x and
# averaged down.
# --------------------------------------------------------------------------- #

SIGHT_SIZE, SIGHT_FRAMES, SIGHT_SS = 24, 4, 3
SIGHT_BONE = (232, 224, 210)
SIGHT_RIM = (14, 10, 12)

EMBER_SIZE, EMBER_FRAMES, EMBER_SS = 16, 4, 3
EMBER_CORE = (255, 236, 170)
EMBER_HOT = (250, 150, 60)
EMBER_RED = (176, 44, 28)
EMBER_CRUST = (48, 20, 18)

BAR_W, BAR_H = 145, 8
BAR_BAND_START, BAR_BAND_WIDTH = 54, 37
BAR_GROOVE = (26, 24, 30)
BAR_GROOVE_L = (58, 54, 62)
BAR_BAND = (120, 100, 72)
BAR_BAND_L = (196, 168, 118)
BAR_TICK = (232, 210, 160)


def _downsample_into(img, ox, cells, size, ss):
    for y in range(size):
        for x in range(size):
            acc, n = [0, 0, 0, 0], 0
            for Y in range(y * ss, y * ss + ss):
                for X in range(x * ss, x * ss + ss):
                    c = cells[Y][X]
                    if c is None:
                        continue
                    acc[0] += c[0]
                    acc[1] += c[1]
                    acc[2] += c[2]
                    acc[3] += c[3]
                    n += 1
            if n:
                img[y][ox + x] = [acc[0] // n, acc[1] // n, acc[2] // n, acc[3] // (ss * ss)]


def build_sight_frame(img, ox, turn):
    S = SIGHT_SS
    c = SIGHT_SIZE / 2.0
    cells = []
    for Y in range(SIGHT_SIZE * S):
        row = []
        py = (Y + 0.5) / S - c
        for X in range(SIGHT_SIZE * S):
            px = (X + 0.5) / S - c
            d = math.hypot(px, py)
            ang = math.atan2(py, px)
            a = 0.0
            # The ring, a pixel and a half thick.
            if abs(d - 9.2) < 0.8:
                a = 1.0
            # Four ticks, turned by this frame, from just inside the ring to well outside.
            for k in range(4):
                ta = turn + k * math.pi / 2.0
                along = px * math.cos(ta) + py * math.sin(ta)
                across = -px * math.sin(ta) + py * math.cos(ta)
                if 7.2 <= along <= 11.6 and abs(across) < 0.75:
                    a = 1.0
            # The dot in the middle.
            if d < 1.1:
                a = 1.0
            if a <= 0.0:
                row.append(None)
                continue
            row.append((SIGHT_BONE[0], SIGHT_BONE[1], SIGHT_BONE[2], 255))
        cells.append(row)
    _downsample_into(img, ox, cells, SIGHT_SIZE, S)
    # The dark rim, so it reads over her face as well as over the dark.
    for y in range(SIGHT_SIZE):
        for x in range(SIGHT_SIZE):
            p = img[y][ox + x]
            if p[3] >= 40:
                continue
            for xx, yy in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= xx < SIGHT_SIZE and 0 <= yy < SIGHT_SIZE and img[yy][ox + xx][3] >= 120:
                    img[y][ox + x] = [SIGHT_RIM[0], SIGHT_RIM[1], SIGHT_RIM[2], max(p[3], 150)]
                    break


def build_sight_sheet():
    img = new_image(SIGHT_SIZE * SIGHT_FRAMES, SIGHT_SIZE)
    for f in range(SIGHT_FRAMES):
        build_sight_frame(img, f * SIGHT_SIZE, (math.pi / 2.0) * f / SIGHT_FRAMES)
    write_png(os.path.join(OUT, "sight-sheet.png"), img)


def build_ember_frame(img, ox, breath, rng):
    S = EMBER_SS
    c = EMBER_SIZE / 2.0
    radius = 4.6 + 0.9 * breath
    cells = []
    for Y in range(EMBER_SIZE * S):
        row = []
        py = (Y + 0.5) / S - c
        for X in range(EMBER_SIZE * S):
            px = (X + 0.5) / S - c
            # A lumpy coal rather than a ball: the radius wanders round the edge.
            ang = math.atan2(py, px)
            r = radius * (1.0 + 0.10 * math.sin(ang * 3.0 + 0.7) + 0.06 * math.sin(ang * 5.0 + breath * 4.0))
            d = math.hypot(px, py) / r
            if d > 1.35:
                row.append(None)
                continue
            if d > 1.0:
                # The corona: light off the coal, fading fast.
                k = (1.35 - d) / 0.35
                row.append((EMBER_HOT[0], EMBER_HOT[1], EMBER_HOT[2], int(120 * k * k * (0.7 + 0.3 * breath))))
                continue
            t = d * d
            col = lerp_color(EMBER_CORE + (255,), EMBER_HOT + (255,), min(1.0, t * 1.6))
            col = lerp_color(col, EMBER_RED + (255,), max(0.0, (t - 0.55) / 0.45))
            # The crust: dark patches where the coal is not glowing through; they move as it breathes.
            crust = 0.5 + 0.5 * math.sin(px * 1.9 + breath * 2.0) * math.sin(py * 2.3 - breath)
            if crust > 0.78 and d > 0.35:
                col = lerp_color(col, EMBER_CRUST + (255,), 0.75)
            row.append(col)
        cells.append(row)
    _downsample_into(img, ox, cells, EMBER_SIZE, S)


def build_ember_sheet():
    img = new_image(EMBER_SIZE * EMBER_FRAMES, EMBER_SIZE)
    rng = random.Random(3131)
    for f in range(EMBER_FRAMES):
        build_ember_frame(img, f * EMBER_SIZE, 0.5 + 0.5 * math.sin(math.pi * 2.0 * f / EMBER_FRAMES), rng)
    write_png(os.path.join(OUT, "ember-sheet.png"), img)


def build_steady_bar():
    img = new_image(BAR_W, BAR_H)
    for y in range(BAR_H):
        for x in range(BAR_W):
            edge = x == 0 or x == BAR_W - 1 or y == 0 or y == BAR_H - 1
            in_band = BAR_BAND_START <= x < BAR_BAND_START + BAR_BAND_WIDTH
            if edge:
                c = BAR_GROOVE_L if y == 0 else BAR_GROOVE
                c = (c[0] // 2, c[1] // 2, c[2] // 2)
            elif in_band:
                # Lit, brightest along the middle of the groove.
                k = 1.0 - abs((y - 3.5) / 3.5) ** 2
                c = lerp_color(BAR_BAND + (255,), BAR_BAND_L + (255,), 0.6 * k)[:3]
            else:
                k = 1.0 - abs((y - 3.5) / 3.5) ** 2
                c = lerp_color(BAR_GROOVE + (255,), BAR_GROOVE_L + (255,), 0.35 * k)[:3]
            put(img, x, y, (c[0], c[1], c[2], 255))
    # A tick at each end of the band.
    for x in (BAR_BAND_START, BAR_BAND_START + BAR_BAND_WIDTH - 1):
        for y in range(1, BAR_H - 1):
            put(img, x, y, BAR_TICK + (255,))
    write_png(os.path.join(OUT, "steady-bar.png"), img)


# --------------------------------------------------------------------------- #
# hearts.png -- what a side has left to lose: a heart, full and hollow
#
# Twelve pixels, drawn at 3x on the heading. Full is the red of the box's light, dark
# at the rim and lit at the upper left where the lamp is; hollow is the same shape as
# an outline in the heading's grey, so a spent heart is still counted.
# --------------------------------------------------------------------------- #

HEART_SIZE = 12
HEART_SS = 3
HEART_RED = (168, 34, 40)
HEART_RED_D = (86, 12, 20)
HEART_RED_L = (232, 104, 96)
HEART_RIM = (14, 6, 10)
HEART_HOLLOW = (96, 88, 92)


def _heart_inside(u, v):
    """Signed: inside a heart drawn in a box from -1 to 1, positive in."""
    # Two lobes and a point: the classic implicit heart, scaled to fit the frame.
    x, y = u * 1.15, -v * 1.15 + 0.15
    f = (x * x + y * y - 1.0) ** 3 - x * x * y * y * y
    return -f


def build_hearts():
    img = new_image(HEART_SIZE * 2, HEART_SIZE)
    S = HEART_SS
    for frame in range(2):
        cells = []
        for Y in range(HEART_SIZE * S):
            row = []
            v = ((Y + 0.5) / S) / HEART_SIZE * 2.0 - 1.0
            for X in range(HEART_SIZE * S):
                u = ((X + 0.5) / S) / HEART_SIZE * 2.0 - 1.0
                d = _heart_inside(u, v)
                if d <= 0.0:
                    row.append(None)
                    continue
                if frame == 1:
                    # Hollow: only the rim.
                    if d < 0.25:
                        row.append(HEART_HOLLOW + (255,))
                    else:
                        row.append(None)
                    continue
                if d < 0.14:
                    row.append(HEART_RIM + (255,))
                    continue
                # Lit at the upper left, dark toward the lower right.
                t = 0.5 + 0.5 * (-(u + 0.2) - (v + 0.3)) / 1.6
                c = lerp_color(HEART_RED_D + (255,), HEART_RED + (255,), min(1.0, max(0.0, t * 1.4)))
                if u < -0.15 and v < -0.2 and d > 0.5:
                    c = lerp_color(c, HEART_RED_L + (255,), 0.55)
                row.append(c)
            cells.append(row)
        _downsample_into(img, frame * HEART_SIZE, cells, HEART_SIZE, S)
    write_png(os.path.join(OUT, "hearts.png"), img)


# --------------------------------------------------------------------------- #
# hand-sheet.png -- the player's own arm, 5 frames of 96x96, curled hand to open hand
#
# Seen from the side and a little above, modelled on a photo of a hand doing this:
# forearm up from the bottom-right, wrist turned, fingers out to the left stacked one
# above the next, thumb along the top. Frame 0 is curled like a hand resting on a
# table, frame 4 is open. It is not drawn, it is modelled and lit: a height field (tubes
# for the arm and fingers, bumps for knuckles, ridges for tendons, nails set in) shaded
# by its angle to the lamp, then rendered at 3x and averaged down so it looks like a
# photo shrunk to pixels. The lamp is over the box, so the fingertips are the bright end.
# --------------------------------------------------------------------------- #

HAND_HW, HAND_HH = 96, 96
HAND_REACH = 5

#: Rendered at this many times the frame size and averaged down.
HAND_SS = 3

HAND_SKIN      = (232, 196, 172)        # albedo: light, a little pink
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
HAND_AMBIENT   = (0.30, 0.28, 0.32)
HAND_KEY       = (1.00, 0.92, 0.80)

# The forearm line, elbow (off the bottom-right corner) to wrist, and the back-of-hand
# line, wrist to top. They are not the same line: the wrist turns.
HAND_ELBOW = (100.0, 112.0)
HAND_WRIST = (62.0, 58.0)
HAND_TOP = (52.0, 22.0)

#: The middle fingertip in the open frame, checked when built. HandSprite.Fingertip anchors by it.
HAND_FINGERTIP = (17, 25)

# Per finger: knuckle position on the left edge of the hand, direction (x right, y down),
# and open length. Index first.
HAND_FINGERS = (
    ((44.5, 27.0), (-0.96, -0.28), 26.0),
    ((43.5, 32.0), (-1.00, -0.08), 28.0),
    ((44.0, 37.0), (-0.99, 0.12), 26.0),
    ((46.5, 42.0), (-0.95, 0.30), 21.0),
)

#: The three segments of a finger as fractions of its length, and the radius at each joint.
HAND_SEGMENTS = (0.44, 0.31, 0.25)
HAND_RADII = (3.0, 2.7, 2.45, 2.0)

#: How far the middle and last segments bend toward the palm, in degrees, open and curled.
HAND_BEND_OPEN = (10.0, 22.0)
HAND_BEND_CURLED = (34.0, 58.0)

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
        """A nail set into the end of a finger: a flattened oval with a rim of skin and a cuticle line."""
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
    tx_, ty_ = HAND_TOP
    length = math.hypot(wx - ex, wy - ey)
    ux, uy = (wx - ex) / length, (wy - ey) / length      # along the forearm, toward the box
    nx, ny = -uy, ux                                     # across it
    if nx < 0:
        nx, ny = -nx, -ny

    # The forearm, a flattened tube up to the wrist, and the back of the hand, a flatter
    # one from the wrist to the top.
    f.capsule(ex, ey, wx, wy, 11.5, 7.0, 0.52, HAND_T_SKIN)
    f.capsule(wx, wy, tx_, ty_, 8.0, 6.8, 0.42, HAND_T_SKIN)
    # The knuckle ridge along the left edge of the hand, where the fingers come off it.
    f.ridge(HAND_FINGERS[0][0][0] + 2.0, HAND_FINGERS[0][0][1], HAND_FINGERS[3][0][0] + 2.0, HAND_FINGERS[3][0][1], 2.2, 0.5 + 0.6 * curl)
    # The wrist bone, the little knob on the outside of the wrist.
    f.bump(wx + 5.0, wy - 1.0, 2.2, 0.55)

    # Fingers: three segments each, off the left edge, each bending a little more toward
    # the palm than the one before, and more when the hand is curled.
    tips, nails = [], []
    bend_mid = HAND_BEND_OPEN[0] + (HAND_BEND_CURLED[0] - HAND_BEND_OPEN[0]) * curl
    bend_end = HAND_BEND_OPEN[1] + (HAND_BEND_CURLED[1] - HAND_BEND_OPEN[1]) * curl
    for n_, ((bx, by), (dx, dy), flen) in enumerate(HAND_FINGERS):
        reach = flen * (0.72 + 0.28 * openness)
        thin = 0.92 if n_ == 3 else 1.0
        joints = [(bx, by)]
        dirs = [(dx, dy)]
        x, y = bx, by
        for s, seg in enumerate(HAND_SEGMENTS):
            d = dirs[-1]
            if s == 1:
                d = _rot(d[0], d[1], bend_mid)
            elif s == 2:
                d = _rot(d[0], d[1], bend_end)
            dirs.append(d)
            x, y = x + d[0] * reach * seg, y + d[1] * reach * seg
            joints.append((x, y))
        for s in range(3):
            (x0, y0), (x1, y1) = joints[s], joints[s + 1]
            f.capsule(x0, y0, x1, y1, HAND_RADII[s] * thin, HAND_RADII[s + 1] * thin, 0.9, HAND_T_SKIN)
        f.bump(bx, by, 2.2 * thin, 0.4 + 0.9 * curl)
        for s in (1, 2):
            jx, jy = joints[s]
            d = dirs[s]
            f.bump(jx, jy, 1.6 * thin, 0.2 + 0.5 * curl)
            f.ridge(jx + d[1] * 2.4 * thin, jy - d[0] * 2.4 * thin, jx - d[1] * 2.4 * thin, jy + d[0] * 2.4 * thin, 0.42, -0.4, taper=False)
        tips.append(joints[3])
        if openness > 0.25:
            (x2, y2), (x3, y3) = joints[2], joints[3]
            d = dirs[3]
            seg = math.hypot(x3 - x2, y3 - y2)
            nails.append((x2 + d[0] * seg * 0.58, y2 + d[1] * seg * 0.58, d[0], d[1], seg * 0.42, HAND_RADII[3] * thin * 0.62))

    # The thumb along the top edge, two segments, the second bending in over the index
    # as the hand curls.
    tbx, tby = 57.0, 34.0
    tdx, tdy = _norm3h((-0.30, -0.95, 0.0))[:2]
    tlen = 15.0 + 2.0 * openness
    tmx, tmy = tbx + tdx * tlen * 0.55, tby + tdy * tlen * 0.55
    d2 = _rot(tdx, tdy, -18.0 - 30.0 * curl)
    ttx, tty = tmx + d2[0] * tlen * 0.45, tmy + d2[1] * tlen * 0.45
    f.capsule(tbx, tby, tmx, tmy, 3.7, 3.2, 0.85, HAND_T_SKIN, lift=-0.6)
    f.capsule(tmx, tmy, ttx, tty, 3.2, 2.4, 0.85, HAND_T_SKIN, lift=-0.6)
    f.bump(tmx, tmy, 1.7, 0.25 + 0.4 * curl)
    if openness > 0.25:
        seg = tlen * 0.45
        nails.append((tmx + d2[0] * seg * 0.6, tmy + d2[1] * seg * 0.6, d2[0], d2[1], seg * 0.4, 2.4 * 0.62))

    # Tendons up the back of the hand to each knuckle, and the crease across the wrist.
    for (bx, by), _, _ in HAND_FINGERS:
        f.ridge(wx - 2.0, wy - 3.0, bx + 3.0, by, 0.85, 0.26)
    hx, hy = tx_ - wx, ty_ - wy
    hl = math.hypot(hx, hy) or 1.0
    px_, py_ = -hy / hl, hx / hl
    for i in (-1.0, 1.6):
        f.ridge(wx + hx / hl * i - px_ * 7.0, wy + hy / hl * i - py_ * 7.0, wx + hx / hl * i + px_ * 7.0, wy + hy / hl * i + py_ * 7.0, 0.5, -0.35)

    for cx_, cy_, dx, dy, ln, wd in nails:
        f.nail(cx_, cy_, dx, dy, ln, wd)

    # The cuff of a sleeve over the near end of the forearm.
    cuff_cells = {}
    for Y in range(f.H):
        py = (Y + 0.5) / S
        for X in range(f.W):
            px = (X + 0.5) / S
            sp = (px - ex) * ux + (py - ey) * uy
            ap = (px - ex) * nx + (py - ey) * ny
            if -16.0 <= sp <= 24.0 and abs(ap) <= 15.0:
                rr = 11.5 + (7.0 - 11.5) * max(0.0, sp / length)
                rr += 1.4
                if abs(ap) >= rr:
                    continue
                z = math.sqrt(rr * rr - ap * ap) * 0.52 + 0.6
                for fold in (-7.5, 0.5, 8.0):
                    z += 0.35 * math.exp(-((ap - fold - 1.2 * math.sin(sp * 0.25)) / 1.1) ** 2)
                if sp > 22.2:
                    z += 0.5
                f.h[Y][X] = z
                f.tag[Y][X] = HAND_T_CLOTH
                cuff_cells[(X, Y)] = sp

    # Now light it.
    ZS = 1.6
    out = [[[0, 0, 0, 0] for _ in range(f.W)] for _ in range(f.H)]
    noise = [[rng.uniform(-1.0, 1.0) for _ in range(f.W // 3 + 2)] for _ in range(f.H // 3 + 2)]

    def hat(X, Y):
        if 0 <= X < f.W and 0 <= Y < f.H:
            v = f.h[Y][X]
            return v if v > 0.0 else -3.0
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

            ao = 1.0
            for ddx, ddy in ((3, 0), (-3, 0), (0, 3), (0, -3), (5, 0), (-5, 0), (0, 5), (0, -5)):
                nb = hat(X + ddx * S // 2, Y + ddy * S // 2)
                if nb > z + 0.6:
                    ao -= 0.09 * min(1.0, (nb - z - 0.6) / 1.5)
            ao = max(0.45, ao)

            s = ((px - ex) * ux + (py - ey) * uy) / length
            reachlit = 0.55 + 0.45 * max(0.0, min(1.0, s / 1.6)) ** 1.1
            tag = f.tag[Y][X]

            if tag == HAND_T_CLOTH:
                sp = cuff_cells.get((X, Y), 0.0)
                alb = HAND_CLOTH
                if sp > 22.2:
                    alb = HAND_CLOTH_L
                c = hand_shade(alb, n, 0.0, 0.0, ao * (0.5 + 0.5 * max(0.0, min(1.0, (sp + 16.0) / 38.0))))
            else:
                alb = HAND_SKIN
                nz = noise[Y // 3][X // 3] * 0.5 + noise[(Y // 3 + 1) % len(noise)][(X // 3 + 1) % len(noise[0])] * 0.25
                alb = tuple(max(0, min(255, int(alb[i] * (1.0 + 0.085 * nz)))) for i in range(3))
                for (bx, by), _, _ in HAND_FINGERS:
                    kk = math.exp(-((px - bx) ** 2 + (py - by) ** 2) / 9.0)
                    alb = lerp_color(alb + (255,), HAND_KNUCKLE + (255,), 0.5 * kk)[:3]
                if tag == HAND_T_NAIL:
                    a_, e_ = f.tint.get((X, Y), (0.0, 0.0))
                    alb = HAND_NAIL
                    if a_ > 0.55:
                        alb = lerp_color(HAND_NAIL + (255,), HAND_NAIL_TIP + (255,), min(1.0, (a_ - 0.55) / 0.35))[:3]
                    if a_ < -0.62 and e_ > 0.72:
                        alb = HAND_CUTICLE
                else:
                    # A vein or two, up the back of the hand from the wrist.
                    for off in (-3.0, 2.5):
                        vx0, vy0 = wx + px_ * off * 0.6, wy + py_ * off * 0.6
                        vx1, vy1 = tx_ + px_ * off * 1.3 + 2.0, ty_ + py_ * off * 1.3 + 6.0
                        lx, ly = vx1 - vx0, vy1 - vy0
                        t = max(0.0, min(1.0, ((px - vx0) * lx + (py - vy0) * ly) / (lx * lx + ly * ly)))
                        dv = math.hypot(px - (vx0 + lx * t), py - (vy0 + ly * t)) + 0.6 * math.sin(t * 9.0 + off)
                        alb = lerp_color(alb + (255,), HAND_VEIN + (255,), 0.22 * math.exp(-(dv / 0.8) ** 2) * math.sin(math.pi * t))[:3]
                ndl = max(0.0, n[0] * HAND_LAMP[0] + n[1] * HAND_LAMP[1] + n[2] * HAND_LAMP[2])
                thin = max(0.0, 1.0 - z / 2.4)
                red = 0.10 * thin * (1.0 - ndl) * reachlit
                spec = 0.10 if tag == HAND_T_NAIL else 0.035
                c = hand_shade(alb, n, spec, red, ao)
                glow = max(0.0, (s - 1.15) / 0.6)
                if glow > 0:
                    c = list(lerp_color(tuple(c) + (255,), HAND_RED + (255,), min(0.30, glow * 0.30))[:3])

            c = [int(v * reachlit) for v in c]
            out[Y][X] = [c[0], c[1], c[2], 255]

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
    tip = None
    for fr in range(HAND_REACH):
        tip = build_hand_frame(img, fr * HAND_HW, fr / (HAND_REACH - 1), random.Random(9090))
    # The open frame's middle fingertip is what HandSprite anchors by; if the geometry
    # moves it, HAND_FINGERTIP and HandSprite.Fingertip have to move with it.
    print("  middle fingertip, open frame: (%.1f, %.1f)" % tip)
    assert abs(tip[0] - HAND_FINGERTIP[0]) <= 1.5 and abs(tip[1] - HAND_FINGERTIP[1]) <= 1.5, \
        ("the fingertip moved", tip, HAND_FINGERTIP)
    write_png(os.path.join(OUT, "hand-sheet.png"), img)


# --------------------------------------------------------------------------- #
# box-lid.png -- the box's mouth shut: the plate over the opening, 76x76
#
# Only seen in the close-up, where two jaws of the same concrete as the shell draw back
# into the rim. This is the plate they are cut from, the exact size of the opening in
# black-box.png, with the seam lit on the top jaw and dark on the bottom one.
# --------------------------------------------------------------------------- #

LID_W, LID_H = 76, 76
LID_SHELL = (32, 30, 37)
LID_SHELL_LT = (58, 55, 65)
LID_SHELL_DK = (13, 12, 16)
LID_SEAM = (4, 3, 6)

#: The radius of the opening's corners in black-box.png, which the plate matches.
LID_CORNER = 12


def build_box_lid(seed=4242):
    rng = random.Random(seed)
    img = new_image(LID_W, LID_H)
    for y in range(LID_H):
        for x in range(LID_W):
            # Poured concrete: grain, a little darker toward the seam and the edges.
            g = rng.uniform(-1.0, 1.0)
            c = LID_SHELL
            if g > 0.82:
                c = LID_SHELL_LT
            elif g < -0.75:
                c = LID_SHELL_DK
            # Rounded corners to match the opening, dark just inside the radius.
            cx_ = min(x, LID_W - 1 - x)
            cy_ = min(y, LID_H - 1 - y)
            corner = 0.0
            if cx_ < LID_CORNER and cy_ < LID_CORNER:
                corner = math.hypot(LID_CORNER - cx_, LID_CORNER - cy_) - LID_CORNER
            if corner > 0.5:
                continue
            edge = min(x, LID_W - 1 - x, y, LID_H - 1 - y)
            if edge < 2 or corner > -1.5:
                c = lerp_color(c + (255,), LID_SHELL_DK + (255,), 0.6)[:3]
            # The seam: top jaw's lower edge lit, bottom jaw's upper edge in shadow, dark between.
            if y == LID_H // 2 - 1:
                c = lerp_color(c + (255,), LID_SHELL_LT + (255,), 0.7)[:3]
            elif y == LID_H // 2:
                c = LID_SEAM
            elif y == LID_H // 2 + 1:
                c = lerp_color(c + (255,), LID_SHELL_DK + (255,), 0.8)[:3]
            put(img, x, y, c + (255,))
    # A few chips out of the seam, where the jaws have met hard before.
    for _ in range(9):
        x = rng.randrange(4, LID_W - 4)
        y = LID_H // 2 + rng.choice((-2, 2))
        put(img, x, y, LID_SHELL_DK + (255,))
        put(img, x + 1, y, LID_SHELL_DK + (255,))
    write_png(os.path.join(OUT, "box-lid.png"), img)



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
    build_second_opponent_sheet()
    build_hand_sheet()
    build_token_sheet()
    build_item_sheet()
    build_sight_sheet()
    build_ember_sheet()
    build_steady_bar()
    build_hearts()
    build_box_lid()
    print("Done.")
