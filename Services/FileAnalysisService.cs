using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SCML.Services
{
    public class FileAnalysisService
    {
        private readonly bool _verbose;
        private readonly string[] _sensitivePatterns = new[]
        {
            "password", "pwd", "passwd", "credential", "cred",
            "secret", "token", "apikey", "api_key", "privatekey",
            "private_key", "connectionstring", "conn_string"
        };

        private readonly string[] _interestingFiles = new[]
        {
            "unattend.xml", "sysprep.xml", "web.config", "app.config",
            "applicationhost.config", "machine.config", "settings.xml"
        };

        public FileAnalysisService(bool verbose = false)
        {
            _verbose = verbose;
        }

        public void AnalyseDownloadedFiles(string outputDirectory)
        {
            if (!Directory.Exists(outputDirectory))
            {
                Console.WriteLine(string.Format("[-] Output directory not found: {0}", outputDirectory));
                return;
            }

            Console.WriteLine("\n[*] Analysing downloaded files for sensitive information...");
            
            var files = Directory.GetFiles(outputDirectory, "*.*", SearchOption.AllDirectories);
            var findings = new List<Finding>();

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileName(file).ToLower();
                    
                    // Check if it's an interesting file by name
                    if (_interestingFiles.Any(f => fileName.Contains(f.ToLower())))
                    {
                        findings.Add(new Finding
                        {
                            FilePath = file,
                            Type = "Interesting File",
                            Description = string.Format("Known configuration file: {0}", fileName)
                        });
                    }

                    // Analyse file content for text files
                    if (IsTextFile(file))
                    {
                        AnalyseFileContent(file, findings);
                    }
                }
                catch (Exception ex)
                {
                    if (_verbose)
                        Console.WriteLine(string.Format("[-] Error analysing {0}: {1}", file, ex.Message));
                }
            }

            // Display findings
            if (findings.Count > 0)
            {
                Console.WriteLine(string.Format("\n[+] Found {0} items of interest:", findings.Count));
                
                var groupedFindings = findings.GroupBy(f => f.Type);
                foreach (var group in groupedFindings)
                {
                    Console.WriteLine(string.Format("\n  {0}:", group.Key));
                    foreach (var finding in group.Take(10)) // Limit output
                    {
                        Console.WriteLine(string.Format("    - {0}", finding.Description));
                        if (_verbose)
                            Console.WriteLine(string.Format("      File: {0}", finding.FilePath));
                    }
                    
                    if (group.Count() > 10)
                        Console.WriteLine(string.Format("    ... and {0} more", group.Count() - 10));
                }

                // Save detailed findings to file
                SaveFindingsToFile(findings, Path.Combine(outputDirectory, "analysis_results.txt"));
            }
            else
            {
                Console.WriteLine("[+] No sensitive information found in downloaded files.");
            }
        }

        private void AnalyseFileContent(string filePath, List<Finding> findings)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var lines = File.ReadAllLines(filePath);
                
                // Check for sensitive patterns
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var lineLower = line.ToLower();
                    
                    foreach (var pattern in _sensitivePatterns)
                    {
                        if (lineLower.Contains(pattern))
                        {
                            // Try to extract the value
                            var value = ExtractSensitiveValue(line, pattern);
                            
                            findings.Add(new Finding
                            {
                                FilePath = filePath,
                                Type = "Sensitive Information",
                                Description = string.Format("Found '{0}' at line {1}", pattern, i + 1),
                                LineNumber = i + 1,
                                Context = SanitizeContext(line)
                            });
                            
                            break; // Only report once per line
                        }
                    }
                }

                // Check for Base64 encoded strings
                var base64Pattern = @"[A-Za-z0-9+/]{20,}={0,2}";
                var base64Matches = Regex.Matches(content, base64Pattern);
                
                if (base64Matches.Count > 0)
                {
                    findings.Add(new Finding
                    {
                        FilePath = filePath,
                        Type = "Potential Encoding",
                        Description = string.Format("Found {0} potential Base64 encoded strings", base64Matches.Count)
                    });
                }

                // Check for connection strings
                var connStringPattern = @"(Server|Data Source|Initial Catalog|User ID|Password)\s*=\s*[^;]+";
                var connStringMatches = Regex.Matches(content, connStringPattern, RegexOptions.IgnoreCase);
                
                if (connStringMatches.Count > 0)
                {
                    findings.Add(new Finding
                    {
                        FilePath = filePath,
                        Type = "Connection String",
                        Description = "Found potential database connection string"
                    });
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine(string.Format("[-] Error reading file {0}: {1}", filePath, ex.Message));
            }
        }

        private string ExtractSensitiveValue(string line, string pattern)
        {
            // Try to extract value after = or :
            var patterns = new[]
            {
                string.Format(@"{0}\s*[:=]\s*[""']([^""']+)[""']", pattern),
                string.Format(@"{0}\s*[:=]\s*([^\s]+)", pattern)
            };

            foreach (var p in patterns)
            {
                var match = Regex.Match(line, p, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
            }

            return null;
        }

        private string SanitizeContext(string line)
        {
            // Truncate long lines and mask potential sensitive values
            if (line.Length > 100)
                line = line.Substring(0, 100) + "...";
            
            // Simple masking of potential values
            line = Regex.Replace(line, @"(password|pwd|token|key)\s*[:=]\s*\S+", "$1=***MASKED***", RegexOptions.IgnoreCase);
            
            return line;
        }

        private bool IsTextFile(string filePath)
        {
            var textExtensions = new[] { ".xml", ".txt", ".config", ".ini", ".ps1", ".vbs", ".bat", ".cmd", ".json", ".yml", ".yaml" };
            var extension = Path.GetExtension(filePath).ToLower();
            return textExtensions.Contains(extension);
        }

        private void SaveFindingsToFile(List<Finding> findings, string outputPath)
        {
            try
            {
                using (var writer = new StreamWriter(outputPath))
                {
                    writer.WriteLine("SCCM Content Analysis Results");
                    writer.WriteLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    writer.WriteLine(new string('=', 50));
                    writer.WriteLine();

                    var groupedFindings = findings.GroupBy(f => f.Type);
                    foreach (var group in groupedFindings)
                    {
                        writer.WriteLine(group.Key + ":");
                        writer.WriteLine(new string('-', group.Key.Length + 1));
                        
                        foreach (var finding in group)
                        {
                            writer.WriteLine(string.Format("  File: {0}", finding.FilePath));
                            writer.WriteLine(string.Format("  Description: {0}", finding.Description));
                            
                            if (finding.LineNumber > 0)
                                writer.WriteLine(string.Format("  Line: {0}", finding.LineNumber));
                            
                            if (!string.IsNullOrEmpty(finding.Context))
                                writer.WriteLine(string.Format("  Context: {0}", finding.Context));
                            
                            writer.WriteLine();
                        }
                    }
                }

                Console.WriteLine(string.Format("\n[+] Detailed analysis saved to: {0}", outputPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error saving analysis results: {0}", ex.Message));
            }
        }

        private class Finding
        {
            public string FilePath { get; set; }
            public string Type { get; set; }
            public string Description { get; set; }
            public int LineNumber { get; set; }
            public string Context { get; set; }
        }
    }
}