using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    public const float TotalSeconds = 90f * 60f;
    public float Remaining { get; private set; } = TotalSeconds;
    public bool IsRunning { get; private set; }

    // Minutes remaining when each milestone fires
    private static readonly int[] Milestones = { 60, 30, 10, 5, 0 };
    private bool[] milestoneTriggered;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        milestoneTriggered = new bool[Milestones.Length];
    }

    public void StartTimer()
    {
        Remaining = TotalSeconds;
        IsRunning = true;
    }

    public void PauseTimer() => IsRunning = false;
    public void ResumeTimer() => IsRunning = true;

    void Update()
    {
        if (!IsRunning) return;

        Remaining -= Time.deltaTime;
        CheckMilestones();

        if (Remaining <= 0f)
        {
            Remaining = 0f;
            IsRunning = false;
            GameManager.Instance?.OnTimerExpired();
        }
    }

    void CheckMilestones()
    {
        int minutesLeft = Mathf.CeilToInt(Remaining / 60f);
        for (int i = 0; i < Milestones.Length; i++)
        {
            if (!milestoneTriggered[i] && minutesLeft <= Milestones[i])
            {
                milestoneTriggered[i] = true;
                GameManager.Instance?.OnTimeMilestone(Milestones[i]);
            }
        }
    }

    public float ElapsedSeconds => TotalSeconds - Remaining;
    public float ElapsedMinutes => ElapsedSeconds / 60f;
}
