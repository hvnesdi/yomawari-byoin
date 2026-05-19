from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
import random, math, os

random.seed(42)
SIZE = 2048
OUT = r"C:\Users\hvnes\YomawariByoin\Assets\Textures"
os.makedirs(OUT, exist_ok=True)

def add_noise(img, amount=8):
    import numpy as np
    arr = np.array(img, dtype=np.int16)
    noise = np.random.randint(-amount, amount+1, arr.shape, dtype=np.int16)
    arr = np.clip(arr + noise, 0, 255).astype(np.uint8)
    return Image.fromarray(arr)

def add_stains(draw, count=20, alpha_img=None):
    for _ in range(count):
        x = random.randint(0, SIZE)
        y = random.randint(0, SIZE)
        r = random.randint(20, 120)
        color = (random.randint(140,170), random.randint(130,155), random.randint(100,130), random.randint(30,80))
        draw.ellipse([x-r, y-r, x+r, y+r], fill=color)

# ─────────────────────────────────────────────
# 1. 病室壁テクスチャ (PatientRoom_Wall)
# くすんだ白 (220,218,210) + 黄ばみ + 傷
# ─────────────────────────────────────────────
img = Image.new('RGB', (SIZE, SIZE), (220, 218, 210))
draw = ImageDraw.Draw(img, 'RGBA')

# 黄ばみグラデーション（下部）
for y in range(SIZE//2, SIZE):
    alpha = int(40 * (y - SIZE//2) / (SIZE//2))
    yellowed = (255, 220, 160, alpha)
    draw.line([(0, y), (SIZE, y)], fill=yellowed)

# 剥がれた塗装
for _ in range(15):
    x = random.randint(0, SIZE)
    y = random.randint(0, SIZE)
    w = random.randint(30, 200)
    h = random.randint(5, 30)
    draw.ellipse([x, y, x+w, y+h], fill=(185, 178, 165, 180))

# 引っかき傷
for _ in range(40):
    x1 = random.randint(0, SIZE)
    y1 = random.randint(0, SIZE)
    x2 = x1 + random.randint(-100, 100)
    y2 = y1 + random.randint(-20, 20)
    draw.line([(x1,y1),(x2,y2)], fill=(190,185,175,120), width=random.randint(1,3))

# 染み
add_stains(draw, 25)

img = img.filter(ImageFilter.GaussianBlur(radius=0.8))
img = add_noise(img, 10)
img.save(os.path.join(OUT, 'PatientRoom_Wall_Diffuse.png'))
print("PatientRoom_Wall done")

# ─────────────────────────────────────────────
# 2. 廊下壁テクスチャ (Corridor_Wall)
# 上部：クリーム色(230,225,200) / 下部腰壁：グリーン(150,170,150)
# ─────────────────────────────────────────────
img = Image.new('RGB', (SIZE, SIZE), (230, 225, 200))
draw = ImageDraw.Draw(img, 'RGBA')

# 腰壁（下1/3）
for y in range(SIZE*2//3, SIZE):
    r = int(150 + (y - SIZE*2//3) * 0.005)
    g = int(170 + (y - SIZE*2//3) * 0.003)
    b = int(150)
    draw.line([(0,y),(SIZE,y)], fill=(min(r,180), min(g,185), b))

# 腰壁境界線（モールディング）
for dy in range(4):
    draw.line([(0, SIZE*2//3 + dy), (SIZE, SIZE*2//3 + dy)], fill=(200,200,180,255), width=1)
draw.rectangle([0, SIZE*2//3-8, SIZE, SIZE*2//3+8], fill=(210,210,190))

# 汚れ・染み
add_stains(draw, 30)
for _ in range(60):
    x1 = random.randint(0, SIZE)
    y1 = random.randint(0, SIZE)
    draw.line([(x1,y1),(x1+random.randint(-150,150), y1+random.randint(-5,5))],
              fill=(190,185,165,100), width=random.randint(1,2))

img = img.filter(ImageFilter.GaussianBlur(radius=0.6))
img = add_noise(img, 8)
img.save(os.path.join(OUT, 'Corridor_Wall_Diffuse.png'))
print("Corridor_Wall done")

# ─────────────────────────────────────────────
# 3. 床テクスチャ (Floor_Linoleum)
# リノリウム床 (180,170,150) + タイル目地
# ─────────────────────────────────────────────
img = Image.new('RGB', (SIZE, SIZE), (180, 170, 150))
draw = ImageDraw.Draw(img, 'RGBA')

# タイル目地（60pxごと）
TILE = 120
for x in range(0, SIZE, TILE):
    draw.line([(x,0),(x,SIZE)], fill=(140,130,115,200), width=2)
for y in range(0, SIZE, TILE):
    draw.line([(0,y),(SIZE,y)], fill=(140,130,115,200), width=2)

# 汚れ・染み
add_stains(draw, 40)

# 摩耗による色の変化（通路中央）
for x in range(SIZE//3, SIZE*2//3):
    alpha = int(20 * math.sin(math.pi * (x - SIZE//3) / (SIZE//3)))
    draw.line([(x,0),(x,SIZE)], fill=(200,195,180,alpha))

img = img.filter(ImageFilter.GaussianBlur(radius=0.5))
img = add_noise(img, 12)
img.save(os.path.join(OUT, 'Floor_Linoleum_Diffuse.png'))
print("Floor_Linoleum done")

# ─────────────────────────────────────────────
# 4. 天井テクスチャ (Ceiling)
# 白い天井板 (245,243,238) + 染み・カビ
# ─────────────────────────────────────────────
img = Image.new('RGB', (SIZE, SIZE), (245, 243, 238))
draw = ImageDraw.Draw(img, 'RGBA')

# 天井板の境界
PANEL = 256
for x in range(0, SIZE, PANEL):
    draw.line([(x,0),(x,SIZE)], fill=(210,208,200,150), width=2)
for y in range(0, SIZE, PANEL):
    draw.line([(0,y),(SIZE,y)], fill=(210,208,200,150), width=2)

# 染み（茶色）
for _ in range(15):
    x = random.randint(0, SIZE)
    y = random.randint(0, SIZE)
    r = random.randint(30, 150)
    color = (180+random.randint(0,30), 160+random.randint(0,20), 120+random.randint(0,20), random.randint(40,100))
    draw.ellipse([x-r, y-r, x+r, y+r], fill=color)

# カビ（黒ずみ）
for _ in range(20):
    x = random.randint(0, SIZE)
    y = random.randint(0, SIZE)
    r = random.randint(10, 60)
    draw.ellipse([x-r, y-r, x+r, y+r], fill=(80,85,75,50))

img = img.filter(ImageFilter.GaussianBlur(radius=0.7))
img = add_noise(img, 6)
img.save(os.path.join(OUT, 'Ceiling_Diffuse.png'))
print("Ceiling done")

# ─────────────────────────────────────────────
# 5. Roughnessマップ（全テクスチャ共通）
# ─────────────────────────────────────────────
rough = Image.new('L', (SIZE, SIZE), 180)
rough_draw = ImageDraw.Draw(rough)
for _ in range(200):
    x = random.randint(0, SIZE)
    y = random.randint(0, SIZE)
    r = random.randint(5, 50)
    rough_draw.ellipse([x-r,y-r,x+r,y+r], fill=random.randint(140,220))
rough = add_noise(rough, 15)
rough.save(os.path.join(OUT, 'Shared_Roughness.png'))
print("Roughness done")

print("=== All textures generated ===")
