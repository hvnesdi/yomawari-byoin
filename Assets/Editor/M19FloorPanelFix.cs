using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M19: 床の中央パネルに床のマテリアルを当てる。
///
/// 「床に長方形が見える」という指摘を追ったら、汚しデカールでも
/// ライトマップの継ぎ目でもなかった。
///
/// パックの `P_Floor_02` は2つのメッシュでできている:
///   `Floor_02` … 外周（`Mat_Tile01`）
///   `Object`   … 中央のパネル（厚さ0・床面より 10cm 低い）
/// この `Object` に**壁のマテリアル**（`Mat_Walllime01_C`）が当たっていた。
/// 床タイルの真ん中だけ壁材、という状態で、
/// 明るさが違うぶん長方形として浮いていた。1F に26枚、地下に50枚。
///
/// 直し方は「隣の床メッシュと同じ材質にする」。
/// 決め打ちで `Mat_Tile01` を入れないのは、フロアによって床材が違う場合に
/// 追従できなくなるため。同じプレハブ内の床メッシュから引いてくる。
///
/// 特定に使った手順（同じことをするとき用）:
///   1. ID 描画と通常描画を2枚保存して見比べ、ジオメトリだと確定
///   2. 色→名前のパレットをログに出し、暗い画素の座標から名前を逆引き
/// </summary>
public static class M19FloorPanelFix
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M19: 床の中央パネルを直す")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M19] 床の中央パネル\n");

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            int fixedCount = 0, skipped = 0;

            foreach (var panel in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                                         FindObjectsSortMode.None)
                                        .Where(m => m.name == "Object")
                                        .Where(m => m.transform.parent != null &&
                                                    m.transform.parent.name.StartsWith("P_Floor")))
            {
                // 同じプレハブの中にある床メッシュの材質を借りる
                var sibling = panel.transform.parent
                                   .GetComponentsInChildren<MeshRenderer>(true)
                                   .FirstOrDefault(r => r != panel && r.name.StartsWith("Floor"));

                if (sibling == null || sibling.sharedMaterial == null) { skipped++; continue; }

                // 材質と高さは**別々に**判定する。
                // 「材質が既に正しければ次へ」と書いていたら、
                // 一度直した後は高さの修正に永久に到達しなかった
                bool changed = false;

                if (panel.sharedMaterial != sibling.sharedMaterial)
                {
                    var slots = new Material[Mathf.Max(1, panel.sharedMaterials.Length)];
                    for (int i = 0; i < slots.Length; i++) slots[i] = sibling.sharedMaterial;
                    panel.sharedMaterials = slots;
                    changed = true;
                }

                // 高さも揃える。パネルは床面より 10.4cm 低く、窪みの縁が
                // 細い線として残っていた。材質を直しただけでは線が消えない。
                // 2mm だけ下げて置くのは、外周メッシュと重なった場合の
                // Z ファイティングを避けるため
                float gap = sibling.bounds.max.y - 0.002f - panel.bounds.max.y;
                if (Mathf.Abs(gap) > 0.003f)
                {
                    panel.transform.position += Vector3.up * gap;
                    EditorUtility.SetDirty(panel.transform);
                    changed = true;
                }

                if (!changed) continue;
                EditorUtility.SetDirty(panel);
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            log.AppendLine($"  {label}: {fixedCount} 枚を床の材質に" +
                            (skipped > 0 ? $"（{skipped} 枚は隣の床が見つからず据え置き）" : ""));
        }

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
