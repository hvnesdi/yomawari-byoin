using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 6-ending system with priority: 暴走 > 孤立 > 覚醒 > 脱出 > 救出 > 日常
/// CLAUDE.md rule: エンド判定は優先度順に処理する
/// CLAUDE.md rule: 悟られないバッドエンドの真実はゲーム内で明示しない
/// </summary>
public class EndingSystem : MonoBehaviour
{
    public static EndingSystem Instance { get; private set; }

    [Header("UI")]
    public GameObject endingPanel;
    public UnityEngine.UI.Text endingTitleText;
    public UnityEngine.UI.Text endingBodyText;

    // Tracks per-player determined endings (multiplayer: all must resolve)
    private readonly Dictionary<string, YomawariEnding> playerEndings = new();
    private bool endingTriggered;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void EvaluateAndTrigger()
    {
        if (endingTriggered) return;

        var fm = FlagManager.Instance;
        if (fm == null) { TriggerEnding(YomawariEnding.Nichijo); return; }

        // Priority order from CLAUDE.md: 暴走 > 孤立 > 覚醒 > 脱出 > 救出 > 日常
        YomawariEnding result = EvaluatePriority(fm);
        TriggerEnding(result);
    }

    YomawariEnding EvaluatePriority(FlagManager fm)
    {
        // 暴走: NPCまたは他プレイヤーを攻撃
        if (fm.GetFlag(FlagType.attackedNPC))
            return YomawariEnding.Boso;

        // 孤立: 他が覚醒したのに自分だけ気づけなかった（マルチプレイ限定）
        if (IsIsolated())
            return YomawariEnding.Koritsu;

        // 覚醒: 全手がかり収集＋残り30分以上＋鏡直視＋NPCの言葉を聞いた
        float remaining = TimeManager.Instance?.Remaining ?? 0f;
        if (fm.GetFlag(FlagType.collectedAllClues) &&
            remaining >= 30f * 60f &&
            fm.GetFlag(FlagType.facedMirror) &&
            fm.GetFlag(FlagType.listenedToNPC))
            return YomawariEnding.Kakusei;

        // 脱出: 手がかりをほぼ無視して外へ
        if (fm.GetFlag(FlagType.triedToEscape) && !fm.GetFlag(FlagType.collectedAllClues))
            return YomawariEnding.Dasshutsu;

        // 救出: 幻覚に従った
        if (fm.GetFlag(FlagType.followedHallucination))
            return YomawariEnding.Kyushutsu;

        // 日常: 時間切れ or 何も解決しないまま
        return YomawariEnding.Nichijo;
    }

    bool IsIsolated()
        => ParanoiaSystem.Instance?.IsLocalPlayerIsolated() ?? false;

    void TriggerEnding(YomawariEnding ending)
    {
        endingTriggered = true;
        GameManager.Instance?.TriggerEnding();
        TimeManager.Instance?.PauseTimer();
        Time.timeScale = 0f;

        if (endingPanel != null) endingPanel.SetActive(true);

        var (title, body) = GetEndingText(ending);
        if (endingTitleText != null) endingTitleText.text = title;
        if (endingBodyText  != null) endingBodyText.text  = body;

        Debug.Log($"[EndingSystem] {ending}: {title}");
        PlayHiddenEffect(ending);
    }

    static (string title, string body) GetEndingText(YomawariEnding e) => e switch
    {
        YomawariEnding.Boso      => ("ENDING: 暴走",     "あなたは隔離室へ送られた。"),
        YomawariEnding.Koritsu   => ("ENDING: 孤立",     "他の全員が気づいた。あなただけが残された。"),
        YomawariEnding.Kakusei   => ("ENDING: 覚醒 ★",  "全てが幻覚だったと気づいた。治療を受け入れる。"),
        YomawariEnding.Dasshutsu => ("ENDING: 脱出",     "病院を出た。外の空気は懐かしい。"),
        YomawariEnding.Kyushutsu => ("ENDING: 救出",     "家族が迎えに来た。"),
        YomawariEnding.Nichijo   => ("ENDING: 日常",     "気がつくと、自分の部屋にいた。"),
        _ => ("", "")
    };

    // Hidden post-roll effects for non-obvious bad endings (CLAUDE.md rule: truth not shown in-game)
    void PlayHiddenEffect(YomawariEnding e)
    {
        // These are triggered after credits in a real build
        switch (e)
        {
            case YomawariEnding.Dasshutsu:
                Debug.Log("[EndingSystem] Hidden: 入院記録の更新日 / 画面フラッシュ");
                break;
            case YomawariEnding.Kyushutsu:
                Debug.Log("[EndingSystem] Hidden: エンドロール中盤に病室の写真カット");
                break;
            case YomawariEnding.Nichijo:
                Debug.Log("[EndingSystem] Hidden: 窓の外の鉄格子");
                break;
        }
    }
}

public enum YomawariEnding { Boso, Koritsu, Kakusei, Dasshutsu, Kyushutsu, Nichijo }
