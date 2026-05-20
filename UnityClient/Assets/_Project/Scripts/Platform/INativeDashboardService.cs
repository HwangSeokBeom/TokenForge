using System;

namespace TokenForge.Client.Platform
{
    public interface INativeDashboardService
    {
        event Action<NativeDashboardActionRequest> ActionRequested;
        bool IsAvailable { get; }
        bool Install();
        void ShowDashboardWindow();
        void HideDashboardWindow();
        void ToggleDashboardWindow();
        void ShowSettingsWindow();
        void UpdateDashboardState(NativeDashboardState state);
        void SetMenuBarStatus(NativeDashboardState state);
        void SetCompanionVisible(bool visible);
    }
}
