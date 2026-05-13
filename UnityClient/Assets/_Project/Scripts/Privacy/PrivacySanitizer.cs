using System;
using System.Collections.Generic;
using System.Text.Json;

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
        private readonly JsonSerializerOptions serializerOptions;

        public PrivacySanitizer(ForbiddenFieldDetector detector = null)
        {
            this.detector = detector ?? new ForbiddenFieldDetector();
            serializerOptions = new JsonSerializerOptions
            {
                WriteIndented = false
            };
        }

        public PrivacyValidationResult ValidateObject(object payload)
        {
            var result = new PrivacyValidationResult();
            if (payload == null)
            {
                return result;
            }

            var json = JsonSerializer.Serialize(payload, payload.GetType(), serializerOptions);
            using (var document = JsonDocument.Parse(json))
            {
                InspectElement(document.RootElement, "$", result);
            }

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

        private void InspectElement(JsonElement element, string path, PrivacyValidationResult result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var propertyPath = $"{path}.{property.Name}";
                        if (detector.IsForbiddenFieldName(property.Name))
                        {
                            result.Violations.Add($"Forbidden field '{property.Name}' at {propertyPath}");
                        }

                        InspectElement(property.Value, propertyPath, result);
                    }
                    break;
                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        InspectElement(item, $"{path}[{index}]", result);
                        index++;
                    }
                    break;
                case JsonValueKind.String:
                    var value = element.GetString();
                    if (detector.ContainsSensitiveString(value))
                    {
                        result.Violations.Add($"Sensitive string pattern at {path}");
                    }
                    break;
            }
        }
    }
}
