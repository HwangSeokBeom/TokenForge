using System;
using System.Runtime.InteropServices;
using TokenForge.Client.Domain;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public sealed class MacDesktopCompanionOverlayService : IDesktopCompanionOverlayService
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

        public CompanionDesktopOverlayState State { get; private set; } = CompanionDesktopOverlayState.Unavailable;

        public bool Create()
        {
            if (!IsAvailable)
            {
                State = CompanionDesktopOverlayState.Unavailable;
                return false;
            }

            try
            {
                var created = NativeCreate();
                State = created ? CompanionDesktopOverlayState.Disabled : CompanionDesktopOverlayState.Unavailable;
                return created;
            }
            catch (Exception)
            {
                State = CompanionDesktopOverlayState.Unavailable;
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
                NativeShow();
                State = CompanionDesktopOverlayState.Active;
            }
            catch (Exception)
            {
                State = CompanionDesktopOverlayState.Unavailable;
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
            }
            catch (Exception)
            {
                State = CompanionDesktopOverlayState.Unavailable;
            }
        }

        public void SetPosition(Vector2 position)
        {
            if (IsAvailable)
            {
                try { NativeSetPosition(position.x, position.y); } catch (Exception) { State = CompanionDesktopOverlayState.Unavailable; }
            }
        }

        public void SetSize(Vector2 size)
        {
            if (IsAvailable)
            {
                try { NativeSetSize(Mathf.Max(24f, size.x), Mathf.Max(24f, size.y)); } catch (Exception) { State = CompanionDesktopOverlayState.Unavailable; }
            }
        }

        public void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft)
        {
            if (IsAvailable)
            {
                try { NativeSetVisualState((int)stage, (int)archetype, (int)animationState, facingLeft); } catch (Exception) { State = CompanionDesktopOverlayState.Unavailable; }
            }
        }

        public void SetClickThrough(bool clickThrough)
        {
            if (IsAvailable)
            {
                try { NativeSetClickThrough(clickThrough); } catch (Exception) { State = CompanionDesktopOverlayState.Unavailable; }
            }
        }

        public void Destroy()
        {
            if (IsAvailable)
            {
                try { NativeDestroy(); } catch (Exception) { }
            }

            State = IsAvailable ? CompanionDesktopOverlayState.Disabled : CompanionDesktopOverlayState.Unavailable;
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

        [DllImport("DesktopCompanionOverlay", EntryPoint = "SetCompanionOverlayClickThrough")]
        private static extern void NativeSetClickThrough(bool clickThrough);

        [DllImport("DesktopCompanionOverlay", EntryPoint = "DestroyDesktopCompanionOverlay")]
        private static extern void NativeDestroy();
#else
        private static bool NativeCreate() { return false; }
        private static void NativeShow() { }
        private static void NativeHide() { }
        private static void NativeSetPosition(float x, float y) { }
        private static void NativeSetSize(float width, float height) { }
        private static void NativeSetVisualState(int stage, int archetype, int animationState, bool facingLeft) { }
        private static void NativeSetClickThrough(bool clickThrough) { }
        private static void NativeDestroy() { }
#endif
    }
}
