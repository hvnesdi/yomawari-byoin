using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    [SerializeField] private float fadeInDuration = 2f;
    [SerializeField] private string hospitalScene = "Hospital";

    private CanvasGroup canvasGroup;
    private Button startButton;

    void Start()
    {
        canvasGroup = FindObjectOfType<CanvasGroup>();
        startButton = FindObjectOfType<Button>();

        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        if (canvasGroup == null) yield break;
        float t = 0f;
        while (t < fadeInDuration)
        {
            canvasGroup.alpha = t / fadeInDuration;
            t += Time.deltaTime;
            yield return null;
        }
        canvasGroup.alpha = 1f;
    }

    void OnStartClicked()
    {
        StartCoroutine(FadeOutAndLoad());
    }

    IEnumerator FadeOutAndLoad()
    {
        if (canvasGroup != null)
        {
            float t = 0f;
            float dur = 1f;
            while (t < dur)
            {
                canvasGroup.alpha = 1f - t / dur;
                t += Time.deltaTime;
                yield return null;
            }
        }
        SceneManager.LoadScene(hospitalScene);
    }
}
