using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
#if UNITY_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

public class BasementSceneBuilder
{
    [MenuItem("Tools/Build Basement Scene")]
    public static void BuildBasementScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // メイン通路（低い天井・圧迫感）
        CreateCube("B_Main_Floor",   new Vector3(0, 0, 0),    new Vector3(4, 0.2f, 30));
        CreateCube("B_Main_Ceiling", new Vector3(0, 2.4f, 0), new Vector3(4, 0.2f, 30));
        CreateCube("B_Main_WallL",   new Vector3(-2, 1.2f, 0), new Vector3(0.2f, 2.4f, 30));
        CreateCube("B_Main_WallR",   new Vector3(2, 1.2f, 0),  new Vector3(0.2f, 2.4f, 30));
        CreateCube("B_Main_WallEnd", new Vector3(0, 1.2f, 14.9f), new Vector3(4, 2.4f, 0.2f));

        // 左通路（棚・資料エリア）
        CreateCube("B_LeftCorridor_Floor",   new Vector3(-8, 0, -2),    new Vector3(12, 0.2f, 20));
        CreateCube("B_LeftCorridor_Ceiling", new Vector3(-8, 2.4f, -2), new Vector3(12, 0.2f, 20));
        CreateCube("B_LeftCorridor_WallL",   new Vector3(-13.9f, 1.2f, -2), new Vector3(0.2f, 2.4f, 20));
        CreateCube("B_LeftCorridor_WallF",   new Vector3(-8, 1.2f, -11.9f), new Vector3(12, 2.4f, 0.2f));
        CreateCube("B_LeftCorridor_WallB",   new Vector3(-8, 1.2f, 7.9f),  new Vector3(12, 2.4f, 0.2f));

        // 棚（記録保管用）- 各列3段
        float[] shelfXs = { -4.5f, -7f, -9.5f, -12f };
        float[] shelfZs = { -9f, -5f, -1f, 3f, 7f };
        int shelfIdx = 0;
        foreach (var sx in shelfXs)
        {
            foreach (var sz in shelfZs)
            {
                CreateCube("B_Shelf_" + shelfIdx, new Vector3(sx, 1.0f, sz), new Vector3(0.3f, 2.0f, 1.2f));
                // 書類箱
                CreateCube("B_Box_" + shelfIdx + "_A", new Vector3(sx, 0.3f, sz - 0.3f), new Vector3(0.25f, 0.25f, 0.4f));
                CreateCube("B_Box_" + shelfIdx + "_B", new Vector3(sx, 0.8f, sz + 0.2f), new Vector3(0.25f, 0.25f, 0.4f));
                CreateCube("B_Box_" + shelfIdx + "_C", new Vector3(sx, 1.3f, sz - 0.1f), new Vector3(0.25f, 0.25f, 0.4f));
                shelfIdx++;
            }
        }

        // 記録保管室（最終手がかりエリア）
        CreateRoom("B_ArchiveRoom", new Vector3(0, 1.2f, 12), new Vector3(6, 2.4f, 5));
        // 重要書類テーブル
        CreateCube("B_ArchiveTable", new Vector3(0, 0.45f, 12), new Vector3(3f, 0.7f, 1.5f));
        CreateCube("B_ArchiveTable_Leg1", new Vector3(-1.3f, 0.2f, 11.5f), new Vector3(0.1f, 0.4f, 0.1f));
        CreateCube("B_ArchiveTable_Leg2", new Vector3(1.3f, 0.2f, 11.5f),  new Vector3(0.1f, 0.4f, 0.1f));
        // カルテファイル（重要オブジェクト）
        CreateCube("B_ImportantFile_1", new Vector3(-0.5f, 0.82f, 11.8f), new Vector3(0.3f, 0.04f, 0.2f));
        CreateCube("B_ImportantFile_2", new Vector3(0.3f, 0.82f, 12.1f),  new Vector3(0.3f, 0.04f, 0.2f));
        // キャビネット（鍵付き）
        CreateCube("B_LockedCabinet", new Vector3(-2.3f, 1.0f, 14.3f), new Vector3(0.8f, 1.8f, 0.6f));

        // 機械室（謎のパイプ・ポンプ音）
        CreateRoom("B_MachineRoom", new Vector3(6, 1.2f, -8), new Vector3(6, 2.4f, 8));
        // ボイラー
        CreateCube("B_Boiler", new Vector3(7, 0.7f, -9), new Vector3(1.5f, 1.2f, 1.5f));
        // パイプ群
        CreateCube("B_Pipe_1", new Vector3(5, 1.8f, -8), new Vector3(0.15f, 0.15f, 6f));
        CreateCube("B_Pipe_2", new Vector3(5.5f, 2f, -6), new Vector3(0.15f, 0.4f, 0.15f));
        CreateCube("B_Pipe_3", new Vector3(7.5f, 1.9f, -8), new Vector3(0.15f, 0.15f, 5f));

        // 水没通路（進入不可エリア・見えるだけ）
        CreateCube("B_FloodedArea_Floor", new Vector3(-8, -0.5f, -12), new Vector3(12, 0.2f, 5));
        CreateCube("B_FloodedArea_WallF", new Vector3(-8, 1.2f, -14.4f), new Vector3(12, 3f, 0.2f));

        // 階段（3Fから降りてくる）
        for (int s = 0; s < 6; s++)
        {
            CreateCube("B_StairEntry_" + s, new Vector3(-1, -s * 0.22f, -14f + s * 0.4f), new Vector3(3, 0.2f, 0.4f));
        }

        // 照明（最小限・恐怖演出）
        // 非常灯のみ（赤）
        float[] redLightZs = { -10f, -4f, 2f, 8f };
        foreach (var rz in redLightZs)
        {
            var rl = new GameObject("B_EmergencyLight_" + rz);
            var l  = rl.AddComponent<Light>();
            l.type      = LightType.Point;
            l.intensity = 0.3f;
            l.range     = 5f;
            l.color     = new Color(0.8f, 0.1f, 0.05f);
            rl.transform.position = new Vector3(0, 2.1f, rz);
        }

        // 棚エリアの薄暗い照明
        var shelfLight = new GameObject("B_ShelfLight");
        var sl = shelfLight.AddComponent<Light>();
        sl.type      = LightType.Point;
        sl.intensity = 0.15f;
        sl.range     = 10f;
        sl.color     = new Color(0.45f, 0.45f, 0.6f);
        shelfLight.transform.position = new Vector3(-8, 2f, -2);

        // 記録室の照明（重要エリア）
        CreateSpotLight("B_ArchiveSpot", new Vector3(0, 2.2f, 12f));

        // DirectionalLight（ほぼなし）
        var dirGo = new GameObject("B_AmbientLight");
        var dir   = dirGo.AddComponent<Light>();
        dir.type      = LightType.Directional;
        dir.intensity = 0.005f;
        dir.color     = new Color(0.2f, 0.2f, 0.3f);
        dirGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        // PostProcessing（最も暗い・ノイズ強）
#if UNITY_URP
        var volumeGo = new GameObject("B_GlobalVolume");
        var volume   = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value       = 0.75f;
        vignette.intensity.overrideState = true;
        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.value    = -2.5f;
        colorAdj.postExposure.overrideState = true;
        colorAdj.colorFilter.value     = new Color(0.50f, 0.55f, 0.85f);
        colorAdj.colorFilter.overrideState = true;
        var filmGrain = profile.Add<FilmGrain>(true);
        filmGrain.intensity.value       = 0.4f;
        filmGrain.intensity.overrideState = true;
        var lensDistort = profile.Add<LensDistortion>(true);
        lensDistort.intensity.value       = -0.15f;
        lensDistort.intensity.overrideState = true;
        volume.profile = profile;
        AssetDatabase.CreateAsset(profile, "Assets/Scenes/HospitalBasement_VolumeProfile.asset");
#endif

        // Player（階段降りてすぐ）
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1f, -12f);
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

        // Enemy（地下：最速・視野最広・追い詰め演出）
        var enemy = new GameObject("Enemy_Basement");
        enemy.transform.position = new Vector3(0, 1f, 0f);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 2.8f;
        enemy.AddComponent<EnemyController>();
        var waypoints = new Transform[4];
        float[] wpZs = { -10f, -3f, 5f, 13f };
        for (int i = 0; i < 4; i++)
        {
            var wp = new GameObject("B_Waypoint_" + (i + 1));
            wp.transform.position = new Vector3(0, 0.5f, wpZs[i]);
            waypoints[i] = wp.transform;
        }
        var wpField = typeof(EnemyController).GetField("waypoints",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (wpField != null) wpField.SetValue(enemy.GetComponent<EnemyController>(), waypoints);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/HospitalBasement.unity");
        AssetDatabase.Refresh();
        Debug.Log("BasementSceneBuilder: HospitalBasement.unity saved successfully!");
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
        l.type      = LightType.Spot;
        l.intensity = 0.4f;
        l.range     = 5f;
        l.spotAngle = 50f;
        l.color     = new Color(0.75f, 0.75f, 0.65f);
    }
}
