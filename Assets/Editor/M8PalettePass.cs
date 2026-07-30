using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// M8: 色を現実の材料に寄せる。
///
/// 直前までの状態には2つの色の問題があった。
///
/// 1. アルベドを一律 0.78 にクランプしたとき、テクスチャで色を持つマテリアルまで
///    22% 暗くしてしまった。テクスチャが色を運んでいる場合、_BaseColor は
///    「白＝そのまま出す」が正しい。1.0 を超えていたものを直すのが目的だったのに、
///    ちょうど 1.0 だったものまで巻き込んでいた。
/// 2. 全マテリアルが灰色〜ベージュに寄っていて、材料の差が無い。
///    実際の病院は白い陶器タイル、緑の腰壁、クリーム色の漆喰、濃い木の扉、
///    鋼の金具と、はっきり色と反射が違う。この差が無いと現実に見えない。
///
/// なので:
///   - テクスチャを持つマテリアル → _BaseColor は白に戻す（テクスチャの色を出す）
///   - 自前の質感テクスチャ（ほぼ白）を使うもの → 材料ごとの実際の色を与える
/// </summary>
public static class M8PalettePass
{
    /// <summary>材料ごとの色と反射。実在の病院の材料を想定した値。</summary>
    struct Look
    {
        public Color color;
        public float smoothness;   // -1 ならテクスチャのアルファに任せる
        public float metallic;
    }

    static readonly (string[] keys, Look look)[] Palette =
    {
        // ── 布 ──
        (new[] { "nurseuniform" },  new Look { color = new Color(0.90f, 0.91f, 0.89f), smoothness = -1f, metallic = 0f }),
        (new[] { "doctorcoat" },    new Look { color = new Color(0.92f, 0.92f, 0.90f), smoothness = -1f, metallic = 0f }),
        (new[] { "patientgown" },   new Look { color = new Color(0.70f, 0.77f, 0.80f), smoothness = -1f, metallic = 0f }),
        (new[] { "guarduniform" },  new Look { color = new Color(0.26f, 0.28f, 0.33f), smoothness = -1f, metallic = 0f }),
        (new[] { "doctorpants" },   new Look { color = new Color(0.33f, 0.34f, 0.38f), smoothness = -1f, metallic = 0f }),
        (new[] { "bedding", "sheet" }, new Look { color = new Color(0.84f, 0.84f, 0.81f), smoothness = -1f, metallic = 0f }),

        // ── 肌 ──
        (new[] { "face", "skin" },  new Look { color = new Color(0.76f, 0.61f, 0.52f), smoothness = -1f, metallic = 0f }),

        // ── 建材 ──
        (new[] { "concrete" },      new Look { color = new Color(0.56f, 0.56f, 0.54f), smoothness = -1f, metallic = 0f }),
        (new[] { "plaster", "corridor_wall", "patientroom_wall", "patientwall", "hospitalwall" },
                                    new Look { color = new Color(0.79f, 0.77f, 0.71f), smoothness = -1f, metallic = 0f }),
        (new[] { "ceiling", "ceil" }, new Look { color = new Color(0.83f, 0.82f, 0.79f), smoothness = -1f, metallic = 0f }),
        (new[] { "linoleum", "hospitalfloor" },
                                    new Look { color = new Color(0.54f, 0.56f, 0.51f), smoothness = -1f, metallic = 0f }),
        (new[] { "padded" },        new Look { color = new Color(0.68f, 0.66f, 0.60f), smoothness = -1f, metallic = 0f }),
        (new[] { "isolation" },     new Look { color = new Color(0.72f, 0.71f, 0.66f), smoothness = -1f, metallic = 0f }),
        (new[] { "wall_green" },    new Look { color = new Color(0.40f, 0.44f, 0.33f), smoothness = -1f, metallic = 0f }),

        // ── 金属 ──
        (new[] { "bedmetal", "doormetal", "ivmetal", "metal_base", "wc_metal", "stretchermetal" },
                                    new Look { color = new Color(0.56f, 0.57f, 0.59f), smoothness = -1f, metallic = 0.85f }),
        (new[] { "fluortube" },     new Look { color = new Color(0.88f, 0.90f, 0.93f), smoothness = -1f, metallic = 0.1f }),

        // ── 木・その他 ──
        (new[] { "wood", "doorwood", "win_wood" },
                                    new Look { color = new Color(0.27f, 0.18f, 0.12f), smoothness = -1f, metallic = 0f }),
        (new[] { "extred" },        new Look { color = new Color(0.42f, 0.06f, 0.05f), smoothness = -1f, metallic = 0.2f }),
        (new[] { "paperflat" },     new Look { color = new Color(0.80f, 0.79f, 0.74f), smoothness = -1f, metallic = 0f }),
    };

    [MenuItem("消灯/M8: 色を材料に合わせる")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M8] 色の割り当て\n");
        int restored = 0, tinted = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:Material"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || !mat.HasProperty("_BaseColor")) continue;

            // 半透明のデカールは gen_decals.py 側で色を持たせているので触らない
            if (mat.renderQueue >= 3000) continue;

            bool isPackMaterial = path.Contains("/Dnk_Dev/");
            bool hasOwnTexture = mat.GetTexture("_BaseMap") != null &&
                                 !IsGeneratedSurface(mat.GetTexture("_BaseMap"));

            if (isPackMaterial || hasOwnTexture)
            {
                // テクスチャが色を運んでいる。_BaseColor は白にして素通しにする。
                // 以前 0.78 にクランプして 22% 暗くしてしまったのを戻す
                var current = mat.GetColor("_BaseColor");
                if (current.maxColorComponent < 0.99f)
                {
                    mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, current.a));
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(1f, 1f, 1f, current.a));
                    EditorUtility.SetDirty(mat);
                    restored++;
                    log.AppendLine($"  白に戻す: {mat.name} (was {current.r:F2})");
                }
                continue;
            }

            // 自前の質感テクスチャ（ほぼ白）を使うもの。材料の色を与える
            var look = Classify(mat.name);
            if (look == null) continue;

            mat.SetColor("_BaseColor", look.Value.color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", look.Value.color);
            mat.SetFloat("_Metallic", look.Value.metallic);
            if (look.Value.smoothness >= 0f) mat.SetFloat("_Smoothness", look.Value.smoothness);
            EditorUtility.SetDirty(mat);
            tinted++;
            log.AppendLine($"  {mat.name} ← RGB({look.Value.color.r:F2},{look.Value.color.g:F2},{look.Value.color.b:F2}) " +
                            $"metallic={look.Value.metallic:F2}");
        }

        AssetDatabase.SaveAssets();
        log.AppendLine($"  白に戻した {restored} 件 / 色を与えた {tinted} 件");
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>自前生成の質感テクスチャかどうか。これらはほぼ白なので色は _BaseColor で決める。</summary>
    static bool IsGeneratedSurface(Texture tex)
    {
        var p = AssetDatabase.GetAssetPath(tex);
        return p.StartsWith("Assets/Textures/Surfaces/");
    }

    static Look? Classify(string name)
    {
        var n = name.ToLowerInvariant();
        foreach (var (keys, look) in Palette)
            foreach (var k in keys)
                if (n.Contains(k)) return look;
        return null;
    }
}
