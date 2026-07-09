using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Git;
using TokenForge.Client.Growth;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.Privacy;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class CompanionProgressionTests
    {
        [Test]
        public void DefaultCompanionState_IsEggWithNoGrowth()
        {
            var state = CompanionProgressionRules.CreateDefaultState();

            Assert.AreEqual(CompanionStage.Egg, state.Stage);
            Assert.AreEqual(CompanionArchetype.Unknown, state.Archetype);
            Assert.AreEqual(1, state.Level);
            Assert.AreEqual(0, state.TotalXp);
            Assert.AreEqual(CompanionProgressionRules.HatchingXp, state.XpToNextStage);
            Assert.That(state.LastGrowthReasonIds, Does.Contain("no_approved_growth_yet"));
        }

        [Test]
        public void StageThresholds_AreDeterministic()
        {
            Assert.AreEqual(CompanionStage.Egg, CompanionProgressionRules.StageForXp(499));
            Assert.AreEqual(CompanionStage.Hatchling, CompanionProgressionRules.StageForXp(500));
            Assert.AreEqual(CompanionStage.Child, CompanionProgressionRules.StageForXp(1500));
            Assert.AreEqual(CompanionStage.Teen, CompanionProgressionRules.StageForXp(3000));
            Assert.AreEqual(CompanionStage.Adult, CompanionProgressionRules.StageForXp(5000));
            Assert.AreEqual(CompanionStage.Legendary, CompanionProgressionRules.StageForXp(9500));
            Assert.AreEqual(0, CompanionProgressionRules.XpToNextStage(9500));
        }

        [Test]
        public void StageMapping_UsesLevelAndKeepsLevelFourOutOfEgg()
        {
            Assert.AreEqual(CompanionStage.Egg, CompanionProgressionRules.StageForLevel(1));
            Assert.AreEqual(CompanionStage.Hatchling, CompanionProgressionRules.StageForLevel(2));
            Assert.AreEqual(CompanionStage.Child, CompanionProgressionRules.StageForLevel(4));
            Assert.AreEqual(CompanionStage.Teen, CompanionProgressionRules.StageForLevel(7));
            Assert.AreEqual(CompanionStage.Adult, CompanionProgressionRules.StageForLevel(11));
            Assert.AreEqual(CompanionStage.Legendary, CompanionProgressionRules.StageForLevel(20));

            var normalized = CompanionProgressionRules.Normalize(new CompanionState { Level = 4, Stage = CompanionStage.Egg, CurrentXp = 0, TotalLifetimeXp = 0 });

            Assert.AreEqual(4, normalized.Level);
            Assert.AreEqual(CompanionStage.Child, normalized.Stage);
        }

        [TestCase(CompanionArchetype.Builder)]
        [TestCase(CompanionArchetype.Debugger)]
        [TestCase(CompanionArchetype.Architect)]
        [TestCase(CompanionArchetype.Explorer)]
        [TestCase(CompanionArchetype.Refiner)]
        [TestCase(CompanionArchetype.Sprinter)]
        public void ArchetypeResolver_UsesSafeAggregateScores(CompanionArchetype expected)
        {
            var profile = ProfileFor(expected);

            var actual = CompanionArchetypeResolver.Resolve(profile);

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void PendingReview_DoesNotMutatePersistedCompanionState()
        {
            var fixture = CreateGitFixture();

            RunAsync("SelectRepositoryAsync", cancellationToken => fixture.Flow.SelectRepositoryAsync(cancellationToken));
            var review = RunAsync("AnalyzeAsync", cancellationToken => fixture.Flow.AnalyzeAsync(cancellationToken));

            Assert.IsTrue(review.IsSuccess, review.ErrorMessage);
            Assert.IsTrue(fixture.Flow.HasPendingReview);
            Assert.AreEqual(0, fixture.Repository.Current.CompanionState.TotalXp);
            Assert.AreEqual(CompanionStage.Egg, fixture.Repository.Current.CompanionState.Stage);
            Assert.AreEqual(1, fixture.Repository.SaveCount);
        }

        [Test]
        public void SaveReview_UpdatesCompanionOnlyAfterApproval()
        {
            var fixture = CreateGitFixture();

            RunAsync("SelectRepositoryAsync", cancellationToken => fixture.Flow.SelectRepositoryAsync(cancellationToken));
            RunAsync("AnalyzeAsync", cancellationToken => fixture.Flow.AnalyzeAsync(cancellationToken));
            var saved = RunAsync("SaveSessionAsync", cancellationToken => fixture.Flow.SaveSessionAsync(cancellationToken));

            Assert.IsTrue(saved.IsSuccess, saved.ErrorMessage);
            Assert.AreEqual(2, fixture.Repository.SaveCount);
            Assert.Greater(fixture.Repository.Current.CompanionState.TotalXp, 0);
            Assert.AreNotEqual(CompanionArchetype.Unknown, fixture.Repository.Current.CompanionState.Archetype);
            Assert.That(fixture.Repository.Current.CompanionState.LastGrowthReasonIds, Does.Contain("approved_aggregate_growth"));
            Assert.IsTrue(new PrivacySanitizer().ValidateSafeSaveData(fixture.Repository.Current).IsSuccess);
        }

        [Test]
        public void TokenOnlyHighUsage_UsesCautiousGrowthReason()
        {
            var session = Session("token-only", AgentProviderType.Codex, WorkType.Research, TokenUsageBucket.Huge);
            session.GitChangeSummary = GitChangeSummary.Empty();
            session.ActionSummary = AgentActionSummary.Empty();

            var state = CompanionProgressionRules.CalculateState(new[] { session }, new[] { Growth("token-only", 160) });

            Assert.That(state.LastGrowthReasonIds, Does.Contain(CompanionGrowthReasonIds.HighTokenLowLocalActivityCautiousGrowth));
            Assert.AreEqual(1, state.GrowthProfile.HighTokenLowLocalActivityCount);
            Assert.Less(state.TotalXp, 160);
        }

        [Test]
        public void BalancedTokenAndGit_AddsGrowthBoost()
        {
            var session = Session("balanced", AgentProviderType.Codex, WorkType.Feature, TokenUsageBucket.Large);
            session.GitChangeSummary.ChangedFileCount = 4;
            session.GitChangeSummary.ChangedFileCountBucket = CountBucket.Small;

            var state = CompanionProgressionRules.CalculateState(new[] { session }, new[] { Growth("balanced", 160) });

            Assert.That(state.LastGrowthReasonIds, Does.Contain(CompanionGrowthReasonIds.BalancedTokenGitGrowthBoost));
            Assert.AreEqual(1, state.GrowthProfile.BalancedTokenGitActivityCount);
            Assert.Greater(state.TotalXp, 160);
        }

        [Test]
        public void ProviderInfluence_CodexDebuggerAndClaudeArchitectAreDeterministic()
        {
            var codex = Session("codex-debug", AgentProviderType.Codex, WorkType.Bugfix, TokenUsageBucket.Medium);
            codex.ActionSummary.FailedCommandCount = 2;
            codex.GitChangeSummary.TestFileChanged = true;
            var codexState = CompanionProgressionRules.CalculateState(new[] { codex }, new[] { Growth("codex-debug", 220) });

            var claude = Session("claude-arch", AgentProviderType.ClaudeCode, WorkType.Docs, TokenUsageBucket.Medium);
            claude.GitChangeSummary.DocsFileChanged = true;
            claude.GitChangeSummary.ExtensionCategoryBuckets.Add(new ExtensionCategoryCount { Category = "markdown", Count = 3, CountBucket = CountBucket.Small });
            var claudeState = CompanionProgressionRules.CalculateState(new[] { claude }, new[] { Growth("claude-arch", 220) });

            Assert.AreEqual(CompanionArchetype.Debugger, codexState.Archetype);
            Assert.That(codexState.LastGrowthReasonIds, Does.Contain(CompanionGrowthReasonIds.ProviderCodexDebuggerSignal));
            Assert.AreEqual(CompanionArchetype.Architect, claudeState.Archetype);
            Assert.That(claudeState.LastGrowthReasonIds, Does.Contain(CompanionGrowthReasonIds.ProviderClaudeArchitectSignal));
        }

        [Test]
        public void GitCommitStyleInfluencesRefinerSprinterAndExplorer()
        {
            var refinerSessions = Enumerable.Range(0, 3)
                .Select(index =>
                {
                    var session = Session("small-" + index, AgentProviderType.GitHubCopilot, WorkType.Chore, TokenUsageBucket.Small);
                    session.GitChangeSummary.CommitCountBucket = CountBucket.Small;
                    session.GitChangeSummary.ChangedFileCount = 1;
                    session.GitChangeSummary.ChangedFileCountBucket = CountBucket.One;
                    session.GitChangeSummary.AddedLineBucket = LineChangeBucket.Small;
                    return session;
                })
                .ToList();
            var refiner = CompanionProgressionRules.CalculateState(refinerSessions, refinerSessions.Select(session => Growth(session.SessionId, 90)));

            var sprinter = Session("burst", AgentProviderType.Cursor, WorkType.Feature, TokenUsageBucket.Medium);
            sprinter.GitChangeSummary.CommitCountBucket = CountBucket.Huge;
            sprinter.GitChangeSummary.ChangedFileCountBucket = CountBucket.Huge;
            sprinter.GitChangeSummary.AddedLineBucket = LineChangeBucket.Huge;
            var sprinterState = CompanionProgressionRules.CalculateState(new[] { sprinter }, new[] { Growth("burst", 160) });

            var explorer = Session("explorer", AgentProviderType.Cursor, WorkType.Mixed, TokenUsageBucket.Medium);
            explorer.SourceProviders.Add("CODEX");
            explorer.SourceProviders.Add("GITHUB_COPILOT");
            explorer.GitChangeSummary.FileCategoryCounts.Add(new FileCategoryCount { Category = FileCategory.Test, Count = 2 });
            explorer.GitChangeSummary.FileCategoryCounts.Add(new FileCategoryCount { Category = FileCategory.Docs, Count = 2 });
            explorer.GitChangeSummary.FileCategoryCounts.Add(new FileCategoryCount { Category = FileCategory.UI, Count = 2 });
            var explorerState = CompanionProgressionRules.CalculateState(new[] { explorer }, new[] { Growth("explorer", 160) });

            Assert.AreEqual(CompanionArchetype.Refiner, refiner.Archetype);
            Assert.That(refiner.LastGrowthReasonIds, Does.Contain(CompanionGrowthReasonIds.SteadyReviewSaveConsistencyBonus));
            Assert.AreEqual(CompanionArchetype.Sprinter, sprinterState.Archetype);
            Assert.That(sprinterState.LastGrowthReasonIds, Does.Contain(CompanionGrowthReasonIds.GitLargeBurstSprinterSignal));
            Assert.AreEqual(CompanionArchetype.Explorer, explorerState.Archetype);
            Assert.That(explorerState.LastGrowthReasonIds, Does.Contain(CompanionGrowthReasonIds.ProviderDiversityExplorerSignal));
        }

        [Test]
        public void DiscardedReview_DoesNotMutatePersistedCompanionState()
        {
            var fixture = CreateGitFixture();

            RunAsync("SelectRepositoryAsync", cancellationToken => fixture.Flow.SelectRepositoryAsync(cancellationToken));
            RunAsync("AnalyzeAsync", cancellationToken => fixture.Flow.AnalyzeAsync(cancellationToken));
            var discarded = fixture.Flow.DiscardPendingReview();

            Assert.IsTrue(discarded.IsSuccess, discarded.ErrorMessage);
            Assert.IsFalse(fixture.Flow.HasPendingReview);
            Assert.AreEqual(0, fixture.Repository.Current.CompanionState.TotalXp);
            Assert.AreEqual(1, fixture.Repository.SaveCount);
        }

        private static CompanionGrowthProfile ProfileFor(CompanionArchetype archetype)
        {
            var profile = new CompanionGrowthProfile { ApprovedSessionCount = 3 };
            switch (archetype)
            {
                case CompanionArchetype.Builder:
                    profile.ImplementationScore = 12;
                    break;
                case CompanionArchetype.Debugger:
                    profile.DebugScore = 12;
                    break;
                case CompanionArchetype.Architect:
                    profile.StructureScore = 12;
                    break;
                case CompanionArchetype.Explorer:
                    profile.ExplorationScore = 12;
                    profile.ProviderDiversityBucket = CountBucket.Small;
                    break;
                case CompanionArchetype.Refiner:
                    profile.CleanupScore = 12;
                    profile.SmallChangeSessionCount = 3;
                    break;
                case CompanionArchetype.Sprinter:
                    profile.BurstScore = 12;
                    profile.LargeBurstSessionCount = 1;
                    break;
            }

            return profile;
        }

        private static AgentWorkSession Session(string id, AgentProviderType provider, WorkType workType, TokenUsageBucket tokenBucket)
        {
            return new AgentWorkSession
            {
                SessionId = id,
                AgentType = provider == AgentProviderType.Codex ? AgentType.Codex : AgentType.Unknown,
                SourceProvider = provider.ToString().ToUpperInvariant(),
                WorkType = workType,
                TokenUsageBucket = tokenBucket,
                ResultStatus = ResultStatus.Succeeded,
                Confidence = ProviderConfidence.High,
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = provider,
                    ConfidenceLevel = ConfidenceLevel.High,
                    InteractionCountBucket = CountBucket.Small
                },
                ActionSummary = new AgentActionSummary
                {
                    FileEditCount = 2,
                    DurationBucket = DurationBucket.FiveTo15Minutes
                },
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 2,
                    ChangedFileCountBucket = CountBucket.Small,
                    AddedLineBucket = LineChangeBucket.Small,
                    CommitCountBucket = CountBucket.One,
                    ConfidenceLevel = ConfidenceLevel.High,
                    AnalyzerVersion = "git-aggregate-v1"
                }
            };
        }

        private static CharacterGrowthResult Growth(string sessionId, int xp)
        {
            return new CharacterGrowthResult
            {
                SessionId = sessionId,
                ExpGained = xp,
                LevelBefore = 1,
                LevelAfter = 1,
                StatDeltas = CharacterStats.Zero()
            };
        }

        private static GitFixture CreateGitFixture()
        {
            var privacy = new PrivacySanitizer();
            var repository = new FakeRepository();
            var rawRepositoryPath = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName(), "PrivateRepo");
            Directory.CreateDirectory(rawRepositoryPath);
            var flow = new GitAnalysisFlowController(
                new FakeRepositoryPicker(rawRepositoryPath),
                new GitAggregateAnalyzer(new FakeGitRunner(), privacy),
                repository,
                new GrowthCalculator(new GrowthCalculationConfig { EnableDailyCap = false }),
                privacy);
            return new GitFixture(flow, repository);
        }

        private static T RunAsync<T>(
            string operationName,
            Func<CancellationToken, Task<T>> action,
            [CallerMemberName] string testName = "")
        {
            const int TimeoutMilliseconds = 10000;
            using (var cancellation = new CancellationTokenSource())
            {
                var originalContext = SynchronizationContext.Current;
                try
                {
                    SynchronizationContext.SetSynchronizationContext(null);
                    var operationTask = action(cancellation.Token);
                    var timeoutTask = Task.Delay(TimeoutMilliseconds);
                    var completed = Task.WhenAny(operationTask, timeoutTask).GetAwaiter().GetResult();
                    if (!ReferenceEquals(completed, operationTask))
                    {
                        cancellation.Cancel();
                        Assert.Fail(
                            testName + " timed out after " + TimeoutMilliseconds + "ms while running " + operationName +
                            ". The async operation did not complete; check for Unity main-thread continuation capture, an infinite wait, or an unobserved cancellation path.");
                    }

                    return operationTask.GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(originalContext);
                }
            }
        }

        private sealed class GitFixture
        {
            public GitFixture(GitAnalysisFlowController flow, FakeRepository repository)
            {
                Flow = flow;
                Repository = repository;
            }

            public GitAnalysisFlowController Flow { get; }
            public FakeRepository Repository { get; }
        }

        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public int SaveCount { get; private set; }

            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Current);
            }

            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                SaveCount++;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeRepositoryPicker : IRepositoryPicker
        {
            private readonly string path;

            public FakeRepositoryPicker(string path)
            {
                this.path = path;
            }

            public Task<RepositoryPickerResult> PickRepositoryAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(RepositoryPickerResult.Selected(path));
            }
        }

        private sealed class FakeGitRunner : IGitCommandRunner
        {
            public Task<GitCommandResult> RunAsync(string workingDirectory, string arguments, CancellationToken cancellationToken)
            {
                if (arguments == "rev-parse --is-inside-work-tree")
                {
                    return Task.FromResult(GitCommandResult.Success("true\n"));
                }

                if (arguments == "rev-parse HEAD")
                {
                    return Task.FromResult(GitCommandResult.Success("HEADSHA\n"));
                }

                if (arguments == "rev-list --max-parents=0 --all --reverse")
                {
                    return Task.FromResult(GitCommandResult.Success("FIRSTSHA\n"));
                }

                if (arguments == "show -s --format=%cI FIRSTSHA")
                {
                    return Task.FromResult(GitCommandResult.Success("2026-01-02T03:04:05Z\n"));
                }

                if (arguments == "rev-list --all --count")
                {
                    return Task.FromResult(GitCommandResult.Success("42\n"));
                }

                if (arguments == "status --porcelain")
                {
                    return Task.FromResult(GitCommandResult.Success(" M Assets/SafeAggregate.cs\n"));
                }

                if (arguments == "diff --numstat" || arguments == "diff --cached --numstat")
                {
                    return Task.FromResult(GitCommandResult.Success("120\t20\tAssets/SafeAggregate.cs\n"));
                }

                if (arguments.StartsWith("log --since=", StringComparison.Ordinal))
                {
                    return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n55\t5\tAssets/SafeAggregate.cs\n"));
                }

                if (arguments == "log --all --numstat --format=format:--TOKENFORGE-COMMIT--")
                {
                    return Task.FromResult(GitCommandResult.Success("--TOKENFORGE-COMMIT--\n55\t5\tAssets/SafeAggregate.cs\n"));
                }

                return Task.FromResult(GitCommandResult.Failure("unsupported_git_command", "Unsupported test command."));
            }
        }
    }
}
