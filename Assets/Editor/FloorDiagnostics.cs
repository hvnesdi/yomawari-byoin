using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 床に見える長方形の正体を調べる。
///
/// 汚しデカールは壁だけに貼っていて床には無い（`M5GrimePass` が
/// `bounds.size.y > 1.0f` で床と天井を除外している）。
/// なので床の長方形は別の何か。推測せず、床の上にある薄い物を数える。
/// </summary>
public static class FloorDiagnostics
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/診断: 床の上にある物を調べる")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[FloorDiag] 床の上の薄い物\n");

        foreach (var path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);
            log.AppendLine($"── {label}");

            // 床のすぐ上（0〜0.4m）にある、薄くて水平な物
            var flat = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None)
                             .Where(m => m.bounds.center.y > -0.2f && m.bounds.center.y < 0.4f)
                             .Where(m => m.bounds.size.y < 0.25f)
                             .Where(m => m.bounds.size.x > 0.2f && m.bounds.size.z > 0.2f)
                             .ToList();

            log.AppendLine($"   該当 {flat.Count} 個");

            foreach (var group in flat.GroupBy(m => Root(m.transform))
                                      .OrderByDescending(g => g.Count()).Take(8))
            {
                var sample = group.First();
                log.AppendLine($"   {group.Count(),5} 個  親={group.Key}  例: {sample.name} " +
                                $"大きさ={sample.bounds.size.x:F2}x{sample.bounds.size.z:F2} " +
                                $"y={sample.bounds.center.y:F3} " +
                                $"mat={(sample.sharedMaterial != null ? sample.sharedMaterial.name : "なし")}");
            }


            // P_Floor_02 の子 "Object" を個別に見る。
            // 床の暗い長方形の正体がこれだった（壁のマテリアルが当たっている）
            var objs = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None)
                             .Where(m => m.name == "Object")
                             .ToList();
            log.AppendLine($"   'Object'（P_Floor_02 の子）{objs.Count} 個");
            foreach (var g in objs.GroupBy(m => m.sharedMaterial != null ? m.sharedMaterial.name : "なし"))
            {
                var s0 = g.First();
                log.AppendLine($"      mat={g.Key} × {g.Count()} 個  " +
                                $"大きさ={s0.bounds.size.x:F2}x{s0.bounds.size.y:F2}x{s0.bounds.size.z:F2} " +
                                $"y={s0.bounds.center.y:F3}");
            }

            // 床そのものの明るさむら（ライトマップの継ぎ目）も疑うので、
            // 床メッシュがいくつのライトマップに分かれているかを見る
            var floors = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                                FindObjectsSortMode.None)
                               .Where(m => m.bounds.size.y < 0.4f)
                               .Where(m => m.bounds.center.y > -0.3f && m.bounds.center.y < 0.3f)
                               .Where(m => m.bounds.size.x > 1.5f && m.bounds.size.z > 1.5f)
                               .ToList();
            var indices = floors.Select(f => f.lightmapIndex).Distinct().OrderBy(i => i).ToList();
            log.AppendLine($"   床メッシュ {floors.Count} 枚 / ライトマップ番号 " +
                            string.Join(",", indices.Take(8)));
            var scales = floors.Select(f => f.lightmapScaleOffset.x).Distinct().Take(5)
                               .Select(v => v.ToString("F3"));
            log.AppendLine($"   ライトマップの縮尺（先頭5種）: {string.Join(", ", scales)}");
        }

        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static string Root(Transform t)
    {
        var top = t;
        while (top.parent != null) top = top.parent;
        return top.name;
    }
}
