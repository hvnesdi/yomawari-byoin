using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 照明の位置を実測する。
///
/// 「蛍光灯の位置がおかしい」という指摘を受けて、推測で直す前に数える。
/// 見るのは3つ:
///   - 器具（メッシュ）と Light の高さがずれていないか
///   - 天井に対して高すぎ／低すぎないか
///   - Light が器具の中に居るか（離れていると、器具が影になって浮く）
///
/// 特に「Light が器具より上」だと、天井だけが明るくなって器具が黒く沈む。
/// 実際の蛍光灯は下を照らすので、逆さまの画になる。
/// </summary>
public static class LightDiagnostics
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/診断: 照明の位置を測る")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[LightDiag] 照明の位置\n");

        foreach (var path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);
            log.AppendLine($"── {label}");

            // 天井の高さ。水平で薄く、高い位置にある面
            var ceilings = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None)
                                 .Where(m => m.bounds.size.y < 0.8f)
                                 .Where(m => m.bounds.size.x > 1.5f && m.bounds.size.z > 1.5f)
                                 .Where(m => m.bounds.center.y > 2.0f)
                                 .ToList();
            float ceilingY = ceilings.Count > 0
                ? ceilings.Select(m => m.bounds.min.y).OrderBy(v => v).ElementAt(ceilings.Count / 2)
                : -1f;
            log.AppendLine($"   天井の下面 y={ceilingY:F2}（{ceilings.Count} 枚から中央値）");

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include,
                                                          FindObjectsSortMode.None)
                               .Where(l => l.type == LightType.Point || l.type == LightType.Spot)
                               .Where(l => l.transform.position.y > 1.5f)
                               .ToList();

            if (lights.Count == 0) { log.AppendLine("   天井灯が無い"); continue; }

            var heights = lights.Select(l => l.transform.position.y).OrderBy(v => v).ToList();
            log.AppendLine($"   Light {lights.Count} 個 / 高さ 最小{heights.First():F2} " +
                            $"中央{heights[heights.Count / 2]:F2} 最大{heights.Last():F2}");
            if (ceilingY > 0)
                log.AppendLine($"   天井からの下がり: 中央 {ceilingY - heights[heights.Count / 2]:F2}m " +
                                $"（負なら天井より上＝天井裏に埋まっている）");

            // 器具のメッシュ。名前で拾う
            var fixtures = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None)
                                 .Where(m => m.transform.root.name.Contains("fluorescent") ||
                                             m.name.Contains("FL_") || m.name.Contains("Lamp") ||
                                             m.transform.parent != null &&
                                             m.transform.parent.name.Contains("Lamp"))
                                 .ToList();
            log.AppendLine($"   器具メッシュ {fixtures.Count} 個");

            // それぞれの Light について、最寄りの器具との位置関係
            int above = 0, below = 0, far = 0;
            float sumDy = 0f;
            var samples = new StringBuilder();

            foreach (var light in lights)
            {
                var lp = light.transform.position;
                MeshRenderer nearest = null;
                float best = float.MaxValue;
                foreach (var f in fixtures)
                {
                    float d = Vector3.Distance(f.bounds.center, lp);
                    if (d < best) { best = d; nearest = f; }
                }
                if (nearest == null || best > 3f) { far++; continue; }

                float dy = lp.y - nearest.bounds.center.y;
                sumDy += dy;
                if (dy > 0.05f) above++;
                else if (dy < -0.05f) below++;

                if (samples.Length < 400)
                    samples.AppendLine($"      light y={lp.y:F2} 器具 y={nearest.bounds.center.y:F2} " +
                                        $"差={dy:+0.00;-0.00} 距離={best:F2} ({nearest.name})");
            }

            int matched = lights.Count - far;
            log.AppendLine($"   器具と対応 {matched} 個 / 対応なし {far} 個");
            if (matched > 0)
                log.AppendLine($"   Light が器具より **上** {above} 個 / **下** {below} 個 / " +
                                $"平均の差 {sumDy / matched:+0.00;-0.00}m");
            log.Append(samples);
        }

        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
