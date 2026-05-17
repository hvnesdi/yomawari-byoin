using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class HospitalSceneBuilder
{
    [MenuItem("Tools/Build Hospital Scene")]
    public static void BuildHospitalScene()
    {
        // 新規シーン作成
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 廊下の床・天井・壁
        CreateCube("Corridor_Floor", new Vector3(0, 0, 0), new Vector3(4, 0.2f, 32));
        CreateCube("Corridor_Ceiling", new Vector3(0, 3f, 0), new Vector3(4, 0.2f, 32));
        CreateCube("Corridor_WallLeft", new Vector3(-2, 1.5f, 0), new Vector3(0.2f, 3, 32));
        CreateCube("Corridor_WallRight", new Vector3(2, 1.5f, 0), new Vector3(0.2f, 3, 32));

        // 受付エリア（廊下の端、z=-14）
        CreateRoom("Reception", new Vector3(0, 1.5f, -14), new Vector3(6, 3, 6));

        // 院長室（廊下右側、z=0, x=5）
        CreateRoom("DirectorRoom", new Vector3(6, 1.5f, 0), new Vector3(5, 3, 5));

        // 病室3部屋（廊下左側）
        for (int i = 0; i < 3; i++)
        {
            float z = 5 + i * 6f;
            var roomGo = CreateRoom("PatientRoom_" + (i + 1), new Vector3(-6, 1.5f, z), new Vector3(4, 3, 5));
            // ベッド
            CreateCube("Bed_" + (i + 1), new Vector3(-6, 0.35f, z), new Vector3(1.5f, 0.5f, 2f));
        }

        // DirectionalLight
        var dirLightGo = new GameObject("DirectionalLight");
        var dirLight = dirLightGo.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.intensity = 0.05f;
        dirLight.color = Color.white;
        dirLightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        // PointLight x4（廊下均等配置）
        float[] pointZs = { -10f, -3f, 4f, 11f };
        Color blueWhite = new Color(0.63f, 0.71f, 1.0f);
        foreach (var pz in pointZs)
        {
            var pl = new GameObject("PointLight_" + pz);
            var l = pl.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 0.5f;
            l.range = 8f;
            l.color = blueWhite;
            pl.transform.position = new Vector3(0, 2.5f, pz);
        }

        // SpotLight per room
        CreateSpotLight("Spot_Reception", new Vector3(0, 2.8f, -14));
        CreateSpotLight("Spot_Director", new Vector3(6, 2.8f, 0));
        CreateSpotLight("Spot_Room1", new Vector3(-6, 2.8f, 5));
        CreateSpotLight("Spot_Room2", new Vector3(-6, 2.8f, 11));
        CreateSpotLight("Spot_Room3", new Vector3(-6, 2.8f, 17));

        // Global Volume
#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
        var volumeGo = new GameObject("GlobalVolume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.45f;
        vignette.intensity.overrideState = true;
        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.value = -1.0f;
        colorAdj.postExposure.overrideState = true;
        volume.profile = profile;
        UnityEditor.AssetDatabase.CreateAsset(profile, "Assets/Scenes/HospitalVolumeProfile.asset");
#endif

        // Player
        var player = new GameObject("Player");
        player.transform.position = new Vector3(-5f, 1f, 5f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.3f;
        var pc = player.AddComponent<PlayerController>();
        // FPS Camera (child)
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0, 0.8f, 0);
        camGo.AddComponent<Camera>();
        var cameraCtrl = camGo.AddComponent<CameraController>();
        // Set playerBody via reflection
        var bodyField = typeof(CameraController).GetField("playerBody",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (bodyField != null) bodyField.SetValue(cameraCtrl, player.transform);

        // Enemy
        var enemy = new GameObject("Enemy");
        enemy.transform.position = new Vector3(0, 1f, -12f);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 1.8f;
        var ec = enemy.AddComponent<EnemyController>();
        // Waypoints
        var waypoints = new Transform[3];
        float[] wpZ = { -8f, 0f, 8f };
        for (int i = 0; i < 3; i++)
        {
            var wp = new GameObject("Waypoint_" + (i + 1));
            wp.transform.position = new Vector3(0, 0.5f, wpZ[i]);
            waypoints[i] = wp.transform;
        }
        var waypointField = typeof(EnemyController).GetField("waypoints",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (waypointField != null) waypointField.SetValue(ec, waypoints);

        // シーン保存
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Hospital.unity");
        UnityEditor.AssetDatabase.Refresh();
        Debug.Log("HospitalSceneBuilder: Hospital.unity saved successfully!");
    }

    static GameObject CreateCube(string name, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        return go;
    }

    static GameObject CreateRoom(string name, Vector3 center, Vector3 size)
    {
        var room = new GameObject(name);
        room.transform.position = center;
        // Floor
        CreateCube(name + "_Floor", center - new Vector3(0, size.y / 2 - 0.1f, 0), new Vector3(size.x, 0.2f, size.z));
        // Ceiling
        CreateCube(name + "_Ceiling", center + new Vector3(0, size.y / 2 - 0.1f, 0), new Vector3(size.x, 0.2f, size.z));
        // Walls
        CreateCube(name + "_WallFront", center + new Vector3(0, 0, -size.z / 2), new Vector3(size.x, size.y, 0.2f));
        CreateCube(name + "_WallBack", center + new Vector3(0, 0, size.z / 2), new Vector3(size.x, size.y, 0.2f));
        CreateCube(name + "_WallLeft", center + new Vector3(-size.x / 2, 0, 0), new Vector3(0.2f, size.y, size.z));
        CreateCube(name + "_WallRight", center + new Vector3(size.x / 2, 0, 0), new Vector3(0.2f, size.y, size.z));
        return room;
    }

    static void CreateSpotLight(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.intensity = 0.3f;
        l.range = 6f;
        l.spotAngle = 60f;
        l.color = new Color(0.63f, 0.71f, 1.0f);
    }
}
