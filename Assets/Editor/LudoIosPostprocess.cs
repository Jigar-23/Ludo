#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace PremiumLudo.Editor
{
    public static class LudoIosPostprocess
    {
        private const string DefaultDeploymentTarget = "15.0";
        private const string BundleIdentifier = "com.jigar.premiumludo";
        private const string MarketingVersion = "1.0";

        [PostProcessBuild(200)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS || string.IsNullOrEmpty(pathToBuiltProject))
            {
                return;
            }

            string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            PBXProject project = new PBXProject();
            project.ReadFromFile(projectPath);

            string mainTargetGuid = project.GetUnityMainTargetGuid();
            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();

            ApplyBuildSettings(project, mainTargetGuid);
            ApplyBuildSettings(project, frameworkTargetGuid);
            project.WriteToFile(projectPath);

            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            PlistDocument plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            PlistElementDict root = plist.root;
            root.SetString("CFBundleDisplayName", PlayerSettings.productName);
            root.SetString("CFBundleShortVersionString", MarketingVersion);
            root.SetString("CFBundleVersion", PlayerSettings.bundleVersion);
            root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            root.SetBoolean("UIViewControllerBasedStatusBarAppearance", false);
            plist.WriteToFile(plistPath);
        }

        private static void ApplyBuildSettings(PBXProject project, string targetGuid)
        {
            if (string.IsNullOrEmpty(targetGuid))
            {
                return;
            }

            project.SetBuildProperty(targetGuid, "PRODUCT_BUNDLE_IDENTIFIER", BundleIdentifier);
            project.SetBuildProperty(targetGuid, "IPHONEOS_DEPLOYMENT_TARGET", DefaultDeploymentTarget);
            project.SetBuildProperty(targetGuid, "TARGETED_DEVICE_FAMILY", "1,2");
            project.SetBuildProperty(targetGuid, "MARKETING_VERSION", MarketingVersion);
            project.SetBuildProperty(targetGuid, "CURRENT_PROJECT_VERSION", PlayerSettings.bundleVersion);
            project.SetBuildProperty(targetGuid, "CODE_SIGN_STYLE", "Automatic");
            project.SetBuildProperty(targetGuid, "DEVELOPMENT_TEAM", ResolveDevelopmentTeamId());
        }

        private static string ResolveDevelopmentTeamId()
        {
            string teamId = System.Environment.GetEnvironmentVariable("APPLE_TEAM_ID");
            if (!string.IsNullOrWhiteSpace(teamId))
            {
                return teamId.Trim();
            }

            return string.Empty;
        }
    }
}
#endif
