using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Auth
{
    public sealed class SecureTokenStoreException : System.Exception
    {
        public SecureTokenStoreException(string errorCode)
            : base(AuthApiError.ToSafeMessage(errorCode))
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? AuthApiError.TokenStoreUnavailable : errorCode;
        }

        public SecureTokenStoreException(string errorCode, System.Exception innerException)
            : base(AuthApiError.ToSafeMessage(errorCode), innerException)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? AuthApiError.TokenStoreUnavailable : errorCode;
        }

        public string ErrorCode { get; }
    }

    public interface ISecureTokenStore
    {
        Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default);
        Task<AuthSession> LoadAsync(CancellationToken cancellationToken = default);
        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
