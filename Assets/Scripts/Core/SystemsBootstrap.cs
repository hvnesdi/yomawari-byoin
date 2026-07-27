using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Resources/__Systems.prefab を起動時に自動生成して常駐させる。
///
/// 各マネージャは Resources/__Systems.prefab のルートに載っており、
/// このクラスが BeforeSceneLoad で1度だけ Instantiate + DontDestroyOnLoad する。
///
/// この方式を採る理由（CLAUDE.md の設計判断）:
///   - TimeManager など DontDestroyOnLoad を持たないマネージャをシーンごとに置くと、
///     フロア移動のたびに 90 分タイマーがリセットされてしまう
///   - どのシーンから Play しても同じ初期化が走る（エディタでの検証が楽）
/// </summary>
public class SystemsBootstrap : MonoBehaviour
{
    public const string PrefabResourcePath = "__Systems";

    private static bool spawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Spawn()
    {
        if (spawned) return;

        var prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[SystemsBootstrap] Resources/{PrefabResourcePath}.prefab が見つかりません。" +
                            "エディタメニュー 消灯/M1: システム一式をセットアップ を実行してください。");
            return;
        }

        var go = Instantiate(prefab);
        go.name = "__Systems";
        DontDestroyOnLoad(go);
        spawned = true;

        ApplyFontTo(go);
        SceneManager.sceneLoaded += (scene, mode) => ApplyFontToScene(scene);
        ApplyFontToScene(SceneManager.GetActiveScene());

        Debug.Log("[SystemsBootstrap] システム一式を生成しました");
    }

    private static void ApplyFontToScene(Scene scene)
    {
        if (!scene.IsValid()) return;
        foreach (var root in scene.GetRootGameObjects())
            ApplyFontTo(root);
    }

    private static void ApplyFontTo(GameObject root)
    {
        var font = JapaneseFont.Get();
        if (font == null) return;

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            // 明示的に別フォントが割り当てられている場合も、日本語が出ないので上書きする
            text.font = font;
        }
    }
}

/// <summary>
/// 日本語が表示できるフォントを OS から動的に取得する。
///
/// TODO(リリース前): OS フォント依存をやめ、ライセンス的に再配布可能な
/// 日本語フォント（Noto Sans JP / M PLUS 等）をプロジェクトに同梱すること。
/// 現状は開発中の可読性を優先している。
/// </summary>
public static class JapaneseFont
{
    private static Font cached;

    private static readonly string[] Candidates =
    {
        "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Meiryo",
        "BIZ UDGothic", "MS Gothic", "MS UI Gothic", "Noto Sans JP",
    };

    public static Font Get()
    {
        if (cached != null) return cached;

        var installed = Font.GetOSInstalledFontNames();
        foreach (var name in Candidates)
        {
            if (System.Array.IndexOf(installed, name) < 0) continue;
            cached = Font.CreateDynamicFontFromOSFont(name, 32);
            if (cached != null)
            {
                Debug.Log($"[JapaneseFont] OS フォント '{name}' を使用します");
                return cached;
            }
        }

        Debug.LogWarning("[JapaneseFont] 日本語フォントが見つかりません。日本語が表示されない可能性があります。");
        cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cached;
    }
}
