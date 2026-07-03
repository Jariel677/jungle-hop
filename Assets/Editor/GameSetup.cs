using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-side bootstrap. Ensures both game scenes exist and are registered in
/// Build Settings:
///   • <c>Assets/Scenes/Flappy.unity</c> — the Flappy Bird game (build index 0,
///     opened by default so pressing Play runs it).
///   • <c>Assets/Scenes/Main.unity</c>   — the original Subway Runner game.
/// Each scene holds a single bootstrap object; the MonoBehaviour on it builds
/// the rest of the world at play time.
/// </summary>
[InitializeOnLoad]
public static class GameSetup
{
    const string SceneDir = "Assets/Scenes";
    const string MainScenePath = "Assets/Scenes/Main.unity";
    const string FlappyScenePath = "Assets/Scenes/Flappy.unity";

    static GameSetup()
    {
        EditorApplication.delayCall += EnsureScenes;
    }

    // ---- Menu items ---------------------------------------------------------
    [MenuItem("Tools/Flappy Bird/Rebuild Scene")]
    public static void RebuildFlappy()
    {
        BuildFlappyScene();
        EnsureBuildSettings();
    }

    [MenuItem("Tools/Flappy Bird/Open Scene")]
    public static void OpenFlappy()
    {
        if (File.Exists(FlappyScenePath))
            EditorSceneManager.OpenScene(FlappyScenePath, OpenSceneMode.Single);
        else
            RebuildFlappy();
    }

    [MenuItem("Tools/Subway Runner/Rebuild Scene")]
    public static void RebuildSubway()
    {
        BuildSubwayScene();
        EnsureBuildSettings();
    }

    [MenuItem("Tools/Subway Runner/Open Scene")]
    public static void OpenSubway()
    {
        if (File.Exists(MainScenePath))
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
    }

    [MenuItem("Tools/Subway Runner/Debug Start Run")]
    public static void DebugStartRun()
    {
        if (Application.isPlaying && GameManager.Instance != null)
            GameManager.Instance.StartRun();
    }

    // ---- Auto-setup on load -------------------------------------------------
    static void EnsureScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);

        if (!File.Exists(MainScenePath)) BuildSubwayScene();
        if (!File.Exists(FlappyScenePath)) BuildFlappyScene();
        EnsureBuildSettings();

        // Default to the Flappy scene so Play runs the new game, but don't yank
        // the editor away from an unsaved scene the developer is editing.
        var active = EditorSceneManager.GetActiveScene();
        if (active.path != FlappyScenePath && !active.isDirty)
            EditorSceneManager.OpenScene(FlappyScenePath, OpenSceneMode.Single);
    }

    static void BuildFlappyScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("FlappyGame");
        go.AddComponent<FlappyBird>();
        EditorSceneManager.SaveScene(scene, FlappyScenePath);
        Debug.Log("[GameSetup] Built scene " + FlappyScenePath);
    }

    static void BuildSubwayScene()
    {
        if (!Directory.Exists(SceneDir)) Directory.CreateDirectory(SceneDir);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        EditorSceneManager.SaveScene(scene, MainScenePath);
        Debug.Log("[GameSetup] Built scene " + MainScenePath);
    }

    static void EnsureBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(FlappyScenePath, true),
            new EditorBuildSettingsScene(MainScenePath, true),
        };
    }
}
