using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M3: 壁パネルの高さ不揃いを直す。
///
/// 特定の経緯:
///   プレイ画面の「暗い廊下に浮く白い矩形」の正体を、オブジェクトID描画で集計したところ
///   明るいピクセルの 100% が PackArch_1F/P_Wall_02/Wall_02（漆喰 Mat_Walllime01_C）だった。
///   配置を調べると P_Wall_02 は高さ 7.62m、P_Wall_01 は 4.59m と揃っていない。
///   2026-05 の「壁を引き伸ばして隙間を塞ぐ」処理で伸ばしすぎたものと思われる。
///
/// 高さを揃えれば、他の壁から突き出て見えていた部分が消えるはず。
/// 効果はスクリーンショットで確認すること（run_playtest.ps1）。
/// 気に入らなければ `git checkout -- Assets/Scenes/` で戻せる。
/// </summary>
public static class M3WallHeightFix
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    const string MarkerName = "__WallHeightFixApplied";

    [MenuItem("消灯/M3: 壁の高さを揃える")]
    public static void RunBatch()
    {
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            if (GameObject.Find(MarkerName) != null)
            {
                Debug.Log($"[M3WallHeightFix] {label}: 適用済み");
                continue;
            }

            var panels = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                               .Where(t => t.name.StartsWith("P_Wall"))
                               .Select(t => (t, b: BoundsOf(t)))
                               .Where(x => x.b.HasValue)
                               .Select(x => (x.t, b: x.b.Value))
                               .ToList();

            if (panels.Count == 0)
            {
                Debug.LogWarning($"[M3WallHeightFix] {label}: 壁パネルが見つかりません");
                continue;
            }

            // 最も多い高さを「正しい高さ」とみなす（多数決）
            var target = panels.GroupBy(p => Mathf.Round(p.b.size.y * 100f) / 100f)
                               .OrderByDescending(g => g.Count())
                               .First().Key;

            int fixedCount = 0;
            foreach (var (t, bounds) in panels)
            {
                float height = bounds.size.y;
                if (height <= 0.01f) continue;
                if (Mathf.Abs(height - target) < 0.05f) continue;   // 既に揃っている

                float factor = target / height;
                float bottomBefore = bounds.min.y;

                var scale = t.localScale;
                t.localScale = new Vector3(scale.x, scale.y * factor, scale.z);

                // 床の高さが変わらないよう、縮めた分だけ位置を戻す
                var after = BoundsOf(t);
                if (after.HasValue)
                {
                    var p = t.position;
                    t.position = new Vector3(p.x, p.y + (bottomBefore - after.Value.min.y), p.z);
                }

                EditorUtility.SetDirty(t);
                fixedCount++;
            }

            new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[M3WallHeightFix] {label}: 基準高さ {target:F2}m / {fixedCount} 枚を修正（全 {panels.Count} 枚）");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[M3WallHeightFix] 完了");
    }

    static Bounds? BoundsOf(Transform t)
    {
        var renderers = t.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return null;

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
