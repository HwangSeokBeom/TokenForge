#import <Cocoa/Cocoa.h>
#include <math.h>
#include <dlfcn.h>
#include <limits.h>
#include <string.h>

@class TokenForgeAppLifecycleDelegate;
@class TokenForgeNativeDashboardController;
static TokenForgeAppLifecycleDelegate *TokenForgeEnsureLifecycleDelegate(void);
static TokenForgeNativeDashboardController *TokenForgeEnsureNativeDashboardController(void);
static void TokenForgeOpenNativeDashboardOnMain(void);
extern "C" void ShowDesktopCompanionOverlay(void);
extern "C" void HideDesktopCompanionOverlay(void);

@interface TokenForgeAppLifecycleDelegate : NSObject <NSApplicationDelegate, NSWindowDelegate>
@property(nonatomic, assign) id<NSApplicationDelegate> originalAppDelegate;
@property(nonatomic, assign) id<NSWindowDelegate> originalMainWindowDelegate;
@property(nonatomic, assign) NSWindow *mainWindow;
@property(nonatomic, strong) NSStatusItem *statusItem;
@property(nonatomic) BOOL explicitTerminationRequested;
@property(nonatomic) BOOL observingWindowNotifications;
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
@property(nonatomic, strong) NSString *visualThemeId;
@property(nonatomic, strong) NSString *speechText;
@property(nonatomic) NSTimeInterval speechExpiresAt;
@end

@interface TokenForgeDashboardCompanionPreviewView : TokenForgeCompanionView
@property(nonatomic) BOOL levelUpReady;
@property(nonatomic) CGFloat motionBounceAmplitude;
@property(nonatomic) CGFloat motionPulseFrequency;
@property(nonatomic, strong) NSTimer *previewTimer;
- (void)startDashboardPreviewAnimation;
- (void)stopDashboardPreviewAnimation;
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
static NSPoint TokenForgeDragStartMouse = {0, 0};
static NSPoint TokenForgeDragStartOrigin = {0, 0};
static NSPoint TokenForgeCompanionAnchor = {0, 0};

static NSRect TokenForgeClampFrameToVisibleFrame(NSRect frame);
static void TokenForgePersistCompanionFrame(NSRect frame);
static void TokenForgeTriggerOverlayReaction(NSInteger reaction, NSString *speech);

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

    NSGraphicsContext *context = [NSGraphicsContext currentContext];
    [context saveGraphicsState];
    context.imageInterpolation = NSImageInterpolationNone;

    CGFloat scale = MIN(self.bounds.size.width, self.bounds.size.height) / 24.0;
    NSAffineTransform *transform = [NSAffineTransform transform];
    [transform translateXBy:NSMidX(self.bounds) yBy:NSMidY(self.bounds)];
    if (self.facingLeft) {
        [transform scaleXBy:-1.0 yBy:1.0];
    }
    [transform translateXBy:0.0 yBy:self.visualOffsetY];
    [transform rotateByDegrees:self.visualRotation];
    CGFloat reactionScale = self.visualScale <= 0.01 ? 1.0 : self.visualScale;
    [transform scaleBy:reactionScale];
    [transform translateXBy:-12.0 * scale yBy:-12.0 * scale];
    [transform concat];

    NSColor *outline = [NSColor colorWithCalibratedRed:0.13 green:0.15 blue:0.19 alpha:1.0];
    NSColor *body = [self bodyColor];
    NSColor *accent = [self accentColor];
    NSColor *highlight = [NSColor colorWithCalibratedRed:1.0 green:0.96 blue:0.82 alpha:1.0];

    if (self.stage == 0) {
        [self ellipse:NSMakeRect(7 * scale, 4 * scale, 10 * scale, 16 * scale) color:outline];
        [self ellipse:NSMakeRect(8 * scale, 5 * scale, 8 * scale, 14 * scale) color:body];
        [self pixelX:11 y:14 scale:scale color:highlight];
        [self pixelX:12 y:15 scale:scale color:highlight];
    } else if (self.stage == 1) {
        [self ellipse:NSMakeRect(6 * scale, 4 * scale, 12 * scale, 16 * scale) color:outline];
        [self ellipse:NSMakeRect(7 * scale, 5 * scale, 10 * scale, 14 * scale) color:body];
        [outline setStroke];
        NSBezierPath *crack = [NSBezierPath bezierPath];
        [crack moveToPoint:NSMakePoint(10 * scale, 17 * scale)];
        [crack lineToPoint:NSMakePoint(13 * scale, 14 * scale)];
        [crack lineToPoint:NSMakePoint(10 * scale, 11 * scale)];
        [crack lineToPoint:NSMakePoint(14 * scale, 8 * scale)];
        crack.lineWidth = scale;
        [crack stroke];
        [self pixelX:16 y:16 scale:scale color:accent];
    } else {
        [self ellipse:NSMakeRect(6 * scale, 6 * scale, 12 * scale, 12 * scale) color:outline];
        [self ellipse:NSMakeRect(7 * scale, 7 * scale, 10 * scale, 10 * scale) color:body];
        [self rect:NSMakeRect(8 * scale, 3 * scale, 8 * scale, 5 * scale) color:outline];
        [self rect:NSMakeRect(9 * scale, 4 * scale, 6 * scale, 4 * scale) color:body];
        [self pixelX:10 y:12 scale:scale color:outline];
        [self pixelX:15 y:12 scale:scale color:outline];
        [self pixelX:11 y:9 scale:scale color:outline];
        [self pixelX:12 y:8 scale:scale color:outline];
        [self pixelX:13 y:8 scale:scale color:outline];
        [self pixelX:14 y:9 scale:scale color:outline];
        [self pixelX:9 y:15 scale:scale color:highlight];
        if (self.stage >= 3) {
            [self rect:NSMakeRect(5 * scale, 9 * scale, 2 * scale, 3 * scale) color:outline];
            [self rect:NSMakeRect(17 * scale, 9 * scale, 2 * scale, 3 * scale) color:outline];
            [self pixelX:5 y:10 scale:scale color:accent];
            [self pixelX:18 y:10 scale:scale color:accent];
        }
        if (self.stage >= 4) {
            [self rect:NSMakeRect(9 * scale, 18 * scale, 6 * scale, 2 * scale) color:outline];
            [self rect:NSMakeRect(10 * scale, 19 * scale, 4 * scale, 1 * scale) color:accent];
        }
        NSInteger step = self.animationState == 1 ? 1 : 0;
        [self rect:NSMakeRect(8 * scale, (2 - step) * scale, 2 * scale, 2 * scale) color:outline];
        [self rect:NSMakeRect(14 * scale, (2 + step) * scale, 2 * scale, 2 * scale) color:outline];
    }

    [context restoreGraphicsState];

    if (self.speechText.length > 0 && [NSDate timeIntervalSinceReferenceDate] < self.speechExpiresAt) {
        [self drawSpeechBubble:self.speechText];
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
        self.visualOffsetY = sin(now * (1.4 + pulse)) * amplitude;
        self.visualScale = self.levelUpReady ? 1.0 + fabs(sin(now * 3.0)) * 0.055 : 1.0 + sin(now * 1.2) * 0.018;
        self.visualRotation = self.levelUpReady ? sin(now * 3.8) * 2.6 : sin(now * 0.75) * 1.0;
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

@implementation TokenForgeCompanionOverlayWindow
- (BOOL)canBecomeKeyWindow { return YES; }
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
static NSString *TokenForgeMenuReaction = @"none";
static NSString *TokenForgeMenuStatusText = @"Repo: None · AI Agents: 0 connected";
static TokenForgeNativeDashboardController *TokenForgeDashboardController = nil;

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
    if (TokenForgeDashboardActionClicked != nil) {
        TokenForgeDashboardActionClicked(action);
    }
}

static NSImage *TokenForgeCreateStatusCompanionImage(NSInteger stage, NSInteger archetype)
{
    NSSize imageSize = NSMakeSize(20.0, 20.0);
    NSImage *image = [[NSImage alloc] initWithSize:imageSize];
    [image lockFocus];
    [[NSColor clearColor] setFill];
    NSRectFill(NSMakeRect(0, 0, imageSize.width, imageSize.height));

    TokenForgeCompanionView *view = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, imageSize.width, imageSize.height)];
    view.stage = stage;
    view.archetype = archetype;
    view.animationState = 0;
    view.facingLeft = NO;
    [view drawRect:view.bounds];

    [image unlockFocus];
    image.size = imageSize;
    [image setTemplate:NO];
    return image;
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
        NSLog(@"INFO [DesktopCompanion] idle/wander tick started");
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

    anchorFrame = TokenForgeClampFrameToVisibleFrame(anchorFrame);
    TokenForgeCompanionAnchor = anchorFrame.origin;

    CGFloat idleX = reacting ? sin(phase * 8.0) * 5.0 : sin(phase) * TokenForgeCompanionIdleRadius;
    CGFloat idleY = reacting ? fabs(sin(phase * 4.0)) * 10.0 : fabs(sin(phase * 0.75)) * 3.0;
    if (paused) {
        idleX = 0.0;
        idleY = 0.0;
    }

    NSRect displayFrame = NSMakeRect(TokenForgeCompanionAnchor.x + idleX, TokenForgeCompanionAnchor.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
    displayFrame = TokenForgeClampFrameToVisibleFrame(displayFrame);
    [TokenForgeCompanionWindow setFrameOrigin:displayFrame.origin];
    static NSTimeInterval TokenForgeLastMotionLogAt = 0.0;
    if (now - TokenForgeLastMotionLogAt > 2.0) {
        TokenForgeLastMotionLogAt = now;
        NSLog(@"INFO [DesktopCompanion] idle/wander position updated %.2f,%.2f", displayFrame.origin.x, displayFrame.origin.y);
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
}

static void TokenForgeCreateCompanionOverlayOnMain(void)
{
    TokenForgeEnsureLifecycleDelegate();
    if (TokenForgeCompanionWindow != nil) {
        return;
    }

    NSPoint origin = TokenForgeLoadCompanionOrigin();
    NSRect frame = TokenForgeClampFrameToVisibleFrame(NSMakeRect(origin.x, origin.y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height));
    TokenForgeCompanionAnchor = frame.origin;
    TokenForgeCompanionWindow = [[TokenForgeCompanionOverlayWindow alloc] initWithContentRect:frame
                                                                                    styleMask:NSWindowStyleMaskBorderless
                                                                                      backing:NSBackingStoreBuffered
                                                                                        defer:NO];
    TokenForgeCompanionWindow.backgroundColor = [NSColor clearColor];
    TokenForgeCompanionWindow.opaque = NO;
    TokenForgeCompanionWindow.hasShadow = NO;
    TokenForgeCompanionWindow.level = NSFloatingWindowLevel;
    TokenForgeCompanionWindow.acceptsMouseMovedEvents = YES;
    TokenForgeCompanionWindow.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary;
    TokenForgeCompanionWindow.ignoresMouseEvents = NO;
    TokenForgeCompanionContentView = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height)];
    TokenForgeCompanionContentView.visualThemeId = @"orange_cat";
    TokenForgeCompanionContentView.wantsLayer = YES;
    TokenForgeCompanionContentView.layerContentsRedrawPolicy = NSViewLayerContentsRedrawOnSetNeedsDisplay;
    TokenForgeCompanionWindow.contentView = TokenForgeCompanionContentView;
    TokenForgeEnsureCompanionMotionTimer();
    NSLog(@"INFO [DesktopCompanion] native overlay window initialized at %.2f,%.2f %.2fx%.2f", frame.origin.x, frame.origin.y, frame.size.width, frame.size.height);
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
@property(nonatomic, strong) NSString *activityFilter;
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

static NSDictionary *TokenForgeDefaultDashboardState(void)
{
    return @{
        @"appTitle": @"TokenForge",
        @"appName": @"TokenForge",
        @"subtitle": @"Turn your development activity into companion growth.",
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
        self.activityFilter = @"all";
    }
    return self;
}

- (void)showDashboard
{
    [self ensureDashboardWindow];
    [NSApp activateIgnoringOtherApps:YES];
    [self.dashboardWindow makeKeyAndOrderFront:nil];
    [self.dashboardWindow orderFrontRegardless];
}

- (void)hideDashboard
{
    [self.dashboardWindow orderOut:nil];
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
    [sidebar.widthAnchor constraintEqualToConstant:236.0].active = YES;
    [split addArrangedSubview:sidebar];

    NSStackView *sidebarStack = TokenForgeDashboardVerticalStack(10.0);
    sidebarStack.alignment = NSLayoutAttributeWidth;
    [sidebar addSubview:sidebarStack];
    TokenForgePinSubview(sidebarStack, sidebar, 22, 12, 16, 12);
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

    NSStackView *content = TokenForgeDashboardVerticalStack(18.0);
    content.alignment = NSLayoutAttributeWidth;
    [document addSubview:content];
    TokenForgePinSubview(content, document, 28, 30, 34, 30);

    [self populateDashboardContent:content];
    return root;
}

- (void)populateSidebar:(NSStackView *)stack
{
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSString *name = TokenForgeDashboardString(companion, @"name", @"Token");
    NSString *syncText = TokenForgeDashboardString(self.state, @"syncStatusText", @"Sync optional");

    NSView *thumbCard = TokenForgeDashboardCard();
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    [thumbCard.heightAnchor constraintGreaterThanOrEqualToConstant:132.0].active = YES;
    NSStackView *thumbStack = TokenForgeDashboardHorizontalStack(10.0);
    thumbStack.distribution = NSStackViewDistributionFill;
    [thumbCard addSubview:thumbStack];
    TokenForgePinSubview(thumbStack, thumbCard, 14, 12, 14, 12);
    TokenForgeCompanionView *icon = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 46, 46)];
    icon.translatesAutoresizingMaskIntoConstraints = NO;
    icon.stage = MAX(0, MIN(4, TokenForgeDashboardInteger(companion, @"stageIndex", 2)));
    [icon.widthAnchor constraintEqualToConstant:48.0].active = YES;
    [icon.heightAnchor constraintEqualToConstant:48.0].active = YES;
    [thumbStack addArrangedSubview:icon];
    NSStackView *labels = TokenForgeDashboardVerticalStack(2.0);
    labels.alignment = NSLayoutAttributeLeading;
    [labels addArrangedSubview:TokenForgeDashboardLabel(name, 15.0, NSFontWeightSemibold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"● %@", syncText], 11.0, NSFontWeightRegular, [NSColor systemGreenColor], 1)];
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ · Lv %ld", TokenForgeDashboardString(companion, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(companion, @"level", 1)], 11.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 1)];
    if (TokenForgeDashboardBool(companion, @"canLevelUp", NO)) {
        [labels addArrangedSubview:TokenForgeDashboardLabel(@"Level Up Ready", 11.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 1)];
    }
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%ld XP", (long)TokenForgeDashboardInteger(self.state, @"persistedCompanionXP", TokenForgeDashboardInteger(companion, @"xp", 0))], 11.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 1)];
    NSString *repositoryLabel = TokenForgeDashboardBool(repository, @"connected", NO)
        ? TokenForgeDashboardString(repository, @"name", @"Repository")
        : @"No repository connected";
    [labels addArrangedSubview:TokenForgeDashboardLabel(repositoryLabel, 11.0, NSFontWeightMedium, TokenForgeMutedTextColor(), 2)];
    [thumbStack addArrangedSubview:labels];
    [stack addArrangedSubview:thumbCard];

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

    NSStackView *header = TokenForgeDashboardHorizontalStack(16.0);
    header.distribution = NSStackViewDistributionFill;
    NSStackView *headerCopy = TokenForgeDashboardVerticalStack(4.0);
    [headerCopy addArrangedSubview:TokenForgeShellHeaderLabel([NSString stringWithFormat:@"%@ Dashboard", title], 27.0, NSFontWeightBold, 1)];
    [headerCopy addArrangedSubview:TokenForgeShellBodyLabel(subtitle, 2)];
    [headerCopy addArrangedSubview:TokenForgeShellBodyLabel(TokenForgeDashboardString(self.state, @"actionStatusText", @"Ready"), 2)];
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
    [content addArrangedSubview:header];

    if ([self.selectedNavItem isEqualToString:@"repository"]) {
        [content addArrangedSubview:[self repositoryScreen]];
        return;
    }

    if ([self.selectedNavItem isEqualToString:@"codexAgent"] || [self.selectedNavItem isEqualToString:@"aiAgents"]) {
        [content addArrangedSubview:[self agentsScreen]];
        return;
    }

    if ([self.selectedNavItem isEqualToString:@"activity"]) {
        [content addArrangedSubview:[self activityScreenWithActivity:activity review:review]];
        return;
    }

    [content addArrangedSubview:[self heroCardWithCompanion:companion activity:activity]];

    NSStackView *actionRow = TokenForgeDashboardHorizontalStack(14.0);
    actionRow.distribution = NSStackViewDistributionFillEqually;
    [actionRow addArrangedSubview:[self actionCardWithTitle:@"Repositories" state:TokenForgeDashboardString(repository, @"statusText", TokenForgeDashboardBool(repository, @"connected", NO) ? @"Connected" : @"Not selected") detail:TokenForgeDashboardBool(repository, @"connected", NO) ? [NSString stringWithFormat:@"%ld connected · %@", (long)TokenForgeDashboardInteger(repository, @"connectedCount", 1), TokenForgeDashboardString(repository, @"name", @"Repository")] : @"No repository connected. Choose a Git repository to start tracking local development growth." buttonTitle:TokenForgeDashboardBool(repository, @"connected", NO) ? @"Manage Repositories" : @"Add Repository" action:@selector(repository:) accent:[NSColor systemBlueColor]]];
    [actionRow addArrangedSubview:[self actionCardWithTitle:@"AI Agents" state:TokenForgeDashboardString(agents, @"statusText", TokenForgeDashboardString(agent, @"statusText", @"No agents connected")) detail:[NSString stringWithFormat:@"Last: %@ · Warnings: %ld · %@", TokenForgeDashboardString(agents, @"lastProvider", @"None"), (long)TokenForgeDashboardInteger(agents, @"warningCount", 0), TokenForgeDashboardString(agents, @"privacyText", @"Local aggregate only")] buttonTitle:@"Manage Agents" action:@selector(codexAgent:) accent:[NSColor systemPurpleColor]]];
    [actionRow addArrangedSubview:[self reviewCardWithActivity:activity review:review]];
    [content addArrangedSubview:actionRow];

    [content addArrangedSubview:[self growthSummaryCardWithActivity:activity]];
    [content addArrangedSubview:[self privacyCard]];
}

- (NSView *)heroCardWithCompanion:(NSDictionary *)companion activity:(NSDictionary *)activity
{
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *review = TokenForgeDashboardDictionary(self.state, @"review");
    NSView *card = TokenForgeDashboardCard();
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:236.0].active = YES;
    NSStackView *row = TokenForgeDashboardHorizontalStack(22.0);
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
    [copy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ is growing with %@", TokenForgeDashboardString(companion, @"name", @"Token"), TokenForgeDashboardString(repository, @"name", @"No repository")], 23.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
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
    [progress.widthAnchor constraintLessThanOrEqualToConstant:560.0].active = YES;
    NSString *carryForward = TokenForgeDashboardString(companion, @"carryForwardText", @"");
    if (carryForward.length > 0) {
        [copy addArrangedSubview:TokenForgeDashboardLabel(carryForward, 12.0, NSFontWeightSemibold, [NSColor systemOrangeColor], 2)];
    }
    NSString *reviewState = TokenForgeDashboardBool(review, @"pending", NO)
        ? [NSString stringWithFormat:@"Recent analysis: pending review · +%ld XP estimated", (long)TokenForgeDashboardInteger(review, @"estimatedXpDelta", 0)]
        : [NSString stringWithFormat:@"Recent analysis: %@", TokenForgeDashboardString(activity, @"state", @"No pending review")];
    [copy addArrangedSubview:TokenForgeLightCardBodyLabel(reviewState, 2)];
    [copy addArrangedSubview:TokenForgeLightCardBodyLabel(TokenForgeDashboardString(companion, @"levelUpStatusText", @"Earn more XP to level up."), 3)];
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
    } else {
        NSString *hiddenReason = TokenForgeDashboardString(companion, @"evolveActionHiddenReason", @"currentXP below requirement");
        [buttons addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"Evolve hidden: %@", hiddenReason], 1)];
    }
    [copy addArrangedSubview:buttons];
    [row addArrangedSubview:copy];

    NSView *previewContainer = [[NSView alloc] initWithFrame:NSZeroRect];
    previewContainer.translatesAutoresizingMaskIntoConstraints = NO;
    previewContainer.wantsLayer = YES;
    previewContainer.layer.masksToBounds = YES;
    previewContainer.layer.cornerRadius = 8.0;
    [previewContainer.widthAnchor constraintEqualToConstant:164.0].active = YES;
    [previewContainer.heightAnchor constraintEqualToConstant:164.0].active = YES;
    [previewContainer setContentHuggingPriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    [previewContainer setContentCompressionResistancePriority:NSLayoutPriorityRequired forOrientation:NSLayoutConstraintOrientationHorizontal];
    TokenForgeDashboardCompanionPreviewView *preview = [[TokenForgeDashboardCompanionPreviewView alloc] initWithFrame:NSMakeRect(0, 0, 136, 136)];
    preview.translatesAutoresizingMaskIntoConstraints = NO;
    preview.stage = MAX(0, MIN(4, TokenForgeDashboardInteger(companion, @"stageIndex", 0)));
    preview.archetype = 0;
    preview.animationState = canLevelUp ? 4 : 1;
    preview.visualThemeId = TokenForgeDashboardString(companion, @"skin", @"orange_cat");
    preview.levelUpReady = canLevelUp;
    NSDictionary *motion = TokenForgeDashboardDictionary(companion, @"motion");
    preview.motionBounceAmplitude = MAX(2.0, TokenForgeDashboardFloat(motion, @"bounceAmplitude", canLevelUp ? 8.0 : 3.0));
    preview.motionPulseFrequency = MAX(0.2, TokenForgeDashboardFloat(motion, @"pulseFrequency", canLevelUp ? 1.0 : 0.2));
    [previewContainer addSubview:preview];
    [NSLayoutConstraint activateConstraints:@[
        [preview.centerXAnchor constraintEqualToAnchor:previewContainer.centerXAnchor],
        [preview.centerYAnchor constraintEqualToAnchor:previewContainer.centerYAnchor],
        [preview.widthAnchor constraintEqualToConstant:136.0],
        [preview.heightAnchor constraintEqualToConstant:136.0]
    ]];
    [row addArrangedSubview:previewContainer];
    return card;
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
    NSButton *button = TokenForgeSecondaryButton(title, self, @selector(setActivityFilter:));
    NSString *normalized = key.length > 0 ? key : @"all";
    button.toolTip = normalized;
    NSString *current = self.activityFilter ?: @"all";
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
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:168.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(title, 13.0, NSFontWeightSemibold, accent ?: [NSColor systemBlueColor], 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(state ?: @"Not connected", 19.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(detail ?: @"", 4)];
    NSView *spacer = [NSView new];
    [spacer.heightAnchor constraintGreaterThanOrEqualToConstant:4.0].active = YES;
    [stack addArrangedSubview:spacer];
    [stack addArrangedSubview:TokenForgeSecondaryButton(buttonTitle, self, action)];
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
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(summary, 4)];
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
    [rowStack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"%@ · %@ · %@ · Source %@ · Mood %@%@", TokenForgeDashboardString(repository, @"companion", @"Egg · Lv 1"), TokenForgeDashboardString(repository, @"xpStatusText", @"0/250 XP"), TokenForgeDashboardString(repository, @"lastAnalyzed", @"Not analyzed"), TokenForgeDashboardString(repository, @"recentGrowthSource", @"None"), TokenForgeDashboardString(repository, @"motionMood", @"idle"), TokenForgeDashboardBool(repository, @"archived", NO) ? @" · Archived" : @""], 3)];
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
    NSButton *viewGrowth = TokenForgeSecondaryButton(@"View Growth", self, @selector(viewRepositoryGrowthAction:));
    viewGrowth.toolTip = identifier;
    viewGrowth.enabled = TokenForgeDashboardBool(repository, @"canViewGrowth", YES);
    [buttons addArrangedSubview:viewGrowth];
    if (TokenForgeDashboardBool(repository, @"canEvolve", NO)) {
        NSButton *evolve = TokenForgePrimaryButton(@"Evolve", self, @selector(levelUpCompanion:));
        evolve.toolTip = identifier;
        evolve.enabled = TokenForgeDashboardBool(repository, @"selected", NO);
        if (!evolve.enabled) {
            evolve.toolTip = @"Set this repository active before evolving its companion.";
        }
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
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel([NSString stringWithFormat:@"%@ · %@ · warnings %ld", TokenForgeDashboardString(provider, @"statusText", @"Not connected"), TokenForgeDashboardString(provider, @"safeCandidateSummary", @"No local source selected"), (long)TokenForgeDashboardInteger(provider, @"warningCount", 0)], 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(@"Local aggregate only. Raw prompts, code, file content, and commands are not stored in progress data.", 2)];
    NSStackView *buttons = TokenForgeDashboardHorizontalStack(8.0);
    NSButton *connect = TokenForgeSecondaryButton(@"Connect", self, @selector(connectAgentAction:));
    connect.toolTip = providerId;
    connect.enabled = TokenForgeDashboardBool(provider, @"canConnect", !TokenForgeDashboardBool(provider, @"connected", NO));
    if (!connect.enabled) connect.toolTip = TokenForgeDashboardBool(provider, @"connected", NO) ? @"Already connected." : TokenForgeDashboardString(provider, @"disabledReason", @"Detect or choose a folder first.");
    [buttons addArrangedSubview:connect];
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
    NSString *filter = self.activityFilter ?: @"all";
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
    [stack addArrangedSubview:TokenForgeLightCardBodyLabel(summary, 3)];
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
    }
    if (notification.object == self.settingsWindow) {
        [self.settingsWindow orderOut:nil];
    }
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
- (void)selectRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.setActive:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)analyzeRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.analyze:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)viewRepositoryGrowthAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; self.activityFilter = value.length > 0 ? [@"repo:" stringByAppendingString:value] : @"active"; [self setSelectedNav:@"activity" action:"navigation.openActivity" showDashboard:YES]; }
- (void)disconnectRepositoryAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @""; NSString *payload = [NSString stringWithFormat:@"repository.archive:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)connectAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.connect:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)autoDetectAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.autoDetect:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)chooseAgentFolderAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.chooseFolder:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)analyzeAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.analyze:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)disconnectAgentAction:(id)sender { NSString *value = [(NSButton *)sender toolTip] ?: @"codex"; NSString *payload = [NSString stringWithFormat:@"agent.disconnect:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)reviewActivity:(id)sender { [self setSelectedNav:@"activity" action:"navigation.openActivity" showDashboard:YES]; }
- (void)approveReview:(id)sender { TokenForgeSendDashboardAction("review.saveGrowth"); }
- (void)viewReviewDetails:(id)sender { NSString *value = TokenForgeDashboardString(TokenForgeDashboardDictionary(self.state, @"review"), @"reviewId", @""); NSString *payload = [NSString stringWithFormat:@"review.viewDetails:%@", value]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)discardReview:(id)sender { TokenForgeSendDashboardAction("review.discard"); }
- (void)setActivityFilter:(id)sender { self.activityFilter = [(NSButton *)sender toolTip] ?: @"all"; [self rebuildDashboardIfNeeded]; }
- (void)toggleCompanionVisible:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"companionVisible", YES)]; [self setStateBool:@"companionVisible" enabled:enabled]; [self sendBoolAction:@"toggleCompanionVisible" enabled:enabled]; }
- (void)toggleWanderEnabled:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"wanderEnabled", YES)]; [self setStateBool:@"wanderEnabled" enabled:enabled]; [self sendBoolAction:@"setWanderEnabled" enabled:enabled]; }
- (void)toggleClickReactionEnabled:(id)sender { BOOL enabled = [self settingsBoolValueFromSender:sender fallback:TokenForgeDashboardBool(self.state, @"clickReactionEnabled", YES)]; [self setStateBool:@"clickReactionEnabled" enabled:enabled]; [self sendBoolAction:@"setClickReactionEnabled" enabled:enabled]; }
- (void)toggleLaunchAtLogin:(id)sender { NSLog(@"INFO [NativeDashboard] launch at login unavailable in unsigned build"); }
- (void)changeSkin:(id)sender { NSString *skin = [(NSButton *)sender toolTip] ?: @"orange_cat"; [self setSelectedCompanionSkin:skin]; NSString *payload = [NSString stringWithFormat:@"changeCompanionSkin:%@", skin]; TokenForgeSendDashboardAction(payload.UTF8String); }
- (void)resetCompanionPosition:(id)sender { TokenForgeResetCompanionFrame(); TokenForgeSendDashboardAction("resetCompanionPosition"); }
- (void)closeSettings:(id)sender { [self.settingsWindow orderOut:nil]; }
- (void)resetLocalState:(id)sender { TokenForgeSendDashboardAction("reset_local_state"); }
- (void)quit:(id)sender { TokenForgeSendDashboardAction("quit"); TokenForgeEnsureLifecycleDelegate().explicitTerminationRequested = YES; [NSApp terminate:nil]; }

@end

static TokenForgeNativeDashboardController *TokenForgeEnsureNativeDashboardController(void)
{
    if (TokenForgeDashboardController == nil) {
        TokenForgeDashboardController = [[TokenForgeNativeDashboardController alloc] init];
    }

    TokenForgeEnsureLifecycleDelegate();
    return TokenForgeDashboardController;
}

static void TokenForgeOpenNativeDashboardOnMain(void)
{
    [TokenForgeEnsureNativeDashboardController() showDashboard];
}

@implementation TokenForgeAppLifecycleDelegate

- (void)install
{
    id<NSApplicationDelegate> currentDelegate = [NSApp delegate];
    if (currentDelegate != self) {
        self.originalAppDelegate = currentDelegate;
        [NSApp setDelegate:self];
    }

    [self installStatusItem];
    [self installWindowNotifications];
    [self installMainWindowHook];

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
    self.statusItem.button.image = TokenForgeCreateStatusCompanionImage(TokenForgeMenuStageIndex, TokenForgeMenuArchetypeIndex);
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
        self.statusAnimationTimer = [NSTimer scheduledTimerWithTimeInterval:0.75 repeats:YES block:^(NSTimer *timer) {
            self.statusAnimationFrame = (self.statusAnimationFrame + 1) % 4;
            [self updateStatusItemMenu];
        }];
        [[NSRunLoop mainRunLoop] addTimer:self.statusAnimationTimer forMode:NSRunLoopCommonModes];
    }
    NSLog(@"INFO [NativeDashboard] status item installed");
}

- (void)updateStatusItemMenu
{
    if (self.statusItem == nil || self.statusItem.menu == nil) {
        return;
    }

    BOOL pulse = TokenForgeMenuCanLevelUp || TokenForgeMenuAnalysisRunning || [TokenForgeMenuReaction isEqualToString:@"GrowthSaved"] || [TokenForgeMenuReaction isEqualToString:@"LevelUp"];
    NSInteger animatedStage = TokenForgeMenuStageIndex;
    NSInteger animatedArchetype = TokenForgeMenuArchetypeIndex;
    if (pulse && self.statusAnimationFrame % 2 == 1) {
        animatedArchetype = MIN(6, animatedArchetype + 1);
    }
    if (TokenForgeMenuAnalysisRunning) {
        animatedStage = MAX(0, MIN(4, TokenForgeMenuStageIndex + (self.statusAnimationFrame % 2)));
    }
    self.statusItem.button.title = TokenForgeMenuStatusText ?: @"";
    self.statusItem.button.image = TokenForgeCreateStatusCompanionImage(animatedStage, animatedArchetype);
    self.statusItem.button.imagePosition = NSImageLeft;
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
    if (TokenForgeLooksLikeMainWindow((NSWindow *)notification.object)) {
        [self installMainWindowHook];
    }
}

- (void)windowDidBecomeKey:(NSNotification *)notification
{
    if (TokenForgeLooksLikeMainWindow((NSWindow *)notification.object)) {
        [self installMainWindowHook];
    }
}

- (void)showMainWindow
{
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
}

- (void)hideMainWindow
{
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
    return NO;
}

- (NSApplicationTerminateReply)applicationShouldTerminate:(NSApplication *)sender
{
    self.explicitTerminationRequested = YES;
    [TokenForgeCompanionWindow orderOut:nil];
    TokenForgeCompanionWindow = nil;
    TokenForgeCompanionContentView = nil;

    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationShouldTerminate:)]) {
        return [self.originalAppDelegate applicationShouldTerminate:sender];
    }

    return NSTerminateNow;
}

- (void)applicationDidFinishLaunching:(NSNotification *)notification
{
    [self installMainWindowHook];
    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationDidFinishLaunching:)]) {
        [self.originalAppDelegate applicationDidFinishLaunching:notification];
    }
}

- (BOOL)applicationShouldHandleReopen:(NSApplication *)sender hasVisibleWindows:(BOOL)flag
{
    [self showMainWindow];
    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationShouldHandleReopen:hasVisibleWindows:)]) {
        [self.originalAppDelegate applicationShouldHandleReopen:sender hasVisibleWindows:flag];
    }

    return YES;
}

- (void)applicationDidBecomeActive:(NSNotification *)notification
{
    if (![self isMainWindowVisible]) {
        [self showMainWindow];
    }

    if (self.originalAppDelegate != nil && [self.originalAppDelegate respondsToSelector:@selector(applicationDidBecomeActive:)]) {
        [self.originalAppDelegate applicationDidBecomeActive:notification];
    }
}

- (BOOL)windowShouldClose:(NSWindow *)sender
{
    if (self.explicitTerminationRequested || sender != self.mainWindow) {
        if (self.originalMainWindowDelegate != nil && [self.originalMainWindowDelegate respondsToSelector:@selector(windowShouldClose:)]) {
            return [self.originalMainWindowDelegate windowShouldClose:sender];
        }

        return YES;
    }

    [sender orderOut:nil];
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
    TokenForgeCreateCompanionOverlayOnMain();
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

    [TokenForgeLifecycleDelegate install];
    return TokenForgeLifecycleDelegate;
}

extern "C" bool InstallTokenForgeMacAppLifecycle()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeEnsureLifecycleDelegate();
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
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureNativeDashboardController() showDashboard];
    });
}

extern "C" void TokenForge_HideDashboardWindow()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureNativeDashboardController() hideDashboard];
    });
}

extern "C" void TokenForge_ToggleDashboardWindow()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureNativeDashboardController() toggleDashboard];
    });
}

extern "C" void TokenForge_ShowSettingsWindow()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureNativeDashboardController() showSettings];
    });
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
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureNativeDashboardController() updateState:state];
        [TokenForgeEnsureNativeDashboardController() setMenuBarStatus:state];
    });
}

extern "C" void TokenForge_SetMenuBarStatus(const char *json)
{
    NSDictionary *state = TokenForgeParseJsonDictionary(json);
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeEnsureNativeDashboardController() setMenuBarStatus:state];
    });
}

extern "C" void TokenForge_SetCompanionVisible(bool visible)
{
    if (visible) {
        ShowDesktopCompanionOverlay();
    } else {
        HideDesktopCompanionOverlay();
    }
}

extern "C" void TokenForge_RegisterDashboardActionCallback(TokenForgeDashboardActionCallback callback)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeDashboardActionClicked = callback;
        NSLog(@"INFO [NativeDashboard] action callback registered");
    });
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
        TokenForgeCreateCompanionOverlayOnMain();
    } else {
        dispatch_sync(dispatch_get_main_queue(), ^{
            TokenForgeCreateCompanionOverlayOnMain();
        });
    }

    return true;
}

extern "C" void ShowDesktopCompanionOverlay()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeCreateCompanionOverlayOnMain();
        TokenForgeEnsureCompanionMotionTimer();
        TokenForgeMenuCompanionEnabled = YES;
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        TokenForgeCompanionWindow.ignoresMouseEvents = TokenForgeMenuClickThrough;
        NSLog(@"INFO [DesktopCompanion] click-through %@", TokenForgeMenuClickThrough ? @"true" : @"false");
        [TokenForgeCompanionWindow makeKeyAndOrderFront:nil];
        [TokenForgeCompanionWindow orderFrontRegardless];
        [TokenForgeCompanionWindow displayIfNeeded];
        NSLog(@"INFO [DesktopCompanion] native overlay show requested visible=%@ frame=%.2f,%.2f %.2fx%.2f",
              TokenForgeCompanionWindow.isVisible ? @"YES" : @"NO",
              TokenForgeCompanionWindow.frame.origin.x,
              TokenForgeCompanionWindow.frame.origin.y,
              TokenForgeCompanionWindow.frame.size.width,
              TokenForgeCompanionWindow.frame.size.height);
    });
}

extern "C" void HideDesktopCompanionOverlay()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeMenuCompanionEnabled = NO;
        [TokenForgeEnsureLifecycleDelegate() updateStatusItemMenu];
        [TokenForgeCompanionWindow orderOut:nil];
    });
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
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeCompanionMotionMode = motionMode;
        TokenForgeCompanionIdleRadius = MAX(0.0, MIN(24.0, idleRadius));
        TokenForgeCompanionWanderRadius = MAX(0.0, MIN(360.0, wanderRadius));
        TokenForgeCompanionWanderSpeed = MAX(0.0, MIN(80.0, wanderSpeed));
        TokenForgeCompanionDecisionInterval = MAX(0.8, MIN(10.0, decisionIntervalSeconds));
        TokenForgeCompanionAllowsWandering = allowsWandering;
        TokenForgeCompanionReactionCooldown = MAX(0.4, MIN(4.0, reactionCooldownSeconds));
        TokenForgeEnsureCompanionMotionTimer();
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
        if (TokenForgeCompanionWindow == nil) return;
        TokenForgeMenuClickThrough = clickThrough;
        if (TokenForgeIsDraggingOverlay && clickThrough) {
            TokenForgeCompanionWindow.ignoresMouseEvents = NO;
            NSLog(@"INFO [CompanionDrag] click-through deferred during drag");
        } else {
            TokenForgeCompanionWindow.ignoresMouseEvents = clickThrough;
        }
        NSLog(@"INFO [DesktopCompanion] %@", clickThrough ? @"click-through enabled" : @"click-through disabled");
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
    });
}
