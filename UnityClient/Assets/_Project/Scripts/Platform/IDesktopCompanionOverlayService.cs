using TokenForge.Client.Domain;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public interface IDesktopCompanionOverlayService
    {
        bool IsAvailable { get; }
        CompanionDesktopOverlayState State { get; }
        bool Create();
        void Show();
        void Hide();
        void SetPosition(Vector2 position);
        void SetSize(Vector2 size);
        void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft);
        void SetClickThrough(bool clickThrough);
        void Destroy();
    }
}
