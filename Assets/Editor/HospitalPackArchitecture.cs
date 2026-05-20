// HospitalPackArchitecture.cs
//
// Replaces the procedural primitive architecture (walls / floors / ceilings)
// in all four scenes with the PBR Hospital Horror Pack prefabs.
//
//   *_Wall*, *Wall*       -> P_Wall_01 / P_Wall_02         tiled along the long axis
//   *_Floor*              -> P_Floor_01 / P_Floor_02       tiled across XZ
//   *_Ceiling*, *_Ceil*   -> P_Ceiling_01                  tiled across XZ
//   columns               -> P_Column_01                   1:1
//
// ExtraProps_*, CharacterShowcase_*, PackProps_* are NEVER touched.
//
// Because the pack textures are authored at a fixed UV scale, we tile
// multiple prefab instances rather than stretching one. The wall prefab
// is also Y-scaled to exactly match the existing wall height so we never
// see gaps at the floor/ceiling line.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.IO;

public static class HospitalPackArchitecture
{
    const string PackPrefabDir = "Assets/Dnk_Dev/HospitalHorrorPack/Prefab";

    static readonly string[] PreserveRoots = {
        "ExtraProps_1F", "ExtraProps_2F", "ExtraProps_3F", "ExtraProps_Bsm",
        "CharacterShowcase_1F",
        "PackProps_1F", "PackProps_2F", "PackProps_3F", "PackProps_Bsm",
    };

    // ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Apply PBR Hospital Pack Architecture")]
    public static void Run()
    {
        Debug.Log("=== HospitalPackArchitecture START ===");
        LoadPrefabs();
        MeasurePrefabs();
        Apply("Assets/Scenes/Hospital.unity", "1F");
        Apply("Assets/Scenes/Hospital2F.unity", "2F");
        Apply("Assets/Scenes/Hospital3F.unity", "3F");
        Apply("Assets/Scenes/HospitalBasement.unity", "Bsm");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalPackArchitecture DONE ===");
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    static GameObject _wall01, _wall02, _floor01, _floor02, _ceiling01, _column01;
    static Vector3 _wall01Size, _wall02Size, _floor01Size, _floor02Size, _ceiling01Size, _column01Size;
    static Vector3 _wall01Center, _wall02Center, _floor01Center, _floor02Center, _ceiling01Center, _column01Center;

    static void LoadPrefabs()
    {
        _wall01    = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Wall_01.prefab");
        _wall02    = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Wall_02.prefab");
        _floor01   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Floor_01.prefab");
        _floor02   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Floor_02.prefab");
        _ceiling01 = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Ceiling_01.prefab");
        _column01  = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Column_01.prefab");
        foreach (var (n, p) in new (string, GameObject)[] {
            ("P_Wall_01", _wall01), ("P_Wall_02", _wall02),
            ("P_Floor_01", _floor01), ("P_Floor_02", _floor02),
            ("P_Ceiling_01", _ceiling01), ("P_Column_01", _column01) })
        {
            if (p == null) Debug.LogWarning($"Missing prefab: {n}");
        }
    }

    static (Vector3 size, Vector3 center) Measure(GameObject prefab)
    {
        if (prefab == null) return (Vector3.one, Vector3.zero);
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        temp.transform.position = Vector3.zero;
        temp.transform.rotation = Quaternion.identity;
        temp.transform.localScale = Vector3.one;
        var bounds = new Bounds();
        bool first = true;
        foreach (var r in temp.GetComponentsInChildren<Renderer>())
        {
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        Object.DestroyImmediate(temp);
        if (first) return (Vector3.one, Vector3.zero);
        return (bounds.size, bounds.center);
    }

    static void MeasurePrefabs()
    {
        (_wall01Size,    _wall01Center)    = Measure(_wall01);
        (_wall02Size,    _wall02Center)    = Measure(_wall02);
        (_floor01Size,   _floor01Center)   = Measure(_floor01);
        (_floor02Size,   _floor02Center)   = Measure(_floor02);
        (_ceiling01Size, _ceiling01Center) = Measure(_ceiling01);
        (_column01Size,  _column01Center)  = Measure(_column01);
        Debug.Log($"  P_Wall_01 size={_wall01Size} center={_wall01Center}");
        Debug.Log($"  P_Wall_02 size={_wall02Size} center={_wall02Center}");
        Debug.Log($"  P_Floor_01 size={_floor01Size} center={_floor01Center}");
        Debug.Log($"  P_Floor_02 size={_floor02Size} center={_floor02Center}");
        Debug.Log($"  P_Ceiling_01 size={_ceiling01Size} center={_ceiling01Center}");
        Debug.Log($"  P_Column_01 size={_column01Size} center={_column01Center}");
    }

    // ──────────────────────────────────────────────────────────────────
    static bool UnderPreservedRoot(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            foreach (var n in PreserveRoots)
                if (p.name == n) return true;
        return false;
    }

    static bool IsWallName(string n) =>
        (n.Contains("_Wall") || n.EndsWith("Wall") || n.Contains("WallS") || n.Contains("WallN") ||
         n.Contains("WallL") || n.Contains("WallR") || n.Contains("WallFront") || n.Contains("WallBack") ||
         n.Contains("WallLeft") || n.Contains("WallRight"));

    static bool IsFloorName(string n) =>
        n.EndsWith("_Floor") || n.Contains("Corridor") && n.Contains("Floor") || n.EndsWith("Floor");

    static bool IsCeilingName(string n) =>
        n.EndsWith("_Ceiling") || n.EndsWith("_Ceil") || n.Contains("Ceiling");

    static bool IsColumnName(string n) =>
        n.Contains("Column") || n.Contains("Pillar") || n.Contains("Pipe_V");

    // ──────────────────────────────────────────────────────────────────
    // Tile placement helpers
    // ──────────────────────────────────────────────────────────────────

    // Tile a flat slab prefab across X and Z to cover the existing primitive's footprint.
    // For each tile we compute the world-space target for the tile's bounds CENTER,
    // then back-compute inst.position so the rotated+scaled mesh lands there.
    static int TileSlab(MeshRenderer existing, GameObject prefab, Vector3 prefabSize, Vector3 prefabCenter,
                         Transform parent, bool ceiling)
    {
        if (prefab == null) return 0;
        var pos = existing.transform.position;
        var s = existing.transform.lossyScale;
        float w = Mathf.Abs(s.x);
        float d = Mathf.Abs(s.z);
        if (prefabSize.x < 0.1f || prefabSize.z < 0.1f) return 0;
        int nx = Mathf.Max(1, Mathf.CeilToInt(w / prefabSize.x));
        int nz = Mathf.Max(1, Mathf.CeilToInt(d / prefabSize.z));
        float sx = w / (nx * prefabSize.x);
        float sz = d / (nz * prefabSize.z);
        float tileW = prefabSize.x * sx;
        float tileD = prefabSize.z * sz;
        float startCx = pos.x - w * 0.5f + tileW * 0.5f;
        float startCz = pos.z - d * 0.5f + tileD * 0.5f;
        float y = ceiling ? (pos.y + Mathf.Abs(s.y) * 0.5f) : (pos.y - Mathf.Abs(s.y) * 0.5f);
        Quaternion rot = ceiling ? Quaternion.Euler(180, 0, 0) : Quaternion.identity;
        Vector3 localScale = new Vector3(sx, 1f, sz);
        Vector3 scaledLocalCenter = Vector3.Scale(prefabCenter, localScale);
        Vector3 worldCenterOffset = rot * scaledLocalCenter;

        int count = 0;
        for (int ix = 0; ix < nx; ix++)
        {
            for (int iz = 0; iz < nz; iz++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                inst.transform.localScale = localScale;
                inst.transform.rotation = rot;
                Vector3 targetCenter = new Vector3(startCx + ix * tileW, y, startCz + iz * tileD);
                inst.transform.position = targetCenter - worldCenterOffset;
                count++;
            }
        }
        return count;
    }

    // Tile a wall prefab along the wall's long axis. Yaw is chosen from the
    // wall's NAME so its visible face always points into the room/corridor
    // it borders. Prefabs in the pack are single-sided (~0 thickness), so
    // orientation matters.
    //
    // Naming rules (per HospitalBuilderUtils):
    //   *WallLeft / *WallL   -> visible face = +X    (room extends to +X)
    //   *WallRight / *WallR  -> visible face = -X    (room extends to -X)
    //   *WallFront / *WallS  -> visible face = +Z
    //   *WallBack / *WallN   -> visible face = -Z
    // We rotate the prefab (whose natural face points +Z) around Y by:
    //   +Z -> 0, +X -> -90 (so prefab's +X becomes world +Z then -90 around Y → +X), etc.
    static int TileWall(MeshRenderer existing, GameObject prefab, Vector3 prefabSize, Vector3 prefabCenter,
                         Transform parent)
    {
        if (prefab == null) return 0;
        var pos = existing.transform.position;
        var s = existing.transform.lossyScale;
        float xSpan = Mathf.Abs(s.x);
        float ySpan = Mathf.Abs(s.y);
        float zSpan = Mathf.Abs(s.z);
        // Wall axis: long horizontal axis (X or Z); we tile along this axis.
        bool wallAlongZ = zSpan >= xSpan;
        float length = wallAlongZ ? zSpan : xSpan;
        float height = ySpan;
        // Yaw chosen from name so the visible face points into the room.
        float yaw = ChooseYaw(existing.gameObject.name, wallAlongZ);
        Quaternion rot = Quaternion.Euler(0, yaw, 0);

        // The pack wall prefab is authored as a 3m × 3m quad facing +Z with
        // bounds center at (1.5, 1.5, 0). Its long axis is +X.
        float prefabLongAxis = Mathf.Max(prefabSize.x, prefabSize.z);
        float prefabHeight   = Mathf.Max(prefabSize.y, 0.01f);
        if (prefabLongAxis < 0.1f) return 0;
        int n = Mathf.Max(1, Mathf.CeilToInt(length / prefabLongAxis));
        float scaleAlong = length / (n * prefabLongAxis);
        float scaleY     = height / prefabHeight;

        float tileLen = prefabLongAxis * scaleAlong;
        float centerY = pos.y - ySpan * 0.5f + (prefabHeight * scaleY) * 0.5f;
        Vector3 step = wallAlongZ ? Vector3.forward : Vector3.right;
        Vector3 startCenter = wallAlongZ
            ? new Vector3(pos.x, centerY, pos.z - length * 0.5f + tileLen * 0.5f)
            : new Vector3(pos.x - length * 0.5f + tileLen * 0.5f, centerY, pos.z);

        Vector3 localScale = new Vector3(scaleAlong, scaleY, scaleAlong);
        Vector3 scaledLocalCenter = Vector3.Scale(prefabCenter, localScale);
        Vector3 worldCenterOffset = rot * scaledLocalCenter;

        int count = 0;
        for (int i = 0; i < n; i++)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.transform.rotation = rot;
            inst.transform.localScale = localScale;
            Vector3 targetCenter = startCenter + step * (i * tileLen);
            inst.transform.position = targetCenter - worldCenterOffset;
            count++;
        }
        return count;
    }

    static float ChooseYaw(string name, bool wallAlongZ)
    {
        // Prefab natural visible face: +Z. We rotate around Y to point it
        // somewhere else. Convention: yaw rotates +Z -> direction listed.
        //   yaw   0 -> +Z
        //   yaw  90 -> +X
        //   yaw 180 -> -Z
        //   yaw 270 -> -X
        if (name.Contains("WallLeft") || name.EndsWith("WallL") || name.Contains("_WallL"))
            return 90f;  // visible +X
        if (name.Contains("WallRight") || name.EndsWith("WallR") || name.Contains("_WallR"))
            return 270f; // visible -X
        if (name.Contains("WallFront") || name.EndsWith("WallS") || name.Contains("_WallS"))
            return 0f;   // visible +Z
        if (name.Contains("WallBack") || name.EndsWith("WallN") || name.Contains("_WallN"))
            return 180f; // visible -Z
        // Fallback heuristic: walls along Z (thin in X) typically face +X.
        return wallAlongZ ? 90f : 0f;
    }

    // ──────────────────────────────────────────────────────────────────
    // Per-scene
    // ──────────────────────────────────────────────────────────────────
    static void Apply(string scenePath, string label)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"Cannot open {scenePath}"); return; }
        var root = EnsureRoot($"PackArch_{label}");

        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var walls = new List<MeshRenderer>();
        var floors = new List<MeshRenderer>();
        var ceilings = new List<MeshRenderer>();
        var columns = new List<MeshRenderer>();
        foreach (var r in renderers)
        {
            var go = r.gameObject;
            if (UnderPreservedRoot(go.transform)) continue;
            var n = go.name;
            // We only operate on primitives (likely cubes); skip if it's part of a complex prefab
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.sharedMesh.name != "Cube" && mf.sharedMesh.name != "Cube Instance") continue;
            if (IsColumnName(n)) { columns.Add(r); continue; }
            if (IsCeilingName(n)) { ceilings.Add(r); continue; }
            if (IsFloorName(n))   { floors.Add(r); continue; }
            if (IsWallName(n))    { walls.Add(r); continue; }
        }
        Debug.Log($"[{label}] walls={walls.Count} floors={floors.Count} ceilings={ceilings.Count} columns={columns.Count}");

        int wallCnt = 0;
        foreach (var w in walls)
        {
            var s = w.transform.lossyScale;
            var prefab = (Mathf.RoundToInt(w.transform.position.z) % 2 == 0) ? _wall01 : _wall02;
            var size  = (prefab == _wall01) ? _wall01Size : _wall02Size;
            var cent  = (prefab == _wall01) ? _wall01Center : _wall02Center;
            wallCnt += TileWall(w, prefab, size, cent, root);
            Object.DestroyImmediate(w.gameObject);
        }
        Debug.Log($"[{label}] wall tiles placed: {wallCnt}");

        int floorCnt = 0;
        foreach (var f in floors)
        {
            // Alternate floor variants for visual variety
            var prefab = (Mathf.RoundToInt(f.transform.position.x + f.transform.position.z) % 3 == 0) ? _floor02 : _floor01;
            var size = (prefab == _floor01) ? _floor01Size : _floor02Size;
            var cent = (prefab == _floor01) ? _floor01Center : _floor02Center;
            floorCnt += TileSlab(f, prefab, size, cent, root, ceiling: false);
            Object.DestroyImmediate(f.gameObject);
        }
        Debug.Log($"[{label}] floor tiles placed: {floorCnt}");

        // Ceilings: use the FLOOR prefab flipped 180° around X so the
        // mesh's natural visible face (which we know works as a floor when
        // viewed from above) ends up facing -Y and is visible from below.
        // This avoids any ambiguity about the ceiling prefab's authored
        // normal direction.
        int ceilCnt = 0;
        foreach (var c in ceilings)
        {
            ceilCnt += TileSlab(c, _floor01, _floor01Size, _floor01Center, root, ceiling: true);
            Object.DestroyImmediate(c.gameObject);
        }
        Debug.Log($"[{label}] ceiling tiles placed: {ceilCnt}");

        int columnCnt = 0;
        foreach (var c in columns)
        {
            var pos = c.transform.position;
            var s = c.transform.lossyScale;
            float h = Mathf.Abs(s.y);
            if (h < 0.5f) continue;
            Object.DestroyImmediate(c.gameObject);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(_column01, root);
            inst.transform.position = new Vector3(pos.x, pos.y - h * 0.5f, pos.z);
            float ySc = h / Mathf.Max(0.5f, _column01Size.y);
            inst.transform.localScale = new Vector3(1f, ySc, 1f);
            columnCnt++;
        }
        Debug.Log($"[{label}] columns placed: {columnCnt}");

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[{label}] saved");
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
    // Screenshots
    // ──────────────────────────────────────────────────────────────────
    public static void CaptureFive()
    {
        try
        {
            string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";
            CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_PatientRoom_pack.png",
                new Vector3(-4.5f, 1.65f, 4.0f), new Vector3(6f, -90f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });
            CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_Corridor_pack.png",
                new Vector3(0.0f, 1.65f, 4f), new Vector3(6f, 180f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });
            CaptureScene("Assets/Scenes/Hospital2F.unity", outDir + "/2F_Corridor_pack.png",
                new Vector3(0f, 1.65f, -12f), new Vector3(6f, 0f, 0f));
            CaptureScene("Assets/Scenes/Hospital3F.unity", outDir + "/3F_Corridor_pack.png",
                new Vector3(0f, 1.55f, -16f), new Vector3(5f, 0f, 0f));
            CaptureScene("Assets/Scenes/HospitalBasement.unity", outDir + "/Basement_pack.png",
                new Vector3(0f, 1.55f, -14f), new Vector3(5f, 0f, 0f));
            Debug.Log("=== Five screenshots captured ===");
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
