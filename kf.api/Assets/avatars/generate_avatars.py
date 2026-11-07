#!/usr/bin/env python3
"""Generate the ten default challenger heraldic shield avatars.

Dependency-free: writes 50x50 PNG files with only the Python standard library
(zlib + struct + math), running anywhere Python 3 runs.

Each avatar is a detailed medieval heraldic shield (escutcheon) featuring
classical heraldic tinctures (Gules, Azure, Sable, Vert, Purpure, Or, Argent),
divisions of the field (pale, fess, bend, cross, chevron, quarterly),
3D lighting with specular rims and bevelled metallic borders, and authentic
heraldic charges (Lion Rampant, Crusader Cross, Imperial Eagle, Fleur-de-lis,
Crossed Swords & Greathelm, Dragon Wyvern, Castle Keep, Solar Crown,
Great Stag, and Warhammers).

In the foreground of every shield sits the charge emoji — 🤺 🌹 🐎 ⚔️ 🪖 🏰 💃 🐷 🐱 🐉 —
rendered as a sticker with a dark outline and a drop shadow so it reads as
being in front of the shield rather than painted on it. The glyph bitmaps live
in `emoji/` next to this script and are read at generation time; they are
produced offline by `render_emoji_glyphs.swift` (macOS, system emoji font).
This script itself never needs a font, an image library, or any third-party
package.

Supersampling (SCALE=8) is used for crisp subpixel antialiasing at 50x50 px.
"""

import math
import struct
import zlib
from pathlib import Path

SIZE = 50          # final avatar size in pixels
SCALE = 8         # supersampling factor (8x8 = 64 samples per pixel for ultra smooth rendering)
GRID = SIZE * SCALE

OUT_DIR = Path(__file__).resolve().parent
EMOJI_DIR = OUT_DIR / "emoji"

# Color utilities
def rgb(r, g, b):
    return (int(r), int(g), int(b))

def lerp_c(c1, c2, t):
    t = max(0.0, min(1.0, t))
    return (
        int(c1[0] + (c2[0] - c1[0]) * t),
        int(c1[1] + (c2[1] - c1[1]) * t),
        int(c1[2] + (c2[2] - c1[2]) * t),
    )

def blend(base, overlay, alpha):
    """Alpha composite overlay over base (alpha in 0..1)."""
    if alpha <= 0:
        return base
    if alpha >= 1:
        return overlay
    return (
        int(base[0] * (1 - alpha) + overlay[0] * alpha),
        int(base[1] * (1 - alpha) + overlay[1] * alpha),
        int(base[2] * (1 - alpha) + overlay[2] * alpha),
    )

# Common Heraldic Tinctures
DARK_BG = rgb(15, 17, 23)        # dark slate backdrop
GOLD_LIGHT = rgb(253, 224, 71)    # Or highlight
GOLD_MID = rgb(234, 179, 8)       # Or base
GOLD_DARK = rgb(161, 98, 7)       # Or shadow
GOLD_DEEP = rgb(113, 63, 18)      # Or deep

SILVER_LIGHT = rgb(248, 250, 252) # Argent highlight
SILVER_MID = rgb(203, 213, 225)   # Argent base
SILVER_DARK = rgb(100, 116, 139)  # Argent shadow
SILVER_DEEP = rgb(51, 65, 85)     # Steel deep

RED_LIGHT = rgb(239, 68, 68)      # Gules light
RED_MID = rgb(185, 28, 28)        # Gules base
RED_DARK = rgb(127, 29, 29)       # Gules dark
RED_DEEP = rgb(69, 10, 10)

BLUE_LIGHT = rgb(96, 165, 250)    # Azure light
BLUE_MID = rgb(29, 78, 216)       # Azure base
BLUE_DARK = rgb(30, 58, 138)      # Azure dark
BLUE_DEEP = rgb(15, 23, 42)

GREEN_LIGHT = rgb(74, 222, 128)   # Vert light
GREEN_MID = rgb(21, 128, 61)      # Vert base
GREEN_DARK = rgb(20, 83, 45)      # Vert dark
GREEN_DEEP = rgb(5, 46, 22)

PURPLE_LIGHT = rgb(192, 132, 252) # Purpure light
PURPLE_MID = rgb(126, 34, 206)    # Purpure base
PURPLE_DARK = rgb(88, 28, 135)    # Purpure dark
PURPLE_DEEP = rgb(59, 7, 100)

BLACK_LIGHT = rgb(75, 85, 99)     # Sable light
BLACK_MID = rgb(31, 41, 55)       # Sable base
BLACK_DARK = rgb(17, 24, 39)      # Sable dark

AMBER_LIGHT = rgb(251, 191, 36)
AMBER_MID = rgb(217, 119, 6)
AMBER_DARK = rgb(146, 64, 14)


def shield_sdf(x, y, inset=0.0):
    """Signed distance approximation for medieval heater shield in 0..SIZE space.
    Negative inside, positive outside.
    Shield spans roughly x in [6, 44], y in [4, 46].
    """
    # Normalized x relative to center (25, 0)
    cx = 25.0
    dx = abs(x - cx)

    # Base shield bounds
    top = 4.0 + inset
    bottom = 46.5 - inset * 0.8
    half_width = 19.0 - inset
    mid_y = 21.0

    if y < top:
        return top - y
    if y > bottom:
        return y - bottom

    # Above mid_y, shield is almost straight with slight flare
    if y <= mid_y:
        # width narrows slightly at top corners
        flare = 0.5 * ((y - top) / (mid_y - top))
        cur_half_w = half_width - (0.5 - flare)
        dist_x = dx - cur_half_w
        dist_y = top - y
        return max(dist_x, dist_y)
    else:
        # Lower gothic arc to bottom tip
        t = (y - mid_y) / (bottom - mid_y)
        # Power curve for gothic pointed bottom
        cur_half_w = half_width * (1.0 - t ** 1.6)
        dist_x = dx - cur_half_w
        dist_bottom = y - bottom
        return max(dist_x, dist_bottom * 0.5)


def inside_shield(x, y, inset=0.0):
    return shield_sdf(x, y, inset) <= 0.0


class Canvas:
    def __init__(self):
        self.data = [[DARK_BG for _ in range(GRID)] for _ in range(GRID)]

    def set_pixel(self, gx, gy, color):
        if 0 <= gx < GRID and 0 <= gy < GRID:
            self.data[gy][gx] = color

    def blend_pixel(self, gx, gy, color, alpha):
        if 0 <= gx < GRID and 0 <= gy < GRID:
            self.data[gy][gx] = blend(self.data[gy][gx], color, alpha)


def render_shield_base(canvas, rim_type="gold"):
    """Draws backdrop vignette, shield drop shadow, metallic rim, and inner bevel.

    This is the base plate and must be painted *before* the field and the
    charge: it owns the backdrop and the rim only, and the field is clipped
    to the inner bevel, so the two never overlap.
    """
    for gy in range(GRID):
        y = gy / SCALE
        for gx in range(GRID):
            x = gx / SCALE

            # Subtle radial vignette on backdrop
            dist_c = math.sqrt((x - 25.0) ** 2 + (y - 25.0) ** 2)
            bg_shade = max(0.0, min(1.0, 1.0 - dist_c / 35.0))
            bg_col = lerp_c(rgb(10, 12, 16), rgb(24, 28, 38), bg_shade * 0.6)
            canvas.data[gy][gx] = bg_col

            # Shield drop shadow
            sdf_shadow = shield_sdf(x - 0.8, y - 1.2, inset=0.0)
            if sdf_shadow <= 0:
                canvas.data[gy][gx] = blend(canvas.data[gy][gx], rgb(4, 5, 8), 0.7)
            elif sdf_shadow < 1.5:
                canvas.data[gy][gx] = blend(canvas.data[gy][gx], rgb(4, 5, 8), 0.7 * (1.0 - sdf_shadow / 1.5))

            # Outer rim (metallic frame)
            sdf_outer = shield_sdf(x, y, inset=0.0)
            if sdf_outer <= 0:
                sdf_inner = shield_sdf(x, y, inset=3.2)

                # 3D light vector from top-left (-1, -1)
                lx = (x - 25.0) / 20.0
                ly = (y - 25.0) / 25.0
                light = max(0.0, min(1.0, 0.5 - 0.4 * lx - 0.4 * ly))

                if sdf_inner > 0:
                    # On the rim
                    rim_t = sdf_inner / 3.2  # 0 at inner edge, 1 at outer edge
                    if rim_type == "gold":
                        c_base = lerp_c(GOLD_DARK, GOLD_LIGHT, light)
                        # Rim bevel highlight
                        if rim_t > 0.7 and (lx + ly < 0):
                            c_base = lerp_c(c_base, rgb(254, 249, 195), 0.6)
                        elif rim_t < 0.3:
                            c_base = lerp_c(c_base, GOLD_DEEP, 0.4)
                    else: # silver / steel
                        c_base = lerp_c(SILVER_DARK, SILVER_LIGHT, light)
                        if rim_t > 0.7 and (lx + ly < 0):
                            c_base = lerp_c(c_base, rgb(255, 255, 255), 0.6)
                        elif rim_t < 0.3:
                            c_base = lerp_c(c_base, SILVER_DEEP, 0.4)
                    canvas.data[gy][gx] = c_base


def apply_shield_shading(canvas):
    """Applies convex 3D lighting, inner shadow and subtle gloss over the shield field."""
    for gy in range(GRID):
        y = gy / SCALE
        for gx in range(GRID):
            x = gx / SCALE
            sdf_inner = shield_sdf(x, y, inset=3.2)
            if sdf_inner <= 0:
                # Inside the shield field
                cur_col = canvas.data[gy][gx]

                # Convex curvature shading
                dx = (x - 25.0) / 16.0
                dy = (y - 22.0) / 20.0
                r2 = dx * dx + dy * dy
                curvature = math.exp(-r2 * 0.8)

                # Light from top-left
                light = 0.5 - 0.35 * dx - 0.35 * dy + 0.2 * curvature
                light = max(0.0, min(1.0, light))

                # Modulate color
                if light > 0.5:
                    shaded = lerp_c(cur_col, rgb(255, 255, 255), (light - 0.5) * 0.4)
                else:
                    shaded = lerp_c(cur_col, rgb(5, 5, 10), (0.5 - light) * 0.65)

                # Inner edge shadow (ambient occlusion under the rim)
                dist_to_rim = -sdf_inner
                if dist_to_rim < 1.5:
                    ao = (1.5 - dist_to_rim) / 1.5
                    shaded = lerp_c(shaded, rgb(10, 10, 15), ao * 0.5)

                canvas.data[gy][gx] = shaded


def add_rim_rivets(canvas, rim_type="gold"):
    """Adds small metallic rivets around the shield rim."""
    rivet_pos = [
        (8.5, 6.5), (25.0, 5.5), (41.5, 6.5),
        (7.5, 18.0), (42.5, 18.0),
        (11.5, 30.0), (38.5, 30.0),
        (17.5, 39.5), (32.5, 39.5),
        (25.0, 44.2)
    ]
    r_light = GOLD_LIGHT if rim_type == "gold" else SILVER_LIGHT
    r_dark = GOLD_DEEP if rim_type == "gold" else SILVER_DEEP

    for rx, ry in rivet_pos:
        grx = int(rx * SCALE)
        gry = int(ry * SCALE)
        rad = int(1.1 * SCALE)
        for dy in range(-rad, rad + 1):
            for dx in range(-rad, rad + 1):
                d2 = dx * dx + dy * dy
                if d2 <= rad * rad:
                    t = (dx + dy + rad * 1.2) / (rad * 2.4)
                    c = lerp_c(r_dark, r_light, max(0.0, min(1.0, t)))
                    canvas.blend_pixel(grx + dx, gry + dy, c, 0.9)


# Drawing primitives on canvas (in 0..SIZE space, auto-supersampled)

def fill_field(canvas, color_fn):
    """Fill the inner field of the shield using a coordinate color function."""
    for gy in range(GRID):
        y = gy / SCALE
        for gx in range(GRID):
            x = gx / SCALE
            if shield_sdf(x, y, inset=3.2) <= 0:
                canvas.data[gy][gx] = color_fn(x, y)


def draw_poly(canvas, points, color, alpha=1.0, clip_field=True):
    scaled = [(p[0] * SCALE, p[1] * SCALE) for p in points]
    ys = [p[1] for p in scaled]
    y_min = max(0, int(min(ys)))
    y_max = min(GRID - 1, int(max(ys)) + 1)

    for gy in range(y_min, y_max + 1):
        sample_y = gy + 0.5
        crossings = []
        for i in range(len(scaled)):
            x1, y1 = scaled[i]
            x2, y2 = scaled[(i + 1) % len(scaled)]
            if (y1 <= sample_y < y2) or (y2 <= sample_y < y1):
                t = (sample_y - y1) / (y2 - y1)
                crossings.append(x1 + t * (x2 - x1))
        crossings.sort()

        for i in range(0, len(crossings) - 1, 2):
            x_start = max(0, int(round(crossings[i])))
            x_end = min(GRID - 1, int(round(crossings[i + 1])))
            for gx in range(x_start, x_end + 1):
                if clip_field and shield_sdf(gx / SCALE, gy / SCALE, inset=3.2) > 0:
                    continue
                canvas.blend_pixel(gx, gy, color, alpha)


def draw_circle(canvas, cx, cy, radius, color, alpha=1.0, clip_field=True):
    gcx = cx * SCALE
    gcy = cy * SCALE
    r = radius * SCALE
    ymin = max(0, int(gcy - r))
    ymax = min(GRID - 1, int(gcy + r) + 1)
    xmin = max(0, int(gcx - r))
    xmax = min(GRID - 1, int(gcx + r) + 1)

    r2 = r * r
    for gy in range(ymin, ymax + 1):
        for gx in range(xmin, xmax + 1):
            if (gx + 0.5 - gcx) ** 2 + (gy + 0.5 - gcy) ** 2 <= r2:
                if clip_field and shield_sdf(gx / SCALE, gy / SCALE, inset=3.2) > 0:
                    continue
                canvas.blend_pixel(gx, gy, color, alpha)


def draw_rect(canvas, x, y, w, h, color, alpha=1.0, clip_field=True):
    draw_poly(canvas, [(x, y), (x + w, y), (x + w, y + h), (x, y + h)], color, alpha, clip_field)


# =========================================================================
# THE 10 HERALDIC SHIELDS WITH EMOJI SYMBOLS (KNIGHTISH THEMATIC SET)
# 1. Knight Emoji, 2. Rose, 3. Warhorse, 4. Sword, 5. Helmet,
# 6. Castle, 7. Dancing Girl, 8. Pig, 9. Cat, 10. Dragon
#
# Every renderer paints in the same order:
#   1. render_shield_base  — backdrop, drop shadow, metallic rim
#   2. fill_field          — the tincture of the field
#   3. the charge          — the heraldic device
#   4. apply_shield_shading + add_rim_rivets
# The base plate comes first because it owns the backdrop: painting it last
# would wipe the field and the charge back out.
# =========================================================================

def shield_01_knight_emoji(canvas):
    """Shield 01: Knight Emoji with golden visor & crimson plume on Royal Gules."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        return lerp_c(RED_MID, RED_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    # Knight Face/Helmet Emoji
    # Golden / Warm emoji skin / helm base
    draw_circle(canvas, 25.0, 24.0, 9.5, SILVER_LIGHT)
    draw_circle(canvas, 25.0, 24.0, 8.0, SILVER_MID)

    # Plume atop helmet (Crimson plume)
    draw_poly(canvas, [(23, 15), (25, 8), (28, 9), (27, 15)], RED_LIGHT)
    draw_poly(canvas, [(25, 8), (30, 10), (28, 14)], RED_MID)
    draw_circle(canvas, 25.0, 15.0, 1.8, GOLD_LIGHT)

    # Visor slit / ocular plate
    draw_rect(canvas, 18.5, 20.5, 13.0, 3.5, BLACK_DARK)
    # Bright cheerful eyes in the visor
    draw_circle(canvas, 21.5, 22.2, 1.3, GOLD_LIGHT)
    draw_circle(canvas, 28.5, 22.2, 1.3, GOLD_LIGHT)
    draw_circle(canvas, 22.0, 22.0, 0.5, rgb(255, 255, 255))
    draw_circle(canvas, 29.0, 22.0, 0.5, rgb(255, 255, 255))

    # Visor grill / smile
    draw_poly(canvas, [(20, 26), (30, 26), (28, 30), (22, 30)], GOLD_MID)
    for bx in [22.5, 25.0, 27.5]:
        draw_rect(canvas, bx - 0.4, 26.5, 0.8, 3.0, BLACK_DARK)

    # Golden Chin guard / Gorget
    draw_poly(canvas, [(20, 30), (30, 30), (28, 34), (25, 36), (22, 34)], GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


def shield_02_rose(canvas):
    """Shield 02: Romantic Knightly Rose with emerald leaves on Argent."""
    render_shield_base(canvas, "steel")

    def field_col(x, y):
        return lerp_c(SILVER_LIGHT, SILVER_MID, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    # Emerald Stem and Leaves
    draw_poly(canvas, [(23.5, 24), (26.5, 24), (26, 38), (24, 38)], GREEN_DARK)
    # Leaves
    draw_poly(canvas, [(23.5, 30), (16, 27), (14, 31), (23.5, 32)], GREEN_MID)
    draw_poly(canvas, [(26.5, 33), (34, 30), (36, 34), (26.5, 35)], GREEN_MID)
    draw_circle(canvas, 16.0, 28.5, 1.5, GREEN_LIGHT)
    draw_circle(canvas, 34.0, 31.5, 1.5, GREEN_LIGHT)

    # Rose Petal Layers (Blooming red chivalric rose)
    # Outer petals
    for ang_deg in [0, 72, 144, 216, 288]:
        rad = math.radians(ang_deg)
        px = 25.0 + math.cos(rad) * 5.5
        py = 22.0 + math.sin(rad) * 5.5
        draw_circle(canvas, px, py, 4.2, RED_DARK)

    for ang_deg in [36, 108, 180, 252, 324]:
        rad = math.radians(ang_deg)
        px = 25.0 + math.cos(rad) * 4.0
        py = 22.0 + math.sin(rad) * 4.0
        draw_circle(canvas, px, py, 3.8, RED_MID)

    # Inner petals
    draw_circle(canvas, 25.0, 22.0, 4.2, RED_LIGHT)
    draw_circle(canvas, 24.2, 21.2, 2.5, rgb(254, 202, 202))
    draw_circle(canvas, 25.0, 22.0, 1.8, RED_DARK)
    draw_circle(canvas, 25.5, 22.2, 0.9, GOLD_LIGHT)

    # Golden glints
    for gx, gy in [(15, 14), (35, 14), (25, 41)]:
        draw_circle(canvas, gx, gy, 1.2, GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "steel")


def shield_03_horse(canvas):
    """Shield 03: Gallant Knight Warhorse / Steed on Azure & Or."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        if x < 25.0:
            return lerp_c(BLUE_MID, BLUE_DARK, (y - 4) / 42.0)
        else:
            return lerp_c(AMBER_MID, AMBER_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    horse_col = SILVER_LIGHT
    horse_shade = SILVER_MID
    mane_col = GOLD_LIGHT

    # Horse Neck & Chest
    draw_poly(canvas, [(20, 22), (28, 20), (33, 35), (20, 36), (17, 28)], horse_col)
    draw_circle(canvas, 24.0, 27.0, 5.0, horse_col)

    # Horse Head (Facing Left/Profile)
    draw_circle(canvas, 21.0, 18.0, 4.2, horse_col)
    # Muzzle / Snout
    draw_poly(canvas, [(21, 15), (14, 18), (13, 23), (19, 23)], horse_col)
    draw_circle(canvas, 14.5, 20.5, 2.4, horse_col)
    # Nostril
    draw_circle(canvas, 13.5, 21.0, 0.7, BLACK_DARK)

    # Pointy Horse Ears
    draw_poly(canvas, [(20, 15), (19, 10), (22, 13)], horse_col)
    draw_poly(canvas, [(23, 15), (23, 10), (25, 13)], horse_col)

    # Big expressive eye
    draw_circle(canvas, 19.5, 17.5, 1.5, BLACK_DARK)
    draw_circle(canvas, 19.2, 17.2, 0.6, rgb(255, 255, 255))

    # Flowing Golden Mane
    draw_poly(canvas, [(24, 13), (30, 15), (28, 19), (24, 17)], mane_col)
    draw_poly(canvas, [(26, 17), (32, 20), (30, 25), (26, 22)], mane_col)
    draw_poly(canvas, [(27, 23), (33, 27), (31, 32), (27, 28)], mane_col)

    # Knightly Bridle & Reins (Crimson & Gold)
    draw_poly(canvas, [(14, 20), (22, 17), (23, 18.5), (15, 21.5)], RED_MID)
    draw_poly(canvas, [(20, 15), (21, 23), (22.5, 23), (21.5, 15)], RED_MID)
    draw_circle(canvas, 20.5, 19.5, 1.2, GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


def shield_04_sword(canvas):
    """Shield 04: Radiant Chivalric Longsword on Azure (Royal Blue)."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        return lerp_c(BLUE_MID, BLUE_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    # Golden Sunburst / Radiant rays behind sword
    for i in range(12):
        ang = i * (math.pi / 6.0)
        rx = 25.0 + math.cos(ang) * 16.0
        ry = 22.0 + math.sin(ang) * 16.0
        draw_poly(canvas, [(25, 22), (rx, ry), (25 + math.cos(ang + 0.15) * 4, 22 + math.sin(ang + 0.15) * 4)], GOLD_LIGHT, alpha=0.3)

    # Shining Blade pointing upwards
    # Blade
    draw_poly(canvas, [(23.5, 12), (25, 8), (26.5, 12), (26.5, 28), (23.5, 28)], SILVER_LIGHT)
    # Blade ridge & gleam
    draw_rect(canvas, 24.7, 9.0, 0.6, 19.0, SILVER_DARK)
    draw_poly(canvas, [(23.5, 12), (25, 8), (24.7, 28), (23.5, 28)], rgb(255, 255, 255), alpha=0.6)

    # Golden Crossguard
    draw_poly(canvas, [(15, 27), (25, 29), (35, 27), (36, 31), (25, 32), (14, 31)], GOLD_LIGHT)
    draw_circle(canvas, 14.5, 29.0, 1.8, GOLD_MID)
    draw_circle(canvas, 35.5, 29.0, 1.8, GOLD_MID)

    # Hilt / Grip
    draw_rect(canvas, 24.0, 31.0, 2.0, 7.0, AMBER_DARK)
    for gy in [32.5, 34.5, 36.5]:
        draw_rect(canvas, 23.8, gy, 2.4, 0.6, GOLD_LIGHT)

    # Pommel (Golden disc with ruby core)
    draw_circle(canvas, 25.0, 39.5, 2.4, GOLD_LIGHT)
    draw_circle(canvas, 25.0, 39.5, 1.2, RED_LIGHT)

    # Sparkles
    for sx, sy in [(16, 14), (34, 14)]:
        draw_circle(canvas, sx, sy, 1.3, GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


def shield_05_helmet(canvas):
    """Shield 05: Steel Knight Greathelm with Brass Visor on Emerald Vert."""
    render_shield_base(canvas, "steel")

    def field_col(x, y):
        return lerp_c(GREEN_MID, GREEN_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    helm_l = SILVER_LIGHT
    helm_m = SILVER_MID
    helm_d = SILVER_DEEP

    # Helmet Dome / Skull cap
    draw_poly(canvas, [(20, 18), (25, 13), (30, 18), (30, 27), (20, 27)], helm_m)
    draw_circle(canvas, 25.0, 17.5, 4.8, helm_m)

    # Lower faceplate / sugarloaf cone
    draw_poly(canvas, [(20, 25), (30, 25), (28, 35), (25, 38), (22, 35)], helm_l)
    draw_poly(canvas, [(25, 25), (30, 25), (28, 35), (25, 38)], helm_d, alpha=0.35)

    # Brass Cross across visor
    draw_rect(canvas, 24.2, 16.5, 1.6, 18.0, GOLD_LIGHT)
    draw_rect(canvas, 19.5, 23.5, 11.0, 2.0, GOLD_LIGHT)

    # Eye slits (dark)
    draw_rect(canvas, 20.5, 24.0, 3.5, 1.0, BLACK_DARK)
    draw_rect(canvas, 26.0, 24.0, 3.5, 1.0, BLACK_DARK)

    # Breathing perforation holes
    for hx, hy in [(22, 28), (22, 31), (28, 28), (28, 31), (25, 33)]:
        draw_circle(canvas, hx, hy, 0.6, BLACK_DARK)

    # Flowing Plume on top
    draw_poly(canvas, [(25, 13), (22, 8), (29, 9)], RED_LIGHT)
    draw_circle(canvas, 25.0, 13.0, 1.5, GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "steel")


def shield_06_castle(canvas):
    """Shield 06: Impregnable Castle Keep & Turrets on Sable & Purpure."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        return lerp_c(PURPLE_MID, BLACK_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    stone_l = SILVER_LIGHT
    stone_m = SILVER_MID
    stone_d = SILVER_DEEP

    # Main Center Keep
    draw_rect(canvas, 18, 19, 14, 18, stone_m)
    draw_rect(canvas, 25, 19, 7, 18, stone_d, alpha=0.3)

    # Left & Right Turrets
    draw_rect(canvas, 13, 16, 6, 21, stone_m)
    draw_rect(canvas, 31, 16, 6, 21, stone_m)
    draw_rect(canvas, 34, 16, 3, 21, stone_d, alpha=0.3)

    # Battlements / Crenellations
    for bx in [13, 16.5, 31, 34.5]:
        draw_rect(canvas, bx, 13.5, 2.0, 3, stone_l)
    for bx in [18.5, 22.0, 25.5, 29.0]:
        draw_rect(canvas, bx, 17.0, 2.0, 3, stone_l)

    # Conical Roof on Center
    draw_poly(canvas, [(20, 17), (25, 9), (30, 17)], RED_MID)
    draw_poly(canvas, [(25, 9), (30, 17), (25, 17)], RED_DARK)
    # Pennant on peak
    draw_poly(canvas, [(25, 9), (25, 5), (29, 7)], GOLD_LIGHT)

    # Gate & Portcullis
    draw_rect(canvas, 22.5, 28, 5, 9, BLACK_DARK)
    draw_circle(canvas, 25.0, 28.0, 2.5, BLACK_DARK)
    for gy in [29, 32, 35]:
        draw_rect(canvas, 23, gy, 4, 0.7, GOLD_LIGHT)
    for gx in [24, 26]:
        draw_rect(canvas, gx, 27, 0.7, 10, GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


def shield_07_dancing_girl(canvas):
    """Shield 07: Graceful Dancing Girl / Princess on Azure & Rose."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        if (x - 4) + (y - 4) < 42.0:
            return lerp_c(BLUE_MID, BLUE_DARK, (y - 4) / 42.0)
        else:
            return lerp_c(RED_MID, PURPLE_DEEP, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    dress = SILVER_LIGHT

    # Swirling Ribbons / Aura around dancing maiden
    draw_poly(canvas, [(14, 25), (18, 18), (25, 14), (22, 12), (15, 18)], GOLD_LIGHT, alpha=0.45)
    draw_poly(canvas, [(36, 25), (32, 18), (25, 14), (28, 12), (35, 18)], GOLD_LIGHT, alpha=0.45)

    # Flowing Bell Skirt in Motion
    draw_poly(canvas, [(23, 24), (27, 24), (35, 38), (29, 39), (25, 36), (21, 39), (15, 38)], dress)
    draw_poly(canvas, [(25, 24), (27, 24), (35, 38), (29, 39), (25, 36)], SILVER_MID)

    # Torso / Corset
    draw_poly(canvas, [(23, 18), (27, 18), (26, 25), (24, 25)], GOLD_MID)

    # Graceful Raised Arms
    draw_poly(canvas, [(23, 19), (17, 14), (19, 11), (21, 14), (24, 18)], dress)
    draw_circle(canvas, 18.5, 11.5, 1.2, GOLD_LIGHT)
    draw_poly(canvas, [(27, 19), (33, 14), (31, 11), (29, 14), (26, 18)], dress)
    draw_circle(canvas, 31.5, 11.5, 1.2, GOLD_LIGHT)

    # Maiden Face & Flowing Golden Hair
    draw_circle(canvas, 25.0, 15.0, 2.5, GOLD_LIGHT)
    draw_poly(canvas, [(23, 13), (21, 17), (20, 22), (23, 20), (24, 15)], GOLD_MID)
    draw_poly(canvas, [(27, 13), (29, 17), (30, 22), (27, 20), (26, 15)], GOLD_MID)
    # Tiara
    draw_poly(canvas, [(23.5, 13), (26.5, 13), (26, 11), (25, 12), (24, 11)], GOLD_LIGHT)

    # Ballet Slippers
    draw_circle(canvas, 23.5, 39.5, 1.2, GOLD_LIGHT)
    draw_circle(canvas, 26.5, 39.5, 1.2, GOLD_LIGHT)

    # Sparkles
    for sx, sy in [(13, 14), (37, 14), (12, 32), (38, 32)]:
        draw_circle(canvas, sx, sy, 1.2, GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


def shield_08_pig(canvas):
    """Shield 08: Cheerful Medieval Pig / Boar with Golden Crown on Vert & Purpure."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        return lerp_c(PURPLE_MID, PURPLE_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    pig_pink = rgb(244, 160, 180)
    pig_light = rgb(253, 200, 215)
    pig_dark = rgb(220, 120, 145)

    # Pig Body
    draw_circle(canvas, 25.0, 26.0, 8.5, pig_pink)
    draw_circle(canvas, 25.0, 26.0, 7.0, pig_light)

    # Pointy Floppy Pig Ears
    draw_poly(canvas, [(19, 19), (14, 13), (20, 15)], pig_dark)
    draw_poly(canvas, [(31, 19), (36, 13), (30, 15)], pig_dark)

    # Pig Eyes (Cheerful & Wide)
    draw_circle(canvas, 20.5, 23.0, 1.6, BLACK_DARK)
    draw_circle(canvas, 20.2, 22.7, 0.6, rgb(255, 255, 255))
    draw_circle(canvas, 29.5, 23.0, 1.6, BLACK_DARK)
    draw_circle(canvas, 29.2, 22.7, 0.6, rgb(255, 255, 255))

    # Cute Pig Snout / Nose
    draw_poly(canvas, [(21, 27), (29, 27), (29, 32), (21, 32)], pig_dark)
    draw_circle(canvas, 21.0, 29.5, 2.5, pig_dark)
    draw_circle(canvas, 29.0, 29.5, 2.5, pig_dark)
    draw_circle(canvas, 25.0, 29.5, 3.2, pig_light)
    # Nostrils
    draw_circle(canvas, 23.2, 29.5, 0.9, BLACK_DARK)
    draw_circle(canvas, 26.8, 29.5, 0.9, BLACK_DARK)

    # Rosy Cheeks
    draw_circle(canvas, 17.5, 26.5, 1.5, RED_LIGHT, alpha=0.5)
    draw_circle(canvas, 32.5, 26.5, 1.5, RED_LIGHT, alpha=0.5)

    # Little Golden Crown atop Pig head
    draw_poly(canvas, [(21, 18), (29, 18), (30, 14), (25, 16), (20, 14)], GOLD_LIGHT)
    draw_circle(canvas, 25.0, 16.0, 1.0, RED_LIGHT)

    # Hooves at bottom
    draw_poly(canvas, [(20, 34), (23, 34), (23, 37), (20, 37)], BLACK_DARK)
    draw_poly(canvas, [(27, 34), (30, 34), (30, 37), (27, 37)], BLACK_DARK)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


def shield_09_cat(canvas):
    """Shield 09: Crowned Heraldic Cat with Emerald Eyes on Forest Vert & Sable."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        if x < 25.0:
            return lerp_c(GREEN_MID, GREEN_DARK, (y - 4) / 42.0)
        else:
            return lerp_c(BLACK_MID, BLACK_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    cat_body = SILVER_LIGHT
    cat_dark = SILVER_DARK

    # Cat Body (Sitting upright)
    draw_poly(canvas, [(21, 23), (29, 23), (31, 35), (19, 35)], cat_body)
    draw_circle(canvas, 25.0, 27.0, 5.5, cat_body)

    # Front Paws
    draw_poly(canvas, [(21, 30), (23, 38), (25, 38), (23, 30)], cat_body)
    draw_poly(canvas, [(27, 30), (25, 38), (27, 38), (29, 30)], cat_body)

    # Cat Head
    draw_circle(canvas, 25.0, 17.5, 4.5, cat_body)

    # Pointy Cat Ears
    draw_poly(canvas, [(21, 15), (19, 9), (24, 13)], cat_body)
    draw_poly(canvas, [(20.5, 14.5), (19.5, 10.5), (23, 13)], RED_LIGHT)
    draw_poly(canvas, [(29, 15), (31, 9), (26, 13)], cat_body)
    draw_poly(canvas, [(29.5, 14.5), (30.5, 10.5), (27, 13)], RED_LIGHT)

    # Emerald Green Cat Eyes
    draw_circle(canvas, 23.0, 17.0, 1.3, GREEN_LIGHT)
    draw_circle(canvas, 27.0, 17.0, 1.3, GREEN_LIGHT)
    draw_rect(canvas, 22.8, 16.2, 0.5, 1.6, BLACK_DARK)
    draw_rect(canvas, 26.8, 16.2, 0.5, 1.6, BLACK_DARK)

    # Nose & Whiskers
    draw_poly(canvas, [(24.5, 19), (25.5, 19), (25, 19.8)], RED_LIGHT)
    draw_poly(canvas, [(21, 19), (14, 18), (21, 19.6)], SILVER_LIGHT)
    draw_poly(canvas, [(21, 20), (14, 21), (21, 20.6)], SILVER_LIGHT)
    draw_poly(canvas, [(29, 19), (36, 18), (29, 19.6)], SILVER_LIGHT)
    draw_poly(canvas, [(29, 20), (36, 21), (29, 20.6)], SILVER_LIGHT)

    # Curled Tail
    draw_poly(canvas, [(29, 35), (34, 34), (36, 28), (34, 24), (32, 25), (33, 29), (30, 33), (27, 36)], cat_body)
    draw_circle(canvas, 33.5, 24.5, 1.5, cat_body)

    # Golden Crown
    draw_poly(canvas, [(22, 13), (28, 13), (29, 10), (25, 11), (21, 10)], GOLD_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


def shield_10_dragon(canvas):
    """Shield 10: Fire-Breathing Dragon on Gules & Sable Saltire."""
    render_shield_base(canvas, "gold")

    def field_col(x, y):
        d1 = (x - 25.0) - (y - 25.0)
        d2 = (x - 25.0) + (y - 25.0)
        if (d1 > 0 and d2 > 0) or (d1 < 0 and d2 < 0):
            return lerp_c(RED_MID, RED_DARK, (y - 4) / 42.0)
        else:
            return lerp_c(BLACK_MID, BLACK_DARK, (y - 4) / 42.0)
    fill_field(canvas, field_col)

    gold = AMBER_LIGHT
    gold_d = AMBER_MID

    # Dragon Body & Chest
    draw_poly(canvas, [(23, 20), (28, 19), (29, 29), (24, 31), (21, 26)], gold)
    draw_circle(canvas, 25.0, 22.0, 4.0, gold)

    # Dragon Head & Horns
    draw_poly(canvas, [(24, 16), (28, 13), (33, 12), (30, 15), (33, 17), (27, 18)], gold)
    draw_poly(canvas, [(23, 15), (20, 10), (24, 13)], gold_d)

    # Fire breath stream
    draw_poly(canvas, [(33, 13), (42, 9), (44, 16), (33, 16)], RED_LIGHT)
    draw_poly(canvas, [(34, 14), (41, 11), (43, 15), (34, 15)], AMBER_LIGHT)
    draw_poly(canvas, [(35, 14.5), (39, 13), (40, 14.5)], rgb(255, 255, 200))

    # Bat Wings
    draw_poly(canvas, [(24, 19), (14, 10), (19, 16)], gold)
    draw_poly(canvas, [(19, 16), (11, 14), (17, 20)], gold)
    draw_poly(canvas, [(17, 20), (12, 19), (21, 23)], gold)

    # Claws & Tail
    draw_poly(canvas, [(27, 21), (33, 22), (35, 20)], gold)
    draw_poly(canvas, [(25, 29), (32, 33), (34, 38), (30, 38), (25, 33)], gold)
    draw_poly(canvas, [(23, 30), (18, 33), (16, 38), (21, 42), (25, 41), (21, 38), (20, 34), (24, 32)], gold)
    draw_poly(canvas, [(25, 41), (29, 43), (27, 39)], RED_LIGHT)

    apply_shield_shading(canvas)
    add_rim_rivets(canvas, "gold")


SHIELD_RENDERERS = [
    shield_01_knight_emoji,
    shield_02_rose,
    shield_03_horse,
    shield_04_sword,
    shield_05_helmet,
    shield_06_castle,
    shield_07_dancing_girl,
    shield_08_pig,
    shield_09_cat,
    shield_10_dragon,
]


# =========================================================================
# THE FOREGROUND EMOJI BADGE
#
# One glyph per shield, drawn on top of the finished shield as a sticker:
# a dark outline keeps it legible over any tincture, and a soft drop shadow
# lifts it off the shield so it reads as being in front of it.
# =========================================================================

# One glyph per shield, in the same order as SHIELD_RENDERERS.
EMOJI_FILES = [
    "knight.png",   # 01 🤺  fencer
    "rose.png",     # 02 🌹  rose
    "horse.png",    # 03 🐎  warhorse
    "sword.png",    # 04 ⚔️  crossed swords
    "helmet.png",   # 05 🪖  helmet
    "castle.png",   # 06 🏰  castle
    "maiden.png",   # 07 💃  dancing maiden
    "pig.png",      # 08 🐷  pig
    "cat.png",      # 09 🐱  cat wearing a 👑
    "dragon.png",   # 10 🐉  dragon
]

BADGE_BOX = 28       # final pixels the badge occupies, outline and shadow included
BADGE_SS = 2         # supersampling inside the badge
EMOJI_SIZE = 20      # final pixels of the glyph itself
BADGE_CENTER = (25.0, 24.0)
OUTLINE_COLOR = rgb(13, 15, 21)
SHADOW_COLOR = rgb(0, 0, 0)


def read_png_rgba(path):
    """Reads an 8-bit RGBA PNG into (width, height, rows) with straight alpha."""
    data = path.read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(f'{path.name}: not a PNG file')

    pos = 8
    idat = bytearray()
    width = height = 0
    while pos < len(data):
        length = struct.unpack('>I', data[pos:pos + 4])[0]
        tag = data[pos + 4:pos + 8]
        payload = data[pos + 8:pos + 8 + length]
        if tag == b'IHDR':
            width, height, depth, color_type, _, _, interlace = struct.unpack('>IIBBBBB', payload[:13])
            if depth != 8 or color_type != 6 or interlace != 0:
                raise ValueError(
                    f'{path.name}: expected an 8-bit non-interlaced RGBA PNG '
                    f'(got depth={depth}, color type={color_type}, interlace={interlace})'
                )
        elif tag == b'IDAT':
            idat += payload
        elif tag == b'IEND':
            break
        pos += 12 + length

    raw = zlib.decompress(bytes(idat))
    stride = width * 4
    rows = []
    prev = bytearray(stride)
    p = 0
    for _ in range(height):
        filter_type = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        if filter_type == 1:
            for i in range(4, stride):
                line[i] = (line[i] + line[i - 4]) & 0xFF
        elif filter_type == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif filter_type == 3:
            for i in range(stride):
                left = line[i - 4] if i >= 4 else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif filter_type == 4:
            for i in range(stride):
                a = line[i - 4] if i >= 4 else 0
                b = prev[i]
                c = prev[i - 4] if i >= 4 else 0
                estimate = a + b - c
                pa, pb, pc = abs(estimate - a), abs(estimate - b), abs(estimate - c)
                predictor = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + predictor) & 0xFF
        rows.append(line)
        prev = line

    return width, height, rows


def crop_to_ink(rows, width, height, threshold=6):
    """Crops an RGBA image down to the bounding box of its visible pixels."""
    x0, y0, x1, y1 = width, height, -1, -1
    for y in range(height):
        row = rows[y]
        for x in range(width):
            if row[x * 4 + 3] > threshold:
                if x < x0:
                    x0 = x
                if x > x1:
                    x1 = x
                if y < y0:
                    y0 = y
                if y > y1:
                    y1 = y
    if x1 < 0:
        return width, height, rows
    return x1 - x0 + 1, y1 - y0 + 1, [rows[y][x0 * 4:(x1 + 1) * 4] for y in range(y0, y1 + 1)]


def resize_rgba(rows, src_w, src_h, dst_w, dst_h):
    """Area-average resize of an RGBA image. Straight alpha in, straight alpha out."""
    out = bytearray(dst_w * dst_h * 4)
    for dy in range(dst_h):
        y0 = dy * src_h // dst_h
        y1 = max(y0 + 1, (dy + 1) * src_h // dst_h)
        for dx in range(dst_w):
            x0 = dx * src_w // dst_w
            x1 = max(x0 + 1, (dx + 1) * src_w // dst_w)
            r_sum = g_sum = b_sum = a_sum = 0
            count = 0
            for sy in range(y0, y1):
                row = rows[sy]
                for sx in range(x0, x1):
                    o = sx * 4
                    alpha = row[o + 3]
                    r_sum += row[o] * alpha
                    g_sum += row[o + 1] * alpha
                    b_sum += row[o + 2] * alpha
                    a_sum += alpha
                    count += 1
            o = (dy * dst_w + dx) * 4
            if a_sum:
                out[o] = min(255, r_sum // a_sum)
                out[o + 1] = min(255, g_sum // a_sum)
                out[o + 2] = min(255, b_sum // a_sum)
            out[o + 3] = a_sum // count
    return out


def box_blur(values, width, height, radius=1):
    """Separable box blur over a list of width*height floats."""
    horizontal = [0.0] * (width * height)
    for y in range(height):
        for x in range(width):
            total = 0.0
            count = 0
            for k in range(-radius, radius + 1):
                xx = x + k
                if 0 <= xx < width:
                    total += values[y * width + xx]
                    count += 1
            horizontal[y * width + x] = total / count

    out = [0.0] * (width * height)
    for y in range(height):
        for x in range(width):
            total = 0.0
            count = 0
            for k in range(-radius, radius + 1):
                yy = y + k
                if 0 <= yy < height:
                    total += horizontal[yy * width + x]
                    count += 1
            out[y * width + x] = total / count
    return out


def max_filter(values, width, height):
    """3x3 maximum filter — grows the silhouette by one pixel."""
    out = [0.0] * (width * height)
    for y in range(height):
        for x in range(width):
            best = 0.0
            for ky in (-1, 0, 1):
                yy = y + ky
                if not 0 <= yy < height:
                    continue
                for kx in (-1, 0, 1):
                    xx = x + kx
                    if not 0 <= xx < width:
                        continue
                    value = values[yy * width + xx]
                    if value > best:
                        best = value
            out[y * width + x] = best
    return out


def over(dst, src):
    """Premultiplied 'source over destination', in place."""
    for i in range(0, len(dst), 4):
        src_alpha = src[i + 3]
        if src_alpha <= 0:
            continue
        inv = 1.0 - src_alpha
        dst[i] = src[i] + dst[i] * inv
        dst[i + 1] = src[i + 1] + dst[i + 1] * inv
        dst[i + 2] = src[i + 2] + dst[i + 2] * inv
        dst[i + 3] = src_alpha + dst[i + 3] * inv


def build_badge(emoji_path):
    """Builds the foreground badge for one shield.

    Returns (pixels, width, height) with premultiplied RGBA. The badge holds
    the emoji glyph, a dark outline that hugs its silhouette, and a drop
    shadow that pushes the whole sticker in front of the shield.
    """
    width, height, rows = read_png_rgba(emoji_path)
    width, height, rows = crop_to_ink(rows, width, height)

    # Fit the glyph into the badge box, keeping its aspect ratio.
    box = BADGE_BOX * BADGE_SS
    glyph_box = EMOJI_SIZE * BADGE_SS
    scale = min(glyph_box / width, glyph_box / height)
    glyph_w = max(1, int(round(width * scale)))
    glyph_h = max(1, int(round(height * scale)))
    glyph = resize_rgba(rows, width, height, glyph_w, glyph_h)

    offset_x = (box - glyph_w) // 2
    offset_y = (box - glyph_h) // 2

    # The glyph's coverage inside the badge box.
    coverage = [0.0] * (box * box)
    for y in range(glyph_h):
        for x in range(glyph_w):
            coverage[(y + offset_y) * box + (x + offset_x)] = glyph[(y * glyph_w + x) * 4 + 3] / 255.0

    softened = box_blur(coverage, box, box, 1)
    spread = max_filter(softened, box, box)

    badge = [0.0] * (box * box * 4)

    # 1. Drop shadow: the silhouette, pushed down and to the right.
    shadow = [0.0] * (box * box * 4)
    for y in range(box):
        for x in range(box):
            sx, sy = x - 3, y - 4
            if not (0 <= sx < box and 0 <= sy < box):
                continue
            alpha = softened[sy * box + sx] * 0.55
            o = (y * box + x) * 4
            shadow[o] = SHADOW_COLOR[0] * alpha
            shadow[o + 1] = SHADOW_COLOR[1] * alpha
            shadow[o + 2] = SHADOW_COLOR[2] * alpha
            shadow[o + 3] = alpha
    over(badge, shadow)

    # 2. Outline: a dark border that follows the silhouette.
    outline = [0.0] * (box * box * 4)
    for i in range(box * box):
        alpha = min(1.0, max(0.0, (spread[i] - coverage[i]) * 1.3)) * 0.92
        o = i * 4
        outline[o] = OUTLINE_COLOR[0] * alpha
        outline[o + 1] = OUTLINE_COLOR[1] * alpha
        outline[o + 2] = OUTLINE_COLOR[2] * alpha
        outline[o + 3] = alpha
    over(badge, outline)

    # 3. The glyph itself.
    glyph_layer = [0.0] * (box * box * 4)
    for y in range(glyph_h):
        for x in range(glyph_w):
            o = ((y + offset_y) * box + (x + offset_x)) * 4
            g = (y * glyph_w + x) * 4
            alpha = glyph[g + 3] / 255.0
            glyph_layer[o] = glyph[g] * alpha
            glyph_layer[o + 1] = glyph[g + 1] * alpha
            glyph_layer[o + 2] = glyph[g + 2] * alpha
            glyph_layer[o + 3] = alpha
    over(badge, glyph_layer)

    # Downsample the badge to its final size.
    dst_w = box // BADGE_SS
    dst_h = box // BADGE_SS
    out = [0.0] * (dst_w * dst_h * 4)
    for dy in range(dst_h):
        for dx in range(dst_w):
            r = g = b = a = 0.0
            for ky in range(BADGE_SS):
                for kx in range(BADGE_SS):
                    o = ((dy * BADGE_SS + ky) * box + (dx * BADGE_SS + kx)) * 4
                    r += badge[o]
                    g += badge[o + 1]
                    b += badge[o + 2]
                    a += badge[o + 3]
            n = BADGE_SS * BADGE_SS
            o = (dy * dst_w + dx) * 4
            out[o] = r / n
            out[o + 1] = g / n
            out[o + 2] = b / n
            out[o + 3] = a / n
    return out, dst_w, dst_h


def composite_badge(pixels, badge, badge_w, badge_h):
    """Puts the badge in front of the finished avatar."""
    x0 = int(round(BADGE_CENTER[0] - badge_w / 2))
    y0 = int(round(BADGE_CENTER[1] - badge_h / 2))

    for y in range(badge_h):
        py = y0 + y
        if not 0 <= py < SIZE:
            continue
        for x in range(badge_w):
            px = x0 + x
            if not 0 <= px < SIZE:
                continue
            o = (y * badge_w + x) * 4
            alpha = min(1.0, badge[o + 3])
            if alpha <= 0:
                continue
            inv = 1.0 - alpha
            p = (py * SIZE + px) * 3
            pixels[p] = int(pixels[p] * inv + badge[o] * alpha + 0.5)
            pixels[p + 1] = int(pixels[p + 1] * inv + badge[o + 1] * alpha + 0.5)
            pixels[p + 2] = int(pixels[p + 2] * inv + badge[o + 2] * alpha + 0.5)


def downsample(canvas):
    """Box-filter the supersampled canvas down to SIZE x SIZE.

    Returns a flat RGB byte buffer of SIZE * SIZE * 3 bytes.
    """
    out = bytearray()
    block = SCALE * SCALE
    for y in range(SIZE):
        for x in range(SIZE):
            r = g = b = 0
            for dy in range(SCALE):
                row = canvas.data[y * SCALE + dy]
                for dx in range(SCALE):
                    pixel = row[x * SCALE + dx]
                    r += pixel[0]
                    g += pixel[1]
                    b += pixel[2]
            out.append(min(255, r // block))
            out.append(min(255, g // block))
            out.append(min(255, b // block))
    return out


def write_png(path, pixels):
    def chunk(tag, data):
        body = tag + data
        return struct.pack('>I', len(data)) + body + struct.pack('>I', zlib.crc32(body) & 0xFFFFFFFF)

    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)  # PNG filter type 0 (None) for this scanline
        raw += pixels[y * SIZE * 3:(y + 1) * SIZE * 3]

    header = struct.pack('>IIBBBBB', SIZE, SIZE, 8, 2, 0, 0, 0)  # 8-bit truecolor RGB
    png = b'\x89PNG\r\n\x1a\n'
    png += chunk(b'IHDR', header)
    png += chunk(b'IDAT', zlib.compress(bytes(raw), 9))
    png += chunk(b'IEND', b'')
    path.write_bytes(png)


def main():
    for index, renderer in enumerate(SHIELD_RENDERERS):
        canvas = Canvas()
        renderer(canvas)
        pixels = downsample(canvas)

        badge, badge_w, badge_h = build_badge(EMOJI_DIR / EMOJI_FILES[index])
        composite_badge(pixels, badge, badge_w, badge_h)

        path = OUT_DIR / f'avatar_{index + 1:02d}.png'
        write_png(path, pixels)
        print(f'wrote {path.name}')


if __name__ == '__main__':
    main()
