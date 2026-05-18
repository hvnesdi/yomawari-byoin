using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HospitalVisualImprover
{
    const string MatFolder = "Assets/Materials";
    const string TexFolder = "Assets/Textures";

    [MenuItem("Tools/Improve All Hospital Visuals")]
    public static void ImproveAll()
    {
        Debug.Log("=== HospitalVisualImprover: Start ===");
        EnsureMaterialFolder();
        Improve1F();
        Improve2F();
        Improve3F();
        ImproveBasement();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== HospitalVisualImprover: All Done ===");
    }

    static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");
    }

    // ─── 1F ───────────────────────────────────────────────────────────────
    public static void Improve1F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("Cannot open Hospital.unity"); return; }

        var matCorridorWall  = CreateTexMaterial("Corridor_Wall",   "Corridor_Wall_Diffuse",  "HospitalWall");
        var matPatientWall   = CreateTexMaterial("PatientRoom_Wall", "PatientRoom_Wall_Diffuse","HospitalWall");
        var matFloor         = CreateTexMaterial("Floor_Linoleum",   "Floor_Linoleum_Diffuse",  "HospitalFloor");
        var matCeiling       = CreateTexMaterial("Ceiling_1F",       "Ceiling_Diffuse",          "HospitalCeiling");
        var matSheet         = CreateColorMat("Sheet_1F",      new Color(0.82f,0.80f,0.72f), 0.95f, 0f);
        var matBedMetal      = CreateColorMat("BedMetal_1F",   new Color(0.55f,0.50f,0.45f), 0.85f, 0.7f);
        var matIVMetal       = CreateColorMat("IVMetal_1F",    new Color(0.58f,0.54f,0.48f), 0.85f, 0.7f);
        var matDoorWood      = CreateColorMat("DoorWood_1F",   new Color(0.40f,0.28f,0.18f), 0.88f, 0f);
        var matDoorMetal     = CreateColorMat("DoorMetal_1F",  new Color(0.55f,0.52f,0.46f), 0.80f, 0.7f);
        var matFluor         = CreateEmissiveMat("FluorTube_1F", new Color(0.85f,0.92f,0.78f), new Color(0.6f,0.7f,0.5f)*2f);

        ApplyByPattern("Corridor_Wall",     matCorridorWall);
        ApplyByPattern("PatientRoom",       matPatientWall);
        ApplyByPattern("DirectorRoom_Wall", matPatientWall);
        ApplyByPattern("Reception_Wall",    matCorridorWall);
        ApplyByPattern("Corridor_Floor",    matFloor);
        ApplyByPattern("Reception_Floor",   matFloor);
        ApplyByPattern("DirectorRoom_Floor",matFloor);
        ApplyByPattern("Corridor_Ceiling",  matCeiling);
        ApplyByPattern("Reception_Ceil",    matCeiling);
        ApplyByPattern("DirectorRoom_Ceil", matCeiling);
        ApplyByPattern("_Bed",              matSheet);
        ApplyByPattern("_Sheet",            matSheet);
        ApplyByPattern("BedFrame",          matBedMetal);
        ApplyByPattern("IVPole",            matIVMetal);
        ApplyByPattern("IVStand",           matIVMetal);
        ApplyByPattern("Door_",             matDoorWood);
        ApplyByPattern("FluorTube",         matFluor);
        ApplyByPattern("Light_",            matFluor);

        AdjustLights(0.8f, new Color(0.78f, 0.88f, 1.0f));
        AdjustPostProcess(scene, -1.2f, new Color(0.78f, 0.88f, 1.0f), 0.45f, 0.8f);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("1F improved");
    }

    // ─── 2F ───────────────────────────────────────────────────────────────
    public static void Improve2F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("Cannot open Hospital2F.unity"); return; }

        var matCorridorWall  = CreateTexMaterial("Corridor_Wall",   "Corridor_Wall_Diffuse",   "HospitalWall");
        var matPatientWall   = CreateTexMaterial("PatientRoom_Wall", "PatientRoom_Wall_Diffuse", "HospitalWall");
        var matFloor         = CreateTexMaterial("Floor_Linoleum",   "Floor_Linoleum_Diffuse",   "HospitalFloor");
        var matCeiling       = CreateTexMaterial("Ceiling_2F",       "Ceiling_Diffuse",           "HospitalCeiling");
        var matSheet         = CreateColorMat("Sheet_2F",    new Color(0.78f,0.76f,0.68f), 0.95f, 0f);
        var matBedMetal      = CreateColorMat("BedMetal_2F", new Color(0.50f,0.46f,0.42f), 0.85f, 0.7f);
        var matIVMetal       = CreateColorMat("IVMetal_2F",  new Color(0.55f,0.50f,0.44f), 0.85f, 0.7f);
        var matFluor         = CreateEmissiveMat("FluorTube_2F", new Color(0.80f,0.88f,0.75f), new Color(0.5f,0.6f,0.4f)*1.8f);
        var matLocker        = CreateColorMat("Locker_2F",   new Color(0.60f,0.62f,0.58f), 0.75f, 0.3f);

        ApplyByPattern("Corridor2F_Wall",   matCorridorWall);
        ApplyByPattern("PatientRoom2F",     matPatientWall);
        ApplyByPattern("NurseStation_Wall", matPatientWall);
        ApplyByPattern("TreatmentRoom",     matPatientWall);
        ApplyByPattern("Corridor2F_Floor",  matFloor);
        ApplyByPattern("PatientRoom2F",     matFloor);
        ApplyByPattern("Corridor2F_Ceiling",matCeiling);
        ApplyByPattern("_Bed",              matSheet);
        ApplyByPattern("_Locker",           matLocker);
        ApplyByPattern("_IVPole",           matIVMetal);
        ApplyByPattern("FluorTube",         matFluor);
        ApplyByPattern("Light2F_",          matFluor);

        AdjustLights(0.7f, new Color(0.72f, 0.82f, 1.0f));
        AdjustPostProcess(scene, -1.4f, new Color(0.72f, 0.82f, 1.0f), 0.50f, 0.9f);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("2F improved");
    }

    // ─── 3F ───────────────────────────────────────────────────────────────
    public static void Improve3F()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("Cannot open Hospital3F.unity"); return; }

        var matCorridorWall  = CreateTexMaterial("Corridor_Wall",    "Corridor_Wall_Diffuse",   "HospitalWall");
        var matPatientWall   = CreateTexMaterial("PatientRoom_Wall",  "PatientRoom_Wall_Diffuse", "HospitalWall");
        var matFloor         = CreateTexMaterial("Floor_Linoleum",    "Floor_Linoleum_Diffuse",   "HospitalFloor");
        var matCeiling       = CreateTexMaterial("Ceiling_3F",        "Ceiling_Diffuse",           "HospitalCeiling");
        var matIsolation     = CreateColorMat("Isolation_Wall", new Color(0.72f,0.70f,0.65f), 0.92f, 0f);
        var matPadded        = CreateColorMat("Padded_Wall",    new Color(0.68f,0.65f,0.60f), 0.98f, 0f);
        var matFluor         = CreateEmissiveMat("FluorTube_3F", new Color(0.75f,0.82f,0.70f), new Color(0.4f,0.5f,0.3f)*1.5f);
        var matSheet         = CreateColorMat("Sheet_3F",   new Color(0.70f,0.68f,0.60f), 0.95f, 0f);
        var matBedMetal      = CreateColorMat("BedMetal_3F",new Color(0.45f,0.40f,0.38f), 0.85f, 0.7f);

        ApplyByPattern("Corridor3F_Wall",    matCorridorWall);
        ApplyByPattern("PatientRoom3F",      matPatientWall);
        ApplyByPattern("IsolationWard",      matIsolation);
        ApplyByPattern("Isolation_",         matIsolation);
        ApplyByPattern("PaddedRoom",         matPadded);
        ApplyByPattern("Corridor3F_Floor",   matFloor);
        ApplyByPattern("Corridor3F_Ceiling", matCeiling);
        ApplyByPattern("_Bed",               matSheet);
        ApplyByPattern("FluorTube",          matFluor);
        ApplyByPattern("Light3F_",           matFluor);

        AdjustLights(0.55f, new Color(0.65f, 0.75f, 1.0f));
        AdjustPostProcess(scene, -1.6f, new Color(0.65f, 0.75f, 1.0f), 0.55f, 1.0f);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("3F improved");
    }

    // ─── Basement ─────────────────────────────────────────────────────────
    public static void ImproveBasement()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("Cannot open HospitalBasement.unity"); return; }

        var matConcreteWall  = CreateColorMat("Concrete_Wall",  new Color(0.55f,0.53f,0.50f), 0.90f, 0f);
        var matConcreteFloor = CreateColorMat("Concrete_Floor", new Color(0.45f,0.43f,0.40f), 0.88f, 0.1f);
        var matConcreteCeil  = CreateColorMat("Concrete_Ceil",  new Color(0.50f,0.48f,0.46f), 0.90f, 0f);
        var matShelves       = CreateColorMat("Shelves_Base",   new Color(0.35f,0.28f,0.22f), 0.85f, 0f);
        var matMetal         = CreateColorMat("Metal_Base",     new Color(0.40f,0.38f,0.35f), 0.70f, 0.5f);
        var matFluor         = CreateEmissiveMat("FluorTube_Base", new Color(0.70f,0.75f,0.65f), new Color(0.3f,0.4f,0.2f)*1.2f);

        ApplyByPattern("BaseCorridor_Wall",   matConcreteWall);
        ApplyByPattern("RecordRoom_Wall",     matConcreteWall);
        ApplyByPattern("DirectorArchive_Wall",matConcreteWall);
        ApplyByPattern("MedStorage_Wall",     matConcreteWall);
        ApplyByPattern("BaseCorridor_Floor",  matConcreteFloor);
        ApplyByPattern("RecordRoom_Floor",    matConcreteFloor);
        ApplyByPattern("BaseCorridor_Ceiling",matConcreteCeil);
        ApplyByPattern("RecordRoom_Ceil",     matConcreteCeil);
        ApplyByPattern("Shelf_",              matShelves);
        ApplyByPattern("MedShelf_",           matShelves);
        ApplyByPattern("_Cabinet",            matMetal);
        ApplyByPattern("_Desk",               matShelves);
        ApplyByPattern("FluorTube",           matFluor);
        ApplyByPattern("BaseLight_",          matFluor);

        AdjustLights(0.4f, new Color(0.60f, 0.68f, 1.0f));
        AdjustPostProcess(scene, -1.8f, new Color(0.60f, 0.68f, 1.0f), 0.60f, 1.1f);

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Basement improved");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    static Material CreateTexMaterial(string matName, string texName, string fallbackTexPrefix)
    {
        string matPath = $"{MatFolder}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", new Color(0.9f, 0.88f, 0.80f, 1f));
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.1f);

        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{texName}.png");
        if (diff == null) diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{fallbackTexPrefix}_Diffuse.png");
        if (diff != null) mat.SetTexture("_BaseMap", diff);

        var norm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexFolder}/{fallbackTexPrefix}_Normal.png");
        if (norm != null) { mat.SetTexture("_BumpMap", norm); mat.EnableKeyword("_NORMALMAP"); }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateColorMat(string matName, Color col, float roughness, float metallic)
    {
        string matPath = $"{MatFolder}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", col);
        mat.SetFloat("_Smoothness", 1f - roughness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateEmissiveMat(string matName, Color baseCol, Color emissCol)
    {
        Material mat = CreateColorMat(matName, baseCol, 0.1f, 0f);
        mat.SetColor("_EmissionColor", emissCol);
        mat.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void ApplyByPattern(string pattern, Material mat)
    {
        int count = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.name.Contains(pattern))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                count++;
            }
        }
        if (count > 0) Debug.Log($"  '{pattern}': {count} renderers");
    }

    static void AdjustLights(float intensityScale, Color ambientColor)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor * 0.08f;

        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            l.intensity *= intensityScale;
            if (l.type == LightType.Directional)
                l.gameObject.SetActive(false);
            EditorUtility.SetDirty(l);
        }
    }

    static void AdjustPostProcess(UnityEngine.SceneManagement.Scene scene, float exposure,
                                   Color colorFilter, float vignetteIntensity, float bloomIntensity)
    {
        var volume = Object.FindFirstObjectByType<Volume>();
        if (volume == null || volume.profile == null) return;
        var profile = volume.profile;

        if (!profile.TryGet<ColorAdjustments>(out var ca))
            ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.value = exposure;
        ca.postExposure.overrideState = true;
        ca.colorFilter.value = colorFilter;
        ca.colorFilter.overrideState = true;

        if (!profile.TryGet<Vignette>(out var vig))
            vig = profile.Add<Vignette>(true);
        vig.intensity.value = vignetteIntensity;
        vig.intensity.overrideState = true;

        if (!profile.TryGet<Bloom>(out var bloom))
            bloom = profile.Add<Bloom>(true);
        bloom.intensity.value = bloomIntensity;
        bloom.intensity.overrideState = true;
        bloom.threshold.value = 0.8f;
        bloom.threshold.overrideState = true;

        EditorUtility.SetDirty(profile);
    }
}
