using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// M5: 壁と床の汚しを増やす。
///
/// 照明とポストプロセスで雰囲気は出たが、壁そのものは新品同様のままだった。
/// 1990年代に閉鎖された病院という設定に対して、面が綺麗すぎるのが
/// 「作りかけ」に見える最大の要因になっている。
///
/// 既存のデカール（血・カビ・水染み・引っかき傷）はフロアあたり数枚しか
/// 置かれていなかったので、壁面に沿って大幅に増やす。
///
/// 置き方の方針:
///   - 水染みは天井際から垂れる（上から下へ伸びる）
///   - カビは床際と隅に溜まる
///   - 引っかき傷は手が届く高さ
///   - 血は稀に、3F と地下だけ
/// 位置は壁の座標から決定的に決める。実行ごとに変わるとシーン差分が汚れるため。
/// </summary>
public static class M5GrimePass
{
    const string DecalMatDir = "Assets/Materials/Decals";
    const string MarkerName = "__GrimeApplied";
    const string RootName = "Grime";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    /// <summary>フロアが下がるほど荒れる。壁1枚あたりの汚しの期待枚数。</summary>
    static float GrimeDensity(string path)
    {
        if (path.EndsWith("HospitalBasement.unity")) return 1.35f;
        if (path.EndsWith("Hospital3F.unity")) return 1.05f;
        if (path.EndsWith("Hospital2F.unity")) return 0.70f;
        return 0.40f;
    }

    static bool AllowBlood(string path)
        => path.EndsWith("Hospital3F.unity") || path.EndsWith("HospitalBasement.unity");

    /// <summary>
    /// デカールのマテリアルを正しい半透明設定に直す。
    ///
    /// 元の状態は `_Blend: 0`（アルファブレンド）と書いてあるのに、実際の係数が
    /// `_SrcBlend: 1`（One）のままだった。これは乗算済みアルファの設定で、
    /// テクスチャの透明部分が白い（＝乗算済みでない）ため、透明であるべき箇所に
    /// 白が加算されて「壁に貼られた白い板」になっていた。
    ///
    /// マテリアルをコードで作るときに Surface/Blend のプロパティだけ書き換えて、
    /// ブレンド係数とキーワードの再設定を呼んでいないと、この状態になる。
    /// </summary>
    static void FixDecalBlending(Dictionary<string, Material> mats)
    {
        foreach (var mat in mats.Values)
        {
            mat.SetFloat("_Surface", 1f);        // Transparent
            mat.SetFloat("_Blend", 0f);          // Alpha
            mat.SetFloat("_AlphaClip", 0f);
            // URP の _SrcBlend / _DstBlend / _ZWrite は Float プロパティなので
            // SetFloat で書くこと。SetInt は Integer プロパティ用で、ここでは
            // 何も起きずに素通りする（実際 _SrcBlend が 1 のまま残って白く抜けた）。
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);

            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EditorUtility.SetDirty(mat);
        }
        Debug.Log($"[Grime] デカール {mats.Count} 種のブレンド設定を修正");
    }

    /// <summary>
    /// 引っかき傷のマテリアルが存在せず、配置が無言でスキップされていたので作る。
    /// </summary>
    static void EnsureScratchMaterial(Dictionary<string, Material> mats)
    {
        const string name = "Decal_Scratch_01";
        if (mats.ContainsKey(name)) return;

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/Decals/decal_scratch_01.png");
        if (tex == null) { Debug.LogWarning("[Grime] decal_scratch_01.png が無い"); return; }

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(0.78f, 0.78f, 0.78f, 1f));
        mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, $"{DecalMatDir}/{name}.mat");
        mats[name] = mat;
        Debug.Log($"[Grime] {name} を新規作成");
    }

    [MenuItem("消灯/M5: 壁の汚しを増やす")]
    public static void RunBatch()
    {
        var mats = LoadDecalMaterials();
        EnsureScratchMaterial(mats);
        FixDecalBlending(mats);
        if (mats.Count == 0)
        {
            Debug.LogError("[Grime] デカールのマテリアルが見つからない");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            // 作り直し方式にする。マーカーで飛ばすと、後から種類を足したときに
            // 反映されないまま気づけない（引っかき傷がまさにそれだった）。
            var old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var oldMarker = GameObject.Find(MarkerName);
            if (oldMarker != null) Object.DestroyImmediate(oldMarker);

            var root = new GameObject(RootName).transform;
            int placed = PlaceGrime(path, root, mats);

            new GameObject(MarkerName);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Grime] {label}: 汚し {placed} 枚を配置");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[Grime] 完了");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    static Dictionary<string, Material> LoadDecalMaterials()
    {
        var result = new Dictionary<string, Material>();
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { DecalMatDir }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null) result[m.name] = m;
        }
        return result;
    }

    static int PlaceGrime(string scenePath, Transform root, Dictionary<string, Material> mats)
    {
        // 壁パネルを集める。厚みの薄い軸が法線方向
        var walls = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                          .Where(r => r.sharedMaterial != null &&
                                      (r.sharedMaterial.name.StartsWith("Mat_Walllime") ||
                                       r.sharedMaterial.name.StartsWith("Mat_Tile")))
                          .Where(r => r.bounds.size.y > 1.0f)     // 床・天井を除外
                          .ToList();

        float density = GrimeDensity(scenePath);
        bool blood = AllowBlood(scenePath);
        int placed = 0;

        foreach (var wall in walls)
        {
            var b = wall.bounds;

            // 薄い軸＝壁の法線
            Vector3 normal;
            if (b.size.x <= b.size.z) normal = Vector3.right;
            else normal = Vector3.forward;

            // 壁面に沿った横方向
            Vector3 tangent = normal == Vector3.right ? Vector3.forward : Vector3.right;
            float width = normal == Vector3.right ? b.size.z : b.size.x;
            if (width < 0.5f) continue;

            // この壁に何枚置くかを座標から決定的に決める
            float h1 = Hash(b.center, 1.7f);
            int count = Mathf.FloorToInt(density + h1);
            for (int i = 0; i < count; i++)
            {
                float hu = Hash(b.center, 3.1f + i * 2.3f);
                float hv = Hash(b.center, 5.9f + i * 1.7f);
                float hk = Hash(b.center, 9.3f + i * 3.7f);

                // 種類ごとに付く高さが違う。ここを揃えると嘘くさくなる
                string key;
                float y, w, hgt;
                if (hk < 0.34f)
                {
                    // 天井際から垂れる水染み。
                    // テクスチャは tools/gen_decals.py で生成し直したもの。
                    // 元のものは白背景＋薄いベージュの筋で、暗く着色しても
                    // 壁に白い板が貼られたようにしか見えなかった。
                    key = "Decal_Water_01";
                    y = b.max.y - 0.30f - hv * 0.9f;
                    w = 0.75f + hu * 0.9f; hgt = 1.3f + hv * 1.2f;
                }
                else if (hk < 0.70f)
                {
                    key = hv < 0.5f ? "Decal_Mold_01" : "Decal_Mold_02";   // 床際に溜まる
                    y = b.min.y + 0.25f + hv * 0.6f;
                    w = 0.8f + hu * 1.4f; hgt = 0.6f + hv * 0.8f;
                }
                else if (hk < 0.90f || !blood)
                {
                    key = "Decal_Scratch_01";                     // 手が届く高さ
                    y = b.min.y + 0.9f + hv * 0.6f;
                    w = 0.5f + hu * 0.7f; hgt = 0.35f + hv * 0.4f;
                }
                else
                {
                    key = hv < 0.5f ? "Decal_Blood_01" : "Decal_Blood_02";
                    y = b.min.y + 0.4f + hv * 1.0f;
                    w = 0.6f + hu * 0.8f; hgt = 0.7f + hv * 0.9f;
                }

                if (!mats.TryGetValue(key, out var mat)) continue;

                // 壁の内側にわずかに浮かせる。同一平面だとちらつく
                float along = (hu - 0.5f) * (width - w);
                var pos = new Vector3(b.center.x, y, b.center.z) + tangent * along;

                // 廊下側がどちらかは分からないので両面に置く。裏は壁で隠れる
                for (int s = -1; s <= 1; s += 2)
                {
                    var offset = normal * (s * (b.size[normal == Vector3.right ? 0 : 2] * 0.5f + 0.012f));
                    var go = MakeDecalQuad($"{key}_{placed}_{s}", mat, pos + offset,
                                           Quaternion.LookRotation(-normal * s), new Vector2(w, hgt), root);
                    placed++;
                }
            }
        }
        return placed;
    }

    static GameObject MakeDecalQuad(string name, Material mat, Vector3 pos, Quaternion rot,
                                     Vector2 size, Transform parent)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.position = pos;
        q.transform.rotation = rot;
        q.transform.localScale = new Vector3(size.x, size.y, 1f);

        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        // 透明な板が影を落とすと矩形の影が出るので必ず切る
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q;
    }

    /// <summary>座標から 0..1 の決定的な値を作る。</summary>
    static float Hash(Vector3 p, float salt)
    {
        float v = Mathf.Sin(p.x * 12.9898f + p.y * 4.1414f + p.z * 78.233f + salt) * 43758.5453f;
        return Mathf.Abs(v - Mathf.Floor(v));
    }
}
