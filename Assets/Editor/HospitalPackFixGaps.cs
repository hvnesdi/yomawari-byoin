// HospitalPackFixGaps.cs
//
// Closes the visible black gap between wall tops and ceiling, makes the
// wall/floor/ceiling textures finer (Tiling 8x8 ≈ 15cm tile), brightens
// the ceiling and adds dedicated ceiling Point Lights.
//
// We work directly on the previously-placed PackArch_X roots (so we
// don't need primitives to be in the scene anymore). The script:
//   1. Drops ceiling tiles by 0.10 m so their visible underside lands
//      flush with the wall tops.
//   2. Multiplies _BaseMap_ST.xy by 8 on all pack wall/floor materials
//      (or sets to 8,8 if currently 1,1).
//   3. Creates a Hospital_Ceiling_Tinted.mat (clone of floor mat with a
//      RGB(220,215,205) base color) and assigns it to every ceiling tile.
//   4. Sets RenderSettings.ambientLight to a neutral grey at strength
//      0.30 in every scene.
//   5. Spawns Point Lights along corridor / room ceilings at 6500K with
//      intensity 0.5.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.IO;

public static class HospitalPackFixGaps
{
    const string PackMatDir = "Assets/Dnk_Dev/HospitalHorrorPack/Models/Materials";
    const string PackPrefabDir = "Assets/Dnk_Dev/HospitalHorrorPack/Prefab";
    const string OurMatDir = "Assets/Materials";
    const float CEIL_DROP = 0.10f;   // distance pack ceilings sit above wall tops in the broken state
    const float TILING = 8f;         // target tiling factor for all pack architecture materials

    static Material _ceilingTinted;
    static GameObject _floor01Prefab;

    // ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Fix Hospital Pack Gaps + Lighting")]
    public static void Run()
    {
        Debug.Log("=== FixGaps START ===");
        TightenPackTextures();
        EnsureCeilingTintedMat();
        _floor01Prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Floor_01.prefab");

        ApplyScene("Assets/Scenes/Hospital.unity",         "1F");
        ApplyScene("Assets/Scenes/Hospital2F.unity",       "2F");
        ApplyScene("Assets/Scenes/Hospital3F.unity",       "3F");
        ApplyScene("Assets/Scenes/HospitalBasement.unity", "Bsm");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== FixGaps DONE ===");
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    // ── Bump tiling on all relevant pack materials ────────────────────
    static void TightenPackTextures()
    {
        string[] names = {
            "Mat_Walllime01_C", "Mat_Walllime02",
            "Mat_Tile01", "Mat_Tile02",
        };
        foreach (var n in names)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{PackMatDir}/{n}.mat");
            if (mat == null) { Debug.LogWarning($"Material missing: {n}"); continue; }
            // _BaseMap is URP/Lit; _MainTex is the legacy fallback the pack ships with.
            // Set both so we cover whichever shader the .mat actually targets.
            mat.SetTextureScale("_BaseMap", new Vector2(TILING, TILING));
            mat.SetTextureScale("_MainTex", new Vector2(TILING, TILING));
            EditorUtility.SetDirty(mat);
            Debug.Log($"  Tiled {n} -> {TILING}x{TILING}");
        }
    }

    // ── Build a brighter ceiling material from the floor mat ──────────
    static void EnsureCeilingTintedMat()
    {
        string p = $"{OurMatDir}/Hospital_Ceiling_Tinted.mat";
        _ceilingTinted = AssetDatabase.LoadAssetAtPath<Material>(p);
        // We use Mat_Tile01 as the base since the floor prefab references it.
        var src = AssetDatabase.LoadAssetAtPath<Material>($"{PackMatDir}/Mat_Tile01.mat");
        if (_ceilingTinted == null)
        {
            _ceilingTinted = new Material(src != null ? src : AssetDatabase.LoadAssetAtPath<Material>($"{PackMatDir}/Mat_Walllime01_C.mat"));
            AssetDatabase.CreateAsset(_ceilingTinted, p);
        }
        else if (src != null)
        {
            _ceilingTinted.CopyPropertiesFromMaterial(src);
        }
        _ceilingTinted.SetColor("_BaseColor", new Color(220f / 255f, 215f / 255f, 205f / 255f));
        _ceilingTinted.SetColor("_Color",     new Color(220f / 255f, 215f / 255f, 205f / 255f));
        _ceilingTinted.SetTextureScale("_BaseMap", new Vector2(TILING, TILING));
        _ceilingTinted.SetTextureScale("_MainTex", new Vector2(TILING, TILING));
        EditorUtility.SetDirty(_ceilingTinted);
    }

    // ──────────────────────────────────────────────────────────────────
    // Scene-by-scene fix
    // ──────────────────────────────────────────────────────────────────
    static void ApplyScene(string scenePath, string label)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"Cannot open {scenePath}"); return; }
        var root = GameObject.Find($"PackArch_{label}");
        if (root == null) { Debug.LogWarning($"PackArch_{label} not found"); return; }

        // 1. Drop ceiling tiles to wall-top height and retint them. We detect
        //    a ceiling by checking the transform's world-space "up" vector:
        //    floor tiles point +Y, ceiling tiles (rotated 180° around X) point -Y.
        int ceilingsFixed = 0;
        foreach (Transform child in root.transform)
        {
            if (!(child.name.StartsWith("P_Floor_01") || child.name.StartsWith("P_Ceiling_01"))) continue;
            bool isCeiling = child.up.y < -0.5f;
            if (!isCeiling) continue;
            var p = child.position;
            child.position = new Vector3(p.x, p.y - CEIL_DROP, p.z);
            foreach (var r in child.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = _ceilingTinted;
                r.sharedMaterials = mats;
            }
            EditorUtility.SetDirty(child.gameObject);
            ceilingsFixed++;
        }
        Debug.Log($"[{label}] ceilings adjusted: {ceilingsFixed}");

        // 2. Extend wall heights upward by CEIL_DROP so they meet the (now
        //    slightly lower) ceiling line cleanly. We do this by editing
        //    P_Wall_01 / P_Wall_02 children: increase Y scale so wall top
        //    moves up by 0.10 m, then bump position so the bottom stays at 0.
        //    (Pack walls are pivoted at the BOTTOM-LEFT corner.)
        int wallsAdjusted = 0;
        foreach (Transform child in root.transform)
        {
            if (!(child.name.StartsWith("P_Wall_01") || child.name.StartsWith("P_Wall_02"))) continue;
            // Walls have local rotations of 0/90/180/270° around Y; vertical scale
            // is in localScale.y regardless.
            var ls = child.localScale;
            // Original tile is 3 m tall; scaleY scales accordingly. Bump scaleY
            // so total height grows by CEIL_DROP (0.10 m).
            float currentHeight = 3f * ls.y;
            float newHeight = currentHeight + CEIL_DROP;
            child.localScale = new Vector3(ls.x, newHeight / 3f, ls.z);
            wallsAdjusted++;
        }
        Debug.Log($"[{label}] walls extended: {wallsAdjusted}");

        // 3. Ambient & ceiling lights.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.30f, 0.32f);

        // Place ceiling Point Lights based on corridor / room geometry of each scene.
        var lightsRoot = EnsureRoot("CeilingLights_" + label);
        SpawnCeilingLights(label, lightsRoot);

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[{label}] saved");
    }

    // ── Ceiling point lights (per scene layout) ───────────────────────
    static void SpawnCeilingLights(string label, Transform parent)
    {
        Color k6500 = new Color(1.00f, 0.96f, 0.92f);  // 6500K-ish

        switch (label)
        {
            case "1F":
                // Corridor 4×3×32, z:-16..+16
                for (int i = 0; i < 9; i++)
                    SpawnPoint($"Ceil_1F_Corr_{i}", new Vector3(0, 2.85f, -14 + i * 3.5f), 0.5f, 7f, k6500, parent);
                // Patient rooms x=-6, z=5/11/17, room 4×3×5
                foreach (float z in new[] { 5f, 11f, 17f })
                    SpawnPoint($"Ceil_1F_PR_{z:F0}", new Vector3(-6f, 2.85f, z), 0.5f, 5f, k6500, parent);
                SpawnPoint("Ceil_1F_Reception", new Vector3(0, 2.85f, -14f), 0.5f, 6f, k6500, parent);
                SpawnPoint("Ceil_1F_DirectorRoom", new Vector3(6f, 2.85f, 0), 0.5f, 5f, k6500, parent);
                break;
            case "2F":
                // Corridor 4×3×48, z:-24..+24
                for (int i = 0; i < 13; i++)
                    SpawnPoint($"Ceil_2F_Corr_{i}", new Vector3(0, 2.85f, -22 + i * 3.7f), 0.5f, 7f, k6500, parent);
                // Patient rooms left x=-7, right x=+7 at various z
                foreach (float z in new[] { -20f, -13f, -6f, 1f, 8f, 15f })
                    SpawnPoint($"Ceil_2F_PRL_{z:F0}", new Vector3(-7f, 2.85f, z), 0.5f, 5f, k6500, parent);
                foreach (float z in new[] { -20f, -13f, 9f, 16f })
                    SpawnPoint($"Ceil_2F_PRR_{z:F0}", new Vector3(7f, 2.85f, z), 0.5f, 5f, k6500, parent);
                SpawnPoint("Ceil_2F_Nurse", new Vector3(8f, 2.85f, 0), 0.5f, 6f, k6500, parent);
                SpawnPoint("Ceil_2F_TreatA", new Vector3(-7, 2.85f, 20), 0.5f, 5f, k6500, parent);
                SpawnPoint("Ceil_2F_TreatB", new Vector3( 7, 2.85f, 20), 0.5f, 5f, k6500, parent);
                break;
            case "3F":
                // Corridor 3×2.8×44, z:-22..+22
                for (int i = 0; i < 11; i++)
                    SpawnPoint($"Ceil_3F_Corr_{i}", new Vector3(0, 2.65f, -20 + i * 4f), 0.5f, 7f, k6500, parent);
                // Isolation rooms left x=-7, right x=+7
                foreach (float z in new[] { -18f, -12f, -6f, 0f, 6f, 12f })
                    SpawnPoint($"Ceil_3F_IsoL_{z:F0}", new Vector3(-7f, 2.65f, z), 0.5f, 5f, k6500, parent);
                foreach (float z in new[] { -18f, -12f, -6f, 6f, 12f })
                    SpawnPoint($"Ceil_3F_IsoR_{z:F0}", new Vector3(7f, 2.65f, z), 0.5f, 5f, k6500, parent);
                SpawnPoint("Ceil_3F_PlayerOwn", new Vector3(7f, 2.65f, 0), 0.5f, 5f, k6500, parent);
                SpawnPoint("Ceil_3F_Obs", new Vector3(0, 2.65f, 18f), 0.5f, 6f, k6500, parent);
                break;
            case "Bsm":
                // Basement keeps the red emergency vibe so we only add a few
                // weak ceiling lights at half intensity & 4500K (slightly warm).
                Color warm = new Color(1.0f, 0.85f, 0.65f);
                for (int i = 0; i < 7; i++)
                    SpawnPoint($"Ceil_Bsm_Corr_{i}", new Vector3(0, 2.45f, -14 + i * 4.5f), 0.30f, 6f, warm, parent);
                SpawnPoint("Ceil_Bsm_Record", new Vector3(-10, 2.45f, 0), 0.30f, 7f, warm, parent);
                SpawnPoint("Ceil_Bsm_Archive", new Vector3(10, 2.45f, -6), 0.30f, 6f, warm, parent);
                SpawnPoint("Ceil_Bsm_MedStorage", new Vector3(10, 2.45f, 8), 0.30f, 6f, warm, parent);
                break;
        }
    }

    static void SpawnPoint(string name, Vector3 pos, float intensity, float range, Color color, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.intensity = intensity;
        l.range = range;
        l.color = color;
        l.shadows = LightShadows.None;
    }

    static Transform EnsureRoot(string name)
    {
        var existing = GameObject.Find(name);
        if (existing != null)
        {
            for (int i = existing.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(existing.transform.GetChild(i).gameObject);
            return existing.transform;
        }
        return new GameObject(name).transform;
    }

    // ──────────────────────────────────────────────────────────────────
    // Screenshots: 3 shots (2F corridor / 1F corridor / 1F patient room)
    // ──────────────────────────────────────────────────────────────────
    public static void CaptureThree()
    {
        try
        {
            string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";
            CaptureScene("Assets/Scenes/Hospital2F.unity", outDir + "/2F_Corridor_fix.png",
                new Vector3(0f, 1.65f, -12f), new Vector3(2f, 0f, 0f), null);
            CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_Corridor_fix.png",
                new Vector3(0.0f, 1.65f, 4f), new Vector3(2f, 180f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });
            CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_PatientRoom_fix.png",
                new Vector3(-4.5f, 1.65f, 4.0f), new Vector3(2f, -90f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });
            Debug.Log("=== Three screenshots captured ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            EditorApplication.Exit(1);
        }
    }

    static void CaptureScene(string scenePath, string outPath, Vector3 camPos, Vector3 camRot,
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
        RenderSettings.ambientLight = savedAmbLight * 1.6f + new Color(0.08f, 0.08f, 0.09f);

        var fillGo = new GameObject("CaptureFill");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Point;
        fill.intensity = 1.0f;
        fill.range = 24f;
        fill.color = new Color(1f, 0.95f, 0.88f);
        fill.shadows = LightShadows.None;
        fillGo.transform.position = camPos + Vector3.up * 0.3f;

        var go = new GameObject("CaptureCam");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = camPos;
        cam.transform.eulerAngles = camRot;
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 120f;
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
