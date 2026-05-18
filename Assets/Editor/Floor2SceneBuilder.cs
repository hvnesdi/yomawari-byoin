using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
#if UNITY_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class Floor2SceneBuilder
{
    [MenuItem("Tools/Build 2F Scene")]
    public static void Build2FScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 主廊下（南北）
        CreateCube("2F_Corridor_Floor",   new Vector3(0, 0, 0),    new Vector3(4, 0.2f, 40));
        CreateCube("2F_Corridor_Ceiling", new Vector3(0, 3f, 0),   new Vector3(4, 0.2f, 40));
        CreateCube("2F_Corridor_WallL",   new Vector3(-2, 1.5f, 0), new Vector3(0.2f, 3, 40));
        CreateCube("2F_Corridor_WallR",   new Vector3(2, 1.5f, 0),  new Vector3(0.2f, 3, 40));

        // 横廊下（処置室側）
        CreateCube("2F_SideCorridor_Floor",   new Vector3(6, 0, 8),    new Vector3(16, 0.2f, 4));
        CreateCube("2F_SideCorridor_Ceiling", new Vector3(6, 3f, 8),   new Vector3(16, 0.2f, 4));
        CreateCube("2F_SideCorridor_WallF",   new Vector3(6, 1.5f, 6), new Vector3(16, 3, 0.2f));
        CreateCube("2F_SideCorridor_WallB",   new Vector3(6, 1.5f, 10), new Vector3(16, 3, 0.2f));

        // 病室6部屋（左側）
        for (int i = 0; i < 6; i++)
        {
            float z = -15f + i * 6f;
            CreateRoom("2F_PatientRoom_" + (i + 1), new Vector3(-7, 1.5f, z), new Vector3(5, 3, 5));
            CreateCube("2F_Bed_" + (i + 1), new Vector3(-7, 0.35f, z), new Vector3(1.5f, 0.5f, 2f));
            // 鏡（各病室）
            var mirror = CreateCube("2F_Mirror_" + (i + 1), new Vector3(-9.3f, 1.5f, z), new Vector3(0.05f, 1.5f, 0.8f));
        }

        // 処置室（右側）
        CreateRoom("2F_TreatmentRoom", new Vector3(8, 1.5f, 8), new Vector3(6, 3, 7));
        // 処置台
        CreateCube("2F_TreatmentTable", new Vector3(8, 0.5f, 8), new Vector3(2f, 0.8f, 0.8f));
        // キャビネット
        CreateCube("2F_Cabinet_1", new Vector3(5.5f, 1f, 6.5f), new Vector3(0.5f, 1.8f, 0.5f));
        CreateCube("2F_Cabinet_2", new Vector3(5.5f, 1f, 7.5f), new Vector3(0.5f, 1.8f, 0.5f));

        // ナースステーション
        CreateRoom("2F_NurseStation", new Vector3(7, 1.5f, -5), new Vector3(6, 3, 6));
        CreateCube("2F_NurseDesk", new Vector3(7, 0.5f, -5), new Vector3(4f, 0.9f, 1.5f));

        // 洗面所（鏡あり・幻覚イベント用）
        CreateRoom("2F_Washroom", new Vector3(7, 1.5f, 15), new Vector3(4, 3, 4));
        CreateCube("2F_MainMirror", new Vector3(5.2f, 1.5f, 15), new Vector3(0.05f, 1.6f, 1.2f));
        CreateCube("2F_Sink", new Vector3(7, 0.5f, 13.3f), new Vector3(0.8f, 0.5f, 0.5f));

        // 階段（1F→2F、2F→3F）
        for (int s = 0; s < 5; s++)
        {
            CreateCube("2F_StairUp_" + s, new Vector3(-1, 0.1f + s * 0.2f, -18f + s * 0.5f), new Vector3(3, 0.2f, 0.5f));
            CreateCube("2F_StairDown_" + s, new Vector3(-1, 0.1f + s * 0.2f, 18f + s * 0.5f), new Vector3(3, 0.2f, 0.5f));
        }

        // 照明（廊下）
        float[] corridorLightZs = { -15f, -8f, -1f, 6f, 13f };
        Color flickerBlue = new Color(0.55f, 0.65f, 1.0f);
        foreach (var lz in corridorLightZs)
        {
            var pl = new GameObject("2F_PointLight_Z" + lz);
            var l = pl.AddComponent<Light>();
            l.type = LightType.Point;
            l.intensity = 0.35f;
            l.range = 7f;
            l.color = flickerBlue;
            pl.transform.position = new Vector3(0, 2.8f, lz);
        }

        // 処置室・ナースステーション照明
        CreateSpotLight("2F_Spot_Treatment", new Vector3(8, 2.8f, 8));
        CreateSpotLight("2F_Spot_Nurse", new Vector3(7, 2.8f, -5));
        CreateSpotLight("2F_Spot_Wash", new Vector3(7, 2.8f, 15));

        // DirectionalLight（暗め）
        var dirLightGo = new GameObject("2F_DirectionalLight");
        var dirLight = dirLightGo.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.intensity = 0.02f;
        dirLight.color = new Color(0.5f, 0.55f, 0.7f);
        dirLightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        // PostProcessing
#if UNITY_URP
        var volumeGo = new GameObject("2F_GlobalVolume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.55f;
        vignette.intensity.overrideState = true;
        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.value = -1.5f;
        colorAdj.postExposure.overrideState = true;
        colorAdj.colorFilter.value = new Color(0.70f, 0.80f, 1.0f);
        colorAdj.colorFilter.overrideState = true;
        volume.profile = profile;
        AssetDatabase.CreateAsset(profile, "Assets/Scenes/Hospital2F_VolumeProfile.asset");
#endif

        // Player（中央廊下スタート）
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1f, -14f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.3f;
        player.AddComponent<PlayerController>();
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0, 0.8f, 0);
        camGo.AddComponent<Camera>();
        var camCtrl = camGo.AddComponent<CameraController>();
        var bodyField = typeof(CameraController).GetField("playerBody",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (bodyField != null) bodyField.SetValue(camCtrl, player.transform);

        // Enemy（廊下巡回）
        var enemy = new GameObject("Enemy_2F");
        enemy.transform.position = new Vector3(0, 1f, 0f);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 1.8f;
        enemy.AddComponent<EnemyController>();
        var waypoints = new Transform[4];
        float[] wpZs = { -14f, -5f, 5f, 14f };
        for (int i = 0; i < 4; i++)
        {
            var wp = new GameObject("2F_Waypoint_" + (i + 1));
            wp.transform.position = new Vector3(0, 0.5f, wpZs[i]);
            waypoints[i] = wp.transform;
        }
        var wpField = typeof(EnemyController).GetField("waypoints",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (wpField != null) wpField.SetValue(enemy.GetComponent<EnemyController>(), waypoints);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Hospital2F.unity");
        AssetDatabase.Refresh();
        Debug.Log("Floor2SceneBuilder: Hospital2F.unity saved successfully!");
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
        CreateCube(name + "_Floor",   center - new Vector3(0, size.y / 2 - 0.1f, 0), new Vector3(size.x, 0.2f, size.z));
        CreateCube(name + "_Ceiling", center + new Vector3(0, size.y / 2 - 0.1f, 0), new Vector3(size.x, 0.2f, size.z));
        CreateCube(name + "_WallF",   center + new Vector3(0, 0, -size.z / 2),       new Vector3(size.x, size.y, 0.2f));
        CreateCube(name + "_WallB",   center + new Vector3(0, 0,  size.z / 2),       new Vector3(size.x, size.y, 0.2f));
        CreateCube(name + "_WallL",   center + new Vector3(-size.x / 2, 0, 0),       new Vector3(0.2f, size.y, size.z));
        CreateCube(name + "_WallR",   center + new Vector3( size.x / 2, 0, 0),       new Vector3(0.2f, size.y, size.z));
        return room;
    }

    static void CreateSpotLight(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.intensity = 0.25f;
        l.range = 5f;
        l.spotAngle = 60f;
        l.color = new Color(0.55f, 0.65f, 1.0f);
    }
}
