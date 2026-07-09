using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Agents
{
    public sealed class ClaudeAgentLogParser : SafeAgentLogParser
    {
        public const string Version = "claude-agent-parser-v1";
        public override AgentProviderType ProviderType => AgentProviderType.ClaudeCode;
        public override string ParserVersion => Version;
    }

    public sealed class CursorAgentLogParser : SafeAgentLogParser
    {
        public const string Version = "cursor-agent-parser-v1";
        public override AgentProviderType ProviderType => AgentProviderType.Cursor;
        public override string ParserVersion => Version;
    }

    public sealed class CodexAgentLogParser : SafeAgentLogParser
    {
        public const string Version = "codex-agent-parser-v1";
        public override AgentProviderType ProviderType => AgentProviderType.Codex;
        public override string ParserVersion => Version;
    }

    public sealed class GitHubCopilotAgentLogParser : SafeAgentLogParser
    {
        public const string Version = "github-copilot-agent-parser-v1";
        public override AgentProviderType ProviderType => AgentProviderType.GitHubCopilot;
        public override string ParserVersion => Version;
    }

    public sealed class GeminiCliAgentLogParser : SafeAgentLogParser
    {
        public const string Version = "gemini-cli-agent-parser-v1";
        public override AgentProviderType ProviderType => AgentProviderType.GeminiCli;
        public override string ParserVersion => Version;
    }

    public sealed class ManualAgentLogParser : SafeAgentLogParser
    {
        public const string Version = "manual-agent-parser-v1";
        public override AgentProviderType ProviderType => AgentProviderType.Manual;
        public override string ParserVersion => Version;
    }

    public sealed class UnknownAgentLogParser : SafeAgentLogParser
    {
        public const string Version = "unknown-agent-parser-v1";
        public override AgentProviderType ProviderType => AgentProviderType.Unknown;
        public override string ParserVersion => Version;
    }

    public abstract class SafeAgentLogParser : IAgentLogParser
    {
        private static readonly Regex TimestampRegex = new Regex(@"\b\d{4}-\d{2}-\d{2}[tT ][0-2]\d:[0-5]\d:[0-5]\d(?:\.\d+)?(?:Z|[+\-][0-2]\d:?[0-5]\d)?\b", RegexOptions.Compiled);
        private static readonly Regex SessionRegex = new Regex(@"session[_\- ]?id[""'\s:=]+([a-z0-9_\-]{4,80})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SourceLikeRegex = new Regex(@"\b(public\s+class|private\s+class|function\s+[a-z0-9_]+\s*\(|const\s+[a-z0-9_]+\s*=|var\s+[a-z0-9_]+\s*=|def\s+[a-z0-9_]+\s*\()", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PathExtensionRegex = new Regex(@"\.(cs|js|jsx|ts|tsx|py|html|css|json|yaml|yml|md|sh|bash)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ForbiddenFieldDetector detector = new ForbiddenFieldDetector();

        public abstract AgentProviderType ProviderType { get; }
        public abstract string ParserVersion { get; }

        public Result<AgentActivitySummary> Parse(AgentAnalysisInput input, IReadOnlyList<AgentLogEntry> entries)
        {
            input = input ?? new AgentAnalysisInput();
            entries = entries ?? new List<AgentLogEntry>();

            var warningIds = new HashSet<string>(StringComparer.Ordinal);
            var sessionKeys = new HashSet<string>(StringComparer.Ordinal);
            var dayCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var toolCounts = new Dictionary<AgentToolUsageCategory, int>();
            var languageCounts = new Dictionary<AgentLanguageCategory, int>();
            var interactionCount = 0;
            var timestampCount = 0;
            var providerMarkerCount = 0;
            long estimatedInputTokens = 0;
            long estimatedOutputTokens = 0;
            var cumulativeTokensBySource = new Dictionary<string, TokenMeasurement>(StringComparer.Ordinal);
            DateTimeOffset? lastActivityAtUtc = null;

            foreach (var entry in entries)
            {
                var text = entry?.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (entry?.LastWriteTimeUtc != null &&
                    (lastActivityAtUtc == null || entry.LastWriteTimeUtc.Value > lastActivityAtUtc.Value))
                {
                    lastActivityAtUtc = entry.LastWriteTimeUtc.Value;
                }

                if (detector.ContainsSensitiveString(text) || SourceLikeRegex.IsMatch(text))
                {
                    warningIds.Add("agent_log_risky_content_discarded");
                }

                if (ContainsProviderMarker(text))
                {
                    providerMarkerCount++;
                }

                var parsedJson = TryParseJson(text, out var json);
                if (parsedJson)
                {
                    if (TryExtractExplicitTokenUsage(json, out var measurement))
                    {
                        if (measurement.Cumulative)
                        {
                            var sourceKey = string.IsNullOrWhiteSpace(entry?.SourceKey) ? "unknown-source" : entry.SourceKey;
                            if (!cumulativeTokensBySource.TryGetValue(sourceKey, out var previous) ||
                                measurement.Total > previous.Total)
                            {
                                cumulativeTokensBySource[sourceKey] = measurement;
                            }
                        }
                        else
                        {
                            estimatedInputTokens = SaturatingAdd(estimatedInputTokens, measurement.Input);
                            estimatedOutputTokens = SaturatingAdd(estimatedOutputTokens, measurement.Output);
                        }
                    }
                    else
                    {
                        EstimateMessageTokens(json, text, out var inputEstimate, out var outputEstimate);
                        estimatedInputTokens = SaturatingAdd(estimatedInputTokens, inputEstimate);
                        estimatedOutputTokens = SaturatingAdd(estimatedOutputTokens, outputEstimate);
                        if (inputEstimate > 0 || outputEstimate > 0)
                        {
                            warningIds.Add("agent_token_usage_estimated_from_message_text");
                        }
                    }
                    ExtractFromJson(json, sessionKeys, dayCounts, toolCounts, languageCounts, ref interactionCount, ref timestampCount, warningIds);
                }
                else
                {
                    var lineTokens = Math.Max(1L, (text.Length + 3L) / 4L);
                    if (LooksLikeAssistantOutput(text))
                    {
                        estimatedOutputTokens = SaturatingAdd(estimatedOutputTokens, lineTokens);
                    }
                    else
                    {
                        estimatedInputTokens = SaturatingAdd(estimatedInputTokens, lineTokens);
                    }
                    warningIds.Add("agent_token_usage_estimated_from_message_text");
                    ExtractFromText(text, sessionKeys, dayCounts, toolCounts, languageCounts, ref interactionCount, ref timestampCount);
                }

                if (entry?.LastWriteTimeUtc != null && dayCounts.Count == 0)
                {
                    AddDay(dayCounts, entry.LastWriteTimeUtc.Value.UtcDateTime);
                }
            }

            foreach (var measurement in cumulativeTokensBySource.Values)
            {
                estimatedInputTokens = SaturatingAdd(estimatedInputTokens, measurement.Input);
                estimatedOutputTokens = SaturatingAdd(estimatedOutputTokens, measurement.Output);
            }

            if (entries.Count == 0)
            {
                warningIds.Add("agent_log_no_entries");
            }

            if (ProviderType == AgentProviderType.Unknown)
            {
                warningIds.Add("agent_log_unknown_provider");
            }

            if (interactionCount == 0 && toolCounts.Count == 0 && sessionKeys.Count == 0)
            {
                warningIds.Add("agent_log_unsupported_format");
            }

            if (dayCounts.Count > 1)
            {
                warningIds.Add("agent_log_multiple_days_collapsed");
            }

            var confidence = DetermineConfidence(providerMarkerCount, timestampCount, interactionCount, toolCounts.Count, warningIds);
            var summary = new AgentActivitySummary
            {
                ProviderType = ProviderType,
                SourceKind = input.SourceKind == AgentSourceKind.Unknown ? AgentSourceKind.ManualFolder : input.SourceKind,
                SafeSourceAlias = SafeAlias(input.SafeSourceAlias, ProviderType),
                SourceIdentifierHash = SafeHashUtility.ComputeProjectPathHash(input.SelectedLocationPath, "TokenForge.AgentLogSource.v1"),
                DayBucket = SelectDayBucket(dayCounts),
                FileCountBucket = SafeAgentLogParser.ToCountBucket(entries.Select(entry => entry?.LastWriteTimeUtc?.UtcDateTime.Ticks ?? 0).Distinct().Count()),
                SessionCountBucket = ToCountBucket(Math.Max(sessionKeys.Count, entries.Count > 0 ? 1 : 0)),
                InteractionCountBucket = ToCountBucket(interactionCount),
                EstimatedCodingActivityBucket = ToCountBucket(toolCounts.Where(item => item.Key != AgentToolUsageCategory.Unknown).Sum(item => item.Value)),
                EstimatedSessionCount = Math.Max(sessionKeys.Count, entries.Count > 0 ? 1 : 0),
                EstimatedInteractionCount = Math.Max(0, interactionCount),
                EstimatedInputTokenCount = Math.Max(0, estimatedInputTokens),
                EstimatedOutputTokenCount = Math.Max(0, estimatedOutputTokens),
                EstimatedTotalTokenCount = SaturatingAdd(estimatedInputTokens, estimatedOutputTokens),
                LastActivityAtUtc = lastActivityAtUtc,
                ToolUsageCategoryBuckets = toolCounts
                    .OrderBy(item => item.Key)
                    .Select(item => new AgentToolUsageCategoryBucket { Category = item.Key, CountBucket = ToCountBucket(item.Value) })
                    .ToList(),
                LanguageCategoryBuckets = languageCounts
                    .OrderBy(item => item.Key)
                    .Select(item => new AgentLanguageCategoryBucket { Category = item.Key, CountBucket = ToCountBucket(item.Value) })
                    .ToList(),
                ConfidenceLevel = confidence,
                WarningIds = warningIds.OrderBy(item => item, StringComparer.Ordinal).Take(12).ToList(),
                AnalyzerVersion = ParserVersion
            };

            return Result<AgentActivitySummary>.Success(summary);
        }

        private struct TokenMeasurement
        {
            public long Input;
            public long Output;
            public bool Cumulative;
            public long Total => SaturatingAdd(Input, Output);
        }

        private static bool TryExtractExplicitTokenUsage(JToken json, out TokenMeasurement measurement)
        {
            measurement = default(TokenMeasurement);
            var properties = EnumerateProperties(json).ToList();

            // Codex token_count events carry both last_token_usage and
            // total_token_usage. The total is cumulative for that source file,
            // so prefer it and let the caller retain only the greatest value.
            foreach (var property in properties.Where(item => NormalizeTokenField(item.Name) == "totaltokenusage"))
            {
                if (TryReadTokenObject(property.Value as JObject, out measurement))
                {
                    measurement.Cumulative = true;
                    return true;
                }
            }

            var candidates = new List<TokenMeasurement>();
            foreach (var property in properties.Where(item =>
                         NormalizeTokenField(item.Name) == "usage" ||
                         NormalizeTokenField(item.Name) == "tokenusage" ||
                         NormalizeTokenField(item.Name) == "lasttokenusage"))
            {
                if (TryReadTokenObject(property.Value as JObject, out var candidate))
                {
                    candidates.Add(candidate);
                }
            }

            if (json is JObject root && TryReadTokenObject(root, out var rootMeasurement))
            {
                candidates.Add(rootMeasurement);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            measurement = candidates.OrderByDescending(item => item.Total).First();
            return true;
        }

        private static bool TryReadTokenObject(JObject value, out TokenMeasurement measurement)
        {
            measurement = default(TokenMeasurement);
            if (value == null)
            {
                return false;
            }

            long input = 0;
            long prompt = 0;
            long output = 0;
            long completion = 0;
            long cacheCreation = 0;
            long cacheRead = 0;
            long total = 0;
            var found = false;
            foreach (var property in value.Properties())
            {
                if (!TryReadNonNegativeLong(property.Value, out var count))
                {
                    continue;
                }

                switch (NormalizeTokenField(property.Name))
                {
                    case "inputtokens": input = Math.Max(input, count); found = true; break;
                    case "prompttokens": prompt = Math.Max(prompt, count); found = true; break;
                    case "outputtokens": output = Math.Max(output, count); found = true; break;
                    case "completiontokens": completion = Math.Max(completion, count); found = true; break;
                    case "cachecreationinputtokens": cacheCreation = Math.Max(cacheCreation, count); found = true; break;
                    case "cachereadinputtokens": cacheRead = Math.Max(cacheRead, count); found = true; break;
                    case "totaltokens": total = Math.Max(total, count); found = true; break;
                }
            }

            if (!found)
            {
                return false;
            }

            measurement.Input = SaturatingAdd(Math.Max(input, prompt), SaturatingAdd(cacheCreation, cacheRead));
            measurement.Output = Math.Max(output, completion);
            if (measurement.Input == 0 && measurement.Output == 0 && total > 0)
            {
                measurement.Input = total;
            }
            return measurement.Total > 0;
        }

        private static bool TryReadNonNegativeLong(JToken token, out long value)
        {
            value = 0;
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float && token.Type != JTokenType.String))
            {
                return false;
            }

            if (!decimal.TryParse(token.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            {
                return false;
            }

            value = parsed >= long.MaxValue ? long.MaxValue : (long)parsed;
            return true;
        }

        private static string NormalizeTokenField(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static void EstimateMessageTokens(JToken json, string originalText, out long inputTokens, out long outputTokens)
        {
            var uniqueValues = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in EnumerateProperties(json))
            {
                var name = NormalizeTokenField(property.Name);
                if (name != "content" && name != "text" && name != "prompt" && name != "input" &&
                    name != "output" && name != "response" && name != "completion" && name != "message")
                {
                    continue;
                }

                if (property.Value?.Type == JTokenType.String)
                {
                    var value = property.Value.Value<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        uniqueValues.Add(value);
                    }
                }
            }

            long characters = 0;
            foreach (var value in uniqueValues)
            {
                characters = SaturatingAdd(characters, value.Length);
            }
            var tokens = characters <= 0 ? 0 : Math.Max(1L, (characters + 3L) / 4L);
            if (LooksLikeAssistantOutput(originalText))
            {
                inputTokens = 0;
                outputTokens = tokens;
            }
            else
            {
                inputTokens = tokens;
                outputTokens = 0;
            }
        }

        private static long SaturatingAdd(long first, long second)
        {
            first = Math.Max(0, first);
            second = Math.Max(0, second);
            return first > long.MaxValue - second ? long.MaxValue : first + second;
        }

        private static bool LooksLikeAssistantOutput(string text)
        {
            text = text ?? string.Empty;
            return text.IndexOf("\"role\":\"assistant\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("\"role\": \"assistant\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("assistant:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("completion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("model_response", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static CountBucket ToCountBucket(int count)
        {
            if (count <= 0) return CountBucket.None;
            if (count == 1) return CountBucket.One;
            if (count <= 5) return CountBucket.Small;
            if (count <= 20) return CountBucket.Medium;
            if (count <= 100) return CountBucket.Large;
            return CountBucket.Huge;
        }

        private void ExtractFromJson(
            JToken json,
            HashSet<string> sessionKeys,
            Dictionary<string, int> dayCounts,
            Dictionary<AgentToolUsageCategory, int> toolCounts,
            Dictionary<AgentLanguageCategory, int> languageCounts,
            ref int interactionCount,
            ref int timestampCount,
            HashSet<string> warningIds)
        {
            foreach (var property in EnumerateProperties(json))
            {
                var name = property.Name ?? string.Empty;
                var value = property.Value?.Type == JTokenType.String ? property.Value.Value<string>() : string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (IsTimestampField(name) && TryParseTimestamp(value, out var timestamp))
                {
                    AddDay(dayCounts, timestamp);
                    timestampCount++;
                }

                if (IsSessionField(name))
                {
                    sessionKeys.Add(SafeHashUtility.ComputeProjectPathHash(value, "TokenForge.AgentSessionKey.v1"));
                }

                if (IsInteractionField(name, value))
                {
                    interactionCount++;
                }

                if (IsToolField(name))
                {
                    Increment(toolCounts, ClassifyToolCategory(value));
                }

                if (IsCommandLikeField(name))
                {
                    Increment(toolCounts, ClassifyToolCategory(value));
                }

                if (IsLanguageField(name))
                {
                    Increment(languageCounts, ClassifyLanguageCategory(value));
                }

                if (IsPathLikeField(name))
                {
                    var language = ClassifyLanguageFromPathLikeValue(value);
                    if (language != AgentLanguageCategory.Unknown)
                    {
                        Increment(languageCounts, language);
                    }
                }

                if (detector.ContainsSensitiveString(value) || SourceLikeRegex.IsMatch(value))
                {
                    warningIds.Add("agent_log_risky_content_discarded");
                }
            }
        }

        private void ExtractFromText(
            string text,
            HashSet<string> sessionKeys,
            Dictionary<string, int> dayCounts,
            Dictionary<AgentToolUsageCategory, int> toolCounts,
            Dictionary<AgentLanguageCategory, int> languageCounts,
            ref int interactionCount,
            ref int timestampCount)
        {
            foreach (Match match in TimestampRegex.Matches(text))
            {
                if (TryParseTimestamp(match.Value, out var timestamp))
                {
                    AddDay(dayCounts, timestamp);
                    timestampCount++;
                }
            }

            var sessionMatch = SessionRegex.Match(text);
            if (sessionMatch.Success)
            {
                sessionKeys.Add(SafeHashUtility.ComputeProjectPathHash(sessionMatch.Groups[1].Value, "TokenForge.AgentSessionKey.v1"));
            }

            if (ContainsAny(text, "user", "assistant", "prompt", "response", "message", "completion"))
            {
                interactionCount++;
            }

            Increment(toolCounts, ClassifyToolCategory(text));
            var language = ClassifyLanguageFromPathLikeValue(text);
            if (language != AgentLanguageCategory.Unknown)
            {
                Increment(languageCounts, language);
            }
        }

        private bool ContainsProviderMarker(string text)
        {
            switch (ProviderType)
            {
                case AgentProviderType.Cursor:
                    return text.IndexOf("cursor", StringComparison.OrdinalIgnoreCase) >= 0;
                case AgentProviderType.Claude:
                case AgentProviderType.ClaudeCode:
                    return text.IndexOf("claude", StringComparison.OrdinalIgnoreCase) >= 0;
                case AgentProviderType.Codex:
                    return text.IndexOf("codex", StringComparison.OrdinalIgnoreCase) >= 0;
                case AgentProviderType.GitHubCopilot:
                    return text.IndexOf("copilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           text.IndexOf("github", StringComparison.OrdinalIgnoreCase) >= 0;
                case AgentProviderType.GeminiCli:
                    return text.IndexOf("gemini", StringComparison.OrdinalIgnoreCase) >= 0;
                default:
                    return false;
            }
        }

        private static string SafeAlias(string alias, AgentProviderType providerType)
        {
            var safe = string.IsNullOrWhiteSpace(alias)
                ? MacAgentSourceDetector.SafeProviderLabel(providerType) + " local source"
                : alias.Trim();
            if (safe.Length > 80)
            {
                safe = safe.Substring(0, 80);
            }

            return safe.IndexOf("/", StringComparison.Ordinal) >= 0 || safe.IndexOf("\\", StringComparison.Ordinal) >= 0
                ? MacAgentSourceDetector.SafeProviderLabel(providerType) + " local source"
                : safe;
        }

        private static bool TryParseJson(string text, out JToken json)
        {
            json = null;
            var trimmed = text.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                json = JToken.Parse(trimmed);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<JProperty> EnumerateProperties(JToken token)
        {
            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var property in obj.Properties())
                {
                    yield return property;
                }
            }

            var container = token as JContainer;
            if (container == null)
            {
                yield break;
            }

            foreach (var property in container.Descendants().OfType<JProperty>())
            {
                yield return property;
            }
        }

        private static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
        {
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out timestamp);
        }

        private static void AddDay(Dictionary<string, int> dayCounts, DateTimeOffset timestamp)
        {
            var day = timestamp.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            dayCounts[day] = dayCounts.TryGetValue(day, out var count) ? count + 1 : 1;
        }

        private static string SelectDayBucket(Dictionary<string, int> dayCounts)
        {
            if (dayCounts == null || dayCounts.Count == 0)
            {
                return DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return dayCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key, StringComparer.Ordinal).First().Key;
        }

        private static bool IsTimestampField(string name)
        {
            return EqualsAny(name, "timestamp", "created_at", "createdAt", "time", "date", "start_time", "startedAt");
        }

        private static bool IsSessionField(string name)
        {
            return EqualsAny(name, "session_id", "sessionId", "conversation_id", "conversationId", "thread_id", "threadId");
        }

        private static bool IsToolField(string name)
        {
            return EqualsAny(name, "tool", "tool_name", "toolName", "name", "event", "type", "action");
        }

        private static bool IsLanguageField(string name)
        {
            return EqualsAny(name, "language", "category", "file_type", "fileType");
        }

        private static bool IsCommandLikeField(string name)
        {
            return EqualsAny(name, "command", "cmd", "arguments", "args");
        }

        private static bool IsPathLikeField(string name)
        {
            return EqualsAny(name, "path", "file", "file_path", "filePath", "uri", "target");
        }

        private static bool IsInteractionField(string name, string value)
        {
            if (EqualsAny(name, "role", "message", "prompt", "response", "completion"))
            {
                return true;
            }

            return EqualsAny(name, "type", "event") && ContainsAny(value, "message", "prompt", "response", "completion", "user", "assistant");
        }

        private static AgentToolUsageCategory ClassifyToolCategory(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return AgentToolUsageCategory.Unknown;
            }

            if (ContainsAny(value, "test", "pytest", "xctest", "nunit", "jest", "vitest"))
            {
                return AgentToolUsageCategory.TestRun;
            }

            if (ContainsAny(value, "build", "compile", "msbuild", "xcodebuild", "gradle", "mvn"))
            {
                return AgentToolUsageCategory.BuildRun;
            }

            if (ContainsAny(value, "edit", "write", "patch", "replace", "str_replace", "multi_edit", "apply_patch"))
            {
                return AgentToolUsageCategory.CodeEditing;
            }

            if (ContainsAny(value, "grep", "ripgrep", "rg", "search", "find"))
            {
                return AgentToolUsageCategory.Search;
            }

            if (ContainsAny(value, "read", "open", "view", "list", "ls", "cat"))
            {
                return AgentToolUsageCategory.FileNavigation;
            }

            if (ContainsAny(value, "bash", "shell", "terminal", "command", "exec"))
            {
                return AgentToolUsageCategory.ShellCommand;
            }

            return AgentToolUsageCategory.Unknown;
        }

        private static AgentLanguageCategory ClassifyLanguageCategory(string value)
        {
            if (ContainsAny(value, "csharp", "c#", ".cs")) return AgentLanguageCategory.CSharp;
            if (ContainsAny(value, "typescript", ".ts", ".tsx")) return AgentLanguageCategory.TypeScript;
            if (ContainsAny(value, "javascript", ".js", ".jsx")) return AgentLanguageCategory.JavaScript;
            if (ContainsAny(value, "python", ".py")) return AgentLanguageCategory.Python;
            if (ContainsAny(value, "html", "css", ".html", ".css")) return AgentLanguageCategory.Web;
            if (ContainsAny(value, "json", "yaml", "yml", ".json", ".yaml", ".yml")) return AgentLanguageCategory.Config;
            if (ContainsAny(value, "markdown", "docs", ".md")) return AgentLanguageCategory.Docs;
            if (ContainsAny(value, "test", "spec")) return AgentLanguageCategory.Test;
            if (ContainsAny(value, "shell", "bash", ".sh")) return AgentLanguageCategory.Shell;
            return AgentLanguageCategory.Unknown;
        }

        private static AgentLanguageCategory ClassifyLanguageFromPathLikeValue(string value)
        {
            var match = PathExtensionRegex.Match(value ?? string.Empty);
            return match.Success ? ClassifyLanguageCategory(match.Value) : AgentLanguageCategory.Unknown;
        }

        private static ConfidenceLevel DetermineConfidence(
            int providerMarkerCount,
            int timestampCount,
            int interactionCount,
            int toolCategoryCount,
            HashSet<string> warningIds)
        {
            if (warningIds.Contains("agent_log_unsupported_format") || warningIds.Contains("agent_log_unknown_provider"))
            {
                return ConfidenceLevel.Low;
            }

            if (providerMarkerCount > 0 && timestampCount > 0 && (interactionCount > 0 || toolCategoryCount > 0))
            {
                return warningIds.Contains("agent_log_risky_content_discarded") ? ConfidenceLevel.Medium : ConfidenceLevel.High;
            }

            if (timestampCount > 0 && (interactionCount > 0 || toolCategoryCount > 0))
            {
                return ConfidenceLevel.Medium;
            }

            return ConfidenceLevel.Low;
        }

        private static bool EqualsAny(string value, params string[] candidates)
        {
            return candidates.Any(candidate => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsAny(string value, params string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return candidates.Any(candidate => value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void Increment<TKey>(Dictionary<TKey, int> counts, TKey key)
        {
            if (EqualityComparer<TKey>.Default.Equals(key, default(TKey)))
            {
                return;
            }

            counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        }
    }
}
