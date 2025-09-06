using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SCML.Models
{
    /// <summary>
    /// Snaffler-style rule for file classification
    /// </summary>
    public class SnafflerRule
    {
        public string RuleName { get; set; }
        public string Description { get; set; }
        public MatchScope Scope { get; set; }
        public MatchType Type { get; set; }
        public List<string> Patterns { get; set; }
        public List<Regex> CompiledPatterns { get; set; }
        public SensitivityLevel Severity { get; set; }
        public int BaseScore { get; set; }
        public bool CaseSensitive { get; set; }
        public List<string> FileExtensions { get; set; }
        public long MaxFileSize { get; set; } // in bytes, 0 = no limit

        public SnafflerRule()
        {
            Patterns = new List<string>();
            CompiledPatterns = new List<Regex>();
            FileExtensions = new List<string>();
            MaxFileSize = 10 * 1024 * 1024; // Default 10MB
        }

        public void CompilePatterns()
        {
            CompiledPatterns.Clear();
            var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
            
            foreach (var pattern in Patterns)
            {
                try
                {
                    CompiledPatterns.Add(new Regex(pattern, options | RegexOptions.Compiled));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[-] Failed to compile pattern {0}: {1}", pattern, ex.Message));
                }
            }
        }
    }

    public enum MatchScope
    {
        FileName,
        FilePath,
        FileContent,
        FileExtension,
        Combined
    }

    public enum MatchType
    {
        Regex,
        Exact,
        Contains,
        StartsWith,
        EndsWith
    }

    public enum SensitivityLevel
    {
        Green = 0,   // Low sensitivity - informational
        Yellow = 1,  // Medium sensitivity - potentially interesting
        Red = 2,     // High sensitivity - likely contains sensitive data
        Black = 3    // Critical - definitely contains sensitive/critical data
    }

    public class MatchResult
    {
        public string FilePath { get; set; }
        public SnafflerRule MatchedRule { get; set; }
        public string MatchedPattern { get; set; }
        public string MatchedText { get; set; }
        public int LineNumber { get; set; }
        public SensitivityLevel Severity { get; set; }
        public int Score { get; set; }
        public DateTime Timestamp { get; set; }
        public string Context { get; set; }
    }
}