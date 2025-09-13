using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;

namespace MockSCCMServer.Services
{
    public class MockLdapServer
    {
        private readonly string _domain;
        private readonly List<string> _sccmServers;

        public MockLdapServer(string domain = "CORP.LOCAL")
        {
            _domain = domain;
            _sccmServers = new List<string>
            {
                "SCCM-PRIMARY.corp.local",
                "SCCM-SECONDARY.corp.local", 
                "SCCM-DP01.corp.local"
            };
        }

        public void CreateMockLdapData()
        {
            Console.WriteLine("[*] Creating mock LDAP data for SCCM discovery testing...");
            
            // Create a mock LDIF file that could be imported into a test LDAP server
            var ldifPath = Path.Combine(Directory.GetCurrentDirectory(), "mock_sccm_ldap.ldif");
            
            var ldifContent = GenerateLdifContent();
            File.WriteAllText(ldifPath, ldifContent);
            
            Console.WriteLine($"[+] Created mock LDAP data: {ldifPath}");
            Console.WriteLine("    This LDIF file can be imported into a test LDAP server");
            Console.WriteLine("    For testing, modify your hosts file to point SCCM server names to localhost");
        }

        private string GenerateLdifContent()
        {
            var ldif = $@"# Mock SCCM LDAP Data
# Domain: {_domain}
# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

# Domain entry
dn: dc=corp,dc=local
objectClass: domain
objectClass: domainDNS
dc: corp

# System Management container (where SCCM objects are stored)
dn: cn=System Management,cn=System,dc=corp,dc=local
objectClass: container
cn: System Management

# SCCM Site System entries
";

            for (int i = 0; i < _sccmServers.Count; i++)
            {
                var server = _sccmServers[i];
                var serverType = i == 0 ? "Primary" : i == 1 ? "Secondary" : "Distribution Point";
                var siteCode = i == 0 ? "PR1" : i == 1 ? "SC1" : "DP1";
                
                ldif += $@"
dn: cn={server},cn=System Management,cn=System,dc=corp,dc=local
objectClass: mSSMSManagementPoint
objectClass: mSSMSSiteSystemServer
cn: {server}
mSSMSMPName: {server}
mSSMSSiteCode: {siteCode}
mSSMSServerType: {serverType}
dNSHostName: {server}
description: Mock SCCM {serverType} Server

";
            }

            ldif += @"
# Instructions for testing:
# 1. Import this LDIF into your test LDAP server
# 2. Configure SCML to query your test LDAP server
# 3. Add entries to your hosts file:
#    127.0.0.1 SCCM-PRIMARY.corp.local
#    127.0.0.1 SCCM-SECONDARY.corp.local
#    127.0.0.1 SCCM-DP01.corp.local
";

            return ldif;
        }

        public List<string> GetMockSccmServers()
        {
            return new List<string>(_sccmServers);
        }

        public void CreateHostsFileInstructions()
        {
            var instructionsPath = Path.Combine(Directory.GetCurrentDirectory(), "hosts_file_instructions.txt");
            
            var instructions = @"Hosts File Configuration for Mock SCCM Testing
==============================================

To test SCCM discovery with the mock server, add these entries to your hosts file:

Windows hosts file location: C:\Windows\System32\drivers\etc\hosts
Linux/Mac hosts file location: /etc/hosts

Add these lines:
--------------
127.0.0.1    SCCM-PRIMARY.corp.local
127.0.0.1    SCCM-SECONDARY.corp.local  
127.0.0.1    SCCM-DP01.corp.local
127.0.0.1    localhost.corp.local

Test Discovery Commands:
-----------------------
After updating your hosts file, test SCCM discovery:

# Test each server individually
SCML.exe --host SCCM-PRIMARY.corp.local --list-shares --debug
SCML.exe --host SCCM-SECONDARY.corp.local --outfile test1.txt --debug  
SCML.exe --host SCCM-DP01.corp.local --outfile test2.txt --debug

# Test multi-target from file
echo SCCM-PRIMARY.corp.local > targets.txt
echo SCCM-SECONDARY.corp.local >> targets.txt
echo SCCM-DP01.corp.local >> targets.txt
SCML.exe --targets-file targets.txt --outfile multi_test.txt --debug

Remember to remove these entries from your hosts file when testing is complete.

Note: LDAP discovery testing requires a separate LDAP server setup.
The mock_sccm_ldap.ldif file can be imported into OpenLDAP or Active Directory for testing.
";

            File.WriteAllText(instructionsPath, instructions);
            Console.WriteLine($"[+] Created hosts file instructions: {instructionsPath}");
        }
    }
}