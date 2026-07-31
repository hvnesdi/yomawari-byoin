using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// プレイ開始から数秒後に「ちゃんと動いているか」を自動で点検して Console に出す。
/// __Systems プレハブに載っており、病院シーンに入ると1度だけ走る。
///
/// 目視だけだと「動いているつもりで実は Null 参照で無音のまま」という
/// これまでの失敗（マネージャ未配置に気づかず Phase 4 完了扱いになっていた）を防ぐのが目的。
///
/// TODO(リリース前): このコンポーネントはデバッグ用。製品ビルドからは外すこと。
/// </summary>
public class PlayModeSelfCheck : MonoBehaviour
{
    [Header("点検")]
    public bool runSelfCheck = true;
    public float delaySeconds = 2f;

    [Header("デバッグ操作（90分待たずに検証するため）")]
    public bool enableDebugKeys = true;
    public KeyCode skip10MinKey  = KeyCode.F9;
    public KeyCode forceEndKey   = KeyCode.F10;

    bool done;

    void Start()
    {
        if (runSelfCheck) StartCoroutine(RunAfterDelay());
    }

    void Update()
    {
        if (!enableDebugKeys) return;

        if (Input.GetKeyDown(skip10MinKey))
        {
            var tm = TimeManager.Instance;
            if (tm != null)
            {
                tm.DebugAdvance(10f * 60f);
                Debug.Log($"[SelfCheck] 10分スキップ → 残り {tm.Remaining / 60f:F1} 分");
            }
        }

        if (Input.GetKeyDown(forceEndKey))
        {
            Debug.Log("[SelfCheck] エンドを強制発火");
            GameManager.Instance?.TriggerEnding();
        }
    }

    IEnumerator RunAfterDelay()
    {
        // 病院シーンに入るまで待つ（タイトル画面では点検しない）
        while (!SceneManager.GetActiveScene().name.StartsWith("Hospital"))
            yield return null;

        yield return new WaitForSeconds(delaySeconds);
        if (done) yield break;
        done = true;

        var sb = new StringBuilder();
        int pass = 0, total = 0;

        void Check(bool ok, string label, string detail)
        {
            total++;
            if (ok) { pass++; sb.AppendLine($"  [PASS] {label}"); }
            else sb.AppendLine($"  [FAIL] {label} — {detail}");
        }

        Check(GameManager.Instance != null, "GameManager 常駐",
              "SystemsBootstrap がプレハブを生成できていない");
        Check(GameManager.Instance != null && GameManager.Instance.State == GameState.Playing,
              "ゲーム開始済み", "State が Playing になっていない");

        var tm = TimeManager.Instance;
        Check(tm != null && tm.IsRunning, "タイマー稼働", "TimeManager が回っていない");
        Check(tm != null && tm.Remaining < TimeManager.TotalSeconds, "タイマー減少",
              "残り時間が減っていない");

        Check(GameObject.FindGameObjectWithTag("Player") != null, "Player タグ",
              "敵の追跡・手がかり調査が動作しない");
        Check(FindFirstObjectByType<AudioListener>() != null, "AudioListener",
              "音が一切鳴らない");

        // AudioListener があるだけでは音は鳴らない。
        // 実際、クリップが1つも割り当てられていないまま「AudioListener PASS」で
        // 通り続け、**ゲームが完全に無音であることに長く気づけなかった**。
        // 聞こえるものが実在するかを見る。
        // 「何か鳴っている」では足りない。蛍光灯だけ鳴っていて環境音が止まっていても
        // 通ってしまう。鳴っているものを種類ごとに数える。
        var sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                          .Where(s => s.isPlaying && s.clip != null).ToList();
        int buzz = sources.Count(s => s.clip.name.StartsWith("Fluorescent"));
        int ambience = sources.Count(s => s.clip.name.StartsWith("Ambient_"));

        Check(ambience > 0, "環境音", "フロアの環境音が鳴っていない");
        Check(buzz > 0, "蛍光灯の音",
              $"光源からの音が無い（鳴っている音源 {sources.Count} 個）");

        var footsteps = FindFirstObjectByType<FootstepPlayer>();
        Check(footsteps != null, "足音", "FootstepPlayer がプレイヤーに付いていない");

        // 恐怖演出は見えない場所で起きるので、音が無いと文字通り何も起きない。
        // フィールドがあるだけで中身が無い状態が長く続いたので、中身を見る
        var horror = FindFirstObjectByType<HorrorEventSystem>();
        Check(horror != null && horror.footstepsClip != null && horror.nameCallClip != null &&
              horror.tapeScreamClip != null && horror.backVoiceClip != null &&
              horror.suddenNoiseClip != null,
              "恐怖演出の音", "HorrorEventSystem のクリップが未設定（演出が無音で発火する）");

        Check(FindFirstObjectByType<ChaseAudio>() != null, "追跡時の音",
              "ChaseAudio が無い（見つかっても音で分からない）");

        // 鏡と写真の演出。実装はあったが、映る物も写真もシーンに無く、
        // 参照も空だったので、発火しても何も起きていなかった
        var horrorProps = HorrorEventSystem.Instance;
        Check(horrorProps != null && horrorProps.mirrorRenderer != null &&
              horrorProps.mirrorNormalMat != null && horrorProps.mirrorDelayMat != null &&
              horrorProps.mirrorChangeMat != null,
              "鏡の演出", "鏡が結線されていない（映りの変化が起きない）");

        Check(horrorProps != null && horrorProps.photoRenderer != null &&
              horrorProps.photoVariants != null && horrorProps.photoVariants.Length > 1,
              "写真の演出", "写真が結線されていない（入れ替わりが起きない）");

        Check(horrorProps != null && horrorProps.npcPrefab != null,
              "暗室の人物", "npcPrefab が未設定（暗い部屋の演出で何も出ない）");

        var mirror = FindFirstObjectByType<MirrorReflection>();
        Check(mirror != null && mirror.mirrorBody != null,
              "鏡に映る体", "鏡に映る体が無い（一人称なので何も映らない）");

        var audioSystem = AudioSystem.Instance;
        Check(audioSystem != null && audioSystem.bgmNormal != null &&
              audioSystem.bgmTense != null && audioSystem.bgmPeak != null,
              "緊張度の層", "BGM が未設定（緊張が上がっても音が変わらない）");

        // キャラクターが動いているか。
        // Animator が付いているだけでは足りない（コントローラ未設定でも
        // 例外は出ず、静止したまま黙って通る）。実際に再生中かを見る。
        var animators = FindObjectsByType<Animator>(FindObjectsSortMode.None)
                            .Where(a => a.runtimeAnimatorController != null).ToList();
        Check(animators.Count > 0, "キャラクターのモーション",
              "Animator が1つも設定されていない（敵が滑って移動する）");
        Check(animators.Any(a => a.GetCurrentAnimatorClipInfoCount(0) > 0),
              "モーション再生中", "Animator は在るがクリップが再生されていない");

        Check(Camera.main != null, "MainCamera", "カメラが無い");

        var ui = UIManager.Instance;
        Check(ui != null && ui.timerText != null && !string.IsNullOrEmpty(ui.timerText.text),
              "タイマー表示", "UI にタイマーが出ていない");

        var hs = HallucinationSystem.Instance;
        Check(hs != null && hs.GetLevel("local") > 0f, "幻覚レベル上昇",
              "レベルが 0 のまま（Update が回っていない可能性）");

        Check(FlagManager.Instance != null, "FlagManager 常駐", "フラグが記録されない");
        Check(EndingSystem.Instance != null && EndingSystem.Instance.endingPanel != null,
              "エンドUI 結線", "エンド画面が出せない");

        var header = $"===== プレイ時セルフチェック ({pass}/{total} PASS) =====";
        var body = header + "\n" + sb + $"シーン: {SceneManager.GetActiveScene().name}\n" +
                   (enableDebugKeys ? $"デバッグ: {skip10MinKey}=10分スキップ / {forceEndKey}=エンド強制\n" : "");

        if (pass == total) Debug.Log(body);
        else Debug.LogError(body);
    }
}
