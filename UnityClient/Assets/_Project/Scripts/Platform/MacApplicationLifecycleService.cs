using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public sealed class MacApplicationLifecycleService
    {
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
                return InstallNativeLifecycle();
            }
            catch (Exception)
            {
                return false;
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
        [DllImport("DesktopCompanionOverlay", EntryPoint = "InstallTokenForgeMacAppLifecycle")]
        private static extern bool InstallNativeLifecycle();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "ShowTokenForgeMainWindow")]
        private static extern void NativeShowMainWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "HideTokenForgeMainWindow")]
        private static extern void NativeHideMainWindow();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "IsTokenForgeMainWindowVisible")]
        private static extern bool NativeIsMainWindowVisible();

        [DllImport("DesktopCompanionOverlay", EntryPoint = "QuitTokenForgeApp")]
        private static extern void NativeQuit();
#else
        private static bool InstallNativeLifecycle() { return false; }
        private static void NativeShowMainWindow() { }
        private static void NativeHideMainWindow() { }
        private static bool NativeIsMainWindowVisible() { return false; }
        private static void NativeQuit() { }
#endif
    }
}
