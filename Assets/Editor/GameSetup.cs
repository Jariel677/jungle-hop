using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-side bootstrap: ensures <c>Assets/Scenes/Main.unity</c> exists with a
/// GameManager object and is registered in Build Settings. The GameManager
/// builds the rest of the world at play time.
/// </summary>
[InitializeOnLoad]
public static class GameSetup
{
    const string SceneDir = "Assets/Scenes";
    const string ScenePath = "Assets/Scenes/Main.unity";

    static GameSetup()
    {
        EditorApplication.delayCall += EnsureScene;
    }

    [MenuItem("Tools/Subway Runner/Rebuild Scene")]
    public static void Rebuild()
    {
        BuildScene();
    }

    static void EnsureScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(ScenePath)) BuildScene();
        else EnsureBuildSettings();
    }

    static void BuildScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        EnsureBuildSettings();
        AssetDatabase.Refresh();
        Debug.Log("[GameSetup] Built scene " + ScenePath);
    }

    static void EnsureBuildSettings()
    {
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }
}
