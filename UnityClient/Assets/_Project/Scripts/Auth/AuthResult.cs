namespace TokenForge.Client.Auth
{
    public sealed class AuthResult
    {
        public bool IsSuccess { get; set; }
        public AuthState State { get; set; } = AuthState.LoggedOut;
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public static AuthResult Success(AuthState state)
        {
            return new AuthResult { IsSuccess = true, State = state };
        }

        public static AuthResult Failure(AuthState state, string errorCode, string errorMessage)
        {
            var safeCode = string.IsNullOrWhiteSpace(errorCode) ? AuthApiError.UnknownAuthError : errorCode;
            return new AuthResult
            {
                IsSuccess = false,
                State = state,
                ErrorCode = safeCode,
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? AuthApiError.ToSafeMessage(safeCode) : errorMessage
            };
        }
    }
}
