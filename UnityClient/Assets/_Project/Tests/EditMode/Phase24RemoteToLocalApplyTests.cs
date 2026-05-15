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
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase24RemoteToLocalApplyTests
    {
        private sealed class FakeRepository : ILocalSaveDataRepository
        {
            public SaveData Current { get; set; } = SaveData.CreateDefault();
            public int SaveCount { get; private set; }
            public Task<SaveData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
            {
                SaveCount += 1;
                Current = saveData;
                return Task.FromResult(Result.Success());
            }
        }

        [Test]
        public void ValidatorAcceptsSafeRemoteSessionAndRejectsUnsafeShapes()
        {
            var validator = new RemoteSafeSessionValidator();

            Assert.IsTrue(validator.Validate(Remote("client-session-1")).IsSuccess);
            Assert.AreEqual(SafeSyncApiError.UnknownSchemaVersion, validator.Validate(Remote("client-session-1", schema: 99)).ErrorCode);
            Assert.AreEqual(SafeSyncApiError.InvalidBucketValue, validator.Validate(Remote("client-session-1", category: "WORK_/Users/private")).ErrorCode);
            Assert.AreEqual(SafeSyncApiError.KeepRemoteUnsafeRejected, validator.ValidateJson("{\"clientSessionId\":\"client-session-1\",\"dayBucket\":\"2026-05-15\",\"sourceProvider\":\"CODEX\",\"repoName\":\"SecretRepo\"}").ErrorCode);
        }

        [Test]
        public void MapperCreatesAggregateOnlyLocalSession()
        {
            var session = new SafeSyncRemoteToLocalMapper().ToLocalSession(Remote("client-session-1"));

            Assert.AreEqual("client-session-1", session.SessionId);
            Assert.AreEqual("CODEX", session.SourceProvider);
            Assert.AreEqual(WorkType.Feature, session.WorkType);
            Assert.AreEqual(ProviderConfidence.High, session.Confidence);
            Assert.AreEqual("2026-05-15", session.AgentActivitySummary.DayBucket);
            Assert.AreEqual(CountBucket.One, session.AgentActivitySummary.SessionCountBucket);
            Assert.AreEqual(LineChangeBucket.Small, session.GitChangeSummary.AddedLineBucket);

            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(session).IsSuccess);
        }

        [Test]
        public void ApplicatorWritesValidatedRemoteAggregateAndDoesNotTouchApprovedLocations()
        {
            var repository = new FakeRepository();
            repository.Current.ConnectedProjects.Add(new ConnectedProject { ProjectAlias = "Local only", ProjectPathHash = "abcdefabcdefabcd" });
            var applicator = new SafeRemoteSessionApplicator(repository);

            var result = RunAsync(() => applicator.ApplyAsync(Remote("client-session-1"), CancellationToken.None));

            Assert.IsTrue(result.IsApplied, result.ErrorCode);
            Assert.AreEqual(1, repository.SaveCount);
            Assert.AreEqual("client-session-1", repository.Current.WorkSessionSummaries.Single().SessionId);
            Assert.AreEqual(1, repository.Current.ConnectedProjects.Count);
            Assert.AreEqual("Local only", repository.Current.ConnectedProjects.Single().ProjectAlias);
        }

        [Test]
        public void ApplicatorRejectsUnknownSchemaWithoutWritingSaveData()
        {
            var repository = new FakeRepository();
            var applicator = new SafeRemoteSessionApplicator(repository);

            var result = RunAsync(() => applicator.ApplyAsync(Remote("client-session-1", schema: 2), CancellationToken.None));

            Assert.IsFalse(result.IsApplied);
            Assert.AreEqual(SafeSyncApiError.UnknownSchemaVersion, result.ErrorCode);
            Assert.AreEqual(0, repository.SaveCount);
            Assert.AreEqual(0, repository.Current.WorkSessionSummaries.Count);
        }

        internal static RemoteSafeActivitySessionDto Remote(string clientSessionId, int schema = 1, string category = "WORK_FEATURE")
        {
            return new RemoteSafeActivitySessionDto
            {
                Id = "server-" + clientSessionId,
                ClientSessionId = clientSessionId,
                SourceProvider = "CODEX",
                DayBucket = "2026-05-15",
                TimeBucket = "HOUR_02",
                Confidence = "HIGH",
                WarningIds = new List<string> { "SAFE_WARNING" },
                AnalyzerVersion = "analyzer.1",
                ParserVersion = "parser.1",
                AggregateSchemaVersion = schema,
                HashedRepositoryId = "abcdefabcdefabcd",
                ChangeCountBucket = "FEW",
                LineCountBucket = "FEW",
                CommitCountBucket = "ONE",
                SessionCountBucket = "ONE",
                InteractionCountBucket = "FEW",
                ActivityCategory = category,
                DurationBucket = "M_5_15",
                CategoryBuckets = new List<SafeBucketContractDto> { new SafeBucketContractDto { Key = category, CountBucket = "ONE" } },
                LanguageBuckets = new List<SafeBucketContractDto> { new SafeBucketContractDto { Key = "LANG_CSHARP", CountBucket = "FEW" } },
                ToolBuckets = new List<SafeBucketContractDto> { new SafeBucketContractDto { Key = "TOOL_EDIT", CountBucket = "FEW" } }
            };
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
