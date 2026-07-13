using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstilCodex.UnityClient.Editor
{
    public static class AstilProjectSetup
    {
        private const string SceneFolder = "Assets/AstilCodex/Scenes";
        private const string MainScenePath = SceneFolder + "/Main.unity";

        [MenuItem("Astil Codex/Create or Refresh Main Scene")]
        public static void CreateMainScene()
        {
            EnsureFolder("Assets/AstilCodex", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Astil Codex Runtime", typeof(AstilRuntimeBootstrap));
            root.transform.position = Vector3.zero;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };

            ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("Astil Codex main scene created and added to Build Settings: " + MainScenePath);
        }

        [MenuItem("Astil Codex/Open Default Avatar Folder")]
        public static void OpenAvatarFolder()
        {
            var localData = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(localData, "AstilCodex", "avatars");
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
        }

        [MenuItem("Astil Codex/Validate Core Host Build")]
        public static void ValidateCoreHost()
        {
            var temporary = new GameObject("Core Host Path Check");
            try
            {
                var launcher = temporary.AddComponent<CoreHostLauncher>();
                var path = launcher.ResolveCoreHostPath();
                if (File.Exists(path))
                {
                    EditorUtility.DisplayDialog(
                        "Astil Codex Core",
                        "Core host found:\n" + path,
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Astil Codex Core",
                        "Core host is not built yet. Run:\n\n" +
                        "dotnet build ..\\AstilCodex.Core.Host\\AstilCodex.Core.Host.csproj " +
                        "--configuration Release\n\nExpected path:\n" + path,
                        "OK");
                }
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Astil Codex Project";
            PlayerSettings.productName = "Astil Codex";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetApiCompatibilityLevel(
                NamedBuildTarget.Standalone,
                ApiCompatibilityLevel.NET_Unity_4_8);
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
