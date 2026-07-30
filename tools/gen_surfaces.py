# 消灯 — 質感用テクスチャ生成（アルベド微細変化 + ノーマル + ラフネス）
#
# 監査したところ、キャラクターのマテリアルにはテクスチャが1枚も入っておらず、
# 単色のべた塗りだった。だから滑らかなプラスチックに見えていた。
# コンクリートと漆喰にもノーマルマップが無く、面が平らなまま。
#
# ここで作るのは「色」ではなく「表面の凹凸と粗さ」。
# アルベドの平均は 1.0 近傍に保つこと。0.85 まで下げていたときは、
# 全マテリアルが揃って暗くなり「白いナース服が灰色」になった。
# アルベドはほぼ白の微細変化にとどめ、色は Unity 側の _BaseColor に任せる。
# こうすればナース服・警備服・患者衣で同じ布テクスチャを共用できる。
#
# 解像度は 1024。512 だと近接時に粒が見えた。
#
# 実行: python tools/gen_surfaces.py

import os
import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                   "Assets", "Textures", "Surfaces")

rng = np.random.default_rng(19951124)   # 固定。再生成で結果が変わらないように


def fbm(h, w, octaves=6, base=4, persistence=0.55):
    """フラクタルノイズ。自然な凹凸の土台。"""
    acc = np.zeros((h, w), dtype=np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        freq = base * (2 ** o)
        small = rng.random((max(2, freq), max(2, freq))).astype(np.float32)
        up = np.array(Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC),
                      dtype=np.float32) / 255.0
        acc += up * amp
        total += amp
        amp *= persistence
    acc /= total
    acc -= acc.min()
    if acc.max() > 0:
        acc /= acc.max()
    return acc


def height_to_normal(height, strength=2.0):
    """高さマップから接空間ノーマルマップを作る。"""
    h, w = height.shape
    # 端をつないでタイリングを崩さない
    hx = np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
    hy = np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)

    nx = -hx * strength
    ny = -hy * strength
    nz = np.ones_like(height)
    length = np.sqrt(nx ** 2 + ny ** 2 + nz ** 2)
    nx, ny, nz = nx / length, ny / length, nz / length

    rgb = np.stack([(nx * 0.5 + 0.5), (ny * 0.5 + 0.5), (nz * 0.5 + 0.5)], axis=-1)
    return (rgb * 255).astype(np.uint8)


def save_gray(name, arr01):
    img = Image.fromarray((np.clip(arr01, 0, 1) * 255).astype(np.uint8), mode="L").convert("RGB")
    img.save(os.path.join(OUT, name))
    print(f"  {name:32s} 平均={arr01.mean():.3f}")


def save_albedo(name, albedo01, rough01):
    """
    アルベドの RGB に微細な明暗、アルファに「滑らかさ」を入れて1枚にまとめる。
    URP Lit は _SmoothnessTextureChannel = 1 でアルベドのアルファを
    滑らかさとして読むので、金属/光沢マップを別に用意しなくて済む。
    不透明マテリアルではアルファは透過に使われないため衝突しない。
    """
    h, w = albedo01.shape
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    for c in range(3):
        rgba[..., c] = np.clip(albedo01 * 255, 0, 255).astype(np.uint8)
    rgba[..., 3] = np.clip((1.0 - rough01) * 255, 0, 255).astype(np.uint8)
    Image.fromarray(rgba, mode="RGBA").save(os.path.join(OUT, name))
    print(f"  {name:32s} albedo平均={albedo01.mean():.3f} 滑らかさ平均={(1-rough01).mean():.3f}")


def save_normal(name, height, strength):
    Image.fromarray(height_to_normal(height, strength), mode="RGB").save(os.path.join(OUT, name))
    print(f"  {name:32s} strength={strength}")


# ----------------------------------------------------------------------
def fabric():
    """布の織り目。アルベドはほぼ白（色は _BaseColor に任せる）。"""
    n = 1024
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)

    # 平織りの交差。周期を細かくして、モデル上で数センチの目になるようにする
    period = 6.0
    weave = (np.sin(xx / period * np.pi * 2) * np.sin(yy / period * np.pi * 2))
    weave = np.abs(weave) ** 0.6

    # 糸のゆらぎ。完全な格子だと機械的に見える
    wobble = fbm(n, n, octaves=4, base=16)
    height = np.clip(weave * 0.75 + wobble * 0.25, 0, 1)

    # 使い込みによる毛羽立ち・薄い汚れ
    wear = fbm(n, n, octaves=3, base=3)
    albedo = np.clip(0.985 + weave * 0.015 - wear * 0.05, 0, 1)

    save_albedo("fabric_albedo.png", albedo, np.clip(0.86 - wear * 0.22, 0, 1))
    save_normal("fabric_N.png", height, 1.6)
    # 布は基本的に粗い。擦れた所だけわずかに光る
    


def skin():
    """肌。顔の造形は作らない（顔の無い人型という設計）。毛穴程度の微細凹凸のみ。"""
    n = 1024
    pores = fbm(n, n, octaves=5, base=48)
    mottle = fbm(n, n, octaves=3, base=5)
    height = np.clip(pores * 0.7 + mottle * 0.3, 0, 1)

    albedo = np.clip(0.985 + (mottle - 0.5) * 0.035, 0, 1)
    save_albedo("skin_albedo.png", albedo, np.clip(0.62 - pores * 0.10, 0, 1))
    save_normal("skin_N.png", height, 0.55)
    


def concrete():
    """コンクリート。骨材の粒と気泡、たまに欠け。"""
    n = 1024
    grain = fbm(n, n, octaves=5, base=24)
    lumps = fbm(n, n, octaves=3, base=6)

    # 気泡。点状のくぼみ
    holes = np.zeros((n, n), dtype=np.float32)
    for _ in range(260):
        cx, cy = rng.integers(0, n, 2)
        r = rng.integers(2, 6)
        y0, y1 = max(0, cy - r), min(n, cy + r)
        x0, x1 = max(0, cx - r), min(n, cx + r)
        yy, xx = np.mgrid[y0:y1, x0:x1]
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) / max(r, 1)
        holes[y0:y1, x0:x1] = np.maximum(holes[y0:y1, x0:x1], np.clip(1.0 - d, 0, 1))

    height = np.clip(grain * 0.45 + lumps * 0.55 - holes * 0.75, 0, 1)
    albedo = np.clip(0.975 + (lumps - 0.5) * 0.055 - holes * 0.14, 0, 1)

    save_albedo("concrete_albedo.png", albedo, np.clip(0.92 - grain * 0.12, 0, 1))
    save_normal("concrete_N.png", height, 2.4)
    


def plaster():
    """漆喰。鏝跡のうねりと、ところどころの剥がれ。"""
    n = 1024
    trowel = fbm(n, n, octaves=4, base=5)
    fine = fbm(n, n, octaves=5, base=40)

    # 剥がれ。輪郭のはっきりした欠け
    peel = (fbm(n, n, octaves=3, base=7) > 0.72).astype(np.float32)
    peel = np.array(Image.fromarray((peel * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(1.2)), dtype=np.float32) / 255.0

    height = np.clip(trowel * 0.7 + fine * 0.3 - peel * 0.45, 0, 1)
    albedo = np.clip(0.985 + (trowel - 0.5) * 0.04 - peel * 0.10, 0, 1)

    save_albedo("plaster_albedo.png", albedo, np.clip(0.90 - trowel * 0.10 + peel * 0.06, 0, 1))
    save_normal("plaster_N.png", height, 1.5)
    


def painted_metal():
    """塗装した金属。配管やラジエーター用。刷毛目と塗装の剥がれ。"""
    n = 1024
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)

    # 縦方向の刷毛目
    streak = fbm(n, n, octaves=4, base=3)
    streak = np.array(Image.fromarray((streak * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(0.4)), dtype=np.float32) / 255.0
    brushed = np.clip(streak * 0.55 + fbm(n, n, octaves=5, base=64) * 0.45, 0, 1)

    # 塗装の剥がれ。ここだけ金属が出るので粗さも変える
    chips = (fbm(n, n, octaves=4, base=11) > 0.76).astype(np.float32)
    chips = np.array(Image.fromarray((chips * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(0.8)), dtype=np.float32) / 255.0

    height = np.clip(brushed * 0.35 - chips * 0.55, 0, 1)
    albedo = np.clip(0.975 - chips * 0.20 + (brushed - 0.5) * 0.03, 0, 1)

    save_albedo("metal_albedo.png", albedo, np.clip(0.42 + chips * 0.35 + brushed * 0.10, 0, 1))
    save_normal("metal_N.png", height, 1.1)
    # 塗装面はやや光り、剥がれた地金は鈍い
    


def ceiling_tile():
    """天井の吸音板。細かい孔が特徴。"""
    n = 1024
    holes = np.zeros((n, n), dtype=np.float32)
    step = 22
    for y in range(4, n, step):
        for x in range(4, n, step):
            jy, jx = rng.integers(-2, 3, 2)
            cy, cx = (y + jy) % n, (x + jx) % n
            r = 2
            for dy in range(-r, r + 1):
                for dx in range(-r, r + 1):
                    if dy * dy + dx * dx <= r * r:
                        holes[(cy + dy) % n, (cx + dx) % n] = 1.0

    holes = np.array(Image.fromarray((holes * 255).astype(np.uint8)).filter(
        ImageFilter.GaussianBlur(0.6)), dtype=np.float32) / 255.0
    fibre = fbm(n, n, octaves=4, base=20)

    height = np.clip(fibre * 0.35 - holes * 0.8, 0, 1)
    albedo = np.clip(0.975 - holes * 0.17 + (fibre - 0.5) * 0.03, 0, 1)

    save_albedo("ceiling_albedo.png", albedo, np.clip(0.94 - fibre * 0.06, 0, 1))
    save_normal("ceiling_N.png", height, 1.8)
    


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    print("=== 質感テクスチャを生成 ===")
    fabric()
    skin()
    concrete()
    plaster()
    painted_metal()
    ceiling_tile()
    print("=== 完了 ===")
