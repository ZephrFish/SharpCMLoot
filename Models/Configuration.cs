using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace SCML.Models
{
    /// <summary>
    /// Application configuration model
    /// </summary>
    public class Configuration
    {
        [JsonProperty("defaultDomain")]
        public string DefaultDomain { get; set; }

        [JsonProperty("defaultUsername")]
        public string DefaultUsername { get; set; }

        [JsonProperty("defaultOutputDirectory")]
        public string DefaultOutputDirectory { get; set; }

        [JsonProperty("defaultExtensions")]
        public List<string> DefaultExtensions { get; set; }

        [JsonProperty("retrySettings")]
        public RetrySettings RetrySettings { get; set; }

        [JsonProperty("performanceSettings")]
        public PerformanceSettings PerformanceSettings { get; set; }

        [JsonProperty("reportingSettings")]
        public ReportingSettings ReportingSettings { get; set; }

        public Configuration()
        {
            DefaultExtensions = new List<string>();
            RetrySettings = new RetrySettings();
            PerformanceSettings = new PerformanceSettings();
            ReportingSettings = new ReportingSettings();
        }

        /// <summary>
        /// Load configuration from file
        /// </summary>
        public static Configuration Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(string.Format("Configuration file not found: {0}", filePath));
            }

            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Configuration>(json);
        }

        /// <summary>
        /// Save configuration to file
        /// </summary>
        public void Save(string filePath)
        {
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// Generate example configuration file
        /// </summary>
        public static void GenerateExampleConfig()
        {
            var config = new Configuration
            {
                DefaultDomain = "CORP.LOCAL",
                DefaultUsername = null,
                DefaultOutputDirectory = "CMLootOut",
                DefaultExtensions = new List<string> { "xml", "ini", "config", "ps1", "txt" },
                RetrySettings = new RetrySettings
                {
                    MaxAttempts = 3,
                    BaseDelayMilliseconds = 1000,
                    MaxDelayMilliseconds = 30000,
                    UseExponentialBackoff = true
                },
                PerformanceSettings = new PerformanceSettings
                {
                    MaxParallelDownloads = 5,
                    ChunkSizeKB = 1024,
                    EnableProgressReporting = true,
                    ConnectionTimeoutSeconds = 30
                },
                ReportingSettings = new ReportingSettings
                {
                    GenerateHtmlReport = true,
                    GenerateCsvExport = true,
                    GenerateCleanupScripts = true,
                    VerboseLogging = false
                }
            };

            config.Save("config.json");
            Console.WriteLine("[+] Example configuration saved to config.json");
            Console.WriteLine("    Edit this file to customise your default settings");
        }
    }

    public class RetrySettings
    {
        [JsonProperty("maxAttempts")]
        public int MaxAttempts { get; set; } = 3;

        [JsonProperty("baseDelayMilliseconds")]
        public int BaseDelayMilliseconds { get; set; } = 1000;

        [JsonProperty("maxDelayMilliseconds")]
        public int MaxDelayMilliseconds { get; set; } = 30000;

        [JsonProperty("useExponentialBackoff")]
        public bool UseExponentialBackoff { get; set; } = true;
    }

    public class PerformanceSettings
    {
        [JsonProperty("maxParallelDownloads")]
        public int MaxParallelDownloads { get; set; } = 5;

        [JsonProperty("chunkSizeKB")]
        public int ChunkSizeKB { get; set; } = 1024;

        [JsonProperty("enableProgressReporting")]
        public bool EnableProgressReporting { get; set; } = true;

        [JsonProperty("connectionTimeoutSeconds")]
        public int ConnectionTimeoutSeconds { get; set; } = 30;
    }

    public class ReportingSettings
    {
        [JsonProperty("generateHtmlReport")]
        public bool GenerateHtmlReport { get; set; } = true;

        [JsonProperty("generateCsvExport")]
        public bool GenerateCsvExport { get; set; } = true;

        [JsonProperty("generateCleanupScripts")]
        public bool GenerateCleanupScripts { get; set; } = true;

        [JsonProperty("verboseLogging")]
        public bool VerboseLogging { get; set; } = false;
    }
}