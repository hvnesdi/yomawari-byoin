from PIL import Image, ImageDraw, ImageFilter
import numpy as np, random, math, os

random.seed(42)
np.random.seed(42)
SIZE = 2048
OUT = r"C:\Users\hvnes\YomawariByoin\Assets\Textures"
os.makedirs(OUT, exist_ok=True)

def noise(img, amt=8):
    a = np.array(img, dtype=np.int16)
    a = np.clip(a + np.random.randint(-amt, amt+1, a.shape), 0, 255).astype(np.uint8)
    return Image.fromarray(a)

# 病室壁 (220,218,210) + 黄ばみ・傷・染み
img = Image.new('RGB',(SIZE,SIZE),(220,218,210))
d = ImageDraw.Draw(img,'RGBA')
for y in range(SIZE//2,SIZE):
    d.line([(0,y),(SIZE,y)],fill=(255,220,160,int(35*(y-SIZE//2)/(SIZE//2))))
for _ in range(15):
    x,y=random.randint(0,SIZE),random.randint(0,SIZE)
    d.ellipse([x,y,x+random.randint(30,200),y+random.randint(5,30)],fill=(185,178,165,180))
for _ in range(40):
    x,y=random.randint(0,SIZE),random.randint(0,SIZE)
    d.line([(x,y),(x+random.randint(-100,100),y+random.randint(-15,15))],fill=(185,180,170,120),width=random.randint(1,3))
for _ in range(20):
    x,y,r=random.randint(0,SIZE),random.randint(0,SIZE),random.randint(20,100)
    d.ellipse([x-r,y-r,x+r,y+r],fill=(145,135,110,random.randint(30,70)))
img.filter(ImageFilter.GaussianBlur(0.8))
noise(img,10).save(f'{OUT}/PatientRoom_Wall_Diffuse.png')
print('Wall done')

# 廊下壁 上:クリーム(230,225,200) 下腰壁:グリーン(150,170,150)
img = Image.new('RGB',(SIZE,SIZE),(230,225,200))
d = ImageDraw.Draw(img,'RGBA')
for y in range(SIZE*2//3,SIZE):
    d.line([(0,y),(SIZE,y)],fill=(min(155,150+int((y-SIZE*2//3)*0.01)),min(175,170+int((y-SIZE*2//3)*0.005)),150))
d.rectangle([0,SIZE*2//3-8,SIZE,SIZE*2//3+8],fill=(210,210,190))
for _ in range(30):
    x,y,r=random.randint(0,SIZE),random.randint(0,SIZE),random.randint(15,80)
    d.ellipse([x-r,y-r,x+r,y+r],fill=(185,178,155,random.randint(25,65)))
noise(img,8).save(f'{OUT}/Corridor_Wall_Diffuse.png')
print('Corridor done')

# リノリウム床 (180,170,150) + タイル目地
img = Image.new('RGB',(SIZE,SIZE),(180,170,150))
d = ImageDraw.Draw(img,'RGBA')
for x in range(0,SIZE,120):
    d.line([(x,0),(x,SIZE)],fill=(135,125,110,200),width=2)
for y in range(0,SIZE,120):
    d.line([(0,y),(SIZE,y)],fill=(135,125,110,200),width=2)
for _ in range(35):
    x,y,r=random.randint(0,SIZE),random.randint(0,SIZE),random.randint(15,80)
    d.ellipse([x-r,y-r,x+r,y+r],fill=(140,130,110,random.randint(30,70)))
noise(img,12).save(f'{OUT}/Floor_Linoleum_Diffuse.png')
print('Floor done')

# 天井 (245,243,238) + 染み・カビ
img = Image.new('RGB',(SIZE,SIZE),(245,243,238))
d = ImageDraw.Draw(img,'RGBA')
for x in range(0,SIZE,256): d.line([(x,0),(x,SIZE)],fill=(205,203,195,140),width=2)
for y in range(0,SIZE,256): d.line([(0,y),(SIZE,y)],fill=(205,203,195,140),width=2)
for _ in range(15):
    x,y,r=random.randint(0,SIZE),random.randint(0,SIZE),random.randint(30,150)
    d.ellipse([x-r,y-r,x+r,y+r],fill=(175,155,120,random.randint(40,90)))
for _ in range(20):
    x,y,r=random.randint(0,SIZE),random.randint(0,SIZE),random.randint(10,50)
    d.ellipse([x-r,y-r,x+r,y+r],fill=(75,80,70,50))
noise(img,6).save(f'{OUT}/Ceiling_Diffuse.png')
print('Ceiling done')
print('All textures generated!')
