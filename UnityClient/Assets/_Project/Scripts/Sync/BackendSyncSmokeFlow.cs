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
    public sealed class BackendSyncSmokeFlow
    {
        private const string PushEndpoint = "/sync/push";
        private const string PullEndpoint = "/sync/pull";

        private readonly SaveDataRepository repository;
        private readonly IAuthTokenProvider authTokenProvider;
        private readonly TokenForgeHttpClient httpClient;
        private readonly SafeSyncMapper mapper;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly ISafeSyncLogger logger;
        private readonly JsonSerializerSettings serializerSettings;

        public BackendSyncSmokeFlow(
            SaveDataRepository repository,
            IAuthTokenProvider authTokenProvider,
            TokenForgeHttpClient httpClient,
            SafeSyncMapper mapper = null,
            PrivacySanitizer privacySanitizer = null,
            ISafeSyncLogger logger = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.authTokenProvider = authTokenProvider ?? throw new ArgumentNullException(nameof(authTokenProvider));
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.mapper = mapper ?? new SafeSyncMapper(new SyncPayloadSanitizer(this.privacySanitizer));
            this.logger = logger ?? new UnitySafeSyncLogger();
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            };
        }

        public async Task<Result<SaveData>> RunAsync(CancellationToken cancellationToken = default)
        {
            logger.Info("SMOKE", "backend_smoke_started", 0);

            var bearerToken = await authTokenProvider.GetBearerTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(bearerToken))
            {
                logger.Warning("SMOKE", "backend_smoke_auth", 0, "auth_failed");
                return Result<SaveData>.Failure("sync_auth_failed", "Guest auth failed.");
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            var localValidation = privacySanitizer.ValidateNoForbiddenFields(saveData);
            if (!localValidation.IsSuccess)
            {
                logger.Warning("SMOKE", "backend_smoke_payload", 0, "privacy_failed");
                return Result<SaveData>.Failure(localValidation.ErrorCode, "Local save data failed privacy validation.");
            }

            SafeSyncPayload payload;
            try
            {
                payload = mapper.ToPayload(saveData);
            }
            catch
            {
                logger.Warning("SMOKE", "backend_smoke_payload", 0, "payload_failed");
                return Result<SaveData>.Failure("privacy_forbidden_data", "Sync payload failed privacy validation.");
            }

            var outboundValidation = privacySanitizer.ValidateNoForbiddenFields(payload);
            if (!outboundValidation.IsSuccess)
            {
                logger.Warning("SMOKE", "backend_smoke_payload", 0, "privacy_failed");
                return Result<SaveData>.Failure(outboundValidation.ErrorCode, outboundValidation.ErrorMessage);
            }

            var pushResponse = await httpClient.PostJsonAsync<SafeSyncPayload, SafeSyncPushResponse>(
                PushEndpoint,
                "sync_push",
                payload,
                cancellationToken);
            if (!pushResponse.IsSuccess)
            {
                logger.Warning("SMOKE", "backend_smoke_push", pushResponse.StatusCode, "push_failed");
                return Result<SaveData>.Failure(pushResponse.ErrorCode, pushResponse.ErrorMessage);
            }

            if (pushResponse.Value == null)
            {
                logger.Warning("SMOKE", "backend_smoke_push", pushResponse.StatusCode, "malformed_response");
                return Result<SaveData>.Failure("sync_malformed_response", "Sync push response was empty.");
            }

            logger.Info("SMOKE", "backend_smoke_push", pushResponse.StatusCode, "accepted_sessions_" + SafeCount(pushResponse.Value.AcceptedSessionCount));
            logger.Info("SMOKE", "backend_smoke_push_achievements", pushResponse.StatusCode, "accepted_achievements_" + SafeCount(pushResponse.Value.AcceptedAchievementCount));

            var pullResponse = await httpClient.GetAsync<SafeSyncPullResponse>(
                PullEndpoint,
                "sync_pull",
                cancellationToken);
            if (!pullResponse.IsSuccess)
            {
                logger.Warning("SMOKE", "backend_smoke_pull", pullResponse.StatusCode, "pull_failed");
                return Result<SaveData>.Failure(pullResponse.ErrorCode, pullResponse.ErrorMessage);
            }

            if (pullResponse.Value == null)
            {
                logger.Warning("SMOKE", "backend_smoke_pull", pullResponse.StatusCode, "malformed_response");
                return Result<SaveData>.Failure("sync_malformed_response", "Sync pull response was empty.");
            }

            var inboundValidation = privacySanitizer.ValidateNoForbiddenFields(pullResponse.Value);
            if (!inboundValidation.IsSuccess)
            {
                logger.Warning("SMOKE", "backend_smoke_pull", pullResponse.StatusCode, "privacy_failed");
                return Result<SaveData>.Failure(inboundValidation.ErrorCode, inboundValidation.ErrorMessage);
            }

            logger.Info("SMOKE", "backend_smoke_pull", pullResponse.StatusCode, "pulled_sessions_" + SafeCount(pullResponse.Value.Sessions?.Count ?? 0));
            logger.Info("SMOKE", "backend_smoke_pull_achievements", pullResponse.StatusCode, "pulled_achievements_" + SafeCount(pullResponse.Value.Achievements?.Count ?? 0));

            var updated = Clone(saveData);
            try
            {
                mapper.MergePullResponse(updated, pullResponse.Value);
            }
            catch
            {
                logger.Warning("SMOKE", "backend_smoke_merge", pullResponse.StatusCode, "merge_failed");
                return Result<SaveData>.Failure("sync_merge_failed", "Sync pull response could not be merged.");
            }

            var saveResult = await repository.SaveAsync(updated, cancellationToken);
            if (!saveResult.IsSuccess)
            {
                logger.Warning("SMOKE", "backend_smoke_save", 0, "merge_failed");
                return Result<SaveData>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
            }

            logger.Info("SMOKE", "backend_smoke_merge", pullResponse.StatusCode, "merge_completed");
            return Result<SaveData>.Success(updated);
        }

        private SaveData Clone(SaveData saveData)
        {
            var json = JsonConvert.SerializeObject(saveData, serializerSettings);
            return JsonConvert.DeserializeObject<SaveData>(json, serializerSettings) ?? SaveData.CreateDefault();
        }

        private static int SafeCount(int count)
        {
            return Math.Max(0, Math.Min(1000000, count));
        }
    }
}
