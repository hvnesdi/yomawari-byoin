using System;
using System.Collections.Generic;
using UnityEngine;

public enum FlagType
{
    // Phase 1
    MetNurse,
    FoundMedicine,
    SawHallucination,
    ExploredRoom1,
    ExploredRoom2,
    ExploredRoom3,
    TriggeredAlarm,
    FoundExit,
    // Phase 2+
    checkedOwnRoom,
    facedMirror,
    readMedicalRecord,
    listenedToNPC,
    attackedNPC,
    collectedAllClues,
    followedHallucination,
    triedToEscape
}

public class FlagManager : MonoBehaviour
{
    public static FlagManager Instance { get; private set; }
    public event Action<FlagType, bool> OnFlagChanged;

    private Dictionary<FlagType, bool> flags = new Dictionary<FlagType, bool>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadFlags();
    }

    public void SetFlag(FlagType flag, bool value)
    {
        flags[flag] = value;
        PlayerPrefs.SetInt(flag.ToString(), value ? 1 : 0);
        PlayerPrefs.Save();
        OnFlagChanged?.Invoke(flag, value);
    }

    public bool GetFlag(FlagType flag)
    {
        return flags.TryGetValue(flag, out bool val) && val;
    }

    public void ResetAllFlags()
    {
        foreach (FlagType f in Enum.GetValues(typeof(FlagType)))
            flags[f] = false;
        PlayerPrefs.DeleteAll();
    }

    private void LoadFlags()
    {
        foreach (FlagType f in Enum.GetValues(typeof(FlagType)))
            flags[f] = PlayerPrefs.GetInt(f.ToString(), 0) == 1;
    }
}
