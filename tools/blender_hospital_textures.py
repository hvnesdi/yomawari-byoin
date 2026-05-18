"""
Hospital horror textures via Cycles baking.
Targets Blender 5.x (5.1.1 tested). Handles API changes:
  - ShaderNodeTexMusgrave removed (4.1+) -> use ShaderNodeTexNoise w/ high detail
  - Principled BSDF: 'Emission' -> 'Emission Color' (4.0+)
  - Material.blend_method deprecated (4.2+); use surface_render_method
"""

import bpy
import os
import sys
import math

# ─── Output path ──────────────────────────────────────────────────────────
# Allow override via env var so the wrapper can target the worktree path.
OUT = os.environ.get("HOSPITAL_TEX_OUT")
if not OUT:
    OUT = r"C:\Users\hvnes\YomawariByoin\.claude\worktrees\quizzical-elgamal-5af78a\Assets\Textures\Hospital"
os.makedirs(OUT, exist_ok=True)

SIZE = 1024  # 1k is plenty for these architectural surfaces and bakes much faster

# ─── Helpers ──────────────────────────────────────────────────────────────

def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for img in list(bpy.data.images):
        if img.users == 0:
            bpy.data.images.remove(img)
    for mat in list(bpy.data.materials):
        bpy.data.materials.remove(mat)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def setup_cycles(samples=24):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    # bake settings: capture only base color, not lighting
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 16
    # CPU is more reliable headless and avoids GPU init issues
    scene.cycles.device = 'CPU'


def make_plane():
    bpy.ops.mesh.primitive_plane_add(size=2)
    obj = bpy.context.active_object
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.unwrap(method='ANGLE_BASED', margin=0.001)
    bpy.ops.object.mode_set(mode='OBJECT')
    return obj


def new_material(obj, name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    for n in list(tree.nodes):
        tree.nodes.remove(n)
    out = tree.nodes.new('ShaderNodeOutputMaterial')
    bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
    tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    return mat, tree, bsdf, out


def add_tex_coord(tree, scale=(1, 1, 1)):
    tc = tree.nodes.new('ShaderNodeTexCoord')
    mp = tree.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = scale
    tree.links.new(tc.outputs['UV'], mp.inputs['Vector'])
    return mp


def add_noise(tree, vec_socket, scale=10, detail=8, roughness=0.6, distortion=0.0):
    n = tree.nodes.new('ShaderNodeTexNoise')
    n.inputs['Scale'].default_value = scale
    n.inputs['Detail'].default_value = detail
    n.inputs['Roughness'].default_value = roughness
    if 'Distortion' in n.inputs:
        n.inputs['Distortion'].default_value = distortion
    tree.links.new(vec_socket, n.inputs['Vector'])
    return n


def add_ramp(tree, fac_socket, stops):
    """stops: list of (position, (r,g,b,a))"""
    ramp = tree.nodes.new('ShaderNodeValToRGB')
    cr = ramp.color_ramp
    # ColorRamp starts with 2 elements; reuse them + add as needed
    while len(cr.elements) > 1:
        cr.elements.remove(cr.elements[-1])
    cr.elements[0].position = stops[0][0]
    cr.elements[0].color = stops[0][1]
    for pos, col in stops[1:]:
        e = cr.elements.new(pos)
        e.color = col
    tree.links.new(fac_socket, ramp.inputs['Fac'])
    return ramp


def add_mix(tree, fac, c1, c2, blend='MIX', value=None):
    m = tree.nodes.new('ShaderNodeMixRGB')
    m.blend_type = blend
    if value is not None:
        m.inputs['Fac'].default_value = value
    if fac is not None:
        tree.links.new(fac, m.inputs['Fac'])
    tree.links.new(c1, m.inputs['Color1'])
    tree.links.new(c2, m.inputs['Color2'])
    return m


def set_emission(bsdf, color, strength):
    """Handle 'Emission' (3.x) vs 'Emission Color' (4.x+)."""
    if 'Emission Color' in bsdf.inputs:
        bsdf.inputs['Emission Color'].default_value = color
    elif 'Emission' in bsdf.inputs:
        bsdf.inputs['Emission'].default_value = color
    if 'Emission Strength' in bsdf.inputs:
        bsdf.inputs['Emission Strength'].default_value = strength


def set_alpha_blend(mat):
    """Enable alpha blending across Blender versions."""
    try:
        mat.surface_render_method = 'BLENDED'
    except (AttributeError, TypeError):
        try:
            mat.blend_method = 'BLEND'
        except Exception:
            pass


def bake_to_png(obj, mat, bake_type, out_path, color_space='sRGB'):
    """Bake the active material to an image, save as PNG."""
    name = os.path.basename(out_path)
    img = bpy.data.images.new(name, width=SIZE, height=SIZE, alpha=False)
    if color_space == 'Non-Color':
        img.colorspace_settings.name = 'Non-Color'

    tree = mat.node_tree
    img_node = tree.nodes.new('ShaderNodeTexImage')
    img_node.image = img
    tree.nodes.active = img_node
    img_node.select = True

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj

    try:
        bpy.ops.object.bake(type=bake_type, use_clear=True)
    except RuntimeError as e:
        print(f"BAKE FAILED for {out_path}: {e}")
        raise

    img.filepath_raw = out_path
    img.file_format = 'PNG'
    img.save()
    tree.nodes.remove(img_node)
    bpy.data.images.remove(img)
    print(f"  saved {out_path}")


def bake_set(obj, mat, base_name, bsdf, bump_node=None):
    """Bake diffuse, roughness, normal for a material."""
    bake_to_png(obj, mat, 'DIFFUSE', os.path.join(OUT, f'{base_name}_Diffuse.png'), 'sRGB')
    bake_to_png(obj, mat, 'ROUGHNESS', os.path.join(OUT, f'{base_name}_Roughness.png'), 'Non-Color')
    if bump_node is not None:
        # The Normal bake reads from the bump-modified normal on the BSDF.
        bake_to_png(obj, mat, 'NORMAL', os.path.join(OUT, f'{base_name}_Normal.png'), 'Non-Color')


# ════════════════════════════════════════════════════════════════════════
# 1. Patient room wall: stained plaster, water marks
# ════════════════════════════════════════════════════════════════════════
def gen_ward_wall():
    print("=== gen_ward_wall ===")
    clear_scene()
    setup_cycles()
    obj = make_plane()
    mat, tree, bsdf, out = new_material(obj, 'WardWall')

    mp = add_tex_coord(tree, scale=(6, 6, 1))

    # Big lumpy plaster texture (Musgrave replacement: noise w/ high detail + low roughness)
    big = add_noise(tree, mp.outputs['Vector'], scale=2.5, detail=10, roughness=0.45)
    # Surface micro-detail
    fine = add_noise(tree, mp.outputs['Vector'], scale=22, detail=12, roughness=0.6)
    # Water-stain shapes
    voro = tree.nodes.new('ShaderNodeTexVoronoi')
    voro.feature = 'F1'
    voro.inputs['Scale'].default_value = 1.4
    voro.inputs['Randomness'].default_value = 0.95
    tree.links.new(mp.outputs['Vector'], voro.inputs['Vector'])

    # Base off-white plaster gradient
    base_ramp = add_ramp(tree, big.outputs['Fac'], [
        (0.0, (0.86, 0.84, 0.78, 1)),
        (0.5, (0.78, 0.76, 0.69, 1)),
        (1.0, (0.66, 0.63, 0.55, 1)),
    ])

    # Brown water-stain color
    stain_color = tree.nodes.new('ShaderNodeRGB')
    stain_color.outputs[0].default_value = (0.34, 0.24, 0.15, 1)

    # Mask: voronoi distance, ramp to make stains organic
    stain_mask = add_ramp(tree, voro.outputs['Distance'], [
        (0.45, (1, 1, 1, 1)),  # stained
        (0.70, (0, 0, 0, 1)),  # clean
    ])

    stained = add_mix(tree, stain_mask.outputs['Color'],
                       base_ramp.outputs['Color'], stain_color.outputs['Color'],
                       blend='MIX')

    # Light dust pass
    dust_ramp = add_ramp(tree, fine.outputs['Fac'], [
        (0.40, (0.92, 0.90, 0.83, 1)),
        (1.00, (0.74, 0.71, 0.62, 1)),
    ])
    final = add_mix(tree, None, stained.outputs['Color'], dust_ramp.outputs['Color'],
                     blend='MULTIPLY', value=0.25)

    tree.links.new(final.outputs['Color'], bsdf.inputs['Base Color'])

    # Roughness: micro-noise gives variation, plaster is rough overall
    rough_ramp = add_ramp(tree, fine.outputs['Fac'], [
        (0.0, (0.78, 0.78, 0.78, 1)),
        (1.0, (0.95, 0.95, 0.95, 1)),
    ])
    tree.links.new(rough_ramp.outputs['Color'], bsdf.inputs['Roughness'])

    # Bump from big + fine combined
    bump_mix = add_mix(tree, None, big.outputs['Color'], fine.outputs['Color'],
                       blend='ADD', value=0.5)
    bump = tree.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.6
    bump.inputs['Distance'].default_value = 0.04
    tree.links.new(bump_mix.outputs['Color'], bump.inputs['Height'])
    tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    bake_set(obj, mat, 'WardWall', bsdf, bump_node=bump)


# ════════════════════════════════════════════════════════════════════════
# 2. Corridor wall: classic hospital green wainscot + cream upper
# ════════════════════════════════════════════════════════════════════════
def gen_corridor_wall():
    print("=== gen_corridor_wall ===")
    clear_scene()
    setup_cycles()
    obj = make_plane()
    mat, tree, bsdf, out = new_material(obj, 'CorridorWall')

    # UV coords for top/bottom split: use Y of UV directly
    tc = tree.nodes.new('ShaderNodeTexCoord')
    sep = tree.nodes.new('ShaderNodeSeparateXYZ')
    tree.links.new(tc.outputs['UV'], sep.inputs['Vector'])

    mp = tree.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (5, 5, 1)
    tree.links.new(tc.outputs['UV'], mp.inputs['Vector'])

    # Two noise textures for upper / lower variation
    n_upper = add_noise(tree, mp.outputs['Vector'], scale=4, detail=10, roughness=0.55)
    n_lower = add_noise(tree, mp.outputs['Vector'], scale=8, detail=8, roughness=0.6)
    # Coarse stain
    voro = tree.nodes.new('ShaderNodeTexVoronoi')
    voro.feature = 'F1'
    voro.inputs['Scale'].default_value = 1.2
    voro.inputs['Randomness'].default_value = 0.9
    tree.links.new(mp.outputs['Vector'], voro.inputs['Vector'])

    # Upper cream tone
    upper_ramp = add_ramp(tree, n_upper.outputs['Fac'], [
        (0.0, (0.88, 0.85, 0.75, 1)),
        (0.6, (0.78, 0.74, 0.63, 1)),
        (1.0, (0.66, 0.60, 0.49, 1)),
    ])
    # Lower hospital-green wainscot
    lower_ramp = add_ramp(tree, n_lower.outputs['Fac'], [
        (0.0, (0.50, 0.58, 0.50, 1)),
        (0.6, (0.42, 0.49, 0.42, 1)),
        (1.0, (0.32, 0.39, 0.33, 1)),
    ])

    # Wainscot mask: Y < ~0.33 = lower
    wainscot_mask = tree.nodes.new('ShaderNodeValToRGB')
    wm = wainscot_mask.color_ramp
    wm.interpolation = 'CONSTANT'
    wm.elements[0].position = 0.32
    wm.elements[0].color = (1, 1, 1, 1)  # lower (where mask = 1)
    wm_e1 = wm.elements.new(0.34)
    wm_e1.color = (0, 0, 0, 1)  # upper
    tree.links.new(sep.outputs['Y'], wainscot_mask.inputs['Fac'])

    upper_lower_mix = add_mix(tree, wainscot_mask.outputs['Color'],
                               upper_ramp.outputs['Color'], lower_ramp.outputs['Color'])

    # Wood chair-rail border
    border_mask = tree.nodes.new('ShaderNodeValToRGB')
    bm = border_mask.color_ramp
    bm.interpolation = 'B_SPLINE'
    bm.elements[0].position = 0.305
    bm.elements[0].color = (0, 0, 0, 1)
    e1 = bm.elements.new(0.325)
    e1.color = (1, 1, 1, 1)
    e2 = bm.elements.new(0.345)
    e2.color = (1, 1, 1, 1)
    e3 = bm.elements.new(0.365)
    e3.color = (0, 0, 0, 1)
    tree.links.new(sep.outputs['Y'], border_mask.inputs['Fac'])
    wood = add_ramp(tree, n_lower.outputs['Fac'], [
        (0.0, (0.34, 0.22, 0.13, 1)),
        (1.0, (0.20, 0.13, 0.07, 1)),
    ])

    with_border = add_mix(tree, border_mask.outputs['Color'],
                           upper_lower_mix.outputs['Color'], wood.outputs['Color'])

    # Yellow water-stains, only on upper
    stain_color = tree.nodes.new('ShaderNodeRGB')
    stain_color.outputs[0].default_value = (0.30, 0.22, 0.12, 1)
    stain_mask = add_ramp(tree, voro.outputs['Distance'], [
        (0.55, (1, 1, 1, 1)),
        (0.85, (0, 0, 0, 1)),
    ])
    # Multiply by inverse-wainscot to keep stains only on the upper
    upper_only = tree.nodes.new('ShaderNodeMath')
    upper_only.operation = 'MULTIPLY'
    invert_wainscot = tree.nodes.new('ShaderNodeInvert')
    tree.links.new(wainscot_mask.outputs['Color'], invert_wainscot.inputs['Color'])
    tree.links.new(invert_wainscot.outputs['Color'], upper_only.inputs[0])
    tree.links.new(stain_mask.outputs['Color'], upper_only.inputs[1])

    final = add_mix(tree, upper_only.outputs['Value'],
                     with_border.outputs['Color'], stain_color.outputs['Color'])
    tree.links.new(final.outputs['Color'], bsdf.inputs['Base Color'])

    # Roughness
    rough_ramp = add_ramp(tree, n_upper.outputs['Fac'], [
        (0.0, (0.70, 0.70, 0.70, 1)),
        (1.0, (0.92, 0.92, 0.92, 1)),
    ])
    tree.links.new(rough_ramp.outputs['Color'], bsdf.inputs['Roughness'])

    bump = tree.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.4
    bump.inputs['Distance'].default_value = 0.03
    tree.links.new(n_upper.outputs['Fac'], bump.inputs['Height'])
    tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    bake_set(obj, mat, 'CorridorWall', bsdf, bump_node=bump)


# ════════════════════════════════════════════════════════════════════════
# 3. Linoleum floor: tile grid + grime + worn streaks
# ════════════════════════════════════════════════════════════════════════
def gen_floor():
    print("=== gen_floor ===")
    clear_scene()
    setup_cycles()
    obj = make_plane()
    mat, tree, bsdf, out = new_material(obj, 'Floor')

    mp = add_tex_coord(tree, scale=(8, 8, 1))

    brick = tree.nodes.new('ShaderNodeTexBrick')
    brick.inputs['Scale'].default_value = 4.0
    brick.inputs['Mortar Size'].default_value = 0.018
    brick.inputs['Mortar Smooth'].default_value = 0.4
    brick.inputs['Color1'].default_value = (0.66, 0.62, 0.54, 1)
    brick.inputs['Color2'].default_value = (0.62, 0.59, 0.51, 1)
    brick.inputs['Mortar'].default_value = (0.30, 0.27, 0.23, 1)
    if 'Squash' in brick.inputs:
        brick.inputs['Squash'].default_value = 1.0
    tree.links.new(mp.outputs['Vector'], brick.inputs['Vector'])

    # Dirt/grime noise
    grime = add_noise(tree, mp.outputs['Vector'], scale=2.5, detail=10, roughness=0.7)
    grime_ramp = add_ramp(tree, grime.outputs['Fac'], [
        (0.30, (0.42, 0.37, 0.30, 1)),
        (0.75, (0.78, 0.74, 0.66, 1)),
    ])
    # Streaks (warped noise)
    streak = add_noise(tree, mp.outputs['Vector'], scale=12, detail=4, roughness=0.4, distortion=2.5)
    streak_ramp = add_ramp(tree, streak.outputs['Fac'], [
        (0.45, (0.35, 0.32, 0.28, 1)),
        (0.65, (1, 1, 1, 1)),
    ])
    base_mix = add_mix(tree, None, brick.outputs['Color'], grime_ramp.outputs['Color'],
                       blend='MULTIPLY', value=0.55)
    final = add_mix(tree, None, base_mix.outputs['Color'], streak_ramp.outputs['Color'],
                     blend='MULTIPLY', value=0.35)
    tree.links.new(final.outputs['Color'], bsdf.inputs['Base Color'])

    rough_ramp = add_ramp(tree, grime.outputs['Fac'], [
        (0.0, (0.50, 0.50, 0.50, 1)),
        (1.0, (0.85, 0.85, 0.85, 1)),
    ])
    tree.links.new(rough_ramp.outputs['Color'], bsdf.inputs['Roughness'])

    # Subtle tile relief
    bump = tree.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.25
    bump.inputs['Distance'].default_value = 0.02
    tree.links.new(brick.outputs['Fac'], bump.inputs['Height'])
    tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    bake_set(obj, mat, 'Floor', bsdf, bump_node=bump)


# ════════════════════════════════════════════════════════════════════════
# 4. Basement concrete: cracks, water damage
# ════════════════════════════════════════════════════════════════════════
def gen_basement():
    print("=== gen_basement ===")
    clear_scene()
    setup_cycles()
    obj = make_plane()
    mat, tree, bsdf, out = new_material(obj, 'Basement')

    mp = add_tex_coord(tree, scale=(6, 6, 1))

    # Concrete base via noise
    conc = add_noise(tree, mp.outputs['Vector'], scale=5, detail=12, roughness=0.65)
    conc_ramp = add_ramp(tree, conc.outputs['Fac'], [
        (0.0, (0.34, 0.32, 0.30, 1)),
        (0.5, (0.42, 0.40, 0.38, 1)),
        (1.0, (0.50, 0.48, 0.45, 1)),
    ])

    # Cracks via voronoi distance to edge
    voro = tree.nodes.new('ShaderNodeTexVoronoi')
    voro.feature = 'DISTANCE_TO_EDGE'
    voro.inputs['Scale'].default_value = 3.5
    voro.inputs['Randomness'].default_value = 1.0
    tree.links.new(mp.outputs['Vector'], voro.inputs['Vector'])
    crack_ramp = add_ramp(tree, voro.outputs['Distance'], [
        (0.0, (0, 0, 0, 1)),
        (0.04, (1, 1, 1, 1)),
    ])

    # Water stain blotches (large)
    water = add_noise(tree, mp.outputs['Vector'], scale=1.0, detail=6, roughness=0.7)
    water_ramp = add_ramp(tree, water.outputs['Fac'], [
        (0.45, (0.22, 0.20, 0.16, 1)),
        (0.65, (1, 1, 1, 1)),
    ])

    base_w = add_mix(tree, None, conc_ramp.outputs['Color'], water_ramp.outputs['Color'],
                     blend='MULTIPLY', value=0.45)
    final = add_mix(tree, None, base_w.outputs['Color'], crack_ramp.outputs['Color'],
                    blend='MULTIPLY', value=0.9)
    tree.links.new(final.outputs['Color'], bsdf.inputs['Base Color'])

    bsdf.inputs['Roughness'].default_value = 0.95

    bump = tree.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.9
    bump.inputs['Distance'].default_value = 0.06
    # combine concrete + crack height
    bump_mix = add_mix(tree, None, conc.outputs['Color'], crack_ramp.outputs['Color'],
                       blend='MULTIPLY', value=1.0)
    tree.links.new(bump_mix.outputs['Color'], bump.inputs['Height'])
    tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    bake_set(obj, mat, 'Basement', bsdf, bump_node=bump)


# ════════════════════════════════════════════════════════════════════════
# 5. Ceiling: drop-tile grid + mold spots + brown stains
# ════════════════════════════════════════════════════════════════════════
def gen_ceiling():
    print("=== gen_ceiling ===")
    clear_scene()
    setup_cycles()
    obj = make_plane()
    mat, tree, bsdf, out = new_material(obj, 'Ceiling')

    mp = add_tex_coord(tree, scale=(5, 5, 1))

    brick = tree.nodes.new('ShaderNodeTexBrick')
    brick.inputs['Scale'].default_value = 2.5
    brick.inputs['Mortar Size'].default_value = 0.013
    brick.inputs['Mortar Smooth'].default_value = 0.2
    brick.inputs['Color1'].default_value = (0.83, 0.81, 0.76, 1)
    brick.inputs['Color2'].default_value = (0.80, 0.78, 0.73, 1)
    brick.inputs['Mortar'].default_value = (0.55, 0.53, 0.49, 1)
    tree.links.new(mp.outputs['Vector'], brick.inputs['Vector'])

    voro = tree.nodes.new('ShaderNodeTexVoronoi')
    voro.feature = 'F1'
    voro.inputs['Scale'].default_value = 1.0
    voro.inputs['Randomness'].default_value = 0.95
    tree.links.new(mp.outputs['Vector'], voro.inputs['Vector'])
    stain_ramp = add_ramp(tree, voro.outputs['Distance'], [
        (0.40, (0.52, 0.38, 0.22, 1)),
        (0.65, (0.83, 0.81, 0.76, 1)),
    ])

    # Black mold dots
    mold_noise = add_noise(tree, mp.outputs['Vector'], scale=18, detail=8, roughness=0.7)
    mold_ramp = add_ramp(tree, mold_noise.outputs['Fac'], [
        (0.72, (0.06, 0.06, 0.05, 1)),
        (0.82, (0.83, 0.81, 0.76, 1)),
    ])

    a = add_mix(tree, None, brick.outputs['Color'], stain_ramp.outputs['Color'],
                blend='MULTIPLY', value=0.45)
    b = add_mix(tree, None, a.outputs['Color'], mold_ramp.outputs['Color'],
                blend='MULTIPLY', value=0.65)
    tree.links.new(b.outputs['Color'], bsdf.inputs['Base Color'])

    bsdf.inputs['Roughness'].default_value = 0.92

    bump = tree.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.3
    tree.links.new(brick.outputs['Fac'], bump.inputs['Height'])
    tree.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    bake_set(obj, mat, 'Ceiling', bsdf, bump_node=bump)


# ────────────────────────────────────────────────────────────────────────
if __name__ == '__main__':
    print(f"Blender: {bpy.app.version_string}")
    print(f"OUT={OUT}")
    try:
        gen_ward_wall()
        gen_corridor_wall()
        gen_floor()
        gen_basement()
        gen_ceiling()
        print("\n=== ALL HOSPITAL TEXTURES BAKED ===")
    except Exception as e:
        import traceback
        traceback.print_exc()
        sys.exit(1)
