using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M3: 1990年代の閉鎖精神病院に見えるところまで雰囲気を落とす。
///
/// 診断で分かった元の状態:
///   環境光 = Flat RGBA(0.3,0.3,0.3) 強度1.0  → どこも均一に明るく影が死んでいる
///   フォグ = 無効                             → 奥行きも闇も出ない
///   点光源 35〜53個 平均強度 0.75             → 廊下が煌々と点いている
///
/// ホラーとして最低限成立させるには「基本は暗く、光源の周りだけ見える」必要がある。
/// 数値は控えめに始めて、スクリーンショットを見ながら詰める前提。
/// </summary>
public static class M3AtmospherePass
{
    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    // フロアが下がるほど暗くする（地下が最も暗い）
    struct FloorMood
    {
        public Color ambient;
        public float fogDensity;
        public Color fogColor;
        public float lightScale;
        public float flickerRatio;   // ちらつかせる蛍光灯の割合
    }

    static FloorMood MoodFor(string scenePath)
    {
        if (scenePath.EndsWith("HospitalBasement.unity"))
            return new FloorMood {
                ambient = new Color(0.020f, 0.020f, 0.026f),
                fogColor = new Color(0.010f, 0.010f, 0.014f), fogDensity = 0.075f,
                lightScale = 0.45f, flickerRatio = 0.5f };

        if (scenePath.EndsWith("Hospital3F.unity"))
            return new FloorMood {
                ambient = new Color(0.030f, 0.030f, 0.038f),
                fogColor = new Color(0.018f, 0.018f, 0.024f), fogDensity = 0.055f,
                lightScale = 0.55f, flickerRatio = 0.4f };

        if (scenePath.EndsWith("Hospital2F.unity"))
            return new FloorMood {
                ambient = new Color(0.038f, 0.038f, 0.046f),
                fogColor = new Color(0.022f, 0.022f, 0.028f), fogDensity = 0.045f,
                lightScale = 0.6f, flickerRatio = 0.3f };

        // 1F はチュートリアル。完全な暗闇だと操作を覚えられないので少しだけ明るい
        return new FloorMood {
            ambient = new Color(0.048f, 0.048f, 0.056f),
            fogColor = new Color(0.028f, 0.028f, 0.034f), fogDensity = 0.035f,
            lightScale = 0.7f, flickerRatio = 0.25f };
    }

    /// <summary>
    /// 光ってはいけないマテリアルの自己発光を消す。
    ///
    /// Mat_Walllime01_C に _EmissionColor(0.55,0.53,0.49) が入っており、壁そのものが
    /// 発光していた。照明をいくら落としても壁だけ明るいままで、プレイ画面で
    /// 「暗い廊下に白い矩形が浮く」原因になっていた。
    /// （2026-05 に「暗すぎる」対処として入れられたと思われる）
    ///
    /// 蛍光灯やモニタなど、本来光るものは名前で除外する。
    /// </summary>
    static readonly string[] KeepEmissive =
    {
        "fluor", "fl_", "lamp", "light", "emis", "screen", "monitor", "exit", "glow",
        // 幽霊・黒い人影は演出として自ら光る想定なので触らない
        "ghost", "shadowfigure",
    };

    static bool ShouldKeepEmission(string materialName)
    {
        var lower = materialName.ToLowerInvariant();
        foreach (var keyword in KeepEmissive)
            if (lower.Contains(keyword)) return true;
        return false;
    }

    [MenuItem("消灯/M3: 発光してはいけないマテリアルを直す")]
    public static void ClearStrayEmission()
    {
        int cleared = 0, kept = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || !mat.HasProperty("_EmissionColor")) continue;

            var emission = mat.GetColor("_EmissionColor");
            if (emission.maxColorComponent <= 0.001f) continue;   // 元から光っていない

            if (ShouldKeepEmission(mat.name))
            {
                kept++;
                continue;
            }

            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(mat);
            cleared++;
            Debug.Log($"[M3AtmospherePass] 発光を解除: {mat.name} (was {emission})  {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[M3AtmospherePass] 発光解除 {cleared} 件 / 光源として維持 {kept} 件");
    }

    /// <summary>
    /// 壁の Z ファイティングを解消する。
    ///
    /// 漆喰の壁面(Mat_Walllime01_C)とタイルの腰壁(Mat_Tile01/02)が同一平面で重なっており、
    /// どちらが手前に描かれるかが面ごとに揺れて「暗い壁に白い矩形が並ぶ」状態になっていた。
    /// 2026-05 に壁を Y 方向へ引き伸ばして隙間を塞いだ処理の副作用と思われる。
    ///
    /// 漆喰側を壁の奥（廊下の外側）へわずかに逃がして、タイルが常に手前に来るようにする。
    /// </summary>
    const string ZFixMarkerName = "__ZFightFixApplied";
    const float ZFixOffset = 0.02f;

    static int FixWallZFighting()
    {
        // 再実行しても二重に動かさないよう、シーンにマーカーを置く
        if (GameObject.Find(ZFixMarkerName) != null)
        {
            Debug.Log("[M3AtmospherePass] Zファイティング対策は適用済み");
            return 0;
        }

        var surface = Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>();
        if (surface == null || surface.navMeshData == null)
        {
            Debug.LogWarning("[M3AtmospherePass] NavMesh が無いため壁の内外を判定できません");
            return 0;
        }
        var interior = surface.navMeshData.sourceBounds.center;

        int moved = 0;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var mat = r.sharedMaterial;
            if (mat == null || !mat.name.StartsWith("Mat_Walllime")) continue;

            var size = r.bounds.size;
            // 一番薄い軸が壁の法線方向
            Vector3 normal;
            if (size.x <= size.y && size.x <= size.z) normal = Vector3.right;
            else if (size.z <= size.x && size.z <= size.y) normal = Vector3.forward;
            else continue;   // 床/天井なので対象外

            // 廊下の内側から見て外向きになる符号を選ぶ
            float sign = Vector3.Dot(r.bounds.center - interior, normal) >= 0f ? 1f : -1f;

            r.transform.position += normal * (sign * ZFixOffset);
            EditorUtility.SetDirty(r.transform);
            moved++;
        }

        new GameObject(ZFixMarkerName);
        Debug.Log($"[M3AtmospherePass] 壁の Zファイティング対策: 漆喰面 {moved} 枚を {ZFixOffset}m 奥へ");
        return moved;
    }

    [MenuItem("消灯/M3: 雰囲気（照明・フォグ）を適用")]
    public static void RunBatch()
    {
        ClearStrayEmission();

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var mood = MoodFor(path);

            // FixWallZFighting() は誤診に基づいていたため呼び出しを外した。
            // 白い矩形は漆喰とタイルの重なりではなかった（VisualDiagnostics.IdentifyBrightPixels 参照）。

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = mood.ambient;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = mood.fogColor;
            RenderSettings.fogDensity = mood.fogDensity;

            int scaled = 0, flickered = 0;
            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            // 決定的に選ぶ（実行のたびに結果が変わらないよう index で判定）
            int flickerEvery = mood.flickerRatio > 0f ? Mathf.Max(2, Mathf.RoundToInt(1f / mood.flickerRatio)) : int.MaxValue;

            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];

                // 幻覚用の演出ライト（M2 で置いた家族の幻覚など）は触らない
                if (light.GetComponentInParent<ClueInteractable>() != null) continue;

                // 元の強度を1度だけ記録し、以降は常にそこから計算する。
                // 掛け算を積み重ねると再実行のたびに暗くなってしまうため。
                var record = light.GetComponent<LightBaseIntensity>();
                if (record == null)
                {
                    record = light.gameObject.AddComponent<LightBaseIntensity>();
                    record.baseIntensity = light.intensity;
                    EditorUtility.SetDirty(record);
                }

                light.intensity = record.baseIntensity * mood.lightScale;
                light.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(light);
                scaled++;

                if (i % flickerEvery == 0 && light.GetComponent<LightFlicker>() == null)
                {
                    light.gameObject.AddComponent<LightFlicker>();
                    flickered++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[M3AtmospherePass] {name}: 環境光={mood.ambient} フォグ密度={mood.fogDensity} " +
                      $"ライト{scaled}個を{mood.lightScale:P0}に / ちらつき{flickered}個追加");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[M3AtmospherePass] 完了");
    }
}
