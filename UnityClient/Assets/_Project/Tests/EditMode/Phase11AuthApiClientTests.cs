using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase11AuthApiClientTests
    {
        private sealed class FakeTransport : ITokenForgeHttpTransport
        {
            private readonly Queue<SafeHttpResponse> responses = new Queue<SafeHttpResponse>();
            private readonly Exception exception;

            public FakeTransport(Exception exception = null)
            {
                this.exception = exception;
            }

            public List<SafeHttpRequest> Requests { get; } = new List<SafeHttpRequest>();

            public void Enqueue(SafeHttpResponse response)
            {
                responses.Enqueue(response);
            }

            public Task<SafeHttpResponse> SendAsync(SafeHttpRequest request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (exception != null)
                {
                    throw exception;
                }

                Requests.Add(request);
                return Task.FromResult(responses.Count > 0 ? responses.Dequeue() : new SafeHttpResponse { StatusCode = 200, BodyJson = "{}" });
            }
        }

        [Test]
        public void Login_SendsServerContractRouteAndParsesTokens()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 201, BodyJson = "{\"user\":{\"id\":\"user-1\",\"email\":\"test@example.com\",\"nickname\":\"Tester\",\"isGuest\":false},\"accessToken\":\"access.jwt.value\",\"refreshToken\":\"refresh-secret\"}" });
            var client = CreateClient(transport);

            var result = RunAsync(() => client.LoginAsync("test@example.com", "password123", CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("http://localhost:3000/api/v1/auth/login", transport.Requests.Single().Url);
            Assert.IsTrue(transport.Requests.Single().BodyJson.Contains("\"email\":\"test@example.com\""));
            Assert.AreEqual("access.jwt.value", result.Value.AccessToken);
            Assert.AreEqual("refresh-secret", result.Value.RefreshToken);
            Assert.AreEqual("user-1", result.Value.User.Id);
        }

        [Test]
        public void RegisterRefreshLogoutAndMe_UseCurrentServerRoutes()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 201, BodyJson = "{\"user\":{\"id\":\"user-1\"},\"accessToken\":\"access\",\"refreshToken\":\"refresh\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"accessToken\":\"access-2\",\"refreshToken\":\"refresh-2\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"status\":\"ok\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"id\":\"user-1\",\"email\":\"test@example.com\",\"nickname\":\"Tester\",\"isGuest\":false}" });
            var client = CreateClient(transport);

            RunAsync(() => client.RegisterAsync("test@example.com", "password123", "Tester", CancellationToken.None));
            RunAsync(() => client.RefreshAsync("refresh", CancellationToken.None));
            RunAsync(() => client.LogoutAsync("refresh-2", CancellationToken.None));
            var me = RunAsync(() => client.GetCurrentUserAsync("access-2", CancellationToken.None));

            CollectionAssert.AreEqual(new[]
            {
                "http://localhost:3000/api/v1/auth/signup",
                "http://localhost:3000/api/v1/auth/refresh",
                "http://localhost:3000/api/v1/auth/logout",
                "http://localhost:3000/api/v1/users/me"
            }, transport.Requests.Select(request => request.Url).ToArray());
            Assert.AreEqual("access-2", transport.Requests.Last().BearerToken);
            Assert.AreEqual("Tester", me.Value.Nickname);
        }

        [Test]
        public void LoginFailure_IsSafeAndDoesNotLogPasswordOrToken()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 401, BodyJson = "{\"errorCode\":\"VALIDATION_FAILED\",\"message\":[\"Invalid credentials password123 accessToken\"]}" });
            var logger = new InMemorySafeSyncLogger();
            var client = CreateClient(transport, logger);

            var result = RunAsync(() => client.LoginAsync("test@example.com", "password123", CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(AuthApiError.ValidationFailed, result.ErrorCode);
            var logs = string.Join("\n", logger.Entries);
            Assert.IsFalse(logs.Contains("password123"));
            Assert.IsFalse(logs.Contains("accessToken"));
            Assert.IsFalse(logs.Contains(transport.Requests.Single().BodyJson));
        }

        [Test]
        public void TimeoutAndNetworkFailure_ReturnSafeErrors()
        {
            var timeout = CreateClient(new FakeTransport(new OperationCanceledException()));
            var network = CreateClient(new FakeTransport(new InvalidOperationException()));

            var timeoutResult = RunAsync(() => timeout.LoginAsync("a@example.com", "password123", CancellationToken.None));
            var networkResult = RunAsync(() => network.LoginAsync("a@example.com", "password123", CancellationToken.None));

            Assert.AreEqual(AuthApiError.Timeout, timeoutResult.ErrorCode);
            Assert.AreEqual(AuthApiError.NetworkError, networkResult.ErrorCode);
        }

        private static AuthApiClient CreateClient(FakeTransport transport, InMemorySafeSyncLogger logger = null)
        {
            return new AuthApiClient(
                new AuthApiConfig("http://localhost:3000/api/v1"),
                transport,
                logger ?? new InMemorySafeSyncLogger());
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
