using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Timer")]
    public Text timerText;

    [Header("Announcement")]
    public Text announcementText;
    public float announcementDuration = 5f;

    [Header("Hallucination Overlay")]
    public CanvasGroup hallucinationOverlay;

    [Header("Interaction Prompt")]
    public Text interactionPrompt;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        UpdateTimer();
        UpdateHallucinationOverlay();
    }

    void UpdateTimer()
    {
        if (timerText == null || TimeManager.Instance == null) return;
        float r = TimeManager.Instance.Remaining;
        int m = Mathf.FloorToInt(r / 60f);
        int s = Mathf.FloorToInt(r % 60f);
        timerText.text = $"{m:D2}:{s:D2}";
        timerText.color = r < 300f ? Color.red : Color.white;
    }

    void UpdateHallucinationOverlay()
    {
        if (hallucinationOverlay == null || HallucinationSystem.Instance == null) return;
        float t = HallucinationSystem.Instance.GetLevel("local") / 100f;
        hallucinationOverlay.alpha = Mathf.Lerp(0f, 0.6f, t);
    }

    public void ShowAnnouncement(string msg)
    {
        if (announcementText == null) return;
        StopCoroutine(nameof(HideAnnouncementAfterDelay));
        announcementText.text = msg;
        announcementText.gameObject.SetActive(true);
        StartCoroutine(HideAnnouncementAfterDelay());
    }

    IEnumerator HideAnnouncementAfterDelay()
    {
        yield return new WaitForSeconds(announcementDuration);
        if (announcementText != null)
            announcementText.gameObject.SetActive(false);
    }

    public void ShowInteractionPrompt(string msg)
    {
        if (interactionPrompt == null) return;
        interactionPrompt.text = msg;
        interactionPrompt.gameObject.SetActive(true);
    }

    public void HideInteractionPrompt()
    {
        if (interactionPrompt != null)
            interactionPrompt.gameObject.SetActive(false);
    }
}
