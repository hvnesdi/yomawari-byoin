using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// M12: Poly Haven のスキャン小物にマテリアルを組む。
///
/// **M11 で71個配置した小物は、テクスチャが1枚も当たっていなかった。**
/// 地下のキャプチャを見て段ボールが「ベージュの箱」になっていることで気づいた。
/// Poly Haven の fbx は Unity 側でマテリアルを生成せず（.mat が1つも作られていない）、
/// 折れ目も汚れも入った 2K のテクスチャが使われないまま置かれていた。
/// 「スキャン素材だから情報量がある」はずが、実際には単色の箱だった。
///
/// 教訓: モデルを配置したら、テクスチャが当たっているかを画で確かめる。
/// 配置数のログは「置けたか」しか語らない。「見えているか」は別の話。
///
/// ここでやること:
///   1. textures/ 配下のファイルを diff / nor_gl / rough / metal に仕分ける
///   2. URP が要求する形に詰め直す（金属マップの RGB=金属・A=滑らかさ）
///   3. URP Lit のマテリアルを作る
///   4. fbx インポーターのマテリアル差し替え表に登録する
///
/// 4 が要点。シーンに置いたインスタンスを1つずつ書き換えるのではなく
/// インポーター側で差し替えるので、既に置いた分にも後から置く分にも効く。
/// </summary>
public static class M12ScannedPropMaterialsPass
{
    const string ModelDir = "Assets/Models/PolyHaven";

    /// <summary>
    /// テクスチャ名の末尾。Poly Haven は素材ごとに綴りが揺れるので両方見る
    /// （rough と roughness、metal と metallic が混在している）。
    /// 長い方を先に並べること。"rough" を先に見ると "roughness" が
    /// "roughness" ではなく "rough" + "ness" として扱われる。
    /// </summary>
    static readonly string[] MapSuffixes =
    {
        "nor_gl", "nor_dx", "roughness", "rough", "metallic", "metal", "diff", "ao", "arm", "disp",
    };

    class TexSet
    {
        public string setName = "";
        public Texture2D diff, normal, rough, metal;
    }

    [MenuItem("消灯/M12: スキャン小物のマテリアルを組む")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M12] スキャン小物のマテリアル\n");

        if (!AssetDatabase.IsValidFolder(ModelDir))
        {
            Debug.LogError($"[M12] {ModelDir} が無い");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        int built = 0, remapped = 0;

        foreach (var propDir in AssetDatabase.GetSubFolders(ModelDir))
        {
            var id = Path.GetFileName(propDir);
            var fbxPath = FindFbx(propDir);
            if (fbxPath == null) { log.AppendLine($"  ? {id}: fbx が無い"); continue; }

            var sets = CollectTextureSets(propDir, id, log);
            if (sets.Count == 0) { log.AppendLine($"  ? {id}: テクスチャが見つからない"); continue; }

            // 素材名はマテリアル名から逆算せず、そのまま持ち回す。
            // "PH_Barrel_02" から末尾を切り出すと素材名が "02" になってしまう
            var materials = new List<(Material mat, string setName)>();
            foreach (var set in sets)
            {
                var mat = BuildMaterial(propDir, id, set, log);
                if (mat != null) { materials.Add((mat, set.setName)); built++; }
            }
            if (materials.Count == 0) continue;

            remapped += Remap(fbxPath, id, materials, log);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine($"  マテリアル {built} 個 / 差し替え {remapped} スロット");
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static string FindFbx(string folder)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.EndsWith(".fbx")) return p;
        }
        return null;
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// textures/ を仕分ける。1つの小物が複数のマテリアルを持つことがある
    /// （CoffeeCart_01 は車体の "cart" と載っている物の "props" に分かれている）。
    /// ファイル名から素材名を取り出して束ねる。
    /// </summary>
    static List<TexSet> CollectTextureSets(string propDir, string id, StringBuilder log)
    {
        var texDir = $"{propDir}/textures";
        var result = new Dictionary<string, TexSet>();
        if (!AssetDatabase.IsValidFolder(texDir)) return new List<TexSet>();

        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var stem = Path.GetFileNameWithoutExtension(path);

            // 解像度の接尾辞を落とす（_2k / _4k / _1k）
            foreach (var res in new[] { "_1k", "_2k", "_4k", "_8k" })
                if (stem.EndsWith(res)) { stem = stem.Substring(0, stem.Length - res.Length); break; }

            string kind = null;
            foreach (var s in MapSuffixes)
            {
                if (!stem.EndsWith("_" + s)) continue;
                kind = s;
                stem = stem.Substring(0, stem.Length - s.Length - 1);
                break;
            }
            if (kind == null) continue;

            // 残った部分から小物名を除くと素材名になる（無ければ既定の1つ）
            var setName = stem.Length > id.Length ? stem.Substring(id.Length).Trim('_') : "";

            if (!result.TryGetValue(setName, out var set))
                result[setName] = set = new TexSet { setName = setName };

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            switch (kind)
            {
                case "diff":                      set.diff = tex; break;
                // Unity は OpenGL 規約。DX 版しか無い場合の保険で拾うが GL を優先する
                case "nor_gl":                    set.normal = tex; break;
                case "nor_dx": if (set.normal == null) set.normal = tex; break;
                case "rough": case "roughness":   set.rough = tex; break;
                case "metal": case "metallic":    set.metal = tex; break;
            }
        }

        var sets = result.Values.Where(s => s.diff != null).ToList();
        foreach (var s in sets)
            log.AppendLine($"  {id}{(s.setName == "" ? "" : "/" + s.setName)}: " +
                            $"diff{(s.normal ? "+normal" : "")}{(s.rough ? "+rough" : "")}{(s.metal ? "+metal" : "")}");
        return sets;
    }

    // ------------------------------------------------------------------
    static Material BuildMaterial(string propDir, string id, TexSet set, StringBuilder log)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) { log.AppendLine("  ? URP Lit シェーダが無い"); return null; }

        var name = set.setName == "" ? $"PH_{id}" : $"PH_{id}_{set.setName}";
        var matPath = $"{propDir}/{name}.mat";

        // 作り直しではなく、あれば使い回す。シーン側の参照を切らないため
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else if (mat.shader != shader) mat.shader = shader;

        ConfigureImporter(set.diff, TextureImporterType.Default, sRGB: true);
        ConfigureImporter(set.normal, TextureImporterType.NormalMap, sRGB: true);
        // 粗さ・金属は色ではなく数値。sRGB を切らないとガンマ補正で値がずれる
        ConfigureImporter(set.rough, TextureImporterType.Default, sRGB: false, readable: true);
        ConfigureImporter(set.metal, TextureImporterType.Default, sRGB: false, readable: true);

        mat.SetTexture("_BaseMap", set.diff);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", set.diff);
        // テクスチャが色を運ぶので _BaseColor は白で素通し
        mat.SetColor("_BaseColor", Color.white);

        if (set.normal != null)
        {
            mat.SetTexture("_BumpMap", set.normal);
            mat.SetFloat("_BumpScale", 1f);
            mat.EnableKeyword("_NORMALMAP");
        }

        // URP は金属と滑らかさを1枚に詰めた形しか受け取らない（RGB=金属 / A=滑らかさ）。
        // Poly Haven は別々に配るので、ここで詰め直す。
        var packed = PackMetallicSmoothness(propDir, name, set, log);
        if (packed != null)
        {
            mat.SetTexture("_MetallicGlossMap", packed);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.SetFloat("_SmoothnessTextureChannel", 0f);   // 0 = 金属マップのアルファ
            mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_Metallic", 1f);
        }
        else
        {
            // 詰め直せなかった場合の保険。金属でない小物として扱う
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.25f);
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void ConfigureImporter(Texture2D tex, TextureImporterType type, bool sRGB, bool readable = false)
    {
        if (tex == null) return;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
        if (importer == null) return;

        bool changed = false;
        if (importer.textureType != type) { importer.textureType = type; changed = true; }
        if (type != TextureImporterType.NormalMap && importer.sRGBTexture != sRGB)
        { importer.sRGBTexture = sRGB; changed = true; }
        if (readable && !importer.isReadable) { importer.isReadable = true; changed = true; }
        if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }

        if (changed) importer.SaveAndReimport();
    }

    /// <summary>
    /// 粗さと金属を1枚の RGBA に詰める。RGB に金属、A に滑らかさ(1-粗さ)。
    /// 金属マップが無い小物（段ボールなど）は金属 0 として詰める。
    /// </summary>
    static Texture2D PackMetallicSmoothness(string propDir, string name, TexSet set, StringBuilder log)
    {
        if (set.rough == null) return null;

        var outDir = $"{propDir}/generated";
        if (!AssetDatabase.IsValidFolder(outDir)) AssetDatabase.CreateFolder(propDir, "generated");
        var outPath = $"{outDir}/{name}_MS.png";

        // 既に作ってあれば作り直さない（実行のたびに書き換えると git が汚れる）
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        if (existing != null) return existing;

        Color[] roughPixels, metalPixels = null;
        try
        {
            roughPixels = set.rough.GetPixels();
            if (set.metal != null && set.metal.width == set.rough.width &&
                                     set.metal.height == set.rough.height)
                metalPixels = set.metal.GetPixels();
        }
        catch (UnityException e)
        {
            // isReadable を立てた直後は再インポートが済んでいないことがある
            log.AppendLine($"  ? {name}: テクスチャを読めない（{e.Message}）");
            return null;
        }

        var packed = new Texture2D(set.rough.width, set.rough.height, TextureFormat.RGBA32, false, true);
        var outPixels = new Color[roughPixels.Length];
        for (int i = 0; i < roughPixels.Length; i++)
        {
            float m = metalPixels != null ? metalPixels[i].r : 0f;
            outPixels[i] = new Color(m, m, m, 1f - roughPixels[i].r);
        }
        packed.SetPixels(outPixels);
        packed.Apply();

        File.WriteAllBytes(outPath, packed.EncodeToPNG());
        Object.DestroyImmediate(packed);
        AssetDatabase.ImportAsset(outPath);

        var importer = AssetImporter.GetAtPath(outPath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;       // 数値なのでガンマ補正をかけない
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        log.AppendLine($"  {name}_MS.png を生成（金属{(metalPixels != null ? "有" : "無")}）");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// fbx インポーターの差し替え表に登録する。
    /// シーン上のインスタンスを個別に書き換えるより確実で、
    /// これから置く分にも自動で効く。
    /// </summary>
    static int Remap(string fbxPath, string id, List<(Material mat, string setName)> materials, StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return 0;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (model == null) return 0;

        // **差し替え表の鍵から元のスロット名を取る。**
        //
        // モデルのレンダラーを読むと、2回目以降は差し替え後のマテリアル名が返る。
        // それを鍵にして登録すると、元の名前とは別の項目が毎回増えていくうえ、
        // CoffeeCart のように複数スロットが同じマテリアルに差し替わっていると
        // スロットが1つに見えて、割り当てを直す機会が失われる（実際に起きた）。
        // 差し替え表の鍵は常に fbx 側の元の名前なので、そこから復元する。
        var slotNames = importer.GetExternalObjectMap()
                                .Where(kv => kv.Key.type == typeof(Material))
                                .Select(kv => kv.Key.name)
                                .Distinct().ToList();

        // 以前の実装が差し替え後の名前を鍵にして登録してしまった分を掃除する。
        // fbx 側のスロットが "PH_" で始まることはない（"PH_" はここで作る名前の接頭辞）
        foreach (var stale in slotNames.Where(n => n.StartsWith("PH_")).ToList())
        {
            importer.RemoveRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), stale));
            slotNames.Remove(stale);
            log.AppendLine($"    {Path.GetFileName(fbxPath)} 不要な差し替え '{stale}' を削除");
        }

        if (slotNames.Count == 0)
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (m != null && !slotNames.Contains(m.name)) slotNames.Add(m.name);

        if (slotNames.Count == 0)
        {
            // 何が入っているのかを書く。「スロットが無い」だけでは
            // レンダラーが無いのか、レンダラーはあるがマテリアルが空なのか判らない
            int renderers = model.GetComponentsInChildren<MeshRenderer>(true).Length;
            int skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
            // alarm_clock_01 はリグ付きで書き出されており SkinnedMeshRenderer になる。
            // マテリアルスロットを持たないので、こちらからは手が出せない
            log.AppendLine($"  ? {Path.GetFileName(fbxPath)}: マテリアルスロットが無い " +
                            $"(MeshRenderer {renderers} / SkinnedMeshRenderer {skinned})" +
                            " → M11 側で直接あてる");
            return 0;
        }

        int count = 0;
        foreach (var slot in slotNames)
        {
            // スロット名から小物名を落としてから素材名と突き合わせる。
            //
            // そのまま比較して外した。CoffeeCart_01 は名前自体に "cart" を含むので、
            // スロット 'CoffeeCart_01_props' が素材 "cart" に一致してしまい、
            // 台車のテクスチャが載っている物にも貼られていた。
            // 部分一致で判定するときは、比較する前に共通の接頭辞を落とすこと。
            var tail = slot.StartsWith(id) && slot.Length > id.Length
                     ? slot.Substring(id.Length).Trim('_') : slot;
            var lower = tail.ToLowerInvariant();

            var hit = materials.FirstOrDefault(m => m.setName != "" &&
                                                    lower.Contains(m.setName.ToLowerInvariant()));
            bool fallback = hit.mat == null;
            var chosen = fallback ? materials[0].mat : hit.mat;

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), chosen);
            // 素材が足りずに流用したときはそう書く。黙って流用すると
            // 「全スロットに正しく当たった」ように読めてしまう
            log.AppendLine($"    {Path.GetFileName(fbxPath)} スロット '{slot}' ← {chosen.name}" +
                            (fallback && materials.Count > 1 ? "（該当素材が無いので流用）" : ""));
            count++;
        }

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        return count;
    }
}
