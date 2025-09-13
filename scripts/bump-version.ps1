# PowerShell script to bump version numbers
param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("major", "minor", "patch")]
    [string]$Type = "patch"
)

# Read current version
$versionFile = Join-Path $PSScriptRoot "..\VERSION"
if (Test-Path $versionFile) {
    $currentVersion = Get-Content $versionFile -Raw
    $currentVersion = $currentVersion.Trim()
} else {
    $currentVersion = "0.0.0"
}

Write-Host "Current version: $currentVersion" -ForegroundColor Cyan

# Parse version
$parts = $currentVersion.Split('.')
$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

# Increment based on type
switch ($Type) {
    "major" {
        $major++
        $minor = 0
        $patch = 0
        Write-Host "Bumping major version" -ForegroundColor Yellow
    }
    "minor" {
        $minor++
        $patch = 0
        Write-Host "Bumping minor version" -ForegroundColor Yellow
    }
    "patch" {
        $patch++
        Write-Host "Bumping patch version" -ForegroundColor Yellow
    }
}

$newVersion = "$major.$minor.$patch"
Write-Host "New version: $newVersion" -ForegroundColor Green

# Update VERSION file
$newVersion | Out-File -FilePath $versionFile -NoNewline -Encoding ASCII

# Update AssemblyInfo files if they exist
$assemblyInfoFiles = @(
    Join-Path $PSScriptRoot "..\Properties\AssemblyInfo.cs"
    Join-Path $PSScriptRoot "..\MockSCCMServer\Properties\AssemblyInfo.cs"
)

foreach ($file in $assemblyInfoFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        $content = $content -replace 'AssemblyVersion\("[0-9\.]+"\)', "AssemblyVersion(`"$newVersion.0`")"
        $content = $content -replace 'AssemblyFileVersion\("[0-9\.]+"\)', "AssemblyFileVersion(`"$newVersion.0`")"
        $content = $content -replace 'AssemblyInformationalVersion\("[0-9\.]+"\)', "AssemblyInformationalVersion(`"$newVersion`")"
        Set-Content $file $content
        Write-Host "Updated: $file" -ForegroundColor Gray
    }
}

# Update CHANGELOG.md
$changelogPath = Join-Path $PSScriptRoot "..\CHANGELOG.md"
if (Test-Path $changelogPath) {
    $date = Get-Date -Format "yyyy-MM-dd"
    $changelogContent = Get-Content $changelogPath -Raw
    
    # Check if version already exists
    if ($changelogContent -notmatch "## \[$newVersion\]") {
        $newEntry = @"

## [$newVersion] - $date

### Added
- 

### Fixed
- 

### Changed
- 

"@
        # Insert after the header
        $changelogContent = $changelogContent -replace '(# Changelog.*?\n)', "`$1$newEntry"
        Set-Content $changelogPath $changelogContent
        Write-Host "Updated CHANGELOG.md with new version entry" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Version bumped to $newVersion" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Update CHANGELOG.md with your changes" -ForegroundColor White
Write-Host "2. Commit: git add -A && git commit -m `"chore: bump version to $newVersion`"" -ForegroundColor White
Write-Host "3. Tag: git tag -a v$newVersion -m `"Release v$newVersion`"" -ForegroundColor White
Write-Host "4. Push: git push && git push --tags" -ForegroundColor White