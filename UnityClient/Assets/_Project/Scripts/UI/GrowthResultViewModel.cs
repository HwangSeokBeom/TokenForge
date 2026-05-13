using TokenForge.Client.Domain;

namespace TokenForge.Client.UI
{
    public sealed class GrowthResultViewModel
    {
        public CharacterGrowthResult LatestResult { get; private set; }

        public void SetResult(CharacterGrowthResult result)
        {
            LatestResult = result;
        }
    }
}
