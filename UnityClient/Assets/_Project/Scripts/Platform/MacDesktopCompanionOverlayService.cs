using System;
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
        private static readonly OverlayClickedCallback ClickedCallback = OnNativeOverlayClicked;
        private static readonly OverlayClickedCallback DoubleClickedCallback = OnNativeOverlayDoubleClicked;
        private static readonly OverlayDragEndedCallback DragEndedCallback = OnNativeOverlayDragEnded;
        private static event Action GlobalClicked;
        private static event Action GlobalDoubleClicked;
        private static event Action<Vector2> GlobalDragEnded;
        private bool createAttempted;
        private bool clickCallbackRegistered;
        private bool? lastClickThrough;
        private string lastLoggedMessage = string.Empty;

        public event Action Clicked;
        public event Action DoubleClicked;
        public event Action<Vector2> DragEnded;

        public MacDesktopCompanionOverlayService()
        {
            GlobalClicked += RaiseClicked;
            GlobalDoubleClicked += RaiseDoubleClicked;
            GlobalDragEnded += RaiseDragEnded;
        }

        ~MacDesktopCompanionOverlayService()
        {
            GlobalClicked -= RaiseClicked;
            GlobalDoubleClicked -= RaiseDoubleClicked;
            GlobalDragEnded -= RaiseDragEnded;
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
        public bool IsDragging => false;
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

                NativeShow();
                State = CompanionDesktopOverlayState.Active;
                StatusMessage = "Native desktop companion active.";
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

            try
            {
                NativeHide();
                State = CompanionDesktopOverlayState.Disabled;
                StatusMessage = "Native desktop companion hidden.";
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

        public void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft)
        {
            if (IsAvailable)
            {
                try { NativeSetVisualState((int)stage, (int)archetype, (int)animationState, facingLeft); } catch (Exception exception) { State = CompanionDesktopOverlayState.Unavailable; StatusMessage = "Native overlay visual update failed: " + exception.GetType().Name; }
            }
        }

        public void SetMotionProfile(CompanionVisualProfile profile)
        {
            if (!IsAvailable || profile == null)
            {
                return;
            }

            var motion = profile.MotionProfile ?? new CompanionMotionProfile();
            var reaction = profile.ReactionProfile ?? new CompanionReactionProfile();
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
            }
            catch (Exception exception)
            {
                StatusMessage = "Native overlay motion profile failed: " + exception.GetType().Name;
            }
        }

        public void TriggerReaction(CompanionReaction reaction, string speechText)
        {
            if (IsAvailable)
            {
                try { NativeTriggerReaction((int)reaction, SafeSpeechText(speechText)); } catch (Exception exception) { StatusMessage = "Native overlay reaction failed: " + exception.GetType().Name; }
            }
        }

        public void ResetPosition()
        {
            if (IsAvailable)
            {
                try { NativeResetPosition(); } catch (Exception exception) { StatusMessage = "Native overlay reset failed: " + exception.GetType().Name; }
            }
        }

        public void SetClickEnabled(bool enabled)
        {
            if (IsAvailable)
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
            if (IsAvailable)
            {
                try
                {
                    if (lastClickThrough.HasValue && lastClickThrough.Value == clickThrough)
                    {
                        return;
                    }

                    NativeSetClickThrough(clickThrough);
                    lastClickThrough = clickThrough;
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
                try { NativeDestroy(); } catch (Exception) { }
            }

            State = IsAvailable ? CompanionDesktopOverlayState.Disabled : CompanionDesktopOverlayState.Unavailable;
            StatusMessage = IsAvailable ? "Native desktop companion destroyed." : "Unsupported runtime.";
            createAttempted = false;
            clickCallbackRegistered = false;
            lastClickThrough = null;
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
            catch (Exception)
            {
            }
        }

        private void RegisterDragEndedCallbackIfPossible()
        {
            try
            {
                NativeRegisterDragEndedCallback(DragEndedCallback);
            }
            catch (Exception)
            {
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

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [DllImport("DesktopCompanionOverlay", EntryPoint = "CreateDesktopCompanionOverlay")]
        private static extern bool NativeCreate();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "ShowDesktopCompanionOverlay")]
        private static extern void NativeShow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "HideDesktopCompanionOverlay")]
        private static extern void NativeHide();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayPosition")]
        private static extern void NativeSetPosition(float x, float y);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlaySize")]
        private static extern void NativeSetSize(float width, float height);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayVisualState")]
        private static extern void NativeSetVisualState(int stage, int archetype, int animationState, bool facingLeft);

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

        [DllImport("DesktopCompanionOverlay", EntryPoint = "DestroyDesktopCompanionOverlay")]
        private static extern void NativeDestroy();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_GetOverlayLibraryPath")]
        private static extern IntPtr NativeGetLibraryPath();
#else
        private static bool NativeCreate() { return false; }
        private static void NativeShow() { }
        private static void NativeHide() { }
        private static void NativeSetPosition(float x, float y) { }
        private static void NativeSetSize(float width, float height) { }
        private static void NativeSetVisualState(int stage, int archetype, int animationState, bool facingLeft) { }
        private static void NativeSetMotionProfile(int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds) { }
        private static void NativeTriggerReaction(int reaction, string speechText) { }
        private static void NativeResetPosition() { }
        private static void NativeSetClickThrough(bool clickThrough) { }
        private static void NativeSetClickEnabled(bool enabled) { }
        private static void NativeRegisterClickedCallback(OverlayClickedCallback callback) { }
        private static void NativeRegisterDoubleClickedCallback(OverlayClickedCallback callback) { }
        private static void NativeRegisterDragEndedCallback(OverlayDragEndedCallback callback) { }
        private static void NativeDestroy() { }
        private static IntPtr NativeGetLibraryPath() { return IntPtr.Zero; }
#endif
    }
}
