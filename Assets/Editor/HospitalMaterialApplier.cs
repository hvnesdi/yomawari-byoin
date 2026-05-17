using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class HospitalMaterialApplier
{
    [MenuItem("Tools/Apply Hospital Materials")]
    public static void ApplyMaterials()
    {
        Debug.Log("=== HospitalMaterialApplier: Start ===");

        string matFolder = "Assets/Materials";
        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string scenePath = "Assets/Scenes/Hospital.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Cannot open Hospital.unity");
            return;
        }

        var matWall    = CreateURPMaterial("HospitalWall",    "HospitalWall",    matFolder);
        var matFloor   = CreateURPMaterial("HospitalFloor",   "HospitalFloor",   matFolder);
        var matCeiling = CreateURPMaterial("HospitalCeiling", "HospitalCeiling", matFolder);
        var matPatient = CreateURPMaterial("PatientWall",     "PatientWall",     matFolder);

        var matBedMetal  = CreateColorMaterial("BedMetal",  new Color(0.55f, 0.50f, 0.45f), matFolder, 0.85f, 0.7f);
        var matSheet     = CreateColorMaterial("Sheet",      new Color(0.82f, 0.80f, 0.72f), matFolder, 0.95f, 0.0f);
        var matIVMetal   = CreateColorMaterial("IVMetal",   new Color(0.58f, 0.54f, 0.48f), matFolder, 0.85f, 0.7f);
        var matWoodDoor  = CreateColorMaterial("DoorWood",  new Color(0.40f, 0.28f, 0.18f), matFolder, 0.88f, 0.0f);
        var matDoorMetal = CreateColorMaterial("DoorMetal", new Color(0.55f, 0.52f, 0.46f), matFolder, 0.80f, 0.7f);
        var matLight     = CreateColorMaterial("FluorTube", new Color(0.85f, 0.92f, 0.78f), matFolder, 0.1f,  0.0f);
        matLight.SetColor("_EmissionColor", new Color(0.6f, 0.7f, 0.5f) * 2.0f);
        matLight.EnableKeyword("_EMISSION");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ApplyByNamePattern("Corridor_Wall",     matWall);
        ApplyByNamePattern("Corridor_Floor",    matFloor);
        ApplyByNamePattern("Corridor_Ceiling",  matCeiling);
        ApplyByNamePattern("Reception_Wall",    matWall);
        ApplyByNamePattern("Reception_Floor",   matFloor);
        ApplyByNamePattern("Reception_Ceil",    matCeiling);
        ApplyByNamePattern("DirectorRoom_Wall", matPatient);
        ApplyByNamePattern("DirectorRoom_Floor",matFloor);
        ApplyByNamePattern("DirectorRoom_Ceil", matCeiling);
        ApplyByNamePattern("PatientRoom",       matPatient);
        ApplyByNamePattern("Bed_",              matSheet);

        PlaceModel("Assets/Models/HospitalBed.fbx",      new Vector3(-5f, 0f, 5f),    "HospitalBed_1",  matBedMetal, matSheet);
        PlaceModel("Assets/Models/HospitalBed.fbx",      new Vector3(-5f, 0f, 11f),   "HospitalBed_2",  matBedMetal, matSheet);
        PlaceModel("Assets/Models/HospitalBed.fbx",      new Vector3(-5f, 0f, 17f),   "HospitalBed_3",  matBedMetal, matSheet);
        PlaceModel("Assets/Models/IVStand.fbx",          new Vector3(-3.5f, 0f, 6f),  "IVStand_1",      matIVMetal);
        PlaceModel("Assets/Models/IVStand.fbx",          new Vector3(-3.5f, 0f, 12f), "IVStand_2",      matIVMetal);
        PlaceModel("Assets/Models/HospitalDoor.fbx",     new Vector3(-4f, 0f, 3f),    "Door_Room1",     matWoodDoor, matDoorMetal);
        PlaceModel("Assets/Models/HospitalDoor.fbx",     new Vector3(-4f, 0f, 9f),    "Door_Room2",     matWoodDoor, matDoorMetal);
        PlaceModel("Assets/Models/HospitalDoor.fbx",     new Vector3(-4f, 0f, 15f),   "Door_Room3",     matWoodDoor, matDoorMetal);
        PlaceModel("Assets/Models/FluorescentLight.fbx", new Vector3(0f, 2.95f, -10f),"Light_1",        matLight);
        PlaceModel("Assets/Models/FluorescentLight.fbx", new Vector3(0f, 2.95f, -3f), "Light_2",        matLight);
        PlaceModel("Assets/Models/FluorescentLight.fbx", new Vector3(0f, 2.95f, 4f),  "Light_3",        matLight);
        PlaceModel("Assets/Models/FluorescentLight.fbx", new Vector3(0f, 2.95f, 11f), "Light_4",        matLight);

        AdjustPostProcessing();

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("=== HospitalMaterialApplier: Done! ===");
    }

    static Material CreateURPMaterial(string matName, string texturePrefix, string folder)
    {
        string matPath = $"{folder}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");

        Texture2D diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texturePrefix}_Diffuse.png");
        if (diff != null) mat.SetTexture("_BaseMap", diff);

        Texture2D norm = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texturePrefix}_Normal.png");
        if (norm != null)
        {
            mat.SetTexture("_BumpMap", norm);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.0f);
        }

        Texture2D rough = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Textures/{texturePrefix}_Roughness.png");
        if (rough != null)
        {
            mat.SetTexture("_MetallicGlossMap", rough);
            mat.SetFloat("_Smoothness", 0.1f);
        }

        mat.SetFloat("_Metallic", 0.0f);
        mat.SetColor("_BaseColor", new Color(0.9f, 0.88f, 0.80f, 1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material CreateColorMaterial(string matName, Color baseColor, string folder, float roughness, float metallic)
    {
        string matPath = $"{folder}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 1f - roughness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void ApplyByNamePattern(string namePattern, Material mat)
    {
        int count = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (r.gameObject.name.Contains(namePattern))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                count++;
            }
        }
        Debug.Log($"ApplyByNamePattern '{namePattern}': applied to {count} renderers");
    }

    static void PlaceModel(string fbxPath, Vector3 position, string goName, params Material[] materials)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (prefab == null)
        {
            Debug.LogWarning($"FBX not found: {fbxPath}");
            return;
        }
        var existing = GameObject.Find(goName);
        if (existing != null) Object.DestroyImmediate(existing);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = goName;
        go.transform.position = position;

        if (materials != null && materials.Length > 0)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = materials[i < materials.Length ? i : materials.Length - 1];
                r.sharedMaterials = mats;
            }
        }
        Debug.Log($"Placed: {goName} at {position}");
    }

    static void AdjustPostProcessing()
    {
        var volume = Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        if (volume == null || volume.profile == null)
        {
            Debug.Log("No Volume found to adjust");
            return;
        }
        var profile = volume.profile;

        if (profile.TryGet<UnityEngine.Rendering.Universal.Vignette>(out var vig))
        {
            vig.intensity.value = 0.45f;
            vig.intensity.overrideState = true;
        }
        if (profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var ca))
        {
            ca.postExposure.value = -1.2f;
            ca.postExposure.overrideState = true;
            ca.colorFilter.value = new Color(0.78f, 0.88f, 1.0f);
            ca.colorFilter.overrideState = true;
        }
        if (profile.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom))
        {
            bloom.intensity.value = 0.8f;
            bloom.intensity.overrideState = true;
            bloom.threshold.value = 0.8f;
            bloom.threshold.overrideState = true;
        }
        else
        {
            var newBloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            newBloom.intensity.value = 0.8f;
            newBloom.intensity.overrideState = true;
            newBloom.threshold.value = 0.8f;
            newBloom.threshold.overrideState = true;
        }
        EditorUtility.SetDirty(profile);
        Debug.Log("PostProcessing adjusted");
    }
}
