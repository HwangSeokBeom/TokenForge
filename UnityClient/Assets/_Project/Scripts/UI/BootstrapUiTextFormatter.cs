using System.Linq;
using System.Text;
using TokenForge.Client.Agents;
using TokenForge.Client.Auth;
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
            builder.Append("Git: ");
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

        public static string FriendlyGitStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var flow = viewModel?.GitFlow;
            if (flow == null)
            {
                return "Git activity is unavailable.";
            }

            switch (flow.State)
            {
                case GitAnalysisFlowState.Selected:
                    return "Ready. Repository selected as " + GitSafeAlias(viewModel) + ".";
                case GitAnalysisFlowState.Analyzing:
                    return "Analyzing Git activity.";
                case GitAnalysisFlowState.ReviewReady:
                    return "Review ready.";
                case GitAnalysisFlowState.Saved:
                    return "Saved.";
                case GitAnalysisFlowState.Failed:
                    return FriendlyError(flow.ErrorCategory, flow.UserMessage);
                default:
                    if (viewModel != null && viewModel.Onboarding.GitSkipped)
                    {
                        return "Skipped for now.";
                    }

                    return "No Git source selected.";
            }
        }

        public static string AgentStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var flow = viewModel?.AgentFlow;
            if (flow == null)
            {
                return "Agent analysis is unavailable.";
            }

            var provider = AgentSourceLabel(viewModel.SelectedAgentProviderType);
            var builder = new StringBuilder();
            builder.Append("Agent: ");
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

        public static string FriendlyAgentStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var flow = viewModel?.AgentFlow;
            if (flow == null)
            {
                return "AI agent activity is unavailable.";
            }

            switch (flow.State)
            {
                case AgentAnalysisFlowState.Selected:
                    return "Ready. Manual log folder selected.";
                case AgentAnalysisFlowState.Analyzing:
                    return "Analyzing safe local aggregate.";
                case AgentAnalysisFlowState.ReviewReady:
                    return "Review ready.";
                case AgentAnalysisFlowState.Saved:
                    return "Saved.";
                case AgentAnalysisFlowState.Failed:
                    return FriendlyError(flow.ErrorCategory, flow.UserMessage);
                default:
                    return SelectedAgentSummary(viewModel);
            }
        }

        public static string SelectedAgentSummary(ApprovedActivityAnalysisViewModel viewModel)
        {
            var selected = viewModel?.Onboarding.AgentSources
                .Where(source => source.Selected)
                .Select(SafeAgentSourceSummary)
                .ToList();
            if (selected == null || selected.Count == 0)
            {
                return "No local agent source selected yet.";
            }

            return string.Join(", ", selected);
        }

        public static string SafeAgentSourceSummary(ConnectedAgentSource source)
        {
            if (source == null)
            {
                return "Unknown source";
            }

            if (source.SourceType == ConnectedAgentSourceType.OtherManualLogFolder)
            {
                return string.IsNullOrWhiteSpace(source.SafeLabel)
                    ? "Manual Log Folder: needs review"
                    : source.SafeLabel;
            }

            if (source.State == AgentSourceSetupState.ReadyToAnalyze)
            {
                return source.DisplayName + " ready to analyze";
            }

            if (source.State == AgentSourceSetupState.AnalysisComplete)
            {
                return source.DisplayName + " analysis complete";
            }

            if (source.State == AgentSourceSetupState.LocalSourceDetected)
            {
                return source.DisplayName + " local source detected";
            }

            if (source.State == AgentSourceSetupState.ManualImportRequired)
            {
                return source.DisplayName + " manual import required";
            }

            return source.DisplayName + " selected";
        }

        public static int SelectedAgentCount(ApprovedActivityAnalysisViewModel viewModel)
        {
            return viewModel?.Onboarding.AgentSources.Count(source => source.Selected) ?? 0;
        }

        public static int DetectedLocalSourceCount(ApprovedActivityAnalysisViewModel viewModel)
        {
            return viewModel?.Onboarding.AgentSources.Count(source =>
                source.State == AgentSourceSetupState.ReadyToAnalyze ||
                source.State == AgentSourceSetupState.AnalysisComplete) ?? 0;
        }

        public static int ReadyToAnalyzeCount(ApprovedActivityAnalysisViewModel viewModel)
        {
            return viewModel?.Onboarding.AgentSources.Count(source => source.State == AgentSourceSetupState.ReadyToAnalyze) ?? 0;
        }

        public static string LocalSourcesSettings(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel?.Onboarding.AgentSources == null)
            {
                return "Local Sources: unavailable.";
            }

            var rows = viewModel.Onboarding.AgentSources.Select(source =>
            {
                var scan = source.LastScanTimeUtc.HasValue
                    ? " | Last scan " + source.LastScanTimeUtc.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm")
                    : string.Empty;
                return source.DisplayName + ": " + SourceStateLabel(source.State) + scan;
            });
            return "Local Sources\n" + string.Join("\n", rows) + "\nClear approved local source: Remove. Re-scan local sources: Detect.";
        }

        public static string SourceStateLabel(AgentSourceSetupState state)
        {
            switch (state)
            {
                case AgentSourceSetupState.Selected: return "selected";
                case AgentSourceSetupState.DetectingLocalSource: return "detecting local source";
                case AgentSourceSetupState.LocalSourceDetected: return "detected";
                case AgentSourceSetupState.ReadyToAnalyze: return "connected";
                case AgentSourceSetupState.PermissionRequired: return "permission required";
                case AgentSourceSetupState.ManualImportRequired: return "manual import required";
                case AgentSourceSetupState.AnalysisComplete: return "analysis complete";
                case AgentSourceSetupState.AnalysisFailedSafely: return "analysis failed safely";
                default: return "not detected";
            }
        }

        public static string GitSafeAlias(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "No source selected";
            }

            if (!string.IsNullOrWhiteSpace(viewModel.Onboarding.GitSafeAlias))
            {
                return viewModel.Onboarding.GitSafeAlias;
            }

            return viewModel.GitFlow.State == GitAnalysisFlowState.Selected ? "Repository 1" : "No source selected";
        }

        public static string FriendlyError(string errorCode, string fallbackMessage)
        {
            switch (errorCode)
            {
                case "NoActiveRepository":
                    return "Connect a repository first.";
                case "RepositoryPathMissing":
                    return "Repository path is missing. Reconnect required.";
                case "RepositoryFolderNotFound":
                    return "Repository folder was not found. Reconnect required.";
                case "NotAGitRepository":
                    return "This folder is not a Git repository.";
                case "GitExecutableNotFound":
                    return "Git executable was not found. Install Xcode Command Line Tools or Git.";
                case "PermissionDenied":
                    return "Permission denied while reading repository folder.";
                case "ProcessTimeout":
                    return "Git command timed out.";
                case "GitCommandFailed":
                    return "Git command failed. Check repository state and try again.";
                case "missing_repository_selection":
                    return "Select a repository before analyzing.";
                case "missing_repository_path":
                    return "Choose a repository folder first.";
                case "missing_agent_log_location":
                    return "Select an agent log folder before analyzing.";
                case "agent_source_manual_import_required":
                    return "Detect a local source or choose a manual log folder before analyzing.";
                case "agent_source_permission_required":
                    return "Permission is required before local source analysis.";
                case "agent_log_picker_failed":
                case "agent_log_picker_unavailable":
                    return "The log folder picker is unavailable.";
                case "agent_log_entry_limit_reached":
                    return "Agent log entry limit reached. Review the safe aggregate summary or increase the entry limit.";
                case "agent_log_location_unavailable":
                    return "The selected agent log folder is unavailable.";
                case "approved_location_not_found":
                    return "The approved location is no longer available.";
                case "":
                case null:
                    return string.IsNullOrWhiteSpace(fallbackMessage) ? "Something needs attention before continuing." : fallbackMessage;
                default:
                    return "Something needs attention before continuing.";
            }
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
                builder.Append(" | warnings ");
                builder.Append(review.PrivacyWarningCount);
                builder.Append(" | XP preview +");
                builder.Append(review.DerivedExpGained);
                builder.Append(" | stat preview ");
                builder.Append(TopStatCategory(review.DerivedStatDeltas));
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
                builder.Append(" | warnings ");
                builder.Append(review.WarningCount);
                builder.Append(" | XP preview +");
                builder.Append(review.DerivedExpGained);
                builder.Append(" | stat preview ");
                builder.Append(TopStatCategory(review.DerivedStatDeltas));
                builder.Append(" | save ");
                builder.Append(review.SaveEligible ? "eligible" : "blocked");
            }

            return builder.Length == 0
                ? "No review yet. Run a local analysis first. Save Review is disabled until a safe aggregate summary is ready."
                : builder.ToString();
        }

        public static string RecentSessions(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel?.RecentSessions == null || viewModel.RecentSessions.Count == 0)
            {
                return "Local: no saved safe sessions yet. Save an approved aggregate review to gain XP.";
            }

            return string.Join("\n", viewModel.RecentSessions.Select(session =>
            {
                return SafeLocalSessionLabel(session);
            }));
        }

        public static string SafeLocalSessionLabel(RecentSafeSessionSummary session)
        {
            if (session == null)
            {
                return "Unknown local session";
            }

            if (session.AgentProviderType != AgentProviderType.Unknown ||
                session.SourceProvider == "AI_AGENT" ||
                session.SourceProvider == "CODEX" ||
                session.SourceProvider == "CLAUDE" ||
                session.SourceProvider == "CURSOR" ||
                session.SourceProvider == "GITHUB_COPILOT")
            {
                return $"{session.DayBucket} · {AgentSourceLabel(session.AgentProviderType)} activity saved · +{session.ExpGained} XP. AI-assisted work contributed to {FriendlyStat(session)} growth. {FriendlyWarningText(session)}";
            }

            return $"{session.DayBucket} · {SourceLabel(session.SourceProvider)} analysis saved · +{session.ExpGained} XP. Recent Git changes increased {FriendlyStat(session)} growth. {FriendlyWarningText(session)}";
        }

        private static string FriendlyStat(RecentSafeSessionSummary session)
        {
            return string.IsNullOrWhiteSpace(session?.TopStatCategory) ? "companion" : session.TopStatCategory;
        }

        private static string FriendlyWarningText(RecentSafeSessionSummary session)
        {
            var count = WarningCount(session);
            return count <= 0
                ? "No sync issues detected."
                : count + " attention item" + (count == 1 ? " needs" : "s need") + " review in details.";
        }

        public static string AuthStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            if (viewModel == null)
            {
                return "Authentication is unavailable.";
            }

            if (viewModel.AuthState == AuthState.LoggedOut)
            {
                return "Local gameplay works offline. Create an account only for Safe Sync.";
            }

            var builder = new StringBuilder();
            builder.Append("Account: ");
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

            if (!viewModel.CanUseAuthenticatedSafeSync)
            {
                return "Connection: " + viewModel.SafeSyncConnection.StatusLabel
                       + "\nLocal gameplay works without login or server."
                       + "\nSafe Sync is optional."
                       + "\nServer: " + viewModel.SafeSyncBaseUrl
                       + "\nLast sync result: " + viewModel.SafeSyncConnection.LastSyncResult;
            }

            var builder = new StringBuilder();
            builder.Append("Connection: ");
            builder.Append(viewModel.SafeSyncConnection.StatusLabel);
            builder.Append(" | Local gameplay available");
            builder.Append("\n");
            builder.Append("Sync: ");
            builder.Append(SafeUserMessageMapper.SafeSyncStatusLabel(viewModel.SafeSyncStatus));
            builder.Append(" | Auth: ");
            builder.Append(SafeUserMessageMapper.AuthStateLabel(viewModel.AuthState));
            builder.Append(" | Server: ");
            builder.Append(viewModel.SafeSyncBaseUrl);
            builder.Append("\nMessage: ");
            builder.Append(viewModel.SafeSyncMessage);
            builder.Append("\nLast sync result: ");
            builder.Append(viewModel.SafeSyncConnection.LastSyncResult);
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

        private static string SourceLabel(string sourceProvider)
        {
            return sourceProvider == "GIT" ? "Git" : "Unknown Agent";
        }

        private static string AgentSourceLabel(AgentProviderType providerType)
        {
            switch (providerType)
            {
                case AgentProviderType.Cursor:
                    return "Cursor";
                case AgentProviderType.ClaudeCode:
                    return "Claude Code";
                case AgentProviderType.Codex:
                    return "Codex";
                case AgentProviderType.GitHubCopilot:
                    return "GitHub Copilot";
                case AgentProviderType.GeminiCli:
                    return "Gemini CLI";
                case AgentProviderType.Manual:
                    return "Other / Manual Log Folder";
                default:
                    return "Unknown Agent";
            }
        }

        private static int WarningCount(RecentSafeSessionSummary session)
        {
            return session?.WarningIds?.Count ?? 0;
        }

        private static string TopStatSuffix(RecentSafeSessionSummary session)
        {
            return string.IsNullOrWhiteSpace(session?.TopStatCategory) ? string.Empty : " | stat " + session.TopStatCategory;
        }

        private static string TopStatCategory(CharacterStats stats)
        {
            if (stats == null)
            {
                return "none";
            }

            var pairs = new[]
            {
                new { Name = "Code", Value = stats.Logic + stats.Architecture + stats.Velocity },
                new { Name = "Focus", Value = stats.Efficiency + stats.Stability },
                new { Name = "Debug", Value = stats.Debug },
                new { Name = "Design", Value = stats.Design + stats.Creativity }
            };
            var top = pairs.OrderByDescending(item => item.Value).FirstOrDefault();
            return top != null && top.Value > 0 ? top.Name : "none";
        }

        public static string RetryQueueStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.RetryQueueSummary ?? new SafeSyncRetryQueueSummary();
            var builder = new StringBuilder();
            builder.Append("Retry Queue: pending ");
            builder.Append(summary.PendingCount);
            builder.Append(" | failed ");
            builder.Append(summary.FailedCount);
            builder.Append(" | paused ");
            builder.Append(summary.PausedCount);
            builder.Append(" | succeeded ");
            builder.Append(summary.SucceededCount);
            if (summary.NextAttemptAt.HasValue)
            {
                builder.Append(" | next ");
                builder.Append(summary.NextAttemptAt.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                builder.Append(" UTC");
            }

            return builder.ToString();
        }

        public static string ConflictStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.ConflictSummary ?? new SafeSyncConflictSummary();
            if (summary.UnresolvedCount == 0)
            {
                return "Conflicts: none unresolved.";
            }

            var first = summary.SafeConflicts.FirstOrDefault();
            if (first == null)
            {
                return "Conflicts: unresolved " + summary.UnresolvedCount;
            }

            var diff = first.SafeDiffSummary?.ChangedSafeFieldNames == null || first.SafeDiffSummary.ChangedSafeFieldNames.Count == 0
                ? "none"
                : string.Join(", ", first.SafeDiffSummary.ChangedSafeFieldNames.Take(6));
            return "Conflicts: unresolved " + summary.UnresolvedCount +
                   " | " + first.ConflictType +
                   " | " + first.ResolutionStatus +
                   " | detected " + first.DetectedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" +
                   "\nLocal: " + SafeConflictSummaryLine(first.SafeLocalSummary, includeServer: false) +
                   "\nRemote: " + SafeConflictSummaryLine(first.SafeRemoteSummary, includeServer: true) +
                   "\nChanged safe fields: " + diff +
                   "\nPolicies: Keep Local, Keep Remote, Prefer Higher Confidence, Prefer Newer Safe Timestamp, Merge Non-Conflicting Aggregates, Mark Resolved Only." +
                   "\nKeep Local queues safe re-upload. Keep Remote applies safe remote aggregate locally when possible. Aggregate merge only uses deterministic safe fields." +
                   "\nResult: " + SafeUserMessageMapper.FromSyncError(first.SafeErrorCode);
        }

        public static string CompactConflictBanner(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.ConflictSummary ?? new SafeSyncConflictSummary();
            if (summary.UnresolvedCount <= 0)
            {
                return "Conflicts: none unresolved.";
            }

            var suffix = summary.UnresolvedCount == 1 ? "conflict" : "conflicts";
            return summary.UnresolvedCount + " unresolved sync " + suffix + " detected. Review in Settings.";
        }

        public static string ConflictAuditHistory(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.ConflictAuditSummary ?? new SafeConflictAuditSummary();
            if (summary.SafeEntries == null || summary.SafeEntries.Count == 0)
            {
                return "Conflict History: no local-only audit entries. Latest 200 resolved decisions are retained when present.";
            }

            var first = summary.SafeEntries.First();
            var fields = first.SafeDiffFieldNames == null || first.SafeDiffFieldNames.Count == 0
                ? "none"
                : string.Join(", ", first.SafeDiffFieldNames.Take(6));
            var warnings = first.WarningIds == null || first.WarningIds.Count == 0
                ? "none"
                : string.Join(", ", first.WarningIds.Take(4));
            var retry = string.IsNullOrWhiteSpace(first.QueuedRetryEntryId) ? "none" : "queued";
            return "Conflict History: total " + summary.TotalCount +
                   " | resolved " + summary.ResolvedCount +
                   " | failed " + summary.FailedCount +
                   " | local-only latest 200" +
                   "\nRecent: " + first.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" +
                   " | " + first.Action +
                   " | " + SafeSyncConfirmationRequestFactory.PolicyLabel(first.Policy) +
                   " | " + first.ResultStatus +
                   "\nFields: " + fields +
                   "\nRetry: " + retry +
                   "\nWarnings: " + warnings +
                   "\nMessage: " + SafeUserMessageMapper.FromSyncError(first.UserMessageCode);
        }

        public static string TombstoneStatus(ApprovedActivityAnalysisViewModel viewModel)
        {
            var summary = viewModel?.TombstoneSummary ?? new SafeSyncTombstoneSummary();
            return $"Deletes: pending {summary.PendingDeleteCount} | synced {summary.DeleteSyncedCount} | failed {summary.DeleteFailedCount} | resolved {summary.DeleteResolvedCount}";
        }

        public static string SafeTombstoneLabel(SafeSyncTombstone tombstone)
        {
            if (tombstone == null)
            {
                return "Unknown tombstone";
            }

            return $"{tombstone.SyncStatus} | source {tombstone.DeleteSource} | code {tombstone.LastSafeErrorCode}";
        }

        public static string SafeConflictLabel(SafeSyncConflict conflict)
        {
            if (conflict == null)
            {
                return "Unknown conflict";
            }

            return $"{conflict.ConflictType} | {conflict.ResolutionStatus} | day {conflict.SafeLocalSummary?.DayBucket ?? conflict.SafeRemoteSummary?.DayBucket ?? string.Empty} | code {conflict.SafeErrorCode}";
        }

        public static string SafeConflictSummaryLine(SafeSyncSessionSafeSummary summary, bool includeServer)
        {
            summary = summary ?? new SafeSyncSessionSafeSummary();
            var id = includeServer ? ShortId(summary.ServerSessionId) : ShortId(summary.ClientSessionId);
            var idLabel = includeServer ? "server " : "client ";
            return (string.IsNullOrWhiteSpace(id) ? string.Empty : idLabel + id + " | ") +
                   summary.SourceProvider +
                   " | day " + summary.DayBucket +
                   (string.IsNullOrWhiteSpace(summary.TimeBucket) ? string.Empty : " | " + summary.TimeBucket) +
                   " | confidence " + summary.Confidence +
                   " | warnings " + summary.WarningCount +
                   " | category " + EmptyAsNone(summary.CategoryBucketSummary, summary.ActivityCategory) +
                   " | tool " + EmptyAsNone(summary.ToolBucketSummary, "none") +
                   " | language " + EmptyAsNone(summary.LanguageBucketSummary, "none");
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

        private static string ShortId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= 8 ? value : value.Substring(0, 8);
        }

        private static string EmptyAsNone(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
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
