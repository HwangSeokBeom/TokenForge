using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public sealed class MacNativeDashboardService : INativeDashboardService
    {
        private const string LogPrefix = "[NativeDashboard]";
        private static readonly NativeDashboardActionCallback DashboardActionCallback = OnNativeDashboardAction;
        private static event Action<NativeDashboardActionRequest> GlobalActionRequested;
        private bool installed;

        public event Action<NativeDashboardActionRequest> ActionRequested
        {
            add { GlobalActionRequested += value; }
            remove { GlobalActionRequested -= value; }
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

        public bool Install()
        {
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                RegisterDashboardActionCallback(DashboardActionCallback);
                installed = true;
                Debug.Log("INFO " + LogPrefix + " action callback registered");
                Debug.Log("INFO " + LogPrefix + " AppKit dashboard bridge installed");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " install failed: " + exception.GetType().Name);
                return false;
            }
        }

        public void ShowDashboardWindow()
        {
            InvokeNative(NativeShowDashboardWindow);
        }

        public void HideDashboardWindow()
        {
            InvokeNative(NativeHideDashboardWindow);
        }

        public void ToggleDashboardWindow()
        {
            InvokeNative(NativeToggleDashboardWindow);
        }

        public void ShowSettingsWindow()
        {
            InvokeNative(NativeShowSettingsWindow);
        }

        public void UpdateDashboardState(NativeDashboardState state)
        {
            if (!EnsureInstalled())
            {
                return;
            }

            try
            {
                NativeUpdateDashboardState((state ?? NativeDashboardState.CreateDefault()).ToJson());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " state update failed: " + exception.GetType().Name);
            }
        }

        public void SetMenuBarStatus(NativeDashboardState state)
        {
            if (!EnsureInstalled())
            {
                return;
            }

            try
            {
                NativeSetMenuBarStatus((state ?? NativeDashboardState.CreateDefault()).ToJson());
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " status update failed: " + exception.GetType().Name);
            }
        }

        public void SetCompanionVisible(bool visible)
        {
            if (!EnsureInstalled())
            {
                return;
            }

            try
            {
                NativeSetCompanionVisible(visible);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " companion visibility failed: " + exception.GetType().Name);
            }
        }

        private bool EnsureInstalled()
        {
            return installed || Install();
        }

        private void InvokeNative(Action nativeCall)
        {
            if (!EnsureInstalled())
            {
                return;
            }

            try
            {
                nativeCall();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " native call failed: " + exception.GetType().Name);
            }
        }

        private static void OnNativeDashboardAction(string action)
        {
            if (TryParseAction(action, out var parsed))
            {
                GlobalActionRequested?.Invoke(parsed);
                Debug.Log("INFO " + LogPrefix + " action=" + parsed.RawAction + " mapped=" + parsed.Action + (string.IsNullOrEmpty(parsed.Value) ? string.Empty : " value=" + parsed.Value));
            }
            else
            {
                Debug.LogWarning("WARN " + LogPrefix + " unknown action=" + (action ?? "<null>"));
            }
        }

        public static bool TryParseAction(string action, out NativeDashboardActionRequest parsed)
        {
            var rawAction = action ?? string.Empty;
            var value = string.Empty;
            var separator = rawAction.IndexOf(':');
            if (separator >= 0)
            {
                value = separator + 1 < rawAction.Length ? rawAction.Substring(separator + 1) : string.Empty;
                action = rawAction.Substring(0, separator);
            }

            switch (action)
            {
                case "dashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Dashboard, rawAction, value);
                    return true;
                case "toggleDashboard":
                case "toggle_dashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ToggleDashboard, rawAction, value);
                    return true;
                case "show_dashboard":
                case "showDashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ShowDashboard, rawAction, value);
                    return true;
                case "hide_dashboard":
                case "hideDashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.HideDashboard, rawAction, value);
                    return true;
                case "repository":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Repository, rawAction, value);
                    return true;
                case "connectRepository":
                case "changeRepository":
                case "connect_repository":
                case "change_repository":
                case "add_repository":
                    parsed = new NativeDashboardActionRequest(
                        action == "changeRepository" || action == "change_repository" ? NativeDashboardAction.ChangeRepository : NativeDashboardAction.ConnectRepository,
                        rawAction,
                        value);
                    return true;
                case "codexAgent":
                case "codex_agent":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.CodexAgent, rawAction, value);
                    return true;
                case "connectCodexAgent":
                case "connect_codex_agent":
                case "connect_ai_agent":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ConnectCodexAgent, rawAction, value);
                    return true;
                case "selectCodexLogFolder":
                case "select_codex_log_folder":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SelectCodexLogFolder, rawAction, value);
                    return true;
                case "activity":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Activity, rawAction, value);
                    return true;
                case "runAnalysis":
                case "run_analysis":
                case "refresh_activity":
                case "analyze_current_repository":
                case "sync_now":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.RunAnalysis, rawAction, value);
                    return true;
                case "reviewActivity":
                case "review_activity":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ReviewActivity, rawAction, value);
                    return true;
                case "approveReview":
                case "approve_review":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ApproveReview, rawAction, value);
                    return true;
                case "discardReview":
                case "discard_review":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DiscardReview, rawAction, value);
                    return true;
                case "settings":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Settings, rawAction, value);
                    return true;
                case "homepage":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Homepage, rawAction, value);
                    return true;
                case "report_issue":
                case "reportIssue":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ReportIssue, rawAction, value);
                    return true;
                case "reset_local_state":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ResetLocalState, rawAction, value);
                    return true;
                case "toggleCompanionVisible":
                case "toggle_companion_visible":
                case "enable_desktop_companion":
                case "disable_desktop_companion":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ToggleCompanionVisible, rawAction, value);
                    return true;
                case "changeCompanionSkin":
                case "change_companion_skin":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ChangeCompanionSkin, rawAction, value);
                    return true;
                case "setLaunchAtLogin":
                case "set_launch_at_login":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetLaunchAtLogin, rawAction, value);
                    return true;
                case "setWanderEnabled":
                case "set_wander_enabled":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetWanderEnabled, rawAction, value);
                    return true;
                case "setClickReactionEnabled":
                case "set_click_reaction_enabled":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetClickReactionEnabled, rawAction, value);
                    return true;
                case "resetCompanionPosition":
                case "reset_companion_position":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ResetCompanionPosition, rawAction, value);
                    return true;
                case "quit":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Quit, rawAction, value);
                    return true;
                default:
                    parsed = null;
                    return false;
            }
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeDashboardActionCallback(string action);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_ShowDashboardWindow")]
        private static extern void NativeShowDashboardWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_HideDashboardWindow")]
        private static extern void NativeHideDashboardWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_ToggleDashboardWindow")]
        private static extern void NativeToggleDashboardWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_ShowSettingsWindow")]
        private static extern void NativeShowSettingsWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_UpdateDashboardState")]
        private static extern void NativeUpdateDashboardState(string json);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetMenuBarStatus")]
        private static extern void NativeSetMenuBarStatus(string json);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetCompanionVisible")]
        private static extern void NativeSetCompanionVisible(bool visible);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_RegisterDashboardActionCallback")]
        private static extern void RegisterDashboardActionCallback(NativeDashboardActionCallback callback);
#else
        private delegate void NativeDashboardActionCallback(string action);
        private static void NativeShowDashboardWindow() { }
        private static void NativeHideDashboardWindow() { }
        private static void NativeToggleDashboardWindow() { }
        private static void NativeShowSettingsWindow() { }
        private static void NativeUpdateDashboardState(string json) { }
        private static void NativeSetMenuBarStatus(string json) { }
        private static void NativeSetCompanionVisible(bool visible) { }
        private static void RegisterDashboardActionCallback(NativeDashboardActionCallback callback) { }
#endif
    }
}
