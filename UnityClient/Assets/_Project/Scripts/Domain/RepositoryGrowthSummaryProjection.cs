using System;
using System.Collections.Generic;
using System.Linq;

namespace TokenForge.Client.Domain
{
    public sealed class RepositoryGrowthSummary
    {
        public string RepositoryId { get; set; } = string.Empty;
        public string SelectedRepositoryId { get; set; } = string.Empty;
        public string CanonicalRepositoryId { get; set; } = string.Empty;
        public int Code { get; set; }
        public int Focus { get; set; }
        public int Debug { get; set; }
        public int Design { get; set; }
        public int Sync { get; set; }
        public int WeeklyCode { get; set; }
        public int WeeklyFocus { get; set; }
        public int WeeklyDebug { get; set; }
        public int WeeklyDesign { get; set; }
        public int WeeklySync { get; set; }
        public int TotalXp { get; set; }
        public int WeeklyXp { get; set; }
        public bool HasSavedGrowth { get; set; }
        public bool HasHistoricalEvents { get; set; }
        public bool HasStoredAxisDeltas { get; set; }
        public bool HasLegacyAxisGap { get; set; }
        public string ProjectionSource { get; set; } = "none";
        public string FirstCommit { get; set; } = string.Empty;
        public string FirstCommitDate { get; set; } = string.Empty;
        public string CurrentHead { get; set; } = string.Empty;
        public string LastAnalyzedCommit { get; set; } = string.Empty;
        public int CommitsAnalyzed { get; set; }
        public int FilesChanged { get; set; }
        public bool FallbackUsed { get; set; }
        public bool CacheHit { get; set; }
        public string ReasonIfUnchanged { get; set; } = string.Empty;
        public string LatestSummary { get; set; } = "No growth recorded yet.";
        public List<RepositoryTimelineEvent> TimelineEvents { get; set; } = new List<RepositoryTimelineEvent>();
        public List<string> MatchedRepositoryAliases { get; set; } = new List<string>();
        public int MatchedSessionCount { get; set; }
        public int MatchedApprovedGrowthCount { get; set; }
        public int MatchedNativeRunCount { get; set; }
        public int MatchedActivityReviewCount { get; set; }
        public int MatchedTimelineEventCount { get; set; }

        public CompanionStatProfile ToStatProfile()
        {
            return new CompanionStatProfile
            {
                CodeStat = Math.Max(0, Code),
                FocusStat = Math.Max(0, Focus),
                DebugStat = Math.Max(0, Debug),
                DesignStat = Math.Max(0, Design),
                SyncStat = Math.Max(0, Sync)
            };
        }
    }

    public static class RepositoryGrowthSummaryProjection
    {
        public static RepositoryGrowthSummary Build(SaveData saveData, string repositoryId, DateTimeOffset? nowUtc = null)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            var selectedRepositoryId = string.IsNullOrWhiteSpace(repositoryId) ? saveData.SelectedRepositoryHash ?? string.Empty : repositoryId.Trim();
            var canonicalSelection = RepositoryCompanionProfileService.CanonicalizeSelectedRepositoryHash(saveData, selectedRepositoryId, false);
            repositoryId = canonicalSelection.Resolved ? canonicalSelection.ResolvedCanonicalHash : selectedRepositoryId;
            var now = nowUtc ?? DateTimeOffset.UtcNow;
            var weekStart = now.AddDays(-7);
            var summary = new RepositoryGrowthSummary
            {
                RepositoryId = repositoryId,
                SelectedRepositoryId = selectedRepositoryId,
                CanonicalRepositoryId = canonicalSelection.Resolved ? canonicalSelection.ResolvedCanonicalHash : string.Empty
            };
            if (string.IsNullOrWhiteSpace(repositoryId))
            {
                return summary;
            }

            var repositoryAliases = RepositoryIdentityAliases(saveData, repositoryId);
            summary.MatchedRepositoryAliases = repositoryAliases.OrderBy(alias => alias, StringComparer.Ordinal).ToList();
            var sessions = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => session != null && repositoryAliases.Contains(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session)))
                .OrderBy(session => session.EndedAt)
                .ToList();
            var sessionById = sessions
                .Where(session => !string.IsNullOrWhiteSpace(session.SessionId))
                .GroupBy(session => session.SessionId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(session => session.EndedAt).First(), StringComparer.Ordinal);
            var sessionIds = sessions
                .Select(session => session.SessionId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.Ordinal);
            var growthRecords = (saveData.GrowthHistory ?? new List<CharacterGrowthResult>())
                .Where(growth => growth != null && sessionIds.Contains(growth.SessionId))
                .ToList();
            var savedNativeRuns = (saveData.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>())
                .Where(run => run != null && repositoryAliases.Contains(run.RepositoryId ?? string.Empty) && IsSavedNativeRun(run))
                .OrderByDescending(run => run.CreatedAtUtc)
                .ToList();
            var savedActivityReviews = (saveData.ActivityReviews ?? new List<ActivityReview>())
                .Where(review => review != null &&
                                 repositoryAliases.Contains(review.RepositoryId ?? string.Empty) &&
                                 string.Equals(review.Status, "saved", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(review => review.SavedAt ?? review.CreatedAt)
                .ToList();
            var timelineEvents = (saveData.RepositoryTimelineEvents ?? new List<RepositoryTimelineEvent>())
                .Where(item => item != null && repositoryAliases.Contains(item.RepositoryId ?? string.Empty))
                .OrderByDescending(item => item.TimestampUtc)
                .ToList();
            var connectedProject = SelectedConnectedProject(saveData, repositoryAliases);
            var aiTokenUsageCount = sessions.Count(session => session != null && session.TokenUsageBucket != TokenUsageBucket.Unknown);
            summary.FirstCommit = FirstNonEmpty(connectedProject?.FirstCommitHash, connectedProject?.FirstAnalyzedCommit, "none");
            summary.FirstCommitDate = FirstNonEmpty(connectedProject?.FirstCommitAt, "none");
            summary.CurrentHead = FirstNonEmpty(connectedProject?.CurrentHeadCommit, "none");
            summary.LastAnalyzedCommit = FirstNonEmpty(connectedProject?.LastAnalyzedCommit, "none");
            summary.CommitsAnalyzed = Math.Max(0, connectedProject?.TotalCommitCount ?? 0);
            summary.FilesChanged = Math.Max(0, connectedProject?.FilesChangedAnalyzed ?? SummedChangedFiles(sessions));
            summary.FallbackUsed = false;
            summary.CacheHit = false;
            summary.ReasonIfUnchanged = summary.CurrentHead == summary.LastAnalyzedCommit && summary.CurrentHead != "none"
                ? "already_at_current_head"
                : "head_or_analysis_checkpoint_changed";

            summary.MatchedSessionCount = sessions.Count;
            summary.MatchedApprovedGrowthCount = growthRecords.Count;
            summary.MatchedNativeRunCount = savedNativeRuns.Count;
            summary.MatchedActivityReviewCount = savedActivityReviews.Count;
            summary.MatchedTimelineEventCount = timelineEvents.Count;
            summary.TimelineEvents = timelineEvents;
            summary.HasSavedGrowth = growthRecords.Any(growth => Math.Max(0, growth.ExpGained) > 0) ||
                                     savedNativeRuns.Any(run => Math.Max(0, run.XpDelta) > 0) ||
                                     savedActivityReviews.Any(review => Math.Max(0, review.XpDelta) > 0) ||
                                     timelineEvents.Any(IsSavedGrowthTimelineEvent);
            summary.HasHistoricalEvents = sessions.Count > 0 || growthRecords.Count > 0 || savedNativeRuns.Count > 0 || savedActivityReviews.Count > 0 || timelineEvents.Count > 0;

            var timelineAxisEvents = timelineEvents.Where(HasTimelineAxisDelta).ToList();
            var savedTimelineAxisEvents = timelineAxisEvents.Where(IsSavedGrowthTimelineEvent).ToList();
            var hasCanonicalGrowthSavedTimelineEvent = timelineEvents.Any(item => string.Equals(item?.EventType, "growth_saved", StringComparison.OrdinalIgnoreCase));
            var timelineHasSavedGrowthXp = savedTimelineAxisEvents.Any(item => ShouldCountTimelineXp(item, hasCanonicalGrowthSavedTimelineEvent));
            var timelineSavedGrowthXp = SumTimelineXp(timelineEvents, item => ShouldCountTimelineXp(item, hasCanonicalGrowthSavedTimelineEvent));
            var growthHistoryXp = SumGrowthXp(growthRecords, null);
            var nativeRunXp = SumNativeRunXp(savedNativeRuns, null);
            var activityReviewXp = SumActivityReviewXp(savedActivityReviews, null);
            var timelineSavedGrowthWeeklyXp = SumTimelineXp(timelineEvents, item => ShouldCountTimelineXp(item, hasCanonicalGrowthSavedTimelineEvent) && item.TimestampUtc >= weekStart);
            var growthHistoryWeeklyXp = SumGrowthXp(growthRecords, growth => (sessionById.TryGetValue(growth.SessionId ?? string.Empty, out var session) ? session.EndedAt : now) >= weekStart);
            var nativeRunWeeklyXp = SumNativeRunXp(savedNativeRuns, run => run.CreatedAtUtc >= weekStart);
            var activityReviewWeeklyXp = SumActivityReviewXp(savedActivityReviews, review => (review.SavedAt ?? review.CreatedAt) >= weekStart);
            if (timelineAxisEvents.Count > 0)
            {
                foreach (var timelineEvent in timelineAxisEvents.OrderBy(item => item.TimestampUtc))
                {
                    AddAxes(summary, AxesFromTimeline(timelineEvent), timelineEvent.TimestampUtc >= weekStart);
                    if (ShouldCountTimelineXp(timelineEvent, hasCanonicalGrowthSavedTimelineEvent))
                    {
                        AddXp(summary, timelineEvent.DeltaXp, timelineEvent.TimestampUtc >= weekStart);
                    }

                    summary.HasStoredAxisDeltas = true;
                    summary.ProjectionSource = "timelineAxisDelta";
                }
            }

            foreach (var growth in growthRecords)
            {
                sessionById.TryGetValue(growth.SessionId ?? string.Empty, out var session);
                var endedAt = session?.EndedAt ?? now;
                if (savedTimelineAxisEvents.Count == 0)
                {
                    var axes = AxesFromCharacterStats(growth.StatDeltas);
                    if (axes.HasAny && HasAnyStat(growth.StatDeltas))
                    {
                        summary.HasStoredAxisDeltas = true;
                        AddAxes(summary, axes, endedAt >= weekStart);
                        summary.ProjectionSource = timelineAxisEvents.Count > 0 ? "growthHistory+timelineAxisDelta" : "growthHistory";
                    }
                }

                if (!timelineHasSavedGrowthXp)
                {
                    AddXp(summary, growth.ExpGained, endedAt >= weekStart);
                }
            }

            if (timelineAxisEvents.Count == 0 && !summary.HasStoredAxisDeltas)
            {
                foreach (var run in savedNativeRuns)
                {
                    var axes = AxesFromCharacterStats(run.StatDeltas);
                    if (!axes.HasAny)
                    {
                        continue;
                    }

                    AddAxes(summary, axes, run.CreatedAtUtc >= weekStart);
                    AddXp(summary, run.XpDelta, run.CreatedAtUtc >= weekStart);
                    summary.HasStoredAxisDeltas = true;
                    summary.ProjectionSource = "savedNativeRun";
                }
            }

            if (timelineAxisEvents.Count == 0 && !summary.HasStoredAxisDeltas)
            {
                foreach (var review in savedActivityReviews)
                {
                    var axes = AxesFromCharacterStats(review.CategoryBreakdown);
                    if (!axes.HasAny)
                    {
                        continue;
                    }

                    var recordedAt = review.SavedAt ?? review.CreatedAt;
                    AddAxes(summary, axes, recordedAt >= weekStart);
                    AddXp(summary, review.XpDelta, recordedAt >= weekStart);
                    summary.HasStoredAxisDeltas = true;
                    summary.ProjectionSource = "savedActivityReview";
                }
            }

            var storedTotalXp = Math.Max(Math.Max(growthHistoryXp, nativeRunXp), Math.Max(activityReviewXp, timelineSavedGrowthXp));
            var storedWeeklyXp = Math.Max(Math.Max(growthHistoryWeeklyXp, nativeRunWeeklyXp), Math.Max(activityReviewWeeklyXp, timelineSavedGrowthWeeklyXp));
            summary.TotalXp = Math.Max(summary.TotalXp, storedTotalXp);
            summary.WeeklyXp = Math.Max(summary.WeeklyXp, storedWeeklyXp);

            foreach (var timelineEvent in timelineEvents)
            {
                AddTimelineSyncSignal(summary, timelineEvent, timelineEvent.TimestampUtc >= weekStart);
            }

            var hasAuthoritativeGitAxes = connectedProject != null &&
                                          string.Equals(connectedProject.GrowthScoringVersion, "git-growth-axes-v2", StringComparison.Ordinal) &&
                                          connectedProject.TotalCommitCount > 0;
            if (hasAuthoritativeGitAxes)
            {
                // The latest persisted Git-history analysis is a repository snapshot. It replaces
                // accumulated legacy rule weights so rerunning Full History cannot double-count an
                // older baseline or leak values from the previously selected repository.
                summary.Code = Math.Max(0, connectedProject.GrowthCodeScore);
                summary.Focus = Math.Max(0, connectedProject.GrowthFocusScore);
                summary.Debug = Math.Max(0, connectedProject.GrowthDebugScore);
                summary.Design = Math.Max(0, connectedProject.GrowthDesignScore);
                summary.Sync = Math.Max(0, connectedProject.GrowthSyncScore);
                summary.HasStoredAxisDeltas = true;
                summary.HasHistoricalEvents = true;
                summary.ProjectionSource = connectedProject.GrowthScoringVersion;
                summary.ReasonIfUnchanged = summary.CurrentHead == summary.LastAnalyzedCommit && summary.CurrentHead != "none"
                    ? "already_at_current_head"
                    : "head_or_analysis_checkpoint_changed";
            }

            if (summary.ProjectionSource == "none" && summary.HasHistoricalEvents)
            {
                summary.ProjectionSource = summary.HasSavedGrowth ? "legacyAxisMissing" : "historicalActivityOnly";
            }

            summary.HasLegacyAxisGap = !hasAuthoritativeGitAxes && summary.HasSavedGrowth &&
                                       ((!summary.HasStoredAxisDeltas &&
                                         summary.Code == 0 &&
                                         summary.Focus == 0 &&
                                         summary.Debug == 0 &&
                                         summary.Design == 0 &&
                                         summary.Sync == 0) ||
                                         (savedTimelineAxisEvents.Count > 0 &&
                                          timelineSavedGrowthXp > 0 &&
                                          growthHistoryXp > timelineSavedGrowthXp));

            if (summary.HasLegacyAxisGap && summary.ProjectionSource != "legacyAxisMissing")
            {
                summary.ProjectionSource = summary.ProjectionSource + "+legacyAxisGap";
            }

            // Distinguish "0 because there is genuinely nothing recorded for this repository"
            // from "0 because the head has not advanced since the last analysis". Without this a
            // freshly connected / never-analyzed repository (e.g. TokenForgeCoreServer) is
            // indistinguishable from a stale-but-unchanged one and the summary looks silently
            // frozen at 0/0/0/0/0 instead of reporting why it is empty.
            var hasAnyAxisSignal = summary.Code > 0 || summary.Focus > 0 || summary.Debug > 0 ||
                                   summary.Design > 0 || summary.Sync > 0 || summary.TotalXp > 0;
            if (!summary.HasHistoricalEvents)
            {
                summary.ReasonIfUnchanged = "no_recorded_activity";
            }
            else if (!hasAnyAxisSignal)
            {
                summary.ReasonIfUnchanged = "recorded_activity_without_axis_delta";
            }

            summary.Code = Math.Max(0, summary.Code);
            summary.Focus = Math.Max(0, summary.Focus);
            summary.Debug = Math.Max(0, summary.Debug);
            summary.Design = Math.Max(0, summary.Design);
            summary.Sync = Math.Max(0, summary.Sync);
            summary.WeeklyCode = Math.Max(0, summary.WeeklyCode);
            summary.WeeklyFocus = Math.Max(0, summary.WeeklyFocus);
            summary.WeeklyDebug = Math.Max(0, summary.WeeklyDebug);
            summary.WeeklyDesign = Math.Max(0, summary.WeeklyDesign);
            summary.WeeklySync = Math.Max(0, summary.WeeklySync);
            summary.TotalXp = Math.Max(0, summary.TotalXp);
            summary.WeeklyXp = Math.Max(0, summary.WeeklyXp);
            summary.LatestSummary = LatestSummary(timelineEvents, growthRecords, savedNativeRuns, savedActivityReviews, sessions, summary);
            var excludedOtherRepoCount = ExcludedOtherRepoCount(saveData, repositoryAliases);
            UnityEngine.Debug.Log("INFO [GrowthSummary][SOURCE_OF_TRUTH] selectedRepoHash=" + selectedRepositoryId +
                                  " repoPath=approvedLocalFolder" +
                                  " repoDisplayName=" + SafeLogValue(connectedProject?.DisplayName, repositoryId) +
                                  " firstCommit=" + SafeLogValue(summary.FirstCommit, "none") +
                                  " firstCommitDate=" + SafeLogValue(summary.FirstCommitDate, "none") +
                                  " firstConnectedAt=" + (connectedProject?.FirstConnectedAt?.UtcDateTime.ToString("O") ?? "none") +
                                  " firstAnalyzedCommit=" + SafeLogValue(connectedProject?.FirstAnalyzedCommit, "none") +
                                  " currentHead=" + SafeLogValue(connectedProject?.CurrentHeadCommit, "none") +
                                  " lastAnalyzedCommit=" + SafeLogValue(summary.LastAnalyzedCommit, "none") +
                                  " commitsAnalyzed=" + summary.CommitsAnalyzed +
                                  " filesChanged=" + summary.FilesChanged +
                                  " projectionSource=" + summary.ProjectionSource +
                                  " code=" + summary.Code +
                                  " focus=" + summary.Focus +
                                  " debug=" + summary.Debug +
                                  " design=" + summary.Design +
                                  " sync=" + summary.Sync +
                                  " legacyGapCount=" + (summary.HasLegacyAxisGap ? 1 : 0) +
                                  " fallbackUsed=" + summary.FallbackUsed +
                                  " cacheHit=" + summary.CacheHit +
                                  " reasonIfUnchanged=" + summary.ReasonIfUnchanged +
                                  " timelineEventCount=" + timelineEvents.Count +
                                  " savedGrowthCount=" + growthRecords.Count +
                                  " aiTokenUsageCount=" + aiTokenUsageCount +
                                  " profileRepoHash=" + ProfileHashFor(saveData, repositoryId) +
                                  " canonicalRepoHash=" + (canonicalSelection.Resolved ? canonicalSelection.ResolvedCanonicalHash : repositoryId) +
                                  " sessionCount=" + sessions.Count +
                                  " savedRunCount=" + savedNativeRuns.Count +
                                  " reviewCount=" + savedActivityReviews.Count +
                                  " timelineEventCount=" + timelineEvents.Count +
                                  " xpTotal=" + summary.TotalXp +
                                  " axisSource=" + summary.ProjectionSource +
                                  " hasLegacyAxisGap=" + summary.HasLegacyAxisGap +
                                  " excludedOtherRepoCount=" + excludedOtherRepoCount +
                                  " sessionsCount=" + sessions.Count +
                                  " savedGrowthCount=" + growthRecords.Count +
                                  " timelineEventsCount=" + timelineEvents.Count +
                                  " legacyGapCount=" + (summary.HasLegacyAxisGap ? 1 : 0) +
                                  " axisTotals=" + summary.Code + ":" + summary.Focus + ":" + summary.Debug + ":" + summary.Design + ":" + summary.Sync +
                                  " sourcePriority=" + summary.ProjectionSource);
            UnityEngine.Debug.Log("INFO [GrowthSummaryDiagnostic] repoHash=" + repositoryId +
                                  " selectedRepoHash=" + SafeLogValue(selectedRepositoryId, "none") +
                                  " axisTotals=" + summary.Code + ":" + summary.Focus + ":" + summary.Debug + ":" + summary.Design + ":" + summary.Sync +
                                  " hasHistoricalEvents=" + summary.HasHistoricalEvents +
                                  " hasSavedGrowth=" + summary.HasSavedGrowth +
                                  " hasStoredAxisDeltas=" + summary.HasStoredAxisDeltas +
                                  " projectionSource=" + summary.ProjectionSource +
                                  " reasonIfUnchanged=" + summary.ReasonIfUnchanged +
                                  " timelineEventCount=" + timelineEvents.Count +
                                  " excludedOtherRepoCount=" + excludedOtherRepoCount +
                                  " fallbackUsed=" + summary.FallbackUsed +
                                  " cacheHit=" + summary.CacheHit);
            UnityEngine.Debug.Log("INFO [GrowthProjectionDiagnostic] repoHash=" + repositoryId +
                                  " code=" + summary.Code +
                                  " focus=" + summary.Focus +
                                  " debug=" + summary.Debug +
                                  " design=" + summary.Design +
                                  " sync=" + summary.Sync +
                                  " xp=" + summary.TotalXp +
                                  " level=" + LevelFor(saveData, repositoryId) +
                                  " stage=" + StageFor(saveData, repositoryId) +
                                  " usedLegacyGap=" + summary.HasLegacyAxisGap +
                                  " preventedGlobalFallback=true");
            return summary;
        }

        private static string ProfileHashFor(SaveData saveData, string repositoryId)
        {
            return (saveData?.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .FirstOrDefault(profile => profile != null &&
                                           profile.ArchivedAtUtc == null &&
                                           string.Equals(profile.RepositoryHash, repositoryId, StringComparison.Ordinal))
                ?.RepositoryHash ?? string.Empty;
        }

        private static ConnectedProject SelectedConnectedProject(SaveData saveData, HashSet<string> repositoryAliases)
        {
            repositoryAliases = repositoryAliases ?? new HashSet<string>(StringComparer.Ordinal);
            return (saveData?.ConnectedProjects ?? new List<ConnectedProject>())
                .Where(project => project != null && !project.IsArchived)
                .OrderByDescending(project => project.IsActive)
                .FirstOrDefault(project =>
                    repositoryAliases.Contains(project.Id ?? string.Empty) ||
                    repositoryAliases.Contains(project.PathHash ?? string.Empty) ||
                    repositoryAliases.Contains(project.ProjectPathHash ?? string.Empty) ||
                    repositoryAliases.Contains(project.LocalOnlyProjectId ?? string.Empty));
        }

        private static string SafeLogValue(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().Replace(" ", "_");
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static int SummedChangedFiles(IEnumerable<AgentWorkSession> sessions)
        {
            return Math.Max(0, (sessions ?? Enumerable.Empty<AgentWorkSession>())
                .Where(session => session?.GitChangeSummary != null)
                .Sum(session => Math.Max(0, session.GitChangeSummary.ChangedFileCount)));
        }

        private static int ExcludedOtherRepoCount(SaveData saveData, HashSet<string> selectedAliases)
        {
            selectedAliases = selectedAliases ?? new HashSet<string>(StringComparer.Ordinal);
            var sessionCount = (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Count(session => session != null &&
                                  !string.IsNullOrWhiteSpace(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session)) &&
                                  !selectedAliases.Contains(RepositoryCompanionProfileService.SafeRepositoryHashForSession(session)));
            var nativeRunCount = (saveData?.RecentNativeAnalysisRuns ?? new List<NativeAnalysisRunRecord>())
                .Count(run => run != null &&
                              !string.IsNullOrWhiteSpace(run.RepositoryId) &&
                              !selectedAliases.Contains(run.RepositoryId));
            var reviewCount = (saveData?.ActivityReviews ?? new List<ActivityReview>())
                .Count(review => review != null &&
                                 !string.IsNullOrWhiteSpace(review.RepositoryId) &&
                                 !selectedAliases.Contains(review.RepositoryId));
            var timelineCount = (saveData?.RepositoryTimelineEvents ?? new List<RepositoryTimelineEvent>())
                .Count(item => item != null &&
                               !string.IsNullOrWhiteSpace(item.RepositoryId) &&
                               !selectedAliases.Contains(item.RepositoryId));
            return Math.Max(0, sessionCount + nativeRunCount + reviewCount + timelineCount);
        }

        private static int LevelFor(SaveData saveData, string repositoryId)
        {
            var profile = (saveData?.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .FirstOrDefault(item => item != null &&
                                        item.ArchivedAtUtc == null &&
                                        string.Equals(item.RepositoryHash, repositoryId, StringComparison.Ordinal));
            return Math.Max(1, CompanionProgressionRules.Normalize(profile?.CompanionState).Level);
        }

        private static string StageFor(SaveData saveData, string repositoryId)
        {
            var profile = (saveData?.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
                .FirstOrDefault(item => item != null &&
                                        item.ArchivedAtUtc == null &&
                                        string.Equals(item.RepositoryHash, repositoryId, StringComparison.Ordinal));
            return CompanionProgressionRules.Normalize(profile?.CompanionState).Stage.ToString();
        }

        private static HashSet<string> RepositoryIdentityAliases(SaveData saveData, string repositoryId)
        {
            var aliases = new HashSet<string>(StringComparer.Ordinal);
            AddAlias(aliases, repositoryId);
            foreach (var project in saveData?.ConnectedProjects ?? new List<ConnectedProject>())
            {
                if (project == null)
                {
                    continue;
                }

                var projectIds = new[] { project.Id, project.PathHash, project.ProjectPathHash, project.LocalOnlyProjectId };
                var matchesRequestedRepository = projectIds.Any(id => !string.IsNullOrWhiteSpace(id) && aliases.Contains(id.Trim())) ||
                                                 (!string.IsNullOrWhiteSpace(repositoryId) &&
                                                  !string.IsNullOrWhiteSpace(project.DisplayName) &&
                                                  string.Equals(project.DisplayName.Trim(), repositoryId.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!matchesRequestedRepository &&
                    project.IsActive &&
                    !project.IsArchived &&
                    !string.IsNullOrWhiteSpace(saveData?.SelectedRepositoryHash) &&
                    string.Equals(saveData.SelectedRepositoryHash.Trim(), repositoryId.Trim(), StringComparison.Ordinal))
                {
                    matchesRequestedRepository = true;
                }

                if (!matchesRequestedRepository)
                {
                    continue;
                }

                foreach (var id in projectIds)
                {
                    AddAlias(aliases, id);
                }
            }

            foreach (var profile in saveData?.RepositoryCompanionProfiles ?? new List<RepositoryCompanionProfile>())
            {
                if (profile == null)
                {
                    continue;
                }

                if (aliases.Contains(profile.RepositoryHash ?? string.Empty) ||
                    (!string.IsNullOrWhiteSpace(profile.SafeRepositoryAlias) &&
                     string.Equals(profile.SafeRepositoryAlias.Trim(), repositoryId?.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    AddAlias(aliases, profile.RepositoryHash);
                }
            }

            return aliases;
        }

        private static void AddAlias(HashSet<string> aliases, string value)
        {
            if (aliases == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            aliases.Add(value.Trim());
        }

        private static AxisScores AxesFromCharacterStats(CharacterStats stats)
        {
            stats = stats ?? CharacterStats.Zero();
            return new AxisScores
            {
                Code = Math.Max(0, stats.Logic + stats.Architecture + stats.Velocity),
                Focus = Math.Max(0, stats.Efficiency + stats.Stability),
                Debug = Math.Max(0, stats.Debug),
                Design = Math.Max(0, stats.Design + stats.Creativity),
                Sync = Math.Max(0, stats.Sync)
            };
        }

        private static AxisScores AxesFromTimeline(RepositoryTimelineEvent timelineEvent)
        {
            if (timelineEvent == null)
            {
                return AxisScores.Zero;
            }

            return new AxisScores
            {
                Code = Math.Max(0, timelineEvent.CodeDelta),
                Focus = Math.Max(0, timelineEvent.FocusDelta),
                Debug = Math.Max(0, timelineEvent.DebugDelta),
                Design = Math.Max(0, timelineEvent.DesignDelta),
                Sync = Math.Max(0, timelineEvent.SyncDelta)
            };
        }

        private static void AddTimelineSyncSignal(RepositoryGrowthSummary summary, RepositoryTimelineEvent timelineEvent, bool weekly)
        {
            if (!IsSyncSignal(timelineEvent))
            {
                return;
            }

            // Events that already carry an explicit Sync axis delta have that value counted via
            // AxesFromTimeline. Adding the heuristic +1 on top would double-count: e.g. a
            // growth_saved event with SyncDelta=4 whose summary mentions "sync" would read 5.
            // The heuristic is only meant to recover Sync for legacy events that lack an explicit
            // SyncDelta (e.g. safe_sync_completed events whose axis delta maps Stability -> Focus).
            if (Math.Max(0, timelineEvent?.SyncDelta ?? 0) > 0)
            {
                return;
            }

            AddAxes(summary, new AxisScores { Sync = 1 }, weekly);
        }

        private static void AddAxes(RepositoryGrowthSummary summary, AxisScores axes, bool weekly)
        {
            summary.Code += axes.Code;
            summary.Focus += axes.Focus;
            summary.Debug += axes.Debug;
            summary.Design += axes.Design;
            summary.Sync += axes.Sync;
            if (!weekly)
            {
                return;
            }

            summary.WeeklyCode += axes.Code;
            summary.WeeklyFocus += axes.Focus;
            summary.WeeklyDebug += axes.Debug;
            summary.WeeklyDesign += axes.Design;
            summary.WeeklySync += axes.Sync;
        }

        private static void AddXp(RepositoryGrowthSummary summary, int xp, bool weekly)
        {
            var delta = Math.Max(0, xp);
            summary.TotalXp += delta;
            if (weekly)
            {
                summary.WeeklyXp += delta;
            }
        }

        private static bool HasAnyStat(CharacterStats stats)
        {
            return stats != null &&
                   (Math.Max(0, stats.Logic) +
                    Math.Max(0, stats.Architecture) +
                    Math.Max(0, stats.Velocity) +
                    Math.Max(0, stats.Efficiency) +
                    Math.Max(0, stats.Stability) +
                    Math.Max(0, stats.Debug) +
                    Math.Max(0, stats.Design) +
                    Math.Max(0, stats.Creativity)) > 0;
        }

        private static bool HasTimelineAxisDelta(RepositoryTimelineEvent item)
        {
            return item != null &&
                   Math.Max(0, item.CodeDelta) +
                   Math.Max(0, item.FocusDelta) +
                   Math.Max(0, item.DebugDelta) +
                   Math.Max(0, item.DesignDelta) +
                   Math.Max(0, item.SyncDelta) > 0;
        }

        private static bool IsSavedNativeRun(NativeAnalysisRunRecord run)
        {
            return run != null &&
                   (string.Equals(run.Status, "saved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(run.SourceKind, "reviewSaved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(run.SourceKind, "levelUp", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsSavedGrowthTimelineEvent(RepositoryTimelineEvent item)
        {
            return item != null &&
                   (Math.Max(0, item.DeltaXp) > 0 ||
                    string.Equals(item.EventType, "xp_applied", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.EventType, "growth_saved", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(item.EventType, "level_up", StringComparison.OrdinalIgnoreCase));
        }

        private static bool ShouldCountTimelineXp(RepositoryTimelineEvent item, bool hasCanonicalGrowthSavedTimelineEvent)
        {
            if (item == null || Math.Max(0, item.DeltaXp) <= 0)
            {
                return false;
            }

            if (hasCanonicalGrowthSavedTimelineEvent &&
                string.Equals(item.EventType, "xp_applied", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return IsSavedGrowthTimelineEvent(item);
        }

        private static bool IsSyncSignal(RepositoryTimelineEvent item)
        {
            var text = ((item?.EventType ?? string.Empty) + " " + (item?.Title ?? string.Empty) + " " + (item?.Summary ?? string.Empty) + " " + (item?.TimelineSource ?? string.Empty)).ToLowerInvariant();
            return text.Contains("sync") || text.Contains("push") || text.Contains("pull") || text.Contains("merge") || text.Contains("conflict") || text.Contains("remote");
        }

        private static int SumTimelineXp(IEnumerable<RepositoryTimelineEvent> events, Func<RepositoryTimelineEvent, bool> predicate)
        {
            return (events ?? Enumerable.Empty<RepositoryTimelineEvent>())
                .Where(item => item != null && (predicate == null || predicate(item)))
                .Sum(item => Math.Max(0, item.DeltaXp));
        }

        private static int SumGrowthXp(IEnumerable<CharacterGrowthResult> records, Func<CharacterGrowthResult, bool> predicate)
        {
            return (records ?? Enumerable.Empty<CharacterGrowthResult>())
                .Where(item => item != null && (predicate == null || predicate(item)))
                .Sum(item => Math.Max(0, item.ExpGained));
        }

        private static int SumNativeRunXp(IEnumerable<NativeAnalysisRunRecord> records, Func<NativeAnalysisRunRecord, bool> predicate)
        {
            return (records ?? Enumerable.Empty<NativeAnalysisRunRecord>())
                .Where(item => item != null && (predicate == null || predicate(item)))
                .Sum(item => Math.Max(0, item.XpDelta));
        }

        private static int SumActivityReviewXp(IEnumerable<ActivityReview> records, Func<ActivityReview, bool> predicate)
        {
            return (records ?? Enumerable.Empty<ActivityReview>())
                .Where(item => item != null && (predicate == null || predicate(item)))
                .Sum(item => Math.Max(0, item.XpDelta));
        }

        private static string LatestSummary(
            List<RepositoryTimelineEvent> timelineEvents,
            List<CharacterGrowthResult> growthRecords,
            List<NativeAnalysisRunRecord> savedNativeRuns,
            List<ActivityReview> savedActivityReviews,
            List<AgentWorkSession> sessions,
            RepositoryGrowthSummary summary)
        {
            var latestEvent = timelineEvents.FirstOrDefault();
            var suffix = summary.HasLegacyAxisGap
                ? " · Legacy event lacks axis delta"
                : string.Empty;
            if (latestEvent != null)
            {
                var title = string.IsNullOrWhiteSpace(latestEvent.Title) ? latestEvent.EventType : latestEvent.Title;
                var eventSummary = string.IsNullOrWhiteSpace(latestEvent.Summary) ? "Repository activity recorded." : latestEvent.Summary;
                return title + " · " + eventSummary + suffix;
            }

            var latestGrowth = growthRecords.OrderByDescending(growth => growth.SessionId).FirstOrDefault();
            if (latestGrowth != null)
            {
                return "+" + Math.Max(0, latestGrowth.ExpGained) + " XP | Level " + latestGrowth.LevelBefore + " -> " + latestGrowth.LevelAfter + suffix;
            }

            var latestRun = savedNativeRuns.FirstOrDefault();
            if (latestRun != null)
            {
                return (string.IsNullOrWhiteSpace(latestRun.SafeSummary) ? "Saved native activity." : latestRun.SafeSummary) + suffix;
            }

            var latestReview = savedActivityReviews.FirstOrDefault();
            if (latestReview != null)
            {
                return (string.IsNullOrWhiteSpace(latestReview.EvidenceSummary) ? "Saved activity review." : latestReview.EvidenceSummary) + suffix;
            }

            var latestSession = sessions.OrderByDescending(session => session.EndedAt).FirstOrDefault();
            return latestSession == null ? "No growth recorded yet." : "Repository activity recorded on " + latestSession.EndedAt.UtcDateTime.ToString("yyyy-MM-dd") + suffix;
        }

        private struct AxisScores
        {
            public int Code;
            public int Focus;
            public int Debug;
            public int Design;
            public int Sync;

            public bool HasAny
            {
                get { return Code > 0 || Focus > 0 || Debug > 0 || Design > 0 || Sync > 0; }
            }

            public static AxisScores Zero
            {
                get { return new AxisScores(); }
            }
        }
    }
}
