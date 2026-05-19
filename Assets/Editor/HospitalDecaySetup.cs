using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;
using System.Collections.Generic;

public static class HospitalDecaySetup
{
    const string GenTexDir  = "Assets/Textures/Generated";
    const string DecalDir   = "Assets/Textures/Decals";
    const string PHDir      = "Assets/Textures/PolyHaven";
    const string PropsDir   = "Assets/Models/Props";
    const string MatDir     = "Assets/Materials";
    const string DecalMatDir= "Assets/Materials/Decals";

    [MenuItem("Tools/Hospital Decay Setup (Batch)")]
    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    [MenuItem("Tools/Hospital Decay Setup")]
    public static void Run()
    {
        Debug.Log("=== DecaySetup START ===");
        EnsureFolders();
        ConfigureNewImports();
        AssetDatabase.Refresh();
        BuildMaterials();
        ApplyToScene1F();
        ApplyToScene2F();
        ApplyToScene3F();
        ApplyToSceneBasement();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== DecaySetup DONE ===");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(DecalMatDir)) AssetDatabase.CreateFolder(MatDir, "Decals");
    }

    static void ConfigureNewImports()
    {
        // Generated textures: sRGB diffuse
        foreach (var f in Directory.GetFiles(GenTexDir, "*.png"))
        {
            var path = f.Replace("\\","/").Substring(f.IndexOf("Assets"));
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Default;
            imp.sRGBTexture = true;
            imp.maxTextureSize = 2048;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Trilinear;
            imp.anisoLevel = 8;
            imp.SaveAndReimport();
        }
        // Decals: alpha-transparent, clamp wrap
        if (Directory.Exists(DecalDir))
        {
            foreach (var f in Directory.GetFiles(DecalDir, "*.png"))
            {
                var path = f.Replace("\\","/").Substring(f.IndexOf("Assets"));
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.alphaIsTransparency = true;
                imp.sRGBTexture = true;
                imp.maxTextureSize = 1024;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Trilinear;
                imp.SaveAndReimport();
            }
        }
        // New props: import settings
        foreach (var f in Directory.GetFiles(PropsDir, "*.fbx"))
        {
            var path = f.Replace("\\","/").Substring(f.IndexOf("Assets"));
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) continue;
            if (imp.globalScale != 1f || imp.materialLocation != ModelImporterMaterialLocation.InPrefab)
            {
                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.importBlendShapes = false;
                imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
                imp.addCollider = false;
                imp.SaveAndReimport();
            }
        }
        Debug.Log("Imports configured");
    }

    // ─────────────────────────────────────────────
    // Material build
    // ─────────────────────────────────────────────
    static Material MatLitTex(string name, string diffPath, string norPath, Vector2 tiling,
                              float smoothness=0.08f, float metallic=0f)
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
        if (diff != null) { mat.SetTexture("_BaseMap", diff); mat.SetColor("_BaseColor", Color.white); }
        if (!string.IsNullOrEmpty(norPath))
        {
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(norPath);
            if (nor != null) { mat.SetTexture("_BumpMap", nor); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }
        }
        mat.SetTextureScale("_BaseMap", tiling);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MatTransparent(string name, string diffPath, float smoothness=0.3f, float metallic=0f)
    {
        string p = $"{DecalMatDir}/{name}.mat";
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
        mat.SetFloat("_Surface", 1f);  // 0=opaque, 1=transparent
        mat.SetFloat("_Blend", 0f);    // 0=alpha
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MatMirror(string name)
    {
        string p = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, p);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", new Color(0.85f, 0.86f, 0.88f, 1f));
        mat.SetFloat("_Metallic", 1.0f);
        mat.SetFloat("_Smoothness", 0.95f);
        // 曇り（fog）テクスチャを薄く重ねるなら：generic mirror fog texture if exists
        var fog = AssetDatabase.LoadAssetAtPath<Texture2D>($"{DecalDir}/decal_waterstain_01.png");
        if (fog != null) mat.SetTexture("_BaseMap", fog);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // 黒い染みの鏡用オーバーレイ
    static Material MatMirrorFog(string name)
    {
        string p = $"{DecalMatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, p);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{DecalDir}/decal_mold_01.png");
        if (diff != null) mat.SetTexture("_BaseMap", diff);
        mat.SetColor("_BaseColor", new Color(0.4f, 0.4f, 0.4f, 0.85f));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Dictionary<string,Material> _mats;
    static void BuildMaterials()
    {
        _mats = new Dictionary<string, Material>();
        // 廊下壁ツーパス
        _mats["CorridorWall"]   = MatLitTex("Corridor_Wall",
            $"{GenTexDir}/corridor_wall_2tone.png",
            $"{PHDir}/worn_cracked_plaster_nor_2k.png",
            new Vector2(1, 0.6f), 0.08f);
        // 病室壁
        _mats["PatientWall"]    = MatLitTex("PatientRoom_Wall",
            $"{GenTexDir}/patient_wall_dirty.png",
            $"{PHDir}/plastered_wall_02_nor_2k.png",
            new Vector2(2, 1.5f), 0.08f);
        // 地下壁
        _mats["BasementWall"]   = MatLitTex("Concrete_Wall",
            $"{GenTexDir}/basement_concrete_damp.png",
            $"{PHDir}/concrete_wall_007_nor_2k.png",
            new Vector2(2, 2), 0.10f);
        // 床リノリウム
        _mats["FloorLino"]      = MatLitTex("Floor_Linoleum",
            $"{GenTexDir}/floor_linoleum_tile.png",
            null,
            new Vector2(4, 4), 0.30f);
        // 床PolyHaven tile (代替/3F用)
        _mats["FloorTile"]      = MatLitTex("Floor_Tile_3F",
            $"{PHDir}/worn_tile_floor_diff_2k.png",
            $"{PHDir}/worn_tile_floor_nor_2k.png",
            new Vector2(4, 4), 0.25f);
        // 地下床
        _mats["FloorBasement"]  = MatLitTex("Concrete_Floor",
            $"{PHDir}/brushed_concrete_diff_2k.png",
            $"{PHDir}/brushed_concrete_nor_2k.png",
            new Vector2(3, 3), 0.20f);
        // 天井（既存利用）
        _mats["Ceiling1F"] = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Ceiling_1F.mat");
        _mats["Ceiling2F"] = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Ceiling_2F.mat");
        _mats["Ceiling3F"] = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Ceiling_3F.mat");
        _mats["CeilingBase"] = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Concrete_Ceil.mat");

        // Decal materials
        _mats["Blood1"]  = MatTransparent("Decal_Blood_01",  $"{DecalDir}/decal_blood_01.png", 0.4f);
        _mats["Blood2"]  = MatTransparent("Decal_Blood_02",  $"{DecalDir}/decal_blood_02.png", 0.4f);
        _mats["Mold1"]   = MatTransparent("Decal_Mold_01",   $"{DecalDir}/decal_mold_01.png", 0.85f);
        _mats["Mold2"]   = MatTransparent("Decal_Mold_02",   $"{DecalDir}/decal_mold_02.png", 0.85f);
        _mats["Water"]   = MatTransparent("Decal_Water_01",  $"{DecalDir}/decal_waterstain_01.png", 0.5f);
        _mats["GTask"]   = MatTransparent("Decal_Tasukete",  $"{DecalDir}/graffiti_tasukete.png", 0.9f);
        _mats["GDera"]   = MatTransparent("Decal_Derarenai", $"{DecalDir}/graffiti_derarenai.png", 0.9f);
        _mats["GKoko"]   = MatTransparent("Decal_Kokoniiru", $"{DecalDir}/graffiti_kokoniiru.png", 0.9f);

        // Mirror
        _mats["Mirror"]    = MatMirror("Mirror_Glass");
        _mats["MirrorFog"] = MatMirrorFog("Mirror_Fog_Overlay");

        Debug.Log("Materials built");
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────
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

    static void ApplyByName(string pattern, Material mat)
    {
        if (mat == null) return;
        int n = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
        if (n > 0) Debug.Log($"  '{pattern}' x{n} ← {mat.name}");
    }

    static GameObject MakeDecal(string name, Material mat, Vector3 pos, Vector3 rot, Vector2 size, Transform parent)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.position = pos;
        q.transform.eulerAngles = rot;
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        q.GetComponent<MeshRenderer>().sharedMaterial = mat;
        q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        q.GetComponent<MeshRenderer>().receiveShadows = false;
        if (parent != null) q.transform.SetParent(parent, true);
        return q;
    }

    static GameObject PlaceProp(string propName, Vector3 pos, Vector3 rot, Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropsDir}/{propName}.fbx");
        if (prefab == null) { Debug.LogWarning($"Prop missing: {propName}"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) return null;
        go.transform.position = pos;
        go.transform.eulerAngles = rot;
        if (parent != null) go.transform.SetParent(parent, true);
        return go;
    }

    static void DisableDirectionalLights()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Directional)
                l.gameObject.SetActive(false);
        }
    }

    static void SetAmbient(float strength, Color tint)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = tint * strength;
    }

    // ─────────────────────────────────────────────
    // 1F
    // ─────────────────────────────────────────────
    static void ApplyToScene1F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        // Materials
        ApplyByName("Corridor_Wall", _mats["CorridorWall"]);
        ApplyByName("Reception_Wall", _mats["CorridorWall"]);
        ApplyByName("DirectorRoom_Wall", _mats["PatientWall"]);
        ApplyByName("PatientRoom", _mats["PatientWall"]);
        ApplyByName("Corridor_Floor", _mats["FloorLino"]);
        ApplyByName("Reception_Floor", _mats["FloorLino"]);
        ApplyByName("DirectorRoom_Floor", _mats["FloorLino"]);

        // Lighting - 1F病室は暗め
        DisableDirectionalLights();
        SetAmbient(0.15f, new Color(0.92f, 0.95f, 1.0f));
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Spot)
            {
                l.intensity = 0.6f;
                l.color = new Color(0.92f, 0.94f, 1.0f);
            }
            else if (l.type == LightType.Point)
            {
                l.intensity = Mathf.Max(0.35f, l.intensity * 0.7f);
                l.color = new Color(0.92f, 0.94f, 1.0f);
            }
            EditorUtility.SetDirty(l);
        }

        // Mirror (病室1)
        SetupMirror(new Vector3(-7.93f, 1.6f, 6.5f), new Vector3(0, 90, 0));

        // Decals
        var decalRoot = EnsureRoot("Decals_1F");
        // 水染み天井→壁(1F廊下少し)
        MakeDecal("WaterDrip_1F_a", _mats["Water"],   new Vector3(1.99f, 2.0f, -5f),  new Vector3(0, -90, 0), new Vector2(1.5f, 2.0f), decalRoot);
        MakeDecal("WaterDrip_1F_b", _mats["Water"],   new Vector3(-1.99f, 2.0f, 7f),  new Vector3(0, 90, 0),  new Vector2(1.5f, 2.0f), decalRoot);
        // カビ天井隅(1F)
        MakeDecal("Mold_1F_a", _mats["Mold1"], new Vector3(-1.95f, 2.85f, -15f), new Vector3(0, 90, 0), new Vector2(2.0f, 1.0f), decalRoot);
        MakeDecal("Mold_1F_b", _mats["Mold2"], new Vector3( 1.95f, 2.85f, 12f), new Vector3(0, -90, 0), new Vector2(2.0f, 1.0f), decalRoot);

        // 追加小物
        var propRoot = EnsureRoot("ExtraProps_1F");
        // ベッド周りカーテン (病室1: x=-6, z=5)
        PlaceProp("prop_bed_curtain_dirty", new Vector3(-6f, 0, 5f), new Vector3(0, 0, 0), propRoot);
        // 結露窓
        PlaceProp("prop_condensation_window", new Vector3(-7.95f, 1.7f, 5.5f), new Vector3(0, 90, 0), propRoot);
        // 落ちた点滴袋
        PlaceProp("prop_iv_bag_fallen", new Vector3(-7f, 0, 4.5f), new Vector3(0, 30, 0), propRoot);
        // 壁の古い紙
        PlaceProp("prop_old_paper", new Vector3(-4.05f, 1.5f, 6.5f), new Vector3(0, -90, 0), propRoot);
        PlaceProp("prop_old_paper", new Vector3(-7.93f, 1.4f, 11.5f), new Vector3(0, 90, 0), propRoot);
        // 剥がれかけポスター（廊下）
        PlaceProp("prop_peeling_poster", new Vector3(1.95f, 1.5f, -3f), new Vector3(0, -90, 0), propRoot);
        PlaceProp("prop_peeling_poster", new Vector3(-1.95f, 1.5f, 8f), new Vector3(0, 90, 0), propRoot);
        // 廊下散乱書類
        PlaceProp("prop_scattered_papers", new Vector3(0.3f, 0, -6f), new Vector3(0, 30, 0), propRoot);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("1F applied");
    }

    static void SetupMirror(Vector3 pos, Vector3 rot)
    {
        // 既存の Mirror プレハブのうち、鏡面メッシュ "Mir_Glass" にミラーマテリアル
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.gameObject.name.Contains("Mir_Glass"))
            {
                r.sharedMaterial = _mats["Mirror"];
                EditorUtility.SetDirty(r);
            }
        }
        // 鏡前に ReflectionProbe（Realtime + Every Frame）
        var existing = GameObject.Find("MirrorReflectionProbe");
        if (existing != null) Object.DestroyImmediate(existing);
        var go = new GameObject("MirrorReflectionProbe");
        var rp = go.AddComponent<ReflectionProbe>();
        rp.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        rp.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
        rp.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        rp.size = new Vector3(8, 4, 8);
        rp.boxProjection = true;
        rp.resolution = 256;
        go.transform.position = pos + new Vector3(0.1f * Mathf.Sin(Mathf.Deg2Rad * rot.y), 0, 0.1f * Mathf.Cos(Mathf.Deg2Rad * rot.y));
        EditorUtility.SetDirty(go);
    }

    // ─────────────────────────────────────────────
    // 2F
    // ─────────────────────────────────────────────
    static void ApplyToScene2F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        ApplyByName("Corridor2F_Wall", _mats["CorridorWall"]);
        ApplyByName("NurseStation_Wall", _mats["PatientWall"]);
        ApplyByName("TreatmentRoom", _mats["PatientWall"]);
        ApplyByName("PatientRoom2F", _mats["PatientWall"]);
        ApplyByName("Corridor2F_Floor", _mats["FloorLino"]);

        DisableDirectionalLights();
        SetAmbient(0.10f, new Color(0.95f, 0.92f, 0.78f));
        // 4500K相当 ≈ RGB(1.0, 0.92, 0.78)
        Color k4500 = new Color(1.0f, 0.92f, 0.78f);
        var allLights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int flickerCount = 0;
        for (int i = 0; i < allLights.Length; i++)
        {
            var l = allLights[i];
            if (l.type == LightType.Point || l.type == LightType.Spot)
            {
                l.intensity = 0.5f;
                l.color = k4500;
                // 一部にちらつきスクリプト
                if (flickerCount < 2 && l.GetComponent<LightFlicker>() == null)
                {
                    l.gameObject.AddComponent<LightFlicker>();
                    flickerCount++;
                }
                EditorUtility.SetDirty(l);
            }
        }

        var decalRoot = EnsureRoot("Decals_2F");
        // 廊下に水染め・カビ
        MakeDecal("Water_2F_a", _mats["Water"], new Vector3(1.99f, 1.9f, -12f), new Vector3(0, -90, 0), new Vector2(1.5f, 2.5f), decalRoot);
        MakeDecal("Water_2F_b", _mats["Water"], new Vector3(-1.99f, 1.9f, 8f),   new Vector3(0, 90, 0), new Vector2(1.5f, 2.5f), decalRoot);
        MakeDecal("Mold_2F_a",  _mats["Mold1"], new Vector3(-1.95f, 2.85f, -20f), new Vector3(0, 90, 0), new Vector2(2.2f, 1.0f), decalRoot);
        MakeDecal("Mold_2F_b",  _mats["Mold2"], new Vector3( 1.95f, 2.85f, 18f),  new Vector3(0, -90, 0), new Vector2(2.2f, 1.0f), decalRoot);

        var propRoot = EnsureRoot("ExtraProps_2F");
        // 散乱書類・剥がれかけポスター
        PlaceProp("prop_scattered_papers", new Vector3(0.4f, 0, -15f), new Vector3(0, 45, 0), propRoot);
        PlaceProp("prop_scattered_papers", new Vector3(-0.3f, 0, 5f),  new Vector3(0, 110, 0), propRoot);
        PlaceProp("prop_peeling_poster",   new Vector3(1.95f, 1.5f, 6f), new Vector3(0, -90, 0), propRoot);
        PlaceProp("prop_peeling_poster",   new Vector3(-1.95f, 1.5f, -8f), new Vector3(0, 90, 0), propRoot);
        // 落ちた点滴袋 (廊下にも)
        PlaceProp("prop_iv_bag_fallen", new Vector3(1.5f, 0, 2f), new Vector3(0, -45, 0), propRoot);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("2F applied");
    }

    // ─────────────────────────────────────────────
    // 3F
    // ─────────────────────────────────────────────
    static void ApplyToScene3F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        ApplyByName("Corridor3F_Wall", _mats["CorridorWall"]);
        ApplyByName("PatientRoom3F", _mats["PatientWall"]);
        ApplyByName("IsolationWard", _mats["PatientWall"]);
        ApplyByName("PaddedRoom", _mats["PatientWall"]);
        ApplyByName("Corridor3F_Floor", _mats["FloorTile"]);

        DisableDirectionalLights();
        SetAmbient(0.08f, new Color(1.0f, 0.88f, 0.65f));
        Color k4000 = new Color(1.0f, 0.85f, 0.60f);
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int flickerCount = 0;
        foreach (var l in lights)
        {
            if (l.type == LightType.Point || l.type == LightType.Spot)
            {
                l.intensity = 0.35f;
                l.color = k4000;
                if (flickerCount < 2 && l.GetComponent<LightFlicker>() == null)
                {
                    l.gameObject.AddComponent<LightFlicker>();
                    flickerCount++;
                }
                EditorUtility.SetDirty(l);
            }
        }

        var decalRoot = EnsureRoot("Decals_3F");
        // 血痕（壁低い位置）
        MakeDecal("Blood_3F_a", _mats["Blood1"], new Vector3(-1.49f, 0.6f, -12f),  new Vector3(0, 90, 0),  new Vector2(1.4f, 1.4f), decalRoot);
        MakeDecal("Blood_3F_b", _mats["Blood2"], new Vector3( 1.49f, 0.4f,   5f),  new Vector3(0, -90, 0), new Vector2(1.6f, 1.6f), decalRoot);
        MakeDecal("Blood_3F_c", _mats["Blood1"], new Vector3(-1.49f, 0.3f,  15f),  new Vector3(0, 90, 0),  new Vector2(1.2f, 1.2f), decalRoot);
        // 落書き
        MakeDecal("Graf_Tasukete",  _mats["GTask"], new Vector3(-1.49f, 1.5f, -8f),  new Vector3(0, 90, 0),  new Vector2(1.5f, 0.8f), decalRoot);
        MakeDecal("Graf_Derarenai", _mats["GDera"], new Vector3( 1.49f, 1.6f,  0f),  new Vector3(0, -90, 0), new Vector2(1.5f, 0.8f), decalRoot);
        MakeDecal("Graf_Kokoniiru", _mats["GKoko"], new Vector3(-1.49f, 1.3f, 10f),  new Vector3(0, 90, 0),  new Vector2(1.4f, 0.7f), decalRoot);
        // カビ天井隅
        MakeDecal("Mold_3F_a", _mats["Mold1"], new Vector3(-1.45f, 2.85f, -18f), new Vector3(0, 90, 0),  new Vector2(2.0f, 1.0f), decalRoot);
        MakeDecal("Mold_3F_b", _mats["Mold2"], new Vector3( 1.45f, 2.85f, 16f),  new Vector3(0, -90, 0), new Vector2(2.0f, 1.0f), decalRoot);
        // 水染み
        MakeDecal("Water_3F",  _mats["Water"], new Vector3(-1.49f, 1.9f, 0f), new Vector3(0, 90, 0), new Vector2(1.4f, 2.5f), decalRoot);

        var propRoot = EnsureRoot("ExtraProps_3F");
        PlaceProp("prop_scattered_papers", new Vector3(0.3f, 0, -3f), new Vector3(0, 70, 0), propRoot);
        PlaceProp("prop_iv_bag_fallen",    new Vector3(-1.2f, 0, 9f), new Vector3(0, 20, 0), propRoot);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("3F applied");
    }

    // ─────────────────────────────────────────────
    // Basement
    // ─────────────────────────────────────────────
    static void ApplyToSceneBasement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);
        ApplyByName("BaseCorridor_Wall", _mats["BasementWall"]);
        ApplyByName("RecordRoom_Wall", _mats["BasementWall"]);
        ApplyByName("DirectorArchive_Wall", _mats["BasementWall"]);
        ApplyByName("MedStorage_Wall", _mats["BasementWall"]);
        ApplyByName("BaseCorridor_Floor", _mats["FloorBasement"]);
        ApplyByName("RecordRoom_Floor", _mats["FloorBasement"]);

        // 通常照明を全削除、赤非常灯のみ
        DisableDirectionalLights();
        SetAmbient(0.05f, new Color(1.0f, 0.2f, 0.15f));
        var emergencyRoot = EnsureRoot("EmergencyLights_Basement");
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type == LightType.Point || l.type == LightType.Spot)
            {
                // 通常照明を削除
                if (!l.gameObject.name.Contains("Emergency"))
                {
                    Object.DestroyImmediate(l.gameObject);
                }
            }
        }
        // 赤非常灯×4
        float[] zs = { -14f, -4f, 6f, 14f };
        foreach (var z in zs)
        {
            var lg = new GameObject($"EmergencyLight_{z}");
            lg.transform.position = new Vector3(0, 2.8f, z);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 0.3f;
            l.range = 9f;
            l.color = new Color(1f, 50f/255f, 50f/255f);
            l.shadows = LightShadows.None;
            lg.transform.SetParent(emergencyRoot, true);
        }

        var decalRoot = EnsureRoot("Decals_Basement");
        // 血痕（地下）
        MakeDecal("Blood_B_a", _mats["Blood1"], new Vector3(-1.99f, 0.5f, -8f), new Vector3(0, 90, 0),  new Vector2(1.4f, 1.4f), decalRoot);
        MakeDecal("Blood_B_b", _mats["Blood2"], new Vector3( 1.99f, 0.3f, 8f),  new Vector3(0, -90, 0), new Vector2(1.7f, 1.7f), decalRoot);
        // カビ
        MakeDecal("Mold_B_a", _mats["Mold1"], new Vector3(-1.95f, 2.85f, -15f), new Vector3(0, 90, 0),  new Vector2(2.2f, 1.2f), decalRoot);
        MakeDecal("Mold_B_b", _mats["Mold2"], new Vector3( 1.95f, 2.85f, 12f),  new Vector3(0, -90, 0), new Vector2(2.2f, 1.2f), decalRoot);
        // 水染め
        MakeDecal("Water_B_a", _mats["Water"], new Vector3(-1.99f, 1.8f, -2f),  new Vector3(0, 90, 0),  new Vector2(1.6f, 2.8f), decalRoot);
        MakeDecal("Water_B_b", _mats["Water"], new Vector3( 1.99f, 1.8f, 4f),   new Vector3(0, -90, 0), new Vector2(1.6f, 2.8f), decalRoot);

        var propRoot = EnsureRoot("ExtraProps_Basement");
        // 散乱カルテ
        PlaceProp("prop_scattered_papers", new Vector3(-1.0f, 0, -2f),  new Vector3(0, 0, 0), propRoot);
        PlaceProp("prop_scattered_papers", new Vector3( 0.7f, 0, 5f),   new Vector3(0, 60, 0), propRoot);
        PlaceProp("prop_scattered_papers", new Vector3(-0.5f, 0, 13f),  new Vector3(0, 100, 0), propRoot);
        // 倒れた棚×2
        PlaceProp("prop_toppled_shelf", new Vector3(-1.5f, 0, -2f),  new Vector3(0, 30, 0), propRoot);
        PlaceProp("prop_toppled_shelf", new Vector3( 0.5f, 0, 6f),   new Vector3(0, -120, 0), propRoot);
        // 割れたガラス瓶×4
        PlaceProp("prop_broken_bottle", new Vector3(-0.8f, 0, -10f), new Vector3(0, 30, 0), propRoot);
        PlaceProp("prop_broken_bottle", new Vector3( 0.6f, 0, -3f),  new Vector3(0, -80, 0), propRoot);
        PlaceProp("prop_broken_bottle", new Vector3(-1.2f, 0, 3f),   new Vector3(0, 120, 0), propRoot);
        PlaceProp("prop_broken_bottle", new Vector3( 1.0f, 0, 11f),  new Vector3(0, 200, 0), propRoot);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Basement applied");
    }
}
