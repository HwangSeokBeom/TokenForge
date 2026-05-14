using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.UI
{
    public sealed class AgentAnalysisSettings
    {
        public int AnalysisWindowDays { get; set; } = AgentAnalysisInput.DefaultAnalysisWindowDays;
        public int MaxFilesToScan { get; set; } = AgentAnalysisInput.DefaultMaxFilesToScan;
        public int MaxLogEntriesToScan { get; set; } = AgentAnalysisInput.DefaultMaxLogEntriesToScan;

        public AgentAnalysisInput ToInput(string selectedLocationPath, AgentProviderType providerType)
        {
            return new AgentAnalysisInput
            {
                SelectedLocationPath = selectedLocationPath ?? string.Empty,
                ProviderHint = providerType,
                AnalysisWindowDays = AnalysisWindowDays,
                MaxFilesToScan = MaxFilesToScan,
                MaxLogEntriesToScan = MaxLogEntriesToScan
            };
        }
    }

    public sealed class RecentSafeSessionSummary
    {
        public string SourceProvider { get; set; } = string.Empty;
        public string DayBucket { get; set; } = string.Empty;
        public WorkType WorkType { get; set; } = WorkType.Unknown;
        public ProviderConfidence Confidence { get; set; } = ProviderConfidence.Unknown;
        public CountBucket GitChangeCountBucket { get; set; } = CountBucket.Unknown;
        public LineChangeBucket GitAddedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public LineChangeBucket GitDeletedLineBucket { get; set; } = LineChangeBucket.Unknown;
        public AgentProviderType AgentProviderType { get; set; } = AgentProviderType.Unknown;
        public CountBucket AgentSessionCountBucket { get; set; } = CountBucket.Unknown;
        public CountBucket AgentInteractionCountBucket { get; set; } = CountBucket.Unknown;
        public List<string> WarningIds { get; set; } = new List<string>();
    }

    public sealed class ApprovedLocationDisplayItem
    {
        public string LocalId { get; set; } = string.Empty;
        public string DisplayAlias { get; set; } = string.Empty;
        public ApprovedLocationSourceType SourceType { get; set; } = ApprovedLocationSourceType.UnknownAuto;
        public bool Enabled { get; set; } = true;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public sealed class ApprovedActivityAnalysisViewModel
    {
        public const string SafeAggregateNotice = "Only safe aggregate data will be saved.";
        public const string RawDataNotice = "Raw paths, prompts, responses, commands, filenames, repository names, and source code are not saved or synced.";
        public const string ApprovedLocationsNotice = "Approved locations are stored only on this device and are never synced.";

        private const int RecentSessionLimit = 8;

        private readonly IAgentLogLocationPicker agentLogLocationPicker;
        private readonly ILocalSaveDataRepository repository;
        private readonly IApprovedLocationSettingsRepository approvedLocationRepository;
        private readonly PrivacySanitizer privacySanitizer;

        public ApprovedActivityAnalysisViewModel(
            GitAnalysisFlowController gitFlow,
            AgentAnalysisFlowController agentFlow,
            IAgentLogLocationPicker agentLogLocationPicker,
            ILocalSaveDataRepository repository,
            PrivacySanitizer privacySanitizer = null,
            IApprovedLocationSettingsRepository approvedLocationRepository = null)
        {
            GitFlow = gitFlow ?? throw new ArgumentNullException(nameof(gitFlow));
            AgentFlow = agentFlow ?? throw new ArgumentNullException(nameof(agentFlow));
            this.agentLogLocationPicker = agentLogLocationPicker ?? throw new ArgumentNullException(nameof(agentLogLocationPicker));
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.approvedLocationRepository = approvedLocationRepository ?? new ApprovedLocationSettingsRepository();
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
        }

        public GitAnalysisFlowController GitFlow { get; }
        public AgentAnalysisFlowController AgentFlow { get; }
        public AgentAnalysisSettings AgentSettings { get; } = new AgentAnalysisSettings();
        public AgentProviderType SelectedAgentProviderType { get; set; } = AgentProviderType.Unknown;
        public string AgentSelectionStatus { get; private set; } = "No agent log location selected";
        public string AgentPickerErrorCategory { get; private set; } = string.Empty;
        public List<RecentSafeSessionSummary> RecentSessions { get; private set; } = new List<RecentSafeSessionSummary>();
        public List<ApprovedLocationDisplayItem> ApprovedGitLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();
        public List<ApprovedLocationDisplayItem> ApprovedAgentLocations { get; private set; } = new List<ApprovedLocationDisplayItem>();

        public async Task<Result> SelectAgentLogLocationAsync(CancellationToken cancellationToken = default)
        {
            var pickResult = await agentLogLocationPicker.PickAgentLogLocationAsync(cancellationToken);
            if (!pickResult.IsSuccess)
            {
                AgentSelectionStatus = "No agent log location selected";
                AgentPickerErrorCategory = pickResult.IsCancelled ? string.Empty : pickResult.ErrorCode;
                if (pickResult.IsCancelled)
                {
                    return Result.Success();
                }

                return Result.Failure(
                    string.IsNullOrWhiteSpace(pickResult.ErrorCode) ? "agent_log_picker_failed" : pickResult.ErrorCode,
                    "Agent log location selection is unavailable.");
            }

            AgentPickerErrorCategory = string.Empty;
            var result = AgentFlow.SelectApprovedLogLocation(AgentSettings.ToInput(pickResult.AgentLogLocationPath, SelectedAgentProviderType));
            AgentSelectionStatus = result.IsSuccess ? "Agent log location selected" : "No agent log location selected";
            return result;
        }

        public async Task<Result<ApprovedLocationEntry>> AddCurrentGitSelectionToApprovedLocationsAsync(string displayAlias, CancellationToken cancellationToken = default)
        {
            var selectedPath = GitFlow.GetSelectedRepositoryPathForLocalOnlyApproval();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return Result<ApprovedLocationEntry>.Failure("missing_repository_selection", "Select a repository before adding it as an approved local location.");
            }

            var result = await approvedLocationRepository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = SafeLocalAlias(displayAlias, "Git repository"),
                SourceType = ApprovedLocationSourceType.Git,
                LocalPath = selectedPath,
                Enabled = true
            }, cancellationToken);

            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result<ApprovedLocationEntry>> AddCurrentAgentSelectionToApprovedLocationsAsync(string displayAlias, CancellationToken cancellationToken = default)
        {
            var selectedPath = AgentFlow.GetSelectedAgentLogLocationPathForLocalOnlyApproval();
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return Result<ApprovedLocationEntry>.Failure("missing_agent_log_location", "Select an agent log location before adding it as an approved local location.");
            }

            var result = await approvedLocationRepository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = SafeLocalAlias(displayAlias, "Agent logs"),
                SourceType = SelectedAgentProviderType.ToApprovedLocationSourceType(),
                LocalPath = selectedPath,
                Enabled = true
            }, cancellationToken);

            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result> SelectApprovedGitLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var entry = await FindApprovedLocationAsync(localId, ApprovedLocationSourceType.Git, cancellationToken);
            if (entry == null)
            {
                return Result.Failure("approved_location_not_found", "Approved Git location was not found.");
            }

            return GitFlow.SelectLocalOnlyApprovedRepositoryPath(entry.LocalPath);
        }

        public async Task<Result> SelectApprovedAgentLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            var entry = (settings.Locations ?? new List<ApprovedLocationEntry>())
                .FirstOrDefault(item => item.LocalId == localId && item.Enabled && item.SourceType != ApprovedLocationSourceType.Git);
            if (entry == null)
            {
                return Result.Failure("approved_location_not_found", "Approved agent log location was not found.");
            }

            SelectedAgentProviderType = entry.SourceType.ToAgentProviderType();
            var result = AgentFlow.SelectApprovedLogLocation(AgentSettings.ToInput(entry.LocalPath, SelectedAgentProviderType));
            AgentSelectionStatus = result.IsSuccess ? "Approved agent log location selected" : "No agent log location selected";
            return result;
        }

        public async Task<Result> DisableApprovedLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var result = await approvedLocationRepository.SetEnabledAsync(localId, false, cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result> RemoveApprovedLocationAsync(string localId, CancellationToken cancellationToken = default)
        {
            var result = await approvedLocationRepository.RemoveAsync(localId, cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshApprovedLocationsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result<AgentAnalysisReviewModel>> AnalyzeAgentActivityAsync(CancellationToken cancellationToken = default)
        {
            return await AgentFlow.AnalyzeAsync(cancellationToken);
        }

        public async Task<Result<SaveData>> SaveGitSessionAsync(CancellationToken cancellationToken = default)
        {
            var result = await GitFlow.SaveSessionAsync(cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshRecentSessionsAsync(cancellationToken);
            }

            return result;
        }

        public async Task<Result<SaveData>> SaveAgentSessionAsync(CancellationToken cancellationToken = default)
        {
            var result = await AgentFlow.SaveSessionAsync(cancellationToken);
            if (result.IsSuccess)
            {
                await RefreshRecentSessionsAsync(cancellationToken);
            }

            return result;
        }

        public Result DiscardGitReview()
        {
            return GitFlow.DiscardPendingReview();
        }

        public Result DiscardAgentReview()
        {
            AgentSelectionStatus = "No agent log location selected";
            AgentPickerErrorCategory = string.Empty;
            return AgentFlow.DiscardPendingReview();
        }

        public async Task<Result<List<RecentSafeSessionSummary>>> RefreshRecentSessionsAsync(CancellationToken cancellationToken = default)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var summaries = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .OrderByDescending(session => session.EndedAt)
                .Take(RecentSessionLimit)
                .Select(ToRecentSummary)
                .Where(summary => privacySanitizer.ValidateNoForbiddenFields(summary).IsSuccess)
                .ToList();

            RecentSessions = summaries;
            return Result<List<RecentSafeSessionSummary>>.Success(summaries);
        }

        public async Task<Result<List<ApprovedLocationDisplayItem>>> RefreshApprovedLocationsAsync(CancellationToken cancellationToken = default)
        {
            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            var safeDisplayItems = (settings.Locations ?? new List<ApprovedLocationEntry>())
                .OrderBy(item => item.SourceType)
                .ThenBy(item => item.DisplayAlias, StringComparer.Ordinal)
                .Select(ToDisplayItem)
                .Where(item => privacySanitizer.ValidateNoForbiddenFields(item).IsSuccess)
                .ToList();

            ApprovedGitLocations = safeDisplayItems
                .Where(item => item.SourceType == ApprovedLocationSourceType.Git)
                .ToList();
            ApprovedAgentLocations = safeDisplayItems
                .Where(item => item.SourceType != ApprovedLocationSourceType.Git)
                .ToList();

            return Result<List<ApprovedLocationDisplayItem>>.Success(safeDisplayItems);
        }

        public async Task RefreshDashboardAsync(CancellationToken cancellationToken = default)
        {
            await RefreshApprovedLocationsAsync(cancellationToken);
            await RefreshRecentSessionsAsync(cancellationToken);
        }

        private async Task<ApprovedLocationEntry> FindApprovedLocationAsync(string localId, ApprovedLocationSourceType sourceType, CancellationToken cancellationToken)
        {
            var settings = await approvedLocationRepository.LoadAsync(cancellationToken);
            return (settings.Locations ?? new List<ApprovedLocationEntry>())
                .FirstOrDefault(item => item.LocalId == localId && item.Enabled && item.SourceType == sourceType);
        }

        private static ApprovedLocationDisplayItem ToDisplayItem(ApprovedLocationEntry entry)
        {
            entry = entry ?? new ApprovedLocationEntry();
            return new ApprovedLocationDisplayItem
            {
                LocalId = entry.LocalId,
                DisplayAlias = entry.DisplayAlias,
                SourceType = entry.SourceType,
                Enabled = entry.Enabled,
                UpdatedAt = entry.UpdatedAt
            };
        }

        private static RecentSafeSessionSummary ToRecentSummary(AgentWorkSession session)
        {
            session = session ?? new AgentWorkSession();
            var gitSummary = session.GitChangeSummary ?? GitChangeSummary.Empty();
            var agentSummary = session.AgentActivitySummary ?? AgentActivitySummary.Empty();

            return new RecentSafeSessionSummary
            {
                SourceProvider = string.IsNullOrWhiteSpace(session.SourceProvider) ? "UNKNOWN" : session.SourceProvider,
                DayBucket = !string.IsNullOrWhiteSpace(agentSummary.DayBucket)
                    ? agentSummary.DayBucket
                    : (!string.IsNullOrWhiteSpace(gitSummary.AnalysisTimeBucket) ? gitSummary.AnalysisTimeBucket : session.EndedAt.UtcDateTime.ToString("yyyy-MM-dd")),
                WorkType = session.WorkType,
                Confidence = session.Confidence,
                GitChangeCountBucket = gitSummary.ChangedFileCountBucket,
                GitAddedLineBucket = gitSummary.AddedLineBucket,
                GitDeletedLineBucket = gitSummary.DeletedLineBucket,
                AgentProviderType = agentSummary.ProviderType,
                AgentSessionCountBucket = agentSummary.SessionCountBucket,
                AgentInteractionCountBucket = agentSummary.InteractionCountBucket,
                WarningIds = (session.Warnings ?? new List<string>()).OrderBy(item => item, StringComparer.Ordinal).Take(8).ToList()
            };
        }

        private static string SafeLocalAlias(string displayAlias, string fallback)
        {
            return string.IsNullOrWhiteSpace(displayAlias) ? fallback : displayAlias.Trim();
        }
    }
}
