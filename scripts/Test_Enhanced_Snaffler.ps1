# Test_Enhanced_Snaffler.ps1
# Tests the enhanced Snaffler functionality with full path and match information

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
Write-Host "  TESTING ENHANCED SNAFFLER WITH FULL PATH AND MATCHES" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan

# Step 1: Create inventory
Write-Host "`n[Step 1] Creating inventory of $Target..." -ForegroundColor Yellow

$inventoryFile = "enhanced_snaffler_inventory.txt"
$credParams = if ($Username) {
    "--username $Username --domain $Domain" + $(if ($Password) { " --password $Password" } else { "" })
} else {
    "--current-user"
}

$cmd = "$SCMLPath --host $Target --outfile $inventoryFile $credParams"
Write-Host "Command: $cmd" -ForegroundColor Gray

Invoke-Expression $cmd

if (-not (Test-Path $inventoryFile)) {
    Write-Host "Failed to create inventory!" -ForegroundColor Red
    exit 1
}

$fileCount = (Get-Content $inventoryFile).Count
Write-Host "[+] Created inventory with $fileCount files" -ForegroundColor Green

# Step 2: Run Snaffler analysis on inventory (without download)
Write-Host "`n[Step 2] Running Snaffler analysis on inventory..." -ForegroundColor Yellow

$snafflerOutput = $inventoryFile.Replace(".txt", "_snaffler.txt")
$cmd = "$SCMLPath --host $Target --snaffler-inventory $inventoryFile $credParams"
Write-Host "Command: $cmd" -ForegroundColor Gray

Invoke-Expression $cmd

# Step 3: Check results
Write-Host "`n[Step 3] Checking results..." -ForegroundColor Yellow

if (Test-Path $snafflerOutput) {
    Write-Host "[+] Snaffler analysis complete!" -ForegroundColor Green
    Write-Host "    Text Report: $snafflerOutput" -ForegroundColor Cyan
    
    # Check for CSV file
    $csvOutput = $snafflerOutput.Replace(".txt", ".csv")
    if (Test-Path $csvOutput) {
        Write-Host "    CSV Report: $csvOutput" -ForegroundColor Cyan
        
        # Show CSV header
        Write-Host "`n[CSV Structure]" -ForegroundColor Yellow
        $csvContent = Import-Csv $csvOutput
        if ($csvContent.Count -gt 0) {
            Write-Host "Columns: $($csvContent[0].PSObject.Properties.Name -join ', ')" -ForegroundColor Gray
            Write-Host "Total Findings: $($csvContent.Count)" -ForegroundColor Green
            
            # Show sample findings
            Write-Host "`n[Sample Findings (First 3)]" -ForegroundColor Yellow
            $csvContent | Select-Object -First 3 | ForEach-Object {
                Write-Host "  File: $($_.FilePath)" -ForegroundColor White
                Write-Host "    Severity: $($_.Severity)" -ForegroundColor $(
                    switch($_.Severity) {
                        "BLACK" { "DarkGray" }
                        "RED" { "Red" }
                        "YELLOW" { "Yellow" }
                        "GREEN" { "Green" }
                        default { "Gray" }
                    }
                )
                Write-Host "    Rule: $($_.Rule)" -ForegroundColor Gray
                Write-Host "    Pattern: $($_.MatchedPattern)" -ForegroundColor Cyan
                if ($_.MatchedText) {
                    $preview = if ($_.MatchedText.Length -gt 50) { 
                        $_.MatchedText.Substring(0, 50) + "..." 
                    } else { 
                        $_.MatchedText 
                    }
                    Write-Host "    Match: $preview" -ForegroundColor Magenta
                }
                Write-Host ""
            }
        }
    } else {
        Write-Host "    [!] CSV file not found" -ForegroundColor Yellow
    }
    
    # Show sample text report findings
    Write-Host "`n[Sample Text Report (First 20 lines)]" -ForegroundColor Yellow
    Get-Content $snafflerOutput | Select-Object -First 20 | ForEach-Object {
        if ($_ -match '^\[FILE\]') {
            Write-Host $_ -ForegroundColor White
        } elseif ($_ -match '^\[SIZE\]') {
            Write-Host $_ -ForegroundColor Gray
        } elseif ($_ -match '^\s+\[(BLACK|RED|YELLOW|GREEN)\]') {
            $color = switch($Matches[1]) {
                "BLACK" { "DarkGray" }
                "RED" { "Red" }
                "YELLOW" { "Yellow" }
                "GREEN" { "Green" }
            }
            Write-Host $_ -ForegroundColor $color
        } elseif ($_ -match '^\s+Pattern:') {
            Write-Host $_ -ForegroundColor Cyan
        } elseif ($_ -match '^\s+Match:') {
            Write-Host $_ -ForegroundColor Magenta
        } elseif ($_ -match '^\s+Context:') {
            Write-Host $_ -ForegroundColor Blue
        } else {
            Write-Host $_ -ForegroundColor Gray
        }
    }
    
} else {
    Write-Host "[-] Snaffler output file not found!" -ForegroundColor Red
}

# Step 4: Test with specific extensions
Write-Host "`n[Step 4] Testing Snaffler with specific extensions (xml, ps1, config)..." -ForegroundColor Yellow

$extensionOutput = "extension_snaffler.txt"
$cmd = "$SCMLPath --host $Target --outfile extension_inventory.txt --snaffler --download-extensions xml,ps1,config $credParams"
Write-Host "Command: $cmd" -ForegroundColor Gray

Invoke-Expression $cmd

if (Test-Path "extension_inventory_snaffler.txt") {
    Write-Host "[+] Extension-filtered Snaffler analysis complete!" -ForegroundColor Green
    
    # Count findings by extension
    $findings = Get-Content "extension_inventory_snaffler.txt" | Select-String "^\[FILE\]"
    Write-Host "    Total files analyzed: $($findings.Count)" -ForegroundColor Cyan
    
    $xmlCount = ($findings | Where-Object { $_ -match "\.xml" }).Count
    $ps1Count = ($findings | Where-Object { $_ -match "\.ps1" }).Count
    $configCount = ($findings | Where-Object { $_ -match "\.config" }).Count
    
    Write-Host "    XML files: $xmlCount" -ForegroundColor Gray
    Write-Host "    PS1 files: $ps1Count" -ForegroundColor Gray
    Write-Host "    Config files: $configCount" -ForegroundColor Gray
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "  ENHANCED SNAFFLER TEST COMPLETE" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green

Write-Host "`nKey Features Demonstrated:" -ForegroundColor Yellow
Write-Host "  1. Full UNC paths in output (\\\\server\\share\\path\\file)" -ForegroundColor Cyan
Write-Host "  2. Pattern matching information (regex/string that matched)" -ForegroundColor Cyan
Write-Host "  3. Matched text preview (actual content that triggered rule)" -ForegroundColor Cyan
Write-Host "  4. Context information when available" -ForegroundColor Cyan
Write-Host "  5. CSV output with all details for easy analysis" -ForegroundColor Cyan
Write-Host "  6. Real-time writing (results saved immediately)" -ForegroundColor Cyan

Write-Host "`nOutput Files:" -ForegroundColor Yellow
Get-ChildItem *snaffler*.txt, *snaffler*.csv -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "  - $($_.Name) ($($_.Length) bytes)" -ForegroundColor Gray
}