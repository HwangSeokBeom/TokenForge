using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Persistence
{
    public sealed class JsonSaveDataStore : ISaveDataStore
    {
        private const string SaveFileName = "tokenforge-save.json";
        private readonly string saveFilePath;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly JsonSerializerSettings settings;

        public JsonSaveDataStore(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            var directory = string.IsNullOrWhiteSpace(saveDirectory) ? GetDefaultSaveDirectory() : saveDirectory;
            Directory.CreateDirectory(directory);
            saveFilePath = Path.Combine(directory, SaveFileName);
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include
            };
        }

        public async Task<SaveData> LoadAsync(CancellationToken cancellationToken)
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
                PreserveRecoveryCopy(saveFilePath + ".corrupt");
                return SaveData.CreateDefault();
            }

            var schemaVersion = ReadSchemaVersion(root);
            if (schemaVersion > SaveData.CurrentSchemaVersion)
            {
                PreserveRecoveryCopy(saveFilePath + ".unsupported-schema");
                return SaveData.CreateDefault();
            }

            try
            {
                return Migrate(root.ToObject<SaveData>(JsonSerializer.Create(settings)) ?? SaveData.CreateDefault());
            }
            catch (JsonException)
            {
                PreserveRecoveryCopy(saveFilePath + ".corrupt");
                return SaveData.CreateDefault();
            }
        }

        public async Task SaveAsync(SaveData saveData, CancellationToken cancellationToken)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            saveData = Migrate(saveData);
            saveData.SchemaVersion = SaveData.CurrentSchemaVersion;
            saveData.SaveVersion = SaveData.CurrentSaveVersion;
            privacySanitizer.ThrowIfUnsafe(saveData);

            var directory = Path.GetDirectoryName(saveFilePath);
            Directory.CreateDirectory(directory);
            var tempPath = saveFilePath + ".tmp";

            var json = JsonConvert.SerializeObject(saveData, Formatting.Indented, settings);
            cancellationToken.ThrowIfCancellationRequested();

            using (var writer = new StreamWriter(tempPath, false, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(json);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(saveFilePath))
            {
                var backupPath = saveFilePath + ".bak";
                File.Replace(tempPath, saveFilePath, backupPath, true);
            }
            else
            {
                File.Move(tempPath, saveFilePath);
            }
        }

        public Task DeleteAsync(CancellationToken cancellationToken)
        {
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
            }

            var backupPath = saveFilePath + ".bak";
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            DeleteIfExists(saveFilePath + ".tmp");
            DeleteIfExists(saveFilePath + ".corrupt");
            DeleteIfExists(saveFilePath + ".unsupported-schema");

            return Task.CompletedTask;
        }

        private static SaveData Migrate(SaveData saveData)
        {
            saveData = saveData ?? SaveData.CreateDefault();
            saveData.SchemaVersion = SaveData.CurrentSchemaVersion;
            saveData.SaveVersion = SaveData.CurrentSaveVersion;
            saveData.CharacterProfile = saveData.CharacterProfile ?? new CharacterProfile();
            saveData.WorkSessionSummaries = saveData.WorkSessionSummaries ?? new System.Collections.Generic.List<AgentWorkSession>();
            saveData.GrowthHistory = saveData.GrowthHistory ?? new System.Collections.Generic.List<CharacterGrowthResult>();
            saveData.ConnectedProjects = saveData.ConnectedProjects ?? new System.Collections.Generic.List<ConnectedProject>();
            saveData.ProviderSettings = saveData.ProviderSettings ?? new System.Collections.Generic.List<ProviderSettings>();
            saveData.SyncState = saveData.SyncState ?? new SyncState();
            saveData.PrivacyPreferences = saveData.PrivacyPreferences ?? new PrivacyPreferences();
            saveData.UserSettings = saveData.UserSettings ?? new UserSettings();
            saveData.UserSettings.PrivacyPreferences = saveData.UserSettings.PrivacyPreferences ?? new PrivacyPreferences();
            saveData.DailyProgress = saveData.DailyProgress ?? new DailyProgress();
            saveData.MiniGameHistory = saveData.MiniGameHistory ?? new System.Collections.Generic.List<MiniGameSession>();
            saveData.Achievements = saveData.Achievements ?? new System.Collections.Generic.List<AchievementProgress>();
            return saveData;
        }

        private static int ReadSchemaVersion(JObject root)
        {
            var schemaToken = root["schemaVersion"] ?? root["SaveVersion"];
            return schemaToken != null && schemaToken.Type == JTokenType.Integer && int.TryParse(schemaToken.ToString(), out var parsed)
                ? parsed
                : 0;
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
            }
            catch (UnauthorizedAccessException)
            {
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
