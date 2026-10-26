"""
Draws the illustrations that replace the stock photos which showed identifiable people
(the four "doctors", the home page hero and the two children's pictures).

Everything here is original artwork made from simple shapes, so there are no model releases or
licences to worry about, and nobody is shown under a name that is not theirs.

Run from the DentalClinic folder:   python tools/generate-illustrations.py
Needs Pillow (pip install pillow). Writes the .jpg files into src/DentalClinic/wwwroot/img.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

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
    d.polygon([(x + 0, y) for x, y in scale(path((70, 22), [((84, 30), (86, 46), (80, 60)), ((76, 70), (76, 80), (74, 84)), ((70, 70), (64, 62), (60, 64))]), ox, oy, k)], fill=shade)
    d.line(pts + [pts[0]], fill=outline, width=width * S, joint="curve")
    d.line(scale(bez((24, 28), (22, 34), (22, 40), (24, 46)), ox, oy, k), fill=(255, 255, 255), width=5 * S)


def sparkle(d, cx, cy, r, fill):
    d.polygon([(cx, cy - r), (cx + r * .28, cy - r * .28), (cx + r, cy), (cx + r * .28, cy + r * .28),
               (cx, cy + r), (cx - r * .28, cy + r * .28), (cx - r, cy), (cx - r * .28, cy - r * .28)], fill=fill)


def finish(img, name, size, quality=88):
    img = img.resize(size, Image.LANCZOS)
    img.save(OUT / f"{name}.jpg", "JPEG", quality=quality, optimize=True)
    print("wrote", name, size)


# ------------------------------------------------------------------ hero (home page)
def hero():
    size = (1800, 1350)
    W, H = size[0] * S, size[1] * S
    img = gradient((W, H), (233, 247, 244), (180, 228, 223))
    d = ImageDraw.Draw(img, "RGBA")
    for cx, cy, r, a in [(0.15, 0.2, 0.34, 70), (0.9, 0.12, 0.26, 60), (0.82, 0.85, 0.42, 80), (0.05, 0.9, 0.22, 60)]:
        d.ellipse([cx * W - r * W, cy * H - r * W, cx * W + r * W, cy * H + r * W], fill=(255, 255, 255, a))
    # soft ground shadow
    d.ellipse([W * .22, H * .80, W * .78, H * .90], fill=(10, 79, 86, 38))
    draw_tooth(d, W * .21, H * .12, W * .0058 * 1.0 * (H / W) * (W / H), width=9)
    for cx, cy, r, c in [(.22, .24, 60, WHITE), (.80, .32, 84, CORAL), (.74, .62, 50, WHITE), (.14, .62, 44, AQUA), (.86, .18, 38, WHITE)]:
        sparkle(d, W * cx, H * cy, r * S, c)
    for cx, cy in [(.3, .10), (.7, .08), (.9, .5), (.1, .45), (.55, .93)]:
        d.ellipse([W * cx - 14 * S, H * cy - 14 * S, W * cx + 14 * S, H * cy + 14 * S], outline=(15, 107, 115, 140), width=5 * S)
    finish(img, "hero", size)


# ------------------------------------------------------------------ children
def face(d, cx, cy, r, cheeks=True):
    d.ellipse([cx - r * .36 - r * .07, cy - r * .05 - r * .1, cx - r * .36 + r * .07, cy - r * .05 + r * .1], fill=INK)
    d.ellipse([cx + r * .36 - r * .07, cy - r * .05 - r * .1, cx + r * .36 + r * .07, cy - r * .05 + r * .1], fill=INK)
    d.arc([cx - r * .3, cy + r * .02, cx + r * .3, cy + r * .5], 20, 160, fill=INK, width=int(r * .07))
    if cheeks:
        for sx in (-1, 1):
            d.ellipse([cx + sx * r * .62 - r * .13, cy + r * .2 - r * .08, cx + sx * r * .62 + r * .13, cy + r * .2 + r * .08], fill=(255, 150, 130, 150))


def toothbrush(d, x, y, length, angle_flat=True, color=CORAL):
    h = length * .09
    d.rounded_rectangle([x, y, x + length * .62, y + h], radius=h / 2, fill=color)
    d.rounded_rectangle([x + length * .6, y - h * .4, x + length, y + h * 1.4], radius=h * .5, fill=WHITE, outline=TEAL, width=3 * S)
    for i in range(7):
        bx = x + length * .64 + i * length * .05
        d.line([(bx, y - h * .4), (bx, y - h * 1.2)], fill=AQUA, width=int(length * .028))


def kids():
    size = (1200, 800)
    W, H = size[0] * S, size[1] * S

    # picture 1: a cheerful tooth brushing its teeth
    img = gradient((W, H), (255, 244, 232), (255, 222, 205))
    d = ImageDraw.Draw(img, "RGBA")
    d.ellipse([-W * .1, H * .55, W * .5, H * 1.4], fill=(255, 255, 255, 90))
    d.ellipse([W * .55, -H * .3, W * 1.1, H * .5], fill=(43, 181, 173, 50))
    d.ellipse([W * .3, H * .86, W * .7, H * .96], fill=(10, 79, 86, 30))
    draw_tooth(d, W * .30, H * .06, W * .0036 * (H / W) * (W / H) * 1.35, width=7)
    face(d, W * .50, H * .43, W * .095)
    toothbrush(d, W * .56, H * .56, W * .26)
    for cx, cy, r in [(.30, .25, 30), (.72, .30, 22), (.78, .72, 26), (.2, .72, 20), (.62, .14, 18)]:
        d.ellipse([W * cx - r * S, H * cy - r * S, W * cx + r * S, H * cy + r * S], fill=(255, 255, 255, 170), outline=(43, 181, 173, 200), width=3 * S)
    sparkle(d, W * .24, H * .42, 40 * S, CORAL)
    sparkle(d, W * .80, H * .46, 32 * S, AQUA)
    finish(img, "kids", size)

    # picture 2: toothbrush, toothpaste and bubbles
    img = gradient((W, H), (222, 244, 241), (186, 230, 225))
    d = ImageDraw.Draw(img, "RGBA")
    d.ellipse([W * .6, H * .5, W * 1.2, H * 1.4], fill=(255, 255, 255, 90))
    d.ellipse([-W * .1, -H * .3, W * .4, H * .4], fill=(255, 255, 255, 80))
    # toothpaste tube
    d.polygon([(W * .26, H * .30), (W * .46, H * .30), (W * .44, H * .74), (W * .28, H * .74)], fill=WHITE, outline=TEAL)
    d.polygon([(W * .26, H * .30), (W * .46, H * .30), (W * .45, H * .40), (W * .27, H * .40)], fill=TEAL)
    d.rounded_rectangle([W * .32, H * .24, W * .40, H * .31], radius=14 * S, fill=CORAL)
    d.line([(W * .26, H * .74), (W * .44, H * .74)], fill=TEAL, width=4 * S)
    draw_tooth(d, W * .295, H * .45, W * .0017 * 1.0, fill=MINT, width=4)
    # brush with paste
    toothbrush(d, W * .50, H * .62, W * .30, color=TEAL)
    d.rounded_rectangle([W * .685, H * .585, W * .79, H * .625], radius=20 * S, fill=(255, 255, 255, 235))
    for cx, cy, r in [(.62, .32, 44), (.7, .22, 28), (.78, .36, 34), (.55, .18, 20), (.86, .5, 22), (.18, .52, 24)]:
        d.ellipse([W * cx - r * S, H * cy - r * S, W * cx + r * S, H * cy + r * S], fill=(255, 255, 255, 150), outline=(15, 107, 115, 150), width=3 * S)
        d.arc([W * cx - r * S * .6, H * cy - r * S * .6, W * cx + r * S * .6, H * cy + r * S * .6], 200, 260, fill=(255, 255, 255), width=3 * S)
    sparkle(d, W * .12, H * .22, 38 * S, WHITE)
    sparkle(d, W * .90, H * .24, 44 * S, CORAL)
    finish(img, "kids-2", size)


# ------------------------------------------------------------------ doctors
def doctor(name, skin, hair, bg_top, bg_bottom, scrubs, style, glasses=False, beard=False):
    size = (700, 875)
    W, H = size[0] * S, size[1] * S
    img = gradient((W, H), bg_top, bg_bottom)
    d = ImageDraw.Draw(img, "RGBA")
    d.ellipse([-W * .2, H * .45, W * .6, H * 1.1], fill=(255, 255, 255, 40))
    d.ellipse([W * .5, -H * .1, W * 1.2, H * .5], fill=(255, 255, 255, 30))
    cx = W / 2

    # long hair goes behind the shoulders
    if style in ("long", "bob"):
        top = H * .20
        bottom = H * (.72 if style == "long" else .58)
        d.rounded_rectangle([cx - W * .21, top, cx + W * .21, bottom], radius=int(W * .2), fill=hair)

    # white coat (shoulders) and the scrub top in the V-neck
    shoulders = path((W * .02, H), [((W * .02, H * .78), (W * .12, H * .66), (W * .30, H * .63)),
                                    ((W * .38, H * .615), (W * .44, H * .60), (cx, H * .60)),
                                    ((W * .56, H * .60), (W * .62, H * .615), (W * .70, H * .63)),
                                    ((W * .88, H * .66), (W * .98, H * .78), (W * .98, H))])
    d.polygon(shoulders, fill=(250, 252, 252))
    d.polygon(shoulders, outline=(205, 224, 224))
    d.polygon([(cx - W * .13, H * .60), (cx + W * .13, H * .60), (cx, H * .80)], fill=scrubs)
    d.polygon([(cx - W * .13, H * .60), (cx - W * .045, H * .60), (cx, H * .70), (cx - W * .01, H)], fill=(235, 243, 243))
    d.polygon([(cx + W * .13, H * .60), (cx + W * .045, H * .60), (cx, H * .70), (cx + W * .01, H)], fill=(235, 243, 243))
    d.line([(cx, H * .80), (cx, H)], fill=(215, 230, 230), width=3 * S)
    # a name tag without any text (the page shows the real name next to the picture)
    d.rounded_rectangle([cx + W * .17, H * .76, cx + W * .30, H * .79], radius=6 * S, fill=(255, 255, 255), outline=AQUA, width=2 * S)

    # neck and head
    d.rounded_rectangle([cx - W * .06, H * .50, cx + W * .06, H * .62], radius=int(W * .04), fill=tuple(int(c * .93) for c in skin))
    hw, hh = W * .15, H * .165
    hy = H * .40
    d.ellipse([cx - hw, hy - hh, cx + hw, hy + hh], fill=skin)
    d.ellipse([cx - hw - W * .012, hy - hh * .1, cx - hw + W * .018, hy + hh * .22], fill=skin)   # ears
    d.ellipse([cx + hw - W * .018, hy - hh * .1, cx + hw + W * .012, hy + hh * .22], fill=skin)

    # hair on top of the head
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
    # a rounded hairline instead of the straight edge of the cap
    d.ellipse([cx - hw * .86, hy - hh * .64, cx + hw * .86, hy + hh * .22], fill=skin)

    if beard:
        outer = bez((cx - hw * .95, hy + hh * .05), (cx - hw * .95, hy + hh * .9), (cx - hw * .45, hy + hh * 1.15), (cx, hy + hh * 1.15))               + bez((cx, hy + hh * 1.15), (cx + hw * .45, hy + hh * 1.15), (cx + hw * .95, hy + hh * .9), (cx + hw * .95, hy + hh * .05))
        inner = bez((cx + hw * .95, hy + hh * .05), (cx + hw * .85, hy + hh * .35), (cx + hw * .55, hy + hh * .42), (cx + hw * .3, hy + hh * .42))               + bez((cx + hw * .3, hy + hh * .42), (cx + hw * .1, hy + hh * .36), (cx - hw * .1, hy + hh * .36), (cx - hw * .3, hy + hh * .42))               + bez((cx - hw * .3, hy + hh * .42), (cx - hw * .55, hy + hh * .42), (cx - hw * .85, hy + hh * .35), (cx - hw * .95, hy + hh * .05))
        d.polygon(outer + inner, fill=hair)
        d.ellipse([cx - hw * .27, hy + hh * .47, cx + hw * .27, hy + hh * .70], fill=tuple(min(255, int(c * 1.12)) for c in skin))

    # face
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

    # stethoscope
    d.arc([cx - W * .20, H * .64, cx + W * .20, H * .90], 20, 160, fill=INK, width=int(W * .012))
    d.ellipse([cx + W * .17, H * .83, cx + W * .215, H * .875], fill=(160, 175, 180), outline=INK, width=int(W * .005))
    finish(img, f"doctor-{name}", size)


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    hero()
    kids()
    doctor("lejla", (240, 200, 168), (46, 30, 24), (208, 238, 235), (150, 214, 208), TEAL, "long")
    doctor("tarik", (214, 160, 124), (28, 24, 24), (221, 236, 245), (160, 200, 222), TEAL_D, "short", beard=True)
    doctor("mia", (246, 212, 186), (118, 66, 42), (255, 232, 222), (255, 196, 176), AQUA, "bob")
    doctor("adnan", (198, 140, 99), (34, 30, 30), (226, 240, 224), (170, 214, 176), TEAL, "side", glasses=True)
