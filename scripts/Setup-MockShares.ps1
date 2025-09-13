# PowerShell script to set up Mock SCCM shares
# Requires Administrator privileges

param(
    [switch]$Remove = $false
)

# Check if running as Administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script requires Administrator privileges!" -ForegroundColor Red
    Write-Host "Please run PowerShell as Administrator and try again." -ForegroundColor Yellow
    exit 1
}

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$contentRoot = Join-Path $scriptPath "MockSCCMContent"

Write-Host "========================================" -ForegroundColor Cyan
if ($Remove) {
    Write-Host "Removing Mock SCCM Shares" -ForegroundColor Cyan
} else {
    Write-Host "Setting up Mock SCCM Shares" -ForegroundColor Cyan
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$shares = @(
    @{Name = "SCCMContentLib$"; Path = Join-Path $contentRoot "SCCMContentLib$"},
    @{Name = "SMSPKGD$"; Path = Join-Path $contentRoot "SMSPKGD$"},
    @{Name = "ADMIN$"; Path = Join-Path $contentRoot "ADMIN$"}
)

if ($Remove) {
    # Remove shares
    foreach ($share in $shares) {
        Write-Host "Removing share: $($share.Name)..." -ForegroundColor Yellow
        try {
            Remove-SmbShare -Name $share.Name -Force -ErrorAction SilentlyContinue
            Write-Host "  [+] Removed $($share.Name)" -ForegroundColor Green
        }
        catch {
            Write-Host "  [-] Share not found or already removed" -ForegroundColor Gray
        }
    }
} else {
    # Create directories
    Write-Host "Creating directories..." -ForegroundColor Yellow
    foreach ($share in $shares) {
        if (!(Test-Path $share.Path)) {
            New-Item -ItemType Directory -Path $share.Path -Force | Out-Null
            Write-Host "  [+] Created directory: $($share.Path)" -ForegroundColor Green
        } else {
            Write-Host "  [*] Directory exists: $($share.Path)" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    Write-Host "Creating shares..." -ForegroundColor Yellow
    
    foreach ($share in $shares) {
        try {
            # Remove existing share if it exists
            Remove-SmbShare -Name $share.Name -Force -ErrorAction SilentlyContinue
        }
        catch { }
        
        try {
            # Create new share with full permissions
            New-SmbShare -Name $share.Name -Path $share.Path -FullAccess "Everyone" -Description "Mock SCCM Share" | Out-Null
            Write-Host "  [+] Created share: $($share.Name)" -ForegroundColor Green
            Write-Host "      Path: $($share.Path)" -ForegroundColor Gray
        }
        catch {
            Write-Host "  [-] Failed to create share: $($share.Name)" -ForegroundColor Red
            Write-Host "      Error: $_" -ForegroundColor Red
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Setup Complete!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Available shares:" -ForegroundColor Yellow
    Get-SmbShare | Where-Object { $_.Name -like "*$" } | Format-Table Name, Path -AutoSize
    
    Write-Host ""
    Write-Host "Test commands:" -ForegroundColor Yellow
    Write-Host "  SCML.exe --host localhost --list-shares" -ForegroundColor Cyan
    Write-Host "  net view \\localhost" -ForegroundColor Cyan
    Write-Host "  Get-SmbShare" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green