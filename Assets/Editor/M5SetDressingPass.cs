using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M5: 廊下を「均一に明るい空の箱」から脱却させる。
///
/// 照明とポストプロセスを整えた結果、次に目立つのは廊下そのものの単調さだった。
/// 市販のホラーは光の溜まりと暗がりのリズムで空間を作っている。均等な間隔で
/// 同じ明るさの蛍光灯が並んでいると、どれだけ暗くしても「暗いだけの廊下」になる。
///
/// やること:
///   1. 切れた蛍光灯を作る。数本を完全に消して暗がりの区間を作る
///   2. 生きている蛍光灯は逆に強くする。明暗差が空間の奥行きを作る
///   3. 天井を床プレハブの流用から専用プレハブに置き換える
/// </summary>
public static class M5SetDressingPass
{
    const string CeilingPrefab = "Assets/Dnk_Dev/HospitalHorrorPack/Prefab/P_Ceiling_01.prefab";
    const string MarkerName = "__SetDressingApplied";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    /// <summary>フロアごとの「切れている蛍光灯」の割合。下に降りるほど荒れている。</summary>
    static float DeadRatio(string path)
    {
        if (path.EndsWith("HospitalBasement.unity")) return 0.55f;
        if (path.EndsWith("Hospital3F.unity")) return 0.42f;
        if (path.EndsWith("Hospital2F.unity")) return 0.30f;
        return 0.20f;   // 1F はチュートリアル。暗すぎると操作を覚えられない
    }

    /// <summary>
    /// 生き残った蛍光灯を強くする倍率。M3 のフロアごとの倍率に掛ける。
    ///
    /// 1.45 から下げた。プレイ画面を実測したら平均輝度 0.419 / 暗部(&lt;0.15) 0.6% で、
    /// 「消灯」という題のゲームなのに昼間の事務所のような明るさだった。
    /// 明暗差は光源を強くするより、切る本数と暗い ambient で作るほうが効く。
    /// </summary>
    const float SurvivorBoost = 1.15f;

    [MenuItem("消灯/M5: 廊下の明暗リズムを作る")]
    public static void RunBatch()
    {
        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            // 以前はマーカーがあれば丸ごと飛ばしていたが、それだと
            // 明るさの値を変えても2回目以降に反映されない。
            // 計算は記録した元の強度からやり直すので、毎回適用してよい。
            int dead = MakeDeadLights(path, label);

            if (GameObject.Find(MarkerName) == null) new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SetDressing] {label}: 蛍光灯 {dead} 本を切れた状態に");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SetDressing] 完了");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// 蛍光灯を数本殺して暗がりを作り、生き残りを強くして明暗差を付ける。
    /// どれを殺すかは位置から決定的に決める（実行のたびに変わるとシーン差分が汚れるため）。
    /// </summary>
    static int MakeDeadLights(string scenePath, string label)
    {
        // 天井の蛍光灯だけを対象にする。手がかり用の演出ライトは触らない。
        //
        // **切れているライトも拾うこと（Include）。**
        // Exclude にしていたので、2回目の実行では前回切った分が対象から外れ、
        // 「残っている本数の N%」を毎回切ることになっていた。
        // 走らせるたびに暗くなる、再現しないパスだった。
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                           .Where(l => l.type == LightType.Point || l.type == LightType.Spot)
                           .Where(l => l.transform.position.y > 1.9f)
                           .Where(l => l.GetComponentInParent<ClueInteractable>() == null)
                           .OrderBy(l => l.transform.position.z)
                           .ThenBy(l => l.transform.position.x)
                           .ToList();

        if (lights.Count == 0)
        {
            Debug.LogWarning($"[SetDressing] {label}: 天井ライトが見つからない");
            return 0;
        }

        float ratio = DeadRatio(scenePath);
        int dead = 0;

        for (int i = 0; i < lights.Count; i++)
        {
            var light = lights[i];

            // 位置から決定的に決める。連続して切れないよう間隔もばらす
            float h = Mathf.Abs(Mathf.Sin(light.transform.position.z * 12.9898f +
                                          light.transform.position.x * 78.233f) * 43758.5453f % 1f);

            // 毎回ここで決め直すので、前回の状態は判断に影響しない
            if (h < ratio)
            {
                // 切れた蛍光灯。器具は残して光だけ消す
                light.enabled = false;
                var flicker = light.GetComponent<LightFlicker>();
                if (flicker != null) Object.DestroyImmediate(flicker);
                dead++;
            }
            else
            {
                // 生きている側は強くする。暗がりとの落差が空間を作る。
                //
                // **フロアごとの倍率を必ず掛けること。**
                // ここで `base * 1.45` としていたため、M3 が設定した
                // フロアごとの明るさ（地下 0.75 / 1F 1.1）が上書きされ、
                // 全フロアが同じ明るさで焼かれていた。地下が暗くなかったのはこれが原因。
                light.enabled = true;   // 前回切ったものを戻す（再実行で結果が変わらないように）
                var record = light.GetComponent<LightBaseIntensity>();
                float baseIntensity = record != null ? record.baseIntensity : light.intensity;
                light.intensity = baseIntensity * M3AtmospherePass.LightScaleFor(scenePath) * SurvivorBoost;
                light.range = Mathf.Max(light.range, 6.5f);
                light.shadows = LightShadows.Soft;
            }
            EditorUtility.SetDirty(light);
        }

        return dead;
    }
}
