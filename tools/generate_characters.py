"""
Walking sprites for The Black Box: the two people the player can be, and Serenity as she
stands around the lobby.

    python tools/generate_characters.py

Every figure is 32x48, seen from slightly above, in four directions (down, left, right, up),
with an idle frame and a four-frame walk: five columns, the same five Serenity's table sheet
has. The player's sheets also come in three versions stacked under each other (nothing, a
scarf, a cap), so a sheet is 5 x 12 frames.

The player's two sheets are painted in key colours: the first row of each ramp in
character-palettes.png. The game swaps those keys for whichever ramps were picked on the
customization screen, so hair, outfit and accent can be any of the presets without a sheet
per combination. Serenity is not drawn here: she is cut from the character sheet I made of her
(tools/source/serenity-sheet.png), in her own colours, and never swapped.

Like generate_assets.py it only needs CPython. It borrows that script's PNG writer and shape
functions.
"""

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generate_assets import OUT, ROOT, new_image, read_png, write_png, _sd_box, _sd_line, _sd_poly  # noqa: E402

FRAME_W = 32
FRAME_H = 48
COLUMNS = 5            # idle, then four walk frames
DIRECTIONS = ("down", "left", "right", "up")
ACCESSORIES = ("none", "scarf", "cap")

#: Subsamples per pixel side when deciding what covers a pixel.
SS = 3

#: Where the light comes from: up, left and toward the viewer, the same corner as the lamp on the table.
LIGHT = (-0.50, -0.75, 0.90)
_LN = math.sqrt(sum(c * c for c in LIGHT))
LIGHT = tuple(c / _LN for c in LIGHT)
#: What a flat surface facing the viewer gets, so flat lands on the middle of a ramp.
NDL_FLAT = LIGHT[2]

#: How far the edge of a shape bends away from the viewer. Bigger is rounder.
EDGE_TILT = 0.85


# --------------------------------------------------------------------------- #
# Ramps. Dark to light. The first preset of hair, outfit and accent is the key the player's
# sheets are painted in, so those three rows must never be used for anything else.
# --------------------------------------------------------------------------- #

HAIR_PRESETS = (
    ("INK",       ((17, 15, 21), (29, 26, 34), (45, 41, 51), (69, 64, 77))),
    ("ASH BROWN", ((34, 26, 24), (56, 43, 38), (83, 65, 55), (113, 91, 77))),
    ("CHESTNUT",  ((41, 22, 16), (71, 39, 25), (105, 61, 37), (141, 89, 55))),
    ("AUBURN",    ((49, 18, 15), (89, 34, 25), (131, 57, 37), (171, 87, 55))),
    ("BLEACHED",  ((99, 89, 71), (141, 129, 101), (185, 171, 137), (223, 211, 177))),
    ("GREY",      ((61, 59, 63), (97, 95, 101), (137, 135, 141), (179, 177, 183))),
)

OUTFIT_PRESETS = (
    ("CANVAS",   ((35, 37, 29), (57, 61, 45), (83, 87, 63), (113, 117, 85))),
    ("SLATE",    ((29, 33, 41), (47, 53, 65), (71, 79, 95), (101, 111, 129))),
    ("OXBLOOD",  ((41, 19, 21), (67, 31, 33), (97, 47, 47), (129, 71, 67))),
    ("CHARCOAL", ((23, 22, 25), (39, 37, 41), (59, 57, 63), (85, 83, 91))),
    ("BONE",     ((97, 91, 83), (141, 133, 121), (183, 175, 161), (215, 207, 193))),
    ("RUST",     ((53, 29, 19), (89, 49, 29), (127, 73, 41), (163, 103, 59))),
)

ACCENT_PRESETS = (
    ("EMBER",  ((97, 23, 19), (161, 45, 33), (215, 85, 59))),
    ("AMBER",  ((111, 65, 21), (177, 111, 41), (227, 161, 77))),
    ("BONE",   ((121, 113, 105), (177, 169, 157), (223, 215, 201))),
    ("TEAL",   ((23, 59, 61), (41, 97, 97), (77, 141, 137))),
    ("MOSS",   ((41, 59, 31), (67, 93, 49), (103, 131, 73))),
    ("VIOLET", ((49, 33, 61), (81, 57, 99), (121, 91, 141))),
)

# Everything else is fixed per figure.
SKIN_OLIVE = ((92, 70, 58), (142, 112, 92), (188, 154, 128), (218, 188, 160))
SKIN_BROWN = ((58, 36, 28), (98, 64, 46), (138, 96, 68), (172, 128, 94))
PANTS = ((22, 20, 26), (36, 34, 42), (52, 50, 60))
SHOES = ((14, 12, 14), (28, 25, 26), (44, 40, 40))
EYE = (14, 10, 12)
LIP = (124, 74, 70)


# --------------------------------------------------------------------------- #
# Shapes. Each part is a signed distance (positive inside, in art pixels), a material, and a
# bounding box so the rasteriser can skip it for pixels it cannot touch.
# --------------------------------------------------------------------------- #

class Part:
    __slots__ = ("sdf", "mat", "bbox", "bevel", "tone", "texture", "contour", "name")

    def __init__(self, sdf, mat, bbox, bevel=2.2, tone=0.0, texture=None, contour=False, name=""):
        self.sdf = sdf
        self.mat = mat
        self.bbox = bbox
        self.bevel = bevel
        self.tone = tone
        self.texture = texture
        self.contour = contour
        self.name = name


def ellipse(cx, cy, rx, ry, mat, **kw):
    k = min(rx, ry)

    def sdf(x, y):
        return (1.0 - math.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)) * k
    return Part(sdf, mat, (cx - rx, cy - ry, cx + rx, cy + ry), **kw)


def capsule(x0, y0, x1, y1, width, mat, **kw):
    h = width / 2.0
    return Part(lambda x, y: _sd_line(x, y, x0, y0, x1, y1, width), mat,
                (min(x0, x1) - h, min(y0, y1) - h, max(x0, x1) + h, max(y0, y1) + h), **kw)


def poly(points, mat, **kw):
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    return Part(lambda x, y: _sd_poly(x, y, points), mat, (min(xs), min(ys), max(xs), max(ys)), **kw)


def box(x0, y0, x1, y1, mat, corner=0.0, **kw):
    return Part(lambda x, y: _sd_box(x, y, x0, y0, x1, y1, corner), mat, (x0, y0, x1, y1), **kw)


def clip(part, top=None, bottom=None, left=None, right=None):
    """Cuts a part off at straight lines, keeping what is between them."""
    inner = part.sdf
    x0, y0, x1, y1 = part.bbox

    def sdf(x, y):
        d = inner(x, y)
        if top is not None:
            d = min(d, y - top)
        if bottom is not None:
            d = min(d, bottom - y)
        if left is not None:
            d = min(d, x - left)
        if right is not None:
            d = min(d, right - x)
        return d
    part.sdf = sdf
    part.bbox = (x0 if left is None else max(x0, left), y0 if top is None else max(y0, top),
                 x1 if right is None else min(x1, right), y1 if bottom is None else min(y1, bottom))
    return part


def shifted(points, dx=0.0, dy=0.0):
    return [(x + dx, y + dy) for x, y in points]


def mirrored(points):
    """Mirrors a list of points across the middle of the frame."""
    return [(FRAME_W - x, y) for x, y in points]


def flip_part(part):
    """Mirrors a part across the middle of the frame. Right-facing frames are the left-facing ones flipped this way, lit the same."""
    inner = part.sdf
    x0, y0, x1, y1 = part.bbox
    part.sdf = lambda x, y: inner(FRAME_W - x, y)
    part.bbox = (FRAME_W - x1, y0, FRAME_W - x0, y1)
    return part


# --------------------------------------------------------------------------- #
# Poses. Frame 0 stands still; 1 to 4 walk: right foot forward, passing, left foot forward, passing.
# Passing frames are one pixel higher, which is the bounce.
# --------------------------------------------------------------------------- #

#: (bob, step, swing) per column. Step +1 is the right leg forward; swing is the arms, opposite to it.
POSES = ((0, 0, 0), (0, 1, -1), (-1, 0, 0), (0, -1, 1), (-1, 0, 0))


# --------------------------------------------------------------------------- #
# The figures
# --------------------------------------------------------------------------- #

def legs_front(parts, step, toward_viewer, top, foot):
    """Two legs and shoes, seen from the front or the back. The leg stepping toward the viewer is lower and drawn last."""
    lx, rx = 13.4, 18.6          # the figure's right leg is on the viewer's left from the front
    if not toward_viewer:
        lx, rx = rx, lx          # and on the viewer's right from behind
    right_drop = 1.0 if step > 0 else (-1.5 if step < 0 else 0.0)
    left_drop = 1.0 if step < 0 else (-1.5 if step > 0 else 0.0)
    if not toward_viewer:
        right_drop, left_drop = -right_drop if step else 0.0, -left_drop if step else 0.0
        # From behind, the leg going forward goes up the screen, and away.
        right_drop = -1.5 if step > 0 else (1.0 if step < 0 else 0.0)
        left_drop = -1.5 if step < 0 else (1.0 if step > 0 else 0.0)
    legs = [(rx, right_drop), (lx, left_drop)]
    # Farthest first: the one higher up the screen is further away.
    legs.sort(key=lambda leg: leg[1])
    for x, drop in legs:
        fy = foot + drop
        parts.append(capsule(x, top, x, fy - 1.0, 4.3, "pants", bevel=1.8, name="leg"))
        parts.append(ellipse(x, fy, 2.5, 1.6, "shoes", bevel=1.2, name="shoe"))


def legs_side(parts, step, top, foot, hip_x):
    """Two legs seen from the side, facing left. The near leg is the left one."""
    near_fwd = step < 0          # the left leg forward
    far_fwd = step > 0
    reach = 3.4
    if step == 0:
        near = (hip_x, foot)
        far = (hip_x + 0.8, foot)
    else:
        near = (hip_x - reach, foot) if near_fwd else (hip_x + reach, foot - 0.6)
        far = (hip_x - reach, foot) if far_fwd else (hip_x + reach, foot - 0.6)
    for (fx, fy), tone, name in ((far, -0.28, "far-leg"), (near, 0.0, "leg")):
        knee = ((hip_x + fx) / 2.0 + 0.6, (top + fy) / 2.0)
        parts.append(capsule(hip_x, top, knee[0], knee[1], 4.2, "pants", bevel=1.8, tone=tone, name=name))
        parts.append(capsule(knee[0], knee[1], fx, fy - 1.0, 3.9, "pants", bevel=1.8, tone=tone, name=name))
        parts.append(ellipse(fx - 0.9, fy, 2.9, 1.5, "shoes", bevel=1.1, tone=tone, name="shoe"))


def figure(spec, direction, column, accessory):
    """Every part of one frame, in the order they are painted, and the details painted over them."""
    bob, step, swing = POSES[column]
    side = direction in ("left", "right")
    front = direction == "down"
    back = direction == "up"
    parts = []
    details = []
    b = bob
    outfit = spec["outfit"]
    hair = spec["hair"]
    coat = outfit == "coat"
    leg_top = 33.0
    foot = 44.4

    if side:
        # ---- facing left; flipped for right at the end ----
        # The far arm, behind the body, swinging the other way.
        hand_far = (16.4 + swing * 2.4, 30.8 + b)
        parts.append(capsule(16.8, 21.6 + b, hand_far[0], hand_far[1] - 1.0, 3.0, "outfit",
                             bevel=1.5, tone=-0.3, name="far-arm"))
        parts.append(ellipse(hand_far[0], hand_far[1], 1.4, 1.5, "skin", bevel=1.0, tone=-0.3, name="hand"))
        legs_side(parts, step, leg_top + b if not coat else 36.0 + b, foot, 16.0)
        parts.append(box(12.6, 30.6 + b, 19.8, 34.0 + b, "pants", corner=1.2, bevel=1.6, name="hips"))
        if coat:
            parts.append(poly(shifted([(12.0, 20.6), (19.6, 20.6), (20.6, 23.0), (20.8, 37.4), (11.6, 37.4), (11.4, 23.0)], dy=b),
                              "outfit", bevel=2.4, name="torso"))
        else:
            parts.append(poly(shifted([(12.4, 20.6), (19.4, 20.6), (20.2, 23.0), (19.8, 31.8), (12.6, 31.8), (11.8, 23.0)], dy=b),
                              "outfit", bevel=2.4, name="torso"))
        if outfit == "jacket":
            parts.append(ellipse(18.4, 21.2 + b, 2.4, 1.9, "outfit", bevel=1.4, tone=-0.1, contour=True, name="hood"))
            parts.append(box(11.9, 29.8 + b, 19.9, 30.9 + b, "accent", bevel=0.8, name="hem"))
        if outfit == "coat":
            parts.append(box(11.8, 29.6 + b, 20.4, 30.9 + b, "accent", bevel=0.8, name="belt"))
            parts.append(poly(shifted([(12.6, 19.0), (16.4, 19.2), (16.0, 22.0), (12.2, 21.6)], dy=b), "accent", bevel=1.0, name="collar"))
        parts.append(box(15.0, 16.6 + b, 17.6, 20.4 + b, "skin", bevel=1.2, tone=-0.25, name="neck"))
        if accessory == "scarf":
            parts.append(poly(shifted([(13.6, 18.0), (18.2, 18.0), (18.8, 20.8), (13.0, 21.4)], dy=b), "accent", bevel=1.4, contour=True, name="scarf"))
            parts.append(capsule(13.8, 20.6 + b, 12.6, 26.4 + b, 2.4, "accent", bevel=1.2, contour=True, name="scarf"))
        parts.append(ellipse(15.6, 12.6 + b, 4.6, 5.5, "skin", bevel=2.6, name="head"))
        parts.append(ellipse(11.2, 13.8 + b, 0.95, 0.9, "skin", bevel=0.8, name="head"))      # the nose
        if hair == "short":
            parts.append(ellipse(17.2, 13.4 + b, 1.1, 1.4, "skin", bevel=0.8, tone=-0.1, name="ear"))
            parts.append(clip(ellipse(17.0, 10.4 + b, 4.9, 4.7, "hair", bevel=2.0, texture="strands", contour=True, name="hair"), bottom=11.6 + b))
            parts.append(poly(shifted([(16.2, 9.0), (21.0, 10.0), (20.8, 15.4), (19.0, 16.8), (17.8, 15.6), (18.6, 12.4)], dy=b),
                              "hair", bevel=1.6, texture="strands", contour=True, name="hair"))
            parts.append(poly(shifted([(11.6, 9.0), (14.8, 7.8), (14.8, 10.2), (13.0, 9.8), (12.0, 10.6)], dy=b),
                              "hair", bevel=1.2, texture="strands", name="hair"))
        else:
            parts.append(clip(ellipse(16.8, 10.6 + b, 4.9, 4.8, "hair", bevel=2.0, texture="strands", contour=True, name="hair"), bottom=11.0 + b))
            parts.append(poly(shifted([(11.8, 8.6), (15.6, 7.8), (14.6, 10.0), (12.2, 10.4)], dy=b),
                              "hair", bevel=1.2, texture="strands", name="hair"))
            parts.append(poly(shifted([(15.8, 9.0), (20.8, 10.2), (20.6, 15.6), (18.6, 16.6), (17.4, 14.6)], dy=b),
                              "hair", bevel=1.6, texture="strands", contour=True, name="hair"))
            parts.append(ellipse(20.6, 15.0 + b, 1.9, 1.9, "hair", bevel=1.4, contour=True, name="hair"))
            parts.append(capsule(20.8, 16.0 + b, 21.4, 22.6 + b, 2.2, "hair", bevel=1.1, texture="strands", contour=True, name="hair"))
        # The near arm, in front of everything else.
        hand = (15.6 - swing * 2.4, 30.8 + b)
        parts.append(capsule(15.6, 21.6 + b, hand[0], hand[1] - 1.0, 3.1, "outfit", bevel=1.5, contour=True, name="arm"))
        parts.append(ellipse(hand[0], hand[1], 1.4, 1.6, "skin", bevel=1.0, name="hand"))
        if accessory == "cap":
            parts.append(clip(ellipse(16.0, 10.0 + b, 5.6, 4.9, "accent", bevel=2.0, contour=True, name="cap"), bottom=10.4 + b))
            parts.append(box(10.4, 9.0 + b, 21.4, 11.4 + b, "accent", corner=0.8, bevel=1.0, tone=-0.22, texture="rib", name="cap"))
        # Face: one eye, a mouth.
        details.append((12, 12 + b, "eye"))
        details.append((12, 16 + b, "lip"))
        if direction == "right":
            parts = [flip_part(p) for p in parts]
            details = [(FRAME_W - 1 - x, y, what) for x, y, what in details]
        return parts, details

    # ---- facing the viewer, or facing away ----
    # Arms swing toward or away from the viewer, which is mostly up and down the screen.
    def arm(x_shoulder, x_hand, swing_dir, outer):
        drop = swing_dir * 1.0
        hx, hy = x_hand - outer * 0.0, 31.4 + b + drop
        parts.append(capsule(x_shoulder, 21.8 + b, hx, hy - 1.0, 3.2, "outfit", bevel=1.5, contour=True, name="arm"))
        parts.append(ellipse(hx, hy, 1.5, 1.6, "skin", bevel=1.0, name="hand"))

    legs_front(parts, step, front, (leg_top if not coat else 36.0) + b, foot)
    parts.append(box(10.6, 30.6 + b, 21.4, 34.2 + b, "pants", corner=1.4, bevel=1.8, name="hips"))

    if coat:
        parts.append(poly(shifted([(9.6, 20.6), (22.4, 20.6), (23.0, 22.4), (23.0, 37.6), (9.0, 37.6), (9.0, 22.4)], dy=b),
                          "outfit", bevel=2.8, name="torso"))
    else:
        parts.append(poly(shifted([(9.6, 20.6), (22.4, 20.6), (22.8, 22.2), (21.8, 31.8), (10.2, 31.8), (9.2, 22.2)], dy=b),
                          "outfit", bevel=2.8, name="torso"))

    if outfit == "jacket":
        if front:
            parts.append(box(14.6, 20.8 + b, 17.4, 30.2 + b, "accent", bevel=1.0, name="shirt"))
        parts.append(ellipse(16.0, 20.4 + b if front else 21.6 + b, 5.0 if back else 4.6, 1.8 if front else 2.4, "outfit",
                             bevel=1.4, tone=-0.12, contour=True, name="hood"))
    if coat:
        parts.append(box(9.2, 29.8 + b, 22.8, 31.1 + b, "accent", bevel=0.8, name="belt"))
        if front:
            parts.append(poly(shifted([(12.6, 18.8), (19.4, 18.8), (18.8, 21.8), (16.0, 22.6), (13.2, 21.8)], dy=b),
                              "accent", bevel=1.2, contour=True, name="collar"))
        else:
            parts.append(box(12.4, 18.8 + b, 19.6, 21.4 + b, "accent", corner=1.0, bevel=1.2, contour=True, name="collar"))

    parts.append(box(14.6, 16.6 + b, 17.4, 20.6 + b, "skin", bevel=1.2, tone=-0.3, name="neck"))

    # Arms: the forward swing drops the hand, the backward one lifts it.
    right_x, left_x = (10.0, 22.0) if front else (22.0, 10.0)
    arm(right_x, right_x - 0.4 if right_x < 16 else right_x + 0.4, -swing if front else swing, -1)
    arm(left_x, left_x + 0.4 if left_x > 16 else left_x - 0.4, swing if front else -swing, 1)

    if accessory == "scarf":
        parts.append(poly(shifted([(12.2, 17.8), (19.8, 17.8), (21.0, 20.4), (16.0, 21.8), (11.0, 20.4)], dy=b),
                          "accent", bevel=1.6, contour=True, name="scarf"))
        tail_x = 18.6 if front else 13.4
        parts.append(capsule(tail_x, 20.6 + b, tail_x + 0.4, 27.0 + b, 2.4, "accent", bevel=1.2, contour=True, name="scarf"))

    parts.append(ellipse(16.0, 12.6 + b, 4.9, 5.5, "skin", bevel=2.8, name="head"))
    if hair == "short":
        parts.append(ellipse(10.9, 13.4 + b, 1.0, 1.4, "skin", bevel=0.8, tone=-0.1, name="ear"))
        parts.append(ellipse(21.1, 13.4 + b, 1.0, 1.4, "skin", bevel=0.8, tone=-0.1, name="ear"))

    if back:
        if hair == "short":
            parts.append(poly(shifted([(10.8, 9.0), (21.2, 9.0), (21.4, 14.0), (20.0, 16.6), (18.0, 16.0), (16.0, 17.0),
                                       (14.0, 16.0), (12.0, 16.6), (10.6, 14.0)], dy=b), "hair", bevel=2.4, texture="strands", name="hair"))
            parts.append(clip(ellipse(16.0, 11.4 + b, 5.4, 5.2, "hair", bevel=2.4, texture="strands", name="hair"), bottom=12.0 + b))
        else:
            parts.append(ellipse(16.0, 11.8 + b, 5.3, 5.6, "hair", bevel=2.6, texture="strands", name="hair"))
            parts.append(ellipse(16.0, 17.2 + b, 2.3, 2.1, "hair", bevel=1.6, contour=True, name="hair"))
            parts.append(capsule(16.0, 18.4 + b, 16.0, 23.6 + b, 2.4, "hair", bevel=1.2, texture="strands", contour=True, name="hair"))
    else:
        if hair == "short":
            parts.append(clip(ellipse(16.0, 10.6 + b, 5.5, 4.7, "hair", bevel=2.2, texture="strands", contour=True, name="hair"), bottom=11.4 + b))
            parts.append(poly(shifted([(10.7, 9.4), (21.3, 9.4), (21.1, 11.6), (19.8, 11.0), (18.8, 12.6), (17.4, 11.2),
                                       (16.0, 12.8), (14.6, 11.2), (13.2, 12.4), (12.2, 11.0), (10.9, 12.6)], dy=b),
                              "hair", bevel=1.4, texture="strands", contour=True, name="hair"))
            parts.append(box(10.6, 10.2 + b, 11.7, 13.6 + b, "hair", bevel=0.8, name="hair"))
            parts.append(box(20.3, 10.2 + b, 21.4, 13.6 + b, "hair", bevel=0.8, name="hair"))
        else:
            parts.append(clip(ellipse(16.0, 10.4 + b, 5.6, 4.6, "hair", bevel=2.2, texture="strands", contour=True, name="hair"), bottom=11.0 + b))
            # Swept to one side, and down to the jaw either side of the face.
            parts.append(poly(shifted([(10.6, 8.6), (16.8, 8.6), (15.2, 10.4), (12.4, 11.8), (11.0, 12.6)], dy=b),
                              "hair", bevel=1.4, texture="strands", contour=True, name="hair"))
            parts.append(poly(shifted([(10.4, 9.6), (12.4, 10.6), (12.2, 15.6), (11.6, 17.6), (10.4, 16.8)], dy=b),
                              "hair", bevel=1.2, texture="strands", contour=True, name="hair"))
            parts.append(poly(mirrored(shifted([(10.4, 9.6), (12.4, 10.6), (12.2, 15.6), (11.6, 17.6), (10.4, 16.8)], dy=b)),
                              "hair", bevel=1.2, texture="strands", contour=True, name="hair"))

    if accessory == "cap":
        parts.append(clip(ellipse(16.0, 9.6 + b, 5.9, 4.8, "accent", bevel=2.2, contour=True, name="cap"), bottom=10.2 + b))
        parts.append(box(10.0, 8.8 + b, 22.0, 11.2 + b, "accent", corner=0.8, bevel=1.0, tone=-0.22, texture="rib", name="cap"))

    if front:
        # Two eyes, a nose in shadow, a mouth.
        details.append((13, 13 + b, "eye"))
        details.append((18, 13 + b, "eye"))
        details.append((15, 14 + b, ("skin", 1)))
        details.append((15, 16 + b, "lip"))
        details.append((16, 16 + b, "lip"))
        if outfit == "coat":
            details.append((16, 25 + b, ("accent", 2)))
            details.append((16, 28 + b, ("accent", 2)))
        if outfit == "jacket":
            details.append((14, 24 + b, ("outfit", 0)))
            details.append((17, 24 + b, ("outfit", 0)))
    return parts, details


# --------------------------------------------------------------------------- #
# Rasteriser
# --------------------------------------------------------------------------- #

def render_frame(parts, details, ramps):
    """Draws one frame and returns its rows of RGBA."""
    W, H = FRAME_W, FRAME_H
    # Which parts can touch which pixel, in paint order.
    candidates = [[[] for _ in range(W)] for _ in range(H)]
    for i, p in enumerate(parts):
        x0, y0, x1, y1 = p.bbox
        for py in range(max(0, int(math.floor(y0)) - 1), min(H, int(math.ceil(y1)) + 1)):
            for px in range(max(0, int(math.floor(x0)) - 1), min(W, int(math.ceil(x1)) + 1)):
                candidates[py][px].append(i)

    owner = [[-1] * W for _ in range(H)]
    half = (SS * SS) / 2.0
    for py in range(H):
        for px in range(W):
            cands = candidates[py][px]
            if not cands:
                continue
            counts = {}
            covered = 0
            for sy in range(SS):
                y = py + (sy + 0.5) / SS
                for sx in range(SS):
                    x = px + (sx + 0.5) / SS
                    top = -1
                    for i in cands:
                        if parts[i].sdf(x, y) > 0.0:
                            top = i
                    if top >= 0:
                        counts[top] = counts.get(top, 0) + 1
                        covered += 1
            if covered >= half:
                owner[py][px] = max(counts, key=lambda k: (counts[k], k))

    index = [[0] * W for _ in range(H)]
    for py in range(H):
        for px in range(W):
            i = owner[py][px]
            if i < 0:
                continue
            p = parts[i]
            ramp = ramps[p.mat]
            n = len(ramp)
            x, y = px + 0.5, py + 0.5
            d = p.sdf(x, y)
            e = 0.35
            gx = p.sdf(x + e, y) - p.sdf(x - e, y)
            gy = p.sdf(x, y + e) - p.sdf(x, y - e)
            gl = math.hypot(gx, gy) or 1.0
            # The SDF grows inward, so its gradient points in; the surface faces the other way.
            ox, oy = -gx / gl, -gy / gl
            tilt = EDGE_TILT * max(0.0, min(1.0, 1.0 - max(d, 0.0) / p.bevel))
            nx, ny = ox * tilt, oy * tilt
            nz = math.sqrt(max(0.0, 1.0 - nx * nx - ny * ny))
            ndl = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
            shade = 0.55 + 1.1 * (ndl - NDL_FLAT) + p.tone
            k = int(math.floor(shade * n))
            if p.texture == "strands" and 0 < k < n - 1:
                # Hair falls in strands: a few darker and lighter streaks down it.
                if (px * 7 + (py // 4) * 3) % 5 == 0:
                    k -= 1
                elif (px * 3 + py // 6) % 7 == 0:
                    k += 1
            elif p.texture == "rib" and k > 0 and px % 2 == 0:
                k -= 1
            if p.contour and d < 0.8:
                k -= 1
            index[py][px] = max(0, min(n - 1, k))

    # Selective outline: the silhouette goes darkest on the shadow side and one step down on the lit side.
    out = new_image(W, H)
    for py in range(H):
        for px in range(W):
            i = owner[py][px]
            if i < 0:
                continue
            k = index[py][px]
            open_left = px == 0 or owner[py][px - 1] < 0
            open_up = py == 0 or owner[py - 1][px] < 0
            open_right = px == W - 1 or owner[py][px + 1] < 0
            open_down = py == H - 1 or owner[py + 1][px] < 0
            if open_right or open_down:
                k = 0
            elif open_left or open_up:
                k = max(0, k - 1)
            c = ramps[parts[i].mat][k]
            out[py][px] = [c[0], c[1], c[2], 255]

    for x, y, what in details:
        if not (0 <= x < W and 0 <= y < H) or owner[y][x] < 0:
            continue
        owner_name = parts[owner[y][x]].name
        if what in ("eye", "lip"):
            # Only on the face itself, never painted over hair hanging in front of it.
            if owner_name != "head":
                continue
            c = EYE if what == "eye" else LIP
        else:
            mat, k = what
            if mat == "skin" and owner_name != "head":
                continue
            c = ramps[mat][k]
        out[y][x] = [c[0], c[1], c[2], 255]
    return out


def build_sheet(spec, ramps, accessories):
    """Every frame of one figure: COLUMNS across, one row per direction, per accessory."""
    rows = len(DIRECTIONS) * len(accessories)
    sheet = new_image(FRAME_W * COLUMNS, FRAME_H * rows)
    for a, accessory in enumerate(accessories):
        for d, direction in enumerate(DIRECTIONS):
            for c in range(COLUMNS):
                parts, details = figure(spec, direction, c, accessory)
                frame = render_frame(parts, details, ramps)
                oy = (a * len(DIRECTIONS) + d) * FRAME_H
                ox = c * FRAME_W
                for y in range(FRAME_H):
                    sheet[oy + y][ox:ox + FRAME_W] = frame[y]
    return sheet


# --------------------------------------------------------------------------- #
# The figures and their colours
# --------------------------------------------------------------------------- #

#: The key ramps the player's sheets are painted in: the first preset of each.
KEYS = dict(hair=HAIR_PRESETS[0][1], outfit=OUTFIT_PRESETS[0][1], accent=ACCENT_PRESETS[0][1])

FIGURES = (
    # The two the player can be. Painted in the keys, swapped in game.
    ("conscript-first", dict(hair="short", outfit="jacket"),
     dict(skin=SKIN_OLIVE, pants=PANTS, shoes=SHOES, **KEYS), ACCESSORIES),
    ("conscript-second", dict(hair="tied", outfit="coat"),
     dict(skin=SKIN_BROWN, pants=PANTS, shoes=SHOES, **KEYS), ACCESSORIES),
)


# --------------------------------------------------------------------------- #
# serenity-walker.png -- Serenity in the lobby, cut from her character sheet
#
# tools/source/serenity-sheet.png is the sheet I made of her: idle, walk, run, interact and
# hurt rows, every figure on the same flat dark backdrop. The lobby wants her standing and her
# four walking frames each way, so those are cut out, keyed off the backdrop, and sampled down
# into the same 32x48 frames as the conscripts, feet on the same row. The sheet only has her
# walking to the right, so walking left is those frames mirrored.
# --------------------------------------------------------------------------- #

SERENITY_SOURCE = os.path.join(ROOT, "tools", "source", "serenity-sheet.png")

#: Sheet pixels per art pixel. Her figures are about 130 sheet pixels tall, which comes out the
#: same height as the conscripts.
SERENITY_PITCH = 3.1

#: The two rows of the sheet the frames come from (top and bottom, in sheet pixels), and where
#: the figures start: the portrait and the row labels are left of it.
SERENITY_IDLE_ROW = (40, 200)
SERENITY_WALK_ROW = (220, 375)
SERENITY_FIGURES_LEFT = 400

#: Which figure along each row goes where. The idle row is front, right, back, right. The walk
#: row is four front, three to the side (one short, so not used), four back, four right.
SERENITY_IDLE = dict(down=0, right=1, up=2)
SERENITY_WALK = dict(down=(0, 1, 2, 3), right=(11, 12, 13, 14), up=(7, 8, 9, 10))

#: The backdrop is a flat, neutral dark grey. Anything warmer, brighter or darker is her.
SERENITY_BG_LO, SERENITY_BG_HI, SERENITY_BG_TINT = 13, 21, 2

#: Narrower than this many sheet pixels is a speck on the backdrop, not a figure.
SERENITY_MIN_WIDTH = 8

#: How much of a cell has to be her for the cell to be, and how far down a figure counts as
#: its body when finding its middle (below that, the stride swings the legs about).
SERENITY_KEEP = 0.5
SERENITY_BODY = 0.55


def _is_backdrop(px):
    r, g, b = px[0], px[1], px[2]
    return (SERENITY_BG_LO <= r <= SERENITY_BG_HI and SERENITY_BG_LO <= g <= SERENITY_BG_HI
            and SERENITY_BG_LO <= b <= SERENITY_BG_HI and max(r, g, b) - min(r, g, b) <= SERENITY_BG_TINT)


def _figures_in_row(pic, row):
    """The left and right edge of every figure along one row of the sheet, left to right."""
    y0, y1 = row
    w = len(pic[0])
    filled = [any(not _is_backdrop(pic[y][x]) for y in range(y0, y1)) for x in range(SERENITY_FIGURES_LEFT, w)]
    spans, x = [], 0
    while x < len(filled):
        if filled[x]:
            start = x
            while x < len(filled) and filled[x]:
                x += 1
            if x - start >= SERENITY_MIN_WIDTH:
                spans.append((SERENITY_FIGURES_LEFT + start, SERENITY_FIGURES_LEFT + x - 1))
        x += 1
    return spans


def _her(pic, span, row):
    """Which sheet pixels in a figure's box are her: everything the backdrop cannot reach from the edge of the box."""
    (x0, x1), (y0, y1) = span, row
    x0, x1 = x0 - 2, x1 + 2
    w, h = x1 - x0 + 1, y1 - y0
    outside = [[False] * w for _ in range(h)]
    stack = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    while stack:
        x, y = stack.pop()
        if outside[y][x] or not _is_backdrop(pic[y0 + y][x0 + x]):
            continue
        outside[y][x] = True
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and not outside[ny][nx]:
                stack.append((nx, ny))
    return {(x0 + x, y0 + y) for y in range(h) for x in range(w) if not outside[y][x]}


def _cut_figure(pic, span, row, ground):
    """One figure as a FRAME_W x FRAME_H frame: its body centred, and the row's ground on the feet row."""
    her = _her(pic, span, row)
    top = min(y for _, y in her)
    body = [x for x, y in her if y <= top + (ground - top) * SERENITY_BODY]
    centre = sum(body) / len(body)

    P = SERENITY_PITCH
    left = centre - (FRAME_W / 2.0) * P
    # The feet go on the row the conscripts' feet are on: the last row of the frame but one.
    above = ground + 1 - (FRAME_H - 1) * P

    frame = new_image(FRAME_W, FRAME_H)
    for j in range(FRAME_H):
        for i in range(FRAME_W):
            cx0, cy0 = left + i * P, above + j * P
            total, mine = 0, []
            for y in range(int(math.floor(cy0)), int(math.ceil(cy0 + P))):
                for x in range(int(math.floor(cx0)), int(math.ceil(cx0 + P))):
                    total += 1
                    if (x, y) in her:
                        mine.append(pic[y][x])
            if total and len(mine) / total >= SERENITY_KEEP:
                mine.sort(key=lambda p: p[0] + p[1] + p[2])
                mid = mine[len(mine) // 2]
                frame[j][i] = [mid[0], mid[1], mid[2], 255]
    return frame


def build_serenity_walker():
    pic = read_png(SERENITY_SOURCE)
    idle = _figures_in_row(pic, SERENITY_IDLE_ROW)
    walk = _figures_in_row(pic, SERENITY_WALK_ROW)
    assert len(idle) >= 3 and len(walk) == 15, ("the sheet's rows have moved", len(idle), len(walk))

    def ground(row, spans):
        return max(y for s in spans for _, y in _her(pic, s, row))

    idle_ground, walk_ground = ground(SERENITY_IDLE_ROW, idle), ground(SERENITY_WALK_ROW, walk)

    frames = {}
    for direction in ("down", "right", "up"):
        frames[direction] = [_cut_figure(pic, idle[SERENITY_IDLE[direction]], SERENITY_IDLE_ROW, idle_ground)]
        frames[direction] += [_cut_figure(pic, walk[k], SERENITY_WALK_ROW, walk_ground) for k in SERENITY_WALK[direction]]
    frames["left"] = [[row[::-1] for row in frame] for frame in frames["right"]]

    sheet = new_image(FRAME_W * COLUMNS, FRAME_H * len(DIRECTIONS))
    for d, direction in enumerate(DIRECTIONS):
        for c, frame in enumerate(frames[direction]):
            for y in range(FRAME_H):
                sheet[d * FRAME_H + y][c * FRAME_W:(c + 1) * FRAME_W] = [list(px) for px in frame[y]]
    write_png(os.path.join(OUT, "serenity-walker.png"), sheet)


def build_palettes():
    """character-palettes.png: one row per preset, hair then outfit then accent. Row 0 of each block is the key."""
    rows = len(HAIR_PRESETS) + len(OUTFIT_PRESETS) + len(ACCENT_PRESETS)
    img = new_image(4, rows)
    y = 0
    for presets in (HAIR_PRESETS, OUTFIT_PRESETS, ACCENT_PRESETS):
        for _, ramp in presets:
            for x, c in enumerate(ramp):
                img[y][x] = [c[0], c[1], c[2], 255]
            y += 1
    write_png(os.path.join(OUT, "character-palettes.png"), img)


def check_keys_are_unique():
    """The swap matches colours exactly, so no key may be painted by anything but its own channel."""
    keys = {}
    for channel, ramp in KEYS.items():
        for c in ramp:
            assert c not in keys, ("key colour used twice", c)
            keys[c] = channel
    for name, _, ramps, _ in FIGURES:
        for mat, ramp in ramps.items():
            if mat in KEYS:
                continue
            for c in ramp:
                assert c not in keys, (name, mat, "paints a key colour", c)
    for c in (EYE, LIP):
        assert c not in keys, ("detail colour is a key", c)
    for presets in (HAIR_PRESETS, OUTFIT_PRESETS, ACCENT_PRESETS):
        for _, ramp in presets:
            assert len(ramp) == len(presets[0][1])


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    check_keys_are_unique()
    print("Generating characters for The Black Box...")
    only = set(sys.argv[1:])
    build_palettes()
    for name, spec, ramps, accessories in FIGURES:
        if only and name not in only:
            continue
        write_png(os.path.join(OUT, name + ".png"), build_sheet(spec, ramps, accessories))
    if not only or "serenity-walker" in only:
        build_serenity_walker()
    print("Done.")
