using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class HallucinationManager : MonoBehaviour
{
    public static HallucinationManager Instance { get; private set; }

    [Header("Hallucination Settings")]
    [Range(0, 10)] public float level = 0f;
    public float riseRatePerSecond = 0.03f;
    public float maxLevel = 10f;

    [Header("Camera Shake")]
    public float shakeAmplitude = 0f;
    public AnimationCurve shakeByLevel;

    [Header("Post Processing")]
    public Volume postProcessVolume;
    private Vignette vignette;
    private ChromaticAberration chromaticAberration;

    public float Level => level;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out vignette);
            postProcessVolume.profile.TryGet(out chromaticAberration);
        }
    }

    void Update()
    {
        RaiseLevel(riseRatePerSecond * Time.deltaTime);
        ApplyEffects();
    }

    public void RaiseLevel(float amount)
    {
        level = Mathf.Clamp(level + amount, 0f, maxLevel);
    }

    public void ReduceLevel(float amount)
    {
        level = Mathf.Clamp(level - amount, 0f, maxLevel);
    }

    void ApplyEffects()
    {
        float t = level / maxLevel;

        if (vignette != null)
            vignette.intensity.value = Mathf.Lerp(0.2f, 0.7f, t);

        if (chromaticAberration != null)
            chromaticAberration.intensity.value = Mathf.Lerp(0f, 1f, t);

        shakeAmplitude = shakeByLevel != null ? shakeByLevel.Evaluate(t) : t * 0.3f;
    }

    public void ResetLevel()
    {
        level = 0f;
    }
}
