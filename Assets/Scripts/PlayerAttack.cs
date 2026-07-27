using UnityEngine;

/// <summary>
/// NPC への攻撃入力。
///
/// CLAUDE.md では「NPCまたは他プレイヤーを攻撃 → 暴走エンド確定」だが、
/// 攻撃を発生させる入力が実装されておらず `ParanoiaSystem.RecordAction` の
/// 呼び出し元がどこにも無かったため、暴走エンドが到達不能だった。
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    [Header("入力")]
    public KeyCode attackKey = KeyCode.F;

    [Header("判定")]
    public float range = 2.5f;

    void Update()
    {
        if (!Input.GetKeyDown(attackKey)) return;
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        NPCManager target = null;
        float bestDistance = float.MaxValue;

        foreach (var npc in FindObjectsByType<NPCManager>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(transform.position, npc.transform.position);
            if (d <= range && d < bestDistance)
            {
                bestDistance = d;
                target = npc;
            }
        }

        if (target == null)
        {
            UIManager.Instance?.ShowAnnouncement("……何もない空間に手を振り上げていた。");
            return;
        }

        // ParanoiaSystem 側で attackedNPC フラグと幻覚+15 が処理される
        ParanoiaSystem.Instance?.RecordAction(target.name, ParanoiaAction.Attacked);
        UIManager.Instance?.ShowAnnouncement($"手を上げてしまった。相手は悲鳴も上げずに見ている。");
        Debug.Log($"[PlayerAttack] {target.name} を攻撃 → 暴走エンド確定");
    }
}
