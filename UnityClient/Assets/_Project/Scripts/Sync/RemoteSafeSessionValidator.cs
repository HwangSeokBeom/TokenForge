using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TokenForge.Client.Common;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public sealed class RemoteSafeSessionValidator
    {
        private static readonly HashSet<string> AllowedJsonFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "id",
            "clientSessionId",
            "sourceProvider",
            "dayBucket",
            "timeBucket",
            "confidence",
            "warningIds",
            "analyzerVersion",
            "parserVersion",
            "aggregateSchemaVersion",
            "hashedRepositoryId",
            "changeCountBucket",
            "lineCountBucket",
            "commitCountBucket",
            "sessionCountBucket",
            "interactionCountBucket",
            "activityCategory",
            "durationBucket",
            "categoryBuckets",
            "languageBuckets",
            "toolBuckets",
            "createdAt",
            "updatedAt"
        };

        private static readonly HashSet<string> SourceProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "UNKNOWN_AGENT",
            "UNKNOWN",
            "MANUAL",
            "GIT",
            "CLAUDE",
            "CODEX"
        };

        private static readonly HashSet<string> ConfidenceBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LOW",
            "MEDIUM",
            "HIGH"
        };

        private static readonly HashSet<string> CountBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NONE",
            "ONE",
            "FEW",
            "MANY",
            "MASSIVE"
        };

        private static readonly HashSet<string> LineBuckets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NONE",
            "FEW",
            "MANY",
            "MASSIVE"
        };

        private static readonly HashSet<string> ActivityCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WORK_UNKNOWN",
            "WORK_FEATURE",
            "WORK_BUGFIX",
            "WORK_REFACTOR",
            "WORK_TEST",
            "WORK_UIUX",
            "WORK_DOCS",
            "WORK_BUILD",
            "WORK_CHORE",
            "WORK_RESEARCH",
            "WORK_MIXED",
            "Unknown",
            "Feature",
            "Bugfix",
            "Refactor",
            "Test",
            "UIUX",
            "Docs",
            "Build",
            "Chore",
            "Research",
            "Mixed"
        };

        private static readonly HashSet<string> LanguageCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LANG_UNKNOWN",
            "LANG_CSHARP",
            "LANG_JAVASCRIPT",
            "LANG_TYPESCRIPT",
            "LANG_PYTHON",
            "LANG_WEB",
            "LANG_CONFIG",
            "LANG_DOCS",
            "LANG_TEST",
            "LANG_SHELL"
        };

        private static readonly HashSet<string> ToolCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TOOL_UNKNOWN",
            "TOOL_GIT_COMMIT",
            "TOOL_EDIT",
            "TOOL_SHELL",
            "TOOL_TEST",
            "TOOL_BUILD",
            "TOOL_NAVIGATION",
            "TOOL_SEARCH"
        };

        private readonly PrivacySanitizer privacySanitizer;

        public RemoteSafeSessionValidator(PrivacySanitizer privacySanitizer = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
        }

        public Result Validate(RemoteSafeActivitySessionDto session)
        {
            if (session == null)
            {
                return Result.Failure(SafeSyncApiError.RemoteSessionValidationFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.RemoteSessionValidationFailed));
            }

            var privacy = privacySanitizer.ValidateNoForbiddenFields(session);
            if (!privacy.IsSuccess)
            {
                return Result.Failure(SafeSyncApiError.KeepRemoteUnsafeRejected, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.KeepRemoteUnsafeRejected));
            }

            var schemaVersion = session.AggregateSchemaVersion <= 0 ? 1 : session.AggregateSchemaVersion;
            if (schemaVersion != 1)
            {
                return Result.Failure(SafeSyncApiError.UnknownSchemaVersion, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.UnknownSchemaVersion));
            }

            if (!SafeIdentifier(session.Id, allowEmpty: true) ||
                !SafeIdentifier(session.ClientSessionId, allowEmpty: false) ||
                !SafeProvider(session.SourceProvider) ||
                !SafeDayBucket(session.DayBucket) ||
                !SafeTimeBucket(session.TimeBucket, allowEmpty: true) ||
                !SafeConfidence(session.Confidence) ||
                !SafeVersion(session.AnalyzerVersion, allowEmpty: true) ||
                !SafeVersion(session.ParserVersion, allowEmpty: true) ||
                !SafeHash(session.HashedRepositoryId, allowEmpty: true) ||
                !SafeBucket(session.ChangeCountBucket, CountBuckets, allowEmpty: true) ||
                !SafeBucket(session.LineCountBucket, LineBuckets, allowEmpty: true) ||
                !SafeBucket(session.CommitCountBucket, CountBuckets, allowEmpty: true) ||
                !SafeBucket(session.SessionCountBucket, CountBuckets, allowEmpty: true) ||
                !SafeBucket(session.InteractionCountBucket, CountBuckets, allowEmpty: true) ||
                !SafeKnownValue(session.ActivityCategory, ActivityCategories, allowEmpty: true) ||
                !SafeEnumKey(session.DurationBucket, string.Empty, allowEmpty: true) ||
                !SafeWarningIds(session.WarningIds) ||
                !SafeBuckets(session.CategoryBuckets, ActivityCategories) ||
                !SafeBuckets(session.LanguageBuckets, LanguageCategories) ||
                !SafeBuckets(session.ToolBuckets, ToolCategories))
            {
                return Result.Failure(SafeSyncApiError.InvalidBucketValue, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBucketValue));
            }

            return Result.Success();
        }

        public Result<RemoteSafeActivitySessionDto> ValidateJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Result<RemoteSafeActivitySessionDto>.Failure(SafeSyncApiError.RemoteSessionValidationFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.RemoteSessionValidationFailed));
            }

            try
            {
                var token = JToken.Parse(json);
                if (token.Type != JTokenType.Object)
                {
                    return Result<RemoteSafeActivitySessionDto>.Failure(SafeSyncApiError.RemoteSessionValidationFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.RemoteSessionValidationFailed));
                }

                var obj = (JObject)token;
                if (obj.Properties().Any(property => !AllowedJsonFields.Contains(property.Name)))
                {
                    return Result<RemoteSafeActivitySessionDto>.Failure(SafeSyncApiError.KeepRemoteUnsafeRejected, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.KeepRemoteUnsafeRejected));
                }

                if (!privacySanitizer.ValidateNoForbiddenFields(obj).IsSuccess)
                {
                    return Result<RemoteSafeActivitySessionDto>.Failure(SafeSyncApiError.KeepRemoteUnsafeRejected, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.KeepRemoteUnsafeRejected));
                }

                var session = obj.ToObject<RemoteSafeActivitySessionDto>();
                var validation = Validate(session);
                return validation.IsSuccess
                    ? Result<RemoteSafeActivitySessionDto>.Success(session)
                    : Result<RemoteSafeActivitySessionDto>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }
            catch (JsonException)
            {
                return Result<RemoteSafeActivitySessionDto>.Failure(SafeSyncApiError.InvalidJson, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidJson));
            }
        }

        private static bool SafeIdentifier(string value, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowEmpty;
            }

            return Regex.IsMatch(value.Trim(), "^[A-Za-z0-9_.:-]{1,128}$");
        }

        private static bool SafeProvider(string value)
        {
            return string.IsNullOrWhiteSpace(value) || SourceProviders.Contains(value.Trim());
        }

        private static bool SafeDayBucket(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value.Trim(), "^\\d{4}-\\d{2}-\\d{2}$");
        }

        private static bool SafeTimeBucket(string value, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowEmpty;
            }

            return Regex.IsMatch(value.Trim(), "^HOUR_\\d{2}$");
        }

        private static bool SafeConfidence(string value)
        {
            return string.IsNullOrWhiteSpace(value) || ConfidenceBuckets.Contains(value.Trim());
        }

        private static bool SafeVersion(string value, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowEmpty;
            }

            return Regex.IsMatch(value.Trim(), "^[A-Za-z0-9_.:-]{1,40}$");
        }

        private static bool SafeHash(string value, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowEmpty;
            }

            return Regex.IsMatch(value.Trim(), "^[A-Fa-f0-9]{16,128}$");
        }

        private static bool SafeBucket(string value, HashSet<string> allowed, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowEmpty;
            }

            return allowed.Contains(value.Trim());
        }

        private static bool SafeWarningIds(List<string> warningIds)
        {
            return (warningIds ?? new List<string>())
                .Take(26)
                .All(id => SafeEnumKey(id, string.Empty, allowEmpty: false));
        }

        private static bool SafeBuckets(List<SafeBucketContractDto> buckets, HashSet<string> allowedKeys)
        {
            return (buckets ?? new List<SafeBucketContractDto>())
                .Take(25)
                .All(bucket => bucket != null &&
                               SafeKnownValue(bucket.Key, allowedKeys, allowEmpty: false) &&
                               SafeBucket(bucket.CountBucket, CountBuckets, allowEmpty: false));
        }

        private static bool SafeKnownValue(string value, HashSet<string> allowed, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowEmpty;
            }

            return Regex.IsMatch(value.Trim(), "^[A-Za-z][A-Za-z0-9_:-]{1,63}$") && allowed.Contains(value.Trim());
        }

        private static bool SafeEnumKey(string value, string requiredPrefix, bool allowEmpty)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return allowEmpty;
            }

            var trimmed = value.Trim();
            if (!Regex.IsMatch(trimmed, "^[A-Z][A-Z0-9_:-]{1,63}$"))
            {
                return false;
            }

            return string.IsNullOrEmpty(requiredPrefix) || trimmed.StartsWith(requiredPrefix, StringComparison.Ordinal);
        }
    }
}
