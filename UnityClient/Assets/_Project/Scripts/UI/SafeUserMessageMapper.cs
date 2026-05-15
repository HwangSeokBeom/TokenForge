using System;
using System.Text.RegularExpressions;
using TokenForge.Client.Auth;
using TokenForge.Client.Sync;

namespace TokenForge.Client.UI
{
    public sealed class UiStatusMessage
    {
        public UiStatusMessage(string title, string message, string safeCode = "")
        {
            Title = title ?? string.Empty;
            Message = message ?? string.Empty;
            SafeCode = safeCode ?? string.Empty;
        }

        public string Title { get; }
        public string Message { get; }
        public string SafeCode { get; }
    }

    public static class SafeUserMessageMapper
    {
        public static UiStatusMessage FromAuth(AuthState state, string errorCode = "")
        {
            var title = AuthStateLabel(state);
            var message = string.IsNullOrWhiteSpace(errorCode)
                ? AuthStateMessage(state)
                : FromAuthError(errorCode);
            return new UiStatusMessage(title, message, SafeCode(errorCode));
        }

        public static UiStatusMessage FromSync(SafeSyncStatus status, string errorCode = "")
        {
            var title = SafeSyncStatusLabel(status);
            var message = string.IsNullOrWhiteSpace(errorCode)
                ? SafeSyncStatusMessage(status)
                : FromSyncError(errorCode);
            return new UiStatusMessage(title, message, SafeCode(errorCode));
        }

        public static string FromAuthError(string errorCode)
        {
            return AuthApiError.ToSafeMessage(SafeCode(errorCode));
        }

        public static string FromSyncError(string errorCode)
        {
            return SafeSyncApiError.ToSafeMessage(SafeCode(errorCode));
        }

        public static string FromTokenStoreError(string errorCode)
        {
            return AuthApiError.ToSafeMessage(SafeCode(errorCode));
        }

        public static string FromSyncResult(SafeSyncResult result)
        {
            if (result == null)
            {
                return SafeSyncApiError.ToSafeMessage(string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorCode))
            {
                return FromSyncError(result.ErrorCode);
            }

            if (result.Status == SafeSyncStatus.Synced)
            {
                return "Safe Sync completed.";
            }

            return SafeSyncStatusMessage(result.Status);
        }

        public static string AuthStateLabel(AuthState state)
        {
            switch (state)
            {
                case AuthState.SigningUp: return "Signing Up";
                case AuthState.LoggingIn: return "Logging In";
                case AuthState.LoggedIn: return "Logged In";
                case AuthState.Refreshing: return "Refreshing Session";
                case AuthState.Expired: return "Session Expired";
                case AuthState.AuthRequired: return "Auth Required";
                case AuthState.ServerUnavailable: return "Server Unavailable";
                case AuthState.Failed: return "Failed";
                default: return "Logged Out";
            }
        }

        public static string SafeSyncStatusLabel(SafeSyncStatus status)
        {
            switch (status)
            {
                case SafeSyncStatus.CheckingHealth:
                case SafeSyncStatus.CheckingServer:
                    return "Checking Server";
                case SafeSyncStatus.Ready:
                case SafeSyncStatus.ServerReady:
                    return "Server Ready";
                case SafeSyncStatus.AuthRequired: return "Auth Required";
                case SafeSyncStatus.Syncing: return "Syncing";
                case SafeSyncStatus.Synced: return "Synced";
                case SafeSyncStatus.Fetching:
                case SafeSyncStatus.FetchingRemoteSessions:
                    return "Fetching Remote Sessions";
                case SafeSyncStatus.DeleteInProgress: return "Delete In Progress";
                case SafeSyncStatus.ServerUnavailable: return "Server Unavailable";
                case SafeSyncStatus.Failed: return "Failed";
                default: return "Idle";
            }
        }

        public static string SessionExpirySummary(AuthState state, DateTimeOffset? expiresAt)
        {
            if (state == AuthState.Expired)
            {
                return "Session expired";
            }

            if (state == AuthState.AuthRequired || state == AuthState.LoggedOut || !expiresAt.HasValue)
            {
                return "Refresh required";
            }

            return expiresAt.Value <= DateTimeOffset.UtcNow ? "Session expired" : "Session active";
        }

        private static string AuthStateMessage(AuthState state)
        {
            switch (state)
            {
                case AuthState.LoggedIn: return "Session active.";
                case AuthState.LoggingIn: return "Logging in.";
                case AuthState.SigningUp: return "Signing up.";
                case AuthState.Refreshing: return "Refreshing session.";
                case AuthState.Expired: return "Your session expired. Please log in again.";
                case AuthState.AuthRequired: return "Log in to use server sync.";
                case AuthState.ServerUnavailable: return "The server is unavailable. Your local data is safe.";
                case AuthState.Failed: return "Authentication failed.";
                default: return "Login is only required for server sync. Local analysis stays on this device.";
            }
        }

        private static string SafeSyncStatusMessage(SafeSyncStatus status)
        {
            switch (status)
            {
                case SafeSyncStatus.CheckingHealth:
                case SafeSyncStatus.CheckingServer:
                    return "Checking server.";
                case SafeSyncStatus.Ready:
                case SafeSyncStatus.ServerReady:
                    return "Server is ready.";
                case SafeSyncStatus.AuthRequired:
                    return "Log in to use server sync.";
                case SafeSyncStatus.Syncing:
                    return "Syncing privacy-safe aggregate sessions.";
                case SafeSyncStatus.Synced:
                    return "Safe Sync completed.";
                case SafeSyncStatus.Fetching:
                case SafeSyncStatus.FetchingRemoteSessions:
                    return "Fetching remote session summaries.";
                case SafeSyncStatus.DeleteInProgress:
                    return "Deleting selected remote session.";
                case SafeSyncStatus.ServerUnavailable:
                    return "The server is unavailable. Your local data is safe.";
                case SafeSyncStatus.Failed:
                    return "Safe Sync failed. Your local data is safe.";
                default:
                    return "Safe Sync is idle.";
            }
        }

        private static string SafeCode(string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                return string.Empty;
            }

            var trimmed = errorCode.Trim();
            if (!Regex.IsMatch(trimmed, "^[A-Za-z0-9_.-]{1,80}$"))
            {
                return string.Empty;
            }

            var lowered = trimmed.ToLowerInvariant();
            return lowered.Contains("authorization") ||
                   lowered.Contains("bearer") ||
                   lowered.Contains("token-secret") ||
                   lowered.Contains("password") ||
                   lowered.Contains("prompt") ||
                   lowered.Contains("response") ||
                   lowered.Contains("command") ||
                   lowered.Contains("body") ||
                   lowered.Contains("/users/") ||
                   lowered.Contains("\\users\\")
                ? string.Empty
                : trimmed;
        }
    }
}
