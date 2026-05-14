using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Git
{
    public sealed class GitAggregateAnalyzer
    {
        public const string Version = "git-aggregate-v1";
        private const string CommitBoundary = "--TOKENFORGE-COMMIT--";

        private readonly IGitCommandRunner commandRunner;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly IGitAnalysisLogger logger;

        public GitAggregateAnalyzer(
            IGitCommandRunner commandRunner = null,
            PrivacySanitizer privacySanitizer = null,
            IGitAnalysisLogger logger = null)
        {
            this.commandRunner = commandRunner ?? new SystemGitCommandRunner(TimeSpan.FromSeconds(5));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.logger = logger;
        }

        public async Task<Result<GitChangeSummary>> AnalyzeAsync(GitRepositoryAnalysisInput input, CancellationToken cancellationToken = default)
        {
            var validation = ValidateInput(input, out var canonicalRootPath);
            if (!validation.IsSuccess)
            {
                logger?.Warning("Git analysis failed category=input_validation");
                return Result<GitChangeSummary>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            logger?.Info("Git analysis started");

            var repositoryCheck = await commandRunner.RunAsync(canonicalRootPath, "rev-parse --is-inside-work-tree", cancellationToken);
            if (!repositoryCheck.IsSuccess || !repositoryCheck.Output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                logger?.Warning("Git analysis failed category=not_repository");
                return Result<GitChangeSummary>.Failure("not_git_repository", "Selected folder is not a Git repository.");
            }

            var aggregate = new AggregateState
            {
                ProjectPathHash = privacySanitizer.HashString(canonicalRootPath),
                AnalysisWindowDays = input.ClampedAnalysisWindowDays()
            };

            if (input.IncludeUncommittedChanges)
            {
                var statusResult = await RunRequiredAsync(canonicalRootPath, "status --porcelain", cancellationToken);
                if (!statusResult.IsSuccess) return Failure(statusResult, "status");

                ParseStatus(statusResult.Output, aggregate);

                var diffResult = await RunRequiredAsync(canonicalRootPath, "diff --numstat", cancellationToken);
                if (!diffResult.IsSuccess) return Failure(diffResult, "diff");
                ParseNumstat(diffResult.Output, aggregate, aggregate.StatusFileCount <= 0);

                var cachedDiffResult = await RunRequiredAsync(canonicalRootPath, "diff --cached --numstat", cancellationToken);
                if (!cachedDiffResult.IsSuccess) return Failure(cachedDiffResult, "cached_diff");
                ParseNumstat(cachedDiffResult.Output, aggregate, aggregate.StatusFileCount <= 0);
            }

            if (input.IncludeRecentCommits)
            {
                var maxCommits = input.ClampedMaxCommitsToInspect();
                var windowDays = input.ClampedAnalysisWindowDays();
                var logArguments = $"log --since={windowDays}.days.ago --numstat --format={CommitBoundary} -n {maxCommits}";
                var logResult = await RunRequiredAsync(canonicalRootPath, logArguments, cancellationToken);
                if (!logResult.IsSuccess) return Failure(logResult, "log");
                ParseLogNumstat(logResult.Output, aggregate, maxCommits);
            }

            var summary = aggregate.ToSummary();
            var privacyValidation = privacySanitizer.ValidateSafeSession(new AgentWorkSession { GitChangeSummary = summary, SourceProvider = "GIT" });
            if (!privacyValidation.IsSuccess)
            {
                logger?.Warning("Git analysis failed category=privacy_validation");
                return Result<GitChangeSummary>.Failure(privacyValidation.ErrorCode, privacyValidation.ErrorMessage);
            }

            logger?.Info($"Git analysis completed changed_file_bucket={summary.ChangedFileCountBucket} added_line_bucket={summary.AddedLineBucket} warning_count={summary.PrivacyWarnings.Count}");
            return Result<GitChangeSummary>.Success(summary);
        }

        public static CountBucket ToCountBucket(int count)
        {
            if (count <= 0) return CountBucket.None;
            if (count == 1) return CountBucket.One;
            if (count <= 5) return CountBucket.Small;
            if (count <= 20) return CountBucket.Medium;
            if (count <= 50) return CountBucket.Large;
            return CountBucket.Huge;
        }

        private static Result ValidateInput(GitRepositoryAnalysisInput input, out string canonicalRootPath)
        {
            canonicalRootPath = string.Empty;
            if (input == null || string.IsNullOrWhiteSpace(input.RepositoryRootPath))
            {
                return Result.Failure("missing_repository_path", "Repository folder is required.");
            }

            try
            {
                canonicalRootPath = Path.GetFullPath(input.RepositoryRootPath);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return Result.Failure("invalid_repository_path", "Repository folder is invalid.");
            }

            if (!Directory.Exists(canonicalRootPath))
            {
                canonicalRootPath = string.Empty;
                return Result.Failure("invalid_repository_path", "Repository folder is invalid.");
            }

            return Result.Success();
        }

        private async Task<GitCommandResult> RunRequiredAsync(string repositoryRootPath, string arguments, CancellationToken cancellationToken)
        {
            var result = await commandRunner.RunAsync(repositoryRootPath, arguments, cancellationToken);
            if (!result.IsSuccess)
            {
                logger?.Warning("Git analysis failed category=git_command");
            }

            return result;
        }

        private static Result<GitChangeSummary> Failure(GitCommandResult result, string category)
        {
            var failure = Result<GitChangeSummary>.Failure(result.ErrorCode, result.ErrorMessage);
            failure.Warnings.Add($"git_{category}_failed");
            return failure;
        }

        private static void ParseStatus(string output, AggregateState aggregate)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Length < 3)
                {
                    continue;
                }

                var code = line.Substring(0, 2);
                var path = ExtractStatusPath(line.Substring(3));
                aggregate.StatusFileCount++;
                aggregate.ChangedFileCount++;
                aggregate.HasUncommittedChanges = true;
                aggregate.AddPathCategory(path);
                aggregate.AddFileKind(code);
            }
        }

        private static string ExtractStatusPath(string path)
        {
            var trimmed = (path ?? string.Empty).Trim();
            var renameMarker = trimmed.IndexOf(" -> ", StringComparison.Ordinal);
            return renameMarker >= 0 ? trimmed.Substring(renameMarker + 4) : trimmed;
        }

        private static void ParseNumstat(string output, AggregateState aggregate, bool countFiles)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ParseNumstatLine(line, aggregate, countFiles);
            }
        }

        private static void ParseLogNumstat(string output, AggregateState aggregate, int maxCommits)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line == CommitBoundary)
                {
                    if (aggregate.CommitCount < maxCommits)
                    {
                        aggregate.CommitCount++;
                    }

                    continue;
                }

                ParseNumstatLine(line, aggregate, true);
            }
        }

        private static void ParseNumstatLine(string line, AggregateState aggregate, bool countFiles)
        {
            var parts = (line ?? string.Empty).Split('\t');
            if (parts.Length < 3)
            {
                return;
            }

            var binary = parts[0] == "-" || parts[1] == "-";
            aggregate.AddedLines += ParseNonNegative(parts[0]);
            aggregate.DeletedLines += ParseNonNegative(parts[1]);
            if (binary)
            {
                aggregate.BinaryFileCount++;
            }

            if (countFiles)
            {
                aggregate.ChangedFileCount++;
                aggregate.ModifiedFileCount++;
                aggregate.AddPathCategory(parts[2]);
            }
        }

        private static int ParseNonNegative(string raw)
        {
            return int.TryParse(raw, out var value) ? Math.Max(0, value) : 0;
        }

        private sealed class AggregateState
        {
            public string ProjectPathHash { get; set; } = string.Empty;
            public int AnalysisWindowDays { get; set; }
            public int StatusFileCount { get; set; }
            public int ChangedFileCount { get; set; }
            public int AddedLines { get; set; }
            public int DeletedLines { get; set; }
            public int ModifiedFileCount { get; set; }
            public int CreatedFileCount { get; set; }
            public int DeletedFileCount { get; set; }
            public int RenamedFileCount { get; set; }
            public int BinaryFileCount { get; set; }
            public int CommitCount { get; set; }
            public bool HasUncommittedChanges { get; set; }
            public Dictionary<FileCategory, int> FileCategories { get; } = new Dictionary<FileCategory, int>();
            public Dictionary<string, int> ExtensionCategories { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            public void AddPathCategory(string repositoryRelativePath)
            {
                Increment(FileCategories, GitPathClassifier.Classify(repositoryRelativePath));
                Increment(ExtensionCategories, GitExtensionCategoryClassifier.Classify(repositoryRelativePath));
            }

            public void AddFileKind(string statusCode)
            {
                var normalized = statusCode ?? string.Empty;
                if (normalized.Contains("R"))
                {
                    RenamedFileCount++;
                }
                else if (normalized.Contains("D"))
                {
                    DeletedFileCount++;
                }
                else if (normalized.Contains("A") || normalized.Contains("?"))
                {
                    CreatedFileCount++;
                }
                else
                {
                    ModifiedFileCount++;
                }
            }

            public GitChangeSummary ToSummary()
            {
                var confidence = CalculateConfidence();
                var warnings = new List<string>();
                if (confidence == ConfidenceLevel.Low)
                {
                    warnings.Add("git_low_confidence");
                }

                if (AnalysisWindowDays >= GitRepositoryAnalysisInput.MaxAnalysisWindowDays)
                {
                    warnings.Add("git_window_capped");
                }

                return new GitChangeSummary
                {
                    ChangedFileCount = Math.Min(ChangedFileCount, 500),
                    ChangedFileCountBucket = ToCountBucket(ChangedFileCount),
                    AddedLineBucket = GitNumstatParser.ToBucket(AddedLines),
                    DeletedLineBucket = GitNumstatParser.ToBucket(DeletedLines),
                    ModifiedFileCountBucket = ToCountBucket(ModifiedFileCount),
                    CreatedFileCountBucket = ToCountBucket(CreatedFileCount),
                    DeletedFileCountBucket = ToCountBucket(DeletedFileCount),
                    RenamedFileCountBucket = ToCountBucket(RenamedFileCount),
                    BinaryFileCountBucket = ToCountBucket(BinaryFileCount),
                    CommitCountBucket = ToCountBucket(CommitCount),
                    TestFileChanged = FileCategories.ContainsKey(FileCategory.Test) || ExtensionCategories.ContainsKey("test"),
                    DocsFileChanged = FileCategories.ContainsKey(FileCategory.Docs) || ExtensionCategories.ContainsKey("markdown"),
                    UiFileChanged = FileCategories.ContainsKey(FileCategory.UI),
                    ArchitectureFileChanged = FileCategories.ContainsKey(FileCategory.Architecture),
                    FileCategoryCounts = FileCategories
                        .Select(kvp => new FileCategoryCount { Category = kvp.Key, Count = Math.Min(kvp.Value, 500) })
                        .OrderBy(item => item.Category.ToString(), StringComparer.Ordinal)
                        .ToList(),
                    ExtensionCategoryBuckets = ExtensionCategories
                        .Select(kvp => new ExtensionCategoryCount
                        {
                            Category = kvp.Key,
                            Count = Math.Min(kvp.Value, 500),
                            CountBucket = ToCountBucket(kvp.Value)
                        })
                        .OrderBy(item => item.Category, StringComparer.Ordinal)
                        .ToList(),
                    WorkingTreeStatus = HasUncommittedChanges ? WorkingTreeStatus.HasChanges : WorkingTreeStatus.Clean,
                    ProjectPathHash = ProjectPathHash,
                    AnalysisTimeBucket = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                    HasUncommittedChanges = HasUncommittedChanges,
                    AnalysisWindowDays = AnalysisWindowDays,
                    ConfidenceLevel = confidence,
                    AnalyzerVersion = Version,
                    SafeSessionAlias = "Git aggregate session",
                    PrivacyWarnings = warnings
                };
            }

            private ConfidenceLevel CalculateConfidence()
            {
                if (ChangedFileCount <= 0 && CommitCount <= 0)
                {
                    return ConfidenceLevel.Low;
                }

                if (FileCategories.Count >= 2 || CommitCount > 0)
                {
                    return ConfidenceLevel.High;
                }

                return ConfidenceLevel.Medium;
            }

            private static void Increment<TKey>(Dictionary<TKey, int> dictionary, TKey key)
            {
                if (!dictionary.ContainsKey(key))
                {
                    dictionary[key] = 0;
                }

                dictionary[key]++;
            }
        }
    }
}
