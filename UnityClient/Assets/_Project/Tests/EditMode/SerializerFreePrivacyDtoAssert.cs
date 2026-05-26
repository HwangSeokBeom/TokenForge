using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Tests
{
    internal static class SerializerFreePrivacyDtoAssert
    {
        private static readonly HashSet<string> AllowedAggregateMemberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TokenUsageBucket",
            "TokenRange",
            "TokenRangeLabel",
            "TokenBucketMultiplier",
            "Logic",
            "Architecture",
            "Design",
            "Stability",
            "Velocity",
            "Creativity",
            "Efficiency",
            "Stress",
            "PromptCount",
            "FileEditCount",
            "FileCountBucket",
            "ChangedFileCount",
            "ProjectPathHash",
            "SourceKind",
            "SafeSourceAlias",
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
            "absolutepath",
            "repositoryabsolutepath",
            "agentrawlogpath",
            "manualfolderrawpath",
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
            "accesstoken",
            "refreshtoken",
            "apikey",
            "privatekey",
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
