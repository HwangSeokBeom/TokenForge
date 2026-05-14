using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TokenForge.Client.Common;
using TokenForge.Client.Domain;
using TokenForge.Client.Privacy;

namespace TokenForge.Client.Agents
{
    public sealed class AgentActivityAnalyzer : IAgentActivityAnalyzer
    {
        private readonly IAgentLogSourceReader sourceReader;
        private readonly List<IAgentLogParser> parsers;
        private readonly PrivacySanitizer privacySanitizer;
        private readonly IAgentAnalysisLogger logger;

        public AgentActivityAnalyzer(
            IAgentLogSourceReader sourceReader = null,
            IEnumerable<IAgentLogParser> parsers = null,
            PrivacySanitizer privacySanitizer = null,
            IAgentAnalysisLogger logger = null)
        {
            this.sourceReader = sourceReader ?? new FileAgentLogSourceReader();
            this.parsers = (parsers ?? new IAgentLogParser[]
            {
                new ClaudeAgentLogParser(),
                new CodexAgentLogParser(),
                new UnknownAgentLogParser()
            }).ToList();
            this.privacySanitizer = privacySanitizer ?? new PrivacySanitizer();
            this.logger = logger;
        }

        public async Task<Result<AgentActivitySummary>> AnalyzeAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
        {
            input = Normalize(input);
            logger?.Info("Agent activity analysis started");

            var readResult = await sourceReader.ReadAsync(input, cancellationToken);
            if (!readResult.IsSuccess)
            {
                logger?.Warning("Agent activity analysis failed category=" + readResult.ErrorCode);
                return Result<AgentActivitySummary>.Failure(readResult.ErrorCode, "Agent log source could not be read.");
            }

            var parser = SelectParser(input.ProviderHint, readResult.Entries);
            var parseResult = parser.Parse(input, readResult.Entries);
            if (!parseResult.IsSuccess)
            {
                logger?.Warning("Agent activity analysis failed category=" + parseResult.ErrorCode);
                return parseResult;
            }

            foreach (var warningId in readResult.WarningIds ?? new List<string>())
            {
                if (!parseResult.Value.WarningIds.Contains(warningId, StringComparer.Ordinal))
                {
                    parseResult.Value.WarningIds.Add(warningId);
                }
            }

            var validation = privacySanitizer.ValidateNoForbiddenFields(parseResult.Value);
            if (!validation.IsSuccess)
            {
                logger?.Warning("Agent activity analysis failed category=agent_summary_privacy_validation");
                return Result<AgentActivitySummary>.Failure(validation.ErrorCode, validation.ErrorMessage);
            }

            logger?.Info("Agent activity analysis completed");
            return parseResult;
        }

        private IAgentLogParser SelectParser(AgentProviderType providerHint, IReadOnlyList<AgentLogEntry> entries)
        {
            if (providerHint != AgentProviderType.Unknown)
            {
                var hinted = parsers.FirstOrDefault(parser => parser.ProviderType == providerHint);
                if (hinted != null)
                {
                    return hinted;
                }
            }

            var sample = string.Join("\n", (entries ?? new List<AgentLogEntry>()).Take(50).Select(entry => entry.Text));
            if (sample.IndexOf("claude", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return parsers.First(parser => parser.ProviderType == AgentProviderType.Claude);
            }

            if (sample.IndexOf("codex", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return parsers.First(parser => parser.ProviderType == AgentProviderType.Codex);
            }

            return parsers.First(parser => parser.ProviderType == AgentProviderType.Unknown);
        }

        private static AgentAnalysisInput Normalize(AgentAnalysisInput input)
        {
            input = input ?? new AgentAnalysisInput();
            input.AnalysisWindowDays = Math.Max(1, Math.Min(input.AnalysisWindowDays, AgentAnalysisInput.MaxAnalysisWindowDays));
            input.MaxFilesToScan = Math.Max(1, Math.Min(input.MaxFilesToScan, AgentAnalysisInput.MaxFilesToScanLimit));
            input.MaxLogEntriesToScan = Math.Max(1, Math.Min(input.MaxLogEntriesToScan, AgentAnalysisInput.MaxLogEntriesToScanLimit));
            return input;
        }
    }
}
