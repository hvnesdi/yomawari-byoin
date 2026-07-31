using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 病室まわりの実測用。
///
/// 設計（CLAUDE.md）では「薄暗い病室で目が覚める」ところから始まるはずだが、
/// 1F の `PatientRoom_1` マーカー (-6, 1.5, 5) の周辺には歩ける床が無く、
/// 開始位置に使えなかった。原因を推測で当てにいくと外すので、
/// 何がそこにあるのかを数えて出す。
///
/// 見るもの:
///   - マーカーの周りにどんなレンダラーがあるか（床はあるのか）
///   - その高さで NavMesh を引けるか。引けないなら何 m 離れた所なら引けるか
///   - NavMesh 全体の範囲に、そもそもその座標が入っているか
/// </summary>
public static class RoomDiagnostics
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/診断: 病室に床があるか調べる")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[RoomDiag] 病室の床とNavMesh\n");

        foreach (var path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);
            log.AppendLine($"── {label}");

            var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
            if (surface == null || surface.navMeshData == null)
            {
                log.AppendLine("   NavMesh が無い");
                continue;
            }
            var navBounds = surface.navMeshData.sourceBounds;
            log.AppendLine($"   NavMesh の範囲 center={navBounds.center} size={navBounds.size}");
            log.AppendLine($"   ベイク設定: layerMask={surface.layerMask.value} " +
                            $"collect={surface.collectObjects} geometry={surface.useGeometry} " +
                            $"agentRadius={NavMesh.GetSettingsByID(surface.agentTypeID).agentRadius} " +
                            $"minRegionArea={surface.minRegionArea}");

            var markers = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None)
                                .Where(t => t.name.StartsWith("PatientRoom"))
                                .OrderBy(t => t.name)
                                .ToList();

            if (markers.Count == 0) { log.AppendLine("   病室マーカーが無い"); continue; }

            foreach (var marker in markers)
            {
                var p = marker.position;
                log.AppendLine($"   {marker.name} @ {p}  子={marker.childCount}");
                log.AppendLine($"      NavMesh範囲に入っている: {navBounds.Contains(p)}");

                // 高さを変えながら NavMesh を探す。床の高さがマーカーとずれている可能性
                for (float dy = 2f; dy >= -3f; dy -= 1f)
                {
                    var probe = new Vector3(p.x, p.y + dy, p.z);
                    bool hit = NavMesh.SamplePosition(probe, out var nh, 2f, NavMesh.AllAreas);
                    if (hit)
                    {
                        log.AppendLine($"      y{dy:+0;-0}m から探すと {nh.position} に着地" +
                                        $"（距離 {Vector3.Distance(probe, nh.position):F2}m）");
                        break;
                    }
                }

                // 半径を広げて、どこまで離れれば歩ける床があるか
                float found = -1f;
                foreach (var r in new[] { 2f, 4f, 8f, 16f, 32f })
                    if (NavMesh.SamplePosition(p, out _, r, NavMesh.AllAreas)) { found = r; break; }
                log.AppendLine(found > 0
                    ? $"      半径 {found}m まで広げれば歩ける床がある"
                    : "      半径 32m まで探しても歩ける床が無い");

                // 周りに何があるのか。床らしきもの（水平で薄い）を数える
                var near = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                                  FindObjectsSortMode.None)
                                 .Where(m => Vector3.Distance(m.bounds.center, p) < 6f)
                                 .ToList();
                var floors = near.Where(m => m.bounds.size.y < 0.6f &&
                                             m.bounds.size.x > 1f && m.bounds.size.z > 1f)
                                 .OrderBy(m => Mathf.Abs(m.bounds.center.y - p.y))
                                 .ToList();

                log.AppendLine($"      半径6m内のレンダラー {near.Count} 個 / うち床らしきもの {floors.Count} 個");
                foreach (var f in floors.Take(4))
                    log.AppendLine($"        {f.name} y={f.bounds.center.y:F2} " +
                                    $"上面={f.bounds.max.y:F2} 大きさ={f.bounds.size.x:F1}x{f.bounds.size.z:F1} " +
                                    $"静的={f.gameObject.isStatic}");
                if (floors.Count == 0)
                    foreach (var m in near.OrderBy(m => Vector3.Distance(m.bounds.center, p)).Take(5))
                        log.AppendLine($"        (床以外) {m.name} 大きさ={m.bounds.size}");

                // 地面の高さに床があるのか。ベッドを床と数えてしまっていたので、
                // 「水平・薄い・y がほぼ 0」に絞って探す
                var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None);
                var ground = all.Where(m => m.bounds.max.y < 0.5f && m.bounds.size.y < 0.6f &&
                                            m.bounds.size.x > 0.8f && m.bounds.size.z > 0.8f)
                                .Select(m => (m, d: Vector2.Distance(
                                     new Vector2(m.bounds.center.x, m.bounds.center.z),
                                     new Vector2(p.x, p.z))))
                                .OrderBy(t => t.d).Take(3).ToList();
                if (ground.Count == 0) log.AppendLine("      地面の高さに床が1枚も無い");
                foreach (var (m, d) in ground)
                {
                    // 床の真上で NavMesh を引けるか。引けないなら、床はあるのに
                    // ベイク対象から外れているということ。層・静的フラグ・
                    // NavMeshModifier のどれかが効いている可能性がある
                    var above = m.bounds.center + Vector3.up * 0.2f;
                    bool navHere = NavMesh.SamplePosition(above, out _, 0.6f, NavMesh.AllAreas);
                    var modifier = m.GetComponentInParent<Unity.AI.Navigation.NavMeshModifier>();

                    log.AppendLine($"      最寄りの床 {m.name} 水平距離 {d:F2}m " +
                                    $"上面y={m.bounds.max.y:F2} 大きさ={m.bounds.size.x:F1}x{m.bounds.size.z:F1}");
                    log.AppendLine($"         真上に NavMesh: {navHere} / layer={LayerMask.LayerToName(m.gameObject.layer)}" +
                                    $"({m.gameObject.layer}) / 静的={m.gameObject.isStatic}" +
                                    $" / 除外指定={(modifier != null ? modifier.ignoreFromBuild.ToString() : "なし")}" +
                                    $" / 親={(m.transform.parent != null ? m.transform.parent.name : "-")}");
                }

                // 床の向きを見る。床プレハブを天井にも流用していて、天井側は
                // 上下反転させて置いてある。もし部屋の床まで反転していたら、
                // 面が下を向くので NavMesh は生成されない（メッシュは在るのに歩けない）
                foreach (var (m, d) in ground.Take(1))
                {
                    var t = m.transform;
                    var up = t.up;
                    log.AppendLine($"         向き: rot={t.rotation.eulerAngles} " +
                                    $"lossyScale={t.lossyScale} 面の上方向={up} " +
                                    $"(上を向いている: {Vector3.Dot(up, Vector3.up) > 0f})");
                }

                // 病室が廊下とつながっているか。
                // つながっていないと、プレイヤーはそこに立てても敵は入って来られない
                // （プレイヤーは CharacterController で動くので NavMesh に縛られない）。
                var player = Object.FindFirstObjectByType<PlayerController>();
                if (player != null &&
                    NavMesh.SamplePosition(p, out var roomPoint, 4f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(player.transform.position, out var from, 4f, NavMesh.AllAreas))
                {
                    var route = new NavMeshPath();
                    NavMesh.CalculatePath(from.position, roomPoint.position, NavMesh.AllAreas, route);
                    log.AppendLine($"      廊下から病室へ経路: {route.status}" +
                                    (route.status == NavMeshPathStatus.PathComplete
                                        ? "（敵が入って来られる）" : "（敵は入って来られない）"));
                }

                // 部屋の中を格子で叩いて、どこに NavMesh があるか数える
                int hitCount = 0, total = 0;
                for (float dx = -2.5f; dx <= 2.5f; dx += 0.5f)
                for (float dz = -3f; dz <= 3f; dz += 0.5f)
                {
                    total++;
                    var probe = new Vector3(p.x + dx, 0.2f, p.z + dz);
                    if (NavMesh.SamplePosition(probe, out _, 0.4f, NavMesh.AllAreas)) hitCount++;
                }
                log.AppendLine($"      部屋の中の格子 {hitCount}/{total} 点に NavMesh");

                // 部屋の中心の真上・真下に何が積まれているか。
                // 天井が低すぎるとエージェント（身長2m）が立てず、床があっても
                // NavMesh は生成されない。そういう「見えない蓋」を探す
                var stack = all.Where(m => m.bounds.size.y < 0.8f)
                               .Where(m => m.bounds.min.x <= p.x && m.bounds.max.x >= p.x &&
                                           m.bounds.min.z <= p.z && m.bounds.max.z >= p.z)
                               .OrderBy(m => m.bounds.center.y)
                               .ToList();
                log.AppendLine($"      部屋中心の真上下にある水平面 {stack.Count} 枚");
                foreach (var m in stack.Take(6))
                    log.AppendLine($"        y={m.bounds.center.y:F2} 厚さ={m.bounds.size.y:F2} " +
                                    $"{m.name} (親={(m.transform.parent != null ? m.transform.parent.name : "-")})");

                // 部屋を囲む壁の範囲。床を足すならこの内側に敷く
                var walls = all.Where(m => m.bounds.size.y > 1.5f)
                               .Where(m => Vector2.Distance(
                                   new Vector2(m.bounds.center.x, m.bounds.center.z),
                                   new Vector2(p.x, p.z)) < 5f)
                               .ToList();
                if (walls.Count > 0)
                {
                    var b = walls[0].bounds;
                    foreach (var w in walls) b.Encapsulate(w.bounds);
                    log.AppendLine($"      周囲5m内の壁 {walls.Count} 枚 → 範囲 " +
                                    $"x[{b.min.x:F1},{b.max.x:F1}] z[{b.min.z:F1},{b.max.z:F1}]");
                }
            }
        }

        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
