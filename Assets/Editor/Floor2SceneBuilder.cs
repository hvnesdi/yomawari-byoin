using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Floor2SceneBuilder
{
    public static void BuildScene()
    {
        BuildFloor2Scene();
    }

    [MenuItem("Tools/Build Hospital 2F Scene")]
    public static void BuildFloor2Scene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCube("Corridor2F_Floor",   new Vector3(0,    0,    0), new Vector3(4, 0.2f, 48));
        CreateCube("Corridor2F_Ceiling", new Vector3(0,    3f,   0), new Vector3(4, 0.2f, 48));
        CreateCube("Corridor2F_WallL",   new Vector3(-2,   1.5f, 0), new Vector3(0.2f, 3, 48));
        CreateCube("Corridor2F_WallR",   new Vector3( 2,   1.5f, 0), new Vector3(0.2f, 3, 48));
        CreateCube("Corridor2F_WallS",   new Vector3(0, 1.5f, -24), new Vector3(4, 3, 0.2f));
        CreateCube("Corridor2F_WallN",   new Vector3(0, 1.5f,  24), new Vector3(4, 3, 0.2f));

        CreateRoom("NurseStation", new Vector3(8, 1.5f, 0), new Vector3(6, 3, 8));
        CreateCube("NurseStation_Counter", new Vector3(5.2f, 0.9f, 0), new Vector3(0.4f, 1.6f, 6));

        float[] roomZLeft  = { -20f, -13f, -6f, 1f, 8f, 15f };
        for (int i = 0; i < roomZLeft.Length; i++)
        {
            string rn = "PatientRoom2F_L" + (i + 1);
            CreateRoom(rn, new Vector3(-7, 1.5f, roomZLeft[i]), new Vector3(5, 3, 6));
            CreateCube(rn + "_Bed",    new Vector3(-7.5f, 0.35f, roomZLeft[i]),       new Vector3(1.5f, 0.5f, 2f));
            CreateCube(rn + "_Locker", new Vector3(-9f,   0.75f, roomZLeft[i] + 2f),  new Vector3(0.8f, 1.5f, 0.6f));
            CreateCube(rn + "_IVPole", new Vector3(-6.5f, 1.0f,  roomZLeft[i] - 0.8f), new Vector3(0.08f, 2.0f, 0.08f));
        }

        float[] roomZRight = { -20f, -13f, 9f, 16f };
        for (int i = 0; i < roomZRight.Length; i++)
        {
            string rn = "PatientRoom2F_R" + (i + 1);
            CreateRoom(rn, new Vector3(7, 1.5f, roomZRight[i]), new Vector3(5, 3, 6));
            CreateCube(rn + "_Bed",    new Vector3(7.5f,  0.35f, roomZRight[i]),      new Vector3(1.5f, 0.5f, 2f));
            CreateCube(rn + "_Locker", new Vector3(9f,    0.75f, roomZRight[i] + 2f), new Vector3(0.8f, 1.5f, 0.6f));
        }

        CreateRoom("TreatmentRoom_A", new Vector3(-7, 1.5f, 20), new Vector3(5, 3, 6));
        CreateCube("TreatmentRoom_A_Table",   new Vector3(-7, 0.45f, 20),    new Vector3(1.8f, 0.8f, 0.8f));
        CreateCube("TreatmentRoom_A_Cabinet", new Vector3(-9, 0.9f,  21.5f), new Vector3(0.4f, 1.8f, 2f));
        CreateRoom("TreatmentRoom_B", new Vector3(7, 1.5f, 20), new Vector3(5, 3, 6));
        CreateCube("TreatmentRoom_B_Table",   new Vector3(7, 0.45f, 20), new Vector3(1.8f, 0.8f, 0.8f));

        CreateCube("Stairs2F_Step1", new Vector3(0, 0.25f, 22),    new Vector3(3, 0.3f, 2));
        CreateCube("Stairs2F_Step2", new Vector3(0, 0.55f, 22.6f), new Vector3(3, 0.3f, 0.8f));

        Color blueWhite = new Color(0.63f, 0.71f, 1.0f);
        float[] lightZ = { -18f, -12f, -6f, 0f, 6f, 12f, 18f, 22f };
        for (int i = 0; i < lightZ.Length; i++)
        {
            var pl = new GameObject("CorrLight2F_" + i);
            var l  = pl.AddComponent<Light>();
            l.type = LightType.Point; l.intensity = 0.45f; l.range = 9f; l.color = blueWhite;
            pl.transform.position = new Vector3(0, 2.8f, lightZ[i]);
        }
        foreach (var z in roomZLeft)  CreateSpotLight("Spot_L_" + z, new Vector3(-7, 2.8f, z), 0.25f);
        foreach (var z in roomZRight) CreateSpotLight("Spot_R_" + z, new Vector3( 7, 2.8f, z), 0.25f);

        var dirGo = new GameObject("DirectionalLight");
        var dl = dirGo.AddComponent<Light>();
        dl.type = LightType.Directional; dl.intensity = 0.03f;
        dirGo.transform.rotation = Quaternion.Euler(50, -30, 0);

        var volGo = new GameObject("GlobalVolume");
        var vol   = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var vig  = prof.Add<Vignette>(true);
        vig.intensity.value = 0.5f; vig.intensity.overrideState = true;
        var ca   = prof.Add<ColorAdjustments>(true);
        ca.postExposure.value = -1.3f; ca.postExposure.overrideState = true;
        vol.profile = prof;
        AssetDatabase.CreateAsset(prof, "Assets/Scenes/Hospital2FVolumeProfile.asset");

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0, 1f, -22f);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.3f;
        player.AddComponent<PlayerController>();
        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(player.transform);
        camGo.transform.localPosition = new Vector3(0, 0.8f, 0);
        camGo.AddComponent<Camera>();
        var camCtrl = camGo.AddComponent<CameraController>();
        SetField(camCtrl, "playerBody", player.transform);

        var enemy = new GameObject("Enemy");
        enemy.transform.position = new Vector3(0, 1f, 0);
        var agent = enemy.AddComponent<NavMeshAgent>();
        agent.speed = 1.9f;
        var ec = enemy.AddComponent<EnemyController>();
        var wps = new Transform[4];
        float[] wpZ = { -18f, -6f, 6f, 18f };
        for (int i = 0; i < 4; i++)
        {
            var wp = new GameObject("Waypoint2F_" + (i + 1));
            wp.transform.position = new Vector3(0, 0.5f, wpZ[i]);
            wps[i] = wp.transform;
        }
        SetField(ec, "waypoints", wps);

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Hospital2F.unity");
        AssetDatabase.Refresh();
        Debug.Log("Floor2SceneBuilder: Hospital2F.unity saved successfully!");
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

    static void CreateSpotLight(string name, Vector3 pos, float intensity = 0.3f)
    {
        var go = new GameObject(name);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var l = go.AddComponent<Light>();
        l.type = LightType.Spot; l.intensity = intensity; l.range = 6f; l.spotAngle = 60f;
        l.color = new Color(0.63f, 0.71f, 1.0f);
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
