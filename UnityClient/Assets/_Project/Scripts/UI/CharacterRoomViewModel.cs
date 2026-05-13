using TokenForge.Client.Domain;

namespace TokenForge.Client.UI
{
    public sealed class CharacterRoomViewModel
    {
        public CharacterProfile CharacterProfile { get; private set; }

        public CharacterRoomViewModel(CharacterProfile characterProfile)
        {
            CharacterProfile = characterProfile;
        }

        public void ApplyGrowth(CharacterGrowthResult result)
        {
            if (result == null || CharacterProfile == null)
            {
                return;
            }

            CharacterProfile.TotalExp += result.ExpGained;
            CharacterProfile.Level = result.LevelAfter;
            CharacterProfile.Stats.Add(result.StatDeltas);
            if (result.EvolutionProgressDelta != EvolutionType.Unknown)
            {
                CharacterProfile.CurrentEvolutionType = result.EvolutionProgressDelta;
            }
        }
    }
}
