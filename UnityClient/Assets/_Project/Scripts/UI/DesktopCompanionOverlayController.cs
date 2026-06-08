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
        private bool overlayCreateAttempted;

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
            overlayCreateAttempted = false;
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
            if (overlayService.State == CompanionDesktopOverlayState.Unavailable)
            {
                lastFailureReason = overlayService.StatusMessage;
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
                Debug.Log("INFO [Overlay][Guard] repoHash=none desiredVisible=" + desiredVisible + " actualVisible=false panelExists=false panelFrame=none reason=serviceUnavailable sourceAction=csharp.applyFarmSettings");
                return;
            }

            var settings = globalSettings ?? DesktopCompanionSettings.CreateDefault();
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
                        Debug.Log("INFO [Overlay][Guard] repoHash=" + item.RepositoryHash +
                                  " desiredVisible=" + (desiredVisible && item.DesktopCompanionEnabled) +
                                  " actualVisible=" + (overlayService.State == CompanionDesktopOverlayState.Active) +
                                  " panelExists=true panelFrame=farmSnapshot reason=dragInProgress sourceAction=csharp.applyFarmSettings");
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
                BlockOverlayWithoutApprovedRepository(desiredVisible, "csharp.noConnectedRepositories", "noApprovedRepository", settings);
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

            if ((!overlayCreateAttempted || overlayService.State == CompanionDesktopOverlayState.Unavailable) && !CreateOverlayOrFallback("csharp.applyFarmSettings"))
            {
                return;
            }

            overlayService.SetClickEnabled(!settings.IsClickThroughEnabled);
            overlayService.SetClickThrough(settings.IsClickThroughEnabled);
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
            if (!overlayService.IsAnyOverlayDragging())
            {
                if (farm.enabled && farm.visibleCount > 0)
                {
                    overlayService.ShowAllRepositoryCompanions("csharp.applyFarmSettings");
                }
                else
                {
                    overlayService.HideAllRepositoryCompanions("csharp.applyFarmSettings.disabled");
                }
            }

            Debug.Log("INFO [Overlay][Actual] repoHash=" + string.Join(",", overlays.Select(item => item.repositoryId).ToArray()) +
                      " desiredVisible=" + desiredVisible +
                      " actualVisible=" + (farm.enabled && farm.visibleCount > 0) +
                      " panelExists=true panelFrame=farmSnapshot reason=farmProjectionApplied sourceAction=csharp.applyFarmSettings");
        }

        public void HideLegacyOverlay(string source = "csharp.hideLegacy")
        {
            if (overlayService == null || !overlayService.IsAvailable)
            {
                return;
            }

            BlockOverlayWithoutApprovedRepository(false, source, "noApprovedRepository", settings);
            overlayService.Hide();
            Debug.Log("INFO [OverlayFarm][HIDE_ALL] reason=noConnectedRepositories source=" + source);
        }

        public void ApplySettings(DesktopCompanionSettings desktopSettings, CompanionState state, CompanionMotionState motion = null, string repositoryId = "", int currentXp = 0)
        {
            settings = desktopSettings ?? DesktopCompanionSettings.CreateDefault();
            var safeRepositoryId = string.IsNullOrWhiteSpace(repositoryId) ? string.Empty : repositoryId.Trim();
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

            if (string.IsNullOrWhiteSpace(safeRepositoryId) ||
                string.Equals(safeRepositoryId, RepositoryCompanionProfileService.DefaultLocalRepositoryHash, StringComparison.Ordinal))
            {
                overlayEnabledLogged = false;
                BlockOverlayWithoutApprovedRepository(settings.IsDesktopCompanionEnabled, "csharp.applySettings", "noApprovedRepository", settings);
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

            if ((!overlayCreateAttempted || overlayService.State == CompanionDesktopOverlayState.Unavailable) && !CreateOverlayOrFallback("csharp.applySettings"))
            {
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
                macOverlay.SetRenderSnapshot(safeRepositoryId, companionState, currentXp, settings.VisualThemeId);
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

        private bool CreateOverlayOrFallback(string sourceAction)
        {
            if (overlayService == null)
            {
                return false;
            }

            overlayCreateAttempted = true;
            if (overlayService.Create())
            {
                return true;
            }

            lastFailureReason = overlayService.StatusMessage;
            if (overlayService.State != CompanionDesktopOverlayState.Unavailable)
            {
                return false;
            }

            overlayService.Clicked -= OnDesktopCompanionClicked;
            overlayService.DoubleClicked -= OnDesktopCompanionDoubleClicked;
            overlayService.DragEnded -= OnDesktopCompanionDragEnded;
            if (overlayService is MacDesktopCompanionOverlayService unavailableMacService)
            {
                unavailableMacService.RepositoryDragEnded -= OnRepositoryCompanionDragEnded;
            }

            Debug.Log("INFO " + LogPrefix + " fallback companion active sourceAction=" + sourceAction);
            overlayService = new InAppCompanionOverlayFallbackService();
            overlayCreateAttempted = true;
            overlayService.Create();
            overlayService.Clicked += OnDesktopCompanionClicked;
            overlayService.DoubleClicked += OnDesktopCompanionDoubleClicked;
            overlayService.DragEnded += OnDesktopCompanionDragEnded;
            movementController = new CompanionDesktopMovementController(overlayService);
            return true;
        }

        private void BlockOverlayWithoutApprovedRepository(bool desiredVisible, string sourceAction, string reason, DesktopCompanionSettings currentSettings)
        {
            var actualVisible = overlayService != null && overlayService.State == CompanionDesktopOverlayState.Active;
            var panelExists = overlayService != null && overlayService.State != CompanionDesktopOverlayState.Unavailable;
            var panelFrame = currentSettings != null && currentSettings.HasSavedOverlayPosition
                ? currentSettings.LastOverlayPositionX.ToString("0.#") + "," + currentSettings.LastOverlayPositionY.ToString("0.#")
                : "none";
            Debug.Log("INFO [Overlay][Guard] repoHash=none desiredVisible=" + desiredVisible +
                      " actualVisible=" + actualVisible +
                      " panelExists=" + panelExists +
                      " panelFrame=" + panelFrame +
                      " reason=" + reason +
                      " sourceAction=" + sourceAction +
                      " selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
            Debug.Log("INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY] repoHash=none desiredVisible=false actualVisible=false panelExists=" + panelExists +
                      " panelFrame=" + panelFrame +
                      " reason=" + reason +
                      " sourceAction=" + sourceAction +
                      " selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
            if (overlayService == null || !overlayService.IsAvailable)
            {
                return;
            }

            overlayService.HideAllRepositoryCompanions(sourceAction);
            overlayService.Hide();
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
