using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M16: 未設定の参照を洗い出す。
///
/// このプロジェクトで繰り返し出てきた不具合は、いつも同じ形をしている——
/// **「受け取るフィールドはあるが、誰も入れていない」**。
///   - `AudioSystem` のクリップが全部 null → ゲームが完全に無音だった
///   - `HorrorEventSystem` のクリップが全部 null → 恐怖演出が無音で発火していた
///   - `HallucinationProfile` の効果が全部 null → 色調整が一度も効いていなかった
/// どれも例外を出さず、ログにも出ず、静かに「何もしない」状態になる。
///
/// 見つけ方が毎回「たまたま気づく」だったので、数えられるようにする。
/// シーンとプレハブの MonoBehaviour を走査して、未設定の参照を並べる。
///
/// **これは検査であって修正ではない。** 未設定が常に不具合とは限らない
/// （任意指定のフィールドもある）。判断はこの一覧を見てから行う。
/// </summary>
public static class M16ReferenceAudit
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    /// <summary>
    /// 未設定でも問題にしないもの。
    /// 実行時に代入される、あるいは意図的に空にしてある。
    /// </summary>
    static readonly string[] Ignore =
    {
        "m_Script",
        "playerSpawnPoint",   // 敵の転送先。SceneWiringFixer が入れる
        "waypoints",          // 巡回点。空でも巡回しないだけ
    };

    /// <summary>
    /// 実行時に代入される参照。アセット上は空で正しい。
    ///
    /// **隠さずに別枠で出す。** 「無視」にしてしまうと、後から結線が壊れても
    /// 検査を通ってしまう。ここに挙がっているものは、プレイテストの
    /// セルフチェック（実行中に非 null かを見る）が担保している。
    /// </summary>
    static readonly string[] RuntimeBound =
    {
        // GameAudioBinder が Resources から読んで入れる
        "AudioSystem.bgmNormal", "AudioSystem.bgmTense", "AudioSystem.bgmPeak",
        "AudioSystem.bgmEndingHappy", "AudioSystem.bgmEndingBad",
        "AudioSystem.announce90", "AudioSystem.announce60", "AudioSystem.announce30",
        "AudioSystem.announce10", "AudioSystem.announce5", "AudioSystem.announce0",
        "HorrorEventSystem.footstepsClip", "HorrorEventSystem.nameCallClip",
        "HorrorEventSystem.tapeScreamClip", "HorrorEventSystem.backVoiceClip",
        "HorrorEventSystem.suddenNoiseClip", "HorrorEventSystem.npcPrefab",
        // HorrorPropBinder がシーンから渡す（フロアごとに別の物になるため）
        "HorrorEventSystem.mirrorRenderer", "HorrorEventSystem.mirrorNormalMat",
        "HorrorEventSystem.mirrorDelayMat", "HorrorEventSystem.mirrorChangeMat",
        "HorrorEventSystem.photoRenderer",
    };

    [MenuItem("消灯/M16: 未設定の参照を調べる")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M16] 未設定の参照\n");
        var tally = new Dictionary<string, List<string>>();

        void Record(string owner, string field, string where)
        {
            var key = $"{owner}.{field}";
            if (!tally.TryGetValue(key, out var list)) tally[key] = list = new List<string>();
            if (!list.Contains(where)) list.Add(where);
        }

        // 常駐システムのプレハブ。ここが空だと全フロアに影響する
        var systems = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/__Systems.prefab");
        if (systems != null) Scan(systems, "__Systems.prefab", Record);
        else log.AppendLine("  ? __Systems.prefab が読めない");

        foreach (var path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                Scan(root, label, Record);
        }

        var runtime = tally.Where(kv => RuntimeBound.Contains(kv.Key))
                           .OrderBy(kv => kv.Key).ToList();
        var unknown = tally.Where(kv => !RuntimeBound.Contains(kv.Key))
                           .OrderByDescending(kv => kv.Value.Count).ThenBy(kv => kv.Key).ToList();

        log.AppendLine($"── 要確認 {unknown.Count} 種類");
        if (unknown.Count == 0) log.AppendLine("  未設定の参照は無い");
        foreach (var kv in unknown)
            log.AppendLine($"  {kv.Key,-52} {kv.Value.Count} 箇所 " +
                            $"({string.Join(", ", kv.Value.Take(4))})");

        // 隠さずに出す。ここが減っていたら結線側が壊れている可能性がある
        log.AppendLine($"── 実行時に入るもの {runtime.Count} 種類（アセット上は空で正しい）");
        foreach (var kv in runtime)
            log.AppendLine($"  {kv.Key}");

        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static string Path(Transform t)
    {
        var parts = new List<string>();
        while (t != null) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    static void Scan(GameObject root, string where,
                     System.Action<string, string, string> record)
    {
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;

            // 自作のものだけ見る。パッケージ側は対象外
            var script = MonoScript.FromMonoBehaviour(behaviour);
            if (script == null) continue;
            var scriptPath = AssetDatabase.GetAssetPath(script);
            if (!scriptPath.StartsWith("Assets/Scripts")) continue;

            var so = new SerializedObject(behaviour);
            var prop = so.GetIterator();
            bool enterChildren = true;

            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;   // 入れ子には降りない（配列の中身まで見ると膨れる）

                if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (Ignore.Contains(prop.name)) continue;
                if (prop.objectReferenceValue != null) continue;

                // どこに在るのかまで書く。コンポーネント名だけだと、
                // 直しに行くときに結局そこから探すことになる
                record(behaviour.GetType().Name, prop.name, $"{where}:{Path(behaviour.transform)}");
            }
        }
    }
}
