using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SCML.Services
{
    public class InventoryService
    {
        private readonly SmbService _smbService;
        private readonly bool _debug;
        private string _connectedShareName = null;
        private string _sccmContentPath = null;
        private string _targetHost = null;
        private StreamWriter _inventoryWriter = null;
        private int _fileCount = 0;
        private int _flushInterval = 10;

        // Alternative share names to try
        private readonly string[] _shareNameVariants = new string[]
        {
            "SCCMContentLib$",
            "SCCMContentLib",
            "SMS_DP$",
            "SMS_DistributionPoint$",
            "ContentLib$",
            "SMSPKGD$",     // Common SCCM package share
            "SMSPKGE$",     // Another SCCM package share
            "SMSPKGF$",     // Another SCCM package share
            "SMSSIG$",      // SCCM signature share
            "SMS_CPSC$",    // Client Peer Cache
            "ADMIN$"        // Last resort - browse for SCCM folders
        };

        public InventoryService(SmbService smbService, bool debug = false)
        {
            _smbService = smbService;
            _debug = debug;
        }

        public void CreateInventory(string inventoryFile, bool append, string targetHost = null)
        {
            _targetHost = targetHost;
            
            if (File.Exists(inventoryFile) && !append)
            {
                Console.WriteLine(string.Format("[!] {0} already exists. Skipping inventory creation.", inventoryFile));
                Console.WriteLine("    Use --append to add to existing inventory, or remove the file to create new.");
                return;
            }

            if (File.Exists(inventoryFile) && append)
            {
                Console.WriteLine(string.Format("[+] {0} exists. Appending to it.", inventoryFile));
            }
            
            // Initialize StreamWriter for periodic writes
            StreamWriter inventoryWriter = null;
            try
            {
                inventoryWriter = new StreamWriter(inventoryFile, append);
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Failed to open inventory file for writing: {0}", ex.Message));
                throw;
            }

            // Try to connect to SCCM content share
            bool connected = false;
            foreach (var shareName in _shareNameVariants)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[*] Attempting to connect to share: {0}", shareName));
                
                if (_smbService.ConnectToShare(shareName))
                {
                    _connectedShareName = shareName;
                    
                    // If connected to ADMIN$, need to find SCCM content path
                    if (shareName == "ADMIN$")
                    {
                        if (_debug)
                            Console.WriteLine("[*] Connected to ADMIN$, searching for SCCM content library...");
                        
                        _sccmContentPath = FindSccmContentPath();
                        if (_sccmContentPath == null)
                        {
                            Console.WriteLine("[-] Connected to ADMIN$ but couldn't find SCCM content library");
                            continue;
                        }
                        Console.WriteLine(string.Format("[+] Found SCCM content at: {0}", _sccmContentPath));
                    }
                    
                    connected = true;
                    break;
                }
            }
            
            if (!connected)
            {
                Console.WriteLine("[-] Failed to connect to any SCCM content share");
                Console.WriteLine("    Tried shares: " + string.Join(", ", _shareNameVariants));
                Console.WriteLine("    Common causes:");
                Console.WriteLine("    - SCCM not installed on this server");
                Console.WriteLine("    - Insufficient permissions");
                Console.WriteLine("    - Firewall blocking SMB port 445");
                throw new Exception("Failed to access SCCM content share. Ensure you have proper permissions.");
            }
            
            if (_debug)
                Console.WriteLine(string.Format("[+] Successfully connected to {0} share", _connectedShareName));

            Console.WriteLine(string.Format("[+] Access to {0} confirmed", _connectedShareName));

            try
            {
                _inventoryWriter = inventoryWriter;
                _fileCount = 0;
                _flushInterval = 10;  // Flush every 10 files for real-time writing

                // Determine base path for scanning
                string basePath = _sccmContentPath ?? "";
                string dataLibPath = string.IsNullOrEmpty(basePath) ? "DataLib" : basePath + "\\DataLib";
                
                Console.WriteLine("[*] Starting file enumeration and writing to inventory in real-time...");
                
                // Scan DataLib folder
                var rootFolders = GetFoldersInFolder(dataLibPath);
                Console.WriteLine(string.Format("[+] Found {0} root folders to scan", rootFolders.Count));
                
                foreach (var folder in rootFolders)
                {
                    if (_debug)
                        Console.WriteLine(string.Format("[*] Scanning folder: {0}", folder));
                    
                    // Scan and write files immediately
                    ScanFolderRecursiveWithImmediateWrite(folder);
                }
                
                // Final flush
                inventoryWriter.Flush();
                Console.WriteLine(string.Format("[+] Completed: {0} files written to inventory", _fileCount));
            }
            finally
            {
                // Always close the writer to ensure data is saved
                if (inventoryWriter != null)
                {
                    try
                    {
                        inventoryWriter.Flush();
                        inventoryWriter.Close();
                        inventoryWriter.Dispose();
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
                
                // Force garbage collection to ensure file handles are released
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Small delay to ensure OS has released file handle
                System.Threading.Thread.Sleep(100);
            }
        }

        private void ScanFolderRecursive(string folderPath, List<string> fileList)
        {
            try
            {
                // Get files in current folder
                var files = GetFilesInFolder(folderPath);
                fileList.AddRange(files);

                // Get subfolders and scan them
                var subfolders = GetFoldersInFolder(folderPath);
                foreach (var subfolder in subfolders)
                {
                    ScanFolderRecursive(subfolder, fileList);
                }
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Error scanning {0}: {1}", folderPath, ex.Message));
            }
        }
        
        private void ScanFolderRecursiveWithImmediateWrite(string folderPath)
        {
            try
            {
                // Get files in current folder and write immediately
                var files = GetFilesInFolder(folderPath);
                foreach (var file in files)
                {
                    _inventoryWriter.WriteLine(file);
                    _fileCount++;
                    
                    // Flush periodically for real-time updates
                    if (_fileCount % _flushInterval == 0)
                    {
                        _inventoryWriter.Flush();
                        if (_fileCount % 100 == 0)  // Progress update every 100 files
                        {
                            Console.WriteLine(string.Format("[*] Progress: {0} files written to inventory...", _fileCount));
                        }
                    }
                }

                // Get subfolders and scan them
                var subfolders = GetFoldersInFolder(folderPath);
                foreach (var subfolder in subfolders)
                {
                    ScanFolderRecursiveWithImmediateWrite(subfolder);
                }
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Error scanning {0}: {1}", folderPath, ex.Message));
            }
        }

        private List<string> GetFilesInFolder(string folderPath)
        {
            var files = new List<string>();
            
            try
            {
                var items = _smbService.ListFiles(folderPath);
                
                foreach (var item in items.Where(i => !i.IsDirectory && i.Size > 0))
                {
                    // Remove .INI extension if present for the file path
                    var fileName = item.Name.EndsWith(".INI", StringComparison.OrdinalIgnoreCase) 
                        ? item.Name.Substring(0, item.Name.Length - 4) 
                        : item.Name;
                    
                    // Construct full UNC path that can be used for downloading
                    // _targetHost contains the server name, _connectedShareName contains the share name
                    var filePath = string.Format("\\\\{0}\\{1}\\{2}\\{3}", _targetHost, _connectedShareName, folderPath, fileName);
                    files.Add(filePath);
                }
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Error listing files in {0}: {1}", folderPath, ex.Message));
            }

            return files;
        }

        private List<string> GetFoldersInFolder(string folderPath)
        {
            var folders = new List<string>();
            
            try
            {
                var items = _smbService.ListFiles(folderPath);
                
                foreach (var item in items.Where(i => i.IsDirectory))
                {
                    var subfolderPath = string.IsNullOrEmpty(folderPath) 
                        ? item.Name 
                        : string.Format("{0}\\{1}", folderPath, item.Name);
                    folders.Add(subfolderPath);
                }
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Error listing folders in {0}: {1}", folderPath, ex.Message));
            }

            return folders;
        }

        public void SortAndUniqFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                
                // Remove duplicates (case-insensitive) and sort
                var uniqueLines = lines
                    .GroupBy(line => line, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(line => line, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                File.WriteAllLines(filePath, uniqueLines);
                
                if (_debug)
                    Console.WriteLine(string.Format("[+] Removed {0} duplicate entries", lines.Length - uniqueLines.Count));
                
                // Ensure file is fully written and handles are released
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error sorting file: {0}", ex.Message));
            }
        }
        
        private string FindSccmContentPath()
        {
            // Common SCCM content locations when accessing via ADMIN$
            string[] possiblePaths = new string[]
            {
                "SCCMContentLib",
                "SMSPKG",
                "SMS\\PKG",
                "SMS_DP\\ContentLib",
                "SMS_DistributionPoint\\ContentLib",
                "Program Files\\Microsoft Configuration Manager\\CMContentLib",
                "Program Files (x86)\\Microsoft Configuration Manager\\CMContentLib",
                "Program Files\\SMS_CCM\\ServiceData",
                "SMSPKGD$",
                "SMSSIG$"
            };
            
            // First, show what's available in root if debug mode
            if (_debug)
            {
                try
                {
                    Console.WriteLine("[*] Listing root folders in ADMIN$ share:");
                    var rootItems = _smbService.ListFiles("");
                    int folderCount = 0;
                    foreach (var item in rootItems.Where(i => i.IsDirectory))
                    {
                        Console.WriteLine(string.Format("      - {0}", item.Name));
                        
                        // Look for SCCM-related folder names
                        if (item.Name.IndexOf("SCCM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Name.IndexOf("SMS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Name.IndexOf("ContentLib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            item.Name.IndexOf("SMSPKG", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Console.WriteLine(string.Format("        [*] Potential SCCM folder detected!"));
                        }
                        folderCount++;
                        if (folderCount > 20)
                        {
                            Console.WriteLine("      ... (truncated, too many folders)");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[!] Error listing root folders: {0}", ex.Message));
                }
            }
            
            foreach (var path in possiblePaths)
            {
                try
                {
                    if (_debug)
                        Console.WriteLine(string.Format("[*] Checking for SCCM content at: {0}", path));
                    
                    // Try to list files in the path to see if it exists
                    var items = _smbService.ListFiles(path);
                    
                    // Show what's in the folder if debug mode
                    if (_debug && items.Count > 0)
                    {
                        Console.WriteLine(string.Format("    Found {0} items in {1}:", items.Count, path));
                        foreach (var item in items.Take(5))
                        {
                            Console.WriteLine(string.Format("      - {0} {1}", 
                                item.IsDirectory ? "[DIR]" : "[FILE]", 
                                item.Name));
                        }
                    }
                    
                    // Check if DataLib subfolder exists
                    bool hasDataLib = items.Any(i => i.IsDirectory && 
                        (i.Name.Equals("DataLib", StringComparison.OrdinalIgnoreCase) ||
                         i.Name.Equals("FileLib", StringComparison.OrdinalIgnoreCase) ||
                         i.Name.Equals("PkgLib", StringComparison.OrdinalIgnoreCase)));
                    
                    if (hasDataLib)
                    {
                        if (_debug)
                            Console.WriteLine(string.Format("[+] Found SCCM content library at: {0}", path));
                        return path;
                    }
                    
                    // Also check if the path itself might be a content library
                    if (path.IndexOf("SMSPKG", StringComparison.OrdinalIgnoreCase) >= 0 && items.Count > 0)
                    {
                        // SMSPKG folders often contain package content directly
                        Console.WriteLine(string.Format("[+] Found SCCM package folder at: {0}", path));
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    if (_debug)
                        Console.WriteLine(string.Format("    Error accessing {0}: {1}", path, ex.Message));
                }
            }
            
            // As a last resort, try to find any folder containing DataLib or SCCM content
            try
            {
                if (_debug)
                    Console.WriteLine("[*] Performing targeted search for SCCM content folders...");
                
                var rootItems = _smbService.ListFiles("");
                foreach (var item in rootItems.Where(i => i.IsDirectory))
                {
                    // Focus on SCCM-related folders
                    if (item.Name.IndexOf("SCCM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Name.IndexOf("SMS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.Name.IndexOf("ContentLib", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            if (_debug)
                                Console.WriteLine(string.Format("[*] Checking SCCM-related folder: {0}", item.Name));
                            
                            var subItems = _smbService.ListFiles(item.Name);
                            if (subItems.Any(i => i.IsDirectory && 
                                (i.Name.Equals("DataLib", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Equals("FileLib", StringComparison.OrdinalIgnoreCase) ||
                                 i.Name.Equals("PkgLib", StringComparison.OrdinalIgnoreCase))))
                            {
                                if (_debug)
                                    Console.WriteLine(string.Format("[+] Found SCCM content library at: {0}", item.Name));
                                return item.Name;
                            }
                            
                            // Check for package content
                            if (subItems.Any(i => i.IsDirectory && i.Name.Length == 8))
                            {
                                // SCCM packages often have 8-character folder names
                                Console.WriteLine(string.Format("[+] Found potential SCCM package folder at: {0}", item.Name));
                                return item.Name;
                            }
                        }
                        catch
                        {
                            // Skip folders we can't access
                        }
                    }
                }
            }
            catch
            {
                // Unable to perform broad search
            }
            
            return null;
        }
    }
}