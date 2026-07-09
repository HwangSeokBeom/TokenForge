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
        // Codex stores sessions below year/month/day directories and Claude can nest by
        // project. The file/entry caps still bound work, while this depth reaches real logs.
        private const int MaxDirectoryDepth = 6;
        private const long MaxFileSizeBytes = 4L * 1024L * 1024L;

        public Task<AgentLogReadResult> ReadAsync(AgentAnalysisInput input, CancellationToken cancellationToken)
        {
            input = input ?? new AgentAnalysisInput();
            if (string.IsNullOrWhiteSpace(input.SelectedLocationPath))
            {
                return Task.FromResult(AgentLogReadResult.Failure("agent_log_location_missing"));
            }

            if (!ExistsSafely(input.SelectedLocationPath))
            {
                return Task.FromResult(AgentLogReadResult.Failure("agent_log_location_unavailable"));
            }

            var maxFiles = Math.Max(1, Math.Min(input.MaxFilesToScan, AgentAnalysisInput.MaxFilesToScanLimit));
            var maxEntries = Math.Max(1, Math.Min(input.MaxLogEntriesToScan, AgentAnalysisInput.MaxLogEntriesToScanLimit));
            var warnings = new List<string>();
            var entries = new List<AgentLogEntry>();

            try
            {
                var filesRead = 0;
                foreach (var filePath in EnumerateCandidateFiles(input.SelectedLocationPath, cancellationToken).Take(maxFiles))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > MaxFileSizeBytes)
                    {
                        warnings.Add("agent_log_file_size_limit_reached");
                        continue;
                    }

                    filesRead++;
                    foreach (var line in File.ReadLines(filePath).Take(Math.Max(0, maxEntries - entries.Count)))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            entries.Add(new AgentLogEntry
                            {
                                Text = line,
                                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                                SourceKey = "file-" + filesRead
                            });
                        }
                    }

                    if (entries.Count >= maxEntries)
                    {
                        warnings.Add("agent_log_entry_limit_reached");
                        break;
                    }
                }

                if (filesRead >= maxFiles)
                {
                    warnings.Add("agent_log_file_limit_reached");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                return Task.FromResult(AgentLogReadResult.Failure("agent_log_permission_required"));
            }
            catch (IOException)
            {
                return Task.FromResult(AgentLogReadResult.Failure("agent_log_read_failed"));
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

        private static bool ExistsSafely(string path)
        {
            try
            {
                return Directory.Exists(path) || File.Exists(path);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        private static IEnumerable<string> EnumerateCandidateFiles(string selectedLocationPath, CancellationToken cancellationToken)
        {
            if (File.Exists(selectedLocationPath))
            {
                if (AllowedExtensions.Contains(Path.GetExtension(selectedLocationPath)))
                {
                    yield return selectedLocationPath;
                }

                yield break;
            }

            foreach (var filePath in EnumerateCandidateFiles(selectedLocationPath, 0, cancellationToken)
                         .OrderByDescending(GetLastWriteTimeUtcSafely))
            {
                if (AllowedExtensions.Contains(Path.GetExtension(filePath)))
                {
                    yield return filePath;
                }
            }
        }

        private static IEnumerable<string> EnumerateCandidateFiles(string directory, int depth, CancellationToken cancellationToken)
        {
            if (depth > MaxDirectoryDepth)
            {
                yield break;
            }

            DirectoryInfo directoryInfo;
            try
            {
                directoryInfo = new DirectoryInfo(directory);
                if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    yield break;
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                yield break;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory);
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                yield break;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory)
                    .Where(child => !ShouldSkipDirectory(child));
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (IOException)
            {
                yield break;
            }

            foreach (var child in children)
            {
                foreach (var file in EnumerateCandidateFiles(child, depth + 1, cancellationToken))
                {
                    yield return file;
                }
            }
        }

        private static bool ShouldSkipDirectory(string path)
        {
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(name))
            {
                return true;
            }

            if (name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Library", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static DateTime GetLastWriteTimeUtcSafely(string path)
        {
            try
            {
                return File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }
    }
}
