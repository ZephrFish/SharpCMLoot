using System;
using System.Collections.Generic;

namespace MockSCCMServer.Models
{
    public class MockPackage
    {
        public string PackageId { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public List<MockFile> Files { get; set; }
        public DateTime CreatedDate { get; set; }

        public MockPackage()
        {
            Files = new List<MockFile>();
            CreatedDate = DateTime.Now.AddDays(-new Random().Next(1, 365));
        }
    }

    public class MockFile
    {
        public string Name { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public byte[] Content { get; set; }
        public string Hash { get; set; }
        public FileSensitivity Sensitivity { get; set; }
        public DateTime LastModified { get; set; }

        public MockFile()
        {
            LastModified = DateTime.Now.AddDays(-new Random().Next(1, 180));
        }
    }

    public enum FileSensitivity
    {
        Green,
        Yellow,
        Red,
        Black
    }

    public static class MockSCCMData
    {
        public static readonly Dictionary<string, string[]> PackageTypes = new Dictionary<string, string[]>
        {
            ["Microsoft Office"] = new[] { "setup.exe", "config.xml", "office.msp", "unattend.xml" },
            ["Windows 10 Deployment"] = new[] { "install.wim", "unattend.xml", "setupcomplete.cmd", "autologon.xml" },
            ["Antivirus Updates"] = new[] { "update.exe", "signatures.dat", "config.ini", "install.bat" },
            ["Custom Applications"] = new[] { "install.msi", "transform.mst", "config.xml", "deploy.ps1" },
            ["Driver Packages"] = new[] { "driver.inf", "driver.sys", "install.bat", "setup.exe" },
            ["Security Updates"] = new[] { "update.msu", "install.ps1", "registry.reg", "config.xml" }
        };

        public static readonly Dictionary<string, FileSensitivity> SensitiveFiles = new Dictionary<string, FileSensitivity>
        {
            // BLACK - Critical sensitivity
            ["passwords.txt"] = FileSensitivity.Black,
            ["credentials.xml"] = FileSensitivity.Black,
            ["service_account.txt"] = FileSensitivity.Black,
            ["admin_password.txt"] = FileSensitivity.Black,
            ["private.key"] = FileSensitivity.Black,
            ["id_rsa"] = FileSensitivity.Black,
            
            // RED - High sensitivity
            ["unattend.xml"] = FileSensitivity.Red,
            ["autologon.xml"] = FileSensitivity.Red,
            ["config.xml"] = FileSensitivity.Red,
            ["connection_strings.config"] = FileSensitivity.Red,
            ["database.config"] = FileSensitivity.Red,
            ["web.config"] = FileSensitivity.Red,
            
            // YELLOW - Medium sensitivity
            ["deploy.ps1"] = FileSensitivity.Yellow,
            ["install.ps1"] = FileSensitivity.Yellow,
            ["setup.bat"] = FileSensitivity.Yellow,
            ["registry.reg"] = FileSensitivity.Yellow,
            ["certificate.cer"] = FileSensitivity.Yellow,
            
            // GREEN - Low sensitivity (default)
        };

        public static readonly string[] CommonExtensions = new[]
        {
            ".xml", ".ini", ".config", ".ps1", ".bat", ".cmd", ".reg", ".txt",
            ".exe", ".msi", ".msu", ".msp", ".wim", ".inf", ".sys", ".cer",
            ".pfx", ".key", ".pem", ".log", ".dat", ".bin", ".cab", ".zip"
        };
    }
}