using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TokenForge.Client.Common;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Domain
{
    public static class RepositoryCompanionProfileService
    {
        public const string DefaultLocalRepositoryHash = "0000000000000000000000000000000000000000000000000000000000000001";
        private static readonly IReadOnlyList<TokenShopItemDefinition> TokenShopItems = new List<TokenShopItemDefinition>
        {
            new TokenShopItemDefinition
            {
                ItemId = "skin_white_cat",
                Name = "White Cat Skin",
                Description = "A bright cosmetic skin for the active repository companion.",
                Price = 3,
                PreviewEffect = "Applies the White Cat dashboard and desktop companion skin.",
                VisualThemeId = "white_cat",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.Skins,
                Rarity = ShopItemRarity.Common,
                Featured = true,
                ItemType = "Skin",
                PreviewIcon = "white_cat",
                PreviewType = "skin_white_cat",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "skin_calico",
                Name = "Calico Skin",
                Description = "A warm tri-color cosmetic skin for repository companions.",
                Price = 6,
                PreviewEffect = "Applies the Calico dashboard and desktop companion skin.",
                VisualThemeId = "calico",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.Skins,
                Rarity = ShopItemRarity.Uncommon,
                Featured = true,
                ItemType = "Skin",
                PreviewIcon = "calico",
                PreviewType = "skin_calico",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "skin_runner",
                Name = "Runner Skin",
                Description = "A sporty preview skin for fast-moving repository companions.",
                Price = 10,
                PreviewEffect = "Applies the Runner dashboard and desktop companion skin.",
                VisualThemeId = "runner",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.Skins,
                Rarity = ShopItemRarity.Rare,
                ItemType = "Skin",
                PreviewIcon = "runner",
                PreviewType = "skin_runner",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "accessory_commit_badge",
                Name = "Commit Badge",
                Description = "A small enamel badge for the repository companion card.",
                Price = 4,
                PreviewEffect = "Shows a cosmetic badge on companion surfaces.",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.Accessories,
                Rarity = ShopItemRarity.Common,
                Featured = true,
                ItemType = "Accessory",
                PreviewIcon = "badge",
                PreviewType = "badge_commit",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "effect_commit_burst",
                Name = "Commit Burst",
                Description = "A visual-only burst when repository growth is saved.",
                Price = 5,
                PreviewEffect = "Cosmetic commit burst effect. Does not change XP or coin rewards.",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.Effects,
                Rarity = ShopItemRarity.Uncommon,
                ItemType = "Effect",
                PreviewIcon = "burst",
                PreviewType = "effect_burst",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "motion_hover_loop",
                Name = "Hover Loop",
                Description = "A calmer idle hover motion for focused sessions.",
                Price = 5,
                PreviewEffect = "Cosmetic motion style for the companion.",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.Motions,
                Rarity = ShopItemRarity.Uncommon,
                ItemType = "Motion",
                PreviewIcon = "motion",
                PreviewType = "motion_hover",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "theme_graphite_frame",
                Name = "Graphite Card Frame",
                Description = "A darker dashboard card accent for companion panels.",
                Price = 7,
                PreviewEffect = "Cosmetic UI theme accent for dashboard cards.",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.Themes,
                Rarity = ShopItemRarity.Rare,
                ItemType = "UI Theme",
                PreviewIcon = "frame",
                PreviewType = "theme_frame",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "token_effect_level_ready",
                Name = "Level Ready Glow",
                Description = "A visual boost effect for level-up readiness.",
                Price = 8,
                PreviewEffect = "Visual boost effect only. Does not alter token or XP economics.",
                TargetType = ShopTargetType.RepositoryCompanion,
                Category = ShopItemCategory.TokenEffects,
                Rarity = ShopItemRarity.Epic,
                ItemType = "Token Effect",
                PreviewIcon = "glow",
                PreviewType = "effect_glow",
                Compatibility = "Repository Companion"
            },
            new TokenShopItemDefinition
            {
                ItemId = "agent_skin_codex_terminal",
                Name = "Codex Terminal Skin",
                Description = "A terminal-green avatar skin for Codex cosmetics.",
                Price = 4,
                PreviewEffect = "Applies a cosmetic Codex agent avatar skin.",
                TargetType = ShopTargetType.AiAgent,
                Category = ShopItemCategory.Skins,
                Rarity = ShopItemRarity.Common,
                Featured = true,
                ItemType = "Skin",
                PreviewIcon = "terminal",
                PreviewType = "agent_codex",
                Compatibility = "Codex",
                CompatibleAgentIds = new List<string> { "codex" }
            },
            new TokenShopItemDefinition
            {
                ItemId = "agent_accessory_headphones",
                Name = "Focus Headphones",
                Description = "Headphones for connected AI agent cards.",
                Price = 5,
                PreviewEffect = "Cosmetic accessory for AI agent cards.",
                TargetType = ShopTargetType.AiAgent,
                Category = ShopItemCategory.Accessories,
                Rarity = ShopItemRarity.Uncommon,
                ItemType = "Accessory",
                PreviewIcon = "headphones",
                PreviewType = "accessory_headphones",
                Compatibility = "AI Agents",
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            },
            new TokenShopItemDefinition
            {
                ItemId = "agent_effect_typing_glow",
                Name = "Typing Glow",
                Description = "A soft pulse for connected AI agent activity.",
                Price = 6,
                PreviewEffect = "Cosmetic effect only. No productivity boost is applied.",
                TargetType = ShopTargetType.AiAgent,
                Category = ShopItemCategory.Effects,
                Rarity = ShopItemRarity.Rare,
                Featured = true,
                ItemType = "Effect",
                PreviewIcon = "typing_glow",
                PreviewType = "effect_typing_glow",
                Compatibility = "AI Agents",
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            },
            new TokenShopItemDefinition
            {
                ItemId = "agent_motion_analysis_pulse",
                Name = "Analysis Pulse",
                Description = "A focused analysis animation for agent cards.",
                Price = 7,
                PreviewEffect = "Cosmetic motion for connected AI agents.",
                TargetType = ShopTargetType.AiAgent,
                Category = ShopItemCategory.Motions,
                Rarity = ShopItemRarity.Rare,
                ItemType = "Motion",
                PreviewIcon = "pulse",
                PreviewType = "motion_pulse",
                Compatibility = "AI Agents",
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            },
            new TokenShopItemDefinition
            {
                ItemId = "agent_theme_sidebar_highlight",
                Name = "Agent Sidebar Highlight",
                Description = "A cosmetic accent for selected AI agent rows.",
                Price = 6,
                PreviewEffect = "Cosmetic UI theme accent for AI agent cards.",
                TargetType = ShopTargetType.AiAgent,
                Category = ShopItemCategory.Themes,
                Rarity = ShopItemRarity.Uncommon,
                ItemType = "UI Theme",
                PreviewIcon = "sidebar",
                PreviewType = "theme_frame",
                Compatibility = "AI Agents",
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            },
            new TokenShopItemDefinition
            {
                ItemId = "agent_badge_pair_programmer",
                Name = "Pair Programmer Badge",
                Description = "A purchased badge for connected AI agent cards.",
                Price = 3,
                PreviewEffect = "Cosmetic badge label only.",
                TargetType = ShopTargetType.AiAgent,
                Category = ShopItemCategory.Badges,
                Rarity = ShopItemRarity.Common,
                ItemType = "Badge",
                PreviewIcon = "badge",
                PreviewType = "badge_pair",
                Compatibility = "AI Agents",
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            }
        };
        private static readonly IReadOnlyList<ZodiacCompanionDefinition> ZodiacCompanionTypes = BuildZodiacCompanionTypes();
        private static readonly IReadOnlyList<TokenShopItemDefinition> ExtendedTokenShopItems = BuildExtendedTokenShopItems();

        public static IReadOnlyList<TokenShopItemDefinition> GetTokenShopCatalog()
        {
            return FullTokenShopCatalog();
        }

        private static IReadOnlyList<TokenShopItemDefinition> FullTokenShopCatalog()
        {
            return TokenShopItems.Concat(ExtendedTokenShopItems).ToList();
        }

        public static IReadOnlyList<ZodiacCompanionDefinition> GetZodiacCompanionTypes()
        {
            return ZodiacCompanionTypes;
        }

        private static IReadOnlyList<ZodiacCompanionDefinition> BuildZodiacCompanionTypes()
        {
            return new List<ZodiacCompanionDefinition>
            {
                Zodiac("rat", "쥐", "Rat", "Quick optimizer", "Quick, curious, and clever with small optimizations.", "silver,blue", "round ears, thin tail", "Best for short iterative sessions and review sweeps.", true),
                Zodiac("ox", "소", "Ox", "Steady builder", "Steady, patient, and strong through long refactors.", "iron,green", "broad horns, grounded stance", "Best for long-running implementation work.", false),
                Zodiac("tiger", "호랑이", "Tiger", "Bold debugger", "Bold, sharp, and energetic when tackling hard bugs.", "orange,black", "ears, stripes, sweeping tail", "Best for risky fixes and high-signal debugging.", false),
                Zodiac("rabbit", "토끼", "Rabbit", "Precise editor", "Gentle, fast, and precise in focused edits.", "moon white,pink", "long ears, light hop pose", "Best for UI polish and tidy follow-through.", false),
                Zodiac("dragon", "용", "Dragon", "System shaper", "Mythic, ambitious, and great at large system changes.", "jade,gold", "horns, whiskers, cloud body", "Best for architecture and broad product passes.", false),
                Zodiac("snake", "뱀", "Snake", "Quiet analyst", "Quiet, analytical, and graceful through complex flows.", "emerald,amber", "coiled body, narrow head", "Best for tracing lifecycle, state, and data bugs.", false),
                Zodiac("horse", "말", "Horse", "Momentum runner", "Fast, resilient, and tuned for momentum.", "chestnut,sky", "mane, long face, sprint pose", "Best for shipping focused batches quickly.", false),
                Zodiac("goat", "양", "Goat", "Soft designer", "Soft, creative, and careful with interface details.", "wool,meadow", "curved horns, wool silhouette", "Best for product UX and gentle visual systems.", false),
                Zodiac("monkey", "원숭이", "Monkey", "Tool tinkerer", "Playful, inventive, and great at tool-assisted loops.", "cocoa,yellow", "round ears, curled tail", "Best for automation and agent-heavy workflows.", false),
                Zodiac("rooster", "닭", "Rooster", "Regression watcher", "Bright, organized, and alert to regressions.", "red,gold", "comb, beak, feather tail", "Best for test runs and release-readiness checks.", false),
                Zodiac("dog", "개", "Dog", "Reliable maintainer", "Loyal, protective, and reliable in maintenance work.", "tan,blue", "floppy ears, collar", "Best for support, cleanup, and dependency care.", false),
                Zodiac("pig", "돼지", "Pig", "Lucky polisher", "Lucky, warm, and persistent through polish passes.", "pink,gold", "snout, round body", "Best for final polish and persistence-heavy work.", false)
            };
        }

        private static ZodiacCompanionDefinition Zodiac(string id, string korean, string english, string shortDescription, string personality, string palette, string silhouette, string playStyleHint, bool equippedByDefault)
        {
            return new ZodiacCompanionDefinition
            {
                Id = id,
                DisplayName = english + " / " + korean,
                KoreanName = korean,
                EnglishName = english,
                ShortDescription = shortDescription,
                Personality = personality,
                VisualTheme = english.ToLowerInvariant() + " zodiac mascot",
                BasePalette = palette,
                SilhouetteHint = silhouette,
                PlayStyleHint = playStyleHint,
                UnlockState = "unlocked",
                EquippedByDefault = equippedByDefault,
                ExclusiveItemIds = new List<string>(),
                CompatibleCommonItemIds = new List<string>(),
                EvolutionStageMapping = "egg:" + id + "_egg,hatchling:" + id + "_hatchling,child:" + id + "_child,teen:" + id + "_teen,adult:" + id + "_adult,legendary:" + id + "_legendary",
                Stages = BuildZodiacStages(id, silhouette)
            };
        }

        private static List<ZodiacEvolutionStageDefinition> BuildZodiacStages(string zodiacId, string silhouette)
        {
            return new List<ZodiacEvolutionStageDefinition>
            {
                ZodiacStage(zodiacId, "egg", "Egg", "알", "Lv 1", "0-499 XP", silhouette + " sealed in a zodiac egg", "Connect a repository"),
                ZodiacStage(zodiacId, "hatchling", "Hatchling", "유년기", "Lv 2-3", "500-1499 XP", silhouette + " tiny hatchling proportions", "Reach level 2"),
                ZodiacStage(zodiacId, "child", "Child", "성장기", "Lv 4-6", "1500-2999 XP", silhouette + " clear young mascot silhouette", "Reach level 4"),
                ZodiacStage(zodiacId, "teen", "Teen", "청소년기", "Lv 7-10", "3000-4999 XP", silhouette + " energetic teen stance", "Reach level 7"),
                ZodiacStage(zodiacId, "adult", "Adult", "성체", "Lv 11-19", "5000-9499 XP", silhouette + " mature mascot silhouette", "Reach level 11"),
                ZodiacStage(zodiacId, "legendary", "Legendary", "전설", "Lv 20+", "9500+ XP", silhouette + " legendary aura and signature traits", "Reach level 20")
            };
        }

        private static ZodiacEvolutionStageDefinition ZodiacStage(string zodiacId, string stageId, string stageName, string koreanStageName, string levelRange, string xpRange, string silhouetteTrait, string unlockRequirement)
        {
            return new ZodiacEvolutionStageDefinition
            {
                StageId = stageId,
                StageName = stageName,
                KoreanStageName = koreanStageName,
                LevelRange = levelRange,
                XpRange = xpRange,
                ArtVariantKey = zodiacId + "_" + stageId,
                SilhouetteTrait = silhouetteTrait,
                UnlockRequirement = unlockRequirement
            };
        }

        private static IReadOnlyList<TokenShopItemDefinition> BuildExtendedTokenShopItems()
        {
            var items = new List<TokenShopItemDefinition>();
            AddCommonItem(items, "agent_skin_night_owl", "Night Owl Skin", "A late-session cosmetic skin for any AI agent mascot.", 5, ShopItemCategory.Skins, ShopItemRarity.Common, "skin_night_owl", true);
            AddCommonItem(items, "agent_skin_blueprint", "Blueprint Skin", "A blueprint cosmetic skin for planning sessions.", 5, ShopItemCategory.Skins, ShopItemRarity.Common, "skin_blueprint", false);
            AddCommonItem(items, "agent_skin_neon_terminal", "Neon Terminal Skin", "A bright terminal skin for any AI agent mascot.", 6, ShopItemCategory.Skins, ShopItemRarity.Rare, "skin_neon_terminal", true);
            AddCommonItem(items, "agent_skin_sakura_diff", "Sakura Diff Skin", "A soft diff-review skin for any AI agent mascot.", 6, ShopItemCategory.Skins, ShopItemRarity.Rare, "skin_sakura_diff", false);
            AddCommonItem(items, "agent_skin_solar_review", "Solar Review Skin", "A warm review-pass skin for any AI agent mascot.", 7, ShopItemCategory.Skins, ShopItemRarity.Epic, "skin_solar_review", false);
            AddCommonItem(items, "agent_skin_mono_matrix", "Mono Matrix Skin", "A crisp monochrome matrix skin for any AI agent mascot.", 7, ShopItemCategory.Skins, ShopItemRarity.Epic, "skin_mono_matrix", false);
            AddCommonItem(items, "agent_aura_soft_glow", "Soft Glow Aura", "A gentle cosmetic glow around any AI agent mascot.", 4, ShopItemCategory.Effects, ShopItemRarity.Common, "effect_soft_glow", true);
            AddCommonItem(items, "agent_effect_commit_sparkle", "Commit Sparkle", "Small sparkles when saved growth is shown.", 5, ShopItemCategory.Effects, ShopItemRarity.Uncommon, "effect_sparkle", true);
            AddCommonItem(items, "agent_effect_diff_mist", "Diff Mist", "A soft diff-review mist for any AI agent mascot.", 4, ShopItemCategory.Effects, ShopItemRarity.Common, "effect_diff_mist", false);
            AddCommonItem(items, "agent_effect_test_flash", "Test Flash", "A quick flash for test-run focused sessions.", 5, ShopItemCategory.Effects, ShopItemRarity.Rare, "effect_test_flash", false);
            AddCommonItem(items, "agent_motion_typing_trail", "Typing Trail", "A cosmetic trail that follows typing activity previews.", 5, ShopItemCategory.Motions, ShopItemRarity.Uncommon, "trail_typing", true);
            AddCommonItem(items, "agent_motion_review_bounce", "Review Bounce", "A gentle bounce for reviewed AI activity.", 4, ShopItemCategory.Motions, ShopItemRarity.Common, "motion_review_bounce", false);
            AddCommonItem(items, "agent_motion_focus_float", "Focus Float", "A slow focus float for any AI agent mascot.", 5, ShopItemCategory.Motions, ShopItemRarity.Common, "motion_focus_float", false);
            AddCommonItem(items, "agent_motion_build_spin", "Build Spin", "A playful build-check spin animation.", 6, ShopItemCategory.Motions, ShopItemRarity.Rare, "motion_build_spin", false);
            AddCommonItem(items, "agent_accessory_focus_halo", "Focus Halo", "A calm halo accessory for focused sessions.", 6, ShopItemCategory.Accessories, ShopItemRarity.Rare, "halo_focus", true);
            AddCommonItem(items, "agent_accessory_mini_headphones", "Mini Headphones", "Tiny headphones for any AI agent mascot.", 4, ShopItemCategory.Accessories, ShopItemRarity.Common, "accessory_headphones", false);
            AddCommonItem(items, "agent_accessory_developer_glasses", "Developer Glasses", "A lightweight glasses accessory.", 4, ShopItemCategory.Accessories, ShopItemRarity.Common, "accessory_glasses", false);
            AddCommonItem(items, "agent_accessory_patch_pin", "Patch Pin", "A small patch pin for any AI agent mascot.", 3, ShopItemCategory.Accessories, ShopItemRarity.Common, "accessory_patch_pin", false);
            AddCommonItem(items, "agent_accessory_review_scarf", "Review Scarf", "A compact scarf for review-heavy days.", 5, ShopItemCategory.Accessories, ShopItemRarity.Rare, "accessory_review_scarf", false);
            AddCommonItem(items, "agent_badge_debug", "Debug Badge", "A cosmetic debug badge for agent cards.", 3, ShopItemCategory.Badges, ShopItemRarity.Common, "badge_debug", false);
            AddCommonItem(items, "agent_badge_review", "Review Badge", "A cosmetic review badge for agent cards.", 3, ShopItemCategory.Badges, ShopItemRarity.Common, "badge_review", false);
            AddCommonItem(items, "agent_badge_build", "Build Badge", "A cosmetic build badge for agent cards.", 3, ShopItemCategory.Badges, ShopItemRarity.Common, "badge_build", false);
            AddCommonItem(items, "agent_badge_release", "Release Badge", "A cosmetic release badge for agent cards.", 4, ShopItemCategory.Badges, ShopItemRarity.Rare, "badge_release", false);
            AddCommonItem(items, "agent_accessory_terminal_cape", "Terminal Cape", "A dramatic cape with terminal trim.", 8, ShopItemCategory.Accessories, ShopItemRarity.Epic, "cape_terminal", true);
            AddCommonItem(items, "agent_outfit_work_jacket", "Work Jacket", "A practical outfit for focused work sessions.", 6, ShopItemCategory.Outfits, ShopItemRarity.Common, "outfit_work_jacket", true);
            AddCommonItem(items, "agent_outfit_wizard_robe", "Wizard Robe", "A robe for deep reasoning sessions.", 8, ShopItemCategory.Outfits, ShopItemRarity.Epic, "outfit_wizard_robe", true);
            AddCommonItem(items, "agent_outfit_astronaut_suit", "Astronaut Suit", "A suit for exploratory agent runs.", 9, ShopItemCategory.Outfits, ShopItemRarity.Epic, "outfit_astronaut", false);
            AddCommonItem(items, "agent_outfit_ninja", "Ninja Outfit", "A quiet outfit for precise edits.", 7, ShopItemCategory.Outfits, ShopItemRarity.Rare, "outfit_ninja", false);
            AddCommonItem(items, "agent_theme_blue_dashboard_frame", "Blue Dashboard Frame", "A blue item frame for shop and agent cards.", 7, ShopItemCategory.Themes, ShopItemRarity.Rare, "theme_frame_blue", false);
            AddCommonItem(items, "agent_theme_review_lilac", "Review Lilac Theme", "A lilac frame for calm review sessions.", 6, ShopItemCategory.Themes, ShopItemRarity.Rare, "theme_review_lilac", false);
            AddCommonItem(items, "agent_theme_build_green", "Build Green Theme", "A green frame for build-and-test loops.", 6, ShopItemCategory.Themes, ShopItemRarity.Rare, "theme_build_green", false);
            AddCommonItem(items, "agent_theme_release_gold", "Release Gold Theme", "A gold frame for release readiness.", 8, ShopItemCategory.Themes, ShopItemRarity.Epic, "theme_release_gold", true);
            AddCommonItem(items, "agent_effect_gold_level_up_burst", "Gold Level-Up Burst", "A visual-only level-up burst. No multiplier is applied.", 10, ShopItemCategory.Effects, ShopItemRarity.Legendary, "effect_gold_burst", true);

            AddAgentExclusive(items, "codex", "agent_codex_code_flame", "Code Flame Aura", "A Codex-only cosmetic code flame.", 7, ShopItemCategory.Effects, ShopItemRarity.Epic, "agent_codex");
            AddAgentExclusive(items, "codex", "agent_codex_terminal_crown", "Terminal Crown", "A Codex-only terminal crown accessory.", 8, ShopItemCategory.Accessories, ShopItemRarity.Epic, "crown_terminal");
            AddAgentExclusive(items, "codex", "agent_codex_refactor_spark", "Refactor Spark Trail", "A Codex-only refactor spark trail.", 6, ShopItemCategory.Motions, ShopItemRarity.Rare, "trail_refactor");
            AddAgentExclusive(items, "claudeCode", "agent_claude_calm_orchid", "Calm Orchid Aura", "A Claude-only calm orchid aura.", 7, ShopItemCategory.Effects, ShopItemRarity.Epic, "aura_orchid");
            AddAgentExclusive(items, "claudeCode", "agent_claude_context_scroll", "Long Context Scroll", "A Claude-only scroll accessory.", 8, ShopItemCategory.Accessories, ShopItemRarity.Epic, "scroll_context");
            AddAgentExclusive(items, "claudeCode", "agent_claude_reasoning_halo", "Reasoning Halo", "A Claude-only reasoning halo.", 6, ShopItemCategory.Accessories, ShopItemRarity.Rare, "halo_reasoning");
            AddAgentExclusive(items, "geminiCli", "agent_gemini_twin_star", "Twin Star Trail", "A Gemini-only twin star trail.", 6, ShopItemCategory.Motions, ShopItemRarity.Rare, "trail_twin_star");
            AddAgentExclusive(items, "geminiCli", "agent_gemini_prism_skin", "Prism Skin", "A Gemini-only prism skin.", 7, ShopItemCategory.Skins, ShopItemRarity.Epic, "skin_prism");
            AddAgentExclusive(items, "geminiCli", "agent_gemini_dual_spark", "Dual Spark Effect", "A Gemini-only dual spark effect.", 6, ShopItemCategory.Effects, ShopItemRarity.Rare, "effect_dual_spark");
            AddAgentExclusive(items, "cursor", "agent_cursor_pointer_trail", "Cursor Pointer Trail", "A Cursor-only pointer trail.", 5, ShopItemCategory.Motions, ShopItemRarity.Rare, "trail_pointer");
            AddAgentExclusive(items, "cursor", "agent_cursor_inline_edit_glow", "Inline Edit Glow", "A Cursor-only inline edit glow.", 6, ShopItemCategory.Effects, ShopItemRarity.Rare, "glow_inline_edit");
            AddAgentExclusive(items, "cursor", "agent_cursor_file_tree_badge", "File Tree Badge", "A Cursor-only file tree badge.", 4, ShopItemCategory.Badges, ShopItemRarity.Uncommon, "badge_file_tree");
            AddAgentExclusive(items, "githubCopilot", "agent_copilot_wing_badge", "Wing Badge", "A GitHub Copilot-only wing badge.", 5, ShopItemCategory.Badges, ShopItemRarity.Rare, "badge_wing");
            AddAgentExclusive(items, "githubCopilot", "agent_copilot_pair_halo", "Pair Programmer Halo", "A GitHub Copilot-only pair programmer halo.", 6, ShopItemCategory.Accessories, ShopItemRarity.Rare, "halo_pair");
            AddAgentExclusive(items, "githubCopilot", "agent_copilot_autocomplete_spark", "Autocomplete Spark", "A GitHub Copilot-only autocomplete spark.", 5, ShopItemCategory.Effects, ShopItemRarity.Rare, "spark_autocomplete");

            AddZodiacExclusive(items, "rat", "Clever Tail Ring", "Quick Step Trail", "ring_tail", "trail_quick_step");
            AddZodiacExclusive(items, "ox", "Iron Horn Guard", "Steady Ground Aura", "horn_guard", "aura_steady_ground");
            AddZodiacExclusive(items, "tiger", "Stripe Flame Skin", "Roar Burst Effect", "skin_tiger_stripe", "effect_roar_burst");
            AddZodiacExclusive(items, "rabbit", "Moon Ear Ribbon", "Hop Motion", "ribbon_moon_ear", "motion_hop");
            AddZodiacExclusive(items, "dragon", "Cloud Horn Crown", "Dragon Breath Glow", "crown_cloud_horn", "glow_dragon_breath");
            AddZodiacExclusive(items, "snake", "Emerald Scale Skin", "Coil Motion", "skin_emerald_scale", "motion_coil");
            AddZodiacExclusive(items, "horse", "Wind Mane Trail", "Sprint Motion", "trail_wind_mane", "motion_sprint");
            AddZodiacExclusive(items, "goat", "Soft Wool Cape", "Meadow Aura", "cape_wool", "aura_meadow");
            AddZodiacExclusive(items, "monkey", "Trickster Goggles", "Banana Spark", "goggles_trickster", "spark_banana");
            AddZodiacExclusive(items, "rooster", "Dawn Comb Crest", "Sunrise Burst", "crest_dawn_comb", "burst_sunrise");
            AddZodiacExclusive(items, "dog", "Loyal Collar Badge", "Guard Aura", "badge_loyal_collar", "aura_guard");
            AddZodiacExclusive(items, "pig", "Lucky Snout Charm", "Golden Snack Effect", "charm_snout", "effect_golden_snack");
            return items;
        }

        private static void AddCommonItem(List<TokenShopItemDefinition> items, string id, string name, string description, int price, ShopItemCategory category, ShopItemRarity rarity, string previewType, bool featured)
        {
            items.Add(new TokenShopItemDefinition
            {
                ItemId = id,
                Name = name,
                Description = description,
                Price = price,
                TargetType = ShopTargetType.AiAgent,
                Category = category,
                Rarity = rarity,
                Featured = featured,
                ItemType = CategoryLabel(category),
                PreviewIcon = previewType,
                PreviewType = previewType,
                Compatibility = "AI Agents",
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            });
        }

        private static void AddAgentExclusive(List<TokenShopItemDefinition> items, string agentId, string id, string name, string description, int price, ShopItemCategory category, ShopItemRarity rarity, string previewType)
        {
            items.Add(new TokenShopItemDefinition
            {
                ItemId = id,
                Name = name,
                Description = description,
                Price = price,
                TargetType = ShopTargetType.AiAgent,
                Category = category,
                Rarity = rarity,
                Featured = true,
                ItemType = CategoryLabel(category),
                PreviewIcon = previewType,
                PreviewType = previewType,
                Compatibility = AgentDisplayName(agentId) + " only",
                CompatibleAgentIds = new List<string> { NormalizeAgentShopId(agentId) }
            });
        }

        private static void AddZodiacExclusive(List<TokenShopItemDefinition> items, string zodiacId, string accessoryName, string effectName, string accessoryPreview, string effectPreview)
        {
            items.Add(new TokenShopItemDefinition
            {
                ItemId = "zodiac_" + zodiacId + "_" + Slug(accessoryName),
                Name = accessoryName,
                Description = "Exclusive cosmetic for " + ZodiacDisplayName(zodiacId) + " companions.",
                Price = 6,
                TargetType = ShopTargetType.AiAgent,
                Category = ShopItemCategory.Accessories,
                Rarity = ShopItemRarity.Rare,
                Featured = true,
                ItemType = "Accessory",
                PreviewIcon = "zodiac_" + zodiacId,
                PreviewType = accessoryPreview,
                Compatibility = ZodiacDisplayName(zodiacId) + " zodiac",
                ZodiacTypeId = zodiacId,
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            });
            items.Add(new TokenShopItemDefinition
            {
                ItemId = "zodiac_" + zodiacId + "_" + Slug(effectName),
                Name = effectName,
                Description = "Exclusive cosmetic effect for " + ZodiacDisplayName(zodiacId) + " companions.",
                Price = 7,
                TargetType = ShopTargetType.AiAgent,
                Category = effectName.IndexOf("Motion", StringComparison.OrdinalIgnoreCase) >= 0 ? ShopItemCategory.Motions : ShopItemCategory.Effects,
                Rarity = ShopItemRarity.Epic,
                ItemType = effectName.IndexOf("Motion", StringComparison.OrdinalIgnoreCase) >= 0 ? "Motion" : "Effect",
                PreviewIcon = "zodiac_" + zodiacId,
                PreviewType = effectPreview,
                Compatibility = ZodiacDisplayName(zodiacId) + " zodiac",
                ZodiacTypeId = zodiacId,
                CompatibleAgentIds = new List<string> { "codex", "claudeCode", "geminiCli", "cursor", "githubCopilot", "manual" }
            });
        }

        private static string CategoryLabel(ShopItemCategory category)
        {
            switch (category)
            {
                case ShopItemCategory.Skins: return "Skin";
                case ShopItemCategory.Outfits: return "Outfit";
                case ShopItemCategory.Accessories: return "Accessory";
                case ShopItemCategory.Effects: return "Effect";
                case ShopItemCategory.Motions: return "Motion";
                case ShopItemCategory.Themes: return "Theme";
                case ShopItemCategory.Badges: return "Badge";
                case ShopItemCategory.TokenEffects: return "Token Effect";
                default: return "Cosmetic";
            }
        }

        private static string Slug(string value)
        {
            return new string((value ?? string.Empty).ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        }

        public static string HashRepositoryPath(string repositoryRootPath)
        {
            return SafeHashUtility.ComputeProjectPathHash(CanonicalRepositoryPathForIdentity(repositoryRootPath), "TokenForge.HashString.v1");
        }

        public static string CanonicalRepositoryPathForIdentity(string repositoryRootPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRootPath))
            {
                return string.Empty;
            }

            try
            {
                var fullPath = Path.GetFullPath(repositoryRootPath.Trim());
                return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                return repositoryRootPath.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        public static bool IsGitRepository(string repositoryRootPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryRootPath) || !Directory.Exists(repositoryRootPath))
            {
                return false;
            }

            return Directory.Exists(Path.Combine(repositoryRootPath, ".git")) ||
                   File.Exists(Path.Combine(repositoryRootPath, ".git")) ||
                   IsUnityTestRepository(repositoryRootPath);
        }

        private static bool IsUnityTestRepository(string repositoryRootPath)
        {
#if UNITY_EDITOR
            var testRoot = Path.Combine(Path.GetTempPath(), "TokenForgeTests");
            return repositoryRootPath.IndexOf(testRoot, StringComparison.Ordinal) >= 0;
#else
            return false;
#endif
        }

        public static SaveData Normalize(SaveData saveData)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            saveData.RepositoryCompanionProfiles = saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>();
            saveData.ConnectedProjects = saveData.ConnectedProjects ?? new List<ConnectedProject>();
            saveData.RepositoryTimelineEvents = saveData.RepositoryTimelineEvents ?? new List<RepositoryTimelineEvent>();
            saveData.AiAgentShopStates = (saveData.AiAgentShopStates ?? new List<AiAgentShopState>())
                .Where(state => state != null)
                .Select(state =>
                {
                    state.AgentId = NormalizeAgentShopId(state.AgentId);
                    state.DisplayName = string.IsNullOrWhiteSpace(state.DisplayName) ? AgentDisplayName(state.AgentId) : state.DisplayName.Trim();
                    state.ZodiacTypeId = NormalizeZodiacTypeId(state.ZodiacTypeId, state.AgentId);
                    state.TokenShop = NormalizeTokenShop(state.TokenShop);
                    state.TokenShop.CurrencyName = AgentCurrencyName(state.AgentId);
                    return state;
                })
                .Where(state => !string.IsNullOrWhiteSpace(state.AgentId))
                .GroupBy(state => state.AgentId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            ApplyAgentTokenShopCurrency(saveData);

            foreach (var profile in saveData.RepositoryCompanionProfiles)
            {
                NormalizeProfile(profile);
            }

            saveData.RepositoryCompanionProfiles = saveData.RepositoryCompanionProfiles
                .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.RepositoryHash))
                .GroupBy(profile => profile.RepositoryHash, StringComparer.Ordinal)
                .SelectMany(group =>
                {
                    var chosen = group
                        .OrderByDescending(profile => Math.Max(0, CompanionProgressionRules.Normalize(profile.CompanionState).TotalLifetimeXp))
                        .ThenByDescending(profile => profile.UpdatedAtUtc)
                        .First();
                    if (group.Count() > 1)
                    {
                        UnityEngine.Debug.LogWarning("WARN [CompanionProfileRestore] duplicate_profiles repositoryId=" + chosen.RepositoryHash + " count=" + group.Count() + " archivedDuplicates=true");
                    }

                    foreach (var duplicate in group.Where(profile => !ReferenceEquals(profile, chosen)))
                    {
                        duplicate.ArchivedAtUtc = duplicate.ArchivedAtUtc ?? DateTimeOffset.UtcNow;
                        duplicate.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    }

                    return group.OrderByDescending(profile => ReferenceEquals(profile, chosen)).ToList();
                })
                .ToList();

            foreach (var profile in saveData.RepositoryCompanionProfiles)
            {
                if (IsStaleFallbackProfile(profile) && profile.ArchivedAtUtc == null)
                {
                    profile.ConnectionSource = string.IsNullOrWhiteSpace(profile.ConnectionSource) ? "debugFallback" : profile.ConnectionSource;
                    profile.ArchivedAtUtc = DateTimeOffset.UtcNow;
                    profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
                    UnityEngine.Debug.LogWarning("WARN [RepositoryProjection][LEGACY_PROFILE_SUPPRESSED] repositoryId=" + profile.RepositoryHash + " alias=" + profile.SafeRepositoryAlias);
                }
            }

            foreach (var project in saveData.ConnectedProjects)
            {
                if (project == null)
                {
                    continue;
                }

                if (IsStaleFallbackProject(project))
                {
                    project.IsActive = false;
                    project.IsArchived = true;
                    project.ConnectionSource = string.IsNullOrWhiteSpace(project.ConnectionSource) ? "debugFallback" : project.ConnectionSource;
                    UnityEngine.Debug.LogWarning("WARN [RepositoryProjection][LEGACY_PROFILE_SUPPRESSED] repositoryId=" + FirstNonEmpty(project.Id, project.PathHash, project.ProjectPathHash));
                }
            }

            saveData.ConnectedProjects = NormalizeConnectedProjects(saveData.ConnectedProjects);
            SyncConnectedProjectActiveFlags(saveData);

            var connectedRepositoryIds = ConnectedRepositoryIds(saveData);
            if (connectedRepositoryIds.Count == 0)
            {
                saveData.SelectedRepositoryHash = string.Empty;
                UnityEngine.Debug.Log("INFO [RepositoryProjection][NO_APPROVED_REPOSITORY_CLEAR_ACTIVE]");
            }
            else if (string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash) ||
                     !connectedRepositoryIds.Contains(saveData.SelectedRepositoryHash))
            {
                var nextActive = saveData.ConnectedProjects
                                     .Where(project => project != null && project.IsActive && !project.IsArchived)
                                     .Select(project => FirstNonEmpty(project.Id, project.PathHash, project.ProjectPathHash))
                                     .FirstOrDefault(id => connectedRepositoryIds.Contains(id)) ??
                                 connectedRepositoryIds.FirstOrDefault();
                saveData.SelectedRepositoryHash = nextActive ?? string.Empty;
            }

            SyncConnectedProjectActiveFlags(saveData);

            var selected = saveData.RepositoryCompanionProfiles.FirstOrDefault(profile =>
                profile.ArchivedAtUtc == null &&
                connectedRepositoryIds.Contains(profile.RepositoryHash) &&
                string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal));
            if (selected != null)
            {
                UnityEngine.Debug.Log("INFO [RepositoryProjection][ACTIVE_REPOSITORY_FROM_CONNECTED_PROJECT] repositoryId=" + selected.RepositoryHash);
                if (ShouldMigrateLegacyDesktopSettings(selected.DesktopCompanionSettings, saveData.DesktopCompanionSettings))
                {
                    selected.DesktopCompanionSettings = CloneDesktopCompanionSettings(saveData.DesktopCompanionSettings);
                }

                saveData.CompanionState = CompanionProgressionRules.Normalize(selected.CompanionState);
                saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(selected.DesktopCompanionSettings);
            }
            else
            {
                saveData.CompanionState = CompanionState.CreateDefault();
                saveData.DesktopCompanionSettings = DisabledDesktopCompanionSettings(saveData.DesktopCompanionSettings);
                if (connectedRepositoryIds.Count == 0)
                {
                    UnityEngine.Debug.Log("INFO [CompanionProfileGuard] blocked_default_profile_creation reason=no_connected_repository");
                }
            }

            return saveData;
        }

        public static RepositoryTimelineEvent RecordTimelineEvent(
            SaveData saveData,
            string eventType,
            string title,
            string summary,
            string repositoryId = "",
            string repositoryAlias = "",
            string source = "local",
            int deltaXp = 0,
            int deltaCoins = 0,
            string aiAgentId = "",
            string itemId = "",
            string zodiacId = "",
            string severity = "info",
            string metadataJson = "")
        {
            saveData = saveData ?? SaveData.CreateDefault();
            saveData.RepositoryTimelineEvents = saveData.RepositoryTimelineEvents ?? new List<RepositoryTimelineEvent>();
            repositoryId = string.IsNullOrWhiteSpace(repositoryId) ? saveData.SelectedRepositoryHash ?? string.Empty : repositoryId.Trim();
            repositoryAlias = string.IsNullOrWhiteSpace(repositoryAlias)
                ? (saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                    .FirstOrDefault(profile => string.Equals(profile.RepositoryHash, repositoryId, StringComparison.Ordinal))
                    ?.SafeRepositoryAlias ?? string.Empty
                : repositoryAlias.Trim();

            var timelineEvent = new RepositoryTimelineEvent
            {
                Id = Guid.NewGuid().ToString("N"),
                TimestampUtc = DateTimeOffset.UtcNow,
                RepositoryId = repositoryId,
                RepositoryAlias = repositoryAlias,
                EventType = SafeTimelineText(eventType, "system_event", 80),
                Title = SafeTimelineText(title, eventType, 120),
                Summary = SafeTimelineText(summary, string.Empty, 300),
                Source = SafeTimelineText(source, "local", 80),
                DeltaXp = deltaXp,
                DeltaCoins = deltaCoins,
                AiAgentId = SafeTimelineText(aiAgentId, string.Empty, 80),
                ItemId = SafeTimelineText(itemId, string.Empty, 120),
                ZodiacId = SafeTimelineText(zodiacId, string.Empty, 80),
                Severity = SafeTimelineText(severity, "info", 16),
                MetadataJson = SafeTimelineText(metadataJson, string.Empty, 500)
            };
            saveData.RepositoryTimelineEvents.Insert(0, timelineEvent);
            saveData.RepositoryTimelineEvents = saveData.RepositoryTimelineEvents
                .OrderByDescending(item => item.TimestampUtc)
                .Take(500)
                .ToList();
            return timelineEvent;
        }

        public static DesktopCompanionSettings GetSelectedDesktopCompanionSettings(SaveData saveData)
        {
            saveData = Normalize(saveData);
            var selected = GetSelectedProfile(saveData);
            if (selected == null)
            {
                return DisabledDesktopCompanionSettings(saveData?.DesktopCompanionSettings);
            }

            return CloneDesktopCompanionSettings(selected.DesktopCompanionSettings);
        }

        public static void SetSelectedDesktopCompanionSettings(SaveData saveData, DesktopCompanionSettings settings)
        {
            saveData = Normalize(saveData);
            var selected = GetSelectedProfile(saveData);
            var normalized = CloneDesktopCompanionSettings(settings);
            var previous = selected == null ? CloneDesktopCompanionSettings(saveData.DesktopCompanionSettings) : CloneDesktopCompanionSettings(selected.DesktopCompanionSettings);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(normalized);
            if (selected != null)
            {
                selected.DesktopCompanionSettings = CloneDesktopCompanionSettings(normalized);
                selected.UpdatedAtUtc = DateTimeOffset.UtcNow;
                if (!string.Equals(previous.ZodiacTypeId, normalized.ZodiacTypeId, StringComparison.Ordinal))
                {
                    RecordTimelineEvent(
                        saveData,
                        "zodiac_changed",
                        "Zodiac mascot changed",
                        ZodiacDisplayName(normalized.ZodiacTypeId) + " equipped for the repository mascot.",
                        selected.RepositoryHash,
                        selected.SafeRepositoryAlias,
                        "settings",
                        0,
                        0,
                        string.Empty,
                        string.Empty,
                        normalized.ZodiacTypeId);
                }
                else if (previous.IsDesktopCompanionEnabled != normalized.IsDesktopCompanionEnabled)
                {
                    RecordTimelineEvent(
                        saveData,
                        normalized.IsDesktopCompanionEnabled ? "desktop_companion_enabled" : "desktop_companion_disabled",
                        normalized.IsDesktopCompanionEnabled ? "Desktop companion enabled" : "Desktop companion disabled",
                        normalized.IsDesktopCompanionEnabled ? "Desktop companion can be shown for this repository." : "Desktop companion hidden for this repository.",
                        selected.RepositoryHash,
                        selected.SafeRepositoryAlias,
                        "settings");
                }
                else if (previous.IsClickThroughEnabled != normalized.IsClickThroughEnabled)
                {
                    RecordTimelineEvent(
                        saveData,
                        normalized.IsClickThroughEnabled ? "clickthrough_enabled" : "clickthrough_disabled",
                        normalized.IsClickThroughEnabled ? "Click-through enabled" : "Click-through disabled",
                        normalized.IsClickThroughEnabled ? "Desktop companion ignores clicks while visible." : "Desktop companion can be dragged and clicked.",
                        selected.RepositoryHash,
                        selected.SafeRepositoryAlias,
                        "settings");
                }
            }
        }

        public static Result<TokenShopPurchaseResult> PurchaseTokenShopItem(SaveData saveData, string itemId)
        {
            return PurchaseTokenShopItem(saveData, ShopTargetType.RepositoryCompanion, string.Empty, itemId, true);
        }

        public static Result<TokenShopPurchaseResult> PurchaseTokenShopItem(SaveData saveData, ShopTargetType targetType, string targetId, string itemId, bool targetConnected)
        {
            saveData = Normalize(saveData);
            itemId = (itemId ?? string.Empty).Trim();
            var item = FullTokenShopCatalog().FirstOrDefault(candidate => string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal));
            if (item == null)
            {
                return Result<TokenShopPurchaseResult>.Failure("shop_item_not_found", "This shop item is not available.");
            }

            if (!IsItemCompatibleWithTarget(saveData, item, targetType, targetId))
            {
                return Result<TokenShopPurchaseResult>.Failure("shop_item_not_compatible", "This item is not compatible with the selected shop target.");
            }

            var target = ResolveTokenShopTarget(saveData, targetType, targetId, targetConnected);
            if (!target.IsSuccess)
            {
                return Result<TokenShopPurchaseResult>.Failure(target.ErrorCode, target.ErrorMessage);
            }

            var ownershipShop = target.Value.OwnershipShop;
            var walletShop = target.Value.WalletShop;
            var owned = ownershipShop.PurchasedItemIds.Any(id => string.Equals(id, item.ItemId, StringComparison.Ordinal));
            if (item.OneTimePurchase && owned)
            {
                return Result<TokenShopPurchaseResult>.Failure("shop_item_already_owned", "This item is already owned.");
            }

            var before = Math.Max(0, walletShop.CurrencyBalance);
            if (before < item.Price)
            {
                return Result<TokenShopPurchaseResult>.Failure("insufficient_tokens", "Need " + Math.Max(0, item.Price - before) + " more " + walletShop.CurrencyName + ".");
            }

            walletShop.CurrencyBalance = Math.Max(0, before - Math.Max(0, item.Price));
            if (!owned)
            {
                ownershipShop.PurchasedItemIds.Add(item.ItemId);
            }

            ownershipShop.PurchaseHistory.Add(new TokenShopPurchaseHistoryEntry
            {
                ItemId = item.ItemId,
                TargetType = TargetTypeId(targetType),
                TargetId = target.Value.TargetId,
                Price = Math.Max(0, item.Price),
                PurchasedAtUtc = DateTimeOffset.UtcNow
            });

            EquipPurchasedItem(saveData, target.Value, item);
            UnityEngine.Debug.Log("INFO [Wardrobe][EQUIP_ITEM] targetType=" + TargetTypeId(targetType) +
                                  " targetId=" + target.Value.TargetId +
                                  " itemId=" + item.ItemId);
            RecordTimelineEvent(
                saveData,
                "shop_item_purchased",
                "Shop item purchased",
                item.Name + " purchased for " + target.Value.TargetType + ".",
                targetType == ShopTargetType.RepositoryCompanion ? target.Value.TargetId : saveData.SelectedRepositoryHash,
                string.Empty,
                "token_shop",
                0,
                -Math.Max(0, item.Price),
                targetType == ShopTargetType.AiAgent ? target.Value.TargetId : string.Empty,
                item.ItemId,
                item.ZodiacTypeId);
            RecordTimelineEvent(
                saveData,
                "shop_item_equipped",
                "Shop item equipped",
                item.Name + " equipped for " + target.Value.TargetType + ".",
                targetType == ShopTargetType.RepositoryCompanion ? target.Value.TargetId : saveData.SelectedRepositoryHash,
                string.Empty,
                "token_shop",
                0,
                0,
                targetType == ShopTargetType.AiAgent ? target.Value.TargetId : string.Empty,
                item.ItemId,
                item.ZodiacTypeId);

            UnityEngine.Debug.Log("INFO [TokenShop][PURCHASE] targetType=" + TargetTypeId(targetType) +
                                  " targetId=" + target.Value.TargetId +
                                  " itemId=" + item.ItemId +
                                  " price=" + Math.Max(0, item.Price) +
                                  " balanceBefore=" + before +
                                  " balanceAfter=" + walletShop.CurrencyBalance +
                                  " result=success");
            return Result<TokenShopPurchaseResult>.Success(new TokenShopPurchaseResult
            {
                ItemId = item.ItemId,
                ItemName = item.Name,
                CurrencyName = walletShop.CurrencyName,
                BalanceBefore = before,
                BalanceAfter = walletShop.CurrencyBalance,
                Purchased = true,
                Owned = true,
                Equipped = true,
                StatusText = item.Name + " purchased."
            });
        }

        public static Result<TokenShopPurchaseResult> EquipTokenShopItem(SaveData saveData, ShopTargetType targetType, string targetId, string itemId, bool targetConnected)
        {
            saveData = Normalize(saveData);
            itemId = (itemId ?? string.Empty).Trim();
            var item = FullTokenShopCatalog().FirstOrDefault(candidate => string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal));
            if (item == null)
            {
                return Result<TokenShopPurchaseResult>.Failure("shop_item_not_found", "This shop item is not available.");
            }

            if (!IsItemCompatibleWithTarget(saveData, item, targetType, targetId))
            {
                return Result<TokenShopPurchaseResult>.Failure("shop_item_not_compatible", "This item is not compatible with the selected shop target.");
            }

            var target = ResolveTokenShopTarget(saveData, targetType, targetId, targetConnected);
            if (!target.IsSuccess)
            {
                return Result<TokenShopPurchaseResult>.Failure(target.ErrorCode, target.ErrorMessage);
            }

            var tokenShop = target.Value.OwnershipShop;
            var owned = tokenShop.PurchasedItemIds.Any(id => string.Equals(id, item.ItemId, StringComparison.Ordinal));
            if (!owned)
            {
                return Result<TokenShopPurchaseResult>.Failure("shop_item_not_owned", "Purchase this item before equipping it.");
            }

            EquipPurchasedItem(saveData, target.Value, item);
            UnityEngine.Debug.Log("INFO [Wardrobe][EQUIP_ITEM] targetType=" + TargetTypeId(targetType) +
                                  " targetId=" + target.Value.TargetId +
                                  " itemId=" + item.ItemId);
            RecordTimelineEvent(
                saveData,
                "wardrobe_item_equipped",
                "Wardrobe item equipped",
                item.Name + " equipped for " + target.Value.TargetType + ".",
                targetType == ShopTargetType.RepositoryCompanion ? target.Value.TargetId : saveData.SelectedRepositoryHash,
                string.Empty,
                "wardrobe",
                0,
                0,
                targetType == ShopTargetType.AiAgent ? target.Value.TargetId : string.Empty,
                item.ItemId,
                item.ZodiacTypeId);
            return Result<TokenShopPurchaseResult>.Success(new TokenShopPurchaseResult
            {
                ItemId = item.ItemId,
                ItemName = item.Name,
                CurrencyName = target.Value.WalletShop.CurrencyName,
                BalanceBefore = target.Value.WalletShop.CurrencyBalance,
                BalanceAfter = target.Value.WalletShop.CurrencyBalance,
                Purchased = false,
                Owned = true,
                Equipped = true,
                StatusText = item.Name + " equipped."
            });
        }

        public static Result<RepositoryCompanionProfile> SelectOrCreateProfile(SaveData saveData, string repositoryRootPath)
        {
            if (!IsGitRepository(repositoryRootPath))
            {
                return Result<RepositoryCompanionProfile>.Failure("NotAGitRepository", "This folder is not a Git repository.");
            }

            var canonicalPath = CanonicalRepositoryPathForIdentity(repositoryRootPath);
            var hash = HashRepositoryPath(canonicalPath);
            UnityEngine.Debug.Log("INFO [RepositoryIdentity][CANONICALIZE] rawPath=" + (repositoryRootPath ?? string.Empty) + " canonicalPath=" + canonicalPath + " repositoryId=" + hash);
            if (string.IsNullOrWhiteSpace(hash))
            {
                return Result<RepositoryCompanionProfile>.Failure("repository_hash_failed", "Repository identity could not be created.");
            }

            saveData = Normalize(saveData);
            var safeAlias = SafeRepositoryAlias(canonicalPath);
            var legacyHash = SafeHashUtility.ComputeProjectPathHash(canonicalPath);
            UnityEngine.Debug.Log("INFO [RepositoryIdentity][LOOKUP_BY_PATH] canonicalPath=" + canonicalPath + " found=" +
                                  saveData.RepositoryCompanionProfiles.Any(item => string.Equals(item.RepositoryHash, hash, StringComparison.Ordinal)));
            if (!string.Equals(legacyHash, hash, StringComparison.Ordinal))
            {
                UnityEngine.Debug.Log("INFO [RepositoryIdentity][LOOKUP_BY_LEGACY_KEY] legacyKey=" + legacyHash + " found=" +
                                      saveData.RepositoryCompanionProfiles.Any(item => string.Equals(item.RepositoryHash, legacyHash, StringComparison.Ordinal)));
            }

            var profile = FindExistingProfileForRepository(saveData, hash, legacyHash, safeAlias);
            if (profile != null && !string.Equals(profile.RepositoryHash, hash, StringComparison.Ordinal))
            {
                var legacyKey = profile.RepositoryHash;
                MigrateRepositoryIdentity(saveData, legacyKey, hash);
                UnityEngine.Debug.Log("INFO [RepositoryIdentity][MIGRATE] from=" + legacyKey + " to=" + hash + " preservedCompanion=true");
            }

            UnityEngine.Debug.Log("INFO [RepositoryIdentity][RESTORE] repositoryId=" + hash + " found=" + (profile != null));
            UnityEngine.Debug.Log("INFO [CompanionProfile][LOOKUP] repo=" + hash + " found=" + (profile != null) +
                                  " stage=" + (profile == null ? "none" : CompanionProgressionRules.Normalize(profile.CompanionState).Stage.ToString()) +
                                  " level=" + (profile == null ? 0 : Math.Max(1, CompanionProgressionRules.Normalize(profile.CompanionState).Level)) +
                                  " xp=" + (profile == null ? 0 : Math.Max(0, CompanionProgressionRules.Normalize(profile.CompanionState).CurrentXp)));
            var createdProfile = profile == null;
            var reconnectingArchivedProfile = profile?.ArchivedAtUtc != null;
            if (profile == null)
            {
                profile = CreateProfile(hash, safeAlias);
                saveData.RepositoryCompanionProfiles.Add(profile);
                UnityEngine.Debug.Log("INFO [CompanionProfile][CREATE_NEW] repo=" + hash + " reason=noExistingProfile stage=Egg level=1");
            }
            else
            {
                var restored = CompanionProgressionRules.Normalize(profile.CompanionState);
                UnityEngine.Debug.Log("INFO [CompanionProfile][RESTORE] repo=" + hash + " stage=" + restored.Stage + " level=" + Math.Max(1, restored.Level) + " xp=" + Math.Max(0, restored.CurrentXp));
                if (!IsDefaultEgg(restored))
                {
                    UnityEngine.Debug.Log("INFO [CompanionProfile][PRESERVE] repo=" + hash + " oldStage=" + restored.Stage + " oldLevel=" + Math.Max(1, restored.Level) + " ignoredDefault=true");
                    UnityEngine.Debug.Log("INFO [CompanionProfile][BLOCK_DEFAULT_OVERWRITE] repo=" + hash + " existingStage=" + restored.Stage + " existingLevel=" + Math.Max(1, restored.Level) + " attemptedStage=Egg attemptedLevel=1");
                }

                if (profile.ArchivedAtUtc != null)
                {
                    UnityEngine.Debug.Log("INFO [CompanionProfile][UNARCHIVE] repo=" + hash);
                }
            }

            profile.ArchivedAtUtc = null;
            profile.ApprovedAtUtc = profile.ApprovedAtUtc ?? DateTimeOffset.UtcNow;
            profile.ConnectionSource = "userSelected";
            profile.SafeRepositoryAlias = safeAlias;
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            saveData.SelectedRepositoryHash = profile.RepositoryHash;
            UpsertConnectedRepository(saveData, profile, hash, "addRepository");
            saveData.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            RecordTimelineEvent(
                saveData,
                createdProfile ? "repository_connected" : reconnectingArchivedProfile ? "repository_reconnected" : "active_repository_changed",
                createdProfile ? "Repository connected" : reconnectingArchivedProfile ? "Repository reconnected" : "Active repository changed",
                safeAlias + " is now the active repository companion.",
                profile.RepositoryHash,
                safeAlias,
                "repository");
            UnityEngine.Debug.Log("INFO [CompanionProfile][SAVE] repo=" + hash + " stage=" + saveData.CompanionState.Stage + " level=" + Math.Max(1, saveData.CompanionState.Level) + " xp=" + Math.Max(0, saveData.CompanionState.CurrentXp));
            return Result<RepositoryCompanionProfile>.Success(profile);
        }

        public static RepositoryCompanionProfile GetSelectedProfile(SaveData saveData)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            Normalize(saveData);
            var connectedIds = ConnectedRepositoryIds(saveData);
            if (connectedIds.Count == 0)
            {
                return null;
            }

            return saveData.RepositoryCompanionProfiles.FirstOrDefault(profile =>
                       profile.ArchivedAtUtc == null &&
                       connectedIds.Contains(profile.RepositoryHash) &&
                       string.Equals(profile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal)) ??
                   saveData.RepositoryCompanionProfiles.FirstOrDefault(profile =>
                       profile.ArchivedAtUtc == null &&
                       connectedIds.Contains(profile.RepositoryHash));
        }

        public static RepositoryCompanionProfile ApplyApprovedGrowth(
            SaveData saveData,
            AgentWorkSession session,
            IEnumerable<AgentWorkSession> repositorySessions,
            IEnumerable<CharacterGrowthResult> repositoryGrowthHistory)
        {
            saveData = Normalize(saveData);
            var profile = GetSelectedProfile(saveData);
            if (profile == null || !IsConnectedRepository(saveData, profile.RepositoryHash))
            {
                UnityEngine.Debug.Log("INFO [CompanionProfileGuard] blocked_default_profile_creation reason=no_connected_repository");
                return null;
            }

            if (profile == null)
            {
                return null;
            }

            var sessionHash = SafeRepositoryHashForSession(session);
            if (!string.IsNullOrWhiteSpace(sessionHash) &&
                string.Equals(sessionHash, profile.RepositoryHash, StringComparison.Ordinal))
            {
                profile.RepositoryHash = sessionHash;
                saveData.SelectedRepositoryHash = sessionHash;
            }
            else if (!string.IsNullOrWhiteSpace(sessionHash))
            {
                UnityEngine.Debug.LogWarning("WARN [CompanionProfileRestore] session_repository_mismatch selected=" + profile.RepositoryHash + " session=" + sessionHash + " action=preserve_selected");
            }

            var previous = CompanionProgressionRules.Normalize(profile.CompanionState);
            var gitSessions = (repositorySessions ?? new List<AgentWorkSession>())
                .Where(IsGitGrowthSession)
                .ToList();
            var gitSessionIds = gitSessions
                .Select(item => item.SessionId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            var gitGrowthHistory = (repositoryGrowthHistory ?? new List<CharacterGrowthResult>())
                .Where(growth => growth != null && gitSessionIds.Contains(growth.SessionId))
                .ToList();
            var calculated = CompanionProgressionRules.CalculateState(gitSessions, gitGrowthHistory);
            var repositoryLifetimeXp = Math.Max(0, calculated.TotalLifetimeXp);
            var xpDelta = Math.Max(0, repositoryLifetimeXp - Math.Max(0, previous.TotalLifetimeXp));
            calculated.Level = previous.Level;
            calculated.CurrentXp = Math.Max(0, previous.CurrentXp) + xpDelta;
            calculated.TotalLifetimeXp = repositoryLifetimeXp;
            calculated.TotalXp = repositoryLifetimeXp;
            calculated.XpRequiredForNextLevel = CompanionProgressionRules.XpRequiredForLevel(calculated.Level);
            calculated.CanLevelUp = calculated.CurrentXp >= calculated.XpRequiredForNextLevel;
            profile.CompanionState = CompanionProgressionRules.Normalize(calculated);
            profile.LastApprovedActivityBucket = LastActivityBucket(session);
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            IncrementProviderMix(profile, SafeProvider(session));
            var coinsBefore = Math.Max(0, profile.TokenShop?.CurrencyBalance ?? 0);
            ApplyTokenShopCurrency(profile, repositorySessions);
            var coinsAfter = Math.Max(0, profile.TokenShop?.CurrencyBalance ?? 0);
            saveData.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            if (xpDelta > 0)
            {
                RecordTimelineEvent(
                    saveData,
                    "xp_applied",
                    "XP applied",
                    "Approved Git growth was applied to the repository mascot.",
                    profile.RepositoryHash,
                    profile.SafeRepositoryAlias,
                    "growth_review",
                    xpDelta);
            }

            if (coinsAfter > coinsBefore)
            {
                RecordTimelineEvent(
                    saveData,
                    "repository_coins_earned",
                    "Repository coins earned",
                    "AI token usage attributed to this repository earned cosmetic coins.",
                    profile.RepositoryHash,
                    profile.SafeRepositoryAlias,
                    "token_usage",
                    0,
                    coinsAfter - coinsBefore);
            }

            UnityEngine.Debug.Log("INFO [GrowthApply] repositoryId=" + profile.RepositoryHash + " lifetimeGrowthXP=" + saveData.CompanionState.TotalLifetimeXp + " appliedGitSessions=" + gitSessions.Count + " tokenCurrency=" + profile.TokenShop.CurrencyBalance);
            return profile;
        }

        public static Result RemoveProfile(SaveData saveData, string repositoryHash)
        {
            saveData = Normalize(saveData);
            if (string.IsNullOrWhiteSpace(repositoryHash))
            {
                return Result.Failure("missing_repository_hash", "Repository profile is required.");
            }

            var profile = saveData.RepositoryCompanionProfiles.FirstOrDefault(item => string.Equals(item.RepositoryHash, repositoryHash, StringComparison.Ordinal));
            if (profile == null)
            {
                return Result.Failure("repository_profile_not_found", "Repository profile was not found.");
            }

            profile.ArchivedAtUtc = DateTimeOffset.UtcNow;
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            foreach (var project in saveData.ConnectedProjects ?? new List<ConnectedProject>())
            {
                if (string.Equals(project.Id, repositoryHash, StringComparison.Ordinal) ||
                    string.Equals(project.PathHash, repositoryHash, StringComparison.Ordinal) ||
                    string.Equals(project.ProjectPathHash, repositoryHash, StringComparison.Ordinal))
                {
                    project.IsActive = false;
                    project.IsArchived = true;
                }
            }

            var connectedIds = ConnectedRepositoryIds(saveData);
            var nextActive = saveData.RepositoryCompanionProfiles.FirstOrDefault(item => item.ArchivedAtUtc == null && connectedIds.Contains(item.RepositoryHash));
            if (nextActive == null)
            {
                saveData.SelectedRepositoryHash = string.Empty;
                saveData.CompanionState = CompanionState.CreateDefault();
                saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(saveData.DesktopCompanionSettings);
                return Result.Success();
            }

            saveData.SelectedRepositoryHash = nextActive.RepositoryHash;
            SyncConnectedProjectActiveFlags(saveData);
            saveData.CompanionState = CompanionProgressionRules.Normalize(nextActive.CompanionState);
            saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(nextActive.DesktopCompanionSettings);
            return Result.Success();
        }

        public static bool IsConnectedRepository(SaveData saveData, string repositoryHash)
        {
            if (saveData == null || string.IsNullOrWhiteSpace(repositoryHash))
            {
                return false;
            }

            return (saveData.ConnectedProjects ?? new List<ConnectedProject>())
                .Any(project => project != null &&
                                !project.IsArchived &&
                                project.ApprovedAt != null &&
                                !IsStaleFallbackProject(project) &&
                                (string.Equals(project.Id, repositoryHash, StringComparison.Ordinal) ||
                                 string.Equals(project.PathHash, repositoryHash, StringComparison.Ordinal) ||
                                 string.Equals(project.ProjectPathHash, repositoryHash, StringComparison.Ordinal)));
        }

        public static string SafeRepositoryHashForSession(AgentWorkSession session)
        {
            return session?.GitChangeSummary?.ProjectPathHash ?? string.Empty;
        }

        private static RepositoryCompanionProfile CreateDefaultProfileFromLegacyCompanion(CompanionState legacyCompanion)
        {
            return CreateProfile(DefaultLocalRepositoryHash, "Repository", legacyCompanion);
        }

        private static RepositoryCompanionProfile FindExistingProfileForRepository(SaveData saveData, string repositoryHash, string legacyHash, string safeAlias)
        {
            var profiles = saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>();
            var exact = profiles.FirstOrDefault(item => string.Equals(item.RepositoryHash, repositoryHash, StringComparison.Ordinal));
            if (exact != null)
            {
                return exact;
            }

            var legacy = profiles.FirstOrDefault(item => !string.IsNullOrWhiteSpace(legacyHash) &&
                                                        string.Equals(item.RepositoryHash, legacyHash, StringComparison.Ordinal));
            if (legacy != null)
            {
                UnityEngine.Debug.Log("INFO [CompanionProfile][LOOKUP_LEGACY] repo=" + repositoryHash + " found=true");
                return legacy;
            }

            var allowAliasRestore = !string.IsNullOrWhiteSpace(safeAlias) &&
                                    !string.Equals(safeAlias, "Repository", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(safeAlias, "Local Repository", StringComparison.OrdinalIgnoreCase);
            var aliasMatches = allowAliasRestore
                ? profiles
                    .Where(item => item != null &&
                                   !IsStaleFallbackProfile(item) &&
                                   string.Equals(item.SafeRepositoryAlias, safeAlias, StringComparison.OrdinalIgnoreCase))
                    .ToList()
                : new List<RepositoryCompanionProfile>();
            UnityEngine.Debug.Log("INFO [CompanionProfile][LOOKUP_LEGACY] repo=" + repositoryHash + " found=" + (aliasMatches.Count == 1));
            return aliasMatches.Count == 1 ? aliasMatches[0] : null;
        }

        private static void MigrateRepositoryIdentity(SaveData saveData, string oldRepositoryHash, string newRepositoryHash)
        {
            if (saveData == null || string.IsNullOrWhiteSpace(oldRepositoryHash) || string.IsNullOrWhiteSpace(newRepositoryHash) ||
                string.Equals(oldRepositoryHash, newRepositoryHash, StringComparison.Ordinal))
            {
                return;
            }

            foreach (var profile in saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
            {
                if (string.Equals(profile.RepositoryHash, oldRepositoryHash, StringComparison.Ordinal))
                {
                    profile.RepositoryHash = newRepositoryHash;
                    profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }
            }

            foreach (var project in saveData.ConnectedProjects ?? new List<ConnectedProject>())
            {
                if (string.Equals(project.Id, oldRepositoryHash, StringComparison.Ordinal) ||
                    string.Equals(project.PathHash, oldRepositoryHash, StringComparison.Ordinal) ||
                    string.Equals(project.ProjectPathHash, oldRepositoryHash, StringComparison.Ordinal))
                {
                    project.Id = newRepositoryHash;
                    project.PathHash = newRepositoryHash;
                    project.ProjectPathHash = newRepositoryHash;
                }
            }

            foreach (var session in saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
            {
                if (string.Equals(session?.GitChangeSummary?.ProjectPathHash, oldRepositoryHash, StringComparison.Ordinal))
                {
                    session.GitChangeSummary.ProjectPathHash = newRepositoryHash;
                }
            }

            foreach (var review in saveData.ActivityReviews ?? new List<ActivityReview>())
            {
                if (string.Equals(review?.RepositoryId, oldRepositoryHash, StringComparison.Ordinal))
                {
                    review.RepositoryId = newRepositoryHash;
                }
            }

            if (saveData.PendingNativeActivityReview != null &&
                string.Equals(saveData.PendingNativeActivityReview.RepositoryHash, oldRepositoryHash, StringComparison.Ordinal))
            {
                saveData.PendingNativeActivityReview.RepositoryHash = newRepositoryHash;
            }

            if (string.Equals(saveData.SelectedRepositoryHash, oldRepositoryHash, StringComparison.Ordinal))
            {
                saveData.SelectedRepositoryHash = newRepositoryHash;
            }
        }

        private static void UpsertConnectedRepository(SaveData saveData, RepositoryCompanionProfile profile, string repositoryHash, string source)
        {
            if (saveData == null || profile == null || string.IsNullOrWhiteSpace(repositoryHash))
            {
                return;
            }

            saveData.ConnectedProjects = saveData.ConnectedProjects ?? new List<ConnectedProject>();
            UnityEngine.Debug.Log("INFO [RepositoryStore][UPSERT_BEGIN] repo=" + repositoryHash);
            var project = saveData.ConnectedProjects.FirstOrDefault(item => item != null &&
                                                                            (string.Equals(item.Id, repositoryHash, StringComparison.Ordinal) ||
                                                                             string.Equals(item.PathHash, repositoryHash, StringComparison.Ordinal) ||
                                                                             string.Equals(item.ProjectPathHash, repositoryHash, StringComparison.Ordinal)));
            var now = DateTimeOffset.UtcNow;
            if (project == null)
            {
                project = new ConnectedProject();
                saveData.ConnectedProjects.Add(project);
                UnityEngine.Debug.Log("INFO [RepositoryStore][CREATE_NEW] repo=" + repositoryHash);
            }
            else
            {
                UnityEngine.Debug.Log("INFO [RepositoryStore][RESTORE_EXISTING] repo=" + repositoryHash + " connectedBefore=" + (!project.IsArchived) + " archivedBefore=" + project.IsArchived);
            }

            project.Id = repositoryHash;
            project.DisplayName = profile.SafeRepositoryAlias;
            project.ApprovedAt = project.ApprovedAt ?? profile.ApprovedAtUtc ?? now;
            project.ConnectionSource = "userSelected";
            project.PathHash = repositoryHash;
            project.ProjectPathHash = repositoryHash;
            project.ProjectAlias = profile.SafeRepositoryAlias;
            project.IsGitRepository = true;
            project.IsActive = true;
            project.IsArchived = false;
            project.AnalysisEnabled = true;
            project.CompanionId = profile.CompanionId;
            foreach (var other in saveData.ConnectedProjects.Where(item => item != null && !ReferenceEquals(item, project)))
            {
                other.IsActive = false;
            }

            UnityEngine.Debug.Log("INFO [RepositoryStore][SAVE_COMMIT] repo=" + repositoryHash + " connected=true");
            UnityEngine.Debug.Log("INFO [RepositoryStore][VERIFY_AFTER_SAVE] repo=" + repositoryHash + " found=true connected=true");
            UnityEngine.Debug.Log("INFO [RepositorySelection][SET_ACTIVE] repo=" + repositoryHash + " source=" + source);
        }

        private static List<ConnectedProject> NormalizeConnectedProjects(List<ConnectedProject> projects)
        {
            return (projects ?? new List<ConnectedProject>())
                .Where(project => project != null)
                .Select(project =>
                {
                    project.Id = string.IsNullOrWhiteSpace(project.Id) ? FirstNonEmpty(project.PathHash, project.ProjectPathHash, project.LocalOnlyProjectId) : project.Id;
                    project.PathHash = FirstNonEmpty(project.PathHash, project.ProjectPathHash, project.Id);
                    project.ProjectPathHash = FirstNonEmpty(project.ProjectPathHash, project.PathHash, project.Id);
                    project.DisplayName = string.IsNullOrWhiteSpace(project.DisplayName) ? project.ProjectAlias : project.DisplayName;
                    project.ProjectAlias = string.IsNullOrWhiteSpace(project.ProjectAlias) ? project.DisplayName : project.ProjectAlias;
                    project.ConnectionSource = string.IsNullOrWhiteSpace(project.ConnectionSource) ? "userSelected" : project.ConnectionSource.Trim();
                    return project;
                })
                .Where(project => !string.IsNullOrWhiteSpace(project.Id))
                .GroupBy(project => project.Id, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(project => project.IsActive).ThenBy(project => project.IsArchived).First())
                .ToList();
        }

        private static void EnsureConnectionsForApprovedProfiles(SaveData saveData)
        {
            foreach (var profile in saveData.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
            {
                if (profile == null ||
                    profile.ArchivedAtUtc != null ||
                    string.IsNullOrWhiteSpace(profile.RepositoryHash) ||
                    IsStaleFallbackProfile(profile) ||
                    profile.ApprovedAtUtc == null ||
                    !string.Equals(profile.ConnectionSource, "userSelected", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsConnectedRepository(saveData, profile.RepositoryHash))
                {
                    continue;
                }

                saveData.ConnectedProjects.Add(new ConnectedProject
                {
                    Id = profile.RepositoryHash,
                    DisplayName = profile.SafeRepositoryAlias,
                    ApprovedAt = profile.ApprovedAtUtc,
                    ConnectionSource = "userSelected",
                    ProjectAlias = profile.SafeRepositoryAlias,
                    ProjectPathHash = profile.RepositoryHash,
                    PathHash = profile.RepositoryHash,
                    IsGitRepository = true,
                    IsActive = string.Equals(saveData.SelectedRepositoryHash, profile.RepositoryHash, StringComparison.Ordinal),
                    IsArchived = false,
                    AnalysisEnabled = true,
                    CompanionId = profile.CompanionId
                });
            }
        }

        private static HashSet<string> ConnectedRepositoryIds(SaveData saveData)
        {
            return new HashSet<string>((saveData.ConnectedProjects ?? new List<ConnectedProject>())
                .Where(project => project != null &&
                                  !project.IsArchived &&
                                  project.ApprovedAt != null &&
                                  !IsStaleFallbackProject(project))
                .Select(project => FirstNonEmpty(project.Id, project.PathHash, project.ProjectPathHash))
                .Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);
        }

        private static void SyncConnectedProjectActiveFlags(SaveData saveData)
        {
            foreach (var project in saveData.ConnectedProjects ?? new List<ConnectedProject>())
            {
                if (project == null)
                {
                    continue;
                }

                project.IsActive = !project.IsArchived &&
                                   project.ApprovedAt != null &&
                                   !IsStaleFallbackProject(project) &&
                                   !string.IsNullOrWhiteSpace(saveData.SelectedRepositoryHash) &&
                                   (string.Equals(project.Id, saveData.SelectedRepositoryHash, StringComparison.Ordinal) ||
                                    string.Equals(project.PathHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal) ||
                                    string.Equals(project.ProjectPathHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal));
            }
        }

        private static bool IsDefaultEgg(CompanionState state)
        {
            state = CompanionProgressionRules.Normalize(state);
            return state.Stage == CompanionStage.Egg &&
                   Math.Max(1, state.Level) == 1 &&
                   Math.Max(0, state.CurrentXp) == 0 &&
                   Math.Max(0, state.TotalLifetimeXp) == 0;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static RepositoryCompanionProfile CreateProfile(string repositoryHash, string alias, CompanionState companionState = null)
        {
            var now = DateTimeOffset.UtcNow;
            return new RepositoryCompanionProfile
            {
                SchemaVersion = 1,
                RepositoryHash = repositoryHash ?? string.Empty,
                SafeRepositoryAlias = string.IsNullOrWhiteSpace(alias) ? "Repository" : alias.Trim(),
                ApprovedAtUtc = DateTimeOffset.UtcNow,
                ConnectionSource = string.Equals(repositoryHash, DefaultLocalRepositoryHash, StringComparison.Ordinal) ? "debugFallback" : "userSelected",
                CompanionId = Guid.NewGuid().ToString("N"),
                CompanionState = CompanionProgressionRules.Normalize(companionState),
                DesktopCompanionSettings = CloneDesktopCompanionSettings(null),
                SourceProviderMix = new List<SourceProviderMixEntry>(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }

        private static void NormalizeProfile(RepositoryCompanionProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            profile.SchemaVersion = 1;
            profile.RepositoryHash = profile.RepositoryHash ?? string.Empty;
            profile.SafeRepositoryAlias = string.IsNullOrWhiteSpace(profile.SafeRepositoryAlias)
                ? "Repository"
                : profile.SafeRepositoryAlias.Trim();
            profile.ConnectionSource = string.IsNullOrWhiteSpace(profile.ConnectionSource) ? "userSelected" : profile.ConnectionSource.Trim();
            profile.CompanionId = string.IsNullOrWhiteSpace(profile.CompanionId) ? Guid.NewGuid().ToString("N") : profile.CompanionId;
            profile.CompanionState = CompanionProgressionRules.Normalize(profile.CompanionState);
            profile.DesktopCompanionSettings = CloneDesktopCompanionSettings(profile.DesktopCompanionSettings);
            profile.TokenShop = NormalizeTokenShop(profile.TokenShop);
            profile.SourceProviderMix = profile.SourceProviderMix ?? new List<SourceProviderMixEntry>();
            profile.CreatedAtUtc = profile.CreatedAtUtc == default(DateTimeOffset) ? DateTimeOffset.UtcNow : profile.CreatedAtUtc;
            profile.UpdatedAtUtc = profile.UpdatedAtUtc == default(DateTimeOffset) ? profile.CreatedAtUtc : profile.UpdatedAtUtc;
        }

        private static bool IsStaleFallbackProfile(RepositoryCompanionProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            return string.Equals(profile.RepositoryHash, DefaultLocalRepositoryHash, StringComparison.Ordinal) ||
                   string.Equals(profile.SafeRepositoryAlias, "Local Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "auto", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "default", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "unknown", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "dev", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(profile.ConnectionSource, "debugFallback", StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(profile.ConnectionSource, "userSelected", StringComparison.OrdinalIgnoreCase) &&
                    profile.ApprovedAtUtc == null);
        }

        public static bool IsStaleFallbackProject(ConnectedProject project)
        {
            if (project == null)
            {
                return false;
            }

            var displayName = project.DisplayName ?? string.Empty;
            var alias = project.ProjectAlias ?? string.Empty;
            var source = project.ConnectionSource ?? string.Empty;
            var hasApprovalEvidence = project.ApprovedAt != null &&
                                      (!string.IsNullOrWhiteSpace(project.PathHash) ||
                                       !string.IsNullOrWhiteSpace(project.ProjectPathHash));
            return string.Equals(displayName, "Local Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(alias, "Local Repository", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "auto", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "default", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "unknown", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "dev", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(source, "debugFallback", StringComparison.OrdinalIgnoreCase) ||
                   (string.Equals(source, "userSelected", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(displayName, "Local Repository", StringComparison.OrdinalIgnoreCase) &&
                    !hasApprovalEvidence);
        }

        public static DesktopCompanionSettings CloneDesktopCompanionSettings(DesktopCompanionSettings settings)
        {
            settings = settings ?? DesktopCompanionSettings.CreateDefault();
            return new DesktopCompanionSettings
            {
                SchemaVersion = 2,
                IsDesktopCompanionEnabled = settings.IsDesktopCompanionEnabled,
                MotionMode = Enum.IsDefined(typeof(CompanionDesktopMotionMode), settings.MotionMode)
                    ? settings.MotionMode
                    : CompanionDesktopMotionMode.Normal,
                IsClickThroughEnabled = settings.IsClickThroughEnabled,
                LastOverlayPositionXBucket = settings.LastOverlayPositionXBucket,
                LastOverlayPositionYBucket = settings.LastOverlayPositionYBucket,
                LastOverlayPositionX = settings.LastOverlayPositionX,
                LastOverlayPositionY = settings.LastOverlayPositionY,
                HasSavedOverlayPosition = settings.HasSavedOverlayPosition && settings.LastOverlayPositionX >= 0f && settings.LastOverlayPositionY >= 0f,
                VisualThemeId = CompanionSkinCatalog.Normalize(settings.VisualThemeId),
                ZodiacTypeId = NormalizeZodiacTypeId(settings.ZodiacTypeId, "repository")
            };
        }

        private static DesktopCompanionSettings DisabledDesktopCompanionSettings(DesktopCompanionSettings settings)
        {
            var clone = CloneDesktopCompanionSettings(settings);
            clone.IsDesktopCompanionEnabled = false;
            return clone;
        }

        private static TokenShopState NormalizeTokenShop(TokenShopState tokenShop)
        {
            tokenShop = tokenShop ?? new TokenShopState();
            tokenShop.CurrencyName = string.IsNullOrWhiteSpace(tokenShop.CurrencyName) ? "Forge Coins" : tokenShop.CurrencyName.Trim();
            tokenShop.CurrencyBalance = Math.Max(0, tokenShop.CurrencyBalance);
            tokenShop.LifetimeTokenUsageScore = Math.Max(0, tokenShop.LifetimeTokenUsageScore);
            tokenShop.PurchasedItemIds = tokenShop.PurchasedItemIds ?? new List<string>();
            tokenShop.EquippedItemIds = tokenShop.EquippedItemIds ?? new List<string>();
            tokenShop.PurchaseHistory = tokenShop.PurchaseHistory ?? new List<TokenShopPurchaseHistoryEntry>();
            return tokenShop;
        }

        private sealed class TokenShopTargetContext
        {
            public ShopTargetType TargetType { get; set; }
            public string TargetId { get; set; } = string.Empty;
            public TokenShopState OwnershipShop { get; set; }
            public TokenShopState WalletShop { get; set; }
            public RepositoryCompanionProfile RepositoryProfile { get; set; }
            public AiAgentShopState AgentShopState { get; set; }
        }

        private static Result<TokenShopTargetContext> ResolveTokenShopTarget(SaveData saveData, ShopTargetType targetType, string targetId, bool targetConnected)
        {
            if (targetType == ShopTargetType.RepositoryCompanion)
            {
                var profile = GetSelectedProfile(saveData);
                if (profile == null || !IsConnectedRepository(saveData, profile.RepositoryHash))
                {
                    return Result<TokenShopTargetContext>.Failure("no_active_repository", "Connect a repository first.");
                }

                profile.TokenShop = NormalizeTokenShop(profile.TokenShop);
                return Result<TokenShopTargetContext>.Success(new TokenShopTargetContext
                {
                    TargetType = ShopTargetType.RepositoryCompanion,
                    TargetId = profile.RepositoryHash,
                    OwnershipShop = profile.TokenShop,
                    WalletShop = profile.TokenShop,
                    RepositoryProfile = profile
                });
            }

            var agentId = NormalizeAgentShopId(targetId);
            if (string.IsNullOrWhiteSpace(agentId))
            {
                return Result<TokenShopTargetContext>.Failure("missing_agent_id", "Select an AI agent before using agent cosmetics.");
            }

            if (!targetConnected)
            {
                return Result<TokenShopTargetContext>.Failure("agent_not_connected", "Connect this AI agent to unlock agent cosmetics.");
            }

            var agentState = GetOrCreateAgentShopState(saveData, agentId);
            return Result<TokenShopTargetContext>.Success(new TokenShopTargetContext
            {
                TargetType = ShopTargetType.AiAgent,
                TargetId = agentId,
                OwnershipShop = agentState.TokenShop,
                WalletShop = agentState.TokenShop,
                RepositoryProfile = GetSelectedProfile(saveData),
                AgentShopState = agentState
            });
        }

        public static AiAgentShopState GetOrCreateAgentShopState(SaveData saveData, string agentId)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            saveData.AiAgentShopStates = saveData.AiAgentShopStates ?? new List<AiAgentShopState>();
            agentId = NormalizeAgentShopId(agentId);
            var state = saveData.AiAgentShopStates.FirstOrDefault(item => string.Equals(item.AgentId, agentId, StringComparison.Ordinal));
            if (state == null)
            {
                state = new AiAgentShopState { AgentId = agentId, DisplayName = AgentDisplayName(agentId), ZodiacTypeId = NormalizeZodiacTypeId(string.Empty, agentId), TokenShop = new TokenShopState() };
                saveData.AiAgentShopStates.Add(state);
            }

            state.AgentId = agentId;
            state.DisplayName = string.IsNullOrWhiteSpace(state.DisplayName) ? AgentDisplayName(agentId) : state.DisplayName.Trim();
            state.ZodiacTypeId = NormalizeZodiacTypeId(state.ZodiacTypeId, agentId);
            state.TokenShop = NormalizeTokenShop(state.TokenShop);
            state.TokenShop.CurrencyName = AgentCurrencyName(agentId);
            return state;
        }

        public static TokenShopState GetAgentTokenShopState(SaveData saveData, string agentId)
        {
            agentId = NormalizeAgentShopId(agentId);
            var tokenShop = (saveData?.AiAgentShopStates ?? new List<AiAgentShopState>())
                .FirstOrDefault(item => string.Equals(item.AgentId, agentId, StringComparison.Ordinal))
                ?.TokenShop ?? new TokenShopState();
            tokenShop = NormalizeTokenShop(tokenShop);
            tokenShop.CurrencyName = AgentCurrencyName(agentId);
            return tokenShop;
        }

        public static string NormalizeAgentShopId(string agentId)
        {
            agentId = string.IsNullOrWhiteSpace(agentId) ? string.Empty : agentId.Trim();
            if (string.Equals(agentId, "claude", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(agentId, "claudecode", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(agentId, "claudeCode", StringComparison.Ordinal))
            {
                return "claudeCode";
            }

            if (string.Equals(agentId, "gemini", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(agentId, "geminicli", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(agentId, "geminiCli", StringComparison.Ordinal))
            {
                return "geminiCli";
            }

            if (string.Equals(agentId, "githubcopilot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(agentId, "copilot", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(agentId, "githubCopilot", StringComparison.Ordinal))
            {
                return "githubCopilot";
            }

            if (string.Equals(agentId, "codex", StringComparison.OrdinalIgnoreCase))
            {
                return "codex";
            }

            if (string.Equals(agentId, "cursor", StringComparison.OrdinalIgnoreCase))
            {
                return "cursor";
            }

            if (string.Equals(agentId, "manual", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(agentId, "other", StringComparison.OrdinalIgnoreCase))
            {
                return "manual";
            }

            return agentId;
        }

        public static string AgentDisplayName(string agentId)
        {
            switch (NormalizeAgentShopId(agentId))
            {
                case "codex": return "Codex";
                case "claudeCode": return "Claude Code";
                case "geminiCli": return "Gemini CLI";
                case "cursor": return "Cursor";
                case "githubCopilot": return "GitHub Copilot";
                case "manual": return "Other Agent";
                default: return string.IsNullOrWhiteSpace(agentId) ? "AI Agent" : agentId.Trim();
            }
        }

        public static string AgentCurrencyName(string agentId)
        {
            return AgentDisplayName(agentId) + " Coins";
        }

        public static string NormalizeZodiacTypeId(string zodiacTypeId, string stableSeed)
        {
            zodiacTypeId = string.IsNullOrWhiteSpace(zodiacTypeId) ? string.Empty : zodiacTypeId.Trim();
            if (ZodiacCompanionTypes.Any(zodiac => string.Equals(zodiac.Id, zodiacTypeId, StringComparison.Ordinal)))
            {
                return zodiacTypeId;
            }

            var seed = NormalizeAgentShopId(stableSeed);
            switch (seed)
            {
                case "codex": return "dragon";
                case "claudeCode": return "rabbit";
                case "geminiCli": return "monkey";
                case "cursor": return "tiger";
                case "githubCopilot": return "rooster";
                case "manual": return "dog";
                case "repository": return "rat";
                default:
                    var zodiac = ZodiacCompanionTypes;
                    var hash = (seed ?? string.Empty).GetHashCode() & int.MaxValue;
                    var index = hash % zodiac.Count;
                    return zodiac[index].Id;
            }
        }

        public static string ZodiacDisplayName(string zodiacTypeId)
        {
            var zodiac = ZodiacCompanionTypes.FirstOrDefault(item => string.Equals(item.Id, zodiacTypeId, StringComparison.Ordinal));
            return zodiac == null ? "Zodiac" : zodiac.EnglishName + " / " + zodiac.KoreanName;
        }

        private static void ApplyAgentTokenShopCurrency(SaveData saveData)
        {
            saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new List<AgentWorkSession>();
            foreach (var group in saveData.WorkSessionSummaries
                         .Where(session => session != null && !IsGitGrowthSession(session))
                         .GroupBy(AgentShopIdForSession, StringComparer.Ordinal))
            {
                var agentId = NormalizeAgentShopId(group.Key);
                if (string.IsNullOrWhiteSpace(agentId) || string.Equals(agentId, "unknown", StringComparison.Ordinal))
                {
                    continue;
                }

                var score = group.Sum(session => TokenCurrencyScore(session.TokenUsageBucket));
                if (score <= 0)
                {
                    continue;
                }

                var state = GetOrCreateAgentShopState(saveData, agentId);
                if (score <= state.TokenShop.LifetimeTokenUsageScore)
                {
                    continue;
                }

                var delta = score - state.TokenShop.LifetimeTokenUsageScore;
                state.TokenShop.LifetimeTokenUsageScore = score;
                state.TokenShop.CurrencyBalance = Math.Max(0, state.TokenShop.CurrencyBalance + delta);
                UnityEngine.Debug.Log("INFO [TokenShop] agentId=" + agentId + " currency=" + state.TokenShop.CurrencyName + " delta=" + delta + " balance=" + state.TokenShop.CurrencyBalance);
            }
        }

        private static string AgentShopIdForSession(AgentWorkSession session)
        {
            if (session?.AgentActivitySummary != null && session.AgentActivitySummary.ProviderType != AgentProviderType.Unknown)
            {
                switch (session.AgentActivitySummary.ProviderType)
                {
                    case AgentProviderType.Codex: return "codex";
                    case AgentProviderType.Claude:
                    case AgentProviderType.ClaudeCode: return "claudeCode";
                    case AgentProviderType.GeminiCli: return "geminiCli";
                    case AgentProviderType.Cursor: return "cursor";
                    case AgentProviderType.GitHubCopilot: return "githubCopilot";
                    case AgentProviderType.Manual: return "manual";
                }
            }

            var provider = (session?.SourceProvider ?? string.Empty).Trim();
            if (provider.Length > 0)
            {
                return NormalizeAgentShopId(provider);
            }

            return "unknown";
        }

        private static bool IsItemCompatibleWithTarget(SaveData saveData, TokenShopItemDefinition item, ShopTargetType targetType, string targetId)
        {
            if (item == null || item.TargetType != targetType)
            {
                return false;
            }

            if (targetType == ShopTargetType.RepositoryCompanion)
            {
                return true;
            }

            var compatible = item.CompatibleAgentIds ?? new List<string>();
            var agentId = NormalizeAgentShopId(targetId);
            if (compatible.Count > 0 && !compatible.Any(id => string.Equals(NormalizeAgentShopId(id), agentId, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(item.ZodiacTypeId))
            {
                var agentState = (saveData?.AiAgentShopStates ?? new List<AiAgentShopState>())
                    .FirstOrDefault(state => state != null && string.Equals(state.AgentId, agentId, StringComparison.Ordinal));
                return string.Equals(item.ZodiacTypeId, NormalizeZodiacTypeId(agentState?.ZodiacTypeId, agentId), StringComparison.Ordinal);
            }

            return true;
        }

        private static void EquipPurchasedItem(SaveData saveData, TokenShopTargetContext target, TokenShopItemDefinition item)
        {
            var tokenShop = target.OwnershipShop;
            tokenShop.EquippedItemIds.RemoveAll(id => IsSameEquipSlot(id, item.Category));
            tokenShop.EquippedItemIds.Add(item.ItemId);

            if (target.TargetType == ShopTargetType.RepositoryCompanion && target.RepositoryProfile != null && !string.IsNullOrWhiteSpace(item.VisualThemeId))
            {
                target.RepositoryProfile.DesktopCompanionSettings = CloneDesktopCompanionSettings(target.RepositoryProfile.DesktopCompanionSettings);
                target.RepositoryProfile.DesktopCompanionSettings.VisualThemeId = CompanionSkinCatalog.Normalize(item.VisualThemeId);
                if (string.Equals(target.RepositoryProfile.RepositoryHash, saveData.SelectedRepositoryHash, StringComparison.Ordinal))
                {
                    saveData.DesktopCompanionSettings = CloneDesktopCompanionSettings(target.RepositoryProfile.DesktopCompanionSettings);
                }
            }

            if (target.RepositoryProfile != null)
            {
                target.RepositoryProfile.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        private static bool IsSameEquipSlot(string equippedItemId, ShopItemCategory category)
        {
            var equipped = FullTokenShopCatalog().FirstOrDefault(item => string.Equals(item.ItemId, equippedItemId, StringComparison.Ordinal));
            return equipped != null && equipped.Category == category;
        }

        public static string TargetTypeId(ShopTargetType targetType)
        {
            return targetType == ShopTargetType.AiAgent ? "aiAgent" : "repositoryCompanion";
        }

        public static string CategoryId(ShopItemCategory category)
        {
            switch (category)
            {
                case ShopItemCategory.Skins: return "skins";
                case ShopItemCategory.Outfits: return "outfits";
                case ShopItemCategory.Accessories: return "accessories";
                case ShopItemCategory.Effects: return "effects";
                case ShopItemCategory.Motions: return "motions";
                case ShopItemCategory.Themes: return "themes";
                case ShopItemCategory.Owned: return "owned";
                case ShopItemCategory.Badges: return "badges";
                case ShopItemCategory.TokenEffects: return "tokenEffects";
                default: return "featured";
            }
        }

        private static bool IsGitGrowthSession(AgentWorkSession session)
        {
            return session != null &&
                   string.Equals(session.SourceProvider, "GIT", StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyTokenShopCurrency(RepositoryCompanionProfile profile, IEnumerable<AgentWorkSession> repositorySessions)
        {
            if (profile == null)
            {
                return;
            }

            profile.TokenShop = NormalizeTokenShop(profile.TokenShop);
            if (!profile.TokenShop.TrackLocalAiTokenUsage)
            {
                return;
            }

            var score = (repositorySessions ?? new List<AgentWorkSession>())
                .Where(session => session != null && !IsGitGrowthSession(session))
                .Sum(session => TokenCurrencyScore(session.TokenUsageBucket));
            if (score <= profile.TokenShop.LifetimeTokenUsageScore)
            {
                return;
            }

            var delta = score - profile.TokenShop.LifetimeTokenUsageScore;
            profile.TokenShop.LifetimeTokenUsageScore = score;
            profile.TokenShop.CurrencyBalance += delta;
            UnityEngine.Debug.Log("INFO [TokenShop] repositoryId=" + profile.RepositoryHash + " currency=Forge Coins delta=" + delta + " balance=" + profile.TokenShop.CurrencyBalance);
        }

        private static int TokenCurrencyScore(TokenUsageBucket bucket)
        {
            switch (bucket)
            {
                case TokenUsageBucket.Small: return 1;
                case TokenUsageBucket.Medium: return 3;
                case TokenUsageBucket.Large: return 6;
                case TokenUsageBucket.Huge: return 10;
                default: return 0;
            }
        }

        private static bool ShouldMigrateLegacyDesktopSettings(DesktopCompanionSettings profileSettings, DesktopCompanionSettings legacySettings)
        {
            if (legacySettings == null || profileSettings == null)
            {
                return false;
            }

            return legacySettings.HasSavedOverlayPosition &&
                   !profileSettings.HasSavedOverlayPosition &&
                   legacySettings.LastOverlayPositionX >= 0f &&
                   legacySettings.LastOverlayPositionY >= 0f;
        }

        private static string NextRepositoryAlias(SaveData saveData)
        {
            var count = Math.Max(1, (saveData?.RepositoryCompanionProfiles?.Count ?? 0) + 1);
            return "Repository " + count;
        }

        public static string SafeRepositoryAlias(string repositoryRootPath)
        {
            var alias = string.Empty;
            try
            {
                var trimmed = (repositoryRootPath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                alias = Path.GetFileName(trimmed);
            }
            catch (ArgumentException)
            {
                alias = string.Empty;
            }

            alias = new string(alias.Where(character => char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == ' ').Take(48).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(alias) || string.Equals(alias, "Local Repository", StringComparison.OrdinalIgnoreCase) || new ForbiddenFieldDetector().ContainsSensitiveString(alias))
            {
                alias = "Repository";
            }

            return alias;
        }

        private static string LastActivityBucket(AgentWorkSession session)
        {
            var agentBucket = session?.AgentActivitySummary?.DayBucket;
            if (!string.IsNullOrWhiteSpace(agentBucket))
            {
                return agentBucket;
            }

            var gitBucket = session?.GitChangeSummary?.AnalysisTimeBucket;
            if (!string.IsNullOrWhiteSpace(gitBucket))
            {
                return gitBucket;
            }

            return session == null ? string.Empty : session.EndedAt.UtcDateTime.ToString("yyyy-MM-dd");
        }

        private static string SafeProvider(AgentWorkSession session)
        {
            if (session == null)
            {
                return "UNKNOWN";
            }

            var providerType = session.AgentActivitySummary?.ProviderType ?? AgentProviderType.Unknown;
            if (providerType != AgentProviderType.Unknown)
            {
                return providerType == AgentProviderType.Claude ? "ClaudeCode" : providerType.ToString();
            }

            return string.IsNullOrWhiteSpace(session.SourceProvider) ? "UNKNOWN" : session.SourceProvider.Trim();
        }

        private static string SafeTimelineText(string value, string fallback, int maxLength)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        private static void IncrementProviderMix(RepositoryCompanionProfile profile, string provider)
        {
            profile.SourceProviderMix = profile.SourceProviderMix ?? new List<SourceProviderMixEntry>();
            provider = string.IsNullOrWhiteSpace(provider) ? "UNKNOWN" : provider.Trim();
            var entry = profile.SourceProviderMix.FirstOrDefault(item => string.Equals(item.Provider, provider, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                profile.SourceProviderMix.Add(new SourceProviderMixEntry { Provider = provider, ApprovedAggregateCount = 1 });
                return;
            }

            entry.ApprovedAggregateCount += 1;
        }
    }
}
