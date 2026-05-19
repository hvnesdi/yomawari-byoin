using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

public class SceneScreenshotter
{
    const float fillLightIntensity = 3.0f;
    const float ambientStrength    = 0.55f;

    static void CaptureScene(string scenePath, string outputPath, Vector3 camPos, Vector3 camRot)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // ── Volume の暗くするポストFXを撮影中だけ上書き ──────────────────
        var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
        var savedExposure = new System.Collections.Generic.Dictionary<ColorAdjustments, (float val, bool over)>();
        var savedVignette = new System.Collections.Generic.Dictionary<Vignette, (float val, bool over)>();
        foreach (var v in volumes)
        {
            if (v.profile == null) continue;
            if (v.profile.TryGet<ColorAdjustments>(out var ca))
            {
                savedExposure[ca] = (ca.postExposure.value, ca.postExposure.overrideState);
                ca.postExposure.value = 0.5f;
                ca.postExposure.overrideState = true;
            }
            if (v.profile.TryGet<Vignette>(out var vig))
            {
                savedVignette[vig] = (vig.intensity.value, vig.intensity.overrideState);
                vig.intensity.value = 0.1f;
                vig.intensity.overrideState = true;
            }
        }

        // ── 明るい環境光 ─────────────────────────────────────────
        var savedAmbientMode = RenderSettings.ambientMode;
        var savedAmbientLight = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(ambientStrength, ambientStrength * 0.98f, ambientStrength * 0.95f);

        // ── 既存ライトを一時的に再有効化（HospitalVisualImprover が Directional を切っている） ──
        var reEnabled = new System.Collections.Generic.List<GameObject>();
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional && !l.gameObject.activeSelf)
            {
                l.gameObject.SetActive(true);
                reEnabled.Add(l.gameObject);
            }
        }

        // ── 強いフィルライト追加 ────────────────────────────────
        var fillGo = new GameObject("CaptureLight");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = fillLightIntensity;
        fill.color = new Color(0.95f, 0.97f, 1.0f);
        fill.shadows = LightShadows.None;
        fillGo.transform.eulerAngles = new Vector3(50f, 30f, 0f);

        // 補助のポイントライト（カメラ後ろから）
        var keyGo = new GameObject("CaptureKey");
        var key = keyGo.AddComponent<Light>();
        key.type = LightType.Point;
        key.intensity = 4f;
        key.range = 30f;
        key.color = new Color(1f, 0.98f, 0.92f);
        keyGo.transform.position = camPos + new Vector3(0, 1f, 0);

        // ── カメラ作成 ─────────────────────────────────────────
        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.eulerAngles = camRot;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 120f;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        // URP camera data — postFX有効
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;

        int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        Debug.Log($"Screenshot saved: {outputPath} ({new FileInfo(outputPath).Length / 1024} KB)");

        // ── 復元 ───────────────────────────────────────────────
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(fillGo);
        Object.DestroyImmediate(keyGo);
        foreach (var gObj in reEnabled) gObj.SetActive(false);
        RenderSettings.ambientMode = savedAmbientMode;
        RenderSettings.ambientLight = savedAmbientLight;
        foreach (var kv in savedExposure)
        {
            kv.Key.postExposure.value = kv.Value.val;
            kv.Key.postExposure.overrideState = kv.Value.over;
        }
        foreach (var kv in savedVignette)
        {
            kv.Key.intensity.value = kv.Value.val;
            kv.Key.intensity.overrideState = kv.Value.over;
        }
        rt.Release();
    }

    public static void CaptureAll()
    {
        string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";

        // Corridor_Floor is at (0,0,0) scale (4,0.2,32) -> 4m wide (x:-2..+2), 32m long (z:-16..+16).
        // Camera must stay inside x:[-1.5, 1.5] and look along +Z. Previous positions worked; restoring them.

        // 1F 廊下: Reception(z:-17..-11)の北側、z=-8 から北向き(+Z)
        CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_Corridor.png",
            new Vector3(0f, 1.7f, -8f), new Vector3(5f, 0f, 0f));

        // 1F 病室: PatientRoom_1 (x=-6 周辺)、入口から奥のベッドを見る
        CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_PatientRoom.png",
            new Vector3(-4.2f, 1.65f, 7.2f), new Vector3(8f, -130f, 0f));

        // 2F 廊下: 長さ48 (z:-24..+24)
        CaptureScene("Assets/Scenes/Hospital2F.unity", outDir + "/2F_Corridor.png",
            new Vector3(0f, 1.7f, -22f), new Vector3(5f, 0f, 0f));

        // 3F 廊下: 長さ44 (z:-22..+22)、天井低め
        CaptureScene("Assets/Scenes/Hospital3F.unity", outDir + "/3F_Corridor.png",
            new Vector3(0f, 1.6f, -20f), new Vector3(5f, 0f, 0f));

        // 地下 廊下: 長さ36 (z:-18..+18)
        CaptureScene("Assets/Scenes/HospitalBasement.unity", outDir + "/Basement.png",
            new Vector3(0f, 1.6f, -16f), new Vector3(5f, 0f, 0f));

        Debug.Log("=== All screenshots captured! ===");
        EditorApplication.Exit(0);
    }
}
