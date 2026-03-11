using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Text;

namespace SnafflerSCCMAddon
{
    /// <summary>
    /// SCCM share discovery module for Snaffler
    /// Discovers SCCM Distribution Points via LDAP and enumerates SCCMContentLib$ shares
    /// </summary>
    public class SCCMShareDiscovery
    {
        private readonly string _domain;
        private readonly string _username;
        private readonly string _password;
        private readonly bool _verbose;
        private readonly int _ldapPort;

        public SCCMShareDiscovery(string domain, string username = null, string password = null,
            bool verbose = false, int ldapPort = 389)
        {
            _domain = domain;
            _username = username;
            _password = password;
            _verbose = verbose;
            _ldapPort = ldapPort;
        }

        /// <summary>
        /// Discover SCCM servers via LDAP query
        /// </summary>
        public List<SCCMServer> DiscoverSCCMServers()
        {
            var servers = new List<SCCMServer>();

            try
            {
                if (_verbose)
                    Console.WriteLine($"[*] Discovering SCCM servers in domain: {_domain}");

                // Try multiple LDAP query methods
                servers.AddRange(QueryViaSMS_SiteSystemServer());
                servers.AddRange(QueryViaServiceConnectionPoint());
                servers.AddRange(QueryViaDistributionPoint());

                // Deduplicate by hostname
                var uniqueServers = servers
                    .GroupBy(s => s.Hostname.ToLower())
                    .Select(g => g.First())
                    .ToList();

                if (_verbose)
                    Console.WriteLine($"[+] Found {uniqueServers.Count} unique SCCM servers");

                return uniqueServers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] LDAP discovery error: {ex.Message}");
                if (_verbose)
                    Console.WriteLine($"    Stack: {ex.StackTrace}");
                return servers;
            }
        }

        /// <summary>
        /// Query for SMS_SiteSystemServer objects (primary method)
        /// </summary>
        private List<SCCMServer> QueryViaSMS_SiteSystemServer()
        {
            var servers = new List<SCCMServer>();

            try
            {
                var ldapPath = $"LDAP://{_domain}/CN=System Management,CN=System,DC={_domain.Replace(".", ",DC=")}";

                using (var entry = new DirectoryEntry(ldapPath, _username, _password))
                using (var searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = "(objectClass=mSSMSSite)";
                    searcher.PropertiesToLoad.Add("mSSMSSiteCode");
                    searcher.PropertiesToLoad.Add("cn");
                    searcher.SearchScope = SearchScope.Subtree;

                    var results = searcher.FindAll();

                    if (_verbose)
                        Console.WriteLine($"[*] Found {results.Count} SCCM site(s)");

                    foreach (SearchResult result in results)
                    {
                        var siteCode = result.Properties["mSSMSSiteCode"]?[0]?.ToString();
                        var siteName = result.Properties["cn"]?[0]?.ToString();

                        if (_verbose)
                            Console.WriteLine($"[*] Enumerating site: {siteName} ({siteCode})");

                        // Now find distribution points for this site
                        servers.AddRange(FindDistributionPointsForSite(siteCode));
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[!] SMS_SiteSystemServer query failed: {ex.Message}");
            }

            return servers;
        }

        /// <summary>
        /// Find Distribution Points for a specific site code
        /// </summary>
        private List<SCCMServer> FindDistributionPointsForSite(string siteCode)
        {
            var servers = new List<SCCMServer>();

            try
            {
                var ldapPath = $"LDAP://{_domain}";

                using (var entry = new DirectoryEntry(ldapPath, _username, _password))
                using (var searcher = new DirectorySearcher(entry))
                {
                    // Look for servers with mSSMSRolesConfigured attribute containing "SMS Distribution Point"
                    searcher.Filter = "(&(objectClass=mSSMSSiteSystemServer)(mSSMSDefaultMP=*))";
                    searcher.PropertiesToLoad.Add("mSSMSSiteName");
                    searcher.PropertiesToLoad.Add("cn");
                    searcher.PropertiesToLoad.Add("dNSHostName");
                    searcher.SearchScope = SearchScope.Subtree;

                    var results = searcher.FindAll();

                    foreach (SearchResult result in results)
                    {
                        var hostname = result.Properties["dNSHostName"]?[0]?.ToString();
                        var cn = result.Properties["cn"]?[0]?.ToString();

                        if (string.IsNullOrEmpty(hostname) && !string.IsNullOrEmpty(cn))
                        {
                            // Try to extract hostname from CN
                            hostname = cn.Split(',')[0].Replace("CN=", "");
                        }

                        if (!string.IsNullOrEmpty(hostname))
                        {
                            servers.Add(new SCCMServer
                            {
                                Hostname = hostname,
                                SiteCode = siteCode,
                                Role = "Distribution Point",
                                DiscoveryMethod = "LDAP-SiteSystem"
                            });

                            if (_verbose)
                                Console.WriteLine($"[+] Found DP: {hostname} (Site: {siteCode})");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[!] Distribution Point query failed: {ex.Message}");
            }

            return servers;
        }

        /// <summary>
        /// Query via Service Connection Point (alternative method)
        /// </summary>
        private List<SCCMServer> QueryViaServiceConnectionPoint()
        {
            var servers = new List<SCCMServer>();

            try
            {
                var ldapPath = $"LDAP://{_domain}";

                using (var entry = new DirectoryEntry(ldapPath, _username, _password))
                using (var searcher = new DirectorySearcher(entry))
                {
                    searcher.Filter = "(&(objectClass=serviceConnectionPoint)(cn=SMS-MP-*))";
                    searcher.PropertiesToLoad.Add("serviceBindingInformation");
                    searcher.PropertiesToLoad.Add("cn");
                    searcher.SearchScope = SearchScope.Subtree;

                    var results = searcher.FindAll();

                    foreach (SearchResult result in results)
                    {
                        var bindingInfo = result.Properties["serviceBindingInformation"];
                        if (bindingInfo != null && bindingInfo.Count > 0)
                        {
                            foreach (var info in bindingInfo)
                            {
                                var infoStr = info.ToString();
                                // Parse hostname from binding information
                                if (infoStr.Contains("://"))
                                {
                                    var hostname = ExtractHostnameFromUrl(infoStr);
                                    if (!string.IsNullOrEmpty(hostname))
                                    {
                                        servers.Add(new SCCMServer
                                        {
                                            Hostname = hostname,
                                            Role = "Management Point",
                                            DiscoveryMethod = "LDAP-ServiceConnectionPoint"
                                        });

                                        if (_verbose)
                                            Console.WriteLine($"[+] Found MP: {hostname}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[!] Service Connection Point query failed: {ex.Message}");
            }

            return servers;
        }

        /// <summary>
        /// Query via direct Distribution Point search (fallback method)
        /// </summary>
        private List<SCCMServer> QueryViaDistributionPoint()
        {
            var servers = new List<SCCMServer>();

            try
            {
                var ldapPath = $"LDAP://{_domain}";

                using (var entry = new DirectoryEntry(ldapPath, _username, _password))
                using (var searcher = new DirectorySearcher(entry))
                {
                    // Search for computers with "SMS" or "SCCM" in description/name
                    searcher.Filter = "(|(cn=*SCCM*)(cn=*SMS*)(description=*SCCM*)(description=*SMS*)(description=*Configuration Manager*))";
                    searcher.PropertiesToLoad.Add("dNSHostName");
                    searcher.PropertiesToLoad.Add("cn");
                    searcher.PropertiesToLoad.Add("description");
                    searcher.SearchScope = SearchScope.Subtree;

                    var results = searcher.FindAll();

                    foreach (SearchResult result in results)
                    {
                        var hostname = result.Properties["dNSHostName"]?[0]?.ToString();
                        var description = result.Properties["description"]?[0]?.ToString();

                        if (!string.IsNullOrEmpty(hostname))
                        {
                            servers.Add(new SCCMServer
                            {
                                Hostname = hostname,
                                Role = "Potential SCCM Server",
                                Description = description,
                                DiscoveryMethod = "LDAP-Fallback"
                            });

                            if (_verbose)
                                Console.WriteLine($"[+] Found potential: {hostname}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[!] Fallback query failed: {ex.Message}");
            }

            return servers;
        }

        /// <summary>
        /// Extract hostname from URL string
        /// </summary>
        private string ExtractHostnameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                // Try manual parsing
                var parts = url.Split(new[] { "://", "/" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return parts[1].Split(':')[0];
                return null;
            }
        }

        /// <summary>
        /// Build target paths for Snaffler from discovered servers
        /// Returns list of UNC paths to SCCM shares
        /// </summary>
        public List<string> BuildSnafflerTargets(List<SCCMServer> servers)
        {
            var targets = new List<string>();

            foreach (var server in servers)
            {
                // SCCMContentLib$ share
                targets.Add($"\\\\{server.Hostname}\\SCCMContentLib$");

                // Also add common SCCM shares
                targets.Add($"\\\\{server.Hostname}\\SMS_{server.SiteCode}");
                targets.Add($"\\\\{server.Hostname}\\SMSPKG{server.SiteCode}");
            }

            return targets.Distinct().ToList();
        }

        /// <summary>
        /// Validate server accessibility and SCCM share existence
        /// </summary>
        public List<SCCMServer> ValidateServers(List<SCCMServer> servers)
        {
            var validatedServers = new List<SCCMServer>();

            Console.WriteLine($"\n[*] Validating {servers.Count} server(s)...");

            foreach (var server in servers)
            {
                try
                {
                    // Try to resolve hostname
                    var addresses = Dns.GetHostAddresses(server.Hostname);
                    server.IPAddress = addresses.FirstOrDefault()?.ToString();

                    // Check if SCCMContentLib$ share exists (ping test)
                    var sharePath = $"\\\\{server.Hostname}\\SCCMContentLib$";

                    if (System.IO.Directory.Exists(sharePath))
                    {
                        server.SCCMContentLibAccessible = true;
                        validatedServers.Add(server);

                        if (_verbose)
                            Console.WriteLine($"[+] Validated: {server.Hostname} - SCCMContentLib$ accessible");
                    }
                    else
                    {
                        if (_verbose)
                            Console.WriteLine($"[!] {server.Hostname} - SCCMContentLib$ not accessible");
                    }
                }
                catch (Exception ex)
                {
                    if (_verbose)
                        Console.WriteLine($"[-] Failed to validate {server.Hostname}: {ex.Message}");
                }
            }

            Console.WriteLine($"[+] {validatedServers.Count}/{servers.Count} server(s) validated with accessible shares");

            return validatedServers;
        }
    }

    /// <summary>
    /// SCCM Server information
    /// </summary>
    public class SCCMServer
    {
        public string Hostname { get; set; }
        public string IPAddress { get; set; }
        public string SiteCode { get; set; }
        public string Role { get; set; }
        public string Description { get; set; }
        public string DiscoveryMethod { get; set; }
        public bool SCCMContentLibAccessible { get; set; }

        public override string ToString()
        {
            return $"{Hostname} ({Role}) - Site: {SiteCode ?? "Unknown"}";
        }
    }
}
