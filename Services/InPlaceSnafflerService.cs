using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SCML.Models;

namespace SCML.Services
{
    /// <summary>
    /// Service to run Snaffler analysis on files in-place on the share without downloading
    /// </summary>
    public class InPlaceSnafflerService
    {
        private readonly SmbService _smbService;
        private readonly bool _verbose;
        private readonly SnafflerRuleEngine _ruleEngine;
        private StreamWriter _outputWriter;
        private StreamWriter _csvWriter;
        private int _filesAnalysed = 0;
        private int _interestingFindings = 0;
        private readonly Dictionary<string, int> _findingsByLevel = new Dictionary<string, int>();
        private readonly List<MatchResult> _allResults = new List<MatchResult>();

        public InPlaceSnafflerService(SmbService smbService, bool verbose = false)
        {
            _smbService = smbService;
            _verbose = verbose;
            _ruleEngine = new SnafflerRuleEngine(verbose);
        }

        public void AnalyseShareFiles(string inventoryFile, string outputFile, IEnumerable<string> extensions = null)
        {
            // Wait a moment to ensure inventory file is fully written and closed
            System.Threading.Thread.Sleep(500);
            
            if (!File.Exists(inventoryFile))
            {
                Console.WriteLine($"[-] Inventory file not found: {inventoryFile}");
                return;
            }
            
            // Make sure we're writing to a different file than we're reading from
            if (string.Equals(inventoryFile, outputFile, StringComparison.OrdinalIgnoreCase))
            {
                outputFile = inventoryFile.Replace(".txt", "_snaffler_results.txt");
                if (outputFile == inventoryFile) // If no .txt extension
                    outputFile = inventoryFile + "_snaffler_results";
                Console.WriteLine($"[*] Output file adjusted to: {outputFile}");
            }

            Console.WriteLine("[*] Starting in-place Snaffler analysis on share files...");
            
            // Initialize output files
            try
            {
                _outputWriter = new StreamWriter(outputFile, false);
                _outputWriter.WriteLine($"=== SNAFFLER IN-PLACE ANALYSIS REPORT ===");
                _outputWriter.WriteLine($"Analysis Started: {DateTime.Now}");
                _outputWriter.WriteLine($"Inventory Source: {inventoryFile}");
                _outputWriter.WriteLine("=========================================\n");
                _outputWriter.Flush();
                
                // Initialize CSV file
                var csvFile = outputFile.Replace(".txt", ".csv");
                if (!csvFile.EndsWith(".csv"))
                    csvFile += ".csv";
                    
                _csvWriter = new StreamWriter(csvFile, false);
                _csvWriter.WriteLine("FilePath,FileSize,Severity,Rule,Description,MatchedPattern,MatchedText,Context");
                _csvWriter.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Failed to create output files: {ex.Message}");
                return;
            }

            try
            {
                // Read inventory file with retry logic in case it's still being finalized
                string[] lines = null;
                int retryCount = 0;
                while (retryCount < 3)
                {
                    try
                    {
                        lines = File.ReadAllLines(inventoryFile);
                        break;
                    }
                    catch (IOException)
                    {
                        retryCount++;
                        if (retryCount >= 3)
                            throw;
                        System.Threading.Thread.Sleep(500);
                    }
                }
                
                if (lines == null)
                {
                    Console.WriteLine("[-] Unable to read inventory file after retries");
                    return;
                }
                var filesToAnalyse = new List<string>();

                // Filter files based on extensions if specified
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // If extensions specified, filter by them
                    if (extensions != null && extensions.Any())
                    {
                        var fileExtension = Path.GetExtension(line);
                        if (extensions.Any(ext => fileExtension.Equals("." + ext, StringComparison.OrdinalIgnoreCase)))
                        {
                            filesToAnalyse.Add(line);
                        }
                    }
                    else
                    {
                        // Analyse all files
                        filesToAnalyse.Add(line);
                    }
                }

                Console.WriteLine($"[*] Found {filesToAnalyse.Count} files to analyse");

                int currentFile = 0;
                foreach (var filePath in filesToAnalyse)
                {
                    currentFile++;
                    
                    if (currentFile % 10 == 0)
                    {
                        Console.WriteLine($"[*] Progress: {currentFile}/{filesToAnalyse.Count} files analysed");
                    }

                    AnalyseSingleFile(filePath);
                }

                // Write summary
                WriteSummary();
                
                Console.WriteLine($"\n[+] Analysis complete: {_filesAnalysed} files analysed");
                Console.WriteLine($"[+] Found {_interestingFindings} interesting items");
                Console.WriteLine($"[+] Results written to: {outputFile}");
                
                var csvFile = outputFile.Replace(".txt", ".csv");
                if (!csvFile.EndsWith(".csv"))
                    csvFile += ".csv";
                Console.WriteLine($"[+] CSV results written to: {csvFile}");
            }
            finally
            {
                _outputWriter?.Close();
                _outputWriter?.Dispose();
                _csvWriter?.Close();
                _csvWriter?.Dispose();
            }
        }

        private void AnalyseSingleFile(string filePath)
        {
            try
            {
                // Parse the UNC path to get relative path
                var match = Regex.Match(filePath, @"\\\\[^\\]+\\[^\\]+\\(.+)");
                if (!match.Success)
                {
                    if (_verbose)
                        Console.WriteLine($"[-] Invalid file path format: {filePath}");
                    return;
                }

                var relativePath = match.Groups[1].Value;
                
                // Check file size first (skip very large files)
                const long maxFileSize = 10 * 1024 * 1024; // 10MB limit for content analysis
                
                try
                {
                    // For INI files, we need to add .INI extension back
                    var actualPath = relativePath;
                    if (!actualPath.EndsWith(".INI", StringComparison.OrdinalIgnoreCase))
                    {
                        // Check if INI file exists for this file
                        var iniPath = actualPath + ".INI";
                        if (FileExistsOnShare(iniPath))
                        {
                            actualPath = iniPath;
                        }
                    }

                    // Read file content for analysis
                    var fileContent = _smbService.ReadFile(actualPath);
                    
                    if (fileContent.Length > maxFileSize)
                    {
                        if (_verbose)
                            Console.WriteLine($"[!] Skipping large file (>{maxFileSize/1024/1024}MB): {filePath}");
                        return;
                    }

                    var content = Encoding.UTF8.GetString(fileContent);
                    
                    // Run Snaffler rules by analyzing the file path first
                    var results = _ruleEngine.AnalyseFile(filePath);
                    
                    // Also analyze content if we have it
                    if (!string.IsNullOrEmpty(content))
                    {
                        // The rule engine analyzes files from disk, so we'll need to check content manually
                        // For now, just check the path-based rules
                    }
                    
                    if (results.Any())
                    {
                        _interestingFindings++;
                        
                        // Store results for CSV
                        _allResults.AddRange(results);
                        
                        // Write findings immediately to output file
                        _outputWriter.WriteLine($"\n[FILE] {filePath}");
                        _outputWriter.WriteLine($"[SIZE] {fileContent.Length} bytes");
                        
                        foreach (var result in results)
                        {
                            var levelStr = result.Severity.ToString().ToUpper();
                            
                            // Track findings by level
                            if (!_findingsByLevel.ContainsKey(levelStr))
                                _findingsByLevel.Add(levelStr, 0);
                            _findingsByLevel[levelStr]++;
                            
                            _outputWriter.WriteLine($"  [{levelStr}] {result.MatchedRule.RuleName}: {result.MatchedRule.Description}");
                            
                            if (!string.IsNullOrEmpty(result.MatchedPattern))
                            {
                                _outputWriter.WriteLine($"    Pattern: {result.MatchedPattern}");
                            }
                            
                            if (!string.IsNullOrEmpty(result.MatchedText))
                            {
                                // Truncate matched content if too long
                                var preview = result.MatchedText.Length > 100 
                                    ? result.MatchedText.Substring(0, 100) + "..." 
                                    : result.MatchedText;
                                _outputWriter.WriteLine($"    Match: {preview}");
                            }
                            
                            if (!string.IsNullOrEmpty(result.Context))
                            {
                                _outputWriter.WriteLine($"    Context: {result.Context}");
                            }
                            
                            // Write to CSV immediately
                            WriteToCsv(result, fileContent.Length);
                        }
                        
                        _outputWriter.Flush(); // Flush immediately so results are available in real-time
                        _csvWriter?.Flush();
                        
                        // Console output for high-value findings
                        var highestLevel = results.Max(r => r.Severity);
                        if (highestLevel >= SensitivityLevel.Red)
                        {
                            Console.WriteLine($"[!] HIGH VALUE: {Path.GetFileName(filePath)} - {results.First().MatchedRule.Description}");
                        }
                    }
                    
                    _filesAnalysed++;
                }
                catch (Exception ex)
                {
                    if (_verbose)
                        Console.WriteLine($"[-] Error reading file {filePath}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[-] Error analyzing file {filePath}: {ex.Message}");
            }
        }

        private bool FileExistsOnShare(string path)
        {
            try
            {
                // Try to read the file - if it exists, this will succeed
                _smbService.ReadFile(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void WriteSummary()
        {
            _outputWriter.WriteLine("\n=========================================");
            _outputWriter.WriteLine("=== ANALYSIS SUMMARY ===");
            _outputWriter.WriteLine($"Total Files Analyzed: {_filesAnalysed}");
            _outputWriter.WriteLine($"Interesting Findings: {_interestingFindings}");
            
            if (_findingsByLevel.Any())
            {
                _outputWriter.WriteLine("\nFindings by Sensitivity Level:");
                foreach (var kvp in _findingsByLevel.OrderByDescending(x => x.Value))
                {
                    _outputWriter.WriteLine($"  {kvp.Key}: {kvp.Value}");
                }
            }
            
            _outputWriter.WriteLine($"\nAnalysis Completed: {DateTime.Now}");
            _outputWriter.WriteLine("=========================================");
            _outputWriter.Flush();
        }
        
        private void WriteToCsv(MatchResult result, long fileSize)
        {
            if (_csvWriter == null)
                return;
                
            try
            {
                // Escape quotes in text fields
                var escapedPath = result.FilePath?.Replace("\"", "\"\"");
                var escapedRule = result.MatchedRule?.RuleName?.Replace("\"", "\"\"");
                var escapedDesc = result.MatchedRule?.Description?.Replace("\"", "\"\"");
                var escapedPattern = result.MatchedPattern?.Replace("\"", "\"\"");
                var escapedText = result.MatchedText?.Replace("\"", "\"\"");
                var escapedContext = result.Context?.Replace("\"", "\"\"");
                
                // Truncate matched text if too long for CSV
                if (escapedText?.Length > 500)
                {
                    escapedText = escapedText.Substring(0, 500) + "...";
                }
                
                _csvWriter.WriteLine(string.Format("\"{0}\",{1},\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",\"{7}\"",
                    escapedPath ?? "",
                    fileSize,
                    result.Severity.ToString().ToUpper(),
                    escapedRule ?? "",
                    escapedDesc ?? "",
                    escapedPattern ?? "",
                    escapedText ?? "",
                    escapedContext ?? ""
                ));
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[-] Error writing to CSV: {ex.Message}");
            }
        }
    }
}