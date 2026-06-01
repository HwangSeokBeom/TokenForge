using TokenForge.Client.Domain;
using TokenForge.Client.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public bool IsDragging => IsAnyOverlayDragging();
        public event Action<Vector2> PositionChanged;
        public event Action DashboardRestoreRequested;

        public bool IsAnyOverlayDragging()
        {
            return overlayService?.IsAnyOverlayDragging() ?? false;
        }

        public bool IsOverlayDragging(string repositoryId)
        {
            return overlayService?.IsOverlayDragging(repositoryId) ?? false;
        }

        public void Initialize(IDesktopCompanionOverlayService service = null, IApplicationLifecycleService lifecycle = null)
        {
            if (overlayService != null)
            {
                if (overlayService is MacDesktopCompanionOverlayService oldMacService)
                {
                    oldMacService.RepositoryDragEnded -= OnRepositoryCompanionDragEnded;
                }
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
            if (overlayService is MacDesktopCompanionOverlayService macService)
            {
                macService.RepositoryDragEnded += OnRepositoryCompanionDragEnded;
            }
            movementController = new CompanionDesktopMovementController(overlayService);
            if (!overlayService.Create() && service == null && overlayService.State != CompanionDesktopOverlayState.Fallback)
            {
                lastFailureReason = overlayService.StatusMessage;
                Debug.Log("INFO " + LogPrefix + " fallback companion active");
                if (overlayService is MacDesktopCompanionOverlayService failedMacService)
                {
                    failedMacService.RepositoryDragEnded -= OnRepositoryCompanionDragEnded;
                }
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
            else if (overlayService.State == CompanionDesktopOverlayState.Unavailable)
            {
                lastFailureReason = overlayService.StatusMessage;
            }

            if (overlayService.State != CompanionDesktopOverlayState.Unavailable)
            {
                overlayService.HideAllRepositoryCompanions("csharp.initialize");
            }
        }

        public void ApplyFarmSettings(
            IEnumerable<RepositoryCompanionDisplayItem> repositories,
            DesktopCompanionSettings globalSettings,
            bool desiredVisible,
            int renderVersion)
        {
            if (overlayService == null || !overlayService.IsAvailable)
            {
                return;
            }

            var settings = globalSettings ?? DesktopCompanionSettings.CreateDefault();
            overlayService.SetClickEnabled(!settings.IsClickThroughEnabled);
            overlayService.SetClickThrough(settings.IsClickThroughEnabled);
            var overlays = (repositories ?? Enumerable.Empty<RepositoryCompanionDisplayItem>())
                .Where(item => item != null &&
                               !item.Archived &&
                               item.ApprovedByUser &&
                               !string.IsNullOrWhiteSpace(item.RepositoryHash) &&
                               !string.Equals(item.RepositoryHash, RepositoryCompanionProfileService.DefaultLocalRepositoryHash, StringComparison.Ordinal))
                .Select(item =>
                {
                    var isDragging = overlayService.IsOverlayDragging(item.RepositoryHash);
                    if (isDragging)
                    {
                        Debug.Log("INFO [CSharpProjection][SKIP_TO_NATIVE] repo=" + item.RepositoryHash + " reason=overlayDragInProgress");
                    }

                    return new RepositoryCompanionOverlayState
                    {
                        repositoryId = item.RepositoryHash,
                        repositoryName = string.IsNullOrWhiteSpace(item.SafeRepositoryAlias) ? "Repository" : item.SafeRepositoryAlias,
                        companionId = item.CompanionId,
                        desiredVisible = desiredVisible && item.DesktopCompanionEnabled,
                        actualVisible = !isDragging && overlayService.State == CompanionDesktopOverlayState.Active,
                        desiredPositionX = item.OverlayPositionX,
                        desiredPositionY = item.OverlayPositionY,
                        actualPositionX = item.OverlayPositionX,
                        actualPositionY = item.OverlayPositionY,
                        hasSavedPosition = item.HasSavedOverlayPosition,
                        dragEnabled = !settings.IsClickThroughEnabled,
                        isDragging = isDragging,
                        hydratedSnapshot = new NativeCompanionFarmSnapshot
                        {
                            repositoryId = item.RepositoryHash,
                            repositoryName = string.IsNullOrWhiteSpace(item.SafeRepositoryAlias) ? "Repository" : item.SafeRepositoryAlias,
                            companionId = item.CompanionId,
                            stage = (int)item.Stage,
                            level = Math.Max(1, item.Level),
                            xp = Math.Max(0, item.CurrentXp),
                            archetype = (int)item.Archetype,
                            visualThemeId = CompanionSkinCatalog.Normalize(item.Skin),
                            hydrated = true,
                            renderVersion = renderVersion,
                            desiredVisible = desiredVisible && item.DesktopCompanionEnabled,
                            hasSavedPosition = item.HasSavedOverlayPosition,
                            desiredInitialX = item.OverlayPositionX,
                            desiredInitialY = item.OverlayPositionY
                        }
                    };
                })
                .ToArray();
            if (overlays.Length == 0)
            {
                Debug.Log("INFO [FarmProjection][SKIP_PLACEHOLDER] reason=noRepository");
                Debug.Log("INFO [FarmProjection][BUILD] connectedRepositories=0 snapshots=0");
                overlayService.SetCompanionFarmSnapshots(new DesktopCompanionFarmState
                {
                    enabled = false,
                    overlays = new RepositoryCompanionOverlayState[0],
                    visibleCount = 0,
                    draggingRepositoryId = string.Empty,
                    globalMotionEnabled = settings.MotionMode != CompanionDesktopMotionMode.Calm,
                    globalClickThroughEnabled = settings.IsClickThroughEnabled
                });
                overlayService.HideAllRepositoryCompanions("csharp.noConnectedRepositories");
                overlayService.Hide();
                return;
            }

            var farm = new DesktopCompanionFarmState
            {
                enabled = desiredVisible && settings.IsDesktopCompanionEnabled,
                overlays = overlays,
                visibleCount = overlays.Count(item => item.desiredVisible),
                draggingRepositoryId = overlays.FirstOrDefault(item => item.isDragging)?.repositoryId ?? string.Empty,
                globalMotionEnabled = settings.MotionMode != CompanionDesktopMotionMode.Calm,
                globalClickThroughEnabled = settings.IsClickThroughEnabled
            };
            overlayService.SetCompanionFarmSnapshots(farm);
        }

        public void HideLegacyOverlay(string source = "csharp.hideLegacy")
        {
            if (overlayService == null || !overlayService.IsAvailable)
            {
                return;
            }

            overlayService.Hide();
            Debug.Log("INFO [OverlayFarm][HIDE_ALL] reason=noConnectedRepositories source=" + source);
        }

        public void ApplySettings(DesktopCompanionSettings desktopSettings, CompanionState state, CompanionMotionState motion = null, string repositoryId = "", int currentXp = 0)
        {
            settings = desktopSettings ?? DesktopCompanionSettings.CreateDefault();
            visualProfile = CompanionVisualProfileResolver.Resolve(state, settings.MotionMode);
            companionState = CompanionProgressionRules.Normalize(state);
            companionState.Stage = visualProfile.Stage;
            motionState = motion ?? CompanionMotionStateResolver.Resolve(new CompanionMotionSignal { CanLevelUp = companionState.CanLevelUp });
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
                if (overlayService is MacDesktopCompanionOverlayService unavailableMacService)
                {
                    unavailableMacService.RepositoryDragEnded -= OnRepositoryCompanionDragEnded;
                }
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
            if (overlayService is MacDesktopCompanionOverlayService macOverlay)
            {
                macOverlay.SetRenderSnapshot(repositoryId, companionState, currentXp, settings.VisualThemeId);
            }
            overlayService.SetVisualTheme(settings.VisualThemeId);
            overlayService.SetVisualState(companionState.Stage, companionState.Archetype, visualProfile.IdleAnimation, false);
            overlayService.SetMotionProfile(visualProfile);
            if (settings.HasSavedOverlayPosition || hasPendingDragPosition)
            {
                var savedPosition = hasPendingDragPosition
                    ? pendingDragPosition
                    : new Vector2(settings.LastOverlayPositionX, settings.LastOverlayPositionY);
                if (!overlayService.IsAnyOverlayDragging())
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
            Debug.Log("INFO [OverlayPositionSync][COMMIT] source=dragEnd position=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ")");
            PositionChanged?.Invoke(position);
        }

        private void OnRepositoryCompanionDragEnded(string repositoryId, Vector2 position)
        {
            Debug.Log("INFO [OverlayPositionSync][COMMIT] repo=" + repositoryId + " source=dragEnd position=(" + position.x.ToString("0.##") + "," + position.y.ToString("0.##") + ")");
            PositionChanged?.Invoke(position);
            RepositoryPositionChanged?.Invoke(repositoryId, position);
        }

        public event Action<string, Vector2> RepositoryPositionChanged;

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
                if (overlayService is MacDesktopCompanionOverlayService macService)
                {
                    macService.RepositoryDragEnded -= OnRepositoryCompanionDragEnded;
                }
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
                case CompanionStage.Hatchling: return new Vector2(92f, 92f);
                case CompanionStage.Child: return new Vector2(96f, 96f);
                case CompanionStage.Teen: return new Vector2(110f, 110f);
                case CompanionStage.Adult: return new Vector2(128f, 128f);
                case CompanionStage.Legendary: return new Vector2(138f, 138f);
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
