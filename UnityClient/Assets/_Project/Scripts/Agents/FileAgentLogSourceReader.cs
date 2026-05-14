using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TokenForge.Client.Agents
{
    public sealed class FileAgentLogSourceReader : IAgentLogSourceReader
    {
        private static readonly HashSet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".json",
            ".jsonl",
            ".log",
            ".txt",
            ".ndjson"
        };

        public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
        {
            input = input ?? new AgentAnalysisInput();
            if (string.IsNullOrWhiteSpace(input.SelectedLocationPath))
            {
                return Task.FromResult(AgentLogReadResult.Failure("agent_log_location_missing"));
            }

            if (!Directory.Exists(input.SelectedLocationPath) && !File.Exists(input.SelectedLocationPath))
            {
                return Task.FromResult(AgentLogReadResult.Failure("agent_log_location_unavailable"));
            }

            var maxFiles = Math.Max(1, Math.Min(input.MaxFilesToScan, AgentAnalysisInput.MaxFilesToScanLimit));
            var maxEntries = Math.Max(1, Math.Min(input.MaxLogEntriesToScan, AgentAnalysisInput.MaxLogEntriesToScanLimit));
            var warnings = new List<string>();
            var entries = new List<AgentLogEntry>();

            try
            {
                foreach (var filePath in EnumerateCandidateFiles(input.SelectedLocationPath).Take(maxFiles))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileInfo = new FileInfo(filePath);
                    foreach (var line in File.ReadLines(filePath).Take(Math.Max(0, maxEntries - entries.Count)))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            entries.Add(new AgentLogEntry
                            {
                                Text = line,
                                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
                            });
                        }
                    }

                    if (entries.Count >= maxEntries)
                    {
                        warnings.Add("agent_log_entry_limit_reached");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return Task.FromResult(AgentLogReadResult.Failure("agent_log_read_failed"));
            }

            if (entries.Count == 0)
            {
                warnings.Add("agent_log_no_entries");
            }

            return Task.FromResult(AgentLogReadResult.Success(entries, warnings.Distinct(StringComparer.Ordinal).ToList()));
        }

        private static IEnumerable<string> EnumerateCandidateFiles(string selectedLocationPath)
        {
            if (File.Exists(selectedLocationPath))
            {
                if (AllowedExtensions.Contains(Path.GetExtension(selectedLocationPath)))
                {
                    yield return selectedLocationPath;
                }

                yield break;
            }

            foreach (var filePath in Directory.EnumerateFiles(selectedLocationPath, "*", SearchOption.TopDirectoryOnly)
                         .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                if (AllowedExtensions.Contains(Path.GetExtension(filePath)))
                {
                    yield return filePath;
                }
            }
        }
    }
}
