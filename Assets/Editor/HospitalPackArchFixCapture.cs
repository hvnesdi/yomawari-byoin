// HospitalPackArchFixCapture.cs
// Capture the three confirmation screenshots requested in the follow-up:
//   2F corridor, 1F corridor, 1F patient room.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.IO;

public static class HospitalPackArchFixCapture
{
    public static void RunBatch()
    {
        try
        {
            string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";
            Capture("Assets/Scenes/Hospital2F.unity", outDir + "/2F_Corridor_fix.png",
                new Vector3(0f, 1.65f, -12f), new Vector3(6f, 0f, 0f));
            Capture("Assets/Scenes/Hospital.unity", outDir + "/1F_Corridor_fix.png",
                new Vector3(0.0f, 1.65f, 4f), new Vector3(6f, 180f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });
            Capture("Assets/Scenes/Hospital.unity", outDir + "/1F_PatientRoom_fix.png",
                new Vector3(-4.5f, 1.65f, 4.0f), new Vector3(6f, -90f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });
            Debug.Log("=== Fix screenshots captured ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            EditorApplication.Exit(1);
        }
    }

    static void Capture(string scenePath, string outPath, Vector3 camPos, Vector3 camRot,
                         string[] hideRoots = null)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var restored = new List<GameObject>();
        if (hideRoots != null)
        {
            foreach (var n in hideRoots)
            {
                var hideGo = GameObject.Find(n);
                if (hideGo != null && hideGo.activeSelf) { hideGo.SetActive(false); restored.Add(hideGo); }
            }
        }

        var savedExp = new Dictionary<ColorAdjustments, (float, bool)>();
        var savedVig = new Dictionary<Vignette, (float, bool)>();
        foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (v.profile == null) continue;
            if (v.profile.TryGet<ColorAdjustments>(out var ca))
            {
                savedExp[ca] = (ca.postExposure.value, ca.postExposure.overrideState);
                ca.postExposure.value = ca.postExposure.value + 1.2f;
                ca.postExposure.overrideState = true;
            }
            if (v.profile.TryGet<Vignette>(out var vig))
            {
                savedVig[vig] = (vig.intensity.value, vig.intensity.overrideState);
                vig.intensity.value = Mathf.Max(0.15f, vig.intensity.value - 0.15f);
                vig.intensity.overrideState = true;
            }
        }
        var savedAmbMode = RenderSettings.ambientMode;
        var savedAmbLight = RenderSettings.ambientLight;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = savedAmbLight * 2.5f + new Color(0.08f, 0.08f, 0.09f);

        var fillGo = new GameObject("CaptureFill");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Point; fill.intensity = 1.4f; fill.range = 24f;
        fill.color = new Color(1f, 0.95f, 0.88f); fill.shadows = LightShadows.None;
        fillGo.transform.position = camPos + Vector3.up * 0.3f;

        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.eulerAngles = camRot;
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
        var camData = cam.GetUniversalAdditionalCameraData();
        if (camData != null) camData.renderPostProcessing = true;

        int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Debug.Log($"Saved: {outPath} ({new FileInfo(outPath).Length / 1024} KB)");

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(fillGo);
        RenderSettings.ambientMode = savedAmbMode;
        RenderSettings.ambientLight = savedAmbLight;
        foreach (var kv in savedExp) { kv.Key.postExposure.value = kv.Value.Item1; kv.Key.postExposure.overrideState = kv.Value.Item2; }
        foreach (var kv in savedVig) { kv.Key.intensity.value = kv.Value.Item1; kv.Key.intensity.overrideState = kv.Value.Item2; }
        foreach (var hg in restored) hg.SetActive(true);
        rt.Release();
    }
}
