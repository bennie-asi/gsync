<#
.SYNOPSIS
    One-click build script for GSYNC desktop application.

.DESCRIPTION
    Restores packages, runs tests, publishes the WinUI 3 app for win-x64,
    and packages the output as a ZIP artifact.

    Locally:
        .\scripts\build.ps1
        .\scripts\build.ps1 -Version 1.2.0
        .\scripts\build.ps1 -Version 1.2.0-beta.1 -SkipTests
        .\scripts\build.ps1 -Version 1.0.0 -Configuration Debug

    From GitHub Actions the script is invoked with -Version provided by the workflow.

.PARAMETER Version
    Semantic version string, e.g. 1.2.3 or 1.2.3-beta.1.
    Defaults to 0.0.0-local when omitted.

.PARAMETER Configuration
    MSBuild configuration: Debug or Release (default).

.PARAMETER SkipTests
    Skip the test suite.

.PARAMETER OutputPath
    Root output directory. Relative paths are resolved from the repo root.
    Defaults to "publish".
#>
[CmdletBinding()]
param(
    [string]$Version = "0.0.0-local",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [string]$OutputPath = "publish"
)

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

# ── Helpers ──────────────────────────────────────────────────────────────────

function Write-Step([string]$Msg) {
    Write-Host ""
    Write-Host "==> $Msg" -ForegroundColor Cyan
}

function Write-Ok([string]$Msg) {
    Write-Host " OK  $Msg" -ForegroundColor Green
}

function Write-Fail([string]$Msg) {
    Write-Host "FAIL $Msg" -ForegroundColor Red
}

function Invoke-Step([string]$Label, [scriptblock]$Body) {
    Write-Step $Label
    & $Body
    if ($LASTEXITCODE -ne 0) {
        Write-Fail $Label
        exit $LASTEXITCODE
    }
    Write-Ok $Label
}

# ── Paths ─────────────────────────────────────────────────────────────────────

$RepoRoot   = Split-Path $PSScriptRoot -Parent
$Solution   = Join-Path $RepoRoot "GSYNC.sln"
$AppProject = Join-Path $RepoRoot "src/GSYNC.App/GSYNC.App.csproj"
$OutRoot    = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $RepoRoot $OutputPath
}
$AppOut     = Join-Path $OutRoot "app"
$ZipName    = "GSYNC-$Version-win-x64.zip"
$ZipPath    = Join-Path $OutRoot $ZipName

# ── Version parsing ───────────────────────────────────────────────────────────
# AssemblyVersion / FileVersion require numeric X.Y.Z.W  (strip pre-release label)

$NumericVersion = ($Version -replace '-[^+]*', '') -replace '\+.*', ''
if ($NumericVersion -notmatch '^\d+(\.\d+){0,3}$') {
    $NumericVersion = "0.0.0.0"
}
$NumericParts = $NumericVersion.Split('.')
while ($NumericParts.Count -lt 4) { $NumericParts += "0" }
$AssemblyVersion = $NumericParts[0..3] -join "."

# ── Banner ────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  GSYNC Build  " -ForegroundColor White -BackgroundColor DarkBlue
Write-Host "  Version          : $Version"
Write-Host "  Assembly version : $AssemblyVersion"
Write-Host "  Configuration    : $Configuration"
Write-Host "  Output           : $OutRoot"
Write-Host "  Skip tests       : $([bool]$SkipTests)"

# ── Steps ─────────────────────────────────────────────────────────────────────

Invoke-Step "Restore packages" {
    dotnet restore $Solution
}

if (-not $SkipTests) {
    $TestResultsDir = Join-Path $OutRoot "test-results"
    Invoke-Step "Run tests" {
        dotnet test $Solution `
            --no-restore `
            --configuration $Configuration `
            --logger "console;verbosity=minimal" `
            --logger "trx;LogFileName=results.trx" `
            --results-directory $TestResultsDir
    }
}

if (Test-Path $AppOut) {
    Remove-Item $AppOut -Recurse -Force
}

Invoke-Step "Publish GSYNC.App  ($Configuration / win-x64)" {
    dotnet publish $AppProject `
        --no-restore `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained false `
        -p:Platform=x64 `
        -p:Version=$Version `
        -p:AssemblyVersion=$AssemblyVersion `
        -p:FileVersion=$AssemblyVersion `
        -p:InformationalVersion=$Version `
        --output $AppOut
}

Invoke-Step "Package  →  $ZipName" {
    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    Compress-Archive -Path "$AppOut/*" -DestinationPath $ZipPath
}

# ── Summary ───────────────────────────────────────────────────────────────────

$Elapsed = ((Get-Date) - $StartTime).TotalSeconds
Write-Host ""
Write-Host "  Build complete in $($Elapsed.ToString('F1')) s" -ForegroundColor Green
Write-Host "  Archive : $ZipPath"
Write-Host ""
