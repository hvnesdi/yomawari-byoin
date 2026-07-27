using UnityEngine;

/// <summary>
/// M3 の雰囲気パスが「元の強度」を覚えておくための記録用コンポーネント。
///
/// これが無いと、パスを再実行するたびに intensity を掛け算してしまい、
/// 走らせるほど暗くなっていく（冪等でない）。
/// シーンに保存される必要があるので ScriptableObject ではなくコンポーネントにしている。
/// </summary>
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
public class LightBaseIntensity : MonoBehaviour
{
    [Tooltip("雰囲気パス適用前の Light.intensity")]
    public float baseIntensity = -1f;
}
