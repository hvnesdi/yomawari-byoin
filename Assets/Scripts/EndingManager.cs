using UnityEngine;
using UnityEngine.UI;

public enum EndingType
{
    None,
    Ending1_Escape,
    Ending2_Caught,
    Ending3_Medicine,
    Ending4_Hallucination,
    Ending5_Dawn,
    Ending6_Secret
}

public class EndingManager : MonoBehaviour
{
    public static EndingManager Instance { get; private set; }

    [Header("UI")]
    public GameObject endingPanel;
    public Text endingText;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        CheckEndings();
    }

    void CheckEndings()
    {
        if (FlagManager.Instance == null) return;

        EndingType ending = EvaluateEnding();
        if (ending != EndingType.None)
            TriggerEnding(ending);
    }

    EndingType EvaluateEnding()
    {
        var fm = FlagManager.Instance;
        if (fm.GetFlag(FlagType.FoundExit) && !fm.GetFlag(FlagType.TriggeredAlarm))
            return EndingType.Ending1_Escape;
        if (fm.GetFlag(FlagType.FoundExit) && fm.GetFlag(FlagType.TriggeredAlarm))
            return EndingType.Ending2_Caught;
        if (fm.GetFlag(FlagType.FoundMedicine) && fm.GetFlag(FlagType.MetNurse))
            return EndingType.Ending3_Medicine;
        if (fm.GetFlag(FlagType.SawHallucination) && HallucinationManager.Instance != null && HallucinationManager.Instance.Level >= 5)
            return EndingType.Ending4_Hallucination;
        if (fm.GetFlag(FlagType.ExploredRoom1) && fm.GetFlag(FlagType.ExploredRoom2) && fm.GetFlag(FlagType.ExploredRoom3))
            return EndingType.Ending5_Dawn;
        if (fm.GetFlag(FlagType.FoundMedicine) && fm.GetFlag(FlagType.FoundExit) && fm.GetFlag(FlagType.MetNurse))
            return EndingType.Ending6_Secret;
        return EndingType.None;
    }

    public void TriggerEnding(EndingType ending)
    {
        if (endingPanel != null) endingPanel.SetActive(true);
        string msg = ending switch
        {
            EndingType.Ending1_Escape        => "Ending 1: You escaped in silence.",
            EndingType.Ending2_Caught        => "Ending 2: You were caught at the exit.",
            EndingType.Ending3_Medicine      => "Ending 3: The medicine calmed your mind.",
            EndingType.Ending4_Hallucination => "Ending 4: Lost in hallucination forever.",
            EndingType.Ending5_Dawn          => "Ending 5: You survived until dawn.",
            EndingType.Ending6_Secret        => "Ending 6: ??? Secret Ending ???",
            _                               => ""
        };
        if (endingText != null) endingText.text = msg;
        Debug.Log($"[EndingManager] {ending}: {msg}");
        Time.timeScale = 0f;
    }
}
