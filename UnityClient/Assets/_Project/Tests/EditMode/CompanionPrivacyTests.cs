using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Tests
{
    public sealed class CompanionPrivacyTests
    {
        [Test]
        public void OldSaveWithoutCompanionData_LoadsDefaultEgg()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            Directory.CreateDirectory(directory);
            File.WriteAllText(repository.SaveFilePath, "{\"SaveVersion\":1,\"CharacterProfile\":{\"Level\":2},\"WorkSessionSummaries\":[]}");

            var loaded = RunAsync(() => repository.LoadAsync(CancellationToken.None));

            Assert.IsNotNull(loaded.CompanionState);
            Assert.AreEqual(CompanionStage.Egg, loaded.CompanionState.Stage);
            Assert.AreEqual(0, loaded.CompanionState.TotalXp);
        }

        [Test]
        public void PrivacyValidator_RejectsForbiddenCompanionFields()
        {
            var sanitizer = new PrivacySanitizer();

            var result = sanitizer.ValidateNoForbiddenFields("{\"CompanionState\":{\"rawPath\":\"/Users/alice/private\",\"commitMessage\":\"secret\"}}");

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("privacy_forbidden_data", result.ErrorCode);
        }

        [Test]
        public void SaveDataRepository_RejectsUnsafeCompanionReasonText()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            saveData.CompanionState.LastGrowthReasonIds = new System.Collections.Generic.List<string>
            {
                "prompt: inspect /Users/alice/private"
            };

            var result = RunAsync(() => repository.SaveAsync(saveData, CancellationToken.None));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("privacy_forbidden_data", result.ErrorCode);
            Assert.IsFalse(File.Exists(repository.SaveFilePath));
        }

        [Test]
        public void CompanionSaveData_UsesOnlySafeAggregateFields()
        {
            var saveData = SaveData.CreateDefault();
            saveData.CompanionState = new CompanionState
            {
                Stage = CompanionStage.Junior,
                Archetype = CompanionArchetype.Builder,
                Level = 3,
                TotalXp = 1200,
                XpToNextStage = 1400,
                GrowthProfile = new CompanionGrowthProfile
                {
                    ApprovedSessionCount = 4,
                    ImplementationScore = 12,
                    DebugScore = 2,
                    StructureScore = 1,
                    ProviderDiversityBucket = CountBucket.Small,
                    WorkTypeDiversityBucket = CountBucket.Small
                },
                LastGrowthReasonIds = { "approved_aggregate_growth", "implementation_activity" }
            };
            saveData.DesktopCompanionSettings = new DesktopCompanionSettings
            {
                IsDesktopCompanionEnabled = true,
                IsClickThroughEnabled = true,
                MotionMode = CompanionDesktopMotionMode.Calm,
                LastOverlayPositionXBucket = CountBucket.Small,
                LastOverlayPositionYBucket = CountBucket.Medium
            };

            var result = new PrivacySanitizer().ValidateSafeSaveData(saveData);

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        }

        [Test]
        public void DomainCompanionModels_DoNotExposeForbiddenRawFields()
        {
            var detector = new ForbiddenFieldDetector();
            foreach (var type in new[] { typeof(CompanionState), typeof(CompanionGrowthProfile), typeof(DesktopCompanionSettings), typeof(CompanionTokenUsageProfile) })
            {
                foreach (var property in type.GetProperties())
                {
                    Assert.IsFalse(detector.IsForbiddenFieldName(property.Name), type.Name + "." + property.Name);
                }
            }
        }

        private static T RunAsync<T>(Func<Task<T>> operation)
        {
            return Task.Run(operation).GetAwaiter().GetResult();
        }
    }
}
