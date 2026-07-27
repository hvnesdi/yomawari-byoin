using UnityEngine;

/// Swaps the enemy body renderers between a "guard" and a "shadow figure"
/// material based on the local player's hallucination level.
///
///   level   0-30   : everyone sees the guard (gray uniform, skin face)
///   level  30-60   : interpolates blackening tint via _BaseColor on the
///                    guard material instance, no full swap
///   level   60-100 : full shadow silhouette (matte black, emissive rim)
public class EnemyAppearanceController : MonoBehaviour
{
    [Header("Materials")]
    public Material guardMaterial;
    public Material shadowMaterial;

    [Header("Renderers to swap")]
    public Renderer[] bodyRenderers;

    [Header("Model swap (optional)")]
    [Tooltip("通常時のモデル。未設定ならマテリアルの差し替えだけ行う")]
    public GameObject guardVisual;
    [Tooltip("幻覚レベルが高いときのモデル。細く引き伸ばした人影")]
    public GameObject shadowVisual;

    [Header("Hallucination link")]
    public string playerID = "local";
    [Range(0f, 100f)] public float blendStart = 30f;
    [Range(0f, 100f)] public float blendEnd   = 60f;

    [Header("Debug override - used by Editor for screenshots")]
    public bool overrideForScreenshot = false;
    public bool forceShadow = false;

    Material[] _runtimeMats;

    void Awake()
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        float level;
        if (overrideForScreenshot)
            level = forceShadow ? 100f : 0f;
        else if (HallucinationSystem.Instance != null)
            level = HallucinationSystem.Instance.GetLevel(playerID);
        else
            return;

        bool shadow = level >= blendEnd;

        // モデルが2種類用意されている場合は、体型ごと差し替える。
        // 色だけ黒くするより「別のものになった」感が出る（CLAUDE.md: 黒く歪んだ人影）
        if (guardVisual != null && shadowVisual != null)
        {
            if (guardVisual.activeSelf == shadow)  guardVisual.SetActive(!shadow);
            if (shadowVisual.activeSelf != shadow) shadowVisual.SetActive(shadow);
        }

        Material target = shadow ? shadowMaterial : guardMaterial;
        if (target == null) return;

        foreach (var r in bodyRenderers)
        {
            if (r == null) continue;
            if (r.sharedMaterial != target) r.sharedMaterial = target;
        }
    }
}
