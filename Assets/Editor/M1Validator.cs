using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M1 のセットアップが正しく完了しているかをエディタ側で検証する。
/// プレイ中の挙動までは見ない（それは PlayModeSelfCheck の役割）。
///
/// バッチ実行:
///   Unity.exe -batchmode -projectPath ... -executeMethod M1Validator.RunBatch -quit
/// 全項目 PASS なら exit 0、1件でも FAIL なら exit 1。
/// </summary>
public static class M1Validator
{
    static readonly List<string> results = new();
    static int failures;

    [MenuItem("消灯/M1: セットアップを検証")]
    public static void RunBatch()
    {
        results.Clear();
        failures = 0;

        CheckBuildSettings();
        CheckSystemsPrefab();
        CheckScenes();
        CheckProjectSettings();

        var sb = new StringBuilder();
        sb.AppendLine("===== M1 検証結果 =====");
        foreach (var r in results) sb.AppendLine(r);
        sb.AppendLine($"===== {results.Count - failures}/{results.Count} PASS =====");
        Debug.Log(sb.ToString());

        if (Application.isBatchMode)
            EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    static void Pass(string label) => results.Add($"[PASS] {label}");

    static void Fail(string label, string detail)
    {
        results.Add($"[FAIL] {label} — {detail}");
        failures++;
    }

    static void Check(bool condition, string label, string detail)
    {
        if (condition) Pass(label);
        else Fail(label, detail);
    }

    // ------------------------------------------------------------------
    static void CheckBuildSettings()
    {
        var expected = new[]
        {
            "Assets/Scenes/TitleScreen.unity",
            "Assets/Scenes/Hospital.unity",
            "Assets/Scenes/Hospital2F.unity",
            "Assets/Scenes/Hospital3F.unity",
            "Assets/Scenes/HospitalBasement.unity",
        };

        var registered = new HashSet<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) registered.Add(s.path);

        foreach (var path in expected)
        {
            Check(registered.Contains(path),
                  $"BuildSettings に {System.IO.Path.GetFileNameWithoutExtension(path)}",
                  "未登録。LoadScene が実行時に失敗する");
        }
    }

    static void CheckSystemsPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/__Systems.prefab");
        if (prefab == null)
        {
            Fail("__Systems.prefab の存在", "Assets/Resources/__Systems.prefab が無い");
            return;
        }
        Pass("__Systems.prefab の存在");

        Check(prefab.GetComponent<GameManager>()          != null, "GameManager",          "プレハブに未搭載");
        Check(prefab.GetComponent<TimeManager>()          != null, "TimeManager",          "プレハブに未搭載");
        Check(prefab.GetComponent<FlagManager>()          != null, "FlagManager",          "プレハブに未搭載");
        Check(prefab.GetComponent<AreaManager>()          != null, "AreaManager",          "プレハブに未搭載");
        Check(prefab.GetComponent<PlayerManager>()        != null, "PlayerManager",        "プレハブに未搭載");
        Check(prefab.GetComponent<HallucinationSystem>()  != null, "HallucinationSystem",  "プレハブに未搭載");
        Check(prefab.GetComponent<ParanoiaSystem>()       != null, "ParanoiaSystem",       "プレハブに未搭載");
        Check(prefab.GetComponent<HorrorEventSystem>()    != null, "HorrorEventSystem",    "プレハブに未搭載");
        Check(prefab.GetComponent<AudioSystem>()          != null, "AudioSystem",          "プレハブに未搭載");
        Check(prefab.GetComponent<SystemsBootstrap>()     != null, "SystemsBootstrap",     "プレハブに未搭載");

        var ui = prefab.GetComponentInChildren<UIManager>(true);
        if (ui == null)
        {
            Fail("UIManager の結線", "UIManager が無い");
        }
        else
        {
            Check(ui.timerText            != null, "UIManager.timerText",            "未結線");
            Check(ui.announcementText     != null, "UIManager.announcementText",     "未結線");
            Check(ui.interactionPrompt    != null, "UIManager.interactionPrompt",    "未結線");
            Check(ui.hallucinationOverlay != null, "UIManager.hallucinationOverlay", "未結線");
        }

        var ending = prefab.GetComponent<EndingSystem>();
        if (ending == null)
        {
            Fail("EndingSystem の結線", "EndingSystem が無い");
        }
        else
        {
            Check(ending.endingPanel     != null, "EndingSystem.endingPanel",     "未結線");
            Check(ending.endingTitleText != null, "EndingSystem.endingTitleText", "未結線");
            Check(ending.endingBodyText  != null, "EndingSystem.endingBodyText",  "未結線");
        }

        Check(prefab.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null,
              "EventSystem", "未搭載。タイトル画面のボタンが押せない");
    }

    static void CheckScenes()
    {
        var scenes = new[]
        {
            "Assets/Scenes/Hospital.unity",
            "Assets/Scenes/Hospital2F.unity",
            "Assets/Scenes/Hospital3F.unity",
            "Assets/Scenes/HospitalBasement.unity",
        };

        foreach (var path in scenes)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null)
            {
                Fail($"{name}: プレイヤー", "PlayerController が無い");
                continue;
            }

            Check(pc.CompareTag("Player"), $"{name}: Player タグ",
                  "未設定。敵の追跡と手がかり調査が動作しない");
            Check(Object.FindFirstObjectByType<AudioListener>() != null, $"{name}: AudioListener",
                  "無し。音が一切鳴らない");

            var cam = pc.GetComponentInChildren<Camera>(true);
            if (cam == null)
            {
                Fail($"{name}: カメラ", "プレイヤー配下に Camera が無い");
            }
            else
            {
                var look = cam.GetComponent<CameraController>();
                Check(look != null && look.playerBody != null, $"{name}: CameraController.playerBody",
                      "未結線。視点操作で本体が回らない");
            }

            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            Check(surface != null && surface.navMeshData != null, $"{name}: NavMesh",
                  "未ベイク。敵が一切移動できない");

            bool enemyOk = true, hasEnemy = false;
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                hasEnemy = true;
                if (enemy.playerSpawnPoint == null) enemyOk = false;
            }
            Check(!hasEnemy || enemyOk, $"{name}: 敵の playerSpawnPoint",
                  "未結線。捕捉されても転送されない");
        }

        // 3F → 地下 の導線
        EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        bool toBasement = false;
        foreach (var t in Object.FindObjectsByType<SceneTransitionTrigger>(FindObjectsSortMode.None))
            if (t.targetScene == "HospitalBasement") toBasement = true;
        Check(toBasement, "3F → 地下 の遷移", "トリガーが無い。最終エリアに到達できない");
    }

    static void CheckProjectSettings()
    {
        // 旧 Input API を使っているため Both(2) でないと実行時例外になる
        var so = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        var handler = so.FindProperty("activeInputHandler");
        Check(handler != null && handler.intValue == 2, "activeInputHandler = Both",
              $"現在 {(handler == null ? "不明" : handler.intValue.ToString())}。" +
              "旧 Input API が実行時例外を投げる");
    }
}
