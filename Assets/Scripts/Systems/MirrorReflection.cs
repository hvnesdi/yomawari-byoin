using UnityEngine;

/// <summary>
/// 鏡。実際に映す。
///
/// `HorrorEventSystem` には鏡の演出が3つ実装されていたが、
/// `mirrorRenderer` も3つのマテリアルも未設定で、**発火しても何も起きなかった**。
///
/// 絵を貼った板ではなく、鏡像を描くカメラで作る。
/// 貼り絵だと、近づいた瞬間に嘘だと分かって全部が台無しになる。
/// 映っているからこそ「映り方がおかしい」演出が効く。
///
/// 3つの状態は `HorrorEventSystem` がマテリアルを差し替えて指示してくる。
/// こちらはどのマテリアルが今当たっているかを見て振る舞いを変える
/// （あちらの実装には手を入れない）:
///   normal … そのまま映す
///   delay  … **カメラを止める**。動いても鏡の中が付いてこない
///   change … 自分の映りが黒い人影に変わる
///
/// 「遅れ」をカメラの停止で作れるのが、実際に映していることの効き目。
/// 止めれば前のフレームが残り続けるので、遅れそのものが絵になる。
/// </summary>
[RequireComponent(typeof(Renderer))]
public class MirrorReflection : MonoBehaviour
{
    /// <summary>鏡像テクスチャの解像度。鏡は小さく映るので控えめでよい。</summary>
    const int Resolution = 512;

    /// <summary>この距離より遠ければ描かない。常時描くと重い。</summary>
    const float ActiveDistance = 14f;

    [Tooltip("HorrorEventSystem が差し替えるマテリアル。振る舞いの判定に使う")]
    public Material normalMaterial;
    public Material delayMaterial;
    public Material changeMaterial;

    [Tooltip("鏡にだけ映る自分の体。main camera からは除外する")]
    public GameObject mirrorBody;
    public Material bodyNormalMaterial;
    public Material bodyShadowMaterial;

    /// <summary>
    /// 鏡面が向いている方向（部屋の側）。
    ///
    /// **Unity の Quad は -Z 側から見える。** transform.forward をそのまま
    /// 法線として使っていたら、鏡が壁の内側を向いて廊下からは見えなかった
    /// （撮ってみたら暗い壁しか写っていなかった）。
    /// 板の見える面と鏡面の法線は一致していなければならないので、ここで一本化する。
    /// </summary>
    public Vector3 SurfaceNormal => -transform.forward;

    Renderer surface;
    Camera reflectionCamera;
    RenderTexture target;
    Transform player;

    void Awake()
    {
        surface = GetComponent<Renderer>();
    }

    void Start()
    {
        var main = Camera.main;
        if (main == null) return;
        player = main.transform;

        target = new RenderTexture(Resolution, Resolution, 16, RenderTextureFormat.DefaultHDR)
        {
            name = "MirrorReflection",
            antiAliasing = 2,
        };

        var go = new GameObject("MirrorCamera");
        go.transform.SetParent(transform, false);
        reflectionCamera = go.AddComponent<Camera>();
        reflectionCamera.CopyFrom(main);
        reflectionCamera.targetTexture = target;

        // **空を映さない。**
        // CopyFrom で本カメラの設定（Skybox でクリア）まで引き継いでいたので、
        // 何も無い方向が既定の青空で埋まり、窓の無い廊下の鏡に空が映っていた。
        // 映る物が無いところは暗いままにする。
        reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
        reflectionCamera.backgroundColor = new Color(0.012f, 0.012f, 0.016f, 1f);
        // 本カメラより先に描かせる
        reflectionCamera.depth = main.depth - 1;
        reflectionCamera.cullingMask = main.cullingMask | MirrorLayerMask();

        // 鏡自身のレイヤーを除外してはいけない。
        // 「鏡の中に鏡を映さない」つもりで自分のレイヤーを外していたが、
        // 鏡は Default レイヤーに居るので**廊下ごと全部消えていた**
        // （鏡の中が真っ黒だったのはこれが原因）。
        // 鏡面はカメラの真後ろ（ニアクリップ面上）にあるので、そもそも描かれない。

        // 反射像にも本編と同じ色調整を掛ける。
        // 掛けないと、鏡の中だけトーンマップ前の暗い画になり、
        // 同じ場所なのに明るさが違って見える
        var reflectionData = go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        reflectionData.renderPostProcessing = true;
        reflectionData.renderShadows = true;

        // 鏡像テクスチャを3つのマテリアル全部に配る。
        // どれが当たっていても「映っている」状態を保つため
        foreach (var mat in new[] { normalMaterial, delayMaterial, changeMaterial })
        {
            if (mat == null) continue;
            mat.SetTexture("_BaseMap", target);
            // 左右を反転する。カメラを鏡像位置に置くだけでは像が裏返らないので、
            // ここで UV を反転して鏡らしくする
            mat.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
            mat.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
        }

        if (mirrorBody != null) mirrorBody.layer = MirrorLayer();
    }

    static int MirrorLayer()
    {
        int layer = LayerMask.NameToLayer("MirrorOnly");
        return layer < 0 ? 0 : layer;
    }

    static int MirrorLayerMask() => 1 << MirrorLayer();

    void LateUpdate()
    {
        if (reflectionCamera == null || player == null) return;

        // 遠いときは止める。廊下に鏡が並ぶと描画が積み上がる
        float distance = Vector3.Distance(player.position, transform.position);
        bool near = distance < ActiveDistance;

        var current = surface.sharedMaterial;
        bool frozen = delayMaterial != null && current == delayMaterial;

        // 「遅れ」はカメラを止めて作る。前のフレームが残り続ける
        reflectionCamera.enabled = near && !frozen;

        if (changeMaterial != null && current == changeMaterial) SetBody(bodyShadowMaterial);
        else SetBody(bodyNormalMaterial);

        if (!reflectionCamera.enabled) return;

        // 鏡面を挟んで本カメラの反対側に置き、反射した向きを向かせる
        var normal = SurfaceNormal;
        var toCamera = player.position - transform.position;
        var mirroredPosition = player.position - 2f * Vector3.Dot(toCamera, normal) * normal;

        var forward = Vector3.Reflect(player.forward, normal);
        var up = Vector3.Reflect(player.up, normal);

        reflectionCamera.transform.SetPositionAndRotation(
            mirroredPosition, Quaternion.LookRotation(forward, up));

        // 鏡より手前（壁の中）を描かない
        reflectionCamera.nearClipPlane = Mathf.Max(0.05f,
            Vector3.Dot(mirroredPosition - transform.position, -normal) - 0.1f);
    }

    void SetBody(Material material)
    {
        if (mirrorBody == null || material == null) return;
        foreach (var r in mirrorBody.GetComponentsInChildren<Renderer>(true))
        {
            if (r.sharedMaterial == material) continue;
            var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            r.sharedMaterials = slots;
        }
    }

    void OnDestroy()
    {
        if (target != null) target.Release();
    }
}
