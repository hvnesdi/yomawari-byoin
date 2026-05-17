import bpy
import os
import sys

OUT_DIR = r"C:\Users\hvnes\Desktop\YomawariByoin_Assets\Textures"
os.makedirs(OUT_DIR, exist_ok=True)

RES = 2048

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)

def save_image(img, name):
    path = os.path.join(OUT_DIR, name)
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    print(f"Saved: {path}")

def make_node_texture(name, nodes_setup_fn):
    """Create a material, set up nodes, bake to image."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    for n in tree.nodes:
        tree.nodes.remove(n)
    nodes_setup_fn(tree)
    return mat

def bake_material_to_image(mat, img, bake_type='DIFFUSE'):
    bpy.ops.mesh.primitive_plane_add(size=2)
    obj = bpy.context.active_object
    obj.data.materials.clear()
    obj.data.materials.append(mat)

    tree = mat.node_tree
    bake_node = tree.nodes.new('ShaderNodeTexImage')
    bake_node.image = img
    bake_node.select = True
    tree.nodes.active = bake_node

    bpy.context.scene.render.engine = 'CYCLES'
    bpy.context.scene.cycles.samples = 32

    if bake_type == 'DIFFUSE':
        bpy.context.scene.render.bake.use_pass_direct = False
        bpy.context.scene.render.bake.use_pass_indirect = False
        bpy.context.scene.render.bake.use_pass_color = True
        bpy.ops.object.bake(type='DIFFUSE')
    elif bake_type == 'NORMAL':
        bpy.ops.object.bake(type='NORMAL')
    elif bake_type == 'ROUGHNESS':
        bpy.ops.object.bake(type='ROUGHNESS')

    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    return img

# ─────────────────────────────────────
# 1. 廊下の壁 (HospitalWall)
# ─────────────────────────────────────
def setup_wall_diffuse(tree):
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    noise1 = tree.nodes.new('ShaderNodeTexNoise')
    noise1.inputs['Scale'].default_value = 8.0
    noise1.inputs['Detail'].default_value = 10.0
    noise1.inputs['Roughness'].default_value = 0.7

    noise2 = tree.nodes.new('ShaderNodeTexNoise')
    noise2.inputs['Scale'].default_value = 2.0
    noise2.inputs['Detail'].default_value = 8.0

    ramp1 = tree.nodes.new('ShaderNodeValToRGB')
    ramp1.color_ramp.elements[0].color = (0.85, 0.82, 0.72, 1.0)
    ramp1.color_ramp.elements[1].color = (0.92, 0.90, 0.82, 1.0)
    tree.links.new(noise1.outputs['Fac'], ramp1.inputs['Fac'])

    ramp2 = tree.nodes.new('ShaderNodeValToRGB')
    ramp2.color_ramp.elements[0].color = (0.55, 0.48, 0.35, 1.0)
    ramp2.color_ramp.elements[1].color = (0.9, 0.88, 0.80, 1.0)
    ramp2.color_ramp.elements[0].position = 0.3
    tree.links.new(noise2.outputs['Fac'], ramp2.inputs['Fac'])

    mix = tree.nodes.new('ShaderNodeMixRGB')
    mix.blend_type = 'MULTIPLY'
    mix.inputs['Fac'].default_value = 0.6
    tree.links.new(ramp1.outputs['Color'], mix.inputs['Color1'])
    tree.links.new(ramp2.outputs['Color'], mix.inputs['Color2'])

    tree.links.new(mix.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85
    bsdf.inputs['Specular IOR Level'].default_value = 0.1

def setup_wall_roughness(tree):
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    noise = tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 12.0
    noise.inputs['Detail'].default_value = 8.0
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.7, 0.7, 0.7, 1.0)
    ramp.color_ramp.elements[1].color = (0.95, 0.95, 0.95, 1.0)
    tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.9

# ─────────────────────────────────────
# 2. 廊下の床 (HospitalFloor)
# ─────────────────────────────────────
def setup_floor_diffuse(tree):
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    noise1 = tree.nodes.new('ShaderNodeTexNoise')
    noise1.inputs['Scale'].default_value = 5.0
    noise1.inputs['Detail'].default_value = 12.0
    noise1.inputs['Roughness'].default_value = 0.8

    ramp1 = tree.nodes.new('ShaderNodeValToRGB')
    ramp1.color_ramp.elements[0].color = (0.58, 0.55, 0.50, 1.0)
    ramp1.color_ramp.elements[1].color = (0.70, 0.67, 0.60, 1.0)
    tree.links.new(noise1.outputs['Fac'], ramp1.inputs['Fac'])

    noise2 = tree.nodes.new('ShaderNodeTexNoise')
    noise2.inputs['Scale'].default_value = 20.0
    noise2.inputs['Detail'].default_value = 16.0
    ramp2 = tree.nodes.new('ShaderNodeValToRGB')
    ramp2.color_ramp.elements[0].color = (0.35, 0.30, 0.25, 1.0)
    ramp2.color_ramp.elements[0].position = 0.45
    ramp2.color_ramp.elements[1].color = (0.68, 0.65, 0.58, 1.0)
    tree.links.new(noise2.outputs['Fac'], ramp2.inputs['Fac'])

    mix = tree.nodes.new('ShaderNodeMixRGB')
    mix.blend_type = 'MULTIPLY'
    mix.inputs['Fac'].default_value = 0.5
    tree.links.new(ramp1.outputs['Color'], mix.inputs['Color1'])
    tree.links.new(ramp2.outputs['Color'], mix.inputs['Color2'])
    tree.links.new(mix.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.9

# ─────────────────────────────────────
# 3. 天井 (HospitalCeiling)
# ─────────────────────────────────────
def setup_ceiling_diffuse(tree):
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    noise1 = tree.nodes.new('ShaderNodeTexNoise')
    noise1.inputs['Scale'].default_value = 4.0
    noise1.inputs['Detail'].default_value = 8.0

    ramp1 = tree.nodes.new('ShaderNodeValToRGB')
    ramp1.color_ramp.elements[0].color = (0.78, 0.75, 0.68, 1.0)
    ramp1.color_ramp.elements[1].color = (0.88, 0.87, 0.83, 1.0)
    tree.links.new(noise1.outputs['Fac'], ramp1.inputs['Fac'])

    noise2 = tree.nodes.new('ShaderNodeTexNoise')
    noise2.inputs['Scale'].default_value = 3.0
    ramp2 = tree.nodes.new('ShaderNodeValToRGB')
    ramp2.color_ramp.elements[0].color = (0.50, 0.48, 0.40, 1.0)
    ramp2.color_ramp.elements[0].position = 0.55
    ramp2.color_ramp.elements[1].color = (0.85, 0.84, 0.79, 1.0)
    tree.links.new(noise2.outputs['Fac'], ramp2.inputs['Fac'])

    mix = tree.nodes.new('ShaderNodeMixRGB')
    mix.blend_type = 'MULTIPLY'
    mix.inputs['Fac'].default_value = 0.7
    tree.links.new(ramp1.outputs['Color'], mix.inputs['Color1'])
    tree.links.new(ramp2.outputs['Color'], mix.inputs['Color2'])
    tree.links.new(mix.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.92

# ─────────────────────────────────────
# 4. 病室の壁 (PatientRoomWall)
# ─────────────────────────────────────
def setup_patientwall_diffuse(tree):
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    noise1 = tree.nodes.new('ShaderNodeTexNoise')
    noise1.inputs['Scale'].default_value = 6.0
    noise1.inputs['Detail'].default_value = 12.0
    noise1.inputs['Roughness'].default_value = 0.8

    ramp1 = tree.nodes.new('ShaderNodeValToRGB')
    ramp1.color_ramp.elements[0].color = (0.65, 0.60, 0.50, 1.0)
    ramp1.color_ramp.elements[1].color = (0.82, 0.80, 0.72, 1.0)
    tree.links.new(noise1.outputs['Fac'], ramp1.inputs['Fac'])

    noise2 = tree.nodes.new('ShaderNodeTexNoise')
    noise2.inputs['Scale'].default_value = 15.0
    noise2.inputs['Detail'].default_value = 16.0
    ramp2 = tree.nodes.new('ShaderNodeValToRGB')
    ramp2.color_ramp.elements[0].color = (0.30, 0.25, 0.20, 1.0)
    ramp2.color_ramp.elements[0].position = 0.40
    ramp2.color_ramp.elements[1].color = (0.80, 0.78, 0.70, 1.0)
    tree.links.new(noise2.outputs['Fac'], ramp2.inputs['Fac'])

    mix = tree.nodes.new('ShaderNodeMixRGB')
    mix.blend_type = 'MULTIPLY'
    mix.inputs['Fac'].default_value = 0.75
    tree.links.new(ramp1.outputs['Color'], mix.inputs['Color1'])
    tree.links.new(ramp2.outputs['Color'], mix.inputs['Color2'])
    tree.links.new(mix.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.95

# ─────────────────────────────────────
# Generic roughness / normal setups
# ─────────────────────────────────────
def setup_generic_roughness(tree):
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    noise = tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 10.0
    noise.inputs['Detail'].default_value = 8.0
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.65, 0.65, 0.65, 1.0)
    ramp.color_ramp.elements[1].color = (0.95, 0.95, 0.95, 1.0)
    tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85

def setup_normal_map(tree):
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    noise = tree.nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 20.0
    noise.inputs['Detail'].default_value = 12.0
    noise.inputs['Roughness'].default_value = 0.6
    bump = tree.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 1.0
    tree.links.new(noise.outputs['Fac'], bump.inputs['Height'])
    tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.5, 0.5, 1.0, 1.0)
    ramp.color_ramp.elements[1].color = (0.6, 0.6, 1.0, 1.0)
    tree.links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    tree.links.new(ramp.outputs['Color'], bsdf.inputs['Base Color'])

# ─────────────────────────────────────
# MAIN
# ─────────────────────────────────────
textures = [
    ("HospitalWall_Diffuse",     setup_wall_diffuse,        'DIFFUSE'),
    ("HospitalWall_Normal",      setup_normal_map,          'NORMAL'),
    ("HospitalWall_Roughness",   setup_wall_roughness,      'ROUGHNESS'),
    ("HospitalFloor_Diffuse",    setup_floor_diffuse,       'DIFFUSE'),
    ("HospitalFloor_Normal",     setup_normal_map,          'NORMAL'),
    ("HospitalFloor_Roughness",  setup_generic_roughness,   'ROUGHNESS'),
    ("HospitalCeiling_Diffuse",  setup_ceiling_diffuse,     'DIFFUSE'),
    ("HospitalCeiling_Normal",   setup_normal_map,          'NORMAL'),
    ("HospitalCeiling_Roughness",setup_generic_roughness,   'ROUGHNESS'),
    ("PatientWall_Diffuse",      setup_patientwall_diffuse, 'DIFFUSE'),
    ("PatientWall_Normal",       setup_normal_map,          'NORMAL'),
    ("PatientWall_Roughness",    setup_generic_roughness,   'ROUGHNESS'),
]

for tex_name, setup_fn, bake_type in textures:
    print(f"\n=== Creating {tex_name} ===")
    clear_scene()
    img = bpy.data.images.new(tex_name, width=RES, height=RES, float_buffer=True)
    mat = make_node_texture(tex_name, setup_fn)
    bake_material_to_image(mat, img, bake_type)
    save_image(img, tex_name + ".png")

print("\nAll textures created successfully!")
