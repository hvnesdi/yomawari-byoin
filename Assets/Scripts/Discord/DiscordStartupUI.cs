using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 起動時のDiscord有無選択UI
/// CLAUDE.md: Discordなしでも全機能でプレイできる設計を維持する
/// </summary>
public class DiscordStartupUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject panel;
    public Button withDiscordButton;
    public Button withoutDiscordButton;
    public Text statusText;

    [Header("Prefabs")]
    public GameObject unityAudioInputPrefab;
    public GameObject discordAudioInputPrefab;

    void Start()
    {
        if (panel != null) panel.SetActive(true);
        if (withDiscordButton    != null) withDiscordButton.onClick.AddListener(ChooseDiscord);
        if (withoutDiscordButton != null) withoutDiscordButton.onClick.AddListener(ChooseUnity);
    }

    void ChooseDiscord()
    {
        Instantiate(discordAudioInputPrefab);
        SetStatus("Discord連携モードで起動します");
        HidePanel();
    }

    void ChooseUnity()
    {
        Instantiate(unityAudioInputPrefab);
        SetStatus("標準マイクモードで起動します");
        HidePanel();
    }

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[DiscordStartupUI] {msg}");
    }

    void HidePanel()
    {
        if (panel != null) panel.SetActive(false);
        Destroy(gameObject, 1f);
    }
}
