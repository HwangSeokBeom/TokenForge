using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TokenForge.Client.Auth
{
    public sealed class MacOSKeychainTokenStore : ISecureTokenStore
    {
        private const string ServiceName = "com.tokenforge.unity.safe-sync";
        private const string AccountName = "safe-sync-session";

        private readonly JsonSerializerSettings serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public async Task SaveAsync(AuthSession session, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSupportedPlatform();
            if (session == null)
            {
                await ClearAsync(cancellationToken);
                return;
            }

            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(session, serializerSettings)));
            await RunSecurityAsync(new[] { "delete-generic-password", "-s", ServiceName, "-a", AccountName }, cancellationToken, true);
            var result = await RunSecurityAsync(new[] { "add-generic-password", "-s", ServiceName, "-a", AccountName, "-w", payload }, cancellationToken, false);
            if (!result.IsSuccess)
            {
                throw new SecureTokenStoreException(AuthApiError.TokenStoreWriteFailed);
            }
        }

        public async Task<AuthSession> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSupportedPlatform();
            var result = await RunSecurityAsync(new[] { "find-generic-password", "-s", ServiceName, "-a", AccountName, "-w" }, cancellationToken, true);
            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Output))
            {
                return null;
            }

            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(result.Output.Trim()));
                return JsonConvert.DeserializeObject<AuthSession>(json, serializerSettings);
            }
            catch
            {
                throw new SecureTokenStoreException(AuthApiError.TokenStoreCorruptedSession);
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSupportedPlatform();
            var result = await RunSecurityAsync(new[] { "delete-generic-password", "-s", ServiceName, "-a", AccountName }, cancellationToken, true);
            if (!result.IsSuccess)
            {
                throw new SecureTokenStoreException(AuthApiError.TokenStoreClearFailed);
            }
        }

        private static void EnsureSupportedPlatform()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return;
#else
            throw new SecureTokenStoreException(AuthApiError.TokenStoreUnsupportedPlatform);
#endif
        }

        private static async Task<SecurityResult> RunSecurityAsync(string[] arguments, CancellationToken cancellationToken, bool allowFailure)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/security",
                Arguments = JoinArguments(arguments),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        return new SecurityResult(false, string.Empty);
                    }

                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit(), cancellationToken);
                    var output = await outputTask;
                    if (process.ExitCode != 0 && !allowFailure)
                    {
                        return new SecurityResult(false, string.Empty);
                    }

                    return new SecurityResult(process.ExitCode == 0 || allowFailure, output);
                }
            }
            catch
            {
                return new SecurityResult(false, string.Empty);
            }
        }

        private static string JoinArguments(string[] arguments)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append('"');
                builder.Append((arguments[i] ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\""));
                builder.Append('"');
            }

            return builder.ToString();
        }

        private sealed class SecurityResult
        {
            public SecurityResult(bool isSuccess, string output)
            {
                IsSuccess = isSuccess;
                Output = output ?? string.Empty;
            }

            public bool IsSuccess { get; }
            public string Output { get; }
        }
    }
}
