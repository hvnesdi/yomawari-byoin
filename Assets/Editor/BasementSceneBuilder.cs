using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BasementSceneBuilder
{
    public static void BuildScene()
    {
        BuildBasementScene();
    }

    [MenuItem("Tools/Build Hospital Basement Scene")]
    public static void BuildBasementScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 地下: 記録保管室エリア - 低天井・コンクリート的な空間
        // メイン廊下
        CreateCube("BaseCorridor_Floor",   new Vector3(0,    0,    0), new Vector3(4, 0.2f, 36));
        CreateCube("BaseCorridor_Ceiling", new Vector3(0,    2.6f, 0), new Vector3(4, 0.2f, 36));
        CreateCube("BaseCorridor_WallL",   new Vector3(-2,   1.3f, 0), new Vector3(0.2f, 2.6f, 36));
        CreateCube("BaseCorridor_WallR",   new Vector3( 2,   1.3f, 0), new Vector3(0.2f, 2.6f, 36));
        CreateCube("BaseCorridor_WallS",   new Vector3(0, 1.3f, -18), new Vector3(4, 2.6f, 0.2f));
        CreateCube("BaseCorridor_WallN",   new Vector3(0, 1.3f,  18), new Vector3(4, 2.6f, 0.2f));

        // メイン記録保管室（広め）
        CreateRoom("RecordRoom_Main", new Vector3(-10, 1.3f, 0), new Vector3(12, 2.6f, 16));
        // 棚（記録ファイル）
        for (int row = 0; row < 4; row++)
        {
            float rx = -14f + row * 1.8f;
            for (int col = 0; col < 3; col++)
            {
                float rz = -4f + col * 4f;
                CreateCube("Shelf_" + row + "_" + col, new Vector3(rx, 1.1f, rz), new Vector3(0.4f, 2.2f, 3.2f));
            }
        }
        // 閲覧テーブル
        CreateCube("RecordRoom_Table1", new Vector3(-8, 0.45f, 0), new Vector3(2f, 0.8f, 0.8f));
        CreateCube("RecordRoom_Table2", new Vector3(-8, 0.45f, 4), new Vector3(2f, 0.8f, 0.8f));

        // 院長の個人記録室（右側）
        CreateRoom("DirectorArchive", new Vector3(10, 1.3f, -6), new Vector3(8, 2.6f, 10));
        CreateCube("DirArch_Desk",    new Vector3(10, 0.45f, -6), new Vector3(2f, 0.8f, 1f));
        CreateCube("DirArch_Chair",   new Vector3(10, 0.4f, -4.5f), new Vector3(0.6f, 0.8f, 0.6f));
        CreateCube("DirArch_Cabinet", new Vector3(13.5f, 0.9f, -8), new Vector3(0.4f, 1.8f, 4f));
        // 証拠品（電灯で照らされた重要書類）
        CreateCube("Evidence_MedicalRecord", new Vector3(10, 0.85f, -6), new Vector3(0.25f, 0.02f, 0.18f));

        // 薬品保管室
        CreateRoom("MedStorage", new Vector3(10, 1.3f, 8), new Vector3(8, 2.6f, 8));
        // 棚
        CreateCube("MedShelf_1", new Vector3(13, 1.1f,  6), new Vector3(0.4f, 2f, 3f));
        CreateCube("MedShelf_2", new Vector3(13, 1.1f, 10), new Vector3(0.4f, 2f, 3f));
        CreateCube("MedShelf_3", new Vector3(10, 1.1f, 11), new Vector3(6f, 0.2f, 0.4f));
        CreateCube("MedShelf_4", new Vector3(10, 1.5f, 11), new Vector3(6f, 0.2f, 0.4f));

        // 隠し通路（フロア奥）
        CreateCube("HiddenPassage_Floor",   new Vector3(-18, 0, 0),    new Vector3(12, 0.2f, 8));
        CreateCube("HiddenPassage_Ceiling", new Vector3(-18, 2.2f, 0), new Vector3(12, 0.2f, 8));
        CreateCube("HiddenPassage_WallL",   new Vector3(-18, 1.1f, -4), new Vector3(12, 2.2f, 0.2f));
        CreateCube("HiddenPassage_WallR",   new Vector3(-18, 1.1f,  4), new Vector3(12, 2.2f, 0.2f));
        CreateCube("HiddenPassage_End",     new Vector3(-23, 1.1f,  0), new Vector3(0.2f, 2.2f, 8));

        // 非常用発電機
        CreateCube("Generator",   new Vector3(-20, 0.4f, 0), new Vector3(2f, 0.8f, 1f));
        // 配管
        CreateCube("Pipe_H1", new Vector3(-16, 2.3f, -3), new Vector3(8f, 0.15f, 0.15f));
        CreateCube("Pipe_H2", new Vector3(-16, 2.3f,  3), new Vector3(8f, 0.15f, 0.15f));
        CreateCube("Pipe_V1", new Vector3(-12, 1.5f,  -3), new Vector3(0.15f, 1.6f, 0.15f));

        // 階段（1Fへの接続）
        CreateCube("BaseStairs_Step1", new Vector3(0, 0.25f, 14),    new Vector3(3, 0.3f, 2));
        CreateCube("BaseStairs_Step2", new Vector3(0, 0.55f, 14.8f), new Vector3(3, 0.3f, 0.8f));
        CreateCube("BaseStairs_Step3", new Vector3(0, 0.85f, 15.5f), new Vector3(3, 0.3f, 0.8f));

        // 照明（非常灯風 - 赤みがかった緑）
        Color emergencyGreen = new Color(0.2f, 0.6f, 0.3f);
        float[] emergLightZ = { -12f, -4f, 4f, 12f };
        for (int i = 0; i < emergLightZ.Length; i++)
        {
            var pl = new GameObject("EmergLight_" + i);
            var l  = pl.AddComponent<Light>();
            l.type = LightType.Point; l.intensity = 0.2f; l.range = 6f; l.color = emergencyGreen;
            pl.transform.position = new Vector3(0, 2.4f, emergLightZ[i]);
        }

        // 記録室内照明（黄色い裸電球風）
        Color bulbYellow = new Color(0.8f, 0.7f, 0.3f);
        float[] archLightZ = { -6f, 0f, 6f };
        foreach (var z in archLightZ)
        {
            var pl = new GameObject("ArchLight_" + z);
            var l  = pl.AddComponent<Light>();
            l.type = LightType.Point; l.intensity = 0.4f; l.range = 7f; l.color = bulbYellow;
            pl.transform.position = new Vector3(-10, 2.4f, z);
        }

        // DirectionalLight（地下は完全に0 - 外光なし）
        var dirGo = new GameObject("DirectionalLight");
        var dl = dirGo.AddComponent<Light>();
        dl.type = LightType.Directional; dl.intensity = 0.0f;

        // グローバルボリューム（地下は最も暗く緑がかる）
        var volGo = new GameObject("GlobalVolume");
        var vol   = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var vig  = prof.Add<Vignette>(true);
        vig.intensity.value = 0.65f; vig.intensity.overrideState = true;
        var ca   = prof.Add<ColorAdjustments>(true);
        ca.postExposure.value = -2.0f; ca.postExposure.overrideState = true;
        var split = prof.Add<SplitToning>(true);
        split.shadows.value = new Color(0.02f, 0.06f, 0.02f); split.shadows.overrideState = true;
        vol.profile = prof;
        AssetDatabase.CreateAsset(prof, "Assets/Scenes/HospitalBasementVolumeProfile.asset");

        // プレイヤー（階段口スタート）
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1f, 12f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.3f;
        player.AddComponent<PlayerController>();
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0, 0.8f, 0);
        camGo.AddComponent<Camera>();
        var camCtrl = camGo.AddComponent<CameraController>();
        SetField(camCtrl, "playerBody", player.transform);

        // 敵（地下は遅めだが視野広め - 暗闇に潜む）
        var enemy = new GameObject("Enemy");
        enemy.transform.position = new Vector3(-10, 1f, 0);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 1.5f;
        var ec = enemy.AddComponent<EnemyController>();
        var wps = new Transform[4];
        float[] wpX = { -10f, 0f, 10f, 0f };
        float[] wpZ = { -8f,  0f, 8f, -8f };
        for (int i = 0; i < 4; i++)
        {
            var wp = new GameObject("WaypointBase_" + (i + 1));
            wp.transform.position = new Vector3(wpX[i], 0.5f, wpZ[i]);
            wps[i] = wp.transform;
        }
        SetField(ec, "waypoints", wps);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/HospitalBasement.unity");
        AssetDatabase.Refresh();
        Debug.Log("BasementSceneBuilder: HospitalBasement.unity saved successfully!");
    }

    static GameObject CreateCube(string name, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.position = pos; go.transform.localScale = scale;
        return go;
    }

    static GameObject CreateRoom(string name, Vector3 center, Vector3 size)
    {
        var room = new GameObject(name);
        room.transform.position = center;
        CreateCube(name + "_Floor",     center - new Vector3(0, size.y/2-0.1f, 0), new Vector3(size.x, 0.2f, size.z));
        CreateCube(name + "_Ceiling",   center + new Vector3(0, size.y/2-0.1f, 0), new Vector3(size.x, 0.2f, size.z));
        CreateCube(name + "_WallFront", center + new Vector3(0, 0, -size.z/2),      new Vector3(size.x, size.y, 0.2f));
        CreateCube(name + "_WallBack",  center + new Vector3(0, 0,  size.z/2),      new Vector3(size.x, size.y, 0.2f));
        CreateCube(name + "_WallLeft",  center + new Vector3(-size.x/2, 0, 0),      new Vector3(0.2f, size.y, size.z));
        CreateCube(name + "_WallRight", center + new Vector3( size.x/2, 0, 0),      new Vector3(0.2f, size.y, size.z));
        return room;
    }

    static void SetField(object obj, string fieldName, object value)
    {
        var f = obj.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(obj, value);
    }
}
