using TokenForge.Client.Domain;

namespace TokenForge.Client.UI
{
    public sealed class SettingsPrivacyViewModel
    {
        public PrivacyPreferences Preferences { get; }

        public SettingsPrivacyViewModel(PrivacyPreferences preferences)
        {
            Preferences = preferences ?? new PrivacyPreferences();
        }

        public void SetAgentAnalysisEnabled(bool enabled)
        {
            Preferences.AgentLogAnalysisEnabled = enabled;
        }

        public void SetGitAnalysisEnabled(bool enabled)
        {
            Preferences.GitAnalysisEnabled = enabled;
        }

        public void SetCloudSyncEnabled(bool enabled)
        {
            Preferences.CloudSyncEnabled = enabled;
        }

        public void SetTelemetryOptIn(bool enabled)
        {
            Preferences.TelemetryOptIn = enabled;
        }
    }
}
