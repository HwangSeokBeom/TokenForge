using TokenForge.Client.Domain;

namespace TokenForge.Client.Platform
{
    public static class CompanionStatusIconProvider
    {
        public static string AssetKeyFor(CompanionState state)
        {
            state = CompanionProgressionRules.Normalize(state);
            return AssetKeyFor(state.Stage);
        }

        public static string AssetKeyFor(CompanionStage stage)
        {
            switch (stage)
            {
                case CompanionStage.Hatching:
                    return "companion.status.hatching.pixel";
                case CompanionStage.Baby:
                    return "companion.status.baby.pixel";
                case CompanionStage.Junior:
                    return "companion.status.junior.pixel";
                case CompanionStage.Adult:
                    return "companion.status.adult.pixel";
                default:
                    return "companion.status.egg.pixel";
            }
        }
    }
}
