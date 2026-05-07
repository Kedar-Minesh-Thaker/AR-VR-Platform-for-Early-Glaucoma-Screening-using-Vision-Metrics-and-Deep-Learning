// UIStyleKit.cs
// Shared styling utilities for the DhrishtiLite clinical UI.
// Provides a curated color palette, procedural textures (rounded rectangles,
// gradients), and animation helpers used by all UI components.
//
// Usage:  UIStyleKit.Colors.Accent,  UIStyleKit.MakeRoundedRect(…),  etc.

using UnityEngine;
using UnityEngine.UI;

namespace OphthalSuite.Core
{
    public static class UIStyleKit
    {
        // ── Curated Color Palette ────────────────────────────────────────────────
        // ── Curated Color Palette (Midnight Violet Theme) ────────────────────────
        public static class Colors
        {
            // Backgrounds
            public static readonly Color BgDark       = Hex("#0f0a1a"); 
            public static readonly Color BgMid        = Hex("#1a1433"); 
            public static readonly Color BgCard       = new Color(0.12f, 0.10f, 0.24f, 0.92f);
            public static readonly Color BgCardBorder = new Color(0.63f, 0.46f, 0.86f, 0.25f); 
            public static readonly Color BgInput      = new Color(0.08f, 0.06f, 0.14f, 0.95f);

            // Text
            public static readonly Color TextPrimary   = Hex("#e8e0f0");
            public static readonly Color TextSecondary  = Hex("#c8b8dc");
            public static readonly Color TextMuted      = Hex("#9b87b5");
            public static readonly Color TextTitle      = Hex("#b47ae8"); 

            // Accents
            public static readonly Color Accent         = Hex("#9b40d0"); 
            public static readonly Color AccentGlow     = Hex("#7020b0");
            public static readonly Color AccentBright   = Hex("#c860f0");

            // Semantic
            public static readonly Color Success        = Hex("#40d080");
            public static readonly Color Warning        = Hex("#d0a040");
            public static readonly Color Danger         = Hex("#c04040");

            // Buttons
            public static readonly Color BtnPrimary     = Hex("#3a1a6a"); 
            public static readonly Color BtnPrimaryHover = Hex("#6b25b0");
            public static readonly Color BtnPrimaryPress = Hex("#8830c8");
            public static readonly Color BtnDanger      = Hex("#5a1818");
            public static readonly Color BtnDangerHover = Hex("#8a2828");
            public static readonly Color BtnWarning     = Hex("#5a4a10");

            // Toggle
            public static readonly Color ToggleActive   = Hex("#9b40d0");
            public static readonly Color ToggleInactive = Hex("#201a30");

            // Progress
            public static readonly Color ProgressFill   = Hex("#9b40d0");
            public static readonly Color ProgressTrack  = new Color(1f, 1f, 1f, 0.08f);
            public static readonly Color ProgressDone   = Hex("#40d080");

            // Particle
            public static readonly Color ParticleDot    = new Color(0.6f, 0.4f, 0.8f, 0.2f);
        }

        // ── Font Sizes (VR-readable) ─────────────────────────────────────────────
        public static class FontSize
        {
            public const int Title      = 52;
            public const int Subtitle   = 26;
            public const int Heading    = 36;
            public const int Body       = 30;
            public const int Label      = 24;
            public const int Small      = 22;
            public const int Button     = 34;
            public const int ButtonSm   = 26;
            public const int HudLarge   = 34;
            public const int HudSmall   = 24;
        }

        // ── Procedural Textures ──────────────────────────────────────────────────

        /// <summary>Creates a solid color Sprite usable as Image.sprite.</summary>
        public static Sprite MakeSolidSprite(Color color, int size = 4)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Creates a rounded rectangle Sprite with optional border.</summary>
        public static Sprite MakeRoundedRect(int w, int h, int radius, Color fill,
            Color border = default, int borderWidth = 0)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float dist = DistToRoundedRect(x, y, w, h, radius);

                    if (dist <= 0f)
                    {
                        // Inside the rect
                        if (borderWidth > 0 && dist > -borderWidth)
                            tex.SetPixel(x, y, border);
                        else
                            tex.SetPixel(x, y, fill);
                    }
                    else if (dist < 1.5f)
                    {
                        // Anti-aliased edge
                        float alpha = 1f - Mathf.Clamp01(dist);
                        Color edgeColor = borderWidth > 0 ? border : fill;
                        tex.SetPixel(x, y, new Color(edgeColor.r, edgeColor.g, edgeColor.b, edgeColor.a * alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();

            // 9-slice borders for proper scaling
            int border4 = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(border4, border4, border4, border4));
        }

        /// <summary>Creates a vertical gradient Sprite.</summary>
        public static Sprite MakeGradient(Color top, Color bottom, int w = 4, int h = 64)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                Color c = Color.Lerp(bottom, top, t);
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, c);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Creates a circular fill sprite for progress rings.</summary>
        public static Sprite MakeCircleSprite(int size = 64, Color color = default)
        {
            if (color == default) color = Color.white;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float radius = center - 1f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (dist < radius - 1f)
                        tex.SetPixel(x, y, color);
                    else if (dist < radius)
                    {
                        float a = 1f - (dist - (radius - 1f));
                        tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * a));
                    }
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Creates a ring sprite (hollow circle) for progress indicators.</summary>
        public static Sprite MakeRingSprite(int size = 128, float thickness = 6f, Color color = default)
        {
            if (color == default) color = Color.white;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            float center = size / 2f;
            float outerR = center - 1f;
            float innerR = outerR - thickness;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = 0f;

                    if (dist >= innerR && dist <= outerR)
                    {
                        alpha = 1f;
                        // AA on outer edge
                        if (dist > outerR - 1f)
                            alpha *= 1f - (dist - (outerR - 1f));
                        // AA on inner edge
                        if (dist < innerR + 1f)
                            alpha *= dist - innerR;
                    }

                    tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * Mathf.Clamp01(alpha)));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        // ── UI Factory Helpers ───────────────────────────────────────────────────

        /// <summary>Create a RectTransform with anchors.</summary>
        public static GameObject MakeRect(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>Create a Text element with curated styling.</summary>
        public static Text MakeText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax,
            string content, int fontSize, TextAnchor align, Color color,
            Font font = null)
        {
            var go = MakeRect(parent, name, anchorMin, anchorMax);
            var t = go.AddComponent<Text>();
            t.font = font;
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (t.font == null) t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (t.font == null) t.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            
            t.text = content;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>Create a styled Image element.</summary>
        public static Image MakeImage(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Sprite sprite, Color color)
        {
            var go = MakeRect(parent, name, anchorMin, anchorMax);
            var img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
            }
            img.color = color;
            return img;
        }

        // ── Animation Helpers ────────────────────────────────────────────────────

        /// <summary>Smooth ease-out curve (fast start, slow end).</summary>
        public static float EaseOut(float t) => 1f - Mathf.Pow(1f - t, 3f);

        /// <summary>Smooth ease-in curve (slow start, fast end).</summary>
        public static float EaseIn(float t) => t * t * t;

        /// <summary>Smooth ease-in-out curve.</summary>
        public static float EaseInOut(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        /// <summary>Overshoot curve for bouncy animations.</summary>
        public static float Overshoot(float t)
        {
            const float c = 1.70158f;
            const float c3 = c + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
        }

        /// <summary>Sine pulse (0 → 1 → 0 over duration).</summary>
        public static float PulseSine(float time, float period = 2f) =>
            (Mathf.Sin(time * 2f * Mathf.PI / period - Mathf.PI / 2f) + 1f) * 0.5f;

        // ── Internal ─────────────────────────────────────────────────────────────

        private static float DistToRoundedRect(int px, int py, int w, int h, int r)
        {
            // Corner regions
            int rx = (px < r) ? r : (px >= w - r) ? w - r - 1 : px;
            int ry = (py < r) ? r : (py >= h - r) ? h - r - 1 : py;

            if (rx == px && ry == py) return -r; // well inside

            float dx = px - rx;
            float dy = py - ry;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
