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
                case CompanionStage.Hatchling:
                    return "companion.status.hatchling.pixel";
                case CompanionStage.Child:
                    return "companion.status.child.pixel";
                case CompanionStage.Teen:
                    return "companion.status.teen.pixel";
                case CompanionStage.Adult:
                    return "companion.status.adult.pixel";
                case CompanionStage.Legendary:
                    return "companion.status.legendary.pixel";
                default:
                    return "companion.status.egg.pixel";
            }
        }
    }
}
