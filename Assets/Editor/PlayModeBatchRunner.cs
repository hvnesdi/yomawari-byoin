using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// バッチモードで実際に Play モードに入り、数秒動かしてから終了する。
///
/// このプロジェクトは「コードは書けているが一度も実行されていない」状態が長く続き、
/// マネージャ未配置・NavMesh 未ベイク・タグ未設定といった欠落に誰も気づけなかった。
/// 静的な検証（M1Validator）だけでは同じことが起きるので、実際に動かして
/// PlayModeSelfCheck のレポートをログに残す。
///
/// バッチ実行:
///   Unity.exe -batchmode -projectPath ... -executeMethod PlayModeBatchRunner.RunBatch
///   （-quit は付けない。このクラスが自分で Exit する）
/// </summary>
public static class PlayModeBatchRunner
{
    const string RunningKey = "PlayModeBatchRunner.Running";
    const string DoneKey    = "PlayModeBatchRunner.Done";

    // タイムライン（Play 開始からの秒数）
    //   2s  PlayModeSelfCheck が自動レポート
    //   5s  ゲーム画面をキャプチャ
    //   7s  タイマーを強制的に切らしてエンドを発火
    //  11s  エンドが出たか検証して終了
    const double CaptureAt   = 5.0;
    const double ForceEndAt  = 7.0;
    const double PlaySeconds = 11.0;

    /// <summary>Play モードに入れなかった場合の打ち切り時間。</summary>
    const double EnterTimeout = 120.0;

    static double playStartTime = -1.0;
    static double hookTime = -1.0;
    static bool captured;
    static bool endForced;

    /// <summary>
    /// Play 中のゲーム画面を PNG に落とす。
    /// ScreenCapture はバッチモードだと黒になることがあるため、
    /// メインカメラを RenderTexture に描いて読み出す。
    /// UI は ScreenSpaceOverlay のため写らない（3D 描画の確認用）。
    /// </summary>
    static void CaptureGameplayScreenshot()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[PlayModeBatchRunner] MainCamera が無く撮影できません"); return; }

        const int w = 1280, h = 720;
        // フォーマットを明示しないと HDR で描かれ、RGB24 に読み出したときに
        // 色が壊れる（全面マゼンタになる）。SceneScreenshotter と同じ指定にする。
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

        var dir = System.IO.Path.Combine(Application.dataPath, "..", "Screenshots");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "PlayMode_1F.png"));
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);

        Debug.Log($"[PlayModeBatchRunner] ゲーム画面を保存: {path}");
    }

    /// <summary>エンドが実際に発火して画面に出たかを確認する。</summary>
    static void VerifyEnding()
    {
        var es = EndingSystem.Instance;
        if (es == null) { Debug.LogError("[PlayModeBatchRunner] [FAIL] EndingSystem が居ない"); return; }

        bool panelShown = es.endingPanel != null && es.endingPanel.activeInHierarchy;
        string title = es.endingTitleText != null ? es.endingTitleText.text : "";
        string body  = es.endingBodyText  != null ? es.endingBodyText.text  : "";

        if (panelShown && !string.IsNullOrEmpty(title))
            Debug.Log($"[PlayModeBatchRunner] [PASS] エンド発火: 「{title}」 / 「{body}」");
        else
            Debug.LogError($"[PlayModeBatchRunner] [FAIL] エンドが出ていない " +
                           $"(panel={panelShown}, title=\"{title}\")");
    }

    [MenuItem("消灯/M2: Play モードで通し確認")]
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(DoneKey, false);
        captured = false;
        endForced = false;
        Debug.Log("[PlayModeBatchRunner] Play モードに入ります");
        EditorApplication.EnterPlaymode();
    }

    // Play モード遷移でドメインリロードが走るため、静的フィールドではなく
    // SessionState で状態を持ち越し、リロード後にここで再フックする
    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (!SessionState.GetBool(RunningKey, false) && !SessionState.GetBool(DoneKey, false))
            return;

        hookTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void Tick()
    {
        if (SessionState.GetBool(DoneKey, false)) return;

        if (!EditorApplication.isPlaying)
        {
            // まだ Play に入っていない。入れないまま時間切れなら失敗として抜ける
            if (hookTime > 0 && EditorApplication.timeSinceStartup - hookTime > EnterTimeout)
            {
                Debug.LogError("[PlayModeBatchRunner] Play モードに入れませんでした");
                Finish(1);
            }
            return;
        }

        if (playStartTime < 0) playStartTime = EditorApplication.timeSinceStartup;
        double elapsed = EditorApplication.timeSinceStartup - playStartTime;

        if (!captured && elapsed >= CaptureAt)
        {
            captured = true;
            CaptureGameplayScreenshot();

            // プレイ中の描画を使って「明るい部分が何なのか」を集計する。
            // エディットモードの手動レンダリングでは URP のライティングが乗らないため、
            // ここで実行する必要がある。
            var cam = Camera.main;
            if (cam != null) VisualDiagnostics.IdentifyFromCamera(cam);
        }

        if (!endForced && elapsed >= ForceEndAt)
        {
            endForced = true;
            // 90分待たずにエンドまで通す
            Debug.Log("[PlayModeBatchRunner] タイマーを強制終了させてエンドを発火します");
            TimeManager.Instance?.DebugAdvance(TimeManager.TotalSeconds);
        }

        if (elapsed < PlaySeconds) return;

        VerifyEnding();
        Debug.Log($"[PlayModeBatchRunner] {PlaySeconds} 秒プレイしました。終了します");
        SessionState.SetBool(RunningKey, false);
        SessionState.SetBool(DoneKey, true);
        EditorApplication.isPlaying = false;   // EnteredEditMode で Exit する
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        if (!SessionState.GetBool(DoneKey, false)) return;
        Finish(0);
    }

    static void Finish(int exitCode)
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        SessionState.SetBool(RunningKey, false);
        SessionState.SetBool(DoneKey, false);

        if (Application.isBatchMode)
            EditorApplication.Exit(exitCode);
    }
}
