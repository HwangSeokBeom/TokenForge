using System;
using System.Runtime.InteropServices;
using TokenForge.Client.Domain;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public enum ApplicationMenuAction
    {
        ShowDashboard,
        HideDashboard,
        EnableDesktopCompanion,
        DisableDesktopCompanion,
        ToggleCompanionClickThrough,
        ResetCompanionPosition,
        AddRepository,
        ConnectAiAgent,
        AnalyzeCurrentRepository,
        SyncNow,
        Settings,
        Quit
    }

    public interface IApplicationLifecycleService
    {
        event Action<ApplicationMenuAction> MenuActionRequested;
        bool IsAvailable { get; }
        bool Install();
        void UpdateStatusItem(string companionName, CompanionStage stage, CompanionArchetype archetype, int level, string repositoryAlias, string agentStatus, string syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync);
        void ShowMainWindow();
        void HideMainWindow();
        bool IsMainWindowVisible();
        void Quit();
    }

    public sealed class MacApplicationLifecycleService : IApplicationLifecycleService
    {
        private const string LogPrefix = "[DesktopCompanion]";
        private static bool statusItemInstallLogged;
        private static readonly NativeMenuActionCallback MenuActionCallback = OnNativeMenuAction;
        private static event Action<ApplicationMenuAction> GlobalMenuActionRequested;

        public event Action<ApplicationMenuAction> MenuActionRequested
        {
            add { GlobalMenuActionRequested += value; }
            remove { GlobalMenuActionRequested -= value; }
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallBeforeSceneLoad()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            if (NativeSafeModeEnabled || DisableStatusItemEnabled)
            {
                Debug.Log("INFO [StartupDiagnostic][STATUS_ITEM_INIT_OK] skipped=true source=RuntimeInitializeOnLoadMethod reason=" + NativeSkipReason);
                return;
            }

            InstallNativeLifecycle();
#endif
        }

        public bool Install()
        {
            if (!IsAvailable)
            {
                return false;
            }

            if (NativeSafeModeEnabled || DisableStatusItemEnabled)
            {
                Debug.Log("INFO [NativeSafeMode][SKIP] function=MacApplicationLifecycleService.Install reason=" + NativeSkipReason);
                return false;
            }

            try
            {
                var installed = InstallNativeLifecycle();
                if (installed && !statusItemInstallLogged)
                {
                    statusItemInstallLogged = true;
                    Debug.Log("INFO " + LogPrefix + " status item installed");
                }

                RegisterNativeMenuActionCallback(MenuActionCallback);

                return installed;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " install failed: " + exception.GetType().Name);
                return false;
            }
        }

        public void UpdateStatusItem(string companionName, CompanionStage stage, CompanionArchetype archetype, int level, string repositoryAlias, string agentStatus, string syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync)
        {
            if (!IsAvailable)
            {
                return;
            }

            if (NativeSafeModeEnabled || DisableStatusItemEnabled)
            {
                Debug.Log("INFO [NativeSafeMode][SKIP] function=MacApplicationLifecycleService.UpdateStatusItem reason=" + NativeSkipReason);
                return;
            }

            try
            {
                NativeUpdateStatusItem(
                    SafeMenuText(companionName, "Token"),
                    SafeMenuText(stage.ToString(), "Egg"),
                    (int)stage,
                    (int)archetype,
                    Math.Max(1, level),
                    SafeMenuText(repositoryAlias, "Not selected"),
                    SafeMenuText(agentStatus, "No agent connected"),
                    SafeMenuText(syncStatus, "Local only"),
                    companionEnabled,
                    clickThrough,
                    canAnalyze,
                    canSync);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " status update failed: " + exception.GetType().Name);
            }
        }

        public void ShowMainWindow()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                NativeShowMainWindow();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " show main window failed: " + exception.GetType().Name);
            }
        }

        public void HideMainWindow()
        {
            if (!IsAvailable)
            {
                return;
            }

            try
            {
                NativeHideMainWindow();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " hide main window failed: " + exception.GetType().Name);
            }
        }

        public bool IsMainWindowVisible()
        {
            if (!IsAvailable)
            {
                return false;
            }

            try
            {
                return NativeIsMainWindowVisible();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " visibility check failed: " + exception.GetType().Name);
                return false;
            }
        }

        public void Quit()
        {
            if (!IsAvailable)
            {
                Debug.Log("INFO [AppLifecycle][QUIT_REQUESTED] source=managed_fallback");
                LogQuitDiagnostic("managed_fallback", "Application.Quit", allowQuit: true, blocked: false, reason: "managedFallback");
                Application.Quit();
                return;
            }

            try
            {
                Debug.Log("INFO [AppLifecycle][QUIT_REQUESTED] source=nativeBridge traceId=managed-native");
                LogQuitDiagnostic("nativeBridge", "QuitTokenForgeApp", allowQuit: true, blocked: false, reason: "explicitNativeBridge");
                NativeQuit();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("WARN " + LogPrefix + " native quit failed: " + exception.GetType().Name);
                Debug.Log("INFO [AppLifecycle][QUIT_REQUESTED] source=managed_native_failure_fallback");
                LogQuitDiagnostic("managed_native_failure_fallback", "Application.Quit", allowQuit: true, blocked: false, reason: "nativeBridgeFailureFallback");
                Application.Quit();
            }
        }

        private static void LogQuitDiagnostic(string request, string source, bool allowQuit, bool blocked, string reason)
        {
            Debug.Log("INFO [QuitDiagnostic][REQUEST] request=" + request + " source=" + source + " reason=" + reason + " thread=managed");
            Debug.Log("INFO [QuitDiagnostic][SOURCE] source=" + source + " explicitFlag=true terminating=unknown lastExplicitSource=managed lastProjectionSource=managed");
            Debug.Log("INFO [QuitDiagnostic][STACK] " + Environment.StackTrace.Replace("\r", " ").Replace("\n", " | "));
            Debug.Log("INFO [QuitDiagnostic][VERIFICATION_MODE] enabled=" + (IsRuntimeVerificationMode ? "true" : "false") + " source=managed");
            Debug.Log("INFO [QuitDiagnostic][ALLOW_QUIT] value=" + (allowQuit ? "true" : "false") + " reason=" + reason);
            Debug.Log((blocked ? "WARN " : "INFO ") + "[QuitDiagnostic][" + (blocked ? "BLOCKED" : "PROCEED") + "] request=" + request + " source=" + source + " reason=" + reason);
        }

        private static bool IsRuntimeVerificationMode
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("TOKENFORGE_VERIFY_RUNTIME");
                if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(env, "true", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(env, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var args = Environment.GetCommandLineArgs();
                for (var index = 0; index < args.Length; index++)
                {
                    if (string.Equals(args[index], "-TokenForgeVerifyRuntime", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeMenuActionCallback(string action);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "InstallTokenForgeMacAppLifecycle")]
        private static extern bool InstallNativeLifecycle();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "ShowTokenForgeMainWindow")]
        private static extern void NativeShowMainWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_UpdateStatusItem")]
        private static extern void NativeUpdateStatusItem(string companionName, string stage, int stageIndex, int archetypeIndex, int level, string repositoryAlias, string agentStatus, string syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "TokenForge_RegisterMenuActionCallback")]
        private static extern void RegisterNativeMenuActionCallback(NativeMenuActionCallback callback);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "HideTokenForgeMainWindow")]
        private static extern void NativeHideMainWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "IsTokenForgeMainWindowVisible")]
        private static extern bool NativeIsMainWindowVisible();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "QuitTokenForgeApp")]
        private static extern void NativeQuit();
#else
        private delegate void NativeMenuActionCallback(string action);
        private static bool InstallNativeLifecycle() { return false; }
        private static void NativeUpdateStatusItem(string companionName, string stage, int stageIndex, int archetypeIndex, int level, string repositoryAlias, string agentStatus, string syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync) { }
        private static void RegisterNativeMenuActionCallback(NativeMenuActionCallback callback) { }
        private static void NativeShowMainWindow() { }
        private static void NativeHideMainWindow() { }
        private static bool NativeIsMainWindowVisible() { return false; }
        private static void NativeQuit() { }
#endif

        private static void OnNativeMenuAction(string action)
        {
            Debug.Log("INFO [NativeAction] received action=" + (action ?? "<null>"));
            if (TryParseMenuAction(action, out var parsed))
            {
                Debug.Log("INFO [NativeAction] routed action=" + action + " handler=" + parsed);
                GlobalMenuActionRequested?.Invoke(parsed);
            }
            else
            {
                Debug.LogWarning("WARN [NativeAction] unknown action=" + (action ?? "<null>"));
            }
        }

        private static bool TryParseMenuAction(string action, out ApplicationMenuAction parsed)
        {
            switch (action)
            {
                case "show_dashboard":
                    parsed = ApplicationMenuAction.ShowDashboard;
                    return true;
                case "hide_dashboard":
                    parsed = ApplicationMenuAction.HideDashboard;
                    return true;
                case "enable_desktop_companion":
                    parsed = ApplicationMenuAction.EnableDesktopCompanion;
                    return true;
                case "disable_desktop_companion":
                    parsed = ApplicationMenuAction.DisableDesktopCompanion;
                    return true;
                case "toggle_click_through":
                    parsed = ApplicationMenuAction.ToggleCompanionClickThrough;
                    return true;
                case "reset_companion_position":
                    parsed = ApplicationMenuAction.ResetCompanionPosition;
                    return true;
                case "add_repository":
                    parsed = ApplicationMenuAction.AddRepository;
                    return true;
                case "connect_ai_agent":
                    parsed = ApplicationMenuAction.ConnectAiAgent;
                    return true;
                case "analyze_current_repository":
                    parsed = ApplicationMenuAction.AnalyzeCurrentRepository;
                    return true;
                case "sync_now":
                    parsed = ApplicationMenuAction.SyncNow;
                    return true;
                case "settings":
                case "open_settings":
                    parsed = ApplicationMenuAction.Settings;
                    return true;
                case "quit":
                    parsed = ApplicationMenuAction.Quit;
                    return true;
                default:
                    parsed = ApplicationMenuAction.ShowDashboard;
                    return false;
            }
        }

        private static string SafeMenuText(string value, string fallback)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= 80 ? value : value.Substring(0, 80);
        }

        private static bool NativeSafeModeEnabled => IsEnvironmentFlagEnabled("TOKENFORGE_NATIVE_SAFE_MODE");
        private static bool DisableStatusItemEnabled => NativeSafeModeEnabled || IsEnvironmentFlagEnabled("TOKENFORGE_DISABLE_STATUS_ITEM");
        private static string NativeSkipReason => NativeSafeModeEnabled ? "TOKENFORGE_NATIVE_SAFE_MODE" : "TOKENFORGE_DISABLE_STATUS_ITEM";

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
    }
}
