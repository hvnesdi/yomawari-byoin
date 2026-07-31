using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// M17: 鏡と写真の演出を実際に動かす。
///
/// `HorrorEventSystem` には3つの鏡の演出（映りが変わる／遅れる）と
/// 写真の入れ替えが**実装済み**だったが、`mirrorRenderer` / `photoRenderer` /
/// マテリアル / スプライトのどれも未設定で、
/// **発火しても何も起きなかった**（`M16ReferenceAudit` で判明）。
/// 対応する物がシーンに1つも無かったので、置くところから作る。
///
/// ここでやること:
///   1. 鏡専用のレイヤーを追加する（鏡にだけ映る体を作るため）
///   2. 鏡・写真・額のマテリアルを作る
///   3. 廊下の壁に鏡と写真を配置する
///   4. プレイヤーに「鏡にだけ映る体」を付ける
///   5. `HorrorEventSystem` に結線する
///
/// 4 が要点。一人称なので自分の体が無く、そのままでは鏡に何も映らない。
/// 映る体が無ければ「映りが変わる」演出は成立しない。
/// 本カメラからは除外するので、視界に体が映り込むことはない。
/// </summary>
public static class M17MirrorAndPhotoPass
{
    const string MirrorLayer = "MirrorOnly";
    const string MatDir = "Assets/Materials/Horror";
    const string PhotoTexDir = "Assets/Textures/Photos";
    const string RootName = "HorrorProps";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M17: 鏡と写真を置く")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M17] 鏡と写真\n");

        int layer = EnsureLayer(log);
        var mats = BuildMaterials(log);
        var photos = LoadPhotoSprites(log);
        BuildHorrorFigurePrefab(mats, log);

        int placed = 0;
        foreach (var path in Scenes)
            placed += PlaceInScene(path, layer, mats, photos, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine($"  計 {placed} 個");
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// 鏡専用レイヤーを確保する。
    /// 本カメラから除外し、鏡のカメラだけが描く層。
    /// これが無いと「鏡にだけ映る体」が作れない。
    /// </summary>
    static int EnsureLayer(StringBuilder log)
    {
        int existing = LayerMask.NameToLayer(MirrorLayer);
        if (existing >= 0) { log.AppendLine($"  レイヤー {MirrorLayer} は既にある（{existing}）"); return existing; }

        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
        if (asset == null) { log.AppendLine("  ? TagManager が読めない"); return 0; }

        var so = new SerializedObject(asset);
        var layers = so.FindProperty("layers");

        // 0〜7 は Unity の予約。8 以降の空きを使う
        for (int i = 8; i < layers.arraySize; i++)
        {
            var element = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(element.stringValue)) continue;

            element.stringValue = MirrorLayer;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            log.AppendLine($"  レイヤー {MirrorLayer} を追加（{i}）");
            return i;
        }

        log.AppendLine("  ? 空きレイヤーが無い");
        return 0;
    }

    // ------------------------------------------------------------------
    class Mats
    {
        public Material mirrorNormal, mirrorDelay, mirrorChange;
        public Material frame, photo, bodyNormal, bodyShadow;
    }

    static Material MakeMaterial(string name, Color color, float smoothness, float metallic,
                                  bool unlit = false)
    {
        var path = $"{MatDir}/{name}.mat";
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit"
                                       : "Universal Render Pipeline/Lit");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader) mat.shader = shader;

        mat.SetColor("_BaseColor", color);
        if (!unlit)
        {
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Mats BuildMaterials(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets/Materials", "Horror");

        var m = new Mats
        {
            // 鏡面は **Unlit**。
            // 鏡が返すのは「反射した光」であって、そこに改めて光が当たるわけではない。
            // Lit にしていたときは、暗い廊下の照明で反射像まで暗く落とされ、
            // 映っているのかどうかも判らない板になっていた。
            mirrorNormal = MakeMaterial("Mirror_Normal", Color.white, 0f, 0f, unlit: true),
            // 遅れているときは見た目を変えない。変えると「遅れ」ではなく「別物」になる
            mirrorDelay = MakeMaterial("Mirror_Delay", Color.white, 0f, 0f, unlit: true),
            // 変異。映像は生きたまま、濁らせて異常だけを伝える
            mirrorChange = MakeMaterial("Mirror_Change",
                                        new Color(0.62f, 0.60f, 0.68f), 0f, 0f, unlit: true),

            frame = MakeMaterial("Photo_Frame", new Color(0.16f, 0.12f, 0.09f), 0.25f, 0f),
            photo = MakeMaterial("Photo_Surface", Color.white, 0.1f, 0f),
            bodyNormal = MakeMaterial("MirrorBody", new Color(0.72f, 0.74f, 0.78f), 0.2f, 0f),
        };

        // 鏡の中で自分が変わる先。人影と同じ材質を使う
        m.bodyShadow = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Props/Char_ShadowFigure.mat");
        if (m.bodyShadow == null) m.bodyShadow = MakeMaterial("MirrorBody_Shadow", Color.black, 0f, 0f);

        log.AppendLine("  マテリアルを用意");
        return m;
    }

    /// <summary>
    /// 「暗い部屋に人が居る」演出で出す人物。
    ///
    /// `HorrorEventSystem.npcPrefab` が未設定で、この演出も何も起きていなかった。
    /// 人影のモデルをそのまま使う。`Resources` に置くのは、
    /// インスペクタ結線がプレハブ作り直しで消えるのを避けるため。
    /// </summary>
    static void BuildHorrorFigurePrefab(Mats mats, StringBuilder log)
    {
        const string dir = "Assets/Resources/Prefabs";
        const string path = dir + "/HorrorFigure.prefab";

        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets/Resources", "Prefabs");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/Characters/Shadow_Rigged.fbx");
        if (model == null) { log.AppendLine("  ? Shadow_Rigged.fbx が無い"); return; }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
        instance.name = "HorrorFigure";

        foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
        {
            var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = mats.bodyShadow;
            r.sharedMaterials = slots;
        }

        // 出た瞬間に消えるので、動きは待機だけでよい
        var animator = instance.GetComponent<Animator>();
        if (animator == null) animator = instance.AddComponent<Animator>();
        animator.runtimeAnimatorController =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/Shadow_Locomotion.controller");
        animator.applyRootMotion = false;

        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        log.AppendLine($"  {path} を作成");
    }

    static Sprite[] LoadPhotoSprites(StringBuilder log)
    {
        var sprites = new List<Sprite>();

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PhotoTexDir })
                                          .OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) sprites.Add(sprite);
        }

        log.AppendLine($"  写真 {sprites.Count} 枚");
        return sprites.ToArray();
    }

    // ------------------------------------------------------------------
    static int PlaceInScene(string scenePath, int layer, Mats mats, Sprite[] photos, StringBuilder log)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var label = System.IO.Path.GetFileNameWithoutExtension(scenePath);

        // 作り直し方式
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;

        var walls = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude,
                                                            FindObjectsSortMode.None)
                          .Where(r => r.sharedMaterial != null &&
                                      (r.sharedMaterial.name.StartsWith("Mat_Walllime") ||
                                       r.sharedMaterial.name.StartsWith("Mat_Tile")))
                          .Where(r => r.bounds.size.y > 2.0f)
                          .OrderBy(r => r.bounds.center.z).ThenBy(r => r.bounds.center.x)
                          .ToList();

        if (walls.Count == 0)
        {
            log.AppendLine($"  {label}: 壁が見つからない");
            return 0;
        }

        int count = 0;
        var mirror = PlaceMirror(walls, root, mats, layer, log, label);
        if (mirror != null) count++;

        var photo = PlacePhoto(walls, root, mats, photos, log, label);
        if (photo != null) count++;

        WireUp(mirror, photo, mats, photos, layer, log, label);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return count;
    }

    /// <summary>壁のどちら側が廊下かを NavMesh で判定する。M6/M11 と同じ考え方。</summary>
    static int CorridorSide(Bounds wall, Vector3 normal)
    {
        float floorY = wall.min.y + 0.1f;
        float best = float.MaxValue; int side = 0;
        for (int s = -1; s <= 1; s += 2)
        {
            var probe = new Vector3(wall.center.x, floorY, wall.center.z) + normal * (s * 0.9f);
            if (!NavMesh.SamplePosition(probe, out var hit, 1.2f, NavMesh.AllAreas)) continue;
            float d = Vector3.Distance(probe, hit.position);
            if (d < best) { best = d; side = s; }
        }
        return side;
    }

    /// <summary>
    /// 面を1枚作って壁に貼る。
    ///
    /// **Unity の Quad は -Z 側から見える。**
    /// 廊下側から見えるようにするには forward を壁の内側に向ける必要がある。
    /// 最初は outward を向けていて、鏡が壁の内側を向き、
    /// 廊下からは何も見えなかった（撮って初めて分かった）。
    /// 鏡面の法線は `MirrorReflection.SurfaceNormal`（= -forward）で取る。
    /// </summary>
    static GameObject MakePanel(string name, Transform parent, Vector3 position,
                                 Vector3 outward, Vector2 size, Material material, int layer = 0)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // 通行の邪魔をしない
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        go.layer = layer;
        return go;
    }

    static MirrorReflection PlaceMirror(List<MeshRenderer> walls, Transform root, Mats mats,
                                         int layer, StringBuilder log, string label)
    {
        // 廊下の中ほどの壁を選ぶ。端だと通りかからない可能性がある
        foreach (var wall in walls.Skip(walls.Count / 3))
        {
            var b = wall.bounds;
            var normal = b.size.x <= b.size.z ? Vector3.right : Vector3.forward;
            float thickness = normal == Vector3.right ? b.size.x : b.size.z;
            float width = normal == Vector3.right ? b.size.z : b.size.x;
            if (width < 1.6f) continue;

            int side = CorridorSide(b, normal);
            if (side == 0) continue;

            var outward = normal * side;
            // 壁からしっかり離す。ぴったり付けると、巾木や見切り（M6 が置く）が
            // 手前を横切って、額の上に線が乗る
            var pos = new Vector3(b.center.x, b.min.y + 1.45f, b.center.z)
                      + outward * (thickness * 0.5f + 0.06f);

            // 枠を先に、鏡面を少し手前に
            MakePanel("MirrorFrame", root, pos, outward, new Vector2(1.12f, 1.52f), mats.frame);
            var surface = MakePanel("Mirror", root, pos + outward * 0.012f, outward,
                                    new Vector2(1.0f, 1.4f), mats.mirrorNormal);

            var reflection = surface.AddComponent<MirrorReflection>();
            reflection.normalMaterial = mats.mirrorNormal;
            reflection.delayMaterial = mats.mirrorDelay;
            reflection.changeMaterial = mats.mirrorChange;
            reflection.bodyNormalMaterial = mats.bodyNormal;
            reflection.bodyShadowMaterial = mats.bodyShadow;

            log.AppendLine($"  {label}: 鏡を {pos} に配置");
            return reflection;
        }

        log.AppendLine($"  {label}: 鏡を置ける壁が無い");
        return null;
    }

    static SpriteRenderer PlacePhoto(List<MeshRenderer> walls, Transform root, Mats mats,
                                      Sprite[] photos, StringBuilder log, string label)
    {
        if (photos.Length == 0) return null;

        // 鏡とは別の壁に。並べると「仕掛けの置き場」に見える
        foreach (var wall in walls.Take(walls.Count / 3))
        {
            var b = wall.bounds;
            var normal = b.size.x <= b.size.z ? Vector3.right : Vector3.forward;
            float thickness = normal == Vector3.right ? b.size.x : b.size.z;
            float width = normal == Vector3.right ? b.size.z : b.size.x;
            if (width < 1.4f) continue;

            int side = CorridorSide(b, normal);
            if (side == 0) continue;

            var outward = normal * side;
            var pos = new Vector3(b.center.x, b.min.y + 1.62f, b.center.z)
                      + outward * (thickness * 0.5f + 0.06f);

            MakePanel("PhotoFrame", root, pos, outward, new Vector2(0.82f, 0.64f), mats.frame);

            // 写真は SpriteRenderer。HorrorEventSystem が sprite を差し替える作りなので合わせる
            var go = new GameObject("Photo");
            go.transform.SetParent(root, false);
            go.transform.position = pos + outward * 0.012f;
            go.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = photos[0];
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.74f, 0.56f);
            // 暗い廊下で白飛びしないよう少し落とす
            sr.color = new Color(0.86f, 0.84f, 0.80f);

            log.AppendLine($"  {label}: 写真を {pos} に配置");
            return sr;
        }

        log.AppendLine($"  {label}: 写真を置ける壁が無い");
        return null;
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// `HorrorEventSystem` に繋ぐ。あちらはシーンをまたいで常駐するので、
    /// シーンを開くたびに繋ぎ直す必要がある。
    /// 実行時は `HorrorPropBinder` が同じことをする。
    /// </summary>
    static void WireUp(MirrorReflection mirror, SpriteRenderer photo, Mats mats,
                       Sprite[] photos, int layer, StringBuilder log, string label)
    {
        // 鏡にだけ映る体をプレイヤーに付ける
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc != null && mirror != null)
        {
            var existing = pc.transform.Find("MirrorBody");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Models/Characters/Patient_Rigged.fbx");
            GameObject body;
            if (model != null)
            {
                body = (GameObject)PrefabUtility.InstantiatePrefab(model, pc.transform);
            }
            else
            {
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(pc.transform, false);
            }

            body.name = "MirrorBody";
            // 足元を合わせる。CharacterController の原点は中心なので下げる
            body.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            body.transform.localRotation = Quaternion.identity;

            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
            {
                var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = mats.bodyNormal;
                r.sharedMaterials = slots;
            }

            mirror.mirrorBody = body;

            // 本カメラからは鏡専用レイヤーを外す。
            // これを忘れると、一人称の視界に自分の体が突き刺さる
            var cam = pc.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.cullingMask &= ~(1 << layer);
                EditorUtility.SetDirty(cam);
            }
            log.AppendLine($"  {label}: 鏡に映る体を配置（レイヤー {layer}）");
        }

        // シーン内に常駐システムが居るとは限らないので、
        // 実行時に繋ぐための部品をシーンに置く
        var binderGo = GameObject.Find("HorrorPropBinder");
        if (binderGo == null) binderGo = new GameObject("HorrorPropBinder");
        var binder = binderGo.GetComponent<HorrorPropBinder>();
        if (binder == null) binder = binderGo.AddComponent<HorrorPropBinder>();

        binder.mirrorRenderer = mirror != null ? mirror.GetComponent<Renderer>() : null;
        binder.mirrorNormalMat = mats.mirrorNormal;
        binder.mirrorDelayMat = mats.mirrorDelay;
        binder.mirrorChangeMat = mats.mirrorChange;
        binder.photoRenderer = photo;
        binder.photoVariants = photos;
        EditorUtility.SetDirty(binder);
    }
}
