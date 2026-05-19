import bpy, math, os, random

random.seed(42)
OUT = r"C:\Users\hvnes\YomawariByoin\Assets\Models\Props"
os.makedirs(OUT, exist_ok=True)

def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for d in list(bpy.data.meshes):
        bpy.data.meshes.remove(d, do_unlink=True)
    for d in list(bpy.data.materials):
        bpy.data.materials.remove(d, do_unlink=True)
    for d in list(bpy.data.objects):
        try:
            bpy.data.objects.remove(d, do_unlink=True)
        except:
            pass

def export(name):
    bpy.ops.object.select_all(action='SELECT')
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True
    )
    print(f"Exported: {name}.fbx")

def mat(name, r, g, b, metallic=0.0, roughness=0.8, emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    n = m.node_tree.nodes['Principled BSDF']
    n.inputs['Base Color'].default_value = (r/255, g/255, b/255, 1)
    n.inputs['Metallic'].default_value = metallic
    n.inputs['Roughness'].default_value = roughness
    if emission:
        try:
            n.inputs['Emission Color'].default_value = (emission[0]/255, emission[1]/255, emission[2]/255, 1)
        except:
            n.inputs['Emission'].default_value = (emission[0]/255, emission[1]/255, emission[2]/255, 1)
        n.inputs['Emission Strength'].default_value = 2.0
    return m

def add_mat(obj, m):
    if obj.data.materials:
        obj.data.materials[0] = m
    else:
        obj.data.materials.append(m)

def cube(name, x, y, z, sx, sy, sz, m=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    o = bpy.context.active_object
    o.name = name
    o.scale = (sx, sy, sz)
    if m:
        add_mat(o, m)
    return o

def cyl(name, x, y, z, r, d, m=None, rx=0, ry=0, rz=0):
    bpy.ops.mesh.primitive_cylinder_add(radius=r, depth=d, location=(x, y, z))
    o = bpy.context.active_object
    o.name = name
    o.rotation_euler = (rx, ry, rz)
    if m:
        add_mat(o, m)
    return o

# 1. STRETCHER
clear()
metal = mat("StretcherMetal", 120, 115, 108, metallic=0.7, roughness=0.7)
fabric = mat("StretcherFabric", 185, 175, 160, roughness=0.9)
rust = mat("StretcherRust", 110, 55, 20, metallic=0.4, roughness=0.95)
cube("S_Top", 0, 0, 0.5, 0.65, 2.0, 0.08, fabric)
for x in [-0.35, 0.35]:
    cube(f"S_Frame_{x}", x, 0, 0.45, 0.03, 2.0, 0.04, metal)
for xi, yi in [(-0.3, -0.85), (0.3, -0.85), (-0.3, 0.85), (0.3, 0.85)]:
    cube(f"S_Leg{xi}{yi}", xi, yi, 0.22, 0.04, 0.04, 0.45, metal)
    cyl(f"S_Caster{xi}{yi}", xi, yi, 0.04, 0.04, 0.06, rust)
cube("S_Head", 0, -1.05, 0.5, 0.65, 0.04, 0.5, metal)
cube("S_Foot", 0, 1.05, 0.5, 0.65, 0.04, 0.3, metal)
export("prop_stretcher")

# 2. FIRE EXTINGUISHER
clear()
red = mat("ExtRed", 178, 34, 34, roughness=0.6)
black = mat("ExtBlack", 20, 20, 20, roughness=0.8)
silver = mat("ExtSilver", 180, 178, 175, metallic=0.8, roughness=0.4)
cyl("Ext_Body", 0, 0, 0.28, 0.09, 0.55, red)
cyl("Ext_Top", 0, 0, 0.6, 0.05, 0.1, silver)
cyl("Ext_Nozzle", 0, 0.07, 0.63, 0.015, 0.2, black)
cyl("Ext_Handle", 0, 0, 0.66, 0.008, 0.18, silver)
cube("Ext_Bracket_V", 0, -0.12, 0.28, 0.03, 0.03, 0.55, silver)
cube("Ext_Bracket_H1", 0, -0.15, 0.48, 0.2, 0.03, 0.03, silver)
cube("Ext_Bracket_H2", 0, -0.15, 0.12, 0.2, 0.03, 0.03, silver)
export("prop_fire_extinguisher")

# 3. NOTICEBOARD
clear()
wood = mat("BoardWood", 90, 70, 50, roughness=0.9)
cork = mat("BoardCork", 175, 140, 95, roughness=0.95)
frame = mat("BoardFrame", 60, 45, 30, roughness=0.92)
cube("BB_Frame", 0, 0, 0, 0.82, 0.03, 0.62, frame)
cube("BB_Frame_T", 0, 0, 0.32, 0.82, 0.03, 0.03, frame)
cube("BB_Frame_B", 0, 0, -0.32, 0.82, 0.03, 0.03, frame)
cube("BB_Frame_L", -0.41, 0, 0, 0.03, 0.03, 0.62, frame)
cube("BB_Frame_R", 0.41, 0, 0, 0.03, 0.03, 0.62, frame)
cube("BB_Cork", 0, 0.01, 0, 0.76, 0.01, 0.56, cork)
export("prop_noticeboard")

# 4a. FLUORESCENT NORMAL
clear()
fl_metal = mat("FL_Metal", 160, 158, 152, metallic=0.6, roughness=0.5)
fl_cover = mat("FL_Cover", 230, 228, 220, roughness=0.3, emission=(220, 220, 240))
cube("FL_Frame", 0, 0, 0, 0.6, 0.12, 0.04, fl_metal)
cube("FL_Cover", 0, 0, -0.025, 0.58, 0.1, 0.015, fl_cover)
for xi in [-0.25, 0.25]:
    cube(f"FL_Mount{xi}", xi, 0, 0.03, 0.04, 0.04, 0.03, fl_metal)
export("prop_fluorescent_normal")

# 4b. FLUORESCENT BROKEN
clear()
fl_metal = mat("FL_Metal", 160, 158, 152, metallic=0.6, roughness=0.5)
broken_glass = mat("BrokenGlass", 200, 200, 200, roughness=0.05, emission=(180, 180, 200))
cube("FL_Frame", 0, 0, 0, 0.6, 0.12, 0.04, fl_metal)
o1 = cube("FL_Cover_L", -0.16, 0, -0.025, 0.26, 0.09, 0.012, broken_glass)
o1.rotation_euler = (0, 0, math.radians(5))
o2 = cube("FL_Cover_R", 0.18, 0, -0.03, 0.22, 0.09, 0.012, broken_glass)
o2.rotation_euler = (0, 0, math.radians(-8))
export("prop_fluorescent_broken")

# 4c. FLUORESCENT LOOSE
clear()
fl_metal = mat("FL_Metal", 160, 158, 152, metallic=0.6, roughness=0.5)
fl_cover = mat("FL_Cover", 230, 228, 220, roughness=0.3, emission=(220, 220, 240))
cube("FL_Frame", 0, 0, 0, 0.6, 0.12, 0.04, fl_metal)
hanging = cube("FL_Cover_Hang", 0.1, 0, -0.045, 0.4, 0.1, 0.015, fl_cover)
hanging.rotation_euler = (0, math.radians(-20), 0)
cyl("FL_Wire", 0.3, 0, -0.02, 0.003, 0.05, fl_metal, rx=math.radians(15))
export("prop_fluorescent_loose")

# 5. WALL CLOCK
clear()
clock_frame = mat("ClockFrame", 80, 78, 72, metallic=0.5, roughness=0.7)
clock_face = mat("ClockFace", 235, 232, 220, roughness=0.85)
clock_glass = mat("ClockGlass", 200, 205, 210, roughness=0.05)
clock_hand = mat("ClockHand", 20, 20, 20, roughness=0.9)
cyl("Clock_Frame", 0, 0, 0, 0.18, 0.025, clock_frame)
cyl("Clock_Face", 0, 0.008, 0, 0.16, 0.01, clock_face)
cyl("Clock_Glass", 0, 0.016, 0, 0.16, 0.003, clock_glass)
cube("Clock_Hour", 0, 0.02, 0.07, 0.012, 0.008, 0.09, clock_hand)
cube("Clock_Min", 0.005, 0.022, 0.08, 0.008, 0.006, 0.12, clock_hand)
cyl("Clock_Back_Mount", 0, -0.015, 0, 0.015, 0.01, clock_frame)
export("prop_wall_clock")

# 6. TRASH CAN
clear()
trash_metal = mat("TrashMetal", 100, 98, 92, metallic=0.6, roughness=0.75)
trash_dark = mat("TrashDark", 55, 52, 48, metallic=0.5, roughness=0.8)
bpy.ops.mesh.primitive_cylinder_add(radius=0.18, depth=0.45, location=(0, 0, 0.22))
body = bpy.context.active_object
body.name = "Trash_Body"
add_mat(body, trash_metal)
bpy.ops.mesh.primitive_cylinder_add(radius=0.185, depth=0.04, location=(0, 0, 0.47))
lid = bpy.context.active_object
lid.name = "Trash_Lid"
add_mat(lid, trash_dark)
cube("Trash_Pedal", 0, 0.2, 0.03, 0.12, 0.04, 0.015, trash_dark)
cube("Trash_PedalArm", 0, 0.1, 0.05, 0.015, 0.12, 0.015, trash_metal)
export("prop_trash_can")

# 7. WHEELCHAIR
clear()
wc_metal = mat("WC_Metal", 110, 108, 100, metallic=0.6, roughness=0.75)
wc_tire = mat("WC_Tire", 15, 15, 15, roughness=0.95)
wc_seat = mat("WC_Seat", 130, 125, 115, roughness=0.85)
cube("WC_Seat", 0, 0, 0.47, 0.46, 0.42, 0.04, wc_seat)
cube("WC_Back", 0, -0.22, 0.72, 0.44, 0.03, 0.5, wc_seat)
for xi in [-0.25, 0.25]:
    bpy.ops.mesh.primitive_torus_add(major_radius=0.28, minor_radius=0.03,
                                     major_segments=32, minor_segments=10,
                                     location=(xi, 0, 0.29))
    wh = bpy.context.active_object
    wh.rotation_euler = (math.pi/2, 0, 0)
    add_mat(wh, wc_tire)
    for ang in range(0, 360, 45):
        r = math.radians(ang)
        bpy.ops.mesh.primitive_cylinder_add(radius=0.008, depth=0.25,
            location=(xi, math.sin(r)*0.12, 0.29 + math.cos(r)*0.12))
        sp = bpy.context.active_object
        sp.rotation_euler = (math.pi/2, 0, r)
        add_mat(sp, wc_metal)
for xi in [-0.2, 0.2]:
    bpy.ops.mesh.primitive_torus_add(major_radius=0.07, minor_radius=0.018,
                                     major_segments=16, minor_segments=8,
                                     location=(xi, 0.3, 0.08))
    fw = bpy.context.active_object
    fw.rotation_euler = (math.pi/2, 0, 0)
    add_mat(fw, wc_tire)
for xi in [-0.24, 0.24]:
    cube(f"WC_Arm{xi}", xi, 0, 0.62, 0.025, 0.42, 0.02, wc_metal)
cube("WC_Foot_L", -0.14, 0.38, 0.18, 0.12, 0.02, 0.08, wc_metal)
cube("WC_Foot_R", 0.14, 0.38, 0.18, 0.12, 0.02, 0.08, wc_metal)
cube("WC_Foot_Bar", 0, 0.38, 0.25, 0.3, 0.015, 0.015, wc_metal)
export("prop_wheelchair")

# 8. IV STAND
clear()
iv_metal = mat("IV_Metal", 140, 138, 132, metallic=0.7, roughness=0.6)
iv_rust = mat("IV_Rust", 100, 50, 20, metallic=0.3, roughness=0.9)
iv_bag = mat("IV_Bag", 210, 230, 215, roughness=0.15)
cyl("IV_Pole", 0, 0, 0.85, 0.022, 1.7, iv_metal)
cyl("IV_Ring", 0, 0, 0.85, 0.03, 0.04, iv_rust)
cyl("IV_Bar", 0, 0, 1.65, 0.01, 0.4, iv_metal, rx=0, ry=math.pi/2, rz=0)
for ox in [-0.16, 0.16]:
    bpy.ops.mesh.primitive_torus_add(major_radius=0.035, minor_radius=0.007,
        major_segments=12, minor_segments=6, location=(ox, 0, 1.72))
    h = bpy.context.active_object
    h.scale = (1, 0.5, 0.7)
    add_mat(h, iv_metal)
cube("IV_Bag1", 0.08, 0, 1.52, 0.1, 0.04, 0.18, iv_bag)
cube("IV_Bag2", -0.08, 0, 1.50, 0.1, 0.04, 0.16, iv_bag)
cyl("IV_Tube", 0.08, 0, 1.2, 0.004, 0.65, iv_metal)
for i in range(5):
    ang = i * 2 * math.pi / 5
    cyl(f"IV_Leg{i}", math.cos(ang)*0.28, math.sin(ang)*0.28, 0.025,
        0.016, 0.38, iv_rust, rx=0, ry=math.pi/2, rz=ang)
    cyl(f"IV_Caster{i}", math.cos(ang)*0.42, math.sin(ang)*0.42, 0.018,
        0.028, 0.028, iv_rust)
export("prop_iv_stand_v2")

# 9. SIDE TABLE
clear()
tbl_metal = mat("Tbl_Metal", 130, 128, 122, metallic=0.6, roughness=0.7)
tbl_wood = mat("Tbl_Wood", 85, 68, 50, roughness=0.88)
cube("ST_Top", 0, 0, 0.45, 0.5, 0.42, 0.03, tbl_metal)
cube("ST_Body", 0, 0, 0.24, 0.48, 0.4, 0.35, tbl_metal)
cube("ST_Drawer", 0, -0.21, 0.24, 0.44, 0.02, 0.28, tbl_wood)
cube("ST_Handle", 0, -0.23, 0.24, 0.15, 0.01, 0.015, tbl_metal)
for xi in [-0.22, 0.22]:
    for yi in [-0.18, 0.18]:
        cube(f"ST_Leg{xi}{yi}", xi, yi, 0.03, 0.03, 0.03, 0.06, tbl_metal)
bpy.ops.mesh.primitive_cylinder_add(radius=0.035, depth=0.1, location=(0.15, -0.1, 0.5))
cup = bpy.context.active_object
cup.name = "ST_Cup"
glass_m = mat("Glass", 200, 215, 220, roughness=0.05)
add_mat(cup, glass_m)
export("prop_side_table")

# 10. MAGAZINE
clear()
mag_paper = mat("Mag_Paper", 205, 195, 170, roughness=0.95)
mag_cover = mat("Mag_Cover", 160, 60, 60, roughness=0.85)
cube("Mag_Body", 0, 0, 0, 0.15, 0.2, 0.01, mag_cover)
cube("Mag_Pages", 0, 0, 0, 0.14, 0.19, 0.008, mag_paper)
cube("Mag_Spine", -0.073, 0, 0, 0.003, 0.19, 0.012, mag_paper)
export("prop_magazine")

# 11. WINDOW
clear()
win_wood = mat("Win_Wood", 90, 72, 52, roughness=0.88)
win_glass = mat("Win_Glass", 100, 120, 130, roughness=0.08)
win_dark = mat("Win_Dark", 5, 8, 12, roughness=0.05)
cube("Win_Frame_T", 0, 0, 0.65, 0.82, 0.1, 0.06, win_wood)
cube("Win_Frame_B", 0, 0, -0.65, 0.82, 0.1, 0.06, win_wood)
cube("Win_Frame_L", -0.41, 0, 0, 0.06, 0.1, 1.3, win_wood)
cube("Win_Frame_R", 0.41, 0, 0, 0.06, 0.1, 1.3, win_wood)
cube("Win_Divider", 0, 0, 0, 0.82, 0.08, 0.05, win_wood)
cube("Win_Glass_T", 0, 0.015, 0.33, 0.74, 0.04, 0.57, win_glass)
cube("Win_Glass_B", 0, 0.015, -0.33, 0.74, 0.04, 0.57, win_glass)
cube("Win_Exterior", 0, 0.12, 0, 0.78, 0.02, 1.2, win_dark)
curtain_m = mat("Curtain", 185, 180, 165, roughness=0.95)
cube("Win_Curtain_L", -0.55, 0.1, 0.1, 0.2, 0.05, 1.3, curtain_m)
cube("Win_Curtain_R", 0.55, 0.1, 0.1, 0.2, 0.05, 1.3, curtain_m)
export("prop_window")

# 12. MIRROR
clear()
mirror_frame = mat("Mirror_Frame", 95, 88, 78, metallic=0.4, roughness=0.75)
mirror_fog = mat("Mirror_Fog", 160, 165, 168, roughness=0.3)
cube("Mir_Frame_T", 0, 0, 0.5, 0.7, 0.06, 0.04, mirror_frame)
cube("Mir_Frame_B", 0, 0, -0.5, 0.7, 0.06, 0.04, mirror_frame)
cube("Mir_Frame_L", -0.35, 0, 0, 0.04, 0.06, 1.0, mirror_frame)
cube("Mir_Frame_R", 0.35, 0, 0, 0.04, 0.06, 1.0, mirror_frame)
cube("Mir_Glass", 0, 0.01, 0, 0.62, 0.015, 0.92, mirror_fog)
for x, z in [(-0.3, -0.4), (0.3, -0.4), (-0.3, 0.4), (0.3, 0.4)]:
    rust_m = mat(f"MirRust{x}{z}", 80, 45, 15, roughness=0.9)
    cube(f"Mir_Rust{x}{z}", x, 0.02, z, 0.06, 0.01, 0.06, rust_m)
export("prop_mirror")

# 13. BLOOD PRESSURE MONITOR
clear()
bp_metal = mat("BP_Metal", 120, 118, 112, metallic=0.5, roughness=0.7)
bp_dark = mat("BP_Dark", 40, 38, 35, roughness=0.85)
bp_face = mat("BP_Face", 220, 218, 205, roughness=0.9)
cube("BP_Box", 0, 0, 0, 0.2, 0.12, 0.25, bp_metal)
cube("BP_Display", 0, 0.065, 0.04, 0.16, 0.01, 0.18, bp_face)
cube("BP_Cuff_Base", 0, 0, -0.18, 0.12, 0.08, 0.08, bp_dark)
bpy.ops.mesh.primitive_cylinder_add(radius=0.05, depth=0.1, location=(0, 0, -0.3))
cuff = bpy.context.active_object
cuff.name = "BP_Cuff"
cuff.rotation_euler = (math.pi/2, 0, 0)
add_mat(cuff, bp_dark)
cube("BP_Wall_Mount", 0, -0.07, 0, 0.22, 0.02, 0.28, bp_metal)
export("prop_blood_pressure_monitor")

# 14. PROCEDURE TABLE
clear()
proc_metal = mat("Proc_Metal", 115, 112, 105, metallic=0.7, roughness=0.65)
proc_leather = mat("Proc_Leather", 100, 82, 60, roughness=0.85)
proc_rust = mat("Proc_Rust", 100, 50, 20, metallic=0.3, roughness=0.9)
cube("PT_Top", 0, 0, 0.7, 0.7, 1.9, 0.08, proc_leather)
for x in [-0.32, 0.32]:
    cube(f"PT_Frame{x}", x, 0, 0.45, 0.04, 1.9, 0.04, proc_metal)
for y in [-0.8, 0, 0.8]:
    cube(f"PT_Cross{y}", 0, y, 0.3, 0.7, 0.03, 0.03, proc_metal)
for xi, yi in [(-0.28, -0.88), (0.28, -0.88), (-0.28, 0.88), (0.28, 0.88)]:
    cube(f"PT_Leg{xi}{yi}", xi, yi, 0.22, 0.04, 0.04, 0.44, proc_rust)
    cyl(f"PT_Cas{xi}{yi}", xi, yi, 0.03, 0.04, 0.06, proc_rust)
export("prop_procedure_table")

# 15. MEDICAL TRAY
clear()
tray_metal = mat("Tray_Metal", 160, 158, 152, metallic=0.8, roughness=0.45)
tray_rust = mat("Tray_Rust", 110, 60, 25, metallic=0.3, roughness=0.9)
cube("Tray_Base", 0, 0, 0, 0.5, 0.32, 0.025, tray_metal)
cube("Tray_Wall_F", 0, -0.17, 0.03, 0.5, 0.015, 0.06, tray_metal)
cube("Tray_Wall_B", 0, 0.17, 0.03, 0.5, 0.015, 0.06, tray_metal)
cube("Tray_Wall_L", -0.255, 0, 0.03, 0.015, 0.32, 0.06, tray_metal)
cube("Tray_Wall_R", 0.255, 0, 0.03, 0.015, 0.32, 0.06, tray_metal)
cyl("Tray_Pole", 0, 0, -0.45, 0.02, 0.9, tray_rust)
cyl("Tray_Base_Ring", 0, 0, -0.9, 0.12, 0.025, tray_rust)
cube("Tool_Scissors", 0.1, 0, 0.03, 0.02, 0.16, 0.01, tray_metal)
cube("Tool_Tweezers", -0.05, 0.05, 0.03, 0.01, 0.14, 0.01, tray_metal)
cube("Tool_Gauze", -0.1, -0.05, 0.03, 0.08, 0.08, 0.02, mat("Gauze", 220, 218, 210, roughness=0.97))
export("prop_medical_tray")

# 16. MEDICINE CABINET
clear()
cab_metal = mat("Cab_Metal", 120, 118, 110, metallic=0.5, roughness=0.7)
cab_glass = mat("Cab_Glass", 180, 195, 200, roughness=0.1)
cube("MC_Body", 0, 0, 0.6, 0.7, 0.35, 1.2, cab_metal)
for z in [0.2, 0.6, 1.0]:
    cube(f"MC_Shelf{z}", 0, 0, z, 0.66, 0.33, 0.025, cab_metal)
cube("MC_Door_L", -0.18, -0.175, 0.6, 0.3, 0.015, 1.15, cab_glass)
cube("MC_Door_R", 0.18, -0.175, 0.6, 0.3, 0.015, 1.15, cab_glass)
for xi, zi in [(-0.12, 0.32), (0.05, 0.32), (-0.15, 0.72), (0.1, 0.72), (-0.05, 1.12)]:
    bpy.ops.mesh.primitive_cylinder_add(radius=0.035, depth=0.12, location=(xi, 0, zi))
    bt = bpy.context.active_object
    bt_m = mat(f"Bottle{xi}{zi}", random.randint(100, 180), random.randint(120, 180), random.randint(80, 150), roughness=0.2)
    add_mat(bt, bt_m)
export("prop_medicine_cabinet")

# 17. SINK
clear()
sink_ceramic = mat("Sink_Ceramic", 195, 188, 170, roughness=0.3)
sink_metal = mat("Sink_Metal", 120, 118, 110, metallic=0.7, roughness=0.5)
sink_rust = mat("Sink_Rust", 110, 70, 30, roughness=0.85)
cube("Sink_Body", 0, 0, 0, 0.55, 0.42, 0.16, sink_ceramic)
cube("Sink_Bowl", 0, 0, 0.04, 0.42, 0.32, 0.1, mat("SinkBowl", 160, 155, 142, roughness=0.25))
cyl("Sink_Faucet_V", 0, -0.1, 0.18, 0.018, 0.12, sink_rust)
cyl("Sink_Faucet_H", 0, -0.08, 0.22, 0.012, 0.1, sink_rust, rx=math.pi/2)
cyl("Sink_Faucet_Spout", 0.02, -0.04, 0.2, 0.01, 0.08, sink_rust, rx=math.radians(30))
bpy.ops.mesh.primitive_cylinder_add(radius=0.022, depth=0.03, location=(-0.05, -0.08, 0.23))
v1 = bpy.context.active_object
add_mat(v1, sink_rust)
bpy.ops.mesh.primitive_cylinder_add(radius=0.022, depth=0.03, location=(0.05, -0.08, 0.23))
v2 = bpy.context.active_object
add_mat(v2, sink_rust)
cube("Sink_Wall_Mount", 0, 0.22, 0.05, 0.6, 0.04, 0.2, sink_metal)
export("prop_sink")

# 18. METAL SHELF
clear()
shelf_metal = mat("Shelf_Metal", 105, 100, 92, metallic=0.5, roughness=0.8)
shelf_rust = mat("Shelf_Rust", 95, 48, 18, metallic=0.3, roughness=0.92)
for xi in [-0.45, 0.45]:
    for yi in [-0.15, 0.15]:
        cube(f"Shelf_Post{xi}{yi}", xi, yi, 1.0, 0.03, 0.03, 2.0, shelf_rust)
for z in [0.1, 0.55, 1.0, 1.45, 1.88]:
    cube(f"Shelf_Board{z}", 0, 0, z, 0.9, 0.3, 0.025, shelf_metal)
for z in [0.1, 1.88]:
    for yi in [-0.15, 0.15]:
        cube(f"Shelf_H{z}{yi}", 0, yi, z + 0.1, 0.88, 0.02, 0.02, shelf_rust)
export("prop_metal_shelf")

# 19. CHART FILE
clear()
file_paper = mat("File_Paper", 195, 185, 160, roughness=0.96)
file_label = mat("File_Label", 210, 200, 175, roughness=0.94)
cube("File_Body", 0, 0, 0, 0.03, 0.22, 0.3, file_paper)
cube("File_Cover_F", -0.016, 0, 0, 0.002, 0.22, 0.3, mat("FileCoverF", 165, 148, 118, roughness=0.92))
cube("File_Cover_B", 0.016, 0, 0, 0.002, 0.22, 0.3, mat("FileCoverB", 158, 140, 112, roughness=0.92))
cube("File_Label", 0, -0.112, 0.08, 0.035, 0.002, 0.08, file_label)
export("prop_chart_file")

# 20. OLD DESK
clear()
desk_wood = mat("Desk_Wood", 80, 62, 42, roughness=0.88)
desk_metal = mat("Desk_Metal", 100, 98, 90, metallic=0.5, roughness=0.75)
cube("Desk_Top", 0, 0, 0.75, 1.2, 0.6, 0.04, desk_wood)
cube("Desk_Front", 0, -0.305, 0.42, 1.15, 0.025, 0.65, desk_wood)
for xi in [-0.6, 0.6]:
    cube(f"Desk_Side{xi}", xi, 0, 0.42, 0.025, 0.58, 0.65, desk_wood)
for z in [0.58, 0.44, 0.3]:
    cube(f"Desk_Drawer{z}", 0.4, -0.302, z, 0.35, 0.015, 0.1, desk_wood)
    cube(f"Desk_DH{z}", 0.4, -0.315, z, 0.12, 0.01, 0.02, desk_metal)
for xi in [-0.56, 0.56]:
    cube(f"Desk_Leg{xi}", xi, 0.26, 0.37, 0.04, 0.04, 0.74, desk_wood)
export("prop_old_desk")

# 21. DESK LAMP
clear()
dl_metal = mat("DL_Metal", 90, 88, 82, metallic=0.6, roughness=0.65)
dl_emit = mat("DL_Bulb", 240, 220, 160, roughness=0.3, emission=(255, 230, 150))
cyl("DL_Base", 0, 0, 0.02, 0.1, 0.03, dl_metal)
cube("DL_Arm1", 0, 0, 0.22, 0.02, 0.02, 0.4, dl_metal)
cube("DL_Joint1", 0, 0, 0.42, 0.03, 0.03, 0.03, dl_metal)
arm2 = cube("DL_Arm2", 0.12, 0, 0.48, 0.02, 0.02, 0.3, dl_metal)
arm2.rotation_euler = (0, math.radians(30), 0)
bpy.ops.mesh.primitive_cone_add(radius1=0.1, radius2=0.03, depth=0.12, location=(0.22, 0, 0.38))
shade = bpy.context.active_object
shade.name = "DL_Shade"
shade.rotation_euler = (math.pi, 0, 0)
add_mat(shade, dl_metal)
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.03, location=(0.22, 0, 0.35))
bulb = bpy.context.active_object
bulb.name = "DL_Bulb"
add_mat(bulb, dl_emit)
export("prop_desk_lamp")

# 22. FILE CABINET
clear()
fc_metal = mat("FC_Metal", 108, 105, 98, metallic=0.55, roughness=0.72)
fc_rust = mat("FC_Rust", 100, 52, 20, roughness=0.9)
cube("FC_Body", 0, 0, 0.7, 0.5, 0.55, 1.4, fc_metal)
for i, z in enumerate([1.12, 0.78, 0.44, 0.1]):
    cube(f"FC_Drawer{i}", 0, -0.28, z, 0.46, 0.02, 0.28, fc_metal)
    cube(f"FC_Handle{i}", 0, -0.3, z, 0.18, 0.015, 0.018, fc_rust)
for xi in [-0.22, 0.22]:
    cube(f"FC_Leg{xi}", xi, 0, 0.03, 0.04, 0.5, 0.06, fc_rust)
export("prop_file_cabinet")

# 23. CARDBOARD BOX
clear()
box_card = mat("Box_Card", 175, 148, 105, roughness=0.97)
box_dark = mat("Box_Dark", 135, 112, 78, roughness=0.97)
cube("Box_Body", 0, 0, 0.2, 0.5, 0.4, 0.4, box_card)
flap1 = cube("Box_Flap1", -0.12, 0, 0.41, 0.24, 0.38, 0.02, box_dark)
flap1.rotation_euler = (math.radians(15), 0, 0)
flap2 = cube("Box_Flap2", 0.12, 0, 0.41, 0.24, 0.38, 0.02, box_dark)
flap2.rotation_euler = (math.radians(-10), 0, 0)
cube("Box_Tape", 0, 0, 0.42, 0.08, 0.42, 0.015, mat("Tape", 180, 155, 90, roughness=0.7))
export("prop_cardboard_box")

# 24. CURTAIN RAIL
clear()
rail_metal = mat("Rail_Metal", 140, 138, 132, metallic=0.6, roughness=0.6)
curtain_m = mat("Curtain_Old", 182, 178, 162, roughness=0.96)
cyl("CR_Rail", 0, 0, 0, 0.012, 2.0, rail_metal, rx=0, ry=math.pi/2, rz=0)
for x in [-0.8, -0.5, -0.2, 0.1, 0.4, 0.7]:
    bpy.ops.mesh.primitive_torus_add(major_radius=0.022, minor_radius=0.005,
        major_segments=10, minor_segments=6, location=(x, 0, 0))
    rng = bpy.context.active_object
    add_mat(rng, rail_metal)
cube("CR_Curtain1", -0.4, 0.05, -0.65, 0.8, 0.04, 1.3, curtain_m)
cube("CR_Curtain2", 0.4, 0.03, -0.65, 0.8, 0.03, 1.3, curtain_m)
export("prop_curtain_rail")

# 25. BED CURTAIN RAIL
clear()
rail_metal = mat("Rail_Metal", 140, 138, 132, metallic=0.6, roughness=0.6)
curtain_m = mat("Curtain_Old", 182, 178, 162, roughness=0.96)
for dx, dz in [(-0.55, 0), (0.55, 0), (0, 0.55), (0, -0.55)]:
    if dx != 0:
        cube(f"BedRail_V{dx}", dx, 0, 0.9, 0.02, 0.02, 1.8, rail_metal)
cyl("BedRail_Top1", 0, 0, 1.8, 0.012, 1.1, rail_metal, rx=0, ry=math.pi/2, rz=0)
cyl("BedRail_Top2", 0, 0.55, 1.8, 0.012, 1.1, rail_metal, rx=0, ry=math.pi/2, rz=0)
cyl("BedRail_Top3", -0.55, 0, 1.8, 0.012, 1.1, rail_metal, rx=0, ry=0, rz=0)
cyl("BedRail_Top4", 0.55, 0, 1.8, 0.012, 1.1, rail_metal, rx=0, ry=0, rz=0)
cube("Bed_Curtain_Side", -0.55, 0.2, 0.9, 0.04, 0.7, 1.8, curtain_m)
export("prop_bed_curtain_rail")

print("=== ALL 25 PROPS EXPORTED ===")
