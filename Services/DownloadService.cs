using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SCML.Services
{
    public class DownloadService
    {
        private readonly SmbService _smbService;
        private readonly bool _debug;
        private readonly bool _preserveFilenames;
        private readonly string _shareName = "SCCMContentLib$";
        private long _totalBytesDownloaded = 0;
        private int _totalFilesDownloaded = 0;
        private DateTime _downloadStartTime;

        public DownloadService(SmbService smbService, bool debug = false, bool preserveFilenames = false)
        {
            _smbService = smbService;
            _debug = debug;
            _preserveFilenames = preserveFilenames;
        }

        public void DownloadFiles(string inventoryFile, IEnumerable<string> extensions, string outputDirectory)
        {
            if (!File.Exists(inventoryFile))
            {
                Console.WriteLine(string.Format("[-] Inventory file not found: {0}", inventoryFile));
                return;
            }

            // Connect to share
            if (!_smbService.ConnectToShare(_shareName))
            {
                throw new Exception(string.Format("Failed to access {0} share", _shareName));
            }

            Console.WriteLine(string.Format("[+] Extensions to download: {0}", string.Join(", ", extensions)));

            // Create output directory
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                Console.WriteLine(string.Format("[+] Creating {0}", outputDirectory));
            }
            else
            {
                Console.WriteLine(string.Format("[+] Using existing directory: {0}", outputDirectory));
            }

            // Build download list
            var downloadList = BuildDownloadList(inventoryFile, extensions);
            
            if (downloadList.Count == 0)
            {
                Console.WriteLine("[-] No files found matching the specified extensions");
                return;
            }

            Console.WriteLine(string.Format("[+] Found {0} files to download", downloadList.Count));
            _downloadStartTime = DateTime.Now;

            // Download files with progress tracking
            int currentFile = 0;
            int totalFiles = downloadList.Count;
            
            foreach (var item in downloadList)
            {
                currentFile++;
                Console.WriteLine(string.Format("\n[*] Downloading file {0}/{1}...", currentFile, totalFiles));
                DownloadFile(item.Key, item.Value, outputDirectory);
                
                // Display progress
                if (currentFile % 10 == 0 || currentFile == totalFiles)
                {
                    var elapsed = DateTime.Now - _downloadStartTime;
                    var rate = _totalFilesDownloaded / elapsed.TotalSeconds;
                    Console.WriteLine(string.Format("[*] Progress: {0}/{1} files | {2:F1} files/sec | {3:F2} MB downloaded",
                        _totalFilesDownloaded, totalFiles, rate, _totalBytesDownloaded / 1048576.0));
                }
            }
            
            // Final summary
            var totalElapsed = DateTime.Now - _downloadStartTime;
            Console.WriteLine(string.Format("\n[+] Download complete: {0} files ({1:F2} MB) in {2:mm\\:ss}",
                _totalFilesDownloaded, _totalBytesDownloaded / 1048576.0, totalElapsed));
        }

        private Dictionary<string, string> BuildDownloadList(string inventoryFile, IEnumerable<string> extensions)
        {
            var downloadList = new Dictionary<string, string>();
            var lines = File.ReadAllLines(inventoryFile);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fileExtension = Path.GetExtension(line);
                
                // Check if file matches any of the requested extensions
                if (extensions.Any(ext => fileExtension.Equals("." + ext, StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        // Extract the path from the inventory line
                        var match = Regex.Match(line, @"\\\\[^\\]+\\SCCMContentLib\$\\(.+)");
                        if (match.Success)
                        {
                            var relativePath = match.Groups[1].Value;
                            var iniPath = relativePath + ".INI";
                            
                            // Read the INI file to get the hash
                            var hashValue = GetHashFromIniFile(iniPath);
                            
                            if (!string.IsNullOrEmpty(hashValue))
                            {
                                var fileName = Path.GetFileName(relativePath);
                                downloadList[hashValue] = fileName;
                                
                                if (_debug)
                                    Console.WriteLine(string.Format("[+] Queued for download: {0} (Hash: {1})", fileName, hashValue));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_debug)
                            Console.WriteLine(string.Format("[-] Error processing {0}: {1}", line, ex.Message));
                    }
                }
            }

            return downloadList;
        }

        private string GetHashFromIniFile(string iniPath)
        {
            try
            {
                var content = _smbService.ReadFile(iniPath);
                var text = Encoding.UTF8.GetString(content);
                
                // Extract hash value from INI content
                var hashMatch = Regex.Match(text, @"Hash[^=]*=([A-Z0-9]+)", RegexOptions.IgnoreCase);
                
                if (hashMatch.Success)
                {
                    return hashMatch.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Error reading INI file {0}: {1}", iniPath, ex.Message));
            }

            return string.Empty;
        }

        private void DownloadFile(string hashValue, string fileName, string outputDirectory)
        {
            try
            {
                // Files are stored in FileLib\<first4chars>\<fullhash>
                // Use original filename or add hash prefix based on user preference
                var targetFileName = _preserveFilenames 
                    ? fileName 
                    : string.Format("{0}-{1}", hashValue.Substring(0, Math.Min(4, hashValue.Length)), fileName);
                var localPath = Path.Combine(outputDirectory, targetFileName);
                
                if (File.Exists(localPath))
                {
                    var existingSize = new FileInfo(localPath).Length;
                    Console.WriteLine(string.Format("[+] Already downloaded: {0} ({1:F2} KB)", targetFileName, existingSize / 1024.0));
                    _totalFilesDownloaded++;
                    _totalBytesDownloaded += existingSize;
                    return;
                }

                var remotePath = string.Format("FileLib\\{0}\\{1}", hashValue.Substring(0, Math.Min(4, hashValue.Length)), hashValue);
                
                // Download with size tracking
                var startTime = DateTime.Now;
                _smbService.DownloadFile(remotePath, localPath);
                
                if (File.Exists(localPath))
                {
                    var fileInfo = new FileInfo(localPath);
                    var downloadTime = (DateTime.Now - startTime).TotalSeconds;
                    var speed = fileInfo.Length / 1024.0 / downloadTime; // KB/s
                    
                    Console.WriteLine(string.Format("[+] Downloaded: {0} ({1:F2} KB in {2:F1}s - {3:F1} KB/s)",
                        targetFileName, fileInfo.Length / 1024.0, downloadTime, speed));
                    
                    _totalFilesDownloaded++;
                    _totalBytesDownloaded += fileInfo.Length;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("[-] Error downloading {0}: {1}", fileName, ex.Message));
                if (_debug)
                    Console.WriteLine(ex.StackTrace);
            }
        }
    }
}