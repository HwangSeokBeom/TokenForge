namespace TokenForge.Client.Auth
{
    public enum AuthState
    {
        LoggedOut,
        LoggingIn,
        SigningUp,
        LoggedIn,
        Refreshing,
        AuthRequired,
        Expired,
        Failed,
        ServerUnavailable
    }
}
