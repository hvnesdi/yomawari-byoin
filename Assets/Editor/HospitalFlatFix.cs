// HospitalFlatFix.cs
//
// Replaces the rock/stone-looking wall textures with FLAT painted plaster
// (no vertical streaks) and the dark floor with light linoleum tiles.
// Uses per-renderer material *instances* so that _BaseMap_ST is baked in
// at the material level - SRP Batcher ignores MaterialPropertyBlocks
// for _BaseMap_ST, so MPB-based tiling silently fails on URP/Lit.
//
// Also lightens 2F: ambient 0.20, all corridor point lights -> 0.7.
//
// Entry points:
//   Tools/Apply Flat Hospital Walls + Floors    (interactive)
//   HospitalFlatFix.RunBatch                    (-executeMethod)
//   HospitalFlatFix.CaptureTwoShots             (2F corridor + 1F room PNGs)

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.IO;

public static class HospitalFlatFix
{
    const string MatDir = "Assets/Materials";
    const string TileMatDir = "Assets/Materials/_Tile";
    const string GenTex = "Assets/Textures/Generated";

    // 1 wall tile == 3 m; 1 floor tile == 2.4 m (4 × 60cm linoleum squares).
    const float WALL_M = 3f;
    const float FLOOR_M = 2.4f;

    // ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Apply Flat Hospital Walls + Floors")]
    public static void Run()
    {
        Debug.Log("=== HospitalFlatFix START ===");
        EnsureFolder(MatDir);
        EnsureFolder(TileMatDir);
        ConfigureTextureImports();
        BuildBaseMaterials();
        Apply1F();
        Apply2F();
        Apply3F();
        ApplyBasement();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalFlatFix DONE ===");
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    // ──────────────────────────────────────────────────────────────────
    // Texture import config
    // ──────────────────────────────────────────────────────────────────
    static void ConfigureTextureImports()
    {
        foreach (var f in Directory.GetFiles(GenTex, "Plaster_*.png"))
            ImportSRGB(f, repeat: true);
        foreach (var f in Directory.GetFiles(GenTex, "Floor_Linoleum_*.png"))
            ImportSRGB(f, repeat: true);
        foreach (var f in Directory.GetFiles(GenTex, "Concrete_Basement_Flat.png"))
            ImportSRGB(f, repeat: true);
    }

    static void ImportSRGB(string fullPath, bool repeat)
    {
        var rel = fullPath.Replace("\\", "/");
        int idx = rel.IndexOf("Assets/");
        if (idx < 0) return;
        rel = rel.Substring(idx);
        var imp = AssetImporter.GetAtPath(rel) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.maxTextureSize = 2048;
        imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 8;
        imp.mipmapEnabled = true;
        imp.SaveAndReimport();
    }

    // ──────────────────────────────────────────────────────────────────
    // Materials (base templates - per-renderer instances clone from these)
    // ──────────────────────────────────────────────────────────────────
    static Material _wallCream, _wallGreen, _wallConcrete;
    static Material _floorLino, _floorLinoWorn, _floorConcrete;
    static Material _ceiling;

    static void BuildBaseMaterials()
    {
        _wallCream    = MakeUrpLit("Plaster_Wall_Base",    $"{GenTex}/Plaster_Wall.png",       smooth: 0.05f);
        _wallGreen    = MakeUrpLit("Plaster_Wall_Green",   $"{GenTex}/Plaster_Wall_Green.png", smooth: 0.08f);
        _wallConcrete = MakeUrpLit("Concrete_Wall_Flat",   $"{GenTex}/Concrete_Basement_Flat.png", smooth: 0.06f);
        _floorLino    = MakeUrpLit("Floor_Linoleum_Flat",  $"{GenTex}/Floor_Linoleum_Flat.png",  smooth: 0.22f);
        _floorLinoWorn= MakeUrpLit("Floor_Linoleum_Worn",  $"{GenTex}/Floor_Linoleum_Worn.png",  smooth: 0.20f);
        _floorConcrete= MakeUrpLit("Concrete_Floor_Flat", $"{GenTex}/Concrete_Basement_Flat.png", smooth: 0.10f);
        _ceiling      = MakeUrpLit("Plaster_Ceiling",      $"{GenTex}/Plaster_Ceiling.png",      smooth: 0.04f);
    }

    static Material MakeUrpLit(string name, string diffPath, float smooth)
    {
        string p = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, p);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
        if (diff != null) mat.SetTexture("_BaseMap", diff);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", smooth);
        mat.DisableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ──────────────────────────────────────────────────────────────────
    // Per-renderer tiled material instance cache
    // Key bucket-rounds tile values to 1 decimal so tiny scale diffs share
    // a material asset.
    // ──────────────────────────────────────────────────────────────────
    static Dictionary<string, Material> _tileCache = new Dictionary<string, Material>();

    static Material GetTiled(Material baseMat, Vector2 tile)
    {
        float rx = Mathf.Max(0.25f, Mathf.Round(tile.x * 4f) / 4f);
        float ry = Mathf.Max(0.25f, Mathf.Round(tile.y * 4f) / 4f);
        string key = $"{baseMat.name}__{rx:F2}x{ry:F2}";
        if (_tileCache.TryGetValue(key, out var cached)) return cached;
        string p = $"{TileMatDir}/{key}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null)
        {
            mat = new Material(baseMat);
            mat.name = key;
            AssetDatabase.CreateAsset(mat, p);
        }
        else
        {
            mat.CopyPropertiesFromMaterial(baseMat);
        }
        mat.mainTextureScale = new Vector2(rx, ry);
        EditorUtility.SetDirty(mat);
        _tileCache[key] = mat;
        return mat;
    }

    // ──────────────────────────────────────────────────────────────────
    // Renderer matching helpers
    // ──────────────────────────────────────────────────────────────────
    static bool IsWall(string n)
    {
        if (n.Contains("_Wall") || n.EndsWith("Wall") || n.Contains("WallL") || n.Contains("WallR") ||
            n.Contains("WallS") || n.Contains("WallN") || n.Contains("WallFront") || n.Contains("WallBack") ||
            n.Contains("WallLeft") || n.Contains("WallRight")) return true;
        return false;
    }
    static bool IsFloor(string n) =>
        n.EndsWith("_Floor") || n.EndsWith("Floor") || (n.Contains("_Floor") && !n.Contains("Wall"));
    static bool IsCeiling(string n) =>
        n.EndsWith("_Ceiling") || n.EndsWith("_Ceil") || n.Contains("Ceiling") || n.Contains("Ceil");

    // Apply wall material to all wall renderers under root pattern. floorExclude removes floor pieces.
    static int ApplyWallsByPattern(string root, Material baseMat)
    {
        int count = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var n = r.gameObject.name;
            if (!n.Contains(root)) continue;
            if (!IsWall(n)) continue;
            if (IsFloor(n) || IsCeiling(n)) continue;
            var tile = ComputeWallTile(r);
            var mat = GetTiled(baseMat, tile);
            AssignSingle(r, mat);
            count++;
        }
        if (count > 0) Debug.Log($"  Wall '{root}' x{count}");
        return count;
    }

    static int ApplyFloorsByPattern(string root, Material baseMat)
    {
        int count = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var n = r.gameObject.name;
            if (!n.Contains(root)) continue;
            if (!IsFloor(n)) continue;
            if (IsWall(n) || IsCeiling(n)) continue;
            var tile = ComputeFloorTile(r);
            var mat = GetTiled(baseMat, tile);
            AssignSingle(r, mat);
            count++;
        }
        if (count > 0) Debug.Log($"  Floor '{root}' x{count}");
        return count;
    }

    static int ApplyCeilingsByPattern(string root, Material baseMat)
    {
        int count = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var n = r.gameObject.name;
            if (!n.Contains(root)) continue;
            if (!IsCeiling(n)) continue;
            var tile = ComputeFloorTile(r);  // ceiling uses the same XZ math as floor
            var mat = GetTiled(baseMat, tile);
            AssignSingle(r, mat);
            count++;
        }
        if (count > 0) Debug.Log($"  Ceil  '{root}' x{count}");
        return count;
    }

    static void AssignSingle(MeshRenderer r, Material mat)
    {
        var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
        r.sharedMaterials = mats;
        // Clear any stale MPB that older runs may have set on _BaseMap_ST.
        r.SetPropertyBlock(null);
        EditorUtility.SetDirty(r);
    }

    static Vector2 ComputeWallTile(MeshRenderer r)
    {
        var s = r.transform.lossyScale;
        float horiz = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        float vert = Mathf.Abs(s.y);
        return new Vector2(horiz / WALL_M, vert / WALL_M);
    }

    static Vector2 ComputeFloorTile(MeshRenderer r)
    {
        var s = r.transform.lossyScale;
        return new Vector2(Mathf.Abs(s.x) / FLOOR_M, Mathf.Abs(s.z) / FLOOR_M);
    }

    // ──────────────────────────────────────────────────────────────────
    // Per-floor scene apply
    // ──────────────────────────────────────────────────────────────────
    static void Apply1F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        ApplyWallsByPattern("Corridor", _wallCream);
        ApplyWallsByPattern("Reception", _wallCream);
        ApplyWallsByPattern("DirectorRoom", _wallCream);
        ApplyWallsByPattern("PatientRoom", _wallCream);
        ApplyFloorsByPattern("Corridor", _floorLino);
        ApplyFloorsByPattern("Reception", _floorLino);
        ApplyFloorsByPattern("DirectorRoom", _floorLino);
        ApplyFloorsByPattern("PatientRoom", _floorLino);
        ApplyCeilingsByPattern("Corridor", _ceiling);
        ApplyCeilingsByPattern("Reception", _ceiling);
        ApplyCeilingsByPattern("DirectorRoom", _ceiling);
        ApplyCeilingsByPattern("PatientRoom", _ceiling);
        // 1F lighting baseline kept (it was already OK)
        SetAmbient(0.15f, new Color(0.95f, 0.95f, 0.93f));
        EditorSceneManager.SaveScene(scene);
        Debug.Log("1F applied");
    }

    static void Apply2F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        ApplyWallsByPattern("Corridor2F", _wallCream);
        ApplyWallsByPattern("NurseStation", _wallCream);
        ApplyWallsByPattern("TreatmentRoom", _wallCream);
        ApplyWallsByPattern("PatientRoom2F", _wallCream);
        ApplyFloorsByPattern("Corridor2F", _floorLino);
        ApplyFloorsByPattern("NurseStation", _floorLino);
        ApplyFloorsByPattern("TreatmentRoom", _floorLino);
        ApplyFloorsByPattern("PatientRoom2F", _floorLino);
        ApplyCeilingsByPattern("Corridor2F", _ceiling);
        ApplyCeilingsByPattern("NurseStation", _ceiling);
        ApplyCeilingsByPattern("TreatmentRoom", _ceiling);
        ApplyCeilingsByPattern("PatientRoom2F", _ceiling);

        // 2F lighting: ambient 0.20, all corridor lights -> 0.7
        SetAmbient(0.20f, new Color(0.96f, 0.95f, 0.92f));
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Point && (l.gameObject.name.Contains("CorrLight2F") || l.gameObject.name.Contains("Light2F")))
            {
                l.intensity = 0.7f;
                l.color = new Color(0.96f, 0.95f, 0.88f);
                l.range = Mathf.Max(l.range, 9f);
                EditorUtility.SetDirty(l);
            }
            else if (l.type == LightType.Spot)
            {
                l.intensity = Mathf.Max(l.intensity, 0.4f);
                EditorUtility.SetDirty(l);
            }
            if (l.type == LightType.Directional)
            {
                // keep directional disabled (atmosphere)
                l.gameObject.SetActive(false);
                EditorUtility.SetDirty(l);
            }
        }
        EditorSceneManager.SaveScene(scene);
        Debug.Log("2F applied (lighting bumped: ambient 0.20, lights 0.7)");
    }

    static void Apply3F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        ApplyWallsByPattern("Corridor3F", _wallCream);
        ApplyWallsByPattern("IsoRoom", _wallCream);
        ApplyWallsByPattern("PlayerOwnRoom", _wallCream);
        ApplyWallsByPattern("ObservationRoom", _wallCream);
        ApplyFloorsByPattern("Corridor3F", _floorLinoWorn);
        ApplyFloorsByPattern("IsoRoom", _floorLinoWorn);
        ApplyFloorsByPattern("PlayerOwnRoom", _floorLinoWorn);
        ApplyFloorsByPattern("ObservationRoom", _floorLinoWorn);
        ApplyCeilingsByPattern("Corridor3F", _ceiling);
        ApplyCeilingsByPattern("IsoRoom", _ceiling);
        ApplyCeilingsByPattern("PlayerOwnRoom", _ceiling);
        ApplyCeilingsByPattern("ObservationRoom", _ceiling);
        SetAmbient(0.10f, new Color(0.88f, 0.88f, 0.95f));
        EditorSceneManager.SaveScene(scene);
        Debug.Log("3F applied");
    }

    static void ApplyBasement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);
        ApplyWallsByPattern("BaseCorridor", _wallConcrete);
        ApplyWallsByPattern("RecordRoom", _wallConcrete);
        ApplyWallsByPattern("DirectorArchive", _wallConcrete);
        ApplyWallsByPattern("MedStorage", _wallConcrete);
        ApplyWallsByPattern("HiddenPassage", _wallConcrete);
        ApplyFloorsByPattern("BaseCorridor", _floorConcrete);
        ApplyFloorsByPattern("RecordRoom", _floorConcrete);
        ApplyFloorsByPattern("DirectorArchive", _floorConcrete);
        ApplyFloorsByPattern("MedStorage", _floorConcrete);
        ApplyFloorsByPattern("HiddenPassage", _floorConcrete);
        ApplyCeilingsByPattern("BaseCorridor", _wallConcrete);
        ApplyCeilingsByPattern("RecordRoom", _wallConcrete);
        ApplyCeilingsByPattern("HiddenPassage", _wallConcrete);
        SetAmbient(0.05f, new Color(1f, 0.6f, 0.6f));
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Basement applied");
    }

    static void SetAmbient(float strength, Color tint)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = tint * strength;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace("\\", "/");
        string name = Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, name);
    }

    // ══════════════════════════════════════════════════════════════════
    // Screenshot capture (just the 2 the user asked for)
    // ══════════════════════════════════════════════════════════════════
    public static void CaptureTwoShots()
    {
        try
        {
            string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";
            CaptureScene("Assets/Scenes/Hospital2F.unity", outDir + "/2F_Corridor.png",
                new Vector3(0f, 1.7f, -22f), new Vector3(5f, 0f, 0f));
            CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_PatientRoom.png",
                new Vector3(-4.2f, 1.65f, 7.2f), new Vector3(8f, -130f, 0f));
            Debug.Log("=== Two screenshots captured ===");
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogError(e);
            EditorApplication.Exit(1);
        }
    }

    static void CaptureScene(string scenePath, string outPath, Vector3 camPos, Vector3 camRot)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Lift volume exposure slightly so dark scenes are visible without
        // wiping out atmosphere. Save & restore.
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
        fill.type = LightType.Point;
        fill.intensity = 1.2f;
        fill.range = 22f;
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
        rt.Release();
    }
}
