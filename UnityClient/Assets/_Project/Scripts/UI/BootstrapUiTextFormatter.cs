using System.Linq;
using System.Text;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using TokenForge.Client.Sync;

namespace TokenForge.Client.UI
{
    public static class BootstrapUiTextFormatter
    {
        public static string GitStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var flow = viewModel?.GitFlow;
            if (flow == null)
            {
                return "Git analysis is unavailable.";
            }

            var builder = new StringBuilder();
            builder.Append("State: ");
            builder.Append(flow.State);
            builder.Append(" | ");
            builder.Append(flow.SelectionStatus);
            builder.Append(" | ");
            builder.Append(flow.UserMessage);

            if (!string.IsNullOrWhiteSpace(flow.ErrorCategory))
            {
                builder.Append(" | Error: ");
                builder.Append(flow.ErrorCategory);
            }

            return builder.ToString();
        }

        public static string AgentStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var flow = viewModel?.AgentFlow;
            if (flow == null)
            {
                return "Agent analysis is unavailable.";
            }

            var provider = viewModel.SelectedAgentProviderType == AgentProviderType.Unknown
                ? "Unknown/Auto"
                : viewModel.SelectedAgentProviderType.ToString();
            var builder = new StringBuilder();
            builder.Append("State: ");
            builder.Append(flow.State);
            builder.Append(" | ");
            builder.Append(viewModel.AgentSelectionStatus);
            builder.Append(" | Provider: ");
            builder.Append(provider);
            builder.Append(" | ");
            builder.Append(flow.UserMessage);

            if (!string.IsNullOrWhiteSpace(flow.ErrorCategory))
            {
                builder.Append(" | Error: ");
                builder.Append(flow.ErrorCategory);
            }

            if (!string.IsNullOrWhiteSpace(viewModel.AgentPickerErrorCategory))
            {
                builder.Append(" | Picker: ");
                builder.Append(viewModel.AgentPickerErrorCategory);
            }

            return builder.ToString();
        }

        public static string ReviewSummary(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "No review ready.";
            }

            var builder = new StringBuilder();
            if (viewModel.GitFlow.Review != null)
            {
                var review = viewModel.GitFlow.Review;
                builder.Append("Git: ");
                builder.Append(review.SessionAlias);
                builder.Append(" | day ");
                builder.Append(review.AnalysisTimeBucket);
                builder.Append(" | changes ");
                builder.Append(review.ChangedFilesBucket);
                builder.Append(" | lines +");
                builder.Append(review.AddedLinesBucket);
                builder.Append(" -");
                builder.Append(review.DeletedLinesBucket);
                builder.Append(" | confidence ");
                builder.Append(review.ConfidenceLevel);
                builder.Append(" | warning IDs ");
                builder.Append(review.PrivacyWarningCategories.Count == 0 ? "none" : string.Join(", ", review.PrivacyWarningCategories));
                builder.AppendLine();
            }

            if (viewModel.AgentFlow.Review != null)
            {
                var review = viewModel.AgentFlow.Review;
                builder.Append("Agent: ");
                builder.Append(review.ProviderType);
                builder.Append(" | day ");
                builder.Append(review.DayBucket);
                builder.Append(" | sessions ");
                builder.Append(review.SessionCountBucket);
                builder.Append(" | interactions ");
                builder.Append(review.InteractionCountBucket);
                builder.Append(" | confidence ");
                builder.Append(review.ConfidenceLevel);
                builder.Append(" | warning IDs ");
                builder.Append(review.WarningIds.Count == 0 ? "none" : string.Join(", ", review.WarningIds));
                builder.Append(" | save ");
                builder.Append(review.SaveEligible ? "eligible" : "blocked");
            }

            return builder.Length == 0
                ? "No review ready. Select an approved location and run analysis to review safe aggregate data before saving."
                : builder.ToString();
        }

        public static string RecentSessions(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel?.RecentSessions == null || viewModel.RecentSessions.Count == 0)
            {
                return "Local: no saved safe sessions yet.";
            }

            return string.Join("\n", viewModel.RecentSessions.Select(session =>
            {
                if (session.SourceProvider == "AI_AGENT")
                {
                    return $"{session.DayBucket} | AI agent {session.AgentProviderType} | sessions {session.AgentSessionCountBucket} | interactions {session.AgentInteractionCountBucket} | confidence {session.Confidence}";
                }

                return $"{session.DayBucket} | {session.SourceProvider} | work {session.WorkType} | changes {session.GitChangeCountBucket} | lines +{session.GitAddedLineBucket} -{session.GitDeletedLineBucket} | confidence {session.Confidence}";
            }));
        }

        public static string AuthStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "Authentication is unavailable.";
            }

            var builder = new StringBuilder();
            builder.Append("Status: ");
            builder.Append(SafeUserMessageMapper.AuthStateLabel(viewModel.AuthState));
            builder.Append(" | ");
            builder.Append(viewModel.AuthStatusMessage);
            builder.Append(" | Session: ");
            builder.Append(viewModel.AuthSessionExpirySummary);
            if (!string.IsNullOrWhiteSpace(viewModel.AuthErrorCode))
            {
                builder.Append(" | Code: ");
                builder.Append(viewModel.AuthErrorCode);
            }

            return builder.ToString();
        }

        public static string SafeSyncStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "Safe Sync is unavailable.";
            }

            var builder = new StringBuilder();
            builder.Append("Status: ");
            builder.Append(SafeUserMessageMapper.SafeSyncStatusLabel(viewModel.SafeSyncStatus));
            builder.Append(" | Auth: ");
            builder.Append(SafeUserMessageMapper.AuthStateLabel(viewModel.AuthState));
            builder.Append(" | Server: ");
            builder.Append(viewModel.SafeSyncBaseUrl);
            builder.Append(" | Message: ");
            builder.Append(viewModel.SafeSyncMessage);
            builder.Append(" | Accepted ");
            builder.Append(viewModel.LastSyncAcceptedCount);
            builder.Append(" / rejected ");
            builder.Append(viewModel.LastSyncRejectedCount);
            if (!string.IsNullOrWhiteSpace(viewModel.SafeSyncErrorCode))
            {
                builder.Append(" | Code: ");
                builder.Append(viewModel.SafeSyncErrorCode);
            }

            return builder.ToString();
        }

        public static string RemoteSessions(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel?.RemoteSafeSessions == null || viewModel.RemoteSafeSessions.Count == 0)
            {
                return "Remote: no fetched remote safe sessions.";
            }

            return string.Join("\n", viewModel.RemoteSafeSessions.Take(6).Select(SafeRemoteSessionLabel));
        }

        public static string SafeRemoteSessionLabel(RemoteSafeSessionSummary session)
        {
            if (session == null)
            {
                return "Unknown remote session";
            }

            return $"{session.DayBucket} | {session.SourceProvider} | {session.ActivityCategory} | changes {session.ChangeCountBucket} | lines {session.LineCountBucket} | sessions {session.SessionCountBucket} | interactions {session.InteractionCountBucket} | confidence {session.Confidence} | warnings {session.WarningCount} | schema {session.SchemaVersion}";
        }

        public static string ApprovedLocationLabel(ApprovedLocationDisplayItem item)
        {
            if (item == null)
            {
                return "No approved locations";
            }

            return item.DisplayAlias + (item.Enabled ? string.Empty : " (disabled)") + " / " + item.SourceType;
        }
    }
}
