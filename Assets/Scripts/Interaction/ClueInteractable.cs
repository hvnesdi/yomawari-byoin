using UnityEngine;

public enum ClueType
{
    MedicalRecord,  // readMedicalRecord フラグ
    OwnRoom,        // checkedOwnRoom フラグ
    Mirror,         // facedMirror フラグ
    RecordingTape,  // ホラー演出トリガー
    Diary,          // 日記（汎用手がかり）
    Photo,          // 写真（汎用手がかり）
}

/// <summary>
/// 手がかりオブジェクトに付けるInteractableスクリプト。
/// CLAUDE.md: FlagManagerの各フラグをセット、幻覚レベルを下げる。
/// </summary>
public class ClueInteractable : MonoBehaviour
{
    [Header("手がかり設定")]
    public ClueType clueType;
    public float interactRange = 2f;
    public string promptText = "E: 調べる";

    private Transform player;
    private bool examined;
    private bool showingPrompt;

    void Start()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null) player = go.transform;
    }

    void Update()
    {
        if (player == null) return;

        bool inRange = Vector3.Distance(transform.position, player.position) <= interactRange;

        if (inRange && !examined)
        {
            if (!showingPrompt)
            {
                UIManager.Instance?.ShowInteractionPrompt(promptText);
                showingPrompt = true;
            }
            if (Input.GetKeyDown(KeyCode.E)) Examine();
        }
        else if (showingPrompt)
        {
            UIManager.Instance?.HideInteractionPrompt();
            showingPrompt = false;
        }
    }

    void Examine()
    {
        var fm = FlagManager.Instance;
        if (fm == null) return;

        switch (clueType)
        {
            case ClueType.MedicalRecord:
                fm.SetFlag(FlagType.readMedicalRecord, true);
                HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.FoundClue);
                UIManager.Instance?.ShowAnnouncement("入院記録を読んだ…… 自分の名前が書いてある。");
                HorrorEventSystem.Instance?.TriggerTapeScream();
                examined = true;
                break;

            case ClueType.OwnRoom:
                fm.SetFlag(FlagType.checkedOwnRoom, true);
                HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.FoundClue);
                UIManager.Instance?.ShowAnnouncement("この病室には…… 自分の私物がある。");
                examined = true;
                break;

            case ClueType.Mirror:
                fm.SetFlag(FlagType.facedMirror, true);
                HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.FacedMirror);
                UIManager.Instance?.ShowAnnouncement("鏡の中の自分を見つめた。");
                HorrorEventSystem.Instance?.TriggerMirrorDelay();
                examined = true;
                break;

            case ClueType.RecordingTape:
                HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.FoundClue);
                UIManager.Instance?.ShowAnnouncement("録音テープを再生した…… 最後に叫び声が入っている。");
                HorrorEventSystem.Instance?.TriggerTapeScream();
                examined = true;
                break;

            case ClueType.Diary:
                HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.FoundClue);
                UIManager.Instance?.ShowAnnouncement("日記を読んだ。日付は数年前から続いている……");
                examined = true;
                break;

            case ClueType.Photo:
                HallucinationSystem.Instance?.ApplyModifier("local", HallucinationModifier.FoundClue);
                UIManager.Instance?.ShowAnnouncement("写真を見た。見覚えのない人たちが写っている。");
                examined = true;
                break;
        }

        if (examined) CheckAllClues();
    }

    void CheckAllClues()
    {
        var fm = FlagManager.Instance;
        if (fm == null) return;

        if (fm.GetFlag(FlagType.readMedicalRecord) &&
            fm.GetFlag(FlagType.checkedOwnRoom) &&
            fm.GetFlag(FlagType.facedMirror) &&
            fm.GetFlag(FlagType.listenedToNPC))
        {
            fm.SetFlag(FlagType.collectedAllClues, true);
            UIManager.Instance?.ShowAnnouncement("全ての手がかりが揃った……");
        }
    }

    void OnDisable()
    {
        if (showingPrompt)
        {
            UIManager.Instance?.HideInteractionPrompt();
            showingPrompt = false;
        }
    }
}
