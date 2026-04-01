# build.ps1 - Build and deploy OniAccess mod to ONI's local mods directory.
# Also patches mods.json to ensure the mod stays enabled (prevents the game
# from disabling it after crashes or version mismatches).

param(
    [switch]$NoBuild,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-NoBuild] [-Help]"
    Write-Host "  -NoBuild  Skip building, just copy the last built DLL and patch mods.json"
    Write-Host "  -Help     Show this help"
    exit 0
}

$ErrorActionPreference = "Stop"

# Locate the game's Managed directory for building against game assemblies.
# Checks ONI_MANAGED env var first, then auto-detects from Steam's library folders.
if (-not $env:ONI_MANAGED) {
    $SteamPaths = @()
    $RegSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $DefaultSteam = if ($RegSteam) { $RegSteam } else { "C:\Program Files (x86)\Steam" }
    if (Test-Path "$DefaultSteam\steamapps") {
        $SteamPaths += $DefaultSteam
    }
    $LibFolders = "$DefaultSteam\steamapps\libraryfolders.vdf"
    if (Test-Path $LibFolders) {
        $content = Get-Content $LibFolders -Raw
        [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
            $p = $_.Groups[1].Value -replace '\\\\', '\'
            if ($p -ne $DefaultSteam -and (Test-Path "$p\steamapps")) {
                $SteamPaths += $p
            }
        }
    }
    foreach ($steam in $SteamPaths) {
        $candidate = "$steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed"
        if (Test-Path $candidate) {
            $env:ONI_MANAGED = $candidate
            break
        }
    }
    if (-not $env:ONI_MANAGED) {
        Write-Host "ERROR: Could not find ONI. Set the ONI_MANAGED environment variable to" -ForegroundColor Red
        Write-Host "  <SteamLibrary>\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed" -ForegroundColor Red
        exit 1
    }
}

$ProjectDir  = "$PSScriptRoot\OniAccess"
$BuildOutput = "$ProjectDir\bin\Release\net48\OniAccess.dll"
$DocsDir     = [Environment]::GetFolderPath("MyDocuments")
$ModDir      = "$DocsDir\Klei\OxygenNotIncluded\mods\local\OniAccess"
$ModsJson    = "$DocsDir\Klei\OxygenNotIncluded\mods\mods.json"

# --- Sync version from .csproj to mod_info.yaml ---
$CsprojPath = "$ProjectDir\OniAccess.csproj"
$ModInfoPath = "$ProjectDir\mod_info.yaml"
[xml]$csproj = Get-Content $CsprojPath
$Version = $csproj.Project.PropertyGroup.Version
$modInfo = Get-Content $ModInfoPath -Raw
$modInfo = $modInfo -replace 'version: ".*"', "version: `"$Version`""
[System.IO.File]::WriteAllText($ModInfoPath, $modInfo)

# --- Build ---
if (-not $NoBuild) {
    Write-Host "Building OniAccess..." -ForegroundColor Cyan
    dotnet build "$ProjectDir\OniAccess.csproj" -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build FAILED." -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $BuildOutput)) {
    Write-Host "ERROR: DLL not found at $BuildOutput" -ForegroundColor Red
    exit 1
}

# --- Copy DLL and native dependencies ---
if (-not (Test-Path $ModDir)) {
    New-Item -ItemType Directory -Path $ModDir -Force | Out-Null
}
Copy-Item $BuildOutput "$ModDir\OniAccess.dll" -Force
Copy-Item "$ProjectDir\mod_info.yaml" "$ModDir\mod_info.yaml" -Force
Copy-Item "$ProjectDir\mod.yaml" "$ModDir\mod.yaml" -Force

# Deploy platform-specific Prism native library.
# For local development, only the Windows binary is needed.
$PrismSrc = "$PSScriptRoot\prism\native\win-x64"
$NativeDir = "$ModDir\native\win-x64"
if (-not (Test-Path $NativeDir)) {
    New-Item -ItemType Directory -Path $NativeDir -Force | Out-Null
}
Copy-Item "$PrismSrc\prism.dll" "$NativeDir\prism.dll" -Force
Write-Host "Deployed Prism native library to $NativeDir" -ForegroundColor Green

# --- Copy translation files ---
$TranslationsSrc = "$PSScriptRoot\translations"
if (Test-Path $TranslationsSrc) {
    $PoFiles = Get-ChildItem "$TranslationsSrc\*.po" -ErrorAction SilentlyContinue
    if ($PoFiles.Count -gt 0) {
        $TranslationsDest = "$ModDir\translations"
        if (-not (Test-Path $TranslationsDest)) {
            New-Item -ItemType Directory -Path $TranslationsDest -Force | Out-Null
        }
        foreach ($po in $PoFiles) {
            Copy-Item $po.FullName "$TranslationsDest\$($po.Name)" -Force
        }
        Write-Host "Deployed $($PoFiles.Count) translation file(s) to $TranslationsDest" -ForegroundColor Green
    }
}

# --- Copy audio files ---
$AudioSrc = "$PSScriptRoot\audio"
if (Test-Path $AudioSrc) {
    $OggFiles = Get-ChildItem "$AudioSrc\*.ogg" -ErrorAction SilentlyContinue
    if ($OggFiles.Count -gt 0) {
        $AudioDest = "$ModDir\audio"
        if (-not (Test-Path $AudioDest)) {
            New-Item -ItemType Directory -Path $AudioDest -Force | Out-Null
        }
        foreach ($ogg in $OggFiles) {
            Copy-Item $ogg.FullName "$AudioDest\$($ogg.Name)" -Force
        }
        Write-Host "Deployed $($OggFiles.Count) audio file(s) to $AudioDest" -ForegroundColor Green
    }
}

# --- Patch mods.json ---
# Ensures the mod entry has enabledForDlc covering both base game ("") and
# Spaced Out ("EXPANSION1_ID"), crash_count reset to 0, enabled = true.
# IMPORTANT: Must write UTF-8 WITHOUT BOM. PowerShell's -Encoding UTF8 adds
# a BOM which corrupts the file for Unity's Mono JSON parser, causing the
# game to silently discard all mod state and re-discover mods as disabled.
if (Test-Path $ModsJson) {
    $json = Get-Content $ModsJson -Raw -Encoding UTF8 | ConvertFrom-Json

    $found = $false
    foreach ($mod in $json.mods) {
        if ($mod.label.id -eq "OniAccess") {
            $mod.enabled = $true
            $mod.enabledForDlc = @("", "EXPANSION1_ID")
            $mod.crash_count = 0
            $mod.status = 1  # Status.Installed
            $found = $true
            break
        }
    }

    if (-not $found) {
        Write-Host "Mod entry not found in mods.json - game will discover it on next launch." -ForegroundColor Yellow
        Write-Host "Enable it in the Mods screen, then future deploys will keep it enabled."
    } else {
        $jsonText = $json | ConvertTo-Json -Depth 4
        [System.IO.File]::WriteAllText($ModsJson, $jsonText, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Patched mods.json - mod is enabled." -ForegroundColor Green
    }
} else {
    Write-Host "mods.json not found - game will create it on first launch." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done. Launch the game." -ForegroundColor Cyan
