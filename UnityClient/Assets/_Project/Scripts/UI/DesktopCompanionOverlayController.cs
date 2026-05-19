using TokenForge.Client.Domain;
using TokenForge.Client.Platform;
using UnityEngine;

namespace TokenForge.Client.UI
{
    public sealed class DesktopCompanionOverlayController : MonoBehaviour
    {
        private IDesktopCompanionOverlayService overlayService;
        private CompanionDesktopMovementController movementController;
        private CompanionState companionState = CompanionState.CreateDefault();
        private DesktopCompanionSettings settings = DesktopCompanionSettings.CreateDefault();

        public CompanionDesktopOverlayState OverlayState => overlayService?.State ?? CompanionDesktopOverlayState.Unavailable;

        public void Initialize(IDesktopCompanionOverlayService service = null)
        {
            overlayService = service ?? CreateDefaultService();
            movementController = new CompanionDesktopMovementController(overlayService);
            if (!overlayService.Create() && service == null)
            {
                overlayService = new InAppCompanionOverlayFallbackService();
                overlayService.Create();
                movementController = new CompanionDesktopMovementController(overlayService);
            }
            ApplySettings(settings, companionState);
        }

        public void ApplySettings(DesktopCompanionSettings desktopSettings, CompanionState state)
        {
            settings = desktopSettings ?? DesktopCompanionSettings.CreateDefault();
            companionState = CompanionProgressionRules.Normalize(state);
            if (overlayService == null)
            {
                Initialize();
                return;
            }

            if (!settings.IsDesktopCompanionEnabled)
            {
                overlayService.Hide();
                return;
            }

            if (overlayService.State == CompanionDesktopOverlayState.Unavailable && !overlayService.Create())
            {
                overlayService = new InAppCompanionOverlayFallbackService();
                overlayService.Create();
                movementController = new CompanionDesktopMovementController(overlayService);
                overlayService.Show();
                return;
            }

            overlayService.SetClickThrough(settings.IsClickThroughEnabled);
            overlayService.SetSize(SizeFor(companionState.Stage));
            overlayService.SetVisualState(companionState.Stage, companionState.Archetype, CompanionAnimationState.Idle, false);
            overlayService.Show();
        }

        public void ResetPosition()
        {
            movementController?.ResetPosition();
        }

        private void Update()
        {
            movementController?.Tick(Time.deltaTime, companionState, settings);
        }

        private void OnDestroy()
        {
            overlayService?.Destroy();
        }

        private static Vector2 SizeFor(CompanionStage stage)
        {
            switch (stage)
            {
                case CompanionStage.Hatching: return new Vector2(92f, 92f);
                case CompanionStage.Baby: return new Vector2(96f, 96f);
                case CompanionStage.Junior: return new Vector2(110f, 110f);
                case CompanionStage.Adult: return new Vector2(128f, 128f);
                default: return new Vector2(84f, 84f);
            }
        }

        private static IDesktopCompanionOverlayService CreateDefaultService()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            return new MacDesktopCompanionOverlayService();
#else
            return new InAppCompanionOverlayFallbackService();
#endif
        }
    }
}
