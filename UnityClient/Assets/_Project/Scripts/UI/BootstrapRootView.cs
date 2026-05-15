using System;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class BootstrapRootView : MonoBehaviour
    {
        [SerializeField] public Text headerStatusLabel;
        [SerializeField] public Text validationStatusLabel;
        [SerializeField] public ScrollRect rootScrollRect;
        [SerializeField] public AccountPanelView accountPanel;
        [SerializeField] public ActivityAnalysisPanelView activityAnalysisPanel;
        [SerializeField] public ApprovedLocationsPanelView approvedLocationsPanel;
        [SerializeField] public ReviewPanelView reviewPanel;
        [SerializeField] public SafeSyncPanelView safeSyncPanel;
        [SerializeField] public RecentSessionsPanelView recentSessionsPanel;
        [SerializeField] public PrivacyNoticePanelView privacyNoticePanel;

        private LocalClientStatus status;
        private ApprovedActivityAnalysisViewModel viewModel;

        public void Bind(LocalClientStatus status, ApprovedActivityAnalysisViewModel viewModel)
        {
            this.status = status;
            this.viewModel = viewModel;
            if (!ValidateReferences(out var error))
            {
                if (validationStatusLabel != null)
                {
                    validationStatusLabel.text = "UI prefab reference error: " + error;
                }

                return;
            }

            accountPanel.Bind(viewModel, Render);
            activityAnalysisPanel.Bind(viewModel, Render);
            approvedLocationsPanel.Bind(viewModel, Render);
            reviewPanel.Bind(viewModel, Render);
            safeSyncPanel.Bind(viewModel, Render);
            recentSessionsPanel.Bind(viewModel, Render);
            privacyNoticePanel.Bind(viewModel, Render);
            Render();
        }

        public void Render()
        {
            if (headerStatusLabel != null)
            {
                headerStatusLabel.text = status == null
                    ? "TokenForge client UI"
                    : status.ClientStatus + " | Initialized " + status.InitializedAt.ToString("yyyy-MM-dd HH:mm:ss zzz");
            }

            if (validationStatusLabel != null)
            {
                validationStatusLabel.text = ValidateReferences(out var error)
                    ? "Prefab UI ready."
                    : "UI prefab reference error: " + error;
            }

            accountPanel?.Render();
            activityAnalysisPanel?.Render();
            approvedLocationsPanel?.Render();
            reviewPanel?.Render();
            safeSyncPanel?.Render();
            recentSessionsPanel?.Render();
            privacyNoticePanel?.Render();
        }

        public bool ValidateReferences(out string error)
        {
            error = string.Empty;
            var valid = true;
            valid &= Require(headerStatusLabel, nameof(headerStatusLabel), ref error);
            valid &= Require(validationStatusLabel, nameof(validationStatusLabel), ref error);
            valid &= Require(rootScrollRect, nameof(rootScrollRect), ref error);
            valid &= Require(accountPanel, nameof(accountPanel), ref error);
            valid &= Require(activityAnalysisPanel, nameof(activityAnalysisPanel), ref error);
            valid &= Require(approvedLocationsPanel, nameof(approvedLocationsPanel), ref error);
            valid &= Require(reviewPanel, nameof(reviewPanel), ref error);
            valid &= Require(safeSyncPanel, nameof(safeSyncPanel), ref error);
            valid &= Require(recentSessionsPanel, nameof(recentSessionsPanel), ref error);
            valid &= Require(privacyNoticePanel, nameof(privacyNoticePanel), ref error);

            valid &= ValidatePanel(accountPanel, ref error);
            valid &= ValidatePanel(activityAnalysisPanel, ref error);
            valid &= ValidatePanel(approvedLocationsPanel, ref error);
            valid &= ValidatePanel(reviewPanel, ref error);
            valid &= ValidatePanel(safeSyncPanel, ref error);
            valid &= ValidatePanel(recentSessionsPanel, ref error);
            valid &= ValidatePanel(privacyNoticePanel, ref error);
            return valid;
        }

        private static bool ValidatePanel(UiBinderBase panel, ref string error)
        {
            if (panel == null)
            {
                return false;
            }

            if (panel.ValidateRequiredReferences(out var panelError))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = panel.GetType().Name + ": " + panelError;
            }

            return false;
        }

        private static bool Require(UnityEngine.Object value, string fieldName, ref string error)
        {
            if (value != null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = fieldName + " is not assigned.";
            }

            return false;
        }
    }
}
