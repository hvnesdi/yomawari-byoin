using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M4: Hospital Horror Pack の未使用アセットを活かす。
///
/// パックは既にプロジェクトに入っており壁・床・ベッド・ドアは使っているが、
/// P_Lamp と P_Ceiling_01 が未使用のまま残っていた。
/// 一方で照明器具は自作の直方体（FL_Cover / FL_Metal / FluorTube）で代用している。
/// 暗い廊下では照明が視線の集まる場所なので、ここをパックのモデルに差し替える。
///
/// 元の器具は消さずに無効化するだけにしてある。位置合わせが合わなかったときに
/// 戻せるようにするため。
/// </summary>
public static class M4PackAssetPass
{
    const string LampPrefab = "Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Lamp.prefab";
    const string MarkerName = "__PackLampsApplied";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M4: 照明をパックのモデルに差し替える")]
    public static void RunBatch()
    {
        var lamp = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab);
        if (lamp == null)
        {
            Debug.LogError($"[PackAssets] {LampPrefab} が見つかりません");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }
        LogPrefabBounds(lamp);

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            if (GameObject.Find(MarkerName) != null)
            {
                Debug.Log($"[PackAssets] {label}: 適用済み");
                continue;
            }

            // 自作の蛍光灯プロップを探す。prop_fluorescent_* という名前で作られている
            var fixtures = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                                 .Where(t => t.name.StartsWith("prop_fluorescent"))
                                 .ToList();

            int replaced = 0;
            foreach (var fixture in fixtures)
            {
                // 既に差し替え済みならとばす
                if (fixture.Find("PackLamp") != null) continue;

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(lamp, fixture);
                instance.name = "PackLamp";
                instance.transform.localPosition = Vector3.zero;
                // localRotation / localScale はインポーターの補正なので触らない
                // （FBX の単位・軸変換がここに入っている。上書きすると倒れたり縮んだりする）

                // 元の自作器具は消さずに描画だけ止める。位置が合わなければ戻せるように
                foreach (var r in fixture.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.transform.IsChildOf(instance.transform)) continue;
                    r.enabled = false;
                    EditorUtility.SetDirty(r);
                }

                replaced++;
            }

            if (replaced > 0)
            {
                new GameObject(MarkerName);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[PackAssets] {label}: 照明 {replaced} 基を差し替え（器具候補 {fixtures.Count}）");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[PackAssets] 完了");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>差し替え先の寸法を出しておく。位置合わせが要るかの判断材料。</summary>
    static void LogPrefabBounds(GameObject prefab)
    {
        var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) { Debug.LogWarning("[PackAssets] P_Lamp にレンダラがありません"); return; }

        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        Debug.Log($"[PackAssets] P_Lamp: レンダラ{renderers.Length}個 size={b.size} center={b.center} " +
                  $"rootScale={prefab.transform.localScale}");
    }
}
