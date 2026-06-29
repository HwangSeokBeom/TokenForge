using System;
using System.Collections.Generic;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public enum NativeDashboardAction
    {
        Dashboard,
        ShowDashboard,
        HideDashboard,
        ToggleDashboard,
        Repository,
        CodexAgent,
        Activity,
        Settings,
        Homepage,
        ReportIssue,
        Quit,
        RunAnalysis,
        RunRepositoryAnalysis,
        RunAgentAnalysis,
        SafeSync,
        ConnectRepository,
        ChangeRepository,
        ChooseRepositoryFolder,
        ConnectCodexAgent,
        ConnectAiAgent,
        ConnectAgent,
        SelectCodexLogFolder,
        ManageAgents,
        LevelUpCompanion,
        SelectRepository,
        OpenActiveCompanionDashboard,
        OpenRepositoryCompanionDashboard,
        AnalyzeRepository,
        DisconnectRepository,
        DetectAgent,
        AutoDetectAgent,
        ChooseAgentFolder,
        AnalyzeAgent,
        DisconnectAgent,
        SaveGrowth,
        SaveReview,
        ApproveReview,
        DiscardReview,
        ViewReviewDetails,
        ReviewActivity,
        TokenShop,
        Wardrobe,
        Onboarding,
        SetOnboardingStep,
        SkipOnboarding,
        CompleteOnboarding,
        ResetOnboarding,
        PurchaseTokenShopItem,
        SelectShopRepositoryTarget,
        SelectShopAgentTarget,
        SelectShopCategory,
        EquipTokenShopItem,
        PreviewTokenShopItem,
        OpenAgentConnect,
        ToggleCompanionVisible,
        ShowCompanion,
        HideCompanion,
        ChangeCompanionSkin,
        SetLaunchAtLogin,
        SetWanderEnabled,
        EnableWander,
        DisableWander,
        EnableDrag,
        EnableClickThrough,
        DisableClickThrough,
        SetClickReactionEnabled,
        SetClickThroughEnabled,
        EnableClick,
        DisableClick,
        ResetCompanionPosition,
        ResetLocalState,
        SelectRepositoryZodiacMascot,
        Unsupported
    }

    public sealed class NativeDashboardActionRequest
    {
        public NativeDashboardActionRequest(NativeDashboardAction action, string rawAction, string value = "", string traceId = "")
        {
            Action = action;
            RawAction = rawAction ?? string.Empty;
            Value = value ?? string.Empty;
            TraceId = string.IsNullOrWhiteSpace(traceId) ? "none" : traceId.Trim();
        }

        public NativeDashboardAction Action { get; }
        public string RawAction { get; }
        public string Value { get; }
        public string TraceId { get; }

        public bool BoolValue(bool fallback = false)
        {
            if (bool.TryParse(Value, out var parsed))
            {
                return parsed;
            }

            if (string.Equals(Value, "1", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(Value, "0", StringComparison.Ordinal))
            {
                return false;
            }

            return fallback;
        }
    }

    [Serializable]
    public sealed class NativeDashboardState
    {
        public string appTitle = "TokenForge";
        public string subtitle = "Turn your development activity into companion growth.";
        public bool isLocalMode = true;
        public string syncStatusText = "Optional sync";
        public string selectedNavItem = "dashboard";
        public bool primaryActionEnabled = true;
        public bool hasActiveRepository;
        public bool isAnalysisRunning;
        public string actionStatusKind = "idle";
        public string actionStatusText = "Ready";
        public string repositoryStatus = "not_selected";
        public string repositorySafeError = string.Empty;
        public bool hasPendingReview;
        public bool canSaveGrowth;
        public bool canDiscardPendingReview;
        public bool hasSavedReviews;
        public bool hasRepositoryActivity;
        public bool hasAiAgentActivity;
        public int persistedCompanionXP;
        public int pendingEstimatedXP;
        public int pendingReviewCount;
        public int warningCount;
        public string lastRunSummary = "No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.";
        public string growthBasis = "Full local Git history";
        public string growthFirstCommit = string.Empty;
        public string growthFirstCommitDate = string.Empty;
        public string growthCurrentHead = string.Empty;
        public string growthLastAnalyzedCommit = string.Empty;
        public int growthCommitsAnalyzed;
        public int growthFilesChanged;
        public string growthProjectionSource = "none";
        public bool growthFallbackUsed;
        public bool growthCacheHit;
        public string growthReasonIfUnchanged = string.Empty;
        public int codeStat;
        public int focusStat;
        public int debugStat;
        public int designStat;
        public int syncStat;
        public int weeklyCodeStat;
        public int weeklyFocusStat;
        public int weeklyDebugStat;
        public int weeklyDesignStat;
        public int weeklySyncStat;
        public bool hasGrowthAxisData;
        public bool hasLegacyGrowthAxisGap;
        public string growthAxisDataStatusText = "No axis data recorded yet.";
        public string dominantGrowthPath = "Unknown";
        public string secondaryGrowthTrait = "Unknown";
        public string currentEvolutionBias = "Unknown";
        public string nextEvolutionPreview = "Repository Hatchling";
        public string eggInfluenceText = "Connect a repository to start shaping a companion.";
        public string tokenCurrencyName = "Forge Coins";
        public int tokenCurrencyBalance;
        public bool tokenUsageTrackingEnabled = true;
        public bool companionVisible = true;
        public bool wanderEnabled = true;
        public bool desiredVisible = true;
        public bool actualVisible;
        public bool movementEnabled = true;
        public string overlayMode = "allConnectedRepos";
        public string movementMode = "allConnectedRepos";
        public bool dragEnabled = true;
        public bool panelExists;
        public string panelFrame = string.Empty;
        public string selectedRepoHash = string.Empty;
        public bool repoApproved;
        public bool movementPaused;
        public string overlayLastAction = string.Empty;
        public string overlayLastError = string.Empty;
        public bool clickThroughEnabled;
        public bool clickReactionEnabled = true;
        public bool explicitQuitRequested;
        public bool dashboardVisible = true;
        public string activeRepositoryId = string.Empty;
        public string lastKnownFrame = string.Empty;
        public string appName = "TokenForge";
        public string connection = "local";
        public string sync = "optional";
        public string persistentStatusBarIdentifier = "TokenForge.PersistentStatusBar";
        public string persistentStatusBarAccessibilityLabel = "TokenForge persistent app status bar";
        public NativeCompanionState companion = NativeCompanionState.CreateDefault();
        public NativeRepositoryState repository = NativeRepositoryState.CreateDefault();
        public NativeCodexAgentState codexAgent = NativeCodexAgentState.CreateDefault();
        public NativeAgentSummaryState agents = NativeAgentSummaryState.CreateDefault();
        public NativeRepositoryListItem[] repositories = new NativeRepositoryListItem[0];
        public DesktopCompanionFarmState companionFarm = DesktopCompanionFarmState.CreateDefault();
        public NativeAgentProviderState[] agentProviders = new NativeAgentProviderState[0];
        public NativeProviderUsagePercentage[] providerUsagePercentages = new NativeProviderUsagePercentage[0];
        public NativeTokenShopState tokenShop = NativeTokenShopState.CreateDefault();
        public NativeOnboardingState onboarding = NativeOnboardingState.CreateDefault();
        public NativeActivityState activity = NativeActivityState.CreateDefault();
        public NativeReviewState review = NativeReviewState.CreateDefault();
        public string statusText = "Repo: None · AI Agents: 0 connected";
        public int stateRevision;

        public static NativeDashboardState CreateDefault()
        {
            return new NativeDashboardState();
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    [Serializable]
    public sealed class RepositoryCompanionOverlayState
    {
        public string repositoryId = string.Empty;
        public string repositoryName = "Repository";
        public string companionId = string.Empty;
        public bool desiredVisible = true;
        public bool actualVisible;
        public float desiredPositionX = -1f;
        public float desiredPositionY = -1f;
        public float actualPositionX = -1f;
        public float actualPositionY = -1f;
        public bool hasSavedPosition;
        public bool dragEnabled = true;
        public bool isDragging;
        public NativeCompanionFarmSnapshot hydratedSnapshot = NativeCompanionFarmSnapshot.CreateDefault();
    }

    [Serializable]
    public sealed class DesktopCompanionFarmState
    {
        public bool enabled = true;
        public RepositoryCompanionOverlayState[] overlays = new RepositoryCompanionOverlayState[0];
        public int visibleCount;
        public string draggingRepositoryId = string.Empty;
        public bool globalMotionEnabled = true;
        public bool globalClickThroughEnabled;

        public static DesktopCompanionFarmState CreateDefault()
        {
            return new DesktopCompanionFarmState();
        }
    }

    [Serializable]
    public sealed class NativeCompanionFarmSnapshot
    {
        public string repositoryId = string.Empty;
        public string repositoryName = "Repository";
        public string companionId = string.Empty;
        public int stage;
        public int level = 1;
        public int xp;
        public int archetype;
        public string visualThemeId = "orange_cat";
        public bool hydrated;
        public int renderVersion;
        public bool desiredVisible = true;
        public bool hasSavedPosition;
        public float desiredInitialX = -1f;
        public float desiredInitialY = -1f;

        public static NativeCompanionFarmSnapshot CreateDefault()
        {
            return new NativeCompanionFarmSnapshot();
        }
    }

    [Serializable]
    public sealed class NativeCompanionFarmSnapshotEnvelope
    {
        public List<NativeCompanionFarmSnapshot> overlays = new List<NativeCompanionFarmSnapshot>();
    }

    [Serializable]
    public sealed class NativeCompanionState
    {
        public string name = "Token";
        public string stage = "Egg";
        public int stageIndex;
        public int level = 1;
        public int xp;
        public int xpToNextLevel = 250;
        public int totalLifetimeXP;
        public bool canLevelUp;
        public string levelUpStatusText = "Earn more XP to level up.";
        public string levelUpDisabledReason = "Earn enough XP before leveling up.";
        public string mood = "active";
        public string skin = "orange_cat";
        public string zodiacType = "rat";
        public string zodiacLabel = "Rat / 쥐";
        public bool evolveActionVisible;
        public string evolveActionHiddenReason = "currentXP below requirement";
        public string xpStatusText = "0 XP · 250 XP required";
        public string carryForwardText = string.Empty;
        public float xpProgressRatio;
        public string dashboardAnimationState = "idle";
        public NativeCompanionMotionState motion = NativeCompanionMotionState.CreateDefault();

        public static NativeCompanionState CreateDefault()
        {
            return new NativeCompanionState();
        }
    }

    [Serializable]
    public sealed class NativeRepositoryState
    {
        public bool connected;
        public string id = string.Empty;
        public string name = string.Empty;
        public string folderName = string.Empty;
        public string status = "not_selected";
        public string statusText = "Not selected";
        public int connectedCount;
        public string lastAnalyzedAt = string.Empty;
        public string growthBasis = "Full local Git history";
        public string firstCommit = string.Empty;
        public string firstCommitDate = string.Empty;
        public string currentHead = string.Empty;
        public string lastAnalyzedCommit = string.Empty;
        public int commitsAnalyzed;
        public int filesChanged;
        public string projectionSource = "none";
        public string disabledReason = "Connect a repository first";
        public bool hasValidSource;
        public bool canAnalyze;
        public string analyzeDisabledReason = "Connect a repository first";

        public static NativeRepositoryState CreateDefault()
        {
            return new NativeRepositoryState();
        }
    }

    [Serializable]
    public sealed class NativeCodexAgentState
    {
        public bool connected;
        public string status = "not_connected";
        public string statusText = "Not connected";

        public static NativeCodexAgentState CreateDefault()
        {
            return new NativeCodexAgentState();
        }
    }

    [Serializable]
    public sealed class NativeAgentSummaryState
    {
        public int connectedCount;
        public string lastProvider = "None";
        public int warningCount;
        public string statusText = "No agents connected";
        public string privacyText = "Local aggregate only";

        public static NativeAgentSummaryState CreateDefault()
        {
            return new NativeAgentSummaryState();
        }
    }

    [Serializable]
    public sealed class NativeRepositoryListItem
    {
        public string id = string.Empty;
        public string name = "Repository";
        public string folderName = string.Empty;
        public string safePath = "Approved local folder";
        public string remoteUrl = "No remote";
        public string branch = "unknown";
        public string repositoryId = string.Empty;
        public string lastAnalysisScope = "Not analyzed";
        public string firstCommit = string.Empty;
        public string firstCommitDate = string.Empty;
        public string currentHead = string.Empty;
        public string lastAnalyzedCommit = string.Empty;
        public int commitsAnalyzed;
        public int filesChanged;
        public string companion = "Egg · Lv 1";
        public string lastAnalyzed = "Not analyzed";
        public string status = "connected";
        public string statusText = "Connected";
        public bool selected;
        public bool canAnalyze = true;
        public string analyzeDisabledReason = string.Empty;
        public bool canDisconnect = true;
        public bool canRestore;
        public bool canDelete;
        public bool archived;
        public string avatarSkin = "orange_cat";
        public string stage = "Egg";
        public int stageIndex;
        public int level = 1;
        public int currentXP;
        public int lifetimeGrowthXP;
        public int weeklyGrowthXP;
        public int requiredXP = 250;
        public bool canLevelUp;
        public string xpStatusText = "0 XP · 250 XP required";
        public string recentGrowthSource = "None";
        public int recentGitXP;
        public int recentAiXP;
        public string estimatedTokenActivity = "Unknown";
        public string dominantStat = "Unknown";
        public string secondaryStat = "Unknown";
        public string evolutionPath = "Unknown";
        public string nextEvolutionPreview = "Repository Hatchling";
        public string tokenCurrencyName = "Forge Coins";
        public int tokenCurrencyBalance;
        public string[] purchasedTokenShopItemIds = new string[0];
        public string[] equippedTokenShopItemIds = new string[0];
        public string motionMood = "idle";
        public string motionReason = "No recent aggregate activity.";
        public bool canViewGrowth = true;
        public bool canEvolve;
        public string sourceBadge = "Connected";
    }

    [Serializable]
    public sealed class NativeCompanionMotionState
    {
        public string repositoryId = string.Empty;
        public string activityLevel = "idle";
        public float movementSpeed = 0.35f;
        public float bounceAmplitude = 2.0f;
        public float idleFrequency = 0.6f;
        public float pulseFrequency = 0.2f;
        public string reaction = "none";
        public string mood = "idle";
        public string reasonSummary = "No recent aggregate activity.";
        public string updatedAt = string.Empty;

        public static NativeCompanionMotionState CreateDefault()
        {
            return new NativeCompanionMotionState();
        }
    }

    [Serializable]
    public sealed class NativeAgentProviderState
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public string type = string.Empty;
        public string detectionStrategy = "manual_folder";
        public string supportedStatus = "manual_folder_required";
        public string status = "notConfigured";
        public string statusText = "Manual folder required";
        public bool connected;
        public bool hasValidSource;
        public bool canAutoDetect;
        public bool canConnect = true;
        public bool canChooseFolder = true;
        public bool canAnalyze;
        public bool canDisconnect;
        public int warningCount;
        public string safeCandidateSummary = "No local source selected";
        public string selectedSourceLabel = "No local source selected";
        public string lastAnalyzedAt = "Not analyzed";
        public bool approvedSource;
        public string estimatedTokenActivity = "Unknown";
        public string estimatedTokensText = "unavailable";
        public string sessionCountText = "0";
        public string interactionCountText = "0";
        public string recentAnalyzedRepository = "Unassigned";
        public string repositoryAttributionSummary = "No recent repository attribution";
        public int pendingXP;
        public int savedXP;
        public string confidence = "Unknown";
        public string warningsText = "None";
        public bool canApprove;
        public bool canViewUsage = true;
        public bool canSaveGrowth;
        public string lastErrorSafeMessage = string.Empty;
        public string disabledReason = "Detect or choose a folder before analyzing.";
        public string unsupportedReason = string.Empty;
    }

    [Serializable]
    public sealed class NativeProviderUsagePercentage
    {
        public string providerId = string.Empty;
        public string label = string.Empty;
        public int percentage;
        public bool hasSavedApprovedActivity;
    }

    [Serializable]
    public sealed class NativeTokenShopState
    {
        public string currencyName = "Forge Coins";
        public int balance;
        public bool hasActiveRepository;
        public string statusText = "Connect a repository to use the Token Shop.";
        public string lastTransactionStatus = string.Empty;
        public string targetType = "repositoryCompanion";
        public string selectedAgentId = string.Empty;
        public string selectedCategory = "featured";
        public string[] categoryIds = new[] { "featured", "zodiac", "skins", "outfits", "accessories", "effects", "motions", "themes", "exclusive", "owned" };
        public NativeTokenShopAgentState[] agents = new NativeTokenShopAgentState[0];
        public string ownedItemIds = string.Empty;
        public string equippedItemIds = string.Empty;
        public NativeTokenShopItemState[] items = new NativeTokenShopItemState[0];

        public static NativeTokenShopState CreateDefault()
        {
            return new NativeTokenShopState();
        }
    }

    [Serializable]
    public sealed class NativeTokenShopItemState
    {
        public string itemId = string.Empty;
        public string name = string.Empty;
        public string description = string.Empty;
        public string itemType = string.Empty;
        public string category = "featured";
        public string targetCompatibility = "Repository Companion";
        public string rarity = "Common";
        public string previewIcon = "TF";
        public string previewType = "generic";
        public string zodiacType = string.Empty;
        public int price;
        public bool owned;
        public bool equipped;
        public bool locked;
        public bool available;
        public bool canEquip;
        public string stateLabel = "Buy";
        public string buttonTitle = "Buy";
        public string disabledReason = string.Empty;
        public string insufficientCoinReason = string.Empty;
        public string lockedAgentReason = string.Empty;
        public string previewEffect = "Preview cosmetic";
    }

    [Serializable]
    public sealed class NativeTokenShopAgentState
    {
        public string id = string.Empty;
        public string displayName = string.Empty;
        public bool connected;
        public bool selected;
        public string statusText = "Not connected";
        public string lockedReason = "Connect to unlock agent cosmetics.";
        public string actionTitle = "Connect agent";
        public int tokenUsageTotal;
        public int tokenUsageRecent;
        public int spendableCoins;
        public string currencyName = "Agent Coins";
        public string zodiacType = string.Empty;
        public string zodiacLabel = string.Empty;
    }

    [Serializable]
    public sealed class NativeOnboardingState
    {
        public bool firstRunCompleted;
        public bool dismissedForNow;
        public bool shouldPresentFirstRunGuide = true;
        public string presentationMode = "guidedTutorial";
        public string currentStep = "step_1";
        public int currentStepIndex;
        public int stepCount = 5;
        public bool canGoBack;
        public bool canGoNext = true;
        public string statusText = "Connect a repository, analyze your work, grow a mascot, and keep it on your Mac desktop.";
        public string[] steps = new[] { "Pick a repository", "Analyze Git history", "Grow your companion", "Unlock cosmetics", "Bring it to the desktop" };
        public string[] zodiacIds = new[] { "rat", "ox", "tiger", "rabbit", "dragon", "snake", "horse", "goat", "monkey", "rooster", "dog", "pig" };

        public static NativeOnboardingState CreateDefault()
        {
            return new NativeOnboardingState();
        }
    }

    [Serializable]
    public sealed class NativeActivityState
    {
        public string todaySummary = "No activity yet";
        public string state = "No pending review";
        public int code;
        public int focus;
        public int debug;
        public int design;
        public int sync;
        public bool hasAxisData;
        public bool hasLegacyAxisGap;
        public string axisDataStatusText = "No axis data recorded yet.";
        public string recentRunsSummary = "No recent runs";
        public string savedReviewsSummary = "No saved reviews";
        public string repositoryActivitySummary = "No repository activity";
        public string agentActivitySummary = "No AI agent activity";
        public NativeActivityItem[] runningJobs = new NativeActivityItem[0];
        public NativeActivityItem[] pendingReviews = new NativeActivityItem[0];
        public NativeActivityItem[] recentRuns = new NativeActivityItem[0];
        public bool hasRecentRuns;
        public bool hasSavedReviews;
        public bool hasRepositoryActivity;
        public bool hasAiAgentActivity;
        public bool hidesZeroDeltaSystemNoise = true;

        public static NativeActivityState CreateDefault()
        {
            return new NativeActivityState();
        }
    }

    [Serializable]
    public sealed class NativeReviewState
    {
        public string reviewId = string.Empty;
        public bool pending;
        public string summary = "No pending review";
        public string source = string.Empty;
        public string repositoryName = string.Empty;
        public string providerName = string.Empty;
        public string confidence = string.Empty;
        public int estimatedXpDelta;
        public int codeDelta;
        public int focusDelta;
        public int debugDelta;
        public int designDelta;
        public int syncDelta;
        public string warnings = string.Empty;
        public bool canSaveGrowth;
        public bool canDiscard;
        public bool canViewDetails;
        public bool detailVisible;
        public string selectedReviewId = string.Empty;
        public string generatedAt = string.Empty;
        public string status = "none";
        public string privacyNote = "Raw prompt, code, file content, and command logs are not stored.";
        public string evidenceSummary = string.Empty;
        public string categoryBreakdown = string.Empty;

        public static NativeReviewState CreateDefault()
        {
            return new NativeReviewState();
        }
    }

    [Serializable]
    public sealed class NativeActivityItem
    {
        public string id = string.Empty;
        public string type = string.Empty;
        public string sourceName = string.Empty;
        public string status = string.Empty;
        public string createdAt = string.Empty;
        public string completedAt = string.Empty;
        public string summary = string.Empty;
        public string currentStep = string.Empty;
        public string actionKey = string.Empty;
        public string disabledReason = string.Empty;
        public int xpDelta;
        public string categoryBreakdown = string.Empty;
        public string confidence = string.Empty;
        public string warnings = string.Empty;
        public string target = string.Empty;
        public string period = string.Empty;
        public string commitHash = string.Empty;
        public string fileCategory = string.Empty;
        public string deltaReason = string.Empty;
    }
}
