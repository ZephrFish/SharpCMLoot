using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SMBLibrary;
using SMBLibrary.Client;

namespace SnafflerSCCMAddon
{
    /// <summary>
    /// SCCM INI file indexer - discovers and indexes .INI files in SCCMContentLib$
    /// The SCCM Content Library stores files with hash-based names, with .INI files containing metadata
    /// </summary>
    public class SCCMINIIndexer
    {
        private readonly bool _verbose;
        private readonly List<INIFileEntry> _iniFiles = new List<INIFileEntry>();
        private readonly Dictionary<string, string> _hashToOriginalName = new Dictionary<string, string>();

        public SCCMINIIndexer(bool verbose = false)
        {
            _verbose = verbose;
        }

        /// <summary>
        /// Discover all INI files in SCCMContentLib$ share structure
        /// SCCM stores files in: \\server\SCCMContentLib$\DataLib\{hash}\{hash}.INI
        /// </summary>
        public List<INIFileEntry> IndexSCCMShare(string serverHostname, string username = null,
            string password = null, string domain = null)
        {
            _iniFiles.Clear();
            _hashToOriginalName.Clear();

            Console.WriteLine($"\n[*] Indexing SCCM Content Library: \\\\{serverHostname}\\SCCMContentLib$");

            try
            {
                // Try local filesystem access first (fastest)
                var localPath = $"\\\\{serverHostname}\\SCCMContentLib$";
                if (Directory.Exists(localPath))
                {
                    IndexViaFilesystem(localPath);
                }
                else
                {
                    // Fall back to SMB library (for remote access without mapping)
                    IndexViaSMB(serverHostname, username, password, domain);
                }

                Console.WriteLine($"[+] Indexed {_iniFiles.Count} INI files");
                Console.WriteLine($"[+] Mapped {_hashToOriginalName.Count} content files to original names");

                return _iniFiles;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error indexing SCCM share: {ex.Message}");
                if (_verbose)
                    Console.WriteLine($"    Stack: {ex.StackTrace}");
                return _iniFiles;
            }
        }

        /// <summary>
        /// Index via filesystem (when share is accessible)
        /// </summary>
        private void IndexViaFilesystem(string basePath)
        {
            try
            {
                var dataLibPath = Path.Combine(basePath, "DataLib");

                if (!Directory.Exists(dataLibPath))
                {
                    Console.WriteLine($"[-] DataLib directory not found: {dataLibPath}");
                    return;
                }

                if (_verbose)
                    Console.WriteLine($"[*] Scanning DataLib: {dataLibPath}");

                // SCCM structure: DataLib\{4-char-prefix}\{full-hash}.INI
                var iniFiles = Directory.GetFiles(dataLibPath, "*.INI", SearchOption.AllDirectories);

                Console.WriteLine($"[*] Found {iniFiles.Length} INI files, parsing...");

                foreach (var iniPath in iniFiles)
                {
                    try
                    {
                        var entry = ParseINIFile(iniPath);
                        if (entry != null)
                        {
                            _iniFiles.Add(entry);

                            // Map hash to original filename
                            if (!string.IsNullOrEmpty(entry.OriginalFileName))
                            {
                                var hash = Path.GetFileNameWithoutExtension(iniPath);
                                _hashToOriginalName[hash] = entry.OriginalFileName;
                            }

                            if (_verbose && _iniFiles.Count % 100 == 0)
                                Console.WriteLine($"[*] Processed {_iniFiles.Count} INI files...");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_verbose)
                            Console.WriteLine($"[!] Error parsing {iniPath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Filesystem indexing error: {ex.Message}");
            }
        }

        /// <summary>
        /// Index via SMB library (when share requires authentication)
        /// </summary>
        private void IndexViaSMB(string serverHostname, string username, string password, string domain)
        {
            SMB2Client client = null;
            ISMBFileStore fileStore = null;

            try
            {
                client = new SMB2Client();
                var addresses = System.Net.Dns.GetHostAddresses(serverHostname);
                var connected = client.Connect(addresses[0], SMBTransportType.DirectTCPTransport);

                if (!connected)
                    throw new Exception($"Failed to connect to {serverHostname}");

                // Login
                var status = string.IsNullOrEmpty(username)
                    ? client.Login(string.Empty, string.Empty, string.Empty, AuthenticationMethod.NTLMv2)
                    : client.Login(domain ?? "", username, password);

                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"Authentication failed: {status}");

                // Connect to SCCMContentLib$ share
                fileStore = client.TreeConnect("SCCMContentLib$", out status);
                if (status != NTStatus.STATUS_SUCCESS)
                    throw new Exception($"Failed to connect to SCCMContentLib$: {status}");

                Console.WriteLine($"[+] Connected to \\\\{serverHostname}\\SCCMContentLib$");

                // Enumerate DataLib directory
                IndexDirectoryViaSMB(fileStore, "DataLib", "");
            }
            finally
            {
                fileStore?.Disconnect();
                client?.Disconnect();
            }
        }

        /// <summary>
        /// Recursively index directory via SMB
        /// </summary>
        private void IndexDirectoryViaSMB(ISMBFileStore fileStore, string path, string relativePath)
        {
            try
            {
                object handle;
                FileStatus fileStatus;
                var status = fileStore.CreateFile(out handle, out fileStatus, path,
                    AccessMask.GENERIC_READ, FileAttributes.Directory, ShareAccess.Read,
                    CreateDisposition.FILE_OPEN, CreateOptions.FILE_DIRECTORY_FILE, null);

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    if (_verbose)
                        Console.WriteLine($"[!] Failed to open directory: {path}");
                    return;
                }

                List<QueryDirectoryFileInformation> fileList;
                status = fileStore.QueryDirectory(out fileList, handle, "*", FileInformationClass.FileDirectoryInformation);

                fileStore.CloseFile(handle);

                if (status != NTStatus.STATUS_SUCCESS)
                    return;

                foreach (var fileInfo in fileList)
                {
                    var fileName = fileInfo.FileName;
                    if (fileName == "." || fileName == "..")
                        continue;

                    var fullPath = Path.Combine(path, fileName);
                    var fullRelativePath = Path.Combine(relativePath, fileName);

                    // Check if directory
                    var dirInfo = fileInfo as FileDirectoryInformation;
                    if (dirInfo != null && (dirInfo.FileAttributes & FileAttributes.Directory) != 0)
                    {
                        // Recurse into subdirectory
                        IndexDirectoryViaSMB(fileStore, fullPath, fullRelativePath);
                    }
                    else if (fileName.EndsWith(".INI", StringComparison.OrdinalIgnoreCase))
                    {
                        // Read and parse INI file
                        var content = ReadFileViaSMB(fileStore, fullPath);
                        if (content != null)
                        {
                            var entry = ParseINIContent(fullPath, content);
                            if (entry != null)
                            {
                                _iniFiles.Add(entry);

                                if (_verbose && _iniFiles.Count % 100 == 0)
                                    Console.WriteLine($"[*] Processed {_iniFiles.Count} INI files...");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[!] Error indexing directory {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Read file content via SMB
        /// </summary>
        private byte[] ReadFileViaSMB(ISMBFileStore fileStore, string path)
        {
            try
            {
                object handle;
                FileStatus fileStatus;
                var status = fileStore.CreateFile(out handle, out fileStatus, path,
                    AccessMask.GENERIC_READ | AccessMask.SYNCHRONIZE, FileAttributes.Normal,
                    ShareAccess.Read, CreateDisposition.FILE_OPEN,
                    CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_ALERT, null);

                if (status != NTStatus.STATUS_SUCCESS)
                    return null;

                using (var stream = new MemoryStream())
                {
                    long offset = 0;
                    while (true)
                    {
                        byte[] data;
                        status = fileStore.ReadFile(out data, handle, offset, 4096);

                        if (status != NTStatus.STATUS_SUCCESS || data.Length == 0)
                            break;

                        stream.Write(data, 0, data.Length);
                        offset += data.Length;
                    }

                    fileStore.CloseFile(handle);
                    return stream.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Parse INI file from disk
        /// </summary>
        private INIFileEntry ParseINIFile(string iniPath)
        {
            try
            {
                var content = File.ReadAllText(iniPath, Encoding.UTF8);
                return ParseINIContent(iniPath, Encoding.UTF8.GetBytes(content));
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[!] Failed to read {iniPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse INI file content
        /// SCCM INI format:
        /// [File]
        /// Name=original_filename.ext
        /// Size=12345
        /// Hash=SHA256_HASH
        /// </summary>
        private INIFileEntry ParseINIContent(string iniPath, byte[] content)
        {
            try
            {
                var text = Encoding.UTF8.GetString(content);
                var entry = new INIFileEntry
                {
                    INIFilePath = iniPath,
                    Hash = Path.GetFileNameWithoutExtension(iniPath)
                };

                // Parse INI content
                var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.OriginalFileName = line.Substring(5).Trim();
                    }
                    else if (line.StartsWith("Size=", StringComparison.OrdinalIgnoreCase))
                    {
                        long.TryParse(line.Substring(5).Trim(), out var size);
                        entry.FileSize = size;
                    }
                    else if (line.StartsWith("Hash=", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.ContentHash = line.Substring(5).Trim();
                    }
                    else if (line.StartsWith("Version=", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.Version = line.Substring(8).Trim();
                    }
                }

                // Build full content file path (remove .INI extension)
                entry.ContentFilePath = iniPath.Replace(".INI", "");

                return entry;
            }
            catch (Exception ex)
            {
                if (_verbose)
                    Console.WriteLine($"[!] Failed to parse INI content: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Export indexed INI files to Snaffler-compatible target list
        /// </summary>
        public void ExportForSnaffler(string outputPath)
        {
            try
            {
                Console.WriteLine($"\n[*] Exporting {_iniFiles.Count} targets for Snaffler...");

                using (var writer = new StreamWriter(outputPath, false))
                {
                    // Write header comment
                    writer.WriteLine("# SCCM Content Library INI Files");
                    writer.WriteLine($"# Generated: {DateTime.Now}");
                    writer.WriteLine($"# Total files: {_iniFiles.Count}");
                    writer.WriteLine();

                    foreach (var entry in _iniFiles.OrderBy(e => e.OriginalFileName))
                    {
                        // Write INI file path as UNC
                        writer.WriteLine(entry.INIFilePath);
                    }
                }

                Console.WriteLine($"[+] Exported to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Export with original filenames for better analysis
        /// </summary>
        public void ExportWithMetadata(string outputPath)
        {
            try
            {
                Console.WriteLine($"\n[*] Exporting indexed files with metadata...");

                using (var writer = new StreamWriter(outputPath, false))
                {
                    // CSV format
                    writer.WriteLine("INIPath,ContentPath,OriginalFileName,FileSize,Hash,Version");

                    foreach (var entry in _iniFiles.OrderBy(e => e.OriginalFileName))
                    {
                        writer.WriteLine($"\"{entry.INIFilePath}\",\"{entry.ContentFilePath}\"," +
                            $"\"{entry.OriginalFileName}\",{entry.FileSize}," +
                            $"\"{entry.ContentHash}\",\"{entry.Version}\"");
                    }
                }

                Console.WriteLine($"[+] Metadata exported to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Metadata export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Filter INI files by extension or pattern
        /// </summary>
        public List<INIFileEntry> FilterByExtension(string[] extensions)
        {
            return _iniFiles.Where(e =>
            {
                var ext = Path.GetExtension(e.OriginalFileName)?.TrimStart('.');
                return extensions.Any(allowed => allowed.Equals(ext, StringComparison.OrdinalIgnoreCase));
            }).ToList();
        }

        /// <summary>
        /// Filter by original filename pattern
        /// </summary>
        public List<INIFileEntry> FilterByPattern(string pattern)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return _iniFiles.Where(e => regex.IsMatch(e.OriginalFileName ?? "")).ToList();
        }

        /// <summary>
        /// Get statistics about indexed files
        /// </summary>
        public void PrintStatistics()
        {
            Console.WriteLine("\n=== SCCM Index Statistics ===");
            Console.WriteLine($"Total INI files: {_iniFiles.Count}");
            Console.WriteLine($"Total size: {_iniFiles.Sum(e => e.FileSize) / 1024 / 1024} MB");

            var extensionGroups = _iniFiles
                .GroupBy(e => Path.GetExtension(e.OriginalFileName)?.ToLower() ?? "no-ext")
                .OrderByDescending(g => g.Count())
                .Take(10);

            Console.WriteLine("\nTop 10 file types:");
            foreach (var group in extensionGroups)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()} files");
            }

            Console.WriteLine("=============================\n");
        }

        /// <summary>
        /// Get all indexed entries
        /// </summary>
        public List<INIFileEntry> GetIndexedFiles() => _iniFiles;
    }

    /// <summary>
    /// Represents an indexed SCCM INI file entry
    /// </summary>
    public class INIFileEntry
    {
        public string INIFilePath { get; set; }           // Path to .INI file
        public string ContentFilePath { get; set; }       // Path to actual content file (without .INI)
        public string OriginalFileName { get; set; }      // Original filename from INI
        public long FileSize { get; set; }                // File size in bytes
        public string ContentHash { get; set; }           // SHA256 hash from INI
        public string Hash { get; set; }                  // SCCM content hash (filename)
        public string Version { get; set; }               // Version from INI

        public override string ToString()
        {
            return $"{OriginalFileName} ({FileSize} bytes) -> {INIFilePath}";
        }
    }
}
