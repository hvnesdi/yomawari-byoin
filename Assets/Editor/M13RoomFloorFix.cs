using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// M13: 病室の床を歩けるようにする。
///
/// 1F の病室に入れなかった。設計（CLAUDE.md）では「薄暗い病室で目が覚める」
/// ところから始まるのに、病室の中に NavMesh が1点も無く、開始位置に使えなかった。
///
/// 実測して分かったこと（RoomDiagnostics）:
///   - 床のメッシュは在る。向きも上向きで正しい。層も静的フラグも問題無い
///   - なのに部屋の中 143 点すべてで NavMesh が引けない（2F の同じ部屋は 75/143）
///   - 1F の病室の床は `P_Floor_01`、**厚さ 0.00 の板**
///   - 2F の病室の床は `P_Floor_02`、厚さ 0.01
///   - `P_Floor_01` は他の場所では**天井として**使われている
///   - しかも同じ位置に4枚重なっている（描画も Z ファイティングしていたはず）
///
/// **つまり病室の床に天井用の板を敷いていた。** 厚みの無い板は
/// NavMesh のボクセル化で歩ける面として拾われない。
///
/// ここでやること:
///   1. 同じ位置に重なっている床を1枚だけ残す
///   2. 地面の高さに残った `P_Floor_01` を `P_Floor_02` に差し替える
///   3. NavMesh を焼き直して、病室の中で歩けるようになったか数える
///
/// 3 が要点。直した「つもり」で終わらせないために、同じ格子で測って報告する。
/// </summary>
public static class M13RoomFloorFix
{
    const string PackPrefabDir = "Assets/Dnk_Dev/HospitalHorrorPack/Prefab";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    /// <summary>
    /// 何も壊さずに「何をするつもりか」だけ出す。
    /// 床を消す処理なので、範囲を間違えると廊下ごと消えかねない。
    /// 先にこれで対象を数えてから本番を走らせること。
    /// </summary>
    static bool dryRun;

    [MenuItem("消灯/M13: 病室の床を調べる（変更しない）")]
    public static void DryRunBatch()
    {
        dryRun = true;
        RunBatch();
    }

    /// <summary>
    /// 「床の厚みが原因」という見立てを確かめる前に、もっと安い実験をする。
    ///
    /// NavMesh のベイクは、エージェント半径で内側に削った結果、面積が
    /// `minRegionArea` 未満になった region を捨てる。病室にベッドが2台入っていると、
    /// 削ったあとの歩ける面積がその閾値を割る可能性がある。
    /// これなら床を1枚も触らずに確かめられる。
    ///
    /// 下見で分かったこと: 「厚みの無い床」を機械的に差し替える案は駄目だった。
    /// 2F は既に歩けているのに 80 枚が該当してしまう。つまり厚みは原因ではない。
    /// </summary>
    [MenuItem("消灯/M13: 病室が狭すぎて捨てられていないか試す")]
    public static void ExperimentMinRegion()
    {
        var log = new StringBuilder("[M13実験] minRegionArea を 0 にして焼き直す\n");

        foreach (var path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);
            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface == null) continue;
            if (RoomMarkers().Count == 0) continue;

            log.AppendLine($"── {label}");
            CountWalkable(log, $"  現状 (minRegionArea={surface.minRegionArea})");

            float original = surface.minRegionArea;
            surface.minRegionArea = 0f;
            surface.BuildNavMesh();
            CountWalkable(log, "  minRegionArea=0");

            // シーンは保存しない。実験なので元に戻す
            surface.minRegionArea = original;
            surface.BuildNavMesh();
        }

        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    [MenuItem("消灯/M13: 病室の床を歩けるようにする")]
    public static void RunBatch()
    {
        var log = new StringBuilder(dryRun ? "[M13] 下見（変更しない）\n" : "[M13] 病室の床\n");

        var floor02 = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Floor_02.prefab");
        if (floor02 == null)
        {
            Debug.LogError("[M13] P_Floor_02.prefab が読めない");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);
            log.AppendLine($"── {label}");

            int before = CountWalkable(log, "  修正前");

            int removed = RemoveDuplicateFloors(log);
            int swapped = SwapThinFloors(floor02, log);

            if (dryRun || (removed == 0 && swapped == 0))
            {
                if (!dryRun) log.AppendLine("  直すところが無い");
                continue;
            }

            var surface = Object.FindFirstObjectByType<NavMeshSurface>();
            if (surface != null) surface.BuildNavMesh();

            int after = CountWalkable(log, "  修正後");
            log.AppendLine($"  重複削除 {removed} 枚 / 差し替え {swapped} 枚 / 歩ける点 {before} → {after}");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// 地面の高さにある薄い水平面のうち、**病室の近くのものだけ**を集める。
    ///
    /// 範囲を絞るのは安全のため。床を消す処理なので、シーン全体を対象にすると
    /// 判定を1つ間違えただけで廊下の床が消える。直したいのは病室だけなので、
    /// マーカーから水平 6m 以内に限る。
    /// </summary>
    static List<MeshRenderer> GroundFloors()
    {
        var rooms = RoomMarkers().Select(t => new Vector2(t.position.x, t.position.z)).ToList();
        if (rooms.Count == 0) return new List<MeshRenderer>();

        return Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(m => m.bounds.max.y < 0.5f && m.bounds.size.y < 0.6f)
                     .Where(m => m.bounds.size.x > 0.8f && m.bounds.size.z > 0.8f)
                     .Where(m => rooms.Any(r => Vector2.Distance(
                         new Vector2(m.bounds.center.x, m.bounds.center.z), r) < 6f))
                     .ToList();
    }

    static List<Transform> RoomMarkers() =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .Where(t => t.name.StartsWith("PatientRoom"))
              .Where(t => t.childCount == 0)
              .OrderBy(t => t.name).ToList();

    /// <summary>
    /// 消してよい単位を返す。`GetOutermostPrefabInstanceRoot` は使わない。
    /// 階層の組み方によっては建物全体を指しかねないため。
    /// 床タイル1枚に相当する `P_Floor_*` の階層までしか遡らない。
    /// </summary>
    static GameObject FloorTileRoot(MeshRenderer m)
    {
        var t = m.transform;
        while (t.parent != null && t.parent.name.StartsWith("P_Floor")) t = t.parent;
        if (t.name.StartsWith("P_Floor") || t.name.StartsWith("Floor")) return t.gameObject;
        return m.gameObject;
    }

    /// <summary>
    /// 同じ場所に重なっている床を1枚だけ残す。
    /// 4枚重なっていた。描画では Z ファイティングになり、
    /// NavMesh のボクセル化では面の内外が定まらない。
    /// </summary>
    static int RemoveDuplicateFloors(StringBuilder log)
    {
        var seen = new HashSet<(int, int, int)>();
        var doomed = new List<GameObject>();

        foreach (var m in GroundFloors().OrderBy(m => m.name))
        {
            var c = m.bounds.center;
            var key = (Mathf.RoundToInt(c.x * 10f), Mathf.RoundToInt(c.y * 10f), Mathf.RoundToInt(c.z * 10f));
            if (!seen.Add(key)) doomed.Add(FloorTileRoot(m));
        }
        doomed = doomed.Distinct().ToList();

        if (doomed.Count > 0)
            log.AppendLine($"  重なっていた床 {doomed.Count} 枚{(dryRun ? "（削除する予定）" : "を削除")}" +
                            $"　例: {string.Join(", ", doomed.Take(3).Select(g => g.name))}");
        if (dryRun) return doomed.Count;

        foreach (var go in doomed) Object.DestroyImmediate(go);
        return doomed.Count;
    }

    /// <summary>
    /// 厚みの無い床を `P_Floor_02` に差し替える。
    /// 厚さ 0 の板は NavMesh に拾われない（これが病室に入れなかった原因）。
    /// </summary>
    static int SwapThinFloors(GameObject floor02, StringBuilder log)
    {
        var thin = GroundFloors().Where(m => m.bounds.size.y < 0.005f).ToList();

        if (thin.Count > 0)
            log.AppendLine($"  厚みの無い床 {thin.Count} 枚{(dryRun ? "（差し替える予定）" : "")}" +
                            $"　例: {string.Join(", ", thin.Take(3).Select(m => FloorTileRoot(m).name))}");
        if (dryRun) return thin.Count;

        int swapped = 0;

        foreach (var m in thin)
        {
            var old = FloorTileRoot(m);
            var parent = old.transform.parent;
            var bounds = m.bounds;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(floor02, parent);
            // 元の板と同じ範囲を覆わせる。位置は中心、大きさは新しい床の実寸から求める
            inst.transform.position = old.transform.position;
            inst.transform.rotation = old.transform.rotation;
            inst.transform.localScale = old.transform.localScale;

            var newRenderer = inst.GetComponentInChildren<MeshRenderer>();
            if (newRenderer != null)
            {
                // 差し替え前後で覆う面積が変わらないよう、XZ の倍率を合わせる
                var got = newRenderer.bounds.size;
                if (got.x > 0.01f && got.z > 0.01f)
                {
                    var s = inst.transform.localScale;
                    inst.transform.localScale = new Vector3(
                        s.x * (bounds.size.x / got.x), s.y, s.z * (bounds.size.z / got.z));
                }
                // 上面の高さを元と揃える（沈むと壁の下に潜り、浮くと段差になる）
                var after = inst.GetComponentInChildren<MeshRenderer>().bounds;
                inst.transform.position += Vector3.up * (bounds.max.y - after.max.y);
            }

            inst.name = old.name + "_Nav";
            Object.DestroyImmediate(old);
            swapped++;
        }

        if (swapped > 0) log.AppendLine($"  厚みの無い床 {swapped} 枚を P_Floor_02 に差し替え");
        return swapped;
    }

    /// <summary>
    /// 病室マーカーの周りで歩ける点を数える。RoomDiagnostics と同じ格子。
    /// 「直した」ではなく「歩けるようになった」を報告するために要る。
    /// </summary>
    static int CountWalkable(StringBuilder log, string prefix)
    {
        var markers = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                            .Where(t => t.name.StartsWith("PatientRoom"))
                            .Where(t => t.childCount == 0)
                            .OrderBy(t => t.name).ToList();
        if (markers.Count == 0) return -1;

        int hit = 0, total = 0;
        foreach (var marker in markers)
        {
            var p = marker.position;
            for (float dx = -2.5f; dx <= 2.5f; dx += 0.5f)
            for (float dz = -3f; dz <= 3f; dz += 0.5f)
            {
                total++;
                if (NavMesh.SamplePosition(new Vector3(p.x + dx, 0.2f, p.z + dz),
                                           out _, 0.4f, NavMesh.AllAreas)) hit++;
            }
        }
        log.AppendLine($"{prefix}: 病室 {markers.Count} 室で歩ける点 {hit}/{total}");
        return hit;
    }
}
