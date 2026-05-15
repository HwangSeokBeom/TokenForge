using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase25ConflictMergeAuditRetryTests
    {
        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveDataWithSession("client-session-1", ProviderConfidence.High, "2026-05-15");
            public int SaveCount { get; private set; }
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                SaveCount += 1;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        private sealed class FakeApiClient : ISafeSyncApiClient
        {
            public SafeSyncApiConfig Config { get; } = new SafeSyncApiConfig("http://localhost:3000/api/v1");
            public int PostCount { get; private set; }
            public SafeSyncApiResult<SafeActivitySessionsUpsertResponse> PostResult { get; set; } =
                SafeSyncApiResult<SafeActivitySessionsUpsertResponse>.Success(new SafeActivitySessionsUpsertResponse { Success = true, AcceptedCount = 1, RejectedCount = 0, SchemaVersion = 1 }, 201);
            public Task<SafeSyncApiResult<SafeSyncHealthResponse>> GetHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeSyncHealthResponse>.Success(new SafeSyncHealthResponse { SchemaVersion = 1 }, 200));
            public Task<SafeSyncApiResult<SafeActivitySessionsUpsertResponse>> PostActivitySessionsAsync(SafeActivitySessionsContractRequest request, CancellationToken cancellationToken = default)
            {
                PostCount += 1;
                return Task.FromResult(PostResult);
            }

            public Task<SafeSyncApiResult<SafeActivitySessionsListResponse>> GetActivitySessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionsListResponse>.Success(new SafeActivitySessionsListResponse(), 200));
            public Task<SafeSyncApiResult<SafeActivitySessionDeleteResponse>> DeleteActivitySessionAsync(string serverSessionId, CancellationToken cancellationToken = default) => Task.FromResult(SafeSyncApiResult<SafeActivitySessionDeleteResponse>.Success(new SafeActivitySessionDeleteResponse { Success = true }, 200));
        }

        [Test]
        public void PreviewPoliciesDoNotPersistOrEnqueue()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var service = CreateService(repository, new FakeApiClient(), directory, Conflict("conflict-1", "HIGH", "MEDIUM", "2026-05-15", "2026-05-16"));

            var preview = RunAsync(() => service.GetConflictMergePreviewAsync("conflict-1", SafeConflictMergePolicy.KeepLocal, CancellationToken.None));
            var queue = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));
            var audit = RunAsync(() => new SafeConflictAuditRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsTrue(preview.CanApply, preview.BlockedReason);
            Assert.AreEqual(0, queue.Entries.Count);
            Assert.AreEqual(0, audit.Entries.Count);
            Assert.AreEqual(0, repository.SaveCount);
        }

        [Test]
        public void PreferPoliciesChooseSafeHigherConfidenceOrNewerTimestampAndBlockTies()
        {
            var directory = TempDirectory();
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory, Conflict("conflict-1", "HIGH", "MEDIUM", "2026-05-15", "2026-05-16"));

            var confidence = RunAsync(() => service.GetConflictMergePreviewAsync("conflict-1", SafeConflictMergePolicy.PreferHigherConfidence, CancellationToken.None));
            var timestamp = RunAsync(() => service.GetConflictMergePreviewAsync("conflict-1", SafeConflictMergePolicy.PreferNewerSafeTimestamp, CancellationToken.None));

            Assert.IsTrue(confidence.CanApply, confidence.BlockedReason);
            Assert.AreEqual(SafeConflictMergePolicy.KeepLocal, confidence.EffectivePolicy);
            Assert.IsTrue(timestamp.CanApply, timestamp.BlockedReason);
            Assert.AreEqual(SafeConflictMergePolicy.KeepRemote, timestamp.EffectivePolicy);

            var tieDirectory = TempDirectory();
            var tieService = CreateService(new FakeRepository(), new FakeApiClient(), tieDirectory, Conflict("conflict-2", "HIGH", "HIGH", "2026-05-15", "2026-05-15"));
            var tie = RunAsync(() => tieService.GetConflictMergePreviewAsync("conflict-2", SafeConflictMergePolicy.PreferHigherConfidence, CancellationToken.None));

            Assert.IsFalse(tie.CanApply);
            Assert.AreEqual(SafeSyncApiError.MergePolicyManualChoiceRequired, tie.BlockedReason);
        }

        [Test]
        public void MergeNonConflictingAggregatesUnionsSafeBucketsAndBlocksAmbiguousCounts()
        {
            var directory = TempDirectory();
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory, Conflict("conflict-1", "HIGH", "HIGH", "2026-05-15", "2026-05-15", localCategory: "WORK_FEATURE:ONE", remoteCategory: "WORK_BUGFIX:ONE"));

            var preview = RunAsync(() => service.GetConflictMergePreviewAsync("conflict-1", SafeConflictMergePolicy.MergeNonConflictingAggregates, CancellationToken.None));

            Assert.IsTrue(preview.CanApply, preview.BlockedReason);
            Assert.That(preview.ResultingSummary.CategoryBucketSummary, Does.Contain("WORK_FEATURE:ONE"));
            Assert.That(preview.ResultingSummary.CategoryBucketSummary, Does.Contain("WORK_BUGFIX:ONE"));

            var blockedDirectory = TempDirectory();
            var blocked = CreateService(new FakeRepository(), new FakeApiClient(), blockedDirectory, Conflict("conflict-2", "HIGH", "HIGH", "2026-05-15", "2026-05-15", warningLocal: 1, warningRemote: 2));
            var blockedPreview = RunAsync(() => blocked.GetConflictMergePreviewAsync("conflict-2", SafeConflictMergePolicy.MergeNonConflictingAggregates, CancellationToken.None));

            Assert.IsFalse(blockedPreview.CanApply);
            Assert.AreEqual(SafeSyncApiError.MergePolicyAmbiguousAggregate, blockedPreview.BlockedReason);
            Assert.That(blockedPreview.Warnings, Does.Contain("warningCount"));
        }

        [Test]
        public void ApplyRequiresConfirmationAndCreatesAuditForKeepLocal()
        {
            var directory = TempDirectory();
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory, Conflict("conflict-1", "HIGH", "MEDIUM", "2026-05-15", "2026-05-16"));

            var denied = RunAsync(() => service.ApplyConflictMergePolicyAsync("conflict-1", SafeConflictMergePolicy.KeepLocal, false, CancellationToken.None));
            var applied = RunAsync(() => service.ApplyConflictMergePolicyAsync("conflict-1", SafeConflictMergePolicy.KeepLocal, true, CancellationToken.None));
            var audit = RunAsync(() => new SafeConflictAuditRepository(directory).LoadAsync(CancellationToken.None));
            var queue = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsFalse(denied.Applied);
            Assert.AreEqual(SafeSyncApiError.MergePolicyConfirmationRequired, denied.UserMessageCode);
            Assert.IsTrue(applied.Applied, applied.UserMessageCode);
            Assert.IsTrue(applied.QueuedRetry);
            Assert.AreEqual(1, queue.Entries.Count);
            Assert.AreEqual(1, audit.Entries.Count);
            Assert.AreEqual(SafeConflictMergePolicy.KeepLocal, audit.Entries.Single().Policy);
        }

        [Test]
        public void ApplyMergeWritesAggregateOnlyLocalSessionAndQueuesUploadRetry()
        {
            var directory = TempDirectory();
            var repository = new FakeRepository();
            var service = CreateService(repository, new FakeApiClient(), directory, Conflict("conflict-1", "HIGH", "HIGH", "2026-05-15", "2026-05-15", localCategory: "WORK_FEATURE:ONE", remoteCategory: "WORK_BUGFIX:ONE"));

            var result = RunAsync(() => service.ApplyConflictMergePolicyAsync("conflict-1", SafeConflictMergePolicy.MergeNonConflictingAggregates, true, CancellationToken.None));
            var queue = RunAsync(() => new SafeSyncRetryQueueRepository(directory).LoadAsync(CancellationToken.None));

            Assert.IsTrue(result.Applied, result.UserMessageCode);
            Assert.IsTrue(result.LocalChanged);
            Assert.IsTrue(result.QueuedRetry);
            Assert.AreEqual(SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, queue.Entries.Single().OperationType);
            Assert.AreEqual(1, repository.Current.WorkSessionSummaries.Count);
            Assert.That(repository.Current.WorkSessionSummaries.Single().Warnings, Does.Not.Contain("/Users/"));
        }

        [Test]
        public void AuditRepositoryIsSafeLocalOnlyCorruptionSafeFutureSchemaSafeAndTrimmed()
        {
            var directory = TempDirectory();
            var repository = new SafeConflictAuditRepository(directory);
            var state = new SafeConflictAuditState();
            for (var i = 0; i < SafeConflictAuditRepository.RetentionLimit + 5; i++)
            {
                state.Entries.Add(new SafeConflictAuditEntry
                {
                    AuditEntryId = "audit-" + i,
                    ConflictId = "conflict-" + i,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i),
                    ResultStatus = "resolved",
                    UserMessageCode = SafeSyncApiError.MergePolicyApplied
                });
            }

            var save = RunAsync(() => repository.SaveAsync(state, CancellationToken.None));
            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.IsTrue(save.IsSuccess, save.ErrorCode);
            Assert.AreEqual(SafeConflictAuditRepository.RetentionLimit, loaded.Entries.Count);
            Assert.That(File.ReadAllText(repository.FilePath), Does.Not.Contain("/Users/"));
            Assert.That(File.ReadAllText(Path.Combine(FindRepoRoot(), ".gitignore")), Does.Contain(SafeConflictAuditRepository.FileName));

            File.WriteAllText(repository.FilePath, "{\"schemaVersion\":99,\"entries\":[{\"auditEntryId\":\"future\"}]}");
            Assert.AreEqual(0, RunAsync(() => repository.LoadAsync(CancellationToken.None)).Entries.Count);
            File.WriteAllText(repository.FilePath, "{broken");
            Assert.AreEqual(0, RunAsync(() => repository.LoadAsync(CancellationToken.None)).Entries.Count);
            File.WriteAllText(repository.FilePath, "{\"schemaVersion\":1,\"entries\":[{\"auditEntryId\":\"unsafe\",\"userMessageCode\":\"/Users/private\"}]}");
            Assert.AreEqual(0, RunAsync(() => repository.LoadAsync(CancellationToken.None)).Entries.Count);
        }

        [Test]
        public void ClearResolvedAuditHistoryRequiresConfirmation()
        {
            var directory = TempDirectory();
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory, Conflict("conflict-1", "HIGH", "MEDIUM", "2026-05-15", "2026-05-16"));
            RunAsync(() => service.ApplyConflictMergePolicyAsync("conflict-1", SafeConflictMergePolicy.MarkResolvedOnly, true, CancellationToken.None));

            var denied = RunAsync(() => service.ClearResolvedConflictAuditHistoryAsync(false, CancellationToken.None));
            var cleared = RunAsync(() => service.ClearResolvedConflictAuditHistoryAsync(true, CancellationToken.None));
            var history = RunAsync(() => service.GetConflictAuditHistoryAsync(CancellationToken.None));

            Assert.IsFalse(denied.IsSuccess);
            Assert.IsTrue(cleared.IsSuccess);
            Assert.AreEqual(0, history.TotalCount);
        }

        [Test]
        public void ForceRetryBypassesNextAttemptOnlyWithConfirmationAndRespectsBlocks()
        {
            var directory = TempDirectory();
            var queue = new SafeSyncRetryQueueRepository(directory);
            RunAsync(() => queue.SaveAsync(new SafeSyncRetryQueueState
            {
                Entries = new List<SafeSyncRetryQueueEntry>
                {
                    new SafeSyncRetryQueueEntry { QueueEntryId = "later", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, ClientSessionIds = new List<string> { "client-session-1" }, Status = SafeSyncRetryQueueEntryStatus.Pending, NextAttemptAt = DateTimeOffset.UtcNow.AddHours(1), LastSafeErrorCode = SafeSyncApiError.ServerUnavailable },
                    new SafeSyncRetryQueueEntry { QueueEntryId = "paused", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, ClientSessionIds = new List<string> { "client-session-1" }, Status = SafeSyncRetryQueueEntryStatus.Paused, LastSafeErrorCode = SafeSyncApiError.AuthRequired },
                    new SafeSyncRetryQueueEntry { QueueEntryId = "unsafe", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, ClientSessionIds = new List<string> { "client-session-1" }, Status = SafeSyncRetryQueueEntryStatus.Failed, LastSafeErrorCode = SafeSyncApiError.PrivacyGuardBlockedPayload },
                    new SafeSyncRetryQueueEntry { QueueEntryId = "permanent", OperationType = SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS, ClientSessionIds = new List<string> { "client-session-1" }, Status = SafeSyncRetryQueueEntryStatus.Failed, LastSafeErrorCode = "PERMANENT_DENIED" }
                }
            }, CancellationToken.None));
            var service = CreateService(new FakeRepository(), new FakeApiClient(), directory, Conflict("conflict-1", "HIGH", "MEDIUM", "2026-05-15", "2026-05-16"));

            Assert.IsFalse(RunAsync(() => service.ForceRetryEntryAsync("later", false, CancellationToken.None)).IsSuccess);
            Assert.AreEqual(SafeSyncApiError.ForcedRetrySkippedAuthPaused, RunAsync(() => service.ForceRetryEntryAsync("paused", true, CancellationToken.None)).ErrorCode);
            Assert.AreEqual(SafeSyncApiError.ForcedRetryBlockedUnsafe, RunAsync(() => service.ForceRetryEntryAsync("unsafe", true, CancellationToken.None)).ErrorCode);
            Assert.AreEqual(SafeSyncApiError.ForcedRetryBlockedPermanentFailure, RunAsync(() => service.ForceRetryEntryAsync("permanent", true, CancellationToken.None)).ErrorCode);

            var forced = RunAsync(() => service.ForceRetryEntryAsync("later", true, CancellationToken.None));
            Assert.IsTrue(forced.IsSuccess, forced.ErrorCode);
            Assert.AreEqual(SafeSyncApiError.ForcedRetrySucceeded, forced.ErrorCode);
        }

        [Test]
        public void ConflictHistoryFormatterAndPolicyLabelsArePrivacySafe()
        {
            var viewModel = ViewModelWithAudit();
            var text = BootstrapUiTextFormatter.ConflictStatus(viewModel) + "\n" + BootstrapUiTextFormatter.ConflictAuditHistory(viewModel);

            Assert.That(text, Does.Contain("Prefer Higher Confidence"));
            Assert.That(text, Does.Contain("Conflict History"));
            Assert.That(text, Does.Not.Contain("{\""));
            Assert.That(text, Does.Not.Contain("/Users/"));
            Assert.That(text, Does.Not.Contain("prompt"));
            Assert.That(text, Does.Not.Contain("response"));
            Assert.That(text, Does.Not.Contain("command"));
        }

        private static ApprovedActivityAnalysisViewModel ViewModelWithAudit()
        {
            var vm = (ApprovedActivityAnalysisViewModel)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(ApprovedActivityAnalysisViewModel));
            typeof(ApprovedActivityAnalysisViewModel).GetProperty(nameof(ApprovedActivityAnalysisViewModel.ConflictSummary)).SetValue(vm, new SafeSyncConflictSummary
            {
                UnresolvedCount = 1,
                SafeConflicts = new List<SafeSyncConflict> { Conflict("conflict-1", "HIGH", "MEDIUM", "2026-05-15", "2026-05-16") }
            });
            typeof(ApprovedActivityAnalysisViewModel).GetProperty(nameof(ApprovedActivityAnalysisViewModel.ConflictAuditSummary)).SetValue(vm, new SafeConflictAuditSummary
            {
                TotalCount = 1,
                ResolvedCount = 1,
                SafeEntries = new List<SafeConflictAuditEntry>
                {
                    new SafeConflictAuditEntry
                    {
                        Action = "applyMergePolicy",
                        Policy = SafeConflictMergePolicy.KeepLocal,
                        ResultStatus = "resolved",
                        UserMessageCode = SafeSyncApiError.ConflictKeepLocalQueued,
                        SafeDiffFieldNames = new List<string> { "confidence" }
                    }
                }
            });
            return vm;
        }

        private static SafeSyncService CreateService(FakeRepository repository, FakeApiClient api, string directory, SafeSyncConflict conflict)
        {
            RunAsync(() => new SafeSyncConflictRepository(directory).SaveAsync(new SafeSyncConflictState { Conflicts = new List<SafeSyncConflict> { conflict } }, CancellationToken.None));
            return new SafeSyncService(
                repository,
                api,
                null,
                null,
                null,
                new SafeSyncRetryQueueRepository(directory),
                new SafeSyncRetryPolicy(),
                new SafeSyncConflictRepository(directory),
                new SafeSyncTombstoneRepository(directory),
                new SafeSyncLocalStateRepository(directory),
                new SafeConflictAuditRepository(directory));
        }

        private static SafeSyncConflict Conflict(string conflictId, string localConfidence, string remoteConfidence, string localDay, string remoteDay, string localCategory = "WORK_FEATURE:ONE", string remoteCategory = "WORK_FEATURE:ONE", int warningLocal = 1, int warningRemote = 1)
        {
            return new SafeSyncConflict
            {
                ConflictId = conflictId,
                ClientSessionId = "client-session-1",
                ServerSessionId = "server-client-session-1",
                ConflictType = SafeSyncConflictType.RemoteDifferent,
                SafeErrorCode = SafeSyncApiError.ConflictDetected,
                SafeLocalSummary = new SafeSyncSessionSafeSummary { ClientSessionId = "client-session-1", SourceProvider = "CODEX", DayBucket = localDay, TimeBucket = "HOUR_02", ActivityCategory = "WORK_FEATURE", Confidence = localConfidence, WarningCount = warningLocal, CategoryBucketSummary = localCategory, ToolBucketSummary = "TOOL_EDIT:FEW", LanguageBucketSummary = "LANG_CSHARP:FEW", ChangeCountBucket = "FEW", LineCountBucket = "FEW", SessionCountBucket = "ONE", InteractionCountBucket = "FEW", AnalyzerVersion = "analyzer.1", ParserVersion = "parser.1" },
                SafeRemoteSummary = new SafeSyncSessionSafeSummary { ClientSessionId = "client-session-1", ServerSessionId = "server-client-session-1", SourceProvider = "CODEX", DayBucket = remoteDay, TimeBucket = "HOUR_02", ActivityCategory = "WORK_FEATURE", Confidence = remoteConfidence, WarningCount = warningRemote, CategoryBucketSummary = remoteCategory, ToolBucketSummary = "TOOL_EDIT:FEW", LanguageBucketSummary = "LANG_CSHARP:FEW", ChangeCountBucket = "FEW", LineCountBucket = "FEW", SessionCountBucket = "ONE", InteractionCountBucket = "FEW", AnalyzerVersion = "analyzer.1", ParserVersion = "parser.1" },
                SafeRemoteSession = Phase24RemoteToLocalApplyTests.Remote("client-session-1"),
                SafeDiffSummary = new SafeSessionDiffSummary { IsDifferent = true, ChangedSafeFieldNames = new List<string> { "confidence", "dayBucket" } },
                ResolutionStatus = SafeSyncConflictResolutionStatus.Unresolved
            };
        }

        private static SaveData SaveDataWithSession(string id, ProviderConfidence confidence, string day)
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = id,
                SourceProvider = "CODEX",
                AgentType = AgentType.Codex,
                WorkType = WorkType.Feature,
                StartedAt = new DateTimeOffset(2026, 5, 15, 2, 0, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 5, 15, 2, 30, 0, TimeSpan.Zero),
                Confidence = confidence,
                GitChangeSummary = new GitChangeSummary { ChangedFileCountBucket = CountBucket.Small, AddedLineBucket = LineChangeBucket.Small, CommitCountBucket = CountBucket.One, ProjectPathHash = "abcdefabcdefabcd" },
                AgentActivitySummary = new AgentActivitySummary { DayBucket = day, SessionCountBucket = CountBucket.One, InteractionCountBucket = CountBucket.Small },
                Warnings = new List<string> { "SAFE_WARNING" }
            });
            return saveData;
        }

        private static string TempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string FindRepoRoot()
        {
            var current = Directory.GetCurrentDirectory();
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, ".gitignore")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            Assert.Fail("Could not locate repository root.");
            return Directory.GetCurrentDirectory();
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
