#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace PremiumLudo.Editor
{
    public sealed class LudoWebGlAssetOptimizer : AssetPostprocessor
    {
        private const string ResourcesPrefix = "Assets/Resources/";

        private void OnPreprocessTexture()
        {
            if (!(assetImporter is TextureImporter importer))
            {
                return;
            }

            if (!assetPath.StartsWith(ResourcesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool isBoard = fileName == "board" || fileName == "board copy" || fileName == "board_copy";
            bool isToken = fileName == "token" || fileName.StartsWith("token_", StringComparison.Ordinal);
            if (!isBoard && !isToken)
            {
                return;
            }

            ApplyWebGlSettings(importer, isBoard ? 2048 : 512);
            ApplyStandaloneSettings(importer, isBoard ? 2048 : 1024);
        }

        private static void ApplyWebGlSettings(TextureImporter importer, int maxTextureSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("WebGL");
            settings.name = "WebGL";
            settings.overridden = true;
            settings.maxTextureSize = maxTextureSize;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.crunchedCompression = false;
            settings.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(settings);
        }

        private static void ApplyStandaloneSettings(TextureImporter importer, int maxTextureSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings("Standalone");
            settings.name = "Standalone";
            settings.overridden = true;
            settings.maxTextureSize = maxTextureSize;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            settings.crunchedCompression = false;
            settings.format = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(settings);
        }
    }
}
#endif
