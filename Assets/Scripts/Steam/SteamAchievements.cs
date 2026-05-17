using System;
using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

public class SteamAchievements : MonoBehaviour
{
    public static SteamAchievements Instance { get; private set; }

    private const string ACH_ENDING1 = "ACH_ESCAPE_SILENT";
    private const string ACH_ENDING2 = "ACH_CAUGHT_EXIT";
    private const string ACH_ENDING3 = "ACH_MEDICINE";
    private const string ACH_ENDING4 = "ACH_HALLUCINATION";
    private const string ACH_ENDING5 = "ACH_DAWN";
    private const string ACH_ENDING6 = "ACH_SECRET";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        EndingManager.OnEndingTriggered += OnEndingTriggered;
    }

    void OnDisable()
    {
        EndingManager.OnEndingTriggered -= OnEndingTriggered;
    }

    void OnEndingTriggered(EndingType ending)
    {
        string achId = ending switch
        {
            EndingType.Ending1_Escape        => ACH_ENDING1,
            EndingType.Ending2_Caught        => ACH_ENDING2,
            EndingType.Ending3_Medicine      => ACH_ENDING3,
            EndingType.Ending4_Hallucination => ACH_ENDING4,
            EndingType.Ending5_Dawn          => ACH_ENDING5,
            EndingType.Ending6_Secret        => ACH_ENDING6,
            _                                => null
        };
        if (achId != null) UnlockAchievement(achId);
    }

    public void UnlockAchievement(string achievementId)
    {
#if STEAMWORKS_NET
        if (!SteamManager.Initialized) return;
        SteamUserStats.SetAchievement(achievementId);
        SteamUserStats.StoreStats();
#endif
        Debug.Log($"[Steam] Achievement unlocked: {achievementId}");
    }
}
