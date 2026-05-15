namespace TokenForge.Client.Sync
{
    public static class SafeSyncApiError
    {
        public const string None = "";
        public const string AuthRequired = "AUTH_REQUIRED";
        public const string ServerUnavailable = "SERVER_UNAVAILABLE";
        public const string NetworkError = "NETWORK_ERROR";
        public const string Timeout = "REQUEST_TIMEOUT";
        public const string NetworkTimeout = "NETWORK_TIMEOUT";
        public const string InvalidJson = "INVALID_JSON";
        public const string InvalidBaseUrl = "INVALID_BASE_URL";
        public const string PrivacyGuardBlockedPayload = "CLIENT_PRIVACY_GUARD_BLOCKED_PAYLOAD";
        public const string EmptyResponse = "EMPTY_RESPONSE";
        public const string AlreadyInProgress = "ALREADY_IN_PROGRESS";

        public static string ToSafeMessage(string code)
        {
            switch (code)
            {
                case AuthRequired:
                    return "Log in to use server sync.";
                case ServerUnavailable:
                case NetworkError:
                    return "The server is unavailable. Your local data is safe.";
                case Timeout:
                case NetworkTimeout:
                    return "The request timed out. Try again.";
                case InvalidBaseUrl:
                    return "Enter a valid HTTP or HTTPS server URL.";
                case PrivacyGuardBlockedPayload:
                    return "Sync was blocked because the outgoing payload failed the privacy check.";
                case AlreadyInProgress:
                    return "That Safe Sync request is already in progress.";
                case InvalidJson:
                    return "The server response could not be read safely.";
                default:
                    return "Safe Sync failed. Your local data is safe.";
            }
        }
    }
}
