#import <Cocoa/Cocoa.h>

@interface TokenForgeCompanionView : NSView
@property(nonatomic) NSInteger stage;
@property(nonatomic) NSInteger archetype;
@property(nonatomic) NSInteger animationState;
@property(nonatomic) BOOL facingLeft;
@end

@implementation TokenForgeCompanionView
- (BOOL)isOpaque { return NO; }

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

@interface TokenForgeAppLifecycleDelegate : NSObject <NSApplicationDelegate, NSWindowDelegate>
@property(nonatomic, assign) id<NSApplicationDelegate> originalAppDelegate;
@property(nonatomic, assign) id<NSWindowDelegate> originalMainWindowDelegate;
@property(nonatomic, assign) NSWindow *mainWindow;
@property(nonatomic, strong) NSStatusItem *statusItem;
@property(nonatomic) BOOL explicitTerminationRequested;
@property(nonatomic) BOOL observingWindowNotifications;
- (void)install;
- (void)installMainWindowHook;
- (void)showMainWindow;
- (void)hideMainWindow;
- (BOOL)isMainWindowVisible;
@end

static NSWindow *TokenForgeCompanionWindow = nil;
static TokenForgeCompanionView *TokenForgeCompanionContentView = nil;
static NSSize TokenForgeCompanionSize = {96.0, 96.0};
static TokenForgeAppLifecycleDelegate *TokenForgeLifecycleDelegate = nil;

static NSRect TokenForgeVisibleFrame()
{
    NSScreen *screen = [NSScreen mainScreen];
    return screen ? [screen visibleFrame] : NSMakeRect(0, 0, 800, 600);
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
    self.statusItem.button.title = @"TF";
    self.statusItem.button.toolTip = @"TokenForge";

    NSMenu *menu = [[NSMenu alloc] initWithTitle:@"TokenForge"];
    NSMenuItem *showItem = [[NSMenuItem alloc] initWithTitle:@"Show TokenForge" action:@selector(showTokenForgeFromStatusItem:) keyEquivalent:@""];
    showItem.target = self;
    [menu addItem:showItem];

    NSMenuItem *hideItem = [[NSMenuItem alloc] initWithTitle:@"Hide Dashboard" action:@selector(hideTokenForgeFromStatusItem:) keyEquivalent:@""];
    hideItem.target = self;
    [menu addItem:hideItem];

    [menu addItem:[NSMenuItem separatorItem]];

    NSMenuItem *quitItem = [[NSMenuItem alloc] initWithTitle:@"Quit TokenForge" action:@selector(quitTokenForgeFromStatusItem:) keyEquivalent:@"q"];
    quitItem.target = self;
    [menu addItem:quitItem];

    self.statusItem.menu = menu;
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
    [self showMainWindow];
}

- (void)hideTokenForgeFromStatusItem:(id)sender
{
    [self hideMainWindow];
}

- (void)quitTokenForgeFromStatusItem:(id)sender
{
    self.explicitTerminationRequested = YES;
    [NSApp terminate:nil];
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

static TokenForgeAppLifecycleDelegate *TokenForgeEnsureLifecycleDelegate()
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
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeEnsureLifecycleDelegate();
        if (TokenForgeCompanionWindow != nil) {
            return;
        }

        NSRect visible = TokenForgeVisibleFrame();
        NSRect frame = NSMakeRect(NSMinX(visible) + 120.0, NSMinY(visible) + 120.0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
        TokenForgeCompanionWindow = [[NSWindow alloc] initWithContentRect:frame
                                                                styleMask:NSWindowStyleMaskBorderless
                                                                  backing:NSBackingStoreBuffered
                                                                    defer:NO];
        TokenForgeCompanionWindow.backgroundColor = [NSColor clearColor];
        TokenForgeCompanionWindow.opaque = NO;
        TokenForgeCompanionWindow.hasShadow = NO;
        TokenForgeCompanionWindow.level = NSFloatingWindowLevel;
        TokenForgeCompanionWindow.collectionBehavior = NSWindowCollectionBehaviorCanJoinAllSpaces | NSWindowCollectionBehaviorFullScreenAuxiliary;
        TokenForgeCompanionWindow.ignoresMouseEvents = YES;
        TokenForgeCompanionContentView = [[TokenForgeCompanionView alloc] initWithFrame:NSMakeRect(0, 0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height)];
        TokenForgeCompanionContentView.wantsLayer = YES;
        TokenForgeCompanionContentView.layerContentsRedrawPolicy = NSViewLayerContentsRedrawOnSetNeedsDisplay;
        TokenForgeCompanionWindow.contentView = TokenForgeCompanionContentView;
    });
    return true;
}

extern "C" void ShowDesktopCompanionOverlay()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionWindow == nil) {
            CreateDesktopCompanionOverlay();
        }
        [TokenForgeCompanionWindow orderFrontRegardless];
    });
}

extern "C" void HideDesktopCompanionOverlay()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeCompanionWindow orderOut:nil];
    });
}

extern "C" void SetCompanionOverlayPosition(float x, float y)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (TokenForgeCompanionWindow == nil) return;
        NSRect visible = TokenForgeVisibleFrame();
        CGFloat clampedX = MIN(MAX(x, NSMinX(visible)), NSMaxX(visible) - TokenForgeCompanionSize.width);
        CGFloat clampedY = MIN(MAX(y, NSMinY(visible)), NSMaxY(visible) - TokenForgeCompanionSize.height);
        [TokenForgeCompanionWindow setFrameOrigin:NSMakePoint(clampedX, clampedY)];
    });
}

extern "C" void SetCompanionOverlaySize(float width, float height)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        TokenForgeCompanionSize = NSMakeSize(MAX(24.0, width), MAX(24.0, height));
        if (TokenForgeCompanionWindow == nil) return;
        NSRect frame = TokenForgeCompanionWindow.frame;
        frame.size = TokenForgeCompanionSize;
        [TokenForgeCompanionWindow setFrame:frame display:YES];
        TokenForgeCompanionContentView.frame = NSMakeRect(0, 0, TokenForgeCompanionSize.width, TokenForgeCompanionSize.height);
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
        TokenForgeCompanionWindow.ignoresMouseEvents = clickThrough;
    });
}

extern "C" void DestroyDesktopCompanionOverlay()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [TokenForgeCompanionWindow orderOut:nil];
        TokenForgeCompanionWindow = nil;
        TokenForgeCompanionContentView = nil;
    });
}
