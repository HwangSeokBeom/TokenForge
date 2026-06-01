using System;
using System.Collections.Generic;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TokenForge.Client.Privacy
{
    public sealed class PrivacyValidationResult
    {
        public bool IsSafe => Violations.Count == 0;
        public List<string> Violations { get; } = new List<string>();
        public List<PrivacyWarning> Warnings { get; } = new List<PrivacyWarning>();
    }

    public sealed class PrivacySanitizer
    {
        private readonly ForbiddenFieldDetector detector;
        private readonly JsonSerializer serializer;

        public PrivacySanitizer(ForbiddenFieldDetector detector = null)
        {
            this.detector = detector ?? new ForbiddenFieldDetector();
            serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            });
        }

        public PrivacyValidationResult ValidateObject(object payload)
        {
            var result = new PrivacyValidationResult();
            if (payload == null)
            {
                return result;
            }

            var token = JToken.FromObject(payload, serializer);
            InspectToken(token, "$", result);

            return result;
        }

        public string HashString(string input)
        {
            return SafeHashUtility.ComputeProjectPathHash(input, "TokenForge.HashString.v1");
        }

        public string SanitizeProjectPath(string path)
        {
            return SafeHashUtility.ComputeProjectPathHash(path);
        }

        public LineChangeBucket BucketLineCount(int lineCount)
        {
            if (lineCount <= 0) return LineChangeBucket.None;
            if (lineCount <= 25) return LineChangeBucket.Small;
            if (lineCount <= 150) return LineChangeBucket.Medium;
            if (lineCount <= 600) return LineChangeBucket.Large;
            return LineChangeBucket.Huge;
        }

        public TokenUsageBucket BucketTokenUsage(int tokenCount)
        {
            if (tokenCount <= 0) return TokenUsageBucket.None;
            if (tokenCount <= 4000) return TokenUsageBucket.Small;
            if (tokenCount <= 16000) return TokenUsageBucket.Medium;
            if (tokenCount <= 64000) return TokenUsageBucket.Large;
            return TokenUsageBucket.Huge;
        }

        public Result ValidateNoForbiddenFields(object payload)
        {
            return ToResult(ValidateObject(payload));
        }

        public Result ValidateNoForbiddenFields(string json)
        {
            var result = new PrivacyValidationResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                return Result.Success();
            }

            try
            {
                InspectToken(JToken.Parse(json), "$", result);
            }
            catch (JsonReaderException)
            {
                if (detector.ContainsSensitiveString(json))
                {
                    AddViolation(result, "Sensitive string pattern in JSON/text payload", "$");
                }
            }

            return ToResult(result);
        }

        public Result ValidateSafeSession(AgentWorkSession session)
        {
            if (session == null)
            {
                return Result.Failure("privacy_null_session", "Session is required.");
            }

            return ValidateNoForbiddenFields(session);
        }

        public Result ValidateSafeSaveData(SaveData saveData)
        {
            if (saveData == null)
            {
                return Result.Failure("privacy_null_save_data", "Save data is required.");
            }

            var validation = ValidateObject(saveData);
            return ToResult(validation);
        }

        public void ThrowIfUnsafe(object payload)
        {
            var result = ValidateObject(payload);
            if (!result.IsSafe)
            {
                throw new InvalidOperationException("Privacy validation failed: " + string.Join(", ", result.Violations));
            }
        }

        private void InspectToken(JToken token, string path, PrivacyValidationResult result)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                    foreach (var property in ((JObject)token).Properties())
                    {
                        var propertyPath = $"{path}.{property.Name}";
                        if (detector.IsForbiddenFieldName(property.Name))
                        {
                            AddViolation(result, $"Forbidden field '{property.Name}'", propertyPath);
                        }

                        InspectToken(property.Value, propertyPath, result);
                    }
                    break;
                case JTokenType.Array:
                    var index = 0;
                    foreach (var item in ((JArray)token).Children())
                    {
                        InspectToken(item, $"{path}[{index}]", result);
                        index++;
                    }
                    break;
                case JTokenType.String:
                    var value = token.Value<string>();
                    if (detector.ContainsSensitiveString(value))
                    {
                        AddViolation(result, "Sensitive string pattern", path);
                    }

                    if (IsCompanionGrowthReasonPath(path) && !IsSafeCompanionGrowthReasonId(value))
                    {
                        AddViolation(result, "Unsafe companion growth reason id", path);
                    }
                    break;
            }
        }

        private static bool IsCompanionGrowthReasonPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.IndexOf(".LastGrowthReasonIds[", StringComparison.Ordinal) >= 0;
        }

        private static bool IsSafeCompanionGrowthReasonId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();
            if (value.Length > 80)
            {
                return false;
            }

            foreach (var character in value)
            {
                var isLowercaseLetter = character >= 'a' && character <= 'z';
                var isDigit = character >= '0' && character <= '9';
                if (!isLowercaseLetter && !isDigit && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddViolation(PrivacyValidationResult result, string message, string location)
        {
            result.Violations.Add($"{message} at {location}");
            result.Warnings.Add(new PrivacyWarning
            {
                Code = "privacy_forbidden_data",
                Message = message,
                Location = location
            });
        }

        private static Result ToResult(PrivacyValidationResult validation)
        {
            if (validation.IsSafe)
            {
                return Result.Success();
            }

            var result = Result.Failure("privacy_forbidden_data", string.Join(", ", validation.Violations));
            foreach (var warning in validation.Warnings)
            {
                result.Warnings.Add($"{warning.Code}: {warning.Message} ({warning.Location})");
            }

            return result;
        }
    }
}
