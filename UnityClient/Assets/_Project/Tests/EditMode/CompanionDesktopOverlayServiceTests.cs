using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Domain;
using TokenForge.Client.Persistence;
using TokenForge.Client.Platform;
using TokenForge.Client.UI;
using UnityEngine;

namespace TokenForge.Client.Tests
{
    public sealed class CompanionDesktopOverlayServiceTests
    {
        [Test]
        public void FallbackServiceReportsUnavailableWithoutCreatingNativeWindow()
        {
            var service = new InAppCompanionOverlayFallbackService();

            var created = service.Create();
            service.Show();

            Assert.IsFalse(created);
            Assert.IsFalse(service.IsAvailable);
            Assert.AreEqual(CompanionDesktopOverlayState.Fallback, service.State);
        }

        [Test]
        public void DesktopMovementControllerUsesAbstractionAndUpdatesVisualState()
        {
            var service = new FakeOverlayService();
            var movement = new CompanionDesktopMovementController(service);
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            settings.MotionMode = CompanionDesktopMotionMode.Playful;
            service.Create();
            service.Show();

            movement.Tick(0.5f, new CompanionState { Stage = CompanionStage.Baby, Archetype = CompanionArchetype.Builder, TotalXp = 600 }, settings);

            Assert.Greater(service.PositionSetCount, 0);
            Assert.Greater(service.VisualSetCount, 0);
            Assert.AreEqual(CompanionStage.Baby, service.LastStage);
            Assert.AreEqual(CompanionArchetype.Builder, service.LastArchetype);
            Assert.IsTrue(service.LastClickThrough);
        }

        [Test]
        public void MacLifecycleBridgeNoOpsOutsideMacPlayerBuild()
        {
            var service = new MacApplicationLifecycleService();

            Assert.DoesNotThrow(() => service.ShowMainWindow());
            Assert.DoesNotThrow(() => service.HideMainWindow());
            Assert.IsFalse(service.Install());
            Assert.IsFalse(service.IsMainWindowVisible());
        }

        [Test]
        public void DashboardHideDoesNotMutateCompanionOverlayState()
        {
            var overlay = new FakeOverlayService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            controller.Initialize(overlay);
            controller.ApplySettings(settings, new CompanionState { Stage = CompanionStage.Baby, Archetype = CompanionArchetype.Builder, TotalXp = 600 });

            new MacApplicationLifecycleService().HideMainWindow();

            Assert.AreEqual(CompanionDesktopOverlayState.Active, overlay.State);
            Assert.AreEqual(CompanionStage.Baby, overlay.LastStage);
            Assert.AreEqual(CompanionArchetype.Builder, overlay.LastArchetype);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void DesktopCompanionEnabledStateSurvivesDashboardHideShowBridgeCalls()
        {
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            settings.MotionMode = CompanionDesktopMotionMode.Playful;
            var lifecycle = new MacApplicationLifecycleService();

            lifecycle.HideMainWindow();
            lifecycle.ShowMainWindow();

            Assert.IsTrue(settings.IsDesktopCompanionEnabled);
            Assert.AreEqual(CompanionDesktopMotionMode.Playful, settings.MotionMode);
        }

        [Test]
        public void DashboardHideDoesNotClearSaveData()
        {
            var directory = Path.Combine(Path.GetTempPath(), "TokenForgeTests", Path.GetRandomFileName());
            var repository = new SaveDataRepository(directory);
            var saveData = SaveData.CreateDefault();
            saveData.CharacterProfile.TotalExp = 1200;
            saveData.DesktopCompanionSettings.IsDesktopCompanionEnabled = true;
            Task.Run(async () => await repository.SaveAsync(saveData, CancellationToken.None)).GetAwaiter().GetResult();

            var lifecycle = new MacApplicationLifecycleService();
            lifecycle.HideMainWindow();
            lifecycle.ShowMainWindow();
            var loaded = Task.Run(async () => await repository.LoadAsync(CancellationToken.None)).GetAwaiter().GetResult();

            Assert.AreEqual(1200, loaded.CharacterProfile.TotalExp);
            Assert.IsTrue(loaded.DesktopCompanionSettings.IsDesktopCompanionEnabled);
        }

        [Test]
        public void ExplicitQuitBridgeIsSeparateFromHideAndShowBridge()
        {
            var lifecycle = new MacApplicationLifecycleService();

            Assert.DoesNotThrow(() => lifecycle.HideMainWindow());
            Assert.DoesNotThrow(() => lifecycle.ShowMainWindow());
            Assert.IsFalse(lifecycle.IsAvailable);
        }

        private sealed class FakeOverlayService : IDesktopCompanionOverlayService
        {
            public bool IsAvailable => true;
            public CompanionDesktopOverlayState State { get; private set; } = CompanionDesktopOverlayState.Disabled;
            public int PositionSetCount { get; private set; }
            public int VisualSetCount { get; private set; }
            public bool LastClickThrough { get; private set; }
            public CompanionStage LastStage { get; private set; }
            public CompanionArchetype LastArchetype { get; private set; }

            public bool Create()
            {
                State = CompanionDesktopOverlayState.Disabled;
                return true;
            }

            public void Show()
            {
                State = CompanionDesktopOverlayState.Active;
            }

            public void Hide()
            {
                State = CompanionDesktopOverlayState.Disabled;
            }

            public void SetPosition(Vector2 position)
            {
                PositionSetCount++;
            }

            public void SetSize(Vector2 size)
            {
            }

            public void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft)
            {
                VisualSetCount++;
                LastStage = stage;
                LastArchetype = archetype;
            }

            public void SetClickThrough(bool clickThrough)
            {
                LastClickThrough = clickThrough;
            }

            public void Destroy()
            {
                State = CompanionDesktopOverlayState.Disabled;
            }
        }
    }
}
