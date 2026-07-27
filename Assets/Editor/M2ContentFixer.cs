using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// M2: 「最後まで遊べる」ために足りていないコンテンツを埋める。
///
/// 覚醒エンド（唯一のハッピーエンド）の条件は
///   全手がかり収集 ＋ 残り30分以上 ＋ 鏡直視 ＋ NPCの言葉を聞いた
/// で、collectedAllClues は readMedicalRecord / checkedOwnRoom / facedMirror / listenedToNPC
/// の4つ全てを要求する（ClueInteractable.CheckAllClues）。
///
/// 元の状態:
///   - 手がかりは 3F の鏡と地下のカルテだけ（OwnRoom が無い）
///   - NPC がどのシーンにも1体も居ない → listenedToNPC が永久に立たない
///   - EscapeAttemptTrigger は実装済みだが未配置 → triedToEscape が立たない
///   - followedHallucination を立てるコードが存在しない
/// → 6エンド中4つが到達不能だった。
///
/// 配置座標は NavMesh 上の「歩ける床」にスナップさせる。
/// 単純な「プレイヤー前方 N m」だと壁の中に埋まるため。
/// </summary>
public static class M2ContentFixer
{
    [MenuItem("消灯/M2: 不足コンテンツを配置")]
    public static void RunBatch()
    {
        AddOwnRoomClue();
        AddNurseNPC();
        AddEscapeTrigger();
        AddHallucinationClue();
        AssetDatabase.SaveAssets();
        Debug.Log("[M2ContentFixer] 完了");
    }

    // ------------------------------------------------------------------
    // 共通ヘルパー
    // ------------------------------------------------------------------

    /// <summary>
    /// origin から dir 方向に distance だけ離れた「歩ける床の上」の点を返す。
    /// 壁の向こう側に回り込んでしまう点は NavMesh.Raycast で除外する。
    /// 見つからない場合は距離を詰めながら再試行する。
    /// </summary>
    static bool TryWalkablePoint(Vector3 origin, Vector3 dir, float distance, out Vector3 result)
    {
        result = origin;

        if (!NavMesh.SamplePosition(origin, out var start, 5f, NavMesh.AllAreas))
            return false;

        for (float d = distance; d >= 2f; d -= 1.5f)
        {
            var candidate = start.position + dir * d;
            if (!NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas)) continue;
            if (NavMesh.Raycast(start.position, hit.position, out _, NavMesh.AllAreas)) continue;

            result = hit.position;
            return true;
        }
        return false;
    }

    static void Save(UnityEngine.SceneManagement.Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------------
    // 3F: 自分の病室に「私物を調べる」手がかり（checkedOwnRoom）
    // ------------------------------------------------------------------
    static void AddOwnRoomClue()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital3F.unity", OpenSceneMode.Single);

        foreach (var existing in Object.FindObjectsByType<ClueInteractable>(FindObjectsSortMode.None))
        {
            if (existing.clueType == ClueType.OwnRoom)
            {
                Debug.Log("[M2ContentFixer] 3F: OwnRoom 手がかりは既に存在");
                return;
            }
        }

        GameObject host = null;
        foreach (var name in new[] { "PlayerOwnRoom_Desk", "PlayerOwnRoom_NamePlate", "PlayerOwnRoom_Calendar", "PlayerOwnRoom" })
        {
            host = GameObject.Find(name);
            if (host != null) break;
        }

        if (host == null)
        {
            Debug.LogError("[M2ContentFixer] 3F: 自分の病室のオブジェクトが見つからない（PlayerOwnRoom_*）");
            return;
        }

        var clue = host.AddComponent<ClueInteractable>();
        clue.clueType = ClueType.OwnRoom;
        clue.interactRange = 2f;
        clue.promptText = "E: 私物を調べる";

        Save(scene);
        Debug.Log($"[M2ContentFixer] 3F: {host.name} に OwnRoom 手がかりを追加");
    }

    // ------------------------------------------------------------------
    // 1F: 会話できる NPC（listenedToNPC）
    // ------------------------------------------------------------------
    static void AddNurseNPC()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);

        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null)
        {
            Debug.LogError("[M2ContentFixer] 1F: PlayerController が見つからない");
            return;
        }

        if (!TryWalkablePoint(pc.transform.position, pc.transform.forward, 8f, out var npcPos))
        {
            Debug.LogError("[M2ContentFixer] 1F: NPC を置ける床が見つからない");
            return;
        }

        var manager = Object.FindFirstObjectByType<NPCManager>();
        bool isNew = manager == null;

        GameObject npc;
        if (isNew)
        {
            npc = new GameObject("NPC_Nurse");
        }
        else
        {
            npc = manager.gameObject;
            Debug.Log("[M2ContentFixer] 1F: 既存の NPC を再配置します");
        }
        npc.transform.position = npcPos;

        Renderer npcRenderer = npc.GetComponentInChildren<Renderer>();
        if (npcRenderer == null)
        {
            var previewSource = GameObject.Find("NPC_Nurse_Preview");
            if (previewSource != null)
            {
                var visual = Object.Instantiate(previewSource, npc.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                npcRenderer = visual.GetComponentInChildren<Renderer>();
            }
            else
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Visual";
                capsule.transform.SetParent(npc.transform, false);
                Object.DestroyImmediate(capsule.GetComponent<Collider>());
                npcRenderer = capsule.GetComponent<Renderer>();
                Debug.LogWarning("[M2ContentFixer] NPC_Nurse_Preview が無いためカプセルで代用");
            }
        }

        // 巡回路: NPC から前方に少し伸ばした2点（床の上にスナップ）
        var wpA = GameObject.Find("NPC_Nurse_WP_A") ?? new GameObject("NPC_Nurse_WP_A");
        var wpB = GameObject.Find("NPC_Nurse_WP_B") ?? new GameObject("NPC_Nurse_WP_B");
        wpA.transform.position = npcPos;
        wpB.transform.position = TryWalkablePoint(npcPos, pc.transform.forward, 6f, out var far)
            ? far : npcPos;

        var agent = npc.GetComponent<NavMeshAgent>() ?? npc.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 1.8f;

        if (manager == null) manager = npc.AddComponent<NPCManager>();
        manager.waypoints = new[] { wpA.transform, wpB.transform };
        manager.walkSpeed = 1.2f;
        manager.talkRange = 2.5f;
        manager.npcRenderer = npcRenderer;
        if (npcRenderer != null && manager.normalMat == null)
            manager.normalMat = npcRenderer.sharedMaterial;
        manager.dialogueLines = new[]
        {
            "看護師「まだ起きていたんですか。もう消灯の時間ですよ」",
            "看護師「お部屋は3階です。ひとりで戻れますか」",
            "看護師「先生には、私から伝えておきますから」",
        };
        EditorUtility.SetDirty(manager);

        Save(scene);
        Debug.Log($"[M2ContentFixer] 1F: NPC_Nurse を {npcPos}（床上）に配置");
    }

    // ------------------------------------------------------------------
    // 1F: 「出口」に見せかけたトリガー（triedToEscape → 脱出エンド）
    // ------------------------------------------------------------------
    static void AddEscapeTrigger()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);

        var pc = Object.FindFirstObjectByType<PlayerController>();
        var surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (pc == null || surface == null || surface.navMeshData == null)
        {
            Debug.LogError("[M2ContentFixer] 1F: プレイヤーか NavMesh が無く脱出トリガーを配置できない");
            return;
        }

        // 階段（2Fへの遷移）と反対側の端を「玄関」とみなす
        var bounds = surface.navMeshData.sourceBounds;
        var stairs = Object.FindFirstObjectByType<SceneTransitionTrigger>();
        float stairZ = stairs != null ? stairs.transform.position.z : bounds.center.z;
        float exitZ = stairZ > bounds.center.z ? bounds.min.z + 1.5f : bounds.max.z - 1.5f;

        var wanted = new Vector3(pc.transform.position.x, pc.transform.position.y, exitZ);
        var pos = NavMesh.SamplePosition(wanted, out var hit, 6f, NavMesh.AllAreas) ? hit.position : wanted;

        var existing = Object.FindFirstObjectByType<EscapeAttemptTrigger>();
        GameObject go = existing != null ? existing.gameObject : new GameObject("EscapeAttemptTrigger_Entrance");
        go.transform.position = pos;

        var box = go.GetComponent<BoxCollider>() ?? go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(5f, 3f, 1.5f);

        var trigger = existing != null ? existing : go.AddComponent<EscapeAttemptTrigger>();
        var respawn = GameObject.Find("PlayerRespawnPoint");
        if (respawn != null) trigger.returnSpawnPoint = respawn.transform;
        else Debug.LogWarning("[M2ContentFixer] PlayerRespawnPoint が無いため帰還先が未設定");
        EditorUtility.SetDirty(trigger);

        Save(scene);
        Debug.Log($"[M2ContentFixer] 1F: 脱出トリガーを {pos}（床上）に配置" +
                  "（階段の反対側を玄関とみなした。目視での位置確認が必要）");
    }

    // ------------------------------------------------------------------
    // 地下: 幻覚（家族）に従う分岐点（followedHallucination → 救出エンド）
    // ------------------------------------------------------------------
    static void AddHallucinationClue()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/HospitalBasement.unity", OpenSceneMode.Single);

        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null)
        {
            Debug.LogError("[M2ContentFixer] 地下: PlayerController が見つからない");
            return;
        }

        if (!TryWalkablePoint(pc.transform.position, pc.transform.forward, 10f, out var pos))
        {
            Debug.LogError("[M2ContentFixer] 地下: 幻覚を置ける床が見つからない");
            return;
        }
        pos += Vector3.up * 1f;

        ClueInteractable clue = null;
        foreach (var existing in Object.FindObjectsByType<ClueInteractable>(FindObjectsSortMode.None))
            if (existing.clueType == ClueType.FollowHallucination) clue = existing;

        GameObject go = clue != null ? clue.gameObject : new GameObject("Clue_FamilyHallucination");
        go.transform.position = pos;

        // TODO(M3): 見た目が無いと発見できない。人影のモデルか演出に差し替える
        var light = go.GetComponent<Light>() ?? go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 6f;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.92f, 0.75f);

        if (clue == null) clue = go.AddComponent<ClueInteractable>();
        clue.clueType = ClueType.FollowHallucination;
        clue.interactRange = 2.5f;
        clue.promptText = "E: 近づく";
        EditorUtility.SetDirty(clue);

        Save(scene);
        Debug.Log($"[M2ContentFixer] 地下: 幻覚の分岐点を {pos}（床上）に配置" +
                  "（仮に点光源で表現。モデル差し替えが必要）");
    }
}
