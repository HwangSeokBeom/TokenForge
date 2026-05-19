using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Agents;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
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
        public void PrivacySanitizer_RejectsForbiddenJsonFields()
        {
            var sanitizer = new PrivacySanitizer();

            var result = sanitizer.ValidateNoForbiddenFields("{\"commandText\":\"git status\",\"tokenRaw\":\"abc\"}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("privacy_forbidden_data", result.ErrorCode);
            Assert.IsNotEmpty(result.Warnings);
        }

        [Test]
        public void DomainSaveModels_DoNotExposeForbiddenRawFields()
        {
            var detector = new ForbiddenFieldDetector();
            var modelTypes = new[]
            {
                typeof(AgentWorkSession),
                typeof(AgentActionSummary),
                typeof(GitChangeSummary),
                typeof(CharacterProfile),
                typeof(CharacterStats),
                typeof(CompanionState),
                typeof(CompanionGrowthProfile),
                typeof(AchievementProgress),
                typeof(SaveData)
            };

            foreach (var type in modelTypes)
            {
                foreach (var property in type.GetProperties())
                {
                    Assert.IsFalse(detector.IsForbiddenFieldName(property.Name), $"{type.Name}.{property.Name}");
                }
            }
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
        public void SafeSyncUploadRequest_ContainsOnlyPrivacySafeAggregateFields()
        {
            var contract = SafeSyncContractBundleFixtures.CreateValidFixture();
            var request = SafeSyncUploadRequest.FromApprovedAggregateRequest(contract);
            var validation = new SyncPayloadSanitizer().ValidatePayload(request);

            Assert.IsTrue(validation.IsSafe, string.Join(",", validation.Violations));
            SerializerFreePrivacyDtoAssert.DoesNotContainForbiddenMembersOrValues(request);
            Assert.IsTrue(request.Sessions.Count >= 1, $"Expected {request.Sessions.Count} to be greater than or equal to 1.");
        }

        [Test]
        public void ForbiddenFieldDetector_RejectsUnsafeSyncDtoFieldNames()
        {
            var detector = new ForbiddenFieldDetector();
            var forbidden = new[]
            {
                "rawPath",
                "rawLog",
                "sourceText",
                "diff",
                "prompt",
                "fileName",
                "commitMessage",
                "branchName",
                "remoteUrl"
            };

            foreach (var fieldName in forbidden)
            {
                Assert.IsTrue(detector.IsForbiddenFieldName(fieldName), fieldName);
            }
        }

        [Test]
        public void JsonSaveDataStore_CanSerializeAndDeserializeWithoutRawFields()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var saveData = SaveData.CreateDefault();
            saveData.CharacterProfile.TotalExp = 42;
            var store = new JsonSaveDataStore(directory);

            Task.Run(async () => await store.SaveAsync(saveData, CancellationToken.None)).GetAwaiter().GetResult();
            var loaded = Task.Run(async () => await store.LoadAsync(CancellationToken.None)).GetAwaiter().GetResult();
            var json = File.ReadAllText(Path.Combine(directory, "tokenforge-save.json"));

            Assert.AreEqual(42, loaded.CharacterProfile.TotalExp);
            Assert.IsTrue(json.Contains("SaveVersion"));
            Assert.IsFalse(json.Contains("RawPrompt"));
            Assert.IsFalse(json.Contains("GitRemoteUrl"));
        }

        [Test]
        public void SaveDataRepository_DoesNotSaveUnsafeSessionData()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                WorkType = WorkType.Feature,
                Warnings = { "prompt: do not persist" }
            });

            var result = Task.Run(async () => await repository.SaveAsync(saveData, CancellationToken.None)).GetAwaiter().GetResult();

            Assert.IsFalse(result.IsSuccess);
            Assert.IsFalse(File.Exists(Path.Combine(directory, SaveDataRepository.SaveFileName)));
        }

        [Test]
        public void ManualSessionProvider_CreatesSafeAgentWorkSession()
        {
            var sanitizer = new PrivacySanitizer();
            var provider = new ManualSessionProvider(new ManualSessionInput
            {
                AgentType = AgentType.ManualFallback,
                WorkType = WorkType.Test,
                ApproximateTokenCount = 5000,
                ChangedFileCount = 2,
                ApproximateAddedLineCount = 80,
                ApproximateDeletedLineCount = 12,
                TestFileChanged = true,
                TestRunDetected = true,
                ResultStatus = ResultStatus.Succeeded,
                Confidence = ProviderConfidence.Medium
            }, sanitizer);

            var result = provider.AnalyzeAsync(new AgentProviderContext
            {
                ProjectPathHash = "safehash",
                ScanStartedAt = System.DateTimeOffset.UtcNow.AddMinutes(-10),
                ScanEndedAt = System.DateTimeOffset.UtcNow
            }, CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(WorkType.Test, result.Session.WorkType);
            Assert.AreEqual(TokenUsageBucket.Medium, result.Session.TokenUsageBucket);
            Assert.AreEqual(LineChangeBucket.Medium, result.Session.GitChangeSummary.AddedLineBucket);
            Assert.IsTrue(sanitizer.ValidateSafeSession(result.Session).IsSuccess);
        }

        [Test]
        public void SaveDataRepository_RoundTripPreservesCharacterAndSessions()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            saveData.CharacterProfile.Level = 3;
            saveData.CharacterProfile.TotalExp = 2400;
            saveData.CharacterProfile.Stats.Logic = 7;
            saveData.CharacterProfile.Stats.Debug = 4;
            saveData.WorkSessionSummaries.Add(new AgentWorkSession
            {
                SessionId = "session-safe-1",
                WorkType = WorkType.Refactor,
                TokenUsageBucket = TokenUsageBucket.Small,
                ResultStatus = ResultStatus.Succeeded,
                GitChangeSummary = new GitChangeSummary
                {
                    ChangedFileCount = 4,
                    AddedLineBucket = LineChangeBucket.Medium,
                    DeletedLineBucket = LineChangeBucket.Small,
                    ProjectPathHash = "projecthash"
                }
            });

            var saveResult = Task.Run(async () => await repository.SaveAsync(saveData, CancellationToken.None)).GetAwaiter().GetResult();
            var loaded = Task.Run(async () => await repository.LoadAsync(CancellationToken.None)).GetAwaiter().GetResult();

            Assert.IsTrue(saveResult.IsSuccess, saveResult.ErrorMessage);
            Assert.AreEqual(3, loaded.CharacterProfile.Level);
            Assert.AreEqual(2400, loaded.CharacterProfile.TotalExp);
            Assert.AreEqual(7, loaded.CharacterProfile.Stats.Logic);
            Assert.AreEqual(4, loaded.CharacterProfile.Stats.Debug);
            Assert.AreEqual(1, loaded.WorkSessionSummaries.Count);
            Assert.AreEqual(WorkType.Refactor, loaded.WorkSessionSummaries[0].WorkType);
            Assert.AreEqual(4, loaded.WorkSessionSummaries[0].GitChangeSummary.ChangedFileCount);
        }

        private static class SerializerFreePrivacyDtoAssert
        {
            private static readonly HashSet<string> AllowedAggregateMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TokenUsageBucket",
                "PromptCount",
                "FileEditCount",
                "FileCountBucket",
                "ChangedFileCount",
                "ProjectPathHash",
                "SourceProvider",
                "SourceProviders",
                "SupportedSourceProviders",
                "HashedRepositoryId",
                "CommitCountBucket",
                "SafeDiffFieldNames"
            };

            private static readonly string[] ForbiddenMemberNameFragments =
            {
                "rawpath",
                "path",
                "filename",
                "file",
                "sourcecode",
                "diff",
                "patch",
                "prompt",
                "log",
                "rawprompt",
                "rawlog",
                "commitmessage",
                "branch",
                "remote",
                "author",
                "token",
                "secret",
                "authorization"
            };

            public static void DoesNotContainForbiddenMembersOrValues(object value)
            {
                var detector = new ForbiddenFieldDetector();
                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

                Inspect(value, "$", detector, visited);
            }

            private static void Inspect(object value, string path, ForbiddenFieldDetector detector, HashSet<object> visited)
            {
                if (value == null)
                {
                    return;
                }

                if (value is string stringValue)
                {
                    Assert.IsFalse(detector.ContainsSensitiveString(stringValue), $"{path} contains a forbidden raw-data value.");
                    return;
                }

                var type = value.GetType();
                if (IsLeafType(type))
                {
                    return;
                }

                if (!type.IsValueType && !visited.Add(value))
                {
                    return;
                }

                if (value is IDictionary dictionary)
                {
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        Inspect(entry.Key, $"{path}.<key>", detector, visited);
                        Inspect(entry.Value, $"{path}[{entry.Key}]", detector, visited);
                    }

                    return;
                }

                if (value is IEnumerable enumerable)
                {
                    var index = 0;
                    foreach (var item in enumerable)
                    {
                        Inspect(item, $"{path}[{index}]", detector, visited);
                        index++;
                    }

                    return;
                }

                const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
                foreach (var field in type.GetFields(PublicInstance))
                {
                    AssertMemberNameIsSafe(field.Name, $"{path}.{field.Name}", detector);
                    foreach (var serializedName in GetSerializedNames(field))
                    {
                        AssertMemberNameIsSafe(serializedName, $"{path}.{field.Name}[serializedName]", detector);
                    }

                    Inspect(field.GetValue(value), $"{path}.{field.Name}", detector, visited);
                }

                foreach (var property in type.GetProperties(PublicInstance))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                    {
                        continue;
                    }

                    AssertMemberNameIsSafe(property.Name, $"{path}.{property.Name}", detector);
                    foreach (var serializedName in GetSerializedNames(property))
                    {
                        AssertMemberNameIsSafe(serializedName, $"{path}.{property.Name}[serializedName]", detector);
                    }

                    Inspect(property.GetValue(value, null), $"{path}.{property.Name}", detector, visited);
                }
            }

            private static void AssertMemberNameIsSafe(string memberName, string path, ForbiddenFieldDetector detector)
            {
                if (string.IsNullOrWhiteSpace(memberName) || AllowedAggregateMemberNames.Contains(memberName))
                {
                    return;
                }

                Assert.IsFalse(detector.IsForbiddenFieldName(memberName), $"{path} uses forbidden field name '{memberName}'.");

                var normalized = Normalize(memberName);
                foreach (var fragment in ForbiddenMemberNameFragments)
                {
                    Assert.IsFalse(normalized.Contains(fragment), $"{path} contains forbidden raw-data fragment '{fragment}'.");
                }
            }

            private static IEnumerable<string> GetSerializedNames(MemberInfo member)
            {
                foreach (var attribute in member.GetCustomAttributes(false))
                {
                    if (attribute.GetType().Name != "JsonPropertyAttribute")
                    {
                        continue;
                    }

                    var propertyName = attribute.GetType().GetProperty("PropertyName")?.GetValue(attribute, null) as string;
                    if (!string.IsNullOrWhiteSpace(propertyName))
                    {
                        yield return propertyName;
                    }
                }
            }

            private static bool IsLeafType(Type type)
            {
                return type.IsPrimitive ||
                    type.IsEnum ||
                    type == typeof(decimal) ||
                    type == typeof(DateTime) ||
                    type == typeof(DateTimeOffset) ||
                    type == typeof(TimeSpan) ||
                    type == typeof(Guid);
            }

            private static string Normalize(string value)
            {
                return value.Replace("_", string.Empty)
                    .Replace("-", string.Empty)
                    .Replace(" ", string.Empty)
                    .ToLowerInvariant();
            }

            private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
            {
                public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

                public new bool Equals(object x, object y)
                {
                    return ReferenceEquals(x, y);
                }

                public int GetHashCode(object obj)
                {
                    return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
                }
            }
        }
    }
}
