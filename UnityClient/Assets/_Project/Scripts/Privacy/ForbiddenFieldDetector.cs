using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TokenForge.Client.Privacy
{
    public sealed class ForbiddenFieldDetector
    {
        private static readonly HashSet<string> AllowedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TokenUsageBucket",
            "TokenRange",
            "TokenRangeLabel",
            "TokenBucketMultiplier",
            "RefreshTokenExpiresAt",
            "ParserVersion",
            "BuildRunCount",
            "Debug"
        };

        private static readonly HashSet<string> ForbiddenExactFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "prompt",
            "rawPrompt",
            "code",
            "rawCode",
            "log",
            "rawLog",
            "terminalOutput",
            "stdout",
            "stderr",
            "diff",
            "patch",
            "absolutePath",
            "filePath",
            "remoteUrl",
            "gitRemote",
            "branchName",
            "commitMessage",
            "gitRemoteUrl",
            "branchNameRaw",
            "commitMessageRaw",
            "apiKey",
            "secret",
            "tokenRaw",
            "commandText",
            "password",
            "bearer",
            "authorization"
        };

        private static readonly string[] ForbiddenNameFragments =
        {
            "rawprompt",
            "rawcode",
            "rawlog",
            "terminaloutput",
            "absolutepath",
            "filepath",
            "remoteurl",
            "gitremote",
            "branchname",
            "commitmessage",
            "gitremoteurl",
            "branchnameraw",
            "commitmessageraw",
            "tokenraw",
            "commandtext"
        };

        private static readonly Regex[] SensitiveStringPatterns =
        {
            new Regex(@"[""']?(prompt|rawPrompt|code|rawCode|log|rawLog|terminalOutput|stdout|stderr|absolutePath|filePath|remoteUrl|gitRemote|branchName|commitMessage|diff|patch|apiKey|secret|tokenRaw|commandText)[""']?\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"bearer\s+[a-z0-9\._\-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"api[_-]?key\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"password\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"secret\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"authorization\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(^|[\s\""])(/users/|/home/|/volumes/)[^\s\""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"[a-z]:\\users\\[^\s\""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        public bool IsForbiddenFieldName(string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName) || AllowedFieldNames.Contains(fieldName))
            {
                return false;
            }

            if (ForbiddenExactFieldNames.Contains(fieldName))
            {
                return true;
            }

            var normalized = Normalize(fieldName);
            return ForbiddenNameFragments.Any(fragment => normalized.Contains(fragment));
        }

        public bool ContainsSensitiveString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return SensitiveStringPatterns.Any(pattern => pattern.IsMatch(value));
        }

        private static string Normalize(string value)
        {
            return value.Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }
    }
}
