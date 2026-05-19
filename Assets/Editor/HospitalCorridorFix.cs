using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

public static class HospitalCorridorFix
{
    const string MatDir = "Assets/Materials";
    const string PHDir  = "Assets/Textures/PolyHaven";
    const string GenDir = "Assets/Textures/Generated";

    [MenuItem("Tools/Hospital Corridor Fix (Batch)")]
    public static void RunBatch()
    {
        try { Run(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    public static void Run()
    {
        Debug.Log("=== CorridorFix START ===");
        BuildMaterials();
        Fix1F();
        Fix2F();
        Fix3F();
        FixBasement();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== CorridorFix DONE ===");
    }

    static Material _wallUpper, _wainscot, _patientWall, _floor1F, _floor2F, _floor3F, _basementWall, _basementFloor;

    static Material BuildLit(string name, string diff, string nor, Vector2 tiling,
                              Color tint, float smooth=0.08f, float metal=0f)
    {
        string p = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, p); }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        var d = AssetDatabase.LoadAssetAtPath<Texture2D>(diff);
        if (d != null) mat.SetTexture("_BaseMap", d);
        mat.SetColor("_BaseColor", tint);
        if (!string.IsNullOrEmpty(nor))
        {
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(nor);
            if (n != null) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); mat.SetFloat("_BumpScale", 1f); }
        }
        mat.SetTextureScale("_BaseMap", tiling);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void BuildMaterials()
    {
        // 廊下上部: クリーム色 plaster (PolyHaven worn_cracked_plaster をクリーム tint)
        _wallUpper = BuildLit("Corridor_Wall",
            $"{PHDir}/worn_cracked_plaster_diff_2k.png",
            $"{PHDir}/worn_cracked_plaster_nor_2k.png",
            new Vector2(8, 1f),
            new Color(0.95f, 0.91f, 0.82f, 1f), 0.08f);
        // 病室壁: 白プラスター
        _patientWall = BuildLit("PatientRoom_Wall",
            $"{PHDir}/plastered_wall_02_diff_2k.png",
            $"{PHDir}/plastered_wall_02_nor_2k.png",
            new Vector2(2, 2),
            new Color(0.96f, 0.93f, 0.87f, 1f), 0.07f);
        // 廊下腰壁(緑)
        _wainscot = BuildLit("Wainscot_Green",
            $"{PHDir}/plastered_wall_02_diff_2k.png",
            $"{PHDir}/plastered_wall_02_nor_2k.png",
            new Vector2(8, 1f),
            new Color(0.55f, 0.62f, 0.52f, 1f), 0.12f);
        // 1F/2F/3F の床
        _floor1F = BuildLit("Floor_Linoleum",
            $"{PHDir}/old_linoleum_flooring_01_diff_2k.png",
            $"{PHDir}/old_linoleum_flooring_01_nor_2k.png",
            new Vector2(3, 16),
            Color.white, 0.30f);
        _floor2F = BuildLit("Floor_Linoleum_2F",
            $"{PHDir}/worn_tile_floor_diff_2k.png",
            $"{PHDir}/worn_tile_floor_nor_2k.png",
            new Vector2(3, 16),
            new Color(0.92f, 0.90f, 0.85f, 1f), 0.25f);
        _floor3F = BuildLit("Floor_Tile_3F",
            $"{PHDir}/large_grey_tiles_diff_2k.png",
            $"{PHDir}/large_grey_tiles_nor_2k.png",
            new Vector2(3, 22),
            new Color(0.78f, 0.76f, 0.70f, 1f), 0.25f);
        // 地下壁・床
        _basementWall = BuildLit("Concrete_Wall",
            $"{PHDir}/concrete_wall_008_diff_2k.png",
            $"{PHDir}/concrete_wall_008_nor_2k.png",
            new Vector2(4, 2),
            new Color(0.75f, 0.73f, 0.70f, 1f), 0.12f);
        _basementFloor = BuildLit("Concrete_Floor",
            $"{PHDir}/brushed_concrete_diff_2k.png",
            $"{PHDir}/brushed_concrete_nor_2k.png",
            new Vector2(4, 16),
            new Color(0.85f, 0.83f, 0.80f, 1f), 0.20f);
        Debug.Log("Materials built");
    }

    // ─────────────────────────────────────────────
    // Generic helpers
    // ─────────────────────────────────────────────
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

    static GameObject MakeWainscot(string name, Vector3 pos, Vector3 scale, Transform parent)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = name;
        Object.DestroyImmediate(c.GetComponent<BoxCollider>());
        c.transform.position = pos;
        c.transform.localScale = scale;
        c.GetComponent<MeshRenderer>().sharedMaterial = _wainscot;
        c.transform.SetParent(parent, true);
        return c;
    }

    static GameObject MakeTrim(string name, Vector3 pos, Vector3 scale, Transform parent)
    {
        var trimMat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/Trim_Dark.mat");
        if (trimMat == null)
        {
            trimMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            trimMat.SetColor("_BaseColor", new Color(0.30f, 0.32f, 0.28f, 1f));
            trimMat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(trimMat, $"{MatDir}/Trim_Dark.mat");
        }
        var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.name = name;
        Object.DestroyImmediate(c.GetComponent<BoxCollider>());
        c.transform.position = pos;
        c.transform.localScale = scale;
        c.GetComponent<MeshRenderer>().sharedMaterial = trimMat;
        c.transform.SetParent(parent, true);
        return c;
    }

    // ─────────────────────────────────────────────
    // 1F: corridor 4x32 (x:±2, z:-16..+16)
    // ─────────────────────────────────────────────
    static void Fix1F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        ApplyByName("Corridor_Wall", _wallUpper);
        ApplyByName("Reception_Wall", _wallUpper);
        ApplyByName("DirectorRoom_Wall", _patientWall);
        ApplyByName("PatientRoom", _patientWall);
        ApplyByName("Corridor_Floor", _floor1F);
        ApplyByName("Reception_Floor", _floor1F);
        ApplyByName("DirectorRoom_Floor", _floor1F);

        // 廊下に腰壁追加 (左壁:x=-2, 右壁:x=+2 / z:-16..+16, length 32m)
        var root = EnsureRoot("Wainscot_1F");
        MakeWainscot("WL", new Vector3(-1.9f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 32f), root);
        MakeWainscot("WR", new Vector3( 1.9f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 32f), root);
        // トリム（暗い帯）腰壁上端
        MakeTrim("TL", new Vector3(-1.89f, 1.02f, 0f), new Vector3(0.07f, 0.04f, 32f), root);
        MakeTrim("TR", new Vector3( 1.89f, 1.02f, 0f), new Vector3(0.07f, 0.04f, 32f), root);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("1F fixed");
    }

    // ─────────────────────────────────────────────
    // 2F: corridor 4x48 (x:±2, z:-24..+24)
    // ─────────────────────────────────────────────
    static void Fix2F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        ApplyByName("Corridor2F_Wall", _wallUpper);
        ApplyByName("NurseStation_Wall", _patientWall);
        ApplyByName("TreatmentRoom", _patientWall);
        ApplyByName("PatientRoom2F", _patientWall);
        ApplyByName("Corridor2F_Floor", _floor2F);

        var root = EnsureRoot("Wainscot_2F");
        MakeWainscot("WL", new Vector3(-1.9f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 48f), root);
        MakeWainscot("WR", new Vector3( 1.9f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 48f), root);
        MakeTrim("TL", new Vector3(-1.89f, 1.02f, 0f), new Vector3(0.07f, 0.04f, 48f), root);
        MakeTrim("TR", new Vector3( 1.89f, 1.02f, 0f), new Vector3(0.07f, 0.04f, 48f), root);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("2F fixed");
    }

    // ─────────────────────────────────────────────
    // 3F: corridor 3x44 (x:±1.5, z:-22..+22)
    // ─────────────────────────────────────────────
    static void Fix3F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        ApplyByName("Corridor3F_Wall", _wallUpper);
        ApplyByName("PatientRoom3F", _patientWall);
        ApplyByName("IsolationWard", _patientWall);
        ApplyByName("PaddedRoom", _patientWall);
        ApplyByName("Corridor3F_Floor", _floor3F);

        var root = EnsureRoot("Wainscot_3F");
        MakeWainscot("WL", new Vector3(-1.4f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 44f), root);
        MakeWainscot("WR", new Vector3( 1.4f, 0.5f, 0f), new Vector3(0.05f, 1.0f, 44f), root);
        MakeTrim("TL", new Vector3(-1.39f, 1.02f, 0f), new Vector3(0.07f, 0.04f, 44f), root);
        MakeTrim("TR", new Vector3( 1.39f, 1.02f, 0f), new Vector3(0.07f, 0.04f, 44f), root);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("3F fixed");
    }

    // ─────────────────────────────────────────────
    // Basement (no wainscot)
    // ─────────────────────────────────────────────
    static void FixBasement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);
        ApplyByName("BaseCorridor_Wall", _basementWall);
        ApplyByName("RecordRoom_Wall", _basementWall);
        ApplyByName("DirectorArchive_Wall", _basementWall);
        ApplyByName("MedStorage_Wall", _basementWall);
        ApplyByName("BaseCorridor_Floor", _basementFloor);
        ApplyByName("RecordRoom_Floor", _basementFloor);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Basement fixed");
    }
}
