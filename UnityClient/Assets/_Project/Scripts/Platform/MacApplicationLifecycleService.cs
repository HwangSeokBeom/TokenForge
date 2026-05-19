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
            InstallNativeLifecycle();
#endif
        }

        public bool Install()
        {
            if (!IsAvailable)
            {
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
            catch (Exception)
            {
                return false;
            }
        }

        public void UpdateStatusItem(string companionName, CompanionStage stage, CompanionArchetype archetype, int level, string repositoryAlias, string agentStatus, string syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync)
        {
            if (!IsAvailable)
            {
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
                    SafeMenuText(repositoryAlias, "Local Repository"),
                    SafeMenuText(agentStatus, "No agent connected"),
                    SafeMenuText(syncStatus, "Local only"),
                    companionEnabled,
                    clickThrough,
                    canAnalyze,
                    canSync);
            }
            catch (Exception)
            {
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
            catch (Exception)
            {
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
            catch (Exception)
            {
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
            catch (Exception)
            {
                return false;
            }
        }

        public void Quit()
        {
            if (!IsAvailable)
            {
                Application.Quit();
                return;
            }

            try
            {
                NativeQuit();
            }
            catch (Exception)
            {
                Application.Quit();
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
            if (TryParseMenuAction(action, out var parsed))
            {
                GlobalMenuActionRequested?.Invoke(parsed);
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
    }
}
