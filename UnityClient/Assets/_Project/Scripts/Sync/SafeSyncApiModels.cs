using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class SafeSyncApiResult<T>
    {
        public bool IsSuccess { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public long StatusCode { get; set; }
        public T Value { get; set; }

        public static SafeSyncApiResult<T> Success(T value, long statusCode)
        {
            return new SafeSyncApiResult<T>
            {
                IsSuccess = true,
                Value = value,
                StatusCode = statusCode
            };
        }

        public static SafeSyncApiResult<T> Failure(string errorCode, string errorMessage, long statusCode = 0)
        {
            return new SafeSyncApiResult<T>
            {
                IsSuccess = false,
                ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? SafeSyncApiError.ServerUnavailable : errorCode,
                ErrorMessage = errorMessage ?? string.Empty,
                StatusCode = statusCode
            };
        }
    }

    [Serializable]
    public sealed class SafeSyncHealthResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("maxLimits")]
        public SafeSyncMaxLimits MaxLimits { get; set; } = new SafeSyncMaxLimits();

        [JsonProperty("serverTime")]
        public string ServerTime { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class SafeSyncMaxLimits
    {
        [JsonProperty("sessionsPerRequest")]
        public int SessionsPerRequest { get; set; }

        [JsonProperty("bucketsPerGroup")]
        public int BucketsPerGroup { get; set; }

        [JsonProperty("warningsPerSession")]
        public int WarningsPerSession { get; set; }
    }

    [Serializable]
    public sealed class SafeActivitySessionsUpsertResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("acceptedCount")]
        public int AcceptedCount { get; set; }

        [JsonProperty("rejectedCount")]
        public int RejectedCount { get; set; }

        [JsonProperty("results")]
        public List<SafeActivitySessionUpsertResult> Results { get; set; } = new List<SafeActivitySessionUpsertResult>();

        [JsonProperty("serverTime")]
        public string ServerTime { get; set; } = string.Empty;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }
    }

    [Serializable]
    public sealed class SafeActivitySessionUpsertResult
    {
        [JsonProperty("clientSessionId")]
        public string ClientSessionId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("errorCode")]
        public string ErrorCode { get; set; }

        [JsonProperty("serverSessionId")]
        public string ServerSessionId { get; set; }
    }

    [Serializable]
    public sealed class SafeActivitySessionsListResponse
    {
        [JsonProperty("sessions")]
        public List<RemoteSafeActivitySessionDto> Sessions { get; set; } = new List<RemoteSafeActivitySessionDto>();

        [JsonProperty("pagination")]
        public SafeSyncPagination Pagination { get; set; } = new SafeSyncPagination();

        [JsonProperty("serverTime")]
        public string ServerTime { get; set; } = string.Empty;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }
    }

    [Serializable]
    public sealed class RemoteSafeActivitySessionDto
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("clientSessionId")]
        public string ClientSessionId { get; set; } = string.Empty;

        [JsonProperty("sourceProvider")]
        public string SourceProvider { get; set; } = "UNKNOWN_AGENT";

        [JsonProperty("dayBucket")]
        public string DayBucket { get; set; } = string.Empty;

        [JsonProperty("timeBucket")]
        public string TimeBucket { get; set; }

        [JsonProperty("confidence")]
        public string Confidence { get; set; } = "LOW";

        [JsonProperty("warningIds")]
        public List<string> WarningIds { get; set; } = new List<string>();

        [JsonProperty("analyzerVersion")]
        public string AnalyzerVersion { get; set; }

        [JsonProperty("parserVersion")]
        public string ParserVersion { get; set; }

        [JsonProperty("aggregateSchemaVersion")]
        public int AggregateSchemaVersion { get; set; }

        [JsonProperty("hashedRepositoryId")]
        public string HashedRepositoryId { get; set; }

        [JsonProperty("changeCountBucket")]
        public string ChangeCountBucket { get; set; }

        [JsonProperty("lineCountBucket")]
        public string LineCountBucket { get; set; }

        [JsonProperty("commitCountBucket")]
        public string CommitCountBucket { get; set; }

        [JsonProperty("sessionCountBucket")]
        public string SessionCountBucket { get; set; }

        [JsonProperty("interactionCountBucket")]
        public string InteractionCountBucket { get; set; }

        [JsonProperty("activityCategory")]
        public string ActivityCategory { get; set; }

        [JsonProperty("durationBucket")]
        public string DurationBucket { get; set; }

        [JsonProperty("categoryBuckets")]
        public List<SafeBucketContractDto> CategoryBuckets { get; set; } = new List<SafeBucketContractDto>();

        [JsonProperty("languageBuckets")]
        public List<SafeBucketContractDto> LanguageBuckets { get; set; } = new List<SafeBucketContractDto>();

        [JsonProperty("toolBuckets")]
        public List<SafeBucketContractDto> ToolBuckets { get; set; } = new List<SafeBucketContractDto>();

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonProperty("updatedAt")]
        public string UpdatedAt { get; set; } = string.Empty;
    }

    [Serializable]
    public sealed class SafeSyncPagination
    {
        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("offset")]
        public int Offset { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    [Serializable]
    public sealed class SafeActivitySessionDeleteResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("deletedId")]
        public string DeletedId { get; set; } = string.Empty;

        [JsonProperty("serverTime")]
        public string ServerTime { get; set; } = string.Empty;
    }
}
