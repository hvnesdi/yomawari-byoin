using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// M9: 間接光をベイクする。
///
/// ここまでで照明・材質・小道具は整えたが、光は実時間の点光源だけで
/// 跳ね返りが一切無い。実際の室内は間接光が支配的で、天井や壁で反射した光が
/// 影の中を満たしている。それが無いと、どれだけ材質を作り込んでも
/// 「暗いところは真っ黒」のCGに見える。
///
/// ベイクで得られるもの:
///   - 壁や床で跳ね返った光（色移りも起きる。緑の腰壁の上が緑を帯びる）
///   - 面光源としての蛍光灯（点光源より柔らかく、器具の形が影に出る）
///   - 隅の暗まり（SSAO より正確で、距離に応じた減衰がある）
///
/// 蛍光灯はちらつかせたいので実時間成分も残す必要がある。
/// よって Mixed にして、直接光は実時間・間接光はベイクとする。
///
/// バッチ実行:
///   Unity.exe -batchmode -projectPath ... -executeMethod M9BakedLightingPass.BakeThirdFloor
/// </summary>
public static class M9BakedLightingPass
{
    /// <summary>
    /// ライトマップ解像度。上げるほど綺麗だがベイク時間が急に伸びる。
    /// 廊下主体なので 12 texel/unit で十分なはず。まず 3F で時間を測る。
    /// </summary>
    const float TexelsPerUnit = 12f;

    [MenuItem("消灯/M9: 3F の間接光をベイク")]
    public static void BakeThirdFloor() => Bake("Assets/Scenes/Hospital3F.unity");

    [MenuItem("消灯/M9: 全フロアの間接光をベイク")]
    public static void BakeAll()
    {
        foreach (var path in new[]
        {
            "Assets/Scenes/Hospital.unity",
            "Assets/Scenes/Hospital2F.unity",
            "Assets/Scenes/Hospital3F.unity",
            "Assets/Scenes/HospitalBasement.unity",
        })
        {
            if (!Bake(path, exitWhenDone: false)) break;
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static bool Bake(string scenePath, bool exitWhenDone = true)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
        var label = System.IO.Path.GetFileNameWithoutExtension(scenePath);

        var (newlyMarked, totalStatic) = MarkStaticGeometry();
        int lights = ConfigureLights();
        ConfigureLightingSettings();

        // 「新たに印を付けた数」と「静的な総数」を分けて出す。
        // 以前は前者だけを「静的 N 個」と出していて、2回目の実行では
        // 既に印が付いている分が数えられず、数が減ったように見えた
        // （1F が 386 → 270 に減って一瞬あわてた。実際は何も失われていない）
        Debug.Log($"[Bake] {label}: 静的 {totalStatic} 個（うち新規 {newlyMarked}）" +
                  $" / ライト {lights} 個 / 解像度 {TexelsPerUnit} texel/unit");

        var sw = Stopwatch.StartNew();
        bool ok = Lightmapping.Bake();
        sw.Stop();

        if (!ok)
        {
            Debug.LogError($"[Bake] {label}: ベイクに失敗（{sw.Elapsed.TotalMinutes:F1} 分）");
            if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(1);
            return false;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Bake] {label}: 完了 {sw.Elapsed.TotalMinutes:F1} 分");

        if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(0);
        return true;
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// 建物側を静的にする。動くもの（プレイヤー・敵・NPC・幻覚の演出）は除く。
    /// 静的でないとライトマップに焼かれないし、逆に動くものを焼くと
    /// 動いた先に影が残る。
    /// </summary>
    static (int marked, int total) MarkStaticGeometry()
    {
        int marked = 0, total = 0;

        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = r.gameObject;
            if (IsDynamic(go)) continue;
            total++;

            var flags = StaticEditorFlags.ContributeGI
                      | StaticEditorFlags.BatchingStatic
                      | StaticEditorFlags.OccluderStatic
                      | StaticEditorFlags.OccludeeStatic
                      | StaticEditorFlags.ReflectionProbeStatic;

            if (GameObjectUtility.GetStaticEditorFlags(go) == flags) continue;
            GameObjectUtility.SetStaticEditorFlags(go, flags);

            // 半透明のデカールはライトマップの UV を持たせても意味が薄いので
            // GI に寄与だけさせて自身は受けない
            if (r.sharedMaterial != null && r.sharedMaterial.renderQueue >= 3000)
                r.receiveGI = ReceiveGI.LightProbes;

            marked++;
        }
        return (marked, total);
    }

    static bool IsDynamic(GameObject go)
    {
        // 走査するのは親も含めた系統。プレイヤー配下のカメラなども動く
        if (go.GetComponentInParent<PlayerController>() != null) return true;
        if (go.GetComponentInParent<EnemyController>() != null) return true;
        if (go.GetComponentInParent<NPCManager>() != null) return true;
        if (go.GetComponentInParent<ClueInteractable>() != null) return true;

        var n = go.name;
        // ショーケースは撮影用。動かないが焼く必要も無い
        if (n.Contains("Preview")) return true;
        return false;
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// 蛍光灯を Mixed にする。直接光は実時間（ちらつきを残す）、間接光はベイク。
    /// 切れている蛍光灯は無効なままなので何も焼かれない。
    /// </summary>
    static int ConfigureLights()
    {
        int count = 0;
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // 幻覚の演出用ライトは実時間のみ。焼くと消えなくなる
            if (light.GetComponentInParent<ClueInteractable>() != null)
            {
                light.lightmapBakeType = LightmapBakeType.Realtime;
                continue;
            }

            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = LightShadows.Soft;
            // ベイクでは面光源として扱われるので、半径を持たせると影が柔らかくなる
            light.shadowRadius = 0.35f;
            EditorUtility.SetDirty(light);
            count++;
        }
        return count;
    }

    // ------------------------------------------------------------------
    static void ConfigureLightingSettings()
    {
        var settings = new LightingSettings
        {
            name = "Shoto_Bake",
            bakedGI = true,
            realtimeGI = false,
            lightmapper = LightingSettings.Lightmapper.ProgressiveCPU,
            lightmapResolution = TexelsPerUnit,
            lightmapPadding = 2,
            lightmapMaxSize = 1024,
            ao = true,
            aoMaxDistance = 0.6f,
            aoExponentIndirect = 1.1f,
            aoExponentDirect = 0.6f,
            directionalityMode = LightmapsMode.CombinedDirectional,
            // 間接光の跳ね返り回数。2回あれば影の中が塗り潰される
            indirectSampleCount = 256,
            directSampleCount = 32,
            bounces = 2,
            denoiserTypeIndirect = LightingSettings.DenoiserType.Optix,
            filteringMode = LightingSettings.FilterMode.Auto,
            // 蛍光灯の器具など、発光マテリアルを光源として扱う
            mixedBakeMode = MixedLightingMode.IndirectOnly,
        };

        Lightmapping.lightingSettings = settings;

        // 発光マテリアルをベイクの光源にする
        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
            if (mat.GetColor("_EmissionColor").maxColorComponent <= 0.001f) continue;

            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(mat);
        }
    }
}
