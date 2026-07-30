using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// M6: 廊下に設備を置いて silhouette を崩す。
///
/// 照明・汚し・キャラクターを整えた後で残っていた最大の弱点は、廊下が
/// まっすぐな空の箱だったこと。市販のホラーは天井の配管や壁の設備で
/// 視線を止め、奥行きと生活の痕跡を作っている。
///
/// モデルは tools/blender/make_corridor_props.py で生成した4種。
///
/// 配置で一番大事なのは「廊下側がどちらか」の判定。
/// 壁の両面に置くと半分が壁の中に埋まる。汚しのデカールは平面なので
/// 両面に置いて逃げたが、立体物ではそうはいかない。
/// NavMesh 上に歩ける床がある側を廊下側とみなす。
/// </summary>
public static class M6CorridorDetailPass
{
    const string ModelDir = "Assets/Models/Props";
    const string MarkerName = "__CorridorDetailApplied";
    const string RootName = "CorridorDetail";
    const string LampPrefab = "Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Lamp.prefab";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M6: 廊下に設備を配置")]
    public static void RunBatch()
    {
        var models = new Dictionary<string, GameObject>();
        foreach (var name in new[] { "Pipe_Run", "Vent_Grille", "Wall_Sign", "Radiator", "Skirting", "Cornice",
                                      "Conduit_Run", "Access_Panel", "Outlet" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            if (prefab == null) { Debug.LogError($"[Detail] {name}.fbx が無い"); continue; }
            models[name] = prefab;
        }
        if (models.Count == 0)
        {
            Debug.LogError("[Detail] モデルが1つも読めない。先に make_corridor_props.py を実行すること");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // 設備ごとに材質を分ける。全部同じ金属マテリアルを共用していたので
        // 配管もラジエーターも案内表示も同じ材質に見えていた（M7SurfacePass が作る）
        var mats = new Dictionary<string, Material>
        {
            ["Pipe_Run"]    = FindMaterial("Prop_Pipe_Painted", "Mat_Bed_Metal_01"),
            ["Vent_Grille"] = FindMaterial("Prop_Vent_Galv", "Prop_ExtMetal", "Mat_Bed_Metal_01"),
            ["Wall_Sign"]   = FindMaterial("Prop_Sign_Plate", "Prop_Wood", "Mat_Bed_Metal_01"),
            ["Radiator"]    = FindMaterial("Prop_Radiator_Enamel", "Mat_Bed_Metal_01"),
            // 巾木と見切りは壁と同系の塗装。目立たせるものではない
            ["Skirting"]    = FindMaterial("Prop_Sign_Plate", "Prop_Wood"),
            ["Cornice"]     = FindMaterial("Prop_Sign_Plate", "Prop_Wood"),
            // 露出配線は金属、点検口とコンセントは塗装板
            ["Conduit_Run"]  = FindMaterial("Prop_Vent_Galv", "Prop_ExtMetal"),
            ["Access_Panel"] = FindMaterial("Prop_Sign_Plate", "Prop_Wood"),
            ["Outlet"]       = FindMaterial("Prop_Sign_Plate", "Prop_Wood"),
        };

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            // 作り直し方式。種類を足したときに反映されないのを避ける
            var oldRoot = GameObject.Find(RootName);
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);
            var oldMarker = GameObject.Find(MarkerName);
            if (oldMarker != null) Object.DestroyImmediate(oldMarker);

            var root = new GameObject(RootName).transform;
            int placed = PlaceDetails(root, models, mats);
            int lamps = path.EndsWith("HospitalBasement.unity")
                ? AddBasementLamps(root, mats.TryGetValue("Vent_Grille", out var lampMat) ? lampMat : null) : 0;

            new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Detail] {label}: 設備 {placed} 個 / 追加照明 {lamps} 基");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Detail] 完了");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Material FindMaterial(params string[] names)
    {
        foreach (var n in names)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{n} t:Material"))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.name == n) return m;
            }
        }
        Debug.LogWarning("[Detail] 金属マテリアルが見つからないので既定のまま使う");
        return null;
    }

    // ------------------------------------------------------------------
    static int PlaceDetails(Transform root, Dictionary<string, GameObject> models,
                             Dictionary<string, Material> mats)
    {
        // 壁パネルを集める。汚しパスと同じ判定
        var walls = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                          .Where(r => r.sharedMaterial != null &&
                                      (r.sharedMaterial.name.StartsWith("Mat_Walllime") ||
                                       r.sharedMaterial.name.StartsWith("Mat_Tile")))
                          .Where(r => r.bounds.size.y > 1.0f)
                          .OrderBy(r => r.bounds.center.z).ThenBy(r => r.bounds.center.x)
                          .ToList();

        int placed = 0;
        int wallIndex = 0;

        // 同種の設備が近接して並ばないよう、置いた位置を覚えて距離で弾く。
        // wallIndex % N だけで選ぶと、隣り合う壁パネルが連続で当たったときに
        // 同じ場所へ集中する（地下の右壁に案内表示が縦3枚重なった原因）。
        var lastPlaced = new Dictionary<string, List<Vector3>>();
        bool FarEnough(string kind, Vector3 pos, float minDistance)
        {
            if (!lastPlaced.TryGetValue(kind, out var list))
            {
                lastPlaced[kind] = new List<Vector3> { pos };
                return true;
            }
            foreach (var p in list)
                if (Vector3.Distance(p, pos) < minDistance) return false;
            list.Add(pos);
            return true;
        }

        foreach (var wall in walls)
        {
            var b = wall.bounds;

            Vector3 normal = b.size.x <= b.size.z ? Vector3.right : Vector3.forward;
            float thickness = normal == Vector3.right ? b.size.x : b.size.z;
            float width = normal == Vector3.right ? b.size.z : b.size.x;
            if (width < 1.0f) continue;

            // 廊下側を NavMesh で判定する。歩ける床がある側が廊下
            int side = CorridorSide(b, normal);
            if (side == 0) continue;

            var face = new Vector3(b.center.x, 0f, b.center.z) + normal * (side * (thickness * 0.5f + 0.01f));
            var outward = normal * side;
            // 壁面の法線が -Z を向くようにモデルを回す（Blender で -Y 面を表として作った）
            var faceRotation = LookRotationSafe(outward);

            wallIndex++;
            float h = Hash(b.center, 1.3f);

            // 換気口: 天井際。数を絞る
            if (wallIndex % 5 == 0 && models.ContainsKey("Vent_Grille"))
            {
                var pos = face + Vector3.up * (b.max.y - 0.45f);
                if (FarEnough("vent", pos, 7f))
                {
                    Place(models["Vent_Grille"], pos, faceRotation, root, Mat(mats, "Vent_Grille"), $"Vent_{placed}");
                    placed++;
                }
            }

            // 案内表示: 目線の高さ
            if (wallIndex % 7 == 3 && models.ContainsKey("Wall_Sign"))
            {
                var pos = face + Vector3.up * (b.min.y + 1.75f);
                if (FarEnough("sign", pos, 10f))
                {
                    Place(models["Wall_Sign"], pos, faceRotation, root, Mat(mats, "Wall_Sign"), $"Sign_{placed}");
                    placed++;
                }
            }

            // ラジエーター: 床際。幅が要るので広い壁だけ
            if (wallIndex % 6 == 1 && width > 2.0f && models.ContainsKey("Radiator"))
            {
                var pos = face + Vector3.up * (b.min.y + 0.42f);
                if (FarEnough("radiator", pos, 9f))
                {
                    Place(models["Radiator"], pos, faceRotation, root, Mat(mats, "Radiator"), $"Radiator_{placed}");
                    placed++;
                }
            }

            // 露出配線: 壁を縦に走る。古い建物は配線が後付けで露出している
            if (wallIndex % 9 == 4 && models.ContainsKey("Conduit_Run"))
            {
                var pos = face + Vector3.up * (b.min.y + 1.35f);
                if (FarEnough("conduit", pos, 12f))
                {
                    Place(models["Conduit_Run"], pos, faceRotation, root, Mat(mats, "Conduit_Run"), $"Conduit_{placed}");
                    placed++;
                }
            }

            // 点検口: 設備スペースへの開口
            if (wallIndex % 11 == 6 && models.ContainsKey("Access_Panel"))
            {
                var pos = face + Vector3.up * (b.min.y + 0.62f);
                if (FarEnough("panel", pos, 14f))
                {
                    Place(models["Access_Panel"], pos, faceRotation, root, Mat(mats, "Access_Panel"), $"Panel_{placed}");
                    placed++;
                }
            }

            // コンセント: 小さいが生活の痕跡になる
            if (wallIndex % 4 == 1 && models.ContainsKey("Outlet"))
            {
                var pos = face + Vector3.up * (b.min.y + 0.32f);
                if (FarEnough("outlet", pos, 5f))
                {
                    Place(models["Outlet"], pos, faceRotation, root, Mat(mats, "Outlet"), $"Outlet_{placed}");
                    placed++;
                }
            }

            // 巾木と見切り: 壁の全長に 1m 刻みで並べる。
            // 間引かない。実際の室内では途切れずに回っているので、
            // 抜けているとかえって不自然になる。
            var alongWall = normal == Vector3.right ? Vector3.forward : Vector3.right;
            int segments = Mathf.Max(1, Mathf.RoundToInt(width));
            float step = width / segments;

            for (int i = 0; i < segments; i++)
            {
                float offset = -width * 0.5f + step * (i + 0.5f);
                var basePos = face + alongWall * offset;

                if (models.ContainsKey("Skirting"))
                {
                    Place(models["Skirting"], basePos + Vector3.up * b.min.y, faceRotation,
                          root, Mat(mats, "Skirting"), $"Skirt_{placed}");
                    placed++;
                }
                if (models.ContainsKey("Cornice"))
                {
                    Place(models["Cornice"], basePos + Vector3.up * (b.max.y - 0.03f), faceRotation,
                          root, Mat(mats, "Cornice"), $"Cornice_{placed}");
                    placed++;
                }
            }

            // 配管: 天井際を廊下に沿って走らせる。壁の向きに合わせて伸ばす方向を決める
            if (wallIndex % 4 == 2 && models.ContainsKey("Pipe_Run") && width > 2.5f)
            {
                // 壁から少し離して天井直下に吊る
                var pos = face + outward * 0.18f + Vector3.up * (b.max.y - 0.22f)
                          - alongWall * (width * 0.5f);
                var rot = Quaternion.LookRotation(alongWall, Vector3.up);
                Place(models["Pipe_Run"], pos, rot, root, Mat(mats, "Pipe_Run"), $"Pipe_{placed}");
                placed++;
            }
        }

        // 種類ごとの内訳を出す。合計だけを見ていたら判断を誤るところだった。
        // 1F の合計が 2F の 1/4 で「1F の設備が抜けている」ように見えたが、
        // 内訳を見れば大半が巾木と回り縁（壁の長さに比例する）で、
        // 1F は単に廊下が短いだけだと分かる。合計は密度の指標にならない。
        var tally = new Dictionary<string, int>();
        foreach (Transform child in root)
        {
            int cut = child.name.LastIndexOf('_');
            var kind = cut > 0 ? child.name.Substring(0, cut) : child.name;
            tally.TryGetValue(kind, out int n);
            tally[kind] = n + 1;
        }
        Debug.Log("[Detail]   内訳: " + string.Join(" / ",
                  tally.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));

        return placed;
    }

    /// <summary>
    /// 壁のどちら側が廊下かを NavMesh で判定する。
    /// 戻り値 +1 / -1 が法線方向の符号。判定できなければ 0。
    /// </summary>
    static int CorridorSide(Bounds wall, Vector3 normal)
    {
        float floorY = wall.min.y + 0.1f;
        float bestDistance = float.MaxValue;
        int best = 0;

        for (int s = -1; s <= 1; s += 2)
        {
            var probe = new Vector3(wall.center.x, floorY, wall.center.z) + normal * (s * 0.8f);
            if (!NavMesh.SamplePosition(probe, out var hit, 1.2f, NavMesh.AllAreas)) continue;

            float d = Vector3.Distance(probe, hit.position);
            if (d < bestDistance) { bestDistance = d; best = s; }
        }
        return best;
    }

    static void Place(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent,
                       Material metal, string name)
    {
        var holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.position = position;
        holder.transform.rotation = rotation;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder.transform);
        instance.transform.localPosition = Vector3.zero;
        // localRotation / localScale は触らない。
        // FBX インポーターが Blender(Z-up・m) と Unity(Y-up・cm) の変換をそこに入れている。
        // 上書きすると 1/100 サイズになったり床に倒れたりする（実際に両方踏んだ）

        if (metal != null)
        {
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < slots.Length; i++) slots[i] = metal;
                r.sharedMaterials = slots;
            }
        }
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// 地下に照明器具を足す。点光源だけで、光源として見えるものが無かった。
    /// </summary>
    static int AddBasementLamps(Transform root, Material metal)
    {
        var lamp = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab);
        if (lamp == null) { Debug.LogWarning("[Detail] P_Lamp が無い"); return 0; }

        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null || surface.navMeshData == null)
        {
            Debug.LogWarning("[Detail] 地下の NavMesh が無いので照明を置けない");
            return 0;
        }

        // 天井高は壁パネルの上端から取る
        var wall = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                         .Where(r => r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("Mat_Wall"))
                         .Where(r => r.bounds.size.y > 1.0f)
                         .OrderByDescending(r => r.bounds.max.y)
                         .FirstOrDefault();
        float ceiling = wall != null ? wall.bounds.max.y - 0.12f : 2.7f;

        var bounds = surface.navMeshData.sourceBounds;
        int count = 0;

        // 歩ける床の上を一定間隔で辿り、天井に器具を吊る
        for (float z = bounds.min.z + 2f; z <= bounds.max.z - 2f; z += 5.5f)
        {
            for (float x = bounds.min.x + 2f; x <= bounds.max.x - 2f; x += 6.5f)
            {
                // 床の高さで探ること。bounds.center.y は空間の中心で床から
                // 数メートル離れており、探索距離 2.5m では届かず 0 件になっていた
                var probe = new Vector3(x, bounds.min.y + 0.5f, z);
                if (!NavMesh.SamplePosition(probe, out var hit, 4f, NavMesh.AllAreas)) continue;

                var holder = new GameObject($"BasementLamp_{count}");
                holder.transform.SetParent(root, false);
                holder.transform.position = new Vector3(hit.position.x, ceiling, hit.position.z);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(lamp, holder.transform);
                instance.transform.localPosition = Vector3.zero;

                // 半分は切れた状態にする。地下は最も荒れている
                bool alive = Hash(holder.transform.position, 4.7f) > 0.5f;
                if (alive)
                {
                    var lightGo = new GameObject("Light");
                    lightGo.transform.SetParent(holder.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, -0.18f, 0f);
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(0.82f, 0.86f, 1f);
                    light.intensity = 1.1f;
                    light.range = 7f;
                    light.shadows = LightShadows.Soft;
                    lightGo.AddComponent<LightFlicker>();
                    lightGo.AddComponent<LightBaseIntensity>().baseIntensity = 1.1f;
                }

                count++;
            }
        }
        return count;
    }

    static Material Mat(Dictionary<string, Material> mats, string key)
        => mats.TryGetValue(key, out var m) ? m : null;

    static Quaternion LookRotationSafe(Vector3 forward)
        => forward.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(forward, Vector3.up);

    static float Hash(Vector3 p, float salt)
    {
        float v = Mathf.Sin(p.x * 12.9898f + p.y * 4.1414f + p.z * 78.233f + salt) * 43758.5453f;
        return Mathf.Abs(v - Mathf.Floor(v));
    }
}
