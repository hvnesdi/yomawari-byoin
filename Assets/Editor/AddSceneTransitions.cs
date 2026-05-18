using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AddSceneTransitions
{
    [MenuItem("Tools/Add Scene Transitions")]
    public static void AddTransitions()
    {
        AddTransition(
            "Assets/Scenes/Hospital.unity",
            "Hospital2F", AreaID.Floor2F,
            new Vector3(0, 1f, 2f),
            "2Fへ移動中…");

        AddTransition(
            "Assets/Scenes/Hospital2F.unity",
            "Hospital3F", AreaID.Floor3F,
            new Vector3(0, 1f, 2f),
            "3Fへ移動中…");

        AddTransition(
            "Assets/Scenes/Hospital3F.unity",
            "HospitalBasement", AreaID.Basement,
            new Vector3(0, 1f, 2f),
            "地下へ移動中…");
    }

    static void AddTransition(string scenePath, string targetScene, AreaID targetArea,
        Vector3 spawnInTarget, string message)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        foreach (var existing in Object.FindObjectsOfType<SceneTransitionTrigger>())
            Object.DestroyImmediate(existing.gameObject);

        HospitalBuilderUtils.CreateTransitionTrigger(
            "SceneTransitionTrigger_" + targetScene,
            new Vector3(0, 1, 20),
            new Vector3(5, 3, 1),
            targetScene, targetArea, spawnInTarget, message);

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[AddSceneTransitions] {scenePath} -> {targetScene}");
    }

    public static void Run() => AddTransitions();
}
