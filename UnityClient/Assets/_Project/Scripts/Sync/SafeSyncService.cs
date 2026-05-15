using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public sealed class SafeSyncService : ISafeSyncService
    {
        private readonly ILocalSaveDataRepository repository;
        private readonly ISafeSyncApiClient apiClient;
        private readonly SafeSyncMapper mapper;
        private readonly Func<SaveData, SafeActivitySessionsContractRequest> requestFactory;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly SyncPayloadSanitizer payloadSanitizer;
        private readonly JsonSerializerSettings serializerSettings;
        private int healthInProgress;
        private int syncInProgress;
        private int fetchInProgress;
        private int deleteInProgress;

        public SafeSyncService(
            ILocalSaveDataRepository repository,
            ISafeSyncApiClient apiClient,
            SafeSyncMapper mapper = null,
            PrivacySanitizer privacySanitizer = null,
            Func<SaveData, SafeActivitySessionsContractRequest> requestFactory = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            payloadSanitizer = new SyncPayloadSanitizer(this.privacySanitizer);
            this.mapper = mapper ?? new SafeSyncMapper(payloadSanitizer);
            this.requestFactory = requestFactory;
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }

        public SafeSyncStatus Status { get; private set; } = SafeSyncStatus.Idle;
        public string BaseUrl => apiClient.Config.BaseUrl;

        public void SetBaseUrl(string baseUrl)
        {
            if (!apiClient.Config.TrySetBaseUrl(baseUrl))
            {
                apiClient.Config.BaseUrl = baseUrl ?? string.Empty;
                Status = SafeSyncStatus.Failed;
            }
        }

        public async Task<SafeSyncResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref healthInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(Status, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
            if (!apiClient.Config.HasValidBaseUrl())
            {
                Status = SafeSyncStatus.Failed;
                return SafeSyncResult.Failure(Status, SafeSyncApiError.InvalidBaseUrl, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl));
            }

            Status = SafeSyncStatus.CheckingHealth;
            var response = await apiClient.GetHealthAsync(cancellationToken);
            if (!response.IsSuccess)
            {
                return FailFromApi(response);
            }

            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                SchemaVersion = response.Value?.SchemaVersion ?? 1
            };
            }
            finally
            {
                Interlocked.Exchange(ref healthInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> SyncNowAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref syncInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(Status, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
            if (!apiClient.Config.HasValidBaseUrl())
            {
                Status = SafeSyncStatus.Failed;
                return SafeSyncResult.Failure(Status, SafeSyncApiError.InvalidBaseUrl, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl));
            }

            Status = SafeSyncStatus.Syncing;
            var saveData = await repository.LoadAsync(cancellationToken);
            SafeActivitySessionsContractRequest request;
            try
            {
                request = requestFactory != null
                    ? requestFactory(saveData)
                    : mapper.ToActivitySessionsContractRequest(saveData, BuildClientSyncId());
                var guard = ValidateOutgoingPayload(request);
                if (!guard.IsSuccess)
                {
                    Status = SafeSyncStatus.Failed;
                    return SafeSyncResult.Failure(Status, guard.ErrorCode, guard.ErrorMessage);
                }
            }
            catch (Exception)
            {
                Status = SafeSyncStatus.Failed;
                return SafeSyncResult.Failure(
                    Status,
                    SafeSyncApiError.PrivacyGuardBlockedPayload,
                    "Safe Sync payload failed client privacy validation.");
            }

            var response = await apiClient.PostActivitySessionsAsync(request, cancellationToken);
            if (!response.IsSuccess)
            {
                return FailFromApi(response);
            }

            Status = response.Value != null && response.Value.RejectedCount == 0
                ? SafeSyncStatus.Synced
                : SafeSyncStatus.Failed;

            return new SafeSyncResult
            {
                IsSuccess = response.Value != null && response.Value.RejectedCount == 0,
                Status = Status,
                AcceptedCount = response.Value?.AcceptedCount ?? 0,
                RejectedCount = response.Value?.RejectedCount ?? 0,
                ErrorCode = FirstRejectedCode(response.Value),
                SchemaVersion = response.Value?.SchemaVersion ?? 1
            };
            }
            finally
            {
                Interlocked.Exchange(ref syncInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> FetchRemoteSessionsAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref fetchInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(Status, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
            if (!apiClient.Config.HasValidBaseUrl())
            {
                Status = SafeSyncStatus.Failed;
                return SafeSyncResult.Failure(Status, SafeSyncApiError.InvalidBaseUrl, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl));
            }

            Status = SafeSyncStatus.Fetching;
            var response = await apiClient.GetActivitySessionsAsync(cancellationToken);
            if (!response.IsSuccess)
            {
                return FailFromApi(response);
            }

            var summaries = (response.Value?.Sessions ?? new List<RemoteSafeActivitySessionDto>())
                .Select(ToSummary)
                .Where(summary => privacySanitizer.ValidateNoForbiddenFields(summary).IsSuccess)
                .ToList();

            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                RemoteSessions = summaries,
                AcceptedCount = summaries.Count,
                SchemaVersion = response.Value?.SchemaVersion ?? 1
            };
            }
            finally
            {
                Interlocked.Exchange(ref fetchInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> DeleteRemoteSessionAsync(string serverSessionId, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref deleteInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(Status, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
            if (!apiClient.Config.HasValidBaseUrl())
            {
                Status = SafeSyncStatus.Failed;
                return SafeSyncResult.Failure(Status, SafeSyncApiError.InvalidBaseUrl, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl));
            }

            Status = SafeSyncStatus.DeleteInProgress;
            var response = await apiClient.DeleteActivitySessionAsync(serverSessionId, cancellationToken);
            if (!response.IsSuccess)
            {
                return FailFromApi(response);
            }

            Status = SafeSyncStatus.Ready;
            return SafeSyncResult.Success(Status);
            }
            finally
            {
                Interlocked.Exchange(ref deleteInProgress, 0);
            }
        }

        private TokenForge.Client.Common.Result ValidateOutgoingPayload(SafeActivitySessionsContractRequest request)
        {
            var objectValidation = payloadSanitizer.ValidatePayload(request);
            if (!objectValidation.IsSafe)
            {
                return TokenForge.Client.Common.Result.Failure(
                    SafeSyncApiError.PrivacyGuardBlockedPayload,
                    "Safe Sync payload failed client privacy validation.");
            }

            var json = JsonConvert.SerializeObject(request, serializerSettings);
            var jsonValidation = privacySanitizer.ValidateNoForbiddenFields(json);
            if (!jsonValidation.IsSuccess)
            {
                return TokenForge.Client.Common.Result.Failure(
                    SafeSyncApiError.PrivacyGuardBlockedPayload,
                    "Safe Sync payload failed client privacy validation.");
            }

            return TokenForge.Client.Common.Result.Success();
        }

        private SafeSyncResult FailFromApi<T>(SafeSyncApiResult<T> response)
        {
            Status = ToStatus(response.ErrorCode);
            return SafeSyncResult.Failure(Status, response.ErrorCode, response.ErrorMessage);
        }

        private static SafeSyncStatus ToStatus(string errorCode)
        {
            if (string.Equals(errorCode, SafeSyncApiError.AuthRequired, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, "HTTP_401", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, "HTTP_403", StringComparison.OrdinalIgnoreCase))
            {
                return SafeSyncStatus.AuthRequired;
            }

            if (string.Equals(errorCode, SafeSyncApiError.ServerUnavailable, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, SafeSyncApiError.NetworkError, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, SafeSyncApiError.Timeout, StringComparison.OrdinalIgnoreCase))
            {
                return SafeSyncStatus.ServerUnavailable;
            }

            return SafeSyncStatus.Failed;
        }

        private static string FirstRejectedCode(SafeActivitySessionsUpsertResponse response)
        {
            return (response?.Results ?? new List<SafeActivitySessionUpsertResult>())
                .FirstOrDefault(result => string.Equals(result.Status, "rejected", StringComparison.OrdinalIgnoreCase))
                ?.ErrorCode ?? string.Empty;
        }

        private static string BuildClientSyncId()
        {
            return "unity-sync-" + Guid.NewGuid().ToString("N");
        }

        private static RemoteSafeSessionSummary ToSummary(RemoteSafeActivitySessionDto session)
        {
            session = session ?? new RemoteSafeActivitySessionDto();
            return new RemoteSafeSessionSummary
            {
                ServerSessionId = session.Id ?? string.Empty,
                ClientSessionId = session.ClientSessionId ?? string.Empty,
                SourceProvider = session.SourceProvider ?? "UNKNOWN_AGENT",
                DayBucket = session.DayBucket ?? string.Empty,
                TimeBucket = session.TimeBucket ?? string.Empty,
                Confidence = session.Confidence ?? "LOW",
                ActivityCategory = session.ActivityCategory ?? string.Empty,
                ChangeCountBucket = session.ChangeCountBucket ?? string.Empty,
                LineCountBucket = session.LineCountBucket ?? string.Empty,
                SessionCountBucket = session.SessionCountBucket ?? string.Empty,
                InteractionCountBucket = session.InteractionCountBucket ?? string.Empty,
                WarningCount = session.WarningIds?.Count ?? 0,
                SchemaVersion = session.AggregateSchemaVersion <= 0 ? 1 : session.AggregateSchemaVersion
            };
        }
    }
}
