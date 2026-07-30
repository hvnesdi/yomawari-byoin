using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// M10: 実写スキャンのマテリアルに差し替える。
///
/// これまでのテクスチャは私が数学的ノイズから生成したもので、凹凸と粗さは
/// 与えられていたが、実物が持つ不規則さや履歴感には届かなかった。
/// ambientCG の CC0 素材（実物スキャン）に置き換える。
///
/// 素材は tools/gen_surfaces.py 由来のものと違い、色まで含んだ本物なので
/// _BaseColor は白にしてテクスチャを素通しさせる。
///
/// 差し替えは「新しいマテリアルを作って貼り替える」のではなく
/// 「既存マテリアルのテクスチャを入れ替える」方式にした。
/// シーン側の参照が全て生きたままになり、貼り替え漏れが起きない。
/// タイリングは既存値に素材ごとの倍率を掛ける（実寸差の補正）。
/// </summary>
public static class M10RealMaterialsPass
{
    const string TexDir = "Assets/Textures/Ambient";

    /// <summary>
    /// ambientCG の素材と、適用先マテリアル名のキーワード、タイリング倍率。
    ///
    /// 素材選びで一度失敗している。zip を落としてから適用して初めて
    /// PaintedMetal004 が「赤い剥がれ塗装」、PaintedPlaster016 が
    /// 「レンガ露出の廃墟外壁」だと分かり、画が明確に悪化した。
    /// API の previewImage（球体のサムネイル）を先に見れば数秒で分かることだった。
    /// 素材を足すときは必ずサムネイルを確認してから落とすこと。
    ///
    /// タイリング倍率も失敗の原因だった。既存値を保持したところ、模様が
    /// 実寸に対して巨大になった（床の目地が 1m 角に見えた）。
    /// ambientCG の素材は 1枚で実寸 1〜2m 程度をカバーする前提なので、
    /// パック素材向けに調整されていた既存値より多く繰り返す必要がある。
    /// </summary>
    static readonly (string asset, string[] keywords, float metallicScale, float tilingMul)[] Mapping =
    {
        // 天井の吸音板だけを採用する。
        //
        // 壁・床・金属も一度差し替えたが、いずれも画が悪化したので戻した。
        // スキャン素材そのものの品質は高い。合わなかった理由は別で、
        //   - 壁: 1枚 1〜2m の素材を長い廊下に敷くと、繰り返しが「レンガ調の模様」に見える
        //   - 床: 実物の光沢がそのまま出て、廃病院の床としては綺麗すぎた
        //   - 金属: 実写の塗装は情報量が多く、細い配管では模様が潰れて汚れに見えた
        // つまり「素材の実寸」と「貼る面の大きさ」が噛み合っていない。
        // 手続き生成のテクスチャは面の大きさに合わせて調整できるので、
        // この形状に対しては結果的にそちらのほうが合っていた。
        //
        // 天井だけは噛み合った。有孔ボードは実際に 60cm 角前後の繰り返しで、
        // 素材の実寸と天井の寸法が近いため。
        ("OfficeCeiling003", new[] { "ceiling", "ceil" }, 0f, 2f),
    };

    [MenuItem("消灯/M10: 実写マテリアルに差し替え")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M10] 実写マテリアルの適用\n");

        var sets = LoadTextureSets(log);
        if (sets.Count == 0)
        {
            Debug.LogError("[M10] テクスチャが見つからない。先に ambientCG の素材を配置すること");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        ConfigureImporters(sets, log);
        int applied = ApplyToMaterials(sets, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine($"  {applied} マテリアルに適用");
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    class TextureSet
    {
        public Texture2D color, normal, metallicSmoothness, occlusion;
    }

    static Dictionary<string, TextureSet> LoadTextureSets(StringBuilder log)
    {
        var result = new Dictionary<string, TextureSet>();

        foreach (var (asset, _, _, _) in Mapping)
        {
            var folder = $"{TexDir}/{asset}";
            if (!AssetDatabase.IsValidFolder(folder)) { log.AppendLine($"  ? {folder} が無い"); continue; }

            var set = new TextureSet
            {
                color = Find(folder, "_Color"),
                // Unity は OpenGL 規約のノーマルマップを使うので NormalGL を選ぶ。
                // NormalDX を渡すと凹凸が逆に出る（見た目は「なんとなく変」で気づきにくい）
                normal = Find(folder, "_NormalGL"),
                metallicSmoothness = Find(folder, "_MetallicSmoothness"),
                occlusion = Find(folder, "_AmbientOcclusion"),
            };

            if (set.color == null || set.normal == null)
            {
                log.AppendLine($"  ? {asset}: Color か NormalGL が無い");
                continue;
            }
            result[asset] = set;
            log.AppendLine($"  {asset}: Color/Normal{(set.metallicSmoothness ? "/MS" : "")}" +
                            $"{(set.occlusion ? "/AO" : "")}");
        }
        return result;
    }

    static Texture2D Find(string folder, string suffix)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path).EndsWith(suffix))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }

    /// <summary>
    /// インポート設定。ノーマルマップは必ず NormalMap 型にする。
    /// AO と MetallicSmoothness は色ではなくデータなので sRGB を切る
    /// （切らないとガンマ補正がかかって値がずれる）。
    /// </summary>
    static void ConfigureImporters(Dictionary<string, TextureSet> sets, StringBuilder log)
    {
        foreach (var set in sets.Values)
        {
            Configure(set.normal, TextureImporterType.NormalMap, true);
            Configure(set.color, TextureImporterType.Default, true);
            Configure(set.metallicSmoothness, TextureImporterType.Default, false);
            Configure(set.occlusion, TextureImporterType.Default, false);
        }
        log.AppendLine("  インポート設定を適用（NormalGL→NormalMap、AO/MS は sRGB 無効）");
    }

    static void Configure(Texture2D tex, TextureImporterType type, bool sRGB)
    {
        if (tex == null) return;
        var path = AssetDatabase.GetAssetPath(tex);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        bool changed = false;
        if (importer.textureType != type) { importer.textureType = type; changed = true; }
        if (importer.sRGBTexture != sRGB) { importer.sRGBTexture = sRGB; changed = true; }
        if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; changed = true; }
        if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }

        if (changed) importer.SaveAndReimport();
    }

    static int ApplyToMaterials(Dictionary<string, TextureSet> sets, StringBuilder log)
    {
        int applied = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || !mat.HasProperty("_BaseMap")) continue;

            // 半透明のデカールは対象外。汚しは別の仕組みで作っている
            if (mat.renderQueue >= 3000) continue;
            // 幽霊・人影は演出用マテリアルなので触らない
            var lower = mat.name.ToLowerInvariant();
            if (lower.Contains("ghost") || lower.Contains("shadowfigure")) continue;
            // キャラクターの布と肌は自前のテクスチャのままにする
            // （ambientCG に人体向けの素材が無く、置き換えると悪化する）
            if (lower.StartsWith("char_")) continue;

            var match = Mapping.FirstOrDefault(m => m.keywords.Any(k => lower.Contains(k)));
            if (match.asset == null) continue;
            if (!sets.TryGetValue(match.asset, out var set)) continue;

            // タイリングは既存値に倍率を掛ける。既存値は UV の張り方を反映しているので
            // 捨てずに使い、実寸差だけ倍率で補正する
            var scale = mat.GetTextureScale("_BaseMap") * match.tilingMul;
            var offset = mat.GetTextureOffset("_BaseMap");

            mat.SetTexture("_BaseMap", set.color);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", set.color);
            mat.SetTexture("_BumpMap", set.normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");

            if (set.metallicSmoothness != null)
            {
                mat.SetTexture("_MetallicGlossMap", set.metallicSmoothness);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                // 滑らかさは金属マップのアルファから読む（0 = Metallic Alpha）
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_Metallic", match.metallicScale);
            }
            if (set.occlusion != null)
            {
                mat.SetTexture("_OcclusionMap", set.occlusion);
                mat.SetFloat("_OcclusionStrength", 0.85f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }

            // テクスチャが色を持つので _BaseColor は白にして素通しにする
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

            foreach (var prop in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" })
            {
                if (!mat.HasProperty(prop)) continue;
                mat.SetTextureScale(prop, scale);
                mat.SetTextureOffset(prop, offset);
            }

            EditorUtility.SetDirty(mat);
            applied++;
            log.AppendLine($"  {mat.name} ← {match.asset}");
        }
        return applied;
    }
}
