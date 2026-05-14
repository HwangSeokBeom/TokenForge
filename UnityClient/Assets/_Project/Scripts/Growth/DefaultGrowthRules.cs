using System.Collections.Generic;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Growth
{
    public static class DefaultGrowthRules
    {
        public static List<GrowthRule> Create()
        {
            return new List<GrowthRule>
            {
                Rule("feature", WorkType.Feature, 120, new CharacterStats { Logic = 3, Velocity = 2, Creativity = 2 }),
                Rule("bugfix", WorkType.Bugfix, 110, new CharacterStats { Debug = 3, Stability = 2, Logic = 1 }),
                Rule("refactor", WorkType.Refactor, 115, new CharacterStats { Architecture = 3, Logic = 2, Efficiency = 2 }),
                Rule("test", WorkType.Test, 100, new CharacterStats { Stability = 3, Debug = 2, Efficiency = 1 }),
                Rule("uiux", WorkType.UIUX, 105, new CharacterStats { Design = 3, Creativity = 2, Velocity = 1 }),
                Rule("docs", WorkType.Docs, 80, new CharacterStats { Architecture = 2, Efficiency = 2, Logic = 1 }),
                Rule("build", WorkType.Build, 90, new CharacterStats { Stability = 2, Debug = 2, Efficiency = 1 }),
                Rule("chore", WorkType.Chore, 70, new CharacterStats { Efficiency = 2, Stability = 1 }),
                Rule("research", WorkType.Research, 85, new CharacterStats { Logic = 2, Creativity = 2 }),
                Rule("mixed", WorkType.Mixed, 100, new CharacterStats { Logic = 1, Debug = 1, Architecture = 1, Design = 1, Stability = 1, Velocity = 1, Creativity = 1, Efficiency = 1 }),
                Rule("unknown", WorkType.Unknown, 50, new CharacterStats { Logic = 1 })
            };
        }

        private static GrowthRule Rule(string id, WorkType workType, int baseExp, CharacterStats stats)
        {
            return new GrowthRule
            {
                RuleId = id,
                TargetWorkType = workType,
                BaseExp = baseExp,
                StatWeights = stats,
                SuccessBonus = 0.12f,
                FailureCompensation = 0.35f,
                StressPenalty = workType == WorkType.Bugfix || workType == WorkType.Build ? 2 : 1
            };
        }
    }
}
