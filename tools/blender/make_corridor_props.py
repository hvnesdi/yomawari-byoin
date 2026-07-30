# 消灯 — 廊下のディテール小道具
#
# 照明・汚し・キャラクターを整えた結果、次に目立つのは廊下そのものの単調さ。
# まっすぐな箱が続くだけで、silhouette を崩すものが何も無い。
# 市販のホラーは天井の配管や壁の設備で視線を止め、奥行きを作っている。
#
# 実行:
#   blender --background --python tools/blender/make_corridor_props.py
#
# 出力:
#   Assets/Models/Props/*.fbx       Unity 用モデル
#   Screenshots/blender_prop_*.png  確認用プレビュー
#
# 向きの約束（Unity 側の配置コードと合わせるため必ず守る）:
#   Blender は Z-up。+Y をキャラクターの正面と同じ「前」とする。
#   FBX 出力で axis_forward='-Z', axis_up='Y' を指定しているので、
#   Blender の +Y が Unity の +Z（前方）に対応する。
#   - 長物（配管）は +Y 方向に伸ばす → Unity では廊下に沿う
#   - 壁付け（換気口・表示・ラジエーター）は -Y を向く面を表とする
#     → Unity では -Z 向き。配置時に親を回して壁の内側へ向ける

import bpy
import math
import os
from mathutils import Vector

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODEL_DIR = os.path.join(PROJECT, "Assets", "Models", "Props")
SHOT_DIR = os.path.join(PROJECT, "Screenshots")


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def box(name, center, size, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
    o = bpy.context.active_object
    o.name = name
    o.scale = (size[0], size[1], size[2])
    o.rotation_euler = rot
    return o


def cyl(name, center, radius, depth, axis="Z", rot_extra=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=depth, location=center)
    o = bpy.context.active_object
    o.name = name
    if axis == "Y":
        o.rotation_euler = (math.radians(90), 0, rot_extra)
    elif axis == "X":
        o.rotation_euler = (0, math.radians(90), rot_extra)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def join(objects, name):
    """複数オブジェクトを1メッシュにまとめる。Unity 側で扱いやすくするため。"""
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    merged = bpy.context.active_object
    merged.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    merged.select_set(False)
    return merged


# ----------------------------------------------------------------------
# 天井の配管。廊下に沿って +Y 方向へ 4m 伸びる
# ----------------------------------------------------------------------
def make_pipe_run():
    parts = []
    L = 4.0

    # 太い主管と細い副管。太さを変えると「設備」に見える
    parts.append(cyl("PipeMain", (0.0, L * 0.5, 0.0), 0.048, L, axis="Y"))
    parts.append(cyl("PipeThin", (0.14, L * 0.5, -0.035), 0.026, L, axis="Y"))
    # 断熱材を巻いた区間。単調さを崩す
    parts.append(cyl("PipeWrap", (0.0, L * 0.34, 0.0), 0.062, 0.55, axis="Y"))

    # 吊り金具
    for i in range(3):
        y = 0.55 + i * 1.45
        parts.append(box(f"Bracket{i}", (0.0, y, 0.075), (0.035, 0.02, 0.15)))
        parts.append(box(f"BracketPlate{i}", (0.0, y, 0.155), (0.16, 0.03, 0.012)))

    # バルブ。1本だけ形の違うものを入れると目が止まる
    parts.append(cyl("Valve", (0.0, L * 0.72, 0.0), 0.075, 0.05, axis="Y"))
    parts.append(cyl("ValveStem", (0.0, L * 0.72, 0.085), 0.012, 0.10, axis="Z"))
    parts.append(cyl("ValveWheel", (0.0, L * 0.72, 0.14), 0.055, 0.014, axis="Z"))

    return join(parts, "Pipe_Run")


# ----------------------------------------------------------------------
# 壁の換気口。-Y を向く（Unity では -Z）
# ----------------------------------------------------------------------
def make_vent():
    parts = []
    W, H, D = 0.52, 0.34, 0.05

    # 枠
    parts.append(box("VentTop",    (0.0,  0.0,  H * 0.5), (W, D, 0.035)))
    parts.append(box("VentBottom", (0.0,  0.0, -H * 0.5), (W, D, 0.035)))
    parts.append(box("VentLeft",   (-W * 0.5, 0.0, 0.0), (0.035, D, H)))
    parts.append(box("VentRight",  ( W * 0.5, 0.0, 0.0), (0.035, D, H)))
    # 奥板。穴が空いて見えないように塞ぐ
    parts.append(box("VentBack",   (0.0, 0.022, 0.0), (W, 0.012, H)))

    # 羽根。傾けると影が出て設備に見える
    slats = 6
    for i in range(slats):
        z = -H * 0.34 + i * (H * 0.68 / (slats - 1))
        parts.append(box(f"Slat{i}", (0.0, -0.012, z), (W * 0.92, 0.028, 0.022),
                          rot=(math.radians(-22), 0, 0)))

    return join(parts, "Vent_Grille")


# ----------------------------------------------------------------------
# 壁の案内表示。-Y を向く
# ----------------------------------------------------------------------
def make_sign():
    parts = []
    parts.append(box("SignPlate", (0.0, 0.0, 0.0), (0.44, 0.018, 0.16)))
    parts.append(box("SignFrame", (0.0, 0.006, 0.0), (0.46, 0.010, 0.18)))
    # 壁から浮かせる金具。影が落ちて板が壁から分離する
    for x in (-0.17, 0.17):
        parts.append(box(f"SignMount{x}", (x, 0.028, 0.0), (0.03, 0.05, 0.03)))
    return join(parts, "Wall_Sign")


# ----------------------------------------------------------------------
# 壁のラジエーター。床際に置く。-Y を向く
# ----------------------------------------------------------------------
def make_radiator():
    parts = []
    W, H = 0.95, 0.52

    parts.append(box("RadBack", (0.0, 0.045, 0.0), (W, 0.03, H)))
    # 縦のフィン
    fins = 14
    for i in range(fins):
        x = -W * 0.46 + i * (W * 0.92 / (fins - 1))
        parts.append(box(f"Fin{i}", (x, 0.0, 0.0), (0.028, 0.075, H * 0.94)))
    # 上下の管
    parts.append(cyl("RadTop", (0.0, 0.0, H * 0.5), 0.028, W, axis="X"))
    parts.append(cyl("RadBottom", (0.0, 0.0, -H * 0.5), 0.028, W, axis="X"))
    # 給水管を床へ落とす
    parts.append(cyl("RadFeed", (W * 0.42, 0.0, -H * 0.5 - 0.14), 0.018, 0.28, axis="Z"))

    return join(parts, "Radiator")


# ----------------------------------------------------------------------
def export_fbx(obj, filename):
    os.makedirs(MODEL_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    path = os.path.join(MODEL_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True,
        apply_unit_scale=True, global_scale=1.0,
        axis_forward='-Z', axis_up='Y',
        mesh_smooth_type='FACE', add_leaf_bones=False,
    )
    print(f"  exported {path}")


def render_preview(filename, target, distance, height):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 20
    scene.cycles.device = 'CPU'
    scene.render.resolution_x = 800
    scene.render.resolution_y = 600

    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.06, 0.06, 0.07, 1)

    bpy.ops.object.camera_add(location=(distance * 0.6, -distance, height + distance * 0.35))
    cam = bpy.context.active_object
    scene.camera = cam
    direction = Vector(target) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()

    bpy.ops.object.light_add(type='AREA', location=(1.4, -1.8, height + 1.6))
    key = bpy.context.active_object
    key.data.energy = 220
    key.data.size = 1.6
    key.rotation_euler = (math.radians(52), 0, math.radians(32))

    os.makedirs(SHOT_DIR, exist_ok=True)
    scene.render.filepath = os.path.join(SHOT_DIR, filename)
    bpy.ops.render.render(write_still=True)
    print(f"  preview {scene.render.filepath}")


def build(name, maker, filename, target, distance, height):
    clear_scene()
    obj = maker()
    render_preview(f"blender_prop_{name}.png", target, distance, height)
    export_fbx(obj, filename)


if __name__ == "__main__":
    print("=== 消灯 corridor props ===")
    build("pipe",     make_pipe_run, "Pipe_Run.fbx",     (0.0, 2.0, 0.0), 3.4, 0.0)
    build("vent",     make_vent,     "Vent_Grille.fbx",  (0.0, 0.0, 0.0), 1.1, 0.0)
    build("sign",     make_sign,     "Wall_Sign.fbx",    (0.0, 0.0, 0.0), 0.9, 0.0)
    build("radiator", make_radiator, "Radiator.fbx",     (0.0, 0.0, 0.0), 1.7, 0.0)
    print("=== done ===")
