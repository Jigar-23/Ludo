using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PremiumLudo
{
    public static class LudoUtility
    {
        private static Font s_RuntimeFont;

        public static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        public static RectTransform CreateUIObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (parent != null)
            {
                rectTransform.SetParent(parent, false);
            }

            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.anchoredPosition3D = Vector3.zero;
            return rectTransform;
        }

        public static void Stretch(RectTransform rectTransform, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        public static void Center(RectTransform rectTransform, Vector2 size)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = size;
            rectTransform.localScale = Vector3.one;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rectTransform = CreateUIObject(name, parent);
            Image image = GetOrAddComponent<Image>(rectTransform.gameObject);
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            return image;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, FontStyle fontStyle, TextAnchor alignment, Color color)
        {
            RectTransform rectTransform = CreateUIObject(name, parent);
            Text text = GetOrAddComponent<Text>(rectTransform.gameObject);
            text.font = GetRuntimeFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Font GetRuntimeFont()
        {
            if (s_RuntimeFont != null)
            {
                return s_RuntimeFont;
            }

            s_RuntimeFont = TryLoadBuiltinFont("LegacyRuntime.ttf");
            if (s_RuntimeFont != null)
            {
                return s_RuntimeFont;
            }

            s_RuntimeFont = TryLoadBuiltinFont("Arial.ttf");
            if (s_RuntimeFont != null)
            {
                return s_RuntimeFont;
            }

            s_RuntimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica", "Verdana", "Tahoma" }, 16);
            return s_RuntimeFont;
        }

        private static Font TryLoadBuiltinFont(string fontName)
        {
            try
            {
                return Resources.GetBuiltinResource<Font>(fontName);
            }
            catch
            {
                return null;
            }
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static Color MultiplyRGB(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
        }
    }

    public static class LudoTheme
    {
        public static readonly Color Background = Hex("FAF3E8");
        public static readonly Color BackgroundDeep = Hex("D9C7B7");
        public static readonly Color BackgroundWarm = Hex("FFE2C3");
        public static readonly Color BackgroundCool = Hex("C7D9FF");
        public static readonly Color Panel = Hex("FFF7F0");
        public static readonly Color PanelDeep = Hex("EADCCF");
        public static readonly Color BoardBase = Hex("FBF6EF");
        public static readonly Color BoardLowlight = Hex("E7D6C4");
        public static readonly Color BoardShadow = new Color(0.22f, 0.16f, 0.11f, 0.18f);
        public static readonly Color BoardShadowSoft = new Color(0.17f, 0.12f, 0.08f, 0.10f);
        public static readonly Color InnerShadow = new Color(0.17f, 0.12f, 0.09f, 0.18f);
        public static readonly Color TextPrimary = Hex("3F352E");
        public static readonly Color TextMuted = Hex("7A695E");
        public static readonly Color Human = Hex("F2685C");
        public static readonly Color HumanSoft = Hex("F7B0A9");
        public static readonly Color AI = Hex("4D88FF");
        public static readonly Color AISoft = Hex("AFCCFF");
        public static readonly Color AccentWarm = Hex("F6BE66");
        public static readonly Color AccentGreen = Hex("7DC9A6");
        public static readonly Color Safe = Hex("F7D47E");
        public static readonly Color NeutralCell = Hex("F8F1E7");
        public static readonly Color NeutralPath = Hex("FFF9F3");
        public static readonly Color HighlightGloss = new Color(1f, 1f, 1f, 0.24f);
        public static readonly Color ShadowSoft = new Color(0.15f, 0.11f, 0.08f, 0.10f);
        public static readonly Color TokenShadow = new Color(0.08f, 0.05f, 0.04f, 0.16f);
        public static readonly Color ReadyGlow = new Color(1f, 0.82f, 0.46f, 0.30f);

        private static Color Hex(string hex)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString("#" + hex, out color))
            {
                return Color.white;
            }

            return color;
        }

        public static Color GetPlayerColor(LudoPlayerId playerId)
        {
            return playerId == LudoPlayerId.Human ? Human : AI;
        }

        public static Color GetPlayerSoftColor(LudoPlayerId playerId)
        {
            return playerId == LudoPlayerId.Human ? HumanSoft : AISoft;
        }

        public static Color GetTokenTint(LudoTokenColor tokenColor)
        {
            switch (tokenColor)
            {
                case LudoTokenColor.Red:
                    return new Color(1f, 0.3f, 0.3f, 1f);
                case LudoTokenColor.Green:
                    return new Color(0.3f, 0.8f, 0.5f, 1f);
                case LudoTokenColor.Blue:
                    return new Color(0.3f, 0.5f, 1f, 1f);
                default:
                    return new Color(1f, 0.85f, 0.3f, 1f);
            }
        }
    }

    public static class LudoArtLibrary
    {
        private static readonly Dictionary<string, Sprite> s_PathCache = new Dictionary<string, Sprite>(16);
        private static readonly Dictionary<Texture2D, Sprite> s_TextureSpriteCache = new Dictionary<Texture2D, Sprite>(16);
        private static readonly Dictionary<Sprite, Sprite> s_TrimmedSpriteCache = new Dictionary<Sprite, Sprite>(8);
        private static Sprite[] s_AllSprites;
        private static Texture2D[] s_AllTextures;

        public static Sprite GetBoardSprite()
        {
            Sprite sprite = TryLoadSprite(
                "board",
                "ludo_board",
                "ludo-board",
                "board_image",
                "vecteezy_ludo-board-game-vector-illustration_35706384");

            if (sprite != null)
            {
                return sprite;
            }

            return FindSpriteByNameFragments("board", "ludo") ?? FindFirstAvailableSprite();
        }

        public static Sprite GetTokenSprite(LudoTokenColor tokenColor)
        {
            string colorName = tokenColor.ToString().ToLowerInvariant();
            Sprite sprite = TryLoadSprite(
                "token_" + colorName,
                "tokens/token_" + colorName,
                colorName + "_token",
                "tokens/" + colorName,
                colorName);

            if (sprite != null)
            {
                return GetOrCreateTrimmedSprite(sprite);
            }

            sprite = FindSpriteByNameFragments("token_" + colorName) ?? FindSpriteByNameFragments("token", colorName) ?? FindSpriteByNameFragments(colorName);
            if (sprite != null)
            {
                return GetOrCreateTrimmedSprite(sprite);
            }

            sprite = TryLoadSprite(
                "token",
                "tokens/token");

            if (sprite != null)
            {
                return GetOrCreateTrimmedSprite(sprite);
            }

            sprite = FindSpriteByNameFragments("token");
            return GetOrCreateTrimmedSprite(sprite);
        }

        private static Sprite TryLoadSprite(params string[] resourcePaths)
        {
            for (int i = 0; i < resourcePaths.Length; i++)
            {
                Sprite sprite = LoadSpriteAtPath(resourcePaths[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static Sprite LoadSpriteAtPath(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            Sprite cachedSprite;
            if (s_PathCache.TryGetValue(resourcePath, out cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                s_PathCache[resourcePath] = sprite;
                return sprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                sprite = GetOrCreateSprite(texture);
                s_PathCache[resourcePath] = sprite;
                return sprite;
            }

            return null;
        }

        private static Sprite FindSpriteByNameFragments(params string[] fragments)
        {
            EnsureResourceCatalogLoaded();

            for (int i = 0; i < s_AllSprites.Length; i++)
            {
                Sprite sprite = s_AllSprites[i];
                if (sprite != null && NameMatches(sprite.name, fragments))
                {
                    return sprite;
                }
            }

            for (int i = 0; i < s_AllTextures.Length; i++)
            {
                Texture2D texture = s_AllTextures[i];
                if (texture != null && NameMatches(texture.name, fragments))
                {
                    return GetOrCreateSprite(texture);
                }
            }

            return null;
        }

        private static Sprite FindFirstAvailableSprite()
        {
            EnsureResourceCatalogLoaded();
            if (s_AllSprites.Length > 0 && s_AllSprites[0] != null)
            {
                return s_AllSprites[0];
            }

            if (s_AllTextures.Length > 0 && s_AllTextures[0] != null)
            {
                return GetOrCreateSprite(s_AllTextures[0]);
            }

            return null;
        }

        private static void EnsureResourceCatalogLoaded()
        {
            s_AllSprites = Resources.LoadAll<Sprite>(string.Empty);
            s_AllTextures = Resources.LoadAll<Texture2D>(string.Empty);
        }

        private static bool NameMatches(string name, string[] fragments)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string normalizedName = name.ToLowerInvariant();
            for (int i = 0; i < fragments.Length; i++)
            {
                if (string.IsNullOrEmpty(fragments[i]))
                {
                    continue;
                }

                if (normalizedName.IndexOf(fragments[i].ToLowerInvariant(), StringComparison.Ordinal) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static Sprite GetOrCreateSprite(Texture2D texture)
        {
            if (texture == null)
            {
                return null;
            }

            Sprite sprite;
            if (s_TextureSpriteCache.TryGetValue(texture, out sprite) && sprite != null)
            {
                return sprite;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            s_TextureSpriteCache[texture] = sprite;
            return sprite;
        }

        private static Sprite GetOrCreateTrimmedSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }

            Sprite cachedSprite;
            if (s_TrimmedSpriteCache.TryGetValue(sprite, out cachedSprite) && cachedSprite != null)
            {
                return cachedSprite;
            }

            Sprite trimmedSprite = TryTrimSprite(sprite);
            if (trimmedSprite == null)
            {
                trimmedSprite = sprite;
            }

            s_TrimmedSpriteCache[sprite] = trimmedSprite;
            return trimmedSprite;
        }

        private static Sprite TryTrimSprite(Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            if (texture == null || !texture.isReadable)
            {
                return sprite;
            }

            Rect textureRect = sprite.textureRect;
            int rectX = Mathf.RoundToInt(textureRect.x);
            int rectY = Mathf.RoundToInt(textureRect.y);
            int rectWidth = Mathf.RoundToInt(textureRect.width);
            int rectHeight = Mathf.RoundToInt(textureRect.height);
            if (rectWidth <= 0 || rectHeight <= 0)
            {
                return sprite;
            }

            Color[] pixels;
            try
            {
                pixels = texture.GetPixels(rectX, rectY, rectWidth, rectHeight);
            }
            catch
            {
                return sprite;
            }

            int minX = rectWidth;
            int minY = rectHeight;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < rectHeight; y++)
            {
                int rowIndex = y * rectWidth;
                for (int x = 0; x < rectWidth; x++)
                {
                    if (pixels[rowIndex + x].a <= 0.01f)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            if (maxX < 0 || maxY < 0)
            {
                return sprite;
            }

            if (minX == 0 && minY == 0 && maxX == rectWidth - 1 && maxY == rectHeight - 1)
            {
                return sprite;
            }

            Rect trimmedRect = new Rect(
                rectX + minX,
                rectY + minY,
                (maxX - minX) + 1,
                (maxY - minY) + 1);

            Sprite trimmedSprite = Sprite.Create(
                texture,
                trimmedRect,
                new Vector2(0.5f, 0.5f),
                sprite.pixelsPerUnit,
                0u,
                SpriteMeshType.Tight);
            trimmedSprite.name = sprite.name;
            return trimmedSprite;
        }
    }

    public static class LudoSpriteFactory
    {
        private static Sprite s_BackdropGradient;
        private static Sprite s_RoundedMask;
        private static Sprite s_RoundedGloss;
        private static Sprite s_RoundedGradient;
        private static Sprite s_RoundedInnerShadow;
        private static Sprite s_CircleMask;
        private static Sprite s_CircleGloss;
        private static Sprite s_CircleGradient;
        private static Sprite s_CircleInnerShadow;
        private static Sprite s_SoftDisc;

        public static Sprite BackdropGradient
        {
            get
            {
                if (s_BackdropGradient == null)
                {
                    s_BackdropGradient = CreateBackdropGradientSprite(128);
                }

                return s_BackdropGradient;
            }
        }

        public static Sprite RoundedMask
        {
            get
            {
                if (s_RoundedMask == null)
                {
                    s_RoundedMask = CreateRoundedMaskSprite(96, 0.24f);
                }

                return s_RoundedMask;
            }
        }

        public static Sprite RoundedGloss
        {
            get
            {
                if (s_RoundedGloss == null)
                {
                    s_RoundedGloss = CreateRoundedGlossSprite(96, 0.24f);
                }

                return s_RoundedGloss;
            }
        }

        public static Sprite RoundedGradient
        {
            get
            {
                if (s_RoundedGradient == null)
                {
                    s_RoundedGradient = CreateRoundedGradientSprite(96, 0.24f);
                }

                return s_RoundedGradient;
            }
        }

        public static Sprite RoundedInnerShadow
        {
            get
            {
                if (s_RoundedInnerShadow == null)
                {
                    s_RoundedInnerShadow = CreateRoundedInnerShadowSprite(96, 0.24f);
                }

                return s_RoundedInnerShadow;
            }
        }

        public static Sprite CircleMask
        {
            get
            {
                if (s_CircleMask == null)
                {
                    s_CircleMask = CreateCircleMaskSprite(128);
                }

                return s_CircleMask;
            }
        }

        public static Sprite CircleGloss
        {
            get
            {
                if (s_CircleGloss == null)
                {
                    s_CircleGloss = CreateCircleGlossSprite(128);
                }

                return s_CircleGloss;
            }
        }

        public static Sprite CircleGradient
        {
            get
            {
                if (s_CircleGradient == null)
                {
                    s_CircleGradient = CreateCircleGradientSprite(128);
                }

                return s_CircleGradient;
            }
        }

        public static Sprite CircleInnerShadow
        {
            get
            {
                if (s_CircleInnerShadow == null)
                {
                    s_CircleInnerShadow = CreateCircleInnerShadowSprite(128);
                }

                return s_CircleInnerShadow;
            }
        }

        public static Sprite SoftDisc
        {
            get
            {
                if (s_SoftDisc == null)
                {
                    s_SoftDisc = CreateSoftDiscSprite(128);
                }

                return s_SoftDisc;
            }
        }

        private static Sprite CreateBackdropGradientSprite(int size)
        {
            Texture2D texture = CreateTexture("BackdropGradient", size);
            Color[] pixels = new Color[size * size];

            Vector2 warmCenter = new Vector2(0.18f, 0.78f);
            Vector2 coolCenter = new Vector2(0.84f, 0.16f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / (size - 1);
                    float v = (float)y / (size - 1);
                    float diagonal = (u * 0.52f) + ((1f - v) * 0.48f);

                    Color baseColor = Color.Lerp(LudoTheme.Background, LudoTheme.BackgroundDeep, diagonal);

                    float warmGlow = Mathf.Clamp01(1f - (Vector2.Distance(new Vector2(u, v), warmCenter) / 0.72f));
                    float coolGlow = Mathf.Clamp01(1f - (Vector2.Distance(new Vector2(u, v), coolCenter) / 0.78f));

                    Color color = baseColor;
                    color += LudoUtility.WithAlpha(LudoTheme.BackgroundWarm, 1f) * (warmGlow * 0.10f);
                    color += LudoUtility.WithAlpha(LudoTheme.BackgroundCool, 1f) * (coolGlow * 0.08f);
                    color.r = Mathf.Clamp01(color.r);
                    color.g = Mathf.Clamp01(color.g);
                    color.b = Mathf.Clamp01(color.b);
                    color.a = 1f;
                    pixels[(y * size) + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateRoundedMaskSprite(int size, float radiusRatio)
        {
            Texture2D texture = CreateTexture("RoundedMask", size);
            Color[] pixels = new Color[size * size];
            float radius = size * radiusRatio;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = EvaluateRoundedAlpha(x, y, size, radius, 1.6f);
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateRoundedGlossSprite(int size, float radiusRatio)
        {
            Texture2D texture = CreateTexture("RoundedGloss", size);
            Color[] pixels = new Color[size * size];
            float radius = size * radiusRatio;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = EvaluateRoundedAlpha(x, y, size, radius, 1.6f);
                    float u = (float)x / (size - 1);
                    float v = (float)y / (size - 1);
                    float diagonal = Mathf.Clamp01((u * 0.35f) + (v * 0.65f));
                    float sheen = Mathf.SmoothStep(1.0f, 0.0f, diagonal);
                    float edgeFade = Mathf.SmoothStep(0.98f, 0.45f, v);
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha * sheen * edgeFade);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateRoundedGradientSprite(int size, float radiusRatio)
        {
            Texture2D texture = CreateTexture("RoundedGradient", size);
            Color[] pixels = new Color[size * size];
            float radius = size * radiusRatio;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = EvaluateRoundedAlpha(x, y, size, radius, 1.6f);
                    float u = (float)x / (size - 1);
                    float v = (float)y / (size - 1);
                    float diagonal = (u * 0.58f) + ((1f - v) * 0.42f);
                    float centerDistance = Vector2.Distance(new Vector2(u, v), new Vector2(0.36f, 0.72f));
                    float centerBoost = Mathf.Clamp01(1f - (centerDistance / 0.90f));
                    float value = Mathf.Clamp01(0.76f + ((1f - diagonal) * 0.18f) + (centerBoost * 0.12f));
                    pixels[(y * size) + x] = new Color(value, value, value, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateRoundedInnerShadowSprite(int size, float radiusRatio)
        {
            Texture2D texture = CreateTexture("RoundedInnerShadow", size);
            Color[] pixels = new Color[size * size];
            float radius = size * radiusRatio;
            float edgeWidth = size * 0.17f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alphaMask = EvaluateRoundedAlpha(x, y, size, radius, 1.8f);
                    float innerDistance = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float edgeFactor = 1f - Mathf.Clamp01(innerDistance / edgeWidth);
                    edgeFactor = Mathf.SmoothStep(0f, 1f, edgeFactor);
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alphaMask * edgeFactor);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateCircleMaskSprite(int size)
        {
            Texture2D texture = CreateTexture("CircleMask", size);
            Color[] pixels = new Color[size * size];
            float radius = size * 0.45f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = EvaluateCircleAlpha(x, y, size, radius, 2.2f);
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateCircleGlossSprite(int size)
        {
            Texture2D texture = CreateTexture("CircleGloss", size);
            Color[] pixels = new Color[size * size];
            float radius = size * 0.45f;
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = EvaluateCircleAlpha(x, y, size, radius, 2.2f);
                    float dx = (x - half) / radius;
                    float dy = (y - half) / radius;
                    float highlight = Mathf.Clamp01(1f - (((dx + 0.24f) * (dx + 0.24f)) + ((dy - 0.36f) * (dy - 0.36f))));
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha * highlight);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateCircleGradientSprite(int size)
        {
            Texture2D texture = CreateTexture("CircleGradient", size);
            Color[] pixels = new Color[size * size];
            float radius = size * 0.45f;
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = EvaluateCircleAlpha(x, y, size, radius, 2.2f);
                    float dx = (x - half) / radius;
                    float dy = (y - half) / radius;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float radial = Mathf.Clamp01(1f - distance);
                    float highlight = Mathf.Clamp01(1f - (((dx + 0.18f) * (dx + 0.18f)) + ((dy - 0.22f) * (dy - 0.22f))));
                    float value = Mathf.Clamp01(0.68f + (radial * 0.24f) + (highlight * 0.12f));
                    pixels[(y * size) + x] = new Color(value, value, value, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateCircleInnerShadowSprite(int size)
        {
            Texture2D texture = CreateTexture("CircleInnerShadow", size);
            Color[] pixels = new Color[size * size];
            float radius = size * 0.45f;
            float half = (size - 1) * 0.5f;
            float rimWidth = size * 0.14f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    float alpha = EvaluateCircleAlpha(x, y, size, radius, 2.2f);
                    float rim = Mathf.Clamp01((distance - (radius - rimWidth)) / rimWidth);
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha * rim);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Sprite CreateSoftDiscSprite(int size)
        {
            Texture2D texture = CreateTexture("SoftDisc", size);
            Color[] pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;
            float sigma = size * 0.22f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float distanceSq = (dx * dx) + (dy * dy);
                    float alpha = Mathf.Exp(-distanceSq / (2f * sigma * sigma));
                    pixels[(y * size) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return CreateSprite(texture);
        }

        private static Texture2D CreateTexture(string name, int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = name;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        }

        private static float EvaluateRoundedAlpha(int x, int y, int size, float radius, float edgeSoftness)
        {
            float half = size * 0.5f;
            float localX = Mathf.Abs((x + 0.5f) - half) - (half - radius);
            float localY = Mathf.Abs((y + 0.5f) - half) - (half - radius);
            float dx = Mathf.Max(localX, 0f);
            float dy = Mathf.Max(localY, 0f);
            float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
            return 1f - Mathf.Clamp01((distance - radius) / edgeSoftness);
        }

        private static float EvaluateCircleAlpha(int x, int y, int size, float radius, float edgeSoftness)
        {
            float half = (size - 1) * 0.5f;
            float dx = x - half;
            float dy = y - half;
            float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
            return 1f - Mathf.Clamp01((distance - radius) / edgeSoftness);
        }
    }
}
