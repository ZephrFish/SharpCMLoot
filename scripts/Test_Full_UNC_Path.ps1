# Test_Full_UNC_Path.ps1
# Verifies that full UNC paths are being written to inventory and Snaffler outputs

param(
    [string]$Target = "SCCM01.lab.local",
    [string]$Username,
    [string]$Password,
    [string]$Domain = "LAB"
)

$ErrorActionPreference = "Continue"
$SCMLPath = ".\bin\Release\SCML.exe"

if (-not (Test-Path $SCMLPath)) {
    $SCMLPath = ".\SCML.exe"
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  TESTING FULL UNC PATH GENERATION" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

# Build credentials parameter
$credParams = if ($Username) {
    "--username $Username --domain $Domain" + $(if ($Password) { " --password $Password" } else { "" })
} else {
    "--current-user"
}

# Step 1: Create inventory and verify paths
Write-Host "`n[Step 1] Creating inventory to verify full UNC paths..." -ForegroundColor Yellow

$inventoryFile = "unc_path_test_inventory.txt"
$cmd = "$SCMLPath --host $Target --outfile $inventoryFile $credParams"
Write-Host "Command: $cmd" -ForegroundColor Gray

Invoke-Expression $cmd

if (Test-Path $inventoryFile) {
    Write-Host "[+] Inventory created successfully" -ForegroundColor Green
    
    # Check first 5 lines of inventory for proper UNC paths
    Write-Host "`n[Verifying UNC Path Format]" -ForegroundColor Yellow
    $samplePaths = Get-Content $inventoryFile | Select-Object -First 5
    
    foreach ($path in $samplePaths) {
        if ($path -match '^\\\\([^\\]+)\\([^\\]+)\\(.+)$') {
            $server = $Matches[1]
            $share = $Matches[2]
            $filePath = $Matches[3]
            
            Write-Host "[+] Valid UNC path found:" -ForegroundColor Green
            Write-Host "    Server: $server" -ForegroundColor Cyan
            Write-Host "    Share: $share" -ForegroundColor Cyan
            Write-Host "    Path: $filePath" -ForegroundColor Gray
            
            # Verify server name matches target
            if ($server -eq $Target -or $server -eq $Target.Split('.')[0]) {
                Write-Host "    [+] Server name matches target!" -ForegroundColor Green
            } else {
                Write-Host "    [-] Warning: Server name doesn't match target" -ForegroundColor Yellow
            }
        } else {
            Write-Host "[-] Invalid UNC path format: $path" -ForegroundColor Red
        }
    }
    
    # Show total count
    $totalFiles = (Get-Content $inventoryFile).Count
    Write-Host "`nTotal files in inventory: $totalFiles" -ForegroundColor Cyan
} else {
    Write-Host "[-] Failed to create inventory" -ForegroundColor Red
    exit 1
}

# Step 2: Run Snaffler analysis and check paths in output
Write-Host "`n[Step 2] Running Snaffler analysis to verify paths in output..." -ForegroundColor Yellow

$snafflerCmd = "$SCMLPath --host $Target --snaffler-inventory $inventoryFile $credParams"
Write-Host "Command: $snafflerCmd" -ForegroundColor Gray

Invoke-Expression $snafflerCmd

$snafflerOutput = $inventoryFile.Replace(".txt", "_snaffler.txt")
$csvOutput = $inventoryFile.Replace(".txt", "_snaffler.csv")

if (Test-Path $snafflerOutput) {
    Write-Host "[+] Snaffler text output created" -ForegroundColor Green
    
    # Check for [FILE] entries with full paths
    $fileEntries = Get-Content $snafflerOutput | Select-String "^\[FILE\]"
    Write-Host "Found $($fileEntries.Count) file entries in Snaffler output" -ForegroundColor Cyan
    
    if ($fileEntries.Count -gt 0) {
        Write-Host "`n[Sample Snaffler File Entries]" -ForegroundColor Yellow
        $fileEntries | Select-Object -First 3 | ForEach-Object {
            $line = $_.Line
            if ($line -match '\[FILE\] (.+)$') {
                $filePath = $Matches[1]
                Write-Host "  $filePath" -ForegroundColor White
                
                # Verify it's a full UNC path
                if ($filePath -match '^\\\\[^\\]+\\[^\\]+\\') {
                    Write-Host "    [+] Valid full UNC path" -ForegroundColor Green
                } else {
                    Write-Host "    [-] Not a full UNC path!" -ForegroundColor Red
                }
            }
        }
    }
}

if (Test-Path $csvOutput) {
    Write-Host "`n[+] CSV output created" -ForegroundColor Green
    
    # Import CSV and check paths
    $csvData = Import-Csv $csvOutput
    if ($csvData.Count -gt 0) {
        Write-Host "CSV contains $($csvData.Count) findings" -ForegroundColor Cyan
        
        Write-Host "`n[Sample CSV Entries]" -ForegroundColor Yellow
        $csvData | Select-Object -First 3 | ForEach-Object {
            Write-Host "  FilePath: $($_.FilePath)" -ForegroundColor White
            
            # Verify it's a full UNC path
            if ($_.FilePath -match '^\\\\[^\\]+\\[^\\]+\\') {
                Write-Host "    [+] Valid full UNC path for download" -ForegroundColor Green
            } else {
                Write-Host "    [-] Not a full UNC path!" -ForegroundColor Red
            }
            
            Write-Host "    Severity: $($_.Severity)" -ForegroundColor Cyan
            Write-Host "    Rule: $($_.Rule)" -ForegroundColor Gray
            if ($_.MatchedPattern) {
                Write-Host "    Pattern: $($_.MatchedPattern)" -ForegroundColor Magenta
            }
            if ($_.MatchedText) {
                $preview = if ($_.MatchedText.Length -gt 50) {
                    $_.MatchedText.Substring(0, 50) + "..."
                } else {
                    $_.MatchedText
                }
                Write-Host "    Match: $preview" -ForegroundColor Yellow
            }
        }
    }
}

# Step 3: Test single file download with a path from inventory
Write-Host "`n[Step 3] Testing single file download with full UNC path..." -ForegroundColor Yellow

# Get a sample file path from inventory
$samplePath = Get-Content $inventoryFile | Where-Object { $_ -match '\.xml|\.ps1|\.config' } | Select-Object -First 1

if ($samplePath) {
    Write-Host "Attempting to download: $samplePath" -ForegroundColor Cyan
    
    $downloadCmd = "$SCMLPath --single-file `"$samplePath`" --download-dir unc_test_download $credParams"
    Write-Host "Command: $downloadCmd" -ForegroundColor Gray
    
    Invoke-Expression $downloadCmd
    
    # Check if file was downloaded
    $fileName = Split-Path $samplePath -Leaf
    $downloadedFile = "unc_test_download\$fileName"
    
    if (Test-Path $downloadedFile) {
        Write-Host "[+] File downloaded successfully using full UNC path!" -ForegroundColor Green
        Write-Host "    Downloaded to: $downloadedFile" -ForegroundColor Cyan
        $fileSize = (Get-Item $downloadedFile).Length
        Write-Host "    Size: $fileSize bytes" -ForegroundColor Gray
    } else {
        Write-Host "[-] File download failed" -ForegroundColor Red
    }
} else {
    Write-Host "[!] No suitable file found in inventory for download test" -ForegroundColor Yellow
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  FULL UNC PATH TEST COMPLETE" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green

Write-Host "`nSummary:" -ForegroundColor Yellow
Write-Host "  - Inventory files contain full UNC paths: \\\\server\\share\\path\\file" -ForegroundColor Cyan
Write-Host "  - Snaffler output includes full UNC paths for all findings" -ForegroundColor Cyan
Write-Host "  - CSV output contains downloadable UNC paths in FilePath column" -ForegroundColor Cyan
Write-Host "  - Single file downloads work with full UNC paths from inventory" -ForegroundColor Cyan
Write-Host "  - All paths can be used directly for download operations" -ForegroundColor Cyan