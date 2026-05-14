using System;
using System.Collections.Generic;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class CharacterStats
    {
        public int Logic { get; set; }
        public int Debug { get; set; }
        public int Architecture { get; set; }
        public int Design { get; set; }
        public int Stability { get; set; }
        public int Velocity { get; set; }
        public int Creativity { get; set; }
        public int Efficiency { get; set; }
        public int Stress { get; set; }

        public static CharacterStats Zero()
        {
            return new CharacterStats();
        }

        public void Add(CharacterStats delta)
        {
            Logic += delta.Logic;
            Debug += delta.Debug;
            Architecture += delta.Architecture;
            Design += delta.Design;
            Stability += delta.Stability;
            Velocity += delta.Velocity;
            Creativity += delta.Creativity;
            Efficiency += delta.Efficiency;
            Stress += delta.Stress;
        }
    }

    [Serializable]
    public sealed class CharacterProfile
    {
        public string CharacterId { get; set; } = Guid.NewGuid().ToString("N");
        public string DisplayName { get; set; } = "Token";
        public int Level { get; set; } = 1;
        public int TotalExp { get; set; }
        public EvolutionType CurrentEvolutionType { get; set; } = EvolutionType.Unknown;
        public CharacterStats Stats { get; set; } = CharacterStats.Zero();
        public string AppearanceVariant { get; set; } = "Default";
        public string MoodState { get; set; } = "Neutral";
        public List<string> UnlockedItems { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class StatDelta
    {
        public string StatName { get; set; } = string.Empty;
        public int Delta { get; set; }
    }

    [Serializable]
    public sealed class PrivacyWarning
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
    }

    [Serializable]
    public class GrowthResult
    {
        public string SessionId { get; set; } = string.Empty;
        public int ExpGained { get; set; }
        public int LevelBefore { get; set; }
        public int LevelAfter { get; set; }
        public CharacterStats StatDeltas { get; set; } = CharacterStats.Zero();
        public int StressDelta { get; set; }
        public EvolutionType EvolutionProgressDelta { get; set; } = EvolutionType.Unknown;
        public int EvolutionProgressDeltaAmount { get; set; }
        public List<PrivacyWarning> Warnings { get; set; } = new List<PrivacyWarning>();
        public List<string> RewardTags { get; set; } = new List<string>();
        public bool DailyCapApplied { get; set; }
    }

    [Serializable]
    public sealed class CharacterGrowthResult : GrowthResult
    {
    }
}
