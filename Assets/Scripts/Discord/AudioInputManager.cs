using UnityEngine;

public class AudioInputManager : MonoBehaviour
{
    public static AudioInputManager Instance { get; private set; }

    private IAudioInput audioInput;

    public bool IsDiscordActive => audioInput is DiscordAudioInput;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Initialize(bool useDiscord)
    {
        audioInput?.Dispose();
        audioInput = useDiscord ? (IAudioInput)new DiscordAudioInput() : new UnityAudioInput();
        audioInput.OnVolumeChanged += HandleVolumeChanged;
        audioInput.Start();
        Debug.Log($"[AudioInputManager] Started with {(useDiscord ? "Discord" : "Unity")} audio input.");
    }

    void HandleVolumeChanged(float volume)
    {
        if (HallucinationManager.Instance == null) return;

        if (volume > 0.8f)
        {
            HallucinationManager.Instance.RaiseLevel(5f);
        }
        else if (volume > 0.4f)
        {
            HallucinationManager.Instance.RaiseLevel(2f);
        }
    }

    void Update()
    {
        audioInput?.Update();
    }

    void OnDestroy()
    {
        if (audioInput != null)
        {
            audioInput.OnVolumeChanged -= HandleVolumeChanged;
            audioInput.Dispose();
        }
    }
}
