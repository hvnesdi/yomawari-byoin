using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M18: 蛍光灯を天井の下に吊り直す。
///
/// 実測（`LightDiagnostics`）で2つ出た:
///
/// 1. **器具も光源も天井にめり込んでいた。**
///    器具は `HospitalFullSetup` が y=2.88 に決め打ちで置いていたが、
///    天井の下面は 1F/2F が 2.80、3F は 2.60。8〜28cm 埋まっている。
///    光源も 2.85 で天井より上にあり、下ではなく天井板を照らしていた。
///    画面でいちばん明るいのが天井、という逆さまの絵になっていた原因。
///
/// 2. **光源の半分以上に器具が無かった**（1F 36個中17個、2F 63個中36個）。
///    何も無い所が光っている状態で、これが「位置がおかしい」と読める。
///
/// ここでやること: 光源ごとに真上の天井を探し、
///   - 器具の**上面**を天井の下面に合わせる（吊り下げ）
///   - 光源を器具の少し下に置く（下を照らす）
///   - 器具の無い光源には器具を足す
///
/// 天井高はフロアで違う（3F は 2.60）ので、決め打ちにせず光源ごとに測る。
/// </summary>
public static class M18LightFixturePass
{
    /// <summary>天井の下面から光源までの距離。器具の下端より少し下に出す。</summary>
    const float LightDrop = 0.16f;

    /// <summary>器具が既にあると見なす水平距離。</summary>
    const float FixtureSearchRadius = 1.6f;

    const string AddedRoot = "AddedFixtures";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M18: 蛍光灯を天井の下に吊り直す")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M18] 蛍光灯の位置\n");

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            var ceilings = CeilingRenderers();
            if (ceilings.Count == 0) { log.AppendLine($"  {label}: 天井が見つからない"); continue; }

            var lights = CeilingLights();
            if (lights.Count == 0) { log.AppendLine($"  {label}: 天井灯が無い"); continue; }

            var template = FindTemplateFixture();
            var addedRoot = EnsureAddedRoot();

            int moved = 0, added = 0, orphan = 0;

            foreach (var light in lights)
            {
                float ceilingY = CeilingUnderAbove(ceilings, light.transform.position);
                if (float.IsNaN(ceilingY)) { orphan++; continue; }

                var fixture = NearestFixture(light.transform.position);

                if (fixture == null && template != null)
                {
                    fixture = (GameObject)PrefabUtility.InstantiatePrefab(
                        PrefabUtility.GetCorrespondingObjectFromSource(template) ?? template);
                    if (fixture == null) fixture = Object.Instantiate(template);
                    fixture.name = template.name;
                    fixture.transform.SetParent(addedRoot, true);
                    fixture.transform.rotation = template.transform.rotation;
                    added++;
                }

                if (fixture != null)
                {
                    // 器具の**上面**を天井の下面に合わせる。
                    // 中心を合わせると、器具の上半分が天井に埋まる
                    var bounds = FixtureBounds(fixture);
                    var pos = fixture.transform.position;
                    pos.x = light.transform.position.x;
                    pos.z = light.transform.position.z;

                    float topOffset = bounds.max.y - pos.y;   // 中心から上面までの距離
                    pos.y = ceilingY - topOffset - 0.005f;    // わずかに離して Z ファイティングを避ける

                    if (Vector3.Distance(fixture.transform.position, pos) > 0.005f)
                    {
                        fixture.transform.position = pos;
                        EditorUtility.SetDirty(fixture.transform);
                        moved++;
                    }
                }

                // 光源は天井の下。ここが天井より上にあると天井板だけが光る
                var lp = light.transform.position;
                float target = ceilingY - LightDrop;
                if (Mathf.Abs(lp.y - target) > 0.005f)
                {
                    lp.y = target;
                    light.transform.position = lp;
                    EditorUtility.SetDirty(light.transform);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  {label}: 光源 {lights.Count} 個 / 器具を移動 {moved} / 器具を追加 {added}" +
                            (orphan > 0 ? $" / 天井が見つからず据え置き {orphan}" : ""));
        }

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------
    static List<MeshRenderer> CeilingRenderers() =>
        Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .Where(m => m.bounds.size.y < 0.8f)
              .Where(m => m.bounds.size.x > 1.5f && m.bounds.size.z > 1.5f)
              .Where(m => m.bounds.center.y > 2.0f)
              .ToList();

    static List<Light> CeilingLights() =>
        Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
              .Where(l => l.type == LightType.Point || l.type == LightType.Spot)
              .Where(l => l.transform.position.y > 1.5f)
              // 手がかり用の演出ライトは動かさない
              .Where(l => l.GetComponentInParent<ClueInteractable>() == null)
              .ToList();

    /// <summary>
    /// その座標の真上にある天井の下面。
    /// フロアで高さが違ううえ、部屋と廊下でも違うので、決め打ちにせず1灯ずつ測る。
    /// </summary>
    static float CeilingUnderAbove(List<MeshRenderer> ceilings, Vector3 position)
    {
        float best = float.NaN;
        float bestGap = float.MaxValue;

        foreach (var c in ceilings)
        {
            var b = c.bounds;
            if (position.x < b.min.x || position.x > b.max.x) continue;
            if (position.z < b.min.z || position.z > b.max.z) continue;

            // 光源より上にあるものだけ。低い所に別階の床があっても拾わない
            float gap = b.min.y - position.y;
            if (gap < -0.4f) continue;

            if (Mathf.Abs(gap) < bestGap) { bestGap = Mathf.Abs(gap); best = b.min.y; }
        }
        return best;
    }

    static bool IsFixture(Transform t) =>
        t.name.StartsWith("prop_fluorescent") || t.name.StartsWith("P_Lamp");

    static GameObject NearestFixture(Vector3 position)
    {
        GameObject best = null;
        float bestDistance = FixtureSearchRadius;

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                              FindObjectsSortMode.None))
        {
            if (!IsFixture(t)) continue;
            var p = t.position;
            float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(position.x, position.z));
            if (d < bestDistance) { bestDistance = d; best = t.gameObject; }
        }
        return best;
    }

    /// <summary>器具を増やすときの見本。既にシーンに在るものを複製する。</summary>
    static GameObject FindTemplateFixture()
    {
        var candidates = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude,
                                                             FindObjectsSortMode.None)
                               .Where(t => t.name.StartsWith("prop_fluorescent_normal"))
                               .Select(t => t.gameObject).ToList();
        if (candidates.Count > 0) return candidates[0];

        return Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                     .Where(t => IsFixture(t)).Select(t => t.gameObject).FirstOrDefault();
    }

    static Bounds FixtureBounds(GameObject fixture)
    {
        var renderers = fixture.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0) return new Bounds(fixture.transform.position, Vector3.one * 0.1f);

        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform EnsureAddedRoot()
    {
        var existing = GameObject.Find(AddedRoot);
        return existing != null ? existing.transform : new GameObject(AddedRoot).transform;
    }
}
