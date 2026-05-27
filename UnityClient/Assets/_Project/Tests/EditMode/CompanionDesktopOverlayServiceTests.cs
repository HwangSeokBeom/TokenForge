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
            settings.VisualThemeId = "runner";
            service.Create();
            service.Show();

            movement.Tick(0.5f, new CompanionState { Stage = CompanionStage.Baby, Archetype = CompanionArchetype.Builder, TotalXp = 600 }, settings);

            Assert.Greater(service.MotionProfileSetCount, 0);
            Assert.Greater(service.VisualSetCount, 0);
            Assert.AreEqual(CompanionStage.Baby, service.LastStage);
            Assert.AreEqual(CompanionArchetype.Builder, service.LastArchetype);
            Assert.AreEqual("runner", service.LastVisualThemeId);
            Assert.IsFalse(service.LastClickThrough);
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
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, new CompanionState { Stage = CompanionStage.Baby, Archetype = CompanionArchetype.Builder, TotalXp = 600 });

            new MacApplicationLifecycleService().HideMainWindow();

            Assert.AreEqual(CompanionDesktopOverlayState.Active, overlay.State);
            Assert.AreEqual(CompanionStage.Baby, overlay.LastStage);
            Assert.AreEqual(CompanionArchetype.Builder, overlay.LastArchetype);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void OverlayClickCallbackTriggersReactionWithoutShowingMainWindow()
        {
            var overlay = new FakeOverlayService();
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            settings.IsClickThroughEnabled = false;
            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, CompanionState.CreateDefault());

            overlay.RaiseClick();

            Assert.AreEqual(0, lifecycle.ShowCount);
            Assert.AreEqual(1, overlay.ReactionCount);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void OverlayDoubleClickCallbackShowsMainWindowInNormalMode()
        {
            var overlay = new FakeOverlayService();
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            settings.IsClickThroughEnabled = false;
            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, CompanionState.CreateDefault());

            overlay.RaiseDoubleClick();

            Assert.AreEqual(1, lifecycle.ShowCount);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void OverlayClickCallbackDoesNotShowMainWindowWhenClickThroughEnabled()
        {
            var overlay = new FakeOverlayService();
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            settings.IsClickThroughEnabled = true;
            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, CompanionState.CreateDefault());

            overlay.RaiseClick();

            Assert.AreEqual(0, lifecycle.ShowCount);
            Assert.AreEqual(0, overlay.ReactionCount);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void DragEndedCallbackReportsPersistablePosition()
        {
            var overlay = new FakeOverlayService();
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var reported = Vector2.zero;
            controller.PositionChanged += position => reported = position;
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;
            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, CompanionState.CreateDefault());

            overlay.RaiseDragEnded(new Vector2(320f, 240f));

            Assert.AreEqual(new Vector2(320f, 240f), reported);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void DesktopCompanionEnableTwiceIsIdempotent()
        {
            var overlay = new FakeOverlayService();
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;

            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, CompanionState.CreateDefault());
            controller.ApplySettings(settings, CompanionState.CreateDefault());

            Assert.AreEqual(1, overlay.CreateCount);
            Assert.AreEqual(CompanionDesktopOverlayState.Active, overlay.State);
            Assert.GreaterOrEqual(overlay.ShowCount, 2);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void DesktopCompanionDisableCleansUpActiveState()
        {
            var overlay = new FakeOverlayService();
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;

            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, CompanionState.CreateDefault());
            settings.IsDesktopCompanionEnabled = false;
            controller.ApplySettings(settings, CompanionState.CreateDefault());

            Assert.AreEqual(CompanionDesktopOverlayState.Disabled, overlay.State);
            Assert.GreaterOrEqual(overlay.HideCount, 1);
            UnityEngine.Object.DestroyImmediate(controllerObject);
        }

        [Test]
        public void DesktopCompanionFailureShowsReasonAndUsesFallback()
        {
            var overlay = new FakeOverlayService { CreateShouldSucceed = false, FailureMessage = "Native overlay library missing or incompatible: EntryPointNotFoundException" };
            var lifecycle = new FakeLifecycleService();
            var controllerObject = new GameObject("Desktop Companion Controller");
            var controller = controllerObject.AddComponent<DesktopCompanionOverlayController>();
            var settings = DesktopCompanionSettings.CreateDefault();
            settings.IsDesktopCompanionEnabled = true;

            controller.Initialize(overlay, lifecycle);
            controller.ApplySettings(settings, CompanionState.CreateDefault());

            Assert.AreEqual(CompanionDesktopOverlayState.Fallback, controller.OverlayState);
            Assert.That(controller.OverlayStatusMessage, Does.Contain("EntryPointNotFoundException"));
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

        [Test]
        public void FakeMenuBarLifecycleActionsSeparateShowHideAndQuit()
        {
            var lifecycle = new FakeLifecycleService();

            lifecycle.UpdateStatusItem("Token", CompanionStage.Egg, CompanionArchetype.Unknown, 1, "Local Repository 1", "Codex selected", "Local only", true, false, true, false);
            lifecycle.ShowMainWindow();
            lifecycle.HideMainWindow();
            lifecycle.Quit();

            Assert.AreEqual(1, lifecycle.UpdateStatusItemCount);
            Assert.AreEqual(1, lifecycle.ShowCount);
            Assert.AreEqual(1, lifecycle.HideCount);
            Assert.AreEqual(1, lifecycle.QuitCount);
        }

        [Test]
        public void StatusIconProviderReturnsStageSpecificPixelAssetKeys()
        {
            Assert.AreEqual("companion.status.egg.pixel", CompanionStatusIconProvider.AssetKeyFor(CompanionStage.Egg));
            Assert.AreEqual("companion.status.hatching.pixel", CompanionStatusIconProvider.AssetKeyFor(CompanionStage.Hatching));
            Assert.AreEqual("companion.status.baby.pixel", CompanionStatusIconProvider.AssetKeyFor(CompanionStage.Baby));
            Assert.AreEqual("companion.status.junior.pixel", CompanionStatusIconProvider.AssetKeyFor(CompanionStage.Junior));
            Assert.AreEqual("companion.status.adult.pixel", CompanionStatusIconProvider.AssetKeyFor(CompanionStage.Adult));
        }

        [Test]
        public void StageMotionProfilesKeepEggMostlyIdleAndAllowBabyWandering()
        {
            var egg = CompanionVisualProfileResolver.Resolve(new CompanionState { Stage = CompanionStage.Egg }, CompanionDesktopMotionMode.Normal);
            var baby = CompanionVisualProfileResolver.Resolve(new CompanionState { Stage = CompanionStage.Baby, TotalXp = 500 }, CompanionDesktopMotionMode.Normal);

            Assert.IsFalse(egg.MotionProfile.AllowsWandering);
            Assert.LessOrEqual(egg.MotionProfile.WanderRadius, 24f);
            Assert.AreEqual(CompanionReaction.Tap, egg.ReactionProfile.PrimaryClickReaction);
            Assert.IsTrue(baby.MotionProfile.AllowsWandering);
            Assert.Greater(baby.MotionProfile.WanderRadius, egg.MotionProfile.WanderRadius);
            Assert.AreEqual(CompanionReaction.Happy, baby.ReactionProfile.PrimaryClickReaction);
        }

        [Test]
        public void NativeStatusItemUsesImageInsteadOfTfTitleFallback()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var nativeSourcePath = Path.Combine(projectRoot, "UnityClient", "Assets", "Plugins", "macOS", "DesktopCompanionOverlay.mm");
            var source = File.ReadAllText(nativeSourcePath);

            Assert.That(source, Does.Contain("TokenForgeCreateStatusCompanionImage"));
            Assert.That(source, Does.Contain("[image setTemplate:NO]"));
            Assert.That(source, Does.Contain("self.statusItem.button.image"));
            Assert.That(source, Does.Contain("self.statusItem.button.title = @\"\""));
            Assert.That(source, Does.Contain("TokenForge_RegisterOverlayDoubleClickedCallback"));
            Assert.That(source, Does.Contain("TokenForge_RegisterOverlayDragEndedCallback"));
            Assert.That(source, Does.Contain("SetCompanionOverlayMotionProfile"));
            Assert.That(source, Does.Contain("Reset Companion Position"));
            Assert.That(source, Does.Contain("Enable Click-through"));
            Assert.That(source, Does.Contain("Disable Click-through"));
            Assert.That(source, Does.Not.Contain("self.statusItem.button.title = @\"TF\""));
            Assert.That(source, Does.Not.Contain("@\"◉ TF\""));
        }

        private sealed class FakeOverlayService : IDesktopCompanionOverlayService
        {
            public event System.Action Clicked;
            public event System.Action DoubleClicked;
            public event System.Action<Vector2> DragEnded;
            public bool IsAvailable => true;
            public bool IsNativeOverlay => true;
            public bool IsDragging { get; set; }
            public CompanionDesktopOverlayState State { get; private set; } = CompanionDesktopOverlayState.Disabled;
            public string StatusMessage { get; private set; } = "fake";
            public bool CreateShouldSucceed { get; set; } = true;
            public string FailureMessage { get; set; } = "failed";
            public int CreateCount { get; private set; }
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }
            public int PositionSetCount { get; private set; }
            public int VisualSetCount { get; private set; }
            public bool LastClickThrough { get; private set; }
            public bool LastClickEnabled { get; private set; }
            public int ReactionCount { get; private set; }
            public int ResetPositionCount { get; private set; }
            public int MotionProfileSetCount { get; private set; }
            public CompanionStage LastStage { get; private set; }
            public CompanionArchetype LastArchetype { get; private set; }
            public string LastVisualThemeId { get; private set; }

            public bool Create()
            {
                CreateCount++;
                if (!CreateShouldSucceed)
                {
                    State = CompanionDesktopOverlayState.Unavailable;
                    StatusMessage = FailureMessage;
                    return false;
                }

                State = CompanionDesktopOverlayState.Disabled;
                StatusMessage = "created";
                return true;
            }

            public void Show()
            {
                ShowCount++;
                State = CompanionDesktopOverlayState.Active;
                StatusMessage = "active";
            }

            public void Hide()
            {
                HideCount++;
                State = CompanionDesktopOverlayState.Disabled;
                StatusMessage = "hidden";
            }

            public void SetPosition(Vector2 position)
            {
                PositionSetCount++;
            }

            public void SetSize(Vector2 size)
            {
            }

            public void SetVisualTheme(string visualThemeId)
            {
                LastVisualThemeId = visualThemeId;
            }

            public void SetVisualState(CompanionStage stage, CompanionArchetype archetype, CompanionAnimationState animationState, bool facingLeft)
            {
                VisualSetCount++;
                LastStage = stage;
                LastArchetype = archetype;
            }

            public void SetMotionProfile(CompanionVisualProfile profile)
            {
                MotionProfileSetCount++;
            }

            public void TriggerReaction(CompanionReaction reaction, string speechText)
            {
                ReactionCount++;
            }

            public void ResetPosition()
            {
                ResetPositionCount++;
            }

            public void SetClickThrough(bool clickThrough)
            {
                LastClickThrough = clickThrough;
            }

            public void SetClickEnabled(bool enabled)
            {
                LastClickEnabled = enabled;
            }

            public void RaiseClick()
            {
                Clicked?.Invoke();
            }

            public void RaiseDoubleClick()
            {
                DoubleClicked?.Invoke();
            }

            public void RaiseDragEnded(Vector2 position)
            {
                DragEnded?.Invoke(position);
            }

            public void Destroy()
            {
                State = CompanionDesktopOverlayState.Disabled;
            }
        }

        private sealed class FakeLifecycleService : IApplicationLifecycleService
        {
            public event System.Action<ApplicationMenuAction> MenuActionRequested;
            public bool IsAvailable => true;
            public int ShowCount { get; private set; }
            public int HideCount { get; private set; }
            public int UpdateStatusItemCount { get; private set; }
            public int QuitCount { get; private set; }
            public bool Install() { return true; }
            public void UpdateStatusItem(string companionName, CompanionStage stage, CompanionArchetype archetype, int level, string repositoryAlias, string agentStatus, string syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync) { UpdateStatusItemCount++; }
            public void ShowMainWindow() { ShowCount++; }
            public void HideMainWindow() { HideCount++; }
            public bool IsMainWindowVisible() { return ShowCount > HideCount; }
            public void Quit() { QuitCount++; }
            public void RaiseMenuAction(ApplicationMenuAction action) { MenuActionRequested?.Invoke(action); }
        }
    }
}
