using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Tests
{
    public sealed class Phase8AgentParserFixtureTests
    {
        private const string RawPrompt = "please review the private billing token path";
        private const string RawResponse = "the private token path should be rewritten";
        private const string RawPath = "/Users/alice/SecretRepo/src/private/UserSecret.cs";
        private const string RawFileName = "UserSecret.cs";
        private const string RawCommand = "git status --short && npm test";
        private const string RawSource = "public class Secret { string password = \"hunter2\"; }";
        private const string RawUsername = "username: alice";
        private const string RawToken = "sk-test_abcdefghijklmnopqrstuvwxyz";
        private const string RawSecret = "secret: private-value";

        [Test]
        public void ClaudeJsonlFixture_ProducesSafeAggregateOnly()
        {
            var result = new ClaudeAgentLogParser().Parse(Input(AgentProviderType.Claude), new List<AgentLogEntry>
            {
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:00:00Z\",\"session_id\":\"claude-session-a\",\"type\":\"message\",\"role\":\"user\",\"prompt\":\"" + RawPrompt + "\"}"),
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:02:00Z\",\"tool_name\":\"Read\",\"file_path\":\"" + RawPath + "\"}"),
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:03:00Z\",\"tool_name\":\"Edit\",\"file_path\":\"" + RawPath + "\",\"sourceText\":\"" + Escape(RawSource) + "\"}"),
                Entry("{\"provider\":\"claude\",\"timestamp\":\"2026-05-14T01:04:00Z\",\"tool_name\":\"Bash\",\"command\":\"" + RawCommand + "\",\"" + RawUsername + "\":\"true\"}")
            });

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(AgentProviderType.Claude, result.Value.ProviderType);
            Assert.AreEqual("2026-05-14", result.Value.DayBucket);
            Assert.AreEqual(CountBucket.One, result.Value.SessionCountBucket);
            AssertHasTool(result.Value, AgentToolUsageCategory.CodeEditing);
            AssertHasTool(result.Value, AgentToolUsageCategory.TestRun);
            AssertHasLanguage(result.Value, AgentLanguageCategory.CSharp);
            AssertLoweredConfidence(result.Value);
            Assert.Contains("agent_log_risky_content_discarded", result.Value.WarningIds);
            Assert.AreEqual(ClaudeAgentLogParser.Version, result.Value.AnalyzerVersion);
            AssertNoRawFixtureData(result.Value);
        }

        [Test]
        public void CodexSessionFixture_ProducesSafeAggregateOnly()
        {
            var result = new CodexAgentLogParser().Parse(Input(AgentProviderType.Codex), new List<AgentLogEntry>
            {
                Entry("{\"provider\":\"codex\",\"created_at\":\"2026-05-14T03:00:00Z\",\"conversation_id\":\"codex-conversation-a\",\"event\":\"message\",\"role\":\"user\",\"prompt\":\"" + RawPrompt + "\"}"),
                Entry("{\"provider\":\"codex\",\"created_at\":\"2026-05-14T03:01:00Z\",\"tool\":\"exec_command\",\"command\":\"dotnet test --filter SafeAggregate\"}"),
                Entry("{\"provider\":\"codex\",\"created_at\":\"2026-05-14T03:02:00Z\",\"tool\":\"apply_patch\",\"target\":\"" + RawPath + "\",\"response\":\"" + RawResponse + "\"}")
            });

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(AgentProviderType.Codex, result.Value.ProviderType);
            Assert.AreEqual("2026-05-14", result.Value.DayBucket);
            AssertHasTool(result.Value, AgentToolUsageCategory.CodeEditing);
            AssertHasTool(result.Value, AgentToolUsageCategory.TestRun);
            AssertHasLanguage(result.Value, AgentLanguageCategory.CSharp);
            AssertLoweredConfidence(result.Value);
            Assert.Contains("agent_log_risky_content_discarded", result.Value.WarningIds);
            Assert.AreEqual(CodexAgentLogParser.Version, result.Value.AnalyzerVersion);
            AssertNoRawFixtureData(result.Value);
        }

        [Test]
        public void UnknownMixedFixture_ReturnsLowConfidenceWarningsWithoutLeaking()
        {
            var result = new UnknownAgentLogParser().Parse(Input(AgentProviderType.Unknown), new List<AgentLogEntry>
            {
                Entry("2026-05-14T04:00:00Z session_id=unknown-session tool=search target=" + RawPath),
                Entry("2026-05-14T04:01:00Z message response " + RawResponse),
                Entry("malformed-json {\"prompt\":\"" + RawPrompt + "\"")
            });

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(AgentProviderType.Unknown, result.Value.ProviderType);
            Assert.AreEqual(ConfidenceLevel.Low, result.Value.ConfidenceLevel);
            Assert.Contains("agent_log_unknown_provider", result.Value.WarningIds);
            Assert.Contains("agent_log_risky_content_discarded", result.Value.WarningIds);
            AssertHasTool(result.Value, AgentToolUsageCategory.Search);
            AssertHasLanguage(result.Value, AgentLanguageCategory.CSharp);
            AssertNoRawFixtureData(result.Value);
        }

        [Test]
        public void MalformedAndPartialSchemaFixture_UsesWarningsAndSafeBuckets()
        {
            var result = new CodexAgentLogParser().Parse(Input(AgentProviderType.Codex), new List<AgentLogEntry>
            {
                Entry("{not-json"),
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T05:00:00Z\",\"event\":\"partial\"}"),
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T05:01:00Z\",\"tool\":\"grep\",\"args\":\"" + RawToken + "\"}"),
                Entry("{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T05:02:00Z\",\"tool\":\"write\",\"file_path\":\"" + RawPath + "\"}")
            });

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(AgentProviderType.Codex, result.Value.ProviderType);
            Assert.AreEqual("2026-05-14", result.Value.DayBucket);
            AssertLoweredConfidence(result.Value);
            Assert.Contains("agent_log_risky_content_discarded", result.Value.WarningIds);
            AssertHasTool(result.Value, AgentToolUsageCategory.Search);
            AssertHasTool(result.Value, AgentToolUsageCategory.CodeEditing);
            AssertNoRawFixtureData(result.Value);
        }

        [Test]
        public void UnsupportedSchemaFixture_DoesNotInventRawDetails()
        {
            var result = new UnknownAgentLogParser().Parse(Input(AgentProviderType.Unknown), new List<AgentLogEntry>
            {
                Entry("unsupported payload without timestamp or usable event fields"),
                Entry("another unsupported payload with " + RawSecret)
            });

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.AreEqual(ConfidenceLevel.Low, result.Value.ConfidenceLevel);
            Assert.Contains("agent_log_unknown_provider", result.Value.WarningIds);
            Assert.Contains("agent_log_unsupported_format", result.Value.WarningIds);
            Assert.Contains("agent_log_risky_content_discarded", result.Value.WarningIds);
            AssertNoRawFixtureData(result.Value);
        }

        private static AgentAnalysisInput Input(AgentProviderType providerType)
        {
            return new AgentAnalysisInput
            {
                SelectedLocationPath = "/Users/alice/SecretRepo/.agent/logs",
                ProviderHint = providerType,
                AnalysisWindowDays = 7,
                MaxFilesToScan = 20,
                MaxLogEntriesToScan = 200
            };
        }

        private static AgentLogEntry Entry(string text)
        {
            return new AgentLogEntry
            {
                Text = text,
                LastWriteTimeUtc = new DateTimeOffset(2026, 5, 14, 0, 0, 0, TimeSpan.Zero)
            };
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void AssertHasTool(AgentActivitySummary summary, AgentToolUsageCategory category)
        {
            Assert.IsTrue((summary.ToolUsageCategoryBuckets ?? new List<AgentToolUsageCategoryBucket>())
                .Any(item => item.Category == category && item.CountBucket != CountBucket.None), category.ToString());
        }

        private static void AssertHasLanguage(AgentActivitySummary summary, AgentLanguageCategory category)
        {
            Assert.IsTrue((summary.LanguageCategoryBuckets ?? new List<AgentLanguageCategoryBucket>())
                .Any(item => item.Category == category && item.CountBucket != CountBucket.None), category.ToString());
        }

        private static void AssertLoweredConfidence(AgentActivitySummary summary)
        {
            Assert.IsTrue(
                summary.ConfidenceLevel == ConfidenceLevel.Low || summary.ConfidenceLevel == ConfidenceLevel.Medium,
                summary.ConfidenceLevel.ToString());
        }

        private static void AssertNoRawFixtureData(object value)
        {
            Assert.IsFalse(ObjectContainsString(value, RawPrompt), RawPrompt);
            Assert.IsFalse(ObjectContainsString(value, RawResponse), RawResponse);
            Assert.IsFalse(ObjectContainsString(value, RawPath), RawPath);
            Assert.IsFalse(ObjectContainsString(value, RawFileName), RawFileName);
            Assert.IsFalse(ObjectContainsString(value, RawCommand), RawCommand);
            Assert.IsFalse(ObjectContainsString(value, RawSource), RawSource);
            Assert.IsFalse(ObjectContainsString(value, RawUsername), RawUsername);
            Assert.IsFalse(ObjectContainsString(value, "alice"), "alice");
            Assert.IsFalse(ObjectContainsString(value, RawToken), RawToken);
            Assert.IsFalse(ObjectContainsString(value, RawSecret), RawSecret);
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(value).IsSuccess);
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new HashSet<object>());
        }

        private static bool ObjectContainsString(object value, string expected, HashSet<object> visited)
        {
            if (value == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            if (value is string text)
            {
                return text.Contains(expected);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(DateTimeOffset) || type == typeof(DateTime))
            {
                return false;
            }

            if (!visited.Add(value))
            {
                return false;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (ObjectContainsString(item, expected, visited))
                    {
                        return true;
                    }
                }

                return false;
            }

            foreach (var property in type.GetProperties())
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (ObjectContainsString(property.GetValue(value), expected, visited))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
