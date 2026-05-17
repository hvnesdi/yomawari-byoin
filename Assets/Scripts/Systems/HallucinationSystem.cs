using System.Collections.Generic;
using UnityEngine;
#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

/// <summary>
/// Per-player hallucination level manager (0-100).
/// CLAUDE.md rule: 幻覚レベルは常にプレイヤーごとに独立して管理する
/// </summary>
public class HallucinationSystem : MonoBehaviour
{
    public static HallucinationSystem Instance { get; private set; }

    // --- Level bands from CLAUDE.md ---
    // 0-30  : じわじわ系のみ・NPC普通に見える
    // 30-60 : 心理系追加・NPC幽霊っぽく見える
    // 60-80 : 全演出・NPC幽霊に見える
    // 80-100: 常時発生・バッドエンド加速

    [Header("Base rise rate (per minute, like CLAUDE.md +1/min)")]
    public float baseRisePerMinute = 1f;

    [Header("Post Processing")]
#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
    public Volume postProcessVolume;

    private Vignette vignette;
    private ChromaticAberration chromaticAberration;
    private LensDistortion lensDistortion;
#endif

    // Per-player levels: key = playerID (local player = "local")
    private readonly Dictionary<string, float> playerLevels = new();

    // Rise/fall modifiers from CLAUDE.md
    private const float RiseOnCapture        = 20f;
    private const float RiseOnNPCAttack      = 15f;
    private const float RiseOnAreaEntry      = 5f;   // area-specific added on top
    private const float RiseOnHide60s        = 5f;   // per minute hiding
    private const float FallOnMirrorFace     = -10f;
    private const float FallOnNPCListen      = -5f;
    private const float FallOnClueFound      = -5f;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out chromaticAberration);
            postProcessVolume.profile.TryGet(out lensDistortion);
        }
#endif
    }

    void Update()
    {
        if (GameManager.Instance?.State != GameState.Playing) return;

        float baseRise = (baseRisePerMinute / 60f) * Time.deltaTime;
        float areaBonus = (AreaManager.Instance?.GetCurrentHallucinationBonus() ?? 0f) * Time.deltaTime;

        // Raise local player's level
        RaiseLevel("local", baseRise + areaBonus);
        ApplyVisualEffects("local");
    }

    public float GetLevel(string playerID = "local")
    {
        playerLevels.TryGetValue(playerID, out float v);
        return v;
    }

    public void SetLevel(string playerID, float value)
    {
        playerLevels[playerID] = Mathf.Clamp(value, 0f, 100f);
    }

    public void RaiseLevel(string playerID, float amount)
        => SetLevel(playerID, GetLevel(playerID) + amount);

    public void ApplyModifier(string playerID, HallucinationModifier mod)
    {
        float delta = mod switch
        {
            HallucinationModifier.Captured    => RiseOnCapture,
            HallucinationModifier.AttackedNPC => RiseOnNPCAttack,
            HallucinationModifier.EnteredArea => RiseOnAreaEntry,
            HallucinationModifier.HidingTick  => RiseOnHide60s * Time.deltaTime,
            HallucinationModifier.FacedMirror => FallOnMirrorFace,
            HallucinationModifier.ListenedNPC => FallOnNPCListen,
            HallucinationModifier.FoundClue   => FallOnClueFound,
            _ => 0f
        };
        RaiseLevel(playerID, delta);
    }

    public HallucinationBand GetBand(string playerID = "local")
    {
        float lvl = GetLevel(playerID);
        if (lvl < 30f) return HallucinationBand.Low;
        if (lvl < 60f) return HallucinationBand.Mid;
        if (lvl < 80f) return HallucinationBand.High;
        return HallucinationBand.Extreme;
    }

    void ApplyVisualEffects(string playerID)
    {
        float t = GetLevel(playerID) / 100f;
#if UNITY_POST_PROCESSING_STACK_V2 || UNITY_URP
        if (vignette != null)
            vignette.intensity.value = Mathf.Lerp(0.15f, 0.75f, t);

        if (chromaticAberration != null)
            chromaticAberration.intensity.value = Mathf.Lerp(0f, 1f, t);

        if (lensDistortion != null)
            lensDistortion.intensity.value = Mathf.Lerp(0f, -0.5f, t);
#endif
    }

    public void ResetPlayer(string playerID)
        => playerLevels[playerID] = 0f;
}

public enum HallucinationModifier
{
    Captured, AttackedNPC, EnteredArea, HidingTick,
    FacedMirror, ListenedNPC, FoundClue
}

public enum HallucinationBand { Low, Mid, High, Extreme }
