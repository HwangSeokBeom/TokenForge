using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Sync;

namespace TokenForge.Client.Tests
{
    public sealed class Phase10SafeSyncApiClientTests
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
                return Task.FromResult(responses.Count > 0
                    ? responses.Dequeue()
                    : new SafeHttpResponse { StatusCode = 200, BodyJson = "{}" });
            }
        }

        [Test]
        public void BuildsCorrectEndpointUrls_AndAttachesAuthorization()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 201, BodyJson = "{\"success\":true,\"acceptedCount\":1,\"rejectedCount\":0,\"results\":[],\"serverTime\":\"2026-05-14T00:00:00Z\",\"schemaVersion\":1}" });
            var client = CreateClient(transport, new StaticAuthTokenProvider("test-token"));

            var result = RunAsync(() => client.PostActivitySessionsAsync(CreateRequest(), CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("http://localhost:3000/api/v1/sync/activity-sessions", transport.Requests[0].Url);
            Assert.AreEqual("test-token", transport.Requests[0].BearerToken);
            Assert.IsTrue(transport.Requests[0].BodyJson.Contains("\"schemaVersion\":1"));
        }

        [Test]
        public void ReturnsAuthRequired_WhenTokenMissing()
        {
            var transport = new FakeTransport();
            var client = CreateClient(transport, new NullAuthTokenProvider());

            var result = RunAsync(() => client.GetActivitySessionsAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.AuthRequired, result.ErrorCode);
            Assert.AreEqual(0, transport.Requests.Count);
        }

        [Test]
        public void ParsesHealthAndPostResponses()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"status\":\"ok\",\"schemaVersion\":1,\"maxLimits\":{\"sessionsPerRequest\":100,\"bucketsPerGroup\":50,\"warningsPerSession\":25},\"serverTime\":\"2026-05-14T00:00:00Z\"}" });
            transport.Enqueue(new SafeHttpResponse { StatusCode = 201, BodyJson = "{\"success\":true,\"acceptedCount\":2,\"rejectedCount\":0,\"results\":[{\"clientSessionId\":\"a\",\"status\":\"accepted\",\"serverSessionId\":\"11111111-1111-4111-8111-111111111111\"}],\"serverTime\":\"2026-05-14T00:00:00Z\",\"schemaVersion\":1}" });
            var client = CreateClient(transport, new StaticAuthTokenProvider("test-token"));

            var health = RunAsync(() => client.GetHealthAsync(CancellationToken.None));
            var post = RunAsync(() => client.PostActivitySessionsAsync(CreateRequest(), CancellationToken.None));

            Assert.IsTrue(health.IsSuccess);
            Assert.AreEqual(100, health.Value.MaxLimits.SessionsPerRequest);
            Assert.IsTrue(post.IsSuccess);
            Assert.AreEqual(2, post.Value.AcceptedCount);
        }

        [Test]
        public void ParsesPerSessionRejectedResponseSafely()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 200, BodyJson = "{\"success\":false,\"acceptedCount\":1,\"rejectedCount\":1,\"results\":[{\"clientSessionId\":\"safe-1\",\"status\":\"rejected\",\"errorCode\":\"INVALID_BUCKET_VALUE\"}],\"serverTime\":\"2026-05-14T00:00:00Z\",\"schemaVersion\":1}" });
            var client = CreateClient(transport, new StaticAuthTokenProvider("test-token"));

            var result = RunAsync(() => client.PostActivitySessionsAsync(CreateRequest(), CancellationToken.None));

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Value.RejectedCount);
            Assert.AreEqual("INVALID_BUCKET_VALUE", result.Value.Results[0].ErrorCode);
        }

        [TestCase(401, SafeSyncApiError.AuthRequired)]
        [TestCase(403, SafeSyncApiError.AuthRequired)]
        [TestCase(500, SafeSyncApiError.ServerUnavailable)]
        public void HandlesHttpErrorsSafely(int statusCode, string expectedError)
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = statusCode, BodyJson = "{\"errorCode\":\"" + expectedError + "\",\"message\":\"prompt /Users/private\"}" });
            var logger = new InMemorySafeSyncLogger();
            var client = CreateClient(transport, new StaticAuthTokenProvider("test-token"), logger);

            var result = RunAsync(() => client.GetActivitySessionsAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(expectedError, result.ErrorCode);
            AssertLogIsSafe(logger);
        }

        [Test]
        public void HandlesNetworkTimeoutSafely()
        {
            var logger = new InMemorySafeSyncLogger();
            var client = CreateClient(new FakeTransport(new OperationCanceledException()), new StaticAuthTokenProvider("test-token"), logger);

            var result = RunAsync(() => client.GetActivitySessionsAsync(CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(SafeSyncApiError.Timeout, result.ErrorCode);
            AssertLogIsSafe(logger);
        }

        [Test]
        public void DoesNotLogRequestOrResponseBodies()
        {
            var transport = new FakeTransport();
            transport.Enqueue(new SafeHttpResponse { StatusCode = 400, BodyJson = "{\"errorCode\":\"VALIDATION_FAILED\",\"message\":\"prompt: /Users/example/private\"}" });
            var logger = new InMemorySafeSyncLogger();
            var client = CreateClient(transport, new StaticAuthTokenProvider("dev-secret-token"), logger);

            RunAsync(() => client.PostActivitySessionsAsync(CreateRequest(), CancellationToken.None));

            var logText = string.Join("\n", logger.Entries);
            Assert.IsFalse(logText.Contains("prompt"));
            Assert.IsFalse(logText.Contains("/Users"));
            Assert.IsFalse(logText.Contains("dev-secret-token"));
            Assert.IsFalse(logText.Contains(transport.Requests.Single().BodyJson));
        }

        private static SafeSyncApiClient CreateClient(FakeTransport transport, IAuthTokenProvider provider, InMemorySafeSyncLogger logger = null)
        {
            return new SafeSyncApiClient(
                new SafeSyncApiConfig("http://localhost:3000/api/v1"),
                provider,
                transport,
                logger ?? new InMemorySafeSyncLogger());
        }

        private static SafeActivitySessionsContractRequest CreateRequest()
        {
            return new SafeActivitySessionsContractRequest
            {
                SchemaVersion = 1,
                ClientSyncId = "sync-1",
                Sessions = new List<SafeActivitySessionContractDto>
                {
                    new SafeActivitySessionContractDto
                    {
                        ClientSessionId = "session-1",
                        SourceProvider = "GIT",
                        DayBucket = "2026-05-14",
                        Confidence = "HIGH",
                        ChangeCountBucket = "FEW",
                        LineCountBucket = "MANY"
                    }
                }
            };
        }

        private static void AssertLogIsSafe(InMemorySafeSyncLogger logger)
        {
            var logText = string.Join("\n", logger.Entries);
            Assert.IsFalse(logText.Contains("prompt"));
            Assert.IsFalse(logText.Contains("/Users"));
            Assert.IsFalse(logText.Contains("Bearer"));
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
