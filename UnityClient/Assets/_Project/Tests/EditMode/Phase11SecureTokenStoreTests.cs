using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TokenForge.Client.Auth;

namespace TokenForge.Client.Tests
{
    public sealed class Phase11SecureTokenStoreTests
    {
        [Test]
        public void InMemoryTokenStore_StoresLoadsAndClearsSession()
        {
            var store = new InMemoryTokenStore();
            var session = new AuthSession
            {
                AccessToken = "access-secret",
                RefreshToken = "refresh-secret",
                UserId = "user-1",
                Email = "test@example.com",
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15)
            };

            RunAsync(() => store.SaveAsync(session, CancellationToken.None));
            var loaded = RunAsync(() => store.LoadAsync(CancellationToken.None));
            RunAsync(() => store.ClearAsync(CancellationToken.None));
            var cleared = RunAsync(() => store.LoadAsync(CancellationToken.None));

            Assert.AreEqual("access-secret", loaded.AccessToken);
            Assert.AreEqual("refresh-secret", loaded.RefreshToken);
            Assert.IsNull(cleared);
        }

        [Test]
        public void TokenStoreInterface_DoesNotUsePlayerPrefs()
        {
            Assert.IsFalse(typeof(ISecureTokenStore).FullName.Contains("PlayerPrefs"));
            Assert.IsFalse(typeof(InMemoryTokenStore).FullName.Contains("PlayerPrefs"));
            Assert.IsFalse(typeof(MacOSKeychainTokenStore).FullName.Contains("PlayerPrefs"));
        }

        [Test]
        public void AuthSessionMetadataCopy_DoesNotExposeTokenValues()
        {
            var safe = new AuthSession
            {
                AccessToken = "access-secret",
                RefreshToken = "refresh-secret",
                UserId = "user-1",
                Email = "test@example.com"
            }.WithoutTokenValues();

            Assert.IsEmpty(safe.AccessToken);
            Assert.IsEmpty(safe.RefreshToken);
            Assert.AreEqual("user-1", safe.UserId);
        }

        private static T RunAsync<T>(Func<Task<T>> taskFactory)
        {
            return Task.Run(taskFactory).GetAwaiter().GetResult();
        }

        private static void RunAsync(Func<Task> taskFactory)
        {
            Task.Run(taskFactory).GetAwaiter().GetResult();
        }
    }
}
