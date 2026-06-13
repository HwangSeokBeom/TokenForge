using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TokenForge.Client.Domain;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public sealed class MacDesktopCompanionOverlayService : IDesktopCompanionOverlayService
    {
        private const string LogPrefix = "[DesktopCompanion]";
        private delegate void OverlayClickedCallback();
        private delegate void OverlayDragEndedCallback(float x, float y);
        private delegate void OverlayRepositoryDragEndedCallback(string repositoryId, float x, float y);
        private static readonly OverlayClickedCallback ClickedCallback = OnNativeOverlayClicked;
        private static readonly OverlayClickedCallback DoubleClickedCallback = OnNativeOverlayDoubleClicked;
        private static readonly OverlayDragEndedCallback DragEndedCallback = OnNativeOverlayDragEnded;
        private static readonly OverlayRepositoryDragEndedCallback RepositoryDragEndedCallback = OnNativeOverlayRepositoryDragEnded;
        private static event Action GlobalClicked;
        private static event Action GlobalDoubleClicked;
        private static event Action<Vector2> GlobalDragEnded;
        private static event Action<string, Vector2> GlobalRepositoryDragEnded;
        private bool createAttempted;
        private bool clickCallbackRegistered;
        private bool? lastClickThrough;
        private string lastMotionProfileSignature = string.Empty;
        private string lastLoggedMessage = string.Empty;

        public event Action Clicked;
        public event Action DoubleClicked;
        public event Action<Vector2> DragEnded;
        public event Action<string, Vector2> RepositoryDragEnded;

        public MacDesktopCompanionOverlayService()
        {
            GlobalClicked += RaiseClicked;
            GlobalDoubleClicked += RaiseDoubleClicked;
            GlobalDragEnded += RaiseDragEnded;
            GlobalRepositoryDragEnded += RaiseRepositoryDragEnded;
        }

        ~MacDesktopCompanionOverlayService()
        {
            GlobalClicked -= RaiseClicked;
            GlobalDoubleClicked -= RaiseDoubleClicked;
            GlobalDragEnded -= RaiseDragEnded;
            GlobalRepositoryDragEnded -= RaiseRepositoryDragEnded;
        }

        public bool IsAvailable
        {
            get
            {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public CompanionDesktopOverlayState State { get; private set; } = CompanionDesktopOverlayState.Unavailable;
        public bool IsNativeOverlay => IsAvailable && State != CompanionDesktopOverlayState.Unavailable;
        public bool IsAnyOverlayDragging()
        {
            if (!IsAvailable)
            {
                return false;
            }

            try { return NativeIsOverlayDragging(); }
            catch { return false; }
        }

        public bool IsAnyOverlayActuallyVisible()
        {
            if (!IsAvailable)
            {
                return false;
            }

            try { return NativeIsCompanionVisible(); }
            catch { return State == CompanionDesktopOverlayState.Active; }
        }
        public string StatusMessage { get; private set; } = "Native overlay has not been initialized.";

        public bool Create()
        {
            createAttempted = true;
            LogOnce("companion overlay enabled");
            if (!IsAvailable)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                StatusMessage = "Unsupported runtime. Native desktop overlay requires a macOS player build.";
                LogOnce("platform unsupported");
                return false;
            }

            if (DisableNativeOverlayEnabled)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                StatusMessage = "Native overlay disabled by startup safe-mode environment.";
                Debug.Log("INFO [NativeSafeMode][SKIP] function=MacDesktopCompanionOverlayService.Create reason=" + NativeOverlaySkipReason);
                return false;
            }

            if (State == CompanionDesktopOverlayState.Disabled || State == CompanionDesktopOverlayState.Active)
            {
                LogOnce("native overlay created");
                return true;
            }

            try
            {
                var created = NativeCreate();
                if (!created)
                {
                    State = CompanionDesktopOverlayState.Unavailable;
                    StatusMessage = "Native overlay creation returned false.";
                    LogOnce("overlay failed");
                    return false;
                }

                State = CompanionDesktopOverlayState.Disabled;
                StatusMessage = "Native desktop overlay is ready.";
                LogOnce("native dylib loaded path " + NativeLibraryPath());
                RegisterClickCallbackIfPossible();
                RegisterDoubleClickCallbackIfPossible();
                RegisterDragEndedCallbackIfPossible();
                RegisterRepositoryDragEndedCallbackIfPossible();
                SetClickEnabled(true);
                SetClickThrough(false);
                LogOnce("native overlay created");
                return true;
            }
            catch (Exception exception)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                StatusMessage = "Native overlay library missing or incompatible: " + exception.GetType().Name;
                LogOnce("native library missing");
                return false;
            }
        }

        public void Show()
        {
            if (!IsAvailable)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                return;
            }

            if (DisableNativeOverlayEnabled)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                Debug.Log("INFO [NativeSafeMode][SKIP] function=MacDesktopCompanionOverlayService.Show reason=" + NativeOverlaySkipReason);
                return;
            }

            try
            {
                if (!createAttempted || State == CompanionDesktopOverlayState.Unavailable)
                {
                    Create();
                }

                if (State == CompanionDesktopOverlayState.Unavailable)
                {
                    LogOnce("overlay failed");
                    return;
                }

                NativeSetCompanionVisibleWithSource(true, "csharp.overlayService.show");
                var nativeVisible = NativeIsCompanionVisible();
                State = nativeVisible ? CompanionDesktopOverlayState.Active : CompanionDesktopOverlayState.Disabled;
                StatusMessage = nativeVisible ? "Native desktop companion active." : "Native desktop companion show requested but not visible.";
                Debug.Log("INFO [Overlay][Action] repoHash=legacy desiredVisible=true actualVisible=" + nativeVisible + " panelExists=true panelFrame=nativeLegacyPanel reason=explicitShow sourceAction=csharp.overlayService.show action=show");
                Debug.Log("INFO [OverlayState][NATIVE_ACTUAL] desiredVisible=true actualVisible=" + nativeVisible + " source=csharp.overlayService.show");
                Debug.Log("INFO [Overlay][Actual] repoHash=legacy desiredVisible=true actualVisible=" + nativeVisible + " panelExists=true panelFrame=nativeLegacyPanel reason=explicitShow sourceAction=csharp.overlayService.show");
            }
            catch (Exception exception)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                StatusMessage = "Native overlay show failed: " + exception.GetType().Name;
                LogOnce("overlay failed");
            }
        }

        public void Hide()
        {
            if (!IsAvailable)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                return;
            }
            if (DisableNativeOverlayEnabled)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                Debug.Log("INFO [NativeSafeMode][SKIP] function=MacDesktopCompanionOverlayService.Hide reason=" + NativeOverlaySkipReason);
                return;
            }

            try
            {
                NativeSetCompanionVisibleWithSource(false, "csharp.overlayService.hide");
                State = CompanionDesktopOverlayState.Disabled;
                StatusMessage = "Native desktop companion hidden.";
                Debug.Log("INFO [Overlay][Action] repoHash=legacy desiredVisible=false actualVisible=false panelExists=true panelFrame=nativeLegacyPanel reason=explicitHide sourceAction=csharp.overlayService.hide action=hide");
                Debug.Log("INFO [OverlayState][NATIVE_ACTUAL] desiredVisible=false actualVisible=false source=csharp.overlayService.hide");
                Debug.Log("INFO [Overlay][Actual] repoHash=legacy desiredVisible=false actualVisible=false panelExists=true panelFrame=nativeLegacyPanel reason=explicitHide sourceAction=csharp.overlayService.hide");
            }
            catch (Exception exception)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                StatusMessage = "Native overlay hide failed: " + exception.GetType().Name;
            }
        }

        public void SetPosition(Vector2 position)
        {
            if (IsAvailable)
            {
                try { NativeSetPosition(position.x, position.y); } catch (Exception exception) { State = CompanionDesktopOverlayState.Unavailable; StatusMessage = "Native overlay position failed: " + exception.GetType().Name; }
            }
        }

        public void SetSize(Vector2 size)
        {
            if (IsAvailable)
            {
                try { NativeSetSize(Mathf.Max(24f, size.x), Mathf.Max(24f, size.y)); } catch (Exception exception) { State = CompanionDesktopOverlayState.Unavailable; StatusMessage = "Native overlay sizing failed: " + exception.GetType().Name; }
            }
        }

        public void SetVisualTheme(string visualThemeId)
        {
            if (IsAvailable)
            {
                try { NativeSetVisualTheme(CompanionSkinCatalog.Normalize(visualThemeId)); } catch (Exception exception) { StatusMessage = "Native overlay theme update failed: " + exception.GetType().Name; }
            }
        }

        public void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft)
        {
            if (IsAvailable)
            {
                try { NativeSetVisualState((int)stage, (int)archetype, (int)animationState, facingLeft); } catch (Exception exception) { State = CompanionDesktopOverlayState.Unavailable; StatusMessage = "Native overlay visual update failed: " + exception.GetType().Name; }
            }
        }

        public void SetRenderSnapshot(string repositoryId, CompanionState state, int xp, string visualThemeId)
        {
            if (!IsAvailable || DisablePixelNativeRendererEnabled)
            {
                if (DisablePixelNativeRendererEnabled)
                {
                    Debug.Log("INFO [NativeSafeMode][SKIP] function=MacDesktopCompanionOverlayService.SetRenderSnapshot reason=" + NativePixelSkipReason);
                }
                return;
            }

            try
            {
                state = CompanionProgressionRules.Normalize(state);
                NativeSetRenderSnapshot(
                    string.IsNullOrWhiteSpace(repositoryId) ? "unknown" : repositoryId.Trim(),
                    (int)state.Stage,
                    Math.Max(1, state.Level),
                    Math.Max(0, xp),
                    (int)state.Archetype,
                    CompanionSkinCatalog.Normalize(visualThemeId),
                    true);
            }
            catch (Exception exception)
            {
                StatusMessage = "Native overlay render snapshot failed: " + exception.GetType().Name;
            }
        }

        public void SetCompanionFarmSnapshots(DesktopCompanionFarmState farmState)
        {
            if (!IsAvailable || DisablePixelNativeRendererEnabled)
            {
                if (DisablePixelNativeRendererEnabled)
                {
                    Debug.Log("INFO [NativeSafeMode][SKIP] function=MacDesktopCompanionOverlayService.SetCompanionFarmSnapshots reason=" + NativePixelSkipReason);
                }
                return;
            }

            try
            {
                var envelope = new NativeCompanionFarmSnapshotEnvelope();
                var overlays = farmState?.overlays ?? new RepositoryCompanionOverlayState[0];
                foreach (var overlay in overlays)
                {
                    if (overlay?.hydratedSnapshot == null || string.IsNullOrWhiteSpace(overlay.repositoryId))
                    {
                        continue;
                    }

                    var snapshot = overlay.hydratedSnapshot;
                    snapshot.repositoryId = overlay.repositoryId;
                    snapshot.repositoryName = string.IsNullOrWhiteSpace(overlay.repositoryName) ? "Repository" : overlay.repositoryName;
                    snapshot.desiredVisible = farmState.enabled && overlay.desiredVisible;
                    snapshot.hasSavedPosition = overlay.hasSavedPosition;
                    snapshot.desiredInitialX = overlay.desiredPositionX;
                    snapshot.desiredInitialY = overlay.desiredPositionY;
                    envelope.overlays.Add(snapshot);
                    Debug.Log("INFO [FarmProjection][ITEM] repo=" + snapshot.repositoryId + " stage=" + ((CompanionStage)snapshot.stage) + " level=" + Math.Max(1, snapshot.level) + " source=profile");
                }

                NativeSetFarmSnapshots(JsonUtility.ToJson(envelope));
                Debug.Log("INFO [Overlay][Desired] repoHash=" + (envelope.overlays.Count == 0 ? "none" : string.Join(",", envelope.overlays.ConvertAll(item => item.repositoryId).ToArray())) +
                          " desiredVisible=" + (farmState != null && farmState.enabled) +
                          " actualVisible=" + (State == CompanionDesktopOverlayState.Active) +
                          " panelExists=" + (envelope.overlays.Count > 0) +
                          " panelFrame=farmSnapshot reason=farmSnapshot sourceAction=csharp.setFarmSnapshots snapshots=" + envelope.overlays.Count);
                if (envelope.overlays.Count == 0)
                {
                    NativeHideAllRepositoryCompanions("csharp.noConnectedRepositories");
                    NativeSetCompanionVisibleWithSource(false, "csharp.noConnectedRepositories");
                    Debug.Log("INFO [Overlay][Action] repoHash=none desiredVisible=false actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=csharp.noConnectedRepositories action=hide");
                    Debug.Log("INFO [OverlayFarm][HIDE_ALL] reason=noConnectedRepositories");
                    Debug.Log("INFO [FarmProjection][SKIP_PLACEHOLDER] reason=noRepository");
                }

                var nativeVisible = NativeIsCompanionVisible();
                State = nativeVisible
                    ? CompanionDesktopOverlayState.Active
                    : CompanionDesktopOverlayState.Disabled;
                StatusMessage = State == CompanionDesktopOverlayState.Active
                    ? "Native repository companion farm active."
                    : "Native repository companion farm hidden.";
                Debug.Log("INFO [FarmProjection][BUILD] connectedRepositories=" + envelope.overlays.Count + " snapshots=" + envelope.overlays.Count);
                Debug.Log("INFO [OverlayFarm][SNAPSHOT_APPLY] count=" + envelope.overlays.Count + " source=csharp");
                Debug.Log("INFO [OverlayFarm][VISIBLE_COUNT] count=" + (nativeVisible ? envelope.overlays.Count : 0));
                Debug.Log("INFO [Overlay][Actual] repoHash=" + (envelope.overlays.Count == 0 ? "none" : string.Join(",", envelope.overlays.ConvertAll(item => item.repositoryId).ToArray())) +
                          " desiredVisible=" + (farmState != null && farmState.enabled) +
                          " actualVisible=" + nativeVisible +
                          " panelExists=" + (envelope.overlays.Count > 0) +
                          " panelFrame=farmSnapshot reason=farmSnapshotApplied sourceAction=csharp.setFarmSnapshots");
                Debug.Log("INFO [OverlayVisibilityDiagnostic] selectedRepoHash=" + (envelope.overlays.Count == 0 ? "none" : string.Join(",", envelope.overlays.ConvertAll(item => item.repositoryId).ToArray())) +
                          " canonicalRepoHash=" + (envelope.overlays.Count == 0 ? "none" : string.Join(",", envelope.overlays.ConvertAll(item => item.repositoryId).ToArray())) +
                          " approvedRepoCount=" + envelope.overlays.Count +
                          " desiredVisible=" + (farmState != null && farmState.enabled) +
                          " actualVisible=" + nativeVisible +
                          " panelExists=" + (envelope.overlays.Count > 0) +
                          " forcedHiddenByNoRepo=" + (envelope.overlays.Count == 0 && farmState != null && farmState.enabled) +
                          " legacyPanelExists=" + nativeVisible +
                          " farmPanelCount=" + envelope.overlays.Count +
                          " persistedFarmSnapshotCount=" + envelope.overlays.Count +
                          " reason=farmSnapshotApplied");
            }
            catch (Exception exception)
            {
                StatusMessage = "Native overlay farm snapshot failed: " + exception.GetType().Name;
            }
        }

        public bool IsOverlayDragging(string repositoryId)
        {
            if (!IsAvailable)
            {
                return false;
            }

            try { return NativeIsOverlayDraggingForRepository(SafeRepositoryId(repositoryId)); }
            catch { return false; }
        }

        public void ShowAllRepositoryCompanions(string source = "csharp.showAll")
        {
            if (IsAvailable && !DisableNativeOverlayEnabled)
            {
                try { NativeShowAllRepositoryCompanions(source); } catch { }
            }
        }

        public void HideAllRepositoryCompanions(string source = "csharp.hideAll")
        {
            if (IsAvailable && !DisableNativeOverlayEnabled)
            {
                try { NativeHideAllRepositoryCompanions(source); } catch { }
            }
        }

        public void SetOverlayFrame(string repositoryId, Rect frame, string source = "csharp.setFrame")
        {
            if (IsAvailable && !DisableNativeOverlayEnabled)
            {
                try { NativeSetOverlayFrame(SafeRepositoryId(repositoryId), frame.x, frame.y, Mathf.Max(24f, frame.width), Mathf.Max(24f, frame.height), source); } catch { }
            }
        }

        private static string SafeRepositoryId(string repositoryId)
        {
            return string.IsNullOrWhiteSpace(repositoryId) ? "legacy" : repositoryId.Trim();
        }

        public void SetMotionProfile(CompanionVisualProfile profile)
        {
            if (!IsAvailable || profile == null || DisableNativeOverlayEnabled)
            {
                if (DisableNativeOverlayEnabled)
                {
                    Debug.Log("INFO [NativeSafeMode][SKIP] function=MacDesktopCompanionOverlayService.SetMotionProfile reason=" + NativeOverlaySkipReason);
                }
                return;
            }

            var motion = profile.MotionProfile ?? new CompanionMotionProfile();
            var reaction = profile.ReactionProfile ?? new CompanionReactionProfile();
            var signature = string.Join("|",
                ((int)motion.DefaultMode).ToString(),
                motion.IdleRadius.ToString("0.###"),
                motion.WanderRadius.ToString("0.###"),
                motion.WanderSpeed.ToString("0.###"),
                motion.DecisionIntervalSeconds.ToString("0.###"),
                motion.AllowsWandering ? "1" : "0",
                reaction.CooldownSeconds.ToString("0.###"));
            if (string.Equals(lastMotionProfileSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                NativeSetMotionProfile(
                    (int)motion.DefaultMode,
                    motion.IdleRadius,
                    motion.WanderRadius,
                    motion.WanderSpeed,
                    motion.DecisionIntervalSeconds,
                    motion.AllowsWandering,
                    reaction.CooldownSeconds);
                lastMotionProfileSignature = signature;
            }
            catch (Exception exception)
            {
                StatusMessage = "Native overlay motion profile failed: " + exception.GetType().Name;
            }
        }

        public void TriggerReaction(CompanionReaction reaction, string speechText)
        {
            if (IsAvailable && !DisableNativeOverlayEnabled)
            {
                try { NativeTriggerReaction((int)reaction, SafeSpeechText(speechText)); } catch (Exception exception) { StatusMessage = "Native overlay reaction failed: " + exception.GetType().Name; }
            }
        }

        public void ResetPosition()
        {
            if (IsAvailable && !DisableNativeOverlayEnabled)
            {
                try { NativeResetPosition(); Debug.Log("INFO [Overlay][Action] reset source=csharp.resetPosition"); } catch (Exception exception) { StatusMessage = "Native overlay reset failed: " + exception.GetType().Name; }
            }
        }

        public void SetClickEnabled(bool enabled)
        {
            if (IsAvailable && !DisableNativeOverlayEnabled)
            {
                try
                {
                    NativeSetClickEnabled(enabled);
                    LogOnce("click mode set");
                }
                catch (Exception exception)
                {
                    StatusMessage = "Native click callback support unavailable: " + exception.GetType().Name;
                    LogOnce("click mode set");
                }
            }
        }

        public void SetClickThrough(bool clickThrough)
        {
            if (IsAvailable && !DisableNativeOverlayEnabled)
            {
                try
                {
                    if (lastClickThrough.HasValue && lastClickThrough.Value == clickThrough)
                    {
                        return;
                    }

                    NativeSetClickThrough(clickThrough);
                    lastClickThrough = clickThrough;
                    Debug.Log("INFO [Overlay][Action] clickThrough enabled=" + clickThrough);
                    LogOnce(clickThrough ? "click-through enabled" : "click-through disabled");
                }
                catch (Exception exception)
                {
                    State = CompanionDesktopOverlayState.Unavailable;
                    StatusMessage = "Native click-through update failed: " + exception.GetType().Name;
                }
            }
        }

        public void Destroy()
        {
            if (IsAvailable)
            {
                try
                {
                    NativeDestroy();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("WARN " + LogPrefix + " destroy failed: " + exception.GetType().Name);
                }
            }

            State = IsAvailable ? CompanionDesktopOverlayState.Disabled : CompanionDesktopOverlayState.Unavailable;
            StatusMessage = IsAvailable ? "Native desktop companion destroyed." : "Unsupported runtime.";
            createAttempted = false;
            clickCallbackRegistered = false;
            lastClickThrough = null;
            lastMotionProfileSignature = string.Empty;
        }

        private void RaiseClicked()
        {
            Clicked?.Invoke();
        }

        private void RaiseDoubleClicked()
        {
            DoubleClicked?.Invoke();
        }

        private void RaiseDragEnded(Vector2 position)
        {
            DragEnded?.Invoke(position);
        }

        private void RaiseRepositoryDragEnded(string repositoryId, Vector2 position)
        {
            RepositoryDragEnded?.Invoke(repositoryId, position);
        }

        private static void OnNativeOverlayClicked()
        {
            Debug.Log("INFO " + LogPrefix + " single click reaction triggered");
            GlobalClicked?.Invoke();
        }

        private static void OnNativeOverlayDoubleClicked()
        {
            Debug.Log("INFO " + LogPrefix + " double click dashboard restore requested");
            GlobalDoubleClicked?.Invoke();
        }

        private static void OnNativeOverlayDragEnded(float x, float y)
        {
            Debug.Log("INFO " + LogPrefix + " drag ended with x/y " + x.ToString("0.##") + "," + y.ToString("0.##"));
            GlobalDragEnded?.Invoke(new Vector2(x, y));
        }

        private static void OnNativeOverlayRepositoryDragEnded(string repositoryId, float x, float y)
        {
            var safeRepo = SafeRepositoryId(repositoryId);
            Debug.Log("INFO " + LogPrefix + " drag ended repo=" + safeRepo + " x/y " + x.ToString("0.##") + "," + y.ToString("0.##"));
            GlobalRepositoryDragEnded?.Invoke(safeRepo, new Vector2(x, y));
        }

        private void RegisterClickCallbackIfPossible()
        {
            if (clickCallbackRegistered)
            {
                return;
            }

            try
            {
                NativeRegisterClickedCallback(ClickedCallback);
                clickCallbackRegistered = true;
            }
            catch (Exception exception)
            {
                StatusMessage = "Native click callback support unavailable: " + exception.GetType().Name + ". Native dashboard restore fallback remains enabled.";
            }
        }

        private void RegisterDoubleClickCallbackIfPossible()
        {
            try
            {
                NativeRegisterDoubleClickedCallback(DoubleClickedCallback);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " double click callback unavailable: " + exception.GetType().Name);
            }
        }

        private void RegisterDragEndedCallbackIfPossible()
        {
            try
            {
                NativeRegisterDragEndedCallback(DragEndedCallback);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " drag callback unavailable: " + exception.GetType().Name);
            }
        }

        private void RegisterRepositoryDragEndedCallbackIfPossible()
        {
            try
            {
                NativeRegisterRepositoryDragEndedCallback(RepositoryDragEndedCallback);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " repository drag callback unavailable: " + exception.GetType().Name);
            }
        }

        private static string SafeSpeechText(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "First safe summary will start growth." : value.Trim();
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= 90 ? value : value.Substring(0, 90);
        }

        private static string NativeLibraryPath()
        {
            try
            {
                var pointer = NativeGetLibraryPath();
                var path = Marshal.PtrToStringAnsi(pointer);
                return string.IsNullOrWhiteSpace(path) ? "DesktopCompanionOverlay via Unity plugin loader" : path;
            }
            catch (Exception exception)
            {
                return "unavailable:" + exception.GetType().Name;
            }
        }

        private void LogOnce(string message)
        {
            if (string.Equals(lastLoggedMessage, message, StringComparison.Ordinal))
            {
                return;
            }

            lastLoggedMessage = message;
            Debug.Log("INFO " + LogPrefix + " " + message);
        }

        private static bool NativeSafeModeEnabled => IsEnvironmentFlagEnabled("TOKENFORGE_NATIVE_SAFE_MODE");
        private static bool DisableNativeOverlayEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_NATIVE_OVERLAY");
        private static bool DisablePixelNativeRendererEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER");
        private static string NativeOverlaySkipReason => NativeSafeModeEnabled ? "TOKENFORGE_NATIVE_SAFE_MODE" : "TOKENFORGE_DISABLE_NATIVE_OVERLAY";
        private static string NativePixelSkipReason => NativeSafeModeEnabled ? "TOKENFORGE_NATIVE_SAFE_MODE" : "TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER";

        private static bool IsEnvironmentFlagEnabled(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("DesktopCompanionOverlay", EntryPoint = "CreateDesktopCompanionOverlay")]
        private static extern bool NativeCreate();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "ShowDesktopCompanionOverlay")]
        private static extern void NativeShow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "HideDesktopCompanionOverlay")]
        private static extern void NativeHide();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetCompanionVisibleWithSource")]
        private static extern void NativeSetCompanionVisibleWithSource([MarshalAs(UnmanagedType.I1)] bool visible, string source);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_IsCompanionVisible")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool NativeIsCompanionVisible();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_IsOverlayDragging")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool NativeIsOverlayDragging();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayPosition")]
        private static extern void NativeSetPosition(float x, float y);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlaySize")]
        private static extern void NativeSetSize(float width, float height);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayVisualState")]
        private static extern void NativeSetVisualState(int stage, int archetype, int animationState, bool facingLeft);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetCompanionRenderSnapshot")]
        private static extern void NativeSetRenderSnapshot(string repositoryId, int stage, int level, int xp, int archetype, string visualThemeId, [MarshalAs(UnmanagedType.I1)] bool hydrated);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetCompanionFarmSnapshots")]
        private static extern void NativeSetFarmSnapshots(string json);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_IsOverlayDraggingForRepository")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool NativeIsOverlayDraggingForRepository(string repositoryId);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_ShowAllRepositoryCompanions")]
        private static extern void NativeShowAllRepositoryCompanions(string source);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_HideAllRepositoryCompanions")]
        private static extern void NativeHideAllRepositoryCompanions(string source);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetOverlayFrame")]
        private static extern void NativeSetOverlayFrame(string repositoryId, float x, float y, float width, float height, string source);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayVisualTheme")]
        private static extern void NativeSetVisualTheme(string visualThemeId);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayMotionProfile")]
        private static extern void NativeSetMotionProfile(int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TriggerCompanionOverlayReaction")]
        private static extern void NativeTriggerReaction(int reaction, string speechText);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "ResetCompanionOverlayPosition")]
        private static extern void NativeResetPosition();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayClickThrough")]
        private static extern void NativeSetClickThrough(bool clickThrough);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetOverlayClickEnabled")]
        private static extern void NativeSetClickEnabled(bool enabled);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_RegisterOverlayClickedCallback")]
        private static extern void NativeRegisterClickedCallback(OverlayClickedCallback callback);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_RegisterOverlayDoubleClickedCallback")]
        private static extern void NativeRegisterDoubleClickedCallback(OverlayClickedCallback callback);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_RegisterOverlayDragEndedCallback")]
        private static extern void NativeRegisterDragEndedCallback(OverlayDragEndedCallback callback);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_RegisterOverlayDragEndedForRepositoryCallback")]
        private static extern void NativeRegisterRepositoryDragEndedCallback(OverlayRepositoryDragEndedCallback callback);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "DestroyDesktopCompanionOverlay")]
        private static extern void NativeDestroy();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_GetOverlayLibraryPath")]
        private static extern IntPtr NativeGetLibraryPath();
#else
        private static bool NativeCreate() { return false; }
        private static void NativeShow() { }
        private static void NativeHide() { }
        private static void NativeSetCompanionVisibleWithSource(bool visible, string source) { }
        private static bool NativeIsCompanionVisible() { return false; }
        private static bool NativeIsOverlayDragging() { return false; }
        private static void NativeSetPosition(float x, float y) { }
        private static void NativeSetSize(float width, float height) { }
        private static void NativeSetVisualState(int stage, int archetype, int animationState, bool facingLeft) { }
        private static void NativeSetRenderSnapshot(string repositoryId, int stage, int level, int xp, int archetype, string visualThemeId, bool hydrated) { }
        private static void NativeSetFarmSnapshots(string json) { }
        private static bool NativeIsOverlayDraggingForRepository(string repositoryId) { return false; }
        private static void NativeShowAllRepositoryCompanions(string source) { }
        private static void NativeHideAllRepositoryCompanions(string source) { }
        private static void NativeSetOverlayFrame(string repositoryId, float x, float y, float width, float height, string source) { }
        private static void NativeSetVisualTheme(string visualThemeId) { }
        private static void NativeSetMotionProfile(int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds) { }
        private static void NativeTriggerReaction(int reaction, string speechText) { }
        private static void NativeResetPosition() { }
        private static void NativeSetClickThrough(bool clickThrough) { }
        private static void NativeSetClickEnabled(bool enabled) { }
        private static void NativeRegisterClickedCallback(OverlayClickedCallback callback) { }
        private static void NativeRegisterDoubleClickedCallback(OverlayClickedCallback callback) { }
        private static void NativeRegisterDragEndedCallback(OverlayDragEndedCallback callback) { }
        private static void NativeRegisterRepositoryDragEndedCallback(OverlayRepositoryDragEndedCallback callback) { }
        private static void NativeDestroy() { }
        private static IntPtr NativeGetLibraryPath() { return IntPtr.Zero; }
#endif
    }
}
