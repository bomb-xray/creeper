"""Design-time tool: authors the knight pixel art and renders a preview.

Running this produces preview PNGs so the art can be looked at while it is being
drawn instead of being written blind. The strings are then copied into the C#
source verbatim.

Target: a tall, slender winged knight, roughly 64 px from heel to helm, matching
the reference art (steel plate, red plume and cape, large pale wings, long blade
held upright in front of the body). Seen from the side, facing right.
"""
from PIL import Image

PALETTE = {
    'k': (10, 10, 14),
    'D': (38, 42, 54),
    'd': (62, 68, 84),
    's': (96, 104, 124),
    'l': (140, 149, 170),
    'w': (186, 194, 212),
    'W': (232, 238, 248),
    'r': (52, 10, 16),
    'R': (92, 18, 26),
    'B': (134, 26, 34),
    'C': (172, 40, 46),
    'b': (20, 18, 24),
    'n': (32, 30, 38),
    'F': (240, 243, 250),
    'f': (202, 209, 224),
    'g': (152, 161, 182),
    'G': (104, 113, 136),
}


def pad(rows):
    w = max(len(r) for r in rows)
    return [r.ljust(w, '.') for r in rows]


# ================================================================= HEAD
# Closed helm, 12 wide. Brow ridge, visor slit, gorget below.

HEAD = pad([
    "...kkkkkk...",
    "..kDDDDDDk..",
    ".kDdddddddk.",
    ".kDdsssssdk.",
    "kDdsslllssdk",
    "kDdslllllsdk",
    "kDdkkkkkksdk",   # visor slit
    "kDdslllllsdk",
    "kDdsslllssdk",
    ".kDdsssssdk.",
    ".kDddsssddk.",
    "..kDdddddk..",
    "...kkDDkk...",
    "....knnk....",   # neck
])

# Plume sweeping back from the crown, 10 wide.
PLUME = pad([
    ".......kCk",
    "......kCBk",
    ".....kCBRk",
    "....kCBRrk",
    "...kCBRrk.",
    "..kCBRrk..",
    ".kBRrrk...",
    "kBRrk.....",
    "kRrk......",
    "kkk.......",
])

# ================================================================= TORSO
# Pauldron, breastplate, sash, hip plates. 18 wide.

TORSO = pad([
    "..kkkkkkkkkkkk..",
    ".kDDddddddddDDk.",
    "kDddsssslllssddk",   # pauldrons
    "kDdsssslllllssdk",
    "kDdssslllwwlllsk",
    "kDdsslllwWWwllsk",   # chest highlight
    "kDdsslllwWWwllsk",
    "kDdssslllwwlllsk",
    "kDdsssslllllssdk",
    "kDddsssslllsssdk",
    ".kDdssssslllssdk",
    ".kDdssssssslssdk",
    ".kDddsssssssssdk",
    "..kDdsssssssssk.",
    "..kCCBBBBBBBCCk.",   # sash
    "..kBRRrrrrrrRBk.",
    "..kBRRrrrrrrRBk.",
    "...kDddssssddk..",
    "...kDdsslllsdk..",   # hip plate
    "....kDddddddk...",
])

# ================================================================= ARMS
# Near arm hangs forward holding the blade, far arm sits behind.

ARM_NEAR = pad([
    ".kkkkk.",
    "kDdsslk",
    "kDdsslk",
    "kDdsslk",
    "kDdsslk",
    ".kdsslk",
    ".kdsslk",
    ".kdsslk",
    ".kdsslk",
    ".kdsslk",
    ".kdsslk",
    ".kwwwlk",   # gauntlet
    ".kwWwlk",
    ".kwwwlk",
    "..kkkk.",
])

ARM_FAR = pad([
    ".kkkk.",
    "kDdddk",
    "kDddsk",
    "kDddsk",
    "kDddsk",
    "kDddsk",
    "kDddsk",
    "kDddsk",
    "kDddsk",
    "kDddsk",
    "kDddsk",
    "kDdssk",
    "kDdssk",
    "kDdssk",
    ".kkkk.",
])

# ================================================================= LEGS
# 20 wide. Near leg lit, far leg darker and set back.

LEGS_IDLE = pad([
    "..kDddk...kdddk.....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "...kDdsk...kddsk....",   # knee
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "...kDssk...kddsk....",   # ankle
    "..kbbbbk..kbbbbk....",
    ".kbbbbbbkkbbbbbbk...",
    ".kkkkkkkkkkkkkkkk...",
])

LEGS_WALK0 = pad([
    "....kDddk.kdddk.....",
    "...kDsslk..kdsslk...",
    "..kDsslk....kdsslk..",
    ".kDsslk......kdsslk.",
    ".kDsslk.......kdssk.",
    "kDdsk..........kddsk",
    "kDsslk.........kdsslk",
    "kDsslk.........kdsslk",
    "kDssk..........kdsslk",
    "kbbbk...........kbbbk",
    "kbbbbk.........kbbbbk",
    "kkkkkk.........kkkkkk",
    "....................",
    "....................",
    "....................",
    "....................",
])

LEGS_WALK1 = pad([
    "..kDddk...kdddk.....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    ".kDsslk....kdsslk...",
    ".kDsslk....kdsslk...",
    "kDdsk.......kddsk...",
    "kDsslk......kdsslk..",
    "kDsslk......kdsslk..",
    "kDsslk......kdsslk..",
    "kDssk.......kdssk...",
    "kbbbbk.....kbbbbk...",
    "kbbbbbk...kbbbbbbk..",
    "kkkkkkk...kkkkkkkk..",
    "....................",
    "....................",
    "....................",
])

LEGS_WALK2 = pad([
    "..kDddk...kdddk.....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "...kDdsk...kddsk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "..kDsslk..kdsslk....",
    "...kDssk...kddsk....",
    "..kbbbbk..kbbbbk....",
    ".kbbbbbbkkbbbbbbk...",
    ".kkkkkkkkkkkkkkkk...",
])

LEGS_WALK3 = pad([
    "...kDddk..kdddk.....",
    "....kDsslk.kdsslk...",
    ".....kDsslk.kdsslk..",
    "......kDsslk.kdssk..",
    ".......kDssk..kddsk.",
    "........kDdsk..kdssk",
    ".......kDsslk..kdsslk",
    "......kDsslk...kdsslk",
    "......kDssk....kbbbk",
    ".....kbbbbk...kbbbbk",
    "....kbbbbbbk..kkkkkk",
    "....kkkkkkkk........",
    "....................",
    "....................",
    "....................",
    "....................",
])

LEGS_CROUCH = pad([
    "....................",
    "....................",
    "....................",
    "....................",
    "..kDddk...kdddk.....",
    ".kDsslsk.kdsslsk....",
    ".kDsslsk.kdsslsk....",
    "kDdsslk.kddsslk.....",
    "kDsslk..kdsslk......",
    "kDssk...kdssk.......",
    "kbbbk...kbbbk.......",
    "kbbbbbkkbbbbbk......",
    "kkkkkkkkkkkkkk......",
    "....................",
    "....................",
    "....................",
])

LEGS_JUMP = pad([
    "....kDddk.kdddk.....",
    "...kDsslk..kdsslk...",
    "...kDsslk...kdsslk..",
    "..kDdsk......kdssk..",
    "..kDsslk......kddsk.",
    ".kDsslk........kdsslk",
    ".kDssk.........kdsslk",
    "kbbbbk..........kbbbk",
    "kbbbbbk........kbbbbk",
    "kkkkkkk........kkkkkk",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
    "....................",
])

LEGS_FALL = pad([
    "..kDddk...kdddk.....",
    "..kDsslk..kdsslk....",
    ".kDsslk....kdsslk...",
    ".kDsslk....kdsslk...",
    "kDdsk.......kddsk...",
    "kDsslk......kdsslk..",
    "kDsslk.......kdsslk.",
    "kDsslk.......kdsslk.",
    "kDssk.........kdssk.",
    "kbbbbk.......kbbbbk.",
    "kbbbbbk.....kbbbbbbk",
    "kkkkkkk.....kkkkkkkk",
    "....................",
    "....................",
    "....................",
    "....................",
])

# Dash: a deep forward lunge, trailing leg stretched far back.
LEGS_DASH = pad([
    "....................",
    "....................",
    "....................",
    "..........kDdddk....",
    ".........kDsssslk...",
    "........kDsssslk....",
    "kkk....kDdssslk.....",
    "kbbk..kDsssslk......",
    "kbbbkkDssslk........",
    ".kbbbDsslk..........",
    "..kbbbbbbk..........",
    "..kkkkkkkk..........",
    "....................",
    "....................",
    "....................",
    "....................",
])

# ================================================================= CAPE
# Hangs from the shoulders behind the body, trailing LEFT. 20 wide.

CAPE_REST = pad([
    "...kkkkkkkk....",
    "..kCBBBBBBCk...",
    "..kBRRRRRRBk...",
    ".kBRRrrrrRRBk..",
    ".kBRrrrrrrrRk..",
    ".kBRrrRRrrrRk..",
    "kBRrrrRRrrrRk..",
    "kBRrrrRRrrrRk..",
    "kRRrrrRRrrrRk..",
    "kRrrrrRRrrrRk..",
    "kRrrrrRRrrrRk..",
    "kRrrrrRRrrRk...",
    "kRrrrrRRrrRk...",
    ".kRrrrRRrrRk...",
    ".kRrrrRRrrRk...",
    ".kRrrrRRrRk....",
    "..kRrrRRrRk....",
    "..kRrrRRrRk....",
    "...kRrrRrRk....",
    "...kRrrrrk.....",
    "....kRrrrk.....",
    "....kkrrkk.....",
    ".....kkkk......",
])

CAPE_DRIFT = pad([
    "....kkkkkkkk...",
    "...kCBBBBBBCk..",
    "..kBRRRRRRRBk..",
    "..kBRRrrrrRRk..",
    ".kBRRrrrrrrRk..",
    ".kBRrrRRrrrRk..",
    "kBRrrrRRrrrRk..",
    "kBRrrRRrrrrRk..",
    "kRRrrRRrrrrRk..",
    "kRrrRRrrrrRk...",
    "kRrrRRrrrRk....",
    "kRrRRrrrrRk....",
    "kRRRrrrrRk.....",
    "kRRrrrrRk......",
    "kRrrrrRk.......",
    "kRrrrRk........",
    "kRrrRk.........",
    "kRrRk..........",
    "kRrk...........",
    "kkk............",
    "...............",
    "...............",
    "...............",
])

CAPE_STREAM = pad([
    "........kkkkkkk",
    "......kkCBBBBBk",
    "...kkkCBBRRRRRk",
    "kkkCBBRRRrrrrRk",
    "kCBBRRrrrrrrrRk",
    "kBRRrrrrrrrrRk.",
    "kBRrrrrrrrrRk..",
    "kRRrrrrrrrRk...",
    "kRrrrrrrRk.....",
    "kRrrrrRk.......",
    "kRrrRk.........",
    "kkrk...........",
    "...............",
    "...............",
    "...............",
    "...............",
    "...............",
    "...............",
    "...............",
    "...............",
    "...............",
    "...............",
    "...............",
])

# ================================================================= WING
# Sweeps UP and BACK behind the knight. 24 wide, 34 tall.

WING_FOLDED = pad([
    "....kkkk........",
    "...kFFFfk.......",
    "..kFFFfgk.......",
    "..kFFfggk.......",
    ".kFFfgGkk.......",
    ".kFFfgGk........",
    ".kFfgGkk........",
    "kFFfgGk.........",
    "kFfgGkk.........",
    "kFfgGk..........",
    "kfgGkk..........",
    "kfgGk...........",
    "kgGkk...........",
    "kgGk............",
    "kGkk............",
    "kGk.............",
    "kk..............",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
])

WING_OPEN = pad([
    "........kkkkkk..",
    "......kkFFFFFfk.",
    "....kkFFFFFffgk.",
    "..kkFFFFFffggGk.",
    "kkFFFFFffgkkkk..",
    "kFFFFffggk......",
    "kFFFffgkk.......",
    "kFFffgk.........",
    "kFFfgkk.........",
    "kFffgk..........",
    "kFfgkk..........",
    "kFfgk...........",
    "kfgkk...........",
    "kfgk............",
    "kgkk............",
    "kgk.............",
    "kk..............",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
    "................",
])

WING_SPREAD = pad([
    "....kkkkkkkkkk..",
    "..kkFFFFFFFFFfk.",
    "kkFFFFFFFFFFffk.",
    "kFFFFFFFFFFfffk.",
    "kFFFFFFFFFffggk.",
    "kFFFFFFFFffggGk.",
    "kFFFFFFFffggkk..",
    "kFFFFFFffggk....",
    "kFFFFFffggk.....",
    "kFFFFffggk......",
    "kFFFffggk.......",
    "kFFffggk........",
    "kFfffgk.........",
    "kFffgk..........",
    "kFfgk...........",
    "kffgk...........",
    "kfgk............",
    "kgk.............",
    "kk..............",
    "................",
    "................",
    "................",
    "................",
    "................",
])

# ================================================================= SWORD
# Long straight blade, point up, 7 wide.

SWORD = pad([
    "...k...",
    "..kWk..",
    "..kWk..",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    ".kdWlk.",
    "kkkkkkk",   # crossguard
    "kdBCBdk",
    "kkkkkkk",
    "..knk..",
    "..knk..",
    "..knk..",
    "..knk..",
    ".kdwdk.",   # pommel
    ".kkkkk.",
])


def render(rows):
    h = len(rows)
    w = max(len(r) for r in rows)
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    px = img.load()
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch in ('.', ' '):
                continue
            c = PALETTE.get(ch, (255, 0, 255))
            px[x, y] = c + (255,)
    return img


# Layout constants shared with the game, all measured from the feet.
CANVAS_W, CANVAS_H = 76, 96
FOOT_X, FOOT_Y = 38, 88


def compose(legs, cape, wing, sword_lift=0, body_drop=0, lunge=0):
    canvas = Image.new('RGBA', (CANVAS_W, CANVAS_H), (26, 24, 32, 255))

    legs_i = render(legs)
    torso_i = render(TORSO)
    head_i = render(HEAD)
    plume_i = render(PLUME)
    armn_i = render(ARM_NEAR)
    armf_i = render(ARM_FAR)
    cape_i = render(cape)
    wing_i = render(wing)
    sword_i = render(SWORD)

    # Vertical stack, measured up from the feet.
    legs_y = FOOT_Y - 16                    # legs bitmaps are 16 tall
    torso_y = legs_y - torso_i.height + 4 + body_drop
    head_y = torso_y - head_i.height + 3

    legs_x = FOOT_X - 10
    torso_x = FOOT_X - 8 + lunge
    head_x = FOOT_X - 6 + lunge

    canvas.alpha_composite(wing_i, (torso_x - 11, torso_y - 6))
    canvas.alpha_composite(cape_i, (torso_x - 8, torso_y + 2))
    canvas.alpha_composite(armf_i, (torso_x + 1, torso_y + 3))
    canvas.alpha_composite(legs_i, (legs_x, legs_y))
    canvas.alpha_composite(torso_i, (torso_x, torso_y))
    canvas.alpha_composite(head_i, (head_x, head_y))
    canvas.alpha_composite(plume_i, (head_x - 7, head_y - 2))
    canvas.alpha_composite(armn_i, (torso_x + 9, torso_y + 4))
    canvas.alpha_composite(sword_i, (torso_x + 13, torso_y - 14 - sword_lift))

    return canvas


if __name__ == '__main__':
    poses = [
        ('idle', LEGS_IDLE, CAPE_REST, WING_FOLDED, 0, 0, 0),
        ('walk0', LEGS_WALK0, CAPE_DRIFT, WING_FOLDED, 0, 0, 0),
        ('walk1', LEGS_WALK1, CAPE_DRIFT, WING_FOLDED, 0, 0, 0),
        ('walk3', LEGS_WALK3, CAPE_DRIFT, WING_FOLDED, 0, 0, 0),
        ('crouch', LEGS_CROUCH, CAPE_REST, WING_FOLDED, -4, 8, 0),
        ('jump', LEGS_JUMP, CAPE_STREAM, WING_SPREAD, 4, 0, 0),
        ('fall', LEGS_FALL, CAPE_DRIFT, WING_OPEN, 0, 0, 0),
        ('dash', LEGS_DASH, CAPE_STREAM, WING_OPEN, -6, 6, 3),
    ]

    sheet = Image.new('RGBA', (CANVAS_W * len(poses), CANVAS_H), (26, 24, 32, 255))
    for i, (name, legs, cape, wing, lift, drop, lunge) in enumerate(poses):
        sheet.alpha_composite(compose(legs, cape, wing, lift, drop, lunge), (CANVAS_W * i, 0))

    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save('/home/user/design/preview.png')
    print(f'wrote preview.png  ({CANVAS_W * len(poses)}x{CANVAS_H} at 4x)')
