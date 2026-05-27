using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Persistence
{
    public enum ApprovedLocationSourceType
    {
        Git = 0,
        Claude = 1,
        Codex = 2,
        UnknownAuto = 3,
        Cursor = 4,
        ClaudeCode = 5,
        GitHubCopilot = 6,
        Manual = 7,
        GeminiCli = 8
    }

    [Serializable]
    public sealed class ApprovedLocationEntry
    {
        public string LocalId { get; set; } = Guid.NewGuid().ToString("N");
        public string DisplayAlias { get; set; } = "Approved location";
        public ApprovedLocationSourceType SourceType { get; set; } = ApprovedLocationSourceType.UnknownAuto;
        public string LocalPath { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
        public bool Enabled { get; set; } = true;
    }

    [Serializable]
    public sealed class ApprovedLocationSettings
    {
        public const int CurrentSchemaVersion = 1;
        public const int CurrentVersion = CurrentSchemaVersion;

        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;
        public int Version { get; set; } = CurrentVersion;
        public List<ApprovedLocationEntry> Locations { get; set; } = new List<ApprovedLocationEntry>();

        public static ApprovedLocationSettings CreateDefault()
        {
            return new ApprovedLocationSettings();
        }
    }

    public interface IApprovedLocationSettingsRepository
    {
        Task<ApprovedLocationSettings> LoadAsync(CancellationToken cancellationToken = default);
        Task<Result> SaveAsync(ApprovedLocationSettings settings, CancellationToken cancellationToken = default);
        Task<Result<ApprovedLocationEntry>> AddOrUpdateAsync(ApprovedLocationEntry entry, CancellationToken cancellationToken = default);
        Task<Result> SetEnabledAsync(string localId, bool enabled, CancellationToken cancellationToken = default);
        Task<Result> RemoveAsync(string localId, CancellationToken cancellationToken = default);
        Task<Result> ClearAsync(CancellationToken cancellationToken = default);
    }

    public sealed class ApprovedLocationSettingsRepository : IApprovedLocationSettingsRepository
    {
        public const string SettingsFileName = "tokenforge-approved-locations.local.json";

        private const int MaxLocations = 100;
        private const int MaxAliasLength = 64;
        private readonly string settingsFilePath;
        private readonly JsonSerializerSettings serializerSettings;
        private readonly ForbiddenFieldDetector detector = new ForbiddenFieldDetector();

        public ApprovedLocationSettingsRepository(string settingsDirectory = null)
        {
            var directory = string.IsNullOrWhiteSpace(settingsDirectory) ? GetDefaultSettingsDirectory() : settingsDirectory;
            Directory.CreateDirectory(directory);
            settingsFilePath = Path.Combine(directory, SettingsFileName);
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                Formatting = Formatting.Indented
            };
        }

        public string SettingsFilePath => settingsFilePath;
        public string BackupFilePath => settingsFilePath + ".bak";
        public string CorruptFilePath => settingsFilePath + ".corrupt";
        public string UnsupportedSchemaFilePath => settingsFilePath + ".unsupported-schema";

        public async Task<ApprovedLocationSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(settingsFilePath))
            {
                return ApprovedLocationSettings.CreateDefault();
            }

            cancellationToken.ThrowIfCancellationRequested();
            string json;
            using (var reader = new StreamReader(settingsFilePath, Encoding.UTF8))
            {
                json = await reader.ReadToEndAsync();
            }

            cancellationToken.ThrowIfCancellationRequested();
            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException)
            {
                PreserveRecoveryCopy(CorruptFilePath);
                return ApprovedLocationSettings.CreateDefault();
            }

            var schemaVersion = ReadSchemaVersion(root, "schemaVersion", "Version");
            if (schemaVersion > ApprovedLocationSettings.CurrentSchemaVersion)
            {
                PreserveRecoveryCopy(UnsupportedSchemaFilePath);
                return ApprovedLocationSettings.CreateDefault();
            }

            try
            {
                ReplaceLegacyProviderNames(root);
                return Normalize(root.ToObject<ApprovedLocationSettings>(JsonSerializer.Create(serializerSettings)) ?? ApprovedLocationSettings.CreateDefault());
            }
            catch (JsonException)
            {
                PreserveRecoveryCopy(CorruptFilePath);
                return ApprovedLocationSettings.CreateDefault();
            }
        }

        public async Task<Result> SaveAsync(ApprovedLocationSettings settings, CancellationToken cancellationToken = default)
        {
            settings = Normalize(settings ?? ApprovedLocationSettings.CreateDefault());
            var validation = Validate(settings);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var json = JsonConvert.SerializeObject(settings, serializerSettings);
            cancellationToken.ThrowIfCancellationRequested();

            var directory = Path.GetDirectoryName(settingsFilePath);
            Directory.CreateDirectory(directory);
            var tempPath = settingsFilePath + ".tmp";
            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(json);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(settingsFilePath))
            {
                File.Replace(tempPath, settingsFilePath, BackupFilePath, true);
            }
            else
            {
                File.Move(tempPath, settingsFilePath);
            }

            return Result.Success();
        }

        public async Task<Result<ApprovedLocationEntry>> AddOrUpdateAsync(ApprovedLocationEntry entry, CancellationToken cancellationToken = default)
        {
            var normalizedEntry = NormalizeEntry(entry);
            var validation = ValidateEntry(normalizedEntry);
            if (!validation.IsSuccess)
            {
                return Result<ApprovedLocationEntry>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            var settings = await LoadAsync(cancellationToken);
            var existingIndex = settings.Locations.FindIndex(item => item.LocalId == normalizedEntry.LocalId);
            if (existingIndex >= 0)
            {
                normalizedEntry.CreatedAt = settings.Locations[existingIndex].CreatedAt;
                normalizedEntry.UpdatedAt = DateTimeOffset.UtcNow;
                settings.Locations[existingIndex] = normalizedEntry;
            }
            else
            {
                settings.Locations.Add(normalizedEntry);
            }

            var saveResult = await SaveAsync(settings, cancellationToken);
            return saveResult.IsSuccess
                ? Result<ApprovedLocationEntry>.Success(normalizedEntry)
                : Result<ApprovedLocationEntry>.Failure(saveResult.ErrorCode, saveResult.ErrorMessage);
        }

        public async Task<Result> SetEnabledAsync(string localId, bool enabled, CancellationToken cancellationToken = default)
        {
            var settings = await LoadAsync(cancellationToken);
            var entry = settings.Locations.FirstOrDefault(item => item.LocalId == localId);
            if (entry == null)
            {
                return Result.Failure("approved_location_not_found", "Approved location was not found.");
            }

            entry.Enabled = enabled;
            entry.UpdatedAt = DateTimeOffset.UtcNow;
            return await SaveAsync(settings, cancellationToken);
        }

        public async Task<Result> RemoveAsync(string localId, CancellationToken cancellationToken = default)
        {
            var settings = await LoadAsync(cancellationToken);
            var removed = settings.Locations.RemoveAll(item => item.LocalId == localId);
            if (removed <= 0)
            {
                return Result.Failure("approved_location_not_found", "Approved location was not found.");
            }

            return await SaveAsync(settings, cancellationToken);
        }

        public Task<Result> ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteIfExists(settingsFilePath);
            DeleteIfExists(BackupFilePath);
            DeleteIfExists(CorruptFilePath);
            DeleteIfExists(UnsupportedSchemaFilePath);
            DeleteIfExists(settingsFilePath + ".tmp");
            return Task.FromResult(Result.Success());
        }

        private ApprovedLocationSettings Normalize(ApprovedLocationSettings settings)
        {
            settings = settings ?? ApprovedLocationSettings.CreateDefault();
            settings.SchemaVersion = ApprovedLocationSettings.CurrentSchemaVersion;
            settings.Version = ApprovedLocationSettings.CurrentVersion;
            settings.Locations = (settings.Locations ?? new List<ApprovedLocationEntry>())
                .Select(NormalizeEntry)
                .Where(entry => !string.IsNullOrWhiteSpace(entry.LocalPath))
                .GroupBy(entry => entry.LocalId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .Take(MaxLocations)
                .ToList();
            return settings;
        }

        private ApprovedLocationEntry NormalizeEntry(ApprovedLocationEntry entry)
        {
            entry = entry ?? new ApprovedLocationEntry();
            entry.LocalId = string.IsNullOrWhiteSpace(entry.LocalId) ? Guid.NewGuid().ToString("N") : SafeIdentifier(entry.LocalId);
            entry.DisplayAlias = SafeAlias(entry.DisplayAlias);
            entry.LocalPath = entry.LocalPath ?? string.Empty;
            if (entry.SourceType == ApprovedLocationSourceType.Git &&
                string.Equals(entry.DisplayAlias, "Local Repository", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(entry.LocalPath))
            {
                entry.DisplayAlias = SafeAlias(Path.GetFileName(entry.LocalPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            }

            if (entry.CreatedAt == default)
            {
                entry.CreatedAt = DateTimeOffset.UtcNow;
            }

            if (entry.UpdatedAt == default)
            {
                entry.UpdatedAt = entry.CreatedAt;
            }

            return entry;
        }

        private Result Validate(ApprovedLocationSettings settings)
        {
            if (settings.Locations.Count > MaxLocations)
            {
                return Result.Failure("too_many_approved_locations", "Too many approved locations.");
            }

            foreach (var entry in settings.Locations)
            {
                var validation = ValidateEntry(entry);
                if (!validation.IsSuccess)
                {
                    return validation;
                }
            }

            return Result.Success();
        }

        private Result ValidateEntry(ApprovedLocationEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.LocalId))
            {
                return Result.Failure("approved_location_missing_id", "Approved location id is required.");
            }

            if (string.IsNullOrWhiteSpace(entry.LocalPath))
            {
                return Result.Failure("approved_location_missing_path", "Approved location path is required.");
            }

            if (string.IsNullOrWhiteSpace(entry.DisplayAlias) || detector.ContainsSensitiveString(entry.DisplayAlias))
            {
                return Result.Failure("approved_location_unsafe_alias", "Approved location alias must be a safe local label.");
            }

            return Result.Success();
        }

        private string SafeAlias(string alias)
        {
            var safe = string.IsNullOrWhiteSpace(alias) ? "Approved location" : alias.Trim();
            if (detector.ContainsSensitiveString(safe))
            {
                safe = "Approved location";
            }

            return safe.Length <= MaxAliasLength ? safe : safe.Substring(0, MaxAliasLength);
        }

        private static string SafeIdentifier(string value)
        {
            var safe = new string((value ?? string.Empty).Where(char.IsLetterOrDigit).Take(64).ToArray());
            return string.IsNullOrWhiteSpace(safe) ? Guid.NewGuid().ToString("N") : safe;
        }

        private static int ReadSchemaVersion(JObject root, string schemaField, string legacyField)
        {
            var schemaToken = root[schemaField] ?? root[legacyField];
            return schemaToken != null && schemaToken.Type == JTokenType.Integer && int.TryParse(schemaToken.ToString(), out var parsed)
                ? parsed
                : 0;
        }

        private static void ReplaceLegacyProviderNames(JToken token)
        {
            if (token == null)
            {
                return;
            }

            if (token.Type == JTokenType.String &&
                string.Equals(token.Value<string>(), "Chat" + "GPT", StringComparison.OrdinalIgnoreCase))
            {
                token.Replace("Codex");
                return;
            }

            foreach (var child in token.Children().ToList())
            {
                ReplaceLegacyProviderNames(child);
            }
        }

        private void PreserveRecoveryCopy(string recoveryPath)
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    File.Copy(settingsFilePath, recoveryPath, true);
                }
            }
            catch (IOException)
            {
                // Recovery copies are best effort; loading still returns a safe local-only default.
            }
            catch (UnauthorizedAccessException)
            {
                // Recovery copies are best effort; loading still returns a safe local-only default.
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetDefaultSettingsDirectory()
        {
#if UNITY_5_3_OR_NEWER
            return UnityEngine.Application.persistentDataPath;
#else
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TokenForge");
#endif
        }
    }

    public static class ApprovedLocationSourceTypeExtensions
    {
        public static AgentProviderType ToAgentProviderType(this ApprovedLocationSourceType sourceType)
        {
            switch (sourceType)
            {
                case ApprovedLocationSourceType.Claude: return AgentProviderType.Claude;
                case ApprovedLocationSourceType.ClaudeCode: return AgentProviderType.ClaudeCode;
                case ApprovedLocationSourceType.Cursor: return AgentProviderType.Cursor;
                case ApprovedLocationSourceType.Codex: return AgentProviderType.Codex;
                case ApprovedLocationSourceType.GitHubCopilot: return AgentProviderType.GitHubCopilot;
                case ApprovedLocationSourceType.GeminiCli: return AgentProviderType.GeminiCli;
                case ApprovedLocationSourceType.Manual: return AgentProviderType.Manual;
                default: return AgentProviderType.Unknown;
            }
        }

        public static ApprovedLocationSourceType ToApprovedLocationSourceType(this AgentProviderType providerType)
        {
            switch (providerType)
            {
                case AgentProviderType.Claude: return ApprovedLocationSourceType.Claude;
                case AgentProviderType.ClaudeCode: return ApprovedLocationSourceType.ClaudeCode;
                case AgentProviderType.Cursor: return ApprovedLocationSourceType.Cursor;
                case AgentProviderType.Codex: return ApprovedLocationSourceType.Codex;
                case AgentProviderType.GitHubCopilot: return ApprovedLocationSourceType.GitHubCopilot;
                case AgentProviderType.GeminiCli: return ApprovedLocationSourceType.GeminiCli;
                case AgentProviderType.Manual: return ApprovedLocationSourceType.Manual;
                default: return ApprovedLocationSourceType.UnknownAuto;
            }
        }
    }
}
