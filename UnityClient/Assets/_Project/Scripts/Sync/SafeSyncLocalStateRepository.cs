using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using TokenForge.Client.Common;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Sync
{
    [Serializable]
    public sealed class SafeSyncLocalSessionMapping
    {
        public string ClientSessionId { get; set; } = string.Empty;
        public string ServerSessionId { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public int SchemaVersion { get; set; } = 1;
    }

    [Serializable]
    public sealed class SafeSyncLocalState
    {
        public int SchemaVersion { get; set; } = 1;
        public List<SafeSyncLocalSessionMapping> SessionMappings { get; set; } = new List<SafeSyncLocalSessionMapping>();
    }

    public sealed class SafeSyncLocalStateRepository
    {
        public const string FileName = "tokenforge-sync-local-state.local.json";
        private readonly SafeSyncLocalJsonStore<SafeSyncLocalState> store;

        public SafeSyncLocalStateRepository(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            store = new SafeSyncLocalJsonStore<SafeSyncLocalState>(saveDirectory, FileName, () => new SafeSyncLocalState(), privacySanitizer);
        }

        public string FilePath => store.FilePath;
        public Task<SafeSyncLocalState> LoadAsync(CancellationToken cancellationToken = default) => store.LoadAsync(cancellationToken);

        public Task<Result> SaveAsync(SafeSyncLocalState state, CancellationToken cancellationToken = default)
        {
            state = state ?? new SafeSyncLocalState();
            state.SchemaVersion = 1;
            state.SessionMappings = (state.SessionMappings ?? new List<SafeSyncLocalSessionMapping>())
                .Where(mapping => !string.IsNullOrWhiteSpace(mapping.ClientSessionId) && !string.IsNullOrWhiteSpace(mapping.ServerSessionId))
                .GroupBy(mapping => mapping.ClientSessionId, StringComparer.Ordinal)
                .Select(group =>
                {
                    var latest = group.OrderByDescending(mapping => mapping.UpdatedAt).First();
                    latest.SchemaVersion = 1;
                    return latest;
                })
                .OrderBy(mapping => mapping.ClientSessionId, StringComparer.Ordinal)
                .ToList();

            return store.SaveAsync(state, cancellationToken);
        }
    }

    public sealed class SafeSyncConflictRepository
    {
        public const string FileName = "tokenforge-sync-conflicts.local.json";
        private readonly SafeSyncLocalJsonStore<SafeSyncConflictState> store;

        public SafeSyncConflictRepository(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            store = new SafeSyncLocalJsonStore<SafeSyncConflictState>(saveDirectory, FileName, () => new SafeSyncConflictState(), privacySanitizer);
        }

        public string FilePath => store.FilePath;
        public Task<SafeSyncConflictState> LoadAsync(CancellationToken cancellationToken = default) => store.LoadAsync(cancellationToken);
        public Task<Result> SaveAsync(SafeSyncConflictState state, CancellationToken cancellationToken = default)
        {
            state = state ?? new SafeSyncConflictState();
            state.SchemaVersion = 1;
            state.Conflicts = state.Conflicts ?? new System.Collections.Generic.List<SafeSyncConflict>();
            foreach (var conflict in state.Conflicts)
            {
                conflict.SchemaVersion = 1;
                conflict.ConflictId = string.IsNullOrWhiteSpace(conflict.ConflictId) ? Guid.NewGuid().ToString("N") : conflict.ConflictId;
                conflict.SafeLocalSummary = conflict.SafeLocalSummary ?? new SafeSyncSessionSafeSummary();
                conflict.SafeRemoteSummary = conflict.SafeRemoteSummary ?? new SafeSyncSessionSafeSummary();
                conflict.SafeDiffSummary = conflict.SafeDiffSummary ?? new SafeSessionDiffSummary();
            }

            return store.SaveAsync(state, cancellationToken);
        }
    }

    public sealed class SafeSyncTombstoneRepository
    {
        public const string FileName = "tokenforge-sync-tombstones.local.json";
        private readonly SafeSyncLocalJsonStore<SafeSyncTombstoneState> store;

        public SafeSyncTombstoneRepository(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            store = new SafeSyncLocalJsonStore<SafeSyncTombstoneState>(saveDirectory, FileName, () => new SafeSyncTombstoneState(), privacySanitizer);
        }

        public string FilePath => store.FilePath;
        public Task<SafeSyncTombstoneState> LoadAsync(CancellationToken cancellationToken = default) => store.LoadAsync(cancellationToken);
        public Task<Result> SaveAsync(SafeSyncTombstoneState state, CancellationToken cancellationToken = default)
        {
            state = state ?? new SafeSyncTombstoneState();
            state.SchemaVersion = 1;
            state.Tombstones = state.Tombstones ?? new System.Collections.Generic.List<SafeSyncTombstone>();
            foreach (var tombstone in state.Tombstones)
            {
                tombstone.SchemaVersion = 1;
                tombstone.TombstoneId = string.IsNullOrWhiteSpace(tombstone.TombstoneId) ? Guid.NewGuid().ToString("N") : tombstone.TombstoneId;
            }

            return store.SaveAsync(state, cancellationToken);
        }
    }

    public sealed class SafeConflictAuditRepository
    {
        public const string FileName = "tokenforge-sync-conflict-audit.local.json";
        public const int RetentionLimit = 200;

        private readonly SafeSyncLocalJsonStore<SafeConflictAuditState> store;

        public SafeConflictAuditRepository(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            store = new SafeSyncLocalJsonStore<SafeConflictAuditState>(saveDirectory, FileName, () => new SafeConflictAuditState(), privacySanitizer);
        }

        public string FilePath => store.FilePath;
        public Task<SafeConflictAuditState> LoadAsync(CancellationToken cancellationToken = default) => store.LoadAsync(cancellationToken);

        public Task<Result> SaveAsync(SafeConflictAuditState state, CancellationToken cancellationToken = default)
        {
            state = state ?? new SafeConflictAuditState();
            state.SchemaVersion = 1;
            state.Entries = (state.Entries ?? new List<SafeConflictAuditEntry>())
                .Where(entry => entry != null)
                .OrderByDescending(entry => entry.CreatedAt)
                .ThenBy(entry => entry.AuditEntryId, StringComparer.Ordinal)
                .Take(RetentionLimit)
                .Select(entry =>
                {
                    entry.SchemaVersion = 1;
                    entry.AuditEntryId = string.IsNullOrWhiteSpace(entry.AuditEntryId) ? Guid.NewGuid().ToString("N") : entry.AuditEntryId;
                    entry.ConflictId = entry.ConflictId ?? string.Empty;
                    entry.Action = entry.Action ?? string.Empty;
                    entry.ResultStatus = entry.ResultStatus ?? string.Empty;
                    entry.SafeDiffFieldNames = (entry.SafeDiffFieldNames ?? new List<string>())
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(item => item, StringComparer.Ordinal)
                        .Take(32)
                        .ToList();
                    entry.WarningIds = (entry.WarningIds ?? new List<string>())
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(item => item, StringComparer.Ordinal)
                        .Take(32)
                        .ToList();
                    entry.SafeLocalSummaryBefore = entry.SafeLocalSummaryBefore ?? new SafeSyncSessionSafeSummary();
                    entry.SafeRemoteSummaryBefore = entry.SafeRemoteSummaryBefore ?? new SafeSyncSessionSafeSummary();
                    entry.UserMessageCode = entry.UserMessageCode ?? string.Empty;
                    entry.QueuedRetryEntryId = entry.QueuedRetryEntryId ?? string.Empty;
                    return entry;
                })
                .ToList();

            return store.SaveAsync(state, cancellationToken);
        }
    }

    internal sealed class SafeSyncLocalJsonStore<T> where T : class
    {
        private const int CurrentSchemaVersion = 1;

        private readonly Func<T> defaultFactory;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly JsonSerializerSettings serializerSettings;

        public SafeSyncLocalJsonStore(string saveDirectory, string fileName, Func<T> defaultFactory, PrivacySanitizer privacySanitizer)
        {
            var directory = string.IsNullOrWhiteSpace(saveDirectory) ? GetDefaultSaveDirectory() : saveDirectory;
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory, fileName);
            this.defaultFactory = defaultFactory;
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new StringEnumConverter() }
            };
        }

        public string FilePath { get; }
        private string BackupFilePath => FilePath + ".bak";

        public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(FilePath))
            {
                return defaultFactory();
            }

            string json;
            using (var reader = new StreamReader(FilePath, Encoding.UTF8))
            {
                json = await reader.ReadToEndAsync();
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException)
            {
                PreserveRecoveryCopy(FilePath + ".corrupt");
                return defaultFactory();
            }

            if (!privacySanitizer.ValidateNoForbiddenFields(root).IsSuccess)
            {
                PreserveRecoveryCopy(FilePath + ".unsafe");
                return defaultFactory();
            }

            var schemaToken = root["schemaVersion"] ?? root["SchemaVersion"];
            var schemaVersion = schemaToken != null && schemaToken.Type == JTokenType.Integer && int.TryParse(schemaToken.ToString(), out var parsed)
                ? parsed
                : CurrentSchemaVersion;
            if (schemaVersion > CurrentSchemaVersion)
            {
                PreserveRecoveryCopy(FilePath + ".unsupported-schema");
                return defaultFactory();
            }

            try
            {
                return root.ToObject<T>(JsonSerializer.Create(serializerSettings)) ?? defaultFactory();
            }
            catch (JsonException)
            {
                PreserveRecoveryCopy(FilePath + ".corrupt");
                return defaultFactory();
            }
        }

        public async Task<Result> SaveAsync(T state, CancellationToken cancellationToken = default)
        {
            var validation = privacySanitizer.ValidateNoForbiddenFields(state);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var json = JsonConvert.SerializeObject(state, serializerSettings);
            var jsonValidation = privacySanitizer.ValidateNoForbiddenFields(json);
            if (!jsonValidation.IsSuccess)
            {
                return jsonValidation;
            }

            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var tempPath = FilePath + ".tmp";
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(json);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(FilePath))
            {
                File.Replace(tempPath, FilePath, BackupFilePath, true);
            }
            else
            {
                File.Move(tempPath, FilePath);
            }

            return Result.Success();
        }

        private void PreserveRecoveryCopy(string recoveryPath)
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    File.Copy(FilePath, recoveryPath, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string GetDefaultSaveDirectory()
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.Application.persistentDataPath;
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TokenForge");
#endif
        }
    }
}
