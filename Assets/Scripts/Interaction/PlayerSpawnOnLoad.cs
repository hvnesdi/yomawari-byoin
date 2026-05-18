using UnityEngine;

/// <summary>
/// シーンロード時にSceneSpawnDataからスポーン位置を読んでプレイヤーを配置する。
/// PlayerコントローラーのGameObjectに付けること。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerSpawnOnLoad : MonoBehaviour
{
    void Start()
    {
        if (!SceneSpawnData.HasCustomSpawn) return;

        var cc = GetComponent<CharacterController>();
        cc.enabled = false;
        transform.position = SceneSpawnData.SpawnPosition;
        cc.enabled = true;

        SceneSpawnData.HasCustomSpawn = false;
    }
}
