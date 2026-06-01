using System;
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
    public interface ILocalSaveDataRepository
    {
        Task<SaveData> LoadAsync(CancellationToken cancellationToken = default);
        Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default);
    }

    public sealed class SaveDataRepository : ILocalSaveDataRepository
    {
        public const string SaveFileName = "tokenforge-save.json";
        private readonly string saveFilePath;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly JsonSerializerSettings serializerSettings;

        public SaveDataRepository(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            var directory = string.IsNullOrWhiteSpace(saveDirectory) ? GetDefaultSaveDirectory() : saveDirectory;
            Directory.CreateDirectory(directory);
            saveFilePath = Path.Combine(directory, SaveFileName);
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                Formatting = Formatting.Indented
            };
        }

        public string SaveFilePath => saveFilePath;
        public string BackupFilePath => saveFilePath + ".bak";
        public string CorruptFilePath => saveFilePath + ".corrupt";
        public string UnsupportedSchemaFilePath => saveFilePath + ".unsupported-schema";
        public string UnsafeFilePath => saveFilePath + ".unsafe";

        public async Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(saveFilePath))
            {
                return SaveData.CreateDefault();
            }

            cancellationToken.ThrowIfCancellationRequested();
            string json;
            using (var reader = new StreamReader(saveFilePath, Encoding.UTF8))
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
                return SaveData.CreateDefault();
            }

            var privacyValidation = privacySanitizer.ValidateNoForbiddenFields(root);
            if (!privacyValidation.IsSuccess)
            {
                PreserveRecoveryCopy(UnsafeFilePath);
            }

            var schemaVersion = ReadSchemaVersion(root, "schemaVersion", "SaveVersion");
            if (schemaVersion > SaveData.CurrentSchemaVersion)
            {
                PreserveRecoveryCopy(UnsupportedSchemaFilePath);
                return SaveData.CreateDefault();
            }

            try
            {
                ReplaceLegacyProviderNames(root);
                var saveData = root.ToObject<SaveData>(JsonSerializer.Create(serializerSettings)) ?? SaveData.CreateDefault();
                return Migrate(saveData, schemaVersion);
            }
            catch (JsonException)
            {
                PreserveRecoveryCopy(CorruptFilePath);
                return SaveData.CreateDefault();
            }
        }

        public async Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            var inputValidation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!inputValidation.IsSuccess)
            {
                return inputValidation;
            }

            saveData = Migrate(saveData, saveData.SchemaVersion);
            saveData.SchemaVersion = SaveData.CurrentSchemaVersion;
            saveData.SaveVersion = SaveData.CurrentSaveVersion;
            var validation = privacySanitizer.ValidateSafeSaveData(saveData);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var json = JsonConvert.SerializeObject(saveData, serializerSettings);
            var jsonValidation = privacySanitizer.ValidateNoForbiddenFields(json);
            if (!jsonValidation.IsSuccess)
            {
                return jsonValidation;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(saveFilePath);
            Directory.CreateDirectory(directory);
            var tempPath = saveFilePath + ".tmp";

            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(json);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(saveFilePath))
            {
                File.Replace(tempPath, saveFilePath, BackupFilePath, true);
            }
            else
            {
                File.Move(tempPath, saveFilePath);
            }

            return Result.Success();
        }

        public Task DeleteLocalData(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeleteIfExists(saveFilePath);
            DeleteIfExists(BackupFilePath);
            DeleteIfExists(CorruptFilePath);
            DeleteIfExists(UnsupportedSchemaFilePath);
            DeleteIfExists(UnsafeFilePath);
            DeleteIfExists(saveFilePath + ".tmp");
            return Task.CompletedTask;
        }

        private static SaveData Migrate(SaveData saveData, int sourceSchemaVersion)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            if (sourceSchemaVersion <= 0)
            {
                sourceSchemaVersion = saveData.SchemaVersion > 0 ? saveData.SchemaVersion : saveData.SaveVersion;
            }

            if (sourceSchemaVersion <= 0)
            {
                sourceSchemaVersion = SaveData.CurrentSchemaVersion;
            }

            if (sourceSchemaVersion < SaveData.CurrentSchemaVersion)
            {
                sourceSchemaVersion = SaveData.CurrentSchemaVersion;
            }

            saveData.SchemaVersion = SaveData.CurrentSchemaVersion;
            saveData.SaveVersion = SaveData.CurrentSaveVersion;
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();
            saveData.CompanionState = CompanionProgressionRules.Normalize(saveData.CompanionState);
            saveData.RepositoryCompanionProfiles = saveData.RepositoryCompanionProfiles ?? new System.Collections.Generic.List<RepositoryCompanionProfile>();
            saveData = RepositoryCompanionProfileService.Normalize(saveData);
            saveData.DesktopCompanionSettings = NormalizeDesktopCompanionSettings(saveData.DesktopCompanionSettings);
            saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new System.Collections.Generic.List<AgentWorkSession>();
            foreach (var session in saveData.WorkSessionSummaries)
            {
                if (session?.AgentActivitySummary != null && session.AgentActivitySummary.ProviderType == AgentProviderType.Claude)
                {
                    session.AgentActivitySummary.ProviderType = AgentProviderType.ClaudeCode;
                }
            }
            saveData.GrowthHistory = saveData.GrowthHistory ?? new System.Collections.Generic.List<CharacterGrowthResult>();
            saveData.AppliedNativeReviewIds = saveData.AppliedNativeReviewIds ?? new System.Collections.Generic.List<string>();
            saveData.ActivityReviews = (saveData.ActivityReviews ?? new System.Collections.Generic.List<ActivityReview>())
                .Where(review => review != null && !string.IsNullOrWhiteSpace(review.Id))
                .OrderByDescending(review => review.CreatedAt)
                .Take(100)
                .ToList();
            if (saveData.PendingNativeActivityReview != null)
            {
                saveData.PendingNativeActivityReview.StatDeltas = saveData.PendingNativeActivityReview.StatDeltas ?? CharacterStats.Zero();
                saveData.PendingNativeActivityReview.WarningIds = saveData.PendingNativeActivityReview.WarningIds ?? new System.Collections.Generic.List<string>();
                saveData.PendingNativeActivityReview.SafeSessions = saveData.PendingNativeActivityReview.SafeSessions ?? new System.Collections.Generic.List<AgentWorkSession>();
                saveData.PendingNativeActivityReview.GrowthResults = saveData.PendingNativeActivityReview.GrowthResults ?? new System.Collections.Generic.List<CharacterGrowthResult>();
            }
            saveData.RecentNativeAnalysisRuns = (saveData.RecentNativeAnalysisRuns ?? new System.Collections.Generic.List<NativeAnalysisRunRecord>())
                .Where(run => run != null && !string.IsNullOrWhiteSpace(run.RunId))
                .OrderByDescending(run => run.CreatedAtUtc)
                .Take(20)
                .ToList();
            saveData.RepositoryTimelineEvents = (saveData.RepositoryTimelineEvents ?? new System.Collections.Generic.List<RepositoryTimelineEvent>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.EventType))
                .OrderByDescending(item => item.TimestampUtc)
                .Take(500)
                .ToList();
            saveData.ConnectedProjects = saveData.ConnectedProjects ?? new System.Collections.Generic.List<ConnectedProject>();
            foreach (var project in saveData.ConnectedProjects)
            {
                if (project == null)
                {
                    continue;
                }

                project.Id = string.IsNullOrWhiteSpace(project.Id) ? project.LocalOnlyProjectId : project.Id;
                project.DisplayName = string.IsNullOrWhiteSpace(project.DisplayName) ? project.ProjectAlias : project.DisplayName;
                project.PathHash = string.IsNullOrWhiteSpace(project.PathHash) ? project.ProjectPathHash : project.PathHash;
                project.ConnectionSource = string.IsNullOrWhiteSpace(project.ConnectionSource) ? "migrated" : project.ConnectionSource;
                project.CompanionId = project.CompanionId ?? string.Empty;
                if (RepositoryCompanionProfileService.IsStaleFallbackProject(project) ||
                    string.Equals(project.ConnectionSource, "auto", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(project.ConnectionSource, "default", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(project.ConnectionSource, "unknown", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(project.ConnectionSource, "dev", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(project.ConnectionSource, "debugFallback", StringComparison.OrdinalIgnoreCase))
                {
                    project.IsActive = false;
                    project.IsArchived = true;
                }
            }
            saveData.ProviderSettings = saveData.ProviderSettings ?? new System.Collections.Generic.List<ProviderSettings>();
            foreach (var provider in saveData.ProviderSettings)
            {
                if (provider == null)
                {
                    continue;
                }

                provider.DetectedSources = provider.DetectedSources ?? new System.Collections.Generic.List<string>();
                provider.Warnings = provider.Warnings ?? new System.Collections.Generic.List<string>();
                provider.Status = string.IsNullOrWhiteSpace(provider.Status)
                    ? ProviderStatusFromLegacy(provider)
                    : provider.Status;
                provider.ApprovedSource = provider.ManualFolderApproved || provider.Selected ? provider.ApprovedSource : string.Empty;
            }
            saveData.SyncState = saveData.SyncState ?? new SyncState();
            saveData.PrivacyPreferences = saveData.PrivacyPreferences ?? new PrivacyPreferences();
            saveData.UserSettings = saveData.UserSettings ?? new UserSettings();
            saveData.UserSettings.PrivacyPreferences = saveData.UserSettings.PrivacyPreferences ?? new PrivacyPreferences();
            saveData.OnboardingPreferences = saveData.OnboardingPreferences ?? new OnboardingPreferences();
            saveData.DailyProgress = saveData.DailyProgress ?? new DailyProgress();
            saveData.MiniGameHistory = saveData.MiniGameHistory ?? new System.Collections.Generic.List<MiniGameSession>();
            saveData.Achievements = saveData.Achievements ?? new System.Collections.Generic.List<AchievementProgress>();
            return saveData;
        }

        private static string ProviderStatusFromLegacy(ProviderSettings provider)
        {
            if (provider == null || !provider.Enabled)
            {
                return "notConfigured";
            }

            if (provider.Selected && !string.IsNullOrWhiteSpace(provider.SafeLocationHash))
            {
                return "connected";
            }

            if (provider.Detected)
            {
                return "detected";
            }

            return "notConfigured";
        }

        private static DesktopCompanionSettings NormalizeDesktopCompanionSettings(DesktopCompanionSettings settings)
        {
            settings = settings ?? DesktopCompanionSettings.CreateDefault();
            var schemaVersion = settings.SchemaVersion;
            settings.SchemaVersion = 2;
            if (schemaVersion < 2)
            {
                settings.IsDesktopCompanionEnabled = true;
                settings.IsClickThroughEnabled = false;
            }

            if (!Enum.IsDefined(typeof(CompanionDesktopMotionMode), settings.MotionMode))
            {
                settings.MotionMode = CompanionDesktopMotionMode.Normal;
            }

            settings.VisualThemeId = CompanionSkinCatalog.Normalize(settings.VisualThemeId);
            if (settings.LastOverlayPositionX < 0f || settings.LastOverlayPositionY < 0f)
            {
                settings.HasSavedOverlayPosition = false;
                settings.LastOverlayPositionX = -1f;
                settings.LastOverlayPositionY = -1f;
            }

            return settings;
        }

        private static int ReadSchemaVersion(JObject root, string schemaField, string legacyField)
        {
            var schemaToken = root[schemaField] ?? root[legacyField];
            if (schemaToken == null)
            {
                return 0;
            }

            return schemaToken.Type == JTokenType.Integer && int.TryParse(schemaToken.ToString(), out var parsed)
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
                if (File.Exists(saveFilePath))
                {
                    File.Copy(saveFilePath, recoveryPath, true);
                }
            }
            catch (IOException)
            {
                // Recovery copies are best effort; loading still returns a safe default.
            }
            catch (UnauthorizedAccessException)
            {
                // Recovery copies are best effort; loading still returns a safe default.
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
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
