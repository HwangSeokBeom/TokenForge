using TokenForge.Client.Domain;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public sealed class InAppCompanionOverlayFallbackService : IDesktopCompanionOverlayService
    {
        public bool IsAvailable => false;
        public CompanionDesktopOverlayState State { get; private set; } = CompanionDesktopOverlayState.Fallback;
        public bool Create()
        {
            State = CompanionDesktopOverlayState.Fallback;
            return false;
        }

        public void Show()
        {
            State = CompanionDesktopOverlayState.Fallback;
        }

        public void Hide()
        {
            State = CompanionDesktopOverlayState.Disabled;
        }

        public void SetPosition(Vector2 position)
        {
        }

        public void SetSize(Vector2 size)
        {
        }

        public void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft)
        {
        }

        public void SetClickThrough(bool clickThrough)
        {
        }

        public void Destroy()
        {
            State = CompanionDesktopOverlayState.Disabled;
        }
    }
}
