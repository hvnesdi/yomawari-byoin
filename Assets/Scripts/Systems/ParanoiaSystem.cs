using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 疑心暗鬼システム
/// CLAUDE.md:
///   発動条件：幻覚レベル30以上
///   他プレイヤーがNPCに見える確率：(幻覚レベル - 60) × 1%
///   行動記録：Trusted / Doubted / Attacked
///   エンド影響：Trusted多い→覚醒/孤立、Doubted多い→脱出/日常、Attacked→暴走確定
/// </summary>
public class ParanoiaSystem : MonoBehaviour
{
    public static ParanoiaSystem Instance { get; private set; }

    // Per-player action records (local player's actions toward others)
    private readonly Dictionary<string, ParanoiaAction> actions = new();

    // Track how many times each action has been taken
    private int trustedCount;
    private int doubtedCount;
    private bool attackedFlag;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (GameManager.Instance?.State != GameState.Playing) return;

        float lvl = HallucinationSystem.Instance?.GetLevel("local") ?? 0f;

        // Paranoia only active when hallucination >= 30
        if (lvl < 30f) return;

        // Chance that another player appears as NPC: (level - 60) × 1%, clamped 0-40%
        float npcChance = Mathf.Clamp((lvl - 60f) * 0.01f, 0f, 0.4f);

        // Apply to remote players
        if (PlayerManager.Instance != null)
        {
            string localID = PlayerManager.Instance.LocalPlayerID;
            foreach (var p in PlayerManager.Instance.AllPlayers())
            {
                if (p.playerID == localID) continue;
                SetPlayerAppearsAsNPC(p.playerID, Random.value < npcChance);
            }
        }
    }

    // ----- Player action recording -----

    public void RecordAction(string targetPlayerID, ParanoiaAction action)
    {
        actions[targetPlayerID] = action;

        switch (action)
        {
            case ParanoiaAction.Trusted:  trustedCount++;  break;
            case ParanoiaAction.Doubted:  doubtedCount++;  break;
            case ParanoiaAction.Attacked:
                attackedFlag = true;
                // 暴走エンド確定 → set FlagManager flag
                FlagManager.Instance?.SetFlag(FlagType.attackedNPC, true);
                // Raise hallucination on attack
                HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.AttackedNPC);
                break;
        }

        UpdateLocalPlayerParanoia();
    }

    void UpdateLocalPlayerParanoia()
    {
        var local = PlayerManager.Instance?.GetLocalPlayer();
        if (local == null) return;

        if (attackedFlag)
            local.paranoiaResult = ParanoiaAction.Attacked;
        else if (trustedCount > doubtedCount)
            local.paranoiaResult = ParanoiaAction.Trusted;
        else if (doubtedCount > 0)
            local.paranoiaResult = ParanoiaAction.Doubted;
    }

    public ParanoiaAction GetLocalParanoiaResult()
        => PlayerManager.Instance?.GetLocalPlayer()?.paranoiaResult ?? ParanoiaAction.None;

    // ----- Isolation ending check -----

    /// <summary>
    /// Returns true if other players awakened while this player has not.
    /// Used by EndingSystem.IsIsolated().
    /// </summary>
    public bool IsLocalPlayerIsolated()
    {
        var pm = PlayerManager.Instance;
        if (pm == null || pm.TotalPlayerCount() <= 1) return false;

        var local = pm.GetLocalPlayer();
        if (local == null || local.isAwakened) return false;

        return pm.AwakenedCount() > 0;
    }

    // ----- NPC appearance on remote player objects -----
    private readonly Dictionary<string, bool> appearsAsNPC = new();

    void SetPlayerAppearsAsNPC(string playerID, bool asNPC)
        => appearsAsNPC[playerID] = asNPC;

    public bool DoesPlayerAppearAsNPC(string playerID)
    {
        appearsAsNPC.TryGetValue(playerID, out bool v);
        return v;
    }
}
