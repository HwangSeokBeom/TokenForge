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
            "Debug",
            "SourceKind",
            "SafeSourceAlias",
            "FileCountBucket"
        };

        private static readonly HashSet<string> ForbiddenExactFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "rawPath",
            "path",
            "prompt",
            "rawPrompt",
            "response",
            "rawResponse",
            "chatContent",
            "conversation",
            "code",
            "rawCode",
            "snippet",
            "source",
            "sourceText",
            "log",
            "rawLog",
            "terminalOutput",
            "stdout",
            "stderr",
            "diff",
            "patch",
            "absolutePath",
            "filePath",
            "fileName",
            "repoName",
            "repositoryName",
            "remoteUrl",
            "gitRemote",
            "branchName",
            "commitMessage",
            "gitRemoteUrl",
            "branchNameRaw",
            "commitMessageRaw",
            "apiKey",
            "secret",
            "token",
            "tokenRaw",
            "commandText",
            "commandString",
            "command",
            "username",
            "userName",
            "password",
            "approvedLocation",
            "approvedLocations",
            "localPath",
            "localOnlyPath",
            "bearer",
            "authorization"
        };

        private static readonly string[] ForbiddenNameFragments =
        {
            "rawprompt",
            "rawresponse",
            "chatcontent",
            "rawcode",
            "sourcetext",
            "rawlog",
            "terminaloutput",
            "absolutepath",
            "filepath",
            "filename",
            "reponame",
            "repositoryname",
            "remoteurl",
            "gitremote",
            "branchname",
            "commitmessage",
            "gitremoteurl",
            "branchnameraw",
            "commitmessageraw",
            "tokenraw",
            "commandtext",
            "commandstring",
            "username"
        };

        private static readonly Regex[] SensitiveStringPatterns =
        {
            new Regex(@"[""']?(prompt|rawPrompt|response|rawResponse|chatContent|conversation|code|rawCode|sourceText|log|rawLog|terminalOutput|stdout|stderr|absolutePath|filePath|fileName|repoName|repositoryName|remoteUrl|gitRemote|branchName|commitMessage|diff|patch|apiKey|secret|tokenRaw|commandText|commandString|username|userName)[""']?\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"[""']?(rawPath|path|filename|source|snippet|token|command|approvedLocation|approvedLocations|localPath|localOnlyPath)[""']?\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"bearer\s+[a-z0-9\._\-]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(sk|ghp|github_pat|xox[baprs])[_\-][a-z0-9_\-]{12,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"api[_-]?key\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"password\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"secret\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"authorization\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(repo|repository|branch|username|user)\s*[:=]\s*[^\s\""]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(public\s+class|private\s+class|function\s+[a-z0-9_]+\s*\(|const\s+[a-z0-9_]+\s*=|var\s+[a-z0-9_]+\s*=|def\s+[a-z0-9_]+\s*\()", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\b(git\s+(status|commit|push|pull|checkout)|npm\s+test|pytest|dotnet\s+test|xcodebuild)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
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
