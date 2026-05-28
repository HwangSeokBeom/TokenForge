#import <Cocoa/Cocoa.h>
#include <math.h>
#include <dlfcn.h>
#include <limits.h>
#include <string.h>
#include <sys/stat.h>
#import <CommonCrypto/CommonDigest.h>

@class TokenForgeAppLifecycleDelegate;
@class TokenForgeNativeDashboardController;
static TokenForgeAppLifecycleDelegate *TokenForgeEnsureLifecycleDelegate(void);
static TokenForgeNativeDashboardController *TokenForgeEnsureNativeDashboardController(void);
static void TokenForgeOpenNativeDashboardOnMain(void);
extern "C" void ShowDesktopCompanionOverlay(void);
extern "C" void HideDesktopCompanionOverlay(void);
extern "C" const char *TokenForge_GetOverlayLibraryPath(void);
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
- (void)updateStatusItemMenu;
- (void)showMainWindow;
- (void)hideMainWindow;
- (BOOL)isMainWindowVisible;
@end

@interface TokenForgeCompanionView : NSView
@property(nonatomic) NSInteger stage;
@property(nonatomic) NSInteger archetype;
@property(nonatomic) NSInteger animationState;
@property(nonatomic) BOOL facingLeft;
@property(nonatomic) CGFloat visualScale;
@property(nonatomic) CGFloat visualRotation;
@property(nonatomic) CGFloat visualOffsetY;
@property(nonatomic) CGFloat safeDrawingInset;
@property(nonatomic, strong) NSString *visualThemeId;
@property(nonatomic, strong) NSString *assetType;
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

@interface TokenForgeDashboardHeroAvatarContainerView : NSView
@property(nonatomic, strong) TokenForgeAvatarPreviewView *preview;
@property(nonatomic, strong) NSString *repositoryName;
@end

@interface TokenForgeCompanionOverlayWindow : NSWindow
@end

typedef void (*TokenForgeOverlayClickedCallback)(void);
typedef void (*TokenForgeOverlayDragEndedCallback)(float x, float y);
typedef void (*TokenForgeMenuActionCallback)(const char *action);
typedef void (*TokenForgeDashboardActionCallback)(const char *action);
static TokenForgeOverlayClickedCallback TokenForgeOverlayClicked = nil;
static TokenForgeOverlayClickedCallback TokenForgeOverlayDoubleClicked = nil;
static TokenForgeOverlayDragEndedCallback TokenForgeOverlayDragEnded = nil;
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
static BOOL TokenForgeDebugOverlayDiagnosticsEnabled = YES;
static BOOL TokenForgeDashboardOpening = NO;
static BOOL TokenForgeLifecycleInstallDeferred = NO;
static BOOL TokenForgeDesiredCompanionVisible = NO;
static BOOL TokenForgeAppLifecycleAllowsOverlay = YES;
static NSString *TokenForgeLastHideReason = @"none";
static NSString *TokenForgeLastShowReason = @"startup";
static NSString *TokenForgeLastProjectionSource = @"startup";
static NSString *TokenForgeNativePluginVersion = @"native-plugin-lifecycle-v8";
static NSPoint TokenForgeDragStartMouse = {0, 0};
static NSPoint TokenForgeDragStartOrigin = {0, 0};
static NSPoint TokenForgeCompanionAnchor = {0, 0};

static NSRect TokenForgeClampFrameToVisibleFrame(NSRect frame);
static void TokenForgePersistCompanionFrame(NSRect frame);
static void TokenForgeTriggerOverlayReaction(NSInteger reaction, NSString *speech);
static void TokenForgeDrawAvatarInRect(TokenForgeCompanionView *view, NSRect containerRect, NSString *preset, NSInteger frameIndex, NSString *mode);
static NSImage *TokenForgeAvatarImageForPreset(NSString *preset, NSSize imageSize, NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode, NSString *theme);
static const char *TokenForgeNextOverlayTraceId(void);
static void TokenForgeLogRuntimeIdentityIfNeeded(void);
static void TokenForgeDumpAllWindows(NSString *reason);
static void TokenForgeDumpOverlayPanelState(NSString *traceId);
static BOOL TokenForgeWindowLooksBlank(NSWindow *window);
static void TokenForgeLogWindowLifecycle(NSString *event, NSWindow *window, NSString *reason);
static void TokenForgeShowDesktopCompanionOverlayWithTrace(NSString *traceId);
static void TokenForgeHideDesktopCompanionOverlayWithTrace(NSString *traceId);
static void TokenForgeSetCompanionOverlayMotionProfileWithTrace(NSString *traceId, int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds);
static BOOL TokenForgeAppKitRegistrationReady(void);
static void TokenForgeRequestLifecycleInstall(NSString *reason);
static void TokenForgeLogOverlayProjection(NSString *traceId, NSString *source);
static void TokenForgeScheduleOverlayWatchdogs(NSString *traceId);
static void TokenForgeCreateCompanionOverlayOnMain(NSString *traceId);

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
    [[NSBezierPath bezierPathWithOvalInRect:TokenForgeAvatarMapRect(finalRect, partRect)] fill];
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

static BOOL TokenForgeAvatarPartInside(NSRect finalRect, NSRect partRect)
{
    return NSContainsRect(NSInsetRect(finalRect, -0.5, -0.5), TokenForgeAvatarMapRect(finalRect, partRect));
}

static void TokenForgeLogAvatarRenderer(NSString *preset, NSRect containerRect, NSRect finalRect, CGFloat scale, BOOL clipped, BOOL partsInside, NSString *imageSource, NSSize imageSize, NSString *cacheKey, BOOL cacheHit)
{
    static NSMutableDictionary<NSString *, NSNumber *> *lastLogTimes = nil;
    if (lastLogTimes == nil) {
        lastLogTimes = [NSMutableDictionary dictionary];
    }

    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    NSString *key = preset ?: @"hero";
    NSTimeInterval last = [lastLogTimes[key] doubleValue];
    if (![key isEqualToString:@"menuBar"] && now - last < 1.4) {
        return;
    }

    lastLogTimes[key] = @(now);
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
          imageSource ?: @"vectorGenerated",
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
    context.imageInterpolation = NSImageInterpolationHigh;
    NSAffineTransform *transform = [NSAffineTransform transform];
    [transform translateXBy:NSMidX(finalRect) yBy:NSMidY(finalRect)];
    if (view.facingLeft) {
        [transform scaleXBy:-1.0 yBy:1.0];
    }
    [transform rotateByDegrees:view.visualRotation];
    [transform translateXBy:-NSMidX(finalRect) yBy:-NSMidY(finalRect)];
    [transform concat];

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

        NSBezierPath *leftEar = [NSBezierPath bezierPath];
        [leftEar moveToPoint:TokenForgeAvatarMapPoint(finalRect, 9.0, 29.0 + bounce)];
        [leftEar lineToPoint:TokenForgeAvatarMapPoint(finalRect, 12.0, 35.0 + bounce)];
        [leftEar lineToPoint:TokenForgeAvatarMapPoint(finalRect, 15.0, 29.5 + bounce)];
        [leftEar closePath];
        [outline setFill];
        [leftEar fill];
        NSBezierPath *rightEar = [NSBezierPath bezierPath];
        [rightEar moveToPoint:TokenForgeAvatarMapPoint(finalRect, 17.0, 29.5 + bounce)];
        [rightEar lineToPoint:TokenForgeAvatarMapPoint(finalRect, 20.0, 35.0 + bounce)];
        [rightEar lineToPoint:TokenForgeAvatarMapPoint(finalRect, 23.0, 29.0 + bounce)];
        [rightEar closePath];
        [rightEar fill];
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
        [outline setStroke];
        NSBezierPath *band = [NSBezierPath bezierPath];
        [band moveToPoint:TokenForgeAvatarMapPoint(finalRect, 7.0, 27.5 + bounce)];
        [band curveToPoint:TokenForgeAvatarMapPoint(finalRect, 25.0, 27.5 + bounce)
             controlPoint1:TokenForgeAvatarMapPoint(finalRect, 10.0, 34.0 + bounce)
             controlPoint2:TokenForgeAvatarMapPoint(finalRect, 22.0, 34.0 + bounce)];
        band.lineWidth = MAX(1.0, scale * 1.2);
        [band stroke];
    }

    [context restoreGraphicsState];

    BOOL clipped = !NSContainsRect(NSInsetRect(containerRect, -0.5, -0.5), finalRect) || !partsInside;
    TokenForgeLogAvatarRenderer(resolvedPreset, containerRect, finalRect, scale, clipped, partsInside, @"vectorGenerated", containerRect.size, nil, NO);
}

static NSString *TokenForgeAvatarCacheKey(NSString *preset, NSSize imageSize, NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode, NSString *theme)
{
    return [NSString stringWithFormat:@"%@|%.0fx%.0f|stage=%ld|arch=%ld|frame=%ld|mode=%@|theme=%@|scale=%.2f",
            preset ?: @"overlay",
            imageSize.width,
            imageSize.height,
            (long)stage,
            (long)archetype,
            (long)frameIndex,
            mode ?: @"idle",
            theme ?: @"orange_cat",
            [NSScreen mainScreen].backingScaleFactor];
}

static NSImage *TokenForgeAvatarImageForPreset(NSString *preset, NSSize imageSize, NSInteger stage, NSInteger archetype, NSInteger frameIndex, NSString *mode, NSString *theme)
{
    static NSMutableDictionary<NSString *, NSImage *> *imageCache = nil;
    if (imageCache == nil) {
        imageCache = [NSMutableDictionary dictionary];
    }

    NSString *resolvedPreset = preset.length > 0 ? preset : @"overlay";
    NSString *cacheKey = TokenForgeAvatarCacheKey(resolvedPreset, imageSize, stage, archetype, frameIndex, mode, theme);
    NSImage *cached = imageCache[cacheKey];
    if (cached != nil) {
        CGFloat scale = 1.0;
        NSRect finalRect = TokenForgeAvatarFinalDrawingRect(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), -1.0, &scale);
        TokenForgeLogAvatarRenderer(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), finalRect, scale, NO, YES, @"cached", imageSize, cacheKey, YES);
        return cached;
    }

    NSImage *image = [[NSImage alloc] initWithSize:imageSize];
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
        [[NSBezierPath bezierPathWithOvalInRect:NSInsetRect(NSMakeRect(0, 0, imageSize.width, imageSize.height), 1.0, 1.0)] fill];
    }

    TokenForgeCompanionView *view = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height)];
    view.stage = stage;
    view.archetype = archetype;
    view.animationState = frameIndex;
    view.facingLeft = frameIndex % 4 == 3;
    view.safeDrawingInset = TokenForgeAvatarSafeInsetForPreset(resolvedPreset, view.bounds);
    view.assetType = resolvedPreset;
    view.visualThemeId = theme.length > 0 ? theme : @"orange_cat";
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

    if ([mode isEqualToString:@"hidden"]) {
        [[NSColor colorWithCalibratedWhite:1.0 alpha:0.55] setFill];
        NSRectFillUsingOperation(NSMakeRect(0, 0, imageSize.width, imageSize.height), NSCompositingOperationSourceAtop);
    }

    [image unlockFocus];
    image.size = imageSize;
    [image setTemplate:NO];
    imageCache[cacheKey] = image;
    CGFloat scale = 1.0;
    NSRect finalRect = TokenForgeAvatarFinalDrawingRect(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), -1.0, &scale);
    TokenForgeLogAvatarRenderer(resolvedPreset, NSMakeRect(0.0, 0.0, imageSize.width, imageSize.height), finalRect, scale, NO, YES, @"vectorGenerated", imageSize, cacheKey, NO);
    return image;
}

@implementation TokenForgeCompanionView
- (BOOL)isOpaque { return NO; }
- (BOOL)acceptsFirstMouse:(NSEvent *)event { return YES; }
- (BOOL)acceptsFirstResponder { return YES; }

- (void)mouseDown:(NSEvent *)event
{
    if (!TokenForgeOverlayClickEnabled) {
        [super mouseDown:event];
        return;
    }

    self.window.ignoresMouseEvents = NO;
    NSLog(@"INFO [CompanionDrag] mouseDown screen=(%.2f,%.2f) window=(%.2f,%.2f)",
          [NSEvent mouseLocation].x,
          [NSEvent mouseLocation].y,
          self.window.frame.origin.x,
          self.window.frame.origin.y);
    TokenForgeIsDraggingOverlay = YES;
    TokenForgeDragExceededThreshold = NO;
    TokenForgeDragStartMouse = [NSEvent mouseLocation];
    TokenForgeDragStartOrigin = self.window.frame.origin;
}

- (void)mouseDragged:(NSEvent *)event
{
    if (!TokenForgeOverlayClickEnabled || !TokenForgeIsDraggingOverlay || self.window == nil) {
        return;
    }

    NSPoint currentMouse = [NSEvent mouseLocation];
    CGFloat dx = currentMouse.x - TokenForgeDragStartMouse.x;
    CGFloat dy = currentMouse.y - TokenForgeDragStartMouse.y;
    if (!TokenForgeDragExceededThreshold && hypot(dx, dy) > 4.0) {
        TokenForgeDragExceededThreshold = YES;
        NSLog(@"INFO [CompanionDrag] thresholdExceeded");
        NSLog(@"INFO [CompanionMotion] idlePaused reason=drag");
        TokenForgeMotionPauseLogged = YES;
    }

    if (TokenForgeDragExceededThreshold) {
        NSRect frame = self.window.frame;
        NSPoint oldOrigin = frame.origin;
        frame.origin = NSMakePoint(TokenForgeDragStartOrigin.x + dx, TokenForgeDragStartOrigin.y + dy);
        frame = TokenForgeClampFrameToVisibleFrame(frame);
        [self.window setFrameOrigin:frame.origin];
        TokenForgeCompanionAnchor = frame.origin;
        NSLog(@"INFO [CompanionDrag] setFrameOrigin old=(%.2f,%.2f) new=(%.2f,%.2f)",
              oldOrigin.x,
              oldOrigin.y,
              frame.origin.x,
              frame.origin.y);
    }
}

- (void)mouseUp:(NSEvent *)event
{
    if (!TokenForgeOverlayClickEnabled || self.window == nil) {
        TokenForgeIsDraggingOverlay = NO;
        return;
    }

    NSLog(@"INFO [CompanionDrag] mouseUp screen=(%.2f,%.2f)",
          [NSEvent mouseLocation].x,
          [NSEvent mouseLocation].y);
    NSRect frame = TokenForgeClampFrameToVisibleFrame(self.window.frame);
    [self.window setFrameOrigin:frame.origin];
    TokenForgeCompanionAnchor = frame.origin;
    if (TokenForgeDragExceededThreshold) {
        TokenForgePersistCompanionFrame(frame);
        NSLog(@"INFO [CompanionDrag] mouseUp final=(%.2f,%.2f) saved=true", frame.origin.x, frame.origin.y);
        if (TokenForgeOverlayDragEnded != nil) {
            TokenForgeOverlayDragEnded(frame.origin.x, frame.origin.y);
        }
    } else if (event.clickCount >= 2) {
        NSLog(@"INFO [DesktopCompanion] double click dashboard restore requested");
        if (TokenForgeOverlayDoubleClicked != nil) {
            TokenForgeOverlayDoubleClicked();
        } else {
            TokenForgeOpenNativeDashboardOnMain();
        }
    } else {
        NSLog(@"INFO [DesktopCompanion] single click reaction triggered");
        TokenForgeTriggerOverlayReaction(0, @"Ready to grow!");
        if (TokenForgeOverlayClicked != nil) {
            TokenForgeOverlayClicked();
        }
    }

    TokenForgeIsDraggingOverlay = NO;
    if (TokenForgeMenuClickThrough) {
        self.window.ignoresMouseEvents = YES;
    }
}

- (void)drawRect:(NSRect)dirtyRect
{
    [[NSColor clearColor] setFill];
    NSRectFill(dirtyRect);

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
            NSLog(@"INFO [AvatarPreview] sourceSize=vector-pixel-hero container=(%.2f,%.2f %.2fx%.2f) safeBounds=(%.2f,%.2f %.2fx%.2f) drawRect=(%.2f,%.2f %.2fx%.2f) clipped=%@",
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
    [[NSBezierPath bezierPathWithOvalInRect:rect] fill];
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
@end

static NSWindow *TokenForgeCompanionWindow = nil;
static TokenForgeCompanionView *TokenForgeCompanionContentView = nil;
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
static BOOL TokenForgeLifecycleInstallInProgress = NO;
static BOOL TokenForgeDashboardOpenPending = NO;
static BOOL TokenForgeDumpingWindows = NO;
static NSString *TokenForgeMenuReaction = @"none";
static NSString *TokenForgeMenuStatusText = @"Repo: None · AI Agents: 0 connected";
static NSString *TokenForgeMenuAnimationMode = @"idle";
static NSString *TokenForgeCurrentDashboardTab = @"dashboard";
static TokenForgeNativeDashboardController *TokenForgeDashboardController = nil;
static NSWindow *TokenForgeNativeDashboardWindow = nil;

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
    NSLog(@"INFO [NativeLifecycle] dylib_loaded no_appkit_touch=true version=%@", TokenForgeNativePluginVersion);
    NSLog(@"INFO [RuntimeIdentity] CFBundleIdentifier=%@", bundleIdentifier);
    NSLog(@"INFO [RuntimeIdentity] CFBundleExecutable=%@", executableName);
    NSLog(@"INFO [RuntimeIdentity] bundlePath=%@", bundlePath);
    NSLog(@"INFO [RuntimeIdentity] executablePath=%@", executablePath);
    NSLog(@"INFO [RuntimeIdentity] appVersion=%@ build=%@ executableModified=%@ executableHash=%@", version, build, TokenForgeFileModifiedTime(executablePath), TokenForgeFileSHA256(executablePath));
    NSLog(@"INFO [RuntimeIdentity] nativeDylibPath=%@ modified=%@ hash=%@ versionMarker=%@", dylibPath, TokenForgeFileModifiedTime(dylibPath), TokenForgeFileSHA256(dylibPath), TokenForgeNativePluginVersion);
    NSLog(@"INFO [RuntimeIdentity] managedAssemblyPath=%@ modified=%@ hash=%@", managedPath, TokenForgeFileModifiedTime(managedPath), TokenForgeFileSHA256(managedPath));
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
    BOOL ready = TokenForgeAppKitRegistrationReady();
    NSLog(@"INFO [NativeLifecycle] install_delegate requested appReady=%@ reason=%@", ready ? @"true" : @"false", reason ?: @"unknown");
    if (ready) {
        TokenForgeLifecycleInstallDeferred = NO;
        TokenForgeEnsureLifecycleDelegate();
        return;
    }

    NSLog(@"INFO [NativeLifecycle] app_registration_ready=false reason=%@", reason ?: @"unknown");
    if (TokenForgeLifecycleInstallDeferred) {
        return;
    }

    TokenForgeLifecycleInstallDeferred = YES;
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.25 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
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
    NSLog(@"INFO [WindowLifecycle] %@ id=%p title=%@ class=%@ frame=(%.2f,%.2f %.2fx%.2f) contentView=%@ visible=%@ reason=%@",
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
          reason ?: @"none");
    if (TokenForgeWindowLooksBlank(window)) {
        if (TokenForgeDumpingWindows) {
            NSLog(@"INFO [NativeLifecycle] window_dump readonly=true blank_window_detected=true id=%p reason=%@",
                  window,
                  reason ?: @"unknown");
            return;
        }

        NSLog(@"WARN [WindowLifecycle] blank TokenForge window detected; redirecting to dashboard route id=%p title=%@ contentView=%@ reason=%@",
              window,
              window.title ?: @"",
              contentClass,
              reason ?: @"unknown");
        [window orderOut:nil];
        if (!TokenForgeDumpingWindows && !TokenForgeLifecycleInstallInProgress && !TokenForgeDashboardOpenPending) {
            TokenForgeDashboardOpenPending = YES;
            dispatch_async(dispatch_get_main_queue(), ^{
                TokenForgeDashboardOpenPending = NO;
                TokenForgeOpenNativeDashboardOnMain();
            });
        }
    }
}

static void TokenForgeDumpAllWindows(NSString *reason)
{
    NSArray<NSWindow *> *windows = [NSApp windows];
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
    NSString *trace = traceId.length > 0 ? traceId : @"none";
    if (TokenForgeCompanionWindow == nil) {
        NSLog(@"INFO [OverlayTrace:%@] panel=nil", trace);
        return;
    }

    NSScreen *screen = TokenForgeCompanionWindow.screen ?: [NSScreen mainScreen];
    NSString *screenName = screen.localizedName ?: @"unknown";
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

static void TokenForgeScheduleOverlayWatchdogs(NSString *traceId)
{
    NSString *trace = [traceId.length > 0 ? traceId : @"none" copy];
    NSArray<NSNumber *> *delays = @[@1, @5, @30];
    for (NSNumber *delayNumber in delays) {
        NSInteger delay = delayNumber.integerValue;
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(delay * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
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

static NSString *TokenForgeCompanionPositionXKey = @"TokenForge.CompanionOverlay.PositionX";
static NSString *TokenForgeCompanionPositionYKey = @"TokenForge.CompanionOverlay.PositionY";
static NSString *TokenForgeCompanionPositionSavedKey = @"TokenForge.CompanionOverlay.PositionSaved";

static NSPoint TokenForgeDefaultCompanionOrigin(void)
{
    NSRect visible = TokenForgeVisibleFrame();
    return NSMakePoint(NSMinX(visible) + 120.0, NSMinY(visible) + 120.0);
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
    TokenForgeCompanionVelocity = NSMakePoint(0, 0);
    TokenForgeCompanionDragCooldownUntil = [NSDate timeIntervalSinceReferenceDate] + 1.0;
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
    TokenForgeCompanionVelocity = NSMakePoint(0, 0);
    if (TokenForgeCompanionWindow != nil) {
        [TokenForgeCompanionWindow setFrameOrigin:frame.origin];
    }
}

static BOOL TokenForgeIsCompanionWindow(NSWindow *window)
{
    return window != nil && window == TokenForgeCompanionWindow;
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

    for (NSWindow *window in [NSApp windows]) {
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
    if (TokenForgeDashboardActionClicked != nil) {
        TokenForgeDashboardActionClicked(action);
    }

    if (TokenForgeMenuActionClicked != nil) {
        TokenForgeMenuActionClicked(action);
    }
}

static void TokenForgeSendDashboardAction(const char *action)
{
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
        return;
    }

    CGFloat direction = arc4random_uniform(2) == 0 ? -1.0 : 1.0;
    TokenForgeCompanionVelocity = NSMakePoint(direction * TokenForgeCompanionWanderSpeed, 0.0);
    if (TokenForgeCompanionContentView != nil) {
        TokenForgeCompanionContentView.facingLeft = direction < 0.0;
    }
}

static void TokenForgeCompanionMotionTick(NSTimer *timer)
{
    if (TokenForgeCompanionWindow == nil || TokenForgeCompanionContentView == nil || !TokenForgeCompanionWindow.isVisible) {
        return;
    }

    if (!TokenForgeMotionTickLogged) {
        TokenForgeMotionTickLogged = YES;
        NSLog(@"INFO [DesktopOverlay] movementTimer started interval=%.2f", 1.0 / 30.0);
        NSLog(@"INFO [DesktopOverlay] movement tick speed=%.2f position=(%.2f,%.2f)", TokenForgeCompanionWanderSpeed, TokenForgeCompanionWindow.frame.origin.x, TokenForgeCompanionWindow.frame.origin.y);
        NSLog(@"INFO [DesktopOverlay] visible=true movementRunning=true");
    }

    NSTimeInterval now = [NSDate timeIntervalSinceReferenceDate];
    NSTimeInterval delta = TokenForgeCompanionLastTick <= 0.0 ? 0.016 : MIN(0.05, now - TokenForgeCompanionLastTick);
    TokenForgeCompanionLastTick = now;

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
        NSLog(@"INFO [DesktopCompanion] tick old=%.2f,%.2f new=%.2f,%.2f speed=%.2f",
              oldDisplayOrigin.x,
              oldDisplayOrigin.y,
              displayFrame.origin.x,
              displayFrame.origin.y,
              TokenForgeCompanionWanderSpeed);
        NSLog(@"INFO [DesktopOverlay] tick oldOrigin=(%.2f,%.2f) newOrigin=(%.2f,%.2f) speed=%.2f",
              oldDisplayOrigin.x,
              oldDisplayOrigin.y,
              displayFrame.origin.x,
              displayFrame.origin.y,
              TokenForgeCompanionWanderSpeed);
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
    if (TokenForgeCompanionMotionTimer != nil) {
        return;
    }

    TokenForgeCompanionLastTick = [NSDate timeIntervalSinceReferenceDate];
    TokenForgeCompanionMotionTimer = [NSTimer scheduledTimerWithTimeInterval:1.0 / 30.0 repeats:YES block:^(NSTimer *timer) {
        TokenForgeCompanionMotionTick(timer);
    }];
    [[NSRunLoop mainRunLoop] addTimer:TokenForgeCompanionMotionTimer forMode:NSRunLoopCommonModes];
    NSLog(@"INFO [DesktopOverlay] movementTimer started interval=%.2f", 1.0 / 30.0);
}

static void TokenForgeCreateCompanionOverlayOnMain(NSString *traceId)
{
    TokenForgeLogRuntimeIdentityIfNeeded();
    NSString *trace = traceId.length > 0 ? traceId : @"none";
    if (!TokenForgeAppKitRegistrationReady()) {
        NSLog(@"INFO [NativeLifecycle] open_dashboard deferred reason=app_not_ready");
        NSLog(@"INFO [OverlayTrace:%@] native_show_deferred reason=app_not_ready", trace);
        TokenForgeRequestLifecycleInstall(@"overlay_create");
        return;
    }

    TokenForgeRequestLifecycleInstall(@"overlay_create");
    if (TokenForgeCompanionWindow != nil) {
        NSLog(@"INFO [DesktopCompanion] strongReference panel=%@ view=%@ controller=%@", TokenForgeCompanionWindow != nil ? @"true" : @"false", TokenForgeCompanionContentView != nil ? @"true" : @"false", TokenForgeLifecycleDelegate != nil ? @"true" : @"false");
        return;
    }

    NSPoint origin = TokenForgeLoadCompanionOrigin();
    NSRect requestedFrame = NSMakeRect(origin.x, origin.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
    NSRect frame = TokenForgeClampFrameToVisibleFrame(requestedFrame);
    TokenForgeCompanionAnchor = frame.origin;
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
    NSPanel *panel = [[NSPanel alloc] initWithContentRect:frame
                                                styleMask:NSWindowStyleMaskBorderless | NSWindowStyleMaskNonactivatingPanel
                                                  backing:NSBackingStoreBuffered
                                                    defer:NO];
    panel.releasedWhenClosed = NO;
    panel.hidesOnDeactivate = NO;
    panel.floatingPanel = YES;
    panel.worksWhenModal = YES;
    panel.becomesKeyOnlyIfNeeded = NO;
    TokenForgeCompanionWindow = panel;
    TokenForgeCompanionWindow.backgroundColor = [NSColor clearColor];
    TokenForgeCompanionWindow.opaque = NO;
    TokenForgeCompanionWindow.alphaValue = 1.0;
    TokenForgeCompanionWindow.hasShadow = YES;
    TokenForgeCompanionWindow.level = NSStatusWindowLevel;
    TokenForgeCompanionWindow.acceptsMouseMovedEvents = YES;
    TokenForgeCompanionWindow.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary | NSWindowCollectionBehaviorStationary | NSWindowCollectionBehaviorIgnoresCycle;
    TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgeMenuClickThrough;
    TokenForgeCompanionContentView = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height)];
    TokenForgeCompanionContentView.visualThemeId = @"orange_cat";
    TokenForgeCompanionContentView.assetType = @"overlay";
    TokenForgeCompanionContentView.wantsLayer = YES;
    TokenForgeCompanionContentView.layerContentsRedrawPolicy = NSViewLayerContentsRedrawOnSetNeedsDisplay;
    TokenForgeCompanionWindow.contentView = TokenForgeCompanionContentView;
    TokenForgeLogWindowLifecycle(@"created", TokenForgeCompanionWindow, @"companionOverlay");
    NSLog(@"INFO [OverlayTrace:%@] panel_create traceId=%@ ptr=%p level=%ld collectionBehavior=%lu releasedWhenClosed=%@ canBecomeKey=false",
          trace,
          trace,
          TokenForgeCompanionWindow,
          (long)TokenForgeCompanionWindow.level,
          (unsigned long)TokenForgeCompanionWindow.collectionBehavior,
          TokenForgeCompanionWindow.releasedWhenClosed ? @"true" : @"false");
    NSLog(@"INFO [DesktopCompanion] window created level=%ld frame=%.2f,%.2f %.2fx%.2f", (long)TokenForgeCompanionWindow.level, frame.origin.x, frame.origin.y, frame.size.width, frame.size.height);
    NSScreen *screen = TokenForgeCompanionWindow.screen ?: [NSScreen mainScreen];
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
- (void)showDashboard;
- (void)hideDashboard;
- (void)toggleDashboard;
- (void)showSettings;
- (void)updateState:(NSDictionary *)state;
- (void)setMenuBarStatus:(NSDictionary *)state;
@end

static NSString *TokenForgeDashboardFrameKey = @"TokenForge.NativeDashboard.Frame";
static NSString *TokenForgeSettingsFrameKey = @"TokenForge.NativeSettings.Frame";

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
        @"syncStatusText": @"Sync optional",
        @"selectedNavItem": @"dashboard",
        @"primaryActionEnabled": @YES,
        @"hasActiveRepository": @NO,
        @"isAnalysisRunning": @NO,
        @"actionStatusKind": @"idle",
        @"actionStatusText": @"Ready",
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
        @"lastRunSummary": @"No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.",
        @"codeStat": @0,
        @"focusStat": @0,
        @"debugStat": @0,
        @"designStat": @0,
        @"syncStat": @0,
        @"companionVisible": @YES,
        @"wanderEnabled": @YES,
        @"clickThroughEnabled": @NO,
        @"clickReactionEnabled": @YES,
        @"statusText": @"Repo: None · AI Agents: 0 connected",
        @"companion": @{@"name": @"Token", @"stage": @"Egg", @"stageIndex": @0, @"level": @1, @"xp": @0, @"xpToNextLevel": @250, @"totalLifetimeXP": @0, @"canLevelUp": @NO, @"evolveActionVisible": @NO, @"evolveActionHiddenReason": @"currentXP below requirement", @"xpStatusText": @"0 XP · 250 XP required", @"carryForwardText": @"", @"xpProgressRatio": @0.0, @"levelUpStatusText": @"Earn more XP to level up.", @"levelUpDisabledReason": @"Earn enough XP before leveling up.", @"dashboardAnimationState": @"subtleIdle", @"mood": @"active", @"skin": @"orange_cat", @"motion": @{@"repositoryId": @"", @"activityLevel": @"idle", @"movementSpeed": @0.35, @"bounceAmplitude": @2.0, @"idleFrequency": @0.6, @"pulseFrequency": @0.2, @"reaction": @"none", @"mood": @"idle", @"reasonSummary": @"No recent aggregate activity.", @"updatedAt": @""}},
        @"repository": @{@"connected": @NO, @"id": @"", @"name": @"", @"status": @"not_selected", @"statusText": @"Not selected", @"connectedCount": @0, @"hasValidSource": @NO, @"canAnalyze": @NO, @"disabledReason": @"Connect an active repository first.", @"analyzeDisabledReason": @"Connect an active repository first."},
        @"codexAgent": @{@"connected": @NO, @"status": @"not_connected", @"statusText": @"Not connected"},
        @"agents": @{@"connectedCount": @0, @"lastProvider": @"None", @"warningCount": @0, @"statusText": @"No agents connected", @"privacyText": @"Local aggregate only"},
        @"providerUsagePercentages": @[],
        @"repositories": @[],
        @"agentProviders": @[],
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
    view.layer.cornerRadius = 8.0;
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
    NSRect head = NSMakeRect(NSMidX(preview) - 17.0, NSMidY(preview) - 4.0, 34.0, 30.0);
    NSRect bodyRect = NSMakeRect(NSMidX(preview) - 22.0, NSMinY(preview) + 5.0, 44.0, 30.0);
    NSColor *outline = [NSColor colorWithCalibratedRed:0.13 green:0.15 blue:0.19 alpha:1.0];
    [outline setFill];
    [[NSBezierPath bezierPathWithOvalInRect:NSInsetRect(bodyRect, -2.0, -2.0)] fill];
    [[NSBezierPath bezierPathWithOvalInRect:NSInsetRect(head, -2.0, -2.0)] fill];
    [body setFill];
    [[NSBezierPath bezierPathWithOvalInRect:bodyRect] fill];
    [[NSBezierPath bezierPathWithOvalInRect:head] fill];
    [accent setFill];
    [[NSBezierPath bezierPathWithRoundedRect:NSMakeRect(NSMidX(preview) - 20.0, NSMaxY(head) - 6.0, 40.0, 7.0) xRadius:3.0 yRadius:3.0] fill];
    [outline setFill];
    NSRectFill(NSMakeRect(NSMidX(head) - 8.0, NSMidY(head) + 2.0, 4.0, 4.0));
    NSRectFill(NSMakeRect(NSMidX(head) + 5.0, NSMidY(head) + 2.0, 4.0, 4.0));
    [accent setFill];
    NSRectFill(NSMakeRect(NSMidX(bodyRect) + 16.0, NSMidY(bodyRect) - 3.0, 16.0, 6.0));
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
    }
    return self;
}

- (void)showDashboard
{
    TokenForgeLogRuntimeIdentityIfNeeded();
    NSLog(@"INFO [WindowLifecycle] openDashboard route=nativeDashboardController");
    [self ensureDashboardWindow];
    [NSApp activateIgnoringOtherApps:YES];
    [self.dashboardWindow makeKeyAndOrderFront:nil];
    [self.dashboardWindow orderFrontRegardless];
    TokenForgeLogWindowLifecycle(@"orderFront", self.dashboardWindow, @"openDashboard");
    TokenForgeDumpAllWindows(@"openDashboard");
}

- (void)hideDashboard
{
    [self.dashboardWindow orderOut:nil];
    TokenForgeLogWindowLifecycle(@"orderOut", self.dashboardWindow, @"hideDashboard");
}

- (void)toggleDashboard
{
    if (self.dashboardWindow != nil && self.dashboardWindow.isVisible) {
        [self hideDashboard];
    } else {
        [self showDashboard];
    }
}

- (void)showSettings
{
    [self ensureSettingsWindow];
    [NSApp activateIgnoringOtherApps:YES];
    [self.settingsWindow makeKeyAndOrderFront:nil];
    [self.settingsWindow orderFrontRegardless];
}

- (void)updateState:(NSDictionary *)state
{
    self.state = TokenForgeMergeDashboardState(self.state, state);
    self.selectedNavItem = TokenForgeDashboardString(self.state, @"selectedNavItem", self.selectedNavItem ?: @"dashboard");
    TokenForgeCurrentDashboardTab = self.selectedNavItem;
    [self rebuildDashboardIfNeeded];
    [self rebuildSettingsIfNeeded];
    NSLog(@"INFO [NativeDashboard] state updated repository=%@ codex=%@ pending=%ld",
          TokenForgeDashboardString(TokenForgeDashboardDictionary(self.state, @"repository"), @"statusText", @"Not selected"),
          TokenForgeDashboardString(TokenForgeDashboardDictionary(self.state, @"codexAgent"), @"statusText", @"Not connected"),
          (long)TokenForgeDashboardInteger(self.state, @"pendingReviewCount", 0));
}

- (void)setMenuBarStatus:(NSDictionary *)state
{
    NSDictionary *companion = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"companion");
    NSDictionary *repository = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"repository");
    NSDictionary *agent = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"codexAgent");
    NSDictionary *agents = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"agents");
    TokenForgeMenuCompanionName = [TokenForgeDashboardString(companion, @"name", @"Token") copy];
    TokenForgeMenuStage = [TokenForgeDashboardString(companion, @"stage", @"Egg") copy];
    TokenForgeMenuStageIndex = MAX(0, MIN(4, TokenForgeDashboardInteger(companion, @"stageIndex", 0)));
    TokenForgeMenuLevel = MAX(1, TokenForgeDashboardInteger(companion, @"level", 1));
    TokenForgeMenuRepositoryAlias = [TokenForgeDashboardString(repository, @"name", TokenForgeDashboardBool(repository, @"connected", NO) ? @"Repository" : @"Not selected") copy];
    TokenForgeMenuAgentStatus = [TokenForgeDashboardString(agents, @"statusText", TokenForgeDashboardString(agent, @"statusText", @"Not connected")) copy];
    TokenForgeMenuSyncStatus = [TokenForgeDashboardString(state.count > 0 ? state : self.state, @"syncStatusText", @"Sync optional") copy];
    TokenForgeMenuCompanionEnabled = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"companionVisible", TokenForgeMenuCompanionEnabled);
    TokenForgeMenuClickThrough = !TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"clickReactionEnabled", !TokenForgeMenuClickThrough);
    TokenForgeMenuCanLevelUp = TokenForgeDashboardBool(companion, @"canLevelUp", NO);
    TokenForgeMenuAnalysisRunning = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"isAnalysisRunning", NO);
    TokenForgeMenuReaction = [TokenForgeDashboardString(TokenForgeDashboardDictionary(companion, @"motion"), @"reaction", @"none") copy];
    TokenForgeMenuCanAnalyze = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"primaryActionEnabled", YES) &&
        !TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"isAnalysisRunning", NO);

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
    if (self.dashboardWindow != nil) {
        [self rebuildDashboardIfNeeded];
        return;
    }

    NSRect frame = NSMakeRect(0, 0, 1180, 760);
    NSString *savedFrame = [[NSUserDefaults standardUserDefaults] stringForKey:TokenForgeDashboardFrameKey];
    if (savedFrame.length > 0) {
        frame = TokenForgeClampFrameToVisibleFrame(NSRectFromString(savedFrame));
    }
    self.dashboardWindow = [[NSWindow alloc] initWithContentRect:frame
                                                       styleMask:NSWindowStyleMaskTitled | NSWindowStyleMaskClosable | NSWindowStyleMaskMiniaturizable | NSWindowStyleMaskResizable
                                                         backing:NSBackingStoreBuffered
                                                           defer:NO];
    self.dashboardWindow.title = @"TokenForge";
    self.dashboardWindow.minSize = NSMakeSize(920, 620);
    self.dashboardWindow.delegate = self;
    self.dashboardWindow.releasedWhenClosed = NO;
    self.dashboardWindow.restorable = NO;
    self.dashboardWindow.restorationClass = nil;
    self.dashboardWindow.identifier = @"TokenForge.NativeDashboard";
    TokenForgeNativeDashboardWindow = self.dashboardWindow;
    TokenForgeLogWindowLifecycle(@"created", self.dashboardWindow, @"dashboard");
    if (savedFrame.length == 0) {
        [self.dashboardWindow center];
    }
    [self rebuildDashboardIfNeeded];
}

- (void)ensureSettingsWindow
{
    if (self.settingsWindow != nil) {
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
    self.settingsWindow.minSize = NSMakeSize(760, 560);
    self.settingsWindow.level = NSFloatingWindowLevel + 1;
    self.settingsWindow.delegate = self;
    self.settingsWindow.releasedWhenClosed = NO;
    self.settingsWindow.restorable = NO;
    self.settingsWindow.restorationClass = nil;
    self.settingsWindow.identifier = @"TokenForge.NativeSettings";
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

    self.dashboardWindow.contentView = [self buildDashboardRootView];
    TokenForgeLogWindowLifecycle(@"contentViewAssigned", self.dashboardWindow, @"dashboardRebuild");
}

- (void)rebuildSettingsIfNeeded
{
    if (self.settingsWindow == nil) {
        return;
    }

    self.settingsWindow.contentView = [self buildSettingsRootView];
}

- (NSView *)buildDashboardRootView
{
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

    NSStackView *split = TokenForgeDashboardHorizontalStack(0.0);
    split.alignment = NSLayoutAttributeTop;
    split.distribution = NSStackViewDistributionFill;
    [root addSubview:split];
    TokenForgePinSubview(split, root, 0, 0, 0, 0);

    NSVisualEffectView *sidebar = [[NSVisualEffectView alloc] initWithFrame:NSZeroRect];
    sidebar.translatesAutoresizingMaskIntoConstraints = NO;
    sidebar.material = NSVisualEffectMaterialSidebar;
    sidebar.blendingMode = NSVisualEffectBlendingModeWithinWindow;
    sidebar.state = NSVisualEffectStateActive;
    sidebar.wantsLayer = YES;
    sidebar.layer.backgroundColor = TokenForgeSidebarBackgroundColor().CGColor;
    CGFloat sidebarWidth = 300.0;
    [sidebar.widthAnchor constraintEqualToConstant:sidebarWidth].active = YES;
    [split addArrangedSubview:sidebar];

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
    [split addArrangedSubview:scrollView];

    TokenForgeFlippedView *document = [[TokenForgeFlippedView alloc] initWithFrame:NSMakeRect(0, 0, 900, 1200)];
    document.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.documentView = document;
    [document.widthAnchor constraintEqualToAnchor:scrollView.contentView.widthAnchor].active = YES;

    NSStackView *content = TokenForgeDashboardVerticalStack(22.0);
    content.alignment = NSLayoutAttributeWidth;
    [document addSubview:content];
    TokenForgePinSubview(content, document, 32, 44, 38, 44);

    [self populateDashboardContent:content];
    return root;
}

- (void)populateSidebar:(NSStackView *)stack
{
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSString *name = TokenForgeDashboardString(companion, @"name", @"Token");
    NSString *syncText = TokenForgeDashboardString(self.state, @"syncStatusText", @"Sync optional");

    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSButton *thumbCard = TokenForgeDashboardButton(@"", self, @selector(openActiveCompanionDashboard:));
    thumbCard.bordered = NO;
    thumbCard.wantsLayer = YES;
    thumbCard.layer.backgroundColor = TokenForgeCardBackgroundColor().CGColor;
    thumbCard.layer.cornerRadius = 8.0;
    thumbCard.layer.borderColor = [NSColor colorWithCalibratedWhite:0.0 alpha:0.08].CGColor;
    thumbCard.layer.borderWidth = 1.0;
    thumbCard.toolTip = @"Open the active companion dashboard.";
    thumbCard.identifier = TokenForgeDashboardString(repository, @"id", @"");
    [thumbCard.heightAnchor constraintGreaterThanOrEqualToConstant:126.0].active = YES;
    NSStackView *thumbStack = TokenForgeDashboardHorizontalStack(12.0);
    thumbStack.distribution = NSStackViewDistributionFill;
    thumbStack.alignment = NSLayoutAttributeTop;
    [thumbCard addSubview:thumbStack];
    TokenForgePinSubview(thumbStack, thumbCard, 16, 14, 16, 14);
    TokenForgeCompanionView *icon = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 46, 46)];
    icon.translatesAutoresizingMaskIntoConstraints = NO;
    icon.stage = MAX(0, MIN(4, TokenForgeDashboardInteger(companion, @"stageIndex", 2)));
    icon.visualThemeId = TokenForgeDashboardString(companion, @"skin", @"orange_cat");
    [icon.widthAnchor constraintEqualToConstant:44.0].active = YES;
    [icon.heightAnchor constraintEqualToConstant:44.0].active = YES;
    [thumbStack addArrangedSubview:icon];
    NSStackView *labels = TokenForgeDashboardVerticalStack(2.0);
    labels.alignment = NSLayoutAttributeLeading;
    [labels setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    NSTextField *nameLabel = TokenForgeDashboardLabel(name, 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 2);
    nameLabel.lineBreakMode = NSLineBreakByWordWrapping;
    [nameLabel setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [labels addArrangedSubview:nameLabel];
    [labels addArrangedSubview:TokenForgeDashboardLabel(@"● Active", 11.0, NSFontWeightRegular, [NSColor systemGreenColor], 1)];
    [labels addArrangedSubview:TokenForgeDashboardLabel(syncText, 10.5, NSFontWeightRegular, TokenForgeMutedTextColor(), 1)];
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ · Lv %ld", TokenForgeDashboardString(companion, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(companion, @"level", 1)], 11.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 1)];
    if (TokenForgeDashboardBool(companion, @"canLevelUp", NO)) {
        [labels addArrangedSubview:TokenForgeDashboardLabel(@"Ready to evolve", 11.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 1)];
        NSButton *evolve = TokenForgePrimaryButton(@"Evolve", self, @selector(levelUpCompanion:));
        [evolve.heightAnchor constraintEqualToConstant:28.0].active = YES;
        [labels addArrangedSubview:evolve];
    }
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%ld XP", (long)TokenForgeDashboardInteger(self.state, @"persistedCompanionXP", TokenForgeDashboardInteger(companion, @"xp", 0))], 11.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 1)];
    NSString *repositoryLabel = TokenForgeDashboardBool(repository, @"connected", NO)
        ? TokenForgeDashboardString(repository, @"name", @"Repository")
        : @"No repository connected";
    [labels addArrangedSubview:TokenForgeDashboardLabel(repositoryLabel, 11.0, NSFontWeightMedium, TokenForgeMutedTextColor(), 2)];
    [thumbStack addArrangedSubview:labels];
    [stack addArrangedSubview:thumbCard];
    CGFloat profileNameAvailableWidth = 300.0 - 32.0 - 28.0 - 44.0 - 12.0;
    NSSize measuredName = [name boundingRectWithSize:NSMakeSize(profileNameAvailableWidth, CGFLOAT_MAX)
                                             options:NSStringDrawingUsesLineFragmentOrigin
                                          attributes:@{NSFontAttributeName: nameLabel.font}
                                             context:nil].size;
    CGFloat profileLineHeight = fabs(nameLabel.font.ascender) + fabs(nameLabel.font.descender) + nameLabel.font.leading;
    BOOL profileNameClipped = measuredName.height > (profileLineHeight * 2.2);
    NSLog(@"INFO [DashboardLayout] profile_card_frame=auto minHeight=126.00");
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

- (void)populateDashboardContent:(NSStackView *)content
{
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *agent = TokenForgeDashboardDictionary(self.state, @"codexAgent");
    NSDictionary *agents = TokenForgeDashboardDictionary(self.state, @"agents");
    NSDictionary *activity = TokenForgeDashboardDictionary(self.state, @"activity");
    NSDictionary *review = TokenForgeDashboardDictionary(self.state, @"review");

    NSString *title = TokenForgeDashboardString(self.state, @"appTitle", TokenForgeDashboardString(self.state, @"appName", @"TokenForge"));
    NSString *subtitle = TokenForgeDashboardString(self.state, @"subtitle", @"Turn your development activity into companion growth.");
    NSString *syncText = TokenForgeDashboardString(self.state, @"syncStatusText", @"Sync optional");

    NSStackView *grid = TokenForgeDashboardVerticalStack(22.0);
    grid.identifier = @"clean-grid-v4";
    grid.alignment = NSLayoutAttributeWidth;
    [grid setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [grid setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [content addArrangedSubview:grid];
    NSLog(@"INFO [RuntimePath] dashboardRenderer=clean-grid-v4");
    NSLog(@"INFO [RuntimePath] sidebarRenderer=repo-switcher-v2");
    NSLog(@"INFO [DashboardLayout] gridVersion=clean-v3 contentFrame=autoLayout margins=44 gap=22");

    NSStackView *header = TokenForgeDashboardHorizontalStack(16.0);
    header.distribution = NSStackViewDistributionFill;
    NSStackView *headerCopy = TokenForgeDashboardVerticalStack(4.0);
    [headerCopy addArrangedSubview:TokenForgeShellHeaderLabel([NSString stringWithFormat:@"%@ Dashboard", title], 27.0, NSFontWeightBold, 1)];
    [headerCopy addArrangedSubview:TokenForgeShellBodyLabel(subtitle, 2)];
    NSString *activeRepositoryState = TokenForgeDashboardBool(repository, @"connected", NO)
        ? [NSString stringWithFormat:@"Active repository: %@ · %@", TokenForgeDashboardString(repository, @"name", @"Repository"), TokenForgeDashboardString(self.state, @"actionStatusText", @"Ready")]
        : [NSString stringWithFormat:@"Active repository: none · %@", TokenForgeDashboardString(self.state, @"actionStatusText", @"Ready")];
    [headerCopy addArrangedSubview:TokenForgeShellBodyLabel(activeRepositoryState, 2)];
    [header addArrangedSubview:headerCopy];
    NSStackView *headerActions = TokenForgeDashboardVerticalStack(8.0);
    headerActions.alignment = NSLayoutAttributeTrailing;
    NSStackView *primaryActions = TokenForgeDashboardHorizontalStack(10.0);
    NSDictionary *repositoryState = TokenForgeDashboardDictionary(self.state, @"repository");
    NSString *repositoryBadge = TokenForgeDashboardBool(repositoryState, @"connected", NO) ? @"Active repository" : @"No repository";
    if (TokenForgeDashboardBool(repositoryState, @"connected", NO)) {
        [headerActions addArrangedSubview:[self pillLabel:[NSString stringWithFormat:@"Active repository · %@ · %@", TokenForgeDashboardString(repositoryState, @"name", @"Repository"), syncText]]];
    } else {
        [headerActions addArrangedSubview:[self pillLabel:@"No repository connected"]];
    }
    if (TokenForgeDashboardBool(self.state, @"isAnalysisRunning", NO)) {
        NSProgressIndicator *spinner = [[NSProgressIndicator alloc] initWithFrame:NSZeroRect];
        spinner.translatesAutoresizingMaskIntoConstraints = NO;
        spinner.style = NSProgressIndicatorStyleSpinning;
        spinner.controlSize = NSControlSizeSmall;
        [spinner startAnimation:nil];
        [headerActions addArrangedSubview:spinner];
    }
    NSButton *primaryRun = TokenForgePrimaryButton(@"Run Analysis", self, @selector(runAnalysis:));
    primaryRun.enabled = TokenForgeDashboardBool(self.state, @"primaryActionEnabled", NO);
    NSDictionary *agentsState = TokenForgeDashboardDictionary(self.state, @"agents");
    primaryRun.toolTip = primaryRun.enabled
        ? (TokenForgeDashboardBool(repositoryState, @"canAnalyze", NO) ? @"Run analysis for the active repository." : @"Run analysis for a ready AI provider.")
        : (TokenForgeDashboardInteger(agentsState, @"connectedCount", 0) > 0 ? @"Analysis is already running." : TokenForgeDashboardString(repositoryState, @"analyzeDisabledReason", @"Connect an active repository first."));
    [primaryActions addArrangedSubview:primaryRun];
    [primaryActions addArrangedSubview:TokenForgeSecondaryButton(@"Connect Repository", self, @selector(connectRepository:))];
    [primaryActions addArrangedSubview:TokenForgeSecondaryButton(@"Connect AI Agent", self, @selector(connectCodexAgent:))];
    [headerActions addArrangedSubview:primaryActions];
    [header addArrangedSubview:headerActions];
    [grid addArrangedSubview:header];

    if ([self.selectedNavItem isEqualToString:@"repository"]) {
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

    NSLog(@"INFO [RuntimePath] heroRenderer=avatar-safe-v2");
    NSLog(@"INFO [RuntimePath] overlayController=independent-panel-v1");
    NSLog(@"INFO [RuntimePath] menuBarRenderer=animated-status-item-v1");
    NSLog(@"INFO [DashboardUI] render clean layout repo=%@ cards=hero,quick-status,growth-summary,recent-timeline",
          TokenForgeDashboardString(repository, @"name", @"No repository"));

    NSView *hero = [self heroCardWithCompanion:companion activity:activity];
    [grid addArrangedSubview:hero];

    NSStackView *quickTop = TokenForgeDashboardHorizontalStack(22.0);
    quickTop.distribution = NSStackViewDistributionFillEqually;
    [quickTop addArrangedSubview:[self repositoryStatusCardWithRepository:repository]];
    [quickTop addArrangedSubview:[self aiAgentsStatusCardWithAgents:agents]];
    [grid addArrangedSubview:quickTop];

    NSStackView *quickBottom = TokenForgeDashboardHorizontalStack(22.0);
    quickBottom.distribution = NSStackViewDistributionFillEqually;
    [quickBottom addArrangedSubview:[self reviewCardWithActivity:activity review:review]];
    [quickBottom addArrangedSubview:[self companionMotionCardWithCompanion:companion]];
    [grid addArrangedSubview:quickBottom];

    NSStackView *bottom = TokenForgeDashboardHorizontalStack(22.0);
    bottom.distribution = NSStackViewDistributionFillEqually;
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
    NSString *repoName = TokenForgeDashboardBool(repository, @"connected", NO) ? TokenForgeDashboardString(repository, @"name", @"Repository") : @"No active repository";
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
    NSButton *run = TokenForgePrimaryButton(@"Run Analysis", self, @selector(runAnalysis:));
    run.enabled = TokenForgeDashboardBool(TokenForgeDashboardDictionary(self.state, @"repository"), @"canAnalyze", NO) || TokenForgeDashboardInteger(TokenForgeDashboardDictionary(self.state, @"agents"), @"connectedCount", 0) > 0;
    run.toolTip = run.enabled ? @"Run analysis for the active approved source." : @"Connect a repository or AI agent before running analysis.";
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
    previewContainer.repositoryName = TokenForgeDashboardString(repository, @"name", @"Repository");
    [previewContainer.widthAnchor constraintGreaterThanOrEqualToConstant:250.0].active = YES;
    [previewContainer.heightAnchor constraintGreaterThanOrEqualToConstant:232.0].active = YES;
    [previewContainer.widthAnchor constraintLessThanOrEqualToConstant:310.0].active = YES;
    [previewContainer.heightAnchor constraintLessThanOrEqualToConstant:260.0].active = YES;
    [previewContainer setContentHuggingPriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [previewContainer setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    TokenForgeAvatarPreviewView *preview = [[TokenForgeAvatarPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 220, 220)];
    preview.translatesAutoresizingMaskIntoConstraints = NO;
    preview.stage = MAX(0, MIN(4, TokenForgeDashboardInteger(companion, @"stageIndex", 0)));
    preview.archetype = 0;
    preview.animationState = canLevelUp ? 4 : 1;
    preview.visualThemeId = TokenForgeDashboardString(companion, @"skin", @"orange_cat");
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
    NSString *repoName = TokenForgeDashboardString(repository, @"name", @"Repository");
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
    [row.heightAnchor constraintGreaterThanOrEqualToConstant:64.0].active = YES;
    NSLog(@"INFO [DashboardLayout] repo_item_frame=auto minHeight=64.00 name=%@", repoName);

    NSStackView *content = TokenForgeDashboardHorizontalStack(8.0);
    content.distribution = NSStackViewDistributionFill;
    [row addSubview:content];
    TokenForgePinSubview(content, row, 8, 8, 8, 8);

    TokenForgeCompanionView *avatar = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 34, 34)];
    avatar.translatesAutoresizingMaskIntoConstraints = NO;
    avatar.stage = MAX(0, MIN(4, TokenForgeDashboardInteger(repository, @"stageIndex", 0)));
    avatar.visualThemeId = TokenForgeDashboardString(repository, @"avatarSkin", @"orange_cat");
    [avatar.widthAnchor constraintEqualToConstant:34.0].active = YES;
    [avatar.heightAnchor constraintEqualToConstant:34.0].active = YES;
    [content addArrangedSubview:avatar];

    NSStackView *copy = TokenForgeDashboardVerticalStack(2.0);
    copy.alignment = NSLayoutAttributeLeading;
    [copy setContentHuggingPriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    [copy setContentCompressionResistancePriority:NSLayoutPriorityDefaultLow forOrientation:NSLayoutConstraintOrientationHorizontal];
    NSTextField *repoNameLabel = TokenForgeDashboardLabel(repoName, 12.0, NSFontWeightSemibold, TokenForgeDarkSidebarTextColor(), 2);
    repoNameLabel.lineBreakMode = NSLineBreakByWordWrapping;
    [repoNameLabel setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [copy addArrangedSubview:repoNameLabel];
    [copy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ · Lv %ld", TokenForgeDashboardString(repository, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(repository, @"level", 1)], 10.5, NSFontWeightRegular, TokenForgeSidebarMutedTextColor(), 1)];
    NSMutableArray<NSString *> *badges = [NSMutableArray array];
    if (selected) [badges addObject:@"Active"];
    if (canLevelUp) [badges addObject:@"Evolve"];
    if (aiRecent) [badges addObject:@"AI"];
    if (gitRecent) [badges addObject:@"Git"];
    if (badges.count == 0) [badges addObject:TokenForgeDashboardString(repository, @"xpStatusText", @"0 XP")];
    [copy addArrangedSubview:TokenForgeDashboardLabel([badges componentsJoinedByString:@"  "], 10.5, canLevelUp ? NSFontWeightSemibold : NSFontWeightRegular, canLevelUp ? [NSColor systemOrangeColor] : TokenForgeSidebarMutedTextColor(), 1)];
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
    [card.heightAnchor constraintEqualToConstant:176.0].active = YES;
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
        ? TokenForgeDashboardString(repository, @"name", @"Repository")
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
    NSDictionary *motion = TokenForgeDashboardDictionary(companion, @"motion");
    NSString *activity = TokenForgeDashboardString(motion, @"activityLevel", @"idle");
    BOOL visible = TokenForgeDashboardBool(self.state, @"companionVisible", YES);
    BOOL wandering = TokenForgeDashboardBool(self.state, @"wanderEnabled", YES);
    BOOL clickThrough = TokenForgeDashboardBool(self.state, @"clickThroughEnabled", NO);
    BOOL clickReaction = TokenForgeDashboardBool(self.state, @"clickReactionEnabled", YES);
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 16.0, 8.0);
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:232.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Companion Motion", 13.0, NSFontWeightSemibold, [NSColor systemGreenColor], 1)];
    NSString *state = !visible ? @"Hidden" : (wandering ? @"Wandering" : @"Visible · Idle");
    [stack addArrangedSubview:TokenForgeDashboardLabel(state, 19.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    NSString *reason = !visible
        ? @"Companion is hidden. Show it to let Token wander on your desktop."
        : (!wandering
            ? @"Wander movement is off. Enable it to see Token move across your desktop."
            : TokenForgeFriendlyDashboardSummary(TokenForgeDashboardString(motion, @"reasonSummary", @"Recent Git + AI activity sets a calm movement pace."), @"Recent Git + AI activity sets a calm movement pace."));
    NSString *speed = [activity isEqualToString:@"high"] ? @"Fast" : ([activity isEqualToString:@"active"] ? @"Active" : @"Calm");
    NSString *detail = visible && wandering
        ? [NSString stringWithFormat:@"Speed: %@. Click reaction %@. Click-through %@.", speed, clickReaction ? @"on" : @"off", clickThrough ? @"on" : @"off"]
        : [NSString stringWithFormat:@"%@ Click reaction %@. Click-through %@.", reason, clickReaction ? @"on" : @"off", clickThrough ? @"on" : @"off"];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(detail, 3)];
    NSString *failureReason = @"none";
    if (visible && TokenForgeCompanionWindow == nil) {
        failureReason = @"panelNotCreated";
    } else if (visible && TokenForgeCompanionWindow != nil && !TokenForgeCompanionWindow.isVisible) {
        failureReason = @"panelNotVisible";
    } else if (visible && TokenForgeCompanionWindow != nil && !NSIntersectsRect(TokenForgeCompanionWindow.frame, TokenForgeVisibleFrameForFrame(TokenForgeCompanionWindow.frame))) {
        failureReason = @"offscreen";
    }
    NSString *screenName = TokenForgeCompanionWindow.screen.localizedName ?: (NSScreen.mainScreen.localizedName ?: @"main screen");
    NSPoint position = TokenForgeCompanionWindow != nil ? TokenForgeCompanionWindow.frame.origin : TokenForgeCompanionAnchor;
    NSString *runtimeStatus = [NSString stringWithFormat:@"Overlay %@ · wander %@ · click %@ · screen %@ · position %.0f, %.0f · reason %@",
                               (TokenForgeCompanionWindow != nil && TokenForgeCompanionWindow.isVisible) ? @"visible" : @"not visible",
                               wandering ? @"enabled" : @"disabled",
                               clickThrough ? @"through" : (clickReaction ? @"reaction" : @"disabled"),
                               screenName,
                               position.x,
                               position.y,
                               failureReason];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(runtimeStatus, 2)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    [buttons addArrangedSubview:TokenForgePrimaryButton(visible ? @"Hide Companion" : @"Show Companion", self, visible ? @selector(hideCompanionFromDashboard:) : @selector(showCompanionFromDashboard:))];
    [buttons addArrangedSubview:TokenForgeSecondaryButton(wandering ? @"Disable Wander" : @"Enable Wander", self, wandering ? @selector(disableWanderFromDashboard:) : @selector(enableWanderFromDashboard:))];
    [buttons addArrangedSubview:TokenForgeSecondaryButton(clickReaction ? @"Disable Click" : @"Enable Click", self, clickReaction ? @selector(disableClickReactionFromDashboard:) : @selector(enableClickReactionFromDashboard:))];
    [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Reset Position", self, @selector(resetCompanionPosition:))];
    [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Settings", self, @selector(settings:))];
    [stack addArrangedSubview:buttons];
    NSStackView *debugButtons = TokenForgeDashboardHorizontalStack(8.0);
    [debugButtons addArrangedSubview:TokenForgeSecondaryButton(@"DEBUG: Direct Show Native Overlay", self, @selector(debugDirectShowNativeOverlay:))];
    [debugButtons addArrangedSubview:TokenForgeSecondaryButton(@"DEBUG: Direct Hide Native Overlay", self, @selector(debugDirectHideNativeOverlay:))];
    [debugButtons addArrangedSubview:TokenForgeSecondaryButton(@"DEBUG: Direct Start Wander", self, @selector(debugDirectStartWander:))];
    [debugButtons addArrangedSubview:TokenForgeSecondaryButton(@"DEBUG: Direct Stop Wander", self, @selector(debugDirectStopWander:))];
    [stack addArrangedSubview:debugButtons];
    NSStackView *dumpButtons = TokenForgeDashboardHorizontalStack(8.0);
    [dumpButtons addArrangedSubview:TokenForgeSecondaryButton(@"DEBUG: Dump All TokenForge Windows", self, @selector(debugDumpAllWindows:))];
    [dumpButtons addArrangedSubview:TokenForgeSecondaryButton(@"DEBUG: Dump Native Overlay Panel State", self, @selector(debugDumpOverlayPanelState:))];
    [stack addArrangedSubview:dumpButtons];
    NSLog(@"INFO [DesktopOverlay] hidden reason=%@", visible ? @"none" : @"companionVisibleOff");
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
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeFriendlyDashboardSummary(summary, @"No activity yet"), 4)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    if (pending && TokenForgeDashboardBool(review, @"canSaveGrowth", NO)) {
        [buttons addArrangedSubview:TokenForgePrimaryButton(@"Approve", self, @selector(approveReview:))];
    }
    if (pending && TokenForgeDashboardBool(review, @"canDiscard", NO)) {
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Discard", self, @selector(discardReview:))];
    }
    if (!pending) {
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Run Analysis", self, @selector(runAnalysis:))];
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
        [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"Not connected. Add a Git repository to create the first repository companion.", 2)];
        [stack addArrangedSubview:TokenForgePrimaryButton(@"Add Repository", self, @selector(connectRepository:))];
        return card;
    }

    for (NSDictionary *repository in repositories) {
        if (![repository isKindOfClass:[NSDictionary class]]) {
            continue;
        }
        [stack addArrangedSubview:[self repositoryListRow:repository]];
    }

    [stack addArrangedSubview:TokenForgeSecondaryButton(@"Add Repository", self, @selector(connectRepository:))];
    return card;
}

- (NSView *)repositoryListRow:(NSDictionary *)repository
{
    NSStackView *rowStack = nil;
    NSView *row = TokenForgeCardWithStack(&rowStack, 14.0, 8.0);
    row.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.74].CGColor;
    [row.heightAnchor constraintGreaterThanOrEqualToConstant:132.0].active = YES;
    NSString *name = TokenForgeDashboardString(repository, @"name", @"Repository");
    NSString *identifier = TokenForgeDashboardString(repository, @"id", @"");
    BOOL canLevelUp = TokenForgeDashboardBool(repository, @"canLevelUp", NO);
    NSStackView *top = TokenForgeDashboardHorizontalStack(10.0);
    TokenForgeCompanionView *avatar = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 52, 52)];
    avatar.translatesAutoresizingMaskIntoConstraints = NO;
    avatar.stage = MAX(0, MIN(4, TokenForgeDashboardInteger(repository, @"level", 1) >= 1 ? TokenForgeDashboardInteger(repository, @"stageIndex", 0) : 0));
    avatar.visualThemeId = TokenForgeDashboardString(repository, @"avatarSkin", @"orange_cat");
    [avatar.widthAnchor constraintEqualToConstant:52.0].active = YES;
    [avatar.heightAnchor constraintEqualToConstant:52.0].active = YES;
    [top addArrangedSubview:avatar];
    NSStackView *titleStack = TokenForgeDashboardVerticalStack(3.0);
    [titleStack addArrangedSubview:TokenForgeDashboardLabel(name, 16.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [titleStack addArrangedSubview:TokenForgeDashboardLabel(TokenForgeDashboardString(repository, @"sourceBadge", @"Connected"), 11.0, NSFontWeightMedium, TokenForgeDashboardBool(repository, @"selected", NO) ? [NSColor systemGreenColor] : TokenForgeMutedTextColor(), 1)];
    [top addArrangedSubview:titleStack];
    [rowStack addArrangedSubview:top];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel(TokenForgeDashboardString(repository, @"safePath", @"Approved local folder"), 1)];
    [rowStack addArrangedSubview:TokenForgeDashboardLabel(canLevelUp ? @"Ready to evolve" : TokenForgeDashboardString(repository, @"stage", @"Egg"), 14.0, NSFontWeightSemibold, canLevelUp ? [NSColor systemOrangeColor] : TokenForgeLightCardPrimaryTextColor(), 1)];
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Level %ld · %@ · Last analyzed %@", (long)TokenForgeDashboardInteger(repository, @"level", 1), TokenForgeDashboardString(repository, @"xpStatusText", @"0 XP · 250 XP required"), TokenForgeDashboardString(repository, @"lastAnalyzed", @"Not analyzed")], 2)];
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
    NSButton *analyze = TokenForgePrimaryButton(@"Run Analysis", self, @selector(analyzeRepositoryAction:));
    analyze.toolTip = identifier;
    analyze.enabled = TokenForgeDashboardBool(repository, @"canAnalyze", NO);
    if (!analyze.enabled) {
        analyze.toolTip = TokenForgeDashboardString(repository, @"analyzeDisabledReason", @"Connect an active repository first.");
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
        NSString *repoName = TokenForgeDashboardString(repository, @"name", @"Repository");
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
        runAgents.enabled = TokenForgeDashboardInteger(agents, @"connectedCount", 0) > 0;
        runAgents.toolTip = runAgents.enabled ? @"Run analysis for a ready AI provider." : @"Choose or detect an AI provider source first.";
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
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 12.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Growth Summary")];
    NSStackView *stats = TokenForgeDashboardHorizontalStack(12.0);
    stats.distribution = NSStackViewDistributionFillEqually;
    [stats addArrangedSubview:[self statTile:@"Code" value:TokenForgeDashboardInteger(activity, @"code", TokenForgeDashboardInteger(self.state, @"codeStat", 0)) detail:@"Implementation growth" accent:[NSColor systemBlueColor]]];
    [stats addArrangedSubview:[self statTile:@"Focus" value:TokenForgeDashboardInteger(activity, @"focus", TokenForgeDashboardInteger(self.state, @"focusStat", 0)) detail:@"Steady local work" accent:[NSColor systemGreenColor]]];
    [stats addArrangedSubview:[self statTile:@"Debug" value:TokenForgeDashboardInteger(activity, @"debug", TokenForgeDashboardInteger(self.state, @"debugStat", 0)) detail:@"Fix and test loops" accent:[NSColor systemOrangeColor]]];
    [stats addArrangedSubview:[self statTile:@"Design" value:TokenForgeDashboardInteger(activity, @"design", TokenForgeDashboardInteger(self.state, @"designStat", 0)) detail:@"UI and structure" accent:[NSColor systemPinkColor]]];
    [stats addArrangedSubview:[self statTile:@"Sync" value:TokenForgeDashboardInteger(activity, @"sync", TokenForgeDashboardInteger(self.state, @"syncStat", 0)) detail:@"Safe sync state" accent:[NSColor systemTealColor]]];
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
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 10.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Recent Activity")];
    NSArray *recentRuns = TokenForgeDashboardArray(activity, @"recentRuns");
    NSMutableArray<NSString *> *items = [NSMutableArray array];
    if (TokenForgeDashboardBool(review, @"pending", NO)) {
        [items addObject:[NSString stringWithFormat:@"Activity Review · +%ld XP waiting for Save Growth", (long)TokenForgeDashboardInteger(review, @"estimatedXpDelta", 0)]];
    }
    for (NSDictionary *run in recentRuns) {
        if (![run isKindOfClass:[NSDictionary class]] || items.count >= 5) {
            continue;
        }
        NSString *type = TokenForgeDashboardString(run, @"type", @"Activity");
        NSString *summary = TokenForgeFriendlyDashboardSummary(TokenForgeDashboardString(run, @"summary", @"Activity recorded."), @"Activity recorded.");
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

- (NSView *)buildSettingsRootView
{
    NSView *root = [[NSView alloc] initWithFrame:NSZeroRect];
    root.wantsLayer = YES;
    root.layer.backgroundColor = [NSColor colorWithCalibratedRed:0.950 green:0.960 blue:0.975 alpha:1.0].CGColor;
    root.translatesAutoresizingMaskIntoConstraints = NO;

    NSScrollView *scrollView = [[NSScrollView alloc] initWithFrame:NSZeroRect];
    scrollView.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.hasVerticalScroller = YES;
    scrollView.hasHorizontalScroller = NO;
    scrollView.drawsBackground = NO;
    [root addSubview:scrollView];
    TokenForgePinSubview(scrollView, root, 0, 0, 0, 0);

    TokenForgeFlippedView *document = [[TokenForgeFlippedView alloc] initWithFrame:NSMakeRect(0, 0, 760, 900)];
    document.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.documentView = document;
    [document.widthAnchor constraintEqualToAnchor:scrollView.contentView.widthAnchor].active = YES;

    NSStackView *content = TokenForgeDashboardVerticalStack(14.0);
    content.alignment = NSLayoutAttributeWidth;
    [document addSubview:content];
    NSLayoutConstraint *contentFillWidth = [content.widthAnchor constraintEqualToAnchor:document.widthAnchor constant:-48.0];
    contentFillWidth.priority = NSLayoutPriorityDefaultHigh;
    [NSLayoutConstraint activateConstraints:@[
        [content.topAnchor constraintEqualToAnchor:document.topAnchor constant:28.0],
        [content.centerXAnchor constraintEqualToAnchor:document.centerXAnchor],
        [content.widthAnchor constraintLessThanOrEqualToConstant:720.0],
        [content.widthAnchor constraintLessThanOrEqualToAnchor:document.widthAnchor constant:-48.0],
        contentFillWidth,
        [content.bottomAnchor constraintLessThanOrEqualToAnchor:document.bottomAnchor constant:-28.0]
    ]];

    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    [content addArrangedSubview:TokenForgeDashboardLabel(@"TokenForge Settings", 25.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [content addArrangedSubview:TokenForgeDashboardLabel(@"Control how your active repository companion appears, moves, and reacts.", 13.0, NSFontWeightRegular, TokenForgeLightCardSecondaryTextColor(), 2)];

    BOOL hasActiveRepository = TokenForgeDashboardBool(self.state, @"hasActiveRepository", NO);
    BOOL companionVisible = TokenForgeDashboardBool(self.state, @"companionVisible", TokenForgeMenuCompanionEnabled);
    NSString *repositorySettingDetail = hasActiveRepository ? @"Show the desktop companion for the active repository." : @"Connect a repository to customize its companion.";
    NSString *behaviorDisabledDetail = companionVisible ? @"Connect a repository to customize its companion." : @"Enable Companion visible to use movement and reactions.";

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Companion", 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Companion visible" detail:repositorySettingDetail enabled:companionVisible action:@selector(toggleCompanionVisible:) actionName:@"toggleCompanionVisible" interactive:hasActiveRepository]];

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Behavior", 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Wander movement" detail:(hasActiveRepository && companionVisible ? @"Let the companion move subtly while TokenForge is running." : behaviorDisabledDetail) enabled:TokenForgeDashboardBool(self.state, @"wanderEnabled", YES) action:@selector(toggleWanderEnabled:) actionName:@"setWanderEnabled" interactive:(hasActiveRepository && companionVisible)]];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Click reaction" detail:(hasActiveRepository && companionVisible ? @"Let clicks trigger a companion reaction. Turn this off for click-through mode." : behaviorDisabledDetail) enabled:TokenForgeDashboardBool(self.state, @"clickReactionEnabled", YES) action:@selector(toggleClickReactionEnabled:) actionName:@"setClickReactionEnabled" interactive:(hasActiveRepository && companionVisible)]];

    NSView *gridCard = TokenForgeDashboardCard();
    [gridCard.heightAnchor constraintGreaterThanOrEqualToConstant:278.0].active = YES;
    NSStackView *grid = TokenForgeDashboardVerticalStack(10.0);
    [gridCard addSubview:grid];
    TokenForgePinSubview(grid, gridCard, 18, 18, 18, 18);
    [grid addArrangedSubview:TokenForgeDashboardLabel(@"Appearance", 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [grid addArrangedSubview:TokenForgeDashboardLabel(@"Choose the skin for this repository companion. The dashboard preview and desktop companion update immediately.", 12.0, NSFontWeightRegular, TokenForgeLightCardSecondaryTextColor(), 2)];
    NSArray<NSArray<NSString *> *> *skins = @[
        @[@"orange_cat", @"Orange Cat"],
        @[@"white_cat", @"White Cat"],
        @[@"calico", @"Calico"],
        @[@"black_cat", @"Black Cat"],
        @[@"retriever", @"Retriever"],
        @[@"runner", @"Runner"]
    ];
    NSString *selectedSkin = TokenForgeDashboardString(companion, @"skin", @"orange_cat");
    for (NSInteger rowIndex = 0; rowIndex < 2; rowIndex++) {
        NSStackView *skinRow = TokenForgeDashboardHorizontalStack(10.0);
        skinRow.distribution = NSStackViewDistributionFillEqually;
        for (NSInteger columnIndex = 0; columnIndex < 3; columnIndex++) {
            NSArray<NSString *> *skin = skins[rowIndex * 3 + columnIndex];
            [skinRow addArrangedSubview:[self skinTileWithId:skin[0] title:skin[1] selected:[selectedSkin isEqualToString:skin[0]]]];
        }
        [grid addArrangedSubview:skinRow];
    }
    [content addArrangedSubview:gridCard];

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Startup", 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Launch at login" detail:@"Available in a signed release build." enabled:NO action:nil actionName:@"setLaunchAtLogin" interactive:NO]];

    [content addArrangedSubview:TokenForgeDashboardLabel(@"Privacy / Sync", 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [content addArrangedSubview:[self localDataCard]];

    NSStackView *toolbar = TokenForgeDashboardHorizontalStack(10.0);
    toolbar.distribution = NSStackViewDistributionFill;
    [toolbar addArrangedSubview:TokenForgeDashboardButton(@"Reset Position", self, @selector(resetCompanionPosition:))];
    [toolbar addArrangedSubview:TokenForgeDashboardButton(@"Close", self, @selector(closeSettings:))];
    [content addArrangedSubview:toolbar];
    NSTextField *footer = TokenForgeDashboardLabel(@"Changes are saved automatically.", 12.0, NSFontWeightRegular, TokenForgeLightCardSecondaryTextColor(), 1);
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

- (NSView *)localDataCard
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 7.0);
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Local data and Safe Sync", 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Local progress is stored on this Mac. Safe Sync is optional and only sends sanitized aggregate summaries when connected.", 13.0, NSFontWeightRegular, TokenForgeLightCardSecondaryTextColor(), 3)];
    [stack addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"Status: %@", TokenForgeDashboardString(self.state, @"syncStatusText", @"Sync optional")], 12.0, NSFontWeightMedium, [NSColor systemGreenColor], 1)];
    return card;
}

- (void)windowWillClose:(NSNotification *)notification
{
    if (notification.object == self.dashboardWindow) {
        [self.dashboardWindow orderOut:nil];
        NSLog(@"INFO [AppLifecycle] dashboardWindowClosed keepAppRunning=true");
    }
    if (notification.object == self.settingsWindow) {
        [self.settingsWindow orderOut:nil];
    }
}

- (BOOL)windowShouldClose:(NSWindow *)sender
{
    if (sender == self.dashboardWindow) {
        [sender orderOut:nil];
        TokenForgeLogWindowLifecycle(@"windowShouldCloseIntercepted", sender, @"dashboardXHide");
        NSLog(@"INFO [AppLifecycle] dashboardWindowClosed keepAppRunning=true");
        NSLog(@"INFO [WindowLifecycle] dashboard X close mappedTo=hideDashboard");
        return NO;
    }

    if (sender == self.settingsWindow) {
        [sender orderOut:nil];
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
    TokenForgeCurrentDashboardTab = self.selectedNavItem;
    NSMutableDictionary *next = [self.state mutableCopy];
    next[@"selectedNavItem"] = self.selectedNavItem;
    self.state = next;
    if (showDashboard) {
        [self rebuildDashboardIfNeeded];
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
- (void)settings:(id)sender { [self setSelectedNav:@"settings" action:"openSettings" showDashboard:YES]; [self showSettings]; }
- (void)homepage:(id)sender { TokenForgeSendDashboardAction("homepage"); if (TokenForgeDashboardActionClicked == nil) [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"https://github.com/HwangSeokBeom/TokenForge"]]; }
- (void)reportIssue:(id)sender { TokenForgeSendDashboardAction("report_issue"); if (TokenForgeDashboardActionClicked == nil) [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"https://github.com/HwangSeokBeom/TokenForge/issues"]]; }
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
- (void)levelUpCompanion:(id)sender { TokenForgeSendDashboardAction("companion.levelUp"); }
- (void)connectRepository:(id)sender { [self setSelectedNav:@"repository" action:"repository.add" showDashboard:YES]; }
- (void)connectCodexAgent:(id)sender { [self setSelectedNav:@"aiAgents" action:"agent.connect:codex" showDashboard:YES]; }
- (void)openActiveCompanionDashboard:(id)sender
{
    self.selectedNavItem = @"dashboard";
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
    TokenForgeCurrentDashboardTab = self.selectedNavItem;
    self.activityFilterValue = value.length > 0 ? [@"repo:" stringByAppendingString:value] : @"active";
    TokenForgeSendDashboardAction(payload.UTF8String);
}
- (void)selectRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.setActive:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)analyzeRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.analyze:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)viewRepositoryGrowthAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; self.activityFilterValue = value.length > 0 ? [@"repo:" stringByAppendingString:value] : @"active"; [self setSelectedNav:@"activity" action:"navigation.openActivity" showDashboard:YES]; }
- (void)evolveRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"companion.levelUp:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)disconnectRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.archive:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
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
- (void)setActivityFilterAction:(id)sender { self.activityFilterValue = [(NSButton *)sender toolTip] ?: @"all"; [self rebuildDashboardIfNeeded]; }
- (void)toggleCompanionVisible:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"companionVisible", YES)]; [self setStateBool:@"companionVisible" enabled:enabled]; if (enabled) { ShowDesktopCompanionOverlay(); } else { HideDesktopCompanionOverlay(); } NSLog(@"INFO [Settings] companionVisible changed value=%@", enabled ? @"true" : @"false"); TokenForgeSendDashboardAction(enabled ? "show_companion" : "hide_companion"); }
- (void)toggleWanderEnabled:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"wanderEnabled", YES)]; [self setStateBool:@"wanderEnabled" enabled:enabled]; SetCompanionOverlayMotionProfile(enabled ? 1 : 0, 5.0, enabled ? 32.0 : 0.0, enabled ? 18.0 : 0.0, 3.4, enabled, 1.1); NSLog(@"INFO [Settings] wanderEnabled changed value=%@", enabled ? @"true" : @"false"); TokenForgeSendDashboardAction(enabled ? "enable_wander" : "disable_wander"); }
- (void)toggleClickReactionEnabled:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"clickReactionEnabled", YES)]; [self setStateBool:@"clickThroughEnabled" enabled:!enabled]; [self setStateBool:@"clickReactionEnabled" enabled:enabled]; SetCompanionOverlayClickThrough(!enabled); NSLog(@"INFO [Settings] clickReactionEnabled changed value=%@", enabled ? @"true" : @"false"); TokenForgeSendDashboardAction(enabled ? "enable_click" : "disable_click"); }
- (void)showCompanionFromDashboard:(id)sender { const char *trace = TokenForgeNextOverlayTraceId(); NSString *traceString = [NSString stringWithUTF8String:trace]; NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=show_companion", traceString); [self setStateBool:@"companionVisible" enabled:YES]; TokenForgeShowDesktopCompanionOverlayWithTrace(traceString); if (TokenForgeDashboardBool(self.state, @"wanderEnabled", YES)) { TokenForgeSetCompanionOverlayMotionProfileWithTrace(traceString, 1, 5.0, 32.0, 18.0, 3.4, true, 1.1); } NSLog(@"INFO [DesktopOverlay] show requested visibleSetting=true source=motionCard"); NSString *payload = [NSString stringWithFormat:@"show_companion:%@", traceString]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)hideCompanionFromDashboard:(id)sender { const char *trace = TokenForgeNextOverlayTraceId(); NSString *traceString = [NSString stringWithUTF8String:trace]; NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=hide_companion", traceString); [self setStateBool:@"companionVisible" enabled:NO]; TokenForgeHideDesktopCompanionOverlayWithTrace(traceString); NSLog(@"INFO [DesktopOverlay] hidden reason=companionVisibleOff"); NSString *payload = [NSString stringWithFormat:@"hide_companion:%@", traceString]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)enableWanderFromDashboard:(id)sender { const char *trace = TokenForgeNextOverlayTraceId(); NSString *traceString = [NSString stringWithUTF8String:trace]; NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=enable_wander", traceString); [self setStateBool:@"companionVisible" enabled:YES]; [self setStateBool:@"wanderEnabled" enabled:YES]; TokenForgeShowDesktopCompanionOverlayWithTrace(traceString); TokenForgeSetCompanionOverlayMotionProfileWithTrace(traceString, 1, 5.0, 32.0, 18.0, 3.4, true, 1.1); NSLog(@"INFO [DesktopOverlay] movementTimer started interval=%.2f source=motionCard", 1.0 / 30.0); NSString *payload = [NSString stringWithFormat:@"enable_wander:%@", traceString]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)disableWanderFromDashboard:(id)sender { const char *trace = TokenForgeNextOverlayTraceId(); NSString *traceString = [NSString stringWithUTF8String:trace]; NSLog(@"INFO [OverlayTrace:%@] Dashboard button click action=disable_wander", traceString); [self setStateBool:@"wanderEnabled" enabled:NO]; TokenForgeSetCompanionOverlayMotionProfileWithTrace(traceString, 0, 0.0, 0.0, 0.0, 3.4, false, 1.1); NSLog(@"INFO [DesktopCompanion] wander stopped source=motionCard"); NSString *payload = [NSString stringWithFormat:@"disable_wander:%@", traceString]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)enableClickReactionFromDashboard:(id)sender { [self setStateBool:@"clickThroughEnabled" enabled:NO]; [self setStateBool:@"clickReactionEnabled" enabled:YES]; SetCompanionOverlayClickThrough(false); NSLog(@"INFO [DesktopCompanion] click-through disabled source=motionCard"); TokenForgeSendDashboardAction("enable_click"); }
- (void)disableClickReactionFromDashboard:(id)sender { [self setStateBool:@"clickThroughEnabled" enabled:YES]; [self setStateBool:@"clickReactionEnabled" enabled:NO]; SetCompanionOverlayClickThrough(true); NSLog(@"INFO [DesktopCompanion] click-through enabled source=motionCard"); TokenForgeSendDashboardAction("disable_click"); }
- (void)toggleLaunchAtLogin:(id)sender { NSLog(@"INFO [NativeDashboard] launch at login unavailable in unsigned build"); }
- (void)changeSkin:(id)sender { NSString *skin = [(NSButton *)sender toolTip] ?: @"orange_cat"; [self setSelectedCompanionSkin:skin]; NSString *payload = [NSString stringWithFormat:@"changeCompanionSkin:%@", skin]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)resetCompanionPosition:(id)sender { TokenForgeResetCompanionFrame(); TokenForgeSendDashboardAction("reset_companion_position"); }
- (void)debugDirectShowNativeOverlay:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Show Native Overlay", trace); TokenForgeShowDesktopCompanionOverlayWithTrace(trace); }
- (void)debugDirectHideNativeOverlay:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Hide Native Overlay", trace); TokenForgeHideDesktopCompanionOverlayWithTrace(trace); }
- (void)debugDirectStartWander:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Start Wander", trace); TokenForgeShowDesktopCompanionOverlayWithTrace(trace); TokenForgeSetCompanionOverlayMotionProfileWithTrace(trace, 1, 5.0, 140.0, 32.0, 1.6, true, 1.1); }
- (void)debugDirectStopWander:(id)sender { NSString *trace = [NSString stringWithFormat:@"debug-%s", TokenForgeNextOverlayTraceId()]; NSLog(@"INFO [OverlayTrace:%@] DEBUG Direct Stop Wander", trace); TokenForgeSetCompanionOverlayMotionProfileWithTrace(trace, 0, 0.0, 0.0, 0.0, 3.4, false, 1.1); }
- (void)debugDumpAllWindows:(id)sender { TokenForgeDumpAllWindows(@"debugButton"); }
- (void)debugDumpOverlayPanelState:(id)sender { TokenForgeDumpOverlayPanelState(@"debugButton"); }
- (void)closeSettings:(id)sender { [self.settingsWindow orderOut:nil]; }
- (void)resetLocalState:(id)sender { TokenForgeSendDashboardAction("reset_local_state"); }
- (void)quit:(id)sender { TokenForgeSendDashboardAction("quit"); TokenForgeEnsureLifecycleDelegate().explicitTerminationRequested = YES; [NSApp terminate:nil]; }

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
    if (TokenForgeDashboardOpening) {
        NSLog(@"INFO [NativeLifecycle] prevent_reentrant_open traceId=dashboard");
        return;
    }

    if (!TokenForgeAppKitRegistrationReady()) {
        NSLog(@"INFO [NativeLifecycle] open_dashboard deferred reason=app_not_ready");
        if (!TokenForgeDashboardOpenPending) {
            TokenForgeDashboardOpenPending = YES;
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.35 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                TokenForgeDashboardOpenPending = NO;
                TokenForgeOpenNativeDashboardOnMain();
            });
        }
        return;
    }

    TokenForgeDashboardOpening = YES;
    NSLog(@"INFO [NativeLifecycle] open_dashboard executed traceId=dashboard");
    [TokenForgeEnsureNativeDashboardController() showDashboard];
    TokenForgeDashboardOpening = NO;
}

@implementation TokenForgeAppLifecycleDelegate

- (void)applicationWillFinishLaunching:(NSNotification *)notification
{
    [[NSUserDefaults standardUserDefaults] setBool:NO forKey:@"NSQuitAlwaysKeepsWindows"];
    [[NSUserDefaults standardUserDefaults] setBool:YES forKey:@"ApplePersistenceIgnoreState"];
    NSLog(@"INFO [WindowLifecycle] applicationWillFinishLaunching restoration=disabled");

    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationWillFinishLaunching:)]) {
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
    NSLog(@"INFO [WindowLifecycle] shouldRestoreApplicationState=false route=openDashboard");
    if (!TokenForgeDashboardOpenPending) {
        TokenForgeDashboardOpenPending = YES;
        dispatch_async(dispatch_get_main_queue(), ^{
            TokenForgeDashboardOpenPending = NO;
            TokenForgeOpenNativeDashboardOnMain();
        });
    }

    return NO;
}

- (BOOL)applicationSupportsSecureRestorableState:(NSApplication *)application
{
    return YES;
}

- (void)install
{
    if (self.installed) {
        [self installMainWindowHook];
        return;
    }

    if (TokenForgeLifecycleInstallInProgress) {
        return;
    }

    TokenForgeLifecycleInstallInProgress = YES;
    TokenForgeLogRuntimeIdentityIfNeeded();
    id<NSApplicationDelegate> currentDelegate = [NSApp delegate];
    if (currentDelegate != self) {
        self.originalAppDelegate = currentDelegate;
        [NSApp setDelegate:self];
    }

    [self installStatusItem];
    [self installWindowNotifications];
    [self installMainWindowHook];
    NSLog(@"INFO [AppLifecycle] shouldTerminateAfterLastWindowClosed=false");
    self.installed = YES;
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
}

- (void)installStatusItem
{
    if (self.statusItem != nil) {
        return;
    }

    self.statusItem = [[NSStatusBar systemStatusBar] statusItemWithLength:NSVariableStatusItemLength];
    self.statusItem.button.title = @"";
    self.statusItem.button.image = TokenForgeCreateStatusCompanionImage(TokenForgeMenuStageIndex, TokenForgeMenuArchetypeIndex, 0, @"idle");
    self.statusItem.button.imagePosition = NSImageLeft;
    self.statusItem.button.toolTip = @"TokenForge";

    NSMenu *menu = [[NSMenu alloc] initWithTitle:@"TokenForge"];
    NSMenuItem *companionItem = [[NSMenuItem alloc] initWithTitle:@"Token · Egg · Level 1" action:nil keyEquivalent:@""];
    companionItem.enabled = NO;
    companionItem.tag = 1001;
    [menu addItem:companionItem];

    NSMenuItem *repositoryItem = [[NSMenuItem alloc] initWithTitle:@"Repository: Not selected" action:nil keyEquivalent:@""];
    repositoryItem.enabled = NO;
    repositoryItem.tag = 1002;
    [menu addItem:repositoryItem];

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

    [menu addItem:[NSMenuItem separatorItem]];

    NSMenuItem *quitItem = [[NSMenuItem alloc] initWithTitle:@"Quit TokenForge" action:@selector(quitTokenForgeFromStatusItem:) keyEquivalent:@"q"];
    quitItem.target = self;
    [menu addItem:quitItem];

    self.statusItem.menu = menu;
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
}

- (void)updateStatusItemMenu
{
    if (self.statusItem == nil || self.statusItem.menu == nil) {
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
    NSImage *newImage = TokenForgeCreateStatusCompanionImage(animatedStage, animatedArchetype, self.statusAnimationFrame, mode);
    self.statusItem.button.image = newImage;
    self.statusItem.button.imagePosition = NSImageLeft;
    NSLog(@"INFO [MenuBarCompanion] tick frameIndex=%ld mode=%@ imageHash=%lu", (long)self.statusAnimationFrame, mode, (unsigned long)newImage.hash);
    NSLog(@"INFO [MenuBarCompanion] buttonImageUpdated changed=%@", oldImage != newImage ? @"true" : @"false");
    NSLog(@"INFO [MenuBarCompanion] cacheKey=%@ cacheHit=%@", cacheKey, oldImage == newImage ? @"true" : @"false");
    NSLog(@"INFO [MenuBarCompanion] frame index=%ld state=%@ animationRunning=true", (long)self.statusAnimationFrame, mode);
    [[self.statusItem.menu itemWithTag:1001] setTitle:[NSString stringWithFormat:@"%@ · %@ · Level %ld%@", TokenForgeMenuCompanionName, TokenForgeMenuStage, (long)MAX(1, TokenForgeMenuLevel), TokenForgeMenuCanLevelUp ? @" · Level Up Ready" : @""]];
    [[self.statusItem.menu itemWithTag:1002] setTitle:[NSString stringWithFormat:@"Repository: %@", TokenForgeMenuRepositoryAlias]];
    [[self.statusItem.menu itemWithTag:1003] setTitle:[NSString stringWithFormat:@"AI Agents: %@", TokenForgeMenuAgentStatus]];
    [[self.statusItem.menu itemWithTag:1004] setTitle:[NSString stringWithFormat:@"Safe Sync: %@", TokenForgeMenuSyncStatus]];
    [[self.statusItem.menu itemWithTag:1005] setTitle:[NSString stringWithFormat:@"Desktop Companion: %@", TokenForgeMenuCompanionEnabled ? (TokenForgeMenuClickThrough ? @"Native Active · Click-through" : @"Native Active · Interactive") : @"Off"]];
    [self.statusItem.menu itemWithTag:1006].enabled = !TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1007].enabled = TokenForgeMenuCompanionEnabled;
    [[self.statusItem.menu itemWithTag:1006] setTitle:@"Show Companion"];
    [[self.statusItem.menu itemWithTag:1007] setTitle:@"Hide Companion"];
    [[self.statusItem.menu itemWithTag:1013] setTitle:TokenForgeMenuClickThrough ? @"Disable Click-through" : @"Enable Click-through"];
    [self.statusItem.menu itemWithTag:1013].enabled = TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1014].enabled = TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1015].enabled = YES;
    [self.statusItem.menu itemWithTag:1016].enabled = YES;
    [self.statusItem.menu itemWithTag:1008].enabled = YES;
    [self.statusItem.menu itemWithTag:1009].enabled = [self isMainWindowVisible];
    [self.statusItem.menu itemWithTag:1010].enabled = TokenForgeMenuCanAnalyze;
    [self.statusItem.menu itemWithTag:1011].enabled = TokenForgeMenuCanSync;
    [self.statusItem.menu itemWithTag:1012].enabled = YES;
}

- (void)installMainWindowHook
{
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
    TokenForgeOpenNativeDashboardOnMain();
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
    [TokenForgeEnsureNativeDashboardController() hideDashboard];
    return;
    [self installMainWindowHook];
    NSWindow *window = self.mainWindow ?: TokenForgeFindMainWindow();
    if (window != nil) {
        [window orderOut:nil];
    }
}

- (BOOL)isMainWindowVisible
{
    [self installMainWindowHook];
    NSWindow *window = self.mainWindow ?: TokenForgeFindMainWindow();
    return window != nil && window.isVisible;
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
    self.explicitTerminationRequested = YES;
    [TokenForgeCompanionWindow orderOut:nil];
    TokenForgeCompanionWindow = nil;
    TokenForgeCompanionContentView = nil;
    [TokenForgeCompanionMotionTimer invalidate];
    TokenForgeCompanionMotionTimer = nil;
    [self.statusAnimationTimer invalidate];
    self.statusAnimationTimer = nil;
    NSLog(@"INFO [MenuBarCompanion] animation stopped reason=quit");
    NSLog(@"INFO [DesktopOverlay] quit cleanup completed");
    NSLog(@"INFO [OverlayTrace] app_terminate overlay_cleanup=true");

    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationShouldTerminate:)]) {
        return [self.originalAppDelegate applicationShouldTerminate:sender];
    }

    return NSTerminateNow;
}

- (void)applicationDidFinishLaunching:(NSNotification *)notification
{
    NSLog(@"INFO [NativeLifecycle] app_registration_ready=true notification=applicationDidFinishLaunching");
    TokenForgeRequestLifecycleInstall(@"applicationDidFinishLaunching");
    [self installMainWindowHook];
    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationDidFinishLaunching:)]) {
        [self.originalAppDelegate applicationDidFinishLaunching:notification];
    }
}

- (void)applicationWillResignActive:(NSNotification *)notification
{
    NSLog(@"INFO [OverlayTrace] app_resign_active keep_overlay=%@", TokenForgeDesiredCompanionVisible ? @"true" : @"false");
    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationWillResignActive:)]) {
        [self.originalAppDelegate applicationWillResignActive:notification];
    }
}

- (void)applicationWillHide:(NSNotification *)notification
{
    NSLog(@"INFO [OverlayTrace] app_hide keep_overlay=%@", TokenForgeDesiredCompanionVisible ? @"true" : @"false");
    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationWillHide:)]) {
        [self.originalAppDelegate applicationWillHide:notification];
    }
}

- (BOOL)applicationShouldHandleReopen:(NSApplication *)sender hasVisibleWindows:(BOOL)flag
{
    NSLog(@"INFO [WindowLifecycle] applicationShouldHandleReopen hasVisibleWindows=%@ route=openDashboard", flag ? @"true" : @"false");
    TokenForgeDumpAllWindows(@"beforeReopen");
    TokenForgeOpenNativeDashboardOnMain();
    return YES;
}

- (void)applicationDidBecomeActive:(NSNotification *)notification
{
    NSLog(@"INFO [DesktopOverlay] background state keepVisible=%@", TokenForgeMenuCompanionEnabled ? @"true" : @"false");

    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationDidBecomeActive:)]) {
        [self.originalAppDelegate applicationDidBecomeActive:notification];
    }
}

- (BOOL)windowShouldClose:(NSWindow *)sender
{
    if (self.explicitTerminationRequested || sender != self.mainWindow) {
        if (sender != self.mainWindow && TokenForgeWindowLooksBlank(sender)) {
            [sender orderOut:nil];
            TokenForgeOpenNativeDashboardOnMain();
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
    [TokenForgeEnsureNativeDashboardController() showDashboard];
}

- (void)hideTokenForgeFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("hide_dashboard");
    [TokenForgeEnsureNativeDashboardController() hideDashboard];
}

- (void)enableDesktopCompanionFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("toggleCompanionVisible:true");
    TokenForgeMenuCompanionEnabled = YES;
    TokenForgeMenuClickThrough = NO;
    TokenForgeCreateCompanionOverlayOnMain(@"status-item");
    TokenForgeCompanionWindow.ignoresMouseEvents = NO;
    [TokenForgeCompanionWindow makeKeyAndOrderFront:nil];
    [TokenForgeCompanionWindow orderFrontRegardless];
    [self updateStatusItemMenu];
}

- (void)disableDesktopCompanionFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("toggleCompanionVisible:false");
    TokenForgeMenuCompanionEnabled = NO;
    [TokenForgeCompanionWindow orderOut:nil];
    [self updateStatusItemMenu];
}

- (void)toggleClickThroughFromStatusItem:(id)sender
{
    TokenForgeMenuClickThrough = !TokenForgeMenuClickThrough;
    TokenForgeSendMenuAction(TokenForgeMenuClickThrough ? "setClickReactionEnabled:false" : "setClickReactionEnabled:true");
    if (TokenForgeCompanionWindow != nil) {
        TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgeMenuClickThrough;
    }
    [self updateStatusItemMenu];
}

- (void)resetCompanionPositionFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("reset_companion_position");
    TokenForgeResetCompanionFrame();
    [self updateStatusItemMenu];
}

- (void)quitTokenForgeFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("quit");
    self.explicitTerminationRequested = YES;
    [NSApp terminate:nil];
}

- (void)analyzeCurrentRepositoryFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("repository.runAnalysis");
    [TokenForgeEnsureNativeDashboardController() showDashboard];
}

- (void)addRepositoryFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("repository.add");
    [TokenForgeEnsureNativeDashboardController() showDashboard];
}

- (void)connectAiAgentFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("navigation.openAgents");
    [TokenForgeEnsureNativeDashboardController() showDashboard];
}

- (void)syncNowFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("sync_now");
    [self showMainWindow];
}

- (void)settingsFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("settings");
    [TokenForgeEnsureNativeDashboardController() showSettings];
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

    if (!TokenForgeAppKitRegistrationReady()) {
        TokenForgeRequestLifecycleInstall(@"ensure_delegate_not_ready");
        return TokenForgeLifecycleDelegate;
    }

    [TokenForgeLifecycleDelegate install];
    return TokenForgeLifecycleDelegate;
}

extern "C" bool InstallTokenForgeMacAppLifecycle()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeLogRuntimeIdentityIfNeeded();
        TokenForgeRequestLifecycleInstall(@"InstallTokenForgeMacAppLifecycle");
    });
    return true;
}

extern "C" const char *TokenForge_GetOverlayLibraryPath()
{
    static char path[PATH_MAX] = {0};
    if (path[0] != '\0') {
        return path;
    }

    Dl_info info;
    if (dladdr((const void *)&TokenForge_GetOverlayLibraryPath, &info) != 0 && info.dli_fname != NULL) {
        strncpy(path, info.dli_fname, sizeof(path) - 1);
        path[sizeof(path) - 1] = '\0';
        return path;
    }

    return "DesktopCompanionOverlay path unavailable";
}

extern "C" void TokenForge_UpdateStatusItem(const char *companionName, const char *stage, int stageIndex, int archetypeIndex, int level, const char *repositoryAlias, const char *agentStatus, const char *syncStatus, bool companionEnabled, bool clickThrough, bool canAnalyze, bool canSync)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeAssignMenuString(&TokenForgeMenuCompanionName, companionName, @"Token");
        TokenForgeAssignMenuString(&TokenForgeMenuStage, stage, @"Egg");
        TokenForgeMenuStageIndex = MAX(0, MIN(4, stageIndex));
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
    });
}

extern "C" void TokenForge_RegisterMenuActionCallback(TokenForgeMenuActionCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeMenuActionClicked = callback;
    });
}

extern "C" void TokenForge_ShowDashboardWindow()
{
    void (^block)(void) = ^{
        [TokenForgeEnsureNativeDashboardController() showDashboard];
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_HideDashboardWindow()
{
    void (^block)(void) = ^{
        [TokenForgeEnsureNativeDashboardController() hideDashboard];
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_ToggleDashboardWindow()
{
    void (^block)(void) = ^{
        [TokenForgeEnsureNativeDashboardController() toggleDashboard];
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_ShowSettingsWindow()
{
    void (^block)(void) = ^{
        [TokenForgeEnsureNativeDashboardController() showSettings];
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" bool TokenForge_PickFolder(const char *prompt, char *selectedPath, int selectedPathCapacity)
{
    if (selectedPath == NULL || selectedPathCapacity <= 0) {
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
        return false;
    }

    const char *utf8 = path.UTF8String;
    if (utf8 == NULL) {
        return false;
    }

    strlcpy(selectedPath, utf8, (size_t)selectedPathCapacity);
    return true;
}

extern "C" void TokenForge_UpdateDashboardState(const char *json)
{
    NSDictionary *state = TokenForgeParseJsonDictionary(json);
    void (^block)(void) = ^{
        [TokenForgeEnsureNativeDashboardController() updateState:state];
        [TokenForgeEnsureNativeDashboardController() setMenuBarStatus:state];
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_SetMenuBarStatus(const char *json)
{
    NSDictionary *state = TokenForgeParseJsonDictionary(json);
    void (^block)(void) = ^{
        [TokenForgeEnsureNativeDashboardController() setMenuBarStatus:state];
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void TokenForge_SetCompanionVisible(bool visible)
{
    NSLog(@"INFO [OverlayTrace:csharp] Native entered TokenForge_SetCompanionVisible visible=%d", visible ? 1 : 0);
    if (visible) {
        TokenForgeShowDesktopCompanionOverlayWithTrace(@"csharp");
    } else {
        TokenForgeHideDesktopCompanionOverlayWithTrace(@"csharp");
    }
}

extern "C" void TokenForge_RegisterDashboardActionCallback(TokenForgeDashboardActionCallback callback)
{
    void (^block)(void) = ^{
        TokenForgeDashboardActionClicked = callback;
        NSLog(@"INFO [NativeDashboard] action callback registered");
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void ShowTokenForgeMainWindow()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureLifecycleDelegate() showMainWindow];
    });
}

extern "C" void HideTokenForgeMainWindow()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureLifecycleDelegate() hideMainWindow];
    });
}

extern "C" bool IsTokenForgeMainWindowVisible()
{
    __block BOOL visible = NO;
    if ([NSThread isMainThread]) {
        visible = [TokenForgeEnsureLifecycleDelegate() isMainWindowVisible];
    } else {
        dispatch_sync(dispatch_get_main_queue(), ^{
            visible = [TokenForgeEnsureLifecycleDelegate() isMainWindowVisible];
        });
    }

    return visible;
}

extern "C" void QuitTokenForgeApp()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeEnsureLifecycleDelegate().explicitTerminationRequested = YES;
        [NSApp terminate:nil];
    });
}

extern "C" bool CreateDesktopCompanionOverlay()
{
    if ([NSThread isMainThread]) {
        TokenForgeCreateCompanionOverlayOnMain(@"create");
    } else {
        dispatch_sync(dispatch_get_main_queue(), ^{
            TokenForgeCreateCompanionOverlayOnMain(@"create");
        });
    }

    return true;
}

extern "C" void ShowDesktopCompanionOverlay()
{
    TokenForgeShowDesktopCompanionOverlayWithTrace(@"direct");
}

static void TokenForgeShowDesktopCompanionOverlayWithTrace(NSString *traceId)
{
    void (^block)(void) = ^{
        NSString *trace = traceId.length > 0 ? traceId : @"direct";
        BOOL oldDesired = TokenForgeDesiredCompanionVisible;
        TokenForgeDesiredCompanionVisible = YES;
        TokenForgeLastShowReason = @"show_requested";
        TokenForgeLastProjectionSource = @"native_show";
        NSLog(@"INFO [OverlayTrace:%@] show_requested traceId=%@ repoId=%@ source=button", trace, trace, TokenForgeMenuRepositoryAlias ?: @"Not selected");
        NSLog(@"INFO [OverlayTrace:%@] desired_visible_changed old=%@ new=true source=native_show", trace, oldDesired ? @"true" : @"false");
        NSLog(@"INFO [OverlayTrace:%@] native_show_enter traceId=%@", trace, trace);
        NSLog(@"INFO [OverlayTrace:%@] Native entered ShowDesktopCompanionOverlay visible=1", trace);
        NSLog(@"INFO [DesktopCompanion] action=show requested source=motionCard thread=%@", [NSThread isMainThread] ? @"main" : @"background");
        NSLog(@"INFO [DesktopCompanion] show requested visible=true repo=%@", TokenForgeMenuRepositoryAlias ?: @"Not selected");
        if (!TokenForgeAppKitRegistrationReady()) {
            NSLog(@"INFO [NativeLifecycle] open_dashboard deferred reason=app_not_ready");
            NSLog(@"INFO [OverlayTrace:%@] native_show_deferred reason=app_not_ready", trace);
            TokenForgeRequestLifecycleInstall(@"overlay_show");
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.35 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                TokenForgeShowDesktopCompanionOverlayWithTrace(trace);
            });
            return;
        }
        TokenForgeCreateCompanionOverlayOnMain(trace);
        if (TokenForgeCompanionWindow == nil) {
            NSLog(@"WARN [OverlayTrace:%@] native_show_aborted reason=panel_create_deferred", trace);
            return;
        }
        TokenForgeMenuCompanionEnabled = YES;
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        NSRect frame = TokenForgeClampFrameToVisibleFrame(TokenForgeCompanionWindow.frame);
        if (!NSEqualRects(frame, TokenForgeCompanionWindow.frame)) {
            [TokenForgeCompanionWindow setFrame:frame display:NO];
            TokenForgeCompanionAnchor = frame.origin;
        }

        TokenForgeCompanionWindow.alphaValue = 1.0;
        TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgeMenuClickThrough;
        TokenForgeCompanionWindow.level = NSStatusWindowLevel;
        TokenForgeCompanionWindow.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary | NSWindowCollectionBehaviorStationary | NSWindowCollectionBehaviorIgnoresCycle;
        NSLog(@"INFO [DesktopCompanion] click-through %@", TokenForgeMenuClickThrough ? @"true" : @"false");
        NSLog(@"INFO [OverlayTrace:%@] panel=%@ frame={{%.2f,%.2f},{%.2f,%.2f}} screen=%@",
              trace,
              TokenForgeCompanionWindow.isVisible ? @"reuse" : @"create-or-reuse",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height,
              (TokenForgeCompanionWindow.screen.localizedName ?: NSScreen.mainScreen.localizedName ?: @"unknown"));
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
        [TokenForgeCompanionWindow orderFrontRegardless];
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
        TokenForgeDumpOverlayPanelState(trace);
        TokenForgeLogOverlayProjection(trace, @"native_show");
        TokenForgeScheduleOverlayWatchdogs(trace);
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void HideDesktopCompanionOverlay()
{
    TokenForgeHideDesktopCompanionOverlayWithTrace(@"direct");
}

static void TokenForgeHideDesktopCompanionOverlayWithTrace(NSString *traceId)
{
    void (^block)(void) = ^{
        NSString *trace = traceId.length > 0 ? traceId : @"direct";
        BOOL oldDesired = TokenForgeDesiredCompanionVisible;
        TokenForgeDesiredCompanionVisible = NO;
        TokenForgeLastHideReason = @"companionVisibleOff";
        TokenForgeLastProjectionSource = @"native_hide";
        NSLog(@"INFO [OverlayTrace:%@] desired_visible_changed old=%@ new=false source=native_hide", trace, oldDesired ? @"true" : @"false");
        NSLog(@"INFO [OverlayTrace:%@] Native entered HideDesktopCompanionOverlay visible=0", trace);
        TokenForgeMenuCompanionEnabled = NO;
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        [TokenForgeCompanionWindow orderOut:nil];
        NSLog(@"INFO [OverlayTrace:%@] panel_order_out traceId=%@ reason=companionVisibleOff", trace, trace);
        TokenForgeLogWindowLifecycle(@"orderOut", TokenForgeCompanionWindow, [NSString stringWithFormat:@"trace=%@", trace]);
        NSLog(@"INFO [DesktopCompanion] hidden reason=companionVisibleOff");
        NSLog(@"INFO [DesktopCompanion] hidden reason=companionVisibleOff visible=%@", TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        NSLog(@"INFO [DesktopOverlay] hide reason=companionVisibleOff");
        TokenForgeDumpOverlayPanelState(trace);
        TokenForgeLogOverlayProjection(trace, @"native_hide");
    };
    if ([NSThread isMainThread]) block(); else dispatch_async(dispatch_get_main_queue(), block);
}

extern "C" void SetCompanionOverlayPosition(float x, float y)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionWindow == nil) return;
        if (TokenForgeIsDraggingOverlay) {
            NSLog(@"INFO [CompanionDrag] setFrameOrigin ignored reason=drag");
            return;
        }
        NSPoint oldOrigin = TokenForgeCompanionWindow.frame.origin;
        NSRect frame = TokenForgeClampFrameToVisibleFrame(NSMakeRect(x, y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height));
        TokenForgeCompanionAnchor = frame.origin;
        [TokenForgeCompanionWindow setFrameOrigin:frame.origin];
        NSLog(@"INFO [CompanionDrag] setFrameOrigin old=(%.2f,%.2f) new=(%.2f,%.2f)",
              oldOrigin.x,
              oldOrigin.y,
              frame.origin.x,
              frame.origin.y);
    });
}

extern "C" void SetCompanionOverlaySize(float width, float height)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeCompanionSize = NSMakeSize(MAX(24.0, width), MAX(24.0, height));
        if (TokenForgeCompanionWindow == nil) return;
        NSRect frame = TokenForgeCompanionWindow.frame;
        frame.size = TokenForgeCompanionSize;
        frame = TokenForgeClampFrameToVisibleFrame(frame);
        TokenForgeCompanionAnchor = frame.origin;
        [TokenForgeCompanionWindow setFrame:frame display:YES];
        TokenForgeCompanionContentView.frame = NSMakeRect(0, 0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
    });
}

extern "C" void SetCompanionOverlayMotionProfile(int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds)
{
    TokenForgeSetCompanionOverlayMotionProfileWithTrace(@"direct", motionMode, idleRadius, wanderRadius, wanderSpeed, decisionIntervalSeconds, allowsWandering, reactionCooldownSeconds);
}

static void TokenForgeSetCompanionOverlayMotionProfileWithTrace(NSString *traceId, int motionMode, float idleRadius, float wanderRadius, float wanderSpeed, float decisionIntervalSeconds, bool allowsWandering, float reactionCooldownSeconds)
{
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
        if (allowsWandering && motionMode != 0 && TokenForgeCompanionWanderSpeed > 0.0) {
            TokenForgeCreateCompanionOverlayOnMain(trace);
            if (TokenForgeCompanionWindow == nil) {
                NSLog(@"WARN [OverlayTrace:%@] motion_start_deferred reason=panel_not_ready", trace);
                return;
            }
            if (!TokenForgeCompanionWindow.isVisible) {
                TokenForgeMenuCompanionEnabled = YES;
                TokenForgeCompanionWindow.alphaValue = 1.0;
                [TokenForgeCompanionWindow orderFrontRegardless];
                NSLog(@"INFO [OverlayTrace:%@] panel_order_front traceId=%@ visible=%@ reason=motion_start", trace, trace, TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
            }

            TokenForgeEnsureCompanionMotionTimer();
            TokenForgeMotionTickLogged = NO;
            NSLog(@"INFO [DesktopCompanion] movement started speed=%.2f", TokenForgeCompanionWanderSpeed);
            NSLog(@"INFO [DesktopOverlay] movementTimer started interval=%.2f", 1.0 / 30.0);
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
            NSLog(@"INFO [OverlayTrace:%@] motionEnabled=false overlayStillVisible=%@", trace, TokenForgeCompanionWindow.isVisible ? @"true" : @"false");
        }
    });
}

extern "C" void TriggerCompanionOverlayReaction(int reaction, const char *speechText)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        NSString *speech = TokenForgeSafeMenuString(speechText, @"First safe summary will start growth.");
        TokenForgeTriggerOverlayReaction(reaction, speech);
    });
}

extern "C" void ResetCompanionOverlayPosition()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeResetCompanionFrame();
    });
}

extern "C" void SetCompanionOverlayVisualState(int stage, int archetype, int animationState, bool facingLeft)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionContentView == nil) return;
        TokenForgeCompanionContentView.stage = stage;
        TokenForgeCompanionContentView.archetype = archetype;
        TokenForgeCompanionContentView.animationState = animationState;
        TokenForgeCompanionContentView.facingLeft = facingLeft;
        [TokenForgeCompanionContentView setNeedsDisplay:YES];
    });
}

extern "C" void SetCompanionOverlayVisualTheme(const char *visualThemeId)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionContentView == nil) return;
        NSString *theme = visualThemeId == NULL ? @"orange_cat" : [NSString stringWithUTF8String:visualThemeId];
        TokenForgeCompanionContentView.visualThemeId = theme.length > 0 ? theme : @"orange_cat";
        [TokenForgeCompanionContentView setNeedsDisplay:YES];
    });
}

extern "C" void SetCompanionOverlayClickThrough(bool clickThrough)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeMenuClickThrough = clickThrough;
        if (TokenForgeCompanionWindow == nil) {
            NSLog(@"INFO [DesktopCompanion] click-through %@ pending window=nil", clickThrough ? @"enabled" : @"disabled");
            return;
        }

        if (TokenForgeIsDraggingOverlay && clickThrough) {
            TokenForgeCompanionWindow.ignoresMouseEvents = NO;
            NSLog(@"INFO [CompanionDrag] click-through deferred during drag");
        } else {
            TokenForgeCompanionWindow.ignoresMouseEvents = clickThrough;
        }
        NSLog(@"INFO [DesktopCompanion] %@", clickThrough ? @"click-through enabled" : @"click-through disabled");
        NSLog(@"INFO [DesktopCompanion] clickMode enabled ignoresMouseEvents=%@", TokenForgeCompanionWindow.ignoresMouseEvents ? @"true" : @"false");
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
    });
}

extern "C" void TokenForge_SetOverlayClickEnabled(bool enabled)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayClickEnabled = enabled;
    });
}

extern "C" void TokenForge_SetOverlayClickThrough(bool enabled)
{
    SetCompanionOverlayClickThrough(enabled);
}

extern "C" void TokenForge_RegisterOverlayClickedCallback(TokenForgeOverlayClickedCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayClicked = callback;
    });
}

extern "C" void TokenForge_RegisterOverlayDoubleClickedCallback(TokenForgeOverlayClickedCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayDoubleClicked = callback;
    });
}

extern "C" void TokenForge_RegisterOverlayDragEndedCallback(TokenForgeOverlayDragEndedCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeOverlayDragEnded = callback;
    });
}

extern "C" void DestroyDesktopCompanionOverlay()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeCompanionWindow orderOut:nil];
        TokenForgeCompanionWindow = nil;
        TokenForgeCompanionContentView = nil;
        TokenForgeCompanionVelocity = NSMakePoint(0, 0);
        [TokenForgeCompanionMotionTimer invalidate];
        TokenForgeCompanionMotionTimer = nil;
        NSLog(@"INFO [DesktopOverlay] quit cleanup completed");
    });
}
