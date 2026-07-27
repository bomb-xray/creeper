"""Design-time renderer for the winged knight.

Hand-typing pixels as strings caps out at a very low quality ceiling. Instead the
knight is described as posed geometry here, then rendered through a shading
pipeline that gives every part a consistent light source, a proper colour ramp
and a clean outline -- the things that actually make pixel art read as
professional.

Because posing happens at design time, the exported frames are ordinary static
bitmaps. The game never rotates anything at runtime, so the pixel grid stays
perfectly intact.

Pipeline per part:
  1. rasterise the shape into a mask
  2. distance-transform the mask to find how deep each pixel sits
  3. light it from the upper left using the depth gradient
  4. quantise onto that material's colour ramp
  5. trace a dark outline around the silhouette
"""
import math
from PIL import Image

W, H = 120, 128          # canvas
GROUND_Y = 118           # where the feet rest

# ---------------------------------------------------------------- materials
# Each ramp runs darkest -> brightest. Shading picks an index per pixel.

RAMPS = {
    'steel': [
        (18, 20, 28), (38, 43, 56), (62, 70, 88), (92, 101, 122),
        (126, 136, 158), (162, 172, 194), (200, 209, 226), (238, 243, 252),
    ],
    'darksteel': [
        (12, 13, 18), (26, 29, 38), (42, 47, 60), (60, 67, 84),
        (82, 90, 110), (106, 115, 138), (132, 142, 166), (164, 174, 198),
    ],
    'cape': [
        (26, 6, 12), (48, 10, 18), (72, 14, 22), (98, 20, 30),
        (126, 26, 36), (156, 36, 44), (186, 52, 58), (214, 78, 80),
    ],
    'cloth': [
        (10, 9, 13), (18, 17, 23), (28, 27, 35), (40, 38, 48),
        (54, 52, 64), (70, 68, 82), (88, 86, 102), (108, 106, 124),
    ],
    'feather': [
        (74, 80, 100), (104, 112, 134), (134, 143, 166), (164, 173, 196),
        (192, 200, 220), (214, 221, 236), (232, 238, 248), (250, 252, 255),
    ],
    'gold': [
        (48, 30, 8), (78, 52, 14), (112, 78, 22), (148, 108, 32),
        (184, 142, 48), (212, 174, 74), (234, 205, 118), (250, 232, 172),
    ],
    'blade': [
        (22, 24, 32), (48, 52, 64), (78, 84, 100), (112, 119, 138),
        (150, 158, 178), (188, 196, 214), (220, 227, 240), (250, 252, 255),
    ],
}

OUTLINE = (8, 8, 12)

# Light comes from the upper left, which is the convention the reference uses.
LIGHT = (-0.55, -0.83)


class Layer:
    """A single part: a mask plus the material it is made of."""

    def __init__(self, material, depth):
        self.material = material
        self.depth = depth                  # draw order, low = behind
        self.mask = [[False] * W for _ in range(H)]
        # Per-pixel shade bias, used to carve details like a visor slit.
        self.bias = {}

    # -- primitives ------------------------------------------------------

    def _set(self, x, y):
        if 0 <= x < W and 0 <= y < H:
            self.mask[y][x] = True

    def disc(self, cx, cy, rx, ry=None):
        ry = rx if ry is None else ry
        for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
            for x in range(int(cx - rx) - 1, int(cx + rx) + 2):
                dx = (x - cx) / max(0.5, rx)
                dy = (y - cy) / max(0.5, ry)
                if dx * dx + dy * dy <= 1.0:
                    self._set(x, y)

    def limb(self, x1, y1, x2, y2, w1, w2=None):
        """A tapered capsule, the workhorse for arms, legs and wing bones."""
        w2 = w1 if w2 is None else w2
        steps = max(2, int(math.hypot(x2 - x1, y2 - y1) * 2))
        for i in range(steps + 1):
            t = i / steps
            cx = x1 + (x2 - x1) * t
            cy = y1 + (y2 - y1) * t
            r = (w1 + (w2 - w1) * t) / 2
            self.disc(cx, cy, r)

    def poly(self, points):
        """Scanline fill of an arbitrary polygon."""
        if len(points) < 3:
            return
        ys = [p[1] for p in points]
        for y in range(int(min(ys)), int(max(ys)) + 1):
            xs = []
            for i in range(len(points)):
                x1, y1 = points[i]
                x2, y2 = points[(i + 1) % len(points)]
                if y1 == y2:
                    continue
                if min(y1, y2) <= y < max(y1, y2):
                    xs.append(x1 + (y - y1) * (x2 - x1) / (y2 - y1))
            xs.sort()
            for i in range(0, len(xs) - 1, 2):
                for x in range(int(round(xs[i])), int(round(xs[i + 1])) + 1):
                    self._set(x, y)

    def rect(self, x, y, w, h):
        for yy in range(int(y), int(y + h)):
            for xx in range(int(x), int(x + w)):
                self._set(xx, yy)

    def carve(self, x, y, w, h):
        """Removes a block, used for slits and gaps."""
        for yy in range(int(y), int(y + h)):
            for xx in range(int(x), int(x + w)):
                if 0 <= xx < W and 0 <= yy < H:
                    self.mask[yy][xx] = False

    def shade(self, x, y, w, h, amount):
        """Biases a region darker or brighter, for panel lines and trim."""
        for yy in range(int(y), int(y + h)):
            for xx in range(int(x), int(x + w)):
                self.bias[(xx, yy)] = self.bias.get((xx, yy), 0) + amount


def distance_field(mask):
    """Chamfer distance from the outside, giving each pixel its depth."""
    INF = 9999
    d = [[0 if not mask[y][x] else INF for x in range(W)] for y in range(H)]

    for y in range(H):
        for x in range(W):
            if d[y][x] == 0:
                continue
            best = INF
            if y > 0:
                best = min(best, d[y - 1][x] + 2)
                if x > 0:
                    best = min(best, d[y - 1][x - 1] + 3)
                if x < W - 1:
                    best = min(best, d[y - 1][x + 1] + 3)
            if x > 0:
                best = min(best, d[y][x - 1] + 2)
            d[y][x] = min(d[y][x], best)

    for y in range(H - 1, -1, -1):
        for x in range(W - 1, -1, -1):
            if d[y][x] == 0:
                continue
            best = d[y][x]
            if y < H - 1:
                best = min(best, d[y + 1][x] + 2)
                if x > 0:
                    best = min(best, d[y + 1][x - 1] + 3)
                if x < W - 1:
                    best = min(best, d[y + 1][x + 1] + 3)
            if x < W - 1:
                best = min(best, d[y][x + 1] + 2)
            d[y][x] = best

    return d


def render(layers):
    """Composites every layer with shading and outlines into an RGBA image."""
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = img.load()

    # Combined silhouette, used for the outline pass.
    solid = [[False] * W for _ in range(H)]

    for layer in sorted(layers, key=lambda l: l.depth):
        ramp = RAMPS[layer.material]
        d = distance_field(layer.mask)

        for y in range(H):
            for x in range(W):
                if not layer.mask[y][x]:
                    continue

                # Surface normal approximated from the depth gradient: on a
                # rounded form the depth rises fastest towards the interior.
                gx = (d[y][min(W - 1, x + 1)] - d[y][max(0, x - 1)]) / 2.0
                gy = (d[min(H - 1, y + 1)][x] - d[max(0, y - 1)][x]) / 2.0
                length = math.hypot(gx, gy)

                if length > 0.01:
                    nx, ny = gx / length, gy / length
                    lit = nx * LIGHT[0] + ny * LIGHT[1]
                else:
                    lit = 0.0

                depth = min(d[y][x] / 2.0, 6.0)

                # Base tone rises with depth so cores read as solid, then the
                # directional term pushes lit edges up and shaded edges down.
                level = 2.6 + depth * 0.32 + lit * 2.5
                level += layer.bias.get((x, y), 0)

                idx = max(0, min(len(ramp) - 1, int(round(level))))
                px[x, y] = ramp[idx] + (255,)
                solid[y][x] = True

    # Outline: any empty pixel touching the silhouette.
    out = img.copy()
    opx = out.load()
    for y in range(H):
        for x in range(W):
            if solid[y][x]:
                continue
            near = False
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= xx < W and 0 <= yy < H and solid[yy][xx]:
                        near = True
                        break
                if near:
                    break
            if near:
                opx[x, y] = OUTLINE + (255,)

    return out


# ================================================================== the knight

def build(pose):
    """Builds every layer for one pose.

    pose keys are joint angles in degrees, measured clockwise from straight down,
    plus a few offsets. Keeping the whole figure parametric is what makes a
    smooth multi-frame walk cycle possible.
    """
    p = {
        'hip_near': 0, 'knee_near': 0, 'hip_far': 0, 'knee_far': 0,
        'shoulder_near': 8, 'elbow_near': -18,
        'shoulder_far': -6, 'elbow_far': 8,
        'lean': 0, 'body_y': 0, 'head_tilt': 0,
        'cape_sway': 0, 'wing_open': 0, 'sword_angle': -4,
    }
    p.update(pose)

    layers = []

    def rad(deg):
        return math.radians(deg)

    # Anchors. The figure is about 100 px from heel to helm.
    hip_x = 60 + p['lean'] * 0.4
    hip_y = 74 + p['body_y']
    chest_x = hip_x + p['lean'] * 0.5
    chest_y = hip_y - 20
    shoulder_y = chest_y - 4
    head_y = chest_y - 15

    # ---- far leg (behind) ----
    far = Layer('darksteel', 10)
    # Far leg is offset back so the pair reads as two legs, not one column.
    fhx = hip_x - 6
    kx = fhx + math.sin(rad(p['hip_far'])) * 20
    ky = hip_y + math.cos(rad(p['hip_far'])) * 20
    ax = kx + math.sin(rad(p['hip_far'] + p['knee_far'])) * 20
    ay = ky + math.cos(rad(p['hip_far'] + p['knee_far'])) * 20
    far.limb(fhx, hip_y, kx, ky, 10, 7)            # thigh
    far.limb(kx, ky, ax, ay, 8, 6)                 # shin
    far.disc(kx, ky, 4.5)                          # knee cop
    far.poly([(ax - 5, ay - 2), (ax + 7, ay - 2), (ax + 8, ay + 4), (ax - 6, ay + 4)])
    layers.append(far)

    # ---- far arm (behind) ----
    afar = Layer('darksteel', 11)
    ex = chest_x + math.sin(rad(p['shoulder_far'])) * 15
    ey = shoulder_y + math.cos(rad(p['shoulder_far'])) * 15
    hx = ex + math.sin(rad(p['shoulder_far'] + p['elbow_far'])) * 14
    hy = ey + math.cos(rad(p['shoulder_far'] + p['elbow_far'])) * 14
    afar.limb(chest_x, shoulder_y, ex, ey, 9, 7)
    afar.limb(ex, ey, hx, hy, 7, 6)
    afar.disc(hx, hy, 3.5)
    layers.append(afar)

    # ---- wing ----
    # Built as an arch: a slim leading bone, then long primaries hanging from it
    # with visible gaps, which is what stops it reading as a solid blob.
    wing = Layer('feather', 5)
    open_amt = p['wing_open']

    # Shoulder of the wing, high on the back.
    wux, wuy = chest_x - 10, shoulder_y - 6

    # Folded wings sit back and only slightly up; opening swings them upward.
    span = 16 + open_amt * 18
    rise = 8 + open_amt * 20
    tipx, tipy = wux - span, wuy - rise

    # Leading bone, thin so it does not bulk out the silhouette.
    wing.limb(wux, wuy, wux - span * 0.45, wuy - rise * 0.7, 7, 4.5)
    wing.limb(wux - span * 0.45, wuy - rise * 0.7, tipx, tipy, 4.5, 2.5)

    # Primaries: long feathers sweeping down and back from the leading edge.
    count = 8
    for i in range(count):
        t = i / (count - 1)

        # Origin walks out along the leading bone.
        ox = wux + (tipx - wux) * t
        oy = wuy + (tipy - wuy) * t

        # Outer feathers are longer and sweep further back, inner ones tuck in.
        length = (11 + t * 17) * (0.75 + open_amt * 0.55)
        angle = 262 - t * 30 - open_amt * 26

        fx = ox + math.cos(rad(angle)) * length
        fy = oy - math.sin(rad(angle)) * length

        width = 4.5 - t * 1.6
        wing.limb(ox, oy, fx, fy, width, 1.8)

        # Dark line between feathers so they stay legible when quantised.
        mid_x = ox + (fx - ox) * 0.55
        mid_y = oy + (fy - oy) * 0.55
        wing.shade(mid_x - 1, mid_y - 1, 2, 3, -1.8)

    layers.append(wing)

    # ---- cape ----
    cape = Layer('cape', 8)
    sway = p['cape_sway']
    # Centre line of the cloth, bending further back the lower it goes.
    pts_back = []
    pts_front = []
    for i in range(9):
        t = i / 8
        cy = shoulder_y + 2 + t * 42
        # Quadratic drift, so the hem trails much further than the collar.
        cx = chest_x - 3 - t * t * sway * 20 - t * 3
        half = 8 - t * 2.5 + math.sin(t * 5.5) * 1.2
        pts_back.append((cx - half, cy))
        pts_front.append((cx + half, cy))
    cape.poly(pts_back + list(reversed(pts_front)))
    # Vertical folds.
    for i in range(9):
        t = i / 8
        cy = shoulder_y + 2 + t * 42
        cx = chest_x - 3 - t * t * sway * 20 - t * 3
        cape.shade(cx - 4, cy, 2, 5, -1.6)
        cape.shade(cx + 3, cy, 2, 5, -1.2)
    layers.append(cape)

    # ---- pelvis and torso ----
    body = Layer('steel', 20)
    # Hip plates
    body.poly([(hip_x - 11, hip_y - 8), (hip_x + 11, hip_y - 8),
               (hip_x + 9, hip_y + 3), (hip_x - 9, hip_y + 3)])
    # Torso tapering up to the chest
    body.poly([(hip_x - 10, hip_y - 6), (hip_x + 10, hip_y - 6),
               (chest_x + 12, chest_y + 2), (chest_x + 13, shoulder_y),
               (chest_x - 13, shoulder_y), (chest_x - 12, chest_y + 2)])
    # Pauldrons
    body.disc(chest_x - 12, shoulder_y + 1, 8, 6)
    body.disc(chest_x + 12, shoulder_y + 1, 8, 6)
    # Chest bevel and a dark seam down the middle
    body.shade(chest_x - 1, chest_y - 2, 2, 16, -1.4)
    body.shade(chest_x - 9, shoulder_y + 3, 3, 8, 1.0)
    layers.append(body)

    # Red sash across the waist.
    sash = Layer('cape', 21)
    sash.poly([(hip_x - 11, hip_y - 10), (hip_x + 11, hip_y - 10),
               (hip_x + 10, hip_y - 5), (hip_x - 10, hip_y - 5)])
    layers.append(sash)

    # Gold trim on the belt.
    trim = Layer('gold', 22)
    trim.rect(hip_x - 4, hip_y - 9, 8, 3)
    layers.append(trim)

    # ---- head ----
    head = Layer('steel', 25)
    tilt = p['head_tilt']
    hx0 = chest_x + tilt * 0.3
    head.disc(hx0, head_y, 9, 10)
    # Jaw and gorget
    head.poly([(hx0 - 7, head_y + 6), (hx0 + 7, head_y + 6),
               (hx0 + 5, head_y + 12), (hx0 - 5, head_y + 12)])
    # Visor slit
    head.carve(hx0 - 8, head_y - 1, 16, 3)
    # Brow highlight
    head.shade(hx0 - 7, head_y - 7, 14, 3, 1.2)
    layers.append(head)

    # Red plume sweeping back over the crown.
    plume = Layer('cape', 24)
    for i in range(7):
        t = i / 6
        px0 = hx0 - 1 - t * 15
        py0 = head_y - 11 + t * t * 9
        plume.disc(px0, py0, 4.0 - t * 2.6)
    layers.append(plume)

    # ---- near leg ----
    near = Layer('steel', 30)
    nhx = hip_x + 5
    kx = nhx + math.sin(rad(p['hip_near'])) * 20
    ky = hip_y + math.cos(rad(p['hip_near'])) * 20
    ax = kx + math.sin(rad(p['hip_near'] + p['knee_near'])) * 20
    ay = ky + math.cos(rad(p['hip_near'] + p['knee_near'])) * 20
    near.limb(nhx, hip_y, kx, ky, 11, 8)
    near.limb(kx, ky, ax, ay, 9, 7)
    near.disc(kx, ky, 5)
    near.shade(kx - 4, ky - 4, 8, 3, 1.0)
    # Sabaton
    near.poly([(ax - 5, ay - 2), (ax + 8, ay - 2), (ax + 9, ay + 4), (ax - 6, ay + 4)])
    layers.append(near)

    # ---- near arm ----
    anear = Layer('steel', 35)
    ex = chest_x + math.sin(rad(p['shoulder_near'])) * 15
    ey = shoulder_y + math.cos(rad(p['shoulder_near'])) * 15
    hx = ex + math.sin(rad(p['shoulder_near'] + p['elbow_near'])) * 14
    hy = ey + math.cos(rad(p['shoulder_near'] + p['elbow_near'])) * 14
    anear.limb(chest_x + 2, shoulder_y, ex, ey, 10, 8)
    anear.limb(ex, ey, hx, hy, 8, 6)
    anear.disc(ex, ey, 4.5)
    anear.disc(hx, hy, 4)          # gauntlet
    layers.append(anear)

    # ---- sword, gripped in the near hand ----
    sword = Layer('blade', 36)
    sa = rad(p['sword_angle'])
    # Blade rises straight out of the fist.
    bx, by = hx, hy
    tip_x = bx + math.sin(sa) * 46
    tip_y = by - math.cos(sa) * 52
    sword.limb(bx, by, tip_x, tip_y, 5, 2)
    layers.append(sword)

    guard = Layer('gold', 37)
    # Crossguard sits just above the fist, perpendicular to the blade.
    perp_x, perp_y = math.cos(sa), math.sin(sa)
    guard.limb(hx - perp_x * 6, hy - perp_y * 6,
               hx + perp_x * 6, hy + perp_y * 6, 3)
    # Pommel below the fist.
    guard.disc(hx - math.sin(sa) * 6, hy + math.cos(sa) * 6, 2.8)
    layers.append(guard)

    return layers


# ================================================================== poses

def walk_pose(t, frames):
    """One frame of the walk cycle. t is the phase in [0, 1)."""
    a = t * 2 * math.pi
    swing = math.sin(a)
    lift = max(0.0, -math.cos(a))

    return {
        'hip_near': swing * 26,
        'knee_near': lift * 40 + 6,
        'hip_far': -swing * 26,
        'knee_far': max(0.0, math.cos(a)) * 40 + 6,
        'shoulder_near': 6 - swing * 10,
        'elbow_near': -20 + lift * 8,
        'shoulder_far': -6 + swing * 20,
        'elbow_far': 10 + max(0.0, math.cos(a)) * 14,
        'lean': 3,
        'body_y': -abs(swing) * 1.5,
        'head_tilt': -swing * 2,
        'cape_sway': 0.55,
        'wing_open': 0.08,
        'sword_angle': -4 - swing * 3,
    }


def idle_pose(t, frames):
    a = t * 2 * math.pi
    breathe = math.sin(a)
    return {
        'hip_near': 9, 'knee_near': 4, 'hip_far': -10, 'knee_far': 8,
        'shoulder_near': 6 + breathe * 2, 'elbow_near': -20,
        'shoulder_far': -6 - breathe * 1.5, 'elbow_far': 10,
        'lean': 0, 'body_y': -breathe * 0.8, 'head_tilt': breathe * 1.5,
        'cape_sway': 0.12 + breathe * 0.06,
        'wing_open': 0.05 + breathe * 0.04,
        'sword_angle': -4 + breathe * 1.5,
    }


POSES = {
    'crouch': {
        'hip_near': -34, 'knee_near': 76, 'hip_far': -26, 'knee_far': 70,
        'shoulder_near': 26, 'elbow_near': 34, 'shoulder_far': 14, 'elbow_far': 30,
        'lean': 8, 'body_y': 14, 'head_tilt': -4,
        'cape_sway': 0.2, 'wing_open': 0.0, 'sword_angle': 14,
    },
    'jump': {
        'hip_near': -30, 'knee_near': 54, 'hip_far': 18, 'knee_far': 16,
        'shoulder_near': -14, 'elbow_near': 16, 'shoulder_far': 34, 'elbow_far': 12,
        'lean': -4, 'body_y': -2, 'head_tilt': 3,
        'cape_sway': 0.9, 'wing_open': 1.0, 'sword_angle': -18,
    },
    'fall': {
        'hip_near': 20, 'knee_near': 14, 'hip_far': -16, 'knee_far': 34,
        'shoulder_near': 18, 'elbow_near': 20, 'shoulder_far': -28, 'elbow_far': 18,
        'lean': 4, 'body_y': 0, 'head_tilt': -3,
        'cape_sway': 0.75, 'wing_open': 0.62, 'sword_angle': -2,
    },
    'dash': {
        'hip_near': 40, 'knee_near': 12, 'hip_far': -40, 'knee_far': 56,
        'shoulder_near': -22, 'elbow_near': 8, 'shoulder_far': 40, 'elbow_far': 14,
        'lean': 14, 'body_y': 8, 'head_tilt': -6,
        'cape_sway': 1.6, 'wing_open': 0.5, 'sword_angle': -30,
    },
}


def crop_all(images):
    """Crops every frame to one shared box so they stay registered."""
    boxes = [im.getbbox() for im in images if im.getbbox()]
    left = min(b[0] for b in boxes)
    top = min(b[1] for b in boxes)
    right = max(b[2] for b in boxes)
    bottom = max(b[3] for b in boxes)
    return [im.crop((left, top, right, bottom)) for im in images], (left, top, right, bottom)


if __name__ == '__main__':
    frames = []
    labels = []

    for i in range(4):
        frames.append(render(build(idle_pose(i / 4, 4))))
        labels.append(f'idle{i}')

    for i in range(8):
        frames.append(render(build(walk_pose(i / 8, 8))))
        labels.append(f'walk{i}')

    for name in ('crouch', 'jump', 'fall', 'dash'):
        frames.append(render(build(POSES[name])))
        labels.append(name)

    cropped, box = crop_all(frames)
    fw, fh = cropped[0].size
    print(f'frame size {fw}x{fh}, box {box}')

    sheet = Image.new('RGBA', (fw * len(cropped), fh), (28, 26, 34, 255))
    for i, im in enumerate(cropped):
        sheet.alpha_composite(im, (fw * i, 0))

    scale = 3
    sheet.resize((sheet.width * scale, sheet.height * scale), Image.NEAREST) \
         .save('/home/user/design/knight_sheet.png')
    print('wrote knight_sheet.png:', ' '.join(labels))
