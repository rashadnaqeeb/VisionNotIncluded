# release.ps1 - Build and populate the release/ folder for Steam Workshop upload.
# Excludes config files so player settings aren't overwritten on update.

$ErrorActionPreference = "Stop"

# Build first
& "$PSScriptRoot\build.ps1"
if ($LASTEXITCODE -ne 0) { exit 1 }

$ReleaseDir = "$PSScriptRoot\release"

# Clean and recreate
if (Test-Path $ReleaseDir) {
    Remove-Item $ReleaseDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleaseDir -Force | Out-Null

$ProjectDir  = "$PSScriptRoot\OniAccess"
$BuildOutput = "$ProjectDir\bin\Release\net48\OniAccess.dll"

# DLL and mod metadata
Copy-Item $BuildOutput "$ReleaseDir\OniAccess.dll" -Force
Copy-Item "$ProjectDir\mod_info.yaml" "$ReleaseDir\mod_info.yaml" -Force
Copy-Item "$ProjectDir\mod.yaml" "$ReleaseDir\mod.yaml" -Force

# Native dependencies - all platforms
$Platforms = @(
    @{ Src = "win-x64"; Files = @("prism.dll") },
    @{ Src = "linux-x64"; Files = @("libprism.so") },
    @{ Src = "osx"; Files = @("libprism.dylib") }
)
foreach ($plat in $Platforms) {
    $destDir = "$ReleaseDir\native\$($plat.Src)"
    New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    foreach ($file in $plat.Files) {
        $src = "$PSScriptRoot\prism\native\$($plat.Src)\$file"
        if (Test-Path $src) {
            Copy-Item $src "$destDir\$file" -Force
        } else {
            Write-Host "WARNING: $file not found at $src" -ForegroundColor Yellow
        }
    }
}

# Translations
$TranslationsSrc = "$PSScriptRoot\translations"
if (Test-Path $TranslationsSrc) {
    $PoFiles = Get-ChildItem "$TranslationsSrc\*.po" -ErrorAction SilentlyContinue
    if ($PoFiles.Count -gt 0) {
        $TranslationsDest = "$ReleaseDir\translations"
        New-Item -ItemType Directory -Path $TranslationsDest -Force | Out-Null
        foreach ($po in $PoFiles) {
            Copy-Item $po.FullName "$TranslationsDest\$($po.Name)" -Force
        }
        Write-Host "Included $($PoFiles.Count) translation file(s)" -ForegroundColor Green
    }
}

# Audio
$AudioSrc = "$PSScriptRoot\audio"
if (Test-Path $AudioSrc) {
    $OggFiles = Get-ChildItem "$AudioSrc\*.ogg" -ErrorAction SilentlyContinue
    if ($OggFiles.Count -gt 0) {
        $AudioDest = "$ReleaseDir\audio"
        New-Item -ItemType Directory -Path $AudioDest -Force | Out-Null
        foreach ($ogg in $OggFiles) {
            Copy-Item $ogg.FullName "$AudioDest\$($ogg.Name)" -Force
        }
        Write-Host "Included $($OggFiles.Count) audio file(s)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Release folder ready at $ReleaseDir" -ForegroundColor Cyan
Write-Host "Point the Steam Workshop uploader at this folder." -ForegroundColor Cyan
