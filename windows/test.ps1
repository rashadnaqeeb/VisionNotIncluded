# test.ps1 - Build and run the offline test suite on Windows.
# Windows counterpart of test.sh, run from the repo root:
#   powershell -ExecutionPolicy Bypass -File windows\test.ps1
# Tests the handler stack contracts without requiring the game.

$ErrorActionPreference = "Stop"
$Repo = Split-Path -Parent $PSScriptRoot

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

$TestProject = "$Repo\OniAccess.Tests\OniAccess.Tests.csproj"
$TestExe     = "$Repo\OniAccess.Tests\bin\Debug\net48\OniAccess.Tests.exe"

Write-Host "Building tests..." -ForegroundColor Cyan
dotnet build $TestProject -c Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}

Write-Host ""
# U60 game assemblies use Span APIs absent from desktop .NET Framework.
# The vendored Mono.CSharp evaluator has the opposite requirement: it calls
# AppDomain.DefineDynamicAssembly, which modern .NET removed. Run every test
# once, under the runtime its dependencies support. The Mac Mono path stays
# a single invocation of the complete suite.
Write-Host "Running game and handler tests on .NET 8 or newer..." -ForegroundColor Cyan
dotnet exec --runtimeconfig "$PSScriptRoot\test.runtimeconfig.json" $TestExe --exclude-evaluator
$CoreExit = $LASTEXITCODE

Write-Host ""
Write-Host "Running evaluator tests on .NET Framework..." -ForegroundColor Cyan
& $TestExe --evaluator-only
$EvaluatorExit = $LASTEXITCODE

if ($CoreExit -ne 0) { exit $CoreExit }
exit $EvaluatorExit
