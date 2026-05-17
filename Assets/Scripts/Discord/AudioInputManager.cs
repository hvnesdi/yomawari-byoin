using UnityEngine;

/// <summary>
/// CLAUDE.md: AudioInputManager（抽象化）
/// Discord有無に関係なく全機能でプレイできる設計を維持する
/// </summary>
public abstract class AudioInputManager : MonoBehaviour
{
    public static AudioInputManager Instance { get; private set; }

    // Per-player volume levels (0-1), keyed by playerID
    protected readonly System.Collections.Generic.Dictionary<string, float> playerVolumes = new();

    // Silence tracking
    private float allSilentDuration;
    private const float SilenceThreshold = 0.05f;
    private const float AllSilentBonusTime = 5f * 60f;

    protected virtual void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    protected virtual void Update()
    {
        if (GameManager.Instance?.State != GameState.Playing) return;
        ProcessAudioEffects();
    }

    public abstract bool IsDiscordActive { get; }

    public float GetVolume(string playerID)
    {
        playerVolumes.TryGetValue(playerID, out float v);
        return v;
    }

    protected void ReportVolume(string playerID, float volume)
    {
        playerVolumes[playerID] = volume;
    }

    // ──────────── Game effect processing ────────────

    void ProcessAudioEffects()
    {
        string localID = PlayerManager.Instance?.LocalPlayerID ?? "local";
        float localVol  = GetVolume(localID);

        ApplyLocalVoiceEffects(localID, localVol);
        CheckAllSilence();
        CheckAllSpeaking();
        if (IsDiscordActive) CheckSoloSpeaking(localID);
    }

    // 音量大（叫び≥0.8）：敵の視野拡大・幻覚+5
    // 音量中（会話 0.3-0.8）：敵の検知範囲拡大・幻覚+2
    void ApplyLocalVoiceEffects(string playerID, float vol)
    {
        if (vol >= 0.8f)
        {
            HallucinationSystem.Instance?.ApplyModifier(playerID, HallucinationModifier.LoudScream);
            // EnemyController picks up noise via its own radius check — broadcast event
            AudioVoiceEvent.Broadcast(AudioVoiceEventType.Scream, vol);
        }
        else if (vol >= 0.3f)
        {
            HallucinationSystem.Instance?.ApplyModifier(playerID, HallucinationModifier.Talking);
            AudioVoiceEvent.Broadcast(AudioVoiceEventType.Talk, vol);
        }
    }

    // 全員無音5分：幻覚-10
    void CheckAllSilence()
    {
        bool allSilent = true;
        foreach (var v in playerVolumes.Values)
            if (v >= SilenceThreshold) { allSilent = false; break; }

        if (allSilent)
        {
            allSilentDuration += Time.deltaTime;
            if (allSilentDuration >= AllSilentBonusTime)
            {
                allSilentDuration = 0f;
                string localID = PlayerManager.Instance?.LocalPlayerID ?? "local";
                HallucinationSystem.Instance?.ApplyModifier(localID, HallucinationModifier.AllSilent);
                Debug.Log("[AudioInput] 全員無音5分: 幻覚-10");
            }
        }
        else
        {
            allSilentDuration = 0f;
        }
    }

    // 全員同時発言：現実が一瞬見える（幻覚-15）
    void CheckAllSpeaking()
    {
        if (playerVolumes.Count == 0) return;
        bool allSpeaking = true;
        foreach (var v in playerVolumes.Values)
            if (v < SilenceThreshold) { allSpeaking = false; break; }

        if (allSpeaking && playerVolumes.Count >= 2)
        {
            string localID = PlayerManager.Instance?.LocalPlayerID ?? "local";
            HallucinationSystem.Instance?.ApplyModifier(localID, HallucinationModifier.AllSpeaking);
            Debug.Log("[AudioInput] 全員同時発言: 現実が一瞬見える");
        }
    }

    // 1人だけ発言（Discordあり）：他プレイヤー幻覚+3
    void CheckSoloSpeaking(string localID)
    {
        int speakingCount = 0;
        bool localSpeaking = GetVolume(localID) >= SilenceThreshold;

        foreach (var kv in playerVolumes)
            if (kv.Value >= SilenceThreshold) speakingCount++;

        if (speakingCount == 1 && !localSpeaking)
        {
            // Someone else is speaking alone — raise local hallucination
            HallucinationSystem.Instance?.ApplyModifier(localID, HallucinationModifier.SoloOther);
            Debug.Log("[AudioInput] 1人だけ発言: 幻覚+3");
        }
    }
}

// ──────────── Voice event bus (picked up by EnemyController) ────────────

public enum AudioVoiceEventType { Talk, Scream }

public static class AudioVoiceEvent
{
    public static event System.Action<AudioVoiceEventType, float> OnVoice;
    public static void Broadcast(AudioVoiceEventType type, float volume)
        => OnVoice?.Invoke(type, volume);
}
