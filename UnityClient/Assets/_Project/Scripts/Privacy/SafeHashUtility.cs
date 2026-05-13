using System;
using System.Security.Cryptography;
using System.Text;

namespace TokenForge.Client.Privacy
{
    public static class SafeHashUtility
    {
        public static string ComputeProjectPathHash(string projectPath, string salt = "TokenForge.ProjectPath.v1")
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return string.Empty;
            }

            var normalized = projectPath.Trim().Replace('\\', '/').ToLowerInvariant();
            var input = $"{salt}:{normalized}";
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
