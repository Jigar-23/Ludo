using System;
using UnityEditor;
using UnityEngine;

namespace PremiumLudo.Editor
{
    public sealed class LudoArtImportPostprocessor : AssetPostprocessor
    {
        private const string ResourcesPrefix = "Assets/Resources/";

        private void OnPreprocessTexture()
        {
            if (!(assetImporter is TextureImporter importer))
            {
                return;
            }

            if (!IsLudoSpriteAsset(assetPath))
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;

            TextureImporterSettings importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            importerSettings.spriteMeshType = IsTokenAsset(assetPath) ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            importer.SetTextureSettings(importerSettings);

            ApplyPlatformSettings(importer, "DefaultTexturePlatform");
            ApplyPlatformSettings(importer, "Standalone");
            ApplyPlatformSettings(importer, "Android");
            ApplyPlatformSettings(importer, "iPhone");
            ApplyPlatformSettings(importer, "WebGL");
        }

        private static void ApplyPlatformSettings(TextureImporter importer, string platformName)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            settings.name = platformName;
            settings.overridden = true;
            settings.maxTextureSize = 4096;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.crunchedCompression = false;
            settings.format = TextureImporterFormat.RGBA32;
            importer.SetPlatformTextureSettings(settings);
        }

        private static bool IsLudoSpriteAsset(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(ResourcesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return fileName == "board"
                || fileName == "board copy"
                || fileName == "board_copy"
                || fileName == "token"
                || fileName == "token_red"
                || fileName == "token_green"
                || fileName == "token_blue"
                || fileName == "token_yellow";
        }

        private static bool IsTokenAsset(string path)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return fileName == "token" || fileName.StartsWith("token_", StringComparison.Ordinal);
        }
    }
}
