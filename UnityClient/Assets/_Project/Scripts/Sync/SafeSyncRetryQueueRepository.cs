using System;
using System.IO;
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
    public sealed class SafeSyncRetryQueueRepository : ISafeSyncRetryQueueRepository
    {
        public const int CurrentSchemaVersion = 1;
        public const string FileName = "tokenforge-sync-retry-queue.local.json";

        private readonly string filePath;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly JsonSerializerSettings serializerSettings;

        public SafeSyncRetryQueueRepository(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            var directory = string.IsNullOrWhiteSpace(saveDirectory) ? GetDefaultSaveDirectory() : saveDirectory;
            Directory.CreateDirectory(directory);
            filePath = Path.Combine(directory, FileName);
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new StringEnumConverter() }
            };
        }

        public string FilePath => filePath;
        public string BackupFilePath => filePath + ".bak";
        public string CorruptFilePath => filePath + ".corrupt";
        public string UnsupportedSchemaFilePath => filePath + ".unsupported-schema";
        public string UnsafeFilePath => filePath + ".unsafe";

        public async Task<SafeSyncRetryQueueState> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                return new SafeSyncRetryQueueState();
            }

            cancellationToken.ThrowIfCancellationRequested();
            string json;
            using (var reader = new StreamReader(filePath, Encoding.UTF8))
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
                PreserveRecoveryCopy(CorruptFilePath);
                return new SafeSyncRetryQueueState();
            }

            if (!privacySanitizer.ValidateNoForbiddenFields(root).IsSuccess)
            {
                PreserveRecoveryCopy(UnsafeFilePath);
                return new SafeSyncRetryQueueState();
            }

            var schemaVersion = ReadSchemaVersion(root);
            if (schemaVersion > CurrentSchemaVersion)
            {
                PreserveRecoveryCopy(UnsupportedSchemaFilePath);
                return new SafeSyncRetryQueueState();
            }

            try
            {
                var state = root.ToObject<SafeSyncRetryQueueState>(JsonSerializer.Create(serializerSettings)) ?? new SafeSyncRetryQueueState();
                return Migrate(state);
            }
            catch (JsonException)
            {
                PreserveRecoveryCopy(CorruptFilePath);
                return new SafeSyncRetryQueueState();
            }
        }

        public async Task<Result> SaveAsync(SafeSyncRetryQueueState state, CancellationToken cancellationToken = default)
        {
            state = Migrate(state);
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
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            var tempPath = filePath + ".tmp";
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(json);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(filePath))
            {
                File.Replace(tempPath, filePath, BackupFilePath, true);
            }
            else
            {
                File.Move(tempPath, filePath);
            }

            return Result.Success();
        }

        private static SafeSyncRetryQueueState Migrate(SafeSyncRetryQueueState state)
        {
            state = state ?? new SafeSyncRetryQueueState();
            state.SchemaVersion = CurrentSchemaVersion;
            state.Entries = state.Entries ?? new System.Collections.Generic.List<SafeSyncRetryQueueEntry>();
            foreach (var entry in state.Entries)
            {
                entry.SchemaVersion = CurrentSchemaVersion;
                entry.QueueEntryId = string.IsNullOrWhiteSpace(entry.QueueEntryId) ? Guid.NewGuid().ToString("N") : entry.QueueEntryId;
                entry.ClientSessionIds = entry.ClientSessionIds ?? new System.Collections.Generic.List<string>();
                entry.MaxAttempts = entry.MaxAttempts <= 0 ? SafeSyncRetryPolicy.DefaultMaxAttempts : entry.MaxAttempts;
                if (entry.CreatedAt == default) entry.CreatedAt = DateTimeOffset.UtcNow;
                if (entry.UpdatedAt == default) entry.UpdatedAt = entry.CreatedAt;
                if (entry.NextAttemptAt == default) entry.NextAttemptAt = entry.UpdatedAt;
            }

            return state;
        }

        private static int ReadSchemaVersion(JObject root)
        {
            var token = root["schemaVersion"] ?? root["SchemaVersion"];
            return token != null && token.Type == JTokenType.Integer && int.TryParse(token.ToString(), out var parsed)
                ? parsed
                : CurrentSchemaVersion;
        }

        private void PreserveRecoveryCopy(string recoveryPath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Copy(filePath, recoveryPath, true);
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
