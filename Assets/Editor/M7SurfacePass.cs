using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// M7: 質感を与える。
///
/// 監査して分かったこと:
///   - キャラクターのマテリアルはテクスチャが1枚も無い単色べた塗りだった。
///     滑らかなプラスチックに見えていた直接の原因。
///   - Concrete / Plaster 系にノーマルマップが無く、面が平ら。
///   - Prop_ExtMetal は金属度 0.85 なのにラフネスの変化が無く、
///     全面が同じように光って安っぽい。
///   - 一方 Hospital Horror Pack のマテリアルは既にノーマル・金属光沢マップ付き。
///     ここは触らない（上書きすると作者の作った質感を壊す）。
///
/// テクスチャは tools/gen_surfaces.py で生成。
/// アルベドのアルファに滑らかさを入れているので `_SmoothnessTextureChannel = 1`
/// を指定して読ませる。金属光沢マップを別に用意しなくて済む。
///
/// 色は各マテリアルの _BaseColor に任せ、テクスチャはほぼ白の微細変化にしてある。
/// だからナース服・警備服・患者衣で同じ布テクスチャを共用できる。
/// </summary>
public static class M7SurfacePass
{
    const string TexDir = "Assets/Textures/Surfaces";
    const string PropMatDir = "Assets/Materials/Props";

    /// <summary>質感の種類ごとの設定。</summary>
    struct Surface
    {
        public string albedo;      // アルファに滑らかさが入っている
        public string normal;
        public float tiling;       // UV 1 あたりの繰り返し数
        public float bumpScale;
        public float metallic;
    }

    static readonly Dictionary<string, Surface> Surfaces = new()
    {
        ["fabric"] = new Surface { albedo = "fabric_albedo", normal = "fabric_N",
                                    tiling = 8f, bumpScale = 1.0f, metallic = 0f },
        ["skin"] = new Surface { albedo = "skin_albedo", normal = "skin_N",
                                  tiling = 4f, bumpScale = 0.6f, metallic = 0f },
        ["concrete"] = new Surface { albedo = "concrete_albedo", normal = "concrete_N",
                                      tiling = 2.5f, bumpScale = 1.3f, metallic = 0f },
        ["plaster"] = new Surface { albedo = "plaster_albedo", normal = "plaster_N",
                                     tiling = 2f, bumpScale = 1.0f, metallic = 0f },
        ["ceiling"] = new Surface { albedo = "ceiling_albedo", normal = "ceiling_N",
                                     tiling = 3f, bumpScale = 1.0f, metallic = 0f },
        ["metal"] = new Surface { albedo = "metal_albedo", normal = "metal_N",
                                   tiling = 3f, bumpScale = 0.9f, metallic = 0.6f },
    };

    [MenuItem("消灯/M7: 質感を割り当てる")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M7] 質感の割り当て\n");

        ConfigureTextureImporters(log);
        int filled = AssignMissingSurfaces(log);
        CreatePropMaterials(log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine($"  マテリアル {filled} 件に質感を付与");
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// ノーマルマップは必ず TextureImporterType.NormalMap にすること。
    /// カラーテクスチャとして読み込むと陰影が壊れる（見た目は「なんとなく変」で、
    /// 原因に気づきにくい）。
    /// アルベドはアルファに滑らかさが入っているので、透過扱いにさせない。
    /// </summary>
    static void ConfigureTextureImporters(StringBuilder log)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool isNormal = System.IO.Path.GetFileNameWithoutExtension(path).EndsWith("_N");
            bool changed = false;

            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                changed = true;
            }
            if (!isNormal)
            {
                if (importer.textureType != TextureImporterType.Default)
                {
                    importer.textureType = TextureImporterType.Default;
                    changed = true;
                }
                if (importer.alphaIsTransparency)
                {
                    // アルファは滑らかさ。透過として扱われると縁が滲む
                    importer.alphaIsTransparency = false;
                    changed = true;
                }
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
                {
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    changed = true;
                }
            }
            if (importer.wrapMode != TextureWrapMode.Repeat)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                log.AppendLine($"  インポート設定: {System.IO.Path.GetFileName(path)}" +
                                (isNormal ? " → NormalMap" : ""));
            }
        }
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// ノーマルマップを持たないマテリアルに、名前から推定した質感を割り当てる。
    /// 既にマップがあるもの（Hospital Horror Pack など）は触らない。
    /// </summary>
    static int AssignMissingSurfaces(StringBuilder log)
    {
        int count = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Materials" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || !mat.HasProperty("_BumpMap")) continue;

            // 既にノーマルマップがあるものは作者の意図なので尊重する
            if (mat.GetTexture("_BumpMap") != null) continue;
            // 半透明のデカールは対象外（別途 gen_decals.py で作っている）
            if (mat.renderQueue >= 3000) continue;

            var key = ClassifySurface(mat.name);
            if (key == null) continue;

            Apply(mat, Surfaces[key], keepBaseColor: true);
            log.AppendLine($"  {mat.name} ← {key}");
            count++;
        }
        return count;
    }

    static string ClassifySurface(string name)
    {
        var n = name.ToLowerInvariant();

        if (n.Contains("face") || n.Contains("skin")) return "skin";
        if (n.Contains("uniform") || n.Contains("gown") || n.Contains("coat") ||
            n.Contains("pants") || n.Contains("bedding") || n.Contains("sheet")) return "fabric";
        if (n.Contains("concrete")) return "concrete";
        if (n.Contains("ceiling") || n.Contains("ceil")) return "ceiling";
        if (n.Contains("plaster") || n.Contains("wall")) return "plaster";
        if (n.Contains("metal") || n.Contains("tube") || n.Contains("pipe") ||
            n.Contains("ext") || n.Contains("frame")) return "metal";

        // 幽霊・人影は演出用の特別なマテリアルなので触らない
        return null;
    }

    static void Apply(Material mat, Surface s, bool keepBaseColor)
    {
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{s.albedo}.png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{s.normal}.png");
        if (albedo == null || normal == null)
        {
            Debug.LogWarning($"[M7] {s.albedo} / {s.normal} が読めない");
            return;
        }

        mat.SetTexture("_BaseMap", albedo);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
        mat.SetTextureScale("_BaseMap", new Vector2(s.tiling, s.tiling));

        mat.SetTexture("_BumpMap", normal);
        mat.SetTextureScale("_BumpMap", new Vector2(s.tiling, s.tiling));
        mat.SetFloat("_BumpScale", s.bumpScale);
        mat.EnableKeyword("_NORMALMAP");

        // アルベドのアルファを滑らかさとして読む
        mat.SetFloat("_SmoothnessTextureChannel", 1f);
        mat.SetFloat("_Smoothness", 1f);
        mat.SetFloat("_Metallic", s.metallic);

        if (!keepBaseColor) mat.SetColor("_BaseColor", Color.white);

        EditorUtility.SetDirty(mat);
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// 廊下の設備に個別のマテリアルを作る。
    /// 配管もラジエーターも案内表示も同じ金属マテリアルを共用していたので、
    /// 全部同じ材質に見えていた。
    /// </summary>
    static void CreatePropMaterials(StringBuilder log)
    {
        var defs = new (string name, Color color, float metallic, string surface, float smoothness)[]
        {
            // 塗装された配管。水色がかった白は病院設備の定番
            ("Prop_Pipe_Painted",  new Color(0.62f, 0.66f, 0.68f), 0.45f, "metal", 1f),
            // 亜鉛メッキの換気口。地金が見えるので金属度は高め
            ("Prop_Vent_Galv",     new Color(0.55f, 0.57f, 0.60f), 0.75f, "metal", 1f),
            // 案内表示。塗装板なので金属度はほぼ無し
            ("Prop_Sign_Plate",    new Color(0.70f, 0.68f, 0.62f), 0.10f, "metal", 1f),
            // ラジエーター。琺瑯塗装で少しだけ光る
            ("Prop_Radiator_Enamel", new Color(0.72f, 0.71f, 0.68f), 0.25f, "metal", 1f),
        };

        if (!AssetDatabase.IsValidFolder(PropMatDir))
            AssetDatabase.CreateFolder("Assets/Materials", "Props");

        foreach (var (name, color, metallic, surfaceKey, smoothness) in defs)
        {
            var path = $"{PropMatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
                log.AppendLine($"  {name} を新規作成");
            }

            var s = Surfaces[surfaceKey];
            s.metallic = metallic;
            Apply(mat, s, keepBaseColor: false);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
        }
    }
}
