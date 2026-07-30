# 消灯 — キャラクターモデル生成
#
# Unity 側でプリミティブを並べて人型を作っていたが、関節ごとに部品が分かれるため
# 「可動フィギュア」に見えてしまう。Blender の Skin モディファイアで骨格に肉付けし、
# 細分化して一体の滑らかなメッシュにする。
#
# 実行:
#   blender --background --python tools/blender/make_characters.py
#
# 出力:
#   Assets/Models/Characters/*.fbx   Unity 用モデル
#   Screenshots/blender_*.png        確認用プレビュー（Unity を起動せずに造形を確認できる）

import bpy
import bmesh
import math
import os
import sys
from mathutils import Vector

PROJECT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MODEL_DIR = os.path.join(PROJECT, "Assets", "Models", "Characters")
SHOT_DIR = os.path.join(PROJECT, "Screenshots")


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# ----------------------------------------------------------------------
# 骨格定義
#
# Blender は Z-up。座標は (右, 前, 上) で、キャラクターは +Y を向く。
# 「わずかに高く痩せていて、腕が長い」= 人間に近いが何かおかしい、を狙う。
# ----------------------------------------------------------------------
def humanoid_skeleton(lean=0.02, arm_drop=0.0, hunch=0.0):
    """関節名 -> (位置, 半径)。half=True の側は後でミラーする。"""
    j = {
        "hips":      (Vector((0.0, lean * 0.3, 0.94)), 0.125),
        "spine":     (Vector((0.0, lean * 0.6, 1.14)), 0.135),
        "chest":     (Vector((0.0, lean + hunch, 1.36)), 0.150),
        "neck":      (Vector((0.0, lean * 1.2 + hunch * 1.4, 1.49)), 0.052),
        "head":      (Vector((0.0, lean * 1.3 + hunch * 1.6, 1.62)), 0.098),
        "headtop":   (Vector((0.0, lean * 1.3 + hunch * 1.6, 1.715)), 0.078),

        "shoulder":  (Vector((0.165, lean * 0.9 + hunch * 0.5, 1.385)), 0.068),
        "elbow":     (Vector((0.205, lean + 0.02, 1.075 - arm_drop)), 0.049),
        "wrist":     (Vector((0.190, lean + 0.05, 0.760 - arm_drop * 1.6)), 0.034),
        "hand":      (Vector((0.183, lean + 0.06, 0.672 - arm_drop * 1.8)), 0.043),

        "hip":       (Vector((0.088, 0.0, 0.905)), 0.090),
        "knee":      (Vector((0.090, 0.015, 0.492)), 0.063),
        "ankle":     (Vector((0.088, -0.012, 0.078)), 0.050),
        "toe":       (Vector((0.088, 0.135, 0.032)), 0.045),
    }
    bones = [
        ("hips", "spine"), ("spine", "chest"), ("chest", "neck"),
        ("neck", "head"), ("head", "headtop"),
        ("chest", "shoulder"), ("shoulder", "elbow"), ("elbow", "wrist"), ("wrist", "hand"),
        ("hips", "hip"), ("hip", "knee"), ("knee", "ankle"), ("ankle", "toe"),
    ]
    mirrored = {"shoulder", "elbow", "wrist", "hand", "hip", "knee", "ankle", "toe"}
    return j, bones, mirrored


def build_body(name, lean=0.02, arm_drop=0.0, hunch=0.0, thin=1.0):
    """骨格に Skin モディファイアで肉付けし、細分化して一体のメッシュにする。"""
    joints, bones, mirrored = humanoid_skeleton(lean, arm_drop, hunch)

    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)

    bm = bmesh.new()
    index = {}

    def add(key, side):
        pos, _ = joints[key]
        co = Vector((pos.x * side, pos.y, pos.z)) if key in mirrored else pos
        tag = f"{key}.{side}" if key in mirrored else key
        if tag not in index:
            index[tag] = bm.verts.new(co)
        return tag

    edges = []
    for side in (1, -1):
        for a, b in bones:
            # 体幹は左右で共有するので片側だけ作る
            if a not in mirrored and b not in mirrored and side == -1:
                continue
            ta, tb = add(a, side), add(b, side)
            edges.append((ta, tb))

    bm.verts.ensure_lookup_table()
    for ta, tb in edges:
        va, vb = index[ta], index[tb]
        if not bm.edges.get((va, vb)):
            bm.edges.new((va, vb))

    order = list(index.keys())
    bm.verts.index_update()
    vert_order = {tag: index[tag].index for tag in order}
    bm.to_mesh(mesh)
    bm.free()

    skin = obj.modifiers.new("Skin", 'SKIN')
    skin.use_smooth_shade = True
    skin.branch_smoothing = 0.35

    layer = mesh.skin_vertices[0].data
    for tag, vi in vert_order.items():
        key = tag.split(".")[0]
        r = joints[key][1] * thin
        layer[vi].radius = (r, r)
    # ルートを腰に。ここを起点に肉が生成される
    layer[vert_order["hips"]].use_root = True

    sub = obj.modifiers.new("Subdiv", 'SUBSURF')
    sub.levels = 2
    sub.render_levels = 2

    return obj


def build_gown(name, hem_z=0.42, waist_z=0.96):
    """患者衣。裾の広がった筒。
    細分化を掛けると球状に丸まって「おむつ」に見えたので、円錐のまま
    滑らかシェーディングと厚みだけを与える。"""
    bpy.ops.mesh.primitive_cone_add(
        vertices=32, radius1=0.300, radius2=0.170,
        depth=waist_z - hem_z,
        location=(0.0, 0.0, (waist_z + hem_z) * 0.5))
    gown = bpy.context.active_object
    gown.name = name
    solid = gown.modifiers.new("Solid", 'SOLIDIFY')
    solid.thickness = 0.010
    for poly in gown.data.polygons:
        poly.use_smooth = True
    return gown


def finalize(obj):
    """モディファイアを適用して素直な1メッシュにする（Unity 側で扱いやすくするため）。"""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for m in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=m.name)
        except RuntimeError as e:
            print(f"  modifier_apply failed on {m.name}: {e}")
    obj.select_set(False)
    uv_unwrap(obj)


def uv_unwrap(obj):
    """
    UV を作る。Skin モディファイアや join した結果は UV を持たないため、
    これが無いとテクスチャが一切乗らない（質感を足そうとして気づいた）。
    Smart UV Project は継ぎ目が出るが、汚しや布目のような微細テクスチャなら
    実用上問題にならない。
    """
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def export_fbx(objects, filename):
    os.makedirs(MODEL_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path = os.path.join(MODEL_DIR, filename)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        global_scale=1.0,
        axis_forward='-Z',
        axis_up='Y',
        mesh_smooth_type='FACE',
        add_leaf_bones=False,
    )
    print(f"  exported {path}")
    return path


def render_preview(objects, filename, camera_z=1.05, distance=3.6):
    """Unity を起動せずに造形を確認するためのプレビュー。
    EEVEE はヘッドレスで GPU コンテキストを要求することがあるので Cycles(CPU) を使う。"""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.cycles.device = 'CPU'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 700
    scene.render.film_transparent = False

    if scene.world is None:
        scene.world = bpy.data.worlds.new("W")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.05, 0.06, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0

    bpy.ops.object.camera_add(location=(0.0, -distance, camera_z), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.active_object
    scene.camera = cam

    bpy.ops.object.light_add(type='AREA', location=(1.6, -2.2, 2.6))
    key = bpy.context.active_object
    key.data.energy = 320
    key.data.size = 2.0
    key.rotation_euler = (math.radians(55), 0, math.radians(35))

    bpy.ops.object.light_add(type='AREA', location=(-2.0, -1.4, 1.4))
    fill = bpy.context.active_object
    fill.data.energy = 90
    fill.data.size = 2.5
    fill.rotation_euler = (math.radians(75), 0, math.radians(-50))

    os.makedirs(SHOT_DIR, exist_ok=True)
    scene.render.filepath = os.path.join(SHOT_DIR, filename)
    bpy.ops.render.render(write_still=True)
    print(f"  preview {scene.render.filepath}")


def main():
    print("=== 消灯 character build ===")

    # ── 患者（主役の見た目。前傾が浅く、痩せている）──
    clear_scene()
    body = build_body("Patient_Body", lean=0.018, arm_drop=0.0, hunch=0.01, thin=0.94)
    finalize(body)
    gown = build_gown("Patient_Gown")
    finalize(gown)
    render_preview([body, gown], "blender_patient.png")
    export_fbx([body, gown], "Patient.fbx")

    # ── 一般（看護師・医師など。標準体型）──
    clear_scene()
    civ = build_body("Civilian_Body", lean=0.02, arm_drop=0.0, hunch=0.0, thin=1.0)
    finalize(civ)
    render_preview([civ], "blender_civilian.png")
    export_fbx([civ], "Civilian.fbx")

    # ── 夜間警備員（体格がよく、やや前傾。追ってくる側）──
    clear_scene()
    guard = build_body("Guard_Body", lean=0.03, arm_drop=0.01, hunch=0.03, thin=1.10)
    finalize(guard)
    render_preview([guard], "blender_guard.png")
    export_fbx([guard], "Guard.fbx")

    # ── 黒い人影（幻覚レベル高。異様に長く、深く前傾）──
    clear_scene()
    shadow = build_body("Shadow_Body", lean=0.05, arm_drop=0.06, hunch=0.06, thin=0.80)
    shadow.scale = (1.0, 1.0, 1.12)
    bpy.context.view_layer.objects.active = shadow
    shadow.select_set(True)
    bpy.ops.object.transform_apply(scale=True)
    shadow.select_set(False)
    finalize(shadow)
    render_preview([shadow], "blender_shadow.png")
    export_fbx([shadow], "Shadow.fbx")

    print("=== done ===")


if __name__ == "__main__":
    main()
