using System;
using System.Collections.Generic;
using System.Linq;
using TokenForge.Client.Domain;

namespace TokenForge.Client.Sync
{
    public static class SafeSyncContractBundleFixtures
    {
        public static readonly DateTimeOffset StableGeneratedAt = new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero);

        public static SafeSyncContractBundle CreateBundle(DateTimeOffset? generatedAt = null, SafeSyncMapper mapper = null)
        {
            mapper = mapper ?? new SafeSyncMapper();
            var valid = CreateValidFixture(mapper);
            return new SafeSyncContractBundle
            {
                BundleVersion = 1,
                ClientName = "TokenForgeUnityClient",
                ClientSchemaVersion = 1,
                GeneratedAt = FormatTimestamp(generatedAt ?? StableGeneratedAt),
                SafeSyncMapperVersion = "unity-safe-sync-mapper.contract-v1",
                SupportedSourceProviders = new List<string>
                {
                    "MANUAL",
                    "GIT",
                    "CLAUDE",
                    "CODEX",
                    "UNKNOWN_AGENT"
                },
                SupportedSchemaVersions = new List<int> { 1 },
                MixedBehavior = "WHOLE_REQUEST_REJECTED_BY_DTO_VALIDATION",
                Fixtures = new SafeSyncContractBundleFixtureSet
                {
                    Valid = valid,
                    Mixed = CreateMixedFixture(valid),
                    Unsafe = CreateUnsafeFixture()
                },
                PrivacyExpectations = new SafeSyncContractPrivacyExpectations
                {
                    ApprovedLocationsAreLocalOnly = true,
                    RawFieldsNeverSynced = CreateRawFieldsNeverSynced()
                }
            };
        }

        public static SafeActivitySessionsContractRequest CreateValidFixture(SafeSyncMapper mapper = null)
        {
            mapper = mapper ?? new SafeSyncMapper();
            return mapper.ToActivitySessionsContractRequest(CreateDeterministicSafeSaveData(), "unity-sync-v1-001");
        }

        public static SaveData CreateDeterministicSafeSaveData()
        {
            var saveData = SaveData.CreateDefault();
            saveData.SyncState.SyncVersion = 1;
            saveData.PrivacyPreferences.CloudSyncEnabled = true;
            saveData.CharacterProfile.CharacterId = "character-contract-safe";
            saveData.CharacterProfile.DisplayName = "Token";

            saveData.WorkSessionSummaries = new List<AgentWorkSession>
            {
                CreateManualSession(),
                CreateGitSession(),
                CreateClaudeSession(),
                CreateCodexSession(),
                CreateUnknownAgentSession()
            };

            return saveData;
        }

        public static SafeActivitySessionsContractRequest CreateMixedFixture(SafeActivitySessionsContractRequest validFixture)
        {
            var validSession = CloneSession(validFixture.Sessions.First(session => session.SourceProvider == "GIT"));
            validSession.ClientSessionId = "unity-mixed-git-001";
            validSession.TimeBucket = "HOUR_14";

            var invalidSession = CloneSession(validFixture.Sessions.First(session => session.SourceProvider == "CODEX"));
            invalidSession.ClientSessionId = "unity-mixed-invalid-bucket-001";
            invalidSession.TimeBucket = "HOUR_15";
            invalidSession.SessionCountBucket = "EVERYTHING";

            return new SafeActivitySessionsContractRequest
            {
                SchemaVersion = 1,
                ClientSyncId = "unity-sync-v1-mixed-001",
                Sessions = new List<SafeActivitySessionContractDto>
                {
                    validSession,
                    invalidSession
                }
            };
        }

        public static UnsafeSafeSyncContractRequest CreateUnsafeFixture()
        {
            return new UnsafeSafeSyncContractRequest
            {
                FixtureSafety = "UNSAFE_FOR_SERVER_REJECTION_ONLY",
                SchemaVersion = 1,
                ClientSyncId = "unity-sync-v1-unsafe-001",
                ApprovedLocations = new List<string> { "/Users/example/private-project" },
                Sessions = new List<UnsafeSafeSyncContractSession>
                {
                    new UnsafeSafeSyncContractSession
                    {
                        ClientSessionId = "unity-unsafe-path-001",
                        SourceProvider = "GIT",
                        DayBucket = "2026-05-14",
                        Confidence = "HIGH",
                        AnalyzerVersion = "unity-git-analyzer.1",
                        ParserVersion = "safe-sync.1",
                        RepoName = "private-project",
                        BranchName = "customer-secret-branch",
                        FileName = "CustomerSecret.cs",
                        CategoryBuckets = { Bucket("WORK_FEATURE", "FEW") },
                        LanguageBuckets = { Bucket("LANG_CSHARP", "MANY") },
                        ToolBuckets = { Bucket("TOOL_GIT_COMMIT", "ONE") }
                    },
                    new UnsafeSafeSyncContractSession
                    {
                        ClientSessionId = "unity-unsafe-agent-001",
                        SourceProvider = "CODEX",
                        DayBucket = "2026-05-14",
                        Confidence = "HIGH",
                        AnalyzerVersion = "unity-agent-analyzer.1",
                        ParserVersion = "codex-parser.1",
                        Command = "git status --short",
                        Prompt = "summarize the private implementation",
                        Response = "private implementation details",
                        RawLog = "private raw log content",
                        ApiKey = "sk-abcdefghijklmnopqrstuvwxyz123456",
                        SourceText = "function unsafeExample() {\n  const value = true;\n  return value;\n}\n",
                        CategoryBuckets = { Bucket("WORK_FEATURE", "FEW") },
                        LanguageBuckets = { Bucket("LANG_TYPESCRIPT", "MANY") },
                        ToolBuckets = { Bucket("TOOL_TEST", "FEW") }
                    }
                }
            };
        }

        public static List<string> CreateRawFieldsNeverSynced()
        {
            return new List<string>
            {
                "prompt",
                "rawPrompt",
                "response",
                "rawResponse",
                "rawDiff",
                "code",
                "rawCode",
                "sourceCode",
                "source",
                "sourceText",
                "snippet",
                "log",
                "rawLog",
                "claudeLog",
                "codexLog",
                "terminalOutput",
                "stdout",
                "stderr",
                "absolutePath",
                "filePath",
                "filename",
                "fileName",
                "path",
                "pathRaw",
                "repoName",
                "repositoryName",
                "remoteUrl",
                "gitRemote",
                "gitRemoteUrl",
                "branchName",
                "rawBranchName",
                "branchNameRaw",
                "commitMessage",
                "rawCommitMessage",
                "commitMessageRaw",
                "diff",
                "patch",
                "command",
                "commandText",
                "apiKey",
                "secret",
                "passwordRaw",
                "password",
                "username",
                "authorization",
                "tokenRaw",
                "rawToken",
                "refreshToken",
                "apiToken",
                "accessToken",
                "secretToken",
                "Authorization",
                "Bearer",
                "token",
                "approvedLocation",
                "approvedLocations",
                "approvedLocationPath",
                "approvedLocationSettings",
                "localPath",
                "localOnlyPath",
                "localApprovedLocations"
            };
        }

        private static AgentWorkSession CreateManualSession()
        {
            return new AgentWorkSession
            {
                SessionId = "unity-manual-20260514-001",
                AgentType = AgentType.ManualFallback,
                WorkType = WorkType.Docs,
                StartedAt = AtHour(13),
                EndedAt = AtHour(13).AddMinutes(45),
                TokenUsageBucket = TokenUsageBucket.None,
                ActionSummary = new AgentActionSummary
                {
                    PromptCount = 0,
                    ToolCallCount = 0,
                    FileEditCount = 0,
                    DurationBucket = DurationBucket.FifteenTo60Minutes
                },
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = "aaaabbbbccccdddd"
                },
                ResultStatus = ResultStatus.Succeeded,
                Confidence = ProviderConfidence.Medium,
                SourceProvider = "MANUAL",
                ParserVersion = "manual-parser.1",
                Warnings = new List<string> { "USER_ENTERED_SUMMARY" },
                UserReviewed = true
            };
        }

        private static AgentWorkSession CreateGitSession()
        {
            return new AgentWorkSession
            {
                SessionId = "unity-git-20260514-001",
                AgentType = AgentType.GitOnly,
                WorkType = WorkType.Feature,
                StartedAt = AtHour(9),
                EndedAt = AtHour(9).AddMinutes(30),
                TokenUsageBucket = TokenUsageBucket.None,
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 4,
                    ChangedFileCountBucket = CountBucket.Small,
                    AddedLineBucket = LineChangeBucket.Large,
                    DeletedLineBucket = LineChangeBucket.Small,
                    CommitCountBucket = CountBucket.One,
                    ProjectPathHash = "0123456789abcdef",
                    AnalysisTimeBucket = "2026-05-14",
                    AnalyzerVersion = "unity-git-analyzer.1",
                    PrivacyWarnings = new List<string> { "LOW_CONFIDENCE_RANGE" }
                },
                ResultStatus = ResultStatus.Succeeded,
                Confidence = ProviderConfidence.High,
                SourceProvider = "GIT",
                ParserVersion = "safe-sync.1",
                UserReviewed = true
            };
        }

        private static AgentWorkSession CreateClaudeSession()
        {
            return new AgentWorkSession
            {
                SessionId = "unity-claude-20260514-001",
                AgentType = AgentType.ClaudeCode,
                WorkType = WorkType.Refactor,
                StartedAt = AtHour(10),
                EndedAt = AtHour(10).AddMinutes(35),
                TokenUsageBucket = TokenUsageBucket.Medium,
                ActionSummary = new AgentActionSummary
                {
                    PromptCount = 3,
                    ToolCallCount = 6,
                    FileEditCount = 2,
                    TestRunCount = 1
                },
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = "1111222233334444"
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Claude,
                    SourceIdentifierHash = "aaaabbbb11112222",
                    DayBucket = "2026-05-14",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Large,
                    EstimatedCodingActivityBucket = CountBucket.Medium,
                    ToolUsageCategoryBuckets = new List<AgentToolUsageCategoryBucket>
                    {
                        new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.CodeEditing, CountBucket = CountBucket.Small },
                        new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.TestRun, CountBucket = CountBucket.One }
                    },
                    LanguageCategoryBuckets = new List<AgentLanguageCategoryBucket>
                    {
                        new AgentLanguageCategoryBucket { Category = AgentLanguageCategory.CSharp, CountBucket = CountBucket.Small }
                    },
                    WarningIds = new List<string> { "PROVIDER_LOG_PARTIAL" },
                    AnalyzerVersion = "unity-agent-analyzer.1"
                },
                ResultStatus = ResultStatus.PartiallySucceeded,
                Confidence = ProviderConfidence.Medium,
                SourceProvider = "CLAUDE",
                ParserVersion = "claude-parser.1",
                UserReviewed = true
            };
        }

        private static AgentWorkSession CreateCodexSession()
        {
            return new AgentWorkSession
            {
                SessionId = "unity-codex-20260514-001",
                AgentType = AgentType.Codex,
                WorkType = WorkType.Test,
                StartedAt = AtHour(11),
                EndedAt = AtHour(11).AddMinutes(50),
                TokenUsageBucket = TokenUsageBucket.Large,
                ActionSummary = new AgentActionSummary
                {
                    PromptCount = 4,
                    ToolCallCount = 8,
                    BuildRunCount = 1,
                    TestRunCount = 2
                },
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = "abcdef0123456789"
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Codex,
                    SourceIdentifierHash = "bbbbcccc33334444",
                    DayBucket = "2026-05-14",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Large,
                    EstimatedCodingActivityBucket = CountBucket.Large,
                    ToolUsageCategoryBuckets = new List<AgentToolUsageCategoryBucket>
                    {
                        new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.BuildRun, CountBucket = CountBucket.One },
                        new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.TestRun, CountBucket = CountBucket.Small }
                    },
                    LanguageCategoryBuckets = new List<AgentLanguageCategoryBucket>
                    {
                        new AgentLanguageCategoryBucket { Category = AgentLanguageCategory.TypeScript, CountBucket = CountBucket.Large }
                    },
                    WarningIds = new List<string> { "SAFE_AGGREGATE_ONLY" },
                    AnalyzerVersion = "unity-agent-analyzer.1"
                },
                ResultStatus = ResultStatus.Succeeded,
                Confidence = ProviderConfidence.High,
                SourceProvider = "CODEX",
                ParserVersion = "codex-parser.1",
                UserReviewed = true
            };
        }

        private static AgentWorkSession CreateUnknownAgentSession()
        {
            return new AgentWorkSession
            {
                SessionId = "unity-unknown-agent-20260514-001",
                AgentType = AgentType.Unknown,
                WorkType = WorkType.Unknown,
                StartedAt = AtHour(12),
                EndedAt = AtHour(12).AddMinutes(20),
                TokenUsageBucket = TokenUsageBucket.Small,
                GitChangeSummary = new GitChangeSummary
                {
                    ProjectPathHash = "9999888877776666"
                },
                AgentActivitySummary = new AgentActivitySummary
                {
                    ProviderType = AgentProviderType.Unknown,
                    SourceIdentifierHash = "ccccdddd55556666",
                    DayBucket = "2026-05-14",
                    SessionCountBucket = CountBucket.One,
                    InteractionCountBucket = CountBucket.Small,
                    EstimatedCodingActivityBucket = CountBucket.Small,
                    ToolUsageCategoryBuckets = new List<AgentToolUsageCategoryBucket>
                    {
                        new AgentToolUsageCategoryBucket { Category = AgentToolUsageCategory.Unknown, CountBucket = CountBucket.Small }
                    },
                    LanguageCategoryBuckets = new List<AgentLanguageCategoryBucket>
                    {
                        new AgentLanguageCategoryBucket { Category = AgentLanguageCategory.Unknown, CountBucket = CountBucket.Small }
                    },
                    WarningIds = new List<string> { "UNKNOWN_PROVIDER" },
                    AnalyzerVersion = "unity-agent-analyzer.1"
                },
                ResultStatus = ResultStatus.PartiallySucceeded,
                Confidence = ProviderConfidence.Low,
                SourceProvider = "UNKNOWN_AGENT",
                ParserVersion = "unknown-agent-parser.1",
                UserReviewed = true
            };
        }

        private static SafeActivitySessionContractDto CloneSession(SafeActivitySessionContractDto session)
        {
            return new SafeActivitySessionContractDto
            {
                ClientSessionId = session.ClientSessionId,
                SourceProvider = session.SourceProvider,
                DayBucket = session.DayBucket,
                TimeBucket = session.TimeBucket,
                Confidence = session.Confidence,
                WarningIds = new List<string>(session.WarningIds ?? new List<string>()),
                AnalyzerVersion = session.AnalyzerVersion,
                ParserVersion = session.ParserVersion,
                HashedRepositoryId = session.HashedRepositoryId,
                ChangeCountBucket = session.ChangeCountBucket,
                LineCountBucket = session.LineCountBucket,
                CommitCountBucket = session.CommitCountBucket,
                SessionCountBucket = session.SessionCountBucket,
                InteractionCountBucket = session.InteractionCountBucket,
                ActivityCategory = session.ActivityCategory,
                DurationBucket = session.DurationBucket,
                CategoryBuckets = CloneBuckets(session.CategoryBuckets),
                LanguageBuckets = CloneBuckets(session.LanguageBuckets),
                ToolBuckets = CloneBuckets(session.ToolBuckets)
            };
        }

        private static List<SafeBucketContractDto> CloneBuckets(List<SafeBucketContractDto> buckets)
        {
            return (buckets ?? new List<SafeBucketContractDto>())
                .Select(bucket => Bucket(bucket.Key, bucket.CountBucket))
                .ToList();
        }

        private static SafeBucketContractDto Bucket(string key, string countBucket)
        {
            return new SafeBucketContractDto
            {
                Key = key,
                CountBucket = countBucket
            };
        }

        private static DateTimeOffset AtHour(int hour)
        {
            return new DateTimeOffset(2026, 5, 14, hour, 0, 0, TimeSpan.Zero);
        }

        private static string FormatTimestamp(DateTimeOffset timestamp)
        {
            return timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
        }
    }
}
