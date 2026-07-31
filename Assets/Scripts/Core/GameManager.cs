using UnityEngine;
using UnityEngine.SceneManagement;

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

    void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start()
    {
        // ブートストラップ生成時点で既に病院シーンが開かれている場合（エディタで
        // Hospital*.unity から直接 Play したケース）に対応する
        TryAutoStart(SceneManager.GetActiveScene().name);
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAutoStart(scene.name);

    /// <summary>
    /// 病院シーンに入った時点でゲームを開始する。フロア移動では再開始しない
    /// （State が Playing のため素通りする）。
    /// </summary>
    void TryAutoStart(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Hospital")) return;
        if (State != GameState.Lobby) return;

        // FlagManager は PlayerPrefs から前回のフラグを復元してしまうため、
        // 新しいプレイの開始時に必ずクリアする
        FlagManager.Instance?.ResetAllFlags();
        StartGame();
    }

    public void StartGame()
    {
        State = GameState.Playing;
        Time.timeScale = 1f;   // 前回のエンドで 0 にされたままのことがある
        TimeManager.Instance?.StartTimer();
        HorrorEventSystem.Instance?.StartAllSlowEvents();
        UIManager.Instance?.ShowAnnouncement("院内放送：消灯まで90分です。病室にお戻りください。");
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
        // 放送のチャイム。`AudioSystem.PlayAnnouncement` は前からあったが
        // **どこからも呼ばれておらず**、時間の節目が文字だけで通り過ぎていた
        AudioSystem.Instance?.PlayAnnouncement(minutesRemaining);
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
