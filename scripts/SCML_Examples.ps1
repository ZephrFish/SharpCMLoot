<#
.SYNOPSIS
    SCML v3.1 - PowerShell Examples and Automation Scripts
    
.DESCRIPTION
    This script provides comprehensive examples of using SCML with various
    credential options, Snaffler analysis, and automation scenarios.
    
.AUTHOR
    SCML Development Team
    
.VERSION
    3.1
#>

# ==============================================================================
# CONFIGURATION
# ==============================================================================

# Set SCML path (adjust as needed)
$SCMLPath = ".\bin\Release\SCML.exe"
if (-not (Test-Path $SCMLPath)) {
    $SCMLPath = ".\SCML.exe"
}

# Output directory for results
$OutputDir = ".\SCML_Results"
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# ==============================================================================
# HELPER FUNCTIONS
# ==============================================================================

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Test-SCMLConnection {
    param(
        [string]$Host,
        [PSCredential]$Credential
    )
    
    Write-ColorOutput "[*] Testing connection to $Host..." "Yellow"
    
    $args = @("--host", $Host, "--list-shares")
    
    if ($Credential) {
        $args += "--username", $Credential.UserName
        $args += "--password", $Credential.GetNetworkCredential().Password
        
        if ($Credential.GetNetworkCredential().Domain) {
            $args += "--domain", $Credential.GetNetworkCredential().Domain
        }
    } else {
        $args += "--current-user"
    }
    
    $result = & $SCMLPath $args 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "[+] Connection successful!" "Green"
        return $true
    } else {
        Write-ColorOutput "[-] Connection failed: $result" "Red"
        return $false
    }
}

function Get-SCMLInventory {
    param(
        [string]$Host,
        [string]$OutputFile,
        [PSCredential]$Credential,
        [switch]$Append
    )
    
    Write-ColorOutput "[*] Creating inventory for $Host..." "Yellow"
    
    $args = @("--host", $Host, "--outfile", $OutputFile)
    
    if ($Append) {
        $args += "--append"
    }
    
    if ($Credential) {
        $args += "--username", $Credential.UserName
        $args += "--password", $Credential.GetNetworkCredential().Password
        
        if ($Credential.GetNetworkCredential().Domain) {
            $args += "--domain", $Credential.GetNetworkCredential().Domain
        }
    } else {
        $args += "--current-user"
    }
    
    & $SCMLPath $args
    
    if (Test-Path $OutputFile) {
        $lineCount = (Get-Content $OutputFile).Count
        Write-ColorOutput "[+] Inventory created: $lineCount files found" "Green"
        return $true
    }
    return $false
}

function Invoke-SCMLSnaffler {
    param(
        [string]$Host,
        [string]$InventoryFile,
        [string[]]$Extensions,
        [PSCredential]$Credential,
        [switch]$UseExistingInventory
    )
    
    Write-ColorOutput "[*] Running Snaffler analysis..." "Yellow"
    
    $args = @("--host", $Host)
    
    if ($UseExistingInventory) {
        $args += "--snaffler-inventory", $InventoryFile
    } else {
        $args += "--outfile", $InventoryFile
        $args += "--snaffler"
    }
    
    if ($Extensions) {
        $args += "--download-extensions", ($Extensions -join ",")
    }
    
    if ($Credential) {
        $args += "--username", $Credential.UserName
        $args += "--password", $Credential.GetNetworkCredential().Password
        
        if ($Credential.GetNetworkCredential().Domain) {
            $args += "--domain", $Credential.GetNetworkCredential().Domain
        }
    } else {
        $args += "--current-user"
    }
    
    & $SCMLPath $args
    
    $snafflerOutput = $InventoryFile.Replace(".txt", "_snaffler.txt")
    if (Test-Path $snafflerOutput) {
        $critical = Select-String -Path $snafflerOutput -Pattern "BLACK|RED" -Quiet
        if ($critical) {
            Write-ColorOutput "[!] CRITICAL FINDINGS DETECTED!" "Red"
        }
        
        $findings = (Select-String -Path $snafflerOutput -Pattern "\[FILE\]").Count
        Write-ColorOutput "[+] Snaffler analysis complete: $findings interesting files found" "Green"
        return $snafflerOutput
    }
    return $null
}

function Get-SCMLSingleFile {
    param(
        [string]$FilePath,
        [string]$OutputDir = ".\Downloads",
        [PSCredential]$Credential
    )
    
    Write-ColorOutput "[*] Downloading single file: $FilePath" "Yellow"
    
    $args = @("--single-file", $FilePath, "--download-dir", $OutputDir)
    
    if ($Credential) {
        $args += "--username", $Credential.UserName
        $args += "--password", $Credential.GetNetworkCredential().Password
        
        if ($Credential.GetNetworkCredential().Domain) {
            $args += "--domain", $Credential.GetNetworkCredential().Domain
        }
    } else {
        $args += "--current-user"
    }
    
    & $SCMLPath $args
    
    $fileName = Split-Path $FilePath -Leaf
    $localPath = Join-Path $OutputDir $fileName
    
    if (Test-Path $localPath) {
        Write-ColorOutput "[+] File downloaded successfully: $localPath" "Green"
        return $localPath
    }
    return $null
}

# ==============================================================================
# EXAMPLE 1: Basic Inventory with Current User
# ==============================================================================

function Example-BasicInventory {
    Write-ColorOutput "`n=== EXAMPLE 1: Basic Inventory with Current User ===" "Cyan"
    
    $host = Read-Host "Enter SCCM server hostname"
    $outputFile = Join-Path $OutputDir "basic_inventory.txt"
    
    # Create inventory using current user
    Get-SCMLInventory -Host $host -OutputFile $outputFile
    
    # Display first 10 files
    if (Test-Path $outputFile) {
        Write-ColorOutput "`nFirst 10 files in inventory:" "Yellow"
        Get-Content $outputFile | Select-Object -First 10 | ForEach-Object {
            Write-Host "  $_"
        }
    }
}

# ==============================================================================
# EXAMPLE 2: Snaffler Analysis with Credentials
# ==============================================================================

function Example-SnafflerWithCredentials {
    Write-ColorOutput "`n=== EXAMPLE 2: Snaffler Analysis with Credentials ===" "Cyan"
    
    $host = Read-Host "Enter SCCM server hostname"
    $cred = Get-Credential -Message "Enter credentials for SCCM access"
    
    $inventoryFile = Join-Path $OutputDir "snaffler_inventory.txt"
    
    # Run Snaffler with credentials
    $snafflerResults = Invoke-SCMLSnaffler -Host $host `
                                           -InventoryFile $inventoryFile `
                                           -Extensions @("xml", "config", "ini", "ps1") `
                                           -Credential $cred
    
    # Parse and display critical findings
    if ($snafflerResults) {
        $criticalFindings = Select-String -Path $snafflerResults -Pattern "BLACK|RED"
        
        if ($criticalFindings) {
            Write-ColorOutput "`nCritical Findings:" "Red"
            $criticalFindings | ForEach-Object {
                Write-Host $_.Line
            }
        }
    }
}

# ==============================================================================
# EXAMPLE 3: Run Snaffler on Existing Inventory
# ==============================================================================

function Example-SnafflerExistingInventory {
    Write-ColorOutput "`n=== EXAMPLE 3: Snaffler on Existing Inventory ===" "Cyan"
    
    $inventoryFile = Read-Host "Enter path to existing inventory file"
    
    if (-not (Test-Path $inventoryFile)) {
        Write-ColorOutput "[-] Inventory file not found!" "Red"
        return
    }
    
    $host = Read-Host "Enter SCCM server hostname (for connection)"
    
    # Option to use credentials or current user
    $useCredentials = Read-Host "Use specific credentials? (y/n)"
    
    if ($useCredentials -eq 'y') {
        $cred = Get-Credential -Message "Enter credentials for SCCM access"
        $snafflerResults = Invoke-SCMLSnaffler -Host $host `
                                               -InventoryFile $inventoryFile `
                                               -Credential $cred `
                                               -UseExistingInventory
    } else {
        $snafflerResults = Invoke-SCMLSnaffler -Host $host `
                                               -InventoryFile $inventoryFile `
                                               -UseExistingInventory
    }
    
    if ($snafflerResults) {
        Write-ColorOutput "[+] Analysis complete. Results saved to: $snafflerResults" "Green"
    }
}

# ==============================================================================
# EXAMPLE 4: Multi-Server Processing
# ==============================================================================

function Example-MultiServerProcessing {
    Write-ColorOutput "`n=== EXAMPLE 4: Multi-Server Processing ===" "Cyan"
    
    $servers = @()
    Write-Host "Enter SCCM server names (one per line, empty line to finish):"
    
    while ($true) {
        $server = Read-Host
        if ([string]::IsNullOrWhiteSpace($server)) { break }
        $servers += $server
    }
    
    if ($servers.Count -eq 0) {
        Write-ColorOutput "[-] No servers provided!" "Red"
        return
    }
    
    $cred = Get-Credential -Message "Enter credentials for SCCM access"
    
    $results = @{}
    
    foreach ($server in $servers) {
        Write-ColorOutput "`n[*] Processing $server..." "Yellow"
        
        $inventoryFile = Join-Path $OutputDir "$server`_inventory.txt"
        
        # Test connection first
        if (Test-SCMLConnection -Host $server -Credential $cred) {
            # Create inventory
            if (Get-SCMLInventory -Host $server -OutputFile $inventoryFile -Credential $cred) {
                # Run Snaffler
                $snafflerResult = Invoke-SCMLSnaffler -Host $server `
                                                      -InventoryFile $inventoryFile `
                                                      -Credential $cred `
                                                      -UseExistingInventory
                
                $results[$server] = @{
                    Inventory = $inventoryFile
                    Snaffler = $snafflerResult
                }
            }
        }
    }
    
    # Summary report
    Write-ColorOutput "`n=== PROCESSING SUMMARY ===" "Cyan"
    foreach ($server in $results.Keys) {
        Write-Host "$server:"
        Write-Host "  Inventory: $($results[$server].Inventory)"
        Write-Host "  Snaffler: $($results[$server].Snaffler)"
        
        if ($results[$server].Snaffler) {
            $critical = (Select-String -Path $results[$server].Snaffler -Pattern "BLACK|RED").Count
            if ($critical -gt 0) {
                Write-ColorOutput "  CRITICAL FINDINGS: $critical" "Red"
            }
        }
    }
}

# ==============================================================================
# EXAMPLE 5: Automated Security Audit
# ==============================================================================

function Example-AutomatedSecurityAudit {
    Write-ColorOutput "`n=== EXAMPLE 5: Automated Security Audit ===" "Cyan"
    
    param(
        [string[]]$Servers,
        [PSCredential]$Credential,
        [string[]]$CriticalExtensions = @("xml", "config", "ini", "ps1", "bat", "txt")
    )
    
    $auditResults = @{}
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $reportFile = Join-Path $OutputDir "SecurityAudit_$timestamp.html"
    
    # Process each server
    foreach ($server in $Servers) {
        Write-ColorOutput "`n[*] Auditing $server..." "Yellow"
        
        $serverResults = @{
            Server = $server
            Status = "Failed"
            InventoryCount = 0
            CriticalFindings = @()
            HighValueFiles = @()
        }
        
        try {
            # Create inventory
            $inventoryFile = Join-Path $OutputDir "$server`_audit_inventory.txt"
            
            if (Get-SCMLInventory -Host $server -OutputFile $inventoryFile -Credential $Credential) {
                $serverResults.InventoryCount = (Get-Content $inventoryFile).Count
                
                # Run Snaffler
                $snafflerResult = Invoke-SCMLSnaffler -Host $server `
                                                      -InventoryFile $inventoryFile `
                                                      -Extensions $CriticalExtensions `
                                                      -Credential $Credential
                
                if ($snafflerResult -and (Test-Path $snafflerResult)) {
                    # Parse critical findings
                    $blackFindings = Select-String -Path $snafflerResult -Pattern "\[BLACK\]" | 
                                    ForEach-Object { $_.Line }
                    $redFindings = Select-String -Path $snafflerResult -Pattern "\[RED\]" | 
                                  ForEach-Object { $_.Line }
                    
                    $serverResults.CriticalFindings = $blackFindings + $redFindings
                    
                    # Identify high-value files for download
                    $files = Select-String -Path $snafflerResult -Pattern "\[FILE\] (.+)" | 
                            ForEach-Object { $_.Matches[0].Groups[1].Value }
                    
                    # Filter for most critical files
                    $highValue = $files | Where-Object { 
                        $_ -match "unattend|sysprep|password|credential|deploy|install"
                    }
                    
                    $serverResults.HighValueFiles = $highValue
                    
                    # Download critical files
                    if ($highValue.Count -gt 0) {
                        Write-ColorOutput "[!] Downloading $($highValue.Count) high-value files..." "Red"
                        
                        $downloadDir = Join-Path $OutputDir "$server`_critical_files"
                        New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
                        
                        foreach ($file in $highValue | Select-Object -First 10) {
                            Get-SCMLSingleFile -FilePath $file `
                                             -OutputDir $downloadDir `
                                             -Credential $Credential
                        }
                    }
                }
                
                $serverResults.Status = "Success"
            }
        }
        catch {
            Write-ColorOutput "[-] Error auditing $server`: $_" "Red"
            $serverResults.Error = $_.ToString()
        }
        
        $auditResults[$server] = $serverResults
    }
    
    # Generate HTML report
    Write-ColorOutput "`n[*] Generating audit report..." "Yellow"
    
    $html = @"
<!DOCTYPE html>
<html>
<head>
    <title>SCML Security Audit Report - $timestamp</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 20px; }
        h1 { color: #333; }
        h2 { color: #666; border-bottom: 2px solid #666; }
        .server-section { margin: 20px 0; padding: 15px; border: 1px solid #ddd; }
        .success { background-color: #f0fff0; }
        .failed { background-color: #fff0f0; }
        .critical { color: red; font-weight: bold; }
        .stats { margin: 10px 0; }
        .finding { margin: 5px 0; padding: 5px; background: #f5f5f5; }
        table { width: 100%; border-collapse: collapse; margin: 20px 0; }
        th, td { padding: 10px; text-align: left; border: 1px solid #ddd; }
        th { background-color: #f0f0f0; }
    </style>
</head>
<body>
    <h1>SCML Security Audit Report</h1>
    <p>Generated: $(Get-Date)</p>
    <p>Servers Audited: $($Servers.Count)</p>
    
    <h2>Summary</h2>
    <table>
        <tr>
            <th>Server</th>
            <th>Status</th>
            <th>Files Inventoried</th>
            <th>Critical Findings</th>
            <th>High-Value Files</th>
        </tr>
"@
    
    foreach ($server in $auditResults.Keys) {
        $result = $auditResults[$server]
        $statusClass = if ($result.Status -eq "Success") { "success" } else { "failed" }
        
        $html += @"
        <tr class='$statusClass'>
            <td>$server</td>
            <td>$($result.Status)</td>
            <td>$($result.InventoryCount)</td>
            <td class='critical'>$($result.CriticalFindings.Count)</td>
            <td>$($result.HighValueFiles.Count)</td>
        </tr>
"@
    }
    
    $html += @"
    </table>
    
    <h2>Detailed Findings</h2>
"@
    
    foreach ($server in $auditResults.Keys) {
        $result = $auditResults[$server]
        $statusClass = if ($result.Status -eq "Success") { "success" } else { "failed" }
        
        $html += @"
    <div class='server-section $statusClass'>
        <h3>$server</h3>
        <div class='stats'>
            <strong>Status:</strong> $($result.Status)<br/>
            <strong>Files Inventoried:</strong> $($result.InventoryCount)<br/>
            <strong>Critical Findings:</strong> <span class='critical'>$($result.CriticalFindings.Count)</span><br/>
            <strong>High-Value Files:</strong> $($result.HighValueFiles.Count)
        </div>
"@
        
        if ($result.CriticalFindings.Count -gt 0) {
            $html += "<h4>Critical Findings (Top 10):</h4>"
            foreach ($finding in $result.CriticalFindings | Select-Object -First 10) {
                $html += "<div class='finding'>$([System.Web.HttpUtility]::HtmlEncode($finding))</div>"
            }
        }
        
        if ($result.HighValueFiles.Count -gt 0) {
            $html += "<h4>High-Value Files Identified:</h4><ul>"
            foreach ($file in $result.HighValueFiles | Select-Object -First 10) {
                $html += "<li>$([System.Web.HttpUtility]::HtmlEncode($file))</li>"
            }
            $html += "</ul>"
        }
        
        $html += "</div>"
    }
    
    $html += @"
</body>
</html>
"@
    
    $html | Out-File -FilePath $reportFile -Encoding UTF8
    Write-ColorOutput "[+] Audit report saved to: $reportFile" "Green"
    
    # Open report in browser
    Start-Process $reportFile
    
    return $auditResults
}

# ==============================================================================
# MAIN MENU
# ==============================================================================

function Show-Menu {
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "       SCML v3.1 - Examples Menu        " "Cyan"
    Write-ColorOutput "========================================" "Cyan"
    Write-Host ""
    Write-Host "1. Basic Inventory with Current User"
    Write-Host "2. Snaffler Analysis with Credentials"
    Write-Host "3. Run Snaffler on Existing Inventory"
    Write-Host "4. Multi-Server Processing"
    Write-Host "5. Automated Security Audit (Advanced)"
    Write-Host "6. Test Connection to Server"
    Write-Host "7. Download Single File"
    Write-Host "8. Find SCCM Servers in Domain"
    Write-Host "9. Custom Command"
    Write-Host "0. Exit"
    Write-Host ""
    
    $choice = Read-Host "Select an option"
    
    switch ($choice) {
        "1" { Example-BasicInventory }
        "2" { Example-SnafflerWithCredentials }
        "3" { Example-SnafflerExistingInventory }
        "4" { Example-MultiServerProcessing }
        "5" { 
            $servers = @()
            Write-Host "Enter servers for audit (comma-separated):"
            $input = Read-Host
            $servers = $input -split "," | ForEach-Object { $_.Trim() }
            
            $cred = Get-Credential -Message "Enter credentials for audit"
            Example-AutomatedSecurityAudit -Servers $servers -Credential $cred
        }
        "6" {
            $host = Read-Host "Enter server hostname"
            $useCred = Read-Host "Use specific credentials? (y/n)"
            
            if ($useCred -eq 'y') {
                $cred = Get-Credential
                Test-SCMLConnection -Host $host -Credential $cred
            } else {
                Test-SCMLConnection -Host $host
            }
        }
        "7" {
            $filePath = Read-Host "Enter full UNC path to file"
            $useCred = Read-Host "Use specific credentials? (y/n)"
            
            if ($useCred -eq 'y') {
                $cred = Get-Credential
                Get-SCMLSingleFile -FilePath $filePath -Credential $cred
            } else {
                Get-SCMLSingleFile -FilePath $filePath
            }
        }
        "8" {
            $domain = Read-Host "Enter domain name"
            Write-ColorOutput "[*] Finding SCCM servers in $domain..." "Yellow"
            & $SCMLPath --findsccmservers --domain $domain
        }
        "9" {
            Write-Host "Enter custom SCML command (without SCML.exe):"
            $customCmd = Read-Host
            Write-ColorOutput "[*] Executing: $SCMLPath $customCmd" "Yellow"
            Invoke-Expression "& `"$SCMLPath`" $customCmd"
        }
        "0" { 
            Write-ColorOutput "Goodbye!" "Green"
            return 
        }
        default { 
            Write-ColorOutput "Invalid option!" "Red" 
        }
    }
    
    # Show menu again unless exiting
    if ($choice -ne "0") {
        Show-Menu
    }
}

# ==============================================================================
# ENTRY POINT
# ==============================================================================

Write-ColorOutput @"
  _____ _____ _____ __    
 |   __|     |     |  |   
 |__   |   --| | | |  |__ 
 |_____|_____|_|_|_|_____|
                          
 SCML v3.1 - PowerShell Examples
 ================================
"@ "Cyan"

# Check if SCML exists
if (-not (Test-Path $SCMLPath)) {
    Write-ColorOutput "[-] SCML.exe not found at: $SCMLPath" "Red"
    Write-Host "Please build SCML first or adjust the path in this script."
    exit 1
}

Write-ColorOutput "[+] SCML found at: $SCMLPath" "Green"
Write-ColorOutput "[+] Output directory: $OutputDir" "Green"

# Show interactive menu
Show-Menu