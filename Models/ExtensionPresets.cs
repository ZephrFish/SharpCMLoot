using System;
using System.Collections.Generic;
using System.Linq;

namespace SCML.Models
{
    /// <summary>
    /// Predefined extension presets for targeted sensitive file discovery
    /// </summary>
    public static class ExtensionPresets
    {
        // Preset categories with descriptions
        public static readonly Dictionary<string, ExtensionPreset> Presets = new Dictionary<string, ExtensionPreset>(StringComparer.OrdinalIgnoreCase)
        {
            ["baseline"] = new ExtensionPreset
            {
                Name = "Baseline",
                Description = "Standard security baseline scan - configs and scripts",
                Extensions = new[] { "xml", "ini", "config", "conf", "cfg", "ps1", "psm1", "psd1", "bat", "cmd", "vbs", "js" },
                SensitivityLevel = SensitivityLevel.Yellow
            },
            
            ["credentials"] = new ExtensionPreset
            {
                Name = "Credentials",
                Description = "Focus on files likely to contain credentials",
                Extensions = new[] { "xml", "config", "conf", "ini", "json", "yml", "yaml", "properties", "env", "key", "pem", "pfx", "p12", "jks", "keystore" },
                SensitivityLevel = SensitivityLevel.Red
            },
            
            ["scripts"] = new ExtensionPreset
            {
                Name = "Scripts",
                Description = "All script files that might contain embedded secrets",
                Extensions = new[] { "ps1", "psm1", "psd1", "bat", "cmd", "sh", "bash", "vbs", "js", "py", "rb", "pl", "sql", "ahk" },
                SensitivityLevel = SensitivityLevel.Yellow
            },
            
            ["deployment"] = new ExtensionPreset
            {
                Name = "Deployment",
                Description = "Deployment and installation files",
                Extensions = new[] { "xml", "inf", "msi", "cab", "wim", "iso", "ps1", "cmd", "bat", "ini", "txt", "log" },
                SensitivityLevel = SensitivityLevel.Yellow
            },
            
            ["databases"] = new ExtensionPreset
            {
                Name = "Databases",
                Description = "Database and data storage files",
                Extensions = new[] { "mdb", "mdf", "ldf", "db", "sqlite", "sqlite3", "dbf", "accdb", "sql", "bak", "dmp" },
                SensitivityLevel = SensitivityLevel.Red
            },
            
            ["certificates"] = new ExtensionPreset
            {
                Name = "Certificates",
                Description = "Certificate and key files",
                Extensions = new[] { "cer", "crt", "cert", "pem", "key", "pfx", "p12", "p7b", "p7c", "der", "csr", "jks", "keystore", "pub" },
                SensitivityLevel = SensitivityLevel.Red
            },
            
            ["backups"] = new ExtensionPreset
            {
                Name = "Backups",
                Description = "Backup files that might contain sensitive data",
                Extensions = new[] { "bak", "backup", "old", "orig", "save", "saved", "tmp", "temp", "cache", "~" },
                SensitivityLevel = SensitivityLevel.Yellow
            },
            
            ["logs"] = new ExtensionPreset
            {
                Name = "Logs",
                Description = "Log files that might expose sensitive information",
                Extensions = new[] { "log", "txt", "out", "err", "trace", "debug" },
                SensitivityLevel = SensitivityLevel.Green
            },
            
            ["sccm"] = new ExtensionPreset
            {
                Name = "SCCM",
                Description = "SCCM-specific configuration and package files",
                Extensions = new[] { "xml", "mof", "ps1", "vbs", "cab", "wim", "ini", "inf", "dat", "sms", "box" },
                SensitivityLevel = SensitivityLevel.Yellow
            },
            
            ["gpo"] = new ExtensionPreset
            {
                Name = "GPO",
                Description = "Group Policy related files",
                Extensions = new[] { "xml", "pol", "inf", "ini", "adm", "admx", "adml" },
                SensitivityLevel = SensitivityLevel.Yellow
            },
            
            ["web"] = new ExtensionPreset
            {
                Name = "Web",
                Description = "Web configuration and code files",
                Extensions = new[] { "config", "json", "xml", "js", "php", "asp", "aspx", "jsp", "env", "htaccess", "htpasswd" },
                SensitivityLevel = SensitivityLevel.Red
            },
            
            ["documentation"] = new ExtensionPreset
            {
                Name = "Documentation",
                Description = "Documentation that might contain sensitive info",
                Extensions = new[] { "doc", "docx", "xls", "xlsx", "pdf", "txt", "rtf", "odt", "csv", "md" },
                SensitivityLevel = SensitivityLevel.Green
            },
            
            ["critical"] = new ExtensionPreset
            {
                Name = "Critical",
                Description = "Critical files - unattend, sysprep, passwords",
                Extensions = new[] { "xml", "inf", "cfg", "conf", "config", "ini", "txt", "key", "pem", "pfx", "p12", "pwd", "pass" },
                SensitivityLevel = SensitivityLevel.Black,
                CustomPatterns = new[] { "*unattend*", "*sysprep*", "*password*", "*credential*", "*secret*" }
            },
            
            ["all-sensitive"] = new ExtensionPreset
            {
                Name = "AllSensitive",
                Description = "Comprehensive sensitive file scan",
                // Combined extensions from baseline, credentials, certificates, databases
                Extensions = new[] { 
                    // baseline
                    "xml", "ini", "config", "conf", "cfg", "ps1", "psm1", "psd1", "bat", "cmd", "vbs", "js",
                    // credentials  
                    "json", "yml", "yaml", "properties", "env", "key", "pem", "pfx", "p12", "jks", "keystore",
                    // certificates
                    "cer", "crt", "der", "p7b", "p7c", "spc", "p8",
                    // databases
                    "sql", "db", "sqlite", "sqlite3", "mdf", "ldf", "bak", "accdb", "mdb", "dbf", "sdf"
                },
                SensitivityLevel = SensitivityLevel.Red
            },
            
            ["quick"] = new ExtensionPreset
            {
                Name = "Quick",
                Description = "Quick scan for most common sensitive files",
                Extensions = new[] { "xml", "config", "ini", "ps1", "txt" },
                SensitivityLevel = SensitivityLevel.Yellow
            }
        };

        /// <summary>
        /// Get extensions for a specific preset
        /// </summary>
        public static string[] GetPresetExtensions(string presetName)
        {
            if (Presets.TryGetValue(presetName, out var preset))
            {
                return preset.Extensions;
            }
            
            // If not a preset, treat as custom comma-separated list
            return presetName.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Combine extensions from multiple presets
        /// </summary>
        private static string[] CombineExtensions(params string[] presetNames)
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var presetName in presetNames)
            {
                if (Presets.TryGetValue(presetName, out var preset))
                {
                    foreach (var ext in preset.Extensions)
                    {
                        extensions.Add(ext);
                    }
                }
            }
            
            return extensions.ToArray();
        }

        /// <summary>
        /// Get all available preset names
        /// </summary>
        public static string[] GetAvailablePresets()
        {
            return Presets.Keys.OrderBy(k => k).ToArray();
        }

        /// <summary>
        /// Display available presets with descriptions
        /// </summary>
        public static void DisplayPresets()
        {
            Console.WriteLine("\n=== Available Extension Presets ===\n");
            
            foreach (var preset in Presets.OrderBy(p => p.Key))
            {
                Console.WriteLine(string.Format("  --preset {0,-15} : {1}", 
                    preset.Key, preset.Value.Description));
                
                if (preset.Value.Extensions.Length <= 10)
                {
                    Console.WriteLine(string.Format("    Extensions: {0}", 
                        string.Join(", ", preset.Value.Extensions)));
                }
                else
                {
                    Console.WriteLine(string.Format("    Extensions ({0}): {1} ...", 
                        preset.Value.Extensions.Length,
                        string.Join(", ", preset.Value.Extensions.Take(8))));
                }
                
                Console.WriteLine(string.Format("    Sensitivity: {0}\n", preset.Value.SensitivityLevel));
            }
            
            Console.WriteLine("Usage Examples:");
            Console.WriteLine("  SCML.exe --host SCCM01 --outfile inv.txt --preset baseline");
            Console.WriteLine("  SCML.exe --host SCCM01 --outfile inv.txt --preset credentials");
            Console.WriteLine("  SCML.exe --host SCCM01 --outfile inv.txt --preset critical --snaffler-scan");
            Console.WriteLine();
        }

        /// <summary>
        /// Get recommended presets based on scan objective
        /// </summary>
        public static Dictionary<string, string[]> GetRecommendedPresets()
        {
            return new Dictionary<string, string[]>
            {
                ["Security Audit"] = new[] { "critical", "credentials", "certificates" },
                ["Compliance Check"] = new[] { "baseline", "gpo", "logs" },
                ["Quick Assessment"] = new[] { "quick", "scripts" },
                ["Full Analysis"] = new[] { "all-sensitive", "documentation" },
                ["SCCM Review"] = new[] { "sccm", "deployment", "scripts" },
                ["Incident Response"] = new[] { "critical", "backups", "logs" }
            };
        }
    }

    public class ExtensionPreset
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] Extensions { get; set; }
        public SensitivityLevel SensitivityLevel { get; set; }
        public string[] CustomPatterns { get; set; }
        
        public ExtensionPreset()
        {
            Extensions = new string[0];
            CustomPatterns = new string[0];
        }
    }
}