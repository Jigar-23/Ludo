#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace PremiumLudo.Editor
{
    public static class LudoBuild
    {
        private const string DefaultIosBuildPath = "Builds/iOS";

        [MenuItem("Premium Ludo/Build/iOS Xcode Project")]
        public static void BuildIosXcodeProject()
        {
            BuildIos(DefaultIosBuildPath);
        }

        public static void BuildIos(string outputPath)
        {
            string[] scenes = GetEnabledScenes();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes were found in EditorBuildSettings.");
            }

            string absoluteOutputPath = Path.GetFullPath(outputPath);
            if (!Directory.Exists(absoluteOutputPath))
            {
                Directory.CreateDirectory(absoluteOutputPath);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = absoluteOutputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("iOS build failed: " + report.summary.result);
            }

            UnityEngine.Debug.Log("iOS Xcode project exported to: " + absoluteOutputPath);
        }

        private static string[] GetEnabledScenes()
        {
            var scenePaths = new System.Collections.Generic.List<string>();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] != null && scenes[i].enabled && !string.IsNullOrEmpty(scenes[i].path))
                {
                    scenePaths.Add(scenes[i].path);
                }
            }

            return scenePaths.ToArray();
        }
    }
}
#endif
