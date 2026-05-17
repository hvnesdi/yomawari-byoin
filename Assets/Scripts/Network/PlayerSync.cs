using UnityEngine;

/// <summary>
/// Syncs THIS player's position and state to all remote players.
/// CLAUDE.md: 同期対象 = 位置・アニメーション・インタラクション・エンド確定状態
///            非同期（個別）= 幻覚レベル・フラグ・見えているもの  ← NOT synced here
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerSync : MonoBehaviour
{
    [Header("Sync settings")]
    public float positionSendRate = 0.1f;   // 10 times/sec
    public float syncThreshold   = 0.05f;   // only send if moved this far

    private float sendTimer;
    private Vector3 lastSentPosition;
    private Animator animator;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        sendTimer += Time.deltaTime;
        if (sendTimer >= positionSendRate)
        {
            sendTimer = 0f;
            TrySendPosition();
        }
    }

    void TrySendPosition()
    {
        if (Vector3.Distance(transform.position, lastSentPosition) < syncThreshold) return;
        lastSentPosition = transform.position;
        NetworkManager.Instance?.BroadcastPosition(transform.position);

        // Update local PlayerManager so other systems can query this player's position
        var localID = PlayerManager.Instance?.LocalPlayerID ?? "local";
        PlayerManager.Instance?.UpdatePlayerPosition(localID, transform.position);
    }

    // Call when local player achieves awakening ending
    public void NotifyAwakened()
    {
        var localID = PlayerManager.Instance?.LocalPlayerID ?? "local";
        PlayerManager.Instance?.SetPlayerAwakened(localID, true);
        NetworkManager.Instance?.BroadcastAwakened(true);
    }

    // Call when local player's ending is decided
    public void NotifyEndingDecided(int endingIndex)
    {
        NetworkManager.Instance?.BroadcastEnding(endingIndex);
    }
}
