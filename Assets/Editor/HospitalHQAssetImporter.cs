using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Imports the high-quality Blender-baked textures and FBX models under
/// Assets/Textures/Hospital and Assets/Models/Hospital, builds URP materials,
/// and re-skins the 4 hospital scenes (1F / 2F / 3F / Basement) plus places
/// extra props (filing cabinets, wheelchairs, medical carts).
/// </summary>
public static class HospitalHQAssetImporter
{
    const string TexDir = "Assets/Textures/Hospital";
    const string ModDir = "Assets/Models/Hospital";
    const string MatDir = "Assets/Materials/Hospital";

    [MenuItem("Tools/Apply HQ Hospital Assets")]
    public static void Apply()
    {
        Debug.Log("=== HospitalHQAssetImporter: Start ===");
        EnsureFolders();
        ConfigureTextureImports();
        ConfigureModelImports();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var mats = BuildMaterials();
        ApplyToScene("Assets/Scenes/Hospital.unity",          mats, Floor.F1);
        ApplyToScene("Assets/Scenes/Hospital2F.unity",        mats, Floor.F2);
        ApplyToScene("Assets/Scenes/Hospital3F.unity",        mats, Floor.F3);
        ApplyToScene("Assets/Scenes/HospitalBasement.unity",  mats, Floor.B1);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalHQAssetImporter: Done ===");
    }

    enum Floor { F1, F2, F3, B1 }

    // ─── Folders ──────────────────────────────────────────────────────────
    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets/Materials", "Hospital");
    }

    // ─── Texture import settings ──────────────────────────────────────────
    static void ConfigureTextureImports()
    {
        if (!AssetDatabase.IsValidFolder(TexDir)) { Debug.LogWarning($"No {TexDir}"); return; }
        foreach (var path in Directory.GetFiles(TexDir, "*.png"))
        {
            var p = path.Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            bool isNormal = p.EndsWith("_Normal.png");
            bool isRough  = p.EndsWith("_Roughness.png");

            ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !(isNormal || isRough);
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 1024;
            ti.SaveAndReimport();
        }
        Debug.Log("  textures reimported with proper PBR settings");
    }

    // ─── Model import settings ────────────────────────────────────────────
    static void ConfigureModelImports()
    {
        if (!AssetDatabase.IsValidFolder(ModDir)) { Debug.LogWarning($"No {ModDir}"); return; }
        foreach (var path in Directory.GetFiles(ModDir, "*.fbx"))
        {
            var p = path.Replace('\\', '/');
            var mi = AssetImporter.GetAtPath(p) as ModelImporter;
            if (mi == null) continue;
            mi.useFileScale = true;
            mi.globalScale  = 1f;
            mi.importBlendShapes = false;
            mi.importVisibility  = true;
            mi.importCameras     = false;
            mi.importLights      = false;
            mi.addCollider       = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // we assign our own
            mi.SaveAndReimport();
        }
        Debug.Log("  FBX reimported");
    }

    // ─── Material set ─────────────────────────────────────────────────────
    class MatSet
    {
        public Material WardWall, CorridorWall, Floor, BasementFloor, BasementWall, Ceiling;
        public Material BedMetal, BedSheet, BedPillow, IVMetal, IVBag, DoorWood, DoorMetal, DoorGlass, DoorKnob;
        public Material LightHousing, LightCover, LightTube;
        public Material WCFrame, WCSeat, WCTire, WCRim;
        public Material CabBody, CabInset, CabHandle;
        public Material CartMetal, CartTop, CartDrawer, CartCaster;
    }

    static MatSet BuildMaterials()
    {
        var m = new MatSet
        {
            WardWall      = TexMat("Hospital_WardWall",     "WardWall"),
            CorridorWall  = TexMat("Hospital_CorridorWall", "CorridorWall"),
            Floor         = TexMat("Hospital_Floor",        "Floor"),
            BasementFloor = TexMat("Hospital_BasementFloor","Basement"),
            BasementWall  = TexMat("Hospital_BasementWall", "Basement"),
            Ceiling       = TexMat("Hospital_Ceiling",      "Ceiling"),

            BedMetal   = ColorMat("Hospital_BedMetal",  new Color(0.62f, 0.59f, 0.54f), 0.45f, 0.85f),
            BedSheet   = ColorMat("Hospital_BedSheet",  new Color(0.80f, 0.78f, 0.70f), 0.95f, 0f),
            BedPillow  = ColorMat("Hospital_BedPillow", new Color(0.86f, 0.84f, 0.75f), 0.92f, 0f),
            IVMetal    = ColorMat("Hospital_IVMetal",   new Color(0.72f, 0.70f, 0.66f), 0.30f, 0.95f),
            IVBag      = TransMat("Hospital_IVBag",     new Color(0.82f, 0.88f, 0.84f, 0.45f), 0.15f),
            DoorWood   = ColorMat("Hospital_DoorWood",  new Color(0.36f, 0.24f, 0.15f), 0.78f, 0f),
            DoorMetal  = ColorMat("Hospital_DoorMetal", new Color(0.62f, 0.59f, 0.55f), 0.45f, 0.6f),
            DoorGlass  = TransMat("Hospital_DoorGlass", new Color(0.82f, 0.84f, 0.82f, 0.35f), 0.05f),
            DoorKnob   = ColorMat("Hospital_DoorKnob",  new Color(0.78f, 0.72f, 0.55f), 0.25f, 0.95f),

            LightHousing = ColorMat   ("Hospital_LightHousing", new Color(0.85f, 0.85f, 0.82f), 0.55f, 0.4f),
            LightCover   = EmissiveMat("Hospital_LightCover",   new Color(0.95f, 0.95f, 0.92f), new Color(0.95f, 0.95f, 0.88f) * 1.8f),
            LightTube    = EmissiveMat("Hospital_LightTube",    new Color(0.98f, 0.98f, 1.00f), new Color(0.92f, 0.95f, 1.00f) * 4.5f),

            WCFrame = ColorMat("Hospital_WCFrame", new Color(0.65f, 0.62f, 0.58f), 0.45f, 0.85f),
            WCSeat  = ColorMat("Hospital_WCSeat",  new Color(0.20f, 0.18f, 0.18f), 0.85f, 0f),
            WCTire  = ColorMat("Hospital_WCTire",  new Color(0.07f, 0.07f, 0.07f), 0.92f, 0f),
            WCRim   = ColorMat("Hospital_WCRim",   new Color(0.72f, 0.70f, 0.66f), 0.35f, 0.9f),

            CabBody    = ColorMat("Hospital_CabBody",    new Color(0.42f, 0.40f, 0.36f), 0.55f, 0.6f),
            CabInset   = ColorMat("Hospital_CabInset",   new Color(0.30f, 0.28f, 0.24f), 0.70f, 0.4f),
            CabHandle  = ColorMat("Hospital_CabHandle",  new Color(0.62f, 0.59f, 0.54f), 0.35f, 0.9f),

            CartMetal  = ColorMat("Hospital_CartMetal",  new Color(0.68f, 0.66f, 0.62f), 0.40f, 0.85f),
            CartTop    = ColorMat("Hospital_CartTop",    new Color(0.55f, 0.53f, 0.48f), 0.55f, 0.5f),
            CartDrawer = ColorMat("Hospital_CartDrawer", new Color(0.50f, 0.48f, 0.44f), 0.50f, 0.6f),
            CartCaster = ColorMat("Hospital_CartCaster", new Color(0.12f, 0.12f, 0.12f), 0.90f, 0f),
        };
        // Tune basement wall: same base texture but darker tint
        m.BasementWall.SetColor("_BaseColor", new Color(0.62f, 0.60f, 0.55f));
        return m;
    }

    static Shader Lit() => Shader.Find("Universal Render Pipeline/Lit");

    static Material TexMat(string name, string texPrefix)
    {
        var mat = LoadOrCreate(name);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.2f);

        var diff  = LoadTex($"{TexDir}/{texPrefix}_Diffuse.png");
        var norm  = LoadTex($"{TexDir}/{texPrefix}_Normal.png");
        var rough = LoadTex($"{TexDir}/{texPrefix}_Roughness.png");
        if (diff  != null) mat.SetTexture("_BaseMap", diff);
        if (norm  != null)
        {
            mat.SetTexture("_BumpMap", norm);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.0f);
        }
        // URP Lit smoothness map: SmoothnessFromMetallicAlpha if metallic map; here roughness goes into Smoothness via inverted A.
        // Simplest reliable path: leave the smoothness slider modest and rely on diffuse+normal for visual quality.
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material ColorMat(string name, Color baseColor, float roughness, float metallic)
    {
        var mat = LoadOrCreate(name);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - roughness));
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material TransMat(string name, Color rgba, float roughness)
    {
        var mat = LoadOrCreate(name);
        mat.SetFloat("_Surface", 1f);   // 0=Opaque, 1=Transparent
        mat.SetFloat("_Blend", 0f);     // 0=Alpha
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");

        mat.SetColor("_BaseColor", rgba);
        mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - roughness));
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material EmissiveMat(string name, Color baseColor, Color emission)
    {
        var mat = ColorMat(name, baseColor, 0.2f, 0f);
        mat.SetColor("_EmissionColor", emission);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreate(string name)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Lit());
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Lit();
        return mat;
    }

    static Texture2D LoadTex(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);

    // ─── Scene application ────────────────────────────────────────────────
    static void ApplyToScene(string scenePath, MatSet m, Floor floor)
    {
        if (!File.Exists(scenePath)) { Debug.LogWarning($"missing scene: {scenePath}"); return; }
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError($"Cannot open {scenePath}"); return; }

        // ── Re-skin renderers by name pattern ────────────────────────────
        // Wall variants
        ApplyByPattern("Corridor_Wall",      m.CorridorWall);
        ApplyByPattern("Corridor2F_Wall",    m.CorridorWall);
        ApplyByPattern("Corridor3F_Wall",    m.CorridorWall);
        ApplyByPattern("Reception_Wall",     m.CorridorWall);
        ApplyByPattern("DirectorRoom_Wall",  m.WardWall);
        ApplyByPattern("PatientRoom",        m.WardWall);
        ApplyByPattern("PatientRoom2F",      m.WardWall);
        ApplyByPattern("PatientRoom3F",      m.WardWall);
        ApplyByPattern("NurseStation_Wall",  m.CorridorWall);
        ApplyByPattern("TreatmentRoom",      m.WardWall);
        ApplyByPattern("IsolationWard",      m.WardWall);
        ApplyByPattern("Isolation_",         m.WardWall);
        ApplyByPattern("PaddedRoom",         m.WardWall);
        ApplyByPattern("BaseCorridor_Wall",  m.BasementWall);
        ApplyByPattern("RecordRoom_Wall",    m.BasementWall);
        ApplyByPattern("DirectorArchive_Wall", m.BasementWall);
        ApplyByPattern("MedStorage_Wall",    m.BasementWall);

        // Floors
        var floorMat = (floor == Floor.B1) ? m.BasementFloor : m.Floor;
        ApplyByPattern("Corridor_Floor",     floorMat);
        ApplyByPattern("Corridor2F_Floor",   floorMat);
        ApplyByPattern("Corridor3F_Floor",   floorMat);
        ApplyByPattern("Reception_Floor",    floorMat);
        ApplyByPattern("DirectorRoom_Floor", floorMat);
        ApplyByPattern("PatientRoom_Floor",  floorMat);
        ApplyByPattern("BaseCorridor_Floor", floorMat);
        ApplyByPattern("RecordRoom_Floor",   floorMat);
        // PatientRoom* contains both wall and floor matches; we already set wall but floor takes precedence by partial pattern
        ReassignSubstringEnds("_Floor", floorMat);

        // Ceilings
        ApplyByPattern("Corridor_Ceiling",    m.Ceiling);
        ApplyByPattern("Corridor2F_Ceiling",  m.Ceiling);
        ApplyByPattern("Corridor3F_Ceiling",  m.Ceiling);
        ApplyByPattern("Reception_Ceil",      m.Ceiling);
        ApplyByPattern("DirectorRoom_Ceil",   m.Ceiling);
        ApplyByPattern("BaseCorridor_Ceiling",m.Ceiling);
        ApplyByPattern("RecordRoom_Ceil",     m.Ceiling);
        ReassignSubstringEnds("_Ceiling", m.Ceiling);

        // Existing furniture pieces (from old FBX)
        ApplyByPattern("_Sheet",    m.BedSheet);
        ApplyByPattern("BedFrame",  m.BedMetal);
        ApplyByPattern("IVPole",    m.IVMetal);
        ApplyByPattern("IVStand",   m.IVMetal);
        ApplyByPattern("Door_",     m.DoorWood);

        // ── Replace HQ props ─────────────────────────────────────────────
        ReplaceHQProps(floor, m);

        // ── Lighting / post-process ──────────────────────────────────────
        TuneLightingPerFloor(floor, m);

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"  scene saved: {scenePath}");
    }

    static void ApplyByPattern(string pattern, Material mat)
    {
        if (mat == null) return;
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r == null || r.gameObject.name.IndexOf(pattern, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
            var arr = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
            EditorUtility.SetDirty(r);
            n++;
        }
        if (n > 0) Debug.Log($"  '{pattern}': {n}");
    }

    static void ReassignSubstringEnds(string suffix, Material mat)
    {
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r == null || !r.gameObject.name.EndsWith(suffix)) continue;
            var arr = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < arr.Length; i++) arr[i] = mat;
            r.sharedMaterials = arr;
            EditorUtility.SetDirty(r);
            n++;
        }
        if (n > 0) Debug.Log($"  suffix '{suffix}': {n}");
    }

    // ─── HQ prop replacement ──────────────────────────────────────────────
    static void ReplaceHQProps(Floor floor, MatSet m)
    {
        var root = new GameObject("HQ_Props_Root").transform;

        // Hospital beds (1F/2F/3F only)
        if (floor != Floor.B1)
        {
            string suffix = floor.ToString();
            // 3 ward rooms with bed+iv stand+door
            for (int i = 0; i < 3; i++)
            {
                Vector3 bedPos = new Vector3(-5f, 0f, 5f + i * 6f);
                Vector3 ivPos  = new Vector3(-3.5f, 0f, 6f + i * 6f);
                Vector3 doorPos = new Vector3(-4f, 0f, 3f + i * 6f);
                PlacePrefab($"{ModDir}/HospitalBed.fbx",     bedPos,  Quaternion.identity,
                            $"HQBed_{suffix}_{i+1}", root, m.BedMetal, m.BedSheet);
                PlacePrefab($"{ModDir}/IVStand.fbx",         ivPos,   Quaternion.identity,
                            $"HQIVStand_{suffix}_{i+1}", root, m.IVMetal, m.IVBag);
                PlacePrefab($"{ModDir}/HospitalDoor.fbx",    doorPos, Quaternion.identity,
                            $"HQDoor_{suffix}_{i+1}", root, m.DoorWood, m.DoorMetal, m.DoorGlass, m.DoorKnob);
            }
            // Wheelchair in corridor
            PlacePrefab($"{ModDir}/Wheelchair.fbx",
                        new Vector3(0.8f, 0f, 2f), Quaternion.Euler(0, 30, 0),
                        $"HQWheelchair_{suffix}", root, m.WCFrame, m.WCSeat, m.WCTire, m.WCRim);
            // Medical cart in corridor
            PlacePrefab($"{ModDir}/MedicalCart.fbx",
                        new Vector3(-1.2f, 0f, -2f), Quaternion.Euler(0, -25, 0),
                        $"HQMedCart_{suffix}", root, m.CartMetal, m.CartTop, m.CartDrawer, m.CartCaster);
        }
        else
        {
            // Basement: filing cabinets, medical cart, wheelchair (abandoned)
            for (int i = 0; i < 4; i++)
            {
                PlacePrefab($"{ModDir}/FilingCabinet.fbx",
                            new Vector3(-4f + i * 1.1f, 0f, 4f), Quaternion.identity,
                            $"HQFilingCab_{i+1}", root, m.CabBody, m.CabInset, m.CabHandle);
            }
            for (int i = 0; i < 3; i++)
            {
                PlacePrefab($"{ModDir}/FilingCabinet.fbx",
                            new Vector3(-4f + i * 1.1f, 0f, 7f), Quaternion.Euler(0, 180, 0),
                            $"HQFilingCabBack_{i+1}", root, m.CabBody, m.CabInset, m.CabHandle);
            }
            PlacePrefab($"{ModDir}/MedicalCart.fbx",
                        new Vector3(2.5f, 0f, -1f), Quaternion.Euler(0, -45, 0),
                        "HQMedCart_B", root, m.CartMetal, m.CartTop, m.CartDrawer, m.CartCaster);
            PlacePrefab($"{ModDir}/Wheelchair.fbx",
                        new Vector3(3.5f, 0f, -4f), Quaternion.Euler(0, 120, 0),
                        "HQWheelchair_B", root, m.WCFrame, m.WCSeat, m.WCTire, m.WCRim);
        }

        // Fluorescent lights along corridor ceiling
        int lightCount = (floor == Floor.B1) ? 6 : 8;
        float dz = (floor == Floor.B1) ? 3f : 3.5f;
        float z0 = (floor == Floor.B1) ? -8f : -10f;
        float ceilingY = (floor == Floor.B1) ? 2.78f : 2.95f;
        for (int i = 0; i < lightCount; i++)
        {
            float z = z0 + i * dz;
            var lg = PlacePrefab($"{ModDir}/FluorescentLight.fbx",
                                  new Vector3(0f, ceilingY, z), Quaternion.identity,
                                  $"HQLight_{floor}_{i+1}", root,
                                  m.LightHousing, m.LightCover, m.LightTube);
            if (lg != null)
            {
                // Add a Point Light to actually illuminate the area (since emission alone needs lightmap baking)
                var lightGo = new GameObject("PointLight");
                lightGo.transform.SetParent(lg.transform, false);
                lightGo.transform.localPosition = new Vector3(0, -0.08f, 0);
                var lt = lightGo.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = floor switch
                {
                    Floor.F1 => new Color(0.95f, 0.92f, 0.84f),
                    Floor.F2 => new Color(0.90f, 0.88f, 0.78f),
                    Floor.F3 => new Color(0.82f, 0.86f, 0.78f),
                    _        => new Color(0.78f, 0.82f, 0.78f),
                };
                lt.intensity = floor switch
                {
                    Floor.F1 => 0.65f,
                    Floor.F2 => 0.55f,
                    Floor.F3 => 0.40f,
                    _        => 0.32f,
                };
                lt.range = 6f;
                lt.shadows = LightShadows.Soft;
                lt.shadowStrength = 0.7f;
            }
        }
    }

    static GameObject PlacePrefab(string fbxPath, Vector3 pos, Quaternion rot, string goName,
                                   Transform parent, params Material[] mats)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null) { Debug.LogWarning($"FBX not found: {fbxPath}"); return null; }
        var existing = GameObject.Find(goName);
        if (existing != null) Object.DestroyImmediate(existing);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = goName;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);

        if (mats != null && mats.Length > 0)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = mats[Mathf.Min(i, mats.Length - 1)];
                r.sharedMaterials = arr;
                EditorUtility.SetDirty(r);
            }
        }
        return go;
    }

    // ─── Lighting / post-process ──────────────────────────────────────────
    static void TuneLightingPerFloor(Floor floor, MatSet m)
    {
        // Disable any directional sunlight — interior horror
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) l.gameObject.SetActive(false);

        // Subtle ambient tint per floor
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = floor switch
        {
            Floor.F1 => new Color(0.10f, 0.11f, 0.12f),
            Floor.F2 => new Color(0.08f, 0.09f, 0.11f),
            Floor.F3 => new Color(0.06f, 0.07f, 0.10f),
            _        => new Color(0.04f, 0.05f, 0.07f),
        };

        // Fog adds depth + obscures distance — pure horror staple
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = floor switch
        {
            Floor.F1 => new Color(0.04f, 0.05f, 0.06f),
            Floor.F2 => new Color(0.03f, 0.04f, 0.06f),
            Floor.F3 => new Color(0.02f, 0.03f, 0.05f),
            _        => new Color(0.01f, 0.02f, 0.03f),
        };
        RenderSettings.fogDensity = floor switch
        {
            Floor.F1 => 0.04f,
            Floor.F2 => 0.05f,
            Floor.F3 => 0.07f,
            _        => 0.09f,
        };

        TunePostProcess(floor);
    }

    static void TunePostProcess(Floor floor)
    {
        var volume = Object.FindFirstObjectByType<Volume>();
        if (volume == null)
        {
            // Create a global Volume so post-process works at all
            var go = new GameObject("HQ_GlobalVolume");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            string profilePath = $"Assets/Settings/HQ_HospitalVolume_{floor}.asset";
            // ensure folder
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");
            AssetDatabase.CreateAsset(volume.profile, profilePath);
        }
        var profile = volume.profile;

        float exposure = floor switch { Floor.F1 => -1.0f, Floor.F2 => -1.3f, Floor.F3 => -1.6f, _ => -1.9f };
        Color filter = floor switch
        {
            Floor.F1 => new Color(0.82f, 0.90f, 1.00f),
            Floor.F2 => new Color(0.74f, 0.84f, 1.00f),
            Floor.F3 => new Color(0.66f, 0.78f, 1.00f),
            _        => new Color(0.58f, 0.70f, 1.00f),
        };
        float vig    = floor switch { Floor.F1 => 0.42f, Floor.F2 => 0.50f, Floor.F3 => 0.58f, _ => 0.65f };
        float bloom  = floor switch { Floor.F1 => 0.55f, Floor.F2 => 0.70f, Floor.F3 => 0.85f, _ => 1.00f };
        float grain  = floor switch { Floor.F1 => 0.30f, Floor.F2 => 0.40f, Floor.F3 => 0.55f, _ => 0.70f };

        if (!profile.TryGet<ColorAdjustments>(out var ca)) ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.value = exposure;       ca.postExposure.overrideState = true;
        ca.colorFilter.value  = filter;         ca.colorFilter.overrideState  = true;
        ca.saturation.value   = -25f;           ca.saturation.overrideState   = true;
        ca.contrast.value     = 15f;            ca.contrast.overrideState     = true;

        if (!profile.TryGet<Vignette>(out var vi)) vi = profile.Add<Vignette>(true);
        vi.intensity.value = vig;               vi.intensity.overrideState = true;
        vi.smoothness.value = 0.45f;            vi.smoothness.overrideState = true;
        vi.color.value = Color.black;           vi.color.overrideState = true;

        if (!profile.TryGet<Bloom>(out var bl)) bl = profile.Add<Bloom>(true);
        bl.intensity.value = bloom;             bl.intensity.overrideState = true;
        bl.threshold.value = 0.85f;             bl.threshold.overrideState = true;
        bl.tint.value = filter;                 bl.tint.overrideState = true;

        if (!profile.TryGet<FilmGrain>(out var fg)) fg = profile.Add<FilmGrain>(true);
        fg.type.value = FilmGrainLookup.Medium2; fg.type.overrideState = true;
        fg.intensity.value = grain;             fg.intensity.overrideState = true;
        fg.response.value = 0.8f;                fg.response.overrideState = true;

        if (!profile.TryGet<ChromaticAberration>(out var cab)) cab = profile.Add<ChromaticAberration>(true);
        cab.intensity.value = floor switch { Floor.F1 => 0.10f, Floor.F2 => 0.18f, Floor.F3 => 0.30f, _ => 0.45f };
        cab.intensity.overrideState = true;

        EditorUtility.SetDirty(profile);
    }
}
