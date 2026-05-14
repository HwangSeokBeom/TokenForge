using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Persistence
{
    public sealed class SaveDataRepository
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

        public async Task<SaveData> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (!File.Exists(saveFilePath))
            {
                return SaveData.CreateDefault();
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (var reader = new StreamReader(saveFilePath, Encoding.UTF8))
            {
                var json = await reader.ReadToEndAsync();
                cancellationToken.ThrowIfCancellationRequested();
                var saveData = JsonConvert.DeserializeObject<SaveData>(json, serializerSettings) ?? SaveData.CreateDefault();
                return Migrate(saveData);
            }
        }

        public async Task<Result> SaveAsync(SaveData saveData, CancellationToken cancellationToken = default)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

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
            DeleteIfExists(saveFilePath + ".tmp");
            return Task.CompletedTask;
        }

        private static SaveData Migrate(SaveData saveData)
        {
            if (saveData.SaveVersion <= 0)
            {
                saveData.SaveVersion = 1;
            }

            if (saveData.SaveVersion < SaveData.CurrentSaveVersion)
            {
                saveData.SaveVersion = SaveData.CurrentSaveVersion;
            }

            return saveData;
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
