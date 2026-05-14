using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public interface ISyncService
    {
        Task<Result<SaveData>> PushAsync(CancellationToken cancellationToken);
        Task<Result<SaveData>> PullAsync(CancellationToken cancellationToken);
        Task<Result<SaveData>> PushThenPullAsync(CancellationToken cancellationToken);
    }

    public sealed class SyncService : ISyncService
    {
        private const string PushEndpoint = "/sync/push";
        private const string PullEndpoint = "/sync/pull";

        private readonly SaveDataRepository repository;
        private readonly TokenForgeHttpClient httpClient;
        private readonly SafeSyncMapper mapper;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly JsonSerializerSettings serializerSettings;

        public SyncService(
            SaveDataRepository repository,
            TokenForgeHttpClient httpClient,
            SafeSyncMapper mapper = null,
            PrivacySanitizer privacySanitizer = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.mapper = mapper ?? new SafeSyncMapper(new SyncPayloadSanitizer(this.privacySanitizer));
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            };
        }

        public async Task<Result<SaveData>> PushAsync(CancellationToken cancellationToken)
        {
            var saveData = await repository.LoadAsync(cancellationToken);
            var localValidation = privacySanitizer.ValidateNoForbiddenFields(saveData);
            if (!localValidation.IsSuccess)
            {
                return Result<SaveData>.Failure(localValidation.ErrorCode, "Local save data failed privacy validation.");
            }

            SafeSyncPayload payload;
            try
            {
                payload = mapper.ToPayload(saveData);
            }
            catch (Exception)
            {
                return Result<SaveData>.Failure("privacy_forbidden_data", "Sync payload failed privacy validation.");
            }

            var validation = privacySanitizer.ValidateNoForbiddenFields(payload);
            if (!validation.IsSuccess)
            {
                return Result<SaveData>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var response = await httpClient.PostJsonAsync<SafeSyncPayload, SafeSyncPushResponse>(
                PushEndpoint,
                "sync_push",
                payload,
                cancellationToken);

            if (!response.IsSuccess)
            {
                return Result<SaveData>.Failure(response.ErrorCode, response.ErrorMessage);
            }

            var updated = Clone(saveData);
            updated.SyncState = updated.SyncState ?? new SyncState();
            updated.SyncState.LastSyncAt = DateTimeOffset.UtcNow;
            updated.SyncState.SyncVersion = Math.Max(updated.SyncState.SyncVersion, Math.Max(1, response.Value?.SyncVersion ?? updated.SyncState.SyncVersion));
            updated.SyncState.PendingQueueCount = 0;
            updated.SyncState.LastErrorCode = string.Empty;

            var saveResult = await repository.SaveAsync(updated, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<SaveData>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            return Result<SaveData>.Success(updated);
        }

        public async Task<Result<SaveData>> PullAsync(CancellationToken cancellationToken)
        {
            var saveData = await repository.LoadAsync(cancellationToken);

            SafeSyncPullRequest request;
            try
            {
                request = mapper.ToPullRequest(saveData);
            }
            catch (Exception)
            {
                return Result<SaveData>.Failure("privacy_forbidden_data", "Sync pull request failed privacy validation.");
            }

            var validation = privacySanitizer.ValidateNoForbiddenFields(request);
            if (!validation.IsSuccess)
            {
                return Result<SaveData>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var response = await httpClient.GetAsync<SafeSyncPullResponse>(
                PullEndpoint,
                "sync_pull",
                cancellationToken);

            if (!response.IsSuccess)
            {
                return Result<SaveData>.Failure(response.ErrorCode, response.ErrorMessage);
            }

            if (response.Value == null)
            {
                return Result<SaveData>.Failure("sync_empty_response", "Sync pull response was empty.");
            }

            var inboundValidation = privacySanitizer.ValidateNoForbiddenFields(response.Value);
            if (!inboundValidation.IsSuccess)
            {
                return Result<SaveData>.Failure(inboundValidation.ErrorCode, inboundValidation.ErrorMessage);
            }

            var updated = Clone(saveData);
            try
            {
                mapper.MergePullResponse(updated, response.Value);
            }
            catch (Exception)
            {
                return Result<SaveData>.Failure("privacy_forbidden_data", "Sync pull response failed privacy validation.");
            }

            var saveResult = await repository.SaveAsync(updated, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                return Result<SaveData>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            return Result<SaveData>.Success(updated);
        }

        public async Task<Result<SaveData>> PushThenPullAsync(CancellationToken cancellationToken)
        {
            var pushResult = await PushAsync(cancellationToken);
            if (!pushResult.IsSuccess)
            {
                return pushResult;
            }

            return await PullAsync(cancellationToken);
        }

        private SaveData Clone(SaveData saveData)
        {
            var json = JsonConvert.SerializeObject(saveData, serializerSettings);
            return JsonConvert.DeserializeObject<SaveData>(json, serializerSettings) ?? SaveData.CreateDefault();
        }
    }
}
