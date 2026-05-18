using UnityEngine;

/// <summary>
/// シーン遷移時にプレイヤーのスポーン位置を渡す静的コンテナ。
/// DontDestroyOnLoad不要・シーンロードをまたいでも保持される。
/// </summary>
public static class SceneSpawnData
{
    public static Vector3 SpawnPosition = new Vector3(0, 1f, 0);
    public static bool HasCustomSpawn = false;
}
