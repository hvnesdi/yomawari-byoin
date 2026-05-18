using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

public class SceneScreenshotter
{
    static void CaptureScene(string scenePath, string outputPath, Vector3 camPos, Vector3 camRot)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // フィルライト追加（暗い batchmode 撮影用）
        var fillGo = new GameObject("CaptureLight");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 1.5f;
        fill.color = new Color(0.85f, 0.88f, 0.95f);
        fill.shadows = LightShadows.None;
        fillGo.transform.eulerAngles = new Vector3(45f, 0f, 0f);
        RenderSettings.ambientLight = new Color(0.35f, 0.34f, 0.32f);

        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.eulerAngles = camRot;
        cam.fieldOfView = 60f;
        cam.farClipPlane = 100f;
        cam.backgroundColor = Color.black;
        cam.clearFlags = CameraClearFlags.SolidColor;

        int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, tex.EncodeToPNG());
        Debug.Log($"Screenshot saved: {outputPath}");

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(fillGo);
        rt.Release();
    }

    public static void CaptureAll()
    {
        string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";

        CaptureScene(
            "Assets/Scenes/Hospital.unity",
            outDir + "/1F_Corridor.png",
            new Vector3(-5, 1.7f, 0),
            new Vector3(0, 90, 0)
        );

        CaptureScene(
            "Assets/Scenes/Hospital.unity",
            outDir + "/1F_PatientRoom.png",
            new Vector3(5, 1.7f, 2),
            new Vector3(0, 180, 0)
        );

        CaptureScene(
            "Assets/Scenes/Hospital2F.unity",
            outDir + "/2F_Corridor.png",
            new Vector3(-5, 1.7f, 0),
            new Vector3(0, 90, 0)
        );

        CaptureScene(
            "Assets/Scenes/Hospital3F.unity",
            outDir + "/3F_Corridor.png",
            new Vector3(-5, 1.7f, 0),
            new Vector3(0, 90, 0)
        );

        CaptureScene(
            "Assets/Scenes/HospitalBasement.unity",
            outDir + "/Basement.png",
            new Vector3(0, 1.7f, -5),
            new Vector3(0, 0, 0)
        );

        Debug.Log("All screenshots captured!");
        EditorApplication.Exit(0);
    }
}
