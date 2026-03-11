using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;

namespace SnafflerSCCMAddon
{
    /// <summary>
    /// Snaffler SCCM Addon - Discovers SCCM shares and runs Snaffler against indexed INI files
    /// Combines SharpCMLoot discovery logic with Snaffler analysis engine
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine(@"
 _____ _____ _____ _____    _____ _____ _____
|   __|     |     |     |  |  _  |     |     |
|__   |   --|   --|  |  |  |     |  |  | | | |
|_____|_____|_____|_____|  |__|__|____/|_|_|_|

Snaffler SCCM Addon v1.0
SCCM Discovery + Snaffler Analysis
========================================
");

            return Parser.Default.ParseArguments<Options>(args)
                .MapResult(
                    options => Run(options),
                    errors => 1);
        }

        static int Run(Options options)
        {
            try
            {
                // Step 1: Discover SCCM servers
                List<SCCMServer> servers = null;

                if (!string.IsNullOrEmpty(options.Domain))
                {
                    Console.WriteLine("[*] Phase 1: SCCM Server Discovery");
                    Console.WriteLine("=" + new string('=', 39));

                    var discovery = new SCCMShareDiscovery(
                        options.Domain,
                        options.Username,
                        options.Password,
                        options.Verbose,
                        options.LdapPort);

                    servers = discovery.DiscoverSCCMServers();

                    if (servers.Count == 0)
                    {
                        Console.WriteLine("[-] No SCCM servers discovered");

                        if (!string.IsNullOrEmpty(options.Server))
                        {
                            Console.WriteLine($"[*] Using manually specified server: {options.Server}");
                            servers = new List<SCCMServer>
                            {
                                new SCCMServer { Hostname = options.Server, Role = "Manual" }
                            };
                        }
                        else
                        {
                            return 1;
                        }
                    }

                    // Validate server accessibility
                    if (options.Validate)
                    {
                        servers = discovery.ValidateServers(servers);

                        if (servers.Count == 0)
                        {
                            Console.WriteLine("[-] No accessible SCCM servers found");
                            return 1;
                        }
                    }

                    // Save discovered servers
                    if (!string.IsNullOrEmpty(options.OutputDiscovery))
                    {
                        File.WriteAllLines(options.OutputDiscovery,
                            servers.Select(s => s.Hostname));
                        Console.WriteLine($"[+] Saved server list: {options.OutputDiscovery}");
                    }
                }
                else if (!string.IsNullOrEmpty(options.Server))
                {
                    servers = new List<SCCMServer>
                    {
                        new SCCMServer { Hostname = options.Server, Role = "Manual" }
                    };
                }
                else if (!string.IsNullOrEmpty(options.ServerListFile))
                {
                    servers = File.ReadAllLines(options.ServerListFile)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => new SCCMServer { Hostname = line.Trim(), Role = "FromList" })
                        .ToList();

                    Console.WriteLine($"[+] Loaded {servers.Count} servers from: {options.ServerListFile}");
                }
                else
                {
                    Console.WriteLine("[-] Must specify either --domain, --server, or --server-list");
                    return 1;
                }

                // Step 2: Index SCCM INI files
                Console.WriteLine("\n[*] Phase 2: SCCM Content Indexing");
                Console.WriteLine("=" + new string('=', 39));

                var allIndexedFiles = new List<INIFileEntry>();

                foreach (var server in servers)
                {
                    try
                    {
                        var indexer = new SCCMINIIndexer(options.Verbose);

                        var indexed = indexer.IndexSCCMShare(
                            server.Hostname,
                            options.Username,
                            options.Password,
                            options.Domain);

                        allIndexedFiles.AddRange(indexed);

                        if (indexed.Count > 0)
                        {
                            indexer.PrintStatistics();

                            // Export indexed files
                            if (!string.IsNullOrEmpty(options.OutputIndex))
                            {
                                var serverIndexFile = options.OutputIndex.Replace(".txt",
                                    $"_{server.Hostname}.txt");
                                indexer.ExportWithMetadata(serverIndexFile);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[-] Failed to index {server.Hostname}: {ex.Message}");
                    }
                }

                if (allIndexedFiles.Count == 0)
                {
                    Console.WriteLine("[-] No files indexed");
                    return 1;
                }

                Console.WriteLine($"\n[+] Total indexed files: {allIndexedFiles.Count}");

                // Filter by extensions if specified
                if (options.Extensions != null && options.Extensions.Any())
                {
                    var indexer = new SCCMINIIndexer(options.Verbose);
                    var filtered = allIndexedFiles.Where(e =>
                    {
                        var ext = Path.GetExtension(e.OriginalFileName)?.TrimStart('.').ToLower();
                        return options.Extensions.Contains(ext);
                    }).ToList();

                    Console.WriteLine($"[*] Filtered to {filtered.Count} files matching extensions: {string.Join(",", options.Extensions)}");
                    allIndexedFiles = filtered;
                }

                // Step 3: Run Snaffler
                if (!string.IsNullOrEmpty(options.SnafflerExe))
                {
                    Console.WriteLine("\n[*] Phase 3: Snaffler Analysis");
                    Console.WriteLine("=" + new string('=', 39));

                    var snaffler = new SnafflerIntegration(options.SnafflerExe, options.Verbose);

                    var snafflerOptions = new SnafflerRunOptions
                    {
                        IndexedFiles = allIndexedFiles,
                        OutputFile = options.OutputSnaffler ?? "sccm_snaffler_results.txt",
                        StreamOutput = options.StreamOutput,
                        VerbosityLevel = options.Verbose ? "debug" : "data",
                        MaxFileSizeBytes = options.MaxFileSize * 1024,
                        RulesFile = options.RulesFile,
                        Domain = options.Domain,
                        Username = options.Username,
                        Password = options.Password,
                        MaxThreads = options.Threads,
                        TimeoutMinutes = options.TimeoutMinutes
                    };

                    var result = snaffler.RunSnaffler(snafflerOptions);

                    // Create enriched report
                    if (result.Success && !string.IsNullOrEmpty(options.OutputReport))
                    {
                        snaffler.CreateEnrichedReport(result, allIndexedFiles, options.OutputReport);
                    }

                    return result.Success ? 0 : 1;
                }
                else
                {
                    Console.WriteLine("\n[!] Snaffler executable not specified (--snaffler-exe)");
                    Console.WriteLine("[*] Discovery and indexing complete");

                    if (!string.IsNullOrEmpty(options.OutputIndex))
                    {
                        Console.WriteLine($"[*] Use indexed files with Snaffler: snaffler.exe -f {options.OutputIndex}");
                    }

                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[-] Fatal error: {ex.Message}");
                if (options.Verbose)
                {
                    Console.WriteLine($"    Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }
    }

    /// <summary>
    /// Command-line options
    /// </summary>
    public class Options
    {
        // Discovery Options
        [Option('d', "domain", HelpText = "Domain for LDAP discovery (e.g., corp.local)")]
        public string Domain { get; set; }

        [Option('s', "server", HelpText = "Single SCCM server to target")]
        public string Server { get; set; }

        [Option("server-list", HelpText = "File containing list of SCCM servers (one per line)")]
        public string ServerListFile { get; set; }

        [Option("ldap-port", Default = 389, HelpText = "LDAP port (default: 389)")]
        public int LdapPort { get; set; }

        [Option("validate", HelpText = "Validate server accessibility before indexing")]
        public bool Validate { get; set; }

        // Authentication Options
        [Option('u', "username", HelpText = "Username for authentication")]
        public string Username { get; set; }

        [Option('p', "password", HelpText = "Password for authentication")]
        public string Password { get; set; }

        // Indexing Options
        [Option('e', "extensions", Separator = ',', HelpText = "Filter by file extensions (e.g., xml,ini,config)")]
        public IEnumerable<string> Extensions { get; set; }

        // Snaffler Options
        [Option("snaffler-exe", HelpText = "Path to Snaffler.exe")]
        public string SnafflerExe { get; set; }

        [Option('r', "rules", HelpText = "Path to Snaffler rules file")]
        public string RulesFile { get; set; }

        [Option('t', "threads", Default = 30, HelpText = "Number of threads for Snaffler (default: 30)")]
        public int Threads { get; set; }

        [Option('m', "max-size", Default = 500, HelpText = "Max file size in KB for Snaffler analysis (default: 500)")]
        public int MaxFileSize { get; set; }

        [Option("stream", HelpText = "Stream Snaffler output to console")]
        public bool StreamOutput { get; set; }

        [Option("timeout", Default = 60, HelpText = "Timeout in minutes for Snaffler execution (default: 60)")]
        public int TimeoutMinutes { get; set; }

        // Output Options
        [Option('o', "output-discovery", HelpText = "Output file for discovered servers")]
        public string OutputDiscovery { get; set; }

        [Option("output-index", HelpText = "Output file for indexed SCCM files")]
        public string OutputIndex { get; set; }

        [Option("output-snaffler", HelpText = "Output file for Snaffler results")]
        public string OutputSnaffler { get; set; }

        [Option("output-report", HelpText = "Output file for enriched report")]
        public string OutputReport { get; set; }

        // General Options
        [Option('v', "verbose", HelpText = "Enable verbose output")]
        public bool Verbose { get; set; }
    }
}
