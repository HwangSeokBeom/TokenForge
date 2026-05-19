using System.Collections.Generic;
using TokenForge.Client.Domain;
using UnityEngine;

namespace TokenForge.Client.UI
{
    public static class CompanionPixelArtFactory
    {
        private const int SpriteSize = 24;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite GetSprite(CompanionState state, bool walkFrame)
        {
            state = CompanionProgressionRules.Normalize(state);
            var key = state.Stage + ":" + state.Archetype + ":" + walkFrame;
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "CompanionPixel_" + key
            };
            Clear(texture);
            DrawStage(texture, state, walkFrame);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), SpriteSize);
            sprite.name = texture.name;
            Cache[key] = sprite;
            return sprite;
        }

        private static void DrawStage(Texture2D texture, CompanionState state, bool walkFrame)
        {
            var accent = AccentFor(state.Archetype);
            var outline = new Color32(34, 38, 48, 255);
            var highlight = new Color32(255, 244, 210, 255);
            var body = BodyFor(state);
            switch (state.Stage)
            {
                case CompanionStage.Egg:
                    Ellipse(texture, 7, 5, 16, 20, outline);
                    Ellipse(texture, 8, 6, 15, 19, body);
                    Pixel(texture, 11, 9, highlight);
                    Pixel(texture, 12, 8, highlight);
                    break;
                case CompanionStage.Hatching:
                    Ellipse(texture, 6, 5, 17, 20, outline);
                    Ellipse(texture, 7, 6, 16, 19, body);
                    Line(texture, 10, 7, 13, 10, outline);
                    Line(texture, 13, 10, 10, 13, outline);
                    Line(texture, 10, 13, 14, 16, outline);
                    Pixel(texture, walkFrame ? 16 : 15, 8, accent);
                    break;
                default:
                    DrawCreature(texture, state, walkFrame, body, accent, outline, highlight);
                    break;
            }
        }

        private static void DrawCreature(Texture2D texture, CompanionState state, bool walkFrame, Color32 body, Color32 accent, Color32 outline, Color32 highlight)
        {
            var footOffset = walkFrame ? 1 : 0;
            Ellipse(texture, 6, 6, 17, 18, outline);
            Ellipse(texture, 7, 7, 16, 17, body);
            Rect(texture, 8, 15, 15, 20, outline);
            Rect(texture, 9, 15, 14, 19, body);
            Pixel(texture, 10, 11, outline);
            Pixel(texture, 15, 11, outline);
            Pixel(texture, 11, 14, outline);
            Pixel(texture, 12, 15, outline);
            Pixel(texture, 13, 15, outline);
            Pixel(texture, 14, 14, outline);
            Pixel(texture, 9, 8, highlight);

            if (state.Stage == CompanionStage.Junior || state.Stage == CompanionStage.Adult)
            {
                Rect(texture, 5, 12, 7, 15, outline);
                Rect(texture, 17, 12, 19, 15, outline);
                Pixel(texture, 5, 13, accent);
                Pixel(texture, 18, 13, accent);
            }

            if (state.Stage == CompanionStage.Adult)
            {
                Rect(texture, 9, 4, 14, 6, outline);
                Rect(texture, 10, 4, 13, 5, accent);
                Pixel(texture, 6, 7, accent);
                Pixel(texture, 17, 7, accent);
            }

            Rect(texture, 8, 20, 10, 21 + footOffset, outline);
            Rect(texture, 14, 20 - footOffset, 16, 21, outline);
        }

        private static Color32 BodyFor(CompanionState state)
        {
            if (state.Stage == CompanionStage.Egg) return new Color32(244, 224, 178, 255);
            if (state.Stage == CompanionStage.Hatching) return new Color32(255, 202, 126, 255);
            return new Color32(142, 202, 230, 255);
        }

        private static Color32 AccentFor(CompanionArchetype archetype)
        {
            switch (archetype)
            {
                case CompanionArchetype.Explorer: return new Color32(64, 190, 255, 255);
                case CompanionArchetype.Builder: return new Color32(242, 154, 66, 255);
                case CompanionArchetype.Debugger: return new Color32(88, 205, 118, 255);
                case CompanionArchetype.Refiner: return new Color32(237, 132, 196, 255);
                case CompanionArchetype.Architect: return new Color32(154, 169, 255, 255);
                case CompanionArchetype.Sprinter: return new Color32(255, 107, 74, 255);
                default: return new Color32(148, 163, 184, 255);
            }
        }

        private static void Clear(Texture2D texture)
        {
            for (var y = 0; y < SpriteSize; y++)
            {
                for (var x = 0; x < SpriteSize; x++)
                {
                    texture.SetPixel(x, y, new Color32(0, 0, 0, 0));
                }
            }
        }

        private static void Rect(Texture2D texture, int minX, int minY, int maxX, int maxY, Color32 color)
        {
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    Pixel(texture, x, y, color);
                }
            }
        }

        private static void Ellipse(Texture2D texture, int minX, int minY, int maxX, int maxY, Color32 color)
        {
            var cx = (minX + maxX) * 0.5f;
            var cy = (minY + maxY) * 0.5f;
            var rx = Mathf.Max(1f, (maxX - minX) * 0.5f);
            var ry = Mathf.Max(1f, (maxY - minY) * 0.5f);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = (x - cx) / rx;
                    var dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        Pixel(texture, x, y, color);
                    }
                }
            }
        }

        private static void Line(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color)
        {
            var dx = Mathf.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Mathf.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var err = dx + dy;
            while (true)
            {
                Pixel(texture, x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                var e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void Pixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || x >= SpriteSize || y < 0 || y >= SpriteSize)
            {
                return;
            }

            texture.SetPixel(x, SpriteSize - 1 - y, color);
        }
    }
}
