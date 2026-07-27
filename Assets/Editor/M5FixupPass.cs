using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M5: 撮影して見つかった2件を直す。どちらも ID 描画で正体を確定させたもの。
///
/// 1. 暴走した小道具の部品
///    3F の右壁を覆っていた白い板の正体は、車椅子プロップの子メッシュ
///    prop_wheelchair/Cylinder.015 だった（画面の 59,283px を占有）。
///    小道具の部品が壁サイズまで巨大化している。他にも同種のものがある可能性が
///    高いので、名前で決め打ちせず「小道具の中で異常に大きい子」を探して止める。
///
/// 2. 汚しが見えない
///    デカールは描画されている（左壁でカビが 25,291px）。枚数ではなく
///    コントラストの問題だった。_BaseColor が 0.78 の明るい灰色で、
///    元から薄いテクスチャをさらに白く寄せていた。種類ごとに暗く色を付ける。
/// </summary>
public static class M5FixupPass
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    /// <summary>小道具の子としてあり得ない大きさ。これを超えたら暴走とみなす。</summary>
    const float RunawaySize = 3.5f;

    [MenuItem("消灯/M5: 撮影で見つかった不具合を直す")]
    public static void RunBatch()
    {
        TintGrimeMaterials();

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);
            int stopped = StopRunawayPropMeshes(label);

            if (stopped > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[Fixup] {label}: 暴走した小道具の部品 {stopped} 個を停止");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Fixup] 完了");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------
    static int StopRunawayPropMeshes(string label)
    {
        int stopped = 0;
        var log = new StringBuilder();

        // Props_* / ExtraProps_* 配下の小道具を対象にする
        var propRoots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                              .Where(t => t.name.StartsWith("prop_"))
                              .ToList();

        foreach (var prop in propRoots)
        {
            var renderers = prop.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length < 2) continue;

            // 同じ小道具の中で、他の部品と比べて桁違いに大きい子を探す
            var sizes = renderers.Select(r => r.bounds.size.magnitude).ToList();
            float median = sizes.OrderBy(s => s).ElementAt(sizes.Count / 2);

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                var size = r.bounds.size;
                float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));

                if (longest < RunawaySize) continue;
                if (median > 0.01f && sizes[i] < median * 6f) continue;   // 全体が大きい小道具は除外

                r.enabled = false;
                EditorUtility.SetDirty(r);
                stopped++;
                log.AppendLine($"    {prop.name}/{r.name} size={size} (中央値 {median:F2}) を停止");
            }
        }

        if (stopped > 0) Debug.Log($"[Fixup] {label} 詳細:\n{log}");
        return stopped;
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// 汚しに色と濃さを与える。
    /// _BaseColor は 0.78 の明るい灰色のままで、元から薄いテクスチャを
    /// さらに白へ寄せていた。汚れは周囲より暗くないと汚れに見えない。
    /// </summary>
    static void TintGrimeMaterials()
    {
        var tints = new (string name, Color tint)[]
        {
            ("Decal_Water_01",   new Color(0.30f, 0.26f, 0.20f)),   // 錆混じりの水染み
            ("Decal_Mold_01",    new Color(0.20f, 0.24f, 0.17f)),   // 黒カビ
            ("Decal_Mold_02",    new Color(0.18f, 0.21f, 0.16f)),
            ("Decal_Scratch_01", new Color(0.22f, 0.22f, 0.24f)),   // 引っかき傷
            ("Decal_Blood_01",   new Color(0.32f, 0.06f, 0.05f)),   // 古い血
            ("Decal_Blood_02",   new Color(0.28f, 0.05f, 0.04f)),
        };

        foreach (var (name, tint) in tints)
        {
            var path = $"Assets/Materials/Decals/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { Debug.LogWarning($"[Fixup] {path} が無い"); continue; }

            var c = tint; c.a = 1f;
            mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            mat.SetFloat("_Smoothness", 0.05f);   // 汚れは光らない
            EditorUtility.SetDirty(mat);
            Debug.Log($"[Fixup] {name} を暗く: {tint}");
        }
    }
}
