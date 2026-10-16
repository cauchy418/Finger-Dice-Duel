# -*- coding: utf-8 -*-
"""图标预览（迭代用）。与 DamageIcons.cs 的光栅化规则一致。

坐标：归一化 0..1，**左上原点**（与 C# 的 mask 一致）。
"""
import io, codecs, sys, math
from PIL import Image, ImageDraw

sys.stdout = codecs.getwriter('utf-8')(sys.stdout.buffer, 'replace')

N = 28
SS = 8
S = N * SS

S = 28 * 8


def rot(px, py, cx, cy, deg):
    if not deg:
        return px, py
    r = math.radians(-deg)
    dx, dy = px - cx, py - cy
    return (cx + dx * math.cos(r) - dy * math.sin(r),
            cy + dx * math.sin(r) + dy * math.cos(r))


def draw(dr, sh):
    k = sh[0]

    if k == 'rect':
        _, x, y, w, h = sh[:5]
        d = sh[5] if len(sh) > 5 else 0
        pts = []
        for sx, sy in ((-.5, -.5), (.5, -.5), (.5, .5), (-.5, .5)):
            px, py = (x + sx * w) * S, (y + sy * h) * S
            pts.append(rot(px, py, x * S, y * S, d))
        dr.polygon(pts, fill=255)

    elif k == 'circle':
        _, x, y, r = sh
        dr.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=255)

    elif k == 'ring':
        _, x, y, r, th = sh
        inner = max(0.0, r - th / float(N))
        dr.ellipse([(x - r) * S, (y - r) * S, (x + r) * S, (y + r) * S], fill=255)
        dr.ellipse([(x - inner) * S, (y - inner) * S,
                    (x + inner) * S, (y + inner) * S], fill=0)

    elif k == 'tri':                       # 顶点在上、底边在下
        _, x, y, w, h = sh[:5]
        d = sh[5] if len(sh) > 5 else 0
        top, bot, half = y - h / 2, y + h / 2, w / 2
        pts = [rot(x * S, top * S, x * S, y * S, d),
               rot((x + half) * S, bot * S, x * S, y * S, d),
               rot((x - half) * S, bot * S, x * S, y * S, d)]
        dr.polygon(pts, fill=255)

    elif k == 'tri_d':                     # 顶点在下、底边在上（倒三角）
        _, x, y, w, h = sh[:5]
        d = sh[5] if len(sh) > 5 else 0
        top, bot, half = y - h / 2, y + h / 2, w / 2
        pts = [rot((x - half) * S, top * S, x * S, y * S, d),
               rot((x + half) * S, top * S, x * S, y * S, d),
               rot(x * S, bot * S, x * S, y * S, d)]
        dr.polygon(pts, fill=255)

    elif k == 'line':
        _, x1, y1, x2, y2, th = sh
        dr.line([x1 * S, y1 * S, x2 * S, y2 * S], fill=255,
                width=max(1, int(th * SS)))

    elif k == 'poly':
        pts = sh[1]
        dr.polygon([(pts[i] * S, pts[i + 1] * S) for i in range(0, len(pts), 2)], fill=255)


# ============================================================
# 重画：每个图标都要"缩到 28px 还认得出"
# ============================================================
SHAPES = {
    # 残响：同心声波
    'thunder': [('ring', .5, .5, .13, 2), ('ring', .5, .5, .26, 2),
                ('ring', .5, .5, .39, 2), ('circle', .5, .5, .06)],

    # 强酸：倒三角 + 两滴
    'acid': [('tri_d', .5, .34, .56, .40), ('circle', .44, .74, .07),
             ('circle', .62, .86, .05)],

    # 中毒：骷髅（圆脸 + 两眼 + 下颌 + 牙）
    'poison': [('circle', .5, .40, .27),
               ('rect', .375, .34, .10, .11), ('rect', .525, .34, .10, .11),
               ('rect', .42, .60, .16, .13),
               ('rect', .365, .75, .055, .11), ('rect', .4725, .75, .055, .11),
               ('rect', .58, .75, .055, .11)],

    # 目盲：眼睛 + 斜杠
    'radiant': [('circle', .5, .5, .33), ('circle', .5, .5, .14),
                ('line', .10, .88, .90, .12, 4)],

    # 腐朽：断口十字（四段围成一个"缺口在中心"的十字）
    'necrotic': [('rect', .435, .06, .13, .32), ('rect', .435, .62, .13, .32),
                 ('rect', .06, .435, .32, .13), ('rect', .62, .435, .32, .13)],

    # 燃烧：火苗（纯外焰，不要内心圆 -> 小尺寸才不会糊）
    'fire': [('poly', [.50, .03, .74, .40, .85, .60, .78, .86, .50, .97,
                       .22, .86, .15, .60, .26, .40])],

    # 寒冷：雪花
    'cold': [('line', .5, .06, .5, .94, 3), ('line', .12, .28, .88, .72, 3),
             ('line', .12, .72, .88, .28, 3)],

    # 闪电：折线闪电
    'lightning': [('poly', [.60, .02, .28, .54, .46, .54, .38, .98,
                            .76, .44, .56, .44, .72, .02])],

    # 力场：力场泡（外环 + 核心）
    'force': [('ring', .5, .5, .38, 3), ('circle', .5, .5, .14)],

    # 心灵：思维辐射（头 + 八向射线）
    'psychic': [('circle', .5, .52, .22), ('circle', .5, .44, .06),
                ('line', .5, .04, .5, .20, 3), ('line', .5, .84, .5, .98, 3),
                ('line', .04, .52, .20, .52, 3), ('line', .80, .52, .96, .52, 3),
                ('line', .14, .16, .27, .29, 3), ('line', .73, .75, .86, .88, 3),
                ('line', .14, .88, .27, .75, 3), ('line', .73, .29, .86, .16, 3)],

    # 挥砍：**一把完整的刀**（刀尖 + 刀身 + 护手 + 刀柄，全在一条 45° 线上）
    'slashing': [
        # 刀身：沿 45° 的窄长矩形（中心在刀身中点）
        ('rect', .56, .42, .12, .52, 45),
        # 刀尖：在刀身右上端
        ('tri_d', .86, .18, .17, .19, 45),
        # 护手：**垂直**于刀身（所以旋转 135°），放在刀身左下端
        ('rect', .30, .68, .38, .10, 135),
        # 刀柄：继续往左下
        ('rect', .175, .805, .10, .26, 45),
    ],

    # 钝击：锤（**粗柄** + 大锤头 + 柄尾）
    'bludgeoning': [('rect', .5, .19, .70, .34),      # 锤头（宽扁）
                    ('rect', .5, .60, .18, .56),      # 锤柄（粗）
                    ('rect', .5, .925, .40, .11)],    # 柄尾

    # 穿刺：箭矢（箭头 + 箭杆 + 尾羽）
    'piercing': [('tri_d', .70, .30, .26, .30, 45),
                 ('line', .14, .86, .66, .34, 4),
                 ('line', .13, .66, .34, .87, 4),
                 ('line', .30, .52, .48, .70, 3)],
}

COLORS = {
    'thunder': 0x9B4FD0, 'acid': 0xB6CC2E, 'poison': 0x3FA34D, 'radiant': 0xF2C72E,
    'necrotic': 0x1E5B2E, 'fire': 0xE8701A, 'cold': 0x4C9EE6, 'lightning': 0x1A4FA0,
    'force': 0x8B1A1A, 'psychic': 0xF08CBB,
    'slashing': 0x9AA0AC, 'bludgeoning': 0x9AA0AC, 'piercing': 0x9AA0AC,
}

ORDER = ['thunder', 'acid', 'poison', 'radiant', 'necrotic', 'fire', 'cold',
         'lightning', 'force', 'psychic', 'slashing', 'bludgeoning', 'piercing']

ZOOM = 6
tiles = []
for key in ORDER:
    mask = Image.new('L', (S, S), 0)
    dr = ImageDraw.Draw(mask)
    for sh in SHAPES[key]:
        draw(dr, sh)
    mask = mask.resize((N, N), Image.LANCZOS)

    rgb = COLORS[key]
    tile = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    solid = Image.new('RGBA', (N, N),
                      ((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255, 255))
    tile.paste(solid, (0, 0), mask)
    tiles.append(tile)

# 上下两排（魔法 10 + 物理 3），便于对比
COLS = 7
ROWS = 2
W = COLS * (N + 6) + 6
H = ROWS * (N + 6) + 6
preview = Image.new('RGBA', (W, H), (24, 26, 32, 255))
for i, tile in enumerate(tiles):
    r, c = divmod(i, COLS)
    preview.paste(tile, (6 + c * (N + 6), 6 + r * (N + 6)), tile)

preview = preview.resize((preview.width * ZOOM, preview.height * ZOOM), Image.NEAREST)
preview.save('Tools/_icons_preview.png')
print(u'导出完成：%dx%d（放大 %dx）' % (preview.width, preview.height, ZOOM))
print(u'顺序：' + u' '.join(ORDER))
