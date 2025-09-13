using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;

namespace MockSCCMServer.Services
{
    public class MockSmbServer
    {
        private readonly string _contentRoot;
        private readonly Dictionary<string, string> _shares;

        public MockSmbServer(string contentRoot)
        {
            _contentRoot = contentRoot;
            _shares = new Dictionary<string, string>
            {
                ["SCCMContentLib$"] = Path.Combine(contentRoot, "SCCMContentLib$"),
                ["SMSPKGD$"] = Path.Combine(contentRoot, "SMSPKGD$"),
                ["ADMIN$"] = Path.Combine(contentRoot, "ADMIN$")
            };
        }

        public void Start()
        {
            
            // Create shared directories if they don't exist
            foreach (var share in _shares.Values)
            {
                Directory.CreateDirectory(share);
            }
            
            Console.WriteLine("[+] Mock content directories created:");
            foreach (var share in _shares)
            {
                Console.WriteLine($"    {share.Key} -> {share.Value}");
            }
            
            // Attempt to create Windows shares programmatically
            Console.WriteLine();
            Console.WriteLine("[*] Attempting to create Windows shares...");
            
            bool sharesCreated = false;
            foreach (var share in _shares)
            {
                if (CreateWindowsShare(share.Key, share.Value))
                {
                    sharesCreated = true;
                }
            }
            
            if (!sharesCreated)
            {
                Console.WriteLine();
                Console.WriteLine("[!] Unable to create shares automatically (requires Administrator privileges)");
                Console.WriteLine();
                Console.WriteLine("To create shares manually, run as Administrator:");
                Console.WriteLine("    MockSCCMServer\\create_shares.bat");
                Console.WriteLine();
                Console.WriteLine("Or run these commands as Administrator:");
                foreach (var share in _shares)
                {
                    Console.WriteLine($"    net share {share.Key}=\"{share.Value}\" /grant:Everyone,FULL");
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("[+] Windows shares created successfully!");
                Console.WriteLine("You can now test with: SCML.exe --host localhost --list-shares");
            }
            
            Console.WriteLine();
            Console.WriteLine("[*] Mock content server ready for testing");
        }
        
        private bool CreateWindowsShare(string shareName, string sharePath)
        {
            try
            {
                // First, try to delete existing share if it exists
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"share {shareName} /delete",
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }).WaitForExit(3000);
                
                // Create new share
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "net",
                    Arguments = $"share {shareName}=\"{sharePath}\" /grant:Everyone,FULL",
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                
                process.WaitForExit(5000);
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"    [+] Created share: {shareName}");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                // Silently fail - we'll show manual instructions
                return false;
            }
        }

        public void Stop()
        {
            Console.WriteLine("[*] Cleaning up mock SMB shares...");
            
            // Attempt to remove Windows shares
            foreach (var shareName in _shares.Keys)
            {
                try
                {
                    var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "net",
                        Arguments = $"share {shareName} /delete",
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    
                    process.WaitForExit(3000);
                    
                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine($"    [+] Removed share: {shareName}");
                    }
                }
                catch
                {
                    // Silently continue - shares may not exist or we may not have permissions
                }
            }
            
            Console.WriteLine("[+] Mock SMB server stopped");
        }

        public List<string> GetShareNames()
        {
            return new List<string>(_shares.Keys);
        }

        public string GetSharePath(string shareName)
        {
            return _shares.ContainsKey(shareName) ? _shares[shareName] : null;
        }

        public bool ShareExists(string shareName)
        {
            return _shares.ContainsKey(shareName);
        }
        
        public void CreateTestingInstructions()
        {
            var instructionsPath = Path.Combine(_contentRoot, "TESTING_INSTRUCTIONS.txt");
            var instructions = @"Mock SCCM Server Testing Instructions
=====================================

This mock server creates a realistic SCCM content structure for testing.

Directory Structure:
-------------------
SCCMContentLib$/
├── DataLib/          - Hash-based content storage (SCCM's actual structure)
├── FileLib/          - File library
└── PkgLib/           - Package library

SMSPKGD$/             - Traditional package storage
└── [Package folders with readable names]

ADMIN$/               - Administrative share simulation
├── SCCMContentLib/   - Alternative SCCM location
├── Program Files/    - Standard Windows structure
└── Windows/Temp/     - Temporary files location

Testing Commands:
----------------

1. List shares:
   SCML.exe --host localhost --list-shares --debug

2. Basic inventory:
   SCML.exe --host localhost --outfile test_inventory.txt --debug --current-user

3. Sensitive file scan:
   SCML.exe --host localhost --outfile sensitive.txt --preset credentials --debug

4. Full security audit:
   SCML.exe --host localhost --outfile audit.txt --preset all-sensitive --html-report --debug

File System Access:
------------------
Since this is a mock server, you can also directly access the files at:
" + _contentRoot + @"

The mock server generates realistic SCCM content including:
- Deployment scripts with embedded credentials
- Configuration files with connection strings
- Unattend.xml files with passwords
- Certificate and key files
- Various sensitive configuration files

Sensitivity Levels:
------------------
BLACK: Critical files (passwords, private keys)
RED: High sensitivity (config files, connection strings)
YELLOW: Medium sensitivity (scripts, certificates)
GREEN: Low sensitivity (documentation, general files)

Authentication Testing:
----------------------
The mock server accepts any credentials for testing purposes.
This allows testing of both successful and failed authentication scenarios.

For testing authentication failures, use obviously wrong credentials:
SCML.exe --host localhost --username wronguser --password wrongpass --outfile test.txt
";

            File.WriteAllText(instructionsPath, instructions);
            Console.WriteLine($"[+] Created testing instructions: {instructionsPath}");
        }
    }
}