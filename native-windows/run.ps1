# run.ps1
# Run SeleneNative Windows version

param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64"
)

Write-Host "Running SeleneNative..."

# Check if built
$runtimeIdentifier = "win-$Platform"
$publishPath = Join-Path $PSScriptRoot "publish/$runtimeIdentifier/SeleneNative.exe"
if (-not (Test-Path $publishPath)) {
    Write-Host "Executable not found, building..."
    & "$PSScriptRoot\build.ps1" -Configuration $Configuration -Platform $Platform
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed"
        exit 1
    }
}

# Run
Write-Host "Starting application..."
Start-Process $publishPath -WorkingDirectory (Split-Path $publishPath)
