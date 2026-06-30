# build-installer.ps1
# Build the native Windows app and package it as an installable Inno Setup exe.

param(
    [string]$Configuration = "Release",
    [ValidateSet("x86", "x64", "arm64")]
    [string]$Platform = "x64",
    [string]$Version = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$nativeRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$runtimeIdentifier = "win-$Platform"
$publishDir = Join-Path $nativeRoot "publish\$runtimeIdentifier"
$distDir = Join-Path $nativeRoot "dist"
$installerScript = Join-Path $nativeRoot "installer.iss"
$repoIcon = Join-Path $root "logo.ico"

function Find-InnoCompiler {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $command = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "Inno Setup 6 compiler (ISCC.exe) was not found. Install Inno Setup 6 first."
}

function Resolve-AppVersion {
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        return $Version
    }

    $pubspecPath = Join-Path $root "pubspec.yaml"
    if (Test-Path $pubspecPath) {
        $versionLine = Get-Content $pubspecPath | Where-Object { $_ -match "^\s*version\s*:" } | Select-Object -First 1
        if ($versionLine -and $versionLine -match "version\s*:\s*([0-9]+(?:\.[0-9]+){1,3})") {
            return $Matches[1]
        }
    }

    return "1.0.0"
}

if (-not (Test-Path $installerScript)) {
    throw "Installer script not found: $installerScript"
}

if (-not (Test-Path $repoIcon)) {
    throw "Installer icon not found: $repoIcon"
}

if (-not $SkipBuild) {
    & (Join-Path $nativeRoot "build.ps1") -Configuration $Configuration -Platform $Platform
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$publishedExe = Join-Path $publishDir "SeleneNative.exe"
if (-not (Test-Path $publishedExe)) {
    throw "Published app not found: $publishedExe"
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

$appVersion = Resolve-AppVersion
$iscc = Find-InnoCompiler

Write-Host "Packaging Selene installer..."
Write-Host "Version: $appVersion"
Write-Host "Source: $publishDir"
Write-Host "Output: $distDir"

& $iscc `
    "/DAppVersion=$appVersion" `
    "/DPlatform=$Platform" `
    "/DSourceDir=$publishDir" `
    "/DOutputDir=$distDir" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$installerPath = Join-Path $distDir "selene-$appVersion-windows-$Platform-setup.exe"
if (-not (Test-Path $installerPath)) {
    throw "Installer build completed, but expected output was not found: $installerPath"
}

Write-Host "Installer completed!"
Write-Host "Installer: $installerPath"
