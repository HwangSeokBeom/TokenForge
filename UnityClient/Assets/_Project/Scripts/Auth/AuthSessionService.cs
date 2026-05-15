using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TokenForge.Client.Auth
{
    public sealed class AuthSessionService : IAuthSessionService
    {
        private static readonly TimeSpan RefreshSkew = TimeSpan.FromSeconds(30);

        private readonly IAuthApiClient apiClient;
        private readonly ISecureTokenStore tokenStore;
        private readonly Func<DateTimeOffset> clock;
        private int loginInProgress;
        private int signupInProgress;
        private int loadCurrentUserInProgress;
        private int logoutInProgress;
        private int refreshInProgress;

        public AuthSessionService(
            IAuthApiClient apiClient,
            ISecureTokenStore tokenStore,
            Func<DateTimeOffset> clock = null)
        {
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        }

        public AuthState State { get; private set; } = AuthState.LoggedOut;
        public AuthSession CurrentSession { get; private set; }
        public string ErrorCode { get; private set; } = string.Empty;
        public string BaseUrl => apiClient.Config.BaseUrl;
        public bool HasUsableAccessToken => CurrentSession != null && CurrentSession.HasAccessToken && !CurrentSession.IsAccessTokenExpired(clock(), RefreshSkew);

        public void SetBaseUrl(string baseUrl)
        {
            var previousBaseUrl = apiClient.Config.BaseUrl;
            if (!apiClient.Config.TrySetBaseUrl(baseUrl))
            {
                State = AuthState.Failed;
                ErrorCode = AuthApiError.InvalidBaseUrl;
                return;
            }

            if (!string.Equals(previousBaseUrl, apiClient.Config.BaseUrl, StringComparison.OrdinalIgnoreCase) && CurrentSession != null)
            {
                CurrentSession = null;
                State = AuthState.AuthRequired;
                ErrorCode = AuthApiError.AuthRequired;
                return;
            }

            if (string.Equals(ErrorCode, AuthApiError.InvalidBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                ErrorCode = string.Empty;
                State = CurrentSession != null && CurrentSession.HasAccessToken ? AuthState.LoggedIn : AuthState.LoggedOut;
            }
        }

        public async Task<AuthResult> LoadSessionAsync(CancellationToken cancellationToken = default)
        {
            AuthSession session;
            try
            {
                session = await tokenStore.LoadAsync(cancellationToken);
            }
            catch (SecureTokenStoreException ex)
            {
                CurrentSession = null;
                State = AuthState.Failed;
                ErrorCode = ex.ErrorCode;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }
            catch (Exception)
            {
                CurrentSession = null;
                State = AuthState.Failed;
                ErrorCode = AuthApiError.TokenStoreReadFailed;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            if (session == null || !session.HasAccessToken)
            {
                CurrentSession = null;
                State = AuthState.LoggedOut;
                ErrorCode = string.Empty;
                return AuthResult.Success(State);
            }

            if (!SessionMatchesCurrentBaseUrl(session))
            {
                CurrentSession = null;
                State = AuthState.AuthRequired;
                ErrorCode = AuthApiError.AuthRequired;
                return AuthResult.Failure(State, ErrorCode, "Log in to use server sync.");
            }

            session.ServerBaseUrl = apiClient.Config.BaseUrl;
            CurrentSession = session;
            if (session.IsAccessTokenExpired(clock(), RefreshSkew))
            {
                State = AuthState.Expired;
                ErrorCode = AuthApiError.TokenExpired;
                return AuthResult.Failure(State, ErrorCode, "Authentication is required for Safe Sync.");
            }

            State = AuthState.LoggedIn;
            ErrorCode = string.Empty;
            return AuthResult.Success(State);
        }

        public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref loginInProgress, 1) == 1)
            {
                return AuthResult.Failure(State, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress));
            }

            try
            {
            var validation = ValidateCredentials(email, password);
            if (validation != null)
            {
                return validation;
            }

            State = AuthState.LoggingIn;
            ErrorCode = string.Empty;
            var response = await apiClient.LoginAsync(email, password, cancellationToken);
            if (!response.IsSuccess || response.Value == null || string.IsNullOrWhiteSpace(response.Value.AccessToken))
            {
                State = ToFailureState(response.ErrorCode);
                ErrorCode = NormalizeResponseError(response.ErrorCode, AuthApiError.InvalidCredentials);
                return AuthResult.Failure(State, ErrorCode, response.ErrorMessage);
            }

            var session = FromTokenResponse(response.Value);
            var saveFailure = await TrySaveSessionAsync(session, cancellationToken);
            if (saveFailure != null)
            {
                CurrentSession = null;
                State = AuthState.Failed;
                ErrorCode = saveFailure;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            CurrentSession = session;
            State = AuthState.LoggedIn;
            ErrorCode = string.Empty;
            return AuthResult.Success(State);
            }
            finally
            {
                Interlocked.Exchange(ref loginInProgress, 0);
            }
        }

        public async Task<AuthResult> SignupAsync(string email, string password, string displayName = "", CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref signupInProgress, 1) == 1)
            {
                return AuthResult.Failure(State, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress));
            }

            try
            {
            var validation = ValidateCredentials(email, password);
            if (validation != null)
            {
                return validation;
            }

            State = AuthState.SigningUp;
            ErrorCode = string.Empty;
            var response = await apiClient.SignupAsync(email, password, displayName, cancellationToken);
            if (!response.IsSuccess || response.Value == null || string.IsNullOrWhiteSpace(response.Value.AccessToken))
            {
                State = ToFailureState(response.ErrorCode);
                ErrorCode = NormalizeResponseError(response.ErrorCode, AuthApiError.ValidationFailed);
                return AuthResult.Failure(State, ErrorCode, response.ErrorMessage);
            }

            var session = FromTokenResponse(response.Value);
            var saveFailure = await TrySaveSessionAsync(session, cancellationToken);
            if (saveFailure != null)
            {
                CurrentSession = null;
                State = AuthState.Failed;
                ErrorCode = saveFailure;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            CurrentSession = session;
            State = AuthState.LoggedIn;
            ErrorCode = string.Empty;
            return AuthResult.Success(State);
            }
            finally
            {
                Interlocked.Exchange(ref signupInProgress, 0);
            }
        }

        public async Task<AuthResult> LoadCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref loadCurrentUserInProgress, 1) == 1)
            {
                return AuthResult.Failure(State, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress));
            }

            try
            {
            if (CurrentSession == null)
            {
                await LoadSessionAsync(cancellationToken);
            }

            if (CurrentSession == null || !CurrentSession.HasAccessToken)
            {
                State = AuthState.AuthRequired;
                ErrorCode = AuthApiError.AuthRequired;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            var first = await apiClient.GetCurrentUserAsync(CurrentSession.AccessToken, cancellationToken);
            if (first.IsSuccess && first.Value != null)
            {
                return await UpdateCurrentUserAsync(first.Value, cancellationToken);
            }

            if (!IsAuthRejected(first.ErrorCode, first.StatusCode))
            {
                State = ToFailureState(first.ErrorCode);
                ErrorCode = NormalizeResponseError(first.ErrorCode, AuthApiError.UnknownAuthError);
                return AuthResult.Failure(State, ErrorCode, first.ErrorMessage);
            }

            var refreshed = await ForceRefreshForRetryAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(refreshed))
            {
                if (!string.IsNullOrWhiteSpace(ErrorCode) &&
                    !string.Equals(ErrorCode, AuthApiError.AuthRequired, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ErrorCode, AuthApiError.RefreshFailed, StringComparison.OrdinalIgnoreCase))
                {
                    return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
                }

                State = AuthState.Expired;
                ErrorCode = AuthApiError.SessionExpired;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            var retry = await apiClient.GetCurrentUserAsync(refreshed, cancellationToken);
            if (retry.IsSuccess && retry.Value != null)
            {
                return await UpdateCurrentUserAsync(retry.Value, cancellationToken);
            }

            if (IsAuthRejected(retry.ErrorCode, retry.StatusCode))
            {
                await ExpireSessionAsync(AuthApiError.SessionExpired, cancellationToken);
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            State = ToFailureState(retry.ErrorCode);
            ErrorCode = NormalizeResponseError(retry.ErrorCode, AuthApiError.UnknownAuthError);
            return AuthResult.Failure(State, ErrorCode, retry.ErrorMessage);
            }
            finally
            {
                Interlocked.Exchange(ref loadCurrentUserInProgress, 0);
            }
        }

        public async Task<AuthResult> LogoutAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref logoutInProgress, 1) == 1)
            {
                return AuthResult.Failure(State, AuthApiError.AlreadyInProgress, AuthApiError.ToSafeMessage(AuthApiError.AlreadyInProgress));
            }

            try
            {
            if (CurrentSession == null)
            {
                try
                {
                    CurrentSession = await tokenStore.LoadAsync(cancellationToken);
                }
                catch (Exception)
                {
                    CurrentSession = null;
                }
            }

            var refreshToken = CurrentSession?.RefreshToken ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await apiClient.LogoutAsync(refreshToken, cancellationToken);
            }

            var clearFailure = await TryClearTokenStoreAsync(cancellationToken);
            CurrentSession = null;
            State = AuthState.LoggedOut;
            if (clearFailure != null)
            {
                ErrorCode = clearFailure;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            ErrorCode = string.Empty;
            return AuthResult.Success(State);
            }
            finally
            {
                Interlocked.Exchange(ref logoutInProgress, 0);
            }
        }

        public async Task<string> GetAccessTokenForSyncAsync(CancellationToken cancellationToken = default)
        {
            if (CurrentSession == null)
            {
                await LoadSessionAsync(cancellationToken);
            }

            if (CurrentSession == null || !CurrentSession.HasAccessToken)
            {
                State = AuthState.AuthRequired;
                ErrorCode = AuthApiError.AuthRequired;
                return string.Empty;
            }

            if (!CurrentSession.IsAccessTokenExpired(clock(), RefreshSkew))
            {
                State = AuthState.LoggedIn;
                ErrorCode = string.Empty;
                return CurrentSession.AccessToken;
            }

            return await ForceRefreshForRetryAsync(cancellationToken);
        }

        public async Task<string> ForceRefreshForRetryAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref refreshInProgress, 1) == 1)
            {
                State = AuthState.Refreshing;
                ErrorCode = AuthApiError.AlreadyInProgress;
                return string.Empty;
            }

            try
            {
            if (CurrentSession == null || !CurrentSession.HasRefreshToken)
            {
                State = AuthState.AuthRequired;
                ErrorCode = AuthApiError.AuthRequired;
                return string.Empty;
            }

            State = AuthState.Refreshing;
            var response = await apiClient.RefreshAsync(CurrentSession.RefreshToken, cancellationToken);
            if (!response.IsSuccess || response.Value == null || string.IsNullOrWhiteSpace(response.Value.AccessToken))
            {
                await ExpireSessionAsync(NormalizeResponseError(response.ErrorCode, AuthApiError.RefreshFailed), cancellationToken);
                return string.Empty;
            }

            CurrentSession.AccessToken = response.Value.AccessToken;
            CurrentSession.RefreshToken = string.IsNullOrWhiteSpace(response.Value.RefreshToken)
                ? CurrentSession.RefreshToken
                : response.Value.RefreshToken;
            CurrentSession.AccessTokenExpiresAt = TryReadJwtExpiration(response.Value.AccessToken);
            CurrentSession.ServerBaseUrl = apiClient.Config.BaseUrl;
            var saveFailure = await TrySaveSessionAsync(CurrentSession, cancellationToken);
            if (saveFailure != null)
            {
                CurrentSession = null;
                State = AuthState.Failed;
                ErrorCode = saveFailure;
                return string.Empty;
            }

            State = AuthState.LoggedIn;
            ErrorCode = string.Empty;
            return CurrentSession.AccessToken;
            }
            finally
            {
                Interlocked.Exchange(ref refreshInProgress, 0);
            }
        }

        private async Task<AuthResult> UpdateCurrentUserAsync(AuthUserDto user, CancellationToken cancellationToken)
        {
            if (CurrentSession == null)
            {
                CurrentSession = new AuthSession();
            }

            CurrentSession.UserId = user?.Id ?? string.Empty;
            CurrentSession.Email = user?.Email ?? string.Empty;
            CurrentSession.DisplayName = user?.DisplayName ?? string.Empty;
            CurrentSession.ServerBaseUrl = apiClient.Config.BaseUrl;
            var saveFailure = await TrySaveSessionAsync(CurrentSession, cancellationToken);
            if (saveFailure != null)
            {
                State = AuthState.Failed;
                ErrorCode = saveFailure;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            State = AuthState.LoggedIn;
            ErrorCode = string.Empty;
            return AuthResult.Success(State);
        }

        private async Task ExpireSessionAsync(string errorCode, CancellationToken cancellationToken)
        {
            State = AuthState.Expired;
            ErrorCode = NormalizeResponseError(errorCode, AuthApiError.SessionExpired);
            var clearFailure = await TryClearTokenStoreAsync(cancellationToken);
            CurrentSession = null;
            if (clearFailure != null)
            {
                ErrorCode = clearFailure;
            }
        }

        private AuthResult ValidateCredentials(string email, string password)
        {
            if (State == AuthState.Failed && string.Equals(ErrorCode, AuthApiError.InvalidBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            if (!apiClient.Config.HasValidBaseUrl())
            {
                State = AuthState.Failed;
                ErrorCode = AuthApiError.InvalidBaseUrl;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || !LooksLikeEmail(email))
            {
                State = AuthState.Failed;
                ErrorCode = AuthApiError.ValidationFailed;
                return AuthResult.Failure(State, ErrorCode, AuthApiError.ToSafeMessage(ErrorCode));
            }

            return null;
        }

        private static bool LooksLikeEmail(string email)
        {
            email = email ?? string.Empty;
            return email.Contains("@") && email.LastIndexOf('.') > email.IndexOf('@') + 1;
        }

        private static bool IsAuthRejected(string errorCode, long statusCode)
        {
            return statusCode == 401 ||
                   statusCode == 403 ||
                   string.Equals(errorCode, AuthApiError.AuthRequired, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, AuthApiError.TokenExpired, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(errorCode, AuthApiError.SessionExpired, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeResponseError(string errorCode, string fallback)
        {
            return string.IsNullOrWhiteSpace(errorCode) ? fallback : errorCode;
        }

        private static AuthState ToFailureState(string errorCode)
        {
            if (string.Equals(errorCode, AuthApiError.ServerUnavailable, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, AuthApiError.NetworkError, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, AuthApiError.Timeout, StringComparison.OrdinalIgnoreCase))
            {
                return AuthState.ServerUnavailable;
            }

            if (string.Equals(errorCode, AuthApiError.AuthRequired, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, AuthApiError.TokenExpired, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, AuthApiError.RefreshFailed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(errorCode, AuthApiError.SessionExpired, StringComparison.OrdinalIgnoreCase))
            {
                return AuthState.Expired;
            }

            return AuthState.Failed;
        }

        private AuthSession FromTokenResponse(AuthTokenResponse response)
        {
            var user = response.User;
            return new AuthSession
            {
                AccessToken = response.AccessToken,
                RefreshToken = response.RefreshToken,
                AccessTokenExpiresAt = TryReadJwtExpiration(response.AccessToken),
                UserId = user?.Id ?? string.Empty,
                Email = user?.Email ?? string.Empty,
                DisplayName = user?.DisplayName ?? string.Empty,
                ServerBaseUrl = apiClient.Config.BaseUrl,
                LastLoginAt = clock()
            };
        }

        private bool SessionMatchesCurrentBaseUrl(AuthSession session)
        {
            return session == null ||
                   string.IsNullOrWhiteSpace(session.ServerBaseUrl) ||
                   string.Equals(session.ServerBaseUrl.TrimEnd('/'), apiClient.Config.BaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> TrySaveSessionAsync(AuthSession session, CancellationToken cancellationToken)
        {
            try
            {
                await tokenStore.SaveAsync(session, cancellationToken);
                return null;
            }
            catch (SecureTokenStoreException ex)
            {
                return string.IsNullOrWhiteSpace(ex.ErrorCode) ? AuthApiError.TokenStoreWriteFailed : ex.ErrorCode;
            }
            catch (Exception)
            {
                return AuthApiError.TokenStoreWriteFailed;
            }
        }

        private async Task<string> TryClearTokenStoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                await tokenStore.ClearAsync(cancellationToken);
                return null;
            }
            catch (SecureTokenStoreException ex)
            {
                return string.IsNullOrWhiteSpace(ex.ErrorCode) ? AuthApiError.TokenStoreClearFailed : ex.ErrorCode;
            }
            catch (Exception)
            {
                return AuthApiError.TokenStoreClearFailed;
            }
        }

        private static DateTimeOffset? TryReadJwtExpiration(string jwt)
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                return null;
            }

            var parts = jwt.Split('.');
            if (parts.Length < 2)
            {
                return null;
            }

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var exp = JObject.Parse(json)["exp"]?.Value<long>();
                return exp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(exp.Value) : (DateTimeOffset?)null;
            }
            catch
            {
                return null;
            }
        }
    }
}
