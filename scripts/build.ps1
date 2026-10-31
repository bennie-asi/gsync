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
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?(?:\+[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw "Version must be a semantic version without path separators or shell metacharacters."
}
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
$UpdaterProject = Join-Path $RepoRoot "src/GSYNC.Updater/GSYNC.Updater.csproj"
$OutRoot    = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $RepoRoot $OutputPath
}
$AppOut     = Join-Path $OutRoot "app"
$ZipName    = "GSYNC-$Version-win-x64.zip"
$ZipPath    = Join-Path $OutRoot $ZipName

# Validate deletion targets before touching a previous build. A linked app folder must never redirect cleanup.
$OutRoot = [System.IO.Path]::GetFullPath($OutRoot)
$AppOut = [System.IO.Path]::GetFullPath((Join-Path $OutRoot "app"))
$ZipPath = [System.IO.Path]::GetFullPath((Join-Path $OutRoot $ZipName))
$OutputPrefix = $OutRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $AppOut.StartsWith($OutputPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not $ZipPath.StartsWith($OutputPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Build artifacts must remain inside the requested output directory."
}
if ((Test-Path -LiteralPath $AppOut) -and
    (((Get-Item -LiteralPath $AppOut -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)) {
    throw "The app output folder is a symbolic link or junction; choose another output directory."
}

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
    $TestProjects = @(Get-ChildItem -LiteralPath (Join-Path $RepoRoot "tests") -Filter "*.Tests.csproj" -File -Recurse | Sort-Object FullName)
    if ($TestProjects.Count -eq 0) { throw "No test projects were found." }
    foreach ($TestProject in $TestProjects) {
        $ProjectResultsDir = Join-Path $TestResultsDir $TestProject.BaseName
        Invoke-Step "Run tests: $($TestProject.BaseName)" {
            dotnet test $TestProject.FullName `
                --no-restore `
                --configuration $Configuration `
                --logger "console;verbosity=minimal" `
                --logger "trx;LogFileName=results.trx" `
                --results-directory $ProjectResultsDir
        }
    }
}

if (Test-Path -LiteralPath $AppOut) {
    Remove-Item -LiteralPath $AppOut -Recurse -Force
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

Invoke-Step "Publish standalone update helper" {
    dotnet publish $UpdaterProject `
        --configuration $Configuration `
        --runtime win-x64 `
        --self-contained false `
        -p:Platform=x64 `
        -p:PublishSingleFile=true `
        -p:DebugType=None `
        --output (Join-Path $AppOut "Updater")
}

Write-Step "Record managed program files"
$ManagedName = "GSYNC.managed.json"
$ManagedFiles = @(Get-ChildItem -LiteralPath $AppOut -File -Recurse | ForEach-Object {
    [System.IO.Path]::GetRelativePath($AppOut, $_.FullName).Replace('\', '/')
}) + @($ManagedName)
$ManagedFiles = @($ManagedFiles | Sort-Object)
[System.IO.File]::WriteAllText((Join-Path $AppOut $ManagedName), (ConvertTo-Json -InputObject $ManagedFiles -Compress), [System.Text.UTF8Encoding]::new($false))
Write-Ok "Recorded $($ManagedFiles.Count) managed files"

Invoke-Step "Package  →  $ZipName" {
    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    Compress-Archive -Path "$AppOut/*" -DestinationPath $ZipPath
}

# ── Summary ───────────────────────────────────────────────────────────────────

$Elapsed = ((Get-Date) - $StartTime).TotalSeconds
Write-Host ""
Write-Host "  Build complete in $($Elapsed.ToString('F1')) s" -ForegroundColor Green
Write-Host "  Archive : $ZipPath"
Write-Host ""
