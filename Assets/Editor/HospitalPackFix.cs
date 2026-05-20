// HospitalPackFix.cs
//
// Fixes the architecture placed by HospitalPackArchitecture:
//   1. Black gap at wall top / ceiling: lower ceilings by 0.105m so the
//      visible face overlaps the wall top by 0.005m.
//   2. Wall tiling: re-assert Tiling X:8 Y:8 on both wall mats.
//   3. Dark ceiling: bump Mat_Walllime02 base color toward (220,215,205)
//      and add a light emission so the ceiling reads as cream, not gray.
//   4. Ceiling lighting: corridor overhead PointLights (6500K).
//   5. Ambient: bump to 0.3 strength per-scene.
//
// Runs in Editor batchmode via HospitalPackFix.RunBatch.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public static class HospitalPackFix
{
    const string MatDir = "Assets/Dnk_Dev/HospitalHorrorPack/Models/Materials";
    const float CeilingDropY = 0.105f;   // shift ceiling down by this much
    const float WallExtendUp  = 0.06f;   // extend wall upward (in world meters)

    [MenuItem("Tools/Hospital Pack Fix (Batch)")]
    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Hospital Pack Fix")]
    public static void Run()
    {
        Debug.Log("=== HospitalPackFix START ===");
        FixMaterials();
        FixScene("Assets/Scenes/Hospital.unity",          "1F",  new Color(0.92f, 0.95f, 1.00f));
        FixScene("Assets/Scenes/Hospital2F.unity",        "2F",  new Color(0.95f, 0.93f, 0.85f));
        FixScene("Assets/Scenes/Hospital3F.unity",        "3F",  new Color(1.00f, 0.88f, 0.65f));
        FixScene("Assets/Scenes/HospitalBasement.unity",  "Bsm", new Color(1.00f, 0.55f, 0.40f));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalPackFix DONE ===");
    }

    // ──────────────────────────────────────────────────────────────────
    // 1. Material adjustments
    // ──────────────────────────────────────────────────────────────────
    static void FixMaterials()
    {
        // Walls: re-assert 8x8 tiling on both wall materials
        SetTiling($"{MatDir}/Mat_Walllime01_C.mat", new Vector2(8, 8));
        SetTiling($"{MatDir}/Mat_Tile02.mat",       new Vector2(8, 8));

        // Ceiling: brighten with cream base color + low emission so it reads
        // as a real painted ceiling under the corridor PointLights.
        var ceiling = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Mat_Walllime02.mat");
        if (ceiling != null)
        {
            // Brighter than 1.0 to compensate for the pack's dark texture
            ceiling.SetColor("_BaseColor",     new Color(1.20f, 1.17f, 1.10f, 1f));
            ceiling.SetColor("_Color",         new Color(1.20f, 1.17f, 1.10f, 1f));
            // Mild self-illumination so the ceiling never reads as black even
            // under low ambient. Matches the cream paint the user asked for.
            ceiling.SetColor("_EmissionColor", new Color(220f/255f, 215f/255f, 205f/255f, 1f) * 0.35f);
            ceiling.EnableKeyword("_EMISSION");
            ceiling.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(ceiling);
            Debug.Log("Ceiling mat brightened");
        }
        else
        {
            Debug.LogWarning("Mat_Walllime02 not found");
        }
        AssetDatabase.SaveAssets();
    }

    static void SetTiling(string path, Vector2 tile)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { Debug.LogWarning($"Missing mat: {path}"); return; }
        m.SetTextureScale("_BaseMap", tile);
        m.SetTextureScale("_MainTex", tile);
        EditorUtility.SetDirty(m);
        Debug.Log($"  tiling 8x8 on {System.IO.Path.GetFileNameWithoutExtension(path)}");
    }

    // ──────────────────────────────────────────────────────────────────
    // 2. Per-scene fix
    // ──────────────────────────────────────────────────────────────────
    static void FixScene(string scenePath, string label, Color ambientTint)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"Cannot open {scenePath}"); return; }

        var packRoot = GameObject.Find($"PackArch_{label}");
        if (packRoot == null)
        {
            Debug.LogWarning($"[{label}] PackArch_{label} not found, skipping geometry fix");
        }
        else
        {
            int ceilingMoved = 0;
            int wallStretched = 0;
            // Classify each child by its world-space bounds center Y.
            //   y > 2.5  -> ceiling
            //   y >= 0.5 && y < 2.5 -> wall
            //   y < 0.5  -> floor (untouched)
            foreach (Transform child in packRoot.transform)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                var bounds = ComputeWorldBounds(child.gameObject);
                if (!bounds.HasValue) continue;
                float cy = bounds.Value.center.y;
                if (cy > 2.5f)
                {
                    // Ceiling tile: lower so visible face lands on wall top
                    // (-0.005 overlap into wall top so no z-fight gap).
                    var p = child.position;
                    p.y -= CeilingDropY;
                    child.position = p;
                    ceilingMoved++;
                }
                else if (cy >= 0.5f && cy < 2.5f)
                {
                    // Wall tile: extend upward to reach (just past) ceiling.
                    // Keep the bottom on the floor by shifting position up by
                    // half the height increase.
                    var b = bounds.Value;
                    float oldH = b.size.y;
                    if (oldH < 0.5f) continue;
                    float newH = oldH + WallExtendUp;
                    float k = newH / oldH;
                    Vector3 ls = child.localScale;
                    ls.y *= k;
                    child.localScale = ls;
                    var p = child.position;
                    p.y += WallExtendUp * 0.5f;
                    child.position = p;
                    wallStretched++;
                }
            }
            EditorUtility.SetDirty(packRoot);
            Debug.Log($"[{label}] ceilings lowered: {ceilingMoved}, walls stretched: {wallStretched}");
        }

        // Ambient
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientTint * 0.3f;

        // Brighten existing corridor / ceiling lights and add new overhead
        // PointLights along corridors so the ceiling reads correctly.
        EnsureCorridorCeilingLights(label);

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[{label}] saved");
    }

    static Bounds? ComputeWorldBounds(GameObject go)
    {
        Bounds b = new Bounds();
        bool first = true;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        if (first) return null;
        return b;
    }

    // ──────────────────────────────────────────────────────────────────
    // 3. Corridor ceiling lights
    // ──────────────────────────────────────────────────────────────────
    static void EnsureCorridorCeilingLights(string label)
    {
        // (zStart, zEnd, count) per floor's main corridor
        float zStart, zEnd; int count;
        float x = 0f;
        switch (label)
        {
            case "1F":  zStart = -13f; zEnd = 13f; count = 5; break;
            case "2F":  zStart = -20f; zEnd = 20f; count = 7; break;
            case "3F":  zStart = -18f; zEnd = 18f; count = 6; break;
            case "Bsm": zStart = -14f; zEnd = 14f; count = 5; break;
            default: return;
        }
        var rootGo = GameObject.Find($"FixLights_{label}");
        if (rootGo != null)
        {
            for (int i = rootGo.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(rootGo.transform.GetChild(i).gameObject);
        }
        else
        {
            rootGo = new GameObject($"FixLights_{label}");
        }
        Color k6500 = new Color(0.98f, 0.97f, 1.00f);
        for (int i = 0; i < count; i++)
        {
            float t = (count == 1) ? 0.5f : (i / (float)(count - 1));
            float z = Mathf.Lerp(zStart, zEnd, t);
            var lg = new GameObject($"PFix_{label}_{i}");
            lg.transform.SetParent(rootGo.transform, true);
            lg.transform.position = new Vector3(x, 2.85f, z);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 0.5f;
            l.range = 9f;
            l.color = k6500;
            l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.ForcePixel;
        }
        Debug.Log($"[{label}] corridor ceiling lights placed: {count}");
    }
}
