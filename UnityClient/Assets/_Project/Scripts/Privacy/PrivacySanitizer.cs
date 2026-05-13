using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TokenForge.Client.Privacy
{
    public sealed class PrivacyValidationResult
    {
        public bool IsSafe => Violations.Count == 0;
        public List<string> Violations { get; } = new List<string>();
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
                            result.Violations.Add($"Forbidden field '{property.Name}' at {propertyPath}");
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
                        result.Violations.Add($"Sensitive string pattern at {path}");
                    }
                    break;
            }
        }
    }
}
