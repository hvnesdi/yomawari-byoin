using UnityEditor;
using UnityEngine;

public class BuildSettingsUpdater
{
    [MenuItem("Tools/Update Build Settings")]
    public static void UpdateBuildSettings()
    {
        var scenes = new[]
        {
            "Assets/Scenes/Hospital.unity",
            "Assets/Scenes/Hospital2F.unity",
            "Assets/Scenes/Hospital3F.unity",
            "Assets/Scenes/HospitalBasement.unity",
        };

        var editorScenes = new EditorBuildSettingsScene[scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
            editorScenes[i] = new EditorBuildSettingsScene(scenes[i], true);

        EditorBuildSettings.scenes = editorScenes;
        Debug.Log("Build Settings updated: 4 scenes registered.");
    }

    public static void Run() => UpdateBuildSettings();
}
