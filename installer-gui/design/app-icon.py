"""Draws the program icon: a white osu! circle with a lightning bolt on the pink-violet tile.

Needs Python 3 with pycairo, numpy, opencv-python and Pillow. Run it from this folder:
    python app-icon.py
It writes app-icon-1024.png, app-icon-256.png, app-icon-preview.png and ../app.ico
(the icon is drawn simpler for 16-24 px so it stays sharp in the taskbar and in Explorer).
"""
import math, os, struct, io
import cairo, numpy as np, cv2
from PIL import Image, ImageDraw

S = 1024
HERE = os.path.dirname(os.path.abspath(__file__))

def rgb(r, g, b): return (r / 255.0, g / 255.0, b / 255.0)
def mix(a, b, t): return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))
PINK, PINK2, VIOLET, WHITE = rgb(255, 102, 170), rgb(255, 128, 190), rgb(150, 110, 255), (1, 1, 1)

# ---------------------------------------------------------------- tiny compositor (premultiplied float RGBA)
def layer():
    s = cairo.ImageSurface(cairo.FORMAT_ARGB32, S, S)
    c = cairo.Context(s)
    c.set_antialias(cairo.ANTIALIAS_BEST)
    c.set_line_cap(cairo.LINE_CAP_ROUND)
    c.set_line_join(cairo.LINE_JOIN_ROUND)
    return s, c

def arr(s):
    s.flush()
    a = np.ndarray((S, S, 4), np.uint8, s.get_data()).astype(np.float32) / 255.0
    return a[..., [2, 1, 0, 3]].copy()

def over(dst, src): return src + dst * (1.0 - src[..., 3:4])
def clip(a, m): return a * m[..., 3:4]

def to_image(a):
    a = np.clip(a, 0, 1)
    al = a[..., 3:4]
    col = np.where(al > 1e-5, a[..., :3] / np.maximum(al, 1e-5), 0)
    return Image.fromarray((np.concatenate([np.clip(col, 0, 1), al], axis=2) * 255 + 0.5).astype(np.uint8), 'RGBA')

# ---------------------------------------------------------------- shapes
def squircle(c, r=S * 0.46, n=5.0, steps=900):
    for i in range(steps):
        t = 2 * math.pi * i / steps
        ct, st = math.cos(t), math.sin(t)
        x = S / 2 + r * math.copysign(abs(ct) ** (2 / n), ct)
        y = S / 2 + r * math.copysign(abs(st) ** (2 / n), st)
        c.line_to(x, y) if i else c.move_to(x, y)
    c.close_path()

def lin(x0, y0, x1, y1, stops):
    g = cairo.LinearGradient(x0, y0, x1, y1)
    for off, col, al in stops: g.add_color_stop_rgba(off, *col, al)
    return g

def rad(cx, cy, r, stops):
    g = cairo.RadialGradient(cx, cy, 0, cx, cy, r)
    for off, col, al in stops: g.add_color_stop_rgba(off, *col, al)
    return g

BOLT = [(0.075, -0.255), (-0.125, 0.035), (-0.012, 0.035), (-0.07, 0.255), (0.135, -0.045), (0.02, -0.045)]

def bolt(c, cx, cy, k):
    for i, (x, y) in enumerate(BOLT):
        X, Y = cx + x * S * k, cy + y * S * k
        c.line_to(X, Y) if i else c.move_to(X, Y)
    c.close_path()

# ---------------------------------------------------------------- the icon
# full: 32 px and up. medium: 24 px (thicker ring). tiny: 16 and 20 px (only the bolt).
STYLES = {
    'full':   dict(ring=True,  k=1.32, ring_w=0.05, ring_r=0.285, gap=0.075, light=0.32, shadow=0.6,  shade=0.0),
    'medium': dict(ring=True,  k=1.50, ring_w=0.08, ring_r=0.300, gap=0.100, light=0.15, shadow=0.8,  shade=0.12),
    'tiny':   dict(ring=False, k=2.05, ring_w=0.0,  ring_r=0.0,   gap=0.0,   light=0.12, shadow=0.85, shade=0.12),
}

def draw(style):
    p = STYLES[style]
    s, c = layer()
    squircle(c)
    c.set_source(lin(S * 0.18, S * 0.12, S * 0.84, S * 0.9, [(0, PINK2, 1), (0.55, mix(PINK, VIOLET, 0.55), 1), (1, VIOLET, 1)]))
    c.fill()
    tile = arr(s)
    s, c = layer()   # soft light at the top left, deeper violet at the bottom right
    c.set_source(rad(S * 0.32, S * 0.08, S * 0.7, [(0, WHITE, p['light']), (1, WHITE, 0)])); c.paint()
    c.set_source(rad(S * 0.75, S * 1.0, S * 0.65, [(0, rgb(90, 60, 220), 0.45 + p['shade']), (1, rgb(90, 60, 220), 0)])); c.paint()
    if p['shade']:
        c.set_source_rgba(*rgb(70, 30, 120), p['shade']); c.paint()
    tile = over(tile, clip(arr(s), tile))

    def art(c, dy=0.0):
        cx, cy = S / 2, S / 2 + dy
        if p['ring']:
            c.arc(cx, cy, S * p['ring_r'], 0, 2 * math.pi); c.set_line_width(S * p['ring_w']); c.stroke()
            c.save(); c.set_operator(cairo.OPERATOR_CLEAR)   # a gap in the ring around the bolt
            bolt(c, cx, cy, p['k']); c.set_line_width(S * p['gap']); c.stroke()
            c.restore()
        bolt(c, cx, cy, p['k']); c.fill_preserve(); c.set_line_width(S * 0.022); c.stroke()

    s, c = layer(); c.set_source_rgba(*rgb(60, 20, 90), p['shadow']); art(c, S * 0.018)
    tile = over(tile, clip(cv2.GaussianBlur(arr(s), (0, 0), S * 0.022), tile))
    s, c = layer(); c.set_source_rgba(1, 1, 1, 1); art(c)
    tile = over(tile, arr(s))
    s, c = layer()   # thin light edge, brighter at the top
    squircle(c, r=S * 0.46 - 3); c.set_line_width(6)
    c.set_source(lin(0, 0, 0, S, [(0, WHITE, 0.24), (0.5, WHITE, 0.064), (1, WHITE, 0.04)])); c.stroke()
    return to_image(over(tile, arr(s)))

def style_for(size): return 'tiny' if size <= 20 else 'medium' if size <= 24 else 'full'

def write_ico(path, images):
    # every frame is stored as PNG (Windows Vista and newer read this)
    blobs = []
    for im in images:
        b = io.BytesIO(); im.save(b, 'PNG', optimize=True); blobs.append(b.getvalue())
    head = struct.pack('<HHH', 0, 1, len(images))
    off = 6 + 16 * len(images)
    entries = b''
    for im, blob in zip(images, blobs):
        w = im.width if im.width < 256 else 0
        entries += struct.pack('<BBBBHHII', w, w, 0, 0, 1, 32, len(blob), off)
        off += len(blob)
    with open(path, 'wb') as f:
        f.write(head + entries + b''.join(blobs))

if __name__ == '__main__':
    art = {name: draw(name) for name in STYLES}
    art['full'].save(os.path.join(HERE, 'app-icon-1024.png'), optimize=True)
    art['full'].resize((256, 256), Image.LANCZOS).save(os.path.join(HERE, 'app-icon-256.png'), optimize=True)
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    frames = [art[style_for(n)].resize((n, n), Image.LANCZOS) for n in sizes]
    write_ico(os.path.join(HERE, '..', 'app.ico'), frames)

    # preview: the big icon and the small sizes on a dark and on a light background
    small = [(n, im) for n, im in zip(sizes, frames) if n <= 64]
    panel = 16 + sum(n + 12 for n, _ in small) + 4
    sheet = Image.new('RGBA', (300 + panel + 22, 300), (16, 12, 22, 255))
    d = ImageDraw.Draw(sheet)
    sheet.alpha_composite(frames[-1], (22, 22))
    for row, bg in enumerate([(32, 32, 32, 255), (238, 238, 238, 255)]):
        y0 = 22 + row * 132
        d.rounded_rectangle((300, y0, 300 + panel, y0 + 124), 14, fill=bg)
        x = 316
        for n, im in small:
            sheet.alpha_composite(im, (x, y0 + 62 - n // 2)); x += n + 12
    sheet.save(os.path.join(HERE, 'app-icon-preview.png'), optimize=True)
    print('done')
