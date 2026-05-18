using System.Collections;
using UnityEngine;

/// <summary>
/// 「出口」に見せかけたトリガー。
/// CLAUDE.md: 脱出しようとすると同じ廊下に戻る / triedToEscapeフラグをセット。
/// </summary>
public class EscapeAttemptTrigger : MonoBehaviour
{
    [Header("帰還先")]
    public Transform returnSpawnPoint;

    [Header("演出")]
    public string escapeMessage = "外に出た…… 気づくと、また同じ廊下にいた。";
    public float blackoutDuration = 1.5f;

    private bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player")) return;
        triggered = true;

        FlagManager.Instance?.SetFlag(FlagType.triedToEscape, true);
        HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.EnteredArea);

        StartCoroutine(EscapeSequence(other.transform));
    }

    IEnumerator EscapeSequence(Transform playerTransform)
    {
        UIManager.Instance?.ShowAnnouncement(escapeMessage);
        yield return new WaitForSeconds(blackoutDuration);

        if (returnSpawnPoint != null)
        {
            var cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerTransform.position = returnSpawnPoint.position;
            if (cc != null) cc.enabled = true;
        }

        // 少し待ってから再トリガー可能にする
        yield return new WaitForSeconds(3f);
        triggered = false;
    }
}
