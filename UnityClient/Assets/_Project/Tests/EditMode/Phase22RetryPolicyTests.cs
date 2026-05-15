using System;
using NUnit.Framework;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase22RetryPolicyTests
    {
        [Test]
        public void ClassifiesRetryableAndNonRetryableErrors()
        {
            var policy = new SafeSyncRetryPolicy();

            Assert.AreEqual(SafeSyncRetryClassification.Retryable, policy.Classify(SafeSyncApiError.NetworkTimeout));
            Assert.AreEqual(SafeSyncRetryClassification.Retryable, policy.Classify(SafeSyncApiError.ServerUnavailable));
            Assert.AreEqual(SafeSyncRetryClassification.Retryable, policy.Classify(SafeSyncApiError.RateLimited));
            Assert.AreEqual(SafeSyncRetryClassification.AuthRequired, policy.Classify(SafeSyncApiError.AuthRequired));
            Assert.AreEqual(SafeSyncRetryClassification.NonRetryable, policy.Classify(SafeSyncApiError.PrivacyGuardBlockedPayload));
            Assert.AreEqual(SafeSyncRetryClassification.NonRetryable, policy.Classify(SafeSyncApiError.ValidationFailed));
            Assert.AreEqual(SafeSyncRetryClassification.NonRetryable, policy.Classify(SafeSyncApiError.UnknownSchemaVersion));
            Assert.AreEqual(SafeSyncRetryClassification.Conflict, policy.Classify("HTTP_409"));
        }

        [Test]
        public void CalculatesDeterministicBackoffAndMaxAttempts()
        {
            var now = new DateTimeOffset(2026, 5, 15, 1, 0, 0, TimeSpan.Zero);
            var policy = new SafeSyncRetryPolicy(4);

            Assert.AreEqual(now, policy.CalculateNextAttemptAt(now, 0));
            Assert.AreEqual(now.AddSeconds(30), policy.CalculateNextAttemptAt(now, 1));
            Assert.AreEqual(now.AddMinutes(2), policy.CalculateNextAttemptAt(now, 2));
            Assert.AreEqual(now.AddMinutes(10), policy.CalculateNextAttemptAt(now, 3));
            Assert.IsTrue(policy.HasAttemptsRemaining(3, 4));
            Assert.IsFalse(policy.HasAttemptsRemaining(4, 4));
        }
    }
}
