using System.IO;
using System.Threading;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class AgentSourceDetectionPrivacyTests
    {
        [Test]
        public void ProviderList_UsesCodexInsteadOfLegacyAssistantProvider()
        {
            var state = new OnboardingState();

            Assert.IsTrue(state.AgentSources.Exists(source => source.SourceType == ConnectedAgentSourceType.Codex && source.DisplayName == "Codex"));
            Assert.IsFalse(ObjectContainsString(state, "Chat" + "GPT"));
        }

        [Test]
        public void LegacyAssistantProviderValue_NormalizesToCodexWithoutCrash()
        {
            Assert.AreEqual(AgentProviderType.Codex, MacAgentSourceDetector.NormalizeProviderValue("Chat" + "GPT"));
        }

        [Test]
        public void CursorCandidatePathExists_ProducesSafeDetectedResult()
        {
            var home = CreateHomeWith("Library/Application Support/Cursor/User/globalStorage", "cursor-log.jsonl");
            var result = RunDetector(AgentProviderType.Cursor, home);

            Assert.AreEqual(AgentSourceAccessState.Detected, result.AccessState);
            Assert.IsTrue(result.HasUsableCandidate);
            Assert.That(result.BestCandidate().SafeAlias, Does.Contain("Cursor"));
            AssertNoRawPath(result.BestCandidate().SafeAlias, home);
        }

        [Test]
        public void ClaudeCandidatePathExists_ProducesSafeDetectedResult()
        {
            var home = CreateHomeWith(".claude/projects", "session.jsonl");
            var result = RunDetector(AgentProviderType.ClaudeCode, home);

            Assert.AreEqual(AgentSourceAccessState.Detected, result.AccessState);
            Assert.IsTrue(result.HasUsableCandidate);
            Assert.That(result.BestCandidate().SafeAlias, Does.Contain("Claude Code"));
            AssertNoRawPath(result.BestCandidate().SafeAlias, home);
        }

        [Test]
        public void CodexCandidatePathExists_ProducesSafeDetectedResult()
        {
            var home = CreateHomeWith(".codex", "session.jsonl");
            var result = RunDetector(AgentProviderType.Codex, home);

            Assert.AreEqual(AgentSourceAccessState.Detected, result.AccessState);
            Assert.IsTrue(result.HasUsableCandidate);
            Assert.That(result.BestCandidate().SafeAlias, Does.Contain("Codex"));
            AssertNoRawPath(result.BestCandidate().SafeAlias, home);
        }

        [Test]
        public void CopilotCandidatePathExists_ProducesLimitedDetectedResult()
        {
            var home = CreateHomeWith("Library/Application Support/Code/User/globalStorage", "copilot.log");
            var result = RunDetector(AgentProviderType.GitHubCopilot, home);

            Assert.AreEqual(AgentSourceAccessState.LimitedSupport, result.AccessState);
            Assert.IsTrue(result.HasUsableCandidate);
            Assert.That(result.BestCandidate().WarningIds, Does.Contain("agent_source_limited_support"));
            AssertNoRawPath(result.BestCandidate().SafeAlias, home);
        }

        [Test]
        public void MissingCodexCandidate_DoesNotCrashAndRequiresManualImport()
        {
            var home = CreateTempDirectory();
            var result = RunDetector(AgentProviderType.Codex, home);

            Assert.AreEqual(AgentSourceAccessState.NotDetected, result.AccessState);
            Assert.IsFalse(result.HasUsableCandidate);
            Assert.That(result.WarningIds, Does.Contain("agent_source_not_detected"));
        }

        [Test]
        public void DetectedSource_ApproveAnalyzeSaveFlowDoesNotExposeRawPath()
        {
            var home = CreateHomeWith(".codex", "session.jsonl", "{\"provider\":\"codex\",\"timestamp\":\"2026-05-14T03:00:00Z\",\"session_id\":\"safe\",\"type\":\"message\"}");
            var result = RunDetector(AgentProviderType.Codex, home);
            var candidate = result.BestCandidate();

            var input = new AgentAnalysisInput
            {
                ProviderHint = AgentProviderType.Codex,
                SelectedLocationPath = candidate.LocalPath,
                SourceKind = candidate.SourceKind,
                SafeSourceAlias = candidate.SafeAlias
            };
            var analysis = RunAsync(() => new AgentActivityAnalyzer().AnalyzeAsync(input, CancellationToken.None));

            Assert.IsTrue(analysis.IsSuccess, analysis.ErrorMessage);
            Assert.AreEqual(AgentProviderType.Codex, analysis.Value.ProviderType);
            Assert.AreEqual(AgentSourceKind.DetectedLocal, analysis.Value.SourceKind);
            Assert.IsTrue(new PrivacySanitizer().ValidateNoForbiddenFields(analysis.Value).IsSuccess);
            AssertNoRawPath(analysis.Value, home);
        }

        private static AgentSourceDetectionResult RunDetector(AgentProviderType providerType, string home)
        {
            return RunAsync(() => new MacAgentSourceDetector(providerType, home, home).DetectAsync(CancellationToken.None));
        }

        private static string CreateHomeWith(string relativeDirectory, string fileName, string content = "{\"timestamp\":\"2026-05-14T00:00:00Z\"}")
        {
            var home = CreateTempDirectory();
            var directory = Path.Combine(home, relativeDirectory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), content);
            return home;
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }

        private static T RunAsync<T>(System.Func<System.Threading.Tasks.Task<T>> action)
        {
            return action().GetAwaiter().GetResult();
        }

        private static void AssertNoRawPath(object value, string rawPath)
        {
            Assert.IsFalse(ObjectContainsString(value, rawPath), rawPath);
        }

        private static bool ObjectContainsString(object value, string expected)
        {
            return ObjectContainsString(value, expected, new System.Collections.Generic.HashSet<object>());
        }

        private static bool ObjectContainsString(object value, string expected, System.Collections.Generic.HashSet<object> visited)
        {
            if (value == null || string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            if (value is string text)
            {
                return text.Contains(expected);
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(System.DateTimeOffset) || type == typeof(System.DateTime))
            {
                return false;
            }

            if (!visited.Add(value))
            {
                return false;
            }

            var enumerable = value as System.Collections.IEnumerable;
            if (enumerable != null)
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
