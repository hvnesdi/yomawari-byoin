"""残り（落書きテキスト + 新規小物）を生成"""
import bpy, os, math, random
random.seed(13)
PROJ = r"C:\Users\hvnes\YomawariByoin"
DECAL_OUT = os.path.join(PROJ, r"Assets\Textures\Decals")
PROP_OUT = os.path.join(PROJ, r"Assets\Models\Props")


def render_graffiti_text(name, text, color=(0.05, 0.05, 0.08), w=1024, h=512):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.text_add(location=(0, 0, 0))
    txt = bpy.context.active_object
    txt.data.body = text
    txt.data.size = 1.0
    txt.data.align_x = 'CENTER'
    txt.data.align_y = 'CENTER'
    m = bpy.data.materials.new(f"GraffitiMat_{name}")
    m.use_nodes = True
    n = m.node_tree.nodes['Principled BSDF']
    n.inputs['Base Color'].default_value = (color[0], color[1], color[2], 1)
    try:
        n.inputs['Emission Color'].default_value = (color[0], color[1], color[2], 1)
        n.inputs['Emission Strength'].default_value = 1.0
    except:
        pass
    txt.data.materials.append(m)

    bpy.ops.object.camera_add(location=(0, 0, 5))
    cam = bpy.context.active_object
    cam.rotation_euler = (0, 0, 0)
    cam.data.type = 'ORTHO'
    cam.data.ortho_scale = 8.0
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type='SUN', location=(0, 0, 3))
    bpy.context.active_object.data.energy = 5

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = w
    scene.render.resolution_y = h
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.view_settings.view_transform = 'Standard'
    out_path = os.path.join(DECAL_OUT, name + ".png")
    scene.render.filepath = out_path
    bpy.ops.render.render(write_still=True)
    print(f"  rendered {name}.png")


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
    n.inputs['Base Color'].default_value = (r / 255, g / 255, b / 255, alpha)
    n.inputs['Metallic'].default_value = metallic
    n.inputs['Roughness'].default_value = roughness
    if alpha < 1.0:
        n.inputs['Alpha'].default_value = alpha
        m.blend_method = 'BLEND'
    return m


def add_mat(o, m):
    if o.data.materials:
        o.data.materials[0] = m
    else:
        o.data.materials.append(m)


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


print("=== Rendering graffiti text ===")
render_graffiti_text("graffiti_tasukete", "たすけて", color=(0.18, 0.05, 0.05))
render_graffiti_text("graffiti_derarenai", "でられない", color=(0.05, 0.05, 0.08))
render_graffiti_text("graffiti_kokoniiru", "ここにいる", color=(0.10, 0.04, 0.04))


print("=== Generating new props ===")
# 落ちた点滴袋
clear_scene()
bag = mat("IVBagFallen", 200, 215, 200, roughness=0.4, alpha=0.7)
tube = mat("Tube", 160, 158, 152, metallic=0.3, roughness=0.6)
cube("Bag", 0, 0, 0.02, 0.16, 0.12, 0.04, bag)
cyl("Tube1", -0.12, 0.05, 0.02, 0.004, 0.2, tube, rx=math.radians(85), rz=math.radians(20))
cyl("Tube2", -0.20, 0.12, 0.02, 0.004, 0.15, tube, rx=math.radians(85), rz=math.radians(60))
export_fbx("prop_iv_bag_fallen")

# 剥がれかけポスター
clear_scene()
paper = mat("Poster", 195, 175, 140, roughness=0.95)
paper_back = mat("PosterBack", 165, 145, 110, roughness=0.95)
cube("Poster_Back", 0, 0, 0, 0.3, 0.005, 0.4, paper)
o = cube("Poster_Curl", 0, -0.04, 0.20, 0.3, 0.005, 0.08, paper_back, rx=math.radians(-30))
o.location.z = 0.20
cube("Poster_Tear", -0.13, 0.005, 0.18, 0.04, 0.004, 0.06, paper, rz=math.radians(15))
export_fbx("prop_peeling_poster")

# 散乱した書類
clear_scene()
paper_mats = [
    mat(f"Paper{i}", random.randint(195, 220), random.randint(180, 210), random.randint(140, 175), roughness=0.96)
    for i in range(4)
]
for i in range(30):
    x = random.uniform(-0.6, 0.6)
    y = random.uniform(-0.5, 0.5)
    rz = random.uniform(0, math.pi)
    rx = random.uniform(-0.1, 0.1)
    cube(f"Pp{i}", x, y, 0.002 + i * 0.0003, 0.105, 0.148, 0.0005,
         paper_mats[i % 4], rx=rx, rz=rz)
export_fbx("prop_scattered_papers")

# 割れたガラス瓶
clear_scene()
glass = mat("BottleGlass", 130, 140, 130, metallic=0.1, roughness=0.15, alpha=0.6)
glass_shard = mat("Shard", 140, 150, 140, metallic=0.1, roughness=0.1)
fluid = mat("Fluid", 80, 90, 60, roughness=0.3, alpha=0.7)
cyl("Bottle", 0, 0, 0.04, 0.04, 0.16, glass, rx=math.pi / 2)
cyl("Neck", 0.12, 0, 0.04, 0.02, 0.06, glass, rx=math.pi / 2)
for i in range(8):
    ang = i * math.pi / 4 + random.uniform(-0.3, 0.3)
    d = random.uniform(0.1, 0.3)
    x, y = math.cos(ang) * d, math.sin(ang) * d
    sz = random.uniform(0.008, 0.025)
    cube(f"Shard{i}", x, y, 0.005, sz, sz * 1.5, 0.003,
         glass_shard, rz=random.uniform(0, math.pi))
cyl("Spill", 0.04, 0, 0.003, 0.18, 0.006, fluid)
export_fbx("prop_broken_bottle")

# 倒れた棚
clear_scene()
metal = mat("ShelfM", 105, 100, 92, metallic=0.5, roughness=0.85)
rust = mat("ShelfR", 95, 48, 18, metallic=0.3, roughness=0.92)
for xi in [-0.45, 0.45]:
    for zi in [-0.15, 0.15]:
        cube(f"P{xi}{zi}", xi, 0.5, zi + 0.5, 0.03, 0.03, 2.0, rust, rx=math.pi / 2)
for y in [0.1, 0.55, 1.0, 1.45, 1.88]:
    cube(f"B{y}", 0, y, 0.5, 0.9, 0.025, 0.3, metal, rx=math.pi / 2)
export_fbx("prop_toppled_shelf")

# 壁の古い紙
clear_scene()
paper = mat("OldPaper", 200, 180, 145, roughness=0.95)
pin = mat("Pin", 150, 145, 140, metallic=0.6, roughness=0.5)
cube("Paper", 0, 0, 0, 0.18, 0.003, 0.24, paper)
cyl("Pin", 0, -0.005, 0.10, 0.005, 0.01, pin, rx=math.pi / 2)
export_fbx("prop_old_paper")

# 結露窓
clear_scene()
wood = mat("WW", 90, 72, 52, roughness=0.88)
glass = mat("CondGlass", 130, 145, 140, roughness=0.55, alpha=0.7)
dark = mat("ExtNight", 8, 10, 14, roughness=0.05)
fog = mat("Fog", 200, 210, 205, roughness=0.6, alpha=0.4)
cube("F_T", 0, 0, 0.65, 0.82, 0.1, 0.06, wood)
cube("F_B", 0, 0, -0.65, 0.82, 0.1, 0.06, wood)
cube("F_L", -0.41, 0, 0, 0.06, 0.1, 1.3, wood)
cube("F_R", 0.41, 0, 0, 0.06, 0.1, 1.3, wood)
cube("Div", 0, 0, 0, 0.82, 0.08, 0.05, wood)
cube("G_T", 0, 0.015, 0.33, 0.74, 0.04, 0.57, glass)
cube("G_B", 0, 0.015, -0.33, 0.74, 0.04, 0.57, glass)
cube("Fog_T", 0, 0.018, 0.33, 0.73, 0.041, 0.56, fog)
cube("Fog_B", 0, 0.018, -0.33, 0.73, 0.041, 0.56, fog)
cube("Night", 0, 0.12, 0, 0.78, 0.02, 1.2, dark)
export_fbx("prop_condensation_window")

# ベッド周りカーテン（汚れた白）
clear_scene()
rail = mat("Rail", 140, 138, 132, metallic=0.6, roughness=0.6)
curtain = mat("CurtainDirty", 175, 168, 150, roughness=0.96)
cyl("R_S1", -0.7, 0, 1.95, 0.012, 2.2, rail, rx=math.pi / 2)
cyl("R_S2", 0.7, 0, 1.95, 0.012, 2.2, rail, rx=math.pi / 2)
cyl("R_F", 0, -1.1, 1.95, 0.012, 1.4, rail, ry=math.pi / 2)
for px in [(-0.7, 1.1), (0.7, 1.1), (-0.7, -1.1), (0.7, -1.1)]:
    cube("Post", px[0], px[1], 1.0, 0.02, 0.02, 1.95, rail)
cube("Cur_L", -0.4, 0, 0.95, 0.04, 2.2, 1.95, curtain)
cube("Cur_F", 0.4, -0.8, 0.95, 0.6, 0.04, 1.95, curtain)
export_fbx("prop_bed_curtain_dirty")

print("=== DONE ===")
