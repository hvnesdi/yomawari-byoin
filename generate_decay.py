"""
Blender 5.1 - 廃病院の汚れ・decal・追加小物を生成
- 廊下壁ツーパス（上クリーム/下緑腰壁）2K
- 染み・カビ・血痕・落書きのdecal PNG
- 新規小物FBX（落ちた点滴袋、剥がれかけポスター、散乱書類、割れたガラス瓶、倒れた棚）
"""

import bpy, os, math, random
from mathutils import noise

random.seed(7)
PROJ = r"C:\Users\hvnes\YomawariByoin"
TEX_OUT = os.path.join(PROJ, r"Assets\Textures\Generated")
DECAL_OUT = os.path.join(PROJ, r"Assets\Textures\Decals")
PROP_OUT = os.path.join(PROJ, r"Assets\Models\Props")
os.makedirs(TEX_OUT, exist_ok=True)
os.makedirs(DECAL_OUT, exist_ok=True)
os.makedirs(PROP_OUT, exist_ok=True)


# ─────────────────────────────────────────────────────────────
# PIL-free PNG writer using Blender Image
# ─────────────────────────────────────────────────────────────
def save_image(name, width, height, pixels_rgba_floats, out_dir):
    """pixels_rgba_floats: list of floats, length = w*h*4, range 0..1"""
    img = bpy.data.images.new(name, width=width, height=height, alpha=True)
    img.pixels = pixels_rgba_floats
    img.filepath_raw = os.path.join(out_dir, name + ".png")
    img.file_format = 'PNG'
    img.save()
    print(f"  saved {name}.png  ({width}x{height})")
    bpy.data.images.remove(img)


def perlin2(x, y, scale=1.0, octaves=4):
    v = 0.0
    amp = 1.0
    freq = scale
    norm = 0.0
    for _ in range(octaves):
        v += noise.noise((x * freq, y * freq, 0)) * amp
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return (v / norm + 1.0) * 0.5  # 0..1


# ─────────────────────────────────────────────────────────────
# 1. 廊下壁ツーパス（上クリーム plaster / 下緑腰壁）2K
# ─────────────────────────────────────────────────────────────
def make_corridor_wall_2tone(name, w=2048, h=2048):
    px = [0.0] * (w * h * 4)
    # 縦方向の腰壁ライン (画面下から40%)
    wainscot_h = int(h * 0.40)
    wainscot_top_band = 6  # トリム
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            # base
            n1 = perlin2(x / 50, y / 50, 0.6, 4)
            n2 = perlin2(x / 8, y / 8, 4.0, 2) * 0.15
            blot = perlin2(x / 200, y / 200, 0.3, 3)
            if y < wainscot_h:
                # 緑の腰壁（古い病院グリーン:160/175/155）
                r = 0.62 + (n1 - 0.5) * 0.05
                g = 0.66 + (n1 - 0.5) * 0.05
                b = 0.58 + (n1 - 0.5) * 0.05
                # 黄ばみ・汚れ
                yellowing = max(0, blot - 0.55) * 0.25
                r += yellowing * 0.5
                g += yellowing * 0.3
                b += yellowing * 0.1
                # 床近くの汚れ
                if y < 60:
                    dirt = (60 - y) / 60.0
                    r *= 1 - dirt * 0.3
                    g *= 1 - dirt * 0.3
                    b *= 1 - dirt * 0.4
            elif y < wainscot_h + wainscot_top_band:
                # トリム（濃い緑）
                r, g, b = 0.40, 0.42, 0.36
            else:
                # 上：クリーム plaster（230/220/200）
                base_r, base_g, base_b = 0.90, 0.86, 0.78
                # ひび・汚れ
                crack = perlin2(x / 4, y / 4, 8.0, 2)
                yellow = perlin2(x / 300, y / 80, 0.25, 3)  # 横方向の黄ばみ
                stain = perlin2(x / 150, y / 200, 0.5, 4)
                r = base_r * (0.88 + n1 * 0.12)
                g = base_g * (0.88 + n1 * 0.12)
                b = base_b * (0.88 + n1 * 0.12)
                # 黄ばみ（不規則）
                if yellow > 0.55:
                    yel = (yellow - 0.55) * 1.5
                    r = min(1.0, r * (1 + yel * 0.05))
                    g = min(1.0, g * (1 - yel * 0.1))
                    b = min(1.0, b * (1 - yel * 0.3))
                # 水染み（下に行くほど濃く）
                drip = perlin2(x / 80, y / 30, 0.3, 3)
                if drip > 0.62 and y < wainscot_h + 600:
                    fade = (drip - 0.62) * 2.5
                    height_fade = max(0, (wainscot_h + 600 - y) / 600.0)
                    f = fade * height_fade
                    r *= 1 - f * 0.35
                    g *= 1 - f * 0.45
                    b *= 1 - f * 0.55
                # ひび
                if crack > 0.85:
                    r *= 0.4; g *= 0.4; b *= 0.4
                # 天井近くのカビ（緑黒）
                if y > h - 200:
                    mold = perlin2(x / 60, y / 20, 1.5, 3)
                    if mold > 0.60:
                        m = (mold - 0.60) * 2.0
                        r *= 1 - m * 0.55
                        g *= 1 - m * 0.30
                        b *= 1 - m * 0.65
            # 細かいノイズ
            r += (n2 - 0.075) * 0.1
            g += (n2 - 0.075) * 0.1
            b += (n2 - 0.075) * 0.1
            px[idx]   = max(0, min(1, r))
            px[idx+1] = max(0, min(1, g))
            px[idx+2] = max(0, min(1, b))
            px[idx+3] = 1.0
    save_image(name, w, h, px, TEX_OUT)


# ─────────────────────────────────────────────────────────────
# 2. 病室壁（白/クリーム plaster + 汚れ）
# ─────────────────────────────────────────────────────────────
def make_patient_wall(name, w=2048, h=2048):
    px = [0.0] * (w * h * 4)
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            n1 = perlin2(x / 40, y / 40, 0.5, 4)
            n2 = perlin2(x / 6, y / 6, 5.0, 2) * 0.12
            stain = perlin2(x / 180, y / 220, 0.4, 4)
            yellow = perlin2(x / 250, y / 70, 0.3, 3)
            r, g, b = 0.93, 0.90, 0.82
            r *= 0.88 + n1 * 0.12
            g *= 0.88 + n1 * 0.12
            b *= 0.88 + n1 * 0.12
            # 黄ばみ
            if yellow > 0.50:
                y2 = (yellow - 0.50) * 1.6
                r *= 1.02
                g *= 1 - y2 * 0.10
                b *= 1 - y2 * 0.28
            # 水染み（下向きに流れた跡）
            drip = perlin2(x / 60, y / 25, 0.4, 3)
            if drip > 0.65:
                f = (drip - 0.65) * 2.0
                r *= 1 - f * 0.30
                g *= 1 - f * 0.42
                b *= 1 - f * 0.50
            # ひび
            crack = perlin2(x / 3, y / 3, 9.0, 2)
            if crack > 0.88:
                r *= 0.45; g *= 0.45; b *= 0.45
            r += (n2 - 0.06) * 0.08
            g += (n2 - 0.06) * 0.08
            b += (n2 - 0.06) * 0.08
            px[idx]   = max(0, min(1, r))
            px[idx+1] = max(0, min(1, g))
            px[idx+2] = max(0, min(1, b))
            px[idx+3] = 1.0
    save_image(name, w, h, px, TEX_OUT)


# ─────────────────────────────────────────────────────────────
# 3. 地下コンクリート（湿気・染みあり）
# ─────────────────────────────────────────────────────────────
def make_basement_concrete(name, w=2048, h=2048):
    px = [0.0] * (w * h * 4)
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            n1 = perlin2(x / 30, y / 30, 0.7, 5)
            grain = perlin2(x / 3, y / 3, 6.0, 2)
            base = 0.42 + n1 * 0.12
            r = base * 0.95
            g = base * 0.93
            b = base * 0.88
            # 湿気の染み（暗い縦縞ではなく不規則）
            wet = perlin2(x / 250, y / 180, 0.4, 4)
            if wet > 0.55:
                f = (wet - 0.55) * 1.8
                r *= 1 - f * 0.35
                g *= 1 - f * 0.35
                b *= 1 - f * 0.45
            # カビ（暗緑黒）
            mold = perlin2(x / 80, y / 60, 0.8, 3)
            if mold > 0.65:
                m = (mold - 0.65) * 2.0
                r *= 1 - m * 0.45
                g *= 1 - m * 0.25
                b *= 1 - m * 0.55
            # 細かい骨材ノイズ
            r += (grain - 0.5) * 0.10
            g += (grain - 0.5) * 0.10
            b += (grain - 0.5) * 0.10
            px[idx]   = max(0, min(1, r))
            px[idx+1] = max(0, min(1, g))
            px[idx+2] = max(0, min(1, b))
            px[idx+3] = 1.0
    save_image(name, w, h, px, TEX_OUT)


# ─────────────────────────────────────────────────────────────
# 4. 床リノリウム（60cm角タイル・目地・汚れ）
# ─────────────────────────────────────────────────────────────
def make_floor_linoleum(name, w=2048, h=2048):
    px = [0.0] * (w * h * 4)
    tile_size = 256   # 2048/8 = 256px per tile
    grout = 6
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            tx = x % tile_size
            ty = y % tile_size
            in_grout = (tx < grout) or (ty < grout) or (tx >= tile_size - grout) or (ty >= tile_size - grout)
            if in_grout:
                # 暗い目地（黒ずんでいる）
                r, g, b = 0.18, 0.17, 0.16
                d = perlin2(x / 5, y / 5, 4.0, 2) * 0.05
                r += d; g += d; b += d
            else:
                # ベージュタイル（ベース）
                tile_id = (x // tile_size) + (y // tile_size) * 13
                random.seed(tile_id)
                v = 0.62 + random.random() * 0.08
                r = v * 0.96
                g = v * 0.92
                b = v * 0.82
                # 細かい斑点
                spec = perlin2(x / 2, y / 2, 8.0, 2)
                r += (spec - 0.5) * 0.05
                g += (spec - 0.5) * 0.05
                b += (spec - 0.5) * 0.04
                # 汚れ・足跡（不規則）
                dirt = perlin2(x / 80, y / 80, 0.5, 4)
                if dirt > 0.55:
                    f = (dirt - 0.55) * 1.5
                    r *= 1 - f * 0.30
                    g *= 1 - f * 0.32
                    b *= 1 - f * 0.30
                # 黄ばみのある染み
                stain = perlin2(x / 200, y / 150, 0.3, 4)
                if stain > 0.62:
                    s = (stain - 0.62) * 1.5
                    r *= 1 - s * 0.12
                    g *= 1 - s * 0.20
                    b *= 1 - s * 0.32
                # スクラッチ（線）
                scr = perlin2(x / 3, y / 80, 6.0, 2)
                if scr > 0.86:
                    r *= 0.78; g *= 0.78; b *= 0.78
            px[idx]   = max(0, min(1, r))
            px[idx+1] = max(0, min(1, g))
            px[idx+2] = max(0, min(1, b))
            px[idx+3] = 1.0
    save_image(name, w, h, px, TEX_OUT)


# ─────────────────────────────────────────────────────────────
# 5. Decal: 血痕・カビ・水染み・落書き（透過PNG）
# ─────────────────────────────────────────────────────────────
def make_decal_blood(name, w=1024, h=1024):
    px = [0.0] * (w * h * 4)
    cx, cy = w * 0.5, h * 0.4
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            dx = x - cx
            dy = y - cy
            n = perlin2(x / 30, y / 30, 0.8, 4)
            # 不規則な飛沫
            base = perlin2(x / 60, y / 60, 0.6, 4)
            radial = math.sqrt(dx * dx + dy * dy) / w
            # メインの血だまり
            shape = 1.0 - radial * 1.8 + (n - 0.5) * 0.7
            shape = max(0, shape)
            # 細かい飛沫
            spot = perlin2(x / 5, y / 5, 5.0, 2)
            spatter = 0
            if spot > 0.82:
                spatter = (spot - 0.82) * 4.0
            alpha = max(shape, spatter * 0.4) * 0.9
            alpha = min(1, max(0, alpha))
            # 流れた跡（下向き）
            if dy > 0 and shape > 0.15:
                stripe = perlin2(x / 10, y / 80, 0.8, 2)
                if stripe > 0.55:
                    drip = max(0, 1 - dy / (h * 0.5)) * (stripe - 0.55) * 2
                    alpha = max(alpha, drip * 0.5)
            r = 0.25 + (n - 0.5) * 0.05
            g = 0.06 + (n - 0.5) * 0.03
            b = 0.06 + (n - 0.5) * 0.03
            px[idx] = r
            px[idx+1] = g
            px[idx+2] = b
            px[idx+3] = alpha
    save_image(name, w, h, px, DECAL_OUT)


def make_decal_mold(name, w=1024, h=1024):
    px = [0.0] * (w * h * 4)
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            n1 = perlin2(x / 80, y / 80, 0.5, 5)
            n2 = perlin2(x / 5, y / 5, 4.0, 3)
            # 隅から広がるカビ
            cx, cy = 0, h
            dx = x / w
            dy = (h - y) / h
            corner = max(0, 1 - math.sqrt(dx * dx + dy * dy) * 0.9)
            mass = n1 * corner
            alpha = 0
            if mass > 0.30:
                alpha = (mass - 0.30) * 2.5
            # 細粒
            if n2 > 0.7:
                alpha = max(alpha, (n2 - 0.7) * 1.2 * corner * 0.6)
            alpha = min(1, max(0, alpha))
            r = 0.10 + (n2 - 0.5) * 0.04
            g = 0.16 + (n2 - 0.5) * 0.05
            b = 0.08 + (n2 - 0.5) * 0.03
            px[idx] = r
            px[idx+1] = g
            px[idx+2] = b
            px[idx+3] = alpha
    save_image(name, w, h, px, DECAL_OUT)


def make_decal_waterstain(name, w=1024, h=1024):
    px = [0.0] * (w * h * 4)
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            n1 = perlin2(x / 30, y / 60, 0.6, 4)
            # 上から下に流れる
            fade = 1 - (y / h) * 0.4
            stripe = perlin2(x / 50, y / 200, 0.3, 3)
            shape = 0
            if stripe > 0.45:
                shape = (stripe - 0.45) * 2.0 * fade
            # ふちが濃く中央が薄い「ティーステイン」効果
            edge = perlin2(x / 8, y / 8, 3.0, 2)
            alpha = shape
            if shape > 0.3 and edge > 0.55:
                alpha = max(alpha, (edge - 0.55) * 1.5)
            alpha = min(1, max(0, alpha)) * 0.75
            r = 0.40 + n1 * 0.06
            g = 0.31 + n1 * 0.04
            b = 0.20 + n1 * 0.03
            px[idx] = r
            px[idx+1] = g
            px[idx+2] = b
            px[idx+3] = alpha
    save_image(name, w, h, px, DECAL_OUT)


def make_decal_graffiti(name, text_lines, w=1024, h=512):
    """シンプルな手書き風文字decalを生成（ピクセル描画）"""
    px = [0.0] * (w * h * 4)
    # 全体は透明
    # 文字を太い線で描く（ベジェじゃなくbitmap風）
    # 簡易日本語フォントは無理なので、Unityで TextMeshPro 使うか、
    # ここでは「手書きスクラッチ風」のテクスチャを作成し、Unity側で文字を上に乗せる前提
    # → 代わりに大きなスクラッチ・引っ掻き傷風のdecalを作る
    for y in range(h):
        for x in range(w):
            idx = (y * w + x) * 4
            n = perlin2(x / 4, y / 4, 6.0, 2)
            line = perlin2(x / 8, y / 80, 2.0, 1)
            # 横方向の引っかき傷
            scratch = 0
            if line > 0.78 and n > 0.6:
                scratch = (line - 0.78) * 3.0
            # 中央寄りに集中
            cy = h * 0.5
            fade = 1 - abs(y - cy) / (h * 0.5)
            alpha = scratch * fade
            alpha = min(1, max(0, alpha)) * 0.7
            r, g, b = 0.08, 0.08, 0.10
            px[idx] = r
            px[idx+1] = g
            px[idx+2] = b
            px[idx+3] = alpha
    save_image(name, w, h, px, DECAL_OUT)


# ─────────────────────────────────────────────────────────────
# 6. 落書きを Blender Text オブジェクトで描き、レンダリングしてPNGに
# ─────────────────────────────────────────────────────────────
def render_graffiti_text(name, text, color=(0.05, 0.05, 0.08), w=1024, h=512):
    # シーンクリア
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    bpy.ops.object.text_add(location=(0, 0, 0))
    txt = bpy.context.active_object
    txt.data.body = text
    txt.data.size = 1.0
    txt.data.align_x = 'CENTER'
    txt.data.align_y = 'CENTER'
    # マテリアル（赤黒っぽい血の色 or 黒）
    m = bpy.data.materials.new(f"GraffitiMat_{name}")
    m.use_nodes = True
    n = m.node_tree.nodes['Principled BSDF']
    n.inputs['Base Color'].default_value = (color[0], color[1], color[2], 1)
    try: n.inputs['Emission Color'].default_value = (color[0], color[1], color[2], 1)
    except: pass
    n.inputs['Emission Strength'].default_value = 0
    txt.data.materials.append(m)

    # 直行カメラ
    bpy.ops.object.camera_add(location=(0, 0, 5))
    cam = bpy.context.active_object
    cam.rotation_euler = (0, 0, 0)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 8.0
    bpy.context.scene.camera = cam

    # 平面光（背景透過のため不要、ただしテキスト自体は emission で発光させない場合は光必要）
    bpy.ops.object.light_add(type='SUN', location=(0, 0, 3))
    bpy.context.active_object.data.energy = 3

    # レンダリング設定（背景透過）
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    out_path = os.path.join(DECAL_OUT, name + ".png")
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    print(f"  rendered {name}.png")


# ─────────────────────────────────────────────────────────────
# 7. 追加小物: 落ちた点滴袋、剥がれかけポスター、散乱書類、割れたガラス瓶、倒れた棚、結露窓
# ─────────────────────────────────────────────────────────────
def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for d in list(bpy.data.meshes):
        bpy.data.meshes.remove(d, do_unlink=True)
    for d in list(bpy.data.materials):
        bpy.data.materials.remove(d, do_unlink=True)


def export_fbx(name):
    bpy.ops.object.select_all(action='SELECT')
    p = os.path.join(PROP_OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=p, use_selection=True, global_scale=1.0,
                              axis_forward='-Z', axis_up='Y', apply_unit_scale=True)
    print(f"  fbx: {name}")


def mat(name, r, g, b, metallic=0.0, roughness=0.85, alpha=1.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    n = m.node_tree.nodes['Principled BSDF']
    n.inputs['Base Color'].default_value = (r/255, g/255, b/255, alpha)
    n.inputs['Metallic'].default_value = metallic
    n.inputs['Roughness'].default_value = roughness
    if alpha < 1.0:
        n.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    return m


def add_mat(o, m):
    if o.data.materials: o.data.materials[0] = m
    else: o.data.materials.append(m)


def cube(name, x, y, z, sx, sy, sz, m, rx=0, ry=0, rz=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    o = bpy.context.active_object
    o.name = name
    o.scale = (sx, sy, sz)
    o.rotation_euler = (rx, ry, rz)
    add_mat(o, m)
    return o


def cyl(name, x, y, z, r, d, m, rx=0, ry=0, rz=0):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d, location=(x, y, z))
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = (rx, ry, rz)
    add_mat(o, m)
    return o


def make_fallen_iv_bag():
    clear_scene()
    bag = mat("IVBagFallen", 200, 215, 200, roughness=0.4, alpha=0.7)
    tube = mat("Tube", 160, 158, 152, metallic=0.3, roughness=0.6)
    # 床に落ちて潰れた点滴袋
    cube("Bag", 0, 0, 0.02, 0.16, 0.12, 0.04, bag)
    # チューブ（くしゃっと）
    cyl("Tube1", -0.12, 0.05, 0.02, 0.004, 0.2, tube, rx=math.radians(85), rz=math.radians(20))
    cyl("Tube2", -0.20, 0.12, 0.02, 0.004, 0.15, tube, rx=math.radians(85), rz=math.radians(60))
    export_fbx("prop_iv_bag_fallen")


def make_peeling_poster():
    clear_scene()
    paper = mat("Poster", 195, 175, 140, roughness=0.95)
    paper_back = mat("PosterBack", 165, 145, 110, roughness=0.95)
    # ベース：紙が壁から剥がれている
    cube("Poster_Back", 0, 0, 0, 0.3, 0.005, 0.4, paper)
    # 剥がれた上端（カール）
    o = cube("Poster_Curl", 0, -0.04, 0.20, 0.3, 0.005, 0.08, paper_back, rx=math.radians(-30))
    o.location.z = 0.20
    # 引きちぎれた左角
    cube("Poster_Tear", -0.13, 0.005, 0.18, 0.04, 0.004, 0.06, paper, rz=math.radians(15))
    export_fbx("prop_peeling_poster")


def make_scattered_papers():
    clear_scene()
    paper_mats = [
        mat(f"Paper{i}", random.randint(195, 220), random.randint(180, 210), random.randint(140, 175), roughness=0.96)
        for i in range(4)
    ]
    # 散乱した書類（30枚）
    for i in range(30):
        x = random.uniform(-0.6, 0.6)
        y = random.uniform(-0.5, 0.5)
        rz = random.uniform(0, math.pi)
        rx = random.uniform(-0.1, 0.1)
        cube(f"Pp{i}", x, y, 0.002 + i * 0.0003, 0.105, 0.148, 0.0005,
             paper_mats[i % 4], rx=rx, rz=rz)
    export_fbx("prop_scattered_papers")


def make_broken_bottle():
    clear_scene()
    glass = mat("BottleGlass", 130, 140, 130, metallic=0.1, roughness=0.15, alpha=0.6)
    glass_shard = mat("Shard", 140, 150, 140, metallic=0.1, roughness=0.1)
    fluid = mat("Fluid", 80, 90, 60, roughness=0.3, alpha=0.7)
    # 倒れた瓶の本体
    cyl("Bottle", 0, 0, 0.04, 0.04, 0.16, glass, rx=math.pi / 2)
    # 割れた首
    cyl("Neck", 0.12, 0, 0.04, 0.02, 0.06, glass, rx=math.pi / 2)
    # 飛び散ったガラス片
    for i in range(8):
        ang = i * math.pi / 4 + random.uniform(-0.3, 0.3)
        d = random.uniform(0.1, 0.3)
        x, y = math.cos(ang) * d, math.sin(ang) * d
        sz = random.uniform(0.008, 0.025)
        cube(f"Shard{i}", x, y, 0.005, sz, sz * 1.5, 0.003,
             glass_shard, rz=random.uniform(0, math.pi))
    # 液体だまり
    cyl("Spill", 0.04, 0, 0.003, 0.18, 0.006, fluid)
    export_fbx("prop_broken_bottle")


def make_toppled_shelf():
    clear_scene()
    metal = mat("ShelfM", 105, 100, 92, metallic=0.5, roughness=0.85)
    rust = mat("ShelfR", 95, 48, 18, metallic=0.3, roughness=0.92)
    # 倒れた金属棚 (90度傾けて)
    # 縦柱 → 横向き
    for xi in [-0.45, 0.45]:
        for zi in [-0.15, 0.15]:
            cube(f"P{xi}{zi}", xi, 0.5, zi + 0.5, 0.03, 0.03, 2.0, rust, rx=math.pi / 2)
    # 棚板
    for y in [0.1, 0.55, 1.0, 1.45, 1.88]:
        cube(f"B{y}", 0, y, 0.5, 0.9, 0.025, 0.3, metal, rx=math.pi / 2)
    export_fbx("prop_toppled_shelf")


def make_old_paper_on_wall():
    clear_scene()
    paper = mat("OldPaper", 200, 180, 145, roughness=0.95)
    pin = mat("Pin", 150, 145, 140, metallic=0.6, roughness=0.5)
    # 紙
    cube("Paper", 0, 0, 0, 0.18, 0.003, 0.24, paper)
    # ピン
    cyl("Pin", 0, -0.005, 0.10, 0.005, 0.01, pin, rx=math.pi / 2)
    export_fbx("prop_old_paper")


def make_condensation_window():
    clear_scene()
    wood = mat("WW", 90, 72, 52, roughness=0.88)
    glass = mat("CondGlass", 130, 145, 140, roughness=0.55, alpha=0.7)
    dark = mat("ExtNight", 8, 10, 14, roughness=0.05)
    fog = mat("Fog", 200, 210, 205, roughness=0.6, alpha=0.4)
    # フレーム
    cube("F_T", 0, 0, 0.65, 0.82, 0.1, 0.06, wood)
    cube("F_B", 0, 0, -0.65, 0.82, 0.1, 0.06, wood)
    cube("F_L", -0.41, 0, 0, 0.06, 0.1, 1.3, wood)
    cube("F_R", 0.41, 0, 0, 0.06, 0.1, 1.3, wood)
    cube("Div", 0, 0, 0, 0.82, 0.08, 0.05, wood)
    # ガラス（曇り）
    cube("G_T", 0, 0.015, 0.33, 0.74, 0.04, 0.57, glass)
    cube("G_B", 0, 0.015, -0.33, 0.74, 0.04, 0.57, glass)
    # 結露の薄い膜
    cube("Fog_T", 0, 0.018, 0.33, 0.73, 0.041, 0.56, fog)
    cube("Fog_B", 0, 0.018, -0.33, 0.73, 0.041, 0.56, fog)
    # 外側暗黒
    cube("Night", 0, 0.12, 0, 0.78, 0.02, 1.2, dark)
    export_fbx("prop_condensation_window")


def make_bed_curtain():
    clear_scene()
    rail = mat("Rail", 140, 138, 132, metallic=0.6, roughness=0.6)
    curtain = mat("CurtainDirty", 175, 168, 150, roughness=0.96)
    # コの字レール (1.4 x 2.2)
    cyl("R_S1", -0.7, 0, 1.95, 0.012, 2.2, rail, rx=math.pi / 2)
    cyl("R_S2", 0.7, 0, 1.95, 0.012, 2.2, rail, rx=math.pi / 2)
    cyl("R_F", 0, -1.1, 1.95, 0.012, 1.4, rail, ry=math.pi / 2)
    # 縦支柱
    for px in [(-0.7, 1.1), (0.7, 1.1), (-0.7, -1.1), (0.7, -1.1)]:
        cube(f"Post", px[0], px[1], 1.0, 0.02, 0.02, 1.95, rail)
    # カーテン（汚れた白）半開き
    cube("Cur_L", -0.4, 0, 0.95, 0.04, 2.2, 1.95, curtain)
    cube("Cur_F", 0.4, -0.8, 0.95, 0.6, 0.04, 1.95, curtain)
    export_fbx("prop_bed_curtain_dirty")


# ─────────────────────────────────────────────────────────────
# RUN
# ─────────────────────────────────────────────────────────────
print("=== Generating wall/floor textures ===")
# テクスチャはサイズが大きい(2048x2048x4=16MB float配列)ので、
# Blender内で実行できるが時間がかかる。サイズを1024に縮小して高速化
print("1. corridor wall 2tone")
make_corridor_wall_2tone("corridor_wall_2tone", w=1024, h=1024)
print("2. patient wall")
make_patient_wall("patient_wall_dirty", w=1024, h=1024)
print("3. basement concrete")
make_basement_concrete("basement_concrete_damp", w=1024, h=1024)
print("4. floor linoleum tile")
make_floor_linoleum("floor_linoleum_tile", w=1024, h=1024)

print("=== Generating decals ===")
make_decal_blood("decal_blood_01", w=512, h=512)
make_decal_blood("decal_blood_02", w=512, h=512)
make_decal_mold("decal_mold_01", w=512, h=512)
make_decal_mold("decal_mold_02", w=512, h=512)
make_decal_waterstain("decal_waterstain_01", w=512, h=1024)
make_decal_graffiti("decal_scratch_01", [], w=512, h=256)

print("=== Rendering graffiti text ===")
render_graffiti_text("graffiti_tasukete", "たすけて", color=(0.18, 0.05, 0.05), w=1024, h=512)
render_graffiti_text("graffiti_derarenai", "でられない", color=(0.05, 0.05, 0.08), w=1024, h=512)
render_graffiti_text("graffiti_kokoniiru", "ここにいる", color=(0.10, 0.04, 0.04), w=1024, h=512)

print("=== Generating new props ===")
make_fallen_iv_bag()
make_peeling_poster()
make_scattered_papers()
make_broken_bottle()
make_toppled_shelf()
make_old_paper_on_wall()
make_condensation_window()
make_bed_curtain()

print("=== DONE ===")
