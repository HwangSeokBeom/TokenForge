using System;

namespace TokenForge.Client.Sync
{
    public enum SafeSyncRetryClassification
    {
        Retryable,
        NonRetryable,
        AuthRequired,
        Conflict
    }

    public sealed class SafeSyncRetryPolicy
    {
        public const int DefaultMaxAttempts = 4;

        private readonly TimeSpan[] delays =
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10)
        };

        public int MaxAttempts { get; }

        public SafeSyncRetryPolicy(int maxAttempts = DefaultMaxAttempts)
        {
            MaxAttempts = Math.Max(1, maxAttempts);
        }

        public SafeSyncRetryClassification Classify(string safeErrorCode)
        {
            if (string.IsNullOrWhiteSpace(safeErrorCode))
            {
                return SafeSyncRetryClassification.NonRetryable;
            }

            var code = safeErrorCode.Trim().ToUpperInvariant();
            switch (code)
            {
                case SafeSyncApiError.NetworkTimeout:
                case SafeSyncApiError.Timeout:
                case SafeSyncApiError.NetworkError:
                case SafeSyncApiError.ServerUnavailable:
                case SafeSyncApiError.TemporaryServerError:
                case SafeSyncApiError.RateLimited:
                case "HTTP_408":
                case "HTTP_429":
                case "HTTP_500":
                case "HTTP_502":
                case "HTTP_503":
                case "HTTP_504":
                    return SafeSyncRetryClassification.Retryable;
                case SafeSyncApiError.AuthRequired:
                case "HTTP_401":
                    return SafeSyncRetryClassification.AuthRequired;
                case SafeSyncApiError.ConflictDetected:
                case SafeSyncApiError.ValidationConflict:
                case "HTTP_409":
                    return SafeSyncRetryClassification.Conflict;
                default:
                    if (code == SafeSyncApiError.PrivacyGuardBlockedPayload ||
                        code == SafeSyncApiError.ValidationFailed ||
                        code == SafeSyncApiError.UnsafePayload ||
                        code == SafeSyncApiError.UnknownSchemaVersion ||
                        code == SafeSyncApiError.Forbidden ||
                        code == "HTTP_403")
                    {
                        return SafeSyncRetryClassification.NonRetryable;
                    }

                    return SafeSyncRetryClassification.NonRetryable;
            }
        }

        public DateTimeOffset CalculateNextAttemptAt(DateTimeOffset now, int attemptCount)
        {
            var index = Math.Max(0, Math.Min(attemptCount, delays.Length - 1));
            return now.Add(delays[index]);
        }

        public bool HasAttemptsRemaining(int attemptCount, int maxAttempts)
        {
            var limit = maxAttempts <= 0 ? MaxAttempts : maxAttempts;
            return attemptCount < limit;
        }
    }
}
