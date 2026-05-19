using System.Linq;
using TokenForge.Client.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class CompanionStatusPanelView : UiBinderBase
    {
        [SerializeField] public Text statusLabel;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;
            Render();
        }

        public void Render()
        {
            var state = viewModel?.CharacterDashboard?.CompanionState ?? CompanionState.CreateDefault();
            SetText(statusLabel, Format(state));
        }

        public static string Format(CompanionState state)
        {
            state = CompanionProgressionRules.Normalize(state);
            var archetype = state.Archetype == CompanionArchetype.Unknown ? "Not assigned" : state.Archetype.ToString();
            var next = state.XpToNextStage <= 0 ? state.TotalXp + " / max stage" : state.TotalXp + " / " + (state.TotalXp + state.XpToNextStage);
            var explanation = state.TotalXp <= 0
                ? "No approved growth yet."
                : "Grew from approved aggregate Git + agent activity.";
            var reasons = state.LastGrowthReasonIds == null || state.LastGrowthReasonIds.Count == 0
                ? "none"
                : string.Join(", ", state.LastGrowthReasonIds.Take(4).ToArray());

            return "Companion Status"
                   + "\nStage: " + state.Stage
                   + "\nArchetype: " + archetype
                   + "\nLevel: " + state.Level
                   + "\nXP / next: " + next
                   + "\n" + explanation
                   + "\nSignals: " + reasons;
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            return Require(statusLabel, nameof(statusLabel), ref error);
        }
    }
}
