using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// M1: 各病院シーンを「実際に遊べる」状態に直す。
///
/// 調査で判明していた欠落:
///   - "Player" タグが付いたオブジェクトが1つも無い
///     （EnemyController / ClueInteractable の FindGameObjectWithTag("Player") が null を返していた）
///   - AudioListener が無い（音が一切鳴らない）
///   - カメラの CameraController.playerBody が未結線
///   - 3F → 地下 のシーン遷移が存在しない
/// </summary>
public static class SceneWiringFixer
{
    static readonly string[] HospitalScenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M1: シーン結線を修復")]
    public static void RunBatch()
    {
        // 2F の階段トリガーの位置を、3F→地下 のトリガー配置に流用する
        // （各フロアは同系の Editor ビルダーで生成されており階段位置が揃っているため）
        Vector3 stairPos = Vector3.zero;
        Vector3 stairScale = Vector3.one;
        bool stairFound = false;

        EditorSceneManager.OpenScene("Assets/Scenes/Hospital2F.unity", OpenSceneMode.Single);
        var probe = Object.FindFirstObjectByType<SceneTransitionTrigger>();
        if (probe != null)
        {
            stairPos = probe.transform.position;
            stairScale = probe.transform.localScale;
            stairFound = true;
            Debug.Log($"[SceneWiringFixer] 2F の遷移トリガー位置を取得: {stairPos}");
        }
        else
        {
            Debug.LogWarning("[SceneWiringFixer] 2F に SceneTransitionTrigger が見つかりません");
        }

        foreach (var path in HospitalScenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int changes = 0;

            changes += FixPlayer(path);
            changes += FixAudioListener();

            if (path.EndsWith("Hospital3F.unity") && stairFound)
                changes += AddBasementTransition(stairPos, stairScale);

            changes += FixEnemy();
            changes += BakeNavMesh(path);
            changes += FixPlayerFacing();   // NavMesh が必要なのでベイク後に実行

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[SceneWiringFixer] {System.IO.Path.GetFileName(path)}: {changes} 件修正");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SceneWiringFixer] 完了");
    }

    // ------------------------------------------------------------------
    static int FixPlayer(string scenePath)
    {
        int changes = 0;

        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null)
        {
            Debug.LogError($"[SceneWiringFixer] {scenePath}: PlayerController が見つかりません");
            return 0;
        }
        var player = pc.gameObject;

        if (!player.CompareTag("Player"))
        {
            player.tag = "Player";
            changes++;
            Debug.Log($"[SceneWiringFixer] {player.name} に \"Player\" タグを設定");
        }

        if (player.GetComponent<PlayerSpawnOnLoad>() == null)
        {
            player.AddComponent<PlayerSpawnOnLoad>();
            changes++;
        }

        // 攻撃入力が無いと暴走エンドに到達できない
        if (player.GetComponent<PlayerAttack>() == null)
        {
            player.AddComponent<PlayerAttack>();
            changes++;
            Debug.Log("[SceneWiringFixer] PlayerAttack を追加（F キーで攻撃）");
        }

        // カメラ（プレイヤーの子）
        var cam = player.GetComponentInChildren<Camera>(true);
        if (cam == null)
        {
            var camGo = new GameObject("PlayerCamera", typeof(Camera));
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            cam = camGo.GetComponent<Camera>();
            changes++;
            Debug.Log("[SceneWiringFixer] PlayerCamera を新規作成");
        }
        if (!cam.CompareTag("MainCamera"))
        {
            cam.tag = "MainCamera";
            changes++;
        }

        var look = cam.GetComponent<CameraController>();
        if (look == null)
        {
            look = cam.gameObject.AddComponent<CameraController>();
            changes++;
        }
        if (look.playerBody != player.transform)
        {
            look.playerBody = player.transform;
            EditorUtility.SetDirty(look);
            changes++;
            Debug.Log("[SceneWiringFixer] CameraController.playerBody を結線");
        }

        // URP: 幻覚ポストプロセスを効かせるためカメラ側で有効化が必要
        var urp = cam.GetComponent<UniversalAdditionalCameraData>();
        if (urp == null) urp = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
        if (!urp.renderPostProcessing)
        {
            urp.renderPostProcessing = true;
            EditorUtility.SetDirty(urp);
            changes++;
            Debug.Log("[SceneWiringFixer] カメラのポストプロセスを有効化");
        }

        return changes;
    }

    static int FixAudioListener()
    {
        if (Object.FindFirstObjectByType<AudioListener>() != null) return 0;

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[SceneWiringFixer] MainCamera が無いため AudioListener を追加できません");
            return 0;
        }
        cam.gameObject.AddComponent<AudioListener>();
        Debug.Log("[SceneWiringFixer] AudioListener を追加");
        return 1;
    }

    static int AddBasementTransition(Vector3 pos, Vector3 scale)
    {
        // 既に地下への遷移があれば何もしない
        foreach (var existing in Object.FindObjectsByType<SceneTransitionTrigger>(FindObjectsSortMode.None))
            if (existing.targetScene == "HospitalBasement") return 0;

        var go = new GameObject("SceneTransitionTrigger_HospitalBasement");
        go.transform.position = pos;
        go.transform.localScale = scale;

        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;

        var trigger = go.AddComponent<SceneTransitionTrigger>();
        trigger.targetScene = "HospitalBasement";
        trigger.targetArea = AreaID.Basement;
        trigger.playerSpawnPosition = new Vector3(0f, 1f, 2f);
        trigger.transitionMessage = "地下へ降りている…";

        Debug.Log($"[SceneWiringFixer] 3F→地下 の遷移トリガーを {pos} に追加" +
                  "（位置は2Fの階段から流用。目視での位置確認が必要）");
        return 1;
    }

    /// <summary>
    /// NavMesh を焼く。どのシーンにも NavMeshSurface が無く NavMesh データも存在しないため、
    /// 敵の NavMeshAgent.SetDestination がこれまで一度も成功していなかった。
    /// </summary>
    static int BakeNavMesh(string scenePath)
    {
        const string NavMeshDir = "Assets/NavMeshes";
        if (!AssetDatabase.IsValidFolder(NavMeshDir))
            AssetDatabase.CreateFolder("Assets", "NavMeshes");

        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        int changes = 0;

        if (surface == null)
        {
            var go = new GameObject("NavMeshSurface");
            surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            changes++;
        }

        surface.BuildNavMesh();

        var data = surface.navMeshData;
        if (data == null)
        {
            Debug.LogError($"[SceneWiringFixer] {scenePath}: NavMesh のベイクに失敗（歩ける床が認識されていない）");
            return changes;
        }

        // BuildNavMesh() が作ったデータはメモリ上にしかないのでアセット化して永続化する
        var sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        var assetPath = $"{NavMeshDir}/{sceneName}_NavMesh.asset";
        if (!AssetDatabase.Contains(data))
        {
            AssetDatabase.CreateAsset(data, assetPath);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
        }
        EditorUtility.SetDirty(surface);

        Debug.Log($"[SceneWiringFixer] {sceneName}: NavMesh をベイク（範囲 {data.sourceBounds.size}）→ {assetPath}");
        return changes + 1;
    }

    /// <summary>
    /// プレイヤーの初期向きを「一番開けている方向」に直す。
    /// 全シーンで壁に向かってスポーンしており、起動直後の画面が壁だった。
    /// 判定にはベイク済み NavMesh を使う（壁にコライダーが無い箇所があるため
    /// Physics.Raycast では判定できない）。
    /// </summary>
    static int FixPlayerFacing()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null) return 0;

        if (!NavMesh.SamplePosition(pc.transform.position, out var onMesh, 5f, NavMesh.AllAreas))
        {
            Debug.LogWarning("[SceneWiringFixer] プレイヤーが NavMesh 上に居ないため向きを判定できません");
            return 0;
        }

        const float Probe = 30f;
        Vector3 bestDir = pc.transform.forward;
        float bestDistance = -1f;

        for (int i = 0; i < 12; i++)
        {
            var dir = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
            var target = onMesh.position + dir * Probe;

            float distance = NavMesh.Raycast(onMesh.position, target, out var hit, NavMesh.AllAreas)
                ? Vector3.Distance(onMesh.position, hit.position)
                : Probe;

            if (distance > bestDistance)
            {
                bestDistance = distance;
                bestDir = dir;
            }
        }

        if (bestDistance < 2f)
        {
            Debug.LogWarning($"[SceneWiringFixer] 開けている方向が見つかりません（最長 {bestDistance:F1}m）");
            return 0;
        }

        var newRotation = Quaternion.LookRotation(bestDir, Vector3.up);
        if (Quaternion.Angle(pc.transform.rotation, newRotation) < 5f) return 0;

        pc.transform.rotation = newRotation;
        EditorUtility.SetDirty(pc.transform);
        Debug.Log($"[SceneWiringFixer] プレイヤーの向きを修正（{bestDistance:F1}m 先まで開けている方向へ）");
        return 1;
    }

    static int FixEnemy()
    {
        int changes = 0;

        // 捕捉されたときの転送先。CLAUDE.md では「最寄り病室」だが、
        // まずはプレイヤーの初期位置に戻す形で成立させる。
        // TODO(M2): フロアごとに病室単位の転送先を用意する
        var respawn = GameObject.Find("PlayerRespawnPoint");
        if (respawn == null)
        {
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                respawn = new GameObject("PlayerRespawnPoint");
                respawn.transform.position = pc.transform.position;
                respawn.transform.rotation = pc.transform.rotation;
                changes++;
                Debug.Log($"[SceneWiringFixer] PlayerRespawnPoint を {respawn.transform.position} に作成");
            }
        }

        foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
        {
            if (enemy.GetComponent<NavMeshAgent>() == null)
            {
                enemy.gameObject.AddComponent<NavMeshAgent>();
                changes++;
                Debug.Log($"[SceneWiringFixer] {enemy.name} に NavMeshAgent を追加");
            }

            if (enemy.waypoints == null || enemy.waypoints.Length == 0)
                Debug.LogWarning($"[SceneWiringFixer] {enemy.name}: waypoints が未設定（巡回しません）");

            if (enemy.playerSpawnPoint == null && respawn != null)
            {
                enemy.playerSpawnPoint = respawn.transform;
                EditorUtility.SetDirty(enemy);
                changes++;
                Debug.Log($"[SceneWiringFixer] {enemy.name}.playerSpawnPoint を結線");
            }
        }
        return changes;
    }
}
