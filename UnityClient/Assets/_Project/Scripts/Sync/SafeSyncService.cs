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
        private readonly ISafeSyncRetryQueueRepository retryQueueRepository;
        private readonly SafeSyncLocalStateRepository localStateRepository;
        private readonly SafeSyncConflictRepository conflictRepository;
        private readonly SafeSyncTombstoneRepository tombstoneRepository;
        private readonly SafeConflictAuditRepository conflictAuditRepository;
        private readonly SafeSyncRetryPolicy retryPolicy;
        private readonly RemoteSafeSessionValidator remoteValidator;
        private readonly SafeRemoteSessionApplicator remoteApplicator;
        private readonly SafeSessionSummaryComparer summaryComparer;
        private readonly SafeConflictMergePolicyEvaluator mergePolicyEvaluator;
        private readonly Func<SaveData, SafeActivitySessionsContractRequest> requestFactory;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly SyncPayloadSanitizer payloadSanitizer;
        private readonly JsonSerializerSettings serializerSettings;
        private int healthInProgress;
        private int syncInProgress;
        private int fetchInProgress;
        private int deleteInProgress;
        private int retryInProgress;
        private int conflictInProgress;

        public SafeSyncService(
            ILocalSaveDataRepository repository,
            ISafeSyncApiClient apiClient,
            SafeSyncMapper mapper = null,
            PrivacySanitizer privacySanitizer = null,
            Func<SaveData, SafeActivitySessionsContractRequest> requestFactory = null,
            ISafeSyncRetryQueueRepository retryQueueRepository = null,
            SafeSyncRetryPolicy retryPolicy = null,
            SafeSyncConflictRepository conflictRepository = null,
            SafeSyncTombstoneRepository tombstoneRepository = null,
            SafeSyncLocalStateRepository localStateRepository = null,
            SafeConflictAuditRepository conflictAuditRepository = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            payloadSanitizer = new SyncPayloadSanitizer(this.privacySanitizer);
            this.mapper = mapper ?? new SafeSyncMapper(payloadSanitizer);
            this.requestFactory = requestFactory;
            this.retryQueueRepository = retryQueueRepository ?? new SafeSyncRetryQueueRepository(null, this.privacySanitizer);
            this.retryPolicy = retryPolicy ?? new SafeSyncRetryPolicy();
            this.conflictRepository = conflictRepository ?? new SafeSyncConflictRepository(null, this.privacySanitizer);
            this.tombstoneRepository = tombstoneRepository ?? new SafeSyncTombstoneRepository(null, this.privacySanitizer);
            this.localStateRepository = localStateRepository ?? new SafeSyncLocalStateRepository(null, this.privacySanitizer);
            this.conflictAuditRepository = conflictAuditRepository ?? new SafeConflictAuditRepository(null, this.privacySanitizer);
            remoteValidator = new RemoteSafeSessionValidator(this.privacySanitizer);
            remoteApplicator = new SafeRemoteSessionApplicator(this.repository, remoteValidator, new SafeSyncRemoteToLocalMapper(), this.privacySanitizer);
            summaryComparer = new SafeSessionSummaryComparer();
            mergePolicyEvaluator = new SafeConflictMergePolicyEvaluator(this.privacySanitizer, remoteValidator);
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

            await RecordSessionMappingsAsync(response.Value?.Results, cancellationToken);

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

        public async Task<SafeSyncResult> EnqueueSyncSafeSessionsAsync(CancellationToken cancellationToken = default)
        {
            var result = await SyncNowAsync(cancellationToken);
            if (result.IsSuccess)
            {
                result.RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken);
                return result;
            }

            var classification = retryPolicy.Classify(result.ErrorCode);
            if (classification == SafeSyncRetryClassification.Retryable)
            {
                var saveData = await repository.LoadAsync(cancellationToken);
                var sessionIds = SafeClientSessionIds(saveData);
                await AddOrUpdateRetryEntryAsync(
                    SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                    sessionIds,
                    string.Empty,
                    result.ErrorCode,
                    SafeSyncRetryQueueEntryStatus.Pending,
                    cancellationToken);

                Status = SafeSyncStatus.RetryPending;
                return new SafeSyncResult
                {
                    IsSuccess = false,
                    Status = Status,
                    ErrorCode = SafeSyncApiError.RetryQueued,
                    ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.RetryQueued),
                    RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken)
                };
            }

            if (classification == SafeSyncRetryClassification.AuthRequired)
            {
                Status = SafeSyncStatus.AuthRequired;
            }
            else if (string.Equals(result.ErrorCode, SafeSyncApiError.PrivacyGuardBlockedPayload, StringComparison.OrdinalIgnoreCase))
            {
                Status = SafeSyncStatus.PrivacyBlocked;
            }
            else if (classification == SafeSyncRetryClassification.Conflict)
            {
                Status = SafeSyncStatus.ConflictDetected;
            }

            result.Status = Status;
            result.RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken);
            return result;
        }

        public async Task<SafeSyncResult> ProcessRetryQueueOnceAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref retryInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(SafeSyncStatus.RetryInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
                var now = DateTimeOffset.UtcNow;
                var state = await retryQueueRepository.LoadAsync(cancellationToken);
                var eligible = (state.Entries ?? new List<SafeSyncRetryQueueEntry>())
                    .Where(entry => entry.IsEligible(now))
                    .OrderBy(entry => entry.NextAttemptAt)
                    .ThenBy(entry => entry.CreatedAt)
                    .ToList();
                var skippedNotReady = (state.Entries ?? new List<SafeSyncRetryQueueEntry>())
                    .Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Pending && !entry.IsEligible(now));

                if (eligible.Count == 0)
                {
                    Status = SafeSyncStatus.RetryWaiting;
                    return new SafeSyncResult
                    {
                        IsSuccess = true,
                        Status = Status,
                        ErrorCode = skippedNotReady > 0 ? SafeSyncApiError.RetrySkippedNotReady : SafeSyncApiError.RetryWaiting,
                        SkippedNotReadyCount = skippedNotReady,
                        RetryQueueSummary = ToRetrySummary(state)
                    };
                }

                Status = SafeSyncStatus.RetryInProgress;
                var anyFailure = false;
                var accepted = 0;
                var succeeded = 0;
                var failed = 0;
                var paused = 0;
                var conflicts = 0;
                foreach (var entry in eligible)
                {
                    entry.Status = SafeSyncRetryQueueEntryStatus.InProgress;
                    entry.AttemptCount += 1;
                    entry.UpdatedAt = now;
                    await retryQueueRepository.SaveAsync(state, cancellationToken);

                    var entryResult = entry.OperationType == SafeSyncRetryOperationType.DELETE_REMOTE_SESSION
                        ? await ProcessDeleteRetryEntryAsync(entry, cancellationToken)
                        : await ProcessUpsertRetryEntryAsync(entry, cancellationToken);

                    var completedAt = DateTimeOffset.UtcNow;
                    if (entryResult.IsSuccess)
                    {
                        entry.Status = SafeSyncRetryQueueEntryStatus.Succeeded;
                        entry.LastSafeErrorCode = string.Empty;
                        entry.UpdatedAt = completedAt;
                        accepted += Math.Max(1, entryResult.AcceptedCount);
                        succeeded += 1;
                    }
                    else
                    {
                        anyFailure = true;
                        ApplyRetryFailure(entry, entryResult.ErrorCode, completedAt);
                        if (entry.Status == SafeSyncRetryQueueEntryStatus.Paused)
                        {
                            paused += 1;
                        }
                        else if (entry.Status == SafeSyncRetryQueueEntryStatus.Conflict)
                        {
                            conflicts += 1;
                        }
                        else
                        {
                            failed += 1;
                        }
                    }

                    await retryQueueRepository.SaveAsync(state, cancellationToken);
                }

                Status = anyFailure ? SafeSyncStatus.RetryFailed : SafeSyncStatus.RetrySucceeded;
                return new SafeSyncResult
                {
                    IsSuccess = !anyFailure,
                    Status = Status,
                    ErrorCode = SafeSyncApiError.BatchRetryProcessComplete,
                    AcceptedCount = accepted,
                    AttemptedCount = eligible.Count,
                    SucceededCount = succeeded,
                    FailedCount = failed,
                    PausedCount = paused,
                    SkippedNotReadyCount = skippedNotReady,
                    ConflictDetectedCount = conflicts,
                    RetryQueueSummary = ToRetrySummary(state),
                    ConflictSummary = await GetConflictSummaryAsync(cancellationToken),
                    TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
                };
            }
            finally
            {
                Interlocked.Exchange(ref retryInProgress, 0);
            }
        }

        public Task<SafeSyncResult> ProcessAllEligibleRetryEntriesOnceAsync(CancellationToken cancellationToken = default)
        {
            return ProcessRetryQueueOnceAsync(cancellationToken);
        }

        public async Task<SafeSyncResult> ForceRetryEntryAsync(string queueEntryId, bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            if (!explicitConfirmation)
            {
                return SafeSyncResult.Failure(SafeSyncStatus.RetryWaiting, SafeSyncApiError.MergePolicyConfirmationRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.MergePolicyConfirmationRequired));
            }

            if (Interlocked.Exchange(ref retryInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(SafeSyncStatus.RetryInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
                var state = await retryQueueRepository.LoadAsync(cancellationToken);
                var entry = (state.Entries ?? new List<SafeSyncRetryQueueEntry>())
                    .FirstOrDefault(item => string.Equals(item.QueueEntryId, queueEntryId, StringComparison.Ordinal));
                if (entry == null || entry.Status == SafeSyncRetryQueueEntryStatus.Cancelled || entry.Status == SafeSyncRetryQueueEntryStatus.Succeeded)
                {
                    Status = SafeSyncStatus.RetryWaiting;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.RetryWaiting,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.RetryWaiting),
                        RetryQueueSummary = ToRetrySummary(state)
                    };
                }

                if (entry.Status == SafeSyncRetryQueueEntryStatus.Paused ||
                    retryPolicy.Classify(entry.LastSafeErrorCode) == SafeSyncRetryClassification.AuthRequired)
                {
                    Status = SafeSyncStatus.AuthRequired;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.ForcedRetrySkippedAuthPaused,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ForcedRetrySkippedAuthPaused),
                        RetryQueueSummary = ToRetrySummary(state)
                    };
                }

                if (IsUnsafeRetryBlock(entry.LastSafeErrorCode))
                {
                    Status = SafeSyncStatus.PrivacyBlocked;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.ForcedRetryBlockedUnsafe,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ForcedRetryBlockedUnsafe),
                        RetryQueueSummary = ToRetrySummary(state)
                    };
                }

                if (entry.Status == SafeSyncRetryQueueEntryStatus.Failed &&
                    retryPolicy.Classify(entry.LastSafeErrorCode) == SafeSyncRetryClassification.NonRetryable)
                {
                    Status = SafeSyncStatus.RetryFailed;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.ForcedRetryBlockedPermanentFailure,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ForcedRetryBlockedPermanentFailure),
                        RetryQueueSummary = ToRetrySummary(state)
                    };
                }

                var now = DateTimeOffset.UtcNow;
                Status = SafeSyncStatus.RetryInProgress;
                entry.Status = SafeSyncRetryQueueEntryStatus.InProgress;
                entry.AttemptCount += 1;
                entry.UpdatedAt = now;
                entry.NextAttemptAt = now;
                entry.LastSafeErrorCode = SafeSyncApiError.ForcedRetryStarted;
                await retryQueueRepository.SaveAsync(state, cancellationToken);

                var entryResult = entry.OperationType == SafeSyncRetryOperationType.DELETE_REMOTE_SESSION
                    ? await ProcessDeleteRetryEntryAsync(entry, cancellationToken)
                    : await ProcessUpsertRetryEntryAsync(entry, cancellationToken);

                var completedAt = DateTimeOffset.UtcNow;
                if (entryResult.IsSuccess)
                {
                    entry.Status = SafeSyncRetryQueueEntryStatus.Succeeded;
                    entry.LastSafeErrorCode = string.Empty;
                    entry.UpdatedAt = completedAt;
                    await retryQueueRepository.SaveAsync(state, cancellationToken);
                    Status = SafeSyncStatus.RetrySucceeded;
                    return new SafeSyncResult
                    {
                        IsSuccess = true,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.ForcedRetrySucceeded,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ForcedRetrySucceeded),
                        AttemptedCount = 1,
                        SucceededCount = 1,
                        AcceptedCount = Math.Max(1, entryResult.AcceptedCount),
                        RetryQueueSummary = ToRetrySummary(state),
                        ConflictSummary = await GetConflictSummaryAsync(cancellationToken),
                        TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
                    };
                }

                ApplyRetryFailure(entry, entryResult.ErrorCode, completedAt);
                await retryQueueRepository.SaveAsync(state, cancellationToken);
                Status = entry.Status == SafeSyncRetryQueueEntryStatus.Paused ? SafeSyncStatus.AuthRequired : SafeSyncStatus.RetryPending;
                return new SafeSyncResult
                {
                    IsSuccess = false,
                    Status = Status,
                    ErrorCode = retryPolicy.Classify(entry.LastSafeErrorCode) == SafeSyncRetryClassification.Retryable
                        ? SafeSyncApiError.ForcedRetryFailedRetryable
                        : entry.LastSafeErrorCode,
                    ErrorMessage = SafeSyncApiError.ToSafeMessage(retryPolicy.Classify(entry.LastSafeErrorCode) == SafeSyncRetryClassification.Retryable
                        ? SafeSyncApiError.ForcedRetryFailedRetryable
                        : entry.LastSafeErrorCode),
                    AttemptedCount = 1,
                    FailedCount = entry.Status == SafeSyncRetryQueueEntryStatus.Pending || entry.Status == SafeSyncRetryQueueEntryStatus.Failed ? 1 : 0,
                    PausedCount = entry.Status == SafeSyncRetryQueueEntryStatus.Paused ? 1 : 0,
                    ConflictDetectedCount = entry.Status == SafeSyncRetryQueueEntryStatus.Conflict ? 1 : 0,
                    RetryQueueSummary = ToRetrySummary(state),
                    ConflictSummary = await GetConflictSummaryAsync(cancellationToken),
                    TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
                };
            }
            finally
            {
                Interlocked.Exchange(ref retryInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> CancelRetryEntryAsync(string queueEntryId, CancellationToken cancellationToken = default)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            var entry = (state.Entries ?? new List<SafeSyncRetryQueueEntry>())
                .FirstOrDefault(item => string.Equals(item.QueueEntryId, queueEntryId, StringComparison.Ordinal));
            if (entry != null && entry.Status != SafeSyncRetryQueueEntryStatus.Succeeded)
            {
                entry.Status = SafeSyncRetryQueueEntryStatus.Cancelled;
                entry.UpdatedAt = DateTimeOffset.UtcNow;
                await retryQueueRepository.SaveAsync(state, cancellationToken);
            }

            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Ready,
                RetryQueueSummary = ToRetrySummary(state)
            };
        }

        public async Task<SafeSyncResult> CancelAllFailedRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            foreach (var entry in state.Entries ?? new List<SafeSyncRetryQueueEntry>())
            {
                if (entry.Status == SafeSyncRetryQueueEntryStatus.Failed)
                {
                    entry.Status = SafeSyncRetryQueueEntryStatus.Cancelled;
                    entry.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            await retryQueueRepository.SaveAsync(state, cancellationToken);
            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                RetryQueueSummary = ToRetrySummary(state)
            };
        }

        public async Task<SafeSyncResult> ClearSucceededRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            state.Entries = (state.Entries ?? new List<SafeSyncRetryQueueEntry>())
                .Where(entry => entry.Status != SafeSyncRetryQueueEntryStatus.Succeeded)
                .ToList();
            await retryQueueRepository.SaveAsync(state, cancellationToken);
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Ready,
                RetryQueueSummary = ToRetrySummary(state)
            };
        }

        public async Task<SafeSyncResult> PauseAllPendingRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            foreach (var entry in state.Entries ?? new List<SafeSyncRetryQueueEntry>())
            {
                if (entry.Status == SafeSyncRetryQueueEntryStatus.Pending)
                {
                    entry.Status = SafeSyncRetryQueueEntryStatus.Paused;
                    entry.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            await retryQueueRepository.SaveAsync(state, cancellationToken);
            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                RetryQueueSummary = ToRetrySummary(state)
            };
        }

        public async Task<SafeSyncResult> ResumeAllPausedRetryEntriesAsync(CancellationToken cancellationToken = default)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            foreach (var entry in state.Entries ?? new List<SafeSyncRetryQueueEntry>())
            {
                if (entry.Status == SafeSyncRetryQueueEntryStatus.Paused)
                {
                    entry.Status = SafeSyncRetryQueueEntryStatus.Pending;
                    entry.NextAttemptAt = DateTimeOffset.UtcNow;
                    entry.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            await retryQueueRepository.SaveAsync(state, cancellationToken);
            Status = SafeSyncStatus.RetryPending;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                RetryQueueSummary = ToRetrySummary(state)
            };
        }

        public async Task<SafeSyncRetryQueueSummary> GetRetryQueueSummaryAsync(CancellationToken cancellationToken = default)
        {
            return ToRetrySummary(await retryQueueRepository.LoadAsync(cancellationToken));
        }

        public async Task<SafeSyncConflictSummary> GetConflictSummaryAsync(CancellationToken cancellationToken = default)
        {
            var state = await conflictRepository.LoadAsync(cancellationToken);
            var conflicts = (state.Conflicts ?? new List<SafeSyncConflict>())
                .Where(conflict => privacySanitizer.ValidateNoForbiddenFields(conflict).IsSuccess)
                .OrderByDescending(conflict => conflict.DetectedAt)
                .Take(20)
                .ToList();
            return new SafeSyncConflictSummary
            {
                UnresolvedCount = conflicts.Count(conflict => conflict.ResolutionStatus == SafeSyncConflictResolutionStatus.Unresolved),
                SafeConflicts = conflicts
            };
        }

        public async Task<SafeSyncTombstoneSummary> GetTombstoneSummaryAsync(CancellationToken cancellationToken = default)
        {
            var state = await tombstoneRepository.LoadAsync(cancellationToken);
            var tombstones = (state.Tombstones ?? new List<SafeSyncTombstone>())
                .Where(tombstone => privacySanitizer.ValidateNoForbiddenFields(tombstone).IsSuccess)
                .OrderByDescending(tombstone => tombstone.DeletedAt)
                .Take(20)
                .ToList();
            return new SafeSyncTombstoneSummary
            {
                PendingDeleteCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.PendingDelete),
                DeleteSyncedCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteSynced || tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteAlreadyApplied),
                DeleteFailedCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteFailed),
                DeleteResolvedCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteResolved || tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteCancelled),
                SafeTombstones = tombstones
            };
        }

        public async Task<SafeSyncResult> DeleteLocalSavedSessionAsync(string clientSessionId, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref deleteInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(Status, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
                if (string.IsNullOrWhiteSpace(clientSessionId))
                {
                    Status = SafeSyncStatus.Failed;
                    return SafeSyncResult.Failure(Status, SafeSyncApiError.LocalSessionMissing, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.LocalSessionMissing));
                }

                var saveData = await repository.LoadAsync(cancellationToken) ?? SaveData.CreateDefault();
                saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new List<AgentWorkSession>();
                var removed = saveData.WorkSessionSummaries.RemoveAll(session => string.Equals(session.SessionId, clientSessionId, StringComparison.Ordinal));
                if (removed == 0)
                {
                    Status = SafeSyncStatus.Failed;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.LocalSessionMissing,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.LocalSessionMissing),
                        RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken),
                        TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
                    };
                }

                var mapping = await FindMappingAsync(clientSessionId, cancellationToken);
                var saveResult = await repository.SaveAsync(saveData, cancellationToken);
                if (!saveResult.IsSuccess)
                {
                    Status = SafeSyncStatus.Failed;
                    return SafeSyncResult.Failure(Status, SafeSyncApiError.LocalSessionDeleteFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.LocalSessionDeleteFailed));
                }

                await RemoveLocalMappingAsync(clientSessionId, cancellationToken);
                var retryUpdated = await RemoveClientSessionFromPendingUpsertsAsync(clientSessionId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(mapping?.ServerSessionId))
                {
                    await RecordTombstoneAsync(clientSessionId, mapping.ServerSessionId, SafeSyncTombstoneDeleteSource.LocalUser, SafeSyncTombstoneStatus.PendingDelete, SafeSyncApiError.TombstonePending, cancellationToken);
                }

                Status = SafeSyncStatus.Ready;
                return new SafeSyncResult
                {
                    IsSuccess = true,
                    Status = Status,
                    ErrorCode = !string.IsNullOrWhiteSpace(mapping?.ServerSessionId)
                        ? SafeSyncApiError.TombstoneCreated
                        : (retryUpdated ? SafeSyncApiError.UpsertRetryUpdatedAfterLocalDelete : SafeSyncApiError.LocalSessionDeleted),
                    ErrorMessage = !string.IsNullOrWhiteSpace(mapping?.ServerSessionId)
                        ? SafeSyncApiError.ToSafeMessage(SafeSyncApiError.TombstoneCreated)
                        : SafeSyncApiError.ToSafeMessage(retryUpdated ? SafeSyncApiError.UpsertRetryUpdatedAfterLocalDelete : SafeSyncApiError.LocalSessionDeleted),
                    RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken),
                    TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
                };
            }
            finally
            {
                Interlocked.Exchange(ref deleteInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> EnqueuePendingTombstoneDeletesAsync(CancellationToken cancellationToken = default)
        {
            var state = await tombstoneRepository.LoadAsync(cancellationToken);
            var pending = (state.Tombstones ?? new List<SafeSyncTombstone>())
                .Where(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.PendingDelete && !string.IsNullOrWhiteSpace(tombstone.ServerSessionId))
                .ToList();

            foreach (var tombstone in pending)
            {
                await AddOrUpdateRetryEntryAsync(
                    SafeSyncRetryOperationType.DELETE_REMOTE_SESSION,
                    new List<string>(),
                    tombstone.ServerSessionId,
                    SafeSyncApiError.TombstonePending,
                    SafeSyncRetryQueueEntryStatus.Pending,
                    cancellationToken);
            }

            Status = SafeSyncStatus.RetryPending;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                ErrorCode = SafeSyncApiError.TombstoneDeletePending,
                ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.TombstoneDeletePending),
                RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken),
                TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
            };
        }

        public async Task<SafeSyncResult> ProcessPendingTombstoneDeletesOnceAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref retryInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(SafeSyncStatus.RetryInProgress, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
                if (!apiClient.Config.HasValidBaseUrl())
                {
                    Status = SafeSyncStatus.Failed;
                    return SafeSyncResult.Failure(Status, SafeSyncApiError.InvalidBaseUrl, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl));
                }

                var state = await tombstoneRepository.LoadAsync(cancellationToken);
                var pending = (state.Tombstones ?? new List<SafeSyncTombstone>())
                    .Where(item => item.SyncStatus == SafeSyncTombstoneStatus.PendingDelete && !string.IsNullOrWhiteSpace(item.ServerSessionId))
                    .OrderBy(item => item.DeletedAt)
                    .Take(25)
                    .ToList();

                if (pending.Count == 0)
                {
                    Status = SafeSyncStatus.RetryWaiting;
                    return new SafeSyncResult
                    {
                        IsSuccess = true,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.RetryWaiting,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.RetryWaiting),
                        TombstoneSummary = ToTombstoneSummary(state)
                    };
                }

                Status = SafeSyncStatus.DeleteInProgress;
                var attempted = 0;
                var succeeded = 0;
                var failed = 0;
                var paused = 0;
                foreach (var tombstone in pending)
                {
                    attempted += 1;
                    var response = await apiClient.DeleteActivitySessionAsync(tombstone.ServerSessionId, cancellationToken);
                    if (response.IsSuccess)
                    {
                        tombstone.SyncStatus = SafeSyncTombstoneStatus.DeleteSynced;
                        tombstone.LastSafeErrorCode = string.Empty;
                        succeeded += 1;
                        await tombstoneRepository.SaveAsync(state, cancellationToken);
                        continue;
                    }

                    if (IsNotFound(response.ErrorCode))
                    {
                        tombstone.SyncStatus = SafeSyncTombstoneStatus.DeleteAlreadyApplied;
                        tombstone.DeleteSource = SafeSyncTombstoneDeleteSource.ServerNotFound;
                        tombstone.LastSafeErrorCode = SafeSyncApiError.DeleteAlreadyApplied;
                        succeeded += 1;
                        await tombstoneRepository.SaveAsync(state, cancellationToken);
                        continue;
                    }

                    var classification = retryPolicy.Classify(response.ErrorCode);
                    tombstone.LastSafeErrorCode = response.ErrorCode ?? string.Empty;
                    if (classification == SafeSyncRetryClassification.AuthRequired)
                    {
                        paused += pending.Count - attempted + 1;
                        await tombstoneRepository.SaveAsync(state, cancellationToken);
                        Status = SafeSyncStatus.AuthRequired;
                        return new SafeSyncResult
                        {
                            IsSuccess = false,
                            Status = Status,
                            ErrorCode = SafeSyncApiError.AuthRequired,
                            ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AuthRequired),
                            AttemptedCount = attempted,
                            SucceededCount = succeeded,
                            FailedCount = failed,
                            PausedCount = paused,
                            TombstoneSummary = ToTombstoneSummary(state)
                        };
                    }

                    if (classification == SafeSyncRetryClassification.Retryable)
                    {
                        failed += 1;
                        await tombstoneRepository.SaveAsync(state, cancellationToken);
                        await AddOrUpdateRetryEntryAsync(SafeSyncRetryOperationType.DELETE_REMOTE_SESSION, new List<string>(), tombstone.ServerSessionId, response.ErrorCode, SafeSyncRetryQueueEntryStatus.Pending, cancellationToken);
                        continue;
                    }

                    failed += 1;
                    tombstone.SyncStatus = SafeSyncTombstoneStatus.DeleteFailed;
                    await tombstoneRepository.SaveAsync(state, cancellationToken);
                }

                Status = failed == 0 ? SafeSyncStatus.RetrySucceeded : SafeSyncStatus.RetryPending;
                return new SafeSyncResult
                {
                    IsSuccess = failed == 0,
                    Status = Status,
                    ErrorCode = failed == 0 ? SafeSyncApiError.BatchTombstoneProcessComplete : SafeSyncApiError.TombstoneBatchPartialFailure,
                    ErrorMessage = SafeSyncApiError.ToSafeMessage(failed == 0 ? SafeSyncApiError.BatchTombstoneProcessComplete : SafeSyncApiError.TombstoneBatchPartialFailure),
                    AttemptedCount = attempted,
                    SucceededCount = succeeded,
                    FailedCount = failed,
                    RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken),
                    TombstoneSummary = ToTombstoneSummary(state)
                };
            }
            finally
            {
                Interlocked.Exchange(ref retryInProgress, 0);
            }
        }

        public async Task<SafeSyncResult> CancelTombstoneAsync(string tombstoneId, CancellationToken cancellationToken = default)
        {
            return await UpdateTombstoneStatusAsync(tombstoneId, SafeSyncTombstoneStatus.DeleteCancelled, string.Empty, cancellationToken);
        }

        public async Task<SafeSyncResult> CancelAllFailedTombstonesAsync(CancellationToken cancellationToken = default)
        {
            var state = await tombstoneRepository.LoadAsync(cancellationToken);
            foreach (var tombstone in state.Tombstones ?? new List<SafeSyncTombstone>())
            {
                if (tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteFailed)
                {
                    tombstone.SyncStatus = SafeSyncTombstoneStatus.DeleteCancelled;
                    tombstone.LastSafeErrorCode = string.Empty;
                }
            }

            await tombstoneRepository.SaveAsync(state, cancellationToken);
            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                TombstoneSummary = ToTombstoneSummary(state)
            };
        }

        public async Task<SafeSyncResult> MarkTombstoneResolvedAsync(string tombstoneId, CancellationToken cancellationToken = default)
        {
            return await UpdateTombstoneStatusAsync(tombstoneId, SafeSyncTombstoneStatus.DeleteResolved, string.Empty, cancellationToken);
        }

        public async Task<SafeSyncResult> ClearResolvedTombstonesAsync(CancellationToken cancellationToken = default)
        {
            var state = await tombstoneRepository.LoadAsync(cancellationToken);
            state.Tombstones = (state.Tombstones ?? new List<SafeSyncTombstone>())
                .Where(tombstone => tombstone.SyncStatus != SafeSyncTombstoneStatus.DeleteResolved &&
                                    tombstone.SyncStatus != SafeSyncTombstoneStatus.DeleteCancelled &&
                                    tombstone.SyncStatus != SafeSyncTombstoneStatus.DeleteSynced &&
                                    tombstone.SyncStatus != SafeSyncTombstoneStatus.DeleteAlreadyApplied)
                .ToList();
            await tombstoneRepository.SaveAsync(state, cancellationToken);
            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                TombstoneSummary = ToTombstoneSummary(state)
            };
        }

        public async Task<SafeSyncResult> KeepLocalConflictAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            var result = await ApplyConflictMergePolicyAsync(conflictId, SafeConflictMergePolicy.KeepLocal, true, cancellationToken);
            return result.SyncResult;
        }

        public async Task<SafeSyncResult> KeepRemoteConflictAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            var result = await ApplyConflictMergePolicyAsync(conflictId, SafeConflictMergePolicy.KeepRemote, true, cancellationToken);
            return result.SyncResult;
        }

        public async Task<SafeSyncResult> MarkConflictResolvedAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            var result = await ApplyConflictMergePolicyAsync(conflictId, SafeConflictMergePolicy.MarkResolvedOnly, true, cancellationToken);
            return result.SyncResult;
        }

        public async Task<SafeSyncResult> CancelConflictResolutionAsync(string conflictId, CancellationToken cancellationToken = default)
        {
            return await ResolveConflictAsync(conflictId, SafeSyncConflictResolutionStatus.Cancelled, cancellationToken);
        }

        public async Task<SafeConflictMergePreview> GetConflictMergePreviewAsync(string conflictId, SafeConflictMergePolicy policy, CancellationToken cancellationToken = default)
        {
            var state = await conflictRepository.LoadAsync(cancellationToken);
            var conflict = (state.Conflicts ?? new List<SafeSyncConflict>())
                .FirstOrDefault(item => string.Equals(item.ConflictId, conflictId, StringComparison.Ordinal));
            var localSessionExists = await LocalSessionExistsAsync(conflict?.ClientSessionId, cancellationToken);
            return mergePolicyEvaluator.Preview(conflict, policy, localSessionExists);
        }

        public async Task<SafeConflictMergeResult> ApplyConflictMergePolicyAsync(string conflictId, SafeConflictMergePolicy policy, bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            if (!explicitConfirmation)
            {
                var failure = SafeSyncResult.Failure(SafeSyncStatus.ConflictDetected, SafeSyncApiError.MergePolicyConfirmationRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.MergePolicyConfirmationRequired));
                return MergeResult(conflictId, policy, policy, false, false, false, string.Empty, string.Empty, failure);
            }

            if (Interlocked.Exchange(ref conflictInProgress, 1) == 1)
            {
                var busy = SafeSyncResult.Failure(Status, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
                return MergeResult(conflictId, policy, policy, false, false, false, string.Empty, string.Empty, busy);
            }

            try
            {
                var preview = await GetConflictMergePreviewAsync(conflictId, policy, cancellationToken);
                if (!preview.CanApply)
                {
                    var blockedCode = string.IsNullOrWhiteSpace(preview.BlockedReason) ? SafeSyncApiError.MergePolicyPreviewBlocked : preview.BlockedReason;
                    var blocked = SafeSyncResult.Failure(SafeSyncStatus.ConflictDetected, blockedCode, SafeSyncApiError.ToSafeMessage(blockedCode));
                    return MergeResult(conflictId, policy, preview.EffectivePolicy, false, false, false, string.Empty, string.Empty, blocked);
                }

                var state = await conflictRepository.LoadAsync(cancellationToken);
                var conflict = (state.Conflicts ?? new List<SafeSyncConflict>())
                    .FirstOrDefault(item => string.Equals(item.ConflictId, conflictId, StringComparison.Ordinal));
                if (conflict == null)
                {
                    var missing = SafeSyncResult.Failure(SafeSyncStatus.Failed, SafeSyncApiError.ConflictResolutionFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ConflictResolutionFailed));
                    return MergeResult(conflictId, policy, preview.EffectivePolicy, false, false, false, string.Empty, string.Empty, missing);
                }

                SafeSyncResult syncResult;
                var localChanged = false;
                var queuedRetry = false;
                var queuedRetryId = string.Empty;
                if (preview.EffectivePolicy == SafeConflictMergePolicy.KeepLocal)
                {
                    syncResult = await ResolveConflictCoreAsync(state, conflict, SafeSyncConflictResolutionStatus.KeepLocal, cancellationToken);
                    queuedRetry = syncResult.IsSuccess;
                    queuedRetryId = await FindMatchingRetryEntryIdAsync(conflict.ClientSessionId, cancellationToken);
                }
                else if (preview.EffectivePolicy == SafeConflictMergePolicy.KeepRemote)
                {
                    syncResult = await ResolveConflictCoreAsync(state, conflict, SafeSyncConflictResolutionStatus.KeepRemote, cancellationToken);
                    localChanged = syncResult.IsSuccess && string.Equals(syncResult.ErrorCode, SafeSyncApiError.KeepRemoteApplied, StringComparison.Ordinal);
                }
                else if (preview.EffectivePolicy == SafeConflictMergePolicy.MarkResolvedOnly)
                {
                    syncResult = await ResolveConflictCoreAsync(state, conflict, SafeSyncConflictResolutionStatus.MarkResolved, cancellationToken);
                }
                else if (preview.EffectivePolicy == SafeConflictMergePolicy.MergeNonConflictingAggregates)
                {
                    syncResult = await ApplyMergedAggregateAsync(state, conflict, preview.ResultingSummary, cancellationToken);
                    localChanged = syncResult.IsSuccess;
                    queuedRetry = syncResult.IsSuccess;
                    queuedRetryId = await FindMatchingRetryEntryIdAsync(conflict.ClientSessionId, cancellationToken);
                }
                else
                {
                    syncResult = SafeSyncResult.Failure(SafeSyncStatus.ConflictDetected, SafeSyncApiError.MergePolicyPreviewBlocked, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.MergePolicyPreviewBlocked));
                }

                var auditEntryId = await RecordConflictAuditAsync(conflict, preview, syncResult, queuedRetryId, cancellationToken);
                return MergeResult(conflictId, policy, preview.EffectivePolicy, syncResult.IsSuccess, localChanged, queuedRetry, queuedRetryId, auditEntryId, syncResult);
            }
            finally
            {
                Interlocked.Exchange(ref conflictInProgress, 0);
            }
        }

        public async Task<SafeConflictAuditSummary> GetConflictAuditHistoryAsync(CancellationToken cancellationToken = default)
        {
            var state = await conflictAuditRepository.LoadAsync(cancellationToken);
            return ToAuditSummary(state);
        }

        public async Task<SafeSyncResult> ClearResolvedConflictAuditHistoryAsync(bool explicitConfirmation, CancellationToken cancellationToken = default)
        {
            if (!explicitConfirmation)
            {
                return SafeSyncResult.Failure(SafeSyncStatus.ConflictDetected, SafeSyncApiError.MergePolicyConfirmationRequired, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.MergePolicyConfirmationRequired));
            }

            var state = await conflictAuditRepository.LoadAsync(cancellationToken);
            state.Entries = (state.Entries ?? new List<SafeConflictAuditEntry>())
                .Where(entry => string.Equals(entry.ResultStatus, "failed", StringComparison.OrdinalIgnoreCase))
                .ToList();
            await conflictAuditRepository.SaveAsync(state, cancellationToken);
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.Ready,
                ErrorCode = SafeSyncApiError.ConflictAuditCleared,
                ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ConflictAuditCleared)
            };
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

            var unsafeRejected = 0;
            var safeRemoteSessions = new List<RemoteSafeActivitySessionDto>();
            foreach (var session in response.Value?.Sessions ?? new List<RemoteSafeActivitySessionDto>())
            {
                var validation = remoteValidator.Validate(session);
                if (!validation.IsSuccess)
                {
                    unsafeRejected += 1;
                    continue;
                }

                safeRemoteSessions.Add(session);
            }

            var summaries = safeRemoteSessions
                .Select(ToSummary)
                .Where(summary => privacySanitizer.ValidateNoForbiddenFields(summary).IsSuccess)
                .ToList();

            await RecordSessionMappingsAsync(summaries, cancellationToken);
            var detection = await DetectRemoteSessionConflictsAsync(safeRemoteSessions, cancellationToken);

            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                RemoteSessions = summaries,
                AcceptedCount = summaries.Count,
                RejectedCount = unsafeRejected,
                UnsafeRejectedCount = unsafeRejected,
                ConflictDetectedCount = detection.conflictsDetected,
                UnchangedCount = detection.unchanged,
                ErrorCode = detection.conflictsDetected > 0 ? SafeSyncApiError.ConflictBatchDetected : string.Empty,
                SchemaVersion = response.Value?.SchemaVersion ?? 1,
                ConflictSummary = await GetConflictSummaryAsync(cancellationToken)
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
                if (IsNotFound(response.ErrorCode))
                {
                    await RecordTombstoneAsync(string.Empty, serverSessionId, SafeSyncTombstoneDeleteSource.ServerNotFound, SafeSyncTombstoneStatus.DeleteSynced, SafeSyncApiError.DeleteAlreadyApplied, cancellationToken);
                    Status = SafeSyncStatus.Ready;
                    return new SafeSyncResult
                    {
                        IsSuccess = true,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.DeleteAlreadyApplied,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.DeleteAlreadyApplied),
                        TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
                    };
                }

                if (retryPolicy.Classify(response.ErrorCode) == SafeSyncRetryClassification.Retryable)
                {
                    await AddOrUpdateRetryEntryAsync(
                        SafeSyncRetryOperationType.DELETE_REMOTE_SESSION,
                        new List<string>(),
                        serverSessionId,
                        response.ErrorCode,
                        SafeSyncRetryQueueEntryStatus.Pending,
                        cancellationToken);
                    await RecordTombstoneAsync(string.Empty, serverSessionId, SafeSyncTombstoneDeleteSource.RemoteUser, SafeSyncTombstoneStatus.PendingDelete, response.ErrorCode, cancellationToken);
                    Status = SafeSyncStatus.RetryPending;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.RetryQueued,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.RetryQueued),
                        RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken),
                        TombstoneSummary = await GetTombstoneSummaryAsync(cancellationToken)
                    };
                }

                return FailFromApi(response);
            }

            await RecordTombstoneAsync(string.Empty, serverSessionId, SafeSyncTombstoneDeleteSource.RemoteUser, SafeSyncTombstoneStatus.DeleteSynced, string.Empty, cancellationToken);
            Status = SafeSyncStatus.Ready;
            return SafeSyncResult.Success(Status);
            }
            finally
            {
                Interlocked.Exchange(ref deleteInProgress, 0);
            }
        }

        private async Task<SafeSyncResult> ProcessUpsertRetryEntryAsync(SafeSyncRetryQueueEntry entry, CancellationToken cancellationToken)
        {
            if (!apiClient.Config.HasValidBaseUrl())
            {
                return SafeSyncResult.Failure(SafeSyncStatus.Failed, SafeSyncApiError.InvalidBaseUrl, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl));
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            var filtered = FilterSaveDataForRetry(saveData, entry.ClientSessionIds);
            SafeActivitySessionsContractRequest request;
            try
            {
                request = mapper.ToActivitySessionsContractRequest(filtered, BuildClientSyncId());
                var guard = ValidateOutgoingPayload(request);
                if (!guard.IsSuccess)
                {
                    return SafeSyncResult.Failure(SafeSyncStatus.PrivacyBlocked, guard.ErrorCode, guard.ErrorMessage);
                }
            }
            catch (Exception)
            {
                return SafeSyncResult.Failure(SafeSyncStatus.PrivacyBlocked, SafeSyncApiError.PrivacyGuardBlockedPayload, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.PrivacyGuardBlockedPayload));
            }

            var response = await apiClient.PostActivitySessionsAsync(request, cancellationToken);
            if (!response.IsSuccess)
            {
                if (retryPolicy.Classify(response.ErrorCode) == SafeSyncRetryClassification.Conflict)
                {
                    await RecordConflictAsync(entry, SafeSyncConflictType.ValidationConflict, response.ErrorCode, filtered, cancellationToken);
                }

                return FailResultFromApi(response);
            }

            var errorCode = FirstRejectedCode(response.Value);
            if (response.Value != null && response.Value.RejectedCount > 0)
            {
                if (retryPolicy.Classify(errorCode) == SafeSyncRetryClassification.Conflict)
                {
                    await RecordConflictAsync(entry, SafeSyncConflictType.ValidationConflict, errorCode, filtered, cancellationToken);
                }

                return SafeSyncResult.Failure(SafeSyncStatus.Failed, errorCode, SafeSyncApiError.ToSafeMessage(errorCode));
            }

            await RecordSessionMappingsAsync(response.Value?.Results, cancellationToken);

            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = SafeSyncStatus.RetrySucceeded,
                AcceptedCount = response.Value?.AcceptedCount ?? request.Sessions.Count,
                SchemaVersion = response.Value?.SchemaVersion ?? 1
            };
        }

        private async Task<SafeSyncResult> ProcessDeleteRetryEntryAsync(SafeSyncRetryQueueEntry entry, CancellationToken cancellationToken)
        {
            if (!apiClient.Config.HasValidBaseUrl())
            {
                return SafeSyncResult.Failure(SafeSyncStatus.Failed, SafeSyncApiError.InvalidBaseUrl, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.InvalidBaseUrl));
            }

            var response = await apiClient.DeleteActivitySessionAsync(entry.ServerSessionId, cancellationToken);
            if (response.IsSuccess)
            {
                await RecordTombstoneAsync(string.Empty, entry.ServerSessionId, SafeSyncTombstoneDeleteSource.RemoteUser, SafeSyncTombstoneStatus.DeleteSynced, string.Empty, cancellationToken);
                return SafeSyncResult.Success(SafeSyncStatus.RetrySucceeded);
            }

            if (IsNotFound(response.ErrorCode))
            {
                await RecordTombstoneAsync(string.Empty, entry.ServerSessionId, SafeSyncTombstoneDeleteSource.ServerNotFound, SafeSyncTombstoneStatus.DeleteSynced, SafeSyncApiError.DeleteAlreadyApplied, cancellationToken);
                await RecordConflictAsync(entry, SafeSyncConflictType.DeleteAlreadyApplied, SafeSyncApiError.DeleteAlreadyApplied, null, cancellationToken);
                return new SafeSyncResult
                {
                    IsSuccess = true,
                    Status = SafeSyncStatus.RetrySucceeded,
                    ErrorCode = SafeSyncApiError.DeleteAlreadyApplied,
                    ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.DeleteAlreadyApplied)
                };
            }

            await RecordTombstoneAsync(string.Empty, entry.ServerSessionId, SafeSyncTombstoneDeleteSource.RemoteUser, SafeSyncTombstoneStatus.DeleteFailed, response.ErrorCode, cancellationToken);
            return FailResultFromApi(response);
        }

        private void ApplyRetryFailure(SafeSyncRetryQueueEntry entry, string errorCode, DateTimeOffset now)
        {
            entry.LastSafeErrorCode = string.IsNullOrWhiteSpace(errorCode) ? SafeSyncApiError.ServerUnavailable : errorCode;
            entry.UpdatedAt = now;
            var classification = retryPolicy.Classify(entry.LastSafeErrorCode);
            if (classification == SafeSyncRetryClassification.AuthRequired)
            {
                entry.Status = SafeSyncRetryQueueEntryStatus.Paused;
                return;
            }

            if (classification == SafeSyncRetryClassification.Conflict)
            {
                entry.Status = SafeSyncRetryQueueEntryStatus.Conflict;
                return;
            }

            if (classification == SafeSyncRetryClassification.NonRetryable)
            {
                entry.Status = SafeSyncRetryQueueEntryStatus.Failed;
                return;
            }

            if (!retryPolicy.HasAttemptsRemaining(entry.AttemptCount, entry.MaxAttempts))
            {
                entry.Status = SafeSyncRetryQueueEntryStatus.Failed;
                entry.LastSafeErrorCode = SafeSyncApiError.RetryExhausted;
                return;
            }

            entry.Status = SafeSyncRetryQueueEntryStatus.Pending;
            entry.NextAttemptAt = retryPolicy.CalculateNextAttemptAt(now, entry.AttemptCount);
        }

        private async Task AddOrUpdateRetryEntryAsync(
            SafeSyncRetryOperationType operationType,
            List<string> clientSessionIds,
            string serverSessionId,
            string errorCode,
            SafeSyncRetryQueueEntryStatus status,
            CancellationToken cancellationToken)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            state.Entries = state.Entries ?? new List<SafeSyncRetryQueueEntry>();
            var safeClientIds = (clientSessionIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var existing = state.Entries.FirstOrDefault(entry =>
                entry.OperationType == operationType &&
                entry.Status != SafeSyncRetryQueueEntryStatus.Cancelled &&
                entry.Status != SafeSyncRetryQueueEntryStatus.Succeeded &&
                string.Equals(entry.ServerSessionId ?? string.Empty, serverSessionId ?? string.Empty, StringComparison.Ordinal) &&
                SameIds(entry.ClientSessionIds, safeClientIds));

            var now = DateTimeOffset.UtcNow;
            if (existing == null)
            {
                existing = new SafeSyncRetryQueueEntry
                {
                    OperationType = operationType,
                    ClientSessionIds = safeClientIds,
                    ServerSessionId = serverSessionId ?? string.Empty,
                    CreatedAt = now,
                    MaxAttempts = retryPolicy.MaxAttempts
                };
                state.Entries.Add(existing);
            }

            existing.Status = status;
            existing.UpdatedAt = now;
            existing.NextAttemptAt = now;
            existing.LastSafeErrorCode = errorCode ?? string.Empty;
            existing.MaxAttempts = existing.MaxAttempts <= 0 ? retryPolicy.MaxAttempts : existing.MaxAttempts;
            await retryQueueRepository.SaveAsync(state, cancellationToken);
        }

        private async Task<bool> RemoveClientSessionFromPendingUpsertsAsync(string clientSessionId, CancellationToken cancellationToken)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            var changed = false;
            foreach (var entry in state.Entries ?? new List<SafeSyncRetryQueueEntry>())
            {
                if (entry.OperationType != SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS ||
                    (entry.Status != SafeSyncRetryQueueEntryStatus.Pending &&
                     entry.Status != SafeSyncRetryQueueEntryStatus.Paused &&
                     entry.Status != SafeSyncRetryQueueEntryStatus.Failed &&
                     entry.Status != SafeSyncRetryQueueEntryStatus.Conflict))
                {
                    continue;
                }

                var ids = entry.ClientSessionIds ?? new List<string>();
                if (!ids.Contains(clientSessionId, StringComparer.Ordinal))
                {
                    continue;
                }

                entry.ClientSessionIds = ids
                    .Where(id => !string.Equals(id, clientSessionId, StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList();
                entry.UpdatedAt = DateTimeOffset.UtcNow;
                entry.LastSafeErrorCode = SafeSyncApiError.UpsertRetryUpdatedAfterLocalDelete;
                if (entry.ClientSessionIds.Count == 0)
                {
                    entry.Status = SafeSyncRetryQueueEntryStatus.Cancelled;
                }

                changed = true;
            }

            if (changed)
            {
                await retryQueueRepository.SaveAsync(state, cancellationToken);
            }

            return changed;
        }

        private async Task<SafeSyncLocalSessionMapping> FindMappingAsync(string clientSessionId, CancellationToken cancellationToken)
        {
            var state = await localStateRepository.LoadAsync(cancellationToken);
            return (state.SessionMappings ?? new List<SafeSyncLocalSessionMapping>())
                .FirstOrDefault(mapping => string.Equals(mapping.ClientSessionId, clientSessionId, StringComparison.Ordinal));
        }

        private async Task RemoveLocalMappingAsync(string clientSessionId, CancellationToken cancellationToken)
        {
            var state = await localStateRepository.LoadAsync(cancellationToken);
            state.SessionMappings = (state.SessionMappings ?? new List<SafeSyncLocalSessionMapping>())
                .Where(mapping => !string.Equals(mapping.ClientSessionId, clientSessionId, StringComparison.Ordinal))
                .ToList();
            await localStateRepository.SaveAsync(state, cancellationToken);
        }

        private async Task RecordSessionMappingsAsync(IEnumerable<SafeActivitySessionUpsertResult> results, CancellationToken cancellationToken)
        {
            var mappings = (results ?? new List<SafeActivitySessionUpsertResult>())
                .Where(result => result != null &&
                                 string.Equals(result.Status, "accepted", StringComparison.OrdinalIgnoreCase) &&
                                 !string.IsNullOrWhiteSpace(result.ClientSessionId) &&
                                 !string.IsNullOrWhiteSpace(result.ServerSessionId))
                .Select(result => new SafeSyncLocalSessionMapping
                {
                    ClientSessionId = result.ClientSessionId,
                    ServerSessionId = result.ServerSessionId,
                    UpdatedAt = DateTimeOffset.UtcNow
                })
                .ToList();
            await RecordSessionMappingsAsync(mappings, cancellationToken);
        }

        private async Task RecordSessionMappingsAsync(IEnumerable<RemoteSafeSessionSummary> summaries, CancellationToken cancellationToken)
        {
            var mappings = (summaries ?? new List<RemoteSafeSessionSummary>())
                .Where(summary => summary != null &&
                                  !string.IsNullOrWhiteSpace(summary.ClientSessionId) &&
                                  !string.IsNullOrWhiteSpace(summary.ServerSessionId))
                .Select(summary => new SafeSyncLocalSessionMapping
                {
                    ClientSessionId = summary.ClientSessionId,
                    ServerSessionId = summary.ServerSessionId,
                    UpdatedAt = DateTimeOffset.UtcNow
                })
                .ToList();
            await RecordSessionMappingsAsync(mappings, cancellationToken);
        }

        private async Task RecordSessionMappingsAsync(List<SafeSyncLocalSessionMapping> mappings, CancellationToken cancellationToken)
        {
            if (mappings == null || mappings.Count == 0)
            {
                return;
            }

            var state = await localStateRepository.LoadAsync(cancellationToken);
            state.SessionMappings = state.SessionMappings ?? new List<SafeSyncLocalSessionMapping>();
            foreach (var mapping in mappings)
            {
                if (privacySanitizer.ValidateNoForbiddenFields(mapping).IsSuccess == false)
                {
                    continue;
                }

                var existing = state.SessionMappings.FirstOrDefault(item => string.Equals(item.ClientSessionId, mapping.ClientSessionId, StringComparison.Ordinal));
                if (existing == null)
                {
                    state.SessionMappings.Add(mapping);
                }
                else
                {
                    existing.ServerSessionId = mapping.ServerSessionId;
                    existing.UpdatedAt = mapping.UpdatedAt;
                }
            }

            await localStateRepository.SaveAsync(state, cancellationToken);
        }

        private async Task<(int conflictsDetected, int unchanged)> DetectRemoteSessionConflictsAsync(List<RemoteSafeActivitySessionDto> remoteSessions, CancellationToken cancellationToken)
        {
            if (remoteSessions == null || remoteSessions.Count == 0)
            {
                return (0, 0);
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            var localById = (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Where(session => !string.IsNullOrWhiteSpace(session.SessionId))
                .GroupBy(session => session.SessionId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var tombstoneState = await tombstoneRepository.LoadAsync(cancellationToken);
            var tombstoneClientIds = new HashSet<string>(
                (tombstoneState.Tombstones ?? new List<SafeSyncTombstone>())
                    .Where(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.PendingDelete)
                    .Select(tombstone => tombstone.ClientSessionId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
            var conflictState = await conflictRepository.LoadAsync(cancellationToken);
            conflictState.Conflicts = conflictState.Conflicts ?? new List<SafeSyncConflict>();
            var changed = false;
            var conflictsDetected = 0;
            var unchanged = 0;

            foreach (var remoteDto in remoteSessions)
            {
                var remote = ToSummary(remoteDto);
                if (string.IsNullOrWhiteSpace(remote.ClientSessionId))
                {
                    continue;
                }

                localById.TryGetValue(remote.ClientSessionId, out var local);
                var type = SafeSyncConflictType.Unknown;
                var localSummary = ToSafeLocalSummary(saveData, remote.ClientSessionId);
                var remoteSummary = ToSafeRemoteSummary(remote);
                var diff = summaryComparer.Compare(localSummary, remoteSummary);
                if (tombstoneClientIds.Contains(remote.ClientSessionId))
                {
                    type = SafeSyncConflictType.RemoteDifferent;
                }
                else if (local == null)
                {
                    type = SafeSyncConflictType.LocalMissing;
                }
                else if (diff.IsDifferent)
                {
                    type = SafeSyncConflictType.RemoteDifferent;
                }

                if (type == SafeSyncConflictType.Unknown)
                {
                    unchanged += 1;
                    continue;
                }

                var alreadyUnresolved = conflictState.Conflicts.Any(conflict =>
                    conflict.ResolutionStatus == SafeSyncConflictResolutionStatus.Unresolved &&
                    conflict.ConflictType == type &&
                    string.Equals(conflict.ClientSessionId, remote.ClientSessionId, StringComparison.Ordinal));
                if (alreadyUnresolved)
                {
                    continue;
                }

                conflictState.Conflicts.Add(new SafeSyncConflict
                {
                    ClientSessionId = remote.ClientSessionId,
                    ServerSessionId = remote.ServerSessionId,
                    ConflictType = type,
                    SafeErrorCode = diff.IsDifferent ? SafeSyncApiError.ConflictDiffDetected : SafeSyncApiError.ConflictDetected,
                    SafeLocalSummary = localSummary,
                    SafeRemoteSummary = remoteSummary,
                    SafeRemoteSession = CloneRemoteSession(remoteDto),
                    SafeDiffSummary = diff,
                    ResolutionStatus = SafeSyncConflictResolutionStatus.Unresolved
                });
                conflictsDetected += 1;
                changed = true;
            }

            if (changed)
            {
                await conflictRepository.SaveAsync(conflictState, cancellationToken);
            }

            var localState = await localStateRepository.LoadAsync(cancellationToken);
            var remoteClientIds = new HashSet<string>(
                remoteSessions.Select(session => session.ClientSessionId).Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
            foreach (var mapping in localState.SessionMappings ?? new List<SafeSyncLocalSessionMapping>())
            {
                if (string.IsNullOrWhiteSpace(mapping.ClientSessionId) ||
                    remoteClientIds.Contains(mapping.ClientSessionId) ||
                    !localById.ContainsKey(mapping.ClientSessionId))
                {
                    continue;
                }

                var alreadyUnresolved = conflictState.Conflicts.Any(conflict =>
                    conflict.ResolutionStatus == SafeSyncConflictResolutionStatus.Unresolved &&
                    conflict.ConflictType == SafeSyncConflictType.RemoteMissing &&
                    string.Equals(conflict.ClientSessionId, mapping.ClientSessionId, StringComparison.Ordinal));
                if (alreadyUnresolved)
                {
                    continue;
                }

                conflictState.Conflicts.Add(new SafeSyncConflict
                {
                    ClientSessionId = mapping.ClientSessionId,
                    ServerSessionId = mapping.ServerSessionId,
                    ConflictType = SafeSyncConflictType.RemoteMissing,
                    SafeErrorCode = SafeSyncApiError.RemoteSessionMissing,
                    SafeLocalSummary = ToSafeLocalSummary(saveData, mapping.ClientSessionId),
                    SafeRemoteSummary = new SafeSyncSessionSafeSummary
                    {
                        ClientSessionId = mapping.ClientSessionId,
                        ServerSessionId = mapping.ServerSessionId,
                        SchemaVersion = 1
                    },
                    SafeDiffSummary = new SafeSessionDiffSummary
                    {
                        IsDifferent = true,
                        ChangedSafeFieldNames = new List<string> { "remotePresence" }
                    },
                    ResolutionStatus = SafeSyncConflictResolutionStatus.Unresolved
                });
                conflictsDetected += 1;
                changed = true;
            }

            if (changed)
            {
                await conflictRepository.SaveAsync(conflictState, cancellationToken);
            }

            return (conflictsDetected, unchanged);
        }

        private async Task<SafeSyncResult> UpdateTombstoneStatusAsync(string tombstoneId, SafeSyncTombstoneStatus status, string errorCode, CancellationToken cancellationToken)
        {
            var state = await tombstoneRepository.LoadAsync(cancellationToken);
            var tombstone = (state.Tombstones ?? new List<SafeSyncTombstone>())
                .FirstOrDefault(item => string.Equals(item.TombstoneId, tombstoneId, StringComparison.Ordinal));
            if (tombstone != null)
            {
                tombstone.SyncStatus = status;
                tombstone.LastSafeErrorCode = errorCode ?? string.Empty;
                await tombstoneRepository.SaveAsync(state, cancellationToken);
            }

            Status = SafeSyncStatus.Ready;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                TombstoneSummary = ToTombstoneSummary(state)
            };
        }

        private async Task<SafeSyncResult> ResolveConflictAsync(string conflictId, SafeSyncConflictResolutionStatus resolution, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref conflictInProgress, 1) == 1)
            {
                return SafeSyncResult.Failure(Status, SafeSyncApiError.AlreadyInProgress, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.AlreadyInProgress));
            }

            try
            {
                var state = await conflictRepository.LoadAsync(cancellationToken);
                var conflict = (state.Conflicts ?? new List<SafeSyncConflict>())
                    .FirstOrDefault(item => string.Equals(item.ConflictId, conflictId, StringComparison.Ordinal));
                if (conflict == null)
                {
                    Status = SafeSyncStatus.Failed;
                    return SafeSyncResult.Failure(Status, SafeSyncApiError.ConflictResolutionFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ConflictResolutionFailed));
                }

                return await ResolveConflictCoreAsync(state, conflict, resolution, cancellationToken);
            }
            finally
            {
                Interlocked.Exchange(ref conflictInProgress, 0);
            }
        }

        private async Task<SafeSyncResult> ResolveConflictCoreAsync(SafeSyncConflictState state, SafeSyncConflict conflict, SafeSyncConflictResolutionStatus resolution, CancellationToken cancellationToken)
        {
            if (resolution == SafeSyncConflictResolutionStatus.Cancelled)
            {
                Status = SafeSyncStatus.ConflictDetected;
                return new SafeSyncResult
                {
                    IsSuccess = true,
                    Status = Status,
                    ConflictSummary = ToConflictSummary(state)
                };
            }

            if (resolution == SafeSyncConflictResolutionStatus.KeepLocal)
            {
                var saveData = await repository.LoadAsync(cancellationToken);
                var exists = (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                    .Any(session => string.Equals(session.SessionId, conflict.ClientSessionId, StringComparison.Ordinal));
                if (!exists)
                {
                    Status = SafeSyncStatus.Failed;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = SafeSyncApiError.LocalSessionMissing,
                        ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.LocalSessionMissing),
                        ConflictSummary = ToConflictSummary(state)
                    };
                }

                await AddOrUpdateRetryEntryAsync(
                    SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                    new List<string> { conflict.ClientSessionId },
                    string.Empty,
                    SafeSyncApiError.ConflictKeepLocalQueued,
                    SafeSyncRetryQueueEntryStatus.Pending,
                    cancellationToken);
                conflict.ResolutionStatus = SafeSyncConflictResolutionStatus.KeepLocal;
                await conflictRepository.SaveAsync(state, cancellationToken);
                Status = SafeSyncStatus.RetryPending;
                return new SafeSyncResult
                {
                    IsSuccess = true,
                    Status = Status,
                    ErrorCode = SafeSyncApiError.ConflictKeepLocalQueued,
                    ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ConflictKeepLocalQueued),
                    RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken),
                    ConflictSummary = ToConflictSummary(state)
                };
            }

            if (resolution == SafeSyncConflictResolutionStatus.KeepRemote)
            {
                var applyResult = await remoteApplicator.ApplyAsync(conflict.SafeRemoteSession, cancellationToken);
                if (!applyResult.IsApplied && !applyResult.IsMarkerOnly)
                {
                    conflict.SafeErrorCode = applyResult.ErrorCode;
                    await conflictRepository.SaveAsync(state, cancellationToken);
                    Status = SafeSyncStatus.PrivacyBlocked;
                    return new SafeSyncResult
                    {
                        IsSuccess = false,
                        Status = Status,
                        ErrorCode = applyResult.ErrorCode,
                        ErrorMessage = applyResult.ErrorMessage,
                        ConflictSummary = ToConflictSummary(state)
                    };
                }

                conflict.ResolutionStatus = SafeSyncConflictResolutionStatus.KeepRemote;
                conflict.SafeErrorCode = applyResult.IsApplied
                    ? SafeSyncApiError.KeepRemoteApplied
                    : SafeSyncApiError.KeepRemoteMarkerOnly;
                await conflictRepository.SaveAsync(state, cancellationToken);
                if (applyResult.IsApplied)
                {
                    await RecordSessionMappingsAsync(new List<SafeSyncLocalSessionMapping>
                    {
                        new SafeSyncLocalSessionMapping
                        {
                            ClientSessionId = conflict.ClientSessionId ?? string.Empty,
                            ServerSessionId = conflict.ServerSessionId ?? string.Empty,
                            UpdatedAt = DateTimeOffset.UtcNow
                        }
                    }, cancellationToken);
                }

                Status = SafeSyncStatus.Ready;
                return new SafeSyncResult
                {
                    IsSuccess = true,
                    Status = Status,
                    ErrorCode = conflict.SafeErrorCode,
                    ErrorMessage = SafeSyncApiError.ToSafeMessage(conflict.SafeErrorCode),
                    ConflictSummary = ToConflictSummary(state)
                };
            }

            conflict.ResolutionStatus = resolution;
            await conflictRepository.SaveAsync(state, cancellationToken);
            Status = SafeSyncStatus.Ready;
            var code = SafeSyncApiError.ConflictMarkedResolved;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                ErrorCode = code,
                ErrorMessage = SafeSyncApiError.ToSafeMessage(code),
                ConflictSummary = ToConflictSummary(state)
            };
        }

        private async Task<SafeSyncResult> ApplyMergedAggregateAsync(SafeSyncConflictState state, SafeSyncConflict conflict, SafeSyncSessionSafeSummary mergedSummary, CancellationToken cancellationToken)
        {
            var remote = ToRemoteSafeSession(mergedSummary, conflict.SafeRemoteSession);
            var validation = remoteValidator.Validate(remote);
            if (!validation.IsSuccess)
            {
                Status = SafeSyncStatus.PrivacyBlocked;
                return SafeSyncResult.Failure(Status, validation.ErrorCode, validation.ErrorMessage);
            }

            var applyResult = await remoteApplicator.ApplyAsync(remote, cancellationToken);
            if (!applyResult.IsApplied)
            {
                Status = SafeSyncStatus.PrivacyBlocked;
                return SafeSyncResult.Failure(Status, applyResult.ErrorCode, applyResult.ErrorMessage);
            }

            await AddOrUpdateRetryEntryAsync(
                SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS,
                new List<string> { conflict.ClientSessionId },
                string.Empty,
                SafeSyncApiError.MergePolicyApplied,
                SafeSyncRetryQueueEntryStatus.Pending,
                cancellationToken);
            conflict.ResolutionStatus = SafeSyncConflictResolutionStatus.Merged;
            conflict.SafeErrorCode = SafeSyncApiError.MergePolicyApplied;
            conflict.SafeLocalSummary = SafeConflictMergePolicyEvaluator.Clone(mergedSummary);
            await conflictRepository.SaveAsync(state, cancellationToken);
            await RecordSessionMappingsAsync(new List<SafeSyncLocalSessionMapping>
            {
                new SafeSyncLocalSessionMapping
                {
                    ClientSessionId = conflict.ClientSessionId ?? string.Empty,
                    ServerSessionId = conflict.ServerSessionId ?? string.Empty,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            }, cancellationToken);

            Status = SafeSyncStatus.RetryPending;
            return new SafeSyncResult
            {
                IsSuccess = true,
                Status = Status,
                ErrorCode = SafeSyncApiError.MergePolicyApplied,
                ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.MergePolicyApplied),
                RetryQueueSummary = await GetRetryQueueSummaryAsync(cancellationToken),
                ConflictSummary = ToConflictSummary(state)
            };
        }

        private async Task<string> RecordConflictAuditAsync(SafeSyncConflict conflict, SafeConflictMergePreview preview, SafeSyncResult result, string queuedRetryEntryId, CancellationToken cancellationToken)
        {
            var entry = new SafeConflictAuditEntry
            {
                ConflictId = conflict?.ConflictId ?? preview?.ConflictId ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow,
                Action = "applyMergePolicy",
                Policy = preview?.SelectedPolicy ?? SafeConflictMergePolicy.KeepLocal,
                ResultStatus = result != null && result.IsSuccess ? "resolved" : "failed",
                SafeDiffFieldNames = (preview?.DiffSummary?.ChangedSafeFieldNames ?? new List<string>()).ToList(),
                SafeLocalSummaryBefore = SafeConflictMergePolicyEvaluator.Clone(preview?.LocalSummary),
                SafeRemoteSummaryBefore = SafeConflictMergePolicyEvaluator.Clone(preview?.RemoteSummary),
                SafeResultSummaryAfter = preview?.ResultingSummary == null ? null : SafeConflictMergePolicyEvaluator.Clone(preview.ResultingSummary),
                QueuedRetryEntryId = queuedRetryEntryId ?? string.Empty,
                WarningIds = (preview?.Warnings ?? new List<string>()).ToList(),
                UserMessageCode = result?.ErrorCode ?? string.Empty
            };

            if (!privacySanitizer.ValidateNoForbiddenFields(entry).IsSuccess)
            {
                return string.Empty;
            }

            var state = await conflictAuditRepository.LoadAsync(cancellationToken);
            state.Entries = state.Entries ?? new List<SafeConflictAuditEntry>();
            state.Entries.Add(entry);
            await conflictAuditRepository.SaveAsync(state, cancellationToken);
            return entry.AuditEntryId;
        }

        private static SafeConflictMergeResult MergeResult(string conflictId, SafeConflictMergePolicy policy, SafeConflictMergePolicy effectivePolicy, bool applied, bool localChanged, bool queuedRetry, string queuedRetryEntryId, string auditEntryId, SafeSyncResult syncResult)
        {
            syncResult = syncResult ?? SafeSyncResult.Failure(SafeSyncStatus.Failed, SafeSyncApiError.ConflictResolutionFailed, SafeSyncApiError.ToSafeMessage(SafeSyncApiError.ConflictResolutionFailed));
            return new SafeConflictMergeResult
            {
                ConflictId = conflictId ?? string.Empty,
                Policy = policy,
                EffectivePolicy = effectivePolicy,
                Applied = applied,
                LocalChanged = localChanged,
                QueuedRetry = queuedRetry,
                QueuedRetryEntryId = queuedRetryEntryId ?? string.Empty,
                AuditEntryId = auditEntryId ?? string.Empty,
                UserMessage = SafeSyncApiError.ToSafeMessage(syncResult.ErrorCode),
                UserMessageCode = syncResult.ErrorCode ?? string.Empty,
                SyncResult = syncResult
            };
        }

        private async Task<bool> LocalSessionExistsAsync(string clientSessionId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clientSessionId))
            {
                return false;
            }

            var saveData = await repository.LoadAsync(cancellationToken);
            return (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Any(session => string.Equals(session.SessionId, clientSessionId, StringComparison.Ordinal));
        }

        private async Task<string> FindMatchingRetryEntryIdAsync(string clientSessionId, CancellationToken cancellationToken)
        {
            var state = await retryQueueRepository.LoadAsync(cancellationToken);
            return (state.Entries ?? new List<SafeSyncRetryQueueEntry>())
                .Where(entry => entry.OperationType == SafeSyncRetryOperationType.UPSERT_ACTIVITY_SESSIONS &&
                                entry.Status == SafeSyncRetryQueueEntryStatus.Pending &&
                                (entry.ClientSessionIds ?? new List<string>()).Contains(clientSessionId ?? string.Empty, StringComparer.Ordinal))
                .OrderByDescending(entry => entry.UpdatedAt)
                .Select(entry => entry.QueueEntryId)
                .FirstOrDefault() ?? string.Empty;
        }

        private static SafeConflictAuditSummary ToAuditSummary(SafeConflictAuditState state)
        {
            var entries = (state?.Entries ?? new List<SafeConflictAuditEntry>())
                .OrderByDescending(entry => entry.CreatedAt)
                .Take(50)
                .ToList();
            return new SafeConflictAuditSummary
            {
                TotalCount = state?.Entries?.Count ?? 0,
                ResolvedCount = (state?.Entries ?? new List<SafeConflictAuditEntry>()).Count(entry => string.Equals(entry.ResultStatus, "resolved", StringComparison.OrdinalIgnoreCase)),
                FailedCount = (state?.Entries ?? new List<SafeConflictAuditEntry>()).Count(entry => string.Equals(entry.ResultStatus, "failed", StringComparison.OrdinalIgnoreCase)),
                SafeEntries = entries
            };
        }

        private async Task RecordConflictAsync(SafeSyncRetryQueueEntry entry, SafeSyncConflictType type, string errorCode, SaveData saveData, CancellationToken cancellationToken)
        {
            var state = await conflictRepository.LoadAsync(cancellationToken);
            state.Conflicts = state.Conflicts ?? new List<SafeSyncConflict>();
            var clientSessionId = entry.ClientSessionIds?.FirstOrDefault() ?? string.Empty;
            state.Conflicts.Add(new SafeSyncConflict
            {
                ClientSessionId = clientSessionId,
                ServerSessionId = entry.ServerSessionId ?? string.Empty,
                ConflictType = type,
                SafeErrorCode = errorCode ?? string.Empty,
                SafeLocalSummary = ToSafeLocalSummary(saveData, clientSessionId),
                SafeRemoteSummary = new SafeSyncSessionSafeSummary
                {
                    ClientSessionId = clientSessionId,
                    SchemaVersion = 1
                },
                ResolutionStatus = type == SafeSyncConflictType.DeleteAlreadyApplied
                    ? SafeSyncConflictResolutionStatus.MarkResolved
                    : SafeSyncConflictResolutionStatus.Unresolved
            });
            await conflictRepository.SaveAsync(state, cancellationToken);
        }

        private async Task RecordTombstoneAsync(string clientSessionId, string serverSessionId, SafeSyncTombstoneDeleteSource source, SafeSyncTombstoneStatus status, string errorCode, CancellationToken cancellationToken)
        {
            var state = await tombstoneRepository.LoadAsync(cancellationToken);
            state.Tombstones = state.Tombstones ?? new List<SafeSyncTombstone>();
            var existing = state.Tombstones.FirstOrDefault(item =>
                (!string.IsNullOrWhiteSpace(serverSessionId) && string.Equals(item.ServerSessionId ?? string.Empty, serverSessionId ?? string.Empty, StringComparison.Ordinal)) ||
                (string.IsNullOrWhiteSpace(serverSessionId) && string.Equals(item.ClientSessionId ?? string.Empty, clientSessionId ?? string.Empty, StringComparison.Ordinal)));
            if (existing == null)
            {
                existing = new SafeSyncTombstone
                {
                    ClientSessionId = clientSessionId ?? string.Empty,
                    ServerSessionId = serverSessionId ?? string.Empty,
                    DeletedAt = DateTimeOffset.UtcNow
                };
                state.Tombstones.Add(existing);
            }

            if (!string.IsNullOrWhiteSpace(clientSessionId))
            {
                existing.ClientSessionId = clientSessionId;
            }

            if (!string.IsNullOrWhiteSpace(serverSessionId))
            {
                existing.ServerSessionId = serverSessionId;
            }

            existing.DeleteSource = source;
            existing.SyncStatus = status;
            existing.LastSafeErrorCode = errorCode ?? string.Empty;
            await tombstoneRepository.SaveAsync(state, cancellationToken);
        }

        private static SafeSyncRetryQueueSummary ToRetrySummary(SafeSyncRetryQueueState state)
        {
            var allEntries = state?.Entries ?? new List<SafeSyncRetryQueueEntry>();
            var entries = allEntries
                .OrderBy(entry => entry.NextAttemptAt)
                .ThenBy(entry => entry.CreatedAt)
                .Take(20)
                .ToList();
            var next = allEntries
                .Where(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Pending)
                .OrderBy(entry => entry.NextAttemptAt)
                .ThenBy(entry => entry.CreatedAt)
                .FirstOrDefault();
            return new SafeSyncRetryQueueSummary
            {
                PendingCount = allEntries.Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Pending),
                InProgressCount = allEntries.Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.InProgress),
                SucceededCount = allEntries.Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Succeeded),
                FailedCount = allEntries.Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Failed),
                PausedCount = allEntries.Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Paused),
                CancelledCount = allEntries.Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Cancelled),
                ConflictCount = allEntries.Count(entry => entry.Status == SafeSyncRetryQueueEntryStatus.Conflict),
                NextQueueEntryId = next?.QueueEntryId ?? string.Empty,
                NextAttemptAt = next?.NextAttemptAt,
                SafeEntries = entries
            };
        }

        private static SafeSyncConflictSummary ToConflictSummary(SafeSyncConflictState state)
        {
            var conflicts = (state?.Conflicts ?? new List<SafeSyncConflict>())
                .OrderByDescending(conflict => conflict.DetectedAt)
                .Take(20)
                .ToList();
            return new SafeSyncConflictSummary
            {
                UnresolvedCount = conflicts.Count(conflict => conflict.ResolutionStatus == SafeSyncConflictResolutionStatus.Unresolved),
                SafeConflicts = conflicts
            };
        }

        private static SafeSyncTombstoneSummary ToTombstoneSummary(SafeSyncTombstoneState state)
        {
            var tombstones = (state?.Tombstones ?? new List<SafeSyncTombstone>())
                .OrderByDescending(tombstone => tombstone.DeletedAt)
                .Take(20)
                .ToList();
            return new SafeSyncTombstoneSummary
            {
                PendingDeleteCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.PendingDelete),
                DeleteSyncedCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteSynced || tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteAlreadyApplied),
                DeleteFailedCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteFailed),
                DeleteResolvedCount = tombstones.Count(tombstone => tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteResolved || tombstone.SyncStatus == SafeSyncTombstoneStatus.DeleteCancelled),
                SafeTombstones = tombstones
            };
        }

        private static List<string> SafeClientSessionIds(SaveData saveData)
        {
            return (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .Select(session => session.SessionId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }

        private static SaveData FilterSaveDataForRetry(SaveData saveData, List<string> clientSessionIds)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            var selectedIds = new HashSet<string>(clientSessionIds ?? new List<string>(), StringComparer.Ordinal);
            return new SaveData
            {
                SchemaVersion = saveData.SchemaVersion,
                SaveVersion = saveData.SaveVersion,
                CharacterProfile = saveData.CharacterProfile,
                SyncState = saveData.SyncState,
                PrivacyPreferences = saveData.PrivacyPreferences,
                UserSettings = saveData.UserSettings,
                WorkSessionSummaries = (saveData.WorkSessionSummaries ?? new List<AgentWorkSession>())
                    .Where(session => selectedIds.Count == 0 || selectedIds.Contains(session.SessionId))
                    .ToList()
            };
        }

        private static SafeSyncSessionSafeSummary ToSafeLocalSummary(SaveData saveData, string clientSessionId)
        {
            var session = (saveData?.WorkSessionSummaries ?? new List<AgentWorkSession>())
                .FirstOrDefault(item => string.Equals(item.SessionId, clientSessionId, StringComparison.Ordinal));
            if (session == null)
            {
                return new SafeSyncSessionSafeSummary { ClientSessionId = clientSessionId ?? string.Empty };
            }

            return new SafeSyncSessionSafeSummary
            {
                ClientSessionId = session.SessionId ?? string.Empty,
                SourceProvider = string.IsNullOrWhiteSpace(session.SourceProvider) ? "UNKNOWN_AGENT" : session.SourceProvider,
                DayBucket = !string.IsNullOrWhiteSpace(session.AgentActivitySummary?.DayBucket)
                    ? session.AgentActivitySummary.DayBucket
                    : session.EndedAt.UtcDateTime.ToString("yyyy-MM-dd"),
                TimeBucket = "HOUR_" + session.StartedAt.UtcDateTime.Hour.ToString("00"),
                ActivityCategory = ToSafeWorkType(session.WorkType),
                ChangeCountBucket = ToSafeCountBucket(session.GitChangeSummary?.ChangedFileCountBucket),
                LineCountBucket = ToSafeLineBucket(session.GitChangeSummary?.AddedLineBucket),
                SessionCountBucket = ToSafeCountBucket(session.AgentActivitySummary?.SessionCountBucket),
                InteractionCountBucket = ToSafeCountBucket(session.AgentActivitySummary?.InteractionCountBucket),
                Confidence = ToSafeConfidence(session.Confidence),
                WarningCount = session.Warnings?.Count ?? 0,
                CategoryBucketSummary = ToSafeWorkType(session.WorkType) + ":ONE",
                ToolBucketSummary = ToolBucketSummary(session.AgentActivitySummary?.ToolUsageCategoryBuckets),
                LanguageBucketSummary = LanguageBucketSummary(session.AgentActivitySummary?.LanguageCategoryBuckets),
                AnalyzerVersion = FirstNonEmpty(session.AgentActivitySummary?.AnalyzerVersion, session.GitChangeSummary?.AnalyzerVersion),
                ParserVersion = session.ParserVersion ?? string.Empty,
                SchemaVersion = 1
            };
        }

        private static SafeSyncSessionSafeSummary ToSafeRemoteSummary(RemoteSafeSessionSummary remote)
        {
            remote = remote ?? new RemoteSafeSessionSummary();
            return new SafeSyncSessionSafeSummary
            {
                ClientSessionId = remote.ClientSessionId ?? string.Empty,
                ServerSessionId = remote.ServerSessionId ?? string.Empty,
                SourceProvider = string.IsNullOrWhiteSpace(remote.SourceProvider) ? "UNKNOWN_AGENT" : remote.SourceProvider,
                DayBucket = remote.DayBucket ?? string.Empty,
                TimeBucket = remote.TimeBucket ?? string.Empty,
                ActivityCategory = remote.ActivityCategory ?? string.Empty,
                ChangeCountBucket = remote.ChangeCountBucket ?? string.Empty,
                LineCountBucket = remote.LineCountBucket ?? string.Empty,
                SessionCountBucket = remote.SessionCountBucket ?? string.Empty,
                InteractionCountBucket = remote.InteractionCountBucket ?? string.Empty,
                Confidence = string.IsNullOrWhiteSpace(remote.Confidence) ? "LOW" : remote.Confidence,
                WarningCount = remote.WarningCount,
                CategoryBucketSummary = remote.CategoryBucketSummary ?? string.Empty,
                ToolBucketSummary = remote.ToolBucketSummary ?? string.Empty,
                LanguageBucketSummary = remote.LanguageBucketSummary ?? string.Empty,
                AnalyzerVersion = remote.AnalyzerVersion ?? string.Empty,
                ParserVersion = remote.ParserVersion ?? string.Empty,
                SchemaVersion = 1
            };
        }

        private static string ToolBucketSummary(IEnumerable<AgentToolUsageCategoryBucket> buckets)
        {
            return string.Join(",", (buckets ?? new List<AgentToolUsageCategoryBucket>())
                .OrderBy(bucket => ToSafeTool(bucket.Category), StringComparer.Ordinal)
                .Select(bucket => ToSafeTool(bucket.Category) + ":" + ToSafeCountBucket(bucket.CountBucket)));
        }

        private static string LanguageBucketSummary(IEnumerable<AgentLanguageCategoryBucket> buckets)
        {
            return string.Join(",", (buckets ?? new List<AgentLanguageCategoryBucket>())
                .OrderBy(bucket => ToSafeLanguage(bucket.Category), StringComparer.Ordinal)
                .Select(bucket => ToSafeLanguage(bucket.Category) + ":" + ToSafeCountBucket(bucket.CountBucket)));
        }

        private static string BucketSummary(IEnumerable<SafeBucketContractDto> buckets)
        {
            return string.Join(",", (buckets ?? new List<SafeBucketContractDto>())
                .OrderBy(bucket => bucket.Key ?? string.Empty, StringComparer.Ordinal)
                .Select(bucket => (bucket.Key ?? string.Empty) + ":" + (bucket.CountBucket ?? string.Empty)));
        }

        private static string FirstNonEmpty(string first, string second)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : (second ?? string.Empty);
        }

        private static string ToSafeConfidence(ProviderConfidence confidence)
        {
            switch (confidence)
            {
                case ProviderConfidence.High: return "HIGH";
                case ProviderConfidence.Medium: return "MEDIUM";
                case ProviderConfidence.Low: return "LOW";
                default: return "LOW";
            }
        }

        private static string ToSafeWorkType(WorkType workType)
        {
            switch (workType)
            {
                case WorkType.Feature: return "WORK_FEATURE";
                case WorkType.Bugfix: return "WORK_BUGFIX";
                case WorkType.Refactor: return "WORK_REFACTOR";
                case WorkType.Test: return "WORK_TEST";
                case WorkType.UIUX: return "WORK_UIUX";
                case WorkType.Docs: return "WORK_DOCS";
                case WorkType.Build: return "WORK_BUILD";
                case WorkType.Chore: return "WORK_CHORE";
                case WorkType.Research: return "WORK_RESEARCH";
                case WorkType.Mixed: return "WORK_MIXED";
                default: return "WORK_UNKNOWN";
            }
        }

        private static string ToSafeCountBucket(CountBucket? bucket)
        {
            if (!bucket.HasValue) return string.Empty;
            switch (bucket.Value)
            {
                case CountBucket.None: return "NONE";
                case CountBucket.One: return "ONE";
                case CountBucket.Small: return "FEW";
                case CountBucket.Medium:
                case CountBucket.Large: return "MANY";
                case CountBucket.Huge: return "MASSIVE";
                default: return string.Empty;
            }
        }

        private static string ToSafeLineBucket(LineChangeBucket? bucket)
        {
            if (!bucket.HasValue) return string.Empty;
            switch (bucket.Value)
            {
                case LineChangeBucket.None: return "NONE";
                case LineChangeBucket.Small: return "FEW";
                case LineChangeBucket.Medium:
                case LineChangeBucket.Large: return "MANY";
                case LineChangeBucket.Huge: return "MASSIVE";
                default: return string.Empty;
            }
        }

        private static string ToSafeTool(AgentToolUsageCategory category)
        {
            switch (category)
            {
                case AgentToolUsageCategory.CodeEditing: return "TOOL_EDIT";
                case AgentToolUsageCategory.ShellCommand: return "TOOL_SHELL";
                case AgentToolUsageCategory.TestRun: return "TOOL_TEST";
                case AgentToolUsageCategory.BuildRun: return "TOOL_BUILD";
                case AgentToolUsageCategory.FileNavigation: return "TOOL_NAVIGATION";
                case AgentToolUsageCategory.Search: return "TOOL_SEARCH";
                default: return "TOOL_UNKNOWN";
            }
        }

        private static string ToSafeLanguage(AgentLanguageCategory category)
        {
            switch (category)
            {
                case AgentLanguageCategory.CSharp: return "LANG_CSHARP";
                case AgentLanguageCategory.JavaScript: return "LANG_JAVASCRIPT";
                case AgentLanguageCategory.TypeScript: return "LANG_TYPESCRIPT";
                case AgentLanguageCategory.Python: return "LANG_PYTHON";
                case AgentLanguageCategory.Web: return "LANG_WEB";
                case AgentLanguageCategory.Config: return "LANG_CONFIG";
                case AgentLanguageCategory.Docs: return "LANG_DOCS";
                case AgentLanguageCategory.Test: return "LANG_TEST";
                case AgentLanguageCategory.Shell: return "LANG_SHELL";
                default: return "LANG_UNKNOWN";
            }
        }

        private static bool SameSafeSummary(SafeSyncSessionSafeSummary left, SafeSyncSessionSafeSummary right)
        {
            left = left ?? new SafeSyncSessionSafeSummary();
            right = right ?? new SafeSyncSessionSafeSummary();
            return string.Equals(left.ClientSessionId, right.ClientSessionId, StringComparison.Ordinal) &&
                   string.Equals(left.SourceProvider, right.SourceProvider, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.DayBucket, right.DayBucket, StringComparison.Ordinal) &&
                   string.Equals(left.ActivityCategory, right.ActivityCategory, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.ChangeCountBucket, right.ChangeCountBucket, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.LineCountBucket, right.LineCountBucket, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.SessionCountBucket, right.SessionCountBucket, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.InteractionCountBucket, right.InteractionCountBucket, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(left.Confidence, right.Confidence, StringComparison.OrdinalIgnoreCase) &&
                   left.WarningCount == right.WarningCount;
        }

        private static bool SameIds(List<string> left, List<string> right)
        {
            left = (left ?? new List<string>()).OrderBy(id => id, StringComparer.Ordinal).ToList();
            right = (right ?? new List<string>()).OrderBy(id => id, StringComparer.Ordinal).ToList();
            return left.SequenceEqual(right, StringComparer.Ordinal);
        }

        private static bool IsNotFound(string errorCode)
        {
            return string.Equals(errorCode, SafeSyncApiError.NotFound, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, "HTTP_404", StringComparison.OrdinalIgnoreCase);
        }

        private SafeSyncResult FailResultFromApi<T>(SafeSyncApiResult<T> response)
        {
            return SafeSyncResult.Failure(ToStatus(response.ErrorCode), response.ErrorCode, response.ErrorMessage);
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
                string.Equals(errorCode, SafeSyncApiError.Timeout, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, SafeSyncApiError.NetworkTimeout, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, SafeSyncApiError.TemporaryServerError, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, "HTTP_503", StringComparison.OrdinalIgnoreCase))
            {
                return SafeSyncStatus.ServerUnavailable;
            }

            if (string.Equals(errorCode, SafeSyncApiError.PrivacyGuardBlockedPayload, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, SafeSyncApiError.UnsafePayload, StringComparison.OrdinalIgnoreCase))
            {
                return SafeSyncStatus.PrivacyBlocked;
            }

            if (string.Equals(errorCode, SafeSyncApiError.ConflictDetected, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, SafeSyncApiError.ValidationConflict, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, "HTTP_409", StringComparison.OrdinalIgnoreCase))
            {
                return SafeSyncStatus.ConflictDetected;
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
                ActivityCategory = NormalizeRemoteActivityCategory(session.ActivityCategory),
                ChangeCountBucket = NormalizeRemoteCountBucket(session.ChangeCountBucket),
                LineCountBucket = NormalizeRemoteLineBucket(session.LineCountBucket),
                SessionCountBucket = NormalizeRemoteCountBucket(session.SessionCountBucket),
                InteractionCountBucket = NormalizeRemoteCountBucket(session.InteractionCountBucket),
                WarningCount = session.WarningIds?.Count ?? 0,
                CategoryBucketSummary = BucketSummary(session.CategoryBuckets),
                ToolBucketSummary = BucketSummary(session.ToolBuckets),
                LanguageBucketSummary = BucketSummary(session.LanguageBuckets),
                AnalyzerVersion = session.AnalyzerVersion ?? string.Empty,
                ParserVersion = session.ParserVersion ?? string.Empty,
                SchemaVersion = session.AggregateSchemaVersion <= 0 ? 1 : session.AggregateSchemaVersion
            };
        }

        private static RemoteSafeActivitySessionDto CloneRemoteSession(RemoteSafeActivitySessionDto session)
        {
            if (session == null)
            {
                return null;
            }

            return new RemoteSafeActivitySessionDto
            {
                Id = session.Id ?? string.Empty,
                ClientSessionId = session.ClientSessionId ?? string.Empty,
                SourceProvider = session.SourceProvider ?? "UNKNOWN_AGENT",
                DayBucket = session.DayBucket ?? string.Empty,
                TimeBucket = session.TimeBucket ?? string.Empty,
                Confidence = session.Confidence ?? "LOW",
                WarningIds = (session.WarningIds ?? new List<string>()).ToList(),
                AnalyzerVersion = session.AnalyzerVersion ?? string.Empty,
                ParserVersion = session.ParserVersion ?? string.Empty,
                AggregateSchemaVersion = session.AggregateSchemaVersion <= 0 ? 1 : session.AggregateSchemaVersion,
                HashedRepositoryId = session.HashedRepositoryId ?? string.Empty,
                ChangeCountBucket = session.ChangeCountBucket ?? string.Empty,
                LineCountBucket = session.LineCountBucket ?? string.Empty,
                CommitCountBucket = session.CommitCountBucket ?? string.Empty,
                SessionCountBucket = session.SessionCountBucket ?? string.Empty,
                InteractionCountBucket = session.InteractionCountBucket ?? string.Empty,
                ActivityCategory = session.ActivityCategory ?? string.Empty,
                DurationBucket = session.DurationBucket ?? string.Empty,
                CategoryBuckets = CloneBuckets(session.CategoryBuckets),
                LanguageBuckets = CloneBuckets(session.LanguageBuckets),
                ToolBuckets = CloneBuckets(session.ToolBuckets),
                CreatedAt = session.CreatedAt ?? string.Empty,
                UpdatedAt = session.UpdatedAt ?? string.Empty
            };
        }

        private static RemoteSafeActivitySessionDto ToRemoteSafeSession(SafeSyncSessionSafeSummary summary, RemoteSafeActivitySessionDto template)
        {
            summary = summary ?? new SafeSyncSessionSafeSummary();
            return new RemoteSafeActivitySessionDto
            {
                Id = FirstNonEmpty(summary.ServerSessionId, template?.Id),
                ClientSessionId = summary.ClientSessionId ?? string.Empty,
                SourceProvider = string.IsNullOrWhiteSpace(summary.SourceProvider) ? "UNKNOWN_AGENT" : summary.SourceProvider,
                DayBucket = summary.DayBucket ?? string.Empty,
                TimeBucket = summary.TimeBucket ?? string.Empty,
                Confidence = summary.Confidence ?? "LOW",
                WarningIds = (template?.WarningIds ?? new List<string>()).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).Take(25).ToList(),
                AnalyzerVersion = summary.AnalyzerVersion ?? string.Empty,
                ParserVersion = summary.ParserVersion ?? string.Empty,
                AggregateSchemaVersion = 1,
                HashedRepositoryId = template?.HashedRepositoryId ?? string.Empty,
                ChangeCountBucket = summary.ChangeCountBucket ?? string.Empty,
                LineCountBucket = summary.LineCountBucket ?? string.Empty,
                CommitCountBucket = template?.CommitCountBucket ?? string.Empty,
                SessionCountBucket = summary.SessionCountBucket ?? string.Empty,
                InteractionCountBucket = summary.InteractionCountBucket ?? string.Empty,
                ActivityCategory = summary.ActivityCategory ?? string.Empty,
                DurationBucket = template?.DurationBucket ?? string.Empty,
                CategoryBuckets = BucketsFromSummary(summary.CategoryBucketSummary),
                LanguageBuckets = BucketsFromSummary(summary.LanguageBucketSummary),
                ToolBuckets = BucketsFromSummary(summary.ToolBucketSummary),
                CreatedAt = template?.CreatedAt ?? string.Empty,
                UpdatedAt = template?.UpdatedAt ?? string.Empty
            };
        }

        private static List<SafeBucketContractDto> CloneBuckets(List<SafeBucketContractDto> buckets)
        {
            return (buckets ?? new List<SafeBucketContractDto>())
                .Select(bucket => new SafeBucketContractDto
                {
                    Key = bucket?.Key ?? string.Empty,
                    CountBucket = bucket?.CountBucket ?? string.Empty
                })
                .ToList();
        }

        private static List<SafeBucketContractDto> BucketsFromSummary(string summary)
        {
            return (summary ?? string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split(new[] { ':' }, 2))
                .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                .Select(parts => new SafeBucketContractDto { Key = parts[0].Trim(), CountBucket = parts[1].Trim() })
                .Take(25)
                .ToList();
        }

        private static bool IsUnsafeRetryBlock(string errorCode)
        {
            return string.Equals(errorCode, SafeSyncApiError.PrivacyGuardBlockedPayload, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, SafeSyncApiError.UnsafePayload, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, SafeSyncApiError.ValidationFailed, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, SafeSyncApiError.UnknownSchemaVersion, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, SafeSyncApiError.KeepRemoteUnsafeRejected, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, SafeSyncApiError.RemoteSessionValidationFailed, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, SafeSyncApiError.InvalidBucketValue, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRemoteActivityCategory(string value)
        {
            switch (value)
            {
                case "Feature": return "WORK_FEATURE";
                case "Bugfix": return "WORK_BUGFIX";
                case "Refactor": return "WORK_REFACTOR";
                case "Test": return "WORK_TEST";
                case "UIUX": return "WORK_UIUX";
                case "Docs": return "WORK_DOCS";
                case "Build": return "WORK_BUILD";
                case "Chore": return "WORK_CHORE";
                case "Research": return "WORK_RESEARCH";
                case "Mixed": return "WORK_MIXED";
                case "Unknown": return "WORK_UNKNOWN";
                default: return value ?? string.Empty;
            }
        }

        private static string NormalizeRemoteCountBucket(string value)
        {
            switch (value)
            {
                case "None": return "NONE";
                case "One": return "ONE";
                case "Small": return "FEW";
                case "Medium":
                case "Large": return "MANY";
                case "Huge": return "MASSIVE";
                default: return value ?? string.Empty;
            }
        }

        private static string NormalizeRemoteLineBucket(string value)
        {
            switch (value)
            {
                case "None": return "NONE";
                case "Small": return "FEW";
                case "Medium":
                case "Large": return "MANY";
                case "Huge": return "MASSIVE";
                default: return value ?? string.Empty;
            }
        }
    }
}
