using System;
using System.IO;
using System.Threading;
using MockSCCMServer.Services;

namespace MockSCCMServer
{
    class Program
    {
        private static bool _isRunning = true;
        private static ContentGenerator _contentGenerator;
        private static MockSmbServer _smbServer;
        private static string _contentRoot;

        static void Main(string[] args)
        {
            Console.WriteLine("Mock SCCM Server v1.0");
            Console.WriteLine("=====================");
            Console.WriteLine("This mock server simulates SCCM infrastructure for testing purposes.");
            Console.WriteLine();

            try
            {
                // Set up content root directory
                _contentRoot = Path.Combine(Environment.CurrentDirectory, "MockSCCMContent");
                
                Console.WriteLine($"Content root: {_contentRoot}");
                Console.WriteLine();

                // Initialize services
                InitializeServices();

                // Set up console cancellation
                Console.CancelKeyPress += Console_CancelKeyPress;

                Console.WriteLine("Mock SCCM Server is running...");
                Console.WriteLine("Available shares:");
                Console.WriteLine("  - SCCMContentLib$");
                Console.WriteLine("  - SMSPKGD$");
                Console.WriteLine("  - ADMIN$");
                Console.WriteLine();
                Console.WriteLine("Test commands:");
                Console.WriteLine("  net view \\\\localhost");
                Console.WriteLine("  SCML.exe --host localhost --list-shares --debug");
                Console.WriteLine();
                Console.WriteLine("Press Ctrl+C to stop the server");

                // Keep the server running
                while (_isRunning)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (args.Length > 0 && args[0] == "--verbose")
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
            finally
            {
                Cleanup();
            }
        }

        private static void InitializeServices()
        {
            Console.WriteLine("[*] Initializing mock SCCM content...");
            
            // Generate realistic SCCM content structure
            _contentGenerator = new ContentGenerator(_contentRoot);
            _contentGenerator.GenerateContent();

            Console.WriteLine("[+] Generated mock SCCM content structure");

            // Start mock SMB server simulation
            Console.WriteLine("[*] Starting SMB share simulation...");
            _smbServer = new MockSmbServer(_contentRoot);
            _smbServer.Start();
            _smbServer.CreateTestingInstructions();

            Console.WriteLine("[+] SMB shares are now accessible");

            // Create mock LDAP data for discovery testing
            Console.WriteLine("[*] Creating mock LDAP data...");
            var ldapServer = new MockLdapServer("CORP.LOCAL");
            ldapServer.CreateMockLdapData();
            ldapServer.CreateHostsFileInstructions();

            Console.WriteLine("[+] Mock LDAP data created");
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("\n[*] Shutting down mock SCCM server...");
            e.Cancel = true;
            _isRunning = false;
        }

        private static void Cleanup()
        {
            try
            {
                _smbServer?.Stop();
                Console.WriteLine("[+] Mock SCCM server stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error during cleanup: {ex.Message}");
            }
        }
    }
}