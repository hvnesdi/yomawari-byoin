using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

public static class HospitalFullSetup
{
    const string PolyHavenDir = "Assets/Textures/PolyHaven";
    const string PropsDir     = "Assets/Models/Props";
    const string MatDir       = "Assets/Materials";
    const string PropMatDir   = "Assets/Materials/Props";

    [MenuItem("Tools/Hospital Full Setup")]
    public static void Run()
    {
        Debug.Log("=== HospitalFullSetup START ===");
        ConfigurePolyHavenImports();
        ConfigurePropImports();
        AssetDatabase.Refresh();

        EnsureFolders();
        BuildPolyHavenMaterials();
        ReassignSceneMaterials();
        PlacePropsInAllScenes();
        FinalizeLighting();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalFullSetup DONE ===");
    }

    [MenuItem("Tools/Hospital Full Setup (Batch Mode)")]
    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(MatDir))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(PropMatDir))
            AssetDatabase.CreateFolder(MatDir, "Props");
    }

    // ─────────────────────────────────────────────────────────────────
    // 1. PolyHaven texture import config (sRGB / normal map / linear)
    // ─────────────────────────────────────────────────────────────────
    static void ConfigurePolyHavenImports()
    {
        if (!Directory.Exists(PolyHavenDir))
        {
            Debug.LogWarning("PolyHaven dir missing");
            return;
        }
        foreach (var f in Directory.GetFiles(PolyHavenDir, "*.png"))
        {
            string assetPath = f.Replace("\\", "/").Substring(f.IndexOf("Assets"));
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null) continue;

            bool isNormal = assetPath.Contains("_nor_");
            bool isLinear = assetPath.Contains("_rough_") || assetPath.Contains("_ao_") || isNormal;

            imp.maxTextureSize = 2048;
            imp.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !isLinear;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
        }
        Debug.Log("PolyHaven imports configured");
    }

    // ─────────────────────────────────────────────────────────────────
    // 2. Prop FBX import config
    // ─────────────────────────────────────────────────────────────────
    static void ConfigurePropImports()
    {
        if (!Directory.Exists(PropsDir)) { Debug.LogWarning("Props dir missing"); return; }
        foreach (var f in Directory.GetFiles(PropsDir, "*.fbx"))
        {
            string assetPath = f.Replace("\\", "/").Substring(f.IndexOf("Assets"));
            var imp = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (imp == null) continue;
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.importBlendShapes = false;
            imp.importVisibility = true;
            imp.importCameras = false;
            imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.addCollider = false;
            imp.SaveAndReimport();
        }
        Debug.Log("Prop FBX imports configured");
    }

    // ─────────────────────────────────────────────────────────────────
    // 3. Create or update materials using PolyHaven textures
    // ─────────────────────────────────────────────────────────────────
    struct PHSet { public string assetId; public Vector2 tiling; }

    static void BuildPolyHavenMaterials()
    {
        // Map matName -> polyhaven asset id + tiling
        var map = new Dictionary<string, PHSet>
        {
            // 1F
            { "Corridor_Wall",        new PHSet{ assetId="worn_cracked_plaster", tiling=new Vector2(3,2) } },
            { "PatientRoom_Wall",     new PHSet{ assetId="plastered_wall_02",    tiling=new Vector2(2,2) } },
            { "Floor_Linoleum",       new PHSet{ assetId="concrete_floor_damaged_01", tiling=new Vector2(4,4) } },
            { "Ceiling_1F",           new PHSet{ assetId="grey_plaster",         tiling=new Vector2(4,4) } },
            // 2F
            { "Ceiling_2F",           new PHSet{ assetId="grey_plaster",         tiling=new Vector2(4,4) } },
            // 3F
            { "Ceiling_3F",           new PHSet{ assetId="worn_plaster_wall",    tiling=new Vector2(4,4) } },
            { "Isolation_Wall",      new PHSet{ assetId="beige_wall_001",        tiling=new Vector2(2,2) } },
            { "Padded_Wall",         new PHSet{ assetId="beige_wall_001",        tiling=new Vector2(2,2) } },
            // Basement
            { "Concrete_Wall",       new PHSet{ assetId="concrete_wall_007",     tiling=new Vector2(3,3) } },
            { "Concrete_Floor",      new PHSet{ assetId="brushed_concrete",      tiling=new Vector2(4,4) } },
            { "Concrete_Ceil",       new PHSet{ assetId="concrete_wall_007",     tiling=new Vector2(4,4) } },
        };

        foreach (var kv in map)
        {
            ApplyTexturesToMaterial(kv.Key, kv.Value.assetId, kv.Value.tiling);
        }
        Debug.Log("PolyHaven materials applied");
    }

    static void ApplyTexturesToMaterial(string matName, string assetId, Vector2 tiling)
    {
        string matPath = $"{MatDir}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");

        var diff  = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHavenDir}/{assetId}_diff_2k.png");
        var nor   = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHavenDir}/{assetId}_nor_2k.png");
        var ao    = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHavenDir}/{assetId}_ao_2k.png");

        if (diff != null)
        {
            mat.SetTexture("_BaseMap", diff);
            mat.SetColor("_BaseColor", Color.white);
        }
        if (nor != null)
        {
            mat.SetTexture("_BumpMap", nor);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.0f);
        }
        if (ao != null)
        {
            mat.SetTexture("_OcclusionMap", ao);
            mat.SetFloat("_OcclusionStrength", 1.0f);
        }
        mat.SetTextureScale("_BaseMap", tiling);
        if (nor != null) mat.SetTextureScale("_BumpMap", tiling);
        if (ao  != null) mat.SetTextureScale("_OcclusionMap", tiling);
        mat.SetFloat("_Smoothness", 0.08f);
        mat.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(mat);
    }

    // ─────────────────────────────────────────────────────────────────
    // 4. Walk all scenes and reassign materials to renderers by name
    //    (existing HospitalVisualImprover patterns)
    // ─────────────────────────────────────────────────────────────────
    static void ReassignSceneMaterials()
    {
        Reassign1F();
        Reassign2F();
        Reassign3F();
        ReassignBasement();
    }

    static void Reassign1F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var cw  = LoadMat("Corridor_Wall");
        var pw  = LoadMat("PatientRoom_Wall");
        var fl  = LoadMat("Floor_Linoleum");
        var ce  = LoadMat("Ceiling_1F");
        ApplyByName("Corridor_Wall", cw);
        ApplyByName("Reception_Wall", cw);
        ApplyByName("DirectorRoom_Wall", pw);
        ApplyByName("PatientRoom", pw);
        ApplyByName("Corridor_Floor", fl);
        ApplyByName("Reception_Floor", fl);
        ApplyByName("DirectorRoom_Floor", fl);
        ApplyByName("Corridor_Ceil", ce);
        ApplyByName("Reception_Ceil", ce);
        ApplyByName("DirectorRoom_Ceil", ce);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("1F materials reassigned");
    }

    static void Reassign2F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var cw  = LoadMat("Corridor_Wall");
        var pw  = LoadMat("PatientRoom_Wall");
        var fl  = LoadMat("Floor_Linoleum");
        var ce  = LoadMat("Ceiling_2F");
        ApplyByName("Corridor2F_Wall", cw);
        ApplyByName("NurseStation_Wall", pw);
        ApplyByName("TreatmentRoom", pw);
        ApplyByName("PatientRoom2F", pw);
        ApplyByName("Corridor2F_Floor", fl);
        ApplyByName("Corridor2F_Ceil", ce);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("2F materials reassigned");
    }

    static void Reassign3F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var cw  = LoadMat("Corridor_Wall");
        var pw  = LoadMat("PatientRoom_Wall");
        var fl  = LoadMat("Floor_Linoleum");
        var ce  = LoadMat("Ceiling_3F");
        var iso = LoadMat("Isolation_Wall");
        var pad = LoadMat("Padded_Wall");
        ApplyByName("Corridor3F_Wall", cw);
        ApplyByName("PatientRoom3F", pw);
        ApplyByName("IsolationWard", iso);
        ApplyByName("Isolation_", iso);
        ApplyByName("PaddedRoom", pad);
        ApplyByName("Corridor3F_Floor", fl);
        ApplyByName("Corridor3F_Ceil", ce);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("3F materials reassigned");
    }

    static void ReassignBasement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var cw = LoadMat("Concrete_Wall");
        var cf = LoadMat("Concrete_Floor");
        var cc = LoadMat("Concrete_Ceil");
        ApplyByName("BaseCorridor_Wall", cw);
        ApplyByName("RecordRoom_Wall", cw);
        ApplyByName("DirectorArchive_Wall", cw);
        ApplyByName("MedStorage_Wall", cw);
        ApplyByName("BaseCorridor_Floor", cf);
        ApplyByName("RecordRoom_Floor", cf);
        ApplyByName("BaseCorridor_Ceil", cc);
        ApplyByName("RecordRoom_Ceil", cc);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Basement materials reassigned");
    }

    static Material LoadMat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

    static void ApplyByName(string pattern, Material mat)
    {
        if (mat == null) return;
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.name.Contains(pattern))
            {
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                EditorUtility.SetDirty(r);
                n++;
            }
        }
        if (n > 0) Debug.Log($"  '{pattern}': {n} renderers ← {mat.name}");
    }

    // ─────────────────────────────────────────────────────────────────
    // 5. Place props in each scene
    // ─────────────────────────────────────────────────────────────────
    static GameObject LoadPropPrefab(string propName)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsDir}/{propName}.fbx");
    }

    static GameObject Place(string propName, Vector3 pos, Vector3 rot, Transform parent = null)
    {
        var prefab = LoadPropPrefab(propName);
        if (prefab == null) { Debug.LogWarning($"Missing prefab: {propName}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) return null;
        go.transform.position = pos;
        go.transform.eulerAngles = rot;
        if (parent != null) go.transform.SetParent(parent, true);
        return go;
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

    static void PlacePropsInAllScenes()
    {
        Place1F();
        Place2F();
        Place3F();
        PlaceBasement();
    }

    // 1F 廊下(4×32, z:-16..+16)+受付+院長室+病室3
    static void Place1F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var root = EnsureRoot("Props_1F");

        // Corridor: 蛍光灯×6 (天井から)
        float[] flZ = { -13f, -7f, -2f, 3f, 8f, 13f };
        for (int i = 0; i < flZ.Length; i++)
        {
            string name = i == 2 ? "prop_fluorescent_broken" :
                          i == 4 ? "prop_fluorescent_loose" : "prop_fluorescent_normal";
            Place(name, new Vector3(0, 2.88f, flZ[i]), new Vector3(180, 0, 0), root);
        }
        // 掲示板×2 (右壁、x=1.85, 内側を向く -90°)
        Place("prop_noticeboard", new Vector3(1.85f, 1.5f, -5f), new Vector3(0, -90, 0), root);
        Place("prop_noticeboard", new Vector3(1.85f, 1.5f,  6f), new Vector3(0, -90, 0), root);
        // 消火器×2 (左壁、x=-1.9, 床)
        Place("prop_fire_extinguisher", new Vector3(-1.85f, 0.1f, -4f), Vector3.zero, root);
        Place("prop_fire_extinguisher", new Vector3(-1.85f, 0.1f, 10f), Vector3.zero, root);
        // ゴミ箱×1
        Place("prop_trash_can", new Vector3(-1.7f, 0.1f, 2f), Vector3.zero, root);
        // 壁掛け時計×1 (受付近く、右壁、内側向き)
        Place("prop_wall_clock", new Vector3(1.85f, 2.3f, -11f), new Vector3(0, -90, 0), root);

        // 病室1 (PatientRoom_1: x=-6, z=5): ベッド・サイドテーブル・点滴・鏡・窓・カーテン・血圧計
        Place("prop_side_table",   new Vector3(-7.2f, 0.1f, 5f),  new Vector3(0, 90, 0), root);
        Place("prop_iv_stand_v2",  new Vector3(-7.3f, 0.1f, 4f),  Vector3.zero, root);
        Place("prop_mirror",       new Vector3(-7.95f, 1.6f, 6.5f), new Vector3(0, 90, 0), root);
        Place("prop_window",       new Vector3(-7.95f, 1.7f, 5.5f), new Vector3(0, 90, 0), root);
        Place("prop_blood_pressure_monitor", new Vector3(-4.05f, 1.3f, 5f), new Vector3(0, -90, 0), root);
        Place("prop_magazine",     new Vector3(-7.1f, 0.5f, 5f),  new Vector3(0, 20, 0), root);
        Place("prop_magazine",     new Vector3(-7.05f, 0.52f, 5.15f), new Vector3(0, -30, 0), root);

        // 病室2 (z=11)
        Place("prop_side_table",   new Vector3(-7.2f, 0.1f, 11f), new Vector3(0, 90, 0), root);
        Place("prop_iv_stand_v2",  new Vector3(-7.3f, 0.1f, 10f), Vector3.zero, root);
        Place("prop_window",       new Vector3(-7.95f, 1.7f, 11.5f), new Vector3(0, 90, 0), root);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("1F props placed");
    }

    // 2F 廊下(4×48, z:-24..+24) + 病室 + 処置室
    static void Place2F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var root = EnsureRoot("Props_2F");

        // ストレッチャー×2
        Place("prop_stretcher", new Vector3(-1.0f, 0, -6f),  new Vector3(0, 90, 0), root);
        Place("prop_stretcher", new Vector3( 1.0f, 0,  10f), new Vector3(0, -90, 0), root);
        // 車椅子×1
        Place("prop_wheelchair", new Vector3(1.4f, 0, -16f), new Vector3(0, 180, 0), root);

        // 蛍光灯×6 (ちらつき含む)
        float[] flZ = { -20f, -12f, -4f, 4f, 12f, 20f };
        for (int i = 0; i < flZ.Length; i++)
        {
            string name = i == 1 ? "prop_fluorescent_broken" :
                          i == 3 ? "prop_fluorescent_loose" :
                          i == 5 ? "prop_fluorescent_broken" : "prop_fluorescent_normal";
            Place(name, new Vector3(0, 2.85f, flZ[i]), new Vector3(180, 0, 0), root);
        }
        // 消火器
        Place("prop_fire_extinguisher", new Vector3(-1.85f, 0.1f, 0f), Vector3.zero, root);
        // 掲示板
        Place("prop_noticeboard", new Vector3(1.85f, 1.5f, -10f), new Vector3(0, -90, 0), root);

        // 処置室 (TreatmentRoom): 通常は右側 x=+6, z=+12 付近を仮定
        Place("prop_procedure_table", new Vector3(5.5f, 0, 11f), new Vector3(0, 0, 0), root);
        Place("prop_medical_tray",    new Vector3(7.2f, 0, 10f), new Vector3(0, 0, 0), root);
        Place("prop_medical_tray",    new Vector3(7.2f, 0, 12f), new Vector3(0, 30, 0), root);
        Place("prop_medicine_cabinet", new Vector3(7.7f, 0, 13.5f), new Vector3(0, -90, 0), root);
        Place("prop_sink",            new Vector3(4.2f, 0.8f, 13.5f), new Vector3(0, 180, 0), root);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("2F props placed");
    }

    // 3F 廊下(3×44, z:-22..+22) — 暗め
    static void Place3F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var root = EnsureRoot("Props_3F");

        // ストレッチャー×1
        Place("prop_stretcher", new Vector3(0, 0, -2f), new Vector3(0, 90, 0), root);
        // 車椅子×2
        Place("prop_wheelchair", new Vector3(-1.0f, 0, 8f), new Vector3(0, 60, 0), root);
        Place("prop_wheelchair", new Vector3(0.8f,  0, -10f), new Vector3(0, -110, 0), root);
        // 蛍光灯×4 (大半が壊れ・暗め)
        float[] flZ = { -16f, -5f, 6f, 16f };
        for (int i = 0; i < flZ.Length; i++)
        {
            string name = (i == 0 || i == 2) ? "prop_fluorescent_broken" : "prop_fluorescent_loose";
            Place(name, new Vector3(0, 2.85f, flZ[i]), new Vector3(180, 0, 0), root);
        }
        EditorSceneManager.SaveScene(scene);
        Debug.Log("3F props placed");
    }

    // Basement 廊下(4×36, z:-18..+18) + 記録保管室
    static void PlaceBasement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) return;
        var root = EnsureRoot("Props_Basement");

        // 金属棚×4
        Place("prop_metal_shelf", new Vector3(-1.85f, 0, -10f), new Vector3(0, 90, 0), root);
        Place("prop_metal_shelf", new Vector3(-1.85f, 0,  -5f), new Vector3(0, 90, 0), root);
        Place("prop_metal_shelf", new Vector3(-1.85f, 0,   3f), new Vector3(0, 90, 0), root);
        Place("prop_metal_shelf", new Vector3(-1.85f, 0,  10f), new Vector3(0, 90, 0), root);

        // カルテファイル×20 (棚に置く)
        float[] shelfZ = { -10f, -5f, 3f, 10f };
        for (int s = 0; s < shelfZ.Length; s++)
        {
            for (int i = 0; i < 5; i++)
            {
                float zOff = -0.4f + i * 0.16f;
                float yShelf = 0.55f + (i % 3) * 0.45f;
                Place("prop_chart_file",
                    new Vector3(-1.55f, yShelf, shelfZ[s] + zOff),
                    new Vector3(0, 90, 0), root);
            }
        }

        // デスク×2 (右壁前)
        Place("prop_old_desk", new Vector3(1.0f, 0, -8f),  new Vector3(0, -90, 0), root);
        Place("prop_old_desk", new Vector3(1.0f, 0, 12f),  new Vector3(0, -90, 0), root);
        // デスクライト×2
        Place("prop_desk_lamp", new Vector3(1.2f, 0.8f, -8f),  Vector3.zero, root);
        Place("prop_desk_lamp", new Vector3(1.2f, 0.8f, 12f),  Vector3.zero, root);
        // ファイルキャビネット×3
        Place("prop_file_cabinet", new Vector3(1.7f, 0, -3f), new Vector3(0, -90, 0), root);
        Place("prop_file_cabinet", new Vector3(1.7f, 0, 0f),  new Vector3(0, -90, 0), root);
        Place("prop_file_cabinet", new Vector3(1.7f, 0, 7f),  new Vector3(0, -90, 0), root);
        // 段ボール×6 (床散在)
        Place("prop_cardboard_box", new Vector3(-1.5f, 0, -14f), new Vector3(0, 20, 0), root);
        Place("prop_cardboard_box", new Vector3(-1.4f, 0, -13.4f), new Vector3(0, -10, 0), root);
        Place("prop_cardboard_box", new Vector3( 1.5f, 0,  -1f), new Vector3(0, 15, 0), root);
        Place("prop_cardboard_box", new Vector3( 1.4f, 0.42f, -1f), new Vector3(0, 25, 0), root);
        Place("prop_cardboard_box", new Vector3(-1.5f, 0,  15f), new Vector3(0, 35, 0), root);
        Place("prop_cardboard_box", new Vector3( 1.4f, 0,  15.5f), new Vector3(0, -15, 0), root);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Basement props placed");
    }

    // ─────────────────────────────────────────────────────────────────
    // 6. Lighting finalize per floor
    // ─────────────────────────────────────────────────────────────────
    static void FinalizeLighting()
    {
        SetSceneLighting("Assets/Scenes/Hospital.unity",
            intensityScale: 1.2f, color: new Color(0.95f, 0.97f, 1.0f), ambient: 0.30f);
        SetSceneLighting("Assets/Scenes/Hospital2F.unity",
            intensityScale: 0.8f, color: new Color(0.92f, 0.90f, 0.82f), ambient: 0.20f);
        SetSceneLighting("Assets/Scenes/Hospital3F.unity",
            intensityScale: 0.5f, color: new Color(0.88f, 0.86f, 0.72f), ambient: 0.15f);
        SetSceneLighting("Assets/Scenes/HospitalBasement.unity",
            intensityScale: 0.3f, color: new Color(1.0f, 0.2f, 0.1f), ambient: 0.10f);
    }

    static void SetSceneLighting(string path, float intensityScale, Color color, float ambient)
    {
        var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        if (!scene.IsValid()) return;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = color * ambient;

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Point || l.type == LightType.Spot)
            {
                l.intensity = Mathf.Max(0.05f, l.intensity * intensityScale);
                l.color = color;
                EditorUtility.SetDirty(l);
            }
        }
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Lighting set: {path}");
    }
}
