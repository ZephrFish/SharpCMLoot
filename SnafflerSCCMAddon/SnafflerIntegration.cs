using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SnafflerSCCMAddon
{
    /// <summary>
    /// Integration layer for running Snaffler against SCCM indexed files
    /// </summary>
    public class SnafflerIntegration
    {
        private readonly string _snafflerExePath;
        private readonly bool _verbose;

        public SnafflerIntegration(string snafflerExePath, bool verbose = false)
        {
            _snafflerExePath = snafflerExePath;
            _verbose = verbose;

            if (!File.Exists(_snafflerExePath))
            {
                throw new FileNotFoundException($"Snaffler executable not found: {_snafflerExePath}");
            }
        }

        /// <summary>
        /// Run Snaffler against indexed SCCM files
        /// </summary>
        public SnafflerExecutionResult RunSnaffler(SnafflerRunOptions options)
        {
            Console.WriteLine("\n[*] Preparing Snaffler execution...");

            var result = new SnafflerExecutionResult
            {
                StartTime = DateTime.Now
            };

            try
            {
                // Prepare target file list
                var targetListPath = PrepareTargetList(options.IndexedFiles, options.TempDirectory);
                result.TargetListPath = targetListPath;

                // Build Snaffler command line
                var arguments = BuildSnafflerArguments(options, targetListPath);

                if (_verbose)
                    Console.WriteLine($"[*] Snaffler command: {_snafflerExePath} {arguments}");

                // Execute Snaffler
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _snafflerExePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        if (_verbose || e.Data.Contains("[RED]") || e.Data.Contains("[BLACK]"))
                        {
                            Console.WriteLine($"[Snaffler] {e.Data}");
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        Console.WriteLine($"[Snaffler ERROR] {e.Data}");
                    }
                };

                Console.WriteLine($"[*] Launching Snaffler against {options.IndexedFiles.Count} SCCM files...");

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait with timeout
                var timeout = options.TimeoutMinutes * 60 * 1000;
                var completed = process.WaitForExit(timeout);

                if (!completed)
                {
                    Console.WriteLine("[!] Snaffler execution timeout - terminating process");
                    process.Kill();
                    result.TimedOut = true;
                }

                result.ExitCode = process.ExitCode;
                result.StandardOutput = outputBuilder.ToString();
                result.StandardError = errorBuilder.ToString();
                result.EndTime = DateTime.Now;
                result.Success = completed && process.ExitCode == 0;

                Console.WriteLine($"\n[{(result.Success ? "+" : "-")}] Snaffler completed (Exit code: {result.ExitCode})");
                Console.WriteLine($"    Duration: {(result.EndTime - result.StartTime).TotalMinutes:F2} minutes");

                // Parse results if successful
                if (result.Success && !string.IsNullOrEmpty(options.OutputFile) && File.Exists(options.OutputFile))
                {
                    result.Findings = ParseSnafflerOutput(options.OutputFile);
                    Console.WriteLine($"[+] Found {result.Findings.Count} interesting items");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Snaffler execution error: {ex.Message}");
                result.Exception = ex;
                result.Success = false;
                result.EndTime = DateTime.Now;
                return result;
            }
        }

        /// <summary>
        /// Prepare target file list for Snaffler
        /// Creates a text file with all paths to analyze
        /// </summary>
        private string PrepareTargetList(List<INIFileEntry> files, string tempDirectory)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetListPath = Path.Combine(tempDirectory, $"sccm_targets_{timestamp}.txt");

            Console.WriteLine($"[*] Creating target list: {targetListPath}");

            using (var writer = new StreamWriter(targetListPath, false))
            {
                // Write INI file paths
                foreach (var file in files)
                {
                    writer.WriteLine(file.INIFilePath);
                }
            }

            Console.WriteLine($"[+] Target list created: {files.Count} files");
            return targetListPath;
        }

        /// <summary>
        /// Build Snaffler command-line arguments
        /// </summary>
        private string BuildSnafflerArguments(SnafflerRunOptions options, string targetListPath)
        {
            var args = new List<string>();

            // Output file
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                args.Add($"-o \"{options.OutputFile}\"");
            }

            // Stream to console
            if (options.StreamOutput)
            {
                args.Add("-s");
            }

            // Verbosity level (data, degub, trace, info)
            args.Add($"-v {options.VerbosityLevel}");

            // Max file size (in bytes)
            if (options.MaxFileSizeBytes > 0)
            {
                args.Add($"-n {options.MaxFileSizeBytes}");
            }

            // Custom rules file
            if (!string.IsNullOrEmpty(options.RulesFile) && File.Exists(options.RulesFile))
            {
                args.Add($"-r \"{options.RulesFile}\"");
            }

            // File path list mode (read paths from file)
            args.Add($"-f \"{targetListPath}\"");

            // Domain credentials if provided
            if (!string.IsNullOrEmpty(options.Domain) && !string.IsNullOrEmpty(options.Username))
            {
                args.Add($"-d {options.Domain}");
                args.Add($"-u {options.Username}");

                if (!string.IsNullOrEmpty(options.Password))
                {
                    args.Add($"-p {options.Password}");
                }
            }

            // Max threads
            if (options.MaxThreads > 0)
            {
                args.Add($"-t {options.MaxThreads}");
            }

            return string.Join(" ", args);
        }

        /// <summary>
        /// Parse Snaffler output file to extract findings
        /// </summary>
        private List<SnafflerFinding> ParseSnafflerOutput(string outputPath)
        {
            var findings = new List<SnafflerFinding>();

            try
            {
                if (!File.Exists(outputPath))
                    return findings;

                var lines = File.ReadAllLines(outputPath);

                foreach (var line in lines)
                {
                    // Snaffler output format: {Timestamp} [{Severity}] {FilePath} - {RuleName} - {Reason}
                    if (line.Contains("[BLACK]") || line.Contains("[RED]") ||
                        line.Contains("[YELLOW]") || line.Contains("[GREEN]"))
                    {
                        var finding = ParseSnafflerLine(line);
                        if (finding != null)
                        {
                            findings.Add(finding);
                        }
                    }
                }

                // Group findings by severity
                var grouped = findings.GroupBy(f => f.Severity).OrderByDescending(g => GetSeverityPriority(g.Key));

                Console.WriteLine("\n=== Snaffler Findings Summary ===");
                foreach (var group in grouped)
                {
                    Console.WriteLine($"  [{group.Key}]: {group.Count()} items");
                }
                Console.WriteLine("=================================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Error parsing Snaffler output: {ex.Message}");
            }

            return findings;
        }

        /// <summary>
        /// Parse a single Snaffler output line
        /// </summary>
        private SnafflerFinding ParseSnafflerLine(string line)
        {
            try
            {
                var finding = new SnafflerFinding();

                // Extract severity
                if (line.Contains("[BLACK]"))
                    finding.Severity = "BLACK";
                else if (line.Contains("[RED]"))
                    finding.Severity = "RED";
                else if (line.Contains("[YELLOW]"))
                    finding.Severity = "YELLOW";
                else if (line.Contains("[GREEN]"))
                    finding.Severity = "GREEN";

                // Extract file path (usually after severity tag)
                var severityIndex = line.IndexOf($"[{finding.Severity}]");
                if (severityIndex >= 0)
                {
                    var afterSeverity = line.Substring(severityIndex + finding.Severity.Length + 2).Trim();
                    var parts = afterSeverity.Split(new[] { " - " }, StringSplitOptions.None);

                    if (parts.Length > 0)
                        finding.FilePath = parts[0].Trim();

                    if (parts.Length > 1)
                        finding.RuleName = parts[1].Trim();

                    if (parts.Length > 2)
                        finding.Reason = string.Join(" - ", parts.Skip(2));
                }

                finding.RawLine = line;
                return finding;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get severity priority for sorting
        /// </summary>
        private int GetSeverityPriority(string severity)
        {
            switch (severity)
            {
                case "BLACK": return 4;
                case "RED": return 3;
                case "YELLOW": return 2;
                case "GREEN": return 1;
                default: return 0;
            }
        }

        /// <summary>
        /// Create enriched output with SCCM metadata
        /// Combines Snaffler findings with original SCCM file information
        /// </summary>
        public void CreateEnrichedReport(SnafflerExecutionResult result,
            List<INIFileEntry> indexedFiles, string outputPath)
        {
            Console.WriteLine($"\n[*] Creating enriched report: {outputPath}");

            try
            {
                // Build lookup dictionary
                var fileDict = indexedFiles.ToDictionary(
                    f => f.INIFilePath,
                    f => f,
                    StringComparer.OrdinalIgnoreCase
                );

                using (var writer = new StreamWriter(outputPath, false))
                {
                    writer.WriteLine("=== SCCM Content Library Analysis Report ===");
                    writer.WriteLine($"Generated: {DateTime.Now}");
                    writer.WriteLine($"Snaffler Execution Time: {(result.EndTime - result.StartTime).TotalMinutes:F2} minutes");
                    writer.WriteLine($"Total Files Analyzed: {indexedFiles.Count}");
                    writer.WriteLine($"Findings: {result.Findings.Count}");
                    writer.WriteLine("=" + new string('=', 44));
                    writer.WriteLine();

                    // Group findings by severity
                    var grouped = result.Findings
                        .GroupBy(f => f.Severity)
                        .OrderByDescending(g => GetSeverityPriority(g.Key));

                    foreach (var severityGroup in grouped)
                    {
                        writer.WriteLine($"\n### [{severityGroup.Key}] Severity Findings ({severityGroup.Count()}) ###\n");

                        foreach (var finding in severityGroup)
                        {
                            writer.WriteLine($"File: {finding.FilePath}");
                            writer.WriteLine($"Rule: {finding.RuleName}");
                            writer.WriteLine($"Reason: {finding.Reason}");

                            // Add SCCM metadata if available
                            if (fileDict.TryGetValue(finding.FilePath, out var iniEntry))
                            {
                                writer.WriteLine($"Original Name: {iniEntry.OriginalFileName}");
                                writer.WriteLine($"File Size: {iniEntry.FileSize:N0} bytes");
                                writer.WriteLine($"Content Hash: {iniEntry.ContentHash}");
                                writer.WriteLine($"Content File: {iniEntry.ContentFilePath}");
                            }

                            writer.WriteLine(new string('-', 80));
                        }
                    }
                }

                Console.WriteLine($"[+] Enriched report created: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Failed to create enriched report: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Options for Snaffler execution
    /// </summary>
    public class SnafflerRunOptions
    {
        public List<INIFileEntry> IndexedFiles { get; set; }
        public string OutputFile { get; set; }
        public string TempDirectory { get; set; } = Path.GetTempPath();
        public bool StreamOutput { get; set; } = false;
        public string VerbosityLevel { get; set; } = "data";
        public int MaxFileSizeBytes { get; set; } = 500 * 1024; // 500KB default
        public string RulesFile { get; set; }
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int MaxThreads { get; set; } = 30;
        public int TimeoutMinutes { get; set; } = 60;
    }

    /// <summary>
    /// Result of Snaffler execution
    /// </summary>
    public class SnafflerExecutionResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
        public bool TimedOut { get; set; }
        public Exception Exception { get; set; }
        public string TargetListPath { get; set; }
        public List<SnafflerFinding> Findings { get; set; } = new List<SnafflerFinding>();
    }

    /// <summary>
    /// Snaffler finding entry
    /// </summary>
    public class SnafflerFinding
    {
        public string Severity { get; set; }
        public string FilePath { get; set; }
        public string RuleName { get; set; }
        public string Reason { get; set; }
        public string RawLine { get; set; }

        public override string ToString()
        {
            return $"[{Severity}] {FilePath} - {RuleName}";
        }
    }
}
