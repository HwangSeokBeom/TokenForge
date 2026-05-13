namespace TokenForge.Client.MiniGame
{
    public static class MiniGameBonusPolicy
    {
        public const float MinimumBonusRatio = 0.05f;
        public const float MaximumBonusRatio = 0.15f;

        public static float ClampBonusRatio(float requestedRatio)
        {
            if (requestedRatio <= 0f) return 0f;
            if (requestedRatio < MinimumBonusRatio) return MinimumBonusRatio;
            if (requestedRatio > MaximumBonusRatio) return MaximumBonusRatio;
            return requestedRatio;
        }
    }
}
