import bpy
import os
import math

OUT_DIR = r"C:\Users\hvnes\Desktop\YomawariByoin_Assets\Models"
os.makedirs(OUT_DIR, exist_ok=True)

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)

def add_rust_material(name, base_r=0.6, base_g=0.55, base_b=0.5):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    for n in tree.nodes:
        tree.nodes.remove(n)
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    noise = tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 15.0
    noise.inputs['Detail'].default_value = 8.0
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.35, 0.18, 0.08, 1.0)
    ramp.color_ramp.elements[0].position = 0.4
    ramp.color_ramp.elements[1].color = (base_r, base_g, base_b, 1.0)
    tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85
    bsdf.inputs['Metallic'].default_value = 0.7
    return mat

def add_white_sheet_material():
    mat = bpy.data.materials.new("WhiteSheet")
    mat.use_nodes = True
    tree = mat.node_tree
    for n in tree.nodes:
        tree.nodes.remove(n)
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    noise = tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 6.0
    noise.inputs['Detail'].default_value = 8.0
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.70, 0.68, 0.60, 1.0)
    ramp.color_ramp.elements[1].color = (0.88, 0.87, 0.82, 1.0)
    tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.95
    return mat

def add_peeled_wood_material():
    mat = bpy.data.materials.new("PeeledWood")
    mat.use_nodes = True
    tree = mat.node_tree
    for n in tree.nodes:
        tree.nodes.remove(n)
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    noise = tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 5.0
    noise.inputs['Detail'].default_value = 10.0
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.28, 0.20, 0.14, 1.0)
    ramp.color_ramp.elements[1].color = (0.60, 0.50, 0.38, 1.0)
    tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.88
    return mat

def export_fbx(name):
    path = os.path.join(OUT_DIR, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE',
        bake_space_transform=False,
        object_types={'MESH', 'ARMATURE'},
        use_mesh_modifiers=True,
        mesh_smooth_type='FACE',
        use_tspace=True,
    )
    print(f"Exported: {path}")

# ─────────────────────────────────────
# MODEL 1: 病院ベッド (HospitalBed)
# ─────────────────────────────────────
def create_hospital_bed():
    clear_scene()
    rust_mat = add_rust_material("BedFrame", 0.75, 0.72, 0.68)
    sheet_mat = add_white_sheet_material()

    bpy.ops.mesh.primitive_cube_add(size=1)
    mattress = bpy.context.active_object
    mattress.name = "Mattress"
    mattress.scale = (0.95, 1.95, 0.12)
    mattress.location = (0, 0, 0.55)
    mattress.data.materials.append(sheet_mat)

    for x, y in [(-0.45, -0.9), (0.45, -0.9), (-0.45, 0.9), (0.45, 0.9)]:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.025, depth=0.5, vertices=8)
        leg = bpy.context.active_object
        leg.name = "Leg"
        leg.location = (x, y, 0.25)
        leg.data.materials.append(rust_mat)

    for x in [-0.47, 0.47]:
        bpy.ops.mesh.primitive_cube_add()
        rail = bpy.context.active_object
        rail.name = "SideRail"
        rail.scale = (0.015, 0.85, 0.04)
        rail.location = (x, 0, 0.7)
        rail.data.materials.append(rust_mat)

    bpy.ops.mesh.primitive_cube_add()
    head = bpy.context.active_object
    head.name = "Headboard"
    head.scale = (0.48, 0.03, 0.30)
    head.location = (0, -0.95, 0.7)
    head.data.materials.append(rust_mat)

    bpy.ops.mesh.primitive_cube_add()
    foot = bpy.context.active_object
    foot.name = "Footboard"
    foot.scale = (0.48, 0.03, 0.20)
    foot.location = (0, 0.95, 0.65)
    foot.data.materials.append(rust_mat)

    bpy.ops.mesh.primitive_cube_add()
    pillow = bpy.context.active_object
    pillow.name = "Pillow"
    pillow.scale = (0.35, 0.25, 0.05)
    pillow.location = (0, -0.65, 0.68)
    pillow.data.materials.append(sheet_mat)

    export_fbx("HospitalBed")

# ─────────────────────────────────────
# MODEL 2: 点滴スタンド (IVStand)
# ─────────────────────────────────────
def create_iv_stand():
    clear_scene()
    rust_mat = add_rust_material("IVMetal", 0.72, 0.70, 0.65)

    bpy.ops.mesh.primitive_cylinder_add(radius=0.02, depth=1.8, vertices=8)
    pole = bpy.context.active_object
    pole.name = "Pole"
    pole.location = (0, 0, 0.9)
    pole.data.materials.append(rust_mat)

    for angle in [0, 90]:
        bpy.ops.mesh.primitive_cube_add()
        base = bpy.context.active_object
        base.name = "Base"
        base.scale = (0.35, 0.02, 0.015)
        base.location = (0, 0, 0.02)
        base.rotation_euler = (0, 0, math.radians(angle))
        base.data.materials.append(rust_mat)

    for x, y in [(0.33, 0), (-0.33, 0), (0, 0.33), (0, -0.33)]:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.025, segments=8, ring_count=6)
        wheel = bpy.context.active_object
        wheel.name = "Wheel"
        wheel.location = (x, y, 0.025)
        wheel.data.materials.append(rust_mat)

    bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.25, vertices=8)
    arm = bpy.context.active_object
    arm.name = "Arm"
    arm.location = (0.1, 0, 1.78)
    arm.rotation_euler = (0, math.radians(90), 0)
    arm.data.materials.append(rust_mat)

    bpy.ops.mesh.primitive_torus_add(major_radius=0.02, minor_radius=0.005)
    hook = bpy.context.active_object
    hook.name = "Hook"
    hook.location = (0.22, 0, 1.78)
    hook.rotation_euler = (math.radians(90), 0, 0)
    hook.data.materials.append(rust_mat)

    export_fbx("IVStand")

# ─────────────────────────────────────
# MODEL 3: 病院ドア (HospitalDoor)
# ─────────────────────────────────────
def create_hospital_door():
    clear_scene()
    wood_mat = add_peeled_wood_material()
    metal_mat = add_rust_material("DoorMetal", 0.65, 0.62, 0.58)
    glass_mat = bpy.data.materials.new("DirtyGlass")
    glass_mat.use_nodes = True
    t = glass_mat.node_tree
    for n in t.nodes:
        t.nodes.remove(n)
    out = t.nodes.new('ShaderNodeOutputMaterial')
    bsdf = t.nodes.new('ShaderNodeBsdfPrincipled')
    t.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs['Base Color'].default_value = (0.6, 0.65, 0.55, 1.0)
    bsdf.inputs['Roughness'].default_value = 0.5
    bsdf.inputs['Alpha'].default_value = 0.3
    glass_mat.blend_method = 'BLEND'

    bpy.ops.mesh.primitive_cube_add()
    door = bpy.context.active_object
    door.name = "DoorPanel"
    door.scale = (0.42, 0.04, 1.0)
    door.location = (0, 0, 1.0)
    door.data.materials.append(wood_mat)

    bpy.ops.mesh.primitive_cube_add()
    window = bpy.context.active_object
    window.name = "DoorWindow"
    window.scale = (0.12, 0.045, 0.10)
    window.location = (0.1, 0, 1.55)
    window.data.materials.append(glass_mat)

    for x, sx in [(-0.46, 0.04), (0.46, 0.04)]:
        bpy.ops.mesh.primitive_cube_add()
        frame = bpy.context.active_object
        frame.name = "Frame"
        frame.scale = (sx, 0.06, 1.05)
        frame.location = (x, 0, 1.0)
        frame.data.materials.append(metal_mat)

    bpy.ops.mesh.primitive_cube_add()
    top_frame = bpy.context.active_object
    top_frame.name = "TopFrame"
    top_frame.scale = (0.50, 0.06, 0.04)
    top_frame.location = (0, 0, 2.05)
    top_frame.data.materials.append(metal_mat)

    bpy.ops.mesh.primitive_cylinder_add(radius=0.025, depth=0.08, vertices=12)
    knob = bpy.context.active_object
    knob.name = "Knob"
    knob.rotation_euler = (0, math.radians(90), 0)
    knob.location = (0.35, -0.06, 1.0)
    knob.data.materials.append(metal_mat)

    export_fbx("HospitalDoor")

# ─────────────────────────────────────
# MODEL 4: 蛍光灯 (FluorescentLight)
# ─────────────────────────────────────
def create_fluorescent_light():
    clear_scene()
    metal_mat = add_rust_material("LightHousing", 0.78, 0.76, 0.72)
    tube_mat = bpy.data.materials.new("FluorescentTube")
    tube_mat.use_nodes = True
    t = tube_mat.node_tree
    for n in t.nodes:
        t.nodes.remove(n)
    out = t.nodes.new('ShaderNodeOutputMaterial')
    emit = t.nodes.new('ShaderNodeEmission')
    emit.inputs['Color'].default_value = (0.85, 0.92, 0.78, 1.0)
    emit.inputs['Strength'].default_value = 2.0
    t.links.new(emit.outputs['Emission'], out.inputs['Surface'])

    bpy.ops.mesh.primitive_cube_add()
    housing = bpy.context.active_object
    housing.name = "Housing"
    housing.scale = (0.06, 0.60, 0.04)
    housing.location = (0, 0, 0.04)
    housing.data.materials.append(metal_mat)

    bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=1.10, vertices=12)
    tube = bpy.context.active_object
    tube.name = "Tube"
    tube.rotation_euler = (0, math.radians(90), 0)
    tube.location = (0, 0, -0.015)
    tube.data.materials.append(tube_mat)

    for y in [-0.56, 0.56]:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.018, depth=0.02, vertices=8)
        cap = bpy.context.active_object
        cap.name = "Cap"
        cap.rotation_euler = (0, math.radians(90), 0)
        cap.location = (y, 0, -0.015)
        cap.data.materials.append(metal_mat)

    bpy.ops.mesh.primitive_cube_add()
    bracket = bpy.context.active_object
    bracket.name = "Bracket"
    bracket.scale = (0.03, 0.03, 0.06)
    bracket.location = (0, 0, 0.10)
    bracket.data.materials.append(metal_mat)

    export_fbx("FluorescentLight")

# ─────────────────────────────────────
# Run all
# ─────────────────────────────────────
print("Creating HospitalBed...")
create_hospital_bed()

print("Creating IVStand...")
create_iv_stand()

print("Creating HospitalDoor...")
create_hospital_door()

print("Creating FluorescentLight...")
create_fluorescent_light()

print("\nAll models created successfully!")
