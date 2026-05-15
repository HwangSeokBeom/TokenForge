using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Sync;
using TokenForge.Client.UI;

namespace TokenForge.Client.Tests
{
    public sealed class Phase13PrivacyLoggingTests
    {
        private sealed class FakeTransport : ITokenForgeHttpTransport
        {
            public SafeHttpResponse Response { get; set; } = new SafeHttpResponse
            {
                StatusCode = 500,
                BodyJson = "{\"errorCode\":\"SERVER_UNAVAILABLE\",\"message\":\"password123 access-token-secret refresh-token-secret /Users/private\"}"
            };

            public Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Response);
            }
        }

        [Test]
        public void ErrorMapperDoesNotEchoRawServerBodyOrSensitiveValues()
        {
            var rawBody = "{\"message\":\"password123 access-token-secret refresh-token-secret /Users/private raw path\"}";

            var authMessage = SafeUserMessageMapper.FromAuthError(rawBody);
            var syncMessage = SafeUserMessageMapper.FromSyncError(rawBody);

            AssertNoSensitiveValues(authMessage + syncMessage);
        }

        [Test]
        public void AuthAndSyncLogsNeverIncludeCredentialsTokensOrBodies()
        {
            var authLogger = new InMemorySafeSyncLogger();
            var authClient = new AuthApiClient(new AuthApiConfig("http://localhost:3000/api/v1"), new FakeTransport(), authLogger);

            RunAsync(() => authClient.LoginAsync("test@example.com", "password123", CancellationToken.None));

            var syncLogger = new InMemorySafeSyncLogger();
            var syncClient = new SafeSyncApiClient(
                new SafeSyncApiConfig("http://localhost:3000/api/v1"),
                new StaticAuthTokenProvider("access-token-secret"),
                new FakeTransport(),
                syncLogger);

            RunAsync(() => syncClient.GetActivitySessionsAsync(CancellationToken.None));

            var logs = string.Join("\n", authLogger.Entries.Concat(syncLogger.Entries));
            AssertNoSensitiveValues(logs);
        }

        [Test]
        public void SafeUiStatusDoesNotContainTokensFromSession()
        {
            var session = new AuthSession
            {
                AccessToken = "access-token-secret",
                RefreshToken = "refresh-token-secret",
                UserId = "user-1",
                Email = "safe@example.com",
                DisplayName = "Safe User",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
            };

            var status = SafeUserMessageMapper.FromAuth(AuthState.LoggedIn).Message + " " +
                         SafeUserMessageMapper.SessionExpirySummary(AuthState.LoggedIn, session.AccessTokenExpiresAt);

            AssertNoSensitiveValues(status);
        }

        private static void AssertNoSensitiveValues(string value)
        {
            var forbidden = new List<string>
            {
                "password123",
                "access-token-secret",
                "refresh-token-secret",
                "/Users/private",
                "raw path",
                "BodyJson"
            };

            foreach (var item in forbidden)
            {
                Assert.IsFalse((value ?? string.Empty).Contains(item), item);
            }
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
