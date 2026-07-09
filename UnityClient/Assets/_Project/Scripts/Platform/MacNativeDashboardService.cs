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

            if (NativeSafeModeEnabled || DisableNativeDashboardEnabled)
            {
                Debug.Log("INFO [NativeSafeMode][SKIP] function=MacNativeDashboardService.Install reason=" + NativeSkipReason);
                return false;
            }

            try
            {
                RegisterDashboardActionCallback(DashboardActionCallback);
                installed = true;
                Debug.Log("INFO " + LogPrefix + " action callback registered");
                Debug.Log("INFO " + LogPrefix + " AppKit dashboard bridge installed");
                Debug.Log("INFO [RuntimeIdentity] DllImportName=DesktopCompanionOverlay loadedDylibPath=" + NativeLibraryPath());
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " install failed: " + exception.GetType().Name);
                return false;
            }
        }

        public void ShowDashboardWindow(string source = "csharp.dashboard")
        {
            if (!EnsureInstalled())
            {
                return;
            }

            try
            {
                NativeShowDashboardWindowWithSource(SafeNativeSource(source, "csharp.dashboard"));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " native call failed: " + exception.GetType().Name);
            }
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

        public void SetCompanionVisible(bool visible, string source = "csharp")
        {
            if (!EnsureInstalled())
            {
                return;
            }

            try
            {
                var safeSource = SafeNativeSource(source, "csharp");
                Debug.Log("INFO [OverlayNative][CALL] TokenForge_SetCompanionVisible visible=" + visible + " source=" + safeSource);
                Debug.Log("INFO [OverlayTrace:csharp] DllImport TokenForge_SetCompanionVisible(" + visible + ") source=" + safeSource);
                NativeSetCompanionVisibleWithSource(visible, safeSource);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " companion visibility failed: " + exception.GetType().Name);
            }
        }

        private bool EnsureInstalled()
        {
            if (NativeSafeModeEnabled || DisableNativeDashboardEnabled)
            {
                Debug.Log("INFO [NativeSafeMode][SKIP] function=MacNativeDashboardService.EnsureInstalled reason=" + NativeSkipReason);
                return false;
            }

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

        private static void OnNativeDashboardAction(string action)
        {
            Debug.Log("INFO [NativeAction] received action=" + (action ?? "<null>"));
            if (TryParseAction(action, out var parsed))
            {
                GlobalActionRequested?.Invoke(parsed);
                if (parsed.Action == NativeDashboardAction.Unsupported)
                {
                    Debug.LogWarning("WARN [NativeAction] unknown action=" + parsed.RawAction);
                }
                else
                {
                    Debug.Log("INFO [NativeAction] routed action=" + parsed.RawAction + " handler=" + parsed.Action);
                    Debug.Log("INFO [OverlayTrace:" + parsed.TraceId + "] C# MacNativeDashboardService action received action=" + parsed.RawAction);
                    Debug.Log("INFO " + LogPrefix + " action=" + parsed.RawAction + " mapped=" + parsed.Action + (string.IsNullOrEmpty(parsed.Value) ? string.Empty : " value=" + parsed.Value));
                }
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
                case "navigation.openDashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Dashboard, rawAction, value);
                    return true;
                case "toggleDashboard":
                case "toggle_dashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ToggleDashboard, rawAction, value);
                    return true;
                case "show_dashboard":
                case "showDashboard":
                case "dashboard.open":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ShowDashboard, rawAction, value);
                    return true;
                case "hide_dashboard":
                case "hideDashboard":
                case "dashboard.close":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.HideDashboard, rawAction, value);
                    return true;
                case "repository":
                case "open_repositories":
                case "navigation.openRepositories":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Repository, rawAction, value);
                    return true;
                case "connectRepository":
                case "changeRepository":
                case "connect_repository":
                case "change_repository":
                case "add_repository":
                case "repository.add":
                case "repository.connect":
                    parsed = new NativeDashboardActionRequest(
                        action == "changeRepository" || action == "change_repository" ? NativeDashboardAction.ChangeRepository : NativeDashboardAction.ConnectRepository,
                        rawAction,
                        value);
                    return true;
                case "chooseRepositoryFolder":
                case "choose_repository_folder":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ChooseRepositoryFolder, rawAction, value);
                    return true;
                case "codexAgent":
                case "codex_agent":
                case "agents":
                case "aiAgents":
                case "ai_agents":
                case "open_agents":
                case "navigation.openAgents":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.CodexAgent, rawAction, value);
                    return true;
                case "manageAgents":
                case "manage_agents":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ManageAgents, rawAction, value);
                    return true;
                case "selectRepository":
                case "select_repository":
                case "repository.setActive":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SelectRepository, rawAction, value);
                    return true;
                case "repository.viewGrowth":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ViewRepositoryGrowth, rawAction, value);
                    return true;
                case "open_active_companion_dashboard":
                case "select_active_companion_dashboard":
                case "companion.openActiveDashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.OpenActiveCompanionDashboard, rawAction, value);
                    return true;
                case "open_repository_companion_dashboard":
                case "select_repository_companion":
                case "select_repository_companion_dashboard":
                case "companion.openRepositoryDashboard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.OpenRepositoryCompanionDashboard, rawAction, value);
                    return true;
                case "analyzeRepository":
                case "analyze_repository":
                case "repository.analyze":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.AnalyzeRepository, rawAction, value);
                    return true;
                case "disconnectRepository":
                case "disconnect_repository":
                case "repository.archive":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DisconnectRepository, rawAction, value);
                    return true;
                case "connectCodexAgent":
                case "connect_codex_agent":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ConnectCodexAgent, rawAction, value);
                    return true;
                case "connectAiAgent":
                case "connect_ai_agent":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ConnectAiAgent, rawAction, value);
                    return true;
                case "connectAgent":
                case "connect_agent":
                case "agent.connect":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ConnectAgent, rawAction, value);
                    return true;
                case "selectCodexLogFolder":
                case "select_codex_log_folder":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SelectCodexLogFolder, rawAction, value);
                    return true;
                case "chooseAgentFolder":
                case "choose_agent_folder":
                case "chooseManualFolder":
                case "choose_manual_folder":
                case "agent.chooseFolder":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ChooseAgentFolder, rawAction, value);
                    return true;
                case "autoDetectAgent":
                case "auto_detect_agent":
                case "agent.autoDetect":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.AutoDetectAgent, rawAction, value);
                    return true;
                case "detectAgent":
                case "detect_agent":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DetectAgent, rawAction, value);
                    return true;
                case "analyzeAgent":
                case "analyze_agent":
                case "agent.analyze":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.AnalyzeAgent, rawAction, value);
                    return true;
                case "analyzeAllAgents":
                case "analyze_all_agents":
                case "agent.analyzeAll":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.AnalyzeAllAgents, rawAction, value);
                    return true;
                case "disconnectAgent":
                case "disconnect_agent":
                case "agent.disconnect":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DisconnectAgent, rawAction, value);
                    return true;
                case "activity":
                case "navigation.openActivity":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Activity, rawAction, value);
                    return true;
                case "runAnalysis":
                case "run_analysis":
                case "refresh_activity":
                case "analyze_current_repository":
                case "sync_now":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.RunAnalysis, rawAction, value);
                    return true;
                case "runRepositoryAnalysis":
                case "run_repository_analysis":
                case "repository.runAnalysis":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.RunRepositoryAnalysis, rawAction, value);
                    return true;
                case "runAgentAnalysis":
                case "run_agent_analysis":
                case "agent.runAnalysis":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.RunAgentAnalysis, rawAction, value);
                    return true;
                case "safeSync":
                case "safe_sync":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SafeSync, rawAction, value);
                    return true;
                case "saveGrowth":
                case "save_growth":
                case "review.saveGrowth":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SaveGrowth, rawAction, value);
                    return true;
                case "viewReviewDetails":
                case "view_review_details":
                case "review.viewDetails":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ViewReviewDetails, rawAction, value);
                    return true;
                case "reviewActivity":
                case "review_activity":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ReviewActivity, rawAction, value);
                    return true;
                case "tokenShop":
                case "token_shop":
                case "shop.open":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.TokenShop, rawAction, value);
                    return true;
                case "wardrobe":
                case "wardrobe.open":
                case "dress_up":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Wardrobe, rawAction, value);
                    return true;
                case "onboarding":
                case "onboarding.open":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Onboarding, rawAction, value);
                    return true;
                case "onboarding.step":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetOnboardingStep, rawAction, value);
                    return true;
                case "onboarding.skip":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SkipOnboarding, rawAction, value);
                    return true;
                case "onboarding.finish":
                case "onboarding.done":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.CompleteOnboarding, rawAction, value);
                    return true;
                case "onboarding.reset":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ResetOnboarding, rawAction, value);
                    return true;
                case "purchaseTokenShopItem":
                case "purchase_token_shop_item":
                case "shop.purchase":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.PurchaseTokenShopItem, rawAction, value);
                    return true;
                case "shop.target":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SelectShopRepositoryTarget, rawAction, value);
                    return true;
                case "shop.mode":
                    parsed = new NativeDashboardActionRequest(
                        string.Equals(value, "repository", StringComparison.OrdinalIgnoreCase) ? NativeDashboardAction.SelectShopRepositoryTarget : NativeDashboardAction.SelectShopAgentTarget,
                        rawAction,
                        value);
                    return true;
                case "shop.target.agent":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SelectShopAgentTarget, rawAction, value);
                    return true;
                case "shop.category":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SelectShopCategory, rawAction, value);
                    return true;
                case "shop.equip":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.EquipTokenShopItem, rawAction, value);
                    return true;
                case "unequip_token_shop_item":
                case "shop.unequip":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.UnequipTokenShopItem, rawAction, value);
                    return true;
                case "shop.preview":
                case "shop.zodiac":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.PreviewTokenShopItem, rawAction, value);
                    return true;
                case "shop.openAgentConnect":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.OpenAgentConnect, rawAction, value);
                    return true;
                case "levelUp":
                case "level_up":
                case "evolveToken":
                case "evolve_token":
                case "companion.levelUp":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.LevelUpCompanion, rawAction, value);
                    return true;
                case "approveReview":
                case "approve_review":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ApproveReview, rawAction, value);
                    return true;
                case "saveReview":
                case "save_review":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SaveReview, rawAction, value);
                    return true;
                case "discardReview":
                case "discard_review":
                case "review.discard":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DiscardReview, rawAction, value);
                    return true;
                case "settings":
                case "openSettings":
                case "open_settings":
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
                case "debug.resetProviderUsage":
                case "debug_reset_provider_usage":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ResetProviderAggregates, rawAction, value);
                    return true;
                case "toggleCompanionVisible":
                case "toggle_companion_visible":
                case "enable_desktop_companion":
                case "disable_desktop_companion":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ToggleCompanionVisible, rawAction, value);
                    return true;
                case "showOnDesktop":
                case "show_companion":
                case "desktop.show":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ShowCompanion, rawAction, "true", OverlayTraceId(value));
                    return true;
                case "hideFromDesktop":
                case "hide_companion":
                case "desktop.hide":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.HideCompanion, rawAction, "false", OverlayTraceId(value));
                    return true;
                case "changeCompanionSkin":
                case "change_companion_skin":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ChangeCompanionSkin, rawAction, value);
                    return true;
                case "settings.zodiac":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SelectRepositoryZodiacMascot, rawAction, value);
                    return true;
                case "setLaunchAtLogin":
                case "set_launch_at_login":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetLaunchAtLogin, rawAction, value);
                    return true;
                case "setWanderEnabled":
                case "set_wander_enabled":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetWanderEnabled, rawAction, value);
                    return true;
                case "enable_wander":
                case "desktop.movement.enable":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.EnableWander, rawAction, "true", OverlayTraceId(value));
                    return true;
                case "disable_wander":
                case "desktop.movement.pause":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DisableWander, rawAction, "false", OverlayTraceId(value));
                    return true;
                case "setClickReactionEnabled":
                case "set_click_reaction_enabled":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetClickReactionEnabled, rawAction, value);
                    return true;
                case "set_companion_click_through_enabled":
                case "setClickThroughEnabled":
                case "set_click_through_enabled":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.SetClickThroughEnabled, rawAction, value);
                    return true;
                case "desktop.drag.enable":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.EnableDrag, rawAction, "true");
                    return true;
                case "desktop.clickThrough.disable":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DisableClickThrough, rawAction, "false");
                    return true;
                case "desktop.clickThrough.enable":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.EnableClickThrough, rawAction, "true");
                    return true;
                case "enable_click":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.EnableClick, rawAction, "true");
                    return true;
                case "disable_click":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.DisableClick, rawAction, "false");
                    return true;
                case "resetCompanionPosition":
                case "reset_companion_position":
                case "desktop.position.reset":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.ResetCompanionPosition, rawAction, value);
                    return true;
                case "quit":
                case "app.quit":
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Quit, rawAction, value);
                    return true;
                default:
                    parsed = new NativeDashboardActionRequest(NativeDashboardAction.Unsupported, rawAction, string.Empty);
                    return true;
            }
        }

        private static string OverlayTraceId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();
        }

        private static string SafeNativeSource(string value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= 96 ? value : value.Substring(0, 96);
        }

        private static bool NativeSafeModeEnabled => IsEnvironmentFlagEnabled("TOKENFORGE_NATIVE_SAFE_MODE");
        private static bool DisableNativeDashboardEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_NATIVE_DASHBOARD");
        private static string NativeSkipReason => NativeSafeModeEnabled ? "TOKENFORGE_NATIVE_SAFE_MODE" : "TOKENFORGE_DISABLE_NATIVE_DASHBOARD";

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
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeDashboardActionCallback(string action);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_ShowDashboardWindow")]
        private static extern void NativeShowDashboardWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_ShowDashboardWindowWithSource")]
        private static extern void NativeShowDashboardWindowWithSource(string source);

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
        private static extern void NativeSetCompanionVisible([MarshalAs(UnmanagedType.I1)] bool visible);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_SetCompanionVisibleWithSource")]
        private static extern void NativeSetCompanionVisibleWithSource([MarshalAs(UnmanagedType.I1)] bool visible, string source);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_GetOverlayLibraryPath")]
        private static extern IntPtr NativeGetLibraryPath();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_RegisterDashboardActionCallback")]
        private static extern void RegisterDashboardActionCallback(NativeDashboardActionCallback callback);
#else
        private delegate void NativeDashboardActionCallback(string action);
        private static void NativeShowDashboardWindow() { }
        private static void NativeShowDashboardWindowWithSource(string source) { }
        private static void NativeHideDashboardWindow() { }
        private static void NativeToggleDashboardWindow() { }
        private static void NativeShowSettingsWindow() { }
        private static void NativeUpdateDashboardState(string json) { }
        private static void NativeSetMenuBarStatus(string json) { }
        private static void NativeSetCompanionVisible(bool visible) { }
        private static void NativeSetCompanionVisibleWithSource(bool visible, string source) { }
        private static IntPtr NativeGetLibraryPath() { return IntPtr.Zero; }
        private static void RegisterDashboardActionCallback(NativeDashboardActionCallback callback) { }
#endif
    }
}
