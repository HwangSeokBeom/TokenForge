namespace TokenForge.Client.Auth
{
    public static class AuthApiError
    {
        public const string None = "";
        public const string AuthRequired = "AUTH_REQUIRED";
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string EmailAlreadyExists = "EMAIL_ALREADY_EXISTS";
        public const string TokenExpired = "TOKEN_EXPIRED";
        public const string RefreshFailed = "REFRESH_FAILED";
        public const string SessionExpired = "SESSION_EXPIRED";
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string NetworkError = "NETWORK_ERROR";
        public const string Timeout = "NETWORK_TIMEOUT";
        public const string ServerUnavailable = "SERVER_UNAVAILABLE";
        public const string InvalidJson = "INVALID_JSON";
        public const string InvalidBaseUrl = "INVALID_BASE_URL";
        public const string MissingToken = "MISSING_TOKEN";
        public const string SecureStoreUnavailable = "SECURE_STORE_UNAVAILABLE";
        public const string TokenStoreUnavailable = "TOKEN_STORE_UNAVAILABLE";
        public const string TokenStoreReadFailed = "TOKEN_STORE_READ_FAILED";
        public const string TokenStoreWriteFailed = "TOKEN_STORE_WRITE_FAILED";
        public const string TokenStoreClearFailed = "TOKEN_STORE_CLEAR_FAILED";
        public const string TokenStoreCorruptedSession = "TOKEN_STORE_CORRUPTED_SESSION";
        public const string TokenStoreUnsupportedPlatform = "TOKEN_STORE_UNSUPPORTED_PLATFORM";
        public const string AlreadyInProgress = "ALREADY_IN_PROGRESS";
        public const string UnknownAuthError = "UNKNOWN_AUTH_ERROR";

        public static string Normalize(string code, long statusCode, string endpointName = "")
        {
            if (IsKnown(code))
            {
                return code;
            }

            if (statusCode == 409)
            {
                return EmailAlreadyExists;
            }

            if (statusCode == 400 || statusCode == 422)
            {
                return ValidationFailed;
            }

            if (statusCode == 401 || statusCode == 403)
            {
                if (string.Equals(endpointName, "auth_login", System.StringComparison.OrdinalIgnoreCase))
                {
                    return InvalidCredentials;
                }

                if (string.Equals(endpointName, "auth_refresh", System.StringComparison.OrdinalIgnoreCase))
                {
                    return RefreshFailed;
                }

                return AuthRequired;
            }

            if (statusCode >= 500)
            {
                return ServerUnavailable;
            }

            return string.IsNullOrWhiteSpace(code) ? UnknownAuthError : code;
        }

        public static string ToSafeMessage(string code)
        {
            switch (code)
            {
                case AuthRequired:
                    return "Sign in to use Safe Sync.";
                case InvalidCredentials:
                    return "Email or password was not accepted.";
                case EmailAlreadyExists:
                    return "An account already exists for that email.";
                case TokenExpired:
                case SessionExpired:
                    return "Your session expired. Sign in again to use Safe Sync.";
                case RefreshFailed:
                    return "Your session could not be refreshed. Sign in again to use Safe Sync.";
                case ServerUnavailable:
                case NetworkError:
                    return "The Safe Sync server is unavailable.";
                case Timeout:
                    return "The authentication request timed out.";
                case ValidationFailed:
                    return "Check the account details and try again.";
                case InvalidBaseUrl:
                    return "Enter a valid HTTP or HTTPS server URL.";
                case TokenStoreUnavailable:
                    return "Secure token storage is unavailable on this device.";
                case TokenStoreReadFailed:
                    return "Could not read the saved session securely.";
                case TokenStoreWriteFailed:
                    return "Could not save the session securely.";
                case TokenStoreClearFailed:
                    return "Could not fully clear the saved session. Try logging out again.";
                case TokenStoreCorruptedSession:
                    return "The saved session could not be read securely. Please log in again.";
                case TokenStoreUnsupportedPlatform:
                case SecureStoreUnavailable:
                    return "Secure token storage is unavailable on this platform.";
                case AlreadyInProgress:
                    return "That account request is already in progress.";
                default:
                    return "Authentication failed.";
            }
        }

        private static bool IsKnown(string code)
        {
            return string.Equals(code, AuthRequired, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, InvalidCredentials, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, EmailAlreadyExists, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, TokenExpired, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, RefreshFailed, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, SessionExpired, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, ValidationFailed, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, NetworkError, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, Timeout, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, ServerUnavailable, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, InvalidJson, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, InvalidBaseUrl, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, MissingToken, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, SecureStoreUnavailable, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, TokenStoreUnavailable, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, TokenStoreReadFailed, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, TokenStoreWriteFailed, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, TokenStoreClearFailed, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, TokenStoreCorruptedSession, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, TokenStoreUnsupportedPlatform, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, AlreadyInProgress, System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(code, UnknownAuthError, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
