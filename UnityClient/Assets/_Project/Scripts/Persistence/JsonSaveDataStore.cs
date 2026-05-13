using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Persistence
{
    public sealed class JsonSaveDataStore : ISaveDataStore
    {
        private const string SaveFileName = "tokenforge-save.json";
        private readonly string saveFilePath;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly JsonSerializerOptions options;

        public JsonSaveDataStore(string saveDirectory = null, PrivacySanitizer privacySanitizer = null)
        {
            var directory = string.IsNullOrWhiteSpace(saveDirectory) ? GetDefaultSaveDirectory() : saveDirectory;
            Directory.CreateDirectory(directory);
            saveFilePath = Path.Combine(directory, SaveFileName);
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<SaveData> LoadAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(saveFilePath))
            {
                return SaveData.CreateDefault();
            }

            using (var stream = File.OpenRead(saveFilePath))
            {
                var saveData = await JsonSerializer.DeserializeAsync<SaveData>(stream, options, cancellationToken);
                return saveData ?? SaveData.CreateDefault();
            }
        }

        public async Task SaveAsync(SaveData saveData, CancellationToken cancellationToken)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            saveData.SaveVersion = SaveData.CurrentSaveVersion;
            privacySanitizer.ThrowIfUnsafe(saveData);

            var directory = Path.GetDirectoryName(saveFilePath);
            Directory.CreateDirectory(directory);
            var tempPath = saveFilePath + ".tmp";

            using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, saveData, options, cancellationToken);
            }

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

            return Task.CompletedTask;
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
