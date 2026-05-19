using System;

namespace TokenForge.Client.Domain
{
    [Serializable]
    public sealed class CompanionGrowthSignal
    {
        public CompanionArchetype Archetype { get; set; } = CompanionArchetype.Unknown;
        public int ScoreDelta { get; set; }
        public string ReasonId { get; set; } = string.Empty;

        public CompanionGrowthSignal()
        {
        }

        public CompanionGrowthSignal(CompanionArchetype archetype, int scoreDelta, string reasonId)
        {
            Archetype = archetype;
            ScoreDelta = scoreDelta;
            ReasonId = reasonId ?? string.Empty;
        }
    }
}
