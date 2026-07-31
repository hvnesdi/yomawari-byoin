using System.Linq;
using UnityEngine;

/// <summary>
/// 追われているときの音。見つかった瞬間の合図と、距離で強まる心音。
///
/// 敵に見つかっても**何の音もしなかった**。画面外から近づかれても気づけず、
/// 捕まって初めて分かる。追跡は音で伝えるのが基本で、
/// 「見つかった」「近づいている」の2つが分かれば緊張が成立する。
///
/// 心音は距離で音量と速さを変える。一定だと情報を持たない飾りになる。
/// </summary>
public class ChaseAudio : MonoBehaviour
{
    /// <summary>この距離まで近づくと心音が最大になる。</summary>
    const float NearDistance = 4f;
    /// <summary>この距離より遠いと心音は鳴らさない。</summary>
    const float FarDistance = 22f;

    const float MaxVolume = 0.45f;

    AudioSource heart;
    AudioClip detectClip;
    EnemyController[] enemies;
    bool wasChased;
    float refreshTimer;

    void Start()
    {
        heart = gameObject.AddComponent<AudioSource>();
        heart.clip = Resources.Load<AudioClip>("Audio/SE/SE_Heartbeat");
        heart.loop = true;
        heart.playOnAwake = false;
        heart.spatialBlend = 0f;   // 自分の心臓なので 2D
        heart.volume = 0f;

        detectClip = Resources.Load<AudioClip>("Audio/SE/SE_EnemyDetect");
        RefreshEnemies();
    }

    void RefreshEnemies()
    {
        // シーン内の敵は増えないが、フロア移動で入れ替わる。
        // 毎フレーム探すのは無駄なので、たまに更新する
        enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
    }

    void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 2f;
            RefreshEnemies();
        }
        if (enemies == null || enemies.Length == 0) return;

        // 追ってきている敵のうち、一番近いものを見る
        float nearest = float.MaxValue;
        bool chased = false;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.IsChasing) continue;
            chased = true;
            nearest = Mathf.Min(nearest, Vector3.Distance(transform.position, enemy.transform.position));
        }

        // 見つかった瞬間だけ鳴らす。追われている間ずっと鳴らすと合図にならない
        if (chased && !wasChased && detectClip != null)
            AudioSource.PlayClipAtPoint(detectClip, transform.position, 0.8f);
        wasChased = chased;

        if (heart == null || heart.clip == null) return;

        float target = 0f;
        if (chased && nearest < FarDistance)
        {
            // 近いほど大きく、速く。0..1 に正規化してから使う
            float closeness = Mathf.InverseLerp(FarDistance, NearDistance, nearest);
            target = MaxVolume * closeness;
            heart.pitch = Mathf.Lerp(0.85f, 1.35f, closeness);
        }

        heart.volume = Mathf.MoveTowards(heart.volume, target, Time.deltaTime * 0.8f);

        if (heart.volume > 0.001f && !heart.isPlaying) heart.Play();
        else if (heart.volume <= 0.001f && heart.isPlaying) heart.Stop();
    }
}
