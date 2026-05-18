"""
Hospital furniture / fixtures, exported as FBX for Unity.

Blender 5.x compatible:
  - Emission Color (4.0+) handled via helper
  - Use Sharp shading + auto smooth modifier replaced by smooth-by-angle (default in 5.x)
"""

import bpy
import os
import math
import sys

OUT = os.environ.get("HOSPITAL_MODELS_OUT")
if not OUT:
    OUT = r"C:\Users\hvnes\YomawariByoin\.claude\worktrees\quizzical-elgamal-5af78a\Assets\Models\Hospital"
os.makedirs(OUT, exist_ok=True)


def clear_scene():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for m in list(bpy.data.materials):
        bpy.data.materials.remove(m)
    for me in list(bpy.data.meshes):
        if me.users == 0:
            bpy.data.meshes.remove(me)


def set_emission(bsdf, color, strength=1.0):
    if 'Emission Color' in bsdf.inputs:
        bsdf.inputs['Emission Color'].default_value = color
    elif 'Emission' in bsdf.inputs:
        bsdf.inputs['Emission'].default_value = color
    if 'Emission Strength' in bsdf.inputs:
        bsdf.inputs['Emission Strength'].default_value = strength


def set_alpha_blend(mat):
    try:
        mat.surface_render_method = 'BLENDED'
    except (AttributeError, TypeError):
        try:
            mat.blend_method = 'BLEND'
        except Exception:
            pass


def make_pbr_mat(name, color, roughness=0.6, metallic=0.0, alpha=1.0,
                 emission_color=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    bsdf = tree.nodes.get('Principled BSDF')
    if bsdf is None:
        # fall back: clear + rebuild
        for n in list(tree.nodes):
            tree.nodes.remove(n)
        out = tree.nodes.new('ShaderNodeOutputMaterial')
        bsdf = tree.nodes.new('ShaderNodeBsdfPrincipled')
        tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    bsdf.inputs['Base Color'].default_value = color
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if 'Alpha' in bsdf.inputs:
        bsdf.inputs['Alpha'].default_value = alpha
    if alpha < 1.0:
        set_alpha_blend(mat)
    if emission_color is not None:
        set_emission(bsdf, emission_color, emission_strength)
    return mat


def assign_mat(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def smooth_shade(obj):
    """Smooth shading; in 5.x angle-based smoothing is done via the Smooth Shading flag + 'Shade Auto Smooth'."""
    for poly in obj.data.polygons:
        poly.use_smooth = True
    try:
        # Modern op (5.x has it as Shade Smooth by Angle)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(30))
    except Exception:
        try:
            bpy.ops.object.shade_smooth()
        except Exception:
            pass


def export_selected_fbx(filepath):
    bpy.ops.object.select_all(action='SELECT')
    # Apply transforms so Unity sees correct sizes/rotations
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=True,
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_NONE',
        bake_space_transform=False,
        object_types={'MESH'},
        mesh_smooth_type='FACE',
        use_mesh_modifiers=True,
        path_mode='COPY',
        embed_textures=False,
    )
    print(f"exported {filepath}")


# ════════════════════════════════════════════════════════════════════════
# 1. Hospital bed (refined): frame, mattress, side rails, casters, IV hook
# ════════════════════════════════════════════════════════════════════════
def make_hospital_bed():
    print("=== make_hospital_bed ===")
    clear_scene()

    mat_metal = make_pbr_mat('BedFrame_Metal', (0.62, 0.59, 0.54, 1), roughness=0.45, metallic=0.85)
    mat_mattress = make_pbr_mat('Mattress_Cloth', (0.78, 0.76, 0.68, 1), roughness=0.95)
    mat_pillow = make_pbr_mat('Pillow_Cloth', (0.85, 0.83, 0.74, 1), roughness=0.92)
    mat_caster = make_pbr_mat('Caster_Rubber', (0.10, 0.10, 0.10, 1), roughness=0.92)
    mat_card = make_pbr_mat('Bed_Card', (0.90, 0.88, 0.80, 1), roughness=0.85)

    # Frame base (bottom rectangle)
    bpy.ops.mesh.primitive_cube_add(size=1)
    base = bpy.context.active_object
    base.name = 'Bed_Frame_Base'
    base.scale = (0.95, 2.05, 0.06)
    base.location = (0, 0, 0.32)
    smooth_shade(base)
    assign_mat(base, mat_metal)

    # Mattress
    bpy.ops.mesh.primitive_cube_add(size=1)
    matt = bpy.context.active_object
    matt.name = 'Bed_Mattress'
    matt.scale = (0.88, 1.92, 0.13)
    matt.location = (0, 0, 0.435)
    assign_mat(matt, mat_mattress)

    # Pillow
    bpy.ops.mesh.primitive_cube_add(size=1)
    pillow = bpy.context.active_object
    pillow.name = 'Bed_Pillow'
    pillow.scale = (0.60, 0.30, 0.05)
    pillow.location = (0, -0.78, 0.52)
    smooth_shade(pillow)
    assign_mat(pillow, mat_pillow)

    # Headboard (raised railing-style)
    bpy.ops.mesh.primitive_cube_add(size=1)
    head = bpy.context.active_object
    head.name = 'Bed_Headboard'
    head.scale = (0.95, 0.05, 0.55)
    head.location = (0, -1.05, 0.58)
    assign_mat(head, mat_metal)

    # Headboard vertical bars
    for x in (-0.32, -0.1, 0.1, 0.32):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.014, depth=0.42, vertices=12)
        bar = bpy.context.active_object
        bar.location = (x, -1.07, 0.65)
        smooth_shade(bar)
        assign_mat(bar, mat_metal)

    # Footboard (lower)
    bpy.ops.mesh.primitive_cube_add(size=1)
    foot = bpy.context.active_object
    foot.name = 'Bed_Footboard'
    foot.scale = (0.95, 0.04, 0.36)
    foot.location = (0, 1.05, 0.50)
    assign_mat(foot, mat_metal)

    # Patient card on footboard
    bpy.ops.mesh.primitive_cube_add(size=1)
    card = bpy.context.active_object
    card.name = 'Bed_Card'
    card.scale = (0.18, 0.005, 0.10)
    card.location = (0, 1.075, 0.50)
    assign_mat(card, mat_card)

    # Side rails (folded up): two long bars on each side
    for x_side in (-0.50, 0.50):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.018, depth=1.5, vertices=12)
        rail = bpy.context.active_object
        rail.rotation_euler = (math.pi / 2, 0, 0)
        rail.location = (x_side, 0, 0.55)
        smooth_shade(rail)
        assign_mat(rail, mat_metal)
        # Vertical bars on the rail
        for y_bar in (-0.5, -0.15, 0.15, 0.5):
            bpy.ops.mesh.primitive_cylinder_add(radius=0.010, depth=0.18, vertices=10)
            vb = bpy.context.active_object
            vb.location = (x_side, y_bar, 0.46)
            smooth_shade(vb)
            assign_mat(vb, mat_metal)

    # Legs
    for x, y in [(-0.42, -0.95), (0.42, -0.95), (-0.42, 0.95), (0.42, 0.95)]:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.025, depth=0.28, vertices=12)
        leg = bpy.context.active_object
        leg.location = (x, y, 0.16)
        smooth_shade(leg)
        assign_mat(leg, mat_metal)

    # Casters
    for x, y in [(-0.42, -0.95), (0.42, -0.95), (-0.42, 0.95), (0.42, 0.95)]:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.045, depth=0.04, vertices=16)
        cas = bpy.context.active_object
        cas.rotation_euler = (math.pi / 2, 0, 0)
        cas.location = (x, y, 0.025)
        smooth_shade(cas)
        assign_mat(cas, mat_caster)

    export_selected_fbx(os.path.join(OUT, 'HospitalBed.fbx'))


# ════════════════════════════════════════════════════════════════════════
# 2. IV stand: pole, fivebase, hooks, glass-ish bag
# ════════════════════════════════════════════════════════════════════════
def make_iv_stand():
    print("=== make_iv_stand ===")
    clear_scene()

    mat_metal = make_pbr_mat('IV_Metal', (0.70, 0.68, 0.64, 1), roughness=0.30, metallic=0.95)
    mat_bag = make_pbr_mat('IV_Bag', (0.82, 0.88, 0.84, 1), roughness=0.15, alpha=0.45)
    mat_tube = make_pbr_mat('IV_Tube', (0.85, 0.85, 0.85, 1), roughness=0.20, alpha=0.6)

    # Main pole
    bpy.ops.mesh.primitive_cylinder_add(radius=0.022, depth=1.7, vertices=16)
    pole = bpy.context.active_object
    pole.location = (0, 0, 0.92)
    smooth_shade(pole)
    assign_mat(pole, mat_metal)

    # Top cross bar
    bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.38, vertices=12)
    cross = bpy.context.active_object
    cross.rotation_euler = (0, math.pi / 2, 0)
    cross.location = (0, 0, 1.70)
    smooth_shade(cross)
    assign_mat(cross, mat_metal)

    # Two hooks (small toruses)
    for ox in (-0.14, 0.14):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.035, minor_radius=0.006,
                                          major_segments=16, minor_segments=8)
        hk = bpy.context.active_object
        hk.location = (ox, 0, 1.74)
        hk.scale = (1, 0.6, 0.6)
        smooth_shade(hk)
        assign_mat(hk, mat_metal)

    # IV bag (slightly translucent)
    bpy.ops.mesh.primitive_cube_add(size=1)
    bag = bpy.context.active_object
    bag.scale = (0.11, 0.035, 0.21)
    bag.location = (0.14, 0, 1.52)
    smooth_shade(bag)
    assign_mat(bag, mat_bag)

    # Tube down to invisible patient
    bpy.ops.curve.primitive_bezier_curve_add()
    tube_curve = bpy.context.active_object
    tube_curve.data.bevel_depth = 0.005
    tube_curve.data.bevel_resolution = 4
    pts = tube_curve.data.splines[0].bezier_points
    pts[0].co = (0.14, 0, 1.40)
    pts[0].handle_left = (0.14, 0, 1.5)
    pts[0].handle_right = (0.14, 0, 1.2)
    pts[1].co = (0.50, 0.10, 0.55)
    pts[1].handle_left = (0.30, 0.0, 0.8)
    pts[1].handle_right = (0.60, 0.20, 0.4)
    bpy.ops.object.convert(target='MESH')
    smooth_shade(bpy.context.active_object)
    assign_mat(bpy.context.active_object, mat_tube)

    # 5-leg base
    for i in range(5):
        ang = i * (2 * math.pi / 5)
        # Leg arm
        bpy.ops.mesh.primitive_cylinder_add(radius=0.014, depth=0.36, vertices=10)
        leg = bpy.context.active_object
        leg.rotation_euler = (0, math.pi / 2, ang)
        leg.location = (math.cos(ang) * 0.20, math.sin(ang) * 0.20, 0.04)
        smooth_shade(leg)
        assign_mat(leg, mat_metal)
        # Caster wheel
        bpy.ops.mesh.primitive_cylinder_add(radius=0.028, depth=0.022, vertices=14)
        cw = bpy.context.active_object
        cw.location = (math.cos(ang) * 0.38, math.sin(ang) * 0.38, 0.022)
        cw.rotation_euler = (math.pi / 2, 0, ang)
        smooth_shade(cw)
        assign_mat(cw, mat_metal)

    export_selected_fbx(os.path.join(OUT, 'IVStand.fbx'))


# ════════════════════════════════════════════════════════════════════════
# 3. Fluorescent fixture (ceiling-mounted, with tubes)
# ════════════════════════════════════════════════════════════════════════
def make_fluorescent_light():
    print("=== make_fluorescent_light ===")
    clear_scene()

    mat_housing = make_pbr_mat('Light_Housing', (0.85, 0.85, 0.82, 1), roughness=0.55, metallic=0.4)
    mat_cover = make_pbr_mat('Light_Cover', (0.95, 0.95, 0.92, 1), roughness=0.25,
                              emission_color=(0.95, 0.95, 0.88, 1), emission_strength=1.8)
    mat_tube = make_pbr_mat('Light_Tube', (0.98, 0.98, 1.0, 1), roughness=0.10,
                             emission_color=(0.92, 0.95, 1.0, 1), emission_strength=5.0)

    # Housing
    bpy.ops.mesh.primitive_cube_add(size=1)
    housing = bpy.context.active_object
    housing.name = 'Light_Housing'
    housing.scale = (1.22, 0.16, 0.085)
    housing.location = (0, 0, 0.04)
    assign_mat(housing, mat_housing)

    # Diffuser cover (slightly inset)
    bpy.ops.mesh.primitive_cube_add(size=1)
    cover = bpy.context.active_object
    cover.name = 'Light_Cover'
    cover.scale = (1.18, 0.13, 0.025)
    cover.location = (0, 0, -0.04)
    assign_mat(cover, mat_cover)

    # 2 tubes
    for off in (-0.04, 0.04):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.014, depth=2.30, vertices=14)
        tube = bpy.context.active_object
        tube.rotation_euler = (0, math.pi / 2, 0)
        tube.location = (0, off, -0.005)
        smooth_shade(tube)
        assign_mat(tube, mat_tube)

    # End caps
    for x in (-1.20, 1.20):
        bpy.ops.mesh.primitive_cube_add(size=1)
        cap = bpy.context.active_object
        cap.scale = (0.04, 0.13, 0.07)
        cap.location = (x, 0, -0.02)
        assign_mat(cap, mat_housing)

    export_selected_fbx(os.path.join(OUT, 'FluorescentLight.fbx'))


# ════════════════════════════════════════════════════════════════════════
# 4. Hospital door: wooden, with frosted window and metal handle
# ════════════════════════════════════════════════════════════════════════
def make_door():
    print("=== make_door ===")
    clear_scene()

    mat_wood = make_pbr_mat('Door_Wood', (0.36, 0.24, 0.15, 1), roughness=0.78)
    mat_panel = make_pbr_mat('Door_Panel', (0.30, 0.20, 0.12, 1), roughness=0.78)
    mat_frame = make_pbr_mat('Door_Frame', (0.62, 0.59, 0.55, 1), roughness=0.45, metallic=0.6)
    mat_glass = make_pbr_mat('Door_Glass', (0.82, 0.84, 0.82, 1), roughness=0.05, alpha=0.32)
    mat_knob = make_pbr_mat('Door_Knob', (0.78, 0.72, 0.55, 1), roughness=0.25, metallic=0.95)

    # Door slab
    bpy.ops.mesh.primitive_cube_add(size=1)
    door = bpy.context.active_object
    door.name = 'Door_Slab'
    door.scale = (0.90, 0.05, 2.05)
    door.location = (0, 0, 1.025)
    assign_mat(door, mat_wood)

    # Lower panels (2)
    for z in (0.55, 0.95):
        bpy.ops.mesh.primitive_cube_add(size=1)
        p = bpy.context.active_object
        p.scale = (0.72, 0.025, 0.32)
        p.location = (0, 0.035, z)
        assign_mat(p, mat_panel)

    # Upper frosted-glass window
    bpy.ops.mesh.primitive_cube_add(size=1)
    w = bpy.context.active_object
    w.scale = (0.55, 0.012, 0.38)
    w.location = (0, 0, 1.55)
    assign_mat(w, mat_glass)
    # window frame
    bpy.ops.mesh.primitive_cube_add(size=1)
    wf = bpy.context.active_object
    wf.scale = (0.6, 0.04, 0.42)
    wf.location = (0, 0, 1.55)
    assign_mat(wf, mat_panel)

    # Frame around the slab
    bpy.ops.mesh.primitive_cube_add(size=1)
    fr = bpy.context.active_object
    fr.scale = (1.0, 0.10, 2.13)
    fr.location = (0, -0.025, 1.065)
    assign_mat(fr, mat_frame)

    # Knob
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.045, segments=18, ring_count=10)
    k = bpy.context.active_object
    k.scale = (1.0, 0.6, 0.8)
    k.location = (0.37, 0.045, 1.05)
    smooth_shade(k)
    assign_mat(k, mat_knob)
    # Knob backing plate
    bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=0.012, vertices=20)
    kb = bpy.context.active_object
    kb.rotation_euler = (math.pi / 2, 0, 0)
    kb.location = (0.37, 0.03, 1.05)
    smooth_shade(kb)
    assign_mat(kb, mat_knob)

    export_selected_fbx(os.path.join(OUT, 'HospitalDoor.fbx'))


# ════════════════════════════════════════════════════════════════════════
# 5. Wheelchair: seat, large+small wheels, armrests, footrest, push handles
# ════════════════════════════════════════════════════════════════════════
def make_wheelchair():
    print("=== make_wheelchair ===")
    clear_scene()

    mat_frame = make_pbr_mat('WC_Frame', (0.65, 0.62, 0.58, 1), roughness=0.45, metallic=0.85)
    mat_seat = make_pbr_mat('WC_Seat', (0.20, 0.18, 0.18, 1), roughness=0.85)
    mat_tire = make_pbr_mat('WC_Tire', (0.07, 0.07, 0.07, 1), roughness=0.92)
    mat_rim = make_pbr_mat('WC_Rim', (0.72, 0.70, 0.66, 1), roughness=0.35, metallic=0.9)

    # Seat cushion
    bpy.ops.mesh.primitive_cube_add(size=1)
    seat = bpy.context.active_object
    seat.scale = (0.45, 0.42, 0.06)
    seat.location = (0, 0, 0.50)
    assign_mat(seat, mat_seat)

    # Back rest cushion
    bpy.ops.mesh.primitive_cube_add(size=1)
    back = bpy.context.active_object
    back.scale = (0.43, 0.045, 0.42)
    back.location = (0, -0.20, 0.74)
    assign_mat(back, mat_seat)

    # Backrest frame (top horizontal + verticals)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.018, depth=0.46, vertices=12)
    bt = bpy.context.active_object
    bt.rotation_euler = (0, math.pi / 2, 0)
    bt.location = (0, -0.22, 0.97)
    smooth_shade(bt)
    assign_mat(bt, mat_frame)
    for x in (-0.23, 0.23):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.016, depth=0.55, vertices=12)
        v = bpy.context.active_object
        v.location = (x, -0.22, 0.72)
        smooth_shade(v)
        assign_mat(v, mat_frame)
    # Push handles
    for x in (-0.23, 0.23):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.10, vertices=10)
        ph = bpy.context.active_object
        ph.rotation_euler = (math.pi / 4, 0, 0)
        ph.location = (x, -0.27, 1.02)
        smooth_shade(ph)
        assign_mat(ph, mat_seat)

    # Large back wheels (tires + spokes simplified to a thin disc)
    for x in (-0.42, 0.42):
        # Tire
        bpy.ops.mesh.primitive_torus_add(major_radius=0.30, minor_radius=0.032,
                                          major_segments=28, minor_segments=10)
        tire = bpy.context.active_object
        tire.rotation_euler = (math.pi / 2, 0, 0)
        tire.location = (x, 0.0, 0.32)
        smooth_shade(tire)
        assign_mat(tire, mat_tire)
        # Push rim slightly outside
        bpy.ops.mesh.primitive_torus_add(major_radius=0.28, minor_radius=0.012,
                                          major_segments=28, minor_segments=8)
        rim = bpy.context.active_object
        rim.rotation_euler = (math.pi / 2, 0, 0)
        rim.location = (x + (0.045 if x > 0 else -0.045), 0.0, 0.32)
        smooth_shade(rim)
        assign_mat(rim, mat_rim)
        # Hub
        bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=0.04, vertices=16)
        hub = bpy.context.active_object
        hub.rotation_euler = (math.pi / 2, 0, 0)
        hub.location = (x, 0.0, 0.32)
        smooth_shade(hub)
        assign_mat(hub, mat_rim)

    # Small front wheels
    for x in (-0.34, 0.34):
        bpy.ops.mesh.primitive_torus_add(major_radius=0.08, minor_radius=0.020,
                                          major_segments=18, minor_segments=8)
        sw = bpy.context.active_object
        sw.rotation_euler = (math.pi / 2, 0, 0)
        sw.location = (x, 0.36, 0.10)
        smooth_shade(sw)
        assign_mat(sw, mat_tire)

    # Arm rests
    for x in (-0.26, 0.26):
        bpy.ops.mesh.primitive_cube_add(size=1)
        ar = bpy.context.active_object
        ar.scale = (0.03, 0.40, 0.025)
        ar.location = (x, 0.0, 0.63)
        assign_mat(ar, mat_frame)

    # Foot rest
    bpy.ops.mesh.primitive_cube_add(size=1)
    fr = bpy.context.active_object
    fr.scale = (0.42, 0.12, 0.022)
    fr.location = (0, 0.42, 0.13)
    assign_mat(fr, mat_frame)

    export_selected_fbx(os.path.join(OUT, 'Wheelchair.fbx'))


# ════════════════════════════════════════════════════════════════════════
# 6. Filing cabinet (basement)
# ════════════════════════════════════════════════════════════════════════
def make_filing_cabinet():
    print("=== make_filing_cabinet ===")
    clear_scene()

    mat_body = make_pbr_mat('Cabinet_Body', (0.42, 0.40, 0.36, 1), roughness=0.55, metallic=0.6)
    mat_dark = make_pbr_mat('Cabinet_Inset', (0.30, 0.28, 0.24, 1), roughness=0.7, metallic=0.4)
    mat_handle = make_pbr_mat('Cabinet_Handle', (0.62, 0.59, 0.54, 1), roughness=0.35, metallic=0.9)
    mat_label = make_pbr_mat('Cabinet_Label', (0.88, 0.86, 0.78, 1), roughness=0.85)

    # Body
    bpy.ops.mesh.primitive_cube_add(size=1)
    body = bpy.context.active_object
    body.scale = (0.50, 0.62, 1.45)
    body.location = (0, 0, 0.725)
    assign_mat(body, mat_body)

    # 4 drawer faces
    for i in range(4):
        z = 0.18 + i * 0.34
        bpy.ops.mesh.primitive_cube_add(size=1)
        d = bpy.context.active_object
        d.scale = (0.47, 0.005, 0.30)
        d.location = (0, 0.31, z)
        assign_mat(d, mat_dark)
        # Handle
        bpy.ops.mesh.primitive_cube_add(size=1)
        h = bpy.context.active_object
        h.scale = (0.12, 0.025, 0.02)
        h.location = (0, 0.325, z + 0.05)
        assign_mat(h, mat_handle)
        # Label slot
        bpy.ops.mesh.primitive_cube_add(size=1)
        lab = bpy.context.active_object
        lab.scale = (0.10, 0.004, 0.025)
        lab.location = (-0.18, 0.319, z + 0.08)
        assign_mat(lab, mat_label)

    # Top plate
    bpy.ops.mesh.primitive_cube_add(size=1)
    top = bpy.context.active_object
    top.scale = (0.52, 0.64, 0.02)
    top.location = (0, 0, 1.46)
    assign_mat(top, mat_body)

    export_selected_fbx(os.path.join(OUT, 'FilingCabinet.fbx'))


# ════════════════════════════════════════════════════════════════════════
# 7. Medical cart (basement / corridor)
# ════════════════════════════════════════════════════════════════════════
def make_medical_cart():
    print("=== make_medical_cart ===")
    clear_scene()

    mat_metal = make_pbr_mat('Cart_Metal', (0.68, 0.66, 0.62, 1), roughness=0.40, metallic=0.85)
    mat_top = make_pbr_mat('Cart_Top', (0.55, 0.53, 0.48, 1), roughness=0.55, metallic=0.5)
    mat_drawer = make_pbr_mat('Cart_Drawer', (0.50, 0.48, 0.44, 1), roughness=0.50, metallic=0.6)
    mat_caster = make_pbr_mat('Cart_Caster', (0.12, 0.12, 0.12, 1), roughness=0.9)

    # Body
    bpy.ops.mesh.primitive_cube_add(size=1)
    body = bpy.context.active_object
    body.scale = (0.48, 0.36, 0.50)
    body.location = (0, 0, 0.55)
    assign_mat(body, mat_metal)

    # Top tray (raised lip)
    bpy.ops.mesh.primitive_cube_add(size=1)
    top = bpy.context.active_object
    top.scale = (0.52, 0.40, 0.02)
    top.location = (0, 0, 0.82)
    assign_mat(top, mat_top)

    # 3 drawers on front
    for i in range(3):
        z = 0.40 + i * 0.13
        bpy.ops.mesh.primitive_cube_add(size=1)
        d = bpy.context.active_object
        d.scale = (0.46, 0.005, 0.11)
        d.location = (0, 0.18, z)
        assign_mat(d, mat_drawer)
        # Handle
        bpy.ops.mesh.primitive_cube_add(size=1)
        h = bpy.context.active_object
        h.scale = (0.18, 0.018, 0.015)
        h.location = (0, 0.19, z + 0.02)
        assign_mat(h, mat_metal)

    # Push handle (curved bar) - simplified
    bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.36, vertices=12)
    ph = bpy.context.active_object
    ph.rotation_euler = (0, math.pi / 2, 0)
    ph.location = (0, -0.22, 0.86)
    smooth_shade(ph)
    assign_mat(ph, mat_metal)
    for ox in (-0.18, 0.18):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.012, depth=0.06, vertices=10)
        v = bpy.context.active_object
        v.location = (ox, -0.20, 0.83)
        smooth_shade(v)
        assign_mat(v, mat_metal)

    # 4 casters
    for x, y in [(-0.22, -0.16), (0.22, -0.16), (-0.22, 0.16), (0.22, 0.16)]:
        bpy.ops.mesh.primitive_cylinder_add(radius=0.04, depth=0.04, vertices=14)
        c = bpy.context.active_object
        c.rotation_euler = (math.pi / 2, 0, 0)
        c.location = (x, y, 0.04)
        smooth_shade(c)
        assign_mat(c, mat_caster)

    export_selected_fbx(os.path.join(OUT, 'MedicalCart.fbx'))


# ────────────────────────────────────────────────────────────────────────
if __name__ == '__main__':
    print(f"Blender: {bpy.app.version_string}")
    print(f"OUT={OUT}")
    try:
        make_hospital_bed()
        make_iv_stand()
        make_fluorescent_light()
        make_door()
        make_wheelchair()
        make_filing_cabinet()
        make_medical_cart()
        print("\n=== ALL HOSPITAL MODELS EXPORTED ===")
    except Exception as e:
        import traceback
        traceback.print_exc()
        sys.exit(1)
