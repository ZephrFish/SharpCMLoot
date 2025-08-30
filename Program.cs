using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using CommandLine;
using SCML.Services;
using SCML.Models;

namespace SCML
{
    class Program
    {
        static int Main(string[] args)
        {
            // Set up comprehensive error handling to prevent crashes
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                try
                {
                    Console.WriteLine(string.Format("[-] CRITICAL ERROR: {0}", e.ExceptionObject?.ToString() ?? "Unknown error"));
                    if (e.IsTerminating)
                    {
                        Console.WriteLine("[-] Application terminating due to unhandled exception");
                    }
                }
                catch
                {
                    // Ignore errors in error handler to prevent infinite loops
                }
                finally
                {
                    Environment.Exit(2);  // Safe exit with error code
                }
            };

            // Check .NET Framework version
            CheckDependencies();
            
            // Display banner
            Console.WriteLine(@"
 _____ _____ _____ __    
|   __|     |     |  |   
|__   |   --| | | |  |__ 
|_____|_____|_|_|_|_____|
                         
SCML v3.0 - SCCM Content Library Tool
======================================
Snaffler Analysis | Extension Presets | Full Reporting
");

            return Parser.Default.ParseArguments<CommandLineOptions>(args)
                .MapResult(
                    options => RunWithOptions(options),
                    errors => HandleParseErrors(errors));
        }

        static void CheckDependencies()
        {
            try
            {
                // Check .NET Framework version
                var frameworkVersion = Environment.Version;
                if (frameworkVersion.Major < 4)
                {
                    Console.WriteLine("[!] Warning: .NET Framework 4.8 or higher is recommended");
                    Console.WriteLine("    Current version: " + frameworkVersion);
                }

                // Skip assembly checks if running as merged/standalone executable
                var currentAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                var assemblyName = currentAssembly.GetName().Name;
                
                // If running as standalone (merged) executable, dependencies are embedded
                if (assemblyName.Contains("Standalone") || currentAssembly.GetManifestResourceNames().Length > 0)
                {
                    // Dependencies are embedded in the standalone executable
                    return;
                }

                // Check if DLL files exist in the current directory
                var currentDir = System.IO.Path.GetDirectoryName(currentAssembly.Location);
                var requiredDlls = new[]
                {
                    "CommandLine.dll",
                    "SMBLibrary.dll", 
                    "Newtonsoft.Json.dll"
                };

                bool allDllsPresent = true;
                foreach (var dll in requiredDlls)
                {
                    var dllPath = System.IO.Path.Combine(currentDir, dll);
                    if (!System.IO.File.Exists(dllPath))
                    {
                        allDllsPresent = false;
                        break;
                    }
                }

                // If all DLLs are present in the directory, skip the warnings
                if (allDllsPresent)
                {
                    return;
                }

                // Check for required assemblies only if DLLs are not in directory
                var requiredAssemblies = new Dictionary<string, string>
                { 
                    { "SMBLibrary", "SMBLibrary.dll" },
                    { "CommandLineParser", "CommandLine.dll" },
                    { "Newtonsoft.Json", "Newtonsoft.Json.dll" },
                    { "System.DirectoryServices", null },
                    { "System.DirectoryServices.Protocols", null }
                };

                foreach (var assembly in requiredAssemblies)
                {
                    try
                    {
                        System.Reflection.Assembly.Load(assembly.Key);
                    }
                    catch
                    {
                        // Check if the DLL exists in the current directory before warning
                        if (assembly.Value != null)
                        {
                            var dllPath = System.IO.Path.Combine(currentDir, assembly.Value);
                            if (!System.IO.File.Exists(dllPath))
                            {
                                Console.WriteLine(string.Format("[!] Warning: Required assembly '{0}' may not be available", assembly.Key));
                            }
                        }
                        else
                        {
                            // System assemblies - only warn if truly not available
                            if (!assembly.Key.StartsWith("System."))
                            {
                                Console.WriteLine(string.Format("[!] Warning: Required assembly '{0}' may not be available", assembly.Key));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently ignore dependency check errors in production
                if (Environment.GetEnvironmentVariable("SCML_DEBUG") != null)
                {
                    Console.WriteLine(string.Format("[!] Dependency check warning: {0}", ex.Message));
                }
            }
        }

        static int HandleParseErrors(IEnumerable<Error> errors)
        {
            if (errors.Any(e => e is HelpRequestedError || e is VersionRequestedError))
            {
                DisplayHelp();
                return 0;
            }

            Console.WriteLine("Error parsing command line arguments. Use --help for usage information.");
            return 1;
        }

        static void DisplayHelp()
        {
            Console.WriteLine(@"
Quick Start Examples:
=====================

BASIC OPERATIONS:
-----------------
1. Find SCCM Servers:
   SCML.exe --findsccmservers --domain CORP.LOCAL

2. List Shares on Server:
   SCML.exe --host SCCM01 --list-shares

3. Create Inventory:
   SCML.exe --host SCCM01 --outfile inventory.txt

4. Download Specific Extensions:
   SCML.exe --host SCCM01 --outfile inventory.txt --download-extensions xml,ps1,config

SNAFFLER ANALYSIS:
------------------
5. Run Snaffler (Create Inventory + Analyse):
   SCML.exe --host SCCM01 --outfile inventory.txt --snaffler

6. Run Snaffler on Existing Inventory:
   SCML.exe --host SCCM01 --snaffler-inventory existing_inventory.txt

7. Snaffler with Specific Extensions:
   SCML.exe --host SCCM01 --outfile inventory.txt --snaffler --download-extensions xml,ps1

SINGLE FILE OPERATIONS:
-----------------------
8. Download Single File:
   SCML.exe --single-file \\server\SCCMContentLib$\DataLib\PKG12345\file.xml

9. Download Multiple Specific Files:
   SCML.exe --download-list files_to_download.txt --username admin --domain CORP

CREDENTIAL OPTIONS:
-------------------
10. Use Current Windows User:
    SCML.exe --host SCCM01 --outfile inventory.txt --current-user

11. Specify Credentials:
    SCML.exe --host SCCM01 --outfile inventory.txt --username admin --domain CORP --password Pass123

12. Domain Account with Snaffler:
    SCML.exe --host SCCM01 --snaffler --username sccm_reader --domain CORP --outfile inventory.txt

13. Process Multiple Targets with Credentials:
    SCML.exe --targets-file servers.txt --outfile inventory.txt --username admin --domain CORP

ADVANCED EXAMPLES:
------------------
14. Snaffler on Existing Inventory with Credentials:
    SCML.exe --host SCCM01 --snaffler-inventory old_inventory.txt --username admin --domain CORP

15. Download After Snaffler Analysis:
    SCML.exe --host SCCM01 --outfile inventory.txt --download-extensions xml,ps1 --current-user

Use --list-presets to see all available extension presets
");
        }

        static int RunWithOptions(CommandLineOptions options)
        {
            // Handle special commands first
            if (options.ListPresets)
            {
                ExtensionPresets.DisplayPresets();
                return 0;
            }

            if (options.GenerateConfig)
            {
                Configuration.GenerateExampleConfig();
                return 0;
            }

            // Initialize statistics
            var stats = new StatisticsService();
            stats.Start();

            try
            {
                // Load configuration if specified
                Configuration config = null;
                if (!string.IsNullOrEmpty(options.ConfigFile))
                {
                    config = Configuration.Load(options.ConfigFile);
                    ApplyConfiguration(options, config);
                }

                // Handle extension presets
                var extensions = DetermineExtensions(options);
                
                // Display preset information if used
                if (!string.IsNullOrEmpty(options.Preset))
                {
                    DisplayPresetInfo(options.Preset, extensions);
                }

                // Validate options
                if (!ValidateOptions(options))
                    return 1;

                // Get credentials
                var credentials = GetCredentials(options);

                // Handle list-shares request
                if (options.ListShares)
                {
                    if (string.IsNullOrEmpty(options.Host))
                    {
                        Console.WriteLine("[-] --host is required when using --list-shares");
                        return 1;
                    }
                    
                    ListAvailableShares(options.Host, credentials, options);
                    return 0;
                }

                // Process targets
                var targets = DetermineTargets(options, stats);
                
                if (targets.Count == 0)
                {
                    Console.WriteLine("[-] No targets specified. Use --host, --findsccmservers, or --targets-file");
                    return 1;
                }

                // If only discovering servers (no outfile specified), exit after discovery
                if (options.FindSccmServers && string.IsNullOrEmpty(options.OutFile))
                {
                    Console.WriteLine("\n[+] Discovery complete. Found {0} SCCM server(s).", targets.Count);
                    Console.WriteLine("[*] To process inventory, run again with --outfile parameter");
                    Console.WriteLine("    Example: SCML.exe --targets-file discovered_sccm_servers.txt --outfile inventory.txt");
                    return 0;
                }

                // Handle single file download
                if (!string.IsNullOrEmpty(options.SingleFile))
                {
                    ProcessSingleFileDownload(options.SingleFile, credentials, options, stats);
                    return 0;
                }

                // Handle download list (multiple specific files)
                if (!string.IsNullOrEmpty(options.DownloadList))
                {
                    ProcessDownloadList(options.DownloadList, credentials, options, stats);
                    return 0;
                }

                // Handle Snaffler mode (analyse files in-place without downloading)
                if (options.Snaffler || !string.IsNullOrEmpty(options.SnafflerInventory))
                {
                    string inventoryToAnalyse = null;
                    
                    // If using existing inventory file specified via --snaffler-inventory
                    if (!string.IsNullOrEmpty(options.SnafflerInventory))
                    {
                        if (!File.Exists(options.SnafflerInventory))
                        {
                            Console.WriteLine($"[-] Inventory file not found: {options.SnafflerInventory}");
                            return 1;
                        }
                        
                        inventoryToAnalyse = options.SnafflerInventory;
                        Console.WriteLine($"[*] Using existing inventory file: {inventoryToAnalyse}");
                        
                        // Still need a host for connection
                        if (targets.Count == 0)
                        {
                            Console.WriteLine("[-] --host is required when using --snaffler-inventory to connect to the SCCM server");
                            return 1;
                        }
                    }
                    // If using --snaffler flag
                    else
                    {
                        // Check if outfile exists and use it as inventory
                        if (!string.IsNullOrEmpty(options.OutFile) && File.Exists(options.OutFile))
                        {
                            inventoryToAnalyse = options.OutFile;
                            Console.WriteLine($"[*] Using existing inventory file: {inventoryToAnalyse}");
                        }
                        else if (!string.IsNullOrEmpty(options.OutFile))
                        {
                            // Create new inventory if file doesn't exist
                            Console.WriteLine($"[*] Creating new inventory file: {options.OutFile}");
                            ProcessInventory(targets, credentials, options, stats);
                            inventoryToAnalyse = options.OutFile;
                            
                            // Ensure inventory file is fully written and closed before snaffler analysis
                            Console.WriteLine("[*] Waiting for inventory file to be finalized...");
                            System.Threading.Thread.Sleep(1000);
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                        }
                        else
                        {
                            Console.WriteLine("[-] --outfile is required to specify inventory file for snaffler analysis");
                            return 1;
                        }
                    }
                    
                    // Store the inventory file path for snaffler to use
                    options.OutFile = inventoryToAnalyse;
                    
                    ProcessSnafflerAnalysis(targets[0], credentials, options, stats);
                    return 0;
                }

                // Process inventory for all targets
                ProcessInventory(targets, credentials, options, stats);

                // Download files if extensions specified
                if (extensions != null && extensions.Any() && !options.InventoryOnly)
                {
                    // Set download directory based on output file name if not explicitly set
                    if (string.IsNullOrEmpty(options.DownloadDir) && !string.IsNullOrEmpty(options.OutFile))
                    {
                        var baseFileName = Path.GetFileNameWithoutExtension(options.OutFile);
                        options.DownloadDir = baseFileName + "_out";
                    }
                    else if (string.IsNullOrEmpty(options.DownloadDir))
                    {
                        options.DownloadDir = "CMLootOut";
                    }
                    
                    Console.WriteLine(string.Format("\n[*] Downloading files with extensions: {0}", 
                        string.Join(", ", extensions.Take(10))));
                    
                    if (extensions.Count() > 10)
                        Console.WriteLine(string.Format("    ... and {0} more extensions", extensions.Count() - 10));

                    DownloadFiles(targets[0], credentials, options, extensions, stats);

                    // Run Snaffler analysis if requested (only for downloaded files, not in-place)
                    if (!string.IsNullOrEmpty(options.Preset))
                    {
                        RunSnafflerAnalysis(options, stats);
                    }
                }

                // Generate reports
                GenerateReports(options, stats);

                // Display summary
                stats.Stop();
                if (!options.NoSummary)
                {
                    stats.DisplaySummary();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("\n[-] Fatal error: {0}", ex.Message));
                stats.AddError(ex.Message);
                
                // Check if this is an authentication-related error to provide better guidance
                if (IsAuthenticationError(ex))
                {
                    Console.WriteLine("\n[!] Authentication Error Detected:");
                    Console.WriteLine("    - Verify credentials are correct");
                    Console.WriteLine("    - Check if account is locked out");
                    Console.WriteLine("    - Try using --current-user if domain-joined");
                    Console.WriteLine("    - Ensure account has permissions to access SCCM shares");
                    Console.WriteLine("\n[!] To prevent account lockout, authentication attempts are limited.");
                }
                
                if (options != null && options.Verbose)
                {
                    Console.WriteLine("\nStack trace:");
                    Console.WriteLine(ex.StackTrace);
                }

                try
                {
                    stats.Stop();
                    stats.DisplaySummary();
                }
                catch
                {
                    // Ignore errors in cleanup to prevent masking original error
                }
                
                return 1;
            }
            finally
            {
                // Ensure resources are always cleaned up
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        static bool IsAuthenticationError(Exception ex)
        {
            var authErrors = new[]
            {
                "login failed",
                "authentication",
                "STATUS_LOGON_FAILURE",
                "STATUS_ACCOUNT_LOCKED_OUT",
                "STATUS_ACCOUNT_DISABLED",
                "STATUS_PASSWORD_EXPIRED",
                "STATUS_WRONG_PASSWORD",
                "bad username or password",
                "invalid credentials",
                "access denied"
            };

            var errorMessage = ex.Message.ToLower();
            return authErrors.Any(error => errorMessage.Contains(error.ToLower()));
        }

        static IEnumerable<string> DetermineExtensions(CommandLineOptions options)
        {
            // Priority: download-extensions > preset > config > default
            if (options.DownloadExtensions != null && options.DownloadExtensions.Any())
            {
                return options.DownloadExtensions;
            }

            if (!string.IsNullOrEmpty(options.Preset))
            {
                return ExtensionPresets.GetPresetExtensions(options.Preset);
            }

            // Default baseline if nothing specified
            return ExtensionPresets.GetPresetExtensions("baseline");
        }

        static void DisplayPresetInfo(string presetName, IEnumerable<string> extensions)
        {
            if (ExtensionPresets.Presets.TryGetValue(presetName, out var preset))
            {
                Console.WriteLine(string.Format("\n[*] Using preset: {0}", preset.Name));
                Console.WriteLine(string.Format("    Description: {0}", preset.Description));
                Console.WriteLine(string.Format("    Sensitivity: {0}", preset.SensitivityLevel));
                Console.WriteLine(string.Format("    Extensions ({0}): {1}", 
                    extensions.Count(), 
                    string.Join(", ", extensions.Take(10))));
                
                if (extensions.Count() > 10)
                    Console.WriteLine(string.Format("    ... and {0} more", extensions.Count() - 10));
            }
        }

        static List<string> DetermineTargets(CommandLineOptions options, StatisticsService stats)
        {
            var targets = new List<string>();

            // Find SCCM servers if requested
            if (options.FindSccmServers)
            {
                Console.WriteLine("[*] Discovering SCCM servers via LDAP...");
                var credentials = GetCredentials(options);
                var ldapService = new LdapService();
                
                targets = NetworkRetryService.ExecuteWithRetry(() =>
                    ldapService.FindSCCMServers(
                        options.Domain ?? Environment.UserDomainName,
                        credentials.Username,
                        credentials.Password,
                        options.LdapPort ?? 389),
                    "LDAP server discovery",
                    options.Verbose);

                if (targets.Count > 0)
                {
                    var discoveryFile = "discovered_sccm_servers.txt";
                    File.WriteAllLines(discoveryFile, targets.ToArray());
                    Console.WriteLine(string.Format("[+] Found {0} SCCM servers. (Written to {1})", 
                        targets.Count, discoveryFile));
                    stats.IncrementCounter("ServersDiscovered", targets.Count);
                }
            }
            else if (!string.IsNullOrEmpty(options.TargetsFile))
            {
                if (File.Exists(options.TargetsFile))
                {
                    targets = File.ReadAllLines(options.TargetsFile)
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .ToList();
                }
            }
            else if (!string.IsNullOrEmpty(options.Host))
            {
                targets.Add(options.Host);
            }

            stats.IncrementCounter("TotalServers", targets.Count);
            return targets;
        }

        static void ProcessInventory(List<string> targets, Credentials credentials, 
            CommandLineOptions options, StatisticsService stats)
        {
            Console.WriteLine(string.Format("\n[*] Processing {0} target(s) for inventory", targets.Count));
            
            bool firstTarget = true;
            int successfulTargets = 0;

            foreach (var target in targets)
            {
                Console.WriteLine(string.Format("\n[+] Using target {0}", target));
                
                try
                {
                    bool shouldAppend = !firstTarget || options.Append;
                    
                    if (shouldAppend && File.Exists(options.OutFile))
                    {
                        Console.WriteLine(string.Format("[+] {0} exists. Appending to it.", options.OutFile));
                    }

                    ProcessSingleTarget(target, credentials, options, shouldAppend, stats);
                    successfulTargets++;
                    stats.IncrementCounter("ServersProcessed");
                    stats.IncrementCounter("ServersSuccessful");
                    firstTarget = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[-] Failed to process {0}: {1}", target, ex.Message));
                    stats.AddError(string.Format("Target {0}: {1}", target, ex.Message));
                    stats.IncrementCounter("ServersFailed");
                }
            }

            Console.WriteLine(string.Format("\n[+] Successfully processed {0}/{1} targets", 
                successfulTargets, targets.Count));
        }

        static void ProcessSingleTarget(string target, Credentials credentials, 
            CommandLineOptions options, bool append, StatisticsService stats)
        {
            var smbService = new SmbService(options.Verbose);
            bool connectionSucceeded = false;
            int authRetryCount = 0;
            const int maxAuthRetries = 1;
            
            while (!connectionSucceeded && authRetryCount <= maxAuthRetries)
            {
                try
                {
                    // Connect with retry logic
                    NetworkRetryService.ExecuteWithRetry(() =>
                    {
                        if (options.CurrentUser)
                        {
                            smbService.ConnectWithCurrentUser(target, 445);
                        }
                        else
                        {
                            smbService.Connect(target, credentials.Username, credentials.Password, 
                                credentials.Domain, 445);
                        }
                    }, string.Format("SMB connection to {0}", target), options.Verbose);

                    connectionSucceeded = true;
                    Console.WriteLine(string.Format("[+] Connected to {0}", target));
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("STATUS_ACCESS_DENIED") && authRetryCount == 0 && options.CurrentUser)
                    {
                        authRetryCount++;
                        Console.WriteLine("\n[!] Current user authentication failed for {0}", target);
                        Console.WriteLine("[!] Prompting for alternative credentials...");
                        
                        // Prompt for credentials
                        Console.Write("  Username: ");
                        credentials.Username = Console.ReadLine();
                        
                        Console.Write("  Domain (or leave blank for local): ");
                        credentials.Domain = Console.ReadLine();
                        if (string.IsNullOrEmpty(credentials.Domain))
                            credentials.Domain = ".";
                        
                        Console.Write("  Password: ");
                        credentials.Password = ReadPassword();
                        Console.WriteLine(); // New line after password
                        
                        // Switch to explicit credentials for retry
                        options.CurrentUser = false;
                        
                        // Clean up and retry
                        smbService.Disconnect();
                        smbService = new SmbService(options.Verbose);
                        continue;
                    }
                    throw;
                }
            }
            
            if (!connectionSucceeded)
            {
                throw new Exception(string.Format("Failed to connect to {0} after authentication retries", target));
            }
            
            try
            {
                // Pass debug flag if specified
                var inventoryService = new InventoryService(smbService, options.Debug || options.Verbose);
                
                try
                {
                    inventoryService.CreateInventory(options.OutFile, append, target);
                }
                catch (Exception ex)
                {
                    // If SCCMContentLib$ fails, provide helpful information
                    if (ex.Message.Contains("SCCMContentLib"))
                    {
                        Console.WriteLine("\n[!] Unable to access SCCMContentLib$ share");
                        Console.WriteLine("    Troubleshooting steps:");
                        Console.WriteLine("    1. Verify SCCM is installed on {0}", target);
                        Console.WriteLine("    2. Check if the share exists: net view \\\\{0}", target);
                        Console.WriteLine("    3. Ensure account has permissions to the share");
                        Console.WriteLine("    4. Try connecting with domain admin credentials");
                        Console.WriteLine("\n    Alternative: Try listing available shares:");
                        Console.WriteLine("    SCML.exe --host {0} --list-shares", target);
                    }
                    throw;
                }
                
                // Update statistics
                var fileCount = File.ReadAllLines(options.OutFile).Length;
                stats.IncrementCounter("FilesInventoried", fileCount);
                
                // Sort and deduplicate
                inventoryService.SortAndUniqFile(options.OutFile);
                Console.WriteLine(string.Format("[+] {0} created, sorted and deduplicated", options.OutFile));
                
                // Force cleanup to ensure file handles are released
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Small delay to ensure OS has fully released the file
                System.Threading.Thread.Sleep(500);
            }
            finally
            {
                smbService.Disconnect();
            }
        }

        static void DownloadFiles(string target, Credentials credentials, CommandLineOptions options,
            IEnumerable<string> extensions, StatisticsService stats)
        {
            // Check if parallel downloads are requested
            if (options.ParallelDownloads.HasValue && options.ParallelDownloads.Value > 1)
            {
                Console.WriteLine(string.Format("[*] Using parallel downloads with {0} connections", 
                    options.ParallelDownloads.Value));
                    
                var parallelService = new ParallelDownloadService(
                    target, 
                    credentials.Username, 
                    credentials.Password,
                    credentials.Domain, 
                    options.CurrentUser,
                    options.Verbose, 
                    options.PreserveFilenames,
                    options.ParallelDownloads.Value);
                    
                parallelService.DownloadFilesParallel(
                    options.OutFile, 
                    extensions, 
                    options.DownloadDir);  // Already set above
            }
            else
            {
                // Use standard single-connection download
                var smbService = new SmbService(options.Verbose);
                
                try
                {
                    // Connect with retry
                    NetworkRetryService.ExecuteWithRetry(() =>
                    {
                        if (options.CurrentUser)
                        {
                            smbService.ConnectWithCurrentUser(target, 445);
                        }
                        else
                        {
                            smbService.Connect(target, credentials.Username, credentials.Password, 
                                credentials.Domain, 445);
                        }
                    }, string.Format("SMB connection for downloads to {0}", target), options.Verbose);

                    // Download files
                    var downloadService = new DownloadService(smbService, options.Verbose, 
                        options.PreserveFilenames);
                        
                    // Convert extensions for proper handling
                    var extensionList = extensions.ToList();
                    downloadService.DownloadFiles(options.OutFile, extensionList, 
                        options.DownloadDir);  // Already set above
                }
                finally
                {
                    smbService.Disconnect();
                }
            }
        }

        static void RunSnafflerAnalysis(CommandLineOptions options, StatisticsService stats)
        {
            Console.WriteLine("\n[*] Running Snaffler-style sensitivity analysis...");
            
            // Ensure download directory is set
            if (string.IsNullOrEmpty(options.DownloadDir))
            {
                if (!string.IsNullOrEmpty(options.OutFile))
                {
                    var baseFileName = Path.GetFileNameWithoutExtension(options.OutFile);
                    options.DownloadDir = baseFileName + "_out";
                }
                else
                {
                    options.DownloadDir = "CMLootOut";
                }
            }
            
            var analysisService = new EnhancedFileAnalysisService(
                options.DownloadDir, 
                options.Verbose,
                options.OutFile);  // Pass the output file for consistent naming
                
            analysisService.AnalyseDownloadedFiles();
            
            // Update stats based on analysis results
            stats.IncrementCounter("FilesAnalysed", 
                stats.GetCounter("FilesDownloaded"));
        }

        static void GenerateReports(CommandLineOptions options, StatisticsService stats)
        {
            // Ensure download directory is set
            if (string.IsNullOrEmpty(options.DownloadDir))
            {
                if (!string.IsNullOrEmpty(options.OutFile))
                {
                    var baseFileName = Path.GetFileNameWithoutExtension(options.OutFile);
                    options.DownloadDir = baseFileName + "_out";
                }
                else
                {
                    options.DownloadDir = "CMLootOut";
                }
            }
            
            var outputDir = options.DownloadDir;
            
            // Always save execution summary
            stats.SaveReport(outputDir);
            
            // Generate HTML report if requested
            if (options.HtmlReport)
            {
                stats.GenerateHtmlReport(outputDir);
            }
        }

        static Credentials GetCredentials(CommandLineOptions options)
        {
            var creds = new Credentials();

            // Use current user if explicitly requested OR if no username/password provided
            if (options.CurrentUser || 
                (string.IsNullOrEmpty(options.Username) && string.IsNullOrEmpty(options.Password)))
            {
                creds.Username = Environment.UserName;
                creds.Domain = Environment.UserDomainName;
                Console.WriteLine(string.Format("[+] Using current Windows user: {0}\\{1}", 
                    creds.Domain, creds.Username));
                // For LDAP and SMB, we'll use integrated auth (no password needed)
                options.CurrentUser = true; // Ensure this flag is set
            }
            else
            {
                creds.Domain = options.Domain ?? Environment.UserDomainName;
                creds.Username = options.Username ?? Environment.UserName;
                creds.Password = options.Password;

                if (string.IsNullOrEmpty(creds.Password))
                {
                    Console.Write(string.Format("Password for {0}\\{1}: ", 
                        creds.Domain, creds.Username));
                    creds.Password = ReadPassword();
                }

                // Handle computer accounts
                if (creds.Username.EndsWith("$"))
                {
                    Console.WriteLine(string.Format("[+] Using computer account: {0}\\{1}", 
                        creds.Domain, creds.Username));
                }
            }

            return creds;
        }

        static bool ValidateOptions(CommandLineOptions options)
        {
            if (options.FindSccmServers && string.IsNullOrEmpty(options.Domain))
            {
                Console.WriteLine("[-] Domain is required when using --findsccmservers");
                return false;
            }

            // Output file is only required for actual inventory operations
            // Not required for: --list-shares, --findsccmservers (discovery only), --list-presets, --generate-config
            bool requiresOutfile = !options.ListShares && 
                                  !options.ListPresets && 
                                  !options.GenerateConfig &&
                                  (!options.FindSccmServers || !string.IsNullOrEmpty(options.Host) || !string.IsNullOrEmpty(options.TargetsFile));
            
            if (requiresOutfile && string.IsNullOrEmpty(options.OutFile))
            {
                Console.WriteLine("[-] Output file (--outfile) is required for inventory operations");
                Console.WriteLine("    Not required for: --list-shares, --findsccmservers (discovery only), --list-presets");
                return false;
            }

            return true;
        }

        static void ApplyConfiguration(CommandLineOptions options, Configuration config)
        {
            // Apply config values if not overridden by command line
            if (string.IsNullOrEmpty(options.Domain))
                options.Domain = config.DefaultDomain;
                
            if (string.IsNullOrEmpty(options.Username))
                options.Username = config.DefaultUsername;
                
            if (string.IsNullOrEmpty(options.DownloadDir))
                options.DownloadDir = config.DefaultOutputDirectory;
                
            if (!options.DownloadExtensions?.Any() ?? true)
                options.DownloadExtensions = config.DefaultExtensions;
        }

        static void ListAvailableShares(string host, Credentials credentials, CommandLineOptions options)
        {
            Console.WriteLine(string.Format("\n[*] Listing shares on {0}...", host));
            
            var smbService = new SmbService(options.Debug || options.Verbose);
            bool connectionSucceeded = false;
            int retryCount = 0;
            const int maxRetries = 2;
            
            while (!connectionSucceeded && retryCount <= maxRetries)
            {
                try
                {
                    // Connect to server
                    if (options.CurrentUser)
                    {
                        try
                        {
                            smbService.ConnectWithCurrentUser(host, 445);
                            connectionSucceeded = true;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("STATUS_ACCESS_DENIED") && retryCount == 0)
                            {
                                Console.WriteLine("\n[!] Current user authentication failed. Please provide credentials:");
                                
                                // Prompt for credentials
                                Console.Write("  Username: ");
                                credentials.Username = Console.ReadLine();
                                
                                Console.Write("  Domain (or leave blank for local): ");
                                credentials.Domain = Console.ReadLine();
                                if (string.IsNullOrEmpty(credentials.Domain))
                                    credentials.Domain = ".";
                                
                                Console.Write("  Password: ");
                                credentials.Password = ReadPassword();
                                Console.WriteLine(); // New line after password
                                
                                // Switch to explicit credentials for retry
                                options.CurrentUser = false;
                                retryCount++;
                                
                                // Clean up and create new service for retry
                                smbService.Disconnect();
                                smbService = new SmbService(options.Debug || options.Verbose);
                                continue;
                            }
                            throw;
                        }
                    }
                    
                    if (!options.CurrentUser)
                    {
                        smbService.Connect(host, credentials.Username, credentials.Password, 
                            credentials.Domain, 445);
                        connectionSucceeded = true;
                    }
                }
                catch (Exception ex)
                {
                    if (retryCount < maxRetries)
                    {
                        retryCount++;
                        Console.WriteLine(string.Format("[!] Connection attempt {0} failed: {1}", retryCount, ex.Message));
                        Console.WriteLine(string.Format("[*] Retrying... (attempt {0}/{1})", retryCount + 1, maxRetries + 1));
                        Thread.Sleep(2000); // Wait before retry
                        
                        // Clean up previous connection attempt
                        smbService.Disconnect();
                        smbService = new SmbService(options.Debug || options.Verbose);
                    }
                    else
                    {
                        Console.WriteLine(string.Format("[-] Error listing shares: {0}", ex.Message));
                        
                        // Write results to file even on failure if requested
                        if (!string.IsNullOrEmpty(options.OutFile))
                        {
                            var errorContent = string.Format("Error connecting to {0}: {1}\n", host, ex.Message);
                            File.AppendAllText(options.OutFile, errorContent);
                        }
                        smbService.Disconnect();
                        return;
                    }
                }
            }
            
            if (!connectionSucceeded)
            {
                Console.WriteLine("[-] Failed to connect after all retry attempts");
                smbService.Disconnect();
                return;
            }
            
            try
            {
                Console.WriteLine(string.Format("[+] Connected to {0}", host));
                
                // List shares
                var shares = smbService.ListShares();
                
                if (shares == null || shares.Count == 0)
                {
                    Console.WriteLine("[-] No shares found or unable to enumerate shares");
                    Console.WriteLine("    The account may lack permission to enumerate shares");
                }
                else
                {
                    Console.WriteLine(string.Format("\n[+] Found {0} shares:", shares.Count));
                    foreach (var share in shares)
                    {
                        Console.WriteLine(string.Format("    - {0}", share));
                        
                        // Check for SCCM-related shares
                        if (share.Contains("SCCM") || share.Contains("ContentLib"))
                        {
                            Console.WriteLine("      ^ SCCM-related share detected!");
                        }
                    }
                    
                    // Look for SCCMContentLib$
                    if (!shares.Any(s => s.Equals("SCCMContentLib$", StringComparison.OrdinalIgnoreCase)))
                    {
                        Console.WriteLine("\n[!] SCCMContentLib$ share not found");
                        Console.WriteLine("    This server may not be an SCCM distribution point");
                    }
                }
                
                // Write results to file if requested
                if (!string.IsNullOrEmpty(options.OutFile) && shares != null && shares.Count > 0)
                {
                    var content = string.Format("Shares on {0}:\n{1}\n", host, string.Join("\n", shares.Select(s => "  - " + s)));
                    File.AppendAllText(options.OutFile, content);
                    Console.WriteLine(string.Format("\n[+] Results written to {0}", options.OutFile));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error listing shares: {0}", ex.Message));
            }
            finally
            {
                smbService.Disconnect();
            }
        }

        static void ProcessDownloadList(string downloadListFile, Credentials credentials, CommandLineOptions options, StatisticsService stats)
        {
            if (!File.Exists(downloadListFile))
            {
                Console.WriteLine($"[-] Download list file not found: {downloadListFile}");
                return;
            }

            Console.WriteLine($"[*] Processing download list: {downloadListFile}");
            
            // Read all file paths from the list
            var filePaths = File.ReadAllLines(downloadListFile)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .Distinct()
                .ToList();

            if (filePaths.Count == 0)
            {
                Console.WriteLine("[-] No file paths found in download list");
                return;
            }

            Console.WriteLine($"[+] Found {filePaths.Count} files to download");

            // Set download directory
            if (string.IsNullOrEmpty(options.DownloadDir))
            {
                options.DownloadDir = "DownloadList_Out";
            }

            if (!Directory.Exists(options.DownloadDir))
            {
                Directory.CreateDirectory(options.DownloadDir);
                Console.WriteLine($"[+] Created output directory: {options.DownloadDir}");
            }

            // Group files by server for efficient connection management
            var filesByServer = new Dictionary<string, List<(string share, string relativePath, string fullPath)>>();
            
            foreach (var filePath in filePaths)
            {
                // Parse UNC path \\server\share\path\file
                var match = Regex.Match(filePath, @"^\\\\([^\\]+)\\([^\\]+)\\(.+)$");
                if (!match.Success)
                {
                    Console.WriteLine($"[!] Invalid UNC path format, skipping: {filePath}");
                    continue;
                }

                var server = match.Groups[1].Value;
                var share = match.Groups[2].Value;
                var relativePath = match.Groups[3].Value;

                if (!filesByServer.ContainsKey(server))
                {
                    filesByServer[server] = new List<(string, string, string)>();
                }
                
                filesByServer[server].Add((share, relativePath, filePath));
            }

            // Process each server
            int totalDownloaded = 0;
            int totalFailed = 0;

            foreach (var serverGroup in filesByServer)
            {
                var server = serverGroup.Key;
                var files = serverGroup.Value;

                Console.WriteLine($"\n[*] Connecting to server: {server}");
                Console.WriteLine($"    Files to download from this server: {files.Count}");

                var smbService = new SmbService(options.Verbose);
                
                try
                {
                    // Connect to server once
                    NetworkRetryService.ExecuteWithRetry(() =>
                    {
                        if (options.CurrentUser)
                        {
                            smbService.ConnectWithCurrentUser(server, 445);
                        }
                        else
                        {
                            smbService.Connect(server, credentials.Username, credentials.Password, 
                                credentials.Domain, 445);
                        }
                    }, $"SMB connection to {server}", options.Verbose);

                    Console.WriteLine($"[+] Connected to {server}");

                    // Group files by share for this server
                    var filesByShare = files.GroupBy(f => f.share);

                    foreach (var shareGroup in filesByShare)
                    {
                        var share = shareGroup.Key;
                        
                        Console.WriteLine($"[*] Accessing share: {share}");
                        
                        if (!smbService.ConnectToShare(share))
                        {
                            Console.WriteLine($"[-] Failed to connect to share: {share}");
                            totalFailed += shareGroup.Count();
                            continue;
                        }

                        // Download each file from this share
                        foreach (var (_, relativePath, fullPath) in shareGroup)
                        {
                            try
                            {
                                var fileName = Path.GetFileName(relativePath);
                                
                                // Create subdirectories if needed to preserve structure
                                var relativeDir = Path.GetDirectoryName(relativePath);
                                var localDir = options.DownloadDir;
                                
                                if (!string.IsNullOrEmpty(relativeDir))
                                {
                                    // Option: preserve directory structure
                                    localDir = Path.Combine(options.DownloadDir, server, share, relativeDir);
                                    if (!Directory.Exists(localDir))
                                    {
                                        Directory.CreateDirectory(localDir);
                                    }
                                }
                                
                                var localPath = Path.Combine(localDir, fileName);
                                
                                Console.WriteLine($"[*] Downloading: {fileName}");
                                smbService.DownloadFile(relativePath, localPath);
                                
                                if (File.Exists(localPath))
                                {
                                    var fileInfo = new FileInfo(localPath);
                                    Console.WriteLine($"[+] Downloaded: {fileName} ({fileInfo.Length / 1024.0:F2} KB)");
                                    totalDownloaded++;
                                    stats.IncrementCounter("FilesDownloaded");
                                }
                                else
                                {
                                    Console.WriteLine($"[-] Failed to download: {fileName}");
                                    totalFailed++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[-] Error downloading {fullPath}: {ex.Message}");
                                totalFailed++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[-] Error connecting to {server}: {ex.Message}");
                    totalFailed += files.Count;
                }
                finally
                {
                    smbService.Disconnect();
                }
            }

            // Summary
            Console.WriteLine($"\n[+] Download complete:");
            Console.WriteLine($"    Successfully downloaded: {totalDownloaded} files");
            Console.WriteLine($"    Failed: {totalFailed} files");
            Console.WriteLine($"    Output directory: {options.DownloadDir}");
        }

        static void ProcessSingleFileDownload(string filePath, Credentials credentials, CommandLineOptions options, StatisticsService stats)
        {
            Console.WriteLine($"[*] Processing single file download: {filePath}");
            
            // Parse the UNC path \\server\share\path\file
            var match = Regex.Match(filePath, @"^\\\\([^\\]+)\\([^\\]+)\\(.+)$");
            if (!match.Success)
            {
                Console.WriteLine("[-] Invalid UNC path format. Expected: \\\\server\\share\\path\\file");
                return;
            }

            var server = match.Groups[1].Value;
            var share = match.Groups[2].Value;
            var relativePath = match.Groups[3].Value;
            
            Console.WriteLine($"[*] Server: {server}");
            Console.WriteLine($"[*] Share: {share}");
            Console.WriteLine($"[*] Path: {relativePath}");

            var smbService = new SmbService(options.Verbose);
            
            try
            {
                // Connect to server
                NetworkRetryService.ExecuteWithRetry(() =>
                {
                    if (options.CurrentUser)
                    {
                        smbService.ConnectWithCurrentUser(server, 445);
                    }
                    else
                    {
                        smbService.Connect(server, credentials.Username, credentials.Password, 
                            credentials.Domain, 445);
                    }
                }, $"SMB connection to {server}", options.Verbose);

                // Connect to share
                if (!smbService.ConnectToShare(share))
                {
                    Console.WriteLine($"[-] Failed to connect to share: {share}");
                    return;
                }

                // Set download directory
                if (string.IsNullOrEmpty(options.DownloadDir))
                {
                    options.DownloadDir = "CMLootOut";
                }

                if (!Directory.Exists(options.DownloadDir))
                {
                    Directory.CreateDirectory(options.DownloadDir);
                }

                // Download the file
                var fileName = Path.GetFileName(relativePath);
                var localPath = Path.Combine(options.DownloadDir, fileName);
                
                Console.WriteLine($"[*] Downloading to: {localPath}");
                smbService.DownloadFile(relativePath, localPath);
                
                if (File.Exists(localPath))
                {
                    var fileInfo = new FileInfo(localPath);
                    Console.WriteLine($"[+] Downloaded: {fileName} ({fileInfo.Length / 1024.0:F2} KB)");
                    stats.IncrementCounter("FilesDownloaded");
                }
                else
                {
                    Console.WriteLine("[-] Download failed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error downloading file: {ex.Message}");
            }
            finally
            {
                smbService.Disconnect();
            }
        }

        static void ProcessSnafflerAnalysis(string target, Credentials credentials, CommandLineOptions options, StatisticsService stats)
        {
            Console.WriteLine("[*] Running Snaffler analysis on share files (in-place, no download)...");
            
            var smbService = new SmbService(options.Verbose);
            
            try
            {
                // Connect to server
                NetworkRetryService.ExecuteWithRetry(() =>
                {
                    if (options.CurrentUser)
                    {
                        smbService.ConnectWithCurrentUser(target, 445);
                    }
                    else
                    {
                        smbService.Connect(target, credentials.Username, credentials.Password, 
                            credentials.Domain, 445);
                    }
                }, $"SMB connection to {target}", options.Verbose);

                // Connect to SCCMContentLib$ share
                if (!smbService.ConnectToShare("SCCMContentLib$"))
                {
                    Console.WriteLine("[-] Failed to connect to SCCMContentLib$ share");
                    return;
                }

                // Run in-place analysis
                var snafflerService = new InPlaceSnafflerService(smbService, options.Verbose);
                
                // Determine output file for Snaffler results - ensure it's different from input
                string snafflerOutput;
                if (options.OutFile.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    snafflerOutput = options.OutFile.Replace(".txt", "_snaffler.txt");
                }
                else
                {
                    snafflerOutput = options.OutFile + "_snaffler.txt";
                }
                
                // Ensure we never write to the same file we're reading from
                if (string.Equals(options.OutFile, snafflerOutput, StringComparison.OrdinalIgnoreCase))
                {
                    snafflerOutput = options.OutFile + "_snaffler_results.txt";
                }
                
                Console.WriteLine($"[*] Inventory file: {options.OutFile}");
                Console.WriteLine($"[*] Snaffler results will be written to: {snafflerOutput}");
                
                // Use download-extensions if specified, otherwise analyse all
                var extensions = options.DownloadExtensions;
                
                snafflerService.AnalyseShareFiles(options.OutFile, snafflerOutput, extensions);
                
                stats.IncrementCounter("FilesAnalysed", stats.GetCounter("FilesInventoried"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error during Snaffler analysis: {ex.Message}");
            }
            finally
            {
                smbService.Disconnect();
            }
        }

        static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(intercept: true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);

            Console.WriteLine();
            return password;
        }
    }
}