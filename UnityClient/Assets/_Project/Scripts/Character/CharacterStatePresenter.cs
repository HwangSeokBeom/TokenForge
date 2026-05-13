using TokenForge.Client.Domain;

namespace TokenForge.Client.Character
{
    public sealed class CharacterStatePresenter
    {
        public string GetMoodState(CharacterProfile profile)
        {
            if (profile == null) return "Unknown";
            if (profile.Stats.Stress >= 20) return "Overloaded";
            if (profile.Stats.Stability >= profile.Stats.Stress) return "Focused";
            return profile.MoodState;
        }
    }
}
