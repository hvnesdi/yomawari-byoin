using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// M11: Poly Haven の CC0 小物を配置する。
///
/// これまでの小道具は全て手続き生成（プリミティブ合成か Blender スクリプト）で、
/// 情報量に限界があった。実写スキャンの小物なら、へこみ・傷・汚れが最初から入っている。
///
/// テクスチャ差し替えでは実寸の不一致で失敗したが（M10 参照）、小物は違う。
/// 椅子や棚は「その物体の大きさ」で作られているので、タイリングの問題が起きない。
/// スキャン素材が噛み合うのはこういう用途。
///
/// 配置は床の上（NavMesh 上）に置き、壁際に寄せる。
/// 通路の中央に置くと敵の経路を塞ぐので、NavMesh を削らない位置を選ぶ。
/// </summary>
public static class M11PolyHavenPropsPass
{
    const string ModelDir = "Assets/Models/PolyHaven";
    const string RootName = "ScannedProps";

    /// <summary>
    /// 小物の定義。
    /// wallHug=true は壁際に寄せる（椅子・棚）。false は床の任意位置（箱・樽）。
    /// countPerScene はフロアあたりの目安。
    /// </summary>
    struct PropDef
    {
        public string id;
        public bool wallHug;
        public float minSpacing;
        public Dictionary<string, int> perScene;   // シーン名 → 個数
    }

    static readonly PropDef[] Props =
    {
        // 待合の椅子。1F と 2F に並べる
        new PropDef { id = "SchoolChair_01", wallHug = true, minSpacing = 1.2f,
                      perScene = new() { ["Hospital"] = 8, ["Hospital2F"] = 6 } },
        new PropDef { id = "plastic_monobloc_chair_01", wallHug = true, minSpacing = 1.4f,
                      perScene = new() { ["Hospital"] = 4, ["Hospital2F"] = 4, ["Hospital3F"] = 3 } },
        // スチール棚。地下の保管室が主戦場
        new PropDef { id = "steel_frame_shelves_01", wallHug = true, minSpacing = 3f,
                      perScene = new() { ["HospitalBasement"] = 7, ["Hospital3F"] = 2 } },
        new PropDef { id = "Shelf_01", wallHug = true, minSpacing = 3f,
                      perScene = new() { ["HospitalBasement"] = 4, ["Hospital2F"] = 2 } },
        // 「床が濡れています」の看板。無人の廊下に置くと効く
        new PropDef { id = "WetFloorSign_01", wallHug = false, minSpacing = 12f,
                      perScene = new() { ["Hospital"] = 2, ["Hospital2F"] = 2, ["Hospital3F"] = 2 } },
        // 医療用ワゴンとして使う
        new PropDef { id = "CoffeeCart_01", wallHug = true, minSpacing = 8f,
                      perScene = new() { ["Hospital2F"] = 2, ["Hospital3F"] = 2 } },
        // 地下の雑多な物
        new PropDef { id = "cardboard_box_01", wallHug = false, minSpacing = 1.5f,
                      perScene = new() { ["HospitalBasement"] = 10 } },
        new PropDef { id = "wooden_crate_02", wallHug = true, minSpacing = 2.5f,
                      perScene = new() { ["HospitalBasement"] = 5 } },
        new PropDef { id = "Barrel_02", wallHug = true, minSpacing = 2.5f,
                      perScene = new() { ["HospitalBasement"] = 4 } },
        // 病室の目覚まし時計
        new PropDef { id = "alarm_clock_01", wallHug = false, minSpacing = 6f,
                      perScene = new() { ["Hospital3F"] = 2 } },
    };

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M11: スキャン小物を配置")]
    public static void RunBatch()
    {
        var models = new Dictionary<string, GameObject>();
        foreach (var p in Props)
        {
            var prefab = FindFbx(p.id);
            if (prefab == null) { Debug.LogWarning($"[Props] {p.id} の fbx が無い"); continue; }
            models[p.id] = prefab;
        }
        if (models.Count == 0)
        {
            Debug.LogError("[Props] モデルが1つも読めない");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            // 作り直し方式
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;

            int placed = PlaceForScene(label, root, models);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Props] {label}: スキャン小物 {placed} 個");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Props] 完了");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static GameObject FindFbx(string id)
    {
        var folder = $"{ModelDir}/{id}";
        if (!AssetDatabase.IsValidFolder(folder)) return null;
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".fbx")) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }

    static int PlaceForScene(string sceneLabel, Transform root, Dictionary<string, GameObject> models)
    {
        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null || surface.navMeshData == null)
        {
            Debug.LogWarning($"[Props] {sceneLabel}: NavMesh が無いので配置できない");
            return 0;
        }
        var bounds = surface.navMeshData.sourceBounds;

        // 壁際に寄せるための壁一覧
        var walls = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                          .Where(r => r.sharedMaterial != null &&
                                      (r.sharedMaterial.name.StartsWith("Mat_Walllime") ||
                                       r.sharedMaterial.name.StartsWith("Mat_Tile")))
                          .Where(r => r.bounds.size.y > 1.0f)
                          .OrderBy(r => r.bounds.center.z).ThenBy(r => r.bounds.center.x)
                          .ToList();

        var used = new List<(Vector3 pos, float radius)>();
        bool Free(Vector3 pos, float radius)
        {
            foreach (var (p, r) in used)
                if (Vector3.Distance(p, pos) < radius + r) return false;
            used.Add((pos, radius));
            return true;
        }

        int placed = 0;
        int seed = 0;

        foreach (var def in Props)
        {
            if (!def.perScene.TryGetValue(sceneLabel, out int count)) continue;
            if (!models.TryGetValue(def.id, out var prefab)) continue;

            int made = 0, attempts = 0;
            while (made < count && attempts < count * 40)
            {
                attempts++;
                seed++;

                Vector3 pos;
                Quaternion rot;

                if (def.wallHug && walls.Count > 0)
                {
                    // 壁を1枚選び、その面に沿って床に置く
                    var wall = walls[(seed * 7 + made * 13) % walls.Count];
                    var b = wall.bounds;
                    Vector3 normal = b.size.x <= b.size.z ? Vector3.right : Vector3.forward;
                    float thickness = normal == Vector3.right ? b.size.x : b.size.z;
                    float width = normal == Vector3.right ? b.size.z : b.size.x;
                    if (width < 1.2f) continue;

                    int side = CorridorSide(b, normal, bounds);
                    if (side == 0) continue;

                    var along = normal == Vector3.right ? Vector3.forward : Vector3.right;
                    float t = (Hash(seed * 1.7f) - 0.5f) * (width - 0.8f);
                    // 壁から少し離す。ぴったり付けると壁に食い込んで見える
                    var candidate = new Vector3(b.center.x, 0f, b.center.z)
                                    + normal * (side * (thickness * 0.5f + 0.35f))
                                    + along * t;

                    if (!NavMesh.SamplePosition(candidate, out var hit, 1.2f, NavMesh.AllAreas)) continue;
                    pos = hit.position;
                    // 壁を背にする向き
                    rot = Quaternion.LookRotation(-normal * side, Vector3.up);
                }
                else
                {
                    // 床の任意位置。ただし通路中央を塞がないよう端寄りにする
                    float x = Mathf.Lerp(bounds.min.x + 1f, bounds.max.x - 1f, Hash(seed * 2.3f));
                    float z = Mathf.Lerp(bounds.min.z + 1f, bounds.max.z - 1f, Hash(seed * 3.1f));
                    if (!NavMesh.SamplePosition(new Vector3(x, bounds.min.y + 0.5f, z),
                                                out var hit, 3f, NavMesh.AllAreas)) continue;
                    pos = hit.position;
                    rot = Quaternion.Euler(0f, Hash(seed * 5.9f) * 360f, 0f);
                }

                if (!Free(pos, def.minSpacing)) continue;

                var holder = new GameObject($"{def.id}_{made}");
                holder.transform.SetParent(root, false);
                holder.transform.position = pos;
                holder.transform.rotation = rot;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
                inst.transform.localPosition = Vector3.zero;
                // localRotation / localScale は触らない（FBX インポーターの補正が入っている）

                made++;
                placed++;
            }

            if (made < count)
                Debug.Log($"[Props] {sceneLabel}: {def.id} は {made}/{count} 個（置ける場所が足りない）");
        }
        return placed;
    }

    /// <summary>壁のどちら側が廊下かを NavMesh で判定する。M6 と同じ考え方。</summary>
    static int CorridorSide(Bounds wall, Vector3 normal, Bounds navBounds)
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

    static float Hash(float seed)
    {
        float v = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
        return Mathf.Abs(v - Mathf.Floor(v));
    }
}
