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
@property(nonatomic, strong) NSString *speechText;
@property(nonatomic) NSTimeInterval speechExpiresAt;
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
    return [NSColor colorWithCalibratedRed:0.56 green:0.79 blue:0.90 alpha:1.0];
}

- (NSColor *)accentColor
{
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
static NSString *TokenForgeMenuRepositoryAlias = @"Local Repository";
static NSString *TokenForgeMenuAgentStatus = @"No agent connected";
static NSString *TokenForgeMenuSyncStatus = @"Local only";
static BOOL TokenForgeMenuCompanionEnabled = NO;
static BOOL TokenForgeMenuCanAnalyze = NO;
static BOOL TokenForgeMenuCanSync = NO;
static NSString *TokenForgeMenuStatusText = @"Cdx 0% · CI 0% · Gem 0%";
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

static void TokenForgeAssignMenuString(NSString **target, const char *value, NSString *fallback)
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

@interface TokenForgeNativeDashboardController : NSObject <NSWindowDelegate>
@property(nonatomic, strong) NSWindow *dashboardWindow;
@property(nonatomic, strong) NSWindow *settingsWindow;
@property(nonatomic, strong) NSDictionary *state;
@property(nonatomic, strong) NSString *selectedNavItem;
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
    return [NSColor colorWithCalibratedRed:0.965 green:0.953 blue:0.925 alpha:1.0];
}

static NSColor *TokenForgeSidebarBackgroundColor(void)
{
    return [NSColor colorWithCalibratedWhite:1.0 alpha:0.46];
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
    return [NSColor labelColor];
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

static NSInteger TokenForgeDashboardInteger(NSDictionary *dictionary, NSString *key, NSInteger fallback)
{
    id value = dictionary[key];
    if ([value respondsToSelector:@selector(integerValue)]) {
        return [value integerValue];
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
        @"statusText": @"Cdx 0% · CI 0% · Gem 0%",
        @"companion": @{@"name": @"Token", @"stage": @"Egg", @"stageIndex": @0, @"level": @1, @"xp": @0, @"xpToNextLevel": @250, @"mood": @"active", @"skin": @"orange_cat"},
        @"repository": @{@"connected": @NO, @"name": @"", @"status": @"not_selected", @"statusText": @"Not selected"},
        @"codexAgent": @{@"connected": @NO, @"status": @"not_connected", @"statusText": @"Not connected"},
        @"activity": @{@"todaySummary": @"No activity yet", @"state": @"No pending review", @"code": @0, @"focus": @0, @"debug": @0, @"design": @0, @"sync": @0},
        @"review": @{@"pending": @NO, @"summary": @"No pending review", @"source": @"", @"confidence": @"", @"estimatedXpDelta": @0, @"codeDelta": @0, @"focusDelta": @0, @"debugDelta": @0, @"designDelta": @0, @"syncDelta": @0, @"warnings": @""}
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

@implementation TokenForgeNativeDashboardController

- (instancetype)init
{
    self = [super init];
    if (self != nil) {
        self.state = TokenForgeDefaultDashboardState();
        self.selectedNavItem = @"dashboard";
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
    TokenForgeMenuCompanionName = [TokenForgeDashboardString(companion, @"name", @"Token") copy];
    TokenForgeMenuStage = [TokenForgeDashboardString(companion, @"stage", @"Egg") copy];
    TokenForgeMenuStageIndex = MAX(0, MIN(4, TokenForgeDashboardInteger(companion, @"stageIndex", 0)));
    TokenForgeMenuLevel = MAX(1, TokenForgeDashboardInteger(companion, @"level", 1));
    TokenForgeMenuRepositoryAlias = [TokenForgeDashboardString(repository, @"name", TokenForgeDashboardBool(repository, @"connected", NO) ? @"Local Repository" : @"Not selected") copy];
    TokenForgeMenuAgentStatus = [TokenForgeDashboardString(agent, @"statusText", @"Not connected") copy];
    TokenForgeMenuSyncStatus = [TokenForgeDashboardString(state.count > 0 ? state : self.state, @"syncStatusText", @"Sync optional") copy];
    TokenForgeMenuCompanionEnabled = TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"companionVisible", TokenForgeMenuCompanionEnabled);
    TokenForgeMenuClickThrough = !TokenForgeDashboardBool(state.count > 0 ? state : self.state, @"clickReactionEnabled", !TokenForgeMenuClickThrough);
    TokenForgeMenuCanAnalyze = YES;

    NSString *statusText = TokenForgeDashboardString(state, @"statusText", nil);
    if (statusText.length == 0) {
        NSDictionary *activity = TokenForgeDashboardDictionary(state.count > 0 ? state : self.state, @"activity");
        statusText = [NSString stringWithFormat:@"Cdx %ld%% · CI %ld%% · Gem %ld%%",
                      (long)TokenForgeDashboardInteger(activity, @"code", 0),
                      (long)TokenForgeDashboardInteger(activity, @"focus", 0),
                      (long)TokenForgeDashboardInteger(activity, @"design", 0)];
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
    self.dashboardWindow.minSize = NSMakeSize(1000, 640);
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
    [sidebar.widthAnchor constraintEqualToConstant:260.0].active = YES;
    [split addArrangedSubview:sidebar];

    NSStackView *sidebarStack = TokenForgeDashboardVerticalStack(10.0);
    sidebarStack.alignment = NSLayoutAttributeWidth;
    [sidebar addSubview:sidebarStack];
    TokenForgePinSubview(sidebarStack, sidebar, 24, 18, 18, 18);
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
    NSString *connection = TokenForgeDashboardString(self.state, @"connection", @"local");
    NSString *syncText = TokenForgeDashboardString(self.state, @"syncStatusText", @"Sync optional");

    NSView *thumbCard = TokenForgeDashboardCard();
    [thumbCard.heightAnchor constraintGreaterThanOrEqualToConstant:112.0].active = YES;
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
    [labels addArrangedSubview:TokenForgeDarkSidebarLabel(name, 15.0, NSFontWeightSemibold, 1)];
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"● %@ connected", connection.capitalizedString], 11.0, NSFontWeightRegular, [NSColor systemGreenColor], 1)];
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ · Lv %ld", TokenForgeDashboardString(companion, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(companion, @"level", 1)], 11.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 1)];
    [labels addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ XP", TokenForgeDashboardString(companion, @"xp", @"0")], 11.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 1)];
    [thumbStack addArrangedSubview:labels];
    [stack addArrangedSubview:thumbCard];

    NSArray<NSArray<NSString *> *> *items = @[
        @[@"Dashboard", @"dashboard", @"dashboard:"],
        @[@"Repository", @"repository", @"repository:"],
        @[@"Codex Agent", @"codexAgent", @"codexAgent:"],
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
    NSTextField *footer = TokenForgeDashboardLabel([NSString stringWithFormat:@"v0.18 · AppKit shell\n%@ · Local-first", syncText], 11.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 2);
    [stack addArrangedSubview:footer];
}

- (void)populateDashboardContent:(NSStackView *)content
{
    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    NSDictionary *repository = TokenForgeDashboardDictionary(self.state, @"repository");
    NSDictionary *agent = TokenForgeDashboardDictionary(self.state, @"codexAgent");
    NSDictionary *activity = TokenForgeDashboardDictionary(self.state, @"activity");
    NSDictionary *review = TokenForgeDashboardDictionary(self.state, @"review");

    NSString *title = TokenForgeDashboardString(self.state, @"appTitle", TokenForgeDashboardString(self.state, @"appName", @"TokenForge"));
    NSString *subtitle = TokenForgeDashboardString(self.state, @"subtitle", @"Turn your development activity into companion growth.");
    NSString *syncText = TokenForgeDashboardString(self.state, @"syncStatusText", @"Sync optional");

    NSStackView *header = TokenForgeDashboardHorizontalStack(16.0);
    header.distribution = NSStackViewDistributionFill;
    NSStackView *headerCopy = TokenForgeDashboardVerticalStack(4.0);
    [headerCopy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ Dashboard", title], 26.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1)];
    [headerCopy addArrangedSubview:TokenForgeLightCardBodyLabel(subtitle, 2)];
    [header addArrangedSubview:headerCopy];
    NSStackView *headerActions = TokenForgeDashboardHorizontalStack(10.0);
    headerActions.distribution = NSStackViewDistributionFill;
    [headerActions addArrangedSubview:[self pillLabel:[NSString stringWithFormat:@"Active · Local · %@", syncText]]];
    [headerActions addArrangedSubview:TokenForgePrimaryButton(@"Run Analysis", self, @selector(runAnalysis:))];
    [headerActions addArrangedSubview:TokenForgeSecondaryButton(@"Connect Repository", self, @selector(connectRepository:))];
    [header addArrangedSubview:headerActions];
    [content addArrangedSubview:header];

    [content addArrangedSubview:[self heroCardWithCompanion:companion activity:activity]];

    NSStackView *actionRow = TokenForgeDashboardHorizontalStack(14.0);
    actionRow.distribution = NSStackViewDistributionFillEqually;
    [actionRow addArrangedSubview:[self actionCardWithTitle:@"Repository" state:TokenForgeDashboardString(repository, @"statusText", TokenForgeDashboardBool(repository, @"connected", NO) ? @"Connected" : @"Not selected") detail:TokenForgeDashboardBool(repository, @"connected", NO) ? TokenForgeDashboardString(repository, @"name", @"Local Repository") : @"Choose a local repository. TokenForge stores only aggregate growth signals." buttonTitle:TokenForgeDashboardBool(repository, @"connected", NO) ? @"Change Repository" : @"Add Repository" action:@selector(connectRepository:) accent:[NSColor systemBlueColor]]];
    [actionRow addArrangedSubview:[self actionCardWithTitle:@"Codex Agent" state:TokenForgeDashboardString(agent, @"statusText", TokenForgeDashboardBool(agent, @"connected", NO) ? @"Connected" : @"Not connected") detail:@"Detect local Codex activity or select an approved agent log folder." buttonTitle:@"Connect Codex Agent" action:@selector(connectCodexAgent:) accent:[NSColor systemPurpleColor]]];
    [actionRow addArrangedSubview:[self reviewCardWithActivity:activity review:review]];
    [content addArrangedSubview:actionRow];

    [content addArrangedSubview:[self growthSummaryCardWithActivity:activity]];
    [content addArrangedSubview:[self privacyCard]];
}

- (NSView *)heroCardWithCompanion:(NSDictionary *)companion activity:(NSDictionary *)activity
{
    NSView *card = TokenForgeDashboardCard();
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:220.0].active = YES;
    NSStackView *row = TokenForgeDashboardHorizontalStack(22.0);
    row.distribution = NSStackViewDistributionFill;
    [card addSubview:row];
    TokenForgePinSubview(row, card, 24, 24, 24, 24);

    NSStackView *copy = TokenForgeDashboardVerticalStack(8.0);
    copy.alignment = NSLayoutAttributeLeading;
    NSString *xp = TokenForgeDashboardString(companion, @"xp", @"0");
    NSInteger xpNext = MAX(1, TokenForgeDashboardInteger(companion, @"xpToNextLevel", 250));
    [copy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ is ready to grow", TokenForgeDashboardString(companion, @"name", @"Token")], 23.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    [copy addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%@ stage · Level %ld · %@/%ld XP", TokenForgeDashboardString(companion, @"stage", @"Egg"), (long)TokenForgeDashboardInteger(companion, @"level", 1), xp, (long)xpNext], 13.0, NSFontWeightMedium, TokenForgeLightCardSecondaryTextColor(), 2)];
    NSProgressIndicator *progress = [[NSProgressIndicator alloc] initWithFrame:NSZeroRect];
    progress.translatesAutoresizingMaskIntoConstraints = NO;
    progress.indeterminate = NO;
    progress.minValue = 0.0;
    progress.maxValue = xpNext;
    progress.doubleValue = MIN(xpNext, MAX(0, TokenForgeDashboardInteger(companion, @"xp", 0)));
    [progress.heightAnchor constraintEqualToConstant:8.0].active = YES;
    [copy addArrangedSubview:progress];
    [progress.widthAnchor constraintGreaterThanOrEqualToConstant:360.0].active = YES;
    [copy addArrangedSubview:TokenForgeLightCardBodyLabel(@"Start with a repository or Codex log. TokenForge reviews aggregate activity, then turns approved work into companion growth.", 3)];
    [copy addArrangedSubview:TokenForgePrimaryButton(@"Run Analysis", self, @selector(runAnalysis:))];
    [row addArrangedSubview:copy];

    TokenForgeCompanionView *preview = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 130, 130)];
    preview.translatesAutoresizingMaskIntoConstraints = NO;
    preview.stage = MAX(0, MIN(4, TokenForgeDashboardInteger(companion, @"stageIndex", 0)));
    preview.archetype = 0;
    preview.animationState = 1;
    [preview.widthAnchor constraintEqualToConstant:172.0].active = YES;
    [preview.heightAnchor constraintEqualToConstant:172.0].active = YES;
    [row addArrangedSubview:preview];
    return card;
}

- (NSButton *)sidebarButton:(NSString *)title navKey:(NSString *)navKey action:(SEL)action
{
    NSButton *button = TokenForgeDashboardButton(title, self, action);
    BOOL selected = [navKey isEqualToString:self.selectedNavItem ?: @"dashboard"];
    button.bordered = NO;
    button.wantsLayer = YES;
    button.layer.cornerRadius = 8.0;
    button.layer.backgroundColor = selected ? [NSColor colorWithCalibratedRed:0.18 green:0.37 blue:0.84 alpha:0.14].CGColor : [NSColor clearColor].CGColor;
    button.alignment = NSTextAlignmentLeft;
    if (@available(macOS 10.14, *)) {
        button.contentTintColor = selected ? TokenForgeSelectedBlueColor() : [NSColor labelColor];
    }
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

- (NSView *)actionCardWithTitle:(NSString *)title state:(NSString *)state detail:(NSString *)detail buttonTitle:(NSString *)buttonTitle action:(SEL)action accent:(NSColor *)accent
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 16.0, 8.0);
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:168.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(title, 13.0, NSFontWeightSemibold, accent ?: [NSColor systemBlueColor], 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(state ?: @"Not connected", 19.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 2)];
    [stack addArrangedSubview:TokenForgeLightCardCaptionLabel(detail ?: @"", 4)];
    [stack addArrangedSubview:TokenForgeSecondaryButton(buttonTitle, self, action)];
    return card;
}

- (NSView *)reviewCardWithActivity:(NSDictionary *)activity review:(NSDictionary *)review
{
    BOOL pending = TokenForgeDashboardBool(review, @"pending", NO) || TokenForgeDashboardInteger(self.state, @"pendingReviewCount", 0) > 0;
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
    if (pending) {
        [buttons addArrangedSubview:TokenForgePrimaryButton(@"Approve", self, @selector(approveReview:))];
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Discard", self, @selector(discardReview:))];
    } else {
        [buttons addArrangedSubview:TokenForgeSecondaryButton(@"Run Analysis", self, @selector(runAnalysis:))];
    }
    [stack addArrangedSubview:buttons];
    return card;
}

- (NSView *)growthSummaryCardWithActivity:(NSDictionary *)activity
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 12.0);
    [stack addArrangedSubview:TokenForgeLightCardTitleLabel(@"Growth Summary")];
    NSStackView *stats = TokenForgeDashboardHorizontalStack(12.0);
    stats.distribution = NSStackViewDistributionFillEqually;
    [stats addArrangedSubview:[self statTile:@"Code" value:TokenForgeDashboardInteger(activity, @"code", TokenForgeDashboardInteger(self.state, @"codeStat", 0)) accent:[NSColor systemBlueColor]]];
    [stats addArrangedSubview:[self statTile:@"Focus" value:TokenForgeDashboardInteger(activity, @"focus", TokenForgeDashboardInteger(self.state, @"focusStat", 0)) accent:[NSColor systemGreenColor]]];
    [stats addArrangedSubview:[self statTile:@"Debug" value:TokenForgeDashboardInteger(activity, @"debug", TokenForgeDashboardInteger(self.state, @"debugStat", 0)) accent:[NSColor systemOrangeColor]]];
    [stats addArrangedSubview:[self statTile:@"Design" value:TokenForgeDashboardInteger(activity, @"design", TokenForgeDashboardInteger(self.state, @"designStat", 0)) accent:[NSColor systemPinkColor]]];
    [stats addArrangedSubview:[self statTile:@"Sync" value:TokenForgeDashboardInteger(activity, @"sync", TokenForgeDashboardInteger(self.state, @"syncStat", 0)) accent:[NSColor systemTealColor]]];
    [stack addArrangedSubview:stats];
    NSString *summary = TokenForgeDashboardString(self.state, @"lastRunSummary", @"No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.");
    [stack addArrangedSubview:TokenForgeLightCardBodyLabel(summary, 3)];
    return card;
}

- (NSView *)statTile:(NSString *)title value:(NSInteger)value accent:(NSColor *)accent
{
    NSStackView *stack = nil;
    NSView *tile = TokenForgeCardWithStack(&stack, 12.0, 5.0);
    tile.layer.backgroundColor = [NSColor colorWithCalibratedWhite:1.0 alpha:0.68].CGColor;
    [tile.heightAnchor constraintGreaterThanOrEqualToConstant:82.0].active = YES;
    [stack addArrangedSubview:TokenForgeDashboardLabel(title, 11.0, NSFontWeightMedium, accent ?: [NSColor systemBlueColor], 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel([NSString stringWithFormat:@"%ld", (long)value], 22.0, NSFontWeightBold, TokenForgeLightCardPrimaryTextColor(), 1)];
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
    root.layer.backgroundColor = TokenForgeDashboardBackgroundColor().CGColor;
    root.translatesAutoresizingMaskIntoConstraints = NO;

    NSScrollView *scrollView = [[NSScrollView alloc] initWithFrame:NSZeroRect];
    scrollView.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.hasVerticalScroller = YES;
    scrollView.hasHorizontalScroller = NO;
    scrollView.drawsBackground = NO;
    [root addSubview:scrollView];
    TokenForgePinSubview(scrollView, root, 0, 0, 0, 0);

    TokenForgeFlippedView *document = [[TokenForgeFlippedView alloc] initWithFrame:NSMakeRect(0, 0, 840, 1000)];
    document.translatesAutoresizingMaskIntoConstraints = NO;
    scrollView.documentView = document;
    [document.widthAnchor constraintEqualToAnchor:scrollView.contentView.widthAnchor].active = YES;

    NSStackView *content = TokenForgeDashboardVerticalStack(16.0);
    content.alignment = NSLayoutAttributeWidth;
    [document addSubview:content];
    TokenForgePinSubview(content, document, 30, 34, 30, 34);

    NSDictionary *companion = TokenForgeDashboardDictionary(self.state, @"companion");
    [content addArrangedSubview:TokenForgeDashboardLabel(@"TokenForge Settings", 25.0, NSFontWeightBold, [NSColor labelColor], 1)];
    [content addArrangedSubview:TokenForgeDashboardLabel(@"Native shell preferences for the companion overlay and local-only activity flow.", 13.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 2)];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Companion visible" detail:@"Show the desktop companion independently from dashboard windows." enabled:TokenForgeDashboardBool(self.state, @"companionVisible", TokenForgeMenuCompanionEnabled) action:@selector(toggleCompanionVisible:) actionName:@"toggleCompanionVisible" interactive:YES]];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Wander movement" detail:@"Allow subtle idle movement while TokenForge is running." enabled:TokenForgeDashboardBool(self.state, @"wanderEnabled", YES) action:@selector(toggleWanderEnabled:) actionName:@"setWanderEnabled" interactive:YES]];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Click reaction" detail:@"Let the companion react when clicked. Turn this off for click-through mode." enabled:TokenForgeDashboardBool(self.state, @"clickReactionEnabled", YES) action:@selector(toggleClickReactionEnabled:) actionName:@"setClickReactionEnabled" interactive:YES]];
    [content addArrangedSubview:[self settingsSwitchCardWithTitle:@"Launch at login" detail:@"Coming soon. This setting is intentionally disabled until the signed login item is added." enabled:NO action:@selector(toggleLaunchAtLogin:) actionName:@"setLaunchAtLogin" interactive:NO]];

    NSView *gridCard = TokenForgeDashboardCard();
    [gridCard.heightAnchor constraintGreaterThanOrEqualToConstant:220.0].active = YES;
    NSStackView *grid = TokenForgeDashboardVerticalStack(10.0);
    [gridCard addSubview:grid];
    TokenForgePinSubview(grid, gridCard, 18, 18, 18, 18);
    [grid addArrangedSubview:TokenForgeDashboardLabel(@"Companion skin", 15.0, NSFontWeightSemibold, [NSColor labelColor], 1)];
    NSStackView *skins = TokenForgeDashboardHorizontalStack(10.0);
    skins.distribution = NSStackViewDistributionFillEqually;
    NSArray<NSString *> *skinNames = @[@"Orange Cat", @"White Cat", @"Calico", @"Black Cat", @"Retriever", @"Runner"];
    NSString *selectedSkin = [TokenForgeDashboardString(companion, @"skin", @"orange_cat") stringByReplacingOccurrencesOfString:@"_" withString:@" "];
    for (NSString *skin in skinNames) {
        [skins addArrangedSubview:[self skinTile:skin selected:[skin.lowercaseString containsString:selectedSkin.lowercaseString]]];
    }
    [grid addArrangedSubview:skins];
    [content addArrangedSubview:gridCard];

    [content addArrangedSubview:[self localDataCard]];

    NSStackView *toolbar = TokenForgeDashboardHorizontalStack(10.0);
    toolbar.distribution = NSStackViewDistributionFill;
    [toolbar addArrangedSubview:TokenForgeDashboardButton(@"Reset Position", self, @selector(resetCompanionPosition:))];
    [toolbar addArrangedSubview:TokenForgeDashboardButton(@"Close", self, @selector(closeSettings:))];
    [content addArrangedSubview:toolbar];
    return root;
}

- (NSView *)settingsSwitchCardWithTitle:(NSString *)title detail:(NSString *)detail enabled:(BOOL)enabled action:(SEL)action actionName:(NSString *)actionName interactive:(BOOL)interactive
{
    NSView *card = TokenForgeDashboardCard();
    [card.heightAnchor constraintGreaterThanOrEqualToConstant:82.0].active = YES;
    NSStackView *row = TokenForgeDashboardHorizontalStack(12.0);
    row.distribution = NSStackViewDistributionFill;
    [card addSubview:row];
    TokenForgePinSubview(row, card, 14, 18, 14, 18);
    NSStackView *copy = TokenForgeDashboardVerticalStack(4.0);
    [copy addArrangedSubview:TokenForgeDashboardLabel(title, 14.0, NSFontWeightSemibold, [NSColor labelColor], 1)];
    [copy addArrangedSubview:TokenForgeDashboardLabel(detail, 12.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 2)];
    [row addArrangedSubview:copy];
    NSButton *toggle = [[NSButton alloc] initWithFrame:NSZeroRect];
    toggle.translatesAutoresizingMaskIntoConstraints = NO;
    [toggle setButtonType:NSButtonTypeSwitch];
    toggle.title = @"";
    toggle.state = enabled ? NSControlStateValueOn : NSControlStateValueOff;
    toggle.enabled = interactive;
    toggle.target = self;
    toggle.action = action;
    toggle.toolTip = actionName;
    [row addArrangedSubview:toggle];
    return card;
}

- (NSView *)skinTile:(NSString *)title selected:(BOOL)selected
{
    NSButton *tile = TokenForgeDashboardButton(@"", self, @selector(changeSkin:));
    tile.wantsLayer = YES;
    tile.bordered = NO;
    tile.layer.backgroundColor = TokenForgeCardBackgroundColor().CGColor;
    tile.layer.cornerRadius = 10.0;
    tile.layer.borderWidth = selected ? 2.0 : 1.0;
    tile.layer.borderColor = (selected ? [NSColor systemGreenColor] : [NSColor colorWithCalibratedWhite:0.0 alpha:0.08]).CGColor;
    tile.toolTip = [[title lowercaseString] stringByReplacingOccurrencesOfString:@" " withString:@"_"];
    [tile.heightAnchor constraintGreaterThanOrEqualToConstant:122.0].active = YES;
    NSStackView *stack = TokenForgeDashboardVerticalStack(6.0);
    stack.alignment = NSLayoutAttributeCenterX;
    [tile addSubview:stack];
    TokenForgePinSubview(stack, tile, 12, 10, 10, 10);
    TokenForgeCompanionView *icon = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, 58, 58)];
    icon.translatesAutoresizingMaskIntoConstraints = NO;
    icon.stage = 2;
    [icon.widthAnchor constraintEqualToConstant:58.0].active = YES;
    [icon.heightAnchor constraintEqualToConstant:58.0].active = YES;
    NSTextField *label = TokenForgeDashboardLabel(title, 12.0, NSFontWeightMedium, [NSColor labelColor], 2);
    label.alignment = NSTextAlignmentCenter;
    [stack addArrangedSubview:icon];
    [stack addArrangedSubview:label];
    return tile;
}

- (NSView *)localDataCard
{
    NSStackView *stack = nil;
    NSView *card = TokenForgeCardWithStack(&stack, 18.0, 7.0);
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Local data and Safe Sync", 15.0, NSFontWeightSemibold, [NSColor labelColor], 1)];
    [stack addArrangedSubview:TokenForgeDashboardLabel(@"Local progress is stored on this Mac. Safe Sync is optional and only sends sanitized aggregate summaries when connected.", 13.0, NSFontWeightRegular, TokenForgeMutedTextColor(), 3)];
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

- (void)dashboard:(id)sender { [self setSelectedNav:@"dashboard" action:"dashboard" showDashboard:YES]; }
- (void)repository:(id)sender { [self setSelectedNav:@"repository" action:"repository" showDashboard:YES]; }
- (void)codexAgent:(id)sender { [self setSelectedNav:@"codexAgent" action:"codexAgent" showDashboard:YES]; }
- (void)activity:(id)sender { [self setSelectedNav:@"activity" action:"activity" showDashboard:YES]; }
- (void)settings:(id)sender { [self setSelectedNav:@"settings" action:"settings" showDashboard:YES]; [self showSettings]; }
- (void)homepage:(id)sender { TokenForgeSendDashboardAction("homepage"); if (TokenForgeDashboardActionClicked == nil) [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"https://github.com/HwangSeokBeom/TokenForge"]]; }
- (void)reportIssue:(id)sender { TokenForgeSendDashboardAction("reportIssue"); if (TokenForgeDashboardActionClicked == nil) [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"https://github.com/HwangSeokBeom/TokenForge/issues"]]; }
- (void)runAnalysis:(id)sender { TokenForgeSendDashboardAction("runAnalysis"); }
- (void)connectRepository:(id)sender { [self setSelectedNav:@"repository" action:"connectRepository" showDashboard:YES]; }
- (void)connectCodexAgent:(id)sender { [self setSelectedNav:@"codexAgent" action:"connectCodexAgent" showDashboard:YES]; }
- (void)reviewActivity:(id)sender { [self setSelectedNav:@"activity" action:"reviewActivity" showDashboard:YES]; }
- (void)approveReview:(id)sender { TokenForgeSendDashboardAction("approveReview"); }
- (void)discardReview:(id)sender { TokenForgeSendDashboardAction("discardReview"); }
- (void)toggleCompanionVisible:(id)sender { BOOL enabled = [(NSButton *)sender state] == NSControlStateValueOn; [self setStateBool:@"companionVisible" enabled:enabled]; [self sendBoolAction:@"toggleCompanionVisible" enabled:enabled]; }
- (void)toggleWanderEnabled:(id)sender { BOOL enabled = [(NSButton *)sender state] == NSControlStateValueOn; [self setStateBool:@"wanderEnabled" enabled:enabled]; [self sendBoolAction:@"setWanderEnabled" enabled:enabled]; }
- (void)toggleClickReactionEnabled:(id)sender { BOOL enabled = [(NSButton *)sender state] == NSControlStateValueOn; [self setStateBool:@"clickReactionEnabled" enabled:enabled]; [self sendBoolAction:@"setClickReactionEnabled" enabled:enabled]; }
- (void)toggleLaunchAtLogin:(id)sender { BOOL enabled = [(NSButton *)sender state] == NSControlStateValueOn; [self sendBoolAction:@"setLaunchAtLogin" enabled:enabled]; }
- (void)changeSkin:(id)sender { NSString *skin = [(NSButton *)sender toolTip] ?: @"orange_cat"; NSString *payload = [NSString stringWithFormat:@"changeCompanionSkin:%@", skin]; TokenForgeSendDashboardAction(payload.UTF8String); }
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

    NSMenuItem *repositoryItem = [[NSMenuItem alloc] initWithTitle:@"Repository: Local Repository" action:nil keyEquivalent:@""];
    repositoryItem.enabled = NO;
    repositoryItem.tag = 1002;
    [menu addItem:repositoryItem];

    NSMenuItem *agentItem = [[NSMenuItem alloc] initWithTitle:@"Codex Agent: Not connected" action:nil keyEquivalent:@""];
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

    NSMenuItem *connectAgentItem = [[NSMenuItem alloc] initWithTitle:@"Connect Codex Agent" action:@selector(connectAiAgentFromStatusItem:) keyEquivalent:@""];
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
    NSLog(@"INFO [NativeDashboard] status item installed");
}

- (void)updateStatusItemMenu
{
    if (self.statusItem == nil || self.statusItem.menu == nil) {
        return;
    }

    self.statusItem.button.title = TokenForgeMenuStatusText ?: @"";
    self.statusItem.button.image = TokenForgeCreateStatusCompanionImage(TokenForgeMenuStageIndex, TokenForgeMenuArchetypeIndex);
    self.statusItem.button.imagePosition = NSImageLeft;
    [[self.statusItem.menu itemWithTag:1001] setTitle:[NSString stringWithFormat:@"%@ · %@ · Level %ld", TokenForgeMenuCompanionName, TokenForgeMenuStage, (long)MAX(1, TokenForgeMenuLevel)]];
    [[self.statusItem.menu itemWithTag:1002] setTitle:[NSString stringWithFormat:@"Repository: %@", TokenForgeMenuRepositoryAlias]];
    [[self.statusItem.menu itemWithTag:1003] setTitle:[NSString stringWithFormat:@"Codex Agent: %@", TokenForgeMenuAgentStatus]];
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
    [self.statusItem.menu itemWithTag:1010].enabled = YES;
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
    TokenForgeSendMenuAction("refresh_activity");
    [TokenForgeEnsureNativeDashboardController() showDashboard];
}

- (void)addRepositoryFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("add_repository");
    [TokenForgeEnsureNativeDashboardController() showDashboard];
}

- (void)connectAiAgentFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("connect_ai_agent");
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
        TokenForgeAssignMenuString(&TokenForgeMenuRepositoryAlias, repositoryAlias, @"Local Repository");
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
