using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace PremiumLudo.Editor
{
    [InitializeOnLoad]
    public static class LudoSpriteAtlasBootstrap
    {
        private const string AtlasFolder = "Assets/Atlases";
        private const string AtlasPath = AtlasFolder + "/LudoGameplay.spriteatlas";

        static LudoSpriteAtlasBootstrap()
        {
            EditorApplication.delayCall += EnsureGameplayAtlas;
        }

        private static void EnsureGameplayAtlas()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(AtlasFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Atlases");
            }

            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
            bool created = atlas == null;
            if (created)
            {
                atlas = new SpriteAtlas();
            }

            SpriteAtlasPackingSettings packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                enableRotation = false,
                enableTightPacking = false,
                padding = 4,
            };
            atlas.SetPackingSettings(packingSettings);

            SpriteAtlasTextureSettings textureSettings = new SpriteAtlasTextureSettings
            {
                generateMipMaps = false,
                readable = false,
                sRGB = true,
                filterMode = FilterMode.Bilinear,
            };
            atlas.SetTextureSettings(textureSettings);

            if (created)
            {
                Object[] packables = LoadPackables();
                if (packables.Length > 0)
                {
                    SpriteAtlasExtensions.Add(atlas, packables);
                }

                AssetDatabase.CreateAsset(atlas, AtlasPath);
            }

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        }

        private static Object[] LoadPackables()
        {
            string[] paths =
            {
                "Assets/Resources/board copy.png",
                "Assets/Resources/token_red.png",
                "Assets/Resources/token_green.png",
                "Assets/Resources/token_blue.png",
                "Assets/Resources/token_yellow.png",
            };

            System.Collections.Generic.List<Object> packables = new System.Collections.Generic.List<Object>(paths.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(paths[i]);
                if (asset != null)
                {
                    packables.Add(asset);
                }
            }

            return packables.ToArray();
        }
    }
}
