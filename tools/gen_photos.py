# 消灯 — 写真の生成
#
# `HorrorEventSystem.PhotoChangeLoop` は「壁の写真が入れ替わる」演出を持っているが、
# `photoRenderer` も `photoVariants` も未設定で、**発火しても何も起きなかった**。
#
# 何が怖いのかを決めてから作る。
# 「気づいたら人が増えている」——最初に見たときは職員が3人、
# 後で通りかかると4人目が居る。**同じ構図のまま人数だけが変わる**のが要点で、
# 別の写真に差し替わるのでは「差し替わった」としか読めない。
# だから4枚はすべて同じ背景・同じ立ち位置から作り、増える人物だけを変える。
#
# 写真そのものは古い記念写真に寄せる: セピア、粒子、周辺減光、わずかな傷。
# 顔は描かない。潰れた顔のほうが、下手に描いた顔より効く。
#
# 実行: python tools/gen_photos.py

import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Assets", "Textures", "Photos")

SIZE = (512, 384)
rng = np.random.default_rng(20260801)


def base_scene():
    """
    背景。病院の玄関前で撮った記念写真のつもり。
    建物の壁と窓の並びだけを置いて、細部は粒子と減光で潰す。
    """
    img = Image.new("RGB", SIZE, (188, 174, 148))
    d = ImageDraw.Draw(img)

    # 空（上部）と建物
    d.rectangle([0, 0, SIZE[0], 120], fill=(206, 196, 174))
    d.rectangle([40, 60, SIZE[0] - 40, 300], fill=(168, 156, 134))

    # 窓の列。等間隔にすると図面に見えるので少し崩す
    for row in range(2):
        for col in range(6):
            x = 62 + col * 62 + (row * 6)
            y = 84 + row * 74
            d.rectangle([x, y, x + 34, y + 46], fill=(132, 124, 108))
            d.rectangle([x + 2, y + 2, x + 32, y + 22], fill=(146, 138, 120))

    # 入口
    d.rectangle([SIZE[0] // 2 - 34, 208, SIZE[0] // 2 + 34, 300], fill=(120, 112, 98))
    # 地面
    d.rectangle([0, 296, SIZE[0], SIZE[1]], fill=(176, 164, 142))
    return img


def draw_person(d, x, ground_y, height, tone, coat=True):
    """
    人物。顔は描かない。
    輪郭だけのほうが古い写真らしく、また下手な顔より不安になる。
    """
    head_r = height * 0.085
    head_y = ground_y - height + head_r

    body_top = head_y + head_r * 1.5
    body_w = height * 0.115

    # 胴（白衣なら明るく）
    body_color = (int(tone * 1.10), int(tone * 1.10), int(tone * 1.05)) if coat else \
                 (int(tone * 0.62), int(tone * 0.60), int(tone * 0.58))
    body_color = tuple(min(255, max(0, c)) for c in body_color)
    d.polygon([(x - body_w, ground_y),
               (x - body_w * 0.78, body_top),
               (x + body_w * 0.78, body_top),
               (x + body_w, ground_y)], fill=body_color)

    # 頭
    head_color = (int(tone * 0.86), int(tone * 0.80), int(tone * 0.72))
    head_color = tuple(min(255, max(0, c)) for c in head_color)
    d.ellipse([x - head_r, head_y - head_r, x + head_r, head_y + head_r], fill=head_color)

    # 腕。体側に沿わせるだけ
    d.line([(x - body_w * 0.85, body_top + height * 0.02),
            (x - body_w * 1.02, ground_y - height * 0.30)], fill=body_color, width=3)
    d.line([(x + body_w * 0.85, body_top + height * 0.02),
            (x + body_w * 1.02, ground_y - height * 0.30)], fill=body_color, width=3)


def age(img, scratch_seed, darkness=1.0):
    """古い写真にする。粒子・周辺減光・傷・退色。"""
    a = np.asarray(img, dtype=np.float32)

    # セピアに寄せる
    grey = a @ np.array([0.30, 0.59, 0.11], dtype=np.float32)
    sepia = np.stack([grey * 1.07, grey * 0.97, grey * 0.80], axis=-1)
    a = a * 0.25 + sepia * 0.75

    # 周辺減光
    yy, xx = np.mgrid[0:SIZE[1], 0:SIZE[0]].astype(np.float32)
    cx, cy = SIZE[0] / 2, SIZE[1] / 2
    r = np.sqrt(((xx - cx) / cx) ** 2 + ((yy - cy) / cy) ** 2)
    a *= np.clip(1.12 - 0.42 * r ** 1.7, 0, 1.2)[..., None]

    # 退色（暗部が持ち上がる）
    a = a * 0.82 + 34

    # 粒子
    local = np.random.default_rng(scratch_seed)
    a += local.normal(0, 7.5, a.shape)

    a *= darkness

    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))

    # 折れ跡・傷。変種ごとに違う位置にすると「別の写真」に見えるので、
    # **傷は全変種で同じにする**（同じ1枚の写真が変化した、と読ませたい）
    d = ImageDraw.Draw(img)
    fixed = np.random.default_rng(999)
    for _ in range(7):
        x0, y0 = fixed.integers(0, SIZE[0]), fixed.integers(0, SIZE[1])
        x1 = x0 + fixed.integers(-40, 40)
        y1 = y0 + fixed.integers(-30, 30)
        d.line([(x0, y0), (x1, y1)], fill=(206, 198, 178), width=1)

    return img.filter(ImageFilter.GaussianBlur(0.4))


def with_border(img):
    """白フチを付けて印画紙にする。額の中に入れるので背景は不要。"""
    b = 18
    out = Image.new("RGB", (SIZE[0] + b * 2, SIZE[1] + b * 2), (214, 206, 186))
    out.paste(img, (b, b))
    return out


# 立ち位置。全変種で共通にして、増減だけを変える
POSITIONS = [
    (128, 0.72, True),    # 白衣
    (196, 0.70, True),
    (300, 0.74, False),   # 私服
    (372, 0.69, True),
]
GROUND = 330


def variant(count, darkness=1.0, extra=None):
    img = base_scene()
    d = ImageDraw.Draw(img)

    for i in range(count):
        x, scale, coat = POSITIONS[i]
        draw_person(d, x, GROUND, SIZE[1] * scale * 0.62, 205, coat)

    # 4枚目だけに現れる人物。少し離れて、少し暗い
    if extra is not None:
        x, scale = extra
        draw_person(d, x, GROUND, SIZE[1] * scale * 0.62, 128, coat=False)

    return with_border(age(img, 7, darkness))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("=== 写真を生成 ===")

    # 同じ構図で、人だけが変わっていく
    variants = [
        ("Photo_1_Staff3.png", variant(3)),
        ("Photo_2_Staff4.png", variant(4)),
        # 4人目の右奥に、誰も居なかったはずの人物
        ("Photo_3_Extra.png", variant(4, extra=(444, 0.60))),
        # 全員居なくなり、その人物だけが残る
        ("Photo_4_Alone.png", variant(0, darkness=0.86, extra=(256, 0.78))),
    ]

    for name, img in variants:
        path = os.path.join(OUT, name)
        img.save(path)
        print(f"  {name:24s} {img.size[0]}x{img.size[1]}")

    print("=== 完了 ===")
