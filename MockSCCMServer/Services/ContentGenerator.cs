using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MockSCCMServer.Models;

namespace MockSCCMServer.Services
{
    public class ContentGenerator
    {
        private readonly string _rootPath;
        private readonly Random _random;

        public ContentGenerator(string rootPath)
        {
            _rootPath = rootPath;
            _random = new Random();
        }

        public void GenerateContent()
        {
            // Create base directory structure
            CreateDirectoryStructure();
            
            // Generate packages
            GeneratePackages();
            
            // Generate sensitive test files
            GenerateSensitiveTestFiles();
            
            Console.WriteLine("[+] Generated realistic SCCM content structure");
        }

        private void CreateDirectoryStructure()
        {
            // Create main SCCM directory structure
            var directories = new[]
            {
                "SCCMContentLib$",
                "SCCMContentLib$/DataLib",
                "SCCMContentLib$/FileLib", 
                "SCCMContentLib$/PkgLib",
                "SMSPKGD$",
                "ADMIN$/SCCMContentLib",
                "ADMIN$/Program Files/Microsoft Configuration Manager",
                "ADMIN$/Windows/Temp/SCCM"
            };

            foreach (var dir in directories)
            {
                var fullPath = Path.Combine(_rootPath, dir);
                Directory.CreateDirectory(fullPath);
            }
        }

        private void GeneratePackages()
        {
            var packageCount = 0;
            
            foreach (var packageType in MockSCCMData.PackageTypes)
            {
                for (int i = 0; i < _random.Next(2, 5); i++)
                {
                    packageCount++;
                    var packageId = GeneratePackageId();
                    
                    // Create package in DataLib (SCCM's hash-based storage)
                    var dataLibPath = Path.Combine(_rootPath, "SCCMContentLib$", "DataLib", packageId);
                    Directory.CreateDirectory(dataLibPath);
                    
                    // Create package in SMSPKGD$ (traditional package storage)
                    var pkgPath = Path.Combine(_rootPath, "SMSPKGD$", $"{packageType.Key}_{i:D2}");
                    Directory.CreateDirectory(pkgPath);
                    
                    GeneratePackageFiles(dataLibPath, pkgPath, packageType.Key, packageType.Value);
                }
            }
            
            Console.WriteLine($"[+] Generated {packageCount} mock SCCM packages");
        }

        private void GeneratePackageFiles(string dataLibPath, string pkgPath, string packageName, string[] fileTemplates)
        {
            foreach (var template in fileTemplates)
            {
                // Generate file in DataLib (preserving original extension for testing)
                var extension = Path.GetExtension(template);
                var dataLibFile = Path.Combine(dataLibPath, GenerateHash() + extension);
                
                // Generate file in package directory
                var pkgFile = Path.Combine(pkgPath, template);
                
                // Generate file content based on sensitivity
                var sensitivity = MockSCCMData.SensitiveFiles.ContainsKey(template.ToLower()) 
                    ? MockSCCMData.SensitiveFiles[template.ToLower()]
                    : FileSensitivity.Green;
                
                var content = GenerateFileContent(template, packageName, sensitivity);
                
                // Write files
                File.WriteAllBytes(dataLibFile, content);
                File.WriteAllBytes(pkgFile, content);
            }
        }

        private void GenerateSensitiveTestFiles()
        {
            Console.WriteLine("[*] Generating sensitive test files for security testing...");
            
            var testFilesPath = Path.Combine(_rootPath, "SCCMContentLib$", "DataLib", "TEST_SENSITIVE");
            Directory.CreateDirectory(testFilesPath);
            
            // Generate files for each sensitivity level
            var testFiles = new Dictionary<string, FileSensitivity>
            {
                // BLACK level - Critical
                ["admin_credentials.txt"] = FileSensitivity.Black,
                ["service_passwords.xml"] = FileSensitivity.Black,
                ["database_connection.config"] = FileSensitivity.Black,
                ["private_key.pem"] = FileSensitivity.Black,
                
                // RED level - High
                ["deployment_unattend.xml"] = FileSensitivity.Red,
                ["app_config.xml"] = FileSensitivity.Red,
                ["connection_strings.config"] = FileSensitivity.Red,
                
                // YELLOW level - Medium
                ["install_script.ps1"] = FileSensitivity.Yellow,
                ["registry_changes.reg"] = FileSensitivity.Yellow,
                ["certificate.cer"] = FileSensitivity.Yellow,
                
                // GREEN level - Low
                ["readme.txt"] = FileSensitivity.Green,
                ["version_info.txt"] = FileSensitivity.Green
            };
            
            foreach (var testFile in testFiles)
            {
                var extension = Path.GetExtension(testFile.Key);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".txt"; // Default extension for files without one
                }
                var filePath = Path.Combine(testFilesPath, GenerateHash() + extension);
                var content = GenerateFileContent(testFile.Key, "TEST_PACKAGE", testFile.Value);
                File.WriteAllBytes(filePath, content);
            }
            
            Console.WriteLine($"[+] Generated {testFiles.Count} sensitive test files");
        }

        private byte[] GenerateFileContent(string fileName, string packageName, FileSensitivity sensitivity)
        {
            var content = new StringBuilder();
            var extension = Path.GetExtension(fileName).ToLower();
            
            // Add realistic content based on file type and sensitivity
            switch (extension)
            {
                case ".xml":
                    content.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                    if (fileName.Contains("unattend"))
                    {
                        content.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");
                        content.AppendLine("  <settings pass=\"specialize\">");
                        content.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\">");
                        if (sensitivity >= FileSensitivity.Red)
                        {
                            content.AppendLine("      <AutoLogon>");
                            content.AppendLine("        <Password>P@ssw0rd123</Password>");
                            content.AppendLine("        <Username>Administrator</Username>");
                            content.AppendLine("      </AutoLogon>");
                        }
                        content.AppendLine("    </component>");
                        content.AppendLine("  </settings>");
                        content.AppendLine("</unattend>");
                    }
                    else if (fileName.Contains("config"))
                    {
                        content.AppendLine("<configuration>");
                        content.AppendLine($"  <packageName>{packageName}</packageName>");
                        if (sensitivity >= FileSensitivity.Red)
                        {
                            content.AppendLine("  <connectionStrings>");
                            content.AppendLine("    <add name=\"DefaultConnection\" connectionString=\"Server=sql01;Database=AppDB;User ID=sa;Password=Secret123;\" />");
                            content.AppendLine("  </connectionStrings>");
                        }
                        content.AppendLine("</configuration>");
                    }
                    break;
                    
                case ".ps1":
                    content.AppendLine($"# {packageName} Deployment Script");
                    content.AppendLine("# Generated by Mock SCCM Server");
                    content.AppendLine();
                    if (sensitivity >= FileSensitivity.Yellow)
                    {
                        content.AppendLine("$credential = Get-Credential");
                        content.AppendLine("# TODO: Remove hardcoded password");
                        content.AppendLine("$password = ConvertTo-SecureString 'TempPass123' -AsPlainText -Force");
                    }
                    content.AppendLine($"Write-Host 'Installing {packageName}...'");
                    content.AppendLine("# Installation logic here");
                    break;
                    
                case ".bat":
                case ".cmd":
                    content.AppendLine($"@echo off");
                    content.AppendLine($"REM {packageName} Installation Script");
                    if (sensitivity >= FileSensitivity.Yellow)
                    {
                        content.AppendLine("net user admin TempPassword123 /add");
                        content.AppendLine("net localgroup administrators admin /add");
                    }
                    content.AppendLine($"echo Installing {packageName}...");
                    break;
                    
                case ".txt":
                    if (fileName.Contains("password") || fileName.Contains("credential"))
                    {
                        content.AppendLine($"{packageName} - Credentials File");
                        content.AppendLine("=================================");
                        content.AppendLine("Service Account: svc_deploy");
                        content.AppendLine("Password: SecurePass2023!");
                        content.AppendLine("Admin Account: deploy_admin");
                        content.AppendLine("Password: Admin123!");
                    }
                    else
                    {
                        content.AppendLine($"{packageName} - Information");
                        content.AppendLine("Version: 1.0");
                        content.AppendLine($"Created: {DateTime.Now:yyyy-MM-dd}");
                        content.AppendLine("Description: Mock SCCM package for testing");
                    }
                    break;
                    
                case ".config":
                    content.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                    content.AppendLine("<configuration>");
                    if (sensitivity >= FileSensitivity.Red)
                    {
                        content.AppendLine("  <connectionStrings>");
                        content.AppendLine("    <add name=\"MainDB\" connectionString=\"Data Source=srv01;Initial Catalog=AppDB;User ID=dbuser;Password=DbPass123;\" />");
                        content.AppendLine("  </connectionStrings>");
                        content.AppendLine("  <appSettings>");
                        content.AppendLine("    <add key=\"AdminPassword\" value=\"AdminSecret123\" />");
                        content.AppendLine("  </appSettings>");
                    }
                    content.AppendLine("</configuration>");
                    break;
                    
                default:
                    content.AppendLine($"# {packageName} - {fileName}");
                    content.AppendLine($"# Mock file content for testing");
                    content.AppendLine($"# Sensitivity Level: {sensitivity}");
                    break;
            }
            
            return Encoding.UTF8.GetBytes(content.ToString());
        }

        private string GeneratePackageId()
        {
            // SCCM packages typically use 8-character IDs
            return $"PKG{_random.Next(10000, 99999):D5}";
        }

        private string GenerateHash()
        {
            // Generate SCCM-style hash (8 characters)
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var hash = new StringBuilder(8);
            
            for (int i = 0; i < 8; i++)
            {
                hash.Append(chars[_random.Next(chars.Length)]);
            }
            
            return hash.ToString();
        }
    }
}