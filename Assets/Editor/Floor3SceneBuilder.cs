using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Floor3SceneBuilder
{
    public static void BuildScene()
    {
        BuildFloor3Scene();
    }

    [MenuItem("Tools/Build Hospital 3F Scene")]
    public static void BuildFloor3Scene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 3F: 隔離病棟 - 廊下は狭く天井低め
        CreateCube("Corridor3F_Floor",   new Vector3(0,    0,    0), new Vector3(3, 0.2f, 44));
        CreateCube("Corridor3F_Ceiling", new Vector3(0,    2.8f, 0), new Vector3(3, 0.2f, 44));
        CreateCube("Corridor3F_WallL",   new Vector3(-1.5f, 1.4f, 0), new Vector3(0.2f, 2.8f, 44));
        CreateCube("Corridor3F_WallR",   new Vector3( 1.5f, 1.4f, 0), new Vector3(0.2f, 2.8f, 44));
        CreateCube("Corridor3F_WallS",   new Vector3(0, 1.4f, -22), new Vector3(3, 2.8f, 0.2f));
        CreateCube("Corridor3F_WallN",   new Vector3(0, 1.4f,  22), new Vector3(3, 2.8f, 0.2f));

        // 隔離室（左側 6室）
        float[] isoZL = { -18f, -12f, -6f, 0f, 6f, 12f };
        for (int i = 0; i < isoZL.Length; i++)
        {
            string rn = "IsoRoom_L" + (i + 1);
            CreateRoom(rn, new Vector3(-7, 1.4f, isoZL[i]), new Vector3(5, 2.8f, 5));
            CreateCube(rn + "_Bed", new Vector3(-7.5f, 0.35f, isoZL[i]), new Vector3(1.2f, 0.4f, 1.8f));
            CreateCube(rn + "_BarH1", new Vector3(-9.9f, 1.2f, isoZL[i]),        new Vector3(0.05f, 0.08f, 0.9f));
            CreateCube(rn + "_BarH2", new Vector3(-9.9f, 1.5f, isoZL[i]),        new Vector3(0.05f, 0.08f, 0.9f));
            CreateCube(rn + "_BarV1", new Vector3(-9.9f, 1.35f, isoZL[i] - 0.2f), new Vector3(0.05f, 0.5f, 0.08f));
            CreateCube(rn + "_BarV2", new Vector3(-9.9f, 1.35f, isoZL[i] + 0.2f), new Vector3(0.05f, 0.5f, 0.08f));
        }

        // 隔離室（右側 5室）
        float[] isoZR = { -18f, -12f, -6f, 6f, 12f };
        for (int i = 0; i < isoZR.Length; i++)
        {
            string rn = "IsoRoom_R" + (i + 1);
            CreateRoom(rn, new Vector3(7, 1.4f, isoZR[i]), new Vector3(5, 2.8f, 5));
            CreateCube(rn + "_Bed",   new Vector3(7.5f, 0.35f, isoZR[i]), new Vector3(1.2f, 0.4f, 1.8f));
            CreateCube(rn + "_BarH1", new Vector3(9.9f, 1.2f, isoZR[i]), new Vector3(0.05f, 0.08f, 0.9f));
            CreateCube(rn + "_BarH2", new Vector3(9.9f, 1.5f, isoZR[i]), new Vector3(0.05f, 0.08f, 0.9f));
        }

        // プレイヤー自身の病室（右側 z=0 特別部屋 - 鏡あり）
        CreateRoom("PlayerOwnRoom", new Vector3(7, 1.4f, 0), new Vector3(5, 2.8f, 5));
        var ownRoomBed = CreateCube("PlayerOwnRoom_Bed", new Vector3(7.5f, 0.35f, 0), new Vector3(1.2f, 0.4f, 1.8f));
        var ownRoomClue = ownRoomBed.AddComponent<ClueInteractable>();
        ownRoomClue.clueType = ClueType.OwnRoom;
        ownRoomClue.promptText = "E: 調べる（自分の病室）";

        CreateCube("PlayerOwnRoom_Desk",     new Vector3(9.2f, 0.6f,  -1.5f), new Vector3(1f,   1.2f, 0.5f));
        CreateCube("PlayerOwnRoom_Calendar", new Vector3(9.8f, 1.8f,  0),     new Vector3(0.02f,0.4f, 0.3f));

        var mirrorGo = CreateCube("PlayerOwnRoom_Mirror", new Vector3(4.1f, 1.4f, 0), new Vector3(0.05f,1.4f, 0.7f));
        var mirrorClue = mirrorGo.AddComponent<ClueInteractable>();
        mirrorClue.clueType = ClueType.Mirror;
        mirrorClue.promptText = "E: 鏡を見る";

        CreateCube("PlayerOwnRoom_NamePlate",new Vector3(1.9f, 1.4f, 2.0f),  new Vector3(0.02f,0.15f,0.25f));

        // 観察室（廊下北端）
        CreateRoom("ObservationRoom", new Vector3(0, 1.4f, 18), new Vector3(8, 2.8f, 6));
        CreateCube("ObsRoom_Window",  new Vector3(0, 1.4f, 14.9f), new Vector3(4, 1.2f, 0.05f));
        CreateCube("ObsRoom_Table",   new Vector3(0, 0.5f, 18),    new Vector3(3, 0.8f, 0.8f));

        // 廊下照明（暗め・間隔広め）
        Color dimYellow = new Color(0.7f, 0.65f, 0.4f);
        float[] lightZ = { -16f, -8f, 0f, 8f, 16f };
        for (int i = 0; i < lightZ.Length; i++)
        {
            var pl = new GameObject("CorrLight3F_" + i);
            var l  = pl.AddComponent<Light>();
            l.type = LightType.Point; l.intensity = 0.35f; l.range = 8f; l.color = dimYellow;
            pl.transform.position = new Vector3(0, 2.6f, lightZ[i]);
        }

        // 隔離室内照明（暗紫）
        Color dimPurple = new Color(0.5f, 0.4f, 0.6f);
        foreach (var z in isoZL)
        {
            var pl = new GameObject("IsoLight_L_" + z);
            var l  = pl.AddComponent<Light>();
            l.type = LightType.Point; l.intensity = 0.15f; l.range = 4f; l.color = dimPurple;
            pl.transform.position = new Vector3(-7, 2.5f, z);
        }

        // DirectionalLight（極薄）
        var dirGo = new GameObject("DirectionalLight");
        var dl = dirGo.AddComponent<Light>();
        dl.type = LightType.Directional; dl.intensity = 0.02f;
        dirGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        // グローバルボリューム（3Fは最も暗く・紫がかる）
        var volGo = new GameObject("GlobalVolume");
        var vol   = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var vig  = prof.Add<Vignette>(true);
        vig.intensity.value = 0.6f; vig.intensity.overrideState = true;
        var ca   = prof.Add<ColorAdjustments>(true);
        ca.postExposure.value = -1.8f; ca.postExposure.overrideState = true;
        var split = prof.Add<SplitToning>(true);
        split.shadows.value = new Color(0.05f, 0.02f, 0.1f); split.shadows.overrideState = true;
        vol.profile = prof;
        AssetDatabase.CreateAsset(prof, "Assets/Scenes/Hospital3FVolumeProfile.asset");

        // プレイヤー（南側スタート）
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1f, -20f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.3f;
        player.AddComponent<PlayerController>();
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0, 0.8f, 0);
        camGo.AddComponent<Camera>();
        var camCtrl = camGo.AddComponent<CameraController>();
        SetField(camCtrl, "playerBody", player.transform);

        // 敵（3Fは速め）
        var enemy = new GameObject("Enemy");
        enemy.transform.position = new Vector3(0, 1f, 0);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 2.2f;
        var ec = enemy.AddComponent<EnemyController>();
        var wps = new Transform[4];
        float[] wpZ = { -16f, -4f, 4f, 16f };
        for (int i = 0; i < 4; i++)
        {
            var wp = new GameObject("Waypoint3F_" + (i + 1));
            wp.transform.position = new Vector3(0, 0.5f, wpZ[i]);
            wps[i] = wp.transform;
        }
        SetField(ec, "waypoints", wps);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Hospital3F.unity");
        AssetDatabase.Refresh();
        Debug.Log("Floor3SceneBuilder: Hospital3F.unity saved successfully!");
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
