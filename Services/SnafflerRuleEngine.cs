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
    /// Snaffler-inspired rule engine for file classification and sensitivity scoring
    /// </summary>
    public class SnafflerRuleEngine
    {
        private readonly List<SnafflerRule> _rules;
        private readonly bool _verbose;

        public SnafflerRuleEngine(bool verbose = false)
        {
            _verbose = verbose;
            _rules = new List<SnafflerRule>();
            InitializeDefaultRules();
        }

        private void InitializeDefaultRules()
        {
            // BLACK - Critical sensitivity files
            AddRule("Passwords_XML", "Password files in XML format", MatchScope.FileName, 
                new[] { @".*password.*\.xml", @".*credential.*\.xml", @".*creds.*\.xml" }, 
                SensitivityLevel.Black, 100);

            AddRule("Unattend_Files", "Windows unattended installation files", MatchScope.FileName,
                new[] { @"unattend\.xml", @"sysprep\.xml", @"autounattend\.xml", @"unattended\.xml" },
                SensitivityLevel.Black, 100);

            AddRule("Private_Keys", "Private key files", MatchScope.FileExtension,
                new[] { @"\.pem$", @"\.key$", @"\.pfx$", @"\.p12$", @"\.pkcs12$", @"\.ppk$" },
                SensitivityLevel.Black, 100);

            AddRule("KeePass_Database", "KeePass password database", MatchScope.FileExtension,
                new[] { @"\.kdbx$", @"\.kdb$" },
                SensitivityLevel.Black, 100);

            // RED - High sensitivity
            AddRule("Config_Files_Sensitive", "Configuration files with potential secrets", MatchScope.FileName,
                new[] { @"web\.config$", @"app\.config$", @"applicationhost\.config$", @"machine\.config$" },
                SensitivityLevel.Red, 80);

            AddRule("Script_Credentials", "Scripts potentially containing credentials", MatchScope.FileContent,
                new[] { 
                    @"password\s*=\s*['""][^'""]+['""]",
                    @"pwd\s*=\s*['""][^'""]+['""]",
                    @"-Password\s+\S+",
                    @"ConvertTo-SecureString.*-AsPlainText",
                    @"net\s+user\s+\S+\s+\S+"
                },
                SensitivityLevel.Red, 75);

            AddRule("Connection_Strings", "Database connection strings", MatchScope.FileContent,
                new[] { 
                    @"(Server|Data Source|Initial Catalog|User ID|Password)\s*=",
                    @"Provider\s*=\s*[\w\.]+;.*Password\s*=",
                    @"mongodb:\/\/[^:]+:[^@]+@",
                    @"postgres:\/\/[^:]+:[^@]+@"
                },
                SensitivityLevel.Red, 80);

            AddRule("AWS_Credentials", "AWS access keys and secrets", MatchScope.FileContent,
                new[] {
                    @"AKIA[0-9A-Z]{16}",
                    @"aws_access_key_id\s*=\s*\S+",
                    @"aws_secret_access_key\s*=\s*\S+",
                    @"AWS_SESSION_TOKEN"
                },
                SensitivityLevel.Red, 85);

            AddRule("Azure_Credentials", "Azure credentials and keys", MatchScope.FileContent,
                new[] {
                    @"DefaultEndpointsProtocol=https;AccountName=",
                    @"AccountKey=[A-Za-z0-9+/]{86}==",
                    @"clientSecret\s*[:=]\s*['""][^'""]+['""]"
                },
                SensitivityLevel.Red, 85);

            AddRule("API_Keys", "API keys and tokens", MatchScope.FileContent,
                new[] {
                    @"api[_-]?key\s*[:=]\s*['""]?[A-Za-z0-9_\-]{20,}['""]?",
                    @"apikey\s*[:=]\s*['""]?[A-Za-z0-9_\-]{20,}['""]?",
                    @"token\s*[:=]\s*['""]?[A-Za-z0-9_\-\.]{20,}['""]?",
                    @"bearer\s+[A-Za-z0-9_\-\.]+",
                    @"access_token\s*[:=]\s*['""]?[A-Za-z0-9_\-\.]+['""]?"
                },
                SensitivityLevel.Red, 75);

            AddRule("VPN_Config", "VPN configuration files", MatchScope.FileName,
                new[] { @"\.ovpn$", @"\.vpn$", @"vpnconfig", @"\.pcf$" },
                SensitivityLevel.Red, 70);

            // YELLOW - Medium sensitivity
            AddRule("PowerShell_Scripts", "PowerShell scripts", MatchScope.FileExtension,
                new[] { @"\.ps1$", @"\.psm1$", @"\.psd1$" },
                SensitivityLevel.Yellow, 50);

            AddRule("Batch_Scripts", "Batch and command scripts", MatchScope.FileExtension,
                new[] { @"\.bat$", @"\.cmd$" },
                SensitivityLevel.Yellow, 45);

            AddRule("Config_Files_General", "General configuration files", MatchScope.FileExtension,
                new[] { @"\.config$", @"\.conf$", @"\.cfg$", @"\.ini$", @"\.properties$" },
                SensitivityLevel.Yellow, 40);

            AddRule("Certificates", "Certificate files", MatchScope.FileExtension,
                new[] { @"\.cer$", @"\.crt$", @"\.der$" },
                SensitivityLevel.Yellow, 50);

            AddRule("Database_Files", "Database files", MatchScope.FileExtension,
                new[] { @"\.mdb$", @"\.mdf$", @"\.ldf$", @"\.sqlite$", @"\.db$" },
                SensitivityLevel.Yellow, 60);

            AddRule("Backup_Files", "Backup files", MatchScope.FileName,
                new[] { @"\.bak$", @"\.backup$", @"\.old$", @"\.orig$", @"~$" },
                SensitivityLevel.Yellow, 45);

            AddRule("Source_Control", "Source control files", MatchScope.FileName,
                new[] { @"\.git", @"\.svn", @"\.hg" },
                SensitivityLevel.Yellow, 55);

            AddRule("SCCM_Task_Sequences", "SCCM Task Sequence files", MatchScope.FileName,
                new[] { @".*TaskSequence.*\.xml", @"TS.*\.xml", @".*\.ts$" },
                SensitivityLevel.Yellow, 60);

            AddRule("Group_Policy", "Group Policy files", MatchScope.FileName,
                new[] { @"Registry\.pol$", @"GptTmpl\.inf$", @"GPO\.xml" },
                SensitivityLevel.Yellow, 55);

            // GREEN - Low sensitivity (informational)
            AddRule("XML_Files", "XML files", MatchScope.FileExtension,
                new[] { @"\.xml$" },
                SensitivityLevel.Green, 20);

            AddRule("Log_Files", "Log files", MatchScope.FileExtension,
                new[] { @"\.log$", @"\.txt$" },
                SensitivityLevel.Green, 15);

            AddRule("Documentation", "Documentation files", MatchScope.FileExtension,
                new[] { @"\.doc$", @"\.docx$", @"\.xls$", @"\.xlsx$", @"\.pdf$" },
                SensitivityLevel.Green, 25);

            // Content-based rules for deeper inspection
            AddRule("Base64_Passwords", "Base64 encoded passwords", MatchScope.FileContent,
                new[] { 
                    @"[Pp]assword['""]?\s*:\s*['""]?[A-Za-z0-9+/]{8,}={0,2}['""]?",
                    @"[Pp]wd['""]?\s*:\s*['""]?[A-Za-z0-9+/]{8,}={0,2}['""]?"
                },
                SensitivityLevel.Red, 70);

            AddRule("NTLM_Hashes", "NTLM password hashes", MatchScope.FileContent,
                new[] { @"[A-Fa-f0-9]{32}:[A-Fa-f0-9]{32}" },
                SensitivityLevel.Red, 85);

            AddRule("Email_Addresses", "Email addresses in files", MatchScope.FileContent,
                new[] { @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}" },
                SensitivityLevel.Green, 30);

            // Compile all patterns
            foreach (var rule in _rules)
            {
                rule.CompilePatterns();
            }
        }

        private void AddRule(string name, string description, MatchScope scope, string[] patterns, 
            SensitivityLevel severity, int baseScore, bool caseSensitive = false)
        {
            var rule = new SnafflerRule
            {
                RuleName = name,
                Description = description,
                Scope = scope,
                Type = MatchType.Regex,
                Patterns = patterns.ToList(),
                Severity = severity,
                BaseScore = baseScore,
                CaseSensitive = caseSensitive
            };
            
            _rules.Add(rule);
        }

        public List<MatchResult> AnalyseFile(string filePath)
        {
            var results = new List<MatchResult>();
            
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name;
                var extension = fileInfo.Extension.ToLower();

                // Check filename and extension rules
                foreach (var rule in _rules.Where(r => r.Scope == MatchScope.FileName || 
                                                       r.Scope == MatchScope.FileExtension ||
                                                       r.Scope == MatchScope.FilePath))
                {
                    string targetString = "";
                    switch (rule.Scope)
                    {
                        case MatchScope.FileName:
                            targetString = fileName;
                            break;
                        case MatchScope.FileExtension:
                            targetString = extension;
                            break;
                        case MatchScope.FilePath:
                            targetString = filePath;
                            break;
                    }

                    foreach (var pattern in rule.CompiledPatterns)
                    {
                        if (pattern.IsMatch(targetString))
                        {
                            results.Add(new MatchResult
                            {
                                FilePath = filePath,
                                MatchedRule = rule,
                                MatchedPattern = pattern.ToString(),
                                MatchedText = targetString,
                                Severity = rule.Severity,
                                Score = rule.BaseScore,
                                Timestamp = DateTime.Now
                            });
                            
                            if (_verbose)
                                Console.WriteLine(string.Format("[{0}] File: {1} - Rule: {2}", 
                                    GetSeveritySymbol(rule.Severity), fileName, rule.RuleName));
                        }
                    }
                }

                // Check content rules for text files
                if (IsTextFile(filePath) && fileInfo.Length < 10 * 1024 * 1024) // Max 10MB for content analysis
                {
                    AnalyseFileContent(filePath, results);
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine(string.Format("[-] Error analysing {0}: {1}", filePath, ex.Message));
            }

            return results;
        }

        private void AnalyseFileContent(string filePath, List<MatchResult> results)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                var contentRules = _rules.Where(r => r.Scope == MatchScope.FileContent).ToList();

                for (int lineNum = 0; lineNum < lines.Length; lineNum++)
                {
                    var line = lines[lineNum];
                    
                    foreach (var rule in contentRules)
                    {
                        foreach (var pattern in rule.CompiledPatterns)
                        {
                            var matches = pattern.Matches(line);
                            
                            if (matches.Count > 0)
                            {
                                foreach (Match match in matches)
                                {
                                    var context = GetContext(lines, lineNum, 2); // Get 2 lines before and after
                                    
                                    results.Add(new MatchResult
                                    {
                                        FilePath = filePath,
                                        MatchedRule = rule,
                                        MatchedPattern = pattern.ToString(),
                                        MatchedText = SanitizeMatch(match.Value),
                                        LineNumber = lineNum + 1,
                                        Severity = rule.Severity,
                                        Score = CalculateContentScore(rule.BaseScore, match.Value),
                                        Timestamp = DateTime.Now,
                                        Context = context
                                    });

                                    if (_verbose)
                                        Console.WriteLine(string.Format("[{0}] Content: {1} Line {2} - Rule: {3}", 
                                            GetSeveritySymbol(rule.Severity), Path.GetFileName(filePath), 
                                            lineNum + 1, rule.RuleName));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine(string.Format("[-] Error reading content of {0}: {1}", filePath, ex.Message));
            }
        }

        private string GetContext(string[] lines, int lineIndex, int contextLines)
        {
            var sb = new StringBuilder();
            var start = Math.Max(0, lineIndex - contextLines);
            var end = Math.Min(lines.Length - 1, lineIndex + contextLines);

            for (int i = start; i <= end; i++)
            {
                var prefix = i == lineIndex ? ">>> " : "    ";
                var lineText = lines[i];
                
                if (lineText.Length > 100)
                    lineText = lineText.Substring(0, 100) + "...";
                
                sb.AppendLine(string.Format("{0}Line {1}: {2}", prefix, i + 1, lineText));
            }

            return sb.ToString();
        }

        private int CalculateContentScore(int baseScore, string matchedValue)
        {
            // Increase score based on match characteristics
            int bonus = 0;

            // Longer matches are usually more significant
            if (matchedValue.Length > 50) bonus += 10;
            else if (matchedValue.Length > 30) bonus += 5;

            // Matches that look like actual values (not just keywords) score higher
            if (Regex.IsMatch(matchedValue, @"['""].*['""]")) bonus += 5;
            if (Regex.IsMatch(matchedValue, @"=\s*\S+")) bonus += 5;

            return Math.Min(100, baseScore + bonus);
        }

        private string SanitizeMatch(string text)
        {
            if (text.Length > 50)
                text = text.Substring(0, 50) + "...";

            // Mask potential sensitive values
            text = Regex.Replace(text, @"(password|pwd|key|token|secret)\s*[:=]\s*\S+", 
                "$1=***MASKED***", RegexOptions.IgnoreCase);

            return text;
        }

        private bool IsTextFile(string filePath)
        {
            var textExtensions = new[] { 
                ".xml", ".txt", ".config", ".ini", ".ps1", ".psm1", ".psd1",
                ".vbs", ".bat", ".cmd", ".json", ".yml", ".yaml", ".conf",
                ".cfg", ".properties", ".log", ".sql", ".cs", ".vb", ".js",
                ".py", ".rb", ".sh", ".inf"
            };
            
            var extension = Path.GetExtension(filePath).ToLower();
            return textExtensions.Contains(extension);
        }

        private string GetSeveritySymbol(SensitivityLevel level)
        {
            switch (level)
            {
                case SensitivityLevel.Black:
                    return "BLACK";
                case SensitivityLevel.Red:
                    return "RED  ";
                case SensitivityLevel.Yellow:
                    return "YELLO";
                case SensitivityLevel.Green:
                    return "GREEN";
                default:
                    return "INFO ";
            }
        }

        public SensitivityReport GenerateReport(List<MatchResult> allResults)
        {
            var report = new SensitivityReport();
            
            // Group by severity
            var groupedBySeverity = allResults.GroupBy(r => r.Severity)
                .OrderByDescending(g => (int)g.Key);

            foreach (var severityGroup in groupedBySeverity)
            {
                var findings = new SeverityFindings
                {
                    Level = severityGroup.Key,
                    Count = severityGroup.Count(),
                    TotalScore = severityGroup.Sum(r => r.Score)
                };

                // Group by rule within severity
                var ruleGroups = severityGroup.GroupBy(r => r.MatchedRule.RuleName)
                    .OrderByDescending(g => g.Count());

                foreach (var ruleGroup in ruleGroups)
                {
                    var rule = ruleGroup.First().MatchedRule;
                    findings.RuleMatches.Add(new RuleMatch
                    {
                        RuleName = rule.RuleName,
                        Description = rule.Description,
                        MatchCount = ruleGroup.Count(),
                        Files = ruleGroup.Select(r => r.FilePath).Distinct().ToList(),
                        TotalScore = ruleGroup.Sum(r => r.Score)
                    });
                }

                report.Findings.Add(findings);
            }

            // Calculate overall risk score
            report.OverallRiskScore = CalculateOverallRisk(allResults);
            report.TotalFiles = allResults.Select(r => r.FilePath).Distinct().Count();
            report.TotalMatches = allResults.Count;

            return report;
        }

        private int CalculateOverallRisk(List<MatchResult> results)
        {
            if (results.Count == 0) return 0;

            double score = 0;
            
            // Weight by severity
            var blackCount = results.Count(r => r.Severity == SensitivityLevel.Black);
            var redCount = results.Count(r => r.Severity == SensitivityLevel.Red);
            var yellowCount = results.Count(r => r.Severity == SensitivityLevel.Yellow);
            var greenCount = results.Count(r => r.Severity == SensitivityLevel.Green);

            score += blackCount * 100;
            score += redCount * 50;
            score += yellowCount * 20;
            score += greenCount * 5;

            // Normalize to 0-100 scale
            return Math.Min(100, (int)(score / 10));
        }
    }

    public class SensitivityReport
    {
        public List<SeverityFindings> Findings { get; set; }
        public int OverallRiskScore { get; set; }
        public int TotalFiles { get; set; }
        public int TotalMatches { get; set; }

        public SensitivityReport()
        {
            Findings = new List<SeverityFindings>();
        }
    }

    public class SeverityFindings
    {
        public SensitivityLevel Level { get; set; }
        public int Count { get; set; }
        public int TotalScore { get; set; }
        public List<RuleMatch> RuleMatches { get; set; }

        public SeverityFindings()
        {
            RuleMatches = new List<RuleMatch>();
        }
    }

    public class RuleMatch
    {
        public string RuleName { get; set; }
        public string Description { get; set; }
        public int MatchCount { get; set; }
        public List<string> Files { get; set; }
        public int TotalScore { get; set; }

        public RuleMatch()
        {
            Files = new List<string>();
        }
    }
}