using System;

namespace TokenForge.Client.Growth
{
    [Serializable]
    public sealed class GrowthCalculationConfig
    {
        public int DailyExpCap { get; set; } = 1200;
        public bool EnableDailyCap { get; set; } = true;
        public float MiniGameBonusMinRatio { get; set; } = 0.05f;
        public float MiniGameBonusMaxRatio { get; set; } = 0.15f;
        public int ExpPerLevel { get; set; } = 1000;
    }
}
