using CommandLine;
using System.Collections.Generic;

namespace SCML
{
    public class CommandLineOptions
    {
        [Option("host", HelpText = "SCCM host to connect to. Required unless using --findsccmservers.")]
        public string Host { get; set; }

        [Option("outfile", HelpText = "File to save the inventory. (Required for inventory operations, optional for --list-shares)")]
        public string OutFile { get; set; }

        [Option("findsccmservers", HelpText = "Find SCCM servers via LDAP.")]
        public bool FindSccmServers { get; set; }

        [Option("domain", HelpText = "Domain for authentication (Required for --findsccmservers).")]
        public string Domain { get; set; }

        [Option("username", HelpText = "Username for authentication.")]
        public string Username { get; set; }

        [Option("password", HelpText = "Password for authentication.")]
        public string Password { get; set; }

        [Option("ldaps", HelpText = "Use secure LDAPS.")]
        public bool UseLdaps { get; set; }

        [Option("ldapport", HelpText = "LDAP/LDAPS port (default: 389 for LDAP, 636 for LDAPS).")]
        public int? LdapPort { get; set; }

        [Option("download-extensions", Separator = ',', HelpText = "Comma-separated list of file extensions to download (e.g., xml,ps1,ini).")]
        public IEnumerable<string> DownloadExtensions { get; set; }
        
        [Option("snaffler", HelpText = "Run Snaffler analysis on files in-place without downloading them.")]
        public bool Snaffler { get; set; }
        
        [Option("snaffler-inventory", HelpText = "Run Snaffler against an existing inventory file (requires --host for connection).")]
        public string SnafflerInventory { get; set; }
        
        [Option("single-file", HelpText = "Download a single specified file path.")]
        public string SingleFile { get; set; }
        
        [Option("download-list", HelpText = "File containing list of specific files to download (one UNC path per line).")]
        public string DownloadList { get; set; }
        
        [Option("preset", HelpText = "Use predefined extension preset (baseline, credentials, critical, scripts, etc.).")]
        public string Preset { get; set; }
        
        [Option("list-presets", HelpText = "Display all available extension presets.")]
        public bool ListPresets { get; set; }

        [Option("inventory-only", HelpText = "Generate inventory only, don't download files.")]
        public bool InventoryOnly { get; set; }

        [Option("download-dir", Default = "CMLootOut", HelpText = "Directory to save downloaded files.")]
        public string DownloadDir { get; set; }

        [Option("current-user", HelpText = "Use current Windows user credentials.")]
        public bool CurrentUser { get; set; }

        [Option("targets-file", HelpText = "File containing multiple SCCM hosts to process.")]
        public string TargetsFile { get; set; }

        [Option("append", HelpText = "Append to existing inventory file instead of overwriting.")]
        public bool Append { get; set; }

        [Option("verbose", HelpText = "Enable verbose output.")]
        public bool Verbose { get; set; }
        
        [Option("parallel", HelpText = "Enable parallel downloads (1-10 connections).")]
        public int? ParallelDownloads { get; set; }
        
        [Option("preserve-filenames", HelpText = "Keep original filenames without hash prefix.")]
        public bool PreserveFilenames { get; set; }
        
        [Option("config", HelpText = "Load settings from configuration file.")]
        public string ConfigFile { get; set; }
        
        [Option("generate-config", HelpText = "Generate example configuration file.")]
        public bool GenerateConfig { get; set; }
        
        [Option("no-summary", HelpText = "Skip execution summary display.")]
        public bool NoSummary { get; set; }
        
        [Option("html-report", HelpText = "Generate HTML report after execution.")]
        public bool HtmlReport { get; set; }

        [Option("debug", HelpText = "Enable debug output for troubleshooting.")]
        public bool Debug { get; set; }

        [Option("list-shares", HelpText = "List available shares on the target server.")]
        public bool ListShares { get; set; }

        [Option("local-path", HelpText = "Use local file system path instead of SMB (for testing).")]
        public string LocalPath { get; set; }
    }
}