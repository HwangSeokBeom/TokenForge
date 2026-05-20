using System;
using UnityEngine;

namespace TokenForge.Client.Platform
{
    public enum NativeDashboardAction
    {
        Dashboard,
        ShowDashboard,
        HideDashboard,
        Repository,
        CodexAgent,
        Activity,
        Settings,
        Homepage,
        ReportIssue,
        Quit,
        RunAnalysis,
        ConnectRepository,
        ConnectCodexAgent,
        ReviewActivity,
        ToggleCompanionVisible,
        ChangeCompanionSkin,
        SetLaunchAtLogin,
        SetWanderEnabled,
        SetClickReactionEnabled,
        ResetCompanionPosition,
        ResetLocalState
    }

    public sealed class NativeDashboardActionRequest
    {
        public NativeDashboardActionRequest(NativeDashboardAction action, string rawAction, string value = "")
        {
            Action = action;
            RawAction = rawAction ?? string.Empty;
            Value = value ?? string.Empty;
        }

        public NativeDashboardAction Action { get; }
        public string RawAction { get; }
        public string Value { get; }

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
        public string syncStatusText = "Sync optional";
        public string selectedNavItem = "dashboard";
        public bool primaryActionEnabled = true;
        public int pendingReviewCount;
        public int warningCount;
        public string lastRunSummary = "No saved growth yet. Run Analysis on a repository or AI agent log to generate your first XP.";
        public int codeStat;
        public int focusStat;
        public int debugStat;
        public int designStat;
        public int syncStat;
        public bool companionVisible = true;
        public bool wanderEnabled = true;
        public bool clickReactionEnabled = true;
        public string appName = "TokenForge";
        public string connection = "local";
        public string sync = "optional";
        public NativeCompanionState companion = NativeCompanionState.CreateDefault();
        public NativeRepositoryState repository = NativeRepositoryState.CreateDefault();
        public NativeCodexAgentState codexAgent = NativeCodexAgentState.CreateDefault();
        public NativeActivityState activity = NativeActivityState.CreateDefault();
        public string statusText = "Cdx 0% · CI 0% · Gem 0%";

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
    public sealed class NativeCompanionState
    {
        public string name = "Token";
        public string stage = "Egg";
        public int level = 1;
        public int xp;
        public int xpToNextLevel = 250;
        public string mood = "active";
        public string skin = "orange_cat";

        public static NativeCompanionState CreateDefault()
        {
            return new NativeCompanionState();
        }
    }

    [Serializable]
    public sealed class NativeRepositoryState
    {
        public bool connected;
        public string name = string.Empty;
        public string status = "not_selected";
        public string statusText = "Not selected";

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
    public sealed class NativeActivityState
    {
        public string todaySummary = "No activity yet";
        public string state = "No pending review";
        public int code;
        public int focus;
        public int debug;
        public int design;
        public int sync;

        public static NativeActivityState CreateDefault()
        {
            return new NativeActivityState();
        }
    }
}
