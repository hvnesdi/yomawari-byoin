using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M3: 見た目の問題を「推測せずに」特定するための診断。
///
/// プレイ画面に写っている白い矩形の正体、明るすぎる照明の内訳、
/// 誤ったマテリアルが割り当てられた面を洗い出す。
/// </summary>
public static class VisualDiagnostics
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital3F.unity",
    };

    /// <summary>
    /// 「画面に写っている明るい部分が、どのオブジェクトなのか」を機械的に特定する。
    ///
    /// 手順:
    ///   1. 通常描画（ビューティ）を1枚
    ///   2. 全レンダラに固有色の Unlit マテリアルを割り当てて描画（IDパス）を1枚
    ///   3. ビューティで明るいピクセルを拾い、同じ座標のID色から object を逆引きして集計
    ///
    /// 見た目の問題を「たぶんデカール」「たぶんZファイティング」と推測で追うと外すので、
    /// 画面の実測から特定する。
    /// </summary>
    [MenuItem("消灯/M3: 明るい部分の正体を特定")]
    public static void IdentifyBrightPixels()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);

        var pc = Object.FindFirstObjectByType<PlayerController>();
        var cam = pc != null ? pc.GetComponentInChildren<Camera>(true) : null;
        if (cam == null) { Debug.LogError("[VisualDiagnostics] カメラが見つかりません"); return; }

        // 実行時と同じく、カメラのピッチは 0 として撮る
        var savedRot = cam.transform.rotation;
        cam.transform.rotation = Quaternion.Euler(0f, pc.transform.eulerAngles.y, 0f);

        IdentifyFromCamera(cam);
        cam.transform.rotation = savedRot;
    }

    /// <summary>
    /// 指定カメラの視界について、明るいピクセルの正体を集計する。
    ///
    /// エディットモードの手動 cam.Render() では URP のライティングが正しく乗らず
    /// 「明るいピクセル0件」になってしまうため、実際にはプレイモード中に
    /// PlayModeBatchRunner から呼ぶ。
    /// </summary>
    /// <summary>
    /// 「明るい」の閾値。周囲のタイル壁が輝度0.26、問題の矩形が0.48〜0.57、
    /// 右の漆喰壁が0.40 なので、この値で矩形だけを拾える。
    ///
    /// 当初 0.72 にしていて0件になり「ツールが壊れている」と誤判断した。
    /// 白く見えても実測は0.665止まりで、明るさは周囲との相対で見えていただけだった。
    /// </summary>
    public const float BrightThreshold = 0.48f;

    public static void IdentifyFromCamera(Camera cam)
    {
        const int W = 1280, H = 720;
        var beauty = Render(cam, W, H);

        // --- ID パス ---
        var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                              .Where(r => Vector3.Distance(cam.transform.position, r.bounds.center) < 40f)
                              .ToArray();

        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) { Debug.LogError("[VisualDiagnostics] Unlit シェーダが見つかりません"); return; }

        var saved = new Material[renderers.Length][];
        var colorToName = new Dictionary<int, string>();
        var temp = new List<Material>();
        var palette = new List<(Color32 color, int index)>();

        for (int i = 0; i < renderers.Length; i++)
        {
            saved[i] = renderers[i].sharedMaterials;

            // 各チャンネル 32 刻みの離散色を使う。
            // ポストプロセスや色空間変換で多少ずれても最近傍で復元できるようにするため。
            int id = i;
            var color = new Color32(
                (byte)((id       % 8) * 32 + 16),
                (byte)((id / 8   % 8) * 32 + 16),
                (byte)((id / 64  % 8) * 32 + 16), 255);
            palette.Add((color, i));
            colorToName[Key(color)] = $"{Path(renderers[i].transform)}  mat={(saved[i].Length > 0 && saved[i][0] != null ? saved[i][0].name : "?")}";

            var m = new Material(unlit);
            m.SetColor("_BaseColor", color);
            temp.Add(m);

            var slots = new Material[saved[i].Length == 0 ? 1 : saved[i].Length];
            for (int s = 0; s < slots.Length; s++) slots[s] = m;
            renderers[i].sharedMaterials = slots;
        }

        // ID パス中はポストプロセスを切る。ビネットやトーンマッピングが乗ると
        // 割り当てた色が変わってしまい、逆引きできなくなる。
        var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool prevPostProcessing = camData != null && camData.renderPostProcessing;
        if (camData != null) camData.renderPostProcessing = false;

        var idPass = Render(cam, W, H);

        if (camData != null) camData.renderPostProcessing = prevPostProcessing;

        for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = saved[i];
        foreach (var m in temp) Object.DestroyImmediate(m);

        // --- 明るいピクセルを集計 ---
        var tally = new Dictionary<int, int>();
        int brightCount = 0;
        var bp = beauty.GetPixels32();
        var ip = idPass.GetPixels32();

        for (int i = 0; i < bp.Length; i++)
        {
            float lum = (bp[i].r * 0.299f + bp[i].g * 0.587f + bp[i].b * 0.114f) / 255f;
            if (lum < BrightThreshold) continue;
            brightCount++;

            // 完全一致ではなく最近傍で引く（多少の色ズレを許容する）
            int best = -1, bestDist = int.MaxValue;
            foreach (var (color, _) in palette)
            {
                int dr = color.r - ip[i].r, dg = color.g - ip[i].g, db = color.b - ip[i].b;
                int d = dr * dr + dg * dg + db * db;
                if (d < bestDist) { bestDist = d; best = Key(color); }
            }
            // 32刻みなので、離れすぎているものは面の境界の混色とみなす
            int key = bestDist <= 16 * 16 * 3 ? best : -1;
            tally[key] = tally.TryGetValue(key, out var c) ? c + 1 : 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[明るい部分の正体] 輝度{BrightThreshold}超のピクセル {brightCount} / {bp.Length} " +
                      $"({(float)brightCount / bp.Length:P1})");
        foreach (var kv in tally.OrderByDescending(k => k.Value).Take(15))
        {
            var name = colorToName.TryGetValue(kv.Key, out var n) ? n : "(不明・境界の混色)";
            sb.AppendLine($"    {kv.Value,7} px ({(float)kv.Value / brightCount:P1})  {name}");
        }
        Debug.Log(sb.ToString());

        Object.DestroyImmediate(beauty);
        Object.DestroyImmediate(idPass);
    }

    static int Key(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

    static Texture2D Render(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        rt.Release();
        Object.DestroyImmediate(rt);
        return tex;
    }

    /// <summary>
    /// PackArch 配下の壁パネルの配置を一覧する。
    /// 白い矩形の正体が P_Wall_02/Wall_02（漆喰）と分かったので、
    /// それがどこに、どの壁と重なって置かれているのかを確認する。
    /// </summary>
    [MenuItem("消灯/M3: 壁パネルの重なりを調べる")]
    public static void ReportWallOverlaps()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Hospital.unity", OpenSceneMode.Single);

        var panels = new List<(string name, Transform t, Bounds b)>();
        foreach (var root in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!root.name.StartsWith("P_Wall")) continue;
            var rs = root.GetComponentsInChildren<MeshRenderer>();
            if (rs.Length == 0) continue;

            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            panels.Add((Path(root), root, b));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"[壁パネル] {panels.Count} 枚");
        foreach (var p in panels.OrderBy(x => x.name))
            sb.AppendLine($"    {p.name}  pos={p.t.position}  rot={p.t.eulerAngles}  " +
                          $"size=({p.b.size.x:F2},{p.b.size.y:F2},{p.b.size.z:F2})");

        sb.AppendLine("[重なっている組み合わせ]");
        int overlaps = 0;
        for (int i = 0; i < panels.Count; i++)
        for (int j = i + 1; j < panels.Count; j++)
        {
            if (!panels[i].b.Intersects(panels[j].b)) continue;

            // 交差体積が小さい方の体積のどれくらいを占めるか
            var a = panels[i].b; var c = panels[j].b;
            var min = Vector3.Max(a.min, c.min);
            var max = Vector3.Min(a.max, c.max);
            var d = max - min;
            float interVolume = Mathf.Max(0, d.x) * Mathf.Max(0, d.y) * Mathf.Max(0, d.z);
            float smaller = Mathf.Min(a.size.x * a.size.y * a.size.z, c.size.x * c.size.y * c.size.z);
            if (smaller <= 0f) continue;
            float ratio = interVolume / smaller;
            if (ratio < 0.05f) continue;

            overlaps++;
            sb.AppendLine($"    {ratio:P0} 重複: {panels[i].name} ∩ {panels[j].name}");
        }
        if (overlaps == 0) sb.AppendLine("    (重なりなし)");

        Debug.Log(sb.ToString());
    }

    [MenuItem("消灯/M3: 見た目を診断")]
    public static void RunBatch()
    {
        foreach (var path in Scenes)
        {
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);

            var sb = new StringBuilder();
            sb.AppendLine($"########## {name} ##########");
            ReportVisibleRenderers(sb);
            ReportWhiteRenderers(sb);
            ReportLighting(sb);
            ReportMaterialUsage(sb);
            Debug.Log(sb.ToString());
        }
    }

    /// <summary>
    /// 起動直後の画面に何が大きく写るかを、画面占有率つきで列挙する。
    /// スクリーンショットに写っている物体の正体を推測で当てにいかないための診断。
    /// カメラのピッチは実行時に CameraController が 0 にするので水平として計算する。
    /// </summary>
    static void ReportVisibleRenderers(StringBuilder sb)
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        if (pc == null) { sb.AppendLine("[視界] プレイヤーが見つかりません"); return; }

        var cam = pc.GetComponentInChildren<Camera>(true);
        if (cam == null) { sb.AppendLine("[視界] カメラが見つかりません"); return; }

        const float W = 1280f, H = 720f;
        var camPos = cam.transform.position;
        var camRot = Quaternion.Euler(0f, pc.transform.eulerAngles.y, 0f);

        var view = Matrix4x4.TRS(camPos, camRot, new Vector3(1f, 1f, -1f)).inverse;
        var proj = Matrix4x4.Perspective(cam.fieldOfView, W / H, cam.nearClipPlane, cam.farClipPlane);
        var vp = proj * view;

        var found = new List<(float area, string info)>();

        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var b = r.bounds;
            if (Vector3.Distance(camPos, b.center) > 30f) continue;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            bool anyInFront = false;

            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);

                var clip = vp * new Vector4(corner.x, corner.y, corner.z, 1f);
                if (clip.w <= 0.01f) continue;
                anyInFront = true;

                float sx = (clip.x / clip.w * 0.5f + 0.5f) * W;
                float sy = (1f - (clip.y / clip.w * 0.5f + 0.5f)) * H;

                minX = Mathf.Min(minX, sx); maxX = Mathf.Max(maxX, sx);
                minY = Mathf.Min(minY, sy); maxY = Mathf.Max(maxY, sy);
            }

            if (!anyInFront) continue;
            if (maxX < 0 || minX > W || maxY < 0 || minY > H) continue;

            float cx = Mathf.Clamp(minX, 0, W), cX = Mathf.Clamp(maxX, 0, W);
            float cy = Mathf.Clamp(minY, 0, H), cY = Mathf.Clamp(maxY, 0, H);
            float area = (cX - cx) * (cY - cy) / (W * H) * 100f;
            if (area < 0.15f) continue;

            var mat = r.sharedMaterial != null ? r.sharedMaterial.name : "(null)";
            found.Add((area, $"    {area,5:F1}%  x[{cx,4:F0}-{cX,4:F0}] y[{cy,4:F0}-{cY,4:F0}]  " +
                             $"{Path(r.transform)}  mat={mat}"));
        }

        sb.AppendLine($"[起動直後の視界] カメラ {camPos} yaw={pc.transform.eulerAngles.y:F0}度 / 画面1280x720換算");
        foreach (var f in found.OrderByDescending(x => x.area).Take(20))
            sb.AppendLine(f.info);

        // 大きな壁に埋もれる小物体を別枠で出す。
        // スクリーンショットに写る「白い矩形」はここに現れるはず。
        sb.AppendLine("[視界内の小物体] 画面占有 0.2%〜3%");
        foreach (var f in found.Where(x => x.area <= 3f).OrderByDescending(x => x.area).Take(25))
            sb.AppendLine(f.info);
    }

    /// <summary>プレイヤー周辺で「テクスチャ無し・ほぼ白」のレンダラを列挙する。</summary>
    static void ReportWhiteRenderers(StringBuilder sb)
    {
        var pc = Object.FindFirstObjectByType<PlayerController>();
        Vector3 origin = pc != null ? pc.transform.position : Vector3.zero;

        var hits = new List<(float dist, string info)>();

        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(origin, r.bounds.center);
            if (d > 20f) continue;

            var mat = r.sharedMaterial;
            if (mat == null)
            {
                hits.Add((d, $"    {d,5:F1}m  {Path(r.transform)}  [マテリアル未設定]"));
                continue;
            }

            bool hasTexture = mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null;
            Color color = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            bool nearWhite = color.r > 0.85f && color.g > 0.85f && color.b > 0.85f;

            if (!hasTexture && nearWhite)
            {
                var size = r.bounds.size;
                hits.Add((d, $"    {d,5:F1}m  {Path(r.transform)}  mat={mat.name}  " +
                             $"size=({size.x:F2},{size.y:F2},{size.z:F2})  shader={mat.shader.name}"));
            }
        }

        sb.AppendLine($"[白い/テクスチャ無しのレンダラ] プレイヤー半径20m内: {hits.Count} 件");
        foreach (var h in hits.OrderBy(x => x.dist).Take(30))
            sb.AppendLine(h.info);
        if (hits.Count > 30) sb.AppendLine($"    …他 {hits.Count - 30} 件");
    }

    static void ReportLighting(StringBuilder sb)
    {
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        sb.AppendLine($"[ライト] {lights.Length} 個");

        var byType = lights.GroupBy(l => l.type);
        foreach (var g in byType)
        {
            var intensities = g.Select(l => l.intensity).ToList();
            sb.AppendLine($"    {g.Key}: {g.Count()} 個  " +
                          $"強度 min={intensities.Min():F2} max={intensities.Max():F2} 平均={intensities.Average():F2}");
        }

        sb.AppendLine($"    環境光モード: {RenderSettings.ambientMode}");
        sb.AppendLine($"    環境光: {RenderSettings.ambientLight}  強度={RenderSettings.ambientIntensity:F2}");
        sb.AppendLine($"    フォグ: {(RenderSettings.fog ? $"有効 色={RenderSettings.fogColor} 密度={RenderSettings.fogDensity:F4}" : "無効")}");
    }

    /// <summary>どのマテリアルが何面に使われているか（誤割り当ての発見用）。</summary>
    static void ReportMaterialUsage(StringBuilder sb)
    {
        var usage = new Dictionary<string, int>();
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            foreach (var m in r.sharedMaterials)
            {
                var key = m != null ? m.name : "(null)";
                usage[key] = usage.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }

        sb.AppendLine($"[マテリアル使用状況] 上位15件");
        foreach (var kv in usage.OrderByDescending(k => k.Value).Take(15))
            sb.AppendLine($"    {kv.Value,5} 面  {kv.Key}");
    }

    static string Path(Transform t)
    {
        var parts = new List<string>();
        while (t != null) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }
}
