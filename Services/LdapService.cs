using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Text;
using System.Linq;
using SCML.Models;

namespace SCML.Services
{
    public class LdapService
    {
        public List<string> FindSCCMServers(string domain, string username, string password, int port)
        {
            var servers = new List<string>();
            
            try
            {
                // Create LDAP connection
                var ldapConnection = new LdapConnection(new LdapDirectoryIdentifier(domain, port));
                
                // Use current user if no credentials provided
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ldapConnection.Credential = CredentialCache.DefaultNetworkCredentials;
                }
                else
                {
                    ldapConnection.Credential = new NetworkCredential(username, password, domain);
                }
                
                ldapConnection.AuthType = AuthType.Negotiate;
                
                // Set LDAP version
                ldapConnection.SessionOptions.ProtocolVersion = 3;
                
                // Use LDAPS if port is 636
                if (port == 636)
                {
                    ldapConnection.SessionOptions.SecureSocketLayer = true;
                }
                
                // Bind to LDAP
                ldapConnection.Bind();
                
                // Convert domain to base DN
                var baseDn = ConvertDomainToBaseDn(domain);
                
                // Create search request for SCCM Management Points and Site Systems
                // This will find Management Points, Distribution Points, and Site Servers
                var searchRequest = new SearchRequest(
                    baseDn,
                    "(|(objectClass=mSSMSManagementPoint)(objectClass=mSSMSSite)(objectClass=mSSMSRoamingBoundaryRange))",
                    SearchScope.Subtree,
                    new string[] { "mSSMSMPName", "mSSMSSiteCode", "cn", "distinguishedName", "dNSHostName", "serverName" }
                );
                
                // Execute search
                var searchResponse = (SearchResponse)ldapConnection.SendRequest(searchRequest);
                
                // Process results
                var serverInfoList = new List<SccmServerInfo>();
                
                foreach (SearchResultEntry entry in searchResponse.Entries)
                {
                    var serverInfo = new SccmServerInfo();
                    
                    // Get server name
                    if (entry.Attributes.Contains("mSSMSMPName"))
                    {
                        serverInfo.ServerName = GetAttributeValue(entry.Attributes["mSSMSMPName"]);
                        serverInfo.Role = "Management Point";
                    }
                    else if (entry.Attributes.Contains("dNSHostName"))
                    {
                        serverInfo.ServerName = GetAttributeValue(entry.Attributes["dNSHostName"]);
                        serverInfo.Role = "Site System";
                    }
                    else if (entry.Attributes.Contains("serverName"))
                    {
                        serverInfo.ServerName = GetAttributeValue(entry.Attributes["serverName"]);
                        serverInfo.Role = "Site System";
                    }
                    else if (entry.Attributes.Contains("cn"))
                    {
                        serverInfo.ServerName = GetAttributeValue(entry.Attributes["cn"]);
                        serverInfo.Role = "SCCM Object";
                    }
                    
                    // Get site code if available
                    if (entry.Attributes.Contains("mSSMSSiteCode"))
                    {
                        serverInfo.SiteCode = GetAttributeValue(entry.Attributes["mSSMSSiteCode"]);
                    }
                    
                    // Get DN for additional context
                    if (entry.Attributes.Contains("distinguishedName"))
                    {
                        serverInfo.DistinguishedName = GetAttributeValue(entry.Attributes["distinguishedName"]);
                        
                        // Try to extract site code from DN if not already found
                        if (string.IsNullOrEmpty(serverInfo.SiteCode) && !string.IsNullOrEmpty(serverInfo.DistinguishedName))
                        {
                            // SCCM objects often have site code in their DN path
                            var match = System.Text.RegularExpressions.Regex.Match(
                                serverInfo.DistinguishedName, 
                                @"CN=([A-Z0-9]{3}),", 
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                serverInfo.SiteCode = match.Groups[1].Value;
                            }
                        }
                    }
                    
                    if (!string.IsNullOrWhiteSpace(serverInfo.ServerName))
                    {
                        serverInfoList.Add(serverInfo);
                        servers.Add(serverInfo.ServerName);
                        
                        // Output detailed information
                        Console.WriteLine($"[+] Found: {serverInfo}");
                    }
                }
                
                // Also search for Site Servers specifically
                SearchForSiteServers(ldapConnection, baseDn, servers);
                
                // Dispose connection
                ldapConnection.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] LDAP error: {ex.Message}");
            }
            
            // Remove duplicates and return
            return servers.Distinct().ToList();
        }
        
        private void SearchForSiteServers(LdapConnection connection, string baseDn, List<string> servers)
        {
            try
            {
                // Search for SCCM Site Servers
                var siteSearchRequest = new SearchRequest(
                    $"CN=System Management,CN=System,{baseDn}",
                    "(objectClass=*)",
                    SearchScope.Subtree,
                    new string[] { "cn", "distinguishedName", "servicePrincipalName" }
                );
                
                var siteSearchResponse = (SearchResponse)connection.SendRequest(siteSearchRequest);
                
                foreach (SearchResultEntry entry in siteSearchResponse.Entries)
                {
                    // Look for service principal names that indicate SCCM servers
                    if (entry.Attributes.Contains("servicePrincipalName"))
                    {
                        var spns = entry.Attributes["servicePrincipalName"];
                        foreach (var spn in spns)
                        {
                            var spnValue = GetAttributeValue(spn);
                            if (spnValue.Contains("SMS_Site") || spnValue.Contains("SMS_MP"))
                            {
                                // Extract server name from SPN
                                var match = System.Text.RegularExpressions.Regex.Match(spnValue, @"[^/]+/([^:]+)");
                                if (match.Success)
                                {
                                    var serverName = match.Groups[1].Value;
                                    if (!servers.Contains(serverName))
                                    {
                                        servers.Add(serverName);
                                        Console.WriteLine($"[+] Found Site Server from SPN: {serverName}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-critical error, continue
                Console.WriteLine($"[!] Could not search System Management container: {ex.Message}");
            }
        }
        
        private string GetAttributeValue(object attributeValue)
        {
            if (attributeValue is byte[])
            {
                return Encoding.UTF8.GetString((byte[])attributeValue);
            }
            else if (attributeValue is DirectoryAttribute)
            {
                var attr = (DirectoryAttribute)attributeValue;
                if (attr.Count > 0)
                {
                    return GetAttributeValue(attr[0]);
                }
            }
            return attributeValue?.ToString() ?? string.Empty;
        }
        
        private string ConvertDomainToBaseDn(string domain)
        {
            var parts = domain.Split('.');
            var dnParts = new List<string>();
            
            foreach (var part in parts)
            {
                dnParts.Add($"DC={part}");
            }
            
            return string.Join(",", dnParts);
        }
    }
}