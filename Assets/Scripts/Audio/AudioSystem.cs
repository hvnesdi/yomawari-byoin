using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// BGM・環境音・院内放送・3Dサウンド・幻覚連動歪み
/// CLAUDE.md: ambient_normal/tense/peak/ending_* BGM、幻覚レベルで環境音が歪む
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioSystem : MonoBehaviour
{
    public static AudioSystem Instance { get; private set; }

    [Header("BGM")]
    public AudioClip bgmNormal;
    public AudioClip bgmTense;
    public AudioClip bgmPeak;
    public AudioClip bgmEndingHappy;
    public AudioClip bgmEndingBad;

    [Header("Ambient")]
    public AudioClip ambientVentilation;
    public AudioClip ambientClock;
    public AudioClip ambientRain;
    public AudioClip ambientCorridor;

    [Header("Announcements")]
    public AudioClip announce90;
    public AudioClip announce60;
    public AudioClip announce30;
    public AudioClip announce10;
    public AudioClip announce5;
    public AudioClip announce0;

    [Header("Mixer")]
    public AudioMixerGroup sfxMixerGroup;
    public AudioMixerGroup bgmMixerGroup;
    public AudioMixerGroup ambientMixerGroup;

    [Header("Distortion")]
    [Range(0f, 1f)] public float distortionMax = 0.8f;

    private AudioSource bgmSource;
    private AudioSource ambientSource;
    private readonly List<AudioSource> pooledSources = new();

    // BGM fade state
    private Coroutine bgmFade;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        bgmSource     = gameObject.AddComponent<AudioSource>();
        ambientSource = gameObject.AddComponent<AudioSource>();

        if (bgmMixerGroup    != null) bgmSource.outputAudioMixerGroup     = bgmMixerGroup;
        if (ambientMixerGroup != null) ambientSource.outputAudioMixerGroup = ambientMixerGroup;

        ambientSource.loop   = true;
        ambientSource.volume = 0.4f;

        // Pre-warm a 3D source pool
        for (int i = 0; i < 8; i++) CreatePoolSource();
    }

    void Start()
    {
        PlayBGM(bgmNormal);
        PlayAmbient(ambientCorridor);
    }

    void Update()
    {
        if (GameManager.Instance?.State != GameState.Playing) return;

        float lvl = HallucinationSystem.Instance?.GetLevel(
            PlayerManager.Instance?.LocalPlayerID ?? "local") ?? 0f;

        SyncBGMToLevel(lvl);
        ApplyDistortion(lvl);
    }

    // ──────────── BGM ────────────

    public void PlayBGM(AudioClip clip, float fadeDuration = 1.5f)
    {
        if (clip == null || bgmSource.clip == clip) return;
        if (bgmFade != null) StopCoroutine(bgmFade);
        bgmFade = StartCoroutine(FadeBGM(clip, fadeDuration));
    }

    IEnumerator FadeBGM(AudioClip next, float dur)
    {
        float start = bgmSource.volume;
        float t = 0f;
        while (t < dur)
        {
            bgmSource.volume = Mathf.Lerp(start, 0f, t / dur);
            t += Time.deltaTime;
            yield return null;
        }
        bgmSource.clip = next;
        bgmSource.loop = true;
        bgmSource.Play();
        t = 0f;
        while (t < dur)
        {
            bgmSource.volume = Mathf.Lerp(0f, 0.7f, t / dur);
            t += Time.deltaTime;
            yield return null;
        }
        bgmSource.volume = 0.7f;
    }

    void SyncBGMToLevel(float lvl)
    {
        AudioClip target = lvl switch
        {
            >= 80f => bgmPeak,
            >= 60f => bgmTense,
            >= 30f => bgmTense,
            _      => bgmNormal
        };
        PlayBGM(target);
    }

    public void PlayEndingBGM(YomawariEnding ending)
    {
        var clip = ending == YomawariEnding.Kakusei ? bgmEndingHappy : bgmEndingBad;
        PlayBGM(clip, 2f);
    }

    // ──────────── Ambient ────────────

    public void PlayAmbient(AudioClip clip)
    {
        if (clip == null) return;
        ambientSource.clip = clip;
        ambientSource.loop = true;
        ambientSource.Play();
    }

    // ──────────── Announcements (hospital PA) ────────────

    public void PlayAnnouncement(int minutesRemaining)
    {
        AudioClip clip = minutesRemaining switch
        {
            90 => announce90,
            60 => announce60,
            30 => announce30,
            10 => announce10,
            5  => announce5,
            0  => announce0,
            _  => null
        };
        PlayOneShot(clip);
        Debug.Log($"[AudioSystem] 院内放送 残り{minutesRemaining}分");
    }

    // ──────────── One-shot (2D) ────────────

    public void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        var src = GetPoolSource();
        src.spatialBlend = 0f;
        src.clip = clip;
        src.Play();
        StartCoroutine(ReturnToPool(src, clip.length + 0.1f));
    }

    // ──────────── 3D spatial ────────────

    public void PlayOneShot3D(AudioClip clip, Vector3 worldPos, float maxDist = 20f)
    {
        if (clip == null) return;
        var src = GetPoolSource();
        src.transform.position = worldPos;
        src.spatialBlend = 1f;
        src.maxDistance  = maxDist;
        src.rolloffMode  = AudioRolloffMode.Logarithmic;
        if (sfxMixerGroup != null) src.outputAudioMixerGroup = sfxMixerGroup;
        src.clip = clip;
        src.Play();
        StartCoroutine(ReturnToPool(src, clip.length + 0.1f));
    }

    // ──────────── Distortion (幻覚連動) ────────────

    void ApplyDistortion(float lvl)
    {
        // Map level 60-100 → distortion 0-max
        float t = Mathf.InverseLerp(60f, 100f, lvl);
        float dist = Mathf.Lerp(0f, distortionMax, t);

        // Pitch wobble
        float wobble = 1f + Mathf.Sin(Time.time * 2.3f) * t * 0.04f;
        ambientSource.pitch = wobble;

        // AudioMixer distortion parameter (requires "Distortion" exposed param)
        if (bgmMixerGroup != null)
            bgmMixerGroup.audioMixer.SetFloat("Distortion", dist);
    }

    // ──────────── Resources.Load API ────────────

    public void PlayBGM(string name)
    {
        var clip = Resources.Load<AudioClip>("Audio/BGM/" + name);
        if (clip == null) { Debug.LogWarning($"[AudioSystem] BGM not found: {name}"); return; }
        PlayBGM(clip);
    }

    public void PlaySE(string name)
    {
        var clip = Resources.Load<AudioClip>("Audio/SE/" + name);
        if (clip == null) { Debug.LogWarning($"[AudioSystem] SE not found: {name}"); return; }
        PlayOneShot(clip);
    }

    // ──────────── Pool ────────────

    AudioSource CreatePoolSource()
    {
        var go  = new GameObject("AudioPool");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        if (sfxMixerGroup != null) src.outputAudioMixerGroup = sfxMixerGroup;
        pooledSources.Add(src);
        return src;
    }

    AudioSource GetPoolSource()
    {
        foreach (var s in pooledSources)
            if (!s.isPlaying) return s;
        return CreatePoolSource();
    }

    IEnumerator ReturnToPool(AudioSource src, float delay)
    {
        yield return new WaitForSeconds(delay);
        src.Stop();
        src.clip = null;
        src.transform.localPosition = Vector3.zero;
    }
}
