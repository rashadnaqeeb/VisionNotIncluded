# build.ps1 - Build and deploy OniAccess to the Windows game's local mods directory.
# Windows counterpart of build.sh, run from the repo root:
#   powershell -ExecutionPolicy Bypass -File windows\build.ps1 [-Module] [-Release] [-NoBuild]
# Builds the host (OniAccess.dll, loaded by the game) and the module
# (Module\OniAccess.Module.dll, byte-loaded by the host and hot-reloadable),
# deploys them, and patches mods.json to ensure the mod stays enabled (prevents
# the game from disabling it after crashes or version mismatches).
# Debug is the default; -Release is the shipping build without the dev server.

param(
    [switch]$NoBuild,
    [switch]$Module,
    [switch]$Release,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\windows\build.ps1 [-Module] [-Release] [-NoBuild] [-Help]"
    Write-Host "  -Module   Rebuild only the module and hot-reload it into the running game (POST /reload)"
    Write-Host "  -Release  Release configuration (no dev server); the shipping build"
    Write-Host "  -NoBuild  Skip building, just deploy the last build and patch mods.json"
    Write-Host "  -Help     Show this help"
    exit 0
}

$ErrorActionPreference = "Stop"
$Repo = Split-Path -Parent $PSScriptRoot
$Config = if ($Release) { "Release" } else { "Debug" }
$DevPort = if ($env:ONIACCESS_DEV_PORT) { $env:ONIACCESS_DEV_PORT } else { "8772" }

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

$HostProject   = "$Repo\OniAccess"
$ModuleProject = "$Repo\OniAccess.Module"
$HostDll       = "$HostProject\bin\$Config\net48\OniAccess.dll"
$DocsDir       = [Environment]::GetFolderPath("MyDocuments")
$ModDir        = "$DocsDir\Klei\OxygenNotIncluded\mods\local\OniAccess"
$ModuleDir     = "$ModDir\Module"
$ModsJson      = "$DocsDir\Klei\OxygenNotIncluded\mods\mods.json"

# --- Sync version from .csproj to mod_info.yaml ---
$CsprojPath = "$HostProject\OniAccess.csproj"
$ModInfoPath = "$HostProject\mod_info.yaml"
[xml]$csproj = Get-Content $CsprojPath
$Version = $csproj.Project.PropertyGroup.Version
$modInfo = Get-Content $ModInfoPath -Raw
$modInfo = $modInfo -replace 'version: ".*"', "version: `"$Version`""
[System.IO.File]::WriteAllText($ModInfoPath, $modInfo)

# --- Build (the module project references the host, so one build makes both) ---
if (-not $NoBuild) {
    Write-Host "Building OniAccess ($Config)..." -ForegroundColor Cyan
    dotnet build "$ModuleProject\OniAccess.Module.csproj" -c $Config
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build FAILED." -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $HostDll)) {
    Write-Host "ERROR: host DLL not found at $HostDll" -ForegroundColor Red
    exit 1
}
# The module's assembly name is timestamped per build; the project prunes older
# outputs, so the newest file is the one just built.
$ModuleDll = Get-ChildItem "$ModuleProject\bin\$Config\net48\OniAccess.Module_*.dll" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $ModuleDll) {
    Write-Host "ERROR: module DLL not found under $ModuleProject\bin\$Config\net48" -ForegroundColor Red
    exit 1
}

# --- Deploy the module (a hot-reload only ever needs this part) ---
# Copy then rename so the host never byte-loads a half-written file.
if (-not (Test-Path $ModuleDir)) {
    New-Item -ItemType Directory -Path $ModuleDir -Force | Out-Null
}
Copy-Item $ModuleDll.FullName "$ModuleDir\OniAccess.Module.dll.tmp" -Force
Move-Item "$ModuleDir\OniAccess.Module.dll.tmp" "$ModuleDir\OniAccess.Module.dll" -Force
Write-Host "Deployed module $($ModuleDll.Name)" -ForegroundColor Green
# The dev server's REPL compiler must sit at the mod root: the game's loader
# resolves the host's dependencies from there (and calls GetTypes on the host
# before any mod code could redirect the lookup). Release ships without it.
if ($Config -eq "Debug") {
    Copy-Item "$Repo\vendor\Mono.CSharp.dll" "$ModDir\Mono.CSharp.dll" -Force
} else {
    Remove-Item "$ModDir\Mono.CSharp.dll" -Force -ErrorAction SilentlyContinue
}

if ($Module) {
    if ((Test-Path "$ModDir\OniAccess.dll") -and
        (Get-FileHash $HostDll).Hash -ne (Get-FileHash "$ModDir\OniAccess.dll").Hash) {
        Write-Host "WARNING: the built host differs from the deployed one. Host changes need a full build.ps1 and a game restart." -ForegroundColor Yellow
    }
    $health = curl.exe -s --max-time 2 "http://127.0.0.1:$DevPort/health"
    if ($health -match "ok") {
        Write-Host "Reloading the module in the running game..." -ForegroundColor Cyan
        curl.exe -s --max-time 90 -X POST "http://127.0.0.1:$DevPort/reload"
    } else {
        Write-Host "Dev server not answering on port $DevPort; the new module loads on the next game launch."
    }
    exit 0
}

# --- Copy the host DLL and native dependencies ---
Copy-Item $HostDll "$ModDir\OniAccess.dll" -Force
Copy-Item "$HostProject\mod_info.yaml" "$ModDir\mod_info.yaml" -Force
Copy-Item "$HostProject\mod.yaml" "$ModDir\mod.yaml" -Force

# Deploy platform-specific Prism native library.
# For local development, only the Windows binary is needed.
$PrismSrc = "$Repo\prism\native\win-x64"
$NativeDir = "$ModDir\native\win-x64"
if (-not (Test-Path $NativeDir)) {
    New-Item -ItemType Directory -Path $NativeDir -Force | Out-Null
}
Copy-Item "$PrismSrc\prism.dll" "$NativeDir\prism.dll" -Force
Write-Host "Deployed Prism native library to $NativeDir" -ForegroundColor Green

# --- Copy translation files ---
$TranslationsSrc = "$Repo\translations"
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
$AudioSrc = "$Repo\audio"
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
        # A crash while mods were loading leaves this set, and the next boot then
        # enters mod safe mode and disables every mod again.
        $json.mod_load_in_progress = $false
        $jsonText = $json | ConvertTo-Json -Depth 4
        [System.IO.File]::WriteAllText($ModsJson, $jsonText, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Patched mods.json - mod is enabled." -ForegroundColor Green
    }
} else {
    Write-Host "mods.json not found - game will create it on first launch." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Done. Launch the game." -ForegroundColor Cyan
