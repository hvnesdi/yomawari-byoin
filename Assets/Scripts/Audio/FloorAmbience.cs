using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// フロアごとの環境音を鳴らす。
///
/// `AudioSystem` は BGM と環境音のクリップを持つ作りになっていたが、
/// **クリップが1つも割り当てられていなかったのでゲームは完全に無音だった。**
/// 曲を流すのではなく、その階に居るときに聞こえる音を鳴らす。
///
/// フロアごとに別の音にしてあるのは、階の違いを音でも伝えるため。
/// 地下は低音と水滴、3F は風、というふうに作り分けてある。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class FloorAmbience : MonoBehaviour
{
    /// <summary>環境音の音量。常時鳴るものなので控えめにする。</summary>
    const float Volume = 0.55f;

    /// <summary>切り替えにかける秒数。突然変わると場面転換に聞こえてしまう。</summary>
    const float FadeSeconds = 2.0f;

    AudioSource source;
    AudioClip pending;
    float fadeTimer;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 環境音は頭の中で鳴らす（2D）
        source.volume = 0f;
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start() => Apply(SceneManager.GetActiveScene().name);

    void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene.name);

    void Apply(string sceneName)
    {
        var clip = ClipFor(sceneName);
        if (clip == null || clip == source.clip) return;

        pending = clip;
        fadeTimer = 0f;
    }

    static AudioClip ClipFor(string sceneName)
    {
        // Resources から読む。シーンごとにインスペクタで結線して回るより、
        // 名前で引くほうが取りこぼしが無い
        var name = sceneName switch
        {
            "Hospital"          => "Audio/Ambient/Ambient_1F",
            "Hospital2F"        => "Audio/Ambient/Ambient_2F",
            "Hospital3F"        => "Audio/Ambient/Ambient_3F",
            "HospitalBasement"  => "Audio/Ambient/Ambient_Basement",
            _ => null,
        };
        return name == null ? null : Resources.Load<AudioClip>(name);
    }

    void Update()
    {
        if (pending != null)
        {
            // 今鳴っているものを落としてから差し替える
            fadeTimer += Time.unscaledDeltaTime;
            float half = FadeSeconds * 0.5f;

            if (source.clip == null || fadeTimer >= half)
            {
                source.clip = pending;
                source.Play();
                pending = null;
                fadeTimer = 0f;
            }
            else
            {
                source.volume = Mathf.Lerp(Volume, 0f, fadeTimer / half);
                return;
            }
        }

        if (source.isPlaying && source.volume < Volume)
            source.volume = Mathf.MoveTowards(source.volume, Volume,
                                              Time.unscaledDeltaTime * Volume / (FadeSeconds * 0.5f));
    }
}
