using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenForge.Client.Domain
{
    public static class CompanionEvolutionPathResolver
    {
        public static CompanionEvolutionBias Resolve(CompanionStatProfile lifetimeStats, CompanionStage stage)
        {
            lifetimeStats = lifetimeStats ?? CompanionStatProfile.Empty();
            var ranked = RankedStats(lifetimeStats);
            var dominant = ranked.FirstOrDefault(item => item.Value > 0);
            var secondary = ranked.Skip(1).FirstOrDefault(item => item.Value > 0);
            var main = dominant.Stat == CompanionGrowthStat.Unknown ? CompanionGrowthStat.Code : dominant.Stat;
            var second = secondary.Stat == CompanionGrowthStat.Unknown ? CompanionGrowthStat.Focus : secondary.Stat;
            var currentBias = main + " + " + second;

            return new CompanionEvolutionBias
            {
                DominantStat = dominant.Value > 0 ? main : CompanionGrowthStat.Unknown,
                SecondaryStat = secondary.Value > 0 ? second : CompanionGrowthStat.Unknown,
                MainPath = PathName(main),
                SecondaryTrait = TraitName(second),
                CurrentBias = dominant.Value > 0 ? currentBias : "아직 성장 성향이 정해지지 않았어요",
                NextEvolutionPreview = NextPreview(main, second, stage),
                EggInfluenceText = EggText(main, dominant.Value)
            };
        }

        private static List<StatScore> RankedStats(CompanionStatProfile stats)
        {
            return new List<StatScore>
                {
                    new StatScore(CompanionGrowthStat.Code, Math.Max(0, stats.CodeStat)),
                    new StatScore(CompanionGrowthStat.Focus, Math.Max(0, stats.FocusStat)),
                    new StatScore(CompanionGrowthStat.Debug, Math.Max(0, stats.DebugStat)),
                    new StatScore(CompanionGrowthStat.Design, Math.Max(0, stats.DesignStat)),
                    new StatScore(CompanionGrowthStat.Sync, Math.Max(0, stats.SyncStat))
                }
                .OrderByDescending(item => item.Value)
                .ThenBy(item => Priority(item.Stat))
                .ToList();
        }

        private static int Priority(CompanionGrowthStat stat)
        {
            switch (stat)
            {
                case CompanionGrowthStat.Code: return 0;
                case CompanionGrowthStat.Focus: return 1;
                case CompanionGrowthStat.Debug: return 2;
                case CompanionGrowthStat.Design: return 3;
                case CompanionGrowthStat.Sync: return 4;
                default: return 99;
            }
        }

        private static string PathName(CompanionGrowthStat stat)
        {
            switch (stat)
            {
                case CompanionGrowthStat.Code: return "Code Builder / Dragon";
                case CompanionGrowthStat.Focus: return "Focus Monk / Ox";
                case CompanionGrowthStat.Debug: return "Debug Fox / Tiger";
                case CompanionGrowthStat.Design: return "Design Crane / Rabbit";
                case CompanionGrowthStat.Sync: return "Sync Dog / Horse";
                default: return "Unknown";
            }
        }

        private static string TraitName(CompanionGrowthStat stat)
        {
            switch (stat)
            {
                case CompanionGrowthStat.Code: return "Builder discipline";
                case CompanionGrowthStat.Focus: return "Calm consistency";
                case CompanionGrowthStat.Debug: return "Sharp troubleshooting";
                case CompanionGrowthStat.Design: return "Polished motion";
                case CompanionGrowthStat.Sync: return "Reliable delivery";
                default: return "Unknown";
            }
        }

        private static string NextPreview(CompanionGrowthStat main, CompanionGrowthStat secondary, CompanionStage stage)
        {
            var body = main == CompanionGrowthStat.Code ? "Dragon" :
                main == CompanionGrowthStat.Focus ? "Ox Guardian" :
                main == CompanionGrowthStat.Debug ? "Scanner Fox" :
                main == CompanionGrowthStat.Design ? "Crane Artist" :
                main == CompanionGrowthStat.Sync ? "Courier Horse" : "Repository";
            var detail = secondary == CompanionGrowthStat.Debug ? " with scanner goggles" :
                secondary == CompanionGrowthStat.Design ? " with brush-tail polish" :
                secondary == CompanionGrowthStat.Focus ? " with calm armor" :
                secondary == CompanionGrowthStat.Sync ? " with orbiting sync ring" :
                secondary == CompanionGrowthStat.Code ? " with blueprint aura" : string.Empty;
            var stageName = stage == CompanionStage.Egg ? "Hatchling" : NextStageName(stage);
            return body + detail + " " + stageName;
        }

        private static string NextStageName(CompanionStage stage)
        {
            switch (stage)
            {
                case CompanionStage.Hatchling: return "Child";
                case CompanionStage.Child: return "Teen";
                case CompanionStage.Teen: return "Adult";
                case CompanionStage.Adult: return "Legendary";
                case CompanionStage.Legendary: return "Legendary";
                default: return "Hatchling";
            }
        }

        private static string EggText(CompanionGrowthStat stat, int score)
        {
            if (score <= 0)
            {
                return "아직 부화 전이에요. 최근 Git 성장 성향이 미래 진화 방향에 영향을 줍니다.";
            }

            switch (stat)
            {
                case CompanionGrowthStat.Code: return "Code 기운이 강해지고 있어요.";
                case CompanionGrowthStat.Focus: return "Focus가 안정적으로 자라고 있어요.";
                case CompanionGrowthStat.Debug: return "Debug 성향이 쌓이고 있어요.";
                case CompanionGrowthStat.Design: return "Design 감각이 선명해지고 있어요.";
                case CompanionGrowthStat.Sync: return "Sync 리듬이 차분하게 자리 잡고 있어요.";
                default: return "최근 Git 성장 성향이 미래 진화 방향에 영향을 줍니다.";
            }
        }

        private readonly struct StatScore
        {
            public StatScore(CompanionGrowthStat stat, int value)
            {
                Stat = stat;
                Value = value;
            }

            public CompanionGrowthStat Stat { get; }
            public int Value { get; }
        }
    }
}
