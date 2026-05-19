using TokenForge.Client.Domain;
using TokenForge.Client.Platform;
using System;
using UnityEngine;

namespace TokenForge.Client.UI
{
    public sealed class DesktopCompanionOverlayController : MonoBehaviour
    {
        private const string LogPrefix = "[DesktopCompanion]";
        private IDesktopCompanionOverlayService overlayService;
        private IApplicationLifecycleService lifecycleService;
        private CompanionDesktopMovementController movementController;
        private CompanionState companionState = CompanionState.CreateDefault();
        private DesktopCompanionSettings settings = DesktopCompanionSettings.CreateDefault();
        private string lastFailureReason = string.Empty;
        private CompanionVisualProfile visualProfile = CompanionVisualProfileResolver.Resolve(CompanionState.CreateDefault());
        private float reactionCooldownRemaining;
        private bool overlayEnabledLogged;

        public CompanionDesktopOverlayState OverlayState => overlayService?.State ?? CompanionDesktopOverlayState.Unavailable;
        public string OverlayStatusMessage => string.IsNullOrWhiteSpace(lastFailureReason)
            ? (overlayService?.StatusMessage ?? "Desktop companion service unavailable.")
            : lastFailureReason;
        public bool IsNativeOverlayActive => overlayService != null && overlayService.IsNativeOverlay && overlayService.State == CompanionDesktopOverlayState.Active;
        public event Action<Vector2> PositionChanged;
        public event Action DashboardRestoreRequested;

        public void Initialize(IDesktopCompanionOverlayService service = null, IApplicationLifecycleService lifecycle = null)
        {
            if (overlayService != null)
            {
                overlayService.Clicked -= OnDesktopCompanionClicked;
                overlayService.DoubleClicked -= OnDesktopCompanionDoubleClicked;
                overlayService.DragEnded -= OnDesktopCompanionDragEnded;
            }

            overlayService = service ?? CreateDefaultService();
            lifecycleService = lifecycle ?? new MacApplicationLifecycleService();
            lifecycleService.Install();
            overlayService.Clicked += OnDesktopCompanionClicked;
            overlayService.DoubleClicked += OnDesktopCompanionDoubleClicked;
            overlayService.DragEnded += OnDesktopCompanionDragEnded;
            movementController = new CompanionDesktopMovementController(overlayService);
            if (!overlayService.Create() && service == null && overlayService.State != CompanionDesktopOverlayState.Fallback)
            {
                lastFailureReason = overlayService.StatusMessage;
                Debug.Log("INFO " + LogPrefix + " fallback companion active");
                overlayService.Clicked -= OnDesktopCompanionClicked;
                overlayService.DoubleClicked -= OnDesktopCompanionDoubleClicked;
                overlayService.DragEnded -= OnDesktopCompanionDragEnded;
                overlayService = new InAppCompanionOverlayFallbackService();
                overlayService.Clicked += OnDesktopCompanionClicked;
                overlayService.DoubleClicked += OnDesktopCompanionDoubleClicked;
                overlayService.DragEnded += OnDesktopCompanionDragEnded;
                overlayService.Create();
                movementController = new CompanionDesktopMovementController(overlayService);
            }
            ApplySettings(settings, companionState);
        }

        public void ApplySettings(DesktopCompanionSettings desktopSettings, CompanionState state)
        {
            settings = desktopSettings ?? DesktopCompanionSettings.CreateDefault();
            companionState = CompanionProgressionRules.Normalize(state);
            visualProfile = CompanionVisualProfileResolver.Resolve(companionState, settings.MotionMode);
            if (overlayService == null)
            {
                Initialize();
                return;
            }

            if (!settings.IsDesktopCompanionEnabled)
            {
                overlayEnabledLogged = false;
                if (overlayService.State != CompanionDesktopOverlayState.Unavailable)
                {
                    lastFailureReason = string.Empty;
                    overlayService.Hide();
                    overlayService.Destroy();
                }

                return;
            }

            if (!overlayEnabledLogged)
            {
                overlayEnabledLogged = true;
                Debug.Log("INFO " + LogPrefix + " companion overlay enabled");
            }

            if (overlayService.State == CompanionDesktopOverlayState.Unavailable && !overlayService.Create())
            {
                lastFailureReason = overlayService.StatusMessage;
                overlayService.Clicked -= OnDesktopCompanionClicked;
                overlayService.DoubleClicked -= OnDesktopCompanionDoubleClicked;
                overlayService.DragEnded -= OnDesktopCompanionDragEnded;
                Debug.Log("INFO " + LogPrefix + " fallback companion active");
                overlayService = new InAppCompanionOverlayFallbackService();
                overlayService.Create();
                overlayService.Clicked += OnDesktopCompanionClicked;
                overlayService.DoubleClicked += OnDesktopCompanionDoubleClicked;
                overlayService.DragEnded += OnDesktopCompanionDragEnded;
                movementController = new CompanionDesktopMovementController(overlayService);
                overlayService.Show();
                return;
            }

            if (overlayService.State != CompanionDesktopOverlayState.Fallback)
            {
                lastFailureReason = string.Empty;
            }

            overlayService.SetClickEnabled(true);
            overlayService.SetClickThrough(settings.IsClickThroughEnabled);
            overlayService.SetSize(SizeFor(companionState.Stage));
            overlayService.SetVisualState(companionState.Stage, companionState.Archetype, visualProfile.IdleAnimation, false);
            overlayService.SetMotionProfile(visualProfile);
            if (settings.HasSavedOverlayPosition)
            {
                var savedPosition = new Vector2(settings.LastOverlayPositionX, settings.LastOverlayPositionY);
                movementController?.SetPosition(savedPosition);
                overlayService.SetPosition(savedPosition);
            }

            overlayService.Show();
        }

        public void ResetPosition()
        {
            movementController?.ResetPosition();
            overlayService?.ResetPosition();
        }

        public void OnDesktopCompanionClicked()
        {
            if (settings != null && settings.IsClickThroughEnabled)
            {
                return;
            }

            if (reactionCooldownRemaining > 0f)
            {
                return;
            }

            Debug.Log("INFO " + LogPrefix + " single click reaction triggered");
            reactionCooldownRemaining = Mathf.Max(0.5f, visualProfile?.ReactionProfile?.CooldownSeconds ?? 1.1f);
            overlayService?.TriggerReaction(
                visualProfile?.ReactionProfile?.PrimaryClickReaction ?? CompanionReaction.Tap,
                visualProfile?.ReactionProfile?.DefaultSpeech ?? "Ready to grow!");
        }

        public void OnDesktopCompanionDoubleClicked()
        {
            if (settings != null && settings.IsClickThroughEnabled)
            {
                return;
            }

            Debug.Log("INFO " + LogPrefix + " double click dashboard restore requested");
            lifecycleService?.ShowMainWindow();
            DashboardRestoreRequested?.Invoke();
        }

        private void OnDesktopCompanionDragEnded(Vector2 position)
        {
            movementController?.SetPosition(position);
            Debug.Log("INFO " + LogPrefix + " drag ended with x/y " + position.x.ToString("0.##") + "," + position.y.ToString("0.##"));
            PositionChanged?.Invoke(position);
        }

        private void Update()
        {
            if (reactionCooldownRemaining > 0f)
            {
                reactionCooldownRemaining -= Time.deltaTime;
            }

            movementController?.Tick(Time.deltaTime, companionState, settings);
        }

        private void OnDestroy()
        {
            if (overlayService != null)
            {
                overlayService.Clicked -= OnDesktopCompanionClicked;
                overlayService.DoubleClicked -= OnDesktopCompanionDoubleClicked;
                overlayService.DragEnded -= OnDesktopCompanionDragEnded;
            }

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
