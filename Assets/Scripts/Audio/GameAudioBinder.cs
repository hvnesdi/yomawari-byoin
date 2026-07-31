using UnityEngine;

/// <summary>
/// 音のクリップを実行時に名前で読み込んで各システムに配る。
///
/// **インスペクタで結線しない。**
/// `AudioSystem` も `HorrorEventSystem` も、クリップを受け取るフィールドは
/// 前から用意されていた。それでも音が鳴らなかったのは、
/// **誰も入れていなかった**から。プレハブを作り直すパス（GameBootstrapBuilder）が
/// あるので、手で結線しても作り直しのたびに消える。
/// 名前で読む方式なら、プレハブが何度作り直されても復旧する。
///
/// 同じ理由で、ここは「足りないものだけ入れる」。
/// 既に入っているものは上書きしない（後から差し替えたものを潰さないため）。
/// </summary>
public class GameAudioBinder : MonoBehaviour
{
    void Awake()
    {
        BindAudioSystem();
        BindHorrorEvents();
    }

    static AudioClip Load(string name)
    {
        var clip = Resources.Load<AudioClip>($"Audio/SE/{name}");
        if (clip == null) Debug.LogWarning($"[GameAudioBinder] Audio/SE/{name} が見つからない");
        return clip;
    }

    void BindAudioSystem()
    {
        var audio = GetComponent<AudioSystem>();
        if (audio == null) return;

        // 時間の節目に鳴らす合図。
        // 合成音声は作れないので、放送のチャイムで代える。
        // 6段階すべてに同じチャイムを入れる（文言は UI が出す）
        var chime = Load("SE_AnnounceChime");
        if (audio.announce90 == null) audio.announce90 = chime;
        if (audio.announce60 == null) audio.announce60 = chime;
        if (audio.announce30 == null) audio.announce30 = chime;
        if (audio.announce10 == null) audio.announce10 = chime;
        if (audio.announce5  == null) audio.announce5  = chime;
        if (audio.announce0  == null) audio.announce0  = chime;
    }

    void BindHorrorEvents()
    {
        var horror = GetComponent<HorrorEventSystem>();
        if (horror == null) return;

        if (horror.footstepsClip  == null) horror.footstepsClip  = Load("SE_DistantFootsteps");
        if (horror.nameCallClip   == null) horror.nameCallClip   = Load("SE_NameCall");
        if (horror.tapeScreamClip == null) horror.tapeScreamClip = Load("SE_TapeScream");
        if (horror.backVoiceClip  == null) horror.backVoiceClip  = Load("SE_BackVoice");
        if (horror.suddenNoiseClip == null) horror.suddenNoiseClip = Load("SE_SuddenNoise");
    }
}
