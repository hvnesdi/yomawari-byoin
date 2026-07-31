using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// M1: システム一式（マネージャ + UI + 幻覚ポストプロセス）を
/// Resources/__Systems.prefab として生成し、Build Settings にシーンを登録する。
///
/// SystemsBootstrap がこのプレハブを実行時に自動生成するため、
/// 各シーンにマネージャを置く必要はない。
/// </summary>
public static class GameBootstrapBuilder
{
    const string ResourcesDir  = "Assets/Resources";
    const string PrefabPath    = "Assets/Resources/__Systems.prefab";
    const string ProfilePath   = "Assets/Resources/HallucinationProfile.asset";

    static readonly string[] ScenePaths =
    {
        "Assets/Scenes/TitleScreen.unity",
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M1: システム一式をセットアップ")]
    public static void RunBatch()
    {
        EnsureResourcesFolder();
        var profile = BuildHallucinationProfile();
        BuildSystemsPrefab(profile);
        RegisterScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GameBootstrapBuilder] 完了");
    }

    static void EnsureResourcesFolder()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
            AssetDatabase.CreateFolder("Assets", "Resources");
    }

    // ------------------------------------------------------------------
    // 幻覚レベル連動のポストプロセスプロファイル
    // HallucinationSystem が vignette / chromaticAberration / lensDistortion を駆動する
    // ------------------------------------------------------------------
    static VolumeProfile BuildHallucinationProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        // **中身を消してはいけない。**
        //
        // ここは幻覚演出用の3つ（ビネット・色収差・レンズ歪み）だけを見ているが、
        // 同じアセットに M5LookPass が色調整・トーンマップ・グレインを入れている。
        // 以前はこの関数が毎回すべて消してから3つを足し直していたので、
        // **run_all.ps1 を走らせるたびに画づくりが丸ごと消えていた**。
        // ゲーム側の修正をしただけのつもりで、画面が白く戻る。
        //
        // 消すのは「参照が死んでいるもの」だけにして、足りないものを足す方式にする。
        int purged = profile.components.RemoveAll(c => c == null);
        if (purged > 0)
            Debug.Log($"[GameBootstrapBuilder] 参照の切れたオーバーライド {purged} 個を掃除");

        EnsureOverride<Vignette>(profile, v =>
        {
            v.intensity.value = 0.15f;
            v.smoothness.value = 0.4f;
            v.color.value = Color.black;
        });
        EnsureOverride<ChromaticAberration>(profile, c => c.intensity.value = 0f);
        EnsureOverride<LensDistortion>(profile, l => l.intensity.value = 0f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        int alive = profile.components.Count(c => c != null);
        Debug.Log($"[GameBootstrapBuilder] VolumeProfile: {ProfilePath}（オーバーライド {alive} 個）");
        return profile;
    }

    // ------------------------------------------------------------------
    // __Systems.prefab
    // ------------------------------------------------------------------
    /// <summary>
    /// 無ければ足す。既にあれば値には触らない。
    ///
    /// 値を毎回上書きしないのは、M5LookPass が同じ効果（ビネット）に別の値を
    /// 入れているため。どちらが後に走ったかで結果が変わるのを避ける。
    /// 実行時は HallucinationSystem が毎フレーム上書きするので、初期値は競わせなくてよい。
    ///
    /// `AddObjectToAsset` を忘れるとアセットの子として保存されず、
    /// 次に読み込んだとき参照が null になる（それで一度、演出が全部消えた）。
    /// </summary>
    static void EnsureOverride<T>(VolumeProfile profile, System.Action<T> configure)
        where T : VolumeComponent
    {
        if (profile.TryGet<T>(out var existing) && existing != null) return;

        var component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        configure(component);
    }

    static void BuildSystemsPrefab(VolumeProfile profile)
    {
        // マネージャは全てルートに載せる。
        // FlagManager / GameManager が Awake で DontDestroyOnLoad(gameObject) を呼ぶため、
        // 子オブジェクトに置くと「ルートでないと効かない」警告が出る。
        var root = new GameObject("__Systems");
        root.AddComponent<SystemsBootstrap>();

        root.AddComponent<GameManager>();
        root.AddComponent<TimeManager>();
        root.AddComponent<FlagManager>();
        root.AddComponent<AreaManager>();
        root.AddComponent<PlayerManager>();
        var hallucination = root.AddComponent<HallucinationSystem>();
        root.AddComponent<ParanoiaSystem>();
        var ending = root.AddComponent<EndingSystem>();
        root.AddComponent<HorrorEventSystem>();
        root.AddComponent<AudioSystem>();

        // フロアごとの環境音。**これが無い間、ゲームは完全に無音だった**
        // （AudioSystem はクリップの入れ物だが、何も割り当てられていなかった）。
        // 別の GameObject に置くのは、AudioSource を1つのオブジェクトに
        // 積み上げると AudioSystem 側の取得と混ざるため。
        var ambienceGo = new GameObject("FloorAmbience");
        ambienceGo.transform.SetParent(root.transform, false);
        ambienceGo.AddComponent<AudioSource>();
        ambienceGo.AddComponent<FloorAmbience>();

        root.AddComponent<PlayModeSelfCheck>();   // デバッグ用。リリース前に外す
        // NetworkManager は Steam 未起動時に落ちるため M1 では載せない（マルチプレイは M4 以降）

        // --- 幻覚ポストプロセス用のグローバル Volume ---
        var volumeGo = new GameObject("HallucinationVolume");
        volumeGo.transform.SetParent(root.transform, false);
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
#if UNITY_URP
        hallucination.postProcessVolume = volume;
#else
        Debug.LogWarning("[GameBootstrapBuilder] UNITY_URP が未定義のため幻覚ポストプロセスは無効です");
#endif

        // --- UI ---
        var canvasGo = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var ui = canvasGo.AddComponent<UIManager>();

        // 幻覚オーバーレイ（描画順を下にするため先に作る）
        var overlay = MakeFullScreenImage(canvasGo.transform, "HallucinationOverlay",
                                          new Color(0.06f, 0.0f, 0.06f, 1f));
        var overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        overlayGroup.interactable = false;

        var timer = MakeText(canvasGo.transform, "TimerText", "90:00", 48, TextAnchor.UpperCenter,
                             new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                             new Vector2(-200f, -110f), new Vector2(200f, -30f));

        var announcement = MakeText(canvasGo.transform, "AnnouncementText", "", 34, TextAnchor.MiddleCenter,
                                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                    new Vector2(-700f, 130f), new Vector2(700f, 260f));
        announcement.gameObject.SetActive(false);

        var prompt = MakeText(canvasGo.transform, "InteractionPrompt", "", 30, TextAnchor.MiddleCenter,
                              new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              new Vector2(-300f, -120f), new Vector2(300f, -60f));
        prompt.gameObject.SetActive(false);

        // エンドパネル（最前面）
        var endingPanel = MakeFullScreenImage(canvasGo.transform, "EndingPanel", new Color(0f, 0f, 0f, 0.97f));
        var endingTitle = MakeText(endingPanel.transform, "EndingTitle", "", 64, TextAnchor.MiddleCenter,
                                   new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   new Vector2(-800f, 20f), new Vector2(800f, 160f));
        var endingBody = MakeText(endingPanel.transform, "EndingBody", "", 36, TextAnchor.UpperCenter,
                                  new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                  new Vector2(-800f, -160f), new Vector2(800f, -10f));
        endingPanel.gameObject.SetActive(false);

        ui.timerText             = timer;
        ui.announcementText      = announcement;
        ui.interactionPrompt     = prompt;
        ui.hallucinationOverlay  = overlayGroup;

        ending.endingPanel     = endingPanel.gameObject;
        ending.endingTitleText = endingTitle;
        ending.endingBodyText  = endingBody;

        // --- EventSystem（タイトル画面のボタンに必要。シーン側には存在しない） ---
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(root.transform, false);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"[GameBootstrapBuilder] プレハブ生成: {PrefabPath}");
    }

    static Image MakeFullScreenImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text MakeText(Transform parent, string name, string content, int fontSize, TextAnchor anchor,
                         Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        // フォントは実行時に JapaneseFont が差し替える（日本語グリフのため）
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var shadow = go.AddComponent<Outline>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    // ------------------------------------------------------------------
    // Build Settings
    // ------------------------------------------------------------------
    static void RegisterScenes()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var path in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                Debug.LogWarning($"[GameBootstrapBuilder] シーンが見つかりません: {path}");
                continue;
            }
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[GameBootstrapBuilder] Build Settings に {scenes.Count} シーンを登録");
    }
}
