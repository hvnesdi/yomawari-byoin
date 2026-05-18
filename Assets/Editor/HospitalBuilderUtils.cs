using UnityEngine;
using UnityEditor;
using UnityEngine.AI;
using Unity.AI.Navigation;
#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// 全フロアシーンビルダーで共用するヘルパーメソッド群。
/// </summary>
public static class HospitalBuilderUtils
{
    public const string MatFolder = "Assets/Materials";

    public static void EnsureMatFolder()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");
    }

    // ─── Materials ───────────────────────────────────────────────────

    public static Material GetOrCreateColorMat(string matName, Color baseColor, float roughness, float metallic)
    {
        EnsureMatFolder();
        string path = $"{MatFolder}/{matName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", 1f - roughness);
        mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ─── Geometry ────────────────────────────────────────────────────

    public static GameObject CreateCubeWithMat(string name, Vector3 pos, Vector3 scale, Material mat = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (mat != null)
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    public static void CreateRoom(string name, Vector3 center, Vector3 size,
        Material matWall, Material matFloor, Material matCeiling)
    {
        var room = new GameObject(name);
        room.transform.position = center;

        float hx = size.x / 2f, hy = size.y / 2f, hz = size.z / 2f;
        CreateCubeWithMat(name + "_Floor",     center - new Vector3(0, hy - 0.1f, 0),  new Vector3(size.x, 0.2f, size.z), matFloor);
        CreateCubeWithMat(name + "_Ceiling",   center + new Vector3(0, hy - 0.1f, 0),  new Vector3(size.x, 0.2f, size.z), matCeiling);
        CreateCubeWithMat(name + "_WallFront", center - new Vector3(0, 0, hz),          new Vector3(size.x, size.y, 0.2f), matWall);
        CreateCubeWithMat(name + "_WallBack",  center + new Vector3(0, 0, hz),          new Vector3(size.x, size.y, 0.2f), matWall);
        CreateCubeWithMat(name + "_WallLeft",  center - new Vector3(hx, 0, 0),          new Vector3(0.2f, size.y, size.z), matWall);
        CreateCubeWithMat(name + "_WallRight", center + new Vector3(hx, 0, 0),          new Vector3(0.2f, size.y, size.z), matWall);
    }

    // ─── Lighting ────────────────────────────────────────────────────

    public static void AddDirectionalLight(float intensity, Color color)
    {
        var go = new GameObject("DirectionalLight");
        var l = go.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = intensity;
        l.color = color;
        go.transform.rotation = Quaternion.Euler(50, -30, 0);
    }

    public static void AddPointLight(string name, Vector3 pos, float intensity, float range, Color color)
    {
        var go = new GameObject(name);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.intensity = intensity;
        l.range = range;
        l.color = color;
        go.transform.position = pos;
    }

    public static void AddSpotLight(string name, Vector3 pos, float intensity, Color color)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.intensity = intensity;
        l.range = 6f;
        l.spotAngle = 60f;
        l.color = color;
    }

    // ─── Post Processing ─────────────────────────────────────────────

    public static void SetupGlobalVolume(string profileAssetPath, float postExposure, float vignetteIntensity, Color tint)
    {
#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
        var go = new GameObject("GlobalVolume");
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var vig = profile.Add<Vignette>(true);
        vig.intensity.value = vignetteIntensity;
        vig.intensity.overrideState = true;

        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.value = postExposure;
        ca.postExposure.overrideState = true;
        ca.colorFilter.value = tint;
        ca.colorFilter.overrideState = true;

        vol.profile = profile;
        AssetDatabase.CreateAsset(profile, profileAssetPath);
#endif
    }

    // ─── Player ──────────────────────────────────────────────────────

    public static GameObject CreatePlayer(Vector3 spawnPos)
    {
        var player = new GameObject("Player");
        player.transform.position = spawnPos;
        player.tag = "Player";

        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.3f;
        player.AddComponent<PlayerController>();
        player.AddComponent<PlayerSync>();
        player.AddComponent<PlayerSpawnOnLoad>();

        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0, 0.8f, 0);
        camGo.AddComponent<Camera>();
        var cc2 = camGo.AddComponent<CameraController>();

        var f = typeof(CameraController).GetField("playerBody",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        f?.SetValue(cc2, player.transform);

        return player;
    }

    // ─── Enemy ───────────────────────────────────────────────────────

    public static void CreateEnemy(Vector3 pos, float[] waypointZs)
    {
        var enemy = new GameObject("Enemy");
        enemy.transform.position = pos;
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 1.8f;
        var ec = enemy.AddComponent<EnemyController>();

        var wps = new Transform[waypointZs.Length];
        for (int i = 0; i < waypointZs.Length; i++)
        {
            var wp = new GameObject("Waypoint_" + (i + 1));
            wp.transform.position = new Vector3(0, 0.5f, waypointZs[i]);
            wps[i] = wp.transform;
        }

        var f = typeof(EnemyController).GetField("waypoints",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        f?.SetValue(ec, wps);
    }

    // ─── Triggers ────────────────────────────────────────────────────

    public static void CreateTransitionTrigger(string name, Vector3 pos, Vector3 boxSize,
        string targetScene, AreaID targetArea, Vector3 spawnInTarget, string message = "")
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var col = go.AddComponent<BoxCollider>();
        col.size = boxSize;
        col.isTrigger = true;
        var t = go.AddComponent<SceneTransitionTrigger>();
        t.targetScene = targetScene;
        t.targetArea = targetArea;
        t.playerSpawnPosition = spawnInTarget;
        t.transitionMessage = message;
    }

    public static void CreateEscapeTrigger(string name, Vector3 triggerPos, Vector3 boxSize, Vector3 returnPos)
    {
        var go = new GameObject(name);
        go.transform.position = triggerPos;
        var col = go.AddComponent<BoxCollider>();
        col.size = boxSize;
        col.isTrigger = true;
        var t = go.AddComponent<EscapeAttemptTrigger>();

        var spawnGo = new GameObject(name + "_ReturnPoint");
        spawnGo.transform.position = returnPos;
        t.returnSpawnPoint = spawnGo.transform;
    }

    public static GameObject CreateClueObject(string name, Vector3 pos, ClueType clueType, string prompt, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;

        Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        var sphere = go.AddComponent<SphereCollider>();
        sphere.radius = 1.5f;
        sphere.isTrigger = true;

        var clue = go.AddComponent<ClueInteractable>();
        clue.clueType = clueType;
        clue.promptText = prompt;
        return go;
    }

    // ─── NavMesh ─────────────────────────────────────────────────────

    public static void BakeNavMesh()
    {
        var go = new GameObject("NavMeshSurface");
        var surface = go.AddComponent<NavMeshSurface>();
        surface.BuildNavMesh();
        Debug.Log("[HospitalBuilderUtils] NavMesh baked.");
    }
}
