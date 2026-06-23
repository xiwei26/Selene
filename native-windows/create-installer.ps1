# create-installer.ps1
# Automates compiling SeleneNative and building the Inno Setup installer.

# 1. Build and Publish the application
Write-Host "Step 1: Building and publishing the application in Release mode..." -ForegroundColor Cyan
if (Test-Path "build.ps1") {
    .\build.ps1 -Configuration Release -Platform x64
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed. Aborting installer creation."
        exit 1
    }
} else {
    Write-Error "Could not find build.ps1 in the current directory."
    exit 1
}

# 2. Locate the Inno Setup compiler (ISCC.exe)
Write-Host "`nStep 2: Locating Inno Setup compiler (ISCC.exe)..." -ForegroundColor Cyan
$isccPath = $null

# Check environment path
$isccCommand = Get-Command iscc -ErrorAction SilentlyContinue
if ($isccCommand) {
    $isccPath = $isccCommand.Source
} else {
    # Check standard installation locations
    $searchPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
        "C:\Program Files\Inno Setup 5\ISCC.exe",
        "$env:USERPROFILE\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
    )

    foreach ($path in $searchPaths) {
        if (Test-Path $path) {
            $isccPath = $path
            break
        }
    }
}

if (-not $isccPath) {
    Write-Host "Error: Inno Setup compiler (ISCC.exe) was not found." -ForegroundColor Red
    Write-Host "Please install Inno Setup 6 using winget by running:" -ForegroundColor Yellow
    Write-Host "  winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
    Write-Host "Then run this script again." -ForegroundColor Yellow
    exit 1
}

Write-Host "Found ISCC.exe at: $isccPath" -ForegroundColor Green

# 3. Run the installer compiler
Write-Host "`nStep 3: Compiling installer using Inno Setup..." -ForegroundColor Cyan
& $isccPath installer.iss

if ($LASTEXITCODE -ne 0) {
    Write-Error "Installer compilation failed."
    exit 1
}

$installerOutput = "publish\SeleneSetup.exe"
if (Test-Path $installerOutput) {
    Write-Host "`nSuccess! Installer created successfully." -ForegroundColor Green
    Write-Host "Installer location: $((Get-Item $installerOutput).FullName)" -ForegroundColor Green
} else {
    Write-Error "Installer compilation reported success but output file was not found."
    exit 1
}
