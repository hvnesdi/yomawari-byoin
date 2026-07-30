using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// M5: 画づくりの土台を整える。市販ホラーの見え方に近づけるための描画設定。
///
/// ここまでで照明とマテリアルは直したが、描画パイプライン側が素のままだった:
///   - トーンマッピング無し → ハイライトが白飛びし、階調が浅い
///   - ブルーム無し         → 暗所の光源が「光っている」ように見えない
///   - SSAO 無し            → 物と床の接地感が無く、書き割りに見える
///   - 追加ライトの影無し   → 光源が増えても陰影が生まれない
///   - フィルムグレイン無し → CG のクリーンさが残る
///
/// これらは個々の小技ではなく、揃って初めて「それっぽく」なる類のもの。
/// </summary>
public static class M5LookPass
{
    const string ProfilePath  = "Assets/Resources/HallucinationProfile.asset";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";

    [MenuItem("消灯/M5: 画づくりの設定を適用")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M5] 画づくり設定\n");

        ConfigurePipelineAsset(log);
        ConfigureRenderer(log);
        ConfigureVolumeProfile(log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------
    // URP アセット: HDR・アンチエイリアス・影
    // ------------------------------------------------------------------
    static void ConfigurePipelineAsset(StringBuilder log)
    {
        var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null)
        {
            log.AppendLine("  URP アセットが取得できない");
            return;
        }

        var so = new SerializedObject(asset);

        // ブルームとトーンマッピングは HDR が前提。切れていると効かない
        Set(so, "m_SupportsHDR", true, log);
        // 4x MSAA。細い手すりや器具のジャギは安っぽさに直結する
        SetInt(so, "m_MSAA", 4, log);

        // 追加ライト（点光源）の影。これが無いと光源を増やしても陰影が出ない
        SetInt(so, "m_AdditionalLightsRenderingMode", 1, log);   // Per Pixel
        Set(so, "m_AdditionalLightShadowsSupported", true, log);
        SetInt(so, "m_AdditionalLightsShadowmapResolution", 2048, log);
        SetInt(so, "m_AdditionalLightsPerObjectLimit", 8, log);

        Set(so, "m_MainLightShadowsSupported", true, log);
        SetInt(so, "m_MainLightShadowmapResolution", 2048, log);
        Set(so, "m_SoftShadowsSupported", true, log);

        // 廊下主体なので遠距離の影は要らない。近距離に解像度を回す
        SetFloat(so, "m_ShadowDistance", 32f, log);
        SetInt(so, "m_ShadowCascadeCount", 2, log);
        SetFloat(so, "m_ShadowDepthBias", 0.8f, log);
        SetFloat(so, "m_ShadowNormalBias", 0.9f, log);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(asset);
    }

    // ------------------------------------------------------------------
    // レンダラ: SSAO
    // ------------------------------------------------------------------
    static void ConfigureRenderer(StringBuilder log)
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null)
        {
            log.AppendLine($"  {RendererPath} が読めない");
            return;
        }

        var existing = data.rendererFeatures.FirstOrDefault(f => f is ScreenSpaceAmbientOcclusion);
        if (existing == null)
        {
            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "SSAO";
            data.rendererFeatures.Add(ssao);
            AssetDatabase.AddObjectToAsset(ssao, data);
            existing = ssao;
            log.AppendLine("  SSAO を追加");
        }
        else
        {
            log.AppendLine("  SSAO は追加済み");
        }

        // 接地感を出すのが目的。強くかけると輪郭が汚れるので控えめに
        var sso = new SerializedObject(existing);
        var settings = sso.FindProperty("m_Settings");
        if (settings != null)
        {
            SetChild(settings, "Intensity", 1.1f, log);
            SetChild(settings, "Radius", 0.28f, log);
            SetChild(settings, "DirectLightingStrength", 0.35f, log);
            SetChild(settings, "Downsample", true, log);
            sso.ApplyModifiedProperties();
        }
        else
        {
            log.AppendLine("  SSAO の設定プロパティが見つからない（既定値のまま）");
        }

        EditorUtility.SetDirty(data);
    }

    // ------------------------------------------------------------------
    // ポストプロセス
    // ------------------------------------------------------------------
    static void ConfigureVolumeProfile(StringBuilder log)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            log.AppendLine($"  {ProfilePath} が読めない");
            return;
        }

        for (int i = profile.components.Count - 1; i >= 0; i--)
            if (profile.components[i] != null) Object.DestroyImmediate(profile.components[i], true);
        profile.components.Clear();

        // 白飛びを抑えて階調を残す。これが無いと光源周りが真っ白に潰れる
        var tone = AddPersistent<Tonemapping>(profile);
        tone.mode.value = TonemappingMode.ACES;

        // 彩度を落としてコントラストを上げる。1990年代の記録映像のような色に寄せる。
        // 露出は下げる方向。プレイ画面を実測したら平均輝度 0.41 で
        // 「消灯」という題に対して明るすぎた（+0.35 EV は明るくする指定だった）
        var color = AddPersistent<ColorAdjustments>(profile);
        color.postExposure.value = -0.55f;
        color.contrast.value = 16f;
        color.saturation.value = -20f;

        // 蛍光灯の青白さ。暖色に寄ると「生活感のある病院」になってしまう
        var wb = AddPersistent<WhiteBalance>(profile);
        wb.temperature.value = -16f;
        wb.tint.value = 5f;

        // 影を青、ハイライトをわずかに暖色にして色の分離を作る
        var smh = AddPersistent<ShadowsMidtonesHighlights>(profile);
        smh.shadows.value    = new Vector4(0.82f, 0.90f, 1.12f, 0f);
        smh.midtones.value   = new Vector4(1.00f, 1.00f, 1.00f, 0f);
        smh.highlights.value = new Vector4(1.06f, 1.02f, 0.94f, 0f);

        // 暗い廊下で光源が光って見えるかはブルームで決まる
        var bloom = AddPersistent<Bloom>(profile);
        bloom.threshold.value = 0.75f;
        bloom.intensity.value = 0.9f;
        bloom.scatter.value = 0.72f;
        bloom.tint.value = new Color(0.88f, 0.93f, 1f);
        bloom.highQualityFiltering.value = true;

        // CG のクリーンさを消す。ホラーでは粒子感がそのまま不安感になる
        var grain = AddPersistent<FilmGrain>(profile);
        grain.type.value = FilmGrainLookup.Medium1;
        grain.intensity.value = 0.32f;
        grain.response.value = 0.75f;

        // 以下3つは HallucinationSystem が毎フレーム上書きするので、ここは初期値
        var vignette = AddPersistent<Vignette>(profile);
        vignette.intensity.value = 0.34f;
        vignette.smoothness.value = 0.45f;
        vignette.color.value = Color.black;

        var ca = AddPersistent<ChromaticAberration>(profile);
        ca.intensity.value = 0.06f;

        var ld = AddPersistent<LensDistortion>(profile);
        ld.intensity.value = 0f;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ProfilePath);

        // 保存できたか数えて出す。「設定した」ではなく「残った」を報告する
        var saved = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        int alive = 0;
        if (saved != null)
            foreach (var c in saved.components) if (c != null) alive++;
        log.AppendLine($"  ポストプロセス: {alive} 個を保存 " +
                        "(ACES / 色調整 / 色温度 / 影ハイライト分離 / ブルーム / グレイン / ビネット)");
        if (alive == 0)
            log.AppendLine("  ? ポストプロセスが1つも保存されていない");
    }

    /// <summary>
    /// ボリュームに効果を足して、**アセットの一部として保存する**。
    ///
    /// `profile.Add&lt;T&gt;()` だけでは足りない。生成されるのはメモリ上のインスタンスで、
    /// アセットの子として登録されないため、Unity を開き直すと参照が全部 null になる。
    /// 実際にそうなっていて、`HallucinationProfile.asset` の components は
    /// `{fileID: 0}` が9個並んだ状態だった。
    ///
    /// **つまり ACES・彩度・グレイン・ブルームは一度も効いていなかった。**
    /// 画面が「昼間の事務所」のように明るく、粒子感もビネットも無かった原因はこれ。
    /// パスのログは毎回「設定した」と報告していたので、ログだけでは気づけなかった。
    /// </summary>
    static T AddPersistent<T>(VolumeProfile profile) where T : VolumeComponent
    {
        var component = profile.Add<T>(true);
        component.name = typeof(T).Name;   // 付けないとインスペクタで名前無しになる
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    // ------------------------------------------------------------------
    static void Set(SerializedObject so, string name, bool value, StringBuilder log)
    {
        var p = so.FindProperty(name);
        if (p == null) { log.AppendLine($"  ? {name} が無い"); return; }
        p.boolValue = value;
        log.AppendLine($"  {name} = {value}");
    }

    static void SetInt(SerializedObject so, string name, int value, StringBuilder log)
    {
        var p = so.FindProperty(name);
        if (p == null) { log.AppendLine($"  ? {name} が無い"); return; }
        p.intValue = value;
        log.AppendLine($"  {name} = {value}");
    }

    static void SetFloat(SerializedObject so, string name, float value, StringBuilder log)
    {
        var p = so.FindProperty(name);
        if (p == null) { log.AppendLine($"  ? {name} が無い"); return; }
        p.floatValue = value;
        log.AppendLine($"  {name} = {value}");
    }

    static void SetChild(SerializedProperty parent, string name, object value, StringBuilder log)
    {
        var p = parent.FindPropertyRelative(name);
        if (p == null) { log.AppendLine($"  ? SSAO.{name} が無い"); return; }
        switch (value)
        {
            case float f: p.floatValue = f; break;
            case bool b:  p.boolValue = b;  break;
            case int i:   p.intValue = i;   break;
        }
        log.AppendLine($"  SSAO.{name} = {value}");
    }
}
