"""
Prop textures: noticeboard with Japanese hospital announcements, clock face
(stopped at 12 with cracked glass), red fire extinguisher label.

All outputs land in Assets/Textures/Generated/ so the existing import
pipeline picks them up as sRGB Repeat.
"""

import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = r"C:\Users\hvnes\YomawariByoin\Assets\Textures\Generated"
os.makedirs(OUT, exist_ok=True)


def _jp_font(size):
    for fn in [r"C:\Windows\Fonts\YuGothB.ttc", r"C:\Windows\Fonts\YuGothM.ttc",
               r"C:\Windows\Fonts\YuGothic.ttc", r"C:\Windows\Fonts\meiryob.ttc",
               r"C:\Windows\Fonts\meiryo.ttc", r"C:\Windows\Fonts\msgothic.ttc"]:
        if os.path.exists(fn):
            try:
                return ImageFont.truetype(fn, size)
            except OSError:
                continue
    return ImageFont.load_default()


def make_noticeboard(out_name="Prop_Noticeboard.png", size=1024):
    """0.8x0.6m noticeboard - texture aspect 4:3, but rendered 1024x1024
    with content placed centered. The quad it maps onto is scaled in
    Unity so the aspect ratio matches the physical board."""
    W, H = size, int(size * 0.75)  # 4:3
    # Cork texture base
    rng = np.random.default_rng(311)
    base = rng.uniform(0.55, 0.80, (H, W, 3)).astype(np.float32)
    # Tint towards cork color RGB(180,150,100)
    cork = np.array([180, 150, 100], dtype=np.float32) / 255.0
    lum = (base * np.array([0.299, 0.587, 0.114])).sum(axis=-1, keepdims=True)
    base = (lum * 0.5 + 0.5) * cork[None, None, :]
    base = np.clip(base, 0, 1)
    # Fine cork dots
    speck = rng.uniform(0, 1, (H, W)) ** 4
    base *= (0.85 + 0.30 * speck[..., None])
    base = np.clip(base, 0, 1)
    img = Image.fromarray((base * 255).astype(np.uint8))
    img = img.filter(ImageFilter.GaussianBlur(0.5))
    draw = ImageDraw.Draw(img)

    # Wooden frame (RGB 100,80,60), 38px wide
    frame_w = 38
    frame_color = (100, 80, 60)
    for k in range(frame_w):
        c = (int(frame_color[0] * (1.0 - k * 0.005)),
             int(frame_color[1] * (1.0 - k * 0.005)),
             int(frame_color[2] * (1.0 - k * 0.005)))
        draw.rectangle([k, k, W - 1 - k, H - 1 - k], outline=c, width=1)
    # Inner shadow
    for k in range(8):
        a = 80 - k * 9
        draw.rectangle([frame_w + k, frame_w + k, W - 1 - frame_w - k, H - 1 - frame_w - k],
                       outline=(40, 30, 20), width=1)

    # 4 paper notices pinned to cork
    paper_color = (245, 240, 225)
    # Notice 1: 面会時間 (top-left)
    n1_x, n1_y, n1_w, n1_h = 80, 90, 380, 200
    draw.rectangle([n1_x, n1_y, n1_x + n1_w, n1_y + n1_h], fill=paper_color, outline=(150, 140, 120), width=2)
    font_big = _jp_font(38)
    font_med = _jp_font(28)
    font_small = _jp_font(22)
    draw.text((n1_x + 18, n1_y + 14), "面会のご案内", font=font_big, fill=(40, 30, 20))
    draw.line([(n1_x + 18, n1_y + 62), (n1_x + n1_w - 18, n1_y + 62)], fill=(120, 80, 50), width=2)
    draw.text((n1_x + 18, n1_y + 80), "面会時間", font=font_med, fill=(60, 40, 25))
    draw.text((n1_x + 18, n1_y + 120), "9:00 〜 17:00", font=font_med, fill=(60, 40, 25))
    draw.text((n1_x + 18, n1_y + 160), "（消灯 21:00）", font=font_small, fill=(80, 50, 30))

    # Notice 2: 安静 (top-right)
    n2_x, n2_y, n2_w, n2_h = 540, 70, 380, 240
    draw.rectangle([n2_x, n2_y, n2_x + n2_w, n2_y + n2_h], fill=(255, 245, 220), outline=(160, 140, 100), width=2)
    draw.text((n2_x + 18, n2_y + 14), "ご注意", font=font_big, fill=(140, 30, 30))
    draw.line([(n2_x + 18, n2_y + 62), (n2_x + n2_w - 18, n2_y + 62)], fill=(140, 30, 30), width=2)
    draw.text((n2_x + 18, n2_y + 80),  "病室では", font=font_med, fill=(50, 30, 20))
    draw.text((n2_x + 18, n2_y + 120), "安静に", font=font_med, fill=(50, 30, 20))
    draw.text((n2_x + 18, n2_y + 160), "してください", font=font_med, fill=(50, 30, 20))
    draw.text((n2_x + 18, n2_y + 200), "—　看護部", font=font_small, fill=(80, 50, 30))

    # Notice 3: 病棟案内 (bottom-left)
    n3_x, n3_y, n3_w, n3_h = 80, 360, 380, 280
    draw.rectangle([n3_x, n3_y, n3_x + n3_w, n3_y + n3_h], fill=paper_color, outline=(150, 140, 120), width=2)
    draw.text((n3_x + 18, n3_y + 14), "病棟案内", font=font_big, fill=(40, 30, 20))
    draw.line([(n3_x + 18, n3_y + 62), (n3_x + n3_w - 18, n3_y + 62)], fill=(60, 60, 60), width=2)
    draw.text((n3_x + 18, n3_y + 80), "第一病棟  → ", font=font_med, fill=(60, 40, 25))
    draw.text((n3_x + 18, n3_y + 120), "第二病棟  →", font=font_med, fill=(60, 40, 25))
    draw.text((n3_x + 18, n3_y + 160), "第三病棟  ←", font=font_med, fill=(140, 30, 30))
    draw.text((n3_x + 18, n3_y + 200), "（隔離病棟）", font=font_small, fill=(140, 30, 30))

    # Notice 4: 消灯 (bottom-right)
    n4_x, n4_y, n4_w, n4_h = 540, 380, 380, 260
    draw.rectangle([n4_x, n4_y, n4_x + n4_w, n4_y + n4_h], fill=paper_color, outline=(150, 140, 120), width=2)
    draw.text((n4_x + 18, n4_y + 14), "消灯について", font=font_big, fill=(40, 30, 20))
    draw.line([(n4_x + 18, n4_y + 62), (n4_x + n4_w - 18, n4_y + 62)], fill=(60, 60, 60), width=2)
    draw.text((n4_x + 18, n4_y + 80),  "毎日　21:00", font=font_med, fill=(60, 40, 25))
    draw.text((n4_x + 18, n4_y + 120), "全館消灯", font=font_med, fill=(60, 40, 25))
    draw.text((n4_x + 18, n4_y + 160), "病室にお戻り", font=font_small, fill=(80, 50, 30))
    draw.text((n4_x + 18, n4_y + 190), "ください。", font=font_small, fill=(80, 50, 30))

    # Push pins (red dots)
    pin_color = (180, 30, 30)
    for (px, py) in [(n1_x + 30, n1_y + 12), (n1_x + n1_w - 30, n1_y + 12),
                      (n2_x + 30, n2_y + 12), (n2_x + n2_w - 30, n2_y + 12),
                      (n3_x + 30, n3_y + 12), (n3_x + n3_w - 30, n3_y + 12),
                      (n4_x + 30, n4_y + 12), (n4_x + n4_w - 30, n4_y + 12)]:
        draw.ellipse([px - 8, py - 8, px + 8, py + 8], fill=pin_color, outline=(90, 10, 10))
        draw.ellipse([px - 3, py - 5, px + 1, py - 1], fill=(255, 180, 180))

    # Slight aging - yellowing & dust
    arr = np.asarray(img, dtype=np.float32) / 255.0
    yellow = np.array([1.04, 1.0, 0.92], dtype=np.float32)
    rng2 = np.random.default_rng(312)
    age = rng2.uniform(0, 1, (H, W))
    age = np.array(Image.fromarray((age * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(80))) / 255.0
    age_mask = np.clip((age - 0.35) * 1.6, 0, 1) * 0.18
    arr = arr * (1 - age_mask[..., None]) + arr * yellow[None, None, :] * age_mask[..., None]
    out = Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8))
    out = out.filter(ImageFilter.GaussianBlur(0.4))
    out.save(os.path.join(OUT, out_name))
    print("  ", out_name)


def make_clock_face(out_name="Prop_ClockFace.png", size=1024):
    """Stopped clock at 12:00, white face, roman numerals, cracked glass overlay.
    RGBA so it can be slapped on a quad inside the clock frame."""
    img = Image.new("RGBA", (size, size), (245, 240, 230, 255))
    draw = ImageDraw.Draw(img)
    cx, cy = size // 2, size // 2
    radius = size // 2 - 40
    # White face circle
    draw.ellipse([cx - radius, cy - radius, cx + radius, cy + radius],
                 fill=(248, 244, 232), outline=(40, 40, 40), width=6)
    # Inner aging
    for r in range(radius - 30, radius - 10, 4):
        a = 30 - (radius - r)
        if a < 0: continue
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=(180, 165, 130, 80))
    # 12 hour marks (roman numerals)
    nums = ["XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI"]
    font = _jp_font(54)
    for i, n in enumerate(nums):
        ang = math.radians(-90 + i * 30)
        nx = cx + math.cos(ang) * (radius - 60)
        ny = cy + math.sin(ang) * (radius - 60)
        bbox = draw.textbbox((0, 0), n, font=font)
        draw.text((nx - (bbox[2] - bbox[0]) / 2, ny - (bbox[3] - bbox[1]) / 2),
                  n, font=font, fill=(35, 30, 25))
    # Minute ticks
    for i in range(60):
        ang = math.radians(-90 + i * 6)
        r0 = radius - 18
        r1 = radius - (28 if i % 5 == 0 else 22)
        x0 = cx + math.cos(ang) * r0
        y0 = cy + math.sin(ang) * r0
        x1 = cx + math.cos(ang) * r1
        y1 = cy + math.sin(ang) * r1
        w = 4 if i % 5 == 0 else 2
        draw.line([(x0, y0), (x1, y1)], fill=(35, 30, 25), width=w)
    # Hands - BOTH pointing at 12 (straight up)
    # Hour hand (short, thick)
    draw.line([(cx, cy), (cx, cy - radius * 0.50)], fill=(20, 20, 20), width=14)
    # Minute hand (long, slim)
    draw.line([(cx, cy), (cx, cy - radius * 0.78)], fill=(20, 20, 20), width=8)
    # Center cap
    draw.ellipse([cx - 18, cy - 18, cx + 18, cy + 18], fill=(70, 70, 70), outline=(20, 20, 20), width=2)
    draw.ellipse([cx - 6, cy - 6, cx + 6, cy + 6], fill=(20, 20, 20))
    # Brand text
    font_brand = _jp_font(24)
    bx_text = "桐島病院"
    bb = draw.textbbox((0, 0), bx_text, font=font_brand)
    draw.text((cx - (bb[2] - bb[0]) / 2, cy + 80), bx_text, font=font_brand, fill=(80, 70, 60))

    # Cracked glass overlay - light scratches radiating from a point
    overlay = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    rng = np.random.default_rng(401)
    # Crack origin
    ox, oy = cx - 140, cy - 60
    od.ellipse([ox - 10, oy - 10, ox + 10, oy + 10], fill=(255, 255, 255, 90))
    for _ in range(7):
        ang = rng.uniform(0, math.tau)
        length = rng.integers(200, 520)
        x, y = ox, oy
        for k in range(length):
            ang += rng.normal(0, 0.04)
            nx = x + math.cos(ang) * 1.5
            ny = y + math.sin(ang) * 1.5
            od.line([(x, y), (nx, ny)], fill=(255, 255, 255, max(20, 200 - k // 2)), width=1)
            x, y = nx, ny
            if rng.random() < 0.02:
                # branch
                ba = ang + rng.uniform(-0.8, 0.8)
                bx, by = x, y
                for _b in range(rng.integers(20, 80)):
                    ba += rng.normal(0, 0.05)
                    bnx = bx + math.cos(ba) * 1.2
                    bny = by + math.sin(ba) * 1.2
                    od.line([(bx, by), (bnx, bny)], fill=(255, 255, 255, 100), width=1)
                    bx, by = bnx, bny
    overlay = overlay.filter(ImageFilter.GaussianBlur(0.4))
    img.paste(overlay, (0, 0), overlay)
    # Mask outside circle (round clock)
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).ellipse([cx - radius - 4, cy - radius - 4, cx + radius + 4, cy + radius + 4], fill=255)
    final = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    final.paste(img, (0, 0), mask)
    final.save(os.path.join(OUT, out_name))
    print("  ", out_name)


def make_extinguisher_label(out_name="Prop_FireExtLabel.png", size=512):
    """White label that wraps around a 0.18m diameter cylinder.
    Aspect ~ pi*d / h ~ 0.57 -> 512×900."""
    W, H = size, 900
    img = Image.new("RGBA", (W, H), (250, 245, 235, 255))
    draw = ImageDraw.Draw(img)
    # Red header band
    draw.rectangle([0, 0, W, 110], fill=(170, 30, 30))
    font_h = _jp_font(54)
    bb = draw.textbbox((0, 0), "消火器", font=font_h)
    draw.text(((W - (bb[2] - bb[0])) / 2, 30), "消火器", font=font_h, fill=(255, 255, 255))
    # Subheader
    font = _jp_font(36)
    draw.text((30, 140), "ABC 粉末", font=font, fill=(20, 20, 20))
    draw.text((30, 190), "10型", font=font, fill=(20, 20, 20))
    # Instructions
    font2 = _jp_font(28)
    lines = ["1.  ピンを抜く", "2.  ノズルを火元に向ける",
             "3.  レバーを強く握る", "4.  火元に粉末を放射する"]
    for i, line in enumerate(lines):
        draw.text((30, 300 + i * 56), line, font=font2, fill=(40, 30, 25))
    # Warning band
    draw.rectangle([0, 580, W, 660], fill=(200, 150, 30))
    draw.text((30, 595), "使用後は再充填", font=font2, fill=(40, 30, 25))
    # Tiny brand
    font3 = _jp_font(22)
    draw.text((30, 720), "型式番号 ABC-10-1990", font=font3, fill=(80, 70, 60))
    draw.text((30, 760), "桐島消防器具(株)", font=font3, fill=(80, 70, 60))
    draw.text((30, 800), "製造年:1990", font=font3, fill=(80, 70, 60))
    # Aging - slight grime/scratches
    arr = np.asarray(img, dtype=np.float32) / 255.0
    rng = np.random.default_rng(501)
    grime = rng.uniform(0, 1, (H, W))
    grime = np.array(Image.fromarray((grime * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(40))) / 255.0
    grime_mask = np.clip((grime - 0.4) * 1.6, 0, 1) * 0.18
    for c in range(3):
        arr[..., c] *= (1 - grime_mask)
    out = Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8))
    out.save(os.path.join(OUT, out_name))
    print("  ", out_name)


def make_paper_chart(out_name="Prop_PaperChart.png", size=512):
    """Scattered chart sheet."""
    W, H = size, int(size * 1.4)  # A4-ish
    img = Image.new("RGBA", (W, H), (248, 244, 232, 255))
    draw = ImageDraw.Draw(img)
    # Header
    font_t = _jp_font(34)
    font_m = _jp_font(22)
    font_s = _jp_font(18)
    draw.text((30, 24), "診療記録", font=font_t, fill=(40, 30, 25))
    draw.line([(30, 70), (W - 30, 70)], fill=(80, 60, 50), width=2)
    # Patient info table
    draw.text((30, 86),  "患者番号 :  K-1990-37", font=font_m, fill=(50, 40, 30))
    draw.text((30, 120), "氏名     :  ─────────────", font=font_m, fill=(50, 40, 30))
    draw.text((30, 154), "年齢     :  ──歳", font=font_m, fill=(50, 40, 30))
    draw.text((30, 188), "病棟     :  第三病棟 隔離室", font=font_m, fill=(130, 30, 30))
    draw.line([(30, 230), (W - 30, 230)], fill=(80, 60, 50), width=1)
    # Body text - scribbles representing notes
    rng = np.random.default_rng(601)
    for i in range(14):
        y = 260 + i * 28
        wlen = rng.integers(W - 200, W - 60)
        # Squiggles
        for k in range(0, wlen, 4):
            yy = y + math.sin(k * 0.3 + i) * 1.2
            draw.line([(30 + k, yy), (30 + k + 3, yy)], fill=(60, 50, 40), width=2)
    # Rubber-stamp red
    draw.ellipse([W - 180, 24, W - 30, 174], outline=(170, 30, 30), width=5)
    draw.text((W - 152, 60), "極秘", font=font_t, fill=(170, 30, 30))
    draw.text((W - 162, 100), "院長扱い", font=font_m, fill=(170, 30, 30))
    # Coffee stain
    arr = np.asarray(img, dtype=np.float32) / 255.0
    yy, xx = np.mgrid[0:H, 0:W]
    cx, cy = 380, H - 200
    r = 90
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    stain = np.clip(1 - d / r, 0, 1) ** 2 * 0.45
    coffee = np.array([0.55, 0.42, 0.28, 1.0], dtype=np.float32)
    arr = arr * (1 - stain[..., None] * 0.6) + coffee[None, None, :] * stain[..., None] * 0.6
    # Crease
    crease = np.clip(np.abs(yy - H * 0.6) < 2, 0, 1).astype(np.float32) * 0.5
    arr[..., :3] *= (1 - crease[..., None] * 0.4)
    img = Image.fromarray(np.clip(arr * 255, 0, 255).astype(np.uint8))
    img.save(os.path.join(OUT, out_name))
    print("  ", out_name)


def main():
    print("Generating prop textures...")
    make_noticeboard()
    make_clock_face()
    make_extinguisher_label()
    make_paper_chart()
    print("Done.")


if __name__ == "__main__":
    main()
