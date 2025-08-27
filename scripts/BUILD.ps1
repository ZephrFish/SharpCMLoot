# BUILD.ps1 - Consolidated Build Script for SCML
# Replaces: build_all.bat, build_single_exe.bat, build_single_exe.sh, scripts/build.bat

param(
    [Parameter(Position=0)]
    [ValidateSet("Debug", "Release", "Standalone", "Clean", "Rebuild", "All")]
    [string]$Action = "Release",
    
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# Build configuration
$ProjectFile = "SCML.csproj"
$SolutionFile = "SCML.sln"
$Configuration = "Release"
$OutputDir = "bin\$Configuration"

function Write-Status {
    param([string]$Message, [string]$Color = "Cyan")
    Write-Host "`n[BUILD] $Message" -ForegroundColor $Color
}

function Clean-Project {
    Write-Status "Cleaning project..."
    
    # Remove build directories
    @("bin", "obj", "packages") | ForEach-Object {
        if (Test-Path $_) {
            Write-Host "  Removing $_" -ForegroundColor Gray
            Remove-Item $_ -Recurse -Force
        }
    }
    
    # Clean MockSCCMServer
    if (Test-Path "MockSCCMServer\bin") {
        Remove-Item "MockSCCMServer\bin" -Recurse -Force
    }
    if (Test-Path "MockSCCMServer\obj") {
        Remove-Item "MockSCCMServer\obj" -Recurse -Force
    }
    
    Write-Status "Clean complete" -Color Green
}

function Restore-Packages {
    Write-Status "Restoring NuGet packages..."
    
    if (Get-Command nuget -ErrorAction SilentlyContinue) {
        nuget restore $SolutionFile
    } else {
        Write-Warning "NuGet not found in PATH. Attempting to download..."
        Invoke-WebRequest -Uri "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -OutFile "nuget.exe"
        .\nuget.exe restore $SolutionFile
    }
}

function Build-Project {
    param([string]$Config = "Release")
    
    Write-Status "Building SCML ($Config)..."
    
    # Find MSBuild
    $msbuild = $null
    
    # Check for VS 2022
    $vs2022 = "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe"
    $msbuildPaths = @(
        (Get-ChildItem $vs2022 -ErrorAction SilentlyContinue | Select-Object -First 1).FullName,
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\MSBuild\14.0\Bin\MSBuild.exe",
        "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
    )
    
    foreach ($path in $msbuildPaths) {
        if ($path -and (Test-Path $path)) {
            $msbuild = $path
            break
        }
    }
    
    if (-not $msbuild) {
        # Try using msbuild from PATH
        if (Get-Command msbuild -ErrorAction SilentlyContinue) {
            $msbuild = "msbuild"
        } else {
            throw "MSBuild not found. Please install Visual Studio or .NET Framework SDK"
        }
    }
    
    Write-Host "  Using MSBuild: $msbuild" -ForegroundColor Gray
    
    # Build the project
    $buildArgs = @(
        $ProjectFile,
        "/p:Configuration=$Config",
        "/p:Platform=AnyCPU",
        "/m"  # Parallel build
    )
    
    if (-not $Verbose) {
        $buildArgs += "/v:minimal"
    }
    
    & $msbuild $buildArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Status "Build complete" -Color Green
}

function Build-Standalone {
    Write-Status "Creating standalone executable..."
    
    if (-not (Test-Path "$OutputDir\SCML.exe")) {
        Build-Project -Config $Configuration
    }
    
    # Check for ILMerge
    $ilmerge = "packages\ILMerge.3.0.41\tools\net452\ILMerge.exe"
    
    if (-not (Test-Path $ilmerge)) {
        Write-Status "ILMerge not found, installing via NuGet..."
        nuget install ILMerge -Version 3.0.41 -OutputDirectory packages
    }
    
    # Get all DLLs to merge
    $mainExe = "$OutputDir\SCML.exe"
    $dlls = Get-ChildItem "$OutputDir\*.dll" | Where-Object { 
        $_.Name -notmatch "^System\." -and 
        $_.Name -notmatch "^Microsoft\." -and
        $_.Name -ne "mscorlib.dll"
    }
    
    Write-Host "  Merging assemblies:" -ForegroundColor Gray
    Write-Host "    Main: SCML.exe" -ForegroundColor Gray
    $dlls | ForEach-Object { Write-Host "    DLL: $($_.Name)" -ForegroundColor Gray }
    
    # Merge assemblies
    $mergeArgs = @(
        "/target:exe",
        "/targetplatform:v4,C:\Windows\Microsoft.NET\Framework64\v4.0.30319",
        "/out:$OutputDir\SCML_Standalone.exe",
        $mainExe
    )
    
    $mergeArgs += $dlls.FullName
    
    & $ilmerge $mergeArgs
    
    if (Test-Path "$OutputDir\SCML_Standalone.exe") {
        $size = (Get-Item "$OutputDir\SCML_Standalone.exe").Length / 1MB
        Write-Status "Standalone executable created: SCML_Standalone.exe ($([Math]::Round($size, 2)) MB)" -Color Green
    } else {
        throw "Failed to create standalone executable"
    }
}

function Build-All {
    Write-Status "Building all configurations..."
    
    # Clean first
    Clean-Project
    
    # Restore packages
    Restore-Packages
    
    # Build Debug
    Build-Project -Config "Debug"
    
    # Build Release
    Build-Project -Config "Release"
    
    # Build Standalone
    Build-Standalone
    
    # Build Mock Server
    Write-Status "Building MockSCCMServer..."
    Push-Location MockSCCMServer
    try {
        if (Get-Command msbuild -ErrorAction SilentlyContinue) {
            msbuild MockSCCMServer.csproj /p:Configuration=Release /v:minimal
        }
    } finally {
        Pop-Location
    }
    
    Write-Status "All builds complete!" -Color Green
}

# Main execution
try {
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "  SCML Build System" -ForegroundColor White
    Write-Host "=========================================" -ForegroundColor Cyan
    
    switch ($Action) {
        "Clean" {
            Clean-Project
        }
        "Debug" {
            Restore-Packages
            Build-Project -Config "Debug"
        }
        "Release" {
            Restore-Packages
            Build-Project -Config "Release"
        }
        "Standalone" {
            Restore-Packages
            Build-Project -Config "Release"
            Build-Standalone
        }
        "Rebuild" {
            Clean-Project
            Restore-Packages
            Build-Project -Config $Configuration
        }
        "All" {
            Build-All
        }
        default {
            Restore-Packages
            Build-Project -Config "Release"
        }
    }
    
    # Show output files
    if ((Test-Path $OutputDir) -and $Action -ne "Clean") {
        Write-Host "`nOutput files:" -ForegroundColor Yellow
        Get-ChildItem "$OutputDir\*.exe" | ForEach-Object {
            $size = [Math]::Round($_.Length / 1KB, 2)
            Write-Host "  - $($_.Name) (${size} KB)" -ForegroundColor Gray
        }
    }
    
    Write-Host "`n[SUCCESS] Build completed successfully!" -ForegroundColor Green
    
} catch {
    Write-Host "`n[ERROR] Build failed: $_" -ForegroundColor Red
    exit 1
}