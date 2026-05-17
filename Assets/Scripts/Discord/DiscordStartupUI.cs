using UnityEngine;
using UnityEngine.UI;

public class DiscordStartupUI : MonoBehaviour
{
    [Header("UI")]
    public GameObject startupPanel;
    public Button withDiscordButton;
    public Button withoutDiscordButton;
    public Text statusText;

    void Start()
    {
        if (startupPanel != null) startupPanel.SetActive(true);
        withDiscordButton?.onClick.AddListener(OnWithDiscord);
        withoutDiscordButton?.onClick.AddListener(OnWithoutDiscord);
    }

    void OnWithDiscord()
    {
        if (statusText != null) statusText.text = "Discord接続中...";
        AudioInputManager.Instance?.Initialize(useDiscord: true);
        if (startupPanel != null) startupPanel.SetActive(false);
    }

    void OnWithoutDiscord()
    {
        AudioInputManager.Instance?.Initialize(useDiscord: false);
        if (startupPanel != null) startupPanel.SetActive(false);
    }
}
