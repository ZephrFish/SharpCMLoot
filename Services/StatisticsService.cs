using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SCML.Services
{
    /// <summary>
    /// Service for tracking and reporting statistics throughout operations
    /// </summary>
    public class StatisticsService
    {
        private readonly Stopwatch _stopwatch;
        private readonly Dictionary<string, int> _counters;
        private readonly Dictionary<string, long> _sizes;
        private readonly List<string> _errors;
        private readonly List<string> _warnings;
        private DateTime _startTime;
        
        public StatisticsService()
        {
            _stopwatch = new Stopwatch();
            _counters = new Dictionary<string, int>();
            _sizes = new Dictionary<string, long>();
            _errors = new List<string>();
            _warnings = new List<string>();
            _startTime = DateTime.Now;
        }

        public void Start()
        {
            _startTime = DateTime.Now;
            _stopwatch.Start();
        }

        public void Stop()
        {
            _stopwatch.Stop();
        }

        public void IncrementCounter(string key, int value = 1)
        {
            if (!_counters.ContainsKey(key))
                _counters[key] = 0;
            _counters[key] += value;
        }

        public void AddSize(string key, long bytes)
        {
            if (!_sizes.ContainsKey(key))
                _sizes[key] = 0;
            _sizes[key] += bytes;
        }

        public void AddError(string error)
        {
            _errors.Add(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), error));
        }

        public void AddWarning(string warning)
        {
            _warnings.Add(string.Format("[{0}] {1}", DateTime.Now.ToString("HH:mm:ss"), warning));
        }

        public int GetCounter(string key)
        {
            return _counters.ContainsKey(key) ? _counters[key] : 0;
        }

        public string GetElapsedTime()
        {
            var elapsed = _stopwatch.Elapsed;
            return string.Format("{0:00}:{1:00}:{2:00}", 
                (int)elapsed.TotalHours, elapsed.Minutes, elapsed.Seconds);
        }

        public void DisplaySummary()
        {
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("                    EXECUTION SUMMARY");
            Console.WriteLine(new string('=', 70));
            
            // Time statistics
            Console.WriteLine(string.Format("\n  Execution Time: {0}", GetElapsedTime()));
            Console.WriteLine(string.Format("   Started: {0}", _startTime.ToString("yyyy-MM-dd HH:mm:ss")));
            Console.WriteLine(string.Format("   Completed: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

            // Server statistics
            if (_counters.ContainsKey("ServersDiscovered"))
            {
                Console.WriteLine(string.Format("\n Discovery:"));
                Console.WriteLine(string.Format("   SCCM Servers Found: {0}", GetCounter("ServersDiscovered")));
            }

            if (_counters.ContainsKey("ServersProcessed"))
            {
                Console.WriteLine(string.Format("\n Servers:"));
                Console.WriteLine(string.Format("   Total Processed: {0}/{1}", 
                    GetCounter("ServersProcessed"), GetCounter("TotalServers")));
                Console.WriteLine(string.Format("   Successful: {0}", GetCounter("ServersSuccessful")));
                Console.WriteLine(string.Format("   Failed: {0}", GetCounter("ServersFailed")));
            }

            // Inventory statistics
            if (_counters.ContainsKey("FilesInventoried"))
            {
                Console.WriteLine(string.Format("\n Inventory:"));
                Console.WriteLine(string.Format("   Files Catalogued: {0:N0}", GetCounter("FilesInventoried")));
                Console.WriteLine(string.Format("   Directories Scanned: {0:N0}", GetCounter("DirectoriesScanned")));
                Console.WriteLine(string.Format("   Duplicates Removed: {0:N0}", GetCounter("DuplicatesRemoved")));
            }

            // Download statistics
            if (_counters.ContainsKey("FilesDownloaded"))
            {
                Console.WriteLine(string.Format("\n Downloads:"));
                Console.WriteLine(string.Format("   Files Downloaded: {0:N0}", GetCounter("FilesDownloaded")));
                Console.WriteLine(string.Format("   Files Skipped (Existing): {0:N0}", GetCounter("FilesSkipped")));
                Console.WriteLine(string.Format("   Download Errors: {0:N0}", GetCounter("DownloadErrors")));
                
                if (_sizes.ContainsKey("TotalDownloaded"))
                {
                    Console.WriteLine(string.Format("   Total Size: {0}", FormatBytes(_sizes["TotalDownloaded"])));
                    
                    if (_stopwatch.Elapsed.TotalSeconds > 0)
                    {
                        var bytesPerSecond = _sizes["TotalDownloaded"] / _stopwatch.Elapsed.TotalSeconds;
                        Console.WriteLine(string.Format("   Average Speed: {0}/s", FormatBytes((long)bytesPerSecond)));
                    }
                }
            }

            // Analysis statistics
            if (_counters.ContainsKey("FilesAnalysed"))
            {
                Console.WriteLine(string.Format("\n Analysis:"));
                Console.WriteLine(string.Format("   Files Analysed: {0:N0}", GetCounter("FilesAnalysed")));
                Console.WriteLine(string.Format("   BLACK Severity: {0}", GetCounter("BlackMatches")));
                Console.WriteLine(string.Format("   RED Severity: {0}", GetCounter("RedMatches")));
                Console.WriteLine(string.Format("   YELLOW Severity: {0}", GetCounter("YellowMatches")));
                Console.WriteLine(string.Format("   GREEN Severity: {0}", GetCounter("GreenMatches")));
                Console.WriteLine(string.Format("   Total Matches: {0:N0}", GetCounter("TotalMatches")));
            }

            // Errors and warnings
            if (_errors.Count > 0 || _warnings.Count > 0)
            {
                Console.WriteLine(string.Format("\n  Issues:"));
                if (_errors.Count > 0)
                {
                    Console.WriteLine(string.Format("   Errors: {0}", _errors.Count));
                    if (_errors.Count <= 5)
                    {
                        foreach (var error in _errors)
                        {
                            Console.WriteLine(string.Format("     - {0}", error));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            Console.WriteLine(string.Format("     - {0}", _errors[i]));
                        }
                        Console.WriteLine(string.Format("     ... and {0} more errors", _errors.Count - 3));
                    }
                }
                
                if (_warnings.Count > 0)
                {
                    Console.WriteLine(string.Format("   Warnings: {0}", _warnings.Count));
                }
            }

            Console.WriteLine("\n" + new string('=', 70));
        }

        public void SaveReport(string outputPath)
        {
            try
            {
                // Ensure the directory exists
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }
                
                var reportPath = Path.Combine(outputPath, "execution_summary.txt");
                
                using (var writer = new StreamWriter(reportPath))
                {
                    writer.WriteLine("SCCM Content Explorer - Execution Summary");
                    writer.WriteLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    writer.WriteLine(new string('=', 70));
                    
                    writer.WriteLine(string.Format("\nExecution Time: {0}", GetElapsedTime()));
                    writer.WriteLine(string.Format("Started: {0}", _startTime.ToString("yyyy-MM-dd HH:mm:ss")));
                    writer.WriteLine(string.Format("Completed: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                    
                    writer.WriteLine("\n--- Statistics ---");
                    foreach (var counter in _counters.OrderBy(c => c.Key))
                    {
                        writer.WriteLine(string.Format("{0}: {1:N0}", counter.Key, counter.Value));
                    }
                    
                    if (_sizes.Count > 0)
                    {
                        writer.WriteLine("\n--- Size Information ---");
                        foreach (var size in _sizes.OrderBy(s => s.Key))
                        {
                            writer.WriteLine(string.Format("{0}: {1}", size.Key, FormatBytes(size.Value)));
                        }
                    }
                    
                    if (_errors.Count > 0)
                    {
                        writer.WriteLine("\n--- Errors ---");
                        foreach (var error in _errors)
                        {
                            writer.WriteLine(error);
                        }
                    }
                    
                    if (_warnings.Count > 0)
                    {
                        writer.WriteLine("\n--- Warnings ---");
                        foreach (var warning in _warnings)
                        {
                            writer.WriteLine(warning);
                        }
                    }
                }
                
                Console.WriteLine(string.Format("[+] Summary report saved to: {0}", reportPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error saving summary report: {0}", ex.Message));
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }

        public void GenerateHtmlReport(string outputPath, Dictionary<string, object> additionalData = null)
        {
            try
            {
                // Ensure the directory exists
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }
                
                var htmlPath = Path.Combine(outputPath, "sccm_explorer_report.html");
                
                var html = new StringBuilder();
                html.AppendLine("<!DOCTYPE html>");
                html.AppendLine("<html>");
                html.AppendLine("<head>");
                html.AppendLine("<title>SCCM Content Explorer Report</title>");
                html.AppendLine("<meta charset='UTF-8'>");
                html.AppendLine("<style>");
                html.AppendLine(@"
                    body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #f5f5f5; }
                    .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
                    h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }
                    h2 { color: #34495e; margin-top: 30px; }
                    .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 20px; margin: 20px 0; }
                    .stat-card { background: #f8f9fa; padding: 15px; border-radius: 5px; border-left: 4px solid #3498db; }
                    .stat-value { font-size: 24px; font-weight: bold; color: #2c3e50; }
                    .stat-label { color: #7f8c8d; margin-top: 5px; }
                    .severity-black { background: #2c3e50; color: white; padding: 5px 10px; border-radius: 3px; }
                    .severity-red { background: #e74c3c; color: white; padding: 5px 10px; border-radius: 3px; }
                    .severity-yellow { background: #f39c12; color: white; padding: 5px 10px; border-radius: 3px; }
                    .severity-green { background: #27ae60; color: white; padding: 5px 10px; border-radius: 3px; }
                    table { width: 100%; border-collapse: collapse; margin: 20px 0; }
                    th { background: #34495e; color: white; padding: 10px; text-align: left; }
                    td { padding: 8px; border-bottom: 1px solid #ecf0f1; }
                    tr:hover { background: #f8f9fa; }
                    .error { color: #e74c3c; }
                    .warning { color: #f39c12; }
                    .success { color: #27ae60; }
                    .timestamp { color: #95a5a6; font-size: 12px; }
                ");
                html.AppendLine("</style>");
                html.AppendLine("</head>");
                html.AppendLine("<body>");
                html.AppendLine("<div class='container'>");
                
                // Header
                html.AppendLine("<h1> SCCM Content Explorer Report</h1>");
                html.AppendLine(string.Format("<p class='timestamp'>Generated: {0} | Execution Time: {1}</p>", 
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), GetElapsedTime()));
                
                // Statistics Grid
                html.AppendLine("<div class='stats-grid'>");
                
                if (_counters.ContainsKey("ServersProcessed"))
                {
                    html.AppendLine("<div class='stat-card'>");
                    html.AppendLine(string.Format("<div class='stat-value'>{0}/{1}</div>", 
                        GetCounter("ServersProcessed"), GetCounter("TotalServers")));
                    html.AppendLine("<div class='stat-label'>Servers Processed</div>");
                    html.AppendLine("</div>");
                }
                
                if (_counters.ContainsKey("FilesInventoried"))
                {
                    html.AppendLine("<div class='stat-card'>");
                    html.AppendLine(string.Format("<div class='stat-value'>{0:N0}</div>", GetCounter("FilesInventoried")));
                    html.AppendLine("<div class='stat-label'>Files Inventoried</div>");
                    html.AppendLine("</div>");
                }
                
                if (_counters.ContainsKey("FilesDownloaded"))
                {
                    html.AppendLine("<div class='stat-card'>");
                    html.AppendLine(string.Format("<div class='stat-value'>{0:N0}</div>", GetCounter("FilesDownloaded")));
                    html.AppendLine("<div class='stat-label'>Files Downloaded</div>");
                    html.AppendLine("</div>");
                }
                
                if (_sizes.ContainsKey("TotalDownloaded"))
                {
                    html.AppendLine("<div class='stat-card'>");
                    html.AppendLine(string.Format("<div class='stat-value'>{0}</div>", FormatBytes(_sizes["TotalDownloaded"])));
                    html.AppendLine("<div class='stat-label'>Total Downloaded</div>");
                    html.AppendLine("</div>");
                }
                
                html.AppendLine("</div>");
                
                // Sensitivity Analysis Summary
                if (_counters.ContainsKey("FilesAnalysed"))
                {
                    html.AppendLine("<h2> Sensitivity Analysis</h2>");
                    html.AppendLine("<table>");
                    html.AppendLine("<tr><th>Severity</th><th>Count</th><th>Percentage</th></tr>");
                    
                    int total = GetCounter("TotalMatches");
                    if (total > 0)
                    {
                        html.AppendLine(string.Format("<tr><td><span class='severity-black'>BLACK</span></td><td>{0}</td><td>{1:F1}%</td></tr>",
                            GetCounter("BlackMatches"), (GetCounter("BlackMatches") * 100.0) / total));
                        html.AppendLine(string.Format("<tr><td><span class='severity-red'>RED</span></td><td>{0}</td><td>{1:F1}%</td></tr>",
                            GetCounter("RedMatches"), (GetCounter("RedMatches") * 100.0) / total));
                        html.AppendLine(string.Format("<tr><td><span class='severity-yellow'>YELLOW</span></td><td>{0}</td><td>{1:F1}%</td></tr>",
                            GetCounter("YellowMatches"), (GetCounter("YellowMatches") * 100.0) / total));
                        html.AppendLine(string.Format("<tr><td><span class='severity-green'>GREEN</span></td><td>{0}</td><td>{1:F1}%</td></tr>",
                            GetCounter("GreenMatches"), (GetCounter("GreenMatches") * 100.0) / total));
                    }
                    
                    html.AppendLine("</table>");
                }
                
                // Issues
                if (_errors.Count > 0 || _warnings.Count > 0)
                {
                    html.AppendLine("<h2> Issues Encountered</h2>");
                    
                    if (_errors.Count > 0)
                    {
                        html.AppendLine(string.Format("<p class='error'>Errors: {0}</p>", _errors.Count));
                    }
                    
                    if (_warnings.Count > 0)
                    {
                        html.AppendLine(string.Format("<p class='warning'>Warnings: {0}</p>", _warnings.Count));
                    }
                }
                
                html.AppendLine("</div>");
                html.AppendLine("</body>");
                html.AppendLine("</html>");
                
                File.WriteAllText(htmlPath, html.ToString());
                Console.WriteLine(string.Format("[+] HTML report generated: {0}", htmlPath));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error generating HTML report: {0}", ex.Message));
            }
        }
    }
}