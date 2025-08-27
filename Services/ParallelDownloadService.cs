using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SCML.Services
{
    /// <summary>
    /// Enhanced download service with parallel processing support
    /// </summary>
    public class ParallelDownloadService
    {
        private readonly bool _debug;
        private readonly bool _preserveFilenames;
        private readonly int _maxParallelDownloads;
        private readonly string _shareName = "SCCMContentLib$";
        
        // Statistics tracking
        private long _totalBytesDownloaded = 0;
        private int _totalFilesDownloaded = 0;
        private int _failedDownloads = 0;
        private DateTime _downloadStartTime;
        private readonly object _statsLock = new object();
        
        // Connection pool for parallel downloads
        private readonly ConcurrentBag<SmbService> _connectionPool;
        private readonly string _serverAddress;
        private readonly string _username;
        private readonly string _password;
        private readonly string _domain;
        private readonly bool _useCurrentUser;

        public ParallelDownloadService(string serverAddress, string username, string password, 
            string domain, bool useCurrentUser, bool debug = false, bool preserveFilenames = false, 
            int maxParallelDownloads = 5)
        {
            _serverAddress = serverAddress;
            _username = username;
            _password = password;
            _domain = domain;
            _useCurrentUser = useCurrentUser;
            _debug = debug;
            _preserveFilenames = preserveFilenames;
            _maxParallelDownloads = Math.Min(Math.Max(1, maxParallelDownloads), 10); // Limit to 1-10
            _connectionPool = new ConcurrentBag<SmbService>();
        }

        /// <summary>
        /// Download files in parallel for improved performance
        /// </summary>
        public void DownloadFilesParallel(string inventoryFile, IEnumerable<string> extensions, string outputDirectory)
        {
            if (!File.Exists(inventoryFile))
            {
                Console.WriteLine(string.Format("[-] Inventory file not found: {0}", inventoryFile));
                return;
            }

            // Create output directory
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                Console.WriteLine(string.Format("[+] Creating {0}", outputDirectory));
            }

            Console.WriteLine(string.Format("[+] Parallel download mode: {0} concurrent connections", _maxParallelDownloads));

            // Build download list
            var downloadList = BuildDownloadList(inventoryFile, extensions);
            
            if (downloadList.Count == 0)
            {
                Console.WriteLine("[-] No files found matching the specified extensions");
                return;
            }

            Console.WriteLine(string.Format("[+] Found {0} files to download", downloadList.Count));
            _downloadStartTime = DateTime.Now;

            // Initialize connection pool
            InitializeConnectionPool();

            // Create download queue
            var downloadQueue = new ConcurrentQueue<KeyValuePair<string, string>>();
            foreach (var item in downloadList)
            {
                downloadQueue.Enqueue(item);
            }

            // Start parallel downloads
            var tasks = new Task[_maxParallelDownloads];
            for (int i = 0; i < _maxParallelDownloads; i++)
            {
                int workerId = i;
                tasks[i] = Task.Run(() => DownloadWorker(workerId, downloadQueue, outputDirectory));
            }

            // Monitor progress
            Task.Run(() => MonitorProgress(downloadList.Count));

            // Wait for all downloads to complete
            Task.WaitAll(tasks);

            // Final statistics
            DisplayFinalStatistics();

            // Cleanup connections
            CleanupConnectionPool();
        }

        private void InitializeConnectionPool()
        {
            Console.WriteLine(string.Format("[*] Initializing {0} SMB connections...", _maxParallelDownloads));
            
            for (int i = 0; i < _maxParallelDownloads; i++)
            {
                try
                {
                    var smbService = new SmbService(_debug);
                    
                    if (_useCurrentUser)
                    {
                        smbService.ConnectWithCurrentUser(_serverAddress, 445);
                    }
                    else
                    {
                        smbService.Connect(_serverAddress, _username, _password, _domain, 445);
                    }

                    if (smbService.ConnectToShare(_shareName))
                    {
                        _connectionPool.Add(smbService);
                        if (_debug)
                            Console.WriteLine(string.Format("[+] Connection {0} established", i + 1));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("[-] Failed to create connection {0}: {1}", i + 1, ex.Message));
                }
            }

            if (_connectionPool.Count == 0)
            {
                throw new Exception("Failed to establish any SMB connections");
            }

            Console.WriteLine(string.Format("[+] Established {0} parallel connections", _connectionPool.Count));
        }

        private void DownloadWorker(int workerId, ConcurrentQueue<KeyValuePair<string, string>> queue, string outputDirectory)
        {
            if (!_connectionPool.TryTake(out SmbService smbService))
            {
                Console.WriteLine(string.Format("[-] Worker {0}: No connection available", workerId));
                return;
            }

            try
            {
                while (queue.TryDequeue(out KeyValuePair<string, string> item))
                {
                    DownloadFileWithConnection(smbService, item.Key, item.Value, outputDirectory, workerId);
                }
            }
            finally
            {
                _connectionPool.Add(smbService); // Return connection to pool
            }
        }

        private void DownloadFileWithConnection(SmbService smbService, string hashValue, string fileName, 
            string outputDirectory, int workerId)
        {
            try
            {
                var targetFileName = _preserveFilenames 
                    ? fileName 
                    : string.Format("{0}-{1}", hashValue.Substring(0, Math.Min(4, hashValue.Length)), fileName);
                var localPath = Path.Combine(outputDirectory, targetFileName);
                
                if (File.Exists(localPath))
                {
                    var existingSize = new FileInfo(localPath).Length;
                    lock (_statsLock)
                    {
                        _totalFilesDownloaded++;
                        _totalBytesDownloaded += existingSize;
                    }
                    if (_debug)
                        Console.WriteLine(string.Format("[Worker {0}] Already exists: {1}", workerId, targetFileName));
                    return;
                }

                var remotePath = string.Format("FileLib\\{0}\\{1}", 
                    hashValue.Substring(0, Math.Min(4, hashValue.Length)), hashValue);
                
                var startTime = DateTime.Now;
                smbService.DownloadFile(remotePath, localPath);
                
                if (File.Exists(localPath))
                {
                    var fileInfo = new FileInfo(localPath);
                    var downloadTime = (DateTime.Now - startTime).TotalSeconds;
                    
                    lock (_statsLock)
                    {
                        _totalFilesDownloaded++;
                        _totalBytesDownloaded += fileInfo.Length;
                    }
                    
                    if (_debug)
                    {
                        var speed = fileInfo.Length / 1024.0 / downloadTime;
                        Console.WriteLine(string.Format("[Worker {0}] Downloaded: {1} ({2:F1} KB/s)", 
                            workerId, targetFileName, speed));
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_statsLock)
                {
                    _failedDownloads++;
                }
                if (_debug)
                    Console.WriteLine(string.Format("[Worker {0}] Error downloading {1}: {2}", 
                        workerId, fileName, ex.Message));
            }
        }

        private void MonitorProgress(int totalFiles)
        {
            while (_totalFilesDownloaded + _failedDownloads < totalFiles)
            {
                Thread.Sleep(5000); // Update every 5 seconds
                
                var elapsed = DateTime.Now - _downloadStartTime;
                var filesPerSec = _totalFilesDownloaded / elapsed.TotalSeconds;
                var mbDownloaded = _totalBytesDownloaded / 1048576.0;
                var mbPerSec = mbDownloaded / elapsed.TotalSeconds;
                
                Console.WriteLine(string.Format("[*] Progress: {0}/{1} files | {2:F1} files/sec | {3:F2} MB ({4:F2} MB/s) | {5} errors",
                    _totalFilesDownloaded, totalFiles, filesPerSec, mbDownloaded, mbPerSec, _failedDownloads));
            }
        }

        private void DisplayFinalStatistics()
        {
            var totalElapsed = DateTime.Now - _downloadStartTime;
            var mbDownloaded = _totalBytesDownloaded / 1048576.0;
            var avgSpeed = mbDownloaded / totalElapsed.TotalSeconds;
            
            Console.WriteLine("\n========================================");
            Console.WriteLine("Download Statistics:");
            Console.WriteLine("========================================");
            Console.WriteLine(string.Format("Total files downloaded: {0}", _totalFilesDownloaded));
            Console.WriteLine(string.Format("Failed downloads: {0}", _failedDownloads));
            Console.WriteLine(string.Format("Total size: {0:F2} MB", mbDownloaded));
            Console.WriteLine(string.Format("Total time: {0:mm\\:ss}", totalElapsed));
            Console.WriteLine(string.Format("Average speed: {0:F2} MB/s", avgSpeed));
            Console.WriteLine(string.Format("Parallel connections used: {0}", _maxParallelDownloads));
            Console.WriteLine("========================================\n");
        }

        private void CleanupConnectionPool()
        {
            while (_connectionPool.TryTake(out SmbService smbService))
            {
                try
                {
                    smbService.Disconnect();
                }
                catch { }
            }
        }

        private Dictionary<string, string> BuildDownloadList(string inventoryFile, IEnumerable<string> extensions)
        {
            var downloadList = new Dictionary<string, string>();
            var lines = File.ReadAllLines(inventoryFile);

            // First pass - collect all INI files to process
            var iniFiles = new List<string>();
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fileExtension = Path.GetExtension(line);
                
                if (extensions.Any(ext => fileExtension.Equals("." + ext, StringComparison.OrdinalIgnoreCase)))
                {
                    var match = Regex.Match(line, @"\\\\[^\\]+\\SCCMContentLib\$\\(.+)");
                    if (match.Success)
                    {
                        var relativePath = match.Groups[1].Value;
                        iniFiles.Add(relativePath + ".INI");
                    }
                }
            }

            // Process INI files to get hashes
            Console.WriteLine(string.Format("[*] Processing {0} INI files for hash resolution...", iniFiles.Count));
            
            // Get first connection for INI processing
            if (_connectionPool.TryTake(out SmbService iniService))
            {
                try
                {
                    foreach (var iniPath in iniFiles)
                    {
                        try
                        {
                            var content = iniService.ReadFile(iniPath);
                            var text = Encoding.UTF8.GetString(content);
                            var hashMatch = Regex.Match(text, @"Hash[^=]*=([A-Z0-9]+)", RegexOptions.IgnoreCase);
                            
                            if (hashMatch.Success)
                            {
                                var hashValue = hashMatch.Groups[1].Value;
                                var fileName = Path.GetFileName(iniPath.Replace(".INI", ""));
                                
                                if (!downloadList.ContainsKey(hashValue))
                                {
                                    downloadList[hashValue] = fileName;
                                }
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    _connectionPool.Add(iniService);
                }
            }

            return downloadList;
        }
    }
}