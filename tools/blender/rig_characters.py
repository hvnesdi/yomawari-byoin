# 消灯 — キャラクターにボーンと歩行モーションを付ける
#
# **敵は NavMesh の上を滑るように移動していた。** 姿勢が固まったままの人型が
# 一定速度で寄ってくるのは、怖いというより不気味の手前で止まる。
# 歩行モーションが付くだけで挙動の説得力が大きく変わる。
#
# Mixamo が使えれば人体も動きも一度に解決するが、取得に Adobe ログインが要る。
# ここは待たずにできるところまでやる: 既にある関節定義から armature を組み、
# 手でキーを打った歩行・待機・徘徊を焼き込む。
#
# 動きの作り: 接地(contact) → 沈み(down) → 通過(passing) → 伸び(up) の4姿勢を
# 1周期に置き、左右を半周期ずらす。腕は脚と逆位相に振る。
# 腰の上下動と胸の捻りを入れないと「脚だけ動く人形」になる。
#
# 実行:
#   "C:\Program Files\Blender Foundation\Blender 5.1\blender.exe" --background --python tools/blender/rig_characters.py

import math
import os
import sys

import bpy
from mathutils import Vector, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.append(HERE)

import make_characters as mc   # noqa: E402  （関節定義とメッシュ生成を再利用する）

MODEL_DIR = mc.MODEL_DIR
FPS = 30


# ----------------------------------------------------------------------
# armature
# ----------------------------------------------------------------------
def build_armature(name, lean, arm_drop, hunch):
    """
    メッシュと同じ関節定義から骨を組む。
    別々に定義すると、体型を変えたときに骨とメッシュがずれる。
    """
    joints, bones, mirrored = mc.humanoid_skeleton(lean, arm_drop, hunch)

    arm_data = bpy.data.armatures.new(name + "_Armature")
    arm_obj = bpy.data.objects.new(name + "_Armature", arm_data)
    bpy.context.collection.objects.link(arm_obj)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='EDIT')

    created = {}

    def make(tag_a, tag_b, side):
        """side: '' / 'L' / 'R'。左右は x を反転する"""
        a, _ = joints[tag_a]
        b, _ = joints[tag_b]
        flip = -1.0 if side == "R" else 1.0

        head = Vector((a.x * (flip if tag_a in mirrored else 1.0), a.y, a.z))
        tail = Vector((b.x * (flip if tag_b in mirrored else 1.0), b.y, b.z))
        if (tail - head).length < 1e-4:
            return None

        bone_name = bone_id(tag_b, side)
        bone = arm_data.edit_bones.new(bone_name)
        bone.head = head
        bone.tail = tail

        # **骨の向き（roll）を揃える。**
        #
        # 揃えないと、骨ごとにローカル軸がばらばらになる。最初はこれをやらずに
        # 「X 回りに振る」と書いたところ、腰の骨では横倒しの回転になり、
        # 歩くのではなく**体ごと傾いて倒れていく**動きになった。
        #
        # ローカル X を必ずワールド X（左右軸）に向ける。
        # そうすれば X 回り = 前後の振り、Y 回り = 骨の軸まわりの捻り、と
        # どの骨でも同じ意味になる。
        # Blender の align_roll はローカル Z を指定方向に寄せるので、
        # Z = X × (骨の向き) を渡す。
        direction = (tail - head).normalized()
        bone.align_roll(Vector((1.0, 0.0, 0.0)).cross(direction))

        created[bone_name] = bone

        parent_name = bone_id(tag_a, side)
        if parent_name in created:
            bone.parent = created[parent_name]
            # つなげない。つなげると回転が親に引きずられて動きが破綻する
            bone.use_connect = False
        return bone

    # 体幹は左右の区別が無い
    for a, b in bones:
        if a in mirrored or b in mirrored:
            continue
        make(a, b, "")

    # 手足は左右に作る
    for side in ("L", "R"):
        for a, b in bones:
            if a not in mirrored and b not in mirrored:
                continue
            make(a, b, side)

    bpy.ops.object.mode_set(mode='OBJECT')
    return arm_obj


def bone_id(tag, side):
    """体幹は tag そのまま、手足は tag_L / tag_R"""
    _, _, mirrored = mc.humanoid_skeleton()
    if tag in mirrored and side:
        return f"{tag}_{side}"
    return tag


def bind(mesh_objects, arm_obj):
    """
    メッシュを骨に追従させる。自動ウェイトで十分。
    Skin モディファイア由来の一体成型なので、部位の境目が無く自動でも破綻しにくい。
    """
    bpy.ops.object.select_all(action='DESELECT')
    for m in mesh_objects:
        m.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    bpy.ops.object.select_all(action='DESELECT')


# ----------------------------------------------------------------------
# モーション
# ----------------------------------------------------------------------
def rot(pose_bone, frame, x=0.0, y=0.0, z=0.0):
    """度で指定してキーを打つ。ラジアンのままだと書いていて量が掴めない"""
    pose_bone.rotation_mode = 'XYZ'
    pose_bone.rotation_euler = Euler((math.radians(x), math.radians(y), math.radians(z)), 'XYZ')
    pose_bone.keyframe_insert("rotation_euler", frame=frame)


def loc(pose_bone, frame, x=0.0, y=0.0, z=0.0):
    pose_bone.location = Vector((x, y, z))
    pose_bone.keyframe_insert("location", frame=frame)


def new_action(arm_obj, name):
    action = bpy.data.actions.new(name)
    if arm_obj.animation_data is None:
        arm_obj.animation_data_create()
    arm_obj.animation_data.action = action
    return action


def make_cycle(arm_obj, name, length, leg_swing, arm_swing, bob, hunch_extra,
               knee_lift, speed_wobble=0.0):
    """
    歩行の1周期。左右を半周期ずらして打つ。
    length はフレーム数。最後のフレームは先頭と同じ姿勢にしてループを閉じる。
    """
    action = new_action(arm_obj, name)
    pb = arm_obj.pose.bones
    half = length // 2

    def leg(side, phase):
        hip = pb.get(f"hip_{side}")
        knee = pb.get(f"knee_{side}")
        ankle = pb.get(f"ankle_{side}")
        if hip is None:
            return
        for i in range(5):
            f = 1 + (i * length) // 4
            t = (i / 4.0 + phase) % 1.0
            # 前後の振り。sin 1周期でひと足
            swing = math.sin(t * math.tau) * leg_swing
            rot(hip, f, x=swing)
            # 膝は前に出すときだけ曲げる（後ろでも曲げると走りに見える）
            bend = max(0.0, -math.sin(t * math.tau + 0.6)) * knee_lift
            rot(knee, f, x=-bend)
            if ankle:
                rot(ankle, f, x=math.sin(t * math.tau + math.pi * 0.5) * (leg_swing * 0.35))

    def arm(side, phase):
        shoulder = pb.get(f"shoulder_{side}")
        elbow = pb.get(f"elbow_{side}")
        if shoulder is None:
            return
        for i in range(5):
            f = 1 + (i * length) // 4
            t = (i / 4.0 + phase) % 1.0
            rot(shoulder, f, x=math.sin(t * math.tau) * arm_swing)
            if elbow:
                # 常に少し曲げておく。真っ直ぐだと人形の腕になる
                rot(elbow, f, x=-(12.0 + max(0.0, math.sin(t * math.tau)) * 14.0))

    leg("L", 0.0)
    leg("R", 0.5)
    arm("L", 0.5)     # 腕は脚と逆
    arm("R", 0.0)

    hips = pb.get("hips")
    spine = pb.get("spine")
    chest = pb.get("chest")
    head = pb.get("head")

    # 捻りと上下動は骨の軸まわり（ローカル Y）。roll を揃えてあるので
    # X=前後の振り / Y=捻り、がどの骨でも成り立つ
    for i in range(5):
        f = 1 + (i * length) // 4
        t = i / 4.0
        if hips is not None:
            # 上下動は1歩につき1回なので絶対値を取る。
            # 腰の骨は上を向いているので、ローカル Y に沿って動かすと上下になる
            loc(hips, f, y=-abs(math.sin(t * math.tau)) * bob)
            rot(hips, f, y=math.sin(t * math.tau) * 4.0)
        if spine is not None:
            rot(spine, f, x=hunch_extra * 0.4, y=-math.sin(t * math.tau) * 3.0)
        if chest is not None:
            # 胸は腰と逆に捻る。これが無いと上半身が板のまま運ばれる
            rot(chest, f, x=hunch_extra, y=-math.sin(t * math.tau) * 6.0)
        if head is not None:
            rot(head, f, x=-hunch_extra * 0.6,
                y=math.sin(t * math.tau + math.pi * 0.25) * speed_wobble)

    action.use_fake_user = True
    return action


def make_idle(arm_obj, name, length, sway, breath):
    """待機。完全に止めない。呼吸と微かな重心移動だけ入れる"""
    action = new_action(arm_obj, name)
    pb = arm_obj.pose.bones

    for i in range(5):
        f = 1 + (i * length) // 4
        t = i / 4.0
        s = math.sin(t * math.tau)
        if "hips" in pb:
            loc(pb["hips"], f, z=s * breath * 0.4)
            rot(pb["hips"], f, z=s * sway)
        if "chest" in pb:
            rot(pb["chest"], f, x=-s * breath * 30.0, z=-s * sway * 0.6)
        if "head" in pb:
            rot(pb["head"], f, x=s * 1.5, z=math.sin(t * math.tau * 0.5) * sway * 2.0)
        for side in ("L", "R"):
            sh = pb.get(f"shoulder_{side}")
            if sh:
                rot(sh, f, x=s * 2.0)
            el = pb.get(f"elbow_{side}")
            if el:
                rot(el, f, x=-14.0 - s * 3.0)

    action.use_fake_user = True
    return action


def push_to_nla(arm_obj, actions):
    """
    すべての動きを NLA に積む。
    FBX 書き出しは NLA トラックを別々のアニメーションとして出すので、
    こうしないと最後の1つしか Unity に渡らない。
    """
    if arm_obj.animation_data is None:
        arm_obj.animation_data_create()
    arm_obj.animation_data.action = None

    for action in actions:
        track = arm_obj.animation_data.nla_tracks.new()
        track.name = action.name
        strip = track.strips.new(action.name, 1, action)
        strip.name = action.name
        track.mute = False


# ----------------------------------------------------------------------
def export_rigged(objects, arm_obj, filename):
    os.makedirs(MODEL_DIR, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:
        o.select_set(True)
    arm_obj.select_set(True)
    bpy.context.view_layer.objects.active = arm_obj

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
        # 動きを焼いて出す。これが無いとボーンだけの静止モデルになる
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=True,
        bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
    )
    print(f"  exported {path}")
    return path


def render_walk_strip(name, arm_obj, meshes, action, frames):
    """
    歩行の途中経過を並べて描く。
    **カーブが存在することと、メッシュが変形していることは別。**
    ボーンだけ動いて肉が付いてこない（ウェイトが乗っていない）状態でも
    FBX にはアニメーションが入っているので、書き出しの検査では気づけない。
    実際に姿勢を描いて目で見るのが確実。
    """
    import bpy as _b
    arm_obj.animation_data.action = action

    scene = _b.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 16
    scene.cycles.device = 'CPU'
    scene.render.resolution_x = 420
    scene.render.resolution_y = 620
    scene.render.film_transparent = False

    # 既存のプレビューと同じ見せ方にする（比較しやすいように）
    if "PreviewCam" not in _b.data.objects:
        cam_data = _b.data.cameras.new("PreviewCam")
        cam = _b.data.objects.new("PreviewCam", cam_data)
        _b.context.collection.objects.link(cam)
        scene.camera = cam
    # **横から見る。**
    # 正面から撮っていたときは、前後の脚の振りが遠近で潰れてほとんど見えず、
    # 「脚が動いていない」ように読めた。動きを確かめる向きで撮らないと、
    # 直っているのか壊れているのかが判定できない。
    cam = _b.data.objects["PreviewCam"]
    cam.location = (3.6, 0.0, 1.05)
    cam.rotation_euler = Euler((math.radians(90), 0, math.radians(90)), 'XYZ')

    if "PreviewKey" not in _b.data.objects:
        light_data = _b.data.lights.new("PreviewKey", type='AREA')
        light_data.energy = 320
        light_data.size = 3.0
        light = _b.data.objects.new("PreviewKey", light_data)
        _b.context.collection.objects.link(light)
        light.location = (2.0, -2.6, 2.6)
        light.rotation_euler = Euler((math.radians(58), 0, math.radians(38)), 'XYZ')

    out_dir = os.path.join(os.path.dirname(os.path.dirname(HERE)), "Screenshots")
    os.makedirs(out_dir, exist_ok=True)

    for i, f in enumerate(frames):
        scene.frame_set(f)
        scene.render.filepath = os.path.join(out_dir, f"rig_{name}_walk_{i}.png")
        _b.ops.render.render(write_still=True)
    print(f"  preview frames: rig_{name}_walk_0..{len(frames) - 1}.png")


def build_character(name, filename, lean, arm_drop, hunch, thin,
                    gown=False, stretch=1.0, motion="normal", preview=False):
    mc.clear_scene()
    bpy.context.scene.render.fps = FPS

    body = mc.build_body(name + "_Body", lean=lean, arm_drop=arm_drop, hunch=hunch, thin=thin)
    if stretch != 1.0:
        body.scale = (1.0, 1.0, stretch)
        bpy.context.view_layer.objects.active = body
        body.select_set(True)
        bpy.ops.object.transform_apply(scale=True)
        body.select_set(False)
    mc.finalize(body)

    meshes = [body]
    if gown:
        g = mc.build_gown(name + "_Gown")
        mc.finalize(g)
        meshes.append(g)

    arm_obj = build_armature(name, lean, arm_drop, hunch)
    bind(meshes, arm_obj)

    bpy.context.view_layer.objects.active = arm_obj
    bpy.ops.object.mode_set(mode='POSE')

    actions = []
    if motion == "shamble":
        # 敵。引きずるように歩く。腕はほとんど振らない
        actions.append(make_idle(arm_obj, "Idle", 90, sway=1.2, breath=0.010))
        actions.append(make_cycle(arm_obj, "Walk", 44, leg_swing=17.0, arm_swing=6.0,
                                  bob=0.030, hunch_extra=7.0, knee_lift=26.0,
                                  speed_wobble=3.0))
        actions.append(make_cycle(arm_obj, "Run", 26, leg_swing=30.0, arm_swing=22.0,
                                  bob=0.055, hunch_extra=12.0, knee_lift=52.0,
                                  speed_wobble=5.0))
    else:
        actions.append(make_idle(arm_obj, "Idle", 110, sway=0.8, breath=0.008))
        actions.append(make_cycle(arm_obj, "Walk", 34, leg_swing=22.0, arm_swing=14.0,
                                  bob=0.028, hunch_extra=2.0, knee_lift=34.0))
        actions.append(make_cycle(arm_obj, "Run", 22, leg_swing=34.0, arm_swing=28.0,
                                  bob=0.050, hunch_extra=6.0, knee_lift=58.0))

    bpy.ops.object.mode_set(mode='OBJECT')

    if preview:
        walk = next((a for a in actions if a.name == "Walk"), None)
        if walk is not None:
            length = int(walk.frame_range[1])
            render_walk_strip(name, arm_obj, meshes, walk,
                              [1, length // 4, length // 2, (length * 3) // 4])

    push_to_nla(arm_obj, actions)

    export_rigged(meshes, arm_obj, filename)
    print(f"  {name}: bones={len(arm_obj.pose.bones)} actions={[a.name for a in actions]}")


def main():
    print("=== 消灯 character rigging ===")

    build_character("Patient", "Patient_Rigged.fbx",
                    lean=0.018, arm_drop=0.0, hunch=0.01, thin=0.94, gown=True)
    build_character("Civilian", "Civilian_Rigged.fbx",
                    lean=0.02, arm_drop=0.0, hunch=0.0, thin=1.0)
    build_character("Guard", "Guard_Rigged.fbx",
                    lean=0.03, arm_drop=0.01, hunch=0.03, thin=1.10, motion="shamble",
                    preview=True)   # 敵は追われる相手なので、動きを目で確認する
    build_character("Shadow", "Shadow_Rigged.fbx",
                    lean=0.05, arm_drop=0.06, hunch=0.06, thin=0.80,
                    stretch=1.12, motion="shamble")

    print("=== done ===")


if __name__ == "__main__":
    main()
