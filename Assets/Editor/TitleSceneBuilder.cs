using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleSceneBuilder
{
    [MenuItem("Tools/Build Title Scene")]
    public static void BuildScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();

        // Canvas
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // CanvasGroup for fade
        var cg = canvasGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Background (black panel)
        var bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImage = bgGo.AddComponent<Image>();
        bgImage.color = Color.black;
        var bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // Title text
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(canvasGo.transform, false);
        var titleText = titleGo.AddComponent<Text>();
        titleText.text = "消灯";
        titleText.fontSize = 120;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(1f, 1f, 1f, 0.9f);
        var titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.55f);
        titleRect.anchorMax = new Vector2(0.5f, 0.55f);
        titleRect.pivot     = new Vector2(0.5f, 0.5f);
        titleRect.sizeDelta = new Vector2(600f, 200f);
        titleRect.anchoredPosition = Vector2.zero;

        // Subtitle line
        var lineGo = new GameObject("SubtitleLine");
        lineGo.transform.SetParent(canvasGo.transform, false);
        var lineImg = lineGo.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.4f);
        var lineRect = lineGo.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.2f, 0.44f);
        lineRect.anchorMax = new Vector2(0.8f, 0.44f);
        lineRect.sizeDelta = new Vector2(0f, 1f);
        lineRect.anchoredPosition = Vector2.zero;

        // START button
        var btnGo = new GameObject("StartButton");
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btn = btnGo.AddComponent<Button>();
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0f);
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.3f);
        btnRect.anchorMax = new Vector2(0.5f, 0.3f);
        btnRect.pivot     = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(300f, 80f);
        btnRect.anchoredPosition = Vector2.zero;

        var btnTextGo = new GameObject("ButtonText");
        btnTextGo.transform.SetParent(btnGo.transform, false);
        var btnText = btnTextGo.AddComponent<Text>();
        btnText.text = "START";
        btnText.fontSize = 48;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.color = new Color(1f, 1f, 1f, 0.85f);
        var btnTextRect = btnTextGo.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.sizeDelta = Vector2.zero;

        // TitleController for fade + button
        var ctrlGo = new GameObject("TitleController");
        ctrlGo.AddComponent<TitleController>();

        // Save scene
        string scenePath = "Assets/Scenes/TitleScreen.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("[TitleSceneBuilder] Saved: " + scenePath);

        // Update build settings: TitleScreen index 0, Hospital index 1
        var scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Hospital.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Hospital2F.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Hospital3F.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/HospitalBasement.unity", true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[TitleSceneBuilder] Build Settings updated.");
    }
}
