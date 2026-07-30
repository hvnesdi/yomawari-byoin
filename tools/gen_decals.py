# 消灯 — 汚しデカールのテクスチャ生成
#
# 既存の decal_waterstain_01.png は「白背景に薄いベージュの筋」という作りだった。
# 白い部分はアルファで抜いてあるものの、可視部分（筋）自体が輝度 83/255 と明るく、
# 暗い壁に貼ると「壁に貼られた白い板」に見えてしまう。
# マテリアル側で暗く色を付けても白さが残ったので、テクスチャから作り直す。
#
# 方針:
#   - RGB は最初から暗く作る（汚れは周囲より暗くないと汚れに見えない）
#   - アルファは汚れの形そのもの。背景は完全な 0
#   - 縁を曖昧にする。輪郭がはっきりしていると板に見える
#
# 実行: python tools/gen_decals.py

import os
import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Assets", "Textures", "Decals")

rng = np.random.default_rng(20260727)   # 再実行で結果が変わらないよう固定


def smooth_noise(h, w, octaves=(4, 8, 16, 32), weights=(0.5, 0.25, 0.15, 0.10)):
    """複数解像度のノイズを重ねた 0..1 の場。染みの形の土台。"""
    acc = np.zeros((h, w), dtype=np.float32)
    for size, weight in zip(octaves, weights):
        small = rng.random((max(2, h * size // max(h, w)), max(2, w * size // max(h, w)))).astype(np.float32)
        up = np.array(Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC),
                      dtype=np.float32) / 255.0
        acc += up * weight
    acc -= acc.min()
    if acc.max() > 0:
        acc /= acc.max()
    return acc


def save(name, rgb, alpha):
    """RGB(0..1) と alpha(0..1) を RGBA PNG で書き出す。"""
    h, w = alpha.shape
    img = np.zeros((h, w, 4), dtype=np.uint8)
    for c in range(3):
        img[..., c] = np.clip(rgb[..., c] * 255, 0, 255).astype(np.uint8)
    img[..., 3] = np.clip(alpha * 255, 0, 255).astype(np.uint8)

    out = Image.fromarray(img, mode="RGBA")
    # 縁を曖昧にする。境界がはっきりしていると壁に貼った板に見える
    out = out.filter(ImageFilter.GaussianBlur(radius=1.4))
    path = os.path.join(OUT, name)
    out.save(path)

    a = np.array(out)[..., 3]
    vis = np.array(out)[..., :3][a > 32]
    lum = vis.mean() if len(vis) else 0
    print(f"  {name:28s} 可視部の輝度={lum:5.1f}/255  不透明率={(a>128).mean():5.1%}")


def water_stain():
    """上から垂れる水染み。錆混じりの暗い茶。"""
    h, w = 1024, 512
    n = smooth_noise(h, w)

    # 縦に伸ばして「垂れ」を作る
    streak = np.zeros_like(n)
    acc = np.zeros(w, dtype=np.float32)
    for y in range(h):
        acc = acc * 0.965 + n[y] * 0.035
        streak[y] = acc
    streak -= streak.min(); streak /= max(streak.max(), 1e-6)

    # 上端が濃く、下へ薄れる
    grad = np.linspace(1.0, 0.12, h, dtype=np.float32)[:, None]
    alpha = np.clip((streak ** 1.5) * grad * 2.6 - 0.10, 0, 1) * 0.85

    # 端に向けて薄れさせて、輪郭を消す
    fx = np.clip(np.minimum(np.arange(w), w - 1 - np.arange(w)) / (w * 0.22), 0, 1)[None, :]
    alpha *= fx

    base = np.stack([np.full((h, w), 0.20), np.full((h, w), 0.165), np.full((h, w), 0.125)], axis=-1)
    # 濃い所ほど暗く。均一な色だと絵に見える
    base *= (1.0 - 0.35 * streak)[..., None]
    save("decal_waterstain_01.png", base.astype(np.float32), alpha)


def mold(name, seed_shift, tint):
    """隅に溜まるカビ。塊が寄り集まった形。"""
    h = w = 512
    n = smooth_noise(h, w, octaves=(3, 7, 14, 28))
    n = np.roll(n, seed_shift, axis=0)

    # 隅（左下）に寄せる
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    corner = 1.0 - np.clip(np.sqrt((xx / w) ** 2 + ((h - yy) / h) ** 2) / 1.15, 0, 1)

    alpha = np.clip((n * 1.35 + corner * 0.75 - 0.72) * 2.4, 0, 1) * 0.9
    base = np.stack([np.full((h, w), tint[0]), np.full((h, w), tint[1]), np.full((h, w), tint[2])], axis=-1)
    base *= (1.0 - 0.45 * n)[..., None]
    save(name, base.astype(np.float32), alpha)


def scratch():
    """引っかき傷。細い線が束になったもの。"""
    h, w = 256, 512
    alpha = np.zeros((h, w), dtype=np.float32)
    for _ in range(26):
        y0 = rng.uniform(0.15, 0.85) * h
        slope = rng.uniform(-0.35, 0.35)
        x0 = rng.uniform(0, w * 0.5)
        length = rng.uniform(w * 0.25, w * 0.75)
        strength = rng.uniform(0.35, 0.9)
        for t in range(int(length)):
            x = int(x0 + t)
            if x >= w:
                break
            y = int(y0 + slope * t + rng.normal(0, 0.6))
            if 0 <= y < h:
                alpha[y, x] = max(alpha[y, x], strength * (1.0 - t / length * 0.5))

    alpha = np.array(Image.fromarray((alpha * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(0.8)), dtype=np.float32) / 255.0
    base = np.stack([np.full((h, w), 0.17)] * 3, axis=-1)
    save("decal_scratch_01.png", base.astype(np.float32), alpha)


def blood(name, seed_shift):
    """古い血。飛沫と垂れ。"""
    h = w = 512
    n = smooth_noise(h, w, octaves=(4, 9, 18, 36))
    n = np.roll(n, seed_shift, axis=1)

    alpha = np.clip((n - 0.60) * 3.4, 0, 1)
    # 下へ垂れる筋を少し足す
    drip = np.zeros_like(alpha)
    acc = np.zeros(w, dtype=np.float32)
    for y in range(h):
        acc = acc * 0.985 + alpha[y] * 0.012
        drip[y] = acc
    alpha = np.clip(alpha + drip * 0.55, 0, 1) * 0.92

    base = np.stack([np.full((h, w), 0.26), np.full((h, w), 0.045), np.full((h, w), 0.035)], axis=-1)
    base *= (1.0 - 0.30 * n)[..., None]
    save(name, base.astype(np.float32), alpha)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("=== 汚しデカールを生成 ===")
    water_stain()
    mold("decal_mold_01.png", 0, (0.135, 0.155, 0.115))
    mold("decal_mold_02.png", 137, (0.115, 0.135, 0.105))
    scratch()
    blood("decal_blood_01.png", 0)
    blood("decal_blood_02.png", 211)
    print("=== 完了 ===")
