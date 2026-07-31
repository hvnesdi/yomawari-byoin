using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 移動の速さをアニメーターに渡す。
///
/// **敵も NPC も、姿勢が固まったまま NavMesh の上を滑っていた。**
/// 一定速度で寄ってくる止まった人型は、怖さの手前で「動く置物」に見える。
/// 歩行モーションが付くだけで、追われている感じが出る。
///
/// 速さは NavMeshAgent があればそこから、無ければ位置の変化から取る。
/// NPC の中には Agent を持たず自前で動くものがあるため。
/// </summary>
public class CharacterAnimatorDriver : MonoBehaviour
{
    /// <summary>アニメーター側のパラメータ名。M15 が作るコントローラと合わせてある。</summary>
    const string SpeedParam = "Speed";

    /// <summary>
    /// 速さの追従の鈍さ。生の値をそのまま渡すと、NavMeshAgent の速度が
    /// 細かく揺れるぶんだけ歩きと待機がちらつく。
    /// </summary>
    const float Smoothing = 6f;

    Animator animator;
    NavMeshAgent agent;
    Vector3 lastPosition;
    float speed;
    bool hasParam;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
        lastPosition = transform.position;

        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (var p in animator.parameters)
                if (p.name == SpeedParam && p.type == AnimatorControllerParameterType.Float)
                    hasParam = true;
    }

    void Update()
    {
        if (animator == null || !hasParam) return;

        float raw;
        if (agent != null && agent.isOnNavMesh)
        {
            raw = agent.velocity.magnitude;
        }
        else
        {
            var delta = transform.position - lastPosition;
            delta.y = 0f;   // 段差や重力で速さが跳ねないように
            raw = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        }
        lastPosition = transform.position;

        speed = Mathf.Lerp(speed, raw, Time.deltaTime * Smoothing);
        animator.SetFloat(SpeedParam, speed);
    }
}
