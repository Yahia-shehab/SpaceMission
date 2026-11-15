using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class MainMenuSceneCreator
{
    static MainMenuSceneCreator()
    {
        EditorApplication.delayCall += EnsureMainMenuSceneExists;
    }

    [MenuItem("SpaceMission/Build Main Menu Scene")]
    public static void EnsureMainMenuSceneExists()
    {
        string scenePath = "Assets/Scenes/MainMenuScene.unity";
        bool sceneExists = File.Exists(scenePath);

        if (!sceneExists)
        {
            UnityEngine.SceneManagement.Scene activeBefore = EditorSceneManager.GetActiveScene();
            UnityEngine.SceneManagement.Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Create Main Camera
            GameObject camObj = new GameObject("Main Camera");
            Camera cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 5.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.024f, 0.055f, 0.040f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);

            // 2. Create MainMenuManager
            GameObject menuMgrObj = new GameObject("MainMenuManager");
            menuMgrObj.AddComponent<MainMenuManager>();

            // 3. Save Scene
            EditorSceneManager.SaveScene(newScene, scenePath);
            Debug.Log("[MainMenuSceneCreator] Successfully created and saved: " + scenePath);

            // Restore previous active scene if it was SpaceMissionScene
            if (activeBefore.IsValid() && !string.IsNullOrEmpty(activeBefore.path))
            {
                EditorSceneManager.OpenScene(activeBefore.path);
            }
        }

        // 4. Ensure both scenes are in EditorBuildSettings
        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes;
        bool hasMainMenu = false;
        bool hasGameScene = false;
        foreach (var s in currentScenes)
        {
            if (s.path == scenePath) hasMainMenu = true;
            if (s.path == "Assets/Scenes/SpaceMissionScene.unity") hasGameScene = true;
        }

        if (!hasMainMenu || !hasGameScene || currentScenes.Length < 2)
        {
            EditorBuildSettingsScene[] newBuildScenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/SpaceMissionScene.unity", true)
            };
            EditorBuildSettings.scenes = newBuildScenes;
            Debug.Log("[MainMenuSceneCreator] Updated EditorBuildSettings: Scene 0 = MainMenuScene, Scene 1 = SpaceMissionScene.");
        }
    }
}
