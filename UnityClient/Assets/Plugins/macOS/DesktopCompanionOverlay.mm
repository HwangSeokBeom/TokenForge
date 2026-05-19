#import <Cocoa/Cocoa.h>
#include <math.h>
#include <dlfcn.h>
#include <limits.h>
#include <string.h>

@class TokenForgeAppLifecycleDelegate;
static TokenForgeAppLifecycleDelegate *TokenForgeEnsureLifecycleDelegate(void);

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
static TokenForgeOverlayClickedCallback TokenForgeOverlayClicked = nil;
static TokenForgeOverlayClickedCallback TokenForgeOverlayDoubleClicked = nil;
static TokenForgeOverlayDragEndedCallback TokenForgeOverlayDragEnded = nil;
static TokenForgeMenuActionCallback TokenForgeMenuActionClicked = nil;
static BOOL TokenForgeOverlayClickEnabled = YES;
static BOOL TokenForgeIsDraggingOverlay = NO;
static BOOL TokenForgeDragExceededThreshold = NO;
static BOOL TokenForgeMotionTickLogged = NO;
static NSPoint TokenForgeDragStartMouse = {0, 0};
static NSPoint TokenForgeDragStartOrigin = {0, 0};

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

    NSLog(@"INFO [DesktopCompanion] mouseDown");
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
    NSLog(@"INFO [DesktopCompanion] mouseDragged");
    CGFloat dx = currentMouse.x - TokenForgeDragStartMouse.x;
    CGFloat dy = currentMouse.y - TokenForgeDragStartMouse.y;
    if (!TokenForgeDragExceededThreshold && hypot(dx, dy) > 4.0) {
        TokenForgeDragExceededThreshold = YES;
        NSLog(@"INFO [DesktopCompanion] drag started");
    }

    if (TokenForgeDragExceededThreshold) {
        NSRect frame = self.window.frame;
        frame.origin = NSMakePoint(TokenForgeDragStartOrigin.x + dx, TokenForgeDragStartOrigin.y + dy);
        frame = TokenForgeClampFrameToVisibleFrame(frame);
        [self.window setFrameOrigin:frame.origin];
    }
}

- (void)mouseUp:(NSEvent *)event
{
    if (!TokenForgeOverlayClickEnabled || self.window == nil) {
        TokenForgeIsDraggingOverlay = NO;
        return;
    }

    NSLog(@"INFO [DesktopCompanion] mouseUp");
    NSRect frame = TokenForgeClampFrameToVisibleFrame(self.window.frame);
    [self.window setFrameOrigin:frame.origin];
    if (TokenForgeDragExceededThreshold) {
        TokenForgePersistCompanionFrame(frame);
        NSLog(@"INFO [DesktopCompanion] drag ended with x/y %.2f,%.2f", frame.origin.x, frame.origin.y);
        if (TokenForgeOverlayDragEnded != nil) {
            TokenForgeOverlayDragEnded(frame.origin.x, frame.origin.y);
        }
    } else if (event.clickCount >= 2) {
        NSLog(@"INFO [DesktopCompanion] double click dashboard restore requested");
        if (TokenForgeOverlayDoubleClicked != nil) {
            TokenForgeOverlayDoubleClicked();
        } else {
            [TokenForgeEnsureLifecycleDelegate() showMainWindow];
        }
    } else {
        NSLog(@"INFO [DesktopCompanion] single click reaction triggered");
        TokenForgeTriggerOverlayReaction(0, @"Ready to grow!");
        if (TokenForgeOverlayClicked != nil) {
            TokenForgeOverlayClicked();
        }
    }

    TokenForgeIsDraggingOverlay = NO;
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
static NSPoint TokenForgeCompanionAnchor = {0, 0};
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
static BOOL TokenForgeMenuClickThrough = NO;
static BOOL TokenForgeMenuCanAnalyze = NO;
static BOOL TokenForgeMenuCanSync = NO;

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
    if (TokenForgeMenuActionClicked != nil) {
        TokenForgeMenuActionClicked(action);
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

    self.statusItem = [[NSStatusBar systemStatusBar] statusItemWithLength:24.0];
    self.statusItem.button.title = @"";
    self.statusItem.button.image = TokenForgeCreateStatusCompanionImage(TokenForgeMenuStageIndex, TokenForgeMenuArchetypeIndex);
    self.statusItem.button.imagePosition = NSImageOnly;
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

    NSMenuItem *agentItem = [[NSMenuItem alloc] initWithTitle:@"AI Agent: No agent connected" action:nil keyEquivalent:@""];
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

    NSMenuItem *enableItem = [[NSMenuItem alloc] initWithTitle:@"Enable Desktop Companion" action:@selector(enableDesktopCompanionFromStatusItem:) keyEquivalent:@""];
    enableItem.target = self;
    enableItem.tag = 1006;
    [menu addItem:enableItem];

    NSMenuItem *disableItem = [[NSMenuItem alloc] initWithTitle:@"Disable Desktop Companion" action:@selector(disableDesktopCompanionFromStatusItem:) keyEquivalent:@""];
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

    NSMenuItem *connectAgentItem = [[NSMenuItem alloc] initWithTitle:@"Connect AI Agent" action:@selector(connectAiAgentFromStatusItem:) keyEquivalent:@""];
    connectAgentItem.target = self;
    connectAgentItem.tag = 1016;
    [menu addItem:connectAgentItem];

    NSMenuItem *analyzeItem = [[NSMenuItem alloc] initWithTitle:@"Run Analysis" action:@selector(analyzeCurrentRepositoryFromStatusItem:) keyEquivalent:@""];
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
}

- (void)updateStatusItemMenu
{
    if (self.statusItem == nil || self.statusItem.menu == nil) {
        return;
    }

    self.statusItem.button.title = @"";
    self.statusItem.button.image = TokenForgeCreateStatusCompanionImage(TokenForgeMenuStageIndex, TokenForgeMenuArchetypeIndex);
    self.statusItem.button.imagePosition = NSImageOnly;
    [[self.statusItem.menu itemWithTag:1001] setTitle:[NSString stringWithFormat:@"%@ · %@ · Level %ld", TokenForgeMenuCompanionName, TokenForgeMenuStage, (long)MAX(1, TokenForgeMenuLevel)]];
    [[self.statusItem.menu itemWithTag:1002] setTitle:[NSString stringWithFormat:@"Repository: %@", TokenForgeMenuRepositoryAlias]];
    [[self.statusItem.menu itemWithTag:1003] setTitle:[NSString stringWithFormat:@"AI Agent: %@", TokenForgeMenuAgentStatus]];
    [[self.statusItem.menu itemWithTag:1004] setTitle:[NSString stringWithFormat:@"Safe Sync: %@", TokenForgeMenuSyncStatus]];
    [[self.statusItem.menu itemWithTag:1005] setTitle:[NSString stringWithFormat:@"Desktop Companion: %@", TokenForgeMenuCompanionEnabled ? (TokenForgeMenuClickThrough ? @"Native Active · Click-through" : @"Native Active · Interactive") : @"Off"]];
    [self.statusItem.menu itemWithTag:1006].enabled = !TokenForgeMenuCompanionEnabled;
    [self.statusItem.menu itemWithTag:1007].enabled = TokenForgeMenuCompanionEnabled;
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
    [self showMainWindow];
}

- (void)hideTokenForgeFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("hide_dashboard");
    [self hideMainWindow];
}

- (void)enableDesktopCompanionFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("enable_desktop_companion");
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
    TokenForgeSendMenuAction("disable_desktop_companion");
    TokenForgeMenuCompanionEnabled = NO;
    [TokenForgeCompanionWindow orderOut:nil];
    [self updateStatusItemMenu];
}

- (void)toggleClickThroughFromStatusItem:(id)sender
{
    TokenForgeMenuClickThrough = !TokenForgeMenuClickThrough;
    TokenForgeSendMenuAction("toggle_click_through");
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
    TokenForgeSendMenuAction("analyze_current_repository");
    [self showMainWindow];
}

- (void)addRepositoryFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("add_repository");
    [self showMainWindow];
}

- (void)connectAiAgentFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("connect_ai_agent");
    [self showMainWindow];
}

- (void)syncNowFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("sync_now");
    [self showMainWindow];
}

- (void)settingsFromStatusItem:(id)sender
{
    TokenForgeSendMenuAction("settings");
    [self showMainWindow];
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
        NSRect frame = TokenForgeClampFrameToVisibleFrame(NSMakeRect(x, y, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height));
        TokenForgeCompanionAnchor = frame.origin;
        [TokenForgeCompanionWindow setFrameOrigin:frame.origin];
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
        TokenForgeCompanionWindow.ignoresMouseEvents = clickThrough;
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
