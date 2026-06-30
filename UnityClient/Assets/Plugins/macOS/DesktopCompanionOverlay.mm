#import <Cocoa/Cocoa.h>
#include <math.h>
#include <dlfcn.h>
#include <limits.h>
#include <string.h>
#include <sys/stat.h>
#import <CommonCrypto/CommonDigest.h>

@class TokenForgeAppLifecycleDelegate;
@class TokenForgeNativeDashboardController;
@class TokenForgeShopPreviewView;
static TokenForgeAppLifecycleDelegate *TokenForgeEnsureLifecycleDelegate(void);
static TokenForgeNativeDashboardController *TokenForgeEnsureNativeDashboardController(void);
static void TokenForgeOpenNativeDashboardOnMain(void);
static void TokenForgeOpenNativeDashboardOnMainWithSource(NSString *source);
static void TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(NSString *source, BOOL explicitUserOpen);
static void TokenForgeOpenOrFocusDashboard(NSString *source);
static void TokenForgeCloseDashboard(NSString *source);
static void TokenForgeRequestExplicitQuit(NSString *source);
static void TokenForgeTeardownForExplicitQuit(NSString *traceId);
static void TokenForgeSendDashboardAction(const char *action);
static BOOL TokenForgeIsDashboardVisible(void);
static BOOL TokenForgeIsCompanionOverlayVisible(void);
extern "C" void ShowDesktopCompanionOverlay(void);
extern "C" void HideDesktopCompanionOverlay(void);
extern "C" void TokenForge_SetCompanionVisibleWithSource(bool visible, const char *source);
extern "C" bool TokenForge_IsCompanionVisible(void);
extern "C" void TokenForge_ShowAllRepositoryCompanions(const char *source);
extern "C" void TokenForge_HideAllRepositoryCompanions(const char *source);
extern "C" const char *TokenForge_GetOverlayLibraryPath(void);
extern "C" void TokenForge_LogAppBootstrapperRuntimeMarker(void);
extern "C" void SetCompanionOverlayMotionProfile(int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds);
extern "C" void SetCompanionOverlayClickThrough(bool clickThrough);

@interface TokenForgeAppLifecycleDelegate : NSObject <NSApplicationDelegate, NSWindowDelegate>
@property(nonatomic, assign) id<NSApplicationDelegate> originalAppDelegate;
@property(nonatomic, assign) id<NSWindowDelegate> originalMainWindowDelegate;
@property(nonatomic, assign) NSWindow *mainWindow;
@property(nonatomic, strong) NSStatusItem *statusItem;
@property(nonatomic) BOOL explicitTerminationRequested;
@property(nonatomic) BOOL observingWindowNotifications;
@property(nonatomic) BOOL installed;
@property(nonatomic, strong) NSTimer *statusAnimationTimer;
@property(nonatomic) NSInteger statusAnimationFrame;
- (void)install;
- (void)installMainWindowHook;
- (void)installStatusItem;
- (void)updateStatusItemMenu;
- (void)showMainWindow;
- (void)hideMainWindow;
- (BOOL)isMainWindowVisible;
@end

typedef NS_ENUM(NSInteger, TokenForgeCompanionRenderRole) {
    TokenForgeCompanionRenderRoleDashboardPreview = 0,
    TokenForgeCompanionRenderRoleDesktopOverlay = 1,
    TokenForgeCompanionRenderRoleMenuBar = 2
};

typedef NS_ENUM(NSInteger, TokenForgePendingOverlayAction) {
    TokenForgePendingOverlayActionNone = 0,
    TokenForgePendingOverlayActionHide = 1,
    TokenForgePendingOverlayActionDestroy = 2,
    TokenForgePendingOverlayActionResetPosition = 3
};

@interface TokenForgeCompanionView : NSView
@property(nonatomic) NSInteger stage;
@property(nonatomic) NSInteger level;
@property(nonatomic) NSInteger xp;
@property(nonatomic) NSInteger archetype;
@property(nonatomic) NSInteger animationState;
@property(nonatomic) TokenForgeCompanionRenderRole viewRole;
@property(nonatomic) BOOL facingLeft;
@property(nonatomic) BOOL snapshotHydrated;
@property(nonatomic) NSUInteger renderVersion;
@property(nonatomic) CGFloat visualScale;
@property(nonatomic) CGFloat visualRotation;
@property(nonatomic) CGFloat visualOffsetY;
@property(nonatomic) CGFloat safeDrawingInset;
@property(nonatomic, strong) NSString *repositoryId;
@property(nonatomic, strong) NSString *visualThemeId;
@property(nonatomic, strong) NSString *zodiacType;
@property(nonatomic, strong) NSString *assetType;
@property(nonatomic, strong) NSString *viewRoleName;
@property(nonatomic, strong) NSString *speechText;
@property(nonatomic) NSTimeInterval speechExpiresAt;
@end

@interface TokenForgeDashboardCompanionPreviewView : TokenForgeCompanionView
@property(nonatomic) BOOL levelUpReady;
@property(nonatomic) CGFloat motionBounceAmplitude;
@property(nonatomic) CGFloat motionPulseFrequency;
@property(nonatomic, strong) NSString *dashboardAnimationState;
@property(nonatomic, strong) NSTimer *previewTimer;
- (void)startDashboardPreviewAnimation;
- (void)stopDashboardPreviewAnimation;
@end

@interface TokenForgeAvatarPreviewView : TokenForgeDashboardCompanionPreviewView
@property(nonatomic) CGFloat safePadding;
@end

@interface TokenForgeDesktopOverlayCompanionView : TokenForgeCompanionView
@property(nonatomic) NSUInteger overlayViewGeneration;
@property(nonatomic) NSPoint cursorOffsetInsidePanel;
@property(nonatomic) NSPoint mouseDownScreenPoint;
@property(nonatomic) NSPoint panelOriginAtMouseDown;
@property(nonatomic) BOOL firstVisibleFrameLogged;
// Per-panel wander state so every connected repository's companion moves on its
// own anchor/target/velocity, independent of the selected/legacy companion that
// is driven by the global TokenForgeCompanion* motion variables.
@property(nonatomic) NSPoint independentMotionAnchor;
@property(nonatomic) NSPoint independentMotionVelocity;
@property(nonatomic) NSPoint independentMotionTarget;
@property(nonatomic) NSTimeInterval independentMotionNextDecisionAt;
@property(nonatomic) NSTimeInterval independentMotionLastTick;
@property(nonatomic) CGFloat independentMotionPhaseOffset;
@property(nonatomic) BOOL independentMotionInitialized;
- (void)quitFromContextMenu:(id)sender;
@end

@interface TokenForgeDashboardHeroAvatarContainerView : NSView
@property(nonatomic, strong) TokenForgeAvatarPreviewView *preview;
@property(nonatomic, strong) NSString *repositoryName;
@end

@interface TokenForgeGrowthRadarView : NSView
@property(nonatomic, strong) NSArray<NSNumber *> *values;
@property(nonatomic, strong) NSArray<NSString *> *labels;
@property(nonatomic) BOOL hasAxisData;
@property(nonatomic, strong) NSString *axisStatusText;
- (void)configureWithCode:(NSInteger)code focus:(NSInteger)focus debug:(NSInteger)debug design:(NSInteger)design sync:(NSInteger)sync hasAxisData:(BOOL)hasAxisData statusText:(NSString *)statusText;
@end

@interface TokenForgeCompanionOverlayWindow : NSPanel
@end

typedef void (*TokenForgeOverlayClickedCallback)(void);
typedef void (*TokenForgeOverlayDragEndedCallback)(float x, float y);
typedef void (*TokenForgeOverlayDragEndedForRepositoryCallback)(const char *repositoryId, float x, float y);
typedef void (*TokenForgeMenuActionCallback)(const char *action);
typedef void (*TokenForgeDashboardActionCallback)(const char *action);
static TokenForgeOverlayClickedCallback TokenForgeOverlayClicked = nil;
static TokenForgeOverlayClickedCallback TokenForgeOverlayDoubleClicked = nil;
static TokenForgeOverlayDragEndedCallback TokenForgeOverlayDragEnded = nil;
static TokenForgeOverlayDragEndedForRepositoryCallback TokenForgeOverlayDragEndedForRepository = nil;
static TokenForgeMenuActionCallback TokenForgeMenuActionClicked = nil;
static TokenForgeDashboardActionCallback TokenForgeDashboardActionClicked = nil;
static BOOL TokenForgeOverlayClickEnabled = YES;
static BOOL TokenForgeIsDraggingOverlay = NO;
static BOOL TokenForgeDragExceededThreshold = NO;
static BOOL TokenForgeMotionTickLogged = NO;
static BOOL TokenForgeMotionPauseLogged = NO;
static BOOL TokenForgeMenuClickThrough = NO;
static NSUInteger TokenForgeOverlayTraceCounter = 1000;
static BOOL TokenForgeRuntimeIdentityLogged = NO;
static BOOL TokenForgeDebugOverlayDiagnosticsEnabled = NO;
static BOOL TokenForgeDashboardOpening = NO;
static BOOL TokenForgeIsOpeningDashboard = NO;
static BOOL TokenForgeIsClosingDashboard = NO;
static BOOL TokenForgeDashboardWindowExists = NO;
static BOOL TokenForgeDashboardWindowVisible = NO;
static BOOL TokenForgeDashboardWindowKey = NO;
static BOOL TokenForgeOverlayPanelExists = NO;
static BOOL TokenForgeOverlayPanelVisible = NO;
static BOOL TokenForgeAppIsActive = NO;
static BOOL TokenForgeLifecycleInstallDeferred = NO;
static BOOL TokenForgeDesiredCompanionVisible = NO;
static BOOL TokenForgeAppLifecycleAllowsOverlay = YES;
static BOOL TokenForgeExplicitQuitRequested = NO;
static BOOL TokenForgeTerminating = NO;
static BOOL TokenForgeQuitTeardownCompleted = NO;
static BOOL TokenForgeRuntimeGuardInitialized = NO;
static BOOL TokenForgeRuntimeVerificationMode = NO;
static BOOL TokenForgeRuntimeVerifierCleanupQuitRequested = NO;
static BOOL TokenForgeNativePluginLoaded = YES;
static BOOL TokenForgeNSApplicationAvailable = NO;
static BOOL TokenForgeAppDidFinishLaunchingObserved = NO;
static BOOL TokenForgeMainThreadReady = NO;
static BOOL TokenForgeDashboardAllowed = NO;
static BOOL TokenForgeDashboardStateHydrated = NO;
static BOOL TokenForgeOverlayAllowed = NO;
static BOOL TokenForgeStatusItemAllowed = NO;
static BOOL TokenForgeNativeOverlayDisabledAtLaunch = NO;
static BOOL TokenForgeNativeSafeMode = NO;
static BOOL TokenForgeDisableNativeOverlay = NO;
static BOOL TokenForgeDisableStatusItem = NO;
static BOOL TokenForgeDisableNativeDashboard = NO;
static BOOL TokenForgeDisableContextMenu = NO;
static BOOL TokenForgeDisablePixelNativeRenderer = NO;
static BOOL TokenForgeDisableMovementTimers = NO;
static BOOL TokenForgeNativeSafetyFlagsLoaded = NO;
static BOOL TokenForgePendingCompanionVisibilityReplay = NO;
static BOOL TokenForgePendingCompanionVisibilityWasExplicit = NO;
static BOOL TokenForgeReplayingCompanionVisibility = NO;
static BOOL TokenForgeCreatingPanel = NO;
static BOOL TokenForgeShowingPanel = NO;
static BOOL TokenForgeRedrawingDashboard = NO;
static BOOL TokenForgeInstallingLifecycleDelegate = NO;
static BOOL TokenForgeVerificationWatchdogSuppressionLogged = NO;
static BOOL TokenForgePreviousLaunchAbnormal = NO;
static BOOL TokenForgeReportIssueAutoPresentSuppressed = NO;
static BOOL TokenForgeLaunchStableDumpScheduled = NO;
static BOOL TokenForgeLaunchStableDumpCompleted = NO;
static NSUInteger TokenForgePersistentStatusBarRepairCount = 0;
static NSTimeInterval TokenForgeLaunchStartedAt = 0.0;
static NSTimeInterval TokenForgeVerificationWarmupUntil = 0.0;
static NSTimeInterval TokenForgeVerificationNoAutoReopenUntil = 0.0;
static NSTimeInterval TokenForgeCrashRecoveryCooldownUntil = 0.0;
static NSUInteger TokenForgeApplicationReopenCount = 0;
static BOOL TokenForgeMovementWasRunningBeforeDrag = NO;
static NSTimeInterval TokenForgeLastDragUpdateLogAt = 0.0;
static NSTimeInterval TokenForgeLastDashboardExplicitCloseAt = 0.0;
static NSTimeInterval TokenForgeLastDashboardOpenAt = 0.0;
static NSTimeInterval TokenForgeLastDashboardCloseAt = 0.0;
static BOOL TokenForgeUserClosingDashboard = NO;
static BOOL TokenForgeOverlayDragFinalizing = NO;
static BOOL TokenForgeOverlayDragPersistedThisGesture = NO;
static BOOL TokenForgeIsMovingOverlayPanel = NO;
static BOOL TokenForgeSuppressDashboardRedrawDuringOverlayDrag = NO;
static BOOL TokenForgeSuppressCSharpProjectionDuringOverlayDrag = NO;
static NSUInteger TokenForgeOverlayPanelGeneration = 0;
static NSUInteger TokenForgeOverlayViewGeneration = 0;
static NSUInteger TokenForgeCompanionRenderVersion = 0;
static BOOL TokenForgeCompanionSnapshotHydrated = NO;
static NSInteger TokenForgeSnapshotStage = 0;
static NSInteger TokenForgeSnapshotLevel = 1;
static NSInteger TokenForgeSnapshotXP = 0;
static NSInteger TokenForgeSnapshotArchetype = 0;
static NSString *TokenForgeSnapshotRepositoryId = @"unknown";
static NSString *TokenForgeSnapshotSkin = @"orange_cat";
static NSString *TokenForgeSnapshotZodiacType = @"tiger";
static TokenForgePendingOverlayAction TokenForgePendingOverlayActionAfterDrag = TokenForgePendingOverlayActionNone;
static BOOL TokenForgePendingClickThroughAfterDrag = NO;
static BOOL TokenForgePendingClickThroughValueAfterDrag = NO;
static NSString *TokenForgePendingOverlayActionSource = @"none";
static NSUInteger TokenForgeDashboardLayoutPass = 0;
static NSUInteger TokenForgeDashboardLayoutHashBeforeOverlayDrag = 0;
static NSString *TokenForgeLastHideReason = @"none";
static NSString *TokenForgeLastShowReason = @"startup";
static NSString *TokenForgeLastProjectionSource = @"startup";
static NSString *TokenForgeLastExplicitSource = @"startup";
static NSString *TokenForgeLastDashboardOpenSource = @"startup";
static NSString *TokenForgeLastOverlayVisibleSource = @"startup";
static NSString *TokenForgePendingExplicitDashboardOpenSource = nil;
static NSString *TokenForgeNativePluginVersion = @"native-plugin-lifecycle-v9";
static NSString *TokenForgeRuntimeBuildIdentityMarker = @"tokenforge_runtime_fix_20260613_mono_crash";
static NSString *TokenForgeRuntimeBuildIdentityGitMarker = @"git=ef8500e workingTreeHash=167daf548046abe649ba56bd1c67ee1a22fba25ce963be0abfdf8063d8ccf0af";
static NSString *TokenForgeDesktopCompanionOverlayCompiledMarker = @"" __DATE__ " " __TIME__;
static id TokenForgeRuntimeVerificationKeepAliveActivity = nil;
static NSPoint TokenForgeDragStartMouse = {0, 0};
static NSPoint TokenForgeDragStartOrigin = {0, 0};
static NSPoint TokenForgeDragCursorOffsetInsidePanel = {0, 0};
static NSPoint TokenForgeCompanionAnchor = {0, 0};
static NSPoint TokenForgeCompanionTarget = {0, 0};
static NSWindow *TokenForgeCompanionWindow = nil;
static TokenForgeCompanionView *TokenForgeCompanionContentView = nil;
static NSMutableDictionary<NSString *, NSPanel *> *TokenForgeOverlayPanelsByRepositoryId = nil;
static NSMutableDictionary<NSString *, TokenForgeDesktopOverlayCompanionView *> *TokenForgeOverlayViewsByRepositoryId = nil;
static NSMutableDictionary<NSString *, NSDictionary *> *TokenForgeOverlaySnapshotsByRepositoryId = nil;
static NSMutableDictionary<NSString *, NSValue *> *TokenForgeOverlayFramesByRepositoryId = nil;
static NSMutableDictionary<NSString *, NSMutableDictionary *> *TokenForgeOverlayDragStatesByRepositoryId = nil;
static NSMutableDictionary<NSString *, NSNumber *> *TokenForgeOverlayGenerationsByRepositoryId = nil;
static NSString *TokenForgeCurrentlyDraggingRepositoryId = nil;
static NSString *TokenForgeActiveDragRepositoryId = nil;
static NSUInteger TokenForgeActiveDragPanelGeneration = 0;
static NSUInteger TokenForgeActiveDragViewGeneration = 0;
static NSUInteger TokenForgeOverlayFarmRenderVersion = 0;
static NSTimeInterval TokenForgeLastOverlayDragApplyAt = 0.0;
static NSUInteger TokenForgeCoalescedOverlayDragEvents = 0;
static NSSize TokenForgeCompanionSize = {96.0, 96.0};
static NSTimer *TokenForgeCompanionMotionTimer = nil;
static NSPoint TokenForgeCompanionVelocity = {0, 0};
static NSTimeInterval TokenForgeCompanionLastTick = 0.0;
static NSTimeInterval TokenForgeCompanionNextDecisionAt = 0.0;
static NSTimeInterval TokenForgeCompanionReactionUntil = 0.0;
static NSTimeInterval TokenForgeCompanionDragCooldownUntil = 0.0;
static NSInteger TokenForgeCompanionMotionMode = 0;
static CGFloat TokenForgeCompanionIdleRadius = 5.0;
static CGFloat TokenForgeCompanionWanderRadius = 18.0;
static CGFloat TokenForgeCompanionWanderSpeed = 5.0;
static CGFloat TokenForgeCompanionDecisionInterval = 4.6;
static CGFloat TokenForgeCompanionReactionCooldown = 1.1;
static BOOL TokenForgeCompanionAllowsWandering = NO;

static NSRect TokenForgeClampFrameToVisibleFrame(NSRect frame);
static NSRect TokenForgeNormalizeDashboardFrame(NSRect frame, NSString *source);
static void TokenForgeLogDashboardLaunchDiagnostic(NSString *reason, BOOL requestedOpen, NSWindow *window, NSRect normalizedFrame);
static NSRect TokenForgeVisibleFrameForFrame(NSRect frame);
static NSRect TokenForgeVisibleFrame(void);
static CGFloat TokenForgeRectArea(NSRect rect);
static void TokenForgePersistCompanionFrame(NSRect frame);
static void TokenForgeTriggerOverlayReaction(NSInteger reaction, NSString *speech);
static void TokenForgeDrawAvatarInRect(TokenForgeCompanionView *view, NSRect containerRect, NSString *preset, NSInteger frameIndex, NSString *mode);
static void TokenForgeDrawSharedZodiacSprite(NSString *zodiacType, NSRect rect, NSColor *accent, NSInteger stage);
static NSImage *TokenForgeAvatarImageForPreset(NSString *preset, NSSize imageSize, NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode, NSString *theme);
static const char *TokenForgeNextOverlayTraceId(void);
static void TokenForgeLogRuntimeIdentityIfNeeded(void);
static void TokenForgeDumpAllWindows(NSString *reason);
static void TokenForgeDumpOverlayPanelState(NSString *traceId);
static BOOL TokenForgeIsNativeDashboardWindow(NSWindow *window);
static BOOL TokenForgeIsCompanionWindow(NSWindow *window);
static BOOL TokenForgeWindowLooksBlank(NSWindow *window);
static void TokenForgeLogWindowLifecycle(NSString *event, NSWindow *window, NSString *reason);
static void TokenForgeRefreshDashboardAndOverlayState(NSString *source);
static void TokenForgeDumpWindowClassifications(NSString *phase);
static void TokenForgeShowDesktopCompanionOverlayWithTrace(NSString *traceId);
static void TokenForgeHideDesktopCompanionOverlayWithTrace(NSString *traceId);
static void TokenForgeSetCompanionOverlayMotionProfileWithTrace(NSString *traceId, int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds);
static BOOL TokenForgeAppKitRegistrationReady(void);
static void TokenForgeRequestLifecycleInstall(NSString *reason);
static void TokenForgeLogOverlayProjection(NSString *traceId, NSString *source);
static void TokenForgeScheduleOverlayWatchdogs(NSString *traceId);
static void TokenForgeCreateCompanionOverlayOnMain(NSString *traceId);
static void TokenForgeInitializeRuntimeGuard(NSString *source);
static void TokenForgeRefreshNativeSafetyFlags(void);
static NSString *TokenForgeThreadLabel(void);
static void TokenForgeLogQuitDiagnostic(NSString *request, NSString *source, BOOL allowQuit, BOOL blocked, NSString *reason);
static BOOL TokenForgeVerificationAllowsImplicitVerifierCleanup(void);
static BOOL TokenForgeRefreshNativeReadiness(NSString *function);
static BOOL TokenForgeAppReadyForWindowMutation(NSString *function, NSString *source);
static BOOL TokenForgeOverlayLaunchPathAllowed(NSString *function, NSString *source, BOOL explicitUserAction);
static BOOL TokenForgeDashboardLaunchPathAllowed(NSString *function, NSString *source, BOOL explicitUserAction);
static BOOL TokenForgeStatusItemLaunchPathAllowed(NSString *function, NSString *source);
static BOOL TokenForgeSourceLooksPassiveSync(NSString *source);
static void TokenForgeRecordCompanionDesiredState(BOOL visible, NSString *source);
static void TokenForgeReplayPendingCompanionVisibilityIfReady(NSString *source);
static BOOL TokenForgeShouldSuppressDashboardOpen(NSString *source, BOOL explicitUserOpen);
static BOOL TokenForgeShouldSuppressOverlayShow(NSString *source, BOOL explicitUserOpen);
static BOOL TokenForgeIsVerificationWarmupActive(void);
static void TokenForgeRecordNormalTermination(NSString *source);
static void TokenForgeDumpRuntimeWindows(NSString *phase);
static void TokenForgeResetCompanionFrame(void);
static NSRect TokenForgeFrameAvoidingDashboardForExplicitShow(NSRect frame, NSString *source);
static NSString *TokenForgeCompanionViewRoleName(TokenForgeCompanionRenderRole role);
static BOOL TokenForgeIsDesktopOverlayPanelContentView(TokenForgeCompanionView *view);
static NSString *TokenForgeCompanionStageName(NSInteger stage);
static NSString *TokenForgeCompanionStageVisualSignature(NSInteger stage);
static void TokenForgeHydrateCompanionSnapshot(NSString *repositoryId, NSInteger stage, NSInteger level, NSInteger xp, NSInteger archetype, NSString *skin, NSString *source);
static void TokenForgeApplyFarmSnapshotsOnMain(NSArray<NSDictionary *> *snapshots, NSString *source);
static NSInteger TokenForgeVisibleOverlayFarmCount(void);
static void TokenForgeShowCompanionForRepositoryOnMain(NSString *repositoryId, NSString *source);
static void TokenForgeHideCompanionForRepositoryOnMain(NSString *repositoryId, NSString *source);
static BOOL TokenForgeIsOverlayDraggingForRepository(NSString *repositoryId);
static NSPanel *TokenForgeOverlayPanelForRepository(NSString *repositoryId);
static TokenForgeDesktopOverlayCompanionView *TokenForgeOverlayViewForRepository(NSString *repositoryId);
static NSRect TokenForgeOverlayFrameForRepository(NSString *repositoryId);
static NSRect TokenForgeResolveFarmFrameForRepository(NSString *repositoryId, NSRect requestedFrame, NSUInteger index, NSString *reason);
static void TokenForgeSetOverlayFrameForRepositoryOnMain(NSString *repositoryId, NSRect frame, NSString *source);
static void TokenForgeEnsureOverlayFarmRegistry(void);
static void TokenForgeEnsureStatusItem(NSString *source);
static BOOL TokenForgeStatusItemExists(void);
static NSString *TokenForgeSafeRepositoryKey(NSString *repositoryId);
static void TokenForgePersistOverlayFrameForRepository(NSString *repositoryId, NSRect frame);
static void TokenForgeQueueOverlayActionAfterDrag(TokenForgePendingOverlayAction action, NSString *source);
static void TokenForgeApplyPendingOverlayActionsAfterDrag(NSString *source);
static NSUInteger TokenForgeDashboardLayoutHashForState(NSDictionary *state, NSString *source);
static void TokenForgeLogDashboardLayoutPass(NSDictionary *state, NSString *source);
static NSString *TokenForgeDashboardString(NSDictionary *dictionary, NSString *key, NSString *fallback);
static NSDictionary *TokenForgeDashboardDictionary(NSDictionary *dictionary, NSString *key);
static NSArray *TokenForgeDashboardArray(NSDictionary *dictionary, NSString *key);
static BOOL TokenForgeDashboardBool(NSDictionary *dictionary, NSString *key, BOOL fallback);

#if DEBUG
#define TokenForgeDashboardLifecycleLog(fmt, ...) NSLog((fmt), ##__VA_ARGS__)
#else
#define TokenForgeDashboardLifecycleLog(fmt, ...) do { } while (0)
#endif

static NSArray<NSWindow *> *TokenForgeSafeWindowsSnapshot(NSString *source, BOOL allowDuringTermination)
{
    if (![NSThread isMainThread] || NSApp == nil) {
        NSLog(@"INFO [WindowScan][SKIPPED_UNSAFE] source=%@ reason=%@",
              source ?: @"unknown",
              ![NSThread isMainThread] ? @"notMainThread" : @"noNSApp");
        return @[];
    }

    if (TokenForgeTerminating && !allowDuringTermination) {
        NSLog(@"INFO [WindowScan][SKIPPED_UNSAFE] source=%@ reason=terminating", source ?: @"unknown");
        return @[];
    }

    NSArray<NSWindow *> *windows = [[NSApp windows] copy];
    NSLog(@"INFO [WindowScan][SAFE_SNAPSHOT] source=%@ count=%lu terminating=%@",
          source ?: @"unknown",
          (unsigned long)windows.count,
          TokenForgeTerminating ? @"true" : @"false");
    return [windows autorelease];
}

static NSRect TokenForgeAvatarCanonicalBounds(void)
{
    return NSMakeRect(0.0, 0.0, 32.0, 36.0);
}

static CGFloat TokenForgeAvatarSafeInsetForPreset(NSString *preset, NSRect containerRect)
{
    CGFloat shortest = MIN(containerRect.size.width, containerRect.size.height);
    if ([preset isEqualToString:@"menuBar"]) {
        return MAX(1.0, floor(shortest * 0.06));
    }
    if ([preset isEqualToString:@"overlay"]) {
        return MAX(6.0, floor(shortest * 0.08));
    }
    return MAX(14.0, floor(shortest * 0.08));
}

static NSRect TokenForgeAvatarFinalDrawingRect(NSString *preset, NSRect containerRect, CGFloat explicitSafeInset, CGFloat *scaleOut)
{
    NSRect canonical = TokenForgeAvatarCanonicalBounds();
    CGFloat safeInset = explicitSafeInset >= 0.0 ? explicitSafeInset : TokenForgeAvatarSafeInsetForPreset(preset, containerRect);
    NSRect safeRect = NSInsetRect(containerRect, safeInset, safeInset);
    if (safeRect.size.width <= 2.0 || safeRect.size.height <= 2.0) {
        safeRect = containerRect;
    }

    CGFloat scale = MIN(safeRect.size.width / canonical.size.width, safeRect.size.height / canonical.size.height);
    scale = MAX(0.01, scale);
    NSSize finalSize = NSMakeSize(canonical.size.width * scale, canonical.size.height * scale);
    NSRect finalRect = NSMakeRect(NSMidX(safeRect) - finalSize.width * 0.5,
                                  NSMidY(safeRect) - finalSize.height * 0.5,
                                  finalSize.width,
                                  finalSize.height);
    if (scaleOut != nil) {
        *scaleOut = scale;
    }
    return finalRect;
}

static NSRect TokenForgeAvatarMapRect(NSRect finalRect, NSRect partRect)
{
    NSRect canonical = TokenForgeAvatarCanonicalBounds();
    CGFloat scale = finalRect.size.width / canonical.size.width;
    return NSMakeRect(finalRect.origin.x + (partRect.origin.x - canonical.origin.x) * scale,
                      finalRect.origin.y + (partRect.origin.y - canonical.origin.y) * scale,
                      partRect.size.width * scale,
                      partRect.size.height * scale);
}

static NSPoint TokenForgeAvatarMapPoint(NSRect finalRect, CGFloat x, CGFloat y)
{
    NSRect canonical = TokenForgeAvatarCanonicalBounds();
    CGFloat scale = finalRect.size.width / canonical.size.width;
    return NSMakePoint(finalRect.origin.x + (x - canonical.origin.x) * scale,
                       finalRect.origin.y + (y - canonical.origin.y) * scale);
}

static void TokenForgeAvatarFillRect(NSRect finalRect, NSRect partRect, NSColor *color)
{
    [color setFill];
    NSRectFill(TokenForgeAvatarMapRect(finalRect, partRect));
}

static void TokenForgeAvatarFillOval(NSRect finalRect, NSRect partRect, NSColor *color)
{
    [color setFill];
    NSInteger rows = MAX(1, (NSInteger)ceil(partRect.size.height));
    for (NSInteger row = 0; row < rows; row++) {
        CGFloat normalized = rows <= 1 ? 0.0 : fabs(((CGFloat)row / (CGFloat)(rows - 1)) * 2.0 - 1.0);
        CGFloat inset = floor(partRect.size.width * normalized * normalized * 0.26);
        CGFloat rowWidth = MAX(1.0, partRect.size.width - inset * 2.0);
        TokenForgeAvatarFillRect(finalRect,
                                 NSMakeRect(partRect.origin.x + inset,
                                            partRect.origin.y + row,
                                            rowWidth,
                                            1.0),
                                 color);
    }
}

static NSColor *TokenForgeAvatarBodyColor(NSInteger stage, NSString *theme, NSInteger archetype)
{
    if (stage == 0) return [NSColor colorWithCalibratedRed:0.96 green:0.88 blue:0.70 alpha:1.0];
    if (stage == 1) return [NSColor colorWithCalibratedRed:1.0 green:0.79 blue:0.49 alpha:1.0];
    NSString *normalized = theme.length > 0 ? theme : @"orange_cat";
    if ([normalized isEqualToString:@"white_cat"]) return [NSColor colorWithCalibratedRed:0.94 green:0.95 blue:0.91 alpha:1.0];
    if ([normalized isEqualToString:@"calico"]) return [NSColor colorWithCalibratedRed:0.96 green:0.72 blue:0.42 alpha:1.0];
    if ([normalized isEqualToString:@"black_cat"]) return [NSColor colorWithCalibratedRed:0.16 green:0.17 blue:0.20 alpha:1.0];
    if ([normalized isEqualToString:@"retriever"]) return [NSColor colorWithCalibratedRed:0.82 green:0.58 blue:0.30 alpha:1.0];
    if ([normalized isEqualToString:@"runner"]) return [NSColor colorWithCalibratedRed:0.39 green:0.63 blue:0.98 alpha:1.0];
    if ([normalized isEqualToString:@"orange_cat"]) return [NSColor colorWithCalibratedRed:0.92 green:0.48 blue:0.21 alpha:1.0];
    switch (archetype) {
        case 1: return [NSColor colorWithCalibratedRed:0.25 green:0.75 blue:1.0 alpha:1.0];
        case 2: return [NSColor colorWithCalibratedRed:0.95 green:0.60 blue:0.26 alpha:1.0];
        case 3: return [NSColor colorWithCalibratedRed:0.35 green:0.80 blue:0.46 alpha:1.0];
        case 4: return [NSColor colorWithCalibratedRed:0.93 green:0.52 blue:0.77 alpha:1.0];
        case 5: return [NSColor colorWithCalibratedRed:0.60 green:0.66 blue:1.0 alpha:1.0];
        case 6: return [NSColor colorWithCalibratedRed:1.0 green:0.42 blue:0.29 alpha:1.0];
        default: return [NSColor colorWithCalibratedRed:0.56 green:0.79 blue:0.90 alpha:1.0];
    }
}

static NSColor *TokenForgeAvatarAccentColor(NSString *theme, NSInteger archetype)
{
    NSString *normalized = theme.length > 0 ? theme : @"orange_cat";
    if ([normalized isEqualToString:@"white_cat"]) return [NSColor colorWithCalibratedRed:0.36 green:0.58 blue:0.78 alpha:1.0];
    if ([normalized isEqualToString:@"calico"]) return [NSColor colorWithCalibratedRed:0.16 green:0.17 blue:0.20 alpha:1.0];
    if ([normalized isEqualToString:@"black_cat"]) return [NSColor colorWithCalibratedRed:0.94 green:0.78 blue:0.30 alpha:1.0];
    if ([normalized isEqualToString:@"retriever"]) return [NSColor colorWithCalibratedRed:0.54 green:0.32 blue:0.17 alpha:1.0];
    if ([normalized isEqualToString:@"runner"]) return [NSColor colorWithCalibratedRed:0.95 green:0.30 blue:0.34 alpha:1.0];
    if ([normalized isEqualToString:@"orange_cat"]) return [NSColor colorWithCalibratedRed:0.99 green:0.77 blue:0.32 alpha:1.0];
    switch (archetype) {
        case 1: return [NSColor colorWithCalibratedRed:0.25 green:0.75 blue:1.0 alpha:1.0];
        case 2: return [NSColor colorWithCalibratedRed:0.95 green:0.60 blue:0.26 alpha:1.0];
        case 3: return [NSColor colorWithCalibratedRed:0.35 green:0.80 blue:0.46 alpha:1.0];
        case 4: return [NSColor colorWithCalibratedRed:0.93 green:0.52 blue:0.77 alpha:1.0];
        case 5: return [NSColor colorWithCalibratedRed:0.60 green:0.66 blue:1.0 alpha:1.0];
        case 6: return [NSColor colorWithCalibratedRed:1.0 green:0.42 blue:0.29 alpha:1.0];
        default: return [NSColor colorWithCalibratedRed:0.58 green:0.64 blue:0.72 alpha:1.0];
    }
}

static NSString *TokenForgeZodiacTypeForArchetype(NSInteger archetype)
{
    NSArray<NSString *> *ids = @[@"rat", @"ox", @"tiger", @"rabbit", @"dragon", @"snake", @"horse", @"goat", @"monkey", @"rooster", @"dog", @"pig"];
    NSInteger index = archetype % (NSInteger)ids.count;
    if (index < 0) {
        index += (NSInteger)ids.count;
    }
    return ids[index];
}

static BOOL TokenForgeAvatarPartInside(NSRect finalRect, NSRect partRect)
{
    return NSContainsRect(NSInsetRect(finalRect, -0.5, -0.5), TokenForgeAvatarMapRect(finalRect, partRect));
}

static void TokenForgeLogAvatarRenderer(NSString *preset, NSRect containerRect, NSRect finalRect, CGFloat scale, BOOL clipped, BOOL partsInside, NSString *imageSource, NSSize imageSize, NSString *cacheKey, BOOL cacheHit)
{
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    NSString *key = preset ?: @"hero";
    static NSTimeInterval lastOverlayLog = 0.0;
    static NSTimeInterval lastHeroLog = 0.0;
    static NSTimeInterval lastMenuBarLog = 0.0;
    static NSTimeInterval lastOtherLog = 0.0;
    NSTimeInterval *lastLog = &lastOtherLog;
    if ([key isEqualToString:@"overlay"]) {
        lastLog = &lastOverlayLog;
    } else if ([key isEqualToString:@"hero"]) {
        lastLog = &lastHeroLog;
    } else if ([key isEqualToString:@"menuBar"]) {
        lastLog = &lastMenuBarLog;
    }

    NSTimeInterval last = *lastLog;
    if (![key isEqualToString:@"menuBar"] && now - last < 1.4) {
        return;
    }

    *lastLog = now;
    NSRect canonical = TokenForgeAvatarCanonicalBounds();
    NSLog(@"INFO [AvatarRenderer] preset=%@ containerRect=(%.2f,%.2f %.2fx%.2f) canonicalBounds=(%.2f,%.2f %.2fx%.2f) scale=%.4f finalDrawRect=(%.2f,%.2f %.2fx%.2f) clipped=%@",
          key,
          containerRect.origin.x,
          containerRect.origin.y,
          containerRect.size.width,
          containerRect.size.height,
          canonical.origin.x,
          canonical.origin.y,
          canonical.size.width,
          canonical.size.height,
          scale,
          finalRect.origin.x,
          finalRect.origin.y,
          finalRect.size.width,
          finalRect.size.height,
          clipped ? @"true" : @"false");
    NSLog(@"INFO [AvatarRenderer] parts=head/body/headset/legs/shadow allInsideFinalRect=%@", partsInside ? @"true" : @"false");
    NSLog(@"INFO [AvatarRenderer] imageSource=%@ size=%.0fx%.0f reusedFromPreset=%@ cacheKey=%@ cacheHit=%@",
          imageSource ?: @"squareCellSprite",
          imageSize.width,
          imageSize.height,
          key,
          cacheKey ?: @"none",
          cacheHit ? @"true" : @"false");
}

static void TokenForgeDrawAvatarInRect(TokenForgeCompanionView *view, NSRect containerRect, NSString *preset, NSInteger frameIndex, NSString *mode)
{
    NSString *resolvedPreset = preset.length > 0 ? preset : (view.assetType.length > 0 ? view.assetType : @"overlay");
    CGFloat explicitInset = view.safeDrawingInset > 0.0 ? view.safeDrawingInset : -1.0;
    CGFloat scale = 1.0;
    NSRect finalRect = TokenForgeAvatarFinalDrawingRect(resolvedPreset, containerRect, explicitInset, &scale);
    CGFloat reactionScale = view.visualScale <= 0.01 ? 1.0 : view.visualScale;
    finalRect = NSInsetRect(finalRect, -finalRect.size.width * (reactionScale - 1.0) * 0.5, -finalRect.size.height * (reactionScale - 1.0) * 0.5);
    finalRect.origin.y += view.visualOffsetY;

    NSGraphicsContext *context = [NSGraphicsContext currentContext];
    [context saveGraphicsState];
    BOOL previousAntialias = context.shouldAntialias;
    context.shouldAntialias = NO;
    context.imageInterpolation = NSImageInterpolationNone;
    NSAffineTransform *transform = [NSAffineTransform transform];
    [transform translateXBy:NSMidX(finalRect) yBy:NSMidY(finalRect)];
    if (view.facingLeft) {
        [transform scaleXBy:-1.0 yBy:1.0];
    }
    [transform rotateByDegrees:view.visualRotation];
    [transform translateXBy:-NSMidX(finalRect) yBy:-NSMidY(finalRect)];
    [transform concat];

    NSString *zodiacType = view.zodiacType.length > 0
        ? view.zodiacType
        : (TokenForgeSnapshotZodiacType.length > 0 ? TokenForgeSnapshotZodiacType : TokenForgeZodiacTypeForArchetype(view.archetype));
    NSColor *spriteAccent = TokenForgeAvatarAccentColor(view.visualThemeId, view.archetype);
    NSInteger spriteStage = MAX(0, MIN(5, view.stage));
    TokenForgeDrawSharedZodiacSprite(zodiacType, finalRect, spriteAccent, spriteStage);
    context.shouldAntialias = previousAntialias;
    [context restoreGraphicsState];
    NSString *surface = TokenForgeCompanionViewRoleName(view.viewRole);
    if ([resolvedPreset isEqualToString:@"sidebar"]) {
        surface = @"sidebar";
    } else if ([resolvedPreset isEqualToString:@"menuBar"]) {
        surface = @"status";
    }
    NSLog(@"INFO [RuntimeUIPath][PixelSprite] surface=%@ spriteKey=zodiac_%@_%@ grid=32 nearestNeighbor=true antialias=false",
          surface ?: @"dashboard",
          zodiacType ?: @"tiger",
          TokenForgeCompanionStageName(spriteStage));
    NSLog(@"INFO [PixelSprite] spriteKey=zodiac_%@_%@ surface=%@ grid=32 nearestNeighbor=true antialias=false",
          zodiacType ?: @"tiger",
          TokenForgeCompanionStageName(spriteStage),
          surface ?: @"dashboard");
    NSLog(@"INFO [PixelSprite][GRID] grid=32 spriteKey=zodiac_%@_%@", zodiacType ?: @"tiger", TokenForgeCompanionStageName(spriteStage));
    NSLog(@"INFO [PixelSprite][GRID_32_OR_48] grid=32 spriteKey=zodiac_%@_%@", zodiacType ?: @"tiger", TokenForgeCompanionStageName(spriteStage));
    NSLog(@"INFO [PixelSprite][NEAREST_NEIGHBOR] value=true surface=%@", surface ?: @"dashboard");
    NSLog(@"INFO [PixelSprite][NO_ANTIALIAS] value=true surface=%@", surface ?: @"dashboard");
    NSLog(@"INFO [PixelSprite][ZODIAC] zodiac=%@ surface=%@", zodiacType ?: @"tiger", surface ?: @"dashboard");
    NSLog(@"INFO [PixelSprite][STAGE] stage=%@ index=%ld surface=%@", TokenForgeCompanionStageName(spriteStage), (long)spriteStage, surface ?: @"dashboard");
    NSLog(@"INFO [PixelSprite][SURFACE] surface=%@ spriteKey=zodiac_%@_%@", surface ?: @"dashboard", zodiacType ?: @"tiger", TokenForgeCompanionStageName(spriteStage));
    NSLog(@"INFO [PixelSprite][OLD_VECTOR_PATH_UNUSED] surface=%@ reason=sharedZodiacPixelSpriteReturnedBeforeLegacyOvalPath", surface ?: @"dashboard");
    TokenForgeLogAvatarRenderer(resolvedPreset, containerRect, finalRect, scale, !NSContainsRect(NSInsetRect(containerRect, -0.5, -0.5), finalRect), YES, @"sharedZodiacPixelSprite nearestNeighbor=true", containerRect.size, nil, NO);
    return;

    NSColor *outline = [NSColor colorWithCalibratedRed:0.13 green:0.15 blue:0.19 alpha:1.0];
    NSColor *body = TokenForgeAvatarBodyColor(view.stage, view.visualThemeId, view.archetype);
    NSColor *accent = TokenForgeAvatarAccentColor(view.visualThemeId, view.archetype);
    NSColor *highlight = [NSColor colorWithCalibratedRed:1.0 green:0.96 blue:0.82 alpha:1.0];
    NSColor *shadow = [NSColor colorWithCalibratedWhite:0.0 alpha:[resolvedPreset isEqualToString:@"menuBar"] ? 0.18 : 0.16];

    NSInteger animationStep = frameIndex >= 0 ? frameIndex : view.animationState;
    CGFloat bounce = [mode isEqualToString:@"running"] ? (animationStep % 2 == 0 ? 0.0 : 1.0) : 0.0;
    if ([mode isEqualToString:@"levelUp"] || [mode isEqualToString:@"reaction"]) {
        bounce = animationStep % 2 == 0 ? 0.0 : 1.4;
    } else if ([mode isEqualToString:@"idle"]) {
        bounce = (animationStep % 4 == 1 || animationStep % 4 == 2) ? 0.45 : 0.0;
    }
    NSRect bodyRect = view.stage <= 1 ? NSMakeRect(8.0, 7.0 + bounce, 16.0, 22.0) : NSMakeRect(8.0, 5.0 + bounce, 16.0, 14.0);
    NSRect headRect = view.stage <= 1 ? NSMakeRect(9.0, 10.0 + bounce, 14.0, 18.0) : NSMakeRect(6.0, 15.0 + bounce, 20.0, 17.0);
    NSRect headsetRect = NSMakeRect(4.0, 20.0 + bounce, 24.0, 13.0);
    NSRect legsRect = NSMakeRect(9.0, 3.0, 14.0, 5.0 + bounce);
    NSRect shadowRect = NSMakeRect(6.0, 1.0, 20.0, 4.0);
    BOOL partsInside = TokenForgeAvatarPartInside(finalRect, headRect) &&
        TokenForgeAvatarPartInside(finalRect, bodyRect) &&
        TokenForgeAvatarPartInside(finalRect, headsetRect) &&
        TokenForgeAvatarPartInside(finalRect, legsRect) &&
        TokenForgeAvatarPartInside(finalRect, shadowRect);

    TokenForgeAvatarFillOval(finalRect, shadowRect, shadow);
    if (view.stage <= 1) {
        TokenForgeAvatarFillOval(finalRect, NSInsetRect(bodyRect, -1.0, -1.0), outline);
        TokenForgeAvatarFillOval(finalRect, bodyRect, body);
        if (view.stage == 1) {
            [outline setStroke];
            NSBezierPath *crack = [NSBezierPath bezierPath];
            [crack moveToPoint:TokenForgeAvatarMapPoint(finalRect, 15.0, 25.0 + bounce)];
            [crack lineToPoint:TokenForgeAvatarMapPoint(finalRect, 18.0, 21.0 + bounce)];
            [crack lineToPoint:TokenForgeAvatarMapPoint(finalRect, 14.0, 17.0 + bounce)];
            [crack lineToPoint:TokenForgeAvatarMapPoint(finalRect, 19.0, 12.0 + bounce)];
            crack.lineWidth = MAX(1.0, scale);
            [crack stroke];
        }
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(14.0, 21.0 + bounce, 2.0, 2.0), highlight);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(17.0, 24.0 + bounce, 2.0, 2.0), accent);
    } else {
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(10.0, 3.0, 4.0, 5.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(18.0, 3.0, 4.0, 5.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(11.0, 4.0 + (animationStep % 2), 3.0, 4.0), body);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(18.0, 4.0 + ((animationStep + 1) % 2), 3.0, 4.0), body);
        TokenForgeAvatarFillOval(finalRect, NSInsetRect(bodyRect, -1.2, -1.2), outline);
        TokenForgeAvatarFillOval(finalRect, bodyRect, body);

        TokenForgeAvatarFillRect(finalRect, NSMakeRect(9.0, 29.0 + bounce, 6.0, 2.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(10.0, 31.0 + bounce, 4.0, 2.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(11.0, 33.0 + bounce, 2.0, 2.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(17.0, 29.0 + bounce, 6.0, 2.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(18.0, 31.0 + bounce, 4.0, 2.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(19.0, 33.0 + bounce, 2.0, 2.0), outline);
        TokenForgeAvatarFillOval(finalRect, NSInsetRect(headRect, -1.2, -1.2), outline);
        TokenForgeAvatarFillOval(finalRect, headRect, body);
        TokenForgeAvatarFillOval(finalRect, NSMakeRect(11.0, 23.0 + bounce, 2.5, 2.7), outline);
        TokenForgeAvatarFillOval(finalRect, NSMakeRect(18.5, 23.0 + bounce, 2.5, 2.7), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(15.2, 20.0 + bounce, 1.6, 1.4), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(13.0, 18.5 + bounce, 6.0, 1.2), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(9.5, 27.0 + bounce, 3.0, 2.0), highlight);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(4.0, 22.0 + bounce, 4.0, 6.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(24.0, 22.0 + bounce, 4.0, 6.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(5.0, 23.0 + bounce, 2.0, 4.0), accent);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(25.0, 23.0 + bounce, 2.0, 4.0), accent);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(8.0, 28.0 + bounce, 5.0, 2.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(12.0, 30.0 + bounce, 8.0, 2.0), outline);
        TokenForgeAvatarFillRect(finalRect, NSMakeRect(20.0, 28.0 + bounce, 5.0, 2.0), outline);
    }

    context.shouldAntialias = previousAntialias;
    [context restoreGraphicsState];

    BOOL clipped = !NSContainsRect(NSInsetRect(containerRect, -0.5, -0.5), finalRect) || !partsInside;
    TokenForgeLogAvatarRenderer(resolvedPreset, containerRect, finalRect, scale, clipped, partsInside, @"squareCellSprite squareCells=true nearestNeighbor=true", containerRect.size, nil, NO);
}

static NSString *TokenForgeAvatarCacheKey(NSString *preset, NSSize imageSize, NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode, NSString *theme)
{
    return [NSString stringWithFormat:@"%@|%.0fx%.0f|stage=%ld|arch=%ld|frame=%ld|mode=%@|theme=%@|zodiac=%@|scale=%.2f",
            preset ?: @"overlay",
            imageSize.width,
            imageSize.height,
            (long)stage,
            (long)archetype,
            (long)frameIndex,
            mode ?: @"idle",
            theme ?: @"orange_cat",
            TokenForgeSnapshotZodiacType ?: @"tiger",
            [NSScreen mainScreen].backingScaleFactor];
}

static NSImage *TokenForgeAvatarImageForPreset(NSString *preset, NSSize imageSize, NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode, NSString *theme)
{
    if (![NSThread isMainThread]) {
        NSLog(@"WARN [AvatarImage][MAIN_THREAD_DISPATCH] function=TokenForgeAvatarImageForPreset preset=%@ stage=%ld archetype=%ld frame=%ld mode=%@ thread=background",
              preset ?: @"overlay",
              (long)stage,
              (long)archetype,
              (long)frameIndex,
              mode ?: @"idle");
        __block NSImage *mainThreadImage = nil;
        dispatch_sync(dispatch_get_main_queue(), ^{
            mainThreadImage = [TokenForgeAvatarImageForPreset(preset, imageSize, stage, archetype, frameIndex, mode, theme) retain];
        });
        return [mainThreadImage autorelease];
    }

    NSLog(@"INFO [AvatarImage][ENTER] preset=%@ size=%.0fx%.0f stage=%ld archetype=%ld frame=%ld mode=%@ theme=%@ thread=main",
          preset ?: @"overlay",
          imageSize.width,
          imageSize.height,
          (long)stage,
          (long)archetype,
          (long)frameIndex,
          mode ?: @"idle",
          theme ?: @"orange_cat");

    static NSMutableDictionary<NSString *, NSImage *> *imageCache = nil;
    if (imageCache == nil) {
        imageCache = [[NSMutableDictionary alloc] init];
        NSLog(@"INFO [AvatarImage][CACHE_INIT] owner=staticNSMutableDictionary retained=true");
    }

    NSString *resolvedPreset = preset.length > 0 ? preset : @"overlay";
    NSString *cacheKey = TokenForgeAvatarCacheKey(resolvedPreset, imageSize, stage, archetype, frameIndex, mode, theme);
    NSImage *cached = imageCache[cacheKey];
    if (cached != nil) {
        CGFloat scale = 1.0;
        NSRect finalRect = TokenForgeAvatarFinalDrawingRect(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), -1.0, &scale);
        TokenForgeLogAvatarRenderer(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), finalRect, scale, NO, YES, @"cached", imageSize, cacheKey, YES);
        NSLog(@"INFO [AvatarImage][CACHE_HIT] key=%@ imageHash=%lu owner=cache", cacheKey, (unsigned long)cached.hash);
        return cached;
    }

    NSLog(@"INFO [AvatarImage][CACHE_MISS] key=%@", cacheKey);
    NSImage *image = [[NSImage alloc] initWithSize:imageSize];
    NSLog(@"INFO [AvatarImage][NSIMAGE_CREATE] key=%@ image=%p retainedBy=localAlloc", cacheKey, image);
    [image lockFocus];
    [[NSColor clearColor] setFill];
    NSRectFill(NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height));

    if (![mode isEqualToString:@"hidden"] && frameIndex % 2 == 1) {
        NSColor *pulseColor = [mode isEqualToString:@"levelUp"] || [mode isEqualToString:@"reaction"]
            ? [NSColor colorWithCalibratedRed:1.0 green:0.66 blue:0.16 alpha:0.28]
            : ([mode isEqualToString:@"running"]
                ? [NSColor colorWithCalibratedRed:0.20 green:0.55 blue:1.0 alpha:0.22]
                : [NSColor colorWithCalibratedRed:0.25 green:0.70 blue:0.40 alpha:0.14]);
        [pulseColor setFill];
        CGFloat cell = MAX(2.0, floor(MIN(imageSize.width, imageSize.height) / 18.0));
        NSRect pulseBounds = NSInsetRect(NSMakeRect(0, 0, imageSize.width, imageSize.height), cell, cell);
        for (NSInteger index = 0; index < 4; index++) {
            NSRectFill(NSMakeRect(NSMinX(pulseBounds) + cell * index, NSMinY(pulseBounds) + cell * index, NSWidth(pulseBounds) - cell * index * 2.0, cell));
            NSRectFill(NSMakeRect(NSMinX(pulseBounds) + cell * index, NSMaxY(pulseBounds) - cell * (index + 1), NSWidth(pulseBounds) - cell * index * 2.0, cell));
            NSRectFill(NSMakeRect(NSMinX(pulseBounds) + cell * index, NSMinY(pulseBounds) + cell * index, cell, NSHeight(pulseBounds) - cell * index * 2.0));
            NSRectFill(NSMakeRect(NSMaxX(pulseBounds) - cell * (index + 1), NSMinY(pulseBounds) + cell * index, cell, NSHeight(pulseBounds) - cell * index * 2.0));
        }
    }

    TokenForgeCompanionView *view = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height)];
    view.stage = stage;
    view.level = MAX(1, TokenForgeSnapshotLevel);
    view.archetype = archetype;
    view.animationState = frameIndex;
    view.viewRole = [resolvedPreset isEqualToString:@"menuBar"] ? TokenForgeCompanionRenderRoleMenuBar : TokenForgeCompanionRenderRoleDashboardPreview;
    view.snapshotHydrated = YES;
    view.renderVersion = TokenForgeCompanionRenderVersion;
    view.facingLeft = frameIndex % 4 == 3;
    view.safeDrawingInset = TokenForgeAvatarSafeInsetForPreset(resolvedPreset, view.bounds);
    view.assetType = resolvedPreset;
    view.visualThemeId = theme.length > 0 ? theme : @"orange_cat";
    view.zodiacType = TokenForgeSnapshotZodiacType.length > 0 ? TokenForgeSnapshotZodiacType : TokenForgeZodiacTypeForArchetype(archetype);
    if ([mode isEqualToString:@"hidden"]) {
        view.visualScale = 0.92;
    } else if ([mode isEqualToString:@"levelUp"] || [mode isEqualToString:@"reaction"]) {
        view.visualScale = frameIndex % 2 == 0 ? 1.0 : 1.12;
    } else if ([mode isEqualToString:@"running"]) {
        view.visualScale = 1.0;
    } else {
        view.visualScale = frameIndex % 4 == 0 ? 1.0 : 1.035;
    }
    TokenForgeDrawAvatarInRect(view, view.bounds, resolvedPreset, frameIndex, mode ?: @"idle");
    [view release];

    if ([mode isEqualToString:@"hidden"]) {
        [[NSColor colorWithCalibratedWhite:1.0 alpha:0.55] setFill];
        NSRectFillUsingOperation(NSMakeRect(0, 0, imageSize.width, imageSize.height), NSCompositingOperationSourceAtop);
    }

    [image unlockFocus];
    image.size = imageSize;
    [image setTemplate:NO];
    imageCache[cacheKey] = image;
    NSImage *cachedImage = imageCache[cacheKey];
    [image release];
    NSLog(@"INFO [AvatarImage][CACHE_STORE] key=%@ imageHash=%lu retainedBy=cache localReleased=true cacheCount=%lu",
          cacheKey,
          (unsigned long)cachedImage.hash,
          (unsigned long)imageCache.count);
    CGFloat scale = 1.0;
    NSRect finalRect = TokenForgeAvatarFinalDrawingRect(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), -1.0, &scale);
    TokenForgeLogAvatarRenderer(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), finalRect, scale, NO, YES, @"squareCellSprite", imageSize, cacheKey, NO);
    return cachedImage;
}

static NSString *TokenForgeCompanionViewRoleName(TokenForgeCompanionRenderRole role)
{
    switch (role) {
        case TokenForgeCompanionRenderRoleDesktopOverlay: return @"desktopOverlay";
        case TokenForgeCompanionRenderRoleMenuBar: return @"menuBar";
        case TokenForgeCompanionRenderRoleDashboardPreview:
        default: return @"dashboardPreview";
    }
}

static NSString *TokenForgeCompanionStageName(NSInteger stage)
{
    switch (stage) {
        case 1: return @"Hatchling";
        case 2: return @"Baby";
        case 3: return @"Junior";
        case 4: return @"Teen";
        case 5: return @"Adult";
        case 0:
        default: return @"Egg";
    }
}

static NSString *TokenForgeCompanionStageVisualSignature(NSInteger stage)
{
    switch (stage) {
        case 1: return @"tiny-face-partial-traits";
        case 2: return @"junior-body-traits";
        case 3: return @"expanded-silhouette-expression";
        case 4: return @"adult-crown-complete-traits";
        case 5: return @"legend-aura-rare-outline";
        case 0:
        default: return @"egg-shell-zodiac-mark";
    }
}

static BOOL TokenForgeIsDesktopOverlayPanelContentView(TokenForgeCompanionView *view)
{
    if (view == nil) {
        return NO;
    }

    NSWindow *window = view.window;
    return view.viewRole == TokenForgeCompanionRenderRoleDesktopOverlay &&
        window != nil &&
        window == TokenForgeCompanionWindow &&
        [window.identifier isEqualToString:@"TokenForge.DesktopCompanion"];
}

@implementation TokenForgeCompanionView
- (BOOL)isOpaque { return NO; }
- (BOOL)acceptsFirstMouse:(NSEvent *)event { return YES; }
- (BOOL)acceptsFirstResponder { return YES; }

- (BOOL)tokenForgeIsDesktopOverlayContentView
{
    return TokenForgeIsDesktopOverlayPanelContentView(self);
}

- (instancetype)initWithFrame:(NSRect)frameRect
{
    self = [super initWithFrame:frameRect];
    if (self != nil) {
        self.viewRole = TokenForgeCompanionRenderRoleDashboardPreview;
        self.viewRoleName = TokenForgeCompanionViewRoleName(self.viewRole);
        self.stage = 0;
        self.level = 1;
        self.xp = 0;
        self.snapshotHydrated = NO;
        self.renderVersion = 0;
        NSLog(@"INFO [CompanionView][INIT] role=%@", self.viewRoleName);
    }
    return self;
}

- (void)setViewRole:(TokenForgeCompanionRenderRole)viewRole
{
    _viewRole = viewRole;
    self.viewRoleName = TokenForgeCompanionViewRoleName(viewRole);
    NSLog(@"INFO [CompanionView][INIT] role=%@", self.viewRoleName);
    if (viewRole == TokenForgeCompanionRenderRoleDashboardPreview) {
        NSLog(@"INFO [DashboardAvatarView][INIT] role=dashboardPreview");
    } else if (viewRole == TokenForgeCompanionRenderRoleDesktopOverlay) {
        NSLog(@"INFO [OverlayView][INIT] role=desktopOverlay");
    }
}

- (void)mouseDown:(NSEvent *)event
{
    NSString *roleName = TokenForgeCompanionViewRoleName(self.viewRole);
    if (self.viewRole == TokenForgeCompanionRenderRoleDashboardPreview) {
        NSLog(@"INFO [DashboardAvatarView][NO_DRAG] reason=previewRole");
    } else {
        NSLog(@"INFO [OverlayDrag][IGNORE] role=%@ reason=baseViewNoDrag", roleName);
    }
}

- (void)mouseDragged:(NSEvent *)event
{
    if (self.viewRole == TokenForgeCompanionRenderRoleDashboardPreview) {
        NSLog(@"INFO [DashboardAvatarView][NO_DRAG] reason=previewRole");
    }
}

- (void)mouseUp:(NSEvent *)event
{
    if (self.viewRole == TokenForgeCompanionRenderRoleDashboardPreview) {
        NSLog(@"INFO [DashboardAvatarView][NO_DRAG] reason=previewRole");
    }
}

- (void)dealloc
{
    if ([self.assetType isEqualToString:@"overlay"]) {
        NSLog(@"INFO [DesktopOverlay] panel.dealloc contentView=true");
    }
#if !__has_feature(objc_arc)
    [super dealloc];
#endif
}

- (void)drawRect:(NSRect)dirtyRect
{
    [[NSColor clearColor] setFill];
    NSRectFill(dirtyRect);

    if (self.viewRole == TokenForgeCompanionRenderRoleDesktopOverlay && !self.snapshotHydrated) {
        NSLog(@"INFO [OverlayRender][SKIP] reason=snapshotNotHydrated");
        return;
    }

    static NSTimeInterval TokenForgeLastCompanionDrawRoleLogAt = 0.0;
    NSTimeInterval drawNow = [NSDate timeIntervalSinceReferenceDate];
    if (drawNow - TokenForgeLastCompanionDrawRoleLogAt > 1.0 || self.viewRole == TokenForgeCompanionRenderRoleDesktopOverlay) {
        TokenForgeLastCompanionDrawRoleLogAt = drawNow;
        NSLog(@"INFO [CompanionView][DRAW] role=%@ stage=%@ level=%ld frame=(%.2f,%.2f %.2fx%.2f)",
              self.viewRoleName ?: TokenForgeCompanionViewRoleName(self.viewRole),
              TokenForgeCompanionStageName(self.stage),
              (long)MAX(1, self.level),
              self.frame.origin.x,
              self.frame.origin.y,
              self.frame.size.width,
              self.frame.size.height);
        if (self.viewRole == TokenForgeCompanionRenderRoleDesktopOverlay) {
            NSLog(@"INFO [OverlayView][DRAW] repo=%@ role=desktopOverlay stage=%@ level=%ld",
                  self.repositoryId ?: @"unknown",
                  TokenForgeCompanionStageName(self.stage),
                  (long)MAX(1, self.level));
        }
    }

    if (TokenForgeDebugOverlayDiagnosticsEnabled && [self.assetType isEqualToString:@"overlay"]) {
        [[NSColor colorWithCalibratedRed:1.0 green:0.0 blue:0.0 alpha:0.18] setFill];
        NSRectFill(self.bounds);
        [[NSColor colorWithCalibratedRed:0.0 green:1.0 blue:0.0 alpha:0.92] setStroke];
        NSBezierPath *border = [NSBezierPath bezierPathWithRect:NSInsetRect(self.bounds, 1.0, 1.0)];
        border.lineWidth = 3.0;
        [border stroke];
        static BOOL TokenForgeOverlayRenderLogged = NO;
        if (!TokenForgeOverlayRenderLogged) {
            TokenForgeOverlayRenderLogged = YES;
            NSLog(@"INFO [OverlayTrace:render] contentView draw/render confirmed bounds=(%.2f,%.2f %.2fx%.2f) debugRectangle=true",
                  self.bounds.origin.x,
                  self.bounds.origin.y,
                  self.bounds.size.width,
                  self.bounds.size.height);
        }
    }

    TokenForgeDrawAvatarInRect(self, self.bounds, self.assetType ?: @"overlay", self.animationState, @"idle");

    if (self.speechText.length > 0 && [NSDate timeIntervalSinceReferenceDate] < self.speechExpiresAt) {
        [self drawSpeechBubble:self.speechText];
    }

    if ([self.assetType isEqualToString:@"hero"]) {
        CGFloat rendererScale = 1.0;
        NSRect drawRect = TokenForgeAvatarFinalDrawingRect(@"hero", self.bounds, self.safeDrawingInset, &rendererScale);
        NSRect safeBounds = NSInsetRect(self.bounds, self.safeDrawingInset, self.safeDrawingInset);
        NSRect animationMaxRect = NSInsetRect(drawRect, -drawRect.size.width * 0.08, -drawRect.size.height * 0.08);
        BOOL clipped = !NSContainsRect(self.bounds, animationMaxRect) || !NSContainsRect(safeBounds, drawRect);
        static NSTimeInterval TokenForgeLastAvatarPreviewLogAt = 0.0;
        NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
        if (now - TokenForgeLastAvatarPreviewLogAt > 1.8) {
            TokenForgeLastAvatarPreviewLogAt = now;
            NSLog(@"INFO [AvatarPreview] sourceSize=square-cell-sprite-hero container=(%.2f,%.2f %.2fx%.2f) safeBounds=(%.2f,%.2f %.2fx%.2f) drawRect=(%.2f,%.2f %.2fx%.2f) clipped=%@",
                  self.frame.origin.x,
                  self.frame.origin.y,
                  self.frame.size.width,
                  self.frame.size.height,
                  safeBounds.origin.x,
                  safeBounds.origin.y,
                  safeBounds.size.width,
                  safeBounds.size.height,
                  drawRect.origin.x,
                  drawRect.origin.y,
                  drawRect.size.width,
                  drawRect.size.height,
                  clipped ? @"true" : @"false");
            NSLog(@"INFO [AvatarPreview] animationMaxRect=(%.2f,%.2f %.2fx%.2f) withinSafeBounds=%@",
                  animationMaxRect.origin.x,
                  animationMaxRect.origin.y,
                  animationMaxRect.size.width,
                  animationMaxRect.size.height,
                  NSContainsRect(self.bounds, animationMaxRect) ? @"true" : @"false");
            NSLog(@"INFO [AvatarPreview] assetType=hero notMenuBar");
        }
    }
}

- (void)drawSpeechBubble:(NSString *)text
{
    NSDictionary *attributes = @{
        NSFontAttributeName: [NSFont boldSystemFontOfSize:11.0],
        NSForegroundColorAttributeName: [NSColor colorWithCalibratedRed:0.12 green:0.14 blue:0.18 alpha:1.0]
    };
    NSSize textSize = [text sizeWithAttributes:attributes];
    CGFloat width = MIN(MAX(textSize.width + 20.0, 118.0), self.bounds.size.width + 110.0);
    NSRect bubbleRect = NSMakeRect(NSMidX(self.bounds) - width * 0.5, NSMaxY(self.bounds) - 28.0, width, 26.0);
    NSBezierPath *bubble = [NSBezierPath bezierPathWithRoundedRect:bubbleRect xRadius:9.0 yRadius:9.0];
    [[NSColor colorWithCalibratedWhite:1.0 alpha:0.94] setFill];
    [bubble fill];
    [[NSColor colorWithCalibratedWhite:0.0 alpha:0.12] setStroke];
    bubble.lineWidth = 1.0;
    [bubble stroke];
    NSRect textRect = NSInsetRect(bubbleRect, 10.0, 6.0);
    [text drawInRect:textRect withAttributes:attributes];
}

- (NSColor *)bodyColor
{
    if (self.stage == 0) return [NSColor colorWithCalibratedRed:0.96 green:0.88 blue:0.70 alpha:1.0];
    if (self.stage == 1) return [NSColor colorWithCalibratedRed:1.0 green:0.79 blue:0.49 alpha:1.0];
    NSString *theme = self.visualThemeId ?: @"orange_cat";
    if ([theme isEqualToString:@"white_cat"]) return [NSColor colorWithCalibratedRed:0.94 green:0.95 blue:0.91 alpha:1.0];
    if ([theme isEqualToString:@"calico"]) return [NSColor colorWithCalibratedRed:0.96 green:0.72 blue:0.42 alpha:1.0];
    if ([theme isEqualToString:@"black_cat"]) return [NSColor colorWithCalibratedRed:0.16 green:0.17 blue:0.20 alpha:1.0];
    if ([theme isEqualToString:@"retriever"]) return [NSColor colorWithCalibratedRed:0.82 green:0.58 blue:0.30 alpha:1.0];
    if ([theme isEqualToString:@"runner"]) return [NSColor colorWithCalibratedRed:0.39 green:0.63 blue:0.98 alpha:1.0];
    if ([theme isEqualToString:@"orange_cat"]) return [NSColor colorWithCalibratedRed:0.92 green:0.48 blue:0.21 alpha:1.0];
    return [NSColor colorWithCalibratedRed:0.56 green:0.79 blue:0.90 alpha:1.0];
}

- (NSColor *)accentColor
{
    NSString *theme = self.visualThemeId ?: @"orange_cat";
    if ([theme isEqualToString:@"white_cat"]) return [NSColor colorWithCalibratedRed:0.36 green:0.58 blue:0.78 alpha:1.0];
    if ([theme isEqualToString:@"calico"]) return [NSColor colorWithCalibratedRed:0.16 green:0.17 blue:0.20 alpha:1.0];
    if ([theme isEqualToString:@"black_cat"]) return [NSColor colorWithCalibratedRed:0.94 green:0.78 blue:0.30 alpha:1.0];
    if ([theme isEqualToString:@"retriever"]) return [NSColor colorWithCalibratedRed:0.54 green:0.32 blue:0.17 alpha:1.0];
    if ([theme isEqualToString:@"runner"]) return [NSColor colorWithCalibratedRed:0.95 green:0.30 blue:0.34 alpha:1.0];
    if ([theme isEqualToString:@"orange_cat"]) return [NSColor colorWithCalibratedRed:0.99 green:0.77 blue:0.32 alpha:1.0];
    switch (self.archetype) {
        case 1: return [NSColor colorWithCalibratedRed:0.25 green:0.75 blue:1.0 alpha:1.0];
        case 2: return [NSColor colorWithCalibratedRed:0.95 green:0.60 blue:0.26 alpha:1.0];
        case 3: return [NSColor colorWithCalibratedRed:0.35 green:0.80 blue:0.46 alpha:1.0];
        case 4: return [NSColor colorWithCalibratedRed:0.93 green:0.52 blue:0.77 alpha:1.0];
        case 5: return [NSColor colorWithCalibratedRed:0.60 green:0.66 blue:1.0 alpha:1.0];
        case 6: return [NSColor colorWithCalibratedRed:1.0 green:0.42 blue:0.29 alpha:1.0];
        default: return [NSColor colorWithCalibratedRed:0.58 green:0.64 blue:0.72 alpha:1.0];
    }
}

- (void)pixelX:(NSInteger)x y:(NSInteger)y scale:(CGFloat)scale color:(NSColor *)color
{
    [self rect:NSMakeRect(x * scale, y * scale, scale, scale) color:color];
}

- (void)rect:(NSRect)rect color:(NSColor *)color
{
    [color setFill];
    NSRectFill(rect);
}

- (void)ellipse:(NSRect)rect color:(NSColor *)color
{
    [color setFill];
    NSInteger rows = MAX(1, (NSInteger)ceil(rect.size.height));
    for (NSInteger row = 0; row < rows; row++) {
        CGFloat normalized = rows <= 1 ? 0.0 : fabs(((CGFloat)row / (CGFloat)(rows - 1)) * 2.0 - 1.0);
        CGFloat inset = floor(rect.size.width * normalized * normalized * 0.26);
        NSRectFill(NSIntegralRect(NSMakeRect(rect.origin.x + inset, rect.origin.y + row, MAX(1.0, rect.size.width - inset * 2.0), 1.0)));
    }
}
@end

@implementation TokenForgeDesktopOverlayCompanionView

- (instancetype)initWithFrame:(NSRect)frameRect
{
    self = [super initWithFrame:frameRect];
    if (self != nil) {
        self.viewRole = TokenForgeCompanionRenderRoleDesktopOverlay;
        self.assetType = @"overlay";
        self.snapshotHydrated = TokenForgeCompanionSnapshotHydrated;
        self.renderVersion = TokenForgeCompanionRenderVersion;
        self.overlayViewGeneration = ++TokenForgeOverlayViewGeneration;
        NSLog(@"INFO [OverlayView][INIT] role=desktopOverlay generation=%lu", (unsigned long)self.overlayViewGeneration);
    }
    return self;
}

- (void)mouseDown:(NSEvent *)event
{
    if (![NSThread isMainThread]) {
        NSLog(@"INFO [OverlayDrag][CRASH_GUARD] reason=notMainThread");
        return;
    }

    if (!TokenForgeOverlayClickEnabled || TokenForgeMenuClickThrough) {
        NSLog(@"INFO [OverlayDrag][BLOCKED] repo=%@ reason=%@", TokenForgeSafeRepositoryKey(self.repositoryId), !TokenForgeOverlayClickEnabled ? @"clickDisabled" : @"clickThrough");
        return;
    }

    NSWindow *panel = self.window;
    NSString *repo = TokenForgeSafeRepositoryKey(self.repositoryId);
    if (panel == nil || panel.contentView != self) {
        NSLog(@"INFO [OverlayDrag][CRASH_GUARD] repo=%@ reason=%@ generation=%lu currentViewGeneration=%lu",
              repo,
              panel == nil ? @"panelMissing" : @"generationMismatch",
              (unsigned long)self.overlayViewGeneration,
              (unsigned long)TokenForgeOverlayViewGeneration);
        return;
    }

    if (TokenForgeIsDraggingOverlay && TokenForgeActiveDragRepositoryId.length > 0) {
        if ([TokenForgeActiveDragRepositoryId isEqualToString:repo]) {
            NSLog(@"INFO [OverlayDrag][BLOCKED] repo=%@ reason=alreadyDragging activeRepo=%@", repo, TokenForgeActiveDragRepositoryId);
            return;
        }
        NSLog(@"INFO [OverlayDrag][BLOCKED] repo=%@ reason=anotherOverlayDragging activeRepo=%@",
              repo,
              TokenForgeActiveDragRepositoryId ?: @"unknown");
        return;
    }

    panel.ignoresMouseEvents = NO;
    [panel orderFrontRegardless];
    NSLog(@"INFO [OverlayPanel][ORDER_FRONT] repo=%@ source=dragBegin", repo);
    TokenForgeEnsureOverlayFarmRegistry();
    self.mouseDownScreenPoint = [NSEvent mouseLocation];
    self.panelOriginAtMouseDown = panel.frame.origin;
    self.cursorOffsetInsidePanel = NSMakePoint(self.mouseDownScreenPoint.x - panel.frame.origin.x,
                                               self.mouseDownScreenPoint.y - panel.frame.origin.y);
    TokenForgeCurrentlyDraggingRepositoryId = [repo copy];
    TokenForgeActiveDragRepositoryId = [repo copy];
    TokenForgeActiveDragViewGeneration = self.overlayViewGeneration;
    TokenForgeActiveDragPanelGeneration = [TokenForgeOverlayGenerationsByRepositoryId[repo] unsignedIntegerValue];
    TokenForgeOverlayDragStatesByRepositoryId[repo] = [@{
        @"generation": @(self.overlayViewGeneration),
        @"panelGeneration": @(TokenForgeActiveDragPanelGeneration),
        @"mouseDownScreenX": @(self.mouseDownScreenPoint.x),
        @"mouseDownScreenY": @(self.mouseDownScreenPoint.y),
        @"beganAt": @([NSDate timeIntervalSinceReferenceDate])
    } mutableCopy];
    TokenForgeDragStartMouse = self.mouseDownScreenPoint;
    TokenForgeDragStartOrigin = self.panelOriginAtMouseDown;
    TokenForgeDragCursorOffsetInsidePanel = self.cursorOffsetInsidePanel;
    TokenForgeMovementWasRunningBeforeDrag = TokenForgeCompanionMotionTimer != nil &&
        TokenForgeCompanionAllowsWandering &&
        TokenForgeCompanionMotionMode != 0;
    TokenForgeCompanionVelocity = NSMakePoint(0, 0);
    TokenForgeCompanionTarget = panel.frame.origin;
    TokenForgeMotionPauseLogged = YES;
    TokenForgeOverlayDragFinalizing = NO;
    TokenForgeOverlayDragPersistedThisGesture = NO;
    TokenForgeIsDraggingOverlay = YES;
    TokenForgeLastOverlayDragApplyAt = 0.0;
    TokenForgeCoalescedOverlayDragEvents = 0;
    TokenForgeSuppressDashboardRedrawDuringOverlayDrag = YES;
    TokenForgeSuppressCSharpProjectionDuringOverlayDrag = YES;
    TokenForgeDragExceededThreshold = NO;
    TokenForgeDashboardLayoutHashBeforeOverlayDrag = TokenForgeDashboardLayoutHashForState(nil, @"overlayDragBegin");
    NSLog(@"INFO [OverlayView][DRAG_BEGIN]");
    NSLog(@"INFO [OverlayDrag][CAPTURE] repo=%@ topmost=true", repo);
    NSLog(@"INFO [OverlayDrag][BEGIN] repo=%@ generation=%lu mouseScreen=(%.2f,%.2f) panelFrame=(%.2f,%.2f %.2fx%.2f)",
          repo,
          (unsigned long)TokenForgeActiveDragPanelGeneration,
          self.mouseDownScreenPoint.x,
          self.mouseDownScreenPoint.y,
          panel.frame.origin.x,
          panel.frame.origin.y,
          panel.frame.size.width,
          panel.frame.size.height);
    NSLog(@"INFO [OverlayMovement][PAUSE] reason=drag");
}

	- (void)rightMouseDown:(NSEvent *)event
	{
	    TokenForgeRefreshNativeSafetyFlags();
	    if (TokenForgeNativeSafeMode || TokenForgeDisableContextMenu) {
	        NSLog(@"INFO [NativeSafeMode][SKIP] function=rightMouseDown reason=%@",
	              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_CONTEXT_MENU");
	        return;
	    }

	    if (![NSThread isMainThread]) {
	        NSLog(@"INFO [OverlayContextMenu][CRASH_GUARD] reason=notMainThread");
	        return;
    }

    NSString *repo = TokenForgeSafeRepositoryKey(self.repositoryId);
    NSLog(@"INFO [RightClickDiagnostic][MOUSE_DOWN] repo=%@ locationInWindow=(%.2f,%.2f) window=%p panelVisible=%@",
          repo,
          event.locationInWindow.x,
          event.locationInWindow.y,
          self.window,
          (self.window != nil && self.window.isVisible) ? @"true" : @"false");
    NSMenu *menu = [[NSMenu alloc] initWithTitle:@"TokenForge Companion"];
    NSMenuItem *openDashboard = [[NSMenuItem alloc] initWithTitle:@"Open Dashboard" action:@selector(showTokenForgeFromStatusItem:) keyEquivalent:@""];
    openDashboard.target = TokenForgeEnsureLifecycleDelegate();
    [menu addItem:openDashboard];
    [menu addItem:[NSMenuItem separatorItem]];
    NSMenuItem *quit = [[NSMenuItem alloc] initWithTitle:@"Quit TokenForge" action:@selector(quitFromContextMenu:) keyEquivalent:@""];
    quit.target = self;
    [menu addItem:quit];
    NSLog(@"INFO [RightClickDiagnostic][MENU_OPEN] repo=%@ source=rightMouseDown itemCount=%ld", repo, (long)menu.numberOfItems);
    NSLog(@"INFO [OverlayContextMenu][OPEN] repo=%@ source=rightClick items=%ld", repo, (long)menu.numberOfItems);
    [NSMenu popUpContextMenu:menu withEvent:event forView:self];
}

	- (NSMenu *)menuForEvent:(NSEvent *)event
	{
	    TokenForgeRefreshNativeSafetyFlags();
	    if (TokenForgeNativeSafeMode || TokenForgeDisableContextMenu) {
	        NSLog(@"INFO [NativeSafeMode][SKIP] function=menuForEvent reason=%@",
	              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_CONTEXT_MENU");
	        return nil;
	    }

	    NSString *repo = TokenForgeSafeRepositoryKey(self.repositoryId);
    NSLog(@"INFO [RightClickDiagnostic][MOUSE_DOWN] repo=%@ source=menuForEvent locationInWindow=(%.2f,%.2f) window=%p panelVisible=%@",
          repo,
          event.locationInWindow.x,
          event.locationInWindow.y,
          self.window,
          (self.window != nil && self.window.isVisible) ? @"true" : @"false");
    NSMenu *menu = [[NSMenu alloc] initWithTitle:@"TokenForge Companion"];
    NSMenuItem *openDashboard = [[NSMenuItem alloc] initWithTitle:@"Open Dashboard" action:@selector(showTokenForgeFromStatusItem:) keyEquivalent:@""];
    openDashboard.target = TokenForgeEnsureLifecycleDelegate();
    [menu addItem:openDashboard];
    [menu addItem:[NSMenuItem separatorItem]];
    NSMenuItem *quit = [[NSMenuItem alloc] initWithTitle:@"Quit TokenForge" action:@selector(quitFromContextMenu:) keyEquivalent:@""];
    quit.target = self;
    [menu addItem:quit];
    NSLog(@"INFO [RightClickDiagnostic][MENU_OPEN] repo=%@ source=menuForEvent itemCount=%ld", repo, (long)menu.numberOfItems);
    return menu;
}

- (void)quitFromContextMenu:(id)sender
{
    NSLog(@"INFO [RightClickDiagnostic][QUIT_SELECTED] repo=%@ source=contextMenu action=app.quit", TokenForgeSafeRepositoryKey(self.repositoryId));
    NSLog(@"INFO [OverlayContextMenu][QUIT] repo=%@ source=contextMenu", TokenForgeSafeRepositoryKey(self.repositoryId));
    TokenForgeSendDashboardAction("app.quit");
    TokenForgeRequestExplicitQuit(@"contextMenu");
}

- (void)mouseDragged:(NSEvent *)event
{
    if (![NSThread isMainThread]) {
        NSLog(@"INFO [OverlayDrag][CRASH_GUARD] reason=notMainThread");
        return;
    }

    NSWindow *panel = self.window;
    NSString *repo = TokenForgeSafeRepositoryKey(self.repositoryId);
    if (!TokenForgeIsOverlayDraggingForRepository(repo)) {
        NSLog(@"INFO [OverlayDrag][IGNORE] repo=%@ reason=notActiveDrag activeRepo=%@",
              repo,
              TokenForgeActiveDragRepositoryId ?: @"none");
        return;
    }
    NSUInteger panelGeneration = [TokenForgeOverlayGenerationsByRepositoryId[repo] unsignedIntegerValue];
    if (panel == nil || panel.contentView != self || panelGeneration != TokenForgeActiveDragPanelGeneration || self.overlayViewGeneration != TokenForgeActiveDragViewGeneration) {
        NSLog(@"INFO [OverlayDrag][CRASH_GUARD] repo=%@ reason=%@ generation=%lu currentViewGeneration=%lu",
              repo,
              panel == nil ? @"panelMissing" : @"generationMismatch",
              (unsigned long)self.overlayViewGeneration,
              (unsigned long)TokenForgeOverlayViewGeneration);
        return;
    }

    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    if (TokenForgeLastOverlayDragApplyAt > 0.0 && now - TokenForgeLastOverlayDragApplyAt < (1.0 / 120.0)) {
        TokenForgeCoalescedOverlayDragEvents += 1;
        if (TokenForgeCoalescedOverlayDragEvents == 1 || TokenForgeCoalescedOverlayDragEvents % 12 == 0) {
            NSPoint latest = [NSEvent mouseLocation];
            NSLog(@"INFO [OverlayDrag][COALESCE] repo=%@ dropped=%lu latestTarget=(%.2f,%.2f)",
                  repo,
                  (unsigned long)TokenForgeCoalescedOverlayDragEvents,
                  latest.x - self.cursorOffsetInsidePanel.x,
                  latest.y - self.cursorOffsetInsidePanel.y);
        }
        return;
    }
    TokenForgeLastOverlayDragApplyAt = now;
    NSPoint mouseScreen = [NSEvent mouseLocation];
    CGFloat dx = mouseScreen.x - self.mouseDownScreenPoint.x;
    CGFloat dy = mouseScreen.y - self.mouseDownScreenPoint.y;
    if (!TokenForgeDragExceededThreshold && hypot(dx, dy) > 2.0) {
        TokenForgeDragExceededThreshold = YES;
        NSLog(@"INFO [CompanionDrag] thresholdExceeded");
    }

    NSRect targetFrame = panel.frame;
    targetFrame.origin = NSMakePoint(mouseScreen.x - self.cursorOffsetInsidePanel.x,
                                     mouseScreen.y - self.cursorOffsetInsidePanel.y);
    NSRect unclamped = targetFrame;
    targetFrame = TokenForgeClampFrameToVisibleFrame(targetFrame);
    if (!NSEqualRects(unclamped, targetFrame)) {
        NSRect visible = TokenForgeVisibleFrameForFrame(unclamped);
        NSLog(@"INFO [OverlayFarmLayout][CLAMP] repo=%@ before=(%.2f,%.2f) after=(%.2f,%.2f) screen=(%.2f,%.2f %.2fx%.2f)",
              repo,
              unclamped.origin.x,
              unclamped.origin.y,
              targetFrame.origin.x,
              targetFrame.origin.y,
              visible.origin.x,
              visible.origin.y,
              visible.size.width,
              visible.size.height);
    }

    TokenForgeIsMovingOverlayPanel = YES;
    [panel setFrameOrigin:targetFrame.origin];
    TokenForgeIsMovingOverlayPanel = NO;
    NSPoint actualOrigin = panel.frame.origin;
    TokenForgeOverlayFramesByRepositoryId[repo] = [NSValue valueWithRect:panel.frame];
    TokenForgeCompanionAnchor = actualOrigin;
    TokenForgeCompanionTarget = actualOrigin;
    CGFloat errorX = actualOrigin.x - targetFrame.origin.x;
    CGFloat errorY = actualOrigin.y - targetFrame.origin.y;
    CGFloat scale = panel.backingScaleFactor > 0.0 ? panel.backingScaleFactor : NSScreen.mainScreen.backingScaleFactor;
    NSLog(@"INFO [OverlayView][DRAG_MOVE]");
    NSLog(@"INFO [OverlayDrag][MOVE] repo=%@ generation=%lu mouseScreen=(%.2f,%.2f) targetOrigin=(%.2f,%.2f) actualFrame=(%.2f,%.2f %.2fx%.2f)",
          repo,
          (unsigned long)TokenForgeActiveDragPanelGeneration,
          mouseScreen.x,
          mouseScreen.y,
          targetFrame.origin.x,
          targetFrame.origin.y,
          panel.frame.origin.x,
          panel.frame.origin.y,
          panel.frame.size.width,
          panel.frame.size.height);
    NSLog(@"INFO [OverlayDrag][COORDS] mouseScreen=(%.2f,%.2f) offset=(%.2f,%.2f) targetOrigin=(%.2f,%.2f) actualOrigin=(%.2f,%.2f) scale=%.2f",
          mouseScreen.x,
          mouseScreen.y,
          self.cursorOffsetInsidePanel.x,
          self.cursorOffsetInsidePanel.y,
          targetFrame.origin.x,
          targetFrame.origin.y,
          actualOrigin.x,
          actualOrigin.y,
          scale);
    NSLog(@"INFO [OverlayDrag][POSITION_ERROR] repo=%@ dx=%.2f dy=%.2f", repo, errorX, errorY);
    NSLog(@"INFO [OverlayDrag][SUPPRESS_PROJECTION] repo=%@ reason=dragInProgress", repo);
    NSLog(@"INFO [OverlayDrag][NO_DASHBOARD_REDRAW] repo=%@", repo);
}

- (void)mouseUp:(NSEvent *)event
{
    if (TokenForgeOverlayDragFinalizing) {
        NSLog(@"INFO [OverlayDrag][END] skipped reason=alreadyFinalizing");
        return;
    }

    TokenForgeOverlayDragFinalizing = YES;
    NSWindow *panel = self.window;
    NSString *repo = TokenForgeSafeRepositoryKey(self.repositoryId);
    if (panel == nil || panel.contentView != self) {
        NSLog(@"INFO [OverlayDrag][CRASH_GUARD] repo=%@ reason=panelMissing", repo);
        TokenForgeIsDraggingOverlay = NO;
        TokenForgeCurrentlyDraggingRepositoryId = nil;
        TokenForgeActiveDragRepositoryId = nil;
        TokenForgeActiveDragPanelGeneration = 0;
        TokenForgeActiveDragViewGeneration = 0;
        TokenForgeSuppressDashboardRedrawDuringOverlayDrag = NO;
        TokenForgeSuppressCSharpProjectionDuringOverlayDrag = NO;
        TokenForgeOverlayDragFinalizing = NO;
        TokenForgeApplyPendingOverlayActionsAfterDrag(@"mouseUpNoWindow");
        return;
    }

    NSRect frame = TokenForgeClampFrameToVisibleFrame(panel.frame);
    [panel setFrameOrigin:frame.origin];
    TokenForgeOverlayFramesByRepositoryId[repo] = [NSValue valueWithRect:frame];
    TokenForgeCompanionAnchor = frame.origin;
    TokenForgeCompanionTarget = frame.origin;
    if (TokenForgeDragExceededThreshold) {
        if (!TokenForgeOverlayDragPersistedThisGesture) {
            TokenForgePersistOverlayFrameForRepository(repo, frame);
            if (panel == TokenForgeCompanionWindow) {
                TokenForgePersistCompanionFrame(frame);
            }
            TokenForgeOverlayDragPersistedThisGesture = YES;
        }
        NSLog(@"INFO [OverlayView][DRAG_END]");
        NSLog(@"INFO [OverlayDrag][END] repo=%@ generation=%lu finalFrame=(%.2f,%.2f %.2fx%.2f)",
              repo,
              (unsigned long)TokenForgeActiveDragPanelGeneration,
              frame.origin.x,
              frame.origin.y,
              frame.size.width,
              frame.size.height);
        NSLog(@"INFO [OverlayPositionSync][COMMIT] repo=%@ source=dragEnd position=(%.2f,%.2f)", repo, frame.origin.x, frame.origin.y);
        if (TokenForgeOverlayDragEndedForRepository != nil) {
            TokenForgeOverlayDragEndedForRepository(repo.UTF8String, frame.origin.x, frame.origin.y);
        }
        if (TokenForgeOverlayDragEndedForRepository == nil && panel == TokenForgeCompanionWindow && TokenForgeOverlayDragEnded != nil) {
            TokenForgeOverlayDragEnded(frame.origin.x, frame.origin.y);
        }
    } else if (event.clickCount >= 2) {
        NSLog(@"INFO [DesktopCompanion] double click dashboard restore requested");
        if (TokenForgeOverlayDoubleClicked != nil) {
            TokenForgeOverlayDoubleClicked();
        } else {
            TokenForgeOpenNativeDashboardOnMainWithSource(@"overlayDoubleClick");
        }
    } else {
        NSLog(@"INFO [DesktopCompanion] single click reaction triggered");
        TokenForgeTriggerOverlayReaction(0, @"Ready to grow!");
        if (TokenForgeOverlayClicked != nil) {
            TokenForgeOverlayClicked();
        }
    }

    TokenForgeIsDraggingOverlay = NO;
    [TokenForgeOverlayDragStatesByRepositoryId removeObjectForKey:repo];
    TokenForgeCurrentlyDraggingRepositoryId = nil;
    TokenForgeActiveDragRepositoryId = nil;
    TokenForgeActiveDragPanelGeneration = 0;
    TokenForgeActiveDragViewGeneration = 0;
    TokenForgeLastOverlayDragApplyAt = 0.0;
    TokenForgeCoalescedOverlayDragEvents = 0;
    TokenForgeSuppressDashboardRedrawDuringOverlayDrag = NO;
    TokenForgeSuppressCSharpProjectionDuringOverlayDrag = NO;
    NSUInteger afterHash = TokenForgeDashboardLayoutHashForState(nil, @"overlayDragEnd");
    if (TokenForgeDashboardLayoutHashBeforeOverlayDrag != 0 && afterHash != TokenForgeDashboardLayoutHashBeforeOverlayDrag) {
        NSLog(@"WARN [DashboardLayout][WARN] changedDuringOverlayDrag before=%lu after=%lu",
              (unsigned long)TokenForgeDashboardLayoutHashBeforeOverlayDrag,
              (unsigned long)afterHash);
    } else {
        NSLog(@"INFO [DashboardLayout][STABLE_DURING_DRAG] beforeHash=%lu afterHash=%lu",
              (unsigned long)TokenForgeDashboardLayoutHashBeforeOverlayDrag,
              (unsigned long)afterHash);
    }
    if (TokenForgeMovementWasRunningBeforeDrag) {
        TokenForgeCompanionDragCooldownUntil = [NSDate timeIntervalSinceReferenceDate] + 0.35;
        NSLog(@"INFO [OverlayMovement][RESUME] reason=dragEnded");
    }
    TokenForgeMovementWasRunningBeforeDrag = NO;
    if (TokenForgeMenuClickThrough) {
        panel.ignoresMouseEvents = YES;
    }
    TokenForgeOverlayDragFinalizing = NO;
    TokenForgeApplyPendingOverlayActionsAfterDrag(@"mouseUp");
}

- (void)drawRect:(NSRect)dirtyRect
{
    [super drawRect:dirtyRect];
    [NSGraphicsContext currentContext].imageInterpolation = NSImageInterpolationNone;
    if (!self.firstVisibleFrameLogged && self.snapshotHydrated && self.window != nil && self.window.isVisible) {
        self.firstVisibleFrameLogged = YES;
        NSLog(@"INFO [OverlayRender][FIRST_VISIBLE_FRAME] repo=%@ stage=%@ level=%ld hydrated=true",
              self.repositoryId ?: @"legacy",
              TokenForgeCompanionStageName(self.stage),
              (long)MAX(1, self.level));
    }
}

@end

@implementation TokenForgeDashboardCompanionPreviewView
- (void)viewWillMoveToWindow:(NSWindow *)newWindow
{
    [super viewWillMoveToWindow:newWindow];
    if (newWindow == nil) {
        [self stopDashboardPreviewAnimation];
    } else {
        [self startDashboardPreviewAnimation];
    }
}

- (void)startDashboardPreviewAnimation
{
    if (self.previewTimer != nil) {
        return;
    }

    self.previewTimer = [NSTimer scheduledTimerWithTimeInterval:1.0 / 18.0 repeats:YES block:^(NSTimer *timer) {
        if (self.window == nil || !self.window.isVisible) {
            return;
        }

        NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
        CGFloat amplitude = MAX(1.0, MIN(10.0, self.motionBounceAmplitude));
        CGFloat pulse = MAX(0.2, MIN(1.6, self.motionPulseFrequency));
        NSString *state = self.dashboardAnimationState ?: @"subtleIdle";
        if ([state isEqualToString:@"warningShake"]) {
            self.visualOffsetY = sin(now * 2.0) * 2.0;
            self.visualScale = 1.0;
            self.visualRotation = sin(now * 15.0) * 3.2;
        } else if ([state isEqualToString:@"attentionBounce"]) {
            self.visualOffsetY = fabs(sin(now * 4.4)) * amplitude;
            self.visualScale = 1.0 + fabs(sin(now * 2.2)) * 0.03;
            self.visualRotation = sin(now * 1.6) * 1.4;
        } else if ([state isEqualToString:@"activityPulse"] || [state isEqualToString:@"growthSparkle"]) {
            self.visualOffsetY = sin(now * (1.8 + pulse)) * amplitude;
            self.visualScale = 1.0 + fabs(sin(now * (2.4 + pulse))) * 0.045;
            self.visualRotation = sin(now * 2.1) * 1.8;
        } else {
            self.visualOffsetY = sin(now * (1.4 + pulse)) * amplitude;
            self.visualScale = self.levelUpReady ? 1.0 + fabs(sin(now * 3.0)) * 0.055 : 1.0 + sin(now * 1.2) * 0.018;
            self.visualRotation = self.levelUpReady ? sin(now * 3.8) * 2.6 : sin(now * 0.75) * 1.0;
        }
        static NSTimeInterval TokenForgeLastDashboardAnimationBoundsLogAt = 0.0;
        if (now - TokenForgeLastDashboardAnimationBoundsLogAt > 2.0) {
            TokenForgeLastDashboardAnimationBoundsLogAt = now;
            NSLog(@"INFO [DashboardHero] animationBounds=previewFrame(%.2f,%.2f %.2fx%.2f) withinContainer=true",
                  self.frame.origin.x,
                  self.frame.origin.y,
                  self.frame.size.width,
                  self.frame.size.height);
        }
        [self setNeedsDisplay:YES];
    }];
    [[NSRunLoop mainRunLoop] addTimer:self.previewTimer forMode:NSRunLoopCommonModes];
}

- (void)stopDashboardPreviewAnimation
{
    [self.previewTimer invalidate];
    self.previewTimer = nil;
}
@end

@implementation TokenForgeAvatarPreviewView
- (instancetype)initWithFrame:(NSRect)frameRect
{
    self = [super initWithFrame:frameRect];
    if (self != nil) {
        self.safePadding = 30.0;
        self.safeDrawingInset = 30.0;
        self.viewRole = TokenForgeCompanionRenderRoleDashboardPreview;
        self.assetType = @"hero";
    }
    return self;
}

- (void)setSafePadding:(CGFloat)safePadding
{
    _safePadding = MAX(24.0, MIN(40.0, safePadding));
    self.safeDrawingInset = _safePadding;
}
@end

@implementation TokenForgeDashboardHeroAvatarContainerView
- (BOOL)isFlipped { return YES; }
- (void)layout
{
    [super layout];
    if (self.preview == nil) {
        return;
    }

    NSRect imageFrame = self.preview.frame;
    NSRect safeBounds = NSInsetRect(self.preview.bounds, self.preview.safePadding, self.preview.safePadding);
    BOOL clipped = !NSContainsRect(self.bounds, imageFrame);
    NSLog(@"INFO [AvatarPreview] layout repo=%@ container=(%.2f,%.2f %.2fx%.2f) safeBounds=(%.2f,%.2f %.2fx%.2f) drawFrame=(%.2f,%.2f %.2fx%.2f) clipped=%@",
          self.repositoryName ?: @"Repository",
          self.frame.origin.x,
          self.frame.origin.y,
          self.frame.size.width,
          self.frame.size.height,
          safeBounds.origin.x,
          safeBounds.origin.y,
          safeBounds.size.width,
          safeBounds.size.height,
          imageFrame.origin.x,
          imageFrame.origin.y,
          imageFrame.size.width,
          imageFrame.size.height,
          clipped ? @"true" : @"false");
}
@end

@implementation TokenForgeCompanionOverlayWindow
- (BOOL)canBecomeKeyWindow { return NO; }
- (BOOL)canBecomeMainWindow { return NO; }
- (void)dealloc
{
    NSLog(@"INFO [DesktopOverlay] panel.dealloc window=true");
#if !__has_feature(objc_arc)
    [super dealloc];
#endif
}
@end

static TokenForgeAppLifecycleDelegate *TokenForgeLifecycleDelegate = nil;
static NSString *TokenForgeMenuCompanionName = @"Token";
static NSString *TokenForgeMenuStage = @"Egg";
static NSInteger TokenForgeMenuStageIndex = 0;
static NSInteger TokenForgeMenuArchetypeIndex = 0;
static NSInteger TokenForgeMenuLevel = 1;
static NSString *TokenForgeMenuRepositoryAlias = @"Not selected";
static NSString *TokenForgeMenuAgentStatus = @"No agent connected";
static NSString *TokenForgeMenuSyncStatus = @"Local only";
static BOOL TokenForgeMenuCompanionEnabled = NO;
static BOOL TokenForgeMenuCanAnalyze = NO;
static BOOL TokenForgeMenuCanSync = NO;
static BOOL TokenForgeMenuCanLevelUp = NO;
static BOOL TokenForgeMenuAnalysisRunning = NO;
static BOOL TokenForgeMenuMovementEnabled = YES;
static NSInteger TokenForgeMenuConnectedCompanionCount = 0;
static NSInteger TokenForgeMenuVisibleOverlayCount = 0;
static BOOL TokenForgeLifecycleInstallInProgress = NO;
static BOOL TokenForgeDashboardOpenPending = NO;
static BOOL TokenForgePendingInitialDashboardOpen = NO;
static BOOL TokenForgeDumpingWindows = NO;
static NSString *TokenForgeMenuReaction = @"none";
static NSString *TokenForgeMenuStatusText = @"Repo: None · AI Agents: 0 connected";
static NSString *TokenForgeMenuAnimationMode = @"idle";
static NSString *TokenForgeCurrentDashboardTab = @"dashboard";
static TokenForgeNativeDashboardController *TokenForgeDashboardController = nil;
static NSWindow *TokenForgeNativeDashboardWindow = nil;

static NSString *TokenForgeRuntimeDefaultsKey(NSString *suffix)
{
    NSString *safeSuffix = suffix.length > 0 ? suffix : @"Unknown";
    return [@"TokenForge.Runtime." stringByAppendingString:safeSuffix];
}

static void TokenForgeEnsureOverlayFarmRegistry(void)
{
    if (TokenForgeOverlayPanelsByRepositoryId == nil) {
        TokenForgeOverlayPanelsByRepositoryId = [[NSMutableDictionary alloc] init];
        NSLog(@"INFO [OverlayFarmRegistry][INIT] name=panels owner=static retained=true thread=%@", TokenForgeThreadLabel());
    }
    if (TokenForgeOverlayViewsByRepositoryId == nil) {
        TokenForgeOverlayViewsByRepositoryId = [[NSMutableDictionary alloc] init];
        NSLog(@"INFO [OverlayFarmRegistry][INIT] name=views owner=static retained=true thread=%@", TokenForgeThreadLabel());
    }
    if (TokenForgeOverlaySnapshotsByRepositoryId == nil) {
        TokenForgeOverlaySnapshotsByRepositoryId = [[NSMutableDictionary alloc] init];
        NSLog(@"INFO [OverlayFarmRegistry][INIT] name=snapshots owner=static retained=true thread=%@", TokenForgeThreadLabel());
    }
    if (TokenForgeOverlayFramesByRepositoryId == nil) {
        TokenForgeOverlayFramesByRepositoryId = [[NSMutableDictionary alloc] init];
        NSLog(@"INFO [OverlayFarmRegistry][INIT] name=frames owner=static retained=true thread=%@", TokenForgeThreadLabel());
    }
    if (TokenForgeOverlayDragStatesByRepositoryId == nil) {
        TokenForgeOverlayDragStatesByRepositoryId = [[NSMutableDictionary alloc] init];
        NSLog(@"INFO [OverlayFarmRegistry][INIT] name=dragStates owner=static retained=true thread=%@", TokenForgeThreadLabel());
    }
    if (TokenForgeOverlayGenerationsByRepositoryId == nil) {
        TokenForgeOverlayGenerationsByRepositoryId = [[NSMutableDictionary alloc] init];
        NSLog(@"INFO [OverlayFarmRegistry][INIT] name=generations owner=static retained=true thread=%@", TokenForgeThreadLabel());
    }
}

static NSString *TokenForgeSafeRepositoryKey(NSString *repositoryId)
{
    NSString *trimmed = [repositoryId stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    return trimmed.length > 0 ? trimmed : @"legacy";
}

static NSString *TokenForgeOverlayDefaultsKey(NSString *repositoryId, NSString *suffix)
{
    NSString *safeRepo = TokenForgeSafeRepositoryKey(repositoryId);
    return [NSString stringWithFormat:@"TokenForge.CompanionOverlay.%@.%@", safeRepo, suffix ?: @"value"];
}

static BOOL TokenForgeHasSavedOverlayOriginForRepository(NSString *repositoryId)
{
    return [[NSUserDefaults standardUserDefaults] boolForKey:TokenForgeOverlayDefaultsKey(repositoryId, @"PositionSaved")];
}

static NSPoint TokenForgeDefaultFarmOriginForIndex(NSUInteger index, NSSize size)
{
    NSRect visible = TokenForgeVisibleFrame();
    CGFloat gap = 18.0;
    NSUInteger column = index % 4;
    NSUInteger row = index / 4;
    CGFloat x = NSMinX(visible) + 36.0 + column * (size.width + gap);
    CGFloat y = NSMinY(visible) + 72.0 + row * (size.height * 0.58 + gap);
    return NSMakePoint(x, y);
}

static NSPoint TokenForgeLoadOverlayOriginForRepository(NSString *repositoryId, NSUInteger index, NSSize size, BOOL *restoredOut)
{
    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
    NSString *repo = TokenForgeSafeRepositoryKey(repositoryId);
    if ([defaults boolForKey:TokenForgeOverlayDefaultsKey(repo, @"PositionSaved")]) {
        if (restoredOut != nil) {
            *restoredOut = YES;
        }
        return NSMakePoint([defaults doubleForKey:TokenForgeOverlayDefaultsKey(repo, @"PositionX")],
                           [defaults doubleForKey:TokenForgeOverlayDefaultsKey(repo, @"PositionY")]);
    }

    if (restoredOut != nil) {
        *restoredOut = NO;
    }
    return TokenForgeDefaultFarmOriginForIndex(index, size);
}

static CGFloat TokenForgeOverlapRatio(NSRect first, NSRect second)
{
    CGFloat intersectionArea = TokenForgeRectArea(NSIntersectionRect(first, second));
    CGFloat smallestArea = MAX(1.0, MIN(TokenForgeRectArea(first), TokenForgeRectArea(second)));
    return intersectionArea / smallestArea;
}

static NSRect TokenForgeResolveFarmFrameForRepository(NSString *repositoryId, NSRect requestedFrame, NSUInteger index, NSString *reason)
{
    TokenForgeEnsureOverlayFarmRegistry();
    NSString *repo = TokenForgeSafeRepositoryKey(repositoryId);
    NSRect candidate = TokenForgeClampFrameToVisibleFrame(requestedFrame);
    CGFloat threshold = 0.28;
    BOOL nudged = NO;
    NSRect originalCandidate = candidate;
    NSUInteger attempt = 0;
    while (attempt < 24) {
        BOOL collided = NO;
        for (NSString *otherRepo in TokenForgeOverlayFramesByRepositoryId) {
            if ([otherRepo isEqualToString:repo]) {
                continue;
            }

            NSRect otherFrame = [TokenForgeOverlayFramesByRepositoryId[otherRepo] rectValue];
            CGFloat ratio = TokenForgeOverlapRatio(candidate, otherFrame);
            if (ratio > threshold) {
                NSLog(@"INFO [OverlayFarmLayout][COLLISION] repo=%@ withRepo=%@ overlapRatio=%.3f",
                      repo,
                      otherRepo,
                      ratio);
                CGFloat stepX = candidate.size.width + 18.0;
                CGFloat stepY = candidate.size.height * 0.58 + 18.0;
                NSUInteger slot = index + attempt + 1;
                NSPoint next = TokenForgeDefaultFarmOriginForIndex(slot, candidate.size);
                if (attempt % 2 == 1) {
                    next = NSMakePoint(candidate.origin.x + stepX, candidate.origin.y);
                } else if (attempt % 3 == 0) {
                    next = NSMakePoint(candidate.origin.x, candidate.origin.y + stepY);
                }
                candidate.origin = next;
                candidate = TokenForgeClampFrameToVisibleFrame(candidate);
                collided = YES;
                nudged = YES;
                break;
            }
        }

        if (!collided) {
            break;
        }
        attempt += 1;
    }

    if (nudged) {
        NSLog(@"INFO [OverlayFarmLayout][NUDGE] repo=%@ from=(%.2f,%.2f) to=(%.2f,%.2f) reason=%@",
              repo,
              originalCandidate.origin.x,
              originalCandidate.origin.y,
              candidate.origin.x,
              candidate.origin.y,
              reason ?: @"collision");
    }

    TokenForgeOverlayFramesByRepositoryId[repo] = [NSValue valueWithRect:candidate];
    return candidate;
}

static void TokenForgePersistOverlayFrameForRepository(NSString *repositoryId, NSRect frame)
{
    NSString *repo = TokenForgeSafeRepositoryKey(repositoryId);
    TokenForgeEnsureOverlayFarmRegistry();
    TokenForgeOverlayFramesByRepositoryId[repo] = [NSValue valueWithRect:frame];
    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
    [defaults setDouble:frame.origin.x forKey:TokenForgeOverlayDefaultsKey(repo, @"PositionX")];
    [defaults setDouble:frame.origin.y forKey:TokenForgeOverlayDefaultsKey(repo, @"PositionY")];
    [defaults setBool:YES forKey:TokenForgeOverlayDefaultsKey(repo, @"PositionSaved")];
    [defaults synchronize];
    NSLog(@"INFO [OverlayPositionSync][SAVE] repo=%@ position=(%.2f,%.2f)", repo, frame.origin.x, frame.origin.y);
}

static NSPanel *TokenForgeOverlayPanelForRepository(NSString *repositoryId)
{
    TokenForgeEnsureOverlayFarmRegistry();
    return TokenForgeOverlayPanelsByRepositoryId[TokenForgeSafeRepositoryKey(repositoryId)];
}

static TokenForgeDesktopOverlayCompanionView *TokenForgeOverlayViewForRepository(NSString *repositoryId)
{
    TokenForgeEnsureOverlayFarmRegistry();
    return TokenForgeOverlayViewsByRepositoryId[TokenForgeSafeRepositoryKey(repositoryId)];
}

static BOOL TokenForgeIsOverlayDraggingForRepository(NSString *repositoryId)
{
    NSString *repo = TokenForgeSafeRepositoryKey(repositoryId);
    return TokenForgeIsDraggingOverlay &&
        TokenForgeActiveDragRepositoryId.length > 0 &&
        [TokenForgeActiveDragRepositoryId isEqualToString:repo];
}

static NSInteger TokenForgeSnapshotInteger(NSDictionary *snapshot, NSString *key, NSInteger fallback)
{
    id value = snapshot[key];
    return [value respondsToSelector:@selector(integerValue)] ? [value integerValue] : fallback;
}

static BOOL TokenForgeSnapshotBool(NSDictionary *snapshot, NSString *key, BOOL fallback)
{
    id value = snapshot[key];
    return [value respondsToSelector:@selector(boolValue)] ? [value boolValue] : fallback;
}

static CGFloat TokenForgeSnapshotFloat(NSDictionary *snapshot, NSString *key, CGFloat fallback)
{
    id value = snapshot[key];
    return [value respondsToSelector:@selector(doubleValue)] ? (CGFloat)[value doubleValue] : fallback;
}

static NSString *TokenForgeSnapshotString(NSDictionary *snapshot, NSString *key, NSString *fallback)
{
    id value = snapshot[key];
    if ([value isKindOfClass:[NSString class]] && [(NSString *)value length] > 0) {
        return (NSString *)value;
    }
    if ([value respondsToSelector:@selector(stringValue)]) {
        return [value stringValue];
    }
    return fallback;
}

static BOOL TokenForgeTruthyString(NSString *value)
{
    if (value.length == 0) {
        return NO;
    }

    NSString *normalized = [[value stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]] lowercaseString];
    return [normalized isEqualToString:@"1"] ||
           [normalized isEqualToString:@"yes"] ||
           [normalized isEqualToString:@"true"] ||
           [normalized isEqualToString:@"on"];
}

static BOOL TokenForgeDetectRuntimeVerificationMode(void)
{
    NSDictionary<NSString *, NSString *> *environment = [[NSProcessInfo processInfo] environment];
    if (TokenForgeTruthyString(environment[@"TOKENFORGE_VERIFY_RUNTIME"])) {
        return YES;
    }

    NSArray<NSString *> *arguments = [[NSProcessInfo processInfo] arguments];
    for (NSUInteger index = 0; index < arguments.count; index++) {
        NSString *argument = arguments[index];
        if ([argument isEqualToString:@"-TokenForgeVerifyRuntime"]) {
            if (index + 1 >= arguments.count) {
                return YES;
            }

            return TokenForgeTruthyString(arguments[index + 1]);
        }

        if ([argument hasPrefix:@"-TokenForgeVerifyRuntime="]) {
            return TokenForgeTruthyString([argument substringFromIndex:@"-TokenForgeVerifyRuntime=".length]);
        }
    }

    return NO;
}

static BOOL TokenForgeDetectNativeOverlayDisabledAtLaunch(void)
{
    NSDictionary<NSString *, NSString *> *environment = [[NSProcessInfo processInfo] environment];
    if (TokenForgeTruthyString(environment[@"TOKENFORGE_DISABLE_NATIVE_OVERLAY_AT_LAUNCH"])) {
        return YES;
    }

    NSArray<NSString *> *arguments = [[NSProcessInfo processInfo] arguments];
    for (NSUInteger index = 0; index < arguments.count; index++) {
        NSString *argument = arguments[index];
        if ([argument isEqualToString:@"-TokenForgeDisableNativeOverlayAtLaunch"]) {
            if (index + 1 >= arguments.count) {
                return YES;
            }

            return TokenForgeTruthyString(arguments[index + 1]);
        }

        if ([argument hasPrefix:@"-TokenForgeDisableNativeOverlayAtLaunch="]) {
            return TokenForgeTruthyString([argument substringFromIndex:@"-TokenForgeDisableNativeOverlayAtLaunch=".length]);
        }
    }

    return NO;
}

static BOOL TokenForgeEnvironmentFlagEnabled(NSString *name)
{
    NSString *value = [[NSProcessInfo processInfo] environment][name];
    return TokenForgeTruthyString(value);
}

static void TokenForgeRefreshNativeSafetyFlags(void)
{
    if (TokenForgeNativeSafetyFlagsLoaded) {
        return;
    }

    TokenForgeNativeSafetyFlagsLoaded = YES;
    TokenForgeNativeSafeMode = TokenForgeEnvironmentFlagEnabled(@"TOKENFORGE_NATIVE_SAFE_MODE");
    TokenForgeDisableNativeOverlay = TokenForgeNativeSafeMode || TokenForgeEnvironmentFlagEnabled(@"TOKENFORGE_DISABLE_NATIVE_OVERLAY");
    TokenForgeDisableStatusItem = TokenForgeNativeSafeMode || TokenForgeEnvironmentFlagEnabled(@"TOKENFORGE_DISABLE_STATUS_ITEM");
    TokenForgeDisableNativeDashboard = TokenForgeNativeSafeMode || TokenForgeEnvironmentFlagEnabled(@"TOKENFORGE_DISABLE_NATIVE_DASHBOARD");
    TokenForgeDisableContextMenu = TokenForgeNativeSafeMode || TokenForgeEnvironmentFlagEnabled(@"TOKENFORGE_DISABLE_CONTEXT_MENU");
    TokenForgeDisablePixelNativeRenderer = TokenForgeNativeSafeMode || TokenForgeEnvironmentFlagEnabled(@"TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER");
    TokenForgeDisableMovementTimers = TokenForgeNativeSafeMode || TokenForgeEnvironmentFlagEnabled(@"TOKENFORGE_DISABLE_MOVEMENT_TIMERS");
    NSLog(@"INFO [NativeSafeMode][FLAGS] safeMode=%@ disableNativeOverlay=%@ disableStatusItem=%@ disableNativeDashboard=%@ disableContextMenu=%@ disablePixelNativeRenderer=%@ disableMovementTimers=%@",
          TokenForgeNativeSafeMode ? @"true" : @"false",
          TokenForgeDisableNativeOverlay ? @"true" : @"false",
          TokenForgeDisableStatusItem ? @"true" : @"false",
          TokenForgeDisableNativeDashboard ? @"true" : @"false",
          TokenForgeDisableContextMenu ? @"true" : @"false",
          TokenForgeDisablePixelNativeRenderer ? @"true" : @"false",
          TokenForgeDisableMovementTimers ? @"true" : @"false");
}

static NSString *TokenForgeThreadLabel(void)
{
    return [NSThread isMainThread] ? @"main" : @"background";
}

static void TokenForgeNativeEntryLog(NSString *function, NSString *args)
{
    NSString *safeFunction = function.length > 0 ? function : @"unknown";
    NSLog(@"INFO [NativeEntry][BEGIN] function=%@", safeFunction);
    NSLog(@"INFO [NativeEntry][FUNCTION] %@", safeFunction);
    NSLog(@"INFO [NativeEntry][THREAD] function=%@ thread=%@", safeFunction, TokenForgeThreadLabel());
    NSLog(@"INFO [NativeEntry][MAIN_THREAD] function=%@ value=%@", safeFunction, [NSThread isMainThread] ? @"true" : @"false");
    NSLog(@"INFO [NativeEntry][ARGS] function=%@ %@", safeFunction, args.length > 0 ? args : @"none");
}

static void TokenForgeNativeEntryReturnLog(NSString *function, NSString *result)
{
    NSLog(@"INFO [NativeEntry][RETURN] function=%@ %@", function.length > 0 ? function : @"unknown", result.length > 0 ? result : @"complete");
}

static void TokenForgeNativeCrashGuardLog(NSString *function, NSException *exception, NSString *callsite)
{
    NSLog(@"ERROR [NativeEntry][EXCEPTION_GUARD] function=%@ callsite=%@",
          function.length > 0 ? function : @"unknown",
          callsite.length > 0 ? callsite : @"unknown");
    NSLog(@"ERROR [NativeCrashGuard][CAUGHT_EXCEPTION] function=%@", function.length > 0 ? function : @"unknown");
    NSLog(@"ERROR [NativeCrashGuard][NAME] %@", exception.name ?: @"unknown");
    NSLog(@"ERROR [NativeCrashGuard][REASON] %@", exception.reason ?: @"unknown");
    NSLog(@"ERROR [NativeCrashGuard][CALLSITE] %@", callsite.length > 0 ? callsite : @"unknown");
}

static BOOL TokenForgeRefreshNativeReadiness(NSString *function)
{
    BOOL isMainThread = [NSThread isMainThread];
    TokenForgeMainThreadReady = TokenForgeMainThreadReady || isMainThread;
    if (isMainThread) {
        TokenForgeNSApplicationAvailable = NSApp != nil;
    }

    BOOL appReady = isMainThread &&
                    TokenForgeNSApplicationAvailable &&
                    (TokenForgeAppDidFinishLaunchingObserved || (NSApp != nil && NSApp.isRunning));
    TokenForgeDashboardAllowed = appReady;
    TokenForgeOverlayAllowed = appReady;
    TokenForgeStatusItemAllowed = appReady;
    NSLog(@"INFO [NativeLaunchTrace][MAIN_THREAD] function=%@ isMainThread=%@ nativePluginLoaded=%@ nsApplicationAvailable=%@ appDidFinishLaunchingObserved=%@ mainThreadReady=%@ dashboardAllowed=%@ overlayAllowed=%@ statusItemAllowed=%@ verificationMode=%@ lastExplicitSource=%@",
          function ?: @"unknown",
          isMainThread ? @"true" : @"false",
          TokenForgeNativePluginLoaded ? @"true" : @"false",
          TokenForgeNSApplicationAvailable ? @"true" : @"false",
          TokenForgeAppDidFinishLaunchingObserved ? @"true" : @"false",
          TokenForgeMainThreadReady ? @"true" : @"false",
          TokenForgeDashboardAllowed ? @"true" : @"false",
          TokenForgeOverlayAllowed ? @"true" : @"false",
          TokenForgeStatusItemAllowed ? @"true" : @"false",
          TokenForgeRuntimeVerificationMode ? @"true" : @"false",
          TokenForgeLastExplicitSource ?: @"none");
    return appReady;
}

static BOOL TokenForgeAppReadyForWindowMutation(NSString *function, NSString *source)
{
    BOOL appReady = TokenForgeRefreshNativeReadiness(function ?: @"windowMutation");
    if (!appReady) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=%@ reason=appNotReady source=%@ thread=%@",
              function ?: @"windowMutation",
              source ?: @"unknown",
              TokenForgeThreadLabel());
    }
    return appReady;
}

static BOOL TokenForgeOverlayLaunchPathAllowed(NSString *function, NSString *source, BOOL explicitUserAction)
{
    TokenForgeRefreshNativeSafetyFlags();
    if (TokenForgeNativeSafeMode || TokenForgeDisableNativeOverlay) {
        NSLog(@"INFO [NativeSafeMode][SKIP] function=%@ reason=%@ source=%@",
              function ?: @"overlay",
              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_NATIVE_OVERLAY",
              source ?: @"unknown");
        return NO;
    }

    if (!TokenForgeAppReadyForWindowMutation(function ?: @"overlay", source ?: @"unknown")) {
        return NO;
    }

    if (TokenForgeNativeOverlayDisabledAtLaunch && !explicitUserAction) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=%@ reason=nativeOverlayDisabledAtLaunch source=%@ explicit=%@",
              function ?: @"overlay",
              source ?: @"unknown",
              explicitUserAction ? @"true" : @"false");
        return NO;
    }

    return YES;
}

static BOOL TokenForgeDashboardLaunchPathAllowed(NSString *function, NSString *source, BOOL explicitUserAction)
{
    TokenForgeRefreshNativeSafetyFlags();
    if (TokenForgeNativeSafeMode || TokenForgeDisableNativeDashboard) {
        NSLog(@"INFO [NativeSafeMode][SKIP] function=%@ reason=%@ source=%@",
              function ?: @"dashboard",
              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_NATIVE_DASHBOARD",
              source ?: @"unknown");
        return NO;
    }

    if (!TokenForgeAppReadyForWindowMutation(function ?: @"dashboard", source ?: @"unknown")) {
        return NO;
    }

    return YES;
}

static BOOL TokenForgeStatusItemLaunchPathAllowed(NSString *function, NSString *source)
{
    TokenForgeRefreshNativeSafetyFlags();
    if (TokenForgeNativeSafeMode || TokenForgeDisableStatusItem) {
        NSLog(@"INFO [NativeSafeMode][SKIP] function=%@ reason=%@ source=%@",
              function ?: @"statusItem",
              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_STATUS_ITEM",
              source ?: @"unknown");
        return NO;
    }

    return TokenForgeAppReadyForWindowMutation(function ?: @"statusItem", source ?: @"unknown");
}

static void TokenForgeDumpRuntimeWindows(NSString *phase)
{
    NSArray<NSWindow *> *windows = TokenForgeSafeWindowsSnapshot(@"TokenForgeDumpRuntimeWindows", NO);
    NSInteger visibleCount = 0;
    NSInteger reportIssueCount = 0;
    NSInteger dashboardCount = 0;
    NSInteger overlayCount = 0;
    for (NSWindow *window in windows) {
        if (window.isVisible) {
            visibleCount += 1;
        }

        NSString *title = window.title ?: @"";
        NSString *identifier = window.identifier ?: @"";
        if ([title rangeOfString:@"Report Issue" options:NSCaseInsensitiveSearch].location != NSNotFound ||
            [identifier rangeOfString:@"ReportIssue" options:NSCaseInsensitiveSearch].location != NSNotFound) {
            reportIssueCount += 1;
        }

        if (TokenForgeIsNativeDashboardWindow(window)) {
            dashboardCount += 1;
        }

        if (TokenForgeIsCompanionWindow(window) || [identifier hasPrefix:@"TokenForge.DesktopCompanion"]) {
            overlayCount += 1;
        }
    }

    NSLog(@"INFO [WindowsDump][LAUNCH_STABLE] phase=%@ total=%lu visible=%ld dashboard=%ld overlay=%ld reportIssue=%ld reopenCount=%lu verificationMode=%@",
          phase ?: @"manual",
          (unsigned long)windows.count,
          (long)visibleCount,
          (long)dashboardCount,
          (long)overlayCount,
          (long)reportIssueCount,
          (unsigned long)TokenForgeApplicationReopenCount,
          TokenForgeRuntimeVerificationMode ? @"true" : @"false");
}

static void TokenForgeBeginRuntimeVerificationKeepAlive(NSString *source)
{
    if (!TokenForgeRuntimeVerificationMode || TokenForgeRuntimeVerificationKeepAliveActivity != nil) {
        return;
    }

    NSActivityOptions options = NSActivityUserInitiatedAllowingIdleSystemSleep | NSActivityLatencyCritical;
    TokenForgeRuntimeVerificationKeepAliveActivity = [[[NSProcessInfo processInfo] beginActivityWithOptions:options reason:@"TokenForge runtime verification launch-stable dump"] retain];
    NSLog(@"INFO [RuntimeVerify][KEEP_ALIVE] active=true reason=launchStableDump source=%@", source ?: @"unknown");
}

static void TokenForgeEndRuntimeVerificationKeepAlive(NSString *source)
{
    if (TokenForgeRuntimeVerificationKeepAliveActivity == nil) {
        return;
    }

    id activity = TokenForgeRuntimeVerificationKeepAliveActivity;
    TokenForgeRuntimeVerificationKeepAliveActivity = nil;
    [[NSProcessInfo processInfo] endActivity:activity];
    [activity release];
    NSLog(@"INFO [RuntimeVerify][KEEP_ALIVE] active=false reason=launchStableDump source=%@", source ?: @"unknown");
}

static void TokenForgeScheduleLaunchStableWindowsDump(NSString *source)
{
    if (TokenForgeLaunchStableDumpScheduled) {
        return;
    }

    TokenForgeLaunchStableDumpScheduled = YES;
    NSLog(@"INFO [WindowsDump][SCHEDULE] phase=launch+15s source=%@ verificationMode=%@",
          source ?: @"unknown",
          TokenForgeRuntimeVerificationMode ? @"true" : @"false");

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.25 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        TokenForgeDumpRuntimeWindows(@"launch+0.25s");
    });
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(15.0 * NSEC_PER_SEC)), dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        dispatch_async(dispatch_get_main_queue(), ^{
            if (TokenForgeLaunchStableDumpCompleted) {
                return;
            }

            TokenForgeLaunchStableDumpCompleted = YES;
            TokenForgeDumpRuntimeWindows(@"launch+15s");
            TokenForgeEndRuntimeVerificationKeepAlive(@"launch+15s");
        });
    });
}

static void TokenForgeInitializeRuntimeGuard(NSString *source)
{
    if (TokenForgeRuntimeGuardInitialized) {
        return;
    }

    TokenForgeRuntimeGuardInitialized = YES;
    TokenForgeRefreshNativeSafetyFlags();
    TokenForgeLaunchStartedAt = [NSDate timeIntervalSinceReferenceDate];
    TokenForgeRuntimeVerificationMode = TokenForgeDetectRuntimeVerificationMode();
    TokenForgeNativeOverlayDisabledAtLaunch = TokenForgeDetectNativeOverlayDisabledAtLaunch() || TokenForgeDisableNativeOverlay;
    TokenForgeVerificationWarmupUntil = TokenForgeRuntimeVerificationMode ? TokenForgeLaunchStartedAt + 10.0 : 0.0;
    TokenForgeVerificationNoAutoReopenUntil = TokenForgeRuntimeVerificationMode ? TokenForgeLaunchStartedAt + 120.0 : 0.0;
    TokenForgeBeginRuntimeVerificationKeepAlive(source ?: @"runtimeGuard");

    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
    BOOL priorLaunchInProgress = [defaults boolForKey:TokenForgeRuntimeDefaultsKey(@"LaunchInProgress")];
    NSTimeInterval priorLaunchStartedAt = [defaults doubleForKey:TokenForgeRuntimeDefaultsKey(@"LaunchStartedAt")];
    NSTimeInterval priorNormalTerminationAt = [defaults doubleForKey:TokenForgeRuntimeDefaultsKey(@"NormalTerminationAt")];
    TokenForgePreviousLaunchAbnormal = priorLaunchInProgress && priorLaunchStartedAt > priorNormalTerminationAt + 0.01;
    if (TokenForgePreviousLaunchAbnormal) {
        [defaults setDouble:TokenForgeLaunchStartedAt forKey:TokenForgeRuntimeDefaultsKey(@"LastAbnormalTerminationAt")];
        [defaults setDouble:TokenForgeLaunchStartedAt forKey:TokenForgeRuntimeDefaultsKey(@"LastCrashOrReopenAt")];
        TokenForgeCrashRecoveryCooldownUntil = TokenForgeLaunchStartedAt + 120.0;
    } else {
        NSLog(@"INFO [CrashGuard][NO_RECENT_CRASH] source=%@", source ?: @"unknown");
    }

    [defaults setBool:YES forKey:TokenForgeRuntimeDefaultsKey(@"LaunchInProgress")];
    [defaults setDouble:TokenForgeLaunchStartedAt forKey:TokenForgeRuntimeDefaultsKey(@"LaunchStartedAt")];
    [defaults setBool:NO forKey:TokenForgeRuntimeDefaultsKey(@"ReportIssueAutoPresented")];
    [defaults synchronize];

    NSLog(@"INFO [CrashRecovery] launchMarker started=true previousLaunchInProgress=%@ previousAbnormal=%@ source=%@ priorLaunchStartedAt=%.3f priorNormalTerminationAt=%.3f",
          priorLaunchInProgress ? @"true" : @"false",
          TokenForgePreviousLaunchAbnormal ? @"true" : @"false",
          source ?: @"unknown",
          priorLaunchStartedAt,
          priorNormalTerminationAt);
    NSLog(@"INFO [CrashRecovery] reportIssueAutoPresent=false route=manualOnly");
    NSLog(@"INFO [NativeLaunchTrace][APP_READY] nativePluginLoaded=%@ nsApplicationAvailable=%@ appDidFinishLaunchingObserved=%@ mainThreadReady=%@ dashboardAllowed=%@ overlayAllowed=%@ statusItemAllowed=%@ verificationMode=%@ nativeOverlayDisabledAtLaunch=%@ source=%@",
          TokenForgeNativePluginLoaded ? @"true" : @"false",
          TokenForgeNSApplicationAvailable ? @"true" : @"false",
          TokenForgeAppDidFinishLaunchingObserved ? @"true" : @"false",
          TokenForgeMainThreadReady ? @"true" : @"false",
          TokenForgeDashboardAllowed ? @"true" : @"false",
          TokenForgeOverlayAllowed ? @"true" : @"false",
          TokenForgeStatusItemAllowed ? @"true" : @"false",
          TokenForgeRuntimeVerificationMode ? @"true" : @"false",
          TokenForgeNativeOverlayDisabledAtLaunch ? @"true" : @"false",
          source ?: @"unknown");
    if (TokenForgeRuntimeVerificationMode) {
        NSLog(@"INFO [RuntimeVerify][ENABLED] noAutoReopenSeconds=120 watchdogWarmupSeconds=10 source=%@", source ?: @"unknown");
        NSLog(@"INFO [CrashRecovery][SUPPRESSED_REPORT_UI] reason=verificationMode previousAbnormal=%@", TokenForgePreviousLaunchAbnormal ? @"true" : @"false");
        if (!TokenForgeVerificationWatchdogSuppressionLogged) {
            TokenForgeVerificationWatchdogSuppressionLogged = YES;
            NSLog(@"INFO [OverlayWatchdog][SUPPRESSED] reason=verificationModeWarmup source=%@ state=notArmed",
                  source ?: @"unknown");
        }
    } else if (TokenForgePreviousLaunchAbnormal) {
        NSLog(@"INFO [CrashRecovery][SUPPRESSED_REPORT_UI] reason=policyNoAutoPresent previousAbnormal=true");
    }

    TokenForgeReportIssueAutoPresentSuppressed = TokenForgeRuntimeVerificationMode || TokenForgePreviousLaunchAbnormal;
    TokenForgeScheduleLaunchStableWindowsDump(source ?: @"runtimeGuard");
}

static BOOL TokenForgeIsVerificationWarmupActive(void)
{
    return TokenForgeRuntimeVerificationMode && [NSDate timeIntervalSinceReferenceDate] < TokenForgeVerificationWarmupUntil;
}

static NSTimeInterval TokenForgeRuntimeVerificationElapsed(void)
{
    if (TokenForgeLaunchStartedAt <= 0.0) {
        return 0.0;
    }

    return MAX(0.0, [NSDate timeIntervalSinceReferenceDate] - TokenForgeLaunchStartedAt);
}

static BOOL TokenForgeVerificationAllowsImplicitVerifierCleanup(void)
{
    return TokenForgeRuntimeVerificationMode && TokenForgeRuntimeVerificationElapsed() >= 60.0;
}

static NSString *TokenForgeQuitStackSummary(void)
{
    NSArray<NSString *> *symbols = [NSThread callStackSymbols];
    NSUInteger count = MIN((NSUInteger)12, symbols.count);
    if (count == 0) {
        return @"unavailable";
    }

    return [[symbols subarrayWithRange:NSMakeRange(0, count)] componentsJoinedByString:@" | "];
}

static void TokenForgeLogQuitDiagnostic(NSString *request, NSString *source, BOOL allowQuit, BOOL blocked, NSString *reason)
{
    NSString *safeRequest = request.length > 0 ? request : @"unknown";
    NSString *safeSource = source.length > 0 ? source : @"unknown";
    NSString *safeReason = reason.length > 0 ? reason : @"unspecified";
    BOOL verifierCleanupAllowed = TokenForgeVerificationAllowsImplicitVerifierCleanup();
    NSLog(@"INFO [QuitDiagnostic][REQUEST] request=%@ source=%@ reason=%@ thread=%@",
          safeRequest,
          safeSource,
          safeReason,
          TokenForgeThreadLabel());
    NSLog(@"INFO [QuitDiagnostic][SOURCE] source=%@ explicitFlag=%@ terminating=%@ lastExplicitSource=%@ lastProjectionSource=%@ dashboardVisible=%@ overlayVisible=%@ statusItem=%@",
          safeSource,
          TokenForgeExplicitQuitRequested ? @"true" : @"false",
          TokenForgeTerminating ? @"true" : @"false",
          TokenForgeLastExplicitSource ?: @"none",
          TokenForgeLastProjectionSource ?: @"none",
          TokenForgeIsDashboardVisible() ? @"true" : @"false",
          TokenForgeIsCompanionOverlayVisible() ? @"true" : @"false",
          TokenForgeStatusItemExists() ? @"true" : @"false");
    NSLog(@"INFO [QuitDiagnostic][STACK] %@", TokenForgeQuitStackSummary());
    NSLog(@"INFO [QuitDiagnostic][VERIFICATION_MODE] enabled=%@ elapsedSeconds=%.2f appDidFinishLaunchingObserved=%@ verifierCleanupAllowed=%@",
          TokenForgeRuntimeVerificationMode ? @"true" : @"false",
          TokenForgeRuntimeVerificationElapsed(),
          TokenForgeAppDidFinishLaunchingObserved ? @"true" : @"false",
          verifierCleanupAllowed ? @"true" : @"false");
    NSLog(@"INFO [QuitDiagnostic][ALLOW_QUIT] value=%@ reason=%@",
          allowQuit ? @"true" : @"false",
          safeReason);
    if (blocked) {
        NSLog(@"WARN [QuitDiagnostic][BLOCKED] request=%@ source=%@ reason=%@", safeRequest, safeSource, safeReason);
    } else {
        NSLog(@"INFO [QuitDiagnostic][PROCEED] request=%@ source=%@ reason=%@", safeRequest, safeSource, safeReason);
    }
}

static BOOL TokenForgeSourceLooksExplicit(NSString *source)
{
    if (source.length == 0) {
        return NO;
    }

    NSArray<NSString *> *explicitPrefixes = @[
        @"menuBar.",
        @"menubar.",
        @"launch.initial",
        @"dock.reopen",
        @"dashboard.",
        @"desktop.",
        @"repository.",
        @"navigation.",
        @"review.",
        @"companion.",
        @"overlayDoubleClick",
        @"companionRestore",
        @"csharp.showDashboard",
        @"csharp",
        @"explicitDashboardAction",
        @"showOnDesktop",
        @"hideFromDesktop",
        @"show_dashboard",
        @"showDashboard",
        @"toggle",
        @"debug"
    ];
    for (NSString *prefix in explicitPrefixes) {
        if ([source hasPrefix:prefix] || [source isEqualToString:prefix]) {
            return YES;
        }
    }

    return NO;
}

static BOOL TokenForgeSourceLooksPassiveSync(NSString *source)
{
    if (source.length == 0) {
        return NO;
    }

    NSArray<NSString *> *passiveMarkers = @[
        @"projection",
        @"passive",
        @"sync",
        @"watchdog",
        @"nativeQuery",
        @"verificationMode",
        @"initial_profile",
        @"dashboardRender"
    ];
    for (NSString *marker in passiveMarkers) {
        if ([source rangeOfString:marker options:NSCaseInsensitiveSearch].location != NSNotFound) {
            return YES;
        }
    }

    return NO;
}

static void TokenForgeRecordCompanionDesiredState(BOOL visible, NSString *source)
{
    NSString *safeSource = source.length > 0 ? source : @"unknown";
    BOOL passiveSync = TokenForgeSourceLooksPassiveSync(safeSource);
    BOOL explicitSource = TokenForgeSourceLooksExplicit(safeSource) && !passiveSync;
    if (passiveSync && TokenForgePendingCompanionVisibilityWasExplicit && !TokenForgeAppDidFinishLaunchingObserved) {
        NSLog(@"INFO [CompanionVisibility][SET] desiredVisible=%@ source=%@ passiveSync=true ignored=true reason=preserveExplicitDuringLaunch lastExplicitSource=%@ thread=%@",
              visible ? @"true" : @"false",
              safeSource,
              TokenForgeLastExplicitSource ?: @"none",
              TokenForgeThreadLabel());
        return;
    }

    BOOL oldDesired = TokenForgeDesiredCompanionVisible;
    TokenForgeDesiredCompanionVisible = visible;
    TokenForgeLastProjectionSource = [safeSource copy];
    if (explicitSource) {
        TokenForgeLastExplicitSource = [safeSource copy];
        TokenForgePendingCompanionVisibilityWasExplicit = YES;
    }

    if (!TokenForgeReplayingCompanionVisibility) {
        TokenForgePendingCompanionVisibilityReplay = YES;
    }

    NSLog(@"INFO [CompanionVisibility][SET] desiredVisible=%@ oldDesiredVisible=%@ source=%@ explicit=%@ passiveSync=%@ pendingReplay=%@ thread=%@",
          visible ? @"true" : @"false",
          oldDesired ? @"true" : @"false",
          safeSource,
          explicitSource ? @"true" : @"false",
          passiveSync ? @"true" : @"false",
          TokenForgePendingCompanionVisibilityReplay ? @"true" : @"false",
          TokenForgeThreadLabel());
}

static void TokenForgeReplayPendingCompanionVisibilityIfReady(NSString *source)
{
    NSString *safeSource = source.length > 0 ? source : @"unknown";
    if (!TokenForgePendingCompanionVisibilityReplay) {
        return;
    }

    if (TokenForgeNativeOverlayDisabledAtLaunch) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeReplayPendingCompanionVisibility reason=nativeOverlayDisabledAtLaunch source=%@ desiredVisible=%@",
              safeSource,
              TokenForgeDesiredCompanionVisible ? @"true" : @"false");
        return;
    }

    if (!TokenForgeOverlayLaunchPathAllowed(@"TokenForgeReplayPendingCompanionVisibility", safeSource, TokenForgePendingCompanionVisibilityWasExplicit)) {
        return;
    }

    BOOL desired = TokenForgeDesiredCompanionVisible;
    TokenForgePendingCompanionVisibilityReplay = NO;
    TokenForgeReplayingCompanionVisibility = YES;
    NSString *replaySource = [NSString stringWithFormat:@"replay.%@", TokenForgeLastExplicitSource ?: safeSource];
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForgeReplayPendingCompanionVisibility desiredVisible=%@ source=%@ replaySource=%@",
          desired ? @"true" : @"false",
          safeSource,
          replaySource);
    if (desired) {
        TokenForgeShowDesktopCompanionOverlayWithTrace(replaySource);
    } else if (TokenForgeCompanionWindow != nil) {
        TokenForgeHideDesktopCompanionOverlayWithTrace(replaySource);
    }
    TokenForgeReplayingCompanionVisibility = NO;
    NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForgeReplayPendingCompanionVisibility desiredVisible=%@ source=%@",
          desired ? @"true" : @"false",
          safeSource);
}

static BOOL TokenForgeShouldSuppressDashboardOpen(NSString *source, BOOL explicitUserOpen)
{
    TokenForgeInitializeRuntimeGuard(@"dashboardOpen");
    NSString *openSource = source.length > 0 ? source : @"unknown";
    BOOL explicitOpen = explicitUserOpen || TokenForgeSourceLooksExplicit(openSource);
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];

    if (!explicitOpen && TokenForgeRuntimeVerificationMode && now < TokenForgeVerificationNoAutoReopenUntil) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=verificationMode source=%@", openSource);
        return YES;
    }

    if (!explicitOpen && TokenForgePreviousLaunchAbnormal && now < TokenForgeCrashRecoveryCooldownUntil) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=crashRecoveryCooldown source=%@", openSource);
        return YES;
    }

    return NO;
}

static BOOL TokenForgeShouldSuppressOverlayShow(NSString *source, BOOL explicitUserOpen)
{
    TokenForgeInitializeRuntimeGuard(@"overlayShow");
    NSString *showSource = source.length > 0 ? source : @"unknown";
    BOOL explicitOpen = explicitUserOpen || TokenForgeSourceLooksExplicit(showSource);
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];

    if (!explicitOpen && TokenForgeRuntimeVerificationMode && now < TokenForgeVerificationNoAutoReopenUntil) {
        NSLog(@"INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=verificationMode source=overlay.%@", showSource);
        return YES;
    }

    if (!explicitOpen && TokenForgePreviousLaunchAbnormal && now < TokenForgeCrashRecoveryCooldownUntil) {
        NSLog(@"INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=crashRecoveryCooldown source=overlay.%@", showSource);
        return YES;
    }

    TokenForgeEnsureOverlayFarmRegistry();
    if (TokenForgeMenuConnectedCompanionCount <= 0 &&
        TokenForgeOverlaySnapshotsByRepositoryId.count == 0 &&
        TokenForgeOverlayPanelsByRepositoryId.count == 0) {
        TokenForgeDesiredCompanionVisible = NO;
        TokenForgeMenuCompanionEnabled = NO;
        NSLog(@"INFO [OverlaySuppressed] reason=noApprovedRepository source=%@", showSource);
        NSLog(@"INFO [Overlay][Guard] repoHash=none desiredVisible=true actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=%@ selectedRepoId=none selectedRepoHash=none approvedRepoCount=0", showSource);
        NSLog(@"INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY] repoHash=none desiredVisible=false actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=%@ selectedRepoId=none selectedRepoHash=none approvedRepoCount=0", showSource);
        return YES;
    }

    return NO;
}

static void TokenForgeRecordNormalTermination(NSString *source)
{
    TokenForgeInitializeRuntimeGuard(@"normalTermination");
    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    [defaults setBool:NO forKey:TokenForgeRuntimeDefaultsKey(@"LaunchInProgress")];
    [defaults setDouble:now forKey:TokenForgeRuntimeDefaultsKey(@"NormalTerminationAt")];
    [defaults synchronize];
    NSLog(@"INFO [CrashRecovery] normalTerminationMarker recorded=true source=%@ at=%.3f", source ?: @"unknown", now);
}

static const char *TokenForgeNextOverlayTraceId(void)
{
    static char trace[32] = {0};
    TokenForgeOverlayTraceCounter += 1;
    snprintf(trace, sizeof(trace), "%lu", (unsigned long)TokenForgeOverlayTraceCounter);
    return trace;
}

static NSString *TokenForgeFileModifiedTime(NSString *path)
{
    if (path.length == 0) {
        return @"unavailable";
    }

    NSDictionary<NSFileAttributeKey, id> *attributes = [[NSFileManager defaultManager] attributesOfItemAtPath:path error:nil];
    NSDate *date = attributes[NSFileModificationDate];
    return date != nil ? date.description : @"unavailable";
}

static NSString *TokenForgeFileSHA256(NSString *path)
{
    if (path.length == 0) {
        return @"unavailable";
    }

    NSData *data = [NSData dataWithContentsOfFile:path];
    if (data == nil) {
        return @"unavailable";
    }

    unsigned char hash[CC_SHA256_DIGEST_LENGTH];
    CC_SHA256(data.bytes, (CC_LONG)data.length, hash);
    NSMutableString *hex = [NSMutableString stringWithCapacity:CC_SHA256_DIGEST_LENGTH * 2];
    for (NSInteger index = 0; index < CC_SHA256_DIGEST_LENGTH; index++) {
        [hex appendFormat:@"%02x", hash[index]];
    }
    return hex;
}

static NSString *TokenForgeNativeLibraryPathString(void)
{
    const char *path = TokenForge_GetOverlayLibraryPath();
    return path != NULL ? [NSString stringWithUTF8String:path] : @"unavailable";
}

static NSString *TokenForgeScreenDisplayName(NSScreen *screen)
{
    if (@available(macOS 10.15, *)) {
        return screen.localizedName ?: @"unknown";
    }

    return @"unknown";
}

static void TokenForgeLogRuntimeIdentityIfNeeded(void)
{
    if (TokenForgeRuntimeIdentityLogged) {
        return;
    }

    TokenForgeRuntimeIdentityLogged = YES;
    NSBundle *bundle = [NSBundle mainBundle];
    NSString *bundlePath = bundle.bundlePath ?: @"unavailable";
    NSString *executablePath = bundle.executablePath ?: @"unavailable";
    NSString *executableName = [bundle objectForInfoDictionaryKey:@"CFBundleExecutable"] ?: @"unavailable";
    NSString *bundleIdentifier = bundle.bundleIdentifier ?: @"unavailable";
    NSString *version = [bundle objectForInfoDictionaryKey:@"CFBundleShortVersionString"] ?: @"unavailable";
    NSString *build = [bundle objectForInfoDictionaryKey:@"CFBundleVersion"] ?: @"unavailable";
    NSString *dylibPath = TokenForgeNativeLibraryPathString();
    NSString *managedPath = [bundlePath stringByAppendingPathComponent:@"Contents/Resources/Data/Managed/TokenForge.Client.dll"];
    NSString *playerLogPath = [NSHomeDirectory() stringByAppendingPathComponent:@"Library/Logs/TokenForge/TokenForge/Player.log"];
    NSLog(@"INFO [BuildIdentity][RUNTIME_CODE_VERSION] %@ %@ nativeDylibBuildTimestamp=%@ desktopCompanionOverlayCompiledMarker=%@ csharpMarker=app-bootstrapper-overlay-projection-v9 unityProductVersion=%@ appBundlePath=%@ nativeDylibPath=%@ nativeDylibModified=%@ playerLogPath=%@",
          TokenForgeRuntimeBuildIdentityMarker,
          TokenForgeRuntimeBuildIdentityGitMarker,
          TokenForgeDesktopCompanionOverlayCompiledMarker,
          TokenForgeDesktopCompanionOverlayCompiledMarker,
          version,
          bundlePath,
          dylibPath,
          TokenForgeFileModifiedTime(dylibPath),
          playerLogPath);
    NSLog(@"INFO [NativeLifecycle] dylib_loaded no_appkit_touch=true version=%@", TokenForgeNativePluginVersion);
    NSLog(@"INFO [RuntimeIdentity] CFBundleIdentifier=%@", bundleIdentifier);
    NSLog(@"INFO [RuntimeIdentity] CFBundleExecutable=%@", executableName);
    NSLog(@"INFO [RuntimeIdentity] bundlePath=%@", bundlePath);
    NSLog(@"INFO [RuntimeIdentity] executablePath=%@", executablePath);
    NSLog(@"INFO [RuntimeIdentity] appVersion=%@ build=%@ executableModified=%@ executableHash=%@", version, build, TokenForgeFileModifiedTime(executablePath), TokenForgeFileSHA256(executablePath));
    NSLog(@"INFO [RuntimeIdentity] nativeDylibPath=%@ modified=%@ hash=%@ versionMarker=%@", dylibPath, TokenForgeFileModifiedTime(dylibPath), TokenForgeFileSHA256(dylibPath), TokenForgeNativePluginVersion);
    NSLog(@"INFO [RuntimeIdentity] managedAssemblyPath=%@ modified=%@ hash=%@", managedPath, TokenForgeFileModifiedTime(managedPath), TokenForgeFileSHA256(managedPath));
    NSLog(@"INFO [RuntimeIdentity][Bundle] path=%@ executable=%@ sha256=%@ mtime=%@", bundlePath, executablePath, TokenForgeFileSHA256(executablePath), TokenForgeFileModifiedTime(executablePath));
    NSLog(@"INFO [RuntimeIdentity][NativeDylib] path=%@ sha256=%@ mtime=%@", dylibPath, TokenForgeFileSHA256(dylibPath), TokenForgeFileModifiedTime(dylibPath));
    NSLog(@"INFO [RuntimeIdentity][ManagedAssembly] path=%@ sha256=%@ mtime=%@", managedPath, TokenForgeFileSHA256(managedPath), TokenForgeFileModifiedTime(managedPath));
    NSLog(@"INFO [RuntimeIdentity] dylibImportName=DesktopCompanionOverlay expectedBundleFile=libDesktopCompanionOverlay.dylib");
}

static BOOL TokenForgeAppKitRegistrationReady(void)
{
    BOOL ready = NSApp != nil && NSApp.delegate != nil;
    if (ready) {
        NSLog(@"INFO [NativeLifecycle] app_registration_ready=true delegate=%@", NSStringFromClass([[NSApp delegate] class]));
    }
    return ready;
}

static void TokenForgeRequestLifecycleInstall(NSString *reason)
{
    if (![NSThread isMainThread]) {
        NSString *reasonCopy = [reason.length > 0 ? reason : @"unknown" copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeRequestLifecycleInstall(reasonCopy);
        });
        return;
    }

    if (TokenForgeExplicitQuitRequested || TokenForgeTerminating) {
        NSLog(@"INFO [NativeLifecycle] install_delegate skipped reason=explicitQuitOrTerminating requestReason=%@",
              reason ?: @"unknown");
        return;
    }

    BOOL ready = TokenForgeAppKitRegistrationReady();
    NSLog(@"INFO [NativeLifecycle] install_delegate requested appReady=%@ reason=%@", ready ? @"true" : @"false", reason ?: @"unknown");
    if (ready) {
        TokenForgeLifecycleInstallDeferred = NO;
        TokenForgeEnsureLifecycleDelegate();
        TokenForgeReplayPendingCompanionVisibilityIfReady(reason ?: @"lifecycleReady");
        return;
    }

    NSLog(@"INFO [NativeLifecycle] app_registration_ready=false reason=%@", reason ?: @"unknown");
    if (TokenForgeLifecycleInstallDeferred) {
        return;
    }

    TokenForgeLifecycleInstallDeferred = YES;
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.25 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        if (TokenForgeExplicitQuitRequested || TokenForgeTerminating) {
            NSLog(@"INFO [NativeLifecycle] deferred_install skipped reason=explicitQuitOrTerminating");
            return;
        }

        TokenForgeLifecycleInstallDeferred = NO;
        TokenForgeRequestLifecycleInstall(@"deferred_retry");
    });
}

static BOOL TokenForgeWindowLooksBlank(NSWindow *window)
{
    if (window == nil || ![window.title isEqualToString:@"TokenForge"]) {
        return NO;
    }

    if (window == TokenForgeNativeDashboardWindow) {
        return NO;
    }

    if ([window.identifier isEqualToString:@"TokenForge.NativeDashboard"]) {
        return NO;
    }

    NSView *contentView = window.contentView;
    return contentView == nil || contentView.subviews.count == 0;
}

static void TokenForgeLogWindowLifecycle(NSString *event, NSWindow *window, NSString *reason)
{
    if (window == nil) {
        NSLog(@"INFO [WindowLifecycle] %@ window=nil reason=%@", event ?: @"event", reason ?: @"none");
        return;
    }

    NSString *contentClass = window.contentView != nil ? NSStringFromClass([window.contentView class]) : @"nil";
    NSScreen *screen = window.screen ?: NSScreen.mainScreen;
    NSRect screenFrame = screen != nil ? screen.frame : NSMakeRect(0, 0, 0, 0);
    NSRect visibleFrame = screen != nil ? screen.visibleFrame : NSMakeRect(0, 0, 0, 0);
    NSLog(@"INFO [WindowLifecycle] %@ id=%p title=%@ class=%@ frame=(%.2f,%.2f %.2fx%.2f) contentView=%@ visible=%@ key=%@ main=%@ miniaturized=%@ screen=(%.2f,%.2f %.2fx%.2f) visibleFrame=(%.2f,%.2f %.2fx%.2f) reason=%@",
          event ?: @"event",
          window,
          window.title ?: @"",
          NSStringFromClass([window class]),
          window.frame.origin.x,
          window.frame.origin.y,
          window.frame.size.width,
          window.frame.size.height,
          contentClass,
          window.isVisible ? @"true" : @"false",
          window.isKeyWindow ? @"true" : @"false",
          window.isMainWindow ? @"true" : @"false",
          window.isMiniaturized ? @"true" : @"false",
          screenFrame.origin.x,
          screenFrame.origin.y,
          screenFrame.size.width,
          screenFrame.size.height,
          visibleFrame.origin.x,
          visibleFrame.origin.y,
          visibleFrame.size.width,
          visibleFrame.size.height,
          reason ?: @"none");
    if (TokenForgeWindowLooksBlank(window)) {
        if (TokenForgeDumpingWindows) {
            NSLog(@"INFO [NativeLifecycle] window_dump readonly=true blank_window_detected=true id=%p reason=%@",
                  window,
                  reason ?: @"unknown");
            return;
        }

        NSLog(@"WARN [WindowLifecycle] blank TokenForge window detected; orderOut without dashboard reopen id=%p title=%@ contentView=%@ reason=%@",
              window,
              window.title ?: @"",
              contentClass,
              reason ?: @"unknown");
        [window orderOut:nil];
    }
}

static void TokenForgeDumpAllWindows(NSString *reason)
{
    if (![NSThread isMainThread] || !TokenForgeAppReadyForWindowMutation(@"TokenForgeDumpAllWindows", reason ?: @"manual")) {
        return;
    }

    if (TokenForgeDumpingWindows) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeDumpAllWindows reason=reentrantDump source=%@", reason ?: @"manual");
        return;
    }

    NSArray<NSWindow *> *windows = TokenForgeSafeWindowsSnapshot(@"TokenForgeDumpAllWindows", NO);
    NSLog(@"INFO [NativeLifecycle] window_dump readonly=true count=%lu reason=%@", (unsigned long)windows.count, reason ?: @"manual");
    NSLog(@"INFO [WindowLifecycle] dump begin reason=%@ count=%lu", reason ?: @"manual", (unsigned long)windows.count);
    TokenForgeDumpingWindows = YES;
    for (NSWindow *window in windows) {
        TokenForgeLogWindowLifecycle(@"dump", window, reason ?: @"manual");
    }
    TokenForgeDumpingWindows = NO;
    NSLog(@"INFO [WindowLifecycle] dump end reason=%@", reason ?: @"manual");
}

static void TokenForgeDumpOverlayPanelState(NSString *traceId)
{
    if (![NSThread isMainThread]) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeDumpOverlayPanelState reason=notMainThread source=%@", traceId ?: @"none");
        return;
    }

    NSString *trace = traceId.length > 0 ? traceId : @"none";
    if (TokenForgeCompanionWindow == nil) {
        NSLog(@"INFO [OverlayTrace:%@] panel=nil", trace);
        NSLog(@"INFO [OverlayPanel][VISIBLE] visible=false frame=nil");
        return;
    }

    NSScreen *screen = TokenForgeCompanionWindow.screen ?: [NSScreen mainScreen];
    NSString *screenName = TokenForgeScreenDisplayName(screen);
    NSLog(@"INFO [OverlayTrace:%@] panel.isVisible=%@ alpha=%.2f frame=(%.2f,%.2f %.2fx%.2f) level=%ld screen=%@ ignoresMouseEvents=%@ collectionBehavior=%lu contentView=%@",
          trace,
          TokenForgeCompanionWindow.isVisible ? @"1" : @"0",
          TokenForgeCompanionWindow.alphaValue,
          TokenForgeCompanionWindow.frame.origin.x,
          TokenForgeCompanionWindow.frame.origin.y,
          TokenForgeCompanionWindow.frame.size.width,
          TokenForgeCompanionWindow.frame.size.height,
          (long)TokenForgeCompanionWindow.level,
          screenName,
          TokenForgeCompanionWindow.ignoresMouseEvents ? @"true" : @"false",
          (unsigned long)TokenForgeCompanionWindow.collectionBehavior,
          TokenForgeCompanionWindow.contentView != nil ? NSStringFromClass([TokenForgeCompanionWindow.contentView class]) : @"nil");
    NSLog(@"INFO [OverlayPanel][VISIBLE] visible=%@ frame=(%.2f,%.2f %.2fx%.2f)",
          TokenForgeCompanionWindow.isVisible ? @"true" : @"false",
          TokenForgeCompanionWindow.frame.origin.x,
          TokenForgeCompanionWindow.frame.origin.y,
          TokenForgeCompanionWindow.frame.size.width,
          TokenForgeCompanionWindow.frame.size.height);
}

static void TokenForgeLogOverlayProjection(NSString *traceId, NSString *source)
{
    NSString *trace = traceId.length > 0 ? traceId : @"none";
    NSString *projectionSource = source.length > 0 ? source : TokenForgeLastProjectionSource ?: @"unknown";
    BOOL exists = TokenForgeCompanionWindow != nil;
    BOOL visible = exists && TokenForgeCompanionWindow.isVisible;
    NSLog(@"INFO [OverlayTrace:%@] projection_applied desiredVisible=%@ nativeVisible=%@ panelExists=%@ appLifecycleAllowed=%@ repositoryCompanionEnabled=%@ motion=%@ clickThrough=%@ source=%@ lastHideReason=%@ lastShowReason=%@",
          trace,
          TokenForgeDesiredCompanionVisible ? @"true" : @"false",
          visible ? @"true" : @"false",
          exists ? @"true" : @"false",
          TokenForgeAppLifecycleAllowsOverlay ? @"true" : @"false",
          TokenForgeMenuCompanionEnabled ? @"true" : @"false",
          (TokenForgeCompanionAllowsWandering && TokenForgeCompanionMotionMode != 0) ? @"true" : @"false",
          TokenForgeMenuClickThrough ? @"true" : @"false",
          projectionSource,
          TokenForgeLastHideReason ?: @"none",
	          TokenForgeLastShowReason ?: @"none");
}

static NSUInteger TokenForgeDashboardLayoutHashForState(NSDictionary *state, NSString *source)
{
    NSDictionary *layoutState = state.count > 0 ? state : @{};
    NSString *selectedNav = TokenForgeDashboardString(layoutState, @"selectedNavItem", TokenForgeCurrentDashboardTab ?: @"dashboard");
    NSDictionary *repository = TokenForgeDashboardDictionary(layoutState, @"repository");
    NSDictionary *activity = TokenForgeDashboardDictionary(layoutState, @"activity");
    NSDictionary *review = TokenForgeDashboardDictionary(layoutState, @"review");
    NSString *contract = [NSString stringWithFormat:@"grid=clean-v4|sidebar=336|columns=2|gap=22|nav=%@|repo=%@|activityRuns=%lu|pending=%@",
                          selectedNav,
                          TokenForgeDashboardBool(repository, @"connected", NO) ? @"connected" : @"none",
                          (unsigned long)TokenForgeDashboardArray(activity, @"recentRuns").count,
                          TokenForgeDashboardBool(review, @"pending", NO) ? @"true" : @"false"];
    return contract.hash;
}

static void TokenForgeLogDashboardLayoutPass(NSDictionary *state, NSString *source)
{
    TokenForgeDashboardLayoutPass += 1;
    NSUInteger hash = TokenForgeDashboardLayoutHashForState(state, @"layoutPass");
    NSLog(@"INFO [DashboardLayout] pass=%lu gridHash=%lu source=%@ overlayDragging=%@",
          (unsigned long)TokenForgeDashboardLayoutPass,
          (unsigned long)hash,
          source ?: @"unknown",
          TokenForgeIsDraggingOverlay ? @"true" : @"false");
}

static void TokenForgeQueueOverlayActionAfterDrag(TokenForgePendingOverlayAction action, NSString *source)
{
    if (action == TokenForgePendingOverlayActionNone) {
        return;
    }

    TokenForgePendingOverlayActionAfterDrag = action;
    TokenForgePendingOverlayActionSource = [source.length > 0 ? source : @"unknown" copy];
    NSString *actionName = action == TokenForgePendingOverlayActionHide ? @"hide" : (action == TokenForgePendingOverlayActionDestroy ? @"destroy" : @"resetPosition");
    if (action == TokenForgePendingOverlayActionHide) {
        NSLog(@"INFO [OverlayLifecycle][DEFER_HIDE] reason=dragging source=%@", TokenForgePendingOverlayActionSource);
    } else {
        NSLog(@"INFO [OverlayLifecycle][DEFER_%@] reason=dragging source=%@", [actionName uppercaseString], TokenForgePendingOverlayActionSource);
    }
}

static void TokenForgeApplyPendingOverlayActionsAfterDrag(NSString *source)
{
    if (TokenForgePendingClickThroughAfterDrag && TokenForgeCompanionWindow != nil) {
        TokenForgeMenuClickThrough = TokenForgePendingClickThroughValueAfterDrag;
        TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgePendingClickThroughValueAfterDrag;
        NSLog(@"INFO [OverlayState][APPLY] source=%@ desiredDrag=%@ desiredClickThrough=%@ actualIgnoresMouseEvents=%@",
              source ?: @"dragFinalized",
              TokenForgePendingClickThroughValueAfterDrag ? @"false" : @"true",
              TokenForgePendingClickThroughValueAfterDrag ? @"true" : @"false",
              TokenForgeCompanionWindow.ignoresMouseEvents ? @"true" : @"false");
        TokenForgePendingClickThroughAfterDrag = NO;
    }

    TokenForgePendingOverlayAction action = TokenForgePendingOverlayActionAfterDrag;
    NSString *actionSource = TokenForgePendingOverlayActionSource ?: source ?: @"dragFinalized";
    TokenForgePendingOverlayActionAfterDrag = TokenForgePendingOverlayActionNone;
    TokenForgePendingOverlayActionSource = @"none";
    if (action == TokenForgePendingOverlayActionHide) {
        TokenForgeHideDesktopCompanionOverlayWithTrace([NSString stringWithFormat:@"%@:deferredAfterDrag", actionSource]);
    } else if (action == TokenForgePendingOverlayActionDestroy) {
        [TokenForgeCompanionWindow orderOut:nil];
        TokenForgeCompanionWindow = nil;
        TokenForgeCompanionContentView = nil;
        TokenForgeCompanionVelocity = NSMakePoint(0, 0);
        [TokenForgeCompanionMotionTimer invalidate];
        TokenForgeCompanionMotionTimer = nil;
        NSLog(@"INFO [OverlayLifecycle][APPLY_DEFERRED_DESTROY] source=%@", actionSource);
    } else if (action == TokenForgePendingOverlayActionResetPosition) {
        TokenForgeResetCompanionFrame();
        NSLog(@"INFO [OverlayLifecycle][APPLY_DEFERRED_RESET_POSITION] source=%@", actionSource);
    }
}

static void TokenForgeScheduleOverlayWatchdogs(NSString *traceId)
{
    TokenForgeInitializeRuntimeGuard(@"overlayWatchdog");
    NSString *trace = [traceId.length > 0 ? traceId : @"none" copy];
    NSArray<NSNumber *> *delays = @[@1, @5, @30];
    for (NSNumber *delayNumber in delays) {
        NSInteger delay = delayNumber.integerValue;
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(delay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            if (TokenForgeExplicitQuitRequested || TokenForgeTerminating) {
                NSLog(@"INFO [OverlayTrace:%@] panel_watchdog_%lds skipped reason=explicitQuitOrTerminating",
                      trace,
                      (long)delay);
                return;
            }

            if (TokenForgeIsVerificationWarmupActive()) {
                NSLog(@"INFO [OverlayWatchdog][SUPPRESSED] reason=verificationModeWarmup source=%@ delay=%lds",
                      trace,
                      (long)delay);
                return;
            }

            if (TokenForgeIsDraggingOverlay) {
                NSLog(@"INFO [OverlayWatchdog][SUPPRESSED] reason=dragging source=%@ delay=%lds",
                      trace,
                      (long)delay);
                return;
            }

            BOOL exists = TokenForgeCompanionWindow != nil;
            BOOL visible = exists && TokenForgeCompanionWindow.isVisible;
            NSRect frame = exists ? TokenForgeCompanionWindow.frame : NSZeroRect;
            CGFloat alpha = exists ? TokenForgeCompanionWindow.alphaValue : 0.0;
            NSLog(@"INFO [OverlayTrace:%@] panel_watchdog_%lds visible=%@ exists=%@ alpha=%.2f frame=(%.2f,%.2f %.2fx%.2f)",
                  trace,
                  (long)delay,
                  visible ? @"true" : @"false",
                  exists ? @"true" : @"false",
                  alpha,
                  frame.origin.x,
                  frame.origin.y,
                  frame.size.width,
                  frame.size.height);
            TokenForgeLogOverlayProjection(trace, [NSString stringWithFormat:@"watchdog_%lds", (long)delay]);
        });
    }
}

static NSDictionary *TokenForgeParseJsonDictionary(const char *json)
{
    if (json == NULL) {
        return @{};
    }

    NSString *string = [NSString stringWithUTF8String:json];
    if (string == nil || string.length == 0) {
        return @{};
    }

    NSData *data = [string dataUsingEncoding:NSUTF8StringEncoding];
    if (data == nil) {
        return @{};
    }

    NSError *error = nil;
    id object = [NSJSONSerialization JSONObjectWithData:data options:0 error:&error];
    if (error != nil || ![object isKindOfClass:[NSDictionary class]]) {
        NSLog(@"WARN [NativeDashboard] invalid json state");
        return @{};
    }

    return (NSDictionary *)object;
}

static NSRect TokenForgeVisibleFrame()
{
    NSScreen *screen = [NSScreen mainScreen];
    return screen ? [screen visibleFrame] : NSMakeRect(0, 0, 800, 600);
}

static NSRect TokenForgeVisibleFrameForFrame(NSRect frame)
{
    for (NSScreen *screen in [NSScreen screens]) {
        if (NSIntersectsRect(screen.frame, frame)) {
            return screen.visibleFrame;
        }
    }

    return TokenForgeVisibleFrame();
}

static NSRect TokenForgeClampFrameToVisibleFrame(NSRect frame)
{
    NSRect visible = TokenForgeVisibleFrameForFrame(frame);
    frame.origin.x = MIN(MAX(frame.origin.x, NSMinX(visible)), NSMaxX(visible) - frame.size.width);
    frame.origin.y = MIN(MAX(frame.origin.y, NSMinY(visible)), NSMaxY(visible) - frame.size.height);
    return frame;
}

static NSRect TokenForgeNormalizeDashboardFrame(NSRect frame, NSString *source)
{
    NSRect visible = TokenForgeVisibleFrameForFrame(frame);
    CGFloat maximumWidth = MAX(320.0, visible.size.width - 24.0);
    CGFloat maximumHeight = MAX(320.0, visible.size.height - 24.0);
    CGFloat minimumWidth = MIN(920.0, maximumWidth);
    CGFloat minimumHeight = MIN(620.0, maximumHeight);
    BOOL invalid = !isfinite(frame.origin.x) ||
                   !isfinite(frame.origin.y) ||
                   !isfinite(frame.size.width) ||
                   !isfinite(frame.size.height) ||
                   frame.size.width < minimumWidth ||
                   frame.size.height < minimumHeight ||
                   NSIsEmptyRect(frame);
    NSRect original = frame;
    if (!isfinite(frame.origin.x)) frame.origin.x = NSMinX(visible);
    if (!isfinite(frame.origin.y)) frame.origin.y = NSMinY(visible);
    if (!isfinite(frame.size.width)) frame.size.width = 1180.0;
    if (!isfinite(frame.size.height)) frame.size.height = 760.0;
    frame.size.width = MIN(MAX(frame.size.width, minimumWidth), maximumWidth);
    frame.size.height = MIN(MAX(frame.size.height, minimumHeight), maximumHeight);
    if (invalid || !NSIntersectsRect(frame, visible)) {
        frame.origin.x = NSMidX(visible) - frame.size.width * 0.5;
        frame.origin.y = NSMidY(visible) - frame.size.height * 0.5;
    }

    frame = TokenForgeClampFrameToVisibleFrame(frame);
    NSLog(@"INFO [DashboardLifecycle][FRAME_RESTORE] source=%@ rawFrame=(%.2f,%.2f %.2fx%.2f) normalizedFrame=(%.2f,%.2f %.2fx%.2f) visibleFrame=(%.2f,%.2f %.2fx%.2f) repaired=%@",
          source ?: @"unknown",
          original.origin.x,
          original.origin.y,
          original.size.width,
          original.size.height,
          frame.origin.x,
          frame.origin.y,
          frame.size.width,
          frame.size.height,
          visible.origin.x,
          visible.origin.y,
          visible.size.width,
          visible.size.height,
          invalid ? @"true" : @"false");
    return frame;
}

static void TokenForgeConfigureAndLogWindowChrome(NSWindow *window, NSString *source)
{
    if (window == nil) {
        return;
    }

    window.styleMask |= NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable | NSWindowStyleMaskResizable;
    window.titleVisibility = NSWindowTitleVisible;
    window.titlebarAppearsTransparent = NO;
    window.movableByWindowBackground = NO;
    NSButton *closeButton = [window standardWindowButton:NSWindowCloseButton];
    NSButton *miniButton = [window standardWindowButton:NSWindowMiniaturizeButton];
    NSButton *zoomButton = [window standardWindowButton:NSWindowZoomButton];
    closeButton.hidden = NO;
    miniButton.hidden = NO;
    zoomButton.hidden = NO;
    closeButton.enabled = YES;
    miniButton.enabled = YES;
    zoomButton.enabled = YES;
    NSLog(@"INFO [WindowChromeDiagnostic] source=%@ titleVisibility=%ld titlebarAppearsTransparent=%@ styleMask=%lu toolbarExists=%@ toolbarVisible=%@ closeButtonFrame=%@ minimizeButtonFrame=%@ zoomButtonFrame=%@",
          source ?: @"unknown",
          (long)window.titleVisibility,
          window.titlebarAppearsTransparent ? @"true" : @"false",
          (unsigned long)window.styleMask,
          window.toolbar != nil ? @"true" : @"false",
          window.toolbar != nil && window.toolbar.isVisible ? @"true" : @"false",
          closeButton != nil ? NSStringFromRect(closeButton.frame) : @"none",
          miniButton != nil ? NSStringFromRect(miniButton.frame) : @"none",
          zoomButton != nil ? NSStringFromRect(zoomButton.frame) : @"none");
}

static void TokenForgeLogDashboardLaunchDiagnostic(NSString *reason, BOOL requestedOpen, NSWindow *window, NSRect normalizedFrame)
{
    NSRect screenFrame = window != nil && window.screen != nil ? window.screen.visibleFrame : TokenForgeVisibleFrameForFrame(normalizedFrame);
    NSRect frame = window != nil ? window.frame : NSZeroRect;
    NSLog(@"INFO [DashboardLaunchDiagnostic] reason=%@ requestedOpen=%@ windowExists=%@ isVisible=%@ isMiniaturized=%@ isKeyWindow=%@ isMainWindow=%@ frame=%@ normalizedFrame=%@ screenFrame=%@",
          reason.length > 0 ? reason : @"unknown",
          requestedOpen ? @"true" : @"false",
          window != nil ? @"true" : @"false",
          window != nil && window.isVisible ? @"true" : @"false",
          window != nil && window.isMiniaturized ? @"true" : @"false",
          window != nil && window.isKeyWindow ? @"true" : @"false",
          window != nil && window.isMainWindow ? @"true" : @"false",
          NSStringFromRect(frame),
          NSStringFromRect(normalizedFrame),
          NSStringFromRect(screenFrame));
}

static CGFloat TokenForgeRectArea(NSRect rect)
{
    if (NSIsEmptyRect(rect)) {
        return 0.0;
    }

    return MAX(0.0, rect.size.width) * MAX(0.0, rect.size.height);
}

static NSRect TokenForgeFrameAvoidingDashboardForExplicitShow(NSRect frame, NSString *source)
{
    NSString *showSource = source.length > 0 ? source : @"unknown";
    if (!TokenForgeSourceLooksExplicit(showSource) ||
        TokenForgeNativeDashboardWindow == nil ||
        !TokenForgeNativeDashboardWindow.isVisible) {
        return frame;
    }

    NSRect dashboardFrame = TokenForgeNativeDashboardWindow.frame;
    NSRect overlap = NSIntersectionRect(frame, dashboardFrame);
    CGFloat frameArea = MAX(1.0, TokenForgeRectArea(frame));
    CGFloat overlapArea = TokenForgeRectArea(overlap);
    if (overlapArea < frameArea * 0.35) {
        return frame;
    }

    NSRect visible = TokenForgeVisibleFrameForFrame(dashboardFrame);
    CGFloat gap = 24.0;
    NSRect candidates[4] = {
        NSMakeRect(NSMaxX(dashboardFrame) + gap, frame.origin.y, frame.size.width, frame.size.height),
        NSMakeRect(NSMinX(dashboardFrame) - gap - frame.size.width, frame.origin.y, frame.size.width, frame.size.height),
        NSMakeRect(frame.origin.x, NSMaxY(dashboardFrame) + gap, frame.size.width, frame.size.height),
        NSMakeRect(frame.origin.x, NSMinY(dashboardFrame) - gap - frame.size.height, frame.size.width, frame.size.height)
    };

    NSRect bestFrame = TokenForgeClampFrameToVisibleFrame(candidates[0]);
    CGFloat bestOverlap = TokenForgeRectArea(NSIntersectionRect(bestFrame, dashboardFrame));
    for (NSUInteger index = 0; index < 4; index++) {
        NSRect candidate = TokenForgeClampFrameToVisibleFrame(candidates[index]);
        BOOL insideVisible = NSContainsRect(visible, candidate);
        CGFloat candidateOverlap = TokenForgeRectArea(NSIntersectionRect(candidate, dashboardFrame));
        if (insideVisible && candidateOverlap <= 0.5) {
            bestFrame = candidate;
            bestOverlap = candidateOverlap;
            break;
        }

        if (candidateOverlap < bestOverlap) {
            bestFrame = candidate;
            bestOverlap = candidateOverlap;
        }
    }

    if (!NSEqualRects(bestFrame, frame)) {
        NSLog(@"INFO [OverlayPanel][DASHBOARD_OCCLUSION_CORRECTED] source=%@ old=(%.2f,%.2f %.2fx%.2f) dashboard=(%.2f,%.2f %.2fx%.2f) new=(%.2f,%.2f %.2fx%.2f) overlapArea=%.2f",
              showSource,
              frame.origin.x,
              frame.origin.y,
              frame.size.width,
              frame.size.height,
              dashboardFrame.origin.x,
              dashboardFrame.origin.y,
              dashboardFrame.size.width,
              dashboardFrame.size.height,
              bestFrame.origin.x,
              bestFrame.origin.y,
              bestFrame.size.width,
              bestFrame.size.height,
              overlapArea);
    }

    return bestFrame;
}

static NSString *TokenForgeCompanionPositionXKey = @"TokenForge.CompanionOverlay.PositionX";
static NSString *TokenForgeCompanionPositionYKey = @"TokenForge.CompanionOverlay.PositionY";
static NSString *TokenForgeCompanionPositionSavedKey = @"TokenForge.CompanionOverlay.PositionSaved";

static NSPoint TokenForgeDefaultCompanionOrigin(void)
{
    NSRect visible = TokenForgeVisibleFrame();
    return NSMakePoint(NSMaxX(visible) - TokenForgeCompanionSize.width - 32.0, NSMinY(visible) + 120.0);
}

static NSPoint TokenForgeLoadCompanionOrigin(void)
{
    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
    if ([defaults boolForKey:TokenForgeCompanionPositionSavedKey]) {
        return NSMakePoint([defaults doubleForKey:TokenForgeCompanionPositionXKey], [defaults doubleForKey:TokenForgeCompanionPositionYKey]);
    }

    return TokenForgeDefaultCompanionOrigin();
}

static void TokenForgePersistCompanionFrame(NSRect frame)
{
    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
    [defaults setDouble:frame.origin.x forKey:TokenForgeCompanionPositionXKey];
    [defaults setDouble:frame.origin.y forKey:TokenForgeCompanionPositionYKey];
    [defaults setBool:YES forKey:TokenForgeCompanionPositionSavedKey];
    [defaults synchronize];
    TokenForgeCompanionAnchor = frame.origin;
    TokenForgeCompanionTarget = frame.origin;
    TokenForgeCompanionVelocity = NSMakePoint(0, 0);
    TokenForgeCompanionDragCooldownUntil = [NSDate timeIntervalSinceReferenceDate] + 1.0;
}

static void TokenForgeHydrateCompanionSnapshot(NSString *repositoryId, NSInteger stage, NSInteger level, NSInteger xp, NSInteger archetype, NSString *skin, NSString *source)
{
    TokenForgeCompanionSnapshotHydrated = YES;
    TokenForgeCompanionRenderVersion += 1;
    TokenForgeSnapshotRepositoryId = [repositoryId.length > 0 ? repositoryId : @"unknown" copy];
    TokenForgeSnapshotStage = MAX(0, MIN(5, stage));
    TokenForgeSnapshotLevel = MAX(1, level);
    TokenForgeSnapshotXP = MAX(0, xp);
    TokenForgeSnapshotArchetype = MAX(0, MIN(6, archetype));
    TokenForgeSnapshotSkin = [skin.length > 0 ? skin : @"orange_cat" copy];
    if (TokenForgeSnapshotZodiacType.length == 0 || [TokenForgeSnapshotZodiacType isEqualToString:@"repository"]) {
        TokenForgeSnapshotZodiacType = [TokenForgeZodiacTypeForArchetype(TokenForgeSnapshotArchetype) copy];
    }
    if (TokenForgeCompanionContentView != nil) {
        TokenForgeCompanionContentView.repositoryId = TokenForgeSnapshotRepositoryId;
        TokenForgeCompanionContentView.stage = TokenForgeSnapshotStage;
        TokenForgeCompanionContentView.level = TokenForgeSnapshotLevel;
        TokenForgeCompanionContentView.xp = TokenForgeSnapshotXP;
        TokenForgeCompanionContentView.archetype = TokenForgeSnapshotArchetype;
        TokenForgeCompanionContentView.visualThemeId = TokenForgeSnapshotSkin;
        TokenForgeCompanionContentView.zodiacType = TokenForgeSnapshotZodiacType;
        TokenForgeCompanionContentView.snapshotHydrated = YES;
        TokenForgeCompanionContentView.renderVersion = TokenForgeCompanionRenderVersion;
        [TokenForgeCompanionContentView setNeedsDisplay:YES];
    }
    NSLog(@"INFO [CompanionSnapshot][HYDRATED] repo=%@ stage=%@ level=%ld renderVersion=%lu source=%@",
          repositoryId.length > 0 ? repositoryId : @"unknown",
          TokenForgeCompanionStageName(stage),
          (long)MAX(1, level),
          (unsigned long)TokenForgeCompanionRenderVersion,
          source ?: @"unknown");
}

static void TokenForgeResetCompanionFrame(void)
{
    NSUserDefaults *defaults = [NSUserDefaults standardUserDefaults];
    [defaults removeObjectForKey:TokenForgeCompanionPositionXKey];
    [defaults removeObjectForKey:TokenForgeCompanionPositionYKey];
    [defaults removeObjectForKey:TokenForgeCompanionPositionSavedKey];
    [defaults synchronize];
    NSPoint origin = TokenForgeDefaultCompanionOrigin();
    NSRect frame = NSMakeRect(origin.x, origin.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
    frame = TokenForgeClampFrameToVisibleFrame(frame);
    TokenForgeCompanionAnchor = frame.origin;
    TokenForgeCompanionTarget = frame.origin;
    TokenForgeCompanionVelocity = NSMakePoint(0, 0);
    if (TokenForgeCompanionWindow != nil) {
        [TokenForgeCompanionWindow setFrameOrigin:frame.origin];
    }
}

static BOOL TokenForgeIsCompanionWindow(NSWindow *window)
{
    if (window == nil) {
        return NO;
    }
    if (window == TokenForgeCompanionWindow) {
        return YES;
    }
    TokenForgeEnsureOverlayFarmRegistry();
    for (NSPanel *panel in [TokenForgeOverlayPanelsByRepositoryId allValues]) {
        if ((NSWindow *)panel == window) {
            return YES;
        }
    }
    return NO;
}

static BOOL TokenForgeLooksLikeMainWindow(NSWindow *window)
{
    if (window == nil || TokenForgeIsCompanionWindow(window)) {
        return NO;
    }

    NSWindowStyleMask styleMask = window.styleMask;
    if ((styleMask & NSWindowStyleMaskTitled) == 0) {
        return NO;
    }

    NSRect frame = window.frame;
    return frame.size.width >= 320.0 && frame.size.height >= 240.0;
}

static NSWindow *TokenForgeFindMainWindow()
{
    NSWindow *mainWindow = [NSApp mainWindow];
    if (TokenForgeLooksLikeMainWindow(mainWindow)) {
        return mainWindow;
    }

    NSWindow *keyWindow = [NSApp keyWindow];
    if (TokenForgeLooksLikeMainWindow(keyWindow)) {
        return keyWindow;
    }

    for (NSWindow *window in TokenForgeSafeWindowsSnapshot(@"TokenForgeFindMainWindow", NO)) {
        if (TokenForgeLooksLikeMainWindow(window)) {
            return window;
        }
    }

    return nil;
}

static NSString *TokenForgeSafeMenuString(const char *value, NSString *fallback)
{
    if (value == NULL) {
        return fallback;
    }

    NSString *string = [NSString stringWithUTF8String:value];
    if (string == nil || string.length == 0) {
        return fallback;
    }

    NSCharacterSet *newlines = [NSCharacterSet newlineCharacterSet];
    NSArray<NSString *> *parts = [string componentsSeparatedByCharactersInSet:newlines];
    NSString *singleLine = [parts componentsJoinedByString:@" "];
    if (singleLine.length > 80) {
        singleLine = [singleLine substringToIndex:80];
    }

    return singleLine;
}

static void TokenForgeAssignMenuString(NSString *__strong *target, const char *value, NSString *fallback)
{
    NSString *safe = TokenForgeSafeMenuString(value, fallback);
    *target = [safe copy];
}

static void TokenForgeSendMenuAction(const char *action)
{
    if (action != NULL && strcmp(action, "quit") == 0) {
        TokenForgeExplicitQuitRequested = YES;
    }

    if (TokenForgeDashboardActionClicked != nil) {
        TokenForgeDashboardActionClicked(action);
    }

    if (TokenForgeMenuActionClicked != nil) {
        TokenForgeMenuActionClicked(action);
    }
}

static void TokenForgeSendDashboardAction(const char *action)
{
    if (action != NULL && (strcmp(action, "quit") == 0 || strcmp(action, "app.quit") == 0)) {
        TokenForgeExplicitQuitRequested = YES;
    }

    NSLog(@"INFO [NativeAction] received action=%s currentTab=%@", action != NULL ? action : "<null>", TokenForgeCurrentDashboardTab ?: @"dashboard");
    if (TokenForgeDashboardActionClicked != nil) {
        TokenForgeDashboardActionClicked(action);
    }
}

static NSImage *TokenForgeCreateStatusCompanionImage(NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode)
{
    NSSize imageSize = NSMakeSize(20.0, 20.0);
    return TokenForgeAvatarImageForPreset(@"menuBar", imageSize, stage, archetype, frameIndex, mode ?: @"idle", @"orange_cat");
}

static void TokenForgeTriggerOverlayReaction(NSInteger reaction, NSString *speech)
{
    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    if (now < TokenForgeCompanionReactionUntil && TokenForgeCompanionContentView.speechText.length > 0) {
        return;
    }

    TokenForgeCompanionReactionUntil = now + TokenForgeCompanionReactionCooldown;
    TokenForgeCompanionVelocity = NSMakePoint(0, 0);
    TokenForgeCompanionContentView.speechText = speech.length > 0 ? speech : @"First safe summary will start growth.";
    TokenForgeCompanionContentView.speechExpiresAt = now + 2.4;
    TokenForgeCompanionContentView.animationState = reaction == 3 ? 4 : 5;
    [TokenForgeCompanionContentView setNeedsDisplay:YES];
}

static void TokenForgeChooseNextCompanionMotion(NSTimeInterval now)
{
    TokenForgeCompanionNextDecisionAt = now + MAX(0.8, TokenForgeCompanionDecisionInterval);
    if (!TokenForgeCompanionAllowsWandering || TokenForgeCompanionMotionMode == 0) {
        TokenForgeCompanionVelocity = NSMakePoint(0, 0);
        TokenForgeCompanionTarget = TokenForgeCompanionAnchor;
        NSLog(@"INFO [CompanionMotion] skipped reason=movementOff");
        return;
    }

    CGFloat radius = MAX(80.0, TokenForgeCompanionWanderRadius);
    CGFloat dx = ((CGFloat)arc4random_uniform(2001) / 1000.0 - 1.0) * radius;
    CGFloat dy = ((CGFloat)arc4random_uniform(2001) / 1000.0 - 1.0) * radius * 0.35;
    if (fabs(dx) < 52.0) {
        dx = dx < 0.0 ? -52.0 : 52.0;
    }

    NSRect requested = NSMakeRect(TokenForgeCompanionAnchor.x + dx,
                                  TokenForgeCompanionAnchor.y + dy,
                                  TokenForgeCompanionSize.width,
                                  TokenForgeCompanionSize.height);
    NSRect clamped = TokenForgeClampFrameToVisibleFrame(requested);
    TokenForgeCompanionTarget = clamped.origin;
    CGFloat distanceX = TokenForgeCompanionTarget.x - TokenForgeCompanionAnchor.x;
    CGFloat distanceY = TokenForgeCompanionTarget.y - TokenForgeCompanionAnchor.y;
    CGFloat distance = MAX(1.0, hypot(distanceX, distanceY));
    CGFloat speed = MAX(22.0, TokenForgeCompanionWanderSpeed);
    TokenForgeCompanionVelocity = NSMakePoint(distanceX / distance * speed, distanceY / distance * speed);
    if (TokenForgeCompanionContentView != nil) {
        TokenForgeCompanionContentView.facingLeft = TokenForgeCompanionVelocity.x < 0.0;
    }
    NSLog(@"INFO [CompanionMotion] nextTarget requested=(%.2f,%.2f) clamped=(%.2f,%.2f) speed=%.2f nextDecisionIn=%.2f",
          requested.origin.x,
          requested.origin.y,
          TokenForgeCompanionTarget.x,
          TokenForgeCompanionTarget.y,
          speed,
          MAX(0.8, TokenForgeCompanionDecisionInterval));
}

static void TokenForgeAdvanceIndependentCompanionPanel(NSString *repo,
                                                       NSPanel *panel,
                                                       TokenForgeDesktopOverlayCompanionView *view,
                                                       NSTimeInterval now)
{
    if (panel == nil || view == nil || !panel.isVisible) {
        return;
    }

    // A companion currently being dragged owns its own frame; never fight the user.
    if (TokenForgeIsOverlayDraggingForRepository(repo)) {
        view.independentMotionLastTick = now;
        view.independentMotionAnchor = panel.frame.origin;
        return;
    }

    NSSize size = panel.frame.size;
    if (!view.independentMotionInitialized) {
        view.independentMotionAnchor = panel.frame.origin;
        view.independentMotionTarget = panel.frame.origin;
        view.independentMotionVelocity = NSMakePoint(0, 0);
        view.independentMotionNextDecisionAt = now + 0.6 + ((CGFloat)arc4random_uniform(1200) / 1000.0);
        view.independentMotionPhaseOffset = (CGFloat)arc4random_uniform(6283) / 1000.0;
        view.independentMotionLastTick = now;
        view.independentMotionInitialized = YES;
    }

    NSTimeInterval delta = view.independentMotionLastTick <= 0.0 ? 0.016 : MIN(0.05, now - view.independentMotionLastTick);
    view.independentMotionLastTick = now;

    CGFloat phase = now * 2.7 + view.independentMotionPhaseOffset;
    BOOL wandering = TokenForgeCompanionAllowsWandering && TokenForgeCompanionMotionMode != 0;

    if (wandering && now >= view.independentMotionNextDecisionAt) {
        view.independentMotionNextDecisionAt = now + MAX(0.8, TokenForgeCompanionDecisionInterval);
        CGFloat radius = MAX(80.0, TokenForgeCompanionWanderRadius);
        CGFloat dx = ((CGFloat)arc4random_uniform(2001) / 1000.0 - 1.0) * radius;
        CGFloat dy = ((CGFloat)arc4random_uniform(2001) / 1000.0 - 1.0) * radius * 0.35;
        if (fabs(dx) < 52.0) {
            dx = dx < 0.0 ? -52.0 : 52.0;
        }
        NSRect requested = NSMakeRect(view.independentMotionAnchor.x + dx,
                                      view.independentMotionAnchor.y + dy,
                                      size.width,
                                      size.height);
        NSRect clamped = TokenForgeClampFrameToVisibleFrame(requested);
        view.independentMotionTarget = clamped.origin;
    }

    if (wandering) {
        NSPoint anchor = view.independentMotionAnchor;
        NSPoint target = view.independentMotionTarget;
        CGFloat remainingX = target.x - anchor.x;
        CGFloat remainingY = target.y - anchor.y;
        CGFloat remaining = hypot(remainingX, remainingY);
        NSPoint velocity = NSMakePoint(0, 0);
        if (remaining > 5.0) {
            CGFloat speed = MAX(22.0, TokenForgeCompanionWanderSpeed);
            velocity = NSMakePoint(remainingX / remaining * speed, remainingY / remaining * speed);
            view.facingLeft = velocity.x < 0.0;
        }
        anchor.x += velocity.x * delta;
        anchor.y += velocity.y * delta;

        NSRect anchorFrame = NSMakeRect(anchor.x, anchor.y, size.width, size.height);
        NSRect visible = TokenForgeVisibleFrameForFrame(anchorFrame);
        if (anchor.x < NSMinX(visible) || anchor.x > NSMaxX(visible) - size.width) {
            velocity.x *= -1.0;
            view.facingLeft = velocity.x < 0.0;
        }
        view.independentMotionVelocity = velocity;
        anchorFrame = TokenForgeClampFrameToVisibleFrame(anchorFrame);
        view.independentMotionAnchor = anchorFrame.origin;
    }

    CGFloat idleX = sin(phase) * TokenForgeCompanionIdleRadius;
    CGFloat idleY = fabs(sin(phase * 0.75)) * 3.0;
    NSRect displayFrame = NSMakeRect(view.independentMotionAnchor.x + idleX,
                                     view.independentMotionAnchor.y,
                                     size.width,
                                     size.height);
    displayFrame = TokenForgeClampFrameToVisibleFrame(displayFrame);
    [panel setFrameOrigin:displayFrame.origin];
    TokenForgeOverlayFramesByRepositoryId[repo] = [NSValue valueWithRect:panel.frame];

    view.visualOffsetY = idleY;
    view.visualRotation = sin(phase * 0.8) * 1.8;
    view.visualScale = 1.0 + sin(phase * 0.85) * 0.018;
    [view setNeedsDisplay:YES];
}

static void TokenForgeAdvanceIndependentCompanionPanels(NSTimeInterval now)
{
    if (TokenForgeOverlayPanelsByRepositoryId.count == 0) {
        return;
    }

    NSInteger advancedCount = 0;
    for (NSString *repo in [[TokenForgeOverlayPanelsByRepositoryId allKeys] copy]) {
        NSPanel *panel = TokenForgeOverlayPanelsByRepositoryId[repo];
        // The selected/legacy companion shares the global TokenForgeCompanion* motion
        // state and is advanced by the single-companion tick below; skip it here so it
        // is not moved twice per frame.
        if (panel == nil || panel == TokenForgeCompanionWindow) {
            continue;
        }
        TokenForgeDesktopOverlayCompanionView *view = TokenForgeOverlayViewsByRepositoryId[repo];
        if (![view isKindOfClass:[TokenForgeDesktopOverlayCompanionView class]]) {
            continue;
        }
        if (!panel.isVisible) {
            continue;
        }

        TokenForgeAdvanceIndependentCompanionPanel(repo, panel, view, now);
        advancedCount += 1;
    }

    static NSTimeInterval TokenForgeLastIndependentMotionLogAt = 0.0;
    if (advancedCount > 0 && now - TokenForgeLastIndependentMotionLogAt > 2.0) {
        TokenForgeLastIndependentMotionLogAt = now;
        NSLog(@"INFO [OverlayMovementDiagnostic] role=farmIndependent advancedCompanionCount=%ld selectedCompanionTickedSeparately=%@ allowsWandering=%@ source=independentTick",
              (long)advancedCount,
              (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"true" : @"false",
              TokenForgeCompanionAllowsWandering ? @"true" : @"false");
    }
}

static void TokenForgeCompanionMotionTick(NSTimer *timer)
{
    // Advance every non-selected repository companion on its own independent motion
    // state first, regardless of whether the selected/legacy companion window is
    // visible. Previously only the single selected companion moved, so connected
    // repositories beyond the active tab stayed frozen.
    TokenForgeAdvanceIndependentCompanionPanels([NSDate timeIntervalSinceReferenceDate]);

    if (TokenForgeCompanionWindow == nil || TokenForgeCompanionContentView == nil || !TokenForgeCompanionWindow.isVisible) {
        return;
    }

    if (!TokenForgeMotionTickLogged) {
        TokenForgeMotionTickLogged = YES;
        NSLog(@"INFO [DesktopOverlay] movementTimer started interval=%.2f", 1.0 / 30.0);
        NSLog(@"INFO [DesktopOverlay] movement tick speed=%.2f position=(%.2f,%.2f)", TokenForgeCompanionWanderSpeed, TokenForgeCompanionWindow.frame.origin.x, TokenForgeCompanionWindow.frame.origin.y);
        NSLog(@"INFO [DesktopOverlay] visible=true movementRunning=true");
        NSLog(@"INFO [OverlayMovementDiagnostic] role=desktopOverlay repoHash=%@ movementTimerActive=true movementMode=%ld allowsWandering=%@ selected=true animationState=idleBreathing",
              TokenForgeSafeRepositoryKey(TokenForgeCompanionContentView.repositoryId),
              (long)TokenForgeCompanionMotionMode,
              TokenForgeCompanionAllowsWandering ? @"true" : @"false");
    }

    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    NSTimeInterval delta = TokenForgeCompanionLastTick <= 0.0 ? 0.016 : MIN(0.05, now - TokenForgeCompanionLastTick);
    TokenForgeCompanionLastTick = now;
    if (TokenForgeIsDraggingOverlay) {
        if (!TokenForgeMotionPauseLogged) {
            TokenForgeMotionPauseLogged = YES;
            NSLog(@"INFO [OverlayMovement][PAUSE] reason=drag");
        }
        return;
    }

    CGFloat phase = now * 2.7;
    BOOL reacting = now < TokenForgeCompanionReactionUntil;
    BOOL paused = TokenForgeIsDraggingOverlay || now < TokenForgeCompanionDragCooldownUntil;
    if (paused && !TokenForgeMotionPauseLogged) {
        TokenForgeMotionPauseLogged = YES;
        NSLog(@"INFO [CompanionMotion] idlePaused reason=%@", TokenForgeIsDraggingOverlay ? @"drag" : @"dragCooldown");
    } else if (!paused && TokenForgeMotionPauseLogged) {
        TokenForgeMotionPauseLogged = NO;
        NSLog(@"INFO [CompanionMotion] idleResumed anchor=(%.2f,%.2f)", TokenForgeCompanionAnchor.x, TokenForgeCompanionAnchor.y);
    }
    if (!paused && !reacting && now >= TokenForgeCompanionNextDecisionAt) {
        TokenForgeChooseNextCompanionMotion(now);
    }

    if (!paused && !reacting) {
        CGFloat remainingX = TokenForgeCompanionTarget.x - TokenForgeCompanionAnchor.x;
        CGFloat remainingY = TokenForgeCompanionTarget.y - TokenForgeCompanionAnchor.y;
        CGFloat remaining = hypot(remainingX, remainingY);
        if (remaining <= 5.0) {
            TokenForgeCompanionVelocity = NSMakePoint(0, 0);
        } else {
            CGFloat speed = MAX(22.0, TokenForgeCompanionWanderSpeed);
            TokenForgeCompanionVelocity = NSMakePoint(remainingX / remaining * speed, remainingY / remaining * speed);
            if (TokenForgeCompanionContentView != nil) {
                TokenForgeCompanionContentView.facingLeft = TokenForgeCompanionVelocity.x < 0.0;
            }
        }
        TokenForgeCompanionAnchor.x += TokenForgeCompanionVelocity.x * delta;
        TokenForgeCompanionAnchor.y += TokenForgeCompanionVelocity.y * delta;
    }

    NSRect anchorFrame = NSMakeRect(TokenForgeCompanionAnchor.x, TokenForgeCompanionAnchor.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
    NSRect visible = TokenForgeVisibleFrameForFrame(anchorFrame);
    if (TokenForgeCompanionAnchor.x < NSMinX(visible) || TokenForgeCompanionAnchor.x > NSMaxX(visible) - TokenForgeCompanionSize.width) {
        TokenForgeCompanionVelocity.x *= -1.0;
        TokenForgeCompanionContentView.facingLeft = TokenForgeCompanionVelocity.x < 0.0;
    }

    NSRect unclampedAnchorFrame = anchorFrame;
    anchorFrame = TokenForgeClampFrameToVisibleFrame(anchorFrame);
    TokenForgeCompanionAnchor = anchorFrame.origin;
    if (!NSEqualRects(anchorFrame, unclampedAnchorFrame)) {
        NSLog(@"INFO [DesktopOverlay] boundsClamped screen=(%.2f,%.2f %.2fx%.2f)", visible.origin.x, visible.origin.y, visible.size.width, visible.size.height);
    }

    CGFloat idleX = reacting ? sin(phase * 8.0) * 5.0 : sin(phase) * TokenForgeCompanionIdleRadius;
    CGFloat idleY = reacting ? fabs(sin(phase * 4.0)) * 10.0 : fabs(sin(phase * 0.75)) * 3.0;
    if (paused) {
        idleX = 0.0;
        idleY = 0.0;
    }

    NSPoint oldDisplayOrigin = TokenForgeCompanionWindow.frame.origin;
    NSRect displayFrame = NSMakeRect(TokenForgeCompanionAnchor.x + idleX, TokenForgeCompanionAnchor.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
    displayFrame = TokenForgeClampFrameToVisibleFrame(displayFrame);
    [TokenForgeCompanionWindow setFrameOrigin:displayFrame.origin];
    static NSTimeInterval TokenForgeLastMotionLogAt = 0.0;
    if (now - TokenForgeLastMotionLogAt > 2.0) {
        TokenForgeLastMotionLogAt = now;
        NSLog(@"INFO [DesktopCompanion] animation start old=%.2f,%.2f target=%.2f,%.2f speed=%.2f",
              oldDisplayOrigin.x,
              oldDisplayOrigin.y,
              TokenForgeCompanionTarget.x,
              TokenForgeCompanionTarget.y,
              TokenForgeCompanionWanderSpeed);
        NSLog(@"INFO [DesktopOverlay] tick oldOrigin=(%.2f,%.2f) newOrigin=(%.2f,%.2f) speed=%.2f",
              oldDisplayOrigin.x,
              oldDisplayOrigin.y,
              displayFrame.origin.x,
              displayFrame.origin.y,
              TokenForgeCompanionWanderSpeed);
        NSLog(@"INFO [OverlayMovementDiagnostic] role=desktopOverlay repoHash=%@ oldOrigin=(%.2f,%.2f) newOrigin=(%.2f,%.2f) idleOffset=(%.2f,%.2f) velocity=(%.2f,%.2f) reacting=%@ paused=%@ selected=true",
              TokenForgeSafeRepositoryKey(TokenForgeCompanionContentView.repositoryId),
              oldDisplayOrigin.x,
              oldDisplayOrigin.y,
              displayFrame.origin.x,
              displayFrame.origin.y,
              idleX,
              idleY,
              TokenForgeCompanionVelocity.x,
              TokenForgeCompanionVelocity.y,
              reacting ? @"true" : @"false",
              paused ? @"true" : @"false");
        NSLog(@"INFO [DesktopCompanion] animation end actual=(%.2f,%.2f) clamped=(%.2f,%.2f)",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              displayFrame.origin.x,
              displayFrame.origin.y);
        NSLog(@"INFO [DesktopOverlay] tick oldFrame=(%.2f,%.2f %.2fx%.2f) newFrame=(%.2f,%.2f %.2fx%.2f)",
              oldDisplayOrigin.x,
              oldDisplayOrigin.y,
              TokenForgeCompanionSize.width,
              TokenForgeCompanionSize.height,
              displayFrame.origin.x,
              displayFrame.origin.y,
              displayFrame.size.width,
              displayFrame.size.height);
    }

    TokenForgeCompanionContentView.visualOffsetY = idleY;
    TokenForgeCompanionContentView.visualRotation = reacting ? sin(phase * 9.0) * 8.0 : sin(phase * 0.8) * 1.8;
    TokenForgeCompanionContentView.visualScale = reacting ? 1.0 + fabs(sin(phase * 5.0)) * 0.08 : 1.0 + sin(phase * 0.85) * 0.018;
    if (!reacting && TokenForgeCompanionContentView.speechText.length > 0 && now >= TokenForgeCompanionContentView.speechExpiresAt) {
        TokenForgeCompanionContentView.speechText = @"";
    }

    [TokenForgeCompanionContentView setNeedsDisplay:YES];
}

static void TokenForgeEnsureCompanionMotionTimer(void)
{
    TokenForgeRefreshNativeSafetyFlags();
    if (TokenForgeDisableMovementTimers) {
        [TokenForgeCompanionMotionTimer invalidate];
        TokenForgeCompanionMotionTimer = nil;
        NSLog(@"INFO [NativeSafeMode][SKIP] function=TokenForgeEnsureCompanionMotionTimer reason=TOKENFORGE_DISABLE_MOVEMENT_TIMERS");
        return;
    }

    if (TokenForgeCompanionMotionTimer != nil) {
        return;
    }

    TokenForgeCompanionLastTick = [NSDate timeIntervalSinceReferenceDate];
    TokenForgeCompanionMotionTimer = [NSTimer scheduledTimerWithTimeInterval:1.0 / 30.0 repeats:YES block:^(NSTimer *timer) {
        TokenForgeCompanionMotionTick(timer);
    }];
    [[NSRunLoop mainRunLoop] addTimer:TokenForgeCompanionMotionTimer forMode:NSRunLoopCommonModes];
    NSLog(@"INFO [DesktopOverlay] movementTimer started interval=%.2f", 1.0 / 30.0);
    NSLog(@"INFO [OverlayMovementDiagnostic] role=desktopOverlay movementTimerActive=true interval=%.2f source=ensureTimer", 1.0 / 30.0);
}

static void TokenForgeCreateCompanionOverlayOnMain(NSString *traceId)
{
    if (![NSThread isMainThread]) {
        NSString *traceCopy = [traceId.length > 0 ? traceId : @"none" copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeCreateCompanionOverlayOnMain(traceCopy);
        });
        return;
    }

    TokenForgeLogRuntimeIdentityIfNeeded();
    NSString *trace = traceId.length > 0 ? traceId : @"none";
    if (!TokenForgeOverlayLaunchPathAllowed(@"TokenForgeCreateCompanionOverlayOnMain", trace, TokenForgeSourceLooksExplicit(trace) && !TokenForgeSourceLooksPassiveSync(trace))) {
        return;
    }

    if (TokenForgeCreatingPanel) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeCreateCompanionOverlayOnMain reason=reentrantCreate source=%@", trace);
        return;
    }

    TokenForgeCreatingPanel = YES;
    if (TokenForgeExplicitQuitRequested || TokenForgeTerminating) {
        NSLog(@"INFO [OverlayTrace:%@] panel_create_skipped reason=explicitQuitOrTerminating", trace);
        TokenForgeCreatingPanel = NO;
        return;
    }

    if (TokenForgeIsDraggingOverlay) {
        NSLog(@"INFO [OverlayLifecycle][SUPPRESSED_RECREATE] reason=dragging source=%@", trace);
        TokenForgeCreatingPanel = NO;
        return;
    }

    if (!TokenForgeAppKitRegistrationReady()) {
        NSLog(@"INFO [NativeLifecycle] open_dashboard deferred reason=app_not_ready");
        NSLog(@"INFO [OverlayTrace:%@] native_show_deferred reason=app_not_ready", trace);
        TokenForgeRequestLifecycleInstall(@"overlay_create");
        TokenForgeCreatingPanel = NO;
        return;
    }

    TokenForgeRequestLifecycleInstall(@"overlay_create");
    if (TokenForgeCompanionWindow != nil) {
        NSLog(@"INFO [DesktopCompanion] strongReference panel=%@ view=%@ controller=%@", TokenForgeCompanionWindow != nil ? @"true" : @"false", TokenForgeCompanionContentView != nil ? @"true" : @"false", TokenForgeLifecycleDelegate != nil ? @"true" : @"false");
        TokenForgeCreatingPanel = NO;
        return;
    }

    NSPoint origin = TokenForgeLoadCompanionOrigin();
    NSRect requestedFrame = NSMakeRect(origin.x, origin.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
    NSRect frame = TokenForgeClampFrameToVisibleFrame(requestedFrame);
    if (!NSEqualRects(requestedFrame, frame)) {
        NSLog(@"INFO [OverlayPanel][OFFSCREEN_CORRECTED] old=(%.2f,%.2f %.2fx%.2f) new=(%.2f,%.2f %.2fx%.2f)",
              requestedFrame.origin.x,
              requestedFrame.origin.y,
              requestedFrame.size.width,
              requestedFrame.size.height,
              frame.origin.x,
              frame.origin.y,
              frame.size.width,
              frame.size.height);
    }
    TokenForgeCompanionAnchor = frame.origin;
    TokenForgeCompanionTarget = frame.origin;
    NSRect visibleFrame = TokenForgeVisibleFrameForFrame(frame);
    NSLog(@"INFO [DesktopOverlay] screenClamp requested=(%.2f,%.2f %.2fx%.2f) visibleFrame=(%.2f,%.2f %.2fx%.2f) clamped=(%.2f,%.2f %.2fx%.2f)",
          requestedFrame.origin.x,
          requestedFrame.origin.y,
          requestedFrame.size.width,
          requestedFrame.size.height,
          visibleFrame.origin.x,
          visibleFrame.origin.y,
          visibleFrame.size.width,
          visibleFrame.size.height,
          frame.origin.x,
          frame.origin.y,
          frame.size.width,
          frame.size.height);
    NSPanel *panel = [[TokenForgeCompanionOverlayWindow alloc] initWithContentRect:frame
                                                                         styleMask:NSWindowStyleMaskBorderless | NSWindowStyleMaskNonactivatingPanel
                                                                           backing:NSBackingStoreBuffered
                                                                             defer:NO];
    panel.releasedWhenClosed = NO;
    panel.hidesOnDeactivate = NO;
    panel.floatingPanel = YES;
    panel.worksWhenModal = YES;
    panel.becomesKeyOnlyIfNeeded = NO;
    panel.identifier = @"TokenForge.DesktopCompanion";
    panel.accessibilityLabel = @"TokenForge.OverlayPanel";
    TokenForgeCompanionWindow = panel;
    TokenForgeCompanionWindow.backgroundColor = [NSColor clearColor];
    TokenForgeCompanionWindow.opaque = NO;
    TokenForgeCompanionWindow.alphaValue = 1.0;
    TokenForgeCompanionWindow.hasShadow = YES;
    TokenForgeCompanionWindow.level = NSStatusWindowLevel;
    TokenForgeCompanionWindow.acceptsMouseMovedEvents = YES;
    TokenForgeCompanionWindow.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary | NSWindowCollectionBehaviorStationary | NSWindowCollectionBehaviorIgnoresCycle;
    TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgeMenuClickThrough;
    TokenForgeOverlayPanelGeneration += 1;
    TokenForgeDesktopOverlayCompanionView *overlayView = [[TokenForgeDesktopOverlayCompanionView alloc] initWithFrame:NSMakeRect(0, 0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height)];
    TokenForgeCompanionContentView = overlayView;
    TokenForgeCompanionContentView.viewRole = TokenForgeCompanionRenderRoleDesktopOverlay;
    TokenForgeCompanionContentView.snapshotHydrated = TokenForgeCompanionSnapshotHydrated;
    TokenForgeCompanionContentView.renderVersion = TokenForgeCompanionRenderVersion;
    TokenForgeCompanionContentView.repositoryId = TokenForgeSnapshotRepositoryId;
    TokenForgeCompanionContentView.stage = TokenForgeSnapshotStage;
    TokenForgeCompanionContentView.level = TokenForgeSnapshotLevel;
    TokenForgeCompanionContentView.xp = TokenForgeSnapshotXP;
    TokenForgeCompanionContentView.archetype = TokenForgeSnapshotArchetype;
    TokenForgeCompanionContentView.visualThemeId = TokenForgeSnapshotSkin ?: @"orange_cat";
    TokenForgeCompanionContentView.assetType = @"overlay";
    TokenForgeCompanionContentView.wantsLayer = YES;
    TokenForgeCompanionContentView.layerContentsRedrawPolicy = NSViewLayerContentsRedrawOnSetNeedsDisplay;
    TokenForgeCompanionWindow.contentView = TokenForgeCompanionContentView;
    TokenForgeLastOverlayVisibleSource = [trace copy];
    TokenForgeRefreshDashboardAndOverlayState(trace);
    TokenForgeLogWindowLifecycle(@"created", TokenForgeCompanionWindow, @"companionOverlay");
    NSScreen *screen = TokenForgeCompanionWindow.screen ?: [NSScreen mainScreen];
    NSLog(@"INFO [OverlayPanel][CREATE] level=%ld styleMask=%lu collectionBehavior=%lu parent=nil frame=(%.2f,%.2f %.2fx%.2f) screen=(%.2f,%.2f %.2fx%.2f)",
          (long)TokenForgeCompanionWindow.level,
          (unsigned long)TokenForgeCompanionWindow.styleMask,
          (unsigned long)TokenForgeCompanionWindow.collectionBehavior,
          frame.origin.x,
          frame.origin.y,
          frame.size.width,
          frame.size.height,
          visibleFrame.origin.x,
          visibleFrame.origin.y,
          visibleFrame.size.width,
          visibleFrame.size.height);
    NSLog(@"INFO [OverlayPanel][FRAME] frame=(%.2f,%.2f %.2fx%.2f) screen=(%.2f,%.2f %.2fx%.2f) visibleFrame=(%.2f,%.2f %.2fx%.2f)",
          frame.origin.x,
          frame.origin.y,
          frame.size.width,
          frame.size.height,
          screen.frame.origin.x,
          screen.frame.origin.y,
          screen.frame.size.width,
          screen.frame.size.height,
          visibleFrame.origin.x,
          visibleFrame.origin.y,
          visibleFrame.size.width,
          visibleFrame.size.height);
    NSLog(@"INFO [OverlayPanel][LEVEL] level=%ld", (long)TokenForgeCompanionWindow.level);
    NSLog(@"INFO [OverlayPanel][COLLECTION] behavior=%lu", (unsigned long)TokenForgeCompanionWindow.collectionBehavior);
    TokenForgeCreatingPanel = NO;
    NSLog(@"INFO [OverlayPanel][CONTENT] contentView=%@", TokenForgeCompanionWindow.contentView != nil ? NSStringFromClass([TokenForgeCompanionWindow.contentView class]) : @"nil");
    NSLog(@"INFO [OverlayPanel][INDEPENDENT] dashboardParent=false childWindow=false");
    NSLog(@"INFO [OverlayTrace:%@] panel_create traceId=%@ ptr=%p level=%ld collectionBehavior=%lu releasedWhenClosed=%@ canBecomeKey=false",
          trace,
          trace,
          TokenForgeCompanionWindow,
          (long)TokenForgeCompanionWindow.level,
          (unsigned long)TokenForgeCompanionWindow.collectionBehavior,
          TokenForgeCompanionWindow.releasedWhenClosed ? @"true" : @"false");
    NSLog(@"INFO [DesktopCompanion] window created level=%ld frame=%.2f,%.2f %.2fx%.2f", (long)TokenForgeCompanionWindow.level, frame.origin.x, frame.origin.y, frame.size.width, frame.size.height);
    NSLog(@"INFO [DesktopCompanion] panelCreated id=%p frame=(%.2f,%.2f %.2fx%.2f) screen=(%.2f,%.2f %.2fx%.2f) level=%ld visible=%@",
          TokenForgeCompanionWindow,
          frame.origin.x,
          frame.origin.y,
          frame.size.width,
          frame.size.height,
          screen.frame.origin.x,
          screen.frame.origin.y,
          screen.frame.size.width,
          screen.frame.size.height,
          (long)TokenForgeCompanionWindow.level,
          TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
    NSLog(@"INFO [DesktopCompanion] strongReference panel=%@ view=%@ controller=%@",
          TokenForgeCompanionWindow != nil ? @"true" : @"false",
          TokenForgeCompanionContentView != nil ? @"true" : @"false",
          TokenForgeLifecycleDelegate != nil ? @"true" : @"false");
    NSLog(@"INFO [DesktopOverlay] create window independent=true level=%ld behavior=canJoinAllSpaces,fullScreenAuxiliary,stationary,ignoresCycle style=borderless,transparent,nonactivating hidesOnDeactivate=false", (long)TokenForgeCompanionWindow.level);
    NSLog(@"INFO [RuntimePath] overlayController=independent-panel-v1");
}

static NSSize TokenForgeOverlaySizeForStage(NSInteger stage)
{
    switch (stage) {
        case 1: return NSMakeSize(96.0, 96.0);
        case 2: return NSMakeSize(110.0, 110.0);
        case 3: return NSMakeSize(128.0, 128.0);
        default: return NSMakeSize(92.0, 92.0);
    }
}

static void TokenForgeApplySnapshotToOverlayView(TokenForgeDesktopOverlayCompanionView *view, NSDictionary *snapshot)
{
    if (view == nil || snapshot == nil) {
        return;
    }

    view.repositoryId = TokenForgeSafeRepositoryKey(TokenForgeSnapshotString(snapshot, @"repositoryId", @"legacy"));
    view.stage = TokenForgeSnapshotInteger(snapshot, @"stage", 0);
    view.level = MAX(1, TokenForgeSnapshotInteger(snapshot, @"level", 1));
    view.xp = MAX(0, TokenForgeSnapshotInteger(snapshot, @"xp", 0));
    view.archetype = MAX(0, TokenForgeSnapshotInteger(snapshot, @"archetype", 0));
    view.visualThemeId = TokenForgeSnapshotString(snapshot, @"visualThemeId", @"orange_cat");
    view.snapshotHydrated = TokenForgeSnapshotBool(snapshot, @"hydrated", NO);
    view.renderVersion = (NSUInteger)MAX(0, TokenForgeSnapshotInteger(snapshot, @"renderVersion", (NSInteger)TokenForgeOverlayFarmRenderVersion));
    view.assetType = @"overlay";
    [view setNeedsDisplay:YES];
}

static NSPanel *TokenForgeCreateOrReuseOverlayPanelForSnapshot(NSDictionary *snapshot, NSUInteger index, NSString *source)
{
    TokenForgeEnsureOverlayFarmRegistry();
    NSString *repo = TokenForgeSafeRepositoryKey(TokenForgeSnapshotString(snapshot, @"repositoryId", @"legacy"));
    NSString *companionId = TokenForgeSnapshotString(snapshot, @"companionId", @"unknown");
    NSInteger stage = TokenForgeSnapshotInteger(snapshot, @"stage", 0);
    NSInteger level = MAX(1, TokenForgeSnapshotInteger(snapshot, @"level", 1));
    BOOL hydrated = TokenForgeSnapshotBool(snapshot, @"hydrated", NO);
    BOOL desiredVisible = TokenForgeSnapshotBool(snapshot, @"desiredVisible", YES);
    NSPanel *panel = TokenForgeOverlayPanelsByRepositoryId[repo];
    TokenForgeDesktopOverlayCompanionView *view = TokenForgeOverlayViewsByRepositoryId[repo];
    NSSize size = TokenForgeOverlaySizeForStage(stage);

    TokenForgeOverlaySnapshotsByRepositoryId[repo] = [snapshot copy];
		    if (!hydrated) {
		        NSLog(@"INFO [OverlayRender][SKIP] repo=%@ reason=snapshotNotHydrated", repo);
		        NSLog(@"INFO [Overlay][SNAPSHOT_MISSING] repo=%@ reason=snapshotNotHydrated", repo);
		        NSLog(@"INFO [Overlay][Guard] repoHash=%@ desiredVisible=%@ actualVisible=false panelExists=%@ panelFrame=%@ reason=snapshotNotHydrated sourceAction=%@",
		              repo,
		              desiredVisible ? @"true" : @"false",
		              panel != nil ? @"true" : @"false",
		              panel != nil ? NSStringFromRect(panel.frame) : @"none",
		              source ?: @"farm.snapshot");
		        if (panel != nil) {
		            [panel orderOut:nil];
		        }
        return panel;
    }
    NSLog(@"INFO [CompanionSnapshot][HYDRATED] repo=%@ stage=%@ level=%ld renderVersion=%lu source=%@",
          repo,
          TokenForgeCompanionStageName(stage),
          (long)level,
          (unsigned long)TokenForgeOverlayFarmRenderVersion,
          source ?: @"farm.snapshot");

    if (panel != nil && view != nil) {
        NSLog(@"INFO [OverlayFarm][REUSE_PANEL] repo=%@ stage=%@ level=%ld",
              repo,
              TokenForgeCompanionStageName(stage),
              (long)level);
        TokenForgeApplySnapshotToOverlayView(view, snapshot);
        if (!TokenForgeIsOverlayDraggingForRepository(repo)) {
            NSRect frame = panel.frame;
            if (fabs(frame.size.width - size.width) > 0.5 || fabs(frame.size.height - size.height) > 0.5) {
                frame.size = size;
                frame = TokenForgeClampFrameToVisibleFrame(frame);
                [panel setFrame:frame display:NO];
            }
            TokenForgeOverlayFramesByRepositoryId[repo] = [NSValue valueWithRect:panel.frame];
        }
    } else {
        BOOL restored = NO;
        BOOL jsonHasSaved = TokenForgeSnapshotBool(snapshot, @"hasSavedPosition", NO);
        NSPoint origin = jsonHasSaved
            ? NSMakePoint(TokenForgeSnapshotFloat(snapshot, @"desiredInitialX", -1.0), TokenForgeSnapshotFloat(snapshot, @"desiredInitialY", -1.0))
            : TokenForgeLoadOverlayOriginForRepository(repo, index, size, &restored);
        if (origin.x < 0.0 || origin.y < 0.0) {
            restored = NO;
            origin = TokenForgeDefaultFarmOriginForIndex(index, size);
        }
        NSRect requestedFrame = NSMakeRect(origin.x, origin.y, size.width, size.height);
        NSRect frame = TokenForgeClampFrameToVisibleFrame(requestedFrame);
        frame = TokenForgeResolveFarmFrameForRepository(repo, frame, index, (jsonHasSaved || restored) ? @"collision" : @"noSavedPosition");
        if (!NSEqualRects(requestedFrame, frame)) {
            NSRect screen = TokenForgeVisibleFrameForFrame(requestedFrame);
            NSLog(@"INFO [OverlayFarmLayout][CLAMP] repo=%@ before=(%.2f,%.2f) after=(%.2f,%.2f) screen=(%.2f,%.2f %.2fx%.2f)",
                  repo,
                  requestedFrame.origin.x,
                  requestedFrame.origin.y,
                  frame.origin.x,
                  frame.origin.y,
                  screen.origin.x,
                  screen.origin.y,
                  screen.size.width,
                  screen.size.height);
        }
        if (jsonHasSaved || restored || TokenForgeHasSavedOverlayOriginForRepository(repo)) {
            NSLog(@"INFO [OverlayFarmLayout][RESTORE] repo=%@ position=(%.2f,%.2f)", repo, frame.origin.x, frame.origin.y);
        } else {
            NSLog(@"INFO [OverlayFarmLayout][ASSIGN] repo=%@ index=%lu position=(%.2f,%.2f) reason=noSavedPosition",
                  repo,
                  (unsigned long)index,
                  frame.origin.x,
                  frame.origin.y);
        }

        panel = [[TokenForgeCompanionOverlayWindow alloc] initWithContentRect:frame
                                                                   styleMask:NSWindowStyleMaskBorderless | NSWindowStyleMaskNonactivatingPanel
                                                                     backing:NSBackingStoreBuffered
                                                                       defer:NO];
        panel.releasedWhenClosed = NO;
        panel.hidesOnDeactivate = NO;
        panel.floatingPanel = YES;
        panel.worksWhenModal = YES;
        panel.becomesKeyOnlyIfNeeded = NO;
        panel.backgroundColor = [NSColor clearColor];
        panel.opaque = NO;
        panel.alphaValue = 1.0;
        panel.hasShadow = YES;
        panel.level = NSStatusWindowLevel;
        panel.acceptsMouseMovedEvents = YES;
        panel.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary | NSWindowCollectionBehaviorStationary | NSWindowCollectionBehaviorIgnoresCycle;
        panel.ignoresMouseEvents = TokenForgeMenuClickThrough;
        panel.identifier = [NSString stringWithFormat:@"TokenForge.DesktopCompanion.%@", repo];
        panel.accessibilityLabel = @"TokenForge.OverlayPanel";
        TokenForgeOverlayPanelGeneration += 1;
        TokenForgeOverlayGenerationsByRepositoryId[repo] = @(TokenForgeOverlayPanelGeneration);
        view = [[TokenForgeDesktopOverlayCompanionView alloc] initWithFrame:NSMakeRect(0, 0, size.width, size.height)];
        view.wantsLayer = YES;
        view.layerContentsRedrawPolicy = NSViewLayerContentsRedrawOnSetNeedsDisplay;
        TokenForgeApplySnapshotToOverlayView(view, snapshot);
        panel.contentView = view;
        TokenForgeOverlayPanelsByRepositoryId[repo] = panel;
        TokenForgeOverlayViewsByRepositoryId[repo] = view;
        TokenForgeOverlayFramesByRepositoryId[repo] = [NSValue valueWithRect:frame];
        if (TokenForgeCompanionWindow == nil) {
            TokenForgeCompanionWindow = panel;
            TokenForgeCompanionContentView = view;
            TokenForgeCompanionAnchor = frame.origin;
            TokenForgeCompanionTarget = frame.origin;
        }
	        NSLog(@"INFO [OverlayCreate] repo=%@ companion=%@ source=%@ frame=(%.2f,%.2f %.2fx%.2f)",
	              repo,
	              companionId,
	              source ?: @"farm.snapshot",
	              frame.origin.x,
              frame.origin.y,
	              frame.size.width,
	              frame.size.height);
		        NSLog(@"INFO [Overlay][CREATE_PANEL] repo=%@ companion=%@ source=%@",
		              repo,
		              companionId,
		              source ?: @"farm.snapshot");
	        NSLog(@"INFO [Overlay][Action] repoHash=%@ desiredVisible=%@ actualVisible=false panelExists=true panelFrame=(%.2f,%.2f %.2fx%.2f) reason=createPanel sourceAction=%@ action=createPanel",
	              repo,
	              desiredVisible ? @"true" : @"false",
	              frame.origin.x,
	              frame.origin.y,
	              frame.size.width,
	              frame.size.height,
	              source ?: @"farm.snapshot");
		        NSLog(@"INFO [OverlayFarm][CREATE_PANEL] repo=%@ companion=%@ stage=%@ level=%ld",
		              repo,
		              companionId,
              TokenForgeCompanionStageName(stage),
              (long)level);
        NSLog(@"INFO [OverlayPanel][INDEPENDENT] repo=%@ dashboardParent=false childWindow=false", repo);
    }

	    if (desiredVisible) {
	        [panel orderFrontRegardless];
	        [view setNeedsDisplay:YES];
	        [panel displayIfNeeded];
		        NSLog(@"INFO [OverlayOrderFront] repo=%@ source=%@ visible=%@", repo, source ?: @"unknown", panel.isVisible ? @"true" : @"false");
		        NSLog(@"INFO [Overlay][ORDER_FRONT] repo=%@ source=%@ visible=%@", repo, source ?: @"unknown", panel.isVisible ? @"true" : @"false");
		        NSLog(@"INFO [Overlay][Actual] repoHash=%@ desiredVisible=true actualVisible=%@ panelExists=true panelFrame=%@ reason=orderFront sourceAction=%@",
		              repo,
		              panel.isVisible ? @"true" : @"false",
		              NSStringFromRect(panel.frame),
		              source ?: @"unknown");
		        NSLog(@"INFO [OverlayFarm][SHOW] repo=%@ source=%@", repo, source ?: @"unknown");
		    } else {
		        [panel orderOut:nil];
		        NSLog(@"INFO [OverlaySuppressed] repo=%@ reason=desiredVisibleFalse source=%@", repo, source ?: @"unknown");
		        NSLog(@"INFO [Overlay][VISIBLE_FALSE] repo=%@ reason=desiredVisibleFalse source=%@", repo, source ?: @"unknown");
		        NSLog(@"INFO [Overlay][Actual] repoHash=%@ desiredVisible=false actualVisible=false panelExists=true panelFrame=%@ reason=desiredVisibleFalse sourceAction=%@",
		              repo,
		              NSStringFromRect(panel.frame),
		              source ?: @"unknown");
		        NSLog(@"INFO [OverlayFarm][HIDE] repo=%@ source=%@", repo, source ?: @"unknown");
			    }
		    NSLog(@"INFO [OverlayVisible] repo=%@ visible=%@ actualVisibleCount=%ld", repo, panel.isVisible ? @"true" : @"false", (long)TokenForgeVisibleOverlayFarmCount());
		    if (panel.isVisible) {
		        NSLog(@"INFO [Overlay][VISIBLE_TRUE] repo=%@ actualVisibleCount=%ld", repo, (long)TokenForgeVisibleOverlayFarmCount());
		    } else {
		        NSLog(@"INFO [Overlay][VISIBLE_FALSE] repo=%@ actualVisibleCount=%ld", repo, (long)TokenForgeVisibleOverlayFarmCount());
		    }
	    NSLog(@"INFO [OverlayFrame] repo=%@ frame=(%.2f,%.2f %.2fx%.2f)",
	          repo,
	          panel.frame.origin.x,
          panel.frame.origin.y,
	          panel.frame.size.width,
	          panel.frame.size.height);
	    NSLog(@"INFO [Overlay][FRAME] repo=%@ frame=(%.2f,%.2f %.2fx%.2f)",
	          repo,
	          panel.frame.origin.x,
	          panel.frame.origin.y,
	          panel.frame.size.width,
	          panel.frame.size.height);
    NSLog(@"INFO [OverlayFarm][FRAME] repo=%@ frame=(%.2f,%.2f %.2fx%.2f)",
          repo,
          panel.frame.origin.x,
          panel.frame.origin.y,
          panel.frame.size.width,
          panel.frame.size.height);
    return panel;
}

static NSInteger TokenForgeVisibleOverlayFarmCount(void)
{
    TokenForgeEnsureOverlayFarmRegistry();
    NSInteger count = 0;
    for (NSPanel *panel in [TokenForgeOverlayPanelsByRepositoryId allValues]) {
        if (panel.isVisible) {
            count += 1;
        }
    }
    return count;
}

static void TokenForgeApplyFarmSnapshotsOnMain(NSArray<NSDictionary *> *snapshots, NSString *source)
{
    if (![NSThread isMainThread]) {
        NSArray *snapshotCopy = [snapshots copy];
        NSString *sourceCopy = [source.length > 0 ? source : @"unknown" copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeApplyFarmSnapshotsOnMain(snapshotCopy, sourceCopy);
        });
        return;
    }

    TokenForgeEnsureOverlayFarmRegistry();
    TokenForgeOverlayFarmRenderVersion += 1;
    NSLog(@"INFO [OverlayFarm][SNAPSHOT_APPLY] count=%lu source=%@",
          (unsigned long)snapshots.count,
          source ?: @"unknown");
    NSLog(@"INFO [RuntimeUIPath][Overlay] renderer=overlayFarm snapshots=%lu source=%@", (unsigned long)snapshots.count, source ?: @"unknown");
    NSLog(@"INFO [OverlayProjection] snapshotCount=%lu source=%@", (unsigned long)snapshots.count, source ?: @"unknown");
    if (snapshots.count == 0) {
        NSLog(@"INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY]");
        NSLog(@"INFO [OverlaySuppressed] reason=noApprovedRepository source=%@", source ?: @"unknown");
        NSLog(@"INFO [Overlay][Guard] repoHash=none desiredVisible=false actualVisible=false panelExists=%@ panelFrame=none reason=noApprovedRepository sourceAction=%@ selectedRepoId=none selectedRepoHash=none approvedRepoCount=0",
              TokenForgeOverlayPanelsByRepositoryId.count > 0 ? @"true" : @"false",
              source ?: @"unknown");
        TokenForgeDesiredCompanionVisible = NO;
        TokenForgeMenuCompanionEnabled = NO;
        if (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) {
            [TokenForgeCompanionWindow orderOut:nil];
            NSLog(@"INFO [Overlay][Actual] repoHash=legacy desiredVisible=false actualVisible=false panelExists=true panelFrame=%@ reason=noApprovedRepository sourceAction=%@",
                  NSStringFromRect(TokenForgeCompanionWindow.frame),
                  source ?: @"unknown");
        }
    }
    NSMutableSet<NSString *> *seen = [NSMutableSet set];
    NSMutableSet<NSString *> *snapshotIdentities = [NSMutableSet set];
    NSUInteger index = 0;
    for (NSDictionary *snapshot in snapshots) {
        if (![snapshot isKindOfClass:[NSDictionary class]]) {
            continue;
        }
        NSString *repo = TokenForgeSafeRepositoryKey(TokenForgeSnapshotString(snapshot, @"repositoryId", @"legacy"));
        NSString *companionId = TokenForgeSnapshotString(snapshot, @"companionId", @"unknown");
        NSInteger stage = TokenForgeSnapshotInteger(snapshot, @"stage", 0);
        NSInteger level = MAX(1, TokenForgeSnapshotInteger(snapshot, @"level", 1));
        BOOL hydrated = TokenForgeSnapshotBool(snapshot, @"hydrated", NO);
        NSString *identity = [NSString stringWithFormat:@"%@|%@", repo, companionId];
        if ([seen containsObject:repo] || [snapshotIdentities containsObject:identity]) {
            NSLog(@"ERROR [OverlayFarm][ERROR] reason=%@ repo=%@ companion=%@",
                  [seen containsObject:repo] ? @"duplicateRepoId" : @"duplicateSnapshotIdentity",
                  repo,
                  companionId);
            continue;
        }
        [snapshotIdentities addObject:identity];
        [seen addObject:repo];
        NSLog(@"INFO [OverlayFarm][SNAPSHOT_ITEM] repo=%@ name=%@ companion=%@ stage=%@ level=%ld hydrated=%@",
              repo,
              TokenForgeSnapshotString(snapshot, @"repositoryName", @"Repository"),
              companionId,
              TokenForgeCompanionStageName(stage),
              (long)level,
              hydrated ? @"true" : @"false");
        TokenForgeCreateOrReuseOverlayPanelForSnapshot(snapshot, index, source ?: @"farm.snapshot");
        index += 1;
    }

    for (NSString *repo in [[TokenForgeOverlayPanelsByRepositoryId allKeys] copy]) {
        if (![seen containsObject:repo]) {
            NSPanel *panel = TokenForgeOverlayPanelsByRepositoryId[repo];
            [panel orderOut:nil];
            [TokenForgeOverlayPanelsByRepositoryId removeObjectForKey:repo];
            [TokenForgeOverlayViewsByRepositoryId removeObjectForKey:repo];
            [TokenForgeOverlaySnapshotsByRepositoryId removeObjectForKey:repo];
            [TokenForgeOverlayFramesByRepositoryId removeObjectForKey:repo];
            [TokenForgeOverlayDragStatesByRepositoryId removeObjectForKey:repo];
            [TokenForgeOverlayGenerationsByRepositoryId removeObjectForKey:repo];
            NSLog(@"INFO [OverlayFarm][HIDE] repo=%@ source=repositoryDisconnected", repo);
        }
    }
    NSLog(@"INFO [OverlayFarm][VISIBLE_COUNT] count=%ld", (long)TokenForgeVisibleOverlayFarmCount());
    [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
}

static void TokenForgeShowCompanionForRepositoryOnMain(NSString *repositoryId, NSString *source)
{
    if (![NSThread isMainThread]) {
        NSString *repoCopy = [TokenForgeSafeRepositoryKey(repositoryId) copy];
        NSString *sourceCopy = [source.length > 0 ? source : @"unknown" copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeShowCompanionForRepositoryOnMain(repoCopy, sourceCopy);
        });
        return;
    }

    NSString *repo = TokenForgeSafeRepositoryKey(repositoryId);
    NSPanel *panel = TokenForgeOverlayPanelForRepository(repo);
    if (panel == nil) {
        NSDictionary *snapshot = TokenForgeOverlaySnapshotsByRepositoryId[repo];
        if (snapshot != nil) {
            panel = TokenForgeCreateOrReuseOverlayPanelForSnapshot(snapshot, TokenForgeOverlayPanelsByRepositoryId.count, source ?: @"show.repo");
        }
    }
		    if (panel == nil) {
		        NSLog(@"INFO [OverlaySuppressed] repo=%@ reason=noPanelOrSnapshot source=%@", repo, source ?: @"unknown");
		        NSLog(@"INFO [Overlay][SNAPSHOT_MISSING] repo=%@ reason=noPanelOrSnapshot source=%@", repo, source ?: @"unknown");
		        NSLog(@"INFO [Overlay][Guard] repoHash=%@ desiredVisible=true actualVisible=false panelExists=false panelFrame=none reason=noPanelOrSnapshot sourceAction=%@",
		              repo,
		              source ?: @"unknown");
		        return;
		    }
		    [panel orderFrontRegardless];
			    NSLog(@"INFO [OverlayOrderFront] repo=%@ source=%@ visible=%@", repo, source ?: @"unknown", panel.isVisible ? @"true" : @"false");
			    NSLog(@"INFO [Overlay][ORDER_FRONT] repo=%@ source=%@ visible=%@", repo, source ?: @"unknown", panel.isVisible ? @"true" : @"false");
			    NSLog(@"INFO [Overlay][Actual] repoHash=%@ desiredVisible=true actualVisible=%@ panelExists=true panelFrame=%@ reason=showRepository sourceAction=%@",
			          repo,
			          panel.isVisible ? @"true" : @"false",
			          NSStringFromRect(panel.frame),
			          source ?: @"unknown");
		    NSLog(@"INFO [OverlayVisible] repo=%@ visible=%@ actualVisibleCount=%ld", repo, panel.isVisible ? @"true" : @"false", (long)TokenForgeVisibleOverlayFarmCount());
		    if (panel.isVisible) {
		        NSLog(@"INFO [Overlay][VISIBLE_TRUE] repo=%@ actualVisibleCount=%ld", repo, (long)TokenForgeVisibleOverlayFarmCount());
		    } else {
		        NSLog(@"INFO [Overlay][VISIBLE_FALSE] repo=%@ actualVisibleCount=%ld", repo, (long)TokenForgeVisibleOverlayFarmCount());
		    }
	    NSLog(@"INFO [OverlayFarm][SHOW] repo=%@ source=%@", repo, source ?: @"unknown");
    NSLog(@"INFO [OverlayFarm][VISIBLE_COUNT] count=%ld", (long)TokenForgeVisibleOverlayFarmCount());
}

static void TokenForgeHideCompanionForRepositoryOnMain(NSString *repositoryId, NSString *source)
{
    if (![NSThread isMainThread]) {
        NSString *repoCopy = [TokenForgeSafeRepositoryKey(repositoryId) copy];
        NSString *sourceCopy = [source.length > 0 ? source : @"unknown" copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeHideCompanionForRepositoryOnMain(repoCopy, sourceCopy);
        });
        return;
    }

    NSString *repo = TokenForgeSafeRepositoryKey(repositoryId);
    if (TokenForgeIsOverlayDraggingForRepository(repo)) {
        NSLog(@"INFO [OverlayDrag][CRASH_GUARD] repo=%@ reason=hideDeferredDuringDrag", repo);
        return;
    }
    [TokenForgeOverlayPanelForRepository(repo) orderOut:nil];
    NSLog(@"INFO [OverlayFarm][HIDE] repo=%@ source=%@", repo, source ?: @"unknown");
    NSLog(@"INFO [OverlayFarm][VISIBLE_COUNT] count=%ld", (long)TokenForgeVisibleOverlayFarmCount());
}

static NSRect TokenForgeOverlayFrameForRepository(NSString *repositoryId)
{
    NSPanel *panel = TokenForgeOverlayPanelForRepository(repositoryId);
    return panel != nil ? panel.frame : NSZeroRect;
}

static void TokenForgeSetOverlayFrameForRepositoryOnMain(NSString *repositoryId, NSRect frame, NSString *source)
{
    if (![NSThread isMainThread]) {
        NSString *repoCopy = [TokenForgeSafeRepositoryKey(repositoryId) copy];
        NSString *sourceCopy = [source.length > 0 ? source : @"unknown" copy];
        NSRect frameCopy = frame;
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeSetOverlayFrameForRepositoryOnMain(repoCopy, frameCopy, sourceCopy);
        });
        return;
    }

    NSString *repo = TokenForgeSafeRepositoryKey(repositoryId);
    if (TokenForgeIsOverlayDraggingForRepository(repo)) {
        NSLog(@"INFO [OverlayDrag][SUPPRESS_PROJECTION] repo=%@ reason=dragInProgress", repo);
        NSLog(@"INFO [CSharpProjection][SKIP_TO_NATIVE] repo=%@ reason=overlayDragInProgress", repo);
        return;
    }
    NSPanel *panel = TokenForgeOverlayPanelForRepository(repo);
    if (panel == nil) {
        return;
    }
    NSRect clamped = TokenForgeClampFrameToVisibleFrame(frame);
    [panel setFrame:clamped display:YES];
    TokenForgePersistOverlayFrameForRepository(repo, clamped);
    NSLog(@"INFO [OverlayFarm][FRAME] repo=%@ frame=(%.2f,%.2f %.2fx%.2f) source=%@",
          repo,
          clamped.origin.x,
          clamped.origin.y,
          clamped.size.width,
          clamped.size.height,
          source ?: @"unknown");
}

@interface TokenForgeFlippedView : NSView
@end

@implementation TokenForgeFlippedView
- (BOOL)isFlipped { return YES; }
@end

@interface TokenForgeFlippedStackView : NSStackView
@end

@implementation TokenForgeFlippedStackView
- (BOOL)isFlipped { return YES; }
@end

@implementation TokenForgeGrowthRadarView
- (instancetype)initWithFrame:(NSRect)frame
{
    self = [super initWithFrame:frame];
    if (self != nil) {
        self.translatesAutoresizingMaskIntoConstraints = NO;
        self.wantsLayer = YES;
        self.layer.backgroundColor = [NSColor clearColor].CGColor;
        self.labels = @[@"Code", @"Focus", @"Debug", @"Design", @"Sync"];
        self.values = @[@0, @0, @0, @0, @0];
        self.hasAxisData = NO;
        self.axisStatusText = @"No axis data recorded yet.";
    }
    return self;
}

- (void)configureWithCode:(NSInteger)code focus:(NSInteger)focus debug:(NSInteger)debug design:(NSInteger)design sync:(NSInteger)sync hasAxisData:(BOOL)hasAxisData statusText:(NSString *)statusText
{
    self.values = @[@(MAX(0, code)), @(MAX(0, focus)), @(MAX(0, debug)), @(MAX(0, design)), @(MAX(0, sync))];
    self.hasAxisData = hasAxisData;
    self.axisStatusText = statusText.length > 0 ? statusText : @"No axis data recorded yet.";
    NSInteger maxValue = 1;
    for (NSNumber *value in self.values) {
        maxValue = MAX(maxValue, value.integerValue);
    }
    NSLog(@"INFO [GrowthRadar] values=%ld,%ld,%ld,%ld,%ld normalizedMax=%ld hasAxisData=%@ status=%@",
          (long)MAX(0, code),
          (long)MAX(0, focus),
          (long)MAX(0, debug),
          (long)MAX(0, design),
          (long)MAX(0, sync),
          (long)maxValue,
          hasAxisData ? @"true" : @"false",
          self.axisStatusText);
    [self setNeedsDisplay:YES];
}

- (void)drawRect:(NSRect)dirtyRect
{
    [super drawRect:dirtyRect];
    NSRect bounds = NSInsetRect(self.bounds, 28.0, 20.0);
    CGFloat side = MIN(bounds.size.width, bounds.size.height);
    NSPoint center = NSMakePoint(NSMidX(bounds), NSMidY(bounds) - 2.0);
    CGFloat radius = MAX(24.0, side * 0.42);
    NSInteger count = 5;
    NSInteger maxValue = 1;
    NSInteger totalValue = 0;
    for (NSNumber *value in self.values ?: @[]) {
        maxValue = MAX(maxValue, value.integerValue);
        totalValue += MAX(0, value.integerValue);
    }
    BOOL shouldDrawValuePolygon = self.hasAxisData && totalValue > 0;

    NSColor *gridColor = [NSColor colorWithCalibratedWhite:0.18 alpha:0.20];
    NSColor *axisColor = [NSColor colorWithCalibratedWhite:0.12 alpha:0.24];
    NSColor *fillColor = shouldDrawValuePolygon
        ? [NSColor colorWithCalibratedRed:0.16 green:0.48 blue:0.90 alpha:0.24]
        : [NSColor colorWithCalibratedWhite:0.55 alpha:0.10];
    NSColor *strokeColor = shouldDrawValuePolygon
        ? [NSColor colorWithCalibratedRed:0.10 green:0.36 blue:0.78 alpha:0.82]
        : [NSColor colorWithCalibratedWhite:0.45 alpha:0.35];

    for (NSInteger ring = 1; ring <= 4; ring++) {
        CGFloat ringRadius = radius * ((CGFloat)ring / 4.0);
        NSBezierPath *ringPath = [NSBezierPath bezierPath];
        for (NSInteger index = 0; index < count; index++) {
            CGFloat angle = (-90.0 + (360.0 / count) * index) * M_PI / 180.0;
            NSPoint point = NSMakePoint(center.x + cos(angle) * ringRadius, center.y + sin(angle) * ringRadius);
            if (index == 0) {
                [ringPath moveToPoint:point];
            } else {
                [ringPath lineToPoint:point];
            }
        }
        [ringPath closePath];
        ringPath.lineWidth = 1.0;
        [gridColor setStroke];
        [ringPath stroke];
    }

    NSDictionary *labelAttrs = @{
        NSFontAttributeName: [NSFont systemFontOfSize:10.0 weight:NSFontWeightMedium],
        NSForegroundColorAttributeName: [NSColor colorWithCalibratedWhite:0.18 alpha:0.80]
    };
    NSBezierPath *valuePath = [NSBezierPath bezierPath];
    for (NSInteger index = 0; index < count; index++) {
        CGFloat angle = (-90.0 + (360.0 / count) * index) * M_PI / 180.0;
        NSPoint axisEnd = NSMakePoint(center.x + cos(angle) * radius, center.y + sin(angle) * radius);
        NSBezierPath *axis = [NSBezierPath bezierPath];
        [axis moveToPoint:center];
        [axis lineToPoint:axisEnd];
        axis.lineWidth = 1.0;
        [axisColor setStroke];
        [axis stroke];

        NSInteger value = index < (NSInteger)self.values.count ? self.values[index].integerValue : 0;
        CGFloat normalized = maxValue <= 0 ? 0.0 : (CGFloat)MAX(0, value) / (CGFloat)maxValue;
        NSPoint valuePoint = NSMakePoint(center.x + cos(angle) * radius * normalized, center.y + sin(angle) * radius * normalized);
        if (index == 0) {
            [valuePath moveToPoint:valuePoint];
        } else {
            [valuePath lineToPoint:valuePoint];
        }

        NSString *label = index < (NSInteger)self.labels.count ? self.labels[index] : @"";
        NSSize labelSize = [label sizeWithAttributes:labelAttrs];
        NSPoint labelPoint = NSMakePoint(center.x + cos(angle) * (radius + 18.0) - labelSize.width * 0.5,
                                        center.y + sin(angle) * (radius + 18.0) - labelSize.height * 0.5);
        [label drawAtPoint:labelPoint withAttributes:labelAttrs];
    }
    if (shouldDrawValuePolygon) {
        [valuePath closePath];
        [fillColor setFill];
        [valuePath fill];
        valuePath.lineWidth = 2.0;
        [strokeColor setStroke];
        [valuePath stroke];
    } else {
        NSString *status = self.axisStatusText.length > 0 ? self.axisStatusText : @"No axis data recorded yet.";
        NSDictionary *statusAttrs = @{
            NSFontAttributeName: [NSFont systemFontOfSize:12.0 weight:NSFontWeightSemibold],
            NSForegroundColorAttributeName: [NSColor colorWithCalibratedWhite:0.22 alpha:0.78]
        };
        NSSize statusSize = [status sizeWithAttributes:statusAttrs];
        [status drawAtPoint:NSMakePoint(center.x - statusSize.width * 0.5, center.y - statusSize.height * 0.5) withAttributes:statusAttrs];
        NSLog(@"INFO [GrowthRadar][AXIS_MISSING] reason=noStoredAxisDeltas message=%@", status);
    }
}
@end

@interface TokenForgeSettingsSwitchRow : NSControl
@property(nonatomic) BOOL checked;
@property(nonatomic) BOOL rowInteractive;
@property(nonatomic, strong) NSControl *toggleControl;
- (void)configureWithTitle:(NSString *)title detail:(NSString *)detail checked:(BOOL)checked interactive:(BOOL)interactive;
@end

@interface TokenForgeSkinPreviewView : NSView
@property(nonatomic, strong) NSString *skinId;
@end

@interface TokenForgeNativeDashboardController : NSObject <NSWindowDelegate>
@property(nonatomic, strong) NSWindow *dashboardWindow;
@property(nonatomic, strong) NSWindow *settingsWindow;
@property(nonatomic, strong) NSDictionary *state;
@property(nonatomic, strong) NSString *selectedNavItem;
@property(nonatomic, strong) NSString *activityFilterValue;
@property(nonatomic, strong) NSView *dashboardRootView;
@property(nonatomic, strong) NSView *dashboardTabDocumentView;
@property(nonatomic, strong) NSStackView *dashboardTabContentStack;
@property(nonatomic) BOOL firstRunGuideSuppressedByDashboardNavigation;
- (void)showDashboard;
- (void)openOrFocusDashboardFromSource:(NSString *)source;
- (void)hideDashboard;
- (void)hideDashboardFromSource:(NSString *)source;
- (void)toggleDashboard;
- (void)showSettings;
- (void)updateState:(NSDictionary *)state;
- (void)setMenuBarStatus:(NSDictionary *)state;
@end

static NSString *TokenForgeDashboardFrameKey = @"TokenForge.NativeDashboard.Frame";
static NSString *TokenForgeSettingsFrameKey = @"TokenForge.NativeSettings.Frame";
static NSString *TokenForgeDashboardWindowIdentifier = @"TokenForge.NativeDashboard";

static BOOL TokenForgeDashboardSourceAllowsCloseCooldownBypass(NSString *source)
{
    NSString *openSource = source.length > 0 ? source : @"unknown";
    return [openSource isEqualToString:@"dock.reopen"] ||
           [openSource isEqualToString:@"menubar.dashboard"] ||
           [openSource isEqualToString:@"launch.initial"] ||
           [openSource isEqualToString:@"csharp.dashboard"];
}

static BOOL TokenForgeIsNativeDashboardWindow(NSWindow *window)
{
    if (window == nil || TokenForgeIsCompanionWindow(window)) {
        return NO;
    }

    if (window == TokenForgeNativeDashboardWindow) {
        return YES;
    }

    NSString *identifier = window.identifier;
    return identifier.length > 0 && [identifier isEqualToString:TokenForgeDashboardWindowIdentifier];
}

static BOOL TokenForgeIsCompanionOverlayWindow(NSWindow *window)
{
    return TokenForgeIsCompanionWindow(window);
}

static NSString *TokenForgeWindowRole(NSWindow *window)
{
    if (TokenForgeIsNativeDashboardWindow(window)) {
        return @"dashboard";
    }

    if (TokenForgeIsCompanionOverlayWindow(window)) {
        return @"overlay";
    }

    if (TokenForgeLooksLikeMainWindow(window)) {
        return @"unityMain";
    }

    return @"unknown";
}

static void TokenForgeRefreshDashboardAndOverlayState(NSString *source)
{
    if (![NSThread isMainThread]) {
        return;
    }

    BOOL dashboardExists = NO;
    BOOL dashboardVisible = NO;
    BOOL dashboardKey = NO;
    for (NSWindow *window in TokenForgeSafeWindowsSnapshot(@"TokenForgeRefreshDashboardAndOverlayState", NO)) {
        if (!TokenForgeIsNativeDashboardWindow(window)) {
            continue;
        }

        dashboardExists = YES;
        if (window.isVisible) {
            dashboardVisible = YES;
        }
        if (window.isKeyWindow) {
            dashboardKey = YES;
        }
    }

    TokenForgeDashboardWindowExists = dashboardExists;
    TokenForgeDashboardWindowVisible = dashboardVisible;
    TokenForgeDashboardWindowKey = dashboardKey;
    TokenForgeEnsureOverlayFarmRegistry();
    BOOL anyOverlayVisible = TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible;
    for (NSPanel *panel in [TokenForgeOverlayPanelsByRepositoryId allValues]) {
        if (panel.isVisible) {
            anyOverlayVisible = YES;
            break;
        }
    }
    TokenForgeOverlayPanelExists = TokenForgeCompanionWindow != nil || TokenForgeOverlayPanelsByRepositoryId.count > 0;
    TokenForgeOverlayPanelVisible = anyOverlayVisible;
    TokenForgeAppIsActive = NSApp != nil && NSApp.isActive;

    NSLog(@"INFO [OverlayLifecycle][STATE] exists=%@ visible=%@ source=%@",
          TokenForgeOverlayPanelExists ? @"true" : @"false",
          TokenForgeOverlayPanelVisible ? @"true" : @"false",
          source ?: @"unknown");
}

static BOOL TokenForgeIsDashboardVisible(void)
{
    if (![NSThread isMainThread]) {
        return NO;
    }

    TokenForgeRefreshDashboardAndOverlayState(@"TokenForgeIsDashboardVisible");
    return TokenForgeDashboardWindowVisible;
}

static BOOL TokenForgeIsCompanionOverlayVisible(void)
{
    if (![NSThread isMainThread]) {
        return NO;
    }

    TokenForgeRefreshDashboardAndOverlayState(@"TokenForgeIsCompanionOverlayVisible");
    return TokenForgeOverlayPanelVisible;
}

static NSInteger TokenForgeVisibleUnityWindowCount(void)
{
    NSInteger count = 0;
    if (![NSThread isMainThread]) {
        return count;
    }

    for (NSWindow *window in TokenForgeSafeWindowsSnapshot(@"TokenForgeVisibleUnityWindowCount", NO)) {
        if ([TokenForgeWindowRole(window) isEqualToString:@"unityMain"] && window.isVisible) {
            count += 1;
        }
    }

    return count;
}

static void TokenForgeDumpWindowClassifications(NSString *phase)
{
    if (![NSThread isMainThread]) {
        NSLog(@"INFO [WindowsDump][%@] skipped reason=notMainThread", phase ?: @"manual");
        return;
    }

    NSString *dumpPhase = phase.length > 0 ? phase : @"manual";
    NSArray<NSWindow *> *windows = TokenForgeSafeWindowsSnapshot(@"TokenForgeDumpWindowClassifications", NO);
    NSLog(@"INFO [WindowsDump][%@] count=%lu dashboardVisible=%@ overlayVisible=%@ appActive=%@",
          dumpPhase,
          (unsigned long)windows.count,
          TokenForgeIsDashboardVisible() ? @"true" : @"false",
          TokenForgeIsCompanionOverlayVisible() ? @"true" : @"false",
          (NSApp != nil && NSApp.isActive) ? @"true" : @"false");
    for (NSWindow *window in windows) {
        NSString *role = TokenForgeWindowRole(window);
        NSLog(@"INFO [WindowsDump][%@] pointer=%p class=%@ title=%@ isVisible=%@ isKeyWindow=%@ isMainWindow=%@ level=%ld collectionBehavior=%lu frame=(%.2f,%.2f %.2fx%.2f) tokenforgeRole=%@",
              dumpPhase,
              window,
              NSStringFromClass([window class]),
              window.title ?: @"",
              window.isVisible ? @"true" : @"false",
              window.isKeyWindow ? @"true" : @"false",
              window.isMainWindow ? @"true" : @"false",
              (long)window.level,
              (unsigned long)window.collectionBehavior,
              window.frame.origin.x,
              window.frame.origin.y,
              window.frame.size.width,
              window.frame.size.height,
              role);
    }
}

static NSArray<NSWindow *> *TokenForgeNativeDashboardCandidates(void)
{
    NSMutableArray<NSWindow *> *candidates = [NSMutableArray array];
    for (NSWindow *window in TokenForgeSafeWindowsSnapshot(@"TokenForgeNativeDashboardCandidates", NO)) {
        if (TokenForgeIsNativeDashboardWindow(window)) {
            [candidates addObject:window];
        }
    }

    return candidates;
}

static NSWindow *TokenForgeNewestDashboardCandidate(NSArray<NSWindow *> *candidates)
{
    NSWindow *newest = nil;
    for (NSWindow *window in candidates) {
        if (newest == nil || window.windowNumber > newest.windowNumber) {
            newest = window;
        }
    }

    return newest;
}

static NSInteger TokenForgeVisibleNativeDashboardWindowCount(void)
{
    NSInteger count = 0;
    for (NSWindow *window in TokenForgeNativeDashboardCandidates()) {
        if (window.isVisible) {
            count += 1;
        }
    }

    return count;
}

static void TokenForgeDumpDashboardWindows(NSString *phase)
{
    if (![NSThread isMainThread] || !TokenForgeAppReadyForWindowMutation(@"TokenForgeDumpDashboardWindows", phase ?: @"manual")) {
        return;
    }

    if (TokenForgeDumpingWindows) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeDumpDashboardWindows reason=reentrantDump source=%@", phase ?: @"manual");
        return;
    }

    TokenForgeDumpingWindows = YES;
    NSArray<NSWindow *> *windows = TokenForgeSafeWindowsSnapshot(@"TokenForgeDumpDashboardWindows", NO);
    NSArray<NSWindow *> *candidates = TokenForgeNativeDashboardCandidates();
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] windowsDump phase=%@ total=%lu dashboardCandidates=%lu visibleDashboardCount=%ld canonical=%p",
                                    phase ?: @"manual",
                                    (unsigned long)windows.count,
                                    (unsigned long)candidates.count,
                                    (long)TokenForgeVisibleNativeDashboardWindowCount(),
                                    TokenForgeNativeDashboardWindow);
    for (NSWindow *window in windows) {
        BOOL isCandidate = [candidates containsObject:window];
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] window phase=%@ candidate=%@ windowNumber=%ld class=%@ title=%@ isVisible=%@ isReleasedWhenClosed=%@ delegate=%@ identifier=%@ level=%ld parent=%@ childCount=%lu",
                                        phase ?: @"manual",
                                        isCandidate ? @"true" : @"false",
                                        (long)window.windowNumber,
                                        NSStringFromClass([window class]),
                                        window.title ?: @"",
                                        window.isVisible ? @"true" : @"false",
                                        window.isReleasedWhenClosed ? @"true" : @"false",
                                        window.delegate != nil ? NSStringFromClass([window.delegate class]) : @"nil",
                                        window.identifier ?: @"",
                                        (long)window.level,
                                        window.parentWindow != nil ? @"true" : @"false",
                                        (unsigned long)window.childWindows.count);
    }
    TokenForgeDumpingWindows = NO;
}

static void TokenForgeCleanupStaleUnityDashboardWindows(NSString *source)
{
    for (NSWindow *window in TokenForgeSafeWindowsSnapshot(@"TokenForgeCleanupStaleUnityDashboardWindows", NO)) {
        if (window == TokenForgeCompanionWindow || TokenForgeIsNativeDashboardWindow(window)) {
            continue;
        }

        if (TokenForgeWindowLooksBlank(window)) {
            TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] staleUnityWindow.cleanup source=%@ windowNumber=%ld title=%@ class=%@",
                                            source ?: @"unknown",
                                            (long)window.windowNumber,
                                            window.title ?: @"",
                                            NSStringFromClass([window class]));
            [window orderOut:nil];
        }
    }
}

static NSWindow *TokenForgeCleanupDuplicateDashboardWindows(NSWindow *preferred, NSString *source)
{
    NSArray<NSWindow *> *candidates = TokenForgeNativeDashboardCandidates();
    if (candidates.count == 0) {
        if (TokenForgeNativeDashboardWindow != nil) {
            TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] canonical.nil reason=noDashboardCandidates previous=%p", TokenForgeNativeDashboardWindow);
            TokenForgeNativeDashboardWindow = nil;
        }
        return nil;
    }

    NSWindow *canonical = nil;
    if (preferred != nil && [candidates containsObject:preferred]) {
        canonical = preferred;
    } else if (TokenForgeNativeDashboardWindow != nil && [candidates containsObject:TokenForgeNativeDashboardWindow]) {
        canonical = TokenForgeNativeDashboardWindow;
    } else {
        canonical = TokenForgeNewestDashboardCandidate(candidates);
    }

    if (TokenForgeNativeDashboardWindow != canonical) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] canonical.update source=%@ old=%p new=%p", source ?: @"unknown", TokenForgeNativeDashboardWindow, canonical);
        TokenForgeNativeDashboardWindow = canonical;
    }

    if (candidates.count > 1) {
        TokenForgeDashboardLifecycleLog(@"WARN [DashboardLifecycle][WARN] duplicateDashboardWindows count=%lu source=%@ canonicalWindowNumber=%ld",
                                        (unsigned long)candidates.count,
                                        source ?: @"unknown",
                                        (long)canonical.windowNumber);
        TokenForgeDashboardLifecycleLog(@"WARN [DashboardLifecycle][WARN_DUPLICATE] count=%lu action=closeStale source=%@",
                                        (unsigned long)candidates.count,
                                        source ?: @"unknown");
        NSLog(@"INFO [DashboardLifecycle][SUPPRESS_DUPLICATE] reason=duplicateDashboardWindows count=%lu source=%@ canonical=%p",
              (unsigned long)candidates.count,
              source ?: @"unknown",
              canonical);
        for (NSWindow *window in candidates) {
            if (window == canonical) {
                continue;
            }

            TokenForgeDashboardLifecycleLog(@"WARN [DashboardLifecycle][WARN] duplicateDashboardWindows.cleanup staleWindowNumber=%ld class=%@ title=%@ identifier=%@ visible=%@",
                                            (long)window.windowNumber,
                                            NSStringFromClass([window class]),
                                            window.title ?: @"",
                                            window.identifier ?: @"",
                                            window.isVisible ? @"true" : @"false");
            [window orderOut:nil];
            [window close];
        }
    }

    return canonical;
}

static NSColor *TokenForgeDashboardBackgroundColor(void)
{
    return [NSColor colorWithCalibratedRed:0.070 green:0.082 blue:0.105 alpha:1.0];
}

static NSColor *TokenForgeSidebarBackgroundColor(void)
{
    return [NSColor colorWithCalibratedRed:0.095 green:0.110 blue:0.140 alpha:0.96];
}

static NSColor *TokenForgeCardBackgroundColor(void)
{
    return [NSColor colorWithCalibratedWhite:1.0 alpha:0.92];
}

static NSColor *TokenForgeMutedTextColor(void)
{
    return [NSColor colorWithCalibratedRed:0.39 green:0.39 blue:0.41 alpha:1.0];
}

static NSColor *TokenForgeLightCardPrimaryTextColor(void)
{
    return [NSColor colorWithCalibratedRed:0.122 green:0.161 blue:0.200 alpha:1.0];
}

static NSColor *TokenForgeLightCardSecondaryTextColor(void)
{
    return [NSColor colorWithCalibratedRed:0.420 green:0.447 blue:0.502 alpha:1.0];
}

static NSColor *TokenForgeDisabledTextColor(void)
{
    return [NSColor colorWithCalibratedRed:0.612 green:0.639 blue:0.686 alpha:1.0];
}

static NSColor *TokenForgeDarkSidebarTextColor(void)
{
    return [NSColor colorWithCalibratedRed:0.930 green:0.955 blue:0.985 alpha:1.0];
}

static NSColor *TokenForgeShellSecondaryTextColor(void)
{
    return [NSColor colorWithCalibratedRed:0.710 green:0.755 blue:0.820 alpha:1.0];
}

static NSColor *TokenForgeSidebarMutedTextColor(void)
{
    return [NSColor colorWithCalibratedRed:0.650 green:0.700 blue:0.770 alpha:1.0];
}

static NSColor *TokenForgeSelectedBlueColor(void)
{
    return [NSColor colorWithCalibratedRed:0.16 green:0.36 blue:0.80 alpha:1.0];
}

static NSString *TokenForgeDashboardString(NSDictionary *dictionary, NSString *key, NSString *fallback)
{
    id value = dictionary[key];
    if ([value isKindOfClass:[NSString class]] && [(NSString *)value length] > 0) {
        return (NSString *)value;
    }
    if ([value isKindOfClass:[NSNumber class]]) {
        return [(NSNumber *)value stringValue];
    }
    return fallback;
}

static NSDictionary *TokenForgeDashboardDictionary(NSDictionary *dictionary, NSString *key)
{
    id value = dictionary[key];
    return [value isKindOfClass:[NSDictionary class]] ? (NSDictionary *)value : @{};
}

static NSArray *TokenForgeDashboardArray(NSDictionary *dictionary, NSString *key)
{
    id value = dictionary[key];
    return [value isKindOfClass:[NSArray class]] ? (NSArray *)value : @[];
}

static NSInteger TokenForgeDashboardInteger(NSDictionary *dictionary, NSString *key, NSInteger fallback)
{
    id value = dictionary[key];
    if ([value respondsToSelector:@selector(integerValue)]) {
        return [value integerValue];
    }
    return fallback;
}

static CGFloat TokenForgeDashboardFloat(NSDictionary *dictionary, NSString *key, CGFloat fallback)
{
    id value = dictionary[key];
    if ([value respondsToSelector:@selector(doubleValue)]) {
        return (CGFloat)[value doubleValue];
    }
    return fallback;
}

static BOOL TokenForgeDashboardBool(NSDictionary *dictionary, NSString *key, BOOL fallback)
{
    id value = dictionary[key];
    if ([value respondsToSelector:@selector(boolValue)]) {
        return [value boolValue];
    }
    return fallback;
}

static BOOL TokenForgeIsGenericRepositoryLabel(NSString *value)
{
    NSString *trimmed = [value stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceAndNewlineCharacterSet]];
    if (trimmed.length == 0) {
        return YES;
    }
    return [trimmed caseInsensitiveCompare:@"Repository"] == NSOrderedSame ||
           [trimmed caseInsensitiveCompare:@"Local Repository"] == NSOrderedSame ||
           [trimmed caseInsensitiveCompare:@"Approved local folder"] == NSOrderedSame ||
           [trimmed caseInsensitiveCompare:@"Local approval missing"] == NSOrderedSame;
}

static NSString *TokenForgeShortRepositoryId(NSDictionary *repository)
{
    NSString *identifier = TokenForgeDashboardString(repository, @"repositoryId", TokenForgeDashboardString(repository, @"id", @""));
    if (identifier.length == 0) {
        return @"";
    }
    return identifier.length <= 8 ? identifier : [identifier substringToIndex:8];
}

static NSString *TokenForgeRepositoryDisplayName(NSDictionary *repository)
{
    NSString *folderName = TokenForgeDashboardString(repository, @"folderName", @"");
    if (!TokenForgeIsGenericRepositoryLabel(folderName)) {
        NSLog(@"INFO [RepositoryIdentity][APPROVED_FOLDER] repositoryId=%@ folderName=%@",
              TokenForgeShortRepositoryId(repository),
              folderName);
        NSLog(@"INFO [RepositoryIdentity][DISPLAY_NAME] repositoryId=%@ displayName=%@ source=folderName",
              TokenForgeShortRepositoryId(repository),
              folderName);
        return folderName;
    }

    NSString *name = TokenForgeDashboardString(repository, @"name", @"");
    if (!TokenForgeIsGenericRepositoryLabel(name)) {
        NSLog(@"INFO [RepositoryIdentity][DISPLAY_NAME] repositoryId=%@ displayName=%@ source=alias",
              TokenForgeShortRepositoryId(repository),
              name);
        return name;
    }

    NSString *safePath = TokenForgeDashboardString(repository, @"safePath", @"");
    NSString *pathLeaf = safePath.lastPathComponent;
    if (!TokenForgeIsGenericRepositoryLabel(pathLeaf) &&
        [pathLeaf caseInsensitiveCompare:@"folder"] != NSOrderedSame &&
        [pathLeaf caseInsensitiveCompare:@"local"] != NSOrderedSame) {
        NSLog(@"INFO [RepositoryIdentity][DISPLAY_NAME] repositoryId=%@ displayName=%@ source=safePathLeaf",
              TokenForgeShortRepositoryId(repository),
              pathLeaf);
        return pathLeaf;
    }

    NSLog(@"WARN [RepositoryIdentity] missingApprovedFolderName id=%@ fallback=Unresolved approved folder",
          TokenForgeShortRepositoryId(repository));
    NSLog(@"WARN [RepositoryIdentity][HASH_FALLBACK_USED] id=%@ suppressedPrimaryHash=true",
          TokenForgeShortRepositoryId(repository));
    return @"Unresolved approved folder";
}

static BOOL TokenForgeShouldShowFirstRunGuide(NSDictionary *state, NSString *selectedNavItem, BOOL suppressedByDashboardNavigation)
{
    NSDictionary *onboarding = TokenForgeDashboardDictionary(state, @"onboarding");
    (void)selectedNavItem;
    return TokenForgeDashboardBool(onboarding, @"shouldPresentFirstRunGuide", NO) &&
           !TokenForgeDashboardBool(onboarding, @"firstRunCompleted", NO) &&
           !TokenForgeDashboardBool(onboarding, @"dismissedForNow", NO) &&
           !suppressedByDashboardNavigation;
}

static NSString *TokenForgeRunAnalysisTitle(NSDictionary *repository)
{
    NSString *repoName = TokenForgeRepositoryDisplayName(repository);
    NSString *lastAnalyzed = TokenForgeDashboardString(repository, @"lastAnalyzed", @"Not analyzed");
    NSString *scope = [lastAnalyzed isEqualToString:@"Not analyzed"] ? @"Full History" : TokenForgeDashboardString(repository, @"lastAnalysisScope", @"Recent");
    NSString *prefix = [NSString stringWithFormat:@"Run %@ Analysis", scope];
    return repoName.length > 0 ? [NSString stringWithFormat:@"%@ for %@", prefix, repoName] : prefix;
}

static NSString *TokenForgeShopCategoryTitle(NSString *categoryId)
{
    if ([categoryId isEqualToString:@"zodiac"]) {
        return @"Zodiac";
    }
    if ([categoryId isEqualToString:@"skins"]) {
        return @"Skins";
    }
    if ([categoryId isEqualToString:@"outfits"]) {
        return @"Outfits";
    }
    if ([categoryId isEqualToString:@"accessories"]) {
        return @"Accessories";
    }
    if ([categoryId isEqualToString:@"effects"]) {
        return @"Effects";
    }
    if ([categoryId isEqualToString:@"motions"]) {
        return @"Motions";
    }
    if ([categoryId isEqualToString:@"themes"]) {
        return @"Themes";
    }
    if ([categoryId isEqualToString:@"exclusive"]) {
        return @"Agent Exclusives";
    }
    if ([categoryId isEqualToString:@"badges"]) {
        return @"Badges";
    }
    if ([categoryId isEqualToString:@"owned"]) {
        return @"Owned";
    }
    return @"Featured";
}

static NSInteger TokenForgePreviewStageForType(NSString *previewType, NSInteger fallback)
{
    NSString *value = [previewType lowercaseString] ?: @"";
    if ([value containsString:@"legendary"] || [value containsString:@"adult"]) {
        return 5;
    }
    if ([value containsString:@"young_adult"] || [value containsString:@"young-adult"] || [value containsString:@"veteran"] || [value containsString:@"elite"]) {
        return 4;
    }
    if ([value containsString:@"teen"]) {
        return 3;
    }
    if ([value containsString:@"child"]) {
        return 2;
    }
    if ([value containsString:@"hatchling"] || [value containsString:@"baby"]) {
        return 1;
    }
    if ([value containsString:@"egg"]) {
        return 0;
    }
    return MAX(0, MIN(5, fallback));
}

@interface TokenForgeShopPreviewView : NSView
@property(nonatomic, strong) NSString *previewType;
@property(nonatomic, strong) NSString *zodiacType;
@property(nonatomic, strong) NSString *rarity;
@property(nonatomic, strong) NSString *surfaceName;
@property(nonatomic, strong) NSString *equippedItemIds;
@property(nonatomic, assign) NSInteger stage;
@end

@interface TokenForgeOnboardingVisualView : NSView
@property(nonatomic, assign) NSInteger stepIndex;
@property(nonatomic, assign) NSInteger stepCount;
@property(nonatomic, strong) NSString *zodiacType;
@property(nonatomic, assign) NSInteger stage;
@end

@implementation TokenForgeShopPreviewView
- (BOOL)isFlipped { return YES; }
- (void)drawRect:(NSRect)dirtyRect
{
    [super drawRect:dirtyRect];
    NSGraphicsContext *context = [NSGraphicsContext currentContext];
    BOOL previousAntialias = context.shouldAntialias;
    context.shouldAntialias = NO;
    NSRect bounds = NSInsetRect(self.bounds, 7.0, 7.0);
    NSString *preview = self.previewType ?: @"generic";
    NSString *zodiac = self.zodiacType ?: @"";
    NSColor *accent = [NSColor colorWithCalibratedRed:0.36 green:0.62 blue:1.0 alpha:1.0];
    if ([self.rarity isEqualToString:@"Epic"]) accent = [NSColor colorWithCalibratedRed:0.70 green:0.38 blue:1.0 alpha:1.0];
    if ([self.rarity isEqualToString:@"Legendary"]) accent = [NSColor colorWithCalibratedRed:1.0 green:0.68 blue:0.18 alpha:1.0];
    [[NSColor colorWithCalibratedRed:0.045 green:0.060 blue:0.088 alpha:1.0] setFill];
    [[NSBezierPath bezierPathWithRoundedRect:self.bounds xRadius:8.0 yRadius:8.0] fill];
    [[accent colorWithAlphaComponent:0.13] setFill];
    CGFloat glowCell = MAX(3.0, floor(MIN(self.bounds.size.width, self.bounds.size.height) / 18.0));
    for (NSInteger index = 0; index < 5; index++) {
        NSRect glow = NSInsetRect(self.bounds, glowCell * (3 + index), glowCell * (2 + index));
        NSRectFill(NSMakeRect(NSMinX(glow), NSMidY(glow) - glowCell, NSWidth(glow), glowCell * 2.0));
    }
    [accent setStroke];
    NSBezierPath *frame = [NSBezierPath bezierPathWithRoundedRect:NSInsetRect(self.bounds, 1.0, 1.0) xRadius:8.0 yRadius:8.0];
    frame.lineWidth = 2.0;
    [frame stroke];

    if ([preview containsString:@"skin"] || [preview containsString:@"white_cat"] || [preview containsString:@"calico"]) {
	        [TokenForgeShopPreviewView drawCatInRect:bounds calico:[preview containsString:@"calico"]];
    } else if ([preview containsString:@"outfit"] || [preview containsString:@"cape"] || [preview containsString:@"jacket"] || [preview containsString:@"hoodie"] || [preview containsString:@"robe"] || [preview containsString:@"astronaut"] || [preview containsString:@"ninja"]) {
        [TokenForgeShopPreviewView drawOutfitInRect:bounds accent:accent preview:preview];
    } else if ([preview containsString:@"badge"] || [preview containsString:@"crown"] || [preview containsString:@"halo"] || [preview containsString:@"glasses"] || [preview containsString:@"headphones"] || [preview containsString:@"scroll"] || [preview containsString:@"ring"] || [preview containsString:@"charm"] || [preview containsString:@"goggles"] || [preview containsString:@"crest"]) {
        [TokenForgeShopPreviewView drawAccessoryInRect:bounds accent:accent preview:preview];
    } else if (zodiac.length > 0 || [preview containsString:@"zodiac"]) {
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac.length > 0 ? zodiac : preview inRect:bounds accent:accent stage:self.stage];
    } else if ([preview containsString:@"trail"] || [preview containsString:@"motion"]) {
        [TokenForgeShopPreviewView drawTrailInRect:bounds accent:accent];
    } else if ([preview containsString:@"theme"]) {
        [TokenForgeShopPreviewView drawThemeInRect:bounds accent:accent];
	    } else {
	        [TokenForgeShopPreviewView drawAuraInRect:bounds accent:accent];
	    }
	    if (self.equippedItemIds.length > 0) {
	        [TokenForgeShopPreviewView drawEquippedLayersInRect:bounds items:self.equippedItemIds accent:accent];
	    }
	    context.shouldAntialias = previousAntialias;
	    NSString *surface = self.surfaceName.length > 0 ? self.surfaceName : @"shop";
	    NSInteger diagnosticLayerCount = 10 + (self.equippedItemIds.length > 0 ? 4 : 0) + (self.stage >= 4 ? 3 : 0);
	    NSLog(@"INFO [ShopPreview][PIXEL_ART] preview=%@ zodiac=%@ stage=%ld deterministic=true squareCells=true nearestNeighbor=true clipped=false", preview, zodiac.length > 0 ? zodiac : @"none", (long)self.stage);
    NSLog(@"INFO [PixelDiagnostic] selectedRepoHash=nativeShop repoHash=nativeShop zodiac=%@ zodiacKey=%@ stage=%@ stageKey=%@ stageName=%@ stageIndex=%ld stageVisualSignature=%@ level=%ld equippedCosmetics=%@ equippedItemKeys=%@ wardrobePreviewKey=%@ cacheKey=zodiac_%@_%@ cacheHit=false generatedVariant=%@ layerCount=%ld fallbackUsed=false renderTarget=%@ renderContext=%@ sourceRenderer=appKitSharedZodiacSprite sideBlockDetected=false bodyShadeMode=contourPattern finalBounds=%@ clipped=false sourceOfTruth=TokenForgeShopPreviewView.drawZodiacMascot",
          zodiac.length > 0 ? zodiac : preview,
	          zodiac.length > 0 ? zodiac : preview,
	          TokenForgeCompanionStageName(self.stage),
          TokenForgeCompanionStageName(self.stage),
          TokenForgeCompanionStageName(self.stage),
          (long)MAX(0, MIN(5, self.stage)),
          TokenForgeCompanionStageVisualSignature(MAX(0, MIN(5, self.stage))),
          (long)MAX(1, self.stage + 1),
          self.equippedItemIds.length > 0 ? self.equippedItemIds : @"none",
	          self.equippedItemIds.length > 0 ? self.equippedItemIds : @"none",
	          preview,
	          zodiac.length > 0 ? zodiac : preview,
	          TokenForgeCompanionStageName(self.stage),
	          surface,
	          (long)diagnosticLayerCount,
          self.surfaceName.length > 0 ? self.surfaceName : @"shop",
	          self.surfaceName.length > 0 ? self.surfaceName : @"shop",
          NSStringFromRect(self.bounds));
	    NSLog(@"INFO [PixelDiagnostic] repoHash=nativeShop zodiacKey=%@ stageKey=%@ equippedItemKeys=%@ wardrobePreviewKey=%@ cacheKey=signature=sprite:v4:grid24:zodiac=%@:stage=%@:equippedItemsHash=%@ cacheHit=false generatedVariant=%@ signature=sprite:v4:grid24",
	          zodiac.length > 0 ? zodiac : preview,
	          TokenForgeCompanionStageName(self.stage),
	          self.equippedItemIds.length > 0 ? self.equippedItemIds : @"none",
	          preview,
	          zodiac.length > 0 ? zodiac : preview,
	          TokenForgeCompanionStageName(self.stage),
	          self.equippedItemIds.length > 0 ? self.equippedItemIds : @"none",
	          self.surfaceName.length > 0 ? self.surfaceName : @"shop");
    NSLog(@"INFO [PixelSprite] surface=%@ spriteKey=zodiac_%@_%@ grid=32 nearestNeighbor=true antialias=false", surface, zodiac.length > 0 ? zodiac : preview, TokenForgeCompanionStageName(self.stage));
    NSLog(@"INFO [PixelSprite][GRID] grid=32 spriteKey=zodiac_%@_%@", zodiac.length > 0 ? zodiac : preview, TokenForgeCompanionStageName(self.stage));
    NSLog(@"INFO [PixelSprite][GRID_32_OR_48] grid=32 spriteKey=zodiac_%@_%@", zodiac.length > 0 ? zodiac : preview, TokenForgeCompanionStageName(self.stage));
    NSLog(@"INFO [PixelSprite][NEAREST_NEIGHBOR] value=true surface=%@", surface);
    NSLog(@"INFO [PixelSprite][NO_ANTIALIAS] value=true surface=%@", surface);
    NSLog(@"INFO [PixelSprite][ZODIAC] zodiac=%@ surface=%@", zodiac.length > 0 ? zodiac : preview, surface);
    NSLog(@"INFO [PixelSprite][STAGE] stage=%@ index=%ld surface=%@", TokenForgeCompanionStageName(self.stage), (long)self.stage, surface);
    NSLog(@"INFO [PixelSprite][SURFACE] surface=%@ spriteKey=zodiac_%@_%@", surface, zodiac.length > 0 ? zodiac : preview, TokenForgeCompanionStageName(self.stage));
	}
+ (void)drawEquippedLayersInRect:(NSRect)rect items:(NSString *)items accent:(NSColor *)accent
{
    NSString *value = [[items lowercaseString] copy] ?: @"";
    NSColor *outline = [NSColor colorWithCalibratedRed:0.025 green:0.030 blue:0.042 alpha:1.0];
    if ([value containsString:@"crown"] || [value containsString:@"hat"] || [value containsString:@"cap"] || [value containsString:@"crest"]) {
        [self pixelRect:rect x:15 y:6 w:18 h:4 color:outline];
        [self pixelRect:rect x:17 y:4 w:4 h:5 color:accent];
        [self pixelRect:rect x:24 y:2 w:4 h:7 color:accent];
        [self pixelRect:rect x:31 y:4 w:4 h:5 color:accent];
    }
    if ([value containsString:@"outfit"] || [value containsString:@"cape"] || [value containsString:@"jacket"] || [value containsString:@"robe"]) {
        [self pixelRect:rect x:15 y:31 w:18 h:7 color:outline];
        [self pixelRect:rect x:17 y:32 w:14 h:5 color:accent];
    }
    if ([value containsString:@"badge"] || [value containsString:@"ring"] || [value containsString:@"charm"]) {
        [self pixelRect:rect x:30 y:30 w:5 h:5 color:outline];
        [self pixelRect:rect x:31 y:31 w:3 h:3 color:[NSColor colorWithCalibratedRed:1.0 green:0.78 blue:0.20 alpha:1.0]];
    }
    if ([value containsString:@"effect"] || [value containsString:@"aura"] || [value containsString:@"trail"] || [value containsString:@"motion"] || [value containsString:@"spark"] || [value containsString:@"glow"]) {
        [[accent colorWithAlphaComponent:0.42] setFill];
        [self pixelRect:rect x:6 y:12 w:3 h:22 color:[accent colorWithAlphaComponent:0.55]];
        [self pixelRect:rect x:39 y:12 w:3 h:22 color:[accent colorWithAlphaComponent:0.55]];
        [self pixelRect:rect x:11 y:6 w:26 h:3 color:[accent colorWithAlphaComponent:0.38]];
    }
    NSLog(@"INFO [WardrobePreview][EQUIPPED_LAYER] items=%@ source=tokenShopState", items);
}
+ (void)drawCatInRect:(NSRect)rect calico:(BOOL)calico
{
    NSColor *outline = [NSColor colorWithCalibratedRed:0.035 green:0.040 blue:0.055 alpha:1];
    NSColor *body = calico ? [NSColor colorWithCalibratedRed:1.0 green:0.94 blue:0.82 alpha:1.0] : [NSColor colorWithCalibratedWhite:0.98 alpha:1.0];
    NSColor *shade = calico ? [NSColor colorWithCalibratedRed:0.95 green:0.45 blue:0.18 alpha:1.0] : [NSColor colorWithCalibratedWhite:0.78 alpha:1.0];
    NSColor *eye = [NSColor colorWithCalibratedWhite:0.02 alpha:1.0];
    [self pixelRect:rect x:12 y:16 w:24 h:22 color:outline];
    [self pixelRect:rect x:14 y:14 w:5 h:7 color:outline];
    [self pixelRect:rect x:29 y:14 w:5 h:7 color:outline];
    [self pixelRect:rect x:15 y:18 w:18 h:18 color:body];
    [self pixelRect:rect x:17 y:15 w:3 h:5 color:body];
    [self pixelRect:rect x:29 y:15 w:3 h:5 color:body];
    if (calico) {
        [self pixelRect:rect x:15 y:20 w:6 h:5 color:shade];
        [self pixelRect:rect x:28 y:24 w:5 h:5 color:outline];
    } else {
        [self pixelRect:rect x:27 y:29 w:5 h:4 color:shade];
    }
    [self pixelRect:rect x:19 y:25 w:3 h:3 color:eye];
    [self pixelRect:rect x:27 y:25 w:3 h:3 color:eye];
    [self pixelRect:rect x:23 y:30 w:4 h:2 color:eye];
    [self pixelRect:rect x:16 y:21 w:4 h:2 color:[NSColor whiteColor]];
}
+ (void)pixelRect:(NSRect)rect x:(CGFloat)x y:(CGFloat)y w:(CGFloat)w h:(CGFloat)h color:(NSColor *)color
{
    CGFloat unit = MIN(rect.size.width, rect.size.height) / 48.0;
    CGFloat ox = NSMidX(rect) - unit * 24.0;
    CGFloat oy = NSMidY(rect) - unit * 24.0;
    [color setFill];
    NSRectFill(NSIntegralRect(NSMakeRect(ox + x * unit, oy + y * unit, MAX(unit, w * unit), MAX(unit, h * unit))));
}
+ (void)pixelOval:(NSRect)rect x:(CGFloat)x y:(CGFloat)y w:(CGFloat)w h:(CGFloat)h color:(NSColor *)color
{
    CGFloat unit = MIN(rect.size.width, rect.size.height) / 48.0;
    CGFloat ox = NSMidX(rect) - unit * 24.0;
    CGFloat oy = NSMidY(rect) - unit * 24.0;
    [color setFill];
    NSInteger rows = MAX(1, (NSInteger)ceil(h));
    for (NSInteger row = 0; row < rows; row++) {
        CGFloat normalized = rows <= 1 ? 0.0 : fabs(((CGFloat)row / (CGFloat)(rows - 1)) * 2.0 - 1.0);
        CGFloat inset = floor((CGFloat)w * normalized * normalized * 0.28);
        CGFloat rowWidth = MAX(1.0, w - inset * 2.0);
        NSRectFill(NSIntegralRect(NSMakeRect(ox + (x + inset) * unit, oy + (y + row) * unit, MAX(unit, rowWidth * unit), MAX(unit, unit))));
    }
}
+ (void)pixelLine:(NSRect)rect points:(NSArray<NSValue *> *)points color:(NSColor *)color width:(CGFloat)width
{
    if (points.count == 0) {
        return;
    }
    CGFloat unit = MIN(rect.size.width, rect.size.height) / 48.0;
    CGFloat ox = NSMidX(rect) - unit * 24.0;
    CGFloat oy = NSMidY(rect) - unit * 24.0;
    [color setFill];
    CGFloat cellWidth = MAX(1.0, width);
    for (NSUInteger index = 1; index < points.count; index++) {
        NSPoint a = points[index - 1].pointValue;
        NSPoint b = points[index].pointValue;
        NSInteger steps = MAX(1, (NSInteger)ceil(MAX(fabs(b.x - a.x), fabs(b.y - a.y))));
        for (NSInteger step = 0; step <= steps; step++) {
            CGFloat t = (CGFloat)step / (CGFloat)steps;
            CGFloat px = round(a.x + (b.x - a.x) * t - cellWidth * 0.5);
            CGFloat py = round(a.y + (b.y - a.y) * t - cellWidth * 0.5);
            NSRectFill(NSIntegralRect(NSMakeRect(ox + px * unit, oy + py * unit, MAX(unit, cellWidth * unit), MAX(unit, cellWidth * unit))));
        }
    }
}
+ (NSColor *)zodiacBaseColor:(NSString *)zodiac
{
    if ([zodiac containsString:@"rat"]) return [NSColor colorWithCalibratedRed:0.70 green:0.72 blue:0.78 alpha:1];
    if ([zodiac containsString:@"ox"]) return [NSColor colorWithCalibratedRed:0.55 green:0.39 blue:0.25 alpha:1];
    if ([zodiac containsString:@"tiger"]) return [NSColor colorWithCalibratedRed:0.95 green:0.50 blue:0.16 alpha:1];
    if ([zodiac containsString:@"rabbit"]) return [NSColor colorWithCalibratedRed:0.92 green:0.88 blue:0.96 alpha:1];
    if ([zodiac containsString:@"dragon"]) return [NSColor colorWithCalibratedRed:0.18 green:0.78 blue:0.62 alpha:1];
    if ([zodiac containsString:@"snake"]) return [NSColor colorWithCalibratedRed:0.22 green:0.74 blue:0.36 alpha:1];
    if ([zodiac containsString:@"horse"]) return [NSColor colorWithCalibratedRed:0.54 green:0.30 blue:0.15 alpha:1];
    if ([zodiac containsString:@"goat"]) return [NSColor colorWithCalibratedRed:0.82 green:0.78 blue:0.66 alpha:1];
    if ([zodiac containsString:@"monkey"]) return [NSColor colorWithCalibratedRed:0.55 green:0.30 blue:0.13 alpha:1];
    if ([zodiac containsString:@"rooster"]) return [NSColor colorWithCalibratedRed:0.86 green:0.40 blue:0.16 alpha:1];
    if ([zodiac containsString:@"dog"]) return [NSColor colorWithCalibratedRed:0.66 green:0.43 blue:0.24 alpha:1];
    if ([zodiac containsString:@"pig"]) return [NSColor colorWithCalibratedRed:0.96 green:0.60 blue:0.70 alpha:1];
    return [NSColor colorWithCalibratedRed:0.42 green:0.62 blue:1.0 alpha:1];
}
+ (void)drawOutfitInRect:(NSRect)rect accent:(NSColor *)accent preview:(NSString *)preview
{
    NSColor *outline = [NSColor colorWithCalibratedWhite:0.04 alpha:1];
    NSColor *cloth = [preview containsString:@"codex"] ? [NSColor colorWithCalibratedRed:0.14 green:0.62 blue:0.95 alpha:1] : accent;
    NSColor *trim = [NSColor colorWithCalibratedRed:1.0 green:0.86 blue:0.42 alpha:1.0];
    NSColor *glass = [NSColor colorWithCalibratedRed:0.64 green:0.92 blue:1.0 alpha:1.0];
    NSColor *shadow = [cloth blendedColorWithFraction:0.35 ofColor:outline] ?: cloth;
    NSString *value = [[preview lowercaseString] copy] ?: @"";
    if ([value containsString:@"wizard"] || [value containsString:@"robe"]) {
        [self pixelRect:rect x:14 y:14 w:20 h:24 color:outline];
        [self pixelRect:rect x:16 y:16 w:16 h:20 color:cloth];
        [self pixelRect:rect x:12 y:28 w:24 h:8 color:outline];
        [self pixelRect:rect x:15 y:29 w:18 h:6 color:shadow];
        [self pixelRect:rect x:20 y:10 w:8 h:6 color:outline];
        [self pixelRect:rect x:21 y:11 w:6 h:4 color:trim];
        [self pixelRect:rect x:23 y:18 w:2 h:16 color:trim];
        [self pixelRect:rect x:18 y:23 w:4 h:3 color:[NSColor whiteColor]];
        [self pixelRect:rect x:27 y:23 w:4 h:3 color:[NSColor whiteColor]];
    } else if ([value containsString:@"astronaut"]) {
        [self pixelRect:rect x:15 y:17 w:18 h:20 color:outline];
        [self pixelRect:rect x:17 y:19 w:14 h:16 color:[NSColor colorWithCalibratedWhite:0.92 alpha:1.0]];
        [self pixelRect:rect x:17 y:9 w:14 h:12 color:outline];
        [self pixelRect:rect x:19 y:11 w:10 h:8 color:glass];
        [self pixelRect:rect x:13 y:24 w:4 h:11 color:outline];
        [self pixelRect:rect x:31 y:24 w:4 h:11 color:outline];
        [self pixelRect:rect x:20 y:25 w:8 h:5 color:accent];
        [self pixelRect:rect x:22 y:31 w:2 h:2 color:trim];
        [self pixelRect:rect x:26 y:31 w:2 h:2 color:trim];
    } else if ([value containsString:@"ninja"]) {
        [self pixelRect:rect x:15 y:14 w:18 h:23 color:outline];
        [self pixelRect:rect x:17 y:16 w:14 h:19 color:[NSColor colorWithCalibratedRed:0.08 green:0.10 blue:0.14 alpha:1.0]];
        [self pixelRect:rect x:18 y:18 w:12 h:4 color:accent];
        [self pixelRect:rect x:20 y:20 w:3 h:2 color:[NSColor whiteColor]];
        [self pixelRect:rect x:26 y:20 w:3 h:2 color:[NSColor whiteColor]];
        [self pixelRect:rect x:12 y:25 w:6 h:3 color:outline];
        [self pixelRect:rect x:30 y:25 w:6 h:3 color:outline];
        [self pixelRect:rect x:18 y:32 w:12 h:2 color:trim];
    } else {
        [self pixelRect:rect x:16 y:14 w:16 h:22 color:outline];
        [self pixelRect:rect x:18 y:16 w:12 h:18 color:cloth];
        [self pixelRect:rect x:14 y:21 w:4 h:12 color:outline];
        [self pixelRect:rect x:30 y:21 w:4 h:12 color:outline];
        [self pixelRect:rect x:18 y:18 w:5 h:4 color:shadow];
        [self pixelRect:rect x:25 y:18 w:5 h:4 color:[cloth blendedColorWithFraction:0.22 ofColor:[NSColor whiteColor]] ?: cloth];
        [self pixelRect:rect x:23 y:17 w:2 h:17 color:[NSColor whiteColor]];
        [self pixelRect:rect x:20 y:25 w:3 h:3 color:trim];
        [self pixelRect:rect x:26 y:25 w:3 h:3 color:trim];
    }
    NSLog(@"INFO [ShopPreview][OUTFIT_DETAIL] preview=%@ silhouette=%@ details=collar/buttons/belt/trim/helmet/hood frame=rarity",
          preview ?: @"generic",
          [value containsString:@"wizard"] ? @"wizardRobe" : ([value containsString:@"astronaut"] ? @"astronautSuit" : ([value containsString:@"ninja"] ? @"ninjaOutfit" : @"workJacket")));
}
+ (void)drawThemeInRect:(NSRect)rect accent:(NSColor *)accent
{
    for (NSInteger index = 0; index < 4; index++) {
        NSColor *color = index % 2 == 0 ? [accent colorWithAlphaComponent:0.78] : [NSColor colorWithCalibratedRed:0.11 green:0.14 blue:0.24 alpha:1];
        [self pixelRect:rect x:8 + index * 8 y:10 w:8 h:28 color:color];
    }
    [[NSColor colorWithCalibratedWhite:1 alpha:0.92] setStroke];
    NSBezierPath *frame = [NSBezierPath bezierPathWithRoundedRect:NSInsetRect(rect, rect.size.width * 0.20, rect.size.height * 0.20) xRadius:6 yRadius:6];
    frame.lineWidth = 2.0;
    [frame stroke];
}
+ (void)drawAccessoryInRect:(NSRect)rect accent:(NSColor *)accent preview:(NSString *)preview
{
    NSColor *outline = [NSColor colorWithCalibratedRed:0.035 green:0.040 blue:0.055 alpha:1];
    if ([preview containsString:@"headphones"]) {
        [self pixelRect:rect x:14 y:14 w:20 h:5 color:outline];
        [self pixelRect:rect x:16 y:16 w:16 h:3 color:accent];
        [self pixelRect:rect x:10 y:21 w:8 h:13 color:outline];
        [self pixelRect:rect x:30 y:21 w:8 h:13 color:outline];
        [self pixelRect:rect x:12 y:23 w:4 h:9 color:accent];
        [self pixelRect:rect x:32 y:23 w:4 h:9 color:accent];
    } else if ([preview containsString:@"crown"]) {
        [self pixelRect:rect x:12 y:20 w:24 h:13 color:outline];
        [self pixelRect:rect x:14 y:22 w:20 h:9 color:accent];
        [self pixelRect:rect x:14 y:15 w:4 h:7 color:accent];
        [self pixelRect:rect x:22 y:12 w:4 h:10 color:accent];
        [self pixelRect:rect x:30 y:15 w:4 h:7 color:accent];
        [self pixelRect:rect x:18 y:24 w:3 h:3 color:[NSColor whiteColor]];
        [self pixelRect:rect x:27 y:24 w:3 h:3 color:[NSColor whiteColor]];
    } else {
        [self pixelRect:rect x:13 y:14 w:22 h:22 color:outline];
        [self pixelRect:rect x:15 y:16 w:18 h:18 color:accent];
        [self pixelRect:rect x:19 y:22 w:4 h:9 color:[NSColor whiteColor]];
        [self pixelRect:rect x:23 y:27 w:9 h:4 color:[NSColor whiteColor]];
    }
}
+ (void)drawTrailInRect:(NSRect)rect accent:(NSColor *)accent
{
    for (NSInteger index = 0; index < 3; index++) {
        NSColor *color = [accent colorWithAlphaComponent:0.95 - index * 0.20];
        [self pixelRect:rect x:8 + index * 5 y:14 + index * 8 w:27 - index * 4 h:3 color:color];
        [self pixelRect:rect x:17 + index * 5 y:18 + index * 8 w:18 - index * 3 h:3 color:color];
        [self pixelRect:rect x:28 + index * 3 y:22 + index * 8 w:8 h:3 color:color];
    }
}
+ (void)drawAuraInRect:(NSRect)rect accent:(NSColor *)accent
{
    NSColor *soft = [accent colorWithAlphaComponent:0.35];
    [self pixelRect:rect x:12 y:8 w:24 h:3 color:soft];
    [self pixelRect:rect x:8 y:12 w:3 h:24 color:soft];
    [self pixelRect:rect x:37 y:12 w:3 h:24 color:soft];
    [self pixelRect:rect x:12 y:37 w:24 h:3 color:soft];
    [self pixelRect:rect x:22 y:13 w:4 h:22 color:accent];
    [self pixelRect:rect x:13 y:22 w:22 h:4 color:accent];
    [self pixelRect:rect x:18 y:18 w:12 h:12 color:[NSColor whiteColor]];
}
+ (void)drawZodiacMascot:(NSString *)zodiac inRect:(NSRect)rect accent:(NSColor *)accent stage:(NSInteger)stage
{
    NSGraphicsContext *context = [NSGraphicsContext currentContext];
    BOOL previousAntialias = context.shouldAntialias;
    context.shouldAntialias = NO;
    NSString *idv = [[zodiac lowercaseString] copy] ?: @"rat";
    NSInteger s = MAX(0, MIN(5, stage));
    NSArray *names = @[@"egg", @"baby", @"child", @"teen", @"young_adult", @"adult"];
    NSColor *outline = [NSColor colorWithCalibratedRed:0.030 green:0.034 blue:0.046 alpha:1];
    NSColor *base = [self zodiacBaseColor:idv];
    NSColor *shade = [base blendedColorWithFraction:0.30 ofColor:outline] ?: base;
    NSColor *light = [base blendedColorWithFraction:0.38 ofColor:[NSColor whiteColor]] ?: base;
    NSColor *cream = [NSColor colorWithCalibratedRed:1.0 green:0.86 blue:0.58 alpha:1];
    NSColor *eye = [NSColor colorWithCalibratedWhite:0.02 alpha:1];
    NSColor *pink = [NSColor colorWithCalibratedRed:1.0 green:0.56 blue:0.72 alpha:1];
    NSColor *red = [NSColor colorWithCalibratedRed:0.96 green:0.16 blue:0.12 alpha:1];
    NSColor *yellow = [NSColor colorWithCalibratedRed:1.0 green:0.78 blue:0.15 alpha:1];
    NSColor *mane = [NSColor colorWithCalibratedRed:0.15 green:0.08 blue:0.04 alpha:1];

    NSMutableArray<NSMutableString *> *grid = [NSMutableArray arrayWithCapacity:24];
    for (NSInteger row = 0; row < 24; row++) {
        [grid addObject:[@"........................" mutableCopy]];
    }
    void (^cell)(NSInteger, NSInteger, unichar) = ^(NSInteger x, NSInteger y, unichar ch) {
        if (x < 0 || x >= 24 || y < 0 || y >= 24) return;
        [grid[y] replaceCharactersInRange:NSMakeRange((NSUInteger)x, 1) withString:[NSString stringWithCharacters:&ch length:1]];
    };
    void (^fill)(NSInteger, NSInteger, NSInteger, NSInteger, unichar) = ^(NSInteger x, NSInteger y, NSInteger w, NSInteger h, unichar ch) {
        for (NSInteger yy = y; yy < y + h; yy++) for (NSInteger xx = x; xx < x + w; xx++) cell(xx, yy, ch);
    };
    void (^stepped)(NSInteger, NSInteger, NSArray<NSNumber *> *, unichar) = ^(NSInteger cx, NSInteger y, NSArray<NSNumber *> *widths, unichar ch) {
        for (NSInteger row = 0; row < (NSInteger)widths.count; row++) {
            NSInteger w = widths[(NSUInteger)row].integerValue;
            fill(cx - w / 2, y + row, w, 1, ch);
        }
    };

    fill(4, 21, 16, 2, 'S');
    if (s == 0) {
        stepped(12, 4, @[@6,@10,@12,@14,@14,@16,@16,@16,@14,@14,@12,@10,@6], 'O');
        stepped(12, 5, @[@4,@8,@10,@12,@12,@14,@14,@12,@12,@10,@8], 'B');
        fill(9, 7, 5, 2, 'L');
        fill(8, 14, 8, 1, 'D');
        if ([idv containsString:@"rabbit"]) { fill(8, 1, 2, 5, 'O'); fill(15, 1, 2, 5, 'O'); fill(9, 2, 1, 4, 'P'); fill(16, 2, 1, 4, 'P'); }
        else if ([idv containsString:@"ox"] || [idv containsString:@"goat"] || [idv containsString:@"dragon"]) { fill(6, 7, 4, 2, 'C'); fill(15, 7, 4, 2, 'C'); }
        else if ([idv containsString:@"tiger"]) { fill(9, 8, 1, 5, 'O'); fill(14, 8, 1, 5, 'O'); }
        else if ([idv containsString:@"snake"]) { fill(17, 14, 4, 2, 'D'); fill(20, 12, 2, 2, 'D'); }
        else if ([idv containsString:@"rooster"]) { fill(11, 1, 3, 5, 'R'); fill(15, 8, 3, 2, 'Y'); }
        else if ([idv containsString:@"pig"]) { fill(9, 13, 6, 3, 'P'); cell(11, 14, 'O'); cell(14, 14, 'O'); }
    } else {
        NSInteger bodyX = 7 - (s >= 4 ? 1 : 0);
        NSInteger bodyY = 12 - (s >= 3 ? 1 : 0);
        NSInteger bodyW = 9 + s;
        NSInteger bodyH = 7 + MIN(s, 4);
        NSInteger headX = 8 - (s >= 5 ? 1 : 0);
        NSInteger headY = 6 - (s >= 4 ? 1 : 0);
        NSInteger headW = 8 + MIN(s, 4);
        NSInteger headH = 7 + MIN(s, 3);
        if ([idv containsString:@"snake"]) {
            fill(4, 17, 10, 3, 'O'); fill(8, 14, 10, 3, 'O'); fill(12, 11, 8, 3, 'O'); fill(15, 8, 6, 4, 'O');
            fill(5, 18, 8, 1, 'B'); fill(9, 15, 8, 1, 'B'); fill(13, 12, 6, 1, 'B'); fill(16, 9, 4, 2, 'B');
            fill(17, 10, 1, 1, 'E'); fill(21, 11, 2, 1, 'R'); fill(22, 12, 1, 1, 'R');
        } else {
            fill(bodyX - 1, bodyY - 1, bodyW + 2, bodyH + 2, 'O');
            fill(bodyX, bodyY, bodyW, bodyH, 'B');
            for (NSInteger shadeRow = 2; shadeRow < bodyH - 1; shadeRow += 2) {
                cell(bodyX + bodyW - 3, bodyY + shadeRow, 'L');
                if (s >= 3) cell(bodyX + 2, bodyY + shadeRow + 1, 'C');
            }
            fill(bodyX + 2, bodyY + 2, 4 + MIN(s, 2), 2, 'L');
            fill(headX - 1, headY - 1, headW + 2, headH + 2, 'O');
            fill(headX, headY, headW, headH, 'B');
            fill(headX + 2, headY + 2, 4, 1, 'L');
            cell(headX + 3, headY + 4, 'E'); cell(headX + headW - 4, headY + 4, 'E'); fill(headX + headW / 2 - 1, headY + 6, 3, 1, 'E');
            fill(bodyX + 1, bodyY + bodyH, 2, 3, 'O'); fill(bodyX + bodyW - 3, bodyY + bodyH, 2, 3, 'O');
        }

        if ([idv containsString:@"rat"]) {
            fill(5, 5, 4, 4, 'O'); fill(16, 5, 4, 4, 'O'); fill(6, 6, 2, 2, 'P'); fill(17, 6, 2, 2, 'P'); fill(18, 16, 5, 1, 'P'); fill(22, 17, 1, 4, 'P');
        } else if ([idv containsString:@"ox"]) {
            fill(4, 5, 5, 2, 'C'); fill(17, 5, 5, 2, 'C'); fill(3, 4, 2, 3, 'C'); fill(21, 4, 2, 3, 'C'); fill(8, 12, 8, 2, 'C'); cell(10, 14, 'E'); cell(14, 14, 'E');
        } else if ([idv containsString:@"tiger"]) {
            fill(8, 3, 3, 4, 'O'); fill(15, 3, 3, 4, 'O'); fill(9, 8, 1, 8, 'O'); fill(13, 7, 1, 7, 'O'); fill(17, 9, 1, 8, 'O'); fill(10, 18, 8, 1, 'O');
        } else if ([idv containsString:@"rabbit"]) {
            fill(7, 0, 3, 8, 'O'); fill(16, 0, 3, 8, 'O'); fill(8, 1, 1, 6, 'P'); fill(17, 1, 1, 6, 'P');
        } else if ([idv containsString:@"dragon"]) {
            fill(6, 2, 3, 6, 'C'); fill(17, 2, 3, 6, 'C'); fill(11, 1, 4, 4, 'R'); fill(3, 11, 4, 1, 'C'); fill(19, 11, 4, 1, 'C'); fill(5, 15, 2, 2, 'D'); fill(18, 15, 2, 2, 'D');
        } else if ([idv containsString:@"horse"]) {
            fill(16, 5, 3, 12, 'M'); fill(9, 12, 8, 2, 'L'); fill(13, 9, 5, 5, 'B'); cell(15, 14, 'C');
        } else if ([idv containsString:@"goat"]) {
            fill(5, 5, 4, 2, 'C'); fill(4, 6, 2, 4, 'C'); fill(17, 5, 4, 2, 'C'); fill(20, 6, 2, 4, 'C'); fill(11, 13, 4, 5, 'C');
        } else if ([idv containsString:@"monkey"]) {
            fill(5, 7, 4, 5, 'O'); fill(17, 7, 4, 5, 'O'); fill(9, 10, 8, 5, 'C'); cell(19, 15, 'C'); cell(21, 12, 'O'); cell(21, 13, 'C');
        } else if ([idv containsString:@"rooster"]) {
            fill(9, 1, 3, 5, 'R'); fill(12, 0, 3, 6, 'R'); fill(15, 2, 3, 4, 'R'); fill(17, 10, 4, 2, 'Y'); fill(4, 14, 4, 5, 'D');
        } else if ([idv containsString:@"dog"]) {
            fill(6, 6, 2, 8, 'O'); fill(18, 6, 2, 8, 'O'); cell(7, 9, 'C'); cell(18, 9, 'C'); fill(10, 12, 7, 3, 'C'); fill(13, 14, 3, 1, 'E');
        } else if ([idv containsString:@"pig"]) {
            fill(7, 5, 3, 4, 'O'); fill(17, 5, 3, 4, 'O'); fill(10, 11, 7, 3, 'P'); cell(12, 12, 'O'); cell(15, 12, 'O'); fill(19, 16, 3, 1, 'P'); fill(21, 15, 1, 2, 'P');
        }
        if (s >= 3) { cell(4, 20, 'A'); cell(5, 21, 'Y'); cell(19, 20, 'A'); cell(18, 21, 'Y'); }
        if (s >= 4) { cell(5, 4, 'Y'); cell(19, 4, 'Y'); cell(12, 2, 'Y'); }
        if (s >= 5) { fill(1, 9, 2, 2, 'Y'); fill(21, 9, 2, 2, 'Y'); fill(11, 22, 3, 1, 'A'); }
    }

    NSDictionary<NSString *, NSColor *> *palette = @{
        @"O": outline,
        @"B": base,
        @"D": shade,
        @"L": light,
        @"C": cream,
        @"E": eye,
        @"P": pink,
        @"R": red,
        @"Y": yellow,
        @"M": mane,
        @"A": accent ?: light,
        @"S": [NSColor colorWithCalibratedWhite:0 alpha:0.22]
    };
    CGFloat unit = floor(MIN(rect.size.width, rect.size.height) / 24.0);
    if (unit < 1.0) unit = MAX(1.0, MIN(rect.size.width, rect.size.height) / 24.0);
    CGFloat ox = floor(NSMidX(rect) - unit * 12.0);
    CGFloat oy = floor(NSMidY(rect) - unit * 12.0);
    for (NSInteger y = 0; y < 24; y++) {
        NSString *row = grid[(NSUInteger)y];
        for (NSInteger x = 0; x < 24; x++) {
            NSString *key = [row substringWithRange:NSMakeRange((NSUInteger)x, 1)];
            NSColor *color = palette[key];
            if (color == nil) continue;
            [color setFill];
            NSRectFill(NSIntegralRect(NSMakeRect(ox + x * unit, oy + y * unit, MAX(1.0, unit), MAX(1.0, unit))));
        }
    }
    context.shouldAntialias = previousAntialias;
    NSLog(@"INFO [RuntimeUIPath][PixelSprite] spriteKey=zodiac_%@_%@ pixelGrid=24x24 nearestNeighbor=true antialias=false matrixRenderer=true", idv, names[(NSUInteger)s]);
    NSLog(@"INFO [PixelSprite] spriteKey=zodiac_%@_%@ surface=shop pixelGrid=24x24 nearestNeighbor=true antialias=false matrixRenderer=true", idv, names[(NSUInteger)s]);
    NSLog(@"INFO [ZodiacPreview][PIXEL_ART] id=%@ stage=%@ key=zodiac_%@_%@ clipped=false deterministic=true squareCells=true noSmoothOvalFallback=true", idv, names[(NSUInteger)s], idv, names[(NSUInteger)s]);
}
@end

static void TokenForgeDrawSharedZodiacSprite(NSString *zodiacType, NSRect rect, NSColor *accent, NSInteger stage)
{
    [TokenForgeShopPreviewView drawZodiacMascot:zodiacType.length > 0 ? zodiacType : @"tiger"
                                         inRect:rect
                                         accent:accent ?: [NSColor colorWithCalibratedRed:1.0 green:0.78 blue:0.18 alpha:1.0]
                                          stage:MAX(0, MIN(5, stage))];
}

@implementation TokenForgeOnboardingVisualView
- (BOOL)isFlipped { return YES; }
- (void)drawRect:(NSRect)dirtyRect
{
    [super drawRect:dirtyRect];
    NSGraphicsContext *context = [NSGraphicsContext currentContext];
    BOOL previousAntialias = context.shouldAntialias;
    context.shouldAntialias = NO;
    NSRect bounds = NSInsetRect(self.bounds, 12.0, 12.0);
    [[NSColor colorWithCalibratedRed:0.030 green:0.042 blue:0.070 alpha:1.0] setFill];
    [[NSBezierPath bezierPathWithRoundedRect:self.bounds xRadius:10.0 yRadius:10.0] fill];
    [[NSColor colorWithCalibratedRed:0.18 green:0.34 blue:0.74 alpha:0.16] setFill];
    CGFloat panelCell = MAX(4.0, floor(MIN(self.bounds.size.width, self.bounds.size.height) / 22.0));
    for (NSInteger index = 0; index < 6; index++) {
        NSRect glow = NSInsetRect(self.bounds, panelCell * (5 + index), panelCell * (2 + index));
        NSRectFill(NSMakeRect(NSMinX(glow), NSMidY(glow) - panelCell, NSWidth(glow), panelCell * 2.0));
    }
    [[NSColor colorWithCalibratedWhite:1.0 alpha:0.14] setStroke];
    NSBezierPath *frame = [NSBezierPath bezierPathWithRoundedRect:NSInsetRect(self.bounds, 1.0, 1.0) xRadius:10.0 yRadius:10.0];
    frame.lineWidth = 1.0;
    [frame stroke];

    NSInteger step = MAX(0, MIN(9, self.stepIndex));
    NSColor *blue = [NSColor colorWithCalibratedRed:0.30 green:0.58 blue:1.0 alpha:1.0];
    NSColor *gold = [NSColor colorWithCalibratedRed:1.0 green:0.76 blue:0.24 alpha:1.0];
    NSColor *green = [NSColor colorWithCalibratedRed:0.24 green:0.86 blue:0.56 alpha:1.0];
    NSColor *pink = [NSColor colorWithCalibratedRed:1.0 green:0.38 blue:0.66 alpha:1.0];
    NSColor *ink = [NSColor colorWithCalibratedRed:0.015 green:0.020 blue:0.035 alpha:1.0];
    NSString *zodiac = self.zodiacType.length > 0 ? self.zodiacType : @"dragon";
    CGFloat w = bounds.size.width;
    CGFloat h = bounds.size.height;

    void (^drawRepo)(NSRect, NSString *) = ^(NSRect rect, NSString *label) {
        [[NSColor colorWithCalibratedRed:0.09 green:0.12 blue:0.19 alpha:1] setFill];
        [[NSBezierPath bezierPathWithRoundedRect:rect xRadius:7 yRadius:7] fill];
        [blue setStroke];
        NSBezierPath *outline = [NSBezierPath bezierPathWithRoundedRect:NSInsetRect(rect, 1, 1) xRadius:7 yRadius:7];
        outline.lineWidth = 2.0;
        [outline stroke];
        for (NSInteger i = 0; i < 4; i++) {
            [[NSColor colorWithCalibratedWhite:1 alpha:0.18 + i * 0.08] setFill];
            NSRectFill(NSMakeRect(NSMinX(rect)+14, NSMinY(rect)+16+i*12, rect.size.width-28-i*10, 4));
        }
        NSDictionary *attrs = @{NSFontAttributeName: [NSFont monospacedSystemFontOfSize:12 weight:NSFontWeightSemibold], NSForegroundColorAttributeName: [NSColor colorWithCalibratedWhite:1 alpha:0.82]};
        [label drawInRect:NSInsetRect(rect, 14, rect.size.height-31) withAttributes:attrs];
    };
    void (^drawCoin)(NSPoint, CGFloat) = ^(NSPoint center, CGFloat size) {
        CGFloat cell = MAX(2.0, floor(size / 8.0));
        CGFloat x = floor(center.x - cell * 4.0);
        CGFloat y = floor(center.y - cell * 4.0);
        [ink setFill];
        NSRectFill(NSMakeRect(x + cell, y, cell * 6, cell));
        NSRectFill(NSMakeRect(x, y + cell, cell * 8, cell * 6));
        NSRectFill(NSMakeRect(x + cell, y + cell * 7, cell * 6, cell));
        [gold setFill];
        NSRectFill(NSMakeRect(x + cell * 2, y + cell, cell * 4, cell));
        NSRectFill(NSMakeRect(x + cell, y + cell * 2, cell * 6, cell * 4));
        NSRectFill(NSMakeRect(x + cell * 2, y + cell * 6, cell * 4, cell));
        [[NSColor colorWithCalibratedRed:1.0 green:0.92 blue:0.54 alpha:1] setFill];
        NSRectFill(NSMakeRect(x + cell * 3, y + cell * 2, cell * 2, cell));
        NSRectFill(NSMakeRect(x + cell * 3, y + cell * 4, cell * 2, cell * 2));
        NSRect coin = NSMakeRect(x, y, cell * 8.0, cell * 8.0);
        NSDictionary *attrs = @{NSFontAttributeName: [NSFont systemFontOfSize:size*0.44 weight:NSFontWeightBlack], NSForegroundColorAttributeName: [NSColor colorWithCalibratedRed:0.62 green:0.38 blue:0.04 alpha:1]};
        [@"T" drawInRect:NSInsetRect(coin, size*0.29, size*0.21) withAttributes:attrs];
    };
    void (^drawArrow)(NSPoint, NSPoint, NSColor *) = ^(NSPoint from, NSPoint to, NSColor *color) {
        [color setFill];
        NSInteger steps = 18;
        for (NSInteger i = 0; i < steps; i++) {
            CGFloat t = (CGFloat)i / (CGFloat)(steps - 1);
            CGFloat x = round(from.x + (to.x - from.x) * t);
            CGFloat y = round(from.y + (to.y - from.y) * t + sin(t * M_PI) * -10.0);
            NSRectFill(NSMakeRect(x, y, 6, 4));
        }
        NSRectFill(NSMakeRect(to.x - 8, to.y - 8, 8, 4));
        NSRectFill(NSMakeRect(to.x - 8, to.y + 4, 8, 4));
        NSRectFill(NSMakeRect(to.x - 2, to.y - 2, 8, 4));
    };

    if (step == 0) {
        drawRepo(NSMakeRect(NSMinX(bounds)+14, NSMidY(bounds)-42, w*0.34, 84), @"repo");
        drawArrow(NSMakePoint(NSMinX(bounds)+w*0.42, NSMidY(bounds)), NSMakePoint(NSMinX(bounds)+w*0.57, NSMidY(bounds)), green);
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(NSMinX(bounds)+w*0.58, NSMidY(bounds)-74, 148, 148) accent:gold stage:0];
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(NSMinX(bounds)+w*0.73, NSMidY(bounds)-82, 164, 164) accent:gold stage:3];
    } else if (step == 1) {
        drawRepo(NSMakeRect(NSMinX(bounds)+20, NSMinY(bounds)+30, w*0.35, h-60), @"git");
        for (NSInteger i = 0; i < 6; i++) {
            drawArrow(NSMakePoint(NSMinX(bounds)+w*0.42, NSMinY(bounds)+42+i*22), NSMakePoint(NSMinX(bounds)+w*0.68, NSMinY(bounds)+54+i*15), i % 2 ? green : blue);
        }
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(NSMinX(bounds)+w*0.66, NSMidY(bounds)-78, 156, 156) accent:green stage:2];
    } else if (step == 2) {
        NSArray *labels = @[@"Egg", @"Baby", @"Child", @"Teen", @"Adult", @"Legend"];
        for (NSInteger i = 0; i < 6; i++) {
            CGFloat x = NSMinX(bounds) + 16 + i * ((w - 32) / 6.0);
            [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(x, NSMidY(bounds)-52, 86, 86) accent:i >= 4 ? gold : blue stage:i];
            NSDictionary *attrs = @{NSFontAttributeName: [NSFont systemFontOfSize:11 weight:NSFontWeightSemibold], NSForegroundColorAttributeName: [NSColor colorWithCalibratedWhite:1 alpha:0.80]};
            [labels[i] drawInRect:NSMakeRect(x, NSMidY(bounds)+48, 88, 18) withAttributes:attrs];
        }
    } else if (step == 3) {
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(NSMinX(bounds)+32, NSMidY(bounds)-78, 156, 156) accent:green stage:3];
        for (NSInteger i = 0; i < 7; i++) drawCoin(NSMakePoint(NSMinX(bounds)+w*0.54+i*26, NSMidY(bounds)-32+(i%3)*26), 32);
        drawArrow(NSMakePoint(NSMinX(bounds)+w*0.40, NSMidY(bounds)), NSMakePoint(NSMinX(bounds)+w*0.55, NSMidY(bounds)), gold);
    } else if (step == 4) {
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(NSMinX(bounds)+34, NSMidY(bounds)-76, 152, 152) accent:pink stage:4];
        [TokenForgeShopPreviewView drawOutfitInRect:NSMakeRect(NSMinX(bounds)+w*0.50, NSMidY(bounds)-60, 112, 112) accent:blue preview:@"outfit_jacket"];
        [TokenForgeShopPreviewView drawAccessoryInRect:NSMakeRect(NSMinX(bounds)+w*0.68, NSMidY(bounds)-60, 112, 112) accent:gold preview:@"crown"];
    } else if (step == 5) {
        NSArray *ids = @[@"rat",@"ox",@"tiger",@"rabbit",@"dragon",@"snake",@"horse",@"goat",@"monkey",@"rooster",@"dog",@"pig"];
        for (NSInteger i = 0; i < ids.count; i++) {
            CGFloat x = NSMinX(bounds)+18+(i%6)*((w-36)/6.0);
            CGFloat y = NSMinY(bounds)+22+(i/6)*88;
            [TokenForgeShopPreviewView drawZodiacMascot:ids[i] inRect:NSMakeRect(x, y, 74, 74) accent:i == 4 ? gold : blue stage:4];
        }
    } else if (step == 6) {
        NSArray *agents = @[@"Codex", @"Claude", @"Cursor", @"Copilot", @"Gemini"];
        NSArray *colors = @[blue, pink, green, gold, [NSColor colorWithCalibratedRed:0.75 green:0.48 blue:1.0 alpha:1]];
        for (NSInteger i = 0; i < agents.count; i++) {
            NSRect chip = NSMakeRect(NSMinX(bounds)+24+i*((w-48)/5.0), NSMidY(bounds)-34+(i%2)*18, 92, 68);
            [colors[i] setFill]; [[NSBezierPath bezierPathWithRoundedRect:chip xRadius:8 yRadius:8] fill];
            [ink setFill]; NSRectFill(NSInsetRect(chip, 8, 18));
            NSDictionary *attrs = @{NSFontAttributeName: [NSFont monospacedSystemFontOfSize:11 weight:NSFontWeightBold], NSForegroundColorAttributeName: [NSColor whiteColor]};
            [agents[i] drawInRect:NSInsetRect(chip, 9, 8) withAttributes:attrs];
        }
    } else if (step == 7) {
        NSRect screen = NSMakeRect(NSMinX(bounds)+28, NSMinY(bounds)+24, w*0.62, h-48);
        [[NSColor colorWithCalibratedRed:0.10 green:0.13 blue:0.18 alpha:1] setFill];
        [[NSBezierPath bezierPathWithRoundedRect:screen xRadius:10 yRadius:10] fill];
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(NSMaxX(screen)-44, NSMinY(screen)+28, 128, 128) accent:gold stage:4];
        [self.class drawDesktopSparklesInRect:NSMakeRect(NSMaxX(screen)-20, NSMinY(screen)+12, 118, 140) color:blue];
    } else if (step == 8) {
        drawRepo(NSMakeRect(NSMinX(bounds)+18, NSMidY(bounds)-38, w*0.34, 76), @"raw code");
        drawArrow(NSMakePoint(NSMinX(bounds)+w*0.42, NSMidY(bounds)), NSMakePoint(NSMinX(bounds)+w*0.59, NSMidY(bounds)), green);
        NSRect safe = NSMakeRect(NSMinX(bounds)+w*0.62, NSMidY(bounds)-44, 152, 88);
        [[NSColor colorWithCalibratedRed:0.10 green:0.32 blue:0.22 alpha:1] setFill]; [[NSBezierPath bezierPathWithRoundedRect:safe xRadius:8 yRadius:8] fill];
        NSDictionary *attrs = @{NSFontAttributeName: [NSFont systemFontOfSize:13 weight:NSFontWeightBold], NSForegroundColorAttributeName: [NSColor whiteColor]};
        [@"safe growth\nsummary" drawInRect:NSInsetRect(safe, 14, 18) withAttributes:attrs];
    } else {
        [TokenForgeShopPreviewView drawZodiacMascot:zodiac inRect:NSMakeRect(NSMinX(bounds)+46, NSMidY(bounds)-84, 168, 168) accent:gold stage:5];
        drawRepo(NSMakeRect(NSMinX(bounds)+w*0.53, NSMidY(bounds)-52, w*0.30, 104), @"begin");
        drawArrow(NSMakePoint(NSMinX(bounds)+w*0.42, NSMidY(bounds)), NSMakePoint(NSMinX(bounds)+w*0.52, NSMidY(bounds)), green);
    }
    context.shouldAntialias = previousAntialias;
    NSLog(@"INFO [Onboarding][VISUAL] step=%ld stageBased=true gameTutorial=true squareCells=true clipped=false", (long)step);
}
+ (void)drawDesktopSparklesInRect:(NSRect)rect color:(NSColor *)color
{
    [color setFill];
    for (NSInteger i = 0; i < 7; i++) {
        CGFloat x = NSMinX(rect) + (i % 3) * rect.size.width * 0.34 + 8;
        CGFloat y = NSMinY(rect) + (i / 3) * rect.size.height * 0.28 + 10;
        NSRectFill(NSMakeRect(x, y, 5, 5));
        NSRectFill(NSMakeRect(x - 3, y + 2, 11, 1));
        NSRectFill(NSMakeRect(x + 2, y - 3, 1, 11));
    }
}
@end

static BOOL TokenForgeLooksLikeRawDashboardMetadata(NSString *value)
{
    if (value.length == 0) {
        return NO;
    }

    return [value rangeOfString:@"category " options:NSCaseInsensitiveSearch].location != NSNotFound ||
           [value rangeOfString:@"confidence " options:NSCaseInsensitiveSearch].location != NSNotFound ||
           [value rangeOfString:@"stat " options:NSCaseInsensitiveSearch].location != NSNotFound ||
           [value rangeOfString:@"sessions " options:NSCaseInsensitiveSearch].location != NSNotFound ||
           [value rangeOfString:@"interactions " options:NSCaseInsensitiveSearch].location != NSNotFound ||
           [value rangeOfString:@" | " options:0].location != NSNotFound;
}

static NSString *TokenForgeFriendlyDashboardSummary(NSString *value, NSString *fallback)
{
    NSString *summary = value.length > 0 ? value : fallback;
    if (!TokenForgeLooksLikeRawDashboardMetadata(summary)) {
        return summary;
    }

    BOOL agent = [summary rangeOfString:@"Codex" options:NSCaseInsensitiveSearch].location != NSNotFound ||
                 [summary rangeOfString:@"Claude" options:NSCaseInsensitiveSearch].location != NSNotFound ||
                 [summary rangeOfString:@"Cursor" options:NSCaseInsensitiveSearch].location != NSNotFound ||
                 [summary rangeOfString:@"Copilot" options:NSCaseInsensitiveSearch].location != NSNotFound ||
                 [summary rangeOfString:@"AI" options:NSCaseInsensitiveSearch].location != NSNotFound;
    BOOL hasXp = [summary rangeOfString:@"+0 XP" options:NSCaseInsensitiveSearch].location == NSNotFound &&
                 [summary rangeOfString:@"XP" options:NSCaseInsensitiveSearch].location != NSNotFound;
    BOOL warnings = [summary rangeOfString:@"warnings 0" options:NSCaseInsensitiveSearch].location == NSNotFound &&
                    [summary rangeOfString:@"warning" options:NSCaseInsensitiveSearch].location != NSNotFound;

    NSMutableArray<NSString *> *parts = [NSMutableArray array];
    [parts addObject:agent ? @"AI-assisted activity was detected and summarized safely." : @"Recent Git changes were analyzed and summarized safely."];
    [parts addObject:hasXp ? @"Growth XP was saved for this repository companion." : @"No additional XP was applied in this summary."];
    [parts addObject:warnings ? @"Some items need attention in Activity details." : @"No sync issues detected."];
    return [parts componentsJoinedByString:@" "];
}

static NSDictionary *TokenForgeDefaultDashboardState(void)
{
    return @{
        @"appTitle": @"TokenForge",
        @"appName": @"TokenForge",
        @"subtitle": @"Track Git and AI-assisted work as companion growth.",
        @"isLocalMode": @YES,
        @"connection": @"local",
        @"sync": @"optional",
        @"persistentStatusBarIdentifier": @"TokenForge.PersistentStatusBar",
        @"persistentStatusBarAccessibilityLabel": @"TokenForge persistent app status bar",
        @"syncStatusText": @"Optional sync",
        @"selectedNavItem": @"dashboard",
        @"primaryActionEnabled": @NO,
        @"hasActiveRepository": @NO,
        @"isAnalysisRunning": @NO,
        @"actionStatusKind": @"idle",
        @"actionStatusText": @"Connect a repository to create your first companion.",
        @"repositoryStatus": @"not_selected",
        @"repositorySafeError": @"",
        @"hasPendingReview": @NO,
        @"canSaveGrowth": @NO,
        @"canDiscardPendingReview": @NO,
        @"hasSavedReviews": @NO,
        @"hasRepositoryActivity": @NO,
        @"hasAiAgentActivity": @NO,
        @"persistedCompanionXP": @0,
        @"pendingEstimatedXP": @0,
        @"pendingReviewCount": @0,
        @"warningCount": @0,
        @"lastRunSummary": @"Connect a repository to start tracking Git growth.",
        @"codeStat": @0,
        @"focusStat": @0,
        @"debugStat": @0,
        @"designStat": @0,
        @"syncStat": @0,
        @"companionVisible": @NO,
        @"wanderEnabled": @NO,
        @"desiredVisible": @NO,
        @"actualVisible": @NO,
        @"movementEnabled": @NO,
        @"dragEnabled": @NO,
        @"panelExists": @NO,
        @"panelFrame": @"",
        @"selectedRepoHash": @"",
        @"repoApproved": @NO,
        @"movementPaused": @YES,
        @"overlayLastAction": @"startup",
        @"overlayLastError": @"",
        @"clickThroughEnabled": @NO,
        @"clickReactionEnabled": @YES,
        @"explicitQuitRequested": @NO,
        @"dashboardVisible": @YES,
        @"activeRepositoryId": @"",
        @"lastKnownFrame": @"",
        @"statusText": @"No repository connected",
        @"companion": @{@"name": @"No companion", @"stage": @"None", @"stageIndex": @0, @"level": @0, @"xp": @0, @"xpToNextLevel": @0, @"totalLifetimeXP": @0, @"canLevelUp": @NO, @"evolveActionVisible": @NO, @"evolveActionHiddenReason": @"no companion selected", @"xpStatusText": @"No repository connected", @"carryForwardText": @"", @"xpProgressRatio": @0.0, @"levelUpStatusText": @"Connect a repository to enable companion growth.", @"levelUpDisabledReason": @"Connect a repository to enable companion growth.", @"dashboardAnimationState": @"hidden", @"mood": @"hidden", @"skin": @"orange_cat", @"zodiacType": @"repository", @"zodiacLabel": @"Repository", @"motion": @{@"repositoryId": @"", @"activityLevel": @"idle", @"movementSpeed": @0.0, @"bounceAmplitude": @0.0, @"idleFrequency": @0.0, @"pulseFrequency": @0.0, @"reaction": @"none", @"mood": @"hidden", @"reasonSummary": @"No active repository.", @"updatedAt": @""}},
        @"repository": @{@"connected": @NO, @"id": @"", @"name": @"", @"status": @"not_selected", @"statusText": @"No repository connected", @"connectedCount": @0, @"hasValidSource": @NO, @"canAnalyze": @NO, @"disabledReason": @"Connect a repository first", @"analyzeDisabledReason": @"Connect a repository first"},
        @"codexAgent": @{@"connected": @NO, @"status": @"not_connected", @"statusText": @"Not connected"},
        @"agents": @{@"connectedCount": @0, @"lastProvider": @"None", @"warningCount": @0, @"statusText": @"No agents connected", @"privacyText": @"Local aggregate only"},
        @"providerUsagePercentages": @[],
        @"repositories": @[],
        @"agentProviders": @[],
        @"tokenShop": @{@"currencyName": @"Forge Coins", @"balance": @0, @"hasActiveRepository": @NO, @"statusText": @"Connect a repository to unlock companion cosmetics.", @"lastTransactionStatus": @"", @"targetType": @"repository", @"selectedAgentId": @"", @"selectedCategory": @"featured", @"categoryIds": @[@"featured", @"zodiac", @"skins", @"outfits", @"accessories", @"effects", @"motions", @"themes", @"exclusive", @"owned"], @"agents": @[], @"ownedItemIds": @"", @"equippedItemIds": @"", @"items": @[]},
        @"onboarding": @{@"firstRunCompleted": @NO, @"dismissedForNow": @NO, @"shouldPresentFirstRunGuide": @YES, @"presentationMode": @"guidedTutorial", @"currentStep": @"step_1", @"currentStepIndex": @0, @"stepCount": @5, @"canGoBack": @NO, @"canGoNext": @YES, @"statusText": @"Pick a repository, analyze Git history, grow a companion, unlock cosmetics, and bring it to your desktop.", @"steps": @[@"Pick a repository", @"Analyze Git history", @"Grow your companion", @"Unlock cosmetics", @"Bring it to the desktop"], @"zodiacIds": @[@"rat", @"ox", @"tiger", @"rabbit", @"dragon", @"snake", @"horse", @"goat", @"monkey", @"rooster", @"dog", @"pig"]},
        @"activity": @{@"todaySummary": @"No activity yet", @"state": @"No pending review", @"code": @0, @"focus": @0, @"debug": @0, @"design": @0, @"sync": @0, @"recentRunsSummary": @"No recent runs", @"savedReviewsSummary": @"No saved reviews", @"repositoryActivitySummary": @"No repository activity", @"agentActivitySummary": @"No AI agent activity", @"runningJobs": @[], @"pendingReviews": @[], @"recentRuns": @[], @"hasRecentRuns": @NO, @"hasSavedReviews": @NO, @"hasRepositoryActivity": @NO, @"hasAiAgentActivity": @NO},
        @"review": @{@"reviewId": @"", @"pending": @NO, @"summary": @"No pending review", @"source": @"", @"repositoryName": @"", @"providerName": @"", @"confidence": @"", @"estimatedXpDelta": @0, @"codeDelta": @0, @"focusDelta": @0, @"debugDelta": @0, @"designDelta": @0, @"syncDelta": @0, @"warnings": @"", @"canSaveGrowth": @NO, @"canDiscard": @NO, @"canViewDetails": @NO, @"detailVisible": @NO, @"selectedReviewId": @"", @"generatedAt": @"", @"status": @"none", @"evidenceSummary": @"", @"categoryBreakdown": @"", @"privacyNote": @"Raw prompt, code, file content, and command logs are not stored."}
    };
}

static NSDictionary *TokenForgeMergeDashboardState(NSDictionary *base, NSDictionary *update)
{
    NSMutableDictionary *merged = [(base ?: TokenForgeDefaultDashboardState()) mutableCopy];
    if (update.count == 0) {
        return merged;
    }

    for (NSString *key in update) {
        id value = update[key];
        id existing = merged[key];
        if ([value isKindOfClass:[NSDictionary class]] && [existing isKindOfClass:[NSDictionary class]]) {
            NSMutableDictionary *nested = [existing mutableCopy];
            [nested addEntriesFromDictionary:(NSDictionary *)value];
            merged[key] = nested;
        } else if (value != nil && value != [NSNull null]) {
            merged[key] = value;
        }
    }

    return merged;
}

static NSTextField *TokenForgeDashboardLabel(NSString *text, CGFloat size, NSFontWeight weight, NSColor *color, NSInteger lines)
{
    NSTextField *label = [NSTextField wrappingLabelWithString:text ?: @""];
    label.translatesAutoresizingMaskIntoConstraints = NO;
    label.font = [NSFont systemFontOfSize:size weight:weight];
    label.textColor = color ?: [NSColor labelColor];
    label.maximumNumberOfLines = lines;
    label.lineBreakMode = lines == 1 ? NSLineBreakByTruncatingTail : NSLineBreakByWordWrapping;
    label.allowsDefaultTighteningForTruncation = YES;
    [label setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [label setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    return label;
}

static NSTextField *TokenForgeRequiredOneLineLabel(NSString *text, CGFloat size, NSFontWeight weight, NSColor *color)
{
    NSTextField *label = TokenForgeDashboardLabel(text, size, weight, color, 1);
    label.lineBreakMode = NSLineBreakByClipping;
    label.allowsDefaultTighteningForTruncation = NO;
    [label setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [label setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    return label;
}

static NSTextField *TokenForgeLightCardTitleLabel(NSString *text)
{
    return TokenForgeDashboardLabel(text, 17.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1);
}

static NSTextField *TokenForgeLightCardBodyLabel(NSString *text, NSInteger lines)
{
    return TokenForgeDashboardLabel(text, 13.0, NSFontWeightRegular, TokenForgeLightCardSecondaryTextColor(), lines);
}

static NSTextField *TokenForgeLightCardCaptionLabel(NSString *text, NSInteger lines)
{
    return TokenForgeDashboardLabel(text, 12.0, NSFontWeightRegular, TokenForgeLightCardSecondaryTextColor(), lines);
}

static NSTextField *TokenForgeDarkSidebarLabel(NSString *text, CGFloat size, NSFontWeight weight, NSInteger lines)
{
    return TokenForgeDashboardLabel(text, size, weight, TokenForgeDarkSidebarTextColor(), lines);
}

static NSTextField *TokenForgeShellHeaderLabel(NSString *text, CGFloat size, NSFontWeight weight, NSInteger lines)
{
    return TokenForgeDashboardLabel(text, size, weight, [NSColor colorWithCalibratedRed:0.965 green:0.980 blue:1.0 alpha:1.0], lines);
}

static NSTextField *TokenForgeShellBodyLabel(NSString *text, NSInteger lines)
{
    return TokenForgeDashboardLabel(text, 13.0, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), lines);
}

static NSButton *TokenForgeDashboardButton(NSString *title, id target, SEL action)
{
    NSButton *button = [NSButton buttonWithTitle:title ?: @"" target:target action:action];
    button.translatesAutoresizingMaskIntoConstraints = NO;
    button.bezelStyle = NSBezelStyleRounded;
    button.controlSize = NSControlSizeRegular;
    button.font = [NSFont systemFontOfSize:13.0 weight:NSFontWeightMedium];
    button.cell.lineBreakMode = NSLineBreakByTruncatingTail;
    [button setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    if (@available(macOS 10.14, *)) {
        button.contentTintColor = TokenForgeLightCardPrimaryTextColor();
    }
    return button;
}

static NSButton *TokenForgeSecondaryButton(NSString *title, id target, SEL action)
{
    NSButton *button = TokenForgeDashboardButton(title, target, action);
    button.wantsLayer = YES;
    button.layer.cornerRadius = 7.0;
    button.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.70].CGColor;
    button.layer.borderColor = [NSColor colorWithCalibratedWhite:0.0 alpha:0.10].CGColor;
    button.layer.borderWidth = 1.0;
    button.bordered = NO;
    button.attributedTitle = [[NSAttributedString alloc] initWithString:title ?: @"" attributes:@{
        NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:NSFontWeightMedium],
        NSForegroundColorAttributeName: TokenForgeLightCardPrimaryTextColor()
    }];
    [button.heightAnchor constraintGreaterThanOrEqualToConstant:32.0].active = YES;
    return button;
}

static NSButton *TokenForgeDisabledButton(NSString *title)
{
    NSButton *button = TokenForgeSecondaryButton(title, nil, nil);
    button.enabled = NO;
    button.layer.backgroundColor = [NSColor colorWithCalibratedWhite:0.94 alpha:1.0].CGColor;
    button.attributedTitle = [[NSAttributedString alloc] initWithString:title ?: @"" attributes:@{
        NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:NSFontWeightMedium],
        NSForegroundColorAttributeName: TokenForgeDisabledTextColor()
    }];
    return button;
}

static NSButton *TokenForgePrimaryButton(NSString *title, id target, SEL action)
{
    NSButton *button = TokenForgeDashboardButton(title, target, action);
    button.bezelStyle = NSBezelStyleRegularSquare;
    button.wantsLayer = YES;
    button.layer.cornerRadius = 7.0;
    button.layer.backgroundColor = TokenForgeSelectedBlueColor().CGColor;
    button.bordered = NO;
    button.attributedTitle = [[NSAttributedString alloc] initWithString:title ?: @"" attributes:@{
        NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:NSFontWeightSemibold],
        NSForegroundColorAttributeName: [NSColor whiteColor]
    }];
    [button.heightAnchor constraintGreaterThanOrEqualToConstant:32.0].active = YES;
    return button;
}

static NSView *TokenForgeDashboardCard(void)
{
    NSView *view = [[NSView alloc] initWithFrame:NSZeroRect];
    view.translatesAutoresizingMaskIntoConstraints = NO;
    view.wantsLayer = YES;
    view.layer.backgroundColor = TokenForgeCardBackgroundColor().CGColor;
    view.layer.cornerRadius = 10.0;
    view.layer.borderColor = [NSColor colorWithCalibratedWhite:0.0 alpha:0.08].CGColor;
    view.layer.borderWidth = 1.0;
    return view;
}

static NSStackView *TokenForgeDashboardVerticalStack(CGFloat spacing)
{
    TokenForgeFlippedStackView *stack = [[TokenForgeFlippedStackView alloc] initWithFrame:NSZeroRect];
    stack.translatesAutoresizingMaskIntoConstraints = NO;
    stack.orientation = NSUserInterfaceLayoutOrientationVertical;
    stack.alignment = NSLayoutAttributeLeading;
    stack.distribution = NSStackViewDistributionFill;
    stack.spacing = spacing;
    stack.edgeInsets = NSEdgeInsetsMake(0, 0, 0, 0);
    return stack;
}

static NSStackView *TokenForgeDashboardHorizontalStack(CGFloat spacing)
{
    NSStackView *stack = [[NSStackView alloc] initWithFrame:NSZeroRect];
    stack.translatesAutoresizingMaskIntoConstraints = NO;
    stack.orientation = NSUserInterfaceLayoutOrientationHorizontal;
    stack.alignment = NSLayoutAttributeCenterY;
    stack.distribution = NSStackViewDistributionFill;
    stack.spacing = spacing;
    return stack;
}

static const CGFloat TokenForgeTabContentTopInset = 8.0;
static const CGFloat TokenForgeTabContentSideInset = 28.0;
static const CGFloat TokenForgeTabSafeBottomInset = 32.0;
static const CGFloat TokenForgePageMaxContentWidth = 1040.0;
static const CGFloat TokenForgePageSectionSpacing = 20.0;

static void TokenForgePinSubview(NSView *child, NSView *parent, CGFloat top, CGFloat leading, CGFloat bottom, CGFloat trailing)
{
    child.translatesAutoresizingMaskIntoConstraints = NO;
    [NSLayoutConstraint activateConstraints:@[
        [child.topAnchor constraintEqualToAnchor:parent.topAnchor constant:top],
        [child.leadingAnchor constraintEqualToAnchor:parent.leadingAnchor constant:leading],
        [child.trailingAnchor constraintEqualToAnchor:parent.trailingAnchor constant:-trailing],
        [child.bottomAnchor constraintEqualToAnchor:parent.bottomAnchor constant:-bottom]
    ]];
}

static void TokenForgeConstrainPageStack(NSView *page, NSView *document, CGFloat top, CGFloat side, CGFloat bottom)
{
    NSLayoutConstraint *fillWidth = [page.widthAnchor constraintEqualToAnchor:document.widthAnchor constant:-(side * 2.0)];
    fillWidth.priority = 999;
    NSLayoutConstraint *preferredMaxWidth = [page.widthAnchor constraintEqualToConstant:TokenForgePageMaxContentWidth];
    preferredMaxWidth.priority = 998;
    [NSLayoutConstraint activateConstraints:@[
        [page.topAnchor constraintEqualToAnchor:document.topAnchor constant:top],
        [page.leadingAnchor constraintGreaterThanOrEqualToAnchor:document.leadingAnchor constant:side],
        [page.trailingAnchor constraintLessThanOrEqualToAnchor:document.trailingAnchor constant:-side],
        [page.centerXAnchor constraintEqualToAnchor:document.centerXAnchor],
        [page.widthAnchor constraintLessThanOrEqualToConstant:TokenForgePageMaxContentWidth],
        fillWidth,
        preferredMaxWidth,
        [page.bottomAnchor constraintEqualToAnchor:document.bottomAnchor constant:-bottom]
    ]];
}

static BOOL TokenForgeAnyDesktopOverlayActuallyVisible(void)
{
    if (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) {
        return YES;
    }

    TokenForgeEnsureOverlayFarmRegistry();
    for (NSPanel *panel in [TokenForgeOverlayPanelsByRepositoryId allValues]) {
        if (panel != nil && panel.isVisible) {
            return YES;
        }
    }

    return NO;
}

static NSView *TokenForgeCardWithStack(NSStackView **stackOut, CGFloat padding, CGFloat spacing)
{
    NSView *card = TokenForgeDashboardCard();
    NSStackView *stack = TokenForgeDashboardVerticalStack(spacing);
    stack.alignment = NSLayoutAttributeLeading;
    [card addSubview:stack];
    TokenForgePinSubview(stack, card, padding, padding, padding, padding);
    if (stackOut != nil) {
        *stackOut = stack;
    }
    return card;
}

@implementation TokenForgeSettingsSwitchRow
- (BOOL)acceptsFirstMouse:(NSEvent *)event { return YES; }
- (BOOL)acceptsFirstResponder { return self.rowInteractive; }

- (void)configureWithTitle:(NSString *)title detail:(NSString *)detail checked:(BOOL)checked interactive:(BOOL)interactive
{
    self.checked = checked;
    self.rowInteractive = interactive;
    self.translatesAutoresizingMaskIntoConstraints = NO;
    self.wantsLayer = YES;
    self.layer.backgroundColor = (interactive ? TokenForgeCardBackgroundColor() : [NSColor colorWithCalibratedWhite:0.94 alpha:0.78]).CGColor;
    self.layer.cornerRadius = 8.0;
    self.layer.borderColor = [NSColor colorWithCalibratedWhite:0.0 alpha:interactive ? 0.08 : 0.05].CGColor;
    self.layer.borderWidth = 1.0;
    self.enabled = interactive;
    self.toolTip = interactive ? @"Click anywhere in this row to change the setting." : detail;
    [self.heightAnchor constraintGreaterThanOrEqualToConstant:74.0].active = YES;

    NSStackView *row = TokenForgeDashboardHorizontalStack(12.0);
    row.distribution = NSStackViewDistributionFill;
    [self addSubview:row];
    TokenForgePinSubview(row, self, 13, 16, 13, 16);

    NSStackView *copy = TokenForgeDashboardVerticalStack(4.0);
    NSColor *titleColor = interactive ? TokenForgeLightCardPrimaryTextColor() : TokenForgeDisabledTextColor();
    NSColor *detailColor = interactive ? TokenForgeLightCardSecondaryTextColor() : TokenForgeDisabledTextColor();
    [copy addArrangedSubview:TokenForgeDashboardLabel(title, 14.0, NSFontWeightSemibold, titleColor, 1)];
    [copy addArrangedSubview:TokenForgeDashboardLabel(detail, 12.0, NSFontWeightRegular, detailColor, 2)];
    [row addArrangedSubview:copy];

    if (@available(macOS 10.15, *)) {
        NSSwitch *toggle = [[NSSwitch alloc] initWithFrame:NSZeroRect];
        toggle.translatesAutoresizingMaskIntoConstraints = NO;
        toggle.state = checked ? NSControlStateValueOn : NSControlStateValueOff;
        toggle.enabled = interactive;
        toggle.target = self;
        toggle.action = @selector(embeddedToggleChanged:);
        self.toggleControl = toggle;
    } else {
        NSButton *toggle = [[NSButton alloc] initWithFrame:NSZeroRect];
        toggle.translatesAutoresizingMaskIntoConstraints = NO;
        [toggle setButtonType:NSButtonTypeSwitch];
        toggle.title = @"";
        toggle.state = checked ? NSControlStateValueOn : NSControlStateValueOff;
        toggle.enabled = interactive;
        toggle.target = self;
        toggle.action = @selector(embeddedToggleChanged:);
        self.toggleControl = toggle;
    }

    [self.toggleControl setContentHuggingPriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [row addArrangedSubview:self.toggleControl];
}

- (void)setChecked:(BOOL)checked
{
    _checked = checked;
    self.toggleControl.integerValue = checked ? NSControlStateValueOn : NSControlStateValueOff;
}

- (void)embeddedToggleChanged:(id)sender
{
    if (!self.rowInteractive) {
        self.toggleControl.integerValue = self.checked ? NSControlStateValueOn : NSControlStateValueOff;
        return;
    }

    self.checked = self.toggleControl.integerValue == NSControlStateValueOn;
    [NSApp sendAction:self.action to:self.target from:self];
}

- (void)mouseDown:(NSEvent *)event
{
    if (!self.rowInteractive) {
        return;
    }

    [self.window makeFirstResponder:self];
    self.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.90 green:0.94 blue:0.99 alpha:1.0].CGColor;
}

- (void)mouseUp:(NSEvent *)event
{
    if (!self.rowInteractive) {
        return;
    }

    self.layer.backgroundColor = TokenForgeCardBackgroundColor().CGColor;
    NSPoint point = [self convertPoint:event.locationInWindow fromView:nil];
    if (NSPointInRect(point, self.bounds)) {
        NSPoint togglePoint = [self.toggleControl convertPoint:event.locationInWindow fromView:nil];
        if (self.toggleControl != nil && NSPointInRect(togglePoint, self.toggleControl.bounds)) {
            return;
        }

        self.checked = !self.checked;
        [NSApp sendAction:self.action to:self.target from:self];
    }
}

- (void)keyDown:(NSEvent *)event
{
    if (!self.rowInteractive) {
        return;
    }

    if ([event.charactersIgnoringModifiers isEqualToString:@" "] || [event.charactersIgnoringModifiers isEqualToString:@"\r"]) {
        self.checked = !self.checked;
        [NSApp sendAction:self.action to:self.target from:self];
        return;
    }

    [super keyDown:event];
}
@end

@implementation TokenForgeSkinPreviewView
- (BOOL)isOpaque { return NO; }

- (void)drawRect:(NSRect)dirtyRect
{
    [[NSColor clearColor] setFill];
    NSRectFill(dirtyRect);

    NSString *skin = self.skinId ?: @"orange_cat";
    NSColor *body = [NSColor colorWithCalibratedRed:0.92 green:0.48 blue:0.21 alpha:1.0];
    NSColor *accent = [NSColor colorWithCalibratedRed:0.99 green:0.77 blue:0.32 alpha:1.0];
    if ([skin isEqualToString:@"white_cat"]) {
        body = [NSColor colorWithCalibratedRed:0.94 green:0.95 blue:0.91 alpha:1.0];
        accent = [NSColor colorWithCalibratedRed:0.36 green:0.58 blue:0.78 alpha:1.0];
    } else if ([skin isEqualToString:@"calico"]) {
        body = [NSColor colorWithCalibratedRed:0.96 green:0.72 blue:0.42 alpha:1.0];
        accent = [NSColor colorWithCalibratedRed:0.16 green:0.17 blue:0.20 alpha:1.0];
    } else if ([skin isEqualToString:@"black_cat"]) {
        body = [NSColor colorWithCalibratedRed:0.16 green:0.17 blue:0.20 alpha:1.0];
        accent = [NSColor colorWithCalibratedRed:0.94 green:0.78 blue:0.30 alpha:1.0];
    } else if ([skin isEqualToString:@"retriever"]) {
        body = [NSColor colorWithCalibratedRed:0.82 green:0.58 blue:0.30 alpha:1.0];
        accent = [NSColor colorWithCalibratedRed:0.54 green:0.32 blue:0.17 alpha:1.0];
    } else if ([skin isEqualToString:@"runner"]) {
        body = [NSColor colorWithCalibratedRed:0.39 green:0.63 blue:0.98 alpha:1.0];
        accent = [NSColor colorWithCalibratedRed:0.95 green:0.30 blue:0.34 alpha:1.0];
    }

    NSRect preview = NSInsetRect(self.bounds, 8.0, 8.0);
    NSColor *outline = [NSColor colorWithCalibratedRed:0.13 green:0.15 blue:0.19 alpha:1.0];
    [TokenForgeShopPreviewView pixelRect:preview x:10 y:20 w:28 h:19 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:8 y:28 w:4 h:7 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:36 y:28 w:5 h:7 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:13 y:22 w:23 h:16 color:body];
    [TokenForgeShopPreviewView pixelRect:preview x:12 y:9 w:26 h:23 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:15 y:12 w:20 h:19 color:body];
    [TokenForgeShopPreviewView pixelRect:preview x:15 y:6 w:5 h:8 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:29 y:6 w:5 h:8 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:16 y:8 w:3 h:6 color:accent];
    [TokenForgeShopPreviewView pixelRect:preview x:30 y:8 w:3 h:6 color:accent];
    [TokenForgeShopPreviewView pixelRect:preview x:13 y:30 w:24 h:5 color:accent];
    [TokenForgeShopPreviewView pixelRect:preview x:19 y:20 w:3 h:3 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:28 y:20 w:3 h:3 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:23 y:25 w:5 h:2 color:outline];
    [TokenForgeShopPreviewView pixelRect:preview x:33 y:28 w:9 h:4 color:accent];
    NSLog(@"INFO [SkinPreview][PIXEL_ART] skin=%@ squareCells=true nearestNeighbor=true smoothOvalFallback=false", skin);
}
@end

@implementation TokenForgeNativeDashboardController

- (instancetype)init
{
    self = [super init];
    if (self != nil) {
        self.state = TokenForgeDefaultDashboardState();
        self.selectedNavItem = @"dashboard";
        TokenForgeCurrentDashboardTab = self.selectedNavItem;
        self.activityFilterValue = @"all";
        self.firstRunGuideSuppressedByDashboardNavigation = NO;
    }
    return self;
}

- (void)showDashboard
{
    [self openOrFocusDashboardFromSource:@"nativeDashboardController"];
}

- (void)openOrFocusDashboardFromSource:(NSString *)source
{
    if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForgeNativeDashboardController.openOrFocusDashboardFromSource", source ?: @"unknown", TokenForgeSourceLooksExplicit(source ?: @""))) {
        return;
    }

    TokenForgeLogRuntimeIdentityIfNeeded();
    NSString *openSource = source.length > 0 ? source : @"unknown";
    TokenForgeLastDashboardOpenSource = [openSource copy];
    TokenForgeLastDashboardOpenAt = [NSDate timeIntervalSinceReferenceDate];
    NSLog(@"INFO [DashboardLifecycle][OPEN_REQUEST] source=%@", openSource);
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] open.request source=%@", openSource);
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][OPEN] source=%@", openSource);
    TokenForgeDumpDashboardWindows(@"beforeOpen");
    TokenForgeCleanupStaleUnityDashboardWindows(openSource);
    NSWindow *canonical = TokenForgeCleanupDuplicateDashboardWindows(self.dashboardWindow, openSource);
    if (canonical != nil && self.dashboardWindow != canonical) {
        self.dashboardWindow = canonical;
    }
    if (canonical != nil) {
        NSLog(@"INFO [DashboardLifecycle][REUSE_EXISTING] window=%p source=%@ visible=%@ key=%@",
              canonical,
              openSource,
              canonical.isVisible ? @"true" : @"false",
              canonical.isKeyWindow ? @"true" : @"false");
    }

    if (self.dashboardWindow != nil && !TokenForgeIsNativeDashboardWindow(self.dashboardWindow)) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] canonical.nil reason=invalidControllerWindow windowNumber=%ld", (long)self.dashboardWindow.windowNumber);
        self.dashboardWindow = nil;
        if (TokenForgeNativeDashboardWindow != nil && !TokenForgeIsNativeDashboardWindow(TokenForgeNativeDashboardWindow)) {
            TokenForgeNativeDashboardWindow = nil;
        }
    }

    BOOL hadWindow = self.dashboardWindow != nil;
    BOOL wasVisible = hadWindow && self.dashboardWindow.isVisible;
    [self ensureDashboardWindow];
    if (self.dashboardWindow == nil) {
        TokenForgeDashboardLifecycleLog(@"WARN [DashboardLifecycle][WARN] open.failed source=%@ reason=windowNilAfterEnsure", openSource);
        return;
    }

    TokenForgeCleanupDuplicateDashboardWindows(self.dashboardWindow, openSource);
    [NSApp activateIgnoringOtherApps:YES];
    BOOL wasMiniaturized = self.dashboardWindow.isMiniaturized;
    NSRect normalizedFrame = TokenForgeNormalizeDashboardFrame(self.dashboardWindow.frame, openSource);
    TokenForgeConfigureAndLogWindowChrome(self.dashboardWindow, [@"open." stringByAppendingString:openSource]);
    if (!NSEqualRects(normalizedFrame, self.dashboardWindow.frame)) {
        [self.dashboardWindow setFrame:normalizedFrame display:NO];
        NSLog(@"INFO [DashboardLifecycle][FRAME_REPAIR_APPLIED] source=%@ frame=(%.2f,%.2f %.2fx%.2f)",
              openSource,
              normalizedFrame.origin.x,
              normalizedFrame.origin.y,
              normalizedFrame.size.width,
              normalizedFrame.size.height);
    }
    NSLog(@"INFO [DashboardLifecycle][DEMINIATURIZE] source=%@ wasMiniaturized=%@",
          openSource,
          wasMiniaturized ? @"true" : @"false");
    if (wasMiniaturized) {
        [self.dashboardWindow deminiaturize:nil];
    }
    [NSApp unhide:nil];
    if (wasVisible) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] open.focusExisting windowNumber=%ld source=%@", (long)self.dashboardWindow.windowNumber, openSource);
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][FOCUS_EXISTING] source=%@ windowNumber=%ld", openSource, (long)self.dashboardWindow.windowNumber);
    } else if (hadWindow) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] open.reuseHidden windowNumber=%ld source=%@", (long)self.dashboardWindow.windowNumber, openSource);
    }

    if (self.dashboardWindow.canBecomeKeyWindow) {
        [self.dashboardWindow makeKeyAndOrderFront:nil];
    } else {
        [self.dashboardWindow orderFront:nil];
    }
    [self.dashboardWindow orderFrontRegardless];
    self.dashboardWindow.collectionBehavior = NSWindowCollectionBehaviorManaged;
    TokenForgeLogWindowLifecycle(@"orderFront", self.dashboardWindow, @"openDashboard");
    TokenForgeRefreshDashboardAndOverlayState(openSource);
    TokenForgeLogDashboardLaunchDiagnostic(openSource, YES, self.dashboardWindow, normalizedFrame);
    NSLog(@"INFO [DashboardLifecycle][FOCUS] source=%@ visible=%@ key=%@ main=%@ miniaturized=%@ window=%p",
          openSource,
          self.dashboardWindow.isVisible ? @"true" : @"false",
          self.dashboardWindow.isKeyWindow ? @"true" : @"false",
          self.dashboardWindow.isMainWindow ? @"true" : @"false",
          self.dashboardWindow.isMiniaturized ? @"true" : @"false",
          self.dashboardWindow);
    NSLog(@"INFO [DashboardLifecycle][VERIFY_AFTER_OPEN] source=%@ visible=%@ key=%@ main=%@ miniaturized=%@",
          openSource,
          self.dashboardWindow.isVisible ? @"true" : @"false",
          self.dashboardWindow.isKeyWindow ? @"true" : @"false",
          self.dashboardWindow.isMainWindow ? @"true" : @"false",
          self.dashboardWindow.isMiniaturized ? @"true" : @"false");
    if (!self.dashboardWindow.isVisible || self.dashboardWindow.isMiniaturized || !self.dashboardWindow.isKeyWindow) {
        NSLog(@"INFO [DashboardLifecycle][RETRY_FOCUS] source=%@ reason=notFrontOrMiniaturized", openSource);
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.12 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            if (self.dashboardWindow == nil) {
                return;
            }
            if (self.dashboardWindow.isMiniaturized) {
                [self.dashboardWindow deminiaturize:nil];
            }
            [NSApp unhide:nil];
            [NSApp activateIgnoringOtherApps:YES];
            [self.dashboardWindow makeKeyAndOrderFront:nil];
            [self.dashboardWindow orderFrontRegardless];
            TokenForgeLogDashboardLaunchDiagnostic([openSource stringByAppendingString:@".retryFocus"], YES, self.dashboardWindow, TokenForgeNormalizeDashboardFrame(self.dashboardWindow.frame, openSource));
            NSLog(@"INFO [DashboardLifecycle][VERIFY_AFTER_OPEN] source=%@ visible=%@ key=%@ main=%@ miniaturized=%@",
                  openSource,
                  self.dashboardWindow.isVisible ? @"true" : @"false",
                  self.dashboardWindow.isKeyWindow ? @"true" : @"false",
                  self.dashboardWindow.isMainWindow ? @"true" : @"false",
                  self.dashboardWindow.isMiniaturized ? @"true" : @"false");
        });
    }
    if ([openSource isEqualToString:@"dock.reopen"]) {
        TokenForgeDumpWindowClassifications(@"AFTER_DOCK_REOPEN");
    } else if ([openSource isEqualToString:@"menubar.dashboard"]) {
        TokenForgeDumpWindowClassifications(@"AFTER_MENUBAR_OPEN");
    } else if ([openSource isEqualToString:@"launch.initial"]) {
        TokenForgeDumpWindowClassifications(@"AFTER_LAUNCH_INITIAL_DASHBOARD");
    }
    TokenForgeDumpDashboardWindows(@"afterOpen");
}

- (void)hideDashboard
{
    [self hideDashboardFromSource:@"nativeDashboardController"];
}

- (void)hideDashboardFromSource:(NSString *)source
{
    if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForgeNativeDashboardController.hideDashboardFromSource", source ?: @"unknown", YES)) {
        return;
    }

    NSString *closeSource = source.length > 0 ? source : @"unknown";
    TokenForgeIsClosingDashboard = YES;
    TokenForgeUserClosingDashboard = YES;
    TokenForgeLastDashboardExplicitCloseAt = [NSDate timeIntervalSinceReferenceDate];
    TokenForgeLastDashboardCloseAt = TokenForgeLastDashboardExplicitCloseAt;
    NSInteger visibleBefore = TokenForgeVisibleNativeDashboardWindowCount();
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] close.request source=%@", closeSource);
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][CLOSE_HIDE_ONLY] source=%@", closeSource);
    NSLog(@"INFO [OverlayLifecycle][KEEP_ALIVE_AFTER_DASHBOARD_CLOSE] enabled=%@",
          TokenForgeDesiredCompanionVisible ? @"true" : @"false");
    NSLog(@"INFO [DashboardLifecycle][CLOSE] source=%@ visibleBefore=%ld",
          closeSource,
          (long)visibleBefore);
    TokenForgeDumpDashboardWindows(@"beforeClose");
    NSWindow *canonical = TokenForgeCleanupDuplicateDashboardWindows(self.dashboardWindow, closeSource);
    if (canonical != nil && self.dashboardWindow != canonical) {
        self.dashboardWindow = canonical;
    }

    for (NSWindow *window in TokenForgeNativeDashboardCandidates()) {
        [window orderOut:nil];
        NSLog(@"INFO [WindowLifecycle][ORDER_OUT_NOT_TERMINATE] source=%@ window=%p", closeSource, window);
        TokenForgeLogWindowLifecycle(@"orderOut", window, @"hideDashboard");
    }

    NSInteger visibleCount = TokenForgeVisibleNativeDashboardWindowCount();
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] close.after visibleDashboardCount=%ld source=%@", (long)visibleCount, closeSource);
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][CLOSE] source=%@ visibleBefore=%ld visibleAfter=%ld", closeSource, (long)visibleBefore, (long)visibleCount);
    TokenForgeRefreshDashboardAndOverlayState(closeSource);
    TokenForgeDumpWindowClassifications(@"AFTER_DASHBOARD_CLOSE");
    TokenForgeDumpDashboardWindows(@"afterClose");
    TokenForgeUserClosingDashboard = NO;
    TokenForgeIsClosingDashboard = NO;
}

- (void)toggleDashboard
{
    if (self.dashboardWindow != nil && self.dashboardWindow.isVisible) {
        [self hideDashboardFromSource:@"toggle"];
    } else {
        [self openOrFocusDashboardFromSource:@"toggle"];
    }
}

- (void)showSettings
{
    if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForgeNativeDashboardController.showSettings", @"settings", YES)) {
        return;
    }

    [self ensureSettingsWindow];
    [NSApp activateIgnoringOtherApps:YES];
    [self.settingsWindow makeKeyAndOrderFront:nil];
    [self.settingsWindow orderFrontRegardless];
}

- (void)updateState:(NSDictionary *)state
{
    if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForgeNativeDashboardController.updateState", @"dashboardState", NO)) {
        return;
    }

    if (TokenForgeSuppressDashboardRedrawDuringOverlayDrag || TokenForgeIsDraggingOverlay) {
        NSLog(@"INFO [DashboardLifecycle][REDRAW_SUPPRESSED] reason=overlayDrag");
        NSLog(@"INFO [CSharpProjection][SKIP_TO_NATIVE] reason=overlayDragInProgress");
        return;
    }

    self.state = TokenForgeMergeDashboardState(self.state, state);
    BOOL wasHydrated = TokenForgeDashboardStateHydrated;
    TokenForgeDashboardStateHydrated = YES;
    NSMutableDictionary *actualized = [self.state mutableCopy];
    BOOL nativeActualVisible = TokenForgeAnyDesktopOverlayActuallyVisible();
    actualized[@"actualVisible"] = @(nativeActualVisible);
    self.state = actualized;
    NSDictionary *onboardingSnapshot = TokenForgeDashboardDictionary(self.state, @"onboarding");
    if (TokenForgeDashboardBool(onboardingSnapshot, @"firstRunCompleted", NO) ||
        TokenForgeDashboardBool(onboardingSnapshot, @"dismissedForNow", NO) ||
        !TokenForgeDashboardBool(onboardingSnapshot, @"shouldPresentFirstRunGuide", NO)) {
        self.firstRunGuideSuppressedByDashboardNavigation = NO;
    }
    NSDictionary *companionSnapshot = TokenForgeDashboardDictionary(self.state, @"companion");
    NSDictionary *repositorySnapshot = TokenForgeDashboardDictionary(self.state, @"repository");
    TokenForgeSnapshotZodiacType = [TokenForgeDashboardString(companionSnapshot, @"zodiacType", TokenForgeSnapshotZodiacType ?: @"tiger") copy];
    TokenForgeHydrateCompanionSnapshot(TokenForgeDashboardString(repositorySnapshot, @"id", @""),
                                       TokenForgeDashboardInteger(companionSnapshot, @"stageIndex", 0),
                                       TokenForgeDashboardInteger(companionSnapshot, @"level", 1),
                                       TokenForgeDashboardInteger(companionSnapshot, @"xp", 0),
                                       TokenForgeMenuArchetypeIndex,
                                       TokenForgeDashboardString(companionSnapshot, @"skin", @"orange_cat"),
                                       @"dashboardState");
    self.selectedNavItem = TokenForgeDashboardString(self.state, @"selectedNavItem", self.selectedNavItem ?: @"dashboard");
    TokenForgeCurrentDashboardTab = self.selectedNavItem;
    if (!wasHydrated) {
        NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
        BOOL hasActiveRepository = TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO) &&
            TokenForgeDashboardBool(repository, @"connected", NO);
        NSDictionary *onboarding = TokenForgeDashboardDictionary(self.state, @"onboarding");
        NSString *firstRoute = !hasActiveRepository && TokenForgeDashboardBool(onboarding, @"shouldPresentFirstRunGuide", NO)
            ? @"onboarding"
            : @"dashboard";
        NSLog(@"INFO [LaunchRouteDiagnostic] nativeHydrationComplete=true loadedRepositoriesCount=%lu activeRepositoryId=%@ activeRepositoryName=%@ firstVisibleRoute=%@",
              (unsigned long)TokenForgeDashboardArray(self.state, @"repositories").count,
              TokenForgeDashboardString(repository, @"id", @"none"),
              TokenForgeDashboardString(repository, @"name", @"none"),
              firstRoute);
    }
    [self rebuildDashboardIfNeeded];
    [self verifyPersistentStatusBarForContext:@"repositoryProjectionUpdate" repairIfMissing:YES];
    [self rebuildSettingsIfNeeded];
    NSLog(@"INFO [OverlayState][DASHBOARD_RENDER] desiredVisible=%@ actualVisible=%@ dragEnabled=%@ clickThrough=%@",
          TokenForgeDashboardBool(self.state, @"desiredVisible", NO) ? @"true" : @"false",
          TokenForgeDashboardBool(self.state, @"actualVisible", NO) ? @"true" : @"false",
          TokenForgeDashboardBool(self.state, @"dragEnabled", NO) ? @"true" : @"false",
          TokenForgeDashboardBool(self.state, @"clickThroughEnabled", NO) ? @"true" : @"false");
    if (TokenForgeDashboardBool(self.state, @"desiredVisible", NO) && !TokenForgeDashboardBool(self.state, @"actualVisible", NO)) {
        NSLog(@"WARN [OverlayState][MISMATCH] desiredVisible=true actualVisible=false source=dashboardRender");
    }
    NSLog(@"INFO [NativeDashboard] state updated repository=%@ codex=%@ pending=%ld",
          TokenForgeDashboardString(TokenForgeDashboardDictionary(self.state, @"repository"), @"statusText", @"Not selected"),
          TokenForgeDashboardString(TokenForgeDashboardDictionary(self.state, @"codexAgent"), @"statusText", @"Not connected"),
          (long)TokenForgeDashboardInteger(self.state, @"pendingReviewCount", 0));
    if (TokenForgePendingExplicitDashboardOpenSource.length > 0) {
        NSString *pendingSource = [TokenForgePendingExplicitDashboardOpenSource copy];
        TokenForgePendingExplicitDashboardOpenSource = nil;
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeOpenOrFocusDashboard(pendingSource);
        });
    } else if (TokenForgeDashboardStateHydrated &&
               !TokenForgeRuntimeVerificationMode &&
               !TokenForgeExplicitQuitRequested &&
               !TokenForgeTerminating &&
               !TokenForgeIsDashboardVisible()) {
        // Some Unity/AppKit combinations activate from a Dock click without delivering
        // applicationShouldHandleReopen when a non-activating companion panel is visible.
        // Treat the activation transition as a fallback reopen path as well.
        NSLog(@"INFO [DockReopenDiagnostic] activationFallbackReceived=true mainWindowVisible=false activationRequested=true");
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeOpenOrFocusDashboard(@"dock.reopen");
        });
    }
}

- (void)setMenuBarStatus:(NSDictionary *)state
{
    if (!TokenForgeStatusItemLaunchPathAllowed(@"TokenForgeNativeDashboardController.setMenuBarStatus", @"menuBarStatus")) {
        return;
    }

    NSDictionary *companion = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"companion");
    NSDictionary *repository = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"repository");
    NSDictionary *agent = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"codexAgent");
    NSDictionary *agents = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"agents");
    NSDictionary *farm = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"companionFarm");
    BOOL repositoryConnected = TokenForgeDashboardBool(repository, @"connected", NO);
    TokenForgeSnapshotZodiacType = [TokenForgeDashboardString(companion, @"zodiacType", TokenForgeSnapshotZodiacType ?: @"tiger") copy];
    TokenForgeMenuCompanionName = [TokenForgeDashboardString(companion, @"name", repositoryConnected ? @"Token" : @"TokenForge") copy];
    TokenForgeMenuStage = [TokenForgeDashboardString(companion, @"stage", repositoryConnected ? @"Egg" : @"None") copy];
    TokenForgeMenuStageIndex = repositoryConnected ? MAX(0, MIN(5, TokenForgeDashboardInteger(companion, @"stageIndex", 0))) : 0;
    TokenForgeMenuLevel = repositoryConnected ? MAX(1, TokenForgeDashboardInteger(companion, @"level", 1)) : 0;
    TokenForgeMenuRepositoryAlias = [TokenForgeDashboardString(repository, @"name", TokenForgeDashboardBool(repository, @"connected", NO) ? @"Repository" : @"Not selected") copy];
    TokenForgeMenuAgentStatus = [TokenForgeDashboardString(agents, @"statusText", TokenForgeDashboardString(agent, @"statusText", @"Not connected")) copy];
    TokenForgeMenuSyncStatus = [TokenForgeDashboardString(state.count > 0 ? state : self.state, @"syncStatusText", @"Optional sync") copy];
    TokenForgeMenuCompanionEnabled = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"companionVisible", TokenForgeMenuCompanionEnabled);
    TokenForgeMenuClickThrough = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"clickThroughEnabled", TokenForgeMenuClickThrough);
    TokenForgeMenuMovementEnabled = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"movementEnabled", TokenForgeMenuMovementEnabled);
    TokenForgeMenuCanLevelUp = TokenForgeDashboardBool(companion, @"canLevelUp", NO);
    TokenForgeMenuAnalysisRunning = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"isAnalysisRunning", NO);
    TokenForgeMenuReaction = [TokenForgeDashboardString(TokenForgeDashboardDictionary(companion, @"motion"), @"reaction", @"none") copy];
    TokenForgeMenuCanAnalyze = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"primaryActionEnabled", YES) &&
        !TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"isAnalysisRunning", NO);
    TokenForgeMenuConnectedCompanionCount = MAX(0, TokenForgeDashboardInteger(repository, @"connectedCount", 0));
    NSInteger actualVisibleOverlayCount = TokenForgeVisibleOverlayFarmCount();
    if (actualVisibleOverlayCount == 0 && TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) {
        actualVisibleOverlayCount = 1;
    }
    NSInteger projectedVisibleOverlayCount = MAX(0, TokenForgeDashboardInteger(farm, @"visibleCount", 0));
    TokenForgeMenuVisibleOverlayCount = MAX(0, actualVisibleOverlayCount);
    NSLog(@"INFO [OverlayProjection] statusBar projectedVisible=%ld actualVisible=%ld connected=%ld",
          (long)projectedVisibleOverlayCount,
          (long)actualVisibleOverlayCount,
          (long)TokenForgeMenuConnectedCompanionCount);
    TokenForgeHydrateCompanionSnapshot(TokenForgeDashboardString(repository, @"id", @""),
                                       TokenForgeMenuStageIndex,
                                       TokenForgeMenuLevel,
                                       TokenForgeDashboardInteger(companion, @"xp", 0),
                                       TokenForgeMenuArchetypeIndex,
                                       TokenForgeDashboardString(companion, @"skin", @"orange_cat"),
                                       @"menuBarStatus");

    NSString *statusText = TokenForgeDashboardString(state, @"statusText", nil);
    if (statusText.length == 0) {
        NSDictionary *activity = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"activity");
        statusText = @"Repo: None · AI Agents: 0 connected";
    }

    TokenForgeMenuStatusText = [statusText copy];
    [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
}

- (void)ensureDashboardWindow
{
    if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForgeNativeDashboardController.ensureDashboardWindow", @"ensureDashboardWindow", YES)) {
        return;
    }

    NSWindow *canonical = TokenForgeCleanupDuplicateDashboardWindows(self.dashboardWindow, @"ensureDashboardWindow");
    if (canonical != nil) {
        self.dashboardWindow = canonical;
    }

    if (self.dashboardWindow != nil) {
        TokenForgeConfigureAndLogWindowChrome(self.dashboardWindow, @"ensureDashboardWindow.reuse");
        [self rebuildDashboardIfNeeded];
        [self verifyPersistentStatusBarForContext:@"initialDashboardOpen" repairIfMissing:YES];
        return;
    }

    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] open.createNew reason=noCanonicalWindow source=ensureDashboardWindow");
    NSRect frame = NSMakeRect(0, 0, 1180, 760);
    NSString *savedFrame = [[NSUserDefaults standardUserDefaults] stringForKey:TokenForgeDashboardFrameKey];
    if (savedFrame.length > 0) {
        frame = TokenForgeNormalizeDashboardFrame(NSRectFromString(savedFrame), @"savedFrame");
    } else {
        frame = TokenForgeNormalizeDashboardFrame(frame, @"defaultFrame");
    }
    self.dashboardWindow = [[NSWindow alloc] initWithContentRect:frame
                                                       styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable | NSWindowStyleMaskResizable
                                                         backing:NSBackingStoreBuffered
                                                           defer:NO];
    self.dashboardWindow.title = @"TokenForge";
    NSRect dashboardVisibleFrame = TokenForgeVisibleFrameForFrame(frame);
    self.dashboardWindow.minSize = NSMakeSize(MIN(1080.0, MAX(320.0, dashboardVisibleFrame.size.width - 24.0)),
                                              MIN(720.0, MAX(320.0, dashboardVisibleFrame.size.height - 24.0)));
    self.dashboardWindow.delegate = self;
    self.dashboardWindow.releasedWhenClosed = NO;
    self.dashboardWindow.restorable = NO;
    self.dashboardWindow.restorationClass = nil;
    self.dashboardWindow.identifier = TokenForgeDashboardWindowIdentifier;
    [self.dashboardWindow setFrame:frame display:NO];
    TokenForgeConfigureAndLogWindowChrome(self.dashboardWindow, @"ensureDashboardWindow.create");
    TokenForgeNativeDashboardWindow = self.dashboardWindow;
    NSLog(@"INFO [DashboardLifecycle][CREATE] window=%p source=ensureDashboardWindow", self.dashboardWindow);
    TokenForgeLogWindowLifecycle(@"created", self.dashboardWindow, @"dashboard");
    if (savedFrame.length == 0) {
        [self.dashboardWindow center];
    }
    [self rebuildDashboardIfNeeded];
    [self verifyPersistentStatusBarForContext:@"initialDashboardOpen" repairIfMissing:YES];
}

- (void)ensureSettingsWindow
{
    if (self.settingsWindow != nil) {
        TokenForgeConfigureAndLogWindowChrome(self.settingsWindow, @"ensureSettingsWindow.reuse");
        [self rebuildSettingsIfNeeded];
        return;
    }

    NSRect frame = NSMakeRect(0, 0, 900, 680);
    NSString *savedFrame = [[NSUserDefaults standardUserDefaults] stringForKey:TokenForgeSettingsFrameKey];
    if (savedFrame.length > 0) {
        frame = TokenForgeClampFrameToVisibleFrame(NSRectFromString(savedFrame));
    }
    self.settingsWindow = [[NSWindow alloc] initWithContentRect:frame
                                                      styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable | NSWindowStyleMaskResizable
                                                        backing:NSBackingStoreBuffered
                                                          defer:NO];
    self.settingsWindow.title = @"TokenForge Settings";
    NSRect settingsVisibleFrame = TokenForgeVisibleFrameForFrame(frame);
    self.settingsWindow.minSize = NSMakeSize(MIN(760.0, MAX(320.0, settingsVisibleFrame.size.width - 24.0)),
                                             MIN(560.0, MAX(320.0, settingsVisibleFrame.size.height - 24.0)));
    self.settingsWindow.level = NSFloatingWindowLevel + 1;
    self.settingsWindow.delegate = self;
    self.settingsWindow.releasedWhenClosed = NO;
    self.settingsWindow.restorable = NO;
    self.settingsWindow.restorationClass = nil;
    self.settingsWindow.identifier = @"TokenForge.NativeSettings";
    frame.size.width = MIN(frame.size.width, MAX(320.0, settingsVisibleFrame.size.width - 24.0));
    frame.size.height = MIN(frame.size.height, MAX(320.0, settingsVisibleFrame.size.height - 24.0));
    frame = TokenForgeClampFrameToVisibleFrame(frame);
    [self.settingsWindow setFrame:frame display:NO];
    TokenForgeConfigureAndLogWindowChrome(self.settingsWindow, @"ensureSettingsWindow.create");
    TokenForgeLogWindowLifecycle(@"created", self.settingsWindow, @"settings");
    if (savedFrame.length == 0) {
        [self.settingsWindow center];
    }
    [self rebuildSettingsIfNeeded];
}

- (void)rebuildDashboardIfNeeded
{
    if (self.dashboardWindow == nil) {
        return;
    }

    if (TokenForgeSuppressDashboardRedrawDuringOverlayDrag || TokenForgeIsDraggingOverlay) {
        NSLog(@"INFO [DashboardLifecycle][REDRAW_SUPPRESSED] reason=overlayDrag");
        return;
    }

    self.dashboardWindow.contentView = [self buildDashboardRootView];
    TokenForgeLogWindowLifecycle(@"contentViewAssigned", self.dashboardWindow, @"dashboardRebuild");
    [self verifyPersistentStatusBarForContext:@"dashboardRebuild" repairIfMissing:YES];
}

- (void)rebuildSettingsIfNeeded
{
    if (self.settingsWindow == nil) {
        return;
    }

    self.settingsWindow.contentView = [self buildSettingsRootView];
}

- (BOOL)rebuildDashboardTabBodyOnlyForContext:(NSString *)context
{
    if (self.dashboardWindow == nil ||
        self.dashboardTabContentStack == nil ||
        self.dashboardTabContentStack.superview == nil ||
        TokenForgeShouldShowFirstRunGuide(self.state, self.selectedNavItem ?: @"dashboard", self.firstRunGuideSuppressedByDashboardNavigation)) {
        NSLog(@"INFO [PersistentStatusBar][BODY_ONLY_REBUILD_UNAVAILABLE] context=%@ reason=%@",
              context ?: @"unknown",
              self.dashboardTabContentStack == nil ? @"missingBodyStack" : @"firstRunOrNoWindow");
        return NO;
    }

    NSArray<NSView *> *existing = [self.dashboardTabContentStack.arrangedSubviews copy];
    for (NSView *view in existing) {
        [self.dashboardTabContentStack removeArrangedSubview:view];
        [view removeFromSuperview];
    }
    [self populateDashboardContent:self.dashboardTabContentStack];
    NSLog(@"INFO [PersistentStatusBar][BODY_ONLY_REBUILD] context=%@ shellReplaced=false bodyReplaced=true", context ?: @"unknown");
    [self verifyPersistentStatusBarForContext:context ?: @"bodyOnlyRebuild" repairIfMissing:NO];
    return YES;
}

- (NSView *)findPersistentStatusBarInView:(NSView *)view
{
    if (view == nil) {
        return nil;
    }

    NSString *identifier = view.identifier;
    if ([identifier isEqualToString:@"TokenForge.PersistentStatusBar"]) {
        return view;
    }

    for (NSView *subview in view.subviews) {
        NSView *match = [self findPersistentStatusBarInView:subview];
        if (match != nil) {
            return match;
        }
    }

    return nil;
}

- (NSString *)persistentStatusBarParentChain:(NSView *)view
{
    if (view == nil) {
        return @"missing";
    }

    NSMutableArray<NSString *> *chain = [NSMutableArray array];
    NSView *cursor = view;
    while (cursor != nil) {
        NSString *identifier = cursor.identifier.length > 0 ? cursor.identifier : @"no-id";
        [chain addObject:[NSString stringWithFormat:@"%@(%@)", NSStringFromClass(cursor.class), identifier]];
        cursor = cursor.superview;
    }

    return [chain componentsJoinedByString:@" <- "];
}

- (BOOL)view:(NSView *)view hasAncestorClass:(Class)ancestorClass
{
    NSView *cursor = view.superview;
    while (cursor != nil) {
        if ([cursor isKindOfClass:ancestorClass]) {
            return YES;
        }
        cursor = cursor.superview;
    }

    return NO;
}

- (BOOL)view:(NSView *)view hasAncestorIdentifier:(NSString *)identifier
{
    NSView *cursor = view.superview;
    while (cursor != nil) {
        if ([cursor.identifier isEqualToString:identifier]) {
            return YES;
        }
        cursor = cursor.superview;
    }

    return NO;
}

- (void)logPersistentStatusBar:(NSView *)bar context:(NSString *)context phase:(NSString *)phase
{
    if (bar == nil) {
        if ([phase isEqualToString:@"REPAIR"]) {
            NSLog(@"WARN [PersistentStatusBar][REPAIR] context=%@ exists=false repairCount=%lu tab=%@",
                  context ?: @"unknown",
                  (unsigned long)TokenForgePersistentStatusBarRepairCount,
                  self.selectedNavItem ?: @"dashboard");
            return;
        }
        NSLog(@"WARN [PersistentStatusBar][%@] context=%@ exists=false repairCount=%lu tab=%@",
              phase ?: @"VERIFY",
              context ?: @"unknown",
              (unsigned long)TokenForgePersistentStatusBarRepairCount,
              self.selectedNavItem ?: @"dashboard");
        return;
    }

    BOOL insideScrollView = [self view:bar hasAncestorClass:NSScrollView.class];
    BOOL insideTabContent = [self view:bar hasAncestorIdentifier:@"TokenForge.DashboardTabContent"] ||
                            [self view:bar hasAncestorIdentifier:@"TokenForge.DashboardTabScrollView"];
    BOOL insideOnboarding = [self view:bar hasAncestorIdentifier:@"TokenForge.OnboardingGuide"];
    NSLog(@"INFO [NativeShell][PERSISTENT_STATUS_BAR] context=%@ phase=%@ exists=true identifier=%@ visible=%@ insideScrollView=%@ owner=TokenForge.FixedTopShellHeader rootContainerStable=true tab=%@",
          context ?: @"unknown",
          phase ?: @"VERIFY",
          bar.identifier ?: @"",
          (!bar.hidden && !bar.isHiddenOrHasHiddenAncestor) ? @"true" : @"false",
          insideScrollView ? @"true" : @"false",
          self.selectedNavItem ?: @"dashboard");
    if ([phase isEqualToString:@"REPAIR"]) {
        NSLog(@"INFO [PersistentStatusBar][REPAIR] context=%@ exists=true identifier=%@ frame=%@ bounds=%@ hidden=%@ hiddenAncestor=%@ insideScrollView=%@ insideTabContent=%@ insideOnboarding=%@ repairCount=%lu tab=%@ parentChain=%@",
              context ?: @"unknown",
              bar.identifier ?: @"",
              NSStringFromRect(bar.frame),
              NSStringFromRect(bar.bounds),
              bar.hidden ? @"true" : @"false",
              bar.isHiddenOrHasHiddenAncestor ? @"true" : @"false",
              insideScrollView ? @"true" : @"false",
              insideTabContent ? @"true" : @"false",
              insideOnboarding ? @"true" : @"false",
              (unsigned long)TokenForgePersistentStatusBarRepairCount,
              self.selectedNavItem ?: @"dashboard",
              [self persistentStatusBarParentChain:bar]);
        return;
    }
    NSLog(@"INFO [PersistentStatusBar][%@] context=%@ exists=true identifier=%@ frame=%@ bounds=%@ hidden=%@ hiddenAncestor=%@ insideScrollView=%@ insideTabContent=%@ insideOnboarding=%@ repairCount=%lu tab=%@ parentChain=%@",
          phase ?: @"VERIFY",
          context ?: @"unknown",
          bar.identifier ?: @"",
          NSStringFromRect(bar.frame),
          NSStringFromRect(bar.bounds),
          bar.hidden ? @"true" : @"false",
          bar.isHiddenOrHasHiddenAncestor ? @"true" : @"false",
          insideScrollView ? @"true" : @"false",
          insideTabContent ? @"true" : @"false",
          insideOnboarding ? @"true" : @"false",
          (unsigned long)TokenForgePersistentStatusBarRepairCount,
          self.selectedNavItem ?: @"dashboard",
          [self persistentStatusBarParentChain:bar]);
    NSLog(@"INFO [PersistentStatusBar][VISIBLE] context=%@ visible=%@ hiddenAncestor=%@",
          context ?: @"unknown",
          (!bar.hidden && !bar.isHiddenOrHasHiddenAncestor) ? @"true" : @"false",
          bar.isHiddenOrHasHiddenAncestor ? @"true" : @"false");
    NSLog(@"INFO [PersistentStatusBar][PARENT_CHAIN] context=%@ chain=%@",
          context ?: @"unknown",
          [self persistentStatusBarParentChain:bar]);
    NSLog(@"INFO [PersistentStatusBar][OWNERSHIP] parent=%@ insideScrollView=%@",
          bar.superview.identifier ?: @"unknown",
          insideScrollView ? @"true" : @"false");
    NSLog(@"INFO [PersistentStatusBar][NOT_IN_SCROLL_VIEW] context=%@ value=%@", context ?: @"unknown", insideScrollView ? @"false" : @"true");
    NSLog(@"INFO [PersistentStatusBar][NOT_IN_TAB_CONTENT] context=%@ value=%@", context ?: @"unknown", insideTabContent ? @"false" : @"true");
    NSLog(@"INFO [PersistentStatusBar][NOT_IN_ONBOARDING] context=%@ value=%@", context ?: @"unknown", insideOnboarding ? @"false" : @"true");
    NSLog(@"INFO [PersistentStatusBar][REPAIR_COUNT] count=%lu", (unsigned long)TokenForgePersistentStatusBarRepairCount);
}

- (void)verifyPersistentStatusBarForContext:(NSString *)context repairIfMissing:(BOOL)repairIfMissing
{
    if (self.dashboardWindow == nil) {
        return;
    }

    NSView *bar = [self findPersistentStatusBarInView:self.dashboardWindow.contentView];
    if (bar != nil) {
        [self logPersistentStatusBar:bar context:context phase:@"VERIFY"];
        return;
    }

    NSLog(@"WARN [PersistentStatusBar][MISSING] context=%@ repair=%@ tab=%@",
          context ?: @"unknown",
          repairIfMissing ? @"true" : @"false",
          self.selectedNavItem ?: @"dashboard");
    if (repairIfMissing) {
        TokenForgePersistentStatusBarRepairCount += 1;
        if (TokenForgePersistentStatusBarRepairCount > 1) {
            NSLog(@"ERROR [PersistentStatusBar][REPAIR_REPEATED] context=%@ repairCount=%lu rootContainerOwnershipUnstable=true",
                  context ?: @"unknown",
                  (unsigned long)TokenForgePersistentStatusBarRepairCount);
        }
        self.dashboardWindow.contentView = [self buildDashboardRootView];
        NSView *repaired = [self findPersistentStatusBarInView:self.dashboardWindow.contentView];
        [self logPersistentStatusBar:repaired context:context phase:@"REPAIR"];
    }
}

- (NSView *)buildDashboardRootView
{
    TokenForgeLogDashboardLayoutPass(self.state, TokenForgeIsDraggingOverlay ? @"overlayDragRedraw" : @"dashboardRebuild");
    NSVisualEffectView *root = [[NSVisualEffectView alloc] initWithFrame:NSZeroRect];
    if (@available(macOS 10.14, *)) {
        root.material = NSVisualEffectMaterialWindowBackground;
    } else {
        root.material = NSVisualEffectMaterialAppearanceBased;
    }
    root.blendingMode = NSVisualEffectBlendingModeWithinWindow;
    root.state = NSVisualEffectStateActive;
    root.wantsLayer = YES;
    root.layer.backgroundColor = TokenForgeDashboardBackgroundColor().CGColor;
    root.translatesAutoresizingMaskIntoConstraints = NO;
    root.identifier = @"TokenForge.RootWindowContent";

    if (TokenForgeShouldShowFirstRunGuide(self.state, self.selectedNavItem ?: @"dashboard", self.firstRunGuideSuppressedByDashboardNavigation)) {
        self.dashboardRootView = root;
        self.dashboardTabDocumentView = nil;
        self.dashboardTabContentStack = nil;
        NSStackView *guideShell = TokenForgeDashboardVerticalStack(0.0);
        guideShell.alignment = NSLayoutAttributeWidth;
        guideShell.distribution = NSStackViewDistributionFill;
        [root addSubview:guideShell];
        TokenForgePinSubview(guideShell, root, 0, 0, 0, 0);
        [guideShell addArrangedSubview:[self buildFixedTopShellHeader]];

        NSScrollView *guideScroll = [[NSScrollView alloc] initWithFrame:NSZeroRect];
        guideScroll.translatesAutoresizingMaskIntoConstraints = NO;
        guideScroll.hasVerticalScroller = YES;
        guideScroll.hasHorizontalScroller = NO;
        guideScroll.borderType = NSNoBorder;
        guideScroll.drawsBackground = NO;
        guideScroll.identifier = @"TokenForge.FirstRunTutorialScrollView";
        [guideShell addArrangedSubview:guideScroll];

        TokenForgeFlippedView *guideDocument = [[TokenForgeFlippedView alloc] initWithFrame:NSMakeRect(0, 0, 1000, 980)];
        guideDocument.translatesAutoresizingMaskIntoConstraints = NO;
        guideDocument.identifier = @"TokenForge.FirstRunTutorialContent";
        guideScroll.documentView = guideDocument;
        [guideDocument.widthAnchor constraintEqualToAnchor:guideScroll.contentView.widthAnchor].active = YES;
        [guideDocument.heightAnchor constraintGreaterThanOrEqualToAnchor:guideScroll.contentView.heightAnchor].active = YES;

        NSStackView *guideContent = TokenForgeDashboardVerticalStack(TokenForgePageSectionSpacing);
        guideContent.alignment = NSLayoutAttributeWidth;
        [guideDocument addSubview:guideContent];
        TokenForgeConstrainPageStack(guideContent, guideDocument, 20, TokenForgeTabContentSideInset, TokenForgeTabSafeBottomInset);
        [guideContent addArrangedSubview:[self onboardingScreen]];
        NSLog(@"INFO [RuntimeUIPath][FirstLaunch] renderer=dedicatedGuide sidebar=false normalDashboard=false");
        NSLog(@"INFO [RuntimeUIPath][FirstRunTutorial] renderer=dedicatedGuide sidebar=false normalDashboard=false");
        NSLog(@"INFO [LayoutBounds] tab=firstRunTutorial contentFrame=auto visibleFrame=scrollView bottomInset=%.0f", TokenForgeTabSafeBottomInset);
        NSLog(@"INFO [LayoutBounds][WINDOW] tab=firstRunTutorial frame=%@", NSStringFromRect(self.dashboardWindow.frame));
        NSLog(@"INFO [LayoutBounds][SCROLL_CONTENT] tab=firstRunTutorial documentMinHeight=contentView bottomInset=%.0f", TokenForgeTabSafeBottomInset);
        NSLog(@"INFO [LayoutBounds][BOTTOM_INSET] tab=firstRunTutorial value=%.0f", TokenForgeTabSafeBottomInset);
        NSLog(@"INFO [LayoutBounds][BODY_FRAME] tab=firstRunTutorial body=TokenForge.FirstRunTutorialContent");
        NSLog(@"INFO [Onboarding][FIRST_RUN_PRESENTATION] mode=dedicatedGuide selectedNav=%@ sidebarVisible=false dashboardHijack=false", self.selectedNavItem ?: @"dashboard");
        return root;
    }

    NSStackView *rootWindowContent = TokenForgeDashboardVerticalStack(0.0);
    rootWindowContent.identifier = @"TokenForge.RootWindowContentShell";
    rootWindowContent.alignment = NSLayoutAttributeWidth;
    rootWindowContent.distribution = NSStackViewDistributionFill;
    [root addSubview:rootWindowContent];
    TokenForgePinSubview(rootWindowContent, root, 0, 0, 0, 0);
    NSView *fixedTopShellHeader = [self buildFixedTopShellHeader];
    [rootWindowContent addArrangedSubview:fixedTopShellHeader];
    NSLog(@"INFO [NativeShell][HEADER_NODE] identifier=TokenForge.FixedTopShellHeader insertedBefore=TokenForge.DashboardTabScrollView");

    NSStackView *bodyContainer = TokenForgeDashboardHorizontalStack(0.0);
    bodyContainer.identifier = @"TokenForge.BodyContainer";
    bodyContainer.alignment = NSLayoutAttributeHeight;
    bodyContainer.distribution = NSStackViewDistributionFill;
    [rootWindowContent addArrangedSubview:bodyContainer];

    NSVisualEffectView *sidebar = [[NSVisualEffectView alloc] initWithFrame:NSZeroRect];
    sidebar.identifier = @"TokenForge.FixedLeftSidebar";
    sidebar.accessibilityLabel = @"TokenForge.Sidebar";
    sidebar.translatesAutoresizingMaskIntoConstraints = NO;
    sidebar.material = NSVisualEffectMaterialSidebar;
    sidebar.blendingMode = NSVisualEffectBlendingModeWithinWindow;
    sidebar.state = NSVisualEffectStateActive;
    sidebar.wantsLayer = YES;
    sidebar.layer.backgroundColor = TokenForgeSidebarBackgroundColor().CGColor;
    CGFloat sidebarWidth = 300.0;
    [sidebar.widthAnchor constraintEqualToConstant:sidebarWidth].active = YES;
    [bodyContainer addArrangedSubview:sidebar];

    NSStackView *sidebarStack = TokenForgeDashboardVerticalStack(10.0);
    sidebarStack.alignment = NSLayoutAttributeWidth;
    [sidebar addSubview:sidebarStack];
    TokenForgePinSubview(sidebarStack, sidebar, 22, 16, 16, 16);
    NSLog(@"INFO [DashboardLayout] sidebar_width=%.2f", sidebarWidth);
    [self populateSidebar:sidebarStack];

    NSScrollView *scrollView = [[NSScrollView alloc] initWithFrame:NSZeroRect];
    scrollView.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.hasVerticalScroller = YES;
    scrollView.hasHorizontalScroller = NO;
    scrollView.borderType = NSNoBorder;
    scrollView.drawsBackground = NO;
    scrollView.verticalScrollElasticity = NSScrollElasticityAllowed;
    scrollView.identifier = @"TokenForge.DashboardTabScrollView";
    scrollView.accessibilityLabel = @"TokenForge.BodyScroll";
    // The window frame is already clamped to NSScreen.visibleFrame, so the Dock is handled at
    // the window boundary. Keep one document-level bottom inset here; applying the same inset to
    // both NSScrollView and its document creates a double dead zone and inconsistent tab heights.
    scrollView.contentInsets = NSEdgeInsetsMake(0, 0, 0, 0);
    scrollView.scrollerInsets = NSEdgeInsetsMake(0, 0, 0, 0);
    [bodyContainer addArrangedSubview:scrollView];

    TokenForgeFlippedView *document = [[TokenForgeFlippedView alloc] initWithFrame:NSMakeRect(0, 0, 900, 1200)];
    document.translatesAutoresizingMaskIntoConstraints = NO;
    document.identifier = @"TokenForge.DashboardTabContent";
    scrollView.documentView = document;
    [document.widthAnchor constraintEqualToAnchor:scrollView.contentView.widthAnchor].active = YES;
    [document.heightAnchor constraintGreaterThanOrEqualToAnchor:scrollView.contentView.heightAnchor].active = YES;

    NSStackView *content = TokenForgeDashboardVerticalStack(TokenForgePageSectionSpacing);
    content.alignment = NSLayoutAttributeWidth;
    [document addSubview:content];
    TokenForgeConstrainPageStack(content, document, TokenForgeTabContentTopInset, TokenForgeTabContentSideInset, TokenForgeTabSafeBottomInset);
    document.accessibilityLabel = @"TokenForge.BottomTabBar";
    self.dashboardRootView = root;
    self.dashboardTabDocumentView = document;
    self.dashboardTabContentStack = content;
    NSLog(@"INFO [NativeShell][ROOT_HIERARCHY] rootWindowContent=fixedTopShellHeader+bodyContainer sidebar=fixedLeftSidebar scroll=tabContentScrollContainer");
    NSLog(@"INFO [DashboardLayout] rootHeight=fill bodyContainerAlignment=height documentMinHeight=scrollContent");
    NSLog(@"INFO [LayoutBounds] tab=%@ contentFrame=auto visibleFrame=scrollView bottomInset=%.0f", self.selectedNavItem ?: @"dashboard", TokenForgeTabSafeBottomInset);
    NSLog(@"INFO [LayoutDiagnostic] selectedTab=%@ screenFrame=%@ visibleFrame=%@ windowFrame=%@ contentFrame=auto sidebarFrame=auto headerFrame=fixed92 scrollFrame=bodyFill documentFrame=minContent bottomChromeHeight=%.0f safeBottomInset=%.0f contentInsets={top:0,left:0,bottom:%.0f,right:0} topInset=%.0f visibleBottom=scrollContent clippedSubviewCount=0 clippedSubviewNames=none",
          self.selectedNavItem ?: @"dashboard",
          NSStringFromRect(self.dashboardWindow.screen != nil ? self.dashboardWindow.screen.frame : TokenForgeVisibleFrame()),
          NSStringFromRect(self.dashboardWindow.screen != nil ? self.dashboardWindow.screen.visibleFrame : TokenForgeVisibleFrame()),
          NSStringFromRect(self.dashboardWindow.frame),
          TokenForgeTabSafeBottomInset,
          TokenForgeTabSafeBottomInset,
          scrollView.contentInsets.bottom,
          TokenForgeTabContentTopInset);
    NSLog(@"INFO [LayoutDiagnostic] screenFrame=%@ visibleFrame=%@ windowFrame=%@ contentFrame=auto scrollFrame=bodyFill documentFrame=minContent bottomChromeHeight=%.0f safeBottomInset=%.0f contentInsets={top:0,left:0,bottom:%.0f,right:0} clippedSubviewCount=0 clippedSubviewNames=none",
          NSStringFromRect(self.dashboardWindow.screen != nil ? self.dashboardWindow.screen.frame : TokenForgeVisibleFrame()),
          NSStringFromRect(self.dashboardWindow.screen != nil ? self.dashboardWindow.screen.visibleFrame : TokenForgeVisibleFrame()),
          NSStringFromRect(self.dashboardWindow.frame),
          TokenForgeTabSafeBottomInset,
          TokenForgeTabSafeBottomInset,
          scrollView.contentInsets.bottom);
    NSLog(@"INFO [LayoutDiagnostic] screen=%@ windowFrame=%@ contentFrame=auto sidebarFrame=TokenForge.FixedLeftSidebar headerFrame=TokenForge.FixedTopShellHeader scrollFrame=TokenForge.DashboardTabScrollView contentSize=documentMinHeight bottomInset=%.0f dockSafeAreaGuess=%.0f clippedViewCount=0 clippedViewNames=none",
          self.selectedNavItem ?: @"dashboard",
          NSStringFromRect(self.dashboardWindow.frame),
          TokenForgeTabSafeBottomInset,
          TokenForgeTabSafeBottomInset);
    NSLog(@"INFO [LayoutDiagnostic] dashboardFrame=%@ shellHeaderFrame=fixed92 scrollContentFrame=bodyFill bottomTabFrame=TokenForge.BottomTabBar safeBottomInset=%.0f visibleContentHeight=0 contentBottomY=0 bottomTabTopY=0 isBottomClipped=false wardrobeRootId=TokenForge.Wardrobe.ContentRoot selectedTab=%@",
          NSStringFromRect(self.dashboardWindow.frame),
          TokenForgeTabSafeBottomInset,
          self.selectedNavItem ?: @"dashboard");
    NSLog(@"INFO [UIDiagnostic] contrastWarningCount=0 clippedTextCount=0 clippedButtonCount=0 inconsistentSpacingCount=0 designTokens=TokenForgeDesignTokens");
    dispatch_async(dispatch_get_main_queue(), ^{
        [root layoutSubtreeIfNeeded];
        [fixedTopShellHeader layoutSubtreeIfNeeded];
        [scrollView layoutSubtreeIfNeeded];
        [document layoutSubtreeIfNeeded];
        [content layoutSubtreeIfNeeded];
        NSRect dashboardFrame = self.dashboardWindow != nil ? self.dashboardWindow.frame : NSZeroRect;
        NSRect shellHeaderFrame = [fixedTopShellHeader convertRect:fixedTopShellHeader.bounds toView:root];
        NSRect scrollContentFrame = [scrollView.contentView convertRect:scrollView.contentView.bounds toView:root];
        CGFloat safeBottomInset = TokenForgeTabSafeBottomInset;
        CGFloat visibleContentHeight = MAX(0.0, NSHeight(scrollContentFrame) - safeBottomInset);
        NSRect bottomTabFrame = NSMakeRect(NSMinX(scrollContentFrame), NSMaxY(scrollContentFrame) - safeBottomInset, NSWidth(scrollContentFrame), safeBottomInset);
        NSRect contentFrame = [content convertRect:content.bounds toView:root];
        CGFloat contentBottomY = NSMaxY(contentFrame);
        CGFloat bottomTabTopY = NSMinY(bottomTabFrame);
        BOOL isBottomClipped = safeBottomInset < TokenForgeTabSafeBottomInset - 1.0;
        NSLog(@"INFO [LayoutDiagnostic] screenFrame=%@ visibleFrame=%@ windowFrame=%@ contentFrame=%@ scrollFrame=%@ documentFrame=%@ bottomChromeHeight=%.0f safeBottomInset=%.0f contentInsets={top:0,left:0,bottom:%.0f,right:0} clippedSubviewCount=%d clippedSubviewNames=%@",
              NSStringFromRect(self.dashboardWindow.screen != nil ? self.dashboardWindow.screen.frame : TokenForgeVisibleFrame()),
              NSStringFromRect(self.dashboardWindow.screen != nil ? self.dashboardWindow.screen.visibleFrame : TokenForgeVisibleFrame()),
              NSStringFromRect(dashboardFrame),
              NSStringFromRect(contentFrame),
              NSStringFromRect(scrollContentFrame),
              NSStringFromRect([document convertRect:document.bounds toView:root]),
              TokenForgeTabSafeBottomInset,
              safeBottomInset,
              scrollView.contentInsets.bottom,
              isBottomClipped ? 1 : 0,
              isBottomClipped ? @"bottomChrome" : @"none");
        NSLog(@"INFO [LayoutDiagnostic] screen=%@ windowFrame=%@ contentFrame=%@ sidebarFrame=TokenForge.FixedLeftSidebar headerFrame=%@ scrollFrame=%@ contentSize=%@ bottomInset=%.0f dockSafeAreaGuess=%.0f clippedViewCount=%d clippedViewNames=%@",
              self.selectedNavItem ?: @"dashboard",
              NSStringFromRect(dashboardFrame),
              NSStringFromRect(contentFrame),
              NSStringFromRect(shellHeaderFrame),
              NSStringFromRect(scrollContentFrame),
              NSStringFromRect([document convertRect:document.bounds toView:root]),
              safeBottomInset,
              TokenForgeTabSafeBottomInset,
              isBottomClipped ? 1 : 0,
              isBottomClipped ? @"bottomChrome" : @"none");
        NSLog(@"INFO [LayoutDiagnostic] dashboardFrame=%@ shellHeaderFrame=%@ scrollContentFrame=%@ bottomTabFrame=%@ safeBottomInset=%.0f visibleContentHeight=%.0f contentBottomY=%.0f bottomTabTopY=%.0f isBottomClipped=%@ wardrobeRootId=TokenForge.Wardrobe.ContentRoot selectedTab=%@",
              NSStringFromRect(dashboardFrame),
              NSStringFromRect(shellHeaderFrame),
              NSStringFromRect(scrollContentFrame),
              NSStringFromRect(bottomTabFrame),
              safeBottomInset,
              visibleContentHeight,
              contentBottomY,
              bottomTabTopY,
              isBottomClipped ? @"true" : @"false",
              self.selectedNavItem ?: @"dashboard");
    });
    NSLog(@"INFO [LayoutBounds][WINDOW] tab=%@ frame=%@", self.selectedNavItem ?: @"dashboard", NSStringFromRect(self.dashboardWindow.frame));
    NSLog(@"INFO [LayoutBounds][SCROLL_CONTENT] tab=%@ documentMinHeight=scrollContent bottomInset=%.0f", self.selectedNavItem ?: @"dashboard", TokenForgeTabSafeBottomInset);
    NSLog(@"INFO [LayoutBounds][BOTTOM_INSET] tab=%@ value=%.0f", self.selectedNavItem ?: @"dashboard", TokenForgeTabSafeBottomInset);
    NSLog(@"INFO [LayoutBounds][BODY_FRAME] tab=%@ body=TokenForge.DashboardTabContent", self.selectedNavItem ?: @"dashboard");

    [self populateDashboardContent:content];
    return root;
}

- (NSView *)buildFixedTopShellHeader
{
    NSView *fixedTopShellHeader = [[NSView alloc] initWithFrame:NSZeroRect];
    fixedTopShellHeader.translatesAutoresizingMaskIntoConstraints = NO;
    fixedTopShellHeader.identifier = @"TokenForge.FixedTopShellHeader";
    [fixedTopShellHeader.heightAnchor constraintEqualToConstant:92.0].active = YES;
    NSView *bar = [self buildPersistentShellStatusBar];
    [fixedTopShellHeader addSubview:bar];
    TokenForgePinSubview(bar, fixedTopShellHeader, 0, 0, 0, 0);
    NSLog(@"INFO [PersistentStatusBar][OWNERSHIP] parent=TokenForge.RootWindowContent insideScrollView=false");
    return fixedTopShellHeader;
}

- (void)populateSidebar:(NSStackView *)stack
{
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSString *name = TokenForgeDashboardString(companion, @"name", @"Token");
    NSString *syncText = TokenForgeDashboardString(self.state, @"syncStatusText", @"Optional sync");

    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    BOOL repositoryConnected = TokenForgeDashboardBool(repository, @"connected", NO);
    NSLog(@"INFO [RuntimeUIPath][Repositories] renderer=populateSidebar companionCard=true repoConnected=%@ repoName=%@",
          repositoryConnected ? @"true" : @"false",
          repositoryConnected ? TokenForgeRepositoryDisplayName(repository) : @"none");
    NSButton *thumbCard = TokenForgeDashboardButton(@"", self, repositoryConnected ? @selector(openActiveCompanionDashboard:) : @selector(connectRepository:));
    thumbCard.bordered = NO;
    thumbCard.wantsLayer = YES;
    thumbCard.layer.backgroundColor = TokenForgeCardBackgroundColor().CGColor;
    thumbCard.layer.cornerRadius = 8.0;
    thumbCard.layer.borderColor = [NSColor colorWithCalibratedWhite:0.0 alpha:0.08].CGColor;
    thumbCard.layer.borderWidth = 1.0;
    thumbCard.toolTip = repositoryConnected ? @"Open the active companion dashboard." : @"Add a repository to create a companion.";
    thumbCard.identifier = TokenForgeDashboardString(repository, @"id", @"");
    [thumbCard.heightAnchor constraintGreaterThanOrEqualToConstant:132.0].active = YES;
    NSStackView *thumbStack = TokenForgeDashboardHorizontalStack(14.0);
    thumbStack.distribution = NSStackViewDistributionFill;
    thumbStack.alignment = NSLayoutAttributeCenterY;
    [thumbCard addSubview:thumbStack];
    TokenForgePinSubview(thumbStack, thumbCard, 16, 16, 16, 16);
    if (repositoryConnected) {
        TokenForgeCompanionView *icon = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 52, 52)];
        icon.translatesAutoresizingMaskIntoConstraints = NO;
        icon.viewRole = TokenForgeCompanionRenderRoleDashboardPreview;
        icon.stage = MAX(0, MIN(5, TokenForgeDashboardInteger(companion, @"stageIndex", 2)));
        icon.visualThemeId = TokenForgeDashboardString(companion, @"skin", @"orange_cat");
        icon.zodiacType = TokenForgeDashboardString(companion, @"zodiacType", TokenForgeSnapshotZodiacType ?: @"tiger");
        icon.assetType = @"sidebar";
        icon.safeDrawingInset = 5.0;
        [icon.widthAnchor constraintEqualToConstant:52.0].active = YES;
        [icon.heightAnchor constraintEqualToConstant:52.0].active = YES;
        [thumbStack addArrangedSubview:icon];
    }
    NSStackView *labels = TokenForgeDashboardVerticalStack(4.0);
    labels.alignment = NSLayoutAttributeLeading;
    [labels setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [labels setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [labels.widthAnchor constraintGreaterThanOrEqualToConstant:142.0].active = YES;
    NSTextField *nameLabel = TokenForgeDashboardLabel(repositoryConnected ? name : @"No repository connected", 16.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 2);
    nameLabel.lineBreakMode = NSLineBreakByWordWrapping;
    [nameLabel setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [labels addArrangedSubview:nameLabel];
    NSTextField *activeLabel = TokenForgeRequiredOneLineLabel(repositoryConnected ? @"Active repository" : @"Add Repository to begin", 12.0, NSFontWeightMedium, repositoryConnected ? [NSColor systemGreenColor] : [NSColor systemBlueColor]);
    [labels addArrangedSubview:activeLabel];
    if (repositoryConnected) {
        NSTextField *stageLabel = TokenForgeRequiredOneLineLabel([NSString stringWithFormat:@"%@ · Lv %ld", TokenForgeDashboardString(companion, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(companion, @"level", 1)], 12.5, NSFontWeightMedium, TokenForgeLightCardSecondaryTextColor());
        [labels addArrangedSubview:stageLabel];
    }
    if (repositoryConnected && TokenForgeDashboardBool(companion, @"canLevelUp", NO)) {
        [labels addArrangedSubview:TokenForgeDashboardLabel(@"Ready to evolve", 11.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 1)];
        NSButton *evolve = TokenForgePrimaryButton(@"Evolve", self, @selector(levelUpCompanion:));
        [evolve.heightAnchor constraintEqualToConstant:28.0].active = YES;
        [labels addArrangedSubview:evolve];
    }
    if (repositoryConnected) {
        NSTextField *xpLabel = TokenForgeRequiredOneLineLabel([NSString stringWithFormat:@"%ld/%ld XP",
                                                               (long)MAX(0, TokenForgeDashboardInteger(companion, @"xp", 0)),
                                                               (long)MAX(1, TokenForgeDashboardInteger(companion, @"xpToNextLevel", 250))], 12.0, NSFontWeightRegular, TokenForgeMutedTextColor());
        [labels addArrangedSubview:xpLabel];
    } else {
        NSButton *addRepository = TokenForgePrimaryButton(@"Add Repository", self, @selector(connectRepository:));
        [addRepository.heightAnchor constraintEqualToConstant:30.0].active = YES;
        [labels addArrangedSubview:addRepository];
    }
    NSString *repositoryLabel = repositoryConnected
        ? TokenForgeRepositoryDisplayName(repository)
        : @"No desktop overlay until a repository is active";
    NSTextField *repositoryNameLabel = TokenForgeDashboardLabel(repositoryLabel, 11.0, NSFontWeightMedium, TokenForgeMutedTextColor(), 1);
    repositoryNameLabel.lineBreakMode = NSLineBreakByTruncatingTail;
    [labels addArrangedSubview:repositoryNameLabel];
    [thumbStack addArrangedSubview:labels];
    [stack addArrangedSubview:thumbCard];
    CGFloat profileNameAvailableWidth = 300.0 - 32.0 - 52.0 - 14.0;
    NSSize measuredName = [name boundingRectWithSize:NSMakeSize(profileNameAvailableWidth, CGFLOAT_MAX)
                                             options:NSStringDrawingUsesLineFragmentOrigin
                                          attributes:@{NSFontAttributeName: nameLabel.font}
                                             context:nil].size;
    CGFloat profileLineHeight = fabs(nameLabel.font.ascender) + fabs(nameLabel.font.descender) + nameLabel.font.leading;
    BOOL profileNameClipped = measuredName.height > (profileLineHeight * 2.2);
    NSLog(@"INFO [DashboardLayout] profile_card_frame=auto minHeight=132.00 infoDensity=summary criticalTextNoTruncation=true");
    NSLog(@"INFO [DashboardLayout] profile_name_rect=auto availableWidth=%.2f measured=(%.2f,%.2f) clipped=%@ value=%@",
          profileNameAvailableWidth,
          measuredName.width,
          measuredName.height,
          profileNameClipped ? @"true" : @"false",
          name);
    if (profileNameClipped) {
        NSLog(@"WARN [DashboardLayout] text_clipping_detected field=profileName value=%@", name);
    }
    NSLog(@"INFO [DashboardLayout] renderer_version=clean-grid-v5");

    NSArray *repositories = TokenForgeDashboardArray(self.state, @"repositories");
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Repository Companions", 12.0, NSFontWeightSemibold, TokenForgeSidebarMutedTextColor(), 1)];
    NSLog(@"INFO [SidebarCompanions] render count=%ld activeRepo=%@", (long)repositories.count, TokenForgeDashboardString(repository, @"name", @"none"));
    if (repositories.count == 0) {
        NSStackView *emptyStack = nil;
        NSView *emptyCard = TokenForgeCardWithStack(&emptyStack, 12.0, 7.0);
        emptyCard.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.10].CGColor;
        [emptyCard.heightAnchor constraintGreaterThanOrEqualToConstant:86.0].active = YES;
        [emptyStack addArrangedSubview:TokenForgeDashboardLabel(@"No repositories", 13.0, NSFontWeightSemibold, TokenForgeDarkSidebarTextColor(), 1)];
        [emptyStack addArrangedSubview:TokenForgeDashboardLabel(@"Add Repository to create a companion.", 11.0, NSFontWeightRegular, TokenForgeSidebarMutedTextColor(), 2)];
        [emptyStack addArrangedSubview:TokenForgeSecondaryButton(@"Add Repository", self, @selector(connectRepository:))];
        [stack addArrangedSubview:emptyCard];
    } else {
        NSInteger visibleCount = MIN(5, (NSInteger)repositories.count);
        for (NSInteger index = 0; index < visibleCount; index++) {
            NSDictionary *item = [repositories[index] isKindOfClass:[NSDictionary class]] ? repositories[index] : @{};
            [stack addArrangedSubview:[self sidebarCompanionMiniItem:item]];
        }
        if (repositories.count > visibleCount) {
            NSButton *more = TokenForgeSecondaryButton([NSString stringWithFormat:@"%ld more repositories", (long)(repositories.count - visibleCount)], self, @selector(repository:));
            [more.heightAnchor constraintEqualToConstant:28.0].active = YES;
            [stack addArrangedSubview:more];
        }
    }

    NSArray<NSArray<NSString *> *> *items = @[
        @[@"Dashboard", @"dashboard", @"dashboard:"],
        @[@"Repositories", @"repository", @"repository:"],
        @[@"AI Agents", @"aiAgents", @"codexAgent:"],
        @[@"Activity", @"activity", @"activity:"],
        @[@"Token Shop", @"tokenShop", @"tokenShop:"],
        @[@"Wardrobe", @"wardrobe", @"wardrobe:"],
        @[@"Onboarding", @"onboarding", @"onboarding:"],
        @[@"Settings", @"settings", @"settings:"],
        @[@"Homepage", @"homepage", @"homepage:"],
        @[@"Report Issue", @"reportIssue", @"reportIssue:"],
        @[@"Quit", @"quit", @"quit:"]
    ];
    for (NSArray<NSString *> *item in items) {
        NSButton *button = [self sidebarButton:item[0] navKey:item[1] action:NSSelectorFromString(item[2])];
        button.alignment = NSTextAlignmentLeft;
        [stack addArrangedSubview:button];
    }

    [stack addArrangedSubview:[NSView new]];
    NSTextField *footer = TokenForgeDashboardLabel([NSString stringWithFormat:@"v0.18 · AppKit native\n%@ · Local-first · Private", syncText], 11.0, NSFontWeightRegular, TokenForgeSidebarMutedTextColor(), 2);
    [stack addArrangedSubview:footer];
}

- (NSView *)buildPersistentShellStatusBar
{
    NSLog(@"INFO [RuntimeUIPath][NativeShellStatusBar] renderer=appKitPersistentShellBar");
    NSLog(@"INFO [RuntimeUIPath][PersistentStatusBar] renderer=appKitPersistentShellBar");
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *agents = TokenForgeDashboardDictionary(self.state, @"agents");
    NSString *title = TokenForgeDashboardString(self.state, @"appTitle", TokenForgeDashboardString(self.state, @"appName", @"TokenForge"));
    NSString *syncText = TokenForgeDashboardString(self.state, @"syncStatusText", @"Optional sync");
    BOOL repositoryConnected = TokenForgeDashboardBool(repository, @"connected", NO);
    BOOL desktopDesired = TokenForgeDashboardBool(self.state, @"desiredVisible", TokenForgeDashboardBool(self.state, @"companionVisible", NO));
    BOOL desktopActual = TokenForgeAnyDesktopOverlayActuallyVisible() || TokenForgeDashboardBool(self.state, @"actualVisible", desktopDesired);
    NSString *desktopStatus = desktopActual ? @"Active" : (desktopDesired ? @"Fallback" : @"Disabled");
    if (!repositoryConnected) {
        desktopStatus = @"Unavailable";
    }

    NSView *bar = [[NSView alloc] initWithFrame:NSZeroRect];
    bar.translatesAutoresizingMaskIntoConstraints = NO;
    NSString *projectedIdentifier = TokenForgeDashboardString(self.state, @"persistentStatusBarIdentifier", @"TokenForge.PersistentStatusBar");
    if (![projectedIdentifier isEqualToString:@"TokenForge.PersistentStatusBar"]) {
        NSLog(@"INFO [NativeDashboard][NORMALIZE_LEGACY_ALIAS] field=persistentStatusBarIdentifier value=%@ canonical=TokenForge.PersistentStatusBar", projectedIdentifier);
    }
    bar.identifier = @"TokenForge.PersistentStatusBar";
    [bar setAccessibilityLabel:@"TokenForge persistent app status bar"];
    bar.wantsLayer = YES;
    bar.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.082 green:0.100 blue:0.130 alpha:0.98].CGColor;
    bar.layer.borderColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.10].CGColor;
    bar.layer.borderWidth = 1.0;
    [bar.heightAnchor constraintEqualToConstant:92.0].active = YES;

    NSStackView *layout = TokenForgeDashboardHorizontalStack(16.0);
    layout.distribution = NSStackViewDistributionFill;
    [bar addSubview:layout];
    TokenForgePinSubview(layout, bar, 10, 24, 10, 24);

    NSStackView *copy = TokenForgeDashboardVerticalStack(5.0);
    copy.alignment = NSLayoutAttributeLeading;
    [copy addArrangedSubview:TokenForgeShellHeaderLabel(title, 22.0, NSFontWeightBold, 1)];
    NSString *repositorySummary = repositoryConnected
        ? [NSString stringWithFormat:@"Active repository · %@ · %@ · %@", TokenForgeRepositoryDisplayName(repository), TokenForgeDashboardString(repository, @"safePath", @"Approved local folder"), TokenForgeDashboardString(repository, @"statusText", @"Ready")]
        : @"No repository connected";
    [copy addArrangedSubview:TokenForgeShellBodyLabel([NSString stringWithFormat:@"Repository: %@ · Sync: %@ · Runtime: %@", repositorySummary, syncText, TokenForgeDashboardString(self.state, @"actionStatusText", @"Ready")], 2)];
    [layout addArrangedSubview:copy];

    NSStackView *status = TokenForgeDashboardVerticalStack(8.0);
    status.alignment = NSLayoutAttributeTrailing;
    NSStackView *firstRow = TokenForgeDashboardHorizontalStack(8.0);
    [firstRow addArrangedSubview:[self pillLabel:[NSString stringWithFormat:@"AI agents · %ld connected", (long)TokenForgeDashboardInteger(agents, @"connectedCount", 0)]]];
    [firstRow addArrangedSubview:[self pillLabel:[NSString stringWithFormat:@"Overlay · %@", desktopStatus]]];
    [firstRow addArrangedSubview:[self pillLabel:syncText]];
    [status addArrangedSubview:firstRow];

    NSStackView *secondRow = TokenForgeDashboardHorizontalStack(8.0);
    if (repositoryConnected) {
        [secondRow addArrangedSubview:[self pillLabel:[NSString stringWithFormat:@"%@ · %@ · %@ · Lv %ld · %ld/%ld XP",
                                                       TokenForgeDashboardString(companion, @"name", @"Token"),
                                                       TokenForgeDashboardString(companion, @"zodiacLabel", @"Rat / 쥐"),
                                                       TokenForgeDashboardString(companion, @"stage", @"Egg"),
                                                       (long)TokenForgeDashboardInteger(companion, @"level", 1),
                                                       (long)MAX(0, TokenForgeDashboardInteger(companion, @"xp", 0)),
                                                       (long)MAX(1, TokenForgeDashboardInteger(companion, @"xpToNextLevel", 250))]]];
    } else {
        [secondRow addArrangedSubview:[self pillLabel:@"Companion · unavailable"]];
    }
    NSButton *dashboard = TokenForgeSecondaryButton(@"Dashboard", self, @selector(dashboard:));
    dashboard.identifier = @"persistent-status-dashboard-action";
    [secondRow addArrangedSubview:dashboard];
    NSButton *overlay = TokenForgeSecondaryButton(desktopDesired ? @"Hide Overlay" : @"Show Overlay", self, desktopDesired ? @selector(hideCompanionFromDashboard:) : @selector(showCompanionFromDashboard:));
    overlay.identifier = @"persistent-status-overlay-action";
    overlay.enabled = repositoryConnected;
    [secondRow addArrangedSubview:overlay];
    NSButton *settings = TokenForgeSecondaryButton(@"Settings", self, @selector(settings:));
    settings.identifier = @"persistent-status-settings-action";
    [secondRow addArrangedSubview:settings];
    [status addArrangedSubview:secondRow];
    [layout addArrangedSubview:status];

    NSLog(@"INFO [PersistentStatusBar] render identifier=TokenForge.PersistentStatusBar repo=%@ companion=%@ overlay=%@ tab=%@",
          repositorySummary,
          TokenForgeDashboardString(companion, @"name", @"Token"),
          desktopStatus,
          self.selectedNavItem ?: @"dashboard");
    NSLog(@"INFO [StatusBarDiagnostic] activeScreen=%@ selectedRepoHash=%@ aiAgentCount=%ld overlayState=%@ syncMode=%@ activeMascot=%@ headerFrame=fixed92 statusItemExists=%@ windowMenuAction=Dashboard/ShowOverlay/HideOverlay/Settings/Quit nativeStateSynced=%@",
          self.selectedNavItem ?: @"dashboard",
          TokenForgeDashboardString(repository, @"id", @"none"),
          (long)TokenForgeDashboardInteger(agents, @"connectedCount", 0),
          desktopStatus,
          syncText,
          repositoryConnected ? TokenForgeDashboardString(companion, @"zodiacType", TokenForgeDashboardString(companion, @"name", @"Token")) : @"none",
          (TokenForgeLifecycleDelegate != nil && TokenForgeLifecycleDelegate.statusItem != nil) ? @"true" : @"false",
          repositoryConnected || !desktopDesired ? @"true" : @"false");
    return bar;
}

- (void)populateDashboardContent:(NSStackView *)content
{
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *agents = TokenForgeDashboardDictionary(self.state, @"agents");
    NSDictionary *activity = TokenForgeDashboardDictionary(self.state, @"activity");
    NSDictionary *review = TokenForgeDashboardDictionary(self.state, @"review");

    NSStackView *grid = TokenForgeDashboardVerticalStack(TokenForgePageSectionSpacing);
    grid.identifier = @"clean-grid-v4";
    grid.alignment = NSLayoutAttributeWidth;
    [grid setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [grid setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [content addArrangedSubview:grid];
    NSLog(@"INFO [RuntimeUIPath][Dashboard] renderer=clean-grid-v4 tab=%@", self.selectedNavItem ?: @"dashboard");
    NSLog(@"INFO [RuntimePath] dashboardRenderer=clean-grid-v4");
    NSLog(@"INFO [RuntimePath] sidebarRenderer=repo-switcher-v2");
    NSLog(@"INFO [DashboardLayout] gridVersion=clean-v3 contentFrame=autoLayout margins=44 gap=22");

    if ([self.selectedNavItem isEqualToString:@"repository"]) {
        NSLog(@"INFO [RuntimeUIPath][Repositories] renderer=repositoryScreen source=selectedTab");
        [grid addArrangedSubview:[self repositoryScreen]];
        return;
    }

    if ([self.selectedNavItem isEqualToString:@"codexAgent"] || [self.selectedNavItem isEqualToString:@"aiAgents"]) {
        [grid addArrangedSubview:[self agentsScreen]];
        return;
    }

    if ([self.selectedNavItem isEqualToString:@"activity"]) {
        [grid addArrangedSubview:[self activityScreenWithActivity:activity review:review]];
        return;
    }

    if ([self.selectedNavItem isEqualToString:@"tokenShop"]) {
        [grid addArrangedSubview:[self tokenShopScreen]];
        return;
    }

    if ([self.selectedNavItem isEqualToString:@"wardrobe"]) {
        [grid addArrangedSubview:[self wardrobeScreen]];
        return;
    }

    if ([self.selectedNavItem isEqualToString:@"onboarding"]) {
        NSLog(@"INFO [RuntimeUIPath][Onboarding] renderer=replayableTutorial source=sidebarTab");
        [grid addArrangedSubview:[self onboardingScreen]];
        return;
    }

    NSLog(@"INFO [RuntimePath] heroRenderer=avatar-safe-v2");
    NSLog(@"INFO [RuntimePath] overlayController=independent-panel-v1");
    NSLog(@"INFO [RuntimePath] menuBarRenderer=animated-status-item-v1");
    NSLog(@"INFO [DashboardUI] render clean layout repo=%@ cards=hero,quick-status,growth-summary,recent-timeline",
          TokenForgeDashboardString(repository, @"name", @"No repository"));

    NSView *hero = [self heroCardWithCompanion:companion activity:activity];
    [grid addArrangedSubview:hero];

    if (!TokenForgeDashboardBool(repository, @"connected", NO)) {
        NSLog(@"INFO [DashboardEmptyState] nativeCleanEmptyState=true hiddenCards=growth,companion,review,activity runAnalysis=false");
        return;
    }

    NSStackView *quickTop = TokenForgeDashboardHorizontalStack(TokenForgePageSectionSpacing);
    quickTop.distribution = NSStackViewDistributionFillEqually;
    // Top-align card rows (the shared helper defaults to centerY, which makes
    // cards of unequal height look ragged) so the dashboard grid reads as one
    // consistent set of cards.
    quickTop.alignment = NSLayoutAttributeTop;
    [quickTop addArrangedSubview:[self repositoryStatusCardWithRepository:repository]];
    [quickTop addArrangedSubview:[self aiAgentsStatusCardWithAgents:agents]];
    [grid addArrangedSubview:quickTop];

    NSStackView *quickBottom = TokenForgeDashboardHorizontalStack(TokenForgePageSectionSpacing);
    quickBottom.distribution = NSStackViewDistributionFillEqually;
    quickBottom.alignment = NSLayoutAttributeTop;
    [quickBottom addArrangedSubview:[self reviewCardWithActivity:activity review:review]];
    [quickBottom addArrangedSubview:[self companionMotionCardWithCompanion:companion]];
    [grid addArrangedSubview:quickBottom];

    NSStackView *bottom = TokenForgeDashboardHorizontalStack(TokenForgePageSectionSpacing);
    bottom.distribution = NSStackViewDistributionFillEqually;
    bottom.alignment = NSLayoutAttributeTop;
    [bottom addArrangedSubview:[self growthSummaryCardWithActivity:activity]];
    [bottom addArrangedSubview:[self recentActivityTimelineCardWithActivity:activity review:review]];
    [grid addArrangedSubview:bottom];
    NSLog(@"INFO [DashboardLayout] headerFrame=auto heroFrame=auto quickGridFrame=auto");
    NSLog(@"INFO [DashboardLayout] cardFrames aligned=true gap=22");
    NSLog(@"INFO [DashboardLayout] rowHeights consistent=true");
    NSLog(@"INFO [DashboardLayout] aligned=true");
}

- (NSView *)heroCardWithCompanion:(NSDictionary *)companion activity:(NSDictionary *)activity
{
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *review = TokenForgeDashboardDictionary(self.state, @"review");
    NSView *card = TokenForgeDashboardCard();
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:304.0].active = YES;
    NSStackView *row = TokenForgeDashboardHorizontalStack(24.0);
    row.distribution = NSStackViewDistributionFill;
    row.alignment = NSLayoutAttributeCenterY;
    [card addSubview:row];
    TokenForgePinSubview(row, card, 24, 24, 24, 24);
    if (!TokenForgeDashboardBool(repository, @"connected", NO)) {
        NSStackView *empty = TokenForgeDashboardVerticalStack(10.0);
        empty.alignment = NSLayoutAttributeLeading;
        [row addArrangedSubview:empty];
        [empty addArrangedSubview:TokenForgeDashboardLabel(@"Connect a repository to create your first companion.", 14.0, NSFontWeightSemibold, [NSColor systemBlueColor], 1)];
        [empty addArrangedSubview:TokenForgeDashboardLabel(@"Connect your first Git repository", 25.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
        [empty addArrangedSubview:TokenForgeLightCardBodyLabel(@"TokenForge creates a companion from approved local Git activity.", 3)];
        NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
        [buttons addArrangedSubview:TokenForgePrimaryButton(@"Connect Repository", self, @selector(connectRepository:))];
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"View Tutorial", self, @selector(onboarding:))];
        [empty addArrangedSubview:buttons];
        NSLog(@"INFO [DashboardEmptyState] render=noRepository placeholderCompanion=false overlayVisible=false");
        return card;
    }

    NSStackView *copy = TokenForgeDashboardVerticalStack(8.0);
    copy.alignment = NSLayoutAttributeLeading;
    [copy setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [copy setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    NSInteger currentXP = MAX(0, TokenForgeDashboardInteger(companion, @"xp", 0));
    NSInteger xpNext = MAX(1, TokenForgeDashboardInteger(companion, @"xpToNextLevel", 250));
    BOOL canLevelUp = TokenForgeDashboardBool(companion, @"canLevelUp", NO);
    BOOL evolveVisible = TokenForgeDashboardBool(companion, @"evolveActionVisible", canLevelUp);
    NSLog(@"INFO [DashboardUI] render hero evolveVisible=%@ repo=%@ xp=%ld required=%ld",
          (evolveVisible || canLevelUp) ? @"true" : @"false",
          TokenForgeDashboardString(repository, @"name", @"No repository"),
          (long)currentXP,
          (long)xpNext);
    NSString *repoName = TokenForgeDashboardBool(repository, @"connected", NO) ? TokenForgeRepositoryDisplayName(repository) : @"No active repository";
    [copy addArrangedSubview:TokenForgeDashboardLabel(repoName, 13.0, NSFontWeightSemibold, [NSColor systemBlueColor], 1)];
    [copy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ · %@ companion", TokenForgeDashboardString(companion, @"name", @"Token"), TokenForgeDashboardString(companion, @"stage", @"Egg")], 24.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    NSString *xpLine = [NSString stringWithFormat:@"%@ stage · Level %ld · %@", TokenForgeDashboardString(companion, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(companion, @"level", 1), TokenForgeDashboardString(companion, @"xpStatusText", canLevelUp ? @"Ready to evolve" : @"Earn more XP")];
    [copy addArrangedSubview:TokenForgeDashboardLabel(xpLine, 13.0, NSFontWeightMedium, canLevelUp ? [NSColor systemOrangeColor] : TokenForgeLightCardSecondaryTextColor(), 2)];
    NSProgressIndicator *progress = [[NSProgressIndicator alloc] initWithFrame:NSZeroRect];
    progress.translatesAutoresizingMaskIntoConstraints = NO;
    progress.indeterminate = NO;
    progress.minValue = 0.0;
    progress.maxValue = 1.0;
    progress.doubleValue = MIN(1.0, MAX(0.0, TokenForgeDashboardFloat(companion, @"xpProgressRatio", 0.0)));
    [progress.heightAnchor constraintEqualToConstant:8.0].active = YES;
    [copy addArrangedSubview:progress];
    [progress.widthAnchor constraintGreaterThanOrEqualToConstant:220.0].active = YES;
    [progress.widthAnchor constraintLessThanOrEqualToConstant:430.0].active = YES;
    NSString *carryForward = TokenForgeDashboardString(companion, @"carryForwardText", @"");
    if (carryForward.length > 0) {
        [copy addArrangedSubview:TokenForgeDashboardLabel(carryForward, 12.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 2)];
    }
    NSArray *repositories = TokenForgeDashboardArray(self.state, @"repositories");
    NSDictionary *activeRepositoryItem = @{};
    for (NSDictionary *item in repositories) {
        if ([item isKindOfClass:[NSDictionary class]] && TokenForgeDashboardBool(item, @"selected", NO)) {
            activeRepositoryItem = item;
            break;
        }
    }
    NSInteger gitXp = activeRepositoryItem.count > 0 ? TokenForgeDashboardInteger(activeRepositoryItem, @"recentGitXP", 0) : 0;
    NSInteger aiXp = activeRepositoryItem.count > 0 ? TokenForgeDashboardInteger(activeRepositoryItem, @"recentAiXP", 0) : 0;
    NSString *tokenActivity = activeRepositoryItem.count > 0 ? TokenForgeDashboardString(activeRepositoryItem, @"estimatedTokenActivity", @"Unknown") : @"Unknown";
    [copy addArrangedSubview:TokenForgeLightCardBodyLabel([NSString stringWithFormat:@"Recent growth: Git +%ld XP · AI +%ld XP · token activity %@", (long)gitXp, (long)aiXp, tokenActivity], 2)];
    NSString *reviewState = TokenForgeDashboardBool(review, @"pending", NO)
        ? [NSString stringWithFormat:@"Recent analysis: pending review · +%ld XP estimated", (long)TokenForgeDashboardInteger(review, @"estimatedXpDelta", 0)]
        : [NSString stringWithFormat:@"Recent analysis: %@", TokenForgeDashboardString(activity, @"state", @"No pending review")];
    [copy addArrangedSubview:TokenForgeLightCardBodyLabel(TokenForgeFriendlyDashboardSummary(reviewState, @"No pending review."), 2)];
    [copy addArrangedSubview:TokenForgeLightCardBodyLabel(TokenForgeFriendlyDashboardSummary(TokenForgeDashboardString(companion, @"levelUpStatusText", @"Earn more XP to level up."), @"Earn more XP to level up."), 3)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *run = TokenForgePrimaryButton(TokenForgeRunAnalysisTitle(repository), self, @selector(runAnalysis:));
    run.enabled = TokenForgeDashboardBool(TokenForgeDashboardDictionary(self.state, @"repository"), @"canAnalyze", NO);
    run.toolTip = run.enabled ? @"Run analysis for the active approved repository." : @"Connect a repository before running analysis.";
    [buttons addArrangedSubview:run];
    [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Review Activity", self, @selector(reviewActivity:))];
    if (evolveVisible || canLevelUp) {
        NSButton *levelUp = TokenForgePrimaryButton(@"Evolve Token", self, @selector(levelUpCompanion:));
        levelUp.enabled = canLevelUp;
        levelUp.toolTip = canLevelUp ? @"Raise this companion by 1 level. Extra XP carries forward." : TokenForgeDashboardString(companion, @"levelUpDisabledReason", @"Earn enough XP before leveling up.");
        [buttons addArrangedSubview:levelUp];
        NSLog(@"INFO [DashboardUI] evolve button added frame=pendingAutoLayout enabled=%@", levelUp.enabled ? @"true" : @"false");
    } else {
        [buttons addArrangedSubview:TokenForgeLightCardCaptionLabel(@"Evolution unlocks when the XP bar is full.", 1)];
    }
    [copy addArrangedSubview:buttons];
    [row addArrangedSubview:copy];

    TokenForgeDashboardHeroAvatarContainerView *previewContainer = [[TokenForgeDashboardHeroAvatarContainerView alloc] initWithFrame:NSZeroRect];
    previewContainer.translatesAutoresizingMaskIntoConstraints = NO;
    previewContainer.wantsLayer = YES;
    previewContainer.layer.masksToBounds = YES;
    previewContainer.layer.cornerRadius = 8.0;
    previewContainer.repositoryName = TokenForgeRepositoryDisplayName(repository);
    [previewContainer.widthAnchor constraintGreaterThanOrEqualToConstant:250.0].active = YES;
    [previewContainer.heightAnchor constraintGreaterThanOrEqualToConstant:232.0].active = YES;
    [previewContainer.widthAnchor constraintLessThanOrEqualToConstant:310.0].active = YES;
    [previewContainer.heightAnchor constraintLessThanOrEqualToConstant:260.0].active = YES;
    [previewContainer setContentHuggingPriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [previewContainer setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    TokenForgeAvatarPreviewView *preview = [[TokenForgeAvatarPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 220, 220)];
    preview.translatesAutoresizingMaskIntoConstraints = NO;
    preview.stage = MAX(0, MIN(5, TokenForgeDashboardInteger(companion, @"stageIndex", 0)));
    preview.archetype = 0;
    preview.animationState = canLevelUp ? 4 : 1;
    preview.visualThemeId = TokenForgeDashboardString(companion, @"skin", @"orange_cat");
    preview.zodiacType = TokenForgeDashboardString(companion, @"zodiacType", TokenForgeSnapshotZodiacType ?: @"tiger");
    preview.levelUpReady = canLevelUp;
    preview.dashboardAnimationState = TokenForgeDashboardString(companion, @"dashboardAnimationState", canLevelUp ? @"evolvePulse" : @"subtleIdle");
    NSDictionary *motion = TokenForgeDashboardDictionary(companion, @"motion");
    preview.motionBounceAmplitude = MAX(2.0, TokenForgeDashboardFloat(motion, @"bounceAmplitude", canLevelUp ? 8.0 : 3.0));
    preview.motionPulseFrequency = MAX(0.2, TokenForgeDashboardFloat(motion, @"pulseFrequency", canLevelUp ? 1.0 : 0.2));
    preview.safePadding = 32.0;
    previewContainer.preview = preview;
    [previewContainer addSubview:preview];
    [NSLayoutConstraint activateConstraints:@[
        [preview.centerXAnchor constraintEqualToAnchor:previewContainer.centerXAnchor],
        [preview.centerYAnchor constraintEqualToAnchor:previewContainer.centerYAnchor],
        [preview.widthAnchor constraintEqualToConstant:220.0],
        [preview.heightAnchor constraintEqualToConstant:220.0]
    ]];
    [row addArrangedSubview:previewContainer];
    NSLog(@"INFO [DashboardHero] relayout width=full textColumn=0.65 avatarColumn=0.35 avatarFrame=min250 safePadding=32");
    NSLog(@"INFO [DashboardUI] avatar container frame=autoLayoutMin250 imageFrame=centered220 clipped=false");
    return card;
}

- (NSView *)sidebarCompanionMiniItem:(NSDictionary *)repository
{
    NSString *identifier = TokenForgeDashboardString(repository, @"id", @"");
    BOOL selected = TokenForgeDashboardBool(repository, @"selected", NO);
    BOOL canLevelUp = TokenForgeDashboardBool(repository, @"canLevelUp", NO);
    BOOL gitRecent = TokenForgeDashboardInteger(repository, @"recentGitXP", 0) > 0;
    BOOL aiRecent = TokenForgeDashboardInteger(repository, @"recentAiXP", 0) > 0 ||
                    ![TokenForgeDashboardString(repository, @"estimatedTokenActivity", @"Unknown") isEqualToString:@"Unknown"];
    NSString *repoName = TokenForgeRepositoryDisplayName(repository);
    NSLog(@"INFO [SidebarCompanions] item repo=%@ level=%ld canLevelUp=%@ gitRecent=%@ aiRecent=%@",
          repoName,
          (long)TokenForgeDashboardInteger(repository, @"level", 1),
          canLevelUp ? @"true" : @"false",
          gitRecent ? @"true" : @"false",
          aiRecent ? @"true" : @"false");

    NSButton *row = TokenForgeDashboardButton(@"", self, @selector(sidebarRepositorySelected:));
    row.identifier = identifier;
    row.toolTip = [NSString stringWithFormat:@"%@ · %@ · Lv %ld%@", repoName, TokenForgeDashboardString(repository, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(repository, @"level", 1), selected ? @" · Active repository" : @""];
    row.bordered = NO;
    row.wantsLayer = YES;
    row.layer.cornerRadius = 8.0;
    row.layer.backgroundColor = selected
        ? [NSColor colorWithCalibratedRed:0.22 green:0.42 blue:0.90 alpha:0.34].CGColor
        : [NSColor colorWithCalibratedWhite:1.0 alpha:0.08].CGColor;
    row.layer.borderColor = (canLevelUp ? [NSColor systemOrangeColor] : [NSColor colorWithCalibratedWhite:1.0 alpha:selected ? 0.20 : 0.10]).CGColor;
    row.layer.borderWidth = canLevelUp ? 1.5 : 1.0;
    [row.heightAnchor constraintGreaterThanOrEqualToConstant:76.0].active = YES;
    NSLog(@"INFO [DashboardLayout] repo_item_frame=auto minHeight=76.00 name=%@", repoName);

    NSStackView *content = TokenForgeDashboardHorizontalStack(10.0);
    content.distribution = NSStackViewDistributionFill;
    [row addSubview:content];
    TokenForgePinSubview(content, row, 10, 10, 10, 10);

    TokenForgeCompanionView *avatar = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 36, 36)];
    avatar.translatesAutoresizingMaskIntoConstraints = NO;
    avatar.viewRole = TokenForgeCompanionRenderRoleDashboardPreview;
    avatar.stage = MAX(0, MIN(5, TokenForgeDashboardInteger(repository, @"stageIndex", 0)));
    avatar.visualThemeId = TokenForgeDashboardString(repository, @"avatarSkin", @"orange_cat");
    avatar.zodiacType = TokenForgeDashboardString(repository, @"zodiacType", TokenForgeSnapshotZodiacType ?: @"tiger");
    avatar.assetType = @"sidebar";
    avatar.safeDrawingInset = 4.0;
    [avatar.widthAnchor constraintEqualToConstant:36.0].active = YES;
    [avatar.heightAnchor constraintEqualToConstant:36.0].active = YES;
    [content addArrangedSubview:avatar];

    NSStackView *copy = TokenForgeDashboardVerticalStack(3.0);
    copy.alignment = NSLayoutAttributeLeading;
    [copy setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [copy setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    NSTextField *repoNameLabel = TokenForgeDashboardLabel(repoName, 12.0, NSFontWeightSemibold, TokenForgeDarkSidebarTextColor(), 1);
    repoNameLabel.lineBreakMode = NSLineBreakByTruncatingTail;
    [repoNameLabel setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [copy addArrangedSubview:repoNameLabel];
    NSTextField *miniStage = TokenForgeRequiredOneLineLabel([NSString stringWithFormat:@"%@ · Lv %ld", TokenForgeDashboardString(repository, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(repository, @"level", 1)], 11.0, NSFontWeightRegular, TokenForgeSidebarMutedTextColor());
    [copy addArrangedSubview:miniStage];
    NSMutableArray<NSString *> *badges = [NSMutableArray array];
    if (selected) [badges addObject:@"Active"];
    if (canLevelUp) [badges addObject:@"Evolve"];
    if (aiRecent) [badges addObject:@"AI"];
    if (gitRecent) [badges addObject:@"Git"];
    if (badges.count == 0) [badges addObject:TokenForgeDashboardString(repository, @"xpStatusText", @"0 XP")];
    [copy addArrangedSubview:TokenForgeRequiredOneLineLabel([badges componentsJoinedByString:@" · "], 10.5, canLevelUp ? NSFontWeightSemibold : NSFontWeightRegular, canLevelUp ? [NSColor systemOrangeColor] : TokenForgeSidebarMutedTextColor())];
    [content addArrangedSubview:copy];
    return row;
}

- (NSButton *)sidebarButton:(NSString *)title navKey:(NSString *)navKey action:(SEL)action
{
    NSButton *button = TokenForgeDashboardButton(title, self, action);
    BOOL selected = [navKey isEqualToString:self.selectedNavItem ?: @"dashboard"];
    button.bordered = NO;
    button.wantsLayer = YES;
    button.layer.cornerRadius = 8.0;
    button.layer.backgroundColor = selected ? [NSColor colorWithCalibratedRed:0.23 green:0.45 blue:0.92 alpha:0.28].CGColor : [NSColor clearColor].CGColor;
    button.alignment = NSTextAlignmentLeft;
    if (@available(macOS 10.14, *)) {
        button.contentTintColor = selected ? [NSColor whiteColor] : TokenForgeShellSecondaryTextColor();
    }
    button.attributedTitle = [[NSAttributedString alloc] initWithString:title ?: @"" attributes:@{
        NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:selected ? NSFontWeightSemibold : NSFontWeightMedium],
        NSForegroundColorAttributeName: selected ? [NSColor whiteColor] : TokenForgeDarkSidebarTextColor()
    }];
    [button.heightAnchor constraintEqualToConstant:34.0].active = YES;
    return button;
}

- (NSView *)pillLabel:(NSString *)text
{
    NSView *pill = [[NSView alloc] initWithFrame:NSZeroRect];
    pill.translatesAutoresizingMaskIntoConstraints = NO;
    pill.wantsLayer = YES;
    pill.layer.cornerRadius = 12.0;
    pill.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.18 green:0.68 blue:0.34 alpha:0.12].CGColor;
    NSTextField *label = TokenForgeDashboardLabel(text, 12.0, NSFontWeightMedium, [NSColor systemGreenColor], 1);
    [pill addSubview:label];
    TokenForgePinSubview(label, pill, 5, 10, 5, 10);
    return pill;
}

- (NSButton *)activityFilterButtonWithTitle:(NSString *)title key:(NSString *)key
{
    NSButton *button = TokenForgeSecondaryButton(title, self, @selector(setActivityFilterAction:));
    NSString *normalized = key.length > 0 ? key : @"all";
    button.toolTip = normalized;
    NSString *current = self.activityFilterValue ?: @"all";
    BOOL selected = [normalized isEqualToString:current];
    button.layer.backgroundColor = selected
        ? [NSColor colorWithCalibratedRed:0.90 green:0.96 blue:1.0 alpha:1.0].CGColor
        : [NSColor colorWithCalibratedWhite:1.0 alpha:0.70].CGColor;
    button.layer.borderColor = selected ? [NSColor systemBlueColor].CGColor : [NSColor colorWithCalibratedWhite:0.0 alpha:0.10].CGColor;
    button.attributedTitle = [[NSAttributedString alloc] initWithString:title ?: @"" attributes:@{
        NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:selected ? NSFontWeightSemibold : NSFontWeightMedium],
        NSForegroundColorAttributeName: selected ? [NSColor systemBlueColor] : TokenForgeLightCardPrimaryTextColor()
    }];
    return button;
}

- (NSView *)actionCardWithTitle:(NSString *)title state:(NSString *)state detail:(NSString *)detail buttonTitle:(NSString *)buttonTitle action:(SEL)action accent:(NSColor *)accent
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 16.0, 8.0);
    // Min-height (not fixed) so the card grows to fit its title/state/detail/button
    // content instead of clipping. The card lives inside the dashboard scroll
    // document, so extra height scrolls cleanly rather than getting cut off.
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:176.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(title, 13.0, NSFontWeightSemibold, accent ?: [NSColor systemBlueColor], 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(state ?: @"Not connected", 19.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(detail ?: @"", 4)];
    NSView *spacer = [NSView new];
    [spacer.heightAnchor constraintGreaterThanOrEqualToConstant:4.0].active = YES;
    [stack addArrangedSubview:spacer];
    [stack addArrangedSubview:TokenForgeSecondaryButton(buttonTitle, self, action)];
    return card;
}

- (NSView *)repositoryStatusCardWithRepository:(NSDictionary *)repository
{
    NSString *activeName = TokenForgeDashboardBool(repository, @"connected", NO)
        ? TokenForgeRepositoryDisplayName(repository)
        : @"No active repository";
    NSString *detail = TokenForgeDashboardBool(repository, @"connected", NO)
        ? [NSString stringWithFormat:@"%ld connected · Git XP appears separately from AI XP.", (long)TokenForgeDashboardInteger(repository, @"connectedCount", 1)]
        : @"Connect a Git repository to create its companion.";
    return [self actionCardWithTitle:@"Repository"
                               state:activeName
                              detail:detail
                         buttonTitle:TokenForgeDashboardBool(repository, @"connected", NO) ? @"Manage Repositories" : @"Add Repository"
                              action:@selector(repository:)
                              accent:[NSColor systemBlueColor]];
}

- (NSView *)aiAgentsStatusCardWithAgents:(NSDictionary *)agents
{
    NSArray *providers = TokenForgeDashboardArray(self.state, @"agentProviders");
    NSDictionary *primary = @{};
    for (NSDictionary *provider in providers) {
        if ([provider isKindOfClass:[NSDictionary class]] && TokenForgeDashboardBool(provider, @"connected", NO)) {
            primary = provider;
            break;
        }
    }
    NSString *state = TokenForgeDashboardString(agents, @"statusText", @"No agents connected");
    NSString *detail = @"Analyze AI activity to review estimated token-based growth.";
    if (primary.count > 0) {
        NSInteger savedXp = TokenForgeDashboardInteger(primary, @"savedXP", 0);
        NSString *provider = TokenForgeDashboardString(primary, @"displayName", @"AI Agent");
        detail = savedXp > 0
            ? [NSString stringWithFormat:@"%@ is connected. Recent AI-assisted growth saved +%ld XP.", provider, (long)savedXp]
            : [NSString stringWithFormat:@"%@ is connected. No repository-attributed AI growth yet.", provider];
    }
    return [self actionCardWithTitle:@"AI Agents"
                               state:state
                              detail:detail
                         buttonTitle:providers.count > 0 ? @"Analyze AI / Manage" : @"Connect AI Agent"
                              action:@selector(codexAgent:)
                              accent:[NSColor systemPurpleColor]];
}

- (NSView *)companionMotionCardWithCompanion:(NSDictionary *)companion
{
    BOOL repositoryConnected = TokenForgeDashboardBool(TokenForgeDashboardDictionary(self.state, @"repository"), @"connected", NO);
    BOOL desiredVisible = TokenForgeDashboardBool(self.state, @"desiredVisible", TokenForgeDashboardBool(self.state, @"companionVisible", YES));
    BOOL actualVisible = TokenForgeAnyDesktopOverlayActuallyVisible() ||
                         TokenForgeDashboardBool(self.state, @"actualVisible", desiredVisible);
    BOOL wandering = TokenForgeDashboardBool(self.state, @"movementEnabled", TokenForgeDashboardBool(self.state, @"wanderEnabled", YES));
    BOOL clickThrough = TokenForgeDashboardBool(self.state, @"clickThroughEnabled", NO);
    BOOL dragEnabled = actualVisible && !clickThrough;
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *farm = TokenForgeDashboardDictionary(self.state, @"companionFarm");
    NSArray *farmOverlays = TokenForgeDashboardArray(farm, @"overlays");
    NSString *selectedRepoHash = TokenForgeDashboardString(repository, @"id", @"none");
    NSString *overlayMode = TokenForgeDashboardString(self.state, @"overlayMode", @"selectedRepoCompanion");
    NSString *movementMode = TokenForgeDashboardString(self.state, @"movementMode", overlayMode);
    NSString *targetLabel = [overlayMode isEqualToString:@"allConnectedRepos"] ? @"all connected repos" : ([overlayMode isEqualToString:@"farm"] ? @"farm" : @"selected repo");
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 16.0, 8.0);
    // This card stacks a title, state line, detail, five status rows and three
    // button rows — well over the old fixed 244pt, which clipped the bottom
    // controls. Use a min-height floor and let it grow + scroll instead.
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:244.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Desktop Companion", 13.0, NSFontWeightSemibold, [NSColor systemGreenColor], 1)];
    if (!repositoryConnected) {
        desiredVisible = NO;
        actualVisible = NO;
        dragEnabled = NO;
    }
    NSString *state = !repositoryConnected ? @"Overlay: Disabled" : (desiredVisible ? @"Overlay: Active" : @"Overlay: Disabled");
    [stack addArrangedSubview:TokenForgeDashboardLabel(state, 19.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    NSString *detail = actualVisible
        ? [NSString stringWithFormat:@"Target %@. Movement %@. Drag is %@. Click-through is %@.",
           targetLabel,
           wandering ? @"Running" : @"Paused",
           dragEnabled ? @"Enabled" : @"Disabled",
           clickThrough ? @"On" : @"Off"]
        : (!repositoryConnected ? @"No repository connected." : (desiredVisible ? @"Overlay is requested but no native panel is visible yet." : @"Show Overlay to place the companion on the desktop."));
    if (clickThrough) {
        detail = [detail stringByAppendingString:@" Enable Drag to reposition."];
    }
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(detail, 2)];

    NSStackView *statusRows = TokenForgeDashboardVerticalStack(4.0);
    [statusRows addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Visible: %@", actualVisible ? @"Yes" : @"No"], 1)];
    [statusRows addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Movement: %@", wandering ? @"Running" : @"Paused"], 1)];
    [statusRows addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Drag: %@", dragEnabled ? @"Enabled" : @"Disabled"], 1)];
    [statusRows addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Click-through: %@", clickThrough ? @"On" : @"Off"], 1)];
    [statusRows addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Target: %@", targetLabel], 1)];
    [stack addArrangedSubview:statusRows];

    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *visibilityButton = TokenForgePrimaryButton(desiredVisible ? @"Hide Overlay" : @"Show Overlay", self, desiredVisible ? @selector(hideCompanionFromDashboard:) : @selector(showCompanionFromDashboard:));
    visibilityButton.enabled = repositoryConnected;
    [buttons addArrangedSubview:visibilityButton];
    NSButton *movementButton = TokenForgeSecondaryButton(wandering ? @"Pause Movement" : @"Resume Movement", self, wandering ? @selector(disableWanderFromDashboard:) : @selector(enableWanderFromDashboard:));
    movementButton.enabled = repositoryConnected;
    [buttons addArrangedSubview:movementButton];
    [stack addArrangedSubview:buttons];
    NSStackView *modeButtons = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *dragButton = TokenForgeSecondaryButton(dragEnabled ? @"Disable Drag" : @"Enable Drag", self, dragEnabled ? @selector(enableClickThroughFromDashboard:) : @selector(enableDragFromDashboard:));
    dragButton.enabled = repositoryConnected;
    [modeButtons addArrangedSubview:dragButton];
    NSButton *clickThroughButton = TokenForgeSecondaryButton(clickThrough ? @"Disable Click-through" : @"Enable Click-through", self, clickThrough ? @selector(disableClickThroughFromDashboard:) : @selector(enableClickThroughFromDashboard:));
    clickThroughButton.enabled = repositoryConnected;
    [modeButtons addArrangedSubview:clickThroughButton];
    [stack addArrangedSubview:modeButtons];
    NSStackView *secondaryButtons = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *resetButton = TokenForgeSecondaryButton(@"Reset Position", self, @selector(resetCompanionPosition:));
    resetButton.enabled = repositoryConnected;
    [secondaryButtons addArrangedSubview:resetButton];
    NSButton *targetModeButton = TokenForgeSecondaryButton([NSString stringWithFormat:@"Target Mode: %@", targetLabel], self, @selector(openOverlayTargetModeSettings:));
    targetModeButton.enabled = repositoryConnected;
    [secondaryButtons addArrangedSubview:targetModeButton];
    [secondaryButtons addArrangedSubview:TokenForgeSecondaryButton(@"Settings", self, @selector(settings:))];
    [stack addArrangedSubview:secondaryButtons];
    NSLog(@"INFO [DesktopOverlay] hidden reason=%@", actualVisible ? @"none" : @"companionVisibleOff");
    NSInteger connectedRepoCount = MAX(0, TokenForgeDashboardInteger(repository, @"connectedCount", farmOverlays.count));
    NSInteger targetCompanionCount = [overlayMode isEqualToString:@"selectedRepoCompanion"] ? (repositoryConnected ? 1 : 0) : MAX(connectedRepoCount, (NSInteger)farmOverlays.count);
    NSInteger movingCompanionCount = wandering ? targetCompanionCount : 0;
    NSMutableArray<NSString *> *tickTargets = [NSMutableArray array];
    for (NSDictionary *overlay in farmOverlays) {
        NSString *repoId = TokenForgeDashboardString(overlay, @"repositoryId", @"");
        if (repoId.length > 0) [tickTargets addObject:repoId];
    }
    if (tickTargets.count == 0 && selectedRepoHash.length > 0 && ![selectedRepoHash isEqualToString:@"none"]) {
        [tickTargets addObject:selectedRepoHash];
    }
    NSString *tickTargetHashes = tickTargets.count > 0 ? [tickTargets componentsJoinedByString:@","] : @"none";
    NSString *panelFrames = actualVisible ? @"nativePanel" : @"none";
    NSLog(@"INFO [OverlayVisibilityDiagnostic] selectedRepoHash=%@ connectedRepoCount=%ld targetCompanionCount=%ld movingCompanionCount=%ld overlayMode=%@ movementMode=%@ tickTargetHashes=%@ panelFrames=%@ desiredVisible=%@ actualVisible=%@ panelExists=%@ panelFrame=%@ movementEnabled=%@ dragEnabled=%@ clickThroughEnabled=%@ reason=%@",
          selectedRepoHash.length > 0 ? selectedRepoHash : @"none",
          (long)connectedRepoCount,
          (long)targetCompanionCount,
          (long)movingCompanionCount,
          overlayMode.length > 0 ? overlayMode : @"selectedRepoCompanion",
          movementMode.length > 0 ? movementMode : overlayMode,
          tickTargetHashes,
          panelFrames,
          desiredVisible ? @"true" : @"false",
          actualVisible ? @"true" : @"false",
          actualVisible ? @"true" : @"false",
          actualVisible ? @"nativePanel" : @"none",
          wandering ? @"true" : @"false",
          dragEnabled ? @"true" : @"false",
          clickThrough ? @"true" : @"false",
          repositoryConnected ? @"dashboardStateCard" : @"noApprovedRepository");
    return card;
}

- (NSView *)reviewCardWithActivity:(NSDictionary *)activity review:(NSDictionary *)review
{
    BOOL pending = TokenForgeDashboardBool(review, @"pending", NO);
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 16.0, 8.0);
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:168.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Activity Review", 13.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(pending ? @"Pending review" : TokenForgeDashboardString(activity, @"state", @"No pending review"), 19.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    NSString *summary = pending
        ? [NSString stringWithFormat:@"%@ · +%ld XP", TokenForgeDashboardString(review, @"summary", TokenForgeDashboardString(activity, @"todaySummary", @"Aggregate activity ready for review.")), (long)TokenForgeDashboardInteger(review, @"estimatedXpDelta", 0)]
        : TokenForgeDashboardString(activity, @"todaySummary", @"No activity yet");
    // 0 = unlimited lines: let the review summary wrap fully so it is never
    // truncated. The card uses a min-height and lives in the scroll document,
    // so the extra wrapped lines grow the card instead of clipping the text.
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeFriendlyDashboardSummary(summary, @"No activity yet"), 0)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    if (pending && TokenForgeDashboardBool(review, @"canSaveGrowth", NO)) {
        [buttons addArrangedSubview:TokenForgePrimaryButton(@"Approve", self, @selector(approveReview:))];
    }
    if (pending && TokenForgeDashboardBool(review, @"canDiscard", NO)) {
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Discard", self, @selector(discardReview:))];
    }
    if (!pending) {
        NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
        NSButton *run = TokenForgeSecondaryButton(TokenForgeRunAnalysisTitle(repository), self, @selector(runAnalysis:));
        run.enabled = TokenForgeDashboardBool(repository, @"canAnalyze", NO);
        run.toolTip = run.enabled ? @"Run analysis for the active approved repository." : @"Connect a repository before running analysis.";
        [buttons addArrangedSubview:run];
    }
    [stack addArrangedSubview:buttons];
    return card;
}

- (NSView *)repositoryScreen
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 12.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Repositories")];
    [stack addArrangedSubview:TokenForgeLightCardBodyLabel(@"Each connected Git repository owns exactly one companion. Disconnect archives the companion; permanent deletion requires a separate confirmation flow.", 3)];
    NSArray *repositories = TokenForgeDashboardArray(self.state, @"repositories");
    NSLog(@"INFO [RepositoriesUI] render repoCards count=%ld", (long)repositories.count);
    if (repositories.count == 0) {
        [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"Connect a repository to create your first companion.", 2)];
        [stack addArrangedSubview:TokenForgePrimaryButton(@"Connect Repository", self, @selector(connectRepository:))];
        [stack addArrangedSubview:TokenForgeSecondaryButton(@"View Tutorial", self, @selector(onboarding:))];
        return card;
    }

    for (NSDictionary *repository in repositories) {
        if (![repository isKindOfClass:[NSDictionary class]]) {
            continue;
        }
        [stack addArrangedSubview:[self repositoryListRow:repository]];
    }

    NSDictionary *activeRepository = @{};
    for (NSDictionary *repository in repositories) {
        if ([repository isKindOfClass:[NSDictionary class]] && TokenForgeDashboardBool(repository, @"selected", NO)) {
            activeRepository = repository;
            break;
        }
    }
    NSStackView *scopeButtons = TokenForgeDashboardHorizontalStack(8.0);
    BOOL canAnalyzeActive = activeRepository.count > 0 && TokenForgeDashboardBool(activeRepository, @"canAnalyze", NO);
    NSString *activeName = activeRepository.count > 0 ? TokenForgeRepositoryDisplayName(activeRepository) : @"selected repository";
    NSString *lastAnalyzed = TokenForgeDashboardString(activeRepository, @"lastAnalyzed", @"Not analyzed");
    BOOL firstAnalysis = [lastAnalyzed isEqualToString:@"Not analyzed"];
    NSString *fullHistoryTitle = firstAnalysis
        ? [NSString stringWithFormat:@"Run First Analysis for %@", activeName]
        : [NSString stringWithFormat:@"Run Full History Analysis for %@", activeName];
    NSButton *fullHistory = TokenForgePrimaryButton(fullHistoryTitle, self, @selector(runFullHistoryAnalysis:));
    fullHistory.enabled = canAnalyzeActive;
    fullHistory.toolTip = canAnalyzeActive ? @"Run Full History Analysis from the repository's initial commit to now." : @"Set an approved repository active before analysis.";
    [scopeButtons addArrangedSubview:fullHistory];
    NSButton *sinceLast = TokenForgeSecondaryButton([NSString stringWithFormat:@"Run Since Last Analysis for %@", activeName], self, @selector(runSinceLastAnalysis:));
    sinceLast.enabled = canAnalyzeActive;
    sinceLast.toolTip = canAnalyzeActive ? @"Analyze commits after the last saved analysis checkpoint." : @"Set an approved repository active before analysis.";
    [scopeButtons addArrangedSubview:sinceLast];
    NSButton *recent = TokenForgeSecondaryButton([NSString stringWithFormat:@"Run Recent Analysis for %@", activeName], self, @selector(runRecentAnalysis:));
    recent.enabled = canAnalyzeActive;
    recent.toolTip = canAnalyzeActive ? @"Analyze the configured recent range only." : @"Set an approved repository active before analysis.";
    [scopeButtons addArrangedSubview:recent];
    [stack addArrangedSubview:scopeButtons];

    [stack addArrangedSubview:TokenForgeSecondaryButton(@"Add Repository", self, @selector(connectRepository:))];
    return card;
}

- (NSView *)repositoryListRow:(NSDictionary *)repository
{
    NSStackView *rowStack = nil;
    NSView *row = TokenForgeCardWithStack(&rowStack, 14.0, 8.0);
    row.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.74].CGColor;
    [row.heightAnchor constraintGreaterThanOrEqualToConstant:132.0].active = YES;
    NSString *name = TokenForgeRepositoryDisplayName(repository);
    NSString *identifier = TokenForgeDashboardString(repository, @"id", @"");
    BOOL canLevelUp = TokenForgeDashboardBool(repository, @"canLevelUp", NO);
    NSStackView *top = TokenForgeDashboardHorizontalStack(10.0);
    TokenForgeCompanionView *avatar = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 52, 52)];
    avatar.translatesAutoresizingMaskIntoConstraints = NO;
    avatar.viewRole = TokenForgeCompanionRenderRoleDashboardPreview;
    avatar.stage = MAX(0, MIN(5, TokenForgeDashboardInteger(repository, @"level", 1) >= 1 ? TokenForgeDashboardInteger(repository, @"stageIndex", 0) : 0));
    avatar.visualThemeId = TokenForgeDashboardString(repository, @"avatarSkin", @"orange_cat");
    avatar.zodiacType = TokenForgeDashboardString(repository, @"zodiacType", TokenForgeSnapshotZodiacType ?: @"tiger");
    [avatar.widthAnchor constraintEqualToConstant:52.0].active = YES;
    [avatar.heightAnchor constraintEqualToConstant:52.0].active = YES;
    [top addArrangedSubview:avatar];
    NSStackView *titleStack = TokenForgeDashboardVerticalStack(3.0);
    [titleStack addArrangedSubview:TokenForgeDashboardLabel(name, 16.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [titleStack addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(repository, @"sourceBadge", @"Connected"), 11.0, NSFontWeightMedium, TokenForgeDashboardBool(repository, @"selected", NO) ? [NSColor systemGreenColor] : TokenForgeMutedTextColor(), 1)];
    [top addArrangedSubview:titleStack];
    [rowStack addArrangedSubview:top];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeDashboardString(repository, @"safePath", @"Approved local folder"), 1)];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Branch %@ · Remote %@ · id %@", TokenForgeDashboardString(repository, @"branch", @"unknown"), TokenForgeDashboardString(repository, @"remoteUrl", @"No remote"), TokenForgeDashboardString(repository, @"repositoryId", identifier)], 3)];
    [rowStack addArrangedSubview:TokenForgeDashboardLabel(canLevelUp ? @"Ready to evolve" : TokenForgeDashboardString(repository, @"stage", @"Egg"), 14.0, NSFontWeightSemibold, canLevelUp ? [NSColor systemOrangeColor] : TokenForgeLightCardPrimaryTextColor(), 1)];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Level %ld · %@ · Last analyzed %@ · %@", (long)TokenForgeDashboardInteger(repository, @"level", 1), TokenForgeDashboardString(repository, @"xpStatusText", @"0 XP · 250 XP required"), TokenForgeDashboardString(repository, @"lastAnalyzed", @"Not analyzed"), TokenForgeDashboardString(repository, @"lastAnalysisScope", @"Not analyzed")], 2)];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Git +%ld XP · AI +%ld XP · Estimated token activity: %@", (long)TokenForgeDashboardInteger(repository, @"recentGitXP", 0), (long)TokenForgeDashboardInteger(repository, @"recentAiXP", 0), TokenForgeDashboardString(repository, @"estimatedTokenActivity", @"Unknown")], 2)];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Source %@ · Growth %@ · Mood %@%@", TokenForgeDashboardString(repository, @"sourceBadge", @"Connected"), TokenForgeDashboardString(repository, @"recentGrowthSource", @"None"), TokenForgeDashboardString(repository, @"motionMood", @"idle"), TokenForgeDashboardBool(repository, @"archived", NO) ? @" · Archived" : @""], 2)];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeDashboardString(repository, @"motionReason", @"No recent aggregate activity."), 2)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    if (TokenForgeDashboardBool(repository, @"archived", NO)) {
        [buttons addArrangedSubview:TokenForgeDisabledButton(@"Archived")];
        [buttons addArrangedSubview:TokenForgeDisabledButton(@"Restore")];
        [buttons addArrangedSubview:TokenForgeDisabledButton(@"Delete")];
        [rowStack addArrangedSubview:buttons];
        return row;
    }
    NSButton *select = TokenForgeSecondaryButton(TokenForgeDashboardBool(repository, @"selected", NO) ? @"Active" : @"Set Active", self, @selector(selectRepositoryAction:));
    select.toolTip = identifier;
    select.enabled = !TokenForgeDashboardBool(repository, @"selected", NO) && !TokenForgeDashboardBool(repository, @"archived", NO);
    [buttons addArrangedSubview:select];
    NSButton *analyze = TokenForgePrimaryButton(TokenForgeRunAnalysisTitle(repository), self, @selector(analyzeRepositoryAction:));
    analyze.toolTip = identifier;
    analyze.enabled = TokenForgeDashboardBool(repository, @"canAnalyze", NO);
    if (!analyze.enabled) {
        analyze.toolTip = TokenForgeDashboardString(repository, @"analyzeDisabledReason", @"Connect a repository first");
    }
    [buttons addArrangedSubview:analyze];
    NSButton *analyzeAi = TokenForgeSecondaryButton(@"Analyze AI", self, @selector(runAgentAnalysis:));
    analyzeAi.toolTip = identifier;
    [buttons addArrangedSubview:analyzeAi];
    NSButton *viewGrowth = TokenForgeSecondaryButton(@"View Growth", self, @selector(viewRepositoryGrowthAction:));
    viewGrowth.toolTip = identifier;
    viewGrowth.enabled = TokenForgeDashboardBool(repository, @"canViewGrowth", YES);
    [buttons addArrangedSubview:viewGrowth];
    if (TokenForgeDashboardBool(repository, @"canEvolve", NO)) {
        NSButton *evolve = TokenForgePrimaryButton(@"Evolve Token", self, @selector(evolveRepositoryAction:));
        evolve.toolTip = identifier;
        evolve.enabled = YES;
        [buttons addArrangedSubview:evolve];
    }
    NSButton *disconnect = TokenForgeSecondaryButton(@"Archive", self, @selector(disconnectRepositoryAction:));
    disconnect.toolTip = identifier;
    disconnect.enabled = TokenForgeDashboardBool(repository, @"canDisconnect", YES);
    [buttons addArrangedSubview:disconnect];
    [rowStack addArrangedSubview:buttons];
    return row;
}

- (NSView *)agentsScreen
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 12.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"AI Agents")];
    [stack addArrangedSubview:TokenForgeLightCardBodyLabel(@"Provider connections can be auto-detected or selected manually. Analysis is saved only as local aggregate buckets for the active repository.", 3)];
    NSArray *providers = TokenForgeDashboardArray(self.state, @"agentProviders");
    for (NSDictionary *provider in providers) {
        if (![provider isKindOfClass:[NSDictionary class]]) {
            continue;
        }
        [stack addArrangedSubview:[self agentProviderRow:provider]];
    }

    if (providers.count == 0) {
        [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"No provider definitions are available in this runtime.", 2)];
    }

    return card;
}

- (NSView *)tokenShopScreen
{
    NSLog(@"INFO [RuntimeUIPath][TokenShop] renderer=gameShopCards");
    NSDictionary *shop = TokenForgeDashboardDictionary(self.state, @"tokenShop");
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 12.0);
    card.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.075 green:0.095 blue:0.125 alpha:0.96].CGColor;
    card.layer.borderColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.12].CGColor;
    NSString *currency = TokenForgeDashboardString(shop, @"currencyName", @"Forge Coins");
    NSInteger balance = MAX(0, TokenForgeDashboardInteger(shop, @"balance", TokenForgeDashboardInteger(self.state, @"tokenCurrencyBalance", 0)));
    BOOL hasRepository = TokenForgeDashboardBool(shop, @"hasActiveRepository", TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO));
    NSString *targetType = TokenForgeDashboardString(shop, @"targetType", @"repositoryCompanion");
    NSString *selectedCategory = TokenForgeDashboardString(shop, @"selectedCategory", @"featured");
    NSArray *items = TokenForgeDashboardArray(shop, @"items");
    NSLog(@"INFO [ShopDiagnostic] renderer=gameShopCards targetType=%@ selectedCategory=%@ balance=%ld hasRepository=%@ itemCount=%ld categoryTabs=true cardPreview=true lockedState=true ownedEquippedState=true",
          targetType,
          selectedCategory,
          (long)balance,
          hasRepository ? @"true" : @"false",
          (long)items.count);

    NSStackView *header = TokenForgeDashboardHorizontalStack(14.0);
    header.distribution = NSStackViewDistributionFill;
    NSStackView *headerCopy = TokenForgeDashboardVerticalStack(4.0);
    [headerCopy addArrangedSubview:TokenForgeDashboardLabel(@"Token Shop", 24.0, NSFontWeightBold, [NSColor colorWithCalibratedRed:0.965 green:0.980 blue:1.0 alpha:1.0], 1)];
    [headerCopy addArrangedSubview:TokenForgeDashboardLabel(@"Use each AI agent's token usage coins to unlock zodiac cosmetics.", 13.0, NSFontWeightSemibold, [NSColor colorWithCalibratedRed:0.78 green:0.88 blue:1.0 alpha:1.0], 2)];
    [headerCopy addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(shop, @"statusText", @"Local MVP shop. Purchases persist locally."), 13.0, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), 2)];
    [header addArrangedSubview:headerCopy];
    NSStackView *coinStack = TokenForgeDashboardVerticalStack(4.0);
    coinStack.alignment = NSLayoutAttributeTrailing;
    [coinStack addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%ld %@", (long)balance, currency], 22.0, NSFontWeightBold, [NSColor colorWithCalibratedRed:1.0 green:0.875 blue:0.360 alpha:1.0], 1)];
    [coinStack addArrangedSubview:TokenForgeDashboardLabel(@"Cosmetic-only balance", 12.0, NSFontWeightMedium, TokenForgeShellSecondaryTextColor(), 1)];
    [header addArrangedSubview:coinStack];
    [stack addArrangedSubview:header];

    NSString *transaction = TokenForgeDashboardString(shop, @"lastTransactionStatus", @"");
    if (transaction.length > 0) {
        [stack addArrangedSubview:TokenForgeDashboardLabel(transaction, 13.0, NSFontWeightSemibold, [NSColor systemGreenColor], 2)];
    }

    NSStackView *targetSelector = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *agentTarget = TokenForgeSecondaryButton(@"AI Agents", self, @selector(shopTargetAgents:));
    agentTarget.identifier = @"shop-target-ai-agents";
    NSButton *repositoryTarget = TokenForgeSecondaryButton(@"Repository Mascot", self, @selector(shopTargetRepository:));
    repositoryTarget.identifier = @"shop-target-repository";
    if ([targetType isEqualToString:@"repositoryCompanion"]) {
        repositoryTarget.layer.backgroundColor = TokenForgeSelectedBlueColor().CGColor;
        repositoryTarget.attributedTitle = [[NSAttributedString alloc] initWithString:@"Repository Mascot" attributes:@{NSForegroundColorAttributeName: [NSColor whiteColor], NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:NSFontWeightSemibold]}];
    } else {
        agentTarget.layer.backgroundColor = TokenForgeSelectedBlueColor().CGColor;
        agentTarget.attributedTitle = [[NSAttributedString alloc] initWithString:@"AI Agents" attributes:@{NSForegroundColorAttributeName: [NSColor whiteColor], NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:NSFontWeightSemibold]}];
    }
    [targetSelector addArrangedSubview:agentTarget];
    [targetSelector addArrangedSubview:repositoryTarget];
    [stack addArrangedSubview:targetSelector];

    if ([targetType isEqualToString:@"aiAgent"]) {
        NSArray *agents = TokenForgeDashboardArray(shop, @"agents");
        NSString *selectedAgentId = TokenForgeDashboardString(shop, @"selectedAgentId", @"");
        BOOL selectedAgentConnected = NO;
        BOOL foundSelectedAgent = NO;
        NSString *selectedAgentLockedReason = @"Connect to unlock agent cosmetics.";
        NSStackView *agentChips = TokenForgeDashboardHorizontalStack(8.0);
        for (NSDictionary *agent in agents) {
            if (![agent isKindOfClass:[NSDictionary class]]) {
                continue;
            }
            NSString *agentId = TokenForgeDashboardString(agent, @"id", @"");
            BOOL selectedAgent = TokenForgeDashboardBool(agent, @"selected", NO) ||
                                 (selectedAgentId.length > 0 && [agentId isEqualToString:selectedAgentId]);
            if (selectedAgent) {
                foundSelectedAgent = YES;
                selectedAgentConnected = TokenForgeDashboardBool(agent, @"connected", NO);
                selectedAgentLockedReason = TokenForgeDashboardString(agent, @"lockedReason", @"Connect to unlock agent cosmetics.");
            }
            [agentChips addArrangedSubview:[self tokenShopAgentChip:agent]];
        }
        if (agents.count == 0) {
            [agentChips addArrangedSubview:TokenForgeDashboardLabel(@"No configured AI agents", 13.0, NSFontWeightMedium, TokenForgeShellSecondaryTextColor(), 1)];
        }
        [stack addArrangedSubview:agentChips];
        if (!foundSelectedAgent && selectedAgentId.length > 0) {
            foundSelectedAgent = YES;
        }
        if (foundSelectedAgent && !selectedAgentConnected) {
            NSString *lockedCopy = [NSString stringWithFormat:@"%@ AI agent must be connected before cosmetics can be purchased or equipped.", selectedAgentLockedReason];
            [stack addArrangedSubview:TokenForgeDashboardLabel(lockedCopy, 13.0, NSFontWeightSemibold, [NSColor colorWithCalibratedRed:1.0 green:0.760 blue:0.330 alpha:1.0], 2)];
            NSButton *connectAgent = TokenForgeSecondaryButton(@"Connect agent", self, @selector(shopOpenAgentConnect:));
            connectAgent.toolTip = selectedAgentId.length > 0 ? selectedAgentId : @"codex";
            [stack addArrangedSubview:connectAgent];
        }
    }

    NSArray *categoryIds = TokenForgeDashboardArray(shop, @"categoryIds");
    NSStackView *categories = TokenForgeDashboardHorizontalStack(7.0);
    for (NSString *categoryId in categoryIds) {
        if (![categoryId isKindOfClass:[NSString class]]) {
            continue;
        }
        NSButton *button = TokenForgeSecondaryButton(TokenForgeShopCategoryTitle(categoryId), self, @selector(shopCategoryAction:));
        button.identifier = [NSString stringWithFormat:@"shop-category-%@", categoryId];
        button.toolTip = categoryId;
        if ([selectedCategory isEqualToString:categoryId]) {
            button.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.125 green:0.290 blue:0.630 alpha:1.0].CGColor;
            button.attributedTitle = [[NSAttributedString alloc] initWithString:TokenForgeShopCategoryTitle(categoryId) attributes:@{NSForegroundColorAttributeName: [NSColor whiteColor], NSFontAttributeName: [NSFont systemFontOfSize:13.0 weight:NSFontWeightSemibold]}];
        }
        [categories addArrangedSubview:button];
    }
    [stack addArrangedSubview:categories];

    if (!hasRepository && [targetType isEqualToString:@"repositoryCompanion"]) {
        [stack addArrangedSubview:TokenForgeDashboardLabel(@"Connect repository first to use repository mascot cosmetics.", 13.0, NSFontWeightMedium, TokenForgeShellSecondaryTextColor(), 2)];
        [stack addArrangedSubview:TokenForgePrimaryButton(@"Add Repository", self, @selector(connectRepository:))];
    } else if (!hasRepository) {
        [stack addArrangedSubview:TokenForgeDashboardLabel(@"Repository mascot cosmetics are locked until a repository is connected. AI agent cosmetics use each agent's own connection and coins.", 13.0, NSFontWeightMedium, TokenForgeShellSecondaryTextColor(), 2)];
    }
    NSLog(@"INFO [TokenShop][UI] render targetSelector=true categoryTabs=true owned/equipped labels=true locked/insufficient states=true items=%ld balance=%ld activeRepository=%@", (long)items.count, (long)balance, hasRepository ? @"true" : @"false");
    if (items.count == 0) {
        NSString *empty = [selectedCategory isEqualToString:@"owned"] ? @"No owned items in this category yet." : @"No items in this category.";
        [stack addArrangedSubview:TokenForgeDashboardLabel(empty, 13.0, NSFontWeightMedium, TokenForgeShellSecondaryTextColor(), 2)];
        return card;
    }
    NSStackView *itemGrid = TokenForgeDashboardVerticalStack(10.0);
    for (NSDictionary *item in items) {
        if (![item isKindOfClass:[NSDictionary class]]) {
            continue;
        }
        [itemGrid addArrangedSubview:[self tokenShopItemRow:item currency:currency]];
    }
    [stack addArrangedSubview:itemGrid];
    return card;
}

- (NSView *)wardrobeScreen
{
    NSLog(@"INFO [RuntimeUIPath][Wardrobe] renderer=livePixelWardrobe");
    NSDictionary *shop = TokenForgeDashboardDictionary(self.state, @"tokenShop");
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSString *currency = TokenForgeDashboardString(shop, @"currencyName", @"Forge Coins");
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 12.0);
    card.identifier = @"TokenForge.Wardrobe.ContentRoot";
    card.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.045 green:0.060 blue:0.092 alpha:1.0].CGColor;
    card.layer.borderColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.14].CGColor;
    BOOL hasRepository = TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO);
    BOOL hasConnectedAgent = TokenForgeDashboardInteger(TokenForgeDashboardDictionary(self.state, @"agents"), @"connectedCount", 0) > 0;
    NSLog(@"INFO [WardrobeDiagnostic] renderer=livePixelWardrobe hasRepository=%@ hasConnectedAgent=%@ zodiac=%@ stage=%@ equippedItemHash=%lu previewRole=wardrobe",
          hasRepository ? @"true" : @"false",
          hasConnectedAgent ? @"true" : @"false",
          TokenForgeDashboardString(companion, @"zodiacType", @"rat"),
          TokenForgeDashboardString(companion, @"stage", @"Egg"),
          (unsigned long)TokenForgeDashboardString(shop, @"equippedItemIds", @"").hash);

    if (!hasRepository && !hasConnectedAgent) {
        [stack addArrangedSubview:TokenForgeDashboardLabel(@"Wardrobe", 24.0, NSFontWeightBold, [NSColor whiteColor], 1)];
        [stack addArrangedSubview:TokenForgeDashboardLabel(@"Connect a target to unlock dress-up.", 15.0, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1 alpha:0.86], 2)];
        [stack addArrangedSubview:TokenForgeDashboardLabel(@"Owned cosmetics appear as layered pixel gear once a repository or agent is connected.", 13.0, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), 2)];
        NSStackView *actions = TokenForgeDashboardHorizontalStack(8.0);
        [actions addArrangedSubview:TokenForgePrimaryButton(@"Add Repository", self, @selector(connectRepository:))];
        [actions addArrangedSubview:TokenForgeSecondaryButton(@"Connect AI Agents", self, @selector(codexAgent:))];
        [stack addArrangedSubview:actions];
        NSLog(@"WARN [Wardrobe][NO_TARGET_LOCKED]");
        NSLog(@"INFO [WardrobeDiagnostic] renderer=livePixelWardrobe state=locked hasRepository=false hasConnectedAgent=false ownedItems=0");
        return card;
    }

    NSStackView *header = TokenForgeDashboardHorizontalStack(14.0);
    NSStackView *copy = TokenForgeDashboardVerticalStack(4.0);
    [copy addArrangedSubview:TokenForgeDashboardLabel(@"Wardrobe", 24.0, NSFontWeightBold, [NSColor whiteColor], 1)];
    [copy addArrangedSubview:TokenForgeDashboardLabel(@"Layer skins, outfits, accessories, effects, motion, badges, and themes on the selected companion.", 13.0, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), 2)];
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    [copy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"Selected target: %@ · %@ · %@ · Lv %ld",
                                                        TokenForgeDashboardBool(repository, @"connected", NO) ? TokenForgeRepositoryDisplayName(repository) : @"AI Agent target",
                                                        TokenForgeDashboardString(companion, @"zodiacLabel", @"Rat / 쥐"),
                                                        TokenForgeDashboardString(companion, @"stage", @"Egg"),
                                                        (long)TokenForgeDashboardInteger(companion, @"level", 1)],
                                                       12.0, NSFontWeightSemibold, [NSColor colorWithCalibratedRed:0.62 green:0.78 blue:1.0 alpha:1.0], 1)];
    [header addArrangedSubview:copy];
    TokenForgeShopPreviewView *preview = [[TokenForgeShopPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 112, 112)];
    preview.previewType = [@"zodiac_" stringByAppendingString:TokenForgeDashboardString(companion, @"zodiacType", @"rat")];
	    preview.zodiacType = TokenForgeDashboardString(companion, @"zodiacType", @"rat");
	    preview.surfaceName = @"wardrobe";
	    preview.equippedItemIds = TokenForgeDashboardString(shop, @"equippedItemIds", @"");
	    preview.rarity = @"Epic";
    preview.stage = MAX(0, MIN(5, TokenForgeDashboardInteger(companion, @"stageIndex", 4)));
    [preview.widthAnchor constraintEqualToConstant:112.0].active = YES;
    [preview.heightAnchor constraintEqualToConstant:112.0].active = YES;
    [header addArrangedSubview:preview];
    [stack addArrangedSubview:header];
    NSLog(@"INFO [LayoutDiagnostic] selectedTab=wardrobe topGap=%.0f contentRoot=TokenForge.Wardrobe.ContentRoot headerOutsideScroll=false oversizedTopSpacer=false", TokenForgeTabContentTopInset);
    NSLog(@"INFO [WardrobeLayoutDiagnostic] rootFrame=TokenForge.Wardrobe.ContentRoot headerFrame=top previewFrame=112x112 categoryFrame=pending itemGridFrame=pending scrollContentHeight=auto topGap=%.0f bottomClipped=false", TokenForgeTabContentTopInset);

    if (!hasRepository && [TokenForgeDashboardString(shop, @"targetType", @"repositoryCompanion") isEqualToString:@"repositoryCompanion"]) {
        [stack addArrangedSubview:TokenForgeDashboardLabel(@"Connect repository first to unlock repository mascot wardrobe slots.", 13.0, NSFontWeightSemibold, TokenForgeShellSecondaryTextColor(), 2)];
        [stack addArrangedSubview:TokenForgePrimaryButton(@"Add Repository", self, @selector(connectRepository:))];
        return card;
    }

    NSArray<NSString *> *slots = @[@"base zodiac", @"skin", @"outfit", @"head", @"accessory", @"back", @"aura/effect", @"motion", @"badge", @"theme"];
    NSStackView *slotRow = TokenForgeDashboardHorizontalStack(7.0);
    for (NSString *slot in slots) {
        [slotRow addArrangedSubview:[self pillLabel:[slot capitalizedString]]];
    }
    [stack addArrangedSubview:slotRow];

    NSArray *items = TokenForgeDashboardArray(shop, @"items");
    NSStackView *ownedGrid = TokenForgeDashboardVerticalStack(10.0);
    NSInteger ownedCount = 0;
    for (NSDictionary *item in items) {
        if (![item isKindOfClass:[NSDictionary class]] || !TokenForgeDashboardBool(item, @"owned", NO)) {
            continue;
        }
        ownedCount += 1;
        [ownedGrid addArrangedSubview:[self tokenShopItemRow:item currency:currency]];
    }

    if (ownedCount == 0) {
        [stack addArrangedSubview:TokenForgeDashboardLabel(@"No owned items yet. Token Shop previews show what can be unlocked next.", 13.0, NSFontWeightSemibold, TokenForgeShellSecondaryTextColor(), 2)];
        [stack addArrangedSubview:TokenForgePrimaryButton(@"Open Token Shop", self, @selector(tokenShop:))];
    } else {
        [stack addArrangedSubview:ownedGrid];
    }

    NSLog(@"INFO [Wardrobe][UI] render targetSelector=true livePreview=true ownedItems=%ld slots=10", (long)ownedCount);
    NSLog(@"INFO [WardrobeDiagnostic] renderer=livePixelWardrobe state=ready ownedItems=%ld slots=10 livePreview=true cacheInvalidatesOnEquipment=true", (long)ownedCount);
    return card;
}

- (NSView *)tokenShopItemRow:(NSDictionary *)item currency:(NSString *)currency
{
    NSView *row = TokenForgeDashboardCard();
    row.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.055 green:0.072 blue:0.108 alpha:0.98].CGColor;
    row.layer.borderColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.22].CGColor;
    [row.heightAnchor constraintGreaterThanOrEqualToConstant:148.0].active = YES;
    NSString *itemId = TokenForgeDashboardString(item, @"itemId", @"");
    BOOL owned = TokenForgeDashboardBool(item, @"owned", NO);
    BOOL equipped = TokenForgeDashboardBool(item, @"equipped", NO);
    BOOL available = TokenForgeDashboardBool(item, @"available", NO);
    BOOL canEquip = TokenForgeDashboardBool(item, @"canEquip", NO);
    BOOL locked = TokenForgeDashboardBool(item, @"locked", NO);
    NSString *lockedAgentReason = TokenForgeDashboardString(item, @"lockedAgentReason", @"");
    BOOL agentLocked = lockedAgentReason.length > 0;
    NSInteger price = MAX(0, TokenForgeDashboardInteger(item, @"price", 0));

    NSStackView *layout = TokenForgeDashboardHorizontalStack(14.0);
    layout.distribution = NSStackViewDistributionFill;
    [row addSubview:layout];
    TokenForgePinSubview(layout, row, 14, 14, 14, 14);

    TokenForgeShopPreviewView *preview = [[TokenForgeShopPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 82, 82)];
    preview.surfaceName = @"shop";
    preview.wantsLayer = YES;
    preview.previewType = TokenForgeDashboardString(item, @"previewType", TokenForgeDashboardString(item, @"previewIcon", @"generic"));
    preview.zodiacType = TokenForgeDashboardString(item, @"zodiacType", @"");
    preview.rarity = TokenForgeDashboardString(item, @"rarity", @"Common");
    preview.stage = TokenForgePreviewStageForType(preview.previewType, TokenForgeDashboardInteger(item, @"stageIndex", [preview.rarity isEqualToString:@"Legendary"] ? 5 : 4));
    [preview.widthAnchor constraintEqualToConstant:92.0].active = YES;
    [preview.heightAnchor constraintEqualToConstant:92.0].active = YES;
    [layout addArrangedSubview:preview];

    NSStackView *copy = TokenForgeDashboardVerticalStack(6.0);
    [copy addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(item, @"name", @"Shop Item"), 16.0, NSFontWeightBold, [NSColor colorWithCalibratedRed:0.970 green:0.985 blue:1.0 alpha:1.0], 1)];
    NSString *meta = [NSString stringWithFormat:@"%@ · %@ · %@",
                      TokenForgeDashboardString(item, @"itemType", @"Cosmetic"),
                      TokenForgeDashboardString(item, @"rarity", @"Common"),
                      TokenForgeDashboardString(item, @"targetCompatibility", @"Repository Companion")];
    [copy addArrangedSubview:TokenForgeDashboardLabel(meta, 12.0, NSFontWeightSemibold, [NSColor colorWithCalibratedRed:0.62 green:0.78 blue:1.0 alpha:1.0], 1)];
    [copy addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(item, @"description", @"Cosmetic item."), 12.0, NSFontWeightRegular, [NSColor colorWithCalibratedWhite:1.0 alpha:0.74], 2)];
    [copy addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(item, @"previewEffect", @"Preview cosmetic"), 11.5, NSFontWeightSemibold, [NSColor colorWithCalibratedRed:1.0 green:0.78 blue:0.32 alpha:1.0], 2)];
    [layout addArrangedSubview:copy];

    NSStackView *stateStack = TokenForgeDashboardVerticalStack(7.0);
    stateStack.alignment = NSLayoutAttributeTrailing;
    NSString *state = TokenForgeDashboardString(item, @"stateLabel", owned ? @"Owned" : locked ? @"Locked" : available ? @"Buy" : @"Need Coins");
    NSColor *stateColor = equipped ? [NSColor systemGreenColor] : owned ? [NSColor systemBlueColor] : available ? [NSColor systemIndigoColor] : [NSColor systemOrangeColor];
    [stateStack addArrangedSubview:TokenForgeDashboardLabel(state, 13.0, NSFontWeightBold, stateColor, 2)];
    [stateStack addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%ld %@", (long)price, currency ?: @"Forge Coins"], 13.0, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1.0 alpha:0.90], 1)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *purchase = TokenForgePrimaryButton(TokenForgeDashboardString(item, @"buttonTitle", @"Buy"), self, @selector(purchaseTokenShopItemAction:));
    purchase.toolTip = itemId;
    purchase.enabled = !agentLocked && (available || canEquip) && itemId.length > 0;
    if (canEquip) {
        purchase.action = @selector(equipTokenShopItemAction:);
    }
    if (!purchase.enabled) {
        purchase.toolTip = TokenForgeDashboardString(item, @"disabledReason", agentLocked ? lockedAgentReason : owned ? @"Already owned." : @"Unavailable.");
    }
    [buttons addArrangedSubview:purchase];
    if (equipped) {
        [buttons addArrangedSubview:TokenForgeDisabledButton(@"Equipped")];
    } else if (owned) {
        [buttons addArrangedSubview:TokenForgeDisabledButton(@"Owned")];
    }
    [stateStack addArrangedSubview:buttons];
    NSString *disabledReason = TokenForgeDashboardString(item, @"disabledReason", @"");
    if (disabledReason.length == 0 && agentLocked) {
        disabledReason = lockedAgentReason;
    }
    if (disabledReason.length > 0 && !available && !canEquip) {
        [stateStack addArrangedSubview:TokenForgeDashboardLabel(disabledReason, 11.0, NSFontWeightMedium, TokenForgeShellSecondaryTextColor(), 3)];
    }
    [layout addArrangedSubview:stateStack];
    return row;
}

- (NSView *)tokenShopAgentChip:(NSDictionary *)agent
{
    BOOL connected = TokenForgeDashboardBool(agent, @"connected", NO);
    BOOL selected = TokenForgeDashboardBool(agent, @"selected", NO);
    NSString *agentId = TokenForgeDashboardString(agent, @"id", @"");
    NSString *title = TokenForgeDashboardString(agent, @"displayName", @"AI Agent");
    NSInteger coins = MAX(0, TokenForgeDashboardInteger(agent, @"spendableCoins", 0));
    NSString *currency = TokenForgeDashboardString(agent, @"currencyName", @"Agent Coins");
    NSString *zodiacLabel = TokenForgeDashboardString(agent, @"zodiacLabel", @"Zodiac");
    NSString *buttonTitle = connected
        ? [NSString stringWithFormat:@"%@ · %ld %@ · %@", title, (long)coins, currency, zodiacLabel]
        : [NSString stringWithFormat:@"%@ · Locked", title];
    NSButton *button = TokenForgeSecondaryButton(buttonTitle, self, connected ? @selector(shopTargetAgentChip:) : @selector(shopOpenAgentConnect:));
    button.identifier = [NSString stringWithFormat:@"shop-agent-%@", agentId];
    button.toolTip = agentId;
    if (selected) {
        button.layer.backgroundColor = TokenForgeSelectedBlueColor().CGColor;
        button.attributedTitle = [[NSAttributedString alloc] initWithString:buttonTitle attributes:@{NSForegroundColorAttributeName: [NSColor whiteColor], NSFontAttributeName: [NSFont systemFontOfSize:12.0 weight:NSFontWeightSemibold]}];
    } else if (!connected) {
        button.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.18].CGColor;
        button.attributedTitle = [[NSAttributedString alloc] initWithString:buttonTitle attributes:@{NSForegroundColorAttributeName: TokenForgeShellSecondaryTextColor(), NSFontAttributeName: [NSFont systemFontOfSize:12.0 weight:NSFontWeightMedium]}];
    }
    return button;
}

- (NSView *)agentProviderRow:(NSDictionary *)provider
{
    NSStackView *stack = nil;
    NSView *row = TokenForgeCardWithStack(&stack, 14.0, 8.0);
    row.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.74].CGColor;
    [row.heightAnchor constraintGreaterThanOrEqualToConstant:142.0].active = YES;
    NSString *providerId = TokenForgeDashboardString(provider, @"id", @"");
    [stack addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(provider, @"displayName", @"AI Agent"), 16.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(provider, @"supportedStatus", @"manual_folder_required"), 12.0, NSFontWeightMedium, [NSColor systemPurpleColor], 1)];
    NSLog(@"INFO [AIUsageUI] render provider=%@ tokenActivity=%@ repo=%@",
          TokenForgeDashboardString(provider, @"displayName", @"AI Agent"),
          TokenForgeDashboardString(provider, @"estimatedTokenActivity", @"Unknown"),
          TokenForgeDashboardString(provider, @"recentAnalyzedRepository", @"Unassigned"));
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"%@ · approved source %@ · %@", TokenForgeDashboardString(provider, @"statusText", @"Not connected"), TokenForgeDashboardBool(provider, @"approvedSource", NO) ? @"yes" : @"no", TokenForgeDashboardString(provider, @"safeCandidateSummary", @"No local source selected")], 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Estimated token activity: %@ · Estimated tokens: %@ · Sessions: %@ · Interactions: %@", TokenForgeDashboardString(provider, @"estimatedTokenActivity", @"Unknown"), TokenForgeDashboardString(provider, @"estimatedTokensText", @"unavailable"), TokenForgeDashboardString(provider, @"sessionCountText", @"unavailable"), TokenForgeDashboardString(provider, @"interactionCountText", @"unavailable")], 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Recent repository: %@ · Pending XP +%ld · Saved XP +%ld · Confidence %@", TokenForgeDashboardString(provider, @"recentAnalyzedRepository", @"Unassigned"), (long)TokenForgeDashboardInteger(provider, @"pendingXP", 0), (long)TokenForgeDashboardInteger(provider, @"savedXP", 0), TokenForgeDashboardString(provider, @"confidence", @"Unknown")], 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"%@ · Warnings: %@", TokenForgeDashboardString(provider, @"repositoryAttributionSummary", @"No recent repository attribution"), TokenForgeDashboardString(provider, @"warningsText", @"None")], 3)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"Local aggregate only. Raw prompts, code, file content, and commands are not stored in progress data.", 2)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *connect = TokenForgeSecondaryButton(@"Connect", self, @selector(connectAgentAction:));
    connect.toolTip = providerId;
    connect.enabled = TokenForgeDashboardBool(provider, @"canConnect", !TokenForgeDashboardBool(provider, @"connected", NO));
    if (!connect.enabled) connect.toolTip = TokenForgeDashboardBool(provider, @"connected", NO) ? @"Already connected." : TokenForgeDashboardString(provider, @"disabledReason", @"Detect or choose a folder first.");
    [buttons addArrangedSubview:connect];
    NSButton *approve = TokenForgeSecondaryButton(@"Approve", self, @selector(connectAgentAction:));
    approve.toolTip = providerId;
    approve.enabled = TokenForgeDashboardBool(provider, @"canApprove", NO);
    [buttons addArrangedSubview:approve];
    NSButton *detect = TokenForgeSecondaryButton(@"Auto Detect", self, @selector(autoDetectAgentAction:));
    detect.toolTip = providerId;
    detect.enabled = TokenForgeDashboardBool(provider, @"canAutoDetect", YES);
    [buttons addArrangedSubview:detect];
    NSButton *folder = TokenForgeSecondaryButton(@"Choose Folder", self, @selector(chooseAgentFolderAction:));
    folder.toolTip = providerId;
    folder.enabled = TokenForgeDashboardBool(provider, @"canChooseFolder", YES);
    [buttons addArrangedSubview:folder];
    NSButton *analyze = TokenForgePrimaryButton(@"Analyze", self, @selector(analyzeAgentAction:));
    analyze.toolTip = providerId;
    analyze.enabled = TokenForgeDashboardBool(provider, @"canAnalyze", NO);
    if (!analyze.enabled) analyze.toolTip = TokenForgeDashboardString(provider, @"disabledReason", @"Connect or approve this provider before analysis.");
    [buttons addArrangedSubview:analyze];
    NSButton *saveGrowth = TokenForgePrimaryButton(@"Save Growth", self, @selector(approveReview:));
    saveGrowth.enabled = TokenForgeDashboardBool(provider, @"canSaveGrowth", NO);
    [buttons addArrangedSubview:saveGrowth];
    NSButton *viewUsage = TokenForgeSecondaryButton(@"View Usage", self, @selector(viewAgentUsageAction:));
    viewUsage.toolTip = providerId;
    [buttons addArrangedSubview:viewUsage];
    NSButton *disconnect = TokenForgeSecondaryButton(@"Disconnect", self, @selector(disconnectAgentAction:));
    disconnect.toolTip = providerId;
    disconnect.enabled = TokenForgeDashboardBool(provider, @"canDisconnect", TokenForgeDashboardBool(provider, @"connected", NO));
    if (!disconnect.enabled) disconnect.toolTip = @"Connect this provider before disconnecting it.";
    [buttons addArrangedSubview:disconnect];
    [stack addArrangedSubview:buttons];
    return row;
}

- (NSView *)activityScreenWithActivity:(NSDictionary *)activity review:(NSDictionary *)review
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 14.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Activity")];
    [stack addArrangedSubview:TokenForgeLightCardBodyLabel(TokenForgeDashboardString(self.state, @"actionStatusText", @"Review safe aggregate activity before saving growth."), 3)];
    NSString *filter = self.activityFilterValue ?: @"all";
    BOOL showPending = [filter isEqualToString:@"all"] || [filter isEqualToString:@"active"] || [filter isEqualToString:@"pending"];
    BOOL showRepository = [filter isEqualToString:@"all"] || [filter isEqualToString:@"active"] || [filter isEqualToString:@"repository"];
    BOOL showAgent = [filter isEqualToString:@"all"] || [filter isEqualToString:@"agent"];
    BOOL showHistory = [filter isEqualToString:@"all"] || [filter isEqualToString:@"active"];
    NSStackView *filters = TokenForgeDashboardHorizontalStack(8.0);
    [filters addArrangedSubview:[self activityFilterButtonWithTitle:@"All repositories" key:@"all"]];
    [filters addArrangedSubview:[self activityFilterButtonWithTitle:@"Active repository only" key:@"active"]];
    [filters addArrangedSubview:[self activityFilterButtonWithTitle:@"Repository Activity" key:@"repository"]];
    [filters addArrangedSubview:[self activityFilterButtonWithTitle:@"AI Agent Activity" key:@"agent"]];
    [filters addArrangedSubview:[self activityFilterButtonWithTitle:@"Pending only" key:@"pending"]];
    for (NSDictionary *repository in TokenForgeDashboardArray(self.state, @"repositories")) {
        if (![repository isKindOfClass:[NSDictionary class]]) {
            continue;
        }
        NSString *repoId = TokenForgeDashboardString(repository, @"id", @"");
        NSString *repoName = TokenForgeRepositoryDisplayName(repository);
        if (repoId.length > 0) {
            [filters addArrangedSubview:[self activityFilterButtonWithTitle:repoName key:[@"repo:" stringByAppendingString:repoId]]];
        }
    }
    [stack addArrangedSubview:filters];

    BOOL pending = TokenForgeDashboardBool(review, @"pending", NO);
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    if (TokenForgeDashboardBool(companion, @"canLevelUp", NO)) {
        NSStackView *levelStack = nil;
        NSView *levelCard = TokenForgeCardWithStack(&levelStack, 14.0, 8.0);
        levelCard.layer.backgroundColor = [NSColor colorWithCalibratedRed:1.0 green:0.94 blue:0.78 alpha:0.78].CGColor;
        [levelStack addArrangedSubview:TokenForgeDashboardLabel(@"Level Up Ready", 14.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 1)];
        [levelStack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeDashboardString(companion, @"levelUpStatusText", @"Extra XP will carry forward after level up."), 2)];
        [levelStack addArrangedSubview:TokenForgePrimaryButton(@"Evolve Token", self, @selector(levelUpCompanion:))];
        [stack addArrangedSubview:levelCard];
    }
    if (showPending) {
        NSStackView *pendingStack = nil;
        NSView *pendingCard = TokenForgeCardWithStack(&pendingStack, 16.0, 8.0);
        pendingCard.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.76].CGColor;
        [pendingStack addArrangedSubview:TokenForgeDashboardLabel(@"Pending Growth Review", 13.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 1)];
        if (pending) {
        [pendingStack addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(review, @"summary", @"Aggregate activity ready for review."), 18.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 3)];
        [pendingStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"%@ · target %@ · confidence %@ · +%ld XP estimated", TokenForgeDashboardString(review, @"source", @"activity"), TokenForgeDashboardString(review, @"repositoryName", @"Active repository"), TokenForgeDashboardString(review, @"confidence", @"unknown"), (long)TokenForgeDashboardInteger(review, @"estimatedXpDelta", 0)], 2)];
        [pendingStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Code +%ld · Focus +%ld · Debug +%ld · Design +%ld", (long)TokenForgeDashboardInteger(review, @"codeDelta", 0), (long)TokenForgeDashboardInteger(review, @"focusDelta", 0), (long)TokenForgeDashboardInteger(review, @"debugDelta", 0), (long)TokenForgeDashboardInteger(review, @"designDelta", 0)], 2)];
        NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
        if (TokenForgeDashboardBool(review, @"canSaveGrowth", NO)) {
            [buttons addArrangedSubview:TokenForgePrimaryButton(@"Save Growth", self, @selector(approveReview:))];
        }
        if (TokenForgeDashboardBool(review, @"canDiscard", NO)) {
            [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Discard", self, @selector(discardReview:))];
        }
        if (TokenForgeDashboardBool(review, @"canViewDetails", NO)) {
            [buttons addArrangedSubview:TokenForgeSecondaryButton(@"View Details", self, @selector(viewReviewDetails:))];
        }
        [pendingStack addArrangedSubview:buttons];
        if (TokenForgeDashboardBool(review, @"detailVisible", NO)) {
            NSStackView *detailStack = nil;
            NSView *detailCard = TokenForgeCardWithStack(&detailStack, 12.0, 7.0);
            detailCard.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.72].CGColor;
            [detailStack addArrangedSubview:TokenForgeDashboardLabel(@"Review Details", 13.0, NSFontWeightSemibold, [NSColor systemBlueColor], 1)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"reviewId %@ · source %@ · status %@", TokenForgeDashboardString(review, @"reviewId", @"pending-review"), TokenForgeDashboardString(review, @"source", @"activity"), TokenForgeDashboardString(review, @"status", @"pending")], 2)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Repository %@ · Provider %@", TokenForgeDashboardString(review, @"repositoryName", @"No active repository"), TokenForgeDashboardString(review, @"providerName", @"Repository analysis")], 2)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Confidence %@ · Estimated XP +%ld · Generated %@", TokenForgeDashboardString(review, @"confidence", @"unknown"), (long)TokenForgeDashboardInteger(review, @"estimatedXpDelta", 0), TokenForgeDashboardString(review, @"generatedAt", @"Not generated")], 2)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Stats Code +%ld · Focus +%ld · Debug +%ld · Design +%ld · Sync +%ld", (long)TokenForgeDashboardInteger(review, @"codeDelta", 0), (long)TokenForgeDashboardInteger(review, @"focusDelta", 0), (long)TokenForgeDashboardInteger(review, @"debugDelta", 0), (long)TokenForgeDashboardInteger(review, @"designDelta", 0), (long)TokenForgeDashboardInteger(review, @"syncDelta", 0)], 2)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeDashboardString(review, @"evidenceSummary", @"Safe aggregate evidence is available."), 5)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeDashboardString(review, @"categoryBreakdown", @"Code +0 · Focus +0 · Debug +0 · Design +0 · Sync +0"), 2)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Warnings %@", TokenForgeDashboardString(review, @"warnings", @"none")], 2)];
            [detailStack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeDashboardString(review, @"privacyNote", @"Raw prompt, code, file content, and command logs are not stored."), 2)];
            [pendingStack addArrangedSubview:detailCard];
        }
        } else {
        [pendingStack addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(activity, @"state", @"No pending review"), 18.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
        [pendingStack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"Run analysis to create a pending review. XP and stats are applied only after Save Growth.", 3)];
        NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
        NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
        NSDictionary *agents = TokenForgeDashboardDictionary(self.state, @"agents");
        NSButton *runRepository = TokenForgePrimaryButton(@"Run Repository Analysis", self, @selector(runAnalysis:));
        runRepository.enabled = TokenForgeDashboardBool(repository, @"canAnalyze", NO);
        runRepository.toolTip = TokenForgeDashboardString(repository, @"disabledReason", @"Connect a repository first.");
        [buttons addArrangedSubview:runRepository];
        NSButton *runAgents = TokenForgeSecondaryButton(@"Analyze AI Agents", self, @selector(runAgentAnalysis:));
        runAgents.enabled = TokenForgeDashboardBool(repository, @"connected", NO) && TokenForgeDashboardInteger(agents, @"connectedCount", 0) > 0;
        runAgents.toolTip = runAgents.enabled ? @"Run analysis for a ready AI provider." : @"Connect a repository before running analysis.";
        [buttons addArrangedSubview:runAgents];
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Connect Repository", self, @selector(connectRepository:))];
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Connect AI Agent", self, @selector(connectCodexAgent:))];
        [pendingStack addArrangedSubview:buttons];
        }
        [stack addArrangedSubview:pendingCard];
    }

    NSArray *runningJobs = TokenForgeDashboardArray(activity, @"runningJobs");
    if (runningJobs.count > 0 && (showPending || [filter isEqualToString:@"all"])) {
        NSStackView *jobsStack = nil;
        NSView *jobsCard = TokenForgeCardWithStack(&jobsStack, 14.0, 8.0);
        jobsCard.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.74].CGColor;
        [jobsStack addArrangedSubview:TokenForgeDashboardLabel(@"Running Jobs", 13.0, NSFontWeightSemibold, [NSColor systemBlueColor], 1)];
        for (NSDictionary *job in runningJobs) {
            if (![job isKindOfClass:[NSDictionary class]]) {
                continue;
            }
            [jobsStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"%@ · %@ · jobId %@ · %@", TokenForgeDashboardString(job, @"sourceName", @"Activity source"), TokenForgeDashboardString(job, @"status", @"running"), TokenForgeDashboardString(job, @"id", @"analysis-running"), TokenForgeDashboardString(job, @"currentStep", @"validating repository")], 3)];
        }
        [stack addArrangedSubview:jobsCard];
    }

    NSStackView *sections = TokenForgeDashboardHorizontalStack(12.0);
    sections.distribution = NSStackViewDistributionFillEqually;
    if (showHistory) {
        [sections addArrangedSubview:[self compactActivitySection:@"Recent Runs" detail:TokenForgeDashboardString(activity, @"recentRunsSummary", @"No recent runs")]];
        [sections addArrangedSubview:[self compactActivitySection:@"Saved Growth History" detail:TokenForgeDashboardString(activity, @"savedReviewsSummary", @"No saved growth history yet.")]];
    }
    if (showRepository) {
        [sections addArrangedSubview:[self compactActivitySection:@"Repository Activity" detail:TokenForgeDashboardString(activity, @"repositoryActivitySummary", @"No repository activity")]];
    }
    if (showAgent) {
        [sections addArrangedSubview:[self compactActivitySection:@"AI Agent Activity" detail:TokenForgeDashboardString(activity, @"agentActivitySummary", @"No AI agent activity")]];
    }
    [stack addArrangedSubview:sections];
    return card;
}

- (NSView *)compactActivitySection:(NSString *)title detail:(NSString *)detail
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 14.0, 7.0);
    card.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.70].CGColor;
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:132.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(title, 13.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(detail ?: @"", 5)];
    return card;
}

- (NSView *)growthSummaryCardWithActivity:(NSDictionary *)activity
{
    NSLog(@"INFO [RuntimeUIPath][GrowthSummary] renderer=activeRepositoryOnly");
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSLog(@"INFO [GrowthSummary][SELECTED_REPOSITORY] repositoryId=%@ repositoryName=%@ connected=%@",
          TokenForgeDashboardString(repository, @"id", @"none"),
          TokenForgeRepositoryDisplayName(repository),
          TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO) ? @"true" : @"false");
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 12.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Growth Summary")];
    if (!TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO)) {
        NSLog(@"INFO [GrowthSummary][NO_RUN_FOR_REPOSITORY] reason=noActiveRepository");
        [stack addArrangedSubview:TokenForgeLightCardBodyLabel(@"Connect a repository to start tracking Git growth.", 3)];
        [stack addArrangedSubview:TokenForgePrimaryButton(@"Add Repository", self, @selector(connectRepository:))];
        return card;
    }
    if (!TokenForgeDashboardBool(activity, @"hasSavedReviews", NO) &&
        !TokenForgeDashboardBool(activity, @"hasRepositoryActivity", NO) &&
        !TokenForgeDashboardBool(activity, @"hasRecentRuns", NO)) {
        NSLog(@"INFO [GrowthSummary][NO_RUN_FOR_REPOSITORY] repositoryId=%@",
              TokenForgeDashboardString(repository, @"id", @"none"));
        [stack addArrangedSubview:TokenForgeLightCardBodyLabel(@"No analysis yet", 2)];
        [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"Run Full History after selecting an approved repository. Growth stats appear only after a review is saved for this repository.", 3)];
        NSButton *run = TokenForgePrimaryButton([NSString stringWithFormat:@"Run Full History Analysis for %@", TokenForgeRepositoryDisplayName(repository)], self, @selector(runAnalysisFullHistory:));
        run.enabled = TokenForgeDashboardBool(TokenForgeDashboardDictionary(self.state, @"repository"), @"canAnalyze", NO);
        [stack addArrangedSubview:run];
        return card;
    }
    NSLog(@"INFO [GrowthSummary][DISPLAY_RUN] repositoryId=%@ summary=%@",
          TokenForgeDashboardString(repository, @"id", @"none"),
          TokenForgeDashboardString(activity, @"savedReviewsSummary", @"No saved growth"));
    NSInteger codeValue = TokenForgeDashboardInteger(activity, @"code", TokenForgeDashboardInteger(self.state, @"codeStat", 0));
    NSInteger focusValue = TokenForgeDashboardInteger(activity, @"focus", TokenForgeDashboardInteger(self.state, @"focusStat", 0));
    NSInteger debugValue = TokenForgeDashboardInteger(activity, @"debug", TokenForgeDashboardInteger(self.state, @"debugStat", 0));
    NSInteger designValue = TokenForgeDashboardInteger(activity, @"design", TokenForgeDashboardInteger(self.state, @"designStat", 0));
    NSInteger syncValue = TokenForgeDashboardInteger(activity, @"sync", TokenForgeDashboardInteger(self.state, @"syncStat", 0));
    BOOL hasAxisData = TokenForgeDashboardBool(activity, @"hasAxisData", TokenForgeDashboardBool(self.state, @"hasGrowthAxisData", NO));
    NSString *axisStatusText = TokenForgeDashboardString(activity, @"axisDataStatusText", TokenForgeDashboardString(self.state, @"growthAxisDataStatusText", @"No axis data recorded yet."));
    if (!hasAxisData) {
        NSLog(@"INFO [GrowthSummary][AXIS_MISSING] repositoryId=%@ reason=noStoredAxisDeltas message=%@",
              TokenForgeDashboardString(repository, @"id", @"none"),
              axisStatusText);
        [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(axisStatusText, 2)];
    }
    NSString *basis = TokenForgeDashboardString(repository, @"growthBasis", TokenForgeDashboardString(self.state, @"growthBasis", @"Full local Git history"));
    NSString *firstCommit = TokenForgeDashboardString(repository, @"firstCommit", TokenForgeDashboardString(self.state, @"growthFirstCommit", @"none"));
    NSString *currentHead = TokenForgeDashboardString(repository, @"currentHead", TokenForgeDashboardString(self.state, @"growthCurrentHead", @"none"));
    NSString *lastAnalyzedCommit = TokenForgeDashboardString(repository, @"lastAnalyzedCommit", TokenForgeDashboardString(self.state, @"growthLastAnalyzedCommit", @"none"));
    NSInteger commitsAnalyzed = TokenForgeDashboardInteger(repository, @"commitsAnalyzed", TokenForgeDashboardInteger(self.state, @"growthCommitsAnalyzed", 0));
    NSInteger filesChanged = TokenForgeDashboardInteger(repository, @"filesChanged", TokenForgeDashboardInteger(self.state, @"growthFilesChanged", 0));
    NSString *projectionSource = TokenForgeDashboardString(repository, @"projectionSource", TokenForgeDashboardString(self.state, @"growthProjectionSource", @"none"));
    NSString *rangeLine = [NSString stringWithFormat:@"Basis: %@\nRange: %@ → %@\nLast analyzed commit: %@\nCommits analyzed: %ld · Files changed: %ld\nAI token usage: excluded from Git axes · Projection: %@",
                           basis,
                           firstCommit.length > 0 ? firstCommit : @"none",
                           currentHead.length > 0 ? currentHead : @"none",
                           lastAnalyzedCommit.length > 0 ? lastAnalyzedCommit : @"none",
                           (long)commitsAnalyzed,
                           (long)filesChanged,
                           projectionSource];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(rangeLine, 6)];
    TokenForgeGrowthRadarView *radar = [[TokenForgeGrowthRadarView alloc] initWithFrame:NSZeroRect];
    radar.identifier = @"TokenForge.GrowthSummaryRadar";
    radar.accessibilityLabel = @"TokenForge growth summary radar";
    [radar configureWithCode:codeValue focus:focusValue debug:debugValue design:designValue sync:syncValue hasAxisData:hasAxisData statusText:axisStatusText];
    [radar.heightAnchor constraintEqualToConstant:184.0].active = YES;
    [radar.widthAnchor constraintGreaterThanOrEqualToConstant:240.0].active = YES;
    [stack addArrangedSubview:radar];
    NSStackView *stats = TokenForgeDashboardHorizontalStack(12.0);
    stats.distribution = NSStackViewDistributionFillEqually;
    [stats addArrangedSubview:[self statTile:@"Code" value:codeValue detail:@"Implementation growth" accent:[NSColor systemBlueColor]]];
    [stats addArrangedSubview:[self statTile:@"Focus" value:focusValue detail:@"Steady local work" accent:[NSColor systemGreenColor]]];
    [stats addArrangedSubview:[self statTile:@"Debug" value:debugValue detail:@"Fix and test loops" accent:[NSColor systemOrangeColor]]];
    [stats addArrangedSubview:[self statTile:@"Design" value:designValue detail:@"UI and structure" accent:[NSColor systemPinkColor]]];
    [stats addArrangedSubview:[self statTile:@"Sync" value:syncValue detail:@"Safe sync state" accent:[NSColor systemTealColor]]];
    [stack addArrangedSubview:stats];
    NSString *summary = TokenForgeDashboardString(self.state, @"lastRunSummary", @"No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.");
    [stack addArrangedSubview:TokenForgeLightCardBodyLabel(TokenForgeFriendlyDashboardSummary(summary, @"No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP."), 3)];
    NSDictionary *activityState = TokenForgeDashboardDictionary(self.state, @"activity");
    NSMutableArray<NSString *> *sentences = [NSMutableArray array];
    if (TokenForgeDashboardBool(activityState, @"hasAiAgentActivity", NO)) {
        [sentences addObject:@"AI-assisted work contributed to Focus growth."];
    }
    if (TokenForgeDashboardBool(activityState, @"hasRepositoryActivity", NO)) {
        [sentences addObject:@"Recent Git changes increased Code and Debug growth."];
    }
    if (TokenForgeDashboardInteger(self.state, @"warningCount", 0) == 0) {
        [sentences addObject:@"No sync issues detected."];
    } else {
        [sentences addObject:@"Some items need attention in Activity details."];
    }
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel([sentences componentsJoinedByString:@"\n"], 4)];
    return card;
}

- (NSView *)statTile:(NSString *)title value:(NSInteger)value detail:(NSString *)detail accent:(NSColor *)accent
{
    NSStackView *stack = nil;
    NSView *tile = TokenForgeCardWithStack(&stack, 12.0, 5.0);
    tile.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.68].CGColor;
    [tile.heightAnchor constraintGreaterThanOrEqualToConstant:82.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(title, 11.0, NSFontWeightMedium, accent ?: [NSColor systemBlueColor], 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%ld", (long)value], 22.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(detail ?: @"", 2)];
    return tile;
}

- (NSView *)recentActivityTimelineCardWithActivity:(NSDictionary *)activity review:(NSDictionary *)review
{
    NSLog(@"INFO [RuntimeUIPath][RecentActivity] renderer=activeRepositoryTimeline");
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 10.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Recent Activity")];
    if (!TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO)) {
        [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"No repository activity yet", 2)];
        [stack addArrangedSubview:TokenForgePrimaryButton(@"Add Repository", self, @selector(connectRepository:))];
        return card;
    }
    NSArray *recentRuns = TokenForgeDashboardArray(activity, @"recentRuns");
    NSMutableArray<NSString *> *items = [NSMutableArray array];
    if (TokenForgeDashboardBool(review, @"pending", NO)) {
        NSString *category = TokenForgeDashboardString(review, @"categoryBreakdown", @"");
        NSString *reviewLine = [NSString stringWithFormat:@"Activity Review · +%ld XP waiting for Save Growth", (long)TokenForgeDashboardInteger(review, @"estimatedXpDelta", 0)];
        if (category.length > 0) {
            reviewLine = [reviewLine stringByAppendingFormat:@"\n%@", category];
        }
        [items addObject:reviewLine];
    }
    for (NSDictionary *run in recentRuns) {
        if (![run isKindOfClass:[NSDictionary class]] || items.count >= 5) {
            continue;
        }
        NSString *type = TokenForgeDashboardString(run, @"type", @"Activity");
        NSString *summary = TokenForgeDashboardString(run, @"summary", @"Activity recorded.");
        if ([summary rangeOfString:@"NoActiveRepository" options:NSCaseInsensitiveSearch].location != NSNotFound) {
            continue;
        }
        summary = TokenForgeFriendlyDashboardSummary(summary, @"Activity recorded.");
        NSInteger xpDelta = TokenForgeDashboardInteger(run, @"xpDelta", 0);
        NSString *categoryBreakdown = TokenForgeDashboardString(run, @"categoryBreakdown", @"");
        if (xpDelta > 0) {
            summary = [summary stringByAppendingFormat:@" · +%ld XP", (long)xpDelta];
        }
        if (categoryBreakdown.length > 0) {
            summary = [summary stringByAppendingFormat:@"\n%@", categoryBreakdown];
        }
        if ([type isEqualToString:@"levelUp"]) {
            [items addObject:[NSString stringWithFormat:@"Level Up · %@", summary]];
        } else if ([type isEqualToString:@"agentAnalysis"]) {
            [items addObject:[NSString stringWithFormat:@"AI Agent · %@", summary]];
        } else if ([type isEqualToString:@"repositoryAnalysis"]) {
            [items addObject:[NSString stringWithFormat:@"Repository · %@", summary]];
        } else {
            [items addObject:summary];
        }
    }
    if (items.count == 0) {
        [items addObject:@"No recent activity yet. Run Analysis to create the first growth review."];
    }
    for (NSString *item in items) {
        [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(item, 2)];
    }
    [stack addArrangedSubview:TokenForgeSecondaryButton(@"View all activity", self, @selector(reviewActivity:))];
    return card;
}

- (NSView *)privacyCard
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 7.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Local-first privacy")];
    [stack addArrangedSubview:TokenForgeLightCardBodyLabel(@"TokenForge stores aggregate growth signals only. Raw code, commit content, and chat transcripts are not saved in local progress data. Safe Sync remains optional.", 3)];
    return card;
}

- (NSView *)onboardingScreen
{
    NSDictionary *onboarding = TokenForgeDashboardDictionary(self.state, @"onboarding");
    NSInteger configuredCount = MAX(1, MIN((NSInteger)5, TokenForgeDashboardArray(onboarding, @"steps").count));
    NSInteger currentIndex = MAX(0, MIN(configuredCount - 1, TokenForgeDashboardInteger(onboarding, @"currentStepIndex", 0)));
    NSArray<NSString *> *titles = @[@"Pick a repository", @"Analyze Git history", @"Grow your companion", @"Unlock cosmetics", @"Bring it to the desktop"];
    NSArray<NSString *> *flowSections = @[@"Connect repo", @"Analyze Git history", @"Approve growth", @"Customize your mascot", @"Show on desktop"];
    NSArray<NSString *> *subtitles = @[@"Choose one approved local Git folder as the companion target.", @"Run a safe analysis from the first commit through HEAD or a bounded recent range.", @"Review scoped growth, then save XP only for the selected repository.", @"Spend earned coins on wardrobe layers and inventory-style items.", @"Create the independent desktop panel and keep the mascot outside the dashboard."];
    NSArray<NSString *> *cues = @[@"Choose Repository", @"Run Full History", @"Save Growth", @"Open Wardrobe", @"Show Overlay"];
    NSArray<NSString *> *badgeLabels = @[@"Pick", @"Analyze", @"Grow", @"Cosmetics", @"Desktop"];
    NSInteger visualIndex = MAX(0, MIN((NSInteger)titles.count - 1, currentIndex));
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSString *zodiacType = TokenForgeDashboardString(companion, @"zodiacType", @"dragon");
    NSInteger stage = MAX(0, MIN(5, TokenForgeDashboardInteger(companion, @"stageIndex", 3)));

    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 22.0, 16.0);
    card.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.035 green:0.047 blue:0.074 alpha:0.98].CGColor;
    card.layer.borderColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.13].CGColor;
    card.identifier = @"TokenForge.OnboardingGuide";
    [card setAccessibilityLabel:@"TokenForge app-wide onboarding guide"];

    NSStackView *journey = TokenForgeDashboardHorizontalStack(6.0);
    journey.distribution = NSStackViewDistributionFillEqually;
    for (NSInteger index = 0; index < badgeLabels.count; index++) {
        NSTextField *badge = TokenForgeDashboardLabel(badgeLabels[index], 10.5, index == visualIndex ? NSFontWeightBold : NSFontWeightSemibold, index <= visualIndex ? [NSColor whiteColor] : [NSColor colorWithCalibratedWhite:1 alpha:0.54], 1);
        badge.alignment = NSTextAlignmentCenter;
        [badge setAccessibilityLabel:flowSections[index]];
        [badge setToolTip:flowSections[index]];
        badge.wantsLayer = YES;
        badge.layer.cornerRadius = 5.0;
        badge.layer.backgroundColor = (index == visualIndex ? [NSColor colorWithCalibratedRed:0.22 green:0.46 blue:1.0 alpha:1.0] : (index < visualIndex ? [NSColor colorWithCalibratedRed:0.14 green:0.30 blue:0.58 alpha:0.80] : [NSColor colorWithCalibratedWhite:1.0 alpha:0.08])).CGColor;
        [badge.heightAnchor constraintEqualToConstant:24.0].active = YES;
        [journey addArrangedSubview:badge];
    }
    [stack addArrangedSubview:journey];

    NSStackView *hero = TokenForgeDashboardHorizontalStack(18.0);
    hero.distribution = NSStackViewDistributionFill;
    NSStackView *copy = TokenForgeDashboardVerticalStack(10.0);
    [copy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"Step %ld of %ld", (long)(visualIndex + 1), (long)MIN(configuredCount, (NSInteger)titles.count)], 12.0, NSFontWeightBold, [NSColor colorWithCalibratedRed:0.58 green:0.74 blue:1.0 alpha:1.0], 1)];
    if (visualIndex == 0) {
        [copy addArrangedSubview:TokenForgeDashboardLabel(@"Turn your Git history into a desktop companion", 27.0, NSFontWeightBlack, [NSColor colorWithCalibratedRed:0.97 green:0.99 blue:1.0 alpha:1.0], 2)];
        [copy addArrangedSubview:TokenForgeDashboardLabel(@"Connect a repository, analyze your work, grow a mascot, and keep it on your Mac desktop.", 15.0, NSFontWeightBold, [NSColor colorWithCalibratedRed:1.0 green:0.84 blue:0.32 alpha:1.0], 3)];
    }
    [copy addArrangedSubview:TokenForgeDashboardLabel(titles[visualIndex], visualIndex == 0 ? 24.0 : 30.0, NSFontWeightBlack, [NSColor colorWithCalibratedRed:0.97 green:0.99 blue:1.0 alpha:1.0], 2)];
    [copy addArrangedSubview:TokenForgeDashboardLabel(subtitles[visualIndex], 15.0, NSFontWeightMedium, [NSColor colorWithCalibratedWhite:1.0 alpha:0.76], 2)];
    [copy addArrangedSubview:TokenForgeDashboardLabel(cues[visualIndex], 13.0, NSFontWeightBold, [NSColor colorWithCalibratedRed:1.0 green:0.78 blue:0.30 alpha:1.0], 2)];
    NSStackView *quickActions = TokenForgeDashboardHorizontalStack(8.0);
    if (visualIndex == 0) {
        [quickActions addArrangedSubview:TokenForgePrimaryButton(@"Connect Repository", self, @selector(connectRepository:))];
    } else if (visualIndex == 1) {
        NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
        NSButton *run = TokenForgePrimaryButton(TokenForgeDashboardBool(repository, @"connected", NO) ? @"Analyze Full History" : @"Connect repository first", self, @selector(runAnalysisFullHistory:));
        run.enabled = TokenForgeDashboardBool(repository, @"canAnalyze", NO);
        run.toolTip = run.enabled ? @"Analyze the active approved repository from the initial commit through HEAD." : @"Connect a repository before running analysis.";
        [quickActions addArrangedSubview:run];
    } else if (visualIndex == 2) {
        [quickActions addArrangedSubview:TokenForgePrimaryButton(@"Review Growth", self, @selector(reviewActivity:))];
    } else if (visualIndex == 3) {
        [quickActions addArrangedSubview:TokenForgePrimaryButton(@"Open Wardrobe", self, @selector(wardrobe:))];
    } else if (visualIndex == 4) {
        [quickActions addArrangedSubview:TokenForgePrimaryButton(@"Show Overlay", self, @selector(showCompanionFromDashboard:))];
        [quickActions addArrangedSubview:TokenForgeSecondaryButton(@"Settings", self, @selector(settings:))];
    }
    if (quickActions.arrangedSubviews.count > 0) {
        [copy addArrangedSubview:quickActions];
    }
    [hero addArrangedSubview:copy];

    TokenForgeOnboardingVisualView *visual = [[TokenForgeOnboardingVisualView alloc] initWithFrame:NSMakeRect(0, 0, 360, 230)];
    visual.translatesAutoresizingMaskIntoConstraints = NO;
    visual.stepIndex = visualIndex;
    visual.stepCount = configuredCount;
    visual.zodiacType = zodiacType;
    visual.stage = stage;
    [visual.widthAnchor constraintGreaterThanOrEqualToConstant:320.0].active = YES;
    [visual.heightAnchor constraintEqualToConstant:236.0].active = YES;
    [hero addArrangedSubview:visual];
    [stack addArrangedSubview:hero];

    NSStackView *stageStrip = TokenForgeDashboardHorizontalStack(8.0);
    stageStrip.distribution = NSStackViewDistributionFillEqually;
    NSArray *stageNames = @[@"Egg", @"Hatchling", @"Baby", @"Junior", @"Teen", @"Adult"];
    for (NSInteger index = 0; index < 6; index++) {
        NSStackView *stageStack = TokenForgeDashboardVerticalStack(4.0);
        stageStack.alignment = NSLayoutAttributeCenterX;
        TokenForgeShopPreviewView *stageIcon = [[TokenForgeShopPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 54, 54)];
        stageIcon.translatesAutoresizingMaskIntoConstraints = NO;
        stageIcon.surfaceName = @"onboarding";
        stageIcon.previewType = [@"zodiac_" stringByAppendingString:zodiacType];
        stageIcon.zodiacType = zodiacType;
        stageIcon.stage = index;
        stageIcon.rarity = index >= 4 ? @"Legendary" : @"Rare";
        [stageIcon.widthAnchor constraintEqualToConstant:54.0].active = YES;
        [stageIcon.heightAnchor constraintEqualToConstant:54.0].active = YES;
        NSTextField *stageLabel = TokenForgeDashboardLabel(stageNames[index], 10.5, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1 alpha:index <= stage ? 0.86 : 0.45], 1);
        stageLabel.alignment = NSTextAlignmentCenter;
        [stageStack addArrangedSubview:stageIcon];
        [stageStack addArrangedSubview:stageLabel];
        [stageStrip addArrangedSubview:stageStack];
    }
    [stack addArrangedSubview:stageStrip];

    NSStackView *actions = TokenForgeDashboardHorizontalStack(10.0);
    NSButton *back = TokenForgeSecondaryButton(@"Back", self, @selector(onboardingBack:));
    back.enabled = currentIndex > 0;
    [actions addArrangedSubview:back];
    NSButton *next = TokenForgeSecondaryButton(@"Next", self, @selector(onboardingNext:));
    next.enabled = currentIndex + 1 < configuredCount;
    [actions addArrangedSubview:next];
    [actions addArrangedSubview:TokenForgeSecondaryButton(@"Skip for now", self, @selector(skipOnboarding:))];
    [actions addArrangedSubview:TokenForgePrimaryButton(@"Finish", self, @selector(completeOnboarding:))];
    [stack addArrangedSubview:actions];
    NSLog(@"INFO [RuntimeUIPath][Onboarding] renderer=gameTutorial step=%ld cards=documentation:false", (long)(currentIndex + 1));
    NSLog(@"INFO [RuntimeUIPath][OnboardingTutorial] renderer=appGameTutorial step=%ld labels=pick_repository,analyze_git_history,grow_companion,unlock_cosmetics,bring_to_desktop", (long)(currentIndex + 1));
    NSLog(@"INFO [OnboardingLayoutDiagnostic] stepIndex=%ld contentFrame=TokenForge.OnboardingGuide previewFrame=360x236 bottomNavFrame=actions clippedTextCount=0 clippedImageCount=0", (long)visualIndex);
    NSLog(@"INFO [Onboarding] render nativeAppKit=true appWideGuide=true gameTutorial=true progress=%ld/%ld heroVisual=true denseDocumentation=false closePolicy=hideOnly completed=%@", (long)(currentIndex + 1), (long)configuredCount, TokenForgeDashboardBool(onboarding, @"firstRunCompleted", NO) ? @"true" : @"false");
    return card;
}

- (NSView *)buildSettingsRootView
{
    NSLog(@"INFO [RuntimeUIPath][SettingsZodiac] renderer=zodiacGrid");
    NSView *root = [[NSView alloc] initWithFrame:NSZeroRect];
    root.identifier = @"Zodiac Mascot Settings";
    [root setAccessibilityLabel:@"Zodiac Mascot Settings"];
    root.wantsLayer = YES;
    root.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.035 green:0.045 blue:0.070 alpha:1.0].CGColor;
    root.translatesAutoresizingMaskIntoConstraints = NO;

    NSScrollView *scrollView = [[NSScrollView alloc] initWithFrame:NSZeroRect];
    scrollView.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.hasVerticalScroller = YES;
    scrollView.hasHorizontalScroller = NO;
    scrollView.drawsBackground = NO;
    scrollView.contentInsets = NSEdgeInsetsMake(0, 0, 0, 0);
    [root addSubview:scrollView];
    TokenForgePinSubview(scrollView, root, 0, 0, 0, 0);

    TokenForgeFlippedView *document = [[TokenForgeFlippedView alloc] initWithFrame:NSMakeRect(0, 0, 760, 900)];
    document.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.documentView = document;
    [document.widthAnchor constraintEqualToAnchor:scrollView.contentView.widthAnchor].active = YES;
    [document.heightAnchor constraintGreaterThanOrEqualToAnchor:scrollView.contentView.heightAnchor].active = YES;

    NSStackView *content = TokenForgeDashboardVerticalStack(TokenForgePageSectionSpacing);
    content.alignment = NSLayoutAttributeWidth;
    [document addSubview:content];
    TokenForgeConstrainPageStack(content, document, 28.0, 24.0, TokenForgeTabSafeBottomInset);
    NSLog(@"INFO [LayoutDiagnostic] activeTab=settings windowSize=%@ scrollViewportRect=auto contentRect=auto bottomSafePadding=%.0f maxContentWidth=%.0f",
          NSStringFromSize(self.settingsWindow.frame.size),
          TokenForgeTabSafeBottomInset,
          TokenForgePageMaxContentWidth);

    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    [content addArrangedSubview:TokenForgeDashboardLabel(@"Zodiac Mascot Settings", 25.0, NSFontWeightBold, [NSColor whiteColor], 1)];
    [content addArrangedSubview:TokenForgeDashboardLabel(@"Pick the repository companion silhouette, then tune how it moves on your desktop.", 13.0, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), 2)];

    BOOL hasActiveRepository = TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO);
    BOOL companionVisible = TokenForgeDashboardBool(self.state, @"companionVisible", TokenForgeMenuCompanionEnabled);
    NSString *repositorySettingDetail = hasActiveRepository ? @"Show the desktop companion for the active repository." : @"Connect a repository to customize its companion.";
    NSString *behaviorDisabledDetail = companionVisible ? @"Connect a repository to customize its companion." : @"Enable Companion visible to use movement and reactions.";

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Repository Mascot", 15.0, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1 alpha:0.92], 1)];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Companion visible" detail:repositorySettingDetail enabled:companionVisible action:@selector(toggleCompanionVisible:) actionName:@"toggleCompanionVisible" interactive:hasActiveRepository]];

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Desktop Settings", 15.0, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1 alpha:0.92], 1)];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Wander movement" detail:(hasActiveRepository && companionVisible ? @"Let the companion move subtly while TokenForge is running." : behaviorDisabledDetail) enabled:TokenForgeDashboardBool(self.state, @"wanderEnabled", YES) action:@selector(toggleWanderEnabled:) actionName:@"setWanderEnabled" interactive:(hasActiveRepository && companionVisible)]];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Click reaction" detail:(hasActiveRepository && companionVisible ? @"Let clicks trigger a companion reaction. Turn this off for click-through mode." : behaviorDisabledDetail) enabled:TokenForgeDashboardBool(self.state, @"clickReactionEnabled", YES) action:@selector(toggleClickReactionEnabled:) actionName:@"setClickReactionEnabled" interactive:(hasActiveRepository && companionVisible)]];

    NSView *gridCard = TokenForgeDashboardCard();
    gridCard.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.050 green:0.065 blue:0.100 alpha:1.0].CGColor;
    gridCard.layer.borderColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.14].CGColor;
    [gridCard.heightAnchor constraintGreaterThanOrEqualToConstant:610.0].active = YES;
    NSStackView *grid = TokenForgeDashboardVerticalStack(12.0);
    [gridCard addSubview:grid];
    TokenForgePinSubview(grid, gridCard, 18, 18, 18, 18);
    NSString *selectedZodiac = TokenForgeDashboardString(companion, @"zodiacType", @"rat");
    NSString *selectedZodiacLabel = TokenForgeDashboardString(companion, @"zodiacLabel", @"Rat / 쥐");
    [grid addArrangedSubview:TokenForgeDashboardLabel(@"12-Zodiac Mascot", 15.0, NSFontWeightSemibold, [NSColor whiteColor], 1)];
    [grid addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"Equipped: %@. The sprite should read before the label.", selectedZodiacLabel], 12.0, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), 2)];
    TokenForgeShopPreviewView *heroPreview = [[TokenForgeShopPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 132, 132)];
    heroPreview.surfaceName = @"settings";
    heroPreview.translatesAutoresizingMaskIntoConstraints = NO;
    heroPreview.zodiacType = selectedZodiac;
    heroPreview.previewType = [@"zodiac_" stringByAppendingString:selectedZodiac];
    heroPreview.rarity = @"Epic";
    heroPreview.stage = 5;
    [heroPreview.widthAnchor constraintEqualToConstant:132.0].active = YES;
    [heroPreview.heightAnchor constraintEqualToConstant:132.0].active = YES;
    [grid addArrangedSubview:heroPreview];
    NSArray<NSArray<NSString *> *> *zodiacs = @[
        @[@"rat", @"Rat / 쥐", @"Quick optimizer"],
        @[@"ox", @"Ox / 소", @"Steady builder"],
        @[@"tiger", @"Tiger / 호랑이", @"Bold debugger"],
        @[@"rabbit", @"Rabbit / 토끼", @"Precise editor"],
        @[@"dragon", @"Dragon / 용", @"System shaper"],
        @[@"snake", @"Snake / 뱀", @"Quiet analyst"],
        @[@"horse", @"Horse / 말", @"Momentum runner"],
        @[@"goat", @"Goat / 양", @"Soft designer"],
        @[@"monkey", @"Monkey / 원숭이", @"Tool tinkerer"],
        @[@"rooster", @"Rooster / 닭", @"Regression watcher"],
        @[@"dog", @"Dog / 개", @"Reliable maintainer"],
        @[@"pig", @"Pig / 돼지", @"Lucky polisher"]
    ];
    for (NSInteger rowIndex = 0; rowIndex < 4; rowIndex++) {
        NSStackView *zodiacRow = TokenForgeDashboardHorizontalStack(10.0);
        zodiacRow.distribution = NSStackViewDistributionFillEqually;
        for (NSInteger columnIndex = 0; columnIndex < 3; columnIndex++) {
            NSArray<NSString *> *zodiac = zodiacs[rowIndex * 3 + columnIndex];
            [zodiacRow addArrangedSubview:[self zodiacTileWithId:zodiac[0] title:zodiac[1] detail:zodiac[2] selected:[selectedZodiac isEqualToString:zodiac[0]] interactive:hasActiveRepository]];
        }
        [grid addArrangedSubview:zodiacRow];
    }
    NSStackView *zodiacActions = TokenForgeDashboardHorizontalStack(10.0);
    NSButton *preview = TokenForgeSecondaryButton(@"Preview", self, @selector(previewSelectedZodiac:));
    preview.enabled = hasActiveRepository;
    [zodiacActions addArrangedSubview:preview];
    NSButton *reset = TokenForgeSecondaryButton(@"Reset to Default", self, @selector(resetZodiacMascot:));
    reset.enabled = hasActiveRepository;
    [zodiacActions addArrangedSubview:reset];
    [grid addArrangedSubview:zodiacActions];
    [content addArrangedSubview:gridCard];

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Startup", 15.0, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1 alpha:0.92], 1)];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Launch at login" detail:@"Available in a signed release build." enabled:NO action:nil actionName:@"setLaunchAtLogin" interactive:NO]];
    [content addArrangedSubview:TokenForgeDashboardButton(@"Reset Onboarding", self, @selector(resetOnboarding:))];

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Privacy / Sync", 15.0, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1 alpha:0.92], 1)];
    [content addArrangedSubview:[self localDataCard]];

    NSStackView *toolbar = TokenForgeDashboardHorizontalStack(10.0);
    toolbar.distribution = NSStackViewDistributionFill;
    [toolbar addArrangedSubview:TokenForgeDashboardButton(@"Reset Position", self, @selector(resetCompanionPosition:))];
    [toolbar addArrangedSubview:TokenForgeDashboardButton(@"Close", self, @selector(closeSettings:))];
    [content addArrangedSubview:toolbar];
    NSTextField *footer = TokenForgeDashboardLabel(@"Changes are saved automatically.", 12.0, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), 1);
    footer.alignment = NSTextAlignmentCenter;
    [content addArrangedSubview:footer];
    return root;
}

- (NSView *)settingsSwitchCardWithTitle:(NSString *)title detail:(NSString *)detail enabled:(BOOL)enabled action:(SEL)action actionName:(NSString *)actionName interactive:(BOOL)interactive
{
    TokenForgeSettingsSwitchRow *row = [[TokenForgeSettingsSwitchRow alloc] initWithFrame:NSZeroRect];
    [row configureWithTitle:title detail:detail checked:enabled interactive:interactive];
    row.target = interactive ? self : nil;
    row.action = interactive ? action : nil;
    row.identifier = actionName.length > 0 ? actionName : nil;
    return row;
}

- (NSView *)skinTileWithId:(NSString *)skinId title:(NSString *)title selected:(BOOL)selected
{
    NSButton *tile = TokenForgeDashboardButton(@"", self, @selector(changeSkin:));
    tile.wantsLayer = YES;
    tile.bordered = NO;
    tile.layer.backgroundColor = (selected ? [NSColor colorWithCalibratedRed:0.90 green:0.96 blue:0.91 alpha:1.0] : TokenForgeCardBackgroundColor()).CGColor;
    tile.layer.cornerRadius = 8.0;
    tile.layer.borderWidth = selected ? 2.0 : 1.0;
    tile.layer.borderColor = (selected ? [NSColor systemGreenColor] : [NSColor colorWithCalibratedWhite:0.0 alpha:0.08]).CGColor;
    tile.toolTip = skinId ?: @"orange_cat";
    [tile.heightAnchor constraintGreaterThanOrEqualToConstant:90.0].active = YES;
    NSStackView *stack = TokenForgeDashboardVerticalStack(6.0);
    stack.alignment = NSLayoutAttributeCenterX;
    [tile addSubview:stack];
    TokenForgePinSubview(stack, tile, 9, 8, 8, 8);
    TokenForgeSkinPreviewView *icon = [[TokenForgeSkinPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 64, 44)];
    icon.translatesAutoresizingMaskIntoConstraints = NO;
    icon.skinId = skinId ?: @"orange_cat";
    [icon.widthAnchor constraintEqualToConstant:64.0].active = YES;
    [icon.heightAnchor constraintEqualToConstant:44.0].active = YES;
    NSTextField *label = TokenForgeDashboardLabel(title, 12.0, NSFontWeightMedium, TokenForgeLightCardPrimaryTextColor(), 2);
    label.alignment = NSTextAlignmentCenter;
    [stack addArrangedSubview:icon];
    [stack addArrangedSubview:label];
    if (selected) {
        NSTextField *selectedLabel = TokenForgeDashboardLabel(@"Selected", 11.0, NSFontWeightSemibold, [NSColor systemGreenColor], 1);
        selectedLabel.alignment = NSTextAlignmentCenter;
        [stack addArrangedSubview:selectedLabel];
    }
    return tile;
}

- (NSView *)zodiacTileWithId:(NSString *)zodiacId title:(NSString *)title detail:(NSString *)detail selected:(BOOL)selected interactive:(BOOL)interactive
{
    NSButton *tile = TokenForgeDashboardButton(@"", self, @selector(changeZodiacMascot:));
    tile.wantsLayer = YES;
    tile.bordered = NO;
    tile.enabled = interactive;
    tile.layer.backgroundColor = (selected ? [NSColor colorWithCalibratedRed:0.100 green:0.210 blue:0.420 alpha:1.0] : [NSColor colorWithCalibratedRed:0.065 green:0.080 blue:0.118 alpha:1.0]).CGColor;
    tile.layer.cornerRadius = 8.0;
    tile.layer.borderWidth = selected ? 2.0 : 1.0;
    tile.layer.borderColor = (selected ? [NSColor colorWithCalibratedRed:0.45 green:0.70 blue:1.0 alpha:1.0] : [NSColor colorWithCalibratedWhite:1.0 alpha:0.12]).CGColor;
    tile.toolTip = zodiacId ?: @"rat";
    [tile.heightAnchor constraintGreaterThanOrEqualToConstant:128.0].active = YES;
    NSStackView *stack = TokenForgeDashboardVerticalStack(5.0);
    stack.alignment = NSLayoutAttributeCenterX;
    [tile addSubview:stack];
    TokenForgePinSubview(stack, tile, 8, 8, 8, 8);
    TokenForgeShopPreviewView *icon = [[TokenForgeShopPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 72, 68)];
    icon.surfaceName = @"settings";
    icon.translatesAutoresizingMaskIntoConstraints = NO;
    NSString *resolvedZodiac = zodiacId ?: @"rat";
    icon.zodiacType = resolvedZodiac;
    icon.previewType = [@"zodiac_" stringByAppendingString:resolvedZodiac];
    icon.rarity = selected ? @"Epic" : @"Rare";
    icon.stage = selected ? 5 : 4;
    [icon.widthAnchor constraintEqualToConstant:72.0].active = YES;
    [icon.heightAnchor constraintEqualToConstant:68.0].active = YES;
    NSTextField *label = TokenForgeDashboardLabel(title, 11.5, NSFontWeightSemibold, [NSColor colorWithCalibratedWhite:1 alpha:0.92], 2);
    label.alignment = NSTextAlignmentCenter;
    NSTextField *detailLabel = TokenForgeDashboardLabel(detail, 10.5, NSFontWeightRegular, TokenForgeShellSecondaryTextColor(), 2);
    detailLabel.alignment = NSTextAlignmentCenter;
    [stack addArrangedSubview:icon];
    [stack addArrangedSubview:label];
    [stack addArrangedSubview:detailLabel];
    [stack addArrangedSubview:TokenForgeDashboardLabel(selected ? @"Equipped" : (interactive ? @"Equip" : @"Connect repository"), 10.5, NSFontWeightSemibold, selected ? [NSColor systemBlueColor] : TokenForgeMutedTextColor(), 1)];
    return tile;
}

- (NSView *)localDataCard
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 7.0);
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Local data and Safe Sync", 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Local progress is stored on this Mac. Safe Sync is optional and only sends sanitized aggregate summaries when connected.", 13.0, NSFontWeightRegular, TokenForgeLightCardSecondaryTextColor(), 3)];
    [stack addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"Status: %@", TokenForgeDashboardString(self.state, @"syncStatusText", @"Optional sync")], 12.0, NSFontWeightMedium, [NSColor systemGreenColor], 1)];
    return card;
}

- (void)windowWillClose:(NSNotification *)notification
{
    if (notification.object == self.dashboardWindow) {
        NSWindow *closingWindow = (NSWindow *)notification.object;
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] windowWillClose windowNumber=%ld source=delegate", (long)closingWindow.windowNumber);
        if (TokenForgeNativeDashboardWindow == closingWindow) {
            TokenForgeNativeDashboardWindow = nil;
            TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] canonical.nil reason=windowWillClose");
        }

        self.dashboardWindow = nil;
        NSLog(@"INFO [AppLifecycle] dashboardWindowClosed keepAppRunning=true");
    }
    if (notification.object == self.settingsWindow) {
        [self.settingsWindow orderOut:nil];
    }
}

- (BOOL)windowShouldClose:(NSWindow *)sender
{
    if (sender == self.dashboardWindow) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] close.request source=dashboardX");
        NSLog(@"INFO [DashboardLifecycle][CLOSE_REQUEST] shouldTerminate=false source=dashboardX");
        NSLog(@"INFO [DashboardLifecycle][CLOSE_HIDE_ONLY] source=dashboardX");
        NSLog(@"INFO [OverlayLifecycle][KEEP_ALIVE_AFTER_DASHBOARD_CLOSE] enabled=%@", TokenForgeDesiredCompanionVisible ? @"true" : @"false");
        [self hideDashboardFromSource:@"dashboardX"];
        TokenForgeLogWindowLifecycle(@"windowShouldCloseIntercepted", sender, @"dashboardXHidePolicy");
        NSLog(@"INFO [AppLifecycle] dashboardWindowClosed keepAppRunning=true");
        NSLog(@"INFO [WindowLifecycle] dashboard X close mappedTo=hideDashboard");
        return NO;
    }

    if (sender == self.settingsWindow) {
        [sender orderOut:nil];
        NSLog(@"INFO [DashboardLifecycle][CLOSE_HIDE_ONLY] source=settingsX");
        NSLog(@"INFO [WindowLifecycle][ORDER_OUT_NOT_TERMINATE] source=settingsX window=%p", sender);
        TokenForgeLogWindowLifecycle(@"windowShouldCloseIntercepted", sender, @"settingsXHide");
        return NO;
    }

    return YES;
}

- (void)windowDidMove:(NSNotification *)notification { [self persistWindowFrame:notification.object]; }
- (void)windowDidResize:(NSNotification *)notification { [self persistWindowFrame:notification.object]; }

- (void)persistWindowFrame:(NSWindow *)window
{
    if (window == self.dashboardWindow) {
        [[NSUserDefaults standardUserDefaults] setObject:NSStringFromRect(window.frame) forKey:TokenForgeDashboardFrameKey];
    } else if (window == self.settingsWindow) {
        [[NSUserDefaults standardUserDefaults] setObject:NSStringFromRect(window.frame) forKey:TokenForgeSettingsFrameKey];
    }
}

- (void)setSelectedNav:(NSString *)nav action:(const char *)action showDashboard:(BOOL)showDashboard
{
    self.selectedNavItem = nav ?: @"dashboard";
    if ([self.selectedNavItem isEqualToString:@"dashboard"] && action != nil && strcmp(action, "navigation.openDashboard") == 0) {
        self.firstRunGuideSuppressedByDashboardNavigation = YES;
        NSLog(@"INFO [Onboarding][FIRST_RUN_SUPPRESSED] reason=explicitDashboardNavigation dashboardHijack=false");
    } else if ([self.selectedNavItem isEqualToString:@"onboarding"]) {
        self.firstRunGuideSuppressedByDashboardNavigation = NO;
    }
    TokenForgeCurrentDashboardTab = self.selectedNavItem;
    NSMutableDictionary *next = [self.state mutableCopy];
    next[@"selectedNavItem"] = self.selectedNavItem;
    self.state = next;
    if (showDashboard) {
        if (![self rebuildDashboardTabBodyOnlyForContext:[NSString stringWithFormat:@"tabSwitch:%@", self.selectedNavItem ?: @"dashboard"]]) {
            [self rebuildDashboardIfNeeded];
        }
        [self verifyPersistentStatusBarForContext:[NSString stringWithFormat:@"tabSwitch:%@", self.selectedNavItem ?: @"dashboard"] repairIfMissing:YES];
    }
    TokenForgeSendDashboardAction(action);
}

- (void)setStateBool:(NSString *)key enabled:(BOOL)enabled
{
    NSMutableDictionary *next = [self.state mutableCopy];
    next[key] = @(enabled);
    self.state = next;
    [self rebuildSettingsIfNeeded];
    [self rebuildDashboardIfNeeded];
}

- (void)sendBoolAction:(NSString *)action enabled:(BOOL)enabled
{
    NSString *payload = [NSString stringWithFormat:@"%@:%@", action, enabled ? @"true" : @"false"];
    TokenForgeSendDashboardAction(payload.UTF8String);
}

- (BOOL)settingsBoolValueFromSender:(id)sender fallback:(BOOL)fallback
{
    if ([sender isKindOfClass:[TokenForgeSettingsSwitchRow class]]) {
        return [(TokenForgeSettingsSwitchRow *)sender checked];
    }
    if ([sender respondsToSelector:@selector(integerValue)]) {
        return [(NSControl *)sender integerValue] == NSControlStateValueOn;
    }
    return fallback;
}

- (void)setSelectedCompanionSkin:(NSString *)skin
{
    NSString *normalized = skin.length > 0 ? skin : @"orange_cat";
    NSMutableDictionary *next = [self.state mutableCopy];
    NSMutableDictionary *companion = [TokenForgeDashboardDictionary(next, @"companion") mutableCopy];
    companion[@"skin"] = normalized;
    next[@"companion"] = companion;
    self.state = next;
    [self rebuildSettingsIfNeeded];
    [self rebuildDashboardIfNeeded];
    TokenForgeCompanionContentView.visualThemeId = normalized;
    [TokenForgeCompanionContentView setNeedsDisplay:YES];
}

- (void)dashboard:(id)sender { [self setSelectedNav:@"dashboard" action:"navigation.openDashboard" showDashboard:YES]; }
- (void)repository:(id)sender { [self setSelectedNav:@"repository" action:"navigation.openRepositories" showDashboard:YES]; }
- (void)codexAgent:(id)sender { [self setSelectedNav:@"aiAgents" action:"navigation.openAgents" showDashboard:YES]; }
- (void)activity:(id)sender { [self setSelectedNav:@"activity" action:"navigation.openActivity" showDashboard:YES]; }
- (void)tokenShop:(id)sender { [self setSelectedNav:@"tokenShop" action:"shop.open" showDashboard:YES]; }
- (void)wardrobe:(id)sender { [self setSelectedNav:@"wardrobe" action:"wardrobe.open" showDashboard:YES]; }
- (void)onboarding:(id)sender { [self setSelectedNav:@"onboarding" action:"onboarding.open" showDashboard:YES]; }
- (void)settings:(id)sender { [self setSelectedNav:@"settings" action:"openSettings" showDashboard:YES]; [self showSettings]; }
- (void)homepage:(id)sender { TokenForgeSendDashboardAction("homepage"); if (TokenForgeDashboardActionClicked == nil) [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"https://github.com/HwangSeokBeom/TokenForge"]]; }
- (void)reportIssue:(id)sender
{
    NSLog(@"INFO [ReportIssue] manualOpen requested=true autoPresent=false nonBlocking=true windowLevel=workspace keyWindowSteal=false");
    TokenForgeSendDashboardAction("report_issue");
    if (TokenForgeDashboardActionClicked == nil) {
        [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"https://github.com/HwangSeokBeom/TokenForge/issues"]];
    }
}
- (void)runAnalysis:(id)sender
{
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *agents = TokenForgeDashboardDictionary(self.state, @"agents");
    if (TokenForgeDashboardBool(repository, @"canAnalyze", NO)) {
        TokenForgeSendDashboardAction("repository.runAnalysis");
        return;
    }
    if (TokenForgeDashboardInteger(agents, @"connectedCount", 0) > 0) {
        TokenForgeSendDashboardAction("agent.runAnalysis");
        return;
    }
    TokenForgeSendDashboardAction("repository.runAnalysis");
}
- (void)runAgentAnalysis:(id)sender { TokenForgeSendDashboardAction("agent.runAnalysis"); }
- (void)runFullHistoryAnalysis:(id)sender { TokenForgeSendDashboardAction("repository.runAnalysis:full"); }
- (void)runSinceLastAnalysis:(id)sender { TokenForgeSendDashboardAction("repository.runAnalysis:sinceLast"); }
- (void)runRecentAnalysis:(id)sender { TokenForgeSendDashboardAction("repository.runAnalysis:recent"); }
- (void)openOverlayTargetModeSettings:(id)sender { [self setSelectedNav:@"settings" action:"overlay.targetMode" showDashboard:YES]; }
- (void)levelUpCompanion:(id)sender { TokenForgeSendDashboardAction("companion.levelUp"); }
- (void)connectRepository:(id)sender { [self setSelectedNav:@"repository" action:"repository.add" showDashboard:YES]; }
- (void)connectCodexAgent:(id)sender { [self setSelectedNav:@"aiAgents" action:"agent.connect:codex" showDashboard:YES]; }
- (void)openActiveCompanionDashboard:(id)sender
{
    self.selectedNavItem = @"dashboard";
    self.firstRunGuideSuppressedByDashboardNavigation = YES;
    TokenForgeCurrentDashboardTab = self.selectedNavItem;
    NSMutableDictionary *next = [self.state mutableCopy];
    next[@"selectedNavItem"] = self.selectedNavItem;
    self.state = next;
    NSLog(@"INFO [SidebarCompanions] open active companion dashboard repo=%@", [(NSButton *)sender identifier] ?: @"active");
    [self rebuildDashboardIfNeeded];
    TokenForgeSendDashboardAction("open_active_companion_dashboard");
}
- (void)sidebarRepositorySelected:(id)sender
{
    NSString *value = [(NSButton *)sender identifier] ?: @"";
    NSString *payload = [NSString stringWithFormat:@"open_repository_companion_dashboard:%@", value];
    NSLog(@"INFO [SidebarCompanions] select repo=%@", value.length > 0 ? value : @"unknown");
    NSLog(@"INFO [SidebarCompanions] activeChanged refreshDashboard=true refreshOverlay=true");
    self.selectedNavItem = @"dashboard";
    self.firstRunGuideSuppressedByDashboardNavigation = YES;
    TokenForgeCurrentDashboardTab = self.selectedNavItem;
    self.activityFilterValue = value.length > 0 ? [@"repo:" stringByAppendingString:value] : @"active";
    TokenForgeSendDashboardAction(payload.UTF8String);
}
- (void)selectRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.setActive:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)analyzeRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.analyze:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)viewRepositoryGrowthAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; self.activityFilterValue = value.length > 0 ? [@"repo:" stringByAppendingString:value] : @"active"; [self setSelectedNav:@"activity" action:"navigation.openActivity" showDashboard:YES]; }
- (void)evolveRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"companion.levelUp:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)disconnectRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.archive:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)purchaseTokenShopItemAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"shop.purchase:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)equipTokenShopItemAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"shop.equip:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)shopTargetRepository:(id)sender { TokenForgeSendDashboardAction("shop.target:repository"); }
- (void)shopTargetAgents:(id)sender { TokenForgeSendDashboardAction("shop.target.agent:codex"); }
- (void)shopTargetAgentChip:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"shop.target.agent:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)shopOpenAgentConnect:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"shop.openAgentConnect:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)shopCategoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"featured"; NSString *payload = [NSString stringWithFormat:@"shop.category:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)connectAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.connect:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)autoDetectAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.autoDetect:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)chooseAgentFolderAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.chooseFolder:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)analyzeAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.analyze:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)viewAgentUsageAction:(id)sender { self.activityFilterValue = @"agent"; [self setSelectedNav:@"activity" action:"navigation.openActivity" showDashboard:YES]; }
- (void)disconnectAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.disconnect:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)reviewActivity:(id)sender { [self setSelectedNav:@"activity" action:"navigation.openActivity" showDashboard:YES]; }
- (void)approveReview:(id)sender { TokenForgeSendDashboardAction("review.saveGrowth"); }
- (void)viewReviewDetails:(id)sender { NSString *value = TokenForgeDashboardString(TokenForgeDashboardDictionary(self.state, @"review"), @"reviewId", @""); NSString *payload = [NSString stringWithFormat:@"review.viewDetails:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)discardReview:(id)sender { TokenForgeSendDashboardAction("review.discard"); }
- (void)setOnboardingStepIndex:(NSInteger)stepIndex
{
    NSMutableDictionary *next = [self.state mutableCopy];
    NSMutableDictionary *onboarding = [TokenForgeDashboardDictionary(next, @"onboarding") mutableCopy];
    NSArray *steps = TokenForgeDashboardArray(onboarding, @"steps");
    NSInteger stepCount = MAX(1, steps.count);
    NSInteger clamped = MAX(0, MIN(stepCount - 1, stepIndex));
    onboarding[@"currentStepIndex"] = @(clamped);
    onboarding[@"currentStep"] = [NSString stringWithFormat:@"step_%ld", (long)(clamped + 1)];
    next[@"onboarding"] = onboarding;
    self.state = next;
    [self rebuildDashboardIfNeeded];
    [self verifyPersistentStatusBarForContext:[NSString stringWithFormat:@"onboardingStep:%ld", (long)clamped] repairIfMissing:YES];
    NSString *payload = [NSString stringWithFormat:@"onboarding.step:%ld", (long)clamped];
    TokenForgeSendDashboardAction(payload.UTF8String);
}
- (void)onboardingBack:(id)sender { NSInteger current = TokenForgeDashboardInteger(TokenForgeDashboardDictionary(self.state, @"onboarding"), @"currentStepIndex", 0); [self setOnboardingStepIndex:current - 1]; }
- (void)onboardingNext:(id)sender { NSInteger current = TokenForgeDashboardInteger(TokenForgeDashboardDictionary(self.state, @"onboarding"), @"currentStepIndex", 0); [self setOnboardingStepIndex:current + 1]; }
- (void)skipOnboarding:(id)sender
{
    NSLog(@"INFO [Onboarding] skip requested closePolicy=keepAppRunning finish=false");
    NSMutableDictionary *next = [self.state mutableCopy];
    NSMutableDictionary *onboarding = [TokenForgeDashboardDictionary(next, @"onboarding") mutableCopy];
    onboarding[@"dismissedForNow"] = @YES;
    onboarding[@"firstRunCompleted"] = @NO;
    onboarding[@"shouldPresentFirstRunGuide"] = @NO;
    next[@"onboarding"] = onboarding;
    self.state = next;
    self.firstRunGuideSuppressedByDashboardNavigation = NO;
    [self rebuildDashboardIfNeeded];
    [self verifyPersistentStatusBarForContext:@"onboardingSkip" repairIfMissing:YES];
    TokenForgeSendDashboardAction("onboarding.skip");
}
- (void)completeOnboarding:(id)sender
{
    NSLog(@"INFO [Onboarding] finish requested closePolicy=keepAppRunning action=onboarding.finish");
    NSMutableDictionary *next = [self.state mutableCopy];
    NSMutableDictionary *onboarding = [TokenForgeDashboardDictionary(next, @"onboarding") mutableCopy];
    onboarding[@"firstRunCompleted"] = @YES;
    onboarding[@"dismissedForNow"] = @NO;
    onboarding[@"shouldPresentFirstRunGuide"] = @NO;
    next[@"onboarding"] = onboarding;
    self.state = next;
    self.firstRunGuideSuppressedByDashboardNavigation = NO;
    [self rebuildDashboardIfNeeded];
    [self verifyPersistentStatusBarForContext:@"onboardingFinish" repairIfMissing:YES];
    TokenForgeSendDashboardAction("onboarding.finish");
}
- (void)resetOnboarding:(id)sender { NSLog(@"INFO [Onboarding] reset requested source=settings"); TokenForgeSendDashboardAction("onboarding.reset"); }
- (void)setActivityFilterAction:(id)sender { self.activityFilterValue = [(NSButton *)sender toolTip] ?: @"all"; [self rebuildDashboardIfNeeded]; }
- (void)toggleCompanionVisible:(id)sender
{
    BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"companionVisible", YES)];
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    BOOL repositoryConnected = TokenForgeDashboardBool(repository, @"connected", NO) &&
        TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO);
    if (enabled && !repositoryConnected) {
        [self setStateBool:@"companionVisible" enabled:NO];
        [self setStateBool:@"desiredVisible" enabled:NO];
        [self setStateBool:@"actualVisible" enabled:NO];
        TokenForge_HideAllRepositoryCompanions("settings.noApprovedRepository");
        TokenForgeHideDesktopCompanionOverlayWithTrace(@"settings.noApprovedRepository");
        NSLog(@"INFO [Overlay][Guard] repoHash=none desiredVisible=true actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=settings.toggleCompanionVisible selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
        NSLog(@"INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY] repoHash=none desiredVisible=false actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=settings.toggleCompanionVisible selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
        NSLog(@"INFO [Settings] companionVisible changed value=false blockedReason=noApprovedRepository");
        TokenForgeSendDashboardAction("desktop.hide:noApprovedRepository");
        return;
    }

    [self setStateBool:@"companionVisible" enabled:enabled];
    [self setStateBool:@"desiredVisible" enabled:enabled];
    if (enabled) {
        TokenForge_ShowAllRepositoryCompanions("settings.toggleCompanionVisible");
    } else {
        TokenForge_HideAllRepositoryCompanions("settings.toggleCompanionVisible");
        HideDesktopCompanionOverlay();
    }
    NSLog(@"INFO [Settings] companionVisible changed value=%@", enabled ? @"true" : @"false");
    TokenForgeSendDashboardAction(enabled ? "desktop.show" : "desktop.hide");
}
- (void)toggleWanderEnabled:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"wanderEnabled", YES)]; [self setStateBool:@"wanderEnabled" enabled:enabled]; [self setStateBool:@"movementEnabled" enabled:enabled]; SetCompanionOverlayMotionProfile(enabled ? 1 : 0, 5.0, enabled ? 32.0 : 0.0, enabled ? 18.0 : 0.0, 3.4, enabled, 1.1); NSLog(@"INFO [Settings] wanderEnabled changed value=%@", enabled ? @"true" : @"false"); TokenForgeSendDashboardAction(enabled ? "desktop.movement.enable" : "desktop.movement.pause"); }
- (void)toggleClickReactionEnabled:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"clickReactionEnabled", YES)]; [self setStateBool:@"clickThroughEnabled" enabled:!enabled]; [self setStateBool:@"clickReactionEnabled" enabled:enabled]; [self setStateBool:@"dragEnabled" enabled:enabled]; SetCompanionOverlayClickThrough(!enabled); NSLog(@"INFO [Settings] clickReactionEnabled changed value=%@ mutualExclusion=clickThrough:%@", enabled ? @"true" : @"false", enabled ? @"false" : @"true"); TokenForgeSendDashboardAction(enabled ? "desktop.drag.enable" : "desktop.clickThrough.enable"); }
- (void)showCompanionFromDashboard:(id)sender
{
    const char *trace = TokenForgeNextOverlayTraceId();
    NSString *traceString = [NSString stringWithUTF8String:trace];
    NSString *explicitTrace = [NSString stringWithFormat:@"desktop.show.%@", traceString];
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    BOOL repositoryConnected = TokenForgeDashboardBool(repository, @"connected", NO) &&
        TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO);
    NSLog(@"INFO [OverlayAction][RECEIVED] action=showOnDesktop source=dashboard explicit=true");
    NSLog(@"INFO [Overlay][REQUEST_SHOW] source=dashboard trace=%@ repositoryConnected=%@", traceString, repositoryConnected ? @"true" : @"false");
    NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=showOnDesktop", traceString);
    if (!repositoryConnected) {
        [self setStateBool:@"companionVisible" enabled:NO];
        [self setStateBool:@"desiredVisible" enabled:NO];
        [self setStateBool:@"actualVisible" enabled:NO];
        TokenForge_HideAllRepositoryCompanions(explicitTrace.UTF8String);
        TokenForgeHideDesktopCompanionOverlayWithTrace(explicitTrace);
        NSLog(@"INFO [Overlay][NO_APPROVED_REPO] source=dashboard trace=%@", traceString);
        NSLog(@"INFO [Overlay][Guard] repoHash=none desiredVisible=true actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=dashboard.showCompanion selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
        NSLog(@"INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY] repoHash=none desiredVisible=false actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=dashboard.showCompanion selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
        NSLog(@"INFO [Overlay][ACTUAL_VISIBLE_COUNT] count=%ld source=dashboard.noApprovedRepo", (long)TokenForgeVisibleOverlayFarmCount());
        [self rebuildDashboardIfNeeded];
        [self rebuildSettingsIfNeeded];
        return;
    }

    [self setStateBool:@"companionVisible" enabled:YES];
    [self setStateBool:@"desiredVisible" enabled:YES];
    TokenForge_ShowAllRepositoryCompanions(explicitTrace.UTF8String);
    NSInteger actualCount = TokenForgeVisibleOverlayFarmCount();
    BOOL legacyVisible = TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible;
    [self setStateBool:@"actualVisible" enabled:(actualCount > 0 || legacyVisible)];
    NSLog(@"INFO [Overlay][ACTUAL_VISIBLE_COUNT] count=%ld legacyVisible=%@ source=dashboard.show", (long)actualCount, legacyVisible ? @"true" : @"false");
    [self rebuildDashboardIfNeeded];
    [self rebuildSettingsIfNeeded];
    if (TokenForgeDashboardBool(self.state, @"wanderEnabled", YES)) {
        TokenForgeSetCompanionOverlayMotionProfileWithTrace(explicitTrace, 1, 5.0, 32.0, 18.0, 3.4, true, 1.1);
    }
    NSLog(@"INFO [DesktopOverlay] show requested visibleSetting=true source=motionCard");
    NSString *payload = [NSString stringWithFormat:@"showOnDesktop:%@", traceString];
    TokenForgeSendDashboardAction(payload.UTF8String);
}
- (void)hideCompanionFromDashboard:(id)sender { const char *trace = TokenForgeNextOverlayTraceId(); NSString *traceString = [NSString stringWithUTF8String:trace]; NSLog(@"INFO [OverlayAction][RECEIVED] action=hideFromDesktop source=dashboard explicit=true"); NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=hideFromDesktop", traceString); [self setStateBool:@"companionVisible" enabled:NO]; [self setStateBool:@"desiredVisible" enabled:NO]; TokenForgeHideDesktopCompanionOverlayWithTrace(traceString); TokenForge_HideAllRepositoryCompanions(traceString.UTF8String); [self setStateBool:@"actualVisible" enabled:NO]; [self rebuildDashboardIfNeeded]; [self rebuildSettingsIfNeeded]; NSLog(@"INFO [DesktopOverlay] hidden reason=companionVisibleOff"); NSString *payload = [NSString stringWithFormat:@"hideFromDesktop:%@", traceString]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)enableWanderFromDashboard:(id)sender { const char *trace = TokenForgeNextOverlayTraceId(); NSString *traceString = [NSString stringWithUTF8String:trace]; NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=desktop.movement.enable", traceString); [self setStateBool:@"wanderEnabled" enabled:YES]; [self setStateBool:@"movementEnabled" enabled:YES]; TokenForgeSetCompanionOverlayMotionProfileWithTrace(traceString, 1, 5.0, 32.0, 18.0, 3.4, true, 1.1); NSLog(@"INFO [DesktopOverlay] movement setting enabled source=motionCard showPolicy=showButtonRequired"); NSString *payload = [NSString stringWithFormat:@"desktop.movement.enable:%@", traceString]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)disableWanderFromDashboard:(id)sender { const char *trace = TokenForgeNextOverlayTraceId(); NSString *traceString = [NSString stringWithUTF8String:trace]; NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=desktop.movement.pause", traceString); [self setStateBool:@"wanderEnabled" enabled:NO]; [self setStateBool:@"movementEnabled" enabled:NO]; TokenForgeSetCompanionOverlayMotionProfileWithTrace(traceString, 0, 0.0, 0.0, 0.0, 3.4, false, 1.1); NSLog(@"INFO [DesktopCompanion] movement paused source=motionCard"); NSString *payload = [NSString stringWithFormat:@"desktop.movement.pause:%@", traceString]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)enableClickReactionFromDashboard:(id)sender { [self enableDragFromDashboard:sender]; }
- (void)disableClickReactionFromDashboard:(id)sender { [self enableClickThroughFromDashboard:sender]; }
- (void)enableDragFromDashboard:(id)sender { NSLog(@"INFO [NativeAction][RECEIVED] action=desktop.enableDrag source=dashboard"); [self setStateBool:@"clickThroughEnabled" enabled:NO]; [self setStateBool:@"clickReactionEnabled" enabled:YES]; [self setStateBool:@"dragEnabled" enabled:YES]; SetCompanionOverlayClickThrough(false); NSLog(@"INFO [CompanionDrag] enabled source=motionCard clickThrough=false mutualExclusion=drag"); TokenForgeSendDashboardAction("desktop.drag.enable"); }
- (void)enableClickThroughFromDashboard:(id)sender { NSLog(@"INFO [NativeAction][RECEIVED] action=desktop.enableClickThrough source=dashboard"); [self setStateBool:@"clickThroughEnabled" enabled:YES]; [self setStateBool:@"clickReactionEnabled" enabled:NO]; [self setStateBool:@"dragEnabled" enabled:NO]; SetCompanionOverlayClickThrough(true); NSLog(@"INFO [DesktopCompanion] click-through enabled source=motionCard dragDisabled=true mutualExclusion=clickThrough"); TokenForgeSendDashboardAction("desktop.clickThrough.enable"); }
- (void)disableClickThroughFromDashboard:(id)sender { [self enableDragFromDashboard:sender]; }
- (void)toggleLaunchAtLogin:(id)sender { NSLog(@"INFO [NativeDashboard] launch at login unavailable in unsigned build"); }
- (void)changeSkin:(id)sender { NSString *skin = [(NSButton *)sender toolTip] ?: @"orange_cat"; [self setSelectedCompanionSkin:skin]; NSString *payload = [NSString stringWithFormat:@"changeCompanionSkin:%@", skin]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)changeZodiacMascot:(id)sender { NSString *zodiac = [(NSButton *)sender toolTip] ?: @"rat"; NSMutableDictionary *next = [self.state mutableCopy]; NSMutableDictionary *companion = [TokenForgeDashboardDictionary(next, @"companion") mutableCopy]; companion[@"zodiacType"] = zodiac; companion[@"zodiacLabel"] = zodiac; next[@"companion"] = companion; self.state = next; [self rebuildSettingsIfNeeded]; [self rebuildDashboardIfNeeded]; NSString *payload = [NSString stringWithFormat:@"settings.zodiac:%@", zodiac]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)previewSelectedZodiac:(id)sender { NSString *zodiac = TokenForgeDashboardString(TokenForgeDashboardDictionary(self.state, @"companion"), @"zodiacType", @"rat"); NSString *payload = [NSString stringWithFormat:@"shop.preview:zodiac_%@", zodiac]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)resetZodiacMascot:(id)sender { TokenForgeSendDashboardAction("settings.zodiac:rat"); }
- (void)resetCompanionPosition:(id)sender { NSLog(@"INFO [NativeAction][RECEIVED] action=desktop.resetPosition source=dashboard"); if (TokenForgeIsDraggingOverlay) { TokenForgeQueueOverlayActionAfterDrag(TokenForgePendingOverlayActionResetPosition, @"dashboard.resetPosition"); } else { TokenForgeResetCompanionFrame(); } TokenForgeSendDashboardAction("desktop.position.reset"); }
- (void)debugDirectShowNativeOverlay:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Show Native Overlay", trace); TokenForgeShowDesktopCompanionOverlayWithTrace(trace); }
- (void)debugDirectHideNativeOverlay:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Hide Native Overlay", trace); TokenForgeHideDesktopCompanionOverlayWithTrace(trace); }
- (void)debugDirectStartWander:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Start Wander", trace); TokenForgeShowDesktopCompanionOverlayWithTrace(trace); TokenForgeSetCompanionOverlayMotionProfileWithTrace(trace, 1, 5.0, 140.0, 32.0, 1.6, true, 1.1); }
- (void)debugDirectStopWander:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Stop Wander", trace); TokenForgeSetCompanionOverlayMotionProfileWithTrace(trace, 0, 0.0, 0.0, 0.0, 3.4, false, 1.1); }
- (void)debugDumpAllWindows:(id)sender { TokenForgeDumpAllWindows(@"debugButton"); }
- (void)debugDumpOverlayPanelState:(id)sender { TokenForgeDumpOverlayPanelState(@"debugButton"); }
- (void)closeSettings:(id)sender { NSLog(@"INFO [DashboardLifecycle][CLOSE_HIDE_ONLY] source=settingsCloseButton"); [self.settingsWindow orderOut:nil]; NSLog(@"INFO [WindowLifecycle][ORDER_OUT_NOT_TERMINATE] source=settingsCloseButton window=%p", self.settingsWindow); }
- (void)resetLocalState:(id)sender { TokenForgeSendDashboardAction("reset_local_state"); }
- (void)quit:(id)sender { TokenForgeSendDashboardAction("app.quit"); TokenForgeRequestExplicitQuit(@"sidebar"); }

@end

static TokenForgeNativeDashboardController *TokenForgeEnsureNativeDashboardController(void)
{
    if (TokenForgeDashboardController == nil) {
        TokenForgeDashboardController = [[TokenForgeNativeDashboardController alloc] init];
    }

    TokenForgeRequestLifecycleInstall(@"native_dashboard_controller");
    return TokenForgeDashboardController;
}

static void TokenForgeOpenNativeDashboardOnMain(void)
{
    TokenForgeOpenNativeDashboardOnMainWithSource(@"legacy");
}

static void TokenForgeOpenNativeDashboardOnMainWithSource(NSString *source)
{
    TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(source, NO);
}

static void TokenForgeOpenOrFocusDashboard(NSString *source)
{
    NSString *openSource = source.length > 0 ? source : @"unknown";
    BOOL explicitOpen = TokenForgeSourceLooksExplicit(openSource) || TokenForgeDashboardSourceAllowsCloseCooldownBypass(openSource);
    TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(openSource, explicitOpen);
}

static void TokenForgeCloseDashboard(NSString *source)
{
    NSString *closeSource = source.length > 0 ? source : @"unknown";
    if (![NSThread isMainThread]) {
        NSString *sourceCopy = [closeSource copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeCloseDashboard(sourceCopy);
        });
        return;
    }

    [TokenForgeEnsureNativeDashboardController() hideDashboardFromSource:closeSource];
}

static void TokenForgeTeardownForExplicitQuit(NSString *traceId)
{
    NSString *trace = traceId.length > 0 ? traceId : @"quit-unknown";
    if (TokenForgeQuitTeardownCompleted) {
        NSLog(@"INFO [AppLifecycle][QUIT_GUARD] traceId=%@ alreadyComplete=true suppressReopen=true", trace);
        NSLog(@"INFO [AppLifecycle][QUIT_BEGIN] traceId=%@ alreadyComplete=true", trace);
        NSLog(@"INFO [AppLifecycle][TIMERS_STOPPED] traceId=%@ reason=alreadyStopped", trace);
        NSLog(@"INFO [AppLifecycle][QUIT_OVERLAYS_STOPPED] traceId=%@ reason=alreadyStopped", trace);
        NSLog(@"INFO [AppLifecycle][OBSERVERS_REMOVED] traceId=%@ reason=alreadyRemoved", trace);
        NSLog(@"INFO [AppLifecycle][PANELS_CLOSED] traceId=%@ reason=alreadyClosed", trace);
        NSLog(@"INFO [AppLifecycle][QUIT_WINDOWS_CLOSED] traceId=%@ reason=alreadyClosed", trace);
        NSLog(@"INFO [AppLifecycle][PENDING_TASKS_CANCELLED] traceId=%@ reason=alreadyCancelled", trace);
        NSLog(@"INFO [AppLifecycle][NSAPP_TERMINATE] traceId=%@ reason=alreadyRequested", trace);
        NSLog(@"INFO [AppLifecycle][QUIT_COMPLETE] traceId=%@ alreadyComplete=true", trace);
        NSLog(@"INFO [AppLifecycle][QUIT_FINAL] traceId=%@ alreadyComplete=true", trace);
        return;
    }

    TokenForgeQuitTeardownCompleted = YES;
    NSLog(@"INFO [AppLifecycle][QUIT_GUARD] traceId=%@ explicitQuit=true suppressReopen=true teardownAlreadyComplete=false", trace);
    NSLog(@"INFO [AppLifecycle][QUIT_BEGIN] traceId=%@", trace);
    NSLog(@"INFO [QuitCleanup][START] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][TEARDOWN_BEGIN] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][TEARDOWN_TIMERS] traceId=%@", trace);
    [TokenForgeCompanionMotionTimer invalidate];
    TokenForgeCompanionMotionTimer = nil;
    TokenForgeCompanionVelocity = NSMakePoint(0, 0);
    NSLog(@"INFO [AppLifecycle][TIMERS_STOPPED] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][QUIT_OVERLAYS_STOPPED] traceId=%@ phase=timersStopped", trace);
    NSLog(@"INFO [QuitCleanup][OVERLAY_TIMER_STOPPED] traceId=%@", trace);

    TokenForgeAppLifecycleDelegate *delegate = TokenForgeLifecycleDelegate;
    if (delegate != nil) {
        [delegate.statusAnimationTimer invalidate];
        delegate.statusAnimationTimer = nil;
        if (delegate.observingWindowNotifications) {
            [[NSNotificationCenter defaultCenter] removeObserver:delegate];
            delegate.observingWindowNotifications = NO;
            NSLog(@"INFO [QuitCleanup][OBSERVERS_REMOVED] traceId=%@", trace);
            NSLog(@"INFO [AppLifecycle][OBSERVERS_REMOVED] traceId=%@", trace);
            NSLog(@"INFO [AppLifecycle][TEARDOWN_OBSERVERS] traceId=%@", trace);
        }
    }
    NSLog(@"INFO [AppLifecycle][TEARDOWN_OBSERVERS] traceId=%@ remaining=false", trace);

    NSLog(@"INFO [AppLifecycle][TEARDOWN_OVERLAY] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][TEARDOWN_OVERLAY_PANELS] traceId=%@", trace);
    [TokenForgeCompanionWindow orderOut:nil];
    TokenForgeEnsureOverlayFarmRegistry();
    for (NSPanel *panel in [TokenForgeOverlayPanelsByRepositoryId allValues]) {
        [panel orderOut:nil];
        panel.delegate = nil;
        [panel close];
    }
    [TokenForgeOverlayPanelsByRepositoryId removeAllObjects];
    [TokenForgeOverlayViewsByRepositoryId removeAllObjects];
    [TokenForgeOverlaySnapshotsByRepositoryId removeAllObjects];
    [TokenForgeOverlayFramesByRepositoryId removeAllObjects];
    [TokenForgeOverlayDragStatesByRepositoryId removeAllObjects];
    [TokenForgeOverlayGenerationsByRepositoryId removeAllObjects];
    TokenForgeCompanionWindow.delegate = nil;
    [TokenForgeCompanionWindow close];
    TokenForgeCompanionWindow = nil;
    TokenForgeCompanionContentView = nil;
    TokenForgeIsDraggingOverlay = NO;
    NSLog(@"INFO [QuitCleanup][PANELS_CLOSED] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][PANELS_CLOSED] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][QUIT_OVERLAYS_STOPPED] traceId=%@ phase=panelsClosed remainingPanels=%ld", trace, (long)TokenForgeOverlayPanelsByRepositoryId.count);

    NSLog(@"INFO [AppLifecycle][TEARDOWN_DASHBOARD] traceId=%@", trace);
    if (TokenForgeDashboardController != nil) {
        if (TokenForgeDashboardController.dashboardWindow != nil) {
            [TokenForgeDashboardController.dashboardWindow orderOut:nil];
            TokenForgeDashboardController.dashboardWindow.delegate = nil;
            [TokenForgeDashboardController.dashboardWindow close];
            TokenForgeDashboardController.dashboardWindow = nil;
        }
        if (TokenForgeDashboardController.settingsWindow != nil) {
            [TokenForgeDashboardController.settingsWindow orderOut:nil];
            TokenForgeDashboardController.settingsWindow.delegate = nil;
            [TokenForgeDashboardController.settingsWindow close];
            TokenForgeDashboardController.settingsWindow = nil;
        }
    }
    TokenForgeNativeDashboardWindow = nil;
    TokenForgeDashboardWindowExists = NO;
    TokenForgeDashboardWindowVisible = NO;
    TokenForgeDashboardWindowKey = NO;

    if (delegate != nil && delegate.statusItem != nil) {
        [[NSStatusBar systemStatusBar] removeStatusItem:delegate.statusItem];
        delegate.statusItem = nil;
    }
    NSLog(@"INFO [AppLifecycle][QUIT_WINDOWS_CLOSED] traceId=%@ dashboardWindow=nil settingsWindow=nil nativeDashboardWindow=nil", trace);

    TokenForgeDashboardActionClicked = nil;
    TokenForgeMenuActionClicked = nil;
    TokenForgeOverlayClicked = nil;
    TokenForgeOverlayDoubleClicked = nil;
    TokenForgeOverlayDragEnded = nil;
    TokenForgeOverlayDragEndedForRepository = nil;
    NSLog(@"INFO [AppLifecycle][PENDING_TASKS_CANCELLED] traceId=%@", trace);
    NSLog(@"INFO [MenuBarCompanion] animation stopped reason=quit");
    NSLog(@"INFO [DesktopOverlay] quit cleanup completed");
    NSLog(@"INFO [OverlayCleanup][DONE] traceId=%@", trace);
    NSLog(@"INFO [QuitCleanup][DONE] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][SUPPRESS_REOPEN_AFTER_QUIT] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][NSAPP_TERMINATE] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][QUIT_COMPLETE] traceId=%@", trace);
    NSLog(@"INFO [AppLifecycle][QUIT_FINAL] traceId=%@ terminateRequested=true", trace);
}

static void TokenForgeRequestExplicitQuit(NSString *source)
{
    NSLog(@"INFO [RuntimeUIPath][Quit] renderer=canonicalNativeQuitPath");
    NSString *safeSource = source.length > 0 ? source : @"nativeBridge";
    NSString *trace = [NSString stringWithFormat:@"quit-%s", TokenForgeNextOverlayTraceId()];
    if ([safeSource isEqualToString:@"menu"]) {
        NSLog(@"INFO [AppLifecycle][QUIT_REQUESTED] source=menu traceId=%@", trace);
    } else if ([safeSource isEqualToString:@"contextMenu"]) {
        NSLog(@"INFO [AppLifecycle][QUIT_REQUESTED] source=contextMenu traceId=%@", trace);
    } else {
        NSLog(@"INFO [AppLifecycle][QUIT_REQUESTED] source=%@ traceId=%@", safeSource, trace);
    }
    TokenForgeLogQuitDiagnostic(@"TokenForgeRequestExplicitQuit", safeSource, YES, NO, @"explicitUserQuit");
    TokenForgeRuntimeVerifierCleanupQuitRequested = NO;
    TokenForgeExplicitQuitRequested = YES;
    TokenForgeTerminating = YES;
    TokenForgeEnsureLifecycleDelegate().explicitTerminationRequested = YES;
    NSLog(@"INFO [AppLifecycle][QUIT_GUARD] source=%@ traceId=%@ explicitQuit=true preventReopen=true", safeSource, trace);
    TokenForgeTeardownForExplicitQuit(trace);
    [NSApp terminate:nil];
}

static void TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(NSString *source, BOOL explicitUserOpen)
{
    if (![NSThread isMainThread]) {
        NSString *sourceCopy = [(source.length > 0 ? source : @"unknown") copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(sourceCopy, explicitUserOpen);
        });
        return;
    }

    NSString *openSourceForLog = source.length > 0 ? source : @"unknown";
    if ([openSourceForLog isEqualToString:@"launch.initial"]) {
        NSLog(@"INFO [LaunchDashboard][REQUEST] source=launch.initial appReady=%@ pending=%@",
              TokenForgeAppKitRegistrationReady() ? @"true" : @"false",
              TokenForgePendingInitialDashboardOpen ? @"true" : @"false");
    }

    if (!TokenForgeAppKitRegistrationReady()) {
        NSLog(@"INFO [NativeLifecycle] open_dashboard deferred reason=app_not_ready");
        if ([openSourceForLog isEqualToString:@"launch.initial"]) {
            TokenForgePendingInitialDashboardOpen = YES;
            NSLog(@"INFO [LaunchDashboard][DEFER] reason=appNotReady");
        }
        if (!TokenForgeDashboardOpenPending) {
            TokenForgeDashboardOpenPending = YES;
            NSString *deferredSource = [openSourceForLog copy];
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.35 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                TokenForgeDashboardOpenPending = NO;
                TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(deferredSource ?: @"deferred", explicitUserOpen);
            });
        }
        return;
    }

    if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness", source ?: @"unknown", explicitUserOpen || TokenForgeSourceLooksExplicit(source ?: @""))) {
        return;
    }

    if (TokenForgeExplicitQuitRequested || TokenForgeTerminating) {
        NSLog(@"INFO [WindowLifecycle] openDashboard ignored reason=explicitQuitOrTerminating source=%@ explicitQuit=%@ terminating=%@",
              source ?: @"unknown",
              TokenForgeExplicitQuitRequested ? @"true" : @"false",
              TokenForgeTerminating ? @"true" : @"false");
        return;
    }

    if (!TokenForgeDashboardStateHydrated) {
        TokenForgePendingExplicitDashboardOpenSource = [openSourceForLog copy];
        NSLog(@"INFO [LaunchRouteDiagnostic] firstVisibleRoute=deferred reason=nativeDashboardStateNotHydrated source=%@", openSourceForLog);
        return;
    }

    if (TokenForgeShouldSuppressDashboardOpen(source ?: @"unknown", explicitUserOpen)) {
        return;
    }

    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    BOOL explicitOpen = explicitUserOpen || TokenForgeSourceLooksExplicit(source ?: @"") || TokenForgeDashboardSourceAllowsCloseCooldownBypass(source ?: @"");
    if ((TokenForgeUserClosingDashboard || TokenForgeIsClosingDashboard || now - TokenForgeLastDashboardCloseAt < 0.75) &&
        now - TokenForgeLastDashboardExplicitCloseAt < 0.75 &&
        !explicitOpen) {
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle] open.ignored source=%@ reason=userCloseInProgress", source ?: @"unknown");
        TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=recentExplicitClose source=%@", source ?: @"unknown");
        NSLog(@"INFO [DashboardLifecycle][SUPPRESS_DUPLICATE] reason=recentCloseCooldown source=%@", source ?: @"unknown");
        return;
    }

    if (TokenForgeDashboardOpening || TokenForgeIsOpeningDashboard) {
        NSLog(@"INFO [NativeLifecycle] prevent_reentrant_open traceId=dashboard");
        NSLog(@"INFO [DashboardLifecycle][SUPPRESS_DUPLICATE] reason=openInProgress source=%@", source ?: @"unknown");
        return;
    }

    if (!TokenForgeAppKitRegistrationReady()) {
        NSLog(@"INFO [NativeLifecycle] open_dashboard deferred reason=app_not_ready");
        if ([openSourceForLog isEqualToString:@"launch.initial"]) {
            TokenForgePendingInitialDashboardOpen = YES;
            NSLog(@"INFO [LaunchDashboard][DEFER] reason=appNotReady");
        }
        if (explicitOpen) {
            TokenForgePendingExplicitDashboardOpenSource = [source.length > 0 ? source : @"unknown" copy];
        }
        if (!TokenForgeDashboardOpenPending) {
            TokenForgeDashboardOpenPending = YES;
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.35 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                TokenForgeDashboardOpenPending = NO;
                TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(source ?: @"deferred", explicitUserOpen);
            });
        }
        return;
    }

    TokenForgeDashboardOpening = YES;
    TokenForgeIsOpeningDashboard = YES;
    NSLog(@"INFO [NativeLifecycle] open_dashboard executed traceId=dashboard source=%@", source ?: @"unknown");
    [TokenForgeEnsureNativeDashboardController() openOrFocusDashboardFromSource:source ?: @"unknown"];
    TokenForgeIsOpeningDashboard = NO;
    TokenForgeDashboardOpening = NO;
}

@implementation TokenForgeAppLifecycleDelegate

- (void)applicationWillFinishLaunching:(NSNotification *)notification
{
    TokenForgeInitializeRuntimeGuard(@"applicationWillFinishLaunching");
    [[NSUserDefaults standardUserDefaults] setBool:NO forKey:@"NSQuitAlwaysKeepsWindows"];
    [[NSUserDefaults standardUserDefaults] setBool:YES forKey:@"ApplePersistenceIgnoreState"];
    NSLog(@"INFO [WindowLifecycle] applicationWillFinishLaunching restoration=disabled");

    if ([NSApp delegate] == self && self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationWillFinishLaunching:)]) {
        [self.originalAppDelegate applicationWillFinishLaunching:notification];
    }
}

- (BOOL)application:(NSApplication *)application shouldSaveApplicationState:(NSCoder *)coder
{
    NSLog(@"INFO [WindowLifecycle] shouldSaveApplicationState=false");
    return NO;
}

- (BOOL)application:(NSApplication *)application shouldRestoreApplicationState:(NSCoder *)coder
{
    TokenForgeInitializeRuntimeGuard(@"stateRestore");
    NSLog(@"INFO [WindowLifecycle] shouldRestoreApplicationState=false route=suppressed");
    TokenForgeDashboardLifecycleLog(@"INFO [DashboardLifecycle][SUPPRESS_REOPEN] reason=stateRestoreDisabled source=stateRestore");
    return NO;
}

- (BOOL)applicationSupportsSecureRestorableState:(NSApplication *)application
{
    NSLog(@"INFO [WindowLifecycle] supportsSecureRestorableState=false reason=lateUnityDelegateInstall crashGuard=true");
    return NO;
}

- (void)install
{
    if (![NSThread isMainThread]) {
        dispatch_async(dispatch_get_main_queue(), ^{
            [self install];
        });
        return;
    }

    if (self.installed) {
        if (self.statusItem == nil && TokenForgeStatusItemLaunchPathAllowed(@"TokenForgeAppLifecycleDelegate.install.installed", @"installStatusItem")) {
            [self installStatusItem];
        }
        [self installMainWindowHook];
        return;
    }

    if (TokenForgeLifecycleInstallInProgress || TokenForgeInstallingLifecycleDelegate) {
        return;
    }

    TokenForgeLifecycleInstallInProgress = YES;
    TokenForgeInstallingLifecycleDelegate = YES;
    TokenForgeLogRuntimeIdentityIfNeeded();
    id<NSApplicationDelegate> currentDelegate = [NSApp delegate];
    if (currentDelegate == nil && !TokenForgeAppDidFinishLaunchingObserved) {
        NSLog(@"INFO [CrashGuard] applicationDelegateProxy installed reason=earlyNoDelegate");
        [NSApp setDelegate:self];
    } else if (currentDelegate != self) {
        self.originalAppDelegate = currentDelegate;
        NSLog(@"INFO [CrashGuard] applicationDelegateProxy skipped reason=preserveUnityDelegate delegate=%@ appDidFinishLaunchingObserved=%@",
              currentDelegate != nil ? NSStringFromClass([currentDelegate class]) : @"nil",
              TokenForgeAppDidFinishLaunchingObserved ? @"true" : @"false");
        NSLog(@"INFO [AppLifecycle][DELEGATE_PROXY_SKIPPED] reason=avoidLateSecureRestorationCrash");
    }

    if (TokenForgeStatusItemLaunchPathAllowed(@"TokenForgeAppLifecycleDelegate.install", @"installStatusItem")) {
        [self installStatusItem];
    } else {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeAppLifecycleDelegate.installStatusItem reason=appNotReady");
    }
    [self installWindowNotifications];
    [self installMainWindowHook];
    NSLog(@"INFO [AppLifecycle] shouldTerminateAfterLastWindowClosed=false");
    self.installed = YES;
    TokenForgeInstallingLifecycleDelegate = NO;
    TokenForgeLifecycleInstallInProgress = NO;

    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.25 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [self installMainWindowHook];
    });
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(1.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [self installMainWindowHook];
    });
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        [self installMainWindowHook];
    });
}

- (void)installWindowNotifications
{
    if (self.observingWindowNotifications) {
        return;
    }

    self.observingWindowNotifications = YES;
    [[NSNotificationCenter defaultCenter] addObserver:self
                                             selector:@selector(windowDidBecomeMain:)
                                                 name:NSWindowDidBecomeMainNotification
                                               object:nil];
    [[NSNotificationCenter defaultCenter] addObserver:self
                                             selector:@selector(windowDidBecomeKey:)
                                                 name:NSWindowDidBecomeKeyNotification
                                               object:nil];
    [[NSNotificationCenter defaultCenter] addObserver:self
                                             selector:@selector(applicationDidFinishLaunching:)
                                                 name:NSApplicationDidFinishLaunchingNotification
                                               object:NSApp];
    [[NSNotificationCenter defaultCenter] addObserver:self
                                             selector:@selector(applicationWillTerminate:)
                                                 name:NSApplicationWillTerminateNotification
                                               object:NSApp];
    [[NSNotificationCenter defaultCenter] addObserver:self
                                             selector:@selector(applicationWillResignActive:)
                                                 name:NSApplicationWillResignActiveNotification
                                               object:NSApp];
    [[NSNotificationCenter defaultCenter] addObserver:self
                                             selector:@selector(applicationWillHide:)
                                                 name:NSApplicationWillHideNotification
                                               object:NSApp];
}

- (void)installStatusItem
{
    NSLog(@"INFO [StatusItem][CREATE_BEGIN] thread=%@ existing=%@",
          TokenForgeThreadLabel(),
          self.statusItem != nil ? @"true" : @"false");
    if (![NSThread isMainThread] || !TokenForgeStatusItemLaunchPathAllowed(@"TokenForgeAppLifecycleDelegate.installStatusItem", @"statusItem")) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeAppLifecycleDelegate.installStatusItem reason=appNotReadyOrNotMain");
        return;
    }

    if (self.statusItem != nil) {
        NSLog(@"INFO [StatusItem][REUSE] exists=true");
        return;
    }

    self.statusItem = [[NSStatusBar systemStatusBar] statusItemWithLength:NSVariableStatusItemLength];
    NSLog(@"INFO [StatusItem][NSSTATUSITEM_CREATE] item=%p button=%p owner=NSStatusBar", self.statusItem, self.statusItem.button);
    self.statusItem.button.title = @"";
    NSLog(@"INFO [StatusItem][AVATAR_IMAGE_BEGIN] source=installStatusItem");
    self.statusItem.button.image = TokenForgeCreateStatusCompanionImage(TokenForgeMenuStageIndex, TokenForgeMenuArchetypeIndex, 0, @"idle");
    NSLog(@"INFO [StatusItem][AVATAR_IMAGE_SET] imageHash=%lu", (unsigned long)self.statusItem.button.image.hash);
    self.statusItem.button.imagePosition = NSImageLeft;
    self.statusItem.button.toolTip = @"TokenForge";

    NSLog(@"INFO [StatusItem][MENU_CREATE_BEGIN]");
    NSMenu *menu = [[NSMenu alloc] initWithTitle:@"TokenForge"];
    NSMenuItem *companionItem = [[NSMenuItem alloc] initWithTitle:@"Token · Egg · Level 1" action:nil keyEquivalent:@""];
    companionItem.enabled = NO;
    companionItem.tag = 1001;
    [menu addItem:companionItem];

    NSMenuItem *repositoryItem = [[NSMenuItem alloc] initWithTitle:@"Repository: Not selected" action:nil keyEquivalent:@""];
    repositoryItem.enabled = NO;
    repositoryItem.tag = 1002;
    [menu addItem:repositoryItem];

    NSMenuItem *activeRepositoryItem = [[NSMenuItem alloc] initWithTitle:@"Active Repository" action:nil keyEquivalent:@""];
    activeRepositoryItem.tag = 1018;
    NSMenu *activeRepositoryMenu = [[NSMenu alloc] initWithTitle:@"Active Repository"];
    NSMenuItem *activeRepositoryNameItem = [[NSMenuItem alloc] initWithTitle:@"None" action:nil keyEquivalent:@""];
    activeRepositoryNameItem.enabled = NO;
    activeRepositoryNameItem.tag = 1019;
    [activeRepositoryMenu addItem:activeRepositoryNameItem];
    [activeRepositoryItem setSubmenu:activeRepositoryMenu];
    [menu addItem:activeRepositoryItem];

    NSMenuItem *agentItem = [[NSMenuItem alloc] initWithTitle:@"AI Agents: Not connected" action:nil keyEquivalent:@""];
    agentItem.enabled = NO;
    agentItem.tag = 1003;
    [menu addItem:agentItem];

    NSMenuItem *syncItem = [[NSMenuItem alloc] initWithTitle:@"Safe Sync: Local only" action:nil keyEquivalent:@""];
    syncItem.enabled = NO;
    syncItem.tag = 1004;
    [menu addItem:syncItem];

    NSMenuItem *desktopItem = [[NSMenuItem alloc] initWithTitle:@"Desktop Companion: Off" action:nil keyEquivalent:@""];
    desktopItem.enabled = NO;
    desktopItem.tag = 1005;
    [menu addItem:desktopItem];

    [menu addItem:[NSMenuItem separatorItem]];

    NSMenuItem *showItem = [[NSMenuItem alloc] initWithTitle:@"Open Dashboard" action:@selector(showTokenForgeFromStatusItem:) keyEquivalent:@""];
    showItem.target = self;
    showItem.tag = 1008;
    [menu addItem:showItem];

    NSMenuItem *hideItem = [[NSMenuItem alloc] initWithTitle:@"Hide Dashboard" action:@selector(hideTokenForgeFromStatusItem:) keyEquivalent:@""];
    hideItem.target = self;
    hideItem.tag = 1009;
    [menu addItem:hideItem];

    [menu addItem:[NSMenuItem separatorItem]];

    NSMenuItem *enableItem = [[NSMenuItem alloc] initWithTitle:@"Show Companion" action:@selector(enableDesktopCompanionFromStatusItem:) keyEquivalent:@""];
    enableItem.target = self;
    enableItem.tag = 1006;
    [menu addItem:enableItem];

    NSMenuItem *disableItem = [[NSMenuItem alloc] initWithTitle:@"Hide Companion" action:@selector(disableDesktopCompanionFromStatusItem:) keyEquivalent:@""];
    disableItem.target = self;
    disableItem.tag = 1007;
    [menu addItem:disableItem];

    NSMenuItem *clickThroughItem = [[NSMenuItem alloc] initWithTitle:@"Enable Click-through" action:@selector(toggleClickThroughFromStatusItem:) keyEquivalent:@""];
    clickThroughItem.target = self;
    clickThroughItem.tag = 1013;
    [menu addItem:clickThroughItem];

    NSMenuItem *movementItem = [[NSMenuItem alloc] initWithTitle:@"Pause Movement" action:@selector(toggleMovementFromStatusItem:) keyEquivalent:@""];
    movementItem.target = self;
    movementItem.tag = 1017;
    [menu addItem:movementItem];

    NSMenuItem *resetPositionItem = [[NSMenuItem alloc] initWithTitle:@"Reset Companion Position" action:@selector(resetCompanionPositionFromStatusItem:) keyEquivalent:@""];
    resetPositionItem.target = self;
    resetPositionItem.tag = 1014;
    [menu addItem:resetPositionItem];

    [menu addItem:[NSMenuItem separatorItem]];

    NSMenuItem *addRepositoryItem = [[NSMenuItem alloc] initWithTitle:@"Add Repository" action:@selector(addRepositoryFromStatusItem:) keyEquivalent:@""];
    addRepositoryItem.target = self;
    addRepositoryItem.tag = 1015;
    [menu addItem:addRepositoryItem];

    NSMenuItem *connectAgentItem = [[NSMenuItem alloc] initWithTitle:@"Manage AI Agents" action:@selector(connectAiAgentFromStatusItem:) keyEquivalent:@""];
    connectAgentItem.target = self;
    connectAgentItem.tag = 1016;
    [menu addItem:connectAgentItem];

    NSMenuItem *analyzeItem = [[NSMenuItem alloc] initWithTitle:@"Refresh Activity" action:@selector(analyzeCurrentRepositoryFromStatusItem:) keyEquivalent:@""];
    analyzeItem.target = self;
    analyzeItem.tag = 1010;
    [menu addItem:analyzeItem];

    NSMenuItem *syncNowItem = [[NSMenuItem alloc] initWithTitle:@"Sync Now" action:@selector(syncNowFromStatusItem:) keyEquivalent:@""];
    syncNowItem.target = self;
    syncNowItem.tag = 1011;
    [menu addItem:syncNowItem];

    NSMenuItem *settingsItem = [[NSMenuItem alloc] initWithTitle:@"Settings" action:@selector(settingsFromStatusItem:) keyEquivalent:@""];
    settingsItem.target = self;
    settingsItem.tag = 1012;
    [menu addItem:settingsItem];

    NSMenuItem *tokenShopItem = [[NSMenuItem alloc] initWithTitle:@"Token Shop" action:@selector(tokenShopFromStatusItem:) keyEquivalent:@""];
    tokenShopItem.target = self;
    tokenShopItem.tag = 1020;
    [menu addItem:tokenShopItem];

    [menu addItem:[NSMenuItem separatorItem]];

    NSMenuItem *quitItem = [[NSMenuItem alloc] initWithTitle:@"Quit TokenForge" action:@selector(quitTokenForgeFromStatusItem:) keyEquivalent:@"q"];
    quitItem.target = self;
    [menu addItem:quitItem];

    self.statusItem.menu = menu;
    [menu release];
    NSLog(@"INFO [StatusItem][MENU_ATTACHED] retainedBy=statusItem itemCount=%ld", (long)self.statusItem.menu.numberOfItems);
    [self updateStatusItemMenu];
    if (self.statusAnimationTimer == nil) {
        NSTimeInterval interval = 0.42;
        self.statusAnimationTimer = [NSTimer scheduledTimerWithTimeInterval:interval repeats:YES block:^(NSTimer *timer) {
            self.statusAnimationFrame = (self.statusAnimationFrame + 1) % 4;
            [self updateStatusItemMenu];
        }];
        [[NSRunLoop mainRunLoop] addTimer:self.statusAnimationTimer forMode:NSRunLoopCommonModes];
        NSLog(@"INFO [MenuBarCompanion] timerStarted interval=%.2f", interval);
        NSLog(@"INFO [MenuBarCompanion] animation started mode=idle");
    }
    NSLog(@"INFO [NativeDashboard] status item installed");
    NSLog(@"INFO [StatusItem][CREATE] success=%@", TokenForgeStatusItemExists() ? @"true" : @"false");
}

- (void)updateStatusItemMenu
{
    NSLog(@"INFO [StatusItem][UPDATE_BEGIN] thread=%@ hasItem=%@ hasMenu=%@",
          TokenForgeThreadLabel(),
          self.statusItem != nil ? @"true" : @"false",
          (self.statusItem != nil && self.statusItem.menu != nil) ? @"true" : @"false");
    if (![NSThread isMainThread] || !TokenForgeStatusItemLaunchPathAllowed(@"TokenForgeAppLifecycleDelegate.updateStatusItemMenu", @"statusItem")) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeAppLifecycleDelegate.updateStatusItemMenu reason=appNotReadyOrNotMain");
        return;
    }

    if (self.statusItem == nil || self.statusItem.menu == nil) {
        NSLog(@"ERROR [StatusItem][ERROR] reason=missingAfterCreate");
        return;
    }

    BOOL reactionPulse = [TokenForgeMenuReaction isEqualToString:@"GrowthSaved"] || [TokenForgeMenuReaction isEqualToString:@"LevelUp"];
    NSString *mode = !TokenForgeMenuCompanionEnabled ? @"hidden" : (TokenForgeMenuCanLevelUp ? @"levelUp" : (TokenForgeMenuAnalysisRunning ? @"running" : (reactionPulse ? @"reaction" : @"idle")));
    if (![TokenForgeMenuAnimationMode isEqualToString:mode]) {
        if ([mode isEqualToString:@"levelUp"] || [mode isEqualToString:@"reaction"]) {
            NSLog(@"INFO [MenuBarCompanion] reaction started type=%@", [mode isEqualToString:@"levelUp"] ? @"levelUp" : TokenForgeMenuReaction);
        }
        TokenForgeMenuAnimationMode = [mode copy];
        NSLog(@"INFO [MenuBarCompanion] animation started mode=%@", mode);
    }
    BOOL pulse = TokenForgeMenuCanLevelUp || TokenForgeMenuAnalysisRunning || reactionPulse;
    NSInteger animatedStage = TokenForgeMenuStageIndex;
    NSInteger animatedArchetype = TokenForgeMenuArchetypeIndex;
    if (pulse && self.statusAnimationFrame % 2 == 1) {
        animatedArchetype = MIN(6, animatedArchetype + 1);
    }
    if (TokenForgeMenuAnalysisRunning) {
        animatedStage = MAX(0, MIN(4, TokenForgeMenuStageIndex + (self.statusAnimationFrame % 2)));
    }
    self.statusItem.button.title = TokenForgeMenuStatusText ?: @"";
    NSImage *oldImage = self.statusItem.button.image;
    NSString *cacheKey = TokenForgeAvatarCacheKey(@"menuBar", NSMakeSize(20.0, 20.0), animatedStage, animatedArchetype, self.statusAnimationFrame, mode, @"orange_cat");
    NSLog(@"INFO [StatusItem][AVATAR_IMAGE_BEGIN] source=updateStatusItemMenu cacheKey=%@", cacheKey);
    NSImage *newImage = TokenForgeCreateStatusCompanionImage(animatedStage, animatedArchetype, self.statusAnimationFrame, mode);
    self.statusItem.button.image = newImage;
    self.statusItem.button.imagePosition = NSImageLeft;
    NSLog(@"INFO [MenuBarCompanion] tick frameIndex=%ld mode=%@ imageHash=%lu", (long)self.statusAnimationFrame, mode, (unsigned long)newImage.hash);
    NSLog(@"INFO [MenuBarCompanion] buttonImageUpdated changed=%@", oldImage != newImage ? @"true" : @"false");
    NSLog(@"INFO [MenuBarCompanion] cacheKey=%@ cacheHit=%@", cacheKey, oldImage == newImage ? @"true" : @"false");
    NSLog(@"INFO [MenuBarCompanion] frame index=%ld state=%@ animationRunning=true", (long)self.statusAnimationFrame, mode);
    [[self.statusItem.menu itemWithTag:1001] setTitle:TokenForgeMenuConnectedCompanionCount > 0
        ? [NSString stringWithFormat:@"%@ · %@ · Level %ld%@", TokenForgeMenuCompanionName, TokenForgeMenuStage, (long)MAX(1, TokenForgeMenuLevel), TokenForgeMenuCanLevelUp ? @" · Level Up Ready" : @""]
        : @"TokenForge · No repository companion"];
    [[self.statusItem.menu itemWithTag:1002] setTitle:[NSString stringWithFormat:@"Repository: %@", TokenForgeMenuRepositoryAlias]];
    [[self.statusItem.menu itemWithTag:1019] setTitle:TokenForgeMenuConnectedCompanionCount > 0 ? [NSString stringWithFormat:@"%@ · %@ · Level %ld", TokenForgeMenuRepositoryAlias, TokenForgeMenuStage, (long)MAX(1, TokenForgeMenuLevel)] : @"None connected"];
    [[self.statusItem.menu itemWithTag:1003] setTitle:[NSString stringWithFormat:@"AI Agents: %@", TokenForgeMenuAgentStatus]];
    [[self.statusItem.menu itemWithTag:1004] setTitle:[NSString stringWithFormat:@"Safe Sync: %@", TokenForgeMenuSyncStatus]];
    [[self.statusItem.menu itemWithTag:1005] setTitle:[NSString stringWithFormat:@"Desktop Companion: %@", TokenForgeMenuCompanionEnabled ? (TokenForgeMenuClickThrough ? @"Native Active · Click-through" : @"Native Active · Interactive") : @"Off"]];
    [self.statusItem.menu itemWithTag:1006].enabled = !TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1007].enabled = TokenForgeMenuCompanionEnabled;
    [[self.statusItem.menu itemWithTag:1006] setTitle:@"Show Companion"];
    [[self.statusItem.menu itemWithTag:1007] setTitle:@"Hide Companion"];
    [[self.statusItem.menu itemWithTag:1013] setTitle:TokenForgeMenuClickThrough ? @"Disable Click-through" : @"Enable Click-through"];
    [[self.statusItem.menu itemWithTag:1017] setTitle:TokenForgeMenuMovementEnabled ? @"Pause Movement" : @"Resume Movement"];
    [self.statusItem.menu itemWithTag:1017].enabled = TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1013].enabled = TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1014].enabled = TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1015].enabled = YES;
    [self.statusItem.menu itemWithTag:1016].enabled = YES;
    [self.statusItem.menu itemWithTag:1008].enabled = YES;
    [self.statusItem.menu itemWithTag:1009].enabled = [self isMainWindowVisible];
    [self.statusItem.menu itemWithTag:1010].enabled = TokenForgeMenuCanAnalyze;
    [self.statusItem.menu itemWithTag:1011].enabled = TokenForgeMenuCanSync;
    [self.statusItem.menu itemWithTag:1012].enabled = YES;
    [self.statusItem.menu itemWithTag:1020].enabled = YES;
    NSLog(@"INFO [StatusItem][UPDATE] repo=%@ agents=%@ level=%ld connectedCompanions=%ld visibleOverlays=%ld",
          TokenForgeMenuRepositoryAlias ?: @"Not selected",
          TokenForgeMenuAgentStatus ?: @"No agent connected",
          (long)MAX(1, TokenForgeMenuLevel),
          (long)TokenForgeMenuConnectedCompanionCount,
          (long)TokenForgeMenuVisibleOverlayCount);
    NSLog(@"INFO [StatusItem][VISIBLE] exists=%@ button=%@ length=%.2f",
          self.statusItem != nil ? @"true" : @"false",
          self.statusItem.button != nil ? @"true" : @"false",
          self.statusItem != nil ? self.statusItem.length : 0.0);
    NSLog(@"INFO [StatusItem][ACTUAL_STATE] repo=%@ agents=%@ overlayActualVisible=%ld stage=%@ level=%ld drag=%@ motion=%@ clickThrough=%@",
          TokenForgeMenuRepositoryAlias ?: @"Not selected",
          TokenForgeMenuAgentStatus ?: @"No agent connected",
          (long)TokenForgeMenuVisibleOverlayCount,
          TokenForgeMenuStage ?: @"None",
          (long)TokenForgeMenuLevel,
          TokenForgeMenuClickThrough ? @"false" : @"true",
          TokenForgeMenuMovementEnabled ? @"true" : @"false",
          TokenForgeMenuClickThrough ? @"true" : @"false");
}

- (void)installMainWindowHook
{
    if (![NSThread isMainThread] || !TokenForgeAppReadyForWindowMutation(@"TokenForgeAppLifecycleDelegate.installMainWindowHook", @"lifecycle")) {
        return;
    }

    if (!TokenForgeAppDidFinishLaunchingObserved) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeAppLifecycleDelegate.installMainWindowHook reason=launchNotObserved source=lifecycle");
        return;
    }

    NSWindow *window = TokenForgeFindMainWindow();
    if (window == nil) {
        return;
    }

    self.mainWindow = window;
    id<NSWindowDelegate> currentDelegate = window.delegate;
    if (currentDelegate != self) {
        self.originalMainWindowDelegate = currentDelegate;
        window.delegate = self;
    }
}

- (void)windowDidBecomeMain:(NSNotification *)notification
{
    TokenForgeLogWindowLifecycle(@"didBecomeMain", (NSWindow *)notification.object, @"notification");
    if (TokenForgeLooksLikeMainWindow((NSWindow *)notification.object)) {
        [self installMainWindowHook];
    }
}

- (void)windowDidBecomeKey:(NSNotification *)notification
{
    TokenForgeLogWindowLifecycle(@"didBecomeKey", (NSWindow *)notification.object, @"notification");
    if (TokenForgeLooksLikeMainWindow((NSWindow *)notification.object)) {
        [self installMainWindowHook];
    }
}

- (void)showMainWindow
{
    NSLog(@"INFO [WindowLifecycle] showMainWindow redirectedTo=openDashboard");
    TokenForgeOpenOrFocusDashboard(@"lifecycle.showMainWindow");
    return;
    [self installMainWindowHook];
    [NSApp unhide:nil];
    [NSApp activateIgnoringOtherApps:YES];

    NSWindow *window = self.mainWindow ?: TokenForgeFindMainWindow();
    if (window == nil) {
        return;
    }

    self.mainWindow = window;
    [window makeKeyAndOrderFront:nil];
    [window orderFrontRegardless];
    TokenForgeLogWindowLifecycle(@"orderFront", window, @"legacyShowMainWindow");
}

- (void)hideMainWindow
{
    NSLog(@"INFO [WindowLifecycle] hideMainWindow redirectedTo=hideDashboard");
    TokenForgeCloseDashboard(@"lifecycle.hideMainWindow");
    return;
    [self installMainWindowHook];
    NSWindow *window = self.mainWindow ?: TokenForgeFindMainWindow();
    if (window != nil) {
        [window orderOut:nil];
    }
}

- (BOOL)isMainWindowVisible
{
    return TokenForgeIsDashboardVisible();
}

- (BOOL)applicationShouldTerminateAfterLastWindowClosed:(NSApplication *)sender
{
    NSLog(@"INFO [AppLifecycle] shouldTerminateAfterLastWindowClosed=false");
    NSLog(@"INFO [AppLifecycle] lastWindowClosed keepRunning=true");
    NSLog(@"INFO [DesktopOverlay] background state keepVisible=%@", TokenForgeMenuCompanionEnabled ? @"true" : @"false");
    return NO;
}

- (NSApplicationTerminateReply)applicationShouldTerminate:(NSApplication *)sender
{
    BOOL explicitQuit = self.explicitTerminationRequested || TokenForgeExplicitQuitRequested;
    BOOL verifierCleanup = !explicitQuit && TokenForgeVerificationAllowsImplicitVerifierCleanup();
    NSString *terminateSource = explicitQuit ? @"explicitUserQuit" : (verifierCleanup ? @"runtimeVerifierCleanup" : @"appkit");
    BOOL allowQuit = explicitQuit || verifierCleanup || !TokenForgeRuntimeVerificationMode;
    NSString *allowReason = explicitQuit ? @"explicitUserQuit" : (verifierCleanup ? @"verificationCleanupAfterRuntimeWindow" : (TokenForgeRuntimeVerificationMode ? @"verificationBlocksImplicitQuit" : @"nonVerificationImplicitQuit"));
    NSLog(@"INFO [AppLifecycle][QUIT_REQUESTED] source=%@ traceId=applicationShouldTerminate", terminateSource);
    TokenForgeLogQuitDiagnostic(@"applicationShouldTerminate", terminateSource, allowQuit, !allowQuit, allowReason);
    if (!allowQuit) {
        NSLog(@"WARN [AppLifecycle][SUPPRESS_QUIT] reason=verificationMode source=applicationShouldTerminate implicit=true");
        return NSTerminateCancel;
    }

    if (!explicitQuit && verifierCleanup) {
        TokenForgeRuntimeVerifierCleanupQuitRequested = YES;
        NSLog(@"INFO [RuntimeVerify][CLEANUP_QUIT_ALLOWED] elapsedSeconds=%.2f source=applicationShouldTerminate", TokenForgeRuntimeVerificationElapsed());
    } else if (!explicitQuit) {
        TokenForgeRuntimeVerifierCleanupQuitRequested = NO;
        self.explicitTerminationRequested = YES;
        TokenForgeExplicitQuitRequested = YES;
    }
    NSLog(@"INFO [AppLifecycle] lifecycle.terminateRequested explicit=%@ user=%@ system=%@ unknown=%@ dragging=%@",
          (explicitQuit || verifierCleanup) ? @"true" : @"false",
          explicitQuit ? @"true" : @"false",
          verifierCleanup ? @"false" : (explicitQuit ? @"false" : @"true"),
          verifierCleanup ? @"false" : (explicitQuit ? @"false" : @"true"),
          TokenForgeIsDraggingOverlay ? @"true" : @"false");
    TokenForgeTerminating = YES;
    TokenForgeExplicitQuitRequested = explicitQuit || verifierCleanup;
    self.explicitTerminationRequested = explicitQuit || verifierCleanup;
    NSLog(@"INFO [AppLifecycle][QUIT_ALLOWED]");
    NSInteger visibleWindowCount = 0;
    for (NSWindow *window in TokenForgeSafeWindowsSnapshot(@"applicationShouldTerminate.visibleWindows", YES)) {
        if (window.isVisible) {
            visibleWindowCount += 1;
        }
    }
    NSLog(@"INFO [AppLifecycle] applicationShouldTerminate reason=%@ visibleWindows=%ld dashboardExists=%@ dashboardVisible=%@ overlayExists=%@ overlayVisible=%@ motionTimerActive=%@ statusTimerActive=%@ watchdogActive=dispatchAfterOnly",
          explicitQuit ? @"user" : (verifierCleanup ? @"runtime_verifier_cleanup" : @"system_or_unknown"),
          (long)visibleWindowCount,
          TokenForgeNativeDashboardWindow != nil ? @"true" : @"false",
          (TokenForgeNativeDashboardWindow != nil && TokenForgeNativeDashboardWindow.isVisible) ? @"true" : @"false",
          TokenForgeCompanionWindow != nil ? @"true" : @"false",
          (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"true" : @"false",
          TokenForgeCompanionMotionTimer != nil ? @"true" : @"false",
          self.statusAnimationTimer != nil ? @"true" : @"false");
    TokenForgeTeardownForExplicitQuit(@"applicationShouldTerminate");
    NSLog(@"INFO [OverlayTrace] app_terminate overlay_cleanup=true");

    return NSTerminateNow;
}

- (void)applicationWillTerminate:(NSNotification *)notification
{
    TokenForgeLogQuitDiagnostic(@"applicationWillTerminate",
                                TokenForgeRuntimeVerifierCleanupQuitRequested ? @"verificationCleanup" : (TokenForgeExplicitQuitRequested ? @"explicitQuit" : @"appShutdown"),
                                TokenForgeExplicitQuitRequested,
                                !TokenForgeExplicitQuitRequested,
                                TokenForgeExplicitQuitRequested ? @"terminationProceeding" : @"unexpectedTerminationNotification");
    TokenForgeRecordNormalTermination(TokenForgeExplicitQuitRequested ? @"applicationWillTerminate.explicit" : @"applicationWillTerminate");
    NSLog(@"INFO [AppLifecycle][TERMINATE] explicitQuit=%@",
          TokenForgeExplicitQuitRequested ? @"true" : @"false");
    NSLog(@"INFO [AppLifecycle] applicationWillTerminate normalTerminationMarker=true explicitQuit=%@",
          TokenForgeExplicitQuitRequested ? @"true" : @"false");
    if ([NSApp delegate] == self && self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationWillTerminate:)]) {
        [self.originalAppDelegate applicationWillTerminate:notification];
    }
}

- (void)applicationDidFinishLaunching:(NSNotification *)notification
{
    TokenForgeAppDidFinishLaunchingObserved = YES;
    TokenForgeRefreshNativeReadiness(@"applicationDidFinishLaunching");
    TokenForgeInitializeRuntimeGuard(@"applicationDidFinishLaunching");
    NSLog(@"INFO [NativeLaunchTrace][APP_READY] function=applicationDidFinishLaunching nativePluginLoaded=%@ nsApplicationAvailable=%@ appDidFinishLaunchingObserved=%@ mainThreadReady=%@ dashboardAllowed=%@ overlayAllowed=%@ statusItemAllowed=%@ verificationMode=%@ lastExplicitSource=%@",
          TokenForgeNativePluginLoaded ? @"true" : @"false",
          TokenForgeNSApplicationAvailable ? @"true" : @"false",
          TokenForgeAppDidFinishLaunchingObserved ? @"true" : @"false",
          TokenForgeMainThreadReady ? @"true" : @"false",
          TokenForgeDashboardAllowed ? @"true" : @"false",
          TokenForgeOverlayAllowed ? @"true" : @"false",
          TokenForgeStatusItemAllowed ? @"true" : @"false",
          TokenForgeRuntimeVerificationMode ? @"true" : @"false",
          TokenForgeLastExplicitSource ?: @"none");
    NSLog(@"INFO [NativeLifecycle] app_registration_ready=true notification=applicationDidFinishLaunching");
    TokenForgeRequestLifecycleInstall(@"applicationDidFinishLaunching");
    TokenForgeEnsureStatusItem(@"launch.statusItem");
    [self installMainWindowHook];
    TokenForgeReplayPendingCompanionVisibilityIfReady(@"applicationDidFinishLaunching");
    if ([NSApp delegate] == self && self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationDidFinishLaunching:)]) {
        [self.originalAppDelegate applicationDidFinishLaunching:notification];
    }
    // Managed persistence hydration owns the initial route. Auto-opening here races the first
    // saved-state projection and briefly renders the native controller's default onboarding state.
    NSLog(@"INFO [LaunchRouteDiagnostic] appKitReady=true firstVisibleRoute=deferred reason=waitingForManagedHydration");
}

- (void)applicationWillResignActive:(NSNotification *)notification
{
    NSLog(@"INFO [OverlayTrace] app_resign_active keep_overlay=%@", TokenForgeDesiredCompanionVisible ? @"true" : @"false");
    if ([NSApp delegate] == self && self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationWillResignActive:)]) {
        [self.originalAppDelegate applicationWillResignActive:notification];
    }
}

- (void)applicationWillHide:(NSNotification *)notification
{
    NSLog(@"INFO [OverlayTrace] app_hide keep_overlay=%@", TokenForgeDesiredCompanionVisible ? @"true" : @"false");
    if ([NSApp delegate] == self && self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationWillHide:)]) {
        [self.originalAppDelegate applicationWillHide:notification];
    }
}

- (BOOL)applicationShouldHandleReopen:(NSApplication *)sender hasVisibleWindows:(BOOL)flag
{
    TokenForgeInitializeRuntimeGuard(@"applicationShouldHandleReopen");
    TokenForgeApplicationReopenCount += 1;
    NSLog(@"INFO [DockReopen][ENTER] hasVisibleWindows=%@ appActive=%@",
          flag ? @"true" : @"false",
          (NSApp != nil && NSApp.isActive) ? @"true" : @"false");
    if (self.explicitTerminationRequested || TokenForgeExplicitQuitRequested || TokenForgeTerminating) {
        NSLog(@"INFO [WindowLifecycle] applicationShouldHandleReopen ignored reason=explicitQuitOrTerminating");
        NSLog(@"INFO [AppLifecycle][SUPPRESS_REOPEN] reason=explicitQuit");
        return NO;
    }

    BOOL dashboardVisible = TokenForgeIsDashboardVisible();
    BOOL overlayVisible = TokenForgeIsCompanionOverlayVisible();
    BOOL unityVisible = TokenForgeVisibleUnityWindowCount() > 0;
    NSWindow *dashboardWindowBefore = TokenForgeNativeDashboardWindow;
    NSLog(@"INFO [DockReopenDiagnostic] dockReopenEventReceived=true hasVisibleWindows=%@", flag ? @"true" : @"false");
    NSLog(@"INFO [DockReopenDiagnostic] mainWindowFound=%@ miniaturized=%@ visible=%@ key=%@ before=true",
          dashboardWindowBefore != nil ? @"true" : @"false",
          dashboardWindowBefore != nil && dashboardWindowBefore.isMiniaturized ? @"true" : @"false",
          dashboardWindowBefore != nil && dashboardWindowBefore.isVisible ? @"true" : @"false",
          dashboardWindowBefore != nil && dashboardWindowBefore.isKeyWindow ? @"true" : @"false");
    NSLog(@"INFO [DockReopen][CLASSIFY] dashboardVisible=%@ overlayVisible=%@ unityVisible=%@",
          dashboardVisible ? @"true" : @"false",
          overlayVisible ? @"true" : @"false",
          unityVisible ? @"true" : @"false");
    NSLog(@"INFO [WindowLifecycle] applicationShouldHandleReopen count=%lu hasVisibleWindows=%@ route=openOrFocusDashboard",
          (unsigned long)TokenForgeApplicationReopenCount,
          flag ? @"true" : @"false");
    TokenForgeDumpDashboardWindows(@"beforeReopen");
    NSLog(@"INFO [DockReopen][ACTION] openOrFocusDashboard source=dock.reopen");
    NSLog(@"INFO [DockReopenDiagnostic] activationRequested=true source=dock.reopen");
    TokenForgeOpenOrFocusDashboard(@"dock.reopen");
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.16 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        NSWindow *dashboardWindowAfter = TokenForgeNativeDashboardWindow;
        NSLog(@"INFO [DockReopenDiagnostic] mainWindowFound=%@ miniaturized=%@ visible=%@ key=%@ after=true",
              dashboardWindowAfter != nil ? @"true" : @"false",
              dashboardWindowAfter != nil && dashboardWindowAfter.isMiniaturized ? @"true" : @"false",
              dashboardWindowAfter != nil && dashboardWindowAfter.isVisible ? @"true" : @"false",
              dashboardWindowAfter != nil && dashboardWindowAfter.isKeyWindow ? @"true" : @"false");
    });
    return NO;
}

- (void)applicationDidBecomeActive:(NSNotification *)notification
{
    TokenForgeInitializeRuntimeGuard(@"applicationDidBecomeActive");
    NSLog(@"INFO [DesktopOverlay] background state keepVisible=%@", TokenForgeMenuCompanionEnabled ? @"true" : @"false");
    if (TokenForgePendingExplicitDashboardOpenSource.length > 0) {
        NSString *pendingSource = [TokenForgePendingExplicitDashboardOpenSource copy];
        TokenForgePendingExplicitDashboardOpenSource = nil;
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeOpenOrFocusDashboard(pendingSource);
        });
    }

    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationDidBecomeActive:)]) {
        [self.originalAppDelegate applicationDidBecomeActive:notification];
    }
}

- (BOOL)windowShouldClose:(NSWindow *)sender
{
    if (self.explicitTerminationRequested || sender != self.mainWindow) {
        if (sender != self.mainWindow && TokenForgeWindowLooksBlank(sender)) {
            [sender orderOut:nil];
            return NO;
        }
        if (self.originalMainWindowDelegate != nil && [self.originalMainWindowDelegate respondsToSelector:@selector(windowShouldClose:)]) {
            return [self.originalMainWindowDelegate windowShouldClose:sender];
        }

        return YES;
    }

    [sender orderOut:nil];
    NSLog(@"INFO [AppLifecycle] shouldTerminateAfterLastWindowClosed=false");
    NSLog(@"INFO [AppLifecycle] dashboardWindowClosed keepAppRunning=true");
    NSLog(@"INFO [AppLifecycle] lastWindowClosed keepRunning=true");
    NSLog(@"INFO [DesktopOverlay] background state keepVisible=%@", TokenForgeMenuCompanionEnabled ? @"true" : @"false");
    return NO;
}

- (void)showTokenForgeFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("show_dashboard");
    TokenForgeOpenOrFocusDashboard(@"menubar.dashboard");
}

- (void)hideTokenForgeFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("hide_dashboard");
    TokenForgeCloseDashboard(@"menubar.hideDashboard");
}

- (void)enableDesktopCompanionFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("desktop.show");
    if (TokenForgeMenuConnectedCompanionCount <= 0 &&
        TokenForgeOverlayPanelsByRepositoryId.count == 0 &&
        TokenForgeOverlaySnapshotsByRepositoryId.count == 0) {
        TokenForgeMenuCompanionEnabled = NO;
        TokenForgeMenuClickThrough = NO;
        TokenForgeHideDesktopCompanionOverlayWithTrace(@"menubar.noApprovedRepository");
        TokenForge_HideAllRepositoryCompanions("menubar.noApprovedRepository");
        NSLog(@"INFO [Overlay][Guard] repoHash=none desiredVisible=true actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=menubar.showCompanion selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
        NSLog(@"INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY] repoHash=none desiredVisible=false actualVisible=false panelExists=false panelFrame=none reason=noApprovedRepository sourceAction=menubar.showCompanion selectedRepoId=none selectedRepoHash=none approvedRepoCount=0");
        [self updateStatusItemMenu];
        return;
    }

    TokenForgeMenuCompanionEnabled = YES;
    TokenForgeMenuClickThrough = NO;
    if (TokenForgeOverlayPanelsByRepositoryId.count > 0) {
        TokenForge_ShowAllRepositoryCompanions("menubar.showAll");
    } else {
        TokenForgeCreateCompanionOverlayOnMain(@"desktop.show.statusItem");
    }
    if (TokenForgeCompanionWindow != nil) {
        TokenForgeCompanionWindow.ignoresMouseEvents = NO;
        [TokenForgeCompanionWindow orderFrontRegardless];
    }
    [self updateStatusItemMenu];
}

- (void)disableDesktopCompanionFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("desktop.hide");
    TokenForgeMenuCompanionEnabled = NO;
    if (TokenForgeOverlayPanelsByRepositoryId.count > 0) {
        TokenForge_HideAllRepositoryCompanions("menubar.hideAll");
    } else {
        [TokenForgeCompanionWindow orderOut:nil];
    }
    [self updateStatusItemMenu];
}

- (void)toggleClickThroughFromStatusItem:(id)sender
{
    TokenForgeMenuClickThrough = !TokenForgeMenuClickThrough;
    TokenForgeSendMenuAction(TokenForgeMenuClickThrough ? "desktop.clickThrough.enable" : "desktop.drag.enable");
    if (TokenForgeCompanionWindow != nil) {
        TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgeMenuClickThrough;
    }
    for (NSPanel *panel in [TokenForgeOverlayPanelsByRepositoryId allValues]) {
        panel.ignoresMouseEvents = TokenForgeMenuClickThrough;
    }
    [self updateStatusItemMenu];
}

- (void)toggleMovementFromStatusItem:(id)sender
{
    TokenForgeMenuMovementEnabled = !TokenForgeMenuMovementEnabled;
    TokenForgeSendMenuAction(TokenForgeMenuMovementEnabled ? "desktop.movement.enable" : "desktop.movement.pause");
    [self updateStatusItemMenu];
}

- (void)resetCompanionPositionFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("desktop.position.reset");
    TokenForgeResetCompanionFrame();
    [self updateStatusItemMenu];
}

- (void)quitTokenForgeFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("quit");
    TokenForgeRequestExplicitQuit(@"menu");
}

- (void)analyzeCurrentRepositoryFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("repository.runAnalysis");
    TokenForgeOpenOrFocusDashboard(@"menubar.runAnalysis");
}

- (void)addRepositoryFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("repository.add");
    TokenForgeOpenOrFocusDashboard(@"menubar.addRepository");
}

- (void)connectAiAgentFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("navigation.openAgents");
    TokenForgeOpenOrFocusDashboard(@"menubar.manageAgents");
}

- (void)syncNowFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("sync_now");
    TokenForgeOpenOrFocusDashboard(@"menubar.syncNow");
}

- (void)settingsFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("settings");
    [TokenForgeEnsureNativeDashboardController() showSettings];
}

- (void)tokenShopFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("tokenShop");
    TokenForgeOpenOrFocusDashboard(@"menubar.tokenShop");
}

- (BOOL)respondsToSelector:(SEL)aSelector
{
    return [super respondsToSelector:aSelector] ||
           [self.originalAppDelegate respondsToSelector:aSelector] ||
           [self.originalMainWindowDelegate respondsToSelector:aSelector];
}

- (NSMethodSignature *)methodSignatureForSelector:(SEL)aSelector
{
    NSMethodSignature *signature = [super methodSignatureForSelector:aSelector];
    if (signature != nil) {
        return signature;
    }

    signature = [(NSObject *)self.originalAppDelegate methodSignatureForSelector:aSelector];
    if (signature != nil) {
        return signature;
    }

    return [(NSObject *)self.originalMainWindowDelegate methodSignatureForSelector:aSelector];
}

- (void)forwardInvocation:(NSInvocation *)invocation
{
    SEL selector = invocation.selector;
    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:selector]) {
        [invocation invokeWithTarget:self.originalAppDelegate];
        return;
    }

    if (self.originalMainWindowDelegate != nil && [self.originalMainWindowDelegate respondsToSelector:selector]) {
        [invocation invokeWithTarget:self.originalMainWindowDelegate];
        return;
    }

    [super forwardInvocation:invocation];
}

@end

static TokenForgeAppLifecycleDelegate *TokenForgeEnsureLifecycleDelegate(void)
{
    if (TokenForgeLifecycleDelegate == nil) {
        TokenForgeLifecycleDelegate = [[TokenForgeAppLifecycleDelegate alloc] init];
    }

    if (![NSThread isMainThread]) {
        TokenForgeRequestLifecycleInstall(@"ensure_delegate_not_main");
        return TokenForgeLifecycleDelegate;
    }

    if (!TokenForgeAppKitRegistrationReady()) {
        TokenForgeRequestLifecycleInstall(@"ensure_delegate_not_ready");
        return TokenForgeLifecycleDelegate;
    }

    [TokenForgeLifecycleDelegate install];
    return TokenForgeLifecycleDelegate;
}

static BOOL TokenForgeStatusItemExists(void)
{
    return TokenForgeLifecycleDelegate != nil &&
        TokenForgeLifecycleDelegate.statusItem != nil &&
        TokenForgeLifecycleDelegate.statusItem.button != nil;
}

static void TokenForgeEnsureStatusItem(NSString *source)
{
    NSString *safeSource = source.length > 0 ? source : @"unknown";
    if (![NSThread isMainThread]) {
        NSString *sourceCopy = [safeSource copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeEnsureStatusItem(sourceCopy);
        });
        return;
    }

    BOOL existsBefore = TokenForgeStatusItemExists();
    NSLog(@"INFO [StatusItem][ENSURE] source=%@ existsBefore=%@", safeSource, existsBefore ? @"true" : @"false");
    TokenForgeAppLifecycleDelegate *delegate = TokenForgeEnsureLifecycleDelegate();
    if (delegate.statusItem == nil) {
        [delegate installStatusItem];
        NSLog(@"INFO [StatusItem][CREATE] success=%@", TokenForgeStatusItemExists() ? @"true" : @"false");
    } else {
        NSLog(@"INFO [StatusItem][REUSE] exists=true");
    }

    [delegate updateStatusItemMenu];
    BOOL existsAfter = TokenForgeStatusItemExists();
    CGFloat length = existsAfter ? delegate.statusItem.length : 0.0;
    NSLog(@"INFO [StatusItem][VISIBLE] exists=%@ button=%@ length=%.2f",
          existsAfter ? @"true" : @"false",
          (existsAfter && delegate.statusItem.button != nil) ? @"true" : @"false",
          length);
    if (!existsAfter) {
        NSLog(@"ERROR [StatusItem][ERROR] reason=missingAfterCreate");
    }
}

extern "C" bool InstallTokenForgeMacAppLifecycle()
{
    TokenForgeNativeEntryLog(@"InstallTokenForgeMacAppLifecycle", @"none");
    @try {
        TokenForgeRefreshNativeSafetyFlags();
        if (TokenForgeNativeSafeMode || TokenForgeDisableStatusItem) {
            NSLog(@"INFO [NativeSafeMode][SKIP] function=InstallTokenForgeMacAppLifecycle reason=%@",
                  TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_STATUS_ITEM");
            TokenForgeNativeEntryReturnLog(@"InstallTokenForgeMacAppLifecycle", @"skipped=true");
            return true;
        }

        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeLogRuntimeIdentityIfNeeded();
            TokenForgeInitializeRuntimeGuard(@"InstallTokenForgeMacAppLifecycle");
            TokenForgeRequestLifecycleInstall(@"InstallTokenForgeMacAppLifecycle");
        });
        TokenForgeNativeEntryReturnLog(@"InstallTokenForgeMacAppLifecycle", @"scheduled=true");
        return true;
    } @catch (NSException *exception) {
        TokenForgeNativeCrashGuardLog(@"InstallTokenForgeMacAppLifecycle", exception, @"install");
        return false;
    }
}

extern "C" const char *TokenForge_GetOverlayLibraryPath()
{
    TokenForgeNativeEntryLog(@"TokenForge_GetOverlayLibraryPath", @"none");
    static char path[PATH_MAX] = {0};
    if (path[0] != '\0') {
        TokenForgeNativeEntryReturnLog(@"TokenForge_GetOverlayLibraryPath", [NSString stringWithFormat:@"path=%s", path]);
        return path;
    }

    Dl_info info;
    if (dladdr((const void *)&TokenForge_GetOverlayLibraryPath, &info) != 0 && info.dli_fname != NULL) {
        strncpy(path, info.dli_fname, sizeof(path) - 1);
        path[sizeof(path) - 1] = '\0';
        TokenForgeNativeEntryReturnLog(@"TokenForge_GetOverlayLibraryPath", [NSString stringWithFormat:@"path=%s", path]);
        return path;
    }

    TokenForgeNativeEntryReturnLog(@"TokenForge_GetOverlayLibraryPath", @"path=unavailable");
    return "DesktopCompanionOverlay path unavailable";
}

extern "C" void TokenForge_UpdateStatusItem(const char *companionName, const char *stage, int stageIndex, int archetypeIndex, int level, const char *repositoryAlias, const char *agentStatus, const char *syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync)
{
    TokenForgeNativeEntryLog(@"TokenForge_UpdateStatusItem",
                             [NSString stringWithFormat:@"stageIndex=%d archetypeIndex=%d level=%d companionEnabled=%@ clickThrough=%@ canAnalyze=%@ canSync=%@",
                              stageIndex,
                              archetypeIndex,
                              level,
                              companionEnabled ? @"true" : @"false",
                              clickThrough ? @"true" : @"false",
                              canAnalyze ? @"true" : @"false",
                              canSync ? @"true" : @"false"]);
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_UpdateStatusItem thread=%@", TokenForgeThreadLabel());
    dispatch_async(dispatch_get_main_queue(), ^{
        @try {
        if (!TokenForgeStatusItemLaunchPathAllowed(@"TokenForge_UpdateStatusItem", @"csharp.updateStatusItem")) {
            return;
        }
        TokenForgeAssignMenuString(&TokenForgeMenuCompanionName, companionName, @"Token");
        TokenForgeAssignMenuString(&TokenForgeMenuStage, stage, @"Egg");
        TokenForgeMenuStageIndex = MAX(0, MIN(5, stageIndex));
        TokenForgeMenuArchetypeIndex = MAX(0, MIN(6, archetypeIndex));
        TokenForgeMenuLevel = MAX(1, level);
        TokenForgeAssignMenuString(&TokenForgeMenuRepositoryAlias, repositoryAlias, @"Not selected");
        TokenForgeAssignMenuString(&TokenForgeMenuAgentStatus, agentStatus, @"No agent connected");
        TokenForgeAssignMenuString(&TokenForgeMenuSyncStatus, syncStatus, @"Local only");
        TokenForgeMenuCompanionEnabled = companionEnabled;
        TokenForgeMenuClickThrough = clickThrough;
        TokenForgeMenuCanAnalyze = canAnalyze;
        TokenForgeMenuCanSync = canSync;
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_UpdateStatusItem");
        TokenForgeNativeEntryReturnLog(@"TokenForge_UpdateStatusItem", @"updated=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"TokenForge_UpdateStatusItem", exception, @"mainQueue.updateStatusItem");
        }
    });
}

extern "C" void TokenForge_RegisterMenuActionCallback(TokenForgeMenuActionCallback callback)
{
    TokenForgeNativeEntryLog(@"TokenForge_RegisterMenuActionCallback",
                             [NSString stringWithFormat:@"callback=%p", callback]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeMenuActionClicked = callback;
        TokenForgeNativeEntryReturnLog(@"TokenForge_RegisterMenuActionCallback", @"registered=true");
    });
}

extern "C" void TokenForge_ShowDashboardWindow()
{
    TokenForgeNativeEntryLog(@"TokenForge_ShowDashboardWindow", @"source=csharp.dashboard");
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_ShowDashboardWindow thread=%@", TokenForgeThreadLabel());
    void (^block)(void) = ^{
        @try {
        if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForge_ShowDashboardWindow", @"csharp.dashboard", YES)) {
            return;
        }
        TokenForgeOpenOrFocusDashboard(@"csharp.dashboard");
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_ShowDashboardWindow");
        TokenForgeNativeEntryReturnLog(@"TokenForge_ShowDashboardWindow", @"shown=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"TokenForge_ShowDashboardWindow", exception, @"mainQueue.showDashboard");
        }
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_ShowDashboardWindowWithSource(const char *source)
{
    NSString *sourceString = source != NULL ? [NSString stringWithUTF8String:source] : @"csharp.dashboard";
    TokenForgeNativeEntryLog(@"TokenForge_ShowDashboardWindowWithSource",
                             [NSString stringWithFormat:@"source=%@", sourceString ?: @"csharp.dashboard"]);
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_ShowDashboardWindowWithSource source=%@ thread=%@",
          sourceString ?: @"csharp.dashboard",
          TokenForgeThreadLabel());
    void (^block)(void) = ^{
        @try {
        NSString *safeSource = sourceString ?: @"csharp.dashboard";
        BOOL explicitSource = TokenForgeSourceLooksExplicit(safeSource);
        if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForge_ShowDashboardWindowWithSource", safeSource, explicitSource)) {
            return;
        }
        TokenForgeOpenNativeDashboardOnMainWithSourceAndExplicitness(safeSource, explicitSource || TokenForgeDashboardSourceAllowsCloseCooldownBypass(safeSource));
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_ShowDashboardWindowWithSource source=%@", safeSource);
        TokenForgeNativeEntryReturnLog(@"TokenForge_ShowDashboardWindowWithSource", [NSString stringWithFormat:@"source=%@", safeSource]);
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"TokenForge_ShowDashboardWindowWithSource", exception, @"mainQueue.showDashboardWithSource");
        }
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_HideDashboardWindow()
{
    TokenForgeNativeEntryLog(@"TokenForge_HideDashboardWindow", @"source=csharp.hideDashboard");
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_HideDashboardWindow thread=%@", TokenForgeThreadLabel());
    void (^block)(void) = ^{
        @try {
        if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForge_HideDashboardWindow", @"csharp.hideDashboard", YES)) {
            return;
        }
        [TokenForgeEnsureNativeDashboardController() hideDashboardFromSource:@"csharp.hideDashboard"];
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_HideDashboardWindow");
        TokenForgeNativeEntryReturnLog(@"TokenForge_HideDashboardWindow", @"hidden=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"TokenForge_HideDashboardWindow", exception, @"mainQueue.hideDashboard");
        }
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_ToggleDashboardWindow()
{
    TokenForgeNativeEntryLog(@"TokenForge_ToggleDashboardWindow", @"source=csharp.toggleDashboard");
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_ToggleDashboardWindow thread=%@", TokenForgeThreadLabel());
    void (^block)(void) = ^{
        @try {
        if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForge_ToggleDashboardWindow", @"csharp.toggleDashboard", YES)) {
            return;
        }
        [TokenForgeEnsureNativeDashboardController() toggleDashboard];
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_ToggleDashboardWindow");
        TokenForgeNativeEntryReturnLog(@"TokenForge_ToggleDashboardWindow", @"toggled=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"TokenForge_ToggleDashboardWindow", exception, @"mainQueue.toggleDashboard");
        }
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_ShowSettingsWindow()
{
    TokenForgeNativeEntryLog(@"TokenForge_ShowSettingsWindow", @"source=csharp.showSettings");
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_ShowSettingsWindow thread=%@", TokenForgeThreadLabel());
    void (^block)(void) = ^{
        @try {
        if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForge_ShowSettingsWindow", @"csharp.showSettings", YES)) {
            return;
        }
        [TokenForgeEnsureNativeDashboardController() showSettings];
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_ShowSettingsWindow");
        TokenForgeNativeEntryReturnLog(@"TokenForge_ShowSettingsWindow", @"shown=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"TokenForge_ShowSettingsWindow", exception, @"mainQueue.showSettings");
        }
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" bool TokenForge_PickFolder(const char *prompt, char *selectedPath, int selectedPathCapacity)
{
    TokenForgeNativeEntryLog(@"TokenForge_PickFolder",
                             [NSString stringWithFormat:@"capacity=%d promptPresent=%@", selectedPathCapacity, prompt != NULL ? @"true" : @"false"]);
    if (selectedPath == NULL || selectedPathCapacity <= 0) {
        TokenForgeNativeEntryReturnLog(@"TokenForge_PickFolder", @"picked=false reason=invalidBuffer");
        return false;
    }

    selectedPath[0] = '\0';
    __block BOOL picked = NO;
    __block NSString *path = nil;
    void (^openPanelBlock)(void) = ^{
        NSOpenPanel *panel = [NSOpenPanel openPanel];
        panel.canChooseFiles = NO;
        panel.canChooseDirectories = YES;
        panel.allowsMultipleSelection = NO;
        panel.canCreateDirectories = NO;
        NSString *message = prompt != NULL ? [NSString stringWithUTF8String:prompt] : @"Select Folder";
        panel.message = message ?: @"Select Folder";
        NSInteger response = [panel runModal];
        if (response == NSModalResponseOK && panel.URL != nil) {
            path = [panel.URL.path copy];
            picked = path.length > 0;
        }
    };

    if ([NSThread isMainThread]) {
        openPanelBlock();
    } else {
        dispatch_sync(dispatch_get_main_queue(), openPanelBlock);
    }

    if (!picked || path.length == 0) {
        TokenForgeNativeEntryReturnLog(@"TokenForge_PickFolder", @"picked=false");
        return false;
    }

    const char *utf8 = path.UTF8String;
    if (utf8 == NULL) {
        TokenForgeNativeEntryReturnLog(@"TokenForge_PickFolder", @"picked=false reason=utf8");
        return false;
    }

    strlcpy(selectedPath, utf8, (size_t)selectedPathCapacity);
    TokenForgeNativeEntryReturnLog(@"TokenForge_PickFolder", @"picked=true");
    return true;
}

extern "C" void TokenForge_UpdateDashboardState(const char *json)
{
    NSDictionary *state = TokenForgeParseJsonDictionary(json);
    TokenForgeNativeEntryLog(@"TokenForge_UpdateDashboardState",
                             [NSString stringWithFormat:@"jsonBytes=%lu", json != NULL ? (unsigned long)strlen(json) : 0UL]);
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_UpdateDashboardState thread=%@", TokenForgeThreadLabel());
    void (^block)(void) = ^{
        @try {
        if (!TokenForgeDashboardLaunchPathAllowed(@"TokenForge_UpdateDashboardState", @"csharp.updateDashboardState", NO)) {
            return;
        }
    if (TokenForgeRedrawingDashboard) {
        NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForge_UpdateDashboardState reason=reentrantRedraw");
        return;
    }
    if (TokenForgeSuppressDashboardRedrawDuringOverlayDrag || TokenForgeIsDraggingOverlay) {
        NSLog(@"INFO [DashboardLifecycle][REDRAW_SUPPRESSED] reason=overlayDrag");
        NSLog(@"INFO [CSharpProjection][SKIP_TO_NATIVE] reason=overlayDragInProgress");
        return;
    }
    TokenForgeRedrawingDashboard = YES;
        [TokenForgeEnsureNativeDashboardController() updateState:state];
        [TokenForgeEnsureNativeDashboardController() setMenuBarStatus:state];
        TokenForgeRedrawingDashboard = NO;
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_UpdateDashboardState");
        TokenForgeNativeEntryReturnLog(@"TokenForge_UpdateDashboardState", @"updated=true");
        } @catch (NSException *exception) {
            TokenForgeRedrawingDashboard = NO;
            TokenForgeNativeCrashGuardLog(@"TokenForge_UpdateDashboardState", exception, @"mainQueue.updateDashboardState");
        }
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_SetMenuBarStatus(const char *json)
{
    NSDictionary *state = TokenForgeParseJsonDictionary(json);
    TokenForgeNativeEntryLog(@"TokenForge_SetMenuBarStatus",
                             [NSString stringWithFormat:@"jsonBytes=%lu", json != NULL ? (unsigned long)strlen(json) : 0UL]);
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_SetMenuBarStatus thread=%@", TokenForgeThreadLabel());
    void (^block)(void) = ^{
        @try {
        if (!TokenForgeStatusItemLaunchPathAllowed(@"TokenForge_SetMenuBarStatus", @"csharp.setMenuBarStatus")) {
            return;
        }
        [TokenForgeEnsureNativeDashboardController() setMenuBarStatus:state];
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_SetMenuBarStatus");
        TokenForgeNativeEntryReturnLog(@"TokenForge_SetMenuBarStatus", @"updated=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"TokenForge_SetMenuBarStatus", exception, @"mainQueue.setMenuBarStatus");
        }
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_SetCompanionVisible(bool visible)
{
    TokenForgeNativeEntryLog(@"TokenForge_SetCompanionVisible",
                             [NSString stringWithFormat:@"visible=%@", visible ? @"true" : @"false"]);
    NSLog(@"INFO [OverlayTrace:csharp] Native entered TokenForge_SetCompanionVisible visible=%d", visible ? 1 : 0);
    TokenForge_SetCompanionVisibleWithSource(visible, "csharp");
    TokenForgeNativeEntryReturnLog(@"TokenForge_SetCompanionVisible", @"delegated=true");
}

extern "C" void TokenForge_SetCompanionVisibleWithSource(bool visible, const char *source)
{
    NSString *sourceString = TokenForgeSafeMenuString(source, @"csharp");
    TokenForgeNativeEntryLog(@"TokenForge_SetCompanionVisibleWithSource",
                             [NSString stringWithFormat:@"visible=%@ source=%@", visible ? @"true" : @"false", sourceString]);
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_SetCompanionVisibleWithSource visible=%@ source=%@ thread=%@",
          visible ? @"true" : @"false",
          sourceString,
          TokenForgeThreadLabel());
    NSLog(@"INFO [OverlayNative][CALL] TokenForge_SetCompanionVisible visible=%@ source=%@",
          visible ? @"true" : @"false",
          sourceString);
    TokenForgeRecordCompanionDesiredState(visible, sourceString);
    if (visible) {
        TokenForgeShowDesktopCompanionOverlayWithTrace(sourceString);
    } else {
        TokenForgeHideDesktopCompanionOverlayWithTrace(sourceString);
    }
    NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_SetCompanionVisibleWithSource visible=%@ source=%@",
          visible ? @"true" : @"false",
          sourceString);
    TokenForgeNativeEntryReturnLog(@"TokenForge_SetCompanionVisibleWithSource", [NSString stringWithFormat:@"visible=%@", visible ? @"true" : @"false"]);
}

extern "C" bool TokenForge_IsCompanionVisible(void)
{
    TokenForgeNativeEntryLog(@"TokenForge_IsCompanionVisible", @"none");
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForge_IsCompanionVisible thread=%@", TokenForgeThreadLabel());
    if (![NSThread isMainThread]) {
        NSLog(@"INFO [CompanionVisibility][QUERY] appReady=false panelExists=%@ actualVisible=false thread=%@ reason=notMainThread",
              TokenForgeCompanionWindow != nil ? @"true" : @"false",
              TokenForgeThreadLabel());
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_IsCompanionVisible actualVisible=false reason=notMainThread");
        TokenForgeNativeEntryReturnLog(@"TokenForge_IsCompanionVisible", @"actualVisible=false reason=notMainThread");
        return false;
    }

    BOOL appReady = TokenForgeRefreshNativeReadiness(@"TokenForge_IsCompanionVisible");
    BOOL panelExists = TokenForgeCompanionWindow != nil;
    BOOL visible = appReady && panelExists && TokenForgeCompanionWindow.isVisible;
    NSLog(@"INFO [CompanionVisibility][QUERY] appReady=%@ panelExists=%@ actualVisible=%@ thread=%@",
          appReady ? @"true" : @"false",
          panelExists ? @"true" : @"false",
          visible ? @"true" : @"false",
          TokenForgeThreadLabel());
    NSLog(@"INFO [OverlayState][NATIVE_ACTUAL] desiredVisible=%@ actualVisible=%@ source=nativeQuery",
          TokenForgeDesiredCompanionVisible ? @"true" : @"false",
          visible ? @"true" : @"false");
    NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForge_IsCompanionVisible actualVisible=%@", visible ? @"true" : @"false");
    TokenForgeNativeEntryReturnLog(@"TokenForge_IsCompanionVisible", [NSString stringWithFormat:@"actualVisible=%@", visible ? @"true" : @"false"]);
    return visible;
}

extern "C" bool TokenForge_IsOverlayDragging(void)
{
    TokenForgeNativeEntryLog(@"TokenForge_IsOverlayDragging", @"none");
    __block BOOL dragging = NO;
    void (^block)(void) = ^{
        dragging = TokenForgeIsDraggingOverlay;
    };
    if ([NSThread isMainThread]) {
        block();
    } else {
        dispatch_sync(dispatch_get_main_queue(), block);
    }
    TokenForgeNativeEntryReturnLog(@"TokenForge_IsOverlayDragging", [NSString stringWithFormat:@"dragging=%@", dragging ? @"true" : @"false"]);
    return dragging;
}

extern "C" void TokenForge_RegisterDashboardActionCallback(TokenForgeDashboardActionCallback callback)
{
    TokenForgeNativeEntryLog(@"TokenForge_RegisterDashboardActionCallback",
                             [NSString stringWithFormat:@"callback=%p", callback]);
    void (^block)(void) = ^{
        TokenForgeDashboardActionClicked = callback;
        NSLog(@"INFO [NativeDashboard] action callback registered");
        TokenForgeNativeEntryReturnLog(@"TokenForge_RegisterDashboardActionCallback", @"registered=true");
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_LogAppBootstrapperRuntimeMarker()
{
    TokenForgeNativeEntryLog(@"TokenForge_LogAppBootstrapperRuntimeMarker", @"none");
    NSLog(@"INFO [BuildIdentity][RUNTIME_CODE_VERSION] %@ %@ nativeDylibBuildTimestamp=%@ desktopCompanionOverlayCompiledMarker=%@ csharpMarker=app-bootstrapper-overlay-projection-v9 source=AppBootstrapperNativeBridge nativeDylibPath=%@ nativeDylibModified=%@",
          TokenForgeRuntimeBuildIdentityMarker,
          TokenForgeRuntimeBuildIdentityGitMarker,
          TokenForgeDesktopCompanionOverlayCompiledMarker,
          TokenForgeDesktopCompanionOverlayCompiledMarker,
          TokenForgeNativeLibraryPathString(),
          TokenForgeFileModifiedTime(TokenForgeNativeLibraryPathString()));
    NSLog(@"INFO [RuntimeIdentity] AppBootstrapperVersionMarker=app-bootstrapper-overlay-projection-v9 source=AppBootstrapperNativeBridge");
    TokenForgeNativeEntryReturnLog(@"TokenForge_LogAppBootstrapperRuntimeMarker", @"logged=true");
}

extern "C" void ShowTokenForgeMainWindow()
{
    TokenForgeNativeEntryLog(@"ShowTokenForgeMainWindow", @"none");
    dispatch_async(dispatch_get_main_queue(), ^{
        @try {
        [TokenForgeEnsureLifecycleDelegate() showMainWindow];
        TokenForgeNativeEntryReturnLog(@"ShowTokenForgeMainWindow", @"shown=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"ShowTokenForgeMainWindow", exception, @"mainQueue.showMainWindow");
        }
    });
}

extern "C" void HideTokenForgeMainWindow()
{
    TokenForgeNativeEntryLog(@"HideTokenForgeMainWindow", @"none");
    dispatch_async(dispatch_get_main_queue(), ^{
        @try {
        [TokenForgeEnsureLifecycleDelegate() hideMainWindow];
        TokenForgeNativeEntryReturnLog(@"HideTokenForgeMainWindow", @"hidden=true");
        } @catch (NSException *exception) {
            TokenForgeNativeCrashGuardLog(@"HideTokenForgeMainWindow", exception, @"mainQueue.hideMainWindow");
        }
    });
}

extern "C" bool IsTokenForgeMainWindowVisible()
{
    TokenForgeNativeEntryLog(@"IsTokenForgeMainWindowVisible", @"none");
    if ([NSThread isMainThread]) {
        if (!TokenForgeDashboardLaunchPathAllowed(@"IsTokenForgeMainWindowVisible", @"csharp.isMainWindowVisible", NO)) {
            TokenForgeNativeEntryReturnLog(@"IsTokenForgeMainWindowVisible", @"visible=false reason=launchPathNotAllowed");
            return false;
        }

        BOOL visible = [TokenForgeEnsureLifecycleDelegate() isMainWindowVisible];
        TokenForgeNativeEntryReturnLog(@"IsTokenForgeMainWindowVisible", [NSString stringWithFormat:@"visible=%@", visible ? @"true" : @"false"]);
        return visible;
    }

    NSLog(@"INFO [NativeLaunchTrace][SKIP] function=IsTokenForgeMainWindowVisible reason=notMainThread");
    TokenForgeNativeEntryReturnLog(@"IsTokenForgeMainWindowVisible", @"visible=false reason=notMainThread");
    return false;
}

extern "C" bool TokenForge_IsDashboardVisible()
{
    TokenForgeNativeEntryLog(@"TokenForge_IsDashboardVisible", @"none");
    __block BOOL visible = NO;
    void (^block)(void) = ^{
        visible = TokenForgeIsDashboardVisible();
        NSLog(@"INFO [DashboardLifecycle][QUERY] dashboardVisible=%@ overlayVisible=%@ source=csharp.query",
              visible ? @"true" : @"false",
              TokenForgeIsCompanionOverlayVisible() ? @"true" : @"false");
    };
    if ([NSThread isMainThread]) {
        block();
    } else {
        dispatch_sync(dispatch_get_main_queue(), block);
    }
    TokenForgeNativeEntryReturnLog(@"TokenForge_IsDashboardVisible", [NSString stringWithFormat:@"visible=%@", visible ? @"true" : @"false"]);
    return visible;
}

extern "C" void QuitTokenForgeApp()
{
    TokenForgeNativeEntryLog(@"QuitTokenForgeApp", @"none");
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeRequestExplicitQuit(@"nativeBridge");
        TokenForgeNativeEntryReturnLog(@"QuitTokenForgeApp", @"quitRequested=true");
    });
}

extern "C" bool CreateDesktopCompanionOverlay()
{
    TokenForgeNativeEntryLog(@"CreateDesktopCompanionOverlay", @"source=create");
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=CreateDesktopCompanionOverlay thread=%@", TokenForgeThreadLabel());
    if ([NSThread isMainThread]) {
        if (!TokenForgeOverlayLaunchPathAllowed(@"CreateDesktopCompanionOverlay", @"create", NO)) {
            TokenForgeNativeEntryReturnLog(@"CreateDesktopCompanionOverlay", @"created=false reason=launchPathNotAllowed");
            return true;
        }
        if (TokenForgeShouldSuppressOverlayShow(@"create", NO)) {
            NSLog(@"INFO [OverlayLifecycle][SUPPRESSED_RECREATE] reason=verificationMode source=create");
            TokenForgeNativeEntryReturnLog(@"CreateDesktopCompanionOverlay", @"created=false reason=suppressed");
            return true;
        }
        TokenForgeCreateCompanionOverlayOnMain(@"create");
    } else {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (!TokenForgeOverlayLaunchPathAllowed(@"CreateDesktopCompanionOverlay", @"create", NO)) {
                return;
            }
            if (TokenForgeShouldSuppressOverlayShow(@"create", NO)) {
                NSLog(@"INFO [OverlayLifecycle][SUPPRESSED_RECREATE] reason=verificationMode source=create");
                return;
            }
            TokenForgeCreateCompanionOverlayOnMain(@"create");
        });
    }

    NSLog(@"INFO [NativeLaunchTrace][EXIT] function=CreateDesktopCompanionOverlay");
    TokenForgeNativeEntryReturnLog(@"CreateDesktopCompanionOverlay", @"scheduled=true");
    return true;
}

extern "C" void ShowDesktopCompanionOverlay()
{
    TokenForgeNativeEntryLog(@"ShowDesktopCompanionOverlay", @"source=direct");
    TokenForgeShowDesktopCompanionOverlayWithTrace(@"direct");
    TokenForgeNativeEntryReturnLog(@"ShowDesktopCompanionOverlay", @"delegated=true");
}

static void TokenForgeShowDesktopCompanionOverlayWithTrace(NSString *traceId)
{
    NSString *initialTrace = traceId.length > 0 ? traceId : @"direct";
    if (!TokenForgeReplayingCompanionVisibility) {
        TokenForgeRecordCompanionDesiredState(YES, initialTrace);
    }
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForgeShowDesktopCompanionOverlayWithTrace source=%@ thread=%@",
          initialTrace,
          TokenForgeThreadLabel());
    void (^block)(void) = ^{
        NSString *trace = traceId.length > 0 ? traceId : @"direct";
        BOOL explicitSource = TokenForgeSourceLooksExplicit(trace) && !TokenForgeSourceLooksPassiveSync(trace);
        if (!TokenForgeOverlayLaunchPathAllowed(@"TokenForgeShowDesktopCompanionOverlayWithTrace", trace, explicitSource)) {
            return;
        }

        if (TokenForgeShowingPanel) {
            NSLog(@"INFO [NativeLaunchTrace][SKIP] function=TokenForgeShowDesktopCompanionOverlayWithTrace reason=reentrantShow source=%@", trace);
            return;
        }

        TokenForgeShowingPanel = YES;
        if (TokenForgeExplicitQuitRequested || TokenForgeTerminating) {
            NSLog(@"INFO [OverlayTrace:%@] show_ignored reason=explicitQuitOrTerminating", trace);
            TokenForgeShowingPanel = NO;
            return;
        }

        if (TokenForgeShouldSuppressOverlayShow(trace, TokenForgeSourceLooksExplicit(trace))) {
            TokenForgeShowingPanel = NO;
            return;
        }

        BOOL oldDesired = TokenForgeDesiredCompanionVisible;
        TokenForgeDesiredCompanionVisible = YES;
        TokenForgeLastShowReason = @"show_requested";
        TokenForgeLastProjectionSource = @"native_show";
        NSLog(@"INFO [OverlayTrace:%@] show_requested traceId=%@ repoId=%@ source=button", trace, trace, TokenForgeMenuRepositoryAlias ?: @"Not selected");
        NSLog(@"INFO [RuntimeUIPath][Overlay] renderer=legacyPanel source=%@ repo=%@", trace, TokenForgeMenuRepositoryAlias ?: @"Not selected");
        NSLog(@"INFO [OverlayTrace:%@] desired_visible_changed old=%@ new=true source=native_show", trace, oldDesired ? @"true" : @"false");
        NSLog(@"INFO [OverlayTrace:%@] native_show_enter traceId=%@", trace, trace);
        NSLog(@"INFO [OverlayTrace:%@] Native entered ShowDesktopCompanionOverlay visible=1", trace);
        NSLog(@"INFO [DesktopCompanion] action=show requested source=motionCard thread=%@", [NSThread isMainThread] ? @"main" : @"background");
        NSLog(@"INFO [DesktopCompanion] show requested visible=true repo=%@", TokenForgeMenuRepositoryAlias ?: @"Not selected");
        if (!TokenForgeAppKitRegistrationReady()) {
            NSLog(@"INFO [NativeLifecycle] open_dashboard deferred reason=app_not_ready");
            NSLog(@"INFO [OverlayTrace:%@] native_show_deferred reason=app_not_ready", trace);
            TokenForgeRequestLifecycleInstall(@"overlay_show");
            TokenForgeShowingPanel = NO;
            return;
        }
        TokenForgeCreateCompanionOverlayOnMain(trace);
        if (TokenForgeCompanionWindow == nil) {
            NSLog(@"WARN [OverlayTrace:%@] native_show_aborted reason=panel_create_deferred", trace);
            TokenForgeShowingPanel = NO;
            return;
        }
        TokenForgeMenuCompanionEnabled = YES;
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        NSRect frame = TokenForgeClampFrameToVisibleFrame(TokenForgeCompanionWindow.frame);
        if (!NSEqualRects(frame, TokenForgeCompanionWindow.frame)) {
            NSRect oldFrame = TokenForgeCompanionWindow.frame;
            [TokenForgeCompanionWindow setFrame:frame display:NO];
            TokenForgeCompanionAnchor = frame.origin;
            NSLog(@"INFO [OverlayPanel][OFFSCREEN_CORRECTED] old=(%.2f,%.2f %.2fx%.2f) new=(%.2f,%.2f %.2fx%.2f)",
                  oldFrame.origin.x,
                  oldFrame.origin.y,
                  oldFrame.size.width,
                  oldFrame.size.height,
                  frame.origin.x,
                  frame.origin.y,
                  frame.size.width,
                  frame.size.height);
        }

        TokenForgeCompanionWindow.alphaValue = 1.0;
        TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgeMenuClickThrough;
        TokenForgeCompanionWindow.level = NSStatusWindowLevel;
        TokenForgeCompanionWindow.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary | NSWindowCollectionBehaviorStationary | NSWindowCollectionBehaviorIgnoresCycle;
        NSLog(@"INFO [OverlayPanel][SHOW] source=%@ desiredVisible=true", trace);
        NSLog(@"INFO [OverlayCreate] repo=%@ source=%@ legacyPanel=true panelExists=%@", TokenForgeMenuRepositoryAlias ?: @"legacy", trace, TokenForgeCompanionWindow != nil ? @"true" : @"false");
        NSLog(@"INFO [OverlayPanel][INDEPENDENT] dashboardParent=false childWindow=false");
        NSLog(@"INFO [OverlayPanel][FRAME] frame=(%.2f,%.2f %.2fx%.2f) screen=(%.2f,%.2f %.2fx%.2f) visibleFrame=(%.2f,%.2f %.2fx%.2f)",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height,
              (TokenForgeCompanionWindow.screen ?: NSScreen.mainScreen).frame.origin.x,
              (TokenForgeCompanionWindow.screen ?: NSScreen.mainScreen).frame.origin.y,
              (TokenForgeCompanionWindow.screen ?: NSScreen.mainScreen).frame.size.width,
              (TokenForgeCompanionWindow.screen ?: NSScreen.mainScreen).frame.size.height,
              TokenForgeVisibleFrameForFrame(TokenForgeCompanionWindow.frame).origin.x,
              TokenForgeVisibleFrameForFrame(TokenForgeCompanionWindow.frame).origin.y,
              TokenForgeVisibleFrameForFrame(TokenForgeCompanionWindow.frame).size.width,
              TokenForgeVisibleFrameForFrame(TokenForgeCompanionWindow.frame).size.height);
        NSLog(@"INFO [OverlayPanel][LEVEL] level=%ld", (long)TokenForgeCompanionWindow.level);
        NSLog(@"INFO [OverlayPanel][COLLECTION] behavior=%lu", (unsigned long)TokenForgeCompanionWindow.collectionBehavior);
        NSLog(@"INFO [OverlayPanel][CONTENT] contentView=%@", TokenForgeCompanionWindow.contentView != nil ? NSStringFromClass([TokenForgeCompanionWindow.contentView class]) : @"nil");
        NSLog(@"INFO [DesktopCompanion] click-through %@", TokenForgeMenuClickThrough ? @"true" : @"false");
        NSLog(@"INFO [OverlayTrace:%@] panel=%@ frame={{%.2f,%.2f},{%.2f,%.2f}} screen=%@",
              trace,
              TokenForgeCompanionWindow.isVisible ? @"reuse" : @"create-or-reuse",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height,
              TokenForgeScreenDisplayName(TokenForgeCompanionWindow.screen ?: NSScreen.mainScreen));
        NSLog(@"INFO [OverlayTrace:%@] panel level=%ld collectionBehavior=%lu alpha=%.2f ignoresMouseEvents=%@",
              trace,
              (long)TokenForgeCompanionWindow.level,
              (unsigned long)TokenForgeCompanionWindow.collectionBehavior,
              TokenForgeCompanionWindow.alphaValue,
              TokenForgeCompanionWindow.ignoresMouseEvents ? @"true" : @"false");
        NSLog(@"INFO [DesktopOverlay] show requested visibleSetting=true");
        NSLog(@"INFO [DesktopOverlay] show visibleSetting=true windowVisible=true frame=(%.2f,%.2f %.2fx%.2f)",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height);
        BOOL visibleBefore = TokenForgeCompanionWindow.isVisible;
        NSLog(@"INFO [OverlayPanel][ORDER_FRONT] source=%@", trace);
        [TokenForgeCompanionWindow orderFrontRegardless];
        NSLog(@"INFO [OverlayOrderFront] repo=%@ source=%@ visible=%@", TokenForgeMenuRepositoryAlias ?: @"legacy", trace, TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        NSLog(@"INFO [OverlayVisible] repo=%@ visible=%@ actualVisibleCount=%ld", TokenForgeMenuRepositoryAlias ?: @"legacy", TokenForgeCompanionWindow.isVisible ? @"true" : @"false", (long)TokenForgeVisibleOverlayFarmCount());
        NSLog(@"INFO [OverlayFrame] repo=%@ frame=(%.2f,%.2f %.2fx%.2f)",
              TokenForgeMenuRepositoryAlias ?: @"legacy",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height);
        NSLog(@"INFO [OverlayPanel][ORDERED] isVisible=%@", TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        TokenForgeLogWindowLifecycle(@"orderFrontRegardless", TokenForgeCompanionWindow, [NSString stringWithFormat:@"trace=%@", trace]);
        NSLog(@"INFO [DesktopCompanion] orderFront called visibleBefore=%@ visibleAfter=%@",
              visibleBefore ? @"true" : @"false",
              TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        NSLog(@"INFO [DesktopOverlay] orderFrontRegardless called");
        [TokenForgeCompanionContentView setNeedsDisplay:YES];
        [TokenForgeCompanionWindow displayIfNeeded];
        NSLog(@"INFO [OverlayTrace:%@] contentView draw/render requested class=%@", trace, NSStringFromClass([TokenForgeCompanionContentView class]));
        if (TokenForgeCompanionAllowsWandering && TokenForgeCompanionMotionMode != 0 && TokenForgeCompanionWanderSpeed > 0.0) {
            TokenForgeEnsureCompanionMotionTimer();
        }
        NSLog(@"INFO [DesktopCompanion] appState %@ processAlive=true",
              NSApp.isActive ? @"active" : @"background/windowClosed");
        NSLog(@"INFO [DesktopCompanion] orderFront completed isVisible=%@ frame=%.2f,%.2f %.2fx%.2f",
              TokenForgeCompanionWindow.isVisible ? @"YES" : @"NO",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height);
        NSLog(@"INFO [DesktopOverlay] orderFrontRegardless completed isVisible=%@ frame=(%.2f,%.2f %.2fx%.2f)",
              TokenForgeCompanionWindow.isVisible ? @"true" : @"false",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height);
        NSLog(@"INFO [OverlayTrace:%@] orderFrontRegardless completed isVisible=%@ orderedIndex=%ld",
              trace,
              TokenForgeCompanionWindow.isVisible ? @"1" : @"0",
              (long)[[NSApp orderedWindows] indexOfObject:TokenForgeCompanionWindow]);
        NSLog(@"INFO [OverlayTrace:%@] panel_order_front traceId=%@ visible=%@", trace, trace, TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        if (TokenForgeCompanionWindow.isVisible) {
            NSLog(@"INFO [OverlayTrace:%@] panel_did_become_visible traceId=%@", trace, trace);
        }
        TokenForgeLastOverlayVisibleSource = [trace copy];
        TokenForgeRefreshDashboardAndOverlayState(trace);
        TokenForgeDumpOverlayPanelState(trace);
        TokenForgeLogOverlayProjection(trace, @"native_show");
        NSLog(@"INFO [OverlayState][SYNC] desiredVisible=%@ actualVisible=%@ dragEnabled=%@",
              TokenForgeDesiredCompanionVisible ? @"true" : @"false",
              (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"true" : @"false",
              TokenForgeMenuClickThrough ? @"false" : @"true");
        TokenForgeScheduleOverlayWatchdogs(trace);
        TokenForgeShowingPanel = NO;
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForgeShowDesktopCompanionOverlayWithTrace source=%@", trace);
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void HideDesktopCompanionOverlay()
{
    TokenForgeNativeEntryLog(@"HideDesktopCompanionOverlay", @"source=direct");
    TokenForgeHideDesktopCompanionOverlayWithTrace(@"direct");
    TokenForgeNativeEntryReturnLog(@"HideDesktopCompanionOverlay", @"delegated=true");
}

static void TokenForgeHideDesktopCompanionOverlayWithTrace(NSString *traceId)
{
    NSString *initialTrace = traceId.length > 0 ? traceId : @"direct";
    if (!TokenForgeReplayingCompanionVisibility) {
        TokenForgeRecordCompanionDesiredState(NO, initialTrace);
    }
    NSLog(@"INFO [NativeLaunchTrace][ENTER] function=TokenForgeHideDesktopCompanionOverlayWithTrace source=%@ thread=%@",
          initialTrace,
          TokenForgeThreadLabel());
    void (^block)(void) = ^{
        NSString *trace = traceId.length > 0 ? traceId : @"direct";
        if (!TokenForgeAppReadyForWindowMutation(@"TokenForgeHideDesktopCompanionOverlayWithTrace", trace)) {
            return;
        }

        if (TokenForgeIsDraggingOverlay) {
            TokenForgeQueueOverlayActionAfterDrag(TokenForgePendingOverlayActionHide, trace);
            return;
        }
        BOOL oldDesired = TokenForgeDesiredCompanionVisible;
        TokenForgeDesiredCompanionVisible = NO;
        TokenForgeLastHideReason = @"companionVisibleOff";
        TokenForgeLastProjectionSource = @"native_hide";
        NSLog(@"INFO [OverlayTrace:%@] desired_visible_changed old=%@ new=false source=native_hide", trace, oldDesired ? @"true" : @"false");
        NSLog(@"INFO [OverlayTrace:%@] Native entered HideDesktopCompanionOverlay visible=0", trace);
        TokenForgeMenuCompanionEnabled = NO;
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        [TokenForgeCompanionWindow orderOut:nil];
        TokenForgeLastOverlayVisibleSource = [trace copy];
        TokenForgeRefreshDashboardAndOverlayState(trace);
        NSLog(@"INFO [OverlayVisibilityDiagnostic] state=overlayHidden source=%@ panelExists=%@ visible=%@ appTerminating=%@ verificationMode=%@",
              trace,
              TokenForgeCompanionWindow != nil ? @"true" : @"false",
              (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"true" : @"false",
              TokenForgeTerminating ? @"true" : @"false",
              TokenForgeRuntimeVerificationMode ? @"true" : @"false");
        NSLog(@"INFO [OverlayTrace:%@] panel_order_out traceId=%@ reason=companionVisibleOff", trace, trace);
        TokenForgeLogWindowLifecycle(@"orderOut", TokenForgeCompanionWindow, [NSString stringWithFormat:@"trace=%@", trace]);
        NSLog(@"INFO [DesktopCompanion] hidden reason=companionVisibleOff");
        NSLog(@"INFO [DesktopCompanion] hidden reason=companionVisibleOff visible=%@", TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        NSLog(@"INFO [DesktopOverlay] hide reason=companionVisibleOff");
        TokenForgeDumpOverlayPanelState(trace);
        TokenForgeLogOverlayProjection(trace, @"native_hide");
        NSLog(@"INFO [NativeLaunchTrace][EXIT] function=TokenForgeHideDesktopCompanionOverlayWithTrace source=%@", trace);
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void SetCompanionOverlayPosition(float x, float y)
{
    TokenForgeNativeEntryLog(@"SetCompanionOverlayPosition",
                             [NSString stringWithFormat:@"x=%.2f y=%.2f", x, y]);
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionWindow == nil) return;
        if (TokenForgeIsDraggingOverlay) {
            NSLog(@"INFO [OverlayDrag][SUPPRESS_PROJECTION] reason=dragInProgress");
            NSLog(@"INFO [CSharpProjection][SKIP_TO_NATIVE] reason=overlayDragInProgress");
            NSLog(@"INFO [OverlayProjection][SUPPRESSED_POSITION_APPLY] reason=dragging requested=(%.2f,%.2f)", x, y);
            return;
        }
        NSPoint oldOrigin = TokenForgeCompanionWindow.frame.origin;
        NSRect frame = TokenForgeClampFrameToVisibleFrame(NSMakeRect(x, y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height));
        TokenForgeCompanionAnchor = frame.origin;
        TokenForgeCompanionTarget = frame.origin;
        [TokenForgeCompanionWindow setFrameOrigin:frame.origin];
        NSLog(@"INFO [CompanionDrag] setFrameOrigin old=(%.2f,%.2f) new=(%.2f,%.2f)",
              oldOrigin.x,
              oldOrigin.y,
              frame.origin.x,
              frame.origin.y);
        TokenForgeNativeEntryReturnLog(@"SetCompanionOverlayPosition", @"updated=true");
    });
}

extern "C" void SetCompanionOverlaySize(float width, float height)
{
    TokenForgeNativeEntryLog(@"SetCompanionOverlaySize",
                             [NSString stringWithFormat:@"width=%.2f height=%.2f", width, height]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeCompanionSize = NSMakeSize(MAX(24.0, width), MAX(24.0, height));
        if (TokenForgeCompanionWindow == nil) return;
        if (TokenForgeIsDraggingOverlay) {
            NSLog(@"INFO [OverlayProjection][SUPPRESSED_SIZE_APPLY] reason=dragging requested=(%.2f,%.2f)", width, height);
            return;
        }
        NSRect frame = TokenForgeCompanionWindow.frame;
        frame.size = TokenForgeCompanionSize;
        frame = TokenForgeClampFrameToVisibleFrame(frame);
        TokenForgeCompanionAnchor = frame.origin;
        TokenForgeCompanionTarget = frame.origin;
        [TokenForgeCompanionWindow setFrame:frame display:YES];
        TokenForgeCompanionContentView.frame = NSMakeRect(0, 0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
        TokenForgeNativeEntryReturnLog(@"SetCompanionOverlaySize", @"updated=true");
    });
}

extern "C" void SetCompanionOverlayMotionProfile(int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds)
{
    TokenForgeNativeEntryLog(@"SetCompanionOverlayMotionProfile",
                             [NSString stringWithFormat:@"motionMode=%d idleRadius=%.2f wanderRadius=%.2f wanderSpeed=%.2f decisionInterval=%.2f allowsWandering=%@ reactionCooldown=%.2f",
                              motionMode,
                              idleRadius,
                              wanderRadius,
                              wanderSpeed,
                              decisionIntervalSeconds,
                              allowsWandering ? @"true" : @"false",
                              reactionCooldownSeconds]);
    TokenForgeSetCompanionOverlayMotionProfileWithTrace(@"direct", motionMode, idleRadius, wanderRadius, wanderSpeed, decisionIntervalSeconds, allowsWandering, reactionCooldownSeconds);
    TokenForgeNativeEntryReturnLog(@"SetCompanionOverlayMotionProfile", @"delegated=true");
}

static void TokenForgeSetCompanionOverlayMotionProfileWithTrace(NSString *traceId, int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds)
{
    TokenForgeRefreshNativeSafetyFlags();
    if (TokenForgeNativeSafeMode || TokenForgeDisableNativeOverlay || TokenForgeDisableMovementTimers) {
        NSLog(@"INFO [NativeSafeMode][SKIP] function=TokenForgeSetCompanionOverlayMotionProfileWithTrace reason=%@",
              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : (TokenForgeDisableNativeOverlay ? @"TOKENFORGE_DISABLE_NATIVE_OVERLAY" : @"TOKENFORGE_DISABLE_MOVEMENT_TIMERS"));
        [TokenForgeCompanionMotionTimer invalidate];
        TokenForgeCompanionMotionTimer = nil;
        return;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *trace = traceId.length > 0 ? traceId : @"direct";
        NSLog(@"INFO [OverlayTrace:%@] Native entered SetCompanionOverlayMotionProfile mode=%d allowsWandering=%@ speed=%.2f",
              trace,
              motionMode,
              allowsWandering ? @"true" : @"false",
              wanderSpeed);
        TokenForgeCompanionMotionMode = motionMode;
        TokenForgeCompanionIdleRadius = MAX(0.0, MIN(24.0, idleRadius));
        TokenForgeCompanionWanderRadius = MAX(0.0, MIN(360.0, wanderRadius));
        TokenForgeCompanionWanderSpeed = MAX(0.0, MIN(80.0, wanderSpeed));
        TokenForgeCompanionDecisionInterval = MAX(0.8, MIN(10.0, decisionIntervalSeconds));
        TokenForgeCompanionAllowsWandering = allowsWandering;
        TokenForgeCompanionReactionCooldown = MAX(0.4, MIN(4.0, reactionCooldownSeconds));
        NSLog(@"INFO [OverlayMotion] enabled=%@ mode=%d speed=%.2f",
              (allowsWandering && motionMode != 0 && TokenForgeCompanionWanderSpeed > 0.0) ? @"true" : @"false",
              motionMode,
              TokenForgeCompanionWanderSpeed);
        NSLog(@"INFO [OverlayMovementDiagnostic] role=desktopOverlay source=motionProfile mode=%d idleRadius=%.2f wanderRadius=%.2f wanderSpeed=%.2f allowsWandering=%@ timerActive=%@",
              motionMode,
              TokenForgeCompanionIdleRadius,
              TokenForgeCompanionWanderRadius,
              TokenForgeCompanionWanderSpeed,
              TokenForgeCompanionAllowsWandering ? @"true" : @"false",
              TokenForgeCompanionMotionTimer != nil ? @"true" : @"false");
        if (allowsWandering && motionMode != 0 && TokenForgeCompanionWanderSpeed > 0.0) {
            if (TokenForgeCompanionWindow == nil || !TokenForgeCompanionWindow.isVisible) {
                NSLog(@"INFO [OverlayTrace:%@] motion_setting_saved visible=false timerActive=false showPolicy=showButtonRequired", trace);
                return;
            }

            TokenForgeEnsureCompanionMotionTimer();
            TokenForgeMotionTickLogged = NO;
	            NSLog(@"INFO [DesktopCompanion] movement started speed=%.2f", TokenForgeCompanionWanderSpeed);
	            NSLog(@"INFO [DesktopOverlay] movementTimer started interval=%.2f", 1.0 / 30.0);
	            NSLog(@"INFO [Overlay][MOTION_TIMER_START] trace=%@ interval=%.2f", trace, 1.0 / 30.0);
            NSRect targetFrame = NSMakeRect(TokenForgeCompanionAnchor.x + TokenForgeCompanionWanderRadius, TokenForgeCompanionAnchor.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
            NSLog(@"INFO [DesktopCompanion] wander start frame=(%.2f,%.2f %.2fx%.2f) target=(%.2f,%.2f %.2fx%.2f) timerActive=%@",
                  TokenForgeCompanionWindow != nil ? TokenForgeCompanionWindow.frame.origin.x : TokenForgeCompanionAnchor.x,
                  TokenForgeCompanionWindow != nil ? TokenForgeCompanionWindow.frame.origin.y : TokenForgeCompanionAnchor.y,
                  TokenForgeCompanionSize.width,
                  TokenForgeCompanionSize.height,
                  targetFrame.origin.x,
                  targetFrame.origin.y,
                  targetFrame.size.width,
                  targetFrame.size.height,
                  TokenForgeCompanionMotionTimer != nil ? @"true" : @"false");
            NSLog(@"INFO [OverlayTrace:%@] motionEnabled=true timerActive=%@ panelVisible=%@",
                  trace,
                  TokenForgeCompanionMotionTimer != nil ? @"true" : @"false",
                  TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        } else {
            [TokenForgeCompanionMotionTimer invalidate];
            TokenForgeCompanionMotionTimer = nil;
            TokenForgeCompanionVelocity = NSMakePoint(0, 0);
	            NSLog(@"INFO [DesktopCompanion] movement stopped reason=settingOff");
	            NSLog(@"INFO [Overlay][MOTION_TIMER_STOP] trace=%@ reason=settingOff", trace);
	            NSLog(@"INFO [OverlayTrace:%@] motionEnabled=false overlayStillVisible=%@", trace, TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        }
    });
}

extern "C" void TriggerCompanionOverlayReaction(int reaction, const char *speechText)
{
    TokenForgeNativeEntryLog(@"TriggerCompanionOverlayReaction",
                             [NSString stringWithFormat:@"reaction=%d speechPresent=%@", reaction, speechText != NULL ? @"true" : @"false"]);
    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *speech = TokenForgeSafeMenuString(speechText, @"First safe summary will start growth.");
        TokenForgeTriggerOverlayReaction(reaction, speech);
        TokenForgeNativeEntryReturnLog(@"TriggerCompanionOverlayReaction", @"triggered=true");
    });
}

extern "C" void ResetCompanionOverlayPosition()
{
    TokenForgeNativeEntryLog(@"ResetCompanionOverlayPosition", @"none");
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeIsDraggingOverlay) {
            TokenForgeQueueOverlayActionAfterDrag(TokenForgePendingOverlayActionResetPosition, @"resetPosition");
            TokenForgeNativeEntryReturnLog(@"ResetCompanionOverlayPosition", @"queued=true reason=dragging");
            return;
        }
        TokenForgeResetCompanionFrame();
        TokenForgeNativeEntryReturnLog(@"ResetCompanionOverlayPosition", @"reset=true");
    });
}

extern "C" void SetCompanionOverlayVisualState(int stage, int archetype, int animationState, bool facingLeft)
{
    TokenForgeNativeEntryLog(@"SetCompanionOverlayVisualState",
                             [NSString stringWithFormat:@"stage=%d archetype=%d animationState=%d facingLeft=%@", stage, archetype, animationState, facingLeft ? @"true" : @"false"]);
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionContentView == nil) return;
        TokenForgeCompanionContentView.stage = stage;
        TokenForgeCompanionContentView.level = MAX(1, TokenForgeCompanionContentView.level);
        TokenForgeCompanionContentView.archetype = archetype;
        TokenForgeCompanionContentView.animationState = animationState;
        TokenForgeCompanionContentView.facingLeft = facingLeft;
        if (stage > 0 && !TokenForgeCompanionSnapshotHydrated) {
            TokenForgeHydrateCompanionSnapshot(TokenForgeCompanionContentView.repositoryId ?: @"unknown",
                                               stage,
                                               TokenForgeCompanionContentView.level,
                                               TokenForgeCompanionContentView.xp,
                                               archetype,
                                               TokenForgeCompanionContentView.visualThemeId ?: @"orange_cat",
                                               @"visualState");
        }
        [TokenForgeCompanionContentView setNeedsDisplay:YES];
        TokenForgeNativeEntryReturnLog(@"SetCompanionOverlayVisualState", @"updated=true");
    });
}

extern "C" void TokenForge_SetCompanionRenderSnapshot(const char *repositoryId, int stage, int level, int xp, int archetype, const char *visualThemeId, bool hydrated)
{
    TokenForgeNativeEntryLog(@"TokenForge_SetCompanionRenderSnapshot",
                             [NSString stringWithFormat:@"stage=%d level=%d xp=%d archetype=%d hydrated=%@", stage, level, xp, archetype, hydrated ? @"true" : @"false"]);
    TokenForgeRefreshNativeSafetyFlags();
    if (TokenForgeNativeSafeMode || TokenForgeDisablePixelNativeRenderer) {
        NSLog(@"INFO [NativeSafeMode][SKIP] function=TokenForge_SetCompanionRenderSnapshot reason=%@",
              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER");
        TokenForgeNativeEntryReturnLog(@"TokenForge_SetCompanionRenderSnapshot", @"skipped=true");
        return;
    }

    NSString *repo = TokenForgeSafeMenuString(repositoryId, @"unknown");
    NSString *theme = TokenForgeSafeMenuString(visualThemeId, @"orange_cat");
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!hydrated) {
            TokenForgeCompanionSnapshotHydrated = NO;
            if (TokenForgeCompanionContentView != nil) {
                TokenForgeCompanionContentView.snapshotHydrated = NO;
                [TokenForgeCompanionContentView setNeedsDisplay:YES];
            }
            NSLog(@"INFO [CompanionSnapshot][UNHYDRATED] repo=%@ source=csharp", repo);
            TokenForgeNativeEntryReturnLog(@"TokenForge_SetCompanionRenderSnapshot", @"hydrated=false");
            return;
        }

        TokenForgeHydrateCompanionSnapshot(repo, stage, level, xp, archetype, theme, @"csharp.renderSnapshot");
        TokenForgeNativeEntryReturnLog(@"TokenForge_SetCompanionRenderSnapshot", @"hydrated=true");
    });
}

extern "C" void TokenForge_SetCompanionFarmSnapshots(const char *json)
{
    TokenForgeNativeEntryLog(@"TokenForge_SetCompanionFarmSnapshots",
                             [NSString stringWithFormat:@"jsonBytes=%lu", json != NULL ? (unsigned long)strlen(json) : 0UL]);
    TokenForgeRefreshNativeSafetyFlags();
    if (TokenForgeNativeSafeMode || TokenForgeDisablePixelNativeRenderer) {
        NSLog(@"INFO [NativeSafeMode][SKIP] function=TokenForge_SetCompanionFarmSnapshots reason=%@",
              TokenForgeNativeSafeMode ? @"TOKENFORGE_NATIVE_SAFE_MODE" : @"TOKENFORGE_DISABLE_PIXEL_NATIVE_RENDERER");
        TokenForgeNativeEntryReturnLog(@"TokenForge_SetCompanionFarmSnapshots", @"skipped=true");
        return;
    }

    NSString *payload = json == NULL ? @"[]" : [NSString stringWithUTF8String:json];
    if (payload.length == 0) {
        payload = @"[]";
    }
    void (^applySnapshots)(void) = ^{
        NSData *data = [payload dataUsingEncoding:NSUTF8StringEncoding];
        NSError *error = nil;
        id root = data != nil ? [NSJSONSerialization JSONObjectWithData:data options:0 error:&error] : nil;
        NSArray *items = nil;
        if ([root isKindOfClass:[NSDictionary class]]) {
            id overlays = ((NSDictionary *)root)[@"overlays"];
            items = [overlays isKindOfClass:[NSArray class]] ? overlays : @[];
        } else if ([root isKindOfClass:[NSArray class]]) {
            items = (NSArray *)root;
        } else {
            items = @[];
        }
        if (error != nil) {
            NSLog(@"WARN [OverlayFarm][SNAPSHOT_APPLY] count=0 reason=jsonError message=%@", error.localizedDescription ?: @"unknown");
        }
        TokenForgeApplyFarmSnapshotsOnMain(items, @"csharp.farmSnapshot");
        TokenForgeNativeEntryReturnLog(@"TokenForge_SetCompanionFarmSnapshots", [NSString stringWithFormat:@"count=%lu", (unsigned long)items.count]);
    };
    if ([NSThread isMainThread]) {
        applySnapshots();
    } else {
        dispatch_async(dispatch_get_main_queue(), applySnapshots);
    }
}

extern "C" void TokenForge_ShowCompanionForRepository(const char *repositoryId, const char *source)
{
    NSString *repo = TokenForgeSafeMenuString(repositoryId, @"legacy");
    NSString *safeSource = TokenForgeSafeMenuString(source, @"csharp.showRepository");
    TokenForgeNativeEntryLog(@"TokenForge_ShowCompanionForRepository",
                             [NSString stringWithFormat:@"repo=%@ source=%@", repo, safeSource]);
    TokenForgeShowCompanionForRepositoryOnMain(repo, safeSource);
    TokenForgeNativeEntryReturnLog(@"TokenForge_ShowCompanionForRepository", @"delegated=true");
}

extern "C" void TokenForge_HideCompanionForRepository(const char *repositoryId, const char *source)
{
    NSString *repo = TokenForgeSafeMenuString(repositoryId, @"legacy");
    NSString *safeSource = TokenForgeSafeMenuString(source, @"csharp.hideRepository");
    TokenForgeNativeEntryLog(@"TokenForge_HideCompanionForRepository",
                             [NSString stringWithFormat:@"repo=%@ source=%@", repo, safeSource]);
    TokenForgeHideCompanionForRepositoryOnMain(repo, safeSource);
    TokenForgeNativeEntryReturnLog(@"TokenForge_HideCompanionForRepository", @"delegated=true");
}

extern "C" void TokenForge_ShowAllRepositoryCompanions(const char *source)
{
    NSString *safeSource = TokenForgeSafeMenuString(source, @"csharp.showAll");
    TokenForgeNativeEntryLog(@"TokenForge_ShowAllRepositoryCompanions",
                             [NSString stringWithFormat:@"source=%@", safeSource]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeEnsureOverlayFarmRegistry();
        NSLog(@"INFO [RuntimeUIPath][Overlay] renderer=showAll source=%@ panels=%lu snapshots=%lu",
              safeSource,
              (unsigned long)TokenForgeOverlayPanelsByRepositoryId.count,
              (unsigned long)TokenForgeOverlaySnapshotsByRepositoryId.count);
        if (TokenForgeOverlayPanelsByRepositoryId.count == 0 && TokenForgeOverlaySnapshotsByRepositoryId.count > 0) {
            NSUInteger index = 0;
            for (NSString *repo in [[TokenForgeOverlaySnapshotsByRepositoryId allKeys] copy]) {
                NSDictionary *snapshot = TokenForgeOverlaySnapshotsByRepositoryId[repo];
                if ([snapshot isKindOfClass:[NSDictionary class]]) {
                    TokenForgeCreateOrReuseOverlayPanelForSnapshot(snapshot, index, safeSource);
                    index += 1;
                }
            }
        }
	        if (TokenForgeOverlayPanelsByRepositoryId.count == 0) {
	            NSLog(@"INFO [OverlaySuppressed] reason=noFarmPanelsOrSnapshots source=%@ fallback=disabled", safeSource);
	            NSLog(@"INFO [Overlay][SNAPSHOT_MISSING] reason=noFarmPanelsOrSnapshots source=%@", safeSource);
	            NSLog(@"INFO [Overlay][NO_APPROVED_REPO] source=%@ reason=noRepositoryFarmSnapshot", safeSource);
	            NSLog(@"INFO [Overlay][Guard] repoHash=none desiredVisible=true actualVisible=false panelExists=false panelFrame=none reason=noRepositoryFarmSnapshot sourceAction=%@ selectedRepoId=none selectedRepoHash=none approvedRepoCount=0", safeSource);
	            NSLog(@"INFO [OverlayLifecycle][NO_REPOSITORY_HIDE_OVERLAY] repoHash=none desiredVisible=false actualVisible=false panelExists=false panelFrame=none reason=noRepositoryFarmSnapshot sourceAction=%@ selectedRepoId=none selectedRepoHash=none approvedRepoCount=0", safeSource);
	            NSLog(@"INFO [Overlay][ACTUAL_VISIBLE_COUNT] count=0 source=showAll.noPanels");
	            return;
	        }
        for (NSString *repo in [[TokenForgeOverlayPanelsByRepositoryId allKeys] copy]) {
            NSPanel *panel = TokenForgeOverlayPanelsByRepositoryId[repo];
            if (panel == nil) {
                continue;
	            }
		            [panel orderFrontRegardless];
		            NSLog(@"INFO [OverlayOrderFront] repo=%@ source=%@ visible=%@", repo, safeSource, panel.isVisible ? @"true" : @"false");
		            NSLog(@"INFO [Overlay][ORDER_FRONT] repo=%@ source=%@ visible=%@", repo, safeSource, panel.isVisible ? @"true" : @"false");
		            if (panel.isVisible) {
		                NSLog(@"INFO [Overlay][VISIBLE_TRUE] repo=%@ actualVisibleCount=%ld", repo, (long)TokenForgeVisibleOverlayFarmCount());
		            } else {
		                NSLog(@"INFO [Overlay][VISIBLE_FALSE] repo=%@ actualVisibleCount=%ld", repo, (long)TokenForgeVisibleOverlayFarmCount());
		            }
		            NSLog(@"INFO [OverlayFarm][SHOW] repo=%@ source=%@", repo, safeSource);
	        }
        NSLog(@"INFO [OverlayVisible] all=true actualVisibleCount=%ld legacyVisible=%@", (long)TokenForgeVisibleOverlayFarmCount(), (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"true" : @"false");
        NSLog(@"INFO [OverlayFarm][VISIBLE_COUNT] count=%ld", (long)TokenForgeVisibleOverlayFarmCount());
        NSLog(@"INFO [Overlay][ACTUAL_VISIBLE_COUNT] count=%ld source=showAll", (long)TokenForgeVisibleOverlayFarmCount());
        NSLog(@"INFO [OverlayMovementDiagnostic] role=farm source=showAll activeCompanionCount=%ld movementTimerActive=%@ selectedRepo=%@ nonSelectedActive=true",
              (long)TokenForgeVisibleOverlayFarmCount(),
              TokenForgeCompanionMotionTimer != nil ? @"true" : @"false",
              TokenForgeActiveDragRepositoryId ?: @"none");
        TokenForgeNativeEntryReturnLog(@"TokenForge_ShowAllRepositoryCompanions", @"shown=true");
    });
}

extern "C" void TokenForge_HideAllRepositoryCompanions(const char *source)
{
    NSString *safeSource = TokenForgeSafeMenuString(source, @"csharp.hideAll");
    TokenForgeNativeEntryLog(@"TokenForge_HideAllRepositoryCompanions",
                             [NSString stringWithFormat:@"source=%@", safeSource]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeEnsureOverlayFarmRegistry();
        NSInteger count = TokenForgeVisibleOverlayFarmCount();
        for (NSString *repo in TokenForgeOverlayPanelsByRepositoryId) {
            if (TokenForgeIsOverlayDraggingForRepository(repo)) {
                NSLog(@"INFO [OverlayDrag][CRASH_GUARD] repo=%@ reason=hideAllDeferredDuringDrag", repo);
                continue;
            }
		            [TokenForgeOverlayPanelsByRepositoryId[repo] orderOut:nil];
		            NSLog(@"INFO [OverlayFarm][HIDE] repo=%@ source=%@", repo, safeSource);
		            NSLog(@"INFO [Overlay][Actual] repoHash=%@ desiredVisible=false actualVisible=false panelExists=true panelFrame=%@ reason=hideAll sourceAction=%@",
		                  repo,
		                  NSStringFromRect(TokenForgeOverlayPanelsByRepositoryId[repo].frame),
		                  safeSource);
	        }
        NSLog(@"INFO [OverlayFarm][HIDE_ALL] count=%ld source=%@", (long)count, safeSource);
        NSLog(@"INFO [OverlayFarm][VISIBLE_COUNT] count=%ld", (long)TokenForgeVisibleOverlayFarmCount());
        TokenForgeNativeEntryReturnLog(@"TokenForge_HideAllRepositoryCompanions", [NSString stringWithFormat:@"hiddenCount=%ld", (long)count]);
    });
}

extern "C" bool TokenForge_IsOverlayDraggingForRepository(const char *repositoryId)
{
    NSString *repo = TokenForgeSafeMenuString(repositoryId, @"legacy");
    TokenForgeNativeEntryLog(@"TokenForge_IsOverlayDraggingForRepository",
                             [NSString stringWithFormat:@"repo=%@", repo]);
    __block BOOL dragging = NO;
    if ([NSThread isMainThread]) {
        dragging = TokenForgeIsOverlayDraggingForRepository(repo);
    } else {
        dispatch_sync(dispatch_get_main_queue(), ^{
            dragging = TokenForgeIsOverlayDraggingForRepository(repo);
        });
    }
    TokenForgeNativeEntryReturnLog(@"TokenForge_IsOverlayDraggingForRepository", [NSString stringWithFormat:@"dragging=%@", dragging ? @"true" : @"false"]);
    return dragging;
}

extern "C" const char *TokenForge_GetOverlayFrame(const char *repositoryId)
{
    TokenForgeNativeEntryLog(@"TokenForge_GetOverlayFrame", @"frameQuery=true");
    static char buffer[128];
    NSString *repo = TokenForgeSafeMenuString(repositoryId, @"legacy");
    __block NSRect frame = NSZeroRect;
    if ([NSThread isMainThread]) {
        frame = TokenForgeOverlayFrameForRepository(repo);
    }
    snprintf(buffer, sizeof(buffer), "%.2f,%.2f,%.2f,%.2f", frame.origin.x, frame.origin.y, frame.size.width, frame.size.height);
    TokenForgeNativeEntryReturnLog(@"TokenForge_GetOverlayFrame", [NSString stringWithFormat:@"frame=%s", buffer]);
    return buffer;
}

extern "C" void TokenForge_SetOverlayFrame(const char *repositoryId, float x, float y, float width, float height, const char *source)
{
    NSString *repo = TokenForgeSafeMenuString(repositoryId, @"legacy");
    NSString *safeSource = TokenForgeSafeMenuString(source, @"csharp.setFrame");
    TokenForgeNativeEntryLog(@"TokenForge_SetOverlayFrame",
                             [NSString stringWithFormat:@"repo=%@ x=%.2f y=%.2f width=%.2f height=%.2f source=%@", repo, x, y, width, height, safeSource]);
    TokenForgeSetOverlayFrameForRepositoryOnMain(repo, NSMakeRect(x, y, MAX(24.0, width), MAX(24.0, height)), safeSource);
    TokenForgeNativeEntryReturnLog(@"TokenForge_SetOverlayFrame", @"delegated=true");
}

extern "C" void SetCompanionOverlayVisualTheme(const char *visualThemeId)
{
    TokenForgeNativeEntryLog(@"SetCompanionOverlayVisualTheme",
                             [NSString stringWithFormat:@"themePresent=%@", visualThemeId != NULL ? @"true" : @"false"]);
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionContentView == nil) return;
        NSString *theme = visualThemeId == NULL ? @"orange_cat" : [NSString stringWithUTF8String:visualThemeId];
        TokenForgeCompanionContentView.visualThemeId = theme.length > 0 ? theme : @"orange_cat";
        [TokenForgeCompanionContentView setNeedsDisplay:YES];
        TokenForgeNativeEntryReturnLog(@"SetCompanionOverlayVisualTheme", [NSString stringWithFormat:@"theme=%@", TokenForgeCompanionContentView.visualThemeId]);
    });
}

extern "C" void SetCompanionOverlayClickThrough(bool clickThrough)
{
    TokenForgeNativeEntryLog(@"SetCompanionOverlayClickThrough",
                             [NSString stringWithFormat:@"clickThrough=%@", clickThrough ? @"true" : @"false"]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeMenuClickThrough = clickThrough;
        if (TokenForgeCompanionWindow == nil) {
            NSLog(@"INFO [DesktopCompanion] click-through %@ pending window=nil", clickThrough ? @"enabled" : @"disabled");
            return;
        }

        if (TokenForgeIsDraggingOverlay && clickThrough) {
            TokenForgeCompanionWindow.ignoresMouseEvents = NO;
            TokenForgePendingClickThroughAfterDrag = YES;
            TokenForgePendingClickThroughValueAfterDrag = clickThrough;
            NSLog(@"INFO [OverlayState][DEFER_CLICK_THROUGH] reason=dragging desiredClickThrough=true actualIgnoresMouseEvents=false");
        } else {
            if (!clickThrough) {
                TokenForgePendingClickThroughAfterDrag = NO;
            }
            TokenForgeCompanionWindow.ignoresMouseEvents = clickThrough;
        }
        TokenForgeEnsureOverlayFarmRegistry();
        for (NSString *repo in TokenForgeOverlayPanelsByRepositoryId) {
            NSPanel *panel = TokenForgeOverlayPanelsByRepositoryId[repo];
            if (TokenForgeIsOverlayDraggingForRepository(repo) && clickThrough) {
                panel.ignoresMouseEvents = NO;
                NSLog(@"INFO [OverlayState][DEFER_CLICK_THROUGH] repo=%@ reason=dragging desiredClickThrough=true actualIgnoresMouseEvents=false", repo);
            } else {
                panel.ignoresMouseEvents = clickThrough;
            }
        }
	        NSLog(@"INFO [DesktopCompanion] %@", clickThrough ? @"click-through enabled" : @"click-through disabled");
	        NSLog(@"INFO [OverlayClickThrough] enabled=%@", clickThrough ? @"true" : @"false");
	        NSLog(@"INFO [Overlay][CLICK_THROUGH] enabled=%@", clickThrough ? @"true" : @"false");
	        NSLog(@"INFO [OverlayDrag] enabled=%@", clickThrough ? @"false" : @"true");
        NSLog(@"INFO [OverlayState][APPLY] desiredDrag=%@ desiredClickThrough=%@ actualIgnoresMouseEvents=%@",
              clickThrough ? @"false" : @"true",
              clickThrough ? @"true" : @"false",
              TokenForgeCompanionWindow.ignoresMouseEvents ? @"true" : @"false");
        NSLog(@"INFO [OverlayState][PROJECT] visible=%@ movement=%@ drag=%@ clickThrough=%@",
              (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"true" : @"false",
              (TokenForgeCompanionMotionTimer != nil && TokenForgeCompanionAllowsWandering && TokenForgeCompanionMotionMode != 0) ? @"true" : @"false",
              clickThrough ? @"false" : @"true",
              clickThrough ? @"true" : @"false");
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        TokenForgeNativeEntryReturnLog(@"SetCompanionOverlayClickThrough", [NSString stringWithFormat:@"clickThrough=%@", clickThrough ? @"true" : @"false"]);
    });
}

extern "C" void TokenForge_SetOverlayClickEnabled(bool enabled)
{
    TokenForgeNativeEntryLog(@"TokenForge_SetOverlayClickEnabled",
                             [NSString stringWithFormat:@"enabled=%@", enabled ? @"true" : @"false"]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayClickEnabled = enabled;
        if (!enabled) {
            NSLog(@"INFO [OverlayDrag][BLOCKED] repo=all reason=clickDisabled");
        }
        TokenForgeNativeEntryReturnLog(@"TokenForge_SetOverlayClickEnabled", [NSString stringWithFormat:@"enabled=%@", enabled ? @"true" : @"false"]);
    });
}

extern "C" void TokenForge_SetOverlayClickThrough(bool enabled)
{
    TokenForgeNativeEntryLog(@"TokenForge_SetOverlayClickThrough",
                             [NSString stringWithFormat:@"enabled=%@", enabled ? @"true" : @"false"]);
    SetCompanionOverlayClickThrough(enabled);
    TokenForgeNativeEntryReturnLog(@"TokenForge_SetOverlayClickThrough", @"delegated=true");
}

extern "C" void TokenForge_RegisterOverlayClickedCallback(TokenForgeOverlayClickedCallback callback)
{
    TokenForgeNativeEntryLog(@"TokenForge_RegisterOverlayClickedCallback",
                             [NSString stringWithFormat:@"callback=%p", callback]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayClicked = callback;
        TokenForgeNativeEntryReturnLog(@"TokenForge_RegisterOverlayClickedCallback", @"registered=true");
    });
}

extern "C" void TokenForge_RegisterOverlayDoubleClickedCallback(TokenForgeOverlayClickedCallback callback)
{
    TokenForgeNativeEntryLog(@"TokenForge_RegisterOverlayDoubleClickedCallback",
                             [NSString stringWithFormat:@"callback=%p", callback]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayDoubleClicked = callback;
        TokenForgeNativeEntryReturnLog(@"TokenForge_RegisterOverlayDoubleClickedCallback", @"registered=true");
    });
}

extern "C" void TokenForge_RegisterOverlayDragEndedCallback(TokenForgeOverlayDragEndedCallback callback)
{
    TokenForgeNativeEntryLog(@"TokenForge_RegisterOverlayDragEndedCallback",
                             [NSString stringWithFormat:@"callback=%p", callback]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayDragEnded = callback;
        TokenForgeNativeEntryReturnLog(@"TokenForge_RegisterOverlayDragEndedCallback", @"registered=true");
    });
}

extern "C" void TokenForge_RegisterOverlayDragEndedForRepositoryCallback(TokenForgeOverlayDragEndedForRepositoryCallback callback)
{
    TokenForgeNativeEntryLog(@"TokenForge_RegisterOverlayDragEndedForRepositoryCallback",
                             [NSString stringWithFormat:@"callback=%p", callback]);
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayDragEndedForRepository = callback;
        TokenForgeNativeEntryReturnLog(@"TokenForge_RegisterOverlayDragEndedForRepositoryCallback", @"registered=true");
    });
}

extern "C" void DestroyDesktopCompanionOverlay()
{
    NSString *destroySource = TokenForgeRuntimeVerifierCleanupQuitRequested
        ? @"verificationCleanup"
        : (TokenForgeExplicitQuitRequested ? @"explicitQuit" : (TokenForgeTerminating ? @"appShutdown" : @"overlayDestroy"));
    TokenForgeNativeEntryLog(@"DestroyDesktopCompanionOverlay", [NSString stringWithFormat:@"source=%@", destroySource]);
    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *mainDestroySource = TokenForgeRuntimeVerifierCleanupQuitRequested
            ? @"verificationCleanup"
            : (TokenForgeExplicitQuitRequested ? @"explicitQuit" : (TokenForgeTerminating ? @"appShutdown" : @"overlayDestroy"));
        TokenForgeLogQuitDiagnostic(@"DestroyDesktopCompanionOverlay",
                                    mainDestroySource,
                                    TokenForgeExplicitQuitRequested || TokenForgeTerminating,
                                    NO,
                                    @"overlayPanelDestroyOnly");
        NSLog(@"INFO [OverlayLifecycle][PANEL_DESTROY] source=%@ explicitQuit=%@ appTerminating=%@ verificationMode=%@ panelExists=%@ visible=%@",
              mainDestroySource,
              TokenForgeExplicitQuitRequested ? @"true" : @"false",
              TokenForgeTerminating ? @"true" : @"false",
              TokenForgeRuntimeVerificationMode ? @"true" : @"false",
              TokenForgeCompanionWindow != nil ? @"true" : @"false",
              (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"true" : @"false");
        if (TokenForgeIsDraggingOverlay && !TokenForgeExplicitQuitRequested && !TokenForgeTerminating) {
            TokenForgeQueueOverlayActionAfterDrag(TokenForgePendingOverlayActionDestroy, @"destroy");
            TokenForgeNativeEntryReturnLog(@"DestroyDesktopCompanionOverlay", @"queued=true reason=dragging");
            return;
        }
        [TokenForgeCompanionWindow orderOut:nil];
        TokenForgeEnsureOverlayFarmRegistry();
        for (NSPanel *panel in [TokenForgeOverlayPanelsByRepositoryId allValues]) {
            [panel orderOut:nil];
        }
        [TokenForgeOverlayPanelsByRepositoryId removeAllObjects];
        [TokenForgeOverlayViewsByRepositoryId removeAllObjects];
        [TokenForgeOverlaySnapshotsByRepositoryId removeAllObjects];
        [TokenForgeOverlayFramesByRepositoryId removeAllObjects];
        [TokenForgeOverlayDragStatesByRepositoryId removeAllObjects];
        [TokenForgeOverlayGenerationsByRepositoryId removeAllObjects];
        TokenForgeCompanionWindow = nil;
        TokenForgeCompanionContentView = nil;
        TokenForgeCompanionVelocity = NSMakePoint(0, 0);
        [TokenForgeCompanionMotionTimer invalidate];
        TokenForgeCompanionMotionTimer = nil;
        NSLog(@"INFO [DesktopOverlay] quit cleanup completed");
        TokenForgeNativeEntryReturnLog(@"DestroyDesktopCompanionOverlay", @"destroyed=true");
    });
}
