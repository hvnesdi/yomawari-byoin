using System.Collections.Generic;
using UnityEngine;

public enum ParanoiaAction { None, Trusted, Doubted, Attacked }

[System.Serializable]
public class PlayerData
{
    public string playerID;
    public Vector3 position;
    // 幻覚レベルは非同期（個別）- CLAUDE.md rule: per-player独立
    public float hallucinationLevel;
    public ParanoiaAction paranoiaResult;
    public YomawariEnding endingResult;
    public bool isAwakened;
    public int captureCount;

    // Flags (per-player, not synced)
    public bool checkedOwnRoom;
    public bool facedMirror;
    public bool readMedicalRecord;
    public bool listenedToNPC;
    public bool attackedNPC;
    public bool collectedAllClues;
    public bool followedHallucination;
    public bool triedToEscape;
}

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    // All players in session (populated by NetworkManager)
    private readonly Dictionary<string, PlayerData> players = new();

    // Local player ID (Steam ID string or "local" in offline mode)
    public string LocalPlayerID { get; private set; } = "local";

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Register local player
        RegisterPlayer(LocalPlayerID);
    }

    public void SetLocalPlayerID(string id)
    {
        if (players.ContainsKey(LocalPlayerID))
        {
            var data = players[LocalPlayerID];
            players.Remove(LocalPlayerID);
            data.playerID = id;
            players[id] = data;
        }
        LocalPlayerID = id;
    }

    public void RegisterPlayer(string playerID)
    {
        if (!players.ContainsKey(playerID))
            players[playerID] = new PlayerData { playerID = playerID };
    }

    public void RemovePlayer(string playerID)
        => players.Remove(playerID);

    public PlayerData GetPlayer(string playerID)
    {
        players.TryGetValue(playerID, out var d);
        return d;
    }

    public PlayerData GetLocalPlayer() => GetPlayer(LocalPlayerID);

    public IEnumerable<PlayerData> AllPlayers() => players.Values;

    // --- Sync helpers (called by PlayerSync) ---

    // Position and awakening state are synced; hallucination is NOT
    public void UpdatePlayerPosition(string playerID, Vector3 pos)
    {
        var d = GetPlayer(playerID);
        if (d != null) d.position = pos;
    }

    public void SetPlayerAwakened(string playerID, bool awakened)
    {
        var d = GetPlayer(playerID);
        if (d != null) d.isAwakened = awakened;
    }

    // --- Ending evaluation (checks all players) ---
    public bool AllPlayersHaveEnding()
    {
        foreach (var p in players.Values)
            if (p.endingResult == YomawariEnding.Nichijo && GameManager.Instance?.State == GameState.Playing)
                return false;
        return true;
    }

    public int AwakenedCount()
    {
        int n = 0;
        foreach (var p in players.Values)
            if (p.isAwakened) n++;
        return n;
    }

    public int TotalPlayerCount() => players.Count;
}
