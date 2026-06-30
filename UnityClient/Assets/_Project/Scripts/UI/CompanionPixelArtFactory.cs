using System;
using System.Collections.Generic;
using System.Linq;
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
            return GetSprite(state, walkFrame, "rat", null, string.Empty, "legacy", string.Empty);
        }

        public static Sprite GetSprite(CompanionState state, bool walkFrame, string zodiacTypeId, IEnumerable<string> equippedItemIds = null)
        {
            return GetSprite(state, walkFrame, zodiacTypeId, equippedItemIds, string.Empty, "legacy", string.Empty);
        }

        public static Sprite GetSprite(CompanionState state, bool walkFrame, string zodiacTypeId, IEnumerable<string> equippedItemIds, string repositoryIdentity, string previewRole, string cosmeticVariant)
        {
            state = CompanionProgressionRules.Normalize(state);
            var key = CanonicalAssetKey(state, walkFrame, zodiacTypeId, equippedItemIds, repositoryIdentity, previewRole, cosmeticVariant);
            if (Cache.TryGetValue(key, out var cached) && cached != null)
            {
                LogPixelDiagnostic(state, zodiacTypeId, equippedItemIds, key, "unityCache", true, repositoryIdentity, previewRole);
                return cached;
            }

            var texture = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "CompanionPixel_" + key.Replace(':', '_').Replace(',', '_')
            };
            Clear(texture);
            DrawStage(texture, state, walkFrame, zodiacTypeId, equippedItemIds);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, SpriteSize, SpriteSize), new Vector2(0.5f, 0.5f), SpriteSize);
            sprite.name = texture.name;
            Cache[key] = sprite;
            LogPixelDiagnostic(state, zodiacTypeId, equippedItemIds, key, "unityFactory", false, repositoryIdentity, previewRole);
            return sprite;
        }

        public static string CanonicalAssetKey(CompanionState state, bool walkFrame, string zodiacTypeId, IEnumerable<string> equippedItemIds = null)
        {
            return CanonicalAssetKey(state, walkFrame, zodiacTypeId, equippedItemIds, string.Empty, "legacy", string.Empty);
        }

        public static string CanonicalAssetKey(CompanionState state, bool walkFrame, string zodiacTypeId, IEnumerable<string> equippedItemIds, string repositoryIdentity, string previewRole, string cosmeticVariant)
        {
            state = CompanionProgressionRules.Normalize(state);
            var zodiac = RepositoryCompanionProfileService.NormalizeZodiacTypeId(zodiacTypeId, state.Archetype.ToString());
            var items = NormalizeItems(equippedItemIds);
            var repo = SanitizeKeyPart(repositoryIdentity, "no-repo");
            var role = SanitizeKeyPart(previewRole, "legacy");
            var variant = SanitizeKeyPart(cosmeticVariant, "default");
            return "signature=sprite:v4:grid24:stage=" + CanonicalStageName(state.Stage) + ":archetype=" + state.Archetype + ":frame=" + (walkFrame ? "walk" : "idle") + ":repo=" + repo + ":role=" + role + ":zodiac=" + zodiac + ":variant=" + variant + ":equippedItemsHash=" + string.Join(",", items);
        }

        public static string SpriteSignature(CompanionState state, bool walkFrame, string zodiacTypeId, IEnumerable<string> equippedItemIds = null)
        {
            return CanonicalAssetKey(state, walkFrame, zodiacTypeId, equippedItemIds);
        }

        public static string SpriteSignature(CompanionState state, bool walkFrame, string zodiacTypeId, IEnumerable<string> equippedItemIds, string repositoryIdentity, string previewRole, string cosmeticVariant)
        {
            return CanonicalAssetKey(state, walkFrame, zodiacTypeId, equippedItemIds, repositoryIdentity, previewRole, cosmeticVariant);
        }

        private static void DrawStage(Texture2D texture, CompanionState state, bool walkFrame, string zodiacTypeId, IEnumerable<string> equippedItemIds)
        {
            var zodiac = RepositoryCompanionProfileService.NormalizeZodiacTypeId(zodiacTypeId, state.Archetype.ToString());
            var palette = PaletteFor(zodiac, state);
            Shadow(texture);
            switch (state.Stage)
            {
                case CompanionStage.Egg:
                    Ellipse(texture, 6, 4, 17, 20, palette.Outline);
                    Ellipse(texture, 7, 5, 16, 19, palette.Body);
                    Pixel(texture, 10, 8, palette.Highlight);
                    Pixel(texture, 11, 7, palette.Highlight);
                    ZodiacMark(texture, zodiac, palette.Accent);
                    break;
                case CompanionStage.Hatchling:
                    Ellipse(texture, 6, 5, 17, 20, palette.Outline);
                    Ellipse(texture, 7, 6, 16, 19, palette.Body);
                    Pixel(texture, 9, 11, palette.Outline);
                    Pixel(texture, 14, 11, palette.Outline);
                    Rect(texture, 10, 15, 13, 16, palette.Outline);
                    DrawZodiacTraits(texture, zodiac, state.Stage, walkFrame, palette);
                    break;
                default:
                    DrawCreature(texture, state, walkFrame, palette);
                    DrawZodiacTraits(texture, zodiac, state.Stage, walkFrame, palette);
                    break;
            }

            DrawCosmetics(texture, NormalizeItems(equippedItemIds), palette);
        }

        private static void LogPixelDiagnostic(CompanionState state, string zodiacTypeId, IEnumerable<string> equippedItemIds, string cacheKey, string generatedFrom, bool reusedCache, string repositoryIdentity, string previewRole)
        {
            var zodiac = RepositoryCompanionProfileService.NormalizeZodiacTypeId(zodiacTypeId, state.Archetype.ToString());
            var equippedItemsHash = string.Join(",", NormalizeItems(equippedItemIds));
            var selectedRepoHash = SanitizeKeyPart(repositoryIdentity, "unity");
            var renderTarget = SanitizeKeyPart(previewRole, "legacy");
            Debug.Log("INFO [PixelDiagnostic] signature=sprite:v4:grid24 selectedRepoHash=" + selectedRepoHash +
                      " repoHash=" + selectedRepoHash +
                      " stageKey=" + state.Stage +
                      " level=" + Math.Max(1, state.Level) +
                      " zodiac=" + zodiac +
                      " zodiacKey=" + zodiac +
                      " equippedItemKeys=" + (string.IsNullOrWhiteSpace(equippedItemsHash) ? "none" : equippedItemsHash) +
                      " equippedCosmetics=" + (string.IsNullOrWhiteSpace(equippedItemsHash) ? "none" : equippedItemsHash) +
                      " wardrobePreviewKey=unitySprite" +
                      " cacheKey=" + cacheKey +
                      " cacheHit=" + reusedCache +
                      " generatedVariant=" + generatedFrom +
                      " stage=" + state.Stage +
                      " stageName=" + state.Stage +
                      " stageIndex=" + (int)state.Stage +
                      " stageVisualSignature=" + StageVisualSignature(state.Stage) +
                      " renderTarget=" + renderTarget +
                      " sourceRenderer=unityPixelArtFactory" +
                      " sideBlockDetected=false" +
                      " bodyShadeMode=contourPattern" +
                      " finalBounds=0,0,24,24" +
                      " clipped=false" +
                      " sourceOfTruth=CompanionPixelArtFactory.CanonicalAssetKey" +
                      " equippedItemsHash=" + (string.IsNullOrWhiteSpace(equippedItemsHash) ? "none" : equippedItemsHash));
        }

        private static void DrawCreature(Texture2D texture, CompanionState state, bool walkFrame, Palette palette)
        {
            var footOffset = walkFrame ? 1 : 0;
            Ellipse(texture, 6, 6, 17, 18, palette.Outline);
            Ellipse(texture, 7, 7, 16, 17, palette.Body);
            Rect(texture, 8, 15, 15, 20, palette.Outline);
            Rect(texture, 9, 15, 14, 19, palette.Body);
            Pixel(texture, 10, 11, palette.Outline);
            Pixel(texture, 15, 11, palette.Outline);
            Pixel(texture, 11, 14, palette.Outline);
            Pixel(texture, 12, 15, palette.Outline);
            Pixel(texture, 13, 15, palette.Outline);
            Pixel(texture, 14, 14, palette.Outline);
            Pixel(texture, 9, 8, palette.Highlight);

            if (state.Stage == CompanionStage.Teen || state.Stage == CompanionStage.Adult || state.Stage == CompanionStage.Legendary)
            {
                Pixel(texture, 7, 14, palette.Highlight);
                Pixel(texture, 16, 14, palette.Accent);
                Pixel(texture, 8, 17, palette.Accent);
                Pixel(texture, 15, 17, palette.Highlight);
            }

            if (state.Stage == CompanionStage.Adult || state.Stage == CompanionStage.Legendary)
            {
                Rect(texture, 9, 4, 14, 6, palette.Outline);
                Rect(texture, 10, 4, 13, 5, palette.Accent);
                Pixel(texture, 6, 7, palette.Accent);
                Pixel(texture, 17, 7, palette.Accent);
            }

            if (state.Stage == CompanionStage.Legendary)
            {
                Pixel(texture, 4, 6, palette.Aura);
                Pixel(texture, 19, 6, palette.Aura);
                Pixel(texture, 3, 13, palette.Aura);
                Pixel(texture, 20, 13, palette.Aura);
            }

            Rect(texture, 8, 20, 10, 21 + footOffset, palette.Outline);
            Rect(texture, 14, 20 - footOffset, 16, 21, palette.Outline);
        }

        private static void DrawZodiacTraits(Texture2D texture, string zodiac, CompanionStage stage, bool walkFrame, Palette palette)
        {
            var tailOffset = walkFrame ? 1 : 0;
            switch (zodiac)
            {
                case "rat":
                    Ellipse(texture, 5, 5, 8, 8, palette.Outline);
                    Ellipse(texture, 6, 6, 7, 7, palette.Accent);
                    Ellipse(texture, 15, 5, 18, 8, palette.Outline);
                    Ellipse(texture, 16, 6, 17, 7, palette.Accent);
                    Line(texture, 8, 13, 4, 12, palette.Highlight);
                    Line(texture, 15, 13, 19, 12, palette.Highlight);
                    Line(texture, 15, 17, 21, 14 + tailOffset, palette.Outline);
                    Line(texture, 16, 17, 21, 15 + tailOffset, palette.Accent);
                    break;
                case "ox":
                    Line(texture, 7, 6, 3, 3, palette.Outline);
                    Line(texture, 16, 6, 20, 3, palette.Outline);
                    Line(texture, 7, 7, 4, 4, palette.Accent);
                    Line(texture, 16, 7, 19, 4, palette.Accent);
                    Rect(texture, 6, 10, 17, 13, palette.Body);
                    Pixel(texture, 10, 9, palette.Dark);
                    Pixel(texture, 13, 9, palette.Dark);
                    Rect(texture, 9, 13, 14, 14, palette.Accent);
                    Pixel(texture, 11, 15, palette.Highlight);
                    Pixel(texture, 13, 15, palette.Highlight);
                    break;
                case "tiger":
                    Rect(texture, 7, 4, 9, 7, palette.Outline);
                    Rect(texture, 15, 4, 17, 7, palette.Outline);
                    Pixel(texture, 8, 5, palette.Body);
                    Pixel(texture, 16, 5, palette.Body);
                    Line(texture, 9, 8, 12, 9, palette.Dark);
                    Line(texture, 14, 8, 11, 9, palette.Dark);
                    Rect(texture, 11, 7, 12, 11, palette.Dark);
                    Rect(texture, 8, 13, 9, 16, palette.Dark);
                    Rect(texture, 15, 13, 16, 16, palette.Dark);
                    Line(texture, 16, 18, 22, 15 + tailOffset, palette.Outline);
                    Line(texture, 17, 18, 21, 16 + tailOffset, palette.Body);
                    Pixel(texture, 20, 16 + tailOffset, palette.Dark);
                    break;
                case "rabbit":
                    Rect(texture, 7, 1, 9, 7, palette.Outline);
                    Rect(texture, 14, 1, 16, 7, palette.Outline);
                    Rect(texture, 8, 2, 8, 6, palette.Accent);
                    Rect(texture, 15, 2, 15, 6, palette.Accent);
                    Pixel(texture, 9, 15, palette.Accent);
                    Pixel(texture, 15, 15, palette.Accent);
                    break;
                case "dragon":
                    Rect(texture, 5, 3, 7, 7, palette.Outline);
                    Rect(texture, 16, 3, 18, 7, palette.Outline);
                    Pixel(texture, 6, 4, palette.Accent);
                    Pixel(texture, 17, 4, palette.Accent);
                    Line(texture, 8, 14, 3, 13, palette.Accent);
                    Line(texture, 15, 14, 20, 13, palette.Accent);
                    Pixel(texture, 12, 4, palette.Aura);
                    Pixel(texture, 10, 10, palette.Accent);
                    Pixel(texture, 13, 10, palette.Accent);
                    Pixel(texture, 8, 16, palette.Highlight);
                    Pixel(texture, 12, 17, palette.Highlight);
                    Pixel(texture, 16, 16, palette.Accent);
                    break;
                case "snake":
                    Rect(texture, 5, 17, 15, 19, palette.Outline);
                    Rect(texture, 7, 16, 18, 18, palette.Body);
                    Line(texture, 16, 18, 21, 18 + tailOffset, palette.Outline);
                    Line(texture, 20, 18 + tailOffset, 19, 20, palette.Outline);
                    Line(texture, 17, 18, 21, 17 + tailOffset, palette.Accent);
                    Pixel(texture, 13, 13, palette.Accent);
                    Pixel(texture, 17, 12, palette.Dark);
                    break;
                case "horse":
                    Line(texture, 12, 3, 14, 9, palette.Dark);
                    Pixel(texture, 11, 5, palette.Accent);
                    Rect(texture, 15, 15, 18, 20, palette.Outline);
                    Rect(texture, 16, 15, 17, 19, palette.Accent);
                    Rect(texture, 8, 9, 15, 11, palette.Body);
                    Line(texture, 9, 7, 7, 10, palette.Outline);
                    break;
                case "goat":
                    Line(texture, 8, 6, 4, 4, palette.Outline);
                    Line(texture, 15, 6, 19, 4, palette.Outline);
                    Line(texture, 5, 4, 5, 7, palette.Accent);
                    Line(texture, 18, 4, 18, 7, palette.Accent);
                    Pixel(texture, 8, 10, palette.Highlight);
                    Pixel(texture, 15, 10, palette.Highlight);
                    Rect(texture, 10, 14, 13, 17, palette.Highlight);
                    break;
                case "monkey":
                    Ellipse(texture, 4, 8, 7, 12, palette.Outline);
                    Ellipse(texture, 16, 8, 19, 12, palette.Outline);
                    Rect(texture, 9, 11, 14, 15, palette.Accent);
                    Line(texture, 17, 17, 22, 19 - tailOffset, palette.Outline);
                    Pixel(texture, 20, 18 - tailOffset, palette.Accent);
                    Pixel(texture, 21, 19 - tailOffset, palette.Accent);
                    break;
                case "rooster":
                    Rect(texture, 9, 2, 11, 6, palette.Accent);
                    Rect(texture, 12, 1, 14, 6, palette.Accent);
                    Rect(texture, 15, 3, 16, 6, palette.Accent);
                    Rect(texture, 15, 12, 18, 13, palette.Accent);
                    Line(texture, 16, 16, 21, 12, palette.Dark);
                    Line(texture, 17, 17, 21, 16, palette.Accent);
                    break;
                case "dog":
                    Line(texture, 6, 6, 8, 14, palette.Dark);
                    Line(texture, 17, 6, 15, 14, palette.Dark);
                    Pixel(texture, 7, 9, palette.Accent);
                    Pixel(texture, 16, 9, palette.Accent);
                    Rect(texture, 8, 16, 15, 17, palette.Accent);
                    Pixel(texture, 12, 13, palette.Dark);
                    Line(texture, 16, 17, 21, 15 + tailOffset, palette.Outline);
                    Line(texture, 17, 17, 21, 14 + tailOffset, palette.Accent);
                    break;
                case "pig":
                    Rect(texture, 10, 12, 13, 14, palette.Accent);
                    Pixel(texture, 10, 13, palette.Dark);
                    Pixel(texture, 13, 13, palette.Dark);
                    Ellipse(texture, 6, 6, 8, 8, palette.Accent);
                    Ellipse(texture, 15, 6, 17, 8, palette.Accent);
                    Line(texture, 17, 17, 20, 16 + tailOffset, palette.Accent);
                    break;
            }

            if (stage == CompanionStage.Legendary)
            {
                Pixel(texture, 4, 4, palette.Aura);
                Pixel(texture, 19, 4, palette.Aura);
                Pixel(texture, 2, 11, palette.Aura);
                Pixel(texture, 21, 11, palette.Aura);
            }
        }

        private static void DrawCosmetics(Texture2D texture, IReadOnlyList<string> items, Palette palette)
        {
            foreach (var item in items)
            {
                if (item.Contains("skin"))
                {
                    Pixel(texture, 8, 9, ColorForItem(item, palette.Highlight));
                    Pixel(texture, 16, 16, ColorForItem(item, palette.Accent));
                }
                else if (item.Contains("outfit") || item.Contains("jacket") || item.Contains("hoodie") || item.Contains("cape") || item.Contains("armor"))
                {
                    Rect(texture, 8, 16, 15, 18, ColorForItem(item, palette.Dark));
                    Pixel(texture, 11, 17, palette.Highlight);
                }
                else if (item.Contains("hat") || item.Contains("crown") || item.Contains("cap") || item.Contains("helmet") || item.Contains("head"))
                {
                    Rect(texture, 8, 3, 15, 5, ColorForItem(item, palette.Accent));
                    Pixel(texture, 11, 2, palette.Highlight);
                    Pixel(texture, 13, 2, palette.Highlight);
                }
                else if (item.Contains("back") || item.Contains("wing") || item.Contains("bag"))
                {
                    Rect(texture, 4, 12, 7, 18, ColorForItem(item, palette.Accent));
                    Rect(texture, 17, 12, 20, 18, ColorForItem(item, palette.Accent));
                }
                else if (!item.Contains("badge") && (item.Contains("accessory") || item.Contains("glasses") || item.Contains("necklace")))
                {
                    Rect(texture, 9, 11, 15, 12, ColorForItem(item, palette.Dark));
                    Pixel(texture, 12, 16, ColorForItem(item, palette.Aura));
                }
                else if (item.Contains("badge"))
                {
                    Pixel(texture, 14, 17, ColorForItem(item, palette.Aura));
                    Pixel(texture, 15, 17, ColorForItem(item, palette.Aura));
                }
                else if (item.Contains("tool") || item.Contains("flame") || item.Contains("spark") || item.Contains("scroll") || item.Contains("goggles") || item.Contains("ring") || item.Contains("charm"))
                {
                    Rect(texture, 16, 14, 19, 16, ColorForItem(item, palette.Accent));
                    Pixel(texture, 20, 13, palette.Highlight);
                    Pixel(texture, 19, 17, palette.Dark);
                }
                else if (item.Contains("effect") || item.Contains("aura") || item.Contains("motion") || item.Contains("theme"))
                {
                    Pixel(texture, 3, 8, ColorForItem(item, palette.Aura));
                    Pixel(texture, 20, 8, ColorForItem(item, palette.Aura));
                    Pixel(texture, 4, 18, ColorForItem(item, palette.Aura));
                    Pixel(texture, 19, 18, ColorForItem(item, palette.Aura));
                }
            }
        }

        private static void ZodiacMark(Texture2D texture, string zodiac, Color32 accent)
        {
            var index = Math.Max(0, Array.IndexOf(new[] { "rat", "ox", "tiger", "rabbit", "dragon", "snake", "horse", "goat", "monkey", "rooster", "dog", "pig" }, zodiac));
            var x = 9 + index % 6;
            var y = 12 + index / 6;
            Pixel(texture, x, y, accent);
            Pixel(texture, x + 1, y, accent);
        }

        private static Palette PaletteFor(string zodiac, CompanionState state)
        {
            var body = BodyFor(state);
            var accent = AccentFor(state.Archetype);
            switch (zodiac)
            {
                case "rat": body = C(168, 181, 196); accent = C(245, 170, 180); break;
                case "ox": body = C(176, 140, 100); accent = C(238, 202, 128); break;
                case "tiger": body = C(238, 154, 55); accent = C(54, 62, 76); break;
                case "rabbit": body = C(244, 226, 224); accent = C(244, 148, 184); break;
                case "dragon": body = C(98, 190, 146); accent = C(255, 210, 78); break;
                case "snake": body = C(84, 170, 116); accent = C(222, 245, 160); break;
                case "horse": body = C(170, 110, 72); accent = C(248, 210, 116); break;
                case "goat": body = C(220, 214, 198); accent = C(154, 124, 92); break;
                case "monkey": body = C(168, 116, 74); accent = C(244, 186, 122); break;
                case "rooster": body = C(238, 214, 142); accent = C(224, 74, 64); break;
                case "dog": body = C(196, 145, 92); accent = C(88, 124, 220); break;
                case "pig": body = C(244, 164, 178); accent = C(236, 102, 146); break;
            }

            if (state.Stage == CompanionStage.Legendary)
            {
                accent = C(116, 216, 255);
            }

            return new Palette
            {
                Body = body,
                Accent = accent,
                Outline = C(34, 38, 48),
                Highlight = C(255, 244, 210),
                Dark = C(67, 72, 86),
                Aura = C(88, 205, 238)
            };
        }

        private static Color32 BodyFor(CompanionState state)
        {
            if (state.Stage == CompanionStage.Egg) return C(244, 224, 178);
            if (state.Stage == CompanionStage.Hatchling) return C(255, 202, 126);
            if (state.Stage == CompanionStage.Legendary) return C(164, 225, 255);
            return C(142, 202, 230);
        }

        private static Color32 AccentFor(CompanionArchetype archetype)
        {
            switch (archetype)
            {
                case CompanionArchetype.Explorer: return C(64, 190, 255);
                case CompanionArchetype.Builder: return C(242, 154, 66);
                case CompanionArchetype.Debugger: return C(88, 205, 118);
                case CompanionArchetype.Refiner: return C(237, 132, 196);
                case CompanionArchetype.Architect: return C(154, 169, 255);
                case CompanionArchetype.Sprinter: return C(255, 107, 74);
                default: return C(148, 163, 184);
            }
        }

        private static Color32 ColorForItem(string itemId, Color32 fallback)
        {
            if (itemId.Contains("white")) return C(248, 250, 252);
            if (itemId.Contains("calico")) return C(242, 154, 66);
            if (itemId.Contains("terminal") || itemId.Contains("codex")) return C(80, 227, 194);
            if (itemId.Contains("context") || itemId.Contains("scroll")) return C(180, 154, 255);
            if (itemId.Contains("crown")) return C(255, 210, 78);
            return fallback;
        }

        private static IReadOnlyList<string> NormalizeItems(IEnumerable<string> equippedItemIds)
        {
            return (equippedItemIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(SanitizeItemId)
                .Select(CanonicalizeEquippedItemId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        private static string SanitizeItemId(string itemId)
        {
            itemId = (itemId ?? string.Empty).Trim().ToLowerInvariant();
            if (itemId.Length > 80)
            {
                itemId = itemId.Substring(0, 80);
            }

            var chars = itemId
                .Where(character => char.IsLetterOrDigit(character) || character == '_' || character == '-')
                .ToArray();
            return new string(chars);
        }

        private static string CanonicalizeEquippedItemId(string sanitizedItemId)
        {
            // Cosmetic skin ids (skin_*) are collapsed to a compact underscore-free canonical
            // token so equivalent skin variants share a single sprite-cache entry. Other equipment
            // ids (outfits, effects, etc.) keep their raw separators to stay distinguishable.
            if (string.IsNullOrEmpty(sanitizedItemId) || !sanitizedItemId.StartsWith("skin_", StringComparison.Ordinal))
            {
                return sanitizedItemId;
            }

            return sanitizedItemId.Replace("_", string.Empty);
        }

        private static string CanonicalStageName(CompanionStage stage)
        {
            // CompanionStage declares back-compat aliases that share underlying values
            // (Baby = Child, Junior = Teen, Hatching = Hatchling). Enum.ToString() is
            // non-deterministic for duplicate values and can emit the alias (e.g. "Baby"
            // for Child), so resolve the canonical primary name explicitly for stable keys.
            switch (stage)
            {
                case CompanionStage.Egg: return "Egg";
                case CompanionStage.Hatchling: return "Hatchling";
                case CompanionStage.Child: return "Child";
                case CompanionStage.Teen: return "Teen";
                case CompanionStage.Adult: return "Adult";
                case CompanionStage.Legendary: return "Legendary";
                default: return stage.ToString();
            }
        }

        private static string StageVisualSignature(CompanionStage stage)
        {
            switch (stage)
            {
                case CompanionStage.Egg: return "egg-shell-zodiac-mark";
                case CompanionStage.Hatchling: return "tiny-face-partial-traits";
                case CompanionStage.Child: return "junior-body-traits";
                case CompanionStage.Teen: return "expanded-silhouette-expression";
                case CompanionStage.Adult: return "adult-crown-complete-traits";
                case CompanionStage.Legendary: return "legend-aura-rare-outline";
                default: return "unknown";
            }
        }

        private static string SanitizeKeyPart(string value, string fallback)
        {
            var sanitized = SanitizeItemId(value);
            return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
        }

        private static void Shadow(Texture2D texture)
        {
            Ellipse(texture, 6, 20, 17, 22, C(0, 0, 0, 40));
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

        private static Color32 C(byte r, byte g, byte b, byte a = 255)
        {
            return new Color32(r, g, b, a);
        }

        private struct Palette
        {
            public Color32 Body;
            public Color32 Accent;
            public Color32 Outline;
            public Color32 Highlight;
            public Color32 Dark;
            public Color32 Aura;
        }
    }
}
