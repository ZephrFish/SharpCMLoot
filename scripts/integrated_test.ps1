# SCML Integrated Testing Suite (PowerShell)
# ==========================================

Write-Host "SCML Integrated Testing Suite" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "This script tests SCML against the Mock SCCM Server" -ForegroundColor White
Write-Host ""

# Check if both executables exist
if (-not (Test-Path "bin\Release\SCML.exe")) {
    Write-Host "Error: SCML.exe not found. Run build_all.bat first." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

if (-not (Test-Path "MockSCCMServer\bin\Release\MockSCCMServer.exe")) {
    Write-Host "Error: MockSCCMServer.exe not found. Run build_all.bat first." -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

Write-Host "Starting Mock SCCM Server..." -ForegroundColor Yellow
Write-Host "============================" -ForegroundColor Yellow

# Start mock server in background
$mockServer = Start-Process -FilePath "MockSCCMServer\bin\Release\MockSCCMServer.exe" -WindowStyle Minimized -PassThru

Write-Host "Waiting for mock server to initialize..." -ForegroundColor Gray
Start-Sleep -Seconds 8

Write-Host ""
Write-Host "Running SCML Test Suite..." -ForegroundColor Yellow
Write-Host "==========================" -ForegroundColor Yellow

# Test 1: Share Discovery
Write-Host ""
Write-Host "[Test 1] Share Discovery" -ForegroundColor Green
Write-Host "------------------------" -ForegroundColor Green
& "bin\Release\SCML.exe" --host localhost --list-shares --debug --current-user

# Test 2: Basic Inventory
Write-Host ""
Write-Host "[Test 2] Basic Inventory Generation" -ForegroundColor Green
Write-Host "-----------------------------------" -ForegroundColor Green
& "bin\Release\SCML.exe" --host localhost --outfile test_inventory.txt --debug --current-user --inventory-only

if (Test-Path "test_inventory.txt") {
    $size = (Get-Item "test_inventory.txt").Length
    Write-Host "[+] Inventory file created successfully (Size: $size bytes)" -ForegroundColor Green
} else {
    Write-Host "[-] Inventory file creation failed" -ForegroundColor Red
}

# Test 3: Baseline Security Scan
Write-Host ""
Write-Host "[Test 3] Baseline Security Scan" -ForegroundColor Green
Write-Host "--------------------------------" -ForegroundColor Green
& "bin\Release\SCML.exe" --host localhost --outfile baseline_test.txt --preset baseline --debug --current-user

if (Test-Path "baseline_test.txt") {
    $size = (Get-Item "baseline_test.txt").Length
    Write-Host "[+] Baseline scan completed (Size: $size bytes)" -ForegroundColor Green
} else {
    Write-Host "[-] Baseline scan failed" -ForegroundColor Red
}

# Test 4: Comprehensive Security Analysis
Write-Host ""
Write-Host "[Test 4] Comprehensive Security Analysis" -ForegroundColor Green
Write-Host "----------------------------------------" -ForegroundColor Green
& "bin\Release\SCML.exe" --host localhost --outfile comprehensive_test.txt --preset all-sensitive --snaffler-scan --html-report --debug --current-user

if (Test-Path "comprehensive_test.txt") {
    $size = (Get-Item "comprehensive_test.txt").Length
    Write-Host "[+] Comprehensive analysis completed (Size: $size bytes)" -ForegroundColor Green
}

if (Test-Path "sccm_explorer_report.html") {
    $size = (Get-Item "sccm_explorer_report.html").Length
    Write-Host "[+] HTML report generated (Size: $size bytes)" -ForegroundColor Green
}

# Test 5: Credential Discovery
Write-Host ""
Write-Host "[Test 5] Credential Discovery" -ForegroundColor Green
Write-Host "-----------------------------" -ForegroundColor Green
& "bin\Release\SCML.exe" --host localhost --outfile credentials_test.txt --preset credentials --debug --current-user

if (Test-Path "credentials_test.txt") {
    $size = (Get-Item "credentials_test.txt").Length
    Write-Host "[+] Credential discovery completed (Size: $size bytes)" -ForegroundColor Green
}

# Results Summary
Write-Host ""
Write-Host "Test Results Summary:" -ForegroundColor Cyan
Write-Host "=====================" -ForegroundColor Cyan
Write-Host "Generated Files:" -ForegroundColor White

$testFiles = @(
    @{Name = "test_inventory.txt"; Description = "Basic inventory"},
    @{Name = "baseline_test.txt"; Description = "Baseline security scan"},
    @{Name = "comprehensive_test.txt"; Description = "Full security analysis"},
    @{Name = "credentials_test.txt"; Description = "Credential discovery"},
    @{Name = "sccm_explorer_report.html"; Description = "HTML report"},
    @{Name = "execution_summary.txt"; Description = "Execution statistics"}
)

foreach ($file in $testFiles) {
    if (Test-Path $file.Name) {
        Write-Host "  - $($file.Name) [$($file.Description)]" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Mock SCCM Content Location:" -ForegroundColor White
Write-Host "  MockSCCMServer\bin\Release\MockSCCMContent\" -ForegroundColor Gray
Write-Host ""

Write-Host "All tests completed!" -ForegroundColor Green
Write-Host ""

# Cleanup
Write-Host "Press any key to stop mock server and exit..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

Write-Host "Stopping mock server..." -ForegroundColor Gray
if ($mockServer -and !$mockServer.HasExited) {
    $mockServer.Kill()
    $mockServer.WaitForExit(5000)
}

Write-Host ""
Write-Host "Integrated testing completed." -ForegroundColor Green
Read-Host "Press Enter to exit"