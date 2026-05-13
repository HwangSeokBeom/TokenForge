using System;
using System.Collections.Generic;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class GrowthRule
    {
        public string RuleId { get; set; } = string.Empty;
        public WorkType TargetWorkType { get; set; } = WorkType.Unknown;
        public List<string> Conditions { get; set; } = new List<string>();
        public int BaseExp { get; set; }
        public CharacterStats StatWeights { get; set; } = CharacterStats.Zero();
        public float TokenMultiplier { get; set; } = 1f;
        public float FileChangeMultiplier { get; set; } = 1f;
        public float SuccessBonus { get; set; } = 0.1f;
        public float FailureCompensation { get; set; } = 0.25f;
        public int StressPenalty { get; set; }
    }
}
