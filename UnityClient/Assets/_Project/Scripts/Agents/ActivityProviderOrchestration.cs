using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Agents
{
    public sealed class ActivityProviderSelection
    {
        public string ProviderId { get; set; } = string.Empty;

        // Memory-only. Never copy this value into provider results, logs, save data, or sync DTOs.
        public string SelectedLocationPath { get; set; } = string.Empty;
        public bool UserApproved { get; set; }
        public AgentProviderType AgentProviderHint { get; set; } = AgentProviderType.Unknown;
        public int AnalysisWindowDays { get; set; } = AgentAnalysisInput.DefaultAnalysisWindowDays;
        public int MaxFilesToScan { get; set; } = AgentAnalysisInput.DefaultMaxFilesToScan;
        public int MaxLogEntriesToScan { get; set; } = AgentAnalysisInput.DefaultMaxLogEntriesToScan;
    }

    public sealed class ActivityProviderPollRequest
    {
        public bool UserApproved { get; set; }
        public List<ActivityProviderSelection> ApprovedSelections { get; set; } = new List<ActivityProviderSelection>();
    }

    public sealed class ActivityProviderResult
    {
        public string ProviderId { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public bool RequiresReview { get; set; } = true;
        public AgentWorkSession Session { get; set; }
        public string ErrorCategory { get; set; } = string.Empty;
        public List<string> WarningIds { get; set; } = new List<string>();

        public static ActivityProviderResult Success(string providerId, AgentWorkSession session)
        {
            return new ActivityProviderResult
            {
                ProviderId = providerId,
                IsSuccess = true,
                RequiresReview = true,
                Session = session,
                WarningIds = session?.Warnings ?? new List<string>()
            };
        }

        public static ActivityProviderResult Failure(string providerId, string errorCategory)
        {
            return new ActivityProviderResult
            {
                ProviderId = providerId,
                IsSuccess = false,
                ErrorCategory = string.IsNullOrWhiteSpace(errorCategory) ? "provider_failed" : errorCategory
            };
        }
    }

    public interface IActivityProvider
    {
        string ProviderId { get; }
        Task<ActivityProviderResult> PollAsync(ActivityProviderSelection selection, CancellationToken cancellationToken);
    }

    public sealed class ActivityProviderPoller
    {
        private readonly List<IActivityProvider> providers;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly IAgentAnalysisLogger logger;

        public ActivityProviderPoller(IEnumerable<IActivityProvider> providers, PrivacySanitizer privacySanitizer = null, IAgentAnalysisLogger logger = null)
        {
            this.providers = (providers ?? Enumerable.Empty<IActivityProvider>()).ToList();
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.logger = logger;
        }

        public async Task<List<ActivityProviderResult>> PollAsync(ActivityProviderPollRequest request, CancellationToken cancellationToken)
        {
            request = request ?? new ActivityProviderPollRequest();
            var results = new List<ActivityProviderResult>();
            if (!request.UserApproved)
            {
                logger?.Warning("Activity provider poll skipped category=user_approval_required");
                results.Add(ActivityProviderResult.Failure("ALL", "user_approval_required"));
                return results;
            }

            foreach (var selection in request.ApprovedSelections ?? new List<ActivityProviderSelection>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (selection == null || !selection.UserApproved)
                {
                    results.Add(ActivityProviderResult.Failure(selection?.ProviderId ?? "UNKNOWN", "provider_location_not_approved"));
                    continue;
                }

                var provider = providers.FirstOrDefault(item => string.Equals(item.ProviderId, selection.ProviderId, StringComparison.Ordinal));
                if (provider == null)
                {
                    results.Add(ActivityProviderResult.Failure(selection.ProviderId, "provider_not_registered"));
                    continue;
                }

                try
                {
                    logger?.Info("Activity provider poll started provider=" + provider.ProviderId);
                    var result = await provider.PollAsync(selection, cancellationToken);
                    if (result?.Session != null)
                    {
                        var validation = privacySanitizer.ValidateSafeSession(result.Session);
                        if (!validation.IsSuccess)
                        {
                            results.Add(ActivityProviderResult.Failure(provider.ProviderId, validation.ErrorCode));
                            continue;
                        }
                    }

                    results.Add(result ?? ActivityProviderResult.Failure(provider.ProviderId, "provider_empty_result"));
                    logger?.Info("Activity provider poll completed provider=" + provider.ProviderId);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    logger?.Warning("Activity provider poll failed category=provider_exception");
                    results.Add(ActivityProviderResult.Failure(provider.ProviderId, "provider_exception"));
                }
            }

            return results;
        }
    }

    public sealed class ActivityAnalysisCoordinator
    {
        private readonly ActivityProviderPoller poller;

        public ActivityAnalysisCoordinator(ActivityProviderPoller poller)
        {
            this.poller = poller ?? throw new ArgumentNullException(nameof(poller));
        }

        public Task<List<ActivityProviderResult>> AnalyzeApprovedAsync(IEnumerable<ActivityProviderSelection> selections, CancellationToken cancellationToken)
        {
            return poller.PollAsync(new ActivityProviderPollRequest
            {
                UserApproved = true,
                ApprovedSelections = (selections ?? Enumerable.Empty<ActivityProviderSelection>()).ToList()
            }, cancellationToken);
        }
    }

    public sealed class AgentLogActivityProviderAdapter : IActivityProvider
    {
        private readonly AgentLogActivityProvider provider;

        public AgentLogActivityProviderAdapter(AgentLogActivityProvider provider = null)
        {
            this.provider = provider ?? new AgentLogActivityProvider();
        }

        public string ProviderId => AgentLogActivityProvider.SourceProviderId;

        public async Task<ActivityProviderResult> PollAsync(ActivityProviderSelection selection, CancellationToken cancellationToken)
        {
            if (selection == null || !selection.UserApproved)
            {
                return ActivityProviderResult.Failure(ProviderId, "provider_location_not_approved");
            }

            var result = await provider.AnalyzeSessionAsync(new AgentAnalysisInput
            {
                SelectedLocationPath = selection.SelectedLocationPath,
                ProviderHint = selection.AgentProviderHint,
                AnalysisWindowDays = selection.AnalysisWindowDays,
                MaxFilesToScan = selection.MaxFilesToScan,
                MaxLogEntriesToScan = selection.MaxLogEntriesToScan
            }, cancellationToken);

            return result.IsSuccess
                ? ActivityProviderResult.Success(ProviderId, result.Value)
                : ActivityProviderResult.Failure(ProviderId, result.ErrorCode);
        }
    }

    public sealed class GitActivityProviderAdapter : IActivityProvider
    {
        public const string ProviderIdValue = "GIT";

        private readonly GitAggregateAnalyzer analyzer;
        private readonly PrivacySanitizer privacySanitizer;

        public GitActivityProviderAdapter(GitAggregateAnalyzer analyzer = null, PrivacySanitizer privacySanitizer = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.analyzer = analyzer ?? new GitAggregateAnalyzer(null, this.privacySanitizer);
        }

        public string ProviderId => ProviderIdValue;

        public async Task<ActivityProviderResult> PollAsync(ActivityProviderSelection selection, CancellationToken cancellationToken)
        {
            if (selection == null || !selection.UserApproved)
            {
                return ActivityProviderResult.Failure(ProviderId, "provider_location_not_approved");
            }

            var result = await analyzer.AnalyzeAsync(new GitRepositoryAnalysisInput
            {
                RepositoryRootPath = selection.SelectedLocationPath,
                AnalysisWindowDays = selection.AnalysisWindowDays,
                IncludeUncommittedChanges = true,
                IncludeRecentCommits = true,
                MaxCommitsToInspect = GitRepositoryAnalysisInput.DefaultMaxCommitsToInspect
            }, cancellationToken);

            if (!result.IsSuccess)
            {
                return ActivityProviderResult.Failure(ProviderId, result.ErrorCode);
            }

            var session = GitAnalysisSessionProvider.CreateSession(result.Value);
            var validation = privacySanitizer.ValidateSafeSession(session);
            return validation.IsSuccess
                ? ActivityProviderResult.Success(ProviderId, session)
                : ActivityProviderResult.Failure(ProviderId, validation.ErrorCode);
        }
    }
}
