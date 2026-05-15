using System;
using Newtonsoft.Json;

namespace TokenForge.Client.Auth
{
    public sealed class AuthLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class AuthSignupRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class AuthRefreshRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public sealed class AuthLogoutRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public sealed class AuthUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public bool IsGuest { get; set; }
        public DateTimeOffset? CreatedAt { get; set; }
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonIgnore]
        public string DisplayName => !string.IsNullOrWhiteSpace(Nickname) ? Nickname : Email;
    }

    public sealed class AuthTokenResponse
    {
        public AuthUserDto User { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public sealed class AuthRefreshResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public sealed class AuthLogoutResponse
    {
        public string Status { get; set; } = string.Empty;
    }

    public sealed class AuthApiResult<T>
    {
        public bool IsSuccess { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public long StatusCode { get; set; }
        public T Value { get; set; }

        public static AuthApiResult<T> Success(T value, long statusCode)
        {
            return new AuthApiResult<T>
            {
                IsSuccess = true,
                Value = value,
                StatusCode = statusCode
            };
        }

        public static AuthApiResult<T> Failure(string errorCode, string errorMessage, long statusCode = 0)
        {
            var safeCode = string.IsNullOrWhiteSpace(errorCode) ? AuthApiError.UnknownAuthError : errorCode;
            return new AuthApiResult<T>
            {
                IsSuccess = false,
                ErrorCode = safeCode,
                ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? AuthApiError.ToSafeMessage(safeCode) : errorMessage,
                StatusCode = statusCode
            };
        }
    }
}
