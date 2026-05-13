using System;
using System.Collections.Generic;

namespace TokenForge.Client.Privacy
{
    public sealed class SyncPayloadSanitizer
    {
        private readonly PrivacySanitizer privacySanitizer;
        private readonly HashSet<string> allowedTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "SafeSyncPayload",
            "SessionSummaryUploadRequest",
            "CharacterSnapshotSyncRequest",
            "SettingsSyncRequest",
            "SessionSummaryDto",
            "WorkTypeDistributionDto",
            "CharacterStatsDto"
        };

        public SyncPayloadSanitizer(PrivacySanitizer privacySanitizer = null)
        {
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
        }

        public PrivacyValidationResult ValidatePayload(object payload)
        {
            var result = new PrivacyValidationResult();
            if (payload == null)
            {
                return result;
            }

            var typeName = payload.GetType().Name;
            if (!allowedTypeNames.Contains(typeName))
            {
                result.Violations.Add($"Sync payload type '{typeName}' is not on the DTO allowlist.");
                return result;
            }

            var privacyResult = privacySanitizer.ValidateObject(payload);
            result.Violations.AddRange(privacyResult.Violations);
            return result;
        }

        public void ThrowIfUnsafe(object payload)
        {
            var result = ValidatePayload(payload);
            if (!result.IsSafe)
            {
                throw new InvalidOperationException("Sync payload validation failed: " + string.Join(", ", result.Violations));
            }
        }
    }
}
