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
            var lowered = value.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("diff") ||
                   lowered.Contains("path") ||
                   lowered.Contains("claudelog") ||
                   lowered.Contains("codexlog") ||
                   lowered.Contains("body");
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

            var lowered = trimmed.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("diff") ||
                   lowered.Contains("path") ||
                   lowered.Contains("claudelog") ||
                   lowered.Contains("codexlog") ||
                   lowered.Contains("body")
                ? fallback
                : trimmed;
        }
    }
}
