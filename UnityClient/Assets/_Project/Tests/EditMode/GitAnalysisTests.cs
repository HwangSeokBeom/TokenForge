using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class GitAnalysisTests
    {
        private const string RawPath = "/Users/alice/SecretRepo/Assets/private/UserSecret.cs";
        private const string RawCommitMessage = "Fix production auth using customer token";
        private const string RawSourceText = "public class Secret { string password = \"hunter2\"; }";
        private const string RawToken = "Bearer abc.def.secret";
        private const string RawAuthorization = "authorization: Bearer abc.def.secret";

        [Test]
        public void GitNumstatParser_ParsesBucketsAndCategories()
        {
            var entries = GitNumstatParser.Parse("10\t2\tAssets/_Project/Scripts/UI/FooView.cs\n-\t-\tAssets/_Project/Art/icon.png\n");

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual(10, entries[0].AddedLines);
            Assert.AreEqual(FileCategory.UI, entries[0].Category);
            Assert.IsTrue(entries[1].IsBinary);
            Assert.AreEqual(LineChangeBucket.Small, GitNumstatParser.ToBucket(12));
        }

        [Test]
        public void WorkTypeInference_TestHeavyChanges_ReturnsTest()
        {
            var counts = new Dictionary<FileCategory, int>
            {
                [FileCategory.Test] = 4,
                [FileCategory.Domain] = 1
            };

            var workType = WorkTypeInferenceService.Infer(counts, 5);

            Assert.AreEqual(WorkType.Test, workType);
        }

        [Test]
        public void GitAggregateAnalyzer_ValidFakeOutputCreatesSafeGitSession()
        {
            var directory = CreateTempDirectory();
            var repository = new SaveDataRepository(Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName()));
            var runner = FakeRunner.WithSafeAggregateOutput();
            var logger = new CapturingGitAnalysisLogger();
            var analyzer = new GitAggregateAnalyzer(runner, new PrivacySanitizer(), logger);
            var provider = new GitAnalysisSessionProvider(repository, analyzer, null, null, logger);

            var result = RunAsync(() => provider.AnalyzeAndSaveAsync(Input(directory), CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(1, result.Value.WorkSessionSummaries.Count);
            Assert.AreEqual("GIT", result.Value.WorkSessionSummaries[0].SourceProvider);
            Assert.AreEqual("Git aggregate session", result.Value.WorkSessionSummaries[0].GitChangeSummary.SafeSessionAlias);
            Assert.AreEqual(GitAggregateAnalyzer.Version, result.Value.WorkSessionSummaries[0].GitChangeSummary.AnalyzerVersion);
            Assert.Greater(result.Value.GrowthHistory[0].ExpGained, 0);
        }

        [Test]
        public void GitAggregateAnalyzer_RawFilePathsAreNotPersisted()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var json = File.ReadAllText(result.Repository.SaveFilePath);

            Assert.IsFalse(json.Contains(RawPath));
            Assert.IsFalse(json.Contains("SecretRepo"));
            Assert.IsFalse(json.Contains("UserSecret.cs"));
            Assert.IsFalse(json.Contains("/Users/alice"));
        }

        [Test]
        public void GitAggregateAnalyzer_RawCommitMessagesAreNotPersisted()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var json = File.ReadAllText(result.Repository.SaveFilePath);

            Assert.IsFalse(json.Contains(RawCommitMessage));
            Assert.IsFalse(json.Contains("customer token"));
        }

        [Test]
        public void GitAggregateAnalyzer_RawDiffAndSourceLookingTextAreNotPersisted()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var json = File.ReadAllText(result.Repository.SaveFilePath);

            Assert.IsFalse(json.Contains(RawSourceText));
            Assert.IsFalse(json.Contains("password"));
            Assert.IsFalse(json.Contains("public class Secret"));
        }

        [Test]
        public void GitAggregateAnalyzer_TokensAndAuthorizationStringsAreRemovedFromPersistenceAndSync()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var saveJson = File.ReadAllText(result.Repository.SaveFilePath);
            var payload = new SafeSyncMapper().ToPayload(result.SaveData);

            Assert.IsFalse(saveJson.Contains(RawToken));
            Assert.IsFalse(saveJson.Contains(RawAuthorization));
            Assert.IsFalse(ObjectContainsString(payload, RawToken));
            Assert.IsFalse(ObjectContainsString(payload, RawAuthorization));
        }

        [Test]
        public void GitAggregateAnalyzer_ExtensionCategoriesAreBucketedCorrectly()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var buckets = result.SaveData.WorkSessionSummaries[0].GitChangeSummary.ExtensionCategoryBuckets;

            AssertCategory(buckets, "csharp", CountBucket.One);
            AssertCategory(buckets, "markdown", CountBucket.Small);
            AssertCategory(buckets, "typescript", CountBucket.One);
            AssertCategory(buckets, "test", CountBucket.Small);
            AssertCategory(buckets, "config", CountBucket.One);
        }

        [Test]
        public void GitAggregateAnalyzer_LineCountsAreBucketedAndCapped()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var summary = result.SaveData.WorkSessionSummaries[0].GitChangeSummary;

            Assert.AreEqual(LineChangeBucket.Huge, summary.AddedLineBucket);
            Assert.AreEqual(LineChangeBucket.Huge, summary.DeletedLineBucket);
            Assert.LessOrEqual(summary.ChangedFileCount, 500);
        }

        [Test]
        public void GitAggregateAnalyzer_CommitCountsAreBucketedAndCapped()
        {
            var directory = CreateTempDirectory();
            var runner = FakeRunner.WithManyCommits(250);
            var analyzer = new GitAggregateAnalyzer(runner);

            var result = RunAsync(() => analyzer.AnalyzeAsync(new GitRepositoryAnalysisInput
            {
                RepositoryRootPath = directory,
                IncludeUncommittedChanges = false,
                IncludeRecentCommits = true,
                MaxCommitsToInspect = 1000,
                AnalysisWindowDays = 90
            }, CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(CountBucket.Huge, result.Value.CommitCountBucket);
            Assert.AreEqual(GitRepositoryAnalysisInput.MaxAnalysisWindowDays, result.Value.AnalysisWindowDays);
            Assert.Contains("git_window_capped", result.Value.PrivacyWarnings);
        }

        [Test]
        public void GitAggregateAnalyzer_InvalidRepoPathFailsSafely()
        {
            var runner = FakeRunner.WithSafeAggregateOutput();
            var analyzer = new GitAggregateAnalyzer(runner);

            var result = RunAsync(() => analyzer.AnalyzeAsync(new GitRepositoryAnalysisInput
            {
                RepositoryRootPath = Path.Combine(Path.GetTempPath(), "TokenForgeMissing", Path.GetRandomFileName())
            }, CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("RepositoryFolderNotFound", result.ErrorCode);
            Assert.AreEqual(0, runner.Commands.Count);
        }

        [Test]
        public void GitAnalysisSessionProvider_FailedGitCommandDoesNotCorruptExistingSaveData()
        {
            var projectDirectory = CreateTempDirectory();
            var saveDirectory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(saveDirectory);
            var existing = SaveData.CreateDefault();
            existing.CharacterProfile.TotalExp = 123;
            existing.WorkSessionSummaries.Add(new AgentWorkSession { SessionId = "safe-session", WorkType = WorkType.Feature });
            Assert.IsTrue(RunAsync(() => repository.SaveAsync(existing, CancellationToken.None)).IsSuccess);
            var before = File.ReadAllText(repository.SaveFilePath);
            var provider = new GitAnalysisSessionProvider(repository, new GitAggregateAnalyzer(FakeRunner.FailOn("status --porcelain")));

            var result = RunAsync(() => provider.AnalyzeAndSaveAsync(Input(projectDirectory), CancellationToken.None));
            var after = File.ReadAllText(repository.SaveFilePath);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(before, after);
            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));
            Assert.AreEqual(123, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
        }

        [Test]
        public void GitAggregateAnalyzer_GeneratedGitSessionPassesPrivacyValidation()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var session = result.SaveData.WorkSessionSummaries[0];

            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSession(session).IsSuccess);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(result.SaveData).IsSuccess);
        }

        [Test]
        public void GitAggregateAnalyzer_MapsToSafeSyncDtoWithGitSourceProvider()
        {
            var result = AnalyzeAndSaveMaliciousOutput();

            var payload = new SafeSyncMapper().ToPayload(result.SaveData);
            var dto = payload.SessionSummary.Sessions[0];

            Assert.AreEqual("GIT", dto.SourceProvider);
            Assert.AreEqual("GIT", dto.SourceProviders[0]);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(payload).IsSafe);
        }

        [Test]
        public void GitAggregateAnalyzer_LogsDoNotContainRawPathOutputMessageTokenAuthorizationDiffOrSource()
        {
            var result = AnalyzeAndSaveMaliciousOutput();
            var logs = string.Join("\n", result.Logger.Messages);

            Assert.IsFalse(logs.Contains(RawPath));
            Assert.IsFalse(logs.Contains("SecretRepo"));
            Assert.IsFalse(logs.Contains(RawCommitMessage));
            Assert.IsFalse(logs.Contains(RawToken));
            Assert.IsFalse(logs.Contains(RawAuthorization));
            Assert.IsFalse(logs.Contains(RawSourceText));
            Assert.IsFalse(logs.Contains("diff --numstat"));
        }

        private static AnalysisSaveResult AnalyzeAndSaveMaliciousOutput()
        {
            var projectDirectory = CreateTempDirectory();
            var saveDirectory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(saveDirectory);
            var logger = new CapturingGitAnalysisLogger();
            var analyzer = new GitAggregateAnalyzer(FakeRunner.WithMaliciousAggregateOutput(), new PrivacySanitizer(), logger);
            var provider = new GitAnalysisSessionProvider(repository, analyzer, null, null, logger);

            var result = RunAsync(() => provider.AnalyzeAndSaveAsync(Input(projectDirectory), CancellationToken.None));

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            return new AnalysisSaveResult(repository, result.Value, logger);
        }

        private static GitRepositoryAnalysisInput Input(string directory)
        {
            return new GitRepositoryAnalysisInput
            {
                RepositoryRootPath = directory,
                AnalysisWindowDays = 7,
                IncludeUncommittedChanges = true,
                IncludeRecentCommits = true,
                MaxCommitsToInspect = 50
            };
        }

        private static void AssertCategory(List<ExtensionCategoryCount> buckets, string category, CountBucket expectedBucket)
        {
            var bucket = buckets.FirstOrDefault(item => item.Category == category);
            Assert.IsNotNull(bucket, category);
            Assert.AreEqual(expectedBucket, bucket.CountBucket, category);
        }

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T RunAsync<T>(System.Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new HashSet<object>());
        }

        private static bool ObjectContainsString(object value, string expected, HashSet<object> visited)
        {
            if (value == null)
            {
                return false;
            }

            if (value is string text)
            {
                return text.Contains(expected);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(System.DateTimeOffset) || type == typeof(System.DateTime))
            {
                return false;
            }

            if (!visited.Add(value))
            {
                return false;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (ObjectContainsString(item, expected, visited))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var property in type.GetProperties())
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (ObjectContainsString(property.GetValue(value), expected, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class AnalysisSaveResult
        {
            public AnalysisSaveResult(SaveDataRepository repository, SaveData saveData, CapturingGitAnalysisLogger logger)
            {
                Repository = repository;
                SaveData = saveData;
                Logger = logger;
            }

            public SaveDataRepository Repository { get; }
            public SaveData SaveData { get; }
            public CapturingGitAnalysisLogger Logger { get; }
        }

        private sealed class CapturingGitAnalysisLogger : IGitAnalysisLogger
        {
            public List<string> Messages { get; } = new List<string>();

            public void Info(string message)
            {
                Messages.Add(message);
            }

            public void Warning(string message)
            {
                Messages.Add(message);
            }
        }

        private sealed class FakeRunner : IGitCommandRunner
        {
            private readonly Dictionary<string, GitCommandResult> responses;

            private FakeRunner(Dictionary<string, GitCommandResult> responses)
            {
                this.responses = responses;
            }

            public List<string> Commands { get; } = new List<string>();

            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                Commands.Add(arguments);
                if (responses.TryGetValue(arguments, out var result))
                {
                    return Task.FromResult(result);
                }

                return Task.FromResult(GitCommandResult.Success(string.Empty));
            }

            public static FakeRunner WithSafeAggregateOutput()
            {
                return new FakeRunner(BaseResponses(
                    " M Assets/_Project/Scripts/Domain/CharacterModels.cs\n?? README.md\n",
                    "14\t2\tAssets/_Project/Scripts/Domain/CharacterModels.cs\n",
                    string.Empty,
                    "--TOKENFORGE-COMMIT--\n4\t1\tAssets/_Project/Tests/EditMode/GitAnalysisTests.cs\n"));
            }

            public static FakeRunner WithMaliciousAggregateOutput()
            {
                return new FakeRunner(BaseResponses(
                    $" M {RawPath}\n?? docs/README.md\nR  old/private/token.txt -> src/components/Button.tsx\n?? config/settings.yml\n?? tests/login.test.ts\n",
                    $"9999\t5000\t{RawPath}\n-\t-\tAssets/_Project/Art/icon.png\n{RawAuthorization}\n{RawSourceText}\n",
                    "2\t1\tpackage.json\n",
                    $"--TOKENFORGE-COMMIT--\n{RawCommitMessage}\n3\t0\tsrc/foo.test.ts\n--TOKENFORGE-COMMIT--\n7\t2\tREADME.md\n{RawSourceText}\n"));
            }

            public static FakeRunner WithManyCommits(int count)
            {
                var lines = new List<string>();
                for (var i = 0; i < count; i++)
                {
                    lines.Add("--TOKENFORGE-COMMIT--");
                    lines.Add("1\t0\tsrc/file" + i + ".cs");
                }

                return new FakeRunner(BaseResponses(string.Empty, string.Empty, string.Empty, string.Join("\n", lines)));
            }

            public static FakeRunner FailOn(string command)
            {
                var responses = BaseResponses(string.Empty, string.Empty, string.Empty, string.Empty);
                responses[command] = GitCommandResult.Failure("git_command_failed", "Git command failed with stderr.");
                return new FakeRunner(responses);
            }

            private static Dictionary<string, GitCommandResult> BaseResponses(string status, string diff, string cachedDiff, string log)
            {
                return new Dictionary<string, GitCommandResult>
                {
                    ["rev-parse --is-inside-work-tree"] = GitCommandResult.Success("true\n"),
                    ["status --porcelain"] = GitCommandResult.Success(status),
                    ["diff --numstat"] = GitCommandResult.Success(diff),
                    ["diff --cached --numstat"] = GitCommandResult.Success(cachedDiff),
                    ["log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 50"] = GitCommandResult.Success(log),
                    ["log --since=30.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 200"] = GitCommandResult.Success(log)
                };
            }
        }
    }
}
