using UnityEngine;

/// <summary>
/// 足音を鳴らす。
///
/// 無音の廊下を歩くと、自分がそこに居る感じがまったく出ない。
/// 足音は「プレイヤーが空間の中に居る」ことを伝える一番安い方法で、
/// 同時に**しゃがみ・歩き・走りの違いを音で返す**役目もある。
/// 走れば大きく速く鳴り、しゃがめばほとんど鳴らない——
/// 隠れる遊びが成立するには、この差が要る。
///
/// 音源は6種類を順不同で使う。1種類を繰り返すと、歩いた瞬間に作り物だと分かる。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FootstepPlayer : MonoBehaviour
{
    /// <summary>歩幅（m）。これだけ進むごとに1歩鳴らす。</summary>
    const float StrideWalk = 0.85f;
    const float StrideRun = 1.15f;    // 走ると歩幅が伸びる
    const float StrideCrouch = 0.65f; // しゃがむと縮む

    const float VolumeWalk = 0.35f;
    const float VolumeRun = 0.55f;
    const float VolumeCrouch = 0.12f;

    CharacterController controller;
    AudioSource source;
    AudioClip[] clips;
    Vector3 lastPosition;
    float distance;
    int lastIndex = -1;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        // 自分の足音なので 2D で鳴らす。3D にすると自分の位置で減衰して不自然になる
        source.spatialBlend = 0f;

        clips = Resources.LoadAll<AudioClip>("Audio/SE/Footsteps");
        lastPosition = transform.position;
    }

    void Update()
    {
        if (clips == null || clips.Length == 0) return;

        // 上下の移動は歩幅に数えない（段差や重力で誤発火する）
        var delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        if (!controller.isGrounded) return;

        float moved = delta.magnitude;
        if (moved < 0.0005f) return;   // 止まっているときの微動は無視

        bool crouching = controller.height < 1.5f;
        bool running = !crouching && Input.GetKey(KeyCode.LeftShift);

        float stride = crouching ? StrideCrouch : (running ? StrideRun : StrideWalk);
        distance += moved;
        if (distance < stride) return;
        distance = 0f;

        // 直前と同じ音を選ばない。連続すると耳に付く
        int index = Random.Range(0, clips.Length);
        if (clips.Length > 1 && index == lastIndex) index = (index + 1) % clips.Length;
        lastIndex = index;

        source.volume = crouching ? VolumeCrouch : (running ? VolumeRun : VolumeWalk);
        source.pitch = Random.Range(0.92f, 1.08f);   // 微妙に高さを変える
        source.PlayOneShot(clips[index]);
    }
}
