using TokenForge.Client.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class CompanionView : UiBinderBase
    {
        [SerializeField] public RectTransform movementArea;
        [SerializeField] public RectTransform characterRoot;
        [SerializeField] public Image bodyImage;
        [SerializeField] public Text stageLabel;
        [SerializeField] public Text markerLabel;
        [SerializeField] public CompanionMovementController movementController;

        private ApprovedActivityAnalysisViewModel viewModel;
        private float frameTimer;
        private bool walkFrame;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;
            Render();
        }

        public void Render(bool reduceMotion = false)
        {
            var state = viewModel?.CharacterDashboard?.CompanionState ?? CompanionState.CreateDefault();
            Render(state, reduceMotion);
        }

        public void Render(CompanionState state, bool reduceMotion)
        {
            state = CompanionProgressionRules.Normalize(state);
            SetText(stageLabel, StageLabel(state));
            SetText(markerLabel, string.Empty);
            if (bodyImage != null)
            {
                bodyImage.color = Color.white;
                bodyImage.sprite = CompanionPixelArtFactory.GetSprite(state, walkFrame);
                bodyImage.preserveAspect = true;
            }

            if (characterRoot != null)
            {
                var size = SizeFor(state.Stage);
                characterRoot.sizeDelta = new Vector2(size, size);
            }

            if (movementController != null)
            {
                movementController.Configure(movementArea, characterRoot, bodyImage);
                movementController.SetStage(state.Stage);
                movementController.SetMotionReduced(reduceMotion);
            }
        }

        private void Update()
        {
            frameTimer += Time.deltaTime;
            if (frameTimer < 0.32f)
            {
                return;
            }

            frameTimer = 0f;
            walkFrame = !walkFrame;
            Render();
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(movementArea, nameof(movementArea), ref error);
            valid &= Require(characterRoot, nameof(characterRoot), ref error);
            valid &= Require(bodyImage, nameof(bodyImage), ref error);
            valid &= Require(stageLabel, nameof(stageLabel), ref error);
            valid &= Require(markerLabel, nameof(markerLabel), ref error);
            valid &= Require(movementController, nameof(movementController), ref error);
            return valid;
        }

        private static string StageLabel(CompanionState state)
        {
            if (state.Stage == CompanionStage.Egg && state.TotalXp <= 0)
            {
                return "Egg | no approved growth yet";
            }

            return state.Stage + " | " + state.Archetype;
        }

        private static float SizeFor(CompanionStage stage)
        {
            switch (stage)
            {
                case CompanionStage.Hatchling: return 78f;
                case CompanionStage.Child: return 82f;
                case CompanionStage.Teen: return 98f;
                case CompanionStage.Adult: return 118f;
                case CompanionStage.Legendary: return 126f;
                default: return 72f;
            }
        }

    }
}
