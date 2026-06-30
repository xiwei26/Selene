# build.ps1
# Build SeleneNative Windows version

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "Building SeleneNative Windows version..."
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"

$ProjectPath = "src/SeleneNative/SeleneNative.csproj"
$TestProjectPath = "tests/SeleneNative.Tests/SeleneNative.Tests.csproj"
$CoreProjectPath = "src/SeleneNative.Core/SeleneNative.Core.csproj"
$CommonProperties = @("-p:Platform=$Platform")

function Remove-BuildDirectory {
    param([string]$RelativePath)

    $rootPath = [System.IO.Path]::GetFullPath($PSScriptRoot)
    $targetPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $RelativePath))
    $rootWithSeparator = $rootPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $targetPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "Refusing to clean path outside native-windows: $targetPath"
        exit 1
    }

    if (Test-Path $targetPath) {
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            try {
                Remove-Item -LiteralPath $targetPath -Recurse -Force -ErrorAction Stop
                return
            }
            catch {
                if ($attempt -eq 1) {
                    dotnet build-server shutdown | Out-Null
                }

                if ($attempt -eq 3) {
                    Write-Host "Warning: could not fully clean $targetPath. It may be in use by another process."
                    return
                }

                Start-Sleep -Milliseconds 500
            }
        }
    }
}

$ProjectXml = [xml](Get-Content -Raw $ProjectPath)
$TargetFramework = @($ProjectXml.Project.PropertyGroup | ForEach-Object { $_.TargetFramework } | Where-Object { $_ })[0]
if (-not $TargetFramework) {
    Write-Host "Error: TargetFramework not found in $ProjectPath"
    exit 1
}

# Check .NET SDK
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: .NET SDK not found, please install .NET 8 SDK first"
    exit 1
}

# Clean
Write-Host "Cleaning..."
dotnet clean $CoreProjectPath -c $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Host "Core clean failed"
    exit 1
}

dotnet clean $ProjectPath -c $Configuration @CommonProperties
if ($LASTEXITCODE -ne 0) {
    Write-Host "Clean failed"
    exit 1
}

dotnet clean $TestProjectPath -c $Configuration @CommonProperties
if ($LASTEXITCODE -ne 0) {
    Write-Host "Test clean failed"
    exit 1
}

Remove-BuildDirectory "src/SeleneNative/bin"
Remove-BuildDirectory "src/SeleneNative/obj"
Remove-BuildDirectory "src/SeleneNative.Core/bin"
Remove-BuildDirectory "src/SeleneNative.Core/obj"
Remove-BuildDirectory "tests/SeleneNative.Tests/bin"
Remove-BuildDirectory "tests/SeleneNative.Tests/obj"
Remove-BuildDirectory "publish"

# Build
Write-Host "Building..."
dotnet build $ProjectPath -c $Configuration @CommonProperties
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed"
    exit 1
}

# Run tests
Write-Host "Running tests..."
dotnet test $TestProjectPath -c $Configuration @CommonProperties
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed"
    exit 1
}

# Publish
Write-Host "Publishing..."
$RuntimeIdentifier = "win-$Platform"
$PublishPath = Join-Path $PSScriptRoot "publish/$RuntimeIdentifier"
$RunningAppProcesses = Get-Process -Name "SeleneNative" -ErrorAction SilentlyContinue
if ($RunningAppProcesses) {
    $RunningAppProcessIds = ($RunningAppProcesses | ForEach-Object { $_.Id }) -join ", "
    Write-Host "Error: SeleneNative is still running (PID: $RunningAppProcessIds). Close it before publishing."
    exit 1
}

if (Test-Path $PublishPath) {
    Remove-Item -LiteralPath $PublishPath -Recurse -Force
}

dotnet publish $ProjectPath -c $Configuration @CommonProperties -r $RuntimeIdentifier --self-contained -o $PublishPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed"
    exit 1
}

$PublishedExe = Join-Path $PublishPath "SeleneNative.exe"
if (-not (Test-Path $PublishedExe)) {
    Write-Host "Publish failed: SeleneNative.exe was not found in $PublishPath"
    exit 1
}

$WindowsAppRuntimePayload = Join-Path $PublishPath "Microsoft.UI.Xaml.Controls.pri"
if (-not (Test-Path $WindowsAppRuntimePayload)) {
    Write-Host "Publish failed: Windows App SDK self-contained payload was not found in $PublishPath"
    exit 1
}

Write-Host "Build completed!"
Write-Host "Output directory: $PublishPath"
