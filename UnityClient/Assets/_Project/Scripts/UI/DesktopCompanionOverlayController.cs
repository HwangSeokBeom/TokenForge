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
        private CompanionMotionState motionState = CompanionMotionState.Idle(string.Empty);
        private string lastFailureReason = string.Empty;
        private CompanionVisualProfile visualProfile = CompanionVisualProfileResolver.Resolve(CompanionState.CreateDefault());
        private float reactionCooldownRemaining;
        private bool overlayEnabledLogged;
        private bool hasPendingDragPosition;
        private Vector2 pendingDragPosition;

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

        public void ApplySettings(DesktopCompanionSettings desktopSettings, CompanionState state, CompanionMotionState motion = null)
        {
            settings = desktopSettings ?? DesktopCompanionSettings.CreateDefault();
            companionState = CompanionProgressionRules.Normalize(state);
            motionState = motion ?? CompanionMotionStateResolver.Resolve(new CompanionMotionSignal { CanLevelUp = companionState.CanLevelUp });
            visualProfile = CompanionVisualProfileResolver.Resolve(companionState, settings.MotionMode);
            ApplyMotionIntensity(visualProfile, motionState);
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

            overlayService.SetClickEnabled(!settings.IsClickThroughEnabled);
            overlayService.SetClickThrough(settings.IsClickThroughEnabled);
            overlayService.SetSize(SizeFor(companionState.Stage));
            overlayService.SetVisualTheme(settings.VisualThemeId);
            overlayService.SetVisualState(companionState.Stage, companionState.Archetype, visualProfile.IdleAnimation, false);
            overlayService.SetMotionProfile(visualProfile);
            if (settings.HasSavedOverlayPosition || hasPendingDragPosition)
            {
                var savedPosition = hasPendingDragPosition
                    ? pendingDragPosition
                    : new Vector2(settings.LastOverlayPositionX, settings.LastOverlayPositionY);
                if (!overlayService.IsDragging)
                {
                    movementController?.SetPosition(savedPosition);
                    overlayService.SetPosition(savedPosition);
                }

                if (hasPendingDragPosition &&
                    settings.HasSavedOverlayPosition &&
                    Vector2.Distance(pendingDragPosition, new Vector2(settings.LastOverlayPositionX, settings.LastOverlayPositionY)) <= 1f)
                {
                    hasPendingDragPosition = false;
                }
            }

            overlayService.Show();
        }

        public void ResetPosition()
        {
            movementController?.ResetPosition();
            overlayService?.ResetPosition();
        }

        public void TriggerReaction(CompanionReaction reaction, string speechText)
        {
            overlayService?.TriggerReaction(reaction, string.IsNullOrWhiteSpace(speechText) ? "Ready to grow!" : speechText);
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
            hasPendingDragPosition = true;
            pendingDragPosition = position;
            movementController?.SetPosition(position);
            Debug.Log("INFO [CompanionDrag] mouseUp final=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ") saved=pending");
            PositionChanged?.Invoke(position);
        }

        private void Update()
        {
            if (reactionCooldownRemaining > 0f)
            {
                reactionCooldownRemaining -= Time.deltaTime;
            }

            movementController?.Tick(Time.deltaTime, companionState, settings, motionState);
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

        private static void ApplyMotionIntensity(CompanionVisualProfile profile, CompanionMotionState motion)
        {
            if (profile?.MotionProfile == null || motion == null)
            {
                return;
            }

            profile.MotionProfile.WanderSpeed *= Mathf.Clamp(motion.MovementSpeed, 0f, 1.6f);
            profile.MotionProfile.IdleRadius += Mathf.Clamp(motion.BounceAmplitude, 0f, 12f) * 0.25f;
            profile.MotionProfile.DecisionIntervalSeconds = Mathf.Max(0.8f, profile.MotionProfile.DecisionIntervalSeconds / Mathf.Max(0.65f, motion.IdleFrequency));
            if (motion.Reaction == CompanionMotionReaction.EvolvePulse)
            {
                profile.IdleAnimation = CompanionAnimationState.GrowthPulse;
                profile.ReactionProfile.PrimaryClickReaction = CompanionReaction.LevelUp;
                profile.ReactionProfile.DefaultSpeech = "Ready to evolve.";
            }
            else if (motion.Reaction == CompanionMotionReaction.ReadyToReview)
            {
                profile.IdleAnimation = CompanionAnimationState.Hop;
                profile.ReactionProfile.PrimaryClickReaction = CompanionReaction.Attention;
                profile.ReactionProfile.DefaultSpeech = "Review is ready.";
            }
            else if (motion.Reaction == CompanionMotionReaction.GrowthSaved)
            {
                profile.IdleAnimation = CompanionAnimationState.GrowthPulse;
                profile.ReactionProfile.PrimaryClickReaction = CompanionReaction.GrowthSaved;
                profile.ReactionProfile.DefaultSpeech = "Growth saved.";
            }
            else if (motion.Reaction == CompanionMotionReaction.AiPulse ||
                     motion.Reaction == CompanionMotionReaction.TokenPulse)
            {
                profile.IdleAnimation = CompanionAnimationState.GrowthPulse;
                profile.MotionProfile.WanderSpeed *= 1.15f;
                profile.ReactionProfile.PrimaryClickReaction = CompanionReaction.Attention;
                profile.ReactionProfile.DefaultSpeech = "AI activity detected.";
            }
            else if (motion.Reaction == CompanionMotionReaction.WarningShake)
            {
                profile.IdleAnimation = CompanionAnimationState.HatchShake;
                profile.MotionProfile.WanderSpeed *= 0.55f;
                profile.ReactionProfile.PrimaryClickReaction = CompanionReaction.Attention;
                profile.ReactionProfile.DefaultSpeech = "Review attention items.";
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
