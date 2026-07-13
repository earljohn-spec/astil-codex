using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AstilCodex.UnityClient.Editor
{
    public static class AstilWindowsBuild
    {
        [MenuItem("Astil Codex/Build Windows Development Client")]
        public static void BuildWindowsClient()
        {
            const string scenePath = "Assets/AstilCodex/Scenes/Main.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                EditorUtility.DisplayDialog(
                    "Astil Codex Build",
                    "Create the main scene first with Astil Codex > Create or Refresh Main Scene.",
                    "OK");
                return;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new DirectoryNotFoundException("Unable to resolve Unity project root.");
            }

            var outputFolder = Path.Combine(projectRoot, "Builds", "Windows");
            Directory.CreateDirectory(outputFolder);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = Path.Combine(outputFolder, "AstilCodex.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    "Astil Codex Build",
                    "Unity build failed. Review the Console for details.",
                    "OK");
                return;
            }

            CopyCoreHost(projectRoot, outputFolder);
            EditorUtility.RevealInFinder(outputFolder);
            Debug.Log("Astil Codex Windows development build completed: " + outputFolder);
        }

        private static void CopyCoreHost(string unityProjectRoot, string outputFolder)
        {
            var source = Path.GetFullPath(Path.Combine(
                unityProjectRoot,
                "..",
                "AstilCodex.Core.Host",
                "bin",
                "Release",
                "net8.0"));
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException(
                    "Core host build was not found. Run dotnet build for AstilCodex.Core.Host " +
                    "in Release mode before building Unity. Expected: " + source);
            }

            var destination = Path.Combine(outputFolder, "Core");
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, true);
            }

            CopyDirectory(source, destination);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }

            foreach (var directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }
    }
}
