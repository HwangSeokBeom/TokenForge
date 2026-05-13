using System.Text.Json;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class PrivacyAndSyncTests
    {
        private sealed class UnsafePayload
        {
            public string RawPrompt { get; set; } = "do not store";
        }

        [Test]
        public void ForbiddenFieldDetector_AllowsTokenBucketButBlocksRawPrompt()
        {
            var detector = new ForbiddenFieldDetector();

            Assert.IsFalse(detector.IsForbiddenFieldName("TokenUsageBucket"));
            Assert.IsFalse(detector.IsForbiddenFieldName("PromptCount"));
            Assert.IsTrue(detector.IsForbiddenFieldName("RawPrompt"));
        }

        [Test]
        public void PrivacySanitizer_DetectsForbiddenField()
        {
            var sanitizer = new PrivacySanitizer();

            var result = sanitizer.ValidateObject(new UnsafePayload());

            Assert.IsFalse(result.IsSafe);
        }

        [Test]
        public void SafeSyncMapper_ProducesPrivacySafePayload()
        {
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                AgentType = AgentType.GitOnly,
                WorkType = WorkType.Docs,
                TokenUsageBucket = TokenUsageBucket.Small,
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 2,
                    ProjectPathHash = "abc123"
                }
            });

            var payload = new SafeSyncMapper().ToPayload(saveData);
            var validation = new SyncPayloadSanitizer().ValidatePayload(payload);

            Assert.IsTrue(validation.IsSafe, string.Join(",", validation.Violations));
            Assert.AreEqual(1, payload.SessionSummary.Sessions.Count);
            Assert.AreEqual("abc123", payload.SessionSummary.Sessions[0].ProjectPathHash);
        }

        [Test]
        public void SaveData_CanSerializeWithoutRawFields()
        {
            var saveData = SaveData.CreateDefault();

            var json = JsonSerializer.Serialize(saveData);

            Assert.IsTrue(json.Contains("SaveVersion"));
            Assert.IsFalse(json.Contains("RawPrompt"));
            Assert.IsFalse(json.Contains("GitRemoteUrl"));
        }
    }
}
