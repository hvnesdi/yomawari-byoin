using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 階段・エレベーターに配置するシーン遷移トリガー。
/// Box Collider (isTrigger=true) が必要。
/// </summary>
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("遷移先")]
    public string targetScene;
    public AreaID targetArea;
    public Vector3 playerSpawnPosition = new Vector3(0, 1f, 0);

    [Header("UI")]
    public string transitionMessage = "…";

    private bool transitioning;

    void OnTriggerEnter(Collider other)
    {
        if (transitioning || !other.CompareTag("Player")) return;
        transitioning = true;

        AreaManager.Instance?.EnterArea(targetArea);

        SceneSpawnData.SpawnPosition = playerSpawnPosition;
        SceneSpawnData.HasCustomSpawn = true;

        if (!string.IsNullOrEmpty(transitionMessage))
            UIManager.Instance?.ShowAnnouncement(transitionMessage);

        SceneManager.LoadScene(targetScene);
    }
}
