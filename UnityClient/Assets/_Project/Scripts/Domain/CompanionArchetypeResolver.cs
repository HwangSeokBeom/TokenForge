using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenForge.Client.Domain
{
    public static class CompanionArchetypeResolver
    {
        public static CompanionArchetype Resolve(CompanionGrowthProfile profile)
        {
            if (profile == null || profile.ApprovedSessionCount <= 0)
            {
                return CompanionArchetype.Unknown;
            }

            var scores = new Dictionary<CompanionArchetype, int>
            {
                { CompanionArchetype.Explorer, profile.ExplorationScore + ProviderDiversityBonus(profile) },
                { CompanionArchetype.Builder, profile.ImplementationScore + Math.Max(0, profile.CursorProviderCount + profile.CodexProviderCount) },
                { CompanionArchetype.Debugger, profile.DebugScore + Math.Max(0, profile.CodexProviderCount) },
                { CompanionArchetype.Refiner, profile.CleanupScore + Math.Max(0, profile.CopilotProviderCount + profile.SteadyReviewSaveCount) },
                { CompanionArchetype.Architect, profile.StructureScore + Math.Max(0, profile.ClaudeCodeProviderCount) },
                { CompanionArchetype.Sprinter, profile.BurstScore }
            };

            if (profile.LargeBurstSessionCount > 0 && profile.BurstScore >= scores.Values.Max() - 1)
            {
                return CompanionArchetype.Sprinter;
            }

            if ((profile.ProviderDiversityBucket == CountBucket.Small ||
                 profile.ProviderDiversityBucket == CountBucket.Medium ||
                 profile.ProviderDiversityBucket == CountBucket.Large ||
                 profile.ProviderDiversityBucket == CountBucket.Huge) &&
                profile.ExplorationScore >= scores.Values.Max() - 1)
            {
                return CompanionArchetype.Explorer;
            }

            if (scores.Values.Max() <= 0)
            {
                return CompanionArchetype.Unknown;
            }

            return scores
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => Priority(pair.Key))
                .First(pair => pair.Value > 0)
                .Key;
        }

        private static int ProviderDiversityBonus(CompanionGrowthProfile profile)
        {
            switch (profile.ProviderDiversityBucket)
            {
                case CountBucket.Small:
                case CountBucket.Medium:
                    return 2;
                case CountBucket.Large:
                case CountBucket.Huge:
                    return 4;
                default:
                    return 0;
            }
        }

        private static int Priority(CompanionArchetype archetype)
        {
            switch (archetype)
            {
                case CompanionArchetype.Builder: return 0;
                case CompanionArchetype.Debugger: return 1;
                case CompanionArchetype.Architect: return 2;
                case CompanionArchetype.Refiner: return 3;
                case CompanionArchetype.Explorer: return 4;
                case CompanionArchetype.Sprinter: return 5;
                default: return 99;
            }
        }
    }
}
