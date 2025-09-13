using SMBLibrary;
using SMBLibrary.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace SCML.Services
{
    public class SmbService : IDisposable
    {
        private SMB2Client _client;
        private ISMBFileStore _fileStore;
        private readonly bool _debug;
        private bool _connected;
        private string _lastAuthenticationTarget;
        private DateTime _lastAuthenticationAttempt;
        private DateTime _lastActivity;
        private string _currentShareName;
        private const int KeepAliveIntervalSeconds = 30;

        public SmbService(bool debug = false)
        {
            _debug = debug;
        }

        private void ApplyAuthenticationDelay(string address)
        {
            // Safety check: prevent rapid authentication attempts to same target
            if (_lastAuthenticationTarget == address && 
                DateTime.Now.Subtract(_lastAuthenticationAttempt).TotalSeconds < 5)
            {
                Console.WriteLine(string.Format("[!] Delaying authentication to {0} to prevent lockout...", address));
                Thread.Sleep(5000);  // 5 second delay
            }
            
            _lastAuthenticationTarget = address;
            _lastAuthenticationAttempt = DateTime.Now;
        }

        public void Connect(string address, string username, string password, string domain, int port)
        {
            ApplyAuthenticationDelay(address);
            
            try
            {
                _client = new SMB2Client();
                
                if (_debug)
                    Console.WriteLine(string.Format("[*] Attempting to connect to {0}:{1}...", address, port));
                
                // Get IP address
                string ipAddress = GetIPAddress(address);
                if (_debug)
                    Console.WriteLine(string.Format("[*] Resolved {0} to {1}", address, ipAddress));
                
                bool isConnected = _client.Connect(IPAddress.Parse(ipAddress), port == 139 ? SMBTransportType.NetBiosOverTCP : SMBTransportType.DirectTCPTransport);
                
                if (!isConnected)
                {
                    throw new Exception(string.Format("Failed to connect to {0}:{1}", address, port));
                }

                if (_debug)
                    Console.WriteLine(string.Format("[*] Attempting authentication to {0} as {1}\\{2}...", address, domain, username));

                NTStatus status = _client.Login(domain ?? string.Empty, username ?? string.Empty, password ?? string.Empty);
                
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    // Log authentication failure for monitoring
                    Console.WriteLine(string.Format("[!] Authentication failed to {0}: {1}", address, status));
                    
                    // Provide specific guidance based on status
                    if (status == NTStatus.STATUS_ACCOUNT_LOCKED_OUT)
                    {
                        throw new Exception(string.Format("Account is locked out. Wait for lockout duration to expire before retrying."));
                    }
                    else if (status == NTStatus.STATUS_LOGON_FAILURE)
                    {
                        throw new Exception(string.Format("Invalid credentials. Status: {0}", status));
                    }
                    else
                    {
                        throw new Exception(string.Format("Login failed: {0}", status));
                    }
                }

                _connected = true;
                if (_debug)
                    Console.WriteLine(string.Format("[+] Connected to {0} as {1}\\{2}", address, domain, username));
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Connection failed: {0}", ex.Message), ex);
            }
        }

        public void ConnectWithCurrentUser(string address, int port)
        {
            ApplyAuthenticationDelay(address);
            
            try
            {
                _client = new SMB2Client();
                
                if (_debug)
                    Console.WriteLine(string.Format("[*] Attempting to connect to {0}:{1} with current user...", address, port));
                
                // Get IP address
                string ipAddress = GetIPAddress(address);
                if (_debug)
                    Console.WriteLine(string.Format("[*] Resolved {0} to {1}", address, ipAddress));
                
                bool isConnected = _client.Connect(IPAddress.Parse(ipAddress), port == 139 ? SMBTransportType.NetBiosOverTCP : SMBTransportType.DirectTCPTransport);
                
                if (!isConnected)
                {
                    throw new Exception(string.Format("Failed to connect to {0}:{1}", address, port));
                }

                if (_debug)
                    Console.WriteLine(string.Format("[*] Attempting authentication to {0} with current user...", address));

                // Use integrated Windows authentication
                NTStatus status = _client.Login(string.Empty, string.Empty, string.Empty, AuthenticationMethod.NTLMv2);
                
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    // Log authentication failure for monitoring
                    Console.WriteLine(string.Format("[!] Authentication failed to {0}: {1}", address, status));
                    
                    // Provide specific guidance based on status
                    if (status == NTStatus.STATUS_ACCESS_DENIED)
                    {
                        Console.WriteLine("    Current user does not have access to this resource.");
                        Console.WriteLine("    Try providing explicit credentials with --username and --password");
                        throw new Exception(string.Format("Access denied for current user. Status: {0}", status));
                    }
                    else if (status == NTStatus.STATUS_ACCOUNT_LOCKED_OUT)
                    {
                        throw new Exception(string.Format("Account is locked out. Wait for lockout duration to expire before retrying."));
                    }
                    else if (status == NTStatus.STATUS_LOGON_FAILURE)
                    {
                        Console.WriteLine("    Current user authentication failed.");
                        Console.WriteLine("    Ensure you are running from a domain-joined machine or provide credentials.");
                        throw new Exception(string.Format("Login failed for current user: {0}", status));
                    }
                    else
                    {
                        throw new Exception(string.Format("Login with current user failed: {0}", status));
                    }
                }

                _connected = true;
                if (_debug)
                    Console.WriteLine(string.Format("[+] Connected to {0} with current user credentials", address));
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Connection failed: {0}", ex.Message), ex);
            }
        }

        public bool ConnectToShare(string shareName)
        {
            if (!_connected || _client == null)
                throw new InvalidOperationException("Not connected to server");

            // Validate share accessibility first
            if (!ValidateShareAccess(shareName))
            {
                Console.WriteLine(string.Format("[-] Share validation failed for: {0}", shareName));
                return false;
            }

            _fileStore = _client.TreeConnect(shareName, out NTStatus status);
            
            if (status != NTStatus.STATUS_SUCCESS)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Failed to connect to share {0}: {1}", shareName, status));
                
                // Provide helpful error messages based on status
                if (status == NTStatus.STATUS_ACCESS_DENIED)
                {
                    Console.WriteLine("    Access denied. Check your credentials and share permissions.");
                    Console.WriteLine("    Ensure the account has access to SCCMContentLib$ share.");
                }
                else if (status == NTStatus.STATUS_BAD_NETWORK_NAME)
                {
                    Console.WriteLine("    Share not found. Verify SCCM is properly configured on this server.");
                    Console.WriteLine("    Expected share: SCCMContentLib$");
                }
                else if (status == NTStatus.STATUS_PATH_NOT_COVERED)
                {
                    Console.WriteLine("    The share path is not available. Check SCCM installation.");
                }
                
                return false;
            }

            if (_debug)
                Console.WriteLine(string.Format("[+] Connected to share: {0}", shareName));
            
            _currentShareName = shareName;
            _lastActivity = DateTime.Now;
            return true;
        }

        public List<string> ListShares()
        {
            if (!_connected || _client == null)
                throw new InvalidOperationException("Not connected to server");
            
            var shareList = new List<string>();
            
            try
            {
                var shares = _client.ListShares(out NTStatus status);
                
                if (status == NTStatus.STATUS_SUCCESS && shares != null)
                {
                    foreach (var share in shares)
                    {
                        shareList.Add(share);
                    }
                }
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Error listing shares: {0}", ex.Message));
            }
            
            return shareList;
        }

        private bool ValidateShareAccess(string shareName)
        {
            try
            {
                // Quick validation by attempting to list shares
                var shares = _client.ListShares(out NTStatus status);
                
                if (status != NTStatus.STATUS_SUCCESS)
                {
                    if (_debug)
                        Console.WriteLine(string.Format("[!] Unable to enumerate shares: {0}", status));
                    return true; // Continue anyway, might be restricted
                }

                // Check if our target share exists
                bool shareFound = false;
                if (shares != null)
                {
                    foreach (var share in shares)
                    {
                        if (share.Equals(shareName.TrimEnd('$'), StringComparison.OrdinalIgnoreCase) ||
                            share.Equals(shareName, StringComparison.OrdinalIgnoreCase))
                        {
                            shareFound = true;
                            if (_debug)
                                Console.WriteLine(string.Format("[+] Found target share: {0}", share));
                            break;
                        }
                    }
                }

                if (!shareFound && _debug)
                {
                    Console.WriteLine(string.Format("[!] Warning: Share '{0}' not found in enumeration", shareName));
                    if (shares != null && shares.Count > 0)
                    {
                        Console.WriteLine("    Available shares:");
                        foreach (var share in shares)
                        {
                            // Show all shares including hidden ones for debugging
                            Console.WriteLine(string.Format("      - {0}", share));
                            
                            // Check for SCCM-related shares
                            if (share.IndexOf("SCCM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                share.IndexOf("ContentLib", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                share.IndexOf("SMS", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                Console.WriteLine(string.Format("        [*] Potential SCCM share detected: {0}", share));
                            }
                        }
                    }
                }

                return true; // Still attempt connection even if not enumerated
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[!] Share validation exception: {0}", ex.Message));
                return true; // Don't block on validation failure
            }
        }

        public List<FileItem> ListFiles(string path)
        {
            if (_fileStore == null)
                throw new InvalidOperationException("Not connected to share");
            
            // Check connection health and reconnect if needed
            if (DateTime.Now.Subtract(_lastActivity).TotalSeconds > KeepAliveIntervalSeconds)
            {
                if (!IsConnectionAlive())
                {
                    if (_debug)
                        Console.WriteLine("[*] Connection lost during listing, attempting to reconnect...");
                    ReconnectToShare();
                }
            }
            _lastActivity = DateTime.Now;

            var files = new List<FileItem>();
            
            NTStatus status = _fileStore.CreateFile(out object handle, out _, path,
                AccessMask.GENERIC_READ, 
                SMBLibrary.FileAttributes.Directory,
                ShareAccess.Read | ShareAccess.Write,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] Failed to open directory {0}: {1}", path, status));
                return files;
            }

            _fileStore.QueryDirectory(out List<QueryDirectoryFileInformation> fileList, handle, "*", FileInformationClass.FileDirectoryInformation);
            
            foreach (var item in fileList)
            {
                if (item is FileDirectoryInformation info)
                {
                    if (info.FileName != "." && info.FileName != "..")
                    {
                        files.Add(new FileItem
                        {
                            Name = info.FileName,
                            IsDirectory = (info.FileAttributes & SMBLibrary.FileAttributes.Directory) != 0,
                            Size = info.EndOfFile
                        });
                    }
                }
            }

            _fileStore.CloseFile(handle);
            return files;
        }

        public byte[] ReadFile(string path)
        {
            if (_fileStore == null)
                throw new InvalidOperationException("Not connected to share");
            
            // Check if connection might have timed out and reconnect if needed
            if (DateTime.Now.Subtract(_lastActivity).TotalSeconds > KeepAliveIntervalSeconds)
            {
                if (!IsConnectionAlive())
                {
                    if (_debug)
                        Console.WriteLine("[*] Connection lost, attempting to reconnect...");
                    ReconnectToShare();
                }
            }
            _lastActivity = DateTime.Now;

            NTStatus status = _fileStore.CreateFile(out object handle, out _, path,
                AccessMask.GENERIC_READ,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read,
                CreateDisposition.FILE_OPEN,
                CreateOptions.FILE_NON_DIRECTORY_FILE,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new Exception(string.Format("Failed to open file {0}: {1}", path, status));
            }

            _fileStore.GetFileInformation(out FileInformation fileInfo, handle, FileInformationClass.FileStandardInformation);
            var standardInfo = (FileStandardInformation)fileInfo;
            
            var buffer = new byte[standardInfo.EndOfFile];
            _fileStore.ReadFile(out byte[] data, handle, 0, (int)standardInfo.EndOfFile);
            
            _fileStore.CloseFile(handle);
            _lastActivity = DateTime.Now;
            return data ?? new byte[0];
        }
        
        private bool IsConnectionAlive()
        {
            try
            {
                if (_client == null || !_connected)
                    return false;
                    
                // Try a simple operation to check if connection is still alive
                var shares = _client.ListShares(out NTStatus status);
                return status == NTStatus.STATUS_SUCCESS;
            }
            catch
            {
                return false;
            }
        }
        
        private void ReconnectToShare()
        {
            if (string.IsNullOrEmpty(_currentShareName))
                throw new InvalidOperationException("No share name stored for reconnection");
                
            // Try to reconnect to the share
            int retryCount = 0;
            while (retryCount < 3)
            {
                try
                {
                    // Re-establish filestore connection
                    _fileStore = _client.TreeConnect(_currentShareName, out NTStatus status);
                    if (status == NTStatus.STATUS_SUCCESS)
                    {
                        if (_debug)
                            Console.WriteLine($"[+] Successfully reconnected to share: {_currentShareName}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    if (_debug)
                        Console.WriteLine($"[-] Reconnection attempt {retryCount + 1} failed: {ex.Message}");
                }
                
                retryCount++;
                if (retryCount < 3)
                    Thread.Sleep(1000);
            }
            
            throw new Exception($"Failed to reconnect to share {_currentShareName} after 3 attempts");
        }

        public void DownloadFile(string remotePath, string localPath)
        {
            var data = ReadFile(remotePath);
            File.WriteAllBytes(localPath, data);
        }

        public void Disconnect()
        {
            try
            {
                if (_fileStore != null)
                {
                    // FileStore will be cleaned up when client disconnects
                    _fileStore = null;
                }

                if (_client != null)
                {
                    try
                    {
                        _client.Logoff();
                    }
                    catch (Exception)
                    {
                        // Ignore logoff errors - may already be logged off
                    }
                    
                    try
                    {
                        _client.Disconnect();
                    }
                    catch (Exception)
                    {
                        // Ignore disconnect errors
                    }
                    
                    _client = null;
                }
            }
            finally
            {
                _connected = false;
            }
        }

        private string GetIPAddress(string hostname)
        {
            try
            {
                IPAddress address;
                if (IPAddress.TryParse(hostname, out address))
                {
                    if (_debug)
                        Console.WriteLine(string.Format("[*] {0} is already an IP address", hostname));
                    return hostname;
                }

                if (_debug)
                    Console.WriteLine(string.Format("[*] Resolving hostname {0}...", hostname));
                
                var hostEntry = Dns.GetHostEntry(hostname);
                if (hostEntry.AddressList.Length > 0)
                {
                    // Prefer IPv4 addresses
                    var ipv4 = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ipv4 != null)
                    {
                        if (_debug)
                            Console.WriteLine(string.Format("[+] Resolved {0} to IPv4 address {1}", hostname, ipv4.ToString()));
                        return ipv4.ToString();
                    }
                    
                    // Fall back to first available address
                    var firstAddr = hostEntry.AddressList[0].ToString();
                    if (_debug)
                        Console.WriteLine(string.Format("[+] Resolved {0} to {1}", hostname, firstAddr));
                    return firstAddr;
                }

                throw new Exception(string.Format("Unable to resolve hostname: {0}", hostname));
            }
            catch (Exception ex)
            {
                if (_debug)
                    Console.WriteLine(string.Format("[-] DNS resolution failed for {0}: {1}", hostname, ex.Message));
                throw new Exception(string.Format("Failed to resolve {0}: {1}", hostname, ex.Message));
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }

    public class FileItem
    {
        public string Name { get; set; }
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
    }
}