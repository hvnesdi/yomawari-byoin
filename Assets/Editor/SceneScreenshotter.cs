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
        // Resolve outputs/ relative to the project (worktree-aware)
        string projRoot = Path.GetDirectoryName(Application.dataPath);
        string outDir = Path.Combine(projRoot, "outputs", "hospital_hq");
        Directory.CreateDirectory(outDir);
        // Keep a copy in the legacy Screenshots/ folder too
        string legacyDir = Path.Combine(projRoot, "Screenshots");
        Directory.CreateDirectory(legacyDir);

        var shots = new (string scene, string name, Vector3 pos, Vector3 rot)[]
        {
            ("Assets/Scenes/Hospital.unity",         "1F_Corridor",       new Vector3(-5f, 1.65f,  0f), new Vector3(0,  90, 0)),
            ("Assets/Scenes/Hospital.unity",         "1F_PatientRoom",    new Vector3( 4f, 1.65f,  4f), new Vector3(0, 200, 0)),
            ("Assets/Scenes/Hospital.unity",         "1F_NearDoor",       new Vector3(-3.5f,1.65f, 9f), new Vector3(0, 270, 0)),
            ("Assets/Scenes/Hospital2F.unity",       "2F_Corridor",       new Vector3(-5f, 1.65f,  0f), new Vector3(0,  90, 0)),
            ("Assets/Scenes/Hospital2F.unity",       "2F_Ward",           new Vector3( 4f, 1.65f,  6f), new Vector3(0, 200, 0)),
            ("Assets/Scenes/Hospital3F.unity",       "3F_Corridor",       new Vector3(-5f, 1.65f,  0f), new Vector3(0,  90, 0)),
            ("Assets/Scenes/Hospital3F.unity",       "3F_Isolation",      new Vector3( 4f, 1.65f, 10f), new Vector3(0, 200, 0)),
            ("Assets/Scenes/HospitalBasement.unity", "Basement_Records",  new Vector3( 0f, 1.65f, -5f), new Vector3(0,   0, 0)),
            ("Assets/Scenes/HospitalBasement.unity", "Basement_Cabinets", new Vector3(-2.5f,1.65f, 0f), new Vector3(0,  60, 0)),
        };

        foreach (var s in shots)
        {
            string outPath = Path.Combine(outDir, s.name + ".png");
            CaptureScene(s.scene, outPath, s.pos, s.rot);
            // mirror into legacy Screenshots/
            try { File.Copy(outPath, Path.Combine(legacyDir, s.name + ".png"), true); } catch { /* ignore */ }
        }

        Debug.Log($"All screenshots captured to: {outDir}");
        EditorApplication.Exit(0);
    }
}
