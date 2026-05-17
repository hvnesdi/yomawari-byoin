using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ホラー演出システム
/// CLAUDE.md: じわじわ系(レベル0+) / 心理系(30+) / びっくり系(60+)
/// </summary>
public class HorrorEventSystem : MonoBehaviour
{
    public static HorrorEventSystem Instance { get; private set; }

    [Header("NPC prefabs for jump scares")]
    public GameObject npcPrefab;

    [Header("Audio clips")]
    public AudioClip footstepsClip;
    public AudioClip nameCallClip;
    public AudioClip tapeScreamClip;
    public AudioClip backVoiceClip;
    public AudioClip suddenNoiseClip;

    [Header("Mirror")]
    public Renderer mirrorRenderer;
    public Material mirrorNormalMat;
    public Material mirrorDelayMat;
    public Material mirrorChangeMat;

    [Header("Photo")]
    public SpriteRenderer photoRenderer;
    public Sprite[] photoVariants;

    // Active coroutine handles keyed by event name
    private readonly Dictionary<string, Coroutine> activeEvents = new();
    private bool systemActive;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()  => StartAllSlowEvents();
    void OnDisable() => StopAllEvents();

    public void StartAllSlowEvents()
    {
        if (systemActive) return;
        systemActive = true;
        // じわじわ系 — start always, they check level internally
        LaunchLoop("HumanShadow",   HumanShadowLoop());
        LaunchLoop("Footsteps",     FootstepsLoop());
        LaunchLoop("NameCall",      NameCallLoop());
        LaunchLoop("PhotoChange",   PhotoChangeLoop());
        LaunchLoop("WindowFigure",  WindowFigureLoop());
        LaunchLoop("PhoneEvent",    PhoneEventLoop());
        // 心理系
        LaunchLoop("CorridorChange",   CorridorChangeLoop());
        LaunchLoop("DiaryReflection",  DiaryReflectionLoop());
        LaunchLoop("MirrorChange",     MirrorChangeLoop());
    }

    void StopAllEvents()
    {
        systemActive = false;
        foreach (var c in activeEvents.Values)
            if (c != null) StopCoroutine(c);
        activeEvents.Clear();
    }

    void LaunchLoop(string key, IEnumerator routine)
    {
        if (activeEvents.ContainsKey(key) && activeEvents[key] != null)
            StopCoroutine(activeEvents[key]);
        activeEvents[key] = StartCoroutine(routine);
    }

    // ──────────── Helpers ────────────

    static float HallucinationLevel()
    {
        string id = PlayerManager.Instance?.LocalPlayerID ?? "local";
        return HallucinationSystem.Instance?.GetLevel(id) ?? 0f;
    }

    static bool IsPlaying()
        => GameManager.Instance?.State == GameState.Playing;

    static IEnumerator WaitRandom(float minSec, float maxSec)
        => WaitSeconds(Random.Range(minSec, maxSec));

    static IEnumerator WaitSeconds(float sec)
    {
        float t = 0f;
        while (t < sec) { t += Time.deltaTime; yield return null; }
    }

    // ──────────── じわじわ系 ────────────

    IEnumerator HumanShadowLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(5f * 60f, 15f * 60f);
            if (!IsPlaying()) continue;
            TriggerHumanShadow();
        }
    }

    void TriggerHumanShadow()
    {
        // Spawn a shadow silhouette at end of nearest corridor then despawn
        AudioSystem.Instance?.PlayOneShot3D(footstepsClip, GetForwardPosition(20f));
        Debug.Log("[Horror] HumanShadow");
    }

    IEnumerator FootstepsLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(3f * 60f, 8f * 60f);
            if (!IsPlaying()) continue;
            AudioSystem.Instance?.PlayOneShot3D(footstepsClip, GetForwardPosition(8f));
            Debug.Log("[Horror] Footsteps");
        }
    }

    IEnumerator NameCallLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(10f * 60f, 20f * 60f);
            if (!IsPlaying()) continue;
            AudioSystem.Instance?.PlayOneShot(nameCallClip);
            Debug.Log("[Horror] NameCall");
        }
    }

    IEnumerator PhotoChangeLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(4f * 60f, 12f * 60f);
            if (!IsPlaying() || photoRenderer == null || photoVariants.Length == 0) continue;
            photoRenderer.sprite = photoVariants[Random.Range(0, photoVariants.Length)];
            Debug.Log("[Horror] PhotoChange");
        }
    }

    IEnumerator WindowFigureLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(6f * 60f, 14f * 60f);
            if (!IsPlaying()) continue;
            // 3F only
            var area = AreaManager.Instance?.CurrentArea;
            if (area == null || area.Value != AreaID.Floor3F) continue;
            Debug.Log("[Horror] WindowFigure (3F)");
        }
    }

    IEnumerator PhoneEventLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(8f * 60f, 16f * 60f);
            if (!IsPlaying()) continue;
            yield return StartCoroutine(PhoneSequence());
        }
    }

    IEnumerator PhoneSequence()
    {
        Debug.Log("[Horror] PhoneEvent start");
        yield return WaitSeconds(3f);
        // Play phone ring, then silence, then spawn NPC behind player
        AudioSystem.Instance?.PlayOneShot(nameCallClip);
        yield return WaitSeconds(5f);
        SpawnNPCBehindPlayer();
        Debug.Log("[Horror] PhoneEvent: NPC spawned behind");
    }

    // ──────────── 心理系 (level 30+) ────────────

    IEnumerator CorridorChangeLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(3f * 60f, 7f * 60f);
            if (!IsPlaying() || HallucinationLevel() < 30f) continue;
            Debug.Log("[Horror] CorridorChange: subtle layout shift");
        }
    }

    IEnumerator DiaryReflectionLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(10f * 60f, 20f * 60f);
            if (!IsPlaying() || HallucinationLevel() < 30f) continue;
            Debug.Log("[Horror] DiaryReflection: player name/data in diary");
        }
    }

    IEnumerator MirrorChangeLoop()
    {
        while (systemActive)
        {
            yield return WaitRandom(5f * 60f, 10f * 60f);
            if (!IsPlaying() || HallucinationLevel() < 30f || mirrorRenderer == null) continue;
            yield return StartCoroutine(MirrorChangeSequence());
        }
    }

    IEnumerator MirrorChangeSequence()
    {
        mirrorRenderer.material = mirrorChangeMat;
        Debug.Log("[Horror] MirrorChange: face distorted");
        yield return WaitSeconds(2f);
        mirrorRenderer.material = mirrorNormalMat;
    }

    // ──────────── びっくり系 (level 60+) ────────────

    // Call from game triggers (room enter events, item pickups, etc.)
    public void TriggerDarkRoomAppear()
    {
        if (HallucinationLevel() < 60f) return;
        SpawnNPCInFrontOfPlayer(2f);
        Debug.Log("[Horror] DarkRoomAppear");
    }

    public void TriggerMirrorDelay()
    {
        if (HallucinationLevel() < 60f || mirrorRenderer == null) return;
        StartCoroutine(MirrorDelaySequence());
    }

    IEnumerator MirrorDelaySequence()
    {
        mirrorRenderer.material = mirrorDelayMat;
        Debug.Log("[Horror] MirrorDelay: 0.5s lag");
        yield return WaitSeconds(0.5f);
        mirrorRenderer.material = mirrorNormalMat;
    }

    public void TriggerDoorEvent()
    {
        if (HallucinationLevel() < 60f) return;
        Debug.Log("[Horror] DoorEvent: eyes in darkness");
        // Visual handled by door animator / shader
    }

    public void TriggerTapeScream()
    {
        if (HallucinationLevel() < 60f) return;
        AudioSystem.Instance?.PlayOneShot(tapeScreamClip);
        Debug.Log("[Horror] TapeScream");
    }

    public void TriggerBackVoice()
    {
        if (HallucinationLevel() < 60f) return;
        AudioSystem.Instance?.PlayOneShot(backVoiceClip);
        Debug.Log("[Horror] BackVoice: \"ねえ\"");
    }

    public void TriggerSuddenNPC()
    {
        if (HallucinationLevel() < 60f) return;
        SpawnNPCInFrontOfPlayer(5f);
        Debug.Log("[Horror] SuddenNPC");
    }

    public void TriggerSuddenLoudNoise()
    {
        if (HallucinationLevel() < 60f) return;
        AudioSystem.Instance?.PlayOneShot(suddenNoiseClip);
        Debug.Log("[Horror] SuddenLoudNoise");
    }

    // ──────────── Spawn helpers ────────────

    void SpawnNPCBehindPlayer()
    {
        if (npcPrefab == null) return;
        var cam = Camera.main;
        if (cam == null) return;
        var pos = cam.transform.position - cam.transform.forward * 1.5f;
        pos.y = cam.transform.position.y - 1.6f;
        var npc = Instantiate(npcPrefab, pos, Quaternion.identity);
        Destroy(npc, 3f);
    }

    void SpawnNPCInFrontOfPlayer(float dist)
    {
        if (npcPrefab == null) return;
        var pos = GetForwardPosition(dist);
        var npc = Instantiate(npcPrefab, pos, Quaternion.identity);
        Destroy(npc, 2f);
    }

    Vector3 GetForwardPosition(float dist)
    {
        var cam = Camera.main;
        if (cam == null) return Vector3.zero;
        return cam.transform.position + cam.transform.forward * dist;
    }
}
