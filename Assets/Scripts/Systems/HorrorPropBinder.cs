using UnityEngine;

/// <summary>
/// シーン内の鏡・写真を、常駐している `HorrorEventSystem` に渡す。
///
/// `HorrorEventSystem` は `DontDestroyOnLoad` で生き続けるが、鏡と写真は
/// シーンの中にある。フロアを移るとシーン側だけが入れ替わるので、
/// **常駐側が持っている参照は前のフロアの、既に壊された物を指す**ことになる。
/// シーンごとに置いたこの部品が、開くたびに繋ぎ直す。
///
/// もともとは参照が最初から空で、鏡も写真もシーンに存在せず、
/// 演出が発火しても何も起きていなかった（`M16ReferenceAudit` で判明）。
/// </summary>
public class HorrorPropBinder : MonoBehaviour
{
    [Header("鏡")]
    public Renderer mirrorRenderer;
    public Material mirrorNormalMat;
    public Material mirrorDelayMat;
    public Material mirrorChangeMat;

    [Header("写真")]
    public SpriteRenderer photoRenderer;
    public Sprite[] photoVariants;

    void Start() => Bind();

    void Bind()
    {
        var horror = HorrorEventSystem.Instance;
        if (horror == null)
        {
            // 常駐システムより先に走ることがあるので、次のフレームで試し直す
            Invoke(nameof(Bind), 0.2f);
            return;
        }

        horror.mirrorRenderer = mirrorRenderer;
        horror.mirrorNormalMat = mirrorNormalMat;
        horror.mirrorDelayMat = mirrorDelayMat;
        horror.mirrorChangeMat = mirrorChangeMat;

        horror.photoRenderer = photoRenderer;
        if (photoVariants != null && photoVariants.Length > 0)
            horror.photoVariants = photoVariants;

        // 暗い部屋に現れる人物。人影のモデルを使う。
        // インスペクタ結線だとプレハブ作り直しで消えるので、名前で読む
        if (horror.npcPrefab == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/HorrorFigure");
            if (prefab != null) horror.npcPrefab = prefab;
        }
    }
}
