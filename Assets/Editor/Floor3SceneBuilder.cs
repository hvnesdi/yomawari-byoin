using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
#if UNITY_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class Floor3SceneBuilder
{
    [MenuItem("Tools/Build 3F Scene")]
    public static void Build3FScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 主廊下（狭め・閉塞感）
        CreateCube("3F_Corridor_Floor",   new Vector3(0, 0, 0),     new Vector3(3.5f, 0.2f, 36));
        CreateCube("3F_Corridor_Ceiling", new Vector3(0, 2.8f, 0),  new Vector3(3.5f, 0.2f, 36));
        CreateCube("3F_Corridor_WallL",   new Vector3(-1.75f, 1.4f, 0), new Vector3(0.2f, 2.8f, 36));
        CreateCube("3F_Corridor_WallR",   new Vector3(1.75f, 1.4f, 0),  new Vector3(0.2f, 2.8f, 36));

        // 隔離病室（左側）- 通常より小さい部屋・鉄扉イメージ
        string[] roomNames = { "A", "B", "C", "D", "E" };
        float[] roomZs     = { -14f, -8f, -2f, 4f, 10f };
        for (int i = 0; i < roomNames.Length; i++)
        {
            string rn = "3F_IsolationRoom_" + roomNames[i];
            CreateRoom(rn, new Vector3(-6.5f, 1.4f, roomZs[i]), new Vector3(4.5f, 2.8f, 5));
            // ベッド
            CreateCube(rn + "_Bed", new Vector3(-6.5f, 0.35f, roomZs[i]), new Vector3(1.2f, 0.5f, 2f));
            // 鉄格子（窓）- x位置が壁外
            CreateCube(rn + "_WindowBars", new Vector3(-8.55f, 1.5f, roomZs[i] + 1f), new Vector3(0.05f, 0.8f, 0.1f));
            CreateCube(rn + "_WindowBars2", new Vector3(-8.55f, 1.5f, roomZs[i] + 1.2f), new Vector3(0.05f, 0.8f, 0.1f));
        }

        // プレイヤー自身の病室（3F最奥・特別マーキング用）
        CreateRoom("3F_PlayerRoom", new Vector3(6.5f, 1.4f, 14f), new Vector3(4.5f, 2.8f, 5));
        CreateCube("3F_PlayerRoom_Bed", new Vector3(6.5f, 0.35f, 14f), new Vector3(1.2f, 0.5f, 2f));
        // 鏡（プレイヤーの病室・覚醒エンド重要）
        CreateCube("3F_PlayerRoom_Mirror", new Vector3(4.45f, 1.4f, 14f), new Vector3(0.05f, 1.4f, 0.9f));
        // カルテ台
        CreateCube("3F_PlayerRoom_Chart", new Vector3(8.3f, 0.7f, 14f), new Vector3(0.1f, 0.8f, 0.5f));
        // 窓（外の人影イベント用）
        CreateCube("3F_PlayerRoom_Window", new Vector3(8.55f, 1.5f, 14f), new Vector3(0.05f, 1.0f, 1.2f));

        // 監視室（右側・中間）
        CreateRoom("3F_ObservationRoom", new Vector3(6.5f, 1.4f, 0), new Vector3(5, 2.8f, 6));
        // 監視窓（ガラス代わり）
        CreateCube("3F_ObsWindow", new Vector3(4.6f, 1.4f, 0), new Vector3(0.05f, 1.2f, 2f));
        CreateCube("3F_ObsDesk", new Vector3(7, 0.5f, 0), new Vector3(2.5f, 0.9f, 1f));

        // 隔離用廊下突き当り（行き止まり感）
        CreateCube("3F_DeadEnd_Wall",    new Vector3(0, 1.4f, 17.9f), new Vector3(3.5f, 2.8f, 0.2f));
        CreateCube("3F_DeadEnd_Floor",   new Vector3(0, 0, 17.9f),    new Vector3(3.5f, 0.2f, 0.5f));
        CreateCube("3F_DeadEnd_Ceiling", new Vector3(0, 2.8f, 17.9f), new Vector3(3.5f, 0.2f, 0.5f));
        // 階段（2Fへ）
        for (int s = 0; s < 5; s++)
        {
            CreateCube("3F_StairDown_" + s, new Vector3(-1, 0.1f + s * 0.2f, -17f + s * 0.5f), new Vector3(2.5f, 0.2f, 0.5f));
        }

        // 廊下照明（点滅演出想定・intensityを意図的に低く）
        float[] lightZs = { -14f, -7f, 0f, 7f, 14f };
        Color warningAmber = new Color(1.0f, 0.55f, 0.2f);
        Color dimBlue      = new Color(0.40f, 0.50f, 0.90f);
        for (int i = 0; i < lightZs.Length; i++)
        {
            var pl = new GameObject("3F_CorridorLight_" + i);
            var l  = pl.AddComponent<Light>();
            l.type      = LightType.Point;
            l.intensity = (i % 2 == 0) ? 0.25f : 0.15f; // 交互に暗い
            l.range     = 5f;
            l.color     = (i % 3 == 0) ? warningAmber : dimBlue;
            pl.transform.position = new Vector3(0, 2.5f, lightZs[i]);
        }

        // 緊急照明（赤）
        var emergGo = new GameObject("3F_EmergencyLight");
        var emergL  = emergGo.AddComponent<Light>();
        emergL.type      = LightType.Point;
        emergL.intensity = 0.5f;
        emergL.range     = 8f;
        emergL.color     = new Color(0.9f, 0.15f, 0.1f);
        emergGo.transform.position = new Vector3(0, 2.5f, 14f);

        // DirectionalLight（ほぼ消灯）
        var dirGo = new GameObject("3F_DirectionalLight");
        var dir   = dirGo.AddComponent<Light>();
        dir.type      = LightType.Directional;
        dir.intensity = 0.01f;
        dir.color     = new Color(0.3f, 0.3f, 0.5f);
        dirGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        // PostProcessing（幻覚ピーク時の強いビネット）
#if UNITY_URP
        var volumeGo = new GameObject("3F_GlobalVolume");
        var volume   = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value  = 0.65f;
        vignette.intensity.overrideState = true;
        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.value = -2.0f;
        colorAdj.postExposure.overrideState = true;
        colorAdj.colorFilter.value = new Color(0.60f, 0.65f, 1.0f);
        colorAdj.colorFilter.overrideState = true;
        var filmGrain = profile.Add<FilmGrain>(true);
        filmGrain.intensity.value = 0.25f;
        filmGrain.intensity.overrideState = true;
        volume.profile = profile;
        AssetDatabase.CreateAsset(profile, "Assets/Scenes/Hospital3F_VolumeProfile.asset");
#endif

        // Player（階段から登ったところ）
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1f, -15f);
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

        // Enemy（3F：やや速い・幻覚連動で視野広）
        var enemy = new GameObject("Enemy_3F");
        enemy.transform.position = new Vector3(0, 1f, 5f);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 2.2f;
        enemy.AddComponent<EnemyController>();
        var waypoints = new Transform[3];
        float[] wpZs = { -12f, 2f, 14f };
        for (int i = 0; i < 3; i++)
        {
            var wp = new GameObject("3F_Waypoint_" + (i + 1));
            wp.transform.position = new Vector3(0, 0.5f, wpZs[i]);
            waypoints[i] = wp.transform;
        }
        var wpField = typeof(EnemyController).GetField("waypoints",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (wpField != null) wpField.SetValue(enemy.GetComponent<EnemyController>(), waypoints);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Hospital3F.unity");
        AssetDatabase.Refresh();
        Debug.Log("Floor3SceneBuilder: Hospital3F.unity saved successfully!");
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
}
