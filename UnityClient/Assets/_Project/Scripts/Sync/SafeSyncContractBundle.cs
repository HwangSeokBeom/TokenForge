using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class SafeSyncContractBundle
    {
        [JsonProperty("bundleVersion")]
        public int BundleVersion { get; set; } = 1;

        [JsonProperty("clientName")]
        public string ClientName { get; set; } = "TokenForgeUnityClient";

        [JsonProperty("clientSchemaVersion")]
        public int ClientSchemaVersion { get; set; } = 1;

        [JsonProperty("generatedAt")]
        public string GeneratedAt { get; set; } = string.Empty;

        [JsonProperty("safeSyncMapperVersion")]
        public string SafeSyncMapperVersion { get; set; } = "unity-safe-sync-mapper.contract-v1";

        [JsonProperty("supportedSourceProviders")]
        public List<string> SupportedSourceProviders { get; set; } = new List<string>();

        [JsonProperty("supportedSchemaVersions")]
        public List<int> SupportedSchemaVersions { get; set; } = new List<int>();

        [JsonProperty("mixedBehavior")]
        public string MixedBehavior { get; set; } = "WHOLE_REQUEST_REJECTED_BY_DTO_VALIDATION";

        [JsonProperty("fixtures")]
        public SafeSyncContractBundleFixtureSet Fixtures { get; set; } = new SafeSyncContractBundleFixtureSet();

        [JsonProperty("privacyExpectations")]
        public SafeSyncContractPrivacyExpectations PrivacyExpectations { get; set; } = new SafeSyncContractPrivacyExpectations();
    }

    [Serializable]
    public sealed class SafeSyncContractBundleFixtureSet
    {
        [JsonProperty("valid")]
        public SafeActivitySessionsContractRequest Valid { get; set; } = new SafeActivitySessionsContractRequest();

        [JsonProperty("mixed")]
        public SafeActivitySessionsContractRequest Mixed { get; set; } = new SafeActivitySessionsContractRequest();

        [JsonProperty("unsafe")]
        public UnsafeSafeSyncContractRequest Unsafe { get; set; } = new UnsafeSafeSyncContractRequest();
    }

    [Serializable]
    public sealed class SafeSyncContractPrivacyExpectations
    {
        [JsonProperty("approvedLocationsAreLocalOnly")]
        public bool ApprovedLocationsAreLocalOnly { get; set; } = true;

        [JsonProperty("rawFieldsNeverSynced")]
        public List<string> RawFieldsNeverSynced { get; set; } = new List<string>();
    }

    [Serializable]
    public sealed class SafeActivitySessionsContractRequest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("clientSyncId", NullValueHandling = NullValueHandling.Ignore)]
        public string ClientSyncId { get; set; } = string.Empty;

        [JsonProperty("requestId", NullValueHandling = NullValueHandling.Ignore)]
        public string RequestId { get; set; }

        [JsonProperty("sessions")]
        public List<SafeActivitySessionContractDto> Sessions { get; set; } = new List<SafeActivitySessionContractDto>();
    }

    [Serializable]
    public sealed class SafeActivitySessionContractDto
    {
        [JsonProperty("clientSessionId")]
        public string ClientSessionId { get; set; } = string.Empty;

        [JsonProperty("sourceProvider")]
        public string SourceProvider { get; set; } = "UNKNOWN_AGENT";

        [JsonProperty("dayBucket")]
        public string DayBucket { get; set; } = string.Empty;

        [JsonProperty("timeBucket", NullValueHandling = NullValueHandling.Ignore)]
        public string TimeBucket { get; set; }

        [JsonProperty("confidence")]
        public string Confidence { get; set; } = "LOW";

        [JsonProperty("warningIds", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> WarningIds { get; set; } = new List<string>();

        [JsonProperty("analyzerVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string AnalyzerVersion { get; set; }

        [JsonProperty("parserVersion", NullValueHandling = NullValueHandling.Ignore)]
        public string ParserVersion { get; set; }

        [JsonProperty("hashedRepositoryId", NullValueHandling = NullValueHandling.Ignore)]
        public string HashedRepositoryId { get; set; }

        [JsonProperty("changeCountBucket", NullValueHandling = NullValueHandling.Ignore)]
        public string ChangeCountBucket { get; set; }

        [JsonProperty("lineCountBucket", NullValueHandling = NullValueHandling.Ignore)]
        public string LineCountBucket { get; set; }

        [JsonProperty("commitCountBucket", NullValueHandling = NullValueHandling.Ignore)]
        public string CommitCountBucket { get; set; }

        [JsonProperty("sessionCountBucket", NullValueHandling = NullValueHandling.Ignore)]
        public string SessionCountBucket { get; set; }

        [JsonProperty("interactionCountBucket", NullValueHandling = NullValueHandling.Ignore)]
        public string InteractionCountBucket { get; set; }

        [JsonProperty("activityCategory", NullValueHandling = NullValueHandling.Ignore)]
        public string ActivityCategory { get; set; }

        [JsonProperty("durationBucket", NullValueHandling = NullValueHandling.Ignore)]
        public string DurationBucket { get; set; }

        [JsonProperty("categoryBuckets", NullValueHandling = NullValueHandling.Ignore)]
        public List<SafeBucketContractDto> CategoryBuckets { get; set; } = new List<SafeBucketContractDto>();

        [JsonProperty("languageBuckets", NullValueHandling = NullValueHandling.Ignore)]
        public List<SafeBucketContractDto> LanguageBuckets { get; set; } = new List<SafeBucketContractDto>();

        [JsonProperty("toolBuckets", NullValueHandling = NullValueHandling.Ignore)]
        public List<SafeBucketContractDto> ToolBuckets { get; set; } = new List<SafeBucketContractDto>();
    }

    [Serializable]
    public sealed class SafeBucketContractDto
    {
        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;

        [JsonProperty("countBucket")]
        public string CountBucket { get; set; } = "FEW";
    }

    [Serializable]
    public sealed class UnsafeSafeSyncContractRequest
    {
        [JsonProperty("fixtureSafety")]
        public string FixtureSafety { get; set; } = "UNSAFE_FOR_SERVER_REJECTION_ONLY";

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [JsonProperty("clientSyncId")]
        public string ClientSyncId { get; set; } = "unity-sync-v1-unsafe-001";

        [JsonProperty("approvedLocations")]
        public List<string> ApprovedLocations { get; set; } = new List<string>();

        [JsonProperty("sessions")]
        public List<UnsafeSafeSyncContractSession> Sessions { get; set; } = new List<UnsafeSafeSyncContractSession>();
    }

    [Serializable]
    public sealed class UnsafeSafeSyncContractSession
    {
        [JsonProperty("clientSessionId")]
        public string ClientSessionId { get; set; } = string.Empty;

        [JsonProperty("sourceProvider")]
        public string SourceProvider { get; set; } = "GIT";

        [JsonProperty("dayBucket")]
        public string DayBucket { get; set; } = "2026-05-14";

        [JsonProperty("confidence")]
        public string Confidence { get; set; } = "HIGH";

        [JsonProperty("analyzerVersion")]
        public string AnalyzerVersion { get; set; } = "unity-agent-analyzer.1";

        [JsonProperty("parserVersion")]
        public string ParserVersion { get; set; } = "safe-sync.1";

        [JsonProperty("repoName", NullValueHandling = NullValueHandling.Ignore)]
        public string RepoName { get; set; }

        [JsonProperty("branchName", NullValueHandling = NullValueHandling.Ignore)]
        public string BranchName { get; set; }

        [JsonProperty("fileName", NullValueHandling = NullValueHandling.Ignore)]
        public string FileName { get; set; }

        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public string Command { get; set; }

        [JsonProperty("prompt", NullValueHandling = NullValueHandling.Ignore)]
        public string Prompt { get; set; }

        [JsonProperty("response", NullValueHandling = NullValueHandling.Ignore)]
        public string Response { get; set; }

        [JsonProperty("rawLog", NullValueHandling = NullValueHandling.Ignore)]
        public string RawLog { get; set; }

        [JsonProperty("apiKey", NullValueHandling = NullValueHandling.Ignore)]
        public string ApiKey { get; set; }

        [JsonProperty("sourceText", NullValueHandling = NullValueHandling.Ignore)]
        public string SourceText { get; set; }

        [JsonProperty("categoryBuckets")]
        public List<SafeBucketContractDto> CategoryBuckets { get; set; } = new List<SafeBucketContractDto>();

        [JsonProperty("languageBuckets")]
        public List<SafeBucketContractDto> LanguageBuckets { get; set; } = new List<SafeBucketContractDto>();

        [JsonProperty("toolBuckets")]
        public List<SafeBucketContractDto> ToolBuckets { get; set; } = new List<SafeBucketContractDto>();
    }
}
