"""
Draws every picture on the site that would otherwise show a person: the four "doctors", the home page hero,
the children's pictures, smiles, braces, the x-ray, the consultation and so on.

Everything here is original artwork made from simple shapes, so nobody is shown (not even a hand or a mouth),
there are no model releases or licences to worry about, and nobody appears under a name that is not theirs.

Run from the DentalClinic folder:   python tools/generate-illustrations.py
Needs Pillow (pip install pillow). Writes the .jpg files into src/DentalClinic/wwwroot/img.
"""
from pathlib import Path
from PIL import Image, ImageChops, ImageDraw

OUT = Path(__file__).resolve().parent.parent / "src" / "DentalClinic" / "wwwroot" / "img"
S = 2  # draw at double size, then shrink: smooth edges without a graphics library

INK = (13, 43, 51)
TEAL = (15, 107, 115)
TEAL_D = (10, 79, 86)
AQUA = (43, 181, 173)
MINT = (231, 246, 243)
CORAL = (255, 122, 89)
SAND = (255, 246, 238)
WHITE = (255, 255, 255)
GUM = (122, 46, 58)


# ------------------------------------------------------------------ small drawing helpers
def bez(p0, p1, p2, p3, n=24):
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        pts.append((u**3 * p0[0] + 3 * u**2 * t * p1[0] + 3 * u * t**2 * p2[0] + t**3 * p3[0],
                    u**3 * p0[1] + 3 * u**2 * t * p1[1] + 3 * u * t**2 * p2[1] + t**3 * p3[1]))
    return pts


def path(start, segments):
    """segments: list of (c1, c2, end) cubic curves from the current point."""
    pts, cur = [], start
    for c1, c2, end in segments:
        pts += bez(cur, c1, c2, end)
        cur = end
    return pts


def gradient(size, top, bottom):
    w, h = size
    img = Image.new("RGB", size)
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return img


def canvas(size, top, bottom):
    """A gradient picture at double size plus its drawing context."""
    img = gradient((size[0] * S, size[1] * S), top, bottom)
    return img, ImageDraw.Draw(img, "RGBA")


def scale(points, ox, oy, k):
    return [(ox + x * k, oy + y * k) for x, y in points]


def tooth_points():
    # a tooth in a 100 x 100 box
    return path((50, 24), [
        ((40, 12), (18, 10), (14, 32)),
        ((11, 48), (22, 58), (24, 74)),
        ((26, 90), (34, 96), (38, 82)),
        ((41, 70), (45, 62), (50, 62)),
        ((55, 62), (59, 70), (62, 82)),
        ((66, 96), (74, 90), (76, 74)),
        ((78, 58), (89, 48), (86, 32)),
        ((82, 10), (60, 12), (50, 24)),
    ])


def draw_tooth(d, ox, oy, k, fill=WHITE, outline=TEAL, width=6, shade=(222, 238, 238)):
    pts = scale(tooth_points(), ox, oy, k)
    d.polygon(pts, fill=fill)
    # a soft shadow on the right and a highlight on the left give it some volume
    d.polygon(scale(path((70, 22), [((84, 30), (86, 46), (80, 60)), ((76, 70), (76, 80), (74, 84)), ((70, 70), (64, 62), (60, 64))]), ox, oy, k), fill=shade)
    if width:
        d.line(pts + [pts[0]], fill=outline, width=width * S, joint="curve")
    d.line(scale(bez((24, 28), (22, 34), (22, 40), (24, 46)), ox, oy, k), fill=(255, 255, 255), width=max(2, int(k * .6)))


def sparkle(d, cx, cy, r, fill):
    d.polygon([(cx, cy - r), (cx + r * .28, cy - r * .28), (cx + r, cy), (cx + r * .28, cy + r * .28),
               (cx, cy + r), (cx - r * .28, cy + r * .28), (cx - r, cy), (cx - r * .28, cy - r * .28)], fill=fill)


def bubbles(d, W, H, spots, outline=(43, 181, 173, 200), fill=(255, 255, 255, 150)):
    for cx, cy, r in spots:
        d.ellipse([W * cx - r * S, H * cy - r * S, W * cx + r * S, H * cy + r * S], fill=fill, outline=outline, width=3 * S)


def blobs(d, W, H, spec):
    for cx, cy, r, a in spec:
        d.ellipse([cx * W - r * W, cy * H - r * W, cx * W + r * W, cy * H + r * W], fill=(255, 255, 255, a))


def finish(img, name, size, quality=88):
    img = img.resize(size, Image.LANCZOS)
    img.save(OUT / f"{name}.jpg", "JPEG", quality=quality, optimize=True)
    print("wrote", name, size)


def face(d, cx, cy, r, cheeks=True):
    d.ellipse([cx - r * .36 - r * .07, cy - r * .05 - r * .1, cx - r * .36 + r * .07, cy - r * .05 + r * .1], fill=INK)
    d.ellipse([cx + r * .36 - r * .07, cy - r * .05 - r * .1, cx + r * .36 + r * .07, cy - r * .05 + r * .1], fill=INK)
    d.arc([cx - r * .3, cy + r * .02, cx + r * .3, cy + r * .5], 20, 160, fill=INK, width=int(r * .07))
    if cheeks:
        for sx in (-1, 1):
            d.ellipse([cx + sx * r * .62 - r * .13, cy + r * .2 - r * .08, cx + sx * r * .62 + r * .13, cy + r * .2 + r * .08], fill=(255, 150, 130, 150))


def toothbrush(d, x, y, length, color=CORAL, bristle=AQUA):
    h = length * .09
    d.rounded_rectangle([x, y, x + length * .62, y + h], radius=h / 2, fill=color)
    d.rounded_rectangle([x + length * .6, y - h * .4, x + length, y + h * 1.4], radius=h * .5, fill=WHITE, outline=TEAL, width=3 * S)
    for i in range(7):
        bx = x + length * .64 + i * length * .05
        d.line([(bx, y - h * .4), (bx, y - h * 1.2)], fill=bristle, width=int(length * .028))


# ------------------------------------------------------------------ a smiling mouth with teeth (no real faces anywhere)
def mouth(img, cx, cy, w, h, lip, extra=None, rows=2):
    """Draws a smile made of simple shapes. `extra(layer_draw, geometry)` can add braces, an aligner and so on on top of the teeth."""
    x0, x1 = cx - w / 2, cx + w / 2
    d = ImageDraw.Draw(img, "RGBA")
    outer = bez((x0, cy), (x0 + w * .22, cy - h * .78), (x1 - w * .22, cy - h * .78), (x1, cy)) \
        + bez((x1, cy), (x1 - w * .18, cy + h * 1.0), (x0 + w * .18, cy + h * 1.0), (x0, cy))
    d.polygon(outer, fill=lip)
    inset = w * .05
    inner = bez((x0 + inset, cy), (x0 + w * .28, cy - h * .45), (x1 - w * .28, cy - h * .45), (x1 - inset, cy)) \
        + bez((x1 - inset, cy), (x1 - w * .26, cy + h * .6), (x0 + w * .26, cy + h * .6), (x0 + inset, cy))

    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.polygon(inner, fill=GUM)
    n = 8
    tw = (w * .82) / n
    top = cy - h * .46
    upper_h = h * .50
    geo = {"x0": x0 + w * .09, "tw": tw, "n": n, "top": top, "upper_h": upper_h, "lower_top": top + upper_h * .97, "lower_h": h * .42}
    # a light band behind the teeth fills the dark gaps between them, so the smile reads as one row of teeth
    ld.rounded_rectangle([geo["x0"], top, geo["x0"] + n * tw, geo["lower_top"] + (geo["lower_h"] if rows == 2 else 0)], radius=tw * .4, fill=(244, 249, 249))
    for i in range(n):
        x = geo["x0"] + i * tw
        ld.rounded_rectangle([x + tw * .04, top, x + tw * .96, top + upper_h], radius=tw * .38, fill=WHITE, outline=(200, 216, 220), width=S)
    if rows == 2:
        for i in range(n):
            x = geo["x0"] + i * tw
            ld.rounded_rectangle([x + tw * .06, geo["lower_top"], x + tw * .94, geo["lower_top"] + geo["lower_h"]], radius=tw * .34, fill=(246, 250, 250), outline=(200, 216, 220), width=S)
    if extra:   # drawn on its own layer and blended on top, so see-through things (an aligner) do not erase the teeth
        overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
        extra(ImageDraw.Draw(overlay), geo)
        layer = Image.alpha_composite(layer, overlay)
        ld = ImageDraw.Draw(layer)
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).polygon(inner, fill=255)
    layer.putalpha(ImageChops.multiply(layer.split()[3], mask))
    img.paste(layer, (0, 0), layer)


# ------------------------------------------------------------------ hero (home page)
def hero():
    size = (1800, 1350)
    img, d = canvas(size, (233, 247, 244), (180, 228, 223))
    W, H = img.size
    blobs(d, W, H, [(0.15, 0.2, 0.34, 70), (0.9, 0.12, 0.26, 60), (0.82, 0.85, 0.42, 80), (0.05, 0.9, 0.22, 60)])
    d.ellipse([W * .22, H * .80, W * .78, H * .90], fill=(10, 79, 86, 38))   # soft ground shadow
    draw_tooth(d, W * .21, H * .12, W * .0058, width=9)
    for cx, cy, r, c in [(.22, .24, 60, WHITE), (.80, .32, 84, CORAL), (.74, .62, 50, WHITE), (.14, .62, 44, AQUA), (.86, .18, 38, WHITE)]:
        sparkle(d, W * cx, H * cy, r * S, c)
    for cx, cy in [(.3, .10), (.7, .08), (.9, .5), (.1, .45), (.55, .93)]:
        d.ellipse([W * cx - 14 * S, H * cy - 14 * S, W * cx + 14 * S, H * cy + 14 * S], outline=(15, 107, 115, 140), width=5 * S)
    finish(img, "hero", size)


# ------------------------------------------------------------------ children
def kids():
    size = (1200, 800)
    img, d = canvas(size, (255, 244, 232), (255, 222, 205))
    W, H = img.size
    d.ellipse([-W * .1, H * .55, W * .5, H * 1.4], fill=(255, 255, 255, 90))
    d.ellipse([W * .55, -H * .3, W * 1.1, H * .5], fill=(43, 181, 173, 50))
    d.ellipse([W * .3, H * .86, W * .7, H * .96], fill=(10, 79, 86, 30))
    draw_tooth(d, W * .30, H * .06, W * .0036 * 1.35, width=7)
    face(d, W * .50, H * .43, W * .095)
    toothbrush(d, W * .56, H * .56, W * .26)
    bubbles(d, W, H, [(.30, .25, 30), (.72, .30, 22), (.78, .72, 26), (.2, .72, 20), (.62, .14, 18)])
    sparkle(d, W * .24, H * .42, 40 * S, CORAL)
    sparkle(d, W * .80, H * .46, 32 * S, AQUA)
    finish(img, "kids", size)

    img, d = canvas(size, (222, 244, 241), (186, 230, 225))
    d.ellipse([W * .6, H * .5, W * 1.2, H * 1.4], fill=(255, 255, 255, 90))
    d.ellipse([-W * .1, -H * .3, W * .4, H * .4], fill=(255, 255, 255, 80))
    d.polygon([(W * .26, H * .30), (W * .46, H * .30), (W * .44, H * .74), (W * .28, H * .74)], fill=WHITE, outline=TEAL)   # toothpaste tube
    d.polygon([(W * .26, H * .30), (W * .46, H * .30), (W * .45, H * .40), (W * .27, H * .40)], fill=TEAL)
    d.rounded_rectangle([W * .32, H * .24, W * .40, H * .31], radius=14 * S, fill=CORAL)
    d.line([(W * .26, H * .74), (W * .44, H * .74)], fill=TEAL, width=4 * S)
    draw_tooth(d, W * .295, H * .45, W * .0017, fill=MINT, width=4)
    toothbrush(d, W * .50, H * .62, W * .30, color=TEAL)
    d.rounded_rectangle([W * .685, H * .585, W * .79, H * .625], radius=20 * S, fill=(255, 255, 255, 235))
    bubbles(d, W, H, [(.62, .32, 44), (.7, .22, 28), (.78, .36, 34), (.55, .18, 20), (.86, .5, 22), (.18, .52, 24)], outline=(15, 107, 115, 150))
    sparkle(d, W * .12, H * .22, 38 * S, WHITE)
    sparkle(d, W * .90, H * .24, 44 * S, CORAL)
    finish(img, "kids-2", size)


# ------------------------------------------------------------------ people drawn as avatars (doctors, patients)
def person(size, skin, hair, bg_top, bg_bottom, top_color, style, glasses=False, beard=False, outfit="coat"):
    img, d = canvas(size, bg_top, bg_bottom)
    W, H = img.size
    d.ellipse([-W * .2, H * .45, W * .6, H * 1.1], fill=(255, 255, 255, 40))
    d.ellipse([W * .5, -H * .1, W * 1.2, H * .5], fill=(255, 255, 255, 30))
    cx = W / 2

    if style in ("long", "bob"):   # long hair goes behind the shoulders
        d.rounded_rectangle([cx - W * .21, H * .20, cx + W * .21, H * (.72 if style == "long" else .58)], radius=int(W * .2), fill=hair)

    shoulders = path((W * .02, H), [((W * .02, H * .78), (W * .12, H * .66), (W * .30, H * .63)),
                                    ((W * .38, H * .615), (W * .44, H * .60), (cx, H * .60)),
                                    ((W * .56, H * .60), (W * .62, H * .615), (W * .70, H * .63)),
                                    ((W * .88, H * .66), (W * .98, H * .78), (W * .98, H))])
    if outfit == "coat":
        d.polygon(shoulders, fill=(250, 252, 252))
        d.polygon(shoulders, outline=(205, 224, 224))
        d.polygon([(cx - W * .13, H * .60), (cx + W * .13, H * .60), (cx, H * .80)], fill=top_color)
        d.polygon([(cx - W * .13, H * .60), (cx - W * .045, H * .60), (cx, H * .70), (cx - W * .01, H)], fill=(235, 243, 243))
        d.polygon([(cx + W * .13, H * .60), (cx + W * .045, H * .60), (cx, H * .70), (cx + W * .01, H)], fill=(235, 243, 243))
        d.line([(cx, H * .80), (cx, H)], fill=(215, 230, 230), width=3 * S)
        d.rounded_rectangle([cx + W * .17, H * .76, cx + W * .30, H * .79], radius=6 * S, fill=(255, 255, 255), outline=AQUA, width=2 * S)   # name tag without text
    else:   # a plain coloured top for patients
        d.polygon(shoulders, fill=top_color)
        d.arc([cx - W * .11, H * .55, cx + W * .11, H * .70], 0, 180, fill=tuple(int(c * .8) for c in top_color), width=int(W * .012))

    d.rounded_rectangle([cx - W * .06, H * .50, cx + W * .06, H * .62], radius=int(W * .04), fill=tuple(int(c * .93) for c in skin))
    hw, hh = W * .15, H * .165
    hy = H * .40
    d.ellipse([cx - hw, hy - hh, cx + hw, hy + hh], fill=skin)
    d.ellipse([cx - hw - W * .012, hy - hh * .1, cx - hw + W * .018, hy + hh * .22], fill=skin)   # ears
    d.ellipse([cx + hw - W * .018, hy - hh * .1, cx + hw + W * .012, hy + hh * .22], fill=skin)

    def curtains(color, outer=1.12, low=.55):
        for sx in (-1, 1):
            d.polygon([(cx + sx * hw * outer, hy - hh * .35), (cx + sx * hw * .74, hy - hh * .92),
                       (cx + sx * hw * .68, hy - hh * .22), (cx + sx * hw * 1.0, hy + hh * low)], fill=color)

    if style == "short":
        d.pieslice([cx - hw * 1.06, hy - hh * 1.12, cx + hw * 1.06, hy + hh * .5], 180, 360, fill=hair)
    elif style == "side":
        d.pieslice([cx - hw * 1.08, hy - hh * 1.18, cx + hw * 1.08, hy + hh * .55], 180, 360, fill=hair)
        d.polygon([(cx - hw * 1.0, hy - hh * .3), (cx + hw * .3, hy - hh * 1.0), (cx + hw * 1.0, hy - hh * .4), (cx + hw * 1.04, hy - hh * .12), (cx - hw * .1, hy - hh * .66)], fill=hair)
    elif style == "bob":
        d.pieslice([cx - hw * 1.12, hy - hh * 1.14, cx + hw * 1.12, hy + hh * .6], 180, 360, fill=hair)
        curtains(hair, 1.14, .5)
    elif style == "long":
        d.pieslice([cx - hw * 1.08, hy - hh * 1.14, cx + hw * 1.08, hy + hh * .5], 180, 360, fill=hair)
        curtains(hair, 1.12, .7)
    d.ellipse([cx - hw * .86, hy - hh * .64, cx + hw * .86, hy + hh * .22], fill=skin)   # a rounded hairline

    if beard:
        outer = bez((cx - hw * .95, hy + hh * .05), (cx - hw * .95, hy + hh * .9), (cx - hw * .45, hy + hh * 1.15), (cx, hy + hh * 1.15)) \
            + bez((cx, hy + hh * 1.15), (cx + hw * .45, hy + hh * 1.15), (cx + hw * .95, hy + hh * .9), (cx + hw * .95, hy + hh * .05))
        inner = bez((cx + hw * .95, hy + hh * .05), (cx + hw * .85, hy + hh * .35), (cx + hw * .55, hy + hh * .42), (cx + hw * .3, hy + hh * .42)) \
            + bez((cx + hw * .3, hy + hh * .42), (cx + hw * .1, hy + hh * .36), (cx - hw * .1, hy + hh * .36), (cx - hw * .3, hy + hh * .42)) \
            + bez((cx - hw * .3, hy + hh * .42), (cx - hw * .55, hy + hh * .42), (cx - hw * .85, hy + hh * .35), (cx - hw * .95, hy + hh * .05))
        d.polygon(outer + inner, fill=hair)
        d.ellipse([cx - hw * .27, hy + hh * .47, cx + hw * .27, hy + hh * .70], fill=tuple(min(255, int(c * 1.12)) for c in skin))

    eye_y = hy - hh * .02
    for sx in (-1, 1):
        ex = cx + sx * hw * .42
        d.ellipse([ex - W * .012, eye_y - H * .010, ex + W * .012, eye_y + H * .010], fill=INK)
        d.line([(ex - W * .026, eye_y - H * .030), (ex + W * .026, eye_y - H * .036)] if sx > 0 else [(ex - W * .026, eye_y - H * .036), (ex + W * .026, eye_y - H * .030)], fill=hair, width=int(W * .007))
    d.line(bez((cx, eye_y + H * .005), (cx - W * .008, eye_y + H * .035), (cx - W * .006, eye_y + H * .05), (cx + W * .006, eye_y + H * .055)), fill=tuple(int(c * .78) for c in skin), width=int(W * .005))
    if not beard:
        d.arc([cx - hw * .34, hy + hh * .22, cx + hw * .34, hy + hh * .62], 15, 165, fill=(150, 70, 70), width=int(W * .007))
    else:
        d.arc([cx - hw * .2, hy + hh * .46, cx + hw * .2, hy + hh * .68], 15, 165, fill=(150, 70, 70), width=int(W * .007))
    for sx in (-1, 1):
        d.ellipse([cx + sx * hw * .62 - W * .02, hy + hh * .22 - H * .008, cx + sx * hw * .62 + W * .02, hy + hh * .22 + H * .008], fill=(255, 140, 120, 90))
    if glasses:
        for sx in (-1, 1):
            ex = cx + sx * hw * .42
            d.ellipse([ex - W * .042, eye_y - H * .028, ex + W * .042, eye_y + H * .028], outline=INK, width=int(W * .0065))
        d.line([(cx - hw * .42 + W * .042, eye_y), (cx + hw * .42 - W * .042, eye_y)], fill=INK, width=int(W * .0055))

    if outfit == "coat":   # stethoscope
        d.arc([cx - W * .20, H * .64, cx + W * .20, H * .90], 20, 160, fill=INK, width=int(W * .012))
        d.ellipse([cx + W * .17, H * .83, cx + W * .215, H * .875], fill=(160, 175, 180), outline=INK, width=int(W * .005))
    return img.resize(size, Image.LANCZOS)


DOCTORS = {
    "lejla": dict(skin=(240, 200, 168), hair=(46, 30, 24), bg_top=(208, 238, 235), bg_bottom=(150, 214, 208), top_color=TEAL, style="long"),
    "tarik": dict(skin=(214, 160, 124), hair=(28, 24, 24), bg_top=(221, 236, 245), bg_bottom=(160, 200, 222), top_color=TEAL_D, style="short", beard=True),
    "mia": dict(skin=(246, 212, 186), hair=(118, 66, 42), bg_top=(255, 232, 222), bg_bottom=(255, 196, 176), top_color=AQUA, style="bob"),
    "adnan": dict(skin=(198, 140, 99), hair=(34, 30, 30), bg_top=(226, 240, 224), bg_bottom=(170, 214, 176), top_color=TEAL, style="side", glasses=True),
}
PATIENTS = [
    dict(skin=(236, 190, 160), hair=(70, 44, 30), bg_top=(255, 240, 226), bg_bottom=(255, 214, 190), top_color=(255, 150, 120), style="bob", outfit="shirt"),
    dict(skin=(205, 150, 112), hair=(25, 22, 22), bg_top=(232, 238, 250), bg_bottom=(190, 205, 235), top_color=(95, 125, 190), style="short", outfit="shirt"),
]


def doctor(name):
    finish_image(person((700, 875), **DOCTORS[name]), f"doctor-{name}")


def finish_image(img, name, quality=88):
    img.save(OUT / f"{name}.jpg", "JPEG", quality=quality, optimize=True)
    print("wrote", name, img.size)


def circle_crop(img, diameter, ring=(255, 255, 255), ring_w=10):
    img = img.copy()
    w, h = img.size
    side = min(w, h)
    img = img.crop(((w - side) // 2, int(h * .08), (w - side) // 2 + side, int(h * .08) + side)).resize((diameter, diameter), Image.LANCZOS)
    mask = Image.new("L", (diameter * 4, diameter * 4), 0)
    ImageDraw.Draw(mask).ellipse([0, 0, diameter * 4 - 1, diameter * 4 - 1], fill=255)
    mask = mask.resize((diameter, diameter), Image.LANCZOS)
    out = Image.new("RGBA", (diameter + ring_w * 2, diameter + ring_w * 2), (0, 0, 0, 0))
    ImageDraw.Draw(out).ellipse([0, 0, out.width - 1, out.height - 1], fill=ring + (255,))
    out.paste(img, (ring_w, ring_w), mask)
    return out


def team_work():
    size = (1200, 1800)
    img, d = canvas(size, (233, 247, 244), (170, 222, 216))
    W, H = img.size
    blobs(d, W, H, [(0.15, 0.12, 0.3, 60), (0.9, 0.5, 0.3, 50), (0.2, 0.9, 0.35, 60)])
    draw_tooth(d, W * .38, H * .06, W * .0025, width=6)
    for cx, cy, r, c in [(.2, .12, 44, WHITE), (.82, .16, 52, CORAL), (.86, .52, 36, WHITE), (.12, .55, 40, AQUA)]:
        sparkle(d, W * cx, H * cy, r * S, c)
    base = img.resize(size, Image.LANCZOS).convert("RGBA")
    for i, name in enumerate(["lejla", "tarik", "mia", "adnan"]):
        crop = circle_crop(person((700, 875), **DOCTORS[name]), 470)
        x = 120 + (i % 2) * 520
        y = 560 + (i // 2) * 600
        base.paste(crop, (x, y), crop)
    finish_image(base.convert("RGB"), "team-work")


def consult():
    size = (1200, 900)
    img, d = canvas(size, (233, 247, 244), (186, 230, 225))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.2, 0.3, 60), (0.92, 0.8, 0.34, 60)])
    base = img.resize(size, Image.LANCZOS).convert("RGBA")
    doc = circle_crop(person((700, 875), **DOCTORS["lejla"]), 380)
    pat = circle_crop(person((700, 875), **PATIENTS[1]), 380)
    base.paste(doc, (90, 330), doc)
    base.paste(pat, (730, 330), pat)
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    ld.rounded_rectangle([430, 120, 780, 300], radius=60, fill=WHITE, outline=TEAL, width=6)
    ld.polygon([(520, 295), (500, 365), (580, 298)], fill=WHITE, outline=TEAL)
    ld.rectangle([512, 288, 585, 300], fill=WHITE)
    base = Image.alpha_composite(base, layer)
    big = base.resize((size[0] * S, size[1] * S), Image.LANCZOS)
    bd = ImageDraw.Draw(big, "RGBA")
    draw_tooth(bd, 560 * S, 138 * S, 1.45 * S, width=4)
    sparkle(bd, 700 * S, 170 * S, 22 * S, CORAL)
    finish_image(big.resize(size, Image.LANCZOS).convert("RGB"), "consult")


# ------------------------------------------------------------------ smiles, braces, aligner, veneers (made of shapes, no faces)
def smiles():
    # smile-1: warm and bright, whitening
    size = (1200, 800)
    img, d = canvas(size, (255, 240, 226), (255, 214, 190))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.9, 0.3, 80), (0.9, 0.1, 0.25, 70)])
    mouth(img, W * .5, H * .52, W * .58, H * .30, (230, 96, 96))
    d = ImageDraw.Draw(img, "RGBA")
    for cx, cy, r, c in [(.26, .30, 56, WHITE), (.76, .27, 44, CORAL), (.82, .68, 36, WHITE), (.18, .66, 30, AQUA)]:
        sparkle(d, W * cx, H * cy, r * S, c)
    finish(img, "smile-1", size)

    # smile-2: cool mint
    size = (1000, 667)
    img, d = canvas(size, (230, 246, 243), (190, 230, 226))
    W, H = img.size
    blobs(d, W, H, [(0.85, 0.2, 0.3, 80), (0.05, 0.9, 0.25, 70)])
    mouth(img, W * .5, H * .53, W * .60, H * .32, (214, 120, 140))
    d = ImageDraw.Draw(img, "RGBA")
    sparkle(d, W * .2, H * .25, 40 * S, WHITE)
    sparkle(d, W * .82, H * .72, 34 * S, TEAL)
    finish(img, "smile-2", size)

    # smile-3: tall, with tooth icons above and below
    size = (1000, 1500)
    img, d = canvas(size, (214, 240, 237), (150, 214, 208))
    W, H = img.size
    blobs(d, W, H, [(0.2, 0.15, 0.3, 70), (0.9, 0.85, 0.35, 70), (0.1, 0.7, 0.2, 50)])
    draw_tooth(d, W * .36, H * .08, W * .0028, width=6)
    mouth(img, W * .5, H * .50, W * .68, H * .17, (196, 80, 96))
    d = ImageDraw.Draw(img, "RGBA")
    draw_tooth(d, W * .12, H * .72, W * .0016, width=4)
    draw_tooth(d, W * .66, H * .74, W * .0016, width=4)
    for cx, cy, r, c in [(.25, .35, 40, WHITE), (.8, .3, 46, CORAL), (.5, .86, 36, WHITE)]:
        sparkle(d, W * cx, H * cy, r * S, c)
    finish(img, "smile-3", size)

    # smile-4: deep teal with a bold red smile
    size = (1000, 800)
    img, d = canvas(size, (22, 84, 92), (10, 53, 60))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.1, 0.3, 30), (0.9, 0.9, 0.35, 30)])
    mouth(img, W * .5, H * .52, W * .62, H * .36, (232, 64, 70))
    d = ImageDraw.Draw(img, "RGBA")
    for cx, cy, r, c in [(.2, .28, 50, (255, 255, 255, 230)), (.82, .26, 40, (255, 150, 120, 255)), (.84, .74, 30, (255, 255, 255, 200))]:
        sparkle(d, W * cx, H * cy, r * S, c)
    finish(img, "smile-4", size)


def braces():
    size = (1200, 800)
    img, d = canvas(size, (231, 246, 243), (190, 230, 225))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.2, 0.3, 70), (0.9, 0.85, 0.3, 60)])

    def brackets(ld, g):
        for row_top, row_h in [(g["top"], g["upper_h"]), (g["lower_top"], g["lower_h"])]:
            yc = row_top + row_h * .52
            ld.line([(g["x0"] + g["tw"] * .2, yc), (g["x0"] + g["tw"] * (g["n"] - .2), yc)], fill=(90, 105, 112), width=3 * S)
            for i in range(g["n"]):
                x = g["x0"] + i * g["tw"] + g["tw"] * .5
                s = g["tw"] * .17
                ld.rounded_rectangle([x - s, yc - s, x + s, yc + s], radius=s * .3, fill=(190, 205, 212), outline=(90, 105, 112), width=S)
    mouth(img, W * .5, H * .52, W * .66, H * .34, (232, 120, 120), extra=brackets)
    d = ImageDraw.Draw(img, "RGBA")
    sparkle(d, W * .17, H * .28, 40 * S, WHITE)
    sparkle(d, W * .85, H * .30, 34 * S, CORAL)
    finish(img, "braces-girl", size)


def aligners():
    # aligner: teeth in a mouth with a clear tray over them
    size = (1200, 800)
    img, d = canvas(size, (255, 244, 220), (255, 224, 170))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.9, 0.3, 80), (0.9, 0.1, 0.25, 70)])

    def tray(ld, g):
        for row_top, row_h in [(g["top"], g["upper_h"]), (g["lower_top"], g["lower_h"])]:
            ld.rounded_rectangle([g["x0"] - g["tw"] * .04, row_top - row_h * .06, g["x0"] + g["tw"] * g["n"] + g["tw"] * .04, row_top + row_h * 1.04],
                                 radius=g["tw"] * .5, fill=(170, 215, 245, 70), outline=(110, 170, 215, 255), width=3 * S)
    mouth(img, W * .5, H * .52, W * .64, H * .34, (232, 110, 110), extra=tray)
    d = ImageDraw.Draw(img, "RGBA")
    sparkle(d, W * .18, H * .26, 44 * S, WHITE)
    sparkle(d, W * .84, H * .72, 36 * S, TEAL)
    finish(img, "aligner", size)

    # aligner-case: a case with the tray inside
    size = (1000, 714)
    img, d = canvas(size, (214, 236, 250), (180, 214, 240))
    W, H = img.size
    blobs(d, W, H, [(0.85, 0.15, 0.3, 70), (0.05, 0.9, 0.25, 70)])
    d.ellipse([W * .18, H * .78, W * .82, H * .92], fill=(10, 79, 86, 36))
    d.rounded_rectangle([W * .2, H * .38, W * .8, H * .8], radius=90 * S, fill=WHITE, outline=(190, 210, 225), width=4 * S)          # base
    d.rounded_rectangle([W * .25, H * .43, W * .75, H * .70], radius=70 * S, fill=(236, 246, 250))                                   # inside
    for i in range(7):                                                                                                               # the tray
        x = W * .31 + i * W * .063
        d.rounded_rectangle([x, H * .47, x + W * .055, H * .63], radius=18 * S, fill=(170, 215, 245, 120), outline=(110, 170, 215), width=2 * S)
    lid = path((W * .2, H * .40), [((W * .2, H * .10), (W * .3, H * .04), (W * .5, H * .04)), ((W * .7, H * .04), (W * .8, H * .10), (W * .8, H * .40))])
    d.polygon(lid, fill=(250, 252, 253), outline=(190, 210, 225))
    d.line(lid, fill=(190, 210, 225), width=4 * S)
    d.rounded_rectangle([W * .45, H * .34, W * .55, H * .40], radius=14 * S, fill=AQUA)                                              # hinge
    d.rounded_rectangle([W * .86, H * .48, W * .93, H * .8], radius=22 * S, fill=WHITE, outline=(190, 210, 225), width=3 * S)         # little bottle
    d.rounded_rectangle([W * .875, H * .42, W * .915, H * .49], radius=8 * S, fill=TEAL)
    sparkle(d, W * .12, H * .25, 36 * S, WHITE)
    sparkle(d, W * .9, H * .22, 30 * S, CORAL)
    finish(img, "aligner-case", size)


def veneers():
    size = (1200, 960)
    img, d = canvas(size, (60, 70, 78), (28, 36, 42))
    W, H = img.size
    d.ellipse([W * .1, H * .1, W * .9, H * 1.1], fill=(255, 255, 255, 14))
    # left: a worn tooth, right: the same tooth with a bright shell on top
    draw_tooth(d, W * .08, H * .20, W * .0030, fill=(226, 205, 160), outline=(150, 128, 90), width=5, shade=(205, 182, 135))
    draw_tooth(d, W * .52, H * .26, W * .0030, fill=(226, 205, 160), outline=(150, 128, 90), width=5, shade=(205, 182, 135))
    draw_tooth(d, W * .54, H * .12, W * .0030, fill=(255, 255, 255), outline=(150, 220, 230), width=5, shade=(226, 240, 244))
    d.polygon([(W * .405, H * .50), (W * .47, H * .50), (W * .47, H * .46), (W * .53, H * .53), (W * .47, H * .60), (W * .47, H * .56), (W * .405, H * .56)], fill=(255, 255, 255, 230))   # "before -> after" arrow
    for cx, cy, r, c in [(.80, .15, 50, WHITE), (.9, .5, 34, CORAL), (.60, .08, 30, AQUA)]:
        sparkle(d, W * cx, H * cy, r * S, c)
    finish(img, "veneers", size)


# ------------------------------------------------------------------ x-ray, instruments, treatments
def xray():
    size = (1200, 800)
    img, d = canvas(size, (8, 24, 40), (14, 44, 66))
    W, H = img.size
    d.ellipse([W * .1, H * .0, W * .9, H * 1.0], fill=(120, 190, 230, 22))
    for row, (cy, flip) in enumerate([(H * .38, 1), (H * .66, -1)]):   # upper and lower jaw
        for i in range(8):
            t = (i - 3.5) / 3.5
            x = W * .5 + t * W * .36
            y = cy + abs(t) ** 2 * H * .06 * flip
            k = W * .0013 * (1.0 - abs(t) * .18)
            pts = scale(tooth_points(), x - 50 * k, (y - 40 * k) if flip == 1 else (y - 60 * k), k)
            if flip == -1:   # the lower teeth point the other way
                pts = [(px, 2 * y - py) for px, py in pts]
            d.polygon(pts, fill=(170, 215, 240, 150), outline=(220, 240, 255, 230))
            d.line(pts + [pts[0]], fill=(220, 240, 255, 230), width=3 * S)
    d.arc([W * .05, H * .02, W * .95, H * .98], 195, 345, fill=(150, 200, 230, 130), width=8 * S)
    d.arc([W * .05, H * .02, W * .95, H * .98], 15, 165, fill=(150, 200, 230, 130), width=8 * S)
    for x, y in [(.05, .06), (.95, .06), (.05, .94), (.95, .94)]:   # corner marks like a scan
        d.line([(W * x - 30 * S * (1 if x < .5 else -1), H * y), (W * x, H * y), (W * x, H * y - 30 * S * (1 if y > .5 else -1) * -1)], fill=(120, 190, 230, 200), width=3 * S)
    sparkle(d, W * .86, H * .2, 26 * S, (255, 255, 255, 220))
    finish(img, "xray", size)


def instruments(d, W, H, x, y, scale_k=1.0):
    """A dental mirror and a probe lying next to each other."""
    k = scale_k
    d.line([(x, y + 520 * k), (x + 40 * k, y + 120 * k)], fill=(150, 165, 172), width=int(14 * k))
    d.ellipse([x + 10 * k, y - 40 * k, x + 110 * k, y + 70 * k], fill=(214, 235, 245), outline=(120, 140, 150), width=int(6 * k))
    d.ellipse([x + 28 * k, y - 22 * k, x + 70 * k, y + 20 * k], fill=(255, 255, 255, 190))
    d.line([(x + 180 * k, y + 540 * k), (x + 170 * k, y + 140 * k), (x + 215 * k, y + 70 * k), (x + 235 * k, y + 78 * k)], fill=(150, 165, 172), width=int(12 * k), joint="curve")
    d.rounded_rectangle([x + 150 * k, y + 380 * k, x + 205 * k, y + 560 * k], radius=int(20 * k), fill=(15, 107, 115))


def exam():
    size = (1200, 900)
    img, d = canvas(size, (231, 246, 243), (186, 230, 225))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.2, 0.3, 70), (0.9, 0.9, 0.35, 70)])
    d.ellipse([W * .3, H * .80, W * .74, H * .90], fill=(10, 79, 86, 36))
    draw_tooth(d, W * .36, H * .10, W * .0034, width=7)
    # a mirror and a probe examining the tooth
    d.line([(W * .84, H * .92), (W * .66, H * .50)], fill=(150, 165, 172), width=int(W * .011))
    d.ellipse([W * .60, H * .34, W * .72, H * .50], fill=(214, 235, 245), outline=(120, 140, 150), width=int(W * .004))
    d.ellipse([W * .62, H * .365, W * .665, H * .415], fill=(255, 255, 255, 200))
    d.line([(W * .12, H * .95), (W * .30, H * .52), (W * .37, H * .44)], fill=(150, 165, 172), width=int(W * .009))
    d.line([(W * .37, H * .44), (W * .395, H * .455)], fill=(150, 165, 172), width=int(W * .009))
    sparkle(d, W * .2, H * .22, 46 * S, WHITE)
    sparkle(d, W * .84, H * .22, 36 * S, CORAL)
    finish(img, "exam", size)


def tools():
    size = (1000, 1500)
    img, d = canvas(size, (233, 247, 244), (170, 222, 216))
    W, H = img.size
    blobs(d, W, H, [(0.2, 0.1, 0.3, 60), (0.9, 0.8, 0.4, 60)])
    d.rounded_rectangle([W * .12, H * .12, W * .88, H * .90], radius=60 * S, fill=(255, 255, 255, 190), outline=(150, 200, 200, 255), width=5 * S)
    instruments(d, W, H, W * .27, H * .22, scale_k=S * 1.25)
    d.line([(W * .66, H * .74), (W * .74, H * .30)], fill=(150, 165, 172), width=int(W * .012))   # tweezers
    d.line([(W * .73, H * .74), (W * .78, H * .30)], fill=(150, 165, 172), width=int(W * .012))
    sparkle(d, W * .78, H * .16, 36 * S, CORAL)
    sparkle(d, W * .20, H * .84, 30 * S, AQUA)
    finish(img, "tools", size)


def cleaning():
    size = (1200, 800)
    img, d = canvas(size, (222, 244, 241), (176, 226, 221))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.2, 0.3, 70), (0.9, 0.9, 0.35, 70)])
    d.ellipse([W * .30, H * .84, W * .70, H * .94], fill=(10, 79, 86, 34))
    draw_tooth(d, W * .30, H * .08, W * .0037, width=7)
    # scaler tool with a spray of bubbles
    d.line([(W * .88, H * .90), (W * .66, H * .52), (W * .60, H * .42)], fill=(150, 165, 172), width=int(W * .012), joint="curve")
    d.rounded_rectangle([W * .80, H * .66, W * .90, H * .94], radius=int(W * .02), fill=TEAL)
    bubbles(d, W, H, [(.58, .36, 18), (.54, .30, 12), (.62, .30, 14), (.50, .38, 10), (.66, .38, 10), (.46, .46, 12), (.34, .62, 16)], outline=(15, 107, 115, 160))
    for cx, cy, r, c in [(.22, .26, 48, WHITE), (.78, .22, 40, CORAL), (.18, .62, 34, AQUA), (.80, .50, 28, WHITE)]:
        sparkle(d, W * cx, H * cy, r * S, c)
    finish(img, "cleaning", size)


def extraction():
    size = (1000, 1500)
    img, d = canvas(size, (233, 247, 244), (186, 228, 223))
    W, H = img.size
    blobs(d, W, H, [(0.15, 0.15, 0.3, 60), (0.9, 0.7, 0.4, 60)])
    d.ellipse([W * .28, H * .80, W * .72, H * .86], fill=(10, 79, 86, 34))
    draw_tooth(d, W * .27, H * .34, W * .0046, width=8)
    # dashed outline: the place where the tooth was
    pts = scale(tooth_points(), W * .27, H * .08, W * .0046 * S / S)
    for i in range(0, len(pts) - 1, 2):
        d.line([pts[i], pts[i + 1]], fill=(15, 107, 115, 200), width=5 * S)
    # forceps
    d.line([(W * .46, H * .30), (W * .40, H * .70)], fill=(150, 165, 172), width=int(W * .018))
    d.line([(W * .54, H * .30), (W * .60, H * .70)], fill=(150, 165, 172), width=int(W * .018))
    d.line([(W * .46, H * .30), (W * .54, H * .30)], fill=(150, 165, 172), width=int(W * .02))
    d.rounded_rectangle([W * .36, H * .70, W * .44, H * .90], radius=int(W * .02), fill=TEAL)
    d.rounded_rectangle([W * .56, H * .70, W * .64, H * .90], radius=int(W * .02), fill=TEAL)
    sparkle(d, W * .2, H * .20, 40 * S, WHITE)
    sparkle(d, W * .82, H * .28, 34 * S, CORAL)
    finish(img, "extraction", size)


def brushing():
    size = (1200, 800)
    img, d = canvas(size, (255, 240, 226), (255, 212, 186))
    W, H = img.size
    blobs(d, W, H, [(0.1, 0.9, 0.3, 80), (0.9, 0.1, 0.25, 70)])
    d.ellipse([W * .22, H * .82, W * .62, H * .92], fill=(10, 79, 86, 30))
    draw_tooth(d, W * .24, H * .12, W * .0034, width=7)
    toothbrush(d, W * .40, H * .56, W * .46, color=(230, 60, 60))
    d.rounded_rectangle([W * .685, H * .515, W * .80, H * .575], radius=24 * S, fill=(255, 255, 255, 240))   # paste
    bubbles(d, W, H, [(.60, .34, 40), (.68, .24, 26), (.76, .40, 32), (.54, .22, 18), (.84, .56, 22), (.14, .5, 26)], outline=(230, 100, 90, 160))
    sparkle(d, W * .20, H * .24, 40 * S, WHITE)
    sparkle(d, W * .86, H * .26, 36 * S, TEAL)
    finish(img, "brushing", size)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    hero()
    kids()
    for name in DOCTORS:
        doctor(name)
    team_work()
    consult()
    smiles()
    braces()
    aligners()
    veneers()
    xray()
    exam()
    tools()
    cleaning()
    extraction()
    brushing()
