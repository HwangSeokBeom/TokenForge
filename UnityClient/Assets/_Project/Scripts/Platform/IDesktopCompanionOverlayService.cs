using TokenForge.Client.Domain;
using System;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public interface IDesktopCompanionOverlayService
    {
        event Action Clicked;
        event Action DoubleClicked;
        event Action<Vector2> DragEnded;
        bool IsAvailable { get; }
        bool IsNativeOverlay { get; }
        bool IsAnyOverlayDragging();
        bool IsOverlayDragging(string repositoryId);
        CompanionDesktopOverlayState State { get; }
        string StatusMessage { get; }
        bool Create();
        void Show();
        void Hide();
        void SetPosition(Vector2 position);
        void SetSize(Vector2 size);
        void SetVisualTheme(string visualThemeId);
        void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft);
        void SetMotionProfile(CompanionVisualProfile profile);
        void TriggerReaction(CompanionReaction reaction, string speechText);
        void ResetPosition();
        void SetClickEnabled(bool enabled);
        void SetClickThrough(bool clickThrough);
        void SetCompanionFarmSnapshots(DesktopCompanionFarmState farmState);
        void ShowAllRepositoryCompanions(string source = "csharp.showAll");
        void HideAllRepositoryCompanions(string source = "csharp.hideAll");
        void SetOverlayFrame(string repositoryId, Rect frame, string source = "csharp.setFrame");
        void Destroy();
    }
}
