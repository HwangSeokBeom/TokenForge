using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Git
{
    public sealed class GitDiffProvider : IAgentLogProvider
    {
        public const string Id = "GitDiffProvider";
        private readonly GitCommandRunner commandRunner;

        public GitDiffProvider(GitCommandRunner commandRunner = null)
        {
            this.commandRunner = commandRunner ?? new GitCommandRunner(TimeSpan.FromSeconds(5));
        }

        public string ProviderId => Id;
        public string ParserVersion => "git-diff-v1";

        public async Task<bool> IsAvailableAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            var result = await commandRunner.RunAsync(context.ProjectRootPath, "rev-parse --is-inside-work-tree", cancellationToken);
            return result.IsSuccess && result.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<AgentProviderResult> AnalyzeAsync(AgentProviderContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(context.ProjectRootPath))
            {
                return AgentProviderResult.Failure(ProviderId, "missing_project_path", "Project folder is required.");
            }

            var available = await IsAvailableAsync(context, cancellationToken);
            if (!available)
            {
                return AgentProviderResult.Failure(ProviderId, "not_git_repository", "Project folder is not a Git repository.");
            }

            var statusResult = await commandRunner.RunAsync(context.ProjectRootPath, "status --short", cancellationToken);
            if (!statusResult.IsSuccess)
            {
                return AgentProviderResult.Failure(ProviderId, statusResult.ErrorCode, statusResult.ErrorMessage);
            }

            var numstatResult = await commandRunner.RunAsync(context.ProjectRootPath, "diff --numstat", cancellationToken);
            if (!numstatResult.IsSuccess)
            {
                return AgentProviderResult.Failure(ProviderId, numstatResult.ErrorCode, numstatResult.ErrorMessage);
            }

            var projectHash = string.IsNullOrWhiteSpace(context.ProjectPathHash)
                ? SafeHashUtility.ComputeProjectPathHash(context.ProjectRootPath)
                : context.ProjectPathHash;

            var statusCategories = GitStatusParser.ParseCategories(statusResult.Value);
            var numstatEntries = GitNumstatParser.Parse(numstatResult.Value);
            var categoryCounts = BuildCategoryCounts(statusCategories, numstatEntries);
            var changedFileCount = Math.Max(statusCategories.Count, numstatEntries.Count);
            var addedLines = numstatEntries.Sum(entry => entry.AddedLines);
            var deletedLines = numstatEntries.Sum(entry => entry.DeletedLines);
            var workType = WorkTypeInferenceService.Infer(categoryCounts, changedFileCount);
            var confidence = WorkTypeInferenceService.ConfidenceFor(workType, categoryCounts, changedFileCount);

            var gitSummary = new GitChangeSummary
            {
                ChangedFileCount = changedFileCount,
                AddedLineApproximation = addedLines,
                DeletedLineApproximation = deletedLines,
                AddedLineBucket = GitNumstatParser.ToBucket(addedLines),
                DeletedLineBucket = GitNumstatParser.ToBucket(deletedLines),
                TestFileChanged = categoryCounts.ContainsKey(FileCategory.Test),
                DocsFileChanged = categoryCounts.ContainsKey(FileCategory.Docs),
                UiFileChanged = categoryCounts.ContainsKey(FileCategory.UI),
                ArchitectureFileChanged = categoryCounts.ContainsKey(FileCategory.Architecture),
                FileCategoryCounts = categoryCounts.Select(kvp => new FileCategoryCount { Category = kvp.Key, Count = kvp.Value }).ToList(),
                WorkingTreeStatus = changedFileCount > 0 ? WorkingTreeStatus.HasChanges : WorkingTreeStatus.Clean,
                ProjectPathHash = projectHash
            };

            var session = new AgentWorkSession
            {
                AgentType = AgentType.GitOnly,
                WorkType = workType,
                StartedAt = context.ScanStartedAt,
                EndedAt = context.ScanEndedAt,
                TokenUsageBucket = TokenUsageBucket.Unknown,
                ActionSummary = new AgentActionSummary
                {
                    FileEditCount = changedFileCount,
                    TestRunCount = gitSummary.TestFileChanged ? 1 : 0
                },
                GitChangeSummary = gitSummary,
                ResultStatus = ResultStatus.Unknown,
                SourceProvider = ProviderId,
                SourceProviders = { ProviderId },
                ParserVersion = ParserVersion,
                Confidence = confidence
            };
            session.DeduplicationKey = $"{projectHash}:{session.StartedAt:yyyyMMddHH}:{workType}:{changedFileCount}";

            if (changedFileCount == 0)
            {
                return AgentProviderResult.Partial(ProviderId, session, ProviderConfidence.Low, "git_clean", "Git repository has no working tree changes.");
            }

            return AgentProviderResult.Success(ProviderId, session, confidence);
        }

        private static Dictionary<FileCategory, int> BuildCategoryCounts(List<FileCategory> statusCategories, List<GitNumstatEntry> numstatEntries)
        {
            var categories = statusCategories.Count > 0
                ? statusCategories
                : numstatEntries.Select(entry => entry.Category).ToList();

            var counts = new Dictionary<FileCategory, int>();
            foreach (var category in categories)
            {
                if (!counts.ContainsKey(category))
                {
                    counts[category] = 0;
                }

                counts[category]++;
            }

            return counts;
        }
    }
}
