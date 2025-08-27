using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SCML.Utilities
{
    public static class HelperUtilities
    {
        /// <summary>
        /// Convert FQDN to Base DN format for LDAP
        /// </summary>
        public static string FqdnToBaseDn(string fqdn)
        {
            if (string.IsNullOrEmpty(fqdn))
                return string.Empty;

            var components = fqdn.Split('.');
            var baseDnComponents = new List<string>();
            
            foreach (var component in components)
            {
                baseDnComponents.Add(string.Format("DC={0}", component));
            }
            
            return string.Join(",", baseDnComponents.ToArray());
        }

        /// <summary>
        /// Sort and remove duplicates from inventory file
        /// </summary>
        public static void SortAndUniqFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return;

                var lines = File.ReadAllLines(filePath);
                
                // Remove duplicates (case-insensitive) and sort
                var uniqueLines = lines
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .GroupBy(line => line.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                File.WriteAllLines(filePath, uniqueLines);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error sorting file: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Parse target string to extract domain, username, and host
        /// Format: [[domain\]username[:password]@]<host>
        /// </summary>
        public static ParsedTarget ParseTarget(string target)
        {
            var result = new ParsedTarget { Host = target };

            if (string.IsNullOrEmpty(target))
                return result;

            // Check if there's an @ sign (credentials provided)
            var atIndex = target.LastIndexOf('@');
            if (atIndex > 0)
            {
                var credentialsPart = target.Substring(0, atIndex);
                result.Host = target.Substring(atIndex + 1);

                // Parse credentials part
                var colonIndex = credentialsPart.IndexOf(':');
                var userPart = colonIndex > 0 ? credentialsPart.Substring(0, colonIndex) : credentialsPart;
                
                if (colonIndex > 0 && colonIndex < credentialsPart.Length - 1)
                {
                    result.Password = credentialsPart.Substring(colonIndex + 1);
                }

                // Check for domain\username format
                var backslashIndex = userPart.IndexOf('\\');
                if (backslashIndex > 0)
                {
                    result.Domain = userPart.Substring(0, backslashIndex);
                    result.Username = userPart.Substring(backslashIndex + 1);
                }
                else
                {
                    result.Username = userPart;
                }
            }

            return result;
        }

        /// <summary>
        /// Validate file extensions format
        /// </summary>
        public static List<string> NormalizeExtensions(IEnumerable<string> extensions)
        {
            if (extensions == null)
                return new List<string>();

            var normalized = new List<string>();
            
            foreach (var ext in extensions)
            {
                if (string.IsNullOrWhiteSpace(ext))
                    continue;

                var trimmed = ext.Trim();
                
                // Ensure extension starts with a dot
                if (!trimmed.StartsWith("."))
                    trimmed = "." + trimmed;
                
                // Convert to uppercase for consistency
                normalized.Add(trimmed.ToUpper());
            }

            return normalized.Distinct().ToList();
        }

        /// <summary>
        /// Create output directory if it doesn't exist
        /// </summary>
        public static bool EnsureDirectoryExists(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    return true;
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error creating directory {0}: {1}", path, ex.Message));
                return false;
            }
        }

        public class ParsedTarget
        {
            public string Host { get; set; }
            public string Domain { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}