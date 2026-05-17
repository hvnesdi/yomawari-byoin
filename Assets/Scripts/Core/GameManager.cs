using UnityEngine;

public enum GameState { Lobby, Playing, Ending, Result }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; } = GameState.Lobby;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // TimeManager and other systems initialize themselves via their own Awake/Start
    }

    public void StartGame()
    {
        State = GameState.Playing;
        TimeManager.Instance?.StartTimer();
        Debug.Log("[GameManager] Game started");
    }

    public void TriggerEnding()
    {
        if (State == GameState.Ending || State == GameState.Result) return;
        State = GameState.Ending;
        EndingSystem.Instance?.EvaluateAndTrigger();
        Debug.Log("[GameManager] Ending triggered");
    }

    public void OnTimerExpired()
    {
        // Time's up → force 日常エンド unless already ending
        if (State == GameState.Playing)
            TriggerEnding();
    }

    // Called by TimeManager at 60/30/10/5 min milestones
    public void OnTimeMilestone(int minutesRemaining)
    {
        UIManager.Instance?.ShowAnnouncement(GetAnnouncementText(minutesRemaining));
        Debug.Log($"[GameManager] Milestone: {minutesRemaining} min remaining");
    }

    string GetAnnouncementText(int min) => min switch
    {
        60 => "院内放送：残り60分です。病室にお戻りください。",
        30 => "院内放送：残り30分です。",
        10 => "院内放送：消灯10分前です。",
        5  => "院内放送：消灯5分前です。",
        0  => "院内放送：消灯時間です。おやすみなさい。",
        _  => ""
    };
}
