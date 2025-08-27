using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SCML.Models;

namespace SCML.Services
{
    /// <summary>
    /// Enhanced file analysis service using Snaffler-style rule engine
    /// </summary>
    public class EnhancedFileAnalysisService
    {
        private readonly SnafflerRuleEngine _ruleEngine;
        private readonly bool _verbose;
        private readonly string _outputDirectory;
        private readonly string _baseOutputFile;

        public EnhancedFileAnalysisService(string outputDirectory, bool verbose = false, string baseOutputFile = null)
        {
            _ruleEngine = new SnafflerRuleEngine(verbose);
            _verbose = verbose;
            _outputDirectory = outputDirectory;
            _baseOutputFile = baseOutputFile;
        }

        public void AnalyseDownloadedFiles()
        {
            if (!Directory.Exists(_outputDirectory))
            {
                Console.WriteLine(string.Format("[-] Output directory not found: {0}", _outputDirectory));
                return;
            }

            Console.WriteLine("\n========================================");
            Console.WriteLine("   SNAFFLER-STYLE SENSITIVITY ANALYSIS");
            Console.WriteLine("========================================\n");

            Console.WriteLine("[*] Analysing downloaded files for sensitive content...");
            Console.WriteLine("[*] Using Snaffler rule engine with sensitivity ratings\n");

            // Get all files to analyse
            var files = Directory.GetFiles(_outputDirectory, "*.*", SearchOption.AllDirectories).ToList();
            Console.WriteLine(string.Format("[*] Found {0} files to analyse\n", files.Count));

            // Analyse each file
            var allResults = new List<MatchResult>();
            var progressCounter = 0;

            foreach (var file in files)
            {
                progressCounter++;
                if (!_verbose && progressCounter % 10 == 0)
                {
                    Console.Write(".");
                    if (progressCounter % 100 == 0)
                        Console.WriteLine(string.Format(" {0}/{1}", progressCounter, files.Count));
                }

                var results = _ruleEngine.AnalyseFile(file);
                allResults.AddRange(results);
            }

            if (!_verbose)
                Console.WriteLine();

            // Generate and display report
            var report = _ruleEngine.GenerateReport(allResults);
            DisplayReport(report);
            SaveDetailedReport(report, allResults);
            GenerateCleanupScript(report, allResults);
        }

        private void DisplayReport(SensitivityReport report)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("         SENSITIVITY REPORT");
            Console.WriteLine("========================================");

            Console.WriteLine(string.Format("\nOverall Risk Score: {0}/100", report.OverallRiskScore));
            Console.WriteLine(string.Format("Total Files Analysed: {0}", report.TotalFiles));
            Console.WriteLine(string.Format("Total Matches Found: {0}\n", report.TotalMatches));

            // Display risk rating
            string riskRating;
            ConsoleColor riskColour;
            
            if (report.OverallRiskScore >= 80)
            {
                riskRating = "CRITICAL - Immediate cleanup required!";
                riskColour = ConsoleColor.Red;
            }
            else if (report.OverallRiskScore >= 60)
            {
                riskRating = "HIGH - Significant sensitive data found";
                riskColour = ConsoleColor.DarkRed;
            }
            else if (report.OverallRiskScore >= 40)
            {
                riskRating = "MEDIUM - Some sensitive data present";
                riskColour = ConsoleColor.Yellow;
            }
            else if (report.OverallRiskScore >= 20)
            {
                riskRating = "LOW - Minor sensitive data detected";
                riskColour = ConsoleColor.DarkYellow;
            }
            else
            {
                riskRating = "MINIMAL - No significant sensitive data";
                riskColour = ConsoleColor.Green;
            }

            var originalColour = Console.ForegroundColor;
            Console.ForegroundColor = riskColour;
            Console.WriteLine(string.Format("Risk Assessment: {0}", riskRating));
            Console.ForegroundColor = originalColour;

            // Display findings by severity
            foreach (var finding in report.Findings)
            {
                Console.WriteLine(string.Format("\n[{0}] Severity - {1} matches (Score: {2})",
                    GetSeverityDisplay(finding.Level), finding.Count, finding.TotalScore));

                // Show top rules for this severity
                var topRules = finding.RuleMatches.Take(5);
                foreach (var rule in topRules)
                {
                    Console.WriteLine(string.Format("  → {0}: {1} matches in {2} file(s)",
                        rule.RuleName, rule.MatchCount, rule.Files.Count));
                    
                    // Show first few files for each rule (even without verbose)
                    foreach (var file in rule.Files.Take(3))
                    {
                        // Get relative path from output directory
                        string displayPath = file;
                        if (displayPath.StartsWith(_outputDirectory))
                        {
                            displayPath = displayPath.Substring(_outputDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        }
                        Console.WriteLine(string.Format("      • {0}", displayPath));
                    }
                    if (rule.Files.Count > 3)
                    {
                        Console.WriteLine(string.Format("      ... and {0} more files", rule.Files.Count - 3));
                    }
                }

                if (finding.RuleMatches.Count > 5)
                {
                    Console.WriteLine(string.Format("  ... and {0} more rules", finding.RuleMatches.Count - 5));
                }
            }

            Console.WriteLine("\n========================================");
        }

        private void SaveDetailedReport(SensitivityReport report, List<MatchResult> allResults)
        {
            // Use base output file name if provided, otherwise default
            string baseFileName = "snaffler_analysis";
            if (!string.IsNullOrEmpty(_baseOutputFile))
            {
                baseFileName = Path.GetFileNameWithoutExtension(_baseOutputFile) + "_snaffler";
            }
            
            var reportPath = Path.Combine(_outputDirectory, baseFileName + "_report.txt");
            var csvPath = Path.Combine(_outputDirectory, baseFileName + "_findings.csv");

            try
            {
                // Save detailed text report
                using (var writer = new StreamWriter(reportPath))
                {
                    writer.WriteLine("SCCM Content Explorer - Snaffler-Style Analysis Report");
                    writer.WriteLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    writer.WriteLine(new string('=', 70));
                    writer.WriteLine();
                    writer.WriteLine(string.Format("Overall Risk Score: {0}/100", report.OverallRiskScore));
                    writer.WriteLine(string.Format("Total Files with Findings: {0}", report.TotalFiles));
                    writer.WriteLine(string.Format("Total Sensitivity Matches: {0}", report.TotalMatches));
                    writer.WriteLine();

                    // Group results by file
                    var fileGroups = allResults.GroupBy(r => r.FilePath)
                        .OrderByDescending(g => g.Max(r => (int)r.Severity))
                        .ThenByDescending(g => g.Sum(r => r.Score));

                    foreach (var fileGroup in fileGroups)
                    {
                        var maxSeverity = fileGroup.Max(r => r.Severity);
                        var totalScore = fileGroup.Sum(r => r.Score);
                        
                        // Get relative path from output directory
                        string displayPath = fileGroup.Key;
                        if (displayPath.StartsWith(_outputDirectory))
                        {
                            displayPath = displayPath.Substring(_outputDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        }
                        
                        writer.WriteLine(new string('-', 70));
                        writer.WriteLine(string.Format("File: {0}", displayPath));
                        writer.WriteLine(string.Format("Full Path: {0}", fileGroup.Key));
                        writer.WriteLine(string.Format("Maximum Severity: {0}", maxSeverity));
                        writer.WriteLine(string.Format("Total Score: {0}", totalScore));
                        writer.WriteLine("Matches:");

                        foreach (var match in fileGroup.OrderByDescending(m => (int)m.Severity))
                        {
                            writer.WriteLine(string.Format("  [{0}] Rule: {1}", 
                                GetSeverityDisplay(match.Severity), match.MatchedRule.RuleName));
                            writer.WriteLine(string.Format("       Description: {0}", 
                                match.MatchedRule.Description));
                            
                            if (match.LineNumber > 0)
                            {
                                writer.WriteLine(string.Format("       Line: {0}", match.LineNumber));
                                if (!string.IsNullOrEmpty(match.Context))
                                {
                                    writer.WriteLine("       Context:");
                                    foreach (var line in match.Context.Split('\n'))
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                            writer.WriteLine(string.Format("         {0}", line.Trim()));
                                    }
                                }
                            }
                            writer.WriteLine();
                        }
                    }
                }

                // Save CSV for easy sorting/filtering
                using (var csvWriter = new StreamWriter(csvPath))
                {
                    csvWriter.WriteLine("File,Full Path,Severity,Rule,Description,Score,Line,Matched Text");
                    
                    foreach (var result in allResults.OrderByDescending(r => (int)r.Severity))
                    {
                        // Get relative path from output directory
                        string displayPath = result.FilePath;
                        if (displayPath.StartsWith(_outputDirectory))
                        {
                            displayPath = displayPath.Substring(_outputDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        }
                        
                        csvWriter.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",{5},{6},\"{7}\"",
                            displayPath,
                            result.FilePath,
                            result.Severity,
                            result.MatchedRule.RuleName,
                            result.MatchedRule.Description,
                            result.Score,
                            result.LineNumber,
                            result.MatchedText?.Replace("\"", "\"\"")));
                    }
                }

                Console.WriteLine(string.Format("\n[+] Detailed report saved to: {0}", reportPath));
                Console.WriteLine(string.Format("[+] CSV findings saved to: {0}", csvPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error saving reports: {0}", ex.Message));
            }
        }

        private void GenerateCleanupScript(SensitivityReport report, List<MatchResult> allResults)
        {
            var scriptPath = Path.Combine(_outputDirectory, "cleanup_sensitive_files.bat");
            var psScriptPath = Path.Combine(_outputDirectory, "cleanup_sensitive_files.ps1");

            try
            {
                // Get files by severity for cleanup prioritisation
                var criticalFiles = allResults
                    .Where(r => r.Severity == SensitivityLevel.Black)
                    .Select(r => r.FilePath)
                    .Distinct()
                    .ToList();

                var highRiskFiles = allResults
                    .Where(r => r.Severity == SensitivityLevel.Red)
                    .Select(r => r.FilePath)
                    .Distinct()
                    .Except(criticalFiles)
                    .ToList();

                // Create batch script
                using (var writer = new StreamWriter(scriptPath))
                {
                    writer.WriteLine("@echo off");
                    writer.WriteLine("REM Cleanup script for sensitive files");
                    writer.WriteLine("REM Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    writer.WriteLine();
                    writer.WriteLine("echo ========================================");
                    writer.WriteLine("echo    SENSITIVE FILE CLEANUP SCRIPT");
                    writer.WriteLine("echo ========================================");
                    writer.WriteLine();
                    
                    if (criticalFiles.Count > 0)
                    {
                        writer.WriteLine("echo.");
                        writer.WriteLine("echo [CRITICAL] Files with BLACK severity rating:");
                        writer.WriteLine("echo These files contain highly sensitive data and should be removed immediately!");
                        writer.WriteLine("pause");
                        
                        foreach (var file in criticalFiles)
                        {
                            writer.WriteLine(string.Format("del /P \"{0}\"", file));
                        }
                    }

                    if (highRiskFiles.Count > 0)
                    {
                        writer.WriteLine();
                        writer.WriteLine("echo.");
                        writer.WriteLine("echo [HIGH RISK] Files with RED severity rating:");
                        writer.WriteLine("echo These files contain sensitive data and should be reviewed!");
                        writer.WriteLine("pause");
                        
                        foreach (var file in highRiskFiles)
                        {
                            writer.WriteLine(string.Format("REM del \"{0}\"", file));
                        }
                    }

                    writer.WriteLine();
                    writer.WriteLine("echo Cleanup complete!");
                    writer.WriteLine("pause");
                }

                // Create PowerShell script with more options
                using (var psWriter = new StreamWriter(psScriptPath))
                {
                    psWriter.WriteLine("# Sensitive File Cleanup Script");
                    psWriter.WriteLine("# Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    psWriter.WriteLine();
                    psWriter.WriteLine("Write-Host '========================================' -ForegroundColor Cyan");
                    psWriter.WriteLine("Write-Host '   SENSITIVE FILE CLEANUP SCRIPT' -ForegroundColor Cyan");
                    psWriter.WriteLine("Write-Host '========================================' -ForegroundColor Cyan");
                    psWriter.WriteLine();

                    if (criticalFiles.Count > 0)
                    {
                        psWriter.WriteLine("Write-Host ''");
                        psWriter.WriteLine("Write-Host '[CRITICAL FILES - BLACK SEVERITY]' -ForegroundColor Red");
                        psWriter.WriteLine("Write-Host 'These files contain highly sensitive data!' -ForegroundColor Red");
                        psWriter.WriteLine();
                        psWriter.WriteLine("$criticalFiles = @(");
                        foreach (var file in criticalFiles)
                        {
                            psWriter.WriteLine(string.Format("    '{0}'", file.Replace("'", "''")));
                        }
                        psWriter.WriteLine(")");
                        psWriter.WriteLine();
                        psWriter.WriteLine("foreach ($file in $criticalFiles) {");
                        psWriter.WriteLine("    if (Test-Path $file) {");
                        psWriter.WriteLine("        Write-Host \"Found: $file\" -ForegroundColor Yellow");
                        psWriter.WriteLine("        $response = Read-Host 'Delete this file? (Y/N)'");
                        psWriter.WriteLine("        if ($response -eq 'Y') {");
                        psWriter.WriteLine("            Remove-Item $file -Force");
                        psWriter.WriteLine("            Write-Host 'File deleted!' -ForegroundColor Green");
                        psWriter.WriteLine("        }");
                        psWriter.WriteLine("    }");
                        psWriter.WriteLine("}");
                    }

                    if (highRiskFiles.Count > 0)
                    {
                        psWriter.WriteLine();
                        psWriter.WriteLine("Write-Host ''");
                        psWriter.WriteLine("Write-Host '[HIGH RISK FILES - RED SEVERITY]' -ForegroundColor DarkRed");
                        psWriter.WriteLine("Write-Host 'Review these files for sensitive content' -ForegroundColor DarkRed");
                        psWriter.WriteLine();
                        psWriter.WriteLine("$highRiskFiles = @(");
                        foreach (var file in highRiskFiles)
                        {
                            psWriter.WriteLine(string.Format("    '{0}'", file.Replace("'", "''")));
                        }
                        psWriter.WriteLine(")");
                        psWriter.WriteLine();
                        psWriter.WriteLine("Write-Host 'High risk files listed. Review manually.' -ForegroundColor Yellow");
                    }

                    psWriter.WriteLine();
                    psWriter.WriteLine("Write-Host ''");
                    psWriter.WriteLine("Write-Host 'Cleanup process complete!' -ForegroundColor Green");
                    psWriter.WriteLine("Read-Host 'Press Enter to exit'");
                }

                Console.WriteLine(string.Format("[+] Cleanup script generated: {0}", scriptPath));
                Console.WriteLine(string.Format("[+] PowerShell cleanup script: {0}", psScriptPath));
                
                if (criticalFiles.Count > 0)
                {
                    Console.WriteLine(string.Format("\n[!] WARNING: {0} CRITICAL files found requiring immediate attention!", 
                        criticalFiles.Count));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error generating cleanup scripts: {0}", ex.Message));
            }
        }

        private string GetSeverityDisplay(SensitivityLevel level)
        {
            switch (level)
            {
                case SensitivityLevel.Black:
                    return "BLACK";
                case SensitivityLevel.Red:
                    return "RED  ";
                case SensitivityLevel.Yellow:
                    return "YELLW";
                case SensitivityLevel.Green:
                    return "GREEN";
                default:
                    return "INFO ";
            }
        }
    }
}