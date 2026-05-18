using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [Header("Flicker Settings")]
    public float minIntensity = 0.6f;
    public float maxIntensity = 1.2f;
    public float flickerSpeed = 8f;
    public float flickerChance = 0.05f;

    [Header("Hallucination Sync")]
    public bool syncWithHallucinationLevel = true;

    Light _light;
    float _baseIntensity;
    float _targetIntensity;
    float _noiseOffset;
    float _flickerTimer;
    bool _isOff;
    float _offTimer;

    void Awake()
    {
        _light = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _targetIntensity = _baseIntensity;
        _noiseOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        float hallucinationMultiplier = 1f;
        if (syncWithHallucinationLevel && HallucinationSystem.Instance != null)
        {
            float level = HallucinationSystem.Instance.GetLevel("local");
            hallucinationMultiplier = 1f + (level / 100f) * 1.5f;
        }

        if (_isOff)
        {
            _offTimer -= Time.deltaTime;
            if (_offTimer <= 0f)
            {
                _isOff = false;
                _light.enabled = true;
            }
            return;
        }

        float noise = Mathf.PerlinNoise(_noiseOffset + Time.time * flickerSpeed, 0f);
        float targetBase = Mathf.Lerp(minIntensity, maxIntensity, noise) * hallucinationMultiplier;

        _flickerTimer -= Time.deltaTime;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = Random.Range(0.05f, 0.3f);
            if (Random.value < flickerChance * hallucinationMultiplier)
            {
                _isOff = true;
                _light.enabled = false;
                _offTimer = Random.Range(0.03f, 0.15f);
                return;
            }
            _targetIntensity = targetBase;
        }

        _light.intensity = Mathf.Lerp(_light.intensity, _targetIntensity, Time.deltaTime * 10f);
    }
}
