using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase8ApprovedLocationSettingsTests
    {
        private const string RawGitPath = "/Users/alice/SecretRepo";
        private const string RawAgentPath = "/Users/alice/SecretRepo/.codex/sessions/private-session.jsonl";
        private const string RawRepoName = "SecretRepo";
        private const string RawPrompt = "prompt: inspect private token flow";
        private const string RawResponse = "response: private customer detail";
        private const string RawCommand = "git status --short && npm test";
        private const string RawSource = "public class Secret { string password = \"hunter2\"; }";
        private const string RawToken = "Bearer abc.def.secret";

        [Test]
        public void ApprovedLocationRepository_PersistsAndReloadsLocalOnlyPaths()
        {
            var repository = new ApprovedLocationSettingsRepository(CreateTempDirectory());

            var git = RunAsync(() => repository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = "Work repo",
                SourceType = ApprovedLocationSourceType.Git,
                LocalPath = RawGitPath,
                Enabled = true
            }, CancellationToken.None));
            var agent = RunAsync(() => repository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = "Codex logs",
                SourceType = ApprovedLocationSourceType.Codex,
                LocalPath = RawAgentPath,
                Enabled = true
            }, CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));
            var json = File.ReadAllText(repository.SettingsFilePath);

            Assert.IsTrue(git.IsSuccess, git.ErrorMessage);
            Assert.IsTrue(agent.IsSuccess, agent.ErrorMessage);
            Assert.AreEqual(2, loaded.Locations.Count);
            Assert.IsTrue(ObjectContainsString(loaded, RawGitPath));
            Assert.IsTrue(ObjectContainsString(loaded, RawAgentPath));
            Assert.IsTrue(json.Contains(RawGitPath));
            Assert.IsTrue(json.Contains(RawAgentPath));
            Assert.AreEqual(ApprovedLocationSettingsRepository.SettingsFileName, Path.GetFileName(repository.SettingsFilePath));
        }

        [Test]
        public void ApprovedLocalPaths_AreNotInSaveDataOrSafeSyncDtos()
        {
            var approvedRepository = new ApprovedLocationSettingsRepository(CreateTempDirectory());
            RunAsync(() => approvedRepository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = "Work repo",
                SourceType = ApprovedLocationSourceType.Git,
                LocalPath = RawGitPath,
                Enabled = true
            }, CancellationToken.None));
            RunAsync(() => approvedRepository.AddOrUpdateAsync(new ApprovedLocationEntry
            {
                DisplayAlias = "Codex logs",
                SourceType = ApprovedLocationSourceType.Codex,
                LocalPath = RawAgentPath,
                Enabled = true
            }, CancellationToken.None));

            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(AgentLogActivityProvider.CreateSession(new AgentActivitySummary
            {
                ProviderType = AgentProviderType.Codex,
                SourceIdentifierHash = "safehash",
                DayBucket = "2026-05-14",
                SessionCountBucket = CountBucket.One,
                InteractionCountBucket = CountBucket.Small,
                EstimatedCodingActivityBucket = CountBucket.Small,
                ConfidenceLevel = ConfidenceLevel.High,
                AnalyzerVersion = CodexAgentLogParser.Version
            }));
            var payload = new SafeSyncMapper().ToPayload(saveData);

            Assert.IsTrue(ObjectContainsString(RunAsync(() => approvedRepository.LoadAsync(CancellationToken.None)), RawAgentPath));
            AssertNoRawApprovedPaths(saveData);
            AssertNoRawApprovedPaths(payload);
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(saveData).IsSuccess);
            Assert.IsTrue(new SyncPayloadSanitizer().ValidatePayload(payload).IsSafe);
        }

        [Test]
        public void DashboardApprovedLocations_UseAliasesForDisplayAndRequireExplicitAnalyze()
        {
            var fixture = CreateDashboardFixture();

            RunAsync(() => fixture.Dashboard.GitFlow.SelectRepositoryAsync(CancellationToken.None));
            var addGit = RunAsync(() => fixture.Dashboard.AddCurrentGitSelectionToApprovedLocationsAsync("Local repo alias", CancellationToken.None));
            fixture.Dashboard.DiscardGitReview();
            var selectGit = RunAsync(() => fixture.Dashboard.SelectApprovedGitLocationAsync(addGit.Value.LocalId, CancellationToken.None));

            RunAsync(() => fixture.Dashboard.SelectAgentLogLocationAsync(CancellationToken.None));
            var addAgent = RunAsync(() => fixture.Dashboard.AddCurrentAgentSelectionToApprovedLocationsAsync("Local logs alias", CancellationToken.None));
            fixture.Dashboard.DiscardAgentReview();
            var selectAgent = RunAsync(() => fixture.Dashboard.SelectApprovedAgentLocationAsync(addAgent.Value.LocalId, CancellationToken.None));

            Assert.IsTrue(addGit.IsSuccess, addGit.ErrorMessage);
            Assert.IsTrue(selectGit.IsSuccess, selectGit.ErrorMessage);
            Assert.AreEqual(GitAnalysisFlowState.Selected, fixture.Dashboard.GitFlow.State);
            Assert.IsNull(fixture.Dashboard.GitFlow.Review);
            Assert.AreEqual(2, fixture.SaveRepository.SaveCount);

            Assert.IsTrue(addAgent.IsSuccess, addAgent.ErrorMessage);
            Assert.IsTrue(selectAgent.IsSuccess, selectAgent.ErrorMessage);
            Assert.AreEqual(AgentAnalysisFlowState.Selected, fixture.Dashboard.AgentFlow.State);
            Assert.IsNull(fixture.Dashboard.AgentFlow.Review);
            Assert.AreEqual("Local repo alias", fixture.Dashboard.ApprovedGitLocations[0].DisplayAlias);
            Assert.AreEqual("Local logs alias", fixture.Dashboard.ApprovedAgentLocations[0].DisplayAlias);
            AssertNoRawApprovedPaths(fixture.Dashboard.ApprovedGitLocations);
            AssertNoRawApprovedPaths(fixture.Dashboard.ApprovedAgentLocations);
        }

        [Test]
        public void DisableOrRemoveApprovedLocation_DoesNotDeleteSavedSafeSessions()
        {
            var fixture = CreateDashboardFixture();

            RunAsync(() => fixture.Dashboard.SelectAgentLogLocationAsync(CancellationToken.None));
            var approved = RunAsync(() => fixture.Dashboard.AddCurrentAgentSelectionToApprovedLocationsAsync("Local logs", CancellationToken.None));
            RunAsync(() => fixture.Dashboard.AnalyzeAgentActivityAsync(CancellationToken.None));
            var save = RunAsync(() => fixture.Dashboard.SaveAgentSessionAsync(CancellationToken.None));
            var disable = RunAsync(() => fixture.Dashboard.DisableApprovedLocationAsync(approved.Value.LocalId, CancellationToken.None));
            var remove = RunAsync(() => fixture.Dashboard.RemoveApprovedLocationAsync(approved.Value.LocalId, CancellationToken.None));

            Assert.IsTrue(save.IsSuccess, save.ErrorMessage);
            Assert.IsTrue(disable.IsSuccess, disable.ErrorMessage);
            Assert.IsTrue(remove.IsSuccess, remove.ErrorMessage);
            Assert.AreEqual(1, fixture.SaveRepository.Current.WorkSessionSummaries.Count);
            Assert.AreEqual(1, fixture.Dashboard.RecentSessions.Count);
            AssertNoRawApprovedPaths(fixture.SaveRepository.Current);
            AssertNoRawApprovedPaths(fixture.Dashboard.RecentSessions);
        }

        [Test]
        public void ForbiddenFieldDetector_StillRejectsPhase8RiskyStrings()
        {
            var detector = new ForbiddenFieldDetector();

            Assert.IsTrue(detector.ContainsSensitiveString(RawPrompt));
            Assert.IsTrue(detector.ContainsSensitiveString(RawResponse));
            Assert.IsTrue(detector.ContainsSensitiveString(RawCommand));
            Assert.IsTrue(detector.ContainsSensitiveString(RawSource));
            Assert.IsTrue(detector.ContainsSensitiveString(RawToken));
            Assert.IsTrue(detector.ContainsSensitiveString("fileName: UserSecret.cs"));
            Assert.IsTrue(detector.ContainsSensitiveString("repo: " + RawRepoName));
            Assert.IsTrue(detector.ContainsSensitiveString("branch: feature/private-token"));
            Assert.IsTrue(detector.ContainsSensitiveString("username: alice"));
        }

        private static DashboardFixture CreateDashboardFixture()
        {
            var privacySanitizer = new PrivacySanitizer();
            var saveRepository = new FakeLocalSaveRepository();
            var approvedRepository = new ApprovedLocationSettingsRepository(CreateTempDirectory());
            var gitRoot = Path.Combine(CreateTempDirectory(), RawRepoName);
            Directory.CreateDirectory(gitRoot);
            var gitFlow = new GitAnalysisFlowController(
                new FakeRepositoryPicker(gitRoot),
                new GitAggregateAnalyzer(new FakeGitRunner(), privacySanitizer),
                saveRepository,
                null,
                privacySanitizer);
            var agentFlow = new AgentAnalysisFlowController(
                new AgentLogActivityProvider(new AgentActivityAnalyzer(new FakeAgentLogReader(), null, privacySanitizer)),
                saveRepository,
                null,
                privacySanitizer);
            var dashboard = new ApprovedActivityAnalysisViewModel(
                gitFlow,
                agentFlow,
                new FakeAgentLogLocationPicker(RawAgentPath),
                saveRepository,
                privacySanitizer,
                approvedRepository);

            return new DashboardFixture(dashboard, saveRepository);
        }

        private static void AssertNoRawApprovedPaths(object value)
        {
            Assert.IsFalse(ObjectContainsString(value, RawGitPath), RawGitPath);
            Assert.IsFalse(ObjectContainsString(value, RawAgentPath), RawAgentPath);
            Assert.IsFalse(ObjectContainsString(value, RawRepoName), RawRepoName);
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new HashSet<object>());
        }

        private static bool ObjectContainsString(object value, string expected, HashSet<object> visited)
        {
            if (value == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            if (value is string text)
            {
                return text.Contains(expected);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(DateTimeOffset) || type == typeof(DateTime))
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

        private static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }

        private sealed class DashboardFixture
        {
            public DashboardFixture(ApprovedActivityAnalysisViewModel dashboard, FakeLocalSaveRepository saveRepository)
            {
                Dashboard = dashboard;
                SaveRepository = saveRepository;
            }

            public ApprovedActivityAnalysisViewModel Dashboard { get; }
            public FakeLocalSaveRepository SaveRepository { get; }
        }

        private sealed class FakeRepositoryPicker : IRepositoryPicker
        {
            private readonly string repositoryRootPath;

            public FakeRepositoryPicker(string repositoryRootPath)
            {
                this.repositoryRootPath = repositoryRootPath;
            }

            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RepositoryPickerResult.Selected(repositoryRootPath));
            }
        }

        private sealed class FakeAgentLogLocationPicker : IAgentLogLocationPicker
        {
            private readonly string locationPath;

            public FakeAgentLogLocationPicker(string locationPath)
            {
                this.locationPath = locationPath;
            }

            public Task<AgentLogLocationPickerResult> PickAgentLogLocationAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(AgentLogLocationPickerResult.Selected(locationPath));
            }
        }

        private sealed class FakeAgentLogReader : IAgentLogSourceReader
        {
            public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
            {
                return Task.FromResult(AgentLogReadResult.Success(new List<AgentLogEntry>
                {
                    new AgentLogEntry { Text = "{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T01:00:00Z\",\"session_id\":\"s1\",\"type\":\"message\",\"role\":\"user\",\"prompt\":\"" + RawPrompt + "\"}" },
                    new AgentLogEntry { Text = "{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T01:01:00Z\",\"tool\":\"exec_command\",\"command\":\"" + RawCommand + "\"}" },
                    new AgentLogEntry { Text = "{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T01:02:00Z\",\"tool\":\"apply_patch\",\"file_path\":\"" + RawAgentPath + "\",\"sourceText\":\"" + Escape(RawSource) + "\"}" }
                }, new List<string>()));
            }

            private static string Escape(string value)
            {
                return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }
        }

        private sealed class FakeGitRunner : IGitCommandRunner
        {
            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                switch (arguments)
                {
                    case "rev-parse --is-inside-work-tree":
                        return Task.FromResult(GitCommandResult.Success("true\n"));
                    case "rev-parse HEAD":
                        return Task.FromResult(GitCommandResult.Success("HEADSHA\n"));
                    case "log --all --reverse --format=%cI -n 1":
                        return Task.FromResult(GitCommandResult.Success("2026-01-02T03:04:05Z\n"));
                    case "rev-list --all --count":
                        return Task.FromResult(GitCommandResult.Success("42\n"));
                    case "status --porcelain":
                        return Task.FromResult(GitCommandResult.Success(" M src/SafeAggregate.cs\n"));
                    case "diff --numstat":
                        return Task.FromResult(GitCommandResult.Success("10\t2\tsrc/SafeAggregate.cs\n"));
                    case "diff --cached --numstat":
                        return Task.FromResult(GitCommandResult.Success(string.Empty));
                    case "log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 200":
                    case "log --since=7.days.ago --numstat --format=--TOKENFORGE-COMMIT-- -n 50":
                    case "log --all --numstat --format=--TOKENFORGE-COMMIT--":
                        return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n1\t0\tREADME.md\n"));
                    default:
                        return Task.FromResult(GitCommandResult.Success(string.Empty));
                }
            }
        }

        private sealed class FakeLocalSaveRepository : ILocalSaveDataRepository
        {
            private readonly PrivacySanitizer privacySanitizer = new PrivacySanitizer();

            public SaveData Current { get; private set; } = SaveData.CreateDefault();
            public int SaveCount { get; private set; }

            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Current);
            }

            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                var validation = privacySanitizer.ValidateSafeSaveData(saveData);
                if (!validation.IsSuccess)
                {
                    return Task.FromResult(validation);
                }

                SaveCount++;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }
    }
}
