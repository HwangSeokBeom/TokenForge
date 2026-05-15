using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    public sealed class SafeRemoteSessionApplyResult
    {
        public bool IsApplied { get; set; }
        public bool IsMarkerOnly { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public sealed class SafeRemoteSessionApplicator
    {
        private readonly ILocalSaveDataRepository repository;
        private readonly RemoteSafeSessionValidator validator;
        private readonly SafeSyncRemoteToLocalMapper mapper;
        private readonly PrivacySanitizer privacySanitizer;

        public SafeRemoteSessionApplicator(
            ILocalSaveDataRepository repository,
            RemoteSafeSessionValidator validator = null,
            SafeSyncRemoteToLocalMapper mapper = null,
            PrivacySanitizer privacySanitizer = null)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.validator = validator ?? new RemoteSafeSessionValidator(this.privacySanitizer);
            this.mapper = mapper ?? new SafeSyncRemoteToLocalMapper();
        }

        public async Task<SafeRemoteSessionApplyResult> ApplyAsync(RemoteSafeActivitySessionDto remote, CancellationToken cancellationToken = default)
        {
            if (remote == null)
            {
                return MarkerOnly(SafeSyncApiError.KeepRemoteMarkerOnly);
            }

            var validation = validator.Validate(remote);
            if (!validation.IsSuccess)
            {
                return Failed(validation.ErrorCode);
            }

            AgentWorkSession mapped;
            try
            {
                mapped = mapper.ToLocalSession(remote);
            }
            catch (Exception)
            {
                return Failed(SafeSyncApiError.RemoteSessionApplyFailed);
            }

            if (string.IsNullOrWhiteSpace(mapped.SessionId))
            {
                return MarkerOnly(SafeSyncApiError.KeepRemoteMarkerOnly);
            }

            var privacy = privacySanitizer.ValidateNoForbiddenFields(mapped);
            if (!privacy.IsSuccess)
            {
                return Failed(SafeSyncApiError.KeepRemoteUnsafeRejected);
            }

            var saveData = await repository.LoadAsync(cancellationToken) ?? SaveData.CreateDefault();
            saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new List<AgentWorkSession>();
            var existing = saveData.WorkSessionSummaries
                .FirstOrDefault(session => string.Equals(session.SessionId, mapped.SessionId, StringComparison.Ordinal));
            if (existing == null)
            {
                saveData.WorkSessionSummaries.Add(mapped);
            }
            else
            {
                var index = saveData.WorkSessionSummaries.IndexOf(existing);
                saveData.WorkSessionSummaries[index] = mapped;
            }

            if (saveData.SyncState == null)
            {
                saveData.SyncState = new SyncState();
            }

            saveData.SyncState.LastSyncAt = DateTimeOffset.UtcNow;
            saveData.SyncState.SyncVersion = 1;
            saveData.SyncState.LastErrorCode = string.Empty;
            var save = await repository.SaveAsync(saveData, cancellationToken);
            if (!save.IsSuccess)
            {
                return Failed(SafeSyncApiError.RemoteSessionApplyFailed);
            }

            return new SafeRemoteSessionApplyResult
            {
                IsApplied = true,
                ErrorCode = SafeSyncApiError.KeepRemoteApplied,
                ErrorMessage = SafeSyncApiError.ToSafeMessage(SafeSyncApiError.KeepRemoteApplied)
            };
        }

        private static SafeRemoteSessionApplyResult MarkerOnly(string code)
        {
            return new SafeRemoteSessionApplyResult
            {
                IsMarkerOnly = true,
                ErrorCode = code,
                ErrorMessage = SafeSyncApiError.ToSafeMessage(code)
            };
        }

        private static SafeRemoteSessionApplyResult Failed(string code)
        {
            return new SafeRemoteSessionApplyResult
            {
                IsApplied = false,
                IsMarkerOnly = false,
                ErrorCode = string.IsNullOrWhiteSpace(code) ? SafeSyncApiError.RemoteSessionApplyFailed : code,
                ErrorMessage = SafeSyncApiError.ToSafeMessage(string.IsNullOrWhiteSpace(code) ? SafeSyncApiError.RemoteSessionApplyFailed : code)
            };
        }
    }
}
