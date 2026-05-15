using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TokenForge.Client.Sync
{
    public interface ISafeSyncLogger
    {
        void Info(string method, string endpointName, long statusCode, string errorCode = "");
        void Warning(string method, string endpointName, long statusCode, string errorCode);
        void Metadata(
            string method,
            string endpointName,
            long statusCode,
            string errorCode,
            int acceptedCount,
            int rejectedCount,
            int schemaVersion,
            long durationMs);
    }

    public sealed class UnitySafeSyncLogger : ISafeSyncLogger
    {
        public void Info(string method, string endpointName, long statusCode, string errorCode = "")
        {
            Debug.Log(Format(method, endpointName, statusCode, errorCode));
        }

        public void Warning(string method, string endpointName, long statusCode, string errorCode)
        {
            Debug.LogWarning(Format(method, endpointName, statusCode, errorCode));
        }

        public void Metadata(
            string method,
            string endpointName,
            long statusCode,
            string errorCode,
            int acceptedCount,
            int rejectedCount,
            int schemaVersion,
            long durationMs)
        {
            var message = Format(method, endpointName, statusCode, errorCode) +
                          $" accepted={Mathf.Max(0, acceptedCount)} rejected={Mathf.Max(0, rejectedCount)} schemaVersion={Mathf.Max(0, schemaVersion)} durationMs={Mathf.Max(0, (int)durationMs)}";
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        private static string Format(string method, string endpointName, long statusCode, string errorCode)
        {
            var safeMethod = SafeToken(method, "UNKNOWN");
            var safeEndpoint = SafeToken(endpointName, "unknown_endpoint");
            var safeCode = SafeToken(errorCode, string.Empty);
            return string.IsNullOrEmpty(safeCode)
                ? $"TokenForge sync {safeMethod} {safeEndpoint} status={statusCode}"
                : $"TokenForge sync {safeMethod} {safeEndpoint} status={statusCode} error={safeCode}";
        }

        private static string SafeToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var trimmed = value.Trim();
            return Regex.IsMatch(trimmed, "^[A-Za-z0-9_.-]{1,80}$") && !ContainsForbiddenLogTerm(trimmed)
                ? trimmed
                : fallback;
        }

        private static bool ContainsForbiddenLogTerm(string value)
        {
            if (IsKnownSafeErrorCode(value))
            {
                return false;
            }

            var lowered = value.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("token") ||
                   lowered.Contains("password") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("response") ||
                   lowered.Contains("command") ||
                   lowered.Contains("diff") ||
                   lowered.Contains("path") ||
                   lowered.Contains("claudelog") ||
                   lowered.Contains("codexlog") ||
                   lowered.Contains("body");
        }

        private static bool IsKnownSafeErrorCode(string value)
        {
            return string.Equals(value, "UNSAFE_FIELD_NAME", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_PATH_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_COMMAND_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_TOKEN_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_SOURCE_SNIPPET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PAYLOAD_TOO_LARGE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNKNOWN_SCHEMA_VERSION", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "INVALID_BUCKET_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "INVALID_ENUM_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "VALIDATION_FAILED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PRIVACY_GUARD_REJECTED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, SafeSyncApiError.PrivacyGuardBlockedPayload, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class InMemorySafeSyncLogger : ISafeSyncLogger
    {
        public List<string> Entries { get; } = new List<string>();

        public void Info(string method, string endpointName, long statusCode, string errorCode = "")
        {
            Entries.Add(Format("info", method, endpointName, statusCode, errorCode));
        }

        public void Warning(string method, string endpointName, long statusCode, string errorCode)
        {
            Entries.Add(Format("warning", method, endpointName, statusCode, errorCode));
        }

        public void Metadata(
            string method,
            string endpointName,
            long statusCode,
            string errorCode,
            int acceptedCount,
            int rejectedCount,
            int schemaVersion,
            long durationMs)
        {
            Entries.Add(Format("metadata", method, endpointName, statusCode, errorCode) +
                        $" accepted={Math.Max(0, acceptedCount)} rejected={Math.Max(0, rejectedCount)} schemaVersion={Math.Max(0, schemaVersion)} durationMs={Math.Max(0, durationMs)}");
        }

        private static string Format(string level, string method, string endpointName, long statusCode, string errorCode)
        {
            return $"{SafeToken(level, "info")} method={SafeToken(method, "UNKNOWN")} endpoint={SafeToken(endpointName, "unknown_endpoint")} status={statusCode} error={SafeToken(errorCode, string.Empty)}";
        }

        private static string SafeToken(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var trimmed = value.Trim();
            if (!Regex.IsMatch(trimmed, "^[A-Za-z0-9_.-]{1,80}$"))
            {
                return fallback;
            }

            if (IsKnownSafeErrorCode(trimmed))
            {
                return trimmed;
            }

            var lowered = trimmed.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("token") ||
                   lowered.Contains("password") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("response") ||
                   lowered.Contains("command") ||
                   lowered.Contains("diff") ||
                   lowered.Contains("path") ||
                   lowered.Contains("claudelog") ||
                   lowered.Contains("codexlog") ||
                   lowered.Contains("body")
                ? fallback
                : trimmed;
        }

        private static bool IsKnownSafeErrorCode(string value)
        {
            return string.Equals(value, "UNSAFE_FIELD_NAME", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_PATH_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_COMMAND_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_TOKEN_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNSAFE_SOURCE_SNIPPET", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PAYLOAD_TOO_LARGE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "UNKNOWN_SCHEMA_VERSION", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "INVALID_BUCKET_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "INVALID_ENUM_VALUE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "VALIDATION_FAILED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "PRIVACY_GUARD_REJECTED", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, SafeSyncApiError.PrivacyGuardBlockedPayload, StringComparison.OrdinalIgnoreCase);
        }
    }
}
