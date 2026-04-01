#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PremiumLudo.Editor
{
    public static class LudoBuild
    {
        private const string DefaultIosBuildPath = "Builds/iOS";
        private const string DefaultWebGlBuildPath = "website/ludo/unity";

        [MenuItem("Premium Ludo/Build/iOS Xcode Project")]
        public static void BuildIosXcodeProject()
        {
            BuildIos(DefaultIosBuildPath);
        }

        [MenuItem("Premium Ludo/Build/Prepare Fast WebGL Settings")]
        public static void PrepareFastWebGlSettingsMenu()
        {
            PrepareFastWebGlSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Applied WebGL-focused performance settings for Premium Ludo.");
        }

        [MenuItem("Premium Ludo/Build/WebGL Website Build")]
        public static void BuildWebGlWebsite()
        {
            PrepareFastWebGlSettings();
            BuildWebGl(DefaultWebGlBuildPath);
        }

        public static void BuildIosXcodeProjectCli()
        {
            BuildIos(DefaultIosBuildPath);
            EditorApplication.Exit(0);
        }

        public static void BuildWebGlWebsiteCli()
        {
            PrepareFastWebGlSettings();
            BuildWebGl(DefaultWebGlBuildPath);
            EditorApplication.Exit(0);
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

        public static void BuildWebGl(string outputPath)
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
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("WebGL build failed: " + report.summary.result);
            }

            CleanupRedundantWebGlFiles(absoluteOutputPath);
            WriteWebGlManifest(absoluteOutputPath);
            Debug.Log("WebGL website build exported to: " + absoluteOutputPath);
        }

        private static void PrepareFastWebGlSettings()
        {
            PlayerSettings.runInBackground = false;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.gcIncremental = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.WebGL, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.WebGL.analyzeBuildSize = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.initialMemorySize = 64;
            PlayerSettings.WebGL.maximumMemorySize = 512;

            QualitySettings.SetQualityLevel(1, true);
        }

        private static void WriteWebGlManifest(string outputPath)
        {
            string buildFolder = Path.Combine(outputPath, "Build");
            if (!Directory.Exists(buildFolder))
            {
                return;
            }

            string loaderUrl = FindRelativeAsset(buildFolder, "*.loader.js");
            string dataUrl = FindRelativeAsset(buildFolder, "*.data*");
            string frameworkUrl = FindRelativeAsset(buildFolder, "*.framework.js*");
            string codeUrl = FindRelativeAsset(buildFolder, "*.wasm*");

            string symbolsUrl = FindRelativeAsset(buildFolder, "*.symbols.json*");
            string streamingAssetsUrl = Directory.Exists(Path.Combine(outputPath, "StreamingAssets")) ? "StreamingAssets" : string.Empty;
            string templateDataExists = Directory.Exists(Path.Combine(outputPath, "TemplateData")) ? "true" : "false";

            string json = "{\n"
                + "  \"companyName\": " + JsonString(PlayerSettings.companyName) + ",\n"
                + "  \"productName\": " + JsonString(PlayerSettings.productName) + ",\n"
                + "  \"productVersion\": " + JsonString(PlayerSettings.bundleVersion) + ",\n"
                + "  \"loaderUrl\": " + JsonString(loaderUrl) + ",\n"
                + "  \"dataUrl\": " + JsonString(dataUrl) + ",\n"
                + "  \"frameworkUrl\": " + JsonString(frameworkUrl) + ",\n"
                + "  \"codeUrl\": " + JsonString(codeUrl) + ",\n"
                + "  \"symbolsUrl\": " + JsonString(symbolsUrl) + ",\n"
                + "  \"streamingAssetsUrl\": " + JsonString(streamingAssetsUrl) + ",\n"
                + "  \"templateDataPresent\": " + templateDataExists + "\n"
                + "}\n";

            File.WriteAllText(Path.Combine(outputPath, "build-manifest.json"), json);
        }

        private static void CleanupRedundantWebGlFiles(string outputPath)
        {
            DeleteFileIfPresent(Path.Combine(outputPath, ".gitkeep"));
            DeleteFileIfPresent(Path.Combine(outputPath, "index.html"));
            DeleteDirectoryIfPresent(Path.Combine(outputPath, "TemplateData"));
            DeleteFileIfPresent(Path.Combine(outputPath, "Build", ".gitkeep"));
        }

        private static void DeleteFileIfPresent(string filePath)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        private static void DeleteDirectoryIfPresent(string directoryPath)
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }

        private static string FindRelativeAsset(string buildFolder, string searchPattern)
        {
            string[] matches = Directory.GetFiles(buildFolder, searchPattern, SearchOption.TopDirectoryOnly);
            if (matches == null || matches.Length == 0)
            {
                return string.Empty;
            }

            string bestMatch = matches.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (string.IsNullOrEmpty(bestMatch))
            {
                return string.Empty;
            }

            return "Build/" + Path.GetFileName(bestMatch);
        }

        private static string JsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
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
