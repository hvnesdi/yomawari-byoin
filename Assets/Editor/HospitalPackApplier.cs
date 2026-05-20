// HospitalPackApplier.cs
//
// Replaces the primitive-built furniture in all four hospital scenes
// with the PBR Hospital Horror Pack prefabs.
//
//   Bed_*, *_Bed, HospitalBed_*, PlayerOwnRoom_Bed, BedFrame  -> P_Bed_01 + P_BedBedding
//   Door_*                                                    -> P_Door_01_  (with frame Base)
//   Light_*, Light2F_*, Light3F_*, CorrLight*, ArchLight_*    -> P_Lamp
//   _Locker, _Cabinet, MedShelf_, Shelf_, DirArch_Cabinet     -> P_Med_box_01
//
// Anything under "ExtraProps_*" (nurse counter / noticeboards / clocks
// / fire extinguishers) and "CharacterShowcase_*" is preserved.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System.IO;

public static class HospitalPackApplier
{
    const string PackPrefabDir = "Assets/Dnk_Dev/HospitalHorrorPack/Prefab";

    // Patterns of GO names whose CHILD or own name we should swap.
    // Object must be a primitive (MeshFilter using a built-in primitive mesh).
    static readonly string[] BedPatterns   = { "_Bed", "Bed_1", "Bed_2", "Bed_3", "HospitalBed_", "PlayerOwnRoom_Bed", "BedFrame" };
    static readonly string[] DoorPatterns  = { "Door_Room", "Door_" };
    static readonly string[] LampPatterns  = { "Light_", "Light2F_", "Light3F_", "CorrLight2F_", "CorrLight3F_",
                                                "ArchLight_", "EmergLight_", "EmergRed_", "BaseLight_", "Spot_",
                                                "IsoLight_", "FluorTube" };
    static readonly string[] MedBoxPatterns = { "_Locker", "_Cabinet", "MedShelf_", "Shelf_", "DirArch_Desk", "DirArch_Cabinet" };

    // Roots we MUST preserve - never delete children of these.
    static readonly string[] PreserveRoots = { "ExtraProps_1F", "ExtraProps_2F", "ExtraProps_3F", "ExtraProps_Bsm",
                                                "CharacterShowcase_1F" };

    // ──────────────────────────────────────────────────────────────────
    [MenuItem("Tools/Apply PBR Hospital Pack")]
    public static void Run()
    {
        Debug.Log("=== HospitalPackApplier START ===");
        VerifyPrefabs();
        Apply("Assets/Scenes/Hospital.unity", "1F", floorY: 0f);
        Apply("Assets/Scenes/Hospital2F.unity", "2F", floorY: 0f);
        Apply("Assets/Scenes/Hospital3F.unity", "3F", floorY: 0f);
        Apply("Assets/Scenes/HospitalBasement.unity", "Bsm", floorY: 0f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalPackApplier DONE ===");
    }

    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    static GameObject _bedPrefab, _bedBeddingPrefab, _doorPrefab, _doorBasePrefab, _lampPrefab, _medBoxPrefab;

    static void VerifyPrefabs()
    {
        _bedPrefab        = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Bed_01.prefab");
        _bedBeddingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_BedBedding.prefab");
        _doorPrefab       = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Door_01_.prefab");
        _doorBasePrefab   = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Door_01_Base.prefab");
        _lampPrefab       = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Lamp.prefab");
        _medBoxPrefab     = AssetDatabase.LoadAssetAtPath<GameObject>($"{PackPrefabDir}/P_Med_box_01.prefab");
        foreach (var (n, p) in new (string, GameObject)[] {
            ("P_Bed_01", _bedPrefab), ("P_BedBedding", _bedBeddingPrefab),
            ("P_Door_01_", _doorPrefab), ("P_Door_01_Base", _doorBasePrefab),
            ("P_Lamp", _lampPrefab), ("P_Med_box_01", _medBoxPrefab) })
        {
            if (p == null) Debug.LogWarning($"  Missing prefab: {n}");
            else           Debug.Log($"  Loaded prefab: {n}");
        }
    }

    // ──────────────────────────────────────────────────────────────────
    static bool UnderPreservedRoot(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
        {
            foreach (var n in PreserveRoots)
                if (p.name == n) return true;
        }
        return false;
    }

    static bool MatchesAny(string name, string[] patterns)
    {
        foreach (var p in patterns)
            if (name.Contains(p)) return true;
        return false;
    }

    // ──────────────────────────────────────────────────────────────────
    // Per-scene replacement
    // ──────────────────────────────────────────────────────────────────
    static void Apply(string scenePath, string label, float floorY)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"Cannot open {scenePath}"); return; }

        var packRoot = EnsureRoot($"PackProps_{label}");
        // First, capture all primitive furniture renderers + delete them.
        var beds   = CollectByPatterns(BedPatterns);
        var doors  = CollectByPatterns(DoorPatterns);
        var lamps  = CollectByPatterns(LampPatterns);
        var medbox = CollectByPatterns(MedBoxPatterns);

        // Replace beds
        int nBed = 0;
        foreach (var go in beds)
        {
            if (UnderPreservedRoot(go.transform)) continue;
            var pos = go.transform.position;
            var rot = go.transform.rotation;
            var scl = go.transform.lossyScale;
            Object.DestroyImmediate(go);
            if (_bedPrefab != null)
            {
                var bed = (GameObject)PrefabUtility.InstantiatePrefab(_bedPrefab, packRoot);
                bed.transform.position = new Vector3(pos.x, floorY, pos.z);
                bed.transform.rotation = rot;
                bed.name = $"Bed_{label}_{nBed}";
                if (_bedBeddingPrefab != null)
                {
                    var bedding = (GameObject)PrefabUtility.InstantiatePrefab(_bedBeddingPrefab, packRoot);
                    bedding.transform.position = new Vector3(pos.x, floorY, pos.z);
                    bedding.transform.rotation = rot;
                    bedding.name = $"Bedding_{label}_{nBed}";
                }
                nBed++;
            }
        }
        Debug.Log($"[{label}] beds replaced: {nBed}");

        // Replace doors
        int nDoor = 0;
        foreach (var go in doors)
        {
            if (UnderPreservedRoot(go.transform)) continue;
            var pos = go.transform.position;
            var rot = go.transform.rotation;
            Object.DestroyImmediate(go);
            if (_doorBasePrefab != null)
            {
                var dbase = (GameObject)PrefabUtility.InstantiatePrefab(_doorBasePrefab, packRoot);
                dbase.transform.position = new Vector3(pos.x, floorY, pos.z);
                dbase.transform.rotation = rot;
                dbase.name = $"DoorFrame_{label}_{nDoor}";
            }
            if (_doorPrefab != null)
            {
                var door = (GameObject)PrefabUtility.InstantiatePrefab(_doorPrefab, packRoot);
                door.transform.position = new Vector3(pos.x, floorY, pos.z);
                door.transform.rotation = rot;
                door.name = $"Door_{label}_{nDoor}";
            }
            nDoor++;
        }
        Debug.Log($"[{label}] doors replaced: {nDoor}");

        // Replace lamps (only primitive-built ones; the FBX ones from earlier
        // are also harmless to swap so we apply to all matches)
        int nLamp = 0;
        foreach (var go in lamps)
        {
            if (UnderPreservedRoot(go.transform)) continue;
            // Keep the Light component if present - re-parent it under the new lamp.
            var existingLight = go.GetComponent<Light>();
            var pos = go.transform.position;
            var rot = go.transform.rotation;
            Color savedColor = existingLight ? existingLight.color : Color.white;
            float savedIntensity = existingLight ? existingLight.intensity : 0f;
            float savedRange = existingLight ? existingLight.range : 0f;
            var savedType = existingLight ? existingLight.type : LightType.Point;
            Object.DestroyImmediate(go);
            if (_lampPrefab != null)
            {
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(_lampPrefab, packRoot);
                lamp.transform.position = pos;
                lamp.transform.rotation = rot;
                lamp.name = $"Lamp_{label}_{nLamp}";
                if (existingLight != null && savedIntensity > 0.01f)
                {
                    var lightChild = new GameObject("Light");
                    lightChild.transform.SetParent(lamp.transform, false);
                    lightChild.transform.localPosition = new Vector3(0, -0.2f, 0);
                    var l = lightChild.AddComponent<Light>();
                    l.type = savedType;
                    l.color = savedColor;
                    l.intensity = savedIntensity;
                    l.range = Mathf.Max(savedRange, 6f);
                    l.shadows = LightShadows.Soft;
                }
                nLamp++;
            }
        }
        Debug.Log($"[{label}] lamps replaced: {nLamp}");

        // Replace cabinets/lockers/shelves with med boxes
        int nBox = 0;
        foreach (var go in medbox)
        {
            if (UnderPreservedRoot(go.transform)) continue;
            // Skip very thin objects that might be wall trim, etc.
            var s = go.transform.lossyScale;
            float volume = Mathf.Abs(s.x * s.y * s.z);
            if (volume < 0.05f) continue;
            var pos = go.transform.position;
            var rot = go.transform.rotation;
            Object.DestroyImmediate(go);
            if (_medBoxPrefab != null)
            {
                var box = (GameObject)PrefabUtility.InstantiatePrefab(_medBoxPrefab, packRoot);
                box.transform.position = new Vector3(pos.x, floorY, pos.z);
                box.transform.rotation = rot;
                box.name = $"MedBox_{label}_{nBox}";
                nBox++;
            }
        }
        Debug.Log($"[{label}] med boxes placed: {nBox}");

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

    static List<GameObject> CollectByPatterns(string[] patterns)
    {
        var result = new List<GameObject>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = r.gameObject;
            if (UnderPreservedRoot(go.transform)) continue;
            if (!MatchesAny(go.name, patterns)) continue;
            // Skip walls/floors/ceilings even if they accidentally match
            if (go.name.Contains("_Wall") || go.name.EndsWith("Wall") ||
                go.name.Contains("_Floor") || go.name.Contains("_Ceiling") || go.name.EndsWith("Ceil"))
                continue;
            result.Add(go);
        }
        return result;
    }

    // ──────────────────────────────────────────────────────────────────
    // Screenshots
    // ──────────────────────────────────────────────────────────────────
    public static void CaptureFive()
    {
        try
        {
            string outDir = "C:/Users/hvnes/YomawariByoin/Screenshots";

            CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_PatientRoom_v2.png",
                new Vector3(-4.5f, 1.65f, 4.0f), new Vector3(6f, -90f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });

            CaptureScene("Assets/Scenes/Hospital.unity", outDir + "/1F_Corridor_v2.png",
                new Vector3(0.0f, 1.65f, 4f), new Vector3(6f, 180f, 0f),
                hideRoots: new[] { "CharacterShowcase_1F" });

            CaptureScene("Assets/Scenes/Hospital2F.unity", outDir + "/2F_Corridor_v2.png",
                new Vector3(0f, 1.65f, -12f), new Vector3(6f, 0f, 0f));

            CaptureScene("Assets/Scenes/Hospital3F.unity", outDir + "/3F_Corridor_v2.png",
                new Vector3(0f, 1.55f, -16f), new Vector3(5f, 0f, 0f));

            CaptureScene("Assets/Scenes/HospitalBasement.unity", outDir + "/Basement_v2.png",
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
        fill.type = LightType.Point;
        fill.intensity = 1.4f;
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
