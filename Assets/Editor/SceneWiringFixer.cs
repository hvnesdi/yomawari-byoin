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

        // 足音。歩き・走り・しゃがみで音量と間隔が変わるので、
        // 隠れる遊びが音でも成立する
        if (player.GetComponent<FootstepPlayer>() == null)
        {
            player.AddComponent<FootstepPlayer>();
            changes++;
            Debug.Log("[SceneWiringFixer] FootstepPlayer を追加");
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
    /// 装飾を NavMesh のベイク対象から外す。
    ///
    /// `useGeometry = RenderMeshes` なので、置いた装飾の**描画メッシュが全部
    /// 障害物として焼かれていた**。巾木と回り縁は壁の全長に沿って床際に並ぶので、
    /// 歩ける範囲がその分だけ削られる。実測すると廊下のどこに立っても
    /// 一番近い縁まで 0.3m しかない状態になっていた。
    ///
    /// 影響は見た目の判定だけではない。敵は NavMeshAgent で動くので、
    /// 歩ける面が細切れになると追跡経路が切れる。
    /// 装飾は通行を妨げない前提で置いているので、ベイクからは外す。
    /// </summary>
    static void ExcludePropsFromNavMesh()
    {
        // M5/M6/M11 が作る入れ物。いずれも「そこにあるが通行の邪魔はしない」もの
        foreach (var rootName in new[] { "CorridorDetail", "ScannedProps", "Grime" })
        {
            var root = FindIncludingInactive(rootName);
            if (root == null) continue;

            var modifier = root.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = root.AddComponent<NavMeshModifier>();

            modifier.ignoreFromBuild = true;
            // 子孫にも効かせる。1つずつ付けると数千個になる
            modifier.applyToChildren = true;
            EditorUtility.SetDirty(modifier);
        }
    }

    /// <summary>
    /// 名前で探す。`GameObject.Find` は**非アクティブなものを見つけられない**。
    /// 病室（PatientRoom_1）がこれで見つからず、開始位置の探索範囲が
    /// 「現在位置の周辺」に落ちていた。
    /// </summary>
    static GameObject FindIncludingInactive(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        return null;
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

        ExcludePropsFromNavMesh();

        // 小さい歩行領域を捨てないようにする。
        //
        // 既定の 2 m² だと、**病室が丸ごと消えていた**。
        // 病室はベッド2台で床が細切れになるうえ、入口がエージェント半径で
        // 削られて廊下から切り離されるため、断片ごとの面積が閾値を割る。
        // 結果、1F の病室3室すべてで歩ける点が 0 だった（実測 0/429）。
        // 0.5 に下げると 99/429 まで戻る。
        //
        // 0 にはしない。ゼロだと数センチの破片まで残り、
        // 敵が乗れない島が増えるだけになる。
        surface.minRegionArea = 0.5f;

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
    /// プレイヤーの初期位置と向きを直す。
    ///
    /// 以前は向きだけを直していたが、それでは足りなかった。
    /// **プレイヤー自身が壁際の角に置かれているので、どちらを向いても壁が映る。**
    /// プレイテストの1枚目がのっぺりした壁とドアで埋まっていて、
    /// ゲームの最初の画がこれでは、他をどれだけ作り込んでも意味がない。
    ///
    /// CLAUDE.md の想定は「薄暗い病室で目が覚める」なので、病室があればその中へ、
    /// 無ければ現在位置の周辺で、**一番広く開けた場所**に置く。
    /// 判定にはベイク済み NavMesh を使う（壁にコライダーが無い箇所があるため
    /// Physics.Raycast では判定できない）。
    /// </summary>
    static int FixPlayerFacing()
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null) return 0;

        var origin = pc.transform.position;

        // 探す範囲は優先順に試す。病室を第一希望にするが、そこに歩ける床が
        // 無いこともある（1F の PatientRoom_1 の周辺には NavMesh が無かった）。
        // 第一希望が空振りしたら現在位置の周辺に落とす。
        // ここで諦めると「壁を向いたまま」に戻ってしまう。
        Vector3 bestPos = origin;
        float bestClearance = -1f;
        bool found = false;

        // 候補に点数を付けて、一番良いものを選ぶ。
        //
        // 最初は「体の周りが一番広い場所」を選んでいたが、それでは画にならなかった。
        // 起動直後の画を決めるのは**どこまで見通せるか**で、広さではない。
        // 廊下の奥まで抜ける画が強いのは見通しが長いからで、
        // 広い部屋の真ん中は、広くても壁しか映らない。
        //
        // 広さは「立てる程度」あれば十分なので上限で頭打ちにし、
        // 見通しの寄与を大きく取る。病室は設計上の開始地点なので下駄を履かせるが、
        // 狭すぎる隅なら廊下に負けるだけ。
        const float MinClearance = 0.7f;   // これ未満は隙間。人が立つ場所ではない

        float Score(float clearance, float sight, bool preferred) =>
            Mathf.Min(clearance, 1.5f) * 3f + Mathf.Min(sight, 15f) + (preferred ? 4f : 0f);

        float bestScore = float.MinValue;
        string bestLabel = "";
        float bestSight = 0f;

        foreach (var area in StartSearchAreas(origin))
        {
            // 広い範囲ほど細かく刻む。シーン全体を9分割では粗すぎて良い場所を逃す
            int steps = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Max(area.bounds.size.x, area.bounds.size.z) / 1.5f), 9, 31);

            int candidates = 0;
            for (int ix = 0; ix < steps; ix++)
            for (int iz = 0; iz < steps; iz++)
            {
                var candidate = new Vector3(
                    Mathf.Lerp(area.bounds.min.x, area.bounds.max.x, ix / (float)(steps - 1)),
                    origin.y,
                    Mathf.Lerp(area.bounds.min.z, area.bounds.max.z, iz / (float)(steps - 1)));

                if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas)) continue;

                // 窮屈さ = 8方向のうち最も近い縁までの距離。角に居ると小さくなる
                float clearance = Clearance(hit.position);
                if (clearance < MinClearance) continue;

                float sight = BestView(hit.position).distance;
                float score = Score(clearance, sight, area.preferred);
                candidates++;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPos = hit.position;
                    bestClearance = clearance;
                    bestSight = sight;
                    bestLabel = area.label;
                    found = true;
                }
            }

            Debug.Log($"[SceneWiringFixer] 「{area.label}」立てる候補 {candidates} 箇所");
        }

        if (!found)
        {
            Debug.LogWarning("[SceneWiringFixer] 置ける場所が見つからないため初期位置を変えません");
            return 0;
        }

        // 選んだ場所から一番遠くまで見通せる方向を向く
        var (bestDir, sightLine) = BestView(bestPos);

        // 足が床に埋まらないよう、元の高さの差分を保つ
        var newPos = new Vector3(bestPos.x, origin.y, bestPos.z);
        float moved = Vector3.Distance(origin, newPos);

        var newRotation = Quaternion.LookRotation(bestDir, Vector3.up);
        bool turn = Quaternion.Angle(pc.transform.rotation, newRotation) >= 5f;
        if (moved < 0.3f && !turn) return 0;

        pc.transform.position = newPos;
        pc.transform.rotation = newRotation;
        EditorUtility.SetDirty(pc.transform);

        // 捕捉されたときの転送先も一緒に動かす。置いていくと
        // 「捕まると壁の角に戻される」ことになる
        var respawn = FindIncludingInactive("PlayerRespawnPoint");
        if (respawn != null)
        {
            respawn.transform.position = newPos;
            respawn.transform.rotation = newRotation;
            EditorUtility.SetDirty(respawn.transform);
        }

        Debug.Log($"[SceneWiringFixer] 初期位置を {moved:F1}m 動かした " +
                  $"（「{bestLabel}」/ 周囲 {bestClearance:F1}m 空き / " +
                  $"視線 {sightLine:F1}m 先まで開けている / 点数 {bestScore:F1}）");
        return 1;
    }

    /// <summary>
    /// 開始位置を探す範囲を、優先順に並べて返す。
    ///
    /// 病室は名前で拾う（`PatientRoom_1` などが各シーンに置かれている）。
    /// **これらは子を持たない空のマーカーになっている。**
    /// 部屋の形状は後から `HospitalPackArchitecture` がパックのプレハブに
    /// 置き換えたため、別の名前の下に移っている。位置だけは残っている。
    /// （形状から寸法を取ろうとして「病室が見つからない」と誤って報告していた）
    ///
    /// なお 1F の PatientRoom_1 の周辺には NavMesh が無い。
    /// 病室が歩ける空間として成立していない可能性があり、要確認。
    /// </summary>
    static System.Collections.Generic.List<(Bounds bounds, string label, bool preferred)>
        StartSearchAreas(Vector3 fallbackCenter)
    {
        var areas = new System.Collections.Generic.List<(Bounds, string, bool)>();

        foreach (var name in new[] { "PatientRoom_1", "PatientRoom", "PatientRoom2F_1", "PatientRoom3F_1" })
        {
            var room = FindIncludingInactive(name);
            if (room == null) continue;

            var renderers = room.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                b.Expand(new Vector3(-1.2f, 0f, -1.2f));   // 壁の内側だけを候補にする
                areas.Add((b, $"{name} の形状 {b.size.x:F1}x{b.size.z:F1}m", true));
            }
            else
            {
                // 形状が無い（空のマーカー）。位置だけを使う
                areas.Add((new Bounds(room.transform.position, new Vector3(6f, 1f, 6f)),
                           $"{name} の位置 {room.transform.position} の周辺 6m 四方", true));
            }
            break;
        }

        // 最後の受け皿は**シーン全体の歩ける範囲**にする。現在位置の周辺にしてはいけない。
        //
        // 周辺にしていたとき、一度プレイヤーが狭い病室に移された後は
        // 探索範囲もその病室の周りになり、そこから抜け出せなくなった
        // （前回の実行結果が次回の入力になってしまう）。
        // シーン全体を見れば、何度走らせても同じ結論になる。
        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface != null && surface.navMeshData != null)
            areas.Add((surface.navMeshData.sourceBounds, "シーン全体の歩ける範囲", false));
        else
            areas.Add((new Bounds(fallbackCenter, new Vector3(10f, 1f, 10f)), "現在位置の周辺 10m 四方", false));

        return areas;
    }

    /// <summary>その場所の窮屈さ。8方向で最も近い壁までの距離を返す。</summary>
    static float Clearance(Vector3 pos)
    {
        const float Probe = 6f;
        float nearest = Probe;
        for (int i = 0; i < 8; i++)
        {
            var dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
            if (NavMesh.Raycast(pos, pos + dir * Probe, out var hit, NavMesh.AllAreas))
                nearest = Mathf.Min(nearest, Vector3.Distance(pos, hit.position));
        }
        return nearest;
    }

    /// <summary>一番遠くまで見通せる方向と、その距離。</summary>
    static (Vector3 dir, float distance) BestView(Vector3 pos)
    {
        const float Probe = 30f;
        Vector3 bestDir = Vector3.forward;
        float bestDistance = -1f;

        for (int i = 0; i < 24; i++)
        {
            var dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
            float distance = NavMesh.Raycast(pos, pos + dir * Probe, out var hit, NavMesh.AllAreas)
                ? Vector3.Distance(pos, hit.position)
                : Probe;

            if (distance > bestDistance) { bestDistance = distance; bestDir = dir; }
        }
        return (bestDir, bestDistance);
    }

    static int FixEnemy()
    {
        int changes = 0;

        // 捕捉されたときの転送先。CLAUDE.md では「最寄り病室」だが、
        // まずはプレイヤーの初期位置に戻す形で成立させる。
        // TODO(M2): フロアごとに病室単位の転送先を用意する
        var respawn = FindIncludingInactive("PlayerRespawnPoint");
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
