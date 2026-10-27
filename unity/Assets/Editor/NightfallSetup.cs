// C# / Assets/Editor/NightfallSetup.cs / Unity 2022.3+ / one-time project wiring
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nightfall.EditorTools
{
    /// <summary>
    /// The game builds itself from code, so the project ships without hand-written
    /// .unity or ProjectSettings assets. This runs once after import and lets Unity
    /// write those files itself: a scene to press Play on, the product name, and the
    /// default resolution.
    /// </summary>
    [InitializeOnLoad]
    public static class NightfallSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneKey = "Nightfall.SetupDone.v1";

        static NightfallSetup()
        {
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool firstTime = !SessionState.GetBool(DoneKey, false);
            SessionState.SetBool(DoneKey, true);

            EnsureScene();
            if (firstTime) ApplyPlayerSettings();
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                AddSceneToBuild();
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");

            // created additively so an unsaved scene the user is working in is untouched
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
            AddSceneToBuild();

            Debug.Log("NIGHTFALL: created " + ScenePath + " — open it and press Play.");
        }

        static void AddSceneToBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
                if (scenes[i].path == ScenePath) return;

            var list = new EditorBuildSettingsScene[scenes.Length + 1];
            list[0] = new EditorBuildSettingsScene(ScenePath, true);
            for (int i = 0; i < scenes.Length; i++) list[i + 1] = scenes[i];
            EditorBuildSettings.scenes = list;
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Big Games Studio";
            PlayerSettings.productName = "NIGHTFALL";
            PlayerSettings.defaultScreenWidth = S.WIDTH;
            PlayerSettings.defaultScreenHeight = S.HEIGHT;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.SaveAssets();
        }

        [MenuItem("NIGHTFALL/Run self-test")]
        static void RunSelftest()
        {
            Debug.Log(Selftest.RunAll(900));
        }

        [MenuItem("NIGHTFALL/Open save folder")]
        static void OpenSaveFolder()
        {
            EditorUtility.RevealInFinder(Application.persistentDataPath);
        }

        [MenuItem("NIGHTFALL/Delete save file")]
        static void DeleteSave()
        {
            string path = SaveData.Path;
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.Log("NIGHTFALL: deleted " + path);
            }
            else
            {
                Debug.Log("NIGHTFALL: no save file at " + path);
            }
        }
    }
}
